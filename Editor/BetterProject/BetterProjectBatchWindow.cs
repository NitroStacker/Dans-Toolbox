using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterProject
{
    internal sealed class BetterProjectBatchWindow : EditorWindow
    {
        [SerializeField] private List<string> guids = new List<string>();
        [SerializeField] private string find = string.Empty;
        [SerializeField] private string replace = string.Empty;
        [SerializeField] private string prefix = string.Empty;
        [SerializeField] private string suffix = string.Empty;
        [SerializeField] private string labels = string.Empty;
        [SerializeField] private string destination = string.Empty;
        [SerializeField] private Preset importerPreset;
        [SerializeField] private Vector2 scroll;
        [NonSerialized] private double nextHoverUpdateAt;

        internal static void Open(IEnumerable<BetterProjectAssetRecord> selected, string currentFolder)
        {
            BetterProjectBatchWindow window = GetWindow<BetterProjectBatchWindow>(true, "Batch");
            window.guids = (selected ?? Enumerable.Empty<BetterProjectAssetRecord>())
                .Where(record => record != null && !record.IsReadOnly)
                .Select(record => record.Guid)
                .Distinct()
                .ToList();
            window.destination = currentFolder ?? "Assets";
            window.minSize = new Vector2(520f, 500f);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), BetterProjectGui.Canvas);
            GUILayout.BeginArea(new Rect(14f, 14f, position.width - 28f, position.height - 28f));
            GUILayout.Label(guids.Count + " SELECTED", BetterProjectGui.CardTitle);
            GUILayout.Space(8f);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawSection("RENAME");
            find = EditorGUILayout.TextField("Find", find);
            replace = EditorGUILayout.TextField("Replace", replace);
            prefix = EditorGUILayout.TextField("Prefix", prefix);
            suffix = EditorGUILayout.TextField("Suffix", suffix);
            DrawRenamePreview();
            if (GUILayout.Button("APPLY NAMES", BetterProjectGui.SegmentActive, GUILayout.Height(28f)))
            {
                ApplyRename();
            }

            GUILayout.Space(12f);
            DrawSection("LABELS");
            labels = EditorGUILayout.TextField("Comma separated", labels);
            if (GUILayout.Button("SET LABELS", BetterProjectGui.Segment, GUILayout.Height(26f)))
            {
                BetterProjectOperations.SetLabels(Resolve(), labels.Split(','));
            }

            GUILayout.Space(12f);
            DrawSection("MOVE");
            destination = EditorGUILayout.TextField("Folder", destination);
            if (GUILayout.Button("MOVE", BetterProjectGui.Segment, GUILayout.Height(26f)))
            {
                BetterProjectOperations.Move(Resolve().Select(record => record.Path), destination);
            }

            GUILayout.Space(12f);
            DrawSection("IMPORT");
            importerPreset = (Preset)EditorGUILayout.ObjectField("Preset", importerPreset, typeof(Preset), false);
            if (GUILayout.Button("APPLY PRESET", BetterProjectGui.Segment, GUILayout.Height(26f)))
            {
                int count = BetterProjectOperations.ApplyPreset(Resolve(), importerPreset);
                ShowNotification(new GUIContent(count + " applied"));
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            if (Event.current.type == EventType.MouseMove)
            {
                double now = EditorApplication.timeSinceStartup;
                if (BetterProjectWindow.ShouldProcessHoverUpdate(now, ref nextHoverUpdateAt)) Repaint();
            }
        }

        private void DrawRenamePreview()
        {
            foreach (BetterProjectAssetRecord record in Resolve().Take(6))
            {
                string next = BuildName(record.Name);
                GUILayout.Label(record.Name + "  →  " + next, BetterProjectGui.Muted);
            }
        }

        private void ApplyRename()
        {
            BetterProjectAssetRecord[] assets = Resolve().ToArray();
            if (!EditorUtility.DisplayDialog("Apply names?", assets.Length + " assets", "Apply", "Cancel"))
            {
                return;
            }
            foreach (BetterProjectAssetRecord record in assets)
            {
                BetterProjectOperations.Rename(record, BuildName(record.Name));
            }
            AssetDatabase.Refresh();
        }

        private string BuildName(string original)
        {
            string value = string.IsNullOrEmpty(find)
                ? original
                : original.Replace(find, replace ?? string.Empty);
            return prefix + value + suffix;
        }

        private IEnumerable<BetterProjectAssetRecord> Resolve()
        {
            return guids.Select(BetterProjectIndex.GetByGuid).Where(record => record != null);
        }

        private static void DrawSection(string label)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 26f);
            EditorGUI.DrawRect(rect, BetterProjectGui.Panel);
            GUI.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height), label, BetterProjectGui.CardTitle);
        }
    }
}
