using System;
using System.Collections.Generic;
using DansToolbox.EditorTools.BetterConsole;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterScene
{
    internal readonly struct BetterSceneDiagnosticReport
    {
        internal BetterSceneDiagnosticReport(
            int missingScripts,
            int missingReferences,
            int prefabOverrides,
            int inactiveObjects,
            BetterConsoleDiagnosticSummary console)
        {
            MissingScripts = missingScripts;
            MissingReferences = missingReferences;
            PrefabOverrides = prefabOverrides;
            InactiveObjects = inactiveObjects;
            Console = console;
        }

        internal int MissingScripts { get; }
        internal int MissingReferences { get; }
        internal int PrefabOverrides { get; }
        internal int InactiveObjects { get; }
        internal BetterConsoleDiagnosticSummary Console { get; }
        internal int Errors => MissingScripts + MissingReferences + Console.Errors;
        internal int Warnings => PrefabOverrides + InactiveObjects + Console.Warnings;
        internal bool HasIssues => Errors > 0 || Warnings > 0;
        internal string Badge => Errors > 0 ? "E" + Errors : Warnings > 0 ? "W" + Warnings : "OK";
    }

    [InitializeOnLoad]
    internal static class BetterSceneDiagnostics
    {
        private static BetterSceneDiagnosticReport cached;
        private static int cachedSelectionHash;
        private static double cachedAt;
        private static bool dirty = true;

        static BetterSceneDiagnostics()
        {
            Selection.selectionChanged += Invalidate;
            EditorApplication.hierarchyChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
            BetterConsoleDiagnosticBridge.Changed += Invalidate;
        }

        internal static BetterSceneDiagnosticReport Current
        {
            get
            {
                GameObject[] targets = Selection.gameObjects;
                int hash = ComputeSelectionHash(targets);
                if (!dirty && hash == cachedSelectionHash && EditorApplication.timeSinceStartup - cachedAt < 0.5d)
                {
                    return cached;
                }

                cachedSelectionHash = hash;
                cachedAt = EditorApplication.timeSinceStartup;
                dirty = false;
                cached = Build(targets);
                return cached;
            }
        }

        internal static BetterSceneDiagnosticReport Build(GameObject[] targets)
        {
            int missingScripts = 0;
            int missingReferences = 0;
            int inactive = 0;
            int overrides = 0;
            var inspectedPrefabRoots = new HashSet<int>();

            foreach (GameObject root in targets ?? Array.Empty<GameObject>())
            {
                if (root == null) continue;
                GameObject[] objects = BetterSceneSettings.IncludeDescendants
                    ? Array.ConvertAll(root.GetComponentsInChildren<Transform>(true), transform => transform.gameObject)
                    : new[] { root };
                foreach (GameObject gameObject in objects)
                {
                    if (!gameObject.activeInHierarchy) inactive++;
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                    missingReferences += CountMissingReferences(gameObject);
                }

                GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(root);
                if (prefabRoot != null && inspectedPrefabRoots.Add(prefabRoot.GetInstanceID()))
                {
                    overrides += PrefabUtility.GetObjectOverrides(prefabRoot, false).Count;
                    overrides += PrefabUtility.GetAddedComponents(prefabRoot).Count;
                    overrides += PrefabUtility.GetAddedGameObjects(prefabRoot).Count;
                    overrides += PrefabUtility.GetRemovedComponents(prefabRoot).Count;
                    overrides += PrefabUtility.GetRemovedGameObjects(prefabRoot).Count;
                }
            }

            return new BetterSceneDiagnosticReport(
                missingScripts,
                missingReferences,
                overrides,
                inactive,
                BetterConsoleDiagnosticBridge.GetSummary(targets));
        }

        internal static int CountMissingReferences(GameObject gameObject)
        {
            int count = 0;
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component == null) continue;
                try
                {
                    var serializedObject = new SerializedObject(component);
                    SerializedProperty property = serializedObject.GetIterator();
                    bool enterChildren = true;
                    while (property.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (property.propertyType != SerializedPropertyType.ObjectReference ||
                            property.propertyPath == "m_Script") continue;
                        if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0) count++;
                    }
                }
                catch (Exception)
                {
                    // Some custom serialized types can throw while their scripts reload.
                }
            }
            return count;
        }

        internal static void Invalidate()
        {
            dirty = true;
            SceneView.RepaintAll();
        }

        private static int ComputeSelectionHash(GameObject[] targets)
        {
            unchecked
            {
                int hash = 17;
                foreach (GameObject target in targets ?? Array.Empty<GameObject>())
                {
                    hash = hash * 31 + (target == null ? 0 : target.GetInstanceID());
                }
                return hash;
            }
        }
    }
}
