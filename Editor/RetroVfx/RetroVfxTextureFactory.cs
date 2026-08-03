using DansToolbox.RetroVfx;
using UnityEngine;

namespace DansToolbox.EditorTools.RetroVfx
{
    internal readonly struct RetroVfxTextureSheet
    {
        internal RetroVfxTextureSheet(Texture2D texture, int columns, int rows)
        {
            Texture = texture;
            Columns = columns;
            Rows = rows;
        }

        internal Texture2D Texture { get; }
        internal int Columns { get; }
        internal int Rows { get; }
        internal bool Animated => Columns * Rows > 1;
    }

    internal static class RetroVfxTextureFactory
    {
        internal static RetroVfxTextureSheet Create(RetroVfxLayer layer, int seed)
        {
            return Create(null, layer, seed);
        }

        internal static RetroVfxTextureSheet Create(RetroVfxRecipe recipe, RetroVfxLayer layer, int seed)
        {
            RetroVfxSpriteStyle style = ResolveStyle(layer);
            int frames = FrameCount(style);
            int frameSize = FrameSize(recipe == null ? RetroVfxArtStyle.Pixel16 : recipe.artStyle);
            bool pixel = IsPixelStyle(style) || recipe != null &&
                         (recipe.artStyle == RetroVfxArtStyle.Pixel8 || recipe.artStyle == RetroVfxArtStyle.Pixel16);
            Texture2D texture = new Texture2D(frameSize * frames, frameSize, TextureFormat.RGBA32, false, true)
            {
                name = "Retro VFX " + style + (frames > 1 ? " Sheet" : " Texture"),
                wrapMode = TextureWrapMode.Clamp,
                filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[texture.width * texture.height];
            for (int frame = 0; frame < frames; frame++)
            {
                float time = frames <= 1 ? 0f : frame / (frames - 1f);
                for (int y = 0; y < frameSize; y++)
                {
                    for (int x = 0; x < frameSize; x++)
                    {
                        float nx;
                        float ny;
                        if (pixel)
                        {
                            int block = recipe != null && recipe.artStyle == RetroVfxArtStyle.Pixel8 ? 4 : 2;
                            nx = ((x / block * block + block * 0.5f) / frameSize) * 2f - 1f;
                            ny = ((y / block * block + block * 0.5f) / frameSize) * 2f - 1f;
                        }
                        else
                        {
                            nx = (x + 0.5f) / frameSize * 2f - 1f;
                            ny = (y + 0.5f) / frameSize * 2f - 1f;
                        }

                        float warp = (ValueNoise(nx * 3.1f, ny * 3.1f, seed) - 0.5f) *
                                     (style == RetroVfxSpriteStyle.PixelSmoke || style == RetroVfxSpriteStyle.PixelExplosion ? 0.1f : 0.025f);
                        float alpha = Sample(style, nx + warp, ny - warp * 0.7f, time, seed + frame * 977);
                        if (pixel)
                        {
                            alpha = alpha > 0.58f ? 1f : alpha > 0.18f ? 0.55f : 0f;
                        }
                        else if (recipe != null && recipe.artStyle == RetroVfxArtStyle.StylizedToon)
                        {
                            alpha = Mathf.Round(alpha * 3f) / 3f;
                        }
                        int sheetX = frame * frameSize + x;
                        pixels[y * texture.width + sheetX] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return new RetroVfxTextureSheet(texture, frames, 1);
        }

        internal static RetroVfxSpriteStyle ResolveStyle(RetroVfxLayer layer)
        {
            if (layer.spriteStyle != RetroVfxSpriteStyle.Auto)
            {
                return layer.spriteStyle;
            }

            return layer.kind switch
            {
                RetroVfxLayerKind.Flash => RetroVfxSpriteStyle.Starburst,
                RetroVfxLayerKind.Sparks => RetroVfxSpriteStyle.Spark,
                RetroVfxLayerKind.Ring => RetroVfxSpriteStyle.Ring,
                RetroVfxLayerKind.Smoke => RetroVfxSpriteStyle.SoftDisc,
                RetroVfxLayerKind.Debris => RetroVfxSpriteStyle.PixelChunk,
                RetroVfxLayerKind.Trail => RetroVfxSpriteStyle.Spark,
                RetroVfxLayerKind.Arc => RetroVfxSpriteStyle.SlashArc,
                RetroVfxLayerKind.Splat => RetroVfxSpriteStyle.BloodSplat,
                RetroVfxLayerKind.Aura => RetroVfxSpriteStyle.Glint,
                RetroVfxLayerKind.Beam => RetroVfxSpriteStyle.Beam,
                _ => RetroVfxSpriteStyle.SoftDisc
            };
        }

        private static int FrameCount(RetroVfxSpriteStyle style)
        {
            return style switch
            {
                RetroVfxSpriteStyle.PixelExplosion => 8,
                RetroVfxSpriteStyle.PixelSmoke => 8,
                RetroVfxSpriteStyle.BloodSplat => 5,
                RetroVfxSpriteStyle.MuzzleFlash => 4,
                RetroVfxSpriteStyle.SlashArc => 5,
                RetroVfxSpriteStyle.Crescent => 5,
                RetroVfxSpriteStyle.Shockwave => 4,
                RetroVfxSpriteStyle.Glint => 4,
                _ => 1
            };
        }

        private static int FrameSize(RetroVfxArtStyle artStyle)
        {
            return artStyle switch
            {
                RetroVfxArtStyle.Pixel8 => 32,
                RetroVfxArtStyle.Pixel16 => 64,
                RetroVfxArtStyle.Crisp2D => 128,
                RetroVfxArtStyle.StylizedToon => 128,
                RetroVfxArtStyle.SoftMagic => 128,
                _ => 96
            };
        }

        private static bool IsPixelStyle(RetroVfxSpriteStyle style)
        {
            return style == RetroVfxSpriteStyle.PixelExplosion ||
                   style == RetroVfxSpriteStyle.PixelSmoke ||
                   style == RetroVfxSpriteStyle.PixelChunk ||
                   style == RetroVfxSpriteStyle.BloodSplat;
        }

        private static float Sample(RetroVfxSpriteStyle style, float x, float y, float time, int seed)
        {
            float distance = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x);
            switch (style)
            {
                case RetroVfxSpriteStyle.PixelExplosion:
                    return PixelExplosion(distance, angle, x, y, time, seed);
                case RetroVfxSpriteStyle.PixelSmoke:
                    return PixelSmoke(x, y, time, seed);
                case RetroVfxSpriteStyle.PixelChunk:
                    return Mathf.Abs(x) < 0.66f && Mathf.Abs(y) < 0.66f &&
                           !(Mathf.Abs(x) > 0.5f && Mathf.Abs(y) > 0.5f)
                        ? 1f
                        : 0f;
                case RetroVfxSpriteStyle.Spark:
                    return TaperedLine(x, y, 0.92f, 0.14f);
                case RetroVfxSpriteStyle.Starburst:
                    return Starburst(distance, angle, 8, 0.14f, 0.93f * Pulse(time));
                case RetroVfxSpriteStyle.Ring:
                    return Band(distance, 0.6f, 0.11f) * EdgeFade(distance, 0.9f, 1f);
                case RetroVfxSpriteStyle.Shockwave:
                    return Band(distance, Mathf.Lerp(0.18f, 0.78f, Smooth01(time)), Mathf.Lerp(0.13f, 0.045f, time)) * (1f - time * 0.65f);
                case RetroVfxSpriteStyle.SlashArc:
                    return Band(distance, 0.64f, Mathf.Lerp(0.18f, 0.07f, time)) *
                           SmoothWindow(angle, -1.18f, Mathf.Lerp(-0.9f, 1.18f, Smooth01(time)), 0.12f);
                case RetroVfxSpriteStyle.Crescent:
                    return Crescent(x, y) * Pulse(time);
                case RetroVfxSpriteStyle.BloodDrop:
                    return BloodDrop(x, y);
                case RetroVfxSpriteStyle.BloodSplat:
                    return BloodSplat(distance / Mathf.Lerp(0.22f, 1f, Smooth01(time)), angle, seed) * (1f - time * 0.12f);
                case RetroVfxSpriteStyle.MuzzleFlash:
                    return MuzzleFlash(x / Mathf.Lerp(0.35f, 1f, Pulse(time)), y) * (1f - time * 0.35f);
                case RetroVfxSpriteStyle.Glint:
                    return Glint(x / Mathf.Lerp(0.25f, 1f, Pulse(time)), y / Mathf.Lerp(0.25f, 1f, Pulse(time)));
                case RetroVfxSpriteStyle.Rune:
                    return Rune(distance, angle);
                case RetroVfxSpriteStyle.Leaf:
                    return Leaf(x, y);
                case RetroVfxSpriteStyle.Bubble:
                    return Band(distance, 0.58f, 0.08f);
                case RetroVfxSpriteStyle.Beam:
                    return TaperedLine(x, y, 0.98f, 0.2f);
                default:
                    return Mathf.Pow(Mathf.Clamp01(1f - distance), 1.65f);
            }
        }

        private static float PixelExplosion(float distance, float angle, float x, float y, float time, int seed)
        {
            float grow = time < 0.46f
                ? Mathf.Lerp(0.18f, 0.96f, Smooth01(time / 0.46f))
                : Mathf.Lerp(0.96f, 0.7f, Smooth01((time - 0.46f) / 0.54f));
            float phase = seed * 0.0137f;
            float lobe = 0.78f + Mathf.Sin(angle * 5f + phase) * 0.16f +
                         Mathf.Sin(angle * 9f - phase * 1.7f) * 0.09f +
                         Mathf.Sin(angle * 3f + phase * 0.4f) * 0.07f;
            float cellular = Hash(Mathf.FloorToInt((x + 1f) * 8f), Mathf.FloorToInt((y + 1f) * 8f), seed);
            float boundary = grow * (lobe + (cellular - 0.5f) * 0.12f);
            if (distance > boundary)
            {
                return 0f;
            }

            if (time > 0.68f)
            {
                float hole = Mathf.InverseLerp(0.68f, 1f, time) * 0.42f;
                if (distance < hole && cellular > 0.48f)
                {
                    return 0f;
                }
            }
            return 1f;
        }

        private static float PixelSmoke(float x, float y, float time, int seed)
        {
            y += time * 0.18f;
            float alpha = 0f;
            for (int index = 0; index < 5; index++)
            {
                float angle = Hash(index, seed, 19) * Mathf.PI * 2f;
                float radius = 0.18f + Hash(index, seed, 23) * 0.2f;
                float cx = Mathf.Cos(angle) * (0.1f + time * 0.2f);
                float cy = Mathf.Sin(angle) * 0.16f + (index - 2) * 0.09f;
                float dx = x - cx;
                float dy = y - cy;
                alpha = Mathf.Max(alpha, dx * dx + dy * dy < radius * radius ? 1f : 0f);
            }
            return alpha;
        }

        private static float Starburst(float distance, float angle, int rays, float core, float reach)
        {
            float spoke = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * rays * 0.5f)), 14f);
            float radius = Mathf.Lerp(core, reach, spoke);
            return 1f - Mathf.SmoothStep(radius - 0.035f, radius + 0.035f, distance);
        }

