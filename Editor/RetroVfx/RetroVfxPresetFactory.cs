using System;
using System.Collections.Generic;
using DansToolbox.RetroVfx;
using UnityEngine;

namespace DansToolbox.EditorTools.RetroVfx
{
    internal readonly struct RetroVfxPresetDescriptor
    {
        internal RetroVfxPresetDescriptor(string id, string name, string description, RetroVfxEffectFamily family)
        {
            Id = id;
            Name = name;
            Description = description;
            Family = family;
        }

        internal string Id { get; }
        internal string Name { get; }
        internal string Description { get; }
        internal RetroVfxEffectFamily Family { get; }
    }

    internal static class RetroVfxPresetFactory
    {
        internal static readonly IReadOnlyList<RetroVfxPresetDescriptor> Presets =
            new[]
            {
                Preset("sharp-impact", "SHARP IMPACT", "Directional hit spark and snap ring", RetroVfxEffectFamily.Impact),
                Preset("heavy-impact", "HEAVY IMPACT", "Dense blunt hit with chunks and pressure", RetroVfxEffectFamily.Impact),
                Preset("ground-slam", "GROUND SLAM", "Ground fan, dust wall, and shockwave", RetroVfxEffectFamily.Impact),
                Preset("armor-hit", "ARMOR HIT", "Cold metal sparks and shield flash", RetroVfxEffectFamily.Impact),

                Preset("pixel-blast", "PIXEL BOMB", "Animated old-school pixel bomb explosion", RetroVfxEffectFamily.Explosion),
                Preset("barrel-blast", "BARREL BLAST", "Heavy fireball, debris, and rolling smoke", RetroVfxEffectFamily.Explosion),
                Preset("firecracker", "FIRECRACKER", "Small repeated pops and bright fragments", RetroVfxEffectFamily.Explosion),
                Preset("plasma-bloom", "PLASMA DETONATION", "Layered energy bloom and expanding rings", RetroVfxEffectFamily.Explosion),

                Preset("sidearm-flash", "PISTOL FLASH", "Compact star-shaped muzzle snap", RetroVfxEffectFamily.MuzzleFlash),
                Preset("rifle-burst", "RIFLE BURST", "Long hot muzzle tongue with brass flecks", RetroVfxEffectFamily.MuzzleFlash),
                Preset("shotgun-blast", "SHOTGUN BLAST", "Wide muzzle bloom, pellets, and smoke", RetroVfxEffectFamily.MuzzleFlash),
                Preset("cannon-flash", "CANNON FLASH", "Huge barrel flash with lingering smoke", RetroVfxEffectFamily.MuzzleFlash),
                Preset("laser-shot", "LASER SHOT", "Neon beam lance and energy recoil ring", RetroVfxEffectFamily.MuzzleFlash),

                Preset("blood-splat", "BLOOD SPLAT", "Central pixel splat with detached droplets", RetroVfxEffectFamily.Blood),
                Preset("blood-spray", "BLOOD SPRAY", "Fast directional spray and small mist", RetroVfxEffectFamily.Blood),
                Preset("heavy-gore", "HEAVY GORE", "Large dark splat, chunks, and falling drops", RetroVfxEffectFamily.Blood),

                Preset("quick-slash", "QUICK SLASH", "Thin fast sword arc with edge sparks", RetroVfxEffectFamily.SwordSwing),
                Preset("heavy-cleave", "HEAVY CLEAVE", "Broad crescent, delayed trail, and impact", RetroVfxEffectFamily.SwordSwing),
                Preset("spin-slash", "SPIN SLASH", "Full circular blade sweep and wind ring", RetroVfxEffectFamily.SwordSwing),
                Preset("parry-spark", "PARRY", "Crossed flash, metal sparks, and recoil ring", RetroVfxEffectFamily.SwordSwing),

                Preset("smoke-puff", "SMOKE PUFF", "Layered soft smoke cluster", RetroVfxEffectFamily.Smoke),
                Preset("dust-kick", "DUST KICK", "Low directional dust and ground flecks", RetroVfxEffectFamily.Smoke),
                Preset("steam-vent", "STEAM VENT", "Narrow rising puffs with repeating pulses", RetroVfxEffectFamily.Smoke),

                Preset("arcane-burst", "ARCANE BURST", "Rune circle, star core, and arc sparks", RetroVfxEffectFamily.EnergyBurst),
                Preset("shield-pop", "SHIELD POP", "Bright shield wave and crystalline fragments", RetroVfxEffectFamily.EnergyBurst),
                Preset("teleport-burst", "TELEPORT", "Collapsing ring and rising energy trail", RetroVfxEffectFamily.EnergyBurst),

                Preset("fire-cast", "FIRE CAST", "Hot cast core, embers, and flame trail", RetroVfxEffectFamily.Magic),
                Preset("ice-shatter", "ICE SHATTER", "Cold flash and angular ice fragments", RetroVfxEffectFamily.Magic),
                Preset("lightning-zap", "LIGHTNING ZAP", "Branch-like beam flicker and electric sparks", RetroVfxEffectFamily.Magic),
                Preset("poison-pop", "POISON POP", "Toxic bubbles, droplets, and vapor ring", RetroVfxEffectFamily.Magic),

                Preset("coin-glint", "COIN GLINT", "Quick gold glint and tiny star flecks", RetroVfxEffectFamily.Pickup),
                Preset("power-up", "POWER UP", "Looping rising energy celebration", RetroVfxEffectFamily.Pickup),
                Preset("heal-burst", "HEAL BURST", "Soft green pulse, crosses, and rising motes", RetroVfxEffectFamily.Pickup),

                Preset("item-shine", "ITEM SHINE", "Classic rotating shine behind an item", RetroVfxEffectFamily.ItemShine),
                Preset("rare-halo", "RARE HALO", "Blue rune halo with orbiting glints", RetroVfxEffectFamily.ItemShine),
                Preset("legendary-rays", "LEGENDARY RAYS", "Large gold rays and layered prestige rings", RetroVfxEffectFamily.ItemShine),

                Preset("footstep-dust", "FOOTSTEP DUST", "Small ground puff and grit", RetroVfxEffectFamily.Environment),
                Preset("leaf-burst", "LEAF BURST", "Windblown leaves with varied spin", RetroVfxEffectFamily.Environment),
                Preset("water-splash", "WATER SPLASH", "Crown splash, droplets, and surface ring", RetroVfxEffectFamily.Environment),
                Preset("bubble-pop", "BUBBLE POP", "Bubble shell, droplets, and tiny glint", RetroVfxEffectFamily.Environment)
            };

        internal static RetroVfxRecipe CreateWorkingRecipe(string presetId = "sharp-impact")
        {
            RetroVfxRecipe recipe = ScriptableObject.CreateInstance<RetroVfxRecipe>();
            recipe.hideFlags = HideFlags.HideAndDontSave;
            Apply(presetId, recipe);
            return recipe;
        }

        internal static void ApplyVariation(string presetId, RetroVfxRecipe recipe, int seed)
        {
            Apply(presetId, recipe);
            RandomizeUnlocked(recipe, seed);
        }

