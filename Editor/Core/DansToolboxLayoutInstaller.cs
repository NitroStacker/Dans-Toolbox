using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal static class DansToolboxLayoutInstaller
    {
        private static readonly Type[] LayoutLoadSignature =
        {
            typeof(string),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool)
        };

        internal static string RecommendedLayoutPath
        {
            get
            {
                UnityEditor.PackageManager.PackageInfo package =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                        typeof(DansToolboxLayoutInstaller).Assembly);
                return package == null || string.IsNullOrEmpty(package.resolvedPath)
                    ? string.Empty
                    : Path.Combine(
                        package.resolvedPath,
                        "Editor",
                        "Layouts",
                        "Toolbox.wlt");
            }
        }

        internal static bool IsLayoutAvailable =>
            File.Exists(RecommendedLayoutPath);

        internal static void ApplyRecommendedLayout()
        {
            EditorApplication.delayCall += () => ApplyRecommendedLayoutNow();
        }

        internal static bool ApplyRecommendedLayoutNow()
        {
            bool loaded = TryLoadRecommendedLayout();
            EditorApplication.delayCall += CloseDisabledToolWindows;
            return loaded;
        }

        private static bool TryLoadRecommendedLayout()
        {
            string path = RecommendedLayoutPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning(
                    "Dans Toolbox could not find its packaged Toolbox layout.");
                return false;
            }

            try
            {
                Type windowLayout = typeof(EditorWindow).Assembly.GetType(
                    "UnityEditor.WindowLayout",
                    true);
                const BindingFlags flags =
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic;
                MethodInfo load = windowLayout.GetMethod(
                                      "TryLoadWindowLayout",
                                      flags,
                                      null,
                                      LayoutLoadSignature,
                                      null) ??
                                  windowLayout.GetMethod(
                                      "LoadWindowLayout",
                                      flags,
                                      null,
                                      LayoutLoadSignature,
                                      null);
                if (load == null)
                {
                    Debug.LogError(
                        "Dans Toolbox could not access Unity's layout loader.");
                    return false;
                }

                object result = load.Invoke(
                    null,
                    new object[]
                    {
                        path,
                        false,
                        true,
                        true,
                        true
                    });
                return !(result is bool success) || success;
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception);
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
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
