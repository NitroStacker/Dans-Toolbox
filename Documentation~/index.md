# Dans Toolbox

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

## Package updates

Releases use the version in `package.json` and a matching Git tag. See the repository README for release and user-update instructions.
