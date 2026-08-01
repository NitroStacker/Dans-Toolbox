using System.IO;
using System.Linq;
using DansToolbox.EditorTools.BetterInspector;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DansToolbox.Editor.Tests
{
    public sealed class BetterInspectorTests
    {
        [Test]
        public void Search_MatchesComponentAndPropertyTokensCaseInsensitively()
        {
            Assert.That(
                BetterInspectorWindow.MatchesSearch(
                    "rig drag",
                    "Rigidbody",
                    "Angular Drag"),
                Is.True);
            Assert.That(
                BetterInspectorWindow.MatchesSearch(
                    "camera speed",
                    "Rigidbody",
                    "Angular Drag"),
                Is.False);
        }

        [Test]
        public void ComponentGroups_ContainOnlyComponentsSharedByEveryTarget()
        {
            var first = new GameObject("First");
            var second = new GameObject("Second");
            try
            {
                first.AddComponent<BoxCollider>();
                second.AddComponent<BoxCollider>();
                first.AddComponent<Rigidbody>();

                var groups = BetterInspectorWindow.BuildComponentGroups(
                    new[] { first, second });

                Assert.That(groups.Select(group => group.Type), Does.Contain(typeof(Transform)));
                Assert.That(groups.Select(group => group.Type), Does.Contain(typeof(BoxCollider)));
                Assert.That(groups.Any(group => group.Type == typeof(Rigidbody)), Is.False);
                Assert.That(groups.All(group => group.Components.Length == 2), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void ComponentGroups_PreserveDuplicateComponentOrdinals()
        {
            var first = new GameObject("First");
            var second = new GameObject("Second");
            try
            {
                first.AddComponent<BoxCollider>();
                first.AddComponent<BoxCollider>();
                second.AddComponent<BoxCollider>();
                second.AddComponent<BoxCollider>();

                var colliderGroups = BetterInspectorWindow.BuildComponentGroups(
                        new[] { first, second })
                    .Where(group => group.Type == typeof(BoxCollider))
                    .ToArray();

                Assert.That(colliderGroups.Length, Is.EqualTo(2));
                Assert.That(colliderGroups.Select(group => group.Ordinal), Is.EqualTo(new[] { 0, 1 }));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void Diagnostics_CleanObjectHasNoIssues()
        {
            var gameObject = new GameObject("Clean");
            try
            {
                Assert.That(BetterInspectorDiagnostics.Scan(new Object[] { gameObject }), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AddComponentCatalog_ExcludesTransformAndAbstractTypes()
        {
            var types = BetterInspectorAddComponentPopup.GetAddableTypes().ToArray();

            Assert.That(types.Any(type => type == typeof(Transform)), Is.False);
            Assert.That(types.Any(type => type.IsAbstract), Is.False);
            Assert.That(types, Does.Contain(typeof(BoxCollider)));
        }

        [Test]
        public void AddComponentCatalog_UsesUnityAndCustomCategories()
        {
            var gameObject = new GameObject("Catalog Target");
            try
            {
                var entries = BetterInspectorAddComponentPopup.BuildCatalog(
                    new[] { gameObject },
                    new[]
                    {
                        typeof(Rigidbody),
                        typeof(AudioSource),
                        typeof(Camera),
                        typeof(BetterInspectorCategorizedTestType)
                    });

                Assert.That(
                    entries.Single(entry => entry.Type == typeof(Rigidbody)).CategoryPath,
                    Is.EqualTo("Physics"));
                Assert.That(
                    entries.Single(entry => entry.Type == typeof(AudioSource)).CategoryPath,
                    Is.EqualTo("Audio"));
                Assert.That(
                    entries.Single(entry => entry.Type == typeof(Camera)).CategoryPath,
                    Is.EqualTo("Rendering"));
                Assert.That(
                    entries.Single(entry => entry.Type == typeof(BetterInspectorCategorizedTestType)).CategoryPath,
                    Is.EqualTo("Better Inspector Tests"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AddComponentCatalog_BrowsesNestedCategoriesAndSearchesGlobally()
        {
            var entries = new[]
            {
                new BetterInspectorComponentMenuEntry(typeof(Rigidbody), "Physics/Rigidbody"),
                new BetterInspectorComponentMenuEntry(typeof(BoxCollider), "Physics/Colliders/Box Collider"),
                new BetterInspectorComponentMenuEntry(typeof(AudioSource), "Audio/Audio Source")
            };

            Assert.That(
                BetterInspectorAddComponentPopup.GetChildCategories(entries, string.Empty),
                Is.EqualTo(new[] { "Audio", "Physics" }));
            Assert.That(
                BetterInspectorAddComponentPopup.GetChildCategories(entries, "Physics"),
                Is.EqualTo(new[] { "Physics/Colliders" }));
            Assert.That(
                BetterInspectorAddComponentPopup.GetEntriesForCategory(entries, "Physics")
                    .Select(entry => entry.Type),
                Is.EqualTo(new[] { typeof(Rigidbody) }));
            Assert.That(
                BetterInspectorAddComponentPopup.GetSearchResults(entries, "physics box")
                    .Select(entry => entry.Type),
                Is.EqualTo(new[] { typeof(BoxCollider) }));
            Assert.That(
                BetterInspectorAddComponentPopup.GetCategoryRepresentativeType(entries, "Physics"),
                Is.EqualTo(typeof(BoxCollider)));
        }

        [Test]
        public void HeaderActions_AddComponentAlignsWithLayerField()
        {
            var header = new Rect(0f, 38f, 500f, 84f);

            Rect layer = BetterInspectorWindow.GetLayerFieldRect(header);
            Rect addComponent = BetterInspectorWindow.GetAddComponentButtonRect(header);

            Assert.That(addComponent.x, Is.EqualTo(layer.x));
            Assert.That(addComponent.width, Is.EqualTo(layer.width));
        }

        [Test]
        public void NativeInspectorContextMenuAdapter_IsAvailableForCurrentUnityVersion()
        {
            Assert.That(BetterInspectorContextMenu.NativeMenuAvailable, Is.True);
        }

        [Test]
        public void ComponentContextMenu_OpensOnlyForContextClickInsideCard()
        {
            var card = new Rect(10f, 20f, 200f, 80f);

            Assert.That(
                BetterInspectorContextMenu.ShouldOpenComponentMenu(
                    EventType.ContextClick,
                    card,
                    new Vector2(50f, 50f)),
                Is.True);
            Assert.That(
                BetterInspectorContextMenu.ShouldOpenComponentMenu(
                    EventType.MouseDown,
                    card,
                    new Vector2(50f, 50f)),
                Is.False);
            Assert.That(
                BetterInspectorContextMenu.ShouldOpenComponentMenu(
                    EventType.ContextClick,
                    card,
                    new Vector2(250f, 50f)),
                Is.False);
        }

        [Test]
        public void ComponentFoldout_IgnoresRightClick()
        {
            var header = new Rect(10f, 20f, 200f, 34f);

            Assert.That(
                BetterInspectorContextMenu.ShouldToggleFoldout(
                    EventType.MouseUp,
                    0,
                    header,
                    new Vector2(50f, 30f)),
                Is.True);
            Assert.That(
                BetterInspectorContextMenu.ShouldToggleFoldout(
                    EventType.MouseUp,
                    1,
                    header,
                    new Vector2(50f, 30f)),
                Is.False);
        }

        [Test]
        public void NativeAsset_UsesTheSelectedObjectEditor()
        {
            const string path = "Assets/BetterInspectorNativeParityTest.mat";
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                AssetDatabase.CreateAsset(material, path);

                Object[] editorTargets = BetterInspectorWindow.GetNativeEditorTargets(
                    new Object[] { material });

                Assert.That(editorTargets, Has.Length.EqualTo(1));
                Assert.That(editorTargets[0], Is.SameAs(material));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void ImportedAsset_UsesItsImporterEditorTarget()
        {
            const string path = "Assets/BetterInspectorImportedParityTest.txt";
            try
            {
                File.WriteAllText(path, "Better Inspector parity test");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);

                Object[] editorTargets = BetterInspectorWindow.GetNativeEditorTargets(
                    new Object[] { asset });

                Assert.That(editorTargets, Has.Length.EqualTo(1));
                Assert.That(editorTargets[0], Is.InstanceOf<AssetImporter>());
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void NativeEditorVisibilityScope_ExpandsAndRestoresTarget()
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
            var material = new Material(shader);
            try
            {
                InternalEditorUtility.SetIsInspectorExpanded(material, false);

                using (new BetterInspectorEditorVisibilityScope(new Object[] { material }))
                {
                    Assert.That(InternalEditorUtility.GetIsInspectorExpanded(material), Is.True);
                }

                Assert.That(InternalEditorUtility.GetIsInspectorExpanded(material), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ContextActions_ExposeAttributedMethodsAsButtons()
        {
            var actions = BetterInspectorWindow.GetContextActions(typeof(BetterInspectorActionTestAsset));

            Assert.That(actions.Select(action => action.Label), Does.Contain("Run Test Action"));
        }
    }

    [AddComponentMenu("Better Inspector Tests/Custom Tool")]
    public sealed class BetterInspectorCategorizedTestType
    {
    }

    public sealed class BetterInspectorActionTestAsset : ScriptableObject
    {
        [ContextMenu("Run Test Action")]
        private void RunTestAction()
        {
        }
    }
}
