using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DansToolbox.EditorTools.BetterHierarchy
{
    [InitializeOnLoad]
    internal static class BetterHierarchyDiagnostics
    {
        private static readonly Dictionary<int, BetterHierarchyDiagnosticFlags> Cache =
            new Dictionary<int, BetterHierarchyDiagnosticFlags>();
        private static int activeAudioListeners;
        private static int activeEventSystems;
        private static int activeMainCameras;
        private static bool countsDirty = true;

        static BetterHierarchyDiagnostics()
        {
            EditorApplication.hierarchyChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            Invalidate();
        }

        internal static BetterHierarchyDiagnosticFlags Get(GameObject gameObject, bool deepScan = false)
        {
            if (gameObject == null)
            {
                return BetterHierarchyDiagnosticFlags.None;
            }

            int id = gameObject.GetInstanceID();
            if (!deepScan && Cache.TryGetValue(id, out BetterHierarchyDiagnosticFlags cached))
            {
                return cached;
            }

            EnsureGlobalCounts();
            BetterHierarchyDiagnosticFlags flags = Evaluate(gameObject, deepScan);
            Cache[id] = flags;
            return flags;
        }

        internal static string GetTooltip(BetterHierarchyDiagnosticFlags flags)
        {
            if (flags == BetterHierarchyDiagnosticFlags.None)
            {
                return string.Empty;
            }

            List<string> messages = new List<string>();
            Add(flags, BetterHierarchyDiagnosticFlags.MissingScript, "Missing script", messages);
            Add(flags, BetterHierarchyDiagnosticFlags.MissingReference, "Missing serialized reference", messages);
            Add(flags, BetterHierarchyDiagnosticFlags.BrokenPrefab, "Missing prefab source", messages);
            Add(flags, BetterHierarchyDiagnosticFlags.InactiveParent, "Inactive parent", messages);
            Add(flags, BetterHierarchyDiagnosticFlags.ZeroScale, "Zero scale", messages);
            Add(flags, BetterHierarchyDiagnosticFlags.NegativeScale, "Negative scale", messages);
            Add(flags, BetterHierarchyDiagnosticFlags.DeepHierarchy, "Deep hierarchy", messages);
            Add(flags, BetterHierarchyDiagnosticFlags.FarFromOrigin, "Far from origin", messages);
            Add(flags, BetterHierarchyDiagnosticFlags.DuplicateAudioListener, "Multiple active AudioListeners", messages);
            Add(flags, BetterHierarchyDiagnosticFlags.EmptyOrganizer, "Empty organizer candidate", messages);
            Add(flags, BetterHierarchyDiagnosticFlags.DuplicateEventSystem, "Multiple active EventSystems", messages);
            Add(flags, BetterHierarchyDiagnosticFlags.DuplicateMainCamera, "Multiple active Main Cameras", messages);
            return string.Join("\n", messages);
        }

        internal static bool IsCritical(BetterHierarchyDiagnosticFlags flags)
        {
            BetterHierarchyDiagnosticFlags critical =
                BetterHierarchyDiagnosticFlags.MissingScript |
                BetterHierarchyDiagnosticFlags.MissingReference |
                BetterHierarchyDiagnosticFlags.BrokenPrefab |
                BetterHierarchyDiagnosticFlags.ZeroScale;
            return (flags & critical) != 0;
        }

        internal static void Invalidate()
        {
            Cache.Clear();
            countsDirty = true;
        }

        private static void EnsureGlobalCounts()
        {
            if (!countsDirty) return;
            countsDirty = false;
            activeAudioListeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(listener => listener != null &&
                                   listener.enabled &&
                                   listener.gameObject.activeInHierarchy &&
                                   listener.gameObject.scene.IsValid());
            Type eventSystemType = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI");
            activeEventSystems = eventSystemType == null
                ? Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                    .Count(behaviour => behaviour != null && behaviour.enabled &&
                                        behaviour.gameObject.activeInHierarchy &&
                                        behaviour.gameObject.scene.IsValid() &&
                                        behaviour.GetType().FullName == "UnityEngine.EventSystems.EventSystem")
                : Resources.FindObjectsOfTypeAll(eventSystemType)
                    .OfType<Behaviour>()
                    .Count(behaviour => behaviour != null && behaviour.enabled &&
                                        behaviour.gameObject.activeInHierarchy &&
                                        behaviour.gameObject.scene.IsValid());
            activeMainCameras = UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(camera => camera != null &&
                                 camera.enabled &&
                                 camera.gameObject.activeInHierarchy &&
                                 camera.gameObject.scene.IsValid() &&
                                 camera.CompareTag("MainCamera"));
        }

        private static BetterHierarchyDiagnosticFlags Evaluate(GameObject gameObject, bool deepScan)
        {
            BetterHierarchyDiagnosticFlags flags = BetterHierarchyDiagnosticFlags.None;
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
            {
                flags |= BetterHierarchyDiagnosticFlags.MissingScript;
            }

            if (PrefabUtility.GetPrefabInstanceStatus(gameObject) == PrefabInstanceStatus.MissingAsset)
            {
                flags |= BetterHierarchyDiagnosticFlags.BrokenPrefab;
            }

            if (gameObject.activeSelf && !gameObject.activeInHierarchy)
            {
                flags |= BetterHierarchyDiagnosticFlags.InactiveParent;
            }

            Vector3 scale = gameObject.transform.localScale;
            if (Mathf.Approximately(scale.x, 0f) ||
                Mathf.Approximately(scale.y, 0f) ||
                Mathf.Approximately(scale.z, 0f))
            {
                flags |= BetterHierarchyDiagnosticFlags.ZeroScale;
            }
            else if (scale.x < 0f || scale.y < 0f || scale.z < 0f)
            {
                flags |= BetterHierarchyDiagnosticFlags.NegativeScale;
            }

            if (GetDepth(gameObject.transform) > 16)
            {
                flags |= BetterHierarchyDiagnosticFlags.DeepHierarchy;
            }

            if (gameObject.transform.position.sqrMagnitude > 100000f * 100000f)
            {
                flags |= BetterHierarchyDiagnosticFlags.FarFromOrigin;
            }

            AudioListener listener = gameObject.GetComponent<AudioListener>();
            if (listener != null && listener.enabled && gameObject.activeInHierarchy && activeAudioListeners > 1)
            {
                flags |= BetterHierarchyDiagnosticFlags.DuplicateAudioListener;
            }

            MonoBehaviour eventSystem = gameObject.GetComponents<MonoBehaviour>()
                .FirstOrDefault(behaviour => behaviour != null &&
                                             behaviour.GetType().FullName == "UnityEngine.EventSystems.EventSystem");
            if (eventSystem != null && eventSystem.enabled && gameObject.activeInHierarchy && activeEventSystems > 1)
            {
                flags |= BetterHierarchyDiagnosticFlags.DuplicateEventSystem;
            }

            Camera camera = gameObject.GetComponent<Camera>();
            if (camera != null && camera.enabled && gameObject.activeInHierarchy &&
                gameObject.CompareTag("MainCamera") && activeMainCameras > 1)
            {
                flags |= BetterHierarchyDiagnosticFlags.DuplicateMainCamera;
            }

            if (gameObject.transform.childCount == 0 &&
                gameObject.GetComponents<Component>().Length == 1 &&
                gameObject.name.IndexOf("group", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                flags |= BetterHierarchyDiagnosticFlags.EmptyOrganizer;
            }

            if (deepScan && HasMissingReference(gameObject))
            {
                flags |= BetterHierarchyDiagnosticFlags.MissingReference;
            }

            return flags;
        }

        private static bool HasMissingReference(GameObject gameObject)
        {
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                try
                {
                    SerializedObject serializedObject = new SerializedObject(component);
                    SerializedProperty property = serializedObject.GetIterator();
                    bool enterChildren = true;
                    while (property.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue == null &&
                            property.objectReferenceInstanceIDValue != 0)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception)
                {
                    // Native serialized objects may reject traversal. Diagnostics stay non-blocking.
                }
            }

            return false;
        }

        private static int GetDepth(Transform transform)
        {
            int depth = 0;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                depth++;
            }

            return depth;
        }

        private static void Add(
            BetterHierarchyDiagnosticFlags flags,
            BetterHierarchyDiagnosticFlags candidate,
            string message,
            ICollection<string> messages)
        {
            if ((flags & candidate) != 0)
            {
                messages.Add(message);
            }
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => Invalidate();
        private static void OnSceneClosed(Scene scene) => Invalidate();
    }
}
