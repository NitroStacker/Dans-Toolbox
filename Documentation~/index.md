# Dans Toolbox

## Setup

The setup wizard opens over a blurred view of Unity on first install, after reinstalling the package, and once after every package-version update. It selects the shared color theme, enabled tools, and optional recommended workspace. Settings are saved per project. Reopen it from the Dans Toolbox toolbar icon or with **Tools > Dans Toolbox > Setup Wizard**.

The available themes are **Signal Orange**, **Neon Cyan**, and **Arcade Violet**. Disabling a tool prevents its menu command from opening and closes any existing instance. Applying **ToolBox** replaces the current Unity window arrangement without recreating the main Unity window, then removes panes for disabled tools.

## Retro SFX

Open **Tools > Dans Toolbox > Retro SFX**. Use the Synth and Import tabs to choose a source, apply effects, preview with the transport controls, and render a WAV asset into the project.

## Native Window Dock

Open **Tools > Dans Toolbox > Native Window Dock**. Choose an application from the thumbnail gallery and attach it. Create additional panels to place different applications in separate Unity dock regions. Use Frame to crop the visible portion with draggable borders.

Detach applications before closing Unity. The recovery guard also attempts to restore attached windows during script reloads.

### Native Window Dock compatibility

- Windows Editor only.
- Unity and the target application must run at the same elevation level.
- Some GPU-accelerated, sandboxed, or custom-compositor applications can refuse Win32 reparenting or render incorrectly.
- Unity shortcuts can reserve some key combinations while Unity is foreground.

## Better Hierarchy

Open **Tools > Dans Toolbox > Better Hierarchy**. The Tree surface mirrors loaded scenes and Prefab Mode with selection, renaming, reparenting, Undo, component inspectors, status actions, rule styling, and diagnostics. The Atlas surface visually browses scene branches, favorites, recent objects, and prefabs using cached previews.

Use the collection button to create either a **Virtual** collection, which leaves Transform parenting untouched, or a **Parent** collection, which creates a normal Transform parent and reparents the selection. Project rules and virtual collections are stored in `ProjectSettings/BetterHierarchySettings.asset`; personal view density, favorites, and recent history stay local to the Editor user.

Hover a collection member and press **−** to remove it. A virtual member keeps the same Transform parent; a Parent member moves one level out while preserving its world transform. Use **Remove Selection** from a virtual collection menu to remove several selected members at once.

Delete a collection from its hover action, context menu, or the Delete key. The confirmation popup offers **Keep Items** for virtual collections or **Move Out** for Parent collections, alongside **Delete All** and **Cancel**. Keeping virtual items removes only their membership; moving out Parent items places them at the collection's parent level while preserving world transforms.

Search supports fuzzy names and compact filters: `t:Camera`, `tag:Player`, `layer:UI`, `scene:Level`, `path:Gameplay`, `is:prefab`, `is:inactive`, `warn:any`, `favorite:true`, `collection:Gameplay`, and `ref:ObjectName`. Prefix a term with `-` to exclude it. Save and recall queries from the `#` menu.

Use an object's context menu to make it the default parent for new objects and placed prefabs. Prefab instances also expose apply, revert, unpack, and source actions. The `...` menu opens rules, view modes, safe batch tools, isolation controls, and Unity's stock Hierarchy when needed.

Better Hierarchy follows Unity's normal Hierarchy shortcuts: **Delete/Backspace** deletes, **F2/Return** renames, **Ctrl/Cmd+D** duplicates, **Ctrl/Cmd+C/X/V** copies, cuts, and pastes, **Ctrl/Cmd+A** selects all, **Ctrl/Cmd+F** focuses search, and **F** frames the selection. Unity's standard create-empty shortcuts also work. Press **Space** to switch Tree/Atlas.

## Package updates

Releases use the version in `package.json` and a matching Git tag. See the repository README for release and user-update instructions.
