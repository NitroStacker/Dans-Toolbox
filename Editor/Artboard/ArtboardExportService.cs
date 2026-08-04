using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.Artboard
{
    internal readonly struct ArtboardSheet
    {
        internal ArtboardSheet(Color32[] pixels, int width, int height, int frameWidth, int frameHeight, int columns)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            Columns = columns;
        }

        internal Color32[] Pixels { get; }
        internal int Width { get; }
        internal int Height { get; }
        internal int FrameWidth { get; }
        internal int FrameHeight { get; }
        internal int Columns { get; }
    }

    internal static class ArtboardExportService
    {
        internal const int MaxOutputDimension = 16384;

        internal static bool CanExport(ArtboardAsset asset, int scale, bool sheet, out string reason)
        {
            if (asset == null)
            {
                reason = "Select an Artboard document first.";
                return false;
            }
            scale = Mathf.Max(1, scale);
            int frameWidth = asset.Width * scale;
            int frameHeight = asset.Height * scale;
            if (frameWidth > MaxOutputDimension || frameHeight > MaxOutputDimension)
            {
                reason = $"The scaled frame would be {frameWidth} x {frameHeight}. Keep each dimension at or below {MaxOutputDimension}.";
                return false;
            }
            if (sheet)
            {
                CalculateGrid(asset.Frames.Count, out int columns, out int rows);
                if ((long)frameWidth * columns > MaxOutputDimension || (long)frameHeight * rows > MaxOutputDimension)
                {
                    reason = $"The sprite sheet would exceed {MaxOutputDimension} px. Reduce the scale or export individual frames.";
                    return false;
                }
            }
            reason = string.Empty;
            return true;
        }

        internal static string ExportFrame(
            ArtboardAsset asset,
            int frameIndex,
            int scale,
            Func<int, int, Color32[]> pixelsForCel)
        {
            if (!CanExport(asset, scale, false, out string reason))
            {
                EditorUtility.DisplayDialog("Artboard Export", reason, "OK");
                return string.Empty;
            }
            string path = EditorUtility.SaveFilePanelInProject(
                "Export Sprite",
                Sanitize(asset.name) + "_frame_" + (frameIndex + 1),
                "png",
                "Choose where to save the crisp sprite PNG.");
            if (string.IsNullOrEmpty(path)) return string.Empty;
            Color32[] composite = ArtboardPixelEngine.Composite(asset, frameIndex, pixelsForCel, true);
            Color32[] scaled = ArtboardPixelEngine.ScaleNearest(composite, asset.Width, asset.Height, scale);
            WritePng(path, scaled, asset.Width * scale, asset.Height * scale);
            ConfigureSprite(path, asset.PixelsPerUnit * scale, false, null);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return path;
        }

        internal static string ExportSheet(
            ArtboardAsset asset,
            int scale,
            Func<int, int, Color32[]> pixelsForCel,
            bool createAnimationClip)
        {
            if (!CanExport(asset, scale, true, out string reason))
            {
                EditorUtility.DisplayDialog("Artboard Export", reason, "OK");
                return string.Empty;
            }
            string path = EditorUtility.SaveFilePanelInProject(
                "Export Sprite Sheet",
                Sanitize(asset.name) + "_sheet",
                "png",
                "Choose where to save the sprite sheet and its sliced sprites.");
            if (string.IsNullOrEmpty(path)) return string.Empty;
            List<Color32[]> frames = new List<Color32[]>(asset.Frames.Count);
            for (int i = 0; i < asset.Frames.Count; i++)
            {
                Color32[] composite = ArtboardPixelEngine.Composite(asset, i, pixelsForCel, true);
                frames.Add(ArtboardPixelEngine.ScaleNearest(composite, asset.Width, asset.Height, scale));
            }
            ArtboardSheet sheet = BuildSheet(frames, asset.Width * scale, asset.Height * scale);
            WritePng(path, sheet.Pixels, sheet.Width, sheet.Height);
            ConfigureSprite(path, asset.PixelsPerUnit * scale, true, BuildSlices(asset, sheet));
            if (createAnimationClip && asset.Frames.Count > 1) CreateAnimationClip(asset, path);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return path;
        }

        internal static ArtboardSheet BuildSheet(IReadOnlyList<Color32[]> frames, int frameWidth, int frameHeight)
        {
            if (frames == null || frames.Count == 0) throw new ArgumentException("At least one frame is required.", nameof(frames));
            CalculateGrid(frames.Count, out int columns, out int rows);
            int width = checked(frameWidth * columns);
            int height = checked(frameHeight * rows);
            Color32[] output = new Color32[checked(width * height)];
            for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                Color32[] source = frames[frameIndex];
                if (source == null || source.Length != frameWidth * frameHeight)
                    throw new ArgumentException("Every frame must match the requested dimensions.", nameof(frames));
                int column = frameIndex % columns;
                int visualRow = frameIndex / columns;
                int row = rows - 1 - visualRow;
                for (int y = 0; y < frameHeight; y++)
                {
                    int sourceOffset = y * frameWidth;
                    int destinationOffset = (row * frameHeight + y) * width + column * frameWidth;
                    Array.Copy(source, sourceOffset, output, destinationOffset, frameWidth);
                }
            }
            return new ArtboardSheet(output, width, height, frameWidth, frameHeight, columns);
        }

        private static void WritePng(string assetPath, Color32[] pixels, int width, int height)
        {
            byte[] png = ArtboardPixelEngine.Encode(pixels, width, height);
            string absolute = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? Application.dataPath);
            File.WriteAllBytes(absolute, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureSprite(string path, int pixelsPerUnit, bool multiple, SpriteMetaData[] slices)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = multiple ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Mathf.Max(1, pixelsPerUnit);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            if (multiple && slices != null)
            {
#pragma warning disable 0618
                importer.spritesheet = slices;
#pragma warning restore 0618
            }
            importer.SaveAndReimport();
        }

        private static SpriteMetaData[] BuildSlices(ArtboardAsset asset, ArtboardSheet sheet)
        {
            CalculateGrid(asset.Frames.Count, out int columns, out int rows);
            SpriteMetaData[] slices = new SpriteMetaData[asset.Frames.Count];
            string baseName = Sanitize(asset.name);
            for (int i = 0; i < slices.Length; i++)
            {
                int column = i % columns;
                int visualRow = i / columns;
                int row = rows - 1 - visualRow;
                slices[i] = new SpriteMetaData
                {
                    name = baseName + "_" + i.ToString("D3"),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    rect = new Rect(column * sheet.FrameWidth, row * sheet.FrameHeight, sheet.FrameWidth, sheet.FrameHeight),
                    border = Vector4.zero
                };
            }
            return slices;
        }

        private static void CreateAnimationClip(ArtboardAsset asset, string sheetPath)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToArray();
            if (sprites.Length != asset.Frames.Count) return;
            AnimationClip clip = new AnimationClip { frameRate = asset.FramesPerSecond, name = asset.name };
            List<ObjectReferenceKeyframe> keys = new List<ObjectReferenceKeyframe>();
            float time = 0f;
            for (int i = 0; i < sprites.Length; i++)
            {
                keys.Add(new ObjectReferenceKeyframe { time = time, value = sprites[i] });
                time += asset.Frames[i].Hold / (float)asset.FramesPerSecond;
            }
            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
            string clipPath = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(sheetPath, ".anim"));
            AssetDatabase.CreateAsset(clip, clipPath);
            AssetDatabase.SaveAssets();
        }

        private static void CalculateGrid(int count, out int columns, out int rows)
        {
            columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, count))));
            rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
        }

        private static string Sanitize(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Artboard" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) result = result.Replace(invalid, '_');
            return result;
        }
    }
}
