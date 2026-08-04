using System;
using System.Collections.Generic;
using UnityEngine;

namespace DansToolbox.EditorTools.Artboard
{
    internal static class ArtboardPixelEngine
    {
        internal static Color32[] Blank(int width, int height)
        {
            return new Color32[Mathf.Max(1, width * height)];
        }

        internal static Color32[] Decode(byte[] png, int width, int height)
        {
            if (png == null || png.Length == 0) return Blank(width, height);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!texture.LoadImage(png, false) || texture.width != width || texture.height != height)
                    return Blank(width, height);
                return texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        internal static byte[] Encode(Color32[] pixels, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        internal static Color32[] Composite(
            ArtboardAsset asset,
            int frameIndex,
            Func<int, int, Color32[]> pixelsForCel,
            bool includeBackground)
        {
            int length = asset.Width * asset.Height;
            Color32[] output = new Color32[length];
            if (includeBackground && !asset.Transparent)
            {
                for (int i = 0; i < length; i++) output[i] = asset.Background;
            }

            for (int layerIndex = 0; layerIndex < asset.Layers.Count; layerIndex++)
            {
                ArtboardLayer layer = asset.Layers[layerIndex];
                if (!layer.Visible || layer.Opacity <= 0f) continue;
                Color32[] source = pixelsForCel(frameIndex, layerIndex);
                if (source == null || source.Length != length) continue;
                BlendOver(output, source, layer.Opacity);
            }
            return output;
        }

        internal static void BlendOver(Color32[] destination, Color32[] source, float opacity)
        {
            int count = Mathf.Min(destination.Length, source.Length);
            for (int i = 0; i < count; i++)
            {
                Color32 src = source[i];
                float sa = src.a / 255f * opacity;
                if (sa <= 0f) continue;
                Color32 dst = destination[i];
                float da = dst.a / 255f;
                float oa = sa + da * (1f - sa);
                if (oa <= 0.0001f)
                {
                    destination[i] = default;
                    continue;
                }
                destination[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt((src.r * sa + dst.r * da * (1f - sa)) / oa), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt((src.g * sa + dst.g * da * (1f - sa)) / oa), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt((src.b * sa + dst.b * da * (1f - sa)) / oa), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(oa * 255f), 0, 255));
            }
        }

