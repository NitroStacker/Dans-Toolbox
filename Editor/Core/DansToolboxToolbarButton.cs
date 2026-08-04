using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal static class DansToolboxToolbarButton
    {
        internal const string ElementPath = "Dans Toolbox/Toolbox Hub";

        [MainToolbarElement(
            ElementPath,
            defaultDockPosition = MainToolbarDockPosition.Left,
            defaultDockIndex = 2)]
        internal static MainToolbarElement Create()
        {
            return new MainToolbarButton(
                new MainToolbarContent(
                    DansToolboxToolbarUpdateIndicator.GetToolbarIcon(),
                    DansToolboxUpdateService.ToolbarTooltip),
                DansToolboxHubWindow.Open);
        }

        internal static Texture2D LoadIcon()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DansToolboxToolbarButton).Assembly);
            if (package != null)
            {
                string iconPath = $"Packages/{package.name}/Editor/Icons/toolbox.png";
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                if (icon != null)
                {
                    return icon;
                }
            }

            return EditorGUIUtility.IconContent("Settings").image as Texture2D;
        }
    }

    [InitializeOnLoad]
    internal static class DansToolboxToolbarUpdateIndicator
    {
        private const double PulseSeconds = 1.6d;
        private const double RefreshSeconds = 1d / 12d;
        private static readonly Color32 UpdateOrange = new Color32(255, 132, 28, 255);

        private static Texture2D pulseIcon;
        private static Color32[] sourcePixels;
        private static Color32[] pulsePixels;
        private static double nextRefresh;

        static DansToolboxToolbarUpdateIndicator()
        {
            DansToolboxUpdateService.Changed -= OnUpdateStateChanged;
            DansToolboxUpdateService.Changed += OnUpdateStateChanged;
            EditorApplication.delayCall += OnUpdateStateChanged;
        }

        internal static Texture2D GetToolbarIcon()
        {
            Texture2D source = DansToolboxToolbarButton.LoadIcon();
            if (!DansToolboxUpdateService.UpdateAvailable || source == null)
            {
                return source;
            }

            EnsurePulseIcon(source);
            UpdatePulseIcon(EditorApplication.timeSinceStartup);
            return pulseIcon != null ? pulseIcon : source;
        }

        internal static float CalculatePulseOpacity(double elapsedSeconds)
        {
            double normalized = elapsedSeconds / PulseSeconds;
            float wave = 0.5f + 0.5f * Mathf.Sin((float)(normalized * Math.PI * 2d));
            return Mathf.Lerp(0.46f, 1f, wave);
        }

        private static void OnUpdateStateChanged()
        {
            EditorApplication.update -= Animate;
            if (DansToolboxUpdateService.UpdateAvailable)
            {
                nextRefresh = 0d;
                EditorApplication.update += Animate;
            }

            MainToolbar.Refresh(DansToolboxToolbarButton.ElementPath);
        }

        private static void Animate()
        {
            if (!DansToolboxUpdateService.UpdateAvailable)
            {
                EditorApplication.update -= Animate;
                MainToolbar.Refresh(DansToolboxToolbarButton.ElementPath);
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < nextRefresh)
            {
                return;
            }

            nextRefresh = now + RefreshSeconds;
            UpdatePulseIcon(now);
            MainToolbar.Refresh(DansToolboxToolbarButton.ElementPath);
        }

        private static void EnsurePulseIcon(Texture2D source)
        {
            if (pulseIcon != null &&
                pulseIcon.width == source.width &&
                pulseIcon.height == source.height)
            {
                return;
            }

            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                pulseIcon = new Texture2D(
                    source.width,
                    source.height,
                    TextureFormat.RGBA32,
                    false,
                    false)
                {
                    name = "Dans Toolbox Update Pulse",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = source.filterMode,
                    wrapMode = TextureWrapMode.Clamp
                };
                pulseIcon.ReadPixels(
                    new Rect(0f, 0f, source.width, source.height),
                    0,
                    0,
                    false);
                pulseIcon.Apply(false, false);
                sourcePixels = pulseIcon.GetPixels32();
                pulsePixels = new Color32[sourcePixels.Length];
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static void UpdatePulseIcon(double now)
        {
            if (pulseIcon == null || sourcePixels == null || pulsePixels == null)
            {
                return;
            }

            float opacity = CalculatePulseOpacity(now);
            for (int index = 0; index < sourcePixels.Length; index++)
            {
                Color32 source = sourcePixels[index];
                pulsePixels[index] = new Color32(
                    UpdateOrange.r,
                    UpdateOrange.g,
                    UpdateOrange.b,
                    (byte)Mathf.RoundToInt(source.a * opacity));
            }

            pulseIcon.SetPixels32(pulsePixels);
            pulseIcon.Apply(false, false);
        }
    }

    [InitializeOnLoad]
    internal static class DansToolboxToolbarVisibility
    {
        private const string MigrationVersion = "1.14.0";
        private const double RetrySeconds = 10d;
        private static double retryDeadline;

        static DansToolboxToolbarVisibility()
        {
            EditorApplication.delayCall += BeginMigration;
        }

        [MenuItem("Tools/Dans Toolbox/Show Toolbar Icon", false, -90)]
        private static void ShowFromMenu()
        {
            BeginShow(true);
        }

        private static void BeginMigration()
        {
            MainToolbar.Refresh(DansToolboxToolbarButton.ElementPath);
            if (EditorPrefs.GetBool(MigrationKey, false))
            {
                return;
            }

            BeginShow(false);
        }

        private static void BeginShow(bool force)
        {
            if (force)
            {
                EditorPrefs.DeleteKey(MigrationKey);
            }

            retryDeadline = EditorApplication.timeSinceStartup + RetrySeconds;
            EditorApplication.update -= TryShow;
            EditorApplication.update += TryShow;
            TryShow();
        }

        private static void TryShow()
        {
            MainToolbar.Refresh(DansToolboxToolbarButton.ElementPath);
            if (TrySetDisplayed())
            {
                EditorPrefs.SetBool(MigrationKey, true);
                StopRetrying();
                return;
            }

            if (EditorApplication.timeSinceStartup >= retryDeadline)
            {
                StopRetrying();
            }
        }

        internal static bool TrySetDisplayed()
        {
            EditorWindow toolbarWindow = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => string.Equals(
                    window.GetType().Name,
                    "MainToolbarWindow",
                    StringComparison.Ordinal));
            Overlay overlay = toolbarWindow?.overlayCanvas?.overlays
                .FirstOrDefault(candidate => string.Equals(
                    candidate.id,
                    DansToolboxToolbarButton.ElementPath,
                    StringComparison.Ordinal));
            if (overlay == null)
            {
                return false;
            }

            overlay.displayed = true;
            MainToolbar.Refresh(DansToolboxToolbarButton.ElementPath);
            return true;
        }

        private static void StopRetrying()
        {
            EditorApplication.update -= TryShow;
        }

        private static string MigrationKey =>
            $"DansToolbox.ToolbarVisibility.{MigrationVersion}.{Application.dataPath}";
    }
}
