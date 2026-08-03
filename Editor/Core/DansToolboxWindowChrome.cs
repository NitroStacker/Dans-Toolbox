using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    public static class DansToolboxWindowChrome
    {
        // Unity 6.3 adds eight logical pixels after this minimum. Thirty-four
        // therefore renders as a compact 42px tab for both a centered 16px
        // Unity icon and a numbered Native Dock badge.
        private const float CompactTabMinimumWidth = 34f;
        private static bool tabChromeRetryScheduled;

        static DansToolboxWindowChrome()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseTabChrome;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseTabChrome;
        }

        public static void ApplyCompactTitle(
            EditorWindow window,
            string toolId,
            string compactLabel = null,
            string tooltip = null)
        {
            if (window == null)
            {
                return;
            }

            EnsureCompactTabChrome();
            window.titleContent = CreateCompactTitle(
                toolId,
                compactLabel,
                tooltip);
        }

        internal static GUIContent CreateCompactTitle(
            string toolId,
            string compactLabel = null,
            string tooltip = null)
        {
            DansToolboxLaunchDescriptor launch = DansToolboxToolLauncher.Find(toolId);
            DansToolboxToolDescriptor tool = DansToolboxTools.Find(toolId);
            string visibleLabel = compactLabel ?? string.Empty;
            Texture icon = string.IsNullOrEmpty(launch.IconName)
                ? null
                : EditorGUIUtility.IconContent(launch.IconName).image;
            string resolvedTooltip = string.IsNullOrWhiteSpace(tooltip)
                ? tool.Name
                : tooltip;

            // DockArea.GetTruncatedTabContent caches by title text only. Every
            // icon-only window used to have an empty title, so Unity returned
            // whichever icon and tooltip first populated that shared cache
            // entry. The zero-width suffix keeps each identity distinct without
            // putting long labels back into the tab strip.
            return new GUIContent(
                visibleLabel + CreateInvisibleCacheKey(toolId),
                icon,
                resolvedTooltip);
        }

        internal static string StripInvisibleCacheKey(string title)
        {
            return string.IsNullOrEmpty(title)
                ? string.Empty
                : title.Replace("\u200B", string.Empty)
                    .Replace("\u2060", string.Empty);
        }

        private static string CreateInvisibleCacheKey(string toolId)
        {
            int ordinal = 1;
            foreach (DansToolboxLaunchDescriptor descriptor in
                     DansToolboxToolLauncher.All)
            {
                if (string.Equals(descriptor.Id, toolId, StringComparison.Ordinal))
                {
                    return new string('\u200B', ordinal);
                }
                ordinal++;
            }

            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in toolId ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }

                return new string('\u200B', (int)(hash % 32u) + ordinal);
            }
        }

        private static void EnsureCompactTabChrome()
        {
            if (!TryApplyCompactTabChrome() && !Application.isBatchMode)
            {
                ScheduleTabChromeRetry();
            }
        }

        private static void ScheduleTabChromeRetry()
        {
            if (tabChromeRetryScheduled)
            {
                return;
            }

            tabChromeRetryScheduled = true;
            EditorApplication.delayCall -= RetryTabChrome;
            EditorApplication.delayCall += RetryTabChrome;
        }

        private static void RetryTabChrome()
        {
            tabChromeRetryScheduled = false;
            EnsureCompactTabChrome();
        }

        private static bool TryApplyCompactTabChrome()
        {
            try
            {
                Type editorWindowAssemblyType = typeof(EditorWindow);
                Type dockArea = editorWindowAssemblyType.Assembly.GetType(
                    "UnityEditor.DockArea",
                    false);
                FieldInfo liveTabStyle = dockArea?.GetField(
                    "tabStyle",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (liveTabStyle == null ||
                    liveTabStyle.FieldType != typeof(GUIStyle))
                {
                    return false;
                }

                // Named Unity GUIStyles may only initialize while Unity owns a
                // current skin. A populated DockArea.tabStyle proves the native
                // dock has already rendered, so reading its shared Styles class
                // here cannot trigger the "no current skin" console error.
                bool dockStyleIsReady = false;
                foreach (UnityEngine.Object instance in
                         Resources.FindObjectsOfTypeAll(dockArea))
                {
                    if (liveTabStyle.GetValue(instance) is GUIStyle)
                    {
                        dockStyleIsReady = true;
                        break;
                    }
                }
                if (!dockStyleIsReady)
                {
                    return false;
                }

                Type styles = editorWindowAssemblyType.Assembly.GetType(
                    "UnityEditor.DockArea+Styles",
                    false);
                FieldInfo minimumWidth = styles?.GetField(
                    "tabMinWidth",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                FieldInfo standardTab = styles?.GetField(
                    "dragTab",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                FieldInfo firstTab = styles?.GetField(
                    "dragTabFirst",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                FieldInfo tabLabel = styles?.GetField(
                    "tabLabel",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (minimumWidth == null ||
                    minimumWidth.FieldType != typeof(float) ||
                    standardTab == null ||
                    standardTab.FieldType != typeof(GUIStyle) ||
                    firstTab == null ||
                    firstTab.FieldType != typeof(GUIStyle) ||
                    tabLabel == null ||
                    tabLabel.FieldType != typeof(GUIStyle))
                {
                    return false;
                }

                float current = (float)minimumWidth.GetValue(null);
                if (current > CompactTabMinimumWidth)
                {
                    minimumWidth.SetValue(null, CompactTabMinimumWidth);
                }

                GUIStyle label = tabLabel.GetValue(null) as GUIStyle;
                if (standardTab.GetValue(null) is not GUIStyle ||
                    firstTab.GetValue(null) is not GUIStyle ||
                    label == null)
                {
                    return false;
                }

                // Center every title inside Unity's native tab hitbox. Icon-only
                // tabs then share one compact width, including the first tab in
                // a dock, while text-labelled tabs retain their natural width.
                // Hover, selection, borders, and shape deliberately remain
                // Unity-native so Toolbox themes cannot recolor the tab strip.
                label.alignment = TextAnchor.MiddleCenter;
                return true;
            }
            catch
            {
                // Dock chrome is an internal Unity implementation detail. A
                // future version can safely retain its normal width rather than
                // producing customer-facing warnings or breaking tool launch.
                return false;
            }
        }

        private static void ReleaseTabChrome()
        {
            EditorApplication.delayCall -= RetryTabChrome;
            tabChromeRetryScheduled = false;
        }
    }
}
