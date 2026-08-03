using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DansToolbox.Editor;
using DansToolbox.EditorTools.BetterConsole;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DansToolbox.EditorTools.BetterInspector
{
    public sealed class BetterInspectorWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Dans Toolbox/Better Inspector";
        private const string SearchControlName = "BetterInspectorSearch";
        private const string FavoritesKey = "DansToolbox.BetterInspector.FavoriteTypes";
        internal const string ExpandedGlyph = "\u25BE";
        internal const string CollapsedGlyph = "\u25B8";
        internal const string MetadataSeparator = "\u00B7";
        private const float ToolbarHeight = 38f;
        private const float TargetHeaderHeight = 84f;

        [SerializeField] private bool targetLocked;
        [SerializeField] private Object[] lockedTargets = Array.Empty<Object>();
        [SerializeField] private string search = string.Empty;
        [SerializeField] private Vector2 scroll;
        [SerializeField] private bool favoritesOnly;
        [SerializeField] private bool diagnosticsOnly;
        [SerializeField] private List<Object> history = new List<Object>();
        [SerializeField] private int historyIndex = -1;
        [SerializeField] private List<string> collapsedKeys = new List<string>();
        [SerializeField] private List<string> collapsedPreviewKeys = new List<string>();
        [SerializeField] private List<string> expandedReferenceKeys = new List<string>();

        private readonly List<BetterInspectorEditorEntry> entries =
            new List<BetterInspectorEditorEntry>();
        private readonly List<Texture2D> styleTextures = new List<Texture2D>();
        private HashSet<string> favoriteTypes = new HashSet<string>(StringComparer.Ordinal);
        private List<BetterInspectorIssue> issues = new List<BetterInspectorIssue>();
        private BetterInspectorStyles styles;
        private string editorSignature = string.Empty;
        private bool editorsDirty = true;
        private bool navigatingHistory;
        private bool pointerOverContentElement;
        private BetterInspectorEditorEntry pendingComponentContextEntry;
        private int styledThemeRevision = -1;
        [NonSerialized] private double revealStartedAt;

        [MenuItem(MenuPath, false, 21)]
        internal static void Open()
        {
            BetterInspectorWindow window = GetWindow<BetterInspectorWindow>();
            DansToolboxWindowChrome.ApplyCompactTitle(
                window,
                DansToolboxTools.BetterInspectorId);
            window.minSize = new Vector2(300f, 260f);
            window.Show();
            window.Focus();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOpen()
        {
            return DansToolboxSettings.IsToolEnabled(DansToolboxTools.BetterInspectorId);
        }

        private void OnEnable()
        {
            revealStartedAt = EditorApplication.timeSinceStartup;
            history ??= new List<Object>();
            collapsedKeys ??= new List<string>();
            collapsedPreviewKeys ??= new List<string>();
            expandedReferenceKeys ??= new List<string>();
            lockedTargets ??= Array.Empty<Object>();
            DansToolboxWindowChrome.ApplyCompactTitle(
                this,
                DansToolboxTools.BetterInspectorId);
            minSize = new Vector2(300f, 260f);
            wantsMouseMove = true;
            favoriteTypes = LoadFavorites();

            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed -= OnObjectsChanged;
            Undo.undoRedoPerformed += OnObjectsChanged;
            EditorApplication.hierarchyChanged -= OnObjectsChanged;
            EditorApplication.hierarchyChanged += OnObjectsChanged;
            EditorApplication.projectChanged -= OnObjectsChanged;
            EditorApplication.projectChanged += OnObjectsChanged;
            DansToolboxTheme.Changed -= OnThemeChanged;
            DansToolboxTheme.Changed += OnThemeChanged;
            BetterConsoleDiagnosticBridge.Changed -= OnConsoleDiagnosticsChanged;
            BetterConsoleDiagnosticBridge.Changed += OnConsoleDiagnosticsChanged;

            if (!targetLocked)
            {
                RecordHistory(Selection.activeObject);
            }
            RebuildEditors(force: true);
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnObjectsChanged;
            EditorApplication.hierarchyChanged -= OnObjectsChanged;
            EditorApplication.projectChanged -= OnObjectsChanged;
            DansToolboxTheme.Changed -= OnThemeChanged;
            BetterConsoleDiagnosticBridge.Changed -= OnConsoleDiagnosticsChanged;
            DisposeEditors();
            DestroyStyleTextures();
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (editorsDirty)
            {
                RebuildEditors(force: false);
            }
            DansToolboxPalette palette = DansToolboxTheme.Current;
            Rect canvas = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(canvas, palette.Canvas);

            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }

            if (!DansToolboxSettings.IsToolEnabled(DansToolboxTools.BetterInspectorId))
            {
                DrawDisabled(canvas, palette);
                return;
            }

            HandleKeyboard();
            Object[] targets = GetTargets();
            Rect toolbar = new Rect(0f, 0f, position.width, ToolbarHeight);
            Rect targetHeader = new Rect(0f, toolbar.yMax, position.width, TargetHeaderHeight);
            Rect content = new Rect(
                0f,
                targetHeader.yMax,
                position.width,
                Mathf.Max(1f, position.height - targetHeader.yMax));

            DrawToolbar(toolbar, palette, targets);
            DrawTargetHeader(targetHeader, targets, palette);
            DrawContent(content, targets, palette);
            HandleContentContextClick(content, targets);

            if (DansToolboxMotion.DrawWindowReveal(canvas, revealStartedAt))
            {
                Repaint();
            }
        }

        private void Update()
        {
            foreach (BetterInspectorEditorEntry entry in entries)
            {
                try
                {
                    if ((entry.Editor != null && entry.Editor.RequiresConstantRepaint()) ||
                        (entry.PreviewEditor != null && entry.PreviewEditor.RequiresConstantRepaint()))
                    {
                        Repaint();
                        return;
                    }
                }
                catch (Exception)
                {
                    // A native editor can become invalid while Unity imports or reloads assets.
                }
            }
        }

        private void DrawToolbar(Rect rect, DansToolboxPalette palette, Object[] targets)
        {
            EditorGUI.DrawRect(rect, palette.Panel);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), palette.Border);

            bool narrow = rect.width < 430f;
            float x = 7f;
            if (DrawIconButton(new Rect(x, 8f, 22f, 22f), "<", "Back", !targetLocked && historyIndex > 0, palette))
            {
                NavigateHistory(-1);
            }
            x += 24f;
            if (DrawIconButton(new Rect(x, 8f, 22f, 22f), ">", "Forward", !targetLocked && historyIndex >= 0 && historyIndex < history.Count - 1, palette))
            {
                NavigateHistory(1);
            }
            x += 29f;

            float actionWidth = narrow ? 106f : 132f;
            float searchWidth = Mathf.Max(72f, rect.width - x - actionWidth - 8f);
            GUI.SetNextControlName(SearchControlName);
            string updated = GUI.TextField(
                new Rect(x, 8f, searchWidth, 22f),
                search,
                styles.Search);
            if (!string.Equals(updated, search, StringComparison.Ordinal))
            {
                search = updated;
                scroll = Vector2.zero;
            }
            x += searchWidth + 5f;

            if (DrawIconButton(
                    new Rect(x, 8f, 22f, 22f),
                    favoritesOnly ? "★" : "☆",
                    "Favorite components only",
                    true,
                    palette,
                    favoritesOnly))
            {
                favoritesOnly = !favoritesOnly;
            }
            x += 24f;
            BetterConsoleDiagnosticSummary consoleSummary = BetterConsoleDiagnosticBridge.GetSummary(targets);
            string consoleLabel = consoleSummary.HasSignals ? consoleSummary.Badge : "@";
            string consoleTooltip = consoleSummary.HasSignals ? consoleSummary.Tooltip : "Show selected target logs in Better Console";
            if (DrawIconButton(
                    new Rect(x, 8f, 22f, 22f),
                    consoleLabel,
                    consoleTooltip,
                    targets.Length > 0,
                    palette,
                    consoleSummary.HasSignals))
            {
                BetterConsoleDiagnosticBridge.OpenForTargets(targets);
            }
            x += 24f;
            if (DrawIconButton(
                    new Rect(x, 8f, 22f, 22f),
                    "!",
                    "Diagnostics",
                    true,
                    palette,
                    diagnosticsOnly))
            {
                diagnosticsOnly = !diagnosticsOnly;
            }
            x += 24f;
            if (!narrow)
            {
                if (DrawIconButton(new Rect(x, 8f, 22f, 22f), "−", "Collapse all", entries.Count > 0, palette))
                {
                    SetAllCollapsed(true);
                }
                x += 24f;
            }
            if (DrawIconButton(new Rect(x, 8f, 22f, 22f), "...", "Inspector options", true, palette))
            {
                ShowWindowMenu(new Rect(x, 8f, 22f, 22f));
            }
        }

        private void DrawTargetHeader(Rect rect, Object[] targets, DansToolboxPalette palette)
        {
            EditorGUI.DrawRect(rect, palette.Inset);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), palette.Border);
            if (targets.Length == 0)
            {
                GUI.Label(new Rect(14f, rect.y + 18f, rect.width - 28f, 24f), "NO SELECTION", styles.HeaderTitle);
                GUI.Label(new Rect(14f, rect.y + 43f, rect.width - 28f, 20f), "Select a scene object or asset to inspect.", styles.Muted);
                return;
            }

            Object primary = targets[0];
            Texture icon = AssetPreview.GetMiniThumbnail(primary) ??
                           EditorGUIUtility.ObjectContent(primary, primary.GetType()).image;
            Rect iconRect = new Rect(12f, rect.y + 16f, 46f, 46f);
            EditorGUI.DrawRect(new Rect(iconRect.x - 1f, iconRect.y - 1f, iconRect.width + 2f, iconRect.height + 2f), palette.Border);
            EditorGUI.DrawPreviewTexture(iconRect, icon, null, ScaleMode.ScaleToFit);

            float rightActions = targets.All(target => target is GameObject) ? 132f : 36f;
            Rect nameRect = new Rect(70f, rect.y + 12f, Mathf.Max(80f, rect.width - 82f - rightActions), 24f);
            if (targets.Length == 1 && primary is GameObject gameObject)
            {
                string renamed = EditorGUI.DelayedTextField(nameRect, gameObject.name, styles.ObjectName);
                if (!string.Equals(renamed, gameObject.name, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(renamed))
                {
                    Undo.RecordObject(gameObject, "Rename GameObject");
                    gameObject.name = renamed.Trim();
                    EditorUtility.SetDirty(gameObject);
                }
            }
            else
            {
                string label = targets.Length == 1 ? primary.name : targets.Length + " OBJECTS";
                GUI.Label(nameRect, label, styles.ObjectName);
            }

            string detail = GetTargetDetail(targets);
            GUI.Label(new Rect(70f, rect.y + 38f, Mathf.Max(80f, rect.width - 118f), 18f), detail, styles.Muted);

            if (targets.Length == 1 && AssetDatabase.Contains(primary))
            {
                GUI.Label(new Rect(70f, rect.y + 56f, Mathf.Max(80f, rect.width - 118f), 16f), AssetDatabase.GetAssetPath(primary), styles.Path);
            }

            if (targets.All(target => target is GameObject))
            {
                DrawGameObjectHeaderControls(GetGameObjectActionColumn(rect), targets.Cast<GameObject>().ToArray(), palette);
            }

            if (DrawIconButton(
                    new Rect(rect.xMax - 32f, rect.y + 12f, 22f, 22f),
                    targetLocked ? "●" : "○",
                    targetLocked ? "Unlock target" : "Lock target",
                    true,
                    palette,
                    targetLocked))
            {
                ToggleLock();
            }

            if (targets.All(target => target is GameObject) &&
                DrawFlatButton(
                    GetAddComponentButtonRect(rect),
                    "+ COMPONENT",
                    "Add a component to the selected objects",
                    true,
                    palette))
            {
                PopupWindow.Show(
                    GetAddComponentButtonRect(rect),
                    new BetterInspectorAddComponentPopup(targets.Cast<GameObject>().ToArray(), OnObjectsChanged));
            }
        }

        internal static Rect GetGameObjectActionColumn(Rect headerRect)
        {
            return new Rect(headerRect.xMax - 134f, headerRect.y + 12f, 96f, 52f);
        }

        internal static Rect GetLayerFieldRect(Rect headerRect)
        {
            Rect column = GetGameObjectActionColumn(headerRect);
            return new Rect(column.x, column.y + 22f, column.width, 18f);
        }

        internal static Rect GetAddComponentButtonRect(Rect headerRect)
        {
            Rect column = GetGameObjectActionColumn(headerRect);
            return new Rect(column.x, headerRect.yMax - 26f, column.width, 20f);
        }

        private void DrawGameObjectHeaderControls(
            Rect rect,
            GameObject[] gameObjects,
            DansToolboxPalette palette)
        {
            bool active = gameObjects[0].activeSelf;
            bool mixed = gameObjects.Any(gameObject => gameObject.activeSelf != active);
            EditorGUI.showMixedValue = mixed;
            bool updated = EditorGUI.ToggleLeft(
                new Rect(rect.x, rect.y, rect.width, 18f),
                "ACTIVE",
                active,
                styles.MiniLabel);
            EditorGUI.showMixedValue = false;
            if (updated != active || mixed)
            {
                Undo.RecordObjects(gameObjects, "Set GameObject Active");
                foreach (GameObject gameObject in gameObjects)
                {
                    gameObject.SetActive(updated);
                }
            }

            if (gameObjects.Length == 1 && position.width >= 470f)
            {
                int layer = EditorGUI.LayerField(
                    new Rect(rect.x, rect.y + 22f, rect.width, 18f),
                    gameObjects[0].layer,
                    styles.CompactPopup);
                if (layer != gameObjects[0].layer)
                {
                    Undo.RecordObject(gameObjects[0], "Change Layer");
                    gameObjects[0].layer = layer;
                }
            }
        }

        private void HandleContentContextClick(Rect contentRect, Object[] targets)
        {
            Event current = Event.current;
            Rect interactionRect = new Rect(
                contentRect.x + 6f,
                contentRect.y + 6f,
                Mathf.Max(0f, contentRect.width - 28f),
                Mathf.Max(0f, contentRect.height - 12f));
            if (current.type != EventType.ContextClick || !interactionRect.Contains(current.mousePosition))
            {
                return;
            }

            Rect anchor = new Rect(current.mousePosition, Vector2.zero);
            if (pendingComponentContextEntry != null)
            {
                ShowComponentContextMenu(pendingComponentContextEntry, anchor);
                current.Use();
                return;
            }

            if (pointerOverContentElement)
            {
                return;
            }

            GenericMenu menu = new GenericMenu();
            AddBetterInspectorContextItems(menu, targets);
            if (!BetterInspectorContextMenu.ShowNativeWithExtras(menu, anchor, targets))
            {
                menu.ShowAsContext();
            }
            current.Use();
        }

        private void AddBetterInspectorContextItems(GenericMenu menu, Object[] targets)
        {
            const string prefix = "Better Inspector/";
            if (entries.Count > 0)
            {
                menu.AddItem(new GUIContent(prefix + "Expand All"), false, () => SetAllCollapsed(false));
                menu.AddItem(new GUIContent(prefix + "Collapse All"), false, () => SetAllCollapsed(true));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(prefix + "Expand All"));
                menu.AddDisabledItem(new GUIContent(prefix + "Collapse All"));
            }

            menu.AddSeparator(prefix);
            menu.AddItem(new GUIContent(prefix + "Favorites Only"), favoritesOnly, () =>
            {
                favoritesOnly = !favoritesOnly;
                Repaint();
            });
            menu.AddItem(new GUIContent(prefix + "Diagnostics"), diagnosticsOnly, () =>
            {
                diagnosticsOnly = !diagnosticsOnly;
                Repaint();
            });

            menu.AddSeparator(prefix);
            if (targets.Length > 0)
            {
                menu.AddItem(new GUIContent(prefix + "Diagnostics/Show in Better Console"), false, () =>
                    BetterConsoleDiagnosticBridge.OpenForTargets(targets));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(prefix + "Diagnostics/Show in Better Console"));
            }

            menu.AddSeparator(prefix);
            if (targets.Length > 0 || targetLocked)
            {
                menu.AddItem(new GUIContent(prefix + (targetLocked ? "Unlock Target" : "Lock Target")), false, ToggleLock);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(prefix + "Lock Target"));
            }

            if (!string.IsNullOrEmpty(search))
            {
                menu.AddItem(new GUIContent(prefix + "Clear Search"), false, () =>
                {
                    search = string.Empty;
                    Repaint();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(prefix + "Clear Search"));
            }
            menu.AddItem(new GUIContent(prefix + "Refresh Editors"), false, OnObjectsChanged);

            Type inspectorType = typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow");
            if (inspectorType != null)
            {
                menu.AddSeparator(prefix);
                menu.AddItem(new GUIContent(prefix + "Open Unity Inspector"), false, () => GetWindow(inspectorType));
            }
        }

        private void ShowComponentContextMenu(BetterInspectorEditorEntry entry, Rect anchor)
        {
            GenericMenu menu = new GenericMenu();
            AddBetterInspectorComponentContextItems(menu, entry);
            Object[] context = entry.Targets.Where(target => target != null).ToArray();
            if (!BetterInspectorContextMenu.ShowNativeWithExtras(menu, anchor, context))
            {
                menu.ShowAsContext();
            }
        }

        private void AddBetterInspectorComponentContextItems(
            GenericMenu menu,
            BetterInspectorEditorEntry entry)
        {
            const string prefix = "Better Inspector/";
            bool favorite = IsFavorite(entry.Type);
            bool expanded = !collapsedKeys.Contains(entry.Key);
            menu.AddItem(
                new GUIContent(prefix + (favorite ? "Remove Favorite" : "Add Favorite")),
                false,
                () => SetFavorite(entry.Type, !favorite));
            menu.AddItem(
                new GUIContent(prefix + (expanded ? "Collapse Component" : "Expand Component")),
                false,
                () => SetCollapsed(entry.Key, expanded));
            menu.AddItem(new GUIContent(prefix + "Collapse Others"), false, () =>
            {
                collapsedKeys.Clear();
                collapsedKeys.AddRange(entries
                    .Where(candidate => candidate.Key != entry.Key)
                    .Select(candidate => candidate.Key));
                Repaint();
            });

            menu.AddSeparator(prefix);
            menu.AddItem(new GUIContent(prefix + "Isolate in Search"), false, () =>
            {
                diagnosticsOnly = false;
                favoritesOnly = false;
                search = entry.Title;
                scroll = Vector2.zero;
                Repaint();
            });
            menu.AddItem(new GUIContent(prefix + "Favorites Only"), favoritesOnly, () =>
            {
                favoritesOnly = !favoritesOnly;
                Repaint();
            });
            menu.AddItem(new GUIContent(prefix + "Diagnostics"), diagnosticsOnly, () =>
            {
                diagnosticsOnly = !diagnosticsOnly;
                Repaint();
            });

            menu.AddSeparator(prefix);
            menu.AddItem(new GUIContent(prefix + "Diagnostics/Show in Better Console"), false, () =>
                BetterConsoleDiagnosticBridge.OpenForTargets(entry.Targets));
            menu.AddSeparator(prefix);
            menu.AddItem(new GUIContent(prefix + "Refresh Editors"), false, OnObjectsChanged);
        }

        private void DrawContent(Rect rect, Object[] targets, DansToolboxPalette palette)
        {
            pointerOverContentElement = false;
            pendingComponentContextEntry = null;
            Rect inner = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            BetterInspectorEditorEntry previewEntry = diagnosticsOnly
                ? null
                : entries.FirstOrDefault(entry => ShouldShowEntry(entry) && CanDrawPreview(entry));
            float previewHeight = previewEntry == null
                ? 0f
                : GetPreviewPanelHeight(previewEntry, inner.height);
            Rect scrollingArea = new Rect(
                inner.x,
                inner.y,
                inner.width,
                Mathf.Max(1f, inner.height - previewHeight - (previewHeight > 0f ? 5f : 0f)));

            GUILayout.BeginArea(scrollingArea);
            scroll = EditorGUILayout.BeginScrollView(scroll, false, true);

            if (targets.Length == 0)
            {
                DrawEmptyState(palette);
            }
            else if (diagnosticsOnly)
            {
                DrawDiagnostics(palette);
            }
            else
            {
                int visible = 0;
                foreach (BetterInspectorEditorEntry entry in entries)
                {
                    if (!ShouldShowEntry(entry))
                    {
                        continue;
                    }

                    DrawEditorCard(entry, palette);
                    GUILayout.Space(6f);
                    visible++;
                }

                if (visible == 0)
                {
                    DrawNoResults(palette);
                }
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            if (previewEntry != null)
            {
                Rect previewRect = new Rect(inner.x, inner.yMax - previewHeight, inner.width, previewHeight);
                DrawPreview(previewEntry, previewRect, palette);
                pointerOverContentElement |= previewRect.Contains(Event.current.mousePosition);
            }
        }

        private void DrawEditorCard(BetterInspectorEditorEntry entry, DansToolboxPalette palette)
        {
            GUILayout.BeginVertical(styles.Card);
            Rect header = GUILayoutUtility.GetRect(1f, 34f, GUILayout.ExpandWidth(true));
            bool expanded = !collapsedKeys.Contains(entry.Key);
            bool favorite = IsFavorite(entry.Type);
            bool hovered = header.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(header, hovered ? palette.Raised : palette.Panel);
            if (favorite)
            {
                EditorGUI.DrawRect(new Rect(header.x, header.y, 3f, header.height), palette.Accent);
            }

            Rect foldout = new Rect(header.x + 7f, header.y + 7f, 18f, 20f);
            GUI.Label(foldout, expanded ? "▾" : "▸", styles.Foldout);
            Texture icon = EditorGUIUtility.ObjectContent(null, entry.Type).image;
            GUI.Label(new Rect(header.x + 27f, header.y + 7f, 20f, 20f), new GUIContent(icon));
            GUI.Label(new Rect(header.x + 51f, header.y + 5f, Mathf.Max(40f, header.width - 130f), 23f), entry.Title, styles.CardTitle);

            Rect favoriteRect = new Rect(header.xMax - 54f, header.y + 6f, 22f, 22f);
            if (DrawIconButton(favoriteRect, favorite ? "★" : "☆", "Favorite component", true, palette, favorite))
            {
                SetFavorite(entry.Type, !favorite);
            }
            Rect menuRect = new Rect(header.xMax - 28f, header.y + 6f, 22f, 22f);
            if (DrawIconButton(menuRect, "...", "Component actions", true, palette))
            {
                ShowEntryMenu(entry, menuRect);
            }

            Rect headerClick = new Rect(header.x, header.y, header.width - 60f, header.height);
            Event current = Event.current;
            if (BetterInspectorContextMenu.ShouldToggleFoldout(
                    current.type,
                    current.button,
                    headerClick,
                    current.mousePosition))
            {
                SetCollapsed(entry.Key, expanded);
                expanded = !expanded;
                current.Use();
            }

            if (expanded && entry.Editor != null)
            {
                GUILayout.Space(5f);
                GUILayout.BeginVertical(styles.CardBody);
                float oldLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.36f, 112f, 210f);
                try
                {
                    DrawEditorBody(entry);
                    DrawContextActions(entry, palette);
                    DrawReferenceSummary(entry, palette);
                }
                catch (Exception exception)
                {
                    EditorGUILayout.HelpBox(
                        "This editor could not be drawn: " + exception.GetBaseException().Message,
                        MessageType.Warning);
                }
                finally
                {
                    EditorGUIUtility.labelWidth = oldLabelWidth;
                }
                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
            GUILayout.EndVertical();
            Rect cardRect = GUILayoutUtility.GetLastRect();
            pointerOverContentElement |= cardRect.Contains(Event.current.mousePosition);
            if (entry.Targets.Length > 0 &&
                entry.Targets.All(target => target is Component) &&
                BetterInspectorContextMenu.ShouldOpenComponentMenu(
                    Event.current.type,
                    cardRect,
                    Event.current.mousePosition))
            {
                pendingComponentContextEntry = entry;
            }
        }

        private void DrawEditorBody(BetterInspectorEditorEntry entry)
        {
            string trimmed = search?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed) || MatchesSearch(trimmed, entry.Title, entry.Type.FullName))
            {
                using (new BetterInspectorEditorVisibilityScope(entry.Editor.targets))
                {
                    entry.Editor.OnInspectorGUI();
                }
                return;
            }

            DrawFilteredProperties(entry.Editor.serializedObject, trimmed);
        }

        private void DrawContextActions(BetterInspectorEditorEntry entry, DansToolboxPalette palette)
        {
            if (entry.Actions.Count == 0)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label("ACTIONS", styles.SectionLabel);
            int columns = position.width >= 560f ? 3 : position.width >= 380f ? 2 : 1;
            for (int index = 0; index < entry.Actions.Count; index += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int actionIndex = index + column;
                    if (actionIndex >= entry.Actions.Count)
                    {
                        GUILayout.FlexibleSpace();
                        continue;
                    }

                    BetterInspectorAction action = entry.Actions[actionIndex];
                    if (GUILayout.Button(action.Label.ToUpperInvariant(), styles.SmallButton, GUILayout.Height(24f)))
                    {
                        InvokeContextAction(entry, action);
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        private void InvokeContextAction(BetterInspectorEditorEntry entry, BetterInspectorAction action)
        {
            Object[] actionTargets = entry.Targets.Where(target => target != null).ToArray();
            Undo.RecordObjects(actionTargets, action.Label);
            foreach (Object target in actionTargets)
            {
                try
                {
                    action.Method.Invoke(target, null);
                    EditorUtility.SetDirty(target);
                }
                catch (TargetInvocationException exception)
                {
                    Debug.LogException(exception.InnerException ?? exception, target);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, target);
                }
            }
            OnObjectsChanged();
        }

        private void DrawReferenceSummary(BetterInspectorEditorEntry entry, DansToolboxPalette palette)
        {
            List<SerializedProperty> references = GetObjectReferenceProperties(entry.Targets);
            if (references.Count == 0)
            {
                return;
            }

            string key = entry.Key + "::references";
            bool expanded = expandedReferenceKeys.Contains(key);
            GUILayout.Space(8f);
            Rect header = GUILayoutUtility.GetRect(1f, 24f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(header, palette.Panel);
            GUI.Label(
                new Rect(header.x + 8f, header.y + 2f, 18f, 20f),
                expanded ? ExpandedGlyph : CollapsedGlyph,
                styles.Foldout);
            GUI.Label(
                new Rect(header.x + 28f, header.y + 2f, header.width - 36f, 20f),
                "REFERENCES  " + MetadataSeparator + "  " + references.Count,
                styles.SectionLabel);
            if (GUI.Button(header, GUIContent.none, GUIStyle.none))
            {
                if (expanded)
                {
                    expandedReferenceKeys.Remove(key);
                }
                else
                {
                    expandedReferenceKeys.Add(key);
                }
                expanded = !expanded;
            }

            if (!expanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            foreach (SerializedProperty reference in references)
            {
                EditorGUILayout.PropertyField(reference, false);
            }
            EditorGUI.indentLevel--;
            references[0].serializedObject.ApplyModifiedProperties();
        }

        private static List<SerializedProperty> GetObjectReferenceProperties(Object[] targets)
        {
            var result = new List<SerializedProperty>();
            if (targets == null || targets.Length == 0 || targets.Any(target => target == null))
            {
                return result;
            }

            try
            {
                var serializedObject = new SerializedObject(targets);
                serializedObject.UpdateIfRequiredOrScript();
                SerializedProperty property = serializedObject.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    if (property.name == "m_Script" ||
                        property.propertyType != SerializedPropertyType.ObjectReference ||
                        (!property.hasMultipleDifferentValues && property.objectReferenceValue == null))
                    {
                        continue;
                    }

                    result.Add(property.Copy());
                    enterChildren = false;
                }
            }
            catch (Exception)
            {
                // Some native object types do not expose a SerializedObject property tree.
            }
            return result;
        }

        private static bool CanDrawPreview(BetterInspectorEditorEntry entry)
        {
            UnityEditor.Editor previewEditor = entry.GetPreviewEditor();
            if (previewEditor == null)
            {
                return false;
            }

            try
            {
                return previewEditor.HasPreviewGUI();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private float GetPreviewPanelHeight(BetterInspectorEditorEntry entry, float availableHeight)
        {
            string key = entry.Key + "::preview";
            bool expanded = !collapsedPreviewKeys.Contains(key);
            if (!expanded)
            {
                return Mathf.Min(23f, availableHeight);
            }

            float minimum = Mathf.Min(92f, availableHeight);
            float maximum = Mathf.Min(280f, availableHeight);
            return Mathf.Clamp(availableHeight * 0.34f, minimum, maximum);
        }

        private void DrawPreview(BetterInspectorEditorEntry entry, Rect rect, DansToolboxPalette palette)
        {
            UnityEditor.Editor previewEditor = entry.GetPreviewEditor();
            if (previewEditor == null)
            {
                return;
            }

            string key = entry.Key + "::preview";
            bool expanded = !collapsedPreviewKeys.Contains(key);
            GUILayout.BeginArea(rect, styles.Card);
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            bool updated = EditorGUILayout.Foldout(expanded, "PREVIEW", true, styles.SectionLabel);
            if (updated != expanded)
            {
                collapsedPreviewKeys.Remove(key);
                if (!updated)
                {
                    collapsedPreviewKeys.Add(key);
                }
                expanded = updated;
            }
            if (expanded)
            {
                GUILayout.FlexibleSpace();
                using (new BetterInspectorEditorVisibilityScope(previewEditor.targets))
                {
                    previewEditor.OnPreviewSettings();
                }
            }
            GUILayout.EndHorizontal();

            if (!expanded)
            {
                GUILayout.EndArea();
                return;
            }

            string info = string.Empty;
            float infoHeight = 18f;
            Rect previewRect = new Rect(3f, 23f, Mathf.Max(1f, rect.width - 6f), Mathf.Max(1f, rect.height - 26f - infoHeight));
            EditorGUI.DrawRect(previewRect, palette.Canvas);
            using (new BetterInspectorEditorVisibilityScope(previewEditor.targets))
            {
                if (previewEditor == entry.Editor)
                {
                    previewEditor.OnInteractivePreviewGUI(previewRect, styles.PreviewBackground);
                }
                else
                {
                    previewEditor.DrawPreview(previewRect);
                }
                info = previewEditor.GetInfoString();
            }
            if (!string.IsNullOrWhiteSpace(info))
            {
                GUI.Label(new Rect(6f, rect.height - infoHeight - 2f, rect.width - 12f, infoHeight), info, styles.Path);
            }
            GUILayout.EndArea();
        }

        private static void DrawFilteredProperties(SerializedObject serializedObject, string query)
        {
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            int matches = 0;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (property.name == "m_Script" ||
                    !MatchesSearch(query, property.displayName, property.name, property.propertyPath))
                {
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
                enterChildren = false;
                matches++;
            }

            if (matches == 0)
            {
                EditorGUILayout.LabelField("No matching fields", EditorStyles.centeredGreyMiniLabel);
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDiagnostics(DansToolboxPalette palette)
        {
            if (issues.Count == 0)
            {
                GUILayout.Space(18f);
                GUIStyle clean = new GUIStyle(styles.HeaderTitle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = palette.Success }
                };
                GUILayout.Label("✓  NO ISSUES FOUND", clean, GUILayout.Height(34f));
                GUILayout.Label("Serialized references and component scripts look healthy.", styles.CenteredMuted);
                return;
            }

            int missingScripts = issues.Count(issue => issue.Kind == BetterInspectorIssueKind.MissingScript);
            GUILayout.Label(issues.Count + " ISSUES", styles.SectionLabel);
            GUILayout.Space(4f);
            foreach (BetterInspectorIssue issue in issues)
            {
                GUILayout.BeginVertical(styles.IssueCard);
                GUILayout.BeginHorizontal();
                GUILayout.Label(issue.Kind == BetterInspectorIssueKind.MissingScript ? "SCRIPT" : "REFERENCE", styles.WarningBadge, GUILayout.Width(72f));
                GUILayout.Label(issue.ComponentName, styles.CardTitle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("PING", styles.SmallButton, GUILayout.Width(44f), GUILayout.Height(20f)))
                {
                    EditorGUIUtility.PingObject(issue.Context);
                }
                GUILayout.EndHorizontal();
                GUILayout.Label(issue.Message, styles.Muted);
                GUILayout.EndVertical();
                pointerOverContentElement |= GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition);
                GUILayout.Space(5f);
            }

            if (missingScripts > 0 && GUILayout.Button("REMOVE MISSING SCRIPTS", styles.PrimaryButton, GUILayout.Height(28f)))
            {
                if (EditorUtility.DisplayDialog(
                        "Remove Missing Scripts",
                        "Remove " + missingScripts + " missing component slot" + (missingScripts == 1 ? "" : "s") + "? This can be undone.",
                        "Remove",
                        "Cancel"))
                {
                    BetterInspectorDiagnostics.RemoveMissingScripts(GetTargets());
                    OnObjectsChanged();
                }
            }
        }

        private void DrawEmptyState(DansToolboxPalette palette)
        {
            GUILayout.Space(30f);
            GUILayout.Label("BETTER INSPECTOR", styles.CenteredTitle);
            GUILayout.Space(6f);
            GUILayout.Label("Select a GameObject, component, or asset.", styles.CenteredMuted);
            GUILayout.Space(3f);
            GUILayout.Label("Pin targets, search fields, favorite components, and inspect issues without losing Unity's native editors.", styles.CenteredMuted);
            GUILayout.Space(16f);
            Rect line = GUILayoutUtility.GetRect(80f, 2f, GUILayout.Width(80f));
            line.x = (position.width - line.width) * 0.5f - 6f;
            EditorGUI.DrawRect(line, palette.Accent);
        }

        private void DrawNoResults(DansToolboxPalette palette)
        {
            GUILayout.Space(24f);
            GUILayout.Label(favoritesOnly ? "NO FAVORITE COMPONENTS" : "NO MATCHES", styles.CenteredTitle);
            GUILayout.Space(4f);
            GUILayout.Label(favoritesOnly ? "Star a component card to keep it close." : "Try a component name or serialized field.", styles.CenteredMuted);
            if (!string.IsNullOrEmpty(search) && GUILayout.Button("CLEAR SEARCH", styles.SmallButton, GUILayout.Width(96f), GUILayout.Height(24f)))
            {
                search = string.Empty;
                GUI.FocusControl(null);
            }
        }

        private void DrawDisabled(Rect canvas, DansToolboxPalette palette)
        {
            Rect panel = new Rect(
                Mathf.Max(18f, canvas.center.x - 150f),
                Mathf.Max(18f, canvas.center.y - 58f),
                Mathf.Min(300f, canvas.width - 36f),
                116f);
            EditorGUI.DrawRect(panel, palette.Border);
            EditorGUI.DrawRect(new Rect(panel.x + 1f, panel.y + 1f, panel.width - 2f, panel.height - 2f), palette.Panel);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 16f, panel.width - 28f, 24f), "BETTER INSPECTOR OFF", styles.HeaderTitle);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 42f, panel.width - 28f, 20f), "Enable it in Toolbox Setup.", styles.Muted);
            if (DrawFlatButton(new Rect(panel.x + 14f, panel.yMax - 40f, 92f, 24f), "SETUP", "Open setup", true, palette))
            {
                EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Setup Wizard");
            }
        }

        private bool ShouldShowEntry(BetterInspectorEditorEntry entry)
        {
            if (favoritesOnly && !IsFavorite(entry.Type))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(search) || EntryMatchesSearch(entry, search);
        }

        private static bool EntryMatchesSearch(BetterInspectorEditorEntry entry, string query)
        {
            var haystack = new StringBuilder(entry.Title).Append(' ').Append(entry.Type.FullName);
            foreach (BetterInspectorAction action in entry.Actions)
            {
                haystack.Append(' ').Append(action.Label);
            }
            try
            {
                SerializedProperty property = entry.Editor.serializedObject.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    haystack.Append(' ').Append(property.displayName).Append(' ').Append(property.propertyPath);
                }
            }
            catch (Exception)
            {
                // A native editor may not expose serialized properties.
            }
            return MatchesSearch(query, haystack.ToString());
        }

        internal static bool MatchesSearch(string query, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string haystack = string.Join(" ", values ?? Array.Empty<string>());
            return query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .All(token => haystack.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static List<BetterInspectorComponentGroup> BuildComponentGroups(GameObject[] gameObjects)
        {
            var result = new List<BetterInspectorComponentGroup>();
            if (gameObjects == null || gameObjects.Length == 0 || gameObjects.Any(gameObject => gameObject == null))
            {
                return result;
            }

            var ordinalByType = new Dictionary<Type, int>();
            foreach (Component primary in gameObjects[0].GetComponents<Component>())
            {
                if (primary == null)
                {
                    continue;
                }

                Type type = primary.GetType();
                ordinalByType.TryGetValue(type, out int ordinal);
                ordinalByType[type] = ordinal + 1;
                var components = new Component[gameObjects.Length];
                components[0] = primary;
                bool common = true;
                for (int targetIndex = 1; targetIndex < gameObjects.Length; targetIndex++)
                {
                    Component[] matching = gameObjects[targetIndex].GetComponents(type);
                    if (ordinal >= matching.Length || matching[ordinal] == null)
                    {
                        common = false;
                        break;
                    }
                    components[targetIndex] = matching[ordinal];
                }

                if (common)
                {
                    result.Add(new BetterInspectorComponentGroup(type, ordinal, components));
                }
            }
            return result;
        }

        private void RebuildEditors(bool force)
        {
            Object[] targets = GetTargets();
            string signature = BuildSignature(targets);
            if (!force && string.Equals(signature, editorSignature, StringComparison.Ordinal))
            {
                editorsDirty = false;
                return;
            }

            editorSignature = signature;
            editorsDirty = false;
            DisposeEditors();
            issues = BetterInspectorDiagnostics.Scan(targets);
            if (targets.Length == 0)
            {
                return;
            }

            if (targets.All(target => target is GameObject))
            {
                foreach (BetterInspectorComponentGroup group in BuildComponentGroups(targets.Cast<GameObject>().ToArray()))
                {
                    UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(group.Components.Cast<Object>().ToArray());
                    if (editor != null)
                    {
                        entries.Add(new BetterInspectorEditorEntry(group.Key, group.Type, group.Components, editor));
                    }
                }
                return;
            }

            Type type = targets[0].GetType();
            if (targets.All(target => target != null && target.GetType() == type))
            {
                Object[] editorTargets = GetNativeEditorTargets(targets);
                UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(editorTargets);
                if (editor != null)
                {
                    UnityEditor.Editor previewEditor = editorTargets.SequenceEqual(targets)
                        ? null
                        : UnityEditor.Editor.CreateEditor(targets);
                    entries.Add(new BetterInspectorEditorEntry(
                        type.AssemblyQualifiedName,
                        type,
                        targets,
                        editor,
                        previewEditor));
                }
            }
        }

        internal static Object[] GetNativeEditorTargets(Object[] targets)
        {
            if (targets == null || targets.Length == 0 || targets.Any(target => target == null))
            {
                return targets ?? Array.Empty<Object>();
            }

            var importers = new List<AssetImporter>(targets.Length);
            Type importerType = null;
            foreach (Object target in targets)
            {
                if (AssetDatabase.IsNativeAsset(target))
                {
                    return targets;
                }

                string path = AssetDatabase.GetAssetPath(target);
                AssetImporter importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path);
                if (importer == null || IsNativeFormatImporter(importer))
                {
                    return targets;
                }

                importerType ??= importer.GetType();
                if (importer.GetType() != importerType)
                {
                    return targets;
                }
                importers.Add(importer);
            }
            return importers.Cast<Object>().ToArray();
        }

        internal static bool IsNativeFormatImporter(AssetImporter importer)
        {
            return importer != null &&
                   string.Equals(importer.GetType().Name, "NativeFormatImporter", StringComparison.Ordinal);
        }

        internal static List<BetterInspectorAction> GetContextActions(Type type)
        {
            var actions = new List<BetterInspectorAction>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (Type current = type; current != null && current != typeof(Object); current = current.BaseType)
            {
                foreach (MethodInfo method in current.GetMethods(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    if (method.IsAbstract || method.ContainsGenericParameters || method.GetParameters().Length != 0)
                    {
                        continue;
                    }

                    foreach (ContextMenu attribute in method.GetCustomAttributes(typeof(ContextMenu), true).Cast<ContextMenu>())
                    {
                        string label = string.IsNullOrWhiteSpace(attribute.menuItem)
                            ? ObjectNames.NicifyVariableName(method.Name)
                            : attribute.menuItem;
                        string identity = method.DeclaringType?.AssemblyQualifiedName + "::" + method.Name + "::" + label;
                        if (seen.Add(identity))
                        {
                            actions.Add(new BetterInspectorAction(label, method));
                        }
                    }
                }
            }
            return actions.OrderBy(action => action.Label, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string BuildSignature(Object[] targets)
        {
            var builder = new StringBuilder();
            foreach (Object target in targets)
            {
                builder.Append(target == null ? 0 : target.GetInstanceID()).Append(':');
                if (target is GameObject gameObject)
                {
                    foreach (Component component in gameObject.GetComponents<Component>())
                    {
                        builder.Append(component == null ? "missing" : component.GetInstanceID().ToString()).Append(',');
                    }
                }
                builder.Append('|');
            }
            return builder.ToString();
        }

        private Object[] GetTargets()
        {
            Object[] source = targetLocked ? lockedTargets : Selection.objects;
            return source?.Where(target => target != null).Distinct().ToArray() ?? Array.Empty<Object>();
        }

        private void ToggleLock()
        {
            if (targetLocked)
            {
                targetLocked = false;
                lockedTargets = Array.Empty<Object>();
                RecordHistory(Selection.activeObject);
            }
            else
            {
                lockedTargets = Selection.objects.Where(target => target != null).ToArray();
                targetLocked = lockedTargets.Length > 0;
            }
            RebuildEditors(force: true);
            Repaint();
        }

        private void RecordHistory(Object target)
        {
            if (navigatingHistory || targetLocked || target == null)
            {
                return;
            }

            history.RemoveAll(item => item == null);
            if (historyIndex >= 0 && historyIndex < history.Count && history[historyIndex] == target)
            {
                return;
            }

            if (historyIndex < history.Count - 1)
            {
                history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
            }
            history.Add(target);
            if (history.Count > 50)
            {
                history.RemoveAt(0);
            }
            historyIndex = history.Count - 1;
        }

        private void NavigateHistory(int direction)
        {
            int next = Mathf.Clamp(historyIndex + direction, 0, history.Count - 1);
            if (next == historyIndex || next < 0 || history[next] == null)
            {
                return;
            }

            navigatingHistory = true;
            historyIndex = next;
            Selection.activeObject = history[next];
            navigatingHistory = false;
            RebuildEditors(force: true);
            Repaint();
        }

        private void ShowEntryMenu(BetterInspectorEditorEntry entry, Rect anchor)
        {
            GenericMenu menu = new GenericMenu();
            bool favorite = IsFavorite(entry.Type);
            menu.AddItem(new GUIContent(favorite ? "Remove Favorite" : "Add Favorite"), false, () => SetFavorite(entry.Type, !favorite));
            menu.AddSeparator(string.Empty);
            if (entry.Targets[0] is Component component)
            {
                menu.AddItem(new GUIContent("Copy Values"), false, () => ComponentUtility.CopyComponent(component));
                menu.AddItem(new GUIContent("Paste Values"), false, () => PasteValues(entry));
                menu.AddSeparator(string.Empty);
                bool movable = !(component is Transform);
                if (movable)
                {
                    menu.AddItem(new GUIContent("Move Up"), false, () => MoveComponents(entry, true));
                    menu.AddItem(new GUIContent("Move Down"), false, () => MoveComponents(entry, false));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Move Up"));
                    menu.AddDisabledItem(new GUIContent("Move Down"));
                }
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Unity Component Menu…"), false, () =>
                    EditorUtility.DisplayPopupMenu(anchor, "CONTEXT/" + entry.Type.Name + "/", new MenuCommand(component)));
                if (movable)
                {
                    menu.AddItem(new GUIContent("Remove Component…"), false, () => RemoveComponents(entry));
                }
            }
            else
            {
                menu.AddItem(new GUIContent("Ping"), false, () => EditorGUIUtility.PingObject(entry.Targets[0]));
                menu.AddItem(new GUIContent("Reveal in Project"), false, () => Selection.activeObject = entry.Targets[0]);
            }
            menu.DropDown(anchor);
        }

        private void PasteValues(BetterInspectorEditorEntry entry)
        {
            Component[] components = entry.Targets.OfType<Component>().ToArray();
            Undo.RecordObjects(components, "Paste Component Values");
            foreach (Component component in components)
            {
                ComponentUtility.PasteComponentValues(component);
                EditorUtility.SetDirty(component);
            }
            OnObjectsChanged();
        }

        private void MoveComponents(BetterInspectorEditorEntry entry, bool up)
        {
            foreach (Component component in entry.Targets.OfType<Component>())
            {
                if (up)
                {
                    ComponentUtility.MoveComponentUp(component);
                }
                else
                {
                    ComponentUtility.MoveComponentDown(component);
                }
            }
            OnObjectsChanged();
        }

        private void RemoveComponents(BetterInspectorEditorEntry entry)
        {
            Component[] components = entry.Targets.OfType<Component>().ToArray();
            if (components.Length == 0 || !EditorUtility.DisplayDialog(
                    "Remove " + entry.Title,
                    "Remove " + entry.Title + " from " + components.Length +
                    (components.Length == 1 ? " object" : " objects") + "? This can be undone.",
                    "Remove",
                    "Cancel"))
            {
                return;
            }

            foreach (Component component in components)
            {
                Undo.DestroyObjectImmediate(component);
            }
            OnObjectsChanged();
        }

        private void ShowWindowMenu(Rect anchor)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Expand All"), false, () => SetAllCollapsed(false));
            menu.AddItem(new GUIContent("Collapse All"), false, () => SetAllCollapsed(true));
            menu.AddItem(new GUIContent("Favorites Only"), favoritesOnly, () => favoritesOnly = !favoritesOnly);
            menu.AddItem(new GUIContent("Diagnostics"), diagnosticsOnly, () => diagnosticsOnly = !diagnosticsOnly);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Clear Search"), false, () => search = string.Empty);
            menu.AddItem(new GUIContent("Clear History"), false, () =>
            {
                history.Clear();
                historyIndex = -1;
                RecordHistory(Selection.activeObject);
            });
            Type inspectorType = typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow");
            if (inspectorType != null)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Open Unity Inspector"), false, () => GetWindow(inspectorType));
            }
            menu.DropDown(anchor);
        }

        private void SetAllCollapsed(bool collapsed)
        {
            collapsedKeys.Clear();
            if (collapsed)
            {
                collapsedKeys.AddRange(entries.Select(entry => entry.Key));
            }
            Repaint();
        }

        private void SetCollapsed(string key, bool collapsed)
        {
            collapsedKeys.Remove(key);
            if (collapsed)
            {
                collapsedKeys.Add(key);
            }
        }

        private static HashSet<string> LoadFavorites()
        {
            return new HashSet<string>(
                EditorPrefs.GetString(FavoritesKey, string.Empty)
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
        }

        private bool IsFavorite(Type type)
        {
            return type != null && favoriteTypes.Contains(type.AssemblyQualifiedName);
        }

        private void SetFavorite(Type type, bool favorite)
        {
            if (type == null)
            {
                return;
            }

            if (favorite)
            {
                favoriteTypes.Add(type.AssemblyQualifiedName);
            }
            else
            {
                favoriteTypes.Remove(type.AssemblyQualifiedName);
            }
            EditorPrefs.SetString(FavoritesKey, string.Join(";", favoriteTypes.OrderBy(value => value)));
            Repaint();
        }

        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown)
            {
                return;
            }

            bool action = Application.platform == RuntimePlatform.OSXEditor ? current.command : current.control;
            if (action && current.keyCode == KeyCode.F)
            {
                GUI.FocusControl(SearchControlName);
                current.Use();
            }
            else if (current.alt && current.keyCode == KeyCode.LeftArrow)
            {
                NavigateHistory(-1);
                current.Use();
            }
            else if (current.alt && current.keyCode == KeyCode.RightArrow)
            {
                NavigateHistory(1);
                current.Use();
            }
            else if (current.keyCode == KeyCode.Escape && !string.IsNullOrEmpty(search))
            {
                search = string.Empty;
                GUI.FocusControl(null);
                current.Use();
            }
        }

        private static string GetTargetDetail(Object[] targets)
        {
            if (targets.Length > 1)
            {
                Type common = targets[0].GetType();
                return targets.All(target => target.GetType() == common)
                    ? ObjectNames.NicifyVariableName(common.Name).ToUpperInvariant()
                    : "MIXED TYPES";
            }

            Object target = targets[0];
            if (target is GameObject gameObject)
            {
                string prefab = PrefabUtility.IsPartOfPrefabInstance(gameObject) ? "  ·  PREFAB INSTANCE" : string.Empty;
                return gameObject.scene.name.ToUpperInvariant() + prefab;
            }
            return ObjectNames.NicifyVariableName(target.GetType().Name).ToUpperInvariant();
        }

        private void OnSelectionChanged()
        {
            if (targetLocked)
            {
                return;
            }
            RecordHistory(Selection.activeObject);
            RebuildEditors(force: true);
            Repaint();
        }

        private void OnObjectsChanged()
        {
            editorSignature = string.Empty;
            editorsDirty = true;
            Repaint();
        }

        private void OnConsoleDiagnosticsChanged()
        {
            Repaint();
        }

        private void OnThemeChanged()
        {
            styledThemeRevision = -1;
            Repaint();
        }

        private void DisposeEditors()
        {
            foreach (BetterInspectorEditorEntry entry in entries)
            {
                if (entry.Editor != null)
                {
                    DestroyImmediate(entry.Editor);
                }
                if (entry.PreviewEditor != null && entry.PreviewEditor != entry.Editor)
                {
                    DestroyImmediate(entry.PreviewEditor);
                }
            }
            entries.Clear();
        }

        private void EnsureStyles()
        {
            if (styles != null && styledThemeRevision == DansToolboxTheme.Revision)
            {
                return;
            }

            styledThemeRevision = DansToolboxTheme.Revision;
            DestroyStyleTextures();
            DansToolboxPalette palette = DansToolboxTheme.Current;
            styles = new BetterInspectorStyles
            {
                Search = new GUIStyle(EditorStyles.toolbarSearchField)
                {
                    fontSize = 10,
                    fixedHeight = 22f,
                    normal = { textColor = palette.Text, background = MakeTexture(palette.Inset) },
                    focused = { textColor = palette.Text, background = MakeTexture(palette.Raised) }
                },
                Card = new GUIStyle
                {
                    normal = { background = MakeTexture(palette.Panel) },
                    margin = new RectOffset(1, 1, 0, 0),
                    padding = new RectOffset(1, 1, 1, 1)
                },
                CardBody = new GUIStyle
                {
                    normal = { background = MakeTexture(palette.Inset) },
                    padding = new RectOffset(10, 10, 8, 8)
                },
                IssueCard = new GUIStyle
                {
                    normal = { background = MakeTexture(palette.Panel) },
                    padding = new RectOffset(10, 10, 8, 8)
                },
                HeaderTitle = Label(palette.Text, 11, FontStyle.Bold),
                ObjectName = Label(palette.Text, 14, FontStyle.Bold),
                CardTitle = Label(palette.Text, 10, FontStyle.Bold),
                Muted = Label(palette.Muted, 10, FontStyle.Normal),
                Path = Label(palette.Muted, 9, FontStyle.Normal),
                Foldout = CenteredLabel(palette.Muted, 12, FontStyle.Bold),
                MiniLabel = Label(palette.Text, 8, FontStyle.Bold),
                CompactPopup = new GUIStyle(EditorStyles.popup) { fontSize = 9, fixedHeight = 18f },
                SectionLabel = Label(palette.Text, 11, FontStyle.Bold),
                WarningBadge = CenteredLabel(palette.Warning, 8, FontStyle.Bold),
                CenteredTitle = CenteredLabel(palette.Text, 12, FontStyle.Bold),
                CenteredMuted = CenteredLabel(palette.Muted, 10, FontStyle.Normal),
                SmallButton = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 8,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = palette.Text, background = MakeTexture(palette.Raised) },
                    hover = { textColor = palette.Accent, background = MakeTexture(palette.Hover) }
                },
                PrimaryButton = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = palette.Text, background = MakeTexture(palette.AccentSoft) },
                    hover = { textColor = palette.Text, background = MakeTexture(palette.Accent) }
                },
                PreviewBackground = new GUIStyle
                {
                    normal = { background = MakeTexture(palette.Canvas) },
                    padding = new RectOffset(1, 1, 1, 1)
                }
            };
            styles.ObjectName.clipping = TextClipping.Clip;
            styles.Muted.wordWrap = true;
            styles.CenteredMuted.wordWrap = true;
        }

        private Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            styleTextures.Add(texture);
            return texture;
        }

        private void DestroyStyleTextures()
        {
            foreach (Texture2D texture in styleTextures)
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }
            styleTextures.Clear();
            styles = null;
        }

        private static GUIStyle Label(Color color, int size, FontStyle fontStyle)
        {
            return new GUIStyle(EditorStyles.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                normal = { textColor = color }
            };
        }

        private static GUIStyle CenteredLabel(Color color, int size, FontStyle fontStyle)
        {
            GUIStyle style = Label(color, size, fontStyle);
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        internal static bool DrawIconButton(
            Rect rect,
            string label,
            string tooltip,
            bool enabled,
            DansToolboxPalette palette,
            bool active = false)
        {
            bool hovered = enabled && rect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(rect, active ? palette.AccentSoft : hovered ? palette.Raised : palette.Inset);
            GUI.Label(rect, new GUIContent(label, tooltip), new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = !enabled ? palette.Muted : active || hovered ? palette.Accent : palette.Text }
            });
            EditorGUI.BeginDisabledGroup(!enabled);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            EditorGUI.EndDisabledGroup();
            return clicked;
        }

        internal static bool DrawFlatButton(
            Rect rect,
            string label,
            string tooltip,
            bool accent,
            DansToolboxPalette palette)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(rect, accent ? palette.Accent : hovered ? palette.BorderStrong : palette.Border);
            EditorGUI.DrawRect(
                new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f),
                accent ? palette.AccentSoft : hovered ? palette.Raised : palette.Inset);
            GUI.Label(rect, new GUIContent(label, tooltip), new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                normal = { textColor = palette.Text }
            });
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }
    }

    internal readonly struct BetterInspectorComponentGroup
    {
        internal BetterInspectorComponentGroup(Type type, int ordinal, Component[] components)
        {
            Type = type;
            Ordinal = ordinal;
            Components = components;
        }

        internal Type Type { get; }
        internal int Ordinal { get; }
        internal Component[] Components { get; }
        internal string Key => Type.AssemblyQualifiedName + "#" + Ordinal;
    }

    internal sealed class BetterInspectorEditorEntry
    {
        internal BetterInspectorEditorEntry(
            string key,
            Type type,
            Object[] targets,
            UnityEditor.Editor editor,
            UnityEditor.Editor previewEditor = null)
        {
            Key = key;
            Type = type;
            Targets = targets;
            Editor = editor;
            PreviewEditor = previewEditor;
            Actions = BetterInspectorWindow.GetContextActions(type);
            Title = ObjectNames.NicifyVariableName(type.Name).ToUpperInvariant();
        }

        internal string Key { get; }
        internal Type Type { get; }
        internal Object[] Targets { get; }
        internal UnityEditor.Editor Editor { get; }
        internal UnityEditor.Editor PreviewEditor { get; }
        internal IReadOnlyList<BetterInspectorAction> Actions { get; }
        internal string Title { get; }

        internal UnityEditor.Editor GetPreviewEditor()
        {
            try
            {
                if (Editor != null && Editor.HasPreviewGUI())
                {
                    return Editor;
                }
            }
            catch (Exception)
            {
                // Fall back to the selected object's editor below.
            }
            return PreviewEditor;
        }
    }

    internal readonly struct BetterInspectorAction
    {
        internal BetterInspectorAction(string label, MethodInfo method)
        {
            Label = label;
            Method = method;
        }

        internal string Label { get; }
        internal MethodInfo Method { get; }
    }

    internal sealed class BetterInspectorEditorVisibilityScope : IDisposable
    {
        private readonly Object[] targets;
        private readonly bool[] previousStates;

        internal BetterInspectorEditorVisibilityScope(Object[] editorTargets)
        {
            targets = editorTargets?.Where(target => target != null).Distinct().ToArray() ?? Array.Empty<Object>();
            previousStates = new bool[targets.Length];
            for (int index = 0; index < targets.Length; index++)
            {
                previousStates[index] = InternalEditorUtility.GetIsInspectorExpanded(targets[index]);
                if (!previousStates[index])
                {
                    InternalEditorUtility.SetIsInspectorExpanded(targets[index], true);
                }
            }
        }

        public void Dispose()
        {
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null && !previousStates[index])
                {
                    InternalEditorUtility.SetIsInspectorExpanded(targets[index], false);
                }
            }
        }
    }

    internal sealed class BetterInspectorStyles
    {
        internal GUIStyle Search;
        internal GUIStyle Card;
        internal GUIStyle CardBody;
        internal GUIStyle IssueCard;
        internal GUIStyle HeaderTitle;
        internal GUIStyle ObjectName;
        internal GUIStyle CardTitle;
        internal GUIStyle Muted;
        internal GUIStyle Path;
        internal GUIStyle Foldout;
        internal GUIStyle MiniLabel;
        internal GUIStyle CompactPopup;
        internal GUIStyle SectionLabel;
        internal GUIStyle WarningBadge;
        internal GUIStyle CenteredTitle;
        internal GUIStyle CenteredMuted;
        internal GUIStyle SmallButton;
        internal GUIStyle PrimaryButton;
        internal GUIStyle PreviewBackground;
    }
}
