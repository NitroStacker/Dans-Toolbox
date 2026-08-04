using System;
using System.Collections.Generic;
using DansToolbox.RetroVfx;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DansToolbox.EditorTools.RetroVfx
{
    internal sealed class RetroVfxPreviewStage : IDisposable
    {
        private PreviewRenderUtility preview;
        private GameObject root;
        private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();
        private Light effectLight;
        private int recipeHash = int.MinValue;
        private double lastTick;
        private float duration = 1f;
        private float time;
        private float zoom = 1f;
        private bool playing = true;

        internal float Time => time;
        internal bool IsPlaying => playing;

        internal bool Tick(RetroVfxRecipe recipe)
        {
            EnsurePreview();
            if (recipe == null)
            {
                return false;
            }

            if (root == null || recipeHash == int.MinValue)
            {
                Rebuild(recipe, recipe.ComputeStableHash());
                return true;
            }

            double now = EditorApplication.timeSinceStartup;
            float delta = lastTick <= 0d ? 0f : Mathf.Min(0.1f, (float)(now - lastTick));
            lastTick = now;
            if (!playing)
            {
                return false;
            }

            float previousTime = time;
            time += delta;
            if (time > duration)
            {
                if (recipe.loopPreview)
                {
                    time %= duration;
                    SimulateAbsolute(time);
                }
                else
                {
                    time = duration;
                    SimulateDelta(Mathf.Max(0f, duration - previousTime));
                    playing = false;
                }
            }
            else
            {
                SimulateDelta(delta);
            }
            return true;
        }

        internal void Draw(Rect rect, RetroVfxRecipe recipe)
        {
            EnsurePreview();
            HandleZoom(rect);
            if (recipe != null)
            {
                if (root == null || recipeHash == int.MinValue)
                {
                    Rebuild(recipe, recipe.ComputeStableHash());
                }
            }

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            preview.BeginPreview(rect, GUIStyle.none);
            Camera camera = preview.camera;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = RetroVfxGui.PreviewBackground;
            camera.orthographic = true;
            camera.orthographicSize = 2.6f / zoom;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            preview.lights[0].intensity = 1.25f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            preview.lights[1].intensity = 0.65f;
            preview.ambientColor = new Color(0.18f, 0.18f, 0.2f);
            if (effectLight != null && recipe != null && recipe.advanced.lightEnabled)
            {
                float normalized = Mathf.Clamp01(time / duration);
                effectLight.intensity = recipe.advanced.lightIntensity *
                                        Mathf.Max(0f, recipe.advanced.lightIntensityOverLifetime.Evaluate(normalized));
            }
            camera.Render();
            preview.EndAndDrawPreview(rect);

            RetroVfxGui.DrawPreviewOverlay(rect, time, duration, zoom);
        }

        internal void TogglePlayback(RetroVfxRecipe recipe)
        {
            if (playing)
            {
                playing = false;
                return;
            }

            if (time >= duration - 0.001f)
            {
                time = 0f;
                SimulateAbsolute(0f);
            }
            playing = true;
            lastTick = EditorApplication.timeSinceStartup;
        }

        internal void Restart()
        {
            time = 0f;
            playing = true;
            lastTick = EditorApplication.timeSinceStartup;
            SimulateAbsolute(0f);
        }

        internal void Stop()
        {
            playing = false;
            time = 0f;
            SimulateAbsolute(0f);
        }

        internal void Scrub(float value)
        {
            time = Mathf.Max(0f, value);
            playing = false;
            SimulateAbsolute(time);
        }

        internal void Invalidate()
        {
            recipeHash = int.MinValue;
        }

        public void Dispose()
        {
            DestroyRoot();
            preview?.Cleanup();
            preview = null;
        }

        private void EnsurePreview()
        {
            if (preview != null)
            {
                return;
            }

            preview = new PreviewRenderUtility();
            lastTick = EditorApplication.timeSinceStartup;
        }

        private void Rebuild(RetroVfxRecipe recipe, int currentHash)
        {
            DestroyRoot();
            root = RetroVfxEffectBuilder.Build(recipe, true);
            preview.AddSingleGO(root);
            particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            effectLight = root.GetComponentInChildren<Light>(true);
            duration = Mathf.Max(0.01f, RetroVfxEffectBuilder.CalculateDuration(recipe));
            recipeHash = currentHash;
            time = 0f;
            playing = true;
            lastTick = EditorApplication.timeSinceStartup;
            SimulateAbsolute(0f);
        }

        private void SimulateAbsolute(float targetTime)
        {
            foreach (ParticleSystem system in particleSystems)
            {
                if (system != null) system.Simulate(targetTime, false, true, false);
            }
        }

        private void SimulateDelta(float delta)
        {
            if (delta <= 0f) return;
            foreach (ParticleSystem system in particleSystems)
            {
                if (system != null) system.Simulate(delta, false, false, false);
            }
        }

        private void HandleZoom(Rect rect)
        {
            Event current = Event.current;
            if (current.type != EventType.ScrollWheel || !rect.Contains(current.mousePosition))
            {
                return;
            }

            zoom = Mathf.Clamp(zoom * (1f - current.delta.y * 0.04f), 0.45f, 3.5f);
            current.Use();
        }

        private void DestroyRoot()
        {
            if (root == null)
            {
                return;
            }

            HashSet<Object> owned = new HashSet<Object>();
            foreach (ParticleSystemRenderer renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material != null && !UnityEditor.AssetDatabase.Contains(material))
                {
                    owned.Add(material);
                    Texture texture = material.mainTexture;
                    if (texture != null && !UnityEditor.AssetDatabase.Contains(texture))
                    {
                        owned.Add(texture);
                    }
                }
            }

            Object.DestroyImmediate(root);
            foreach (Object item in owned)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }
            root = null;
            particleSystems = Array.Empty<ParticleSystem>();
            effectLight = null;
        }
    }
}
