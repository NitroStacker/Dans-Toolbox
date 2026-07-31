using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
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
        private const float Margin = 14f;
        private const float Gap = 9f;

        private DansToolboxThemeId selectedTheme;
        private readonly HashSet<string> enabledToolIds = new HashSet<string>();
        private bool useRecommendedLayout;
        private Vector2 scrollPosition;
        private WizardStyles styles;
        private DansToolboxThemeId styledTheme = (DansToolboxThemeId)(-1);

        [MenuItem("Tools/Dans Toolbox/Setup Wizard", false, -100)]
        internal static void Open()
        {
            DansToolboxSetupWizard window = GetWindow<DansToolboxSetupWizard>(true);
            window.titleContent = new GUIContent("Dans Toolbox Setup");
            window.minSize = new Vector2(650f, 610f);
            window.maxSize = new Vector2(940f, 900f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            selectedTheme = DansToolboxSettings.Theme;
            useRecommendedLayout = DansToolboxSettings.RecommendedLayoutSelected;
            enabledToolIds.Clear();
            foreach (DansToolboxToolDescriptor tool in DansToolboxTools.All)
            {
                if (DansToolboxSettings.IsToolEnabled(tool.Id))
                {
                    enabledToolIds.Add(tool.Id);
                }
            }
        }

        private void OnGUI()
        {
            DansToolboxPalette palette = DansToolboxTheme.GetPalette(selectedTheme);
            EnsureStyles(palette);
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), palette.Canvas);

            Rect signalRail = new Rect(0f, 0f, position.width, 3f);
            EditorGUI.DrawRect(signalRail, palette.Accent);

            GUILayout.BeginArea(new Rect(
                Margin,
                Margin,
                position.width - Margin * 2f,
                position.height - Margin * 2f));
            DrawHeader(palette);

            scrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none);
            DrawThemeSection(palette);
            GUILayout.Space(Gap);
            DrawToolsSection(palette);
            GUILayout.Space(Gap);
            DrawLayoutSection(palette);
            EditorGUILayout.EndScrollView();

            GUILayout.Space(10f);
            DrawFooter(palette);
            GUILayout.EndArea();
        }

        private void DrawHeader(DansToolboxPalette palette)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 58f, GUILayout.ExpandWidth(true));
            DrawPanel(rect, palette.Raised, palette.BorderStrong);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), palette.Accent);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 9f, rect.width - 150f, 22f),
                "SET UP DANS TOOLBOX", styles.Header);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 31f, rect.width - 24f, 17f),
                "Choose the tools, color system, and workspace this project should use.",
                styles.Body);
            GUI.Label(new Rect(rect.xMax - 104f, rect.y + 10f, 88f, 18f),
                "FIRST RUN", styles.Badge);
        }

        private void DrawThemeSection(DansToolboxPalette palette)
        {
            DrawSectionLabel("01  COLOR THEME", "Changes both tools immediately after setup.");
            Rect row = GUILayoutUtility.GetRect(1f, 112f, GUILayout.ExpandWidth(true));
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
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                selectedTheme = theme;
                styledTheme = (DansToolboxThemeId)(-1);
                Repaint();
            }

            DrawPanel(
                rect,
                hovered || selected ? cardPalette.Raised : cardPalette.Panel,
                selected ? cardPalette.Accent : hovered ? cardPalette.BorderStrong : cardPalette.Border);
            EditorGUI.DrawRect(new Rect(rect.x + 10f, rect.y + 12f, rect.width - 20f, 10f),
                cardPalette.Accent);
            EditorGUI.DrawRect(new Rect(rect.x + 10f, rect.y + 27f, rect.width - 20f, 8f),
                cardPalette.Signal);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 48f, rect.width - 20f, 18f),
                DansToolboxTheme.GetDisplayName(theme),
                MakeLabel(cardPalette.Text, 11, FontStyle.Bold));
            GUI.Label(new Rect(rect.x + 10f, rect.y + 69f, rect.width - 20f, 30f),
                theme == DansToolboxThemeId.SignalOrange
                    ? "Warm studio signal"
                    : theme == DansToolboxThemeId.NeonCyan
                        ? "Cool technical glow"
                        : "Arcade ultraviolet",
                MakeLabel(cardPalette.Muted, 9, FontStyle.Normal));
            if (selected)
            {
                GUI.Label(new Rect(rect.xMax - 72f, rect.yMax - 22f, 62f, 16f),
                    "SELECTED", MakeLabel(cardPalette.Accent, 8, FontStyle.Bold, TextAnchor.MiddleRight));
            }
        }

        private void DrawToolsSection(DansToolboxPalette palette)
        {
            DrawSectionLabel("02  ENABLED TOOLS", "Disabled tools stay installed but cannot be opened.");
            foreach (DansToolboxToolDescriptor tool in DansToolboxTools.All)
            {
                Rect rect = GUILayoutUtility.GetRect(1f, 68f, GUILayout.ExpandWidth(true));
                bool enabled = enabledToolIds.Contains(tool.Id);
                bool hovered = rect.Contains(Event.current.mousePosition);
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

                DrawPanel(rect,
                    hovered ? palette.Raised : palette.Panel,
                    enabled ? palette.AccentSoft : hovered ? palette.BorderStrong : palette.Border);
                DrawToggleIndicator(new Rect(rect.x + 13f, rect.y + 20f, 28f, 28f), enabled, palette);
                GUI.Label(new Rect(rect.x + 54f, rect.y + 11f, rect.width - 150f, 20f),
                    tool.Name.ToUpperInvariant(), styles.CardTitle);
                GUI.Label(new Rect(rect.x + 54f, rect.y + 33f, rect.width - 70f, 24f),
                    tool.Description, styles.Body);
                if (tool.WindowsOnly)
                {
                    GUI.Label(new Rect(rect.xMax - 90f, rect.y + 11f, 76f, 18f),
                        "WINDOWS", styles.Badge);
                }

                GUILayout.Space(5f);
            }
        }

        private void DrawLayoutSection(DansToolboxPalette palette)
        {
            DrawSectionLabel("03  WORKSPACE", "Optional. Replaces the current Unity window arrangement.");
            Rect rect = GUILayoutUtility.GetRect(1f, 112f, GUILayout.ExpandWidth(true));
            bool available = DansToolboxLayoutInstaller.IsLayoutAvailable;
            bool hovered = rect.Contains(Event.current.mousePosition);
            EditorGUI.BeginDisabledGroup(!available);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                useRecommendedLayout = !useRecommendedLayout;
            }
            EditorGUI.EndDisabledGroup();

            DrawPanel(rect,
                hovered && available ? palette.Raised : palette.Panel,
                useRecommendedLayout ? palette.Accent : available ? palette.Border : palette.Danger);
            DrawToggleIndicator(new Rect(rect.x + 13f, rect.y + 18f, 28f, 28f),
                useRecommendedLayout && available, palette);
            GUI.Label(new Rect(rect.x + 54f, rect.y + 12f, rect.width - 290f, 20f),
                "TOOLBOX LAYOUT", styles.CardTitle);
            GUI.Label(new Rect(rect.x + 54f, rect.y + 35f, rect.width - 290f, 52f),
                available
                    ? "Places the enabled tools into the saved production workspace. You can switch layouts again from Unity at any time."
                    : "The packaged ToolBox Layout file is missing.",
                styles.Body);
            DrawLayoutPreview(new Rect(rect.xMax - 220f, rect.y + 15f, 202f, 82f), palette);
        }

        private void DrawFooter(DansToolboxPalette palette)
        {
            Rect row = GUILayoutUtility.GetRect(1f, 36f, GUILayout.ExpandWidth(true));
            Rect laterRect = new Rect(row.x, row.y, 105f, row.height);
            Rect applyRect = new Rect(row.xMax - 176f, row.y, 176f, row.height);
            if (DrawFlatButton(laterRect, "NOT NOW", palette, false))
            {
                DansToolboxSettings.DismissSetupPrompt();
                Close();
            }

            GUI.Label(new Rect(laterRect.xMax + 12f, row.y, row.width - 310f, row.height),
                "Reopen this wizard from Tools > Dans Toolbox > Setup Wizard.", styles.Small);
            if (DrawFlatButton(applyRect, "APPLY SETUP", palette, true))
            {
                DansToolboxSettings.Apply(selectedTheme, enabledToolIds, useRecommendedLayout);
                bool applyLayout = useRecommendedLayout && DansToolboxLayoutInstaller.IsLayoutAvailable;
                Close();
                if (applyLayout)
                {
                    DansToolboxLayoutInstaller.ApplyRecommendedLayout();
                }
            }
        }

        private void DrawSectionLabel(string title, string detail)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 35f, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, 16f), title, styles.Section);
            GUI.Label(new Rect(rect.x, rect.y + 18f, rect.width, 15f), detail, styles.Small);
        }

        private static void DrawToggleIndicator(Rect rect, bool enabled, DansToolboxPalette palette)
        {
            DrawPanel(rect, enabled ? palette.AccentSoft : palette.Inset,
                enabled ? palette.Accent : palette.Border);
            if (!enabled)
            {
                return;
            }

            EditorGUI.DrawRect(new Rect(rect.x + 7f, rect.center.y, 5f, 2f), palette.Text);
            EditorGUI.DrawRect(new Rect(rect.x + 11f, rect.center.y - 4f, 2f, 6f), palette.Text);
            EditorGUI.DrawRect(new Rect(rect.x + 13f, rect.center.y - 6f, 9f, 2f), palette.Text);
        }

        private static void DrawLayoutPreview(Rect rect, DansToolboxPalette palette)
        {
            DrawPanel(rect, palette.Inset, palette.Border);
            Rect left = new Rect(rect.x + 8f, rect.y + 8f, rect.width * 0.48f, rect.height - 16f);
            Rect right = new Rect(left.xMax + 6f, left.y, rect.xMax - left.xMax - 14f, left.height);
            Rect leftTop = new Rect(left.x, left.y, left.width, left.height * 0.58f - 3f);
            Rect leftBottom = new Rect(left.x, leftTop.yMax + 6f, left.width, left.height - leftTop.height - 6f);
            DrawPanel(leftTop, palette.Raised, palette.Border);
            DrawPanel(leftBottom, palette.Panel, palette.AccentSoft);
            DrawPanel(right, palette.Panel, palette.Accent);
            EditorGUI.DrawRect(new Rect(right.x + 5f, right.y + 5f, right.width - 10f, 3f), palette.Signal);
        }

        private static bool DrawFlatButton(
            Rect rect,
            string label,
            DansToolboxPalette palette,
            bool primary)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            Color fill = primary
                ? hovered ? palette.Accent : palette.AccentSoft
                : hovered ? palette.Raised : palette.Inset;
            Color border = primary
                ? hovered ? palette.Signal : palette.Accent
                : hovered ? palette.BorderStrong : palette.Border;
            DrawPanel(rect, fill, border);
            GUI.Label(rect, label,
                MakeLabel(primary && hovered ? Color.black : palette.Text, 10,
                    FontStyle.Bold, TextAnchor.MiddleCenter));
            return clicked;
        }

        private static void DrawPanel(Rect rect, Color fill, Color border)
        {
            EditorGUI.DrawRect(rect, border);
            EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f,
                Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f)), fill);
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
                Header = MakeLabel(palette.Text, 16, FontStyle.Bold),
                Section = MakeLabel(palette.Muted, 10, FontStyle.Bold),
                CardTitle = MakeLabel(palette.Text, 11, FontStyle.Bold),
                Body = MakeLabel(palette.Muted, 10, FontStyle.Normal),
                Small = MakeLabel(palette.Muted, 9, FontStyle.Normal, TextAnchor.MiddleLeft),
                Badge = MakeLabel(palette.Accent, 9, FontStyle.Bold, TextAnchor.MiddleRight)
            };
            styles.Body.wordWrap = true;
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
            internal GUIStyle Header;
            internal GUIStyle Section;
            internal GUIStyle CardTitle;
            internal GUIStyle Body;
            internal GUIStyle Small;
            internal GUIStyle Badge;
        }
    }
}
