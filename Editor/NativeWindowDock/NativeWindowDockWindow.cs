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
using DansToolbox.Editor;
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
        private const float CropPanelHeight = 112f;
        private const float PickerMinimumHeight = 190f;
        private const float PickerMaximumHeight = 420f;
        private const float PickerCardMinimumWidth = 184f;
        private const float PickerCardGap = 8f;
        private const float CropHandleThickness = 10f;
        private const float StatusHeight = 24f;
        private const int MaxHorizontalCrop = 1200;
        private const int MaxVerticalCrop = 800;
        private const float MinimumZoom = 0.5f;
        private const float MaximumZoom = 2f;
        private const float ZoomStep = 0.1f;
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
        [SerializeField] private float viewZoom = 1f;
        [SerializeField] private int panelNumber;
        [SerializeField] private long reloadTargetHandle;

        private IReadOnlyList<NativeWindowCandidate> candidates =
            Array.Empty<NativeWindowCandidate>();
        private NativeWindowSession session;
        private IntPtr claimedTarget;
        private string attachedLabel = string.Empty;
        private string cropProfileKey = string.Empty;
        private List<CropPreset> cropPresets = new List<CropPreset>();
        private int selectedCropPreset = -1;
        private string cropPresetName = string.Empty;
        private string statusMessage =
            "Choose a running window or launch an application";
        private Color statusColor;
        private bool pendingLaunch;
        private int launchedProcessId;
        private double launchDeadline;
        private double nextLaunchPoll;
        private double nextRepaint;
        private double reloadReattachDeadline;
        private int consecutivePositionFailures;
        private bool preparingForReload;
        private HashSet<long> windowsBeforeLaunch = new HashSet<long>();
        private Vector2 cropDragStartMouse;
        private NativeWindowCrop cropDragStart;
        private Dictionary<long, Texture2D> candidateThumbnails =
            new Dictionary<long, Texture2D>();
        private HashSet<long> failedThumbnailHandles = new HashSet<long>();
        private HashSet<long> pendingThumbnailHandles = new HashSet<long>();
        private ConcurrentQueue<QueuedThumbnail> queuedThumbnails =
            new ConcurrentQueue<QueuedThumbnail>();
        private CancellationTokenSource thumbnailCancellation;
        private int thumbnailGeneration;
        private double revealStartedAt;

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
            DansToolboxToolHub.OpenNewNativeDock();
        }

        [MenuItem("Tools/Dans Toolbox/Native Window Dock", true)]
        private static bool ValidateOpen()
        {
            return DansToolboxSettings.IsToolEnabled(
                DansToolboxTools.NativeWindowDockId);
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            statusColor = NativeWindowDockGui.Muted;
            revealStartedAt = EditorApplication.timeSinceStartup;
            EnsurePanelIdentity();
            UpdateTitle();
            minSize = new Vector2(520f, 340f);
            wantsMouseMove = true;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += DetachForReload;
            if (IsWindowsEditor)
            {
                RefreshCandidates();
                if (reloadTargetHandle != 0)
                {
                    reloadReattachDeadline = EditorApplication.timeSinceStartup + 10d;
                    SetStatus(
                        "RECONNECTING  /  restoring the app after Unity reloaded scripts",
                        NativeWindowDockGui.Warning);
                }
            }
            else
            {
                SetStatus("UNSUPPORTED  ·  native embedding currently requires the Windows Editor",
                    NativeWindowDockGui.Warning);
            }
        }

        private void OnDisable()
        {
            EnsureRuntimeState();
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= DetachForReload;
            ResetThumbnailPreviews();
            if (!preparingForReload && session != null && EditorApplication.isCompiling)
            {
                DetachForReload();
            }

            if (!preparingForReload)
            {
                reloadTargetHandle = 0;
                Detach(false);
            }
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
                        consecutivePositionFailures = 0;
                        SetStatus(
                            "ATTACHED  ·  click the app to type; Detach restores its desktop window",
                            NativeWindowDockGui.Accent);
                    }
                    else if (consecutivePositionFailures > 0)
                    {
                        consecutivePositionFailures = 0;
                        SetStatus(
                            "ATTACHED  /  native surface reconnected",
                            NativeWindowDockGui.Accent);
                    }
                }
                catch (Exception exception)
                {
                    consecutivePositionFailures++;
                    SetStatus(
                        "RECONNECTING  /  " + exception.Message,
                        NativeWindowDockGui.Warning);
                    if (consecutivePositionFailures == 1
                        || consecutivePositionFailures % 120 == 0)
                    {
                        Debug.LogWarning(
                            "Native Window Dock will keep retrying a transient native positioning failure: "
                            + exception.Message);
                    }
                    Repaint();
                }
            }

            if (DansToolboxMotion.DrawWindowReveal(
                    new Rect(0f, 0f, position.width, position.height),
                    revealStartedAt))
            {
                Repaint();
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

            if (GUI.Button(
                    new Rect(rect.xMax - 108, rect.y + 4, 92, 28),
                    "NEW PANEL",
                    NativeWindowDockGui.Button))
            {
                DansToolboxToolHub.OpenNewNativeDock();
            }

            if (session != null || pendingLaunch)
            {
                string state = session != null ? "ATTACHED" : "WAITING";
                Color stateColor = session != null
                    ? NativeWindowDockGui.Accent
                    : NativeWindowDockGui.Warning;
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
                    new GUIContent(
                        "FRAME",
                        "Drag the glowing borders to crop the app, or zoom its surface in and out."),
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
                $"FRAME  /  L {cropLeft}   T {cropTop}   R {cropRight}   B {cropBottom}  px",
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
                viewZoom = 1f;
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
                viewZoom = 1f;
                ApplyCrop();
            }

            float zoomY = rect.y + 42f;
            GUI.Label(
                new Rect(rect.x + 12f, zoomY + 4f, Mathf.Max(80f, rect.width - 304f), 20f),
                $"ZOOM  /  {Mathf.RoundToInt(viewZoom * 100f)}%",
                NativeWindowDockGui.Status);
            if (GUI.Button(
                    new Rect(rect.xMax - 286f, zoomY, 86f, 26f),
                    new GUIContent("ZOOM OUT", "Shrink the embedded app to reveal more of its surface"),
                    NativeWindowDockGui.Button))
            {
                ChangeZoom(-ZoomStep);
            }

            if (GUI.Button(
                    new Rect(rect.xMax - 194f, zoomY, 68f, 26f),
                    new GUIContent("100%", "Reset the embedded app zoom"),
                    NativeWindowDockGui.Button))
            {
                SetZoom(1f);
            }

            if (GUI.Button(
                    new Rect(rect.xMax - 120f, zoomY, 108f, 26f),
                    new GUIContent("ZOOM IN", "Enlarge the embedded app inside the frame"),
                    NativeWindowDockGui.Button))
            {
                ChangeZoom(ZoomStep);
            }

            float presetY = rect.y + 78f;
            float contentWidth = rect.width - 24f;
            const float gap = 6f;
            const float saveWidth = 54f;
            const float updateWidth = 62f;
            const float deleteWidth = 58f;
            float nameWidth = Mathf.Clamp(contentWidth * 0.25f, 90f, 140f);
            float popupWidth = Mathf.Max(
                92f,
                contentWidth - nameWidth - saveWidth - updateWidth - deleteWidth - gap * 4f);
            Rect popupRect = new Rect(rect.x + 12f, presetY, popupWidth, 26f);
            Rect nameRect = new Rect(popupRect.xMax + gap, presetY, nameWidth, 26f);
            Rect saveRect = new Rect(nameRect.xMax + gap, presetY, saveWidth, 26f);
            Rect updateRect = new Rect(saveRect.xMax + gap, presetY, updateWidth, 26f);
            Rect deleteRect = new Rect(updateRect.xMax + gap, presetY, deleteWidth, 26f);

            string[] presetOptions = new string[cropPresets.Count + 1];
            presetOptions[0] = "PRESET / CHOOSE";
            for (int index = 0; index < cropPresets.Count; index++)
            {
                presetOptions[index + 1] = cropPresets[index].name;
            }

            int popupSelection = Mathf.Clamp(selectedCropPreset + 1, 0, cropPresets.Count);
            int nextSelection = EditorGUI.Popup(popupRect, popupSelection, presetOptions);
            if (nextSelection != popupSelection)
            {
                selectedCropPreset = nextSelection - 1;
                ApplySelectedCropPreset();
            }

            cropPresetName = GUI.TextField(
                nameRect,
                cropPresetName ?? string.Empty,
                NativeWindowDockGui.TextField);
            if (GUI.Button(
                    saveRect,
                    new GUIContent("SAVE", "Save the current frame as a new named preset"),
                    NativeWindowDockGui.PrimaryButton))
            {
                SaveNewCropPreset();
            }

            using (new EditorGUI.DisabledScope(
                       selectedCropPreset < 0 || selectedCropPreset >= cropPresets.Count))
            {
                if (GUI.Button(
                        updateRect,
                        new GUIContent("UPDATE", "Replace and rename the selected preset"),
                        NativeWindowDockGui.Button))
                {
                    UpdateSelectedCropPreset();
                }

                if (GUI.Button(
                        deleteRect,
                        new GUIContent("DELETE", "Remove the selected preset"),
                        NativeWindowDockGui.DangerButton))
                {
                    DeleteSelectedCropPreset();
                }
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
                    $"FRAMED  /  L {cropLeft}  T {cropTop}  R {cropRight}  B {cropBottom}  /  {Mathf.RoundToInt(viewZoom * 100f)}%",
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

        private void ChangeZoom(float delta)
        {
            SetZoom(viewZoom + delta);
        }

        private void SetZoom(float zoom)
        {
            viewZoom = NormalizeZoom(zoom);
            ApplyCrop();
            SetStatus(
                $"ZOOM  /  {Mathf.RoundToInt(viewZoom * 100f)}%",
                NativeWindowDockGui.Accent);
        }

        internal static float NormalizeZoom(float zoom)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom) || zoom <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp(zoom, MinimumZoom, MaximumZoom);
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
                    SetStatus("Launch wait cancelled", NativeWindowDockGui.Muted);
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
                            : $"{candidates.Count} application window(s) available",
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
                consecutivePositionFailures = 0;
                attachedLabel = candidate.DisplayLabel;
                UpdateTitle(candidate.ProcessName);
                LoadCropProfile(candidate);
                session.SetFrame(CurrentCrop(), viewZoom);
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
            consecutivePositionFailures = 0;
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
            bool wasShowingCropPanel = showCropPanel;
            if (session != null)
            {
                reloadTargetHandle = session.Target.ToInt64();
                preparingForReload = true;
            }

            Detach(false);
            showCropPanel = wasShowingCropPanel;
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
            EnsureRuntimeState();
            DrainThumbnailQueue();
            double now = EditorApplication.timeSinceStartup;
            if (session == null && reloadTargetHandle != 0)
            {
                NativeWindowCandidate reloadCandidate = candidates.FirstOrDefault(
                    candidate => candidate.Handle.ToInt64() == reloadTargetHandle);
                if (reloadCandidate != null)
                {
                    reloadTargetHandle = 0;
                    Attach(reloadCandidate);
                    return;
                }

                if (now >= reloadReattachDeadline)
                {
                    reloadTargetHandle = 0;
                    SetStatus(
                        "RECONNECT FAILED  /  the previous application window is no longer available",
                        NativeWindowDockGui.Warning);
                    Repaint();
                }
            }

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

                bool tabIsVisible = IsSelectedDockTab();
                session.SetVisible(tabIsVisible);
                if (tabIsVisible && now >= nextRepaint)
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

        private void EnsureRuntimeState()
        {
            candidates ??= Array.Empty<NativeWindowCandidate>();
            windowsBeforeLaunch ??= new HashSet<long>();
            candidateThumbnails ??= new Dictionary<long, Texture2D>();
            failedThumbnailHandles ??= new HashSet<long>();
            pendingThumbnailHandles ??= new HashSet<long>();
            queuedThumbnails ??= new ConcurrentQueue<QueuedThumbnail>();
            cropPresets ??= new List<CropPreset>();
            attachedLabel ??= string.Empty;
            cropProfileKey ??= string.Empty;
            cropPresetName ??= string.Empty;
            viewZoom = NormalizeZoom(viewZoom);
            launchPath ??= string.Empty;
            launchArguments ??= string.Empty;
            statusMessage ??= "Choose a running window or launch an application";
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
                // A reflection miss is not evidence that the tab became hidden. Keeping the
                // child host visible avoids a hide/show pulse; its Unity parent still controls
                // visibility when the native container is minimized or covered.
                return true;
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
            string fullTitle = ComposePanelTitle(panelNumber, processName);
            DansToolboxWindowChrome.ApplyCompactTitle(
                this,
                DansToolboxTools.NativeWindowDockId,
                null,
                fullTitle);
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
            viewZoom = NormalizeZoom(viewZoom);
            session?.SetFrame(CurrentCrop(), viewZoom);
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
            LoadCropPresets();
            string data = EditorPrefs.GetString(cropProfileKey, string.Empty);
            if (string.IsNullOrEmpty(data))
            {
                cropLeft = 0;
                cropTop = 0;
                cropRight = 0;
                cropBottom = 0;
                viewZoom = 1f;
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
            viewZoom = NormalizeZoom(profile.zoom);
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
                bottom = cropBottom,
                zoom = viewZoom
            };
            EditorPrefs.SetString(cropProfileKey, JsonUtility.ToJson(profile));
        }

        private void LoadCropPresets()
        {
            cropPresets.Clear();
            selectedCropPreset = -1;
            cropPresetName = string.Empty;
            if (string.IsNullOrEmpty(cropProfileKey))
            {
                return;
            }

            string data = EditorPrefs.GetString(CropPresetStorageKey, string.Empty);
            if (string.IsNullOrEmpty(data))
            {
                return;
            }

            try
            {
                CropPresetCollection collection =
                    JsonUtility.FromJson<CropPresetCollection>(data);
                if (collection?.presets == null)
                {
                    return;
                }

                foreach (CropPreset preset in collection.presets)
                {
                    if (preset == null || string.IsNullOrWhiteSpace(preset.name))
                    {
                        continue;
                    }

                    preset.id = string.IsNullOrEmpty(preset.id)
                        ? Guid.NewGuid().ToString("N")
                        : preset.id;
                    preset.name = NativeWindowCandidate.NormalizeDisplayText(
                        preset.name.Trim(),
                        48);
                    preset.zoom = NormalizeZoom(preset.zoom);
                    cropPresets.Add(preset);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Native Window Dock could not read framing presets: "
                    + exception.Message);
            }
        }

        private void SaveNewCropPreset()
        {
            string requestedName = string.IsNullOrWhiteSpace(cropPresetName)
                ? "Frame " + (cropPresets.Count + 1)
                : cropPresetName.Trim();
            CropPreset preset = CreateCropPreset(
                Guid.NewGuid().ToString("N"),
                CreateUniqueCropPresetName(requestedName, cropPresets, null),
                CurrentCrop(),
                viewZoom);
            cropPresets.Add(preset);
            selectedCropPreset = cropPresets.Count - 1;
            cropPresetName = preset.name;
            PersistCropPresets();
            SetStatus("PRESET SAVED  /  " + preset.name, NativeWindowDockGui.Accent);
        }

        private void UpdateSelectedCropPreset()
        {
            if (selectedCropPreset < 0 || selectedCropPreset >= cropPresets.Count)
            {
                return;
            }

            CropPreset preset = cropPresets[selectedCropPreset];
            string requestedName = string.IsNullOrWhiteSpace(cropPresetName)
                ? preset.name
                : cropPresetName.Trim();
            preset.name = CreateUniqueCropPresetName(
                requestedName,
                cropPresets,
                preset.id);
            SetPresetCrop(preset, CurrentCrop());
            preset.zoom = viewZoom;
            cropPresetName = preset.name;
            PersistCropPresets();
            SetStatus("PRESET UPDATED  /  " + preset.name, NativeWindowDockGui.Accent);
        }

        private void DeleteSelectedCropPreset()
        {
            if (selectedCropPreset < 0 || selectedCropPreset >= cropPresets.Count)
            {
                return;
            }

            string removedName = cropPresets[selectedCropPreset].name;
            cropPresets.RemoveAt(selectedCropPreset);
            selectedCropPreset = -1;
            cropPresetName = string.Empty;
            PersistCropPresets();
            SetStatus("PRESET REMOVED  /  " + removedName, NativeWindowDockGui.Muted);
        }

        private void ApplySelectedCropPreset()
        {
            if (selectedCropPreset < 0 || selectedCropPreset >= cropPresets.Count)
            {
                cropPresetName = string.Empty;
                return;
            }

            CropPreset preset = cropPresets[selectedCropPreset];
            cropPresetName = preset.name;
            SetCropValues(new NativeWindowCrop(
                preset.left,
                preset.top,
                preset.right,
                preset.bottom));
            viewZoom = NormalizeZoom(preset.zoom);
            ApplyCrop();
            SetStatus("PRESET APPLIED  /  " + preset.name, NativeWindowDockGui.Accent);
        }

        private void PersistCropPresets()
        {
            if (string.IsNullOrEmpty(cropProfileKey))
            {
                return;
            }

            CropPresetCollection collection = new CropPresetCollection
            {
                presets = cropPresets
            };
            EditorPrefs.SetString(CropPresetStorageKey, JsonUtility.ToJson(collection));
        }

        private string CropPresetStorageKey => cropProfileKey + ".Presets.v1";

        private static CropPreset CreateCropPreset(
            string id,
            string name,
            NativeWindowCrop crop,
            float zoom)
        {
            CropPreset preset = new CropPreset
            {
                id = id,
                name = name,
                zoom = NormalizeZoom(zoom)
            };
            SetPresetCrop(preset, crop);
            return preset;
        }

        private static void SetPresetCrop(CropPreset preset, NativeWindowCrop crop)
        {
            preset.left = crop.Left;
            preset.top = crop.Top;
            preset.right = crop.Right;
            preset.bottom = crop.Bottom;
        }

        internal static string CreateUniqueCropPresetName(
            string requestedName,
            IEnumerable<CropPreset> presets,
            string ignoredId)
        {
            string baseName = NativeWindowCandidate.NormalizeDisplayText(
                string.IsNullOrWhiteSpace(requestedName) ? "Frame" : requestedName.Trim(),
                48);
            HashSet<string> existingNames = new HashSet<string>(
                (presets ?? Enumerable.Empty<CropPreset>())
                    .Where(preset => preset != null
                                     && !string.Equals(
                                         preset.id,
                                         ignoredId,
                                         StringComparison.Ordinal))
                    .Select(preset => preset.name),
                StringComparer.OrdinalIgnoreCase);
            if (!existingNames.Contains(baseName))
            {
                return baseName;
            }

            int suffix = 2;
            string candidate;
            do
            {
                string suffixText = " " + suffix;
                string shortenedBase = NativeWindowCandidate.NormalizeDisplayText(
                    baseName,
                    48 - suffixText.Length);
                candidate = shortenedBase + suffixText;
                suffix++;
            }
            while (existingNames.Contains(candidate));

            return candidate;
        }

        [Serializable]
        private sealed class CropProfile
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
            public float zoom;
        }

        [Serializable]
        internal sealed class CropPreset
        {
            public string id;
            public string name;
            public int left;
            public int top;
            public int right;
            public int bottom;
            public float zoom;
        }

        [Serializable]
        private sealed class CropPresetCollection
        {
            public List<CropPreset> presets = new List<CropPreset>();
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
