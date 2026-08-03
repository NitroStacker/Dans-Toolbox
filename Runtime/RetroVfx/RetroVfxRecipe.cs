using System;
using System.Collections.Generic;
using UnityEngine;

namespace DansToolbox.RetroVfx
{
    public enum RetroVfxEffectFamily
    {
        Impact = 0,
        Explosion = 1,
        MuzzleFlash = 2,
        Smoke = 3,
        EnergyBurst = 4,
        Pickup = 5,
        Custom = 6,
        Blood = 7,
        SwordSwing = 8,
        Magic = 9,
        ItemShine = 10,
        Environment = 11
    }

    public enum RetroVfxLayerKind
    {
        Flash = 0,
        Burst = 1,
        Sparks = 2,
        Ring = 3,
        Smoke = 4,
        Debris = 5,
        Trail = 6,
        Flipbook = 7,
        Arc = 8,
        Splat = 9,
        Aura = 10,
        Beam = 11
    }

    public enum RetroVfxParticleShape
    {
        Point,
        Circle,
        Cone,
        Sphere,
        Box
    }

    public enum RetroVfxSpriteStyle
    {
        Auto,
        SoftDisc,
        PixelExplosion,
        PixelSmoke,
        PixelChunk,
        Spark,
        Starburst,
        Ring,
        Shockwave,
        SlashArc,
        Crescent,
        BloodDrop,
        BloodSplat,
        MuzzleFlash,
        Glint,
        Rune,
        Leaf,
        Bubble,
        Beam
    }

    public enum RetroVfxMotionMode
    {
        Radial,
        Directional,
        Stationary,
        Rising,
        Falling,
        Drift
    }

    public enum RetroVfxBlendMode
    {
        Alpha,
        Additive,
        Premultiply,
        Multiply
    }

    public enum RetroVfxArtStyle
    {
        Pixel8,
        Pixel16,
        Crisp2D,
        StylizedToon,
        SoftMagic,
        Custom
    }

    public enum RetroVfxPhase
    {
        Anticipation,
        Primary,
        Secondary,
        Sustain,
        Decay
    }

    public enum RetroVfxSourceMode
    {
        Procedural,
        SourceLibrary,
        Texture,
        Flipbook,
        Mesh,
        Material
    }

    public enum RetroVfxRenderGeometry
    {
        Billboard,
        StretchedBillboard,
        Mesh,
        ParticleTrail
    }

    public enum RetroVfxNoiseProfile
    {
        None,
        SoftTurbulence,
        RollingSmoke,
        ChaoticFire,
        ElectricJitter,
        WindShear
    }

    public enum RetroVfxSpawnEvent
    {
        None,
        Birth,
        Death,
        Collision,
        Trigger
    }

    public enum RetroVfxOutputMode
    {
        ParticlePrefab,
        Flipbook,
        Both
    }

    [Serializable]
    public sealed class RetroVfxLayer
    {
        public string name = "Layer";
        public bool enabled = true;
        public bool locked;
        public RetroVfxPhase phase = RetroVfxPhase.Primary;
        public RetroVfxLayerKind kind = RetroVfxLayerKind.Burst;
        public RetroVfxParticleShape shape = RetroVfxParticleShape.Point;
        public RetroVfxSpriteStyle spriteStyle = RetroVfxSpriteStyle.Auto;
        public RetroVfxMotionMode motion = RetroVfxMotionMode.Radial;
        public RetroVfxBlendMode blendMode = RetroVfxBlendMode.Alpha;
        public RetroVfxSourceMode sourceMode = RetroVfxSourceMode.Procedural;
        public RetroVfxRenderGeometry renderGeometry = RetroVfxRenderGeometry.Billboard;
        public string sourcePackId = string.Empty;
        public Texture2D sourceTexture;
        public Mesh sourceMesh;
        public Material materialOverride;
        [Min(1)] public int sourceColumns = 1;
        [Min(1)] public int sourceRows = 1;
        [Min(1f)] public float sourceFramesPerSecond = 24f;
        public bool sourceLoop;
        [Min(1)] public int count = 12;
        [Min(0f)] public float rateOverTime;
        [Min(1)] public int burstCount = 1;
        [Min(0f)] public float burstInterval;
        [Min(0f)] public float delay;
        [Min(0.01f)] public float lifetime = 0.35f;
        [Min(0f)] public float speed = 4f;
        [Range(0f, 1f)] public float speedRandomness = 0.18f;
        [Min(0.001f)] public float size = 0.25f;
        [Range(0f, 1f)] public float sizeRandomness = 0.12f;
        [Range(0f, 360f)] public float spread = 360f;
        [Min(0f)] public float emissionRadius = 0.015f;
        public Vector2 offset;
        public Vector2 aspect = Vector2.one;
        public Vector2 velocity;
        public float gravity;
        public float rotation;
        public float rotationSpeed;
        public bool randomRotation;
        [Min(0f)] public float stretch = 2.2f;
        public Color startColor = Color.white;
        public Color endColor = new Color(1f, 0.35f, 0.05f, 0f);
        public Gradient colorOverLifetime;
        public AnimationCurve sizeOverLifetime = new AnimationCurve(
            new Keyframe(0f, 0.15f),
            new Keyframe(0.15f, 1f),
            new Keyframe(1f, 0f));

