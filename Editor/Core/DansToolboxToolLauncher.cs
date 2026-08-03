using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal enum DansToolboxPlacement
    {
        Auto,
        Left,
        Right,
        Bottom,
        Center,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        InspectorDock,
        DockPicker
    }

    internal enum DansToolboxToolGroup
    {
        Workspace,
        Create,
        Integrate
    }

    internal readonly struct DansToolboxLaunchDescriptor
    {
        internal DansToolboxLaunchDescriptor(
            string id,
            string typeName,
            string iconName,
            DansToolboxToolGroup group,
            DansToolboxPlacement defaultPlacement,
            Vector2 preferredSize,
            Vector2 minimumSize,
            bool allowsMultiple = false)
        {
            Id = id;
            TypeName = typeName;
            IconName = iconName;
            Group = group;
            DefaultPlacement = defaultPlacement;
            PreferredSize = preferredSize;
            MinimumSize = minimumSize;
            AllowsMultiple = allowsMultiple;
        }

        internal string Id { get; }
        internal string TypeName { get; }
        internal string IconName { get; }
        internal DansToolboxToolGroup Group { get; }
        internal DansToolboxPlacement DefaultPlacement { get; }
        internal Vector2 PreferredSize { get; }
        internal Vector2 MinimumSize { get; }
        internal bool AllowsMultiple { get; }
    }

    public static class DansToolboxToolHub
    {
        public static bool Open(string toolId)
        {
            return DansToolboxToolLauncher.Launch(toolId);
        }

        public static bool OpenNewNativeDock()
        {
            return DansToolboxToolLauncher.Launch(
                DansToolboxTools.NativeWindowDockId,
                DansToolboxPlacement.Auto,
                true);
        }
    }

    /// <summary>
    /// Keeps tool discovery and placement in the core assembly without making
    /// the core assembly depend on every individual tool assembly.
    /// </summary>
    internal static class DansToolboxToolLauncher
    {
        private const string PlacementKeyPrefix = "DansToolbox.Hub.Placement.";
        private const float EdgeMargin = 14f;
        private const float EditorChromeHeight = 74f;

        private static readonly IReadOnlyList<DansToolboxLaunchDescriptor> descriptors =
            new[]
            {
                new DansToolboxLaunchDescriptor(
                    DansToolboxTools.BetterHierarchyId,
                    "DansToolbox.EditorTools.BetterHierarchy.BetterHierarchyWindow, DansToolbox.BetterHierarchy.Editor",
                    "UnityEditor.SceneHierarchyWindow",
                    DansToolboxToolGroup.Workspace,
                    DansToolboxPlacement.Left,
                    new Vector2(520f, 720f),
                    new Vector2(260f, 240f)),
                new DansToolboxLaunchDescriptor(
                    DansToolboxTools.BetterInspectorId,
                    "DansToolbox.EditorTools.BetterInspector.BetterInspectorWindow, DansToolbox.BetterInspector.Editor",
                    "UnityEditor.InspectorWindow",
                    DansToolboxToolGroup.Workspace,
                    DansToolboxPlacement.Right,
                    new Vector2(520f, 720f),
                    new Vector2(300f, 260f)),
                new DansToolboxLaunchDescriptor(
                    DansToolboxTools.BetterProjectId,
                    "DansToolbox.EditorTools.BetterProject.BetterProjectWindow, DansToolbox.BetterProject.Editor",
                    "Project",
                    DansToolboxToolGroup.Workspace,
                    DansToolboxPlacement.Bottom,
                    new Vector2(900f, 520f),
                    new Vector2(620f, 320f)),
                new DansToolboxLaunchDescriptor(
                    DansToolboxTools.BetterConsoleId,
                    "DansToolbox.EditorTools.BetterConsole.BetterConsoleWindow, DansToolbox.BetterConsole.Editor",
                    "UnityEditor.ConsoleWindow",
                    DansToolboxToolGroup.Workspace,
                    DansToolboxPlacement.Bottom,
                    new Vector2(860f, 500f),
                    new Vector2(420f, 260f)),
                new DansToolboxLaunchDescriptor(
                    DansToolboxTools.BetterSceneId,
                    "DansToolbox.EditorTools.BetterScene.BetterSceneWindow, DansToolbox.BetterScene.Editor",
                    "UnityEditor.SceneView",
                    DansToolboxToolGroup.Workspace,
                    DansToolboxPlacement.Right,
                    new Vector2(460f, 650f),
                    new Vector2(300f, 300f)),
                new DansToolboxLaunchDescriptor(
                    DansToolboxTools.RetroSfxId,
                    "DansToolbox.EditorTools.Audio.RetroSfxGeneratorWindow, DansToolbox.RetroSfx.Editor",
                    "Audio Mixer",
                    DansToolboxToolGroup.Create,
                    DansToolboxPlacement.InspectorDock,
                    new Vector2(860f, 760f),
                    new Vector2(620f, 680f)),
                new DansToolboxLaunchDescriptor(
                    DansToolboxTools.RetroVfxId,
                    "DansToolbox.EditorTools.RetroVfx.RetroVfxGeneratorWindow, DansToolbox.RetroVfx.Editor",
                    "Particle Effect",
                    DansToolboxToolGroup.Create,
                    DansToolboxPlacement.InspectorDock,
                    new Vector2(920f, 740f),
                    new Vector2(760f, 720f)),
                new DansToolboxLaunchDescriptor(
                    DansToolboxTools.NativeWindowDockId,
                    "DansToolbox.EditorTools.NativeWindowDock.NativeWindowDockWindow, DansToolbox.NativeWindowDock.Editor",
                    "BuildSettings.Standalone.Small",
                    DansToolboxToolGroup.Integrate,
                    DansToolboxPlacement.DockPicker,
                    new Vector2(720f, 540f),
                    new Vector2(520f, 340f),
                    true)
            };

        internal static IReadOnlyList<DansToolboxLaunchDescriptor> All => descriptors;

        internal static IReadOnlyList<string> KnownWindowTypeNames =>
            descriptors.Select(descriptor => descriptor.TypeName).ToArray();

        internal static DansToolboxLaunchDescriptor Find(string toolId)
        {
            return descriptors.FirstOrDefault(descriptor =>
                string.Equals(descriptor.Id, toolId, StringComparison.Ordinal));
        }

        internal static DansToolboxPlacement GetPreferredPlacement(string toolId)
        {
            DansToolboxLaunchDescriptor descriptor = Find(toolId);
            int stored = EditorPrefs.GetInt(
                PlacementKeyPrefix + toolId,
                (int)descriptor.DefaultPlacement);
            return Enum.IsDefined(typeof(DansToolboxPlacement), stored)
                ? (DansToolboxPlacement)stored
                : descriptor.DefaultPlacement;
        }

        internal static void SetPreferredPlacement(
            string toolId,
            DansToolboxPlacement placement)
        {
            EditorPrefs.SetInt(PlacementKeyPrefix + toolId, (int)placement);
        }

        internal static int GetOpenCount(string toolId)
        {
            Type type = ResolveType(Find(toolId).TypeName);
            return type == null ? 0 : Resources.FindObjectsOfTypeAll(type).Length;
        }

        internal static bool Launch(
            string toolId,
            DansToolboxPlacement placement = DansToolboxPlacement.Auto,
            bool forceNew = false)
        {
            DansToolboxLaunchDescriptor descriptor = Find(toolId);
            if (string.IsNullOrEmpty(descriptor.Id))
            {
                Debug.LogError("Dans Toolbox could not find tool '" + toolId + "'.");
                return false;
            }

            if (!DansToolboxSettings.IsToolEnabled(toolId))
            {
                DansToolboxSetupWizard.Open();
                return false;
            }

            Type type = ResolveType(descriptor.TypeName);
            if (type == null || !typeof(EditorWindow).IsAssignableFrom(type))
            {
                Debug.LogError("Dans Toolbox could not load " + descriptor.TypeName + ".");
                return false;
            }

            EditorWindow[] existing = Resources.FindObjectsOfTypeAll(type)
                .OfType<EditorWindow>()
                .Where(window => window != null)
                .ToArray();
            DansToolboxPlacement resolvedPlacement = ResolvePlacement(
                toolId,
                descriptor,
                placement,
                existing.Length);

            if (resolvedPlacement == DansToolboxPlacement.DockPicker)
            {
                DansToolboxDockPickerWindow.Open(
                    target => CreateDockedWindow(
                        type,
                        descriptor,
                        toolId,
                        target),
                    () => CreateFloatingWindow(
                        type,
                        descriptor,
                        toolId,
                        DansToolboxPlacement.Center));
                return true;
            }

            if (!forceNew && !descriptor.AllowsMultiple && existing.Length > 0)
            {
                DansToolboxWindowChrome.ApplyCompactTitle(existing[0], toolId);
                if (resolvedPlacement == DansToolboxPlacement.InspectorDock)
                {
                    if (!DansToolboxDocking.TryDockToInspector(existing[0]))
                    {
                        existing[0].position = CalculateRect(
                            EditorGUIUtility.GetMainWindowPosition(),
                            DansToolboxPlacement.Right,
                            descriptor.PreferredSize);
                    }
                }
                else if (resolvedPlacement != DansToolboxPlacement.Auto)
                {
                    existing[0].position = CalculateRect(
                        EditorGUIUtility.GetMainWindowPosition(),
                        resolvedPlacement,
                        descriptor.PreferredSize);
                }
                existing[0].Show();
                existing[0].Focus();
                return true;
            }

            if (resolvedPlacement == DansToolboxPlacement.InspectorDock)
            {
                return CreateInspectorDockedWindow(type, descriptor, toolId);
            }

            return CreateFloatingWindow(type, descriptor, toolId, resolvedPlacement);
        }

        private static DansToolboxPlacement ResolvePlacement(
            string toolId,
            DansToolboxLaunchDescriptor descriptor,
            DansToolboxPlacement requested,
            int existingCount)
        {
            if (requested != DansToolboxPlacement.Auto)
            {
                return requested;
            }

            if (descriptor.DefaultPlacement == DansToolboxPlacement.InspectorDock ||
                descriptor.DefaultPlacement == DansToolboxPlacement.DockPicker)
            {
                return descriptor.DefaultPlacement;
            }

            DansToolboxPlacement resolved = descriptor.AllowsMultiple
                ? GetNextPanelPlacement(existingCount)
                : GetPreferredPlacement(toolId);
            if (resolved != DansToolboxPlacement.Auto)
            {
                return resolved;
            }

            return descriptor.DefaultPlacement == DansToolboxPlacement.Auto
                ? DansToolboxPlacement.Center
                : descriptor.DefaultPlacement;
        }

        private static bool CreateInspectorDockedWindow(
            Type type,
            DansToolboxLaunchDescriptor descriptor,
            string toolId)
        {
            EditorWindow window = ScriptableObject.CreateInstance(type) as EditorWindow;
            if (window == null)
            {
                Debug.LogError("Dans Toolbox could not create " + descriptor.TypeName + ".");
                return false;
            }

            ConfigureWindow(window, descriptor, toolId);
            if (DansToolboxDocking.TryDockToInspector(window))
            {
                return true;
            }

            window.position = CalculateRect(
                EditorGUIUtility.GetMainWindowPosition(),
                DansToolboxPlacement.Right,
                descriptor.PreferredSize);
            window.Show();
            window.Focus();
            return true;
        }

        private static void CreateDockedWindow(
            Type type,
            DansToolboxLaunchDescriptor descriptor,
            string toolId,
            DansToolboxDockTarget target)
        {
            EditorWindow window = ScriptableObject.CreateInstance(type) as EditorWindow;
            if (window == null)
            {
                return;
            }

            ConfigureWindow(window, descriptor, toolId);
            if (DansToolboxDocking.TryDock(window, target))
            {
                return;
            }

            window.position = CalculateRect(
                EditorGUIUtility.GetMainWindowPosition(),
                DansToolboxPlacement.Center,
                descriptor.PreferredSize);
            window.Show();
            window.Focus();
        }

        private static bool CreateFloatingWindow(
            Type type,
            DansToolboxLaunchDescriptor descriptor,
            string toolId,
            DansToolboxPlacement placement)
        {
            DansToolboxPlacement floatingPlacement =
                placement == DansToolboxPlacement.InspectorDock ||
                placement == DansToolboxPlacement.DockPicker ||
                placement == DansToolboxPlacement.Auto
                    ? DansToolboxPlacement.Center
                    : placement;
            Rect launchRect = CalculateRect(
                EditorGUIUtility.GetMainWindowPosition(),
                floatingPlacement,
                descriptor.PreferredSize);
            DansToolboxToolDescriptor tool = DansToolboxTools.Find(toolId);
            EditorWindow window = descriptor.AllowsMultiple
                ? ScriptableObject.CreateInstance(type) as EditorWindow
                : EditorWindow.GetWindowWithRect(type, launchRect, false, tool.Name);
            if (window == null)
            {
                Debug.LogError("Dans Toolbox could not create " + descriptor.TypeName + ".");
                return false;
            }

            ConfigureWindow(window, descriptor, toolId);
            window.position = launchRect;
            if (descriptor.AllowsMultiple)
            {
                window.Show();
            }
            window.Focus();
            return true;
        }

        private static void ConfigureWindow(
            EditorWindow window,
            DansToolboxLaunchDescriptor descriptor,
            string toolId)
        {
            window.minSize = descriptor.MinimumSize;
            if (!descriptor.AllowsMultiple)
            {
                DansToolboxWindowChrome.ApplyCompactTitle(window, toolId);
            }
        }

        internal static void CloseAllToolWindows()
        {
            foreach (DansToolboxLaunchDescriptor descriptor in descriptors)
            {
                Type type = ResolveType(descriptor.TypeName);
                if (type == null)
                {
                    continue;
                }

                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll(type)
                             .OfType<EditorWindow>()
                             .ToArray())
                {
                    if (window != null)
                    {
                        window.Close();
                    }
                }
            }
        }

        internal static Rect CalculateRect(
            Rect mainWindow,
            DansToolboxPlacement placement,
            Vector2 preferredSize)
        {
            Rect workArea = new Rect(
                mainWindow.x + EdgeMargin,
                mainWindow.y + EditorChromeHeight,
                Mathf.Max(320f, mainWindow.width - EdgeMargin * 2f),
                Mathf.Max(260f, mainWindow.height - EditorChromeHeight - EdgeMargin));

            Vector2 minimum = new Vector2(320f, 260f);
            Vector2 maximum = new Vector2(workArea.width, workArea.height);
            Vector2 requested = new Vector2(
                Mathf.Clamp(preferredSize.x, minimum.x, maximum.x),
                Mathf.Clamp(preferredSize.y, minimum.y, maximum.y));

            switch (placement)
            {
                case DansToolboxPlacement.Left:
                    requested.x = Mathf.Min(requested.x, workArea.width * 0.44f);
                    requested.y = workArea.height;
                    return new Rect(workArea.x, workArea.y, requested.x, requested.y);
                case DansToolboxPlacement.Right:
                    requested.x = Mathf.Min(requested.x, workArea.width * 0.44f);
                    requested.y = workArea.height;
                    return new Rect(workArea.xMax - requested.x, workArea.y, requested.x, requested.y);
                case DansToolboxPlacement.Bottom:
                    requested.x = Mathf.Min(Mathf.Max(requested.x, workArea.width * 0.62f), workArea.width);
                    requested.y = Mathf.Min(requested.y, workArea.height * 0.52f);
                    return new Rect(
                        workArea.center.x - requested.x * 0.5f,
                        workArea.yMax - requested.y,
                        requested.x,
                        requested.y);
                case DansToolboxPlacement.TopLeft:
                case DansToolboxPlacement.TopRight:
                case DansToolboxPlacement.BottomLeft:
                case DansToolboxPlacement.BottomRight:
                    float tileGap = 8f;
                    float tileWidth = (workArea.width - tileGap) * 0.5f;
                    float tileHeight = (workArea.height - tileGap) * 0.5f;
                    bool right = placement == DansToolboxPlacement.TopRight ||
                                 placement == DansToolboxPlacement.BottomRight;
                    bool bottom = placement == DansToolboxPlacement.BottomLeft ||
                                  placement == DansToolboxPlacement.BottomRight;
                    return new Rect(
                        right ? workArea.x + tileWidth + tileGap : workArea.x,
                        bottom ? workArea.y + tileHeight + tileGap : workArea.y,
                        tileWidth,
                        tileHeight);
                default:
                    return new Rect(
                        workArea.center.x - requested.x * 0.5f,
                        workArea.center.y - requested.y * 0.5f,
                        requested.x,
                        requested.y);
            }
        }

        private static DansToolboxPlacement GetNextPanelPlacement(int openPanelCount)
        {
            switch (openPanelCount % 4)
            {
                case 0: return DansToolboxPlacement.TopLeft;
                case 1: return DansToolboxPlacement.TopRight;
                case 2: return DansToolboxPlacement.BottomLeft;
                default: return DansToolboxPlacement.BottomRight;
            }
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            Type resolved = Type.GetType(typeName, false);
            if (resolved != null)
            {
                return resolved;
            }

            string fullName = typeName.Split(',')[0].Trim();
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolved = assembly.GetType(fullName, false);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return null;
        }
    }
}
