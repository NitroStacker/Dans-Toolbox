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

Native `.mat` and `.asset` files render through their real Unity custom editors. Imported textures, models, audio, scripts, and other source files render through Unity's importer inspectors, including platform overrides and their normal Apply/Revert workflows. A fixed preview host below the scrolling properties preserves each editor's native preview settings, interaction, and information without interfering with the rest of the Editor surface.

Better Inspector adds an Odin-inspired **Actions** group for zero-argument methods marked with Unity's `ContextMenu` attribute. Its collapsible **References** group provides a compact, editable view of live serialized object links while the complete native editor remains available above it.

Use the header circle to lock the current targets. Selection history remains available from the back/forward buttons or **Alt+Left/Right** when unlocked. Star component cards to create a focused favorites view, and collapse cards individually or together when working with component-heavy objects.

The toolbar search matches both component names and serialized field names. A component-name match keeps its complete custom editor. A field-only match switches that card to a focused property view, which is useful for finding a single value on a large component. Press **Ctrl/Cmd+F** to focus search and **Escape** to clear it.

Multi-selection creates one multi-object editor for every component type and duplicate ordinal shared by all selected GameObjects. **+ Component** opens a searchable component palette and applies the chosen component to each compatible selected object with Undo.

The Add Component palette mirrors Unity's registered category structure, including nested package and script groups. Category rows use representative native icons, and every component keeps its own Unity icon while browsing or searching. Browse from the category index or type immediately to search every category at once; **Escape** clears search, moves up one category, then closes the palette.

Each card menu exposes favorite, copy/paste values, reorder, remove, and Unity's native component context menu. The `!` view scans selected GameObjects for missing scripts and broken serialized object references. Issues can be pinged, and missing scripts can be removed as one Undoable operation.

Right-click blank space below the component cards to open Unity's native Inspector menu. A **Better Inspector** submenu adds expand/collapse, favorites, diagnostics, target locking, refresh, search cleanup, and access to the stock Inspector without hiding Unity or package-added commands.

Right-click a component card to open that component's complete Unity context menu with an additional **Better Inspector** submenu. Native property-field menus retain priority, while unclaimed card space provides favorite, collapse, isolate, diagnostics, and refresh actions.

## Better Project

Open **Tools > Dans Toolbox > Better Project**. Browse assets in compact list, visual grid, sortable details, or split-pane views. The ToolBox workspace places Better Project in the stock Project dock and keeps **Unity Project Window** available from the `...` menu.

Use **Browse** for folders, history, pinned locations, previews, sub-assets, drag/drop, and familiar file operations. Use **Library** for favorites, recents, saved searches, smart or manual collections, exact-content duplicates, oversized assets, issues, and unused candidates. Use **Impact** to navigate dependencies and reverse references, assess likely build use, collect or export a dependency set, replace serialized references, or inspect deletion impact.

Rules stored in `ProjectSettings/BetterProjectSettings.asset` color and badge assets by path, name, type, extension, label, package, folder, diagnostic, or exact asset. Personal favorites and navigation history are stored under `UserSettings`. Search supports fuzzy text and `t:`, `ext:`, `path:`, `l:`, `size:`, `modified:`, `ref:`, and `is:` filters.

Better Project requests previews only for visible assets and builds its reverse-reference index incrementally. Closed scenes are reported by Impact but intentionally excluded from automatic reference replacement; open and review those scene references explicitly.

## Better Console

Open **Tools > Dans Toolbox > Better Console**. The ToolBox workspace places it where Unity's Console normally lives; the native Console remains available from the `...` menu.

**Live** is a virtualized stream with severity filters, optional timestamps, follow, pause, Error Pause, and signature-based Collapse. **Issues** groups changing instances of the same problem, shows hit rate and session spread, and persists New, Seen, Acknowledged, Muted, or Resolved state with bookmarks and notes. **Sessions** records Editor, compile, Play Mode, build, test-like, and remote activity and compares each selected session with the previous session of its kind.

Search supports plain text, quoted phrases, exclusions prefixed with `-`, and optional `/regex/`. Structured fields include `sev:`, `type:` or `cat:`, `source:`, `device:`, `file:`, `scene:`, `session:`, `channel:`, `tag:`, `has:stack`, `has:file`, `has:context`, `has:properties`, `is:remote`, `is:structured`, `is:bookmarked`, and triage states such as `is:muted`. Use `before:` or `after:` with a local date/time.

The detail pane opens source frames, pings object context, shows structured properties and stack frames, stores issue notes, copies full entries, and creates an evidence-bounded fix prompt only when **FIX** is pressed. Visible results or an individual session can be exported as JSON or Markdown.

Diagnostics are linked across the toolbox. Better Project assets and Better Hierarchy objects show compact clickable `W/E` badges, while Better Inspector offers a selection-aware console action. Their context menus can open a precise multi-target Better Console view. Better Console's `@` action filters to the current Unity selection, and a source asset can be revealed back in Better Project without replacing source-line opening in the external code editor.

Better Console always captures Unity's public threaded log callback. On supported Unity versions, an isolated reflection bridge also imports native Console history such as compiler and importer messages; if Unity changes that internal API, callback capture continues. History is bounded and cached below `Library/DansToolbox/BetterConsole`, while shared saved views, mute rules, triage, bookmarks, and notes live in `ProjectSettings/BetterConsoleSettings.asset`.

Runtime code can opt into channels and properties without depending on UnityEditor:

```csharp
DansToolbox.BetterConsole.Warning(
    "Server retry",
    "NET",
    gameObject,
    DansToolbox.BetterConsole.Property("attempt", retryCount),
    DansToolbox.BetterConsole.Tag("retry"));
```

The same call still reaches `Debug.unityLogger`, so player logs and Unity's native Console retain the message.

## Better Scene

Open **Tools > Dans Toolbox > Better Scene** beside Unity's native Scene view. Its four modes keep spatial authoring compact: Select handles focus, isolation, alignment, distribution, snapping, grounding, scatter, replacement, visibility, and saved views; Place previews and creates prefabs, models, sprites, meshes, or audio from Better Project; Measure captures surface-aware distance and delta; Review shows bounds, pivots, missing references, prefab changes, inactive objects, and related Better Console signals.

Visibility bands preserve the pre-filter hidden and picking state, along with visible and locked layer masks. Scene-camera bookmarks retain scene path, pivot, rotation, size, projection, and 2D mode. Alt+1 through Alt+4 switch modes, F frames the selection, and Escape leaves an active spatial workflow.

## Package updates

Releases use the version in `package.json` and a matching Git tag. See the repository README for release and user-update instructions.
