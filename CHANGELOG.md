# Changelog

All notable changes to Dans Toolbox are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow [Semantic Versioning](https://semver.org/).

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
