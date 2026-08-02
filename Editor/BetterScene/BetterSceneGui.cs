using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterScene
{
    internal static class BetterSceneGui
    {
        private static int revision = -1;
        private static GUIStyle label;
        private static GUIStyle muted;
        private static GUIStyle tiny;
        private static GUIStyle title;
        private static GUIStyle button;
        private static GUIStyle field;
        private static GUIStyle centered;

        internal static GUIStyle Label { get { Ensure(); return label; } }
        internal static GUIStyle Muted { get { Ensure(); return muted; } }
        internal static GUIStyle Tiny { get { Ensure(); return tiny; } }
        internal static GUIStyle Title { get { Ensure(); return title; } }
        internal static GUIStyle Field { get { Ensure(); return field; } }
        internal static GUIStyle Centered { get { Ensure(); return centered; } }

        internal static void Panel(Rect rect, bool inset = false, bool strong = false)
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            EditorGUI.DrawRect(rect, strong ? palette.Raised : inset ? palette.Inset : palette.Panel);
            Border(rect, strong ? palette.BorderStrong : palette.Border);
        }

        internal static void Border(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        internal static bool Button(Rect rect, GUIContent content, bool selected = false, bool enabled = true, Color? signal = null)
        {
            Ensure();
            DansToolboxPalette palette = DansToolboxTheme.Current;
            bool hover = enabled && rect.Contains(Event.current.mousePosition);
            Color fill = !enabled ? palette.Inset : selected ? palette.AccentSoft : hover ? palette.Hover : palette.Raised;
            Color edge = !enabled ? palette.Border : selected ? (signal ?? palette.Accent) : hover ? palette.BorderStrong : palette.Border;
            EditorGUI.DrawRect(rect, fill);
            Border(rect, edge);
            Color previous = GUI.color;
            GUI.color = !enabled ? palette.Muted : signal ?? (selected ? palette.Text : palette.Text);
            EditorGUI.BeginDisabledGroup(!enabled);
            bool clicked = GUI.Button(rect, content, button);
            EditorGUI.EndDisabledGroup();
            GUI.color = previous;
            if (enabled) EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            return clicked;
        }

        internal static void SectionHeader(Rect rect, string labelText, string badge = null, Color? badgeColor = null)
        {
            Ensure();
            DansToolboxPalette palette = DansToolboxTheme.Current;
            GUI.Label(rect, labelText, tiny);
            if (!string.IsNullOrEmpty(badge))
            {
                GUIStyle right = new GUIStyle(tiny)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = badgeColor ?? palette.Accent }
                };
                GUI.Label(rect, badge, right);
            }
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), palette.Border);
        }

        internal static void Crosshair(Vector2 center, float radius, Color color)
        {
            EditorGUI.DrawRect(new Rect(center.x - radius, center.y - 1f, radius * 2f, 2f), color);
            EditorGUI.DrawRect(new Rect(center.x - 1f, center.y - radius, 2f, radius * 2f), color);
            EditorGUI.DrawRect(new Rect(center.x - 2f, center.y - 2f, 4f, 4f), color);
        }

        private static void Ensure()
        {
            if (revision == DansToolboxTheme.Revision && label != null) return;
            revision = DansToolboxTheme.Revision;
            DansToolboxPalette palette = DansToolboxTheme.Current;
            label = Make(EditorStyles.label, 11, FontStyle.Normal, palette.Text);
            muted = Make(EditorStyles.label, 10, FontStyle.Normal, palette.Muted);
            tiny = Make(EditorStyles.label, 9, FontStyle.Bold, palette.Muted);
            title = Make(EditorStyles.label, 13, FontStyle.Bold, palette.Text);
            centered = Make(EditorStyles.label, 10, FontStyle.Bold, palette.Text);
            centered.alignment = TextAnchor.MiddleCenter;
            button = Make(EditorStyles.label, 9, FontStyle.Bold, palette.Text);
            button.alignment = TextAnchor.MiddleCenter;
            button.clipping = TextClipping.Clip;
            field = Make(EditorStyles.textField, 10, FontStyle.Normal, palette.Text);
        }

        private static GUIStyle Make(GUIStyle source, int size, FontStyle fontStyle, Color color)
        {
            return new GUIStyle(source)
            {
                fontSize = size,
                fontStyle = fontStyle,
                normal = { textColor = color },
                hover = { textColor = color },
                active = { textColor = color },
                focused = { textColor = color },
                clipping = TextClipping.Clip
            };
        }
    }
}
