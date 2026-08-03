using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal sealed class BetterHierarchyRulesWindow : EditorWindow
    {
        private const string SearchControlName = "BetterHierarchyRulesSearch";

        [Serializable]
        private sealed class RuleExport
        {
            public List<BetterHierarchyRule> rules = new List<BetterHierarchyRule>();
        }

        [SerializeField] private Vector2 scroll;
        [SerializeField] private string filter = string.Empty;
        private readonly HashSet<string> expanded = new HashSet<string>();
        [NonSerialized] private Rect lastSearchRect;

        internal static void Open()
        {
            BetterHierarchyRulesWindow window = GetWindow<BetterHierarchyRulesWindow>(true, "Hierarchy Rules", true);
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove) Repaint();
            if (DansToolboxSearchField.ReleaseFocusOnPointerDown(lastSearchRect, SearchControlName)) Repaint();
            DansToolboxPalette palette = DansToolboxTheme.Current;
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), palette.Canvas);
            DrawToolbar(palette);

            Rect content = new Rect(10f, 48f, position.width - 20f, position.height - 58f);
            GUILayout.BeginArea(content);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            List<BetterHierarchyRule> rules = BetterHierarchyProjectSettings.MutableRules;
            bool any = false;
            for (int index = 0; index < rules.Count; index++)
            {
                BetterHierarchyRule rule = rules[index];
                if (!string.IsNullOrWhiteSpace(filter) &&
                    rule.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    rule.Value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                any = true;
                DrawRule(rule, index, rules, palette);
                GUILayout.Space(6f);
            }

            if (!any)
            {
                GUILayout.Space(60f);
                GUILayout.Label("NO RULES", new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = palette.Muted }
                });
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawToolbar(DansToolboxPalette palette)
        {
            Rect bar = new Rect(0f, 0f, position.width, 40f);
            EditorGUI.DrawRect(bar, palette.Panel);
            float x = 8f;
            if (BetterHierarchyWindow.DrawFlatButton(new Rect(x, 8f, 30f, 24f), "+", "Add rule", true, palette))
            {
                BetterHierarchyProjectSettings.MutableRules.Add(new BetterHierarchyRule());
                BetterHierarchyProjectSettings.SaveNow();
            }
            x += 36f;
            if (BetterHierarchyWindow.DrawFlatButton(new Rect(x, 8f, 82f, 24f), "SELECTION", "Rule from selection", false, palette))
            {
                AddFromSelection();
            }
            x += 88f;
            if (BetterHierarchyWindow.DrawFlatButton(new Rect(x, 8f, 62f, 24f), "STARTER", "Reset starter rules", false, palette))
            {
                if (EditorUtility.DisplayDialog("Starter rules", "Replace all rules with the starter set?", "Replace", "Cancel"))
                {
                    BetterHierarchyProjectSettings.ResetRules();
                }
            }
            x += 68f;
            if (position.width > 630f)
            {
                if (BetterHierarchyWindow.DrawFlatButton(new Rect(x, 8f, 56f, 24f), "EXPORT", string.Empty, false, palette))
                {
                    ExportRules();
                }
                x += 62f;
                if (BetterHierarchyWindow.DrawFlatButton(new Rect(x, 8f, 56f, 24f), "IMPORT", string.Empty, false, palette))
                {
                    ImportRules();
                }
                x += 62f;
            }

            lastSearchRect = new Rect(x, 9f, Mathf.Max(80f, position.width - x - 8f), DansToolboxSearchField.Height);
            filter = DansToolboxSearchField.Draw(
                lastSearchRect,
                filter,
                SearchControlName,
                "Search rules");
        }

        private void DrawRule(
            BetterHierarchyRule rule,
            int index,
            List<BetterHierarchyRule> rules,
            DansToolboxPalette palette)
        {
            bool isExpanded = expanded.Contains(rule.Id);
            float height = isExpanded ? 224f : 42f;
            Rect card = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(card, rule.Enabled ? palette.BorderStrong : palette.Border);
            EditorGUI.DrawRect(new Rect(card.x + 1f, card.y + 1f, card.width - 2f, card.height - 2f), palette.Panel);
            EditorGUI.DrawRect(new Rect(card.x + 1f, card.y + 1f, 4f, card.height - 2f),
                new Color(rule.Color.r, rule.Color.g, rule.Color.b, Mathf.Max(0.7f, rule.Color.a)));

            EditorGUI.BeginChangeCheck();
            rule.Enabled = EditorGUI.Toggle(new Rect(card.x + 12f, card.y + 12f, 18f, 18f), rule.Enabled);
            rule.Name = EditorGUI.TextField(new Rect(card.x + 36f, card.y + 9f, Mathf.Max(80f, card.width - 250f), 24f), rule.Name);
            GUI.Label(new Rect(card.xMax - 200f, card.y + 9f, 96f, 24f), rule.Match.ToString().ToUpperInvariant(),
                new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    clipping = TextClipping.Clip,
                    normal = { textColor = palette.Muted }
                });

            if (BetterHierarchyWindow.DrawIconButton(new Rect(card.xMax - 96f, card.y + 9f, 22f, 22f), "↑", "Move up", index > 0, palette))
            {
                rules.RemoveAt(index);
                rules.Insert(index - 1, rule);
                BetterHierarchyProjectSettings.SaveNow();
                GUIUtility.ExitGUI();
            }
            if (BetterHierarchyWindow.DrawIconButton(new Rect(card.xMax - 72f, card.y + 9f, 22f, 22f), "↓", "Move down", index < rules.Count - 1, palette))
            {
                rules.RemoveAt(index);
                rules.Insert(index + 1, rule);
                BetterHierarchyProjectSettings.SaveNow();
                GUIUtility.ExitGUI();
            }
            if (BetterHierarchyWindow.DrawIconButton(new Rect(card.xMax - 48f, card.y + 9f, 22f, 22f), isExpanded ? "−" : "+", "Details", true, palette))
            {
                if (!expanded.Remove(rule.Id)) expanded.Add(rule.Id);
                Repaint();
            }
            if (BetterHierarchyWindow.DrawIconButton(new Rect(card.xMax - 24f, card.y + 9f, 18f, 22f), "×", "Delete", true, palette))
            {
                rules.Remove(rule);
                BetterHierarchyProjectSettings.SaveNow();
                GUIUtility.ExitGUI();
            }

            if (isExpanded)
            {
                float y = card.y + 48f;
                DrawLabel(card.x + 14f, y, "MATCH", palette);
                rule.Match = (BetterHierarchyRuleMatch)EditorGUI.EnumPopup(
                    new Rect(card.x + 76f, y - 2f, 154f, 22f), rule.Match);
                DrawLabel(card.x + 246f, y, "VALUE", palette);
                rule.Value = EditorGUI.TextField(new Rect(card.x + 300f, y - 2f, card.width - 316f, 22f), rule.Value);

                y += 32f;
                DrawLabel(card.x + 14f, y, "COLOR", palette);
                rule.Color = EditorGUI.ColorField(new Rect(card.x + 76f, y - 2f, 154f, 22f), GUIContent.none,
                    rule.Color, false, true, false);
                DrawLabel(card.x + 246f, y, "BADGE", palette);
                rule.Badge = EditorGUI.TextField(new Rect(card.x + 300f, y - 2f, 70f, 22f), rule.Badge);
                DrawLabel(card.x + 384f, y, "ICON", palette);
                rule.IconName = EditorGUI.TextField(new Rect(card.x + 424f, y - 2f, card.width - 470f, 22f), rule.IconName);
                if (BetterHierarchyWindow.DrawIconButton(
                        new Rect(card.xMax - 38f, y - 2f, 24f, 22f),
                        "...",
                        "Choose icon",
                        true,
                        palette))
                {
                    ShowIconMenu(rule);
                }

                y += 32f;
                DrawLabel(card.x + 14f, y, "PRIORITY", palette);
                rule.Priority = EditorGUI.IntField(new Rect(card.x + 76f, y - 2f, 64f, 22f), rule.Priority);
                rule.Recursive = EditorGUI.ToggleLeft(new Rect(card.x + 160f, y - 2f, 90f, 22f), "Recursive", rule.Recursive);
                rule.Header = EditorGUI.ToggleLeft(new Rect(card.x + 256f, y - 2f, 72f, 22f), "Header", rule.Header);
                rule.Bold = EditorGUI.ToggleLeft(new Rect(card.x + 334f, y - 2f, 62f, 22f), "Bold", rule.Bold);
                rule.OverrideTextColor = EditorGUI.ToggleLeft(new Rect(card.x + 402f, y - 2f, 84f, 22f), "Text", rule.OverrideTextColor);
                if (rule.OverrideTextColor)
                {
                    rule.TextColor = EditorGUI.ColorField(new Rect(card.x + 490f, y - 2f, 56f, 22f), GUIContent.none,
                        rule.TextColor, false, false, false);
                }

                y += 38f;
                GameObject selected = Selection.activeGameObject;
                bool matches = selected != null && BetterHierarchyRuleMatcher.Matches(
                    rule,
                    selected,
                    BetterHierarchyDiagnostics.Get(selected, true));
                string preview = selected == null ? "SELECT AN OBJECT TO TEST" : matches ? "MATCH" : "NO MATCH";
                Color previewColor = matches ? palette.Success : palette.Muted;
                GUI.Label(new Rect(card.x + 14f, y, 190f, 22f), preview,
                    new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = previewColor } });
                if (BetterHierarchyWindow.DrawFlatButton(new Rect(card.xMax - 132f, y - 2f, 58f, 24f), "COPY", string.Empty, false, palette))
                {
                    rules.Insert(index + 1, rule.Clone());
                    BetterHierarchyProjectSettings.SaveNow();
                    GUIUtility.ExitGUI();
                }
                if (BetterHierarchyWindow.DrawFlatButton(new Rect(card.xMax - 68f, y - 2f, 54f, 24f), "PING", string.Empty, false, palette) && selected != null)
                {
                    EditorGUIUtility.PingObject(selected);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                BetterHierarchyProjectSettings.SaveNow();
            }
        }

        private static void DrawLabel(float x, float y, string label, DansToolboxPalette palette)
        {
            GUI.Label(new Rect(x, y, 58f, 18f), label,
                new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    fontSize = 8,
                    normal = { textColor = palette.Muted }
                });
        }

        private static void ShowIconMenu(BetterHierarchyRule rule)
        {
            GenericMenu menu = new GenericMenu();
            AddIcon(menu, rule, "None", string.Empty);
            menu.AddSeparator(string.Empty);
            AddIcon(menu, rule, "Camera", "Camera Icon");
            AddIcon(menu, rule, "Light", "Light Icon");
            AddIcon(menu, rule, "Audio", "AudioSource Icon");
            AddIcon(menu, rule, "UI", "Canvas Icon");
            AddIcon(menu, rule, "Prefab", "Prefab Icon");
            AddIcon(menu, rule, "Settings", "Settings");
            AddIcon(menu, rule, "Warning", "console.warnicon");
            AddIcon(menu, rule, "Error", "console.erroricon");
            AddIcon(menu, rule, "Favorite", "Favorite");
            menu.ShowAsContext();
        }

        private static void AddIcon(GenericMenu menu, BetterHierarchyRule rule, string label, string iconName)
        {
            menu.AddItem(new GUIContent(label), string.Equals(rule.IconName, iconName, StringComparison.Ordinal), () =>
            {
                rule.IconName = iconName;
                BetterHierarchyProjectSettings.SaveNow();
            });
        }

        private static void AddFromSelection()
        {
            GameObject gameObject = Selection.activeGameObject;
            if (gameObject == null)
            {
                return;
            }

            Component component = gameObject.GetComponents<Component>()
                .FirstOrDefault(candidate => candidate != null && !(candidate is Transform));
            BetterHierarchyRule rule = new BetterHierarchyRule
            {
                Name = component != null ? component.GetType().Name : gameObject.name,
                Match = component != null ? BetterHierarchyRuleMatch.HasComponent : BetterHierarchyRuleMatch.Object,
                Value = component != null ? component.GetType().FullName : BetterHierarchyObjectIds.Get(gameObject),
                Color = new Color(
                    DansToolboxTheme.Current.Accent.r,
                    DansToolboxTheme.Current.Accent.g,
                    DansToolboxTheme.Current.Accent.b,
                    0.22f),
                Badge = component != null ? component.GetType().Name.Substring(0, Mathf.Min(3, component.GetType().Name.Length)).ToUpperInvariant() : "PIN",
                Priority = 200
            };
            BetterHierarchyProjectSettings.MutableRules.Add(rule);
            BetterHierarchyProjectSettings.SaveNow();
        }

        private static void ExportRules()
        {
            string path = EditorUtility.SaveFilePanel("Export hierarchy rules", string.Empty, "BetterHierarchyRules.json", "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            RuleExport export = new RuleExport { rules = BetterHierarchyProjectSettings.Rules.ToList() };
            File.WriteAllText(path, JsonUtility.ToJson(export, true));
        }

        private static void ImportRules()
        {
            string path = EditorUtility.OpenFilePanel("Import hierarchy rules", string.Empty, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                RuleExport import = JsonUtility.FromJson<RuleExport>(File.ReadAllText(path));
                if (import?.rules == null)
                {
                    throw new InvalidDataException("No rules were found.");
                }

                List<BetterHierarchyRule> rules = BetterHierarchyProjectSettings.MutableRules;
                rules.Clear();
                rules.AddRange(import.rules);
                BetterHierarchyProjectSettings.SaveNow();
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Import failed", exception.Message, "OK");
            }
        }
    }
}
