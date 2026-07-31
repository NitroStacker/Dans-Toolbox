using UnityEditor;
using UnityEngine;
using DansToolbox.Editor;

namespace DansToolbox.EditorTools.Audio
{
    /// <summary>
    /// Shared IMGUI controls and visual tokens for the synth-style editor surface.
    /// </summary>
    internal static class RetroSfxSynthGui
    {
        internal enum TransportIcon
        {
            Play,
            Stop,
            Save,
            Reset
        }

        internal static Color Canvas => DansToolboxTheme.Current.Canvas;
        internal static Color Panel => DansToolboxTheme.Current.Panel;
        internal static Color PanelInset => DansToolboxTheme.Current.Inset;
        internal static Color PanelRaised => DansToolboxTheme.Current.Raised;
        internal static Color Border => DansToolboxTheme.Current.Border;
        internal static Color BorderStrong => DansToolboxTheme.Current.BorderStrong;
        internal static Color Text => DansToolboxTheme.Current.Text;
        internal static Color MutedText => DansToolboxTheme.Current.Muted;
        internal static Color Accent => DansToolboxTheme.Current.Accent;
        internal static Color AccentSoft => DansToolboxTheme.Current.AccentSoft;
        internal static Color Signal => DansToolboxTheme.Current.Signal;
        internal static Color Success => DansToolboxTheme.Current.Success;
        internal static Color Danger => DansToolboxTheme.Current.Danger;

        private static GUIStyle panelStyle;
        private static GUIStyle insetStyle;
        private static GUIStyle headerStyle;
        private static GUIStyle titleStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle sectionTitleStyle;
        private static GUIStyle labelStyle;
        private static GUIStyle valueStyle;
        private static GUIStyle presetButtonStyle;
        private static GUIStyle primaryButtonStyle;
        private static GUIStyle fieldStyle;
        private static GUIStyle statusStyle;
        private static GUIStyle tinyStyle;
        private static GUIStyle helpStyle;
        private static GUIStyle inlineValueStyle;
        private static float dragStartValue;
        private static float dragStartMouseY;

        static RetroSfxSynthGui()
        {
            DansToolboxTheme.Changed += ResetStyles;
        }

        private static void ResetStyles()
        {
            panelStyle = null;
            insetStyle = null;
            headerStyle = null;
            titleStyle = null;
            subtitleStyle = null;
            sectionTitleStyle = null;
            labelStyle = null;
            valueStyle = null;
            presetButtonStyle = null;
            primaryButtonStyle = null;
            fieldStyle = null;
            statusStyle = null;
            tinyStyle = null;
            helpStyle = null;
            inlineValueStyle = null;
        }

        internal static GUIStyle PanelStyle => panelStyle ??= CreatePanelStyle(Panel, Border, 10);
        internal static GUIStyle InsetStyle => insetStyle ??= CreatePanelStyle(PanelInset, Border, 8);
        internal static GUIStyle HeaderStyle => headerStyle ??= CreatePanelStyle(PanelRaised, BorderStrong, 10);

