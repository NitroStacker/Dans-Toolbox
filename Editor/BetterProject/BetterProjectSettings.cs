using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterProject
{
    [FilePath("ProjectSettings/BetterProjectSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class BetterProjectSettings : ScriptableSingleton<BetterProjectSettings>
    {
        [SerializeField] private bool initialized;
        [SerializeField] private bool showPackages = true;
        [SerializeField] private bool showPreview = true;
        [SerializeField] private bool showFolderRail = true;
        [SerializeField] private List<BetterProjectStyleRule> rules = new List<BetterProjectStyleRule>();
        [SerializeField] private List<BetterProjectCollection> collections = new List<BetterProjectCollection>();
        [SerializeField] private List<BetterProjectSavedSearch> savedSearches = new List<BetterProjectSavedSearch>();

        internal static bool ShowPackages
        {
            get { EnsureInitialized(); return instance.showPackages; }
            set { EnsureInitialized(); instance.showPackages = value; SaveNow(); }
        }

        internal static bool ShowPreview
        {
            get { EnsureInitialized(); return instance.showPreview; }
            set { EnsureInitialized(); instance.showPreview = value; SaveNow(); }
        }

        internal static bool ShowFolderRail
        {
            get { EnsureInitialized(); return instance.showFolderRail; }
            set { EnsureInitialized(); instance.showFolderRail = value; SaveNow(); }
        }

        internal static List<BetterProjectStyleRule> Rules
        {
            get { EnsureInitialized(); return instance.rules; }
        }

        internal static List<BetterProjectCollection> Collections
        {
            get { EnsureInitialized(); return instance.collections; }
        }

        internal static List<BetterProjectSavedSearch> SavedSearches
        {
            get { EnsureInitialized(); return instance.savedSearches; }
        }

        internal static void RecordUndo(string name)
        {
            EnsureInitialized();
            Undo.RecordObject(instance, name);
        }

        internal static void SaveNow()
        {
            EnsureInitialized();
            instance.Save(true);
            BetterProjectIndex.InvalidatePresentation();
        }

        internal static BetterProjectCollection CreateCollection(
            string name,
            BetterProjectCollectionKind kind,
            string query,
            IEnumerable<string> guids)
        {
            RecordUndo("Create Better Project Collection");
            var collection = new BetterProjectCollection
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Collection" : name.Trim(),
                Kind = kind,
                Query = query ?? string.Empty
            };
            if (guids != null)
            {
                collection.AssetGuids.AddRange(
                    guids.Where(guid => !string.IsNullOrEmpty(guid)).Distinct());
            }
            Collections.Add(collection);
            SaveNow();
            return collection;
        }

        internal static bool RemoveCollection(BetterProjectCollection collection)
        {
            if (collection == null || !Collections.Contains(collection))
            {
                return false;
            }
            RecordUndo("Delete Better Project Collection");
            Collections.Remove(collection);
            SaveNow();
            return true;
        }

        internal static void EnsureInitialized()
        {
            if (instance.initialized)
            {
                instance.rules ??= new List<BetterProjectStyleRule>();
                instance.collections ??= new List<BetterProjectCollection>();
                instance.savedSearches ??= new List<BetterProjectSavedSearch>();
                if (MigrateDefaultDiagnosticRule()) instance.Save(true);
                return;
            }

            instance.initialized = true;
            instance.rules = CreateDefaultRules();
            instance.collections = new List<BetterProjectCollection>();
            instance.savedSearches = new List<BetterProjectSavedSearch>();
            instance.Save(true);
        }

        private static List<BetterProjectStyleRule> CreateDefaultRules()
        {
            return new List<BetterProjectStyleRule>
            {
                Rule("Scenes", BetterProjectRuleMatch.Extension, ".unity", new Color32(82, 196, 255, 255), "SCN", 80),
                Rule("Prefabs", BetterProjectRuleMatch.Type, "Prefab", new Color32(76, 166, 255, 255), "PFB", 70),
                Rule("Models", BetterProjectRuleMatch.Type, "Model", new Color32(86, 176, 224, 255), "MDL", 65),
                Rule("Scripts", BetterProjectRuleMatch.Extension, ".cs", new Color32(114, 216, 146, 255), "CS", 60),
                Rule("Audio", BetterProjectRuleMatch.Type, "AudioClip", new Color32(214, 112, 255, 255), "SFX", 55),
                Rule("Sprites", BetterProjectRuleMatch.Type, "Sprite", new Color32(255, 166, 102, 255), "SPR", 52),
                Rule("Textures", BetterProjectRuleMatch.Type, "Texture", new Color32(255, 152, 92, 255), "TEX", 50),
                Rule("Materials", BetterProjectRuleMatch.Type, "Material", new Color32(245, 192, 86, 255), "MAT", 45),
                Rule("Packages", BetterProjectRuleMatch.Package, string.Empty, new Color32(132, 139, 151, 255), "PKG", 20),
                Rule("Issues", BetterProjectRuleMatch.Diagnostic, "critical", new Color32(235, 98, 105, 255), "!", 100)
            };
        }

        private static bool MigrateDefaultDiagnosticRule()
        {
            BetterProjectStyleRule defaultIssueRule = instance.rules.FirstOrDefault(rule =>
                rule != null &&
                rule.Name == "Issues" &&
                rule.Match == BetterProjectRuleMatch.Diagnostic &&
                rule.Value == "any" &&
                rule.Badge == "!" &&
                rule.Priority == 100);
            if (defaultIssueRule == null) return false;
            defaultIssueRule.Value = "critical";
            return true;
        }

        private static BetterProjectStyleRule Rule(
            string name,
            BetterProjectRuleMatch match,
            string value,
            Color color,
            string badge,
            int priority)
        {
            return new BetterProjectStyleRule
            {
                Name = name,
                Match = match,
                Value = value,
                Color = color,
                Badge = badge,
                Priority = priority
            };
        }
    }

    [FilePath("UserSettings/BetterProjectUserSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class BetterProjectUserSettings : ScriptableSingleton<BetterProjectUserSettings>
    {
        private const int MaxRecent = 24;

        [SerializeField] private List<string> favoriteGuids = new List<string>();
        [SerializeField] private List<string> recentAssetGuids = new List<string>();
        [SerializeField] private List<string> recentFolders = new List<string>();

        internal static IReadOnlyList<string> FavoriteGuids => instance.favoriteGuids;
        internal static IReadOnlyList<string> RecentAssetGuids => instance.recentAssetGuids;
        internal static IReadOnlyList<string> RecentFolders => instance.recentFolders;

        internal static bool IsFavorite(string guid)
        {
            return !string.IsNullOrEmpty(guid) && instance.favoriteGuids.Contains(guid);
        }

        internal static bool ToggleFavorite(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }
            if (!instance.favoriteGuids.Remove(guid))
            {
                instance.favoriteGuids.Insert(0, guid);
            }
            instance.Save(true);
            BetterProjectIndex.InvalidatePresentation();
            return instance.favoriteGuids.Contains(guid);
        }

        internal static void TouchAsset(string guid)
        {
            Touch(instance.recentAssetGuids, guid);
            instance.Save(true);
        }

        internal static void TouchFolder(string path)
        {
            Touch(instance.recentFolders, path);
            instance.Save(true);
        }

        private static void Touch(List<string> values, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            values.Remove(value);
            values.Insert(0, value);
            if (values.Count > MaxRecent)
            {
                values.RemoveRange(MaxRecent, values.Count - MaxRecent);
            }
        }
    }
}
