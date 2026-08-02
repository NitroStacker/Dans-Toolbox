using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DansToolbox.EditorTools.BetterConsole
{
    internal sealed class BetterConsoleQuery
    {
        private readonly List<Term> terms = new List<Term>();

        private BetterConsoleQuery(string raw)
        {
            Raw = raw ?? string.Empty;
            Parse();
        }

        public string Raw { get; }
        public string Error { get; private set; } = string.Empty;
        public bool IsValid => string.IsNullOrEmpty(Error);
        public bool IsEmpty => terms.Count == 0;

        public static BetterConsoleQuery Compile(string raw)
        {
            return new BetterConsoleQuery(raw);
        }

        public bool Matches(BetterConsoleEntry entry, BetterConsoleIssueState state = null)
        {
            if (entry == null || !IsValid)
            {
                return false;
            }

            foreach (Term term in terms)
            {
                bool match = MatchTerm(term, entry, state);
                if (term.negative ? match : !match)
                {
                    return false;
                }
            }

            return true;
        }

        public bool Matches(BetterConsoleSession session)
        {
            if (session == null || !IsValid)
            {
                return false;
            }

            string text = string.Concat(session.label, " ", session.source, " ", session.kind);
            foreach (Term term in terms)
            {
                bool match;
                switch (term.field)
                {
                    case "session":
                    case "type":
                        match = Contains(session.kind.ToString(), term.value) || Contains(session.label, term.value);
                        break;
                    case "source":
                        match = Contains(session.source, term.value);
                        break;
                    case "after":
                        match = TryDate(term.value, out DateTime after) && session.StartUtc >= after;
                        break;
                    case "before":
                        match = TryDate(term.value, out DateTime before) && session.StartUtc <= before;
                        break;
                    default:
                        match = Contains(text, term.value);
                        break;
                }

                if (term.negative ? match : !match)
                {
                    return false;
                }
            }

            return true;
        }

        private void Parse()
        {
            foreach (string rawToken in Tokenize(Raw))
            {
                string token = rawToken;
                bool negative = token.StartsWith("-", StringComparison.Ordinal) && token.Length > 1;
                if (negative)
                {
                    token = token.Substring(1);
                }

                int separator = token.IndexOf(':');
                string field = separator > 0 ? token.Substring(0, separator).ToLowerInvariant() : string.Empty;
                string value = separator > 0 ? token.Substring(separator + 1) : token;
                value = Unquote(value);
                if (value.Length == 0)
                {
                    Error = "Missing query value";
                    return;
                }

                Regex regex = null;
                if (value.Length >= 2 && value[0] == '/' && value[value.Length - 1] == '/')
                {
                    try
                    {
                        regex = new Regex(
                            value.Substring(1, value.Length - 2),
                            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                            TimeSpan.FromMilliseconds(40));
                    }
                    catch (ArgumentException)
                    {
                        Error = "Invalid regex";
                        return;
                    }
                }

                terms.Add(new Term(field, value, negative, regex));
            }
        }

        private static bool MatchTerm(Term term, BetterConsoleEntry entry, BetterConsoleIssueState state)
        {
            string value;
            switch (term.field)
            {
                case "sev":
                case "severity":
                    value = entry.severity.ToString();
                    break;
                case "type":
                case "cat":
                case "category":
                    value = entry.category.ToString();
                    break;
                case "source":
                case "device":
                    value = string.Concat(entry.source, " ", entry.device);
                    break;
                case "file":
                    value = entry.file;
                    break;
                case "context":
                    value = entry.contextName;
                    break;
                case "ctxid":
                    return MatchContextIds(term.value, entry.contextInstanceId);
                case "target":
                    return MatchTarget(term.value, entry);
                case "scene":
                    value = entry.scene;
                    break;
                case "session":
                    value = string.Concat(entry.sessionId, " ", entry.sessionLabel, " ", entry.sessionKind);
                    break;
                case "channel":
                    value = entry.channel;
                    break;
                case "tag":
                    value = string.Join(" ", entry.tags);
                    break;
                case "has":
                    return MatchHas(term.value, entry);
                case "is":
                    return MatchState(term.value, entry, state);
                case "after":
                    return TryDate(term.value, out DateTime after) && entry.TimestampUtc >= after;
                case "before":
                    return TryDate(term.value, out DateTime before) && entry.TimestampUtc <= before;
                default:
                    value = SearchText(entry);
                    break;
            }

            if (term.regex != null)
            {
                try
                {
                    return term.regex.IsMatch(value ?? string.Empty);
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }
            }

            return Contains(value, term.value);
        }

        private static bool MatchHas(string value, BetterConsoleEntry entry)
        {
            switch (value.ToLowerInvariant())
            {
                case "stack": return entry.HasStack;
                case "file": return !string.IsNullOrEmpty(entry.file);
                case "context": return entry.contextInstanceId != 0;
                case "properties": return entry.properties.Count > 0;
                case "channel": return !string.IsNullOrEmpty(entry.channel);
                default: return false;
            }
        }

        private static bool MatchContextIds(string value, int contextInstanceId)
        {
            foreach (string candidate in (value ?? string.Empty).Split('|'))
            {
                if (int.TryParse(candidate, out int parsed) && parsed == contextInstanceId) return true;
            }
            return false;
        }

        private static bool MatchTarget(string value, BetterConsoleEntry entry)
        {
            string entryFile = BetterConsoleDiagnosticBridge.NormalizeAssetPath(entry.file);
            foreach (string selector in (value ?? string.Empty).Split('|'))
            {
                if (selector.StartsWith("id=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(selector.Substring(3), out int id) &&
                    id == entry.contextInstanceId)
                {
                    return true;
                }

                if (!selector.StartsWith("file=", StringComparison.OrdinalIgnoreCase)) continue;
                string file = BetterConsoleDiagnosticBridge.NormalizeAssetPath(selector.Substring(5));
                if (file.EndsWith("/", StringComparison.Ordinal)
                    ? entryFile.StartsWith(file, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(entryFile, file, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool MatchState(string value, BetterConsoleEntry entry, BetterConsoleIssueState state)
        {
            switch (value.ToLowerInvariant())
            {
                case "remote": return entry.remote;
                case "structured": return entry.structured;
                case "bookmarked": return state != null && state.bookmarked;
                case "muted": return state != null && state.triage == BetterConsoleTriage.Muted;
                case "new": return state == null || state.triage == BetterConsoleTriage.New;
                case "seen": return state != null && state.triage == BetterConsoleTriage.Seen;
                case "ack":
                case "acknowledged": return state != null && state.triage == BetterConsoleTriage.Acknowledged;
                case "resolved": return state != null && state.triage == BetterConsoleTriage.Resolved;
                default: return false;
            }
        }

        private static string SearchText(BetterConsoleEntry entry)
        {
            StringBuilder builder = new StringBuilder(256);
            builder.Append(entry.message).Append(' ')
                .Append(entry.stackTrace).Append(' ')
                .Append(entry.file).Append(' ')
                .Append(entry.contextName).Append(' ')
                .Append(entry.source).Append(' ')
                .Append(entry.device).Append(' ')
                .Append(entry.scene).Append(' ')
                .Append(entry.channel).Append(' ')
                .Append(entry.category).Append(' ')
                .Append(entry.severity);
            foreach (string tag in entry.tags)
            {
                builder.Append(' ').Append(tag);
            }

            foreach (BetterConsolePropertyData property in entry.properties)
            {
                builder.Append(' ').Append(property.name).Append('=').Append(property.value);
            }

            return builder.ToString();
        }

        private static IEnumerable<string> Tokenize(string query)
        {
            List<string> tokens = new List<string>();
            StringBuilder token = new StringBuilder();
            bool quoted = false;
            bool regex = false;
            for (int index = 0; index < (query ?? string.Empty).Length; index++)
            {
                char character = query[index];
                if (character == '"' && !regex)
                {
                    quoted = !quoted;
                    token.Append(character);
                    continue;
                }

                if (character == '/' && !quoted)
                {
                    regex = !regex;
                    token.Append(character);
                    continue;
                }

                if (char.IsWhiteSpace(character) && !quoted && !regex)
                {
                    if (token.Length > 0)
                    {
                        tokens.Add(token.ToString());
                        token.Length = 0;
                    }

                    continue;
                }

                token.Append(character);
            }

            if (token.Length > 0)
            {
                tokens.Add(token.ToString());
            }

            return tokens;
        }

        private static string Unquote(string value)
        {
            return value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"'
                ? value.Substring(1, value.Length - 2)
                : value;
        }

        private static bool Contains(string haystack, string needle)
        {
            return (haystack ?? string.Empty).IndexOf(
                needle ?? string.Empty,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryDate(string value, out DateTime result)
        {
            return DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out result);
        }

        private sealed class Term
        {
            public Term(string field, string value, bool negative, Regex regex)
            {
                this.field = field;
                this.value = value;
                this.negative = negative;
                this.regex = regex;
            }

            public readonly string field;
            public readonly string value;
            public readonly bool negative;
            public readonly Regex regex;
        }
    }
}
