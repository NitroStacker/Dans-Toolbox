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
        private static readonly MethodInfo getMethod;
        private static readonly MethodInfo clearMethod;
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
            getMethod = entriesType?.GetMethod("GetEntryInternal", flags);
            clearMethod = entriesType?.GetMethod("Clear", flags);
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
        public static bool Available => !failed && entriesType != null && entryType != null && getMethod != null;

        public static IReadOnlyList<BetterConsoleEntry> ReadNewEntries()
        {
            List<BetterConsoleEntry> results = new List<BetterConsoleEntry>();
            if (!Available) return results;

            try
            {
                startMethod?.Invoke(null, null);
                int count = Convert.ToInt32(countMethod.Invoke(null, null));
                if (count < cursor) cursor = 0;
                for (int index = cursor; index < count; index++)
                {
                    object nativeEntry = Activator.CreateInstance(entryType);
                    object read = getMethod.Invoke(null, new[] { (object)index, nativeEntry });
                    if (read is bool success && !success) continue;
                    BetterConsoleEntry entry = ConvertEntry(nativeEntry);
                    if (entry != null) results.Add(entry);
                }
                cursor = count;
            }
            catch
            {
                failed = true;
            }
            finally
            {
                try { endMethod?.Invoke(null, null); }
                catch { /* optional bridge */ }
            }

            return results;
        }

        public static void ClearNative()
        {
            if (!Available) return;
            try
            {
                clearMethod?.Invoke(null, null);
                cursor = 0;
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

        private static void OnChanged()
        {
            Changed?.Invoke();
        }
    }
}
