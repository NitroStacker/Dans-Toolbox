using System;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterScene
{
    internal enum BetterSceneMode
    {
        Select,
        Place,
        Measure,
        Review
    }

    internal enum BetterSceneSnapMode
    {
        Free,
        Grid,
        Surface,
        Vertex
    }

    internal enum BetterSceneAxis
    {
        X,
        Y,
        Z
    }

    internal enum BetterSceneAlignAnchor
    {
        Minimum,
        Center,
        Maximum
    }

    internal enum BetterSceneVisibilityBand
    {
        All,
        Environment,
        Gameplay,
        Lighting,
        Audio,
        UI,
        Cameras,
        Debug
    }

    [Serializable]
    internal sealed class BetterSceneBookmark
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField] private string name = "VIEW";
        [SerializeField] private string scenePath = string.Empty;
        [SerializeField] private Vector3 pivot;
        [SerializeField] private Quaternion rotation = Quaternion.identity;
        [SerializeField] private float size = 10f;
        [SerializeField] private bool orthographic;
        [SerializeField] private bool in2DMode;

        internal string Id => id;
        internal string Name { get => name; set => name = value ?? string.Empty; }
        internal string ScenePath { get => scenePath; set => scenePath = value ?? string.Empty; }
        internal Vector3 Pivot { get => pivot; set => pivot = value; }
        internal Quaternion Rotation { get => rotation; set => rotation = value; }
        internal float Size { get => size; set => size = Mathf.Max(0.01f, value); }
        internal bool Orthographic { get => orthographic; set => orthographic = value; }
        internal bool In2DMode { get => in2DMode; set => in2DMode = value; }
    }

    [Serializable]
    internal sealed class BetterSceneLayerPreset
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField] private string name = "LAYERS";
        [SerializeField] private int visibleLayers = -1;
        [SerializeField] private int lockedLayers;

        internal string Id => id;
        internal string Name { get => name; set => name = value ?? string.Empty; }
        internal int VisibleLayers { get => visibleLayers; set => visibleLayers = value; }
        internal int LockedLayers { get => lockedLayers; set => lockedLayers = value; }
    }

    internal readonly struct BetterSceneMeasurement
    {
        internal BetterSceneMeasurement(Vector3 start, Vector3 end, bool hasStart, bool hasEnd)
        {
            Start = start;
            End = end;
            HasStart = hasStart;
            HasEnd = hasEnd;
        }

        internal Vector3 Start { get; }
        internal Vector3 End { get; }
        internal bool HasStart { get; }
        internal bool HasEnd { get; }
        internal float Distance => HasStart && HasEnd ? Vector3.Distance(Start, End) : 0f;
        internal Vector3 Delta => HasStart && HasEnd ? End - Start : Vector3.zero;
    }
}
