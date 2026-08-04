# Changelog

All notable changes to Dans Toolbox are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.23.0] - 2026-08-04

### Added

- Add centered 50%–200% Native Window Dock zoom controls and preserve zoom with application profiles and framing presets.

### Fixed

- Match the Native Window Dock preset-name field to the Toolbox's flat, square visual language in every interaction state.

## [1.22.2] - 2026-08-04

### Fixed

- Restore full Better Inspector prefab parity by drawing the prefab root and each attached component through Unity's native editor stack instead of treating the prefab as one empty `GameObject` inspector.
- Preserve native component enable toggles, help, presets, menus, custom inspector sections, referenced materials, and previews while retaining Better Inspector favorites, search, diagnostics, references, and context actions.
- Add a direct **Open Prefab** action and reset the content scroll position when changing or navigating targets.

## [1.22.1] - 2026-08-04

### Fixed

- Inspect prefab assets through their selected `GameObject` targets instead of constructing Unity's internal `PrefabImporterEditor`, preventing prefab selections from producing `NullReferenceException` errors.
- Purge destroyed objects from Better Inspector's cached and locked selections before repainting, preventing transient `MissingReferenceException` errors after object lifecycle changes.

## [1.22.0] - 2026-08-04

### Changed

- Make Retro VFX source discovery incremental, cache preview-stage components, advance particle previews without replaying from zero, and avoid repeated runtime hierarchy and reflection scans.
- Update Artboard previews by changed pixel region, reuse compositing and upload buffers, cache document integrity, and render checkerboard and workspace patterns with tiled textures.
- Batch Better Console capture notifications, reconcile native history with indexed linear matching, and make native-line duplicate checks constant time.
- Cache Better Inspector targets, editors, reference paths, diagnostics, and constant-repaint requirements without changing native inspector presentation or editing behavior.
- Limit Better Scene spatial queries to relevant pointer events, preserve placement previews across unrelated project changes, and reuse overlay actions and themed GUI styles.
- Cache Better Hierarchy diagnostics, rule matches, representative component types, previews, and themed Atlas labels while keeping asset and scene invalidation targeted.
- Reduce repeated window discovery, native dock positioning, hover repaint, and transient GUI allocation work across the Toolbox Hub, Native Window Dock, Better Project utility windows, and Retro SFX.

### Fixed

- Keep Retro SFX previews on Unity's audible asset-backed path while reusing the imported preview clip until its generated audio actually changes.
- Prevent idle Better Console capture, Better Scene overlays, and hover effects from producing unnecessary per-frame allocations or repaint work.
- Preserve Artboard visual output while painting by recompositing only the affected region and retaining full refreshes for structural or whole-frame changes.

## [1.21.0] - 2026-08-04

### Added

- Add complete Better Project Undo/Redo for asset rename, move, cut/paste, duplicate, folder creation, imports, prefab creation, deletion, labels, and importer presets while preserving asset GUIDs.
- Stage undoable deletions and created-asset redo data under `Library/DansToolbox/BetterProjectUndo`, keeping temporary recovery data out of the project asset database and version control.

### Changed

- Update Better Project incrementally for ordinary imported, deleted, and moved assets while preserving unaffected cached metadata and reserving full rebuilds for large or manual refreshes.
- Reduce Better Project UI work by caching asset objects, styles, thumbnails, pinned folders, search metadata requirements, and expanded grid entries.

### Fixed

- Keep reference-index progress from invalidating search results and rebuilding the folder tree after every small batch.
- Bound Better Project hover work to 60 Hz, cache selected-asset Addressables/build/importer metadata, and throttle asynchronous preview repaint checks.
- Keep duplicate-name diagnostics within project or package scope so package test fixtures cannot flag unrelated project assets.
- Reserve red issue styling for broken assets, scripts, shaders, and importers, while giving informational diagnostics named amber badges and explanatory tooltips.

## [1.20.2] - 2026-08-04

### Fixed

