using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterConsole
{
    internal static class BetterConsoleClassification
    {
        private static readonly Regex HexRegex = new Regex(
            @"\b(?:0x)?[0-9a-f]{8,16}\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex GuidRegex = new Regex(
            @"\b[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new Regex(
            @"(?<![A-Za-z_])[-+]?\d+(?:\.\d+)?(?![A-Za-z_])",
            RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new Regex(
            @"\s+",
            RegexOptions.Compiled);

        public static string Signature(BetterConsoleEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            string value = entry.message ?? string.Empty;
            value = GuidRegex.Replace(value, "<guid>");
            value = HexRegex.Replace(value, "<hex>");
            value = NumberRegex.Replace(value, "#");
            value = WhitespaceRegex.Replace(value, " ").Trim().ToLowerInvariant();

            string firstFrame = FirstUsefulFrame(entry.stackTrace);
            return string.Concat(
                ((int)entry.severity).ToString(CultureInfo.InvariantCulture), "|",
                value, "|", firstFrame.ToLowerInvariant());
        }

        public static BetterConsoleCategory Categorize(
            string message,
            string stack,
            string file,
            BetterConsoleSessionKind sessionKind)
        {
            string haystack = string.Concat(message, "\n", stack, "\n", file).ToLowerInvariant();
            if (sessionKind == BetterConsoleSessionKind.Build || ContainsAny(haystack, "buildpipeline", "building player", "buildreport")) return BetterConsoleCategory.Build;
            if (sessionKind == BetterConsoleSessionKind.Test || ContainsAny(haystack, "nunit", "testrunner", "test failed")) return BetterConsoleCategory.Test;
            if (sessionKind == BetterConsoleSessionKind.Compile || ContainsAny(haystack, "compiler error", "cs0", "compilationpipeline", ".asmdef")) return BetterConsoleCategory.Compile;
            if (ContainsAny(haystack, "shader error", "shader compiler", ".shader", ".hlsl")) return BetterConsoleCategory.Shader;
            if (ContainsAny(haystack, "serialize", "deserialize", "serialization", "missing script")) return BetterConsoleCategory.Serialization;
            if (ContainsAny(haystack, "assetdatabase", "importer", "importing asset", "failed to import")) return BetterConsoleCategory.Import;
            if (ContainsAny(haystack, "packagemanager", "packages/manifest", "package cache")) return BetterConsoleCategory.Package;
            if (ContainsAny(haystack, "socket", "http", "network", "transport", "connection")) return BetterConsoleCategory.Network;
            if (ContainsAny(haystack, "profiler", "allocation", "performance", "framerate", "fps")) return BetterConsoleCategory.Performance;
            if (ContainsAny(haystack, "unityeditor.", "/editor/", " editor ")) return BetterConsoleCategory.Editor;
            if (sessionKind == BetterConsoleSessionKind.Play || sessionKind == BetterConsoleSessionKind.Remote) return BetterConsoleCategory.Runtime;
            return BetterConsoleCategory.General;
        }

        public static BetterConsoleSeverity Severity(LogType type)
        {
            switch (type)
            {
                case LogType.Warning: return BetterConsoleSeverity.Warning;
                case LogType.Error: return BetterConsoleSeverity.Error;
                case LogType.Exception: return BetterConsoleSeverity.Exception;
                case LogType.Assert: return BetterConsoleSeverity.Assert;
                default: return BetterConsoleSeverity.Log;
            }
        }

        public static string FirstUsefulFrame(string stack)
        {
            if (string.IsNullOrWhiteSpace(stack))
            {
                return string.Empty;
            }

            string[] lines = stack.Replace("\r", string.Empty).Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 ||
                    line.StartsWith("UnityEngine.Debug", StringComparison.Ordinal) ||
                    line.IndexOf("DansToolbox.BetterConsole.Emit", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("DansToolbox.BetterConsole.Log", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("DansToolbox.BetterConsole.Warning", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("DansToolbox.BetterConsole.Error", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("DansToolbox.BetterConsole.Exception", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                return line;
            }

            return lines[0].Trim();
        }

        public static void ParseFileLocation(BetterConsoleEntry entry)
        {
            if (entry == null || !string.IsNullOrEmpty(entry.file) || string.IsNullOrEmpty(entry.stackTrace))
            {
                return;
            }

            Match match = Regex.Match(entry.stackTrace, @"\(at (?<file>.*?):(?<line>\d+)(?::(?<column>\d+))?\)");
            if (!match.Success)
            {
                match = Regex.Match(entry.stackTrace, @" in (?<file>.*?):line (?<line>\d+)");
            }

            if (!match.Success)
            {
                return;
            }

            entry.file = match.Groups["file"].Value.Replace('\\', '/');
            int.TryParse(match.Groups["line"].Value, out entry.line);
            int.TryParse(match.Groups["column"].Value, out entry.column);
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            foreach (string needle in needles)
            {
                if (value.IndexOf(needle, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
