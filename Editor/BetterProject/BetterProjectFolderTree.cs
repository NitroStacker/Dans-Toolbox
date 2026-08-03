using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#if UNITY_6000_3_OR_NEWER
using BetterProjectTreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using BetterProjectTreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
using BetterProjectTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#else
using BetterProjectTreeView = UnityEditor.IMGUI.Controls.TreeView;
using BetterProjectTreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem;
using BetterProjectTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState;
#endif

namespace DansToolbox.EditorTools.BetterProject
{
    internal sealed class BetterProjectFolderTree : BetterProjectTreeView
    {
        private readonly Action<string> folderSelected;
        private readonly Dictionary<int, string> paths = new Dictionary<int, string>();

        internal BetterProjectFolderTree(BetterProjectTreeViewState state, Action<string> folderSelected)
            : base(state)
        {
            this.folderSelected = folderSelected;
            rowHeight = 22f;
            showBorder = false;
            Reload();
        }

        internal void SelectPath(string path, bool reveal)
        {
            foreach (KeyValuePair<int, string> pair in paths)
            {
                if (!string.Equals(pair.Value, path, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                SetSelection(
                    new[] { pair.Key },
                    reveal ? TreeViewSelectionOptions.RevealAndFrame : TreeViewSelectionOptions.None);
                return;
            }
        }

        protected override BetterProjectTreeViewItem BuildRoot()
        {
            paths.Clear();
            var root = new BetterProjectTreeViewItem(0, -1, "ROOT");
            var items = new List<BetterProjectTreeViewItem>();
            foreach (BetterProjectAssetRecord record in BetterProjectIndex.Records
                         .Where(asset => asset.IsFolder &&
                                         (asset.Path == "Assets" || asset.Path.StartsWith("Assets/", StringComparison.Ordinal))))
            {
                int id = StableId(record.Guid);
                paths[id] = record.Path;
                int depth = record.Path.Count(character => character == '/');
                items.Add(new BetterProjectTreeViewItem(id, Mathf.Max(0, depth), record.Name)
                {
                    icon = EditorGUIUtility.FindTexture("Folder Icon")
                });
            }
            if (BetterProjectSettings.ShowPackages)
            {
                const string packagesRoot = "Packages";
                int packagesId = StableId(packagesRoot);
                paths[packagesId] = packagesRoot;
                items.Add(new BetterProjectTreeViewItem(packagesId, 0, packagesRoot)
                {
                    icon = EditorGUIUtility.FindTexture("Package Manager") ?? EditorGUIUtility.FindTexture("Folder Icon")
                });
                foreach (BetterProjectAssetRecord record in BetterProjectIndex.Records
                             .Where(asset => asset.IsFolder && asset.Path.StartsWith("Packages/", StringComparison.Ordinal))
                             .OrderBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase))
                {
                    int id = StableId(record.Guid);
                    paths[id] = record.Path;
                    int depth = record.Path.Count(character => character == '/');
                    items.Add(new BetterProjectTreeViewItem(id, depth, record.Name)
                    {
                        icon = EditorGUIUtility.FindTexture("Folder Icon")
                    });
                }
            }
            List<BetterProjectTreeViewItem> ordered = items
                .Where(item => paths[item.id].StartsWith("Assets", StringComparison.Ordinal))
                .OrderBy(item => paths[item.id], StringComparer.OrdinalIgnoreCase)
                .Concat(items.Where(item => paths[item.id].StartsWith("Packages", StringComparison.Ordinal))
                    .OrderBy(item => paths[item.id], StringComparer.OrdinalIgnoreCase))
                .ToList();
            SetupParentsAndChildrenFromDepths(root, ordered);
            return root;
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds != null && selectedIds.Count > 0 && paths.TryGetValue(selectedIds[0], out string path) && path != "Packages")
            {
                folderSelected?.Invoke(path);
            }
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            if (args.dragAndDropPosition != DragAndDropPosition.UponItem ||
                !paths.TryGetValue(args.parentItem.id, out string destination))
            {
                return DragAndDropVisualMode.Rejected;
            }
            DragAndDropVisualMode visualMode = BetterProjectOperations.GetDropVisualMode(
                DragAndDrop.paths,
                DragAndDrop.objectReferences,
                destination);
            if (visualMode == DragAndDropVisualMode.Rejected) return visualMode;
            if (args.performDrop)
            {
                BetterProjectOperations.PerformDrop(
                    DragAndDrop.paths,
                    DragAndDrop.objectReferences,
                    destination);
            }
            return visualMode;
        }

        internal static int StableId(string value)
        {
            unchecked
            {
                int hash = (int)2166136261;
                foreach (char character in value ?? string.Empty)
                {
                    hash = (hash ^ character) * 16777619;
                }
                return hash == 0 ? 1 : hash;
            }
        }
    }
}
