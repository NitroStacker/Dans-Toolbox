using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal sealed class DansToolboxInstallSuccessOverlay : EditorWindow
    {
        private const double Duration = 2.35d;
        private static bool showPending;
        private static double showAt;
        private static DansToolboxThemeId pendingTheme;

        [SerializeField] private DansToolboxThemeId theme;
        [SerializeField] private double startedAt;

        [System.NonSerialized] private Texture2D backdrop;
        [System.NonSerialized] private GUIStyle successStyle;

        internal static void ShowAfter(DansToolboxThemeId selectedTheme, double delay)
        {
            pendingTheme = selectedTheme;
            showAt = EditorApplication.timeSinceStartup + Mathf.Max(0f, (float)delay);
            showPending = true;
            EditorApplication.update -= OpenWhenReady;
            EditorApplication.update += OpenWhenReady;
        }

        private static void OpenWhenReady()
        {
            if (!showPending)
            {
                EditorApplication.update -= OpenWhenReady;
                return;
            }

            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < showAt)
            {
                return;
            }

            showPending = false;
            EditorApplication.update -= OpenWhenReady;
            foreach (DansToolboxInstallSuccessOverlay existing in
                     Resources.FindObjectsOfTypeAll<DansToolboxInstallSuccessOverlay>())
            {
                existing.Close();
            }

            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            DansToolboxInstallSuccessOverlay window =
                CreateInstance<DansToolboxInstallSuccessOverlay>();
            window.titleContent = new GUIContent("Dans Toolbox Installed");
            window.theme = pendingTheme;
            window.backdrop = DansToolboxEditorBackdrop.CaptureBlurred();
            window.position = mainWindow;
            window.minSize = mainWindow.size;
            window.maxSize = mainWindow.size;
            window.startedAt = EditorApplication.timeSinceStartup;
            window.ShowPopup();
            window.Focus();
        }

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= CloseForReload;
            AssemblyReloadEvents.beforeAssemblyReload += CloseForReload;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= CloseForReload;
            if (backdrop != null)
            {
                DestroyImmediate(backdrop);
                backdrop = null;
            }
        }

        private void CloseForReload()
        {
            Close();
        }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup - startedAt >= Duration)
            {
                Close();
                return;
            }

            Repaint();
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

            float progress = Mathf.Clamp01((float)(
                (EditorApplication.timeSinceStartup - startedAt) / Duration));
            float opacity = DansToolboxSetupWizard.CalculateInstallOverlayOpacity(progress);
            DansToolboxPalette palette = DansToolboxTheme.GetPalette(theme);
            Rect canvas = new Rect(0f, 0f, position.width, position.height);
            DrawBackdrop(canvas, palette, opacity);
            DrawSuccess(canvas, palette, progress, opacity);
        }

        private void DrawBackdrop(Rect canvas, DansToolboxPalette palette, float opacity)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, opacity);
            if (backdrop != null)
            {
                GUI.DrawTexture(canvas, backdrop, ScaleMode.StretchToFill, false);
            }
            else
            {
                Color fallback = palette.Inset;
                fallback.a *= opacity;
                EditorGUI.DrawRect(canvas, fallback);
            }

            GUI.color = previousColor;
            EditorGUI.DrawRect(
                canvas,
                new Color(0.015f, 0.02f, 0.028f, 0.72f * opacity));
        }

        private void DrawSuccess(
            Rect canvas,
            DansToolboxPalette palette,
            float progress,
            float opacity)
        {
            float iconProgress = Mathf.Clamp01((progress - 0.055f) / 0.38f);
            float iconScale = DansToolboxSetupWizard.CalculateInstallIconScale(iconProgress);
            float iconSize = 92f * Mathf.Max(0f, iconScale);
            Rect iconRect = new Rect(
                canvas.center.x - iconSize * 0.5f,
                canvas.center.y - 112f - iconSize * 0.5f,
                iconSize,
                iconSize);

            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, opacity);
            if (iconSize > 0.5f)
            {
                Color border = palette.Accent;
                border.a *= opacity;
                Color fill = palette.AccentSoft;
                fill.a *= opacity;
                EditorGUI.DrawRect(iconRect, border);
                EditorGUI.DrawRect(
                    new Rect(
                        iconRect.x + 2f,
                        iconRect.y + 2f,
                        Mathf.Max(0f, iconRect.width - 4f),
                        Mathf.Max(0f, iconRect.height - 4f)),
                    fill);
                Color check = palette.Text;
                check.a *= opacity;
                DansToolboxSetupWizard.DrawCheck(iconRect, check);
            }

            float textProgress = EaseOutCubic(Mathf.Clamp01((progress - 0.2f) / 0.24f));
            GUI.color = new Color(1f, 1f, 1f, opacity * textProgress);
            successStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = palette.Text }
            };
            GUI.Label(
                new Rect(canvas.center.x - 260f, canvas.center.y + 2f, 520f, 54f),
                "Toolbox installed!",
                successStyle);
            GUI.color = previousColor;

            float railProgress = EaseOutCubic(Mathf.Clamp01((progress - 0.25f) / 0.28f));
            Color rail = palette.Accent;
            rail.a *= opacity * railProgress;
            float railWidth = 132f * railProgress;
            EditorGUI.DrawRect(
                new Rect(canvas.center.x - railWidth * 0.5f, canvas.center.y + 64f, railWidth, 2f),
                rail);
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }
    }
}