        internal static GUIStyle TitleStyle => titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Text, background = null },
            alignment = TextAnchor.MiddleLeft
        };

        internal static GUIStyle SubtitleStyle => subtitleStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            normal = { textColor = MutedText, background = null },
            alignment = TextAnchor.MiddleLeft
        };

        internal static GUIStyle SectionTitleStyle => sectionTitleStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            normal = { textColor = MutedText, background = null },
            alignment = TextAnchor.MiddleLeft
        };

        internal static GUIStyle LabelStyle => labelStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            normal = { textColor = MutedText, background = null },
            alignment = TextAnchor.UpperCenter
        };

        internal static GUIStyle ValueStyle => valueStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Text, background = null },
            alignment = TextAnchor.UpperCenter
        };

        internal static GUIStyle TinyStyle => tinyStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            normal = { textColor = MutedText, background = null },
            alignment = TextAnchor.MiddleLeft
        };

        internal static GUIStyle HelpStyle => helpStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            normal = { textColor = MutedText, background = null },
            alignment = TextAnchor.UpperLeft,
            wordWrap = true
        };

        internal static GUIStyle InlineValueStyle => inlineValueStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Text, background = null },
            alignment = TextAnchor.MiddleRight
        };

        internal static GUIStyle PresetButtonStyle =>
            presetButtonStyle ??= CreateFlatButtonStyle(
                PanelRaised,
                DansToolboxTheme.Current.Hover,
                AccentSoft,
                Text,
                Color.white,
                26f,
                new RectOffset(2, 2, 2, 2));

        internal static GUIStyle PrimaryButtonStyle =>
            primaryButtonStyle ??= CreateFlatButtonStyle(
                AccentSoft,
                Accent,
                Color.Lerp(PanelInset, AccentSoft, 0.55f),
                Color.white,
                Color.black,
                48f,
                new RectOffset(0, 0, 0, 0));

        internal static GUIStyle FieldStyle => fieldStyle ??= new GUIStyle(EditorStyles.textField)
        {
            fixedHeight = 22f,
            fontSize = 10,
            normal =
            {
                background = CreateBorderedTexture(PanelInset, Border),
                textColor = Text
            },
            focused =
            {
                background = CreateBorderedTexture(PanelInset, AccentSoft),
                textColor = Color.white
            },
            border = new RectOffset(1, 1, 1, 1),
            padding = new RectOffset(7, 7, 3, 3)
        };

        internal static GUIStyle StatusStyle => statusStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            normal = { textColor = MutedText, background = null },
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            padding = new RectOffset(7, 7, 5, 5)
        };

        internal static void DrawRackScrews(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            DrawScrew(new Vector2(rect.x + 11f, rect.center.y));
            DrawScrew(new Vector2(rect.xMax - 11f, rect.center.y));
        }

        internal static bool TransportButton(
            TransportIcon icon,
            string tooltip,
            bool highlighted = false,
            float size = 34f)
        {
            Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            Event currentEvent = Event.current;
            bool hovered = rect.Contains(currentEvent.mousePosition);
            bool clicked = GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);

            if (currentEvent.type == EventType.Repaint)
            {
                Color fill = highlighted
                    ? Accent
                    : hovered ? DansToolboxTheme.Current.Hover : PanelInset;
                Color outline = highlighted ? Signal : hovered ? BorderStrong : Border;
                DrawFlatBox(rect, fill, outline);
                DrawTransportIcon(rect, icon, highlighted ? Color.black : Text);
            }

            return clicked;
        }

        internal static bool PresetButton(string label, string tooltip)
        {
            return GUILayout.Button(new GUIContent(label, tooltip), PresetButtonStyle);
        }

        internal static bool RectButton(
            Rect rect,
            string label,
            string tooltip,
            bool selected = false,
            bool danger = false)
        {
            Event currentEvent = Event.current;
            bool hovered = rect.Contains(currentEvent.mousePosition);
            bool clicked = GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);

            if (currentEvent.type == EventType.Repaint)
            {
                Color fill = selected
                    ? AccentSoft
                    : hovered ? PanelRaised : PanelInset;
                Color outline = danger && hovered
                    ? Danger
                    : selected ? Accent : hovered ? BorderStrong : Border;
                DrawFlatBox(rect, fill, outline);

                GUIStyle style = new GUIStyle(LabelStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                style.normal.textColor = danger && hovered
                    ? Color.Lerp(Text, Danger, 0.55f)
                    : selected ? Color.white : Text;
                GUI.Label(rect, label, style);
            }

            return clicked;
        }

        internal static bool FoldoutButton(Rect rect, bool expanded, string tooltip)
        {
            bool clicked = GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
            if (Event.current.type != EventType.Repaint)
            {
                return clicked;
            }

            Vector2 center = rect.center;
            Handles.BeginGUI();
            Handles.color = Text;
            if (expanded)
            {
                Handles.DrawAAConvexPolygon(
                    center + new Vector2(-4f, -2f),
                    center + new Vector2(4f, -2f),
                    center + new Vector2(0f, 3f));
            }
            else
            {
                Handles.DrawAAConvexPolygon(
                    center + new Vector2(-2f, -4f),
                    center + new Vector2(3f, 0f),
                    center + new Vector2(-2f, 4f));
            }
            Handles.EndGUI();
            return clicked;
        }

        internal static void DrawDragHandle(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            for (int index = -1; index <= 1; index++)
            {
                float y = Mathf.Round(rect.center.y + index * 4f);
                EditorGUI.DrawRect(
                    new Rect(rect.center.x - 4f, y, 8f, 1f),
                    MutedText);
            }
        }

        internal static float EffectSlider(
            Rect rect,
            string controlName,
            string label,
            float value,
            float minimum,
            float maximum,
            string valueFormat,
            string tooltip,
            float? displayedValue = null,
            bool automationActive = false)
        {
            Rect labelRect = new Rect(rect.x, rect.y, rect.width, 14f);
            Rect valueRect = new Rect(rect.x, rect.y, rect.width, 14f);
            Rect trackRect = new Rect(rect.x, rect.y + 23f, rect.width, 12f);
            Rect hitRect = new Rect(trackRect.x, trackRect.y - 4f, trackRect.width, 20f);
            int controlId = GUIUtility.GetControlID(
                controlName.GetHashCode(),
                FocusType.Passive,
                hitRect);
            Event currentEvent = Event.current;

            switch (currentEvent.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (currentEvent.button == 0 && hitRect.Contains(currentEvent.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        value = Mathf.Lerp(
                            minimum,
                            maximum,
                            Mathf.InverseLerp(trackRect.x, trackRect.xMax, currentEvent.mousePosition.x));
                        GUI.changed = true;
                        currentEvent.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        value = Mathf.Lerp(
                            minimum,
                            maximum,
                            Mathf.InverseLerp(trackRect.x, trackRect.xMax, currentEvent.mousePosition.x));
                        GUI.changed = true;
                        currentEvent.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        currentEvent.Use();
                    }
                    break;
                case EventType.ScrollWheel:
                    if (rect.Contains(currentEvent.mousePosition))
                    {
                        float step = (maximum - minimum) * (currentEvent.shift ? 0.002f : 0.01f);
                        value = Mathf.Clamp(value - currentEvent.delta.y * step, minimum, maximum);
                        GUI.changed = true;
                        currentEvent.Use();
                    }
                    break;
            }

            value = Mathf.Clamp(value, minimum, maximum);
            if (currentEvent.type == EventType.Repaint)
            {
                float visualValue =
                    automationActive &&
                    displayedValue.HasValue &&
                    GUIUtility.hotControl != controlId
                        ? Mathf.Clamp(displayedValue.Value, minimum, maximum)
                        : value;
                GUIStyle parameterLabelStyle = new GUIStyle(TinyStyle)
                {
                    alignment = TextAnchor.MiddleLeft
                };
                parameterLabelStyle.normal.textColor =
                    automationActive ? Accent : Text;
                GUI.Label(
                    labelRect,
                    new GUIContent(
                        automationActive
                            ? $"{label.ToUpperInvariant()}  AUTO"
                            : label.ToUpperInvariant(),
                        tooltip),
                    parameterLabelStyle);
                GUI.Label(
                    valueRect,
                    FormatValue(visualValue, valueFormat),
                    InlineValueStyle);

                Rect lineRect = new Rect(trackRect.x, trackRect.center.y - 2f, trackRect.width, 4f);
                EditorGUI.DrawRect(lineRect, Border);
                float normalized = Mathf.InverseLerp(
                    minimum,
                    maximum,
                    visualValue);
                EditorGUI.DrawRect(
                    new Rect(lineRect.x, lineRect.y, lineRect.width * normalized, lineRect.height),
                    automationActive ? Accent : AccentSoft);
                float handleX = Mathf.Round(Mathf.Lerp(lineRect.x, lineRect.xMax, normalized));
                EditorGUI.DrawRect(
                    new Rect(handleX - 2f, trackRect.y, 4f, trackRect.height),
                    GUIUtility.hotControl == controlId || automationActive
                        ? Signal
                        : Text);
            }

            return value;
        }

        internal static bool WaveButton(RetroWaveType waveType, bool selected)
        {
            Rect rect = GUILayoutUtility.GetRect(54f, 48f, GUILayout.ExpandWidth(true));
            bool clicked = GUI.Button(
                rect,
                new GUIContent(string.Empty, $"{waveType} oscillator"),
                GUIStyle.none);

            if (Event.current.type == EventType.Repaint)
            {
                Color fill = selected ? Color.Lerp(PanelInset, AccentSoft, 0.45f) : PanelInset;
                DrawFlatBox(rect, fill, selected ? Accent : Border);
                Rect iconRect = new Rect(rect.x + 9f, rect.y + 7f, rect.width - 18f, 20f);
                DrawWaveIcon(iconRect, waveType, selected ? Signal : Text);
                GUI.Label(
                    new Rect(rect.x, rect.yMax - 17f, rect.width, 14f),
                    waveType == RetroWaveType.Saw ? "SAW" : waveType.ToString().ToUpperInvariant(),
                    LabelStyle);
            }

            return clicked;
        }

        internal static float Knob(
            string controlName,
            string label,
            float value,
            float minimum,
            float maximum,
            float defaultValue,
            string valueFormat,
            string tooltip,
            float width = 70f)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 88f, GUILayout.Width(width));
            Rect knobRect = new Rect(rect.center.x - 23f, rect.y + 2f, 46f, 46f);
            int controlId = GUIUtility.GetControlID(controlName.GetHashCode(), FocusType.Passive, knobRect);
            Event currentEvent = Event.current;

            switch (currentEvent.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (currentEvent.button == 0 && knobRect.Contains(currentEvent.mousePosition))
                    {
                        if (currentEvent.clickCount == 2)
                        {
                            value = defaultValue;
                            GUI.changed = true;
                        }
                        else
                        {
                            GUIUtility.hotControl = controlId;
                            dragStartValue = value;
                            dragStartMouseY = currentEvent.mousePosition.y;
                        }

                        currentEvent.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        float sensitivity = currentEvent.shift ? 500f : 140f;
                        float normalizedDelta = (dragStartMouseY - currentEvent.mousePosition.y) / sensitivity;
                        value = Mathf.Clamp(dragStartValue + normalizedDelta * (maximum - minimum), minimum, maximum);
                        GUI.changed = true;
                        currentEvent.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        currentEvent.Use();
                    }
                    break;
                case EventType.ScrollWheel:
                    if (knobRect.Contains(currentEvent.mousePosition))
                    {
                        float step = (maximum - minimum) * (currentEvent.shift ? 0.002f : 0.01f);
                        value = Mathf.Clamp(value - currentEvent.delta.y * step, minimum, maximum);
                        GUI.changed = true;
                        currentEvent.Use();
                    }
                    break;
            }

            if (currentEvent.type == EventType.Repaint)
            {
                float normalizedValue = Mathf.InverseLerp(minimum, maximum, value);
                DrawKnob(knobRect, normalizedValue, GUIUtility.hotControl == controlId);
                GUI.Label(
                    new Rect(rect.x, knobRect.yMax + 3f, rect.width, 15f),
                    new GUIContent(label.ToUpperInvariant(), tooltip),
                    LabelStyle);
                GUI.Label(
                    new Rect(rect.x, knobRect.yMax + 20f, rect.width, 16f),
                    FormatValue(value, valueFormat),
                    ValueStyle);
            }

            return value;
        }

        internal static void DrawEnvelopeGraph(Rect rect, RetroSfxSettings settings)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            DrawFlatBox(rect, PanelInset, Border);
            DrawGraphGrid(rect);

            float total = Mathf.Max(0.0001f, settings.Duration);
            float graphWidth = rect.width - 20f;
            float baseline = rect.yMax - 12f;
            float top = rect.y + 12f;
            float attackX = rect.x + 10f + graphWidth * settings.AttackTime / total;
            float sustainX = attackX + graphWidth * settings.SustainTime / total;
            float endX = rect.xMax - 10f;
            float punchY = Mathf.Lerp(top, baseline, 1f / Mathf.Max(1f, 1f + settings.SustainPunch * 2f));

            Vector3[] points =
            {
                new Vector3(rect.x + 10f, baseline),
                new Vector3(attackX, top),
                new Vector3(attackX, punchY),
                new Vector3(sustainX, top + 4f),
                new Vector3(endX, baseline)
            };

            Handles.BeginGUI();
            Handles.color = Signal;
            Handles.DrawAAPolyLine(2.2f, points);
            foreach (Vector3 point in points)
            {
                Handles.DrawSolidDisc(point, Vector3.forward, 2.5f);
            }
            Handles.EndGUI();
        }

        internal static void DrawWaveformGrid(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, PanelInset);
            DrawBorder(rect, Border);
            Color gridColor = new Color(Border.r, Border.g, Border.b, 0.45f);
            for (int index = 1; index < 8; index++)
            {
                float x = Mathf.Round(rect.x + rect.width * index / 8f);
                EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height), gridColor);
            }
            for (int index = 1; index < 4; index++)
            {
                float y = Mathf.Round(rect.y + rect.height * index / 4f);
                EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, 1f), gridColor);
            }
        }

        internal static void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static GUIStyle CreatePanelStyle(Color fill, Color outline, int padding)
        {
            return new GUIStyle
            {
                normal = { background = CreateBorderedTexture(fill, outline) },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(padding, padding, padding, padding),
                margin = new RectOffset(0, 0, 0, 8),
                stretchWidth = true
            };
        }

        private static Texture2D CreateBorderedTexture(Color fill, Color outline)
        {
            Texture2D texture = new Texture2D(3, 3)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[9];
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    pixels[y * 3 + x] = x == 0 || x == 2 || y == 0 || y == 2 ? outline : fill;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void DrawFlatBox(Rect rect, Color fill, Color outline)
        {
            EditorGUI.DrawRect(rect, outline);
            EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), fill);
        }

        private static GUIStyle CreateFlatButtonStyle(
            Color normalFill,
            Color hoverFill,
            Color activeFill,
            Color normalText,
            Color hoverText,
            float height,
            RectOffset margin)
        {
            Texture2D normalTexture = CreateBorderedTexture(normalFill, Border);
            Texture2D hoverTexture = CreateBorderedTexture(hoverFill, BorderStrong);
            Texture2D activeTexture = CreateBorderedTexture(activeFill, Accent);

            GUIStyle style = new GUIStyle
            {
                fixedHeight = height,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(6, 6, 3, 3),
                margin = margin
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

        private static void DrawScrew(Vector2 center)
        {
            Handles.BeginGUI();
            Handles.color = PanelInset;
            Handles.DrawSolidDisc(center, Vector3.forward, 4f);
            Handles.color = BorderStrong;
            Handles.DrawWireDisc(center, Vector3.forward, 4f);
            Handles.DrawLine(center + new Vector2(-2f, 0f), center + new Vector2(2f, 0f));
            Handles.EndGUI();
        }

        private static void DrawTransportIcon(Rect rect, TransportIcon icon, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Vector2 center = rect.center;
            switch (icon)
            {
                case TransportIcon.Play:
                    Handles.DrawAAConvexPolygon(
                        center + new Vector2(-5f, -8f),
                        center + new Vector2(8f, 0f),
                        center + new Vector2(-5f, 8f));
                    break;
                case TransportIcon.Stop:
                    EditorGUI.DrawRect(new Rect(center.x - 6f, center.y - 6f, 12f, 12f), color);
                    break;
                case TransportIcon.Save:
                    Handles.DrawAAPolyLine(
                        2f,
                        center + new Vector2(-7f, -7f),
                        center + new Vector2(7f, -7f),
                        center + new Vector2(7f, 7f),
                        center + new Vector2(-7f, 7f),
                        center + new Vector2(-7f, -7f));
                    EditorGUI.DrawRect(new Rect(center.x - 3f, center.y - 7f, 6f, 6f), color);
                    Handles.DrawAAPolyLine(
                        2f,
                        center + new Vector2(-4f, 5f),
                        center + new Vector2(4f, 5f));
                    break;
                case TransportIcon.Reset:
                    Handles.DrawWireArc(center, Vector3.forward, Vector3.up, 290f, 8f);
                    Handles.DrawAAConvexPolygon(
                        center + new Vector2(-8f, -1f),
                        center + new Vector2(-3f, -7f),
                        center + new Vector2(-1f, 1f));
                    break;
            }
            Handles.EndGUI();
        }

        private static void DrawWaveIcon(Rect rect, RetroWaveType waveType, Color color)
        {
            Vector3[] points = new Vector3[waveType == RetroWaveType.Square ? 8 : 25];
            for (int index = 0; index < points.Length; index++)
            {
                float normalized = index / (float)(points.Length - 1);
                float sample;
                switch (waveType)
                {
                    case RetroWaveType.Saw:
                        sample = normalized * 2f - 1f;
                        break;
                    case RetroWaveType.Sine:
                        sample = Mathf.Sin(normalized * Mathf.PI * 2f);
                        break;
                    case RetroWaveType.Noise:
                        sample = Mathf.Sin(index * 12.9898f) * 0.75f;
                        break;
                    default:
                        sample = normalized < 0.25f || normalized >= 0.75f ? -0.75f : 0.75f;
                        break;
                }

                points[index] = new Vector3(
                    Mathf.Lerp(rect.x, rect.xMax, normalized),
                    rect.center.y - sample * rect.height * 0.42f);
            }

            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(1.8f, points);
            Handles.EndGUI();
        }

        private static void DrawKnob(Rect rect, float normalizedValue, bool active)
        {
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.43f;
            DrawArc(center, radius + 4f, 135f, 405f, BorderStrong, 2f);
            DrawArc(center, radius + 4f, 135f, Mathf.Lerp(135f, 405f, normalizedValue), Accent, 2.8f);

            Handles.BeginGUI();
            Handles.color = active ? DansToolboxTheme.Current.Hover : PanelRaised;
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Handles.color = active ? Accent : BorderStrong;
            Handles.DrawWireDisc(center, Vector3.forward, radius);

            float angle = Mathf.Lerp(135f, 405f, normalizedValue) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Handles.color = active ? Signal : Text;
            Handles.DrawAAPolyLine(
                2.2f,
                center + direction * 5f,
                center + direction * (radius - 5f));
            Handles.EndGUI();
        }

        private static void DrawArc(
            Vector2 center,
            float radius,
            float startDegrees,
            float endDegrees,
            Color color,
            float width)
        {
            const int segmentCount = 32;
            Vector3[] points = new Vector3[segmentCount + 1];
            for (int index = 0; index <= segmentCount; index++)
            {
                float angle = Mathf.Lerp(startDegrees, endDegrees, index / (float)segmentCount) * Mathf.Deg2Rad;
                points[index] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(width, points);
            Handles.EndGUI();
        }

        private static void DrawGraphGrid(Rect rect)
        {
            Color gridColor = new Color(Border.r, Border.g, Border.b, 0.38f);
            for (int index = 1; index < 4; index++)
            {
                float x = Mathf.Round(rect.x + rect.width * index / 4f);
                EditorGUI.DrawRect(new Rect(x, rect.y + 1f, 1f, rect.height - 2f), gridColor);
            }
            for (int index = 1; index < 3; index++)
            {
                float y = Mathf.Round(rect.y + rect.height * index / 3f);
                EditorGUI.DrawRect(new Rect(rect.x + 1f, y, rect.width - 2f, 1f), gridColor);
            }
        }

        internal static string FormatParameterValue(
            float value,
            string valueFormat)
        {
            return FormatValue(value, valueFormat);
        }

        private static string FormatValue(float value, string valueFormat)
        {
            if (valueFormat == "Hz")
            {
                return value >= 1000f ? $"{value / 1000f:0.00} kHz" : $"{value:0} Hz";
            }
            if (valueFormat == "Hz1")
            {
                return $"{value:0.0} Hz";
            }
            if (valueFormat == "s")
            {
                return value < 1f ? $"{value * 1000f:0} ms" : $"{value:0.00} s";
            }
            if (valueFormat == "%")
            {
                return $"{value * 100f:0}%";
            }
            if (valueFormat == "st")
            {
                return $"{value:+0.0;-0.0;0} st";
            }
            if (valueFormat == "Hz/s")
            {
                return $"{value:+0;-0;0}";
            }
            if (valueFormat == "dB")
            {
                return $"{value:+0.0;-0.0;0.0} dB";
            }
            if (valueFormat == "ratio")
            {
                return $"{value:0.0}:1";
            }
            if (valueFormat == "x")
            {
                return $"{value:0.0}×";
            }
            return value.ToString(valueFormat);
        }
    }
}
