using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterScene
{
    [FilePath("ProjectSettings/BetterSceneSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class BetterSceneSettings : ScriptableSingleton<BetterSceneSettings>
    {
        [SerializeField] private bool overlayVisible = true;
        [SerializeField] private bool drawSelectionBounds = true;
        [SerializeField] private bool drawPivot = true;
        [SerializeField] private bool drawDiagnostics = true;
        [SerializeField] private bool alignToSurface = true;
        [SerializeField] private bool parentToSurface;
        [SerializeField] private bool keepPlacing = true;
        [SerializeField] private bool includeDescendants = true;
        [SerializeField] private bool groundToZeroWhenNoSurface = true;
        [SerializeField] private float scatterRadius = 2f;
        [SerializeField] private float scatterHeight;
        [SerializeField] private int scatterSeed = 1337;
        [SerializeField] private string placementAssetGuid = string.Empty;
        [SerializeField] private List<BetterSceneBookmark> bookmarks = new List<BetterSceneBookmark>();
        [SerializeField] private List<BetterSceneLayerPreset> layerPresets = new List<BetterSceneLayerPreset>();

        internal static event Action Changed;

        internal static bool OverlayVisible { get => instance.overlayVisible; set => Set(ref instance.overlayVisible, value); }
        internal static bool DrawSelectionBounds { get => instance.drawSelectionBounds; set => Set(ref instance.drawSelectionBounds, value); }
        internal static bool DrawPivot { get => instance.drawPivot; set => Set(ref instance.drawPivot, value); }
        internal static bool DrawDiagnostics { get => instance.drawDiagnostics; set => Set(ref instance.drawDiagnostics, value); }
        internal static bool AlignToSurface { get => instance.alignToSurface; set => Set(ref instance.alignToSurface, value); }
        internal static bool ParentToSurface { get => instance.parentToSurface; set => Set(ref instance.parentToSurface, value); }
        internal static bool KeepPlacing { get => instance.keepPlacing; set => Set(ref instance.keepPlacing, value); }
        internal static bool IncludeDescendants { get => instance.includeDescendants; set => Set(ref instance.includeDescendants, value); }
        internal static bool GroundToZeroWhenNoSurface { get => instance.groundToZeroWhenNoSurface; set => Set(ref instance.groundToZeroWhenNoSurface, value); }
        internal static float ScatterRadius { get => instance.scatterRadius; set => Set(ref instance.scatterRadius, Mathf.Max(0f, value)); }
        internal static float ScatterHeight { get => instance.scatterHeight; set => Set(ref instance.scatterHeight, Mathf.Max(0f, value)); }
        internal static int ScatterSeed { get => instance.scatterSeed; set => Set(ref instance.scatterSeed, value); }
        internal static IReadOnlyList<BetterSceneBookmark> Bookmarks => instance.bookmarks;
        internal static IReadOnlyList<BetterSceneLayerPreset> LayerPresets => instance.layerPresets;

        internal static UnityEngine.Object PlacementAsset
        {
            get
            {
                return string.IsNullOrEmpty(instance.placementAssetGuid)
                    ? null
                    : AssetDatabase.LoadMainAssetAtPath(
                        AssetDatabase.GUIDToAssetPath(instance.placementAssetGuid));
            }
            set
            {
                string path = value == null ? string.Empty : AssetDatabase.GetAssetPath(value);
                string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                if (string.Equals(instance.placementAssetGuid, guid, StringComparison.Ordinal)) return;
                instance.placementAssetGuid = guid;
                SaveAndNotify();
            }
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
