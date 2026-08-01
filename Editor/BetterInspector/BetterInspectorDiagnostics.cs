using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DansToolbox.EditorTools.BetterInspector
{
    internal enum BetterInspectorIssueKind
    {
        MissingScript,
        MissingReference
    }

    internal readonly struct BetterInspectorIssue
    {
        internal BetterInspectorIssue(
            BetterInspectorIssueKind kind,
            Object context,
            string componentName,
            string message)
        {
            Kind = kind;
            Context = context;
            ComponentName = componentName;
            Message = message;
        }

        internal BetterInspectorIssueKind Kind { get; }
        internal Object Context { get; }
        internal string ComponentName { get; }
        internal string Message { get; }
    }

    internal static class BetterInspectorDiagnostics
    {
        internal static List<BetterInspectorIssue> Scan(Object[] targets)
        {
            var issues = new List<BetterInspectorIssue>();
            foreach (GameObject gameObject in ExpandGameObjects(targets))
            {
                int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                for (int index = 0; index < missingScripts; index++)
                {
                    issues.Add(new BetterInspectorIssue(
                        BetterInspectorIssueKind.MissingScript,
                        gameObject,
                        "Missing Script",
                        "A component script cannot be resolved."));
                }

                foreach (Component component in gameObject.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        continue;
                    }

                    ScanMissingReferences(component, issues);
                }
            }

            return issues;
        }

        internal static int RemoveMissingScripts(IEnumerable<Object> targets)
        {
            int removed = 0;
            GameObject[] gameObjects = ExpandGameObjects(targets).Distinct().ToArray();
            if (gameObjects.Length == 0)
            {
                return 0;
            }

            Undo.SetCurrentGroupName("Remove Missing Scripts");
            int group = Undo.GetCurrentGroup();
            foreach (GameObject gameObject in gameObjects)
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (count == 0)
                {
                    continue;
                }

                Undo.RegisterCompleteObjectUndo(gameObject, "Remove Missing Scripts");
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
                EditorUtility.SetDirty(gameObject);
            }
            Undo.CollapseUndoOperations(group);
            return removed;
        }

        private static IEnumerable<GameObject> ExpandGameObjects(IEnumerable<Object> targets)
        {
            if (targets == null)
            {
                yield break;
            }

            foreach (Object target in targets)
            {
                if (target is GameObject gameObject)
                {
                    yield return gameObject;
                }
                else if (target is Component component && component != null)
                {
                    yield return component.gameObject;
                }
            }
        }

        private static void ScanMissingReferences(
            Component component,
            ICollection<BetterInspectorIssue> issues)
        {
            try
            {
                using (var serializedObject = new SerializedObject(component))
                {
                    SerializedProperty property = serializedObject.GetIterator();
                    bool enterChildren = true;
                    while (property.NextVisible(enterChildren))
                    {
                        enterChildren = true;
                        if (property.propertyType != SerializedPropertyType.ObjectReference ||
                            property.name == "m_Script" ||
                            property.objectReferenceValue != null ||
                            property.objectReferenceInstanceIDValue == 0)
                        {
                            continue;
                        }

                        issues.Add(new BetterInspectorIssue(
                            BetterInspectorIssueKind.MissingReference,
                            component,
                            ObjectNames.NicifyVariableName(component.GetType().Name),
                            property.displayName + " references a missing object."));
                    }
                }
            }
            catch (Exception)
            {
                // Some native components do not expose a traversable SerializedObject.
            }
        }
    }
}
