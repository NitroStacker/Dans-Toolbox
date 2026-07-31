using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.NativeWindowDock
{
    internal static class NativeWindowDockGui
    {
        // Mirrors the warm graphite-and-signal palette used by Retro SFX while
        // remaining self-contained so the embedded package can travel between projects.
        internal static readonly Color Canvas = new Color(0.105f, 0.11f, 0.115f);
        internal static readonly Color Raised = new Color(0.155f, 0.16f, 0.165f);
        internal static readonly Color Header = new Color(0.2f, 0.205f, 0.21f);
        internal static readonly Color Inset = new Color(0.09f, 0.095f, 0.1f);
        internal static readonly Color Hover = new Color(0.24f, 0.245f, 0.25f);
        internal static readonly Color Border = new Color(0.27f, 0.275f, 0.28f);
        internal static readonly Color BorderStrong = new Color(0.38f, 0.385f, 0.39f);
        internal static readonly Color Text = new Color(0.88f, 0.875f, 0.84f);
        internal static readonly Color Muted = new Color(0.57f, 0.575f, 0.56f);
        internal static readonly Color Accent = new Color(1f, 0.55f, 0.12f);
        internal static readonly Color AccentHover = new Color(1f, 0.68f, 0.24f);
        internal static readonly Color AccentSoft = new Color(0.66f, 0.33f, 0.08f);
        internal static readonly Color Warning = new Color32(240, 180, 72, 255);
        internal static readonly Color Danger = new Color32(235, 98, 105, 255);

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
                    textField = new GUIStyle(EditorStyles.textField)
                    {
                        fontSize = 11,
                        fixedHeight = 26,
                        border = new RectOffset(1, 1, 1, 1),
                        padding = new RectOffset(8, 8, 4, 3),
                        normal =
                        {
                            background = MakeBorderedTexture(Inset, Border),
                            textColor = Text
                        },
                        focused =
                        {
                            background = MakeBorderedTexture(Inset, AccentSoft),
                            textColor = Color.white
                        }
                    };
                }

                return textField;
            }
        }

        internal static GUIStyle Button =>
            button ??= CreateButton(Header, Hover, AccentSoft, Text, Color.white);

        internal static GUIStyle PrimaryButton =>
            primaryButton ??= CreateButton(AccentSoft, Accent, new Color(0.48f, 0.23f, 0.05f),
                Color.white, Color.black);

        internal static GUIStyle DangerButton =>
            dangerButton ??= CreateButton(new Color32(86, 42, 48, 255), new Color32(111, 48, 56, 255),
                new Color32(73, 36, 41, 255), new Color32(255, 217, 220, 255), Color.white);

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
            if (Event.current.type == EventType.Repaint)
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