- Restore audible Retro SFX editor previews by using the proven asset-backed WAV path while excluding only its transient preview asset from Better Project indexing.
- Refresh the Better Project index from path-aware asset import notifications, preserving normal asset updates without rebuilding the entire index for Retro SFX preview creation and cleanup.

## [1.20.1] - 2026-08-04

### Fixed

- Preview Retro SFX through an in-memory `AudioClip`, avoiding temporary project-asset imports and the Better Project index refreshes they triggered on every audition.

## [1.20.0] - 2026-08-04

### Added

- Add Artboard, a native layered drawing and cel-animation workspace for digital art, pixel art, onion-skin animation, mirrored painting, and crisp high-resolution sprite production.
- Export individual Sprite PNGs or automatically sliced sprite sheets at 1×–16× using nearest-neighbor expansion, Point filtering, uncompressed texture import, and optional generated `AnimationClip` assets.
- Show a themed, pixel-aligned Pencil, Brush, and Eraser footprint on the artboard with a live size readout that stays visible at distant zoom levels.

## [1.19.1] - 2026-08-04

### Fixed

- Keep the Native Window Dock host above Unity's rendered client surface and issue a one-time presentation redraw, preventing attached Chromium windows from appearing as a blank gray viewport.

## [1.19.0] - 2026-08-04

### Added

- Check the `main` package manifest for new Dans Toolbox releases, pulse the Unity main-toolbar Toolbox icon orange when an update is available, and provide a one-click Git package update from the Toolbox Hub.
- Add a manual **Tools > Dans Toolbox > Check for Updates** command, cached background checks, release notes access, and clear update progress or failure feedback.

## [1.18.2] - 2026-08-04

### Added

- Add application-specific Native Window Dock framing presets with save, apply, rename/update, and delete controls.

### Fixed

- Prevent intermittent Native Window Dock flashes by removing repaint-timeout visibility pulses and redundant native reposition/redraw calls.
- Keep attached applications connected by retrying transient container failures, repairing changed native parents and styles, and reconnecting the previous window after Unity script reloads.

## [1.18.1] - 2026-08-04

### Fixed

- Prevent Better Scene's Place-to-Erase transition from corrupting Unity's IMGUI/D3D12 render frame and leaving the Scene view blank. GPU-backed object picking now runs only for pointer input instead of recursively during Scene layout and repaint events.
- Defer Erase state changes and floating-overlay synchronization until the active IMGUI pass has completed, and balance Scene GUI scopes during exceptional exits.
- Avoid filled placement-ghost injection through SRP Scene cameras, while retaining spatial wire feedback in URP projects.
- Compile the Retro VFX Uber shader correctly under URP by calculating eye depth from the shader's actual vertex input.

## [1.18.0] - 2026-08-03

### Added

- Bundle the intended Toolbox workspace as a Unity layout and apply it when the Setup Wizard's Toolbox Layout option is selected, while retaining a Keep Current Layout choice.
- Add expandable rendered sub-asset previews for compound models and sprites in Better Project list and grid views, including direct selection, multi-selection, opening, and dragging.

### Changed

- Redesign the Setup Wizard tool picker as responsive Tool Hub-style cards grouped into Workspace, Create, and Integrate categories.
- Make every Setup Wizard tab directly navigable, keep Apply Setup available from every tab, and replace the ambiguous selection glyph with a crisp checkmark.
- Document `#main` as the recommended Git installation revision so Package Manager users can update the package from the branch.

### Fixed

- Restore Unity Project-window parity for compound assets in Better Project: models and sprites keep their imported identity, use a high-contrast disclosure control, and are no longer treated as prefabs.
- Treat imported model roots as assets in Better Inspector so selecting an FBX uses its native importer surface instead of scene-object controls.

## [1.17.0] - 2026-08-03

### Added

- Add collapsible hierarchy browsing to Better Hierarchy's Atlas so multi-part scene objects appear as one root card until their children are expanded.
- Add native component icons for cameras, lights, and other non-renderable scene objects in the Atlas while preserving combined previews for renderable model roots.

### Changed

