using System;
using System.Linq;
using DansToolbox.EditorTools.BetterProject;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace DansToolbox.Editor.Tests
{
    internal sealed class BetterProjectTests
    {
        [SetUp]
        public void SetUpAssetUndoJournal()
        {
            BetterProjectAssetUndo.ResetForTests();
        }

        [TearDown]
        public void TearDownAssetUndoJournal()
        {
            BetterProjectAssetUndo.ResetForTests();
        }

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
        public void Query_OnlyRequestsExpensiveMetadataWhenTermsNeedIt()
        {
            BetterProjectQuery plain = BetterProjectQuery.Parse("player t:texture path:art");
            BetterProjectQuery metadata = BetterProjectQuery.Parse("l:character is:favorite is:problem");

            Assert.That(plain.RequiresDiagnostics, Is.False);
            Assert.That(plain.RequiresFavorites, Is.False);
            Assert.That(plain.RequiresLabels, Is.False);
            Assert.That(metadata.RequiresDiagnostics, Is.True);
            Assert.That(metadata.RequiresFavorites, Is.True);
            Assert.That(metadata.RequiresLabels, Is.True);
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
        public void AssetChanges_IgnoreOnlyTheTransientRetroSfxPreview()
        {
            string preview = DansToolboxTransientAssets.RetroSfxPreviewPath;

            Assert.That(
                BetterProjectIndex.ShouldRefreshForAssetChanges(new[] { preview }),
                Is.False);
            Assert.That(
                BetterProjectIndex.ShouldRefreshForAssetChanges(
                    Array.Empty<string>(),
                    new[] { preview }),
                Is.False);
            Assert.That(
                BetterProjectIndex.ShouldRefreshForAssetChanges(
                    new[] { preview, "Assets/Audio/Finished.wav" }),
                Is.True);
            Assert.That(
                BetterProjectIndex.ShouldRefreshForAssetChanges(
                    new[] { "Packages/com.example.tool/Editor/Tool.cs" }),
                Is.True);
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
        public void IncrementalAssetChanges_AddMoveAndDeleteWhilePreservingUnchangedRecords()
        {
            const string root = "Assets/__BetterProjectIncrementalTests";
            const string source = root + "/Source.asset";
            const string destinationRoot = "Assets/__BetterProjectIncrementalMoved";
            const string destination = destinationRoot + "/Source.asset";
            AssetDatabase.DeleteAsset(root);
            AssetDatabase.DeleteAsset(destinationRoot);
            BetterProjectIndex.Refresh();
            BetterProjectAssetRecord unchanged = BetterProjectIndex.GetByPath("Assets");

            AssetDatabase.CreateFolder("Assets", "__BetterProjectIncrementalTests");
            AssetDatabase.CreateAsset(new AnimationClip(), source);
            AssetDatabase.SaveAssets();
            try
            {
                BetterProjectIndex.ApplyAssetChanges(
                    new[] { root, source },
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>());
                BetterProjectAssetRecord created = BetterProjectIndex.GetByPath(source);
                Assert.That(created, Is.Not.Null);
                Assert.That(BetterProjectIndex.GetByPath("Assets"), Is.SameAs(unchanged));

                Assert.That(AssetDatabase.MoveAsset(root, destinationRoot), Is.Empty);
                BetterProjectIndex.ApplyAssetChanges(
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    new[] { destinationRoot },
                    new[] { root });
                Assert.That(BetterProjectIndex.GetByPath(source), Is.Null);
                Assert.That(BetterProjectIndex.GetByPath(destination), Is.SameAs(created));
                Assert.That(BetterProjectIndex.GetByPath("Assets"), Is.SameAs(unchanged));

                AssetDatabase.DeleteAsset(destinationRoot);
                BetterProjectIndex.ApplyAssetChanges(
                    Array.Empty<string>(),
                    new[] { destinationRoot },
                    Array.Empty<string>(),
                    Array.Empty<string>());
                Assert.That(BetterProjectIndex.GetByPath(destination), Is.Null);
                Assert.That(BetterProjectIndex.GetByPath("Assets"), Is.SameAs(unchanged));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.DeleteAsset(destinationRoot);
                AssetDatabase.Refresh();
                BetterProjectIndex.Refresh();
            }
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
            Assert.That(
                BetterProjectSettings.Rules.Any(rule =>
                    rule.Match == BetterProjectRuleMatch.Diagnostic &&
                    rule.Value == "critical"),
                Is.True);
            Assert.That(BetterProjectSettings.Rules.Any(rule => rule.Match == BetterProjectRuleMatch.Package), Is.True);
        }

        [Test]
        public void DuplicateNames_DoNotCrossProjectAndPackageScopes()
        {
            BetterProjectAssetRecord projectCube = Record(
                "project-cube",
                "Assets/Cube.prefab",
                typeof(GameObject),
                1024);
            BetterProjectAssetRecord secondProjectCube = Record(
                "second-project-cube",
                "Assets/Prefabs/Cube.prefab",
                typeof(GameObject),
                1024);
            BetterProjectAssetRecord packageCube = Record(
                "package-cube",
                "Packages/com.example.tests/Cube.prefab",
                typeof(GameObject),
                1024);
            packageCube.IsPackage = true;

            Assert.That(
                BetterProjectIndex.DuplicateKey(projectCube),
                Is.EqualTo(BetterProjectIndex.DuplicateKey(secondProjectCube)));
            Assert.That(
                BetterProjectIndex.DuplicateKey(projectCube),
                Is.Not.EqualTo(BetterProjectIndex.DuplicateKey(packageCube)));
        }

        [Test]
        public void Diagnostics_ReserveCriticalSeverityForBrokenAssets()
        {
            Assert.That(
                BetterProjectIndex.HasCriticalDiagnostics(BetterProjectDiagnosticFlags.MissingScript),
                Is.True);
            Assert.That(
                BetterProjectIndex.HasCriticalDiagnostics(BetterProjectDiagnosticFlags.Importer),
                Is.True);
            Assert.That(
                BetterProjectIndex.HasCriticalDiagnostics(BetterProjectDiagnosticFlags.DuplicateName),
                Is.False);
            Assert.That(
                BetterProjectIndex.HasCriticalDiagnostics(BetterProjectDiagnosticFlags.Unreferenced),
                Is.False);
            Assert.That(
                BetterProjectIndex.HasCriticalDiagnostics(BetterProjectDiagnosticFlags.Oversized),
                Is.False);
        }

        [Test]
        public void DiagnosticBadges_NameAndExplainTheirReason()
        {
            Assert.That(
                BetterProjectGui.DiagnosticCode(BetterProjectDiagnosticFlags.DuplicateName),
                Is.EqualTo("DUP"));
            Assert.That(
                BetterProjectGui.DiagnosticSummary(BetterProjectDiagnosticFlags.DuplicateName),
                Does.Contain("Duplicate name"));
            Assert.That(
                BetterProjectGui.DiagnosticCode(BetterProjectDiagnosticFlags.MissingScript),
                Is.EqualTo("SCRIPT"));
            Assert.That(
                BetterProjectGui.DiagnosticSummary(BetterProjectDiagnosticFlags.Unreferenced),
                Does.Contain("may be intentional"));
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
        public void AssetUndo_RenameRestoresPathAndGuidInBothDirections()
        {
            const string root = "Assets/__BetterProjectUndoRenameTests";
            const string source = root + "/Source.asset";
            const string renamed = root + "/Renamed.asset";
            AssetDatabase.DeleteAsset(root);
            AssetDatabase.CreateFolder("Assets", "__BetterProjectUndoRenameTests");
            AssetDatabase.CreateAsset(new AnimationClip(), source);
            AssetDatabase.SaveAssets();
            string guid = AssetDatabase.AssetPathToGUID(source);

            try
            {
                BetterProjectAssetRecord record = AssetRecord(source, typeof(AnimationClip));
                Assert.That(BetterProjectOperations.Rename(record, "Renamed"), Is.Empty);
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(renamed));

                PerformUndo();
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(source));

                PerformRedo();
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(renamed));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void AssetUndo_MoveAndCutPasteRestoreOriginalPaths()
        {
            const string root = "Assets/__BetterProjectUndoMoveTests";
            const string moveFolder = root + "/Moved";
            const string pasteFolder = root + "/Pasted";
            const string moveSource = root + "/Move.asset";
            const string moveDestination = moveFolder + "/Move.asset";
            const string pasteSource = root + "/Paste.asset";
            const string pasteDestination = pasteFolder + "/Paste.asset";
            AssetDatabase.DeleteAsset(root);
            AssetDatabase.CreateFolder("Assets", "__BetterProjectUndoMoveTests");
            AssetDatabase.CreateFolder(root, "Moved");
            AssetDatabase.CreateFolder(root, "Pasted");
            AssetDatabase.CreateAsset(new AnimationClip(), moveSource);
            AssetDatabase.CreateAsset(new AnimationClip(), pasteSource);
            AssetDatabase.SaveAssets();
            string moveGuid = AssetDatabase.AssetPathToGUID(moveSource);
            string pasteGuid = AssetDatabase.AssetPathToGUID(pasteSource);

            try
            {
                Assert.That(BetterProjectOperations.Move(new[] { moveSource }, moveFolder), Is.True);
                Assert.That(AssetDatabase.GUIDToAssetPath(moveGuid), Is.EqualTo(moveDestination));
                PerformUndo();
                Assert.That(AssetDatabase.GUIDToAssetPath(moveGuid), Is.EqualTo(moveSource));
                PerformRedo();
                Assert.That(AssetDatabase.GUIDToAssetPath(moveGuid), Is.EqualTo(moveDestination));

                BetterProjectOperations.Copy(
                    new[] { AssetRecord(pasteSource, typeof(AnimationClip)) },
                    true);
                Assert.That(BetterProjectOperations.Paste(pasteFolder), Is.True);
                Assert.That(AssetDatabase.GUIDToAssetPath(pasteGuid), Is.EqualTo(pasteDestination));
                PerformUndo();
                Assert.That(AssetDatabase.GUIDToAssetPath(pasteGuid), Is.EqualTo(pasteSource));
                PerformRedo();
                Assert.That(AssetDatabase.GUIDToAssetPath(pasteGuid), Is.EqualTo(pasteDestination));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void AssetUndo_DuplicateFolderAndDeletePreserveGuidAcrossRedo()
        {
            const string root = "Assets/__BetterProjectUndoCreateDeleteTests";
            const string source = root + "/Source.asset";
            const string createdFolder = root + "/Generated";
            AssetDatabase.DeleteAsset(root);
            AssetDatabase.CreateFolder("Assets", "__BetterProjectUndoCreateDeleteTests");
            AssetDatabase.CreateAsset(new AnimationClip(), source);
            AssetDatabase.SaveAssets();

            try
            {
                string folderPath = BetterProjectOperations.CreateFolder(root, "Generated");
                string folderGuid = AssetDatabase.AssetPathToGUID(folderPath);
                Assert.That(folderPath, Is.EqualTo(createdFolder));
                PerformUndo();
                Assert.That(AssetDatabase.IsValidFolder(createdFolder), Is.False);
                PerformRedo();
                Assert.That(AssetDatabase.AssetPathToGUID(createdFolder), Is.EqualTo(folderGuid));

                string duplicatePath = AssetDatabase.GenerateUniqueAssetPath(source);
                Assert.That(
                    BetterProjectOperations.Duplicate(
                        new[] { AssetRecord(source, typeof(AnimationClip)) }),
                    Is.True);
                string duplicateGuid = AssetDatabase.AssetPathToGUID(duplicatePath);
                Assert.That(duplicateGuid, Is.Not.Empty);
                Assert.That(BetterProjectAssetUndo.Cursor, Is.EqualTo(2));
                PerformUndo();
                Assert.That(BetterProjectAssetUndo.Cursor, Is.EqualTo(1));
                Assert.That(BetterProjectAssetUndo.AppliedCursor, Is.EqualTo(1));
                string duplicateAbsolute = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(
                        System.IO.Directory.GetParent(Application.dataPath).FullName,
                        duplicatePath));
                Assert.That(System.IO.File.Exists(duplicateAbsolute), Is.False);
                Assert.That(System.IO.File.Exists(duplicateAbsolute + ".meta"), Is.False);
                Assert.That(AssetDatabase.LoadMainAssetAtPath(duplicatePath), Is.Null);
                PerformRedo();
                Assert.That(AssetDatabase.AssetPathToGUID(duplicatePath), Is.EqualTo(duplicateGuid));

                string sourceGuid = AssetDatabase.AssetPathToGUID(source);
                Assert.That(
                    BetterProjectOperations.Delete(
                        new[] { AssetRecord(source, typeof(AnimationClip)) },
                        false),
                    Is.True);
                Assert.That(AssetDatabase.LoadMainAssetAtPath(source), Is.Null);
                PerformUndo();
                Assert.That(AssetDatabase.AssetPathToGUID(source), Is.EqualTo(sourceGuid));
                PerformRedo();
                Assert.That(AssetDatabase.LoadMainAssetAtPath(source), Is.Null);
                PerformUndo();
                Assert.That(AssetDatabase.AssetPathToGUID(source), Is.EqualTo(sourceGuid));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void AssetUndo_LabelsAndImporterPresetsRestoreSerializedImporterState()
        {
            const string root = "Assets/__BetterProjectUndoImporterTests";
            const string source = root + "/Source.png";
            AssetDatabase.DeleteAsset(root);
            AssetDatabase.CreateFolder("Assets", "__BetterProjectUndoImporterTests");
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
            texture.Apply();
            string sourceAbsolute = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                source));
            System.IO.File.WriteAllBytes(sourceAbsolute, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceSynchronousImport);
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(source);
            AssetDatabase.SetLabels(asset, new[] { "Before" });
            AssetDatabase.SaveAssets();
            Preset preset = null;

            try
            {
                BetterProjectAssetRecord record = AssetRecord(source, typeof(Texture2D));
                BetterProjectOperations.SetLabels(new[] { record }, new[] { "After" });
                Assert.That(AssetDatabase.GetLabels(asset), Is.EqualTo(new[] { "After" }));
                PerformUndo();
                Assert.That(AssetDatabase.GetLabels(asset), Is.EqualTo(new[] { "Before" }));
                PerformRedo();
                Assert.That(AssetDatabase.GetLabels(asset), Is.EqualTo(new[] { "After" }));

                AssetImporter importer = AssetImporter.GetAtPath(source);
                importer.userData = "Preset Value";
                importer.SaveAndReimport();
                preset = new Preset(AssetImporter.GetAtPath(source));
                importer = AssetImporter.GetAtPath(source);
                importer.userData = "Before Value";
                importer.SaveAndReimport();

                Assert.That(BetterProjectOperations.ApplyPreset(new[] { record }, preset), Is.EqualTo(1));
                Assert.That(AssetImporter.GetAtPath(source).userData, Is.EqualTo("Preset Value"));
                PerformUndo();
                Assert.That(AssetImporter.GetAtPath(source).userData, Is.EqualTo("Before Value"));
                PerformRedo();
                Assert.That(AssetImporter.GetAtPath(source).userData, Is.EqualTo("Preset Value"));
            }
            finally
            {
                if (preset != null) UnityEngine.Object.DestroyImmediate(preset);
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
        public void HoverUpdates_AreBoundedToSixtyHertz()
        {
            double nextUpdateAt = 0d;

            Assert.That(BetterProjectWindow.ShouldProcessHoverUpdate(10d, ref nextUpdateAt), Is.True);
            Assert.That(BetterProjectWindow.ShouldProcessHoverUpdate(10.001d, ref nextUpdateAt), Is.False);
            Assert.That(BetterProjectWindow.ShouldProcessHoverUpdate(nextUpdateAt, ref nextUpdateAt), Is.True);
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

        private static BetterProjectAssetRecord AssetRecord(string path, Type type)
        {
            return Record(AssetDatabase.AssetPathToGUID(path), path, type, 0L);
        }

        private static void PerformUndo()
        {
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void PerformRedo()
        {
            Undo.FlushUndoRecordObjects();
            Undo.PerformRedo();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
