using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DansToolbox.RetroVfx;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.RetroVfx
{
    internal enum RetroVfxSourceLicense
    {
        Cc0,
        Mit,
        UnityCompanion,
        UnityAssetStore,
        CommercialRestricted,
        ReferenceOnly
    }

    internal sealed class RetroVfxSourceDescriptor
    {
        internal RetroVfxSourceDescriptor(
            string id,
            string name,
            string purpose,
            string url,
            RetroVfxSourceLicense license,
            bool redistributable,
            params string[] markers)
        {
            Id = id;
            Name = name;
            Purpose = purpose;
            Url = url;
            License = license;
            Redistributable = redistributable;
            Markers = markers ?? Array.Empty<string>();
        }

        internal string Id { get; }
        internal string Name { get; }
        internal string Purpose { get; }
        internal string Url { get; }
        internal RetroVfxSourceLicense License { get; }
        internal bool Redistributable { get; }
        internal string[] Markers { get; }
        internal int DetectedAssetCount { get; set; }
        internal bool Installed => DetectedAssetCount > 0;
    }

    [InitializeOnLoad]
    internal static class RetroVfxSourceLibrary
    {
        private static readonly List<RetroVfxSourceDescriptor> descriptors = new List<RetroVfxSourceDescriptor>
        {
            new RetroVfxSourceDescriptor("brackeys", "Brackeys VFX Bundle", "General masks, particles, flipbooks, and sprite sheets", "https://brackeysgames.itch.io/brackeys-vfx-bundle", RetroVfxSourceLicense.Cc0, true, "brackeys", "vfx bundle"),
            new RetroVfxSourceDescriptor("codemanu", "CodeManu Free VFX", "Pixel explosions, smoke, blood, impacts, and timing references", "https://opengameart.org/content/free-vfx-asset-pack", RetroVfxSourceLicense.Cc0, true, "codemanu", "free vfx asset pack", "spritemancer"),
            new RetroVfxSourceDescriptor("ansimuz", "Ansimuz Explosions", "Old-school animated pixel explosions", "https://ansimuz.itch.io/explosion-animations-pack", RetroVfxSourceLicense.Cc0, true, "ansimuz", "explosion animations"),
            new RetroVfxSourceDescriptor("kenney-smoke", "Kenney Smoke Particles", "Reusable smoke and cloud masks", "https://www.kenney.nl/assets/smoke-particles", RetroVfxSourceLicense.Cc0, true, "kenney", "smoke particles"),
            new RetroVfxSourceDescriptor("oga-explosion", "OpenGameArt Explosion 7", "Long-form 50-frame explosion reference", "https://opengameart.org/content/explosion-7", RetroVfxSourceLicense.Cc0, true, "explosion 7", "blastfx"),
            new RetroVfxSourceDescriptor("pewas-rpg", "Pixel RPG VFX Pack", "RPG slashes, projectiles, auras, shields, heals, and impacts", "https://pewas.itch.io/pixel-rpg-vfx-pack-free-animated-effects", RetroVfxSourceLicense.CommercialRestricted, false, "pewas", "pixel rpg vfx"),
            new RetroVfxSourceDescriptor("pixogen-lite", "Pixogen RPG VFX Lite", "Compact hand-drawn 64px animation sheets", "https://pixogenassets.itch.io/pixel-art-rpg-vfx-lite", RetroVfxSourceLicense.CommercialRestricted, false, "pixogen", "rpg vfx lite"),
            new RetroVfxSourceDescriptor("frostwindz", "Frostwindz Slashes", "Pixel sword slash silhouettes and timing", "https://frostwindz.itch.io/pixel-art-slashes", RetroVfxSourceLicense.CommercialRestricted, false, "frostwindz", "pixel art slashes"),
            new RetroVfxSourceDescriptor("sidelka-blood", "Sidelka Blood FX", "Pixel splashes, slashes, impacts, and decals", "https://sidelka.itch.io/24-pixel-blood-fx-pack-splashes-slashes-impacts-64128px", RetroVfxSourceLicense.CommercialRestricted, false, "sidelka", "blood fx"),
            new RetroVfxSourceDescriptor("untied-blood", "unTied Super Pixel Blood", "Blood burst and splatter references", "https://untiedgames.itch.io/super-pixel-blood-fx-pack-1", RetroVfxSourceLicense.CommercialRestricted, false, "untied", "super pixel blood"),
            new RetroVfxSourceDescriptor("quick-effects", "Free Quick Effects Vol. 1", "Production prefab benchmark across common gameplay families", "https://marketplace.unity.com/packages/vfx/particles/free-quick-effects-vol-1-304424", RetroVfxSourceLicense.UnityAssetStore, false, "quick effects", "quickeffects"),
            new RetroVfxSourceDescriptor("cartoon-fx", "Cartoon FX Remaster Free", "Shader, distortion, dissolve, light, and camera-shake benchmark", "https://marketplace.unity.com/packages/vfx/particles/cartoon-fx-remaster-free-109565", RetroVfxSourceLicense.UnityAssetStore, false, "cartoon fx", "cfxr"),
            new RetroVfxSourceDescriptor("simple-fx", "Simple FX Cartoon", "Stylized blood, dust, fire, smoke, and splatter benchmark", "https://marketplace.unity.com/packages/vfx/particles/simple-fx-cartoon-particles-67834", RetroVfxSourceLicense.UnityAssetStore, false, "simple fx", "simplefx"),
            new RetroVfxSourceDescriptor("unity-particle-pack", "Unity Particle Pack", "First-party particle-system reference content", "https://assetstore.unity.com/packages/vfx/particles/particle-pack-127325", RetroVfxSourceLicense.UnityAssetStore, false, "particle pack", "unity particle"),
            new RetroVfxSourceDescriptor("ultimate-fx", "Ultimate FX Pack 1", "Large production Shuriken benchmark library", "https://marketplace.unity.com/packages/vfx/particles/ultimate-fx-pack-1-cartoon-4382", RetroVfxSourceLicense.UnityAssetStore, false, "ultimate fx", "ultimatefx"),
            new RetroVfxSourceDescriptor("texture-lab", "VFX Texture Lab", "Mask operations, ramps, posterize, dilate, erode, and channel packing", "https://github.com/PudinKiller/VFXTextureLab", RetroVfxSourceLicense.Mit, true, "vfxtexturelab", "vfx texture lab"),
            new RetroVfxSourceDescriptor("mesh-lab", "VFX Mesh Lab", "Arcs, rings, ribbons, discs, beams, and crescents", "https://github.com/PudinKiller/VFXMeshLab", RetroVfxSourceLicense.Mit, true, "vfxmeshlab", "vfx mesh lab"),
            new RetroVfxSourceDescriptor("nova", "Nova Shader", "URP particle uber-shader integration", "https://github.com/CyberAgentGameEntertainment/NovaShader", RetroVfxSourceLicense.Mit, true, "novashader", "nova shader", "jp.co.cyberagent.nova"),
            new RetroVfxSourceDescriptor("vfx-graph-samples", "Unity VFX Graph Samples", "Layered GPU effects and output-event reference content", "https://github.com/Unity-Technologies/VisualEffectGraph-Samples", RetroVfxSourceLicense.UnityCompanion, false, "vfx graph samples", "visualeffectgraph-samples"),
            new RetroVfxSourceDescriptor("vfx-toolbox", "Unity VFX Toolbox", "Image Sequencer, flipbook, point-cache, and vector-field workflow", "https://github.com/Unity-Technologies/VFXToolbox", RetroVfxSourceLicense.UnityCompanion, false, "vfxtoolbox", "vfx toolbox"),
            new RetroVfxSourceDescriptor("effekseer", "Effekseer", "Emitter-tree and event-chain authoring reference", "https://github.com/effekseer/EffekseerForUnity", RetroVfxSourceLicense.Mit, true, "effekseer"),
            new RetroVfxSourceDescriptor("ui-particle", "Particle Effect For UGUI", "Optional UI-particle output adapter", "https://github.com/mob-sakai/ParticleEffectForUGUI", RetroVfxSourceLicense.Mit, true, "particleeffectforugui", "particle effect for ui"),
            new RetroVfxSourceDescriptor("urp-shaders", "URP Shaders Collection", "Additional URP VFX shader references", "https://github.com/TinyPlay/URPShadersCollection", RetroVfxSourceLicense.Mit, true, "urpshaderscollection", "urp shaders collection"),
            new RetroVfxSourceDescriptor("ui-effect", "UIEffect", "Optional dissolve, transition, and blur UI adapter", "https://github.com/mob-sakai/UIEffect", RetroVfxSourceLicense.Mit, true, "uieffect", "com.coffee.ui-effect"),
            new RetroVfxSourceDescriptor("spritemancer", "SpriteMancer", "External sprite/flipbook authoring workflow", "https://spritemancer.com/", RetroVfxSourceLicense.ReferenceOnly, false, "spritemancer")
        };

        private static readonly List<string> texturePaths = new List<string>();
        private static readonly HashSet<string> texturePathLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> assetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool initialized;

        internal static IReadOnlyList<RetroVfxSourceDescriptor> Descriptors
        {
            get
            {
                EnsureFresh();
                return descriptors;
            }
        }

        internal static int InstalledCount
        {
            get
            {
                EnsureFresh();
                return descriptors.Count(item => item.Installed);
            }
        }

        internal static void Refresh()
        {
            texturePaths.Clear();
            texturePathLookup.Clear();
            assetPaths.Clear();
            foreach (RetroVfxSourceDescriptor descriptor in descriptors)
            {
                descriptor.DetectedAssetCount = 0;
            }

            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                AddAssetPath(path);
            }

            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D");
            foreach (string guid in textureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && texturePathLookup.Add(path))
                {
                    texturePaths.Add(path);
                }
            }
            texturePaths.Sort(StringComparer.OrdinalIgnoreCase);
            initialized = true;
        }

        internal static void ApplyAssetChanges(
            IReadOnlyList<string> imported,
            IReadOnlyList<string> deleted,
            IReadOnlyList<string> moved,
            IReadOnlyList<string> movedFrom)
        {
            if (!initialized)
            {
                return;
            }

            bool texturesChanged = false;
            if (deleted != null)
            {
                foreach (string path in deleted)
                {
                    texturesChanged |= RemoveAssetPath(path);
                }
            }
            if (movedFrom != null)
            {
                foreach (string path in movedFrom)
                {
                    texturesChanged |= RemoveAssetPath(path);
                }
            }
            if (imported != null)
            {
                foreach (string path in imported)
                {
                    texturesChanged |= AddImportedPath(path);
                }
            }
            if (moved != null)
            {
                foreach (string path in moved)
                {
                    texturesChanged |= AddImportedPath(path);
                }
            }

            if (texturesChanged)
            {
                texturePaths.Sort(StringComparer.OrdinalIgnoreCase);
            }
        }

        internal static bool TryResolveTexture(
            RetroVfxRecipe recipe,
            RetroVfxLayer layer,
            int seed,
            out Texture2D texture,
            out int columns,
            out int rows,
            out string sourceName)
        {
            texture = null;
            columns = 1;
            rows = 1;
            sourceName = string.Empty;

            if (layer.sourceTexture != null)
            {
                texture = layer.sourceTexture;
                columns = Mathf.Max(1, layer.sourceColumns);
                rows = Mathf.Max(1, layer.sourceRows);
                sourceName = string.IsNullOrEmpty(layer.sourcePackId) ? "Assigned asset" : layer.sourcePackId;
                return true;
            }
            if (layer.sourceMode != RetroVfxSourceMode.SourceLibrary)
            {
                return false;
            }

            EnsureFresh();
            string[] keywords = Keywords(recipe.family, layer);
            List<string> candidates = texturePaths.Where(path =>
            {
                string normalized = path.ToLowerInvariant();
                string immediateCategory = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
                string searchableName = (Path.GetFileNameWithoutExtension(path) + " " + immediateCategory).ToLowerInvariant();
                bool packMatches = string.IsNullOrEmpty(layer.sourcePackId) ||
                                   descriptors.FirstOrDefault(item => item.Id == layer.sourcePackId)?.Markers.Any(marker => normalized.Contains(marker.ToLowerInvariant())) == true;
                return packMatches && keywords.Any(searchableName.Contains);
            }).ToList();

            if (candidates.Count == 0)
            {
                return false;
            }

            int choice = Mathf.Abs(seed) % candidates.Count;
            string selectedPath = candidates[choice];
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(selectedPath);
            if (texture == null)
            {
                return false;
            }
            columns = Mathf.Max(1, layer.sourceColumns);
            rows = Mathf.Max(1, layer.sourceRows);
            Match grid = Regex.Match(selectedPath, @"__(\d+)x(\d+)_", RegexOptions.IgnoreCase);
            if (grid.Success && int.TryParse(grid.Groups[1].Value, out int detectedColumns) &&
                int.TryParse(grid.Groups[2].Value, out int detectedRows))
            {
                columns = Mathf.Clamp(detectedColumns, 1, 32);
                rows = Mathf.Clamp(detectedRows, 1, 32);
            }
            sourceName = descriptors.FirstOrDefault(item => item.Markers.Any(marker => selectedPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0))?.Name ?? selectedPath;
            return true;
        }

        internal static string LicenseLabel(RetroVfxSourceDescriptor descriptor)
        {
            string boundary = descriptor.Redistributable ? "EMBEDDABLE" : "USE IN PLACE";
            return descriptor.License.ToString().ToUpperInvariant() + "  •  " + boundary;
        }

        private static void EnsureFresh()
        {
            if (!initialized)
            {
                Refresh();
            }
        }

        private static bool AddImportedPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            AddAssetPath(path);
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type != null && typeof(Texture2D).IsAssignableFrom(type) && texturePathLookup.Add(path))
            {
                texturePaths.Add(path);
                return true;
            }
            return false;
        }

        private static void AddAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !assetPaths.Add(path))
            {
                return;
            }
            ApplyDescriptorDelta(path, 1);
        }

        private static bool RemoveAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            if (assetPaths.Remove(path))
            {
                ApplyDescriptorDelta(path, -1);
            }
            if (!texturePathLookup.Remove(path))
            {
                return false;
            }
            texturePaths.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        private static void ApplyDescriptorDelta(string path, int delta)
        {
            string searchable = path.ToLowerInvariant();
            foreach (RetroVfxSourceDescriptor descriptor in descriptors)
            {
                int occurrences = descriptor.Markers.Sum(marker =>
                    CountOccurrences(searchable, marker.ToLowerInvariant()));
                descriptor.DetectedAssetCount = Mathf.Max(0, descriptor.DetectedAssetCount + occurrences * delta);
            }
        }

        private static int CountOccurrences(string source, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        private static string[] Keywords(RetroVfxEffectFamily family, RetroVfxLayer layer)
        {
            List<string> keywords = new List<string>
            {
                layer.spriteStyle.ToString().ToLowerInvariant(),
                layer.kind.ToString().ToLowerInvariant(),
                family.ToString().ToLowerInvariant()
            };
            if (family == RetroVfxEffectFamily.MuzzleFlash)
            {
                keywords.AddRange(new[] { "muzzle", "gun", "shot" });
            }
            if (family == RetroVfxEffectFamily.Explosion)
            {
                keywords.AddRange(new[] { "explosion", "blast", "kaboom", "ditheredfire" });
            }
            if (family == RetroVfxEffectFamily.Impact)
            {
                keywords.AddRange(new[] { "impact", "hit", "flash" });
            }
            if (family == RetroVfxEffectFamily.Blood)
            {
                keywords.AddRange(new[] { "blood", "splat", "gore" });
            }
            if (family == RetroVfxEffectFamily.Smoke)
            {
                keywords.AddRange(new[] { "smoke", "puff", "cloud", "dust" });
            }
            if (family == RetroVfxEffectFamily.Magic || family == RetroVfxEffectFamily.EnergyBurst)
            {
                keywords.AddRange(new[] { "charged", "electric", "shield", "magic", "fire" });
            }
            if (family == RetroVfxEffectFamily.SwordSwing)
            {
                keywords.AddRange(new[] { "slash", "sword", "swing", "arc" });
            }
            if (family == RetroVfxEffectFamily.ItemShine || family == RetroVfxEffectFamily.Pickup)
            {
                keywords.AddRange(new[] { "glint", "shine", "star", "sparkle", "pickup" });
            }
            return keywords.Where(item => !string.IsNullOrWhiteSpace(item) && item != "auto").Distinct().ToArray();
        }
    }

    internal sealed class RetroVfxSourceAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            RetroVfxSourceLibrary.ApplyAssetChanges(
                importedAssets,
                deletedAssets,
                movedAssets,
                movedFromAssetPaths);
        }
    }
}