- Make Better Hierarchy Tree visibility and picking-lock actions cascade from a parent to its descendants while keeping every child independently overridable afterward.

## [1.16.0] - 2026-08-03

### Added

- Add native-style Shift range selection, Ctrl/Cmd selection toggling, and Ctrl/Cmd+C multi-entry copy to Better Console.

### Changed

- Standardize Toolbox search bars on Better Project's visual and interaction model, including a clear action and click-away focus release.

### Fixed

- Reconcile Better Console with Unity's current Console snapshot after domain reloads, recompiles, and native clears so stale errors cannot survive after Unity's Console is empty.

## [1.15.0] - 2026-08-03

### Added

- Add render-accurate ghost previews to Better Scene placement without creating temporary scene objects.
- Add bounds-centered smart snapping while Shift is held, including dynamically repeated footprint-sized slots across larger surfaces.
- Add an exact-asset erase painter that removes only instances matching the current Place asset while the mouse is held and groups each stroke into one Undo action.
- Add an Account for Zoom directional-view option and preserve exact Scene view zoom in saved views.
- Add Better Project support for dragging Hierarchy objects into asset panes to create connected prefabs with unique paths.

### Changed

- Ground Surface-mode placements by their rendered bounds so prefab transform offsets do not lift objects above the contacted surface.
- Persist Better Scene placement assets by GUID and local file ID, including built-in and sub-assets, and show clear feedback for unsupported transient selections.
- Make Better Scene mega-panels dynamically resize and reclamp as saved views and other expandable content change.
- Treat external Better Project drops as copy imports with unique destination paths while preserving Move semantics for project-internal assets.

### Fixed

- Prevent the Better Scene Place asset field from immediately clearing supported mesh, sprite, audio, prefab, and model selections.
- Prevent Better Project from sending absolute external paths to `AssetDatabase.MoveAsset`.
- Restore default Project-window parity for external file imports and Hierarchy-to-prefab drops.
- Eliminate small Shift-snap placement offsets caused by root pivots and near-integral mesh bounds.
- Keep placement preview and final placement transforms identical across surface contact and smart snapping.

## [1.14.0] - 2026-08-03

### Added

- Add a searchable Toolbox Hub with job-based groups, favorites, recents, enabled/open status, keyboard launch, and direct access from Unity's main toolbar.
- Add automatic launch placement with per-tool defaults and explicit floating recovery positions.
- Add exact Native Dock panel targeting through a numbered live-region overlay.
- Add a guarded clean-workspace action that closes only Dans Toolbox windows and safely detaches Native Dock applications.
- Add a default-on Seamless Tool Surfaces appearance option that unifies Toolbox content surfaces and softens internal dividers without modifying native tabs or dock geometry.
- Add real Unity dock targeting: Retro SFX and Retro VFX join the Inspector dock automatically, while Native Dock uses a numbered full-Editor region picker.

### Changed

- Replace the eager eight-tab ToolBox layout with an organized launcher workflow that preserves the user's existing Unity layout.
- Turn the setup workspace choice into a Hub-first preference that preserves currently open windows and dock regions.
- Route in-tool Toolbox actions to the Hub while keeping the Setup Wizard focused on project-wide configuration.
- Redesign the Hub as a responsive thumbnail card grid with category filters, native tool imagery, hover explanations and actions, a neutral initial search state, and fully scrollable content at short window heights.
- Replace the Hub's decorative tool motifs with cached native Unity icons that communicate each tool's purpose at a glance.
- Replace long Toolbox dock-tab names with compact native icons and full-name tooltips; Native Dock uses Unity's standalone-computer icon with its panel number retained in the tooltip.

### Fixed

- Remove Unity 6.3 deprecation warnings by using the typed hierarchy tree APIs and Entity ID object lookup while preserving Unity 6.0 compatibility.
- Remove redundant READY footers from the core workspace tools and reserve status chrome for actionable progress, guidance, and failures.
- Make Hub card hover immediate by repainting only on hover-state changes, caching the visible tool order and icons, and eliminating unchanged periodic repaints.
- Keep compact dock tabs genuinely narrow and give every tool an isolated native icon and tooltip identity instead of sharing Unity's empty-title cache entry.
- Decouple seamless appearance from workspace cleanup so applying it can never collapse dock regions or expand the center view.

