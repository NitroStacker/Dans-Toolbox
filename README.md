# Dans Toolbox

Dans Toolbox is a Unity 6 package containing eight focused tools:

- **Retro SFX** — create, preview, import, process, and render retro sound effects.
- **Retro VFX** — forge deterministic particle effects, import flipbooks, attach advanced rendering, and export editable prefabs or baked sprite sheets.
- **Native Window Dock** — place interactive Windows application windows inside independently positioned panels, including multi-panel layouts and crop framing.
- **Better Hierarchy** — replace Unity's hierarchy with rule styling, collections, diagnostics, visual search, batch actions, and the thumbnail-based Object Atlas.
- **Better Inspector** — inspect scene objects and assets with searchable component cards, pinned targets, favorites, multi-editing, and diagnostics.
- **Better Project** — browse assets with visual rules, smart collections, rich previews, health diagnostics, batch actions, and dependency impact tracing.
- **Better Console** — capture, search, group, triage, compare, and export Editor or player logs without losing Unity Console compatibility.
- **Better Scene** — create, place, transform, measure, review, and bookmark content directly inside the Scene view.

All visual tools are Editor-only. Retro VFX includes a small player-safe recipe/player API, and Better Console includes an optional player-safe structured logging API. Native Window Dock is available only in the Windows Editor.

## First-install setup

The setup wizard opens as a focused, blurred overlay after the package is installed. It lets each project choose:

- Signal Orange, Neon Cyan, or Arcade Violet color themes.
- Which Dans Toolbox tools are enabled.
- Whether to apply the packaged Toolbox layout captured from the intended Unity workspace or keep the project's current layout.
- Whether Toolbox windows use visually seamless shared surfaces and softer internal dividers. Native tabs and dock geometry remain unchanged.

The choices are stored in `ProjectSettings/DansToolboxSettings.asset` so they can be shared with the project. The toolbar icon now opens the **Toolbox Hub** for everyday use; project themes and enabled tools remain available from **Tools > Dans Toolbox > Setup Wizard**.

## Install from Git

Open **Window > Package Manager**, select **+ > Add package from git URL**, and enter:

```text
https://github.com/NitroStacker/Dans-Toolbox.git#main
```

This tracks the repository's `main` branch. When a newer commit is available, select **Dans Toolbox** in Package Manager and click **Update**. Because this package lives at the repository root, the URL does not need a `?path=/...` segment.

For a project that must stay on an exact release, use a version-tagged URL instead:

```text
https://github.com/NitroStacker/Dans-Toolbox.git#v1.19.0
```

Tagged installs are intentionally pinned and do not advance when a newer tag is published.

For local development, use:

```text
file:R:/Dans Toolbox
```

