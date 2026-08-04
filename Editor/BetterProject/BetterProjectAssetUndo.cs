using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditorInternal;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterProject
{
    internal enum BetterProjectAssetUndoActionKind
    {
        Move,
        Created,
        Deleted,
        Labels,
        Importer
    }

    [Serializable]
    internal sealed class BetterProjectAssetUndoAction
    {
        [SerializeField] private BetterProjectAssetUndoActionKind kind;
        [SerializeField] private string sourcePath = string.Empty;
        [SerializeField] private string destinationPath = string.Empty;
        [SerializeField] private string stashPath = string.Empty;
        [SerializeField] private string guid = string.Empty;
        [SerializeField] private List<string> beforeValues = new List<string>();
        [SerializeField] private List<string> afterValues = new List<string>();
        [SerializeField] private string beforeSnapshotPath = string.Empty;
        [SerializeField] private string afterSnapshotPath = string.Empty;

        internal BetterProjectAssetUndoActionKind Kind => kind;
        internal string SourcePath => sourcePath;
        internal string DestinationPath => destinationPath;
        internal string StashPath => stashPath;
        internal string Guid => guid;
        internal IReadOnlyList<string> BeforeValues => beforeValues;
        internal IReadOnlyList<string> AfterValues => afterValues;
        internal string BeforeSnapshotPath => beforeSnapshotPath;
        internal string AfterSnapshotPath => afterSnapshotPath;

        internal static BetterProjectAssetUndoAction Move(string source, string destination)
        {
            return new BetterProjectAssetUndoAction
            {
                kind = BetterProjectAssetUndoActionKind.Move,
                sourcePath = Normalize(source),
                destinationPath = Normalize(destination)
            };
        }

        internal static BetterProjectAssetUndoAction Created(string assetPath, string stash)
        {
            return new BetterProjectAssetUndoAction
            {
                kind = BetterProjectAssetUndoActionKind.Created,
                destinationPath = Normalize(assetPath),
                stashPath = stash
            };
        }

        internal static BetterProjectAssetUndoAction Deleted(string assetPath, string stash)
        {
            return new BetterProjectAssetUndoAction
            {
                kind = BetterProjectAssetUndoActionKind.Deleted,
                sourcePath = Normalize(assetPath),
                stashPath = stash
            };
        }

        internal static BetterProjectAssetUndoAction Labels(
            string assetGuid,
            string assetPath,
            IEnumerable<string> before,
            IEnumerable<string> after)
        {
            return new BetterProjectAssetUndoAction
            {
                kind = BetterProjectAssetUndoActionKind.Labels,
                guid = assetGuid ?? string.Empty,
                sourcePath = Normalize(assetPath),
                beforeValues = new List<string>(before ?? Array.Empty<string>()),
                afterValues = new List<string>(after ?? Array.Empty<string>())
            };
        }

        internal static BetterProjectAssetUndoAction Importer(
            string assetGuid,
            string assetPath,
            string beforeSnapshot,
            string afterSnapshot)
        {
            return new BetterProjectAssetUndoAction
            {
                kind = BetterProjectAssetUndoActionKind.Importer,
                guid = assetGuid ?? string.Empty,
                sourcePath = Normalize(assetPath),
                beforeSnapshotPath = beforeSnapshot ?? string.Empty,
                afterSnapshotPath = afterSnapshot ?? string.Empty
            };
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }
    }

    [Serializable]
    internal sealed class BetterProjectAssetUndoTransaction
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string name = string.Empty;
        [SerializeField] private List<BetterProjectAssetUndoAction> actions =
            new List<BetterProjectAssetUndoAction>();

        internal string Id => id;
        internal string Name => name;
        internal List<BetterProjectAssetUndoAction> Actions => actions;
        internal bool HasActions => actions.Count > 0;

        internal static BetterProjectAssetUndoTransaction Create(string transactionName)
        {
            return new BetterProjectAssetUndoTransaction
            {
                id = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrWhiteSpace(transactionName)
                    ? "Better Project Asset Change"
                    : transactionName
            };
        }
    }

    [FilePath(
        "Library/DansToolbox/BetterProjectAssetUndo.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class BetterProjectAssetUndoJournal : ScriptableSingleton<BetterProjectAssetUndoJournal>
    {
        [SerializeField] private string sessionId = string.Empty;
        [SerializeField] private int cursor;
        [SerializeField] private List<BetterProjectAssetUndoTransaction> transactions =
            new List<BetterProjectAssetUndoTransaction>();

        internal string SessionId => sessionId;
        internal int Cursor => cursor;
        internal IReadOnlyList<BetterProjectAssetUndoTransaction> Transactions => transactions;

        internal void BeginSession(string value)
        {
            sessionId = value;
            cursor = 0;
            transactions.Clear();
            Save(true);
        }

        internal void Prepare(BetterProjectAssetUndoTransaction transaction)
        {
            if (cursor < transactions.Count)
            {
                transactions.RemoveRange(cursor, transactions.Count - cursor);
            }
            transactions.Add(transaction);
            Save(true);
        }

        internal void Advance()
        {
            cursor++;
            Save(true);
        }

        internal void RestoreCursor(int value)
        {
            cursor = Mathf.Clamp(value, 0, transactions.Count);
            Save(true);
        }

        internal void ResetState()
        {
            cursor = 0;
            transactions.Clear();
            Save(true);
        }
    }

    [InitializeOnLoad]
    internal static class BetterProjectAssetUndo
    {
        private const string SessionKey = "DansToolbox.BetterProject.AssetUndo.Session";
        private const string StashFolder = "Library/DansToolbox/BetterProjectUndo";

        private static bool initialized;
        private static bool applyingUndoRedo;
        private static int appliedCursor;

        internal static int Cursor
        {
            get
            {
                EnsureInitialized();
                return BetterProjectAssetUndoJournal.instance.Cursor;
            }
        }

        internal static int AppliedCursor => appliedCursor;

        static BetterProjectAssetUndo()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.delayCall += EnsureInitialized;
        }

        internal static BetterProjectAssetUndoTransaction Begin(string name)
        {
            EnsureInitialized();
            return BetterProjectAssetUndoTransaction.Create(name);
        }

        internal static void RecordMove(
            BetterProjectAssetUndoTransaction transaction,
            string sourcePath,
            string destinationPath)
        {
            if (transaction == null) return;
            transaction.Actions.Add(BetterProjectAssetUndoAction.Move(sourcePath, destinationPath));
        }

        internal static void RecordCreated(
            BetterProjectAssetUndoTransaction transaction,
            string assetPath)
        {
            if (transaction == null) return;
            string stash = BuildStashPath(transaction, assetPath);
            transaction.Actions.Add(BetterProjectAssetUndoAction.Created(assetPath, stash));
        }

        internal static bool TryDelete(
            BetterProjectAssetUndoTransaction transaction,
            string assetPath)
        {
            if (transaction == null) return false;
            string stash = BuildStashPath(transaction, assetPath);
            BetterProjectAssetUndoAction action = BetterProjectAssetUndoAction.Deleted(assetPath, stash);
            if (!ApplyAction(action, true, out string error))
            {
                Debug.LogError("Better Project could not move '" + assetPath + "' to its undo stash: " + error);
                return false;
            }
            transaction.Actions.Add(action);
            return true;
        }

        internal static void RecordLabels(
            BetterProjectAssetUndoTransaction transaction,
            string guid,
            string assetPath,
            IEnumerable<string> before,
            IEnumerable<string> after)
        {
            if (transaction == null) return;
            transaction.Actions.Add(BetterProjectAssetUndoAction.Labels(
                guid,
                assetPath,
                before,
                after));
        }

        internal static void RecordImporter(
            BetterProjectAssetUndoTransaction transaction,
            string guid,
            string assetPath,
            Preset before,
            Preset after)
        {
            if (transaction == null || before == null || after == null) return;
            string actionRoot = transaction.Id + "/" + transaction.Actions.Count;
            string beforePath = actionRoot + "/before.presetundo";
            string afterPath = actionRoot + "/after.presetundo";
            SavePresetSnapshot(before, beforePath);
            SavePresetSnapshot(after, afterPath);
            transaction.Actions.Add(BetterProjectAssetUndoAction.Importer(
                guid,
                assetPath,
                beforePath,
                afterPath));
        }

        internal static void Commit(
            BetterProjectAssetUndoTransaction transaction,
            int existingUndoGroup = -1)
        {
            if (transaction == null || !transaction.HasActions) return;
            EnsureInitialized();

            BetterProjectAssetUndoJournal journal = BetterProjectAssetUndoJournal.instance;
            RemoveAbandonedRedoStashes(journal);
            journal.Prepare(transaction);

            if (existingUndoGroup < 0) Undo.IncrementCurrentGroup();
            int group = existingUndoGroup < 0 ? Undo.GetCurrentGroup() : existingUndoGroup;
            Undo.SetCurrentGroupName(transaction.Name);
            Undo.RegisterCompleteObjectUndo(journal, transaction.Name);
            journal.Advance();
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            appliedCursor = journal.Cursor;
        }

        internal static void RefreshAfterFilesystemChange()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BetterProjectIndex.InvalidatePresentation();
        }

        internal static void ResetForTests()
        {
            EnsureInitialized();
            BetterProjectAssetUndoJournal journal = BetterProjectAssetUndoJournal.instance;
            Undo.ClearUndo(journal);
            DeleteDirectoryIfPresent(StashRoot);
            journal.ResetState();
            appliedCursor = 0;
        }

        private static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            string session = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(session))
            {
                session = Guid.NewGuid().ToString("N");
                SessionState.SetString(SessionKey, session);
            }

            BetterProjectAssetUndoJournal journal = BetterProjectAssetUndoJournal.instance;
            if (!string.Equals(journal.SessionId, session, StringComparison.Ordinal))
            {
                Undo.ClearUndo(journal);
                DeleteDirectoryIfPresent(StashRoot);
                journal.BeginSession(session);
            }
            appliedCursor = journal.Cursor;
        }

        private static void OnUndoRedo()
        {
            if (applyingUndoRedo) return;
            EnsureInitialized();

            BetterProjectAssetUndoJournal journal = BetterProjectAssetUndoJournal.instance;
            int requestedCursor = journal.Cursor;
            if (requestedCursor == appliedCursor)
            {
                BetterProjectIndex.InvalidatePresentation();
                return;
            }

            applyingUndoRedo = true;
            int originalCursor = appliedCursor;
            bool success = true;
            try
            {
                if (requestedCursor < appliedCursor)
                {
                    for (int index = appliedCursor - 1; index >= requestedCursor; index--)
                    {
                        if (!ApplyTransaction(journal.Transactions[index], false))
                        {
                            success = false;
                            break;
                        }
                        appliedCursor--;
                    }
                }
                else
                {
                    for (int index = appliedCursor; index < requestedCursor; index++)
                    {
                        if (!ApplyTransaction(journal.Transactions[index], true))
                        {
                            success = false;
                            break;
                        }
                        appliedCursor++;
                    }
                }

                if (!success)
                {
                    journal.RestoreCursor(appliedCursor);
                    Debug.LogError(
                        "Better Project could not complete asset Undo/Redo. " +
                        "The journal was stopped at its last safe operation (" +
                        originalCursor + " -> " + appliedCursor + ").");
                }
                RefreshAfterFilesystemChange();
            }
            finally
            {
                applyingUndoRedo = false;
            }
        }

        private static bool ApplyTransaction(BetterProjectAssetUndoTransaction transaction, bool forward)
        {
            BetterProjectAssetUndoAction[] actions = forward
                ? transaction.Actions.ToArray()
                : transaction.Actions.AsEnumerable().Reverse().ToArray();
            var completed = new List<BetterProjectAssetUndoAction>();
            foreach (BetterProjectAssetUndoAction action in actions)
            {
                if (ApplyAction(action, forward, out string error))
                {
                    completed.Add(action);
                    continue;
                }

                Debug.LogError(
                    "Better Project could not " + (forward ? "redo" : "undo") +
                    " '" + transaction.Name + "': " + error);
                foreach (BetterProjectAssetUndoAction rollback in completed.AsEnumerable().Reverse())
                {
                    if (!ApplyAction(rollback, !forward, out string rollbackError))
                    {
                        Debug.LogError("Better Project asset Undo rollback failed: " + rollbackError);
                    }
                }
                return false;
            }
            return true;
        }

        private static bool ApplyAction(
            BetterProjectAssetUndoAction action,
            bool forward,
            out string error)
        {
            error = string.Empty;
            try
            {
                switch (action.Kind)
                {
                    case BetterProjectAssetUndoActionKind.Move:
                    {
                        string source = forward ? action.SourcePath : action.DestinationPath;
                        string destination = forward ? action.DestinationPath : action.SourcePath;
                        error = AssetDatabase.MoveAsset(source, destination);
                        return string.IsNullOrEmpty(error);
                    }
                    case BetterProjectAssetUndoActionKind.Created:
                        return forward
                            ? RestoreFromStash(action.StashPath, action.DestinationPath, out error)
                            : MoveToStash(action.DestinationPath, action.StashPath, out error);
                    case BetterProjectAssetUndoActionKind.Deleted:
                        return forward
                            ? MoveToStash(action.SourcePath, action.StashPath, out error)
                            : RestoreFromStash(action.StashPath, action.SourcePath, out error);
                    case BetterProjectAssetUndoActionKind.Labels:
                    {
                        string path = ResolveCurrentAssetPath(action.Guid, action.SourcePath);
                        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                        if (asset == null)
                        {
                            error = "Could not load labeled asset: " + path;
                            return false;
                        }
                        AssetDatabase.SetLabels(
                            asset,
                            (forward ? action.AfterValues : action.BeforeValues).ToArray());
                        EditorUtility.SetDirty(asset);
                        return true;
                    }
                    case BetterProjectAssetUndoActionKind.Importer:
                    {
                        string path = ResolveCurrentAssetPath(action.Guid, action.SourcePath);
                        AssetImporter importer = AssetImporter.GetAtPath(path);
                        if (importer == null)
                        {
                            error = "Could not load importer: " + path;
                            return false;
                        }
                        string snapshotPath = forward
                            ? action.AfterSnapshotPath
                            : action.BeforeSnapshotPath;
                        UnityEngine.Object[] snapshot = InternalEditorUtility.LoadSerializedFileAndForget(
                            StashPathToAbsolute(snapshotPath));
                        Preset preset = snapshot.OfType<Preset>().FirstOrDefault();
                        if (preset == null || !preset.CanBeAppliedTo(importer))
                        {
                            foreach (UnityEngine.Object item in snapshot)
                                if (item != null) UnityEngine.Object.DestroyImmediate(item);
                            error = "Importer undo snapshot is missing or incompatible: " + path;
                            return false;
                        }
                        preset.ApplyTo(importer);
                        importer.SaveAndReimport();
                        foreach (UnityEngine.Object item in snapshot)
                            if (item != null) UnityEngine.Object.DestroyImmediate(item);
                        return true;
                    }
                    default:
                        error = "Unknown asset undo action.";
                        return false;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool MoveToStash(string assetPath, string stashPath, out string error)
        {
            string source = AssetPathToAbsolute(assetPath);
            string destination = StashPathToAbsolute(stashPath);
            return MoveContentAndMeta(source, destination, out error);
        }

        private static bool RestoreFromStash(string stashPath, string assetPath, out string error)
        {
            string source = StashPathToAbsolute(stashPath);
            string destination = AssetPathToAbsolute(assetPath);
            bool result = MoveContentAndMeta(source, destination, out error);
            if (result) DeleteEmptyStashParents(Path.GetDirectoryName(source));
            return result;
        }

        private static bool MoveContentAndMeta(string source, string destination, out string error)
        {
            error = string.Empty;
            bool sourceIsFile = File.Exists(source);
            bool sourceIsDirectory = Directory.Exists(source);
            if (!sourceIsFile && !sourceIsDirectory)
            {
                error = "Source does not exist: " + source;
                return false;
            }
            if (File.Exists(destination) || Directory.Exists(destination) ||
                File.Exists(destination + ".meta") || Directory.Exists(destination + ".meta"))
            {
                error = "Destination already exists: " + destination;
                return false;
            }

            string parent = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(parent))
            {
                error = "Destination has no parent folder: " + destination;
                return false;
            }
            Directory.CreateDirectory(parent);

            bool movedContent = false;
            try
            {
                if (sourceIsDirectory) Directory.Move(source, destination);
                else File.Move(source, destination);
                movedContent = true;

                if (File.Exists(source + ".meta")) File.Move(source + ".meta", destination + ".meta");
                return true;
            }
            catch (Exception exception)
            {
                if (movedContent && !File.Exists(source) && !Directory.Exists(source))
                {
                    string sourceParent = Path.GetDirectoryName(source);
                    if (!string.IsNullOrEmpty(sourceParent)) Directory.CreateDirectory(sourceParent);
                    if (Directory.Exists(destination)) Directory.Move(destination, source);
                    else if (File.Exists(destination)) File.Move(destination, source);
                }
                error = exception.Message;
                return false;
            }
        }

        private static string BuildStashPath(
            BetterProjectAssetUndoTransaction transaction,
            string assetPath)
        {
            string fileName = Path.GetFileName((assetPath ?? string.Empty).TrimEnd('/', '\\'));
            return transaction.Id + "/" + transaction.Actions.Count + "/" + fileName;
        }

        private static string ResolveCurrentAssetPath(string guid, string fallbackPath)
        {
            string path = string.IsNullOrEmpty(guid)
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? fallbackPath : path;
        }

        private static void SavePresetSnapshot(Preset preset, string stashPath)
        {
            string absolute = StashPathToAbsolute(stashPath);
            string parent = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            InternalEditorUtility.SaveToSerializedFileAndForget(
                new UnityEngine.Object[] { preset },
                absolute,
                false);
        }

        private static string AssetPathToAbsolute(string assetPath)
        {
            string normalized = (assetPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            string result = Path.GetFullPath(Path.Combine(ProjectRoot, normalized));
            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!result.Equals(assetsRoot, StringComparison.OrdinalIgnoreCase) &&
                !result.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Asset undo path is outside Assets: " + assetPath);
            }
            return result;
        }

        private static string StashPathToAbsolute(string stashPath)
        {
            string result = Path.GetFullPath(Path.Combine(
                StashRoot,
                (stashPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
            string root = Path.GetFullPath(StashRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!result.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Asset undo stash path escaped its root.");
            }
            return result;
        }

        private static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

        private static string StashRoot => Path.Combine(ProjectRoot, StashFolder);

        private static void RemoveAbandonedRedoStashes(BetterProjectAssetUndoJournal journal)
        {
            for (int index = journal.Cursor; index < journal.Transactions.Count; index++)
            {
                DeleteDirectoryIfPresent(Path.Combine(StashRoot, journal.Transactions[index].Id));
            }
        }

        private static void DeleteEmptyStashParents(string path)
        {
            string root = Path.GetFullPath(StashRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string current = path;
            while (!string.IsNullOrEmpty(current) &&
                   current.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                   Directory.Exists(current) &&
                   !Directory.EnumerateFileSystemEntries(current).Any())
            {
                Directory.Delete(current);
                current = Path.GetDirectoryName(current);
            }
        }

        private static void DeleteDirectoryIfPresent(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            Directory.Delete(path, true);
        }
    }
}