## [1.13.0] - 2026-08-03

### Added

- Add a native Unity Scene overlay with collapsible Create, Transform, Place, View, Visibility, Measure, and Review mega-panels.
- Add icon and thumbnail-driven creation and recent-placement palettes, six orthographic view presets, Scene-camera creation, grouping, transform resets, and active-object mirroring.

### Changed

- Replace the Better Scene dockable button wall with a selection-aware companion containing a compact tool navigator and collapsible active-tool, saved-view, and Scene-health sections.
- Move spatial commands into contextual Scene overlays, keep unavailable actions visibly disabled, and block overlay clicks from leaking into Scene placement or measurement.
- Let Unity dock, float, move, collapse, orient, and persist the Better Scene toolbar and active mega-panel; toolbar groups can be hidden and tool buttons can be reordered or removed from their context menus.
- Open each mega-panel beside the toolbar's current horizontal, vertical, docked, or floating position instead of restoring an unrelated corner.

### Fixed

- Fully clean up measurement and placement state when switching Better Scene panels, selecting a native Unity tool, collapsing the overlay, entering Play Mode, or reloading assemblies.
- Prevent measurement guides and placement previews from remaining active after their owning tool exits.
- Keep Better Scene mega-panels open beside the toolbar that launched them instead of flashing at a generic position and immediately closing.
- Replace corrupted Better Inspector reference-header glyphs with encoding-safe Unicode characters.
- Ignore malformed diagnostic paths and map local-package source paths safely so Better Console cannot interrupt Better Inspector rendering.
- Stop Better Hierarchy context menus from accumulating orphaned native Hierarchy windows and clean up invalid windows left by earlier versions so Unity layouts save normally.

## [1.12.0] - 2026-08-03

### Added

- Turn Retro VFX into a source-aware production workstation with anticipation/primary/secondary/sustain/decay phases, source and geometry modes, gradients, turbulence, drag, real trails, surface animation, collision, and sub-emitter events.
- Add a flat, hover-aware Sources tab with detection and license boundaries for twenty-five researched sprite packs, repositories, shader libraries, authoring tools, Asset Store effects, and runtime integrations.
- Embed curated CC0 CodeManu flipbooks and the Kenney Smoke Particles library with local provenance, automatic grid inference, and procedural fallback.
- Add a portable Retro VFX Uber shader, generated arc/ring/ribbon meshes, third-party prefab layers, scene-response events, and expanded production tests.
- Expand Retro VFX to forty procedural patches across eleven families, including pixel-art bomb explosions, gunfire, blood, sword swings, magic, item shines, pickups, smoke, impacts, and environmental effects.
- Add nineteen purpose-built procedural sprite silhouettes, animated point-filtered pixel explosion and smoke sheets, directional and stationary motion modes, repeated bursts, per-axis aspect, emission offsets, jitter, stretch, and additive or alpha rendering.
- Add deterministic preset rerolls: every preset click generates a fresh variation of that archetype while the existing regenerate action continues to preserve locked layers.
- Add a compact Retro SFX-style Shape rack with draggable, scrollable, resettable knobs and focused Library, Shape, Layers, and Advanced tabs.

### Changed

- Replace long layer slider stacks with compact Retro SFX-style rotary control banks while keeping explicit source, curve, gradient, and event fields.
- Preserve point filtering when generated pixel textures are exported so baked Particle System prefabs retain crisp retro edges.
- Replace broad generic particle recipes with family-specific geometry, timing, motion, palette, and layering.

## [1.11.0] - 2026-08-02

### Added

