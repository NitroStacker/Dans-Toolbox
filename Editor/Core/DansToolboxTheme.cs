using System;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    public enum DansToolboxThemeId
    {
        SignalOrange,
        NeonCyan,
        ArcadeViolet
    }

    public readonly struct DansToolboxPalette
    {
        public DansToolboxPalette(
            Color canvas,
            Color panel,
            Color inset,
            Color raised,
            Color hover,
            Color border,
            Color borderStrong,
            Color text,
            Color muted,
            Color accent,
            Color accentHover,
            Color accentSoft,
            Color signal,
            Color success,
            Color warning,
            Color danger)
        {
            Canvas = canvas;
            Panel = panel;
            Inset = inset;
            Raised = raised;
            Hover = hover;
            Border = border;
            BorderStrong = borderStrong;
            Text = text;
            Muted = muted;
            Accent = accent;
            AccentHover = accentHover;
            AccentSoft = accentSoft;
            Signal = signal;
            Success = success;
            Warning = warning;
            Danger = danger;
        }

        public Color Canvas { get; }
        public Color Panel { get; }
        public Color Inset { get; }
        public Color Raised { get; }
        public Color Hover { get; }
        public Color Border { get; }
        public Color BorderStrong { get; }
        public Color Text { get; }
        public Color Muted { get; }
        public Color Accent { get; }
        public Color AccentHover { get; }
        public Color AccentSoft { get; }
        public Color Signal { get; }
        public Color Success { get; }
        public Color Warning { get; }
        public Color Danger { get; }
    }

    public static class DansToolboxTheme
    {
        public static event Action Changed;

        public static int Revision { get; private set; }

        public static DansToolboxPalette Current =>
            GetPalette(
                DansToolboxSettings.Theme,
                DansToolboxSettings.SeamlessToolSurfaces);

        public static string GetDisplayName(DansToolboxThemeId theme)
        {
            switch (theme)
            {
                case DansToolboxThemeId.NeonCyan:
                    return "Neon Cyan";
                case DansToolboxThemeId.ArcadeViolet:
                    return "Arcade Violet";
                default:
                    return "Signal Orange";
            }
        }

        public static DansToolboxPalette GetPalette(DansToolboxThemeId theme)
        {
            return GetPalette(theme, false);
        }

        internal static DansToolboxPalette GetPalette(
            DansToolboxThemeId theme,
            bool seamlessToolSurfaces)
        {
            DansToolboxPalette palette = GetBasePalette(theme);
            return seamlessToolSurfaces ? MakeSeamless(palette) : palette;
        }

        private static DansToolboxPalette GetBasePalette(DansToolboxThemeId theme)
        {
            switch (theme)
            {
                case DansToolboxThemeId.NeonCyan:
                    return new DansToolboxPalette(
                        new Color(0.07f, 0.095f, 0.115f),
                        new Color(0.11f, 0.145f, 0.165f),
                        new Color(0.055f, 0.073f, 0.088f),
                        new Color(0.155f, 0.195f, 0.215f),
                        new Color(0.19f, 0.255f, 0.275f),
                        new Color(0.21f, 0.335f, 0.37f),
                        new Color(0.31f, 0.51f, 0.56f),
                        new Color(0.84f, 0.925f, 0.94f),
                        new Color(0.49f, 0.64f, 0.665f),
                        new Color(0.08f, 0.82f, 0.88f),
                        new Color(0.35f, 0.96f, 1f),
                        new Color(0.035f, 0.47f, 0.53f),
                        new Color(0.39f, 1f, 0.89f),
                        new Color(0.35f, 0.83f, 0.6f),
                        new Color32(240, 180, 72, 255),
                        new Color32(235, 98, 105, 255));
                case DansToolboxThemeId.ArcadeViolet:
                    return new DansToolboxPalette(
                        new Color(0.105f, 0.08f, 0.125f),
                        new Color(0.16f, 0.125f, 0.18f),
                        new Color(0.082f, 0.06f, 0.1f),
                        new Color(0.21f, 0.165f, 0.235f),
                        new Color(0.265f, 0.205f, 0.295f),
                        new Color(0.325f, 0.26f, 0.365f),
                        new Color(0.5f, 0.395f, 0.56f),
                        new Color(0.92f, 0.865f, 0.95f),
                        new Color(0.635f, 0.545f, 0.69f),
                        new Color(0.76f, 0.38f, 1f),
                        new Color(0.9f, 0.62f, 1f),
                        new Color(0.425f, 0.19f, 0.58f),
                        new Color(1f, 0.48f, 0.8f),
                        new Color(0.48f, 0.82f, 0.58f),
                        new Color32(246, 188, 82, 255),
                        new Color32(240, 94, 124, 255));
                default:
                    return new DansToolboxPalette(
                        new Color(0.105f, 0.11f, 0.115f),
                        new Color(0.155f, 0.16f, 0.165f),
                        new Color(0.09f, 0.095f, 0.1f),
                        new Color(0.2f, 0.205f, 0.21f),
                        new Color(0.24f, 0.245f, 0.25f),
                        new Color(0.27f, 0.275f, 0.28f),
                        new Color(0.38f, 0.385f, 0.39f),
                        new Color(0.88f, 0.875f, 0.84f),
                        new Color(0.57f, 0.575f, 0.56f),
                        new Color(1f, 0.55f, 0.12f),
                        new Color(1f, 0.68f, 0.24f),
                        new Color(0.66f, 0.33f, 0.08f),
                        new Color(1f, 0.68f, 0.24f),
                        new Color(0.42f, 0.78f, 0.48f),
                        new Color32(240, 180, 72, 255),
                        new Color32(235, 98, 105, 255));
            }
        }

        private static DansToolboxPalette MakeSeamless(DansToolboxPalette palette)
        {
            Color surface = Color.Lerp(palette.Canvas, palette.Panel, 0.72f);
            Color inset = Color.Lerp(palette.Inset, surface, 0.12f);
            Color border = Color.Lerp(surface, palette.Border, 0.38f);
            Color borderStrong = Color.Lerp(surface, palette.BorderStrong, 0.62f);
            return new DansToolboxPalette(
                surface,
                surface,
                inset,
                palette.Raised,
                palette.Hover,
                border,
                borderStrong,
                palette.Text,
                palette.Muted,
                palette.Accent,
                palette.AccentHover,
                palette.AccentSoft,
                palette.Signal,
                palette.Success,
                palette.Warning,
                palette.Danger);
        }

        internal static void NotifyChanged()
        {
            Revision++;
            Changed?.Invoke();
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                window.Repaint();
            }
        }
    }
}
