using UnityEditor;
using UnityEngine;
using DansToolbox.Editor;

namespace DansToolbox.EditorTools.NativeWindowDock
{
    internal static class NativeWindowDockGui
    {
        internal static Color Canvas => DansToolboxTheme.Current.Canvas;
        internal static Color Raised => DansToolboxTheme.Current.Panel;
        internal static Color Header => DansToolboxTheme.Current.Raised;
        internal static Color Inset => DansToolboxTheme.Current.Inset;
        internal static Color Hover => DansToolboxTheme.Current.Hover;
        internal static Color Border => DansToolboxTheme.Current.Border;
        internal static Color BorderStrong => DansToolboxTheme.Current.BorderStrong;
        internal static Color Text => DansToolboxTheme.Current.Text;
        internal static Color Muted => DansToolboxTheme.Current.Muted;
        internal static Color Accent => DansToolboxTheme.Current.Accent;
        internal static Color AccentHover => DansToolboxTheme.Current.AccentHover;
        internal static Color AccentSoft => DansToolboxTheme.Current.AccentSoft;
        internal static Color Warning => DansToolboxTheme.Current.Warning;
        internal static Color Danger => DansToolboxTheme.Current.Danger;

        private static GUIStyle title;
        private static GUIStyle subtitle;
        private static GUIStyle body;
        private static GUIStyle muted;
        private static GUIStyle status;
        private static GUIStyle popup;
        private static GUIStyle textField;
        private static GUIStyle centeredTitle;
        private static GUIStyle centeredBody;
        private static GUIStyle button;
        private static GUIStyle primaryButton;
        private static GUIStyle dangerButton;
        private static GUIStyle windowPickerButton;
        private static GUIStyle cardTitle;
        private static GUIStyle cardSubtitle;

        static NativeWindowDockGui()
        {
            DansToolboxTheme.Changed += ResetStyles;
        }

        private static void ResetStyles()
        {
            title = null;
            subtitle = null;
            body = null;
            muted = null;
            status = null;
            popup = null;
            textField = null;
            centeredTitle = null;
            centeredBody = null;
            button = null;
            primaryButton = null;
            dangerButton = null;
            windowPickerButton = null;
            cardTitle = null;
            cardSubtitle = null;
        }

        internal static GUIStyle Title => title ??= CreateLabel(18, FontStyle.Bold, Text);
        internal static GUIStyle Subtitle => subtitle ??= CreateLabel(10, FontStyle.Bold, Muted);
        internal static GUIStyle Body => body ??= CreateLabel(12, FontStyle.Normal, Text);
        internal static GUIStyle MutedLabel => muted ??= CreateLabel(11, FontStyle.Normal, Muted);
        internal static GUIStyle Status => status ??= CreateLabel(10, FontStyle.Bold, Muted);

        internal static GUIStyle CenteredTitle
        {
            get
            {
                if (centeredTitle == null)
                {
                    centeredTitle = CreateLabel(16, FontStyle.Bold, Text);
                    centeredTitle.alignment = TextAnchor.MiddleCenter;
                }

                return centeredTitle;
            }
        }

        internal static GUIStyle CenteredBody
        {
            get
            {
                if (centeredBody == null)
                {
                    centeredBody = CreateLabel(12, FontStyle.Normal, Muted);
                    centeredBody.alignment = TextAnchor.UpperCenter;
                    centeredBody.wordWrap = true;
                }

                return centeredBody;
            }
        }

        internal static GUIStyle Popup
        {
            get
            {
                if (popup == null)
                {
                    popup = new GUIStyle(EditorStyles.popup)
                    {
                        fontSize = 11,
                        fixedHeight = 28,
                        alignment = TextAnchor.MiddleLeft,
                        border = new RectOffset(1, 1, 1, 1),
                        padding = new RectOffset(10, 24, 0, 0),
                        normal =
                        {
                            background = MakeBorderedTexture(Inset, Border),
                            textColor = Text
                        },
                        hover =
                        {
                            background = MakeBorderedTexture(Hover, BorderStrong),
                            textColor = Color.white
                        },
                        focused =
                        {
                            background = MakeBorderedTexture(Inset, AccentSoft),
                            textColor = Color.white
                        }
                    };
                }

                return popup;
            }
        }

        internal static GUIStyle TextField
        {
            get
            {
                if (textField == null)
                {
                    Texture2D normalTexture = MakeBorderedTexture(Inset, Border);
                    Texture2D focusedTexture = MakeBorderedTexture(Inset, Accent);
                    textField = new GUIStyle
                    {
                        fontSize = 11,
                        fixedHeight = 26,
                        alignment = TextAnchor.MiddleLeft,
                        border = new RectOffset(1, 1, 1, 1),
                        padding = new RectOffset(8, 8, 3, 3),
                        clipping = TextClipping.Clip
                    };

                    // Build every state from flat textures. Inheriting EditorStyles.textField
                    // leaks Unity's rounded blue gradient into hover/active/on states.
                    textField.normal.background = normalTexture;
                    textField.normal.textColor = Text;
                    textField.hover.background = normalTexture;
                    textField.hover.textColor = Text;
                    textField.active.background = focusedTexture;
                    textField.active.textColor = Text;
                    textField.focused.background = focusedTexture;
                    textField.focused.textColor = Text;
                    textField.onNormal.background = normalTexture;
                    textField.onNormal.textColor = Text;
                    textField.onHover.background = normalTexture;
                    textField.onHover.textColor = Text;
                    textField.onActive.background = focusedTexture;
                    textField.onActive.textColor = Text;
                    textField.onFocused.background = focusedTexture;
                    textField.onFocused.textColor = Text;
                }

                return textField;
            }
        }

