using System;
using System.Collections.Generic;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterProject
{
    internal enum BetterProjectSurface
    {
        Browse,
        Library,
        Impact
    }

    internal enum BetterProjectView
    {
        List,
        Grid,
        Details
    }

    internal enum BetterProjectSort
    {
        Name,
        Type,
        Size,
        Modified
    }

    internal enum BetterProjectSearchScope
    {
        Assets,
        Packages,
        All
    }

    internal enum BetterProjectLibrarySource
    {
        All,
        Favorites,
        Recent,
        Issues,
        Duplicates,
        Large,
        Unused,
        Collection
    }

    internal enum BetterProjectCollectionKind
    {
        Manual,
        Smart
    }

    internal enum BetterProjectRuleMatch
    {
        PathStartsWith,
        NameContains,
        Type,
        Extension,
        Label,
        Package,
        Folder,
        Diagnostic,
        Asset
    }

    internal enum BetterProjectAssetKind
    {
        Asset,
        Folder,
        Prefab,
        Model,
        Sprite,
        Texture
    }

    [Flags]
    internal enum BetterProjectDiagnosticFlags
    {
        None = 0,
        MissingAsset = 1 << 0,
        MissingScript = 1 << 1,
        MissingShader = 1 << 2,
        Oversized = 1 << 3,
        EmptyFolder = 1 << 4,
        DuplicateName = 1 << 5,
        Naming = 1 << 6,
        EditorPlacement = 1 << 7,
        Unreferenced = 1 << 8,
        Importer = 1 << 9
    }

    internal sealed class BetterProjectAssetRecord
    {
        internal string Guid;
        internal string Path;
        internal string ParentPath;
        internal string Name;
        internal string Extension;
        internal Type MainType;
        internal BetterProjectAssetKind Kind;
        internal bool IsFolder;
        internal bool IsPackage;
        internal bool IsReadOnly;
        internal long FileSize;
        internal DateTime ModifiedUtc;
        internal int DirectDependencyCount;
        internal int ReferenceCount;

        internal string TypeName
        {
            get
            {
                switch (Kind)
                {
                    case BetterProjectAssetKind.Folder: return "Folder";
                    case BetterProjectAssetKind.Prefab: return "Prefab";
                    case BetterProjectAssetKind.Model: return "Model";
                    case BetterProjectAssetKind.Sprite: return "Sprite";
                    case BetterProjectAssetKind.Texture: return "Texture";
                    default: return MainType == null ? "Asset" : MainType.Name;
                }
            }
        }
    }

    [Serializable]
    internal sealed class BetterProjectStyleRule
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField] private string name = "Rule";
        [SerializeField] private bool enabled = true;
        [SerializeField] private BetterProjectRuleMatch match;
        [SerializeField] private string value = string.Empty;
        [SerializeField] private Color color = new Color(1f, 0.55f, 0.12f, 0.9f);
        [SerializeField] private string badge = string.Empty;
        [SerializeField] private string iconName = string.Empty;
        [SerializeField] private int priority;

        internal string Id { get => id; set => id = value ?? string.Empty; }
        internal string Name { get => name; set => name = value ?? string.Empty; }
        internal bool Enabled { get => enabled; set => enabled = value; }
        internal BetterProjectRuleMatch Match { get => match; set => match = value; }
        internal string Value { get => value; set => this.value = value ?? string.Empty; }
        internal Color Color { get => color; set => color = value; }
        internal string Badge { get => badge; set => badge = value ?? string.Empty; }
        internal string IconName { get => iconName; set => iconName = value ?? string.Empty; }
        internal int Priority { get => priority; set => priority = value; }

        internal BetterProjectStyleRule Clone()
        {
            return new BetterProjectStyleRule
            {
                id = Guid.NewGuid().ToString("N"),
                name = name + " Copy",
                enabled = enabled,
                match = match,
                value = value,
                color = color,
                badge = badge,
                iconName = iconName,
                priority = priority
            };
        }
    }

    [Serializable]
    internal sealed class BetterProjectCollection
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField] private string name = "Collection";
        [SerializeField] private BetterProjectCollectionKind kind;
        [SerializeField] private string query = string.Empty;
        [SerializeField] private Color color = new Color(1f, 0.55f, 0.12f, 0.9f);
        [SerializeField] private List<string> assetGuids = new List<string>();

        internal string Id { get => id; set => id = value ?? string.Empty; }
        internal string Name { get => name; set => name = value ?? string.Empty; }
        internal BetterProjectCollectionKind Kind { get => kind; set => kind = value; }
        internal string Query { get => query; set => query = value ?? string.Empty; }
        internal Color Color { get => color; set => color = value; }
        internal List<string> AssetGuids => assetGuids;
    }

    [Serializable]
    internal sealed class BetterProjectSavedSearch
    {
        [SerializeField] private string name = "Search";
        [SerializeField] private string query = string.Empty;

        internal string Name { get => name; set => name = value ?? string.Empty; }
        internal string Query { get => query; set => query = value ?? string.Empty; }
    }

    internal readonly struct BetterProjectStyle
    {
        internal BetterProjectStyle(BetterProjectStyleRule rule)
        {
            Rule = rule;
            Color = rule == null ? Color.clear : rule.Color;
            Badge = rule == null ? string.Empty : rule.Badge;
            IconName = rule == null ? string.Empty : rule.IconName;
        }

        internal BetterProjectStyleRule Rule { get; }
        internal Color Color { get; }
        internal string Badge { get; }
        internal string IconName { get; }
        internal bool IsValid => Rule != null;
    }
}
