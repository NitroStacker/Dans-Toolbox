using System;
using DansToolbox.Editor;
using DansToolbox.EditorTools.BetterConsole;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterScene
{
    internal static class BetterSceneOverlay
    {
        private static BetterSceneAxis transformAxis = BetterSceneAxis.X;
        private static Vector2 panelScroll;
        private static string bookmarkName = "VIEW";
        private static string layerPresetName = "LAYERS";
        private static UnityEngine.Object replacementAsset;
        private static bool transformExtras;
        private static bool savedViews;
        private static bool layerPresets;
        private static BetterScenePanel scrollPanel;
        private static readonly TileAction[] tileBuffer = new TileAction[4];
        private static readonly SegmentAction[] segmentBuffer = new SegmentAction[4];
        private static readonly Metric[] metricBuffer = new Metric[4];

        internal static void DrawPanel(Rect panelRect, BetterScenePanel panel)
        {
            if (scrollPanel != panel)
            {
                scrollPanel = panel;
                panelScroll = Vector2.zero;
            }
            DansToolboxPalette palette = DansToolboxTheme.Current;
            BetterSceneGui.Panel(panelRect, false, true);
            EditorGUI.DrawRect(new Rect(panelRect.x, panelRect.y, 3f, panelRect.height), palette.Accent);
            DrawPanelHeader(panelRect, panel);

            Rect content = new Rect(panelRect.x + 10f, panelRect.y + 52f, panelRect.width - 20f, panelRect.height - 62f);
            GUILayout.BeginArea(content);
            try
            {
                panelScroll = GUILayout.BeginScrollView(panelScroll, false, false);
                try
                {
                    switch (panel)
                    {
                        case BetterScenePanel.Create: DrawCreate(); break;
                        case BetterScenePanel.Transform: DrawTransform(); break;
                        case BetterScenePanel.Place: DrawPlace(); break;
                        case BetterScenePanel.View: DrawView(); break;
                        case BetterScenePanel.Visibility: DrawVisibility(); break;
                        case BetterScenePanel.Measure: DrawMeasure(); break;
                        case BetterScenePanel.Review: DrawReview(); break;
                    }
                    GUILayout.Space(4f);
                }
                finally
                {
                    GUILayout.EndScrollView();
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static void DrawPanelHeader(Rect rect, BetterScenePanel panel)
        {
            Texture icon = PanelIcon(panel);
            if (icon != null) GUI.DrawTexture(new Rect(rect.x + 13f, rect.y + 13f, 24f, 24f), icon, ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(rect.x + 44f, rect.y + 9f, rect.width - 86f, 20f), PanelTitle(panel), BetterSceneGui.Title);
            GUI.Label(new Rect(rect.x + 44f, rect.y + 28f, rect.width - 86f, 17f), PanelDescription(panel), BetterSceneGui.Muted);
            Rect close = new Rect(rect.xMax - 34f, rect.y + 12f, 22f, 22f);
            if (BetterSceneGui.Button(close, new GUIContent("X", "Collapse tools"))) BetterSceneController.CollapsePanel();
        }

        private static void DrawCreate()
        {
            GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "CREATE AT SCENE PIVOT", BetterSceneGui.Tiny);
            DrawTileRow(
                Tile("EMPTY", "Create an empty GameObject", IconFor(typeof(GameObject)), () => BetterSceneOperations.Create(BetterSceneCreateKind.Empty)),
                Tile("GROUP", "Group the current selection", IconFor(typeof(Transform)), () => BetterSceneOperations.Create(BetterSceneCreateKind.Group)),
                Tile("CUBE", "Create a Cube", IconFor(typeof(MeshRenderer)), () => BetterSceneOperations.Create(BetterSceneCreateKind.Cube)),
                Tile("SPHERE", "Create a Sphere", IconFor(typeof(SphereCollider)), () => BetterSceneOperations.Create(BetterSceneCreateKind.Sphere))
            );
            GUILayout.Space(5f);
            DrawTileRow(
                Tile("PLANE", "Create a Plane", IconFor(typeof(MeshFilter)), () => BetterSceneOperations.Create(BetterSceneCreateKind.Plane)),
                Tile("CAMERA", "Create a Camera", IconFor(typeof(Camera)), () => BetterSceneOperations.Create(BetterSceneCreateKind.Camera)),
                Tile("LIGHT", "Create a Directional Light", IconFor(typeof(Light)), () => BetterSceneOperations.Create(BetterSceneCreateKind.Light)),
                Tile("AUDIO", "Create an Audio Source", IconFor(typeof(AudioSource)), () => BetterSceneOperations.Create(BetterSceneCreateKind.Audio))
            );
            GUILayout.Space(8f);
            DrawHint(Selection.gameObjects.Length > 0
                ? "GROUP preserves world transforms and remains fully Undoable."
                : "Objects are created at the current Scene pivot and selected immediately.");
        }

        private static void DrawTransform()
        {
            GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "WORKING AXIS", BetterSceneGui.Tiny);
            DrawSegmented(
                Segment("X", transformAxis == BetterSceneAxis.X, () => transformAxis = BetterSceneAxis.X),
                Segment("Y", transformAxis == BetterSceneAxis.Y, () => transformAxis = BetterSceneAxis.Y),
                Segment("Z", transformAxis == BetterSceneAxis.Z, () => transformAxis = BetterSceneAxis.Z)
            );
            GUILayout.Space(7f);
            GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "ALIGN TO ACTIVE", BetterSceneGui.Tiny);
            bool align = Selection.gameObjects.Length >= 2 && Selection.activeGameObject != null;
            DrawTileRow(
                Tile("MIN", "Align minimum bounds", IconFor(typeof(BoxCollider)), () => BetterSceneOperations.AlignSelection(transformAxis, BetterSceneAlignAnchor.Minimum), align),
                Tile("CENTER", "Align bounds centers", IconFor(typeof(Transform)), () => BetterSceneOperations.AlignSelection(transformAxis, BetterSceneAlignAnchor.Center), align),
                Tile("MAX", "Align maximum bounds", IconFor(typeof(BoxCollider)), () => BetterSceneOperations.AlignSelection(transformAxis, BetterSceneAlignAnchor.Maximum), align)
            );
            GUILayout.Space(5f);
            DrawTileRow(
                Tile("SPACE", "Distribute evenly on the working axis", IconFor(typeof(Transform)), () => BetterSceneOperations.DistributeSelection(transformAxis), Selection.gameObjects.Length >= 3),
                Tile("GROUND", "Drop selection to the first surface below", IconFor(typeof(Terrain)), BetterSceneOperations.GroundSelection, Selection.gameObjects.Length > 0),
                Tile("GROUP", "Group selection under one parent", IconFor(typeof(Transform)), () => BetterSceneOperations.GroupSelection(), Selection.gameObjects.Length > 0),
                Tile("MIRROR", "Mirror around the active object", IconFor(typeof(Transform)), () => BetterSceneOperations.MirrorSelection(transformAxis), align)
            );
            GUILayout.Space(6f);
            if (DrawDisclosure("TRANSFORM EXTRAS", transformExtras))
            {
                transformExtras = !transformExtras;
                NotifyContentSizeChanged();
            }
            if (transformExtras)
            {
                GUILayout.Space(4f);
                DrawTileRow(
                    Tile("SNAP POS", "Snap position to Unity increments", IconFor(typeof(Transform)), () => BetterSceneOperations.SnapSelection(true, false, false), Selection.transforms.Length > 0),
                    Tile("SNAP ROT", "Snap rotation to Unity increments", IconFor(typeof(Transform)), () => BetterSceneOperations.SnapSelection(false, true, false), Selection.transforms.Length > 0),
                    Tile("SNAP SCALE", "Snap scale to Unity increments", IconFor(typeof(Transform)), () => BetterSceneOperations.SnapSelection(false, false, true), Selection.transforms.Length > 0),
                    Tile("SNAP ALL", "Snap position, rotation and scale", IconFor(typeof(MeshFilter)), () => BetterSceneOperations.SnapSelection(true, true, true), Selection.transforms.Length > 0)
                );
                GUILayout.Space(4f);
                DrawSegmented(
                    Segment("RESET POS", false, () => BetterSceneOperations.ResetSelection(true, false, false), Selection.transforms.Length > 0),
                    Segment("RESET ROT", false, () => BetterSceneOperations.ResetSelection(false, true, false), Selection.transforms.Length > 0),
                    Segment("RESET SCALE", false, () => BetterSceneOperations.ResetSelection(false, false, true), Selection.transforms.Length > 0)
                );
            }
        }

        private static void DrawPlace()
        {
            UnityEngine.Object current = BetterSceneSettings.PlacementAsset;
            Rect assetCard = GUILayoutUtility.GetRect(10f, 62f, GUILayout.ExpandWidth(true));
            BetterSceneGui.Panel(assetCard, true);
            Texture preview = current == null ? null : AssetPreview.GetAssetPreview(current) ?? AssetPreview.GetMiniThumbnail(current);
            if (preview != null) GUI.DrawTexture(new Rect(assetCard.x + 7f, assetCard.y + 7f, 48f, 48f), preview, ScaleMode.ScaleToFit, true);
            else BetterSceneGui.Crosshair(new Vector2(assetCard.x + 31f, assetCard.center.y), 8f, DansToolboxTheme.Current.Muted);
            GUI.Label(new Rect(assetCard.x + 64f, assetCard.y + 8f, assetCard.width - 72f, 18f), current == null ? "CHOOSE AN ASSET" : current.name, BetterSceneGui.Label);
            Rect objectRect = new Rect(assetCard.x + 64f, assetCard.y + 31f, assetCard.width - 72f, 22f);
            UnityEngine.Object next = EditorGUI.ObjectField(objectRect, current, typeof(UnityEngine.Object), false);
            if (next != current)
            {
                if (next == null)
                {
                    BetterSceneSettings.PlacementAsset = null;
                }
                else if (BetterSceneController.CanPlaceAsset(next) &&
                         BetterSceneSettings.CanPersistPlacementAsset(next))
                {
                    BetterSceneSettings.PlacementAsset = next;
                }
                else
                {
                    SceneView.lastActiveSceneView?.ShowNotification(
                        new GUIContent("Place needs a prefab, model, sprite, mesh, or AudioClip from the Project."),
                        2.5d);
                }
            }

            UnityEngine.Object[] recent = BetterSceneSettings.GetRecentPlacementAssets();
            if (recent.Length > 0)
            {
                GUILayout.Space(6f);
                GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "RECENT", BetterSceneGui.Tiny);
                DrawAssetStrip(recent);
            }
            GUILayout.Space(6f);
            GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "ACTION", BetterSceneGui.Tiny);
            DrawSegmented(
                Segment("PLACE", !BetterSceneController.EraseMode, () => BetterSceneController.SetEraseModeAfterGui(false)),
                Segment("ERASE CURRENT", BetterSceneController.EraseMode, () => BetterSceneController.SetEraseModeAfterGui(true), current != null)
            );
            GUILayout.Space(6f);
            GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "SNAP TARGET", BetterSceneGui.Tiny);
            DrawSnapModes();
            GUILayout.Space(5f);
            DrawSegmented(
                Segment("ALIGN", BetterSceneSettings.AlignToSurface, () => BetterSceneSettings.AlignToSurface = !BetterSceneSettings.AlignToSurface),
                Segment("PARENT", BetterSceneSettings.ParentToSurface, () => BetterSceneSettings.ParentToSurface = !BetterSceneSettings.ParentToSurface),
                Segment("REPEAT", BetterSceneSettings.KeepPlacing, () => BetterSceneSettings.KeepPlacing = !BetterSceneSettings.KeepPlacing)
            );
            GUILayout.Space(7f);
            DrawHint(current == null
                ? "Choose or drag a prefab, model, sprite, mesh, or AudioClip."
                : BetterSceneController.EraseMode
                    ? "Only matching instances highlight red. Click to erase. Undo restores."
                    : "Move to preview. Hold Shift for centered smart snap. Click to place. Escape exits.");
        }

        private static void DrawView()
        {
            GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "VIEWPOINT", BetterSceneGui.Tiny);
            DrawTileRow(
                Tile("PERSP", "Perspective view", IconFor(typeof(Camera)), () => BetterSceneOperations.SetView(BetterSceneViewDirection.Perspective)),
                Tile("TOP", "Top orthographic view", IconFor(typeof(Camera)), () => BetterSceneOperations.SetView(BetterSceneViewDirection.Top)),
                Tile("FRONT", "Front orthographic view", IconFor(typeof(Camera)), () => BetterSceneOperations.SetView(BetterSceneViewDirection.Front)),
                Tile("RIGHT", "Right orthographic view", IconFor(typeof(Camera)), () => BetterSceneOperations.SetView(BetterSceneViewDirection.Right))
            );
            GUILayout.Space(5f);
            DrawTileRow(
                Tile("BOTTOM", "Bottom orthographic view", IconFor(typeof(Camera)), () => BetterSceneOperations.SetView(BetterSceneViewDirection.Bottom)),
                Tile("BACK", "Back orthographic view", IconFor(typeof(Camera)), () => BetterSceneOperations.SetView(BetterSceneViewDirection.Back)),
                Tile("LEFT", "Left orthographic view", IconFor(typeof(Camera)), () => BetterSceneOperations.SetView(BetterSceneViewDirection.Left)),
                Tile("CAMERA", "Create a Camera matching this Scene view", IconFor(typeof(Camera)), () => BetterSceneOperations.CreateCameraFromView())
            );
            GUILayout.Space(6f);
            GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "VIEW BEHAVIOR", BetterSceneGui.Tiny);
            DrawSegmented(
                Segment(
                    "ACCOUNT FOR ZOOM",
                    BetterSceneSettings.AccountForViewZoom,
                    () => BetterSceneSettings.AccountForViewZoom = !BetterSceneSettings.AccountForViewZoom)
            );
            GUILayout.Space(4f);
            DrawHint(BetterSceneSettings.AccountForViewZoom
                ? "Directional views preserve the current zoom and framing scale."
                : "Directional views use a consistent 10-unit framing scale.");
            GUILayout.Space(5f);
            DrawSegmented(
                Segment("FRAME", false, () => BetterSceneOperations.FrameSelection(), Selection.activeGameObject != null),
                Segment("FRAME + LOCK", false, () => BetterSceneOperations.FrameSelection(true), Selection.activeGameObject != null),
                Segment("SAVE VIEW", false, () => BetterSceneOperations.CaptureBookmark(bookmarkName), SceneView.lastActiveSceneView != null)
            );
            GUILayout.Space(6f);
            if (DrawDisclosure("SAVED VIEWS  " + BetterSceneSettings.Bookmarks.Count, savedViews))
            {
                savedViews = !savedViews;
                NotifyContentSizeChanged();
            }
            if (savedViews)
            {
                GUILayout.Space(4f);
                Rect saveRow = GUILayoutUtility.GetRect(10f, 24f, GUILayout.ExpandWidth(true));
                bookmarkName = EditorGUI.TextField(new Rect(saveRow.x, saveRow.y, saveRow.width - 58f, 22f), bookmarkName, BetterSceneGui.Field);
                if (BetterSceneGui.Button(new Rect(saveRow.xMax - 54f, saveRow.y, 54f, 22f), new GUIContent("SAVE", "Save current Scene view")))
                {
                    BetterSceneOperations.CaptureBookmark(bookmarkName);
                    bookmarkName = "VIEW";
                }
                int bookmarkCount = Mathf.Min(8, BetterSceneSettings.Bookmarks.Count);
                for (int bookmarkIndex = 0; bookmarkIndex < bookmarkCount; bookmarkIndex++)
                {
                    BetterSceneBookmark bookmark = BetterSceneSettings.Bookmarks[bookmarkIndex];
                    Rect row = GUILayoutUtility.GetRect(10f, 24f, GUILayout.ExpandWidth(true));
                    if (BetterSceneGui.Button(new Rect(row.x, row.y, row.width - 30f, 22f), new GUIContent(bookmark.Name, "Restore saved view"))) BetterSceneOperations.RestoreBookmark(bookmark);
                    if (BetterSceneGui.Button(new Rect(row.xMax - 26f, row.y, 26f, 22f), new GUIContent("X", "Delete saved view")))
                    {
                        BetterSceneSettings.RemoveBookmark(bookmark.Id);
                        break;
                    }
                }
            }
        }

        private static void DrawVisibility()
        {
            GameObject[] selected = Selection.gameObjects;
            GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "SELECTION", BetterSceneGui.Tiny);
            DrawSegmented(
                Segment("SOLO", BetterSceneVisibility.IsIsolating, () => BetterSceneVisibility.ToggleIsolation(selected), selected.Length > 0),
                Segment("HIDE", false, () => BetterSceneVisibility.ToggleHidden(selected), selected.Length > 0),
                Segment("LOCK", false, () => BetterSceneVisibility.TogglePicking(selected), selected.Length > 0)
            );
            GUILayout.Space(7f);
            GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "SCENE FILTER", BetterSceneGui.Tiny);
            DrawTileRow(
                BandTile(BetterSceneVisibilityBand.Environment, typeof(MeshRenderer)),
                BandTile(BetterSceneVisibilityBand.Gameplay, typeof(Collider)),
                BandTile(BetterSceneVisibilityBand.Lighting, typeof(Light)),
                BandTile(BetterSceneVisibilityBand.Audio, typeof(AudioSource))
            );
            GUILayout.Space(5f);
            DrawTileRow(
                BandTile(BetterSceneVisibilityBand.UI, typeof(Canvas)),
                BandTile(BetterSceneVisibilityBand.Cameras, typeof(Camera)),
                BandTile(BetterSceneVisibilityBand.Debug, typeof(MonoScript)),
                Tile("RESTORE", "Restore the exact previous visibility state", IconFor(typeof(SceneAsset)), BetterSceneVisibility.Restore, BetterSceneVisibility.HasSnapshot)
            );
            GUILayout.Space(5f);
            DrawSegmented(Segment("SHOW + UNLOCK ALL", false, BetterSceneVisibility.ShowAndUnlockAll));
            GUILayout.Space(6f);
            if (DrawDisclosure("LAYER PRESETS  " + BetterSceneSettings.LayerPresets.Count, layerPresets))
            {
                layerPresets = !layerPresets;
                NotifyContentSizeChanged();
            }
            if (layerPresets)
            {
                GUILayout.Space(4f);
                Rect saveRow = GUILayoutUtility.GetRect(10f, 24f, GUILayout.ExpandWidth(true));
                layerPresetName = EditorGUI.TextField(new Rect(saveRow.x, saveRow.y, saveRow.width - 58f, 22f), layerPresetName, BetterSceneGui.Field);
                if (BetterSceneGui.Button(new Rect(saveRow.xMax - 54f, saveRow.y, 54f, 22f), new GUIContent("SAVE", "Save current layer visibility")))
                {
                    BetterSceneSettings.AddLayerPreset(layerPresetName, Tools.visibleLayers, Tools.lockedLayers);
                    layerPresetName = "LAYERS";
                }
                int presetCount = Mathf.Min(8, BetterSceneSettings.LayerPresets.Count);
                for (int presetIndex = 0; presetIndex < presetCount; presetIndex++)
                {
                    BetterSceneLayerPreset preset = BetterSceneSettings.LayerPresets[presetIndex];
                    Rect row = GUILayoutUtility.GetRect(10f, 24f, GUILayout.ExpandWidth(true));
                    if (BetterSceneGui.Button(new Rect(row.x, row.y, row.width - 30f, 22f), new GUIContent(preset.Name, "Apply layer preset"))) BetterSceneVisibility.ApplyLayerPreset(preset);
                    if (BetterSceneGui.Button(new Rect(row.xMax - 26f, row.y, 26f, 22f), new GUIContent("X", "Delete layer preset")))
                    {
                        BetterSceneSettings.RemoveLayerPreset(preset.Id);
                        break;
                    }
                }
            }
        }

        private static void DrawMeasure()
        {
            BetterSceneMeasurement measurement = BetterSceneController.Measurement;
            Rect card = GUILayoutUtility.GetRect(10f, 72f, GUILayout.ExpandWidth(true));
            BetterSceneGui.Panel(card, true);
            GUI.Label(new Rect(card.x + 10f, card.y + 8f, card.width * 0.4f, 18f), measurement.HasEnd ? "MEASUREMENT" : "LIVE MEASURE", BetterSceneGui.Tiny);
            GUIStyle distance = BetterSceneGui.LargeLabel;
            distance.normal.textColor = DansToolboxTheme.Current.Signal;
            GUI.Label(new Rect(card.x + 10f, card.y + 27f, card.width * 0.42f, 30f), measurement.HasStart ? measurement.Distance.ToString("0.###") + " m" : "--", distance);
            GUIStyle delta = BetterSceneGui.RightLabel;
            delta.normal.textColor = DansToolboxTheme.Current.Text;
            GUI.Label(new Rect(card.x + card.width * 0.42f, card.y + 15f, card.width * 0.55f - 8f, 40f), measurement.HasStart ? FormatVector(measurement.Delta) : "CLICK A START POINT", delta);
            GUILayout.Space(7f);
            GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "SNAP TARGET", BetterSceneGui.Tiny);
            DrawSnapModes();
            GUILayout.Space(6f);
            DrawSegmented(
                Segment("COPY", false, CopyMeasurement, measurement.HasStart),
                Segment("CLEAR", false, BetterSceneController.ClearMeasurement, measurement.HasStart),
                Segment("DONE", false, BetterSceneController.CollapsePanel)
            );
            GUILayout.Space(7f);
            DrawHint(measurement.HasEnd ? "Measurement locked. Click again to begin a new one." : "Click start and end points. Switching tools removes the live guide.");
        }

        private static void DrawReview()
        {
            BetterSceneDiagnosticReport report = BetterSceneDiagnostics.Current;
            GameObject active = Selection.activeGameObject;
            bool hasPrefabSource = active != null && !string.IsNullOrEmpty(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(active));
            DrawMetricCards(
                new Metric("MISSING SCRIPTS", report.MissingScripts, report.MissingScripts > 0),
                new Metric("MISSING REFS", report.MissingReferences, report.MissingReferences > 0),
                new Metric("PREFAB CHANGES", report.PrefabOverrides, false),
                new Metric("INACTIVE", report.InactiveObjects, false)
            );
            GUILayout.Space(7f);
            DrawSegmented(
                Segment("RELATED LOGS", false, () => BetterConsoleDiagnosticBridge.OpenForTargets(Selection.objects), report.Console.Total > 0),
                Segment("INSPECT", false, () => EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Better Inspector"), Selection.objects.Length > 0),
                Segment("PREFAB SOURCE", false, () => BetterSceneOperations.RevealPrefabAsset(active), hasPrefabSource)
            );
            GUILayout.Space(7f);
            GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "SCENE GUIDES", BetterSceneGui.Tiny);
            DrawSegmented(
                Segment("BOUNDS", BetterSceneSettings.DrawSelectionBounds, () => BetterSceneSettings.DrawSelectionBounds = !BetterSceneSettings.DrawSelectionBounds),
                Segment("PIVOTS", BetterSceneSettings.DrawPivot, () => BetterSceneSettings.DrawPivot = !BetterSceneSettings.DrawPivot),
                Segment("BADGES", BetterSceneSettings.DrawDiagnostics, () => BetterSceneSettings.DrawDiagnostics = !BetterSceneSettings.DrawDiagnostics),
                Segment("CHILDREN", BetterSceneSettings.IncludeDescendants, () => BetterSceneSettings.IncludeDescendants = !BetterSceneSettings.IncludeDescendants)
            );
            GUILayout.Space(7f);
            DrawHint(report.HasIssues ? "Use the actions above to investigate selected-object issues." : "No selected-object issues found. Review remains selection-aware.");
        }

        internal static float DesiredHeight(BetterScenePanel panel)
        {
            return CalculateDesiredHeight(
                panel,
                transformExtras,
                savedViews,
                BetterSceneSettings.Bookmarks.Count,
                layerPresets,
                BetterSceneSettings.LayerPresets.Count,
                BetterSceneSettings.GetRecentPlacementAssets().Length > 0);
        }

        internal static float CalculateDesiredHeight(
            BetterScenePanel panel,
            bool showTransformExtras,
            bool showSavedViews,
            int savedViewCount,
            bool showLayerPresets,
            int layerPresetCount,
            bool hasRecentPlacementAssets)
        {
            float height;
            switch (panel)
            {
                case BetterScenePanel.Create: height = 270f; break;
                case BetterScenePanel.Transform: height = showTransformExtras ? 455f : 335f; break;
                case BetterScenePanel.Place: height = hasRecentPlacementAssets ? 405f : 335f; break;
                case BetterScenePanel.View:
                    height = 400f + (showSavedViews ? 28f + Mathf.Min(8, Mathf.Max(0, savedViewCount)) * 24f : 0f);
                    break;
                case BetterScenePanel.Visibility:
                    height = 355f + (showLayerPresets ? 28f + Mathf.Min(8, Mathf.Max(0, layerPresetCount)) * 24f : 0f);
                    break;
                case BetterScenePanel.Measure: height = 280f; break;
                default: height = 290f; break;
            }
            return height;
        }

        private static void NotifyContentSizeChanged()
        {
            panelScroll = Vector2.zero;
            BetterSceneNativeOverlayUtility.SchedulePanelResize();
            SceneView.RepaintAll();
        }

        private static void DrawTileRow(TileAction first, TileAction second, TileAction third)
        {
            tileBuffer[0] = first;
            tileBuffer[1] = second;
            tileBuffer[2] = third;
            DrawTileRow(tileBuffer, 3);
        }

        private static void DrawTileRow(TileAction first, TileAction second, TileAction third, TileAction fourth)
        {
            tileBuffer[0] = first;
            tileBuffer[1] = second;
            tileBuffer[2] = third;
            tileBuffer[3] = fourth;
            DrawTileRow(tileBuffer, 4);
        }

        private static void DrawTileRow(TileAction[] tiles, int count)
        {
            if (tiles == null || count == 0) return;
            Rect row = GUILayoutUtility.GetRect(10f, 62f, GUILayout.ExpandWidth(true));
            float gap = 5f;
            float width = (row.width - gap * (count - 1)) / count;
            for (int index = 0; index < count; index++)
            {
                TileAction tile = tiles[index];
                Rect rect = new Rect(row.x + index * (width + gap), row.y, width, row.height);
                if (DrawTile(rect, tile)) tile.Action?.Invoke();
            }
        }

        private static bool DrawTile(Rect rect, TileAction tile)
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            bool hover = tile.Enabled && rect.Contains(Event.current.mousePosition);
            Color fill = !tile.Enabled ? palette.Inset : tile.Selected ? palette.AccentSoft : hover ? palette.Hover : palette.Raised;
            Color border = !tile.Enabled ? palette.Border : tile.Selected ? palette.Accent : hover ? palette.BorderStrong : palette.Border;
            EditorGUI.DrawRect(rect, fill);
            BetterSceneGui.Border(rect, border);
            if (tile.Icon != null)
            {
                Color previous = GUI.color;
                GUI.color = tile.Enabled ? (tile.Selected ? palette.Accent : palette.Text) : palette.Muted;
                GUI.DrawTexture(new Rect(rect.center.x - 11f, rect.y + 8f, 22f, 22f), tile.Icon, ScaleMode.ScaleToFit, true);
                GUI.color = previous;
            }
            GUIStyle label = BetterSceneGui.CenteredTiny;
            label.normal.textColor = tile.Enabled ? palette.Text : palette.Muted;
            GUI.Label(new Rect(rect.x + 3f, rect.yMax - 23f, rect.width - 6f, 18f), tile.Label, label);
            EditorGUI.BeginDisabledGroup(!tile.Enabled);
            bool clicked = GUI.Button(rect, new GUIContent(string.Empty, tile.Tooltip), GUIStyle.none);
            EditorGUI.EndDisabledGroup();
            if (tile.Enabled) EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            return clicked;
        }

        private static void DrawSnapModes()
        {
            BetterSceneSnapMode current = BetterSceneController.SnapMode;
            DrawSegmented(
                Segment("FREE", current == BetterSceneSnapMode.Free, () => BetterSceneController.SetSnapMode(BetterSceneSnapMode.Free)),
                Segment("GRID", current == BetterSceneSnapMode.Grid, () => BetterSceneController.SetSnapMode(BetterSceneSnapMode.Grid)),
                Segment("SURFACE", current == BetterSceneSnapMode.Surface, () => BetterSceneController.SetSnapMode(BetterSceneSnapMode.Surface)),
                Segment("VERTEX", current == BetterSceneSnapMode.Vertex, () => BetterSceneController.SetSnapMode(BetterSceneSnapMode.Vertex)));
        }

        private static void DrawSegmented(SegmentAction first)
        {
            segmentBuffer[0] = first;
            DrawSegmented(segmentBuffer, 1);
        }

        private static void DrawSegmented(SegmentAction first, SegmentAction second)
        {
            segmentBuffer[0] = first;
            segmentBuffer[1] = second;
            DrawSegmented(segmentBuffer, 2);
        }

        private static void DrawSegmented(SegmentAction first, SegmentAction second, SegmentAction third)
        {
            segmentBuffer[0] = first;
            segmentBuffer[1] = second;
            segmentBuffer[2] = third;
            DrawSegmented(segmentBuffer, 3);
        }

        private static void DrawSegmented(SegmentAction first, SegmentAction second, SegmentAction third, SegmentAction fourth)
        {
            segmentBuffer[0] = first;
            segmentBuffer[1] = second;
            segmentBuffer[2] = third;
            segmentBuffer[3] = fourth;
            DrawSegmented(segmentBuffer, 4);
        }

        private static void DrawSegmented(SegmentAction[] segments, int count)
        {
            if (segments == null || count == 0) return;
            Rect row = GUILayoutUtility.GetRect(10f, 26f, GUILayout.ExpandWidth(true));
            float gap = 4f;
            float width = (row.width - gap * (count - 1)) / count;
            for (int index = 0; index < count; index++)
            {
                SegmentAction segment = segments[index];
                Rect rect = new Rect(row.x + index * (width + gap), row.y, width, row.height);
                if (BetterSceneGui.Button(rect, new GUIContent(segment.Label, segment.Label), segment.Selected, segment.Enabled)) segment.Action?.Invoke();
            }
        }

        private static void DrawAssetStrip(UnityEngine.Object[] assets)
        {
            Rect row = GUILayoutUtility.GetRect(10f, 64f, GUILayout.ExpandWidth(true));
            int count = Mathf.Min(5, assets.Length);
            float gap = 5f;
            float width = (row.width - gap * (count - 1)) / count;
            for (int index = 0; index < count; index++)
            {
                UnityEngine.Object asset = assets[index];
                Texture preview = AssetPreview.GetAssetPreview(asset) ?? AssetPreview.GetMiniThumbnail(asset);
                TileAction tile = Tile(asset.name, "Place " + asset.name, preview, null);
                Rect rect = new Rect(row.x + index * (width + gap), row.y, width, row.height);
                if (DrawTile(rect, tile))
                {
                    BetterSceneSettings.PlacementAsset = asset;
                    BetterSceneController.SetMode(BetterSceneMode.Place);
                }
            }
        }

        private static void DrawMetricCards(Metric first, Metric second, Metric third, Metric fourth)
        {
            metricBuffer[0] = first;
            metricBuffer[1] = second;
            metricBuffer[2] = third;
            metricBuffer[3] = fourth;
            DrawMetricCards(metricBuffer, 4);
        }

        private static void DrawMetricCards(Metric[] metrics, int count)
        {
            Rect row = GUILayoutUtility.GetRect(10f, 64f, GUILayout.ExpandWidth(true));
            float gap = 5f;
            float width = (row.width - gap * (count - 1)) / count;
            DansToolboxPalette palette = DansToolboxTheme.Current;
            for (int index = 0; index < count; index++)
            {
                Metric metric = metrics[index];
                Rect rect = new Rect(row.x + index * (width + gap), row.y, width, row.height);
                BetterSceneGui.Panel(rect, true);
                GUIStyle value = BetterSceneGui.CenteredTitle;
                value.normal.textColor = metric.Error ? palette.Danger : metric.Value > 0 ? palette.Warning : palette.Success;
                GUI.Label(new Rect(rect.x + 3f, rect.y + 7f, rect.width - 6f, 25f), metric.Value.ToString(), value);
                GUIStyle label = BetterSceneGui.CenteredTiny;
                label.normal.textColor = palette.Muted;
                GUI.Label(new Rect(rect.x + 3f, rect.y + 35f, rect.width - 6f, 20f), metric.Label, label);
            }
        }

        private static bool DrawDisclosure(string label, bool expanded)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 24f, GUILayout.ExpandWidth(true));
            return BetterSceneGui.Button(rect, new GUIContent((expanded ? "-  " : "+  ") + label, "Expand or collapse"), expanded);
        }

        private static void DrawHint(string text)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 38f, GUILayout.ExpandWidth(true));
            BetterSceneGui.Panel(rect, true);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, rect.height - 8f), text, BetterSceneGui.WrappedMuted);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), DansToolboxTheme.Current.Signal);
        }

        private static TileAction BandTile(BetterSceneVisibilityBand band, Type iconType)
        {
            return Tile(ShortBand(band), "Show only " + band, IconFor(iconType), () => BetterSceneVisibility.ApplyBand(band));
        }

        private static string ShortBand(BetterSceneVisibilityBand band)
        {
            if (band == BetterSceneVisibilityBand.Environment) return "WORLD";
            if (band == BetterSceneVisibilityBand.Gameplay) return "GAME";
            if (band == BetterSceneVisibilityBand.Lighting) return "LIGHT";
            if (band == BetterSceneVisibilityBand.Cameras) return "CAMERA";
            return band.ToString().ToUpperInvariant();
        }

        private static void CopyMeasurement()
        {
            BetterSceneMeasurement measurement = BetterSceneController.Measurement;
            if (!measurement.HasStart) return;
            EditorGUIUtility.systemCopyBuffer = measurement.Distance.ToString("0.###") + " m | " + FormatVector(measurement.Delta);
        }

        private static Texture PanelIcon(BetterScenePanel panel)
        {
            switch (panel)
            {
                case BetterScenePanel.Create: return IconFor(typeof(GameObject));
                case BetterScenePanel.Transform: return IconFor(typeof(Transform));
                case BetterScenePanel.Place: return IconFor(typeof(MeshFilter));
                case BetterScenePanel.View: return IconFor(typeof(Camera));
                case BetterScenePanel.Visibility: return IconFor(typeof(MeshRenderer));
                case BetterScenePanel.Measure: return IconFor(typeof(BoxCollider));
                case BetterScenePanel.Review: return IconFor(typeof(MonoScript));
                default: return null;
            }
        }

        private static string PanelTitle(BetterScenePanel panel)
        {
            return panel.ToString().ToUpperInvariant();
        }

        private static string PanelDescription(BetterScenePanel panel)
        {
            switch (panel)
            {
                case BetterScenePanel.Create: return "Common objects, one deliberate palette.";
                case BetterScenePanel.Transform: return "Spatial edits relative to the active object.";
                case BetterScenePanel.Place: return "Asset placement with a live Scene preview.";
                case BetterScenePanel.View: return "Viewpoints, cameras, and saved composition.";
                case BetterScenePanel.Visibility: return "Temporary filters with exact restoration.";
                case BetterScenePanel.Measure: return "Two-click world-space distance and delta.";
                case BetterScenePanel.Review: return "Selection-aware diagnostics and guides.";
                default: return string.Empty;
            }
        }

        private static Texture IconFor(Type type)
        {
            GUIContent content = EditorGUIUtility.ObjectContent(null, type);
            return content == null ? null : content.image;
        }

        private static string FormatVector(Vector3 value)
        {
            return value.x.ToString("0.##") + ", " + value.y.ToString("0.##") + ", " + value.z.ToString("0.##");
        }

        private static TileAction Tile(string label, string tooltip, Texture icon, Action action, bool enabled = true, bool selected = false)
        {
            return new TileAction(label, tooltip, icon, action, enabled, selected);
        }

        private static SegmentAction Segment(string label, bool selected, Action action, bool enabled = true)
        {
            return new SegmentAction(label, selected, action, enabled);
        }

        private readonly struct TileAction
        {
            internal TileAction(string label, string tooltip, Texture icon, Action action, bool enabled, bool selected)
            {
                Label = label;
                Tooltip = tooltip;
                Icon = icon;
                Action = action;
                Enabled = enabled;
                Selected = selected;
            }

            internal string Label { get; }
            internal string Tooltip { get; }
            internal Texture Icon { get; }
            internal Action Action { get; }
            internal bool Enabled { get; }
            internal bool Selected { get; }
        }

        private readonly struct SegmentAction
        {
            internal SegmentAction(string label, bool selected, Action action, bool enabled)
            {
                Label = label;
                Selected = selected;
                Action = action;
                Enabled = enabled;
            }

            internal string Label { get; }
            internal bool Selected { get; }
            internal Action Action { get; }
            internal bool Enabled { get; }
        }

        private readonly struct Metric
        {
            internal Metric(string label, int value, bool error)
            {
                Label = label;
                Value = value;
                Error = error;
            }

            internal string Label { get; }
            internal int Value { get; }
            internal bool Error { get; }
        }
    }
}