        internal static void Apply(string presetId, RetroVfxRecipe recipe)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            Reset(recipe);
            switch (presetId)
            {
                case "heavy-impact": ConfigureHeavyImpact(recipe); break;
                case "ground-slam": ConfigureGroundSlam(recipe); break;
                case "armor-hit": ConfigureArmorHit(recipe); break;
                case "pixel-blast": ConfigurePixelBomb(recipe); break;
                case "barrel-blast": ConfigureBarrelBlast(recipe); break;
                case "firecracker": ConfigureFirecracker(recipe); break;
                case "plasma-bloom": ConfigurePlasmaDetonation(recipe); break;
                case "sidearm-flash": ConfigurePistolFlash(recipe); break;
                case "rifle-burst": ConfigureRifleBurst(recipe); break;
                case "shotgun-blast": ConfigureShotgunBlast(recipe); break;
                case "cannon-flash": ConfigureCannonFlash(recipe); break;
                case "laser-shot": ConfigureLaserShot(recipe); break;
                case "blood-splat": ConfigureBloodSplat(recipe); break;
                case "blood-spray": ConfigureBloodSpray(recipe); break;
                case "heavy-gore": ConfigureHeavyGore(recipe); break;
                case "quick-slash": ConfigureQuickSlash(recipe); break;
                case "heavy-cleave": ConfigureHeavyCleave(recipe); break;
                case "spin-slash": ConfigureSpinSlash(recipe); break;
                case "parry-spark": ConfigureParry(recipe); break;
                case "smoke-puff": ConfigureSmokePuff(recipe); break;
                case "dust-kick": ConfigureDustKick(recipe); break;
                case "steam-vent": ConfigureSteamVent(recipe); break;
                case "arcane-burst": ConfigureArcaneBurst(recipe); break;
                case "shield-pop": ConfigureShieldPop(recipe); break;
                case "teleport-burst": ConfigureTeleport(recipe); break;
                case "fire-cast": ConfigureFireCast(recipe); break;
                case "ice-shatter": ConfigureIceShatter(recipe); break;
                case "lightning-zap": ConfigureLightningZap(recipe); break;
                case "poison-pop": ConfigurePoisonPop(recipe); break;
                case "coin-glint": ConfigureCoinGlint(recipe); break;
                case "power-up": ConfigurePowerUp(recipe); break;
                case "heal-burst": ConfigureHealBurst(recipe); break;
                case "item-shine": ConfigureItemShine(recipe); break;
                case "rare-halo": ConfigureRareHalo(recipe); break;
                case "legendary-rays": ConfigureLegendaryRays(recipe); break;
                case "footstep-dust": ConfigureFootstepDust(recipe); break;
                case "leaf-burst": ConfigureLeafBurst(recipe); break;
                case "water-splash": ConfigureWaterSplash(recipe); break;
                case "bubble-pop": ConfigureBubblePop(recipe); break;
                default: ConfigureSharpImpact(recipe); break;
            }
            FinalizeRecipe(recipe);
            recipe.Normalize();
        }

        internal static void RandomizeUnlocked(RetroVfxRecipe recipe, int seed)
        {
            recipe.Normalize();
            recipe.seed = seed;
            System.Random random = new System.Random(seed);
            recipe.duration *= Range(random, 0.9f, 1.12f);
            recipe.scale *= Range(random, 0.92f, 1.1f);
            recipe.direction = Mathf.DeltaAngle(0f, recipe.direction + Range(random, -7f, 7f));
            recipe.primaryColor = ShiftHue(recipe.primaryColor, Range(random, -0.025f, 0.025f), Range(random, 0.94f, 1.06f));
            recipe.secondaryColor = ShiftHue(recipe.secondaryColor, Range(random, -0.018f, 0.018f), Range(random, 0.96f, 1.05f));

            foreach (RetroVfxLayer layer in recipe.layers)
            {
                if (layer.locked)
                {
                    continue;
                }

                layer.count = Mathf.Max(1, Mathf.RoundToInt(layer.count * Range(random, 0.66f, 1.45f)));
                layer.lifetime *= Range(random, 0.78f, 1.24f);
                layer.speed *= Range(random, 0.68f, 1.36f);
                layer.size *= Range(random, 0.74f, 1.3f);
                layer.delay = Mathf.Max(0f, layer.delay + Range(random, -0.035f, 0.055f));
                layer.burstInterval *= Range(random, 0.88f, 1.14f);
                layer.spread = Mathf.Clamp(layer.spread * Range(random, 0.76f, 1.2f), 0f, 360f);
                layer.emissionRadius *= Range(random, 0.78f, 1.28f);
                layer.rotation += Range(random, -18f, 18f);
                layer.rotationSpeed *= Range(random, 0.78f, 1.25f);
                layer.offset += new Vector2(Range(random, -0.035f, 0.035f), Range(random, -0.035f, 0.035f));
                layer.aspect = Vector2.Scale(layer.aspect, new Vector2(Range(random, 0.88f, 1.14f), Range(random, 0.88f, 1.14f)));
                layer.startColor = ShiftHue(layer.startColor, Range(random, -0.02f, 0.02f), Range(random, 0.94f, 1.07f));
                layer.endColor = ShiftHue(layer.endColor, Range(random, -0.02f, 0.02f), Range(random, 0.94f, 1.07f));
                layer.noiseStrength *= Range(random, 0.72f, 1.35f);
                layer.noiseFrequency *= Range(random, 0.82f, 1.22f);
                layer.trailLifetime *= Range(random, 0.82f, 1.2f);
                layer.edgeGlow = Mathf.Clamp01(layer.edgeGlow * Range(random, 0.82f, 1.18f));
                layer.emission = Mathf.Clamp(layer.emission * Range(random, 0.88f, 1.16f), 0f, 2f);
                layer.flowSpeed *= Range(random, 0.8f, 1.25f);
            }
            recipe.Normalize();
        }

        internal static RetroVfxPresetDescriptor Find(string id)
        {
            foreach (RetroVfxPresetDescriptor preset in Presets)
            {
                if (string.Equals(preset.Id, id, StringComparison.Ordinal))
                {
                    return preset;
                }
            }
            return Presets[0];
        }

