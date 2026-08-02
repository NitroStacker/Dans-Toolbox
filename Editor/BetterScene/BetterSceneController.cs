using System;
using System.Linq;
using DansToolbox.Editor;
using DansToolbox.EditorTools.BetterConsole;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DansToolbox.EditorTools.BetterScene
{
    [InitializeOnLoad]
    internal static class BetterSceneController
    {
        private const string ModeKey = "DansToolbox.BetterScene.Mode";
        private const string SnapKey = "DansToolbox.BetterScene.Snap";
        private static BetterSceneMode mode;
        private static BetterSceneSnapMode snapMode;
        private static bool hasMeasureStart;
        private static bool hasMeasureEnd;
        private static Vector3 measureStart;
        private static Vector3 measureEnd;
        private static Vector3 hoverPoint;
        private static Vector3 hoverNormal = Vector3.up;
        private static GameObject hoverObject;
        private static bool hasHoverPoint;

        static BetterSceneController()
        {
            mode = (BetterSceneMode)Mathf.Clamp(SessionState.GetInt(ModeKey, 0), 0, 3);
            snapMode = (BetterSceneSnapMode)Mathf.Clamp(SessionState.GetInt(SnapKey, 2), 0, 3);
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            Selection.selectionChanged += Repaint;
            EditorApplication.hierarchyChanged += Repaint;
            Undo.undoRedoPerformed += Repaint;
            BetterSceneSettings.Changed += Repaint;
            BetterSceneSelectionHistory.Changed += Repaint;
            BetterConsoleDiagnosticBridge.Changed += Repaint;
            DansToolboxTheme.Changed += Repaint;
        }

        internal static event Action Changed;
        internal static BetterSceneMode Mode => mode;
        internal static BetterSceneSnapMode SnapMode => snapMode;
        internal static BetterSceneMeasurement Measurement => new BetterSceneMeasurement(measureStart, measureEnd, hasMeasureStart, hasMeasureEnd);

        internal static void SetMode(BetterSceneMode value)
        {
            if (mode == value) return;
            mode = value;
            SessionState.SetInt(ModeKey, (int)value);
            if (mode != BetterSceneMode.Measure) hasMeasureEnd = false;
            Changed?.Invoke();
            Repaint();
        }

        internal static void SetSnapMode(BetterSceneSnapMode value)
        {
            if (snapMode == value) return;
            snapMode = value;
            SessionState.SetInt(SnapKey, (int)value);
            Changed?.Invoke();
            Repaint();
        }

        internal static void ClearMeasurement()
        {
            hasMeasureStart = false;
            hasMeasureEnd = false;
            measureStart = Vector3.zero;
            measureEnd = Vector3.zero;
            Changed?.Invoke();
            Repaint();
        }

        internal static bool CanPlaceAsset(UnityEngine.Object asset)
        {
            return asset is GameObject || asset is Sprite || asset is Mesh || asset is AudioClip;
        }

        internal static GameObject PlaceAsset(
            UnityEngine.Object asset,
            Vector3 point,
            Vector3 normal,
            GameObject surfaceObject)
        {
            if (!CanPlaceAsset(asset)) return null;
            GameObject created;
            if (asset is GameObject gameObjectAsset)
            {
                created = PrefabUtility.InstantiatePrefab(gameObjectAsset) as GameObject;
                if (created == null) created = UnityEngine.Object.Instantiate(gameObjectAsset);
            }
            else if (asset is Sprite sprite)
            {
                created = new GameObject(sprite.name);
                created.AddComponent<SpriteRenderer>().sprite = sprite;
            }
            else if (asset is Mesh mesh)
            {
                created = new GameObject(mesh.name);
                created.AddComponent<MeshFilter>().sharedMesh = mesh;
                created.AddComponent<MeshRenderer>();
            }
            else
            {
                AudioClip clip = (AudioClip)asset;
                created = new GameObject(clip.name);
                created.AddComponent<AudioSource>().clip = clip;
            }

            if (created == null) return null;
            StageUtility.PlaceGameObjectInCurrentStage(created);
            Undo.RegisterCreatedObjectUndo(created, "Place " + asset.name);
            created.transform.position = point;
            if (BetterSceneSettings.AlignToSurface && normal.sqrMagnitude > 0.001f && !(asset is Sprite))
            {
                created.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal.normalized);
            }
            if (BetterSceneSettings.ParentToSurface && surfaceObject != null &&
                surfaceObject.scene == created.scene && !surfaceObject.transform.IsChildOf(created.transform))
            {
                Undo.SetTransformParent(created.transform, surfaceObject.transform, "Parent Placed Object");
            }
            if (Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up)) > 0.92f &&
                BetterSceneOperations.TryGetBounds(created, out Bounds bounds))
            {
                created.transform.position += Vector3.up * (point.y - bounds.min.y);
            }
            GameObjectUtility.EnsureUniqueNameForSibling(created);
            Selection.activeGameObject = created;
            if (created.scene.IsValid()) EditorSceneManager.MarkSceneDirty(created.scene);
            BetterSceneDiagnostics.Invalidate();
            if (!BetterSceneSettings.KeepPlacing) SetMode(BetterSceneMode.Select);
            return created;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!DansToolboxSettings.IsToolEnabled(DansToolboxTools.BetterSceneId)) return;
            Event current = Event.current;
            if (current.type == EventType.MouseMove || current.type == EventType.DragUpdated) sceneView.Repaint();

            hasHoverPoint = TryGetWorldPoint(sceneView, current.mousePosition, out hoverPoint, out hoverNormal, out hoverObject);
            HandleEscape(current);
            HandleAssetDrag(current);
            if (mode == BetterSceneMode.Place) HandlePlacement(current);
            else if (mode == BetterSceneMode.Measure) HandleMeasurement(current);

            DrawSpatialFeedback();
            DrawMeasurement();
            DrawPlacementPreview();
            if (BetterSceneSettings.OverlayVisible) DrawOverlay(sceneView);
        }

        private static void HandleEscape(Event current)
        {
            if (current.type != EventType.KeyDown || current.keyCode != KeyCode.Escape) return;
            if (mode == BetterSceneMode.Measure && (hasMeasureStart || hasMeasureEnd)) ClearMeasurement();
            else SetMode(BetterSceneMode.Select);
            current.Use();
        }

        private static void HandleAssetDrag(Event current)
        {
            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform) return;
            UnityEngine.Object asset = DragAndDrop.objectReferences.FirstOrDefault(CanPlaceAsset);
            if (asset == null || !hasHoverPoint) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                BetterSceneSettings.PlacementAsset = asset;
                PlaceAsset(asset, hoverPoint, hoverNormal, hoverObject);
            }
            current.Use();
        }

        private static void HandlePlacement(Event current)
        {
            UnityEngine.Object asset = BetterSceneSettings.PlacementAsset;
            if (!CanPlaceAsset(asset) || !hasHoverPoint) return;
            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return;
            }
            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                PlaceAsset(asset, hoverPoint, hoverNormal, hoverObject);
                current.Use();
            }
        }

        private static void HandleMeasurement(Event current)
        {
            if (!hasHoverPoint) return;
            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return;
            }
            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                if (!hasMeasureStart || hasMeasureEnd)
                {
                    measureStart = hoverPoint;
                    measureEnd = hoverPoint;
                    hasMeasureStart = true;
                    hasMeasureEnd = false;
                }
                else
                {
                    measureEnd = hoverPoint;
                    hasMeasureEnd = true;
                }
                Changed?.Invoke();
                current.Use();
            }
            else if (current.type == EventType.MouseMove && hasMeasureStart && !hasMeasureEnd)
            {
                measureEnd = hoverPoint;
            }
        }

        private static bool TryGetWorldPoint(
            SceneView sceneView,
            Vector2 guiPoint,
            out Vector3 point,
            out Vector3 normal,
            out GameObject surfaceObject)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
            if (Physics.Raycast(ray, out RaycastHit hit, 100000f, ~0, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = hit.normal;
                surfaceObject = hit.collider == null ? null : hit.collider.gameObject;
                if (snapMode == BetterSceneSnapMode.Vertex) point = FindNearestVertex(guiPoint, hit, point);
                else if (snapMode == BetterSceneSnapMode.Grid) point = BetterSceneOperations.SnapVector(point, EditorSnapSettings.move);
                return true;
            }

            RaycastHit2D hit2D = Physics2D.GetRayIntersection(ray, 100000f, ~0);
            if (hit2D.collider != null)
            {
                point = hit2D.point;
                normal = hit2D.normal.sqrMagnitude > 0.001f ? hit2D.normal : Vector3.forward;
                surfaceObject = hit2D.collider.gameObject;
                if (snapMode == BetterSceneSnapMode.Grid) point = BetterSceneOperations.SnapVector(point, EditorSnapSettings.move);
                return true;
            }

            Plane plane = sceneView.in2DMode
                ? new Plane(Vector3.forward, Vector3.zero)
                : new Plane(Vector3.up, Selection.activeTransform == null ? Vector3.zero : Selection.activeTransform.position);
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                normal = sceneView.in2DMode ? Vector3.forward : Vector3.up;
                surfaceObject = null;
                if (snapMode == BetterSceneSnapMode.Grid) point = BetterSceneOperations.SnapVector(point, EditorSnapSettings.move);
                return true;
            }

            point = default;
            normal = Vector3.up;
            surfaceObject = null;
            return false;
        }

        private static Vector3 FindNearestVertex(Vector2 guiPoint, RaycastHit hit, Vector3 fallback)
        {
            Mesh mesh = null;
            Transform meshTransform = null;
            MeshFilter filter = hit.collider == null ? null : hit.collider.GetComponentInParent<MeshFilter>();
            if (filter != null)
            {
                mesh = filter.sharedMesh;
                meshTransform = filter.transform;
            }
            else
            {
                SkinnedMeshRenderer skinned = hit.collider == null ? null : hit.collider.GetComponentInParent<SkinnedMeshRenderer>();
                if (skinned != null) { mesh = skinned.sharedMesh; meshTransform = skinned.transform; }
            }
            if (mesh == null || meshTransform == null) return fallback;
            try
            {
                Vector3[] vertices = mesh.vertices;
                int step = Mathf.Max(1, vertices.Length / 5000);
                float best = 18f * 18f;
                Vector3 result = fallback;
                for (int index = 0; index < vertices.Length; index += step)
                {
                    Vector3 world = meshTransform.TransformPoint(vertices[index]);
                    float distance = (HandleUtility.WorldToGUIPoint(world) - guiPoint).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    result = world;
                }
                return result;
            }
            catch (UnityException)
            {
                return fallback;
            }
        }

        private static void DrawSpatialFeedback()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0) return;
            DansToolboxPalette palette = DansToolboxTheme.Current;
            CompareFunction previousZ = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            if (BetterSceneSettings.DrawSelectionBounds)
            {
                Handles.color = new Color(palette.Accent.r, palette.Accent.g, palette.Accent.b, 0.72f);
                foreach (GameObject gameObject in selected)
                {
                    if (!BetterSceneOperations.TryGetBounds(gameObject, out Bounds bounds)) continue;
                    Handles.DrawWireCube(bounds.center, bounds.size);
                }
            }
            if (BetterSceneSettings.DrawPivot)
            {
                Handles.color = palette.Signal;
                foreach (GameObject gameObject in selected)
                {
                    float size = HandleUtility.GetHandleSize(gameObject.transform.position) * 0.09f;
                    Handles.DrawLine(gameObject.transform.position - Vector3.right * size, gameObject.transform.position + Vector3.right * size);
                    Handles.DrawLine(gameObject.transform.position - Vector3.up * size, gameObject.transform.position + Vector3.up * size);
                    Handles.DrawLine(gameObject.transform.position - Vector3.forward * size, gameObject.transform.position + Vector3.forward * size);
                }
            }
            if (BetterSceneSettings.DrawDiagnostics && mode == BetterSceneMode.Review)
            {
                BetterSceneDiagnosticReport report = BetterSceneDiagnostics.Current;
                Bounds bounds = BetterSceneOperations.GetCombinedBounds(selected);
                GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    normal = { textColor = report.Errors > 0 ? palette.Danger : report.Warnings > 0 ? palette.Warning : palette.Success }
                };
                Handles.Label(bounds.max, report.Badge + "  " + selected.Length + " SELECTED", style);
            }
            Handles.zTest = previousZ;
        }

        private static void DrawMeasurement()
        {
            if (!hasMeasureStart) return;
            Vector3 end = hasMeasureEnd ? measureEnd : hoverPoint;
            DansToolboxPalette palette = DansToolboxTheme.Current;
            Handles.color = hasMeasureEnd ? palette.Success : palette.Signal;
            Handles.DrawAAPolyLine(3f, measureStart, end);
            float sizeA = HandleUtility.GetHandleSize(measureStart) * 0.04f;
            float sizeB = HandleUtility.GetHandleSize(end) * 0.04f;
            Handles.DrawWireDisc(measureStart, SceneView.lastActiveSceneView == null ? Vector3.up : SceneView.lastActiveSceneView.camera.transform.forward, sizeA);
            Handles.DrawWireDisc(end, SceneView.lastActiveSceneView == null ? Vector3.up : SceneView.lastActiveSceneView.camera.transform.forward, sizeB);
            Vector3 delta = end - measureStart;
            Handles.Label((measureStart + end) * 0.5f,
                delta.magnitude.ToString("0.###") + " m  ·  " + FormatVector(delta),
                new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = palette.Text } });
        }

        private static void DrawPlacementPreview()
        {
            UnityEngine.Object asset = BetterSceneSettings.PlacementAsset;
            bool dragging = DragAndDrop.objectReferences.Any(CanPlaceAsset);
            if (!hasHoverPoint || (!dragging && (mode != BetterSceneMode.Place || !CanPlaceAsset(asset)))) return;
            DansToolboxPalette palette = DansToolboxTheme.Current;
            float size = HandleUtility.GetHandleSize(hoverPoint) * 0.12f;
            Handles.color = new Color(palette.Signal.r, palette.Signal.g, palette.Signal.b, 0.9f);
            Handles.DrawWireDisc(hoverPoint, hoverNormal, size);
            Handles.DrawLine(hoverPoint, hoverPoint + hoverNormal * size * 1.5f);
            UnityEngine.Object labelAsset = dragging ? DragAndDrop.objectReferences.FirstOrDefault(CanPlaceAsset) : asset;
            if (labelAsset != null)
            {
                Handles.Label(hoverPoint + hoverNormal * size * 1.7f, labelAsset.name,
                    new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = palette.Signal } });
            }
        }

        private static void DrawOverlay(SceneView sceneView)
        {
            Handles.BeginGUI();
            DansToolboxPalette palette = DansToolboxTheme.Current;
            // Leave the native Scene tool strip unobstructed while keeping the
            // controls close enough to feel like part of the Scene view.
            float width = Mathf.Min(430f, Mathf.Max(270f, sceneView.position.width - 64f));
            Rect panel = new Rect(48f, 42f, width, 34f);
            BetterSceneGui.Panel(panel, false, true);
            EditorGUI.DrawRect(new Rect(panel.x, panel.y, 3f, panel.height), palette.Accent);
            float x = panel.x + 7f;
            if (BetterSceneGui.Button(new Rect(x, panel.y + 6f, 22f, 22f), new GUIContent("<", "Selection back"), false, BetterSceneSelectionHistory.CanBack)) BetterSceneSelectionHistory.Back();
            x += 25f;
            if (BetterSceneGui.Button(new Rect(x, panel.y + 6f, 22f, 22f), new GUIContent(">", "Selection forward"), false, BetterSceneSelectionHistory.CanForward)) BetterSceneSelectionHistory.Forward();
            x += 28f;
            foreach (BetterSceneMode candidate in Enum.GetValues(typeof(BetterSceneMode)))
            {
                string glyph = candidate == BetterSceneMode.Select ? "S" : candidate == BetterSceneMode.Place ? "P" : candidate == BetterSceneMode.Measure ? "M" : "R";
                if (BetterSceneGui.Button(new Rect(x, panel.y + 6f, 25f, 22f), new GUIContent(glyph, candidate.ToString()), mode == candidate)) SetMode(candidate);
                x += 28f;
            }
            x += 2f;
            GameObject active = Selection.activeGameObject;
            BetterSceneDiagnosticReport report = BetterSceneDiagnostics.Current;
            float actionsX = panel.xMax - 88f;
            if (BetterSceneGui.Button(new Rect(actionsX, panel.y + 6f, 25f, 22f), new GUIContent("F", "Frame selection"), false, active != null)) BetterSceneOperations.FrameSelection();
            actionsX += 28f;
            if (BetterSceneGui.Button(new Rect(actionsX, panel.y + 6f, 25f, 22f), new GUIContent("I", "Isolate / restore"), BetterSceneVisibility.IsIsolating, active != null)) BetterSceneVisibility.ToggleIsolation(Selection.gameObjects);
            actionsX += 28f;
            if (BetterSceneGui.Button(
                    new Rect(actionsX, panel.y + 6f, 25f, 22f),
                    new GUIContent("!", report.Console.Tooltip),
                    report.Console.HasSignals,
                    report.Console.Total > 0,
                    report.Console.Errors > 0 ? palette.Danger : palette.Warning))
            {
                BetterConsoleDiagnosticBridge.OpenForTargets(Selection.objects);
            }
            Rect labelRect = new Rect(x, panel.y + 5f, Mathf.Max(0f, actionsX - x - 8f), 24f);
            string label = active == null ? "NO SELECTION" : active.name;
            GUI.Label(labelRect, label, BetterSceneGui.Label);
            Handles.EndGUI();
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("0.##") + ", " + value.y.ToString("0.##") + ", " + value.z.ToString("0.##") + ")";
        }

        private static void Repaint()
        {
            Changed?.Invoke();
            SceneView.RepaintAll();
            foreach (BetterSceneWindow window in Resources.FindObjectsOfTypeAll<BetterSceneWindow>()) window.Repaint();
        }
    }
}
