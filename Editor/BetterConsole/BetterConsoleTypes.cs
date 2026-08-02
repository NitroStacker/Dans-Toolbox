using System;
using System.Collections.Generic;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterConsole
{
    internal enum BetterConsoleSurface { Live, Issues, Sessions }
    internal enum BetterConsoleDetailPlacement { Hidden, Bottom, Right }
    internal enum BetterConsoleSeverity { Log, Warning, Error, Exception, Assert }
    internal enum BetterConsoleTriage { New, Seen, Acknowledged, Muted, Resolved }
    internal enum BetterConsoleSessionKind { Editor, Compile, Play, Build, Test, Remote }
    internal enum BetterConsoleCategory
    {
        General,
        Compile,
        Runtime,
        Import,
        Serialization,
        Shader,
        Package,
        Build,
        Test,
        Editor,
        Network,
        Performance
    }

    [Serializable]
    internal sealed class BetterConsolePropertyData
    {
        public string name = string.Empty;
        public string value = string.Empty;
    }

    [Serializable]
    internal sealed class BetterConsoleEntry
    {
        public long id;
        public long utcTicks;
        public int frame;
        public string sessionId = string.Empty;
        public string sessionLabel = string.Empty;
        public BetterConsoleSessionKind sessionKind;
        public BetterConsoleSeverity severity;
        public BetterConsoleCategory category;
        public string message = string.Empty;
        public string stackTrace = string.Empty;
        public string file = string.Empty;
        public int line;
        public int column;
        public int contextInstanceId;
        public string contextName = string.Empty;
        public string source = "Editor";
        public string device = string.Empty;
        public string scene = string.Empty;
        public string channel = string.Empty;
        public string signature = string.Empty;
        public int threadId;
        public int nativeLineIndex;
        public bool remote;
        public bool structured;
        public List<string> tags = new List<string>();
        public List<BetterConsolePropertyData> properties = new List<BetterConsolePropertyData>();

        public DateTime TimestampUtc => utcTicks > 0
            ? new DateTime(utcTicks, DateTimeKind.Utc)
            : DateTime.MinValue;

        public bool HasStack => !string.IsNullOrEmpty(stackTrace);
    }

    [Serializable]
    internal sealed class BetterConsoleSession
    {
        public string id = string.Empty;
        public BetterConsoleSessionKind kind;
        public string label = string.Empty;
        public string source = "Editor";
        public long startUtcTicks;
        public long endUtcTicks;
        public int logs;
        public int warnings;
        public int errors;
        public bool active;

        public DateTime StartUtc => new DateTime(startUtcTicks, DateTimeKind.Utc);
        public DateTime EndUtc => endUtcTicks > 0
            ? new DateTime(endUtcTicks, DateTimeKind.Utc)
            : DateTime.UtcNow;
    }

    internal sealed class BetterConsoleIssue
    {
        public string signature;
        public BetterConsoleEntry representative;
        public int count;
        public long firstUtcTicks;
        public long lastUtcTicks;
        public int sessionCount;
        public float perMinute;
        public BetterConsoleTriage triage;
        public bool bookmarked;
        public string note;
    }

    [Serializable]
    internal sealed class BetterConsoleSavedView
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public string query = string.Empty;
    }

    [Serializable]
    internal sealed class BetterConsoleIssueState
    {
        public string signature = string.Empty;
        public BetterConsoleTriage triage;
        public bool bookmarked;
        public string note = string.Empty;
    }

    [Serializable]
    internal sealed class BetterConsoleMuteRule
    {
        public string id = string.Empty;
        public string label = string.Empty;
        public string query = string.Empty;
        public bool enabled = true;
    }

    [Serializable]
    internal sealed class BetterConsoleHistoryData
    {
        public long nextEntryId = 1;
        public List<BetterConsoleEntry> entries = new List<BetterConsoleEntry>();
        public List<BetterConsoleSession> sessions = new List<BetterConsoleSession>();
    }
}
