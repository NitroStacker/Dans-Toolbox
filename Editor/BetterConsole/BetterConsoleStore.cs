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

        static BetterConsoleStore()
        {
            data = Load();
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
            entry.id = data.nextEntryId++;
            if (entry.utcTicks <= 0) entry.utcTicks = DateTime.UtcNow.Ticks;
            BetterConsoleSession session = ActiveSession ?? EnsureEditorSession();
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
            data.entries.Add(entry);
            Increment(session, entry.severity);

            int overflow = data.entries.Count - BetterConsoleSettings.MaxEntries;
            if (overflow > 0) data.entries.RemoveRange(0, overflow);
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
            return nativeLineIndex != 0 && data.entries.Any(entry => entry.nativeLineIndex == nativeLineIndex);
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

        private static void MarkChanged()
        {
            dirty = true;
            Revision++;
            nextSaveAt = EditorApplication.timeSinceStartup + SaveDelaySeconds;
            Changed?.Invoke();
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
