using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DansToolbox.EditorTools.BetterProject
{
    internal sealed class BetterProjectQuery
    {
        private readonly List<Term> terms = new List<Term>();

        private BetterProjectQuery(string value)
        {
            Raw = value ?? string.Empty;
            foreach (string token in Tokenize(Raw))
            {
                bool exclude = token.StartsWith("-", StringComparison.Ordinal) && token.Length > 1;
                string body = exclude ? token.Substring(1) : token;
                int colon = body.IndexOf(':');
                string key = colon > 0 ? body.Substring(0, colon).ToLowerInvariant() : string.Empty;
                string valuePart = colon > 0 ? body.Substring(colon + 1) : body;
                if (!string.IsNullOrWhiteSpace(valuePart))
                {
                    terms.Add(new Term(key, valuePart, exclude));
                }
            }
            RequiresDiagnostics = terms.Any(term =>
                term.Key == "is" && term.Value.Equals("problem", StringComparison.OrdinalIgnoreCase));
            RequiresFavorites = terms.Any(term =>
                term.Key == "is" && term.Value.Equals("favorite", StringComparison.OrdinalIgnoreCase));
            RequiresLabels = terms.Any(term => term.Key == "l" || term.Key == "label");
        }

        internal string Raw { get; }
        internal bool IsEmpty => terms.Count == 0;
        internal bool RequiresDiagnostics { get; }
        internal bool RequiresFavorites { get; }
        internal bool RequiresLabels { get; }

        internal static BetterProjectQuery Parse(string value)
        {
            return new BetterProjectQuery(value);
        }

        internal bool Matches(
            BetterProjectAssetRecord asset,
            BetterProjectDiagnosticFlags diagnostics,
            bool favorite,
            IReadOnlyList<string> labels)
        {
            if (asset == null)
            {
                return false;
            }

            foreach (Term term in terms)
            {
                bool match = Match(term, asset, diagnostics, favorite, labels);
                if (term.Exclude ? match : !match)
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool FuzzyContains(string source, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            int queryIndex = 0;
            for (int index = 0; index < source.Length && queryIndex < query.Length; index++)
            {
                if (char.ToUpperInvariant(source[index]) == char.ToUpperInvariant(query[queryIndex]))
                {
                    queryIndex++;
                }
            }
            return queryIndex == query.Length;
        }

        internal static IReadOnlyList<string> Tokenize(string value)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }

            var builder = new StringBuilder();
            bool quoted = false;
            foreach (char character in value)
            {
                if (character == '"')
                {
                    quoted = !quoted;
                    continue;
                }
                if (char.IsWhiteSpace(character) && !quoted)
                {
                    if (builder.Length > 0)
                    {
                        result.Add(builder.ToString());
                        builder.Length = 0;
                    }
                    continue;
                }
                builder.Append(character);
            }
            if (builder.Length > 0)
            {
                result.Add(builder.ToString());
            }
            return result;
        }

        private static bool Match(
            Term term,
            BetterProjectAssetRecord asset,
            BetterProjectDiagnosticFlags diagnostics,
            bool favorite,
            IReadOnlyList<string> labels)
        {
            string value = term.Value;
            switch (term.Key)
            {
                case "t":
                case "type":
                    return Contains(asset.TypeName, value);
                case "ext":
                    return string.Equals(
                        asset.Extension.TrimStart('.'),
                        value.TrimStart('.'),
                        StringComparison.OrdinalIgnoreCase);
                case "path":
                    return Contains(asset.Path, value);
                case "l":
                case "label":
                    return labels != null && labels.Any(label => Contains(label, value));
                case "is":
                    return MatchState(value, asset, diagnostics, favorite);
                case "size":
                    return MatchSize(asset.FileSize, value);
                case "modified":
                    return MatchAge(asset.ModifiedUtc, value);
                case "ref":
                    return value.Equals("any", StringComparison.OrdinalIgnoreCase)
                        ? asset.ReferenceCount > 0
                        : int.TryParse(value, out int minimum) && asset.ReferenceCount >= minimum;
                default:
                    return FuzzyContains(asset.Name, value) ||
                           Contains(asset.Path, value) ||
                           Contains(asset.TypeName, value);
            }
        }

        private static bool MatchState(
            string value,
            BetterProjectAssetRecord asset,
            BetterProjectDiagnosticFlags diagnostics,
            bool favorite)
        {
            switch (value.ToLowerInvariant())
            {
                case "folder": return asset.IsFolder;
                case "asset": return !asset.IsFolder;
                case "package": return asset.IsPackage;
                case "readonly": return asset.IsReadOnly;
                case "favorite": return favorite;
                case "problem": return diagnostics != BetterProjectDiagnosticFlags.None;
                case "unused": return !asset.IsFolder && asset.ReferenceCount == 0;
                default: return false;
            }
        }

        private static bool MatchSize(long bytes, string value)
        {
            if (bytes < 0 || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            string normalized = value.Trim().ToLowerInvariant();
            bool greater = normalized.StartsWith(">", StringComparison.Ordinal);
            bool less = normalized.StartsWith("<", StringComparison.Ordinal);
            if (greater || less)
            {
                normalized = normalized.Substring(1);
            }
            long multiplier = normalized.EndsWith("gb", StringComparison.Ordinal) ? 1024L * 1024L * 1024L :
                normalized.EndsWith("mb", StringComparison.Ordinal) ? 1024L * 1024L :
                normalized.EndsWith("kb", StringComparison.Ordinal) ? 1024L : 1L;
            normalized = normalized.TrimEnd('g', 'm', 'k', 'b');
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount))
            {
                return false;
            }
            long expected = (long)(amount * multiplier);
            return greater ? bytes > expected : less ? bytes < expected : bytes == expected;
        }

        private static bool MatchAge(DateTime modifiedUtc, string value)
        {
            if (modifiedUtc == default || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            string normalized = value.Trim().ToLowerInvariant();
            bool older = normalized.StartsWith(">", StringComparison.Ordinal);
            bool newer = normalized.StartsWith("<", StringComparison.Ordinal);
            if (older || newer)
            {
                normalized = normalized.Substring(1);
            }
            double multiplier = normalized.EndsWith("h", StringComparison.Ordinal) ? 1d / 24d :
                normalized.EndsWith("w", StringComparison.Ordinal) ? 7d : 1d;
            normalized = normalized.TrimEnd('h', 'd', 'w');
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount))
            {
                return false;
            }
            double ageDays = (DateTime.UtcNow - modifiedUtc).TotalDays;
            double expected = amount * multiplier;
            return older ? ageDays > expected : newer ? ageDays < expected : ageDays <= expected;
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private readonly struct Term
        {
            internal Term(string key, string value, bool exclude)
            {
                Key = key;
                Value = value;
                Exclude = exclude;
            }

            internal string Key { get; }
            internal string Value { get; }
            internal bool Exclude { get; }
        }
    }
}
