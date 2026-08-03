using System;
using System.Collections.Generic;
using System.Linq;
using DansToolbox.Editor;
using DansToolbox.EditorTools.BetterConsole;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_6000_3_OR_NEWER
using HierarchyTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#else
using HierarchyTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState;
#endif

namespace DansToolbox.EditorTools.BetterHierarchy
{
    public sealed class BetterHierarchyWindow : EditorWindow
    {
        private const float ToolbarHeight = 38f;
        private const string MenuPath = "Tools/Dans Toolbox/Better Hierarchy";
        private const string SearchControlName = "BetterHierarchySearch";
        internal const string NativeGameObjectMenuPath = "GameObject/";

        internal static Color CanvasColor => DansToolboxTheme.Current.Canvas;

        [SerializeField] private HierarchyTreeViewState treeState;
        [SerializeField] private BetterHierarchyExpansionState treeExpansionState;
        [SerializeField] private BetterHierarchySurface surface;
        [SerializeField] private BetterHierarchyAtlasSource atlasSource;
        [SerializeField] private string search = string.Empty;
        [SerializeField] private float atlasTileSize = 112f;
        [SerializeField] private Vector2 atlasScroll;
        [SerializeField] private List<string> selectionHistory = new List<string>();
        [SerializeField] private int historyIndex = -1;

        private BetterHierarchyTreeView tree;
        private BetterHierarchyAtlasView atlas;
        private GUIStyle searchStyle;
        private GUIStyle toolbarLabel;
        private Texture2D searchNormalBackground;
        private Texture2D searchFocusedBackground;
        private int styledThemeRevision = -1;
        private bool navigatingHistory;
        [NonSerialized] private double revealStartedAt;

        [MenuItem(MenuPath, false, 20)]
        internal static void Open()
        {
            BetterHierarchyWindow window = GetWindow<BetterHierarchyWindow>();
            DansToolboxWindowChrome.ApplyCompactTitle(
                window,
                DansToolboxTools.BetterHierarchyId);
            window.minSize = new Vector2(260f, 240f);
            window.Show();
            window.Focus();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOpen()
        {
            return DansToolboxSettings.IsToolEnabled(DansToolboxTools.BetterHierarchyId);
        }

        private void OnEnable()
        {
            revealStartedAt = EditorApplication.timeSinceStartup;
            treeState ??= new HierarchyTreeViewState();
            treeExpansionState ??= new BetterHierarchyExpansionState();
            tree = new BetterHierarchyTreeView(treeState, this, treeExpansionState);
            atlas = new BetterHierarchyAtlasView(this);
            DansToolboxWindowChrome.ApplyCompactTitle(
                this,
                DansToolboxTools.BetterHierarchyId);
            minSize = new Vector2(260f, 240f);
            wantsMouseMove = true;

            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            BetterHierarchyProjectSettings.Changed -= OnProjectSettingsChanged;
            BetterHierarchyProjectSettings.Changed += OnProjectSettingsChanged;
            BetterConsoleDiagnosticBridge.Changed -= OnConsoleDiagnosticsChanged;
            BetterConsoleDiagnosticBridge.Changed += OnConsoleDiagnosticsChanged;
            DansToolboxTheme.Changed -= OnThemeChanged;
            DansToolboxTheme.Changed += OnThemeChanged;

            tree.SetQuery(search);
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            BetterHierarchyProjectSettings.Changed -= OnProjectSettingsChanged;
            BetterConsoleDiagnosticBridge.Changed -= OnConsoleDiagnosticsChanged;
            DansToolboxTheme.Changed -= OnThemeChanged;
            atlas?.Dispose();
            DestroySearchTextures();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DansToolboxPalette palette = DansToolboxTheme.Current;
            Rect canvas = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(canvas, CanvasColor);

            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }

            if (!DansToolboxSettings.IsToolEnabled(DansToolboxTools.BetterHierarchyId))
            {
                DrawDisabled(canvas, palette);
                return;
            }

            HandleKeyboard();
            Rect toolbar = new Rect(0f, 0f, position.width, ToolbarHeight);
            Rect content = new Rect(0f, toolbar.yMax, position.width,
                Mathf.Max(1f, position.height - toolbar.yMax));

            DrawToolbar(toolbar, palette);
            EditorGUI.DrawRect(content, CanvasColor);
            if (surface == BetterHierarchySurface.Tree)
            {
                tree.OnGUI(new Rect(content.x + 2f, content.y + 2f, content.width - 4f, content.height - 4f));
            }
            else
            {
                atlas.Draw(content, ref atlasScroll, search, ref atlasSource, ref atlasTileSize, palette);
            }

            if (AssetPreview.IsLoadingAssetPreviews())
            {
                Repaint();
            }

            if (DansToolboxMotion.DrawWindowReveal(canvas, revealStartedAt))
            {
                Repaint();
            }
        }