The package can also be added directly to a project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.dans.toolbox": "https://github.com/NitroStacker/Dans-Toolbox.git#main"
  }
}
```

## Open the tools

- **Dans Toolbox icon in the Unity main toolbar**
- **Tools > Dans Toolbox > Toolbox Hub**
- **Tools > Dans Toolbox > Setup Wizard**
- **Tools > Dans Toolbox > Retro SFX**
- **Tools > Dans Toolbox > Retro VFX**
- **Tools > Dans Toolbox > Native Window Dock**
- **Tools > Dans Toolbox > Better Hierarchy**
- **Tools > Dans Toolbox > Better Inspector**
- **Tools > Dans Toolbox > Better Project**
- **Tools > Dans Toolbox > Better Console**
- **Tools > Dans Toolbox > Better Scene**

The Toolbox Hub presents tools as a responsive visual gallery with direct All Tools, Workspace, Create, and Integrate filters. It searches names and jobs, remembers favorites and recent tools, and reports which tools are already open. Click a tile to use its recommended dock or position. Retro SFX and Retro VFX automatically join the Inspector-side dock. Native Window Dock opens a numbered full-Editor picker so each new panel can be attached to a specific Unity pane; floating positions remain available from the tile menu. **Close Tool Windows** closes only Dans Toolbox windows and leaves the user's Unity layout untouched.

## Native Window Dock essentials

- Turn on **Frame** and drag the glowing viewport borders to crop the attached application.
- Enter a preset name and choose **Save** to keep the current frame for that application. Select a preset to apply it; change its name or frame and choose **Update** to edit it, or choose **Delete** to remove it.
- Framing presets are stored per application window class, so each attached tool can keep its own reusable views.
- Attached windows remain connected across ordinary Unity script reloads. The dock also repairs applications that try to reset their native parent or window style.

## Retro VFX essentials

- **Library** contains forty procedural patches across eleven families: Impact, Explosion, Gunfire, Blood, Swords, Magic, Energy, Pickup, Item Shine, Smoke, and World. Click any patch—including the active patch—to generate a fresh deterministic variation of that archetype.
- **Shape** uses compact Retro SFX-style knobs for duration, scale, intensity, and direction, plus palette and attached Retro SFX audio controls.
- **Layers** edits purpose-built pixel explosions, smoke, chunks, sparks, rings, slash arcs, blood splats and drops, muzzle flashes, glints, runes, leaves, bubbles, beams, and imported flipbooks. Its compact knob banks cover phase timing, emission, motion, turbulence, real Particle System trails, shader surfaces, collision, and sub-emitter events. Lock any layer before using the global regenerate control.
- **Sources** discovers and routes layers through the embedded CC0 CodeManu and Kenney libraries plus compatible packs already installed in the project. Asset Store and commercial sources remain use-in-place references and are never copied into the package.
- **Advanced** adds the production dissolve/flow/emission shader, Built-in-pipeline distortion, custom materials, third-party prefab layers, capability-detected VFX Graph assets, imported tiled flipbooks, animated effect lights, and camera-shake/hit-stop/decal events.
- The live stage plays, pauses, restarts, scrubs, and zooms without adding preview objects to the current scene.
- Presets vary their geometry, timing, count, motion, palette, and layering while retaining their identity; animated pixel sheets remain point-filtered through export.
- Save a nondestructive `RetroVfxRecipe`, export an editable Particle System prefab, bake a transparent flipbook PNG with its material and prefab, or generate both.
- Generated prefabs use the player-safe `RetroVfxPlayer` component. URP and HDRP distortion requires a pipeline-compatible material assigned in Advanced.

See [Documentation~/retro-vfx.md](Documentation~/retro-vfx.md) for the full workflow and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for embedded-source provenance and optional integration boundaries.

## Better Hierarchy essentials

- **Tree / Atlas** switches between the structural hierarchy and visual scene or prefab cards. Atlas groups multi-part scene objects under collapsible root cards and uses native component icons for non-renderable objects. Press Space to switch.
- Search accepts names plus compact filters such as `t:Camera`, `tag:Player`, `layer:UI`, `is:prefab`, `is:hidden`, `warn:any`, and `collection:Gameplay`.
- **Virtual collections** organize objects without changing Transforms. **Parent collections** create a normal parent so moving it moves every member.
- Hover a collection member and press **−** to remove it. Virtual members keep their Transform untouched; Parent members move one level out while preserving their world transform. Use **Remove Selection** on a virtual collection menu for batches.
- Delete a collection from its hover action, context menu, or the Delete key. The confirmation popup can keep/move out its items, delete every item, or cancel.
- Hover rows for active, Scene visibility, picking lock, and favorite controls. Parent visibility and picking changes cascade to descendants, and children remain independently overridable. Component icons open compact inspectors.
- Save useful queries from the `#` menu, and set any scene object as the default parent from its context menu.
- Rules, view modes, batch tools, isolation, and the stock Unity Hierarchy fallback live under the `...` menu.
- Prefab instance menus expose ping, apply, revert, and unpack actions with confirmation for destructive changes.
- Unity's normal Hierarchy keyboard workflow is preserved, including Delete/Backspace, F2/Return, duplicate, copy/cut/paste, select all, search, frame, Undo/Redo, and create-empty shortcuts.

## Better Inspector essentials

- Select GameObjects, components, or assets; Better Inspector follows the live selection while preserving Unity's native and third-party custom editors.
- Native assets use their complete custom editors, while imported assets use Unity's real importer inspectors with their normal Apply/Revert controls and platform settings.
- Use the fixed preview panel for Unity's native interactive previews and preview settings; collapse it when you want more editing space.
- Pin the current target with the header lock, then inspect other selections without losing it. Use the back/forward buttons or Alt+Left/Right to revisit selection history.
- Search by component name or serialized field name. A component-name match keeps the full custom editor; a field-only match draws a focused serialized-property view.
- Star frequently used component cards and use the toolbar star to show favorites only. Collapse individual cards or all cards for dense objects.
- Multi-select GameObjects to edit every component type and duplicate ordinal shared by the selection.
- Use **+ Component** for a searchable, multi-object Add Component palette.
- Open a card's `...` menu to copy/paste values, move, remove, or open Unity's complete component context menu.
- Expand **References** for a compact view of the object's live serialized links. Methods marked with Unity's `ContextMenu` attribute appear as responsive **Actions** buttons.
- Open `!` diagnostics to find missing scripts and broken object references, ping their owners, and remove missing script slots with Undo.
- Press Ctrl/Cmd+F to focus search, Escape to clear it, and Alt+Left/Right to move through history.

## Better Project essentials

