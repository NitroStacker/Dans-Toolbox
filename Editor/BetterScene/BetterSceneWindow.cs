using System;
using System.Collections.Generic;
using System.Linq;
using DansToolbox.Editor;
using DansToolbox.EditorTools.BetterConsole;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterScene
{
    public sealed class BetterSceneWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Dans Toolbox/Better Scene";
        private const float ToolbarHeight = 38f;
        private const float StatusHeight = 22f;
        private const string BookmarkControl = "BetterSceneBookmarkName";
        private const string PresetControl = "BetterScenePresetName";

        [SerializeField] private Vector2 scroll;
        [SerializeField] private UnityEngine.Object replacementAsset;
        [SerializeField] private string bookmarkName = "VIEW";
        [SerializeField] private string presetName = "LAYERS";
        [SerializeField] private bool showAdvanced;

        [NonSerialized] private double revealStartedAt;
        [NonSerialized] private string transientStatus = string.Empty;
        [NonSerialized] private double transientStatusUntil;

        [MenuItem(MenuPath, false, 24)]
        internal static void Open()
        {
            BetterSceneWindow window = GetWindow<BetterSceneWindow>();
            window.titleContent = new GUIContent(
                "Better Scene",
                EditorGUIUtility.IconContent("UnityEditor.SceneView").image,
                "Better Scene");
            window.minSize = new Vector2(300f, 300f);
            window.Show();
            window.Focus();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOpen()
        {
            return DansToolboxSettings.IsToolEnabled(DansToolboxTools.BetterSceneId);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Better Scene", EditorGUIUtility.IconContent("UnityEditor.SceneView").image);
            minSize = new Vector2(300f, 300f);
            wantsMouseMove = true;
            revealStartedAt = EditorApplication.timeSinceStartup;
            Selection.selectionChanged -= OnContextChanged;
            Selection.selectionChanged += OnContextChanged;
            BetterSceneController.Changed -= Repaint;
            BetterSceneController.Changed += Repaint;
            BetterSceneSettings.Changed -= Repaint;
            BetterSceneSettings.Changed += Repaint;
            BetterSceneSelectionHistory.Changed -= Repaint;
            BetterSceneSelectionHistory.Changed += Repaint;
            BetterConsoleDiagnosticBridge.Changed -= Repaint;
            BetterConsoleDiagnosticBridge.Changed += Repaint;
            DansToolboxTheme.Changed -= Repaint;
            DansToolboxTheme.Changed += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnContextChanged;
            BetterSceneController.Changed -= Repaint;
            BetterSceneSettings.Changed -= Repaint;
            BetterSceneSelectionHistory.Changed -= Repaint;
            BetterConsoleDiagnosticBridge.Changed -= Repaint;
            DansToolboxTheme.Changed -= Repaint;
        }

        private void OnGUI()
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            Rect canvas = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(canvas, palette.Canvas);
            if (Event.current.type == EventType.MouseMove) Repaint();
            ReleaseTextFocusOnPointerDown();

            if (!DansToolboxSettings.IsToolEnabled(DansToolboxTools.BetterSceneId))
            {
                DrawDisabled(canvas, palette);
                return;
            }

            HandleKeyboard();
            Rect toolbar = new Rect(0f, 0f, position.width, ToolbarHeight);
            Rect status = new Rect(0f, position.height - StatusHeight, position.width, StatusHeight);
            Rect content = new Rect(0f, toolbar.yMax, position.width, Mathf.Max(0f, status.y - toolbar.yMax));
            DrawToolbar(toolbar, palette);
            DrawContent(content, palette);
            DrawStatus(status, palette);
            if (DansToolboxMotion.DrawWindowReveal(canvas, revealStartedAt)) Repaint();
        }

        private void DrawToolbar(Rect rect, DansToolboxPalette palette)
        {
            BetterSceneGui.Panel(rect, false, true);
            float x = 5f;
            float y = 7f;
            if (BetterSceneGui.Button(new Rect(x, y, 25f, 24f), new GUIContent("<", "Selection back"), false, BetterSceneSelectionHistory.CanBack)) BetterSceneSelectionHistory.Back();
            x += 28f;
            if (BetterSceneGui.Button(new Rect(x, y, 25f, 24f), new GUIContent(">", "Selection forward"), false, BetterSceneSelectionHistory.CanForward)) BetterSceneSelectionHistory.Forward();
            x += 32f;

            bool compact = rect.width < 480f;
            float available = rect.width - x - 40f;
            float modeWidth = Mathf.Clamp((available - 9f) / 4f, 30f, compact ? 58f : 74f);
            foreach (BetterSceneMode candidate in Enum.GetValues(typeof(BetterSceneMode)))
            {
                string text = compact ? candidate.ToString().Substring(0, 1) : candidate.ToString().ToUpperInvariant();
                if (BetterSceneGui.Button(
                        new Rect(x, y, modeWidth, 24f),
                        new GUIContent(text, candidate + " mode · Alt+" + ((int)candidate + 1)),
                        BetterSceneController.Mode == candidate))
                {
                    BetterSceneController.SetMode(candidate);
                    GUI.FocusControl(null);
                }
                x += modeWidth + 3f;
            }

            Rect menuRect = new Rect(rect.xMax - 31f, y, 26f, 24f);
            if (BetterSceneGui.Button(menuRect, new GUIContent("...", "View and tool options"))) ShowOptionsMenu(menuRect);
        }

        private void DrawContent(Rect rect, DansToolboxPalette palette)
        {
            GUILayout.BeginArea(rect);
            scroll = GUILayout.BeginScrollView(scroll, false, true);
            GUILayout.Space(10f);
            DrawSelectionCard(palette);
            GUILayout.Space(10f);
            switch (BetterSceneController.Mode)
            {
                case BetterSceneMode.Place:
                    DrawPlaceMode(palette);
                    break;
                case BetterSceneMode.Measure:
                    DrawMeasureMode(palette);
                    break;
                case BetterSceneMode.Review:
                    DrawReviewMode(palette);
                    break;
                default:
                    DrawSelectMode(palette);
                    break;
            }
            GUILayout.Space(16f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSelectionCard(DansToolboxPalette palette)
        {
            GameObject active = Selection.activeGameObject;
            GameObject[] selected = Selection.gameObjects;
            Rect card = GUILayoutUtility.GetRect(10f, active == null ? 72f : 94f, GUILayout.ExpandWidth(true));
            card.x += 8f;
            card.width -= 17f;
            BetterSceneGui.Panel(card, false, true);
            EditorGUI.DrawRect(new Rect(card.x, card.y, 3f, card.height), active == null ? palette.BorderStrong : palette.Accent);
            if (active == null)
            {
                GUI.Label(new Rect(card.x + 14f, card.y + 13f, card.width - 28f, 22f), "NO SELECTION", BetterSceneGui.Title);
                GUI.Label(new Rect(card.x + 14f, card.y + 37f, card.width - 28f, 18f), "Pick an object in Scene or Better Hierarchy.", BetterSceneGui.Muted);
                return;
            }

            BetterSceneDiagnosticReport report = BetterSceneDiagnostics.Current;
            GUI.Label(new Rect(card.x + 14f, card.y + 9f, card.width - 76f, 22f), active.name, BetterSceneGui.Title);
            GUIStyle badgeStyle = new GUIStyle(BetterSceneGui.Centered)
            {
                normal = { textColor = report.Errors > 0 ? palette.Danger : report.Warnings > 0 ? palette.Warning : palette.Success }
            };
            Rect badge = new Rect(card.xMax - 54f, card.y + 9f, 40f, 20f);
            EditorGUI.DrawRect(badge, palette.Inset);
            BetterSceneGui.Border(badge, report.Errors > 0 ? palette.Danger : report.Warnings > 0 ? palette.Warning : palette.Success);
            GUI.Label(badge, report.Badge, badgeStyle);
            string path = GetPath(active.transform);
            GUI.Label(new Rect(card.x + 14f, card.y + 32f, card.width - 28f, 18f),
                selected.Length > 1 ? selected.Length + " SELECTED  ·  " + path : path,
                BetterSceneGui.Muted);

            float x = card.x + 14f;
            float y = card.yMax - 31f;
            if (BetterSceneGui.Button(new Rect(x, y, 52f, 22f), new GUIContent("FOCUS", "Frame selection · F"))) BetterSceneOperations.FrameSelection();
            x += 55f;
            if (BetterSceneGui.Button(new Rect(x, y, 52f, 22f), new GUIContent("SOLO", "Isolate selection"), BetterSceneVisibility.IsIsolating)) BetterSceneVisibility.ToggleIsolation(selected);
            x += 55f;
            if (BetterSceneGui.Button(new Rect(x, y, 45f, 22f), new GUIContent("HIDE", "Hide selection"))) BetterSceneVisibility.ToggleHidden(selected);
            x += 48f;
            if (BetterSceneGui.Button(new Rect(x, y, 45f, 22f), new GUIContent("LOCK", "Toggle Scene picking"))) BetterSceneVisibility.TogglePicking(selected);
            x += 48f;
            if (x + 48f <= card.xMax - 10f && BetterSceneGui.Button(
                    new Rect(x, y, 48f, 22f),
                    new GUIContent("LOGS", report.Console.Tooltip),
                    report.Console.HasSignals,
                    report.Console.Total > 0,
                    report.Console.Errors > 0 ? palette.Danger : palette.Warning))
            {
                BetterConsoleDiagnosticBridge.OpenForTargets(Selection.objects);
            }
        }

        private void DrawSelectMode(DansToolboxPalette palette)
        {
            DrawSection("PRECISION", null, palette);
            DrawAlignGrid();
            GUILayout.Space(4f);
            DrawActionRow(new[]
            {
                new ActionItem("DIST X", "Even spacing on X", Selection.gameObjects.Length >= 3, () => BetterSceneOperations.DistributeSelection(BetterSceneAxis.X)),
                new ActionItem("DIST Y", "Even spacing on Y", Selection.gameObjects.Length >= 3, () => BetterSceneOperations.DistributeSelection(BetterSceneAxis.Y)),
                new ActionItem("DIST Z", "Even spacing on Z", Selection.gameObjects.Length >= 3, () => BetterSceneOperations.DistributeSelection(BetterSceneAxis.Z)),
                new ActionItem("GROUND", "Drop to the first surface below", Selection.gameObjects.Length > 0, BetterSceneOperations.GroundSelection)
            });
            GUILayout.Space(4f);
            DrawActionRow(new[]
            {
                new ActionItem("SNAP P", "Snap position", Selection.transforms.Length > 0, () => BetterSceneOperations.SnapSelection(true, false, false)),
                new ActionItem("SNAP R", "Snap rotation", Selection.transforms.Length > 0, () => BetterSceneOperations.SnapSelection(false, true, false)),
                new ActionItem("SNAP S", "Snap scale", Selection.transforms.Length > 0, () => BetterSceneOperations.SnapSelection(false, false, true)),
                new ActionItem("SNAP ALL", "Snap transform", Selection.transforms.Length > 0, () => BetterSceneOperations.SnapSelection(true, true, true))
            });
            DrawScatter(palette);

            GUILayout.Space(12f);
            DrawVisibility(palette);
            GUILayout.Space(12f);
            DrawViews(palette);
            GUILayout.Space(12f);
            DrawReplace(palette);
        }

        private void DrawAlignGrid()
        {
            bool enabled = Selection.gameObjects.Length >= 2 && Selection.activeGameObject != null;
            foreach (BetterSceneAxis axis in Enum.GetValues(typeof(BetterSceneAxis)))
            {
                DrawActionRow(new[]
                {
                    new ActionItem(axis + " MIN", "Align minimum bounds to active", enabled, () => BetterSceneOperations.AlignSelection(axis, BetterSceneAlignAnchor.Minimum)),
                    new ActionItem(axis + " MID", "Align centers to active", enabled, () => BetterSceneOperations.AlignSelection(axis, BetterSceneAlignAnchor.Center)),
                    new ActionItem(axis + " MAX", "Align maximum bounds to active", enabled, () => BetterSceneOperations.AlignSelection(axis, BetterSceneAlignAnchor.Maximum))
                });
                GUILayout.Space(3f);
            }
        }

        private void DrawScatter(DansToolboxPalette palette)
        {
            GUILayout.Space(7f);
            Rect header = GUILayoutUtility.GetRect(10f, 22f, GUILayout.ExpandWidth(true));
            header.x += 8f;
            header.width -= 17f;
            if (BetterSceneGui.Button(header, new GUIContent(showAdvanced ? "−  SCATTER / REPLACE" : "+  SCATTER / REPLACE", "Advanced spatial actions"), showAdvanced))
            {
                showAdvanced = !showAdvanced;
                GUI.FocusControl(null);
            }
            if (!showAdvanced) return;
            GUILayout.Space(4f);
            using (new EditorGUI.IndentLevelScope())
            {
                float radius = EditorGUILayout.FloatField("Radius", BetterSceneSettings.ScatterRadius);
                float height = EditorGUILayout.FloatField("Height", BetterSceneSettings.ScatterHeight);
                int seed = EditorGUILayout.IntField("Seed", BetterSceneSettings.ScatterSeed);
                if (!Mathf.Approximately(radius, BetterSceneSettings.ScatterRadius)) BetterSceneSettings.ScatterRadius = radius;
                if (!Mathf.Approximately(height, BetterSceneSettings.ScatterHeight)) BetterSceneSettings.ScatterHeight = height;
                if (seed != BetterSceneSettings.ScatterSeed) BetterSceneSettings.ScatterSeed = seed;
            }
            DrawActionRow(new[]
            {
                new ActionItem("SCATTER", "Scatter selected objects with deterministic seed", Selection.transforms.Length > 0,
                    () => BetterSceneOperations.ScatterSelection(BetterSceneSettings.ScatterRadius, BetterSceneSettings.ScatterHeight, BetterSceneSettings.ScatterSeed)),
                new ActionItem("GROUND AFTER", "Ground after scattering", Selection.transforms.Length > 0, () =>
                {
                    BetterSceneOperations.ScatterSelection(BetterSceneSettings.ScatterRadius, BetterSceneSettings.ScatterHeight, BetterSceneSettings.ScatterSeed);
                    BetterSceneOperations.GroundSelection();
                })
            });
        }

        private void DrawVisibility(DansToolboxPalette palette)
        {
            DrawSection("VISIBILITY", BetterSceneVisibility.HasSnapshot ? "FILTERED" : "LIVE", palette);
            BetterSceneVisibilityBand[] bands =
            {
                BetterSceneVisibilityBand.Environment, BetterSceneVisibilityBand.Gameplay,
                BetterSceneVisibilityBand.Lighting, BetterSceneVisibilityBand.Audio,
                BetterSceneVisibilityBand.UI, BetterSceneVisibilityBand.Cameras,
                BetterSceneVisibilityBand.Debug
            };
            for (int index = 0; index < bands.Length; index += 4)
            {
                DrawActionRow(bands.Skip(index).Take(4).Select(band =>
                    new ActionItem(ShortBand(band), "Show " + band, true, () => BetterSceneVisibility.ApplyBand(band))).ToArray());
                GUILayout.Space(3f);
            }
            DrawActionRow(new[]
            {
                new ActionItem("RESTORE", "Restore visibility before the filter", BetterSceneVisibility.HasSnapshot, BetterSceneVisibility.Restore),
                new ActionItem("SHOW ALL", "Show and unlock every object", true, BetterSceneVisibility.ShowAndUnlockAll)
            });

            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName(PresetControl);
            presetName = EditorGUILayout.TextField(presetName, BetterSceneGui.Field);
            if (MiniButton("SAVE", "Save current visible and locked layer masks", true, 54f))
            {
                BetterSceneSettings.AddLayerPreset(presetName, Tools.visibleLayers, Tools.lockedLayers);
                presetName = "LAYERS";
                ClearTextFocus();
            }
            EditorGUILayout.EndHorizontal();
            foreach (BetterSceneLayerPreset preset in BetterSceneSettings.LayerPresets.ToArray())
            {
                EditorGUILayout.BeginHorizontal();
                if (MiniButton(preset.Name, "Apply layer preset", true, Mathf.Max(80f, position.width - 76f))) BetterSceneVisibility.ApplyLayerPreset(preset);
                if (MiniButton("X", "Delete preset", true, 24f)) BetterSceneSettings.RemoveLayerPreset(preset.Id);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawViews(DansToolboxPalette palette)
        {
            DrawSection("VIEWS", BetterSceneSettings.Bookmarks.Count + " SAVED", palette);
            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName(BookmarkControl);
            bookmarkName = EditorGUILayout.TextField(bookmarkName, BetterSceneGui.Field);
            if (MiniButton("SAVE", "Save the current Scene camera", SceneView.lastActiveSceneView != null, 54f))
            {
                BetterSceneOperations.CaptureBookmark(bookmarkName);
                bookmarkName = "VIEW";
                ClearTextFocus();
                Flash("VIEW SAVED");
            }
            EditorGUILayout.EndHorizontal();

            foreach (BetterSceneBookmark bookmark in BetterSceneSettings.Bookmarks.ToArray())
            {
                EditorGUILayout.BeginHorizontal();
                Rect fieldRect = GUILayoutUtility.GetRect(70f, 22f, GUILayout.ExpandWidth(true));
                string renamed = EditorGUI.DelayedTextField(fieldRect, bookmark.Name, BetterSceneGui.Field);
                if (!string.Equals(renamed, bookmark.Name, StringComparison.Ordinal)) BetterSceneSettings.RenameBookmark(bookmark.Id, renamed);
                if (MiniButton("GO", "Restore view" + (string.IsNullOrEmpty(bookmark.ScenePath) ? string.Empty : " · " + bookmark.ScenePath), true, 34f)) BetterSceneOperations.RestoreBookmark(bookmark);
                if (MiniButton("X", "Delete view", true, 24f)) BetterSceneSettings.RemoveBookmark(bookmark.Id);
                EditorGUILayout.EndHorizontal();
            }

            Camera[] cameras = SceneView.GetAllSceneCameras();
            if (cameras.Length > 0)
            {
                GUILayout.Space(5f);
                GUI.Label(GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true)), "SCENE CAMERAS", BetterSceneGui.Tiny);
                foreach (Camera camera in cameras.Take(12))
                {
                    if (camera != null && MiniButton(camera.name, "Align Scene view to camera", true)) BetterSceneOperations.ViewThrough(camera);
                }
            }
        }

        private void DrawReplace(DansToolboxPalette palette)
        {
            DrawSection("REPLACE", "UNDOABLE", palette);
            EditorGUILayout.BeginHorizontal();
            replacementAsset = EditorGUILayout.ObjectField(replacementAsset, typeof(GameObject), false);
            if (MiniButton("REPLACE", "Replace selection while preserving transforms and hierarchy", replacementAsset is GameObject && Selection.gameObjects.Length > 0, 72f))
            {
                BetterSceneOperations.ReplaceSelection(replacementAsset);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlaceMode(DansToolboxPalette palette)
        {
            DrawSection("ASSET", "DRAG TO SCENE", palette);
            UnityEngine.Object current = BetterSceneSettings.PlacementAsset;
            UnityEngine.Object next = EditorGUILayout.ObjectField(current, typeof(UnityEngine.Object), false);
            if (next != current)
            {
                if (next == null || BetterSceneController.CanPlaceAsset(next)) BetterSceneSettings.PlacementAsset = next;
                else Flash("USE PREFAB · SPRITE · MESH · AUDIO");
            }
            GUILayout.Space(8f);
            DrawSection("SNAP", BetterSceneController.SnapMode.ToString().ToUpperInvariant(), palette);
            DrawSnapModes();
            GUILayout.Space(8f);
            BetterSceneSettings.AlignToSurface = DrawToggle("ALIGN NORMAL", "Orient placed objects to the hit surface", BetterSceneSettings.AlignToSurface);
            BetterSceneSettings.ParentToSurface = DrawToggle("PARENT", "Parent under the hit object", BetterSceneSettings.ParentToSurface);
            BetterSceneSettings.KeepPlacing = DrawToggle("REPEAT", "Keep placement active after each click", BetterSceneSettings.KeepPlacing);
            GUILayout.Space(12f);
            DrawHint(current == null
                ? "Pick an asset here or drag one from Better Project into Scene."
                : "Move across Scene for a preview. Click to place. Esc returns to Select.", palette);
        }

        private void DrawMeasureMode(DansToolboxPalette palette)
        {
            DrawSection("MEASURE", BetterSceneController.Measurement.HasEnd ? "LOCKED" : "LIVE", palette);
            DrawSnapModes();
            GUILayout.Space(8f);
            BetterSceneMeasurement measurement = BetterSceneController.Measurement;
            DrawMetric("DIST", measurement.HasStart ? measurement.Distance.ToString("0.###") + " m" : "—", palette.Signal);
            DrawMetric("DELTA", measurement.HasStart ? FormatVector(measurement.Delta) : "—", palette.Accent);
            if (measurement.HasStart)
            {
                DrawMetric("START", FormatVector(measurement.Start), palette.Muted);
                DrawMetric("END", FormatVector(measurement.End), palette.Muted);
            }
            GUILayout.Space(6f);
            DrawActionRow(new[]
            {
                new ActionItem("COPY", "Copy distance and delta", measurement.HasStart, () =>
                {
                    EditorGUIUtility.systemCopyBuffer = measurement.Distance.ToString("0.###") + " m · " + FormatVector(measurement.Delta);
                    Flash("MEASUREMENT COPIED");
                }),
                new ActionItem("CLEAR", "Clear measurement", measurement.HasStart, BetterSceneController.ClearMeasurement)
            });
            GUILayout.Space(12f);
            DrawHint("Click a start point, then an end point. A third click begins again. Esc clears.", palette);
        }

        private void DrawReviewMode(DansToolboxPalette palette)
        {
            BetterSceneDiagnosticReport report = BetterSceneDiagnostics.Current;
            DrawSection("DIAGNOSTICS", report.HasIssues ? report.Badge : "CLEAN", palette);
            DrawMetric("MISSING SCRIPTS", report.MissingScripts.ToString(), report.MissingScripts > 0 ? palette.Danger : palette.Muted);
            DrawMetric("MISSING REFS", report.MissingReferences.ToString(), report.MissingReferences > 0 ? palette.Danger : palette.Muted);
            DrawMetric("PREFAB OVERRIDES", report.PrefabOverrides.ToString(), report.PrefabOverrides > 0 ? palette.Warning : palette.Muted);
            DrawMetric("INACTIVE", report.InactiveObjects.ToString(), report.InactiveObjects > 0 ? palette.Warning : palette.Muted);
            DrawMetric("CONSOLE", report.Console.Errors + " E  ·  " + report.Console.Warnings + " W  ·  " + report.Console.Logs + " L",
                report.Console.Errors > 0 ? palette.Danger : report.Console.Warnings > 0 ? palette.Warning : palette.Muted);
            GUILayout.Space(7f);
            DrawActionRow(new[]
            {
                new ActionItem("LOGS", "Open related Better Console entries", report.Console.Total > 0, () => BetterConsoleDiagnosticBridge.OpenForTargets(Selection.objects)),
                new ActionItem("INSPECT", "Open Better Inspector", Selection.objects.Length > 0, () => EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Better Inspector")),
                new ActionItem("PREFAB", "Reveal prefab source in Better Project", Selection.activeGameObject != null, () => BetterSceneOperations.RevealPrefabAsset(Selection.activeGameObject))
            });
            GUILayout.Space(12f);
            DrawSection("OVERLAY", null, palette);
            BetterSceneSettings.DrawSelectionBounds = DrawToggle("BOUNDS", "Draw selected bounds", BetterSceneSettings.DrawSelectionBounds);
            BetterSceneSettings.DrawPivot = DrawToggle("PIVOTS", "Draw transform pivots", BetterSceneSettings.DrawPivot);
            BetterSceneSettings.DrawDiagnostics = DrawToggle("BADGES", "Draw diagnostics in Scene", BetterSceneSettings.DrawDiagnostics);
            BetterSceneSettings.IncludeDescendants = DrawToggle("CHILDREN", "Include descendants in diagnostics and spatial actions", BetterSceneSettings.IncludeDescendants);
        }

        private void DrawSnapModes()
        {
            DrawActionRow(Enum.GetValues(typeof(BetterSceneSnapMode)).Cast<BetterSceneSnapMode>().Select(candidate =>
                new ActionItem(candidate.ToString().ToUpperInvariant(), candidate + " placement and measurement", true,
                    () => BetterSceneController.SetSnapMode(candidate), BetterSceneController.SnapMode == candidate)).ToArray());
        }

        private void DrawSection(string title, string badge, DansToolboxPalette palette)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 25f, GUILayout.ExpandWidth(true));
            rect.x += 8f;
            rect.width -= 17f;
            BetterSceneGui.SectionHeader(rect, title, badge, palette.Accent);
            GUILayout.Space(4f);
        }

        private void DrawMetric(string label, string value, Color color)
        {
            Rect row = GUILayoutUtility.GetRect(10f, 23f, GUILayout.ExpandWidth(true));
            row.x += 8f;
            row.width -= 17f;
            if (Event.current.type == EventType.Repaint && row.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(row, DansToolboxTheme.Current.Hover);
            GUI.Label(new Rect(row.x + 5f, row.y, row.width * 0.48f, row.height), label, BetterSceneGui.Tiny);
            GUIStyle right = new GUIStyle(BetterSceneGui.Label) { alignment = TextAnchor.MiddleRight, normal = { textColor = color } };
            GUI.Label(new Rect(row.x + row.width * 0.45f, row.y, row.width * 0.55f - 5f, row.height), value, right);
        }

        private bool DrawToggle(string label, string tooltip, bool value)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 25f, GUILayout.ExpandWidth(true));
            rect.x += 8f;
            rect.width -= 17f;
            if (BetterSceneGui.Button(rect, new GUIContent((value ? "●  " : "○  ") + label, tooltip), value)) value = !value;
            return value;
        }

        private void DrawHint(string message, DansToolboxPalette palette)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 54f, GUILayout.ExpandWidth(true));
            rect.x += 8f;
            rect.width -= 17f;
            BetterSceneGui.Panel(rect, true);
            GUIStyle wrapped = new GUIStyle(BetterSceneGui.Muted) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(rect.x + 12f, rect.y + 7f, rect.width - 24f, rect.height - 14f), message, wrapped);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), palette.Signal);
        }

        private void DrawActionRow(IReadOnlyList<ActionItem> items)
        {
            if (items == null || items.Count == 0) return;
            Rect row = GUILayoutUtility.GetRect(10f, 25f, GUILayout.ExpandWidth(true));
            row.x += 8f;
            row.width -= 17f;
            float gap = 3f;
            float width = (row.width - gap * (items.Count - 1)) / items.Count;
            for (int index = 0; index < items.Count; index++)
            {
                ActionItem item = items[index];
                Rect button = new Rect(row.x + index * (width + gap), row.y, width, row.height);
                if (BetterSceneGui.Button(button, new GUIContent(item.Label, item.Tooltip), item.Selected, item.Enabled)) item.Action?.Invoke();
            }
        }

        private bool MiniButton(string label, string tooltip, bool enabled, float width = -1f)
        {
            Rect rect = width > 0f
                ? GUILayoutUtility.GetRect(width, 22f, GUILayout.Width(width))
                : GUILayoutUtility.GetRect(70f, 22f, GUILayout.ExpandWidth(true));
            return BetterSceneGui.Button(rect, new GUIContent(label, tooltip), false, enabled);
        }

        private void DrawStatus(Rect rect, DansToolboxPalette palette)
        {
            EditorGUI.DrawRect(rect, palette.Inset);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), palette.Border);
            string left = !string.IsNullOrEmpty(transientStatus) && EditorApplication.timeSinceStartup < transientStatusUntil
                ? transientStatus
                : Selection.gameObjects.Length == 0 ? "READY" : Selection.gameObjects.Length + " SELECTED";
            GUI.Label(new Rect(8f, rect.y + 1f, rect.width - 160f, rect.height - 2f), left, BetterSceneGui.Tiny);
            GUIStyle right = new GUIStyle(BetterSceneGui.Tiny) { alignment = TextAnchor.MiddleRight, normal = { textColor = palette.Accent } };
            GUI.Label(new Rect(rect.xMax - 170f, rect.y + 1f, 162f, rect.height - 2f),
                BetterSceneController.Mode.ToString().ToUpperInvariant() + "  ·  " + BetterSceneController.SnapMode.ToString().ToUpperInvariant(), right);
            if (!string.IsNullOrEmpty(transientStatus) && EditorApplication.timeSinceStartup < transientStatusUntil) Repaint();
        }

        private void DrawDisabled(Rect canvas, DansToolboxPalette palette)
        {
            Rect panel = new Rect(
                Mathf.Max(18f, canvas.center.x - 150f),
                Mathf.Max(18f, canvas.center.y - 58f),
                Mathf.Min(300f, canvas.width - 36f),
                116f);
            BetterSceneGui.Panel(panel, false, true);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 16f, panel.width - 28f, 24f), "BETTER SCENE OFF", BetterSceneGui.Title);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 42f, panel.width - 28f, 20f), "Enable it in Toolbox Setup.", BetterSceneGui.Muted);
            if (BetterSceneGui.Button(new Rect(panel.x + 14f, panel.yMax - 40f, 92f, 24f), new GUIContent("SETUP", "Open Toolbox Setup")))
            {
                EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Setup Wizard");
            }
        }

        private void ShowOptionsMenu(Rect activator)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Scene overlay"), BetterSceneSettings.OverlayVisible, () => BetterSceneSettings.OverlayVisible = !BetterSceneSettings.OverlayVisible);
            menu.AddItem(new GUIContent("Selection bounds"), BetterSceneSettings.DrawSelectionBounds, () => BetterSceneSettings.DrawSelectionBounds = !BetterSceneSettings.DrawSelectionBounds);
            menu.AddItem(new GUIContent("Pivots"), BetterSceneSettings.DrawPivot, () => BetterSceneSettings.DrawPivot = !BetterSceneSettings.DrawPivot);
            menu.AddItem(new GUIContent("Diagnostics"), BetterSceneSettings.DrawDiagnostics, () => BetterSceneSettings.DrawDiagnostics = !BetterSceneSettings.DrawDiagnostics);
            menu.AddItem(new GUIContent("Include descendants"), BetterSceneSettings.IncludeDescendants, () => BetterSceneSettings.IncludeDescendants = !BetterSceneSettings.IncludeDescendants);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Restore visibility"), false, BetterSceneVisibility.Restore);
            menu.AddItem(new GUIContent("Show and unlock all"), false, BetterSceneVisibility.ShowAndUnlockAll);
            menu.AddItem(new GUIContent("Clear selection history"), false, BetterSceneSelectionHistory.Clear);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Native Scene view"), false, () => EditorApplication.ExecuteMenuItem("Window/General/Scene"));
            menu.AddItem(new GUIContent("Toolbox Setup"), false, () => EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Setup Wizard"));
            menu.DropDown(activator);
        }

        private void ReleaseTextFocusOnPointerDown()
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || string.IsNullOrEmpty(GUI.GetNameOfFocusedControl())) return;

            // Clear before IMGUI dispatches the click. A clicked text field will
            // immediately reclaim focus; every other surface reliably releases it.
            ClearTextFocus();
        }

        private void ClearTextFocus()
        {
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            Repaint();
        }

        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;
            if (current.keyCode == KeyCode.F && !current.control && !current.command && !current.alt)
            {
                BetterSceneOperations.FrameSelection();
                current.Use();
            }
        }

        private void Flash(string message)
        {
            transientStatus = message ?? string.Empty;
            transientStatusUntil = EditorApplication.timeSinceStartup + 1.8d;
            Repaint();
        }

        private void OnContextChanged()
        {
            BetterSceneDiagnostics.Invalidate();
            Repaint();
        }

        private static string ShortBand(BetterSceneVisibilityBand band)
        {
            switch (band)
            {
                case BetterSceneVisibilityBand.Environment: return "WORLD";
                case BetterSceneVisibilityBand.Gameplay: return "GAME";
                case BetterSceneVisibilityBand.Lighting: return "LIGHT";
                case BetterSceneVisibilityBand.Cameras: return "CAM";
                default: return band.ToString().ToUpperInvariant();
            }
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null) return string.Empty;
            var names = new Stack<string>();
            Transform cursor = transform;
            while (cursor != null)
            {
                names.Push(cursor.name);
                cursor = cursor.parent;
            }
            return transform.gameObject.scene.name + " / " + string.Join(" / ", names);
        }

        private static string FormatVector(Vector3 value)
        {
            return value.x.ToString("0.##") + ", " + value.y.ToString("0.##") + ", " + value.z.ToString("0.##");
        }

        private readonly struct ActionItem
        {
            internal ActionItem(string label, string tooltip, bool enabled, Action action, bool selected = false)
            {
                Label = label;
                Tooltip = tooltip;
                Enabled = enabled;
                Action = action;
                Selected = selected;
            }

            internal string Label { get; }
            internal string Tooltip { get; }
            internal bool Enabled { get; }
            internal Action Action { get; }
            internal bool Selected { get; }
        }
    }
}
