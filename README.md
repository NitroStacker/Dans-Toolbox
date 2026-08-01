# Dans Toolbox

Dans Toolbox is a Unity 6 Editor package containing four focused tools:

- **Retro SFX** — create, preview, import, process, and render retro sound effects.
- **Native Window Dock** — place interactive Windows application windows inside resizable Unity tabs, including multi-panel layouts and crop framing.
- **Better Hierarchy** — replace Unity's hierarchy with rule styling, collections, diagnostics, visual search, batch actions, and the thumbnail-based Object Atlas.
- **Better Inspector** — inspect scene objects and assets with searchable component cards, pinned targets, favorites, multi-editing, and diagnostics.

All tools are Editor-only. Native Window Dock is available only in the Windows Editor.

## First-install setup

The setup wizard opens as a focused, blurred overlay after the package is installed. It lets each project choose:

- Signal Orange, Neon Cyan, or Arcade Violet color themes.
- Which Dans Toolbox tools are enabled.
- Whether to apply the packaged **ToolBox** workspace.

The choices are stored in `ProjectSettings/DansToolboxSettings.asset` so they can be shared with the project. Reopen the wizard at any time from the Dans Toolbox icon in Unity's main toolbar or from **Tools > Dans Toolbox > Setup Wizard**.

## Install from Git

After this repository is pushed to a Git host, open **Window > Package Manager**, select **+ > Add package from git URL**, and enter a version-tagged URL:

```text
https://github.com/NitroStacker/Dans-Toolbox.git#v1.5.0
```

For local development, use:

```text
file:R:/Dans Toolbox
```

The package can also be added directly to a project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.dans.toolbox": "https://github.com/NitroStacker/Dans-Toolbox.git#v1.5.0"
  }
}
```

## Open the tools

- **Dans Toolbox icon in the Unity main toolbar**
- **Tools > Dans Toolbox > Setup Wizard**
- **Tools > Dans Toolbox > Retro SFX**
- **Tools > Dans Toolbox > Native Window Dock**
- **Tools > Dans Toolbox > Better Hierarchy**
- **Tools > Dans Toolbox > Better Inspector**

## Better Hierarchy essentials

- **Tree / Atlas** switches between the structural hierarchy and visual scene or prefab cards. Press Space to switch.
- Search accepts names plus compact filters such as `t:Camera`, `tag:Player`, `layer:UI`, `is:prefab`, `is:hidden`, `warn:any`, and `collection:Gameplay`.
- **Virtual collections** organize objects without changing Transforms. **Parent collections** create a normal parent so moving it moves every member.
- Hover a collection member and press **−** to remove it. Virtual members keep their Transform untouched; Parent members move one level out while preserving their world transform. Use **Remove Selection** on a virtual collection menu for batches.
- Delete a collection from its hover action, context menu, or the Delete key. The confirmation popup can keep/move out its items, delete every item, or cancel.
- Hover rows for active, Scene visibility, picking lock, and favorite controls. Component icons open compact inspectors.
- Save useful queries from the `#` menu, and set any scene object as the default parent from its context menu.
- Rules, view modes, batch tools, isolation, and the stock Unity Hierarchy fallback live under the `...` menu.
- Prefab instance menus expose ping, apply, revert, and unpack actions with confirmation for destructive changes.
- Unity's normal Hierarchy keyboard workflow is preserved, including Delete/Backspace, F2/Return, duplicate, copy/cut/paste, select all, search, frame, Undo/Redo, and create-empty shortcuts.

## Better Inspector essentials

- Select GameObjects, components, or assets; Better Inspector follows the live selection while preserving Unity's native and third-party custom editors.
- Pin the current target with the header lock, then inspect other selections without losing it. Use the back/forward buttons or Alt+Left/Right to revisit selection history.
- Search by component name or serialized field name. A component-name match keeps the full custom editor; a field-only match draws a focused serialized-property view.
- Star frequently used component cards and use the toolbar star to show favorites only. Collapse individual cards or all cards for dense objects.
- Multi-select GameObjects to edit every component type and duplicate ordinal shared by the selection.
- Use **+ Component** for a searchable, multi-object Add Component palette.
- Open a card's `...` menu to copy/paste values, move, remove, or open Unity's complete component context menu.
- Open `!` diagnostics to find missing scripts and broken object references, ping their owners, and remove missing script slots with Undo.
- Press Ctrl/Cmd+F to focus search, Escape to clear it, and Alt+Left/Right to move through history.

## Updating users

Use semantic versions and matching Git tags. For each release:

1. Change `version` in `package.json`.
2. Add the release notes to `CHANGELOG.md`.
3. Commit the release and create a tag such as `v1.1.0`.
4. Push both the commit and tag.

Users update by changing the version at the end of their Git URL, for example from `#v1.4.0` to `#v1.5.0`. A scoped registry such as OpenUPM can later provide version discovery and an Update button without changing this package layout.

## Migrating an existing project

Remove the older standalone Retro SFX scripts and `com.battlesoccer.native-window-dock` package before installing Dans Toolbox. Keeping both copies installed will create duplicate menu commands.

See [Documentation~/index.md](Documentation~/index.md) for usage and compatibility notes.