        internal static void DrawStroke(
            Color32[] pixels, int width, int height, Vector2Int from, Vector2Int to,
            Color32 color, int size, bool erase, bool circular, bool mirrorX, bool mirrorY)
        {
            int dx = Mathf.Abs(to.x - from.x);
            int dy = Mathf.Abs(to.y - from.y);
            int steps = Mathf.Max(1, Mathf.Max(dx, dy));
            for (int i = 0; i <= steps; i++)
            {
                Vector2Int point = new Vector2Int(
                    Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, i / (float)steps)),
                    Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, i / (float)steps)));
                StampMirrored(pixels, width, height, point, color, size, erase, circular, mirrorX, mirrorY);
            }
        }

        internal static void DrawLine(
            Color32[] pixels, int width, int height, Vector2Int from, Vector2Int to,
            Color32 color, int size, bool erase, bool mirrorX, bool mirrorY)
        {
            DrawStroke(pixels, width, height, from, to, color, size, erase, false, mirrorX, mirrorY);
        }

        internal static void DrawRectangle(
            Color32[] pixels, int width, int height, RectInt rect, Color32 color, int size,
            bool filled, bool erase)
        {
            RectInt r = Normalize(rect);
            if (filled)
            {
                for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++) Set(pixels, width, height, x, y, color, erase);
                return;
            }
            DrawLine(pixels, width, height, new Vector2Int(r.xMin, r.yMin), new Vector2Int(r.xMax - 1, r.yMin), color, size, erase, false, false);
            DrawLine(pixels, width, height, new Vector2Int(r.xMax - 1, r.yMin), new Vector2Int(r.xMax - 1, r.yMax - 1), color, size, erase, false, false);
            DrawLine(pixels, width, height, new Vector2Int(r.xMax - 1, r.yMax - 1), new Vector2Int(r.xMin, r.yMax - 1), color, size, erase, false, false);
            DrawLine(pixels, width, height, new Vector2Int(r.xMin, r.yMax - 1), new Vector2Int(r.xMin, r.yMin), color, size, erase, false, false);
        }

        internal static void DrawEllipse(
            Color32[] pixels, int width, int height, RectInt rect, Color32 color, int size,
            bool filled, bool erase)
        {
            RectInt r = Normalize(rect);
            float rx = Mathf.Max(0.5f, r.width * 0.5f);
            float ry = Mathf.Max(0.5f, r.height * 0.5f);
            float cx = r.xMin + rx - 0.5f;
            float cy = r.yMin + ry - 0.5f;
            if (filled)
            {
                for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++)
                    {
                        float nx = (x - cx) / rx;
                        float ny = (y - cy) / ry;
                        if (nx * nx + ny * ny <= 1f) Set(pixels, width, height, x, y, color, erase);
                    }
                return;
            }
            int samples = Mathf.Max(16, Mathf.CeilToInt(Mathf.PI * Mathf.Max(rx, ry) * 2f));
            Vector2Int previous = new Vector2Int(Mathf.RoundToInt(cx + rx), Mathf.RoundToInt(cy));
            for (int i = 1; i <= samples; i++)
            {
                float angle = i / (float)samples * Mathf.PI * 2f;
                Vector2Int point = new Vector2Int(
                    Mathf.RoundToInt(cx + Mathf.Cos(angle) * rx),
                    Mathf.RoundToInt(cy + Mathf.Sin(angle) * ry));
                DrawLine(pixels, width, height, previous, point, color, size, erase, false, false);
                previous = point;
            }
        }

        internal static int FloodFill(Color32[] pixels, int width, int height, int x, int y, Color32 replacement)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return 0;
            int start = y * width + x;
            Color32 target = pixels[start];
            if (Equal(target, replacement)) return 0;
            Queue<int> queue = new Queue<int>();
            bool[] visited = new bool[pixels.Length];
            queue.Enqueue(start);
            int changed = 0;
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                if (visited[index]) continue;
                visited[index] = true;
                if (!Equal(pixels[index], target)) continue;
                pixels[index] = replacement;
                changed++;
                int px = index % width;
                int py = index / width;
                if (px > 0) queue.Enqueue(index - 1);
                if (px + 1 < width) queue.Enqueue(index + 1);
                if (py > 0) queue.Enqueue(index - width);
                if (py + 1 < height) queue.Enqueue(index + width);
            }
            return changed;
        }

        internal static Color32[] ScaleNearest(Color32[] source, int width, int height, int scale)
        {
            scale = Mathf.Max(1, scale);
            if (scale == 1) return (Color32[])source.Clone();
            int targetWidth = checked(width * scale);
            int targetHeight = checked(height * scale);
            Color32[] output = new Color32[checked(targetWidth * targetHeight)];
            for (int y = 0; y < targetHeight; y++)
            {
                int sourceRow = (y / scale) * width;
                int targetRow = y * targetWidth;
                for (int x = 0; x < targetWidth; x++) output[targetRow + x] = source[sourceRow + x / scale];
            }
            return output;
        }

        private static void StampMirrored(
            Color32[] pixels, int width, int height, Vector2Int point, Color32 color, int size,
            bool erase, bool circular, bool mirrorX, bool mirrorY)
        {
            Stamp(pixels, width, height, point.x, point.y, color, size, erase, circular);
            if (mirrorX) Stamp(pixels, width, height, width - 1 - point.x, point.y, color, size, erase, circular);
            if (mirrorY) Stamp(pixels, width, height, point.x, height - 1 - point.y, color, size, erase, circular);
            if (mirrorX && mirrorY) Stamp(pixels, width, height, width - 1 - point.x, height - 1 - point.y, color, size, erase, circular);
        }

        private static void Stamp(
            Color32[] pixels, int width, int height, int cx, int cy, Color32 color, int size, bool erase, bool circular)
        {
            size = Mathf.Max(1, size);
            int min = -(size / 2);
            int max = min + size - 1;
            float radius = size * 0.5f;
            for (int oy = min; oy <= max; oy++)
            {
                for (int ox = min; ox <= max; ox++)
                {
                    if (circular && size > 2 && new Vector2(ox + 0.5f, oy + 0.5f).sqrMagnitude > radius * radius) continue;
                    Set(pixels, width, height, cx + ox, cy + oy, color, erase);
                }
            }
        }

        private static void Set(Color32[] pixels, int width, int height, int x, int y, Color32 color, bool erase)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            pixels[y * width + x] = erase ? default : color;
        }

        private static RectInt Normalize(RectInt rect)
        {
            int xMin = Mathf.Min(rect.x, rect.x + rect.width);
            int yMin = Mathf.Min(rect.y, rect.y + rect.height);
            int xMax = Mathf.Max(rect.x, rect.x + rect.width);
            int yMax = Mathf.Max(rect.y, rect.y + rect.height);
            return new RectInt(xMin, yMin, Mathf.Max(1, xMax - xMin + 1), Mathf.Max(1, yMax - yMin + 1));
        }

        private static bool Equal(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }
    }
}
