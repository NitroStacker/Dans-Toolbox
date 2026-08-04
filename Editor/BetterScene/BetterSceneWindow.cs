using System;
using System.Collections.Generic;
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
        [SerializeField] private Vector2 scroll;
        [SerializeField] private bool showActiveTool = true;
        [SerializeField] private bool showSavedViews;
        [SerializeField] private bool showSceneHealth;
        [NonSerialized] private double revealStartedAt;
        [NonSerialized] private double nextHoverUpdateAt;

        [MenuItem(MenuPath, false, 24)]
        internal static void Open()
        {
            BetterSceneWindow window = GetWindow<BetterSceneWindow>();
            DansToolboxWindowChrome.ApplyCompactTitle(
                window,
                DansToolboxTools.BetterSceneId);
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
            DansToolboxWindowChrome.ApplyCompactTitle(
                this,
                DansToolboxTools.BetterSceneId);
            minSize = new Vector2(300f, 300f);
            wantsMouseMove = true;
            revealStartedAt = EditorApplication.timeSinceStartup;
            Selection.selectionChanged -= Repaint;
            Selection.selectionChanged += Repaint;
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
            Selection.selectionChanged -= Repaint;
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
            if (Event.current.type == EventType.MouseMove)
            {
                double now = EditorApplication.timeSinceStartup;
                if (now >= nextHoverUpdateAt)
                {
                    nextHoverUpdateAt = now + 1d / 60d;
                    Repaint();
                }
            }
            ReleaseTextFocusOnPointerDown();

            if (!DansToolboxSettings.IsToolEnabled(DansToolboxTools.BetterSceneId))
            {
                DrawDisabled(canvas, palette);
                return;
            }

            HandleKeyboard();
            Rect toolbar = new Rect(0f, 0f, position.width, ToolbarHeight);
            Rect content = new Rect(0f, toolbar.yMax, position.width, Mathf.Max(0f, position.height - toolbar.yMax));
            DrawToolbar(toolbar, palette);
            DrawContent(content, palette);
            if (DansToolboxMotion.DrawWindowReveal(canvas, revealStartedAt)) Repaint();
        }

        private void DrawToolbar(Rect rect, DansToolboxPalette palette)
        {
            BetterSceneGui.Panel(rect, false, true);
            float x = 6f;
            if (BetterSceneGui.Button(new Rect(x, 7f, 25f, 24f), new GUIContent("<", "Selection back"), false, BetterSceneSelectionHistory.CanBack)) BetterSceneSelectionHistory.Back();
            x += 28f;
            if (BetterSceneGui.Button(new Rect(x, 7f, 25f, 24f), new GUIContent(">", "Selection forward"), false, BetterSceneSelectionHistory.CanForward)) BetterSceneSelectionHistory.Forward();
            x += 34f;
            GUI.Label(new Rect(x, 7f, rect.width - x - 78f, 24f), "SCENE WORKSPACE", BetterSceneGui.Tiny);
            Rect scene = new Rect(rect.xMax - 66f, 7f, 32f, 24f);
            if (BetterSceneGui.Button(scene, new GUIContent("SCN", "Focus the native Scene tab"))) FocusScene();
            Rect menu = new Rect(rect.xMax - 31f, 7f, 26f, 24f);
            if (BetterSceneGui.Button(menu, new GUIContent("...", "Scene workspace options"))) ShowOptionsMenu(menu);
        }

        private void DrawContent(Rect rect, DansToolboxPalette palette)
        {
            GUILayout.BeginArea(rect);
            scroll = GUILayout.BeginScrollView(scroll, false, true);
            GUILayout.Space(10f);
            DrawSelectionCard(palette);
            GUILayout.Space(12f);
            DrawSectionLabel("SCENE TOOLS", "OPEN IN SCENE", palette);
            DrawToolGrid();
            GUILayout.Space(10f);

            if (DrawDisclosure("ACTIVE TOOL", BetterSceneController.ActivePanel.ToString().ToUpperInvariant(), showActiveTool)) showActiveTool = !showActiveTool;
            if (showActiveTool) DrawActiveTool(palette);
            GUILayout.Space(6f);

            if (DrawDisclosure("SAVED VIEWS", BetterSceneSettings.Bookmarks.Count.ToString(), showSavedViews)) showSavedViews = !showSavedViews;
            if (showSavedViews) DrawSavedViews();
            GUILayout.Space(6f);

            BetterSceneDiagnosticReport report = BetterSceneDiagnostics.Current;
            if (DrawDisclosure("SCENE HEALTH", report.HasIssues ? report.Badge : "CLEAN", showSceneHealth)) showSceneHealth = !showSceneHealth;
            if (showSceneHealth) DrawSceneHealth(report, palette);
            GUILayout.Space(16f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSelectionCard(DansToolboxPalette palette)
        {
            GameObject active = Selection.activeGameObject;
            GameObject[] selected = Selection.gameObjects;
            Rect card = Inset(GUILayoutUtility.GetRect(10f, active == null ? 68f : 86f, GUILayout.ExpandWidth(true)));
            BetterSceneGui.Panel(card, false, true);
            EditorGUI.DrawRect(new Rect(card.x, card.y, 3f, card.height), active == null ? palette.BorderStrong : palette.Accent);
            if (active == null)
            {
                GUI.Label(new Rect(card.x + 14f, card.y + 11f, card.width - 28f, 22f), "NO SELECTION", BetterSceneGui.Title);
                GUI.Label(new Rect(card.x + 14f, card.y + 34f, card.width - 28f, 18f), "Choose an object in Scene or Better Hierarchy.", BetterSceneGui.Muted);
                return;
            }

            BetterSceneDiagnosticReport report = BetterSceneDiagnostics.Current;
            GUI.Label(new Rect(card.x + 14f, card.y + 9f, card.width - 110f, 22f), active.name, BetterSceneGui.Title);
            GUI.Label(new Rect(card.x + 14f, card.y + 31f, card.width - 110f, 18f), selected.Length == 1 ? GetPath(active.transform) : selected.Length + " OBJECTS SELECTED", BetterSceneGui.Muted);
            Rect badge = new Rect(card.xMax - 50f, card.y + 10f, 36f, 20f);
            BetterSceneGui.Panel(badge, true);
            GUIStyle badgeStyle = BetterSceneGui.Centered;
            badgeStyle.normal.textColor = report.Errors > 0 ? palette.Danger : report.Warnings > 0 ? palette.Warning : palette.Success;
            GUI.Label(badge, report.Badge, badgeStyle);

            float y = card.yMax - 29f;
            float x = card.x + 14f;
            if (BetterSceneGui.Button(new Rect(x, y, 58f, 21f), new GUIContent("FOCUS", "Frame selection"))) BetterSceneOperations.FrameSelection();
            x += 62f;
            if (BetterSceneGui.Button(new Rect(x, y, 52f, 21f), new GUIContent("SOLO", "Isolate selection"), BetterSceneVisibility.IsIsolating)) BetterSceneVisibility.ToggleIsolation(selected);
            x += 56f;
            if (x + 52f <= card.xMax - 12f && BetterSceneGui.Button(new Rect(x, y, 52f, 21f), new GUIContent("LOGS", report.Console.Tooltip), report.Console.HasSignals, report.Console.Total > 0))
                BetterConsoleDiagnosticBridge.OpenForTargets(Selection.objects);
        }

        private void DrawToolGrid()
        {
            BetterScenePanel[] panels =
            {
                BetterScenePanel.Create, BetterScenePanel.Transform, BetterScenePanel.Place,
                BetterScenePanel.View, BetterScenePanel.Visibility, BetterScenePanel.Measure,
                BetterScenePanel.Review
            };
            int columns = position.width >= 590f ? 3 : 2;
            for (int index = 0; index < panels.Length; index += columns)
            {
                Rect row = Inset(GUILayoutUtility.GetRect(10f, 58f, GUILayout.ExpandWidth(true)));
                float gap = 5f;
                float width = (row.width - gap * (columns - 1)) / columns;
                for (int column = 0; column < columns; column++)
                {
                    int itemIndex = index + column;
                    if (itemIndex >= panels.Length) break;
                    BetterScenePanel panel = panels[itemIndex];
                    Rect tile = new Rect(row.x + column * (width + gap), row.y, width, row.height);
                    if (DrawToolTile(tile, panel))
                    {
                        BetterSceneController.TogglePanel(panel);
                        FocusScene();
                    }
                }
                GUILayout.Space(5f);
            }
        }

        private bool DrawToolTile(Rect rect, BetterScenePanel panel)
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            bool selected = BetterSceneController.PanelExpanded && BetterSceneController.ActivePanel == panel;
            bool hover = rect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(rect, selected ? palette.AccentSoft : hover ? palette.Hover : palette.Raised);
            BetterSceneGui.Border(rect, selected ? palette.Accent : hover ? palette.BorderStrong : palette.Border);
            Texture icon = ToolIcon(panel);
            if (icon != null)
            {
                Color previous = GUI.color;
                GUI.color = selected ? palette.Accent : palette.Text;
                GUI.DrawTexture(new Rect(rect.x + 10f, rect.center.y - 11f, 22f, 22f), icon, ScaleMode.ScaleToFit, true);
                GUI.color = previous;
            }
            GUI.Label(new Rect(rect.x + 40f, rect.y + 8f, rect.width - 48f, 18f), panel.ToString().ToUpperInvariant(), BetterSceneGui.Label);
            GUI.Label(new Rect(rect.x + 40f, rect.y + 28f, rect.width - 48f, 16f), ToolSubtitle(panel), BetterSceneGui.Muted);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            return GUI.Button(rect, new GUIContent(string.Empty, "Open " + panel + " tools in Scene"), GUIStyle.none);
        }

        private void DrawActiveTool(DansToolboxPalette palette)
        {
            Rect card = Inset(GUILayoutUtility.GetRect(10f, 74f, GUILayout.ExpandWidth(true)));
            BetterSceneGui.Panel(card, true);
            BetterScenePanel panel = BetterSceneController.ActivePanel;
            Texture icon = ToolIcon(panel);
            if (icon != null) GUI.DrawTexture(new Rect(card.x + 11f, card.y + 13f, 28f, 28f), icon, ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(card.x + 48f, card.y + 10f, card.width - 124f, 20f), ActiveTitle(panel), BetterSceneGui.Label);
            GUI.Label(new Rect(card.x + 48f, card.y + 31f, card.width - 62f, 32f), ActiveSummary(panel), BetterSceneGui.WrappedMuted);
            Rect open = new Rect(card.xMax - 64f, card.y + 11f, 52f, 22f);
            if (BetterSceneGui.Button(open, new GUIContent(BetterSceneController.PanelExpanded ? "OPEN" : "SHOW", "Show this panel in Scene"), BetterSceneController.PanelExpanded))
            {
                if (!BetterSceneController.PanelExpanded) BetterSceneController.TogglePanel(panel == BetterScenePanel.None ? BetterScenePanel.Transform : panel);
                FocusScene();
            }
        }

        private void DrawSavedViews()
        {
            if (BetterSceneSettings.Bookmarks.Count == 0)
            {
                DrawEmpty("No saved views yet. Open View tools in Scene to capture one.");
                return;
            }
            foreach (BetterSceneBookmark bookmark in BetterSceneSettings.Bookmarks)
            {
                Rect row = Inset(GUILayoutUtility.GetRect(10f, 27f, GUILayout.ExpandWidth(true)));
                if (BetterSceneGui.Button(new Rect(row.x, row.y, row.width - 31f, row.height), new GUIContent(bookmark.Name, "Restore saved Scene camera")))
                    BetterSceneOperations.RestoreBookmark(bookmark);
                if (BetterSceneGui.Button(new Rect(row.xMax - 27f, row.y, 27f, row.height), new GUIContent("X", "Delete saved view")))
                    BetterSceneSettings.RemoveBookmark(bookmark.Id);
                GUILayout.Space(3f);
            }
        }

        private void DrawSceneHealth(BetterSceneDiagnosticReport report, DansToolboxPalette palette)
        {
            DrawMetric("MISSING SCRIPTS", report.MissingScripts, report.MissingScripts > 0 ? palette.Danger : palette.Success);
            DrawMetric("MISSING REFERENCES", report.MissingReferences, report.MissingReferences > 0 ? palette.Danger : palette.Success);
            DrawMetric("PREFAB CHANGES", report.PrefabOverrides, report.PrefabOverrides > 0 ? palette.Warning : palette.Success);
            DrawMetric("INACTIVE", report.InactiveObjects, report.InactiveObjects > 0 ? palette.Warning : palette.Success);
            Rect logs = Inset(GUILayoutUtility.GetRect(10f, 26f, GUILayout.ExpandWidth(true)));
            if (BetterSceneGui.Button(logs, new GUIContent("OPEN RELATED LOGS", report.Console.Tooltip), false, report.Console.Total > 0))
                BetterConsoleDiagnosticBridge.OpenForTargets(Selection.objects);
        }

        private void DrawMetric(string label, int value, Color color)
        {
            Rect row = Inset(GUILayoutUtility.GetRect(10f, 23f, GUILayout.ExpandWidth(true)));
            if (row.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(row, DansToolboxTheme.Current.Hover);
            GUI.Label(new Rect(row.x + 5f, row.y, row.width * 0.7f, row.height), label, BetterSceneGui.Tiny);
            GUIStyle right = BetterSceneGui.RightLabel;
            right.normal.textColor = color;
            GUI.Label(new Rect(row.x + row.width * 0.7f, row.y, row.width * 0.3f - 5f, row.height), value.ToString(), right);
        }

        private bool DrawDisclosure(string label, string badge, bool expanded)
        {
            Rect rect = Inset(GUILayoutUtility.GetRect(10f, 29f, GUILayout.ExpandWidth(true)));
            BetterSceneGui.Panel(rect, false, true);
            GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 74f, rect.height), (expanded ? "-  " : "+  ") + label, BetterSceneGui.Tiny);
            GUIStyle right = BetterSceneGui.RightTiny;
            right.normal.textColor = DansToolboxTheme.Current.Accent;
            GUI.Label(new Rect(rect.xMax - 70f, rect.y, 58f, rect.height), badge, right);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void DrawSectionLabel(string label, string badge, DansToolboxPalette palette)
        {
            Rect rect = Inset(GUILayoutUtility.GetRect(10f, 23f, GUILayout.ExpandWidth(true)));
            BetterSceneGui.SectionHeader(rect, label, badge, palette.Accent);
        }

        private void DrawEmpty(string message)
        {
            Rect rect = Inset(GUILayoutUtility.GetRect(10f, 46f, GUILayout.ExpandWidth(true)));
            BetterSceneGui.Panel(rect, true);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, rect.height - 10f), message, BetterSceneGui.WrappedMuted);
        }

        private void ShowOptionsMenu(Rect activator)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show Scene toolbar"), false, BetterSceneNativeOverlayUtility.ShowToolbar);
            menu.AddItem(new GUIContent("Reset Scene toolbar"), false, BetterSceneNativeOverlayUtility.ResetToolbar);
            menu.AddItem(new GUIContent("Selection bounds"), BetterSceneSettings.DrawSelectionBounds, () => BetterSceneSettings.DrawSelectionBounds = !BetterSceneSettings.DrawSelectionBounds);
            menu.AddItem(new GUIContent("Pivots"), BetterSceneSettings.DrawPivot, () => BetterSceneSettings.DrawPivot = !BetterSceneSettings.DrawPivot);
            menu.AddItem(new GUIContent("Diagnostics"), BetterSceneSettings.DrawDiagnostics, () => BetterSceneSettings.DrawDiagnostics = !BetterSceneSettings.DrawDiagnostics);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Restore visibility"), false, BetterSceneVisibility.Restore);
            menu.AddItem(new GUIContent("Show and unlock all"), false, BetterSceneVisibility.ShowAndUnlockAll);
            menu.AddItem(new GUIContent("Clear selection history"), false, BetterSceneSelectionHistory.Clear);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Toolbox Hub"), false, () => EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Toolbox Hub"));
            menu.DropDown(activator);
        }

        private void DrawDisabled(Rect canvas, DansToolboxPalette palette)
        {
            Rect panel = new Rect(Mathf.Max(18f, canvas.center.x - 150f), Mathf.Max(18f, canvas.center.y - 58f), Mathf.Min(300f, canvas.width - 36f), 116f);
            BetterSceneGui.Panel(panel, false, true);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 16f, panel.width - 28f, 24f), "BETTER SCENE OFF", BetterSceneGui.Title);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 42f, panel.width - 28f, 20f), "Enable it in Toolbox Setup.", BetterSceneGui.Muted);
            if (BetterSceneGui.Button(new Rect(panel.x + 14f, panel.yMax - 40f, 92f, 24f), new GUIContent("SETUP", "Open Toolbox Setup")))
                EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Setup Wizard");
        }

        private void ReleaseTextFocusOnPointerDown()
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || string.IsNullOrEmpty(GUI.GetNameOfFocusedControl())) return;
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

        private static void FocusScene()
        {
            EditorApplication.delayCall += () => EditorApplication.ExecuteMenuItem("Window/General/Scene");
        }

        private static Rect Inset(Rect rect)
        {
            rect.x += 8f;
            rect.width -= 17f;
            return rect;
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
            return string.Join(" / ", names.ToArray());
        }

        private static Texture ToolIcon(BetterScenePanel panel)
        {
            switch (panel)
            {
                case BetterScenePanel.Create: return EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image;
                case BetterScenePanel.Transform: return EditorGUIUtility.ObjectContent(null, typeof(Transform)).image;
                case BetterScenePanel.Place: return EditorGUIUtility.ObjectContent(null, typeof(MeshFilter)).image;
                case BetterScenePanel.View: return EditorGUIUtility.ObjectContent(null, typeof(Camera)).image;
                case BetterScenePanel.Visibility: return EditorGUIUtility.ObjectContent(null, typeof(MeshRenderer)).image;
                case BetterScenePanel.Measure: return EditorGUIUtility.ObjectContent(null, typeof(BoxCollider)).image;
                case BetterScenePanel.Review: return EditorGUIUtility.ObjectContent(null, typeof(MonoScript)).image;
                default: return EditorGUIUtility.IconContent("UnityEditor.SceneView").image;
            }
        }

        private static string ToolSubtitle(BetterScenePanel panel)
        {
            switch (panel)
            {
                case BetterScenePanel.Create: return "Objects";
                case BetterScenePanel.Transform: return "Spatial edits";
                case BetterScenePanel.Place: return "Assets + snap";
                case BetterScenePanel.View: return "Views + cameras";
                case BetterScenePanel.Visibility: return "Filters + layers";
                case BetterScenePanel.Measure: return "Distance + delta";
                case BetterScenePanel.Review: return "Health + logs";
                default: return string.Empty;
            }
        }

        private static string ActiveTitle(BetterScenePanel panel)
        {
            if (!BetterSceneController.PanelExpanded) return "TOOLS COLLAPSED";
            return panel.ToString().ToUpperInvariant() + " IS OPEN IN SCENE";
        }

        private static string ActiveSummary(BetterScenePanel panel)
        {
            if (!BetterSceneController.PanelExpanded) return "Choose a category above to open its focused Scene menu.";
            if (panel == BetterScenePanel.Place)
            {
                UnityEngine.Object asset = BetterSceneSettings.PlacementAsset;
                return asset == null ? "Choose an asset in the Scene panel." : asset.name + "  |  " + BetterSceneController.SnapMode;
            }
            if (panel == BetterScenePanel.Measure)
            {
                BetterSceneMeasurement measurement = BetterSceneController.Measurement;
                return measurement.HasStart ? measurement.Distance.ToString("0.###") + " m" : "Click two Scene points to measure.";
            }
            if (panel == BetterScenePanel.Visibility) return BetterSceneVisibility.HasSnapshot ? "A reversible Scene filter is active." : "Scene visibility is unfiltered.";
            if (panel == BetterScenePanel.View) return BetterSceneSettings.Bookmarks.Count + " saved view" + (BetterSceneSettings.Bookmarks.Count == 1 ? string.Empty : "s") + ".";
            if (panel == BetterScenePanel.Review) return BetterSceneDiagnostics.Current.HasIssues ? BetterSceneDiagnostics.Current.Badge + " selected-object issues." : "Selected objects are clean.";
            return "Use the focused mega-panel directly over the Scene view.";
        }
    }
}
