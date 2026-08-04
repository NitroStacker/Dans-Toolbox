using System;
using System.Collections.Generic;
using System.Linq;
using DansToolbox.Editor;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace DansToolbox.EditorTools.BetterScene
{
    [Overlay(typeof(SceneView), Id, "Better Scene Tools", true, defaultLayout = Layout.HorizontalToolbar)]
    internal sealed class BetterSceneToolbarOverlay : Overlay, ICreateHorizontalToolbar, ICreateVerticalToolbar
    {
        internal const string Id = "DansToolbox.BetterScene.Toolbar";
        private readonly List<OverlayToolbar> contents = new List<OverlayToolbar>();
        private readonly List<EditorToolbarToggle> panelToggles = new List<EditorToolbarToggle>();

        public override void OnCreated()
        {
            base.OnCreated();
            collapsedIcon = IconFor(typeof(SceneView));
            minSize = new Vector2(34f, 34f);
            BetterSceneController.Changed += RefreshState;
            BetterSceneSettings.Changed += Rebuild;
            BetterSceneSelectionHistory.Changed += Rebuild;
            DansToolboxTheme.Changed += Rebuild;
            Selection.selectionChanged += Rebuild;
        }

        public override void OnWillBeDestroyed()
        {
            BetterSceneController.Changed -= RefreshState;
            BetterSceneSettings.Changed -= Rebuild;
            BetterSceneSelectionHistory.Changed -= Rebuild;
            DansToolboxTheme.Changed -= Rebuild;
            Selection.selectionChanged -= Rebuild;
            contents.Clear();
            panelToggles.Clear();
            base.OnWillBeDestroyed();
        }

        public override VisualElement CreatePanelContent()
        {
            return BuildToolbar(true);
        }

        public OverlayToolbar CreateHorizontalToolbarContent()
        {
            return BuildToolbar(false);
        }

        public OverlayToolbar CreateVerticalToolbarContent()
        {
            return BuildToolbar(true);
        }

        private OverlayToolbar BuildToolbar(bool vertical)
        {
            var toolbar = new OverlayToolbar();
            toolbar.style.flexDirection = vertical ? FlexDirection.Column : FlexDirection.Row;
            contents.Add(toolbar);
            Populate(toolbar, vertical);
            return toolbar;
        }

        private void Populate(OverlayToolbar toolbar, bool vertical)
        {
            toolbar.Clear();
            panelToggles.Clear();

            if (BetterSceneSettings.ToolbarHistoryVisible)
            {
                toolbar.Add(CreateTextButton("<", "Previous selection", BetterSceneSelectionHistory.Back, BetterSceneSelectionHistory.CanBack));
                toolbar.Add(CreateTextButton(">", "Next selection", BetterSceneSelectionHistory.Forward, BetterSceneSelectionHistory.CanForward));
                toolbar.Add(CreateDivider(vertical));
            }

            foreach (BetterScenePanel panel in BetterSceneSettings.ToolbarOrder)
            {
                if (!BetterSceneSettings.IsToolbarPanelVisible(panel)) continue;
                toolbar.Add(CreatePanelToggle(panel));
            }

            if (BetterSceneSettings.ToolbarQuickActionsVisible)
            {
                toolbar.Add(CreateDivider(vertical));
                toolbar.Add(CreateTextButton("F", "Frame selection", () => BetterSceneOperations.FrameSelection(), Selection.activeGameObject != null));
                toolbar.Add(CreateTextButton("I", "Isolate or restore selection", () => BetterSceneVisibility.ToggleIsolation(Selection.gameObjects), Selection.activeGameObject != null));
            }

            toolbar.Add(CreateTextButton("...", "Configure Better Scene toolbar", ShowConfigureMenu));
            toolbar.SetupChildrenAsButtonStrip();
            RefreshState();
        }

        private EditorToolbarToggle CreatePanelToggle(BetterScenePanel panel)
        {
            var toggle = new EditorToolbarToggle(IconFor(IconTypeFor(panel)))
            {
                tooltip = PanelLabel(panel) + " - " + PanelTooltip(panel)
            };
            toggle.userData = panel;
            toggle.RegisterValueChangedCallback(change =>
            {
                bool currentlyOpen = BetterSceneController.PanelExpanded && BetterSceneController.ActivePanel == panel;
                if (change.newValue || currentlyOpen) BetterSceneController.TogglePanel(panel);
                BetterSceneNativeOverlayUtility.SyncPanelOverlays();
            });
            toggle.AddManipulator(new ContextualMenuManipulator(evt => PopulatePanelContextMenu(evt.menu, panel)));
            panelToggles.Add(toggle);
            return toggle;
        }

        private static EditorToolbarButton CreateTextButton(string text, string tooltip, Action action, bool enabled = true)
        {
            var button = new EditorToolbarButton(action) { text = text, tooltip = tooltip };
            button.SetEnabled(enabled);
            return button;
        }

        private static VisualElement CreateDivider(bool vertical)
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            var divider = new VisualElement { pickingMode = PickingMode.Ignore };
            divider.style.backgroundColor = palette.BorderStrong;
            if (vertical)
            {
                divider.style.height = 1f;
                divider.style.marginTop = 3f;
                divider.style.marginBottom = 3f;
                divider.style.marginLeft = 4f;
                divider.style.marginRight = 4f;
            }
            else
            {
                divider.style.width = 1f;
                divider.style.marginLeft = 3f;
                divider.style.marginRight = 3f;
                divider.style.marginTop = 4f;
                divider.style.marginBottom = 4f;
            }
            return divider;
        }

        private void RefreshState()
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            rootVisualElement?.SetEnabled(DansToolboxSettings.IsToolEnabled(DansToolboxTools.BetterSceneId));
            foreach (EditorToolbarToggle toggle in panelToggles.ToArray())
            {
                if (!(toggle.userData is BetterScenePanel panel)) continue;
                bool selected = BetterSceneController.PanelExpanded && BetterSceneController.ActivePanel == panel;
                toggle.SetValueWithoutNotify(selected);
                toggle.style.unityBackgroundImageTintColor = selected ? palette.Accent : palette.Text;
                toggle.style.backgroundColor = selected
                    ? new StyleColor(palette.AccentSoft)
                    : new StyleColor(StyleKeyword.Null);
                toggle.style.borderBottomColor = palette.Accent;
                toggle.style.borderTopColor = palette.Accent;
                toggle.style.borderLeftColor = palette.Accent;
                toggle.style.borderRightColor = palette.Accent;
                toggle.style.borderBottomWidth = selected ? 1f : 0f;
                toggle.style.borderTopWidth = selected ? 1f : 0f;
                toggle.style.borderLeftWidth = selected ? 1f : 0f;
                toggle.style.borderRightWidth = selected ? 1f : 0f;
            }
            rootVisualElement?.MarkDirtyRepaint();
            BetterSceneNativeOverlayUtility.SyncPanelOverlays();
        }

        private void Rebuild()
        {
            OverlayToolbar[] activeContents = contents.Where(content => content != null && content.panel != null).ToArray();
            contents.Clear();
            contents.AddRange(activeContents);
            foreach (OverlayToolbar content in activeContents)
            {
                bool vertical = content.resolvedStyle.flexDirection == FlexDirection.Column;
                Populate(content, vertical);
            }
            rootVisualElement?.MarkDirtyRepaint();
        }

        private void PopulatePanelContextMenu(DropdownMenu menu, BetterScenePanel panel)
        {
            IReadOnlyList<BetterScenePanel> order = BetterSceneSettings.ToolbarOrder;
            int index = order.ToList().IndexOf(panel);
            menu.AppendAction("Move Earlier", _ => BetterSceneSettings.MoveToolbarPanel(panel, -1),
                _ => index > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Move Later", _ => BetterSceneSettings.MoveToolbarPanel(panel, 1),
                _ => index >= 0 && index < order.Count - 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendSeparator();
            menu.AppendAction("Hide from Toolbar", _ => BetterSceneSettings.SetToolbarPanelVisible(panel, false));
        }

        private void ShowConfigureMenu()
        {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent("Drag the handle to move or dock"));
            menu.AddDisabledItem(new GUIContent("Use the handle menu for orientation"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Groups/Selection History"), BetterSceneSettings.ToolbarHistoryVisible,
                () => BetterSceneSettings.ToolbarHistoryVisible = !BetterSceneSettings.ToolbarHistoryVisible);
            menu.AddItem(new GUIContent("Groups/Frame + Isolate"), BetterSceneSettings.ToolbarQuickActionsVisible,
                () => BetterSceneSettings.ToolbarQuickActionsVisible = !BetterSceneSettings.ToolbarQuickActionsVisible);
            menu.AddSeparator("Tools/");
            foreach (BetterScenePanel panel in BetterSceneSettings.ToolbarOrder)
            {
                BetterScenePanel captured = panel;
                menu.AddItem(new GUIContent("Tools/" + PanelLabel(panel)), BetterSceneSettings.IsToolbarPanelVisible(panel),
                    () => BetterSceneSettings.SetToolbarPanelVisible(captured, !BetterSceneSettings.IsToolbarPanelVisible(captured)));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Reset Toolbar Contents"), false, BetterSceneSettings.ResetToolbarLayout);
            menu.ShowAsContext();
        }

        private static Type IconTypeFor(BetterScenePanel panel)
        {
            switch (panel)
            {
                case BetterScenePanel.Create: return typeof(GameObject);
                case BetterScenePanel.Transform: return typeof(Transform);
                case BetterScenePanel.Place: return typeof(MeshFilter);
                case BetterScenePanel.View: return typeof(Camera);
                case BetterScenePanel.Visibility: return typeof(MeshRenderer);
                case BetterScenePanel.Measure: return typeof(BoxCollider);
                case BetterScenePanel.Review: return typeof(MonoScript);
                default: return typeof(SceneView);
            }
        }

        private static Texture2D IconFor(Type type)
        {
            return EditorGUIUtility.ObjectContent(null, type)?.image as Texture2D;
        }

        private static string PanelLabel(BetterScenePanel panel)
        {
            return panel.ToString();
        }

        private static string PanelTooltip(BetterScenePanel panel)
        {
            switch (panel)
            {
                case BetterScenePanel.Create: return "Create scene objects";
                case BetterScenePanel.Transform: return "Align, distribute, ground, group, and mirror";
                case BetterScenePanel.Place: return "Place assets with spatial snapping";
                case BetterScenePanel.View: return "Scene viewpoints and bookmarks";
                case BetterScenePanel.Visibility: return "Visibility, isolation, and layer presets";
                case BetterScenePanel.Measure: return "Measure distance and delta";
                case BetterScenePanel.Review: return "Selection diagnostics and related logs";
                default: return string.Empty;
            }
        }
    }

    [Overlay(
        typeof(SceneView),
        Id,
        "Better Scene Tool",
        false,
        defaultLayout = Layout.Panel,
        defaultDockZone = DockZone.Floating,
        defaultWidth = 480f,
        defaultHeight = 335f,
        minWidth = 280f,
        minHeight = 150f,
        maxWidth = 720f,
        maxHeight = 1200f)]
    internal sealed class BetterScenePanelOverlay : IMGUIOverlay
    {
        internal const string Id = "DansToolbox.BetterScene.Panel";
        internal const float DefaultPanelWidth = 480f;
        internal const float MinimumPanelWidth = 280f;
        internal const float MinimumPanelHeight = 150f;
        internal const float MaximumPanelWidth = 720f;
        internal const float MaximumPanelHeight = 1200f;
        private bool synchronizing;
        private bool renderingGui;

        public override void OnCreated()
        {
            base.OnCreated();
            collapsedIcon = EditorGUIUtility.ObjectContent(null, typeof(SceneView))?.image as Texture2D;
            minSize = new Vector2(MinimumPanelWidth, MinimumPanelHeight);
            maxSize = new Vector2(MaximumPanelWidth, MaximumPanelHeight);
            defaultSize = new Vector2(480f, 335f);
            BetterSceneController.Changed += Sync;
            BetterSceneSettings.Changed += OnContentChanged;
            DansToolboxTheme.Changed += Repaint;
            displayedChanged += OnDisplayedChanged;
            collapsedChanged += OnCollapsedChanged;
            Sync();
        }

        public override void OnWillBeDestroyed()
        {
            BetterSceneController.Changed -= Sync;
            BetterSceneSettings.Changed -= OnContentChanged;
            DansToolboxTheme.Changed -= Repaint;
            displayedChanged -= OnDisplayedChanged;
            collapsedChanged -= OnCollapsedChanged;
            base.OnWillBeDestroyed();
        }

        public override void OnGUI()
        {
            if (!BetterSceneController.PanelExpanded || BetterSceneController.ActivePanel == BetterScenePanel.None) return;
            renderingGui = true;
            try
            {
                float height = BetterSceneOverlay.DesiredHeight(BetterSceneController.ActivePanel);
                Rect rect = GUILayoutUtility.GetRect(MinimumPanelWidth, height, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                BetterSceneOverlay.DrawPanel(rect, BetterSceneController.ActivePanel);
            }
            finally
            {
                renderingGui = false;
            }
        }

        internal void Sync()
        {
            if (synchronizing) return;
            if (renderingGui)
            {
                // Controller changes can originate from buttons inside this overlay. Native
                // overlay state must not change before that IMGUI pass has ended.
                rootVisualElement?.MarkDirtyRepaint();
                BetterSceneNativeOverlayUtility.SchedulePanelSync();
                return;
            }
            bool shouldDisplay = BetterSceneController.PanelExpanded && BetterSceneController.ActivePanel != BetterScenePanel.None;
            if (shouldDisplay && !displayed)
            {
                BetterSceneNativeOverlayUtility.SchedulePanelNearToolbar();
                return;
            }

            synchronizing = true;
            try
            {
                if (!shouldDisplay && displayed) displayed = false;
                if (shouldDisplay)
                {
                    if (collapsed) collapsed = false;
                    displayName = "Better Scene - " + BetterSceneController.ActivePanel;
                }
                rootVisualElement?.MarkDirtyRepaint();
            }
            finally
            {
                synchronizing = false;
            }
        }

        internal void Resize()
        {
            if (synchronizing || !displayed || !floating) return;
            if (renderingGui)
            {
                BetterSceneNativeOverlayUtility.SchedulePanelResize();
                return;
            }

            synchronizing = true;
            try
            {
                ApplyResponsiveSize();
                rootVisualElement?.MarkDirtyRepaint();
            }
            finally
            {
                synchronizing = false;
            }
        }

        internal Rect GetFloatingCanvasWorldBounds()
        {
            VisualElement current = rootVisualElement;
            while (current != null)
            {
                if (current.name == "unity-overlay-canvas") return current.worldBound;
                current = current.parent;
            }

            return rootVisualElement?.panel?.visualTree.worldBound ?? Rect.zero;
        }

        internal void ShowAt(Vector2 position)
        {
            synchronizing = true;
            try
            {
                if (!floating) Undock();
                Vector2 responsiveSize = GetResponsiveSize();
                Rect canvas = GetFloatingCanvasWorldBounds();
                floatingPosition = canvas.width > 1f && canvas.height > 1f
                    ? BetterSceneNativeOverlayUtility.ClampPanelPosition(position, canvas.size, responsiveSize)
                    : position;
                ApplyResponsiveConstraints(responsiveSize);
                size = responsiveSize;
                if (collapsed) collapsed = false;
                displayName = "Better Scene - " + BetterSceneController.ActivePanel;
                if (!displayed) displayed = true;
                rootVisualElement?.MarkDirtyRepaint();
            }
            finally
            {
                synchronizing = false;
            }
        }

        private void Repaint()
        {
            rootVisualElement?.MarkDirtyRepaint();
        }

        internal Vector2 GetResponsiveSize()
        {
            Rect canvas = GetFloatingCanvasWorldBounds();
            Vector2 viewport = canvas.width > 1f && canvas.height > 1f
                ? canvas.size
                : SceneView.lastActiveSceneView != null
                    ? SceneView.lastActiveSceneView.position.size
                    : new Vector2(DefaultPanelWidth + 16f, MaximumPanelHeight + 16f);
            float width = size.x > 0f ? size.x : DefaultPanelWidth;
            return BetterSceneNativeOverlayUtility.CalculateResponsivePanelSize(
                width,
                BetterSceneOverlay.DesiredHeight(BetterSceneController.ActivePanel),
                viewport);
        }

        private void ApplyResponsiveSize()
        {
            Vector2 responsiveSize = GetResponsiveSize();
            ApplyResponsiveConstraints(responsiveSize);
            size = responsiveSize;
            Rect canvas = GetFloatingCanvasWorldBounds();
            if (canvas.width > 1f && canvas.height > 1f)
            {
                floatingPosition = BetterSceneNativeOverlayUtility.ClampPanelPosition(
                    floatingPosition,
                    canvas.size,
                    responsiveSize);
            }
        }

        private void ApplyResponsiveConstraints(Vector2 responsiveSize)
        {
            minSize = new Vector2(
                Mathf.Min(MinimumPanelWidth, responsiveSize.x),
                Mathf.Min(MinimumPanelHeight, responsiveSize.y));
            maxSize = new Vector2(MaximumPanelWidth, MaximumPanelHeight);
        }

        private void OnContentChanged()
        {
            Repaint();
            BetterSceneNativeOverlayUtility.SchedulePanelResize();
        }

        private void OnDisplayedChanged(bool value)
        {
            if (!synchronizing && !value && BetterSceneController.PanelExpanded) BetterSceneController.CollapsePanel();
        }

        private void OnCollapsedChanged(bool value)
        {
            if (!synchronizing && value && BetterSceneController.PanelExpanded) BetterSceneController.CollapsePanel();
        }
    }

    internal static class BetterSceneNativeOverlayUtility
    {
        internal static void SyncPanelOverlays()
        {
            foreach (BetterScenePanelOverlay overlay in FindOverlays<BetterScenePanelOverlay>()) overlay.Sync();
        }

        internal static void SchedulePanelNearToolbar()
        {
            if (!BetterSceneController.PanelExpanded) return;
            EditorApplication.delayCall -= ShowPanelNearToolbar;
            EditorApplication.delayCall += ShowPanelNearToolbar;
        }

        internal static void SchedulePanelSync()
        {
            EditorApplication.delayCall -= SyncPanelOverlays;
            EditorApplication.delayCall += SyncPanelOverlays;
        }

        internal static void SchedulePanelResize()
        {
            EditorApplication.delayCall -= ResizePanelOverlays;
            EditorApplication.delayCall += ResizePanelOverlays;
        }

        private static void ResizePanelOverlays()
        {
            foreach (BetterScenePanelOverlay overlay in FindOverlays<BetterScenePanelOverlay>()) overlay.Resize();
        }

        internal static void ShowPanelNearToolbar()
        {
            if (!BetterSceneController.PanelExpanded) return;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.overlayCanvas == null) return;
            BetterSceneToolbarOverlay toolbar = sceneView.overlayCanvas.overlays
                .OfType<BetterSceneToolbarOverlay>()
                .FirstOrDefault(overlay => overlay.displayed);
            if (toolbar == null) return;
            BetterScenePanelOverlay panel = sceneView.overlayCanvas.overlays
                .OfType<BetterScenePanelOverlay>()
                .FirstOrDefault();
            if (panel == null) return;

            Rect toolbarWorldBounds = toolbar.rootVisualElement == null
                ? Rect.zero
                : toolbar.rootVisualElement.worldBound;
            Rect canvasWorldBounds = panel.GetFloatingCanvasWorldBounds();
            Rect toolbarBounds = ConvertWorldBoundsToCanvas(toolbarWorldBounds, canvasWorldBounds);
            Vector2 panelSize = panel.GetResponsiveSize();
            float viewportWidth = canvasWorldBounds.width > 1f ? canvasWorldBounds.width : panelSize.x + 16f;
            float viewportHeight = canvasWorldBounds.height > 1f ? canvasWorldBounds.height : panelSize.y + 16f;
            bool vertical = toolbar.layout == Layout.VerticalToolbar || toolbarBounds.height > toolbarBounds.width * 1.25f;
            Vector2 position = CalculatePanelPosition(
                toolbarBounds,
                new Vector2(viewportWidth, viewportHeight),
                panelSize,
                vertical);

            panel.ShowAt(position);
            SceneView.RepaintAll();
        }

        internal static Rect ConvertWorldBoundsToCanvas(Rect worldBounds, Rect canvasWorldBounds)
        {
            return new Rect(worldBounds.position - canvasWorldBounds.position, worldBounds.size);
        }

        internal static Vector2 CalculatePanelPosition(
            Rect toolbarBounds,
            Vector2 viewportSize,
            Vector2 panelSize,
            bool vertical)
        {
            const float gap = 8f;
            float viewportWidth = Mathf.Max(panelSize.x + 16f, viewportSize.x);
            float viewportHeight = Mathf.Max(panelSize.y + 16f, viewportSize.y);
            if (vertical)
            {
                float x = toolbarBounds.center.x > viewportWidth * 0.5f
                    ? toolbarBounds.xMin - panelSize.x - gap
                    : toolbarBounds.xMax + gap;
                return new Vector2(
                    Mathf.Clamp(x, 8f, viewportWidth - panelSize.x - 8f),
                    Mathf.Clamp(toolbarBounds.yMin, 8f, viewportHeight - panelSize.y - 8f));
            }
            float y = toolbarBounds.center.y > viewportHeight * 0.5f
                ? toolbarBounds.yMin - panelSize.y - gap
                : toolbarBounds.yMax + gap;
            return new Vector2(
                Mathf.Clamp(toolbarBounds.xMin, 8f, viewportWidth - panelSize.x - 8f),
                Mathf.Clamp(y, 8f, viewportHeight - panelSize.y - 8f));
        }

        internal static Vector2 CalculateResponsivePanelSize(
            float currentWidth,
            float preferredHeight,
            Vector2 viewportSize)
        {
            const float margin = 16f;
            float availableWidth = viewportSize.x > margin
                ? viewportSize.x - margin
                : BetterScenePanelOverlay.MinimumPanelWidth;
            float availableHeight = viewportSize.y > margin
                ? viewportSize.y - margin
                : BetterScenePanelOverlay.MinimumPanelHeight;
            float minimumWidth = Mathf.Min(BetterScenePanelOverlay.MinimumPanelWidth, availableWidth);
            float minimumHeight = Mathf.Min(BetterScenePanelOverlay.MinimumPanelHeight, availableHeight);
            float maximumWidth = Mathf.Min(BetterScenePanelOverlay.MaximumPanelWidth, availableWidth);
            float maximumHeight = Mathf.Min(BetterScenePanelOverlay.MaximumPanelHeight, availableHeight);
            float requestedWidth = currentWidth > 0f
                ? currentWidth
                : BetterScenePanelOverlay.DefaultPanelWidth;
            return new Vector2(
                Mathf.Clamp(requestedWidth, minimumWidth, maximumWidth),
                Mathf.Clamp(preferredHeight, minimumHeight, maximumHeight));
        }

        internal static Vector2 ClampPanelPosition(
            Vector2 position,
            Vector2 viewportSize,
            Vector2 panelSize)
        {
            return new Vector2(
                Mathf.Clamp(position.x, 8f, Mathf.Max(8f, viewportSize.x - panelSize.x - 8f)),
                Mathf.Clamp(position.y, 8f, Mathf.Max(8f, viewportSize.y - panelSize.y - 8f)));
        }

        internal static void ShowToolbar()
        {
            foreach (BetterSceneToolbarOverlay overlay in FindOverlays<BetterSceneToolbarOverlay>())
            {
                overlay.displayed = true;
                overlay.collapsed = false;
            }
            SceneView.RepaintAll();
        }

        internal static void ResetToolbar()
        {
            BetterSceneSettings.ResetToolbarLayout();
            foreach (SceneView sceneView in Resources.FindObjectsOfTypeAll<SceneView>())
            {
                if (sceneView == null || sceneView.overlayCanvas == null) continue;
                foreach (BetterSceneToolbarOverlay overlay in sceneView.overlayCanvas.overlays.OfType<BetterSceneToolbarOverlay>())
                {
                    sceneView.overlayCanvas.ResetOverlay(overlay);
                    overlay.displayed = true;
                    overlay.collapsed = false;
                }
            }
            SceneView.RepaintAll();
        }

        private static IEnumerable<T> FindOverlays<T>() where T : Overlay
        {
            return Resources.FindObjectsOfTypeAll<SceneView>()
                .Where(sceneView => sceneView != null && sceneView.overlayCanvas != null)
                .SelectMany(sceneView => sceneView.overlayCanvas.overlays)
                .OfType<T>();
        }
    }
}