        [Header("Motion detail")]
        public RetroVfxNoiseProfile noiseProfile;
        [Min(0f)] public float noiseStrength = 0.3f;
        [Min(0.01f)] public float noiseFrequency = 0.55f;
        public float noiseScrollSpeed = 0.2f;
        [Range(1, 3)] public int noiseOctaves = 1;
        [Range(0f, 1f)] public float drag;

        [Header("Trail")]
        public bool trailEnabled;
        [Min(0.01f)] public float trailLifetime = 0.18f;
        [Range(0f, 1f)] public float trailRatio = 1f;
        [Min(0.001f)] public float trailWidth = 0.12f;
        public Color trailColor = Color.white;

        [Header("Surface")]
        [Range(0f, 1f)] public float dissolve;
        [Range(0f, 1f)] public float edgeGlow = 0.35f;
        [Range(0f, 2f)] public float emission = 1f;
        public Vector2 flowSpeed;
        public bool softParticles = true;

        [Header("Relationships")]
        public int spawnFromLayer = -1;
        public RetroVfxSpawnEvent spawnEvent;
        public bool collisionEnabled;
        [Range(0f, 1f)] public float collisionDampen = 0.25f;
        [Range(0f, 1f)] public float collisionBounce = 0.15f;

        public RetroVfxLayer Clone()
        {
            RetroVfxLayer clone = (RetroVfxLayer)MemberwiseClone();
            clone.sizeOverLifetime = sizeOverLifetime == null
                ? AnimationCurve.Linear(0f, 1f, 1f, 0f)
                : new AnimationCurve(sizeOverLifetime.keys);
            clone.colorOverLifetime = CloneGradient(colorOverLifetime);
            return clone;
        }

