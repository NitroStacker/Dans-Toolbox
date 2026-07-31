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
        public const string NativeWindowDockId = "native-window-dock";

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
                    NativeWindowDockId,
                    "Native Window Dock",
                    "Embed interactive Windows applications in resizable Unity tabs.",
                    true,
                    true)
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
        [UnityEngine.SerializeField] private bool recommendedLayoutSelected;

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

            return settings.enabledToolIds.Contains(toolId);
        }

        public static void Apply(
            DansToolboxThemeId selectedTheme,
            IEnumerable<string> enabledTools,
            bool useRecommendedLayout)
        {
            DansToolboxSettings settings = instance;
            settings.initialized = true;
            settings.setupPromptDismissed = false;
            settings.setupCompletedVersion = CurrentPackageVersion;
            settings.setupPromptDismissedVersion = string.Empty;
            settings.setupRequiredAfterReinstall = false;
            settings.theme = selectedTheme;
            settings.enabledToolIds = new List<string>(enabledTools ?? Array.Empty<string>());
            settings.recommendedLayoutSelected = useRecommendedLayout;
            settings.Save(true);
            DansToolboxTheme.NotifyChanged();
            EditorApplication.delayCall += DansToolboxLayoutInstaller.CloseDisabledToolWindows;
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
    }
}