        private static float Crescent(float x, float y)
        {
            float outer = Mathf.Sqrt(x * x + y * y);
            float inner = Mathf.Sqrt((x - 0.25f) * (x - 0.25f) + y * y);
            return outer < 0.76f && inner > 0.53f ? EdgeFade(outer, 0.68f, 0.78f) : 0f;
        }

        private static float BloodDrop(float x, float y)
        {
            float body = Mathf.Sqrt((x + 0.2f) * (x + 0.2f) + y * y);
            if (body < 0.48f)
            {
                return 1f;
            }
            float taper = Mathf.Lerp(0.24f, 0f, Mathf.InverseLerp(-0.02f, 0.94f, x));
            return x > -0.02f && x < 0.94f && Mathf.Abs(y) < taper ? 1f : 0f;
        }

        private static float BloodSplat(float distance, float angle, int seed)
        {
            float phase = seed * 0.017f;
            float arms = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(angle * 7f + phase)), 10f);
            float radius = 0.42f + arms * 0.48f + Mathf.Sin(angle * 11f - phase) * 0.05f;
            return distance < radius ? 1f : 0f;
        }

        private static float MuzzleFlash(float x, float y)
        {
            float core = Mathf.Abs(x) + Mathf.Abs(y) * 1.5f < 0.48f ? 1f : 0f;
            float forward = x > -0.18f && x < 0.98f && Mathf.Abs(y) < (1f - x) * 0.28f ? 1f : 0f;
            float upper = y > 0f && y < 0.72f && Mathf.Abs(x) < (0.72f - y) * 0.18f ? 1f : 0f;
            float lower = y < 0f && y > -0.72f && Mathf.Abs(x) < (0.72f + y) * 0.18f ? 1f : 0f;
            return Mathf.Max(core, Mathf.Max(forward, Mathf.Max(upper, lower)));
        }

        private static float Glint(float x, float y)
        {
            float horizontal = Mathf.Abs(y) < 0.065f * (1f - Mathf.Abs(x)) && Mathf.Abs(x) < 0.96f ? 1f : 0f;
            float vertical = Mathf.Abs(x) < 0.065f * (1f - Mathf.Abs(y)) && Mathf.Abs(y) < 0.96f ? 1f : 0f;
            float diamond = Mathf.Abs(x) + Mathf.Abs(y) < 0.28f ? 1f : 0f;
            return Mathf.Max(diamond, Mathf.Max(horizontal, vertical));
        }

        private static float Rune(float distance, float angle)
        {
            float ring = Band(distance, 0.58f, 0.055f);
            float ticks = Band(distance, 0.76f, 0.09f) *
                          (Mathf.Abs(Mathf.Sin(angle * 6f)) < 0.16f ? 1f : 0f);
            return Mathf.Max(ring, ticks);
        }

        private static float Leaf(float x, float y)
        {
            float rotatedX = (x + y) * 0.7071067f;
            float rotatedY = (y - x) * 0.7071067f;
            float body = rotatedX * rotatedX / 0.76f + rotatedY * rotatedY / 0.12f;
            float vein = Mathf.Abs(rotatedY) < 0.025f && Mathf.Abs(rotatedX) < 0.62f ? 0.75f : 0f;
            return body < 1f ? Mathf.Max(0.85f, vein) : 0f;
        }

        private static float TaperedLine(float x, float y, float length, float halfWidth)
        {
            if (Mathf.Abs(x) > length)
            {
                return 0f;
            }
            float width = halfWidth * (1f - Mathf.Abs(x) / length);
            return 1f - Mathf.SmoothStep(width * 0.65f, width, Mathf.Abs(y));
        }

        private static float Band(float value, float center, float width)
        {
            return 1f - Mathf.SmoothStep(width * 0.55f, width, Mathf.Abs(value - center));
        }

        private static float EdgeFade(float value, float start, float end)
        {
            return 1f - Mathf.SmoothStep(start, end, value);
        }

        private static float SmoothWindow(float value, float minimum, float maximum, float feather)
        {
            return Mathf.SmoothStep(minimum - feather, minimum + feather, value) *
                   (1f - Mathf.SmoothStep(maximum - feather, maximum + feather, value));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float Pulse(float time)
        {
            if (time <= 0f)
            {
                return 1f;
            }
            return Mathf.Sin(Mathf.Clamp01(time) * Mathf.PI);
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = Smooth01(x - x0);
            float ty = Smooth01(y - y0);
            float a = Hash(x0, y0, seed);
            float b = Hash(x0 + 1, y0, seed);
            float c = Hash(x0, y0 + 1, seed);
            float d = Hash(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                value = (value ^ (value >> 13)) * 1274126177u;
                value ^= value >> 16;
                return (value & 0x00ffffff) / 16777215f;
            }
        }
    }
}
