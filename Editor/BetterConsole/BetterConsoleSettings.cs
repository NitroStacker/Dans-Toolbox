using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterConsole
{
    [FilePath("ProjectSettings/BetterConsoleSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class BetterConsoleSettings : ScriptableSingleton<BetterConsoleSettings>
    {
        private static readonly Dictionary<string, BetterConsoleQuery> queryCache =
            new Dictionary<string, BetterConsoleQuery>(StringComparer.Ordinal);
        private static readonly Dictionary<string, BetterConsoleIssueState> issueStateCache =
            new Dictionary<string, BetterConsoleIssueState>(StringComparer.Ordinal);
        private static bool issueStateCacheReady;
        [SerializeField] private int maxEntries = 10000;
        [SerializeField] private int maxSessions = 30;
        [SerializeField] private bool captureNativeHistory = true;
        [SerializeField] private bool persistHistory = true;
        [SerializeField] private bool errorPause;
        [SerializeField] private bool collapse;
        [SerializeField] private bool showTimestamps = true;
        [SerializeField] private bool follow = true;
        [SerializeField] private bool detailsVisible = true;
        [SerializeField] private bool dense = true;
        [SerializeField] private List<BetterConsoleSavedView> savedViews = new List<BetterConsoleSavedView>();
        [SerializeField] private List<BetterConsoleIssueState> issueStates = new List<BetterConsoleIssueState>();
        [SerializeField] private List<BetterConsoleMuteRule> muteRules = new List<BetterConsoleMuteRule>();

        public static int MaxEntries => Mathf.Clamp(instance.maxEntries, 500, 100000);
        public static int MaxSessions => Mathf.Clamp(instance.maxSessions, 5, 200);
        public static bool CaptureNativeHistory { get => instance.captureNativeHistory; set => Set(ref instance.captureNativeHistory, value); }
        public static bool PersistHistory { get => instance.persistHistory; set => Set(ref instance.persistHistory, value); }
        public static bool ErrorPause { get => instance.errorPause; set => Set(ref instance.errorPause, value); }
        public static bool Collapse { get => instance.collapse; set => Set(ref instance.collapse, value); }
        public static bool ShowTimestamps { get => instance.showTimestamps; set => Set(ref instance.showTimestamps, value); }
        public static bool Follow { get => instance.follow; set => Set(ref instance.follow, value); }
        public static bool DetailsVisible { get => instance.detailsVisible; set => Set(ref instance.detailsVisible, value); }
        public static bool Dense { get => instance.dense; set => Set(ref instance.dense, value); }
        public static IReadOnlyList<BetterConsoleSavedView> SavedViews => instance.savedViews;
        public static IReadOnlyList<BetterConsoleMuteRule> MuteRules => instance.muteRules;

        public static BetterConsoleIssueState GetIssueState(string signature, bool create = false)
        {
            if (string.IsNullOrEmpty(signature))
            {
                return null;
            }

            EnsureIssueStateCache();
            issueStateCache.TryGetValue(signature, out BetterConsoleIssueState state);
            if (state == null && create)
            {
                state = new BetterConsoleIssueState { signature = signature };
                instance.issueStates.Add(state);
                issueStateCache.Add(signature, state);
                instance.Save(true);
            }

            return state;
        }

        public static void SetIssueState(
            string signature,
            BetterConsoleTriage triage,
            bool? bookmarked = null,
            string note = null)
        {
            BetterConsoleIssueState state = GetIssueState(signature, true);
            state.triage = triage;
            if (bookmarked.HasValue) state.bookmarked = bookmarked.Value;
            if (note != null) state.note = note;
            instance.Save(true);
        }

        public static BetterConsoleSavedView AddSavedView(string name, string query)
        {
            BetterConsoleSavedView view = new BetterConsoleSavedView
            {
                id = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrWhiteSpace(name) ? "VIEW" : name.Trim(),
                query = query ?? string.Empty
            };
            instance.savedViews.Add(view);
            instance.Save(true);
            return view;
        }

        public static void RemoveSavedView(string id)
        {
            instance.savedViews.RemoveAll(view => string.Equals(view.id, id, StringComparison.Ordinal));
            instance.Save(true);
        }

        public static BetterConsoleMuteRule AddMuteRule(string label, string query)
        {
            BetterConsoleMuteRule rule = new BetterConsoleMuteRule
            {
                id = Guid.NewGuid().ToString("N"),
                label = string.IsNullOrWhiteSpace(label) ? "MUTE" : label.Trim(),
                query = query ?? string.Empty,
                enabled = true
            };
            instance.muteRules.Add(rule);
            instance.Save(true);
            return rule;
        }

        public static void RemoveMuteRule(string id)
        {
            instance.muteRules.RemoveAll(rule => string.Equals(rule.id, id, StringComparison.Ordinal));
            instance.Save(true);
        }

        public static bool IsMuted(BetterConsoleEntry entry)
        {
            foreach (BetterConsoleMuteRule rule in instance.muteRules)
            {
                if (rule.enabled && GetQuery(rule.query).Matches(entry))
                {
                    return true;
                }
            }

            BetterConsoleIssueState state = GetIssueState(entry.signature);
            return state != null && state.triage == BetterConsoleTriage.Muted;
        }

        private static BetterConsoleQuery GetQuery(string query)
        {
            string key = query ?? string.Empty;
            if (!queryCache.TryGetValue(key, out BetterConsoleQuery compiled))
            {
                compiled = BetterConsoleQuery.Compile(key);
                queryCache.Add(key, compiled);
            }
            return compiled;
        }

        private static void EnsureIssueStateCache()
        {
            if (issueStateCacheReady) return;
            issueStateCache.Clear();
            foreach (BetterConsoleIssueState state in instance.issueStates)
            {
                if (state != null && !string.IsNullOrEmpty(state.signature))
                {
                    issueStateCache[state.signature] = state;
                }
            }
            issueStateCacheReady = true;
        }

        private static void Set(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            instance.Save(true);
        }
    }
}
