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

## Better Inspector

Open **Tools > Dans Toolbox > Better Inspector**. Better Inspector follows the live Unity selection and renders the same native or third-party custom editors inside compact, themed component cards. The ToolBox workspace places it in the former Inspector dock beside Retro SFX and Native Window Dock.

Use the header circle to lock the current targets. Selection history remains available from the back/forward buttons or **Alt+Left/Right** when unlocked. Star component cards to create a focused favorites view, and collapse cards individually or together when working with component-heavy objects.

The toolbar search matches both component names and serialized field names. A component-name match keeps its complete custom editor. A field-only match switches that card to a focused property view, which is useful for finding a single value on a large component. Press **Ctrl/Cmd+F** to focus search and **Escape** to clear it.

Multi-selection creates one multi-object editor for every component type and duplicate ordinal shared by all selected GameObjects. **+ Component** opens a searchable component palette and applies the chosen component to each compatible selected object with Undo.

The Add Component palette mirrors Unity's registered category structure, including nested package and script groups. Category rows use representative native icons, and every component keeps its own Unity icon while browsing or searching. Browse from the category index or type immediately to search every category at once; **Escape** clears search, moves up one category, then closes the palette.

Each card menu exposes favorite, copy/paste values, reorder, remove, and Unity's native component context menu. The `!` view scans selected GameObjects for missing scripts and broken serialized object references. Issues can be pinged, and missing scripts can be removed as one Undoable operation.

Right-click blank space below the component cards to open Unity's native Inspector menu. A **Better Inspector** submenu adds expand/collapse, favorites, diagnostics, target locking, refresh, search cleanup, and access to the stock Inspector without hiding Unity or package-added commands.

Right-click a component card to open that component's complete Unity context menu with an additional **Better Inspector** submenu. Native property-field menus retain priority, while unclaimed card space provides favorite, collapse, isolate, diagnostics, and refresh actions.

## Package updates

Releases use the version in `package.json` and a matching Git tag. See the repository README for release and user-update instructions.
