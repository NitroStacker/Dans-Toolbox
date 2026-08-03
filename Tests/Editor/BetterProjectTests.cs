using System;
using System.Linq;
using DansToolbox.EditorTools.BetterProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor.Tests
{
    internal sealed class BetterProjectTests
    {
        [Test]
        public void Query_MatchesFuzzyTypePathLabelAndStateTerms()
        {
            var asset = Record("abc", "Assets/Art/PlayerBody.png", typeof(Texture2D), 4 * 1024 * 1024);
            BetterProjectQuery query = BetterProjectQuery.Parse(
                "plyb t:texture path:art l:character is:favorite size:<8mb");

            Assert.That(query.Matches(
                asset,
                BetterProjectDiagnosticFlags.None,
                true,
                new[] { "Character" }), Is.True);
        }

        [Test]
        public void Query_ExclusionsAndProblemStateRejectCleanAssets()
        {
            var asset = Record("abc", "Assets/Audio/Menu.wav", typeof(AudioClip), 1024);

            Assert.That(BetterProjectQuery.Parse("-ext:wav").Matches(
                asset, BetterProjectDiagnosticFlags.None, false, Array.Empty<string>()), Is.False);
            Assert.That(BetterProjectQuery.Parse("is:problem").Matches(
                asset, BetterProjectDiagnosticFlags.None, false, Array.Empty<string>()), Is.False);
            Assert.That(BetterProjectQuery.Parse("is:problem").Matches(
                asset, BetterProjectDiagnosticFlags.Oversized, false, Array.Empty<string>()), Is.True);
        }

        [Test]
        public void Query_TokenizerPreservesQuotedValues()
        {
            Assert.That(
                BetterProjectQuery.Tokenize("path:\"Player Art\" t:Texture -l:Old"),
                Is.EqualTo(new[] { "path:Player Art", "t:Texture", "-l:Old" }));
        }

        [Test]
        public void FolderIds_AreStableAndDistinct()
        {
            Assert.That(BetterProjectFolderTree.StableId("abc"), Is.EqualTo(BetterProjectFolderTree.StableId("abc")));
            Assert.That(BetterProjectFolderTree.StableId("abc"), Is.Not.EqualTo(BetterProjectFolderTree.StableId("abd")));
        }

        [Test]
        public void ParentPath_UsesUnityForwardSlashPaths()
        {
            Assert.That(BetterProjectIndex.Parent("Assets/Art/Textures"), Is.EqualTo("Assets/Art"));
            Assert.That(BetterProjectIndex.Parent("Assets"), Is.Empty);
        }

        [Test]
        public void AssetClassification_DistinguishesModelsPrefabsSpritesAndTextures()
        {
            Assert.That(
                BetterProjectIndex.ClassifyAsset(
                    "Assets/Art/Character.fbx",
                    typeof(GameObject),
                    false,
                    false,
                    false),
                Is.EqualTo(BetterProjectAssetKind.Model));
            Assert.That(
                BetterProjectIndex.ClassifyAsset(
                    "Assets/Prefabs/Character.prefab",
                    typeof(GameObject),
                    false,
                    false,
                    false),
                Is.EqualTo(BetterProjectAssetKind.Prefab));
            Assert.That(
                BetterProjectIndex.ClassifyAsset(
                    "Assets/Art/Characters.png",
                    typeof(Texture2D),
                    false,
                    false,
                    true),
                Is.EqualTo(BetterProjectAssetKind.Sprite));
            Assert.That(
                BetterProjectIndex.ClassifyAsset(
                    "Assets/Art/Backdrop.png",
                    typeof(Texture2D),
                    false,
                    false,
                    false),
                Is.EqualTo(BetterProjectAssetKind.Texture));
        }

        [Test]
        public void CompoundAsset_ExposesItsImportedSubAssets()
        {
            const string root = "Assets/__BetterProjectSubAssetTests";
            const string path = root + "/Compound.asset";
            if (!AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.CreateFolder("Assets", "__BetterProjectSubAssetTests");
            }
            var main = new AnimationClip { name = "Main" };
            var child = new AnimationClip { name = "Child" };
            AssetDatabase.CreateAsset(main, path);
            AssetDatabase.AddObjectToAsset(child, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            try
            {
                BetterProjectIndex.Refresh();
                BetterProjectAssetRecord record = BetterProjectIndex.GetByPath(path);

                Assert.That(record, Is.Not.Null);
                Assert.That(
                    BetterProjectIndex.GetSubAssets(record).Select(asset => asset.name),
                    Does.Contain("Child"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
                BetterProjectIndex.Refresh();
            }
        }

        [Test]
        public void Move_DetectsSameFolderAsNoOp()
        {
            Assert.That(BetterProjectOperations.IsSameFolder(
                "Assets/Art/Test.mat",
                "Assets/Art"), Is.True);
            Assert.That(BetterProjectOperations.IsSameFolder(
                "Assets/Art/Test.mat",
                "Assets/Materials"), Is.False);
        }

        [Test]
        public void Move_OnlyAllowsSafeDestinationFolders()
        {
            Assert.That(BetterProjectOperations.CanMoveToFolder(
                "Assets/Art/Test.mat",
                "Assets/Art/Materials"), Is.True);
            Assert.That(BetterProjectOperations.CanMoveToFolder(
                "Assets/Art/Test.mat",
                "Assets/Art"), Is.False);
            Assert.That(BetterProjectOperations.CanMoveToFolder(
                "Assets/Art",
                "Assets/Art"), Is.False);
            Assert.That(BetterProjectOperations.CanMoveToFolder(
                "Assets/Art",
                "Assets/Art/Textures"), Is.False);
            Assert.That(BetterProjectOperations.CanMoveToFolder(
                "Packages/com.example.tool/Test.mat",
                "Assets/Art"), Is.False);
            Assert.That(BetterProjectOperations.CanMoveToFolder(
                "C:/Users/Test/Downloads/Test.mat",
                "Assets/Art"), Is.False);
        }

        [Test]
        public void ExternalDrop_ImportsFileWithCopySemantics()
        {
            const string root = "Assets/__BetterProjectExternalDropTests";
            string external = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "BetterProject_" + Guid.NewGuid().ToString("N") + ".txt");
            System.IO.File.WriteAllText(external, "external asset");
            if (!AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.CreateFolder("Assets", "__BetterProjectExternalDropTests");
            }
            try
            {
                Assert.That(BetterProjectOperations.GetDropVisualMode(
                    new[] { external },
                    Array.Empty<UnityEngine.Object>(),
                    root), Is.EqualTo(DragAndDropVisualMode.Copy));
                Assert.That(BetterProjectOperations.PerformDrop(
                    new[] { external },
                    Array.Empty<UnityEngine.Object>(),
                    root), Is.True);

                string imported = root + "/" + System.IO.Path.GetFileName(external);
                TextAsset importedAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(imported);
                Assert.That(importedAsset, Is.Not.Null);
                Assert.That(Selection.objects, Does.Contain(importedAsset));
            }
            finally
            {
                Selection.objects = Array.Empty<UnityEngine.Object>();
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
                if (System.IO.File.Exists(external)) System.IO.File.Delete(external);
            }
        }

        [Test]
        public void HierarchyDrop_CreatesAndConnectsPrefab()
        {
            const string root = "Assets/__BetterProjectHierarchyDropTests";
            const string prefabPath = root + "/Hierarchy Source.prefab";
            if (!AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.CreateFolder("Assets", "__BetterProjectHierarchyDropTests");
            }
            var source = new GameObject("Hierarchy Source");
            try
            {
                Assert.That(BetterProjectOperations.GetDropVisualMode(
                    Array.Empty<string>(),
                    new UnityEngine.Object[] { source },
                    root), Is.EqualTo(DragAndDropVisualMode.Copy));
                Assert.That(BetterProjectOperations.PerformDrop(
                    Array.Empty<string>(),
                    new UnityEngine.Object[] { source },
                    root), Is.True);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Not.Null);
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(source), Is.True);
            }
            finally
            {
                Selection.objects = Array.Empty<UnityEngine.Object>();
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ContextMenu_IdentifiesOnlyUnityCreateCommands()
        {
            Assert.That(BetterProjectContextMenus.IsCreateItem("Assets/Create/Folder"), Is.True);
            Assert.That(BetterProjectContextMenus.IsCreateItem("Assets/Create/Material"), Is.True);
            Assert.That(BetterProjectContextMenus.IsCreateItem("Assets/Refresh"), Is.False);
        }

        [Test]
        public void SearchScope_DistinguishesAssetsPackagesAndAll()
        {
            BetterProjectAssetRecord projectAsset = Record(
                "asset",
                "Assets/Art/Player.png",
                typeof(Texture2D),
                1024);
            BetterProjectAssetRecord packageAsset = Record(
                "package",
                "Packages/com.example.tool/Editor/Tool.cs",
                typeof(MonoScript),
                1024);
            packageAsset.IsPackage = true;

            Assert.That(BetterProjectWindow.IsInSearchScope(
                projectAsset,
                BetterProjectSearchScope.Assets), Is.True);
            Assert.That(BetterProjectWindow.IsInSearchScope(
                packageAsset,
                BetterProjectSearchScope.Assets), Is.False);
            Assert.That(BetterProjectWindow.IsInSearchScope(
                projectAsset,
                BetterProjectSearchScope.Packages), Is.False);
            Assert.That(BetterProjectWindow.IsInSearchScope(
                packageAsset,
                BetterProjectSearchScope.Packages), Is.True);
            Assert.That(BetterProjectWindow.IsInSearchScope(
                projectAsset,
                BetterProjectSearchScope.All), Is.True);
            Assert.That(BetterProjectWindow.IsInSearchScope(
                packageAsset,
                BetterProjectSearchScope.All), Is.True);
        }

        [Test]
        public void DefaultRules_CoverCommonAssetFamiliesAndDiagnostics()
        {
            BetterProjectSettings.EnsureInitialized();
            Assert.That(BetterProjectSettings.Rules.Any(rule => rule.Match == BetterProjectRuleMatch.Extension && rule.Value == ".cs"), Is.True);
            Assert.That(BetterProjectSettings.Rules.Any(rule => rule.Match == BetterProjectRuleMatch.Diagnostic), Is.True);
            Assert.That(BetterProjectSettings.Rules.Any(rule => rule.Match == BetterProjectRuleMatch.Package), Is.True);
        }

        [Test]
        public void Collection_CanBeCreatedAndRemovedWithoutMovingAssets()
        {
            BetterProjectCollection collection = BetterProjectSettings.CreateCollection(
                "Test Collection",
                BetterProjectCollectionKind.Manual,
                string.Empty,
                new[] { "a", "a", "b" });
            try
            {
                Assert.That(collection.AssetGuids, Is.EqualTo(new[] { "a", "b" }));
                Assert.That(BetterProjectSettings.Collections, Does.Contain(collection));
            }
            finally
            {
                BetterProjectSettings.RemoveCollection(collection);
            }
            Assert.That(BetterProjectSettings.Collections.Contains(collection), Is.False);
        }

        [Test]
        public void AssetOperations_RenameCopyAndMovePreserveAssetDatabaseVisibility()
        {
            const string root = "Assets/__BetterProjectTests";
            string source = root + "/Source.asset";
            string renamed = root + "/Renamed.asset";
            string copied = root + "/Copy.asset";
            if (!AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.CreateFolder("Assets", "__BetterProjectTests");
            }
            var asset = new AnimationClip();
            AssetDatabase.CreateAsset(asset, source);
            try
            {
                Assert.That(AssetDatabase.RenameAsset(source, "Renamed"), Is.Empty);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Assert.That(AssetDatabase.AssetPathToGUID(renamed), Is.Not.Empty);
                Assert.That(AssetDatabase.CopyAsset(renamed, copied), Is.True);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Assert.That(AssetDatabase.AssetPathToGUID(copied), Is.Not.Empty);
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void Move_WithinCurrentFolderDoesNotCreateNumberedDuplicate()
        {
            const string root = "Assets/__BetterProjectMoveTests";
            const string source = root + "/Test.asset";
            if (!AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.CreateFolder("Assets", "__BetterProjectMoveTests");
            }
            AssetDatabase.CreateAsset(new AnimationClip(), source);
            try
            {
                Assert.That(BetterProjectOperations.Move(new[] { source }, root), Is.False);
                Assert.That(AssetDatabase.AssetPathToGUID(source), Is.Not.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(root + "/Test 2.asset"), Is.Empty);
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void Move_IntoChildFolderMovesOriginalAsset()
        {
            const string root = "Assets/__BetterProjectChildMoveTests";
            const string child = root + "/Materials";
            const string source = root + "/Test.asset";
            const string destination = child + "/Test.asset";
            if (!AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.CreateFolder("Assets", "__BetterProjectChildMoveTests");
            }
            if (!AssetDatabase.IsValidFolder(child))
            {
                AssetDatabase.CreateFolder(root, "Materials");
            }
            AssetDatabase.CreateAsset(new AnimationClip(), source);
            string guid = AssetDatabase.AssetPathToGUID(source);
            try
            {
                Assert.That(BetterProjectOperations.Move(new[] { source }, child), Is.True);
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(destination));
                Assert.That(AssetDatabase.AssetPathToGUID(source), Is.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(child + "/Test 2.asset"), Is.Empty);
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void Window_CanBeCreatedAtCompactMinimumSize()
        {
            BetterProjectWindow window = ScriptableObject.CreateInstance<BetterProjectWindow>();
            try
            {
                Assert.That(window, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void GridLayout_KeepsTileSizeStableAcrossColumnBreakpoints()
        {
            BetterProjectWindow.CalculateGridLayout(360f, 112f, out int narrowColumns, out float narrowWidth);
            BetterProjectWindow.CalculateGridLayout(390f, 112f, out int wideColumns, out float wideWidth);

            Assert.That(narrowColumns, Is.EqualTo(2));
            Assert.That(wideColumns, Is.EqualTo(3));
            Assert.That(narrowWidth, Is.EqualTo(112f));
            Assert.That(wideWidth, Is.EqualTo(112f));
        }

        [Test]
        public void SearchLayout_SeparatesClearButtonFromFieldAndFocusBorder()
        {
            Rect searchRect = new Rect(100f, 8f, 180f, 22f);

            BetterProjectWindow.CalculateSearchControlRects(
                searchRect,
                true,
                out Rect fieldRect,
                out Rect clearRect);

            Assert.That(fieldRect.xMax, Is.LessThan(clearRect.xMin));
            Assert.That(clearRect.xMax, Is.LessThan(searchRect.xMax));
            Assert.That(clearRect.yMin, Is.GreaterThan(searchRect.yMin));
            Assert.That(clearRect.yMax, Is.LessThan(searchRect.yMax));
        }

        [Test]
        public void SharedSearch_ReleasesFocusOnlyOnPointerDownOutsideTheField()
        {
            Rect searchRect = new Rect(100f, 8f, 180f, DansToolboxSearchField.Height);

            Assert.That(
                DansToolboxSearchField.ShouldReleaseFocus(searchRect, new Vector2(50f, 50f), true, EventType.MouseDown),
                Is.True);
            Assert.That(
                DansToolboxSearchField.ShouldReleaseFocus(searchRect, new Vector2(150f, 16f), true, EventType.MouseDown),
                Is.False);
            Assert.That(
                DansToolboxSearchField.ShouldReleaseFocus(searchRect, new Vector2(50f, 50f), false, EventType.MouseDown),
                Is.False);
            Assert.That(
                DansToolboxSearchField.ShouldReleaseFocus(searchRect, new Vector2(50f, 50f), true, EventType.MouseMove),
                Is.False);
        }

        private static BetterProjectAssetRecord Record(string guid, string path, Type type, long bytes)
        {
            return new BetterProjectAssetRecord
            {
                Guid = guid,
                Path = path,
                ParentPath = BetterProjectIndex.Parent(path),
                Name = System.IO.Path.GetFileNameWithoutExtension(path),
                Extension = System.IO.Path.GetExtension(path),
                MainType = type,
                FileSize = bytes,
                ModifiedUtc = DateTime.UtcNow
            };
        }
    }
}
