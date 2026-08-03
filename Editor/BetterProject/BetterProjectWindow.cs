using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DansToolbox.Editor;
using DansToolbox.EditorTools.BetterConsole;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#if UNITY_6000_3_OR_NEWER
using BetterProjectTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#else
using BetterProjectTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState;
#endif

namespace DansToolbox.EditorTools.BetterProject
{
    public sealed class BetterProjectWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Dans Toolbox/Better Project";
        private const string SearchControlName = "BetterProjectSearch";
        private const float ToolbarHeight = 38f;
        private const float ChipHeight = 25f;
        private const float StatusHeight = 22f;
        private const float DefaultRailWidth = 214f;
        private const float DefaultPreviewWidth = 310f;
        private const float DefaultSplitRatio = 0.5f;
        private const float MinimumRailWidth = 140f;
        private const float MaximumRailWidth = 420f;
        private const float MinimumPreviewWidth = 220f;
        private const float MaximumPreviewWidth = 520f;
        private const float MinimumAssetPaneWidth = 220f;
        private const float ResizeHandleWidth = 7f;
        private const float ListRowHeight = 26f;
        private const float GridGap = 8f;
        private const float GridScrollBarAllowance = 14f;

        [SerializeField] private BetterProjectTreeViewState folderTreeState;
        [SerializeField] private BetterProjectSurface surface;
        [SerializeField] private BetterProjectView view = BetterProjectView.Grid;
        [SerializeField] private BetterProjectSort sort;
        [SerializeField] private BetterProjectSearchScope searchScope = BetterProjectSearchScope.Assets;
        [SerializeField] private BetterProjectLibrarySource librarySource;
        [SerializeField] private bool sortAscending = true;
        [SerializeField] private bool dualPane;
        [SerializeField] private bool secondaryPaneActive;
        [SerializeField] private string currentFolder = "Assets";
        [SerializeField] private string secondaryFolder = "Assets";
        [SerializeField] private string activeCollectionId = string.Empty;
        [SerializeField] private string search = string.Empty;
        [SerializeField] private float tileSize = 112f;
        [SerializeField] private float folderRailWidth = DefaultRailWidth;
        [SerializeField] private float previewPanelWidth = DefaultPreviewWidth;
        [SerializeField] private float splitRatio = DefaultSplitRatio;
        [SerializeField] private Vector2 primaryScroll;
        [SerializeField] private Vector2 secondaryScroll;
        [SerializeField] private Vector2 previewScroll;
        [SerializeField] private Vector2 libraryRailScroll;
        [SerializeField] private Vector2 impactScroll;
        [SerializeField] private List<string> selectedGuids = new List<string>();
        [SerializeField] private List<string> expandedAssetGuids = new List<string>();
        [SerializeField] private List<string> folderHistory = new List<string>();
        [SerializeField] private int folderHistoryIndex = -1;
        [SerializeField] private List<string> secondaryFolderHistory = new List<string>();
        [SerializeField] private int secondaryFolderHistoryIndex = -1;
        [SerializeField] private string renamingGuid = string.Empty;
        [SerializeField] private string renameValue = string.Empty;

        [NonSerialized] private BetterProjectFolderTree folderTree;
        [NonSerialized] private UnityEditor.Editor previewEditor;
        [NonSerialized] private UnityEngine.Object previewTarget;
        [NonSerialized] private bool syncingSelection;
        [NonSerialized] private int lastSeenRevision = -1;
        [NonSerialized] private double revealStartedAt;
        [NonSerialized] private IReadOnlyList<BetterProjectAssetRecord> lastVisible = Array.Empty<BetterProjectAssetRecord>();
        [NonSerialized] private Dictionary<string, BetterProjectAssetRecord[]> visibleCache =
            new Dictionary<string, BetterProjectAssetRecord[]>(StringComparer.Ordinal);
        [NonSerialized] private int visibleCacheRevision = -1;
        [NonSerialized] private Rect lastSearchRect;
        [NonSerialized] private string dragHoverFolderGuid = string.Empty;

