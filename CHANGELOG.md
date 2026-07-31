# Changelog

All notable changes to Dans Toolbox are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow [Semantic Versioning](https://semver.org/).

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
