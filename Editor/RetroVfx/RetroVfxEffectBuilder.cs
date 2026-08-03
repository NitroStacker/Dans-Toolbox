using System;
using System.Collections.Generic;
using System.Reflection;
using DansToolbox.RetroVfx;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DansToolbox.EditorTools.RetroVfx
{
    internal static class RetroVfxEffectBuilder
    {
        private const string DistortionShaderName = "Dans Toolbox/Retro VFX/Distortion";
        private const string UberShaderName = "Dans Toolbox/Retro VFX/Uber";

        internal static GameObject Build(RetroVfxRecipe recipe, bool preview)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            recipe.Normalize();
            GameObject root = new GameObject(SanitizeObjectName(recipe.displayName));
            root.transform.localScale = Vector3.one * recipe.scale;
            if (preview)
            {
                SetPreviewHideFlags(root);
            }

            List<ParticleSystem> systems = new List<ParticleSystem>();
            Dictionary<int, ParticleSystem> systemsByLayer = new Dictionary<int, ParticleSystem>();
            for (int index = 0; index < recipe.layers.Count; index++)
            {
                RetroVfxLayer layer = recipe.layers[index];
                if (!layer.enabled)
                {
                    continue;
                }

                ParticleSystem system = CreateLayer(root.transform, recipe, layer, index, preview);
                systems.Add(system);
                systemsByLayer[index] = system;
            }

            ConfigureSubEmitters(recipe, systemsByLayer);

            CreateExternalEffect(root.transform, recipe, preview, systems);

            if (recipe.advanced.importedFlipbook != null)
            {
                systems.Add(CreateImportedFlipbook(root.transform, recipe, preview));
            }

            if (recipe.advanced.distortionEnabled)
            {
                systems.Add(CreateDistortionLayer(root.transform, recipe, preview));
            }

            AudioSource audioSource = CreateAudio(root, recipe);
            Light effectLight = CreateLight(root, recipe);
            TryAttachVfxGraph(root, recipe.advanced.vfxGraphAsset, out _);

            RetroVfxPlayer player = root.AddComponent<RetroVfxPlayer>();
            player.Configure(
                CalculateDuration(recipe),
                systems.ToArray(),
                audioSource,
                effectLight,
                recipe.advanced.lightIntensity,
                recipe.advanced.lightIntensityOverLifetime,
                recipe.advanced.cameraShakeEnabled,
                recipe.advanced.cameraShakeAmplitude,
                recipe.advanced.cameraShakeDuration,
                recipe.advanced.hitStopEventEnabled,
                recipe.advanced.hitStopDuration,
                recipe.advanced.decalEventEnabled,
                recipe.advanced.decalPrefab);

            return root;
        }

        internal static float CalculateDuration(RetroVfxRecipe recipe)
        {
            float duration = Mathf.Max(0.05f, recipe.duration);
            foreach (RetroVfxLayer layer in recipe.layers)
            {
                if (layer.enabled)
                {
                    duration = Mathf.Max(
                        duration,
                        layer.delay + layer.burstInterval * (layer.burstCount - 1) + layer.lifetime);
                }
            }

            if (recipe.advanced.importedFlipbook != null)
            {
                float flipbookDuration = recipe.advanced.flipbookColumns *
                                         recipe.advanced.flipbookRows /
                                         Mathf.Max(1f, recipe.advanced.flipbookFramesPerSecond);
                duration = Mathf.Max(duration, flipbookDuration);
            }

            return duration;
        }

        internal static bool IsVfxGraphAvailable()
        {
            return FindType("UnityEngine.VFX.VisualEffect") != null &&
                   FindType("UnityEngine.VFX.VisualEffectAsset") != null;
        }

        internal static bool IsVfxGraphAsset(Object candidate)
        {
            Type assetType = FindType("UnityEngine.VFX.VisualEffectAsset");
            return candidate == null || assetType != null && assetType.IsInstanceOfType(candidate);
        }

        internal static bool TryAttachVfxGraph(
            GameObject root,
            Object graphAsset,
            out string message)
        {
            message = string.Empty;
            if (graphAsset == null)
            {
                return false;
            }

            Type componentType = FindType("UnityEngine.VFX.VisualEffect");
            Type assetType = FindType("UnityEngine.VFX.VisualEffectAsset");
            if (componentType == null || assetType == null)
            {
                message = "VFX Graph runtime support is not available in this project.";
                return false;
            }

            if (!assetType.IsInstanceOfType(graphAsset))
            {
                message = "The selected object is not a VisualEffectAsset.";
                return false;
            }

            Component component = root.AddComponent(componentType);
            PropertyInfo assetProperty = componentType.GetProperty(
                "visualEffectAsset",
                BindingFlags.Instance | BindingFlags.Public);
            if (assetProperty == null || !assetProperty.CanWrite)
            {
                Object.DestroyImmediate(component);
                message = "This Unity version does not expose the VFX Graph asset property.";
                return false;
            }

            assetProperty.SetValue(component, graphAsset);
            message = "VFX Graph asset attached.";
            return true;
        }

        internal static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Retro VFX";
            }

            char[] invalid = { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
            foreach (char character in invalid)
            {
                value = value.Replace(character, '_');
            }

            return value.Trim();
        }

        private static ParticleSystem CreateLayer(
            Transform parent,
            RetroVfxRecipe recipe,
            RetroVfxLayer layer,
            int index,
            bool preview)
        {
            GameObject child = new GameObject(layer.name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = new Vector3(layer.offset.x, layer.offset.y, 0f);
            child.transform.localRotation = Quaternion.Euler(0f, 0f, recipe.direction);
            if (preview)
            {
                SetPreviewHideFlags(child);
            }

            ParticleSystem system = child.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            ConfigureMain(system, recipe, layer, index);
            ConfigureEmission(system, layer);
            ConfigureShape(system, layer);
            ConfigureVelocity(system, layer);
            ConfigureColor(system, layer);
            ConfigureSize(system, layer);
            ConfigureRotation(system, layer);
            ConfigureNoise(system, layer, recipe.intensity);
            ConfigureDrag(system, layer);
            ConfigureTrails(system, layer);
            ConfigureCollision(system, layer);
            ConfigureRenderer(renderer, recipe, layer, index);
            return system;
        }

        private static void ConfigureMain(
            ParticleSystem system,
            RetroVfxRecipe recipe,
            RetroVfxLayer layer,
            int index)
        {
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = true;
            main.loop = recipe.loopPreview && layer.kind != RetroVfxLayerKind.Flash;
            main.duration = Mathf.Max(0.05f, recipe.duration, layer.burstInterval * (layer.burstCount - 1) + 0.05f);
            main.startDelay = layer.delay;
            main.startLifetime = layer.lifetime;
            float speed = layer.motion == RetroVfxMotionMode.Stationary ||
                          layer.motion == RetroVfxMotionMode.Rising ||
                          layer.motion == RetroVfxMotionMode.Falling ||
                          layer.motion == RetroVfxMotionMode.Drift
                ? 0f
                : layer.speed * recipe.intensity;
            main.startSpeed = RandomizedCurve(speed, layer.speedRandomness);
            float size = layer.size * recipe.intensity;
            main.startSize3D = true;
            main.startSizeX = RandomizedCurve(size * layer.aspect.x, layer.sizeRandomness);
            main.startSizeY = RandomizedCurve(size * layer.aspect.y, layer.sizeRandomness);
            main.startSizeZ = RandomizedCurve(size, layer.sizeRandomness);
            main.startRotation = layer.randomRotation
                ? new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI)
                : new ParticleSystem.MinMaxCurve(layer.rotation * Mathf.Deg2Rad);
            main.gravityModifier = layer.gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            int authoredMaximum = layer.count * layer.burstCount;
            main.maxParticles = Mathf.Max(authoredMaximum * (main.loop ? 3 : 1), authoredMaximum + 8);
            main.stopAction = ParticleSystemStopAction.None;
            main.useUnscaledTime = false;
            system.useAutoRandomSeed = false;
            system.randomSeed = unchecked((uint)(recipe.seed * 397 ^ index * 7919 ^ 0x5f3759df));
        }

        private static void ConfigureEmission(ParticleSystem system, RetroVfxLayer layer)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0f, layer.rateOverTime);
            short count = (short)Mathf.Clamp(layer.count, 1, short.MaxValue);
            ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[layer.burstCount];
            for (int index = 0; index < bursts.Length; index++)
            {
                bursts[index] = new ParticleSystem.Burst(index * layer.burstInterval, count);
            }
            emission.SetBursts(bursts);
        }

        private static void ConfigureShape(ParticleSystem system, RetroVfxLayer layer)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = layer.motion != RetroVfxMotionMode.Stationary || layer.emissionRadius > 0f;
            shape.radius = Mathf.Max(0.0001f, layer.emissionRadius);
            shape.radiusThickness = layer.kind == RetroVfxLayerKind.Ring ? 1f : 0f;
            shape.arc = Mathf.Max(0.1f, layer.spread);
            shape.alignToDirection = layer.kind == RetroVfxLayerKind.Sparks ||
                                     layer.kind == RetroVfxLayerKind.Trail ||
                                     layer.spriteStyle == RetroVfxSpriteStyle.BloodDrop;
            switch (layer.shape)
            {
                case RetroVfxParticleShape.Circle:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    break;
                case RetroVfxParticleShape.Cone:
                    // A 2D circle arc emits in the preview plane. Unity's cone emits
                    // down local Z, which makes directional effects look identical head-on.
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.arc = Mathf.Max(0.1f, layer.spread);
                    break;
                case RetroVfxParticleShape.Sphere:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.12f;
                    break;
                case RetroVfxParticleShape.Box:
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = Vector3.one * 0.2f;
                    break;
                default:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    break;
            }
        }

        private static void ConfigureVelocity(ParticleSystem system, RetroVfxLayer layer)
        {
            Vector2 velocity = layer.velocity;
            if (velocity.sqrMagnitude < 0.000001f)
            {
                velocity = layer.motion switch
                {
                    RetroVfxMotionMode.Rising => Vector2.up * layer.speed,
                    RetroVfxMotionMode.Falling => Vector2.down * layer.speed,
                    RetroVfxMotionMode.Drift => Vector2.right * layer.speed,
                    _ => Vector2.zero
                };
            }

            if (velocity.sqrMagnitude < 0.000001f)
            {
                return;
            }

            ParticleSystem.VelocityOverLifetimeModule module = system.velocityOverLifetime;
            module.enabled = true;
            module.space = ParticleSystemSimulationSpace.Local;
            module.x = RandomizedCurve(velocity.x, layer.speedRandomness);
            module.y = RandomizedCurve(velocity.y, layer.speedRandomness);
            // Unity requires X/Y/Z to use the same MinMaxCurve mode. Assigning
            // only X and Y leaves Z in Constant mode while the randomized axes
            // use TwoConstants, which logs an error as soon as the system plays.
            module.z = RandomizedCurve(0f, layer.speedRandomness);
        }

        private static void ConfigureColor(ParticleSystem system, RetroVfxLayer layer)
        {
            Gradient gradient = layer.colorOverLifetime;
            if (gradient == null)
            {
                gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(layer.startColor, 0f),
                        new GradientColorKey(layer.endColor, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(layer.startColor.a, 0f),
                        new GradientAlphaKey(layer.endColor.a, 1f)
                    });
            }
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void ConfigureSize(ParticleSystem system, RetroVfxLayer layer)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, layer.sizeOverLifetime);
        }

        private static void ConfigureRotation(ParticleSystem system, RetroVfxLayer layer)
        {
            if (Mathf.Abs(layer.rotationSpeed) < 0.01f)
            {
                return;
            }

            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = layer.rotationSpeed * Mathf.Deg2Rad;
        }

        private static void ConfigureNoise(
            ParticleSystem system,
            RetroVfxLayer layer,
            float intensity)
        {
            RetroVfxNoiseProfile profile = layer.noiseProfile;
            if (profile == RetroVfxNoiseProfile.None)
            {
                profile = layer.kind switch
                {
                    RetroVfxLayerKind.Smoke => RetroVfxNoiseProfile.RollingSmoke,
                    RetroVfxLayerKind.Burst => RetroVfxNoiseProfile.ChaoticFire,
                    RetroVfxLayerKind.Trail => RetroVfxNoiseProfile.WindShear,
                    RetroVfxLayerKind.Beam => RetroVfxNoiseProfile.ElectricJitter,
                    _ => RetroVfxNoiseProfile.None
                };
            }
            if (profile == RetroVfxNoiseProfile.None)
            {
                return;
            }

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            float profileStrength = profile switch
            {
                RetroVfxNoiseProfile.SoftTurbulence => 0.45f,
                RetroVfxNoiseProfile.RollingSmoke => 0.8f,
                RetroVfxNoiseProfile.ChaoticFire => 1.2f,
                RetroVfxNoiseProfile.ElectricJitter => 1.65f,
                RetroVfxNoiseProfile.WindShear => 0.65f,
                _ => 1f
            };
            noise.strength = Mathf.Max(0.01f, layer.noiseStrength) * profileStrength * intensity;
            noise.frequency = layer.noiseFrequency;
            noise.scrollSpeed = layer.noiseScrollSpeed;
            noise.octaveCount = layer.noiseOctaves;
            noise.damping = profile != RetroVfxNoiseProfile.ElectricJitter;
            noise.quality = profile == RetroVfxNoiseProfile.ElectricJitter
                ? ParticleSystemNoiseQuality.High
                : ParticleSystemNoiseQuality.Medium;
        }

        private static void ConfigureDrag(ParticleSystem system, RetroVfxLayer layer)
        {
            if (layer.drag <= 0.0001f)
            {
                return;
            }
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = layer.drag;
            limit.dampen = 0f;
            limit.multiplyDragByParticleSize = true;
        }

        private static void ConfigureTrails(ParticleSystem system, RetroVfxLayer layer)
        {
            bool enabled = layer.trailEnabled || layer.renderGeometry == RetroVfxRenderGeometry.ParticleTrail;
            if (!enabled)
            {
                return;
            }
            ParticleSystem.TrailModule trails = system.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = Mathf.Clamp01(layer.trailRatio);
            trails.lifetime = layer.trailLifetime;
            trails.minVertexDistance = 0.04f;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.dieWithParticles = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
                layer.trailWidth,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            Gradient gradient = new Gradient();
            Color end = layer.trailColor;
            end.a = 0f;
            gradient.SetKeys(
                new[] { new GradientColorKey(layer.trailColor, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(layer.trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverLifetime = gradient;
        }

        private static void ConfigureCollision(ParticleSystem system, RetroVfxLayer layer)
        {
            if (!layer.collisionEnabled)
            {
                return;
            }
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.dampen = layer.collisionDampen;
            collision.bounce = layer.collisionBounce;
            collision.lifetimeLoss = 0.1f;
            collision.sendCollisionMessages = layer.spawnEvent == RetroVfxSpawnEvent.Collision;
        }

        private static void ConfigureRenderer(
            ParticleSystemRenderer renderer,
            RetroVfxRecipe recipe,
            RetroVfxLayer layer,
            int index)
        {
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
            renderer.sortingFudge = -index * 0.01f;
            RetroVfxSpriteStyle resolvedStyle = RetroVfxTextureFactory.ResolveStyle(layer);
            bool wantsMesh = layer.renderGeometry == RetroVfxRenderGeometry.Mesh ||
                             layer.sourceMode == RetroVfxSourceMode.Mesh ||
                             layer.kind == RetroVfxLayerKind.Arc ||
                             resolvedStyle == RetroVfxSpriteStyle.SlashArc ||
                             resolvedStyle == RetroVfxSpriteStyle.Crescent;
            bool wantsStretch = layer.renderGeometry == RetroVfxRenderGeometry.StretchedBillboard ||
                                layer.kind == RetroVfxLayerKind.Sparks ||
                                layer.kind == RetroVfxLayerKind.Trail ||
                                layer.kind == RetroVfxLayerKind.Beam ||
                                resolvedStyle == RetroVfxSpriteStyle.BloodDrop ||
                                resolvedStyle == RetroVfxSpriteStyle.Beam;
            renderer.renderMode = wantsMesh
                ? ParticleSystemRenderMode.Mesh
                : wantsStretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (wantsMesh)
            {
                renderer.mesh = RetroVfxMeshFactory.Create(layer, recipe.seed + index * 131);
                renderer.alignment = ParticleSystemRenderSpace.Local;
            }
            if (renderer.renderMode == ParticleSystemRenderMode.Stretch)
            {
                renderer.lengthScale = layer.stretch;
                renderer.velocityScale = 0.16f;
            }

            Texture2D texture;
            int columns;
            int rows;
            if (!RetroVfxSourceLibrary.TryResolveTexture(recipe, layer, recipe.seed + index * 101, out texture, out columns, out rows, out _))
            {
                RetroVfxTextureSheet generated = RetroVfxTextureFactory.Create(recipe, layer, recipe.seed + index * 101);
                texture = generated.Texture;
                columns = generated.Columns;
                rows = generated.Rows;
            }
            renderer.sharedMaterial = CreateParticleMaterial(recipe, layer, texture);
            renderer.trailMaterial = renderer.sharedMaterial;
            if (columns * rows > 1)
            {
                ParticleSystem system = renderer.GetComponent<ParticleSystem>();
                ParticleSystem.TextureSheetAnimationModule animation = system.textureSheetAnimation;
                animation.enabled = true;
                animation.mode = ParticleSystemAnimationMode.Grid;
                animation.animation = ParticleSystemAnimationType.WholeSheet;
                animation.numTilesX = columns;
                animation.numTilesY = rows;
                animation.frameOverTime = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));
                animation.cycleCount = layer.sourceLoop
                    ? Mathf.Max(1, Mathf.RoundToInt(layer.lifetime * layer.sourceFramesPerSecond / (columns * rows)))
                    : 1;
            }
        }

        private static void ConfigureSubEmitters(
            RetroVfxRecipe recipe,
            IReadOnlyDictionary<int, ParticleSystem> systemsByLayer)
        {
            foreach (KeyValuePair<int, ParticleSystem> pair in systemsByLayer)
            {
                RetroVfxLayer childLayer = recipe.layers[pair.Key];
                if (childLayer.spawnEvent == RetroVfxSpawnEvent.None ||
                    !systemsByLayer.TryGetValue(childLayer.spawnFromLayer, out ParticleSystem parent) ||
                    parent == pair.Value)
                {
                    continue;
                }
                ParticleSystemSubEmitterType type = childLayer.spawnEvent switch
                {
                    RetroVfxSpawnEvent.Birth => ParticleSystemSubEmitterType.Birth,
                    RetroVfxSpawnEvent.Death => ParticleSystemSubEmitterType.Death,
                    RetroVfxSpawnEvent.Collision => ParticleSystemSubEmitterType.Collision,
                    RetroVfxSpawnEvent.Trigger => ParticleSystemSubEmitterType.Trigger,
                    _ => ParticleSystemSubEmitterType.Death
                };
                ParticleSystem.SubEmittersModule subEmitters = parent.subEmitters;
                subEmitters.enabled = true;
                subEmitters.AddSubEmitter(pair.Value, type, ParticleSystemSubEmitterProperties.InheritNothing);
                ParticleSystem.MainModule childMain = pair.Value.main;
                childMain.playOnAwake = false;
            }
        }

        private static void CreateExternalEffect(
            Transform parent,
            RetroVfxRecipe recipe,
            bool preview,
            ICollection<ParticleSystem> systems)
        {
            if (!recipe.advanced.externalEffectEnabled || recipe.advanced.externalEffectPrefab == null)
            {
                return;
            }
            GameObject instance = Object.Instantiate(recipe.advanced.externalEffectPrefab, parent, false);
            instance.name = recipe.advanced.externalEffectPrefab.name + " [External Layer]";
            instance.transform.localPosition = recipe.advanced.externalEffectPosition;
            instance.transform.localRotation = Quaternion.Euler(recipe.advanced.externalEffectRotation);
            instance.transform.localScale = recipe.advanced.externalEffectScale;
            if (preview)
            {
                SetPreviewHideFlags(instance);
            }
            foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                systems.Add(system);
            }
        }

        private static ParticleSystem CreateImportedFlipbook(
            Transform parent,
            RetroVfxRecipe recipe,
            bool preview)
        {
            RetroVfxLayer layer = new RetroVfxLayer
            {
                name = "Imported Flipbook",
                kind = RetroVfxLayerKind.Flipbook,
                count = 1,
                lifetime = recipe.advanced.flipbookColumns * recipe.advanced.flipbookRows /
                           Mathf.Max(1f, recipe.advanced.flipbookFramesPerSecond),
                speed = 0f,
                size = 1f,
                spread = 360f,
                startColor = Color.white,
                endColor = Color.white,
                sizeOverLifetime = AnimationCurve.Linear(0f, 1f, 1f, 1f)
            };
            ParticleSystem system = CreateLayer(parent, recipe, layer, 9001, preview);
            ParticleSystem.MainModule main = system.main;
            main.loop = recipe.advanced.flipbookLoop;
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = recipe.advanced.flipbookColumns;
            sheet.numTilesY = recipe.advanced.flipbookRows;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            sheet.cycleCount = 1;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial.mainTexture = recipe.advanced.importedFlipbook;
            SetTexture(renderer.sharedMaterial, recipe.advanced.importedFlipbook);
            return system;
        }

        private static ParticleSystem CreateDistortionLayer(
            Transform parent,
            RetroVfxRecipe recipe,
            bool preview)
        {
            RetroVfxLayer layer = new RetroVfxLayer
            {
                name = "Distortion",
                kind = RetroVfxLayerKind.Ring,
                count = 1,
                lifetime = Mathf.Min(recipe.duration, 0.75f),
                speed = 0f,
                size = recipe.advanced.distortionSize,
                startColor = new Color(1f, 1f, 1f, recipe.advanced.distortionStrength),
                endColor = new Color(1f, 1f, 1f, 0f),
                sizeOverLifetime = new AnimationCurve(
                    new Keyframe(0f, 0.15f),
                    new Keyframe(0.22f, 0.75f),
                    new Keyframe(1f, 1.65f))
            };
            ParticleSystem system = CreateLayer(parent, recipe, layer, 8128, preview);
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            Material material = recipe.advanced.distortionMaterial != null
                ? new Material(recipe.advanced.distortionMaterial)
                : CreateDistortionMaterial(recipe);
            renderer.sharedMaterial = material;
            return system;
        }

        private static Material CreateDistortionMaterial(RetroVfxRecipe recipe)
        {
            Shader shader = Shader.Find(DistortionShaderName) ?? FindFallbackShader();
            Material material = new Material(shader)
            {
                name = "Retro VFX Distortion",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_Strength"))
            {
                material.SetFloat("_Strength", recipe.advanced.distortionStrength);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(1f, 1f, 1f, recipe.advanced.distortionStrength));
            }
            return material;
        }

        private static AudioSource CreateAudio(GameObject root, RetroVfxRecipe recipe)
        {
            if (recipe.audioClip == null)
            {
                return null;
            }

            AudioSource source = root.AddComponent<AudioSource>();
            source.clip = recipe.audioClip;
            source.playOnAwake = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 24f;
            return source;
        }

        private static Light CreateLight(GameObject root, RetroVfxRecipe recipe)
        {
            if (!recipe.advanced.lightEnabled)
            {
                return null;
            }

            GameObject child = new GameObject("Effect Light");
            child.transform.SetParent(root.transform, false);
            Light light = child.AddComponent<Light>();
            light.type = recipe.advanced.lightType;
            light.color = recipe.advanced.lightColor;
            light.intensity = recipe.advanced.lightIntensity;
            light.range = recipe.advanced.lightRange;
            light.shadows = recipe.advanced.lightShadows;
            return light;
        }

        private static Material CreateParticleMaterial(
            RetroVfxRecipe recipe,
            RetroVfxLayer layer,
            Texture texture)
        {
            Material material;
            if (layer.materialOverride != null)
            {
                material = new Material(layer.materialOverride);
            }
            else if (recipe.advanced.customMaterial != null)
            {
                material = new Material(recipe.advanced.customMaterial);
            }
            else
            {
                Shader shader = recipe.advanced.customShader != null
                    ? recipe.advanced.customShader
                    : recipe.advanced.productionShader
                        ? Shader.Find(UberShaderName) ?? FindParticleShader(layer.blendMode)
                        : FindParticleShader(layer.blendMode);
                material = new Material(shader);
            }

            material.name = "Retro VFX " + layer.name;
            material.hideFlags = HideFlags.HideAndDontSave;
            SetTexture(material, texture);
            ConfigureBlend(material, layer.blendMode);
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }
            SetFloat(material, "_Emission", layer.emission * recipe.advanced.globalEmission);
            SetFloat(material, "_EdgeGlow", layer.edgeGlow * recipe.advanced.globalEdgeGlow);
            SetFloat(material, "_Dissolve", Mathf.Clamp01(layer.dissolve + recipe.advanced.globalDissolve));
            if (material.HasProperty("_EdgeColor"))
            {
                material.SetColor("_EdgeColor", layer.startColor);
            }
            if (material.HasProperty("_FlowSpeed"))
            {
                material.SetVector("_FlowSpeed", new Vector4(layer.flowSpeed.x, layer.flowSpeed.y, -layer.flowSpeed.y, layer.flowSpeed.x));
            }
            if (recipe.advanced.dissolveTexture != null && material.HasProperty("_DissolveTex"))
            {
                material.SetTexture("_DissolveTex", recipe.advanced.dissolveTexture);
            }
            if (recipe.advanced.flowTexture != null && material.HasProperty("_FlowTex"))
            {
                material.SetTexture("_FlowTex", recipe.advanced.flowTexture);
            }
            return material;
        }

        private static void ConfigureBlend(Material material, RetroVfxBlendMode blendMode)
        {
            if (material == null)
            {
                return;
            }
            BlendMode source = blendMode == RetroVfxBlendMode.Premultiply ? BlendMode.One : BlendMode.SrcAlpha;
            BlendMode destination = blendMode switch
            {
                RetroVfxBlendMode.Additive => BlendMode.One,
                RetroVfxBlendMode.Multiply => BlendMode.SrcColor,
                _ => BlendMode.OneMinusSrcAlpha
            };
            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)source);
            }
            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)destination);
            }
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void SetTexture(Material material, Texture texture)
        {
            if (material == null || texture == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
            material.mainTexture = texture;
        }

        private static Shader FindFallbackShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                Shader urp = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (urp != null)
                {
                    return urp;
                }

                Shader hdrp = Shader.Find("HDRP/Unlit");
                if (hdrp != null)
                {
                    return hdrp;
                }
            }

            return Shader.Find("Particles/Standard Unlit") ??
                   Shader.Find("Legacy Shaders/Particles/Additive") ??
                   Shader.Find("Unlit/Transparent") ??
                   Shader.Find("Sprites/Default");
        }

        private static Shader FindParticleShader(RetroVfxBlendMode blendMode)
        {
            if (blendMode == RetroVfxBlendMode.Additive && GraphicsSettings.currentRenderPipeline == null)
            {
                Shader additive = Shader.Find("Legacy Shaders/Particles/Additive");
                if (additive != null)
                {
                    return additive;
                }
            }
            return FindFallbackShader();
        }

        private static ParticleSystem.MinMaxCurve RandomizedCurve(float value, float randomness)
        {
            float first = value * (1f - randomness);
            float second = value * (1f + randomness);
            return new ParticleSystem.MinMaxCurve(Mathf.Min(first, second), Mathf.Max(first, second));
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        private static void SetPreviewHideFlags(GameObject gameObject)
        {
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                component.hideFlags = HideFlags.HideAndDontSave;
            }
        }
    }
}
