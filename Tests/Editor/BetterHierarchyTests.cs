using System.Collections.Generic;
using DansToolbox.EditorTools.BetterHierarchy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor.Tests
{
    internal sealed class BetterHierarchyTests
    {
        [Test]
        public void Query_ParsesQuotedAndNegatedTokens()
        {
            BetterHierarchyQuery query = BetterHierarchyQuery.Parse(
                "t:Camera -tag:EditorOnly path:\"World/Main Camera\"");

            Assert.That(query.Tokens.Count, Is.EqualTo(3));
            Assert.That(query.Tokens[0].Key, Is.EqualTo("t"));
            Assert.That(query.Tokens[1].Negated, Is.True);
            Assert.That(query.Tokens[2].Value, Is.EqualTo("World/Main Camera"));
        }

        [TestCase("Main Camera", "mcam", true)]
        [TestCase("Directional Light", "dlight", true)]
        [TestCase("Player", "camera", false)]
        public void Query_FuzzyMatchingIsPredictable(string source, string query, bool expected)
        {
            Assert.That(BetterHierarchyQuery.FuzzyContains(source, query), Is.EqualTo(expected));
        }

        [Test]
        public void Query_MatchesComponentsAndHierarchyState()
        {
            GameObject root = new GameObject("Gameplay Root");
            GameObject camera = new GameObject("Main Camera");
            camera.AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            try
            {
                Assert.That(BetterHierarchyQuery.Parse("t:Camera is:leaf").Matches(
                    camera,
                    BetterHierarchyDiagnosticFlags.None), Is.True);
                Assert.That(BetterHierarchyQuery.Parse("is:root").Matches(
                    camera,
                    BetterHierarchyDiagnosticFlags.None), Is.False);
                Assert.That(BetterHierarchyQuery.Parse("path:\"Gameplay Root/Main Camera\"").Matches(
                    camera,
                    BetterHierarchyDiagnosticFlags.None), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Rules_CanCascadeFromAParent()
        {
            GameObject parent = new GameObject("Enemies");
            GameObject child = new GameObject("Grunt");
            child.transform.SetParent(parent.transform);
            BetterHierarchyRule rule = new BetterHierarchyRule
            {
                Match = BetterHierarchyRuleMatch.NameEquals,
                Value = "Enemies",
                Recursive = true
            };
            try
            {
                Assert.That(BetterHierarchyRuleMatcher.Matches(
                    rule,
                    child,
                    BetterHierarchyDiagnosticFlags.None), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Diagnostics_FlagsZeroScale()
        {
            GameObject gameObject = new GameObject("Zero Scale");
            gameObject.transform.localScale = new Vector3(1f, 0f, 1f);
            try
            {
                BetterHierarchyDiagnosticFlags flags = BetterHierarchyDiagnostics.Get(gameObject, true);
                Assert.That(flags.HasFlag(BetterHierarchyDiagnosticFlags.ZeroScale), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                BetterHierarchyDiagnostics.Invalidate();
            }
        }

        [Test]
        public void VirtualCollection_DoesNotChangeTransformParenting()
        {
            GameObject root = new GameObject("Root");
            GameObject member = new GameObject("Member");
            member.transform.SetParent(root.transform);
            BetterHierarchyCollection collection = null;
            try
            {
                collection = BetterHierarchyCollections.CreateVirtual(
                    "Test Virtual",
                    Color.cyan,
                    new[] { member });

                Assert.That(member.transform.parent, Is.EqualTo(root.transform));
                Assert.That(BetterHierarchyCollections.Resolve(collection), Does.Contain(member));
            }
            finally
            {
                if (collection != null)
                {
                    BetterHierarchyProjectSettings.RemoveCollection(collection);
                }
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VirtualCollection_RemovesMembersWithoutChangingSceneObjects()
        {
            GameObject root = new GameObject("Root");
            GameObject first = new GameObject("First");
            GameObject second = new GameObject("Second");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);
            BetterHierarchyCollection collection = null;
            try
            {
                collection = BetterHierarchyCollections.CreateVirtual(
                    "Test Removals",
                    Color.cyan,
                    new[] { first, second });

                Assert.That(BetterHierarchyCollections.RemoveMember(collection, first), Is.True);
                Assert.That(first, Is.Not.Null);
                Assert.That(first.transform.parent, Is.EqualTo(root.transform));
                Assert.That(BetterHierarchyCollections.Contains(collection, first), Is.False);
                Assert.That(BetterHierarchyCollections.Contains(collection, second), Is.True);

                Assert.That(
                    BetterHierarchyCollections.RemoveMembers(collection, new[] { first, second }),
                    Is.EqualTo(1));
                Assert.That(second, Is.Not.Null);
                Assert.That(second.transform.parent, Is.EqualTo(root.transform));
                Assert.That(BetterHierarchyCollections.Resolve(collection), Is.Empty);
            }
            finally
            {
                if (collection != null)
                {
                    BetterHierarchyProjectSettings.RemoveCollection(collection);
                }
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CollectionItemChecks_DistinguishEmptyFromPopulatedCollections()
        {
            GameObject member = new GameObject("Member");
            GameObject parent = new GameObject("Parent");
            BetterHierarchyCollection collection = BetterHierarchyCollections.CreateVirtual(
                "Item Check",
                Color.cyan,
                new GameObject[0]);
            try
            {
                Assert.That(BetterHierarchyCollections.HasVirtualCollectionItems(collection), Is.False);
                Assert.That(BetterHierarchyCollections.HasTransformCollectionItems(parent), Is.False);

                BetterHierarchyCollections.AddMembers(collection, new[] { member });
                member.transform.SetParent(parent.transform);

                Assert.That(BetterHierarchyCollections.HasVirtualCollectionItems(collection), Is.True);
                Assert.That(BetterHierarchyCollections.HasTransformCollectionItems(parent), Is.True);
            }
            finally
            {
                BetterHierarchyProjectSettings.RemoveCollection(collection);
                Object.DestroyImmediate(parent);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VirtualCollection_DeleteChoiceControlsItsSceneItems(bool deleteItems)
        {
            GameObject root = new GameObject("Root");
            GameObject first = new GameObject("First");
            GameObject second = new GameObject("Second");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);
            BetterHierarchyCollection collection = BetterHierarchyCollections.CreateVirtual(
                "Delete Choice",
                Color.cyan,
                new[] { first, second });
            string collectionId = collection.Id;
            try
            {
                Assert.That(
                    BetterHierarchyCollections.DeleteVirtualCollection(
                        collection,
                        deleteItems,
                        registerUndo: false),
                    Is.True);
                Assert.That(BetterHierarchyProjectSettings.FindCollection(collectionId), Is.Null);
                Assert.That(first == null, Is.EqualTo(deleteItems));
                Assert.That(second == null, Is.EqualTo(deleteItems));
                if (!deleteItems)
                {
                    Assert.That(first.transform.parent, Is.EqualTo(root.transform));
                    Assert.That(second.transform.parent, Is.EqualTo(root.transform));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ParentCollection_CreatesARealSharedTransformParent()
        {
            GameObject root = new GameObject("Root");
            GameObject first = new GameObject("First");
            GameObject second = new GameObject("Second");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);
            first.transform.position = new Vector3(1f, 2f, 3f);
            second.transform.position = new Vector3(-2f, 1f, 4f);
            Vector3 firstWorldPosition = first.transform.position;
            Vector3 secondWorldPosition = second.transform.position;
            GameObject parent = null;
            string parentId = string.Empty;
            try
            {
                parent = BetterHierarchyCollections.CreateTransformParent(
                    "Test Parent",
                    new[] { first, second },
                    Color.yellow);
                parentId = BetterHierarchyObjectIds.Get(parent);

                Assert.That(parent, Is.Not.Null);
                Assert.That(parent.transform.parent, Is.EqualTo(root.transform));
                Assert.That(first.transform.parent, Is.EqualTo(parent.transform));
                Assert.That(second.transform.parent, Is.EqualTo(parent.transform));
                Assert.That(first.transform.position, Is.EqualTo(firstWorldPosition));
                Assert.That(second.transform.position, Is.EqualTo(secondWorldPosition));
            }
            finally
            {
                if (!string.IsNullOrEmpty(parentId))
                {
                    BetterHierarchyProjectSettings.MutableRules.RemoveAll(rule => rule.Value == parentId);
                    BetterHierarchyProjectSettings.SaveNow();
                }
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ParentCollection_RemovesMemberAndPreservesWorldTransform()
        {
            GameObject root = new GameObject("Root");
            GameObject first = new GameObject("First");
            GameObject second = new GameObject("Second");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);
            first.transform.position = new Vector3(3f, -2f, 7f);
            Vector3 worldPosition = first.transform.position;
            GameObject parent = null;
            string parentId = string.Empty;
            try
            {
                parent = BetterHierarchyCollections.CreateTransformParent(
                    "Test Parent Removal",
                    new[] { first, second },
                    Color.yellow);
                parentId = BetterHierarchyObjectIds.Get(parent);

                Assert.That(BetterHierarchyCollections.RemoveFromTransformCollection(first), Is.True);
                Assert.That(first.transform.parent, Is.EqualTo(root.transform));
                Assert.That(first.transform.position, Is.EqualTo(worldPosition));
                Assert.That(second.transform.parent, Is.EqualTo(parent.transform));
            }
            finally
            {
                if (!string.IsNullOrEmpty(parentId))
                {
                    BetterHierarchyProjectSettings.MutableRules.RemoveAll(rule => rule.Value == parentId);
                    BetterHierarchyProjectSettings.SaveNow();
                }
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ParentCollection_DeleteChoiceControlsItsChildren(bool deleteItems)
        {
            GameObject root = new GameObject("Root");
            GameObject first = new GameObject("First");
            GameObject second = new GameObject("Second");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);
            first.transform.position = new Vector3(8f, 2f, -4f);
            second.transform.position = new Vector3(-3f, 5f, 6f);
            Vector3 firstWorldPosition = first.transform.position;
            Vector3 secondWorldPosition = second.transform.position;
            Object[] previousSelection = Selection.objects;
            GameObject parent = BetterHierarchyCollections.CreateTransformParent(
                "Parent Delete Choice",
                new[] { first, second },
                Color.yellow);
            try
            {
                Assert.That(
                    BetterHierarchyCollections.DeleteTransformCollection(
                        parent,
                        deleteItems,
                        registerUndo: false),
                    Is.True);
                Assert.That(parent == null, Is.True);
                Assert.That(first == null, Is.EqualTo(deleteItems));
                Assert.That(second == null, Is.EqualTo(deleteItems));
                if (!deleteItems)
                {
                    Assert.That(first.transform.parent, Is.EqualTo(root.transform));
                    Assert.That(second.transform.parent, Is.EqualTo(root.transform));
                    Assert.That(first.transform.position, Is.EqualTo(firstWorldPosition));
                    Assert.That(second.transform.position, Is.EqualTo(secondWorldPosition));
                }
            }
            finally
            {
                Selection.objects = previousSelection;
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(18f)]
        [TestCase(24f)]
        [TestCase(30f)]
        public void RowLayout_CentersTextAtEveryDensity(float rowHeight)
        {
            Rect row = new Rect(4f, 10f, 320f, rowHeight);
            Rect content = BetterHierarchyRowLayout.CenterContent(row, 18f);

            Assert.That(content.center.y, Is.EqualTo(row.center.y).Within(0.001f));
            Assert.That(content.height, Is.EqualTo(Mathf.Min(rowHeight, 18f)).Within(0.001f));
        }

        [Test]
        public void Visuals_UseRetroSfxHierarchyCanvas()
        {
            Color canvas = BetterHierarchyWindow.CanvasColor;

            Assert.That(canvas.r, Is.EqualTo(0x1B / 255f).Within(0.0001f));
            Assert.That(canvas.g, Is.EqualTo(0x1C / 255f).Within(0.0001f));
            Assert.That(canvas.b, Is.EqualTo(0x1D / 255f).Within(0.0001f));
            Assert.That(canvas.a, Is.EqualTo(1f));
        }

        [Test]
        public void ContextMenu_UsesUnityRegisteredGameObjectMenu()
        {
            Assert.That(BetterHierarchyWindow.NativeGameObjectMenuPath, Is.EqualTo("GameObject/"));
            Assert.That(BetterHierarchyContextMenus.RegisteredGameObjectItemCount, Is.GreaterThan(0));
            Assert.That(
                BetterHierarchyContextMenus.StripGameObjectRoot("GameObject/3D Object/Cube"),
                Is.EqualTo("3D Object/Cube"));
        }

        [Test]
        public void ContextMenu_ComposesCompleteHierarchyMenuWithoutDisplayingItEarly()
        {
            GameObject gameObject = new GameObject("Context Menu Test");
            Object[] previousSelection = Selection.objects;
            try
            {
                Selection.activeGameObject = gameObject;
                GenericMenu menu = new GenericMenu();

                int nativeCount = BetterHierarchyContextMenus.AddUnityHierarchyObjectItems(
                    menu,
                    gameObject,
                    () => { },
                    () => { },
                    () => { });

                Assert.That(
                    nativeCount,
                    Is.GreaterThan(BetterHierarchyContextMenus.RegisteredGameObjectItemCount));
            }
            finally
            {
                Selection.objects = previousSelection;
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SelectionSync_PrefersSceneRowOverVirtualCollectionCopy()
        {
            BetterHierarchyTreeItem virtualMember = new BetterHierarchyTreeItem(10, 2, "Member")
            {
                IsVirtualMember = true
            };
            BetterHierarchyTreeItem sceneMember = new BetterHierarchyTreeItem(20, 2, "Member");
            var lookup = new Dictionary<int, BetterHierarchyTreeItem>
            {
                [virtualMember.id] = virtualMember,
                [sceneMember.id] = sceneMember
            };

            Assert.That(
                BetterHierarchyTreeView.ChoosePreferredSelectionId(
                    new[] { virtualMember.id, sceneMember.id },
                    lookup),
                Is.EqualTo(sceneMember.id));
            Assert.That(
                BetterHierarchyTreeView.ChoosePreferredSelectionId(
                    new[] { virtualMember.id },
                    lookup),
                Is.EqualTo(virtualMember.id));
        }

        [Test]
        public void TreeExpansion_UsesStableSyntheticIdsAndDropsOnlyMissingRows()
        {
            int collectionId = BetterHierarchyTreeView.StableIdForKey("collection:alpha");
            int sameCollectionId = BetterHierarchyTreeView.StableIdForKey("collection:alpha");
            int sceneId = BetterHierarchyTreeView.StableIdForKey("scene:Assets/Main.unity");
            var lookup = new Dictionary<int, BetterHierarchyTreeItem>
            {
                [collectionId] = new BetterHierarchyTreeItem(collectionId, 0, "Collection"),
                [sceneId] = new BetterHierarchyTreeItem(sceneId, 0, "Scene")
            };

            Assert.That(collectionId, Is.EqualTo(sameCollectionId));
            Assert.That(collectionId, Is.Not.EqualTo(sceneId));
            Assert.That(
                BetterHierarchyTreeView.KeepExistingExpansion(
                    new[] { sceneId, 123456789, sceneId },
                    lookup),
                Is.EqualTo(new[] { sceneId }));
        }

        [TestCase(KeyCode.Delete, false, false, false, BetterHierarchyShortcutAction.Delete)]
        [TestCase(KeyCode.F2, false, false, false, BetterHierarchyShortcutAction.Rename)]
        [TestCase(KeyCode.D, true, false, false, BetterHierarchyShortcutAction.Duplicate)]
        [TestCase(KeyCode.C, true, false, false, BetterHierarchyShortcutAction.Copy)]
        [TestCase(KeyCode.X, true, false, false, BetterHierarchyShortcutAction.Cut)]
        [TestCase(KeyCode.V, true, false, false, BetterHierarchyShortcutAction.Paste)]
        [TestCase(KeyCode.A, true, false, false, BetterHierarchyShortcutAction.SelectAll)]
        [TestCase(KeyCode.F, true, false, false, BetterHierarchyShortcutAction.FocusSearch)]
        [TestCase(KeyCode.N, true, true, false, BetterHierarchyShortcutAction.CreateEmpty)]
        [TestCase(KeyCode.N, false, true, true, BetterHierarchyShortcutAction.CreateEmptyChild)]
        [TestCase(KeyCode.G, true, true, false, BetterHierarchyShortcutAction.CreateEmptyParent)]
        [TestCase(KeyCode.X, false, false, false, BetterHierarchyShortcutAction.None)]
        [TestCase(KeyCode.E, false, false, false, BetterHierarchyShortcutAction.None)]
        public void Shortcuts_MatchNativeHierarchyEditing(
            KeyCode keyCode,
            bool actionKey,
            bool shift,
            bool alt,
            BetterHierarchyShortcutAction expected)
        {
            Assert.That(
                BetterHierarchyShortcuts.Resolve(keyCode, actionKey, shift, alt),
                Is.EqualTo(expected));
        }

        [Test]
        public void DeleteShortcut_DeletesTheSelectedSceneObject()
        {
            GameObject gameObject = new GameObject("Delete Shortcut Test");
            Object[] previousSelection = Selection.objects;
            try
            {
                Selection.activeGameObject = gameObject;

                Assert.That(BetterHierarchyWindow.DeleteSelectedGameObjects(registerUndo: false), Is.True);
                Assert.That(gameObject == null, Is.True);
                Assert.That(Selection.objects, Is.Empty);
            }
            finally
            {
                Selection.objects = previousSelection;
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }
    }
}
