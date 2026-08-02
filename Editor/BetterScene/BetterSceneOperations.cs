using System;
using System.Collections.Generic;
using System.Linq;
using DansToolbox.EditorTools.BetterConsole;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DansToolbox.EditorTools.BetterScene
{
    internal static class BetterSceneOperations
    {
        internal static bool TryGetBounds(GameObject gameObject, out Bounds bounds)
        {
            bounds = default;
            if (gameObject == null) return false;
            bool found = false;
            foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            foreach (Collider collider in gameObject.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null) continue;
                if (!found) { bounds = collider.bounds; found = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            foreach (Collider2D collider in gameObject.GetComponentsInChildren<Collider2D>(true))
            {
                if (collider == null) continue;
                if (!found) { bounds = collider.bounds; found = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            if (!found) bounds = new Bounds(gameObject.transform.position, Vector3.zero);
            return true;
        }

        internal static Bounds GetCombinedBounds(IEnumerable<GameObject> gameObjects)
        {
            bool found = false;
            Bounds combined = default;
            foreach (GameObject gameObject in gameObjects ?? Array.Empty<GameObject>())
            {
                if (!TryGetBounds(gameObject, out Bounds bounds)) continue;
                if (!found) { combined = bounds; found = true; }
                else combined.Encapsulate(bounds);
            }
            return combined;
        }

        internal static float SnapValue(float value, float increment)
        {
            return increment <= 0f ? value : Mathf.Round(value / increment) * increment;
        }

        internal static Vector3 SnapVector(Vector3 value, Vector3 increment)
        {
            return new Vector3(
                SnapValue(value.x, Mathf.Abs(increment.x)),
                SnapValue(value.y, Mathf.Abs(increment.y)),
                SnapValue(value.z, Mathf.Abs(increment.z)));
        }

        internal static Vector3 CalculateAlignedPosition(
            Bounds moving,
            Bounds anchor,
            BetterSceneAxis axis,
            BetterSceneAlignAnchor edge,
            Vector3 currentPosition)
        {
            int dimension = (int)axis;
            float movingValue = edge == BetterSceneAlignAnchor.Minimum
                ? moving.min[dimension]
                : edge == BetterSceneAlignAnchor.Maximum ? moving.max[dimension] : moving.center[dimension];
            float anchorValue = edge == BetterSceneAlignAnchor.Minimum
                ? anchor.min[dimension]
                : edge == BetterSceneAlignAnchor.Maximum ? anchor.max[dimension] : anchor.center[dimension];
            currentPosition[dimension] += anchorValue - movingValue;
            return currentPosition;
        }

        internal static void AlignSelection(BetterSceneAxis axis, BetterSceneAlignAnchor edge)
        {
            GameObject anchorObject = Selection.activeGameObject;
            GameObject[] selected = Selection.gameObjects.Where(item => item != null).ToArray();
            if (anchorObject == null || selected.Length < 2 || !TryGetBounds(anchorObject, out Bounds anchor)) return;
            Transform[] transforms = selected.Select(item => item.transform).ToArray();
            Undo.RecordObjects(transforms, "Align Scene Objects");
            foreach (GameObject gameObject in selected)
            {
                if (gameObject == anchorObject || !TryGetBounds(gameObject, out Bounds moving)) continue;
                gameObject.transform.position = CalculateAlignedPosition(moving, anchor, axis, edge, gameObject.transform.position);
            }
            MarkScenesDirty(selected);
        }

        internal static void DistributeSelection(BetterSceneAxis axis)
        {
            GameObject[] selected = Selection.gameObjects.Where(item => item != null).ToArray();
            if (selected.Length < 3) return;
            int dimension = (int)axis;
            var items = selected
                .Select(item => new { Object = item, Bounds = GetBounds(item) })
                .OrderBy(item => item.Bounds.center[dimension])
                .ToArray();
            float start = items[0].Bounds.center[dimension];
            float end = items[items.Length - 1].Bounds.center[dimension];
            float spacing = (end - start) / (items.Length - 1);
            Undo.RecordObjects(items.Select(item => item.Object.transform).ToArray(), "Distribute Scene Objects");
            for (int index = 1; index < items.Length - 1; index++)
            {
                Transform transform = items[index].Object.transform;
                Vector3 position = transform.position;
                position[dimension] += start + spacing * index - items[index].Bounds.center[dimension];
                transform.position = position;
            }
            MarkScenesDirty(selected);
        }

        internal static void SnapSelection(bool position, bool rotation, bool scale)
        {
            Transform[] transforms = Selection.transforms.Where(item => item != null).ToArray();
            if (transforms.Length == 0) return;
            Undo.RecordObjects(transforms, "Snap Scene Objects");
            foreach (Transform transform in transforms)
            {
                if (position) transform.position = SnapVector(transform.position, EditorSnapSettings.move);
                if (rotation)
                {
                    Vector3 step = Vector3.one * Mathf.Max(0.0001f, EditorSnapSettings.rotate);
                    transform.eulerAngles = SnapVector(transform.eulerAngles, step);
                }
                if (scale)
                {
                    Vector3 step = Vector3.one * Mathf.Max(0.0001f, EditorSnapSettings.scale);
                    transform.localScale = SnapVector(transform.localScale, step);
                }
            }
            MarkScenesDirty(transforms.Select(item => item.gameObject));
        }

        internal static void GroundSelection()
        {
            GameObject[] selected = Selection.gameObjects.Where(item => item != null).ToArray();
            if (selected.Length == 0) return;
            Undo.RecordObjects(selected.Select(item => item.transform).ToArray(), "Ground Scene Objects");
            foreach (GameObject gameObject in selected)
            {
                Bounds bounds = GetBounds(gameObject);
                Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + Mathf.Max(0.05f, bounds.size.y * 0.05f), bounds.center.z);
                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 100000f, ~0, QueryTriggerInteraction.Ignore)
                    .OrderBy(hit => hit.distance).ToArray();
                bool grounded = false;
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider == null || hit.collider.transform.IsChildOf(gameObject.transform)) continue;
                    Vector3 position = gameObject.transform.position;
                    position.y += hit.point.y - bounds.min.y;
                    gameObject.transform.position = position;
                    grounded = true;
                    break;
                }
                if (!grounded)
                {
                    RaycastHit2D[] hits2D = Physics2D.RaycastAll(origin, Vector2.down, 100000f);
                    foreach (RaycastHit2D hit in hits2D.OrderBy(item => item.distance))
                    {
                        if (hit.collider == null || hit.collider.transform.IsChildOf(gameObject.transform)) continue;
                        Vector3 position = gameObject.transform.position;
                        position.y += hit.point.y - bounds.min.y;
                        gameObject.transform.position = position;
                        grounded = true;
                        break;
                    }
                }
                if (!grounded && BetterSceneSettings.GroundToZeroWhenNoSurface)
                {
                    Vector3 position = gameObject.transform.position;
                    position.y -= bounds.min.y;
                    gameObject.transform.position = position;
                }
            }
            MarkScenesDirty(selected);
        }

        internal static void ScatterSelection(float radius, float height, int seed)
        {
            Transform[] transforms = Selection.transforms.Where(item => item != null).ToArray();
            if (transforms.Length == 0) return;
            var random = new System.Random(seed);
            Undo.RecordObjects(transforms, "Scatter Scene Objects");
            foreach (Transform transform in transforms)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
                float y = ((float)random.NextDouble() * 2f - 1f) * height;
                transform.position += new Vector3(Mathf.Cos(angle) * distance, y, Mathf.Sin(angle) * distance);
                Vector3 euler = transform.eulerAngles;
                euler.y = (float)random.NextDouble() * 360f;
                transform.eulerAngles = euler;
            }
            MarkScenesDirty(transforms.Select(item => item.gameObject));
        }

        internal static bool ReplaceSelection(UnityEngine.Object replacement)
        {
            GameObject source = replacement as GameObject;
            GameObject[] selected = Selection.gameObjects.Where(item => item != null).ToArray();
            if (source == null || !AssetDatabase.Contains(source) || selected.Length == 0) return false;
            if (!EditorUtility.DisplayDialog(
                    "Replace scene objects",
                    "Replace " + selected.Length + " selected object" + (selected.Length == 1 ? string.Empty : "s") +
                    " with " + source.name + "? This is Undoable.",
                    "Replace",
                    "Cancel")) return false;

            var replacements = new List<GameObject>();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace Scene Objects");
            foreach (GameObject oldObject in selected)
            {
                GameObject created = PrefabUtility.InstantiatePrefab(source, oldObject.scene) as GameObject;
                if (created == null) continue;
                Undo.RegisterCreatedObjectUndo(created, "Replace Scene Object");
                Transform oldTransform = oldObject.transform;
                Transform createdTransform = created.transform;
                if (oldTransform.parent != null) Undo.SetTransformParent(createdTransform, oldTransform.parent, "Parent Replacement");
                createdTransform.SetSiblingIndex(oldTransform.GetSiblingIndex());
                createdTransform.localPosition = oldTransform.localPosition;
                createdTransform.localRotation = oldTransform.localRotation;
                createdTransform.localScale = oldTransform.localScale;
                created.name = oldObject.name;
                replacements.Add(created);
                Undo.DestroyObjectImmediate(oldObject);
            }
            Undo.CollapseUndoOperations(group);
            Selection.objects = replacements.ToArray();
            MarkScenesDirty(replacements);
            return replacements.Count > 0;
        }

        internal static void FrameSelection(bool lockView = false)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null) view.FrameSelected(lockView, false);
        }

        internal static BetterSceneBookmark CaptureBookmark(string name)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) return null;
            Scene scene = SceneManager.GetActiveScene();
            return BetterSceneSettings.AddBookmark(
                name,
                scene.path,
                view.pivot,
                view.rotation,
                view.size,
                view.orthographic,
                view.in2DMode);
        }

        internal static bool RestoreBookmark(BetterSceneBookmark bookmark)
        {
            if (bookmark == null) return false;
            if (!string.IsNullOrEmpty(bookmark.ScenePath) &&
                !string.Equals(SceneManager.GetActiveScene().path, bookmark.ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(bookmark.ScenePath);
                if (sceneAsset == null || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                EditorSceneManager.OpenScene(bookmark.ScenePath, OpenSceneMode.Single);
            }
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) return false;
            view.in2DMode = bookmark.In2DMode;
            view.LookAt(bookmark.Pivot, bookmark.Rotation, bookmark.Size, bookmark.Orthographic, false);
            view.Focus();
            return true;
        }

        internal static void ViewThrough(Camera camera)
        {
            if (camera == null || SceneView.lastActiveSceneView == null) return;
            SceneView.lastActiveSceneView.AlignViewToObject(camera.transform);
            SceneView.lastActiveSceneView.Focus();
        }

        internal static bool RevealPrefabAsset(GameObject gameObject)
        {
            if (gameObject == null) return false;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
            return !string.IsNullOrEmpty(path) && BetterConsoleDiagnosticBridge.RevealAssetPath(path);
        }

        private static Bounds GetBounds(GameObject gameObject)
        {
            TryGetBounds(gameObject, out Bounds bounds);
            return bounds;
        }

        private static void MarkScenesDirty(IEnumerable<GameObject> gameObjects)
        {
            foreach (Scene scene in gameObjects.Where(item => item != null).Select(item => item.scene).Distinct())
            {
                if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
            }
            BetterSceneDiagnostics.Invalidate();
            SceneView.RepaintAll();
        }
    }
}