        private void DrawToolbar(Rect rect, DansToolboxPalette palette)
        {
            EditorGUI.DrawRect(rect, palette.Panel);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), palette.Border);

            bool narrow = position.width < 420f;
            bool wide = position.width >= 610f;
            float x = 6f;
            float treeWidth = narrow ? 26f : 46f;
            float atlasWidth = narrow ? 26f : 48f;
            DrawSurfaceButton(new Rect(x, 6f, treeWidth, 26f), BetterHierarchySurface.Tree, narrow ? "T" : "TREE", palette);
            x += treeWidth + 4f;
            DrawSurfaceButton(new Rect(x, 6f, atlasWidth, 26f), BetterHierarchySurface.Atlas, narrow ? "A" : "ATLAS", palette);
            x += atlasWidth + 8f;

            if (!narrow)
            {
                if (DrawIconButton(new Rect(x, 8f, 22f, 22f), "<", "Back", historyIndex > 0, palette))
                {
                    NavigateHistory(-1);
                }
                x += 24f;
                if (DrawIconButton(new Rect(x, 8f, 22f, 22f), ">", "Forward", historyIndex >= 0 && historyIndex < selectionHistory.Count - 1, palette))
                {
                    NavigateHistory(1);
                }
                x += 28f;
            }

            if (wide)
            {
                float sceneWidth = Mathf.Min(126f, Mathf.Max(72f, position.width * 0.2f));
                if (DrawFlatButton(new Rect(x, 8f, sceneWidth, 22f), GetSceneLabel(), "Scenes", false, palette))
                {
                    ShowSceneMenu(new Rect(x, 8f, sceneWidth, 22f));
                }
                x += sceneWidth + 6f;
            }

            float actionsWidth = narrow ? 104f : 128f;
            float searchWidth = Mathf.Max(80f, rect.width - x - actionsWidth - 8f);
            GUI.SetNextControlName(SearchControlName);
            string updated = GUI.TextField(new Rect(x, 8f, searchWidth, 22f), search, searchStyle);
            if (!string.Equals(updated, search, StringComparison.Ordinal))
            {
                SetSearch(updated);
            }
            x += searchWidth + 5f;

