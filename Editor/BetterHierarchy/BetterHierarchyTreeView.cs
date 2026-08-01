using System;
using System.Collections.Generic;
using System.Linq;
using DansToolbox.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal static class BetterHierarchyRowLayout
    {
        internal static Rect CenterContent(Rect row, float lineHeight)
        {
            float height = Mathf.Min(row.height, lineHeight);
            return new Rect(row.x, row.y + (row.height - height) * 0.5f, row.width, height);
        }
    }

    [Serializable]
    internal sealed class BetterHierarchyExpansionState
    {
        [SerializeField] internal bool HasSearchSnapshot;
        [SerializeField] internal List<int> BeforeSearch = new List<int>();
    }

    internal sealed class BetterHierarchyTreeItem : TreeViewItem
    {
        internal BetterHierarchyTreeItem(int id, int depth, string name) : base(id, depth, name)
        {
        }

        internal GameObject GameObject { get; set; }
        internal BetterHierarchyCollection Collection { get; set; }
        internal Scene Scene { get; set; }
        internal bool IsScene { get; set; }
        internal bool IsCollectionRoot { get; set; }
        internal bool IsVirtualMember { get; set; }
    }

    internal sealed class BetterHierarchyTreeView : TreeView
    {
        private readonly BetterHierarchyWindow host;
        private readonly Dictionary<int, BetterHierarchyTreeItem> items =
            new Dictionary<int, BetterHierarchyTreeItem>();
        private readonly Dictionary<int, List<int>> objectItemIds =
            new Dictionary<int, List<int>>();
        private readonly BetterHierarchyExpansionState expansionState;
        private BetterHierarchyQuery query = BetterHierarchyQuery.Parse(string.Empty);
        private bool syncingSelection;
        private double lastItemContextClickAt = double.NegativeInfinity;

        internal BetterHierarchyTreeView(
            TreeViewState state,
            BetterHierarchyWindow host,
            BetterHierarchyExpansionState expansionState) : base(state)
        {
            this.host = host;
            this.expansionState = expansionState ?? new BetterHierarchyExpansionState();
            rowHeight = BetterHierarchyUserSettings.RowHeight;
            showBorder = false;
            showAlternatingRowBackgrounds = false;
            useScrollView = true;
            enableItemHovering = true;
            Reload();
        }

        internal void SetQuery(string value)
        {
            BetterHierarchyQuery next = BetterHierarchyQuery.Parse(value);
            bool enteringSearch = query.IsEmpty && !next.IsEmpty;
            bool leavingSearch = !query.IsEmpty && next.IsEmpty;
            if (enteringSearch && !expansionState.HasSearchSnapshot)
            {
                expansionState.BeforeSearch = GetExpanded().ToList();
                expansionState.HasSearchSnapshot = true;
            }

            query = next;
            ReloadPreservingExpansion();
            if (!query.IsEmpty)
            {
                ExpandSearchResults();
            }
            else if (leavingSearch && expansionState.HasSearchSnapshot)
            {
                RestoreExpansion(expansionState.BeforeSearch);
                expansionState.BeforeSearch.Clear();
                expansionState.HasSearchSnapshot = false;
            }
        }

        internal void Refresh()
        {
            rowHeight = BetterHierarchyUserSettings.RowHeight;
            ReloadPreservingExpansion();
            if (!query.IsEmpty)
            {
                ExpandSearchResults();
            }
            SyncSelection();
        }

        internal void SyncSelection()
        {
            if (syncingSelection)
            {
                return;
            }

            List<int> selection = new List<int>();
            foreach (int instanceId in Selection.instanceIDs)
            {
                if (objectItemIds.TryGetValue(instanceId, out List<int> ids) && ids.Count > 0)
                {
                    int preferredId = ChoosePreferredSelectionId(ids, items);
                    if (preferredId != 0)
                    {
                        selection.Add(preferredId);
                    }
                }
            }

            syncingSelection = true;
            SetSelection(selection, TreeViewSelectionOptions.None);
            syncingSelection = false;
        }

        internal IReadOnlyList<GameObject> GetVisibleGameObjects()
        {
            return GetRows()
                .OfType<BetterHierarchyTreeItem>()
                .Where(item => item.GameObject != null)
                .Select(item => item.GameObject)
                .Distinct()
                .ToList();
        }

        internal GameObject GetGameObject(int id)
        {
            return items.TryGetValue(id, out BetterHierarchyTreeItem item) ? item.GameObject : null;
        }

        internal void Reveal(GameObject gameObject)
        {
            if (gameObject == null || !objectItemIds.TryGetValue(gameObject.GetInstanceID(), out List<int> ids))
            {
                return;
            }

            int preferredId = ChoosePreferredSelectionId(ids, items);
            if (preferredId != 0)
            {
                SetSelection(new[] { preferredId }, TreeViewSelectionOptions.RevealAndFrame);
            }
        }

        internal static int ChoosePreferredSelectionId(
            IReadOnlyList<int> ids,
            IReadOnlyDictionary<int, BetterHierarchyTreeItem> lookup)
        {
            if (ids == null || lookup == null)
            {
                return 0;
            }

            int fallback = 0;
            foreach (int id in ids)
            {
                if (!lookup.TryGetValue(id, out BetterHierarchyTreeItem item) || item == null)
                {
                    continue;
                }

                fallback = fallback == 0 ? id : fallback;
                if (!item.IsVirtualMember)
                {
                    return id;
                }
            }

            return fallback;
        }

        internal void CollapseToSelection()
        {
            IList<int> selection = GetSelection();
            SetExpanded(new List<int>());
            foreach (int id in selection)
            {
                if (!items.TryGetValue(id, out BetterHierarchyTreeItem item))
                {
                    continue;
                }

                for (TreeViewItem parent = item.parent; parent != null && parent.id != 0; parent = parent.parent)
                {
                    SetExpanded(parent.id, true);
                }
            }

            FrameItem(selection.FirstOrDefault());
        }

        internal bool TryDeleteSelectedCollection()
        {
            IList<int> selection = GetSelection();
            if (selection.Count != 1 ||
                !items.TryGetValue(selection[0], out BetterHierarchyTreeItem item) ||
                item.Collection == null ||
                item.GameObject != null)
            {
                return false;
            }

            host.ConfirmDeleteCollection(item.Collection);
            return true;
        }

        internal bool BeginRenameSelection()
        {
            IList<int> selection = GetSelection();
            if (selection.Count != 1 ||
                !items.TryGetValue(selection[0], out BetterHierarchyTreeItem item) ||
                !CanRename(item))
            {
                return false;
            }

            BeginRename(item);
            return true;
        }

        protected override TreeViewItem BuildRoot()
        {
            items.Clear();
            objectItemIds.Clear();

            BetterHierarchyTreeItem root = new BetterHierarchyTreeItem(0, -1, "Root");
            items[root.id] = root;

            AddCollections(root);
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null)
            {
                BetterHierarchyTreeItem stage = new BetterHierarchyTreeItem(
                    StableIdForKey("prefab-stage:" + prefabStage.assetPath),
                    0,
                    prefabStage.prefabContentsRoot.name.ToUpperInvariant())
                {
                    Scene = prefabStage.scene,
                    IsScene = true,
                    icon = EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D
                };
                root.AddChild(stage);
                AddTransform(stage, prefabStage.prefabContentsRoot.transform, 1, true);
            }
            else
            {
                for (int index = 0; index < SceneManager.sceneCount; index++)
                {
                    Scene scene = SceneManager.GetSceneAt(index);
                    AddScene(root, scene);
                }
            }

            if (!root.hasChildren)
            {
                root.AddChild(new BetterHierarchyTreeItem(StableIdForKey("empty"), 0, "NO OBJECTS"));
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        private void AddScene(BetterHierarchyTreeItem root, Scene scene)
        {
            BetterHierarchyTreeItem sceneItem = new BetterHierarchyTreeItem(
                StableIdForKey(GetSceneKey(scene)),
                0,
                string.IsNullOrEmpty(scene.name) ? "UNTITLED" : scene.name.ToUpperInvariant())
            {
                Scene = scene,
                IsScene = true,
                icon = EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D
            };
            items[sceneItem.id] = sceneItem;

            foreach (GameObject gameObject in scene.GetRootGameObjects())
            {
                AddTransform(sceneItem, gameObject.transform, 1, false);
            }

            if (sceneItem.hasChildren || query.IsEmpty)
            {
                root.AddChild(sceneItem);
            }
        }

        private BetterHierarchyTreeItem AddTransform(
            BetterHierarchyTreeItem parent,
            Transform transform,
            int depth,
            bool forceInclude)
        {
            GameObject gameObject = transform.gameObject;
            BetterHierarchyDiagnosticFlags diagnostics = BetterHierarchyUserSettings.Diagnostics
                ? BetterHierarchyDiagnostics.Get(gameObject)
                : BetterHierarchyDiagnosticFlags.None;
            bool directMatch = query.IsEmpty || query.Matches(
                gameObject,
                diagnostics,
                BetterHierarchyCollections.Contains);

            BetterHierarchyTreeItem item = new BetterHierarchyTreeItem(
                gameObject.GetInstanceID(),
                depth,
                gameObject.name)
            {
                GameObject = gameObject,
                icon = GetPrimaryIcon(gameObject)
            };

            for (int index = 0; index < transform.childCount; index++)
            {
                AddTransform(item, transform.GetChild(index), depth + 1, false);
            }

            if (!directMatch && !item.hasChildren && !forceInclude)
            {
                return null;
            }

            parent.AddChild(item);
            Register(item, gameObject);
            return item;
        }

        private void AddCollections(BetterHierarchyTreeItem root)
        {
            if (BetterHierarchyProjectSettings.Collections.Count == 0)
            {
                return;
            }

            BetterHierarchyTreeItem collectionRoot = new BetterHierarchyTreeItem(
                StableIdForKey("collections-root"),
                0,
                "COLLECTIONS")
            {
                IsCollectionRoot = true,
                icon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D
            };
            items[collectionRoot.id] = collectionRoot;

            foreach (BetterHierarchyCollection collection in BetterHierarchyProjectSettings.Collections)
            {
                BetterHierarchyTreeItem collectionItem = new BetterHierarchyTreeItem(
                    StableIdForKey("collection:" + collection.Id),
                    1,
                    collection.Name)
                {
                    Collection = collection,
                    icon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D
                };
                items[collectionItem.id] = collectionItem;
                foreach (GameObject gameObject in BetterHierarchyCollections.Resolve(collection))
                {
                    BetterHierarchyDiagnosticFlags diagnostics = BetterHierarchyUserSettings.Diagnostics
                        ? BetterHierarchyDiagnostics.Get(gameObject)
                        : BetterHierarchyDiagnosticFlags.None;
                    if (!query.IsEmpty && !query.Matches(gameObject, diagnostics, BetterHierarchyCollections.Contains))
                    {
                        continue;
                    }

                    BetterHierarchyTreeItem member = new BetterHierarchyTreeItem(
                        StableIdForKey(
                            "collection-member:" + collection.Id + ":" + BetterHierarchyObjectIds.Get(gameObject)),
                        2,
                        gameObject.name)
                    {
                        GameObject = gameObject,
                        Collection = collection,
                        IsVirtualMember = true,
                        icon = GetPrimaryIcon(gameObject)
                    };
                    collectionItem.AddChild(member);
                    Register(member, gameObject);
                }

                collectionRoot.AddChild(collectionItem);
            }

            root.AddChild(collectionRoot);
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (syncingSelection)
            {
                return;
            }

            UnityEngine.Object[] selected = selectedIds
                .Select(GetGameObject)
                .Where(gameObject => gameObject != null)
                .Distinct()
                .Cast<UnityEngine.Object>()
                .ToArray();
            if (selected.Length == 0)
            {
                syncingSelection = true;
                Selection.objects = Array.Empty<UnityEngine.Object>();
                syncingSelection = false;
                return;
            }

            syncingSelection = true;
            Selection.objects = selected;
            syncingSelection = false;
            host.RecordSelection(selected[0] as GameObject);
        }

        protected override void DoubleClickedItem(int id)
        {
            GameObject gameObject = GetGameObject(id);
            if (gameObject == null)
            {
                return;
            }

            Selection.activeGameObject = gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return item is BetterHierarchyTreeItem better && better.GameObject != null;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            if (!args.acceptedRename || string.IsNullOrWhiteSpace(args.newName))
            {
                return;
            }

            GameObject gameObject = GetGameObject(args.itemID);
            if (gameObject == null)
            {
                return;
            }

            Undo.RecordObject(gameObject, "Rename GameObject");
            gameObject.name = args.newName.Trim();
            EditorUtility.SetDirty(gameObject);
            ReloadPreservingExpansion();
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            return args.draggedItemIDs.Any(id => GetGameObject(id) != null);
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            GameObject[] dragged = args.draggedItemIDs
                .Select(GetGameObject)
                .Where(gameObject => gameObject != null)
                .Distinct()
                .ToArray();
            if (dragged.Length == 0)
            {
                return;
            }

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = dragged;
            DragAndDrop.SetGenericData("BetterHierarchyDrag", true);
            DragAndDrop.StartDrag(dragged.Length == 1 ? dragged[0].name : dragged.Length + " Objects");
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            BetterHierarchyTreeItem target = args.parentItem as BetterHierarchyTreeItem;
            GameObject[] draggedObjects = DragAndDrop.objectReferences.OfType<GameObject>().ToArray();
            if (draggedObjects.Length == 0)
            {
                return DragAndDropVisualMode.None;
            }

            if (target?.Collection != null)
            {
                if (args.performDrop)
                {
                    BetterHierarchyCollections.AddMembers(target.Collection, draggedObjects);
                    DragAndDrop.AcceptDrag();
                    Refresh();
                }

                return DragAndDropVisualMode.Link;
            }

            if (DragAndDrop.objectReferences.Any(reference => AssetDatabase.Contains(reference)))
            {
                return HandleAssetDrop(args, target);
            }

            if (args.performDrop)
            {
                Transform newParent = target?.GameObject?.transform;
                Scene targetScene = target != null && target.IsScene
                    ? target.Scene
                    : newParent != null ? newParent.gameObject.scene : default;

                foreach (GameObject gameObject in draggedObjects)
                {
                    if (newParent != null && (gameObject == newParent.gameObject || newParent.IsChildOf(gameObject.transform)))
                    {
                        continue;
                    }

                    if (targetScene.IsValid() && gameObject.scene != targetScene)
                    {
                        Undo.MoveGameObjectToScene(gameObject, targetScene, "Move GameObject To Scene");
                    }

                    Undo.SetTransformParent(gameObject.transform, newParent, "Reparent GameObject");
                }

                DragAndDrop.AcceptDrag();
                ReloadPreservingExpansion();
            }

            return DragAndDropVisualMode.Move;
        }

        private DragAndDropVisualMode HandleAssetDrop(
            DragAndDropArgs args,
            BetterHierarchyTreeItem target)
        {
            GameObject[] prefabs = DragAndDrop.objectReferences
                .OfType<GameObject>()
                .Where(PrefabUtility.IsPartOfPrefabAsset)
                .ToArray();
            if (prefabs.Length == 0)
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (args.performDrop)
            {
                Transform parent = target?.GameObject?.transform;
                Scene scene = target != null && target.IsScene
                    ? target.Scene
                    : parent != null ? parent.gameObject.scene : SceneManager.GetActiveScene();
                List<UnityEngine.Object> created = new List<UnityEngine.Object>();
                foreach (GameObject prefab in prefabs)
                {
                    GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                    if (instance == null)
                    {
                        continue;
                    }

                    Undo.RegisterCreatedObjectUndo(instance, "Place Prefab");
                    if (parent != null)
                    {
                        Undo.SetTransformParent(instance.transform, parent, "Parent Prefab");
                    }
                    created.Add(instance);
                }

                Selection.objects = created.ToArray();
                DragAndDrop.AcceptDrag();
                ReloadPreservingExpansion();
            }

            return DragAndDropVisualMode.Copy;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            BetterHierarchyTreeItem item = (BetterHierarchyTreeItem)args.item;
            Rect row = args.rowRect;
            DansToolboxPalette palette = DansToolboxTheme.Current;

            if (BetterHierarchyUserSettings.Zebra && args.row % 2 == 1)
            {
                EditorGUI.DrawRect(row, new Color(palette.Raised.r, palette.Raised.g, palette.Raised.b, 0.18f));
            }

            if (item.Collection != null && item.GameObject == null)
            {
                EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height), item.Collection.Color);
            }

            BetterHierarchyDiagnosticFlags diagnostics = BetterHierarchyDiagnosticFlags.None;
            BetterHierarchyStyle style = default;
            if (item.GameObject != null)
            {
                diagnostics = BetterHierarchyUserSettings.Diagnostics
                    ? BetterHierarchyDiagnostics.Get(item.GameObject, BetterHierarchyUserSettings.Mode == BetterHierarchyMode.Debug)
                    : BetterHierarchyDiagnosticFlags.None;
                style = BetterHierarchyRuleMatcher.GetStyle(item.GameObject, diagnostics);
                if (style.IsValid)
                {
                    Color tint = style.Color;
                    EditorGUI.DrawRect(row, tint);
                    EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height),
                        new Color(tint.r, tint.g, tint.b, Mathf.Max(0.8f, tint.a)));
                    if (!string.IsNullOrEmpty(style.IconName))
                    {
                        item.icon = EditorGUIUtility.IconContent(style.IconName).image as Texture2D ?? item.icon;
                    }
                }
            }

            if (!args.selected && (hoveredItem == item || row.Contains(Event.current.mousePosition)))
            {
                Color hover = Color.Lerp(BetterHierarchyWindow.CanvasColor, palette.Hover, 0.55f);
                EditorGUI.DrawRect(row, hover);
            }

            DrawTreeLines(row, item.depth, palette);

            Color previousContent = GUI.contentColor;
            if (style.IsValid && style.OverrideTextColor)
            {
                GUI.contentColor = style.TextColor;
            }
            else if (item.GameObject != null && !item.GameObject.activeInHierarchy)
            {
                GUI.contentColor = palette.Muted;
            }
            else if (item.IsScene || item.IsCollectionRoot)
            {
                GUI.contentColor = palette.Accent;
            }

            RowGUIArgs centeredArgs = args;
            centeredArgs.rowRect = BetterHierarchyRowLayout.CenterContent(
                row,
                EditorGUIUtility.singleLineHeight);
            base.RowGUI(centeredArgs);
            GUI.contentColor = previousContent;

            if (item.GameObject != null)
            {
                DrawRowDetails(row, item, diagnostics, style, palette);
            }
            else if (item.Collection != null)
            {
                if (row.Contains(Event.current.mousePosition))
                {
                    DrawTinyAction(
                        row,
                        row.xMax - 4f,
                        "×",
                        "Delete collection",
                        false,
                        () => host.ConfirmDeleteCollection(item.Collection),
                        palette);
                }
                else
                {
                    DrawCountBadge(row, BetterHierarchyCollections.Resolve(item.Collection).Count.ToString(), palette.Muted, palette);
                }
            }
            else if (item.IsScene)
            {
                string sceneMark = item.Scene.IsValid() && item.Scene.isDirty ? "●" : string.Empty;
                if (!string.IsNullOrEmpty(sceneMark))
                {
                    DrawCountBadge(row, sceneMark, palette.Warning, palette);
                }
            }
        }

        protected override void BeforeRowsGUI()
        {
            base.BeforeRowsGUI();
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            // TreeView begins its own styled scroll view before this callback. Paint the
            // visible content coordinates here so Unity's default scroll surface cannot
            // cover Better Hierarchy's canvas color.
            Rect visibleCanvas = new Rect(
                state.scrollPos.x,
                state.scrollPos.y,
                treeViewRect.width,
                treeViewRect.height);
            EditorGUI.DrawRect(visibleCanvas, BetterHierarchyWindow.CanvasColor);
        }

        private void DrawRowDetails(
            Rect row,
            BetterHierarchyTreeItem item,
            BetterHierarchyDiagnosticFlags diagnostics,
            BetterHierarchyStyle style,
            DansToolboxPalette palette)
        {
            GameObject gameObject = item.GameObject;
            float right = row.xMax - 4f;
            bool hovered = row.Contains(Event.current.mousePosition);

            if (hovered && BetterHierarchyCollections.IsTransformCollection(gameObject))
            {
                right = DrawTinyAction(
                    row,
                    right,
                    "×",
                    "Delete collection",
                    false,
                    () => host.ConfirmDeleteTransformCollection(gameObject),
                    palette);
            }
            else if (item.IsVirtualMember && hovered)
            {
                right = DrawTinyAction(
                    row,
                    right,
                    "−",
                    "Remove from " + item.Collection.Name,
                    false,
                    () => BetterHierarchyCollections.RemoveMember(item.Collection, gameObject),
                    palette);
            }
            else if (hovered)
            {
                GameObject transformCollection =
                    BetterHierarchyCollections.GetTransformCollectionParent(gameObject);
                if (transformCollection != null)
                {
                    right = DrawTinyAction(
                        row,
                        right,
                        "−",
                        "Remove from " + transformCollection.name,
                        false,
                        () => BetterHierarchyCollections.RemoveFromTransformCollection(gameObject),
                        palette);
                }
            }

            if (BetterHierarchyUserSettings.QuickActions && hovered)
            {
                right = DrawTinyAction(row, right, "★", "Favorite", BetterHierarchyUserSettings.IsFavorite(gameObject), () =>
                {
                    BetterHierarchyUserSettings.ToggleFavorite(gameObject);
                    host.Repaint();
                }, palette);
                right = DrawTinyAction(row, right, "L", "Toggle picking", SceneVisibilityManager.instance.IsPickingDisabled(gameObject), () =>
                {
                    SceneVisibilityManager.instance.TogglePicking(gameObject, false);
                    host.Repaint();
                }, palette);
                right = DrawTinyAction(row, right, "V", "Toggle Scene visibility", SceneVisibilityManager.instance.IsHidden(gameObject), () =>
                {
                    SceneVisibilityManager.instance.ToggleVisibility(gameObject, false);
                    host.Repaint();
                }, palette);
                right = DrawTinyAction(row, right, gameObject.activeSelf ? "●" : "○", "Toggle active", gameObject.activeSelf, () =>
                {
                    Undo.RecordObject(gameObject, "Toggle GameObject");
                    gameObject.SetActive(!gameObject.activeSelf);
                    EditorUtility.SetDirty(gameObject);
                }, palette);
            }

            if (diagnostics != BetterHierarchyDiagnosticFlags.None)
            {
                bool critical = BetterHierarchyDiagnostics.IsCritical(diagnostics);
                right = DrawBadge(row, right, "!", BetterHierarchyDiagnostics.GetTooltip(diagnostics),
                    critical ? palette.Danger : palette.Warning, palette);
            }

            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
            if (prefabRoot == gameObject && PrefabUtility.HasPrefabInstanceAnyOverrides(prefabRoot, false))
            {
                right = DrawBadge(row, right, "P*", "Prefab overrides", palette.Signal, palette);
            }

            if (!string.IsNullOrEmpty(style.Badge))
            {
                right = DrawBadge(row, right, style.Badge, style.Rule.Name, palette.Accent, palette);
            }

            bool showComponents = BetterHierarchyUserSettings.Components &&
                                  BetterHierarchyUserSettings.Mode != BetterHierarchyMode.Clean;
            if (showComponents)
            {
                Component[] components = gameObject.GetComponents<Component>();
                for (int index = components.Length - 1, shown = 0; index >= 0 && shown < 4; index--)
                {
                    Component component = components[index];
                    if (component == null || component is Transform)
                    {
                        continue;
                    }

                    right -= 18f;
                    Rect iconRect = new Rect(right, row.y + (row.height - 16f) * 0.5f, 16f, 16f);
                    Texture icon = EditorGUIUtility.ObjectContent(null, component.GetType()).image;
                    GUIContent content = new GUIContent(icon, component.GetType().Name + " · click to inspect");
                    if (GUI.Button(iconRect, content, GUIStyle.none))
                    {
                        PopupWindow.Show(iconRect, new BetterHierarchyComponentPopup(component));
                        Event.current.Use();
                    }
                    shown++;
                }
            }

            if (BetterHierarchyUserSettings.ChildCounts && gameObject.transform.childCount > 0 && !hovered)
            {
                DrawCountBadge(row, gameObject.transform.childCount.ToString(), palette.Muted, palette, right);
            }
        }

        private static float DrawTinyAction(
            Rect row,
            float right,
            string label,
            string tooltip,
            bool active,
            Action action,
            DansToolboxPalette palette)
        {
            right -= 19f;
            Rect rect = new Rect(right, row.y + 2f, 17f, row.height - 4f);
            if (active)
            {
                EditorGUI.DrawRect(rect, new Color(palette.Accent.r, palette.Accent.g, palette.Accent.b, 0.22f));
            }

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                normal = { textColor = active ? palette.Accent : palette.Muted }
            };
            if (GUI.Button(rect, new GUIContent(label, tooltip), style))
            {
                action();
                Event.current.Use();
            }
            return right;
        }

        private static float DrawBadge(
            Rect row,
            float right,
            string label,
            string tooltip,
            Color color,
            DansToolboxPalette palette)
        {
            float width = Mathf.Clamp(label.Length * 6f + 8f, 16f, 44f);
            right -= width + 3f;
            Rect rect = new Rect(right, row.y + 4f, width, row.height - 8f);
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.2f));
            GUI.Label(rect, new GUIContent(label, tooltip), new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                normal = { textColor = color }
            });
            return right;
        }

        private static void DrawCountBadge(
            Rect row,
            string text,
            Color color,
            DansToolboxPalette palette,
            float right = -1f)
        {
            right = right < 0f ? row.xMax - 4f : right;
            float width = Mathf.Max(18f, text.Length * 6f + 6f);
            Rect rect = new Rect(right - width, row.y + 3f, width, row.height - 6f);
            GUI.Label(rect, text, new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                normal = { textColor = color }
            });
        }

        private static void DrawTreeLines(Rect row, int depth, DansToolboxPalette palette)
        {
            if (!BetterHierarchyUserSettings.TreeLines || depth <= 0)
            {
                return;
            }

            Color line = new Color(palette.BorderStrong.r, palette.BorderStrong.g, palette.BorderStrong.b, 0.28f);
            for (int index = 0; index < depth; index++)
            {
                float x = row.x + 14f + index * 14f;
                EditorGUI.DrawRect(new Rect(x, row.y, 1f, row.height), line);
            }
        }

        protected override void ContextClickedItem(int id)
        {
            if (!items.TryGetValue(id, out BetterHierarchyTreeItem item))
            {
                return;
            }

            lastItemContextClickAt = EditorApplication.timeSinceStartup;
            ShowContextForItem(item);
        }

        protected override void ContextClicked()
        {
            if (EditorApplication.timeSinceStartup - lastItemContextClickAt < 0.1d)
            {
                return;
            }

            if (TryGetItemAtMousePosition(out BetterHierarchyTreeItem item))
            {
                ShowContextForItem(item);
                return;
            }

            SetSelection(Array.Empty<int>(), TreeViewSelectionOptions.FireSelectionChanged);
            host.ShowBlankContextMenu();
        }

        private bool TryGetItemAtMousePosition(out BetterHierarchyTreeItem item)
        {
            item = null;
            Event current = Event.current;
            IList<TreeViewItem> rows = GetRows();
            if (current == null || rows == null || rows.Count == 0)
            {
                return false;
            }

            for (int row = 0; row < rows.Count; row++)
            {
                if (!GetRowRect(row).Contains(current.mousePosition))
                {
                    continue;
                }

                item = rows[row] as BetterHierarchyTreeItem;
                if (item == null)
                {
                    items.TryGetValue(rows[row].id, out item);
                }
                return item != null;
            }

            return false;
        }

        private void ShowContextForItem(BetterHierarchyTreeItem item)
        {
            if (item == null)
            {
                return;
            }

            if (item.GameObject != null)
            {
                GameObject gameObject = item.GameObject;
                if (!Selection.gameObjects.Contains(gameObject))
                {
                    Selection.activeGameObject = gameObject;
                    SetSelection(new[] { item.id }, TreeViewSelectionOptions.FireSelectionChanged);
                }

                host.ShowGameObjectContextMenu(
                    gameObject,
                    item.IsVirtualMember ? item.Collection : null);
                return;
            }

            if (item.Collection != null)
            {
                host.ShowVirtualCollectionContextMenu(item.Collection);
                return;
            }

            if (item.IsScene)
            {
                host.ShowSceneContextMenu(item.Scene);
                return;
            }

            host.ShowBlankContextMenu();
        }

        protected override void AfterRowsGUI()
        {
            base.AfterRowsGUI();
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            bool overRow = false;
            IList<TreeViewItem> rows = GetRows();
            for (int row = 0; rows != null && row < rows.Count; row++)
            {
                if (GetRowRect(row).Contains(current.mousePosition))
                {
                    overRow = true;
                    break;
                }
            }

            if (!overRow)
            {
                SetSelection(Array.Empty<int>(), TreeViewSelectionOptions.FireSelectionChanged);
                current.Use();
            }
        }

        private void Register(BetterHierarchyTreeItem item, GameObject gameObject)
        {
            items[item.id] = item;
            if (!objectItemIds.TryGetValue(gameObject.GetInstanceID(), out List<int> ids))
            {
                ids = new List<int>();
                objectItemIds[gameObject.GetInstanceID()] = ids;
            }
            ids.Add(item.id);
        }

        internal static int StableIdForKey(string key)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string source = key ?? string.Empty;
                for (int index = 0; index < source.Length; index++)
                {
                    hash ^= source[index];
                    hash *= 16777619u;
                }

                return (int)(0xC0000000u | (hash & 0x3FFFFFFFu));
            }
        }

        internal static List<int> KeepExistingExpansion(
            IEnumerable<int> expanded,
            IReadOnlyDictionary<int, BetterHierarchyTreeItem> lookup)
        {
            if (expanded == null || lookup == null)
            {
                return new List<int>();
            }

            return expanded.Where(lookup.ContainsKey).Distinct().ToList();
        }

        private void ReloadPreservingExpansion()
        {
            List<int> expanded = GetExpanded().ToList();
            Reload();
            RestoreExpansion(expanded);
        }

        private void RestoreExpansion(IEnumerable<int> expanded)
        {
            SetExpanded(KeepExistingExpansion(expanded, items));
        }

        private void ExpandSearchResults()
        {
            HashSet<int> expanded = GetExpanded().ToHashSet();
            foreach (BetterHierarchyTreeItem item in items.Values)
            {
                bool isCollection = item.IsCollectionRoot ||
                                    item.Collection != null ||
                                    BetterHierarchyCollections.IsTransformCollection(item.GameObject);
                if (item.hasChildren && !isCollection)
                {
                    expanded.Add(item.id);
                }
            }
            RestoreExpansion(expanded);
        }

        private static string GetSceneKey(Scene scene)
        {
            return !string.IsNullOrEmpty(scene.path)
                ? "scene:" + scene.path
                : "scene:" + scene.handle + ":" + scene.name;
        }

        private static Texture2D GetPrimaryIcon(GameObject gameObject)
        {
            Component component = gameObject.GetComponents<Component>()
                .FirstOrDefault(candidate => candidate != null && !(candidate is Transform));
            return component != null
                ? EditorGUIUtility.ObjectContent(null, component.GetType()).image as Texture2D
                : EditorGUIUtility.IconContent("GameObject Icon").image as Texture2D;
        }
    }
}
