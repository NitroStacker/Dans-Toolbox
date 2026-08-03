using System;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    /// <summary>
    /// Shared Toolbox search control. Its visual and interaction contract follows
    /// Better Project: inset shell, accent focus, search glyph, clear action, and
    /// explicit click-away focus release.
    /// </summary>
    public static class DansToolboxSearchField
    {
        public const float Height = 22f;

        private static int styledThemeRevision = -1;
        private static GUIStyle fieldStyle;
        private static GUIStyle hintStyle;

        public static string Draw(
            Rect rect,
            string value,
            string controlName,
            string placeholder = null,
            string clearTooltip = "Clear search")
        {
            EnsureStyles();
            value ??= string.Empty;
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
            bool hovered = rect.Contains(Event.current.mousePosition);
            bool showClear = !string.IsNullOrEmpty(value);
            CalculateControlRects(rect, showClear, out Rect textRect, out Rect clearRect);

            DansToolboxPalette palette = DansToolboxTheme.Current;
            EditorGUI.DrawRect(rect, focused ? palette.Accent : hovered ? palette.BorderStrong : palette.Border);
            EditorGUI.DrawRect(
                new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f)),
                focused || hovered ? palette.Raised : palette.Inset);

            EditorGUIUtility.AddCursorRect(textRect, MouseCursor.Text);
            GUI.SetNextControlName(controlName);
            string result = GUI.TextField(textRect, value, fieldStyle);
            DrawSearchGlyph(
                new Rect(rect.x + 4f, rect.y + (rect.height - 16f) * 0.5f, 16f, 16f),
                focused || hovered ? palette.Accent : palette.Muted);

            if (string.IsNullOrEmpty(result) && !focused && !string.IsNullOrEmpty(placeholder))
            {
                GUI.Label(textRect, placeholder, hintStyle);
            }

            if (showClear && DrawClearButton(clearRect, clearTooltip, palette))
            {
                result = string.Empty;
                GUI.FocusControl(controlName);
            }

            return result;
        }

        public static bool ReleaseFocusOnPointerDown(Rect searchRect, string controlName)
        {
            Event current = Event.current;
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(), controlName, StringComparison.Ordinal);
            if (!ShouldReleaseFocus(searchRect, current.mousePosition, focused, current.type)) return false;

            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            return true;
        }

        public static bool ShouldReleaseFocus(
            Rect searchRect,
            Vector2 pointerPosition,
            bool searchFocused,
            EventType eventType)
        {
            return searchFocused && eventType == EventType.MouseDown && !searchRect.Contains(pointerPosition);
        }

        public static void CalculateControlRects(
            Rect searchRect,
            bool showClear,
            out Rect fieldRect,
            out Rect clearRect)
        {
            clearRect = new Rect(searchRect.xMax - 21f, searchRect.y + 2f, 18f, Mathf.Max(1f, searchRect.height - 4f));
            float fieldRight = showClear ? clearRect.x - 2f : searchRect.xMax - 1f;
            fieldRect = new Rect(
                searchRect.x + 1f,
                searchRect.y + 1f,
                Mathf.Max(1f, fieldRight - searchRect.x - 1f),
                Mathf.Max(1f, searchRect.height - 2f));
        }

        private static bool DrawClearButton(Rect rect, string tooltip, DansToolboxPalette palette)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(rect, hovered ? palette.BorderStrong : palette.Border);
            EditorGUI.DrawRect(
                new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f)),
                hovered ? palette.Raised : palette.Inset);
            DrawCloseGlyph(rect, hovered ? palette.Accent : palette.Muted);
            return GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
        }

        private static void DrawSearchGlyph(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            float cx = Mathf.Round(rect.center.x);
            float cy = Mathf.Round(rect.center.y);
            const int pointCount = 10;
            Vector3[] circle = new Vector3[pointCount + 1];
            for (int index = 0; index <= pointCount; index++)
            {
                float angle = Mathf.PI * 2f * index / pointCount;
                circle[index] = new Vector3(
                    cx - 1f + Mathf.Cos(angle) * 4f,
                    cy - 1f + Mathf.Sin(angle) * 4f);
            }

            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(1.35f, circle);
            Handles.DrawAAPolyLine(
                1.35f,
                new Vector3(cx + 2f, cy + 2f),
                new Vector3(cx + 6f, cy + 6f));
            Handles.EndGUI();
        }

        private static void DrawCloseGlyph(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            float cx = Mathf.Round(rect.center.x);
            float cy = Mathf.Round(rect.center.y);
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(1.35f, new Vector3(cx - 4f, cy - 4f), new Vector3(cx + 4f, cy + 4f));
            Handles.DrawAAPolyLine(1.35f, new Vector3(cx + 4f, cy - 4f), new Vector3(cx - 4f, cy + 4f));
            Handles.EndGUI();
        }

        private static void EnsureStyles()
        {
            if (styledThemeRevision == DansToolboxTheme.Revision && fieldStyle != null) return;
            styledThemeRevision = DansToolboxTheme.Revision;
            DansToolboxPalette palette = DansToolboxTheme.Current;
            fieldStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(22, 20, 1, 1),
                normal = { textColor = palette.Text },
                hover = { textColor = palette.Text },
                active = { textColor = palette.Text },
                focused = { textColor = palette.Text }
            };
            hintStyle = new GUIStyle(fieldStyle)
            {
                normal = { textColor = new Color(palette.Muted.r, palette.Muted.g, palette.Muted.b, 0.72f) }
            };
        }
    }
}
