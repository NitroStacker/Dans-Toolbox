# Changelog

All notable changes to Dans Toolbox are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow [Semantic Versioning](https://semver.org/).

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
- Optional packaged ToolBox Layout with safe Unity 6 layout loading.

### Changed

- Retro SFX and Native Window Dock now consume a shared theme and configuration core.

## [1.0.0] - 2026-07-31

### Added

- Retro SFX generator with synthesis, audio import, effects, preview, and WAV rendering.
- Native Window Dock with interactive Win32 embedding, multi-panel layouts, window thumbnails, and draggable crop framing.
- Editor-only assembly boundaries and Native Window Dock EditMode tests.
