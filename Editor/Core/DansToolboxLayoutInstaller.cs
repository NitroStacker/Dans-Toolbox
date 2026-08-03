using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal static class DansToolboxLayoutInstaller
    {
        private const string LayoutRelativePath = "Editor/Layouts/ToolBox.wlt";

        internal static string GetLayoutPath()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(DansToolboxLayoutInstaller).Assembly);
            return package == null
                ? string.Empty
                : Path.Combine(package.resolvedPath, LayoutRelativePath);
        }

        internal static bool IsLayoutAvailable => File.Exists(GetLayoutPath());

        internal static void ApplyRecommendedLayout()
        {
            EditorApplication.delayCall += () => ApplyRecommendedLayoutNow();
        }

        internal static bool ApplyRecommendedLayoutNow()
        {
            string path = GetLayoutPath();
            if (!File.Exists(path))
            {
                Debug.LogError("Dans Toolbox could not find its recommended layout.");
                return false;
            }

            return LoadLayout(path);
        }

        private static bool LoadLayout(string path)
        {
            try
            {
                Type windowLayoutType = typeof(EditorWindow).Assembly.GetType(
                    "UnityEditor.WindowLayout",
                    true);
                MethodInfo loader = windowLayoutType
                    .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(method =>
                    {
                        if (method.Name != "TryLoadWindowLayout")
                        {
                            return false;
                        }

                        ParameterInfo[] parameters = method.GetParameters();
                        return parameters.Length == 5 &&
                               parameters[0].ParameterType == typeof(string) &&
                               parameters[1].ParameterType == typeof(bool) &&
                               parameters[2].ParameterType == typeof(bool) &&
                               parameters[3].ParameterType == typeof(bool) &&
                               parameters[4].ParameterType == typeof(bool);
                    });
                if (loader == null)
                {
                    throw new MissingMethodException(
                        "UnityEditor.WindowLayout.TryLoadWindowLayout(string, bool, bool, bool, bool)");
                }

                // KeepMainWindow is critical here. Without it Unity tears down and
                // recreates its native main window, which visibly minimizes and
                // maximizes the Editor during the layout swap.
                bool loaded = (bool)loader.Invoke(
                    null,
                    new object[] { path, false, false, true, true });
                if (!loaded)
                {
                    throw new InvalidOperationException("Unity rejected the layout file.");
                }

                EditorApplication.delayCall += CloseDisabledToolWindows;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Dans Toolbox could not apply ToolBox: " +
                               Unwrap(exception).Message);
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

        private static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException invocation &&
                   invocation.InnerException != null)
            {
                exception = invocation.InnerException;
            }

            return exception;
        }
    }
}
