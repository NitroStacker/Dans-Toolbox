using UnityEngine;
using System;
using System.Reflection;

namespace DansToolbox.RetroVfx
{
    [DisallowMultipleComponent]
    public sealed class RetroVfxPlayer : MonoBehaviour
    {
        public static event Action<RetroVfxPlayer, float, float> CameraShakeRequested;
        public static event Action<RetroVfxPlayer, float> HitStopRequested;
        public static event Action<RetroVfxPlayer, GameObject> DecalRequested;

        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool destroyAfterPlayback;
        [SerializeField] private float duration = 1f;
        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Light effectLight;
        [SerializeField] private float lightPeakIntensity;
        [SerializeField] private AnimationCurve lightIntensity = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [SerializeField] private Component visualEffect;
        [SerializeField] private bool requestCameraShake;
        [SerializeField] private float cameraShakeAmplitude;
        [SerializeField] private float cameraShakeDuration;
        [SerializeField] private bool requestHitStop;
        [SerializeField] private float hitStopDuration;
        [SerializeField] private bool requestDecal;
        [SerializeField] private GameObject decalPrefab;

        private float playbackStartedAt = float.NegativeInfinity;
        private bool childrenCached;
        private bool suppressEnablePlayback;
        private MethodInfo visualEffectReinit;
        private MethodInfo visualEffectPlay;
        private MethodInfo visualEffectStop;

        public float Duration => duration;
        public bool IsPlaying => Time.time - playbackStartedAt <= duration;

        private void Awake()
        {
            CacheChildren();
        }

        private void OnEnable()
        {
            if (playOnEnable && !suppressEnablePlayback)
            {
                Play();
            }
        }

        private void Update()
        {
            float elapsed = Time.time - playbackStartedAt;
            if (effectLight != null)
            {
                float normalized = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                effectLight.intensity = IsPlaying
                    ? lightPeakIntensity * Mathf.Max(0f, lightIntensity.Evaluate(normalized))
                    : 0f;
            }

            if (destroyAfterPlayback && elapsed > duration)
            {
                Destroy(gameObject);
                return;
            }
            if (elapsed > duration)
            {
                enabled = false;
            }
        }

        public void Play()
        {
            CacheChildren();
            if (!enabled)
            {
                suppressEnablePlayback = true;
                enabled = true;
                suppressEnablePlayback = false;
            }
            playbackStartedAt = Time.time;
            foreach (ParticleSystem system in particleSystems)
            {
                if (system == null)
                {
                    continue;
                }

                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Play(true);
            }

            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.Stop();
                audioSource.Play();
            }

            InvokeVisualEffect(visualEffectReinit);
            InvokeVisualEffect(visualEffectPlay);

            if (requestCameraShake)
            {
                CameraShakeRequested?.Invoke(this, cameraShakeAmplitude, cameraShakeDuration);
            }
            if (requestHitStop)
            {
                HitStopRequested?.Invoke(this, hitStopDuration);
            }
            if (requestDecal)
            {
                DecalRequested?.Invoke(this, decalPrefab);
            }
            if (effectLight == null && !destroyAfterPlayback)
            {
                enabled = false;
            }
        }

        public void Play(Vector3 position, Vector3 forward)
        {
            transform.position = position;
            if (forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }

            Play();
        }

        public void Stop()
        {
            CacheChildren();
            playbackStartedAt = float.NegativeInfinity;
            foreach (ParticleSystem system in particleSystems)
            {
                if (system != null)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (audioSource != null)
            {
                audioSource.Stop();
            }

            if (effectLight != null)
            {
                effectLight.intensity = 0f;
            }

            InvokeVisualEffect(visualEffectStop);
            enabled = false;
        }

        public void Configure(
            float effectDuration,
            ParticleSystem[] systems,
            AudioSource source,
            Light light,
            float peakLightIntensity,
            AnimationCurve lightCurve,
            bool cameraShake,
            float shakeAmplitude,
            float shakeDuration,
            bool hitStop,
            float stopDuration,
            bool decal,
            GameObject decalAsset)
        {
            duration = Mathf.Max(0.01f, effectDuration);
            particleSystems = systems;
            audioSource = source;
            effectLight = light;
            lightPeakIntensity = Mathf.Max(0f, peakLightIntensity);
            lightIntensity = lightCurve == null
                ? AnimationCurve.Linear(0f, 1f, 1f, 0f)
                : new AnimationCurve(lightCurve.keys);
            requestCameraShake = cameraShake;
            cameraShakeAmplitude = Mathf.Max(0f, shakeAmplitude);
            cameraShakeDuration = Mathf.Max(0.01f, shakeDuration);
            requestHitStop = hitStop;
            hitStopDuration = Mathf.Clamp(stopDuration, 0f, 0.25f);
            requestDecal = decal;
            decalPrefab = decalAsset;
            childrenCached = false;
        }

        private void CacheChildren()
        {
            if (childrenCached)
            {
                return;
            }
            if (particleSystems == null || particleSystems.Length == 0)
            {
                particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            }

            audioSource ??= GetComponentInChildren<AudioSource>(true);
            effectLight ??= GetComponentInChildren<Light>(true);
            if (visualEffect == null)
            {
                foreach (Component component in GetComponentsInChildren<Component>(true))
                {
                    if (component != null &&
                        string.Equals(component.GetType().FullName, "UnityEngine.VFX.VisualEffect", StringComparison.Ordinal))
                    {
                        visualEffect = component;
                        break;
                    }
                }
            }
            if (visualEffect != null)
            {
                Type type = visualEffect.GetType();
                visualEffectReinit = FindVisualEffectMethod(type, "Reinit");
                visualEffectPlay = FindVisualEffectMethod(type, "Play");
                visualEffectStop = FindVisualEffectMethod(type, "Stop");
            }
            childrenCached = true;
        }

        private static MethodInfo FindVisualEffectMethod(Type type, string methodName)
        {
            return type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
        }

        private void InvokeVisualEffect(MethodInfo method)
        {
            if (visualEffect != null) method?.Invoke(visualEffect, null);
        }
    }
}
