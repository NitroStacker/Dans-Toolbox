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
        private const int ReferenceBatchSize = 18;
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
        private static readonly Dictionary<string, HashSet<string>> references =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> duplicateNames =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> duplicateContentGuids =
            new HashSet<string>(StringComparer.Ordinal);

        private static int revision;
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
            if (records.Count == 0)
            {
                Refresh();
            }
        }

        internal static void Refresh()
        {
            records.Clear();
            byGuid.Clear();
            byPath.Clear();
            children.Clear();
            subAssets.Clear();
            labels.Clear();
            diagnostics.Clear();
            references.Clear();
            duplicateNames.Clear();
            duplicateContentGuids.Clear();
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

                bool folder = AssetDatabase.IsValidFolder(path);
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                string physicalPath = ResolvePhysicalPath(path);
                Type mainType = folder ? typeof(DefaultAsset) : AssetDatabase.GetMainAssetTypeAtPath(path);
                AssetImporter importer = folder ? null : AssetImporter.GetAtPath(path);
                var record = new BetterProjectAssetRecord
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
                    FileSize = !folder && File.Exists(physicalPath) ? new FileInfo(physicalPath).Length : 0L,
                    ModifiedUtc = File.Exists(physicalPath) || Directory.Exists(physicalPath)
                        ? File.GetLastWriteTimeUtc(physicalPath)
                        : default
                };
                records.Add(record);
                byGuid[guid] = record;
                byPath[path] = record;

                if (!children.TryGetValue(record.ParentPath, out List<BetterProjectAssetRecord> siblings))
                {
                    siblings = new List<BetterProjectAssetRecord>();
                    children.Add(record.ParentPath, siblings);
                }
                siblings.Add(record);

                string nameKey = record.Name + "|" + record.Extension;
                duplicateNames.TryGetValue(nameKey, out int count);
                duplicateNames[nameKey] = count + 1;
            }

            foreach (List<BetterProjectAssetRecord> siblingList in children.Values)
            {
                siblingList.Sort(CompareDefault);
            }
            revision++;
            Changed?.Invoke();
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
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(record.Path);
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
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(record.Path);
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

            string duplicateKey = record.Name + "|" + record.Extension;
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
            return new BetterProjectStyle(winner);
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
            referenceWork = records.Where(record => !record.IsFolder).ToList();
            referenceIndexCursor = 0;
            referenceIndexing = true;
            EditorApplication.update -= BuildReferenceBatch;
            EditorApplication.update += BuildReferenceBatch;
            Changed?.Invoke();
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
            if (!ShouldRefreshForAssetChanges(changedPathGroups))
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
                Refresh();
            };
        }

        private static void BuildReferenceBatch()
        {
            if (!referenceIndexing || referenceWork == null)
            {
                CancelReferenceIndex();
                return;
            }

            int end = Math.Min(referenceWork.Count, referenceIndexCursor + ReferenceBatchSize);
            for (; referenceIndexCursor < end; referenceIndexCursor++)
            {
                BetterProjectAssetRecord owner = referenceWork[referenceIndexCursor];
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
            }
            revision++;
            Changed?.Invoke();
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
                    return flags != BetterProjectDiagnosticFlags.None;
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
            BetterProjectIndex.QueueRefresh(
                importedAssets,
                deletedAssets,
                movedAssets,
                movedFromAssetPaths);
        }
    }
}