- **Browse / Library / Impact** switches between folders, virtual workspaces, and asset relationships.
- Browse in list, grid, details, or split-pane views with breadcrumbs, back/forward history, pinned folders, previews, sub-assets, and native drag/drop.
- Color and badge assets manually or through project-shared rules for path, type, extension, label, package, folder, and diagnostic matches.
- Search accepts fuzzy terms plus `t:`, `ext:`, `path:`, `l:`, `size:`, `modified:`, `ref:`, and `is:folder`, `is:package`, `is:favorite`, `is:problem`, or `is:unused` filters. Prefix a term with `-` to exclude it.
- Smart collections preserve saved queries; manual collections group assets without moving them on disk.
- Library health views find broken imports, missing scripts or shaders, oversized assets, empty folders, unused candidates, and exact duplicate file content.
- Impact maps direct dependencies and indexed reverse references, estimates build use, collects or exports dependencies, previews safe deletion impact, and can replace serialized references with a dry-run scan.
- Batch actions rename with preview, apply labels, move assets, and apply compatible importer presets.
- Native Project shortcuts are preserved, including F2, Delete, Enter, Backspace, Ctrl/Cmd+A/C/X/V/D/F, and Alt+Left/Right. The stock Project window remains available from `...`.

## Better Console essentials

- **Live / Issues / Sessions** separates the current stream, normalized recurring problems, and compile/play/build/remote timelines.
- Search accepts text, quoted phrases, optional `/regex/`, exclusions such as `-source:Remote`, and filters including `sev:`, `type:`, `source:`, `file:`, `scene:`, `session:`, `channel:`, `tag:`, `has:stack`, and `is:bookmarked`.
- Repeated messages group by a stable signature that removes changing numbers, GUIDs, and addresses. Collapse applies the same grouping to Live.
- Issue state, bookmarks, notes, saved views, and mute rules persist in `ProjectSettings/BetterConsoleSettings.asset`. Bounded session history is cached under `Library/DansToolbox/BetterConsole`.
- The detail pane exposes the first source frame, clickable stack frames, object context, structured properties, copy/export, and an explicit **FIX** prompt action.
- Better Project and Better Hierarchy show clickable `W/E` log badges, Better Inspector can open logs for its current targets, and Better Console can filter the Unity selection or reveal a source asset back in Better Project.
- Click selects one Live entry, Shift-click selects a range, and Ctrl/Cmd-click toggles individual entries. Ctrl/Cmd+C copies every selected entry in visible order; Ctrl/Cmd+F focuses search, Ctrl/Cmd+L clears both Better Console and Unity's Console, Enter opens source, arrows move selection, and Escape clears the query. Native stack-trace settings, Error Pause, Editor/Player logs, and the stock Console remain available from `...`.
- Player code may use `DansToolbox.BetterConsole.Log`, `.Warning`, `.Error`, `.Exception`, `.Property`, and `.Tag` for channels and structured values. These calls still write to Unity's normal logger.

## Better Scene

Open it from `Tools > Dans Toolbox > Better Scene`. A native Unity Scene overlay opens one focused mega-panel beside the toolbar's current position. Drag its handle to dock, float, or reposition it; use Unity's overlay menu to collapse it or switch between horizontal, vertical, and compact-panel layouts. Right-click a tool to move or hide it, and use `...` to restore tools or hide the history and quick-action groups:

- **Create** offers common objects in a visual palette, including groups, primitives, cameras, lights, and audio sources.
- **Transform** groups active-object alignment, distribution, grounding, grouping, mirroring, snapping, and reset actions by working axis.
- **Place** accepts prefabs, models, sprites, meshes, and audio clips, with recent-asset thumbnails, live preview, and free, grid, surface, or vertex snapping.
- **View** supplies perspective and orthographic presets, framing, saved views, camera jumps, and camera creation from the Scene view.
- **Visibility** keeps selection isolation and picking controls beside reversible category filters and layer presets.
- **Measure** provides two-click surface-aware distance and delta measurement with strict cleanup when another tool is chosen.
- **Review** keeps selection diagnostics, related Better Console logs, Inspector and prefab navigation, bounds, pivots, and badges together.

The active mega-panel is also a native movable and collapsible Scene overlay, while the dockable Better Scene tab remains a compact companion: selection context, the seven-category navigator, and collapsible active-tool, saved-view, and Scene-health sections. Alt+1 through Alt+4 open Transform, Place, Measure, and Review. F frames selection and Escape clears or collapses the active Scene tool. Visibility filters preserve the previous hidden, picking, visible-layer, and locked-layer state so Restore returns to the exact working context.

## Updating users

Use semantic versions and matching Git tags. For each release:

1. Change `version` in `package.json`.
2. Add the release notes to `CHANGELOG.md`.
3. Commit the release and create a tag such as `v1.1.0`.
4. Push both the commit and tag.

Dans Toolbox checks the `main` package manifest in the background and fades its Unity main-toolbar icon orange when a newer release is available. Open the Toolbox Hub and choose **Update Now** to update through Unity Package Manager. `#main` installs keep tracking `main`; version-tagged installs advance to the newest matching release tag. Local and embedded development copies show the release but remain manually managed.

You can also run **Tools > Dans Toolbox > Check for Updates** at any time, or update the package directly from Unity Package Manager.

## Migrating an existing project

Remove the older standalone Retro SFX scripts and `com.battlesoccer.native-window-dock` package before installing Dans Toolbox. Keeping both copies installed will create duplicate menu commands.

See [Documentation~/index.md](Documentation~/index.md) for usage and compatibility notes.
