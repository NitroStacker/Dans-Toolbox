# Retro VFX

Open **Tools > Dans Toolbox > Retro VFX**. The tool is a nondestructive effect workstation: pick a production archetype, click it again for a new seeded variation, refine the recipe, then export an editable Particle System prefab, a transparent flipbook, or both.

## Workflow

1. Choose one of forty archetypes in **Library**. The eleven families cover impacts, old-school bomb explosions, gunfire, blood, sword swings, magic, energy, pickups, item shines, smoke, and environmental effects.
2. Use **Shape** for art direction, duration, scale, intensity, direction, palette, and a rendered Retro SFX clip.
3. Use **Layers** to author anticipation, primary, secondary, sustain, and decay phases. Every layer has a source, geometry, emitter, motion, blend mode, timing, color-over-life, size curve, turbulence, real Particle System trails, surface animation, collision, and optional sub-emitter event.
4. Use **Sources** to route compatible layers through embedded or installed packs. Click **Rescan** after importing a package. A blank Preferred Pack lets the router choose the best detected match for each layer.
5. Use **Advanced** for the production shader, dissolve/noise and flow maps, distortion, custom materials, third-party prefab layers, VFX Graph, flipbook import, animated lighting, and scene-response hooks.

The live stage plays, pauses, restarts, scrubs, and zooms without adding objects to the current scene. Layer locks survive global rerolls. Rotary controls support vertical drag, Shift for fine adjustment, mouse-wheel adjustment, and double-click reset.

## Source model

Retro VFX uses three source tiers:

- **Procedural** generates silhouettes and animated sheets deterministically. It is always available and supplies crisp pixel explosions, splats, slash arcs, flashes, glints, rings, smoke, runes, debris, and environmental particles.
- **Embedded CC0** routes through the curated CodeManu and Kenney libraries included with the package. These assets are covered in `THIRD_PARTY_NOTICES.md`.
- **Use in place** detects optional repositories, Asset Store packages, and commercial packs already present in the consuming project. Retro VFX references those assets but never copies their restricted raw files into Dans Toolbox.

If a preferred source is absent or has no compatible asset, the layer falls back to the procedural generator. This makes saved recipes portable while still benefiting from richer project-local libraries.

## Rendering and integrations

The bundled `Dans Toolbox/Retro VFX/Uber` particle shader provides alpha, additive, premultiplied, and multiply blending; HDR emission; edge glow; dissolve; flow-map warping; and soft-particle depth fading. A material can be replaced globally or per layer. Nova Shader and other installed materials can therefore be used without a compile-time dependency.

Mesh layers generate real arc, ring, ribbon, or quad geometry. Texture sheets use Unity Texture Sheet Animation with inferred or explicit grids. Trails use the Particle System Trails module rather than stretched billboard approximations. Child layers can spawn on birth, death, collision, or trigger through Particle System sub-emitters.

VFX Graph is capability-detected. When installed, an assigned Visual Effect Asset is layered beside the generated Particle Systems and retained by prefab export. Effekseer, Asset Store effects, or repository prefabs can be layered through **Third-party Effect Layer**. These bridges intentionally avoid compile-time package dependencies.

`RetroVfxPlayer` exposes framework-neutral camera-shake, hit-stop, and decal events. A game can subscribe its own camera, time, or decal system without Dans Toolbox depending on Cinemachine or a specific gameplay framework.

## Export and portability

- **Save Recipe** writes a reusable `RetroVfxRecipe` asset.
- **Prefab** writes an editable effect hierarchy with generated materials and meshes.
- **Flipbook** bakes a transparent PNG, material, and ready-to-use flipbook prefab.
- **Both** produces the prefab and flipbook outputs together.

Source-library textures remain references to their original assets. Keep optional packages installed when exported prefabs use them. Embedded and generated textures are included by the Dans Toolbox package. URP or HDRP projects should assign a pipeline-compatible distortion material because the bundled GrabPass distortion implementation targets the Built-in render pipeline.

## Included and supported sources

The complete, current catalog is visible inside the **Sources** tab. It includes the embedded CodeManu and Kenney collections plus discovery entries for Brackeys, Ansimuz, OpenGameArt packs, Pixel RPG/Pixogen/Frostwindz/Sidelka/unTied packs, common Unity Asset Store effect packs, VFXTextureLab, VFXMeshLab, Nova Shader, VFX Graph samples, VFXToolbox, Keijiro testbeds, Effekseer, ParticleEffectForUGUI, UIEffect, URP shader collections, and SpriteMancer workflows.

See [third-party notices](../THIRD_PARTY_NOTICES.md) for redistribution and provenance details.

