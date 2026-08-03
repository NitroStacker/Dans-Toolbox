using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterInspector
{
    internal sealed class BetterInspectorComponentMenuEntry
    {
        internal BetterInspectorComponentMenuEntry(Type type, string menuPath)
        {
            Type = type;
            MenuPath = menuPath;
            int separator = menuPath.LastIndexOf('/');
            CategoryPath = separator < 0 ? string.Empty : menuPath.Substring(0, separator);
            DisplayName = separator < 0 ? menuPath : menuPath.Substring(separator + 1);
        }

        internal Type Type { get; }
        internal string MenuPath { get; }
        internal string CategoryPath { get; }
        internal string DisplayName { get; }
    }

    internal sealed class BetterInspectorAddComponentPopup : PopupWindowContent
    {
        private const string SuggestedCategory = "Suggested";
        private const string SearchControlName = "BetterInspectorAddSearch";

        private static readonly string[] SuggestedTypeNames =
        {
            "BoxCollider",
            "Rigidbody",
            "AudioSource",
            "Animator",
            "Camera",
            "Light",
            "Canvas",
            "ParticleSystem"
        };

        private static readonly Type NativeDataSourceType =
            typeof(EditorWindow).Assembly.GetType("UnityEditor.AddComponent.AddComponentDataSource");

        private static readonly MethodInfo NativeMenuItemsMethod = NativeDataSourceType?.GetMethod(
            "GetSortedMenuItems",
            BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly Type UnityTypeType =
            typeof(EditorWindow).Assembly.GetType("UnityEditor.UnityType");

        private static readonly MethodInfo FindUnityTypeMethod = UnityTypeType?.GetMethod(
            "FindTypeByPersistentTypeID",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly PropertyInfo UnityTypeNameProperty = UnityTypeType?.GetProperty(
            "name",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly GameObject[] targets;
        private readonly Action completed;
        private readonly Dictionary<string, int> categoryCountCache =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, GUIContent> categoryIconCache =
            new Dictionary<string, GUIContent>(StringComparer.Ordinal);
        private readonly Dictionary<Type, GUIContent> typeIconCache =
            new Dictionary<Type, GUIContent>();
        private List<BetterInspectorComponentMenuEntry> entries;
        private Vector2 scroll;
        private string query = string.Empty;
        private string currentCategory = string.Empty;
        private bool focusSearchOnNextRepaint;
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle rowTitleStyle;
        private GUIStyle metadataStyle;
        private GUIStyle navigationGlyphStyle;
        private Rect lastSearchRect;

        internal BetterInspectorAddComponentPopup(GameObject[] targets, Action completed)
        {
            this.targets = targets?.Where(target => target != null).Distinct().ToArray() ??
                           Array.Empty<GameObject>();
            this.completed = completed;
        }

        public override Vector2 GetWindowSize() => new Vector2(390f, 470f);

        public override void OnOpen()
        {
            entries = BuildCatalog(targets);
            categoryCountCache.Clear();
            categoryIconCache.Clear();
            typeIconCache.Clear();
            focusSearchOnNextRepaint = true;
            editorWindow.wantsMouseMove = true;
        }

        public override void OnGUI(Rect rect)
        {
            if (Event.current.type == EventType.MouseMove)
            {
                editorWindow.Repaint();
            }

            DansToolboxPalette palette = DansToolboxTheme.Current;
            EnsureStyles(palette);
            if (DansToolboxSearchField.ReleaseFocusOnPointerDown(lastSearchRect, SearchControlName)) editorWindow.Repaint();
            EditorGUI.DrawRect(rect, palette.Canvas);
            Rect header = new Rect(0f, 0f, rect.width, 42f);
            EditorGUI.DrawRect(header, palette.Panel);
            EditorGUI.DrawRect(new Rect(0f, header.yMax - 1f, rect.width, 1f), palette.Border);
            GUI.Label(new Rect(12f, 6f, 116f, 28f), "ADD COMPONENT", headerStyle);
            lastSearchRect = new Rect(128f, 10f, rect.width - 140f, DansToolboxSearchField.Height);
            string updatedQuery = DansToolboxSearchField.Draw(
                lastSearchRect,
                query,
                SearchControlName,
                "Search components");
            if (!string.Equals(updatedQuery, query, StringComparison.Ordinal))
            {
                query = updatedQuery;
                scroll = Vector2.zero;
            }

            HandleKeyboard();

            Rect body = new Rect(8f, 50f, rect.width - 16f, rect.height - 58f);
            GUILayout.BeginArea(body);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (string.IsNullOrWhiteSpace(query))
            {
                DrawCategoryBrowser(palette);
            }
            else
            {
                DrawSearchResults(palette);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            if (focusSearchOnNextRepaint && Event.current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl(SearchControlName);
                focusSearchOnNextRepaint = false;
            }
        }

        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || current.keyCode != KeyCode.Escape)
            {
                return;
            }

            if (!string.IsNullOrEmpty(query))
            {
                query = string.Empty;
                scroll = Vector2.zero;
            }
            else if (!string.IsNullOrEmpty(currentCategory))
            {
                currentCategory = GetParentCategory(currentCategory);
                scroll = Vector2.zero;
            }
            else
            {
                editorWindow.Close();
            }
            current.Use();
        }

        private void DrawCategoryBrowser(DansToolboxPalette palette)
        {
            if (!string.IsNullOrEmpty(currentCategory))
            {
                DrawBackRow(palette);
                GUILayout.Space(4f);
            }
            else
            {
                GUILayout.Label("CATEGORIES", sectionStyle);
                GUILayout.Space(3f);
            }

            List<string> categories = GetChildCategories(entries, currentCategory).ToList();
            if (string.IsNullOrEmpty(currentCategory) && HasSuggestedEntries())
            {
                categories.Insert(0, SuggestedCategory);
            }

            foreach (string category in categories)
            {
                DrawCategoryRow(category, palette);
                GUILayout.Space(2f);
            }

            List<BetterInspectorComponentMenuEntry> components =
                GetEntriesForCategory(entries, currentCategory).ToList();
            if (components.Count > 0 && categories.Count > 0)
            {
                GUILayout.Space(5f);
                GUILayout.Label("COMPONENTS", sectionStyle);
                GUILayout.Space(3f);
            }

            foreach (BetterInspectorComponentMenuEntry entry in components)
            {
                DrawTypeRow(entry, palette, showPath: false);
            }

            if (categories.Count == 0 && components.Count == 0)
            {
                DrawEmpty("NO COMPONENTS FOUND");
            }
        }

        private void DrawSearchResults(DansToolboxPalette palette)
        {
            List<BetterInspectorComponentMenuEntry> visible =
                GetSearchResults(entries, query).Take(160).ToList();
            GUILayout.Label(visible.Count + (visible.Count == 1 ? " RESULT" : " RESULTS"),
                sectionStyle);
            GUILayout.Space(3f);
            if (visible.Count == 0)
            {
                DrawEmpty("NO COMPONENTS FOUND");
                return;
            }

            foreach (BetterInspectorComponentMenuEntry entry in visible)
            {
                DrawTypeRow(entry, palette, showPath: true);
            }
        }

        private void DrawBackRow(DansToolboxPalette palette)
        {
            Rect row = GUILayoutUtility.GetRect(1f, 32f, GUILayout.ExpandWidth(true));
            bool hovered = row.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(row, hovered ? palette.Raised : palette.Panel);
            navigationGlyphStyle.normal.textColor = palette.Accent;
            GUI.Label(new Rect(row.x + 9f, row.y + 6f, 20f, 20f), "‹", navigationGlyphStyle);
            GUI.Label(
                new Rect(row.x + 36f, row.y + 2f, row.width - 45f, 18f),
                GetCategoryName(currentCategory).ToUpperInvariant(),
                rowTitleStyle);
            GUI.Label(
                new Rect(row.x + 36f, row.y + 17f, row.width - 45f, 12f),
                "BACK TO " + (string.IsNullOrEmpty(GetParentCategory(currentCategory))
                    ? "ALL CATEGORIES"
                    : GetParentCategory(currentCategory).ToUpperInvariant()),
                metadataStyle);
            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                currentCategory = GetParentCategory(currentCategory);
                scroll = Vector2.zero;
            }
        }

        private void DrawCategoryRow(string category, DansToolboxPalette palette)
        {
            Rect row = GUILayoutUtility.GetRect(1f, 35f, GUILayout.ExpandWidth(true));
            bool hovered = row.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(row, hovered ? palette.Raised : palette.Inset);
            if (hovered)
            {
                EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height), palette.Accent);
            }

            GUI.Label(
                new Rect(row.x + 9f, row.y + 7f, 20f, 20f),
                GetCategoryIconContent(category));
            GUI.Label(
                new Rect(row.x + 36f, row.y + 3f, row.width - 84f, 18f),
                GetCategoryName(category),
                rowTitleStyle);
            int count = GetCategoryCount(category);
            GUI.Label(
                new Rect(row.x + 36f, row.y + 18f, row.width - 84f, 14f),
                count + (count == 1 ? " COMPONENT" : " COMPONENTS"),
                metadataStyle);
            navigationGlyphStyle.normal.textColor = palette.Muted;
            GUI.Label(new Rect(row.xMax - 29f, row.y + 7f, 18f, 20f), "›", navigationGlyphStyle);

            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                currentCategory = category;
                scroll = Vector2.zero;
            }
        }

        private void DrawTypeRow(
            BetterInspectorComponentMenuEntry entry,
            DansToolboxPalette palette,
            bool showPath)
        {
            Rect row = GUILayoutUtility.GetRect(1f, 35f, GUILayout.ExpandWidth(true));
            bool hovered = row.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(row, hovered ? palette.Raised : palette.Inset);
            if (hovered)
            {
                EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height), palette.Accent);
            }

            GUI.Label(
                new Rect(row.x + 9f, row.y + 7f, 20f, 20f),
                GetTypeIconContent(entry.Type));
            GUI.Label(
                new Rect(row.x + 36f, row.y + 3f, row.width - 46f, 18f),
                entry.DisplayName,
                rowTitleStyle);
            string metadata = showPath
                ? entry.CategoryPath
                : string.IsNullOrEmpty(entry.Type.Namespace) ? "PROJECT" : entry.Type.Namespace;
            GUI.Label(
                new Rect(row.x + 36f, row.y + 18f, row.width - 46f, 14f),
                string.IsNullOrEmpty(metadata) ? "UNCATEGORIZED" : metadata,
                metadataStyle);

            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                Add(entry.Type);
            }
            GUILayout.Space(2f);
        }

        private void DrawEmpty(string label)
        {
            GUILayout.Space(24f);
            GUILayout.Label(label, new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontStyle = FontStyle.Bold
            });
        }

        private int GetCategoryCount(string category)
        {
            if (categoryCountCache.TryGetValue(category, out int cachedCount))
            {
                return cachedCount;
            }

            int count;
            if (category == SuggestedCategory)
            {
                count = entries.Count(entry => SuggestedTypeNames.Contains(entry.Type.Name));
            }
            else
            {
                string prefix = category + "/";
                count = entries.Count(entry =>
                    entry.CategoryPath == category ||
                    entry.CategoryPath.StartsWith(prefix, StringComparison.Ordinal));
            }

            categoryCountCache[category] = count;
            return count;
        }

        private GUIContent GetCategoryIconContent(string category)
        {
            if (categoryIconCache.TryGetValue(category, out GUIContent content))
            {
                return content;
            }

            Type representativeType = GetCategoryRepresentativeType(entries, category);
            content = representativeType == null
                ? new GUIContent(EditorGUIUtility.IconContent("Folder Icon").image)
                : GetTypeIconContent(representativeType);
            categoryIconCache[category] = content;
            return content;
        }

        private GUIContent GetTypeIconContent(Type type)
        {
            if (!typeIconCache.TryGetValue(type, out GUIContent content))
            {
                GUIContent unityContent = EditorGUIUtility.ObjectContent(null, type);
                content = new GUIContent(unityContent.image);
                typeIconCache[type] = content;
            }
            return content;
        }

        private bool HasSuggestedEntries()
        {
            return entries.Any(entry => SuggestedTypeNames.Contains(entry.Type.Name));
        }

        private void Add(Type type)
        {
            if (type == null || targets.Length == 0)
            {
                return;
            }

            Undo.SetCurrentGroupName("Add " + ObjectNames.NicifyVariableName(type.Name));
            int group = Undo.GetCurrentGroup();
            int added = 0;
            bool disallowMultiple = Attribute.IsDefined(type, typeof(DisallowMultipleComponent), true);
            foreach (GameObject target in targets)
            {
                if (disallowMultiple && target.GetComponent(type) != null)
                {
                    continue;
                }

                try
                {
                    if (Undo.AddComponent(target, type) != null)
                    {
                        added++;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Better Inspector could not add " + type.Name + " to " +
                                     target.name + ": " + exception.GetBaseException().Message);
                }
            }
            Undo.CollapseUndoOperations(group);
            if (added > 0)
            {
                completed?.Invoke();
                editorWindow.Close();
            }
        }

        internal static IEnumerable<Type> GetAddableTypes()
        {
            return TypeCache.GetTypesDerivedFrom<Component>()
                .Where(type =>
                    type != null &&
                    type != typeof(Transform) &&
                    !type.IsAbstract &&
                    !type.ContainsGenericParameters &&
                    (type.IsPublic || type.IsNestedPublic))
                .OrderBy(type => ObjectNames.NicifyVariableName(type.Name));
        }

        internal static List<BetterInspectorComponentMenuEntry> BuildCatalog(
            GameObject[] gameObjects,
            IEnumerable<Type> candidates = null)
        {
            List<Type> types = (candidates ?? GetAddableTypes())
                .Where(type => type != null)
                .Distinct()
                .ToList();
            var pathByType = ReadNativeMenuPaths(gameObjects, types);
            var result = new List<BetterInspectorComponentMenuEntry>(types.Count);
            foreach (Type type in types)
            {
                if (!pathByType.TryGetValue(type, out string path))
                {
                    path = GetFallbackMenuPath(type);
                }
                result.Add(new BetterInspectorComponentMenuEntry(type, NormalizeMenuPath(path)));
            }
            return result
                .OrderBy(entry => entry.MenuPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Type.FullName, StringComparer.Ordinal)
                .ToList();
        }

        internal static IEnumerable<string> GetChildCategories(
            IEnumerable<BetterInspectorComponentMenuEntry> source,
            string parentCategory)
        {
            string parent = parentCategory ?? string.Empty;
            string prefix = string.IsNullOrEmpty(parent) ? string.Empty : parent + "/";
            return (source ?? Enumerable.Empty<BetterInspectorComponentMenuEntry>())
                .Select(entry => entry.CategoryPath)
                .Where(category =>
                    !string.IsNullOrEmpty(category) &&
                    category.StartsWith(prefix, StringComparison.Ordinal))
                .Select(category =>
                {
                    string remainder = category.Substring(prefix.Length);
                    int separator = remainder.IndexOf('/');
                    string child = separator < 0 ? remainder : remainder.Substring(0, separator);
                    return prefix + child;
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => GetCategoryName(category), StringComparer.OrdinalIgnoreCase);
        }

        internal static IEnumerable<BetterInspectorComponentMenuEntry> GetEntriesForCategory(
            IEnumerable<BetterInspectorComponentMenuEntry> source,
            string category)
        {
            IEnumerable<BetterInspectorComponentMenuEntry> entriesSource =
                source ?? Enumerable.Empty<BetterInspectorComponentMenuEntry>();
            if (category == SuggestedCategory)
            {
                return entriesSource
                    .Where(entry => SuggestedTypeNames.Contains(entry.Type.Name))
                    .OrderBy(entry => Array.IndexOf(SuggestedTypeNames, entry.Type.Name));
            }

            string current = category ?? string.Empty;
            return entriesSource
                .Where(entry => entry.CategoryPath == current)
                .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);
        }

        internal static Type GetCategoryRepresentativeType(
            IEnumerable<BetterInspectorComponentMenuEntry> source,
            string category)
        {
            IEnumerable<BetterInspectorComponentMenuEntry> entriesSource =
                source ?? Enumerable.Empty<BetterInspectorComponentMenuEntry>();
            if (category == SuggestedCategory)
            {
                return entriesSource
                    .Where(entry => SuggestedTypeNames.Contains(entry.Type.Name))
                    .OrderBy(entry => Array.IndexOf(SuggestedTypeNames, entry.Type.Name))
                    .Select(entry => entry.Type)
                    .FirstOrDefault();
            }

            string prefix = (category ?? string.Empty) + "/";
            return entriesSource
                .Where(entry =>
                    entry.CategoryPath == category ||
                    entry.CategoryPath.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(entry =>
                {
                    int suggested = Array.IndexOf(SuggestedTypeNames, entry.Type.Name);
                    return suggested < 0 ? int.MaxValue : suggested;
                })
                .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.Type)
                .FirstOrDefault();
        }

        internal static IEnumerable<BetterInspectorComponentMenuEntry> GetSearchResults(
            IEnumerable<BetterInspectorComponentMenuEntry> source,
            string value)
        {
            string queryValue = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(queryValue))
            {
                return Enumerable.Empty<BetterInspectorComponentMenuEntry>();
            }

            string[] tokens = queryValue.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            return (source ?? Enumerable.Empty<BetterInspectorComponentMenuEntry>())
                .Where(entry =>
                {
                    string label = entry.DisplayName + " " + entry.MenuPath + " " + entry.Type.FullName;
                    return tokens.All(token =>
                        label.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
                })
                .OrderBy(entry => Score(entry, queryValue))
                .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<Type, string> ReadNativeMenuPaths(
            GameObject[] gameObjects,
            List<Type> addableTypes)
        {
            var result = new Dictionary<Type, string>();
            if (NativeMenuItemsMethod == null || gameObjects == null || gameObjects.Length == 0)
            {
                return result;
            }

            try
            {
                IEnumerable nativeItems = NativeMenuItemsMethod.Invoke(
                    null,
                    new object[] { gameObjects }) as IEnumerable;
                if (nativeItems == null)
                {
                    return result;
                }

                Dictionary<string, List<Type>> typesByName = addableTypes
                    .GroupBy(type => type.Name)
                    .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
                foreach (object item in nativeItems)
                {
                    Type itemType = item.GetType();
                    string path = itemType.GetField("path")?.GetValue(item) as string;
                    string command = itemType.GetField("command")?.GetValue(item) as string;
                    if (string.IsNullOrEmpty(path) || path == "Component/Add...")
                    {
                        continue;
                    }

                    Type type = ResolveNativeType(command, typesByName);
                    if (type != null && addableTypes.Contains(type) && !result.ContainsKey(type))
                    {
                        result.Add(type, path);
                    }
                }
            }
            catch
            {
                // Unity's private menu data is version-specific; attribute and namespace fallbacks remain valid.
            }
            return result;
        }

        private static Type ResolveNativeType(
            string command,
            Dictionary<string, List<Type>> typesByName)
        {
            if (string.IsNullOrEmpty(command))
            {
                return null;
            }

            if (command.StartsWith("SCRIPT", StringComparison.Ordinal) &&
                int.TryParse(command.Substring(6), out int scriptId))
            {
#if UNITY_6000_3_OR_NEWER
                MonoScript script = EditorUtility.EntityIdToObject(scriptId) as MonoScript;
#else
                MonoScript script = EditorUtility.InstanceIDToObject(scriptId) as MonoScript;
#endif
                return script?.GetClass();
            }

            if (!int.TryParse(command, out int persistentTypeId) ||
                FindUnityTypeMethod == null ||
                UnityTypeNameProperty == null)
            {
                return null;
            }

            object unityType = FindUnityTypeMethod.Invoke(null, new object[] { persistentTypeId });
            string nativeName = unityType == null
                ? null
                : UnityTypeNameProperty.GetValue(unityType) as string;
            if (string.IsNullOrEmpty(nativeName) ||
                !typesByName.TryGetValue(nativeName, out List<Type> matches))
            {
                return null;
            }
            return matches.FirstOrDefault();
        }

        private static string GetFallbackMenuPath(Type type)
        {
            AddComponentMenu attribute = type
                .GetCustomAttributes(typeof(AddComponentMenu), false)
                .OfType<AddComponentMenu>()
                .FirstOrDefault();
            if (attribute != null && !string.IsNullOrWhiteSpace(attribute.componentMenu))
            {
                return attribute.componentMenu;
            }

            string name = ObjectNames.NicifyVariableName(type.Name);
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                string namespacePath = string.IsNullOrEmpty(type.Namespace)
                    ? string.Empty
                    : type.Namespace.Replace('.', '/');
                return string.IsNullOrEmpty(namespacePath)
                    ? "Scripts/" + name
                    : "Scripts/" + namespacePath + "/" + name;
            }
            return "Miscellaneous/" + name;
        }

        private static string NormalizeMenuPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\u005c', '/').Trim('/');
            if (normalized.StartsWith("Component/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("Component/".Length);
            }
            return string.IsNullOrEmpty(normalized) ? "Miscellaneous/Component" : normalized;
        }

        private static string GetParentCategory(string category)
        {
            if (string.IsNullOrEmpty(category) || category == SuggestedCategory)
            {
                return string.Empty;
            }
            int separator = category.LastIndexOf('/');
            return separator < 0 ? string.Empty : category.Substring(0, separator);
        }

        private static string GetCategoryName(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                return string.Empty;
            }
            int separator = category.LastIndexOf('/');
            return separator < 0 ? category : category.Substring(separator + 1);
        }

        private static int Score(BetterInspectorComponentMenuEntry entry, string value)
        {
            if (entry.DisplayName.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            if (entry.DisplayName.StartsWith(value, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (entry.MenuPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }
            return 3;
        }

        private void EnsureStyles(DansToolboxPalette palette)
        {
            headerStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };
            sectionStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = 9
            };
            rowTitleStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                clipping = TextClipping.Clip
            };
            metadataStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 8,
                clipping = TextClipping.Clip
            };
            navigationGlyphStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = palette.Text;
            sectionStyle.normal.textColor = palette.Muted;
            rowTitleStyle.normal.textColor = palette.Text;
            metadataStyle.normal.textColor = palette.Muted;
        }
    }
}