- Add **Retro VFX** as an eighth selectable tool with the shared Retro SFX rack, transport, theme, motion, and status language adapted to a radial live preview stage.
- Add twelve deterministic procedural patches across Impact, Explosion, Muzzle Flash, Smoke, Energy Burst, and Pickup families with high-level shaping, palette changes, per-layer editing, layer locking, and seeded variation generation.
- Add nondestructive `RetroVfxRecipe` assets and the player-safe `RetroVfxPlayer` component with particle, audio, and animated-light playback.
- Add editable Particle System prefab export plus transparent flipbook baking that produces a PNG, material, and ready-to-use flipbook prefab.
- Add the Advanced surface for Built-in-pipeline distortion, custom material or shader overrides, capability-detected VFX Graph attachment, tiled flipbook importing, and animated point, spot, or directional lights.
- Add setup-catalog, disabled-window cleanup, recommended-layout, package metadata, documentation, and EditMode coverage for Retro VFX.

## [1.10.0] - 2026-08-02

### Added

- Add Better Scene with Select, Place, Measure, and Review workflows plus a compact Scene-view overlay.
- Add selection history, framing, isolation, hide/picking controls, active-object alignment, distribution, transform snapping, grounding, scatter, and Undoable prefab replacement.
- Add prefab/model, sprite, mesh, and audio placement from Better Project with free, grid, surface, and vertex snapping, normal alignment, parenting, repeat placement, and live previews.
- Add surface-aware measurement, saved Scene-camera bookmarks, camera jumps, reversible category visibility bands, and saved visible/locked layer presets.
- Add bounds, pivot, missing-script, missing-reference, prefab-override, inactive-object, and Better Console diagnostic signals in both Better Scene and the Scene view.
- Add Better Scene to the Toolbox setup catalog, recommended layout, shared themes, shortcuts, documentation, and EditMode coverage.

## [1.9.0] - 2026-08-02

### Added

- Add a revision-cached diagnostic bridge that relates Better Console entries to scene objects, components, assets, and source paths without scanning history during every row repaint.
- Add clickable warning and error badges to Better Hierarchy and Better Project, plus selection-aware Better Console actions in Better Inspector.
- Add two-way navigation: filter Better Console from Project, Hierarchy, or Inspector; filter it to the current Unity selection; and reveal source assets back in Better Project.
- Add `context:`, `ctxid:`, and multi-target query support for precise cross-tool diagnostic views.

### Changed

- Refresh Project, Hierarchy, and Inspector diagnostic signals when Better Console capture or issue state changes.

## [1.8.0] - 2026-08-01

### Added

- Add **Better Console** as a sixth selectable tool and replace the stock Console tab in the recommended ToolBox layout.
- Add responsive Live, Issues, and Sessions surfaces using the flat Retro SFX visual language with virtualized dense rows and adaptive side-or-bottom details.
- Add advanced field queries, phrases, exclusions, optional regex, saved views, normalized issue grouping, spam rate analysis, triage states, bookmarks, notes, and mute rules.
- Add persistent bounded history and automatic Editor, compile, Play Mode, build, and remote-aware session lanes with previous-session comparisons.
- Add clickable structured stacks, first-source navigation, object context, JSON/Markdown export, native Console parity controls, and an explicit evidence-bounded fix-prompt action.
- Add a player-safe structured logging API with channels and key/value properties while retaining normal Unity logger output.

### Changed

- Extend setup, disabled-tool cleanup, documentation, configuration tests, package metadata, and the recommended layout for Better Console.
- Isolate optional native Console history reflection behind a durable public callback capture path.

## [1.7.1] - 2026-08-01

### Added

- Add Odin-inspired responsive action groups for zero-argument methods marked with Unity's `ContextMenu` attribute.
- Add collapsible serialized-reference summaries and a persistent native preview panel with preview settings and asset information.

### Fixed

- Render native assets such as Materials through their real Unity custom editors even when Unity stores their Inspector expansion state as collapsed.
- Render imported assets through their importer inspectors, preserving texture/model/audio/script import settings, Apply/Revert workflows, multi-editing, and custom importer UI.
- Isolate low-level native asset previews outside the scroll view so texture and render previews cannot corrupt the surrounding Editor surface.

## [1.7.0] - 2026-08-01

### Added

