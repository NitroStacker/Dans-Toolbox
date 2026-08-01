using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DansToolbox.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal sealed class BetterHierarchyBatchWindow : EditorWindow
    {
        private enum BatchMode
        {
            Rename,
            State,
            Organize,
            Replace,
            Component
        }

        [SerializeField] private List<string> targetIds = new List<string>();
        [SerializeField] private BatchMode mode;
        [SerializeField] private bool includeChildren;

        [SerializeField] private string find = string.Empty;
        [SerializeField] private string replace = string.Empty;
        [SerializeField] private string prefix = string.Empty;
        [SerializeField] private string suffix = string.Empty;
        [SerializeField] private bool regex;
        [SerializeField] private bool number;
        [SerializeField] private int numberStart = 1;

        [SerializeField] private int activeState = -1;
        [SerializeField] private int staticState = -1;
        [SerializeField] private bool applyLayer;
        [SerializeField] private int layer = -1;
        [SerializeField] private bool applyTag;
        [SerializeField] private string tagName = string.Empty;

        [SerializeField] private string collectionName = "Collection";
        [SerializeField] private Color collectionColor = new Color(1f, 0.55f, 0.12f, 0.8f);
        [SerializeField] private GameObject replacementPrefab;
        [SerializeField] private Component sourceComponent;

        internal static void Open(IEnumerable<GameObject> targets)
        {
            BetterHierarchyBatchWindow window = GetWindow<BetterHierarchyBatchWindow>(true, "Batch", true);
            window.targetIds = targets?
                .Where(gameObject => gameObject != null)
                .Distinct()
                .Select(BetterHierarchyObjectIds.Get)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList() ?? new List<string>();
            window.collectionColor = DansToolboxTheme.Current.Accent;
            window.minSize = new Vector2(460f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            DansToolboxPalette palette = DansToolboxTheme.Current;
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), palette.Canvas);
            DrawModeBar(palette);

            Rect body = new Rect(16f, 56f, position.width - 32f, position.height - 118f);
            GUILayout.BeginArea(body);
            GUILayout.Label(GetTargets().Count + " OBJECTS", new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = palette.Accent }
            });
            includeChildren = EditorGUILayout.ToggleLeft("Include children", includeChildren);
            GUILayout.Space(10f);

            switch (mode)
            {
                case BatchMode.State:
                    DrawState();
                    break;
                case BatchMode.Organize:
                    DrawOrganize(palette);
                    break;
                case BatchMode.Replace:
                    DrawReplace();
                    break;
                case BatchMode.Component:
                    DrawComponent();
                    break;
                default:
                    DrawRename();
                    break;
            }
            GUILayout.EndArea();

            Rect footer = new Rect(0f, position.height - 50f, position.width, 50f);
            EditorGUI.DrawRect(footer, palette.Panel);
            bool enabled = CanApply();
            EditorGUI.BeginDisabledGroup(!enabled);
            if (BetterHierarchyWindow.DrawFlatButton(
                    new Rect(footer.xMax - 110f, footer.y + 11f, 94f, 28f),
                    "APPLY",
                    string.Empty,
                    true,
                    palette))
            {
                Apply();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawModeBar(DansToolboxPalette palette)
        {
            Rect bar = new Rect(0f, 0f, position.width, 42f);
            EditorGUI.DrawRect(bar, palette.Panel);
            float x = 8f;
            foreach (BatchMode candidate in Enum.GetValues(typeof(BatchMode)))
            {
                string label = candidate == BatchMode.Component ? "COPY" : candidate.ToString().ToUpperInvariant();
                float width = label.Length * 7f + 16f;
                if (BetterHierarchyWindow.DrawFlatButton(
                        new Rect(x, 8f, width, 26f),
                        label,
                        string.Empty,
                        mode == candidate,
                        palette))
                {
                    mode = candidate;
                    GUI.FocusControl(null);
                }
                x += width + 4f;
            }
        }

        private void DrawRename()
        {
            GUILayout.Label("FIND / REPLACE", EditorStyles.miniBoldLabel);
            GUILayout.BeginHorizontal();
            find = EditorGUILayout.TextField(find);
            replace = EditorGUILayout.TextField(replace);
            GUILayout.EndHorizontal();
            regex = EditorGUILayout.ToggleLeft("Regex", regex);
            GUILayout.Space(8f);
            GUILayout.Label("PREFIX / SUFFIX", EditorStyles.miniBoldLabel);
            GUILayout.BeginHorizontal();
            prefix = EditorGUILayout.TextField(prefix);
            suffix = EditorGUILayout.TextField(suffix);
            GUILayout.EndHorizontal();
            number = EditorGUILayout.ToggleLeft("Sequential number", number);
            if (number)
            {
                numberStart = EditorGUILayout.IntField("Start", numberStart);
            }
        }

        private void DrawState()
        {
            activeState = EditorGUILayout.Popup("Active", activeState + 1, new[] { "No change", "Off", "On" }) - 1;
            staticState = EditorGUILayout.Popup("Static", staticState + 1, new[] { "No change", "Off", "On" }) - 1;
            applyLayer = EditorGUILayout.ToggleLeft("Change layer", applyLayer);
            if (applyLayer)
            {
                layer = EditorGUILayout.LayerField("Layer", Mathf.Max(0, layer));
            }
            applyTag = EditorGUILayout.ToggleLeft("Change tag", applyTag);
            if (applyTag)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Tag", GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                tagName = EditorGUILayout.TagField(string.IsNullOrEmpty(tagName) ? "Untagged" : tagName);
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox("Only enabled fields change. Include children applies the operation recursively.", MessageType.None);
        }

        private void DrawOrganize(DansToolboxPalette palette)
        {
            collectionName = EditorGUILayout.TextField("Name", collectionName);
            collectionColor = EditorGUILayout.ColorField("Color", collectionColor);
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("VIRTUAL", GUILayout.Height(34f)))
            {
                BetterHierarchyCollections.CreateVirtual(collectionName, collectionColor, GetTargets());
                Close();
            }
            if (GUILayout.Button("PARENT", GUILayout.Height(34f)))
            {
                BetterHierarchyCollections.CreateTransformParent(collectionName, GetTargets(), collectionColor);
                Close();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
            if (GUILayout.Button("SORT CHILDREN A–Z", GUILayout.Height(28f)))
            {
                SortChildren();
            }
            if (GUILayout.Button("SORT CHILDREN BY TYPE", GUILayout.Height(28f)))
            {
                SortChildrenByType();
            }
            if (GUILayout.Button("SORT CHILDREN BY POSITION", GUILayout.Height(28f)))
            {
                SortChildrenByPosition();
            }
        }

        private void DrawReplace()
        {
            replacementPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Prefab",
                replacementPrefab,
                typeof(GameObject),
                false);
            EditorGUILayout.HelpBox(
                "Replaces each selected object while preserving parent, sibling index, world position, rotation, and local scale. The operation is Undoable.",
                MessageType.Warning);
        }

        private void DrawComponent()
        {
            sourceComponent = (Component)EditorGUILayout.ObjectField(
                "Source",
                sourceComponent,
                typeof(Component),
                true);
            EditorGUILayout.HelpBox(
                "Copies the component and its serialized values to targets that do not already have that exact component type.",
                MessageType.None);
        }

        private bool CanApply()
        {
            if (GetTargets().Count == 0)
            {
                return false;
            }

            switch (mode)
            {
                case BatchMode.Replace:
                    return replacementPrefab != null && PrefabUtility.IsPartOfPrefabAsset(replacementPrefab);
                case BatchMode.Component:
                    return sourceComponent != null;
                case BatchMode.State:
                    return activeState >= 0 || staticState >= 0 || applyLayer || applyTag;
                case BatchMode.Organize:
                    return false;
                default:
                    return true;
            }
        }

        private void Apply()
        {
            List<GameObject> targets = GetEffectiveTargets();
            switch (mode)
            {
                case BatchMode.State:
                    ApplyState(targets);
                    break;
                case BatchMode.Replace:
                    if (EditorUtility.DisplayDialog("Replace objects", "Replace " + targets.Count + " objects with " + replacementPrefab.name + "?", "Replace", "Cancel"))
                    {
                        ReplaceObjects(targets);
                    }
                    break;
                case BatchMode.Component:
                    CopyComponent(targets);
                    break;
                default:
                    Rename(targets);
                    break;
            }
        }

        private void Rename(IReadOnlyList<GameObject> targets)
        {
            Regex expression = null;
            if (regex && !string.IsNullOrEmpty(find))
            {
                try
                {
                    expression = new Regex(find);
                }
                catch (ArgumentException exception)
                {
                    EditorUtility.DisplayDialog("Invalid regex", exception.Message, "OK");
                    return;
                }
            }

            for (int index = 0; index < targets.Count; index++)
            {
                GameObject gameObject = targets[index];
                Undo.RecordObject(gameObject, "Batch Rename");
                string next = gameObject.name;
                if (!string.IsNullOrEmpty(find))
                {
                    next = expression != null
                        ? expression.Replace(next, replace ?? string.Empty)
                        : next.Replace(find, replace ?? string.Empty);
                }
                next = (prefix ?? string.Empty) + next + (suffix ?? string.Empty);
                if (number)
                {
                    next += " " + (numberStart + index).ToString("00");
                }
                gameObject.name = next;
                EditorUtility.SetDirty(gameObject);
            }
            Close();
        }

        private void ApplyState(IEnumerable<GameObject> targets)
        {
            foreach (GameObject gameObject in targets)
            {
                Undo.RecordObject(gameObject, "Batch State");
                if (activeState >= 0)
                {
                    gameObject.SetActive(activeState == 1);
                }
                if (staticState >= 0)
                {
                    gameObject.isStatic = staticState == 1;
                }
                if (applyLayer && layer >= 0)
                {
                    gameObject.layer = layer;
                }
                if (applyTag && !string.IsNullOrEmpty(tagName))
                {
                    gameObject.tag = tagName;
                }
                EditorUtility.SetDirty(gameObject);
            }
            Close();
        }

        private void ReplaceObjects(IReadOnlyList<GameObject> targets)
        {
            List<UnityEngine.Object> created = new List<UnityEngine.Object>();
            foreach (GameObject original in targets)
            {
                Transform originalTransform = original.transform;
                Transform parent = originalTransform.parent;
                Scene scene = original.scene;
                int sibling = originalTransform.GetSiblingIndex();
                Vector3 position = originalTransform.position;
                Quaternion rotation = originalTransform.rotation;
                Vector3 localScale = originalTransform.localScale;

                GameObject instance = PrefabUtility.InstantiatePrefab(replacementPrefab, scene) as GameObject;
                if (instance == null)
                {
                    continue;
                }
                Undo.RegisterCreatedObjectUndo(instance, "Replace GameObject");
                if (parent != null)
                {
                    Undo.SetTransformParent(instance.transform, parent, "Parent Replacement");
                }
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.transform.localScale = localScale;
                instance.transform.SetSiblingIndex(sibling);
                created.Add(instance);
                Undo.DestroyObjectImmediate(original);
            }
            Selection.objects = created.ToArray();
            Close();
        }

        private void CopyComponent(IEnumerable<GameObject> targets)
        {
            Type type = sourceComponent.GetType();
            ComponentUtility.CopyComponent(sourceComponent);
            foreach (GameObject gameObject in targets)
            {
                if (gameObject == sourceComponent.gameObject || gameObject.GetComponent(type) != null)
                {
                    continue;
                }
                ComponentUtility.PasteComponentAsNew(gameObject);
            }
            Close();
        }

        private void SortChildren()
        {
            SortChildrenWith(transform => transform.name, StringComparer.OrdinalIgnoreCase);
        }

        private void SortChildrenByType()
        {
            SortChildrenWith(transform => transform.GetComponents<Component>()
                .FirstOrDefault(component => component != null && !(component is Transform))?.GetType().Name ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
        }

        private void SortChildrenByPosition()
        {
            foreach (GameObject gameObject in GetTargets())
            {
                List<Transform> children = Enumerable.Range(0, gameObject.transform.childCount)
                    .Select(gameObject.transform.GetChild)
                    .OrderBy(child => child.localPosition.x)
                    .ThenBy(child => child.localPosition.z)
                    .ThenBy(child => child.localPosition.y)
                    .ToList();
                for (int index = 0; index < children.Count; index++)
                {
                    Undo.RecordObject(children[index], "Sort Children");
                    children[index].SetSiblingIndex(index);
                }
            }
        }

        private void SortChildrenWith(Func<Transform, string> key, IComparer<string> comparer)
        {
            foreach (GameObject gameObject in GetTargets())
            {
                List<Transform> children = Enumerable.Range(0, gameObject.transform.childCount)
                    .Select(gameObject.transform.GetChild)
                    .OrderBy(key, comparer)
                    .ToList();
                for (int index = 0; index < children.Count; index++)
                {
                    Undo.RecordObject(children[index], "Sort Children");
                    children[index].SetSiblingIndex(index);
                }
            }
        }

        private List<GameObject> GetTargets()
        {
            return targetIds
                .Select(BetterHierarchyObjectIds.Resolve)
                .Where(gameObject => gameObject != null)
                .Distinct()
                .ToList();
        }

        private List<GameObject> GetEffectiveTargets()
        {
            List<GameObject> targets = GetTargets();
            if (!includeChildren)
            {
                return targets;
            }

            return targets
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .Distinct()
                .ToList();
        }
    }
}
