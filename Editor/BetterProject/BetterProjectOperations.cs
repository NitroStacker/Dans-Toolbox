using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterProject
{
    internal static class BetterProjectOperations
    {
        private static readonly List<string> clipboard = new List<string>();
        private static bool clipboardCut;

        internal static IReadOnlyList<string> Clipboard => clipboard;
        internal static bool ClipboardIsCut => clipboardCut;

        internal static UnityEngine.Object Load(BetterProjectAssetRecord record)
        {
            return BetterProjectIndex.LoadMainAsset(record);
        }

        internal static void Select(IEnumerable<BetterProjectAssetRecord> records)
        {
            Selection.objects = (records ?? Enumerable.Empty<BetterProjectAssetRecord>())
                .Select(Load)
                .Where(asset => asset != null)
                .ToArray();
        }

        internal static void Open(BetterProjectAssetRecord record)
        {
            UnityEngine.Object asset = Load(record);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset);
            }
        }

        internal static void Ping(BetterProjectAssetRecord record)
        {
            UnityEngine.Object asset = Load(record);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
            }
        }

        internal static string Rename(BetterProjectAssetRecord record, string newName)
        {
            if (record == null || record.IsReadOnly)
            {
                return "Read only";
            }
            newName = (newName ?? string.Empty).Trim();
            if (newName.Length == 0 || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return "Invalid name";
            }
            string sourcePath = record.Path;
            string guid = record.Guid;
            string error = AssetDatabase.RenameAsset(sourcePath, newName);
            if (string.IsNullOrEmpty(error))
            {
                AssetDatabase.SaveAssets();
                string destinationPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(destinationPath) &&
                    !string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    BetterProjectAssetUndoTransaction transaction = BetterProjectAssetUndo.Begin("Rename Asset");
                    BetterProjectAssetUndo.RecordMove(transaction, sourcePath, destinationPath);
                    BetterProjectAssetUndo.Commit(transaction);
                }
            }
            return error;
        }

        internal static bool Delete(
            IReadOnlyList<BetterProjectAssetRecord> selected,
            bool confirm = true)
        {
            BetterProjectAssetRecord[] editable = (selected ?? Array.Empty<BetterProjectAssetRecord>())
                .Where(record => record != null && !record.IsReadOnly && record.Path != "Assets")
                .Distinct()
                .Where(record => !(selected ?? Array.Empty<BetterProjectAssetRecord>()).Any(parent =>
                    parent != null &&
                    !ReferenceEquals(parent, record) &&
                    record.Path.StartsWith(parent.Path.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (editable.Length == 0)
            {
                return false;
            }

            int dependents = editable.Sum(record => BetterProjectIndex.GetReferences(record).Count);
            string detail = editable.Length == 1 ? editable[0].Name : editable.Length + " assets";
            if (dependents > 0)
            {
                detail += "\n\n" + dependents + " indexed references may be affected.";
            }
            if (confirm && !EditorUtility.DisplayDialog(
                    "Move to Undoable Trash?",
                    detail + "\n\nYou can restore this with Edit > Undo.",
                    "Move to Trash",
                    "Cancel"))
            {
                return false;
            }

            AssetDatabase.SaveAssets();
            BetterProjectAssetUndoTransaction transaction = BetterProjectAssetUndo.Begin(
                editable.Length == 1 ? "Delete Asset" : "Delete Assets");
            foreach (BetterProjectAssetRecord record in editable.OrderByDescending(item => item.Path.Length))
            {
                BetterProjectAssetUndo.TryDelete(transaction, record.Path);
            }
            if (!transaction.HasActions) return false;

            BetterProjectAssetUndo.RefreshAfterFilesystemChange();
            BetterProjectAssetUndo.Commit(transaction);
            return true;
        }

        internal static void Copy(IReadOnlyList<BetterProjectAssetRecord> selected, bool cut)
        {
            clipboard.Clear();
            clipboard.AddRange((selected ?? Array.Empty<BetterProjectAssetRecord>())
                .Where(record => record != null && (!cut || !record.IsReadOnly))
                .Select(record => record.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase));
            clipboardCut = cut;
        }

        internal static bool Paste(string destinationFolder)
        {
            return Paste(destinationFolder, clipboardCut ? "Move Assets" : "Paste Assets");
        }

        private static bool Paste(string destinationFolder, string undoName)
        {
            if (clipboard.Count == 0 || !AssetDatabase.IsValidFolder(destinationFolder) ||
                destinationFolder.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return false;
            }
            bool changed = false;
            BetterProjectAssetUndoTransaction transaction = BetterProjectAssetUndo.Begin(undoName);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string source in clipboard.ToArray())
                {
                    string destination = AssetDatabase.GenerateUniqueAssetPath(
                        destinationFolder + "/" + Path.GetFileName(source));
                    string error = clipboardCut
                        ? AssetDatabase.MoveAsset(source, destination)
                        : AssetDatabase.CopyAsset(source, destination) ? string.Empty : "Copy failed";
                    if (!string.IsNullOrEmpty(error)) continue;
                    changed = true;
                    if (clipboardCut) BetterProjectAssetUndo.RecordMove(transaction, source, destination);
                    else BetterProjectAssetUndo.RecordCreated(transaction, destination);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            if (clipboardCut && changed)
            {
                clipboard.Clear();
                clipboardCut = false;
            }
            BetterProjectAssetUndo.Commit(transaction);
            return changed;
        }

        internal static bool Duplicate(IReadOnlyList<BetterProjectAssetRecord> selected)
        {
            Copy(selected, false);
            return selected != null && selected.Count > 0 &&
                   Paste(selected[0].ParentPath, "Duplicate Assets");
        }

        internal static bool Move(IEnumerable<string> sourcePaths, string destinationFolder)
        {
            if (!IsWritableAssetFolder(destinationFolder))
            {
                return false;
            }
            bool changed = false;
            BetterProjectAssetUndoTransaction transaction = BetterProjectAssetUndo.Begin("Move Assets");
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string source in (sourcePaths ?? Array.Empty<string>()).Distinct())
                {
                    if (!TryGetProjectAssetPath(source, out string assetPath) ||
                        !CanMoveToFolder(assetPath, destinationFolder))
                    {
                        continue;
                    }
                    string destination = AssetDatabase.GenerateUniqueAssetPath(
                        destinationFolder + "/" + Path.GetFileName(assetPath));
                    if (!string.IsNullOrEmpty(AssetDatabase.MoveAsset(assetPath, destination))) continue;
                    changed = true;
                    BetterProjectAssetUndo.RecordMove(transaction, assetPath, destination);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            BetterProjectAssetUndo.Commit(transaction);
            return changed;
        }

        internal static bool CanMoveToFolder(string sourcePath, string destinationFolder)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationFolder))
            {
                return false;
            }

            if (!TryGetProjectAssetPath(sourcePath, out string source) ||
                !IsAssetFolderPath(destinationFolder))
            {
                return false;
            }
            string destination = destinationFolder.Replace('\\', '/').TrimEnd('/');
            return !string.Equals(source, "Assets", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) &&
                   !IsSameFolder(source, destination) &&
                   !destination.StartsWith(source + "/", StringComparison.OrdinalIgnoreCase);
        }

        internal static DragAndDropVisualMode GetDropVisualMode(
            IEnumerable<string> sourcePaths,
            IEnumerable<UnityEngine.Object> objectReferences,
            string destinationFolder)
        {
            if (!IsWritableAssetFolder(destinationFolder))
            {
                return DragAndDropVisualMode.Rejected;
            }

            string[] paths = (sourcePaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            UnityEngine.Object[] objects = (objectReferences ?? Array.Empty<UnityEngine.Object>())
                .Where(item => item != null)
                .Distinct()
                .ToArray();

            if (paths.Any(CanImportExternalPath) || objects.Any(CanCreatePrefabFrom))
            {
                return DragAndDropVisualMode.Copy;
            }
            return paths.Any(path => CanMoveToFolder(path, destinationFolder))
                ? DragAndDropVisualMode.Move
                : DragAndDropVisualMode.Rejected;
        }

        internal static bool PerformDrop(
            IEnumerable<string> sourcePaths,
            IEnumerable<UnityEngine.Object> objectReferences,
            string destinationFolder)
        {
            if (!IsWritableAssetFolder(destinationFolder)) return false;

            string[] paths = (sourcePaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            UnityEngine.Object[] objects = (objectReferences ?? Array.Empty<UnityEngine.Object>())
                .Where(item => item != null)
                .Distinct()
                .ToArray();

            bool changed = false;
            if (paths.Any(path => CanMoveToFolder(path, destinationFolder)))
            {
                changed |= Move(paths, destinationFolder);
            }
            if (paths.Any(CanImportExternalPath))
            {
                changed |= ImportExternal(paths, destinationFolder);
            }
            if (objects.Any(CanCreatePrefabFrom))
            {
                changed |= CreatePrefabs(objects, destinationFolder);
            }
            return changed;
        }

        internal static bool CanImportExternalPath(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) ||
                string.Equals(Path.GetExtension(sourcePath), ".meta", StringComparison.OrdinalIgnoreCase) ||
                TryGetProjectAssetPath(sourcePath, out _))
            {
                return false;
            }

            try
            {
                return Path.IsPathRooted(sourcePath) &&
                       (File.Exists(sourcePath) || Directory.Exists(sourcePath));
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool ImportExternal(IEnumerable<string> sourcePaths, string destinationFolder)
        {
            if (!IsWritableAssetFolder(destinationFolder)) return false;

            bool changed = false;
            BetterProjectAssetUndoTransaction transaction = BetterProjectAssetUndo.Begin("Import Assets");
            var importedAssets = new List<UnityEngine.Object>();
            foreach (string source in (sourcePaths ?? Array.Empty<string>())
                         .Where(CanImportExternalPath)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string trimmedSource = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fileName = Path.GetFileName(trimmedSource);
                if (string.IsNullOrWhiteSpace(fileName)) continue;

                string destination = AssetDatabase.GenerateUniqueAssetPath(destinationFolder + "/" + fileName);
                string destinationAbsolute = AssetPathToAbsolute(destination);
                try
                {
                    FileUtil.CopyFileOrDirectory(source, destinationAbsolute);
                    RemoveCopiedMetaFiles(destinationAbsolute);
                    AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                    UnityEngine.Object imported = AssetDatabase.LoadMainAssetAtPath(destination);
                    if (imported != null) importedAssets.Add(imported);
                    BetterProjectAssetUndo.RecordCreated(transaction, destination);
                    changed = true;
                }
                catch (Exception exception)
                {
                    if (File.Exists(destinationAbsolute) || Directory.Exists(destinationAbsolute))
                    {
                        FileUtil.DeleteFileOrDirectory(destinationAbsolute);
                    }
                    Debug.LogError("Better Project could not import '" + source + "': " + exception.Message);
                }
            }
            if (changed)
            {
                AssetDatabase.SaveAssets();
                if (importedAssets.Count > 0) Selection.objects = importedAssets.ToArray();
                BetterProjectAssetUndo.Commit(transaction);
            }
            return changed;
        }

        internal static bool CanCreatePrefabFrom(UnityEngine.Object source)
        {
            return source is GameObject gameObject &&
                   !EditorUtility.IsPersistent(gameObject) &&
                   gameObject.scene.IsValid() &&
                   (gameObject.hideFlags & HideFlags.DontSave) == 0;
        }

        internal static bool CreatePrefabs(
            IEnumerable<UnityEngine.Object> objectReferences,
            string destinationFolder)
        {
            if (!IsWritableAssetFolder(destinationFolder)) return false;

            GameObject[] candidates = (objectReferences ?? Array.Empty<UnityEngine.Object>())
                .Where(CanCreatePrefabFrom)
                .Cast<GameObject>()
                .Distinct()
                .ToArray();
            var candidateTransforms = new HashSet<Transform>(candidates.Select(item => item.transform));
            GameObject[] roots = candidates
                .Where(item => !HasDraggedAncestor(item.transform.parent, candidateTransforms))
                .ToArray();
            if (roots.Length == 0) return false;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(roots.Length == 1 ? "Create Prefab" : "Create Prefabs");
            BetterProjectAssetUndoTransaction transaction = BetterProjectAssetUndo.Begin(
                roots.Length == 1 ? "Create Prefab" : "Create Prefabs");
            var createdAssets = new List<UnityEngine.Object>();
            foreach (GameObject source in roots)
            {
                string name = SanitizeAssetFileName(source.name);
                string destination = AssetDatabase.GenerateUniqueAssetPath(
                    destinationFolder + "/" + name + ".prefab");
                GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    source,
                    destination,
                    InteractionMode.UserAction,
                    out bool success);
                if (success && prefab != null)
                {
                    createdAssets.Add(prefab);
                    BetterProjectAssetUndo.RecordCreated(transaction, destination);
                }
            }

            if (createdAssets.Count == 0) return false;
            AssetDatabase.SaveAssets();
            Selection.objects = createdAssets.ToArray();
            BetterProjectAssetUndo.Commit(transaction, undoGroup);
            return true;
        }

        private static bool IsWritableAssetFolder(string path)
        {
            return IsAssetFolderPath(path) &&
                   AssetDatabase.IsValidFolder(path.Replace('\\', '/').TrimEnd('/'));
        }

        private static bool IsAssetFolderPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string normalized = path.Replace('\\', '/').TrimEnd('/');
            return string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetProjectAssetPath(string sourcePath, out string assetPath)
        {
            assetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(sourcePath)) return false;

            string normalized = sourcePath.Replace('\\', '/').TrimEnd('/');
            if (string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                assetPath = normalized;
                return true;
            }
            if (!Path.IsPathRooted(sourcePath)) return false;

            try
            {
                string assetsRoot = Path.GetFullPath(Application.dataPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string absolute = Path.GetFullPath(sourcePath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(absolute, assetsRoot, StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = "Assets";
                    return true;
                }
                string prefix = assetsRoot + Path.DirectorySeparatorChar;
                if (!absolute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
                assetPath = "Assets/" + absolute.Substring(prefix.Length).Replace('\\', '/');
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string AssetPathToAbsolute(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static void RemoveCopiedMetaFiles(string destinationAbsolute)
        {
            if (File.Exists(destinationAbsolute)) return;
            if (!Directory.Exists(destinationAbsolute)) return;
            foreach (string metaFile in Directory.GetFiles(destinationAbsolute, "*.meta", SearchOption.AllDirectories))
            {
                File.Delete(metaFile);
            }
        }

        private static bool HasDraggedAncestor(Transform parent, HashSet<Transform> candidates)
        {
            while (parent != null)
            {
                if (candidates.Contains(parent)) return true;
                parent = parent.parent;
            }
            return false;
        }

        private static string SanitizeAssetFileName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "GameObject" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(name) ? "GameObject" : name;
        }

        internal static bool IsSameFolder(string sourcePath, string destinationFolder)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationFolder))
            {
                return false;
            }

            string sourceFolder = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? string.Empty;
            return string.Equals(sourceFolder, destinationFolder, StringComparison.OrdinalIgnoreCase);
        }

        internal static string CreateFolder(string parent, string preferredName = "New Folder")
        {
            if (!AssetDatabase.IsValidFolder(parent) || parent.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return string.Empty;
            }
            string name = preferredName;
            int suffix = 2;
            while (AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                name = preferredName + " " + suffix++;
            }
            string guid = AssetDatabase.CreateFolder(parent, name);
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                BetterProjectAssetUndoTransaction transaction = BetterProjectAssetUndo.Begin("Create Folder");
                BetterProjectAssetUndo.RecordCreated(transaction, path);
                BetterProjectAssetUndo.Commit(transaction);
            }
            return path;
        }

        internal static void SetLabels(
            IEnumerable<BetterProjectAssetRecord> selected,
            IEnumerable<string> labelValues)
        {
            string[] clean = (labelValues ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var changes = new List<KeyValuePair<BetterProjectAssetRecord, UnityEngine.Object>>();
            foreach (BetterProjectAssetRecord record in selected ?? Array.Empty<BetterProjectAssetRecord>())
            {
                UnityEngine.Object asset = Load(record);
                if (asset != null && !record.IsReadOnly &&
                    !AssetDatabase.GetLabels(asset).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .SequenceEqual(clean.OrderBy(value => value, StringComparer.OrdinalIgnoreCase),
                            StringComparer.OrdinalIgnoreCase))
                {
                    changes.Add(new KeyValuePair<BetterProjectAssetRecord, UnityEngine.Object>(record, asset));
                }
            }
            if (changes.Count == 0) return;

            BetterProjectAssetUndoTransaction transaction = BetterProjectAssetUndo.Begin(
                changes.Count == 1 ? "Set Asset Labels" : "Set Asset Labels (Batch)");
            foreach (KeyValuePair<BetterProjectAssetRecord, UnityEngine.Object> change in changes)
            {
                string[] before = AssetDatabase.GetLabels(change.Value);
                AssetDatabase.SetLabels(change.Value, clean);
                EditorUtility.SetDirty(change.Value);
                BetterProjectAssetUndo.RecordLabels(
                    transaction,
                    change.Key.Guid,
                    change.Key.Path,
                    before,
                    clean);
            }
            AssetDatabase.SaveAssets();
            BetterProjectAssetUndo.Commit(transaction);
            BetterProjectIndex.InvalidatePresentation();
        }

        internal static int ApplyPreset(
            IEnumerable<BetterProjectAssetRecord> selected,
            Preset preset)
        {
            if (preset == null)
            {
                return 0;
            }
            KeyValuePair<BetterProjectAssetRecord, AssetImporter>[] changes =
                (selected ?? Array.Empty<BetterProjectAssetRecord>())
                .Where(record => record != null && !record.IsReadOnly)
                .Select(record => new KeyValuePair<BetterProjectAssetRecord, AssetImporter>(
                    record,
                    AssetImporter.GetAtPath(record.Path)))
                .Where(change => change.Value != null && preset.CanBeAppliedTo(change.Value))
                .ToArray();
            if (changes.Length == 0) return 0;

            BetterProjectAssetUndoTransaction transaction = BetterProjectAssetUndo.Begin(
                changes.Length == 1 ? "Apply Importer Preset" : "Apply Importer Presets");
            int applied = 0;
            foreach (KeyValuePair<BetterProjectAssetRecord, AssetImporter> change in changes)
            {
                var before = new Preset(change.Value);
                Preset after = null;
                try
                {
                    preset.ApplyTo(change.Value);
                    after = new Preset(change.Value);
                    change.Value.SaveAndReimport();
                    BetterProjectAssetUndo.RecordImporter(
                        transaction,
                        change.Key.Guid,
                        change.Key.Path,
                        before,
                        after);
                    applied++;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(before);
                    if (after != null) UnityEngine.Object.DestroyImmediate(after);
                }
            }
            BetterProjectAssetUndo.Commit(transaction);
            BetterProjectIndex.InvalidatePresentation();
            return applied;
        }

        internal static void CopyPath(IReadOnlyList<BetterProjectAssetRecord> selected, bool guid)
        {
            EditorGUIUtility.systemCopyBuffer = string.Join(
                Environment.NewLine,
                (selected ?? Array.Empty<BetterProjectAssetRecord>())
                    .Where(record => record != null)
                    .Select(record => guid ? record.Guid : record.Path));
        }

        internal static void ShowUnityCreateMenu(Rect anchor)
        {
            EditorUtility.DisplayPopupMenu(anchor, "Assets/Create/", null);
        }

        internal static void ShowUnityAssetMenu(Rect anchor)
        {
            EditorUtility.DisplayPopupMenu(anchor, "Assets/", null);
        }
    }
}
