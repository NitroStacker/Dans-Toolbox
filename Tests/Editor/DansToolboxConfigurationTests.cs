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
        public void ToolCatalog_UsesUniqueIds()
        {
            var ids = new HashSet<string>();
            foreach (DansToolboxToolDescriptor tool in DansToolboxTools.All)
            {
                Assert.That(tool.Id, Is.Not.Empty);
                Assert.That(ids.Add(tool.Id), Is.True, "Duplicate tool id: " + tool.Id);
            }

            Assert.That(ids, Does.Contain(DansToolboxTools.RetroSfxId));
            Assert.That(ids, Does.Contain(DansToolboxTools.NativeWindowDockId));
            Assert.That(ids, Does.Contain(DansToolboxTools.BetterHierarchyId));
            Assert.That(ids, Does.Contain(DansToolboxTools.BetterInspectorId));
            Assert.That(ids, Does.Contain(DansToolboxTools.BetterProjectId));
        }

        [Test]
        public void RecommendedLayout_ReferencesPackagedToolAssemblies()
        {
            string path = DansToolboxLayoutInstaller.GetLayoutPath();
            Assert.That(File.Exists(path), Is.True, path);
            Assert.That(Path.GetFileName(path), Is.EqualTo("ToolBox.wlt"));

            string layout = File.ReadAllText(path);
            Assert.That(layout, Does.Contain("DansToolbox.RetroSfx.Editor"));
            Assert.That(layout, Does.Contain("DansToolbox.NativeWindowDock.Editor"));
            Assert.That(layout, Does.Contain("DansToolbox.BetterHierarchy.Editor"));
            Assert.That(layout, Does.Contain("DansToolbox.BetterInspector.Editor"));
            Assert.That(layout, Does.Contain("DansToolbox.BetterProject.Editor"));
            Assert.That(layout, Does.Not.Contain("m_Text: Inspector"));
            Assert.That(layout, Does.Not.Contain("BattleSoccer.EditorTools"));
            Assert.That(layout, Does.Not.Contain("RetroSongArrangerWindow"));
        }

        [Test]
        public void RecommendedLayout_UnityLoaderContractIsAvailable()
        {
            System.Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.WindowLayout");
            Assert.That(type, Is.Not.Null);

            MethodInfo loader = type.GetMethods(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "TryLoadWindowLayout")
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 5 &&
                           parameters[0].ParameterType == typeof(string) &&
                           parameters[1].ParameterType == typeof(bool) &&
                           parameters[2].ParameterType == typeof(bool) &&
                           parameters[3].ParameterType == typeof(bool) &&
                           parameters[4].ParameterType == typeof(bool);
                });
            Assert.That(loader, Is.Not.Null);
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
