using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal sealed class BetterHierarchyQuery
    {
        internal readonly struct Token
        {
            internal Token(string key, string value, bool negated)
            {
                Key = key;
                Value = value;
                Negated = negated;
            }

            internal string Key { get; }
            internal string Value { get; }
            internal bool Negated { get; }
        }

        private readonly List<Token> tokens;

        private BetterHierarchyQuery(List<Token> tokens)
        {
            this.tokens = tokens;
        }

        internal IReadOnlyList<Token> Tokens => tokens;
        internal bool IsEmpty => tokens.Count == 0;

        internal static BetterHierarchyQuery Parse(string query)
        {
            List<Token> result = new List<Token>();
            foreach (string raw in Split(query ?? string.Empty))
            {
                bool negated = raw.Length > 1 && raw[0] == '-';
                string token = negated ? raw.Substring(1) : raw;
                int separator = token.IndexOf(':');
                if (separator > 0)
                {
                    result.Add(new Token(
                        token.Substring(0, separator).ToLowerInvariant(),
                        token.Substring(separator + 1),
                        negated));
                }
                else if (!string.IsNullOrWhiteSpace(token))
                {
                    result.Add(new Token(string.Empty, token, negated));
                }
            }

            return new BetterHierarchyQuery(result);
        }

        internal bool Matches(
            GameObject gameObject,
            BetterHierarchyDiagnosticFlags diagnostics,
            Func<string, GameObject, bool> collectionLookup = null)
        {
            if (gameObject == null)
            {
                return false;
            }

            foreach (Token token in tokens)
            {
                bool match = MatchToken(gameObject, diagnostics, token, collectionLookup);
                if (token.Negated ? match : !match)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchToken(
            GameObject gameObject,
            BetterHierarchyDiagnosticFlags diagnostics,
            Token token,
            Func<string, GameObject, bool> collectionLookup)
        {
            string value = token.Value ?? string.Empty;
            switch (token.Key)
            {
                case "t":
                case "type":
                    return gameObject.GetComponents<Component>()
                        .Any(component => component != null &&
                                          Contains(component.GetType().Name, value));
                case "tag":
                    return Contains(gameObject.tag, value);
                case "layer":
                    return Contains(LayerMask.LayerToName(gameObject.layer), value) ||
                           gameObject.layer.ToString() == value;
                case "scene":
                    return Contains(gameObject.scene.name, value);
                case "path":
                    return Contains(GetPath(gameObject.transform), value);
                case "is":
                    return MatchState(gameObject, value);
                case "warn":
                case "warning":
                    return MatchDiagnostic(diagnostics, value);
                case "fav":
                case "favorite":
                    return BetterHierarchyUserSettings.IsFavorite(gameObject) ==
                           !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
                case "collection":
                    return collectionLookup != null && collectionLookup(value, gameObject);
                case "ref":
                    return HasReference(gameObject, value);
                default:
                    return FuzzyContains(gameObject.name, value) ||
                           FuzzyContains(GetPath(gameObject.transform), value);
            }
        }

        private static bool MatchState(GameObject gameObject, string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "active": return gameObject.activeInHierarchy;
                case "inactive": return !gameObject.activeInHierarchy;
                case "root": return gameObject.transform.parent == null;
                case "child": return gameObject.transform.parent != null;
                case "leaf": return gameObject.transform.childCount == 0;
                case "prefab": return PrefabUtility.IsPartOfAnyPrefab(gameObject);
                case "static": return gameObject.isStatic;
                case "hidden": return SceneVisibilityManager.instance.IsHidden(gameObject);
                case "visible": return !SceneVisibilityManager.instance.IsHidden(gameObject);
                case "locked": return SceneVisibilityManager.instance.IsPickingDisabled(gameObject);
                case "favorite": return BetterHierarchyUserSettings.IsFavorite(gameObject);
                default: return false;
            }
        }

        private static bool MatchDiagnostic(BetterHierarchyDiagnosticFlags diagnostics, string value)
        {
            if (string.Equals(value, "any", StringComparison.OrdinalIgnoreCase))
            {
                return diagnostics != BetterHierarchyDiagnosticFlags.None;
            }

            string normalized = value.Replace("-", string.Empty).Replace("_", string.Empty);
            foreach (BetterHierarchyDiagnosticFlags flag in Enum.GetValues(typeof(BetterHierarchyDiagnosticFlags)))
            {
                if (flag != BetterHierarchyDiagnosticFlags.None &&
                    string.Equals(flag.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return (diagnostics & flag) != 0;
                }
            }

            return false;
        }

        private static bool HasReference(GameObject gameObject, string value)
        {
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                try
                {
                    SerializedObject serializedObject = new SerializedObject(component);
                    SerializedProperty property = serializedObject.GetIterator();
                    bool enterChildren = true;
                    while (property.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (property.propertyType != SerializedPropertyType.ObjectReference ||
                            property.objectReferenceValue == null)
                        {
                            continue;
                        }

                        UnityEngine.Object reference = property.objectReferenceValue;
                        if (Contains(reference.name, value) ||
                            Contains(AssetDatabase.GetAssetPath(reference), value))
                        {
                            return true;
                        }
                    }
                }
                catch (Exception)
                {
                    // Some native components cannot be traversed safely; skip them.
                }
            }

            return false;
        }

        private static IEnumerable<string> Split(string query)
        {
            StringBuilder current = new StringBuilder();
            bool quoted = false;
            foreach (char character in query)
            {
                if (character == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (char.IsWhiteSpace(character) && !quoted)
                {
                    if (current.Length > 0)
                    {
                        yield return current.ToString();
                        current.Length = 0;
                    }
                }
                else
                {
                    current.Append(character);
                }
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
            }
        }

        internal static bool FuzzyContains(string source, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            int valueIndex = 0;
            foreach (char character in source)
            {
                if (char.ToUpperInvariant(character) == char.ToUpperInvariant(value[valueIndex]))
                {
                    valueIndex++;
                    if (valueIndex == value.Length)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static string GetPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            Stack<string> segments = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent)
            {
                segments.Push(current.name);
            }

            return string.Join("/", segments);
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(value ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal static class BetterHierarchyRuleMatcher
    {
        private static readonly Dictionary<string, Regex> RegexCache = new Dictionary<string, Regex>();
        private static readonly Dictionary<int, CachedStyle> StyleCache = new Dictionary<int, CachedStyle>();
        private static int revision;

        static BetterHierarchyRuleMatcher()
        {
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
            BetterHierarchyProjectSettings.Changed += Invalidate;
        }

        internal static BetterHierarchyStyle GetStyle(
            GameObject gameObject,
            BetterHierarchyDiagnosticFlags diagnostics)
        {
            if (gameObject == null) return default;
            int id = gameObject.GetInstanceID();
            if (StyleCache.TryGetValue(id, out CachedStyle cached) &&
                cached.Revision == revision && cached.Diagnostics == diagnostics)
                return cached.Style;

            BetterHierarchyRule best = null;
            foreach (BetterHierarchyRule rule in BetterHierarchyProjectSettings.Rules)
            {
                if (!rule.Enabled || !Matches(rule, gameObject, diagnostics))
                {
                    continue;
                }

                if (best == null || rule.Priority >= best.Priority)
                {
                    best = rule;
                }
            }

            BetterHierarchyStyle style = new BetterHierarchyStyle(best);
            StyleCache[id] = new CachedStyle(revision, diagnostics, style);
            return style;
        }

        internal static void Invalidate()
        {
            revision++;
            StyleCache.Clear();
        }

        internal static bool Matches(
            BetterHierarchyRule rule,
            GameObject gameObject,
            BetterHierarchyDiagnosticFlags diagnostics)
        {
            if (rule == null || gameObject == null)
            {
                return false;
            }

            if (MatchesDirect(rule, gameObject, diagnostics))
            {
                return true;
            }

            if (!rule.Recursive)
            {
                return false;
            }

            for (Transform parent = gameObject.transform.parent; parent != null; parent = parent.parent)
            {
                if (MatchesDirect(rule, parent.gameObject, diagnostics))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesDirect(
            BetterHierarchyRule rule,
            GameObject gameObject,
            BetterHierarchyDiagnosticFlags diagnostics)
        {
            string value = rule.Value ?? string.Empty;
            switch (rule.Match)
            {
                case BetterHierarchyRuleMatch.NameEquals:
                    return string.Equals(gameObject.name, value, StringComparison.OrdinalIgnoreCase);
                case BetterHierarchyRuleMatch.NameStartsWith:
                    return gameObject.name.StartsWith(value, StringComparison.OrdinalIgnoreCase);
                case BetterHierarchyRuleMatch.NameContains:
                    return gameObject.name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
                case BetterHierarchyRuleMatch.NameRegex:
                    return RegexMatch(gameObject.name, value);
                case BetterHierarchyRuleMatch.HasComponent:
                    return gameObject.GetComponents<Component>().Any(component =>
                        component != null &&
                        (string.Equals(component.GetType().Name, value, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(component.GetType().FullName, value, StringComparison.OrdinalIgnoreCase)));
                case BetterHierarchyRuleMatch.Tag:
                    return string.Equals(gameObject.tag, value, StringComparison.OrdinalIgnoreCase);
                case BetterHierarchyRuleMatch.Layer:
                    return string.Equals(LayerMask.LayerToName(gameObject.layer), value, StringComparison.OrdinalIgnoreCase) ||
                           gameObject.layer.ToString() == value;
                case BetterHierarchyRuleMatch.Scene:
                    return gameObject.scene.name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
                case BetterHierarchyRuleMatch.Prefab:
                    return MatchPrefab(gameObject, value);
                case BetterHierarchyRuleMatch.Root:
                    return gameObject.transform.parent == null;
                case BetterHierarchyRuleMatch.Leaf:
                    return gameObject.transform.childCount == 0;
                case BetterHierarchyRuleMatch.Inactive:
                    return !gameObject.activeInHierarchy;
                case BetterHierarchyRuleMatch.MissingScript:
                    return (diagnostics & BetterHierarchyDiagnosticFlags.MissingScript) != 0;
                case BetterHierarchyRuleMatch.Object:
                    return string.Equals(BetterHierarchyObjectIds.Get(gameObject), value, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        private static bool MatchPrefab(GameObject gameObject, string value)
        {
            if (!PrefabUtility.IsPartOfAnyPrefab(gameObject))
            {
                return false;
            }

            PrefabAssetType type = PrefabUtility.GetPrefabAssetType(gameObject);
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, "any", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(type.ToString(), value, StringComparison.OrdinalIgnoreCase);
        }

        private static bool RegexMatch(string input, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            if (!RegexCache.TryGetValue(pattern, out Regex regex))
            {
                try
                {
                    regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                }
                catch (ArgumentException)
                {
                    return false;
                }

                RegexCache[pattern] = regex;
            }

            return regex.IsMatch(input ?? string.Empty);
        }

        private readonly struct CachedStyle
        {
            internal CachedStyle(int revision, BetterHierarchyDiagnosticFlags diagnostics, BetterHierarchyStyle style)
            {
                Revision = revision;
                Diagnostics = diagnostics;
                Style = style;
            }

            internal int Revision { get; }
            internal BetterHierarchyDiagnosticFlags Diagnostics { get; }
            internal BetterHierarchyStyle Style { get; }
        }
    }
}
