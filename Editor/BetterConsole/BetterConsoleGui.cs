using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterConsole
{
    internal static class BetterConsoleGui
    {
        private static int revision = -1;
        private static GUIStyle label;
        private static GUIStyle muted;
        private static GUIStyle tiny;
        private static GUIStyle title;
        private static GUIStyle mono;
        private static GUIStyle wrapped;
        private static GUIStyle field;
        private static GUIStyle button;

        public static GUIStyle Label { get { Ensure(); return label; } }
        public static GUIStyle Muted { get { Ensure(); return muted; } }
        public static GUIStyle Tiny { get { Ensure(); return tiny; } }
        public static GUIStyle Title { get { Ensure(); return title; } }
        public static GUIStyle Mono { get { Ensure(); return mono; } }
        public static GUIStyle Wrapped { get { Ensure(); return wrapped; } }
        public static GUIStyle Field { get { Ensure(); return field; } }

        public static void Panel(Rect rect, bool inset = false, bool strong = false)
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            EditorGUI.DrawRect(rect, strong ? palette.Raised : inset ? palette.Inset : palette.Panel);
            Border(rect, strong ? palette.BorderStrong : palette.Border);
        }

        public static void Border(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        public static bool Button(Rect rect, GUIContent content, bool selected = false, Color? signal = null)
        {
            Ensure();
            DansToolboxPalette palette = DansToolboxTheme.Current;
            bool hover = rect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(rect, selected ? palette.AccentSoft : hover ? palette.Hover : palette.Raised);
            Border(rect, selected ? (signal ?? palette.Accent) : palette.Border);
            Color previous = GUI.color;
            GUI.color = selected ? palette.Text : (signal ?? palette.Text);
            bool clicked = GUI.Button(rect, content, button);
            GUI.color = previous;
            return clicked;
        }

        public static string SearchField(Rect rect, string value, string controlName)
        {
            Ensure();
            DansToolboxPalette palette = DansToolboxTheme.Current;
            bool focused = GUI.GetNameOfFocusedControl() == controlName;
            bool hovered = rect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(rect, focused || hovered ? palette.Raised : palette.Inset);
            Border(rect, focused ? palette.Accent : hovered ? palette.BorderStrong : palette.Border);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Text);
            GUI.SetNextControlName(controlName);
            string result = GUI.TextField(new Rect(rect.x + 7f, rect.y + 1f, rect.width - 25f, rect.height - 2f), value ?? string.Empty, field);
            if (string.IsNullOrEmpty(result) && !focused && Event.current.type == EventType.Repaint)
            {
                GUI.Label(new Rect(rect.x + 8f, rect.y + 1f, rect.width - 28f, rect.height - 2f), "SEARCH  /  sev:error", muted);
            }
            return result;
        }

        public static void SeverityMark(Rect rect, BetterConsoleSeverity severity)
        {
            Color color = SeverityColor(severity);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), color);
        }

        public static Color SeverityColor(BetterConsoleSeverity severity)
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            switch (severity)
            {
                case BetterConsoleSeverity.Warning: return palette.Warning;
                case BetterConsoleSeverity.Error:
                case BetterConsoleSeverity.Exception:
                case BetterConsoleSeverity.Assert: return palette.Danger;
                default: return palette.Signal;
            }
        }

        public static void Divider(Rect rect)
        {
            EditorGUI.DrawRect(rect, DansToolboxTheme.Current.Border);
        }

        private static void Ensure()
        {
            if (revision == DansToolboxTheme.Revision && label != null) return;
            revision = DansToolboxTheme.Revision;
            DansToolboxPalette palette = DansToolboxTheme.Current;
            label = NewStyle(EditorStyles.label, 11, FontStyle.Normal, palette.Text);
            muted = NewStyle(EditorStyles.label, 10, FontStyle.Normal, palette.Muted);
            tiny = NewStyle(EditorStyles.label, 9, FontStyle.Bold, palette.Muted);
            tiny.alignment = TextAnchor.MiddleLeft;
            title = NewStyle(EditorStyles.label, 12, FontStyle.Bold, palette.Text);
            mono = NewStyle(EditorStyles.label, 10, FontStyle.Normal, palette.Text);
            mono.font = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font ?? EditorStyles.label.font;
            mono.wordWrap = false;
            wrapped = NewStyle(EditorStyles.label, 11, FontStyle.Normal, palette.Text);
            wrapped.wordWrap = true;
            field = NewStyle(EditorStyles.textField, 11, FontStyle.Normal, palette.Text);
            field.normal.background = null;
            field.focused.background = null;
            field.hover.background = null;
            field.border = new RectOffset();
            field.padding = new RectOffset(0, 0, 3, 2);
            button = NewStyle(EditorStyles.label, 10, FontStyle.Bold, palette.Text);
            button.alignment = TextAnchor.MiddleCenter;
            button.clipping = TextClipping.Clip;
        }

        private static GUIStyle NewStyle(GUIStyle source, int size, FontStyle fontStyle, Color color)
        {
            GUIStyle style = new GUIStyle(source)
            {
                fontSize = size,
                fontStyle = fontStyle,
                normal = { textColor = color },
                hover = { textColor = color },
                active = { textColor = color },
                focused = { textColor = color }
            };
            return style;
        }
    }
}
