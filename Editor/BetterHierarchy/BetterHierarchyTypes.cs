using System;
using System.Collections.Generic;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal enum BetterHierarchySurface
    {
        Tree,
        Atlas
    }

    internal enum BetterHierarchyMode
    {
        Clean,
        Production,
        Debug,
        Art,
        LevelDesign,
        Custom
    }

    internal enum BetterHierarchyAtlasSource
    {
        Scene,
        Selection,
        Favorites,
        Recent,
        Prefabs
    }

    internal enum BetterHierarchyRuleMatch
    {
        NameEquals,
        NameStartsWith,
        NameContains,
        NameRegex,
        HasComponent,
        Tag,
        Layer,
        Scene,
        Prefab,
        Root,
        Leaf,
        Inactive,
        MissingScript,
        Object
    }

    [Flags]
    internal enum BetterHierarchyDiagnosticFlags
    {
        None = 0,
        MissingScript = 1 << 0,
        MissingReference = 1 << 1,
        BrokenPrefab = 1 << 2,
        InactiveParent = 1 << 3,
        ZeroScale = 1 << 4,
        NegativeScale = 1 << 5,
        DeepHierarchy = 1 << 6,
        FarFromOrigin = 1 << 7,
        DuplicateAudioListener = 1 << 8,
        EmptyOrganizer = 1 << 9,
        DuplicateEventSystem = 1 << 10,
        DuplicateMainCamera = 1 << 11
    }

    [Serializable]
    internal sealed class BetterHierarchyRule
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField] private string name = "Rule";
        [SerializeField] private bool enabled = true;
        [SerializeField] private BetterHierarchyRuleMatch match;
        [SerializeField] private string value = string.Empty;
        [SerializeField] private Color color = new Color(1f, 0.55f, 0.12f, 0.2f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private bool overrideTextColor;
        [SerializeField] private bool bold;
        [SerializeField] private bool header;
        [SerializeField] private bool recursive;
        [SerializeField] private string iconName = string.Empty;
        [SerializeField] private string badge = string.Empty;
        [SerializeField] private int priority;

        internal string Id { get => id; set => id = value; }
        internal string Name { get => name; set => name = value; }
        internal bool Enabled { get => enabled; set => enabled = value; }
        internal BetterHierarchyRuleMatch Match { get => match; set => match = value; }
        internal string Value { get => value; set => this.value = value ?? string.Empty; }
        internal Color Color { get => color; set => color = value; }
        internal Color TextColor { get => textColor; set => textColor = value; }
        internal bool OverrideTextColor { get => overrideTextColor; set => overrideTextColor = value; }
        internal bool Bold { get => bold; set => bold = value; }
        internal bool Header { get => header; set => header = value; }
        internal bool Recursive { get => recursive; set => recursive = value; }
        internal string IconName { get => iconName; set => iconName = value ?? string.Empty; }
        internal string Badge { get => badge; set => badge = value ?? string.Empty; }
        internal int Priority { get => priority; set => priority = value; }

        internal BetterHierarchyRule Clone()
        {
            return new BetterHierarchyRule
            {
                id = Guid.NewGuid().ToString("N"),
                name = name + " Copy",
                enabled = enabled,
                match = match,
                value = value,
                color = color,
                textColor = textColor,
                overrideTextColor = overrideTextColor,
                bold = bold,
                header = header,
                recursive = recursive,
                iconName = iconName,
                badge = badge,
                priority = priority
            };
        }
    }

    [Serializable]
    internal sealed class BetterHierarchyCollection
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField] private string name = "Collection";
        [SerializeField] private Color color = new Color(1f, 0.55f, 0.12f, 0.8f);
        [SerializeField] private List<string> memberIds = new List<string>();

        internal string Id { get => id; set => id = value; }
        internal string Name { get => name; set => name = value; }
        internal Color Color { get => color; set => color = value; }
        internal List<string> MemberIds => memberIds;
    }

    [Serializable]
    internal sealed class BetterHierarchySavedSearch
    {
        [SerializeField] private string name = "Search";
        [SerializeField] private string query = string.Empty;

        internal string Name { get => name; set => name = value ?? string.Empty; }
        internal string Query { get => query; set => query = value ?? string.Empty; }
    }

    [Serializable]
    internal sealed class BetterHierarchySavedSearchSet
    {
        [SerializeField] private List<BetterHierarchySavedSearch> items = new List<BetterHierarchySavedSearch>();

        internal List<BetterHierarchySavedSearch> Items => items;
    }

    internal readonly struct BetterHierarchyStyle
    {
        internal BetterHierarchyStyle(BetterHierarchyRule rule)
        {
            Rule = rule;
            Color = rule?.Color ?? Color.clear;
            TextColor = rule?.TextColor ?? Color.white;
            OverrideTextColor = rule?.OverrideTextColor ?? false;
            Bold = rule?.Bold ?? false;
            Header = rule?.Header ?? false;
            IconName = rule?.IconName ?? string.Empty;
            Badge = rule?.Badge ?? string.Empty;
        }

        internal BetterHierarchyRule Rule { get; }
        internal Color Color { get; }
        internal Color TextColor { get; }
        internal bool OverrideTextColor { get; }
        internal bool Bold { get; }
        internal bool Header { get; }
        internal string IconName { get; }
        internal string Badge { get; }
        internal bool IsValid => Rule != null;
    }
}
