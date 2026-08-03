using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal enum DansToolboxSetupStep
    {
        Theme,
        Tools,
        Layout
    }

    [InitializeOnLoad]
    internal static class DansToolboxSetupPrompt
    {
        static DansToolboxSetupPrompt()
        {
            EditorApplication.delayCall += OfferSetup;
        }

        private static void OfferSetup()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OfferSetup;
                return;
            }

            if (DansToolboxSettings.ShouldOfferSetup)
            {
                DansToolboxSetupWizard.Open();
            }
        }
    }

    internal sealed class DansToolboxSetupWizard : EditorWindow
    {
        private const float Margin = 18f;
        private const float Gap = 10f;
        private const float StepRailHeight = 36f;
        private const float FooterHeight = 38f;
        private const float OverlayPanelWidth = 652f;
        private const float OverlayPanelHeight = 642f;
        private const float OverlayEdgeMargin = 36f;
        private const double SplashDuration = 0.82d;
        private const double StepTransitionDuration = 0.24d;
        private const float InstallFadeStart = 0.72f;
        private static readonly string[] StepLabels = { "01  THEME", "02  TOOLS", "03  WORKSPACE" };

        [SerializeField] private DansToolboxSetupStep currentStep;
        [SerializeField] private DansToolboxThemeId selectedTheme;
        [SerializeField] private List<string> enabledToolIds = new List<string>();
        [SerializeField] private bool useRecommendedLayout;
        [SerializeField] private bool seamlessToolSurfaces = true;
        [SerializeField] private bool stagedStateLoaded;
        [SerializeField] private Vector2 scrollPosition;
        [SerializeField] private bool showSplash;
        [SerializeField] private double splashStartedAt;
        [SerializeField] private double stepTransitionStartedAt;
        [SerializeField] private int stepTransitionDirection = 1;

        private WizardStyles styles;
        private DansToolboxThemeId styledTheme = (DansToolboxThemeId)(-1);
        private bool styledSeamlessToolSurfaces;
        [System.NonSerialized] private Texture2D backdrop;
        [System.NonSerialized] private float surfaceWidth;
        [System.NonSerialized] private float surfaceHeight;

        internal DansToolboxSetupStep CurrentStep => currentStep;

        [MenuItem("Tools/Dans Toolbox/Setup Wizard", false, -80)]
        internal static void Open()
        {
            foreach (DansToolboxSetupWizard existing in
                     Resources.FindObjectsOfTypeAll<DansToolboxSetupWizard>())
            {
                existing.Close();
            }

            Texture2D capturedBackdrop = DansToolboxEditorBackdrop.CaptureBlurred();
            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            DansToolboxSetupWizard window = CreateInstance<DansToolboxSetupWizard>();
            window.titleContent = new GUIContent("Dans Toolbox Setup");
            window.backdrop = capturedBackdrop;
            window.position = mainWindow;
            window.minSize = mainWindow.size;
            window.maxSize = mainWindow.size;
            window.ResetFromSettings();
            window.ShowPopup();
            window.Focus();
        }

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= CloseForReload;
            AssemblyReloadEvents.beforeAssemblyReload += CloseForReload;
            if (!stagedStateLoaded)
            {
                ResetFromSettings();
            }
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= CloseForReload;
            if (backdrop != null)
            {
                DestroyImmediate(backdrop);
                backdrop = null;
            }
        }

        private void CloseForReload()
        {
            Close();
        }

        private void ResetFromSettings()
        {
            selectedTheme = DansToolboxSettings.Theme;
            useRecommendedLayout =
                !DansToolboxSettings.IsInitialized ||
                DansToolboxSettings.RecommendedLayoutSelected;
            seamlessToolSurfaces =
                !DansToolboxSettings.IsInitialized ||
                DansToolboxSettings.SeamlessToolSurfaces;
            enabledToolIds ??= new List<string>();
            enabledToolIds.Clear();
            foreach (DansToolboxToolDescriptor tool in DansToolboxTools.All)
            {
                if (DansToolboxSettings.IsToolEnabled(tool.Id))
                {
                    enabledToolIds.Add(tool.Id);
                }
            }

            currentStep = DansToolboxSetupStep.Theme;
            scrollPosition = Vector2.zero;
            stagedStateLoaded = true;
            styledTheme = (DansToolboxThemeId)(-1);
            showSplash = true;
            splashStartedAt = EditorApplication.timeSinceStartup;
            stepTransitionStartedAt = splashStartedAt + SplashDuration;
            stepTransitionDirection = 1;
            Repaint();
        }

        private void OnGUI()
        {
            HandleKeyboard();

            DansToolboxPalette palette = DansToolboxTheme.GetPalette(
                selectedTheme,
                seamlessToolSurfaces);
            EnsureStyles(palette);
            Rect canvas = new Rect(0f, 0f, position.width, position.height);
            DrawBackdrop(canvas, palette, 1f);

            Rect panel = CalculatePanelRect(canvas.size);

            Event current = Event.current;
            if (current.type == EventType.MouseDown &&
                !panel.Contains(current.mousePosition))
            {
                Close();
                current.Use();
                return;
            }

            DrawOverlayPanel(panel, palette);
            Rect innerPanel = new Rect(
                panel.x + 1f,
                panel.y + 1f,
                panel.width - 2f,
                panel.height - 2f);
            GUI.BeginGroup(innerPanel);
            DrawWizardSurface(palette, innerPanel.width, innerPanel.height);
            GUI.EndGroup();
        }

        private void DrawWizardSurface(
            DansToolboxPalette palette,
            float width,
            float height)
        {
            surfaceWidth = width;
            surfaceHeight = height;
            EditorGUI.DrawRect(new Rect(0f, 0f, surfaceWidth, surfaceHeight), palette.Canvas);
            if (!seamlessToolSurfaces)
            {
                EditorGUI.DrawRect(new Rect(0f, 0f, surfaceWidth, 2f), palette.Accent);
            }

            if (showSplash)
            {
                DrawSplash(palette);
                return;
            }

            float innerWidth = surfaceWidth - Margin * 2f;
            float footerY = surfaceHeight - Margin - FooterHeight;
            Rect railRect = new Rect(Margin, Margin, innerWidth, StepRailHeight);
            Rect contentRect = new Rect(
                Margin,
                railRect.yMax + 18f,
                innerWidth,
                Mathf.Max(1f, footerY - railRect.yMax - 32f));
            Rect footerRect = new Rect(Margin, footerY, innerWidth, FooterHeight);

            DrawStepRail(railRect, palette);
            DrawCurrentStep(contentRect, palette);
            DrawFooter(footerRect, palette);
        }

        internal static Rect CalculatePanelRect(Vector2 canvasSize)
        {
            float width = Mathf.Min(
                OverlayPanelWidth,
                Mathf.Max(1f, canvasSize.x - OverlayEdgeMargin * 2f));
            float height = Mathf.Min(
                OverlayPanelHeight,
                Mathf.Max(1f, canvasSize.y - OverlayEdgeMargin * 2f));
            return new Rect(
                (canvasSize.x - width) * 0.5f,
                (canvasSize.y - height) * 0.5f,
                width,
                height);
        }

        private void DrawBackdrop(Rect canvas, DansToolboxPalette palette, float opacity)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, opacity);
            if (backdrop != null)
            {
                GUI.DrawTexture(canvas, backdrop, ScaleMode.StretchToFill, false);
            }
            else
            {
                Color fallback = palette.Inset;
                fallback.a *= opacity;
                EditorGUI.DrawRect(canvas, fallback);
            }
            GUI.color = previousColor;

            EditorGUI.DrawRect(
                canvas,
                new Color(0.015f, 0.02f, 0.028f, 0.72f * opacity));
        }

        private static void DrawOverlayPanel(Rect panel, DansToolboxPalette palette)
        {
            for (int index = 5; index >= 1; index--)
            {
                float spread = index * 5f;
                Color shadow = Color.black;
                shadow.a = 0.035f * (6 - index);
                EditorGUI.DrawRect(
                    new Rect(
                        panel.x - spread,
                        panel.y - spread + 8f,
                        panel.width + spread * 2f,
                        panel.height + spread * 2f),
                    shadow);
            }

            EditorGUI.DrawRect(panel, palette.BorderStrong);
        }

        private void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            bool repaint = false;

            if (showSplash)
            {
                repaint = true;
                if (now - splashStartedAt >= SplashDuration)
                {
                    showSplash = false;
                    stepTransitionStartedAt = now;
                }
            }

            if (StepTransitionProgress < 1f)
            {
                repaint = true;
            }

            if (repaint)
            {
                Repaint();
            }
        }

        private float StepTransitionProgress => Mathf.Clamp01((float)(
            (EditorApplication.timeSinceStartup - stepTransitionStartedAt) /
            StepTransitionDuration));

        private void DrawStepRail(Rect rect, DansToolboxPalette palette)
        {
            float segmentWidth = (rect.width - 2f) / StepLabels.Length;

            for (int index = 0; index < StepLabels.Length; index++)
            {
                Rect segment = new Rect(
                    rect.x + index * segmentWidth,
                    rect.y,
                    segmentWidth,
                    rect.height);
                bool active = index == (int)currentStep;
                bool complete = index < (int)currentStep;
                DrawPanel(
                    segment,
                    active ? palette.Raised : palette.Inset,
                    active ? palette.BorderStrong : palette.Border);

                if (active)
                {
                    EditorGUI.DrawRect(
                        new Rect(segment.x + 1f, segment.yMax - 3f, segment.width - 2f, 2f),
                        palette.Accent);
                }

                GUIStyle labelStyle = active
                    ? styles.StepActive
                    : complete ? styles.StepComplete : styles.StepInactive;
                GUI.Label(segment, StepLabels[index], labelStyle);
            }
        }

        private void DrawCurrentStep(Rect rect, DansToolboxPalette palette)
        {
            float eased = EaseOutCubic(StepTransitionProgress);
            float offset = (1f - eased) * 30f * stepTransitionDirection;
            Rect animatedRect = new Rect(rect.x + offset, rect.y, rect.width, rect.height);
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.2f, 1f, eased));

            GUILayout.BeginArea(animatedRect);
            scrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none);

            switch (currentStep)
            {
                case DansToolboxSetupStep.Tools:
                    DrawToolsStep(palette);
                    break;
                case DansToolboxSetupStep.Layout:
                    DrawLayoutStep(palette);
                    break;
                default:
                    DrawThemeStep();
                    break;
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.color = previousColor;
        }

        private void DrawThemeStep()
        {
            DrawScreenTitle("Choose a theme");
            GUILayout.Space(16f);

            Rect row = GUILayoutUtility.GetRect(1f, 132f, GUILayout.ExpandWidth(true));
            float cardWidth = (row.width - Gap * 2f) / 3f;
            DrawThemeCard(new Rect(row.x, row.y, cardWidth, row.height),
                DansToolboxThemeId.SignalOrange);
            DrawThemeCard(new Rect(row.x + cardWidth + Gap, row.y, cardWidth, row.height),
                DansToolboxThemeId.NeonCyan);
            DrawThemeCard(new Rect(row.x + (cardWidth + Gap) * 2f, row.y, cardWidth, row.height),
                DansToolboxThemeId.ArcadeViolet);
        }

        private void DrawThemeCard(Rect rect, DansToolboxThemeId theme)
        {
            DansToolboxPalette cardPalette = DansToolboxTheme.GetPalette(theme);
            bool selected = selectedTheme == theme;
            bool hovered = rect.Contains(Event.current.mousePosition);

            DrawPanel(
                rect,
                hovered || selected ? cardPalette.Raised : cardPalette.Panel,
                selected ? cardPalette.Accent : hovered ? cardPalette.BorderStrong : cardPalette.Border);
            EditorGUI.DrawRect(new Rect(rect.x + 12f, rect.y + 16f, rect.width - 24f, 12f),
                cardPalette.Accent);
            EditorGUI.DrawRect(new Rect(rect.x + 12f, rect.y + 34f, rect.width - 24f, 8f),
                cardPalette.Signal);
            EditorGUI.DrawRect(new Rect(rect.x + 12f, rect.y + 48f, rect.width - 24f, 6f),
                cardPalette.BorderStrong);
            GUI.Label(
                new Rect(rect.x + 12f, rect.yMax - 45f, rect.width - 48f, 24f),
                DansToolboxTheme.GetDisplayName(theme),
                MakeLabel(cardPalette.Text, 11, FontStyle.Bold));

            if (selected)
            {
                DrawChoiceMark(new Rect(rect.xMax - 32f, rect.yMax - 39f, 18f, 18f), cardPalette);
            }

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                selectedTheme = theme;
                styledTheme = (DansToolboxThemeId)(-1);
                Repaint();
            }
        }

        private void DrawToolsStep(DansToolboxPalette palette)
        {
            DrawScreenTitle("Choose tools");
            GUILayout.Space(14f);

            foreach (DansToolboxToolDescriptor tool in DansToolboxTools.All)
            {
                Rect rect = GUILayoutUtility.GetRect(1f, 62f, GUILayout.ExpandWidth(true));
                bool enabled = enabledToolIds.Contains(tool.Id);
                bool hovered = rect.Contains(Event.current.mousePosition);

                DrawPanel(
                    rect,
                    hovered ? palette.Raised : palette.Panel,
                    enabled ? palette.Accent : hovered ? palette.BorderStrong : palette.Border);
                DrawToggleIndicator(new Rect(rect.x + 15f, rect.y + 17f, 28f, 28f), enabled, palette);
                GUI.Label(
                    new Rect(rect.x + 58f, rect.y + 17f, rect.width - 170f, 28f),
                    tool.Name.ToUpperInvariant(),
                    styles.CardTitle);

                if (tool.WindowsOnly)
                {
                    GUI.Label(
                        new Rect(rect.xMax - 92f, rect.y + 21f, 76f, 20f),
                        "WINDOWS",
                        styles.Badge);
                }

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    if (enabled)
                    {
                        enabledToolIds.Remove(tool.Id);
                    }
                    else
                    {
                        enabledToolIds.Add(tool.Id);
                    }
                }

                GUILayout.Space(8f);
            }
        }

        private void DrawLayoutStep(DansToolboxPalette palette)
        {
            DrawScreenTitle("Choose workspace behavior");
            GUILayout.Space(16f);

            Rect row = GUILayoutUtility.GetRect(1f, 174f, GUILayout.ExpandWidth(true));
            float cardWidth = (row.width - Gap) / 2f;
            Rect toolboxRect = new Rect(row.x, row.y, cardWidth, row.height);
            Rect currentRect = new Rect(row.x + cardWidth + Gap, row.y, cardWidth, row.height);
            DrawLayoutCard(toolboxRect, true, palette);
            DrawLayoutCard(currentRect, false, palette);

            GUILayout.Space(12f);
            Rect seamlessRect = GUILayoutUtility.GetRect(
                1f,
                92f,
                GUILayout.ExpandWidth(true));
            DrawSeamlessSurfaceCard(seamlessRect, palette);
        }

        private void DrawSeamlessSurfaceCard(Rect rect, DansToolboxPalette palette)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            DrawPanel(
                rect,
                hovered ? palette.Raised : palette.Panel,
                seamlessToolSurfaces ? palette.Accent : palette.Border);

            Rect preview = new Rect(rect.x + 14f, rect.y + 14f, 126f, rect.height - 28f);
            DrawSeamlessSurfacePreview(preview, palette, seamlessToolSurfaces);

            float copyX = preview.xMax + 16f;
            float statusWidth = 50f;
            GUI.Label(
                new Rect(copyX, rect.y + 14f, rect.xMax - copyX - statusWidth - 18f, 22f),
                "SEAMLESS TOOL SURFACES",
                styles.CardTitle);
            GUI.Label(
                new Rect(copyX, rect.y + 36f, rect.xMax - copyX - statusWidth - 18f, 40f),
                "Visual only: shared Toolbox surfaces and softer internal dividers. Native tabs and docking never change.",
                styles.CardBody);
            GUI.Label(
                new Rect(rect.xMax - statusWidth - 14f, rect.y + 14f, statusWidth, 22f),
                seamlessToolSurfaces ? "ON" : "OFF",
                seamlessToolSurfaces ? styles.Badge : styles.DangerBadge);

            if (seamlessToolSurfaces)
            {
                DrawChoiceMark(
                    new Rect(rect.xMax - 32f, rect.yMax - 32f, 18f, 18f),
                    palette);
            }

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                seamlessToolSurfaces = !seamlessToolSurfaces;
                Repaint();
            }
        }

        private void DrawLayoutCard(Rect rect, bool recommended, DansToolboxPalette palette)
        {
            bool available = !recommended || DansToolboxLayoutInstaller.IsLayoutAvailable;
            bool selected = useRecommendedLayout == recommended && available;
            bool hovered = available && rect.Contains(Event.current.mousePosition);

            DrawPanel(
                rect,
                hovered || selected ? palette.Raised : palette.Panel,
                selected ? palette.Accent : available ? palette.Border : palette.Danger);
            DrawLayoutPreview(
                new Rect(rect.x + 14f, rect.y + 14f, rect.width - 28f, 92f),
                palette,
                recommended);
            GUI.Label(
                new Rect(rect.x + 14f, rect.yMax - 48f, rect.width - 48f, 28f),
                recommended ? "ORGANIZED" : "KEEP OPEN WINDOWS",
                styles.CardTitle);

            if (selected)
            {
                DrawChoiceMark(new Rect(rect.xMax - 32f, rect.yMax - 42f, 18f, 18f), palette);
            }
            else if (!available)
            {
                GUI.Label(
                    new Rect(rect.xMax - 104f, rect.yMax - 45f, 88f, 22f),
                    "UNAVAILABLE",
                    styles.DangerBadge);
            }

            EditorGUI.BeginDisabledGroup(!available);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                useRecommendedLayout = recommended;
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawFooter(Rect row, DansToolboxPalette palette)
        {
            Rect laterRect = new Rect(row.x, row.y, 92f, row.height);
            Rect nextRect = new Rect(row.xMax - 118f, row.y, 118f, row.height);
            Rect backRect = new Rect(nextRect.x - 100f, row.y, 88f, row.height);

            if (DrawFlatButton(laterRect, "NOT NOW", palette, false, true))
            {
                DansToolboxSettings.DismissSetupPrompt();
                Close();
                return;
            }

            if (DrawFlatButton(
                    backRect,
                    "BACK",
                    palette,
                    false,
                    currentStep != DansToolboxSetupStep.Theme))
            {
                MoveBack();
            }

            string nextLabel = currentStep == DansToolboxSetupStep.Layout ? "APPLY" : "NEXT";
            if (DrawFlatButton(nextRect, nextLabel, palette, true, true))
            {
                if (currentStep == DansToolboxSetupStep.Layout)
                {
                    ApplySelection();
                }
                else
                {
                    MoveNext();
                }
            }
        }

        private void DrawScreenTitle(string title)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 30f, GUILayout.ExpandWidth(true));
            float reveal = EaseOutCubic(StepTransitionProgress);
            Rect clip = new Rect(rect.x, rect.y, rect.width * reveal, rect.height);
            GUI.BeginGroup(clip);
            GUI.Label(new Rect(0f, 0f, rect.width, rect.height), title, styles.ScreenTitle);
            GUI.EndGroup();
        }

        internal void MoveNext()
        {
            if (currentStep < DansToolboxSetupStep.Layout)
            {
                currentStep = (DansToolboxSetupStep)((int)currentStep + 1);
                scrollPosition = Vector2.zero;
                stepTransitionDirection = 1;
                stepTransitionStartedAt = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        internal void MoveBack()
        {
            if (currentStep > DansToolboxSetupStep.Theme)
            {
                currentStep = (DansToolboxSetupStep)((int)currentStep - 1);
                scrollPosition = Vector2.zero;
                stepTransitionDirection = -1;
                stepTransitionStartedAt = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void ApplySelection()
        {
            DansToolboxSettings.Apply(
                selectedTheme,
                enabledToolIds,
                useRecommendedLayout,
                seamlessToolSurfaces);
            DansToolboxInstallSuccessOverlay.ShowAfter(selectedTheme, 0.08d);
            Close();
        }

        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown)
            {
                return;
            }

            if (current.keyCode == KeyCode.Escape)
            {
                Close();
                current.Use();
                return;
            }

            if (showSplash)
            {
                showSplash = false;
                stepTransitionStartedAt = EditorApplication.timeSinceStartup;
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.LeftArrow)
            {
                MoveBack();
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.RightArrow)
            {
                MoveNext();
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
            {
                if (currentStep == DansToolboxSetupStep.Layout)
                {
                    ApplySelection();
                }
                else
                {
                    MoveNext();
                }

                current.Use();
            }
        }

        private void DrawSplash(DansToolboxPalette palette)
        {
            float progress = Mathf.Clamp01((float)(
                (EditorApplication.timeSinceStartup - splashStartedAt) / SplashDuration));
            float eased = EaseOutCubic(progress);
            float logoSize = Mathf.Lerp(44f, 56f, eased);
            Rect logo = new Rect(
                surfaceWidth * 0.5f - logoSize * 0.5f,
                surfaceHeight * 0.5f - 68f,
                logoSize,
                logoSize * 0.72f);

            DrawPanel(logo, palette.Raised, palette.Accent);
            EditorGUI.DrawRect(
                new Rect(logo.x + logo.width * 0.32f, logo.y - 7f, logo.width * 0.36f, 8f),
                palette.Accent);
            EditorGUI.DrawRect(
                new Rect(logo.x + 8f, logo.y + 13f, logo.width - 16f, 3f),
                palette.Signal);

            Rect textRect = new Rect(
                surfaceWidth * 0.5f - 150f,
                logo.yMax + 20f,
                300f,
                28f);
            Rect clip = new Rect(textRect.x, textRect.y, textRect.width * eased, textRect.height);
            GUI.BeginGroup(clip);
            GUI.Label(
                new Rect(0f, 0f, textRect.width, textRect.height),
                "DANS TOOLBOX",
                styles.Splash);
            GUI.EndGroup();

            Rect progressTrack = new Rect(
                surfaceWidth * 0.5f - 96f,
                textRect.yMax + 13f,
                192f,
                2f);
            EditorGUI.DrawRect(progressTrack, palette.Border);
            EditorGUI.DrawRect(
                new Rect(progressTrack.x, progressTrack.y, progressTrack.width * eased, 2f),
                palette.Accent);
        }

        internal static float CalculateInstallIconScale(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (progress <= 0f)
            {
                return 0f;
            }

            if (progress >= 1f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-7f * progress) * Mathf.Cos(12f * progress);
        }

        internal static float CalculateInstallOverlayOpacity(float progress)
        {
            float fade = Mathf.Clamp01(
                (Mathf.Clamp01(progress) - InstallFadeStart) /
                (1f - InstallFadeStart));
            return 1f - fade * fade * (3f - 2f * fade);
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private static void DrawToggleIndicator(Rect rect, bool enabled, DansToolboxPalette palette)
        {
            DrawPanel(
                rect,
                enabled ? palette.AccentSoft : palette.Inset,
                enabled ? palette.Accent : palette.Border);
            if (enabled)
            {
                DrawCheck(rect, palette.Text);
            }
        }

        private static void DrawChoiceMark(Rect rect, DansToolboxPalette palette)
        {
            DrawPanel(rect, palette.AccentSoft, palette.Accent);
            DrawCheck(rect, palette.Text);
        }

        private static void DrawCheck(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x + 4f, rect.center.y, 4f, 2f), color);
            EditorGUI.DrawRect(new Rect(rect.x + 7f, rect.center.y - 3f, 2f, 5f), color);
            EditorGUI.DrawRect(new Rect(rect.x + 9f, rect.center.y - 5f, 6f, 2f), color);
        }

        private static void DrawLayoutPreview(
            Rect rect,
            DansToolboxPalette palette,
            bool recommended)
        {
            DrawPanel(rect, palette.Inset, palette.Border);
            float gap = 5f;
            if (recommended)
            {
                Rect editor = new Rect(
                    rect.x + 8f,
                    rect.y + 8f,
                    rect.width - 16f,
                    rect.height - 16f);
                DrawPanel(editor, palette.Panel, palette.BorderStrong);
                Rect toolbar = new Rect(editor.x + 1f, editor.y + 1f, editor.width - 2f, 12f);
                EditorGUI.DrawRect(toolbar, palette.Inset);
                EditorGUI.DrawRect(new Rect(toolbar.x + 6f, toolbar.y + 4f, 30f, 4f), palette.Accent);
                Rect launcher = new Rect(
                    editor.x + 12f,
                    toolbar.yMax + 7f,
                    editor.width * 0.47f,
                    editor.height - toolbar.height - 20f);
                DrawPanel(launcher, palette.Raised, palette.AccentSoft);
                for (int index = 0; index < 3; index++)
                {
                    Rect card = new Rect(
                        launcher.x + 6f,
                        launcher.y + 7f + index * 14f,
                        launcher.width - 12f,
                        9f);
                    DrawPanel(card, palette.Panel, index == 0 ? palette.Accent : palette.Border);
                }
                Rect floatingTool = new Rect(
                    launcher.xMax + gap,
                    toolbar.yMax + 15f,
                    editor.xMax - launcher.xMax - gap - 8f,
                    editor.height - toolbar.height - 31f);
                DrawPanel(floatingTool, palette.Panel, palette.Accent);
                EditorGUI.DrawRect(
                    new Rect(
                        floatingTool.x + 5f,
                        floatingTool.y + 5f,
                        floatingTool.width - 10f,
                        3f),
                    palette.Signal);
            }
            else
            {
                Rect main = new Rect(
                    rect.x + 8f,
                    rect.y + 8f,
                    rect.width - 16f,
                    rect.height - 16f);
                DrawPanel(main, palette.Panel, palette.BorderStrong);
                EditorGUI.DrawRect(
                    new Rect(main.x + 6f, main.y + 6f, main.width - 12f, 3f),
                    palette.Muted);
            }
        }

        private static void DrawSeamlessSurfacePreview(
            Rect rect,
            DansToolboxPalette palette,
            bool seamless)
        {
            DrawPanel(rect, palette.Inset, palette.Border);
            Rect window = new Rect(rect.x + 7f, rect.y + 7f, rect.width - 14f, rect.height - 14f);
            Color surface = seamless ? palette.Panel : palette.Canvas;
            Color divider = seamless ? palette.Border : palette.BorderStrong;
            DrawPanel(window, surface, divider);

            Rect left = new Rect(window.x + 1f, window.y + 10f, window.width * 0.48f, window.height - 11f);
            Rect right = new Rect(left.xMax, left.y, window.xMax - left.xMax - 1f, left.height);
            EditorGUI.DrawRect(left, surface);
            EditorGUI.DrawRect(right, seamless ? surface : palette.Panel);
            EditorGUI.DrawRect(
                new Rect(left.xMax, left.y, 1f, left.height),
                divider);

            Rect leftTab = new Rect(window.x + 6f, window.y + 3f, 30f, 7f);
            Rect rightTab = new Rect(left.xMax + 6f, window.y + 3f, 30f, 7f);
            EditorGUI.DrawRect(leftTab, palette.Raised);
            EditorGUI.DrawRect(rightTab, palette.Raised);
            EditorGUI.DrawRect(
                new Rect(leftTab.x + 4f, leftTab.yMax - 2f, leftTab.width - 8f, 1f),
                palette.Accent);
            EditorGUI.DrawRect(
                new Rect(rightTab.x + 4f, rightTab.yMax - 2f, rightTab.width - 8f, 1f),
                palette.Muted);
        }

        private static bool DrawFlatButton(
            Rect rect,
            string label,
            DansToolboxPalette palette,
            bool primary,
            bool enabled)
        {
            bool hovered = enabled && rect.Contains(Event.current.mousePosition);
            Color fill = !enabled
                ? palette.Inset
                : primary
                    ? hovered ? palette.Accent : palette.AccentSoft
                    : hovered ? palette.Raised : palette.Inset;
            Color border = !enabled
                ? palette.Border
                : primary
                    ? hovered ? palette.Signal : palette.Accent
                    : hovered ? palette.BorderStrong : palette.Border;
            DrawPanel(rect, fill, border);

            Color textColor = !enabled
                ? palette.Muted
                : primary && hovered ? Color.black : palette.Text;
            GUI.Label(
                rect,
                label,
                MakeLabel(textColor, 10, FontStyle.Bold, TextAnchor.MiddleCenter));

            EditorGUI.BeginDisabledGroup(!enabled);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            EditorGUI.EndDisabledGroup();
            return clicked;
        }

        private static void DrawPanel(Rect rect, Color fill, Color border)
        {
            EditorGUI.DrawRect(rect, border);
            EditorGUI.DrawRect(
                new Rect(
                    rect.x + 1f,
                    rect.y + 1f,
                    Mathf.Max(0f, rect.width - 2f),
                    Mathf.Max(0f, rect.height - 2f)),
                fill);
        }

        private void EnsureStyles(DansToolboxPalette palette)
        {
            if (styles != null &&
                styledTheme == selectedTheme &&
                styledSeamlessToolSurfaces == seamlessToolSurfaces)
            {
                return;
            }

            styledTheme = selectedTheme;
            styledSeamlessToolSurfaces = seamlessToolSurfaces;
            styles = new WizardStyles
            {
                ScreenTitle = MakeLabel(palette.Text, 17, FontStyle.Bold),
                Splash = MakeLabel(palette.Text, 15, FontStyle.Bold, TextAnchor.MiddleCenter),
                StepActive = MakeLabel(palette.Text, 9, FontStyle.Bold, TextAnchor.MiddleCenter),
                StepComplete = MakeLabel(palette.Accent, 9, FontStyle.Bold, TextAnchor.MiddleCenter),
                StepInactive = MakeLabel(palette.Muted, 9, FontStyle.Bold, TextAnchor.MiddleCenter),
                CardTitle = MakeLabel(palette.Text, 11, FontStyle.Bold),
                CardBody = new GUIStyle(MakeLabel(palette.Muted, 10, FontStyle.Normal))
                {
                    wordWrap = true
                },
                Badge = MakeLabel(palette.Accent, 8, FontStyle.Bold, TextAnchor.MiddleRight),
                DangerBadge = MakeLabel(palette.Danger, 8, FontStyle.Bold, TextAnchor.MiddleRight)
            };
        }

        private static GUIStyle MakeLabel(
            Color color,
            int size,
            FontStyle fontStyle,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            return new GUIStyle(EditorStyles.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                alignment = alignment,
                normal = { textColor = color },
                clipping = TextClipping.Clip
            };
        }

        private sealed class WizardStyles
        {
            internal GUIStyle ScreenTitle;
            internal GUIStyle Splash;
            internal GUIStyle StepActive;
            internal GUIStyle StepComplete;
            internal GUIStyle StepInactive;
            internal GUIStyle CardTitle;
            internal GUIStyle CardBody;
            internal GUIStyle Badge;
            internal GUIStyle DangerBadge;
        }
    }

}
