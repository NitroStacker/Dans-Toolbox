using System;
using System.Collections.Generic;
using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.Artboard
{
    internal enum ArtboardTool
    {
        Pencil,
        Brush,
        Eraser,
        Fill,
        Line,
        Rectangle,
        Ellipse,
        Eyedropper,
        Hand
    }

    public sealed class ArtboardWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Dans Toolbox/Artboard";
        private const float ToolbarHeight = 44f;
        private const float ToolRailWidth = 44f;
        private const float LeftPanelWidth = 196f;
        private const float RightPanelWidth = 244f;
        private const float TimelineHeight = 132f;
        private const int PaletteColumns = 6;

        private static readonly Color32[] PaletteColors =
        {
            new Color32(37,36,33,255), new Color32(243,242,239,255),
            new Color32(198,93,103,255), new Color32(217,168,95,255),
            new Color32(121,184,143,255), new Color32(99,151,168,255),
            new Color32(102,125,154,255), new Color32(141,134,187,255),
            new Color32(177,118,155,255), new Color32(149,100,77,255),
            new Color32(104,104,100,255), new Color32(201,198,192,255)
        };

        [SerializeField] private ArtboardAsset document;
        [SerializeField] private ArtboardTool tool = ArtboardTool.Pencil;
        [SerializeField] private int activeFrame;
        [SerializeField] private int activeLayer;
        [SerializeField] private Color color = new Color32(243, 242, 239, 255);
        [SerializeField] private int brushSize = 1;
        [SerializeField] private bool mirrorX;
        [SerializeField] private bool mirrorY;
        [SerializeField] private bool showGrid = true;
        [SerializeField] private bool onionSkin = true;
        [SerializeField] private bool filledShapes;
        [SerializeField] private bool createAnimationClip = true;
        [SerializeField] private float zoom = 8f;
        [SerializeField] private Vector2 pan;
        [SerializeField] private Vector2 layerScroll;
        [SerializeField] private Vector2 timelineScroll;
        [SerializeField] private Vector2 leftScroll;
        [SerializeField] private Vector2 exportScroll;

        [NonSerialized] private readonly Dictionary<string, Color32[]> celCache = new Dictionary<string, Color32[]>();
        [NonSerialized] private readonly Dictionary<int, Texture2D> frameTextures = new Dictionary<int, Texture2D>();
        [NonSerialized] private Rect workspaceRect;
        [NonSerialized] private Rect artboardRect;
        [NonSerialized] private bool drawing;
        [NonSerialized] private bool panning;
        [NonSerialized] private Vector2Int gestureStart;
        [NonSerialized] private Vector2Int gestureLast;
        [NonSerialized] private Color32[] gestureBase;
        [NonSerialized] private Vector2 panMouseStart;
        [NonSerialized] private Vector2 panStart;
        [NonSerialized] private Vector2Int hoverPixel;
        [NonSerialized] private bool hoverIndicatorVisible;
        [NonSerialized] private bool playing;
        [NonSerialized] private double playbackLast;
        [NonSerialized] private double revealStartedAt;
        [NonSerialized] private string status = "Ready";

        [MenuItem(MenuPath, false, 27)]
        internal static void Open()
        {
            ArtboardWindow window = GetWindow<ArtboardWindow>();
            DansToolboxWindowChrome.ApplyCompactTitle(window, DansToolboxTools.ArtboardId);
            window.minSize = new Vector2(720f, 440f);
            window.Show();
            window.Focus();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOpen()
        {
            return DansToolboxSettings.IsToolEnabled(DansToolboxTools.ArtboardId);
        }

        public static void OpenAsset(ArtboardAsset asset)
        {
            Open();
            ArtboardWindow window = GetWindow<ArtboardWindow>();
            window.SetDocument(asset, true);
        }

        internal static ArtboardAsset CreateAndOpenDocument(int width, int height, ArtboardMode mode, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            ArtboardAsset asset = ArtboardAsset.CreateDocument(width, height, mode);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            OpenAsset(asset);
            return asset;
        }

        private void OnEnable()
        {
            revealStartedAt = EditorApplication.timeSinceStartup;
            wantsMouseMove = true;
            minSize = new Vector2(720f, 440f);
            DansToolboxWindowChrome.ApplyCompactTitle(this, DansToolboxTools.ArtboardId);
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            DansToolboxTheme.Changed -= OnThemeChanged;
            DansToolboxTheme.Changed += OnThemeChanged;
            if (document != null)
            {
                document.EnsureIntegrity();
                ClampSelection();
            }
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
            DansToolboxTheme.Changed -= OnThemeChanged;
            DisposeTextures();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is ArtboardAsset selected && selected != document)
                SetDocument(selected, false);
        }

        private void OnThemeChanged()
        {
            Repaint();
        }

        private void OnUndoRedo()
        {
            document?.EnsureIntegrity();
            ClampSelection();
            ClearCaches();
            status = "Undo state restored";
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (!playing || document == null || document.Frames.Count <= 1) return;
            double now = EditorApplication.timeSinceStartup;
            float duration = document.Frames[activeFrame].Hold / (float)document.FramesPerSecond;
            if (now - playbackLast < duration) return;
            playbackLast = now;
            activeFrame = (activeFrame + 1) % document.Frames.Count;
            Repaint();
        }

        private void OnGUI()
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            Rect canvas = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(canvas, palette.Canvas);
            HandleKeyboard();
            DrawToolbar(new Rect(0f, 0f, position.width, ToolbarHeight), palette);

            if (!DansToolboxSettings.IsToolEnabled(DansToolboxTools.ArtboardId))
            {
                DrawDisabled(new Rect(0f, ToolbarHeight, position.width, position.height - ToolbarHeight), palette);
                return;
            }
            if (document == null)
            {
                DrawEmpty(new Rect(0f, ToolbarHeight, position.width, position.height - ToolbarHeight), palette);
                return;
            }

            document.EnsureIntegrity();
            ClampSelection();
            bool timelineVisible = document.Mode != ArtboardMode.DigitalArt || document.Frames.Count > 1;
            float bottom = timelineVisible ? TimelineHeight : 0f;
            Rect content = new Rect(0f, ToolbarHeight, position.width, Mathf.Max(1f, position.height - ToolbarHeight - bottom));
            Rect toolRail = new Rect(content.x, content.y, ToolRailWidth, content.height);
            float leftWidth = position.width < 860f ? 164f : LeftPanelWidth;
            float rightWidth = position.width < 860f ? 210f : RightPanelWidth;
            Rect left = new Rect(toolRail.xMax, content.y, leftWidth, content.height);
            Rect right = new Rect(content.xMax - rightWidth, content.y, rightWidth, content.height);
            workspaceRect = new Rect(left.xMax, content.y, Mathf.Max(100f, right.xMin - left.xMax), content.height);

            DrawToolRail(toolRail, palette);
            DrawLeftPanel(left, palette);
            DrawWorkspace(workspaceRect, palette);
            DrawRightPanel(right, palette);
            if (timelineVisible)
                DrawTimeline(new Rect(0f, content.yMax, position.width, TimelineHeight), palette);

            if (DansToolboxMotion.DrawWindowReveal(canvas, revealStartedAt)) Repaint();
        }

        private void DrawToolbar(Rect rect, DansToolboxPalette palette)
        {
            EditorGUI.DrawRect(rect, palette.Panel);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), palette.Border);
            float x = 8f;
            if (ArtboardGui.Button(new Rect(x, 8f, 48f, 27f), "NEW", "Create a new Artboard document"))
                ArtboardNewDocumentWindow.Open();
            x += 54f;
            EditorGUI.BeginChangeCheck();
            ArtboardAsset next = EditorGUI.ObjectField(new Rect(x, 9f, Mathf.Min(220f, rect.width * 0.25f), 25f),
                document, typeof(ArtboardAsset), false) as ArtboardAsset;
            if (EditorGUI.EndChangeCheck()) SetDocument(next, true);
            x += Mathf.Min(220f, rect.width * 0.25f) + 8f;

            if (document != null)
            {
                if (ArtboardGui.Button(new Rect(x, 8f, 46f, 27f), "SAVE", "Save the Artboard asset  Ctrl+S")) SaveDocument();
                x += 52f;
                if (rect.width > 900f)
                {
                    GUI.Label(new Rect(x, 11f, 150f, 20f), $"{document.Width} x {document.Height}  ·  {ModeLabel(document.Mode)}", ArtboardGui.Muted);
                }

                float exportX = rect.xMax - 90f;
                if (ArtboardGui.Button(new Rect(exportX, 8f, 82f, 27f), "EXPORT", "Export the current frame  Ctrl+E", false, true))
                    ExportCurrentFrame();
                float fitX = exportX - 44f;
                if (ArtboardGui.Button(new Rect(fitX, 8f, 38f, 27f), "FIT", "Fit the artboard in the workspace")) FitCanvas();
                float zoomX = fitX - 86f;
                GUI.Label(new Rect(zoomX, 11f, 80f, 20f), Mathf.RoundToInt(zoom * 100f) + "%", ArtboardGui.Muted);
            }
        }

        private void DrawToolRail(Rect rect, DansToolboxPalette palette)
        {
            EditorGUI.DrawRect(rect, palette.Inset);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), palette.Border);
            ArtboardTool[] tools =
            {
                ArtboardTool.Pencil, ArtboardTool.Brush, ArtboardTool.Eraser, ArtboardTool.Fill,
                ArtboardTool.Line, ArtboardTool.Rectangle, ArtboardTool.Ellipse, ArtboardTool.Eyedropper, ArtboardTool.Hand
            };
            string[] glyphs = { "P", "B", "E", "F", "/", "□", "○", "I", "H" };
            for (int i = 0; i < tools.Length; i++)
            {
                Rect button = new Rect(rect.x + 6f, rect.y + 7f + i * 38f, 32f, 32f);
                if (ArtboardGui.Button(button, glyphs[i], ToolLabel(tools[i]) + "  " + ToolShortcut(tools[i]), tool == tools[i]))
                {
                    CancelGesture();
                    tool = tools[i];
                    status = ToolLabel(tool);
                }
            }
        }

        private void DrawLeftPanel(Rect rect, DansToolboxPalette palette)
        {
            ArtboardGui.Panel(rect, palette);
            Rect header = new Rect(rect.x, rect.y, rect.width, 38f);
            EditorGUI.DrawRect(header, palette.Raised);
            EditorGUI.DrawRect(new Rect(header.x, header.yMax - 1f, header.width, 1f), palette.Border);
            GUI.Label(new Rect(header.x + 11f, header.y + 10f, header.width - 22f, 18f), ToolLabel(tool), ArtboardGui.Label);

            Rect view = new Rect(rect.x + 1f, header.yMax, rect.width - 2f, rect.height - header.height - 1f);
            Rect content = new Rect(0f, 0f, view.width - 16f, 620f);
            leftScroll = GUI.BeginScrollView(view, leftScroll, content);
            float y = 12f;
            ArtboardGui.SectionLabel(new Rect(10f, y, content.width - 20f, 16f), "Stroke");
            y += 22f;

            if (tool != ArtboardTool.Hand && tool != ArtboardTool.Eyedropper)
            {
                EditorGUI.BeginChangeCheck();
                Color nextColor = EditorGUI.ColorField(new Rect(10f, y, content.width - 20f, 28f), GUIContent.none, color, true, true, false);
                if (EditorGUI.EndChangeCheck()) color = nextColor;
                y += 36f;
                int nextSize = EditorGUI.IntSlider(new Rect(10f, y, content.width - 20f, 20f), brushSize, 1,
                    document.Mode == ArtboardMode.PixelArt ? 32 : 256);
                if (nextSize != brushSize) brushSize = nextSize;
                y += 30f;
                if (tool == ArtboardTool.Rectangle || tool == ArtboardTool.Ellipse)
                {
                    filledShapes = EditorGUI.ToggleLeft(new Rect(10f, y, content.width - 20f, 18f), "Filled shape", filledShapes);
                    y += 28f;
                }
            }

            ArtboardGui.SectionLabel(new Rect(10f, y, content.width - 20f, 16f), "Palette");
            y += 21f;
            float swatch = Mathf.Max(20f, (content.width - 25f) / PaletteColumns);
            for (int i = 0; i < PaletteColors.Length; i++)
            {
                int column = i % PaletteColumns;
                int row = i / PaletteColumns;
                Rect swatchRect = new Rect(10f + column * swatch, y + row * swatch, swatch - 4f, swatch - 4f);
                EditorGUI.DrawRect(swatchRect, PaletteColors[i]);
                if (Approximately(color, PaletteColors[i])) ArtboardGui.Border(swatchRect, palette.AccentHover);
                if (GUI.Button(swatchRect, GUIContent.none, GUIStyle.none)) color = PaletteColors[i];
            }
            y += Mathf.Ceil(PaletteColors.Length / (float)PaletteColumns) * swatch + 12f;

            ArtboardGui.SectionLabel(new Rect(10f, y, content.width - 20f, 16f), "Assist");
            y += 22f;
            mirrorX = EditorGUI.ToggleLeft(new Rect(10f, y, content.width - 20f, 18f), "Mirror X", mirrorX); y += 23f;
            mirrorY = EditorGUI.ToggleLeft(new Rect(10f, y, content.width - 20f, 18f), "Mirror Y", mirrorY); y += 23f;
            showGrid = EditorGUI.ToggleLeft(new Rect(10f, y, content.width - 20f, 18f), "Pixel grid", showGrid); y += 23f;
            onionSkin = EditorGUI.ToggleLeft(new Rect(10f, y, content.width - 20f, 18f), "Onion skin", onionSkin); y += 31f;

            ArtboardGui.SectionLabel(new Rect(10f, y, content.width - 20f, 16f), "Navigation");
            y += 22f;
            if (ArtboardGui.Button(new Rect(10f, y, content.width - 20f, 27f), "FIT ARTBOARD", "Fit canvas to the available space")) FitCanvas();
            y += 35f;
            GUI.Label(new Rect(10f, y, content.width - 20f, 48f), "Wheel zooms · Middle-drag pans\nRight-click samples color", ArtboardGui.Muted);
            GUI.EndScrollView();
        }

        private void DrawWorkspace(Rect rect, DansToolboxPalette palette)
        {
            EditorGUI.DrawRect(rect, palette.Canvas);
            DrawWorkspaceDots(rect, palette);
            CalculateArtboardRect(rect);
            ProcessWorkspaceInput(rect);

            GUI.BeginClip(rect);
            Rect local = new Rect(artboardRect.x - rect.x, artboardRect.y - rect.y, artboardRect.width, artboardRect.height);
            ArtboardGui.Checker(local, Mathf.Clamp(8f * zoom, 6f, 28f), new Color(0.72f, 0.71f, 0.68f), new Color(0.57f, 0.56f, 0.54f));
            if (!document.Transparent) EditorGUI.DrawRect(local, document.Background);

            if (onionSkin && document.Frames.Count > 1 && !playing)
            {
                int previous = (activeFrame - 1 + document.Frames.Count) % document.Frames.Count;
                int next = (activeFrame + 1) % document.Frames.Count;
                Color old = GUI.color;
                GUI.color = new Color(0.5f, 0.85f, 0.72f, 0.18f);
                GUI.DrawTexture(local, GetFrameTexture(previous), ScaleMode.StretchToFill, true);
                GUI.color = new Color(0.76f, 0.65f, 1f, 0.15f);
                GUI.DrawTexture(local, GetFrameTexture(next), ScaleMode.StretchToFill, true);
                GUI.color = old;
            }
            GUI.DrawTexture(local, GetFrameTexture(activeFrame), ScaleMode.StretchToFill, true);
            if (showGrid && zoom >= 6f) DrawPixelGrid(local, rect, palette);
            DrawBrushIndicator(local, palette);
            ArtboardGui.Border(local, palette.BorderStrong);
            GUI.EndClip();

            DrawWorkspaceBadge(rect, palette);
        }

        private void DrawRightPanel(Rect rect, DansToolboxPalette palette)
        {
            ArtboardGui.Panel(rect, palette);
            Rect header = new Rect(rect.x, rect.y, rect.width, 38f);
            EditorGUI.DrawRect(header, palette.Raised);
            EditorGUI.DrawRect(new Rect(header.x, header.yMax - 1f, header.width, 1f), palette.Border);
            GUI.Label(new Rect(header.x + 11f, header.y + 10f, 90f, 18f), "Layers", ArtboardGui.Label);
            if (ArtboardGui.Button(new Rect(header.xMax - 34f, header.y + 7f, 26f, 25f), "+", "Add layer")) AddLayer();

            float exportHeight = Mathf.Min(235f, rect.height * 0.43f);
            Rect layerArea = new Rect(rect.x + 1f, header.yMax, rect.width - 2f, Mathf.Max(80f, rect.height - header.height - exportHeight));
            DrawLayers(layerArea, palette);
            DrawExportPanel(new Rect(rect.x + 1f, layerArea.yMax, rect.width - 2f, rect.yMax - layerArea.yMax - 1f), palette);
        }

        private void DrawLayers(Rect rect, DansToolboxPalette palette)
        {
            float contentHeight = document.Layers.Count * 47f + 84f;
            Rect content = new Rect(0f, 0f, rect.width - 16f, contentHeight);
            layerScroll = GUI.BeginScrollView(rect, layerScroll, content);
            float y = 8f;
            for (int reverse = document.Layers.Count - 1; reverse >= 0; reverse--)
            {
                ArtboardLayer layer = document.Layers[reverse];
                Rect row = new Rect(7f, y, content.width - 14f, 40f);
                bool selected = reverse == activeLayer;
                EditorGUI.DrawRect(row, selected ? palette.AccentSoft : palette.Raised);
                ArtboardGui.Border(row, selected ? palette.AccentHover : palette.Border);
                Rect eye = new Rect(row.x + 5f, row.y + 7f, 25f, 25f);
                if (ArtboardGui.Button(eye, layer.Visible ? "●" : "○", layer.Visible ? "Hide layer" : "Show layer"))
                {
                    int captured = reverse;
                    MutateDocument("Layer Visibility", () => document.Layers[captured].Visible = !document.Layers[captured].Visible, true);
                }
                GUI.Label(new Rect(row.x + 36f, row.y + 5f, row.width - 68f, 18f), layer.Name, ArtboardGui.Label);
                GUI.Label(new Rect(row.x + 36f, row.y + 22f, row.width - 68f, 14f), Mathf.RoundToInt(layer.Opacity * 100f) + "%", ArtboardGui.Muted);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) activeLayer = reverse;
                Rect more = new Rect(row.xMax - 29f, row.y + 7f, 24f, 25f);
                if (ArtboardGui.Button(more, "…", "Layer actions")) ShowLayerMenu(reverse);
                y += 47f;
            }

            ArtboardLayer active = document.Layers[activeLayer];
            y += 4f;
            EditorGUI.BeginChangeCheck();
            string nextName = EditorGUI.TextField(new Rect(7f, y, content.width - 14f, 22f), active.Name);
            if (EditorGUI.EndChangeCheck() && nextName != active.Name)
            {
                Undo.RecordObject(document, "Rename Layer");
                active.Name = nextName;
                EditorUtility.SetDirty(document);
            }
            y += 28f;
            EditorGUI.BeginChangeCheck();
            float opacity = EditorGUI.Slider(new Rect(7f, y, content.width - 14f, 18f), active.Opacity, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(document, "Layer Opacity");
                active.Opacity = opacity;
                EditorUtility.SetDirty(document);
                InvalidateAllFrames();
            }
            GUI.EndScrollView();
        }

        private void DrawExportPanel(Rect rect, DansToolboxPalette palette)
        {
            EditorGUI.DrawRect(rect, palette.Inset);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), palette.Border);
            Rect content = new Rect(0f, 0f, Mathf.Max(120f, rect.width - 16f), 208f);
            exportScroll = GUI.BeginScrollView(rect, exportScroll, content, false, true);
            float x = 10f;
            float width = content.width - 20f;
            float y = 10f;
            ArtboardGui.SectionLabel(new Rect(x, y, width, 16f), "Unity Export");
            y += 24f;
            EditorGUI.BeginChangeCheck();
            int scale = EditorGUI.IntSlider(new Rect(x, y, width, 18f), document.ExportScale, 1, 16);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(document, "Export Scale");
                document.ExportScale = scale;
                EditorUtility.SetDirty(document);
            }
            y += 26f;
            EditorGUI.BeginChangeCheck();
            int ppu = EditorGUI.IntField(new Rect(x, y, width, 20f), new GUIContent("Pixels / Unit"), document.PixelsPerUnit);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(document, "Pixels Per Unit");
                document.PixelsPerUnit = ppu;
                EditorUtility.SetDirty(document);
            }
            y += 27f;
            createAnimationClip = EditorGUI.ToggleLeft(new Rect(x, y, width, 18f), "Create Animation Clip", createAnimationClip);
            y += 25f;
            int outputWidth = document.Width * document.ExportScale;
            int outputHeight = document.Height * document.ExportScale;
            GUI.Label(new Rect(x, y, width, 18f), $"Frame: {outputWidth:N0} x {outputHeight:N0} px", ArtboardGui.Muted);
            y += 23f;
            if (ArtboardGui.Button(new Rect(x, y, width, 27f), "EXPORT FRAME", "Export current frame as a crisp PNG")) ExportCurrentFrame();
            y += 33f;
            if (ArtboardGui.Button(new Rect(x, y, width, 27f), "EXPORT SPRITE SHEET", "Export, slice, and optionally create an Animation Clip", false, true)) ExportSheet();
            GUI.EndScrollView();
        }

        private void DrawTimeline(Rect rect, DansToolboxPalette palette)
        {
            EditorGUI.DrawRect(rect, palette.Panel);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), palette.Border);
            Rect controls = new Rect(rect.x, rect.y, 220f, rect.height);
            EditorGUI.DrawRect(controls, palette.Inset);
            EditorGUI.DrawRect(new Rect(controls.xMax - 1f, controls.y, 1f, controls.height), palette.Border);
            GUI.Label(new Rect(12f, rect.y + 10f, 100f, 18f), "Timeline", ArtboardGui.Label);
            if (ArtboardGui.Button(new Rect(12f, rect.y + 35f, 44f, 28f), playing ? "■" : "▶", "Play / pause  Space", playing)) TogglePlayback();
            if (ArtboardGui.Button(new Rect(62f, rect.y + 35f, 44f, 28f), "+", "Add blank frame")) AddFrame(false);
            if (ArtboardGui.Button(new Rect(112f, rect.y + 35f, 44f, 28f), "DUP", "Duplicate current frame")) AddFrame(true);
            if (ArtboardGui.Button(new Rect(162f, rect.y + 35f, 44f, 28f), "−", "Delete current frame")) DeleteFrame();
            EditorGUI.BeginChangeCheck();
            int fps = EditorGUI.IntSlider(new Rect(12f, rect.y + 76f, 194f, 18f), document.FramesPerSecond, 1, 30);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(document, "Animation FPS");
                document.FramesPerSecond = fps;
                EditorUtility.SetDirty(document);
            }
            EditorGUI.BeginChangeCheck();
            int hold = EditorGUI.IntField(new Rect(12f, rect.y + 99f, 92f, 18f), new GUIContent("Hold"), document.Frames[activeFrame].Hold);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(document, "Frame Hold");
                document.Frames[activeFrame].Hold = hold;
                EditorUtility.SetDirty(document);
            }
            GUI.Label(new Rect(112f, rect.y + 100f, 94f, 18f), $"Frame {activeFrame + 1}/{document.Frames.Count}", ArtboardGui.Muted);

            Rect frameViewport = new Rect(controls.xMax, rect.y + 1f, rect.width - controls.width, rect.height - 2f);
            Rect frameContent = new Rect(0f, 0f, document.Frames.Count * 88f + 12f, frameViewport.height - 16f);
            timelineScroll = GUI.BeginScrollView(frameViewport, timelineScroll, frameContent, true, false);
            for (int i = 0; i < document.Frames.Count; i++)
            {
                Rect card = new Rect(9f + i * 88f, 8f, 78f, 94f);
                EditorGUI.DrawRect(card, i == activeFrame ? palette.AccentSoft : palette.Raised);
                ArtboardGui.Border(card, i == activeFrame ? palette.AccentHover : palette.Border);
                Rect thumb = new Rect(card.x + 6f, card.y + 6f, card.width - 12f, 62f);
                ArtboardGui.Checker(thumb, 7f, new Color(0.72f, 0.71f, 0.68f), new Color(0.57f, 0.56f, 0.54f));
                GUI.DrawTexture(thumb, GetFrameTexture(i), ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(card.x + 7f, card.y + 72f, card.width - 14f, 16f), $"F{i + 1:D2}  ·  {document.Frames[i].Hold}x", ArtboardGui.Muted);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none))
                {
                    activeFrame = i;
                    playing = false;
                }
            }
            GUI.EndScrollView();
        }

        private void DrawDisabled(Rect rect, DansToolboxPalette palette)
        {
            Rect panel = new Rect(rect.center.x - 180f, rect.center.y - 70f, 360f, 140f);
            ArtboardGui.Panel(panel, palette, true);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 18f, panel.width - 36f, 24f), "Artboard is disabled", ArtboardGui.Centered);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 48f, panel.width - 48f, 32f), "Enable it in Toolbox Setup to resume editing.", ArtboardGui.Muted);
            if (ArtboardGui.Button(new Rect(panel.center.x - 70f, panel.yMax - 44f, 140f, 27f), "OPEN SETUP", "Open Toolbox Setup"))
                EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Setup Wizard");
        }

        private void DrawEmpty(Rect rect, DansToolboxPalette palette)
        {
            DrawWorkspaceDots(rect, palette);
            Rect panel = new Rect(rect.center.x - 220f, rect.center.y - 120f, 440f, 240f);
            ArtboardGui.Panel(panel, palette, true);
            Rect motif = new Rect(panel.center.x - 34f, panel.y + 24f, 68f, 68f);
            ArtboardGui.Checker(motif, 8f, palette.Panel, palette.Inset);
            ArtboardGui.Border(motif, palette.AccentHover);
            EditorGUI.DrawRect(new Rect(motif.x + 22f, motif.y + 22f, 24f, 24f), palette.Accent);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 104f, panel.width - 48f, 24f), "Make sprites that stay sharp", ArtboardGui.Centered);
            GUI.Label(new Rect(panel.x + 42f, panel.y + 134f, panel.width - 84f, 38f),
                "Paint layers, animate cels, and export nearest-neighbor sprites up to 16K per dimension.", ArtboardGui.Muted);
            if (ArtboardGui.Button(new Rect(panel.center.x - 91f, panel.yMax - 48f, 182f, 30f), "CREATE ARTBOARD", "Create a new document", false, true))
                ArtboardNewDocumentWindow.Open();
        }

        private void ProcessWorkspaceInput(Rect workspace)
        {
            Event evt = Event.current;
            if (evt.type == EventType.MouseMove || evt.type == EventType.MouseDrag || evt.type == EventType.MouseDown)
            {
                bool wasVisible = hoverIndicatorVisible;
                hoverIndicatorVisible = workspace.Contains(evt.mousePosition) && artboardRect.Contains(evt.mousePosition);
                if (hoverIndicatorVisible) hoverPixel = ScreenToPixel(evt.mousePosition);
                if (evt.type == EventType.MouseMove || wasVisible != hoverIndicatorVisible) Repaint();
            }
            else if (evt.type == EventType.MouseLeaveWindow)
            {
                hoverIndicatorVisible = false;
                Repaint();
            }

            if (evt.type == EventType.ScrollWheel && workspace.Contains(evt.mousePosition))
            {
                Vector2 before = ScreenToPixelFloat(evt.mousePosition);
                float factor = Mathf.Pow(1.12f, -evt.delta.y);
                zoom = Mathf.Clamp(zoom * factor, 0.05f, 64f);
                CalculateArtboardRect(workspace);
                Vector2 after = ScreenToPixelFloat(evt.mousePosition);
                pan += new Vector2((after.x - before.x) * zoom, -(after.y - before.y) * zoom);
                evt.Use();
                Repaint();
                return;
            }

            bool panInput = evt.button == 2 || (tool == ArtboardTool.Hand && evt.button == 0);
            if (evt.type == EventType.MouseDown && panInput && workspace.Contains(evt.mousePosition))
            {
                panning = true;
                panMouseStart = evt.mousePosition;
                panStart = pan;
                evt.Use();
                return;
            }
            if (panning && evt.type == EventType.MouseDrag)
            {
                pan = panStart + evt.mousePosition - panMouseStart;
                evt.Use();
                Repaint();
                return;
            }
            if (panning && (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp))
            {
                panning = false;
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 1 && artboardRect.Contains(evt.mousePosition))
            {
                SampleColor(ScreenToPixel(evt.mousePosition));
                evt.Use();
                return;
            }
            if (playing || tool == ArtboardTool.Hand) return;
            if (evt.type == EventType.MouseDown && evt.button == 0 && artboardRect.Contains(evt.mousePosition))
            {
                BeginGesture(ScreenToPixel(evt.mousePosition));
                evt.Use();
            }
            else if (drawing && evt.type == EventType.MouseDrag)
            {
                ContinueGesture(ScreenToPixel(evt.mousePosition));
                evt.Use();
                Repaint();
            }
            else if (drawing && (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp))
            {
                EndGesture();
                evt.Use();
            }
        }

        private void BeginGesture(Vector2Int point)
        {
            if (tool == ArtboardTool.Eyedropper)
            {
                SampleColor(point);
                return;
            }
            Color32[] pixels = GetCelPixels(activeFrame, activeLayer);
            if (tool == ArtboardTool.Fill)
            {
                Color32 replacement = color;
                if (ArtboardPixelEngine.FloodFill(pixels, document.Width, document.Height, point.x, point.y, replacement) > 0)
                    CommitPixels("Flood Fill");
                return;
            }
            drawing = true;
            gestureStart = gestureLast = point;
            gestureBase = (Color32[])pixels.Clone();
            if (tool == ArtboardTool.Pencil || tool == ArtboardTool.Brush || tool == ArtboardTool.Eraser)
            {
                DrawFreehand(point, point);
                InvalidateFrame(activeFrame);
            }
        }

        private void ContinueGesture(Vector2Int point)
        {
            if (!drawing) return;
            if (tool == ArtboardTool.Pencil || tool == ArtboardTool.Brush || tool == ArtboardTool.Eraser)
            {
                DrawFreehand(gestureLast, point);
                gestureLast = point;
            }
            else
            {
                Color32[] pixels = GetCelPixels(activeFrame, activeLayer);
                Array.Copy(gestureBase, pixels, pixels.Length);
                DrawShape(gestureStart, point, pixels);
                gestureLast = point;
            }
            InvalidateFrame(activeFrame);
        }

        private void EndGesture()
        {
            if (!drawing) return;
            drawing = false;
            CommitPixels(tool == ArtboardTool.Pencil || tool == ArtboardTool.Brush || tool == ArtboardTool.Eraser ? "Paint Stroke" : "Draw Shape");
            gestureBase = null;
        }

        private void CancelGesture()
        {
            if (!drawing || gestureBase == null) return;
            Color32[] pixels = GetCelPixels(activeFrame, activeLayer);
            Array.Copy(gestureBase, pixels, pixels.Length);
            drawing = false;
            gestureBase = null;
            InvalidateFrame(activeFrame);
            status = "Gesture cancelled";
        }

        private void DrawFreehand(Vector2Int from, Vector2Int to)
        {
            bool pixel = tool == ArtboardTool.Pencil || document.Mode == ArtboardMode.PixelArt;
            ArtboardPixelEngine.DrawStroke(GetCelPixels(activeFrame, activeLayer), document.Width, document.Height,
                from, to, color, brushSize, tool == ArtboardTool.Eraser, !pixel, mirrorX, mirrorY);
        }

        private void DrawShape(Vector2Int from, Vector2Int to, Color32[] pixels)
        {
            switch (tool)
            {
                case ArtboardTool.Line:
                    ArtboardPixelEngine.DrawLine(pixels, document.Width, document.Height, from, to, color, brushSize, false, mirrorX, mirrorY);
                    break;
                case ArtboardTool.Rectangle:
                    ArtboardPixelEngine.DrawRectangle(pixels, document.Width, document.Height,
                        new RectInt(from.x, from.y, to.x - from.x, to.y - from.y), color, brushSize, filledShapes, false);
                    break;
                case ArtboardTool.Ellipse:
                    ArtboardPixelEngine.DrawEllipse(pixels, document.Width, document.Height,
                        new RectInt(from.x, from.y, to.x - from.x, to.y - from.y), color, brushSize, filledShapes, false);
                    break;
            }
        }

        private void CommitPixels(string undoName)
        {
            Color32[] pixels = GetCelPixels(activeFrame, activeLayer);
            Undo.RecordObject(document, undoName);
            document.SetCelPixels(activeFrame, activeLayer, ArtboardPixelEngine.Encode(pixels, document.Width, document.Height));
            EditorUtility.SetDirty(document);
            InvalidateFrame(activeFrame);
            status = undoName;
        }

        private void SampleColor(Vector2Int point)
        {
            Color32[] composite = ArtboardPixelEngine.Composite(document, activeFrame, GetCelPixels, false);
            color = composite[point.y * document.Width + point.x];
            if (color.a == 0) color = Color.white;
            status = "Color sampled";
            Repaint();
        }

        private void AddLayer()
        {
            MutateDocument("Add Layer", () => activeLayer = document.AddLayer(activeLayer), true);
        }

        private void ShowLayerMenu(int index)
        {
            activeLayer = index;
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate"), false, () => MutateDocument("Duplicate Layer", () => activeLayer = document.DuplicateLayer(index), true));
            if (index < document.Layers.Count - 1)
                menu.AddItem(new GUIContent("Move Up"), false, () => MutateDocument("Move Layer", () => { document.MoveLayer(index, index + 1); activeLayer = index + 1; }, true));
            else menu.AddDisabledItem(new GUIContent("Move Up"));
            if (index > 0)
                menu.AddItem(new GUIContent("Move Down"), false, () => MutateDocument("Move Layer", () => { document.MoveLayer(index, index - 1); activeLayer = index - 1; }, true));
            else menu.AddDisabledItem(new GUIContent("Move Down"));
            menu.AddSeparator(string.Empty);
            if (document.Layers.Count > 1)
                menu.AddItem(new GUIContent("Delete"), false, () => MutateDocument("Delete Layer", () => activeLayer = document.DeleteLayer(index), true));
            else menu.AddDisabledItem(new GUIContent("Delete"));
            menu.ShowAsContext();
        }

        private void AddFrame(bool duplicate)
        {
            MutateDocument(duplicate ? "Duplicate Frame" : "Add Frame",
                () => activeFrame = document.AddFrame(activeFrame, duplicate), true);
        }

        private void DeleteFrame()
        {
            if (document.Frames.Count <= 1) return;
            MutateDocument("Delete Frame", () => activeFrame = document.DeleteFrame(activeFrame), true);
        }

        private void MutateDocument(string undoName, Action mutation, bool clearAllFrames)
        {
            Undo.RecordObject(document, undoName);
            mutation();
            document.EnsureIntegrity();
            EditorUtility.SetDirty(document);
            ClampSelection();
            if (clearAllFrames) ClearCaches();
            else InvalidateFrame(activeFrame);
            status = undoName;
            Repaint();
        }

        private void ExportCurrentFrame()
        {
            string path = ArtboardExportService.ExportFrame(document, activeFrame, document.ExportScale, GetCelPixels);
            if (!string.IsNullOrEmpty(path)) status = "Exported " + path;
        }

        private void ExportSheet()
        {
            string path = ArtboardExportService.ExportSheet(document, document.ExportScale, GetCelPixels, createAnimationClip);
            if (!string.IsNullOrEmpty(path)) status = "Exported " + path;
        }

        private void SaveDocument()
        {
            if (document == null) return;
            EditorUtility.SetDirty(document);
            AssetDatabase.SaveAssets();
            status = "Saved " + document.name;
        }

        private void TogglePlayback()
        {
            if (document == null || document.Frames.Count <= 1) return;
            playing = !playing;
            playbackLast = EditorApplication.timeSinceStartup;
            status = playing ? "Playing" : "Paused";
            Repaint();
        }

        private void HandleKeyboard()
        {
            Event evt = Event.current;
            if (evt.type != EventType.KeyDown) return;
            if (EditorGUIUtility.editingTextField && evt.keyCode != KeyCode.Escape) return;
            bool command = evt.control || evt.command;
            if (command && evt.keyCode == KeyCode.S) { SaveDocument(); evt.Use(); return; }
            if (command && evt.keyCode == KeyCode.E) { if (document != null) ExportCurrentFrame(); evt.Use(); return; }
            if (evt.keyCode == KeyCode.Escape) { CancelGesture(); playing = false; evt.Use(); return; }
            if (document == null) return;
            if (evt.keyCode == KeyCode.Space) { TogglePlayback(); evt.Use(); return; }
            if (evt.keyCode == KeyCode.B) { tool = ArtboardTool.Brush; evt.Use(); }
            else if (evt.keyCode == KeyCode.P) { tool = ArtboardTool.Pencil; evt.Use(); }
            else if (evt.keyCode == KeyCode.E) { tool = ArtboardTool.Eraser; evt.Use(); }
            else if (evt.keyCode == KeyCode.G) { tool = ArtboardTool.Fill; evt.Use(); }
            else if (evt.keyCode == KeyCode.L) { tool = ArtboardTool.Line; evt.Use(); }
            else if (evt.keyCode == KeyCode.R) { tool = ArtboardTool.Rectangle; evt.Use(); }
            else if (evt.keyCode == KeyCode.O) { tool = ArtboardTool.Ellipse; evt.Use(); }
            else if (evt.keyCode == KeyCode.I) { tool = ArtboardTool.Eyedropper; evt.Use(); }
            else if (evt.keyCode == KeyCode.H) { tool = ArtboardTool.Hand; evt.Use(); }
            else if (evt.keyCode == KeyCode.LeftBracket) { brushSize = Mathf.Max(1, brushSize - 1); evt.Use(); }
            else if (evt.keyCode == KeyCode.RightBracket) { brushSize = Mathf.Min(256, brushSize + 1); evt.Use(); }
        }

        private void SetDocument(ArtboardAsset asset, bool select)
        {
            CancelGesture();
            playing = false;
            document = asset;
            activeFrame = activeLayer = 0;
            ClearCaches();
            if (document != null)
            {
                document.EnsureIntegrity();
                showGrid = document.Mode == ArtboardMode.PixelArt;
                brushSize = document.Mode == ArtboardMode.PixelArt ? 1 : 8;
                zoom = 1f;
                FitCanvas();
                status = "Opened " + document.name;
                if (select) Selection.activeObject = document;
            }
            Repaint();
        }

        private Color32[] GetCelPixels(int frameIndex, int layerIndex)
        {
            string key = frameIndex + ":" + layerIndex;
            if (celCache.TryGetValue(key, out Color32[] pixels)) return pixels;
            ArtboardCel cel = document.GetCel(frameIndex, layerIndex);
            pixels = ArtboardPixelEngine.Decode(cel.PngData, document.Width, document.Height);
            celCache[key] = pixels;
            return pixels;
        }

        private Texture2D GetFrameTexture(int frameIndex)
        {
            frameIndex = Mathf.Clamp(frameIndex, 0, document.Frames.Count - 1);
            if (frameTextures.TryGetValue(frameIndex, out Texture2D texture) && texture != null) return texture;
            Color32[] composite = ArtboardPixelEngine.Composite(document, frameIndex, GetCelPixels, false);
            texture = new Texture2D(document.Width, document.Height, TextureFormat.RGBA32, false, true)
            {
                name = document.name + " Preview",
                filterMode = document.Mode == ArtboardMode.PixelArt ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(composite);
            texture.Apply(false, false);
            frameTextures[frameIndex] = texture;
            return texture;
        }

        private void InvalidateFrame(int frameIndex)
        {
            if (frameTextures.TryGetValue(frameIndex, out Texture2D texture) && texture != null) DestroyImmediate(texture);
            frameTextures.Remove(frameIndex);
        }

        private void InvalidateAllFrames()
        {
            foreach (Texture2D texture in frameTextures.Values) if (texture != null) DestroyImmediate(texture);
            frameTextures.Clear();
        }

        private void ClearCaches()
        {
            celCache.Clear();
            InvalidateAllFrames();
        }

        private void DisposeTextures()
        {
            ClearCaches();
        }

        private void CalculateArtboardRect(Rect workspace)
        {
            float width = document.Width * zoom;
            float height = document.Height * zoom;
            artboardRect = new Rect(workspace.center.x - width * 0.5f + pan.x,
                workspace.center.y - height * 0.5f + pan.y, width, height);
        }

        private void FitCanvas()
        {
            if (document == null) return;
            float availableWidth = workspaceRect.width > 100f ? workspaceRect.width : Mathf.Max(100f, position.width - 500f);
            float availableHeight = workspaceRect.height > 100f ? workspaceRect.height : Mathf.Max(100f, position.height - 190f);
            zoom = Mathf.Clamp(Mathf.Min((availableWidth - 70f) / document.Width, (availableHeight - 70f) / document.Height), 0.05f, 64f);
            pan = Vector2.zero;
            Repaint();
        }

        private Vector2Int ScreenToPixel(Vector2 mouse)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((mouse.x - artboardRect.x) / zoom), 0, document.Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((artboardRect.yMax - mouse.y) / zoom), 0, document.Height - 1);
            return new Vector2Int(x, y);
        }

        private Vector2 ScreenToPixelFloat(Vector2 mouse)
        {
            return new Vector2((mouse.x - artboardRect.x) / Mathf.Max(0.001f, zoom),
                (artboardRect.yMax - mouse.y) / Mathf.Max(0.001f, zoom));
        }

        private void DrawPixelGrid(Rect localArtboard, Rect workspace, DansToolboxPalette palette)
        {
            Color grid = new Color(palette.BorderStrong.r, palette.BorderStrong.g, palette.BorderStrong.b, 0.42f);
            int firstX = Mathf.Clamp(Mathf.FloorToInt((workspace.x - artboardRect.x) / zoom), 0, document.Width);
            int lastX = Mathf.Clamp(Mathf.CeilToInt((workspace.xMax - artboardRect.x) / zoom), 0, document.Width);
            int firstY = Mathf.Clamp(Mathf.FloorToInt((artboardRect.yMax - workspace.yMax) / zoom), 0, document.Height);
            int lastY = Mathf.Clamp(Mathf.CeilToInt((artboardRect.yMax - workspace.y) / zoom), 0, document.Height);
            for (int x = firstX; x <= lastX; x++) EditorGUI.DrawRect(new Rect(localArtboard.x + x * zoom, localArtboard.y, 1f, localArtboard.height), grid);
            for (int y = firstY; y <= lastY; y++) EditorGUI.DrawRect(new Rect(localArtboard.x, localArtboard.yMax - y * zoom, localArtboard.width, 1f), grid);
        }

        private void DrawBrushIndicator(Rect localArtboard, DansToolboxPalette palette)
        {
            if (!hoverIndicatorVisible || playing ||
                (tool != ArtboardTool.Pencil && tool != ArtboardTool.Brush && tool != ArtboardTool.Eraser)) return;

            Rect indicator = CalculateBrushIndicatorRect(
                localArtboard, document.Height, hoverPixel, brushSize, zoom);
            bool erasing = tool == ArtboardTool.Eraser;
            bool circular = document.Mode != ArtboardMode.PixelArt &&
                            (tool == ArtboardTool.Brush || tool == ArtboardTool.Eraser);
            Color outline = erasing ? palette.Danger : palette.AccentHover;
            Color fill = erasing
                ? new Color(palette.Danger.r, palette.Danger.g, palette.Danger.b, 0.18f)
                : new Color(color.r, color.g, color.b, 0.16f);
            Vector2 center = indicator.center;

            if (circular)
            {
                Handles.BeginGUI();
                Handles.color = fill;
                Handles.DrawSolidDisc(center, Vector3.forward, indicator.width * 0.5f);
                Handles.color = new Color(0f, 0f, 0f, 0.82f);
                Handles.DrawWireDisc(center, Vector3.forward, indicator.width * 0.5f + 1f);
                Handles.color = outline;
                Handles.DrawWireDisc(center, Vector3.forward, indicator.width * 0.5f);
                if (erasing) Handles.DrawLine(indicator.min + Vector2.one * 2f, indicator.max - Vector2.one * 2f);
                Handles.EndGUI();
            }
            else
            {
                EditorGUI.DrawRect(indicator, fill);
                ArtboardGui.Border(new Rect(indicator.x - 1f, indicator.y - 1f, indicator.width + 2f, indicator.height + 2f), new Color(0f, 0f, 0f, 0.82f));
                ArtboardGui.Border(indicator, outline);
                if (erasing)
                {
                    Handles.BeginGUI();
                    Handles.color = outline;
                    Handles.DrawLine(indicator.min + Vector2.one * 2f, indicator.max - Vector2.one * 2f);
                    Handles.EndGUI();
                }
                else
                {
                    EditorGUI.DrawRect(new Rect(center.x - 1f, center.y - 1f, 2f, 2f), outline);
                }
            }

            Rect readout = new Rect(indicator.xMax + 6f, indicator.yMax + 3f, 44f, 18f);
            if (readout.xMax > localArtboard.xMax) readout.x = indicator.xMin - readout.width - 6f;
            if (readout.yMax > localArtboard.yMax) readout.y = indicator.yMin - readout.height - 3f;
            EditorGUI.DrawRect(readout, new Color(palette.Panel.r, palette.Panel.g, palette.Panel.b, 0.92f));
            ArtboardGui.Border(readout, erasing ? palette.Danger : palette.BorderStrong);
            GUI.Label(new Rect(readout.x + 4f, readout.y + 1f, readout.width - 8f, 16f), brushSize + " px", ArtboardGui.Muted);
        }

        internal static Rect CalculateBrushIndicatorRect(
            Rect localArtboard,
            int canvasHeight,
            Vector2Int pixel,
            int size,
            float pixelsToPoints,
            float minimumVisualSize = 10f)
        {
            size = Mathf.Max(1, size);
            pixelsToPoints = Mathf.Max(0.001f, pixelsToPoints);
            int minimumOffset = -(size / 2);
            int maximumOffset = minimumOffset + size - 1;
            float x = localArtboard.x + (pixel.x + minimumOffset) * pixelsToPoints;
            float y = localArtboard.y + (canvasHeight - (pixel.y + maximumOffset + 1)) * pixelsToPoints;
            float actualSize = size * pixelsToPoints;
            Rect exact = new Rect(x, y, actualSize, actualSize);
            float visualSize = Mathf.Max(minimumVisualSize, actualSize);
            return new Rect(
                exact.center.x - visualSize * 0.5f,
                exact.center.y - visualSize * 0.5f,
                visualSize,
                visualSize);
        }

        private void DrawWorkspaceDots(Rect rect, DansToolboxPalette palette)
        {
            Color dot = new Color(palette.Border.r, palette.Border.g, palette.Border.b, 0.55f);
            const float spacing = 24f;
            int columns = Mathf.CeilToInt(rect.width / spacing);
            int rows = Mathf.CeilToInt(rect.height / spacing);
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                    EditorGUI.DrawRect(new Rect(rect.x + x * spacing, rect.y + y * spacing, 1f, 1f), dot);
        }

        private void DrawWorkspaceBadge(Rect rect, DansToolboxPalette palette)
        {
            string text = $"{ToolLabel(tool)}  ·  {document.Width} x {document.Height}  ·  Frame {activeFrame + 1}/{document.Frames.Count}";
            Rect badge = new Rect(rect.x + 10f, rect.y + 10f, Mathf.Min(rect.width - 20f, 260f), 24f);
            EditorGUI.DrawRect(badge, new Color(palette.Panel.r, palette.Panel.g, palette.Panel.b, 0.94f));
            ArtboardGui.Border(badge, palette.Border);
            GUI.Label(new Rect(badge.x + 8f, badge.y + 4f, badge.width - 16f, 16f), text, ArtboardGui.Muted);
            if (!string.IsNullOrEmpty(status) && rect.width > 430f)
            {
                Rect state = new Rect(rect.xMax - 170f, rect.yMax - 32f, 160f, 22f);
                EditorGUI.DrawRect(state, new Color(palette.Panel.r, palette.Panel.g, palette.Panel.b, 0.92f));
                GUI.Label(new Rect(state.x + 7f, state.y + 3f, state.width - 14f, 16f), status, ArtboardGui.Muted);
            }
        }

        private void ClampSelection()
        {
            if (document == null) return;
            activeFrame = Mathf.Clamp(activeFrame, 0, document.Frames.Count - 1);
            activeLayer = Mathf.Clamp(activeLayer, 0, document.Layers.Count - 1);
        }

        private static bool Approximately(Color colorValue, Color32 byteValue)
        {
            Color32 current = colorValue;
            return current.r == byteValue.r && current.g == byteValue.g && current.b == byteValue.b;
        }

        private static string ModeLabel(ArtboardMode mode)
        {
            switch (mode)
            {
                case ArtboardMode.Animation: return "Animation";
                case ArtboardMode.PixelArt: return "Pixel Art";
                default: return "Digital Art";
            }
        }

        private static string ToolLabel(ArtboardTool value)
        {
            switch (value)
            {
                case ArtboardTool.Pencil: return "Pencil";
                case ArtboardTool.Brush: return "Brush";
                case ArtboardTool.Eraser: return "Eraser";
                case ArtboardTool.Fill: return "Fill";
                case ArtboardTool.Line: return "Line";
                case ArtboardTool.Rectangle: return "Rectangle";
                case ArtboardTool.Ellipse: return "Ellipse";
                case ArtboardTool.Eyedropper: return "Eyedropper";
                default: return "Hand";
            }
        }

        private static string ToolShortcut(ArtboardTool value)
        {
            switch (value)
            {
                case ArtboardTool.Pencil: return "P";
                case ArtboardTool.Brush: return "B";
                case ArtboardTool.Eraser: return "E";
                case ArtboardTool.Fill: return "G";
                case ArtboardTool.Line: return "L";
                case ArtboardTool.Rectangle: return "R";
                case ArtboardTool.Ellipse: return "O";
                case ArtboardTool.Eyedropper: return "I";
                default: return "H";
            }
        }
    }

    internal sealed class ArtboardNewDocumentWindow : EditorWindow
    {
        [SerializeField] private int documentWidth = 256;
        [SerializeField] private int documentHeight = 256;
        [SerializeField] private ArtboardMode mode = ArtboardMode.PixelArt;

        internal static void Open()
        {
            ArtboardNewDocumentWindow window = CreateInstance<ArtboardNewDocumentWindow>();
            window.titleContent = new GUIContent("New Artboard");
            window.minSize = window.maxSize = new Vector2(360f, 210f);
            window.ShowUtility();
            window.Focus();
        }

        private void OnGUI()
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), palette.Canvas);
            GUI.Label(new Rect(18f, 16f, position.width - 36f, 22f), "New Artboard", new GUIStyle(ArtboardGui.Label) { fontSize = 15, fontStyle = FontStyle.Bold });
            GUI.Label(new Rect(18f, 41f, position.width - 36f, 32f), "Choose an editable source size. Export scale can enlarge it later without blur.", ArtboardGui.Muted);
            documentWidth = EditorGUI.IntField(new Rect(18f, 82f, 154f, 20f), new GUIContent("Width"), documentWidth);
            documentHeight = EditorGUI.IntField(new Rect(188f, 82f, 154f, 20f), new GUIContent("Height"), documentHeight);
            mode = (ArtboardMode)EditorGUI.EnumPopup(new Rect(18f, 112f, 324f, 20f), new GUIContent("Workspace"), mode);
            documentWidth = Mathf.Clamp(documentWidth, ArtboardAsset.MinDimension, ArtboardAsset.MaxDimension);
            documentHeight = Mathf.Clamp(documentHeight, ArtboardAsset.MinDimension, ArtboardAsset.MaxDimension);
            if (ArtboardGui.Button(new Rect(18f, 162f, 96f, 29f), "CANCEL", "Close without creating")) Close();
            if (ArtboardGui.Button(new Rect(224f, 162f, 118f, 29f), "CREATE", "Create the Artboard asset", false, true))
            {
                string path = EditorUtility.SaveFilePanelInProject("Create Artboard", "New Artboard", "asset", "Choose where to save the editable Artboard document.");
                if (!string.IsNullOrEmpty(path))
                {
                    ArtboardWindow.CreateAndOpenDocument(documentWidth, documentHeight, mode, path);
                    Close();
                }
            }
        }
    }
}
