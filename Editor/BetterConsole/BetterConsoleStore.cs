using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterConsole
{
    [InitializeOnLoad]
    internal static class BetterConsoleStore
    {
        private const double SaveDelaySeconds = 1.0d;
        private static readonly BetterConsoleHistoryData data;
        private static bool dirty;
        private static bool saveWarningIssued;
        private static double nextSaveAt;
        private static readonly HashSet<int> nativeLineIndexes = new HashSet<int>();

        static BetterConsoleStore()
        {
            data = Load();
            RebuildNativeLineIndex();
            EnsureEditorSession();
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += SaveNow;
            EditorApplication.quitting += SaveNow;
        }

        public static event Action Changed;
        public static int Revision { get; private set; }
        public static IReadOnlyList<BetterConsoleEntry> Entries => data.entries;
        public static IReadOnlyList<BetterConsoleSession> Sessions => data.sessions;
        public static BetterConsoleSession ActiveSession => data.sessions.LastOrDefault(item => item.active);

        public static void Add(BetterConsoleEntry entry)
        {
            if (entry == null) return;
            BetterConsoleSession session = PrepareEntry(entry);
            data.entries.Add(entry);
            if (entry.nativeLineIndex != 0) nativeLineIndexes.Add(entry.nativeLineIndex);
            Increment(session, entry.severity);
            int overflow = data.entries.Count - BetterConsoleSettings.MaxEntries;
            if (overflow > 0)
            {
                data.entries.RemoveRange(0, overflow);
                RebuildNativeLineIndex();
            }
            MarkChanged();
        }

        public static void AddRange(IReadOnlyList<BetterConsoleEntry> entries)
        {
            if (entries == null || entries.Count == 0) return;
            int added = 0;
            foreach (BetterConsoleEntry entry in entries)
            {
                if (entry == null) continue;
                BetterConsoleSession session = PrepareEntry(entry);
                data.entries.Add(entry);
                if (entry.nativeLineIndex != 0) nativeLineIndexes.Add(entry.nativeLineIndex);
                Increment(session, entry.severity);
                added++;
            }
            if (added == 0) return;

            int overflow = data.entries.Count - BetterConsoleSettings.MaxEntries;
            if (overflow > 0)
            {
                data.entries.RemoveRange(0, overflow);
                RebuildNativeLineIndex();
            }
            MarkChanged();
        }

        public static void ReconcileNativeSnapshot(IReadOnlyList<BetterConsoleEntry> nativeEntries)
        {
            IReadOnlyList<BetterConsoleEntry> snapshot = nativeEntries ?? Array.Empty<BetterConsoleEntry>();
            List<BetterConsoleEntry> previous = new List<BetterConsoleEntry>(data.entries);
            bool[] used = new bool[previous.Count];
            Dictionary<string, Queue<int>> lineMatches = BuildNativeMatchIndex(previous, NativeLineKey);
            Dictionary<string, Queue<int>> exactMatches = BuildNativeMatchIndex(previous, ExactKey);
            Dictionary<string, Queue<int>> messageMatches = BuildNativeMatchIndex(previous, MessageKey);
            List<BetterConsoleEntry> reconciled = new List<BetterConsoleEntry>(snapshot.Count);

            foreach (BetterConsoleEntry native in snapshot)
            {
                if (native == null) continue;
                int match = TakeNativeMatch(lineMatches, NativeLineKey(native), used);
                if (match < 0) match = TakeNativeMatch(exactMatches, ExactKey(native), used);
                if (match < 0) match = TakeNativeMatch(messageMatches, MessageKey(native), used);
                if (match >= 0)
                {
                    BetterConsoleEntry existing = previous[match];
                    used[match] = true;
                    MergeNativeMetadata(existing, native);
                    reconciled.Add(existing);
                }
                else
                {
                    PrepareEntry(native);
                    reconciled.Add(native);
                }
            }

            int overflow = reconciled.Count - BetterConsoleSettings.MaxEntries;
            if (overflow > 0) reconciled.RemoveRange(0, overflow);
            data.entries.Clear();
            data.entries.AddRange(reconciled);
            RebuildNativeLineIndex();
            RecalculateSessionCounts();
            MarkChanged();
        }

        public static BetterConsoleSession BeginSession(
            BetterConsoleSessionKind kind,
            string label,
            string source = "Editor")
        {
            EndActiveSession();
            BetterConsoleSession session = new BetterConsoleSession
            {
                id = Guid.NewGuid().ToString("N"),
                kind = kind,
                label = string.IsNullOrEmpty(label) ? kind.ToString() : label,
                source = string.IsNullOrEmpty(source) ? "Editor" : source,
                startUtcTicks = DateTime.UtcNow.Ticks,
                active = true
            };
            data.sessions.Add(session);
            TrimSessions();
            MarkChanged();
            return session;
        }

        public static void EndActiveSession()
        {
            BetterConsoleSession active = ActiveSession;
            if (active == null) return;
            active.active = false;
            active.endUtcTicks = DateTime.UtcNow.Ticks;
            MarkChanged();
        }

        public static void ResumeEditorSession()
        {
            if (ActiveSession?.kind == BetterConsoleSessionKind.Editor) return;
            BeginSession(BetterConsoleSessionKind.Editor, "EDITOR");
        }

        public static List<BetterConsoleIssue> BuildIssues(BetterConsoleQuery query, bool includeMuted)
        {
            Dictionary<string, BetterConsoleIssue> issues = new Dictionary<string, BetterConsoleIssue>();
            Dictionary<string, HashSet<string>> sessionSets = new Dictionary<string, HashSet<string>>();
            foreach (BetterConsoleEntry entry in data.entries)
            {
                BetterConsoleIssueState state = BetterConsoleSettings.GetIssueState(entry.signature);
                if (query != null && !query.Matches(entry, state)) continue;
                if (!includeMuted && BetterConsoleSettings.IsMuted(entry)) continue;

                if (!issues.TryGetValue(entry.signature, out BetterConsoleIssue issue))
                {
                    issue = new BetterConsoleIssue
                    {
                        signature = entry.signature,
                        representative = entry,
                        firstUtcTicks = entry.utcTicks,
                        triage = state?.triage ?? BetterConsoleTriage.New,
                        bookmarked = state?.bookmarked ?? false,
                        note = state?.note ?? string.Empty
                    };
                    issues.Add(entry.signature, issue);
                    sessionSets.Add(entry.signature, new HashSet<string>());
                }

                issue.count++;
                issue.lastUtcTicks = entry.utcTicks;
                issue.representative = entry;
                sessionSets[entry.signature].Add(entry.sessionId);
            }

            foreach (BetterConsoleIssue issue in issues.Values)
            {
                issue.sessionCount = sessionSets[issue.signature].Count;
                double minutes = Math.Max(
                    1d / 60d,
                    TimeSpan.FromTicks(Math.Max(0, issue.lastUtcTicks - issue.firstUtcTicks)).TotalMinutes);
                issue.perMinute = (float)(issue.count / minutes);
            }

            return issues.Values
                .OrderByDescending(item => item.bookmarked)
                .ThenByDescending(item => item.representative.severity)
                .ThenByDescending(item => item.lastUtcTicks)
                .ToList();
        }

        public static List<BetterConsoleEntry> FilterEntries(
            BetterConsoleQuery query,
            bool logs,
            bool warnings,
            bool errors,
            bool includeMuted,
            string sessionId = null)
        {
            IEnumerable<BetterConsoleEntry> source = data.entries;
            if (!string.IsNullOrEmpty(sessionId))
            {
                source = source.Where(entry => string.Equals(entry.sessionId, sessionId, StringComparison.Ordinal));
            }

            return source.Where(entry =>
                    SeverityVisible(entry.severity, logs, warnings, errors) &&
                    (includeMuted || !BetterConsoleSettings.IsMuted(entry)) &&
                    (query == null || query.Matches(entry, BetterConsoleSettings.GetIssueState(entry.signature))))
                .ToList();
        }

        public static void Clear()
        {
            data.entries.Clear();
            nativeLineIndexes.Clear();
            foreach (BetterConsoleSession session in data.sessions)
            {
                session.logs = 0;
                session.warnings = 0;
                session.errors = 0;
            }
            MarkChanged();
            SaveNow();
        }

        public static bool ContainsNativeLine(int nativeLineIndex)
        {
            return nativeLineIndex != 0 && nativeLineIndexes.Contains(nativeLineIndex);
        }

        public static void SaveNow()
        {
            if (!dirty) return;
            if (!BetterConsoleSettings.PersistHistory)
            {
                dirty = false;
                return;
            }
            try
            {
                string path = HistoryPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(data, false));
                dirty = false;
                saveWarningIssued = false;
            }
            catch (Exception exception)
            {
                dirty = false;
                if (!saveWarningIssued)
                {
                    saveWarningIssued = true;
                    Debug.LogWarning($"Better Console could not save history: {exception.Message}");
                }
            }
        }

        private static BetterConsoleHistoryData Load()
        {
            if (!BetterConsoleSettings.PersistHistory || !File.Exists(HistoryPath))
            {
                return new BetterConsoleHistoryData();
            }

            try
            {
                BetterConsoleHistoryData loaded = JsonUtility.FromJson<BetterConsoleHistoryData>(File.ReadAllText(HistoryPath));
                if (loaded?.entries != null)
                {
                    foreach (BetterConsoleEntry entry in loaded.entries)
                    {
                        if (entry.contextInstanceId != 0 && string.IsNullOrEmpty(entry.contextName))
                        {
                            entry.contextInstanceId = 0;
                        }
                    }
                }
                return loaded ?? new BetterConsoleHistoryData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Better Console history was reset: {exception.Message}");
                return new BetterConsoleHistoryData();
            }
        }

        private static BetterConsoleSession EnsureEditorSession()
        {
            BetterConsoleSession active = ActiveSession;
            if (active != null) return active;
            return BeginSession(BetterConsoleSessionKind.Editor, "EDITOR");
        }

        private static void Update()
        {
            if (dirty && EditorApplication.timeSinceStartup >= nextSaveAt) SaveNow();
        }

        private static BetterConsoleSession PrepareEntry(BetterConsoleEntry entry)
        {
            entry.id = data.nextEntryId++;
            if (entry.utcTicks <= 0) entry.utcTicks = DateTime.UtcNow.Ticks;
            BetterConsoleSession session = string.IsNullOrEmpty(entry.sessionId)
                ? ActiveSession ?? EnsureEditorSession()
                : data.sessions.FirstOrDefault(item => item != null && item.id == entry.sessionId)
                  ?? ActiveSession ?? EnsureEditorSession();
            if (string.IsNullOrEmpty(entry.sessionId)) entry.sessionId = session.id;
            if (string.IsNullOrEmpty(entry.sessionLabel)) entry.sessionLabel = session.label;
            entry.sessionKind = session.kind;
            entry.signature = BetterConsoleClassification.Signature(entry);
            BetterConsoleClassification.ParseFileLocation(entry);
            entry.category = BetterConsoleClassification.Categorize(
                entry.message,
                entry.stackTrace,
                entry.file,
                session.kind);
            return session;
        }

        private static Dictionary<string, Queue<int>> BuildNativeMatchIndex(
            IReadOnlyList<BetterConsoleEntry> entries,
            Func<BetterConsoleEntry, string> keyFor)
        {
            Dictionary<string, Queue<int>> result = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Count; index++)
            {
                BetterConsoleEntry entry = entries[index];
                string key = keyFor(entry);
                if (string.IsNullOrEmpty(key)) continue;
                if (!result.TryGetValue(key, out Queue<int> queue))
                {
                    queue = new Queue<int>();
                    result.Add(key, queue);
                }
                queue.Enqueue(index);
            }
            return result;
        }

        private static int TakeNativeMatch(
            IReadOnlyDictionary<string, Queue<int>> index,
            string key,
            IReadOnlyList<bool> used)
        {
            if (string.IsNullOrEmpty(key) || !index.TryGetValue(key, out Queue<int> queue)) return -1;
            while (queue.Count > 0)
            {
                int candidate = queue.Dequeue();
                if (!used[candidate]) return candidate;
            }
            return -1;
        }

        private static string NativeLineKey(BetterConsoleEntry entry)
        {
            return entry == null || entry.nativeLineIndex == 0
                ? string.Empty
                : entry.nativeLineIndex + "\u001f" + (int)entry.severity + "\u001f" + entry.message;
        }

        private static string ExactKey(BetterConsoleEntry entry)
        {
            return entry == null ? string.Empty :
                (int)entry.severity + "\u001f" + entry.message + "\u001f" + entry.stackTrace;
        }

        private static string MessageKey(BetterConsoleEntry entry)
        {
            return entry == null ? string.Empty : (int)entry.severity + "\u001f" + entry.message;
        }

        private static void MergeNativeMetadata(BetterConsoleEntry existing, BetterConsoleEntry native)
        {
            if (native.nativeLineIndex != 0) existing.nativeLineIndex = native.nativeLineIndex;
            if (string.IsNullOrEmpty(existing.file)) existing.file = native.file;
            if (existing.line <= 0) existing.line = native.line;
            if (existing.column <= 0) existing.column = native.column;
            if (existing.contextInstanceId == 0) existing.contextInstanceId = native.contextInstanceId;
            if (string.IsNullOrEmpty(existing.contextName)) existing.contextName = native.contextName;
            BetterConsoleClassification.ParseFileLocation(existing);
        }

        private static void RecalculateSessionCounts()
        {
            Dictionary<string, BetterConsoleSession> sessions = data.sessions
                .Where(session => session != null)
                .ToDictionary(session => session.id, session => session);
            foreach (BetterConsoleSession session in sessions.Values)
            {
                session.logs = 0;
                session.warnings = 0;
                session.errors = 0;
            }

            BetterConsoleSession fallback = ActiveSession ?? EnsureEditorSession();
            foreach (BetterConsoleEntry entry in data.entries)
            {
                if (!sessions.TryGetValue(entry.sessionId, out BetterConsoleSession session)) session = fallback;
                Increment(session, entry.severity);
            }
        }

        private static void MarkChanged()
        {
            dirty = true;
            Revision++;
            nextSaveAt = EditorApplication.timeSinceStartup + SaveDelaySeconds;
            Changed?.Invoke();
        }

        private static void RebuildNativeLineIndex()
        {
            nativeLineIndexes.Clear();
            foreach (BetterConsoleEntry entry in data.entries)
            {
                if (entry != null && entry.nativeLineIndex != 0) nativeLineIndexes.Add(entry.nativeLineIndex);
            }
        }

        private static void TrimSessions()
        {
            int overflow = data.sessions.Count - BetterConsoleSettings.MaxSessions;
            if (overflow <= 0) return;
            HashSet<string> removed = new HashSet<string>(
                data.sessions.Take(overflow).Select(item => item.id));
            data.sessions.RemoveRange(0, overflow);
            data.entries.RemoveAll(entry => removed.Contains(entry.sessionId));
        }

        private static void Increment(BetterConsoleSession session, BetterConsoleSeverity severity)
        {
            if (severity == BetterConsoleSeverity.Log) session.logs++;
            else if (severity == BetterConsoleSeverity.Warning) session.warnings++;
            else session.errors++;
        }

        private static bool SeverityVisible(
            BetterConsoleSeverity severity,
            bool logs,
            bool warnings,
            bool errors)
        {
            if (severity == BetterConsoleSeverity.Log) return logs;
            if (severity == BetterConsoleSeverity.Warning) return warnings;
            return errors;
        }

        private static string HistoryPath => Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "Library",
            "DansToolbox",
            "BetterConsole",
            "history.json"));
    }
}