        public void Normalize()
        {
            name = string.IsNullOrWhiteSpace(name) ? kind.ToString() : name.Trim();
            count = Mathf.Clamp(count, 1, 4096);
            rateOverTime = Mathf.Clamp(rateOverTime, 0f, 4096f);
            burstCount = Mathf.Clamp(burstCount, 1, 64);
            burstInterval = Mathf.Clamp(burstInterval, 0f, 30f);
            delay = Mathf.Max(0f, delay);
            lifetime = Mathf.Clamp(lifetime, 0.01f, 30f);
            speed = Mathf.Clamp(speed, 0f, 100f);
            speedRandomness = Mathf.Clamp01(speedRandomness);
            size = Mathf.Clamp(size, 0.001f, 100f);
            sizeRandomness = Mathf.Clamp01(sizeRandomness);
            spread = Mathf.Clamp(spread, 0f, 360f);
            emissionRadius = Mathf.Clamp(emissionRadius, 0f, 100f);
            aspect.x = Mathf.Clamp(aspect.x, 0.01f, 100f);
            aspect.y = Mathf.Clamp(aspect.y, 0.01f, 100f);
            gravity = Mathf.Clamp(gravity, -10f, 10f);
            rotationSpeed = Mathf.Clamp(rotationSpeed, -1440f, 1440f);
            stretch = Mathf.Clamp(stretch, 0f, 20f);
            sourceColumns = Mathf.Clamp(sourceColumns, 1, 32);
            sourceRows = Mathf.Clamp(sourceRows, 1, 32);
            sourceFramesPerSecond = Mathf.Clamp(sourceFramesPerSecond, 1f, 240f);
            noiseStrength = Mathf.Clamp(noiseStrength, 0f, 20f);
            noiseFrequency = Mathf.Clamp(noiseFrequency, 0.01f, 20f);
            noiseScrollSpeed = Mathf.Clamp(noiseScrollSpeed, -20f, 20f);
            noiseOctaves = Mathf.Clamp(noiseOctaves, 1, 3);
            drag = Mathf.Clamp01(drag);
            trailLifetime = Mathf.Clamp(trailLifetime, 0.01f, 30f);
            trailRatio = Mathf.Clamp01(trailRatio);
            trailWidth = Mathf.Clamp(trailWidth, 0.001f, 100f);
            dissolve = Mathf.Clamp01(dissolve);
            edgeGlow = Mathf.Clamp01(edgeGlow);
            emission = Mathf.Clamp(emission, 0f, 2f);
            spawnFromLayer = Mathf.Max(-1, spawnFromLayer);
            collisionDampen = Mathf.Clamp01(collisionDampen);
            collisionBounce = Mathf.Clamp01(collisionBounce);
            sizeOverLifetime ??= AnimationCurve.Linear(0f, 1f, 1f, 0f);
            colorOverLifetime ??= CreateDefaultGradient(startColor, endColor);
            if (kind == RetroVfxLayerKind.Trail && renderGeometry == RetroVfxRenderGeometry.Billboard)
            {
                renderGeometry = RetroVfxRenderGeometry.StretchedBillboard;
            }
        }

        private static Gradient CloneGradient(Gradient source)
        {
            if (source == null)
            {
                return null;
            }
            Gradient clone = new Gradient();
            clone.SetKeys(source.colorKeys, source.alphaKeys);
            return clone;
        }

