using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DansToolbox.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterConsole
{
    public sealed class BetterConsoleWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Dans Toolbox/Better Console";
        private const string SearchControl = "BetterConsoleSearch";
        private const float ToolbarHeight = 38f;
        private const float StatusHeight = 22f;
        private const float HeaderHeight = 24f;
        private const float DetailWidth = 360f;
        private static readonly Regex RichTextRegex = new Regex(
            @"</?(?:b|i|color|size|material|quad)(?:=[^>]+)?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [SerializeField] private BetterConsoleSurface surface;
        [SerializeField] private string queryText = string.Empty;
        [SerializeField] private long selectedEntryId;
        [SerializeField] private long selectionAnchorEntryId;
        [SerializeField] private List<long> selectedEntryIds = new List<long>();
        [SerializeField] private string selectedSignature = string.Empty;
        [SerializeField] private string selectedSessionId = string.Empty;
        [SerializeField] private bool showLogs = true;
        [SerializeField] private bool showWarnings = true;
        [SerializeField] private bool showErrors = true;
        [SerializeField] private bool includeMuted;
        [SerializeField] private Vector2 listScroll;
        [SerializeField] private Vector2 detailScroll;

        [NonSerialized] private List<BetterConsoleEntry> visibleEntries = new List<BetterConsoleEntry>();
        [NonSerialized] private List<BetterConsoleIssue> visibleIssues = new List<BetterConsoleIssue>();
        [NonSerialized] private List<BetterConsoleSession> visibleSessions = new List<BetterConsoleSession>();
        [NonSerialized] private Dictionary<long, int> collapsedCounts = new Dictionary<long, int>();
        [NonSerialized] private BetterConsoleQuery query;
        [NonSerialized] private string cachedQuery = string.Empty;
        [NonSerialized] private int cacheRevision = -1;
        [NonSerialized] private int cacheFlags = -1;
        [NonSerialized] private string transientStatus = string.Empty;
        [NonSerialized] private double transientStatusUntil;
        [NonSerialized] private Rect lastSearchRect;

        [MenuItem(MenuPath, false, 23)]
        internal static void Open()
        {
            BetterConsoleWindow window = GetWindow<BetterConsoleWindow>();
            DansToolboxWindowChrome.ApplyCompactTitle(
                window,
                DansToolboxTools.BetterConsoleId);
            window.minSize = new Vector2(420f, 260f);
            window.Show();
        }

        internal static void OpenQuery(string query)
        {
            Open();
            BetterConsoleWindow window = GetWindow<BetterConsoleWindow>();
            window.surface = BetterConsoleSurface.Live;
            window.queryText = query ?? string.Empty;
            window.listScroll = Vector2.zero;
            window.Invalidate();
            window.Focus();
            window.Repaint();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOpen()
        {
            return DansToolboxSettings.IsToolEnabled(DansToolboxTools.BetterConsoleId);
        }

        private void OnEnable()
        {
            DansToolboxWindowChrome.ApplyCompactTitle(
                this,
                DansToolboxTools.BetterConsoleId);
            minSize = new Vector2(420f, 260f);
            wantsMouseMove = true;
            if (selectedEntryIds == null) selectedEntryIds = new List<long>();
            if (selectedEntryId != 0 && !selectedEntryIds.Contains(selectedEntryId)) selectedEntryIds.Add(selectedEntryId);
            BetterConsoleStore.Changed -= OnStoreChanged;
            BetterConsoleStore.Changed += OnStoreChanged;
            DansToolboxTheme.Changed -= Repaint;
            DansToolboxTheme.Changed += Repaint;
            Invalidate();
        }

        private void OnDisable()
        {
            BetterConsoleStore.Changed -= OnStoreChanged;
            DansToolboxTheme.Changed -= Repaint;
        }

        private void OnInspectorUpdate()
        {
            if (BetterConsoleCapture.PendingCount > 0 || BetterConsoleSettings.Follow) Repaint();
        }

        private void OnGUI()
        {
            DrawCanvas();
            ReleaseSearchFocusOnPointerDown();
            HandleKeyboard();
            RefreshVisible();

            Rect toolbar = new Rect(0f, 0f, position.width, ToolbarHeight);
            Rect status = new Rect(0f, position.height - StatusHeight, position.width, StatusHeight);
            Rect content = new Rect(0f, toolbar.yMax, position.width, Mathf.Max(0f, status.y - toolbar.yMax));
            DrawToolbar(toolbar);
            DrawContent(content);
            DrawStatus(status);
        }

        private void DrawCanvas()
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), DansToolboxTheme.Current.Canvas);
        }

        private void DrawToolbar(Rect rect)
        {
            BetterConsoleGui.Panel(rect, false, true);
            bool compact = rect.width < 760f;
            bool roomy = rect.width >= 1040f;
            float x = 5f;
            float y = 7f;
            float height = 24f;
            float tabWidth = compact ? 30f : 64f;
            DrawSurfaceButton(ref x, y, tabWidth, height, BetterConsoleSurface.Live, compact ? "L" : "LIVE");
            DrawSurfaceButton(ref x, y, tabWidth, height, BetterConsoleSurface.Issues, compact ? "I" : "ISSUES");
            DrawSurfaceButton(ref x, y, tabWidth, height, BetterConsoleSurface.Sessions, compact ? "S" : "SESSIONS");
            x += 5f;

            float severityWidth = compact ? 36f : 52f;
            int rightButtons = roomy ? 5 : 4;
            float rightWidth = 3f * (severityWidth + 3f) + rightButtons * 31f + 7f;
            float searchWidth = Mathf.Max(90f, rect.width - x - rightWidth);
            lastSearchRect = new Rect(x, y + 1f, searchWidth, DansToolboxSearchField.Height);
            string nextQuery = DansToolboxSearchField.Draw(
                lastSearchRect,
                queryText,
                SearchControl,
                "Search logs - sev:error");
            if (!string.Equals(nextQuery, queryText, StringComparison.Ordinal))
            {
                queryText = nextQuery;
                Invalidate();
            }
            x += searchWidth + 4f;

            if (BetterConsoleGui.Button(new Rect(x, y, 27f, height), new GUIContent("*", "Saved views")))
            {
                ShowSavedViewsMenu();
            }
            x += 31f;
            if (BetterConsoleGui.Button(new Rect(x, y, 27f, height), new GUIContent("@", "Show logs for current selection")))
            {
                string selectionQuery = BetterConsoleDiagnosticBridge.BuildTargetQuery(Selection.objects, null);
                if (string.IsNullOrEmpty(selectionQuery)) Flash("NO SELECTION");
                else
                {
                    queryText = selectionQuery;
                    surface = BetterConsoleSurface.Live;
                    listScroll = Vector2.zero;
                    Invalidate();
                }
            }
            x += 31f;
            if (roomy)
            {
                if (BetterConsoleGui.Button(
                        new Rect(x, y, 27f, height),
                        new GUIContent(BetterConsoleCapture.Paused ? ">" : "||", "Pause capture"),
                        BetterConsoleCapture.Paused))
                {
                    BetterConsoleCapture.Paused = !BetterConsoleCapture.Paused;
                }
                x += 31f;
            }

            DrawSeverityButton(ref x, y, severityWidth, height, BetterConsoleSeverity.Log);
            DrawSeverityButton(ref x, y, severityWidth, height, BetterConsoleSeverity.Warning);
            DrawSeverityButton(ref x, y, severityWidth, height, BetterConsoleSeverity.Error);

            if (BetterConsoleGui.Button(new Rect(x, y, 27f, height), new GUIContent("X", "Clear Unity and Better Console")))
            {
                ClearAll();
            }
            x += 31f;
            if (BetterConsoleGui.Button(new Rect(x, y, 27f, height), new GUIContent("...", "Console options")))
            {
                ShowWindowMenu();
            }
        }

        private void ReleaseSearchFocusOnPointerDown()
        {
            if (DansToolboxSearchField.ReleaseFocusOnPointerDown(lastSearchRect, SearchControl)) Repaint();
        }

        private void DrawSurfaceButton(
            ref float x,
            float y,
            float width,
            float height,
            BetterConsoleSurface target,
            string label)
        {
            if (BetterConsoleGui.Button(
                    new Rect(x, y, width, height),
                    new GUIContent(label, target.ToString()),
                    surface == target))
            {
                surface = target;
                listScroll = Vector2.zero;
                Invalidate();
            }
            x += width + 3f;
        }

        private void DrawSeverityButton(
            ref float x,
            float y,
            float width,
            float height,
            BetterConsoleSeverity severity)
        {
            int count = CountForSeverity(severity);
            bool selected = severity == BetterConsoleSeverity.Log
                ? showLogs
                : severity == BetterConsoleSeverity.Warning ? showWarnings : showErrors;
            string glyph = severity == BetterConsoleSeverity.Log ? "L" : severity == BetterConsoleSeverity.Warning ? "W" : "E";
            if (BetterConsoleGui.Button(
                    new Rect(x, y, width, height),
                    new GUIContent(glyph + (width > 40f ? " " + CompactCount(count) : string.Empty), severity.ToString()),
                    selected,
                    BetterConsoleGui.SeverityColor(severity)))
            {
                if (severity == BetterConsoleSeverity.Log) showLogs = !showLogs;
                else if (severity == BetterConsoleSeverity.Warning) showWarnings = !showWarnings;
                else showErrors = !showErrors;
                Invalidate();
            }
            x += width + 3f;
        }

        private void DrawContent(Rect rect)
        {
            BetterConsoleEntry selected = SelectedEntry();
            BetterConsoleDetailPlacement placement = CalculateDetailPlacement(
                rect.size,
                BetterConsoleSettings.DetailsVisible,
                selected != null || SelectedSession() != null);
            if (placement == BetterConsoleDetailPlacement.Right)
            {
                Rect list = new Rect(rect.x, rect.y, rect.width - DetailWidth - 4f, rect.height);
                Rect detail = new Rect(list.xMax + 4f, rect.y, DetailWidth, rect.height);
                DrawSurface(list);
                DrawDetails(detail);
            }
            else if (placement == BetterConsoleDetailPlacement.Bottom)
            {
                float detailHeight = Mathf.Clamp(rect.height * 0.42f, 160f, 250f);
                Rect list = new Rect(rect.x, rect.y, rect.width, rect.height - detailHeight - 4f);
                Rect detail = new Rect(rect.x, list.yMax + 4f, rect.width, detailHeight);
                DrawSurface(list);
                DrawDetails(detail);
            }
            else
            {
                DrawSurface(rect);
            }
        }

        private void DrawSurface(Rect rect)
        {
            BetterConsoleGui.Panel(rect, true);
            DrawListHeader(new Rect(rect.x, rect.y, rect.width, HeaderHeight));
            Rect body = new Rect(rect.x + 1f, rect.y + HeaderHeight, rect.width - 2f, rect.height - HeaderHeight - 1f);
            switch (surface)
            {
                case BetterConsoleSurface.Issues: DrawIssueList(body); break;
                case BetterConsoleSurface.Sessions: DrawSessionList(body); break;
                default: DrawEntryList(body); break;
            }
        }

        private void DrawListHeader(Rect rect)
        {
            EditorGUI.DrawRect(rect, DansToolboxTheme.Current.Panel);
            BetterConsoleGui.Divider(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f));
            string left;
            string right;
            if (surface == BetterConsoleSurface.Issues)
            {
                left = "ISSUE";
                right = "HITS  RATE";
            }
            else if (surface == BetterConsoleSurface.Sessions)
            {
                left = "SESSION";
                right = "L / W / E";
            }
            else
            {
                left = BetterConsoleSettings.Collapse ? "LIVE / COLLAPSED" : "LIVE";
                right = BetterConsoleSettings.ShowTimestamps ? "TIME" : "SOURCE";
            }
            GUI.Label(new Rect(rect.x + 9f, rect.y, rect.width - 130f, rect.height), left, BetterConsoleGui.Tiny);
            GUI.Label(new Rect(rect.xMax - 118f, rect.y, 108f, rect.height), right, BetterConsoleGui.Tiny);
        }

        private void DrawEntryList(Rect rect)
        {
            float rowHeight = BetterConsoleSettings.Dense ? 25f : 32f;
            float totalHeight = visibleEntries.Count * rowHeight;
            Rect view = new Rect(0f, 0f, Mathf.Max(rect.width - 14f, 1f), Mathf.Max(totalHeight, rect.height));
            listScroll = GUI.BeginScrollView(rect, listScroll, view);
            int first = Mathf.Max(0, Mathf.FloorToInt(listScroll.y / rowHeight));
            int last = Mathf.Min(visibleEntries.Count, Mathf.CeilToInt((listScroll.y + rect.height) / rowHeight) + 1);
            for (int index = first; index < last; index++)
            {
                DrawEntryRow(new Rect(0f, index * rowHeight, view.width, rowHeight), visibleEntries[index], index);
            }
            GUI.EndScrollView();
            DrawEmptyState(rect, visibleEntries.Count, "NO LOGS", "Adjust filters or enter Play mode.");

            if (BetterConsoleSettings.Follow && visibleEntries.Count > 0 && Event.current.type == EventType.Repaint)
            {
                BetterConsoleEntry lastEntry = visibleEntries[visibleEntries.Count - 1];
                if (selectedEntryId == 0 || selectedEntryId == lastEntry.id)
                {
                    listScroll.y = Mathf.Max(0f, totalHeight - rect.height);
                }
            }
        }

        private void DrawEntryRow(Rect rect, BetterConsoleEntry entry, int index)
        {
            bool selected = IsEntrySelected(entry.id);
            bool hover = rect.Contains(Event.current.mousePosition);
            Color background = selected
                ? DansToolboxTheme.Current.AccentSoft
                : hover ? DansToolboxTheme.Current.Hover
                : (index & 1) == 0 ? DansToolboxTheme.Current.Inset : DansToolboxTheme.Current.Panel;
            EditorGUI.DrawRect(rect, background);
            BetterConsoleGui.SeverityMark(rect, entry.severity);

            float x = rect.x + 9f;
            if (BetterConsoleSettings.ShowTimestamps)
            {
                GUI.Label(new Rect(x, rect.y, 58f, rect.height), entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"), BetterConsoleGui.Tiny);
                x += 62f;
            }
            if (rect.width >= 620f)
            {
                GUI.Label(new Rect(x, rect.y, 62f, rect.height), entry.category.ToString().ToUpperInvariant(), BetterConsoleGui.Tiny);
                x += 66f;
            }
            float trailing = 78f;
            GUI.Label(new Rect(x, rect.y, Mathf.Max(20f, rect.width - x - trailing), rect.height), OneLine(entry.message), BetterConsoleGui.Label);
            if (collapsedCounts.TryGetValue(entry.id, out int count) && count > 1)
            {
                GUI.Label(new Rect(rect.xMax - 70f, rect.y, 34f, rect.height), "x" + CompactCount(count), BetterConsoleGui.Tiny);
            }
            string source = !string.IsNullOrEmpty(entry.channel) ? entry.channel : entry.remote ? "REMOTE" : entry.source;
            GUI.Label(new Rect(rect.xMax - 38f, rect.y, 34f, rect.height), ShortSource(source), BetterConsoleGui.Tiny);
            BetterConsoleGui.Divider(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f));
            HandleEntryRow(rect, entry);
        }

        private void DrawIssueList(Rect rect)
        {
            const float rowHeight = 36f;
            float totalHeight = visibleIssues.Count * rowHeight;
            Rect view = new Rect(0f, 0f, Mathf.Max(rect.width - 14f, 1f), Mathf.Max(totalHeight, rect.height));
            listScroll = GUI.BeginScrollView(rect, listScroll, view);
            int first = Mathf.Max(0, Mathf.FloorToInt(listScroll.y / rowHeight));
            int last = Mathf.Min(visibleIssues.Count, Mathf.CeilToInt((listScroll.y + rect.height) / rowHeight) + 1);
            for (int index = first; index < last; index++)
            {
                DrawIssueRow(new Rect(0f, index * rowHeight, view.width, rowHeight), visibleIssues[index], index);
            }
            GUI.EndScrollView();
            DrawEmptyState(rect, visibleIssues.Count, "NO ISSUES", "Muted and resolved work stays out of the way.");
        }

        private void DrawIssueRow(Rect rect, BetterConsoleIssue issue, int index)
        {
            bool selected = string.Equals(issue.signature, selectedSignature, StringComparison.Ordinal);
            bool hover = rect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(rect, selected
                ? DansToolboxTheme.Current.AccentSoft
                : hover ? DansToolboxTheme.Current.Hover
                : (index & 1) == 0 ? DansToolboxTheme.Current.Inset : DansToolboxTheme.Current.Panel);
            BetterConsoleGui.SeverityMark(rect, issue.representative.severity);
            GUI.Label(new Rect(rect.x + 9f, rect.y + 2f, rect.width - 145f, 18f), OneLine(issue.representative.message), BetterConsoleGui.Label);
            string meta = StateGlyph(issue.triage) + "  " + issue.representative.category.ToString().ToUpperInvariant();
            if (issue.bookmarked) meta = "*  " + meta;
            GUI.Label(new Rect(rect.x + 9f, rect.y + 18f, rect.width - 145f, 16f), meta, BetterConsoleGui.Tiny);
            GUI.Label(new Rect(rect.xMax - 130f, rect.y, 52f, rect.height), "x" + CompactCount(issue.count), BetterConsoleGui.Title);
            GUI.Label(new Rect(rect.xMax - 76f, rect.y, 70f, rect.height), Rate(issue.count, issue.perMinute), BetterConsoleGui.Tiny);
            BetterConsoleGui.Divider(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f));

            Event current = Event.current;
            if (current.type == EventType.MouseDown && rect.Contains(current.mousePosition))
            {
                SelectIssue(issue);
                if (current.button == 1) ShowEntryContext(issue.representative);
                else if (current.clickCount == 2) OpenSource(issue.representative);
                current.Use();
            }
        }

        private void DrawSessionList(Rect rect)
        {
            const float rowHeight = 44f;
            float totalHeight = visibleSessions.Count * rowHeight;
            Rect view = new Rect(0f, 0f, Mathf.Max(rect.width - 14f, 1f), Mathf.Max(totalHeight, rect.height));
            listScroll = GUI.BeginScrollView(rect, listScroll, view);
            int first = Mathf.Max(0, Mathf.FloorToInt(listScroll.y / rowHeight));
            int last = Mathf.Min(visibleSessions.Count, Mathf.CeilToInt((listScroll.y + rect.height) / rowHeight) + 1);
            for (int index = first; index < last; index++)
            {
                BetterConsoleSession session = visibleSessions[index];
                Rect row = new Rect(0f, index * rowHeight, view.width, rowHeight);
                bool selected = string.Equals(session.id, selectedSessionId, StringComparison.Ordinal);
                bool hover = row.Contains(Event.current.mousePosition);
                EditorGUI.DrawRect(row, selected
                    ? DansToolboxTheme.Current.AccentSoft
                    : hover ? DansToolboxTheme.Current.Hover
                    : (index & 1) == 0 ? DansToolboxTheme.Current.Inset : DansToolboxTheme.Current.Panel);
                Color lane = session.kind == BetterConsoleSessionKind.Remote
                    ? DansToolboxTheme.Current.Accent
                    : session.active ? DansToolboxTheme.Current.Success : DansToolboxTheme.Current.BorderStrong;
                EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height), lane);
                GUI.Label(new Rect(row.x + 9f, row.y + 3f, row.width - 190f, 19f), session.label, BetterConsoleGui.Title);
                string meta = session.StartUtc.ToLocalTime().ToString("HH:mm:ss") + "  " + Duration(session);
                if (!string.Equals(session.source, "Editor", StringComparison.OrdinalIgnoreCase)) meta += "  " + session.source.ToUpperInvariant();
                GUI.Label(new Rect(row.x + 9f, row.y + 22f, row.width - 190f, 17f), meta, BetterConsoleGui.Tiny);
                GUI.Label(new Rect(row.xMax - 176f, row.y, 168f, row.height), $"{session.logs}  /  {session.warnings}  /  {session.errors}", BetterConsoleGui.Label);
                BetterConsoleGui.Divider(new Rect(row.x, row.yMax - 1f, row.width, 1f));
                Event current = Event.current;
                if (current.type == EventType.MouseDown && row.Contains(current.mousePosition))
                {
                    selectedSessionId = session.id;
                    detailScroll = Vector2.zero;
                    if (current.button == 1) ShowSessionContext(session);
                    else if (current.clickCount == 2) ShowSessionLogs(session);
                    current.Use();
                    Repaint();
                }
            }
            GUI.EndScrollView();
            DrawEmptyState(rect, visibleSessions.Count, "NO SESSIONS", "Editor activity appears here automatically.");
        }

        private void DrawDetails(Rect rect)
        {
            BetterConsoleGui.Panel(rect, false);
            Rect header = new Rect(rect.x, rect.y, rect.width, 34f);
            EditorGUI.DrawRect(header, DansToolboxTheme.Current.Raised);
            BetterConsoleGui.Divider(new Rect(header.x, header.yMax - 1f, header.width, 1f));
            if (surface == BetterConsoleSurface.Sessions && SelectedSession() != null)
            {
                DrawSessionDetails(rect, header, SelectedSession());
            }
            else
            {
                BetterConsoleEntry entry = SelectedEntry();
                if (entry != null) DrawEntryDetails(rect, header, entry);
            }
        }

        private void DrawEntryDetails(Rect rect, Rect header, BetterConsoleEntry entry)
        {
            GUI.Label(new Rect(header.x + 9f, header.y, header.width - 156f, header.height), entry.severity.ToString().ToUpperInvariant(), BetterConsoleGui.Title);
            float x = header.xMax - 146f;
            if (BetterConsoleGui.Button(new Rect(x, header.y + 5f, 42f, 24f), new GUIContent("OPEN", "Open first source frame"))) OpenSource(entry);
            x += 46f;
            if (BetterConsoleGui.Button(new Rect(x, header.y + 5f, 42f, 24f), new GUIContent("COPY", "Copy selected entries"))) CopySelectionOrEntry(entry);
            x += 46f;
            if (BetterConsoleGui.Button(new Rect(x, header.y + 5f, 42f, 24f), new GUIContent("FIX", "Copy a grounded fix prompt")))
            {
                EditorGUIUtility.systemCopyBuffer = BetterConsoleExporter.FixPrompt(entry);
                Flash("FIX PROMPT COPIED");
            }

            Rect viewport = new Rect(rect.x + 1f, header.yMax, rect.width - 2f, rect.height - header.height - 1f);
            float contentWidth = Mathf.Max(200f, viewport.width - 18f);
            string displayMessage = DisplayText(entry.message);
            float messageHeight = Mathf.Clamp(BetterConsoleGui.Wrapped.CalcHeight(new GUIContent(displayMessage), contentWidth - 18f), 38f, 140f);
            int stackLines = string.IsNullOrEmpty(entry.stackTrace) ? 0 : entry.stackTrace.Split('\n').Length;
            float propertiesHeight = entry.properties.Count * 20f;
            float totalHeight = 118f + messageHeight + stackLines * 20f + propertiesHeight + 92f;
            Rect view = new Rect(0f, 0f, contentWidth, Mathf.Max(viewport.height, totalHeight));
            detailScroll = GUI.BeginScrollView(viewport, detailScroll, view);
            float y = 10f;
            GUI.Label(new Rect(10f, y, contentWidth - 20f, 15f), EntryMeta(entry), BetterConsoleGui.Tiny);
            y += 23f;
            GUI.Label(new Rect(10f, y, contentWidth - 20f, messageHeight), displayMessage, BetterConsoleGui.Wrapped);
            y += messageHeight + 8f;

            if (!string.IsNullOrEmpty(entry.file))
            {
                Rect source = new Rect(10f, y, contentWidth - 20f, 22f);
                bool canReveal = BetterConsoleDiagnosticBridge.CanRevealAssetPath(entry.file);
                Rect openSource = canReveal ? new Rect(source.x, source.y, source.width - 29f, source.height) : source;
                if (BetterConsoleGui.Button(openSource, new GUIContent(Path.GetFileName(entry.file) + ":" + entry.line, entry.file))) OpenSource(entry);
                if (canReveal && BetterConsoleGui.Button(new Rect(source.xMax - 25f, source.y, 25f, source.height), new GUIContent("@", "Reveal source in Better Project")))
                {
                    BetterConsoleDiagnosticBridge.RevealAssetPath(entry.file);
                }
                y += 30f;
            }
            if (entry.contextInstanceId != 0)
            {
                Rect context = new Rect(10f, y, contentWidth - 20f, 22f);
                if (BetterConsoleGui.Button(context, new GUIContent("@ " + entry.contextName, "Ping context"))) PingContext(entry);
                y += 30f;
            }

            if (entry.properties.Count > 0)
            {
                GUI.Label(new Rect(10f, y, contentWidth - 20f, 18f), "DATA", BetterConsoleGui.Tiny);
                y += 20f;
                foreach (BetterConsolePropertyData property in entry.properties)
                {
                    GUI.Label(new Rect(10f, y, 100f, 18f), property.name.ToUpperInvariant(), BetterConsoleGui.Tiny);
                    GUI.Label(new Rect(114f, y, contentWidth - 124f, 18f), property.value, BetterConsoleGui.Mono);
                    y += 20f;
                }
                y += 5f;
            }

            if (!string.IsNullOrEmpty(entry.stackTrace))
            {
                GUI.Label(new Rect(10f, y, contentWidth - 20f, 18f), "STACK", BetterConsoleGui.Tiny);
                y += 20f;
                foreach (string raw in entry.stackTrace.Replace("\r", string.Empty).Split('\n'))
                {
                    string frame = raw.Trim();
                    if (frame.Length == 0) continue;
                    Rect frameRect = new Rect(10f, y, contentWidth - 20f, 19f);
                    bool hasSource = TryFrameSource(frame, out string frameFile, out int frameLine);
                    if (hasSource)
                    {
                        if (BetterConsoleGui.Button(frameRect, new GUIContent(OneLine(frame), frame)))
                        {
                            OpenSource(frameFile, frameLine, 0);
                        }
                    }
                    else
                    {
                        GUI.Label(frameRect, OneLine(frame), BetterConsoleGui.Mono);
                    }
                    y += 20f;
                }
                y += 7f;
            }

            DrawTriageStrip(entry, contentWidth, ref y);
            DrawNote(entry, contentWidth, ref y);
            GUI.EndScrollView();
        }

        private void DrawSessionDetails(Rect rect, Rect header, BetterConsoleSession session)
        {
            GUI.Label(new Rect(header.x + 9f, header.y, header.width - 104f, header.height), session.kind.ToString().ToUpperInvariant(), BetterConsoleGui.Title);
            if (BetterConsoleGui.Button(new Rect(header.xMax - 94f, header.y + 5f, 84f, 24f), new GUIContent("SHOW LOGS", "Filter Live to this session")))
            {
                ShowSessionLogs(session);
            }

            Rect viewport = new Rect(rect.x + 1f, header.yMax, rect.width - 2f, rect.height - header.height - 1f);
            Rect view = new Rect(0f, 0f, Mathf.Max(200f, viewport.width - 18f), Mathf.Max(viewport.height, 310f));
            detailScroll = GUI.BeginScrollView(viewport, detailScroll, view);
            float y = 12f;
            GUI.Label(new Rect(10f, y, view.width - 20f, 22f), session.label, BetterConsoleGui.Title);
            y += 26f;
            GUI.Label(new Rect(10f, y, view.width - 20f, 18f), session.StartUtc.ToLocalTime().ToString("yyyy-MM-dd  HH:mm:ss") + "  ·  " + Duration(session), BetterConsoleGui.Muted);
            y += 32f;
            DrawMetric(view.width, ref y, "LOG", session.logs, DansToolboxTheme.Current.Signal);
            DrawMetric(view.width, ref y, "WARN", session.warnings, DansToolboxTheme.Current.Warning);
            DrawMetric(view.width, ref y, "ERROR", session.errors, DansToolboxTheme.Current.Danger);
            y += 12f;

            BetterConsoleSession previous = PreviousComparableSession(session);
            GUI.Label(new Rect(10f, y, view.width - 20f, 18f), "VS PREVIOUS", BetterConsoleGui.Tiny);
            y += 22f;
            if (previous == null)
            {
                GUI.Label(new Rect(10f, y, view.width - 20f, 20f), "No earlier matching session.", BetterConsoleGui.Muted);
            }
            else
            {
                GUI.Label(new Rect(10f, y, view.width - 20f, 20f),
                    $"L {Delta(session.logs, previous.logs)}   W {Delta(session.warnings, previous.warnings)}   E {Delta(session.errors, previous.errors)}",
                    BetterConsoleGui.Label);
                y += 24f;
                GUI.Label(new Rect(10f, y, view.width - 20f, 20f), "Previous  " + previous.StartUtc.ToLocalTime().ToString("MMM d  HH:mm"), BetterConsoleGui.Muted);
            }
            GUI.EndScrollView();
        }

        private void DrawMetric(float width, ref float y, string label, int count, Color color)
        {
            Rect bar = new Rect(10f, y, width - 20f, 24f);
            EditorGUI.DrawRect(bar, DansToolboxTheme.Current.Inset);
            BetterConsoleGui.Border(bar, DansToolboxTheme.Current.Border);
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, 3f, bar.height), color);
            GUI.Label(new Rect(bar.x + 9f, bar.y, 70f, bar.height), label, BetterConsoleGui.Tiny);
            GUI.Label(new Rect(bar.xMax - 75f, bar.y, 65f, bar.height), count.ToString(), BetterConsoleGui.Title);
            y += 28f;
        }

        private void DrawTriageStrip(BetterConsoleEntry entry, float width, ref float y)
        {
            BetterConsoleIssueState state = BetterConsoleSettings.GetIssueState(entry.signature);
            BetterConsoleTriage current = state?.triage ?? BetterConsoleTriage.New;
            GUI.Label(new Rect(10f, y, width - 20f, 18f), "STATE", BetterConsoleGui.Tiny);
            y += 20f;
            float buttonWidth = Mathf.Max(42f, (width - 28f) / 4f);
            BetterConsoleTriage[] states =
            {
                BetterConsoleTriage.Seen,
                BetterConsoleTriage.Acknowledged,
                BetterConsoleTriage.Muted,
                BetterConsoleTriage.Resolved
            };
            float x = 10f;
            foreach (BetterConsoleTriage triage in states)
            {
                if (BetterConsoleGui.Button(
                        new Rect(x, y, buttonWidth - 3f, 23f),
                        new GUIContent(StateGlyph(triage), triage.ToString()),
                        current == triage))
                {
                    SetTriage(entry, triage);
                }
                x += buttonWidth;
            }
            y += 31f;
        }

        private void DrawNote(BetterConsoleEntry entry, float width, ref float y)
        {
            BetterConsoleIssueState state = BetterConsoleSettings.GetIssueState(entry.signature);
            string note = state?.note ?? string.Empty;
            GUI.Label(new Rect(10f, y, width - 20f, 18f), "NOTE", BetterConsoleGui.Tiny);
            y += 19f;
            Rect noteRect = new Rect(10f, y, width - 20f, 44f);
            EditorGUI.DrawRect(noteRect, DansToolboxTheme.Current.Inset);
            BetterConsoleGui.Border(noteRect, DansToolboxTheme.Current.Border);
            string next = EditorGUI.TextArea(new Rect(noteRect.x + 4f, noteRect.y + 3f, noteRect.width - 8f, noteRect.height - 6f), note, BetterConsoleGui.Wrapped);
            if (!string.Equals(next, note, StringComparison.Ordinal))
            {
                BetterConsoleSettings.SetIssueState(entry.signature, state?.triage ?? BetterConsoleTriage.New, note: next);
                Invalidate();
            }
            y += 50f;
        }

        private void DrawEmptyState(Rect rect, int count, string title, string hint)
        {
            if (count != 0) return;
            float center = rect.y + rect.height * 0.5f;
            GUIStyle centeredTitle = new GUIStyle(BetterConsoleGui.Title) { alignment = TextAnchor.MiddleCenter };
            GUIStyle centeredHint = new GUIStyle(BetterConsoleGui.Muted) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(rect.x + 20f, center - 23f, rect.width - 40f, 20f), title, centeredTitle);
            if (rect.height > 120f)
            {
                GUI.Label(new Rect(rect.x + 20f, center + 1f, rect.width - 40f, 20f), hint, centeredHint);
            }
        }

        private void DrawStatus(Rect rect)
        {
            EditorGUI.DrawRect(rect, DansToolboxTheme.Current.Panel);
            BetterConsoleGui.Divider(new Rect(rect.x, rect.y, rect.width, 1f));
            int visible = surface == BetterConsoleSurface.Live ? visibleEntries.Count : surface == BetterConsoleSurface.Issues ? visibleIssues.Count : visibleSessions.Count;
            string session = BetterConsoleStore.ActiveSession?.label ?? "EDITOR";
            string left = $"{visible}  ·  {session}";
            if (BetterConsoleCapture.Paused) left += "  ·  PAUSED";
            GUI.Label(new Rect(rect.x + 8f, rect.y + 1f, rect.width * 0.55f, rect.height - 1f), left, BetterConsoleGui.Tiny);
            string right = EditorApplication.isRemoteConnected ? "REMOTE" : BetterConsoleNativeBridge.Available ? "NATIVE + CALLBACK" : "CALLBACK";
            if (query != null && !query.IsValid) right = query.Error.ToUpperInvariant();
            if (EditorApplication.timeSinceStartup < transientStatusUntil) right = transientStatus;
            GUIStyle aligned = new GUIStyle(BetterConsoleGui.Tiny) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(rect.x + rect.width * 0.5f, rect.y + 1f, rect.width * 0.5f - 8f, rect.height - 1f), right, aligned);
        }

        private void RefreshVisible()
        {
            int flags = (showLogs ? 1 : 0) | (showWarnings ? 2 : 0) | (showErrors ? 4 : 0) |
                        (includeMuted ? 8 : 0) | (BetterConsoleSettings.Collapse ? 16 : 0) | ((int)surface << 8);
            if (cacheRevision == BetterConsoleStore.Revision && cacheFlags == flags && string.Equals(cachedQuery, queryText, StringComparison.Ordinal)) return;
            cacheRevision = BetterConsoleStore.Revision;
            cacheFlags = flags;
            cachedQuery = queryText ?? string.Empty;
            query = BetterConsoleQuery.Compile(cachedQuery);
            collapsedCounts.Clear();

            if (surface == BetterConsoleSurface.Issues)
            {
                visibleIssues = BetterConsoleStore.BuildIssues(query, includeMuted)
                    .Where(issue => SeverityAllowed(issue.representative.severity))
                    .ToList();
            }
            else if (surface == BetterConsoleSurface.Sessions)
            {
                visibleSessions = BetterConsoleStore.Sessions
                    .Where(session => query.Matches(session))
                    .OrderByDescending(session => session.startUtcTicks)
                    .ToList();
            }
            else if (BetterConsoleSettings.Collapse)
            {
                List<BetterConsoleIssue> groups = BetterConsoleStore.BuildIssues(query, includeMuted)
                    .Where(issue => SeverityAllowed(issue.representative.severity))
                    .OrderBy(issue => issue.lastUtcTicks)
                    .ToList();
                visibleEntries = groups.Select(issue => issue.representative).ToList();
                foreach (BetterConsoleIssue issue in groups) collapsedCounts[issue.representative.id] = issue.count;
            }
            else
            {
                visibleEntries = BetterConsoleStore.FilterEntries(query, showLogs, showWarnings, showErrors, includeMuted);
            }
        }

        private void HandleEntryRow(Rect rect, BetterConsoleEntry entry)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || !rect.Contains(current.mousePosition)) return;
            if (current.button == 1)
            {
                if (!IsEntrySelected(entry.id)) SelectOnly(entry);
            }
            else
            {
                SelectEntry(entry, current.shift, current.control || current.command);
            }
            selectedSessionId = string.Empty;
            detailScroll = Vector2.zero;
            MarkSeen(entry);
            if (current.button == 1) ShowEntryContext(entry);
            else if (current.clickCount == 2) OpenSource(entry);
            if (BetterConsoleSettings.Follow) BetterConsoleSettings.Follow = false;
            current.Use();
            Repaint();
        }

        private void SelectEntry(BetterConsoleEntry entry, bool shift, bool actionKey)
        {
            List<long> visibleIds = visibleEntries.Select(item => item.id).ToList();
            selectedEntryIds = CalculateEntrySelection(
                visibleIds,
                selectedEntryIds,
                selectionAnchorEntryId,
                entry.id,
                shift,
                actionKey);
            if (!shift || !visibleIds.Contains(selectionAnchorEntryId)) selectionAnchorEntryId = entry.id;

            if (selectedEntryIds.Contains(entry.id))
            {
                selectedEntryId = entry.id;
                selectedSignature = entry.signature;
            }
            else
            {
                HashSet<long> selected = new HashSet<long>(selectedEntryIds);
                BetterConsoleEntry fallback = visibleEntries.LastOrDefault(item => selected.Contains(item.id));
                selectedEntryId = fallback?.id ?? 0;
                selectedSignature = fallback?.signature ?? string.Empty;
            }
        }

        private void SelectOnly(BetterConsoleEntry entry)
        {
            selectedEntryIds.Clear();
            selectedEntryIds.Add(entry.id);
            selectedEntryId = entry.id;
            selectionAnchorEntryId = entry.id;
            selectedSignature = entry.signature;
        }

        private void SelectIssue(BetterConsoleIssue issue)
        {
            selectedEntryIds.Clear();
            selectedEntryIds.Add(issue.representative.id);
            selectedSignature = issue.signature;
            selectedEntryId = issue.representative.id;
            selectionAnchorEntryId = issue.representative.id;
            selectedSessionId = string.Empty;
            detailScroll = Vector2.zero;
            MarkSeen(issue.representative);
        }

        private BetterConsoleEntry SelectedEntry()
        {
            if (selectedEntryId != 0)
            {
                BetterConsoleEntry exact = BetterConsoleStore.Entries.FirstOrDefault(entry => entry.id == selectedEntryId);
                if (exact != null) return exact;
            }
            if (!string.IsNullOrEmpty(selectedSignature))
            {
                return BetterConsoleStore.Entries.LastOrDefault(entry => string.Equals(entry.signature, selectedSignature, StringComparison.Ordinal));
            }
            return null;
        }

        private BetterConsoleSession SelectedSession()
        {
            return BetterConsoleStore.Sessions.FirstOrDefault(item => string.Equals(item.id, selectedSessionId, StringComparison.Ordinal));
        }

        private void ShowSavedViewsMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (BetterConsoleSavedView view in BetterConsoleSettings.SavedViews)
            {
                BetterConsoleSavedView captured = view;
                menu.AddItem(new GUIContent(captured.name), string.Equals(queryText, captured.query, StringComparison.Ordinal), () =>
                {
                    queryText = captured.query;
                    Invalidate();
                    Repaint();
                });
            }
            if (BetterConsoleSettings.SavedViews.Count > 0) menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Save Current"), false, () => BetterConsoleNamePopup.Show("SAVE VIEW", "VIEW", name =>
            {
                BetterConsoleSettings.AddSavedView(name, queryText);
                Flash("VIEW SAVED");
            }));
            if (BetterConsoleSettings.SavedViews.Count > 0)
            {
                foreach (BetterConsoleSavedView view in BetterConsoleSettings.SavedViews)
                {
                    BetterConsoleSavedView captured = view;
                    menu.AddItem(new GUIContent("Remove/" + captured.name), false, () => BetterConsoleSettings.RemoveSavedView(captured.id));
                }
            }
            menu.ShowAsContext();
        }

        private void ShowWindowMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Clear"), false, ClearAll);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Collapse"), BetterConsoleSettings.Collapse, () => { BetterConsoleSettings.Collapse = !BetterConsoleSettings.Collapse; Invalidate(); });
            menu.AddItem(new GUIContent("Error Pause"), BetterConsoleSettings.ErrorPause, () => BetterConsoleSettings.ErrorPause = !BetterConsoleSettings.ErrorPause);
            menu.AddItem(new GUIContent("Follow"), BetterConsoleSettings.Follow, () => BetterConsoleSettings.Follow = !BetterConsoleSettings.Follow);
            menu.AddItem(new GUIContent("Timestamps"), BetterConsoleSettings.ShowTimestamps, () => BetterConsoleSettings.ShowTimestamps = !BetterConsoleSettings.ShowTimestamps);
            menu.AddItem(new GUIContent("Dense Rows"), BetterConsoleSettings.Dense, () => BetterConsoleSettings.Dense = !BetterConsoleSettings.Dense);
            menu.AddItem(new GUIContent("Details"), BetterConsoleSettings.DetailsVisible, () => BetterConsoleSettings.DetailsVisible = !BetterConsoleSettings.DetailsVisible);
            menu.AddItem(new GUIContent("Include Muted"), includeMuted, () => { includeMuted = !includeMuted; Invalidate(); });
            menu.AddItem(new GUIContent("Capture Native History"), BetterConsoleSettings.CaptureNativeHistory, () => BetterConsoleSettings.CaptureNativeHistory = !BetterConsoleSettings.CaptureNativeHistory);
            menu.AddItem(new GUIContent("Persist History"), BetterConsoleSettings.PersistHistory, () => BetterConsoleSettings.PersistHistory = !BetterConsoleSettings.PersistHistory);
            menu.AddSeparator(string.Empty);
            AddStackTraceItems(menu, LogType.Log);
            AddStackTraceItems(menu, LogType.Warning);
            AddStackTraceItems(menu, LogType.Error);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Export Visible/JSON"), false, () => ExportVisible(false));
            menu.AddItem(new GUIContent("Export Visible/Markdown"), false, () => ExportVisible(true));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Editor Log"), false, InternalEditorUtility.OpenEditorConsole);
            menu.AddItem(new GUIContent("Open Player Log"), false, InternalEditorUtility.OpenPlayerConsole);
            menu.AddItem(new GUIContent("Unity Console"), false, () => EditorApplication.ExecuteMenuItem("Window/General/Console"));
            menu.AddItem(new GUIContent("Toolbox Hub"), false, () => EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Toolbox Hub"));
            menu.ShowAsContext();
        }

        private void ShowEntryContext(BetterConsoleEntry entry)
        {
            BetterConsoleIssueState state = BetterConsoleSettings.GetIssueState(entry.signature);
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Open Source"), false, () => OpenSource(entry));
            if (BetterConsoleDiagnosticBridge.CanRevealAssetPath(entry.file))
            {
                menu.AddItem(new GUIContent("Reveal Source Asset"), false, () => BetterConsoleDiagnosticBridge.RevealAssetPath(entry.file));
            }
            if (entry.contextInstanceId != 0) menu.AddItem(new GUIContent("Ping Context"), false, () => PingContext(entry));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy/Message"), false, () => EditorGUIUtility.systemCopyBuffer = entry.message);
            menu.AddItem(new GUIContent("Copy/Full"), false, () => CopyFull(entry));
            int selectedCount = SelectedVisibleEntries().Count;
            if (selectedCount > 1)
            {
                menu.AddItem(new GUIContent($"Copy/Selected ({selectedCount})"), false, CopySelectedEntries);
            }
            menu.AddItem(new GUIContent("Copy/Fix Prompt"), false, () => EditorGUIUtility.systemCopyBuffer = BetterConsoleExporter.FixPrompt(entry));
            menu.AddSeparator(string.Empty);
            AddTriageItems(menu, entry, state?.triage ?? BetterConsoleTriage.New);
            menu.AddItem(new GUIContent("Bookmark"), state?.bookmarked ?? false, () =>
            {
                BetterConsoleSettings.SetIssueState(entry.signature, state?.triage ?? BetterConsoleTriage.New, !(state?.bookmarked ?? false));
                Invalidate();
            });
            menu.AddItem(new GUIContent("Mute/File"), false, () =>
            {
                if (!string.IsNullOrEmpty(entry.file)) BetterConsoleSettings.AddMuteRule(Path.GetFileName(entry.file), "file:\"" + entry.file + "\"");
                Invalidate();
            });
            menu.AddItem(new GUIContent("Save as View"), false, () => BetterConsoleNamePopup.Show("SAVE VIEW", "VIEW", name => BetterConsoleSettings.AddSavedView(name, queryText)));
            menu.ShowAsContext();
        }

        private void ShowSessionContext(BetterConsoleSession session)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show Logs"), false, () => ShowSessionLogs(session));
            menu.AddItem(new GUIContent("Copy ID"), false, () => EditorGUIUtility.systemCopyBuffer = session.id);
            menu.AddItem(new GUIContent("Export/JSON"), false, () => ExportSession(session, false));
            menu.AddItem(new GUIContent("Export/Markdown"), false, () => ExportSession(session, true));
            menu.ShowAsContext();
        }

        private void AddTriageItems(GenericMenu menu, BetterConsoleEntry entry, BetterConsoleTriage current)
        {
            foreach (BetterConsoleTriage triage in Enum.GetValues(typeof(BetterConsoleTriage)))
            {
                BetterConsoleTriage captured = triage;
                menu.AddItem(new GUIContent("State/" + triage), current == triage, () => SetTriage(entry, captured));
            }
        }

        private static void AddStackTraceItems(GenericMenu menu, LogType type)
        {
            StackTraceLogType current = PlayerSettings.GetStackTraceLogType(type);
            foreach (StackTraceLogType setting in Enum.GetValues(typeof(StackTraceLogType)))
            {
                StackTraceLogType captured = setting;
                menu.AddItem(new GUIContent("Stack Traces/" + type + "/" + setting), current == setting, () => PlayerSettings.SetStackTraceLogType(type, captured));
            }
        }

        private void ShowSessionLogs(BetterConsoleSession session)
        {
            queryText = "session:" + session.id;
            surface = BetterConsoleSurface.Live;
            listScroll = Vector2.zero;
            Invalidate();
        }

        private void ExportVisible(bool markdown)
        {
            List<BetterConsoleEntry> entries = surface == BetterConsoleSurface.Issues
                ? visibleIssues.Select(issue => issue.representative).ToList()
                : surface == BetterConsoleSurface.Sessions
                    ? BetterConsoleStore.Entries.Where(entry => visibleSessions.Any(session => session.id == entry.sessionId)).ToList()
                    : new List<BetterConsoleEntry>(visibleEntries);
            Export(entries, markdown, "better-console");
        }

        private void ExportSession(BetterConsoleSession session, bool markdown)
        {
            Export(BetterConsoleStore.Entries.Where(entry => entry.sessionId == session.id).ToList(), markdown, "better-console-" + session.kind.ToString().ToLowerInvariant());
        }

        private void Export(List<BetterConsoleEntry> entries, bool markdown, string defaultName)
        {
            string extension = markdown ? "md" : "json";
            string path = EditorUtility.SaveFilePanel("Export Better Console", string.Empty, defaultName, extension);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (markdown) BetterConsoleExporter.WriteMarkdown(path, entries);
                else BetterConsoleExporter.WriteJson(path, entries);
                Flash("EXPORTED");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Export failed", exception.Message, "OK");
            }
        }

        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown) return;
            bool command = current.control || current.command;
            if (command && current.keyCode == KeyCode.F)
            {
                GUI.FocusControl(SearchControl);
                current.Use();
            }
            else if (command && current.keyCode == KeyCode.C &&
                     !EditorGUIUtility.editingTextField &&
                     GUI.GetNameOfFocusedControl() != SearchControl &&
                     SelectedVisibleEntries().Count > 0)
            {
                CopySelectedEntries();
                current.Use();
            }
            else if (command && current.keyCode == KeyCode.L)
            {
                ClearAll();
                current.Use();
            }
            else if (current.keyCode == KeyCode.Escape && !string.IsNullOrEmpty(queryText))
            {
                queryText = string.Empty;
                Invalidate();
                current.Use();
            }
            else if (current.keyCode == KeyCode.Return && SelectedEntry() != null)
            {
                OpenSource(SelectedEntry());
                current.Use();
            }
            else if (current.keyCode == KeyCode.UpArrow || current.keyCode == KeyCode.DownArrow)
            {
                MoveSelection(current.keyCode == KeyCode.DownArrow ? 1 : -1, current.shift);
                current.Use();
            }
        }

        private void MoveSelection(int direction, bool extend)
        {
            if (surface == BetterConsoleSurface.Sessions)
            {
                int index = visibleSessions.FindIndex(item => item.id == selectedSessionId);
                index = Mathf.Clamp(index + direction, 0, visibleSessions.Count - 1);
                if (index >= 0 && index < visibleSessions.Count) selectedSessionId = visibleSessions[index].id;
            }
            else
            {
                List<BetterConsoleEntry> source = surface == BetterConsoleSurface.Issues
                    ? visibleIssues.Select(issue => issue.representative).ToList()
                    : visibleEntries;
                int index = source.FindIndex(item => item.id == selectedEntryId);
                index = Mathf.Clamp(index + direction, 0, source.Count - 1);
                if (index >= 0 && index < source.Count)
                {
                    BetterConsoleEntry entry = source[index];
                    if (surface == BetterConsoleSurface.Live) SelectEntry(entry, extend, false);
                    else SelectOnly(entry);
                    MarkSeen(entry);
                }
            }
            Repaint();
        }

        private void ClearAll()
        {
            BetterConsoleNativeBridge.ClearNative();
            BetterConsoleStore.Clear();
            selectedEntryIds.Clear();
            selectedEntryId = 0;
            selectionAnchorEntryId = 0;
            selectedSignature = string.Empty;
            selectedSessionId = string.Empty;
            Invalidate();
        }

        private void SetTriage(BetterConsoleEntry entry, BetterConsoleTriage triage)
        {
            BetterConsoleIssueState state = BetterConsoleSettings.GetIssueState(entry.signature);
            BetterConsoleSettings.SetIssueState(entry.signature, triage, state?.bookmarked, state?.note);
            Invalidate();
            Repaint();
        }

        private static void MarkSeen(BetterConsoleEntry entry)
        {
            BetterConsoleIssueState state = BetterConsoleSettings.GetIssueState(entry.signature);
            if (state == null || state.triage == BetterConsoleTriage.New)
            {
                BetterConsoleSettings.SetIssueState(entry.signature, BetterConsoleTriage.Seen);
            }
        }

        private static void CopyFull(BetterConsoleEntry entry)
        {
            EditorGUIUtility.systemCopyBuffer = FormatEntriesForClipboard(new[] { entry });
        }

        private void CopySelectionOrEntry(BetterConsoleEntry fallback)
        {
            List<BetterConsoleEntry> selected = SelectedVisibleEntries();
            if (selected.Count == 0 && fallback != null) selected.Add(fallback);
            CopyEntries(selected);
        }

        private void CopySelectedEntries()
        {
            CopyEntries(SelectedVisibleEntries());
        }

        private void CopyEntries(IReadOnlyList<BetterConsoleEntry> entries)
        {
            if (entries == null || entries.Count == 0) return;
            EditorGUIUtility.systemCopyBuffer = FormatEntriesForClipboard(entries);
            Flash(entries.Count == 1 ? "ENTRY COPIED" : $"{entries.Count} ENTRIES COPIED");
        }

        private List<BetterConsoleEntry> SelectedVisibleEntries()
        {
            if (surface == BetterConsoleSurface.Issues)
            {
                BetterConsoleEntry issue = SelectedEntry();
                return issue == null ? new List<BetterConsoleEntry>() : new List<BetterConsoleEntry> { issue };
            }
            if (surface != BetterConsoleSurface.Live) return new List<BetterConsoleEntry>();
            HashSet<long> selected = new HashSet<long>(selectedEntryIds);
            return visibleEntries.Where(entry => selected.Contains(entry.id)).ToList();
        }

        private bool IsEntrySelected(long entryId)
        {
            return selectedEntryIds != null && selectedEntryIds.Contains(entryId);
        }

        internal static List<long> CalculateEntrySelection(
            IReadOnlyList<long> visibleIds,
            IReadOnlyCollection<long> currentSelection,
            long anchorId,
            long clickedId,
            bool shift,
            bool actionKey)
        {
            List<long> order = visibleIds?.ToList() ?? new List<long>();
            int clickedIndex = order.IndexOf(clickedId);
            if (clickedIndex < 0) return currentSelection?.ToList() ?? new List<long>();

            HashSet<long> selected = currentSelection == null
                ? new HashSet<long>()
                : new HashSet<long>(currentSelection);
            int anchorIndex = order.IndexOf(anchorId);
            if (shift && anchorIndex >= 0)
            {
                if (!actionKey) selected.Clear();
                int first = Math.Min(anchorIndex, clickedIndex);
                int last = Math.Max(anchorIndex, clickedIndex);
                for (int index = first; index <= last; index++) selected.Add(order[index]);
            }
            else if (actionKey)
            {
                if (!selected.Add(clickedId)) selected.Remove(clickedId);
            }
            else
            {
                selected.Clear();
                selected.Add(clickedId);
            }

            return order.Where(selected.Contains).ToList();
        }

        internal static string FormatEntriesForClipboard(IEnumerable<BetterConsoleEntry> entries)
        {
            if (entries == null) return string.Empty;
            StringBuilder clipboard = new StringBuilder();
            foreach (BetterConsoleEntry entry in entries.Where(item => item != null))
            {
                if (clipboard.Length > 0) clipboard.Append("\n\n");
                clipboard.Append(entry.message ?? string.Empty);
                if (!string.IsNullOrEmpty(entry.stackTrace))
                {
                    if (clipboard.Length > 0 && clipboard[clipboard.Length - 1] != '\n') clipboard.Append('\n');
                    clipboard.Append(entry.stackTrace.TrimEnd('\r', '\n'));
                }
            }
            return clipboard.ToString();
        }

        private static void PingContext(BetterConsoleEntry entry)
        {
            UnityEngine.Object context = BetterConsoleNativeBridge.ResolveContext(entry.contextInstanceId);
            if (context == null) return;
            EditorGUIUtility.PingObject(context);
            Selection.activeObject = context;
        }

        private static void OpenSource(BetterConsoleEntry entry)
        {
            if (entry == null) return;
            if (!string.IsNullOrEmpty(entry.file))
            {
                OpenSource(entry.file, entry.line, entry.column);
                return;
            }
            foreach (string frame in (entry.stackTrace ?? string.Empty).Split('\n'))
            {
                if (TryFrameSource(frame, out string file, out int line))
                {
                    OpenSource(file, line, 0);
                    return;
                }
            }
        }

        private static void OpenSource(string file, int line, int column)
        {
            if (string.IsNullOrEmpty(file)) return;
            string path = file.Replace('\\', '/');
            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            }
            InternalEditorUtility.OpenFileAtLineExternal(path, Mathf.Max(1, line), Mathf.Max(0, column));
        }

        private static bool TryFrameSource(string frame, out string file, out int line)
        {
            Match match = Regex.Match(frame ?? string.Empty, @"\(at (?<file>.*?):(?<line>\d+)(?::\d+)?\)");
            if (!match.Success)
            {
                match = Regex.Match(frame ?? string.Empty, @" in (?<file>.*?):line (?<line>\d+)");
            }
            file = match.Success ? match.Groups["file"].Value : string.Empty;
            line = match.Success && int.TryParse(match.Groups["line"].Value, out int parsed) ? parsed : 0;
            return match.Success;
        }

        private BetterConsoleSession PreviousComparableSession(BetterConsoleSession selected)
        {
            return BetterConsoleStore.Sessions
                .Where(item => item.kind == selected.kind && item.startUtcTicks < selected.startUtcTicks)
                .OrderByDescending(item => item.startUtcTicks)
                .FirstOrDefault();
        }

        private int CountForSeverity(BetterConsoleSeverity severity)
        {
            if (severity == BetterConsoleSeverity.Log) return BetterConsoleStore.Entries.Count(entry => entry.severity == BetterConsoleSeverity.Log);
            if (severity == BetterConsoleSeverity.Warning) return BetterConsoleStore.Entries.Count(entry => entry.severity == BetterConsoleSeverity.Warning);
            return BetterConsoleStore.Entries.Count(entry => entry.severity >= BetterConsoleSeverity.Error);
        }

        private bool SeverityAllowed(BetterConsoleSeverity severity)
        {
            if (severity == BetterConsoleSeverity.Log) return showLogs;
            if (severity == BetterConsoleSeverity.Warning) return showWarnings;
            return showErrors;
        }

        private void OnStoreChanged()
        {
            Invalidate();
            Repaint();
        }

        private void Invalidate()
        {
            cacheRevision = -1;
        }

        private void Flash(string message)
        {
            transientStatus = message;
            transientStatusUntil = EditorApplication.timeSinceStartup + 2d;
            Repaint();
        }

        private static string OneLine(string value)
        {
            return DisplayText(value).Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static string DisplayText(string value)
        {
            return RichTextRegex.Replace(value ?? string.Empty, string.Empty);
        }

        internal static BetterConsoleDetailPlacement CalculateDetailPlacement(
            Vector2 available,
            bool detailsEnabled,
            bool hasSelection)
        {
            if (!detailsEnabled || !hasSelection)
            {
                return BetterConsoleDetailPlacement.Hidden;
            }

            if (available.x >= 880f && available.y >= 160f)
            {
                return BetterConsoleDetailPlacement.Right;
            }

            return available.y >= 390f
                ? BetterConsoleDetailPlacement.Bottom
                : BetterConsoleDetailPlacement.Hidden;
        }

        internal static bool ShouldReleaseSearchFocus(
            Rect searchRect,
            Vector2 pointerPosition,
            bool searchFocused,
            EventType eventType)
        {
            return DansToolboxSearchField.ShouldReleaseFocus(
                searchRect,
                pointerPosition,
                searchFocused,
                eventType);
        }

        private static string CompactCount(int value)
        {
            if (value >= 1000000) return (value / 1000000f).ToString("0.#") + "m";
            if (value >= 1000) return (value / 1000f).ToString("0.#") + "k";
            return value.ToString();
        }

        private static string ShortSource(string value)
        {
            if (string.IsNullOrEmpty(value)) return "ED";
            return value.Length <= 3 ? value.ToUpperInvariant() : value.Substring(0, 3).ToUpperInvariant();
        }

        private static string StateGlyph(BetterConsoleTriage triage)
        {
            switch (triage)
            {
                case BetterConsoleTriage.Seen: return "SEEN";
                case BetterConsoleTriage.Acknowledged: return "ACK";
                case BetterConsoleTriage.Muted: return "MUTE";
                case BetterConsoleTriage.Resolved: return "DONE";
                default: return "NEW";
            }
        }

        private static string Rate(int count, float perMinute)
        {
            if (count <= 1) return "ONCE";
            if (perMinute >= 60f) return Mathf.RoundToInt(perMinute / 60f) + "/s";
            if (perMinute >= 1f) return Mathf.RoundToInt(perMinute) + "/m";
            return "<1/m";
        }

        private static string Duration(BetterConsoleSession session)
        {
            TimeSpan duration = session.EndUtc - session.StartUtc;
            return duration.TotalHours >= 1d
                ? duration.ToString(@"h\:mm\:ss")
                : duration.ToString(@"m\:ss");
        }

        private static string Delta(int current, int previous)
        {
            int delta = current - previous;
            return delta > 0 ? "+" + delta : delta.ToString();
        }

        private static string EntryMeta(BetterConsoleEntry entry)
        {
            string value = entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff") + "  ·  " + entry.category.ToString().ToUpperInvariant();
            if (!string.IsNullOrEmpty(entry.channel)) value += "  ·  " + entry.channel.ToUpperInvariant();
            if (entry.threadId != 0) value += "  ·  T" + entry.threadId;
            return value;
        }
    }

    internal sealed class BetterConsoleNamePopup : EditorWindow
    {
        private string heading = string.Empty;
        private string value = string.Empty;
        private Action<string> accepted;

        public static void Show(string heading, string initial, Action<string> accepted)
        {
            BetterConsoleNamePopup window = CreateInstance<BetterConsoleNamePopup>();
            window.heading = heading;
            window.value = initial;
            window.accepted = accepted;
            window.titleContent = new GUIContent(heading);
            window.minSize = window.maxSize = new Vector2(260f, 82f);
            window.ShowUtility();
            window.Focus();
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), DansToolboxTheme.Current.Canvas);
            GUI.Label(new Rect(10f, 7f, position.width - 20f, 18f), heading, BetterConsoleGui.Tiny);
            Rect fieldRect = new Rect(10f, 29f, position.width - 70f, 25f);
            EditorGUI.DrawRect(fieldRect, DansToolboxTheme.Current.Inset);
            BetterConsoleGui.Border(fieldRect, DansToolboxTheme.Current.Border);
            GUI.SetNextControlName("Name");
            value = GUI.TextField(new Rect(fieldRect.x + 6f, fieldRect.y + 2f, fieldRect.width - 12f, 21f), value, BetterConsoleGui.Field);
            if (BetterConsoleGui.Button(new Rect(position.width - 54f, 29f, 44f, 25f), new GUIContent("SAVE"))) Accept();
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                Accept();
                Event.current.Use();
            }
            else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
            }
            if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "Name")
            {
                EditorGUI.FocusTextInControl("Name");
            }
        }

        private void Accept()
        {
            accepted?.Invoke(string.IsNullOrWhiteSpace(value) ? "VIEW" : value.Trim());
            Close();
        }
    }
}