        [MenuItem(MenuPath, false, 22)]
        internal static void Open()
        {
            BetterProjectWindow window = GetWindow<BetterProjectWindow>();
            DansToolboxWindowChrome.ApplyCompactTitle(
                window,
                DansToolboxTools.BetterProjectId);
            window.minSize = new Vector2(620f, 320f);
            window.Show();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOpen()
        {
            return DansToolboxSettings.IsToolEnabled(DansToolboxTools.BetterProjectId);
        }

        private void OnEnable()
        {
            DansToolboxWindowChrome.ApplyCompactTitle(
                this,
                DansToolboxTools.BetterProjectId);
            minSize = new Vector2(620f, 320f);
            revealStartedAt = EditorApplication.timeSinceStartup;
            wantsMouseMove = true;
            folderTreeState ??= new BetterProjectTreeViewState();
            selectedGuids ??= new List<string>();
            expandedAssetGuids ??= new List<string>();
            folderHistory ??= new List<string>();
            secondaryFolderHistory ??= new List<string>();
            visibleCache ??= new Dictionary<string, BetterProjectAssetRecord[]>(StringComparer.Ordinal);
            visibleCache.Clear();
            visibleCacheRevision = BetterProjectIndex.Revision;
            currentFolder = AssetDatabase.IsValidFolder(currentFolder) ? currentFolder : "Assets";
            secondaryFolder = AssetDatabase.IsValidFolder(secondaryFolder) ? secondaryFolder : "Assets";
            EnsureHistory();
            BetterProjectSettings.EnsureInitialized();
            BetterProjectIndex.EnsureReady();
            RebuildFolderTree();
            BetterProjectIndex.Changed -= OnIndexChanged;
            BetterProjectIndex.Changed += OnIndexChanged;
            BetterConsoleDiagnosticBridge.Changed -= OnConsoleDiagnosticsChanged;
            BetterConsoleDiagnosticBridge.Changed += OnConsoleDiagnosticsChanged;
            BetterConsoleDiagnosticBridge.AssetRevealRequested -= OnConsoleAssetRevealRequested;
            BetterConsoleDiagnosticBridge.AssetRevealRequested += OnConsoleAssetRevealRequested;
            Selection.selectionChanged -= SyncFromUnitySelection;
            Selection.selectionChanged += SyncFromUnitySelection;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            BetterProjectIndex.Changed -= OnIndexChanged;
            BetterConsoleDiagnosticBridge.Changed -= OnConsoleDiagnosticsChanged;
            BetterConsoleDiagnosticBridge.AssetRevealRequested -= OnConsoleAssetRevealRequested;
            Selection.selectionChanged -= SyncFromUnitySelection;
            Undo.undoRedoPerformed -= OnUndoRedo;
            DestroyPreviewEditor();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.DragExited)
            {
                ClearFolderDropHover();
            }
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), BetterProjectGui.Canvas);
            ReleaseSearchFocusOnPointerDown();
            HandleKeyboard();

            IReadOnlyList<string> tokens = BetterProjectQuery.Tokenize(search);
            float commandHeight = ToolbarHeight + (tokens.Count > 0 ? ChipHeight : 0f);
            Rect toolbarRect = new Rect(0f, 0f, position.width, ToolbarHeight);
            Rect chipsRect = new Rect(0f, ToolbarHeight, position.width, tokens.Count > 0 ? ChipHeight : 0f);
            Rect statusRect = new Rect(0f, position.height - StatusHeight, position.width, StatusHeight);
            Rect bodyRect = new Rect(0f, commandHeight, position.width, Mathf.Max(0f, statusRect.y - commandHeight));

            DrawRefinedToolbar(toolbarRect);
            if (tokens.Count > 0)
            {
                DrawChips(chipsRect, tokens);
            }
            DrawBody(bodyRect);
            DrawStatus(statusRect);

            if (Event.current.type == EventType.MouseMove || AssetPreview.IsLoadingAssetPreviews())
            {
                Repaint();
            }
            if (DansToolboxMotion.DrawWindowReveal(
                    new Rect(0f, 0f, position.width, position.height), revealStartedAt))
            {
                Repaint();
            }
        }

        private void DrawRefinedToolbar(Rect rect)
        {
            BetterProjectGui.DrawPanel(rect, BetterProjectGui.Panel, false);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), BetterProjectGui.Border);

            float x = 6f;
            DrawRefinedSurfaceTab(ref x, rect, BetterProjectSurface.Browse, "BROWSE", "Folders and assets");
            DrawRefinedSurfaceTab(ref x, rect, BetterProjectSurface.Library, "LIBRARY", "Collections and health");
            DrawRefinedSurfaceTab(ref x, rect, BetterProjectSurface.Impact, "IMPACT", "Dependencies and references");

            bool compact = position.width < 760f;
            if (!compact)
            {
                x += 6f;
                Rect back = new Rect(x, rect.y + 8f, 22f, 22f);
                if (BetterProjectGui.ToolbarIconButton(back, BetterProjectToolbarGlyph.Back, "Back  Alt+Left", false, CanNavigateHistory(-1)))
                {
                    NavigateHistory(-1);
                }
                x += 24f;
                Rect forward = new Rect(x, rect.y + 8f, 22f, 22f);
                bool canGoForward = CanNavigateHistory(1);
                if (BetterProjectGui.ToolbarIconButton(forward, BetterProjectToolbarGlyph.Forward, "Forward  Alt+Right", false, canGoForward))
                {
                    NavigateHistory(1);
                }
                x += 30f;
            }

            float utilityWidth = compact ? 100f : 154f;
            float searchWidth = compact ? 138f : Mathf.Clamp(position.width * 0.18f, 160f, 230f);
            float searchX = rect.xMax - 6f - utilityWidth - 6f - searchWidth;
            float scopeWidth = compact ? 46f : 62f;
            float scopeX = searchX - scopeWidth - 4f;
            if (scopeX - x > 72f)
            {
                DrawBreadcrumbs(new Rect(x, rect.y + 8f, scopeX - x - 8f, 22f));
            }

            Rect scopeRect = new Rect(scopeX, rect.y + 8f, scopeWidth, 22f);
            if (BetterProjectGui.ToolbarTab(
                    scopeRect,
                    SearchScopeLabel(compact),
                    false,
                    "Search scope: " + searchScope))
            {
                ShowSearchScopeMenu();
            }

            Rect searchRect = new Rect(searchX, rect.y + 8f, searchWidth, 22f);
            lastSearchRect = searchRect;
            bool searchFocused = GUI.GetNameOfFocusedControl() == SearchControlName;
            bool searchHovered = searchRect.Contains(Event.current.mousePosition);
            bool showClearSearch = !string.IsNullOrEmpty(search);
            CalculateSearchControlRects(
                searchRect,
                showClearSearch,
                out Rect searchFieldRect,
                out Rect clearSearchRect);
            EditorGUI.DrawRect(
                searchRect,
                searchFocused
                    ? BetterProjectGui.Accent
                    : searchHovered ? BetterProjectGui.BorderStrong : BetterProjectGui.Border);
            EditorGUI.DrawRect(
                new Rect(searchRect.x + 1f, searchRect.y + 1f, searchRect.width - 2f, searchRect.height - 2f),
                searchFocused || searchHovered ? BetterProjectGui.Raised : BetterProjectGui.Inset);
            GUI.SetNextControlName(SearchControlName);
            search = GUI.TextField(
                searchFieldRect,
                search,
                BetterProjectGui.Search);
            BetterProjectGui.DrawToolbarGlyph(
                new Rect(searchRect.x + 4f, searchRect.y + 3f, 16f, 16f),
                BetterProjectToolbarGlyph.Search,
                searchFocused || searchHovered ? BetterProjectGui.Accent : BetterProjectGui.MutedColor);
            if (showClearSearch && BetterProjectGui.ToolbarIconButton(
                    clearSearchRect,
                    BetterProjectToolbarGlyph.Close,
                    "Clear"))
            {
                search = string.Empty;
                GUI.FocusControl(SearchControlName);
            }

            float right = searchRect.xMax + 6f;
            if (BetterProjectGui.ToolbarIconButton(new Rect(right, rect.y + 8f, 22f, 22f), BetterProjectToolbarGlyph.List, "List", view == BetterProjectView.List))
            {
                view = BetterProjectView.List;
            }
            right += 24f;
            if (BetterProjectGui.ToolbarIconButton(new Rect(right, rect.y + 8f, 22f, 22f), BetterProjectToolbarGlyph.Grid, "Grid", view == BetterProjectView.Grid))
            {
                view = BetterProjectView.Grid;
            }
            right += 24f;
            if (BetterProjectGui.ToolbarIconButton(new Rect(right, rect.y + 8f, 22f, 22f), BetterProjectToolbarGlyph.Details, "Details", view == BetterProjectView.Details))
            {
                view = BetterProjectView.Details;
            }
            if (!compact)
            {
                right += 28f;
                if (BetterProjectGui.ToolbarIconButton(new Rect(right, rect.y + 8f, 22f, 22f), BetterProjectToolbarGlyph.Split, "Split panes", dualPane))
                {
                    dualPane = !dualPane;
                    if (!dualPane)
                    {
                        secondaryPaneActive = false;
                        folderTree?.SelectPath(currentFolder, true);
                    }
                }
                right += 24f;
                if (BetterProjectGui.ToolbarIconButton(new Rect(right, rect.y + 8f, 22f, 22f), BetterProjectToolbarGlyph.Preview, "Preview", BetterProjectSettings.ShowPreview))
                {
                    BetterProjectSettings.ShowPreview = !BetterProjectSettings.ShowPreview;
                }
            }
            right += 28f;
            if (BetterProjectGui.ToolbarIconButton(new Rect(right, rect.y + 8f, 22f, 22f), BetterProjectToolbarGlyph.More, "More"))
            {
                ShowWindowMenu();
            }
        }

        private void ReleaseSearchFocusOnPointerDown()
        {
            Event evt = Event.current;
            if (evt.type != EventType.MouseDown ||
                lastSearchRect.Contains(evt.mousePosition) ||
                GUI.GetNameOfFocusedControl() != SearchControlName)
            {
                return;
            }

            GUI.FocusControl(null);
            Repaint();
        }

        internal static void CalculateSearchControlRects(
            Rect searchRect,
            bool showClear,
            out Rect fieldRect,
            out Rect clearRect)
        {
            clearRect = new Rect(searchRect.xMax - 21f, searchRect.y + 2f, 18f, 18f);
            float fieldRight = showClear ? clearRect.x - 2f : searchRect.xMax - 1f;
            fieldRect = new Rect(
                searchRect.x + 1f,
                searchRect.y + 1f,
                Mathf.Max(1f, fieldRight - searchRect.x - 1f),
                Mathf.Max(1f, searchRect.height - 2f));
        }

        private void DrawRefinedSurfaceTab(
            ref float x,
            Rect toolbarRect,
            BetterProjectSurface target,
            string label,
            string tooltip)
        {
            float width = label == "LIBRARY" ? 64f : label == "BROWSE" ? 60f : 58f;
            Rect tabRect = new Rect(x, toolbarRect.y + 6f, width, 26f);
            if (BetterProjectGui.ToolbarTab(tabRect, label, surface == target, tooltip))
            {
                surface = target;
                GUI.FocusControl(null);
                if (surface == BetterProjectSurface.Impact &&
                    !BetterProjectIndex.IsReferenceIndexReady &&
                    !BetterProjectIndex.IsReferenceIndexing)
                {
                    BetterProjectIndex.StartReferenceIndex();
                }
            }
            x += width + 4f;
        }

        private string SearchScopeLabel(bool compact)
        {
            switch (searchScope)
            {
                case BetterProjectSearchScope.Packages:
                    return compact ? "PKG" : "PACKAGES";
                case BetterProjectSearchScope.All:
                    return "ALL";
                default:
                    return compact ? "AST" : "ASSETS";
            }
        }

        private void ShowSearchScopeMenu()
        {
            var menu = new GenericMenu();
            AddSearchScopeItem(menu, BetterProjectSearchScope.Assets, "Assets");
            AddSearchScopeItem(menu, BetterProjectSearchScope.Packages, "Packages");
            AddSearchScopeItem(menu, BetterProjectSearchScope.All, "All");
            menu.ShowAsContext();
        }

        private void AddSearchScopeItem(
            GenericMenu menu,
            BetterProjectSearchScope target,
            string label)
        {
            menu.AddItem(new GUIContent(label), searchScope == target, () =>
            {
                searchScope = target;
                visibleCache.Clear();
                Repaint();
            });
        }

        private void DrawToolbar(Rect rect)
        {
            BetterProjectGui.DrawPanel(rect, BetterProjectGui.Panel, false);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), BetterProjectGui.Border);

            float x = 8f;
            DrawSurfaceSegment(ref x, rect, BetterProjectSurface.Browse, "BROWSE", "Folders and assets");
            DrawSurfaceSegment(ref x, rect, BetterProjectSurface.Library, "LIBRARY", "Collections and health");
            DrawSurfaceSegment(ref x, rect, BetterProjectSurface.Impact, "IMPACT", "Dependencies and references");

            bool compact = position.width < 760f;
            if (!compact)
            {
                x += 8f;
                Rect back = new Rect(x, rect.y + 8f, 25f, 25f);
                EditorGUI.BeginDisabledGroup(folderHistoryIndex <= 0);
                if (BetterProjectGui.IconButton(back, new GUIContent("‹", "Back  Alt+Left"))) NavigateHistory(-1);
                EditorGUI.EndDisabledGroup();
                x += 27f;
                Rect forward = new Rect(x, rect.y + 8f, 25f, 25f);
                EditorGUI.BeginDisabledGroup(folderHistoryIndex < 0 || folderHistoryIndex >= folderHistory.Count - 1);
                if (BetterProjectGui.IconButton(forward, new GUIContent("›", "Forward  Alt+Right"))) NavigateHistory(1);
                EditorGUI.EndDisabledGroup();
                x += 31f;
            }

            float rightControls = compact ? 120f : 180f;
            float searchWidth = compact ? 145f : Mathf.Clamp(position.width * 0.24f, 170f, 320f);
            float searchX = rect.xMax - rightControls - searchWidth;
            if (searchX - x > 90f)
            {
                DrawBreadcrumbs(new Rect(x, rect.y + 7f, searchX - x - 8f, 27f));
            }

            Rect searchRect = new Rect(searchX, rect.y + 7f, searchWidth, 28f);
            GUI.SetNextControlName(SearchControlName);
            search = GUI.TextField(searchRect, search, BetterProjectGui.Search);
            GUI.DrawTexture(new Rect(searchRect.x + 8f, searchRect.y + 6f, 16f, 16f), BetterProjectGui.Icon("Search Icon"), ScaleMode.ScaleToFit);
            if (!string.IsNullOrEmpty(search) && BetterProjectGui.IconButton(
                    new Rect(searchRect.xMax - 22f, searchRect.y + 4f, 20f, 20f), new GUIContent("×", "Clear")))
            {
                search = string.Empty;
                GUI.FocusControl(SearchControlName);
            }

            float right = searchRect.xMax + 7f;
            if (BetterProjectGui.IconButton(new Rect(right, rect.y + 8f, 25f, 25f), new GUIContent("≡", "List"), view == BetterProjectView.List)) view = BetterProjectView.List;
            right += 27f;
            if (BetterProjectGui.IconButton(new Rect(right, rect.y + 8f, 25f, 25f), new GUIContent("▦", "Grid"), view == BetterProjectView.Grid)) view = BetterProjectView.Grid;
            right += 27f;
            if (BetterProjectGui.IconButton(new Rect(right, rect.y + 8f, 25f, 25f), new GUIContent("☷", "Details"), view == BetterProjectView.Details)) view = BetterProjectView.Details;
            if (!compact)
            {
                right += 29f;
                if (BetterProjectGui.IconButton(new Rect(right, rect.y + 8f, 25f, 25f), new GUIContent("◫", "Split panes"), dualPane)) dualPane = !dualPane;
                right += 29f;
                if (BetterProjectGui.IconButton(new Rect(right, rect.y + 8f, 25f, 25f), new GUIContent("◧", "Preview"), BetterProjectSettings.ShowPreview))
                {
                    BetterProjectSettings.ShowPreview = !BetterProjectSettings.ShowPreview;
                }
            }
            right += 29f;
            if (BetterProjectGui.IconButton(new Rect(right, rect.y + 8f, 25f, 25f), new GUIContent("•••", "More")))
            {
                ShowWindowMenu();
            }
        }

        private void DrawSurfaceSegment(ref float x, Rect toolbarRect, BetterProjectSurface target, string label, string tooltip)
        {
            float width = label == "LIBRARY" ? 72f : 68f;
            Rect rect = new Rect(x, toolbarRect.y + 7f, width, 28f);
            if (BetterProjectGui.SegmentButton(rect, label, surface == target, tooltip))
            {
                surface = target;
                if (surface == BetterProjectSurface.Impact && !BetterProjectIndex.IsReferenceIndexReady && !BetterProjectIndex.IsReferenceIndexing)
                {
                    BetterProjectIndex.StartReferenceIndex();
                }
            }
            x += width + 3f;
        }

        private void DrawBreadcrumbs(Rect rect)
        {
            bool primary = !dualPane || !secondaryPaneActive;
            string path = primary ? currentFolder : secondaryFolder;
            string[] segments = path.Split('/');
            float x = rect.x;
            string accumulated = string.Empty;
            for (int index = 0; index < segments.Length; index++)
            {
                accumulated = index == 0 ? segments[index] : accumulated + "/" + segments[index];
                string destination = accumulated;
                float width = Mathf.Min(96f, BetterProjectGui.Muted.CalcSize(new GUIContent(segments[index])).x + 16f);
                if (x + width > rect.xMax)
                {
                    GUI.Label(new Rect(x, rect.y, 18f, rect.height), "…", BetterProjectGui.Muted);
                    break;
                }
                if (GUI.Button(new Rect(x, rect.y, width, rect.height), segments[index], BetterProjectGui.Muted))
                {
                    NavigatePane(destination, primary);
                }
                x += width;
                if (index < segments.Length - 1)
                {
                    GUI.Label(new Rect(x - 3f, rect.y, 10f, rect.height), "/", BetterProjectGui.Muted);
                    x += 7f;
                }
            }
        }

        private void DrawChips(Rect rect, IReadOnlyList<string> tokens)
        {
            EditorGUI.DrawRect(rect, BetterProjectGui.Inset);
            float x = 8f;
            foreach (string token in tokens)
            {
                float width = Mathf.Min(150f, BetterProjectGui.Badge.CalcSize(new GUIContent(token)).x + 22f);
                Rect chip = new Rect(x, rect.y + 3f, width, 19f);
                if (GUI.Button(chip, token + "  ×", BetterProjectGui.Badge))
                {
                    List<string> remaining = tokens.ToList();
                    remaining.Remove(token);
                    search = string.Join(" ", remaining);
                    GUI.FocusControl(SearchControlName);
                    break;
                }
                x += width + 4f;
                if (x > rect.xMax - 80f) break;
            }
        }

        private void DrawBody(Rect body)
        {
            bool showRail = BetterProjectSettings.ShowFolderRail;
            bool showPreview = BetterProjectSettings.ShowPreview && body.width > 850f;
            float railHandleWidth = showRail ? ResizeHandleWidth : 0f;
            float previewHandleWidth = showPreview ? ResizeHandleWidth : 0f;

            float railMaximum = Mathf.Max(
                MinimumRailWidth,
                Mathf.Min(
                    MaximumRailWidth,
                    body.width - MinimumAssetPaneWidth - railHandleWidth - previewHandleWidth -
                    (showPreview ? MinimumPreviewWidth : 0f)));
            if (showRail)
            {
                folderRailWidth = Mathf.Clamp(folderRailWidth, MinimumRailWidth, railMaximum);
            }
            float railWidth = showRail ? folderRailWidth : 0f;
            Rect railDivider = new Rect(body.x + railWidth, body.y, railHandleWidth, body.height);
            if (showRail)
            {
                HandleResizableDivider(
                    railDivider,
                    "BetterProjectRailDivider".GetHashCode(),
                    ref folderRailWidth,
                    MinimumRailWidth,
                    railMaximum,
                    DefaultRailWidth,
                    1f,
                    "Resize folder sidebar · Double-click to reset");
                railWidth = Mathf.Clamp(folderRailWidth, MinimumRailWidth, railMaximum);
                railDivider.x = body.x + railWidth;
            }

            float previewMaximum = Mathf.Max(
                MinimumPreviewWidth,
                Mathf.Min(
                    MaximumPreviewWidth,
                    body.width - railWidth - railHandleWidth - previewHandleWidth - MinimumAssetPaneWidth));
            if (showPreview)
            {
                previewPanelWidth = Mathf.Clamp(previewPanelWidth, MinimumPreviewWidth, previewMaximum);
            }
            float previewWidth = showPreview ? previewPanelWidth : 0f;
            Rect previewDivider = new Rect(
                body.xMax - previewWidth - previewHandleWidth,
                body.y,
                previewHandleWidth,
                body.height);
            if (showPreview)
            {
                HandleResizableDivider(
                    previewDivider,
                    "BetterProjectPreviewDivider".GetHashCode(),
                    ref previewPanelWidth,
                    MinimumPreviewWidth,
                    previewMaximum,
                    DefaultPreviewWidth,
                    -1f,
                    "Resize preview · Double-click to reset");
                previewWidth = Mathf.Clamp(previewPanelWidth, MinimumPreviewWidth, previewMaximum);
                previewDivider.x = body.xMax - previewWidth - previewHandleWidth;
            }

            Rect rail = new Rect(body.x, body.y, railWidth, body.height);
            Rect preview = new Rect(body.xMax - previewWidth, body.y, previewWidth, body.height);
            Rect content = new Rect(
                rail.xMax + railHandleWidth,
                body.y,
                Mathf.Max(0f, body.width - railWidth - previewWidth - railHandleWidth - previewHandleWidth),
                body.height);

            if (railWidth > 0f)
            {
                DrawRail(rail);
            }
            if (surface == BetterProjectSurface.Impact)
            {
                DrawImpact(content);
            }
            else if (dualPane && content.width >= MinimumAssetPaneWidth * 2f + ResizeHandleWidth)
            {
                float paneSpace = content.width - ResizeHandleWidth;
                float primaryWidth = Mathf.Clamp(
                    paneSpace * Mathf.Clamp01(splitRatio),
                    MinimumAssetPaneWidth,
                    paneSpace - MinimumAssetPaneWidth);
                Rect splitDivider = new Rect(
                    content.x + primaryWidth,
                    content.y,
                    ResizeHandleWidth,
                    content.height);
                HandleResizableDivider(
                    splitDivider,
                    "BetterProjectSplitDivider".GetHashCode(),
                    ref primaryWidth,
                    MinimumAssetPaneWidth,
                    paneSpace - MinimumAssetPaneWidth,
                    paneSpace * DefaultSplitRatio,
                    1f,
                    "Resize split panes · Double-click to balance");
                splitRatio = paneSpace <= 0f
                    ? DefaultSplitRatio
                    : Mathf.Clamp(primaryWidth / paneSpace, 0f, 1f);
                splitDivider.x = content.x + primaryWidth;
                DrawAssetPane(
                    new Rect(content.x, content.y, primaryWidth, content.height),
                    currentFolder,
                    ref primaryScroll,
                    true);
                DrawAssetPane(
                    new Rect(splitDivider.xMax, content.y, paneSpace - primaryWidth, content.height),
                    secondaryFolder,
                    ref secondaryScroll,
                    false);
            }
            else
            {
                DrawAssetPane(content, currentFolder, ref primaryScroll, true);
            }
            if (previewWidth > 0f)
            {
                DrawPreview(preview);
            }
        }

        private void HandleResizableDivider(
            Rect rect,
            int controlHint,
            ref float value,
            float minimum,
            float maximum,
            float defaultValue,
            float direction,
            string tooltip)
        {
            int controlId = GUIUtility.GetControlID(controlHint, FocusType.Passive, rect);
            Event evt = Event.current;
            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (evt.button != 0 || !rect.Contains(evt.mousePosition))
                    {
                        break;
                    }
                    if (evt.clickCount >= 2)
                    {
                        value = Mathf.Clamp(defaultValue, minimum, maximum);
                        GUIUtility.hotControl = 0;
                    }
                    else
                    {
                        GUIUtility.hotControl = controlId;
                    }
                    evt.Use();
                    Repaint();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId)
                    {
                        break;
                    }
                    value = Mathf.Clamp(value + evt.delta.x * direction, minimum, maximum);
                    evt.Use();
                    Repaint();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId)
                    {
                        break;
                    }
                    GUIUtility.hotControl = 0;
                    evt.Use();
                    Repaint();
                    break;
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            bool highlighted = GUIUtility.hotControl == controlId || rect.Contains(evt.mousePosition);
            Color lineColor = highlighted ? BetterProjectGui.Accent : BetterProjectGui.Border;
            EditorGUI.DrawRect(
                new Rect(Mathf.Floor(rect.center.x), rect.y, 1f, rect.height),
                lineColor);
            GUI.Label(rect, new GUIContent(string.Empty, tooltip));
        }

        private void DrawRail(Rect rect)
        {
            BetterProjectGui.DrawPanel(rect, BetterProjectGui.Inset, false);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), BetterProjectGui.Border);
            if (surface == BetterProjectSurface.Browse)
            {
                if (folderTree == null) RebuildFolderTree();
                folderTree.OnGUI(new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f));
                return;
            }
            DrawLibraryRail(rect);
        }

        private void DrawLibraryRail(Rect rect)
        {
            Rect view = new Rect(rect.x + 7f, rect.y + 7f, rect.width - 14f, rect.height - 14f);
            Rect content = new Rect(0f, 0f, view.width - 14f, 420f +
                (BetterProjectSettings.Collections.Count + BetterProjectSettings.SavedSearches.Count) * 27f);
            libraryRailScroll = GUI.BeginScrollView(view, libraryRailScroll, content);
            float y = 0f;
            DrawLibrarySource(ref y, content.width, BetterProjectLibrarySource.All, "ALL", "All assets");
            DrawLibrarySource(ref y, content.width, BetterProjectLibrarySource.Favorites, "★ FAVORITES", "Pinned assets");
            DrawLibrarySource(ref y, content.width, BetterProjectLibrarySource.Recent, "RECENT", "Recently selected");
            if (BetterProjectSettings.SavedSearches.Count > 0)
            {
                y += 8f;
                GUI.Label(new Rect(6f, y, content.width - 12f, 20f), "SEARCHES", BetterProjectGui.Tiny);
                y += 22f;
                foreach (BetterProjectSavedSearch saved in BetterProjectSettings.SavedSearches.ToArray())
                {
                    Rect savedRow = new Rect(0f, y, content.width, 24f);
                    if (GUI.Button(savedRow, new GUIContent(saved.Name, saved.Query), BetterProjectGui.Row))
                    {
                        search = saved.Query;
                        librarySource = BetterProjectLibrarySource.All;
                    }
                    if (Event.current.type == EventType.ContextClick && savedRow.Contains(Event.current.mousePosition))
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Delete"), false, () =>
                        {
                            BetterProjectSettings.RecordUndo("Delete Saved Search");
                            BetterProjectSettings.SavedSearches.Remove(saved);
                            BetterProjectSettings.SaveNow();
                        });
                        menu.ShowAsContext();
                        Event.current.Use();
                    }
                    y += 27f;
                }
            }
            y += 8f;
            GUI.Label(new Rect(6f, y, content.width - 12f, 20f), "HEALTH", BetterProjectGui.Tiny);
            y += 22f;
            DrawLibrarySource(ref y, content.width, BetterProjectLibrarySource.Issues, "! ISSUES", "Diagnostics");
            DrawLibrarySource(ref y, content.width, BetterProjectLibrarySource.Duplicates, "DUPLICATES", "Matching names");
            DrawLibrarySource(ref y, content.width, BetterProjectLibrarySource.Large, "LARGE", "Oversized files");
            DrawLibrarySource(ref y, content.width, BetterProjectLibrarySource.Unused, "UNUSED?", "No indexed references");
            y += 8f;
            GUI.Label(new Rect(6f, y, content.width - 72f, 20f), "COLLECTIONS", BetterProjectGui.Tiny);
            if (GUI.Button(new Rect(content.width - 58f, y, 52f, 19f), "+ NEW", BetterProjectGui.Badge))
            {
                CreateCollection(false);
            }
            y += 23f;
            foreach (BetterProjectCollection collection in BetterProjectSettings.Collections.ToArray())
            {
                bool active = librarySource == BetterProjectLibrarySource.Collection && activeCollectionId == collection.Id;
                Rect row = new Rect(0f, y, content.width, 24f);
                EditorGUI.DrawRect(new Rect(row.x, row.y + 3f, 3f, row.height - 6f), collection.Color);
                if (GUI.Toggle(row, active, new GUIContent(collection.Name, collection.Kind == BetterProjectCollectionKind.Smart ? collection.Query : "Manual"),
                        active ? BetterProjectGui.RowSelected : BetterProjectGui.Row))
                {
                    librarySource = BetterProjectLibrarySource.Collection;
                    activeCollectionId = collection.Id;
                }
                if (Event.current.type == EventType.ContextClick && row.Contains(Event.current.mousePosition))
                {
                    ShowCollectionMenu(collection);
                    Event.current.Use();
                }
                y += 27f;
            }
            GUI.EndScrollView();
            HandleCollectionDrop(rect);
        }

        private void DrawLibrarySource(ref float y, float width, BetterProjectLibrarySource target, string label, string tooltip)
        {
            Rect row = new Rect(0f, y, width, 24f);
            if (GUI.Toggle(row, librarySource == target, new GUIContent(label, tooltip),
                    librarySource == target ? BetterProjectGui.RowSelected : BetterProjectGui.Row))
            {
                librarySource = target;
                activeCollectionId = string.Empty;
            }
            y += 27f;
        }

        private void DrawAssetPane(Rect pane, string folder, ref Vector2 scroll, bool primary)
        {
            Event paneEvent = Event.current;
            if ((paneEvent.type == EventType.MouseDown || paneEvent.type == EventType.ContextClick) &&
                pane.Contains(paneEvent.mousePosition))
            {
                SetActivePane(primary);
            }

            IReadOnlyList<BetterProjectAssetRecord> visible = GetVisibleAssets(folder);
            if (!dualPane || (primary && !secondaryPaneActive) || (!primary && secondaryPaneActive))
            {
                lastVisible = visible;
            }
            BetterProjectGui.DrawPanel(pane, BetterProjectGui.Canvas, false);

            BetterProjectAssetRecord[] pinnedFolders = BetterProjectUserSettings.FavoriteGuids
                .Select(BetterProjectIndex.GetByGuid)
                .Where(record => record != null && record.IsFolder)
                .Take(5)
                .ToArray();
            if (pinnedFolders.Length > 0)
            {
                DrawPinnedFolders(new Rect(pane.x, pane.y, pane.width, 25f), pinnedFolders, primary);
                pane.y += 25f;
                pane.height -= 25f;
            }

            if (dualPane)
            {
                Rect paneHeader = new Rect(pane.x, pane.y, pane.width, 26f);
                bool activePane = primary ? !secondaryPaneActive : secondaryPaneActive;
                EditorGUI.DrawRect(paneHeader, activePane ? BetterProjectGui.Raised : BetterProjectGui.Panel);
                EditorGUI.DrawRect(
                    new Rect(paneHeader.x, paneHeader.y, 3f, paneHeader.height),
                    activePane ? BetterProjectGui.Accent : BetterProjectGui.Border);
                string paneName = primary ? "LEFT" : "RIGHT";
                GUI.Label(
                    new Rect(paneHeader.x + 9f, paneHeader.y, paneHeader.width - 18f, paneHeader.height),
                    paneName + "  /  " + folder,
                    BetterProjectGui.Tiny);
                pane.y += 26f;
                pane.height -= 26f;
            }

            if (view == BetterProjectView.Grid)
            {
                DrawGrid(pane, visible, ref scroll, primary);
            }
            else
            {
                DrawList(pane, visible, ref scroll, view == BetterProjectView.Details, primary);
            }

            if (visible.Count == 0)
            {
                DrawEmpty(pane, folder);
            }
            HandlePaneDrop(pane, folder);
            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                pane.Contains(Event.current.mousePosition))
            {
                ClearAssetSelection();
                Event.current.Use();
            }
            else if (Event.current.type == EventType.ContextClick && pane.Contains(Event.current.mousePosition))
            {
                ClearAssetSelection(false);
                ShowBlankMenu(folder, primary);
                Event.current.Use();
            }
        }

        private void DrawGrid(Rect pane, IReadOnlyList<BetterProjectAssetRecord> visible, ref Vector2 scroll, bool primary)
        {
            CalculateGridLayout(pane.width, tileSize, out int columns, out float actualWidth);
            float cardHeight = actualWidth + 42f;
            int rows = Mathf.CeilToInt(visible.Count / (float)columns);
            Rect content = new Rect(
                0f,
                0f,
                Mathf.Max(1f, pane.width - GridScrollBarAllowance),
                Mathf.Max(pane.height, GridGap + rows * (cardHeight + GridGap)));
            scroll = GUI.BeginScrollView(pane, scroll, content);
            float visibleTop = Mathf.Max(0f, scroll.y - GridGap);
            float visibleBottom = scroll.y + pane.height + GridGap;
            for (int index = 0; index < visible.Count; index++)
            {
                int column = index % columns;
                int row = index / columns;
                Rect card = new Rect(
                    GridGap + column * (actualWidth + GridGap),
                    GridGap + row * (cardHeight + GridGap),
                    actualWidth,
                    cardHeight);
                if (card.yMax < visibleTop || card.y > visibleBottom)
                {
                    continue;
                }
                DrawAssetCard(card, visible[index], index, primary);
            }
            GUI.EndScrollView();
        }

        internal static void CalculateGridLayout(
            float paneWidth,
            float preferredCardWidth,
            out int columns,
            out float cardWidth)
        {
            float availableWidth = Mathf.Max(1f, paneWidth - GridScrollBarAllowance);
            float preferredWidth = Mathf.Clamp(preferredCardWidth, 76f, 180f);
            cardWidth = Mathf.Min(preferredWidth, Mathf.Max(1f, availableWidth - GridGap * 2f));
            columns = Mathf.Max(
                1,
                Mathf.FloorToInt((availableWidth - GridGap) / (cardWidth + GridGap)));
        }

        private void DrawAssetCard(Rect rect, BetterProjectAssetRecord record, int index, bool primary)
        {
            bool selected = selectedGuids.Contains(record.Guid);
            bool hover = rect.Contains(Event.current.mousePosition);
            bool dropTarget = record.IsFolder && dragHoverFolderGuid == record.Guid;
            Color background = dropTarget
                ? BetterProjectGui.AccentSoft
                : selected
                    ? BetterProjectGui.Selected
                    : hover
                        ? BetterProjectGui.Hover
                        : BetterProjectGui.Panel;
            BetterProjectGui.DrawPanel(rect, background);
            if (dropTarget) DrawDropTargetOutline(rect);
            BetterProjectStyle style = BetterProjectIndex.GetStyle(record);
            Color rail = style.IsValid ? style.Color : BetterProjectGui.Border;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), rail);

            Rect preview = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.width - 16f);
            Texture image = style.IsValid && !string.IsNullOrEmpty(style.IconName)
                ? EditorGUIUtility.IconContent(style.IconName).image
                : record.IsFolder
                    ? EditorGUIUtility.FindTexture("Folder Icon")
                    : AssetPreview.GetAssetPreview(BetterProjectOperations.Load(record)) ?? AssetPreview.GetMiniThumbnail(BetterProjectOperations.Load(record));
            if (image != null)
            {
                GUI.DrawTexture(preview, image, ScaleMode.ScaleToFit, true);
            }
            Rect name = new Rect(rect.x + 8f, preview.yMax + 4f, rect.width - 16f, 18f);
            GUI.Label(name, record.Name, BetterProjectGui.CardTitle);
            string meta = style.IsValid && !string.IsNullOrEmpty(style.Badge) ? style.Badge : record.TypeName.ToUpperInvariant();
            GUI.Label(new Rect(rect.x + 8f, name.yMax, rect.width - 34f, 16f), meta, BetterProjectGui.Tiny);

            BetterConsoleDiagnosticSummary consoleSummary = BetterConsoleDiagnosticBridge.GetSummaryForAssetPath(record.Path);
            BetterProjectDiagnosticFlags flags = BetterProjectIndex.GetDiagnostics(record);
            if (flags != BetterProjectDiagnosticFlags.None)
            {
                float diagnosticOffset = consoleSummary.HasSignals ? 55f : 28f;
                GUI.Label(new Rect(rect.xMax - diagnosticOffset, name.yMax, 20f, 14f), "!", BetterProjectGui.Badge);
            }
            if (consoleSummary.HasSignals)
            {
                DrawConsoleDiagnosticBadge(
                    new Rect(rect.xMax - 34f, name.yMax, 26f, 15f),
                    consoleSummary,
                    () => BetterConsoleDiagnosticBridge.OpenForAssetPaths(new[] { record.Path }));
            }
            if (hover && BetterProjectGui.IconButton(new Rect(rect.xMax - 25f, rect.y + 5f, 20f, 20f),
                    new GUIContent(BetterProjectUserSettings.IsFavorite(record.Guid) ? "★" : "☆", "Favorite")))
            {
                BetterProjectUserSettings.ToggleFavorite(record.Guid);
            }
            HandleFolderDrop(rect, record);
            HandleAssetPointer(rect, record, index, primary);
        }

        private void DrawList(Rect pane, IReadOnlyList<BetterProjectAssetRecord> visible, ref Vector2 scroll, bool details, bool primary)
        {
            float header = details ? 24f : 0f;
            if (details)
            {
                DrawDetailsHeader(new Rect(pane.x, pane.y, pane.width, header));
            }
            Rect viewport = new Rect(pane.x, pane.y + header, pane.width, pane.height - header);
            int subRows = visible.Count(record => expandedAssetGuids.Contains(record.Guid));
            Rect content = new Rect(0f, 0f, viewport.width - 14f, Mathf.Max(viewport.height, (visible.Count + subRows * 2) * ListRowHeight));
            scroll = GUI.BeginScrollView(viewport, scroll, content);
            float visibleTop = Mathf.Max(0f, scroll.y - ListRowHeight);
            float visibleBottom = scroll.y + viewport.height + ListRowHeight;
            float y = 0f;
            for (int index = 0; index < visible.Count; index++)
            {
                BetterProjectAssetRecord record = visible[index];
                Rect row = new Rect(0f, y, content.width, ListRowHeight);
                if (row.yMax >= visibleTop && row.y <= visibleBottom)
                {
                    DrawAssetRow(row, record, index, details, primary);
                }
                y += ListRowHeight;
                if (expandedAssetGuids.Contains(record.Guid) && !record.IsFolder)
                {
                    float estimatedBottom = y + ListRowHeight * 2f;
                    y = estimatedBottom >= visibleTop && y <= visibleBottom
                        ? DrawSubAssets(record, y, content.width)
                        : estimatedBottom;
                }
            }
            GUI.EndScrollView();
        }

        private void DrawDetailsHeader(Rect rect)
        {
            EditorGUI.DrawRect(rect, BetterProjectGui.Inset);
            GUI.Label(new Rect(rect.x + 40f, rect.y, rect.width * 0.48f, rect.height), "NAME", BetterProjectGui.Tiny);
            GUI.Label(new Rect(rect.x + rect.width * 0.56f, rect.y, 90f, rect.height), "TYPE", BetterProjectGui.Tiny);
            GUI.Label(new Rect(rect.xMax - 150f, rect.y, 70f, rect.height), "SIZE", BetterProjectGui.Tiny);
            GUI.Label(new Rect(rect.xMax - 78f, rect.y, 70f, rect.height), "MODIFIED", BetterProjectGui.Tiny);
        }

        private void DrawAssetRow(Rect rect, BetterProjectAssetRecord record, int index, bool details, bool primary)
        {
            bool selected = selectedGuids.Contains(record.Guid);
            bool hover = rect.Contains(Event.current.mousePosition);
            bool dropTarget = record.IsFolder && dragHoverFolderGuid == record.Guid;
            if (dropTarget || selected || hover)
            {
                EditorGUI.DrawRect(
                    rect,
                    dropTarget ? BetterProjectGui.AccentSoft : selected ? BetterProjectGui.Selected : BetterProjectGui.Hover);
            }
            if (dropTarget) DrawDropTargetOutline(rect);
            BetterProjectStyle style = BetterProjectIndex.GetStyle(record);
            BetterConsoleDiagnosticSummary consoleSummary = BetterConsoleDiagnosticBridge.GetSummaryForAssetPath(record.Path);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3f, 3f, rect.height - 6f), style.IsValid ? style.Color : BetterProjectGui.Border);

            Rect fold = new Rect(rect.x + 6f, rect.y + 4f, 16f, 18f);
            if (!record.IsFolder && GUI.Button(fold, expandedAssetGuids.Contains(record.Guid) ? "▾" : "▸", BetterProjectGui.Tiny))
            {
                if (!expandedAssetGuids.Remove(record.Guid)) expandedAssetGuids.Add(record.Guid);
            }
            Texture icon = style.IsValid && !string.IsNullOrEmpty(style.IconName)
                ? EditorGUIUtility.IconContent(style.IconName).image
                : record.IsFolder
                    ? EditorGUIUtility.FindTexture("Folder Icon")
                    : AssetPreview.GetMiniThumbnail(BetterProjectOperations.Load(record));
            if (icon != null) GUI.DrawTexture(new Rect(rect.x + 23f, rect.y + 4f, 18f, 18f), icon, ScaleMode.ScaleToFit);

            float nameWidth = details ? rect.width * 0.48f - 12f : rect.width - 160f;
            Rect nameRect = new Rect(rect.x + 46f, rect.y + 2f, Mathf.Max(60f, nameWidth), rect.height - 4f);
            if (renamingGuid == record.Guid)
            {
                GUI.SetNextControlName("BetterProjectRename");
                renameValue = EditorGUI.TextField(nameRect, renameValue);
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                {
                    CommitRename(record);
                    Event.current.Use();
                }
            }
            else
            {
                GUI.Label(nameRect, record.Name, BetterProjectGui.Row);
            }

            if (details)
            {
                GUI.Label(new Rect(rect.x + rect.width * 0.56f, rect.y, 90f, rect.height), record.TypeName, BetterProjectGui.Tiny);
                GUI.Label(new Rect(rect.xMax - 150f, rect.y, 70f, rect.height), record.IsFolder ? "—" : BetterProjectGui.FormatBytes(record.FileSize), BetterProjectGui.Tiny);
                GUI.Label(new Rect(rect.xMax - 78f, rect.y, 70f, rect.height), record.ModifiedUtc == default ? "—" : record.ModifiedUtc.ToLocalTime().ToString("MM/dd/yy"), BetterProjectGui.Tiny);
                if (consoleSummary.HasSignals)
                {
                    DrawConsoleDiagnosticBadge(
                        new Rect(rect.x + rect.width * 0.56f - 34f, rect.y + 5f, 28f, 16f),
                        consoleSummary,
                        () => BetterConsoleDiagnosticBridge.OpenForAssetPaths(new[] { record.Path }));
                }
            }
            else
            {
                string badge = style.IsValid && !string.IsNullOrEmpty(style.Badge) ? style.Badge : record.TypeName;
                GUI.Label(new Rect(rect.xMax - 108f, rect.y + 4f, 62f, 18f), badge, BetterProjectGui.Badge);
                BetterProjectDiagnosticFlags flags = BetterProjectIndex.GetDiagnostics(record);
                if (flags != BetterProjectDiagnosticFlags.None)
                {
                    float diagnosticOffset = consoleSummary.HasSignals ? 72f : 42f;
                    GUI.Label(new Rect(rect.xMax - diagnosticOffset, rect.y + 4f, 34f, 18f), "!", BetterProjectGui.Badge);
                }
                if (consoleSummary.HasSignals)
                {
                    DrawConsoleDiagnosticBadge(
                        new Rect(rect.xMax - 38f, rect.y + 5f, 30f, 16f),
                        consoleSummary,
                        () => BetterConsoleDiagnosticBridge.OpenForAssetPaths(new[] { record.Path }));
                }
            }
            HandleFolderDrop(rect, record);
            HandleAssetPointer(rect, record, index, primary);
        }

        private float DrawSubAssets(BetterProjectAssetRecord record, float y, float width)
        {
            foreach (UnityEngine.Object subAsset in AssetDatabase.LoadAllAssetRepresentationsAtPath(record.Path))
            {
                if (subAsset == null) continue;
                Rect row = new Rect(0f, y, width, ListRowHeight);
                EditorGUI.DrawRect(new Rect(row.x + 24f, row.y, 1f, row.height), BetterProjectGui.Border);
                Texture icon = AssetPreview.GetMiniThumbnail(subAsset);
                if (icon != null) GUI.DrawTexture(new Rect(row.x + 34f, row.y + 4f, 18f, 18f), icon, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(row.x + 58f, row.y, row.width - 66f, row.height), subAsset.name, BetterProjectGui.Muted);
                if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
                {
                    Selection.activeObject = subAsset;
                    if (Event.current.clickCount == 2) AssetDatabase.OpenAsset(subAsset);
                    Event.current.Use();
                }
                y += ListRowHeight;
            }
            return y;
        }

        private void HandleAssetPointer(Rect rect, BetterProjectAssetRecord record, int index, bool primary)
        {
            Event evt = Event.current;
            if (!rect.Contains(evt.mousePosition)) return;
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                SelectRecord(record, index, EditorGUI.actionKey, evt.shift);
                if (evt.clickCount == 2)
                {
                    if (record.IsFolder) NavigatePane(record.Path, primary);
                    else BetterProjectOperations.Open(record);
                }
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && evt.button == 0 && selectedGuids.Contains(record.Guid))
            {
                BetterProjectAssetRecord[] selected = SelectedRecords().ToArray();
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = selected.Select(BetterProjectOperations.Load).Where(asset => asset != null).ToArray();
                DragAndDrop.paths = selected.Select(asset => asset.Path).ToArray();
                DragAndDrop.StartDrag(selected.Length == 1 ? selected[0].Name : selected.Length + " assets");
                evt.Use();
            }
            else if (evt.type == EventType.ContextClick)
            {
                if (!selectedGuids.Contains(record.Guid)) SelectRecord(record, index, false, false);
                ShowAssetMenu(record, primary);
                evt.Use();
            }
        }

        private void DrawPreview(Rect rect)
        {
            BetterProjectGui.DrawPanel(rect, BetterProjectGui.Inset, false);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), BetterProjectGui.Border);
            BetterProjectAssetRecord[] selected = SelectedRecords().ToArray();
            if (selected.Length == 0)
            {
                GUI.Label(new Rect(rect.x + 18f, rect.center.y - 12f, rect.width - 36f, 24f), "SELECT AN ASSET", BetterProjectGui.Muted);
                return;
            }
            if (selected.Length == 2)
            {
                DrawComparePreview(rect, selected[0], selected[1]);
                return;
            }
            BetterProjectAssetRecord record = selected[selected.Length - 1];
            UnityEngine.Object target = BetterProjectOperations.Load(record);
            EnsurePreviewEditor(target);
            Rect inner = new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f);
            previewScroll = GUI.BeginScrollView(inner, previewScroll, new Rect(0f, 0f, inner.width - 14f, 620f));
            float y = 0f;
            GUI.Label(new Rect(0f, y, inner.width - 20f, 22f), record.Name, BetterProjectGui.CardTitle);
            y += 24f;
            GUI.Label(new Rect(0f, y, inner.width - 20f, 18f), record.Path, BetterProjectGui.Tiny);
            y += 24f;
            Rect preview = new Rect(0f, y, inner.width - 18f, Mathf.Min(230f, inner.width - 18f));
            BetterProjectGui.DrawPanel(preview, BetterProjectGui.Canvas);
            if (previewEditor != null && previewEditor.HasPreviewGUI())
            {
                previewEditor.OnInteractivePreviewGUI(new Rect(preview.x + 4f, preview.y + 4f, preview.width - 8f, preview.height - 8f), GUIStyle.none);
            }
            else
            {
                Texture image = target == null ? null : AssetPreview.GetAssetPreview(target) ?? AssetPreview.GetMiniThumbnail(target);
                if (image != null) GUI.DrawTexture(new Rect(preview.x + 8f, preview.y + 8f, preview.width - 16f, preview.height - 16f), image, ScaleMode.ScaleToFit, true);
            }
            y = preview.yMax + 10f;
            DrawPreviewActions(ref y, inner.width - 18f, record, target);
            DrawMetadata(ref y, inner.width - 18f, record, target);
            GUI.EndScrollView();
        }

        private void DrawComparePreview(Rect rect, BetterProjectAssetRecord first, BetterProjectAssetRecord second)
        {
            Rect inner = new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f);
            float half = (inner.width - 8f) * 0.5f;
            DrawCompareCard(new Rect(inner.x, inner.y, half, inner.height), first);
            DrawCompareCard(new Rect(inner.x + half + 8f, inner.y, half, inner.height), second);
        }

        private void DrawCompareCard(Rect rect, BetterProjectAssetRecord record)
        {
            BetterProjectGui.DrawPanel(rect, BetterProjectGui.Panel);
            GUI.Label(new Rect(rect.x + 7f, rect.y + 7f, rect.width - 14f, 34f), record.Name, BetterProjectGui.CardTitle);
            UnityEngine.Object target = BetterProjectOperations.Load(record);
            Texture image = target == null ? null : AssetPreview.GetAssetPreview(target) ?? AssetPreview.GetMiniThumbnail(target);
            if (image != null) GUI.DrawTexture(new Rect(rect.x + 7f, rect.y + 42f, rect.width - 14f, Mathf.Min(rect.width - 14f, 160f)), image, ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(rect.x + 7f, rect.y + 210f, rect.width - 14f, 18f), record.TypeName, BetterProjectGui.Muted);
            GUI.Label(new Rect(rect.x + 7f, rect.y + 230f, rect.width - 14f, 18f), BetterProjectGui.FormatBytes(record.FileSize), BetterProjectGui.Muted);
        }

        private void DrawPreviewActions(ref float y, float width, BetterProjectAssetRecord record, UnityEngine.Object target)
        {
            float buttonWidth = (width - 9f) / 4f;
            if (GUI.Button(new Rect(0f, y, buttonWidth, 24f), "OPEN", BetterProjectGui.SegmentActive)) BetterProjectOperations.Open(record);
            if (GUI.Button(new Rect(buttonWidth + 3f, y, buttonWidth, 24f), "PING", BetterProjectGui.Segment)) BetterProjectOperations.Ping(record);
            if (GUI.Button(new Rect((buttonWidth + 3f) * 2f, y, buttonWidth, 24f), BetterProjectUserSettings.IsFavorite(record.Guid) ? "★" : "☆", BetterProjectGui.Segment)) BetterProjectUserSettings.ToggleFavorite(record.Guid);
            if (GUI.Button(new Rect((buttonWidth + 3f) * 3f, y, buttonWidth, 24f), "REF", BetterProjectGui.Segment)) BetterProjectReferenceReplaceWindow.Open(target);
            y += 34f;
        }

        private void DrawMetadata(ref float y, float width, BetterProjectAssetRecord record, UnityEngine.Object target)
        {
            BetterProjectDiagnosticFlags flags = BetterProjectIndex.GetDiagnostics(record);
            DrawMetaLine(ref y, width, "TYPE", record.TypeName);
            DrawMetaLine(ref y, width, "SIZE", record.IsFolder ? "—" : BetterProjectGui.FormatBytes(record.FileSize));
            DrawMetaLine(ref y, width, "GUID", record.Guid);
            DrawMetaLine(ref y, width, "LABELS", string.Join(", ", BetterProjectIndex.GetLabels(record)));
            DrawMetaLine(ref y, width, "USES", record.DirectDependencyCount.ToString());
            DrawMetaLine(ref y, width, "USED BY", record.ReferenceCount.ToString());
            DrawMetaLine(ref y, width, "BUILD", BetterProjectIndex.IsIncludedByBuildHeuristic(record) ? "LIKELY" : "—");
            DrawMetaLine(ref y, width, "ADDRESS", BetterProjectIntegrations.GetAddressableGroup(record.Guid));
            DrawMetaLine(ref y, width, "VCS", BetterProjectIntegrations.GetVersionControlState(record.Path));
            if (flags != BetterProjectDiagnosticFlags.None) DrawMetaLine(ref y, width, "CHECK", flags.ToString());
            if (target is Texture2D texture) DrawMetaLine(ref y, width, "IMAGE", texture.width + " × " + texture.height);
            if (target is AudioClip audio) DrawMetaLine(ref y, width, "AUDIO", audio.length.ToString("0.00") + "s · " + audio.channels + "ch · " + audio.frequency + "Hz");
            if (target is AnimationClip clip) DrawMetaLine(ref y, width, "CLIP", clip.length.ToString("0.00") + "s · " + clip.frameRate.ToString("0.#") + "fps");
            if (target is GameObject prefab)
            {
                MeshFilter[] meshes = prefab.GetComponentsInChildren<MeshFilter>(true);
                int vertices = meshes.Where(filter => filter.sharedMesh != null).Sum(filter => filter.sharedMesh.vertexCount);
                DrawMetaLine(ref y, width, "PREFAB", prefab.GetComponentsInChildren<Transform>(true).Length + " objects · " + vertices.ToString("N0") + " verts");
            }
            if (target is Sprite sprite) DrawMetaLine(ref y, width, "SPRITE", sprite.rect.width + " × " + sprite.rect.height + " · " + sprite.pixelsPerUnit + " PPU");
            if (target is Shader shader) DrawMetaLine(ref y, width, "SHADER", shader.GetPropertyCount() + " properties");
            if (target is MonoScript script)
            {
                Type scriptType = script.GetClass();
                DrawMetaLine(ref y, width, "SCRIPT", scriptType == null ? "No compiled class" : scriptType.FullName);
            }
            AssetImporter importer = AssetImporter.GetAtPath(record.Path);
            if (importer is TextureImporter textureImporter)
            {
                DrawMetaLine(ref y, width, "IMPORT", textureImporter.textureType + " · max " + textureImporter.maxTextureSize + " · " + textureImporter.textureCompression);
            }
            else if (importer is AudioImporter audioImporter)
            {
                AudioImporterSampleSettings settings = audioImporter.defaultSampleSettings;
                DrawMetaLine(ref y, width, "IMPORT", settings.loadType + " · " + settings.compressionFormat + " · q" + settings.quality.ToString("0.00"));
            }
            else if (importer is ModelImporter modelImporter)
            {
                DrawMetaLine(ref y, width, "IMPORT", modelImporter.animationType + " · scale " + modelImporter.globalScale.ToString("0.###"));
            }
        }

        private static void DrawMetaLine(ref float y, float width, string key, string value)
        {
            GUI.Label(new Rect(0f, y, 58f, 18f), key, BetterProjectGui.Tiny);
            GUI.Label(new Rect(62f, y, width - 62f, 18f), string.IsNullOrEmpty(value) ? "—" : value, BetterProjectGui.Muted);
            y += 20f;
        }

        private void DrawImpact(Rect rect)
        {
            BetterProjectGui.DrawPanel(rect, BetterProjectGui.Canvas, false);
            BetterProjectAssetRecord record = SelectedRecords().LastOrDefault();
            if (record == null || record.IsFolder)
            {
                GUI.Label(new Rect(rect.x + 20f, rect.center.y - 12f, rect.width - 40f, 24f), "SELECT AN ASSET", BetterProjectGui.Muted);
                return;
            }
            if (!BetterProjectIndex.IsReferenceIndexReady && !BetterProjectIndex.IsReferenceIndexing)
            {
                BetterProjectIndex.StartReferenceIndex();
            }

            Rect inner = new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f);
            Rect content = new Rect(0f, 0f, inner.width - 14f, Mathf.Max(inner.height, 560f));
            impactScroll = GUI.BeginScrollView(inner, impactScroll, content);
            float centerWidth = Mathf.Clamp(content.width * 0.28f, 150f, 250f);
            float sideWidth = (content.width - centerWidth - 36f) * 0.5f;
            Rect left = new Rect(0f, 42f, sideWidth, 430f);
            Rect center = new Rect(left.xMax + 18f, 112f, centerWidth, 210f);
            Rect right = new Rect(center.xMax + 18f, 42f, sideWidth, 430f);

            string[] dependencies = BetterProjectIndex.GetDirectDependencies(record);
            IReadOnlyList<string> usedBy = BetterProjectIndex.GetReferences(record);
            DrawImpactLinks(left, dependencies, "USES", center.center, true);
            DrawImpactCenter(center, record);
            DrawImpactLinks(right, usedBy, "USED BY", center.center, false);

            float actionsY = 500f;
            if (GUI.Button(new Rect(0f, actionsY, 118f, 28f), "COLLECT USES", BetterProjectGui.Segment)) CollectDependencies(record, dependencies);
            if (GUI.Button(new Rect(124f, actionsY, 104f, 28f), "EXPORT", BetterProjectGui.Segment)) ExportWithDependencies(record);
            if (GUI.Button(new Rect(234f, actionsY, 110f, 28f), "REPLACE REF", BetterProjectGui.Segment)) BetterProjectReferenceReplaceWindow.Open(BetterProjectOperations.Load(record));
            if (GUI.Button(new Rect(350f, actionsY, 88f, 28f), "DELETE", BetterProjectGui.Segment)) BetterProjectOperations.Delete(new[] { record });
            GUI.EndScrollView();
        }

        private void DrawImpactCenter(Rect rect, BetterProjectAssetRecord record)
        {
            BetterProjectStyle style = BetterProjectIndex.GetStyle(record);
            BetterProjectGui.DrawPanel(rect, BetterProjectGui.Panel);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), style.IsValid ? style.Color : BetterProjectGui.Accent);
            UnityEngine.Object target = BetterProjectOperations.Load(record);
            Texture image = target == null ? null : AssetPreview.GetAssetPreview(target) ?? AssetPreview.GetMiniThumbnail(target);
            if (image != null) GUI.DrawTexture(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, 112f), image, ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 130f, rect.width - 24f, 34f), record.Name, BetterProjectGui.CardTitle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 164f, rect.width - 24f, 18f), BetterProjectIndex.IsIncludedByBuildHeuristic(record) ? "BUILD · LIKELY" : record.TypeName.ToUpperInvariant(), BetterProjectGui.Tiny);
            if (BetterProjectIndex.IsReferenceIndexing)
            {
                EditorGUI.ProgressBar(new Rect(rect.x + 12f, rect.yMax - 18f, rect.width - 24f, 7f), BetterProjectIndex.ReferenceIndexProgress, string.Empty);
            }
        }

        private void DrawImpactLinks(Rect rect, IEnumerable<string> paths, string label, Vector2 center, bool toCenter)
        {
            GUI.Label(new Rect(rect.x, rect.y - 28f, rect.width, 22f), label, BetterProjectGui.CardTitle);
            int index = 0;
            foreach (string path in (paths ?? Array.Empty<string>()).Take(16))
            {
                BetterProjectAssetRecord linked = BetterProjectIndex.GetByPath(path);
                Rect row = new Rect(rect.x, rect.y + index * 25f, rect.width, 22f);
                BetterProjectGui.DrawPanel(row, BetterProjectGui.Panel);
                GUI.Label(new Rect(row.x + 7f, row.y, row.width - 14f, row.height), linked == null ? Path.GetFileNameWithoutExtension(path) : linked.Name, BetterProjectGui.Tiny);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none) && linked != null)
                {
                    SelectRecord(linked, 0, false, false);
                }
                if (Event.current.type == EventType.Repaint)
                {
                    Vector2 start = toCenter ? new Vector2(row.xMax, row.center.y) : center;
                    Vector2 end = toCenter ? center : new Vector2(row.x, row.center.y);
                    Handles.BeginGUI();
                    Handles.color = new Color(BetterProjectGui.Accent.r, BetterProjectGui.Accent.g, BetterProjectGui.Accent.b, 0.28f);
                    Handles.DrawAAPolyLine(1.2f, start, end);
                    Handles.EndGUI();
                }
                index++;
            }
            if (index == 0)
            {
                GUI.Label(new Rect(rect.x, rect.y, rect.width, 24f), "NONE", BetterProjectGui.Muted);
            }
        }

        private void DrawStatus(Rect rect)
        {
            EditorGUI.DrawRect(rect, BetterProjectGui.Inset);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), BetterProjectGui.Border);
            string left = lastVisible.Count + " ITEMS";
            if (selectedGuids.Count > 0) left += "  ·  " + selectedGuids.Count + " SELECTED";
            GUI.Label(new Rect(rect.x + 8f, rect.y, rect.width * 0.5f, rect.height), left, BetterProjectGui.Tiny);
            string right = BetterProjectIndex.IsReferenceIndexing
                ? "INDEX  " + Mathf.RoundToInt(BetterProjectIndex.ReferenceIndexProgress * 100f) + "%"
                : BetterProjectIndex.IsReferenceIndexReady ? "INDEXED" : "INDEX IDLE";
            GUI.Label(new Rect(rect.xMax - 130f, rect.y, 122f, rect.height), right, BetterProjectGui.Tiny);
        }

        private IReadOnlyList<BetterProjectAssetRecord> GetVisibleAssets(string folder)
        {
            if (visibleCacheRevision != BetterProjectIndex.Revision)
            {
                visibleCache.Clear();
                visibleCacheRevision = BetterProjectIndex.Revision;
            }

            string collectionSignature = string.Empty;
            if (librarySource == BetterProjectLibrarySource.Collection)
            {
                BetterProjectCollection activeCollection = BetterProjectSettings.Collections
                    .FirstOrDefault(item => item.Id == activeCollectionId);
                if (activeCollection != null)
                {
                    collectionSignature = activeCollection.Query + ":" + activeCollection.AssetGuids.Count;
                }
            }
            string favoriteHead = BetterProjectUserSettings.FavoriteGuids.FirstOrDefault() ?? string.Empty;
            string recentHead = BetterProjectUserSettings.RecentAssetGuids.FirstOrDefault() ?? string.Empty;
            string cacheKey = string.Join("|",
                folder,
                surface,
                librarySource,
                activeCollectionId,
                collectionSignature,
                search,
                searchScope,
                sort,
                sortAscending,
                BetterProjectSettings.ShowPackages,
                BetterProjectUserSettings.FavoriteGuids.Count,
                favoriteHead,
                BetterProjectUserSettings.RecentAssetGuids.Count,
                recentHead);
            if (visibleCache.TryGetValue(cacheKey, out BetterProjectAssetRecord[] cached))
            {
                return cached;
            }

            IEnumerable<BetterProjectAssetRecord> source;
            if (surface == BetterProjectSurface.Browse)
            {
                source = string.IsNullOrWhiteSpace(search)
                    ? BetterProjectIndex.GetChildren(folder)
                    : BetterProjectIndex.Records.Where(record => IsInSearchScope(record, searchScope));
            }
            else
            {
                source = GetLibraryAssets();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    source = source.Where(record => IsInSearchScope(record, searchScope));
                }
            }
            if (!BetterProjectSettings.ShowPackages && string.IsNullOrWhiteSpace(search))
            {
                source = source.Where(record => !record.IsPackage);
            }
            BetterProjectQuery query = BetterProjectQuery.Parse(search);
            source = source.Where(record => query.Matches(
                record,
                BetterProjectIndex.GetDiagnostics(record),
                BetterProjectUserSettings.IsFavorite(record.Guid),
                BetterProjectIndex.GetLabels(record)));
            source = Sort(source);
            BetterProjectAssetRecord[] result = source.ToArray();
            if (visibleCache.Count >= 12)
            {
                visibleCache.Clear();
            }
            visibleCache[cacheKey] = result;
            return result;
        }

        internal static bool IsInSearchScope(
            BetterProjectAssetRecord record,
            BetterProjectSearchScope scope)
        {
            if (record == null)
            {
                return false;
            }

            switch (scope)
            {
                case BetterProjectSearchScope.Assets:
                    return record.Path.Equals("Assets", StringComparison.OrdinalIgnoreCase) ||
                           record.Path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
                case BetterProjectSearchScope.Packages:
                    return record.IsPackage ||
                           record.Path.Equals("Packages", StringComparison.OrdinalIgnoreCase) ||
                           record.Path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
                default:
                    return true;
            }
        }

        private IEnumerable<BetterProjectAssetRecord> GetLibraryAssets()
        {
            switch (librarySource)
            {
                case BetterProjectLibrarySource.Favorites:
                    return BetterProjectUserSettings.FavoriteGuids.Select(BetterProjectIndex.GetByGuid).Where(record => record != null);
                case BetterProjectLibrarySource.Recent:
                    return BetterProjectUserSettings.RecentAssetGuids.Select(BetterProjectIndex.GetByGuid).Where(record => record != null);
                case BetterProjectLibrarySource.Issues:
                    return BetterProjectIndex.Records.Where(record => BetterProjectIndex.GetDiagnostics(record) != BetterProjectDiagnosticFlags.None);
                case BetterProjectLibrarySource.Duplicates:
                    return BetterProjectIndex.GetContentDuplicates();
                case BetterProjectLibrarySource.Large:
                    return BetterProjectIndex.Records.Where(record => (BetterProjectIndex.GetDiagnostics(record) & BetterProjectDiagnosticFlags.Oversized) != 0);
                case BetterProjectLibrarySource.Unused:
                    return BetterProjectIndex.Records.Where(record => (BetterProjectIndex.GetDiagnostics(record) & BetterProjectDiagnosticFlags.Unreferenced) != 0);
                case BetterProjectLibrarySource.Collection:
                    return GetCollectionAssets();
                default:
                    return BetterProjectIndex.Records;
            }
        }

        private IEnumerable<BetterProjectAssetRecord> GetCollectionAssets()
        {
            BetterProjectCollection collection = BetterProjectSettings.Collections.FirstOrDefault(item => item.Id == activeCollectionId);
            if (collection == null) return Array.Empty<BetterProjectAssetRecord>();
            if (collection.Kind == BetterProjectCollectionKind.Manual)
            {
                return collection.AssetGuids.Select(BetterProjectIndex.GetByGuid).Where(record => record != null);
            }
            BetterProjectQuery query = BetterProjectQuery.Parse(collection.Query);
            return BetterProjectIndex.Records.Where(record => query.Matches(
                record,
                BetterProjectIndex.GetDiagnostics(record),
                BetterProjectUserSettings.IsFavorite(record.Guid),
                BetterProjectIndex.GetLabels(record)));
        }

        private IEnumerable<BetterProjectAssetRecord> Sort(IEnumerable<BetterProjectAssetRecord> source)
        {
            Func<BetterProjectAssetRecord, object> key = sort switch
            {
                BetterProjectSort.Type => record => record.TypeName,
                BetterProjectSort.Size => record => record.FileSize,
                BetterProjectSort.Modified => record => record.ModifiedUtc,
                _ => record => record.Name
            };
            IOrderedEnumerable<BetterProjectAssetRecord> ordered = sortAscending
                ? source.OrderByDescending(record => record.IsFolder).ThenBy(key)
                : source.OrderByDescending(record => record.IsFolder).ThenByDescending(key);
            return ordered.ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase);
        }

        private void SelectRecord(BetterProjectAssetRecord record, int index, bool toggle, bool range)
        {
            if (record == null) return;
            if (range && selectedGuids.Count > 0)
            {
                int anchor = IndexOfGuid(lastVisible, selectedGuids[selectedGuids.Count - 1]);
                if (anchor >= 0)
                {
                    selectedGuids.Clear();
                    for (int i = Math.Min(anchor, index); i <= Math.Max(anchor, index) && i < lastVisible.Count; i++)
                    {
                        selectedGuids.Add(lastVisible[i].Guid);
                    }
                }
            }
            else if (toggle)
            {
                if (!selectedGuids.Remove(record.Guid)) selectedGuids.Add(record.Guid);
            }
            else
            {
                selectedGuids.Clear();
                selectedGuids.Add(record.Guid);
            }
            BetterProjectUserSettings.TouchAsset(record.Guid);
            syncingSelection = true;
            BetterProjectOperations.Select(SelectedRecords());
            syncingSelection = false;
            DestroyPreviewEditor();
            Repaint();
        }

        private void ClearAssetSelection(bool clearUnitySelection = true)
        {
            if (selectedGuids.Count == 0 && (!clearUnitySelection || Selection.objects.Length == 0))
            {
                return;
            }
            selectedGuids.Clear();
            renamingGuid = string.Empty;
            DestroyPreviewEditor();
            if (clearUnitySelection)
            {
                syncingSelection = true;
                Selection.objects = Array.Empty<UnityEngine.Object>();
                syncingSelection = false;
            }
            Repaint();
        }

        private void SetActivePane(bool primary)
        {
            bool nextSecondary = dualPane && !primary;
            if (secondaryPaneActive == nextSecondary)
            {
                return;
            }
            secondaryPaneActive = nextSecondary;
            lastVisible = GetVisibleAssets(primary ? currentFolder : secondaryFolder);
            folderTree?.SelectPath(primary ? currentFolder : secondaryFolder, true);
            Repaint();
        }

        private IEnumerable<BetterProjectAssetRecord> SelectedRecords()
        {
            return selectedGuids.Select(BetterProjectIndex.GetByGuid).Where(record => record != null);
        }

        private void Navigate(string folder, bool push = true)
        {
            NavigatePane(folder, true, push);
        }

        private void NavigatePane(string folder, bool primary, bool push = true)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            if (primary)
            {
                currentFolder = folder;
                primaryScroll = Vector2.zero;
            }
            else
            {
                secondaryFolder = folder;
                secondaryScroll = Vector2.zero;
            }
            secondaryPaneActive = dualPane && !primary;
            surface = BetterProjectSurface.Browse;
            BetterProjectUserSettings.TouchFolder(folder);
            folderTree?.SelectPath(folder, true);
            if (push)
            {
                List<string> history = primary ? folderHistory : secondaryFolderHistory;
                int historyIndex = primary ? folderHistoryIndex : secondaryFolderHistoryIndex;
                if (historyIndex >= 0 && historyIndex < history.Count - 1)
                {
                    history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
                }
                if (history.Count == 0 || history[history.Count - 1] != folder)
                {
                    history.Add(folder);
                }
                if (primary)
                {
                    folderHistoryIndex = history.Count - 1;
                }
                else
                {
                    secondaryFolderHistoryIndex = history.Count - 1;
                }
            }
            Repaint();
        }

        private void NavigateHistory(int direction)
        {
            bool primary = !dualPane || !secondaryPaneActive;
            List<string> history = primary ? folderHistory : secondaryFolderHistory;
            int historyIndex = primary ? folderHistoryIndex : secondaryFolderHistoryIndex;
            int target = Mathf.Clamp(historyIndex + direction, 0, history.Count - 1);
            if (target == historyIndex)
            {
                return;
            }
            if (primary)
            {
                folderHistoryIndex = target;
            }
            else
            {
                secondaryFolderHistoryIndex = target;
            }
            NavigatePane(history[target], primary, false);
        }

        private bool CanNavigateHistory(int direction)
        {
            bool primary = !dualPane || !secondaryPaneActive;
            List<string> history = primary ? folderHistory : secondaryFolderHistory;
            int historyIndex = primary ? folderHistoryIndex : secondaryFolderHistoryIndex;
            int target = historyIndex + direction;
            return target >= 0 && target < history.Count;
        }

        private void EnsureHistory()
        {
            folderHistory.RemoveAll(path => !AssetDatabase.IsValidFolder(path));
            if (folderHistory.Count == 0)
            {
                folderHistory.Add(currentFolder);
                folderHistoryIndex = 0;
            }
            else
            {
                folderHistoryIndex = Mathf.Clamp(folderHistoryIndex, 0, folderHistory.Count - 1);
            }

            secondaryFolderHistory.RemoveAll(path => !AssetDatabase.IsValidFolder(path));
            if (secondaryFolderHistory.Count == 0)
            {
                secondaryFolderHistory.Add(secondaryFolder);
                secondaryFolderHistoryIndex = 0;
            }
            else
            {
                secondaryFolderHistoryIndex = Mathf.Clamp(
                    secondaryFolderHistoryIndex,
                    0,
                    secondaryFolderHistory.Count - 1);
            }
        }

        private string ActiveFolder => dualPane && secondaryPaneActive
            ? secondaryFolder
            : currentFolder;

        private void HandleKeyboard()
        {
            Event evt = Event.current;
            if (evt.type != EventType.KeyDown || focusedWindow != this) return;
            bool action = EditorGUI.actionKey;
            if (action && evt.keyCode == KeyCode.F)
            {
                GUI.FocusControl(SearchControlName); evt.Use(); return;
            }

            string focusedControl = GUI.GetNameOfFocusedControl();
            bool editingText = EditorGUIUtility.editingTextField ||
                               focusedControl == SearchControlName ||
                               focusedControl == "BetterProjectRename";
            if (editingText)
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    GUI.FocusControl(null);
                    evt.Use();
                }
                return;
            }

            bool primary = !dualPane || !secondaryPaneActive;
            if (evt.alt && evt.keyCode == KeyCode.LeftArrow) { NavigateHistory(-1); evt.Use(); return; }
            if (evt.alt && evt.keyCode == KeyCode.RightArrow) { NavigateHistory(1); evt.Use(); return; }
            if (action && evt.keyCode == KeyCode.A) { selectedGuids = lastVisible.Select(record => record.Guid).ToList(); BetterProjectOperations.Select(SelectedRecords()); evt.Use(); return; }
            if (action && evt.keyCode == KeyCode.C) { BetterProjectOperations.Copy(SelectedRecords().ToArray(), false); evt.Use(); return; }
            if (action && evt.keyCode == KeyCode.X) { BetterProjectOperations.Copy(SelectedRecords().ToArray(), true); evt.Use(); return; }
            if (action && evt.keyCode == KeyCode.V) { BetterProjectOperations.Paste(ActiveFolder); evt.Use(); return; }
            if (action && evt.keyCode == KeyCode.D) { BetterProjectOperations.Duplicate(SelectedRecords().ToArray()); evt.Use(); return; }
            if (action && evt.shift && evt.keyCode == KeyCode.N) { string folder = BetterProjectOperations.CreateFolder(ActiveFolder); StartRename(BetterProjectIndex.GetByPath(folder)); evt.Use(); return; }
            if (evt.keyCode == KeyCode.Delete) { BetterProjectOperations.Delete(SelectedRecords().ToArray()); evt.Use(); return; }
            if (evt.keyCode == KeyCode.F2) { StartRename(SelectedRecords().LastOrDefault()); evt.Use(); return; }
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                BetterProjectAssetRecord record = SelectedRecords().LastOrDefault();
                if (record != null) { if (record.IsFolder) NavigatePane(record.Path, primary); else BetterProjectOperations.Open(record); }
                evt.Use(); return;
            }
            if (evt.keyCode == KeyCode.Backspace && !action) { string parent = BetterProjectIndex.Parent(ActiveFolder); if (AssetDatabase.IsValidFolder(parent)) NavigatePane(parent, primary); evt.Use(); return; }
            if (evt.keyCode == KeyCode.F) { BetterProjectAssetRecord record = SelectedRecords().LastOrDefault(); if (record != null) NavigatePane(record.IsFolder ? record.Path : record.ParentPath, primary); evt.Use(); return; }
            if (evt.keyCode == KeyCode.Escape)
            {
                if (!string.IsNullOrEmpty(renamingGuid)) { renamingGuid = string.Empty; Repaint(); }
                else if (!string.IsNullOrEmpty(search)) { search = string.Empty; }
                evt.Use();
            }
        }

        private void StartRename(BetterProjectAssetRecord record)
        {
            if (record == null || record.IsReadOnly) return;
            renamingGuid = record.Guid;
            renameValue = record.Name;
            EditorApplication.delayCall += () =>
            {
                Focus();
                GUI.FocusControl("BetterProjectRename");
                Repaint();
            };
        }

        private void CommitRename(BetterProjectAssetRecord record)
        {
            string error = BetterProjectOperations.Rename(record, renameValue);
            if (!string.IsNullOrEmpty(error)) ShowNotification(new GUIContent(error));
            renamingGuid = string.Empty;
        }

        private void ShowAssetMenu(BetterProjectAssetRecord record, bool primary)
        {
            SetActivePane(primary);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Open"), false, () => { if (record.IsFolder) NavigatePane(record.Path, primary); else BetterProjectOperations.Open(record); });
            menu.AddItem(new GUIContent("Ping"), false, () => BetterProjectOperations.Ping(record));
            menu.AddItem(new GUIContent("Diagnostics/Show in Better Console"), false, () =>
                BetterConsoleDiagnosticBridge.OpenForAssetPaths(SelectedRecords().Select(item => item.Path)));
            menu.AddItem(new GUIContent(BetterProjectUserSettings.IsFavorite(record.Guid) ? "Remove Favorite" : "Favorite"), false, () => BetterProjectUserSettings.ToggleFavorite(record.Guid));
            menu.AddSeparator(string.Empty);
            if (!record.IsReadOnly)
            {
                menu.AddItem(new GUIContent("Rename  F2"), false, () => StartRename(record));
                menu.AddItem(new GUIContent("Duplicate  Ctrl+D"), false, () => BetterProjectOperations.Duplicate(SelectedRecords().ToArray()));
                menu.AddItem(new GUIContent("Cut  Ctrl+X"), false, () => BetterProjectOperations.Copy(SelectedRecords().ToArray(), true));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Rename"));
                menu.AddDisabledItem(new GUIContent("Duplicate"));
                menu.AddDisabledItem(new GUIContent("Cut"));
            }
            menu.AddItem(new GUIContent("Copy  Ctrl+C"), false, () => BetterProjectOperations.Copy(SelectedRecords().ToArray(), false));
            menu.AddItem(new GUIContent("Copy/Path"), false, () => BetterProjectOperations.CopyPath(SelectedRecords().ToArray(), false));
            menu.AddItem(new GUIContent("Copy/GUID"), false, () => BetterProjectOperations.CopyPath(SelectedRecords().ToArray(), true));
            menu.AddSeparator(string.Empty);
            foreach (BetterProjectCollection collection in BetterProjectSettings.Collections.Where(item => item.Kind == BetterProjectCollectionKind.Manual))
            {
                BetterProjectCollection captured = collection;
                menu.AddItem(new GUIContent("Add to/" + collection.Name), false, () => AddSelectionToCollection(captured));
            }
            menu.AddItem(new GUIContent("Add to/New Collection"), false, () => CreateCollection(true));
            AddColorMenu(menu, record);
            menu.AddItem(new GUIContent("Impact"), false, () => { surface = BetterProjectSurface.Impact; BetterProjectIndex.StartReferenceIndex(); });
            menu.AddItem(new GUIContent("Replace References…"), false, () => BetterProjectReferenceReplaceWindow.Open(BetterProjectOperations.Load(record)));
            menu.AddItem(new GUIContent("Batch…"), false, () => BetterProjectBatchWindow.Open(SelectedRecords(), ActiveFolder));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Unity Asset Menu…"), false, () => BetterProjectOperations.ShowUnityAssetMenu(new Rect(Event.current.mousePosition, Vector2.zero)));
            if (!record.IsReadOnly && record.Path != "Assets")
            {
                menu.AddItem(new GUIContent("Delete  Del"), false, () => BetterProjectOperations.Delete(SelectedRecords().ToArray()));
            }
            menu.ShowAsContext();
        }

        private static void DrawConsoleDiagnosticBadge(
            Rect rect,
            BetterConsoleDiagnosticSummary summary,
            Action open)
        {
            Color signal = summary.Errors > 0 ? BetterProjectGui.Danger : BetterProjectGui.Warning;
            bool hover = rect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(rect, hover ? signal : BetterProjectGui.Border);
            EditorGUI.DrawRect(
                new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f),
                new Color(signal.r, signal.g, signal.b, hover ? 0.28f : 0.17f));
            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                normal = { textColor = signal }
            };
            GUI.Label(rect, new GUIContent(summary.Badge, summary.Tooltip), style);
            if (GUI.Button(rect, new GUIContent(string.Empty, summary.Tooltip), GUIStyle.none)) open();
        }

        private void ShowBlankMenu(string folder, bool primary)
        {
            SetActivePane(primary);
            Vector2 anchor = Event.current.mousePosition;
            BetterProjectAssetRecord folderRecord = BetterProjectIndex.GetByPath(folder);
            UnityEngine.Object folderContext = AssetDatabase.LoadMainAssetAtPath(folder);
            syncingSelection = true;
            Selection.activeObject = folderContext;
            syncingSelection = false;

            var menu = new GenericMenu();
            int nativeItems = BetterProjectContextMenus.AddUnityAssetItems(
                menu,
                folderContext,
                menuPath => ExecuteUnityCreate(menuPath, folder, folderContext, primary));
            if (nativeItems == 0)
            {
                menu.AddItem(new GUIContent("Create…"), false, () =>
                    BetterProjectOperations.ShowUnityCreateMenu(new Rect(anchor, Vector2.zero)));
                menu.AddItem(new GUIContent("Show in Explorer"), false, () =>
                    EditorUtility.RevealInFinder(folder));
                menu.AddItem(new GUIContent("Refresh"), false, BetterProjectIndex.Refresh);
            }

            menu.AddSeparator(string.Empty);
            bool writable = folderRecord == null || !folderRecord.IsReadOnly;
            if (writable)
            {
                menu.AddItem(new GUIContent("Better Project/New Folder"), false, () =>
                {
                    string path = BetterProjectOperations.CreateFolder(folder);
                    EditorApplication.delayCall += () =>
                    {
                        BetterProjectIndex.Refresh();
                        StartRename(BetterProjectIndex.GetByPath(path));
                    };
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Better Project/New Folder"));
            }

            if (writable && BetterProjectOperations.Clipboard.Count > 0)
            {
                menu.AddItem(new GUIContent("Better Project/Paste"), false, () =>
                    BetterProjectOperations.Paste(folder));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Better Project/Paste"));
            }

            if (folderRecord != null)
            {
                bool favorite = BetterProjectUserSettings.IsFavorite(folderRecord.Guid);
                menu.AddItem(
                    new GUIContent(favorite ? "Better Project/Unpin Folder" : "Better Project/Pin Folder"),
                    false,
                    () => BetterProjectUserSettings.ToggleFavorite(folderRecord.Guid));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Better Project/Pin Folder"));
            }

            menu.AddItem(new GUIContent("Better Project/Search This Folder"), false, () =>
            {
                searchScope = folder.StartsWith("Packages", StringComparison.OrdinalIgnoreCase)
                    ? BetterProjectSearchScope.Packages
                    : BetterProjectSearchScope.Assets;
                search = "path:\"" + folder + "\"";
                visibleCache.Clear();
                GUI.FocusControl(SearchControlName);
                Repaint();
            });
            menu.AddItem(new GUIContent("Better Project/New Collection"), false, () => CreateCollection(true));
            menu.AddItem(new GUIContent("Better Project/Asset Rules…"), false, BetterProjectRulesWindow.Open);
            menu.AddItem(new GUIContent("Better Project/Refresh Index"), false, BetterProjectIndex.Refresh);
            menu.AddItem(new GUIContent("Better Project/Unity Project Window"), false, OpenUnityProjectWindow);
            menu.ShowAsContext();
        }

        private void ExecuteUnityCreate(
            string menuPath,
            string folder,
            UnityEngine.Object folderContext,
            bool primary)
        {
            var existingGuids = new HashSet<string>(
                AssetDatabase.FindAssets(string.Empty, new[] { folder }),
                StringComparer.Ordinal);
            SetActivePane(primary);
            BetterProjectContextMenus.ExecuteCreateAndReturnControl(
                menuPath,
                folderContext,
                () => CaptureCreatedAsset(folder, existingGuids, primary));
            Focus();
        }

        private void CaptureCreatedAsset(
            string folder,
            HashSet<string> existingGuids,
            bool primary)
        {
            AssetDatabase.Refresh();
            string createdPath = AssetDatabase.FindAssets(string.Empty, new[] { folder })
                .Where(guid => !existingGuids.Contains(guid))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => string.Equals(
                    BetterProjectIndex.Parent(path),
                    folder,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => File.Exists(path)
                    ? File.GetLastWriteTimeUtc(path)
                    : Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path) : DateTime.MinValue)
                .FirstOrDefault();

            Focus();
            if (string.IsNullOrEmpty(createdPath))
            {
                Repaint();
                return;
            }

            BetterProjectIndex.Refresh();
            BetterProjectAssetRecord created = BetterProjectIndex.GetByPath(createdPath);
            if (created == null)
            {
                Repaint();
                return;
            }

            SetActivePane(primary);
            selectedGuids.Clear();
            selectedGuids.Add(created.Guid);
            syncingSelection = true;
            BetterProjectOperations.Select(new[] { created });
            syncingSelection = false;
            StartRename(created);
            Repaint();
        }

        private void AddColorMenu(GenericMenu menu, BetterProjectAssetRecord record)
        {
            AddColorItem(menu, record, "Signal", DansToolboxTheme.Current.Accent);
            AddColorItem(menu, record, "Cyan", new Color32(62, 205, 224, 255));
            AddColorItem(menu, record, "Violet", new Color32(184, 105, 255, 255));
            AddColorItem(menu, record, "Green", new Color32(86, 202, 128, 255));
            AddColorItem(menu, record, "Gold", new Color32(240, 180, 72, 255));
            AddColorItem(menu, record, "Red", new Color32(235, 98, 105, 255));
            menu.AddItem(new GUIContent("Color/Clear Manual"), false, () =>
            {
                BetterProjectSettings.RecordUndo("Clear Asset Color");
                BetterProjectSettings.Rules.RemoveAll(rule => rule.Match == BetterProjectRuleMatch.Asset && rule.Value == record.Guid);
                BetterProjectSettings.SaveNow();
            });
        }

        private static void AddColorItem(GenericMenu menu, BetterProjectAssetRecord record, string label, Color color)
        {
            menu.AddItem(new GUIContent("Color/" + label), false, () =>
            {
                BetterProjectSettings.RecordUndo("Color Asset");
                BetterProjectSettings.Rules.RemoveAll(rule => rule.Match == BetterProjectRuleMatch.Asset && rule.Value == record.Guid);
                BetterProjectSettings.Rules.Add(new BetterProjectStyleRule
                {
                    Name = record.Name,
                    Match = BetterProjectRuleMatch.Asset,
                    Value = record.Guid,
                    Color = color,
                    Priority = 1000
                });
                BetterProjectSettings.SaveNow();
            });
        }

        private void ShowWindowMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Asset Rules…"), false, BetterProjectRulesWindow.Open);
            menu.AddItem(new GUIContent("Batch…"), false, () => BetterProjectBatchWindow.Open(SelectedRecords(), ActiveFolder));
            menu.AddItem(new GUIContent("Save Search/Bookmark"), false, SaveSearch);
            menu.AddItem(new GUIContent("Save Search/Smart Collection"), false, () => CreateCollection(false));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Show Packages"), BetterProjectSettings.ShowPackages, () => BetterProjectSettings.ShowPackages = !BetterProjectSettings.ShowPackages);
            menu.AddItem(new GUIContent("Show Folder Rail"), BetterProjectSettings.ShowFolderRail, () => BetterProjectSettings.ShowFolderRail = !BetterProjectSettings.ShowFolderRail);
            menu.AddItem(new GUIContent("Show Preview"), BetterProjectSettings.ShowPreview, () => BetterProjectSettings.ShowPreview = !BetterProjectSettings.ShowPreview);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Sort/Name"), sort == BetterProjectSort.Name, () => sort = BetterProjectSort.Name);
            menu.AddItem(new GUIContent("Sort/Type"), sort == BetterProjectSort.Type, () => sort = BetterProjectSort.Type);
            menu.AddItem(new GUIContent("Sort/Size"), sort == BetterProjectSort.Size, () => sort = BetterProjectSort.Size);
            menu.AddItem(new GUIContent("Sort/Modified"), sort == BetterProjectSort.Modified, () => sort = BetterProjectSort.Modified);
            menu.AddItem(new GUIContent("Sort/Ascending"), sortAscending, () => sortAscending = !sortAscending);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Rebuild Asset Index"), false, BetterProjectIndex.Refresh);
            menu.AddItem(new GUIContent("Rebuild Reference Index"), false, BetterProjectIndex.StartReferenceIndex);
            menu.AddItem(new GUIContent("Unity Project Window"), false, OpenUnityProjectWindow);
            menu.AddItem(new GUIContent("Toolbox Hub"), false, () => EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Toolbox Hub"));
            menu.ShowAsContext();
        }

        private void ShowCollectionMenu(BetterProjectCollection collection)
        {
            var menu = new GenericMenu();
            if (collection.Kind == BetterProjectCollectionKind.Manual)
            {
                menu.AddItem(new GUIContent("Add Selection"), false, () => AddSelectionToCollection(collection));
                menu.AddItem(new GUIContent("Remove Selection"), false, () => RemoveSelectionFromCollection(collection));
            }
            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Delete collection?", collection.Name, "Delete", "Cancel")) BetterProjectSettings.RemoveCollection(collection);
            });
            menu.ShowAsContext();
        }

        private void SaveSearch()
        {
            if (string.IsNullOrWhiteSpace(search)) return;
            BetterProjectSettings.RecordUndo("Save Better Project Search");
            BetterProjectSettings.SavedSearches.Add(new BetterProjectSavedSearch
            {
                Name = search.Length > 28 ? search.Substring(0, 28) : search,
                Query = search
            });
            BetterProjectSettings.SaveNow();
        }

        private void DrawPinnedFolders(Rect rect, IReadOnlyList<BetterProjectAssetRecord> folders, bool primary)
        {
            EditorGUI.DrawRect(rect, BetterProjectGui.Inset);
            float x = rect.x + 5f;
            foreach (BetterProjectAssetRecord folder in folders)
            {
                float width = Mathf.Min(110f, BetterProjectGui.Tiny.CalcSize(new GUIContent(folder.Name)).x + 18f);
                Rect button = new Rect(x, rect.y + 3f, width, 19f);
                if (GUI.Button(button, folder.Name, BetterProjectGui.Badge))
                {
                    NavigatePane(folder.Path, primary);
                }
                HandleFolderDrop(button, folder);
                if (dragHoverFolderGuid == folder.Guid)
                {
                    DrawDropTargetOutline(button);
                }
                x += width + 4f;
                if (x > rect.xMax - 50f) break;
            }
        }

        private void CreateCollection(bool manual)
        {
            string name = manual ? "Selection" : string.IsNullOrWhiteSpace(search) ? "Collection" : search;
            BetterProjectCollection collection = BetterProjectSettings.CreateCollection(
                name,
                manual ? BetterProjectCollectionKind.Manual : BetterProjectCollectionKind.Smart,
                manual ? string.Empty : search,
                manual ? selectedGuids : null);
            surface = BetterProjectSurface.Library;
            librarySource = BetterProjectLibrarySource.Collection;
            activeCollectionId = collection.Id;
        }

        private void AddSelectionToCollection(BetterProjectCollection collection)
        {
            BetterProjectSettings.RecordUndo("Add Assets to Better Project Collection");
            foreach (string guid in selectedGuids)
            {
                if (!collection.AssetGuids.Contains(guid)) collection.AssetGuids.Add(guid);
            }
            BetterProjectSettings.SaveNow();
        }

        private void RemoveSelectionFromCollection(BetterProjectCollection collection)
        {
            BetterProjectSettings.RecordUndo("Remove Assets from Better Project Collection");
            collection.AssetGuids.RemoveAll(selectedGuids.Contains);
            BetterProjectSettings.SaveNow();
        }

        private void HandleCollectionDrop(Rect rect)
        {
            Event evt = Event.current;
            if (!rect.Contains(evt.mousePosition) || (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)) return;
            if (DragAndDrop.paths == null || DragAndDrop.paths.Length == 0) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                BetterProjectCollection collection = BetterProjectSettings.CreateCollection(
                    "Dropped Assets", BetterProjectCollectionKind.Manual, string.Empty,
                    DragAndDrop.paths.Select(AssetDatabase.AssetPathToGUID));
                librarySource = BetterProjectLibrarySource.Collection;
                activeCollectionId = collection.Id;
            }
            evt.Use();
        }

        private void HandlePaneDrop(Rect rect, string folder)
        {
            Event evt = Event.current;
            if (!rect.Contains(evt.mousePosition) || (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)) return;
            if (DragAndDrop.paths == null || DragAndDrop.paths.Length == 0) return;
            ClearFolderDropHover();
            bool hasMove = DragAndDrop.paths.Any(path => BetterProjectOperations.CanMoveToFolder(path, folder));
            DragAndDrop.visualMode = hasMove ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Rejected;
            if (evt.type == EventType.DragPerform && hasMove)
            {
                DragAndDrop.AcceptDrag();
                BetterProjectOperations.Move(DragAndDrop.paths, folder);
            }
            evt.Use();
        }

        private void HandleFolderDrop(Rect rect, BetterProjectAssetRecord folder)
        {
            if (!folder.IsFolder) return;

            Event evt = Event.current;
            if (!rect.Contains(evt.mousePosition) ||
                (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform))
            {
                return;
            }
            if (DragAndDrop.paths == null || DragAndDrop.paths.Length == 0) return;

            bool hasMove = !folder.IsReadOnly &&
                           DragAndDrop.paths.Any(path => BetterProjectOperations.CanMoveToFolder(path, folder.Path));
            DragAndDrop.visualMode = hasMove ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Rejected;
            SetFolderDropHover(hasMove ? folder.Guid : string.Empty);
            if (evt.type == EventType.DragPerform && hasMove)
            {
                DragAndDrop.AcceptDrag();
                BetterProjectOperations.Move(DragAndDrop.paths, folder.Path);
                ClearFolderDropHover();
            }
            evt.Use();
        }

        private void SetFolderDropHover(string guid)
        {
            guid ??= string.Empty;
            if (dragHoverFolderGuid == guid) return;
            dragHoverFolderGuid = guid;
            Repaint();
        }

        private void ClearFolderDropHover()
        {
            SetFolderDropHover(string.Empty);
        }

        private static void DrawDropTargetOutline(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), BetterProjectGui.Accent);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), BetterProjectGui.Accent);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), BetterProjectGui.Accent);
            EditorGUI.DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), BetterProjectGui.Accent);
        }

        private void CollectDependencies(BetterProjectAssetRecord record, IEnumerable<string> dependencies)
        {
            BetterProjectCollection collection = BetterProjectSettings.CreateCollection(
                record.Name + " Uses", BetterProjectCollectionKind.Manual, string.Empty,
                dependencies.Select(AssetDatabase.AssetPathToGUID));
            surface = BetterProjectSurface.Library;
            librarySource = BetterProjectLibrarySource.Collection;
            activeCollectionId = collection.Id;
        }

        private static void ExportWithDependencies(BetterProjectAssetRecord record)
        {
            string destination = EditorUtility.SaveFilePanel("Export with dependencies", string.Empty, record.Name, "unitypackage");
            if (string.IsNullOrEmpty(destination)) return;
            AssetDatabase.ExportPackage(record.Path, destination, ExportPackageOptions.IncludeDependencies | ExportPackageOptions.Interactive);
        }

        private void DrawEmpty(Rect pane, string folder)
        {
            string message = string.IsNullOrWhiteSpace(search) ? "EMPTY" : "NO MATCHES";
            GUI.Label(new Rect(pane.x + 20f, pane.center.y - 16f, pane.width - 40f, 24f), message, BetterProjectGui.Muted);
            if (string.IsNullOrWhiteSpace(search) && !folder.StartsWith("Packages/", StringComparison.Ordinal) &&
                GUI.Button(new Rect(pane.center.x - 48f, pane.center.y + 12f, 96f, 25f), "+ FOLDER", BetterProjectGui.Segment))
            {
                BetterProjectOperations.CreateFolder(folder);
            }
        }

        private void EnsurePreviewEditor(UnityEngine.Object target)
        {
            if (target == previewTarget && previewEditor != null) return;
            DestroyPreviewEditor();
            previewTarget = target;
            if (target != null) previewEditor = UnityEditor.Editor.CreateEditor(target);
        }

        private void DestroyPreviewEditor()
        {
            if (previewEditor != null) DestroyImmediate(previewEditor);
            previewEditor = null;
            previewTarget = null;
        }

        private void RebuildFolderTree()
        {
            folderTreeState ??= new BetterProjectTreeViewState();
            folderTree = new BetterProjectFolderTree(
                folderTreeState,
                path => NavigatePane(path, !dualPane || !secondaryPaneActive));
            folderTree.SelectPath(ActiveFolder, false);
        }

        private void OnIndexChanged()
        {
            visibleCache.Clear();
            visibleCacheRevision = BetterProjectIndex.Revision;
            if (lastSeenRevision != BetterProjectIndex.Revision)
            {
                lastSeenRevision = BetterProjectIndex.Revision;
                RebuildFolderTree();
            }
            Repaint();
        }

        private void OnConsoleDiagnosticsChanged()
        {
            Repaint();
        }

        private void OnConsoleAssetRevealRequested(string assetPath)
        {
            BetterProjectAssetRecord record = BetterProjectIndex.GetByPath(assetPath);
            if (record == null) return;
            surface = BetterProjectSurface.Browse;
            activeCollectionId = string.Empty;
            selectedGuids.Clear();
            selectedGuids.Add(record.Guid);
            string folder = record.IsFolder
                ? record.Path
                : Path.GetDirectoryName(record.Path)?.Replace('\\', '/') ?? "Assets";
            NavigatePane(folder, true);
            folderTree?.SelectPath(folder, true);
            Repaint();
        }

        private void OnUndoRedo()
        {
            BetterProjectSettings.EnsureInitialized();
            BetterProjectIndex.InvalidatePresentation();
        }

        private void SyncFromUnitySelection()
        {
            if (syncingSelection) return;
            List<string> guids = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(AssetDatabase.AssetPathToGUID)
                .Where(guid => !string.IsNullOrEmpty(guid))
                .Distinct()
                .ToList();
            if (guids.Count > 0)
            {
                selectedGuids = guids;
                foreach (string guid in guids) BetterProjectUserSettings.TouchAsset(guid);
                DestroyPreviewEditor();
                Repaint();
            }
        }

        private static int IndexOfGuid(IReadOnlyList<BetterProjectAssetRecord> records, string guid)
        {
            for (int index = 0; index < records.Count; index++)
            {
                if (records[index].Guid == guid) return index;
            }
            return -1;
        }

        private static void OpenUnityProjectWindow()
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (type != null) GetWindow(type);
        }
    }
}
