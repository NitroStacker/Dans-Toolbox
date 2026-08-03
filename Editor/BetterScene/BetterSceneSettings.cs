using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterScene
{
    [FilePath("ProjectSettings/BetterSceneSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class BetterSceneSettings : ScriptableSingleton<BetterSceneSettings>
    {
        private static readonly Dictionary<string, UnityEngine.Object> assetReferenceCache =
            new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);

        [SerializeField] private bool drawSelectionBounds = true;
        [SerializeField] private bool drawPivot = true;
        [SerializeField] private bool drawDiagnostics = true;
        [SerializeField] private bool alignToSurface = true;
        [SerializeField] private bool parentToSurface;
        [SerializeField] private bool keepPlacing = true;
        [SerializeField] private bool includeDescendants = true;
        [SerializeField] private bool groundToZeroWhenNoSurface = true;
        [SerializeField] private bool accountForViewZoom = true;
        [SerializeField] private float scatterRadius = 2f;
        [SerializeField] private float scatterHeight;
        [SerializeField] private int scatterSeed = 1337;
        [SerializeField] private string placementAssetGuid = string.Empty;
        [SerializeField] private long placementAssetLocalId;
        [SerializeField] private List<string> recentPlacementGuids = new List<string>();
        [SerializeField] private List<long> recentPlacementLocalIds = new List<long>();
        [SerializeField] private List<int> toolbarOrder = new List<int>
        {
            (int)BetterScenePanel.Create,
            (int)BetterScenePanel.Transform,
            (int)BetterScenePanel.Place,
            (int)BetterScenePanel.View,
            (int)BetterScenePanel.Visibility,
            (int)BetterScenePanel.Measure,
            (int)BetterScenePanel.Review
        };
        [SerializeField] private List<int> hiddenToolbarPanels = new List<int>();
        [SerializeField] private bool toolbarHistoryVisible = true;
        [SerializeField] private bool toolbarQuickActionsVisible = true;
        [SerializeField] private List<BetterSceneBookmark> bookmarks = new List<BetterSceneBookmark>();
        [SerializeField] private List<BetterSceneLayerPreset> layerPresets = new List<BetterSceneLayerPreset>();

        internal static event Action Changed;

        static BetterSceneSettings()
        {
            EditorApplication.projectChanged += assetReferenceCache.Clear;
        }

        internal static bool DrawSelectionBounds { get => instance.drawSelectionBounds; set => Set(ref instance.drawSelectionBounds, value); }
        internal static bool DrawPivot { get => instance.drawPivot; set => Set(ref instance.drawPivot, value); }
        internal static bool DrawDiagnostics { get => instance.drawDiagnostics; set => Set(ref instance.drawDiagnostics, value); }
        internal static bool AlignToSurface { get => instance.alignToSurface; set => Set(ref instance.alignToSurface, value); }
        internal static bool ParentToSurface { get => instance.parentToSurface; set => Set(ref instance.parentToSurface, value); }
        internal static bool KeepPlacing { get => instance.keepPlacing; set => Set(ref instance.keepPlacing, value); }
        internal static bool IncludeDescendants { get => instance.includeDescendants; set => Set(ref instance.includeDescendants, value); }
        internal static bool GroundToZeroWhenNoSurface { get => instance.groundToZeroWhenNoSurface; set => Set(ref instance.groundToZeroWhenNoSurface, value); }
        internal static bool AccountForViewZoom { get => instance.accountForViewZoom; set => Set(ref instance.accountForViewZoom, value); }
        internal static float ScatterRadius { get => instance.scatterRadius; set => Set(ref instance.scatterRadius, Mathf.Max(0f, value)); }
        internal static float ScatterHeight { get => instance.scatterHeight; set => Set(ref instance.scatterHeight, Mathf.Max(0f, value)); }
        internal static int ScatterSeed { get => instance.scatterSeed; set => Set(ref instance.scatterSeed, value); }
        internal static IReadOnlyList<BetterSceneBookmark> Bookmarks => instance.bookmarks;
        internal static IReadOnlyList<BetterSceneLayerPreset> LayerPresets => instance.layerPresets;
        internal static IReadOnlyList<string> RecentPlacementGuids => instance.recentPlacementGuids;
        internal static IReadOnlyList<BetterScenePanel> ToolbarOrder
        {
            get
            {
                EnsureToolbarOrder();
                return instance.toolbarOrder.ConvertAll(value => (BetterScenePanel)value);
            }
        }
        internal static bool ToolbarHistoryVisible { get => instance.toolbarHistoryVisible; set => Set(ref instance.toolbarHistoryVisible, value); }
        internal static bool ToolbarQuickActionsVisible { get => instance.toolbarQuickActionsVisible; set => Set(ref instance.toolbarQuickActionsVisible, value); }

        internal static bool IsToolbarPanelVisible(BetterScenePanel panel)
        {
            return !instance.hiddenToolbarPanels.Contains((int)panel);
        }

        internal static void SetToolbarPanelVisible(BetterScenePanel panel, bool visible)
        {
            int value = (int)panel;
            bool changed = visible
                ? instance.hiddenToolbarPanels.Remove(value)
                : !instance.hiddenToolbarPanels.Contains(value) && AddHiddenToolbarPanel(value);
            if (changed) SaveAndNotify();
        }

        internal static void MoveToolbarPanel(BetterScenePanel panel, int direction)
        {
            EnsureToolbarOrder();
            int index = instance.toolbarOrder.IndexOf((int)panel);
            int target = Mathf.Clamp(index + Math.Sign(direction), 0, instance.toolbarOrder.Count - 1);
            if (index < 0 || target == index) return;
            int value = instance.toolbarOrder[index];
            instance.toolbarOrder.RemoveAt(index);
            instance.toolbarOrder.Insert(target, value);
            SaveAndNotify();
        }

        internal static void ResetToolbarLayout()
        {
            instance.toolbarOrder = DefaultToolbarOrder();
            instance.hiddenToolbarPanels.Clear();
            instance.toolbarHistoryVisible = true;
            instance.toolbarQuickActionsVisible = true;
            SaveAndNotify();
        }

        internal static UnityEngine.Object PlacementAsset
        {
            get
            {
                return LoadAssetReference(
                    instance.placementAssetGuid,
                    instance.placementAssetLocalId);
            }
            set
            {
                string guid = string.Empty;
                long localId = 0;
                if (value != null && !TryGetAssetReference(value, out guid, out localId)) return;
                if (string.Equals(instance.placementAssetGuid, guid, StringComparison.Ordinal) &&
                    instance.placementAssetLocalId == localId)
                {
                    return;
                }
                instance.placementAssetGuid = guid;
                instance.placementAssetLocalId = localId;
                if (!string.IsNullOrEmpty(guid)) assetReferenceCache[AssetReferenceKey(guid, localId)] = value;
                if (!string.IsNullOrEmpty(guid))
                {
                    EnsureRecentPlacementReferences();
                    for (int index = instance.recentPlacementGuids.Count - 1; index >= 0; index--)
                    {
                        if (!string.Equals(instance.recentPlacementGuids[index], guid, StringComparison.Ordinal) ||
                            instance.recentPlacementLocalIds[index] != localId)
                        {
                            continue;
                        }
                        instance.recentPlacementGuids.RemoveAt(index);
                        instance.recentPlacementLocalIds.RemoveAt(index);
                    }
                    instance.recentPlacementGuids.Insert(0, guid);
                    instance.recentPlacementLocalIds.Insert(0, localId);
                    if (instance.recentPlacementGuids.Count > 8)
                    {
                        instance.recentPlacementGuids.RemoveRange(8, instance.recentPlacementGuids.Count - 8);
                        instance.recentPlacementLocalIds.RemoveRange(8, instance.recentPlacementLocalIds.Count - 8);
                    }
                }
                SaveAndNotify();
            }
        }

        internal static bool CanPersistPlacementAsset(UnityEngine.Object asset)
        {
            return TryGetAssetReference(asset, out _, out _);
        }

        internal static UnityEngine.Object[] GetRecentPlacementAssets()
        {
            var assets = new List<UnityEngine.Object>();
            bool changed = false;
            EnsureRecentPlacementReferences();
            for (int index = instance.recentPlacementGuids.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object asset = LoadAssetReference(
                    instance.recentPlacementGuids[index],
                    instance.recentPlacementLocalIds[index]);
                if (asset == null)
                {
                    instance.recentPlacementGuids.RemoveAt(index);
                    instance.recentPlacementLocalIds.RemoveAt(index);
                    changed = true;
                }
                else
                {
                    assets.Insert(0, asset);
                }
            }
            if (changed) SaveAndNotify();
            return assets.ToArray();
        }

        private static bool TryGetAssetReference(
            UnityEngine.Object asset,
            out string guid,
            out long localId)
        {
            guid = string.Empty;
            localId = 0;
            return asset != null &&
                   EditorUtility.IsPersistent(asset) &&
                   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId) &&
                   !string.IsNullOrEmpty(guid);
        }

        private static UnityEngine.Object LoadAssetReference(string guid, long localId)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            string key = AssetReferenceKey(guid, localId);
            if (assetReferenceCache.TryGetValue(key, out UnityEngine.Object cached)) return cached;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                assetReferenceCache[key] = null;
                return null;
            }
            if (localId == 0)
            {
                UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(path);
                assetReferenceCache[key] = main;
                return main;
            }

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset != null &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string candidateGuid, out long candidateLocalId) &&
                    string.Equals(candidateGuid, guid, StringComparison.Ordinal) &&
                    candidateLocalId == localId)
                {
                    assetReferenceCache[key] = asset;
                    return asset;
                }
            }
            assetReferenceCache[key] = null;
            return null;
        }

        private static string AssetReferenceKey(string guid, long localId)
        {
            return guid + ":" + localId;
        }

        private static void EnsureRecentPlacementReferences()
        {
            instance.recentPlacementGuids ??= new List<string>();
            instance.recentPlacementLocalIds ??= new List<long>();
            while (instance.recentPlacementLocalIds.Count < instance.recentPlacementGuids.Count)
            {
                instance.recentPlacementLocalIds.Add(0);
            }
            if (instance.recentPlacementLocalIds.Count > instance.recentPlacementGuids.Count)
            {
                instance.recentPlacementLocalIds.RemoveRange(
                    instance.recentPlacementGuids.Count,
                    instance.recentPlacementLocalIds.Count - instance.recentPlacementGuids.Count);
            }
        }

        private static bool AddHiddenToolbarPanel(int value)
        {
            instance.hiddenToolbarPanels.Add(value);
            return true;
        }

        private static void EnsureToolbarOrder()
        {
            if (instance.toolbarOrder == null) instance.toolbarOrder = new List<int>();
            if (instance.hiddenToolbarPanels == null) instance.hiddenToolbarPanels = new List<int>();
            List<int> defaults = DefaultToolbarOrder();
            var valid = new HashSet<int>(defaults);
            instance.toolbarOrder.RemoveAll(value => !valid.Contains(value));
            var seen = new HashSet<int>();
            instance.toolbarOrder.RemoveAll(value => !seen.Add(value));
            foreach (int value in defaults)
            {
                if (!seen.Contains(value)) instance.toolbarOrder.Add(value);
            }
            instance.hiddenToolbarPanels.RemoveAll(value => !valid.Contains(value));
        }

        private static List<int> DefaultToolbarOrder()
        {
            return new List<int>
            {
                (int)BetterScenePanel.Create,
                (int)BetterScenePanel.Transform,
                (int)BetterScenePanel.Place,
                (int)BetterScenePanel.View,
                (int)BetterScenePanel.Visibility,
                (int)BetterScenePanel.Measure,
                (int)BetterScenePanel.Review
            };
        }

        internal static BetterSceneBookmark AddBookmark(
            string name,
            string scenePath,
            Vector3 pivot,
            Quaternion rotation,
            float size,
            bool orthographic,
            bool in2DMode)
        {
            var bookmark = new BetterSceneBookmark
            {
                Name = MakeUniqueName(name, instance.bookmarks.ConvertAll(item => item.Name), "VIEW"),
                ScenePath = scenePath,
                Pivot = pivot,
                Rotation = rotation,
                Size = size,
                Orthographic = orthographic,
                In2DMode = in2DMode
            };
            instance.bookmarks.Add(bookmark);
            SaveAndNotify();
            return bookmark;
        }

        internal static void RenameBookmark(string id, string name)
        {
            BetterSceneBookmark bookmark = instance.bookmarks.Find(item => item.Id == id);
            if (bookmark == null) return;
            bookmark.Name = MakeUniqueName(name, instance.bookmarks.FindAll(item => item.Id != id).ConvertAll(item => item.Name), "VIEW");
            SaveAndNotify();
        }

        internal static void RemoveBookmark(string id)
        {
            if (instance.bookmarks.RemoveAll(item => item.Id == id) > 0) SaveAndNotify();
        }

        internal static BetterSceneLayerPreset AddLayerPreset(string name, int visibleLayers, int lockedLayers)
        {
            var preset = new BetterSceneLayerPreset
            {
                Name = MakeUniqueName(name, instance.layerPresets.ConvertAll(item => item.Name), "LAYERS"),
                VisibleLayers = visibleLayers,
                LockedLayers = lockedLayers
            };
            instance.layerPresets.Add(preset);
            SaveAndNotify();
            return preset;
        }

        internal static void RemoveLayerPreset(string id)
        {
            if (instance.layerPresets.RemoveAll(item => item.Id == id) > 0) SaveAndNotify();
        }

        internal static string MakeUniqueName(string proposed, IEnumerable<string> existing, string fallback)
        {
            string root = string.IsNullOrWhiteSpace(proposed) ? fallback : proposed.Trim().ToUpperInvariant();
            var names = new HashSet<string>(existing ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!names.Contains(root)) return root;
            for (int index = 2; index < 1000; index++)
            {
                string candidate = root + " " + index;
                if (!names.Contains(candidate)) return candidate;
            }
            return root + " " + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
        }

        private static void Set<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            SaveAndNotify();
        }

        private static void SaveAndNotify()
        {
            instance.Save(true);
            Changed?.Invoke();
            SceneView.RepaintAll();
        }
    }
}
