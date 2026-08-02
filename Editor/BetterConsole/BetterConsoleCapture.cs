using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DansToolbox.EditorTools.BetterConsole
{
    [InitializeOnLoad]
    internal static class BetterConsoleCapture
    {
        private static readonly ConcurrentQueue<PendingEntry> pending = new ConcurrentQueue<PendingEntry>();
        private static readonly Dictionary<string, long> structuredFingerprints = new Dictionary<string, long>();
        private static readonly object fingerprintLock = new object();
        private static double nextNativePoll;
        private static bool lastRemoteState;
        private static double lastTestLogAt = double.NegativeInfinity;

        static BetterConsoleCapture()
        {
            Application.logMessageReceivedThreaded -= OnUnityLog;
            Application.logMessageReceivedThreaded += OnUnityLog;
            DansToolbox.BetterConsole.Emitted -= OnStructuredLog;
            DansToolbox.BetterConsole.Emitted += OnStructuredLog;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            BetterConsoleNativeBridge.Changed -= QueueNativePoll;
            BetterConsoleNativeBridge.Changed += QueueNativePoll;
            QueueNativePoll();
            lastRemoteState = EditorApplication.isRemoteConnected;
        }

        public static bool Paused { get; set; }
        public static int PendingCount => pending.Count;

        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (Paused || IsStructuredDuplicate(condition, type)) return;
            pending.Enqueue(new PendingEntry
            {
                utcTicks = DateTime.UtcNow.Ticks,
                type = type,
                message = condition ?? string.Empty,
                stack = stackTrace ?? string.Empty,
                threadId = Environment.CurrentManagedThreadId
            });
        }

        private static void OnStructuredLog(BetterConsoleEvent payload)
        {
            if (Paused || payload == null) return;
            RememberStructured(payload.Message, payload.Type, payload.UtcTicks);
            PendingEntry item = new PendingEntry
            {
                utcTicks = payload.UtcTicks,
                type = payload.Type,
                message = payload.Message,
                stack = payload.StackTrace,
                channel = payload.Channel,
                context = payload.Context,
                threadId = payload.ThreadId,
                structured = true
            };
            foreach (BetterConsoleProperty property in payload.Properties)
            {
                if (string.Equals(property.Name, "$tag", StringComparison.Ordinal))
                {
                    item.tags.Add(property.Value);
                    continue;
                }
                item.properties.Add(new BetterConsolePropertyData
                {
                    name = property.Name,
                    value = property.Value
                });
            }
            pending.Enqueue(item);
        }

        private static void Update()
        {
            UpdateAutomaticSessions();
            int budget = 500;
            while (budget-- > 0 && pending.TryDequeue(out PendingEntry item))
            {
                BetterConsoleCategory inferred = BetterConsoleClassification.Categorize(
                    item.message,
                    item.stack,
                    item.file,
                    BetterConsoleStore.ActiveSession?.kind ?? BetterConsoleSessionKind.Editor);
                if (inferred == BetterConsoleCategory.Test && BetterConsoleStore.ActiveSession?.kind != BetterConsoleSessionKind.Test)
                {
                    BetterConsoleStore.BeginSession(BetterConsoleSessionKind.Test, "TEST");
                }
                if (inferred == BetterConsoleCategory.Test) lastTestLogAt = EditorApplication.timeSinceStartup;
                BetterConsoleSession session = BetterConsoleStore.ActiveSession;
                bool remote = EditorApplication.isRemoteConnected;
                int contextInstanceId = item.context != null
                    ? item.context.GetInstanceID()
                    : item.contextInstanceId;
                string contextName = item.context != null
                    ? item.context.name
                    : item.contextName;
                BetterConsoleEntry entry = new BetterConsoleEntry
                {
                    utcTicks = item.utcTicks,
                    frame = Application.isPlaying ? Time.frameCount : -1,
                    sessionId = session?.id ?? string.Empty,
                    severity = BetterConsoleClassification.Severity(item.type),
                    message = item.message,
                    stackTrace = item.stack,
                    file = item.file,
                    line = item.line,
                    column = item.column,
                    contextInstanceId = contextInstanceId,
                    contextName = contextName,
                    source = remote ? "Remote" : item.source,
                    device = remote ? "PLAYER" : item.device,
                    scene = SceneManager.GetActiveScene().name,
                    channel = item.channel,
                    threadId = item.threadId,
                    remote = remote || item.remote,
                    structured = item.structured,
                    tags = item.tags,
                    properties = item.properties
                };
                BetterConsoleStore.Add(entry);

                if (BetterConsoleSettings.ErrorPause &&
                    EditorApplication.isPlaying &&
                    entry.severity >= BetterConsoleSeverity.Error)
                {
                    EditorApplication.isPaused = true;
                }
            }

            if (BetterConsoleSettings.CaptureNativeHistory && EditorApplication.timeSinceStartup >= nextNativePoll)
            {
                nextNativePoll = double.MaxValue;
                foreach (BetterConsoleEntry native in BetterConsoleNativeBridge.ReadNewEntries())
                {
                    if (!IsStoreDuplicate(native)) BetterConsoleStore.Add(native);
                }
            }

            PruneFingerprints();
        }

        private static void QueueNativePoll()
        {
            nextNativePoll = EditorApplication.timeSinceStartup + 0.05d;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                BetterConsoleStore.BeginSession(BetterConsoleSessionKind.Play, "PLAY");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                BetterConsoleStore.ResumeEditorSession();
            }
        }

        private static void OnCompilationStarted(object context)
        {
            BetterConsoleStore.BeginSession(BetterConsoleSessionKind.Compile, "COMPILE");
        }

        private static void OnCompilationFinished(object context)
        {
            BetterConsoleStore.ResumeEditorSession();
        }

        private static string Fingerprint(string message, LogType type)
        {
            return string.Concat((int)type, "|", message ?? string.Empty);
        }

        private static void RememberStructured(string message, LogType type, long ticks)
        {
            lock (fingerprintLock)
            {
                structuredFingerprints[Fingerprint(message, type)] = ticks;
            }
        }

        private static bool IsStructuredDuplicate(string message, LogType type)
        {
            lock (fingerprintLock)
            {
                return structuredFingerprints.TryGetValue(Fingerprint(message, type), out long ticks) &&
                       DateTime.UtcNow.Ticks - ticks < TimeSpan.FromSeconds(1).Ticks;
            }
        }

        private static void PruneFingerprints()
        {
            long cutoff = DateTime.UtcNow.Ticks - TimeSpan.FromSeconds(2).Ticks;
            lock (fingerprintLock)
            {
                foreach (string key in structuredFingerprints
                             .Where(pair => pair.Value < cutoff)
                             .Select(pair => pair.Key)
                             .ToArray())
                {
                    structuredFingerprints.Remove(key);
                }
            }
        }

        private static bool IsStoreDuplicate(BetterConsoleEntry candidate)
        {
            if (BetterConsoleStore.ContainsNativeLine(candidate.nativeLineIndex)) return true;
            IReadOnlyList<BetterConsoleEntry> entries = BetterConsoleStore.Entries;
            int start = Math.Max(0, entries.Count - 50);
            for (int index = entries.Count - 1; index >= start; index--)
            {
                BetterConsoleEntry existing = entries[index];
                if (Math.Abs(existing.utcTicks - candidate.utcTicks) > TimeSpan.FromSeconds(2).Ticks) continue;
                if (existing.severity == candidate.severity &&
                    string.Equals(existing.message, candidate.message, StringComparison.Ordinal) &&
                    string.Equals(existing.stackTrace, candidate.stackTrace, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void UpdateAutomaticSessions()
        {
            bool remote = EditorApplication.isRemoteConnected;
            if (remote != lastRemoteState)
            {
                lastRemoteState = remote;
                if (remote)
                {
                    BetterConsoleStore.BeginSession(BetterConsoleSessionKind.Remote, "REMOTE", "Player");
                }
                else if (EditorApplication.isPlaying)
                {
                    BetterConsoleStore.BeginSession(BetterConsoleSessionKind.Play, "PLAY");
                }
                else
                {
                    BetterConsoleStore.ResumeEditorSession();
                }
            }

            if (BetterConsoleStore.ActiveSession?.kind == BetterConsoleSessionKind.Test &&
                EditorApplication.timeSinceStartup - lastTestLogAt > 10d)
            {
                if (EditorApplication.isPlaying) BetterConsoleStore.BeginSession(BetterConsoleSessionKind.Play, "PLAY");
                else BetterConsoleStore.ResumeEditorSession();
            }
        }

        private sealed class PendingEntry
        {
            public long utcTicks;
            public LogType type;
            public string message = string.Empty;
            public string stack = string.Empty;
            public UnityEngine.Object context;
            public string file = string.Empty;
            public int line;
            public int column;
            public int contextInstanceId;
            public string contextName = string.Empty;
            public string source = "Editor";
            public string device = string.Empty;
            public string channel = string.Empty;
            public int threadId;
            public bool remote;
            public bool structured;
            public List<string> tags = new List<string>();
            public List<BetterConsolePropertyData> properties = new List<BetterConsolePropertyData>();
        }
    }

    internal sealed class BetterConsoleBuildSessions : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            BetterConsoleStore.BeginSession(
                BetterConsoleSessionKind.Build,
                "BUILD " + report.summary.platform);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            BetterConsoleStore.ResumeEditorSession();
        }
    }
}
