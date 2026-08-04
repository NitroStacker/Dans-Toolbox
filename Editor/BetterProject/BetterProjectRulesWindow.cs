using System;
using System.Linq;
using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterProject
{
    internal sealed class BetterProjectRulesWindow : EditorWindow
    {
        [SerializeField] private Vector2 scroll;
        [NonSerialized] private double nextHoverUpdateAt;

        internal static void Open()
        {
            BetterProjectRulesWindow window = GetWindow<BetterProjectRulesWindow>(true, "Asset Rules");
            window.minSize = new Vector2(540f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), BetterProjectGui.Canvas);
            Rect header = new Rect(12f, 12f, position.width - 24f, 34f);
            BetterProjectGui.DrawPanel(header, BetterProjectGui.Panel);
            GUI.Label(new Rect(header.x + 10f, header.y, 120f, header.height), "ASSET RULES", BetterProjectGui.CardTitle);
            if (GUI.Button(new Rect(header.xMax - 78f, header.y + 5f, 68f, 24f), "+ RULE", BetterProjectGui.SegmentActive))
            {
                BetterProjectSettings.RecordUndo("Add Better Project Rule");
                BetterProjectSettings.Rules.Add(new BetterProjectStyleRule
                {
                    Name = "New Rule",
                    Match = BetterProjectRuleMatch.PathStartsWith,
                    Value = "Assets/",
                    Priority = 10
                });
                BetterProjectSettings.SaveNow();
            }

            GUILayout.BeginArea(new Rect(12f, 56f, position.width - 24f, position.height - 68f));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (BetterProjectStyleRule rule in BetterProjectSettings.Rules
                         .OrderByDescending(item => item.Priority).ToArray())
            {
                DrawRule(rule);
                GUILayout.Space(6f);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            if (Event.current.type == EventType.MouseMove)
            {
                double now = EditorApplication.timeSinceStartup;
                if (BetterProjectWindow.ShouldProcessHoverUpdate(now, ref nextHoverUpdateAt)) Repaint();
            }
        }

        private void DrawRule(BetterProjectStyleRule rule)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 104f);
            BetterProjectGui.DrawPanel(rect, BetterProjectGui.Panel);
            Rect content = new Rect(rect.x + 10f, rect.y + 9f, rect.width - 20f, rect.height - 18f);

            EditorGUI.BeginChangeCheck();
            rule.Enabled = EditorGUI.Toggle(new Rect(content.x, content.y, 18f, 20f), rule.Enabled);
            rule.Name = EditorGUI.TextField(new Rect(content.x + 24f, content.y, 170f, 20f), rule.Name);
            rule.Color = EditorGUI.ColorField(new Rect(content.x + 200f, content.y, 76f, 20f), GUIContent.none, rule.Color, false, true, false);
            rule.Badge = EditorGUI.TextField(new Rect(content.x + 282f, content.y, 64f, 20f), rule.Badge);
            rule.Priority = EditorGUI.IntField(new Rect(content.xMax - 88f, content.y, 40f, 20f), rule.Priority);
            if (GUI.Button(new Rect(content.xMax - 40f, content.y, 40f, 20f), "×", BetterProjectGui.Segment))
            {
                BetterProjectSettings.RecordUndo("Delete Better Project Rule");
                BetterProjectSettings.Rules.Remove(rule);
                BetterProjectSettings.SaveNow();
                GUIUtility.ExitGUI();
            }

            rule.Match = (BetterProjectRuleMatch)EditorGUI.EnumPopup(
                new Rect(content.x + 24f, content.y + 31f, 160f, 20f), rule.Match);
            rule.Value = EditorGUI.TextField(new Rect(content.x + 190f, content.y + 31f, content.width - 190f, 20f), rule.Value);
            rule.IconName = EditorGUI.TextField(new Rect(content.x + 24f, content.y + 62f, content.width - 24f, 20f),
                new GUIContent("Icon", "Unity built-in icon name; leave empty to use the asset icon"), rule.IconName);
            if (EditorGUI.EndChangeCheck())
            {
                BetterProjectSettings.RecordUndo("Edit Better Project Rule");
                BetterProjectSettings.SaveNow();
            }
        }
    }
}
