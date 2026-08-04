using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.Artboard
{
    public enum ArtboardMode
    {
        DigitalArt,
        Animation,
        PixelArt
    }

    [Serializable]
    public sealed class ArtboardLayer
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string layerName = "Paint";
        [SerializeField] private bool visible = true;
        [SerializeField, Range(0f, 1f)] private float opacity = 1f;

        public string Id => id;
        public string Name { get => layerName; set => layerName = string.IsNullOrWhiteSpace(value) ? "Paint" : value.Trim(); }
        public bool Visible { get => visible; set => visible = value; }
        public float Opacity { get => opacity; set => opacity = Mathf.Clamp01(value); }

        internal static ArtboardLayer Create(string name)
        {
            return new ArtboardLayer { id = Guid.NewGuid().ToString("N"), layerName = name };
        }

        internal ArtboardLayer Duplicate()
        {
            return new ArtboardLayer
            {
                id = Guid.NewGuid().ToString("N"),
                layerName = layerName + " Copy",
                visible = visible,
                opacity = opacity
            };
        }

        internal void RepairId()
        {
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
        }
    }

    [Serializable]
    public sealed class ArtboardCel
    {
        [SerializeField] private string layerId = string.Empty;
        [SerializeField] private byte[] pngData = Array.Empty<byte>();

        public string LayerId => layerId;
        public byte[] PngData => pngData ?? Array.Empty<byte>();

        internal static ArtboardCel Create(string id, byte[] data = null)
        {
            return new ArtboardCel
            {
                layerId = id,
                pngData = data == null ? Array.Empty<byte>() : (byte[])data.Clone()
            };
        }

        internal void SetPixels(byte[] data)
        {
            pngData = data ?? Array.Empty<byte>();
        }
    }

    [Serializable]
    public sealed class ArtboardFrame
    {
        [SerializeField, Min(1)] private int hold = 1;
        [SerializeField] private List<ArtboardCel> cels = new List<ArtboardCel>();

        public int Hold { get => Mathf.Max(1, hold); set => hold = Mathf.Max(1, value); }
        public IReadOnlyList<ArtboardCel> Cels => cels;

        internal static ArtboardFrame Create(IReadOnlyList<ArtboardLayer> layers)
        {
            ArtboardFrame frame = new ArtboardFrame();
            foreach (ArtboardLayer layer in layers) frame.cels.Add(ArtboardCel.Create(layer.Id));
            return frame;
        }

        internal ArtboardCel GetCel(string layerId)
        {
            return cels.Find(cel => string.Equals(cel.LayerId, layerId, StringComparison.Ordinal));
        }

        internal void AddCel(string layerId, byte[] data = null)
        {
            cels.Add(ArtboardCel.Create(layerId, data));
        }

        internal void RemoveCel(string layerId)
        {
            cels.RemoveAll(cel => string.Equals(cel.LayerId, layerId, StringComparison.Ordinal));
        }

        internal ArtboardFrame Duplicate()
        {
            ArtboardFrame copy = new ArtboardFrame { hold = hold };
            foreach (ArtboardCel cel in cels) copy.cels.Add(ArtboardCel.Create(cel.LayerId, cel.PngData));
            return copy;
        }

        internal void EnsureCels(IReadOnlyList<ArtboardLayer> layers)
        {
            cels.RemoveAll(cel => cel == null || !ContainsLayer(layers, cel.LayerId));
            foreach (ArtboardLayer layer in layers)
            {
                if (GetCel(layer.Id) == null) AddCel(layer.Id);
            }
        }

        private static bool ContainsLayer(IReadOnlyList<ArtboardLayer> layers, string layerId)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (string.Equals(layers[i].Id, layerId, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }

    /// <summary>An editable, layered sprite and cel-animation document.</summary>
    public sealed class ArtboardAsset : ScriptableObject
    {
        public const int MinDimension = 8;
        public const int MaxDimension = 4096;

        [SerializeField] private int width = 256;
        [SerializeField] private int height = 256;
        [SerializeField] private ArtboardMode mode = ArtboardMode.PixelArt;
        [SerializeField, Range(1, 60)] private int framesPerSecond = 12;
        [SerializeField] private bool transparent = true;
        [SerializeField] private Color32 background = new Color32(32, 31, 29, 255);
        [SerializeField, Min(1)] private int pixelsPerUnit = 100;
        [SerializeField, Range(1, 16)] private int exportScale = 4;
        [SerializeField] private List<ArtboardLayer> layers = new List<ArtboardLayer>();
        [SerializeField] private List<ArtboardFrame> frames = new List<ArtboardFrame>();

        public int Width => width;
        public int Height => height;
        public ArtboardMode Mode => mode;
        public int FramesPerSecond { get => framesPerSecond; set => framesPerSecond = Mathf.Clamp(value, 1, 60); }
        public bool Transparent { get => transparent; set => transparent = value; }
        public Color32 Background { get => background; set => background = value; }
        public int PixelsPerUnit { get => pixelsPerUnit; set => pixelsPerUnit = Mathf.Max(1, value); }
        public int ExportScale { get => exportScale; set => exportScale = Mathf.Clamp(value, 1, 16); }
        public IReadOnlyList<ArtboardLayer> Layers => layers;
        public IReadOnlyList<ArtboardFrame> Frames => frames;

        public static ArtboardAsset CreateDocument(int documentWidth, int documentHeight, ArtboardMode documentMode)
        {
            ArtboardAsset asset = CreateInstance<ArtboardAsset>();
            asset.width = Mathf.Clamp(documentWidth, MinDimension, MaxDimension);
            asset.height = Mathf.Clamp(documentHeight, MinDimension, MaxDimension);
            asset.mode = documentMode;
            asset.exportScale = documentMode == ArtboardMode.PixelArt ? 4 : 1;
            asset.layers.Add(ArtboardLayer.Create("Paint 1"));
            asset.frames.Add(ArtboardFrame.Create(asset.layers));
            return asset;
        }

        public ArtboardCel GetCel(int frameIndex, int layerIndex)
        {
            EnsureIntegrity();
            return frames[Mathf.Clamp(frameIndex, 0, frames.Count - 1)]
                .GetCel(layers[Mathf.Clamp(layerIndex, 0, layers.Count - 1)].Id);
        }

        public void SetCelPixels(int frameIndex, int layerIndex, byte[] pngData)
        {
            GetCel(frameIndex, layerIndex).SetPixels(pngData);
        }

        public int AddLayer(int afterIndex)
        {
            EnsureIntegrity();
            int index = Mathf.Clamp(afterIndex + 1, 0, layers.Count);
            ArtboardLayer layer = ArtboardLayer.Create("Paint " + (layers.Count + 1));
            layers.Insert(index, layer);
            foreach (ArtboardFrame frame in frames) frame.AddCel(layer.Id);
            return index;
        }

        public int DuplicateLayer(int index)
        {
            EnsureIntegrity();
            index = Mathf.Clamp(index, 0, layers.Count - 1);
            ArtboardLayer source = layers[index];
            ArtboardLayer copy = source.Duplicate();
            layers.Insert(index + 1, copy);
            foreach (ArtboardFrame frame in frames)
            {
                frame.AddCel(copy.Id, frame.GetCel(source.Id)?.PngData);
            }
            return index + 1;
        }

        public int DeleteLayer(int index)
        {
            EnsureIntegrity();
            if (layers.Count <= 1) return 0;
            index = Mathf.Clamp(index, 0, layers.Count - 1);
            string id = layers[index].Id;
            layers.RemoveAt(index);
            foreach (ArtboardFrame frame in frames) frame.RemoveCel(id);
            return Mathf.Clamp(index - 1, 0, layers.Count - 1);
        }

        public void MoveLayer(int from, int to)
        {
            EnsureIntegrity();
            from = Mathf.Clamp(from, 0, layers.Count - 1);
            to = Mathf.Clamp(to, 0, layers.Count - 1);
            if (from == to) return;
            ArtboardLayer item = layers[from];
            layers.RemoveAt(from);
            layers.Insert(to, item);
        }

        public int AddFrame(int afterIndex, bool duplicate)
        {
            EnsureIntegrity();
            afterIndex = Mathf.Clamp(afterIndex, 0, frames.Count - 1);
            ArtboardFrame frame = duplicate ? frames[afterIndex].Duplicate() : ArtboardFrame.Create(layers);
            frames.Insert(afterIndex + 1, frame);
            return afterIndex + 1;
        }

        public int DeleteFrame(int index)
        {
            EnsureIntegrity();
            if (frames.Count <= 1) return 0;
            frames.RemoveAt(Mathf.Clamp(index, 0, frames.Count - 1));
            return Mathf.Clamp(index - 1, 0, frames.Count - 1);
        }

        public void EnsureIntegrity()
        {
            width = Mathf.Clamp(width, MinDimension, MaxDimension);
            height = Mathf.Clamp(height, MinDimension, MaxDimension);
            framesPerSecond = Mathf.Clamp(framesPerSecond, 1, 60);
            pixelsPerUnit = Mathf.Max(1, pixelsPerUnit);
            exportScale = Mathf.Clamp(exportScale, 1, 16);
            layers ??= new List<ArtboardLayer>();
            frames ??= new List<ArtboardFrame>();
            layers.RemoveAll(layer => layer == null);
            if (layers.Count == 0) layers.Add(ArtboardLayer.Create("Paint 1"));
            foreach (ArtboardLayer layer in layers) layer.RepairId();
            frames.RemoveAll(frame => frame == null);
            if (frames.Count == 0) frames.Add(ArtboardFrame.Create(layers));
            foreach (ArtboardFrame frame in frames) frame.EnsureCels(layers);
        }

        private void OnValidate()
        {
            EnsureIntegrity();
        }
    }

    internal static class ArtboardAssetFactory
    {
        [MenuItem("Assets/Create/Dans Toolbox/Artboard", false, 230)]
        private static void CreateAsset()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/New Artboard.asset");
            ArtboardAsset asset = ArtboardAsset.CreateDocument(256, 256, ArtboardMode.PixelArt);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            ArtboardWindow.OpenAsset(asset);
        }
    }
}