- Add **Better Project** as a fifth selectable tool and replace the stock Project tab in the recommended ToolBox layout.
- Add Browse, Library, and Impact surfaces with folder history, breadcrumbs, pinned locations, list/grid/details views, split panes, previews, sub-assets, and selection synchronization.
- Add project-shared rule colors, icons, badges, saved searches, smart collections, and manual collections without moving assets.
- Add fuzzy structured search, lazy asset diagnostics, exact-content duplicate discovery, build-use hints, optional Addressables and version-control metadata, and incremental reverse-reference indexing.
- Add dependency mapping, reference replacement with a dry-run scan, dependency collection/export, safe deletion impact, two-asset comparison, and rich type/import metadata.
- Add batch rename previews, labels, moves, importer presets, drag/drop, native keyboard parity, and the stock Unity Project fallback.

### Changed

- Extend the shared setup catalog, disabled-tool cleanup, reveal animation, documentation, tests, and package metadata for Better Project.
- Bump the package development version to `1.7.0`.

## [1.6.0] - 2026-07-31

### Added

- Add **Better Inspector** as a fourth selectable Dans Toolbox tool and replace the stock Inspector in the recommended ToolBox layout.
- Add target locking, selection history, component favorites, collapse controls, and responsive component cards.
- Add component-and-property search that preserves full native and third-party custom editors until a focused field query is active.
- Add multi-object component editing for components shared by every selected GameObject.
- Add a searchable Add Component palette with common components surfaced before a query is entered.
- Organize the Better Inspector Add Component palette by Unity's registered component categories, with nested navigation and global search.
- Preserve native component icons throughout category browsing and use representative component icons in the category index.
- Add missing-script and broken-reference diagnostics with ping and Undoable missing-script repair.
- Add component copy, paste, reorder, remove, and native context-menu access from every card.

### Changed

- Track known setup tools so newly introduced default tools opt in for existing projects without re-enabling tools users previously disabled.
- Align the Better Inspector Add Component action with the Layer control's header column.
- Extend Unity's native empty-Inspector context menu with Better Inspector display, locking, refresh, search, and stock-Inspector actions.
- Extend Unity's native component context menu from component-card right-clicks with favorite, collapse, isolate, diagnostics, and refresh actions.
- Bump the package development version to `1.6.0`.

## [1.5.0] - 2026-07-31

### Added

- Add **Better Hierarchy** as a third selectable Dans Toolbox tool and replace the stock Hierarchy in the recommended layout when enabled.
- Add rule-driven row colors, icons, badges, headers, tree guides, component mini-inspectors, hover actions, favorites, selection history, scene navigation, Prefab Mode support, and visual query filters.
- Add the cached-thumbnail **Object Atlas** for scene objects, branches, favorites, recents, and drag-or-double-click prefab placement.
- Add both virtual collections and Undoable Transform-parent collections.
- Add incremental scene diagnostics for missing scripts and references, broken prefabs, transform hazards, deep trees, inactive parents, duplicate listeners, EventSystems and Main Cameras, and organizer candidates.
- Add batch rename, state, sorting, prefab replacement, and component-copy workflows.
- Add Clean, Production, Debug, Art, Level Design, and Custom view modes.
- Add saved searches, default parenting, prefab override badges, and safe apply, revert, and unpack actions.

### Changed

- Restore Unity's complete native Hierarchy context menu for object and blank-row clicks, including editing, selection, prefab, creation, Properties, and package-added commands.
- Deselect on empty-space clicks and add density-safe centered row content with Retro SFX hover states.
- Pin the Better Hierarchy working canvas to `#1B1C1D`.
- Match native Hierarchy keyboard editing for delete, rename, duplicate, copy, cut, paste, select all, frame, search, and empty-object creation shortcuts.
- Remove virtual or Parent collection members directly from their row, with batch removal from virtual collection menus and world-transform preservation for Parent members.
- Include Better Hierarchy in the shared staggered reveal when the wizard applies the workspace.
- Rename the recommended workspace to **ToolBox**.
- Keep virtual collections collapsed when scene or Inspector selection sync reveals an object that also belongs to a collection.
- Confirm collection deletion with separate keep-items and delete-all paths for both virtual and Parent collections, including Undo and world-transform preservation.
- Delete empty virtual and Parent collections immediately without showing a confirmation popup.
- Place Better Hierarchy's object, collection, and scene-specific actions first, above Unity's live package-aware menus, so the additional controls are immediately visible.
- Stabilize expansion with persistent row identities, non-expanding selection synchronization, and temporary search expansion that restores the user's previous tree state.

