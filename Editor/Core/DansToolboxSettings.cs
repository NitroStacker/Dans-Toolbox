using System;
using System.Collections.Generic;
using UnityEditor;

namespace DansToolbox.Editor
{
    [InitializeOnLoad]
    internal static class DansToolboxPackageLifecycle
    {
        private const string PackageName = "com.dans.toolbox";

        static DansToolboxPackageLifecycle()
        {
            UnityEditor.PackageManager.Events.registeringPackages -=
                OnPackagesRegistering;
            UnityEditor.PackageManager.Events.registeringPackages +=
                OnPackagesRegistering;
        }

        private static void OnPackagesRegistering(
            UnityEditor.PackageManager.PackageRegistrationEventArgs changes)
        {
            foreach (UnityEditor.PackageManager.PackageInfo package in changes.removed)
            {
                if (string.Equals(package.name, PackageName, StringComparison.Ordinal))
                {
                    DansToolboxSettings.MarkPackageRemoved();
                    return;
                }
            }
        }
    }

    public readonly struct DansToolboxToolDescriptor
    {
        public DansToolboxToolDescriptor(
            string id,
            string name,
            string description,
            bool defaultEnabled,
            bool windowsOnly)
        {
            Id = id;
            Name = name;
            Description = description;
            DefaultEnabled = defaultEnabled;
            WindowsOnly = windowsOnly;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public bool DefaultEnabled { get; }
        public bool WindowsOnly { get; }
    }

    public static class DansToolboxTools
    {
        public const string RetroSfxId = "retro-sfx";
        public const string RetroVfxId = "retro-vfx";
        public const string NativeWindowDockId = "native-window-dock";
        public const string BetterHierarchyId = "better-hierarchy";
        public const string BetterInspectorId = "better-inspector";
        public const string BetterProjectId = "better-project";
        public const string BetterConsoleId = "better-console";
        public const string BetterSceneId = "better-scene";
        public const string ArtboardId = "artboard";

        private static readonly IReadOnlyList<DansToolboxToolDescriptor> descriptors =
            new[]
            {
                new DansToolboxToolDescriptor(
                    RetroSfxId,
                    "Retro SFX",
                    "Synthesize, import, process, preview, and render game-ready sound effects.",
                    true,
                    false),
                new DansToolboxToolDescriptor(
                    RetroVfxId,
                    "Retro VFX",
                    "Forge, preview, import, and export procedural particle effects and flipbooks.",
                    true,
                    false),
                new DansToolboxToolDescriptor(
                    NativeWindowDockId,
                    "Native Window Dock",
                    "Embed interactive Windows applications in independently placed panels.",
                    true,
                    true),
                new DansToolboxToolDescriptor(
                    BetterHierarchyId,
                    "Better Hierarchy",
                    "Navigate, organize, inspect, and preview scene objects in a visual hierarchy.",
                    true,
                    false),
                new DansToolboxToolDescriptor(
                    BetterInspectorId,
                    "Better Inspector",
                    "Inspect faster with search, pinned targets, component cards, favorites, and diagnostics.",
                    true,
                    false),
                new DansToolboxToolDescriptor(
                    BetterProjectId,
                    "Better Project",
                    "Browse, organize, preview, diagnose, and trace every project asset.",
                    true,
                    false),
                new DansToolboxToolDescriptor(
                    BetterConsoleId,
                    "Better Console",
                    "Find, group, triage, compare, and resolve Unity logs faster.",
                    true,
                    false),
                new DansToolboxToolDescriptor(
                    BetterSceneId,
                    "Better Scene",
                    "Place, align, isolate, measure, review, and revisit scene content faster.",
                    true,
                    false),
                new DansToolboxToolDescriptor(
                    ArtboardId,
                    "Artboard",
                    "Draw, layer, animate, and export crisp high-resolution sprites.",
                    true,
                    false)
            };

        public static IReadOnlyList<DansToolboxToolDescriptor> All => descriptors;

        public static DansToolboxToolDescriptor Find(string id)
        {
            foreach (DansToolboxToolDescriptor descriptor in descriptors)
            {
                if (string.Equals(descriptor.Id, id, StringComparison.Ordinal))
                {
                    return descriptor;
                }
            }

            return default;
        }
    }

    public static class DansToolboxTransientAssets
    {
        public const string RetroSfxPreviewPath =
            "Assets/Editor/RetroSfxGenerator/__RetroSfxPreview.wav";
    }

    [FilePath("ProjectSettings/DansToolboxSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class DansToolboxSettings : ScriptableSingleton<DansToolboxSettings>
    {
        [UnityEngine.SerializeField] private bool initialized;
        // Retained so existing project settings deserialize without churn.
        [UnityEngine.SerializeField] private bool setupPromptDismissed;
        [UnityEngine.SerializeField] private string setupCompletedVersion = string.Empty;
        [UnityEngine.SerializeField] private string setupPromptDismissedVersion = string.Empty;
        [UnityEngine.SerializeField] private bool setupRequiredAfterReinstall;
        [UnityEngine.SerializeField] private DansToolboxThemeId theme =
            DansToolboxThemeId.SignalOrange;
        [UnityEngine.SerializeField] private List<string> enabledToolIds = new List<string>();
        [UnityEngine.SerializeField] private List<string> knownToolIds = new List<string>();
        [UnityEngine.SerializeField] private bool recommendedLayoutSelected;
        [UnityEngine.SerializeField] private bool seamlessToolSurfaces = true;

        public static bool IsInitialized => instance.initialized;
        public static bool ShouldOfferSetup => ShouldOfferSetupForVersion(
            instance.initialized,
            instance.setupPromptDismissed,
            instance.setupCompletedVersion,
            instance.setupPromptDismissedVersion,
            instance.setupRequiredAfterReinstall,
            CurrentPackageVersion);
        public static DansToolboxThemeId Theme => instance.theme;
        public static bool RecommendedLayoutSelected => instance.recommendedLayoutSelected;
        public static bool SeamlessToolSurfaces => instance.seamlessToolSurfaces;

        internal static string CurrentPackageVersion
        {
            get
            {
                UnityEditor.PackageManager.PackageInfo package =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                        typeof(DansToolboxSettings).Assembly);
                return package?.version ?? string.Empty;
            }
        }

        internal static bool ShouldOfferSetupForVersion(
            bool isInitialized,
            bool legacyPromptDismissed,
            string completedVersion,
            string dismissedVersion,
            bool requiredAfterReinstall,
            string currentVersion)
        {
            if (requiredAfterReinstall)
            {
                return true;
            }

            if (string.IsNullOrEmpty(currentVersion))
            {
                return !isInitialized && !legacyPromptDismissed;
            }

            return !string.Equals(
                       completedVersion,
                       currentVersion,
                       StringComparison.Ordinal) &&
                   !string.Equals(
                       dismissedVersion,
                       currentVersion,
                       StringComparison.Ordinal);
        }

        public static bool IsToolEnabled(string toolId)
        {
            DansToolboxSettings settings = instance;
            if (!settings.initialized)
            {
                return DansToolboxTools.Find(toolId).DefaultEnabled;
            }

            settings.EnsureKnownToolsMigrated();
            return settings.enabledToolIds.Contains(toolId) ||
                   (!settings.knownToolIds.Contains(toolId) &&
                    DansToolboxTools.Find(toolId).DefaultEnabled);
        }

        public static void Apply(
            DansToolboxThemeId selectedTheme,
            IEnumerable<string> enabledTools,
            bool useRecommendedLayout,
            bool useSeamlessToolSurfaces)
        {
            DansToolboxSettings settings = instance;
            settings.initialized = true;
            settings.setupPromptDismissed = false;
            settings.setupCompletedVersion = CurrentPackageVersion;
            settings.setupPromptDismissedVersion = string.Empty;
            settings.setupRequiredAfterReinstall = false;
            settings.theme = selectedTheme;
            settings.enabledToolIds = new List<string>(enabledTools ?? Array.Empty<string>());
            settings.knownToolIds = new List<string>();
            foreach (DansToolboxToolDescriptor tool in DansToolboxTools.All)
            {
                settings.knownToolIds.Add(tool.Id);
            }
            settings.recommendedLayoutSelected = useRecommendedLayout;
            settings.seamlessToolSurfaces = useSeamlessToolSurfaces;
            settings.Save(true);
            DansToolboxTheme.NotifyChanged();
            if (useRecommendedLayout)
            {
                DansToolboxLayoutInstaller.ApplyRecommendedLayout();
            }
            else
            {
                EditorApplication.delayCall +=
                    DansToolboxLayoutInstaller.CloseDisabledToolWindows;
            }
        }

        public static void DismissSetupPrompt()
        {
            DansToolboxSettings settings = instance;
            settings.setupPromptDismissed = true;
            settings.setupPromptDismissedVersion = CurrentPackageVersion;
            settings.setupRequiredAfterReinstall = false;
            settings.Save(true);
        }

        internal static void MarkPackageRemoved()
        {
            DansToolboxSettings settings = instance;
            settings.setupRequiredAfterReinstall = true;
            settings.Save(true);
        }

        private void EnsureKnownToolsMigrated()
        {
            knownToolIds ??= new List<string>();
            if (knownToolIds.Count > 0)
            {
                return;
            }

            // Settings written before the catalog tracked known tools belong to
            // the first three releases. Preserve their explicit on/off choices,
            // while allowing newly introduced default tools to opt in once.
            knownToolIds.Add(DansToolboxTools.RetroSfxId);
            knownToolIds.Add(DansToolboxTools.NativeWindowDockId);
            knownToolIds.Add(DansToolboxTools.BetterHierarchyId);
        }
    }
}
