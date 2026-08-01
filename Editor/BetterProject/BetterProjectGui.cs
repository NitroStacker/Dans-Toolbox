using System;
using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterProject
{
    internal enum BetterProjectToolbarGlyph
    {
        Back,
        Forward,
        Search,
        Close,
        List,
        Grid,
        Details,
        Split,
        Preview,
        More
    }

    internal static class BetterProjectGui
    {
        private static int revision = -1;
        private static GUIStyle toolbar;
        private static GUIStyle segment;
        private static GUIStyle segmentActive;
        private static GUIStyle search;
        private static GUIStyle row;
        private static GUIStyle rowSelected;
        private static GUIStyle muted;
        private static GUIStyle badge;
        private static GUIStyle cardTitle;
        private static GUIStyle tiny;

        internal static Color Canvas => DansToolboxTheme.Current.Canvas;
        internal static Color Panel => DansToolboxTheme.Current.Panel;
        internal static Color Inset => DansToolboxTheme.Current.Inset;
        internal static Color Raised => DansToolboxTheme.Current.Raised;
        internal static Color Hover => DansToolboxTheme.Current.Hover;
        internal static Color Border => DansToolboxTheme.Current.Border;
        internal static Color BorderStrong => DansToolboxTheme.Current.BorderStrong;
        internal static Color Text => DansToolboxTheme.Current.Text;
        internal static Color MutedColor => DansToolboxTheme.Current.Muted;
        internal static Color Accent => DansToolboxTheme.Current.Accent;
        internal static Color AccentSoft => DansToolboxTheme.Current.AccentSoft;
        internal static Color Selected => Color.Lerp(Panel, AccentSoft, 0.42f);
        internal static Color Danger => DansToolboxTheme.Current.Danger;
        internal static Color Warning => DansToolboxTheme.Current.Warning;
        internal static Color Success => DansToolboxTheme.Current.Success;

        internal static GUIStyle Toolbar { get { Ensure(); return toolbar; } }
        internal static GUIStyle Segment { get { Ensure(); return segment; } }
        internal static GUIStyle SegmentActive { get { Ensure(); return segmentActive; } }
        internal static GUIStyle Search { get { Ensure(); return search; } }
        internal static GUIStyle Row { get { Ensure(); return row; } }
        internal static GUIStyle RowSelected { get { Ensure(); return rowSelected; } }
        internal static GUIStyle Muted { get { Ensure(); return muted; } }
        internal static GUIStyle Badge { get { Ensure(); return badge; } }
        internal static GUIStyle CardTitle { get { Ensure(); return cardTitle; } }
        internal static GUIStyle Tiny { get { Ensure(); return tiny; } }

        internal static bool IconButton(Rect rect, GUIContent content, bool active = false)
        {
            Color background = active ? Accent : rect.Contains(Event.current.mousePosition) ? Hover : Color.clear;
            if (Event.current.type == EventType.Repaint && background.a > 0f)
            {
                EditorGUI.DrawRect(rect, background);
            }
            return GUI.Button(rect, content, GUIStyle.none);
        }

        internal static bool SegmentButton(Rect rect, string label, bool active, string tooltip)
        {
            return GUI.Toggle(rect, active, new GUIContent(label, tooltip), active ? SegmentActive : Segment);
        }

        internal static bool ToolbarTab(Rect rect, string label, bool active, string tooltip)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color border = active ? Accent : hovered ? BorderStrong : Border;
            Color fill = active ? AccentSoft : hovered ? Raised : Inset;
            DrawPanel(rect, fill, border);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                clipping = TextClipping.Clip,
                normal = { textColor = active ? Text : hovered ? Text : MutedColor }
            };
            GUI.Label(rect, new GUIContent(label, tooltip), labelStyle);
            return GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
        }

        internal static bool ToolbarIconButton(
            Rect rect,
            BetterProjectToolbarGlyph glyph,
            string tooltip,
            bool active = false,
            bool enabled = true)
        {
            bool hovered = enabled && rect.Contains(Event.current.mousePosition);
            Color border = active ? Accent : hovered ? BorderStrong : Border;
            Color fill = active ? AccentSoft : hovered ? Raised : Inset;
            DrawPanel(rect, fill, border);
            DrawToolbarGlyph(rect, glyph, enabled ? active || hovered ? Accent : Text : MutedColor);

            EditorGUI.BeginDisabledGroup(!enabled);
            bool clicked = GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
            EditorGUI.EndDisabledGroup();
            return clicked;
        }

        internal static void DrawToolbarGlyph(Rect rect, BetterProjectToolbarGlyph glyph, Color color)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            float cx = Mathf.Round(rect.center.x);
            float cy = Mathf.Round(rect.center.y);
            Handles.BeginGUI();
            Handles.color = color;
            switch (glyph)
            {
                case BetterProjectToolbarGlyph.Back:
                    Line(new Vector2(cx + 2f, cy - 5f), new Vector2(cx - 3f, cy), new Vector2(cx + 2f, cy + 5f));
                    break;
                case BetterProjectToolbarGlyph.Forward:
                    Line(new Vector2(cx - 2f, cy - 5f), new Vector2(cx + 3f, cy), new Vector2(cx - 2f, cy + 5f));
                    break;
                case BetterProjectToolbarGlyph.Search:
                    DrawSearchGlyph(cx, cy);
                    break;
                case BetterProjectToolbarGlyph.Close:
                    Line(new Vector2(cx - 4f, cy - 4f), new Vector2(cx + 4f, cy + 4f));
                    Line(new Vector2(cx + 4f, cy - 4f), new Vector2(cx - 4f, cy + 4f));
                    break;
                case BetterProjectToolbarGlyph.List:
                    DrawListGlyph(cx, cy);
                    break;
                case BetterProjectToolbarGlyph.Grid:
                    FillSquare(cx - 4f, cy - 4f, 3f, color);
                    FillSquare(cx + 2f, cy - 4f, 3f, color);
                    FillSquare(cx - 4f, cy + 2f, 3f, color);
                    FillSquare(cx + 2f, cy + 2f, 3f, color);
                    break;
                case BetterProjectToolbarGlyph.Details:
                    Outline(new Rect(cx - 6f, cy - 5f, 5f, 10f), color);
                    Line(new Vector2(cx + 1f, cy - 4f), new Vector2(cx + 6f, cy - 4f));
                    Line(new Vector2(cx + 1f, cy), new Vector2(cx + 6f, cy));
                    Line(new Vector2(cx + 1f, cy + 4f), new Vector2(cx + 6f, cy + 4f));
                    break;
                case BetterProjectToolbarGlyph.Split:
                    Outline(new Rect(cx - 6f, cy - 5f, 12f, 10f), color);
                    Line(new Vector2(cx, cy - 5f), new Vector2(cx, cy + 5f));
                    break;
                case BetterProjectToolbarGlyph.Preview:
                    Line(
                        new Vector2(cx - 7f, cy),
                        new Vector2(cx - 3f, cy - 4f),
                        new Vector2(cx, cy - 5f),
                        new Vector2(cx + 3f, cy - 4f),
                        new Vector2(cx + 7f, cy),
                        new Vector2(cx + 3f, cy + 4f),
                        new Vector2(cx, cy + 5f),
                        new Vector2(cx - 3f, cy + 4f),
                        new Vector2(cx - 7f, cy));
                    FillSquare(cx - 1f, cy - 1f, 3f, color);
                    break;
                case BetterProjectToolbarGlyph.More:
                    FillSquare(cx - 6f, cy - 1f, 2f, color);
                    FillSquare(cx - 1f, cy - 1f, 2f, color);
                    FillSquare(cx + 4f, cy - 1f, 2f, color);
                    break;
            }
            Handles.EndGUI();
        }

        internal static void DrawPanel(Rect rect, Color color, bool border = true)
        {
            EditorGUI.DrawRect(rect, color);
            if (!border)
            {
                return;
            }
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), Border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), Border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Border);
        }

        private static void DrawPanel(Rect rect, Color fill, Color border)
        {
            EditorGUI.DrawRect(rect, border);
            EditorGUI.DrawRect(
                new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f)),
                fill);
        }

        private static void DrawSearchGlyph(float cx, float cy)
        {
            const int pointCount = 10;
            Vector3[] circle = new Vector3[pointCount + 1];
            for (int index = 0; index <= pointCount; index++)
            {
                float angle = Mathf.PI * 2f * index / pointCount;
                circle[index] = new Vector3(
                    cx - 1f + Mathf.Cos(angle) * 4f,
                    cy - 1f + Mathf.Sin(angle) * 4f);
            }
            Handles.DrawAAPolyLine(1.35f, circle);
            Line(new Vector2(cx + 2f, cy + 2f), new Vector2(cx + 6f, cy + 6f));
        }

        private static void DrawListGlyph(float cx, float cy)
        {
            for (int rowIndex = -1; rowIndex <= 1; rowIndex++)
            {
                float y = cy + rowIndex * 5f;
                FillSquare(cx - 6f, y - 1f, 2f, Handles.color);
                Line(new Vector2(cx - 2f, y), new Vector2(cx + 6f, y));
            }
        }

        private static void Line(params Vector2[] points)
        {
            Vector3[] vectors = new Vector3[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                vectors[index] = points[index];
            }
            Handles.DrawAAPolyLine(1.35f, vectors);
        }

        private static void FillSquare(float x, float y, float size, Color color)
        {
            EditorGUI.DrawRect(new Rect(Mathf.Round(x), Mathf.Round(y), size, size), color);
        }

        private static void Outline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        internal static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L)
            {
                return (bytes / (1024d * 1024d * 1024d)).ToString("0.0") + " GB";
            }
            if (bytes >= 1024L * 1024L)
            {
                return (bytes / (1024d * 1024d)).ToString("0.0") + " MB";
            }
            if (bytes >= 1024L)
            {
                return (bytes / 1024d).ToString("0") + " KB";
            }
            return bytes + " B";
        }

        internal static string DiagnosticCode(BetterProjectDiagnosticFlags flags)
        {
            if (flags == BetterProjectDiagnosticFlags.None) return string.Empty;
            if ((flags & BetterProjectDiagnosticFlags.MissingScript) != 0) return "SCRIPT";
            if ((flags & BetterProjectDiagnosticFlags.MissingShader) != 0) return "SHADER";
            if ((flags & BetterProjectDiagnosticFlags.MissingAsset) != 0) return "MISSING";
            if ((flags & BetterProjectDiagnosticFlags.Oversized) != 0) return "SIZE";
            if ((flags & BetterProjectDiagnosticFlags.Unreferenced) != 0) return "UNUSED?";
            if ((flags & BetterProjectDiagnosticFlags.DuplicateName) != 0) return "DUP";
            if ((flags & BetterProjectDiagnosticFlags.EmptyFolder) != 0) return "EMPTY";
            return "CHECK";
        }

        internal static Texture Icon(string name)
        {
            return EditorGUIUtility.IconContent(name).image;
        }

        private static void Ensure()
        {
            if (revision == DansToolboxTheme.Revision && toolbar != null)
            {
                return;
            }
            revision = DansToolboxTheme.Revision;

            toolbar = FlatStyle(Panel, Text, 11, FontStyle.Normal, TextAnchor.MiddleLeft);
            toolbar.padding = new RectOffset(8, 8, 2, 2);

            segment = FlatStyle(Inset, MutedColor, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
            segmentActive = FlatStyle(Accent, Color.black, 10, FontStyle.Bold, TextAnchor.MiddleCenter);

            search = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(22, 20, 1, 1),
                normal = { textColor = Text },
                hover = { textColor = Text },
                active = { textColor = Text },
                focused = { textColor = Text }
            };

            row = FlatStyle(Color.clear, Text, 12, FontStyle.Normal, TextAnchor.MiddleLeft);
            row.padding = new RectOffset(6, 6, 0, 0);
            rowSelected = FlatStyle(Selected, Text, 12, FontStyle.Normal, TextAnchor.MiddleLeft);
            rowSelected.padding = row.padding;

            muted = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                normal = { textColor = MutedColor },
                alignment = TextAnchor.MiddleLeft
            };
            badge = FlatStyle(Raised, Text, 8, FontStyle.Bold, TextAnchor.MiddleCenter);
            badge.padding = new RectOffset(4, 4, 1, 1);
            cardTitle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = Text }
            };
            tiny = new GUIStyle(EditorStyles.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = MutedColor }
            };
        }

        private static GUIStyle FlatStyle(
            Color background,
            Color foreground,
            int size,
            FontStyle fontStyle,
            TextAnchor alignment)
        {
            Color hoverColor = background.a <= 0.01f
                ? Hover
                : Color.Lerp(background, Color.white, 0.08f);
            Color pressedColor = background.a <= 0.01f
                ? Raised
                : Color.Lerp(background, Color.black, 0.12f);
            Texture2D normalTexture = MakeTexture(background);
            Texture2D hoverTexture = MakeTexture(hoverColor);
            Texture2D pressedTexture = MakeTexture(pressedColor);
            GUIStyle style = new GUIStyle
            {
                font = EditorStyles.miniLabel.font,
                fontSize = size,
                fontStyle = fontStyle,
                alignment = alignment,
                border = new RectOffset(),
                margin = new RectOffset(),
                padding = new RectOffset(),
                clipping = TextClipping.Clip,
                normal = { background = normalTexture, textColor = foreground },
                hover = { background = hoverTexture, textColor = foreground },
                active = { background = pressedTexture, textColor = foreground },
                focused = { background = normalTexture, textColor = foreground }
            };
            style.onNormal.background = normalTexture;
            style.onNormal.textColor = foreground;
            style.onHover.background = hoverTexture;
            style.onHover.textColor = foreground;
            style.onActive.background = pressedTexture;
            style.onActive.textColor = foreground;
            style.onFocused.background = normalTexture;
            style.onFocused.textColor = foreground;
            return style;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
