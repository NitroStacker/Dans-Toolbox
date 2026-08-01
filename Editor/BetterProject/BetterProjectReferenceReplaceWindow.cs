using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterProject
{
    internal sealed class BetterProjectReferenceReplaceWindow : EditorWindow
    {
        [SerializeField] private UnityEngine.Object source;
        [SerializeField] private UnityEngine.Object replacement;
        [SerializeField] private List<string> matches = new List<string>();
        [SerializeField] private Vector2 scroll;

        internal static void Open(UnityEngine.Object sourceAsset)
        {
            BetterProjectReferenceReplaceWindow window = GetWindow<BetterProjectReferenceReplaceWindow>(true, "References");
            window.source = sourceAsset;
            window.minSize = new Vector2(520f, 430f);
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
            GUILayout.Label("REPLACE REFERENCES", BetterProjectGui.CardTitle);
            GUILayout.Space(8f);
            source = EditorGUILayout.ObjectField("From", source, typeof(UnityEngine.Object), false);
            replacement = EditorGUILayout.ObjectField("To", replacement, typeof(UnityEngine.Object), false);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SCAN", BetterProjectGui.SegmentActive, GUILayout.Height(28f)))
            {
                Scan();
            }
            EditorGUI.BeginDisabledGroup(matches.Count == 0 || source == null || replacement == null);
            if (GUILayout.Button("REPLACE", BetterProjectGui.Segment, GUILayout.Height(28f)))
            {
                Apply();
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.Label(matches.Count + " SERIALIZED ASSETS", BetterProjectGui.Muted);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string path in matches)
            {
                GUILayout.Label(path, BetterProjectGui.Tiny);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.Label("Scenes are reported by Impact but are not rewritten while closed.", BetterProjectGui.Muted);
            GUILayout.EndArea();
            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }
        }

        private void Scan()
        {
            matches.Clear();
            if (source == null)
            {
                return;
            }
            try
            {
                int index = 0;
                BetterProjectAssetRecord[] assets = BetterProjectIndex.Records
                    .Where(record => !record.IsFolder && record.Extension != ".unity" && record.Extension != ".cs")
                    .ToArray();
                foreach (BetterProjectAssetRecord record in assets)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("References", record.Path, index++ / (float)Mathf.Max(1, assets.Length)))
                    {
                        break;
                    }
                    if (ContainsReference(record.Path, source))
                    {
                        matches.Add(record.Path);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void Apply()
        {
            if (!EditorUtility.DisplayDialog("Replace references?", matches.Count + " assets will be edited.", "Replace", "Cancel"))
            {
                return;
            }
            int changed = 0;
            foreach (string path in matches.ToArray())
            {
                foreach (UnityEngine.Object owner in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (owner == null)
                    {
                        continue;
                    }
                    var serialized = new SerializedObject(owner);
                    SerializedProperty property = serialized.GetIterator();
                    bool enterChildren = true;
                    bool dirty = false;
                    while (property.Next(enterChildren))
                    {
                        enterChildren = false;
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == source)
                        {
                            Undo.RecordObject(owner, "Replace Asset Reference");
                            property.objectReferenceValue = replacement;
                            dirty = true;
                        }
                    }
                    if (dirty)
                    {
                        serialized.ApplyModifiedProperties();
                        EditorUtility.SetDirty(owner);
                        changed++;
                    }
                }
            }
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent(changed + " updated"));
            Scan();
        }

        private static bool ContainsReference(string path, UnityEngine.Object target)
        {
            foreach (UnityEngine.Object owner in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (owner == null)
                {
                    continue;
                }
                var serialized = new SerializedObject(owner);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == target)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
