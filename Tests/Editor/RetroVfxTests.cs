using System.Collections.Generic;
using DansToolbox.EditorTools.RetroVfx;
using DansToolbox.RetroVfx;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.Editor.Tests
{
    internal sealed class RetroVfxTests
    {
        private readonly List<Object> cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in cleanup)
            {
                if (item != null && !AssetDatabase.Contains(item))
                {
                    Object.DestroyImmediate(item);
                }
            }
            cleanup.Clear();
        }

        [Test]
        public void Presets_UseUniqueIdsAndBuildCompleteRecipes()
        {
            HashSet<string> ids = new HashSet<string>();
            HashSet<RetroVfxEffectFamily> families = new HashSet<RetroVfxEffectFamily>();
            foreach (RetroVfxPresetDescriptor descriptor in RetroVfxPresetFactory.Presets)
            {
                Assert.That(ids.Add(descriptor.Id), Is.True, descriptor.Id);
                RetroVfxRecipe recipe = RetroVfxPresetFactory.CreateWorkingRecipe(descriptor.Id);
                cleanup.Add(recipe);
                Assert.That(recipe.displayName, Is.Not.Empty);
                Assert.That(recipe.layers, Is.Not.Empty, descriptor.Id);
                Assert.That(recipe.duration, Is.GreaterThan(0f));
                families.Add(recipe.family);
            }

            Assert.That(families, Does.Contain(RetroVfxEffectFamily.Impact));
            Assert.That(families, Does.Contain(RetroVfxEffectFamily.Explosion));
            Assert.That(families, Does.Contain(RetroVfxEffectFamily.MuzzleFlash));
            Assert.That(families, Does.Contain(RetroVfxEffectFamily.Smoke));
            Assert.That(families, Does.Contain(RetroVfxEffectFamily.EnergyBurst));
            Assert.That(families, Does.Contain(RetroVfxEffectFamily.Pickup));
            Assert.That(families, Does.Contain(RetroVfxEffectFamily.Blood));
            Assert.That(families, Does.Contain(RetroVfxEffectFamily.SwordSwing));
            Assert.That(families, Does.Contain(RetroVfxEffectFamily.Magic));
            Assert.That(families, Does.Contain(RetroVfxEffectFamily.ItemShine));
            Assert.That(families, Does.Contain(RetroVfxEffectFamily.Environment));
            Assert.That(RetroVfxPresetFactory.Presets.Count, Is.GreaterThanOrEqualTo(40));
        }

        [Test]
        public void PresetVariations_AreRepeatablePerSeedAndDifferentAcrossSeeds()
        {
            RetroVfxRecipe first = RetroVfxPresetFactory.CreateWorkingRecipe("pixel-blast");
            RetroVfxRecipe second = RetroVfxPresetFactory.CreateWorkingRecipe("pixel-blast");
            RetroVfxRecipe third = RetroVfxPresetFactory.CreateWorkingRecipe("pixel-blast");
            cleanup.Add(first);
            cleanup.Add(second);
            cleanup.Add(third);

            RetroVfxPresetFactory.ApplyVariation("pixel-blast", first, 41521);
            RetroVfxPresetFactory.ApplyVariation("pixel-blast", second, 41521);
            RetroVfxPresetFactory.ApplyVariation("pixel-blast", third, 41522);

            Assert.That(first.ComputeStableHash(), Is.EqualTo(second.ComputeStableHash()));
            Assert.That(first.ComputeStableHash(), Is.Not.EqualTo(third.ComputeStableHash()));
            Assert.That(first.layers.Exists(layer => layer.spriteStyle == RetroVfxSpriteStyle.PixelExplosion), Is.True);
        }

        [Test]
        public void SignatureFamilies_UsePurposeBuiltSpriteArchetypes()
        {
            AssertPresetContains("blood-splat", RetroVfxSpriteStyle.BloodSplat, RetroVfxSpriteStyle.BloodDrop);
            AssertPresetContains("quick-slash", RetroVfxSpriteStyle.SlashArc, RetroVfxSpriteStyle.Spark);
            AssertPresetContains("sidearm-flash", RetroVfxSpriteStyle.MuzzleFlash, RetroVfxSpriteStyle.Spark);
            AssertPresetContains("item-shine", RetroVfxSpriteStyle.Glint, RetroVfxSpriteStyle.Ring);
        }

        [Test]
        public void PixelExplosionTexture_IsAnimatedAndPointFiltered()
        {
            RetroVfxLayer layer = new RetroVfxLayer
            {
                spriteStyle = RetroVfxSpriteStyle.PixelExplosion,
                startColor = Color.white,
                endColor = Color.clear
            };
            RetroVfxTextureSheet sheet = RetroVfxTextureFactory.Create(layer, 1234);
            cleanup.Add(sheet.Texture);

            Assert.That(sheet.Animated, Is.True);
            Assert.That(sheet.Columns, Is.EqualTo(8));
            Assert.That(sheet.Texture.filterMode, Is.EqualTo(FilterMode.Point));
        }

        [Test]
        public void RandomizeUnlocked_PreservesLockedLayersAndIsDeterministic()
        {
            RetroVfxRecipe first = RetroVfxPresetFactory.CreateWorkingRecipe("heavy-impact");
            RetroVfxRecipe second = RetroVfxPresetFactory.CreateWorkingRecipe("heavy-impact");
            cleanup.Add(first);
            cleanup.Add(second);
            first.layers[0].locked = true;
            second.layers[0].locked = true;
            float lockedSize = first.layers[0].size;
            int lockedCount = first.layers[0].count;

            RetroVfxPresetFactory.RandomizeUnlocked(first, 987654);
            RetroVfxPresetFactory.RandomizeUnlocked(second, 987654);

            Assert.That(first.layers[0].size, Is.EqualTo(lockedSize));
            Assert.That(first.layers[0].count, Is.EqualTo(lockedCount));
            Assert.That(first.ComputeStableHash(), Is.EqualTo(second.ComputeStableHash()));
            Assert.That(first.layers[1].size, Is.EqualTo(second.layers[1].size));
        }

        [Test]
        public void StableHash_ChangesWhenAuthoredDataChanges()
        {
            RetroVfxRecipe recipe = RetroVfxPresetFactory.CreateWorkingRecipe("arcane-burst");
            cleanup.Add(recipe);
            int before = recipe.ComputeStableHash();
            recipe.layers[0].speed += 0.5f;
            int after = recipe.ComputeStableHash();
            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void EffectBuilder_CreatesPlayableHierarchy()
        {
            RetroVfxRecipe recipe = RetroVfxPresetFactory.CreateWorkingRecipe("pixel-blast");
            cleanup.Add(recipe);
            recipe.advanced.distortionEnabled = true;
            recipe.advanced.lightEnabled = true;

            GameObject result = RetroVfxEffectBuilder.Build(recipe, false);
            cleanup.Add(result);
            foreach (ParticleSystemRenderer renderer in result.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (renderer.sharedMaterial != null)
                {
                    cleanup.Add(renderer.sharedMaterial);
                    if (renderer.sharedMaterial.mainTexture != null)
                    {
                        cleanup.Add(renderer.sharedMaterial.mainTexture);
                    }
                }
            }

            Assert.That(result.GetComponent<RetroVfxPlayer>(), Is.Not.Null);
            Assert.That(result.GetComponentsInChildren<ParticleSystem>(true).Length,
                Is.EqualTo(recipe.layers.Count + 1));
            Assert.That(result.GetComponentInChildren<Light>(true), Is.Not.Null);
            foreach (ParticleSystem system in result.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                if (!velocity.enabled)
                {
                    continue;
                }
                Assert.That(velocity.x.mode, Is.EqualTo(velocity.y.mode));
                Assert.That(velocity.y.mode, Is.EqualTo(velocity.z.mode));
            }
        }

        [Test]
        public void ExportValidation_RejectsUnsafeAndEmptyInputs()
        {
            RetroVfxRecipe recipe = RetroVfxPresetFactory.CreateWorkingRecipe("coin-glint");
            cleanup.Add(recipe);

            Assert.That(RetroVfxExportService.TryValidate(
                recipe,
                "C:/Outside",
                "Coin",
                128,
                4,
                4,
                out string outsideError), Is.False);
            Assert.That(outsideError, Does.Contain("Assets"));

            Assert.That(RetroVfxExportService.TryValidate(
                recipe,
                "Assets/Test",
                "Coin",
                128,
                4,
                4,
                out string validError), Is.True, validError);
        }

        [Test]
        public void Export_CreatesParticleAndFlipbookAssets()
        {
            RetroVfxRecipe recipe = RetroVfxPresetFactory.CreateWorkingRecipe("coin-glint");
            cleanup.Add(recipe);
            string folder = "Assets/__DansToolboxRetroVfxTest_" + System.Guid.NewGuid().ToString("N");
            try
            {
                RetroVfxExportResult result = RetroVfxExportService.Export(
                    recipe,
                    folder,
                    "Coin Glint",
                    RetroVfxOutputMode.Both,
                    64,
                    2,
                    2);

                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.AssetPaths.Count, Is.EqualTo(4));
                foreach (string path in result.AssetPaths)
                {
                    Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.Refresh();
            }
        }

        [TestCase("A/B:C*D?", "A_B_C_D_")]
        [TestCase("", "Retro VFX")]
        public void ExportNames_AreSanitized(string input, string expected)
        {
            Assert.That(RetroVfxExportService.SanitizeFileName(input), Is.EqualTo(expected));
        }

        [Test]
        public void GuiButtons_OwnFlatHighDpiStatesAndDistinctHoverFeedback()
        {
            GUIStyle style = RetroVfxGui.TabStyle;

            Assert.That(style.normal.background, Is.Not.Null);
            Assert.That(style.hover.background, Is.Not.Null);
            Assert.That(style.active.background, Is.Not.Null);
            Assert.That(style.onNormal.background, Is.Not.Null);
            Assert.That(style.onHover.background, Is.Not.Null);

            Assert.That(style.normal.scaledBackgrounds, Is.Empty);
            Assert.That(style.hover.scaledBackgrounds, Is.Empty);
            Assert.That(style.active.scaledBackgrounds, Is.Empty);
            Assert.That(style.onNormal.scaledBackgrounds, Is.Empty);
            Assert.That(style.onHover.scaledBackgrounds, Is.Empty);

            Assert.That(style.hover.background.GetPixel(1, 1),
                Is.Not.EqualTo(style.normal.background.GetPixel(1, 1)));
            Assert.That(style.onHover.background.GetPixel(1, 1),
                Is.Not.EqualTo(style.onNormal.background.GetPixel(1, 1)));
        }

        [Test]
        public void Presets_AuthorProductionPhasesSourcesAndSurfaceMotion()
        {
            RetroVfxRecipe explosion = RetroVfxPresetFactory.CreateWorkingRecipe("pixel-blast");
            RetroVfxRecipe sword = RetroVfxPresetFactory.CreateWorkingRecipe("heavy-cleave");
            cleanup.Add(explosion);
            cleanup.Add(sword);

            Assert.That(explosion.artStyle, Is.EqualTo(RetroVfxArtStyle.Pixel16));
            Assert.That(explosion.advanced.productionShader, Is.True);
            Assert.That(explosion.advanced.cameraShakeEnabled, Is.True);
            Assert.That(explosion.layers.Exists(layer => layer.phase == RetroVfxPhase.Primary), Is.True);
            Assert.That(explosion.layers.Exists(layer => layer.phase == RetroVfxPhase.Decay), Is.True);
            Assert.That(explosion.layers.Exists(layer => layer.sourceMode == RetroVfxSourceMode.SourceLibrary), Is.True);
            Assert.That(explosion.layers.Exists(layer => layer.noiseProfile == RetroVfxNoiseProfile.RollingSmoke), Is.True);
            Assert.That(sword.layers.Exists(layer => layer.renderGeometry == RetroVfxRenderGeometry.Mesh), Is.True);
            Assert.That(sword.layers.Exists(layer => layer.trailEnabled), Is.True);
        }

        [Test]
        public void EmbeddedSourceLibrary_ResolvesCuratedCc0FlipbookGrid()
        {
            RetroVfxRecipe recipe = RetroVfxPresetFactory.CreateWorkingRecipe("pixel-blast");
            cleanup.Add(recipe);
            RetroVfxLayer sourceLayer = recipe.layers.Find(layer => layer.sourceMode == RetroVfxSourceMode.SourceLibrary);
            sourceLayer.sourcePackId = "codemanu";

            bool resolved = RetroVfxSourceLibrary.TryResolveTexture(
                recipe,
                sourceLayer,
                1337,
                out Texture2D texture,
                out int columns,
                out int rows,
                out string sourceName);

            Assert.That(resolved, Is.True);
            Assert.That(texture, Is.Not.Null);
            Assert.That(columns, Is.GreaterThan(1));
            Assert.That(rows, Is.GreaterThan(1));
            Assert.That(sourceName, Does.Contain("CodeManu"));
            Assert.That(RetroVfxSourceLibrary.Descriptors.Count, Is.GreaterThanOrEqualTo(20));
            Assert.That(RetroVfxSourceLibrary.InstalledCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void SourceLibrary_AppliesDescriptorChangesIncrementally()
        {
            RetroVfxSourceLibrary.Refresh();
            RetroVfxSourceDescriptor descriptor = null;
            foreach (RetroVfxSourceDescriptor candidate in RetroVfxSourceLibrary.Descriptors)
            {
                if (candidate.Id == "brackeys") descriptor = candidate;
            }
            Assert.That(descriptor, Is.Not.Null);
            int before = descriptor.DetectedAssetCount;
            string path = "Assets/__DansToolbox_brackeys_" + System.Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                RetroVfxSourceLibrary.ApplyAssetChanges(new[] { path }, null, null, null);
                Assert.That(descriptor.DetectedAssetCount, Is.EqualTo(before + 1));
            }
            finally
            {
                RetroVfxSourceLibrary.ApplyAssetChanges(null, new[] { path }, null, null);
            }
            Assert.That(descriptor.DetectedAssetCount, Is.EqualTo(before));
        }

        [Test]
        public void EffectBuilder_ConfiguresMeshTrailsAndProductionShader()
        {
            RetroVfxRecipe recipe = RetroVfxPresetFactory.CreateWorkingRecipe("heavy-cleave");
            cleanup.Add(recipe);
            GameObject result = RetroVfxEffectBuilder.Build(recipe, false);
            cleanup.Add(result);

            ParticleSystemRenderer[] renderers = result.GetComponentsInChildren<ParticleSystemRenderer>(true);
            Assert.That(System.Array.Exists(renderers, renderer => renderer.renderMode == ParticleSystemRenderMode.Mesh), Is.True);
            Assert.That(System.Array.Exists(renderers, renderer => renderer.sharedMaterial != null &&
                                                                  renderer.sharedMaterial.shader != null &&
                                                                  renderer.sharedMaterial.shader.name == "Dans Toolbox/Retro VFX/Uber"), Is.True);
            Assert.That(System.Array.Exists(result.GetComponentsInChildren<ParticleSystem>(true), system => system.trails.enabled), Is.True);

            foreach (ParticleSystemRenderer renderer in renderers)
            {
                if (renderer.sharedMaterial != null)
                {
                    cleanup.Add(renderer.sharedMaterial);
                }
                if (renderer.mesh != null && renderer.mesh.hideFlags != HideFlags.None)
                {
                    cleanup.Add(renderer.mesh);
                }
            }
        }

        [Test]
        public void SceneResponseHooks_ArePublishedWithoutOwningGameFrameworks()
        {
            GameObject root = new GameObject("Scene Response Test");
            cleanup.Add(root);
            RetroVfxPlayer player = root.AddComponent<RetroVfxPlayer>();
            bool shakeRaised = false;
            bool hitStopRaised = false;
            System.Action<RetroVfxPlayer, float, float> shake = (_, amplitude, duration) =>
                shakeRaised = amplitude > 0f && duration > 0f;
            System.Action<RetroVfxPlayer, float> hitStop = (_, duration) => hitStopRaised = duration > 0f;
            RetroVfxPlayer.CameraShakeRequested += shake;
            RetroVfxPlayer.HitStopRequested += hitStop;
            try
            {
                player.Configure(
                    0.5f,
                    System.Array.Empty<ParticleSystem>(),
                    null,
                    null,
                    0f,
                    AnimationCurve.Linear(0f, 1f, 1f, 0f),
                    true,
                    0.4f,
                    0.15f,
                    true,
                    0.04f,
                    false,
                    null);
                player.Play();
            }
            finally
            {
                RetroVfxPlayer.CameraShakeRequested -= shake;
                RetroVfxPlayer.HitStopRequested -= hitStop;
            }

            Assert.That(shakeRaised, Is.True);
            Assert.That(hitStopRaised, Is.True);
        }

        private void AssertPresetContains(string presetId, params RetroVfxSpriteStyle[] expectedStyles)
        {
            RetroVfxRecipe recipe = RetroVfxPresetFactory.CreateWorkingRecipe(presetId);
            cleanup.Add(recipe);
            foreach (RetroVfxSpriteStyle style in expectedStyles)
            {
                Assert.That(recipe.layers.Exists(layer => layer.spriteStyle == style), Is.True, presetId + " should contain " + style);
            }
        }
    }
}
