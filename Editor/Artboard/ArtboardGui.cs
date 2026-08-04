using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.Artboard
{
    internal static class ArtboardGui
    {
        private static GUIStyle label;
        private static GUIStyle muted;
        private static GUIStyle section;
        private static GUIStyle button;
        private static GUIStyle centered;
        private static int revision = -1;

        internal static GUIStyle Label { get { Ensure(); return label; } }
        internal static GUIStyle Muted { get { Ensure(); return muted; } }
        internal static GUIStyle Section { get { Ensure(); return section; } }
        internal static GUIStyle Centered { get { Ensure(); return centered; } }

        internal static void Panel(Rect rect, DansToolboxPalette palette, bool raised = false)
        {
            EditorGUI.DrawRect(rect, raised ? palette.Raised : palette.Panel);
            Border(rect, palette.Border);
        }

        internal static void Border(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        internal static bool Button(Rect rect, string text, string tooltip, bool active = false, bool primary = false)
        {
            Ensure();
            DansToolboxPalette palette = DansToolboxTheme.Current;
            bool hover = rect.Contains(Event.current.mousePosition);
            Color fill = active ? palette.AccentSoft : primary ? palette.Accent : hover ? palette.Hover : palette.Raised;
            Color outline = active || primary ? palette.AccentHover : palette.Border;
            EditorGUI.DrawRect(rect, fill);
            Border(rect, outline);
            GUIStyle style = new GUIStyle(button)
            {
                normal = { textColor = primary ? Color.white : active ? palette.Text : palette.Text }
            };
            return GUI.Button(rect, new GUIContent(text, tooltip), style);
        }

        internal static void Checker(Rect rect, float size, Color a, Color b)
        {
            size = Mathf.Max(2f, size);
            EditorGUI.DrawRect(rect, a);
            int columns = Mathf.CeilToInt(rect.width / size);
            int rows = Mathf.CeilToInt(rect.height / size);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    if (((x + y) & 1) != 0)
                        EditorGUI.DrawRect(new Rect(rect.x + x * size, rect.y + y * size,
                            Mathf.Min(size, rect.xMax - (rect.x + x * size)),
                            Mathf.Min(size, rect.yMax - (rect.y + y * size))), b);
        }

        internal static void SectionLabel(Rect rect, string text)
        {
            GUI.Label(rect, text.ToUpperInvariant(), Section);
        }

        private static void Ensure()
        {
            if (revision == DansToolboxTheme.Revision && label != null) return;
            revision = DansToolboxTheme.Revision;
            DansToolboxPalette palette = DansToolboxTheme.Current;
            label = new GUIStyle(EditorStyles.label) { fontSize = 11, normal = { textColor = palette.Text } };
            muted = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10, normal = { textColor = palette.Muted }, wordWrap = true };
            section = new GUIStyle(EditorStyles.miniBoldLabel) { fontSize = 9, normal = { textColor = palette.Muted } };
            button = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter, fontSize = 10, fontStyle = FontStyle.Bold };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }
    }
}