        private static void ConfigureSharpImpact(RetroVfxRecipe r)
        {
            Setup(r, "Sharp Impact", RetroVfxEffectFamily.Impact, 0.42f, 1f, 0f, Ember(), WarmWhite());
            Add(r, Stationary(Layer("Core Snap", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.Starburst, 1, 0.1f, 0f, 0.62f, 0f, 360f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive)));
            Add(r, Directional(Layer("Cut Sparks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 18, 0.28f, 8.2f, 0.13f, 0f, 64f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 3.2f));
            Add(r, Stationary(Layer("Snap Ring", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Shockwave, 1, 0.3f, 0f, 0.5f, 0.012f, 360f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
        }

        private static void ConfigureHeavyImpact(RetroVfxRecipe r)
        {
            Setup(r, "Heavy Impact", RetroVfxEffectFamily.Impact, 0.82f, 1.25f, 0f, new Color(1f, 0.24f, 0.03f), WarmWhite());
            Add(r, Stationary(Layer("Heavy Core", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.Starburst, 1, 0.16f, 0f, 1.05f, 0f, 360f, Color.white, Clear(Color.white), RetroVfxBlendMode.Additive)));
            Add(r, Directional(Layer("Impact Streaks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 34, 0.5f, 7.2f, 0.19f, 0f, 150f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 3.8f));
            RetroVfxLayer chunks = Layer("Heavy Chunks", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.PixelChunk, 20, 0.75f, 4.5f, 0.18f, 0.02f, 205f, new Color(0.62f, 0.18f, 0.04f), Clear(new Color(0.18f, 0.04f, 0.01f)), RetroVfxBlendMode.Alpha, 1f);
            chunks.randomRotation = true; chunks.rotationSpeed = 260f; Add(r, chunks);
            Add(r, Stationary(Layer("Pressure", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Shockwave, 1, 0.48f, 0f, 0.85f, 0.025f, 360f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive)));
        }

        private static void ConfigureGroundSlam(RetroVfxRecipe r)
        {
            Setup(r, "Ground Slam", RetroVfxEffectFamily.Impact, 1.2f, 1.5f, 90f, new Color(0.72f, 0.38f, 0.14f), new Color(1f, 0.78f, 0.35f));
            Add(r, Stationary(Layer("Ground Flash", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.Starburst, 1, 0.14f, 0f, 0.9f, 0f, 360f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive)));
            RetroVfxLayer fan = Directional(Layer("Rock Fan", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.PixelChunk, 30, 0.9f, 5.5f, 0.17f, 0.02f, 130f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Alpha, 1.25f), 1.4f);
            fan.randomRotation = true; fan.rotationSpeed = 310f; Add(r, fan);
            RetroVfxLayer dust = Directional(Layer("Dust Wall", RetroVfxLayerKind.Smoke, RetroVfxSpriteStyle.PixelSmoke, 12, 1.05f, 2.2f, 0.62f, 0.08f, 115f, new Color(0.5f, 0.34f, 0.2f, 0.78f), Clear(new Color(0.16f, 0.12f, 0.09f)), RetroVfxBlendMode.Alpha, 0.65f), 1f);
            dust.emissionRadius = 0.22f; Add(r, dust);
            Add(r, Stationary(Layer("Ground Wave", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Shockwave, 1, 0.68f, 0f, 1.3f, 0.04f, 360f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive)));
        }

        private static void ConfigureArmorHit(RetroVfxRecipe r)
        {
            Setup(r, "Armor Hit", RetroVfxEffectFamily.Impact, 0.55f, 1f, 0f, new Color(0.48f, 0.78f, 1f), Color.white);
            Add(r, Stationary(Layer("Metal Flash", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.Glint, 2, 0.18f, 0f, 0.54f, 0f, 360f, Color.white, Clear(Color.white), RetroVfxBlendMode.Additive)));
            Add(r, Directional(Layer("Metal Sparks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 26, 0.42f, 7.4f, 0.09f, 0f, 110f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 4.2f));
            Add(r, Stationary(Layer("Shield Ripple", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Ring, 1, 0.48f, 0f, 0.72f, 0.025f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
        }

        private static void ConfigurePixelBomb(RetroVfxRecipe r)
        {
            Setup(r, "Pixel Bomb", RetroVfxEffectFamily.Explosion, 1.15f, 1.45f, 0f, new Color(1f, 0.2f, 0.015f), new Color(1f, 0.88f, 0.18f));
            Add(r, Stationary(Layer("Yellow Pixel Core", RetroVfxLayerKind.Burst, RetroVfxSpriteStyle.PixelExplosion, 1, 0.48f, 0f, 1.05f, 0f, 360f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive)));
            Add(r, Stationary(Layer("Orange Pixel Fire", RetroVfxLayerKind.Burst, RetroVfxSpriteStyle.PixelExplosion, 1, 0.62f, 0f, 1.42f, 0.055f, 360f, new Color(1f, 0.46f, 0.03f), Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            Add(r, Stationary(Layer("Red Pixel Rim", RetroVfxLayerKind.Burst, RetroVfxSpriteStyle.PixelExplosion, 1, 0.76f, 0f, 1.72f, 0.11f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Alpha)));
            RetroVfxLayer debris = Layer("Bomb Fragments", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.PixelChunk, 30, 0.86f, 5.4f, 0.12f, 0.08f, 360f, new Color(0.95f, 0.24f, 0.02f), Clear(new Color(0.22f, 0.04f, 0.01f)), RetroVfxBlendMode.Alpha, 0.75f);
            debris.randomRotation = true; debris.rotationSpeed = 360f; Add(r, debris);
            RetroVfxLayer smoke = Rising(Layer("Pixel Smoke", RetroVfxLayerKind.Smoke, RetroVfxSpriteStyle.PixelSmoke, 9, 0.95f, 0.75f, 0.55f, 0.34f, 360f, new Color(0.32f, 0.2f, 0.15f, 0.82f), Clear(new Color(0.08f, 0.07f, 0.07f)), RetroVfxBlendMode.Alpha));
            smoke.emissionRadius = 0.28f; Add(r, smoke);
        }

        private static void ConfigureBarrelBlast(RetroVfxRecipe r)
        {
            Setup(r, "Barrel Blast", RetroVfxEffectFamily.Explosion, 1.5f, 1.65f, 0f, new Color(0.92f, 0.18f, 0.02f), new Color(1f, 0.72f, 0.08f));
            Add(r, Stationary(Layer("Fireball", RetroVfxLayerKind.Burst, RetroVfxSpriteStyle.PixelExplosion, 3, 0.72f, 0f, 1.15f, 0f, 360f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            RetroVfxLayer chunks = Layer("Barrel Chunks", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.PixelChunk, 42, 1.15f, 6f, 0.16f, 0.04f, 360f, new Color(0.42f, 0.18f, 0.06f), Clear(new Color(0.09f, 0.04f, 0.02f)), RetroVfxBlendMode.Alpha, 1.1f);
            chunks.randomRotation = true; chunks.rotationSpeed = 420f; Add(r, chunks);
            RetroVfxLayer smoke = Rising(Layer("Rolling Smoke", RetroVfxLayerKind.Smoke, RetroVfxSpriteStyle.PixelSmoke, 16, 1.3f, 0.9f, 0.76f, 0.25f, 360f, new Color(0.24f, 0.2f, 0.18f, 0.88f), Clear(new Color(0.06f, 0.06f, 0.065f)), RetroVfxBlendMode.Alpha));
            smoke.emissionRadius = 0.36f; Add(r, smoke);
            Add(r, Stationary(Layer("Blast Wave", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Shockwave, 1, 0.64f, 0f, 1.35f, 0.03f, 360f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive)));
        }

        private static void ConfigureFirecracker(RetroVfxRecipe r)
        {
            Setup(r, "Firecracker", RetroVfxEffectFamily.Explosion, 0.85f, 0.75f, 0f, new Color(1f, 0.18f, 0.03f), new Color(1f, 0.92f, 0.45f));
            RetroVfxLayer pops = Stationary(Layer("Quick Pops", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.Starburst, 1, 0.13f, 0f, 0.48f, 0f, 360f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive));
            pops.burstCount = 4; pops.burstInterval = 0.14f; pops.emissionRadius = 0.3f; Add(r, pops);
            RetroVfxLayer flecks = Layer("Crackle Flecks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 8, 0.34f, 5.8f, 0.08f, 0f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive);
            flecks.burstCount = 3; flecks.burstInterval = 0.16f; Add(r, flecks);
        }

        private static void ConfigurePlasmaDetonation(RetroVfxRecipe r)
        {
            Setup(r, "Plasma Detonation", RetroVfxEffectFamily.Explosion, 1.25f, 1.2f, 0f, new Color(0.18f, 0.6f, 1f), new Color(0.86f, 0.28f, 1f));
            Add(r, Stationary(Layer("White Plasma", RetroVfxLayerKind.Burst, RetroVfxSpriteStyle.SoftDisc, 10, 0.52f, 0f, 0.9f, 0f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            Add(r, Stationary(Layer("Blue Wave", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Shockwave, 1, 0.82f, 0f, 1.15f, 0.035f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            Add(r, Stationary(Layer("Violet Rune", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Rune, 1, 0.95f, 0f, 0.85f, 0.11f, 360f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive)));
            Add(r, Layer("Plasma Sparks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 34, 0.72f, 4.2f, 0.1f, 0.06f, 360f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
        }

        private static void ConfigurePistolFlash(RetroVfxRecipe r)
        {
            Setup(r, "Pistol Flash", RetroVfxEffectFamily.MuzzleFlash, 0.24f, 0.72f, 0f, Ember(), WarmWhite());
            Add(r, Stationary(Layer("Muzzle Star", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.MuzzleFlash, 1, 0.09f, 0f, 0.78f, 0f, 360f, Color.white, Clear(r.secondaryColor), RetroVfxBlendMode.Additive)));
            Add(r, Directional(Layer("Hot Flecks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 8, 0.2f, 6.4f, 0.07f, 0f, 42f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 3f));
        }

        private static void ConfigureRifleBurst(RetroVfxRecipe r)
        {
            Setup(r, "Rifle Burst", RetroVfxEffectFamily.MuzzleFlash, 0.32f, 0.9f, 0f, new Color(1f, 0.34f, 0.03f), WarmWhite());
            RetroVfxLayer tongue = Stationary(Layer("Rifle Tongue", RetroVfxLayerKind.Beam, RetroVfxSpriteStyle.MuzzleFlash, 1, 0.12f, 0f, 0.92f, 0f, 360f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            tongue.aspect = new Vector2(1.8f, 0.72f); Add(r, tongue);
            Add(r, Directional(Layer("Rifle Sparks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 14, 0.28f, 8.5f, 0.065f, 0f, 30f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 4.3f));
            RetroVfxLayer brass = Directional(Layer("Brass Flecks", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.PixelChunk, 4, 0.55f, 2.6f, 0.07f, 0.04f, 18f, new Color(0.85f, 0.58f, 0.12f), Clear(new Color(0.3f, 0.15f, 0.03f)), RetroVfxBlendMode.Alpha, 0.8f), 1f);
            brass.rotation = 110f; brass.gravity = 1.2f; brass.randomRotation = true; Add(r, brass);
        }

        private static void ConfigureShotgunBlast(RetroVfxRecipe r)
        {
            Setup(r, "Shotgun Blast", RetroVfxEffectFamily.MuzzleFlash, 0.65f, 1.35f, 0f, new Color(1f, 0.28f, 0.02f), WarmWhite());
            RetroVfxLayer bloom = Stationary(Layer("Wide Bloom", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.MuzzleFlash, 2, 0.15f, 0f, 1f, 0f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            bloom.aspect = new Vector2(1.45f, 1f); Add(r, bloom);
            Add(r, Directional(Layer("Pellet Streaks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 26, 0.34f, 9f, 0.075f, 0.01f, 72f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 3.5f));
            RetroVfxLayer smoke = Directional(Layer("Barrel Smoke", RetroVfxLayerKind.Smoke, RetroVfxSpriteStyle.PixelSmoke, 7, 0.7f, 1.1f, 0.38f, 0.1f, 55f, new Color(0.46f, 0.43f, 0.38f, 0.68f), Clear(new Color(0.12f, 0.12f, 0.12f)), RetroVfxBlendMode.Alpha), 1f);
            smoke.emissionRadius = 0.08f; Add(r, smoke);
        }

        private static void ConfigureCannonFlash(RetroVfxRecipe r)
        {
            Setup(r, "Cannon Flash", RetroVfxEffectFamily.MuzzleFlash, 0.9f, 1.7f, 0f, new Color(1f, 0.26f, 0.015f), WarmWhite());
            RetroVfxLayer flash = Stationary(Layer("Cannon Flame", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.MuzzleFlash, 3, 0.22f, 0f, 1.2f, 0f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            flash.aspect = new Vector2(1.65f, 1.1f); Add(r, flash);
            Add(r, Directional(Layer("Cannon Sparks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 32, 0.48f, 8f, 0.12f, 0.01f, 66f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 4.5f));
            RetroVfxLayer smoke = Directional(Layer("Cannon Smoke", RetroVfxLayerKind.Smoke, RetroVfxSpriteStyle.PixelSmoke, 12, 0.92f, 1f, 0.52f, 0.12f, 48f, new Color(0.42f, 0.4f, 0.36f, 0.76f), Clear(new Color(0.1f, 0.1f, 0.11f)), RetroVfxBlendMode.Alpha), 1f);
            smoke.emissionRadius = 0.13f; Add(r, smoke);
        }

        private static void ConfigureLaserShot(RetroVfxRecipe r)
        {
            Setup(r, "Laser Shot", RetroVfxEffectFamily.MuzzleFlash, 0.38f, 1f, 0f, new Color(0.15f, 0.9f, 1f), Color.white);
            RetroVfxLayer beam = Directional(Layer("Laser Lance", RetroVfxLayerKind.Beam, RetroVfxSpriteStyle.Beam, 1, 0.18f, 12f, 0.16f, 0f, 8f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 7f);
            beam.aspect = new Vector2(2.6f, 0.5f); Add(r, beam);
            Add(r, Stationary(Layer("Energy Recoil", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Rune, 1, 0.34f, 0f, 0.55f, 0f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            Add(r, Directional(Layer("Ion Flecks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 12, 0.3f, 5.8f, 0.055f, 0.02f, 55f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 3.2f));
        }

        private static void ConfigureBloodSplat(RetroVfxRecipe r)
        {
            Setup(r, "Blood Splat", RetroVfxEffectFamily.Blood, 0.9f, 1f, 0f, new Color(0.68f, 0.015f, 0.025f), new Color(0.28f, 0.005f, 0.01f));
            RetroVfxLayer splat = Stationary(Layer("Main Splat", RetroVfxLayerKind.Splat, RetroVfxSpriteStyle.BloodSplat, 1, 0.72f, 0f, 0.95f, 0f, 360f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha));
            splat.randomRotation = true; Add(r, splat);
            Add(r, Directional(Layer("Detached Drops", RetroVfxLayerKind.Trail, RetroVfxSpriteStyle.BloodDrop, 18, 0.72f, 4.8f, 0.11f, 0.015f, 145f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha, 0.75f), 3.4f));
            RetroVfxLayer beads = Layer("Blood Beads", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.SoftDisc, 9, 0.65f, 3.2f, 0.08f, 0.03f, 210f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha, 1.25f);
            beads.sizeRandomness = 0.35f; Add(r, beads);
        }

        private static void ConfigureBloodSpray(RetroVfxRecipe r)
        {
            Setup(r, "Blood Spray", RetroVfxEffectFamily.Blood, 0.85f, 0.9f, 0f, new Color(0.76f, 0.02f, 0.035f), new Color(0.24f, 0.005f, 0.008f));
            Add(r, Directional(Layer("Long Spray", RetroVfxLayerKind.Trail, RetroVfxSpriteStyle.BloodDrop, 32, 0.66f, 7.4f, 0.08f, 0f, 58f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha, 1.1f), 4.8f));
            RetroVfxLayer mist = Directional(Layer("Fine Mist", RetroVfxLayerKind.Burst, RetroVfxSpriteStyle.SoftDisc, 22, 0.46f, 5.2f, 0.045f, 0.02f, 82f, new Color(r.primaryColor.r, r.primaryColor.g, r.primaryColor.b, 0.68f), Clear(r.primaryColor), RetroVfxBlendMode.Alpha), 1f);
            mist.sizeRandomness = 0.55f; Add(r, mist);
        }

        private static void ConfigureHeavyGore(RetroVfxRecipe r)
        {
            Setup(r, "Heavy Gore", RetroVfxEffectFamily.Blood, 1.35f, 1.35f, 0f, new Color(0.58f, 0.008f, 0.015f), new Color(0.16f, 0.002f, 0.004f));
            RetroVfxLayer splat = Stationary(Layer("Heavy Splat", RetroVfxLayerKind.Splat, RetroVfxSpriteStyle.BloodSplat, 2, 1.05f, 0f, 1.2f, 0f, 360f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha));
            splat.randomRotation = true; Add(r, splat);
            RetroVfxLayer chunks = Layer("Dark Chunks", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.PixelChunk, 18, 1.15f, 4.4f, 0.14f, 0.02f, 300f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha, 1.6f);
            chunks.randomRotation = true; chunks.rotationSpeed = 260f; Add(r, chunks);
            Add(r, Falling(Layer("Falling Drops", RetroVfxLayerKind.Trail, RetroVfxSpriteStyle.BloodDrop, 22, 1.05f, 3.2f, 0.1f, 0.04f, 140f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha, 1.4f), 3.8f));
        }

        private static void ConfigureQuickSlash(RetroVfxRecipe r)
        {
            Setup(r, "Quick Slash", RetroVfxEffectFamily.SwordSwing, 0.38f, 1.25f, 0f, new Color(0.42f, 0.82f, 1f), Color.white);
            RetroVfxLayer arc = Stationary(Layer("Blade Arc", RetroVfxLayerKind.Arc, RetroVfxSpriteStyle.SlashArc, 1, 0.24f, 0f, 1.25f, 0f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            arc.aspect = new Vector2(1.3f, 1f); Add(r, arc);
            Add(r, Directional(Layer("Edge Sparks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 12, 0.28f, 6.4f, 0.06f, 0.06f, 110f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 3.1f));
        }

        private static void ConfigureHeavyCleave(RetroVfxRecipe r)
        {
            Setup(r, "Heavy Cleave", RetroVfxEffectFamily.SwordSwing, 0.72f, 1.55f, -20f, new Color(0.3f, 0.7f, 1f), Color.white);
            RetroVfxLayer crescent = Stationary(Layer("Heavy Crescent", RetroVfxLayerKind.Arc, RetroVfxSpriteStyle.Crescent, 1, 0.46f, 0f, 1.45f, 0f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            crescent.rotation = -28f;
            crescent.aspect = new Vector2(1.25f, 1f);
            crescent.trailEnabled = true;
            crescent.trailLifetime = 0.18f;
            crescent.trailWidth = 0.12f;
            crescent.trailColor = r.primaryColor;
            Add(r, crescent);
            RetroVfxLayer echo = Stationary(Layer("Arc Echo", RetroVfxLayerKind.Arc, RetroVfxSpriteStyle.SlashArc, 1, 0.48f, 0f, 1.65f, 0.08f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            echo.rotation = -28f; Add(r, echo);
            Add(r, Directional(Layer("Cleave Impact", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.PixelChunk, 18, 0.65f, 4.5f, 0.1f, 0.16f, 105f, new Color(0.62f, 0.76f, 0.9f), Clear(r.primaryColor), RetroVfxBlendMode.Alpha, 0.8f), 1f));
        }

        private static void ConfigureSpinSlash(RetroVfxRecipe r)
        {
            Setup(r, "Spin Slash", RetroVfxEffectFamily.SwordSwing, 0.9f, 1.5f, 0f, new Color(0.22f, 0.85f, 1f), Color.white);
            Add(r, Stationary(Layer("Full Blade Ring", RetroVfxLayerKind.Arc, RetroVfxSpriteStyle.Ring, 1, 0.5f, 0f, 1.4f, 0f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            RetroVfxLayer wind = Stationary(Layer("Wind Echoes", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Shockwave, 1, 0.62f, 0f, 1.65f, 0.06f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            wind.burstCount = 2; wind.burstInterval = 0.14f; Add(r, wind);
            Add(r, Layer("Spin Flecks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 24, 0.5f, 4.6f, 0.07f, 0.03f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
        }

        private static void ConfigureParry(RetroVfxRecipe r)
        {
            Setup(r, "Parry", RetroVfxEffectFamily.SwordSwing, 0.48f, 0.95f, 0f, new Color(1f, 0.72f, 0.12f), Color.white);
            RetroVfxLayer crossA = Stationary(Layer("Cross Flash A", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.Glint, 1, 0.18f, 0f, 0.78f, 0f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            crossA.rotation = 45f; Add(r, crossA);
            RetroVfxLayer crossB = crossA.Clone(); crossB.name = "Cross Flash B"; crossB.rotation = -45f; Add(r, crossB);
            Add(r, Layer("Parry Sparks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 30, 0.42f, 7.4f, 0.075f, 0.02f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            Add(r, Stationary(Layer("Recoil Ring", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Shockwave, 1, 0.4f, 0f, 0.64f, 0.03f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
        }

        private static void ConfigureSmokePuff(RetroVfxRecipe r)
        {
            Setup(r, "Smoke Puff", RetroVfxEffectFamily.Smoke, 1.65f, 1f, 0f, new Color(0.56f, 0.55f, 0.53f, 0.82f), new Color(0.24f, 0.24f, 0.26f, 0.7f));
            RetroVfxLayer large = Rising(Layer("Puff Cluster", RetroVfxLayerKind.Smoke, RetroVfxSpriteStyle.PixelSmoke, 14, 1.45f, 0.8f, 0.58f, 0f, 360f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha));
            large.emissionRadius = 0.25f; large.sizeRandomness = 0.36f; Add(r, large);
            RetroVfxLayer soft = Rising(Layer("Soft Center", RetroVfxLayerKind.Smoke, RetroVfxSpriteStyle.SoftDisc, 9, 1.15f, 0.45f, 0.72f, 0.04f, 360f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha));
            soft.emissionRadius = 0.16f; Add(r, soft);
        }

        private static void ConfigureDustKick(RetroVfxRecipe r)
        {
            Setup(r, "Dust Kick", RetroVfxEffectFamily.Smoke, 1.15f, 1f, 20f, new Color(0.58f, 0.4f, 0.22f, 0.76f), new Color(0.27f, 0.19f, 0.12f, 0.58f));
            RetroVfxLayer dust = Directional(Layer("Dust Fan", RetroVfxLayerKind.Smoke, RetroVfxSpriteStyle.PixelSmoke, 14, 1.02f, 2.2f, 0.42f, 0f, 105f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha, 0.45f), 1f);
            dust.emissionRadius = 0.18f; Add(r, dust);
            RetroVfxLayer grit = Directional(Layer("Ground Grit", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.PixelChunk, 16, 0.72f, 3.3f, 0.08f, 0f, 82f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha, 1.3f), 1f);
            grit.randomRotation = true; Add(r, grit);
        }

        private static void ConfigureSteamVent(RetroVfxRecipe r)
        {
            Setup(r, "Steam Vent", RetroVfxEffectFamily.Smoke, 1.8f, 1f, 0f, new Color(0.82f, 0.88f, 0.9f, 0.62f), new Color(0.5f, 0.58f, 0.62f, 0.3f));
            r.loopPreview = true;
            RetroVfxLayer steam = Rising(Layer("Steam Pulses", RetroVfxLayerKind.Smoke, RetroVfxSpriteStyle.SoftDisc, 4, 0.92f, 1.8f, 0.38f, 0f, 30f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha));
            steam.burstCount = 4; steam.burstInterval = 0.36f; steam.emissionRadius = 0.08f; steam.aspect = new Vector2(0.72f, 1.2f); Add(r, steam);
        }

        private static void ConfigureArcaneBurst(RetroVfxRecipe r)
        {
            Setup(r, "Arcane Burst", RetroVfxEffectFamily.EnergyBurst, 0.95f, 1.1f, 0f, new Color(0.16f, 0.88f, 1f), new Color(0.82f, 0.22f, 1f));
            Add(r, Stationary(Layer("Arcane Star", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.Starburst, 1, 0.2f, 0f, 0.82f, 0f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            Add(r, Stationary(Layer("Rune Circle", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Rune, 1, 0.78f, 0f, 0.96f, 0.015f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            Add(r, Layer("Arc Sparks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 34, 0.68f, 4.8f, 0.095f, 0.05f, 360f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
        }

        private static void ConfigureShieldPop(RetroVfxRecipe r)
        {
            Setup(r, "Shield Pop", RetroVfxEffectFamily.EnergyBurst, 0.82f, 1.2f, 0f, new Color(0.1f, 0.54f, 1f), Color.white);
            Add(r, Stationary(Layer("Shield Core", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.SoftDisc, 1, 0.17f, 0f, 0.72f, 0f, 360f, Color.white, Clear(Color.white), RetroVfxBlendMode.Additive)));
            Add(r, Stationary(Layer("Shield Wave", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Rune, 1, 0.72f, 0f, 1.18f, 0.02f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            RetroVfxLayer fragments = Layer("Shield Fragments", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.PixelChunk, 24, 0.72f, 3.5f, 0.13f, 0.05f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive, -0.1f);
            fragments.randomRotation = true; fragments.rotationSpeed = 260f; Add(r, fragments);
        }

        private static void ConfigureTeleport(RetroVfxRecipe r)
        {
            Setup(r, "Teleport", RetroVfxEffectFamily.EnergyBurst, 1.1f, 1.2f, 0f, new Color(0.4f, 0.22f, 1f), new Color(0.16f, 0.88f, 1f));
            Add(r, Stationary(Layer("Collapse Rune", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Rune, 1, 0.72f, 0f, 1.25f, 0f, 360f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive)));
            RetroVfxLayer rise = Rising(Layer("Rising Pixels", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.PixelChunk, 34, 0.92f, 2.4f, 0.08f, 0.02f, 360f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            rise.emissionRadius = 0.55f; rise.randomRotation = true; Add(r, rise);
            Add(r, Stationary(Layer("Exit Glint", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.Glint, 1, 0.25f, 0f, 0.9f, 0.55f, 360f, Color.white, Clear(r.secondaryColor), RetroVfxBlendMode.Additive)));
        }

        private static void ConfigureFireCast(RetroVfxRecipe r)
        {
            Setup(r, "Fire Cast", RetroVfxEffectFamily.Magic, 0.95f, 1.1f, 0f, new Color(1f, 0.22f, 0.02f), new Color(1f, 0.78f, 0.08f));
            Add(r, Stationary(Layer("Fire Core", RetroVfxLayerKind.Burst, RetroVfxSpriteStyle.PixelExplosion, 1, 0.45f, 0f, 0.78f, 0f, 360f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            Add(r, Directional(Layer("Flame Tongues", RetroVfxLayerKind.Trail, RetroVfxSpriteStyle.MuzzleFlash, 12, 0.48f, 4.5f, 0.17f, 0.04f, 85f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 2.4f));
            Add(r, Rising(Layer("Embers", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.PixelChunk, 20, 0.78f, 2.8f, 0.065f, 0.05f, 360f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
        }

        private static void ConfigureIceShatter(RetroVfxRecipe r)
        {
            Setup(r, "Ice Shatter", RetroVfxEffectFamily.Magic, 0.9f, 1.15f, 0f, new Color(0.28f, 0.76f, 1f), Color.white);
            Add(r, Stationary(Layer("Ice Flash", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.Starburst, 1, 0.18f, 0f, 0.78f, 0f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            RetroVfxLayer shards = Layer("Ice Shards", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.PixelChunk, 36, 0.92f, 5.2f, 0.13f, 0.02f, 360f, r.primaryColor, Clear(new Color(0.1f, 0.35f, 0.65f)), RetroVfxBlendMode.Additive, 0.75f);
            shards.aspect = new Vector2(0.45f, 1.5f); shards.randomRotation = true; shards.rotationSpeed = 380f; Add(r, shards);
            Add(r, Stationary(Layer("Frost Ring", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Shockwave, 1, 0.62f, 0f, 0.95f, 0.03f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
        }

        private static void ConfigureLightningZap(RetroVfxRecipe r)
        {
            Setup(r, "Lightning Zap", RetroVfxEffectFamily.Magic, 0.5f, 1.1f, 0f, new Color(0.25f, 0.78f, 1f), Color.white);
            RetroVfxLayer beam = Directional(Layer("Electric Beam", RetroVfxLayerKind.Beam, RetroVfxSpriteStyle.Beam, 3, 0.12f, 10f, 0.12f, 0f, 12f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 6f);
            beam.aspect = new Vector2(2.8f, 0.42f); beam.burstCount = 3; beam.burstInterval = 0.07f; Add(r, beam);
            RetroVfxLayer forks = Directional(Layer("Electric Forks", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Spark, 16, 0.3f, 7f, 0.06f, 0.02f, 105f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive), 3.4f);
            forks.rotationSpeed = 180f; Add(r, forks);
            Add(r, Stationary(Layer("Zap Core", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.Glint, 1, 0.16f, 0f, 0.55f, 0f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
        }

        private static void ConfigurePoisonPop(RetroVfxRecipe r)
        {
            Setup(r, "Poison Pop", RetroVfxEffectFamily.Magic, 1.2f, 1.05f, 0f, new Color(0.28f, 0.95f, 0.12f), new Color(0.62f, 0.2f, 0.88f));
            Add(r, Stationary(Layer("Toxic Bubble", RetroVfxLayerKind.Aura, RetroVfxSpriteStyle.Bubble, 4, 0.48f, 0f, 0.52f, 0f, 360f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive)));
            RetroVfxLayer drops = Layer("Poison Drops", RetroVfxLayerKind.Trail, RetroVfxSpriteStyle.BloodDrop, 18, 0.82f, 3.8f, 0.09f, 0.08f, 360f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha, 0.6f);
            drops.randomRotation = true; Add(r, drops);
            RetroVfxLayer vapor = Rising(Layer("Toxic Vapor", RetroVfxLayerKind.Smoke, RetroVfxSpriteStyle.SoftDisc, 10, 1.05f, 0.65f, 0.42f, 0.16f, 360f, new Color(0.4f, 0.8f, 0.18f, 0.55f), Clear(r.secondaryColor), RetroVfxBlendMode.Alpha));
            vapor.emissionRadius = 0.28f; Add(r, vapor);
        }

        private static void ConfigureCoinGlint(RetroVfxRecipe r)
        {
            Setup(r, "Coin Glint", RetroVfxEffectFamily.Pickup, 0.58f, 0.75f, 0f, new Color(1f, 0.75f, 0.08f), Color.white);
            RetroVfxLayer glint = Stationary(Layer("Coin Glint", RetroVfxLayerKind.Aura, RetroVfxSpriteStyle.Glint, 1, 0.34f, 0f, 0.62f, 0f, 360f, Color.white, Clear(Color.white), RetroVfxBlendMode.Additive));
            glint.burstCount = 2; glint.burstInterval = 0.16f; Add(r, glint);
            Add(r, Layer("Gold Stars", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Starburst, 9, 0.48f, 2.4f, 0.075f, 0.04f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
        }

        private static void ConfigurePowerUp(RetroVfxRecipe r)
        {
            Setup(r, "Power Up", RetroVfxEffectFamily.Pickup, 1.55f, 1.15f, 0f, new Color(0.24f, 1f, 0.48f), new Color(0.28f, 0.68f, 1f));
            r.loopPreview = true;
            RetroVfxLayer rise = Rising(Layer("Rising Energy", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Glint, 18, 1.25f, 2.2f, 0.09f, 0f, 360f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive));
            rise.burstCount = 2; rise.burstInterval = 0.5f; rise.emissionRadius = 0.48f; Add(r, rise);
            RetroVfxLayer pulse = Stationary(Layer("Power Rings", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Rune, 1, 0.72f, 0f, 0.85f, 0f, 360f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive));
            pulse.burstCount = 3; pulse.burstInterval = 0.42f; Add(r, pulse);
        }

        private static void ConfigureHealBurst(RetroVfxRecipe r)
        {
            Setup(r, "Heal Burst", RetroVfxEffectFamily.Pickup, 1.05f, 1f, 0f, new Color(0.2f, 1f, 0.48f), Color.white);
            Add(r, Stationary(Layer("Healing Pulse", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Shockwave, 1, 0.65f, 0f, 0.95f, 0f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
            RetroVfxLayer crosses = Rising(Layer("Healing Glints", RetroVfxLayerKind.Aura, RetroVfxSpriteStyle.Glint, 12, 0.82f, 1.5f, 0.12f, 0.05f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            crosses.emissionRadius = 0.42f; Add(r, crosses);
            Add(r, Stationary(Layer("Soft Core", RetroVfxLayerKind.Burst, RetroVfxSpriteStyle.SoftDisc, 4, 0.52f, 0f, 0.58f, 0f, 360f, new Color(0.4f, 1f, 0.65f, 0.65f), Clear(r.primaryColor), RetroVfxBlendMode.Additive)));
        }

        private static void ConfigureItemShine(RetroVfxRecipe r)
        {
            Setup(r, "Item Shine", RetroVfxEffectFamily.ItemShine, 1.6f, 1f, 0f, new Color(1f, 0.82f, 0.18f), Color.white);
            r.loopPreview = true;
            RetroVfxLayer glint = Stationary(Layer("Rotating Shine", RetroVfxLayerKind.Aura, RetroVfxSpriteStyle.Glint, 1, 0.72f, 0f, 1.1f, 0f, 360f, Color.white, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            glint.burstCount = 3; glint.burstInterval = 0.48f; glint.rotationSpeed = 80f; Add(r, glint);
            RetroVfxLayer halo = Stationary(Layer("Item Halo", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Ring, 1, 1.2f, 0f, 0.78f, 0f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            halo.burstCount = 2; halo.burstInterval = 0.65f; Add(r, halo);
        }

        private static void ConfigureRareHalo(RetroVfxRecipe r)
        {
            Setup(r, "Rare Halo", RetroVfxEffectFamily.ItemShine, 1.8f, 1.1f, 0f, new Color(0.18f, 0.62f, 1f), new Color(0.72f, 0.38f, 1f));
            r.loopPreview = true;
            RetroVfxLayer rune = Stationary(Layer("Rare Rune", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Rune, 1, 1.2f, 0f, 0.95f, 0f, 360f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Additive));
            rune.burstCount = 2; rune.burstInterval = 0.75f; rune.rotationSpeed = 35f; Add(r, rune);
            RetroVfxLayer motes = Layer("Orbit Glints", RetroVfxLayerKind.Aura, RetroVfxSpriteStyle.Glint, 8, 1.25f, 0.8f, 0.1f, 0.05f, 360f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive);
            motes.emissionRadius = 0.62f; motes.rotationSpeed = 90f; Add(r, motes);
        }

        private static void ConfigureLegendaryRays(RetroVfxRecipe r)
        {
            Setup(r, "Legendary Rays", RetroVfxEffectFamily.ItemShine, 2f, 1.35f, 0f, new Color(1f, 0.58f, 0.04f), new Color(1f, 0.95f, 0.56f));
            r.loopPreview = true;
            RetroVfxLayer rays = Stationary(Layer("Prestige Rays", RetroVfxLayerKind.Aura, RetroVfxSpriteStyle.Starburst, 1, 1.35f, 0f, 1.55f, 0f, 360f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            rays.burstCount = 2; rays.burstInterval = 0.92f; rays.rotationSpeed = 26f; Add(r, rays);
            RetroVfxLayer rings = Stationary(Layer("Gold Halos", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Rune, 1, 1.15f, 0f, 1.1f, 0.05f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            rings.burstCount = 2; rings.burstInterval = 0.68f; Add(r, rings);
            RetroVfxLayer rise = Rising(Layer("Legend Motes", RetroVfxLayerKind.Sparks, RetroVfxSpriteStyle.Glint, 14, 1.25f, 1.7f, 0.1f, 0.08f, 360f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Additive));
            rise.emissionRadius = 0.62f; Add(r, rise);
        }

        private static void ConfigureFootstepDust(RetroVfxRecipe r)
        {
            Setup(r, "Footstep Dust", RetroVfxEffectFamily.Environment, 0.72f, 0.6f, 22f, new Color(0.56f, 0.42f, 0.28f, 0.72f), new Color(0.28f, 0.21f, 0.15f, 0.48f));
            RetroVfxLayer puff = Directional(Layer("Step Puff", RetroVfxLayerKind.Smoke, RetroVfxSpriteStyle.PixelSmoke, 7, 0.62f, 1.5f, 0.32f, 0f, 115f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha, 0.55f), 1f);
            puff.emissionRadius = 0.1f; Add(r, puff);
            RetroVfxLayer grit = Directional(Layer("Step Grit", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.PixelChunk, 8, 0.46f, 2.4f, 0.055f, 0f, 90f, r.secondaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha, 1.2f), 1f);
            grit.randomRotation = true; Add(r, grit);
        }

        private static void ConfigureLeafBurst(RetroVfxRecipe r)
        {
            Setup(r, "Leaf Burst", RetroVfxEffectFamily.Environment, 1.5f, 1f, 20f, new Color(0.42f, 0.72f, 0.12f), new Color(0.85f, 0.38f, 0.08f));
            RetroVfxLayer leaves = Directional(Layer("Wind Leaves", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.Leaf, 24, 1.3f, 3.8f, 0.16f, 0f, 125f, r.primaryColor, Clear(r.secondaryColor), RetroVfxBlendMode.Alpha, -0.1f), 1f);
            leaves.randomRotation = true; leaves.rotationSpeed = 320f; leaves.sizeRandomness = 0.42f; Add(r, leaves);
            RetroVfxLayer back = leaves.Clone(); back.name = "Autumn Leaves"; back.count = 12; back.delay = 0.08f; back.startColor = r.secondaryColor; back.speed *= 0.75f; Add(r, back);
        }

        private static void ConfigureWaterSplash(RetroVfxRecipe r)
        {
            Setup(r, "Water Splash", RetroVfxEffectFamily.Environment, 1.05f, 1f, 90f, new Color(0.18f, 0.62f, 1f, 0.82f), new Color(0.72f, 0.92f, 1f));
            Add(r, Stationary(Layer("Surface Ring", RetroVfxLayerKind.Ring, RetroVfxSpriteStyle.Shockwave, 1, 0.72f, 0f, 1f, 0f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Alpha)));
            RetroVfxLayer crown = Directional(Layer("Splash Crown", RetroVfxLayerKind.Trail, RetroVfxSpriteStyle.BloodDrop, 26, 0.82f, 4.8f, 0.09f, 0.02f, 130f, r.secondaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Alpha, 1.8f), 2.8f);
            crown.gravity = 1.45f; Add(r, crown);
            RetroVfxLayer mist = Directional(Layer("Water Mist", RetroVfxLayerKind.Burst, RetroVfxSpriteStyle.SoftDisc, 18, 0.48f, 3.5f, 0.05f, 0.02f, 140f, new Color(0.7f, 0.9f, 1f, 0.58f), Clear(r.primaryColor), RetroVfxBlendMode.Alpha), 1f);
            Add(r, mist);
        }

        private static void ConfigureBubblePop(RetroVfxRecipe r)
        {
            Setup(r, "Bubble Pop", RetroVfxEffectFamily.Environment, 0.68f, 0.72f, 0f, new Color(0.25f, 0.75f, 1f, 0.72f), Color.white);
            Add(r, Stationary(Layer("Bubble Shell", RetroVfxLayerKind.Aura, RetroVfxSpriteStyle.Bubble, 1, 0.34f, 0f, 0.72f, 0f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Alpha)));
            Add(r, Layer("Bubble Drops", RetroVfxLayerKind.Debris, RetroVfxSpriteStyle.SoftDisc, 12, 0.5f, 2.8f, 0.055f, 0.08f, 360f, r.primaryColor, Clear(r.primaryColor), RetroVfxBlendMode.Alpha, 0.3f));
            Add(r, Stationary(Layer("Wet Glint", RetroVfxLayerKind.Flash, RetroVfxSpriteStyle.Glint, 1, 0.18f, 0f, 0.42f, 0.02f, 360f, Color.white, Clear(Color.white), RetroVfxBlendMode.Additive)));
        }

        private static RetroVfxPresetDescriptor Preset(string id, string name, string description, RetroVfxEffectFamily family)
        {
            return new RetroVfxPresetDescriptor(id, name, description, family);
        }

        private static void Reset(RetroVfxRecipe recipe)
        {
            recipe.displayName = "Retro VFX";
            recipe.family = RetroVfxEffectFamily.Impact;
            recipe.artStyle = RetroVfxArtStyle.Pixel16;
            recipe.seed = 1337;
            recipe.duration = 0.65f;
            recipe.scale = 1f;
            recipe.intensity = 1f;
            recipe.direction = 0f;
            recipe.loopPreview = false;
            recipe.primaryColor = Ember();
            recipe.secondaryColor = WarmWhite();
            recipe.audioClip = null;
            recipe.layers = new List<RetroVfxLayer>();
            recipe.advanced = new RetroVfxAdvancedSettings();
        }

        private static void Setup(RetroVfxRecipe recipe, string name, RetroVfxEffectFamily family, float duration, float scale, float direction, Color primary, Color secondary)
        {
            recipe.displayName = name;
            recipe.family = family;
            recipe.duration = duration;
            recipe.scale = scale;
            recipe.direction = direction;
            recipe.primaryColor = primary;
            recipe.secondaryColor = secondary;
            recipe.artStyle = family switch
            {
                RetroVfxEffectFamily.Explosion => RetroVfxArtStyle.Pixel16,
                RetroVfxEffectFamily.Blood => RetroVfxArtStyle.Pixel16,
                RetroVfxEffectFamily.SwordSwing => RetroVfxArtStyle.Crisp2D,
                RetroVfxEffectFamily.Magic => RetroVfxArtStyle.SoftMagic,
                RetroVfxEffectFamily.EnergyBurst => RetroVfxArtStyle.StylizedToon,
                RetroVfxEffectFamily.ItemShine => RetroVfxArtStyle.Crisp2D,
                _ => RetroVfxArtStyle.StylizedToon
            };
        }

        private static RetroVfxLayer Layer(
            string name,
            RetroVfxLayerKind kind,
            RetroVfxSpriteStyle sprite,
            int count,
            float lifetime,
            float speed,
            float size,
            float delay,
            float spread,
            Color start,
            Color end,
            RetroVfxBlendMode blend,
            float gravity = 0f)
        {
            return new RetroVfxLayer
            {
                name = name,
                kind = kind,
                spriteStyle = sprite,
                blendMode = blend,
                sourceMode = RetroVfxSourceMode.Procedural,
                shape = RetroVfxParticleShape.Circle,
                motion = speed <= 0.001f ? RetroVfxMotionMode.Stationary : RetroVfxMotionMode.Radial,
                count = count,
                lifetime = lifetime,
                speed = speed,
                size = size,
                delay = delay,
                spread = spread,
                gravity = gravity,
                emissionRadius = kind == RetroVfxLayerKind.Smoke ? 0.18f : 0.015f,
                startColor = start,
                endColor = end,
                speedRandomness = 0.2f,
                sizeRandomness = 0.14f,
                stretch = sprite == RetroVfxSpriteStyle.BloodDrop ? 3.4f : 2.2f,
                sizeOverLifetime = CurveFor(kind),
                noiseProfile = NoiseFor(kind),
                noiseStrength = NoiseStrengthFor(kind),
                drag = kind == RetroVfxLayerKind.Smoke ? 0.28f : kind == RetroVfxLayerKind.Debris ? 0.08f : 0f,
                renderGeometry = GeometryFor(kind, sprite),
                trailEnabled = kind == RetroVfxLayerKind.Trail || kind == RetroVfxLayerKind.Beam,
                trailLifetime = kind == RetroVfxLayerKind.Beam ? 0.1f : 0.22f,
                trailWidth = Mathf.Max(0.015f, size * 0.42f),
                trailColor = start,
                edgeGlow = blend == RetroVfxBlendMode.Additive ? 0.62f : 0.18f,
                emission = blend == RetroVfxBlendMode.Additive ? 1.35f : 0.78f,
                softParticles = kind == RetroVfxLayerKind.Smoke || kind == RetroVfxLayerKind.Burst,
                flowSpeed = kind == RetroVfxLayerKind.Smoke ? new Vector2(0.03f, 0.09f) : Vector2.zero
            };
        }

        private static RetroVfxLayer Stationary(RetroVfxLayer layer)
        {
            layer.motion = RetroVfxMotionMode.Stationary;
            layer.speed = 0f;
            layer.shape = RetroVfxParticleShape.Point;
            return layer;
        }

        private static RetroVfxLayer Directional(RetroVfxLayer layer, float stretch)
        {
            layer.motion = RetroVfxMotionMode.Directional;
            layer.shape = RetroVfxParticleShape.Cone;
            layer.stretch = stretch;
            return layer;
        }

        private static RetroVfxLayer Rising(RetroVfxLayer layer)
        {
            layer.motion = RetroVfxMotionMode.Rising;
            layer.shape = RetroVfxParticleShape.Circle;
            return layer;
        }

        private static RetroVfxLayer Falling(RetroVfxLayer layer, float stretch)
        {
            layer.motion = RetroVfxMotionMode.Falling;
            layer.shape = RetroVfxParticleShape.Circle;
            layer.stretch = stretch;
            return layer;
        }

        private static void Add(RetroVfxRecipe recipe, RetroVfxLayer layer)
        {
            recipe.layers.Add(layer);
        }

        private static void FinalizeRecipe(RetroVfxRecipe recipe)
        {
            bool sourceLayerAssigned = false;
            for (int index = 0; index < recipe.layers.Count; index++)
            {
                RetroVfxLayer layer = recipe.layers[index];
                layer.phase = PhaseFor(layer, recipe.duration);
                layer.colorOverLifetime = RichGradient(layer, recipe);
                bool sourceCandidate = layer.kind == RetroVfxLayerKind.Burst ||
                                       layer.kind == RetroVfxLayerKind.Splat ||
                                       layer.kind == RetroVfxLayerKind.Flash ||
                                       layer.kind == RetroVfxLayerKind.Aura;
                if (!sourceLayerAssigned && sourceCandidate && recipe.family != RetroVfxEffectFamily.SwordSwing)
                {
                    layer.sourceMode = RetroVfxSourceMode.SourceLibrary;
                    sourceLayerAssigned = true;
                }
            }

            recipe.advanced.productionShader = true;
            recipe.advanced.softParticles = true;
            recipe.advanced.flipbookBlending = true;
            recipe.advanced.globalEmission = 1f;
            recipe.advanced.globalEdgeGlow = recipe.family == RetroVfxEffectFamily.Smoke ? 0.12f : 0.7f;
            recipe.advanced.cameraShakeEnabled = recipe.family == RetroVfxEffectFamily.Explosion ||
                                                    recipe.family == RetroVfxEffectFamily.Impact ||
                                                    recipe.family == RetroVfxEffectFamily.MuzzleFlash;
            recipe.advanced.cameraShakeAmplitude = recipe.family == RetroVfxEffectFamily.Explosion ? 0.42f : 0.18f;
            recipe.advanced.cameraShakeDuration = Mathf.Min(0.22f, recipe.duration * 0.35f);
            recipe.advanced.hitStopEventEnabled = recipe.family == RetroVfxEffectFamily.Impact ||
                                                   recipe.family == RetroVfxEffectFamily.SwordSwing;
            recipe.advanced.hitStopDuration = recipe.family == RetroVfxEffectFamily.SwordSwing ? 0.045f : 0.03f;
            if (recipe.family == RetroVfxEffectFamily.Explosion ||
                recipe.family == RetroVfxEffectFamily.MuzzleFlash ||
                recipe.family == RetroVfxEffectFamily.Magic)
            {
                recipe.advanced.lightEnabled = true;
                recipe.advanced.lightColor = recipe.primaryColor;
                recipe.advanced.lightIntensity = recipe.family == RetroVfxEffectFamily.Explosion ? 4.5f : 2.6f;
                recipe.advanced.lightRange = recipe.family == RetroVfxEffectFamily.Explosion ? 5f : 3f;
            }
        }

        private static RetroVfxPhase PhaseFor(RetroVfxLayer layer, float duration)
        {
            if (layer.delay > duration * 0.55f || layer.kind == RetroVfxLayerKind.Smoke)
            {
                return RetroVfxPhase.Decay;
            }
            return layer.kind switch
            {
                RetroVfxLayerKind.Flash => RetroVfxPhase.Primary,
                RetroVfxLayerKind.Burst => RetroVfxPhase.Primary,
                RetroVfxLayerKind.Arc => RetroVfxPhase.Primary,
                RetroVfxLayerKind.Splat => RetroVfxPhase.Primary,
                RetroVfxLayerKind.Beam => RetroVfxPhase.Primary,
                RetroVfxLayerKind.Aura when layer.name.IndexOf("charge", StringComparison.OrdinalIgnoreCase) >= 0 => RetroVfxPhase.Anticipation,
                RetroVfxLayerKind.Aura => RetroVfxPhase.Sustain,
                _ => RetroVfxPhase.Secondary
            };
        }

        private static Gradient RichGradient(RetroVfxLayer layer, RetroVfxRecipe recipe)
        {
            Color hot = layer.blendMode == RetroVfxBlendMode.Additive
                ? Color.Lerp(Color.white, layer.startColor, 0.28f)
                : layer.startColor;
            Color middle = Color.Lerp(layer.startColor, recipe.secondaryColor, 0.34f);
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(hot, 0f),
                    new GradientColorKey(layer.startColor, 0.16f),
                    new GradientColorKey(middle, 0.62f),
                    new GradientColorKey(layer.endColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(layer.startColor.a, 0f),
                    new GradientAlphaKey(Mathf.Max(layer.startColor.a, 0.78f), 0.12f),
                    new GradientAlphaKey(layer.endColor.a, 1f)
                });
            return gradient;
        }

        private static RetroVfxNoiseProfile NoiseFor(RetroVfxLayerKind kind)
        {
            return kind switch
            {
                RetroVfxLayerKind.Smoke => RetroVfxNoiseProfile.RollingSmoke,
                RetroVfxLayerKind.Burst => RetroVfxNoiseProfile.ChaoticFire,
                RetroVfxLayerKind.Beam => RetroVfxNoiseProfile.ElectricJitter,
                RetroVfxLayerKind.Trail => RetroVfxNoiseProfile.WindShear,
                _ => RetroVfxNoiseProfile.None
            };
        }

        private static float NoiseStrengthFor(RetroVfxLayerKind kind)
        {
            return kind switch
            {
                RetroVfxLayerKind.Smoke => 0.62f,
                RetroVfxLayerKind.Burst => 0.34f,
                RetroVfxLayerKind.Beam => 0.48f,
                RetroVfxLayerKind.Trail => 0.24f,
                _ => 0f
            };
        }

        private static RetroVfxRenderGeometry GeometryFor(RetroVfxLayerKind kind, RetroVfxSpriteStyle sprite)
        {
            if (kind == RetroVfxLayerKind.Arc || sprite == RetroVfxSpriteStyle.SlashArc || sprite == RetroVfxSpriteStyle.Crescent)
            {
                return RetroVfxRenderGeometry.Mesh;
            }
            if (kind == RetroVfxLayerKind.Trail || kind == RetroVfxLayerKind.Beam || kind == RetroVfxLayerKind.Sparks || sprite == RetroVfxSpriteStyle.BloodDrop)
            {
                return RetroVfxRenderGeometry.StretchedBillboard;
            }
            return RetroVfxRenderGeometry.Billboard;
        }

        private static AnimationCurve CurveFor(RetroVfxLayerKind kind)
        {
            return kind switch
            {
                RetroVfxLayerKind.Ring => new AnimationCurve(new Keyframe(0f, 0.08f), new Keyframe(0.16f, 0.85f), new Keyframe(1f, 1.8f)),
                RetroVfxLayerKind.Arc => new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.12f, 1f), new Keyframe(0.72f, 1.08f), new Keyframe(1f, 0f)),
                RetroVfxLayerKind.Splat => new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.08f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0.85f)),
                RetroVfxLayerKind.Smoke => new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.5f)),
                RetroVfxLayerKind.Flash => new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.08f, 1f), new Keyframe(1f, 0f)),
                RetroVfxLayerKind.Aura => new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.18f, 1f), new Keyframe(0.72f, 0.8f), new Keyframe(1f, 0f)),
                _ => new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f))
            };
        }

        private static Color ShiftHue(Color color, float hueDelta, float valueMultiplier)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            Color shifted = Color.HSVToRGB(Mathf.Repeat(hue + hueDelta, 1f), saturation, Mathf.Clamp01(value * valueMultiplier));
            shifted.a = color.a;
            return shifted;
        }

        private static Color Clear(Color color)
        {
            color.a = 0f;
            return color;
        }

        private static Color Ember() => new Color(1f, 0.38f, 0.035f);
        private static Color WarmWhite() => new Color(1f, 0.94f, 0.7f);

        private static float Range(System.Random random, float minimum, float maximum)
        {
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }
    }
}
