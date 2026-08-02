using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.BetterScene
{
    internal static class BetterSceneVisibility
    {
        private sealed class ObjectState
        {
            internal string Id;
            internal bool Hidden;
            internal bool PickingDisabled;
        }

        private static readonly List<ObjectState> snapshot = new List<ObjectState>();
        private static int visibleLayersSnapshot;
        private static int lockedLayersSnapshot;
        private static bool hasSnapshot;

        internal static bool HasSnapshot => hasSnapshot;
        internal static bool IsIsolating => SceneVisibilityManager.instance.IsCurrentStageIsolated();

        internal static void ToggleIsolation(GameObject[] selection)
        {
            SceneVisibilityManager manager = SceneVisibilityManager.instance;
            if (manager.IsCurrentStageIsolated())
            {
                manager.ExitIsolation();
                SceneView.RepaintAll();
                return;
            }
            GameObject[] valid = (selection ?? Array.Empty<GameObject>()).Where(item => item != null).ToArray();
            if (valid.Length == 0) return;
            manager.Isolate(valid, BetterSceneSettings.IncludeDescendants);
            SceneView.RepaintAll();
        }

        internal static void ToggleHidden(GameObject[] selection)
        {
            GameObject[] valid = (selection ?? Array.Empty<GameObject>()).Where(item => item != null).ToArray();
            if (valid.Length == 0) return;
            SceneVisibilityManager manager = SceneVisibilityManager.instance;
            bool hidden = valid.All(item => manager.IsHidden(item, false));
            if (hidden) manager.Show(valid, BetterSceneSettings.IncludeDescendants);
            else manager.Hide(valid, BetterSceneSettings.IncludeDescendants);
            SceneView.RepaintAll();
        }

        internal static void TogglePicking(GameObject[] selection)
        {
            GameObject[] valid = (selection ?? Array.Empty<GameObject>()).Where(item => item != null).ToArray();
            if (valid.Length == 0) return;
            SceneVisibilityManager manager = SceneVisibilityManager.instance;
            bool disabled = valid.All(item => manager.IsPickingDisabled(item, false));
            if (disabled) manager.EnablePicking(valid, BetterSceneSettings.IncludeDescendants);
            else manager.DisablePicking(valid, BetterSceneSettings.IncludeDescendants);
            SceneView.RepaintAll();
        }

        internal static void ShowAndUnlockAll()
        {
            SceneVisibilityManager manager = SceneVisibilityManager.instance;
            if (manager.IsCurrentStageIsolated()) manager.ExitIsolation();
            manager.ShowAll();
            manager.EnableAllPicking();
            Tools.visibleLayers = -1;
            Tools.lockedLayers = 0;
            snapshot.Clear();
            hasSnapshot = false;
            SceneView.RepaintAll();
        }

        internal static void ApplyBand(BetterSceneVisibilityBand band)
        {
            if (band == BetterSceneVisibilityBand.All)
            {
                Restore();
                return;
            }

            CaptureIfNeeded();
            GameObject[] all = FindSceneObjects();
            var visible = new HashSet<GameObject>();
            foreach (GameObject gameObject in all)
            {
                if (!Matches(gameObject, band)) continue;
                AddFamily(gameObject, visible);
            }

            SceneVisibilityManager manager = SceneVisibilityManager.instance;
            if (manager.IsCurrentStageIsolated()) manager.ExitIsolation();
            foreach (GameObject gameObject in all)
            {
                if (visible.Contains(gameObject)) manager.Show(gameObject, false);
                else manager.Hide(gameObject, false);
            }
            SceneView.RepaintAll();
        }

        internal static void ApplyLayerPreset(BetterSceneLayerPreset preset)
        {
            if (preset == null) return;
            CaptureIfNeeded();
            Tools.visibleLayers = preset.VisibleLayers;
            Tools.lockedLayers = preset.LockedLayers;
            SceneView.RepaintAll();
        }

        internal static void Restore()
        {
            if (!hasSnapshot)
            {
                SceneView.RepaintAll();
                return;
            }

            SceneVisibilityManager manager = SceneVisibilityManager.instance;
            if (manager.IsCurrentStageIsolated()) manager.ExitIsolation();
            foreach (ObjectState state in snapshot)
            {
                GameObject gameObject = BetterSceneSelectionHistory.Resolve(state.Id) as GameObject;
                if (gameObject == null) continue;
                if (state.Hidden) manager.Hide(gameObject, false);
                else manager.Show(gameObject, false);
                if (state.PickingDisabled) manager.DisablePicking(gameObject, false);
                else manager.EnablePicking(gameObject, false);
            }
            Tools.visibleLayers = visibleLayersSnapshot;
            Tools.lockedLayers = lockedLayersSnapshot;
            snapshot.Clear();
            hasSnapshot = false;
            SceneView.RepaintAll();
        }

        internal static GameObject[] FindSceneObjects()
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item != null && item.scene.IsValid() && item.scene.isLoaded &&
                               (item.hideFlags & HideFlags.HideAndDontSave) == 0)
                .ToArray();
        }

        internal static bool Matches(GameObject gameObject, BetterSceneVisibilityBand band)
        {
            if (gameObject == null) return false;
            Component[] components = gameObject.GetComponents<Component>();
            string layer = LayerMask.LayerToName(gameObject.layer);
            string searchable = (gameObject.name + " " + layer).ToLowerInvariant();
            switch (band)
            {
                case BetterSceneVisibilityBand.Environment:
                    return gameObject.GetComponent<Renderer>() != null ||
                           gameObject.GetComponent<Terrain>() != null ||
                           searchable.Contains("environment") || searchable.Contains("world") ||
                           searchable.Contains("level") || searchable.Contains("terrain");
                case BetterSceneVisibilityBand.Gameplay:
                    return gameObject.GetComponent<Collider>() != null ||
                           gameObject.GetComponent<Collider2D>() != null ||
                           gameObject.GetComponent<Rigidbody>() != null ||
                           gameObject.GetComponent<Rigidbody2D>() != null ||
                           gameObject.GetComponent<Animator>() != null ||
                           components.Any(IsGameplayComponent) ||
                           searchable.Contains("gameplay") || searchable.Contains("player") || searchable.Contains("enemy");
                case BetterSceneVisibilityBand.Lighting:
                    return gameObject.GetComponent<Light>() != null ||
                           gameObject.GetComponent<ReflectionProbe>() != null ||
                           gameObject.GetComponent<LightProbeGroup>() != null ||
                           searchable.Contains("light") || searchable.Contains("probe");
                case BetterSceneVisibilityBand.Audio:
                    return gameObject.GetComponent<AudioSource>() != null ||
                           gameObject.GetComponent<AudioListener>() != null ||
                           gameObject.GetComponent<AudioReverbZone>() != null ||
                           searchable.Contains("audio") || searchable.Contains("sound") || searchable.Contains("music");
                case BetterSceneVisibilityBand.UI:
                    return gameObject.GetComponent<RectTransform>() != null ||
                           gameObject.GetComponent<Canvas>() != null || searchable.Contains("ui");
                case BetterSceneVisibilityBand.Cameras:
                    return gameObject.GetComponent<Camera>() != null || searchable.Contains("camera");
                case BetterSceneVisibilityBand.Debug:
                    return searchable.Contains("debug") || searchable.Contains("gizmo") ||
                           searchable.Contains("waypoint") || searchable.Contains("spawn") || searchable.Contains("trigger");
                default:
                    return true;
            }
        }

        private static bool IsGameplayComponent(Component component)
        {
            if (component == null) return false;
            Type type = component.GetType();
            string name = type.Name;
            return component is MonoBehaviour || name.Contains("Agent") || name.Contains("Controller") ||
                   name.Contains("Character") || name.Contains("Interact") || name.Contains("Trigger");
        }

        private static void AddFamily(GameObject gameObject, ISet<GameObject> visible)
        {
            Transform cursor = gameObject.transform;
            while (cursor != null)
            {
                visible.Add(cursor.gameObject);
                cursor = cursor.parent;
            }
            foreach (Transform child in gameObject.GetComponentsInChildren<Transform>(true)) visible.Add(child.gameObject);
        }

        private static void CaptureIfNeeded()
        {
            if (hasSnapshot) return;
            SceneVisibilityManager manager = SceneVisibilityManager.instance;
            snapshot.Clear();
            foreach (GameObject gameObject in FindSceneObjects())
            {
                string id = BetterSceneSelectionHistory.GetId(gameObject);
                if (string.IsNullOrEmpty(id)) continue;
                snapshot.Add(new ObjectState
                {
                    Id = id,
                    Hidden = manager.IsHidden(gameObject, false),
                    PickingDisabled = manager.IsPickingDisabled(gameObject, false)
                });
            }
            visibleLayersSnapshot = Tools.visibleLayers;
            lockedLayersSnapshot = Tools.lockedLayers;
            hasSnapshot = true;
        }
    }
}
