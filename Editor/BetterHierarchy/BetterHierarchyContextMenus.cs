using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal static class BetterHierarchyContextMenus
    {
        private const BindingFlags StaticInternal = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags InstanceInternal =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static EditorWindow nativeHierarchyWindow;
        private static object nativeHierarchy;
        private static MethodInfo createGameObjectContextMethod;
        private static MethodInfo nativePrefabMenuMethod;
        private static MethodInfo hierarchySelectionChangedMethod;
        private static MethodInfo hierarchySyncMethod;

        private static readonly PropertyInfo GenericMenuItemsProperty = typeof(GenericMenu).GetProperty(
            "menuItems",
            BindingFlags.Instance | BindingFlags.NonPublic);

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

        private static readonly MethodInfo AddPackageItemsMethod = typeof(SceneHierarchyHooks).GetMethod(
            "AddCustomGameObjectContextMenuItems",
            StaticInternal,
            null,
            new[] { typeof(GenericMenu), typeof(GameObject) },
            null);

        internal static int RegisteredGameObjectItemCount
        {
            get
            {
                try
                {
                    return (GetMenuItemsMethod?.Invoke(null, new object[] { "GameObject", true, false }) as Array)
                        ?.Length ?? 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        internal static void AddStandardObjectItems(
            GenericMenu menu,
            GameObject context,
            Action rename,
            Action delete,
            Action selectAll)
        {
            AddEditorCommand(menu, "Cut", "Edit/Cut");
            AddEditorCommand(menu, "Copy", "Edit/Copy");
            AddEditorCommand(menu, "Paste", "Edit/Paste");
            AddEditorCommand(menu, "Paste Special/Paste as Child (Keep Local Transform)",
                "Edit/Paste Special/Paste as Child (Keep Local Transform)");
            AddEditorCommand(menu, "Paste Special/Paste as Child (Keep World Transform)",
                "Edit/Paste Special/Paste as Child (Keep World Transform)");
            AddCallback(menu, "Rename", rename);
            AddEditorCommand(menu, "Duplicate", "Edit/Duplicate");
            AddCallback(menu, "Delete", delete);

            menu.AddSeparator(string.Empty);
            AddCallback(menu, "Select All", selectAll);
            if (Selection.objects.Length > 0)
            {
                menu.AddItem(new GUIContent("Deselect All"), false,
                    () => Selection.objects = Array.Empty<UnityEngine.Object>());
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Deselect All"));
            }
            menu.AddItem(new GUIContent("Invert Selection"), false, InvertSceneSelection);
            if (context != null && context.transform.childCount > 0)
            {
                menu.AddItem(new GUIContent("Select Children"), false, () =>
                {
                    Transform[] transforms = context.GetComponentsInChildren<Transform>(true);
                    UnityEngine.Object[] children = new UnityEngine.Object[Math.Max(0, transforms.Length - 1)];
                    for (int index = 1; index < transforms.Length; index++)
                    {
                        children[index - 1] = transforms[index].gameObject;
                    }
                    Selection.objects = children;
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Select Children"));
            }

            menu.AddSeparator(string.Empty);
            if (context != null && EnsureNativeHierarchy())
            {
                menu.AddItem(new GUIContent("Find References in Scene"), false,
                    () => InvokeNativeHierarchyCommand("FindReferenceInScene", context));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Find References in Scene"));
            }

            menu.AddSeparator(string.Empty);
            if (context != null && context.scene.IsValid() && EnsureNativeHierarchy())
            {
                menu.AddItem(new GUIContent("Set as Default Parent"), false,
                    () => InvokeNativeHierarchyCommand("SetDefaultParentObject", context, true, context));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Set as Default Parent"));
            }

            if (!AddNativePrefabItems(menu, context))
            {
                AddPrefabItems(menu, context);
            }
            menu.AddSeparator(string.Empty);
        }

        internal static int AddUnityHierarchyObjectItems(
            GenericMenu menu,
            GameObject context,
            Action rename,
            Action delete,
            Action selectAll,
            Action<GameObject> afterCreate = null)
        {
            if (menu == null)
            {
                return 0;
            }

            int startCount = menu.GetItemCount();
            AddStandardObjectItems(menu, context, rename, delete, selectAll);
            AddRegisteredGameObjectItems(
                menu,
                context,
                afterCreate,
                includePath: IsNativeHierarchyRegisteredPath,
                includeDisabled: true);
            AddPackageObjectItems(menu, context);
            menu.AddSeparator(string.Empty);
            if (context != null)
            {
                menu.AddItem(new GUIContent("Properties..."), false,
                    () => OpenPropertyEditor(context));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Properties..."));
            }
            return menu.GetItemCount() - startCount;
        }

        internal static int AddCompleteUnityObjectItems(
            GenericMenu menu,
            GameObject context,
            Action rename,
            Action delete)
        {
            if (menu == null || !EnsureNativeHierarchy())
            {
                return 0;
            }

            int startIndex = menu.GetItemCount();
            try
            {
                hierarchySelectionChangedMethod?.Invoke(nativeHierarchy, null);
                hierarchySyncMethod?.Invoke(nativeHierarchy, null);

                ParameterInfo contextParameter = createGameObjectContextMethod.GetParameters()[1];
                object contextId = GetHierarchyContextId(context, contextParameter.ParameterType);
                createGameObjectContextMethod.Invoke(nativeHierarchy, new[] { menu, contextId });

                PatchStockCallback(menu, startIndex, "Rename", rename);
                PatchStockCallback(menu, startIndex, "Delete", delete);
                return menu.GetItemCount() - startIndex;
            }
            catch
            {
                return 0;
            }
        }

        internal static int AddRegisteredGameObjectItems(
            GenericMenu menu,
            GameObject context,
            Action<GameObject> afterCreate = null,
            bool useRootContext = false,
            Func<string, bool> includePath = null,
            bool includeDisabled = false)
        {
            if (menu == null || GetMenuItemsMethod == null)
            {
                return 0;
            }

            try
            {
                Array items = GetMenuItemsMethod.Invoke(
                    null,
                    new object[] { "GameObject", true, false }) as Array;
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
                    if (string.IsNullOrEmpty(fullPath))
                    {
                        continue;
                    }

                    if (includePath != null && !includePath(fullPath))
                    {
                        previousPriority = priority;
                        continue;
                    }

                    string displayPath = StripGameObjectRoot(fullPath);
                    bool enabled = !isSeparator && IsMenuEnabled(fullPath, context);
                    if (!isSeparator && (enabled || includeDisabled))
                    {
                        if (priority >= 0 && priority > previousPriority + 10 && added > 0)
                        {
                            menu.AddSeparator(GetMenuDirectory(displayPath));
                        }

                        string hotkey = GetHotkeyMethod?.Invoke(null, new object[] { fullPath }) as string;
                        GUIContent content = new GUIContent(string.IsNullOrEmpty(hotkey)
                            ? displayPath
                            : displayPath + " " + hotkey);
                        string capturedPath = fullPath;
                        if (enabled)
                        {
                            menu.AddItem(content, Menu.GetChecked(fullPath),
                                () => ExecuteRegisteredItem(capturedPath, context, afterCreate, useRootContext));
                        }
                        else
                        {
                            menu.AddDisabledItem(content, Menu.GetChecked(fullPath));
                        }
                        added++;
                    }

                    previousPriority = priority;
                }

                return added;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Better Hierarchy could not read Unity's GameObject menu: " + exception.Message);
                return 0;
            }
        }

        internal static void AddPackageObjectItems(GenericMenu menu, GameObject context)
        {
            try
            {
                AddPackageItemsMethod?.Invoke(null, new object[] { menu, context });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Better Hierarchy could not add package context actions: " + exception.Message);
            }
        }

        internal static void MoveItemsToFront(GenericMenu menu, string pathPrefix)
        {
            if (string.IsNullOrEmpty(pathPrefix) ||
                GenericMenuItemsProperty?.GetValue(menu) is not IList items)
            {
                return;
            }

            var matching = new ArrayList();
            int firstMatch = -1;
            for (int index = 0; index < items.Count; index++)
            {
                object item = items[index];
                GUIContent content = item?.GetType()
                    .GetField("content", InstanceInternal)
                    ?.GetValue(item) as GUIContent;
                if (content != null && content.text.StartsWith(pathPrefix, StringComparison.Ordinal))
                {
                    firstMatch = firstMatch < 0 ? index : firstMatch;
                    matching.Add(item);
                }
            }

            if (firstMatch <= 0 || matching.Count == 0)
            {
                return;
            }

            object rootSeparator = null;
            object preceding = items[firstMatch - 1];
            FieldInfo separatorField = preceding?.GetType().GetField("separator", InstanceInternal);
            if (separatorField?.GetValue(preceding) is bool isSeparator && isSeparator)
            {
                rootSeparator = preceding;
            }

            foreach (object item in matching)
            {
                items.Remove(item);
            }
            if (rootSeparator != null)
            {
                items.Remove(rootSeparator);
            }

            int insertionIndex = 0;
            foreach (object item in matching)
            {
                items.Insert(insertionIndex++, item);
            }
            if (rootSeparator != null)
            {
                items.Insert(insertionIndex, rootSeparator);
            }
        }

        internal static string StripGameObjectRoot(string path)
        {
            const string root = "GameObject/";
            return path != null && path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(root.Length)
                : path ?? string.Empty;
        }

        private static void AddEditorCommand(GenericMenu menu, string label, string menuPath)
        {
            string hotkey = GetHotkeyMethod?.Invoke(null, new object[] { menuPath }) as string;
            GUIContent content = new GUIContent(string.IsNullOrEmpty(hotkey) ? label : label + " " + hotkey);
            if (Menu.GetEnabled(menuPath))
            {
                menu.AddItem(content, false, () => EditorApplication.ExecuteMenuItem(menuPath));
            }
            else
            {
                menu.AddDisabledItem(content);
            }
        }

        private static void AddCallback(GenericMenu menu, string label, Action callback)
        {
            if (callback != null)
            {
                menu.AddItem(new GUIContent(label), false, callback.Invoke);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(label));
            }
        }

        private static bool IsNativeHierarchyRegisteredPath(string fullPath)
        {
            switch (fullPath)
            {
                case "GameObject/Create Empty Child":
                case "GameObject/Center On Children":
                case "GameObject/Make Parent":
                case "GameObject/Clear Parent":
                case "GameObject/Set as first sibling":
                case "GameObject/Set as last sibling":
                    return false;
                default:
                    return true;
            }
        }

        private static void InvertSceneSelection()
        {
            var selected = new HashSet<GameObject>(Selection.gameObjects);
            var inverted = new List<UnityEngine.Object>();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    inverted.AddRange(root.GetComponentsInChildren<Transform>(true)
                        .Select(transform => transform.gameObject)
                        .Where(gameObject => !selected.Contains(gameObject) &&
                                             (gameObject.hideFlags &
                                              (HideFlags.HideAndDontSave | HideFlags.NotEditable)) == 0));
                }
            }
            Selection.objects = inverted.ToArray();
        }

        private static void AddPrefabItems(GenericMenu menu, GameObject context)
        {
            GameObject root = context != null
                ? PrefabUtility.GetOutermostPrefabInstanceRoot(context)
                : null;
            if (root == null)
            {
                return;
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(context);
            menu.AddSeparator("Prefab/");
            if (source != null)
            {
                menu.AddItem(new GUIContent("Prefab/Open Asset"), false, () => AssetDatabase.OpenAsset(source));
                menu.AddItem(new GUIContent("Prefab/Select Asset"), false, () =>
                {
                    Selection.activeObject = source;
                    EditorGUIUtility.PingObject(source);
                });
            }

            if (root == context && PrefabUtility.HasPrefabInstanceAnyOverrides(root, false))
            {
                menu.AddItem(new GUIContent("Prefab/Apply All"), false,
                    () => PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction));
                menu.AddItem(new GUIContent("Prefab/Revert All"), false,
                    () => PrefabUtility.RevertPrefabInstance(root, InteractionMode.UserAction));
            }

            if (root == context)
            {
                menu.AddItem(new GUIContent("Prefab/Unpack"), false,
                    () => PrefabUtility.UnpackPrefabInstance(
                        root,
                        PrefabUnpackMode.OutermostRoot,
                        InteractionMode.UserAction));
                menu.AddItem(new GUIContent("Prefab/Unpack Completely"), false,
                    () => PrefabUtility.UnpackPrefabInstance(
                        root,
                        PrefabUnpackMode.Completely,
                        InteractionMode.UserAction));
            }
        }

        private static bool AddNativePrefabItems(GenericMenu menu, GameObject context)
        {
            if (context == null || !EnsureNativeHierarchy() || nativePrefabMenuMethod == null)
            {
                return false;
            }

            try
            {
                int before = menu.GetItemCount();
                ParameterInfo idParameter = nativePrefabMenuMethod.GetParameters()[1];
                nativePrefabMenuMethod.Invoke(
                    nativeHierarchy,
                    new[] { menu, GetHierarchyContextId(context, idParameter.ParameterType) });
                return menu.GetItemCount() > before;
            }
            catch
            {
                return false;
            }
        }

        private static void InvokeNativeHierarchyCommand(
            string methodName,
            GameObject context,
            params object[] arguments)
        {
            if (!EnsureNativeHierarchy())
            {
                return;
            }

            Selection.activeGameObject = context;
            hierarchySelectionChangedMethod?.Invoke(nativeHierarchy, null);
            hierarchySyncMethod?.Invoke(nativeHierarchy, null);
            nativeHierarchy.GetType()
                .GetMethod(methodName, InstanceInternal)
                ?.Invoke(nativeHierarchy, arguments);
        }

        private static void OpenPropertyEditor(GameObject context)
        {
            Type propertyEditorType = Type.GetType("UnityEditor.PropertyEditor,UnityEditor");
            MethodInfo openMethod = propertyEditorType?.GetMethod(
                "OpenPropertyEditor",
                StaticInternal,
                null,
                new[] { typeof(UnityEngine.Object), typeof(bool) },
                null);
            if (openMethod != null)
            {
                openMethod.Invoke(null, new object[] { context, true });
            }
        }

        private static string GetMenuDirectory(string displayPath)
        {
            int separator = displayPath.LastIndexOf('/');
            return separator >= 0 ? displayPath.Substring(0, separator + 1) : string.Empty;
        }

        private static bool IsMenuEnabled(string menuPath, GameObject context)
        {
            if (context != null && GetEnabledWithContextMethod != null)
            {
                return (bool)GetEnabledWithContextMethod.Invoke(
                    null,
                    new object[] { menuPath, new UnityEngine.Object[] { context } });
            }

            return Menu.GetEnabled(menuPath);
        }

        private static void ExecuteRegisteredItem(
            string menuPath,
            GameObject context,
            Action<GameObject> afterCreate,
            bool useRootContext)
        {
            GameObject previous = Selection.activeGameObject;
            bool executed;
            if ((context != null || useRootContext) && ExecuteWithContextMethod != null)
            {
                executed = (bool)ExecuteWithContextMethod.Invoke(
                    null,
                    new object[]
                    {
                        menuPath,
                        context != null
                            ? new UnityEngine.Object[] { context }
                            : Array.Empty<UnityEngine.Object>()
                    });
            }
            else
            {
                executed = EditorApplication.ExecuteMenuItem(menuPath);
            }

            GameObject created = Selection.activeGameObject;
            if (executed && created != null && created != previous)
            {
                afterCreate?.Invoke(created);
            }
        }

        private static bool EnsureNativeHierarchy()
        {
            if (nativeHierarchy != null && createGameObjectContextMethod != null)
            {
                return true;
            }

            try
            {
                Type windowType = Type.GetType("UnityEditor.SceneHierarchyWindow,UnityEditor");
                if (windowType == null)
                {
                    return false;
                }

                nativeHierarchyWindow = ScriptableObject.CreateInstance(windowType) as EditorWindow;
                if (nativeHierarchyWindow == null)
                {
                    return false;
                }
                nativeHierarchyWindow.hideFlags = HideFlags.HideAndDontSave;

                PropertyInfo hierarchyProperty = windowType.GetProperty("sceneHierarchy", InstanceInternal);
                nativeHierarchy = hierarchyProperty?.GetValue(nativeHierarchyWindow) ??
                                  windowType.GetField("m_SceneHierarchy", InstanceInternal)
                                      ?.GetValue(nativeHierarchyWindow);
                if (nativeHierarchy == null)
                {
                    UnityEngine.Object.DestroyImmediate(nativeHierarchyWindow);
                    nativeHierarchyWindow = null;
                    return false;
                }

                Type hierarchyType = nativeHierarchy.GetType();
                foreach (MethodInfo method in hierarchyType.GetMethods(InstanceInternal))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (method.Name == "CreateGameObjectContextClick" &&
                        parameters.Length == 2 &&
                        parameters[0].ParameterType == typeof(GenericMenu))
                    {
                        createGameObjectContextMethod = method;
                        break;
                    }
                }

                foreach (MethodInfo method in hierarchyType.GetMethods(InstanceInternal))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (method.Name == "PopulateGenericMenuWithPrefabMenuItems" &&
                        parameters.Length == 2 &&
                        parameters[0].ParameterType == typeof(GenericMenu))
                    {
                        nativePrefabMenuMethod = method;
                        break;
                    }
                }

                hierarchySelectionChangedMethod = hierarchyType.GetMethod(
                    "OnSelectionChange",
                    InstanceInternal,
                    null,
                    Type.EmptyTypes,
                    null);
                hierarchySyncMethod = hierarchyType.GetMethod(
                    "SyncIfNeeded",
                    InstanceInternal,
                    null,
                    Type.EmptyTypes,
                    null);
                return createGameObjectContextMethod != null;
            }
            catch
            {
                if (nativeHierarchyWindow != null)
                {
                    UnityEngine.Object.DestroyImmediate(nativeHierarchyWindow);
                }
                nativeHierarchyWindow = null;
                nativeHierarchy = null;
                createGameObjectContextMethod = null;
                return false;
            }
        }

        private static object GetHierarchyContextId(GameObject context, Type idType)
        {
            if (idType == typeof(int))
            {
                return context != null ? context.GetInstanceID() : 0;
            }

            if (context != null)
            {
                MethodInfo getEntityId = typeof(UnityEngine.Object).GetMethod(
                    "GetEntityId",
                    InstanceInternal,
                    null,
                    Type.EmptyTypes,
                    null);
                if (getEntityId != null && idType.IsAssignableFrom(getEntityId.ReturnType))
                {
                    return getEntityId.Invoke(context, null);
                }
            }

            return idType.IsValueType ? Activator.CreateInstance(idType) : null;
        }

        private static void PatchStockCallback(
            GenericMenu menu,
            int startIndex,
            string label,
            Action callback)
        {
            if (callback == null || GenericMenuItemsProperty?.GetValue(menu) is not IList items)
            {
                return;
            }

            for (int index = Math.Max(0, startIndex); index < items.Count; index++)
            {
                object item = items[index];
                if (item == null)
                {
                    continue;
                }

                Type itemType = item.GetType();
                FieldInfo contentField = itemType.GetField("content", InstanceInternal);
                GUIContent content = contentField?.GetValue(item) as GUIContent;
                if (content == null ||
                    !(string.Equals(content.text, label, StringComparison.Ordinal) ||
                      content.text.StartsWith(label + " ", StringComparison.Ordinal)))
                {
                    continue;
                }

                itemType.GetField("func", InstanceInternal)?.SetValue(
                    item,
                    new GenericMenu.MenuFunction(callback.Invoke));
                itemType.GetField("func2", InstanceInternal)?.SetValue(item, null);
                itemType.GetField("userData", InstanceInternal)?.SetValue(item, null);
                return;
            }
        }
    }
}