## [1.4.0] - 2026-07-31

### Added

- Finish setup with a full-overlay **Toolbox installed!** splash using the real toolbar icon, spring motion, and a unified blur-and-content fade.

### Changed

- Slow tool-tab reveals into a staggered multi-band curtain with a restrained signal scan.
- Extend the recommended-layout handoff with arcing pane movement, staggered placement, and a short settling pulse before completion.

## [1.3.1] - 2026-07-31

### Changed

- Open the setup wizard after first install, reinstalling the package, and every Dans Toolbox package-version update, even when setup was completed for the same or an earlier version.
- Remember completion or dismissal per version so ordinary domain reloads and Editor restarts do not repeatedly reopen setup.

## [1.3.0] - 2026-07-31

### Added

- Present setup as a centered overlay over a softly blurred snapshot of the Unity Editor.
- Keep a dark fallback backdrop on platforms where native Editor capture is unavailable.

### Changed

- Removed decorative setup particles while retaining the focused splash and structural step transitions.
- Keep the setup cover visible through the recommended-layout handoff while Unity settles.

### Fixed

- Preserve Unity's existing native main window during layout loading so applying ToolBox no longer briefly minimizes and maximizes the Editor.

## [1.2.1] - 2026-07-31

### Fixed

- Automatically reveal the registered Dans Toolbox button when Unity serializes a newly installed toolbar element as hidden.
- Added **Tools > Dans Toolbox > Show Toolbar Icon** as a manual recovery action.
- Deferred Native Dock theme access until `OnEnable` so restored tabs do not touch project settings from an EditorWindow constructor.

## [1.2.0] - 2026-07-31

### Changed

- Reworked setup into a clean three-step Theme, Tools, and Layout flow.
- Reduced setup copy and replaced the dense single page with focused card choices and fixed navigation.
- Added keyboard navigation with Left, Right, Enter, and Escape.
- Added a short splash, step transitions, text reveals, signal VFX, an animated layout handoff, and tool-window reveal wipes.

### Fixed

- Restored Native Dock's transient thumbnail state after Unity deserialization to prevent repeated exceptions when saved tabs return after a domain reload.

## [1.1.3] - 2026-07-31

### Fixed

- Assigned independent Unity asset GUIDs to the package so it can coexist with shared Retro Song Maker support scripts retained by an existing project.
- Added regression coverage for duplicate package GUIDs and collisions with assets in the consuming project.

## [1.1.2] - 2026-07-31

### Added

- Added a native Unity 6 main-toolbar button using the Dans Toolbox icon. Clicking it opens the Setup Wizard.

## [1.1.1] - 2026-07-31

### Changed

- Made the permanent manual entry explicit at **Tools > Dans Toolbox > Setup Wizard**.

## [1.1.0] - 2026-07-31

### Added

- First-install setup wizard with project-level settings.
- Signal Orange, Neon Cyan, and Arcade Violet color themes shared by both tools.
- Per-tool enablement designed to scale as more Dans Toolbox tools are added.
- Optional packaged ToolBox workspace with safe Unity 6 layout loading.

### Changed

- Retro SFX and Native Window Dock now consume a shared theme and configuration core.

## [1.0.0] - 2026-07-31

### Added

- Retro SFX generator with synthesis, audio import, effects, preview, and WAV rendering.
- Native Window Dock with interactive Win32 embedding, multi-panel layouts, window thumbnails, and draggable crop framing.
- Editor-only assembly boundaries and Native Window Dock EditMode tests.
