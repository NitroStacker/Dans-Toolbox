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
        internal const float DefaultViewZoom = 10f;

        internal static GameObject Create(BetterSceneCreateKind kind)
        {
            if (kind == BetterSceneCreateKind.Group && Selection.gameObjects.Length > 0)
            {
                return GroupSelection();
            }

            GameObject created;
            switch (kind)
            {
                case BetterSceneCreateKind.Cube:
                    created = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    break;
                case BetterSceneCreateKind.Sphere:
                    created = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    break;
                case BetterSceneCreateKind.Plane:
                    created = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    break;
                case BetterSceneCreateKind.Camera:
                    created = new GameObject("Camera", typeof(Camera));
                    break;
                case BetterSceneCreateKind.Light:
                    created = new GameObject("Directional Light", typeof(Light));
                    created.GetComponent<Light>().type = LightType.Directional;
                    created.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    break;
                case BetterSceneCreateKind.Audio:
                    created = new GameObject("Audio Source", typeof(AudioSource));
                    break;
                case BetterSceneCreateKind.Group:
                    created = new GameObject("Group");
                    break;
                default:
                    created = new GameObject("GameObject");
                    break;
            }

            StageUtility.PlaceGameObjectInCurrentStage(created);
            Undo.RegisterCreatedObjectUndo(created, "Create " + created.name);
            SceneView view = SceneView.lastActiveSceneView;
            created.transform.position = view == null ? Vector3.zero : view.pivot;
            GameObjectUtility.EnsureUniqueNameForSibling(created);
            Selection.activeGameObject = created;
            MarkScenesDirty(new[] { created });
            return created;
        }

        internal static GameObject GroupSelection()
        {
            GameObject[] selected = Selection.gameObjects.Where(item => item != null).ToArray();
            if (selected.Length == 0) return null;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Group Scene Objects");
            Transform commonParent = selected[0].transform.parent;
            if (selected.Any(item => item.transform.parent != commonParent)) commonParent = null;

            var group = new GameObject("Group");
            StageUtility.PlaceGameObjectInCurrentStage(group);
            Undo.RegisterCreatedObjectUndo(group, "Group Scene Objects");
            if (commonParent != null) Undo.SetTransformParent(group.transform, commonParent, "Parent Scene Group");
            group.transform.position = GetCombinedBounds(selected).center;
            foreach (GameObject gameObject in selected)
            {
                if (gameObject == null || gameObject == group) continue;
                Undo.SetTransformParent(gameObject.transform, group.transform, "Group Scene Objects");
            }
            GameObjectUtility.EnsureUniqueNameForSibling(group);
            Selection.activeGameObject = group;
            Undo.CollapseUndoOperations(undoGroup);
            MarkScenesDirty(selected.Concat(new[] { group }));
            return group;
        }

        internal static void ResetSelection(bool position, bool rotation, bool scale)
        {
            Transform[] transforms = Selection.transforms.Where(item => item != null).ToArray();
            if (transforms.Length == 0) return;
            Undo.RecordObjects(transforms, "Reset Scene Transforms");
            foreach (Transform transform in transforms)
            {
                if (position) transform.localPosition = Vector3.zero;
                if (rotation) transform.localRotation = Quaternion.identity;
                if (scale) transform.localScale = Vector3.one;
            }
            MarkScenesDirty(transforms.Select(item => item.gameObject));
        }

        internal static void MirrorSelection(BetterSceneAxis axis)
        {
            GameObject anchor = Selection.activeGameObject;
            GameObject[] selected = Selection.gameObjects.Where(item => item != null).ToArray();
            if (anchor == null || selected.Length < 2) return;
            Undo.RecordObjects(selected.Select(item => item.transform).ToArray(), "Mirror Scene Objects");
            int dimension = (int)axis;
            float pivot = anchor.transform.position[dimension];
            foreach (GameObject gameObject in selected)
            {
                if (gameObject == anchor) continue;
                Vector3 position = gameObject.transform.position;
                position[dimension] = pivot - (position[dimension] - pivot);
                gameObject.transform.position = position;
            }
            MarkScenesDirty(selected);
        }

        internal static void SetView(BetterSceneViewDirection direction)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) return;
            Quaternion rotation;
            bool orthographic = direction != BetterSceneViewDirection.Perspective;
            switch (direction)
            {
                case BetterSceneViewDirection.Top: rotation = Quaternion.Euler(90f, 0f, 0f); break;
                case BetterSceneViewDirection.Bottom: rotation = Quaternion.Euler(-90f, 0f, 0f); break;
                case BetterSceneViewDirection.Front: rotation = Quaternion.identity; break;
                case BetterSceneViewDirection.Back: rotation = Quaternion.Euler(0f, 180f, 0f); break;
                case BetterSceneViewDirection.Left: rotation = Quaternion.Euler(0f, 90f, 0f); break;
                case BetterSceneViewDirection.Right: rotation = Quaternion.Euler(0f, -90f, 0f); break;
                default:
                    rotation = Quaternion.Euler(25f, -35f, 0f);
                    orthographic = false;
                    break;
            }
            float zoom = ResolveDirectionalViewZoom(
                BetterSceneSettings.AccountForViewZoom,
                view.size);
            view.in2DMode = false;
            view.LookAt(view.pivot, rotation, zoom, orthographic, true);
            view.size = zoom;
            view.Repaint();
            view.Focus();
        }

        internal static float ResolveDirectionalViewZoom(bool accountForZoom, float currentZoom)
        {
            return accountForZoom
                ? Mathf.Max(0.1f, currentZoom)
                : DefaultViewZoom;
        }

        internal static Camera CreateCameraFromView()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null) return null;
            GameObject created = Create(BetterSceneCreateKind.Camera);
            Camera camera = created.GetComponent<Camera>();
            created.transform.position = view.camera.transform.position;
            created.transform.rotation = view.camera.transform.rotation;
            camera.orthographic = view.orthographic;
            camera.orthographicSize = view.size;
            camera.fieldOfView = view.camera.fieldOfView;
            MarkScenesDirty(new[] { created });
            return camera;
        }
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

        internal static bool TryCalculatePlacementContactOffset(
            BetterSceneSnapMode snapMode,
            GameObject gameObject,
            Vector3 surfacePoint,
            Vector3 surfaceNormal,
            out Vector3 offset)
        {
            offset = Vector3.zero;
            return snapMode == BetterSceneSnapMode.Surface &&
                   TryCalculateSurfaceContactOffset(gameObject, surfacePoint, surfaceNormal, out offset);
        }

        private static bool TryCalculateSurfaceContactOffset(
            GameObject gameObject,
            Vector3 surfacePoint,
            Vector3 surfaceNormal,
            out Vector3 offset)
        {
            offset = Vector3.zero;
            if (gameObject == null || surfaceNormal.sqrMagnitude <= 0.001f) return false;

            Vector3 normal = surfaceNormal.normalized;
            float minimumProjection = float.PositiveInfinity;
            bool found = false;
            foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                float candidate = CalculateMinimumProjection(renderer.localBounds, renderer.localToWorldMatrix, normal);
                if (float.IsNaN(candidate) || float.IsInfinity(candidate)) continue;
                minimumProjection = Mathf.Min(minimumProjection, candidate);
                found = true;
            }

            // Renderer-local bounds retain their orientation and give accurate contact on
            // slopes. Colliders are a useful fallback for invisible placement helpers.
            if (!found)
            {
                foreach (Collider collider in gameObject.GetComponentsInChildren<Collider>(true))
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    float candidate = CalculateMinimumProjection(collider.bounds, Matrix4x4.identity, normal);
                    minimumProjection = Mathf.Min(minimumProjection, candidate);
                    found = true;
                }
                foreach (Collider2D collider in gameObject.GetComponentsInChildren<Collider2D>(true))
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    float candidate = CalculateMinimumProjection(collider.bounds, Matrix4x4.identity, normal);
                    minimumProjection = Mathf.Min(minimumProjection, candidate);
                    found = true;
                }
            }

            if (!found) minimumProjection = Vector3.Dot(gameObject.transform.position, normal);
            offset = CalculateSurfaceContactOffset(minimumProjection, surfacePoint, normal);
            return true;
        }

        internal static Vector3 CalculateSurfaceContactOffset(
            float minimumProjection,
            Vector3 surfacePoint,
            Vector3 surfaceNormal)
        {
            if (surfaceNormal.sqrMagnitude <= 0.001f) return Vector3.zero;
            Vector3 normal = surfaceNormal.normalized;
            return normal * (Vector3.Dot(surfacePoint, normal) - minimumProjection);
        }

        internal static float CalculateMinimumProjection(
            Bounds localBounds,
            Matrix4x4 localToWorld,
            Vector3 surfaceNormal)
        {
            if (surfaceNormal.sqrMagnitude <= 0.001f) return 0f;
            Vector3 normal = surfaceNormal.normalized;
            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            float minimumProjection = float.PositiveInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                float projection = Vector3.Dot(localToWorld.MultiplyPoint3x4(local), normal);
                minimumProjection = Mathf.Min(minimumProjection, projection);
            }
            return minimumProjection;
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

        internal static Vector3 SnapPointToSurface(
            Vector3 point,
            Vector3 surfaceNormal,
            Vector3 increment)
        {
            Vector3 snapped = SnapVector(point, increment);
            if (surfaceNormal.sqrMagnitude <= 0.001f) return snapped;

            Vector3 normal = surfaceNormal.normalized;
            return snapped + normal * Vector3.Dot(point - snapped, normal);
        }

        internal static Vector3 SnapPlacementPointToTarget(
            Vector3 placementPoint,
            Vector3 surfaceNormal,
            Bounds targetBounds,
            Bounds placedBounds,
            Vector3 increment)
        {
            if (surfaceNormal.sqrMagnitude <= 0.001f)
            {
                return SnapPlacementPointToGridAnchor(
                    placementPoint,
                    surfaceNormal,
                    placedBounds,
                    increment);
            }

            Vector3 normal = surfaceNormal.normalized;
            int normalAxis = DominantAxis(normal);
            Vector3 offset = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
            {
                if (axis == normalAxis) continue;
                float step = ResolveSmartSnapStep(
                    placedBounds.size[axis],
                    Mathf.Abs(increment[axis]));
                float slot = CalculateCenteredSnapSlot(
                    placementPoint[axis],
                    targetBounds.min[axis],
                    targetBounds.max[axis],
                    step);
                offset[axis] = slot - placedBounds.center[axis];
            }

            // Preserve contact with the hit plane even when its normal is not axis-aligned.
            offset -= normal * Vector3.Dot(offset, normal);
            return placementPoint + offset;
        }

        internal static Vector3 SnapPlacementPointToGridAnchor(
            Vector3 placementPoint,
            Vector3 surfaceNormal,
            Bounds placedBounds,
            Vector3 increment)
        {
            Vector3 center = placedBounds.center;
            Vector3 snappedCenter = SnapPointToSurface(center, surfaceNormal, increment);
            Vector3 offset = snappedCenter - center;
            if (surfaceNormal.sqrMagnitude > 0.001f)
            {
                Vector3 normal = surfaceNormal.normalized;
                offset -= normal * Vector3.Dot(offset, normal);
            }
            return placementPoint + offset;
        }

        internal static float ResolveSmartSnapStep(float footprint, float increment)
        {
            footprint = Mathf.Abs(footprint);
            increment = Mathf.Abs(increment);
            if (footprint <= 0.001f) return increment;
            if (increment <= 0.001f) return footprint;

            float normalized = Mathf.Max(increment, Mathf.Round(footprint / increment) * increment);
            float tolerance = Mathf.Max(0.001f, increment * 0.05f);
            return Mathf.Abs(footprint - normalized) <= tolerance ? normalized : footprint;
        }

        internal static float CalculateCenteredSnapSlot(
            float cursor,
            float minimum,
            float maximum,
            float step)
        {
            float center = (minimum + maximum) * 0.5f;
            float span = Mathf.Max(0f, maximum - minimum);
            if (step <= 0.001f || span <= 0.001f) return center;

            int slotCount = Mathf.Clamp(Mathf.FloorToInt(span / step + 0.05f), 1, 10000);
            float first = center - (slotCount - 1) * step * 0.5f;
            int index = Mathf.Clamp(Mathf.RoundToInt((cursor - first) / step), 0, slotCount - 1);
            return first + index * step;
        }

        private static int DominantAxis(Vector3 value)
        {
            float x = Mathf.Abs(value.x);
            float y = Mathf.Abs(value.y);
            float z = Mathf.Abs(value.z);
            if (x >= y && x >= z) return 0;
            return y >= z ? 1 : 2;
        }

        internal static GameObject FindPlacementAssetTarget(
            GameObject pickedObject,
            UnityEngine.Object placementAsset)
        {
            if (pickedObject == null || placementAsset == null) return null;
            for (Transform current = pickedObject.transform; current != null; current = current.parent)
            {
                GameObject candidate = current.gameObject;
                if (placementAsset is GameObject gameObjectAsset)
                {
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(candidate);
                    if (source == gameObjectAsset) return candidate;
                }
                else if (placementAsset is Mesh mesh)
                {
                    MeshFilter filter = candidate.GetComponent<MeshFilter>();
                    SkinnedMeshRenderer skinned = candidate.GetComponent<SkinnedMeshRenderer>();
                    if ((filter != null && filter.sharedMesh == mesh) ||
                        (skinned != null && skinned.sharedMesh == mesh))
                    {
                        return candidate;
                    }
                }
                else if (placementAsset is Sprite sprite)
                {
                    SpriteRenderer renderer = candidate.GetComponent<SpriteRenderer>();
                    if (renderer != null && renderer.sprite == sprite) return candidate;
                }
                else if (placementAsset is AudioClip clip)
                {
                    AudioSource source = candidate.GetComponent<AudioSource>();
                    if (source != null && source.clip == clip) return candidate;
                }
            }
            return null;
        }

        internal static bool ErasePlacementAssetTarget(
            GameObject pickedObject,
            UnityEngine.Object placementAsset)
        {
            GameObject target = FindPlacementAssetTarget(pickedObject, placementAsset);
            if (target == null) return false;
            Scene scene = target.scene;
            Undo.SetCurrentGroupName("Erase " + placementAsset.name);
            Undo.DestroyObjectImmediate(target);
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
            BetterSceneDiagnostics.Invalidate();
            return true;
        }

        internal static int BeginEraseUndoGroup(UnityEngine.Object placementAsset)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Erase " + (placementAsset == null ? "Placed Objects" : placementAsset.name));
            return group;
        }

        internal static void EndEraseUndoGroup(int group)
        {
            if (group >= 0) Undo.CollapseUndoOperations(group);
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
            float zoom = Mathf.Max(0.01f, bookmark.Size);
            view.in2DMode = bookmark.In2DMode;
            view.LookAt(bookmark.Pivot, bookmark.Rotation, zoom, bookmark.Orthographic, true);
            view.size = zoom;
            view.Repaint();
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
