using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace DansToolbox.Editor.Tests
{
    internal sealed class DansToolboxConfigurationTests
    {
        [Test]
        public void Themes_HaveDistinctAccentColors()
        {
            var accents = new HashSet<string>();
            foreach (DansToolboxThemeId theme in
                     (DansToolboxThemeId[])System.Enum.GetValues(typeof(DansToolboxThemeId)))
            {
                accents.Add(DansToolboxTheme.GetPalette(theme).Accent.ToString());
            }

            Assert.That(accents.Count, Is.EqualTo(3));
        }

        [Test]
        public void SeamlessPalette_UnifiesOuterSurfaceWithoutRemovingHierarchy()
        {
            DansToolboxPalette standard = DansToolboxTheme.GetPalette(
                DansToolboxThemeId.SignalOrange,
                false);
            DansToolboxPalette seamless = DansToolboxTheme.GetPalette(
                DansToolboxThemeId.SignalOrange,
                true);

            Assert.That(standard.Canvas, Is.Not.EqualTo(standard.Panel));
            Assert.That(seamless.Canvas, Is.EqualTo(seamless.Panel));
            Assert.That(seamless.Inset, Is.Not.EqualTo(seamless.Panel));
            Assert.That(seamless.Border, Is.Not.EqualTo(standard.Border));
            Assert.That(seamless.Accent, Is.EqualTo(standard.Accent));
        }

        [Test]
        public void ToolCatalog_UsesUniqueIds()
        {
            var ids = new HashSet<string>();
            foreach (DansToolboxToolDescriptor tool in DansToolboxTools.All)
            {
                Assert.That(tool.Id, Is.Not.Empty);
                Assert.That(ids.Add(tool.Id), Is.True, "Duplicate tool id: " + tool.Id);
            }

            Assert.That(ids, Does.Contain(DansToolboxTools.RetroSfxId));
            Assert.That(ids, Does.Contain(DansToolboxTools.RetroVfxId));
            Assert.That(ids, Does.Contain(DansToolboxTools.NativeWindowDockId));
            Assert.That(ids, Does.Contain(DansToolboxTools.BetterHierarchyId));
            Assert.That(ids, Does.Contain(DansToolboxTools.BetterInspectorId));
            Assert.That(ids, Does.Contain(DansToolboxTools.BetterProjectId));
            Assert.That(ids, Does.Contain(DansToolboxTools.BetterConsoleId));
            Assert.That(ids, Does.Contain(DansToolboxTools.BetterSceneId));
        }

        [Test]
        public void ToolboxHub_NativeToolIconsResolve()
        {
            foreach (DansToolboxLaunchDescriptor descriptor in DansToolboxToolLauncher.All)
            {
                Assert.That(
                    EditorGUIUtility.IconContent(descriptor.IconName).image,
                    Is.Not.Null,
                    "Missing Unity icon: " + descriptor.IconName);
            }
        }

        [Test]
        public void ToolboxWindows_UseDistinctIconOnlyCompactTitles()
        {
            var cacheKeys = new HashSet<string>();
            var iconIds = new HashSet<int>();
            foreach (DansToolboxLaunchDescriptor descriptor in DansToolboxToolLauncher.All)
            {
                GUIContent title = DansToolboxWindowChrome.CreateCompactTitle(
                    descriptor.Id);
                Assert.That(
                    DansToolboxWindowChrome.StripInvisibleCacheKey(title.text),
                    Is.Empty,
                    descriptor.Id);
                Assert.That(cacheKeys.Add(title.text), Is.True, descriptor.Id);
                Assert.That(title.image, Is.Not.Null, descriptor.Id);
                Assert.That(
                    iconIds.Add(title.image.GetInstanceID()),
                    Is.True,
                    "Duplicate Unity icon: " + descriptor.IconName);
                Assert.That(
                    title.tooltip,
                    Is.EqualTo(DansToolboxTools.Find(descriptor.Id).Name),
                    descriptor.Id);
            }

            GUIContent nativeDockPanel = DansToolboxWindowChrome.CreateCompactTitle(
                DansToolboxTools.NativeWindowDockId,
                null,
                "Native Dock 3");
            Assert.That(
                DansToolboxWindowChrome.StripInvisibleCacheKey(nativeDockPanel.text),
                Is.Empty);
            Assert.That(nativeDockPanel.image, Is.Not.Null);
            Assert.That(nativeDockPanel.tooltip, Is.EqualTo("Native Dock 3"));
        }

        [Test]
        public void ToolboxWindowChrome_ResolvesInteractiveTabStyleInternals()
        {
            System.Type dockArea = typeof(EditorWindow).Assembly.GetType(
                "UnityEditor.DockArea",
                true);
            System.Type styles = typeof(EditorWindow).Assembly.GetType(
                "UnityEditor.DockArea+Styles",
                true);

            Assert.That(
                dockArea.GetField(
                    "tabStyle",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)?.FieldType,
                Is.EqualTo(typeof(GUIStyle)));
            foreach (string fieldName in new[]
                     {
                         "dragTab",
                         "dragTabFirst",
                         "tabLabel"
                     })
            {
                Assert.That(
                    styles.GetField(
                        fieldName,
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)?.FieldType,
                    Is.EqualTo(typeof(GUIStyle)),
                    fieldName);
            }
        }

        [Test]
        public void OrganizedWorkspace_CatalogsEveryToolWindowWithoutLoadingALayout()
        {
            Assert.That(DansToolboxLayoutInstaller.IsLayoutAvailable, Is.True);
            Assert.That(
                DansToolboxToolLauncher.KnownWindowTypeNames.Count(),
                Is.EqualTo(DansToolboxTools.All.Count));
            foreach (DansToolboxToolDescriptor tool in DansToolboxTools.All)
            {
                Assert.That(
                    DansToolboxToolLauncher.Find(tool.Id).TypeName,
                    Is.Not.Empty,
                    "No launcher registration for " + tool.Id);
            }
            Assert.That(
                DansToolboxToolLauncher.KnownWindowTypeNames.Any(name =>
                    name.Contains("DansToolbox.RetroSfx.Editor")),
                Is.True);
            Assert.That(
                DansToolboxToolLauncher.KnownWindowTypeNames.Any(name =>
                    name.Contains("DansToolbox.NativeWindowDock.Editor")),
                Is.True);
            Assert.That(
                DansToolboxToolLauncher.KnownWindowTypeNames.Any(name =>
                    name.Contains("DansToolbox.BetterScene.Editor")),
                Is.True);
        }

        [Test]
        public void ToolPlacement_StaysInsideTheEditorWorkArea()
        {
            Rect main = new Rect(100f, 50f, 1600f, 900f);
            foreach (DansToolboxPlacement placement in
                     (DansToolboxPlacement[])System.Enum.GetValues(typeof(DansToolboxPlacement)))
            {
                Rect result = DansToolboxToolLauncher.CalculateRect(
                    main,
                    placement,
                    new Vector2(900f, 700f));
                Assert.That(result.xMin, Is.GreaterThanOrEqualTo(main.xMin));
                Assert.That(result.yMin, Is.GreaterThanOrEqualTo(main.yMin));
                Assert.That(result.xMax, Is.LessThanOrEqualTo(main.xMax));
                Assert.That(result.yMax, Is.LessThanOrEqualTo(main.yMax));
            }
        }

        [Test]
        public void ToolDefaults_UseRealDockTargets()
        {
            DansToolboxLaunchDescriptor nativeDock = DansToolboxToolLauncher.Find(
                DansToolboxTools.NativeWindowDockId);
            DansToolboxLaunchDescriptor retroSfx = DansToolboxToolLauncher.Find(
                DansToolboxTools.RetroSfxId);
            DansToolboxLaunchDescriptor retroVfx = DansToolboxToolLauncher.Find(
                DansToolboxTools.RetroVfxId);

            Assert.That(nativeDock.AllowsMultiple, Is.True);
            Assert.That(
                nativeDock.DefaultPlacement,
                Is.EqualTo(DansToolboxPlacement.DockPicker));
            Assert.That(
                retroSfx.DefaultPlacement,
                Is.EqualTo(DansToolboxPlacement.InspectorDock));
            Assert.That(
                retroVfx.DefaultPlacement,
                Is.EqualTo(DansToolboxPlacement.InspectorDock));
        }

        [Test]
        public void UnityDockingAdapter_ResolvesRequiredUnityInternals()
        {
            Assert.That(DansToolboxDocking.IsSupported, Is.True);
        }

        [Test]
        public void ToolboxHub_UsesResponsiveThumbnailColumns()
        {
            Assert.That(DansToolboxHubWindow.CalculateColumnCount(320f), Is.EqualTo(1));
            Assert.That(DansToolboxHubWindow.CalculateColumnCount(340f), Is.EqualTo(2));
            Assert.That(DansToolboxHubWindow.CalculateColumnCount(539f), Is.EqualTo(2));
            Assert.That(DansToolboxHubWindow.CalculateColumnCount(540f), Is.EqualTo(3));
            Assert.That(DansToolboxHubWindow.CalculateColumnCount(899f), Is.EqualTo(3));
            Assert.That(DansToolboxHubWindow.CalculateColumnCount(900f), Is.EqualTo(4));
        }

        [TestCase(440f, 480f)]
        [TestCase(680f, 480f)]
        [TestCase(680f, 650f)]
        public void ToolboxHub_GalleryNeverRunsUnderTheFooter(float width, float height)
        {
            DansToolboxHubWindow.HubLayoutRegions layout =
                DansToolboxHubWindow.CalculateLayout(new Vector2(width, height));

            Assert.That(layout.Search.yMin, Is.GreaterThanOrEqualTo(layout.Header.yMax));
            Assert.That(layout.Filter.yMin, Is.GreaterThanOrEqualTo(layout.Search.yMax));
            Assert.That(layout.Gallery.yMin, Is.GreaterThanOrEqualTo(layout.Filter.yMax));
            Assert.That(layout.Gallery.yMax, Is.LessThanOrEqualTo(layout.Footer.yMin));
            Assert.That(layout.Footer.yMax, Is.LessThanOrEqualTo(height));
        }

        [Test]
        public void SetupWizard_OverlayPanelStaysCenteredAndInsideCanvas()
        {
            Rect panel = DansToolboxSetupWizard.CalculatePanelRect(new Vector2(1200f, 800f));

            Assert.That(panel.center.x, Is.EqualTo(600f).Within(0.01f));
            Assert.That(panel.center.y, Is.EqualTo(400f).Within(0.01f));
            Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(panel.xMax, Is.LessThanOrEqualTo(1200f));
            Assert.That(panel.yMax, Is.LessThanOrEqualTo(800f));
        }

        [Test]
        public void SetupPrompt_OpensOnceForEveryPackageVersion()
        {
            Assert.That(
                DansToolboxSettings.ShouldOfferSetupForVersion(
                    true,
                    false,
                    "1.3.0",
                    string.Empty,
                    false,
                    "1.3.1"),
                Is.True,
                "A package update should offer setup again.");
            Assert.That(
                DansToolboxSettings.ShouldOfferSetupForVersion(
                    true,
                    false,
                    "1.3.1",
                    string.Empty,
                    false,
                    "1.3.1"),
                Is.False,
                "Applying setup should acknowledge the current version.");
            Assert.That(
                DansToolboxSettings.ShouldOfferSetupForVersion(
                    true,
                    true,
                    "1.3.0",
                    "1.3.1",
                    false,
                    "1.3.1"),
                Is.False,
                "Dismissing setup should suppress repeats for the current version.");
            Assert.That(
                DansToolboxSettings.ShouldOfferSetupForVersion(
                    true,
                    true,
                    "1.3.0",
                    "1.3.1",
                    false,
                    "1.3.2"),
                Is.True,
                "A later update should offer setup after an earlier dismissal.");
            Assert.That(
                DansToolboxSettings.ShouldOfferSetupForVersion(
                    true,
                    false,
                    "1.3.1",
                    string.Empty,
                    true,
                    "1.3.1"),
                Is.True,
                "Reinstalling the same version should offer setup again.");
        }

        [Test]
        public void ToolbarButton_CanBeCreated()
        {
            Assert.That(DansToolboxToolbarButton.Create(), Is.Not.Null);
        }

        [Test]
        public void ToolbarButton_RegistersInVisibleLeftGroup()
        {
            MethodInfo factory = typeof(DansToolboxToolbarButton).GetMethod(
                "Create",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MainToolbarElementAttribute attribute =
                factory?.GetCustomAttribute<MainToolbarElementAttribute>();

            Assert.That(attribute, Is.Not.Null);
            Assert.That(
                DansToolboxToolbarButton.ElementPath,
                Is.EqualTo("Dans Toolbox/Toolbox Hub"));
            Assert.That(attribute.path, Is.EqualTo(DansToolboxToolbarButton.ElementPath));
            Assert.That(attribute.defaultDockPosition, Is.EqualTo(MainToolbarDockPosition.Left));
            Assert.That(attribute.defaultDockIndex, Is.EqualTo(2));
        }

        [Test]
        public void SetupWizard_MovesThroughThreeBoundedSteps()
        {
            DansToolboxSetupWizard wizard =
                ScriptableObject.CreateInstance<DansToolboxSetupWizard>();
            try
            {
                Assert.That(wizard.CurrentStep, Is.EqualTo(DansToolboxSetupStep.Theme));
                wizard.MoveNext();
                Assert.That(wizard.CurrentStep, Is.EqualTo(DansToolboxSetupStep.Tools));
                wizard.MoveNext();
                Assert.That(wizard.CurrentStep, Is.EqualTo(DansToolboxSetupStep.Layout));
                wizard.MoveNext();
                Assert.That(wizard.CurrentStep, Is.EqualTo(DansToolboxSetupStep.Layout));
                wizard.MoveBack();
                Assert.That(wizard.CurrentStep, Is.EqualTo(DansToolboxSetupStep.Tools));
                wizard.MoveBack();
                wizard.MoveBack();
                Assert.That(wizard.CurrentStep, Is.EqualTo(DansToolboxSetupStep.Theme));
            }
            finally
            {
                Object.DestroyImmediate(wizard);
            }
        }

        [Test]
        public void SetupWizard_InstalledIconSpringOvershootsAndSettles()
        {
            float[] samples = Enumerable.Range(0, 101)
                .Select(index => DansToolboxSetupWizard.CalculateInstallIconScale(index / 100f))
                .ToArray();

            Assert.That(samples[0], Is.EqualTo(0f).Within(0.0001f));
            Assert.That(samples.Max(), Is.GreaterThan(1.05f));
            Assert.That(samples[samples.Length - 1], Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void SetupWizard_InstalledOverlayHoldsThenFadesOut()
        {
            Assert.That(
                DansToolboxSetupWizard.CalculateInstallOverlayOpacity(0.5f),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                DansToolboxSetupWizard.CalculateInstallOverlayOpacity(0.86f),
                Is.InRange(0.35f, 0.65f));
            Assert.That(
                DansToolboxSetupWizard.CalculateInstallOverlayOpacity(1f),
                Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ToolReveal_UsesCinematicDurationAndStaggeredBands()
        {
            Assert.That(DansToolboxMotion.RevealDuration, Is.GreaterThanOrEqualTo(1.2d));
            Assert.That(
                DansToolboxMotion.CalculateRevealBandProgress(0.2f, 0),
                Is.GreaterThan(DansToolboxMotion.CalculateRevealBandProgress(0.2f, 3)));
            Assert.That(
                DansToolboxMotion.CalculateRevealBandProgress(1f, 3),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void PackageAssets_HaveUniqueGuids()
        {
            string packageRoot = GetPackageRoot();
            string[] guids = Directory.EnumerateFiles(packageRoot, "*.meta", SearchOption.AllDirectories)
                .Select(ReadMetaGuid)
                .Where(guid => !string.IsNullOrEmpty(guid))
                .ToArray();

            Assert.That(guids.Length, Is.GreaterThan(0));
            Assert.That(guids.Distinct().Count(), Is.EqualTo(guids.Length));
        }

        [Test]
        public void PackageAssets_DoNotReuseProjectAssetGuids()
        {
            var packageGuids = new HashSet<string>(
                Directory.EnumerateFiles(GetPackageRoot(), "*.meta", SearchOption.AllDirectories)
                    .Select(ReadMetaGuid)
                    .Where(guid => !string.IsNullOrEmpty(guid)));

            string[] collisions = Directory
                .EnumerateFiles(Application.dataPath, "*.meta", SearchOption.AllDirectories)
                .Select(path => new { Path = path, Guid = ReadMetaGuid(path) })
                .Where(asset => !string.IsNullOrEmpty(asset.Guid) && packageGuids.Contains(asset.Guid))
                .Select(asset => asset.Path)
                .ToArray();

            Assert.That(collisions, Is.Empty, string.Join("\n", collisions));
        }

        private static string GetPackageRoot()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DansToolboxTheme).Assembly);
            Assert.That(package, Is.Not.Null);
            return package.resolvedPath;
        }

        private static string ReadMetaGuid(string path)
        {
            foreach (string line in File.ReadLines(path))
            {
                if (line.StartsWith("guid: "))
                {
                    return line.Substring(6).Trim();
                }
            }

            return string.Empty;
        }
    }
}
