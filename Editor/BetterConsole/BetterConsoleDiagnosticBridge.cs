using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

namespace DansToolbox.EditorTools.BetterConsole
{
    public readonly struct BetterConsoleDiagnosticSummary
    {
        public BetterConsoleDiagnosticSummary(int logs, int warnings, int errors)
        {
            Logs = logs;
            Warnings = warnings;
            Errors = errors;
        }

        public int Logs { get; }
        public int Warnings { get; }
        public int Errors { get; }
        public int Total => Logs + Warnings + Errors;
        public bool HasSignals => Warnings > 0 || Errors > 0;
        public string Badge => Errors > 0 ? "E" + Compact(Errors) : Warnings > 0 ? "W" + Compact(Warnings) : string.Empty;
        public string Tooltip => Errors + " errors · " + Warnings + " warnings · " + Total + " related logs\nOpen in Better Console";

        private static string Compact(int value)
        {
            if (value >= 1000) return (value / 1000f).ToString("0.#") + "k";
            return value.ToString();
        }
    }

    [InitializeOnLoad]
    public static class BetterConsoleDiagnosticBridge
    {
        private static readonly Dictionary<int, List<BetterConsoleEntry>> entriesByContext =
            new Dictionary<int, List<BetterConsoleEntry>>();
        private static readonly Dictionary<string, List<BetterConsoleEntry>> entriesByFile =
            new Dictionary<string, List<BetterConsoleEntry>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, BetterConsoleDiagnosticSummary> objectSummaries =
            new Dictionary<int, BetterConsoleDiagnosticSummary>();
        private static readonly Dictionary<string, BetterConsoleDiagnosticSummary> assetSummaries =
            new Dictionary<string, BetterConsoleDiagnosticSummary>(StringComparer.OrdinalIgnoreCase);
        private static List<KeyValuePair<string, string>> packageRoots;
        private static int indexedRevision = -1;

        static BetterConsoleDiagnosticBridge()
        {
            BetterConsoleStore.Changed -= Invalidate;
            BetterConsoleStore.Changed += Invalidate;
        }

        public static event Action Changed;
        public static event Action<string> AssetRevealRequested;

        public static BetterConsoleDiagnosticSummary GetSummary(UnityEngine.Object target)
        {
            if (target == null) return default;
            EnsureIndex();
            int instanceId = target.GetInstanceID();
            if (objectSummaries.TryGetValue(instanceId, out BetterConsoleDiagnosticSummary cached)) return cached;

            Dictionary<long, BetterConsoleEntry> related = new Dictionary<long, BetterConsoleEntry>();
            AddTargetEntries(target, related);
            BetterConsoleDiagnosticSummary summary = Summarize(related.Values);
            objectSummaries[instanceId] = summary;
            return summary;
        }

        public static BetterConsoleDiagnosticSummary GetSummary(IEnumerable<UnityEngine.Object> targets)
        {
            EnsureIndex();
            Dictionary<long, BetterConsoleEntry> related = new Dictionary<long, BetterConsoleEntry>();
            foreach (UnityEngine.Object target in targets ?? Enumerable.Empty<UnityEngine.Object>())
            {
                if (target != null) AddTargetEntries(target, related);
            }
            return Summarize(related.Values);
        }

        public static BetterConsoleDiagnosticSummary GetSummaryForAssetPath(string assetPath)
        {
            EnsureIndex();
            string path = NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(path)) return default;
            if (AssetDatabase.IsValidFolder(path.TrimEnd('/'))) path = path.TrimEnd('/') + "/";
            if (assetSummaries.TryGetValue(path, out BetterConsoleDiagnosticSummary cached)) return cached;

