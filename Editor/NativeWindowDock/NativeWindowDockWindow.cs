using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DansToolbox.EditorTools.NativeWindowDock
{
    internal enum NativeWindowCropEdge
    {
        Left,
        Top,
        Right,
        Bottom
    }

    internal sealed class NativeWindowDockWindow : EditorWindow
    {
        private const float Margin = 10f;
        private const float HeaderHeight = 36f;
        private const float ToolbarHeight = 42f;
        private const float LaunchPanelHeight = 82f;
        private const float CropPanelHeight = 42f;
        private const float PickerMinimumHeight = 190f;
        private const float PickerMaximumHeight = 420f;
        private const float PickerCardMinimumWidth = 184f;
        private const float PickerCardGap = 8f;
        private const float CropHandleThickness = 10f;
        private const float StatusHeight = 24f;
        private const int MaxHorizontalCrop = 1200;
        private const int MaxVerticalCrop = 800;
        private const double LaunchTimeoutSeconds = 15d;
        private const double RepaintIntervalSeconds = 1d / 30d;
        private const string BaseTitle = "Native Dock";

        private static readonly FieldInfo ParentField = typeof(EditorWindow).GetField(
            "m_Parent",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField] private int selectedIndex;
        [SerializeField] private bool showLaunchPanel;
        [SerializeField] private bool showWindowPicker;
        [SerializeField] private bool showCropPanel;
        [SerializeField] private Vector2 windowPickerScroll;
        [SerializeField] private string launchPath = string.Empty;
        [SerializeField] private string launchArguments = string.Empty;
        [SerializeField] private int cropLeft;
        [SerializeField] private int cropTop;
        [SerializeField] private int cropRight;
        [SerializeField] private int cropBottom;
        [SerializeField] private int panelNumber;

        private IReadOnlyList<NativeWindowCandidate> candidates =
            Array.Empty<NativeWindowCandidate>();
        private NativeWindowSession session;
        private IntPtr claimedTarget;
        private string attachedLabel = string.Empty;
        private string cropProfileKey = string.Empty;
        private string statusMessage =
            "READY  ·  choose a running window or launch an application";
        private Color statusColor = NativeWindowDockGui.Muted;
        private bool pendingLaunch;
        private int launchedProcessId;
        private double launchDeadline;
        private double nextLaunchPoll;
        private double nextRepaint;
        private double lastPaint;
        private HashSet<long> windowsBeforeLaunch = new HashSet<long>();
        private Vector2 cropDragStartMouse;
        private NativeWindowCrop cropDragStart;
        private readonly Dictionary<long, Texture2D> candidateThumbnails =
            new Dictionary<long, Texture2D>();
        private readonly HashSet<long> failedThumbnailHandles = new HashSet<long>();
        private readonly HashSet<long> pendingThumbnailHandles = new HashSet<long>();
        private readonly ConcurrentQueue<QueuedThumbnail> queuedThumbnails =
            new ConcurrentQueue<QueuedThumbnail>();
        private CancellationTokenSource thumbnailCancellation;
        private int thumbnailGeneration;

        private sealed class QueuedThumbnail
        {
            internal QueuedThumbnail(int generation, NativeWindowThumbnailData data)
            {
                Generation = generation;
                Data = data;
            }

            internal int Generation { get; }
            internal NativeWindowThumbnailData Data { get; }
        }

        private static bool IsWindowsEditor
        {
            get
            {
#if UNITY_EDITOR_WIN
                return true;
#else
                return false;
#endif
            }
        }

        [MenuItem("Tools/Dans Toolbox/Native Window Dock")]
        private static void Open()
        {
            CreatePanel();
        }

        private static NativeWindowDockWindow CreatePanel()
        {
            NativeWindowDockWindow window = CreateWindow<NativeWindowDockWindow>();
            window.EnsurePanelIdentity();
            window.minSize = new Vector2(520f, 340f);
            window.Show();
            window.Focus();
            return window;
        }

        private void OnEnable()
        {
            EnsurePanelIdentity();
            UpdateTitle();
            minSize = new Vector2(520f, 340f);
            wantsMouseMove = true;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += DetachForReload;
            if (IsWindowsEditor)
            {
                RefreshCandidates();
            }
            else
            {
                SetStatus("UNSUPPORTED  ·  native embedding currently requires the Windows Editor",
                    NativeWindowDockGui.Warning);
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= DetachForReload;
            ResetThumbnailPreviews();
            Detach(false);
        }

        private void OnFocus()
        {
            if (session == null && IsWindowsEditor)
            {
                RefreshCandidates();
            }
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Repaint)
            {
                lastPaint = EditorApplication.timeSinceStartup;
            }

            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height),
                NativeWindowDockGui.Canvas);

            Rect headerRect = new Rect(Margin, Margin, position.width - Margin * 2, HeaderHeight);
            Rect toolbarRect = new Rect(
                Margin,
                headerRect.yMax + 6,
                position.width - Margin * 2,
                ToolbarHeight);
            DrawHeader(headerRect);
            DrawToolbar(toolbarRect);

            float pickerPanelHeight = showWindowPicker && session == null
                ? Mathf.Clamp(position.height * 0.48f, PickerMinimumHeight, PickerMaximumHeight)
                : 0f;
            float pickerHeight = pickerPanelHeight > 0f ? pickerPanelHeight + 6f : 0f;
            float launchHeight = showLaunchPanel && !showWindowPicker && session == null
                ? LaunchPanelHeight + 6
                : 0;
            float cropHeight = showCropPanel && session != null ? CropPanelHeight + 6 : 0;
            if (pickerHeight > 0f)
            {
                Rect pickerRect = new Rect(
                    Margin,
                    toolbarRect.yMax + 6,
                    position.width - Margin * 2,
                    pickerPanelHeight);
                DrawWindowPicker(pickerRect);
            }

            if (launchHeight > 0)
            {
                Rect launchRect = new Rect(
                    Margin,
                    toolbarRect.yMax + 6,
                    position.width - Margin * 2,
                    LaunchPanelHeight);
                DrawLaunchPanel(launchRect);
            }

            if (cropHeight > 0)
            {
                Rect cropRect = new Rect(
                    Margin,
                    toolbarRect.yMax + 6,
                    position.width - Margin * 2,
                    CropPanelHeight);
                DrawCropPanel(cropRect);
            }

            Rect statusRect = new Rect(
                Margin,
                position.height - Margin - StatusHeight,
                position.width - Margin * 2,
                StatusHeight);
            Rect viewportRect = new Rect(
                Margin,
                toolbarRect.yMax + 6 + pickerHeight + launchHeight + cropHeight,
                position.width - Margin * 2,
                Mathf.Max(
                    40,
                    statusRect.y - toolbarRect.yMax - 12 - pickerHeight - launchHeight - cropHeight));
            Rect embeddedViewportRect = GetEmbeddedViewportRect(viewportRect);
            DrawViewport(viewportRect);
            if (session != null && showCropPanel)
            {
                DrawCropBorders(viewportRect);
            }
            DrawStatus(statusRect);

            if (Event.current.type == EventType.Repaint && session != null)
            {
                Vector2 screenPoint = GUIUtility.GUIToScreenPoint(
                    embeddedViewportRect.position);
                try
                {
                    bool wasEmbedded = session.IsEmbedded;
                    session.Position(
                        new Rect(
                            screenPoint.x,
                            screenPoint.y,
                            embeddedViewportRect.width,
                            embeddedViewportRect.height),
                        EditorGUIUtility.pixelsPerPoint);
                    session.SetVisible(IsSelectedDockTab());
                    if (!wasEmbedded && session.IsEmbedded)
                    {
                        SetStatus(
                            "ATTACHED  ·  click the app to type; Detach restores its desktop window",
                            NativeWindowDockGui.Accent);
                    }
                }
                catch (Exception exception)
                {
                    NativeWindowSession failedSession = session;
                    session = null;
                    failedSession.Dispose();
                    attachedLabel = string.Empty;
                    SetStatus(
                        "ATTACH FAILED  ·  " + exception.Message,
                        NativeWindowDockGui.Danger);
                    Debug.LogWarning("Native Window Dock: " + exception);
                    Repaint();
                }
            }
        }

        private void DrawHeader(Rect rect)
        {
            NativeWindowDockGui.DrawPanel(
                rect,
                NativeWindowDockGui.Raised,
                NativeWindowDockGui.BorderStrong);
            NativeWindowDockGui.DrawSignalRail(
                new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 2f));

            string state = session != null ? "ATTACHED" : pendingLaunch ? "WAITING" : "READY";
            Color stateColor = session != null
                ? NativeWindowDockGui.Accent
                : pendingLaunch ? NativeWindowDockGui.Warning : NativeWindowDockGui.Muted;
            if (GUI.Button(
                    new Rect(rect.xMax - 108, rect.y + 4, 92, 28),
                    "NEW PANEL",
                    NativeWindowDockGui.Button))
            {
                CreatePanel();
            }

            Rect stateRect = new Rect(rect.x + 8, rect.y + 4, 104, 28);
            NativeWindowDockGui.DrawPanel(
                stateRect,
                NativeWindowDockGui.Inset,
                NativeWindowDockGui.Border);
            NativeWindowDockGui.DrawStatusDot(
                new Vector2(stateRect.x + 15, stateRect.center.y),
                stateColor);
            GUI.Label(
                new Rect(stateRect.x + 26, stateRect.y, stateRect.width - 30, stateRect.height),
                state,
                NativeWindowDockGui.Status);
        }

        private void DrawToolbar(Rect rect)
        {
            NativeWindowDockGui.DrawPanel(
                rect,
                NativeWindowDockGui.Raised,
                NativeWindowDockGui.Border);

            float y = rect.y + 7;
            if (session != null)
            {
                GUI.Label(
                    new Rect(rect.x + 12, y, Mathf.Max(120, rect.width - 300), 28),
                    attachedLabel,
                    NativeWindowDockGui.Body);
                if (GUI.Button(
                        new Rect(rect.xMax - 280, y, 72, 28),
                        "FOCUS",
                        NativeWindowDockGui.Button))
                {
                    session.Focus();
                }

                showCropPanel = GUI.Toggle(
                    new Rect(rect.xMax - 200, y, 92, 28),
                    showCropPanel,
                    new GUIContent("FRAME", "Drag the glowing viewport borders to crop the app."),
                    NativeWindowDockGui.Button);

                if (GUI.Button(
                        new Rect(rect.xMax - 100, y, 88, 28),
                        "DETACH",
                        NativeWindowDockGui.DangerButton))
                {
                    Detach(true);
                }

                return;
            }

            float rightControlsWidth = 212;
            Rect popupRect = new Rect(
                rect.x + 8,
                y,
                Mathf.Max(120, rect.width - rightControlsWidth - 16),
                28);
            string pickerLabel = candidates.Count == 0
                ? "CHOOSE WINDOW  /  NONE AVAILABLE"
                : "CHOOSE WINDOW  /  " + candidates[
                    Mathf.Clamp(selectedIndex, 0, candidates.Count - 1)].DisplayLabel;
            using (new EditorGUI.DisabledScope(!IsWindowsEditor))
            {
                bool nextPickerState = GUI.Toggle(
                    popupRect,
                    showWindowPicker,
                    new GUIContent(pickerLabel, "Open the visual window gallery"),
                    NativeWindowDockGui.WindowPickerButton);
                if (nextPickerState != showWindowPicker)
                {
                    showWindowPicker = nextPickerState;
                    if (showWindowPicker)
                    {
                        showLaunchPanel = false;
                        BeginThumbnailCapture();
                    }
                }
            }

            if (GUI.Button(
                    new Rect(rect.xMax - 204, y, 34, 28),
                    "↻",
                    NativeWindowDockGui.Button))
            {
                RefreshCandidates();
            }

            bool nextLaunchState = GUI.Toggle(
                new Rect(rect.xMax - 162, y, 72, 28),
                showLaunchPanel,
                "LAUNCH",
                NativeWindowDockGui.Button);
            if (nextLaunchState != showLaunchPanel)
            {
                showLaunchPanel = nextLaunchState;
                if (showLaunchPanel)
                {
                    showWindowPicker = false;
                }
            }

            using (new EditorGUI.DisabledScope(candidates.Count == 0 || !IsWindowsEditor))
            {
                if (GUI.Button(
                        new Rect(rect.xMax - 82, y, 74, 28),
                        "ATTACH",
                        NativeWindowDockGui.PrimaryButton))
                {
                    AttachSelected();
                }
            }
        }

        private void DrawWindowPicker(Rect rect)
        {
            NativeWindowDockGui.DrawPanel(
                rect,
                NativeWindowDockGui.Raised,
                NativeWindowDockGui.BorderStrong);
            NativeWindowDockGui.DrawSignalRail(
                new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 2f));

            Rect headerRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 38f);
            EditorGUI.DrawRect(headerRect, NativeWindowDockGui.Header);
            NativeWindowDockGui.DrawRackScrews(headerRect);
            GUI.Label(
                new Rect(headerRect.x + 28f, headerRect.y + 10f, 210f, 18f),
                "AVAILABLE WINDOWS",
                NativeWindowDockGui.CardTitle);

            if (GUI.Button(
                    new Rect(headerRect.xMax - 112f, headerRect.y + 5f, 68f, 28f),
                    "REFRESH",
                    NativeWindowDockGui.Button))
            {
                RefreshCandidates();
            }

            if (GUI.Button(
                    new Rect(headerRect.xMax - 38f, headerRect.y + 5f, 28f, 28f),
                    "X",
                    NativeWindowDockGui.Button))
            {
                showWindowPicker = false;
            }

            Rect scrollViewport = new Rect(
                rect.x + 8f,
                headerRect.yMax + 8f,
                rect.width - 16f,
                Mathf.Max(40f, rect.yMax - headerRect.yMax - 16f));
            if (candidates.Count == 0)
            {
                NativeWindowDockGui.DrawPanel(
                    scrollViewport,
                    NativeWindowDockGui.Inset,
                    NativeWindowDockGui.Border);
                NativeWindowDockGui.DrawTechnicalGrid(scrollViewport);
                GUI.Label(
                    new Rect(scrollViewport.x + 20f, scrollViewport.center.y - 20f,
                        scrollViewport.width - 40f, 22f),
                    "NO WINDOWS AVAILABLE",
                    NativeWindowDockGui.CenteredTitle);
                GUI.Label(
                    new Rect(scrollViewport.x + 30f, scrollViewport.center.y + 4f,
                        scrollViewport.width - 60f, 36f),
                    "Open an application, then select Refresh.",
                    NativeWindowDockGui.CenteredBody);
                return;
            }

            float gridWidth = Mathf.Max(1f, scrollViewport.width - 16f);
            int columns = CalculateWindowPickerColumnCount(gridWidth);
            float cardWidth = (gridWidth - PickerCardGap * (columns - 1)) / columns;
            float previewHeight = Mathf.Clamp(cardWidth * 0.5625f, 76f, 132f);
            float cardHeight = previewHeight + 50f;
            int rows = Mathf.CeilToInt(candidates.Count / (float)columns);
            float contentHeight = Mathf.Max(
                scrollViewport.height,
                rows * cardHeight + Mathf.Max(0, rows - 1) * PickerCardGap);
            Rect contentRect = new Rect(0f, 0f, gridWidth, contentHeight);

            windowPickerScroll = GUI.BeginScrollView(
                scrollViewport,
                windowPickerScroll,
                contentRect,
                false,
                contentHeight > scrollViewport.height);
            for (int index = 0; index < candidates.Count; index++)
            {
                int column = index % columns;
                int row = index / columns;
                Rect cardRect = new Rect(
                    column * (cardWidth + PickerCardGap),
                    row * (cardHeight + PickerCardGap),
                    cardWidth,
                    cardHeight);
                DrawWindowCard(cardRect, candidates[index], index, previewHeight);
            }
            GUI.EndScrollView();
        }

        private void DrawWindowCard(
            Rect rect,
            NativeWindowCandidate candidate,
            int index,
            float previewHeight)
        {
            bool selected = index == selectedIndex;
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color fill = selected
                ? new Color(0.29f, 0.22f, 0.15f)
                : hovered ? NativeWindowDockGui.Hover : NativeWindowDockGui.Header;
            Color border = selected
                ? NativeWindowDockGui.Accent
                : hovered ? NativeWindowDockGui.BorderStrong : NativeWindowDockGui.Border;
            NativeWindowDockGui.DrawPanel(rect, fill, border);

            Rect previewRect = new Rect(
                rect.x + 6f,
                rect.y + 6f,
                rect.width - 12f,
                previewHeight - 8f);
            NativeWindowDockGui.DrawPanel(
                previewRect,
                NativeWindowDockGui.Inset,
                selected ? NativeWindowDockGui.AccentSoft : NativeWindowDockGui.Border);

            Texture2D thumbnail;
            long handle = candidate.Handle.ToInt64();
            if (candidateThumbnails.TryGetValue(handle, out thumbnail) && thumbnail != null)
            {
                GUI.DrawTexture(
                    new Rect(previewRect.x + 1f, previewRect.y + 1f,
                        previewRect.width - 2f, previewRect.height - 2f),
                    thumbnail,
                    ScaleMode.ScaleAndCrop,
                    false);
            }
            else
            {
                NativeWindowDockGui.DrawTechnicalGrid(previewRect, 20f);
                string previewState = pendingThumbnailHandles.Contains(handle)
                    ? "CAPTURING PREVIEW..."
                    : failedThumbnailHandles.Contains(handle)
                        ? "PREVIEW UNAVAILABLE"
                        : "PREVIEW QUEUED";
                GUI.Label(
                    new Rect(previewRect.x + 8f, previewRect.center.y - 10f,
                        previewRect.width - 16f, 20f),
                    previewState,
                    NativeWindowDockGui.CenteredBody);
            }

            float textY = previewRect.yMax + 5f;
            GUI.Label(
                new Rect(rect.x + 8f, textY, rect.width - 16f, 17f),
                candidate.ProcessName,
                NativeWindowDockGui.CardTitle);
            GUI.Label(
                new Rect(rect.x + 8f, textY + 17f, rect.width - 16f, 16f),
                candidate.Title,
                NativeWindowDockGui.CardSubtitle);
            if (selected)
            {
                NativeWindowDockGui.DrawSignalRail(
                    new Rect(rect.x + 1f, rect.yMax - 3f, rect.width - 2f, 2f));
            }

            if (GUI.Button(
                    rect,
                    new GUIContent(string.Empty, candidate.DisplayLabel),
                    GUIStyle.none))
            {
                selectedIndex = index;
                showWindowPicker = false;
                SetStatus(
                    "SELECTED  /  " + candidate.DisplayLabel + "  /  press Attach",
                    NativeWindowDockGui.Accent);
            }
        }

        internal static int CalculateWindowPickerColumnCount(float availableWidth)
        {
            int columns = Mathf.FloorToInt(
                (Mathf.Max(1f, availableWidth) + PickerCardGap)
                / (PickerCardMinimumWidth + PickerCardGap));
            return Mathf.Clamp(columns, 1, 4);
        }

        private void DrawCropPanel(Rect rect)
        {
            NativeWindowDockGui.DrawPanel(
                rect,
                NativeWindowDockGui.Inset,
                NativeWindowDockGui.Border);

            GUI.Label(
                new Rect(rect.x + 12, rect.y + 8, Mathf.Max(120, rect.width - 250), 26),
                $"CROP  /  L {cropLeft}   T {cropTop}   R {cropRight}   B {cropBottom}  px",
                NativeWindowDockGui.Status);

            if (GUI.Button(
                    new Rect(rect.xMax - 226, rect.y + 8, 96, 26),
                    "DISCORD CHAT",
                    NativeWindowDockGui.PrimaryButton))
            {
                cropLeft = 312;
                cropTop = 0;
                cropRight = 240;
                cropBottom = 0;
                ApplyCrop();
            }

            if (GUI.Button(
                    new Rect(rect.xMax - 122, rect.y + 8, 110, 26),
                    "FULL WINDOW",
                    NativeWindowDockGui.Button))
            {
                cropLeft = 0;
                cropTop = 0;
                cropRight = 0;
                cropBottom = 0;
                ApplyCrop();
            }
        }

        private Rect GetEmbeddedViewportRect(Rect viewportRect)
        {
            if (session == null || !showCropPanel)
            {
                return viewportRect;
            }

            float inset = Mathf.Min(
                CropHandleThickness,
                Mathf.Max(0f, (Mathf.Min(viewportRect.width, viewportRect.height) - 2f) * 0.5f));
            return new Rect(
                viewportRect.x + inset,
                viewportRect.y + inset,
                Mathf.Max(1f, viewportRect.width - inset * 2f),
                Mathf.Max(1f, viewportRect.height - inset * 2f));
        }

        private void DrawCropBorders(Rect viewportRect)
        {
            float thickness = Mathf.Min(
                CropHandleThickness,
                Mathf.Max(1f, Mathf.Min(viewportRect.width, viewportRect.height) * 0.25f));
            Rect top = new Rect(viewportRect.x, viewportRect.y, viewportRect.width, thickness);
            Rect bottom = new Rect(
                viewportRect.x,
                viewportRect.yMax - thickness,
                viewportRect.width,
                thickness);
            Rect left = new Rect(
                viewportRect.x,
                viewportRect.y + thickness,
                thickness,
                Mathf.Max(1f, viewportRect.height - thickness * 2f));
            Rect right = new Rect(
                viewportRect.xMax - thickness,
                viewportRect.y + thickness,
                thickness,
                Mathf.Max(1f, viewportRect.height - thickness * 2f));

            DrawCropBorder(top, NativeWindowCropEdge.Top);
            DrawCropBorder(bottom, NativeWindowCropEdge.Bottom);
            DrawCropBorder(left, NativeWindowCropEdge.Left);
            DrawCropBorder(right, NativeWindowCropEdge.Right);
        }

        private void DrawCropBorder(Rect rect, NativeWindowCropEdge edge)
        {
            int controlId = GUIUtility.GetControlID(
                0x4E574400 + (int)edge,
                FocusType.Passive,
                rect);
            Event current = Event.current;
            MouseCursor cursor = edge == NativeWindowCropEdge.Left
                                 || edge == NativeWindowCropEdge.Right
                ? MouseCursor.ResizeHorizontal
                : MouseCursor.ResizeVertical;
            EditorGUIUtility.AddCursorRect(rect, cursor, controlId);

            if (current.type == EventType.Repaint)
            {
                bool active = GUIUtility.hotControl == controlId;
                bool hovered = rect.Contains(current.mousePosition);
                Color rail = active || hovered
                    ? NativeWindowDockGui.AccentHover
                    : NativeWindowDockGui.Accent;
                EditorGUI.DrawRect(rect, new Color(rail.r, rail.g, rail.b, active ? 0.95f : 0.72f));
                DrawCropGrip(rect, edge);
            }

            EventType eventType = current.GetTypeForControl(controlId);
            if (eventType == EventType.MouseDown
                && current.button == 0
                && rect.Contains(current.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                GUIUtility.keyboardControl = 0;
                cropDragStartMouse = current.mousePosition;
                cropDragStart = CurrentCrop();
                current.Use();
                return;
            }

            if (eventType == EventType.MouseDrag && GUIUtility.hotControl == controlId)
            {
                NativeWindowCrop adjusted = AdjustCropFromDrag(
                    cropDragStart,
                    edge,
                    current.mousePosition - cropDragStartMouse);
                SetCropValues(adjusted);
                ApplyCrop(false);
                current.Use();
                return;
            }

            if (eventType == EventType.MouseUp && GUIUtility.hotControl == controlId)
            {
                GUIUtility.hotControl = 0;
                ApplyCrop(true);
                SetStatus(
                    $"FRAMED  /  L {cropLeft}  T {cropTop}  R {cropRight}  B {cropBottom}",
                    NativeWindowDockGui.Accent);
                current.Use();
            }
        }

        private static void DrawCropGrip(Rect rect, NativeWindowCropEdge edge)
        {
            bool horizontalEdge = edge == NativeWindowCropEdge.Top
                                  || edge == NativeWindowCropEdge.Bottom;
            Rect grip = horizontalEdge
                ? new Rect(rect.center.x - 18f, rect.center.y - 1f, 36f, 2f)
                : new Rect(rect.center.x - 1f, rect.center.y - 18f, 2f, 36f);
            EditorGUI.DrawRect(grip, NativeWindowDockGui.Canvas);
        }

        internal static NativeWindowCrop AdjustCropFromDrag(
            NativeWindowCrop start,
            NativeWindowCropEdge edge,
            Vector2 delta)
        {
            int left = start.Left;
            int top = start.Top;
            int right = start.Right;
            int bottom = start.Bottom;
            switch (edge)
            {
                case NativeWindowCropEdge.Left:
                    left = Mathf.Clamp(left + Mathf.RoundToInt(delta.x), 0, MaxHorizontalCrop);
                    break;
                case NativeWindowCropEdge.Top:
                    top = Mathf.Clamp(top + Mathf.RoundToInt(delta.y), 0, MaxVerticalCrop);
                    break;
                case NativeWindowCropEdge.Right:
                    right = Mathf.Clamp(right - Mathf.RoundToInt(delta.x), 0, MaxHorizontalCrop);
                    break;
                case NativeWindowCropEdge.Bottom:
                    bottom = Mathf.Clamp(bottom - Mathf.RoundToInt(delta.y), 0, MaxVerticalCrop);
                    break;
            }

            return new NativeWindowCrop(left, top, right, bottom);
        }

        private void SetCropValues(NativeWindowCrop crop)
        {
            cropLeft = crop.Left;
            cropTop = crop.Top;
            cropRight = crop.Right;
            cropBottom = crop.Bottom;
        }

        private void DrawLaunchPanel(Rect rect)
        {
            NativeWindowDockGui.DrawPanel(
                rect,
                NativeWindowDockGui.Inset,
                NativeWindowDockGui.Border);

            float labelWidth = 52;
            float buttonWidth = 78;
            float rowWidth = rect.width - 24;
            float firstY = rect.y + 9;
            GUI.Label(new Rect(rect.x + 12, firstY + 4, labelWidth, 20), "EXE",
                NativeWindowDockGui.Status);
            launchPath = GUI.TextField(
                new Rect(rect.x + 12 + labelWidth, firstY, rowWidth - labelWidth - buttonWidth - 6, 26),
                launchPath ?? string.Empty,
                NativeWindowDockGui.TextField);
            if (GUI.Button(
                    new Rect(rect.xMax - buttonWidth - 12, firstY, buttonWidth, 26),
                    "BROWSE",
                    NativeWindowDockGui.Button))
            {
                string selected = EditorUtility.OpenFilePanel(
                    "Choose an application",
                    string.IsNullOrEmpty(launchPath)
                        ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                        : Path.GetDirectoryName(launchPath),
                    "exe");
                if (!string.IsNullOrEmpty(selected))
                {
                    launchPath = selected;
                }
            }

            float secondY = firstY + 34;
            GUI.Label(new Rect(rect.x + 12, secondY + 4, labelWidth, 20), "ARGS",
                NativeWindowDockGui.Status);
            launchArguments = GUI.TextField(
                new Rect(rect.x + 12 + labelWidth, secondY, rowWidth - labelWidth - 120 - 6, 26),
                launchArguments ?? string.Empty,
                NativeWindowDockGui.TextField);

            if (pendingLaunch)
            {
                if (GUI.Button(
                        new Rect(rect.xMax - 132, secondY, 120, 26),
                        "CANCEL WAIT",
                        NativeWindowDockGui.DangerButton))
                {
                    pendingLaunch = false;
                    SetStatus("READY  ·  launch wait cancelled", NativeWindowDockGui.Muted);
                }
            }
            else if (GUI.Button(
                         new Rect(rect.xMax - 132, secondY, 120, 26),
                         "LAUNCH + ATTACH",
                         NativeWindowDockGui.PrimaryButton))
            {
                LaunchAndAttach();
            }
        }

        private void DrawViewport(Rect rect)
        {
            NativeWindowDockGui.DrawPanel(
                rect,
                NativeWindowDockGui.Inset,
                session != null ? NativeWindowDockGui.Accent : NativeWindowDockGui.Border);
            if (session != null)
            {
                return;
            }

            NativeWindowDockGui.DrawTechnicalGrid(
                new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f));

            float centerY = rect.center.y;
            if (!IsWindowsEditor)
            {
                GUI.Label(
                    new Rect(rect.x + 30, centerY - 42, rect.width - 60, 26),
                    "WINDOWS EDITOR REQUIRED",
                    NativeWindowDockGui.CenteredTitle);
                GUI.Label(
                    new Rect(rect.x + 50, centerY - 8, rect.width - 100, 52),
                    "This tool uses native Win32 parenting. It stays editor-only and does not affect player builds.",
                    NativeWindowDockGui.CenteredBody);
                return;
            }

            GUI.Label(
                new Rect(rect.x + 30, centerY - 13, rect.width - 60, 26),
                pendingLaunch ? "WAITING FOR WINDOW…" : "CHOOSE WINDOW",
                NativeWindowDockGui.CenteredTitle);
        }

        private void DrawStatus(Rect rect)
        {
            NativeWindowDockGui.DrawPanel(
                rect,
                NativeWindowDockGui.Raised,
                NativeWindowDockGui.Border);
            NativeWindowDockGui.DrawSignalRail(
                new Rect(rect.x + 1f, rect.yMax - 2f, rect.width - 2f, 1f));
            NativeWindowDockGui.DrawStatusDot(
                new Vector2(rect.x + 12, rect.center.y),
                statusColor);
            GUI.Label(
                new Rect(rect.x + 23, rect.y, rect.width - 30, rect.height),
                statusMessage,
                NativeWindowDockGui.Status);
        }

        private void RefreshCandidates()
        {
            if (!IsWindowsEditor)
            {
                return;
            }

            ResetThumbnailPreviews();

            IntPtr previousHandle = candidates.Count > 0
                && selectedIndex >= 0
                && selectedIndex < candidates.Count
                    ? candidates[selectedIndex].Handle
                    : IntPtr.Zero;
            try
            {
                candidates = NativeWindowInterop.EnumerateCandidates();
                candidates = candidates
                    .Where(candidate => !NativeWindowClaimRegistry.IsClaimedByOther(
                        candidate.Handle,
                        GetInstanceID()))
                    .ToArray();
                int previousIndex = candidates
                    .Select((candidate, index) => new { candidate, index })
                    .Where(item => item.candidate.Handle == previousHandle)
                    .Select(item => item.index)
                    .DefaultIfEmpty(0)
                    .First();
                selectedIndex = Mathf.Clamp(previousIndex, 0, Math.Max(0, candidates.Count - 1));
                if (!pendingLaunch && session == null)
                {
                    SetStatus(
                        candidates.Count == 0
                            ? "EMPTY  ·  no interactive top-level application windows found"
                            : $"READY  ·  {candidates.Count} application window(s) available",
                        candidates.Count == 0
                            ? NativeWindowDockGui.Warning
                            : NativeWindowDockGui.Muted);
                }
            }
            catch (Exception exception)
            {
                candidates = Array.Empty<NativeWindowCandidate>();
                SetStatus("ERROR  ·  " + exception.Message, NativeWindowDockGui.Danger);
            }

            if (showWindowPicker)
            {
                BeginThumbnailCapture();
            }

            Repaint();
        }

        private void BeginThumbnailCapture()
        {
            if (!IsWindowsEditor || candidates.Count == 0)
            {
                return;
            }

            if (thumbnailCancellation != null)
            {
                thumbnailCancellation.Cancel();
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            thumbnailCancellation = cancellation;
            CancellationToken token = cancellation.Token;
            int generation = thumbnailGeneration;
            IntPtr[] handles = candidates
                .Select(candidate => candidate.Handle)
                .Where(handle => !candidateThumbnails.ContainsKey(handle.ToInt64())
                                 && !failedThumbnailHandles.Contains(handle.ToInt64()))
                .ToArray();
            foreach (IntPtr handle in handles)
            {
                pendingThumbnailHandles.Add(handle.ToInt64());
            }

            Task.Run(
                () =>
                {
                    foreach (IntPtr handle in handles)
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        NativeWindowThumbnailData data = NativeWindowThumbnailCapture.Capture(
                            handle,
                            320,
                            180);
                        queuedThumbnails.Enqueue(new QueuedThumbnail(generation, data));
                    }
                },
                token);
        }

        private void DrainThumbnailQueue()
        {
            bool changed = false;
            QueuedThumbnail queued;
            while (queuedThumbnails.TryDequeue(out queued))
            {
                if (queued.Generation != thumbnailGeneration || queued.Data == null)
                {
                    continue;
                }

                long handle = queued.Data.Handle.ToInt64();
                pendingThumbnailHandles.Remove(handle);
                if (!queued.Data.Succeeded)
                {
                    failedThumbnailHandles.Add(handle);
                    changed = true;
                    continue;
                }

                Texture2D previous;
                if (candidateThumbnails.TryGetValue(handle, out previous) && previous != null)
                {
                    DestroyImmediate(previous);
                }

                Texture2D texture = new Texture2D(
                    queued.Data.Width,
                    queued.Data.Height,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "Native Dock Window Preview"
                };
                texture.LoadRawTextureData(queued.Data.RgbaPixels);
                texture.Apply(false, true);
                candidateThumbnails[handle] = texture;
                changed = true;
            }

            if (changed)
            {
                Repaint();
            }
        }

        private void ResetThumbnailPreviews()
        {
            thumbnailGeneration++;
            if (thumbnailCancellation != null)
            {
                thumbnailCancellation.Cancel();
                thumbnailCancellation = null;
            }

            QueuedThumbnail ignored;
            while (queuedThumbnails.TryDequeue(out ignored))
            {
            }

            foreach (Texture2D texture in candidateThumbnails.Values)
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }

            candidateThumbnails.Clear();
            failedThumbnailHandles.Clear();
            pendingThumbnailHandles.Clear();
        }

        private void AttachSelected()
        {
            if (candidates.Count == 0 || selectedIndex < 0 || selectedIndex >= candidates.Count)
            {
                SetStatus("SELECT  ·  choose a valid application window first",
                    NativeWindowDockGui.Warning);
                return;
            }

            Attach(candidates[selectedIndex]);
        }

        private void Attach(NativeWindowCandidate candidate)
        {
            Detach(false);
            showWindowPicker = false;
            ResetThumbnailPreviews();
            int ownerId = GetInstanceID();
            if (!NativeWindowClaimRegistry.TryClaim(candidate.Handle, ownerId))
            {
                SetStatus(
                    "IN USE  /  that application window is attached to another Native Dock panel",
                    NativeWindowDockGui.Warning);
                RefreshCandidates();
                return;
            }

            claimedTarget = candidate.Handle;
            try
            {
                session = NativeWindowSession.Attach(candidate.Handle);
                attachedLabel = candidate.DisplayLabel;
                UpdateTitle(candidate.ProcessName);
                LoadCropProfile(candidate);
                session.SetCrop(CurrentCrop());
                pendingLaunch = false;
                showLaunchPanel = false;
                SetStatus(
                    "CONNECTING  ·  preparing the native surface",
                    NativeWindowDockGui.Warning);
                Repaint();
            }
            catch (Exception exception)
            {
                session = null;
                NativeWindowClaimRegistry.Release(claimedTarget, ownerId);
                claimedTarget = IntPtr.Zero;
                UpdateTitle();
                SetStatus("ATTACH FAILED  ·  " + exception.Message, NativeWindowDockGui.Danger);
                Debug.LogWarning("Native Window Dock: " + exception);
            }
        }

        private void Detach(bool report)
        {
            NativeWindowSession previous = session;
            IntPtr previousClaim = claimedTarget;
            session = null;
            claimedTarget = IntPtr.Zero;
            SaveCropProfile();
            try
            {
                if (previous != null)
                {
                    previous.Dispose();
                }
            }
            finally
            {
                NativeWindowClaimRegistry.Release(previousClaim, GetInstanceID());
            }

            attachedLabel = string.Empty;
            UpdateTitle();
            showCropPanel = false;
            if (report)
            {
                SetStatus("DETACHED  ·  the application window was restored",
                    NativeWindowDockGui.Muted);
                RefreshCandidates();
            }
        }

        private void DetachForReload()
        {
            Detach(false);
        }

        private void LaunchAndAttach()
        {
            if (string.IsNullOrWhiteSpace(launchPath) || !File.Exists(launchPath))
            {
                SetStatus("INVALID EXE  ·  choose an existing .exe file",
                    NativeWindowDockGui.Warning);
                return;
            }

            try
            {
                windowsBeforeLaunch = new HashSet<long>(
                    NativeWindowInterop.EnumerateCandidates()
                        .Select(candidate => candidate.Handle.ToInt64()));
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = launchPath,
                    Arguments = launchArguments ?? string.Empty,
                    WorkingDirectory = Path.GetDirectoryName(launchPath) ?? string.Empty,
                    UseShellExecute = true
                };
                Process process = Process.Start(startInfo);
                launchedProcessId = process?.Id ?? 0;
                process?.Dispose();
                pendingLaunch = true;
                launchDeadline = EditorApplication.timeSinceStartup + LaunchTimeoutSeconds;
                nextLaunchPoll = 0;
                SetStatus("WAITING  ·  application launched; looking for its window",
                    NativeWindowDockGui.Warning);
            }
            catch (Exception exception)
            {
                pendingLaunch = false;
                SetStatus("LAUNCH FAILED  ·  " + exception.Message, NativeWindowDockGui.Danger);
            }
        }

        private void Tick()
        {
            DrainThumbnailQueue();
            double now = EditorApplication.timeSinceStartup;
            if (session != null)
            {
                if (!session.IsTargetAlive)
                {
                    Detach(false);
                    SetStatus("CLOSED  ·  the embedded application window exited",
                        NativeWindowDockGui.Warning);
                    Repaint();
                    return;
                }

                bool tabIsVisible = IsSelectedDockTab() && now - lastPaint < 0.5d;
                session.SetVisible(tabIsVisible);
                if (now >= nextRepaint)
                {
                    nextRepaint = now + RepaintIntervalSeconds;
                    Repaint();
                }
            }

            if (!pendingLaunch || now < nextLaunchPoll)
            {
                return;
            }

            nextLaunchPoll = now + 0.2d;
            IReadOnlyList<NativeWindowCandidate> current = NativeWindowInterop.EnumerateCandidates();
            NativeWindowCandidate launchedWindow = current.FirstOrDefault(
                candidate => launchedProcessId != 0 && candidate.ProcessId == launchedProcessId);
            if (launchedWindow == null)
            {
                string expectedProcess = Path.GetFileNameWithoutExtension(launchPath);
                launchedWindow = current.FirstOrDefault(
                    candidate => !windowsBeforeLaunch.Contains(candidate.Handle.ToInt64())
                                 && string.Equals(
                                     candidate.ProcessName,
                                     expectedProcess,
                                     StringComparison.OrdinalIgnoreCase));
            }

            if (launchedWindow != null)
            {
                candidates = current;
                Attach(launchedWindow);
                return;
            }

            if (now >= launchDeadline)
            {
                pendingLaunch = false;
                RefreshCandidates();
                SetStatus(
                    "TIMEOUT  ·  no new window appeared; select it from the list if the app reused an existing process",
                    NativeWindowDockGui.Warning);
                Repaint();
            }
        }

        private bool IsSelectedDockTab()
        {
            try
            {
                object parent = ParentField?.GetValue(this);
                if (parent == null)
                {
                    return true;
                }

                PropertyInfo actualView = parent.GetType().GetProperty(
                    "actualView",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return actualView == null || ReferenceEquals(actualView.GetValue(parent), this);
            }
            catch
            {
                return EditorApplication.timeSinceStartup - lastPaint < 0.5d;
            }
        }

        private void SetStatus(string message, Color color)
        {
            statusMessage = message;
            statusColor = color;
        }

        private void EnsurePanelIdentity()
        {
            if (panelNumber > 0)
            {
                return;
            }

            HashSet<int> usedNumbers = new HashSet<int>(
                Resources.FindObjectsOfTypeAll<NativeWindowDockWindow>()
                    .Where(window => window != null && !ReferenceEquals(window, this))
                    .Select(window => window.panelNumber)
                    .Where(value => value > 0));
            panelNumber = 1;
            while (usedNumbers.Contains(panelNumber))
            {
                panelNumber++;
            }
        }

        private void UpdateTitle(string processName = null)
        {
            titleContent = new GUIContent(ComposePanelTitle(panelNumber, processName));
        }

        internal static string ComposePanelTitle(int number, string processName)
        {
            string panelTitle = number > 0 ? $"{BaseTitle} {number}" : BaseTitle;
            if (string.IsNullOrWhiteSpace(processName))
            {
                return panelTitle;
            }

            return panelTitle + "  ·  " + NativeWindowCandidate.NormalizeDisplayText(
                processName,
                18);
        }

        private NativeWindowCrop CurrentCrop()
        {
            return new NativeWindowCrop(cropLeft, cropTop, cropRight, cropBottom);
        }

        private void ApplyCrop(bool save = true)
        {
            cropLeft = Mathf.Max(0, cropLeft);
            cropTop = Mathf.Max(0, cropTop);
            cropRight = Mathf.Max(0, cropRight);
            cropBottom = Mathf.Max(0, cropBottom);
            session?.SetCrop(CurrentCrop());
            if (save)
            {
                SaveCropProfile();
            }
            Repaint();
        }

        private void LoadCropProfile(NativeWindowCandidate candidate)
        {
            cropProfileKey =
                "BattleSoccer.NativeWindowDock.Crop."
                + candidate.ProcessName
                + "."
                + candidate.ClassName;
            string data = EditorPrefs.GetString(cropProfileKey, string.Empty);
            if (string.IsNullOrEmpty(data))
            {
                cropLeft = 0;
                cropTop = 0;
                cropRight = 0;
                cropBottom = 0;
                return;
            }

            CropProfile profile = JsonUtility.FromJson<CropProfile>(data);
            if (profile == null)
            {
                return;
            }

            cropLeft = Mathf.Max(0, profile.left);
            cropTop = Mathf.Max(0, profile.top);
            cropRight = Mathf.Max(0, profile.right);
            cropBottom = Mathf.Max(0, profile.bottom);
        }

        private void SaveCropProfile()
        {
            if (string.IsNullOrEmpty(cropProfileKey))
            {
                return;
            }

            CropProfile profile = new CropProfile
            {
                left = cropLeft,
                top = cropTop,
                right = cropRight,
                bottom = cropBottom
            };
            EditorPrefs.SetString(cropProfileKey, JsonUtility.ToJson(profile));
        }

        [Serializable]
        private sealed class CropProfile
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
    }

    internal static class NativeWindowClaimRegistry
    {
        private static readonly Dictionary<long, int> Owners =
            new Dictionary<long, int>();

        internal static bool TryClaim(IntPtr target, int ownerId)
        {
            if (target == IntPtr.Zero)
            {
                return false;
            }

            long key = target.ToInt64();
            int currentOwner;
            if (Owners.TryGetValue(key, out currentOwner))
            {
                return currentOwner == ownerId;
            }

            Owners[key] = ownerId;
            return true;
        }

        internal static bool IsClaimedByOther(IntPtr target, int ownerId)
        {
            int currentOwner;
            return target != IntPtr.Zero
                   && Owners.TryGetValue(target.ToInt64(), out currentOwner)
                   && currentOwner != ownerId;
        }

        internal static void Release(IntPtr target, int ownerId)
        {
            if (target == IntPtr.Zero)
            {
                return;
            }

            long key = target.ToInt64();
            int currentOwner;
            if (Owners.TryGetValue(key, out currentOwner) && currentOwner == ownerId)
            {
                Owners.Remove(key);
            }
        }
    }
}
