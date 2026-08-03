using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal sealed class DansToolboxDockTarget
    {
        internal DansToolboxDockTarget(
            UnityEngine.Object host,
            Rect screenRect,
            string label,
            IReadOnlyList<string> paneTypeNames)
        {
            Host = host;
            ScreenRect = screenRect;
            Label = label;
            PaneTypeNames = paneTypeNames;
        }

        internal UnityEngine.Object Host { get; }
        internal Rect ScreenRect { get; }
        internal string Label { get; }
        internal IReadOnlyList<string> PaneTypeNames { get; }

        internal bool ContainsPane(string fullTypeName)
        {
            return PaneTypeNames.Any(name =>
                string.Equals(name, fullTypeName, StringComparison.Ordinal));
        }
    }

    internal static class DansToolboxDocking
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Type DockAreaType =
            typeof(EditorWindow).Assembly.GetType("UnityEditor.DockArea");
        private static readonly FieldInfo PanesField =
            DockAreaType?.GetField("m_Panes", InstanceFlags);
        private static readonly FieldInfo ParentField =
            typeof(EditorWindow).GetField("m_Parent", InstanceFlags);
        private static readonly MethodInfo AddTabMethod = DockAreaType?.GetMethod(
            "AddTab",
            InstanceFlags,
            null,
            new[] { typeof(EditorWindow), typeof(bool) },
            null);
        private static readonly MethodInfo RemoveTabMethod = DockAreaType?.GetMethod(
            "RemoveTab",
            InstanceFlags,
            null,
            new[] { typeof(EditorWindow), typeof(bool), typeof(bool) },
            null);
        private static readonly PropertyInfo ScreenPositionProperty =
            FindProperty(DockAreaType, "screenPosition");
        private static readonly PropertyInfo FloatingWindowProperty =
            FindProperty(DockAreaType, "floatingWindow");

        internal static bool IsSupported =>
            DockAreaType != null &&
            PanesField != null &&
            ParentField != null &&
            AddTabMethod != null &&
            RemoveTabMethod != null &&
            ScreenPositionProperty != null;

        internal static IReadOnlyList<DansToolboxDockTarget> DiscoverTargets()
        {
            var targets = new List<DansToolboxDockTarget>();
            if (!IsSupported)
            {
                return targets;
            }

            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            foreach (UnityEngine.Object host in Resources.FindObjectsOfTypeAll(DockAreaType))
            {
                if (host == null || IsFloating(host))
                {
                    continue;
                }

                Rect screenRect;
                IList panes;
                try
                {
                    screenRect = (Rect)ScreenPositionProperty.GetValue(host);
                    panes = PanesField.GetValue(host) as IList;
                }
                catch
                {
                    continue;
                }

                if (panes == null || panes.Count == 0 ||
                    screenRect.width < 120f || screenRect.height < 90f ||
                    !mainWindow.Contains(screenRect.center))
                {
                    continue;
                }

                var paneTypes = new List<string>();
                var paneLabels = new List<string>();
                foreach (object paneObject in panes)
                {
                    if (!(paneObject is EditorWindow pane) || pane == null)
                    {
                        continue;
                    }

                    paneTypes.Add(pane.GetType().FullName);
                    string label = pane.titleContent?.tooltip;
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        label = pane.titleContent?.text;
                    }
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        label = CleanTypeName(pane.GetType().Name);
                    }
                    paneLabels.Add(label);
                }

                if (paneTypes.Count == 0)
                {
                    continue;
                }

                targets.Add(new DansToolboxDockTarget(
                    host,
                    screenRect,
                    GetDockLabel(paneTypes, paneLabels),
                    paneTypes));
            }

            return targets
                .OrderBy(target => Mathf.RoundToInt(target.ScreenRect.y / 24f))
                .ThenBy(target => target.ScreenRect.x)
                .ToArray();
        }

        internal static DansToolboxDockTarget FindInspectorTarget()
        {
            IReadOnlyList<DansToolboxDockTarget> targets = DiscoverTargets();
            DansToolboxDockTarget inspector = targets
                .Where(target => target.ContainsPane("UnityEditor.InspectorWindow"))
                .OrderByDescending(target => target.ScreenRect.xMax)
                .FirstOrDefault();
            if (inspector != null)
            {
                return inspector;
            }

            return targets
                .Where(target => target.ScreenRect.height >= 240f)
                .OrderByDescending(target => target.ScreenRect.center.x)
                .ThenByDescending(target => target.ScreenRect.height)
                .FirstOrDefault();
        }

        internal static bool TryDockToInspector(EditorWindow window)
        {
            DansToolboxDockTarget inspector = FindInspectorTarget();
            return inspector != null && TryDock(window, inspector);
        }

        internal static bool TryDock(
            EditorWindow window,
            DansToolboxDockTarget target)
        {
            if (!IsSupported || window == null || target?.Host == null)
            {
                return false;
            }

            try
            {
                object currentParent = ParentField.GetValue(window);
                if (ReferenceEquals(currentParent, target.Host))
                {
                    window.Show();
                    window.Focus();
                    return true;
                }

                if (currentParent != null && DockAreaType.IsInstanceOfType(currentParent))
                {
                    RemoveTabMethod.Invoke(
                        currentParent,
                        new object[] { window, true, true });
                }

                AddTabMethod.Invoke(target.Host, new object[] { window, true });
                window.Focus();
                window.Repaint();
                return ReferenceEquals(ParentField.GetValue(window), target.Host);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsFloating(UnityEngine.Object host)
        {
            if (FloatingWindowProperty == null)
            {
                return false;
            }

            try
            {
                return (bool)FloatingWindowProperty.GetValue(host);
            }
            catch
            {
                return false;
            }
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            while (type != null)
            {
                PropertyInfo property = type.GetProperty(
                    name,
                    InstanceFlags | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property;
                }
                type = type.BaseType;
            }
            return null;
        }

        private static string GetDockLabel(
            IReadOnlyList<string> paneTypes,
            IReadOnlyList<string> paneLabels)
        {
            if (paneTypes.Contains("UnityEditor.InspectorWindow")) return "INSPECTOR";
            if (paneTypes.Contains("UnityEditor.SceneView")) return "SCENE";
            if (paneTypes.Contains("UnityEditor.GameView")) return "GAME";
            if (paneTypes.Any(name => name.Contains("ProjectBrowser"))) return "PROJECT";
            if (paneTypes.Any(name => name.Contains("ConsoleWindow"))) return "CONSOLE";
            return paneLabels.Count > 0
                ? paneLabels[0].ToUpperInvariant()
                : "DOCK";
        }

        private static string CleanTypeName(string name)
        {
            return name
                .Replace("Window", string.Empty)
                .Replace("View", string.Empty)
                .Trim();
        }
    }
}
