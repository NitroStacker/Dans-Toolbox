using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterConsole
{
    internal static class BetterConsoleExporter
    {
        [Serializable]
        private sealed class ExportData
        {
            public string generatedUtc;
            public List<BetterConsoleEntry> entries;
        }

        public static void WriteJson(string path, List<BetterConsoleEntry> entries)
        {
            ExportData export = new ExportData
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                entries = entries ?? new List<BetterConsoleEntry>()
            };
            File.WriteAllText(path, JsonUtility.ToJson(export, true));
        }

        public static void WriteMarkdown(string path, List<BetterConsoleEntry> entries)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("# Better Console Export").AppendLine();
            foreach (BetterConsoleEntry entry in entries ?? new List<BetterConsoleEntry>())
            {
                text.Append("## ").Append(entry.severity).Append(" · ")
                    .AppendLine(entry.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                text.AppendLine().AppendLine(entry.message ?? string.Empty);
                if (!string.IsNullOrEmpty(entry.file))
                {
                    text.AppendLine().Append('`').Append(entry.file).Append(':').Append(entry.line).AppendLine("`");
                }
                if (!string.IsNullOrEmpty(entry.stackTrace))
                {
                    text.AppendLine().AppendLine("```text").AppendLine(entry.stackTrace).AppendLine("```");
                }
                text.AppendLine();
            }
            File.WriteAllText(path, text.ToString());
        }

        public static string FixPrompt(BetterConsoleEntry entry)
        {
            if (entry == null) return string.Empty;
            return string.Concat(
                "Diagnose this Unity issue using only the evidence below. Explain the likely cause, identify the first user-code frame, and propose the smallest safe fix.\n\n",
                "Severity: ", entry.severity, "\nCategory: ", entry.category,
                "\nMessage:\n", entry.message,
                "\n\nStack:\n", entry.stackTrace,
                string.IsNullOrEmpty(entry.file) ? string.Empty : $"\n\nSource: {entry.file}:{entry.line}");
        }
    }
}
