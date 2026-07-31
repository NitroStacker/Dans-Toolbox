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
        private const string LayoutRelativePath = "Editor/Layouts/ToolBox Layout.wlt";

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
            string path = GetLayoutPath();
            if (!File.Exists(path))
            {
                Debug.LogError("Dans Toolbox could not find its recommended layout.");
                return;
            }

            EditorApplication.delayCall += () => LoadLayout(path);
        }

        private static void LoadLayout(string path)
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
                        return parameters.Length == 2 &&
                               parameters[0].ParameterType == typeof(string) &&
                               parameters[1].ParameterType == typeof(bool);
                    });
                if (loader == null)
                {
                    throw new MissingMethodException(
                        "UnityEditor.WindowLayout.TryLoadWindowLayout(string, bool)");
                }

                bool loaded = (bool)loader.Invoke(null, new object[] { path, false });
                if (!loaded)
                {
                    throw new InvalidOperationException("Unity rejected the layout file.");
                }

                EditorApplication.delayCall += CloseDisabledToolWindows;
            }
            catch (Exception exception)
            {
                Debug.LogError("Dans Toolbox could not apply ToolBox Layout: " +
                               Unwrap(exception).Message);
            }
        }

        internal static void CloseDisabledToolWindows()
        {
            bool retroEnabled = DansToolboxSettings.IsToolEnabled(
                DansToolboxTools.RetroSfxId);
            bool dockEnabled = DansToolboxSettings.IsToolEnabled(
                DansToolboxTools.NativeWindowDockId);

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                string fullName = window.GetType().FullName;
                if ((!retroEnabled &&
                     fullName == "DansToolbox.EditorTools.Audio.RetroSfxGeneratorWindow") ||
                    (!dockEnabled &&
                     fullName == "DansToolbox.EditorTools.NativeWindowDock.NativeWindowDockWindow"))
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