        private static Gradient CreateDefaultGradient(Color start, Color end)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
            return gradient;
        }
    }

    [Serializable]
    public sealed class RetroVfxAdvancedSettings
    {
        [Header("Distortion")]
        public bool distortionEnabled;
        [Range(0f, 1f)] public float distortionStrength = 0.12f;
        [Min(0.01f)] public float distortionSize = 1.4f;
        public Material distortionMaterial;

        [Header("Custom rendering")]
        public bool productionShader = true;
        public Material customMaterial;
        public Shader customShader;
        public Texture2D dissolveTexture;
        public Texture2D flowTexture;
        [Range(0f, 1f)] public float globalDissolve;
        [Range(0f, 2f)] public float globalEmission = 1f;
        [Range(0f, 2f)] public float globalEdgeGlow = 0.35f;
        public bool softParticles = true;
        public bool flipbookBlending = true;

        [Header("VFX Graph")]
        public UnityEngine.Object vfxGraphAsset;

        [Header("External effect layer")]
        public bool externalEffectEnabled;
        public GameObject externalEffectPrefab;
        public Vector3 externalEffectPosition;
        public Vector3 externalEffectRotation;
        public Vector3 externalEffectScale = Vector3.one;

        [Header("Flipbook import")]
        public Texture2D importedFlipbook;
        [Min(1)] public int flipbookColumns = 4;
        [Min(1)] public int flipbookRows = 4;
        [Min(1f)] public float flipbookFramesPerSecond = 24f;
        public bool flipbookLoop;

        [Header("Lighting")]
        public bool lightEnabled;
        public LightType lightType = LightType.Point;
        public Color lightColor = new Color(1f, 0.45f, 0.12f);
        [Min(0f)] public float lightIntensity = 3f;
        [Min(0.01f)] public float lightRange = 4f;
        public LightShadows lightShadows = LightShadows.None;
        public AnimationCurve lightIntensityOverLifetime = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.22f, 0.72f),
            new Keyframe(1f, 0f));

        [Header("Scene response")]
        public bool cameraShakeEnabled;
        [Range(0f, 2f)] public float cameraShakeAmplitude = 0.25f;
        [Min(0.01f)] public float cameraShakeDuration = 0.14f;
        public bool hitStopEventEnabled;
        [Range(0f, 0.25f)] public float hitStopDuration = 0.035f;
        public bool decalEventEnabled;
        public GameObject decalPrefab;

        public void Normalize()
        {
            distortionStrength = Mathf.Clamp01(distortionStrength);
            distortionSize = Mathf.Clamp(distortionSize, 0.01f, 100f);
            flipbookColumns = Mathf.Clamp(flipbookColumns, 1, 32);
            flipbookRows = Mathf.Clamp(flipbookRows, 1, 32);
            flipbookFramesPerSecond = Mathf.Clamp(flipbookFramesPerSecond, 1f, 240f);
            lightIntensity = Mathf.Clamp(lightIntensity, 0f, 100000f);
            lightRange = Mathf.Clamp(lightRange, 0.01f, 10000f);
            globalDissolve = Mathf.Clamp01(globalDissolve);
            globalEmission = Mathf.Clamp(globalEmission, 0f, 2f);
            globalEdgeGlow = Mathf.Clamp(globalEdgeGlow, 0f, 2f);
            cameraShakeAmplitude = Mathf.Clamp(cameraShakeAmplitude, 0f, 2f);
            cameraShakeDuration = Mathf.Clamp(cameraShakeDuration, 0.01f, 5f);
            hitStopDuration = Mathf.Clamp(hitStopDuration, 0f, 0.25f);
            externalEffectScale.x = Mathf.Max(0.001f, externalEffectScale.x);
            externalEffectScale.y = Mathf.Max(0.001f, externalEffectScale.y);
            externalEffectScale.z = Mathf.Max(0.001f, externalEffectScale.z);
            lightIntensityOverLifetime ??= AnimationCurve.Linear(0f, 1f, 1f, 0f);
        }
    }

    [CreateAssetMenu(fileName = "RetroVfxRecipe", menuName = "Dans Toolbox/Retro VFX Recipe")]
    public sealed class RetroVfxRecipe : ScriptableObject
    {
        public const int CurrentFormatVersion = 3;

        [SerializeField, HideInInspector] private int formatVersion = CurrentFormatVersion;
        public string displayName = "New Retro VFX";
        public RetroVfxEffectFamily family = RetroVfxEffectFamily.Impact;
        public RetroVfxArtStyle artStyle = RetroVfxArtStyle.Pixel16;
        public int seed = 1337;
        [Min(0.05f)] public float duration = 0.65f;
        [Min(0.01f)] public float scale = 1f;
        [Range(0.1f, 3f)] public float intensity = 1f;
        [Range(-180f, 180f)] public float direction;
        public bool loopPreview;
        public Color primaryColor = new Color(1f, 0.58f, 0.12f);
        public Color secondaryColor = new Color(1f, 0.95f, 0.72f);
        public AudioClip audioClip;
        public List<RetroVfxLayer> layers = new List<RetroVfxLayer>();
        public RetroVfxAdvancedSettings advanced = new RetroVfxAdvancedSettings();

        public int FormatVersion => formatVersion;

        public void Normalize()
        {
            formatVersion = CurrentFormatVersion;
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? family + " VFX"
                : displayName.Trim();
            duration = Mathf.Clamp(duration, 0.05f, 30f);
            scale = Mathf.Clamp(scale, 0.01f, 100f);
            intensity = Mathf.Clamp(intensity, 0.1f, 3f);
            direction = Mathf.Clamp(direction, -180f, 180f);
            layers ??= new List<RetroVfxLayer>();
            layers.RemoveAll(layer => layer == null);
            foreach (RetroVfxLayer layer in layers)
            {
                layer.Normalize();
            }

            advanced ??= new RetroVfxAdvancedSettings();
            advanced.Normalize();
        }

        public int ComputeStableHash()
        {
            Normalize();
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)family;
                hash = hash * 31 + (int)artStyle;
                hash = hash * 31 + seed;
                hash = hash * 31 + duration.GetHashCode();
                hash = hash * 31 + scale.GetHashCode();
                hash = hash * 31 + intensity.GetHashCode();
                hash = hash * 31 + direction.GetHashCode();
                hash = hash * 31 + primaryColor.GetHashCode();
                hash = hash * 31 + secondaryColor.GetHashCode();
                foreach (RetroVfxLayer layer in layers)
                {
                    hash = hash * 31 + layer.name.GetHashCode();
                    hash = hash * 31 + layer.enabled.GetHashCode();
                    hash = hash * 31 + layer.locked.GetHashCode();
                    hash = hash * 31 + (int)layer.phase;
                    hash = hash * 31 + (int)layer.kind;
                    hash = hash * 31 + (int)layer.shape;
                    hash = hash * 31 + (int)layer.spriteStyle;
                    hash = hash * 31 + (int)layer.motion;
                    hash = hash * 31 + (int)layer.blendMode;
                    hash = hash * 31 + (int)layer.sourceMode;
                    hash = hash * 31 + (int)layer.renderGeometry;
                    hash = hash * 31 + (layer.sourcePackId?.GetHashCode() ?? 0);
                    hash = hash * 31 + (layer.sourceTexture != null ? layer.sourceTexture.GetInstanceID() : 0);
                    hash = hash * 31 + layer.sourceColumns;
                    hash = hash * 31 + layer.sourceRows;
                    hash = hash * 31 + layer.count;
                    hash = hash * 31 + layer.rateOverTime.GetHashCode();
                    hash = hash * 31 + layer.burstCount;
                    hash = hash * 31 + layer.burstInterval.GetHashCode();
                    hash = hash * 31 + layer.delay.GetHashCode();
                    hash = hash * 31 + layer.lifetime.GetHashCode();
                    hash = hash * 31 + layer.speed.GetHashCode();
                    hash = hash * 31 + layer.speedRandomness.GetHashCode();
                    hash = hash * 31 + layer.size.GetHashCode();
                    hash = hash * 31 + layer.sizeRandomness.GetHashCode();
                    hash = hash * 31 + layer.spread.GetHashCode();
                    hash = hash * 31 + layer.emissionRadius.GetHashCode();
                    hash = hash * 31 + layer.offset.GetHashCode();
                    hash = hash * 31 + layer.aspect.GetHashCode();
                    hash = hash * 31 + layer.velocity.GetHashCode();
                    hash = hash * 31 + layer.gravity.GetHashCode();
                    hash = hash * 31 + layer.rotation.GetHashCode();
                    hash = hash * 31 + layer.rotationSpeed.GetHashCode();
                    hash = hash * 31 + layer.randomRotation.GetHashCode();
                    hash = hash * 31 + layer.stretch.GetHashCode();
                    hash = hash * 31 + layer.startColor.GetHashCode();
                    hash = hash * 31 + layer.endColor.GetHashCode();
                    hash = hash * 31 + (int)layer.noiseProfile;
                    hash = hash * 31 + layer.noiseStrength.GetHashCode();
                    hash = hash * 31 + layer.trailEnabled.GetHashCode();
                    hash = hash * 31 + layer.trailLifetime.GetHashCode();
                    hash = hash * 31 + layer.dissolve.GetHashCode();
                    hash = hash * 31 + layer.edgeGlow.GetHashCode();
                    hash = hash * 31 + layer.spawnFromLayer;
                    hash = hash * 31 + (int)layer.spawnEvent;
                }

                hash = hash * 31 + advanced.distortionEnabled.GetHashCode();
                hash = hash * 31 + advanced.distortionStrength.GetHashCode();
                hash = hash * 31 + advanced.lightEnabled.GetHashCode();
                hash = hash * 31 + advanced.lightIntensity.GetHashCode();
                hash = hash * 31 + advanced.flipbookColumns;
                hash = hash * 31 + advanced.flipbookRows;
                hash = hash * 31 + advanced.productionShader.GetHashCode();
                hash = hash * 31 + advanced.globalDissolve.GetHashCode();
                hash = hash * 31 + advanced.globalEmission.GetHashCode();
                hash = hash * 31 + advanced.cameraShakeEnabled.GetHashCode();
                hash = hash * 31 + advanced.externalEffectEnabled.GetHashCode();
                hash = hash * 31 + (advanced.externalEffectPrefab != null ? advanced.externalEffectPrefab.GetInstanceID() : 0);
                return hash;
            }
        }

        private void OnValidate()
        {
            Normalize();
        }
    }
}
