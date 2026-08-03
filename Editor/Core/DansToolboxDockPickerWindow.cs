using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal sealed class DansToolboxDockPickerWindow : EditorWindow
    {
        private const float HeaderHeight = 72f;
        private const float FooterHeight = 44f;

        [NonSerialized] private IReadOnlyList<DansToolboxDockTarget> targets;
        [NonSerialized] private Action<DansToolboxDockTarget> dockSelected;
        [NonSerialized] private Action floatingSelected;
        [NonSerialized] private Texture2D backdrop;
        [NonSerialized] private Rect mainScreenRect;
        [NonSerialized] private int hoveredIndex = -1;
        [NonSerialized] private PickerStyles styles;
        [NonSerialized] private int styledThemeRevision = -1;

        internal static void Open(
            Action<DansToolboxDockTarget> onDockSelected,
            Action onFloatingSelected)
        {
            foreach (DansToolboxDockPickerWindow existing in
                     Resources.FindObjectsOfTypeAll<DansToolboxDockPickerWindow>())
            {
                existing.Close();
            }

            Rect main = EditorGUIUtility.GetMainWindowPosition();
            DansToolboxDockPickerWindow window = CreateInstance<DansToolboxDockPickerWindow>();
            window.titleContent = new GUIContent("Choose Dock Position");
            window.targets = DansToolboxDocking.DiscoverTargets();
            window.dockSelected = onDockSelected;
            window.floatingSelected = onFloatingSelected;
            window.backdrop = DansToolboxEditorBackdrop.CaptureBlurred();
            window.mainScreenRect = main;
            window.position = main;
            window.minSize = main.size;
            window.maxSize = main.size;
            window.wantsMouseMove = true;
            window.ShowPopup();
            window.Focus();
        }

        private void OnDisable()
        {
            if (backdrop != null)
            {
                DestroyImmediate(backdrop);
                backdrop = null;
            }
        }

        private void OnGUI()
        {
            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                Close();
                current.Use();
                return;
            }

            DansToolboxPalette palette = DansToolboxTheme.Current;
            EnsureStyles(palette);
            Rect canvas = new Rect(0f, 0f, position.width, position.height);
            UpdateHoveredTarget(current.type, current.mousePosition, canvas);
            if (backdrop != null)
            {
                GUI.DrawTexture(canvas, backdrop, ScaleMode.StretchToFill, false);
            }
            else
            {
                EditorGUI.DrawRect(canvas, palette.Canvas);
            }
            EditorGUI.DrawRect(canvas, new Color(0.015f, 0.02f, 0.028f, 0.74f));

            DrawHeader(canvas, palette);
            if (targets == null || targets.Count == 0)
            {
                DrawUnavailable(canvas, palette);
            }
            else
            {
                DrawTargets(canvas, palette);
            }
            DrawFooter(canvas, palette);
        }

        private void DrawHeader(Rect canvas, DansToolboxPalette palette)
        {
            Rect header = new Rect(0f, 0f, canvas.width, HeaderHeight);
            EditorGUI.DrawRect(header, new Color(
                palette.Panel.r,
                palette.Panel.g,
                palette.Panel.b,
                0.96f));
            EditorGUI.DrawRect(
                new Rect(0f, header.yMax - 1f, header.width, 1f),
                palette.BorderStrong);
            GUI.Label(
                new Rect(24f, 12f, header.width - 48f, 26f),
                "Choose a dock region",
                styles.Title);
            GUI.Label(
                new Rect(24f, 38f, header.width - 48f, 20f),
                "Select a numbered Unity pane. The new Native Dock panel becomes a tab there.",
                styles.Body);
        }

        private void DrawTargets(Rect canvas, DansToolboxPalette palette)
        {
            Vector2 mouse = Event.current.mousePosition;
            for (int index = 0; index < targets.Count; index++)
            {
                DansToolboxDockTarget target = targets[index];
                Rect rect = ToLocal(target.ScreenRect);
                rect = ClipToCanvas(rect, canvas);
                if (rect.width < 40f || rect.height < 40f)
                {
                    continue;
                }

                bool pointerOver = rect.Contains(mouse);
                bool hovered = hoveredIndex == index;

                Color fill = hovered
                    ? Color.Lerp(palette.Panel, palette.Accent, 0.24f)
                    : Color.Lerp(palette.Canvas, palette.Panel, 0.58f);
                Color border = hovered ? palette.Accent : new Color(
                    palette.BorderStrong.r,
                    palette.BorderStrong.g,
                    palette.BorderStrong.b,
                    0.9f);
                DrawFrame(rect, fill, border, hovered ? 3f : 2f);

                float badgeSize = Mathf.Clamp(
                    Mathf.Min(rect.width, rect.height) * 0.22f,
                    46f,
                    68f);
                Rect badge = new Rect(
                    rect.center.x - badgeSize * 0.5f,
                    rect.center.y - badgeSize * 0.5f - 10f,
                    badgeSize,
                    badgeSize);
                DrawFrame(
                    badge,
                    hovered ? palette.Accent : palette.Raised,
                    hovered ? palette.Signal : palette.BorderStrong,
                    2f);
                GUI.Label(badge, (index + 1).ToString(), hovered ? styles.NumberHot : styles.Number);
                GUI.Label(
                    new Rect(rect.x + 10f, badge.yMax + 7f, rect.width - 20f, 24f),
                    target.Label,
                    hovered ? styles.TargetHot : styles.Target);

                if (Event.current.type == EventType.MouseUp &&
                    Event.current.button == 0 && pointerOver)
                {
                    SelectDock(target);
                    Event.current.Use();
                    return;
                }
            }
        }

        private void UpdateHoveredTarget(
            EventType inputEventType,
            Vector2 mousePosition,
            Rect canvas)
        {
            if (inputEventType == EventType.MouseLeaveWindow)
            {
                if (hoveredIndex != -1)
                {
                    hoveredIndex = -1;
                    Repaint();
                }
                return;
            }

            if (inputEventType != EventType.MouseMove &&
                inputEventType != EventType.MouseEnterWindow)
            {
                return;
            }

            int nextHoveredIndex = -1;
            if (targets != null)
            {
                for (int index = 0; index < targets.Count; index++)
                {
                    Rect rect = ClipToCanvas(ToLocal(targets[index].ScreenRect), canvas);
                    if (rect.width >= 40f &&
                        rect.height >= 40f &&
                        rect.Contains(mousePosition))
                    {
                        nextHoveredIndex = index;
                        break;
                    }
                }
            }

            if (nextHoveredIndex == hoveredIndex)
            {
                return;
            }

            hoveredIndex = nextHoveredIndex;
            Repaint();
        }

        private void DrawUnavailable(Rect canvas, DansToolboxPalette palette)
        {
            Rect panel = new Rect(
                canvas.center.x - 230f,
                canvas.center.y - 74f,
                460f,
                148f);
            DrawFrame(panel, palette.Panel, palette.BorderStrong, 1f);
            GUI.Label(
                new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, 28f),
                "No dock regions are available",
                styles.EmptyTitle);
            GUI.Label(
                new Rect(panel.x + 24f, panel.y + 50f, panel.width - 48f, 40f),
                "Open a normal Unity pane first, or use a temporary floating panel.",
                styles.EmptyBody);
            Rect floating = new Rect(panel.center.x - 74f, panel.yMax - 40f, 148f, 28f);
            DrawFrame(floating, palette.Raised, palette.Accent, 1f);
            GUI.Label(floating, "OPEN FLOATING", styles.Button);
            if (GUI.Button(floating, GUIContent.none, GUIStyle.none))
            {
                SelectFloating();
            }
        }

        private void DrawFooter(Rect canvas, DansToolboxPalette palette)
        {
            Rect footer = new Rect(
                0f,
                canvas.yMax - FooterHeight,
                canvas.width,
                FooterHeight);
            EditorGUI.DrawRect(footer, new Color(
                palette.Panel.r,
                palette.Panel.g,
                palette.Panel.b,
                0.94f));
            EditorGUI.DrawRect(new Rect(0f, footer.y, footer.width, 1f), palette.Border);
            GUI.Label(
                new Rect(20f, footer.y, footer.width - 40f, footer.height),
                hoveredIndex >= 0
                    ? "CLICK TO DOCK  /  ESC CANCELS"
                    : "MOVE OVER A NUMBERED REGION  /  ESC CANCELS",
                styles.Footer);
        }

        private void SelectDock(DansToolboxDockTarget target)
        {
            Action<DansToolboxDockTarget> callback = dockSelected;
            dockSelected = null;
            floatingSelected = null;
            Close();
            if (callback != null)
            {
                EditorApplication.delayCall += () => callback(target);
            }
        }

        private void SelectFloating()
        {
            Action callback = floatingSelected;
            dockSelected = null;
            floatingSelected = null;
            Close();
            if (callback != null)
            {
                EditorApplication.delayCall += () => callback();
            }
        }

        private Rect ToLocal(Rect screenRect)
        {
            return new Rect(
                screenRect.x - mainScreenRect.x,
                screenRect.y - mainScreenRect.y,
                screenRect.width,
                screenRect.height);
        }

        private static Rect ClipToCanvas(Rect rect, Rect canvas)
        {
            float xMin = Mathf.Max(rect.xMin, canvas.xMin + 4f);
            float yMin = Mathf.Max(rect.yMin, HeaderHeight + 4f);
            float xMax = Mathf.Min(rect.xMax, canvas.xMax - 4f);
            float yMax = Mathf.Min(rect.yMax, canvas.yMax - FooterHeight - 4f);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void DrawFrame(
            Rect rect,
            Color fill,
            Color border,
            float thickness)
        {
            EditorGUI.DrawRect(rect, border);
            EditorGUI.DrawRect(
                new Rect(
                    rect.x + thickness,
                    rect.y + thickness,
                    Mathf.Max(0f, rect.width - thickness * 2f),
                    Mathf.Max(0f, rect.height - thickness * 2f)),
                fill);
        }

        private void EnsureStyles(DansToolboxPalette palette)
        {
            if (styles != null && styledThemeRevision == DansToolboxTheme.Revision)
            {
                return;
            }

            styledThemeRevision = DansToolboxTheme.Revision;
            styles = new PickerStyles
            {
                Title = Label(palette.Text, 18, FontStyle.Bold),
                Body = Label(palette.Muted, 11),
                Number = Label(palette.Text, 24, FontStyle.Bold, TextAnchor.MiddleCenter),
                NumberHot = Label(Color.black, 24, FontStyle.Bold, TextAnchor.MiddleCenter),
                Target = Label(palette.Text, 10, FontStyle.Bold, TextAnchor.MiddleCenter),
                TargetHot = Label(palette.Signal, 10, FontStyle.Bold, TextAnchor.MiddleCenter),
                EmptyTitle = Label(palette.Text, 15, FontStyle.Bold, TextAnchor.MiddleCenter),
                EmptyBody = Label(palette.Muted, 10, FontStyle.Normal, TextAnchor.UpperCenter, true),
                Button = Label(palette.Text, 9, FontStyle.Bold, TextAnchor.MiddleCenter),
                Footer = Label(palette.Muted, 9, FontStyle.Bold, TextAnchor.MiddleCenter)
            };
        }

        private static GUIStyle Label(
            Color color,
            int size,
            FontStyle fontStyle = FontStyle.Normal,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            bool wordWrap = false)
        {
            return new GUIStyle(EditorStyles.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                alignment = alignment,
                wordWrap = wordWrap,
                clipping = TextClipping.Clip,
                normal = { textColor = color }
            };
        }

        private sealed class PickerStyles
        {
            internal GUIStyle Title;
            internal GUIStyle Body;
            internal GUIStyle Number;
            internal GUIStyle NumberHot;
            internal GUIStyle Target;
            internal GUIStyle TargetHot;
            internal GUIStyle EmptyTitle;
            internal GUIStyle EmptyBody;
            internal GUIStyle Button;
            internal GUIStyle Footer;
        }
    }
}
