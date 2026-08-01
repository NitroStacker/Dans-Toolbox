using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    [FilePath("ProjectSettings/BetterHierarchySettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class BetterHierarchyProjectSettings : ScriptableSingleton<BetterHierarchyProjectSettings>
    {
        [SerializeField] private bool initialized;
        [SerializeField] private List<BetterHierarchyRule> rules = new List<BetterHierarchyRule>();
        [SerializeField] private List<BetterHierarchyCollection> collections = new List<BetterHierarchyCollection>();

        internal static IReadOnlyList<BetterHierarchyRule> Rules
        {
            get
            {
                EnsureInitialized();
                return instance.rules;
            }
        }

        internal static IReadOnlyList<BetterHierarchyCollection> Collections
        {
            get
            {
                EnsureInitialized();
                return instance.collections;
            }
        }

        internal static List<BetterHierarchyRule> MutableRules
        {
            get
            {
                EnsureInitialized();
                return instance.rules;
            }
        }

        internal static List<BetterHierarchyCollection> MutableCollections
        {
            get
            {
                EnsureInitialized();
                return instance.collections;
            }
        }

        internal static event Action Changed;

        internal static void SaveNow()
        {
            EnsureInitialized();
            instance.Save(true);
            Changed?.Invoke();
            EditorApplication.RepaintHierarchyWindow();
        }

        internal static void RecordUndo(string actionName)
        {
            EnsureInitialized();
            Undo.RegisterCompleteObjectUndo(instance, actionName);
        }

        internal static void ResetRules()
        {
            instance.rules = CreateStarterRules();
            instance.initialized = true;
            SaveNow();
        }

        internal static BetterHierarchyCollection AddCollection(string name, Color color)
        {
            EnsureInitialized();
            BetterHierarchyCollection collection = new BetterHierarchyCollection
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Collection" : name.Trim(),
                Color = color
            };
            instance.collections.Add(collection);
            SaveNow();
            return collection;
        }

        internal static bool RemoveCollection(BetterHierarchyCollection collection)
        {
            EnsureInitialized();
            if (collection != null && instance.collections.Remove(collection))
            {
                SaveNow();
                return true;
            }

            return false;
        }

        internal static BetterHierarchyCollection FindCollection(string id)
        {
            EnsureInitialized();
            return instance.collections.FirstOrDefault(collection => collection.Id == id);
        }

        private static void EnsureInitialized()
        {
            if (instance.initialized)
            {
                instance.rules ??= new List<BetterHierarchyRule>();
                instance.collections ??= new List<BetterHierarchyCollection>();
                return;
            }

            instance.initialized = true;
            instance.rules = CreateStarterRules();
            instance.collections = new List<BetterHierarchyCollection>();
            instance.Save(true);
        }

        private static List<BetterHierarchyRule> CreateStarterRules()
        {
            return new List<BetterHierarchyRule>
            {
                MakeRule("Cameras", BetterHierarchyRuleMatch.HasComponent, "Camera",
                    new Color(0.16f, 0.58f, 0.95f, 0.22f), "CAM", "Camera Icon"),
                MakeRule("Lights", BetterHierarchyRuleMatch.HasComponent, "Light",
                    new Color(1f, 0.72f, 0.12f, 0.2f), "LGT", "Light Icon"),
                MakeRule("UI", BetterHierarchyRuleMatch.HasComponent, "Canvas",
                    new Color(0.54f, 0.36f, 1f, 0.2f), "UI", "Canvas Icon"),
                MakeRule("Audio", BetterHierarchyRuleMatch.HasComponent, "AudioSource",
                    new Color(0.2f, 0.82f, 0.58f, 0.18f), "SFX", "AudioSource Icon"),
                MakeRule("Managers", BetterHierarchyRuleMatch.NameContains, "Manager",
                    new Color(1f, 0.55f, 0.12f, 0.18f), "SYS", "Settings"),
                MakeRule("Missing", BetterHierarchyRuleMatch.MissingScript, string.Empty,
                    new Color(0.92f, 0.18f, 0.24f, 0.34f), "!", "console.erroricon", 100)
            };
        }

        private static BetterHierarchyRule MakeRule(
            string name,
            BetterHierarchyRuleMatch match,
            string value,
            Color color,
            string badge,
            string icon,
            int priority = 0)
        {
            return new BetterHierarchyRule
            {
                Name = name,
                Match = match,
                Value = value,
                Color = color,
                Badge = badge,
                IconName = icon,
                Priority = priority
            };
        }
    }

    internal static class BetterHierarchyUserSettings
    {
        private static string Prefix => "DansToolbox.BetterHierarchy." +
                                        Hash128.Compute(Application.dataPath) + ".";

        internal static BetterHierarchyMode Mode
        {
            get => (BetterHierarchyMode)EditorPrefs.GetInt(Prefix + "Mode", (int)BetterHierarchyMode.Production);
            set => EditorPrefs.SetInt(Prefix + "Mode", (int)value);
        }

        internal static bool TreeLines
        {
            get => EditorPrefs.GetBool(Prefix + "TreeLines", true);
            set => EditorPrefs.SetBool(Prefix + "TreeLines", value);
        }

        internal static bool Zebra
        {
            get => EditorPrefs.GetBool(Prefix + "Zebra", true);
            set => EditorPrefs.SetBool(Prefix + "Zebra", value);
        }

        internal static bool Components
        {
            get => EditorPrefs.GetBool(Prefix + "Components", true);
            set => EditorPrefs.SetBool(Prefix + "Components", value);
        }

        internal static bool QuickActions
        {
            get => EditorPrefs.GetBool(Prefix + "QuickActions", true);
            set => EditorPrefs.SetBool(Prefix + "QuickActions", value);
        }

        internal static bool Diagnostics
        {
            get => EditorPrefs.GetBool(Prefix + "Diagnostics", true);
            set => EditorPrefs.SetBool(Prefix + "Diagnostics", value);
        }

        internal static bool ChildCounts
        {
            get => EditorPrefs.GetBool(Prefix + "ChildCounts", true);
            set => EditorPrefs.SetBool(Prefix + "ChildCounts", value);
        }

        internal static float RowHeight
        {
            get => Mathf.Clamp(EditorPrefs.GetFloat(Prefix + "RowHeight", 22f), 18f, 30f);
            set => EditorPrefs.SetFloat(Prefix + "RowHeight", Mathf.Clamp(value, 18f, 30f));
        }

        internal static bool IsFavorite(GameObject gameObject)
        {
            string id = BetterHierarchyObjectIds.Get(gameObject);
            return !string.IsNullOrEmpty(id) && ReadIds("Favorites").Contains(id);
        }

        internal static void ToggleFavorite(GameObject gameObject)
        {
            string id = BetterHierarchyObjectIds.Get(gameObject);
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            List<string> ids = ReadIds("Favorites");
            if (!ids.Remove(id))
            {
                ids.Insert(0, id);
            }

            WriteIds("Favorites", ids, 200);
        }

        internal static IReadOnlyList<GameObject> Favorites => Resolve(ReadIds("Favorites"));

        internal static IReadOnlyList<GameObject> Recent => Resolve(ReadIds("Recent"));

        internal static GameObject DefaultParent
        {
            get
            {
                GameObject parent = BetterHierarchyObjectIds.Resolve(EditorPrefs.GetString(Prefix + "DefaultParent", string.Empty));
                if (parent == null)
                {
                    EditorPrefs.DeleteKey(Prefix + "DefaultParent");
                }
                return parent;
            }
            set
            {
                string id = BetterHierarchyObjectIds.Get(value);
                if (string.IsNullOrEmpty(id))
                {
                    EditorPrefs.DeleteKey(Prefix + "DefaultParent");
                }
                else
                {
                    EditorPrefs.SetString(Prefix + "DefaultParent", id);
                }
            }
        }

        internal static IReadOnlyList<BetterHierarchySavedSearch> SavedSearches
        {
            get
            {
                string json = EditorPrefs.GetString(Prefix + "SavedSearches", string.Empty);
                if (string.IsNullOrEmpty(json))
                {
                    return Array.Empty<BetterHierarchySavedSearch>();
                }

                BetterHierarchySavedSearchSet set = JsonUtility.FromJson<BetterHierarchySavedSearchSet>(json);
                return set?.Items ?? (IReadOnlyList<BetterHierarchySavedSearch>)Array.Empty<BetterHierarchySavedSearch>();
            }
        }

        internal static void SaveSearch(string name, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            BetterHierarchySavedSearchSet set = ReadSavedSearchSet();
            string cleanName = string.IsNullOrWhiteSpace(name) ? query.Trim() : name.Trim();
            BetterHierarchySavedSearch existing = set.Items.FirstOrDefault(item =>
                string.Equals(item.Name, cleanName, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new BetterHierarchySavedSearch { Name = cleanName };
                set.Items.Add(existing);
            }
            existing.Query = query.Trim();
            WriteSavedSearchSet(set);
        }

        internal static void RemoveSavedSearch(BetterHierarchySavedSearch search)
        {
            if (search == null)
            {
                return;
            }

            BetterHierarchySavedSearchSet set = ReadSavedSearchSet();
            set.Items.RemoveAll(item =>
                string.Equals(item.Name, search.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Query, search.Query, StringComparison.Ordinal));
            WriteSavedSearchSet(set);
        }

        internal static void RecordRecent(GameObject gameObject)
        {
            string id = BetterHierarchyObjectIds.Get(gameObject);
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            List<string> ids = ReadIds("Recent");
            ids.Remove(id);
            ids.Insert(0, id);
            WriteIds("Recent", ids, 80);
        }

        private static List<string> ReadIds(string key)
        {
            return EditorPrefs.GetString(Prefix + key, string.Empty)
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static void WriteIds(string key, List<string> ids, int limit)
        {
            if (ids.Count > limit)
            {
                ids.RemoveRange(limit, ids.Count - limit);
            }

            EditorPrefs.SetString(Prefix + key, string.Join("\n", ids));
        }

        private static IReadOnlyList<GameObject> Resolve(IEnumerable<string> ids)
        {
            List<GameObject> result = new List<GameObject>();
            foreach (string id in ids)
            {
                GameObject gameObject = BetterHierarchyObjectIds.Resolve(id);
                if (gameObject != null)
                {
                    result.Add(gameObject);
                }
            }

            return result;
        }

        private static BetterHierarchySavedSearchSet ReadSavedSearchSet()
        {
            string json = EditorPrefs.GetString(Prefix + "SavedSearches", string.Empty);
            return string.IsNullOrEmpty(json)
                ? new BetterHierarchySavedSearchSet()
                : JsonUtility.FromJson<BetterHierarchySavedSearchSet>(json) ?? new BetterHierarchySavedSearchSet();
        }

        private static void WriteSavedSearchSet(BetterHierarchySavedSearchSet set)
        {
            EditorPrefs.SetString(Prefix + "SavedSearches", JsonUtility.ToJson(set));
        }
    }

    [InitializeOnLoad]
    internal static class BetterHierarchyObjectIds
    {
        private const string InstancePrefix = "instance:";

        static BetterHierarchyObjectIds()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        internal static string Get(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            // Unsaved scene objects can produce a GlobalObjectId that resolves only until
            // the next hierarchy refresh. Keep a session-stable id and migrate it on save.
            if (gameObject.scene.IsValid() && string.IsNullOrEmpty(gameObject.scene.path))
            {
                return InstancePrefix + gameObject.GetInstanceID();
            }

            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
            if (id.identifierType != 0 &&
                GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) == gameObject)
            {
                return id.ToString();
            }

            return gameObject.scene.IsValid()
                ? InstancePrefix + gameObject.GetInstanceID()
                : string.Empty;
        }

        internal static GameObject Resolve(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (value.StartsWith(InstancePrefix, StringComparison.Ordinal) &&
                int.TryParse(value.Substring(InstancePrefix.Length), out int instanceId))
            {
                return EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            }

            if (!GlobalObjectId.TryParse(value, out GlobalObjectId id))
            {
                return null;
            }

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
        }

        private static void OnSceneSaved(Scene scene)
        {
            bool changed = false;
            foreach (BetterHierarchyCollection collection in BetterHierarchyProjectSettings.MutableCollections)
            {
                for (int index = 0; index < collection.MemberIds.Count; index++)
                {
                    string existing = collection.MemberIds[index];
                    if (!existing.StartsWith(InstancePrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    GameObject gameObject = Resolve(existing);
                    if (gameObject == null || gameObject.scene != scene)
                    {
                        continue;
                    }

                    string persistent = Get(gameObject);
                    if (!persistent.StartsWith(InstancePrefix, StringComparison.Ordinal))
                    {
                        collection.MemberIds[index] = persistent;
                        changed = true;
                    }
                }

                int countBeforeCleanup = collection.MemberIds.Count;
                collection.MemberIds.RemoveAll(string.IsNullOrEmpty);
                changed |= collection.MemberIds.Count != countBeforeCleanup;
                for (int index = collection.MemberIds.Count - 1; index >= 0; index--)
                {
                    if (collection.MemberIds.IndexOf(collection.MemberIds[index]) != index)
                    {
                        collection.MemberIds.RemoveAt(index);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                BetterHierarchyProjectSettings.SaveNow();
            }
        }
    }
}
