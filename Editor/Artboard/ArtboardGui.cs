using System.Collections.Generic;
using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.Artboard
{
    internal static class ArtboardGui
    {
        private static GUIStyle label;
        private static GUIStyle muted;
        private static GUIStyle section;
        private static GUIStyle button;
        private static GUIStyle centered;
        private static readonly Dictionary<ulong, Texture2D> checkerTextures = new Dictionary<ulong, Texture2D>();
        private static readonly Dictionary<int, Texture2D> dotTextures = new Dictionary<int, Texture2D>();
        private static int revision = -1;

        internal static GUIStyle Label { get { Ensure(); return label; } }
        internal static GUIStyle Muted { get { Ensure(); return muted; } }
        internal static GUIStyle Section { get { Ensure(); return section; } }
        internal static GUIStyle Centered { get { Ensure(); return centered; } }

        internal static void Panel(Rect rect, DansToolboxPalette palette, bool raised = false)
        {
            EditorGUI.DrawRect(rect, raised ? palette.Raised : palette.Panel);
            Border(rect, palette.Border);
        }

        internal static void Border(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        internal static bool Button(Rect rect, string text, string tooltip, bool active = false, bool primary = false)
        {
            Ensure();
            DansToolboxPalette palette = DansToolboxTheme.Current;
            bool hover = rect.Contains(Event.current.mousePosition);
            Color fill = active ? palette.AccentSoft : primary ? palette.Accent : hover ? palette.Hover : palette.Raised;
            Color outline = active || primary ? palette.AccentHover : palette.Border;
            EditorGUI.DrawRect(rect, fill);
            Border(rect, outline);
            Color previous = GUI.contentColor;
            GUI.contentColor = primary ? Color.white : palette.Text;
            bool clicked = GUI.Button(rect, new GUIContent(text, tooltip), button);
            GUI.contentColor = previous;
            return clicked;
        }

        internal static void Checker(Rect rect, float size, Color a, Color b)
        {
            size = Mathf.Max(2f, size);
            Texture2D texture = GetCheckerTexture(a, b);
            GUI.DrawTextureWithTexCoords(rect, texture,
                new Rect(0f, 0f, rect.width / (size * 2f), rect.height / (size * 2f)));
        }

        internal static void DotPattern(Rect rect, float spacing, Color color)
        {
            int size = Mathf.Max(2, Mathf.RoundToInt(spacing));
            Color32 byteColor = color;
            int key = size * 397 ^ Pack(byteColor);
            if (!dotTextures.TryGetValue(key, out Texture2D texture) || texture == null)
            {
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat,
                    name = "Artboard Dot Pattern"
                };
                Color32[] pixels = new Color32[size * size];
                pixels[0] = byteColor;
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                dotTextures[key] = texture;
            }
            GUI.DrawTextureWithTexCoords(rect, texture,
                new Rect(0f, 0f, rect.width / size, rect.height / size));
        }

        internal static void SectionLabel(Rect rect, string text)
        {
            GUI.Label(rect, text.ToUpperInvariant(), Section);
        }

        private static void Ensure()
        {
            if (revision == DansToolboxTheme.Revision && label != null) return;
            revision = DansToolboxTheme.Revision;
            DansToolboxPalette palette = DansToolboxTheme.Current;
            label = new GUIStyle(EditorStyles.label) { fontSize = 11, normal = { textColor = palette.Text } };
            muted = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10, normal = { textColor = palette.Muted }, wordWrap = true };
            section = new GUIStyle(EditorStyles.miniBoldLabel) { fontSize = 9, normal = { textColor = palette.Muted } };
            button = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter, fontSize = 10, fontStyle = FontStyle.Bold };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }

        private static Texture2D GetCheckerTexture(Color a, Color b)
        {
            Color32 first = a;
            Color32 second = b;
            ulong key = ((ulong)(uint)Pack(first) << 32) | (uint)Pack(second);
            if (checkerTextures.TryGetValue(key, out Texture2D texture) && texture != null) return texture;
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                name = "Artboard Checker Pattern"
            };
            texture.SetPixels32(new[] { first, second, second, first });
            texture.Apply(false, true);
            checkerTextures[key] = texture;
            return texture;
        }

        private static int Pack(Color32 color)
        {
            return color.r | color.g << 8 | color.b << 16 | color.a << 24;
        }
    }
}
