using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace DansToolbox.EditorTools.BetterProject
{
    [InitializeOnLoad]
    internal static class BetterProjectIndex
    {
        private const int ReferenceBatchLimit = 18;
        private const double ReferenceBatchBudgetMilliseconds = 3d;
        private const int IncrementalChangeLimit = 256;
        private const long OversizedTextureBytes = 16L * 1024L * 1024L;
        private const long OversizedAudioBytes = 24L * 1024L * 1024L;
        private const long OversizedGeneralBytes = 64L * 1024L * 1024L;

        private static readonly List<BetterProjectAssetRecord> records =
            new List<BetterProjectAssetRecord>();
        private static readonly Dictionary<string, BetterProjectAssetRecord> byGuid =
            new Dictionary<string, BetterProjectAssetRecord>(StringComparer.Ordinal);
        private static readonly Dictionary<string, BetterProjectAssetRecord> byPath =
            new Dictionary<string, BetterProjectAssetRecord>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<BetterProjectAssetRecord>> children =
            new Dictionary<string, List<BetterProjectAssetRecord>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, UnityEngine.Object[]> subAssets =
            new Dictionary<string, UnityEngine.Object[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string[]> labels =
            new Dictionary<string, string[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, BetterProjectDiagnosticFlags> diagnostics =
            new Dictionary<string, BetterProjectDiagnosticFlags>(StringComparer.Ordinal);
        private static readonly Dictionary<string, BetterProjectStyle> styles =
            new Dictionary<string, BetterProjectStyle>(StringComparer.Ordinal);
        private static readonly Dictionary<string, UnityEngine.Object> mainAssets =
            new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        private static readonly Dictionary<string, HashSet<string>> references =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> duplicateNames =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> duplicateContentGuids =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> pendingImportedPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> pendingDeletedPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> pendingMoves =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static int revision;
        private static bool isReady;
        private static bool refreshQueued;
        private static bool referenceIndexing;
        private static int referenceIndexCursor;
        private static List<BetterProjectAssetRecord> referenceWork;
        private static bool duplicateContentReady;

        static BetterProjectIndex()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= CancelReferenceIndex;
            AssemblyReloadEvents.beforeAssemblyReload += CancelReferenceIndex;
        }

        internal static event Action Changed;
        internal static event Action ReferenceProgressChanged;

        internal static IReadOnlyList<BetterProjectAssetRecord> Records
        {
            get
            {
                EnsureReady();
                return records;
            }
        }

        internal static int Revision => revision;
        internal static bool IsReferenceIndexing => referenceIndexing;
        internal static bool IsReferenceIndexReady => referenceWork != null && !referenceIndexing && referenceIndexCursor >= referenceWork.Count;
        internal static float ReferenceIndexProgress => referenceWork == null || referenceWork.Count == 0
            ? 0f
            : Mathf.Clamp01(referenceIndexCursor / (float)referenceWork.Count);

        internal static void EnsureReady()
        {
            if (!isReady)
            {
                Refresh();
            }
        }

        internal static void Refresh()
        {
            isReady = false;
            records.Clear();
            byGuid.Clear();
            byPath.Clear();
            children.Clear();
            subAssets.Clear();
            labels.Clear();
            diagnostics.Clear();
            styles.Clear();
            mainAssets.Clear();
            references.Clear();
            duplicateNames.Clear();
            duplicateContentGuids.Clear();
            pendingImportedPaths.Clear();
            pendingDeletedPaths.Clear();
            pendingMoves.Clear();
            duplicateContentReady = false;
            CancelReferenceIndex();
            referenceWork = null;
            referenceIndexCursor = 0;

            foreach (string rawPath in AssetDatabase.GetAllAssetPaths())
            {
                string path = Normalize(rawPath);
                if (IsTransientAsset(path))
                {
                    continue;
                }
                if ((!path.StartsWith("Assets", StringComparison.Ordinal) &&
                     !path.StartsWith("Packages/", StringComparison.Ordinal)) ||
                    path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                BetterProjectAssetRecord record = CreateRecord(path);
                if (record == null) continue;
                records.Add(record);
                byGuid[record.Guid] = record;
                byPath[path] = record;

                if (!children.TryGetValue(record.ParentPath, out List<BetterProjectAssetRecord> siblings))
                {
                    siblings = new List<BetterProjectAssetRecord>();
                    children.Add(record.ParentPath, siblings);
                }
                siblings.Add(record);

                string nameKey = DuplicateKey(record);
                duplicateNames.TryGetValue(nameKey, out int count);
                duplicateNames[nameKey] = count + 1;
            }

            foreach (List<BetterProjectAssetRecord> siblingList in children.Values)
            {
                siblingList.Sort(CompareDefault);
            }
            isReady = true;
            revision++;
            Changed?.Invoke();
        }

        private static BetterProjectAssetRecord CreateRecord(string rawPath)
        {
            string path = Normalize(rawPath);
            if (!IsIndexablePath(path) || IsTransientAsset(path)) return null;

            bool folder = AssetDatabase.IsValidFolder(path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return null;

            string physicalPath = ResolvePhysicalPath(path);
            Type mainType = folder ? typeof(DefaultAsset) : AssetDatabase.GetMainAssetTypeAtPath(path);
            AssetImporter importer = folder ? null : AssetImporter.GetAtPath(path);
            bool physicalFile = !string.IsNullOrEmpty(physicalPath) && File.Exists(physicalPath);
            bool physicalDirectory = !string.IsNullOrEmpty(physicalPath) && Directory.Exists(physicalPath);
            return new BetterProjectAssetRecord
            {
                Guid = guid,
                Path = path,
                ParentPath = Parent(path),
                Name = folder ? LastSegment(path) : Path.GetFileNameWithoutExtension(path),
                Extension = folder ? string.Empty : Path.GetExtension(path).ToLowerInvariant(),
                MainType = mainType,
                Kind = ClassifyAsset(path, mainType, folder, importer),
                IsFolder = folder,
                IsPackage = path.StartsWith("Packages/", StringComparison.Ordinal),
                IsReadOnly = path.StartsWith("Packages/", StringComparison.Ordinal),
                FileSize = !folder && physicalFile ? new FileInfo(physicalPath).Length : 0L,
                ModifiedUtc = physicalFile || physicalDirectory
                    ? File.GetLastWriteTimeUtc(physicalPath)
                    : default
            };
        }

        internal static BetterProjectAssetRecord GetByGuid(string guid)
        {
            EnsureReady();
            return !string.IsNullOrEmpty(guid) && byGuid.TryGetValue(guid, out BetterProjectAssetRecord record)
                ? record
                : null;
        }

        internal static BetterProjectAssetRecord GetByPath(string path)
        {
            EnsureReady();
            return !string.IsNullOrEmpty(path) && byPath.TryGetValue(Normalize(path), out BetterProjectAssetRecord record)
                ? record
                : null;
        }

        internal static IReadOnlyList<BetterProjectAssetRecord> GetChildren(string folderPath)
        {
            EnsureReady();
            return children.TryGetValue(Normalize(folderPath), out List<BetterProjectAssetRecord> result)
                ? result
                : Array.Empty<BetterProjectAssetRecord>();
        }

        internal static IReadOnlyList<UnityEngine.Object> GetSubAssets(BetterProjectAssetRecord record)
        {
            if (record == null || record.IsFolder)
            {
                return Array.Empty<UnityEngine.Object>();
            }
            if (subAssets.TryGetValue(record.Guid, out UnityEngine.Object[] cached))
            {
                return cached;
            }

            cached = AssetDatabase.LoadAllAssetRepresentationsAtPath(record.Path)
                .Where(asset => asset != null)
                .ToArray();
            subAssets[record.Guid] = cached;
            return cached;
        }

        internal static UnityEngine.Object LoadMainAsset(BetterProjectAssetRecord record)
        {
            if (record == null) return null;
            if (mainAssets.TryGetValue(record.Guid, out UnityEngine.Object cached) && cached != null)
            {
                return cached;
            }
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(record.Path);
            if (asset != null) mainAssets[record.Guid] = asset;
            return asset;
        }

        internal static BetterProjectAssetKind ClassifyAsset(
            string path,
            Type mainType,
            bool isFolder,
            bool hasModelImporter,
            bool isSpriteTexture)
        {
            if (isFolder) return BetterProjectAssetKind.Folder;
            string extension = Path.GetExtension(path ?? string.Empty);
            if (hasModelImporter || IsModelExtension(extension)) return BetterProjectAssetKind.Model;
            if (string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) &&
                mainType != null && typeof(GameObject).IsAssignableFrom(mainType))
            {
                return BetterProjectAssetKind.Prefab;
            }
            if (isSpriteTexture || mainType != null && typeof(Sprite).IsAssignableFrom(mainType))
            {
                return BetterProjectAssetKind.Sprite;
            }
            if (mainType != null && typeof(Texture).IsAssignableFrom(mainType))
            {
                return BetterProjectAssetKind.Texture;
            }
            return BetterProjectAssetKind.Asset;
        }

        private static BetterProjectAssetKind ClassifyAsset(
            string path,
            Type mainType,
            bool isFolder,
            AssetImporter importer)
        {
            var textureImporter = importer as TextureImporter;
            return ClassifyAsset(
                path,
                mainType,
                isFolder,
                importer is ModelImporter,
                textureImporter != null && textureImporter.textureType == TextureImporterType.Sprite);
        }

        internal static string[] GetLabels(BetterProjectAssetRecord record)
        {
            if (record == null || record.IsFolder)
            {
                return Array.Empty<string>();
            }
            if (labels.TryGetValue(record.Guid, out string[] cached))
            {
                return cached;
            }
            UnityEngine.Object asset = LoadMainAsset(record);
            cached = asset == null ? Array.Empty<string>() : AssetDatabase.GetLabels(asset);
            labels[record.Guid] = cached;
            return cached;
        }

        internal static BetterProjectDiagnosticFlags GetDiagnostics(BetterProjectAssetRecord record)
        {
            if (record == null)
            {
                return BetterProjectDiagnosticFlags.None;
            }
            if (diagnostics.TryGetValue(record.Guid, out BetterProjectDiagnosticFlags cached))
            {
                return cached;
            }

            BetterProjectDiagnosticFlags flags = BetterProjectDiagnosticFlags.None;
            if (!record.IsFolder)
            {
                UnityEngine.Object asset = LoadMainAsset(record);
                if (asset == null)
                {
                    flags |= BetterProjectDiagnosticFlags.MissingAsset;
                }
                if (AssetImporter.GetAtPath(record.Path) == null && !record.IsPackage)
                {
                    flags |= BetterProjectDiagnosticFlags.Importer;
                }
                if (record.Kind == BetterProjectAssetKind.Prefab &&
                    asset is GameObject prefab &&
                    HasMissingScripts(prefab))
                {
                    flags |= BetterProjectDiagnosticFlags.MissingScript;
                }
                if (asset is Material material && material.shader == null)
                {
                    flags |= BetterProjectDiagnosticFlags.MissingShader;
                }
                if (IsOversized(record))
                {
                    flags |= BetterProjectDiagnosticFlags.Oversized;
                }
                if (IsReferenceIndexReady && record.ReferenceCount == 0 &&
                    !record.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) &&
                    !record.Extension.Equals(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= BetterProjectDiagnosticFlags.Unreferenced;
                }
            }
            else if (GetChildren(record.Path).Count == 0)
            {
                flags |= BetterProjectDiagnosticFlags.EmptyFolder;
            }

            string duplicateKey = DuplicateKey(record);
            if (!record.IsFolder && duplicateNames.TryGetValue(duplicateKey, out int count) && count > 1)
            {
                flags |= BetterProjectDiagnosticFlags.DuplicateName;
            }
            if (record.Name.StartsWith(" ", StringComparison.Ordinal) ||
                record.Name.EndsWith(" ", StringComparison.Ordinal) ||
                record.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                flags |= BetterProjectDiagnosticFlags.Naming;
            }

            diagnostics[record.Guid] = flags;
            return flags;
        }

        internal static BetterProjectStyle GetStyle(BetterProjectAssetRecord record)
        {
            if (record == null)
            {
                return default;
            }
            if (styles.TryGetValue(record.Guid, out BetterProjectStyle cached))
            {
                return cached;
            }
            BetterProjectDiagnosticFlags flags = GetDiagnostics(record);
            BetterProjectStyleRule winner = null;
            foreach (BetterProjectStyleRule rule in BetterProjectSettings.Rules)
            {
                if (rule == null || !rule.Enabled || !Matches(rule, record, flags))
                {
                    continue;
                }
                if (winner == null || rule.Priority > winner.Priority)
                {
                    winner = rule;
                }
            }
            var style = new BetterProjectStyle(winner);
            styles[record.Guid] = style;
            return style;
        }

        internal static bool HasCriticalDiagnostics(BetterProjectDiagnosticFlags flags)
        {
            const BetterProjectDiagnosticFlags critical =
                BetterProjectDiagnosticFlags.MissingAsset |
                BetterProjectDiagnosticFlags.MissingScript |
                BetterProjectDiagnosticFlags.MissingShader |
                BetterProjectDiagnosticFlags.Importer;
            return (flags & critical) != 0;
        }

        internal static string[] GetDirectDependencies(BetterProjectAssetRecord record)
        {
            if (record == null || record.IsFolder)
            {
                return Array.Empty<string>();
            }
            string[] result = AssetDatabase.GetDependencies(record.Path, false)
                .Where(path => !string.Equals(path, record.Path, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            record.DirectDependencyCount = result.Length;
            return result;
        }

        internal static IReadOnlyList<string> GetReferences(BetterProjectAssetRecord record)
        {
            if (record == null || !references.TryGetValue(record.Path, out HashSet<string> result))
            {
                return Array.Empty<string>();
            }
            return result.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static IReadOnlyList<BetterProjectAssetRecord> GetContentDuplicates()
        {
            EnsureReady();
            if (!duplicateContentReady)
            {
                BuildDuplicateContentIndex();
            }
            return duplicateContentGuids.Select(GetByGuid).Where(record => record != null).ToArray();
        }

        internal static void StartReferenceIndex()
        {
            EnsureReady();
            references.Clear();
            foreach (BetterProjectAssetRecord record in records)
            {
                record.ReferenceCount = 0;
            }
            diagnostics.Clear();
            styles.Clear();
            referenceWork = records.Where(record => !record.IsFolder).ToList();
            referenceIndexCursor = 0;
            referenceIndexing = true;
            EditorApplication.update -= BuildReferenceBatch;
            EditorApplication.update += BuildReferenceBatch;
            revision++;
            Changed?.Invoke();
            ReferenceProgressChanged?.Invoke();
        }

        internal static void CancelReferenceIndex()
        {
            EditorApplication.update -= BuildReferenceBatch;
            referenceIndexing = false;
        }

        internal static bool IsIncludedByBuildHeuristic(BetterProjectAssetRecord record)
        {
            if (record == null || record.IsFolder)
            {
                return false;
            }
            if (record.Path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                record.Path.EndsWith("/Resources", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                {
                    continue;
                }
                if (string.Equals(scene.path, record.Path, StringComparison.OrdinalIgnoreCase) ||
                    AssetDatabase.GetDependencies(scene.path, true).Contains(record.Path))
                {
                    return true;
                }
            }
            return false;
        }

        internal static void InvalidatePresentation()
        {
            diagnostics.Clear();
            styles.Clear();
            revision++;
            Changed?.Invoke();
        }

        private static void BuildDuplicateContentIndex()
        {
            duplicateContentGuids.Clear();
            BetterProjectAssetRecord[][] candidates = records
                .Where(record => !record.IsFolder && !record.IsPackage && record.FileSize > 0)
                .GroupBy(record => record.FileSize)
                .Where(group => group.Count() > 1)
                .Select(group => group.ToArray())
                .ToArray();
            int total = candidates.Sum(group => group.Length);
            int processed = 0;
            try
            {
                using SHA256 sha = SHA256.Create();
                foreach (BetterProjectAssetRecord[] group in candidates)
                {
                    var hashes = new Dictionary<string, List<BetterProjectAssetRecord>>(StringComparer.Ordinal);
                    foreach (BetterProjectAssetRecord record in group)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar(
                                "Duplicate content", record.Path, processed++ / (float)Mathf.Max(1, total)))
                        {
                            duplicateContentReady = true;
                            return;
                        }
                        string physicalPath = ResolvePhysicalPath(record.Path);
                        if (!File.Exists(physicalPath)) continue;
                        string hash;
                        using (FileStream stream = File.OpenRead(physicalPath))
                        {
                            hash = Convert.ToBase64String(sha.ComputeHash(stream));
                        }
                        if (!hashes.TryGetValue(hash, out List<BetterProjectAssetRecord> matches))
                        {
                            matches = new List<BetterProjectAssetRecord>();
                            hashes.Add(hash, matches);
                        }
                        matches.Add(record);
                    }
                    foreach (List<BetterProjectAssetRecord> matches in hashes.Values.Where(items => items.Count > 1))
                    {
                        foreach (BetterProjectAssetRecord record in matches) duplicateContentGuids.Add(record.Guid);
                    }
                }
                duplicateContentReady = true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        internal static bool ShouldRefreshForAssetChanges(params string[][] changedPathGroups)
        {
            foreach (string[] paths in changedPathGroups ?? Array.Empty<string[]>())
            {
                foreach (string path in paths ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrEmpty(path) && !IsTransientAsset(path))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static void QueueRefresh(params string[][] changedPathGroups)
        {
            QueueAssetChanges(
                (changedPathGroups ?? Array.Empty<string[]>()).SelectMany(group => group ?? Array.Empty<string>()).ToArray(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        internal static void QueueAssetChanges(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!isReady)
            {
                return;
            }
            AddPendingPaths(pendingImportedPaths, importedAssets);
            AddPendingPaths(pendingDeletedPaths, deletedAssets);
            int moveCount = Math.Min(movedAssets?.Length ?? 0, movedFromAssetPaths?.Length ?? 0);
            for (int index = 0; index < moveCount; index++)
            {
                string from = Normalize(movedFromAssetPaths[index]);
                string to = Normalize(movedAssets[index]);
                if (!IsMeaningfulChangePath(from) || !IsMeaningfulChangePath(to)) continue;

                string predecessor = pendingMoves.FirstOrDefault(pair =>
                    string.Equals(pair.Value, from, StringComparison.OrdinalIgnoreCase)).Key;
                if (!string.IsNullOrEmpty(predecessor))
                {
                    pendingMoves[predecessor] = to;
                    pendingMoves.Remove(from);
                }
                else
                {
                    pendingMoves[from] = to;
                }
            }
            if (pendingImportedPaths.Count == 0 && pendingDeletedPaths.Count == 0 && pendingMoves.Count == 0)
            {
                return;
            }
            if (refreshQueued)
            {
                return;
            }
            refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                refreshQueued = false;
                string[] imported = pendingImportedPaths.ToArray();
                string[] deleted = pendingDeletedPaths.ToArray();
                KeyValuePair<string, string>[] moves = pendingMoves.ToArray();
                pendingImportedPaths.Clear();
                pendingDeletedPaths.Clear();
                pendingMoves.Clear();
                ApplyAssetChanges(
                    imported,
                    deleted,
                    moves.Select(pair => pair.Value).ToArray(),
                    moves.Select(pair => pair.Key).ToArray());
            };
        }

        internal static void ApplyAssetChanges(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!isReady) return;

            importedAssets = FilterChangePaths(importedAssets);
            deletedAssets = FilterChangePaths(deletedAssets);
            movedAssets = FilterChangePaths(movedAssets);
            movedFromAssetPaths = FilterChangePaths(movedFromAssetPaths);
            int moveCount = Math.Min(movedAssets.Length, movedFromAssetPaths.Length);
            if (importedAssets.Length == 0 && deletedAssets.Length == 0 && moveCount == 0) return;
            if (importedAssets.Length + deletedAssets.Length + moveCount * 2 > IncrementalChangeLimit)
            {
                Refresh();
                return;
            }

            bool referenceWasReady = IsReferenceIndexReady;
            bool referenceWasIndexing = referenceIndexing;
            if (referenceWasIndexing)
            {
                CancelReferenceIndex();
                references.Clear();
                referenceWork = null;
                referenceIndexCursor = 0;
                foreach (BetterProjectAssetRecord record in records) record.ReferenceCount = 0;
                diagnostics.Clear();
                styles.Clear();
            }

            var affectedGuids = new HashSet<string>(StringComparer.Ordinal);
            var affectedDuplicateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var affectedParents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deletedRoots = new HashSet<string>(deletedAssets, StringComparer.OrdinalIgnoreCase);
            var changedOwnerPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var moves = new List<KeyValuePair<string, string>>();

            for (int index = 0; index < moveCount; index++)
            {
                string from = movedFromAssetPaths[index];
                string to = movedAssets[index];
                moves.Add(new KeyValuePair<string, string>(from, to));
                MoveIndexedPath(
                    from,
                    to,
                    affectedGuids,
                    affectedDuplicateKeys,
                    affectedParents,
                    changedOwnerPaths);
            }

            foreach (string path in deletedAssets)
            {
                RemoveIndexedPath(
                    path,
                    affectedGuids,
                    affectedDuplicateKeys,
                    affectedParents);
            }

            var pathsToImport = new HashSet<string>(importedAssets, StringComparer.OrdinalIgnoreCase);
            foreach (string movedPath in movedAssets.Take(moveCount)) pathsToImport.Add(movedPath);
            foreach (string path in pathsToImport)
            {
                AddOrUpdateIndexedPath(
                    path,
                    affectedGuids,
                    affectedDuplicateKeys,
                    affectedParents,
                    changedOwnerPaths);
            }

            RefreshSecondaryIndexes(affectedParents, affectedDuplicateKeys);
            InvalidateRelatedPresentation(affectedGuids, affectedDuplicateKeys, affectedParents);
            duplicateContentGuids.Clear();
            duplicateContentReady = false;

            if (referenceWasReady)
            {
                UpdateReferencesIncrementally(moves, deletedRoots, changedOwnerPaths);
            }

            revision++;
            Changed?.Invoke();
            ReferenceProgressChanged?.Invoke();
        }

        private static void AddPendingPaths(HashSet<string> destination, IEnumerable<string> paths)
        {
            foreach (string rawPath in paths ?? Array.Empty<string>())
            {
                string path = Normalize(rawPath);
                if (IsMeaningfulChangePath(path)) destination.Add(path);
            }
        }

        private static string[] FilterChangePaths(IEnumerable<string> paths)
        {
            return (paths ?? Array.Empty<string>())
                .Select(Normalize)
                .Where(IsMeaningfulChangePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool IsMeaningfulChangePath(string path)
        {
            return !string.IsNullOrEmpty(path) && !IsTransientAsset(path) && IsIndexablePath(path);
        }

        private static void MoveIndexedPath(
            string from,
            string to,
            HashSet<string> affectedGuids,
            HashSet<string> affectedDuplicateKeys,
            HashSet<string> affectedParents,
            HashSet<string> changedOwnerPaths)
        {
            BetterProjectAssetRecord[] movedRecords = records
                .Where(record => IsPathOrChild(record.Path, from))
                .OrderBy(record => record.Path.Length)
                .ToArray();
            foreach (BetterProjectAssetRecord record in movedRecords)
            {
                string oldPath = record.Path;
                string suffix = oldPath.Length == from.Length ? string.Empty : oldPath.Substring(from.Length);
                string newPath = to + suffix;
                affectedGuids.Add(record.Guid);
                affectedDuplicateKeys.Add(DuplicateKey(record));
                affectedParents.Add(record.ParentPath);
                if (!record.IsFolder) changedOwnerPaths.Add(newPath);

                byPath.Remove(oldPath);
                record.Path = newPath;
                record.ParentPath = Parent(newPath);
                record.Name = record.IsFolder ? LastSegment(newPath) : Path.GetFileNameWithoutExtension(newPath);
                record.Extension = record.IsFolder ? string.Empty : Path.GetExtension(newPath).ToLowerInvariant();
                record.IsPackage = newPath.StartsWith("Packages/", StringComparison.Ordinal);
                record.IsReadOnly = record.IsPackage;
                affectedDuplicateKeys.Add(DuplicateKey(record));
                affectedParents.Add(record.ParentPath);
                byPath[newPath] = record;
                diagnostics.Remove(record.Guid);
                styles.Remove(record.Guid);
            }
        }

        private static void RemoveIndexedPath(
            string path,
            HashSet<string> affectedGuids,
            HashSet<string> affectedDuplicateKeys,
            HashSet<string> affectedParents)
        {
            BetterProjectAssetRecord[] removed = records
                .Where(record => IsPathOrChild(record.Path, path))
                .ToArray();
            if (removed.Length == 0) return;

            var removedSet = new HashSet<BetterProjectAssetRecord>(removed);
            records.RemoveAll(removedSet.Contains);
            foreach (BetterProjectAssetRecord record in removed)
            {
                affectedGuids.Add(record.Guid);
                affectedDuplicateKeys.Add(DuplicateKey(record));
                affectedParents.Add(record.ParentPath);
                byPath.Remove(record.Path);
                byGuid.Remove(record.Guid);
                InvalidateAssetCaches(record.Guid);
            }
        }

        private static void AddOrUpdateIndexedPath(
            string path,
            HashSet<string> affectedGuids,
            HashSet<string> affectedDuplicateKeys,
            HashSet<string> affectedParents,
            HashSet<string> changedOwnerPaths)
        {
            BetterProjectAssetRecord replacement = CreateRecord(path);
            if (replacement == null) return;

            if (byPath.TryGetValue(path, out BetterProjectAssetRecord existing))
            {
                replacement.DirectDependencyCount = existing.DirectDependencyCount;
                replacement.ReferenceCount = existing.ReferenceCount;
                affectedDuplicateKeys.Add(DuplicateKey(existing));
                affectedParents.Add(existing.ParentPath);
                records.Remove(existing);
                byGuid.Remove(existing.Guid);
                byPath.Remove(existing.Path);
                InvalidateAssetCaches(existing.Guid);
                affectedGuids.Add(existing.Guid);
            }
            records.Add(replacement);
            byGuid[replacement.Guid] = replacement;
            byPath[replacement.Path] = replacement;
            affectedGuids.Add(replacement.Guid);
            affectedDuplicateKeys.Add(DuplicateKey(replacement));
            affectedParents.Add(replacement.ParentPath);
            InvalidateAssetCaches(replacement.Guid);
            if (!replacement.IsFolder) changedOwnerPaths.Add(replacement.Path);
        }

        private static void RefreshSecondaryIndexes(
            HashSet<string> affectedParents,
            HashSet<string> affectedDuplicateKeys)
        {
            if (affectedParents.Count > 64 || affectedDuplicateKeys.Count > 64)
            {
                RebuildLookupIndexes();
                return;
            }

            foreach (string parent in affectedParents)
            {
                List<BetterProjectAssetRecord> siblings = records
                    .Where(record => string.Equals(record.ParentPath, parent, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (siblings.Count == 0)
                {
                    children.Remove(parent);
                }
                else
                {
                    siblings.Sort(CompareDefault);
                    children[parent] = siblings;
                }
            }

            foreach (string key in affectedDuplicateKeys)
            {
                int count = records.Count(record =>
                    string.Equals(DuplicateKey(record), key, StringComparison.OrdinalIgnoreCase));
                if (count == 0) duplicateNames.Remove(key);
                else duplicateNames[key] = count;
            }
        }

        private static void RebuildLookupIndexes()
        {
            byGuid.Clear();
            byPath.Clear();
            children.Clear();
            duplicateNames.Clear();
            foreach (BetterProjectAssetRecord record in records)
            {
                byGuid[record.Guid] = record;
                byPath[record.Path] = record;
                if (!children.TryGetValue(record.ParentPath, out List<BetterProjectAssetRecord> siblings))
                {
                    siblings = new List<BetterProjectAssetRecord>();
                    children.Add(record.ParentPath, siblings);
                }
                siblings.Add(record);
                string key = DuplicateKey(record);
                duplicateNames.TryGetValue(key, out int count);
                duplicateNames[key] = count + 1;
            }
            foreach (List<BetterProjectAssetRecord> siblings in children.Values) siblings.Sort(CompareDefault);
        }

        private static void InvalidateRelatedPresentation(
            HashSet<string> affectedGuids,
            HashSet<string> duplicateKeys,
            HashSet<string> parentPaths)
        {
            foreach (BetterProjectAssetRecord record in records)
            {
                if (duplicateKeys.Contains(DuplicateKey(record)) || parentPaths.Contains(record.Path))
                {
                    affectedGuids.Add(record.Guid);
                }
            }
            foreach (string guid in affectedGuids)
            {
                diagnostics.Remove(guid);
                styles.Remove(guid);
            }
        }

        private static void InvalidateAssetCaches(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;
            subAssets.Remove(guid);
            labels.Remove(guid);
            diagnostics.Remove(guid);
            styles.Remove(guid);
            mainAssets.Remove(guid);
        }

        private static void UpdateReferencesIncrementally(
            IReadOnlyList<KeyValuePair<string, string>> moves,
            HashSet<string> deletedRoots,
            HashSet<string> changedOwnerPaths)
        {
            var remapped = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, HashSet<string>> entry in references)
            {
                string dependency = RemapMovedPath(entry.Key, moves);
                if (deletedRoots.Any(root => IsPathOrChild(dependency, root))) continue;
                if (!remapped.TryGetValue(dependency, out HashSet<string> owners))
                {
                    owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    remapped.Add(dependency, owners);
                }
                foreach (string rawOwner in entry.Value)
                {
                    string owner = RemapMovedPath(rawOwner, moves);
                    if (!deletedRoots.Any(root => IsPathOrChild(owner, root))) owners.Add(owner);
                }
            }
            references.Clear();
            foreach (KeyValuePair<string, HashSet<string>> entry in remapped) references[entry.Key] = entry.Value;

            foreach (HashSet<string> owners in references.Values)
            {
                owners.RemoveWhere(changedOwnerPaths.Contains);
            }
            foreach (string ownerPath in changedOwnerPaths)
            {
                BetterProjectAssetRecord owner = byPath.TryGetValue(ownerPath, out BetterProjectAssetRecord found)
                    ? found
                    : null;
                if (owner == null || owner.IsFolder) continue;
                foreach (string dependency in AssetDatabase.GetDependencies(owner.Path, false))
                {
                    if (string.Equals(dependency, owner.Path, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!references.TryGetValue(dependency, out HashSet<string> owners))
                    {
                        owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        references.Add(dependency, owners);
                    }
                    owners.Add(owner.Path);
                }
            }

            foreach (string emptyKey in references.Where(pair => pair.Value.Count == 0).Select(pair => pair.Key).ToArray())
            {
                references.Remove(emptyKey);
            }
            foreach (BetterProjectAssetRecord record in records)
            {
                int count = references.TryGetValue(record.Path, out HashSet<string> owners) ? owners.Count : 0;
                if (record.ReferenceCount != count)
                {
                    record.ReferenceCount = count;
                    diagnostics.Remove(record.Guid);
                    styles.Remove(record.Guid);
                }
            }
            referenceWork = records.Where(record => !record.IsFolder).ToList();
            referenceIndexCursor = referenceWork.Count;
        }

        private static string RemapMovedPath(
            string path,
            IReadOnlyList<KeyValuePair<string, string>> moves)
        {
            foreach (KeyValuePair<string, string> move in moves)
            {
                if (IsPathOrChild(path, move.Key))
                {
                    return move.Value + (path.Length == move.Key.Length ? string.Empty : path.Substring(move.Key.Length));
                }
            }
            return path;
        }

        private static bool IsPathOrChild(string path, string root)
        {
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }

        internal static string DuplicateKey(BetterProjectAssetRecord record)
        {
            return (record.IsPackage ? "Packages|" : "Assets|") +
                   record.Name + "|" + record.Extension;
        }

        private static void BuildReferenceBatch()
        {
            if (!referenceIndexing || referenceWork == null)
            {
                CancelReferenceIndex();
                return;
            }

            double startedAt = EditorApplication.timeSinceStartup;
            int processed = 0;
            while (referenceIndexCursor < referenceWork.Count && processed < ReferenceBatchLimit)
            {
                BetterProjectAssetRecord owner = referenceWork[referenceIndexCursor++];
                foreach (string dependency in AssetDatabase.GetDependencies(owner.Path, false))
                {
                    if (string.Equals(dependency, owner.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (!references.TryGetValue(dependency, out HashSet<string> owners))
                    {
                        owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        references.Add(dependency, owners);
                    }
                    owners.Add(owner.Path);
                }
                processed++;
                if ((EditorApplication.timeSinceStartup - startedAt) * 1000d >= ReferenceBatchBudgetMilliseconds)
                {
                    break;
                }
            }

            if (referenceIndexCursor >= referenceWork.Count)
            {
                CancelReferenceIndex();
                foreach (BetterProjectAssetRecord record in records)
                {
                    record.ReferenceCount = references.TryGetValue(record.Path, out HashSet<string> owners)
                        ? owners.Count
                        : 0;
                }
                diagnostics.Clear();
                styles.Clear();
                revision++;
                Changed?.Invoke();
            }
            ReferenceProgressChanged?.Invoke();
        }

        private static bool Matches(
            BetterProjectStyleRule rule,
            BetterProjectAssetRecord record,
            BetterProjectDiagnosticFlags flags)
        {
            string value = rule.Value ?? string.Empty;
            switch (rule.Match)
            {
                case BetterProjectRuleMatch.PathStartsWith:
                    return record.Path.StartsWith(value, StringComparison.OrdinalIgnoreCase);
                case BetterProjectRuleMatch.NameContains:
                    return record.Name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
                case BetterProjectRuleMatch.Type:
                    if (record.TypeName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                    // Preserve existing default rules created before imported models and
                    // prefabs had distinct display identities.
                    if (record.Kind == BetterProjectAssetKind.Prefab &&
                        string.Equals(value, "GameObject", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    return record.Kind == BetterProjectAssetKind.Sprite &&
                           value.IndexOf("Texture", StringComparison.OrdinalIgnoreCase) >= 0;
                case BetterProjectRuleMatch.Extension:
                    return string.Equals(record.Extension, value, StringComparison.OrdinalIgnoreCase);
                case BetterProjectRuleMatch.Label:
                    return GetLabels(record).Any(label => string.Equals(label, value, StringComparison.OrdinalIgnoreCase));
                case BetterProjectRuleMatch.Package:
                    return record.IsPackage;
                case BetterProjectRuleMatch.Folder:
                    return record.IsFolder;
                case BetterProjectRuleMatch.Diagnostic:
                    return value.Equals("critical", StringComparison.OrdinalIgnoreCase)
                        ? HasCriticalDiagnostics(flags)
                        : flags != BetterProjectDiagnosticFlags.None;
                case BetterProjectRuleMatch.Asset:
                    return string.Equals(record.Guid, value, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        private static bool HasMissingScripts(GameObject prefab)
        {
            foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsOversized(BetterProjectAssetRecord record)
        {
            if (record.Kind == BetterProjectAssetKind.Texture ||
                record.Kind == BetterProjectAssetKind.Sprite ||
                record.MainType != null && typeof(Texture).IsAssignableFrom(record.MainType))
            {
                return record.FileSize > OversizedTextureBytes;
            }
            if (record.MainType != null && typeof(AudioClip).IsAssignableFrom(record.MainType))
            {
                return record.FileSize > OversizedAudioBytes;
            }
            return record.FileSize > OversizedGeneralBytes;
        }

        private static bool IsModelExtension(string extension)
        {
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".fbx":
                case ".obj":
                case ".dae":
                case ".3ds":
                case ".dxf":
                case ".blend":
                case ".max":
                case ".ma":
                case ".mb":
                    return true;
                default:
                    return false;
            }
        }

        private static int CompareDefault(BetterProjectAssetRecord left, BetterProjectAssetRecord right)
        {
            if (left.IsFolder != right.IsFolder)
            {
                return left.IsFolder ? -1 : 1;
            }
            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        internal static string ResolvePhysicalPath(string assetPath)
        {
            if (assetPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                return Path.GetFullPath(assetPath);
            }
            PackageManagerInfo package = PackageManagerInfo.FindForAssetPath(assetPath);
            if (package == null)
            {
                return string.Empty;
            }
            string prefix = "Packages/" + package.name;
            string relative = assetPath.Length > prefix.Length
                ? assetPath.Substring(prefix.Length).TrimStart('/')
                : string.Empty;
            return Path.Combine(package.resolvedPath, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        internal static string Parent(string path)
        {
            path = Normalize(path);
            int slash = path.LastIndexOf('/');
            return slash <= 0 ? string.Empty : path.Substring(0, slash);
        }

        private static string LastSegment(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        private static bool IsIndexablePath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (path.StartsWith("Assets", StringComparison.Ordinal) ||
                    path.StartsWith("Packages/", StringComparison.Ordinal)) &&
                   !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTransientAsset(string path)
        {
            return string.Equals(
                Normalize(path),
                DansToolboxTransientAssets.RetroSfxPreviewPath,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class BetterProjectAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            BetterProjectIndex.QueueAssetChanges(
                importedAssets,
                deletedAssets,
                movedAssets,
                movedFromAssetPaths);
        }
    }
}
