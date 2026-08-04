using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor
{
    internal sealed class DansToolboxHubWindow : EditorWindow
    {
        private const float PopupWidth = 760f;
        private const float PopupHeight = 680f;
        private const float Margin = 16f;
        private const float HeaderHeight = 48f;
        private const float UpdateBannerHeight = 58f;
        private const float SearchHeight = 38f;
        private const float FooterHeight = 30f;
        private const float CardHeight = 168f;
        private const float CardTitleHeight = 36f;
        private const float CardGap = 10f;
        private const string SearchControl = "DansToolboxHubSearch";
        private const string FavoritesKey = "DansToolbox.Hub.Favorites";
        private const string RecentsKey = "DansToolbox.Hub.Recents";
        private const int MaximumRecents = 5;

        [SerializeField] private string search = string.Empty;
        [SerializeField] private Vector2 scroll;
        [SerializeField] private int groupFilter = -1;

        [NonSerialized] private HashSet<string> favorites;
        [NonSerialized] private List<string> recents;
        [NonSerialized] private Dictionary<string, int> openCounts;
        [NonSerialized] private Dictionary<string, Texture> nativeIcons;
        [NonSerialized] private List<DansToolboxLaunchDescriptor> visibleTools;
        [NonSerialized] private bool visibleToolsDirty = true;
        [NonSerialized] private string hoveredToolId;
        [NonSerialized] private string hoverCandidateToolId;
        [NonSerialized] private HubStyles styles;
        [NonSerialized] private int styledThemeRevision = -1;
        [NonSerialized] private bool focusSearch;
        [NonSerialized] private bool clearInitialFocus;
        [NonSerialized] private Rect lastSearchRect;
        [NonSerialized] private double nextOpenCountRefresh;

        [MenuItem("Tools/Dans Toolbox/Toolbox Hub", false, -100)]
        internal static void Open()
        {
            foreach (DansToolboxHubWindow existing in
                     Resources.FindObjectsOfTypeAll<DansToolboxHubWindow>())
            {
                existing.Close();
            }

            Rect main = EditorGUIUtility.GetMainWindowPosition();
            float width = Mathf.Min(PopupWidth, Mathf.Max(480f, main.width - 36f));
            float height = Mathf.Min(PopupHeight, Mathf.Max(520f, main.height - 96f));
            float x = Mathf.Clamp(main.x + 18f, main.x, main.xMax - width);
            float y = main.y + 38f;

            DansToolboxHubWindow window = CreateInstance<DansToolboxHubWindow>();
            window.titleContent = new GUIContent("Dans Toolbox");
            window.minSize = window.maxSize = new Vector2(width, height);
            window.focusSearch = false;
            window.clearInitialFocus = true;
            window.ShowAsDropDown(new Rect(x, y, 1f, 1f), new Vector2(width, height));
            window.Focus();
        }

        private void OnEnable()
        {
            favorites = LoadIds(FavoritesKey).ToHashSet(StringComparer.Ordinal);
            recents = LoadIds(RecentsKey).Take(MaximumRecents).ToList();
            RefreshOpenCounts();
            DansToolboxTheme.Changed -= OnThemeChanged;
            DansToolboxTheme.Changed += OnThemeChanged;
            DansToolboxUpdateService.Changed -= OnUpdateChanged;
            DansToolboxUpdateService.Changed += OnUpdateChanged;
            wantsMouseMove = true;
        }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup < nextOpenCountRefresh)
            {
                return;
            }

            if (RefreshOpenCounts())
            {
                Repaint();
            }
        }

        private void OnDisable()
        {
            DansToolboxTheme.Changed -= OnThemeChanged;
            DansToolboxUpdateService.Changed -= OnUpdateChanged;
        }

        private void OnThemeChanged()
        {
            styledThemeRevision = -1;
            Repaint();
        }

        private void OnUpdateChanged()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EnsureState();
            EventType inputEventType = Event.current.type;
            hoverCandidateToolId = null;
            DansToolboxPalette palette = DansToolboxTheme.Current;
            EnsureStyles(palette);
            if (DansToolboxSearchField.ReleaseFocusOnPointerDown(lastSearchRect, SearchControl)) Repaint();
            HandleKeyboard();
            if (clearInitialFocus)
            {
                GUIUtility.keyboardControl = 0;
            }

            Rect canvas = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(canvas, palette.Canvas);
            if (!DansToolboxSettings.SeamlessToolSurfaces)
            {
                EditorGUI.DrawRect(new Rect(0f, 0f, canvas.width, 3f), palette.Accent);
            }

            bool showUpdate = DansToolboxUpdateService.UpdateAvailable ||
                              DansToolboxUpdateService.IsUpdating;
            HubLayoutRegions layout = CalculateLayout(position.size, showUpdate);
            DrawHeader(layout.Header, palette);
            if (showUpdate)
            {
                DrawUpdateBanner(layout.Update, palette);
            }
            DrawSearch(layout.Search);
            DrawGroupFilter(layout.Filter, palette);
            DrawToolGallery(layout.Gallery, palette);
            DrawFooter(layout.Footer, palette);
            UpdateHoveredTool(inputEventType);

            if (clearInitialFocus && Event.current.type == EventType.Repaint)
            {
                clearInitialFocus = false;
                GUI.FocusControl(null);
            }
            else if (focusSearch && Event.current.type == EventType.Repaint)
            {
                focusSearch = false;
                EditorGUI.FocusTextInControl(SearchControl);
            }
        }

        private void DrawHeader(Rect row, DansToolboxPalette palette)
        {
            Texture2D icon = DansToolboxToolbarButton.LoadIcon();
            if (icon != null)
            {
                GUI.DrawTexture(new Rect(row.x, row.y + 5f, 32f, 32f), icon, ScaleMode.ScaleToFit, true);
            }

            GUI.Label(
                new Rect(row.x + 43f, row.y, row.width - 176f, 25f),
                "Dans Toolbox",
                styles.Title);
            GUI.Label(
                new Rect(row.x + 43f, row.y + 24f, row.width - 176f, 18f),
                "Choose what you want to work on.",
                styles.Subtitle);

            Rect settings = new Rect(row.xMax - 112f, row.y + 7f, 112f, 30f);
            if (DrawButton(settings, "TOOL SETTINGS", palette, false))
            {
                Close();
                EditorApplication.delayCall += DansToolboxSetupWizard.Open;
            }
        }

        private void DrawUpdateBanner(Rect row, DansToolboxPalette palette)
        {
            Color fill = Color.Lerp(palette.Inset, palette.Warning, 0.09f);
            DrawPanel(row, fill, palette.Warning);
            EditorGUI.DrawRect(new Rect(row.x + 1f, row.y + 1f, 4f, row.height - 2f), palette.Warning);

            string title = DansToolboxUpdateService.IsUpdating
                ? "UPDATING DANS TOOLBOX"
                : "UPDATE AVAILABLE";
            string body;
            if (DansToolboxUpdateService.IsUpdating)
            {
                body = $"Unity Package Manager is installing v{DansToolboxUpdateService.LatestVersion}...";
            }
            else if (!string.IsNullOrEmpty(DansToolboxUpdateService.LastError))
            {
                body = DansToolboxUpdateService.LastError;
            }
            else
            {
                body = $"v{DansToolboxUpdateService.CurrentVersion}  ->  v{DansToolboxUpdateService.LatestVersion}";
            }

            GUI.Label(
                new Rect(row.x + 15f, row.y + 6f, row.width - 258f, 18f),
                title,
                styles.UpdateTitle);
            GUI.Label(
                new Rect(row.x + 15f, row.y + 27f, row.width - 258f, 20f),
                body,
                string.IsNullOrEmpty(DansToolboxUpdateService.LastError)
                    ? styles.UpdateBody
                    : styles.UpdateError);

            Rect release = new Rect(row.xMax - 230f, row.y + 14f, 108f, 30f);
            if (DrawButton(release, "RELEASE NOTES", palette, false))
            {
                DansToolboxUpdateService.OpenReleasePage();
            }

            Rect primary = new Rect(row.xMax - 114f, row.y + 14f, 102f, 30f);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !DansToolboxUpdateService.IsUpdating;
            string action = DansToolboxUpdateService.CanAutoUpdate
                ? DansToolboxUpdateService.IsUpdating ? "UPDATING..." : "UPDATE NOW"
                : "PACKAGE MGR";
            if (DrawButton(primary, action, palette, true))
            {
                if (DansToolboxUpdateService.CanAutoUpdate)
                {
                    DansToolboxUpdateService.BeginUpdate();
                }
                else
                {
                    DansToolboxUpdateService.OpenPackageManager();
                }
            }
            GUI.enabled = previousEnabled;
        }

        private void DrawSearch(Rect rect)
        {
            lastSearchRect = new Rect(
                rect.x,
                rect.center.y - DansToolboxSearchField.Height * 0.5f,
                rect.width,
                DansToolboxSearchField.Height);
            string next = DansToolboxSearchField.Draw(
                lastSearchRect,
                search,
                SearchControl,
                "Find tools - audio, assets, logs, scene...");
            if (!string.Equals(next, search, StringComparison.Ordinal))
            {
                search = next;
                scroll = Vector2.zero;
                visibleToolsDirty = true;
            }
        }

        private void DrawGroupFilter(Rect rect, DansToolboxPalette palette)
        {
            string[] labels = { "ALL TOOLS", "WORKSPACE", "CREATE", "INTEGRATE" };
            float gap = 6f;
            float width = (rect.width - gap * (labels.Length - 1)) / labels.Length;
            for (int index = 0; index < labels.Length; index++)
            {
                int filter = index - 1;
                Rect button = new Rect(
                    rect.x + index * (width + gap),
                    rect.y,
                    width,
                    rect.height);
                bool active = groupFilter == filter;
                bool hovered = button.Contains(Event.current.mousePosition);
                Color accent = filter < 0
                    ? palette.Accent
                    : GetGroupColor((DansToolboxToolGroup)filter, palette);
                DrawPanel(
                    button,
                    active ? palette.Raised : hovered ? palette.Panel : palette.Inset,
                    active ? accent : hovered ? palette.BorderStrong : palette.Border);
                if (active)
                {
                    EditorGUI.DrawRect(
                        new Rect(button.x + 1f, button.yMax - 3f, button.width - 2f, 2f),
                        accent);
                }
                GUI.Label(button, labels[index], active ? styles.FilterActive : styles.Filter);
                if (GUI.Button(button, GUIContent.none, GUIStyle.none))
                {
                    groupFilter = filter;
                    scroll = Vector2.zero;
                    visibleToolsDirty = true;
                    GUI.FocusControl(null);
                }
            }
        }

        private void DrawToolGallery(Rect rect, DansToolboxPalette palette)
        {
            GUILayout.BeginArea(rect);
            scroll = EditorGUILayout.BeginScrollView(
                scroll,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none,
                GUILayout.Width(rect.width),
                GUILayout.Height(rect.height));

            IReadOnlyList<DansToolboxLaunchDescriptor> matches = GetVisibleTools();
            if (matches.Count == 0)
            {
                DrawEmptyState(palette);
            }
            else
            {
                DrawToolGrid(matches, palette, rect.width - 17f);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private IReadOnlyList<DansToolboxLaunchDescriptor> GetVisibleTools()
        {
            if (!visibleToolsDirty && visibleTools != null)
            {
                return visibleTools;
            }

            visibleTools ??= new List<DansToolboxLaunchDescriptor>();
            visibleTools.Clear();
            foreach (DansToolboxLaunchDescriptor descriptor in DansToolboxToolLauncher.All)
            {
                if ((groupFilter < 0 || (int)descriptor.Group == groupFilter) &&
                    MatchesSearch(descriptor))
                {
                    visibleTools.Add(descriptor);
                }
            }

            visibleTools.Sort(CompareVisibleTools);
            visibleToolsDirty = false;
            return visibleTools;
        }

        private int CompareVisibleTools(
            DansToolboxLaunchDescriptor left,
            DansToolboxLaunchDescriptor right)
        {
            int favoriteOrder = favorites.Contains(right.Id).CompareTo(
                favorites.Contains(left.Id));
            if (favoriteOrder != 0)
            {
                return favoriteOrder;
            }

            int recentOrder = RecentRank(left.Id).CompareTo(RecentRank(right.Id));
            if (recentOrder != 0)
            {
                return recentOrder;
            }

            return string.Compare(
                DansToolboxTools.Find(left.Id).Name,
                DansToolboxTools.Find(right.Id).Name,
                StringComparison.Ordinal);
        }

        private void DrawToolGrid(
            IReadOnlyList<DansToolboxLaunchDescriptor> descriptors,
            DansToolboxPalette palette,
            float availableWidth)
        {
            int columns = CalculateColumnCount(availableWidth);
            for (int start = 0; start < descriptors.Count; start += columns)
            {
                Rect row = GUILayoutUtility.GetRect(1f, CardHeight, GUILayout.ExpandWidth(true));
                float cardWidth = (row.width - CardGap * (columns - 1)) / columns;
                for (int column = 0; column < columns; column++)
                {
                    int index = start + column;
                    if (index >= descriptors.Count)
                    {
                        break;
                    }

                    DrawToolCard(
                        new Rect(
                            row.x + column * (cardWidth + CardGap),
                            row.y,
                            cardWidth,
                            CardHeight),
                        descriptors[index],
                        palette);
                }

                GUILayout.Space(CardGap);
            }
        }

        internal static int CalculateColumnCount(float availableWidth)
        {
            if (availableWidth >= 900f) return 4;
            if (availableWidth >= 540f) return 3;
            if (availableWidth >= 340f) return 2;
            return 1;
        }

        internal static HubLayoutRegions CalculateLayout(Vector2 windowSize)
        {
            return CalculateLayout(windowSize, false);
        }

        internal static HubLayoutRegions CalculateLayout(Vector2 windowSize, bool showUpdate)
        {
            Rect content = new Rect(
                Margin,
                Margin + 2f,
                windowSize.x - Margin * 2f,
                windowSize.y - Margin * 2f - 2f);
            Rect header = new Rect(content.x, content.y, content.width, HeaderHeight);
            Rect update = showUpdate
                ? new Rect(content.x, header.yMax + 8f, content.width, UpdateBannerHeight)
                : default;
            float searchY = showUpdate ? update.yMax + 8f : header.yMax + 10f;
            Rect search = new Rect(content.x, searchY, content.width, SearchHeight);
            Rect filter = new Rect(content.x, search.yMax + 8f, content.width, 30f);
            Rect footer = new Rect(
                content.x,
                content.yMax - FooterHeight,
                content.width,
                FooterHeight);
            Rect gallery = new Rect(
                content.x,
                filter.yMax + 10f,
                content.width,
                Mathf.Max(120f, footer.y - filter.yMax - 20f));
            return new HubLayoutRegions(header, update, search, filter, gallery, footer);
        }

        internal readonly struct HubLayoutRegions
        {
            internal HubLayoutRegions(
                Rect header,
                Rect update,
                Rect search,
                Rect filter,
                Rect gallery,
                Rect footer)
            {
                Header = header;
                Update = update;
                Search = search;
                Filter = filter;
                Gallery = gallery;
                Footer = footer;
            }

            internal Rect Header { get; }
            internal Rect Update { get; }
            internal Rect Search { get; }
            internal Rect Filter { get; }
            internal Rect Gallery { get; }
            internal Rect Footer { get; }
        }

        private void DrawToolCard(
            Rect rect,
            DansToolboxLaunchDescriptor descriptor,
            DansToolboxPalette palette)
        {
            DansToolboxToolDescriptor tool = DansToolboxTools.Find(descriptor.Id);
            bool enabled = DansToolboxSettings.IsToolEnabled(descriptor.Id);
            int openCount = openCounts.TryGetValue(descriptor.Id, out int count) ? count : 0;
            bool pointerOver = rect.Contains(Event.current.mousePosition);
            if (pointerOver)
            {
                hoverCandidateToolId = descriptor.Id;
            }
            bool hovered = string.Equals(
                hoveredToolId,
                descriptor.Id,
                StringComparison.Ordinal);
            Color accent = GetGroupColor(descriptor.Group, palette);
            Rect thumbnail = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - CardTitleHeight - 1f);
            Rect titleBar = new Rect(rect.x + 1f, thumbnail.yMax, rect.width - 2f, CardTitleHeight - 1f);
            DrawPanel(
                rect,
                hovered ? palette.Hover : palette.Panel,
                openCount > 0 ? accent : hovered ? palette.BorderStrong : palette.Border);
            EditorGUI.DrawRect(
                new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, hovered ? 3f : 2f),
                hovered || openCount > 0 ? accent : new Color(accent.r, accent.g, accent.b, 0.38f));

            DrawToolThumbnail(thumbnail, descriptor, palette, accent, enabled, hovered);
            EditorGUI.DrawRect(titleBar, hovered ? palette.Raised : palette.Inset);
            GUI.Label(
                new Rect(titleBar.x + 11f, titleBar.y, titleBar.width - 22f, titleBar.height),
                tool.Name,
                enabled ? styles.CardName : styles.CardNameDisabled);
            if (openCount > 0)
            {
                EditorGUI.DrawRect(
                    new Rect(titleBar.xMax - 14f, titleBar.center.y - 3f, 6f, 6f),
                    accent);
            }

            Rect favoriteRect = new Rect(thumbnail.xMax - 61f, thumbnail.y + 8f, 25f, 25f);
            Rect moreRect = new Rect(thumbnail.xMax - 33f, thumbnail.y + 8f, 25f, 25f);
            if (hovered)
            {
                if (GUI.Button(
                        favoriteRect,
                        new GUIContent(favorites.Contains(descriptor.Id) ? "\u2605" : "\u2606", "Pin this tool"),
                        favorites.Contains(descriptor.Id) ? styles.FavoriteActive : styles.Favorite))
                {
                    ToggleFavorite(descriptor.Id);
                }
                if (GUI.Button(moreRect, new GUIContent("...", "Choose position"), styles.More))
                {
                    ShowPlacementMenu(descriptor, moreRect);
                }
            }

            Event current = Event.current;
            if (current.type == EventType.MouseUp &&
                current.button == 0 &&
                pointerOver &&
                !favoriteRect.Contains(current.mousePosition) &&
                !moreRect.Contains(current.mousePosition))
            {
                ScheduleLaunch(descriptor.Id, DansToolboxPlacement.Auto, false);
                current.Use();
            }
            else if (current.type == EventType.MouseDown &&
                     current.button == 1 &&
                     pointerOver)
            {
                ShowPlacementMenu(descriptor, new Rect(current.mousePosition, Vector2.one));
                current.Use();
            }
        }

        private void DrawToolThumbnail(
            Rect rect,
            DansToolboxLaunchDescriptor descriptor,
            DansToolboxPalette palette,
            Color accent,
            bool enabled,
            bool hovered)
        {
            Color wash = Color.Lerp(palette.Inset, accent, hovered ? 0.12f : 0.045f);
            EditorGUI.DrawRect(rect, wash);

            Texture icon = GetNativeIcon(descriptor.IconName);
            if (icon != null && !hovered)
            {
                float size = Mathf.Clamp(rect.height * 0.44f, 48f, 58f);
                Rect plate = new Rect(
                    rect.center.x - 38f,
                    rect.center.y - 38f,
                    76f,
                    76f);
                DrawPanel(
                    plate,
                    new Color(palette.Panel.r, palette.Panel.g, palette.Panel.b, 0.78f),
                    new Color(palette.Border.r, palette.Border.g, palette.Border.b, 0.72f));
                Color previous = GUI.color;
                GUI.color = enabled
                    ? new Color(1f, 1f, 1f, 0.96f)
                    : new Color(1f, 1f, 1f, 0.32f);
                GUI.DrawTexture(
                    new Rect(rect.center.x - size * 0.5f, rect.center.y - size * 0.5f, size, size),
                    icon,
                    ScaleMode.ScaleToFit,
                    true);
                GUI.color = previous;
            }

            if (!hovered)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(palette.Canvas.r, palette.Canvas.g, palette.Canvas.b, 0.93f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 3f), accent);
            GUI.Label(
                new Rect(rect.x + 13f, rect.y + 12f, rect.width - 84f, 18f),
                GetToolCategory(descriptor.Id),
                styles.HoverCategory);
            GUI.Label(
                new Rect(rect.x + 13f, rect.y + 34f, rect.width - 26f, rect.height - 64f),
                GetToolSummary(descriptor.Id),
                enabled ? styles.HoverDescription : styles.HoverDescriptionDisabled);

            int openCount = openCounts.TryGetValue(descriptor.Id, out int count) ? count : 0;
            string action = !enabled
                ? "ENABLE TOOL"
                : openCount > 0
                    ? openCount > 1 ? "FOCUS  ·  " + openCount + " OPEN" : "FOCUS OPEN TOOL"
                    : "OPEN TOOL";
            GUI.Label(
                new Rect(rect.x + 13f, rect.yMax - 27f, rect.width - 26f, 18f),
                action + "  \u2192",
                enabled ? styles.HoverAction : styles.HoverActionDisabled);
        }

        private Texture GetNativeIcon(string iconName)
        {
            nativeIcons ??= new Dictionary<string, Texture>(StringComparer.Ordinal);
            if (nativeIcons.TryGetValue(iconName, out Texture cached))
            {
                return cached;
            }

            Texture icon = EditorGUIUtility.IconContent(iconName)?.image;
            if (icon == null && !iconName.StartsWith("d_", StringComparison.Ordinal))
            {
                icon = EditorGUIUtility.IconContent("d_" + iconName)?.image;
            }
            nativeIcons[iconName] = icon;
            return icon;
        }

        private void DrawEmptyState(DansToolboxPalette palette)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 130f, GUILayout.ExpandWidth(true));
            DrawPanel(rect, palette.Inset, palette.Border);
            GUI.Label(
                new Rect(rect.x + 16f, rect.y + 20f, rect.width - 32f, 26f),
                "No tools match \"" + search + "\"",
                styles.EmptyTitle);
            GUI.Label(
                new Rect(rect.x + 16f, rect.y + 50f, rect.width - 32f, 44f),
                "Try a tool name such as Console, or a job such as audio, assets, logs, or scene.",
                styles.EmptyBody);
            Rect clear = new Rect(rect.x + 16f, rect.yMax - 34f, 92f, 24f);
            if (DrawButton(clear, "CLEAR", palette, false))
            {
                search = string.Empty;
                focusSearch = true;
                visibleToolsDirty = true;
            }
        }

        private void DrawFooter(Rect row, DansToolboxPalette palette)
        {
            GUI.Label(
                new Rect(row.x, row.y, row.width - 154f, row.height),
                "Ctrl/Cmd+F searches  /  Enter opens",
                styles.Footer);
            Rect clean = new Rect(row.xMax - 146f, row.y + 1f, 146f, 28f);
            if (DrawButton(clean, "CLOSE TOOL WINDOWS", palette, false))
            {
                if (EditorUtility.DisplayDialog(
                        "Clean Toolbox Windows",
                        "Close every open Dans Toolbox window? Native Dock panels will safely detach their applications.",
                        "Close Windows",
                        "Cancel"))
                {
                    DansToolboxToolLauncher.CloseAllToolWindows();
                    Close();
                }
            }
        }

        private void ShowPlacementMenu(
            DansToolboxLaunchDescriptor descriptor,
            Rect activator)
        {
            GenericMenu menu = new GenericMenu();
            string verb = descriptor.AllowsMultiple ? "New Panel/" : "Open/";
            if (descriptor.AllowsMultiple)
            {
                AddLaunchMenuItem(
                    menu,
                    verb + "Choose Dock...",
                    descriptor.Id,
                    DansToolboxPlacement.DockPicker,
                    true);
                menu.AddSeparator(verb + "Floating/");
                AddLaunchMenuItem(menu, verb + "Floating/Top Left", descriptor.Id, DansToolboxPlacement.TopLeft, true);
                AddLaunchMenuItem(menu, verb + "Floating/Top Right", descriptor.Id, DansToolboxPlacement.TopRight, true);
                AddLaunchMenuItem(menu, verb + "Floating/Bottom Left", descriptor.Id, DansToolboxPlacement.BottomLeft, true);
                AddLaunchMenuItem(menu, verb + "Floating/Bottom Right", descriptor.Id, DansToolboxPlacement.BottomRight, true);
                AddLaunchMenuItem(menu, verb + "Floating/Center", descriptor.Id, DansToolboxPlacement.Center, true);
            }
            else
            {
                if (descriptor.DefaultPlacement == DansToolboxPlacement.InspectorDock)
                {
                    AddLaunchMenuItem(
                        menu,
                        verb + "Inspector Dock",
                        descriptor.Id,
                        DansToolboxPlacement.InspectorDock,
                        false);
                    menu.AddSeparator(verb + "Floating/");
                }
                else
                {
                    AddLaunchMenuItem(menu, verb + "Automatic", descriptor.Id, DansToolboxPlacement.Auto, false);
                }
                AddLaunchMenuItem(menu, verb + "Left", descriptor.Id, DansToolboxPlacement.Left, false);
                AddLaunchMenuItem(menu, verb + "Right", descriptor.Id, DansToolboxPlacement.Right, false);
                AddLaunchMenuItem(menu, verb + "Bottom", descriptor.Id, DansToolboxPlacement.Bottom, false);
                AddLaunchMenuItem(menu, verb + "Center", descriptor.Id, DansToolboxPlacement.Center, false);
            }

            if (!descriptor.AllowsMultiple &&
                descriptor.DefaultPlacement != DansToolboxPlacement.InspectorDock)
            {
                menu.AddSeparator(string.Empty);
                DansToolboxPlacement preferred = DansToolboxToolLauncher.GetPreferredPlacement(descriptor.Id);
                foreach (DansToolboxPlacement placement in new[]
                         {
                             DansToolboxPlacement.Left,
                             DansToolboxPlacement.Right,
                             DansToolboxPlacement.Bottom,
                             DansToolboxPlacement.Center
                         })
                {
                    DansToolboxPlacement captured = placement;
                    menu.AddItem(
                        new GUIContent("Default Position/" + placement),
                        preferred == placement,
                        () => DansToolboxToolLauncher.SetPreferredPlacement(descriptor.Id, captured));
                }
            }

            menu.DropDown(activator);
        }

        private void AddLaunchMenuItem(
            GenericMenu menu,
            string label,
            string toolId,
            DansToolboxPlacement placement,
            bool forceNew)
        {
            menu.AddItem(
                new GUIContent(label),
                false,
                () => ScheduleLaunch(toolId, placement, forceNew));
        }

        private bool MatchesSearch(DansToolboxLaunchDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            DansToolboxToolDescriptor tool = DansToolboxTools.Find(descriptor.Id);
            string query = search.Trim();
            return tool.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   tool.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   GetToolSummary(descriptor.Id).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   GetToolCategory(descriptor.Id).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   descriptor.Group.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int RecentRank(string toolId)
        {
            int index = recents.IndexOf(toolId);
            return index < 0 ? int.MaxValue : index;
        }

        private static string GetToolSummary(string toolId)
        {
            switch (toolId)
            {
                case DansToolboxTools.BetterHierarchyId:
                    return "Find, organize, and inspect scene objects at a glance.";
                case DansToolboxTools.BetterInspectorId:
                    return "Edit selections with searchable, focused component cards.";
                case DansToolboxTools.BetterProjectId:
                    return "Browse assets, trace references, and catch project issues.";
                case DansToolboxTools.BetterConsoleId:
                    return "Search, group, compare, and resolve Unity logs faster.";
                case DansToolboxTools.BetterSceneId:
                    return "Place, align, measure, and revisit scene content.";
                case DansToolboxTools.RetroSfxId:
                    return "Design, preview, process, and export game-ready sounds.";
                case DansToolboxTools.RetroVfxId:
                    return "Build and export procedural particles and flipbooks.";
                case DansToolboxTools.NativeWindowDockId:
                    return "Bring interactive Windows applications into Unity panels.";
                default:
                    return DansToolboxTools.Find(toolId).Description;
            }
        }

        private static string GetToolCategory(string toolId)
        {
            switch (toolId)
            {
                case DansToolboxTools.BetterHierarchyId: return "SCENE NAVIGATION";
                case DansToolboxTools.BetterInspectorId: return "OBJECT EDITING";
                case DansToolboxTools.BetterProjectId: return "ASSET WORKSPACE";
                case DansToolboxTools.BetterConsoleId: return "DEBUGGING";
                case DansToolboxTools.BetterSceneId: return "SCENE AUTHORING";
                case DansToolboxTools.RetroSfxId: return "AUDIO CREATION";
                case DansToolboxTools.RetroVfxId: return "VISUAL EFFECTS";
                case DansToolboxTools.NativeWindowDockId: return "APP INTEGRATION";
                default: return "TOOL";
            }
        }

        private static Color GetGroupColor(
            DansToolboxToolGroup group,
            DansToolboxPalette palette)
        {
            switch (group)
            {
                case DansToolboxToolGroup.Create: return palette.Signal;
                case DansToolboxToolGroup.Integrate: return palette.Success;
                default: return palette.Accent;
            }
        }

        private void ScheduleLaunch(
            string toolId,
            DansToolboxPlacement placement,
            bool forceNew)
        {
            AddRecent(toolId);
            if (this != null)
            {
                Close();
            }
            EditorApplication.delayCall += () =>
                DansToolboxToolLauncher.Launch(toolId, placement, forceNew);
        }

        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown)
            {
                return;
            }

            if (current.keyCode == KeyCode.F && (current.control || current.command))
            {
                focusSearch = true;
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Escape)
            {
                if (!string.IsNullOrEmpty(search))
                {
                    search = string.Empty;
                    scroll = Vector2.zero;
                    visibleToolsDirty = true;
                    GUI.FocusControl(null);
                    Repaint();
                }
                else
                {
                    Close();
                }
                current.Use();
                return;
            }

            if (current.keyCode != KeyCode.Return && current.keyCode != KeyCode.KeypadEnter)
            {
                return;
            }

            DansToolboxLaunchDescriptor first = DansToolboxToolLauncher.All
                .Where(descriptor => groupFilter < 0 || (int)descriptor.Group == groupFilter)
                .Where(MatchesSearch)
                .OrderByDescending(descriptor => favorites.Contains(descriptor.Id))
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(first.Id))
            {
                ScheduleLaunch(first.Id, DansToolboxPlacement.Auto, false);
                current.Use();
            }
        }

        private void ToggleFavorite(string toolId)
        {
            if (!favorites.Add(toolId))
            {
                favorites.Remove(toolId);
            }
            SaveIds(FavoritesKey, favorites);
            visibleToolsDirty = true;
            Repaint();
        }

        private void AddRecent(string toolId)
        {
            List<string> updated = LoadIds(RecentsKey).Take(MaximumRecents).ToList();
            updated.Remove(toolId);
            updated.Insert(0, toolId);
            if (updated.Count > MaximumRecents)
            {
                updated.RemoveRange(MaximumRecents, updated.Count - MaximumRecents);
            }
            SaveIds(RecentsKey, updated);
            if (this != null)
            {
                recents = updated;
                visibleToolsDirty = true;
            }
        }

        private void EnsureState()
        {
            favorites ??= LoadIds(FavoritesKey).ToHashSet(StringComparer.Ordinal);
            recents ??= LoadIds(RecentsKey).Take(MaximumRecents).ToList();
            openCounts ??= new Dictionary<string, int>(StringComparer.Ordinal);
            nativeIcons ??= new Dictionary<string, Texture>(StringComparer.Ordinal);
            visibleTools ??= new List<DansToolboxLaunchDescriptor>();
            search ??= string.Empty;
        }

        private bool RefreshOpenCounts()
        {
            openCounts ??= new Dictionary<string, int>(StringComparer.Ordinal);
            bool changed = false;
            IReadOnlyDictionary<string, int> currentCounts = DansToolboxToolLauncher.GetOpenCounts();
            foreach (DansToolboxLaunchDescriptor descriptor in DansToolboxToolLauncher.All)
            {
                int count = currentCounts.TryGetValue(descriptor.Id, out int current) ? current : 0;
                if (!openCounts.TryGetValue(descriptor.Id, out int previous) || previous != count)
                {
                    openCounts[descriptor.Id] = count;
                    changed = true;
                }
            }
            nextOpenCountRefresh = EditorApplication.timeSinceStartup + 0.75d;
            return changed;
        }

        private void UpdateHoveredTool(EventType inputEventType)
        {
            if (inputEventType == EventType.MouseLeaveWindow)
            {
                if (!string.IsNullOrEmpty(hoveredToolId))
                {
                    hoveredToolId = null;
                    Repaint();
                }
                return;
            }

            if (inputEventType != EventType.MouseMove &&
                inputEventType != EventType.MouseDrag &&
                inputEventType != EventType.ScrollWheel &&
                inputEventType != EventType.MouseEnterWindow)
            {
                return;
            }

            if (string.Equals(
                    hoveredToolId,
                    hoverCandidateToolId,
                    StringComparison.Ordinal))
            {
                return;
            }

            hoveredToolId = hoverCandidateToolId;
            Repaint();
        }

        private static IEnumerable<string> LoadIds(string key)
        {
            HashSet<string> known = DansToolboxTools.All
                .Select(tool => tool.Id)
                .ToHashSet(StringComparer.Ordinal);
            return EditorPrefs.GetString(key, string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(known.Contains)
                .Distinct(StringComparer.Ordinal);
        }

        private static void SaveIds(string key, IEnumerable<string> ids)
        {
            EditorPrefs.SetString(key, string.Join(";", ids));
        }

        private bool DrawButton(
            Rect rect,
            string label,
            DansToolboxPalette palette,
            bool primary)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color fill = primary
                ? hovered ? palette.AccentHover : palette.AccentSoft
                : hovered ? palette.Raised : palette.Inset;
            Color border = primary
                ? palette.Accent
                : hovered ? palette.BorderStrong : palette.Border;
            DrawPanel(rect, fill, border);
            GUI.Label(rect, label, styles.ActionButton);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private static void DrawPanel(Rect rect, Color fill, Color border)
        {
            EditorGUI.DrawRect(rect, border);
            EditorGUI.DrawRect(
                new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f)),
                fill);
        }

        private void EnsureStyles(DansToolboxPalette palette)
        {
            if (styles != null && styledThemeRevision == DansToolboxTheme.Revision)
            {
                return;
            }

            styledThemeRevision = DansToolboxTheme.Revision;
            styles = new HubStyles
            {
                Title = Label(palette.Text, 17, FontStyle.Bold),
                Subtitle = Label(palette.Muted, 10),
                UpdateTitle = Label(palette.Warning, 9, FontStyle.Bold),
                UpdateBody = Label(palette.Text, 10),
                UpdateError = Label(palette.Danger, 9),
                Filter = Label(palette.Muted, 9, FontStyle.Bold, TextAnchor.MiddleCenter),
                FilterActive = Label(palette.Text, 9, FontStyle.Bold, TextAnchor.MiddleCenter),
                CardName = Label(palette.Text, 11, FontStyle.Bold),
                CardNameDisabled = Label(palette.Muted, 11, FontStyle.Bold),
                HoverCategory = Label(palette.Accent, 8, FontStyle.Bold),
                HoverDescription = Label(palette.Text, 10, FontStyle.Normal, TextAnchor.UpperLeft, true),
                HoverDescriptionDisabled = Label(palette.Muted, 10, FontStyle.Normal, TextAnchor.UpperLeft, true),
                HoverAction = Label(palette.Accent, 8, FontStyle.Bold),
                HoverActionDisabled = Label(palette.Warning, 8, FontStyle.Bold),
                EmptyTitle = Label(palette.Text, 13, FontStyle.Bold, TextAnchor.MiddleCenter),
                EmptyBody = Label(palette.Muted, 10, FontStyle.Normal, TextAnchor.UpperCenter, true),
                Footer = Label(palette.Muted, 9),
                ActionButton = Label(palette.Text, 9, FontStyle.Bold, TextAnchor.MiddleCenter),
                Favorite = TransparentButton(palette.Muted, 18),
                FavoriteActive = TransparentButton(palette.Accent, 18),
                More = TransparentButton(palette.Muted, 11)
            };
        }

        private static GUIStyle Label(
            Color color,
            int size,
            FontStyle fontStyle = FontStyle.Normal,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            bool wordWrap = false)
        {
            return new GUIStyle(EditorStyles.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                alignment = alignment,
                wordWrap = wordWrap,
                clipping = TextClipping.Clip,
                normal = { textColor = color }
            };
        }

        private static GUIStyle TransparentButton(Color color, int size)
        {
            return new GUIStyle(GUI.skin.button)
            {
                border = new RectOffset(),
                normal = { background = null, textColor = color },
                hover = { background = null, textColor = Color.white },
                active = { background = null, textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                fontSize = size,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset()
            };
        }

        private sealed class HubStyles
        {
            internal GUIStyle Title;
            internal GUIStyle Subtitle;
            internal GUIStyle UpdateTitle;
            internal GUIStyle UpdateBody;
            internal GUIStyle UpdateError;
            internal GUIStyle Filter;
            internal GUIStyle FilterActive;
            internal GUIStyle CardName;
            internal GUIStyle CardNameDisabled;
            internal GUIStyle HoverCategory;
            internal GUIStyle HoverDescription;
            internal GUIStyle HoverDescriptionDisabled;
            internal GUIStyle HoverAction;
            internal GUIStyle HoverActionDisabled;
            internal GUIStyle EmptyTitle;
            internal GUIStyle EmptyBody;
            internal GUIStyle Footer;
            internal GUIStyle ActionButton;
            internal GUIStyle Favorite;
            internal GUIStyle FavoriteActive;
            internal GUIStyle More;
        }
    }
}
