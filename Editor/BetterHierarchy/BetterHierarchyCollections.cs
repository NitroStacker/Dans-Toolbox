using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    internal static class BetterHierarchyCollections
    {
        internal static BetterHierarchyCollection CreateVirtual(
            string name,
            Color color,
            IEnumerable<GameObject> members)
        {
            BetterHierarchyCollection collection = BetterHierarchyProjectSettings.AddCollection(name, color);
            AddMembers(collection, members);
            return collection;
        }

        internal static GameObject CreateTransformParent(
            string name,
            IEnumerable<GameObject> source,
            Color color)
        {
            List<GameObject> members = source?
                .Where(gameObject => gameObject != null)
                .Distinct()
                .OrderBy(gameObject => gameObject.transform.GetSiblingIndex())
                .ToList() ?? new List<GameObject>();
            if (members.Count == 0)
            {
                return null;
            }

            Scene scene = members[0].scene;
            if (members.Any(gameObject => gameObject.scene != scene))
            {
                EditorUtility.DisplayDialog(
                    "Better Hierarchy",
                    "A Transform collection can only contain objects from one scene. Use a virtual collection for cross-scene organization.",
                    "OK");
                return null;
            }

            Transform commonParent = members[0].transform.parent;
            if (members.Any(gameObject => gameObject.transform.parent != commonParent))
            {
                commonParent = null;
            }

            string parentName = string.IsNullOrWhiteSpace(name) ? "Collection" : name.Trim();
            GameObject parent = new GameObject(parentName);
            Undo.RegisterCreatedObjectUndo(parent, "Create Transform Collection");
            SceneManager.MoveGameObjectToScene(parent, scene);
            if (commonParent != null)
            {
                Undo.SetTransformParent(parent.transform, commonParent, "Parent Transform Collection");
            }

            int siblingIndex = members.Min(gameObject => gameObject.transform.GetSiblingIndex());
            parent.transform.SetSiblingIndex(siblingIndex);

            foreach (GameObject member in members)
            {
                Undo.SetTransformParent(member.transform, parent.transform, "Add To Transform Collection");
            }

            string objectId = BetterHierarchyObjectIds.Get(parent);
            if (!string.IsNullOrEmpty(objectId))
            {
                BetterHierarchyProjectSettings.MutableRules.Add(new BetterHierarchyRule
                {
                    Name = parentName,
                    Match = BetterHierarchyRuleMatch.Object,
                    Value = objectId,
                    Color = new Color(color.r, color.g, color.b, Mathf.Min(color.a, 0.28f)),
                    Header = true,
                    Bold = true,
                    Badge = "GROUP",
                    Priority = 500
                });
                BetterHierarchyProjectSettings.SaveNow();
            }

            Selection.activeGameObject = parent;
            return parent;
        }

        internal static void AddMembers(
            BetterHierarchyCollection collection,
            IEnumerable<GameObject> members)
        {
            if (collection == null || members == null)
            {
                return;
            }

            bool changed = false;
            foreach (GameObject gameObject in members.Where(gameObject => gameObject != null).Distinct())
            {
                string id = BetterHierarchyObjectIds.Get(gameObject);
                if (!string.IsNullOrEmpty(id) && !collection.MemberIds.Contains(id))
                {
                    collection.MemberIds.Add(id);
                    changed = true;
                }
            }

            if (changed)
            {
                BetterHierarchyProjectSettings.SaveNow();
            }
        }

        internal static int RemoveMembers(
            BetterHierarchyCollection collection,
            IEnumerable<GameObject> gameObjects)
        {
            if (collection == null || gameObjects == null)
            {
                return 0;
            }

            HashSet<string> ids = gameObjects
                .Where(gameObject => gameObject != null)
                .Select(BetterHierarchyObjectIds.Get)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();
            int removableCount = collection.MemberIds.Count(ids.Contains);
            if (removableCount == 0)
            {
                return 0;
            }

            BetterHierarchyProjectSettings.RecordUndo(
                removableCount == 1 ? "Remove From Collection" : "Remove Members From Collection");
            int removed = collection.MemberIds.RemoveAll(ids.Contains);
            BetterHierarchyProjectSettings.SaveNow();
            return removed;
        }

        internal static bool RemoveMember(
            BetterHierarchyCollection collection,
            GameObject gameObject)
        {
            return RemoveMembers(collection, new[] { gameObject }) > 0;
        }

        internal static bool Contains(
            BetterHierarchyCollection collection,
            GameObject gameObject)
        {
            if (collection == null || gameObject == null)
            {
                return false;
            }

            string id = BetterHierarchyObjectIds.Get(gameObject);
            return !string.IsNullOrEmpty(id) && collection.MemberIds.Contains(id);
        }

        internal static GameObject GetTransformCollectionParent(GameObject gameObject)
        {
            GameObject parent = gameObject != null && gameObject.transform.parent != null
                ? gameObject.transform.parent.gameObject
                : null;
            if (parent == null)
            {
                return null;
            }

            return IsTransformCollection(parent) ? parent : null;
        }

        internal static bool IsTransformCollection(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            string id = BetterHierarchyObjectIds.Get(gameObject);
            return !string.IsNullOrEmpty(id) && BetterHierarchyProjectSettings.Rules.Any(rule =>
                rule.Match == BetterHierarchyRuleMatch.Object &&
                rule.Value == id &&
                rule.Badge == "GROUP");
        }

        internal static bool RemoveFromTransformCollection(GameObject gameObject)
        {
            GameObject collectionParent = GetTransformCollectionParent(gameObject);
            if (collectionParent == null)
            {
                return false;
            }

            Undo.SetTransformParent(
                gameObject.transform,
                collectionParent.transform.parent,
                "Remove From Parent Collection");
            return true;
        }

        internal static bool HasVirtualCollectionItems(BetterHierarchyCollection collection)
        {
            return collection != null && Resolve(collection).Count > 0;
        }

        internal static bool HasTransformCollectionItems(GameObject collectionParent)
        {
            return collectionParent != null && collectionParent.transform.childCount > 0;
        }

        internal static bool DeleteVirtualCollection(
            BetterHierarchyCollection collection,
            bool deleteItems,
            bool registerUndo = true)
        {
            if (collection == null || !BetterHierarchyProjectSettings.Collections.Contains(collection))
            {
                return false;
            }

            GameObject[] members = Resolve(collection).ToArray();
            int undoGroup = registerUndo ? Undo.GetCurrentGroup() : -1;
            if (registerUndo)
            {
                Undo.SetCurrentGroupName("Delete Collection");
                BetterHierarchyProjectSettings.RecordUndo("Delete Collection");
            }

            if (deleteItems)
            {
                DestroySceneObjects(members, registerUndo);
            }

            BetterHierarchyProjectSettings.RemoveCollection(collection);
            if (registerUndo)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
            return true;
        }

        internal static bool DeleteTransformCollection(
            GameObject collectionParent,
            bool deleteItems,
            bool registerUndo = true)
        {
            if (!IsTransformCollection(collectionParent))
            {
                return false;
            }

            string objectId = BetterHierarchyObjectIds.Get(collectionParent);
            Transform destination = collectionParent.transform.parent;
            Transform[] children = Enumerable.Range(0, collectionParent.transform.childCount)
                .Select(index => collectionParent.transform.GetChild(index))
                .ToArray();
            int undoGroup = registerUndo ? Undo.GetCurrentGroup() : -1;
            if (registerUndo)
            {
                Undo.SetCurrentGroupName("Delete Parent Collection");
                BetterHierarchyProjectSettings.RecordUndo("Delete Parent Collection");
            }

            if (!deleteItems)
            {
                foreach (Transform child in children)
                {
                    if (registerUndo)
                    {
                        Undo.SetTransformParent(child, destination, "Move Out Of Collection");
                    }
                    else
                    {
                        child.SetParent(destination, true);
                    }
                }
            }

            BetterHierarchyProjectSettings.MutableRules.RemoveAll(rule =>
                rule.Match == BetterHierarchyRuleMatch.Object &&
                rule.Value == objectId &&
                rule.Badge == "GROUP");
            BetterHierarchyProjectSettings.SaveNow();
            if (registerUndo)
            {
                Undo.DestroyObjectImmediate(collectionParent);
                Undo.CollapseUndoOperations(undoGroup);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(collectionParent);
            }

            if (!deleteItems)
            {
                Selection.objects = children
                    .Where(child => child != null)
                    .Select(child => child.gameObject)
                    .Cast<UnityEngine.Object>()
                    .ToArray();
            }
            return true;
        }

        private static void DestroySceneObjects(
            IEnumerable<GameObject> gameObjects,
            bool registerUndo)
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            HashSet<GameObject> selected = gameObjects
                .Where(gameObject => gameObject != null &&
                                     !AssetDatabase.Contains(gameObject) &&
                                     (gameObject.hideFlags & HideFlags.NotEditable) == 0 &&
                                     (prefabStage == null || gameObject != prefabStage.prefabContentsRoot))
                .ToHashSet();
            GameObject[] roots = selected
                .Where(gameObject =>
                {
                    for (Transform parent = gameObject.transform.parent; parent != null; parent = parent.parent)
                    {
                        if (selected.Contains(parent.gameObject))
                        {
                            return false;
                        }
                    }
                    return true;
                })
                .ToArray();

            foreach (GameObject root in roots)
            {
                if (registerUndo)
                {
                    Undo.DestroyObjectImmediate(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        internal static IReadOnlyList<GameObject> Resolve(BetterHierarchyCollection collection)
        {
            if (collection == null)
            {
                return Array.Empty<GameObject>();
            }

            List<GameObject> result = new List<GameObject>();
            for (int index = collection.MemberIds.Count - 1; index >= 0; index--)
            {
                GameObject gameObject = BetterHierarchyObjectIds.Resolve(collection.MemberIds[index]);
                if (gameObject != null)
                {
                    result.Add(gameObject);
                }
            }

            return result;
        }

        internal static bool Contains(string collectionName, GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            string id = BetterHierarchyObjectIds.Get(gameObject);
            return BetterHierarchyProjectSettings.Collections.Any(collection =>
                collection.Name.IndexOf(collectionName ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0 &&
                collection.MemberIds.Contains(id));
        }
    }
}
