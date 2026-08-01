using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    public static class DansToolboxMotion
    {
        internal const double RevealDuration = 1.35d;
        private const int RevealBands = 4;

        public static bool DrawWindowReveal(Rect rect, double startedAt)
        {
            float progress = Mathf.Clamp01((float)(
                (EditorApplication.timeSinceStartup - startedAt) / RevealDuration));
            if (progress >= 1f)
            {
                return false;
            }

            DansToolboxPalette palette = DansToolboxTheme.Current;
            float bandHeight = rect.height / RevealBands;
            for (int index = 0; index < RevealBands; index++)
            {
                float bandProgress = CalculateRevealBandProgress(progress, index);
                float coverWidth = rect.width * (1f - EaseOutQuint(bandProgress));
                float y = rect.y + bandHeight * index;
                float height = index == RevealBands - 1 ? rect.yMax - y : bandHeight + 1f;
                Rect cover = new Rect(rect.xMax - coverWidth, y, coverWidth, height);
                EditorGUI.DrawRect(cover, palette.Canvas);

                if (coverWidth > 0.5f)
                {
                    float edgeX = Mathf.Max(rect.x, cover.x - 2f);
                    Color edge = index % 2 == 0 ? palette.Accent : palette.Signal;
                    edge.a = Mathf.Lerp(0.9f, 0.25f, bandProgress);
                    EditorGUI.DrawRect(new Rect(edgeX, y, 2f, height), edge);
                }
            }

            float scanProgress = Mathf.Clamp01((progress - 0.08f) / 0.78f);
            float scanX = Mathf.Lerp(rect.x, rect.xMax, EaseOutCubic(scanProgress));
            Color scan = palette.Signal;
            scan.a = Mathf.Sin(scanProgress * Mathf.PI) * 0.4f;
            EditorGUI.DrawRect(new Rect(scanX, rect.y, 1f, rect.height), scan);

            return true;
        }

        internal static float CalculateRevealBandProgress(float progress, int bandIndex)
        {
            float delay = Mathf.Clamp(bandIndex, 0, RevealBands - 1) * 0.075f;
            return Mathf.Clamp01((Mathf.Clamp01(progress) - delay) / (0.82f - delay));
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseOutQuint(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse * inverse * inverse;
        }
    }
}
