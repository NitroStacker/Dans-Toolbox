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
        private const double SplashDuration = 0.82d;
        private const double StepTransitionDuration = 0.24d;
        private const double LayoutHandoffDuration = 0.72d;
        private static readonly string[] StepLabels = { "01  THEME", "02  TOOLS", "03  LAYOUT" };

        [SerializeField] private DansToolboxSetupStep currentStep;
        [SerializeField] private DansToolboxThemeId selectedTheme;
        [SerializeField] private List<string> enabledToolIds = new List<string>();
        [SerializeField] private bool useRecommendedLayout;
        [SerializeField] private bool stagedStateLoaded;
        [SerializeField] private Vector2 scrollPosition;
        [SerializeField] private bool showSplash;
        [SerializeField] private double splashStartedAt;
        [SerializeField] private double stepTransitionStartedAt;
        [SerializeField] private int stepTransitionDirection = 1;
        [SerializeField] private bool layoutHandoffActive;
        [SerializeField] private double layoutHandoffStartedAt;

        private WizardStyles styles;
        private DansToolboxThemeId styledTheme = (DansToolboxThemeId)(-1);

        internal DansToolboxSetupStep CurrentStep => currentStep;

        [MenuItem("Tools/Dans Toolbox/Setup Wizard", false, -100)]
        internal static void Open()
        {
            DansToolboxSetupWizard window = GetWindow<DansToolboxSetupWizard>(true);
            window.titleContent = new GUIContent("Dans Toolbox Setup");
            window.minSize = new Vector2(580f, 430f);
            window.maxSize = new Vector2(820f, 620f);
            window.ResetFromSettings();
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            if (!stagedStateLoaded)
            {
                ResetFromSettings();
            }
        }

        private void ResetFromSettings()
        {
            selectedTheme = DansToolboxSettings.Theme;
            useRecommendedLayout =
                DansToolboxSettings.RecommendedLayoutSelected &&
                DansToolboxLayoutInstaller.IsLayoutAvailable;
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
            layoutHandoffActive = false;
            Repaint();
        }

        private void OnGUI()
        {
            HandleKeyboard();

            DansToolboxPalette palette = DansToolboxTheme.GetPalette(selectedTheme);
            EnsureStyles(palette);
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), palette.Canvas);
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, 2f), palette.Accent);

            if (layoutHandoffActive)
            {
                DrawLayoutHandoff(palette);
                return;
            }

            if (showSplash)
            {
                DrawSplash(palette);
                return;
            }

            float innerWidth = position.width - Margin * 2f;
            float footerY = position.height - Margin - FooterHeight;
            Rect railRect = new Rect(Margin, Margin, innerWidth, StepRailHeight);
            Rect contentRect = new Rect(
                Margin,
                railRect.yMax + 18f,
                innerWidth,
                Mathf.Max(1f, footerY - railRect.yMax - 32f));
            Rect footerRect = new Rect(Margin, footerY, innerWidth, FooterHeight);

            DrawStepRail(railRect, palette);
            DrawSignalVfx(railRect, palette, StepTransitionProgress);
            DrawCurrentStep(contentRect, palette);
            DrawFooter(footerRect, palette);
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

            if (layoutHandoffActive)
            {
                repaint = true;
                if (now - layoutHandoffStartedAt >= LayoutHandoffDuration)
                {
                    layoutHandoffActive = false;
                    Close();
                    EditorApplication.delayCall += DansToolboxLayoutInstaller.ApplyRecommendedLayout;
                    return;
                }
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
            DrawScreenTitle("Choose a layout");
            GUILayout.Space(16f);

            Rect row = GUILayoutUtility.GetRect(1f, 174f, GUILayout.ExpandWidth(true));
            float cardWidth = (row.width - Gap) / 2f;
            Rect toolboxRect = new Rect(row.x, row.y, cardWidth, row.height);
            Rect currentRect = new Rect(row.x + cardWidth + Gap, row.y, cardWidth, row.height);
            DrawLayoutCard(toolboxRect, true, palette);
            DrawLayoutCard(currentRect, false, palette);
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
                recommended ? "TOOLBOX LAYOUT" : "KEEP CURRENT",
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
            bool applyLayout =
                useRecommendedLayout && DansToolboxLayoutInstaller.IsLayoutAvailable;
            DansToolboxSettings.Apply(selectedTheme, enabledToolIds, applyLayout);
            if (applyLayout)
            {
                layoutHandoffActive = true;
                layoutHandoffStartedAt = EditorApplication.timeSinceStartup;
                Repaint();
                return;
            }

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

            if (layoutHandoffActive)
            {
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
                position.width * 0.5f - logoSize * 0.5f,
                position.height * 0.5f - 68f,
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
                position.width * 0.5f - 150f,
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
                position.width * 0.5f - 96f,
                textRect.yMax + 13f,
                192f,
                2f);
            EditorGUI.DrawRect(progressTrack, palette.Border);
            EditorGUI.DrawRect(
                new Rect(progressTrack.x, progressTrack.y, progressTrack.width * eased, 2f),
                palette.Accent);
            DrawSignalVfx(progressTrack, palette, progress);
        }

        private void DrawLayoutHandoff(DansToolboxPalette palette)
        {
            float progress = Mathf.Clamp01((float)(
                (EditorApplication.timeSinceStartup - layoutHandoffStartedAt) /
                LayoutHandoffDuration));
            float eased = EaseInOutCubic(progress);
            Rect stage = new Rect(
                Margin + 30f,
                position.height * 0.5f - 92f,
                position.width - Margin * 2f - 60f,
                126f);

            float panelWidth = Mathf.Max(80f, (stage.width - Gap * 2f) / 3f);
            for (int index = 0; index < 3; index++)
            {
                float destination = stage.x + index * (panelWidth + Gap);
                float origin = position.width * 0.5f - panelWidth * 0.5f;
                float x = Mathf.Lerp(origin, destination, eased);
                float yOffset = (index - 1) * (1f - eased) * 24f;
                Rect panel = new Rect(x, stage.y + yOffset, panelWidth, 78f);
                DrawPanel(
                    panel,
                    index == 1 ? palette.Raised : palette.Panel,
                    index == 1 ? palette.Accent : palette.BorderStrong);
                EditorGUI.DrawRect(
                    new Rect(panel.x + 8f, panel.y + 8f, panel.width - 16f, 3f),
                    index == 1 ? palette.Signal : palette.AccentSoft);
            }

            Rect labelRect = new Rect(
                Margin,
                stage.yMax + 12f,
                position.width - Margin * 2f,
                28f);
            Rect clip = new Rect(
                labelRect.x,
                labelRect.y,
                labelRect.width * EaseOutCubic(progress),
                labelRect.height);
            GUI.BeginGroup(clip);
            GUI.Label(
                new Rect(0f, 0f, labelRect.width, labelRect.height),
                "ARRANGING WORKSPACE",
                styles.Splash);
            GUI.EndGroup();
            DrawSignalVfx(stage, palette, progress);
        }

        private static void DrawSignalVfx(
            Rect bounds,
            DansToolboxPalette palette,
            float progress)
        {
            float time = (float)EditorApplication.timeSinceStartup;
            for (int index = 0; index < 9; index++)
            {
                float phase = Mathf.Repeat(time * 0.9f + index * 0.137f, 1f);
                float x = Mathf.Lerp(bounds.x, bounds.xMax, phase);
                float wave = Mathf.Sin((phase + index * 0.21f) * Mathf.PI * 2f);
                float y = bounds.center.y + wave * Mathf.Min(12f, bounds.height * 0.22f);
                float alpha = Mathf.Sin(phase * Mathf.PI) * Mathf.Clamp01(progress * 2f);
                Color color = index % 3 == 0 ? palette.Signal : palette.Accent;
                color.a *= alpha * 0.72f;
                float size = index % 3 == 0 ? 3f : 2f;
                EditorGUI.DrawRect(new Rect(x, y, size, size), color);
            }
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseInOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return value < 0.5f
                ? 4f * value * value * value
                : 1f - Mathf.Pow(-2f * value + 2f, 3f) * 0.5f;
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
                Rect left = new Rect(
                    rect.x + 8f,
                    rect.y + 8f,
                    rect.width * 0.48f,
                    rect.height - 16f);
                Rect right = new Rect(
                    left.xMax + gap,
                    left.y,
                    rect.xMax - left.xMax - gap - 8f,
                    left.height);
                Rect leftTop = new Rect(left.x, left.y, left.width, left.height * 0.58f - 2f);
                Rect leftBottom = new Rect(
                    left.x,
                    leftTop.yMax + gap,
                    left.width,
                    left.height - leftTop.height - gap);
                DrawPanel(leftTop, palette.Raised, palette.Border);
                DrawPanel(leftBottom, palette.Panel, palette.AccentSoft);
                DrawPanel(right, palette.Panel, palette.Accent);
                EditorGUI.DrawRect(
                    new Rect(right.x + 5f, right.y + 5f, right.width - 10f, 3f),
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
            if (styles != null && styledTheme == selectedTheme)
            {
                return;
            }

            styledTheme = selectedTheme;
            styles = new WizardStyles
            {
                ScreenTitle = MakeLabel(palette.Text, 17, FontStyle.Bold),
                Splash = MakeLabel(palette.Text, 15, FontStyle.Bold, TextAnchor.MiddleCenter),
                StepActive = MakeLabel(palette.Text, 9, FontStyle.Bold, TextAnchor.MiddleCenter),
                StepComplete = MakeLabel(palette.Accent, 9, FontStyle.Bold, TextAnchor.MiddleCenter),
                StepInactive = MakeLabel(palette.Muted, 9, FontStyle.Bold, TextAnchor.MiddleCenter),
                CardTitle = MakeLabel(palette.Text, 11, FontStyle.Bold),
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
            internal GUIStyle Badge;
            internal GUIStyle DangerBadge;
        }
    }

}
