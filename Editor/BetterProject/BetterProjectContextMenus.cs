using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterProject
{
    internal static class BetterProjectContextMenus
    {
        private const BindingFlags StaticInternal = BindingFlags.Static | BindingFlags.NonPublic;

        private static readonly MethodInfo GetMenuItemsMethod = typeof(Menu).GetMethod(
            "GetMenuItems",
            StaticInternal,
            null,
            new[] { typeof(string), typeof(bool), typeof(bool) },
            null);

        private static readonly MethodInfo GetHotkeyMethod = typeof(Menu).GetMethod(
            "GetHotkey",
            StaticInternal,
            null,
            new[] { typeof(string) },
            null);

        private static readonly MethodInfo GetEnabledWithContextMethod = typeof(Menu).GetMethod(
            "GetEnabledWithContext",
            StaticInternal,
            null,
            new[] { typeof(string), typeof(UnityEngine.Object[]) },
            null);

        private static readonly MethodInfo ExecuteWithContextMethod = typeof(EditorApplication).GetMethod(
            "ExecuteMenuItemWithTemporaryContext",
            StaticInternal,
            null,
            new[] { typeof(string), typeof(UnityEngine.Object[]) },
            null);

        private static readonly FieldInfo LastProjectBrowserField = typeof(EditorWindow).Assembly
            .GetType("UnityEditor.ProjectBrowser")
            ?.GetField(
                "s_LastInteractedProjectBrowser",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo EndProjectBrowserRenameMethod = typeof(EditorWindow).Assembly
            .GetType("UnityEditor.ProjectBrowser")
            ?.GetMethod(
                "EndRenaming",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

        internal static int AddUnityAssetItems(
            GenericMenu menu,
            UnityEngine.Object folderContext,
            Action<string> executeCreate = null)
        {
            if (menu == null || GetMenuItemsMethod == null)
            {
                return 0;
            }

            try
            {
                Array items = GetMenuItemsMethod.Invoke(
                    null,
                    new object[] { "Assets", true, false }) as Array;
                if (items == null || items.Length == 0)
                {
                    return 0;
                }

                Type itemType = items.GetType().GetElementType();
                PropertyInfo pathProperty = itemType?.GetProperty("path");
                PropertyInfo separatorProperty = itemType?.GetProperty("isSeparator");
                PropertyInfo priorityProperty = itemType?.GetProperty("priority");
                if (pathProperty == null || separatorProperty == null || priorityProperty == null)
                {
                    return 0;
                }

                int added = 0;
                int previousPriority = -1;
                foreach (object item in items)
                {
                    string fullPath = pathProperty.GetValue(item) as string;
                    bool isSeparator = (bool)separatorProperty.GetValue(item);
                    int priority = (int)priorityProperty.GetValue(item);
                    if (string.IsNullOrEmpty(fullPath) || isSeparator)
                    {
                        previousPriority = priority;
                        continue;
                    }

                    string displayPath = StripAssetsRoot(fullPath);
                    if (string.IsNullOrEmpty(displayPath))
                    {
                        previousPriority = priority;
                        continue;
                    }
                    if (priority >= 0 && priority > previousPriority + 10 && added > 0)
                    {
                        menu.AddSeparator(GetMenuDirectory(displayPath));
                    }

                    string hotkey = GetHotkeyMethod?.Invoke(null, new object[] { fullPath }) as string;
                    GUIContent content = new GUIContent(string.IsNullOrEmpty(hotkey)
                        ? displayPath
                        : displayPath + " " + hotkey);
                    bool enabled = IsEnabled(fullPath, folderContext);
                    string capturedPath = fullPath;
                    if (enabled)
                    {
                        menu.AddItem(
                            content,
                            Menu.GetChecked(fullPath),
                            () =>
                            {
                                if (executeCreate != null && IsCreateItem(capturedPath))
                                {
                                    executeCreate(capturedPath);
                                }
                                else
                                {
                                    Execute(capturedPath, folderContext);
                                }
                            });
                    }
                    else
                    {
                        menu.AddDisabledItem(content, Menu.GetChecked(fullPath));
                    }
                    added++;
                    previousPriority = priority;
                }
                return added;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Better Project could not read Unity's Assets menu: " + exception.Message);
                return 0;
            }
        }

        internal static string StripAssetsRoot(string path)
        {
            const string root = "Assets/";
            return path != null && path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(root.Length)
                : path ?? string.Empty;
        }

        internal static bool IsCreateItem(string menuPath)
        {
            return menuPath != null &&
                   menuPath.StartsWith("Assets/Create/", StringComparison.OrdinalIgnoreCase);
        }

        internal static void ExecuteCreateAndReturnControl(
            string menuPath,
            UnityEngine.Object context,
            Action completed)
        {
            object projectBrowser = LastProjectBrowserField?.GetValue(null);
            bool executed = Execute(menuPath, context);
            if (executed && projectBrowser != null && EndProjectBrowserRenameMethod != null)
            {
                EndProjectBrowserRenameMethod.Invoke(projectBrowser, null);
            }

            EditorApplication.delayCall += () => completed?.Invoke();
        }

        private static bool IsEnabled(string menuPath, UnityEngine.Object context)
        {
            if (context != null && GetEnabledWithContextMethod != null)
            {
                return (bool)GetEnabledWithContextMethod.Invoke(
                    null,
                    new object[] { menuPath, new[] { context } });
            }
            return Menu.GetEnabled(menuPath);
        }

        private static bool Execute(string menuPath, UnityEngine.Object context)
        {
            if (context != null && ExecuteWithContextMethod != null)
            {
                return (bool)ExecuteWithContextMethod.Invoke(
                    null,
                    new object[] { menuPath, new[] { context } });
            }
            return EditorApplication.ExecuteMenuItem(menuPath);
        }

        private static string GetMenuDirectory(string displayPath)
        {
            int separator = displayPath.LastIndexOf('/');
            return separator >= 0 ? displayPath.Substring(0, separator + 1) : string.Empty;
        }
    }
}
