using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterConsole
{
    /// <summary>
    /// Isolates Unity's internal Console history API. All essential capture uses
    /// public callbacks; this bridge adds compile/import history when available.
    /// </summary>
    [InitializeOnLoad]
    internal static class BetterConsoleNativeBridge
    {
        private static readonly Type entriesType;
        private static readonly Type entryType;
        private static readonly MethodInfo startMethod;
        private static readonly MethodInfo endMethod;
        private static readonly MethodInfo countMethod;
        private static readonly MethodInfo countsByTypeMethod;
        private static readonly MethodInfo getMethod;
        private static readonly MethodInfo clearMethod;
        private static readonly MethodInfo getConsoleFlagsMethod;
        private static readonly MethodInfo setConsoleFlagsMethod;
        private static readonly MethodInfo getFilteringTextMethod;
        private static readonly MethodInfo setFilteringTextMethod;
        private static readonly FieldInfo messageField;
        private static readonly FieldInfo fileField;
        private static readonly FieldInfo lineField;
        private static readonly FieldInfo columnField;
        private static readonly FieldInfo modeField;
        private static readonly FieldInfo entityIdField;
        private static readonly FieldInfo callstackStartField;
        private static readonly FieldInfo globalLineIndexField;
        private static readonly MethodInfo entityIdToObjectMethod;
        private static readonly MethodInfo instanceIdToObjectMethod;
        private static int cursor;
        private static int totalCursor;
        private static bool initialized;
        private static bool failed;

        static BetterConsoleNativeBridge()
        {
            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            entriesType = editorAssembly.GetType("UnityEditor.LogEntries");
            entryType = editorAssembly.GetType("UnityEditor.LogEntry");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            startMethod = entriesType?.GetMethod("StartGettingEntries", flags);
            endMethod = entriesType?.GetMethod("EndGettingEntries", flags);
            countMethod = entriesType?.GetMethod("GetCount", flags);
            countsByTypeMethod = entriesType?.GetMethod("GetCountsByType", flags);
            getMethod = entriesType?.GetMethod("GetEntryInternal", flags);
            clearMethod = entriesType?.GetMethod("Clear", flags);
            getConsoleFlagsMethod = entriesType?.GetMethod("get_consoleFlags", flags);
            setConsoleFlagsMethod = entriesType?.GetMethod("set_consoleFlags", flags);
            getFilteringTextMethod = entriesType?.GetMethod("GetFilteringText", flags);
            setFilteringTextMethod = entriesType?.GetMethod("SetFilteringText", flags);
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            messageField = entryType?.GetField("message", fields);
            fileField = entryType?.GetField("file", fields);
            lineField = entryType?.GetField("line", fields);
            columnField = entryType?.GetField("column", fields);
            modeField = entryType?.GetField("mode", fields);
            entityIdField = entryType?.GetField("entityId", fields);
            callstackStartField = entryType?.GetField("callstackTextStartUTF16", fields);
            globalLineIndexField = entryType?.GetField("globalLineIndex", fields);
            entityIdToObjectMethod = typeof(EditorUtility).GetMethod(
                "EntityIdToObject",
                BindingFlags.Static | BindingFlags.Public,
                null,
                entityIdField == null ? Type.EmptyTypes : new[] { entityIdField.FieldType },
                null);
            instanceIdToObjectMethod = typeof(EditorUtility).GetMethod(
                "InstanceIDToObject",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(int) },
                null);
            ConsoleWindowUtility.consoleLogsChanged -= OnChanged;
            ConsoleWindowUtility.consoleLogsChanged += OnChanged;
        }

        public static event Action Changed;
        public static bool Available => !failed && entriesType != null && entryType != null &&
                                        countMethod != null && countsByTypeMethod != null && getMethod != null;
        public static bool ClearOnPlay => HasConsoleFlag(2);
        public static bool ClearOnBuild => HasConsoleFlag(2048);
        public static bool ClearOnRecompile => HasConsoleFlag(4096);

        public static BetterConsoleNativeRead ReadChanges()
        {
            List<BetterConsoleEntry> results = new List<BetterConsoleEntry>();
            if (!Available) return new BetterConsoleNativeRead(results, false);

            bool reconcile = false;
            bool reading = false;
            int originalFlags = 0;
            string originalFilter = string.Empty;
            bool restoreConsoleState = false;
            try
            {
                int totalCount = ReadTotalCount();
                reconcile = RequiresReconciliation(initialized, totalCursor, totalCount);
                bool changed = !initialized || totalCount != totalCursor;
                if (changed && totalCount > 0)
                {
                    restoreConsoleState = TryShowAllEntries(out originalFlags, out originalFilter);
                    startMethod?.Invoke(null, null);
                    reading = true;
                    int count = Convert.ToInt32(countMethod.Invoke(null, null));
                    int first = reconcile || count < cursor ? 0 : cursor;
                    for (int index = first; index < count; index++)
                    {
                        object nativeEntry = Activator.CreateInstance(entryType);
                        object read = getMethod.Invoke(null, new[] { (object)index, nativeEntry });
                        if (read is bool success && !success) continue;
                        BetterConsoleEntry entry = ConvertEntry(nativeEntry);
                        if (entry != null) results.Add(entry);
                    }
                    cursor = count;
                }
                else if (totalCount == 0)
                {
                    cursor = 0;
                }
                totalCursor = totalCount;
                initialized = true;
            }
            catch
            {
                failed = true;
            }
            finally
            {
                try { if (reading) endMethod?.Invoke(null, null); }
                catch { /* optional bridge */ }
                if (restoreConsoleState) RestoreConsoleState(originalFlags, originalFilter);
            }

            return new BetterConsoleNativeRead(results, reconcile && !failed);
        }

        public static void ClearNative()
        {
            if (!Available) return;
            try
            {
                clearMethod?.Invoke(null, null);
                cursor = 0;
                totalCursor = 0;
                initialized = true;
            }
            catch
            {
                failed = true;
            }
        }

        public static UnityEngine.Object ResolveContext(int instanceId)
        {
            if (instanceId == 0 || instanceIdToObjectMethod == null) return null;
            try { return instanceIdToObjectMethod.Invoke(null, new object[] { instanceId }) as UnityEngine.Object; }
            catch { return null; }
        }

        private static BetterConsoleEntry ConvertEntry(object native)
        {
            string combined = messageField?.GetValue(native) as string ?? string.Empty;
            int split = GetInt(callstackStartField, native);
            string message = combined;
            string stack = string.Empty;
            if (split > 0 && split <= combined.Length)
            {
                message = combined.Substring(0, split).TrimEnd('\r', '\n');
                stack = combined.Substring(split).TrimStart('\r', '\n');
            }

            int mode = GetInt(modeField, native);
            UnityEngine.Object context = null;
            if (entityIdField != null && entityIdToObjectMethod != null)
            {
                object entityId = entityIdField.GetValue(native);
                context = entityIdToObjectMethod.Invoke(null, new[] { entityId }) as UnityEngine.Object;
            }
            int contextId = context != null ? context.GetInstanceID() : 0;
            return new BetterConsoleEntry
            {
                utcTicks = DateTime.UtcNow.Ticks,
                severity = SeverityFromMode(mode),
                message = message,
                stackTrace = stack,
                file = fileField?.GetValue(native) as string ?? string.Empty,
                line = GetInt(lineField, native),
                column = GetInt(columnField, native),
                contextInstanceId = contextId,
                contextName = context != null ? context.name : string.Empty,
                source = "Editor",
                nativeLineIndex = GetInt(globalLineIndexField, native)
            };
        }

        private static BetterConsoleSeverity SeverityFromMode(int mode)
        {
            const int assertMask = (1 << 1) | (1 << 21);
            const int exceptionMask = 1 << 17;
            const int errorMask = (1 << 0) | (1 << 4) | (1 << 6) | (1 << 8) | (1 << 11);
            const int warningMask = (1 << 7) | (1 << 9) | (1 << 12);
            if ((mode & assertMask) != 0) return BetterConsoleSeverity.Assert;
            if ((mode & exceptionMask) != 0) return BetterConsoleSeverity.Exception;
            if ((mode & errorMask) != 0) return BetterConsoleSeverity.Error;
            if ((mode & warningMask) != 0) return BetterConsoleSeverity.Warning;
            return BetterConsoleSeverity.Log;
        }

        private static int GetInt(FieldInfo field, object target)
        {
            if (field == null) return 0;
            object value = field.GetValue(target);
            return value == null ? 0 : Convert.ToInt32(value);
        }

        private static int ReadTotalCount()
        {
            object[] counts = { 0, 0, 0 };
            countsByTypeMethod.Invoke(null, counts);
            return Convert.ToInt32(counts[0]) + Convert.ToInt32(counts[1]) + Convert.ToInt32(counts[2]);
        }

        private static bool HasConsoleFlag(int flag)
        {
            if (getConsoleFlagsMethod == null) return false;
            try { return (Convert.ToInt32(getConsoleFlagsMethod.Invoke(null, null)) & flag) != 0; }
            catch { return false; }
        }

        private static bool TryShowAllEntries(out int originalFlags, out string originalFilter)
        {
            originalFlags = 0;
            originalFilter = string.Empty;
            if (getConsoleFlagsMethod == null || setConsoleFlagsMethod == null ||
                getFilteringTextMethod == null || setFilteringTextMethod == null)
            {
                return false;
            }

            originalFlags = Convert.ToInt32(getConsoleFlagsMethod.Invoke(null, null));
            originalFilter = getFilteringTextMethod.Invoke(null, null) as string ?? string.Empty;
            const int collapse = 1;
            const int allSeverities = 128 | 256 | 512;
            setConsoleFlagsMethod.Invoke(null, new object[] { (originalFlags | allSeverities) & ~collapse });
            if (!string.IsNullOrEmpty(originalFilter)) setFilteringTextMethod.Invoke(null, new object[] { string.Empty });
            return true;
        }

        private static void RestoreConsoleState(int flags, string filter)
        {
            try
            {
                if (!string.IsNullOrEmpty(filter)) setFilteringTextMethod?.Invoke(null, new object[] { filter });
                setConsoleFlagsMethod?.Invoke(null, new object[] { flags });
            }
            catch
            {
                // The bridge is optional; never let restoration interrupt the Editor.
            }
        }

        internal static bool RequiresReconciliation(bool hasSnapshot, int previousCount, int currentCount)
        {
            return !hasSnapshot || currentCount < previousCount;
        }

        private static void OnChanged()
        {
            Changed?.Invoke();
        }
    }

    internal readonly struct BetterConsoleNativeRead
    {
        public BetterConsoleNativeRead(IReadOnlyList<BetterConsoleEntry> entries, bool reconcile)
        {
            Entries = entries ?? Array.Empty<BetterConsoleEntry>();
            Reconcile = reconcile;
        }

        public IReadOnlyList<BetterConsoleEntry> Entries { get; }
        public bool Reconcile { get; }
    }
}