        internal static GUIStyle Button =>
            button ??= CreateButton(Header, Hover, AccentSoft, Text, Color.white);

        internal static GUIStyle PrimaryButton =>
            primaryButton ??= CreateButton(AccentSoft, Accent, Color.Lerp(Inset, AccentSoft, 0.55f),
                Color.white, Color.black);

        internal static GUIStyle DangerButton =>
            dangerButton ??= CreateButton(
                Color.Lerp(Inset, Danger, 0.28f),
                Color.Lerp(Inset, Danger, 0.42f),
                Color.Lerp(Inset, Danger, 0.2f),
                Color.Lerp(Text, Danger, 0.3f),
                Color.white);

        internal static GUIStyle WindowPickerButton
        {
            get
            {
                if (windowPickerButton == null)
                {
                    windowPickerButton = new GUIStyle(Button)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 10,
                        padding = new RectOffset(10, 24, 3, 3)
                    };
                }

                return windowPickerButton;
            }
        }

        internal static GUIStyle CardTitle
        {
            get
            {
                if (cardTitle == null)
                {
                    cardTitle = CreateLabel(10, FontStyle.Bold, Text);
                    cardTitle.alignment = TextAnchor.MiddleLeft;
                    cardTitle.clipping = TextClipping.Clip;
                }

                return cardTitle;
            }
        }

        internal static GUIStyle CardSubtitle
        {
            get
            {
                if (cardSubtitle == null)
                {
                    cardSubtitle = CreateLabel(9, FontStyle.Normal, Muted);
                    cardSubtitle.alignment = TextAnchor.MiddleLeft;
                    cardSubtitle.clipping = TextClipping.Clip;
                }

                return cardSubtitle;
            }
        }

        internal static void DrawPanel(Rect rect, Color fill, Color border)
        {
            EditorGUI.DrawRect(rect, border);
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), fill);
        }

        internal static void DrawStatusDot(Vector2 center, Color color)
        {
            EditorGUI.DrawRect(new Rect(center.x - 3, center.y - 3, 6, 6), color);
        }

        internal static void DrawRackScrews(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            DrawScrew(new Vector2(rect.x + 11f, rect.center.y));
            DrawScrew(new Vector2(rect.xMax - 11f, rect.center.y));
        }

        internal static void DrawTechnicalGrid(Rect rect, float spacing = 28f)
        {
            if (Event.current.type != EventType.Repaint || rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            Color grid = new Color(Border.r, Border.g, Border.b, 0.28f);
            for (float x = rect.x + spacing; x < rect.xMax; x += spacing)
            {
                EditorGUI.DrawRect(new Rect(x, rect.y + 1f, 1f, rect.height - 2f), grid);
            }

            for (float y = rect.y + spacing; y < rect.yMax; y += spacing)
            {
                EditorGUI.DrawRect(new Rect(rect.x + 1f, y, rect.width - 2f, 1f), grid);
            }
        }

        internal static void DrawSignalRail(Rect rect)
        {
            if (!DansToolboxSettings.SeamlessToolSurfaces &&
                Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, Accent);
            }
        }

        private static GUIStyle CreateLabel(int size, FontStyle fontStyle, Color color)
        {
            return new GUIStyle(EditorStyles.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                normal = { textColor = color }
            };
        }

        private static void DrawScrew(Vector2 center)
        {
            EditorGUI.DrawRect(new Rect(center.x - 4f, center.y - 4f, 8f, 8f), BorderStrong);
            EditorGUI.DrawRect(new Rect(center.x - 3f, center.y - 3f, 6f, 6f), Inset);
            EditorGUI.DrawRect(new Rect(center.x - 2f, center.y - 0.5f, 4f, 1f), Muted);
        }

        private static GUIStyle CreateButton(
            Color normalFill,
            Color hoverFill,
            Color activeFill,
            Color normalText,
            Color hoverText)
        {
            Texture2D normalTexture = MakeBorderedTexture(normalFill, Border);
            Texture2D hoverTexture = MakeBorderedTexture(hoverFill, BorderStrong);
            Texture2D activeTexture = MakeBorderedTexture(activeFill, Accent);

            // Start empty, just like RetroSfxSynthGui.CreateFlatButtonStyle. Inheriting
            // GUI.skin.button leaves Unity's rounded gradients in toggle/on states.
            GUIStyle style = new GUIStyle
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 28,
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(6, 6, 3, 3),
                clipping = TextClipping.Clip
            };

            style.normal.background = normalTexture;
            style.normal.textColor = normalText;
            style.hover.background = hoverTexture;
            style.hover.textColor = hoverText;
            style.active.background = activeTexture;
            style.active.textColor = Color.white;
            style.focused.background = hoverTexture;
            style.focused.textColor = hoverText;

            style.onNormal.background = activeTexture;
            style.onNormal.textColor = Color.white;
            style.onHover.background = activeTexture;
            style.onHover.textColor = Color.white;
            style.onActive.background = activeTexture;
            style.onActive.textColor = Color.white;
            style.onFocused.background = activeTexture;
            style.onFocused.textColor = Color.white;
            return style;
        }

        private static Texture2D MakeBorderedTexture(Color fill, Color outline)
        {
            Texture2D texture = new Texture2D(3, 3)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "NativeWindowDockGui",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    bool edge = x == 0 || x == 2 || y == 0 || y == 2;
                    texture.SetPixel(x, y, edge ? outline : fill);
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
