using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.RetroVfx
{
    internal static class RetroVfxGui
    {
        internal enum TransportIcon
        {
            Play,
            Pause,
            Stop,
            Regenerate,
            Save
        }

        private static GUIStyle panel;
        private static GUIStyle inset;
        private static GUIStyle header;
        private static GUIStyle title;
        private static GUIStyle subtitle;
        private static GUIStyle section;
        private static GUIStyle label;
        private static GUIStyle value;
        private static GUIStyle tiny;
        private static GUIStyle knobLabel;
        private static GUIStyle help;
        private static GUIStyle presetDescription;
        private static GUIStyle presetAction;
        private static GUIStyle tab;
        private static GUIStyle primary;
        private static GUIStyle textField;
        private static float knobDragStartValue;
        private static float knobDragStartMouseY;

        internal static Color Canvas => DansToolboxTheme.Current.Canvas;
        internal static Color Panel => DansToolboxTheme.Current.Panel;
        internal static Color Inset => DansToolboxTheme.Current.Inset;
        internal static Color Raised => DansToolboxTheme.Current.Raised;
        internal static Color Border => DansToolboxTheme.Current.Border;
        internal static Color BorderStrong => DansToolboxTheme.Current.BorderStrong;
        internal static Color Text => DansToolboxTheme.Current.Text;
        internal static Color Muted => DansToolboxTheme.Current.Muted;
        internal static Color Accent => DansToolboxTheme.Current.Accent;
        internal static Color AccentSoft => DansToolboxTheme.Current.AccentSoft;
        internal static Color Signal => DansToolboxTheme.Current.Signal;
        internal static Color Success => DansToolboxTheme.Current.Success;
        internal static Color Warning => DansToolboxTheme.Current.Warning;
        internal static Color Danger => DansToolboxTheme.Current.Danger;
        internal static Color PreviewBackground => Color.Lerp(Inset, Color.black, 0.28f);

        static RetroVfxGui()
        {
            DansToolboxTheme.Changed += Reset;
        }

        internal static GUIStyle PanelStyle => panel ??= Box(Panel, Border, 10);
        internal static GUIStyle InsetStyle => inset ??= Box(Inset, Border, 8);
        internal static GUIStyle HeaderStyle => header ??= Box(Raised, BorderStrong, 10);
        internal static GUIStyle TitleStyle => title ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Text }
        };
        internal static GUIStyle SubtitleStyle => subtitle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Muted }
        };
        internal static GUIStyle SectionStyle => section ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Muted }
        };
        internal static GUIStyle LabelStyle => label ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            normal = { textColor = Text }
        };
        internal static GUIStyle ValueStyle => value ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Text }
        };
        internal static GUIStyle TinyStyle => tiny ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            normal = { textColor = Muted }
        };
        private static GUIStyle KnobLabelStyle => knobLabel ??= new GUIStyle(TinyStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip
        };
        private static GUIStyle PresetDescriptionStyle => presetDescription ??= new GUIStyle(TinyStyle)
        {
            clipping = TextClipping.Clip
        };
        private static GUIStyle PresetActionStyle => presetAction ??= new GUIStyle(TinyStyle)
        {
            alignment = TextAnchor.MiddleRight,
            fontStyle = FontStyle.Bold
        };
        internal static GUIStyle HelpStyle => help ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            wordWrap = true,
            normal = { textColor = Muted }
        };
        internal static GUIStyle TabStyle => tab ??= FlatButton(
            Raised,
            Color.Lerp(Raised, AccentSoft, 0.28f),
            AccentSoft,
            Text,
            Color.white,
            28f);
        internal static GUIStyle PrimaryStyle => primary ??= FlatButton(AccentSoft, Accent, Signal, Color.white, Color.black, 42f);
        internal static GUIStyle TextFieldStyle => textField ??= new GUIStyle(EditorStyles.textField)
        {
            fixedHeight = 22f,
            fontSize = 10,
            padding = new RectOffset(7, 7, 3, 3),
            border = new RectOffset(1, 1, 1, 1),
            normal = { background = BorderedTexture(Inset, Border), textColor = Text },
            focused = { background = BorderedTexture(Inset, AccentSoft), textColor = Color.white }
        };

        internal static bool TransportButton(TransportIcon icon, string tooltip, bool active = false)
        {
            Rect rect = GUILayoutUtility.GetRect(28f, 28f, GUILayout.Width(28f), GUILayout.Height(28f));
            Event current = Event.current;
            bool hover = rect.Contains(current.mousePosition);
            bool clicked = GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
            if (current.type == EventType.Repaint)
            {
                DrawBox(rect, active ? Accent : hover ? DansToolboxTheme.Current.Hover : Inset, active ? Signal : hover ? BorderStrong : Border);
                DrawTransportIcon(rect, icon, active ? Color.black : Text);
            }
            return clicked;
        }

        internal static bool TabButton(string text, string tooltip, bool selected, params GUILayoutOption[] options)
        {
            bool clicked = GUILayout.Toggle(selected, new GUIContent(text, tooltip), TabStyle, options);
            return clicked != selected;
        }

        internal static bool PresetButton(string title, string description, string tooltip, bool selected)
        {
            Rect rect = GUILayoutUtility.GetRect(180f, 42f, GUILayout.ExpandWidth(true), GUILayout.Height(42f));
            Event current = Event.current;
            bool hover = rect.Contains(current.mousePosition);
            bool clicked = GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
            if (current.type == EventType.Repaint)
            {
                Color fill = selected
                    ? Color.Lerp(Inset, AccentSoft, hover ? 0.62f : 0.45f)
                    : hover ? Color.Lerp(Raised, AccentSoft, 0.28f) : Inset;
                Color outline = selected ? Accent : hover ? BorderStrong : Border;
                DrawBox(rect, fill, outline);
                if (selected)
                {
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), Signal);
                }
                GUI.Label(new Rect(rect.x + 9f, rect.y + 5f, rect.width - 72f, 15f), title, LabelStyle);
                GUI.Label(new Rect(rect.x + 9f, rect.y + 21f, rect.width - 18f, 14f), description, PresetDescriptionStyle);
                if (selected || hover)
                {
                    Color previous = PresetActionStyle.normal.textColor;
                    PresetActionStyle.normal.textColor = selected ? Signal : Text;
                    GUI.Label(new Rect(rect.xMax - 67f, rect.y + 5f, 58f, 15f), selected ? "REROLL" : "TRY", PresetActionStyle);
                    PresetActionStyle.normal.textColor = previous;
                }
            }
            return clicked;
        }

        internal static float Knob(
            string controlName,
            string labelText,
            float currentValue,
            float minimum,
            float maximum,
            float defaultValue,
            string valueFormat,
            string tooltip,
            float width = 64f)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 84f, GUILayout.Width(width), GUILayout.Height(84f));
            Rect knobRect = new Rect(rect.center.x - 21f, rect.y + 2f, 42f, 42f);
            int controlId = GUIUtility.GetControlID(controlName.GetHashCode(), FocusType.Passive, knobRect);
            Event current = Event.current;

            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (current.button == 0 && knobRect.Contains(current.mousePosition))
                    {
                        if (current.clickCount == 2)
                        {
                            currentValue = defaultValue;
                            GUI.changed = true;
                        }
                        else
                        {
                            GUIUtility.hotControl = controlId;
                            knobDragStartValue = currentValue;
                            knobDragStartMouseY = current.mousePosition.y;
                        }
                        current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        float sensitivity = current.shift ? 500f : 140f;
                        float normalizedDelta = (knobDragStartMouseY - current.mousePosition.y) / sensitivity;
                        currentValue = Mathf.Clamp(
                            knobDragStartValue + normalizedDelta * (maximum - minimum),
                            minimum,
                            maximum);
                        GUI.changed = true;
                        current.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }
                    break;
                case EventType.ScrollWheel:
                    if (knobRect.Contains(current.mousePosition))
                    {
                        float step = (maximum - minimum) * (current.shift ? 0.002f : 0.01f);
                        currentValue = Mathf.Clamp(currentValue - current.delta.y * step, minimum, maximum);
                        GUI.changed = true;
                        current.Use();
                    }
                    break;
            }

            if (current.type == EventType.Repaint)
            {
                bool hover = knobRect.Contains(current.mousePosition);
                DrawKnob(knobRect, Mathf.InverseLerp(minimum, maximum, currentValue), hover, GUIUtility.hotControl == controlId);
                GUI.Label(
                    new Rect(rect.x, knobRect.yMax + 3f, rect.width, 14f),
                    new GUIContent(labelText.ToUpperInvariant(), tooltip),
                    KnobLabelStyle);
                GUI.Label(
                    new Rect(rect.x, knobRect.yMax + 18f, rect.width, 16f),
                    FormatKnobValue(currentValue, valueFormat),
                    ValueStyle);
            }
            return currentValue;
        }

        internal static float Slider(string name, float value, float minimum, float maximum, string format, string tooltip)
        {
            GUILayout.BeginVertical(InsetStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(name, tooltip), TinyStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value.ToString(format), LabelStyle, GUILayout.Width(54f));
            GUILayout.EndHorizontal();
            Rect rect = GUILayoutUtility.GetRect(20f, 20f, GUILayout.ExpandWidth(true));
            value = GUI.HorizontalSlider(rect, value, minimum, maximum);
            if (Event.current.type == EventType.Repaint)
            {
                Rect rail = new Rect(rect.x + 4f, rect.center.y - 2f, rect.width - 8f, 4f);
                EditorGUI.DrawRect(rail, Border);
                float t = Mathf.InverseLerp(minimum, maximum, value);
                EditorGUI.DrawRect(new Rect(rail.x, rail.y, rail.width * t, rail.height), Accent);
            }
            GUILayout.EndVertical();
            return value;
        }

        internal static void DrawPreviewOverlay(Rect rect, float time, float duration, float zoom)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            DrawBorder(rect, BorderStrong);
            float centerX = rect.center.x;
            float centerY = rect.center.y;
            Color grid = new Color(Border.r, Border.g, Border.b, 0.25f);
            EditorGUI.DrawRect(new Rect(centerX, rect.y + 10f, 1f, rect.height - 20f), grid);
            EditorGUI.DrawRect(new Rect(rect.x + 10f, centerY, rect.width - 20f, 1f), grid);
            Handles.BeginGUI();
            Handles.color = new Color(Accent.r, Accent.g, Accent.b, 0.25f);
            Handles.DrawWireDisc(rect.center, Vector3.forward, Mathf.Min(rect.width, rect.height) * 0.18f);
            Handles.DrawWireDisc(rect.center, Vector3.forward, Mathf.Min(rect.width, rect.height) * 0.34f);
            Handles.EndGUI();

            float progress = duration <= 0f ? 0f : Mathf.Clamp01(time / duration);
            Rect timeline = new Rect(rect.x + 12f, rect.yMax - 9f, rect.width - 24f, 3f);
            EditorGUI.DrawRect(timeline, Border);
            EditorGUI.DrawRect(new Rect(timeline.x, timeline.y, timeline.width * progress, timeline.height), Accent);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 7f, 100f, 18f), $"{time:0.00}s", TinyStyle);
            GUI.Label(new Rect(rect.xMax - 70f, rect.y + 7f, 60f, 18f), $"{zoom:0.0}x", TinyStyle);
        }

        internal static void DrawStatus(string message, MessageType type)
        {
            Color color = type switch
            {
                MessageType.Error => Danger,
                MessageType.Warning => Warning,
                MessageType.Info => Accent,
                _ => Success
            };
            Rect rect = GUILayoutUtility.GetRect(20f, 34f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                DrawBox(rect, Inset, Border);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), color);
            }
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 18f, rect.height - 8f), message, HelpStyle);
        }

        internal static void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static void Reset()
        {
            panel = null;
            inset = null;
            header = null;
            title = null;
            subtitle = null;
            section = null;
            label = null;
            value = null;
            tiny = null;
            knobLabel = null;
            help = null;
            presetDescription = null;
            presetAction = null;
            tab = null;
            primary = null;
            textField = null;
        }

        private static void DrawKnob(Rect rect, float normalizedValue, bool hover, bool active)
        {
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.42f;
            DrawArc(center, radius + 4f, 135f, 405f, BorderStrong, 2f);
            DrawArc(center, radius + 4f, 135f, Mathf.Lerp(135f, 405f, normalizedValue), Accent, 2.6f);

            Handles.BeginGUI();
            Handles.color = active
                ? Color.Lerp(Raised, AccentSoft, 0.6f)
                : hover ? Color.Lerp(Raised, AccentSoft, 0.28f) : Raised;
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Handles.color = active ? Signal : hover ? Accent : BorderStrong;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            float angle = Mathf.Lerp(135f, 405f, normalizedValue) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Handles.color = active ? Signal : Text;
            Handles.DrawAAPolyLine(2.1f, center + direction * 4f, center + direction * (radius - 5f));
            Handles.EndGUI();
        }

        private static void DrawArc(Vector2 center, float radius, float startDegrees, float endDegrees, Color color, float width)
        {
            const int segments = 28;
            Vector3[] points = new Vector3[segments + 1];
            for (int index = 0; index <= segments; index++)
            {
                float angle = Mathf.Lerp(startDegrees, endDegrees, index / (float)segments) * Mathf.Deg2Rad;
                points[index] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(width, points);
            Handles.EndGUI();
        }

        private static string FormatKnobValue(float valueToFormat, string format)
        {
            return format switch
            {
                "s" => valueToFormat < 1f ? $"{valueToFormat * 1000f:0} ms" : $"{valueToFormat:0.00} s",
                "x" => $"{valueToFormat:0.00}\u00D7",
                "deg" => $"{valueToFormat:0}\u00B0",
                "%" => $"{valueToFormat * 100f:0}%",
                _ => valueToFormat.ToString(format)
            };
        }

        private static GUIStyle Box(Color fill, Color outline, int padding)
        {
            return new GUIStyle
            {
                normal = { background = BorderedTexture(fill, outline) },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(padding, padding, padding, padding),
                margin = new RectOffset(0, 0, 0, 0)
            };
        }

        private static GUIStyle FlatButton(Color normal, Color hover, Color active, Color text, Color activeText, float height)
        {
            Texture2D normalTexture = BorderedTexture(normal, Border);
            Texture2D hoverTexture = BorderedTexture(hover, BorderStrong);
            Texture2D activeTexture = BorderedTexture(active, Accent);
            Texture2D selectedTexture = BorderedTexture(active, Signal);
            Texture2D selectedHoverTexture = BorderedTexture(Color.Lerp(active, Accent, 0.45f), Signal);

            GUIStyle style = new GUIStyle
            {
                fixedHeight = height,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(7, 7, 2, 2),
                margin = new RectOffset(0, 0, 0, 0)
            };

            // Build every state from scratch. Copying GUI.skin.button also copies its
            // high-DPI scaled backgrounds, which makes Unity silently restore the
            // stock glossy bevel instead of using our flat textures.
            SetState(style.normal, normalTexture, text);
            SetState(style.hover, hoverTexture, Color.white);
            SetState(style.active, activeTexture, activeText);
            SetState(style.focused, hoverTexture, Color.white);
            SetState(style.onNormal, selectedTexture, activeText);
            SetState(style.onHover, selectedHoverTexture, activeText);
            SetState(style.onActive, selectedHoverTexture, activeText);
            SetState(style.onFocused, selectedHoverTexture, activeText);
            return style;
        }

        private static void SetState(GUIStyleState state, Texture2D background, Color textColor)
        {
            state.background = background;
            state.scaledBackgrounds = System.Array.Empty<Texture2D>();
            state.textColor = textColor;
        }

        private static Texture2D BorderedTexture(Color fill, Color outline)
        {
            Texture2D texture = new Texture2D(3, 3, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] colors = new Color[9];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = i == 4 ? fill : outline;
            }
            texture.SetPixels(colors);
            texture.Apply();
            return texture;
        }

        private static void DrawBox(Rect rect, Color fill, Color outline)
        {
            EditorGUI.DrawRect(rect, outline);
            EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), fill);
        }

        private static void DrawTransportIcon(Rect rect, TransportIcon icon, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Vector2 center = rect.center;
            switch (icon)
            {
                case TransportIcon.Play:
                    Handles.DrawAAConvexPolygon(center + new Vector2(-4f, -7f), center + new Vector2(7f, 0f), center + new Vector2(-4f, 7f));
                    break;
                case TransportIcon.Pause:
                    EditorGUI.DrawRect(new Rect(center.x - 5f, center.y - 7f, 3f, 14f), color);
                    EditorGUI.DrawRect(new Rect(center.x + 2f, center.y - 7f, 3f, 14f), color);
                    break;
                case TransportIcon.Stop:
                    EditorGUI.DrawRect(new Rect(center.x - 6f, center.y - 6f, 12f, 12f), color);
                    break;
                case TransportIcon.Regenerate:
                    Handles.DrawWireArc(center, Vector3.forward, Vector3.up, 292f, 7f);
                    Handles.DrawAAConvexPolygon(center + new Vector2(6f, -5f), center + new Vector2(7f, 2f), center + new Vector2(1f, -1f));
                    break;
                case TransportIcon.Save:
                    Handles.DrawAAPolyLine(2f, center + new Vector2(-6f, -7f), center + new Vector2(6f, -7f), center + new Vector2(6f, 7f), center + new Vector2(-6f, 7f), center + new Vector2(-6f, -7f));
                    EditorGUI.DrawRect(new Rect(center.x - 3f, center.y - 6f, 6f, 5f), color);
                    break;
            }
            Handles.EndGUI();
        }
    }
}
