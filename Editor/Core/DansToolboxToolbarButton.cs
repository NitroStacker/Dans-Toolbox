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
        internal const string ElementPath = "Dans Toolbox/Setup Wizard";
        private const string Tooltip = "Open Dans Toolbox Setup Wizard";

        [MainToolbarElement(
            ElementPath,
            defaultDockPosition = MainToolbarDockPosition.Left,
            defaultDockIndex = 2)]
        internal static MainToolbarElement Create()
        {
            return new MainToolbarButton(
                new MainToolbarContent(LoadIcon(), Tooltip),
                DansToolboxSetupWizard.Open);
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
    internal static class DansToolboxToolbarVisibility
    {
        private const string MigrationVersion = "1.2.1";
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
