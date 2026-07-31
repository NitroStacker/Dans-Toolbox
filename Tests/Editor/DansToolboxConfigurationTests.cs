using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

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
        }

        [Test]
        public void RecommendedLayout_ReferencesPackagedToolAssemblies()
        {
            string path = DansToolboxLayoutInstaller.GetLayoutPath();
            Assert.That(File.Exists(path), Is.True, path);

            string layout = File.ReadAllText(path);
            Assert.That(layout, Does.Contain("DansToolbox.RetroSfx.Editor"));
            Assert.That(layout, Does.Contain("DansToolbox.NativeWindowDock.Editor"));
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
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType == typeof(string) &&
                           parameters[1].ParameterType == typeof(bool);
                });
            Assert.That(loader, Is.Not.Null);
        }

        [Test]
        public void ToolbarButton_CanBeCreated()
        {
            Assert.That(DansToolboxToolbarButton.Create(), Is.Not.Null);
        }
    }
}