            if (DrawIconButton(new Rect(x, 8f, 22f, 22f), "#", "Saved searches", true, palette))
            {
                ShowSavedSearchMenu(new Rect(x, 8f, 22f, 22f));
            }
            x += 24f;
            if (DrawIconButton(new Rect(x, 8f, 22f, 22f), "+", "Create", true, palette))
            {
                ShowCreateMenu();
            }
            x += 24f;
            if (DrawIconButton(new Rect(x, 8f, 22f, 22f), "C", "Collection", true, palette))
            {
                ShowCollectionPopup(true);
            }
            x += 24f;
            if (!narrow)
            {
                if (DrawIconButton(new Rect(x, 8f, 22f, 22f), "B", "Batch", Selection.gameObjects.Length > 0, palette))
                {
                    BetterHierarchyBatchWindow.Open(Selection.gameObjects);
                }
                x += 24f;
            }
            if (DrawIconButton(new Rect(x, 8f, 22f, 22f), "...", "Rules and view", true, palette))
            {
                ShowSettingsMenu(new Rect(x, 8f, 22f, 22f));
            }
        }

        private void DrawSurfaceButton(
            Rect rect,
            BetterHierarchySurface target,
            string label,
            DansToolboxPalette palette)
        {
            bool active = surface == target;
            Color fill = active ? palette.AccentSoft : palette.Inset;
            EditorGUI.DrawRect(rect, active ? palette.Accent : palette.Border);
            EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), fill);
            GUI.Label(rect, label, new GUIStyle(toolbarLabel)
            {
                normal = { textColor = active ? palette.Text : palette.Muted }
            });
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                surface = target;
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
            GUI.Label(new Rect(panel.x + 14f, panel.y + 16f, panel.width - 28f, 24f), "BETTER HIERARCHY OFF",
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = palette.Text } });
            GUI.Label(new Rect(panel.x + 14f, panel.y + 42f, panel.width - 28f, 20f), "Enable it in Toolbox Setup.",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = palette.Muted } });
            if (DrawFlatButton(new Rect(panel.x + 14f, panel.yMax - 40f, 92f, 24f), "SETUP", "Open setup", true, palette))
            {
                EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Setup Wizard");
            }
        }

        internal void RecordSelection(GameObject gameObject)
        {
            if (navigatingHistory || gameObject == null)
            {
                return;
            }

            BetterHierarchyUserSettings.RecordRecent(gameObject);
            string id = BetterHierarchyObjectIds.Get(gameObject);
            if (string.IsNullOrEmpty(id) ||
                (historyIndex >= 0 && historyIndex < selectionHistory.Count && selectionHistory[historyIndex] == id))
            {
                return;
            }

            if (historyIndex < selectionHistory.Count - 1)
            {
                selectionHistory.RemoveRange(historyIndex + 1, selectionHistory.Count - historyIndex - 1);
            }
            selectionHistory.Add(id);
            if (selectionHistory.Count > 80)
            {
                selectionHistory.RemoveAt(0);
            }
            historyIndex = selectionHistory.Count - 1;
            atlas.Invalidate();
        }

        internal void ShowCollectionPopup(bool virtualByDefault)
        {
            PopupWindow.Show(
                new Rect(Event.current != null ? Event.current.mousePosition : new Vector2(position.width * 0.5f, 40f), Vector2.zero),
                new BetterHierarchyCollectionPopup(Selection.gameObjects, virtualByDefault, RefreshAll));
        }

        internal void RenameCollection(BetterHierarchyCollection collection)
        {
            PopupWindow.Show(
                new Rect(new Vector2(position.width * 0.5f, 40f), Vector2.zero),
                new BetterHierarchyCollectionPopup(collection, RefreshAll));
        }

        internal void ConfirmDeleteCollection(BetterHierarchyCollection collection)
        {
            if (collection == null)
            {
                return;
            }

            if (!BetterHierarchyCollections.HasVirtualCollectionItems(collection))
            {
                BetterHierarchyCollections.DeleteVirtualCollection(collection, deleteItems: false);
                RefreshAll();
                return;
            }

            PopupWindow.Show(
                GetPopupAnchor(),
                new BetterHierarchyDeleteCollectionPopup(collection, RefreshAll));
        }

        internal void ConfirmDeleteTransformCollection(GameObject collectionParent)
        {
            if (collectionParent == null)
            {
                return;
            }

            if (!BetterHierarchyCollections.HasTransformCollectionItems(collectionParent))
            {
                BetterHierarchyCollections.DeleteTransformCollection(collectionParent, deleteItems: false);
                RefreshAll();
                return;
            }

            PopupWindow.Show(
                GetPopupAnchor(),
                new BetterHierarchyDeleteCollectionPopup(collectionParent, RefreshAll));
        }

        private Rect GetPopupAnchor()
        {
            Vector2 point = Event.current != null
                ? Event.current.mousePosition
                : new Vector2(position.width * 0.5f, ToolbarHeight + 24f);
            return new Rect(point, Vector2.zero);
        }

        internal void RefreshAll()
        {
            tree?.Refresh();
            atlas?.Invalidate();
            Repaint();
        }

        internal IReadOnlyList<GameObject> VisibleObjects => tree?.GetVisibleGameObjects() ?? Array.Empty<GameObject>();

        private void NavigateHistory(int direction)
        {
            int candidate = historyIndex + direction;
            if (candidate < 0 || candidate >= selectionHistory.Count)
            {
                return;
            }

            GameObject gameObject = BetterHierarchyObjectIds.Resolve(selectionHistory[candidate]);
            if (gameObject == null)
            {
                selectionHistory.RemoveAt(candidate);
                historyIndex = Mathf.Clamp(historyIndex, 0, selectionHistory.Count - 1);
                return;
            }

            navigatingHistory = true;
            historyIndex = candidate;
            Selection.activeGameObject = gameObject;
            tree.Reveal(gameObject);
            navigatingHistory = false;
        }

        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
            {
                return;
            }

            BetterHierarchyShortcutAction shortcut = BetterHierarchyShortcuts.Resolve(
                current.keyCode,
                current.control || current.command,
                current.shift,
                current.alt);
            if (shortcut != BetterHierarchyShortcutAction.None && PerformShortcut(shortcut))
            {
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Space && !current.control && !current.command &&
                !current.shift && !current.alt)
            {
                surface = surface == BetterHierarchySurface.Tree
                    ? BetterHierarchySurface.Atlas
                    : BetterHierarchySurface.Tree;
                current.Use();
                return;
            }

        }

        private bool PerformShortcut(BetterHierarchyShortcutAction shortcut)
        {
            switch (shortcut)
            {
                case BetterHierarchyShortcutAction.Delete:
                {
                    if (surface == BetterHierarchySurface.Tree && tree.TryDeleteSelectedCollection())
                    {
                        return true;
                    }
                    GameObject transformCollection = Selection.gameObjects
                        .FirstOrDefault(BetterHierarchyCollections.IsTransformCollection);
                    if (transformCollection != null)
                    {
                        ConfirmDeleteTransformCollection(transformCollection);
                        return true;
                    }
                    return DeleteSelectedGameObjects();
                }
                case BetterHierarchyShortcutAction.Rename:
                    return surface == BetterHierarchySurface.Tree && tree.BeginRenameSelection();
                case BetterHierarchyShortcutAction.Duplicate:
                    return EditorApplication.ExecuteMenuItem("Edit/Duplicate");
                case BetterHierarchyShortcutAction.Copy:
                    return EditorApplication.ExecuteMenuItem("Edit/Copy");
                case BetterHierarchyShortcutAction.Cut:
                    return EditorApplication.ExecuteMenuItem("Edit/Cut");
                case BetterHierarchyShortcutAction.Paste:
                    return EditorApplication.ExecuteMenuItem("Edit/Paste");
                case BetterHierarchyShortcutAction.SelectAll:
                    if (surface == BetterHierarchySurface.Tree)
                    {
                        tree.SelectAllRows();
                    }
                    else
                    {
                        atlas.SelectAllEntries();
                    }
                    return true;
                case BetterHierarchyShortcutAction.FocusSearch:
                    EditorGUI.FocusTextInControl(SearchControlName);
                    return true;
                case BetterHierarchyShortcutAction.Frame:
                    if (Selection.activeGameObject == null)
                    {
                        return false;
                    }
                    tree.Reveal(Selection.activeGameObject);
                    SceneView.lastActiveSceneView?.FrameSelected();
                    return true;
                case BetterHierarchyShortcutAction.CreateEmpty:
                    return EditorApplication.ExecuteMenuItem("GameObject/Create Empty");
                case BetterHierarchyShortcutAction.CreateEmptyChild:
                    return EditorApplication.ExecuteMenuItem("GameObject/Create Empty Child");
                case BetterHierarchyShortcutAction.CreateEmptyParent:
                    return EditorApplication.ExecuteMenuItem("GameObject/Create Empty Parent");
                default:
                    return false;
            }
        }

        internal static bool DeleteSelectedGameObjects(bool registerUndo = true)
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            HashSet<GameObject> selected = Selection.gameObjects
                .Where(gameObject => gameObject != null &&
                                     !AssetDatabase.Contains(gameObject) &&
                                     (gameObject.hideFlags & HideFlags.NotEditable) == 0 &&
                                     (prefabStage == null || gameObject != prefabStage.prefabContentsRoot))
                .ToHashSet();
            if (selected.Count == 0)
            {
                return false;
            }

            GameObject[] roots = selected
                .Where(gameObject =>
                {
                    for (Transform parent = gameObject.transform.parent; parent != null; parent = parent.parent)
                    {
                        if (selected.Contains(parent.gameObject))
                        {
                            return false;
                        }
                    }
                    return true;
                })
                .ToArray();

            Selection.objects = Array.Empty<UnityEngine.Object>();
            if (registerUndo)
            {
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(roots.Length == 1 ? "Delete GameObject" : "Delete GameObjects");
                foreach (GameObject root in roots)
                {
                    Undo.DestroyObjectImmediate(root);
                }
                Undo.CollapseUndoOperations(undoGroup);
            }
            else
            {
                foreach (GameObject root in roots)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            return true;
        }

        private void ShowCreateMenu()
        {
            ShowUnityGameObjectMenu(Selection.activeGameObject);
        }

        internal void ShowUnityGameObjectMenu(GameObject context)
        {
            GenericMenu menu = new GenericMenu();
            if (BetterHierarchyContextMenus.AddRegisteredGameObjectItems(menu, context) > 0)
            {
                menu.ShowAsContext();
                return;
            }

            Vector2 mouse = Event.current != null
                ? Event.current.mousePosition
                : new Vector2(position.width * 0.5f, ToolbarHeight);
            EditorUtility.DisplayPopupMenu(
                new Rect(mouse.x, mouse.y, 0f, 0f),
                NativeGameObjectMenuPath,
                new MenuCommand(context));
        }

        internal void ShowBlankContextMenu()
        {
            GenericMenu menu = new GenericMenu();
            BetterHierarchyContextMenus.AddUnityHierarchyObjectItems(
                menu,
                null,
                null,
                null,
                () => tree.SelectAllRows());
            menu.ShowAsContext();
        }

        internal void ShowGameObjectContextMenu(
            GameObject context,
            BetterHierarchyCollection virtualCollection = null)
        {
            if (context == null)
            {
                ShowBlankContextMenu();
                return;
            }

            GenericMenu menu = new GenericMenu();
            Action delete = () =>
            {
                if (BetterHierarchyCollections.IsTransformCollection(context))
                {
                    ConfirmDeleteTransformCollection(context);
                }
                else
                {
                    DeleteSelectedGameObjects();
                }
            };

            BetterHierarchyContextMenus.AddUnityHierarchyObjectItems(
                menu,
                context,
                () => tree.BeginRenameSelection(),
                delete,
                () => tree.SelectAllRows(),
                _ => RefreshAll());

            menu.AddSeparator(string.Empty);
            AddBetterHierarchyObjectItems(menu, context, virtualCollection);
            BetterHierarchyContextMenus.MoveItemsToFront(menu, "Better Hierarchy/");
            menu.ShowAsContext();
        }

        internal void ShowVirtualCollectionContextMenu(BetterHierarchyCollection collection)
        {
            if (collection == null)
            {
                return;
            }

            GameObject[] selectedAtOpen = Selection.gameObjects.ToArray();
            GenericMenu menu = new GenericMenu();
            BetterHierarchyContextMenus.AddRegisteredGameObjectItems(
                menu,
                null,
                created =>
                {
                    BetterHierarchyCollections.AddMembers(collection, new[] { created });
                    RefreshAll();
                },
                useRootContext: true);
            BetterHierarchyContextMenus.AddPackageObjectItems(menu, null);
            menu.AddSeparator(string.Empty);

            const string prefix = "Better Hierarchy/";
            menu.AddItem(new GUIContent(prefix + "Rename"), false, () => RenameCollection(collection));
            if (selectedAtOpen.Length > 0)
            {
                menu.AddItem(new GUIContent(prefix + "Add Selection"), false, () =>
                {
                    BetterHierarchyCollections.AddMembers(collection, selectedAtOpen);
                    RefreshAll();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(prefix + "Add Selection"));
            }

            GameObject[] removable = selectedAtOpen
                .Where(gameObject => BetterHierarchyCollections.Contains(collection, gameObject))
                .ToArray();
            if (removable.Length > 0)
            {
                menu.AddItem(new GUIContent(prefix + "Remove Selection"), false, () =>
                {
                    BetterHierarchyCollections.RemoveMembers(collection, removable);
                    RefreshAll();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(prefix + "Remove Selection"));
            }

            IReadOnlyList<GameObject> members = BetterHierarchyCollections.Resolve(collection);
            if (members.Count > 0)
            {
                menu.AddItem(new GUIContent(prefix + "Select Items"), false,
                    () => Selection.objects = members.Cast<UnityEngine.Object>().ToArray());
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(prefix + "Select Items"));
            }

            menu.AddSeparator(prefix);
            menu.AddItem(new GUIContent(prefix + "Delete Collection…"), false,
                () => ConfirmDeleteCollection(collection));
            BetterHierarchyContextMenus.MoveItemsToFront(menu, "Better Hierarchy/");
            menu.ShowAsContext();
        }

        internal void ShowSceneContextMenu(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            GenericMenu menu = new GenericMenu();
            BetterHierarchyContextMenus.AddRegisteredGameObjectItems(
                menu,
                null,
                created =>
                {
                    if (created != null && created.scene != scene)
                    {
                        SceneManager.MoveGameObjectToScene(created, scene);
                    }
                    RefreshAll();
                },
                useRootContext: true);
            BetterHierarchyContextMenus.AddPackageObjectItems(menu, null);
            menu.AddSeparator(string.Empty);

            const string prefix = "Better Hierarchy/";
            menu.AddItem(new GUIContent(prefix + "Set Active"), scene == SceneManager.GetActiveScene(),
                () => SceneManager.SetActiveScene(scene));
            if (scene.isLoaded)
            {
                menu.AddItem(new GUIContent(prefix + "Save"), false,
                    () => EditorSceneManager.SaveScene(scene));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(prefix + "Save"));
            }
            BetterHierarchyContextMenus.MoveItemsToFront(menu, "Better Hierarchy/");
            menu.ShowAsContext();
        }

        private void AddBetterHierarchyObjectItems(
            GenericMenu menu,
            GameObject context,
            BetterHierarchyCollection virtualCollection)
        {
            const string objectPrefix = "Better Hierarchy/";
            menu.AddItem(new GUIContent(objectPrefix + "Diagnostics/Show in Better Console"), false, () =>
                BetterConsoleDiagnosticBridge.OpenForTargets(
                    Selection.gameObjects.Contains(context) ? Selection.gameObjects : new[] { context }));
            menu.AddItem(new GUIContent(objectPrefix + "Frame"), false, () =>
            {
                Selection.activeGameObject = context;
                SceneView.lastActiveSceneView?.FrameSelected();
            });
            menu.AddItem(new GUIContent(objectPrefix + "Isolate"), false,
                () => SceneVisibilityManager.instance.Isolate(context, true));
            menu.AddItem(new GUIContent(objectPrefix + "Favorite"),
                BetterHierarchyUserSettings.IsFavorite(context), () =>
                {
                    BetterHierarchyUserSettings.ToggleFavorite(context);
                    RefreshAll();
                });
            menu.AddItem(new GUIContent(objectPrefix + (context.activeSelf ? "Disable" : "Enable")), false, () =>
            {
                Undo.RecordObject(context, "Toggle GameObject");
                context.SetActive(!context.activeSelf);
                EditorUtility.SetDirty(context);
                RefreshAll();
            });
            bool hidden = SceneVisibilityManager.instance.IsHidden(context);
            menu.AddItem(new GUIContent(objectPrefix + (hidden ? "Show" : "Hide")), false, () =>
            {
                SceneVisibilityManager.instance.ToggleVisibility(context, false);
                RefreshAll();
            });
            bool locked = SceneVisibilityManager.instance.IsPickingDisabled(context);
            menu.AddItem(new GUIContent(objectPrefix + (locked ? "Unlock" : "Lock")), false, () =>
            {
                SceneVisibilityManager.instance.TogglePicking(context, false);
                RefreshAll();
            });
            menu.AddItem(new GUIContent(objectPrefix + "New Collection…"), false, () =>
            {
                Selection.activeGameObject = context;
                ShowCollectionPopup(true);
            });
            menu.AddItem(new GUIContent(objectPrefix + "Batch…"), false,
                () => BetterHierarchyBatchWindow.Open(Selection.gameObjects));
            menu.AddItem(new GUIContent(objectPrefix + "Set as Default Parent"),
                BetterHierarchyUserSettings.DefaultParent == context,
                () => BetterHierarchyUserSettings.DefaultParent =
                    BetterHierarchyUserSettings.DefaultParent == context ? null : context);

            const string collectionPrefix = "Better Hierarchy/Collection/";
            if (virtualCollection != null)
            {
                string collectionName = virtualCollection.Name.Replace("/", "∕");
                menu.AddItem(new GUIContent(collectionPrefix + "Remove from " + collectionName), false, () =>
                {
                    BetterHierarchyCollections.RemoveMember(virtualCollection, context);
                    RefreshAll();
                });
            }

            GameObject transformCollection =
                BetterHierarchyCollections.GetTransformCollectionParent(context);
            if (transformCollection != null)
            {
                string collectionName = transformCollection.name.Replace("/", "∕");
                menu.AddItem(new GUIContent(collectionPrefix + "Remove from " + collectionName), false, () =>
                {
                    BetterHierarchyCollections.RemoveFromTransformCollection(context);
                    RefreshAll();
                });
            }

            if (BetterHierarchyCollections.IsTransformCollection(context))
            {
                if (context.transform.childCount > 0)
                {
                    menu.AddItem(new GUIContent(collectionPrefix + "Select Items"), false, () =>
                    {
                        Selection.objects = Enumerable.Range(0, context.transform.childCount)
                            .Select(index => context.transform.GetChild(index).gameObject)
                            .Cast<UnityEngine.Object>()
                            .ToArray();
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(collectionPrefix + "Select Items"));
                }
                menu.AddSeparator(collectionPrefix);
                menu.AddItem(new GUIContent(collectionPrefix + "Delete Collection…"), false,
                    () => ConfirmDeleteTransformCollection(context));
            }
        }

        private void ShowSceneMenu(Rect anchor)
        {
            GenericMenu menu = new GenericMenu();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                Scene captured = scene;
                menu.AddItem(new GUIContent("Loaded/" + captured.name), captured == SceneManager.GetActiveScene(), () =>
                {
                    SceneManager.SetActiveScene(captured);
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(captured.path);
                });
            }

            menu.AddSeparator(string.Empty);
            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                menu.AddItem(new GUIContent("Open/" + name), false, () =>
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    }
                });
                menu.AddItem(new GUIContent("Additive/" + name), false, () =>
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
            }
            menu.DropDown(anchor);
        }

        private void ShowSettingsMenu(Rect anchor)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Rules"), false, BetterHierarchyRulesWindow.Open);
            menu.AddItem(new GUIContent("View"), false, () =>
                PopupWindow.Show(anchor, new BetterHierarchySettingsPopup(RefreshAll)));
            menu.AddItem(new GUIContent("Batch"), false, () => BetterHierarchyBatchWindow.Open(Selection.gameObjects));
            GameObject defaultParent = BetterHierarchyUserSettings.DefaultParent;
            if (defaultParent != null)
            {
                menu.AddItem(new GUIContent("Default Parent/Ping " + defaultParent.name), false, () => EditorGUIUtility.PingObject(defaultParent));
                menu.AddItem(new GUIContent("Default Parent/Clear"), false, () => BetterHierarchyUserSettings.DefaultParent = null);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Default Parent/None"));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Exit Isolation"), false, () => SceneVisibilityManager.instance.ExitIsolation());
            Type hierarchyType = Type.GetType("UnityEditor.SceneHierarchyWindow,UnityEditor");
            if (hierarchyType != null)
            {
                menu.AddItem(new GUIContent("Unity Hierarchy"), false, () => GetWindow(hierarchyType));
            }
            menu.DropDown(anchor);
        }

        private void ShowSavedSearchMenu(Rect anchor)
        {
            GenericMenu menu = new GenericMenu();
            if (string.IsNullOrWhiteSpace(search))
            {
                menu.AddDisabledItem(new GUIContent("Save Current"));
            }
            else
            {
                string capturedQuery = search.Trim();
                menu.AddItem(new GUIContent("Save Current"), false, () =>
                    BetterHierarchyUserSettings.SaveSearch(capturedQuery, capturedQuery));
            }

            IReadOnlyList<BetterHierarchySavedSearch> searches = BetterHierarchyUserSettings.SavedSearches;
            menu.AddSeparator(string.Empty);
            if (searches.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Saved Searches"));
            }
            foreach (BetterHierarchySavedSearch saved in searches)
            {
                BetterHierarchySavedSearch captured = saved;
                string label = captured.Name.Replace("/", "-");
                menu.AddItem(new GUIContent("Use/" + label), string.Equals(search, captured.Query, StringComparison.Ordinal), () => SetSearch(captured.Query));
                menu.AddItem(new GUIContent("Remove/" + label), false, () => BetterHierarchyUserSettings.RemoveSavedSearch(captured));
            }
            if (!string.IsNullOrEmpty(search))
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Clear"), false, () => SetSearch(string.Empty));
            }
            menu.DropDown(anchor);
        }

        private void SetSearch(string value)
        {
            search = value ?? string.Empty;
            tree.SetQuery(search);
            atlas.Invalidate();
            Repaint();
        }

        private string GetSceneLabel()
        {
            Scene scene = SceneManager.GetActiveScene();
            return string.IsNullOrEmpty(scene.name) ? "SCENE" : scene.name.ToUpperInvariant();
        }

        private void OnSelectionChanged()
        {
            tree?.SyncSelection();
            if (Selection.activeGameObject != null)
            {
                RecordSelection(Selection.activeGameObject);
            }
            Repaint();
        }

        private void OnHierarchyChanged()
        {
            tree?.Refresh();
            atlas?.Invalidate();
            Repaint();
        }

        private void OnProjectChanged()
        {
            atlas?.InvalidateAssets();
            Repaint();
        }

        private void OnUndoRedo() => RefreshAll();
        private void OnProjectSettingsChanged() => RefreshAll();
        private void OnConsoleDiagnosticsChanged() => Repaint();
        private void OnThemeChanged() { styledThemeRevision = -1; Repaint(); }

        private void EnsureStyles()
        {
            if (styledThemeRevision == DansToolboxTheme.Revision && searchStyle != null)
            {
                return;
            }

            styledThemeRevision = DansToolboxTheme.Revision;
            DansToolboxPalette palette = DansToolboxTheme.Current;
            DestroySearchTextures();
            searchNormalBackground = MakeTexture(palette.Inset);
            searchFocusedBackground = MakeTexture(palette.Raised);
            searchStyle = new GUIStyle(EditorStyles.toolbarSearchField)
            {
                fontSize = 10,
                fixedHeight = 22f,
                normal = { textColor = palette.Text, background = searchNormalBackground },
                focused = { textColor = palette.Text, background = searchFocusedBackground }
            };
            toolbarLabel = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8
            };
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void DestroySearchTextures()
        {
            if (searchNormalBackground != null)
            {
                DestroyImmediate(searchNormalBackground);
                searchNormalBackground = null;
            }
            if (searchFocusedBackground != null)
            {
                DestroyImmediate(searchFocusedBackground);
                searchFocusedBackground = null;
            }
        }

        internal static bool DrawIconButton(Rect rect, string label, string tooltip, bool enabled, DansToolboxPalette palette)
        {
            bool hovered = enabled && rect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(rect, hovered ? palette.Raised : palette.Inset);
            GUI.Label(rect, new GUIContent(label, tooltip), new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = enabled ? hovered ? palette.Accent : palette.Text : palette.Muted }
            });
            EditorGUI.BeginDisabledGroup(!enabled);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            EditorGUI.EndDisabledGroup();
            return clicked;
        }

        internal static bool DrawFlatButton(Rect rect, string label, string tooltip, bool accent, DansToolboxPalette palette)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color border = accent ? palette.Accent : hovered ? palette.BorderStrong : palette.Border;
            Color fill = accent ? palette.AccentSoft : hovered ? palette.Raised : palette.Inset;
            EditorGUI.DrawRect(rect, border);
            EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), fill);
            GUI.Label(rect, new GUIContent(label, tooltip), new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = palette.Text }
            });
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }
    }
}
