using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DansToolbox
{
    /// <summary>
    /// Optional structured logging for Better Console. Messages still flow to
    /// Unity's logger, so builds and the native Console keep working normally.
    /// </summary>
    public static class BetterConsole
    {
        public static event Action<BetterConsoleEvent> Emitted;

        public static void Log(
            string message,
            string channel = null,
            UnityEngine.Object context = null,
            params BetterConsoleProperty[] properties)
        {
            Emit(LogType.Log, message, channel, context, properties);
        }

        public static void Warning(
            string message,
            string channel = null,
            UnityEngine.Object context = null,
            params BetterConsoleProperty[] properties)
        {
            Emit(LogType.Warning, message, channel, context, properties);
        }

        public static void Error(
            string message,
            string channel = null,
            UnityEngine.Object context = null,
            params BetterConsoleProperty[] properties)
        {
            Emit(LogType.Error, message, channel, context, properties);
        }

        public static void Exception(
            Exception exception,
            string channel = null,
            UnityEngine.Object context = null,
            params BetterConsoleProperty[] properties)
        {
            Emit(
                LogType.Exception,
                exception?.ToString() ?? "Unknown exception",
                channel,
                context,
                properties);
        }

        public static BetterConsoleProperty Property(string name, object value)
        {
            return new BetterConsoleProperty(name, value?.ToString() ?? "null");
        }

        public static BetterConsoleProperty Tag(string value)
        {
            return new BetterConsoleProperty("$tag", value ?? string.Empty);
        }

        private static void Emit(
            LogType type,
            string message,
            string channel,
            UnityEngine.Object context,
            IReadOnlyList<BetterConsoleProperty> properties)
        {
            string safeMessage = message ?? string.Empty;
            BetterConsoleEvent payload = new BetterConsoleEvent(
                DateTime.UtcNow.Ticks,
                Thread.CurrentThread.ManagedThreadId,
                type,
                safeMessage,
                StackTraceUtility.ExtractStackTrace(),
                channel ?? string.Empty,
                context,
                properties);

            try
            {
                Emitted?.Invoke(payload);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            Debug.unityLogger.Log(type, (object)safeMessage, context);
        }
    }

    [Serializable]
    public readonly struct BetterConsoleProperty
    {
        public BetterConsoleProperty(string name, string value)
        {
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Name { get; }
        public string Value { get; }
    }

    public sealed class BetterConsoleEvent
    {
        internal BetterConsoleEvent(
            long utcTicks,
            int threadId,
            LogType type,
            string message,
            string stackTrace,
            string channel,
            UnityEngine.Object context,
            IReadOnlyList<BetterConsoleProperty> properties)
        {
            UtcTicks = utcTicks;
            ThreadId = threadId;
            Type = type;
            Message = message;
            StackTrace = stackTrace;
            Channel = channel;
            Context = context;
            Properties = properties ?? Array.Empty<BetterConsoleProperty>();
        }

        public long UtcTicks { get; }
        public int ThreadId { get; }
        public LogType Type { get; }
        public string Message { get; }
        public string StackTrace { get; }
        public string Channel { get; }
        public UnityEngine.Object Context { get; }
        public IReadOnlyList<BetterConsoleProperty> Properties { get; }
    }
}
