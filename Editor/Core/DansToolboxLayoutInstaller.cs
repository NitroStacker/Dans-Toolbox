using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal static class DansToolboxLayoutInstaller
    {
        // The organized workspace does not depend on a serialized Unity layout.
        internal static bool IsLayoutAvailable => true;

        internal static void ApplyRecommendedLayout()
        {
            EditorApplication.delayCall += () => ApplyRecommendedLayoutNow();
        }

        internal static bool ApplyRecommendedLayoutNow()
        {
            // Organized is a launcher preference, not a serialized Unity layout.
            // Never close docked windows here: removing the only tab from a dock
            // collapses that region and causes Unity's center view to expand.
            EditorApplication.delayCall += CloseDisabledToolWindows;
            return true;
        }

        internal static void CloseDisabledToolWindows()
        {
            bool retroEnabled = DansToolboxSettings.IsToolEnabled(
                DansToolboxTools.RetroSfxId);
            bool retroVfxEnabled = DansToolboxSettings.IsToolEnabled(
                DansToolboxTools.RetroVfxId);
            bool dockEnabled = DansToolboxSettings.IsToolEnabled(
                DansToolboxTools.NativeWindowDockId);
            bool hierarchyEnabled = DansToolboxSettings.IsToolEnabled(
                DansToolboxTools.BetterHierarchyId);
            bool inspectorEnabled = DansToolboxSettings.IsToolEnabled(
                DansToolboxTools.BetterInspectorId);
            bool projectEnabled = DansToolboxSettings.IsToolEnabled(
                DansToolboxTools.BetterProjectId);
            bool consoleEnabled = DansToolboxSettings.IsToolEnabled(
                DansToolboxTools.BetterConsoleId);
            bool sceneEnabled = DansToolboxSettings.IsToolEnabled(
                DansToolboxTools.BetterSceneId);

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                string fullName = window.GetType().FullName;
                if ((!retroEnabled &&
                     fullName == "DansToolbox.EditorTools.Audio.RetroSfxGeneratorWindow") ||
                    (!retroVfxEnabled &&
                     fullName == "DansToolbox.EditorTools.RetroVfx.RetroVfxGeneratorWindow") ||
                    (!dockEnabled &&
                     fullName == "DansToolbox.EditorTools.NativeWindowDock.NativeWindowDockWindow") ||
                    (!hierarchyEnabled &&
                     fullName == "DansToolbox.EditorTools.BetterHierarchy.BetterHierarchyWindow") ||
                    (!inspectorEnabled &&
                     fullName == "DansToolbox.EditorTools.BetterInspector.BetterInspectorWindow") ||
                    (!projectEnabled &&
                     fullName == "DansToolbox.EditorTools.BetterProject.BetterProjectWindow") ||
                    (!consoleEnabled &&
                     fullName == "DansToolbox.EditorTools.BetterConsole.BetterConsoleWindow") ||
                    (!sceneEnabled &&
                     fullName == "DansToolbox.EditorTools.BetterScene.BetterSceneWindow"))
                {
                    window.Close();
                }
            }
        }
    }
}
