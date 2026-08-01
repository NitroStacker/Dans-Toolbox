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
            return record == null ? null : AssetDatabase.LoadMainAssetAtPath(record.Path);
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
            string error = AssetDatabase.RenameAsset(record.Path, newName);
            if (string.IsNullOrEmpty(error))
            {
                AssetDatabase.SaveAssets();
            }
            return error;
        }

        internal static bool Delete(IReadOnlyList<BetterProjectAssetRecord> selected)
        {
            BetterProjectAssetRecord[] editable = (selected ?? Array.Empty<BetterProjectAssetRecord>())
                .Where(record => record != null && !record.IsReadOnly && record.Path != "Assets")
                .Distinct()
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
            if (!EditorUtility.DisplayDialog("Move to Trash?", detail, "Move to Trash", "Cancel"))
            {
                return false;
            }

            bool changed = false;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (BetterProjectAssetRecord record in editable.OrderByDescending(item => item.Path.Length))
                {
                    changed |= AssetDatabase.MoveAssetToTrash(record.Path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
            return changed;
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
            if (clipboard.Count == 0 || !AssetDatabase.IsValidFolder(destinationFolder) ||
                destinationFolder.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return false;
            }
            bool changed = false;
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
                    changed |= string.IsNullOrEmpty(error);
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
            return changed;
        }

        internal static bool Duplicate(IReadOnlyList<BetterProjectAssetRecord> selected)
        {
            Copy(selected, false);
            return selected != null && selected.Count > 0 && Paste(selected[0].ParentPath);
        }

        internal static bool Move(IEnumerable<string> sourcePaths, string destinationFolder)
        {
            if (!AssetDatabase.IsValidFolder(destinationFolder) ||
                destinationFolder.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return false;
            }
            bool changed = false;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string source in (sourcePaths ?? Array.Empty<string>()).Distinct())
                {
                    if (!CanMoveToFolder(source, destinationFolder))
                    {
                        continue;
                    }
                    string destination = AssetDatabase.GenerateUniqueAssetPath(
                        destinationFolder + "/" + Path.GetFileName(source));
                    changed |= string.IsNullOrEmpty(AssetDatabase.MoveAsset(source, destination));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return changed;
        }

        internal static bool CanMoveToFolder(string sourcePath, string destinationFolder)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationFolder))
            {
                return false;
            }

            string source = sourcePath.Replace('\\', '/').TrimEnd('/');
            string destination = destinationFolder.Replace('\\', '/').TrimEnd('/');
            return !string.Equals(source, "Assets", StringComparison.OrdinalIgnoreCase) &&
                   !source.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) &&
                   !destination.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) &&
                   !IsSameFolder(source, destination) &&
                   !destination.StartsWith(source + "/", StringComparison.OrdinalIgnoreCase);
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
            return AssetDatabase.GUIDToAssetPath(guid);
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
            foreach (BetterProjectAssetRecord record in selected ?? Array.Empty<BetterProjectAssetRecord>())
            {
                UnityEngine.Object asset = Load(record);
                if (asset != null && !record.IsReadOnly)
                {
                    AssetDatabase.SetLabels(asset, clean);
                    EditorUtility.SetDirty(asset);
                }
            }
            AssetDatabase.SaveAssets();
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
            int applied = 0;
            foreach (BetterProjectAssetRecord record in selected ?? Array.Empty<BetterProjectAssetRecord>())
            {
                AssetImporter importer = record == null ? null : AssetImporter.GetAtPath(record.Path);
                if (importer != null && !record.IsReadOnly && preset.CanBeAppliedTo(importer))
                {
                    preset.ApplyTo(importer);
                    importer.SaveAndReimport();
                    applied++;
                }
            }
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
