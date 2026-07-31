using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    public static class DansToolboxMotion
    {
        private const double RevealDuration = 0.28d;

        public static bool DrawWindowReveal(Rect rect, double startedAt)
        {
            float progress = Mathf.Clamp01((float)(
                (EditorApplication.timeSinceStartup - startedAt) / RevealDuration));
            if (progress >= 1f)
            {
                return false;
            }

            float inverse = 1f - progress;
            float eased = 1f - inverse * inverse * inverse;
            float coverWidth = rect.width * (1f - eased);
            DansToolboxPalette palette = DansToolboxTheme.Current;
            Rect cover = new Rect(rect.xMax - coverWidth, rect.y, coverWidth, rect.height);
            EditorGUI.DrawRect(cover, palette.Canvas);

            float edgeX = Mathf.Max(rect.x, cover.x - 2f);
            EditorGUI.DrawRect(new Rect(edgeX, rect.y, 2f, rect.height), palette.Accent);
            for (int index = 0; index < 7; index++)
            {
                float phase = Mathf.Repeat(progress * 1.4f + index * 0.173f, 1f);
                float y = Mathf.Lerp(rect.y + 10f, rect.yMax - 10f, phase);
                Color color = index % 3 == 0 ? palette.Signal : palette.Accent;
                color.a *= Mathf.Sin(phase * Mathf.PI) * 0.78f;
                EditorGUI.DrawRect(new Rect(edgeX - 5f - index % 2 * 4f, y, 3f, 3f), color);
            }

            return true;
        }
    }
}