            Dictionary<long, BetterConsoleEntry> related = new Dictionary<long, BetterConsoleEntry>();
            AddFileEntries(path, related);
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path.TrimEnd('/'));
            if (asset != null) AddContextEntries(asset.GetInstanceID(), related);
            BetterConsoleDiagnosticSummary summary = Summarize(related.Values);
            assetSummaries[path] = summary;
            return summary;
        }

        public static bool OpenForTargets(IEnumerable<UnityEngine.Object> targets)
        {
            string query = BuildTargetQuery(targets, null);
            if (string.IsNullOrEmpty(query)) return false;
            BetterConsoleWindow.OpenQuery(query);
            return true;
        }

        public static bool OpenForAssetPaths(IEnumerable<string> assetPaths)
        {
            string query = BuildTargetQuery(null, assetPaths);
            if (string.IsNullOrEmpty(query)) return false;
            BetterConsoleWindow.OpenQuery(query);
            return true;
        }

        public static bool OpenForCurrentSelection()
        {
            return OpenForTargets(Selection.objects);
        }

        public static bool CanRevealAssetPath(string path)
        {
            string assetPath = NormalizeAssetPath(path).TrimEnd('/');
            return !string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
        }

        public static bool RevealAssetPath(string path)
        {
            string assetPath = NormalizeAssetPath(path).TrimEnd('/');
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null) return false;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            AssetRevealRequested?.Invoke(assetPath);
            return true;
        }

        internal static string BuildTargetQuery(
            IEnumerable<UnityEngine.Object> targets,
            IEnumerable<string> assetPaths)
        {
            HashSet<string> selectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (UnityEngine.Object target in targets ?? Enumerable.Empty<UnityEngine.Object>())
            {
                if (target == null) continue;
                AddTargetSelectors(target, selectors);
            }

            foreach (string rawPath in assetPaths ?? Enumerable.Empty<string>())
            {
                string path = NormalizeAssetPath(rawPath);
                if (string.IsNullOrEmpty(path)) continue;
                if (AssetDatabase.IsValidFolder(path.TrimEnd('/'))) path = path.TrimEnd('/') + "/";
                selectors.Add("file=" + path);
            }

            return selectors.Count == 0
                ? string.Empty
                : "target:\"" + string.Join("|", selectors.OrderBy(value => value, StringComparer.Ordinal)) + "\"";
        }

        internal static void Invalidate()
        {
            indexedRevision = -1;
            objectSummaries.Clear();
            assetSummaries.Clear();
            Changed?.Invoke();
        }

        internal static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string normalized = path.Replace('\\', '/').Trim();
            try
            {
                if (!Path.IsPathRooted(normalized)) return normalized.TrimStart('/');

                string fullPath = Path.GetFullPath(normalized).Replace('\\', '/');
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                    .Replace('\\', '/')
                    .TrimEnd('/');
                if (fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return fullPath.Substring(projectRoot.Length + 1);
                }

                foreach (KeyValuePair<string, string> packageRoot in GetPackageRoots())
                {
                    if (string.Equals(fullPath, packageRoot.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        return packageRoot.Value;
                    }

                    if (fullPath.StartsWith(packageRoot.Key + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        return packageRoot.Value + fullPath.Substring(packageRoot.Key.Length);
                    }
                }
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
            catch (NotSupportedException)
            {
                return string.Empty;
            }
            catch (PathTooLongException)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static List<KeyValuePair<string, string>> GetPackageRoots()
        {
            if (packageRoots != null) return packageRoots;

            packageRoots = new List<KeyValuePair<string, string>>();
            foreach (PackageManagerInfo package in PackageManagerInfo.GetAllRegisteredPackages())
            {
                if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath) ||
                    string.IsNullOrWhiteSpace(package.assetPath))
                {
                    continue;
                }

                try
                {
                    string resolvedPath = Path.GetFullPath(package.resolvedPath)
                        .Replace('\\', '/')
                        .TrimEnd('/');
                    packageRoots.Add(new KeyValuePair<string, string>(
                        resolvedPath,
                        package.assetPath.TrimEnd('/')));
                }
                catch (ArgumentException)
                {
                    // Ignore malformed package metadata rather than breaking diagnostics.
                }
                catch (NotSupportedException)
                {
                    // Ignore malformed package metadata rather than breaking diagnostics.
                }
                catch (PathTooLongException)
                {
                    // Ignore malformed package metadata rather than breaking diagnostics.
                }
            }

            packageRoots.Sort((left, right) => right.Key.Length.CompareTo(left.Key.Length));
            return packageRoots;
        }

        private static void EnsureIndex()
        {
            if (indexedRevision == BetterConsoleStore.Revision) return;
            indexedRevision = BetterConsoleStore.Revision;
            entriesByContext.Clear();
            entriesByFile.Clear();
            objectSummaries.Clear();
            assetSummaries.Clear();

            foreach (BetterConsoleEntry entry in BetterConsoleStore.Entries)
            {
                if (entry.contextInstanceId != 0) Add(entriesByContext, entry.contextInstanceId, entry);
                string file = NormalizeAssetPath(entry.file);
                if (!string.IsNullOrEmpty(file)) Add(entriesByFile, file, entry);

                UnityEngine.Object context = BetterConsoleNativeBridge.ResolveContext(entry.contextInstanceId);
                if (context != null && AssetDatabase.Contains(context))
                {
                    string contextPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(context));
                    if (!string.IsNullOrEmpty(contextPath)) Add(entriesByFile, contextPath, entry);
                }
            }
        }

        private static void AddTargetEntries(UnityEngine.Object target, IDictionary<long, BetterConsoleEntry> destination)
        {
            AddContextEntries(target.GetInstanceID(), destination);
            if (target is GameObject gameObject)
            {
                foreach (Component component in gameObject.GetComponents<Component>())
                {
                    if (component != null) AddContextEntries(component.GetInstanceID(), destination);
                }
            }
            else if (target is Component component)
            {
                AddContextEntries(component.gameObject.GetInstanceID(), destination);
            }

            if (AssetDatabase.Contains(target)) AddFileEntries(NormalizeAssetPath(AssetDatabase.GetAssetPath(target)), destination);
        }

        private static void AddTargetSelectors(UnityEngine.Object target, ISet<string> selectors)
        {
            selectors.Add("id=" + target.GetInstanceID());
            if (target is GameObject gameObject)
            {
                foreach (Component component in gameObject.GetComponents<Component>())
                {
                    if (component != null) selectors.Add("id=" + component.GetInstanceID());
                }
            }
            else if (target is Component component)
            {
                selectors.Add("id=" + component.gameObject.GetInstanceID());
            }

            if (AssetDatabase.Contains(target))
            {
                string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(target));
                if (!string.IsNullOrEmpty(path)) selectors.Add("file=" + path);
            }
        }

        private static void AddContextEntries(int instanceId, IDictionary<long, BetterConsoleEntry> destination)
        {
            if (!entriesByContext.TryGetValue(instanceId, out List<BetterConsoleEntry> entries)) return;
            foreach (BetterConsoleEntry entry in entries) destination[entry.id] = entry;
        }

        private static void AddFileEntries(string path, IDictionary<long, BetterConsoleEntry> destination)
        {
            if (string.IsNullOrEmpty(path)) return;
            bool folder = path.EndsWith("/", StringComparison.Ordinal);
            if (!folder)
            {
                if (!entriesByFile.TryGetValue(path, out List<BetterConsoleEntry> exactEntries)) return;
                foreach (BetterConsoleEntry entry in exactEntries) destination[entry.id] = entry;
                return;
            }

            foreach (KeyValuePair<string, List<BetterConsoleEntry>> pair in entriesByFile)
            {
                if (!pair.Key.StartsWith(path, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (BetterConsoleEntry entry in pair.Value) destination[entry.id] = entry;
            }
        }

        private static BetterConsoleDiagnosticSummary Summarize(IEnumerable<BetterConsoleEntry> entries)
        {
            int logs = 0;
            int warnings = 0;
            int errors = 0;
            foreach (BetterConsoleEntry entry in entries)
            {
                if (entry.severity == BetterConsoleSeverity.Log)
                {
                    logs++;
                    continue;
                }

                BetterConsoleIssueState state = BetterConsoleSettings.GetIssueState(entry.signature);
                if (BetterConsoleSettings.IsMuted(entry) || state?.triage == BetterConsoleTriage.Resolved) continue;
                if (entry.severity == BetterConsoleSeverity.Warning) warnings++;
                else errors++;
            }
            return new BetterConsoleDiagnosticSummary(logs, warnings, errors);
        }

        private static void Add<TKey>(IDictionary<TKey, List<BetterConsoleEntry>> map, TKey key, BetterConsoleEntry entry)
        {
            if (!map.TryGetValue(key, out List<BetterConsoleEntry> entries))
            {
                entries = new List<BetterConsoleEntry>();
                map.Add(key, entries);
            }
            entries.Add(entry);
        }
    }
}
