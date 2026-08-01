using System;
using System.Linq;
using DansToolbox.Editor;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal sealed class BetterHierarchyComponentPopup : PopupWindowContent
    {
        private readonly Component component;
        private UnityEditor.Editor editor;
        private Vector2 scroll;

        internal BetterHierarchyComponentPopup(Component component)
        {
            this.component = component;
        }

        public override Vector2 GetWindowSize() => new Vector2(360f, 440f);

        public override void OnOpen()
        {
            if (component != null)
            {
                editor = UnityEditor.Editor.CreateEditor(component);
            }
        }

        public override void OnClose()
        {
            if (editor != null)
            {
                UnityEngine.Object.DestroyImmediate(editor);
                editor = null;
            }
        }

        public override void OnGUI(Rect rect)
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            EditorGUI.DrawRect(rect, palette.Canvas);
            if (component == null || editor == null)
            {
                GUI.Label(rect, "COMPONENT UNAVAILABLE", Centered(palette.Muted));
                return;
            }

            Rect header = new Rect(0f, 0f, rect.width, 38f);
            EditorGUI.DrawRect(header, palette.Panel);
            Texture icon = EditorGUIUtility.ObjectContent(null, component.GetType()).image;
            GUI.Label(new Rect(10f, 8f, 22f, 22f), new GUIContent(icon));
            GUI.Label(new Rect(36f, 7f, rect.width - 78f, 24f), component.GetType().Name.ToUpperInvariant(),
                new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    clipping = TextClipping.Clip,
                    normal = { textColor = palette.Text }
                });
            if (BetterHierarchyWindow.DrawIconButton(new Rect(rect.width - 32f, 8f, 22f, 22f), "◎", "Ping", true, palette))
            {
                EditorGUIUtility.PingObject(component);
            }

            Rect body = new Rect(8f, 46f, rect.width - 16f, rect.height - 54f);
            GUILayout.BeginArea(body);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            editor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static GUIStyle Centered(Color color)
        {
            return new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = color }
            };
        }
    }

    internal sealed class BetterHierarchyCollectionPopup : PopupWindowContent
    {
        private readonly GameObject[] members;
        private readonly BetterHierarchyCollection editing;
        private readonly Action completed;
        private string collectionName;
        private Color collectionColor;
        private bool virtualCollection;

        internal BetterHierarchyCollectionPopup(
            GameObject[] members,
            bool virtualByDefault,
            Action completed)
        {
            this.members = members?.Where(gameObject => gameObject != null).Distinct().ToArray() ?? Array.Empty<GameObject>();
            this.completed = completed;
            collectionName = "Collection";
            collectionColor = DansToolboxTheme.Current.Accent;
            virtualCollection = virtualByDefault;
        }

        internal BetterHierarchyCollectionPopup(BetterHierarchyCollection editing, Action completed)
        {
            this.editing = editing;
            this.completed = completed;
            members = Array.Empty<GameObject>();
            collectionName = editing?.Name ?? "Collection";
            collectionColor = editing?.Color ?? DansToolboxTheme.Current.Accent;
            virtualCollection = true;
        }

        public override Vector2 GetWindowSize() => new Vector2(340f, editing == null ? 258f : 170f);

        public override void OnGUI(Rect rect)
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            EditorGUI.DrawRect(rect, palette.Canvas);
            GUI.Label(new Rect(14f, 12f, rect.width - 28f, 22f),
                editing == null ? "NEW COLLECTION" : "EDIT COLLECTION",
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = palette.Text } });

            GUI.Label(new Rect(14f, 44f, 54f, 18f), "NAME",
                new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = palette.Muted } });
            collectionName = EditorGUI.TextField(new Rect(72f, 41f, rect.width - 86f, 23f), collectionName);
            GUI.Label(new Rect(14f, 76f, 54f, 18f), "COLOR",
                new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = palette.Muted } });
            collectionColor = EditorGUI.ColorField(
                new Rect(72f, 73f, rect.width - 86f, 23f),
                GUIContent.none,
                collectionColor,
                false,
                false,
                false);

            if (editing == null)
            {
                Rect virtualRect = new Rect(14f, 108f, (rect.width - 36f) * 0.5f, 64f);
                Rect parentRect = new Rect(virtualRect.xMax + 8f, 108f, virtualRect.width, 64f);
                DrawChoice(virtualRect, "VIRTUAL", "Organize only", virtualCollection, palette);
                DrawChoice(parentRect, "PARENT", "Moves together", !virtualCollection, palette);
                if (GUI.Button(virtualRect, GUIContent.none, GUIStyle.none)) virtualCollection = true;
                if (GUI.Button(parentRect, GUIContent.none, GUIStyle.none)) virtualCollection = false;

                string scope = members.Length == 0 ? "EMPTY" : members.Length + " OBJECT" + (members.Length == 1 ? string.Empty : "S");
                GUI.Label(new Rect(14f, 184f, 110f, 20f), scope,
                    new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = palette.Muted } });
            }

            float buttonY = rect.height - 42f;
            bool canApply = !string.IsNullOrWhiteSpace(collectionName) &&
                            (editing != null || virtualCollection || members.Length > 0);
            EditorGUI.BeginDisabledGroup(!canApply);
            if (BetterHierarchyWindow.DrawFlatButton(
                    new Rect(rect.width - 104f, buttonY, 90f, 28f),
                    editing == null ? "CREATE" : "SAVE",
                    string.Empty,
                    true,
                    palette))
            {
                Apply();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void Apply()
        {
            if (editing != null)
            {
                editing.Name = collectionName.Trim();
                editing.Color = collectionColor;
                BetterHierarchyProjectSettings.SaveNow();
            }
            else if (virtualCollection)
            {
                BetterHierarchyCollections.CreateVirtual(collectionName, collectionColor, members);
            }
            else
            {
                BetterHierarchyCollections.CreateTransformParent(collectionName, members, collectionColor);
            }

            completed?.Invoke();
            editorWindow.Close();
        }

        private static void DrawChoice(
            Rect rect,
            string title,
            string subtitle,
            bool selected,
            DansToolboxPalette palette)
        {
            EditorGUI.DrawRect(rect, selected ? palette.Accent : palette.Border);
            EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f),
                selected ? palette.AccentSoft : palette.Panel);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 10f, rect.width - 16f, 18f), title,
                new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = selected ? palette.Text : palette.Muted }
                });
            GUI.Label(new Rect(rect.x + 8f, rect.y + 32f, rect.width - 16f, 16f), subtitle,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = palette.Muted }
                });
        }
    }

    internal sealed class BetterHierarchyDeleteCollectionPopup : PopupWindowContent
    {
        private readonly BetterHierarchyCollection virtualCollection;
        private readonly GameObject transformCollection;
        private readonly Action completed;

        internal BetterHierarchyDeleteCollectionPopup(
            BetterHierarchyCollection collection,
            Action completed)
        {
            virtualCollection = collection;
            this.completed = completed;
        }

        internal BetterHierarchyDeleteCollectionPopup(
            GameObject collectionParent,
            Action completed)
        {
            transformCollection = collectionParent;
            this.completed = completed;
        }

        public override Vector2 GetWindowSize() => new Vector2(360f, 210f);

        public override void OnGUI(Rect rect)
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            EditorGUI.DrawRect(rect, palette.Canvas);

            string collectionName = virtualCollection?.Name ??
                                    (transformCollection != null ? transformCollection.name : "Collection");
            int itemCount = virtualCollection != null
                ? BetterHierarchyCollections.Resolve(virtualCollection).Count
                : transformCollection != null ? transformCollection.transform.childCount : 0;

            GUI.Label(
                new Rect(14f, 12f, rect.width - 28f, 22f),
                "DELETE COLLECTION",
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = palette.Text } });
            GUI.Label(
                new Rect(14f, 37f, rect.width - 28f, 18f),
                collectionName + "  ·  " + itemCount + " ITEM" + (itemCount == 1 ? string.Empty : "S"),
                new GUIStyle(EditorStyles.miniLabel)
                {
                    clipping = TextClipping.Clip,
                    normal = { textColor = palette.Muted }
                });

            Rect keepRect = new Rect(14f, 68f, (rect.width - 36f) * 0.5f, 72f);
            Rect deleteRect = new Rect(keepRect.xMax + 8f, keepRect.y, keepRect.width, keepRect.height);
            DrawDeleteChoice(
                keepRect,
                virtualCollection != null ? "KEEP ITEMS" : "MOVE OUT",
                virtualCollection != null ? "Remove collection only" : "Preserve all items",
                palette.Accent,
                palette);
            DrawDeleteChoice(
                deleteRect,
                "DELETE ALL",
                "Collection + items",
                palette.Danger,
                palette);

            if (GUI.Button(keepRect, GUIContent.none, GUIStyle.none))
            {
                Apply(deleteItems: false);
            }
            if (GUI.Button(deleteRect, GUIContent.none, GUIStyle.none))
            {
                Apply(deleteItems: true);
            }
            if (BetterHierarchyWindow.DrawFlatButton(
                    new Rect(rect.width - 94f, rect.height - 42f, 80f, 28f),
                    "CANCEL",
                    "Keep collection",
                    false,
                    palette))
            {
                editorWindow.Close();
            }
        }

        private void Apply(bool deleteItems)
        {
            if (virtualCollection != null)
            {
                BetterHierarchyCollections.DeleteVirtualCollection(virtualCollection, deleteItems);
            }
            else if (transformCollection != null)
            {
                BetterHierarchyCollections.DeleteTransformCollection(transformCollection, deleteItems);
            }

            completed?.Invoke();
            editorWindow.Close();
        }

        private static void DrawDeleteChoice(
            Rect rect,
            string title,
            string subtitle,
            Color accent,
            DansToolboxPalette palette)
        {
            EditorGUI.DrawRect(rect, accent);
            EditorGUI.DrawRect(
                new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f),
                palette.Panel);
            EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, 3f, rect.height - 2f), accent);
            GUI.Label(
                new Rect(rect.x + 10f, rect.y + 13f, rect.width - 20f, 18f),
                title,
                new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = accent }
                });
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 39f, rect.width - 16f, 16f),
                subtitle,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = palette.Muted }
                });
        }
    }

    internal sealed class BetterHierarchySettingsPopup : PopupWindowContent
    {
        private readonly Action changed;

        internal BetterHierarchySettingsPopup(Action changed)
        {
            this.changed = changed;
        }

        public override Vector2 GetWindowSize() => new Vector2(300f, 330f);

        public override void OnGUI(Rect rect)
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            EditorGUI.DrawRect(rect, palette.Canvas);
            GUILayout.BeginArea(new Rect(12f, 12f, rect.width - 24f, rect.height - 24f));
            GUILayout.Label("VIEW", new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = palette.Text } });
            GUILayout.Space(6f);

            EditorGUI.BeginChangeCheck();
            BetterHierarchyMode mode = (BetterHierarchyMode)EditorGUILayout.EnumPopup(BetterHierarchyUserSettings.Mode);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyMode(mode);
            }

            GUILayout.Space(10f);
            DrawToggle("TREE LINES", BetterHierarchyUserSettings.TreeLines, value => BetterHierarchyUserSettings.TreeLines = value, palette);
            DrawToggle("ROW SHADING", BetterHierarchyUserSettings.Zebra, value => BetterHierarchyUserSettings.Zebra = value, palette);
            DrawToggle("COMPONENTS", BetterHierarchyUserSettings.Components, value => BetterHierarchyUserSettings.Components = value, palette);
            DrawToggle("QUICK ACTIONS", BetterHierarchyUserSettings.QuickActions, value => BetterHierarchyUserSettings.QuickActions = value, palette);
            DrawToggle("DIAGNOSTICS", BetterHierarchyUserSettings.Diagnostics, value => BetterHierarchyUserSettings.Diagnostics = value, palette);
            DrawToggle("CHILD COUNTS", BetterHierarchyUserSettings.ChildCounts, value => BetterHierarchyUserSettings.ChildCounts = value, palette);

            GUILayout.Space(10f);
            GUILayout.Label("DENSITY", new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = palette.Muted } });
            float rowHeight = EditorGUILayout.Slider(BetterHierarchyUserSettings.RowHeight, 18f, 30f);
            if (!Mathf.Approximately(rowHeight, BetterHierarchyUserSettings.RowHeight))
            {
                BetterHierarchyUserSettings.RowHeight = rowHeight;
                BetterHierarchyUserSettings.Mode = BetterHierarchyMode.Custom;
                changed?.Invoke();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("RULES", GUILayout.Height(26f)))
            {
                BetterHierarchyRulesWindow.Open();
                editorWindow.Close();
            }
            GUILayout.EndArea();
        }

        private void DrawToggle(string label, bool value, Action<bool> setter, DansToolboxPalette palette)
        {
            Rect row = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(row, value ? palette.AccentSoft : palette.Inset);
            GUI.Label(new Rect(row.x + 8f, row.y, row.width - 44f, row.height), label,
                new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    normal = { textColor = value ? palette.Text : palette.Muted }
                });
            Rect mark = new Rect(row.xMax - 30f, row.y + 6f, 20f, 16f);
            EditorGUI.DrawRect(mark, value ? palette.Accent : palette.Border);
            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                setter(!value);
                BetterHierarchyUserSettings.Mode = BetterHierarchyMode.Custom;
                changed?.Invoke();
            }
        }

        private void ApplyMode(BetterHierarchyMode mode)
        {
            BetterHierarchyUserSettings.Mode = mode;
            switch (mode)
            {
                case BetterHierarchyMode.Clean:
                    Set(true, false, false, true, false, false, 20f);
                    break;
                case BetterHierarchyMode.Debug:
                    Set(true, true, true, true, true, true, 24f);
                    break;
                case BetterHierarchyMode.Art:
                    Set(true, true, true, true, false, false, 26f);
                    break;
                case BetterHierarchyMode.LevelDesign:
                    Set(true, true, false, true, true, true, 23f);
                    break;
                case BetterHierarchyMode.Production:
                    Set(true, true, true, true, true, true, 22f);
                    break;
            }
            changed?.Invoke();
        }

        private static void Set(
            bool lines,
            bool zebra,
            bool components,
            bool actions,
            bool diagnostics,
            bool counts,
            float rowHeight)
        {
            BetterHierarchyUserSettings.TreeLines = lines;
            BetterHierarchyUserSettings.Zebra = zebra;
            BetterHierarchyUserSettings.Components = components;
            BetterHierarchyUserSettings.QuickActions = actions;
            BetterHierarchyUserSettings.Diagnostics = diagnostics;
            BetterHierarchyUserSettings.ChildCounts = counts;
            BetterHierarchyUserSettings.RowHeight = rowHeight;
        }
    }
}
