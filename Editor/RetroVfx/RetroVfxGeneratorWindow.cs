using System;
using System.Collections.Generic;
using System.IO;
using DansToolbox.Editor;
using DansToolbox.RetroVfx;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DansToolbox.EditorTools.RetroVfx
{
    internal sealed class RetroVfxGeneratorWindow : EditorWindow
    {
        private enum ToolTab
        {
            Library = 0,
            Layers = 1,
            Advanced = 2,
            Shape = 3,
            Sources = 4
        }

        [SerializeField] private ToolTab activeTab;
        [SerializeField] private RetroVfxEffectFamily libraryFamily = RetroVfxEffectFamily.Impact;
        [SerializeField] private RetroVfxRecipe workingRecipe;
        [SerializeField] private string workingRecipeJson = string.Empty;
        [SerializeField] private RetroVfxRecipe sourceRecipe;
        [SerializeField] private string selectedPresetId = "sharp-impact";
        [SerializeField] private string outputFolder = RetroVfxExportService.DefaultOutputFolder;
        [SerializeField] private string exportName = "Sharp Impact";
        [SerializeField] private RetroVfxOutputMode outputMode = RetroVfxOutputMode.Both;
        [SerializeField] private int flipbookFrameSize = 256;
        [SerializeField] private int flipbookColumns = 4;
        [SerializeField] private int flipbookRows = 4;
        [SerializeField] private bool workingDirty;
        [SerializeField] private Vector2 forgeScroll;
        [SerializeField] private Vector2 shapeScroll;
        [SerializeField] private Vector2 layersScroll;
        [SerializeField] private Vector2 advancedScroll;
        [SerializeField] private Vector2 sourcesScroll;
        [SerializeField] private string sourceSearch = string.Empty;
        [SerializeField] private string status = "Choose a preset, shape it, then save a recipe or export production assets.";
        [SerializeField] private MessageType statusType = MessageType.None;

        [NonSerialized] private RetroVfxPreviewStage preview;
        [NonSerialized] private readonly HashSet<int> expandedLayers = new HashSet<int>();
        [NonSerialized] private double revealStartedAt;

        [MenuItem("Tools/Dans Toolbox/Retro VFX")]
        internal static void Open()
        {
            RetroVfxGeneratorWindow window = GetWindow<RetroVfxGeneratorWindow>();
            window.titleContent = new GUIContent("Retro VFX");
            window.minSize = new Vector2(760f, 720f);
            window.Show();
        }

        [MenuItem("Tools/Dans Toolbox/Retro VFX", true)]
        private static bool ValidateOpen()
        {
            return DansToolboxSettings.IsToolEnabled(DansToolboxTools.RetroVfxId);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Retro VFX");
            minSize = new Vector2(760f, 720f);
            preview ??= new RetroVfxPreviewStage();
            if (workingRecipe == null)
            {
                workingRecipe = ScriptableObject.CreateInstance<RetroVfxRecipe>();
                workingRecipe.hideFlags = HideFlags.HideAndDontSave;
                if (!string.IsNullOrEmpty(workingRecipeJson))
                {
                    EditorJsonUtility.FromJsonOverwrite(workingRecipeJson, workingRecipe);
                    workingRecipe.Normalize();
                }
                else
                {
                    RetroVfxPresetFactory.Apply(selectedPresetId, workingRecipe);
                }
                exportName = workingRecipe.displayName;
            }
            else
            {
                workingRecipe.hideFlags = HideFlags.HideAndDontSave;
                workingRecipe.Normalize();
            }
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            revealStartedAt = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            preview?.Dispose();
            preview = null;
            if (workingRecipe != null && !AssetDatabase.Contains(workingRecipe))
            {
                workingRecipeJson = EditorJsonUtility.ToJson(workingRecipe);
                DestroyImmediate(workingRecipe);
            }
            workingRecipe = null;
        }

        private void Update()
        {
            if (preview != null && preview.Tick(workingRecipe))
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), RetroVfxGui.Canvas);
            if (!DansToolboxSettings.IsToolEnabled(DansToolboxTools.RetroVfxId))
            {
                DrawDisabled();
                return;
            }

            HandleShortcuts();
            GUILayout.BeginArea(new Rect(10f, 10f, position.width - 20f, position.height - 20f));
            DrawHeader();
            GUILayout.Space(8f);
            DrawTabBar();
            GUILayout.Space(8f);

            float exportHeight = 150f;
            float contentHeight = Mathf.Max(300f, position.height - 10f - 78f - 50f - exportHeight - 54f);
            if (position.width >= 1040f)
            {
                GUILayout.BeginHorizontal(GUILayout.Height(contentHeight));
                GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(360f, (position.width - 38f) * 0.47f)));
                DrawActiveTab(contentHeight);
                GUILayout.EndVertical();
                GUILayout.Space(8f);
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                DrawPreview(contentHeight);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            else
            {
                float previewHeight = Mathf.Clamp(contentHeight * 0.36f, 190f, 280f);
                DrawPreview(previewHeight);
                GUILayout.Space(8f);
                DrawActiveTab(Mathf.Max(220f, contentHeight - previewHeight - 8f));
            }

            GUILayout.Space(8f);
            DrawExportBay();
            GUILayout.Space(6f);
            RetroVfxGui.DrawStatus(status, statusType);
            GUILayout.EndArea();

            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }
            if (DansToolboxMotion.DrawWindowReveal(new Rect(0f, 0f, position.width, position.height), revealStartedAt))
            {
                Repaint();
            }
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(RetroVfxGui.HeaderStyle, GUILayout.Height(60f));
            GUILayout.BeginVertical();
            GUILayout.Label("RETRO VFX", RetroVfxGui.TitleStyle);
            GUILayout.Label(
                workingDirty
                    ? $"{RetroVfxPresetFactory.Presets.Count} ARCHETYPES  •  {RetroVfxSourceLibrary.InstalledCount}/{RetroVfxSourceLibrary.Descriptors.Count} SOURCES  •  UNSAVED"
                    : $"{RetroVfxPresetFactory.Presets.Count} ARCHETYPES  •  SEEDED VARIATIONS  •  SOURCE-AWARE",
                RetroVfxGui.SubtitleStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(260f));
            GUILayout.Label("RECIPE", RetroVfxGui.SectionStyle);
            RetroVfxRecipe selected = (RetroVfxRecipe)EditorGUILayout.ObjectField(
                sourceRecipe,
                typeof(RetroVfxRecipe),
                false,
                GUILayout.Height(20f));
            if (selected != sourceRecipe)
            {
                LoadRecipe(selected);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle, GUILayout.Height(42f));
            if (RetroVfxGui.TabButton("LIBRARY", "Preset families; click any patch to generate a variation", activeTab == ToolTab.Library, GUILayout.Width(90f)))
            {
                activeTab = ToolTab.Library;
            }
            if (RetroVfxGui.TabButton("SHAPE", "Compact high-level shaping controls", activeTab == ToolTab.Shape, GUILayout.Width(78f)))
            {
                activeTab = ToolTab.Shape;
            }
            if (RetroVfxGui.TabButton("LAYERS", "Detailed particle layer authoring", activeTab == ToolTab.Layers, GUILayout.Width(90f)))
            {
                activeTab = ToolTab.Layers;
            }
            if (RetroVfxGui.TabButton("SOURCES", "Asset packs, repositories, and source routing", activeTab == ToolTab.Sources, GUILayout.Width(92f)))
            {
                activeTab = ToolTab.Sources;
            }
            if (RetroVfxGui.TabButton("ADVANCED", "Distortion, shaders, VFX Graph, flipbooks, and lighting", activeTab == ToolTab.Advanced, GUILayout.Width(104f)))
            {
                activeTab = ToolTab.Advanced;
            }
            GUILayout.FlexibleSpace();
            if (RetroVfxGui.TransportButton(
                    preview != null && preview.IsPlaying ? RetroVfxGui.TransportIcon.Pause : RetroVfxGui.TransportIcon.Play,
                    "Play or pause preview (Space)",
                    preview != null && preview.IsPlaying))
            {
                preview?.TogglePlayback(workingRecipe);
            }
            GUILayout.Space(4f);
            if (RetroVfxGui.TransportButton(RetroVfxGui.TransportIcon.Stop, "Stop and return to frame zero (Esc)"))
            {
                preview?.Stop();
            }
            GUILayout.Space(4f);
            if (RetroVfxGui.TransportButton(RetroVfxGui.TransportIcon.Regenerate, "Generate another unlocked variation (R)"))
            {
                GenerateVariation();
            }
            GUILayout.Space(4f);
            if (RetroVfxGui.TransportButton(RetroVfxGui.TransportIcon.Save, "Save recipe (Ctrl/Cmd+S)"))
            {
                SaveRecipe();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawActiveTab(float height)
        {
            switch (activeTab)
            {
                case ToolTab.Layers:
                    GUILayout.BeginVertical(RetroVfxGui.PanelStyle, GUILayout.Height(height));
                    layersScroll = EditorGUILayout.BeginScrollView(layersScroll, GUILayout.Height(Mathf.Max(100f, height - 20f)));
                    DrawLayers();
                    EditorGUILayout.EndScrollView();
                    GUILayout.EndVertical();
                    break;
                case ToolTab.Advanced:
                    GUILayout.BeginVertical(RetroVfxGui.PanelStyle, GUILayout.Height(height));
                    advancedScroll = EditorGUILayout.BeginScrollView(advancedScroll, GUILayout.Height(Mathf.Max(100f, height - 20f)));
                    DrawAdvanced();
                    EditorGUILayout.EndScrollView();
                    GUILayout.EndVertical();
                    break;
                case ToolTab.Sources:
                    GUILayout.BeginVertical(RetroVfxGui.PanelStyle, GUILayout.Height(height));
                    sourcesScroll = EditorGUILayout.BeginScrollView(sourcesScroll, GUILayout.Height(Mathf.Max(100f, height - 20f)));
                    DrawSources();
                    EditorGUILayout.EndScrollView();
                    GUILayout.EndVertical();
                    break;
                case ToolTab.Shape:
                    GUILayout.BeginVertical(RetroVfxGui.PanelStyle, GUILayout.Height(height));
                    shapeScroll = EditorGUILayout.BeginScrollView(shapeScroll, GUILayout.Height(Mathf.Max(100f, height - 20f)));
                    DrawShape();
                    EditorGUILayout.EndScrollView();
                    GUILayout.EndVertical();
                    break;
                default:
                    GUILayout.BeginVertical(RetroVfxGui.PanelStyle, GUILayout.Height(height));
                    forgeScroll = EditorGUILayout.BeginScrollView(forgeScroll, GUILayout.Height(Mathf.Max(100f, height - 20f)));
                    DrawLibrary();
                    EditorGUILayout.EndScrollView();
                    GUILayout.EndVertical();
                    break;
            }
        }

        private void DrawPreview(float height)
        {
            GUILayout.BeginVertical(RetroVfxGui.PanelStyle, GUILayout.Height(height));
            GUILayout.BeginHorizontal();
            GUILayout.Label("LIVE STAGE", RetroVfxGui.SectionStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("SCROLL TO ZOOM", RetroVfxGui.TinyStyle);
            GUILayout.EndHorizontal();
            Rect rect = GUILayoutUtility.GetRect(200f, Mathf.Max(160f, height - 62f), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            preview?.Draw(rect, workingRecipe);
            float duration = RetroVfxEffectBuilder.CalculateDuration(workingRecipe);
            EditorGUI.BeginChangeCheck();
            float scrub = GUILayout.HorizontalSlider(preview?.Time ?? 0f, 0f, duration);
            if (EditorGUI.EndChangeCheck())
            {
                preview?.Scrub(scrub);
            }
            GUILayout.EndVertical();
        }

        private void DrawLibrary()
        {
            GUILayout.Label("PRESET FAMILY  •  CLICK AGAIN TO REROLL", RetroVfxGui.SectionStyle);
            GUILayout.Space(4f);
            DrawFamilyButtons();
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(FamilyLabel(libraryFamily) + " PATCHES", RetroVfxGui.SectionStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("EVERY CLICK = NEW SEED", RetroVfxGui.TinyStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            int column = 0;
            foreach (RetroVfxPresetDescriptor preset in RetroVfxPresetFactory.Presets)
            {
                if (preset.Family != libraryFamily)
                {
                    continue;
                }
                if (column % 2 == 0)
                {
                    GUILayout.BeginHorizontal();
                }
                if (RetroVfxGui.PresetButton(
                        preset.Name,
                        preset.Description,
                        "Generate a fresh " + preset.Name + " variation",
                        selectedPresetId == preset.Id))
                {
                    ApplyPreset(preset.Id);
                }
                column++;
                if (column % 2 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4f);
                }
            }
            if (column % 2 != 0)
            {
                GUILayout.Space(4f);
                GUILayoutUtility.GetRect(180f, 42f, GUILayout.ExpandWidth(true), GUILayout.Height(42f));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("REROLL ACTIVE PATCH", RetroVfxGui.PrimaryStyle))
            {
                ApplyPreset(selectedPresetId);
            }
            GUILayout.Space(3f);
            GUILayout.Label("Locked layers remain protected when using the top-bar regenerate control.", RetroVfxGui.HelpStyle);
        }

        private void DrawSources()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("SOURCE LIBRARY", RetroVfxGui.SectionStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{RetroVfxSourceLibrary.InstalledCount} DETECTED", RetroVfxGui.TinyStyle);
            GUILayout.Space(6f);
            if (GUILayout.Button("RESCAN", RetroVfxGui.TabStyle, GUILayout.Width(72f)))
            {
                RetroVfxSourceLibrary.Refresh();
                preview?.Invalidate();
                status = "Source library rescanned. Imported packs remain in their original folders.";
                statusType = MessageType.Info;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label(
                "Retro VFX routes layers through installed packs without copying restricted source files. CC0 and MIT sources may be embedded; Asset Store and commercial packs are always consumed in place.",
                RetroVfxGui.HelpStyle);
            GUILayout.Space(7f);

            sourceSearch = EditorGUILayout.TextField("Search", sourceSearch, RetroVfxGui.TextFieldStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("AUTO ROUTE ALL LAYERS", RetroVfxGui.PrimaryStyle))
            {
                Mutate("Auto Route Retro VFX Sources", () =>
                {
                    foreach (RetroVfxLayer layer in workingRecipe.layers)
                    {
                        layer.sourceMode = RetroVfxSourceMode.SourceLibrary;
                        layer.sourcePackId = string.Empty;
                    }
                });
            }
            GUILayout.Space(5f);
            if (GUILayout.Button("PROCEDURAL ONLY", RetroVfxGui.TabStyle, GUILayout.Width(126f), GUILayout.Height(42f)))
            {
                Mutate("Use Procedural Retro VFX Sources", () =>
                {
                    foreach (RetroVfxLayer layer in workingRecipe.layers)
                    {
                        layer.sourceMode = RetroVfxSourceMode.Procedural;
                        layer.sourcePackId = string.Empty;
                    }
                });
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            foreach (RetroVfxSourceDescriptor descriptor in RetroVfxSourceLibrary.Descriptors)
            {
                if (!string.IsNullOrWhiteSpace(sourceSearch) &&
                    descriptor.Name.IndexOf(sourceSearch, StringComparison.OrdinalIgnoreCase) < 0 &&
                    descriptor.Purpose.IndexOf(sourceSearch, StringComparison.OrdinalIgnoreCase) < 0 &&
                    descriptor.License.ToString().IndexOf(sourceSearch, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                GUILayout.BeginVertical(RetroVfxGui.InsetStyle);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                GUILayout.Label(descriptor.Name.ToUpperInvariant(), RetroVfxGui.LabelStyle);
                GUILayout.Label(descriptor.Purpose, RetroVfxGui.HelpStyle);
                GUILayout.Label(
                    descriptor.Installed
                        ? $"DETECTED  •  {descriptor.DetectedAssetCount} MATCHES  •  {RetroVfxSourceLibrary.LicenseLabel(descriptor)}"
                        : $"NOT DETECTED  •  {RetroVfxSourceLibrary.LicenseLabel(descriptor)}",
                    RetroVfxGui.TinyStyle);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                if (descriptor.Installed && GUILayout.Button("USE", RetroVfxGui.TabStyle, GUILayout.Width(48f)))
                {
                    string id = descriptor.Id;
                    Mutate("Route Retro VFX Source Pack", () =>
                    {
                        foreach (RetroVfxLayer layer in workingRecipe.layers)
                        {
                            layer.sourceMode = RetroVfxSourceMode.SourceLibrary;
                            layer.sourcePackId = id;
                        }
                    });
                    status = descriptor.Name + " is now the preferred source. Procedural content remains the fallback.";
                    statusType = MessageType.Info;
                }
                GUILayout.Space(4f);
                if (GUILayout.Button(descriptor.Installed ? "INFO" : "GET", RetroVfxGui.TabStyle, GUILayout.Width(48f)))
                {
                    Application.OpenURL(descriptor.Url);
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void DrawShape()
        {
            GUILayout.Label("PATCH IDENTITY", RetroVfxGui.SectionStyle);
            DrawCoreFields();
            GUILayout.Space(8f);
            GUILayout.Label("PALETTE", RetroVfxGui.SectionStyle);
            DrawPaletteFields();
            GUILayout.Space(8f);
            DrawAudioField();
            GUILayout.Space(8f);
            if (GUILayout.Button("GENERATE UNLOCKED VARIATION", RetroVfxGui.PrimaryStyle))
            {
                GenerateVariation();
            }
        }

        private void DrawFamilyButtons()
        {
            RetroVfxEffectFamily[] families =
            {
                RetroVfxEffectFamily.Impact,
                RetroVfxEffectFamily.Explosion,
                RetroVfxEffectFamily.MuzzleFlash,
                RetroVfxEffectFamily.Blood,
                RetroVfxEffectFamily.SwordSwing,
                RetroVfxEffectFamily.Magic,
                RetroVfxEffectFamily.EnergyBurst,
                RetroVfxEffectFamily.Pickup,
                RetroVfxEffectFamily.ItemShine,
                RetroVfxEffectFamily.Smoke,
                RetroVfxEffectFamily.Environment
            };
            for (int index = 0; index < families.Length; index++)
            {
                if (index % 4 == 0)
                {
                    GUILayout.BeginHorizontal();
                }
                RetroVfxEffectFamily family = families[index];
                if (RetroVfxGui.TabButton(FamilyLabel(family), family.ToString(), libraryFamily == family, GUILayout.ExpandWidth(true)))
                {
                    libraryFamily = family;
                }
                if (index % 4 == 3 || index == families.Length - 1)
                {
                    GUILayout.EndHorizontal();
                    if (index < families.Length - 1)
                    {
                        GUILayout.Space(4f);
                    }
                }
            }
        }

        private void DrawCoreFields()
        {
            string displayName = workingRecipe.displayName;
            RetroVfxEffectFamily family = workingRecipe.family;
            RetroVfxArtStyle artStyle = workingRecipe.artStyle;
            float duration = workingRecipe.duration;
            float scale = workingRecipe.scale;
            float intensity = workingRecipe.intensity;
            float direction = workingRecipe.direction;
            bool loop = workingRecipe.loopPreview;
            EditorGUI.BeginChangeCheck();
            displayName = EditorGUILayout.TextField("Name", displayName, RetroVfxGui.TextFieldStyle);
            family = (RetroVfxEffectFamily)EditorGUILayout.EnumPopup("Family", family);
            artStyle = (RetroVfxArtStyle)EditorGUILayout.EnumPopup("Art Direction", artStyle);
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
            GUILayout.FlexibleSpace();
            duration = RetroVfxGui.Knob("vfx-duration", "Duration", duration, 0.05f, 5f, 0.45f, "s", "Overall authoring and playback duration");
            GUILayout.Space(12f);
            scale = RetroVfxGui.Knob("vfx-scale", "Scale", scale, 0.1f, 4f, 1f, "x", "Generated hierarchy scale");
            GUILayout.Space(12f);
            intensity = RetroVfxGui.Knob("vfx-intensity", "Intensity", intensity, 0.1f, 3f, 1f, "x", "Particle speed and size multiplier");
            GUILayout.Space(12f);
            direction = RetroVfxGui.Knob("vfx-direction", "Direction", direction, -180f, 180f, 0f, "deg", "Primary direction around the preview plane");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            loop = EditorGUILayout.ToggleLeft("Loop preview and looping layers", loop);
            if (EditorGUI.EndChangeCheck())
            {
                Mutate("Shape Retro VFX", () =>
                {
                    workingRecipe.displayName = displayName;
                    workingRecipe.family = family;
                    workingRecipe.artStyle = artStyle;
                    workingRecipe.duration = duration;
                    workingRecipe.scale = scale;
                    workingRecipe.intensity = intensity;
                    workingRecipe.direction = direction;
                    workingRecipe.loopPreview = loop;
                    exportName = displayName;
                });
            }
        }

        private void DrawPaletteFields()
        {
            Color primary = workingRecipe.primaryColor;
            Color secondary = workingRecipe.secondaryColor;
            EditorGUI.BeginChangeCheck();
            primary = EditorGUILayout.ColorField("Primary", primary);
            secondary = EditorGUILayout.ColorField("Secondary", secondary);
            if (EditorGUI.EndChangeCheck())
            {
                Color previousPrimary = workingRecipe.primaryColor;
                Color previousSecondary = workingRecipe.secondaryColor;
                Mutate("Change Retro VFX Palette", () =>
                {
                    workingRecipe.primaryColor = primary;
                    workingRecipe.secondaryColor = secondary;
                    RecolorMatchingLayers(previousPrimary, previousSecondary, primary, secondary);
                });
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("EMBER", RetroVfxGui.TabStyle))
            {
                ApplyPalette(new Color(1f, 0.35f, 0.04f), new Color(1f, 0.92f, 0.55f));
            }
            if (GUILayout.Button("PLASMA", RetroVfxGui.TabStyle))
            {
                ApplyPalette(new Color(0.15f, 0.72f, 1f), new Color(0.82f, 0.3f, 1f));
            }
            if (GUILayout.Button("TOXIC", RetroVfxGui.TabStyle))
            {
                ApplyPalette(new Color(0.2f, 1f, 0.42f), new Color(0.85f, 1f, 0.25f));
            }
            GUILayout.EndHorizontal();
        }

        private void DrawAudioField()
        {
            GUILayout.Label("RETRO SFX LINK", RetroVfxGui.SectionStyle);
            AudioClip clip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", workingRecipe.audioClip, typeof(AudioClip), false);
            if (clip != workingRecipe.audioClip)
            {
                Mutate("Attach Retro VFX Audio", () => workingRecipe.audioClip = clip);
            }
            GUILayout.Label("Attach any rendered Retro SFX clip. Exported prefabs play it with the visual effect.", RetroVfxGui.HelpStyle);
        }

        private void DrawLayers()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{workingRecipe.layers.Count} LAYERS", RetroVfxGui.SectionStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ ADD LAYER", RetroVfxGui.TabStyle, GUILayout.Width(100f)))
            {
                AddLayer();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            int pendingDelete = -1;
            int pendingDuplicate = -1;
            int pendingMove = 0;
            int pendingMoveIndex = -1;
            for (int index = 0; index < workingRecipe.layers.Count; index++)
            {
                RetroVfxLayer layer = workingRecipe.layers[index];
                bool expanded = expandedLayers.Contains(index);
                GUILayout.BeginVertical(RetroVfxGui.InsetStyle);
                GUILayout.BeginHorizontal();
                bool enabled = EditorGUILayout.Toggle(layer.enabled, GUILayout.Width(18f));
                bool nextExpanded = EditorGUILayout.Foldout(expanded, layer.name, true);
                GUILayout.FlexibleSpace();
                bool locked = GUILayout.Toggle(layer.locked, layer.locked ? "LOCKED" : "UNLOCKED", RetroVfxGui.TabStyle, GUILayout.Width(78f));
                if (GUILayout.Button("↑", RetroVfxGui.TabStyle, GUILayout.Width(28f)))
                {
                    pendingMove = -1;
                    pendingMoveIndex = index;
                }
                if (GUILayout.Button("↓", RetroVfxGui.TabStyle, GUILayout.Width(28f)))
                {
                    pendingMove = 1;
                    pendingMoveIndex = index;
                }
                if (GUILayout.Button("DUP", RetroVfxGui.TabStyle, GUILayout.Width(42f)))
                {
                    pendingDuplicate = index;
                }
                if (GUILayout.Button("×", RetroVfxGui.TabStyle, GUILayout.Width(28f)))
                {
                    pendingDelete = index;
                }
                GUILayout.EndHorizontal();

                if (enabled != layer.enabled || locked != layer.locked)
                {
                    int captured = index;
                    Mutate("Change Retro VFX Layer State", () =>
                    {
                        workingRecipe.layers[captured].enabled = enabled;
                        workingRecipe.layers[captured].locked = locked;
                    });
                }
                if (nextExpanded)
                {
                    expandedLayers.Add(index);
                }
                else
                {
                    expandedLayers.Remove(index);
                }
                if (expanded)
                {
                    DrawLayerFields(index, layer);
                }
                GUILayout.EndVertical();
                GUILayout.Space(5f);
            }

            if (pendingDelete >= 0)
            {
                int captured = pendingDelete;
                Mutate("Remove Retro VFX Layer", () => workingRecipe.layers.RemoveAt(captured));
                ReindexExpanded(captured, -1);
            }
            else if (pendingDuplicate >= 0)
            {
                int captured = pendingDuplicate;
                Mutate("Duplicate Retro VFX Layer", () =>
                {
                    RetroVfxLayer clone = workingRecipe.layers[captured].Clone();
                    clone.name += " Copy";
                    clone.locked = false;
                    workingRecipe.layers.Insert(captured + 1, clone);
                });
                ReindexExpanded(pendingDuplicate + 1, 1);
            }
            else if (pendingMoveIndex >= 0)
            {
                int destination = Mathf.Clamp(pendingMoveIndex + pendingMove, 0, workingRecipe.layers.Count - 1);
                if (destination != pendingMoveIndex)
                {
                    int source = pendingMoveIndex;
                    Mutate("Reorder Retro VFX Layer", () =>
                    {
                        RetroVfxLayer moving = workingRecipe.layers[source];
                        workingRecipe.layers.RemoveAt(source);
                        workingRecipe.layers.Insert(destination, moving);
                    });
                    expandedLayers.Clear();
                    expandedLayers.Add(destination);
                }
            }

            if (workingRecipe.layers.Count == 0)
            {
                GUILayout.Space(20f);
                GUILayout.Label("This recipe has no particle layers.", RetroVfxGui.LabelStyle);
                GUILayout.Label("Add a layer, import a flipbook, or attach a VFX Graph asset.", RetroVfxGui.HelpStyle);
            }
        }

        private void DrawLayerFields(int index, RetroVfxLayer layer)
        {
            string name = layer.name;
            RetroVfxPhase phase = layer.phase;
            RetroVfxLayerKind kind = layer.kind;
            RetroVfxParticleShape shape = layer.shape;
            RetroVfxSpriteStyle spriteStyle = layer.spriteStyle;
            RetroVfxMotionMode motion = layer.motion;
            RetroVfxBlendMode blendMode = layer.blendMode;
            RetroVfxSourceMode sourceMode = layer.sourceMode;
            RetroVfxRenderGeometry renderGeometry = layer.renderGeometry;
            string sourcePackId = layer.sourcePackId;
            Texture2D sourceTexture = layer.sourceTexture;
            Mesh sourceMesh = layer.sourceMesh;
            Material materialOverride = layer.materialOverride;
            int sourceColumns = layer.sourceColumns;
            int sourceRows = layer.sourceRows;
            float sourceFps = layer.sourceFramesPerSecond;
            bool sourceLoop = layer.sourceLoop;
            int count = layer.count;
            float rateOverTime = layer.rateOverTime;
            int burstCount = layer.burstCount;
            float burstInterval = layer.burstInterval;
            float delay = layer.delay;
            float lifetime = layer.lifetime;
            float speed = layer.speed;
            float speedRandomness = layer.speedRandomness;
            float size = layer.size;
            float sizeRandomness = layer.sizeRandomness;
            float spread = layer.spread;
            float emissionRadius = layer.emissionRadius;
            Vector2 offset = layer.offset;
            Vector2 aspect = layer.aspect;
            Vector2 velocity = layer.velocity;
            float gravity = layer.gravity;
            float rotation = layer.rotation;
            float rotationSpeed = layer.rotationSpeed;
            bool randomRotation = layer.randomRotation;
            float stretch = layer.stretch;
            Color start = layer.startColor;
            Color end = layer.endColor;
            Gradient colorOverLifetime = layer.colorOverLifetime;
            AnimationCurve curve = layer.sizeOverLifetime;
            RetroVfxNoiseProfile noiseProfile = layer.noiseProfile;
            float noiseStrength = layer.noiseStrength;
            float noiseFrequency = layer.noiseFrequency;
            float noiseScrollSpeed = layer.noiseScrollSpeed;
            int noiseOctaves = layer.noiseOctaves;
            float drag = layer.drag;
            bool trailEnabled = layer.trailEnabled;
            float trailLifetime = layer.trailLifetime;
            float trailRatio = layer.trailRatio;
            float trailWidth = layer.trailWidth;
            Color trailColor = layer.trailColor;
            float dissolve = layer.dissolve;
            float edgeGlow = layer.edgeGlow;
            float emission = layer.emission;
            Vector2 flowSpeed = layer.flowSpeed;
            bool softParticles = layer.softParticles;
            int spawnFromLayer = layer.spawnFromLayer;
            RetroVfxSpawnEvent spawnEvent = layer.spawnEvent;
            bool collisionEnabled = layer.collisionEnabled;
            float collisionDampen = layer.collisionDampen;
            float collisionBounce = layer.collisionBounce;
            EditorGUI.BeginChangeCheck();
            name = EditorGUILayout.TextField("Name", name);
            phase = (RetroVfxPhase)EditorGUILayout.EnumPopup("Phase", phase);
            kind = (RetroVfxLayerKind)EditorGUILayout.EnumPopup("Kind", kind);
            shape = (RetroVfxParticleShape)EditorGUILayout.EnumPopup("Emitter", shape);
            spriteStyle = (RetroVfxSpriteStyle)EditorGUILayout.EnumPopup("Sprite", spriteStyle);
            motion = (RetroVfxMotionMode)EditorGUILayout.EnumPopup("Motion", motion);
            blendMode = (RetroVfxBlendMode)EditorGUILayout.EnumPopup("Blend", blendMode);
            GUILayout.Space(4f);
            GUILayout.Label("SOURCE + GEOMETRY", RetroVfxGui.TinyStyle);
            sourceMode = (RetroVfxSourceMode)EditorGUILayout.EnumPopup("Source Mode", sourceMode);
            renderGeometry = (RetroVfxRenderGeometry)EditorGUILayout.EnumPopup("Geometry", renderGeometry);
            if (sourceMode == RetroVfxSourceMode.SourceLibrary)
            {
                sourcePackId = EditorGUILayout.TextField("Preferred Pack", sourcePackId);
                GUILayout.Label("Leave Preferred Pack empty to auto-route across every detected source.", RetroVfxGui.HelpStyle);
            }
            if (sourceMode == RetroVfxSourceMode.Texture || sourceMode == RetroVfxSourceMode.Flipbook || sourceMode == RetroVfxSourceMode.SourceLibrary)
            {
                sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Texture Override", sourceTexture, typeof(Texture2D), false);
            }
            if (sourceMode == RetroVfxSourceMode.Flipbook || sourceTexture != null && sourceColumns * sourceRows > 1)
            {
                sourceColumns = EditorGUILayout.IntSlider("Columns", sourceColumns, 1, 32);
                sourceRows = EditorGUILayout.IntSlider("Rows", sourceRows, 1, 32);
                sourceFps = EditorGUILayout.Slider("Frames / Second", sourceFps, 1f, 120f);
                sourceLoop = EditorGUILayout.Toggle("Loop Frames", sourceLoop);
            }
            if (sourceMode == RetroVfxSourceMode.Mesh || renderGeometry == RetroVfxRenderGeometry.Mesh)
            {
                sourceMesh = (Mesh)EditorGUILayout.ObjectField("Mesh Override", sourceMesh, typeof(Mesh), false);
            }
            if (sourceMode == RetroVfxSourceMode.Material)
            {
                materialOverride = (Material)EditorGUILayout.ObjectField("Material", materialOverride, typeof(Material), false);
            }
            GUILayout.Space(4f);
            GUILayout.Label("EMISSION + MOTION", RetroVfxGui.TinyStyle);
            GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
            GUILayout.FlexibleSpace();
            count = Mathf.RoundToInt(RetroVfxGui.Knob($"layer-{index}-count", "Count", count, 1f, 256f, 12f, "0", "Particles in each burst"));
            GUILayout.Space(8f);
            rateOverTime = RetroVfxGui.Knob($"layer-{index}-rate", "Rate", rateOverTime, 0f, 256f, 0f, "0.0", "Continuous particles per second");
            GUILayout.Space(8f);
            burstCount = Mathf.RoundToInt(RetroVfxGui.Knob($"layer-{index}-bursts", "Bursts", burstCount, 1f, 16f, 1f, "0", "Number of repeated bursts"));
            GUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(burstCount <= 1))
            {
                burstInterval = RetroVfxGui.Knob($"layer-{index}-gap", "Gap", burstInterval, 0f, 2f, 0.08f, "s", "Seconds between repeated bursts");
            }
            GUILayout.Space(8f);
            delay = RetroVfxGui.Knob($"layer-{index}-delay", "Delay", delay, 0f, 3f, 0f, "s", "Layer start delay");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
            GUILayout.FlexibleSpace();
            lifetime = RetroVfxGui.Knob($"layer-{index}-lifetime", "Lifetime", lifetime, 0.01f, 5f, 0.45f, "s", "Particle lifetime");
            GUILayout.Space(8f);
            speed = RetroVfxGui.Knob($"layer-{index}-speed", "Speed", speed, 0f, 20f, 3f, "0.0", "Initial particle speed");
            GUILayout.Space(8f);
            speedRandomness = RetroVfxGui.Knob($"layer-{index}-speed-jitter", "Speed Jit", speedRandomness, 0f, 1f, 0.2f, "%", "Random speed variation");
            GUILayout.Space(8f);
            size = RetroVfxGui.Knob($"layer-{index}-size", "Size", size, 0.01f, 4f, 0.35f, "0.00", "Particle size");
            GUILayout.Space(8f);
            sizeRandomness = RetroVfxGui.Knob($"layer-{index}-size-jitter", "Size Jit", sizeRandomness, 0f, 1f, 0.15f, "%", "Random size variation");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
            GUILayout.FlexibleSpace();
            spread = RetroVfxGui.Knob($"layer-{index}-spread", "Spread", spread, 0f, 360f, 30f, "deg", "Emission arc");
            GUILayout.Space(8f);
            emissionRadius = RetroVfxGui.Knob($"layer-{index}-radius", "Radius", emissionRadius, 0f, 4f, 0f, "0.00", "Emitter radius");
            GUILayout.Space(8f);
            gravity = RetroVfxGui.Knob($"layer-{index}-gravity", "Gravity", gravity, -3f, 3f, 0f, "0.00", "Particle gravity multiplier");
            GUILayout.Space(8f);
            rotation = RetroVfxGui.Knob($"layer-{index}-rotation", "Rotation", rotation, -180f, 180f, 0f, "deg", "Initial rotation");
            GUILayout.Space(8f);
            rotationSpeed = RetroVfxGui.Knob($"layer-{index}-spin", "Spin", rotationSpeed, -720f, 720f, 0f, "deg", "Degrees per second");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            offset = EditorGUILayout.Vector2Field("Offset", offset);
            aspect = EditorGUILayout.Vector2Field("Aspect", aspect);
            if (motion == RetroVfxMotionMode.Drift)
            {
                velocity = EditorGUILayout.Vector2Field("Drift Velocity", velocity);
            }
            randomRotation = EditorGUILayout.Toggle("Random Rotation", randomRotation);
            if (spriteStyle == RetroVfxSpriteStyle.Spark ||
                spriteStyle == RetroVfxSpriteStyle.Beam ||
                spriteStyle == RetroVfxSpriteStyle.BloodDrop ||
                kind == RetroVfxLayerKind.Trail)
            {
                GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
                stretch = RetroVfxGui.Knob($"layer-{index}-stretch", "Stretch", stretch, 0f, 10f, 1f, "0.0", "Particle length multiplier");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            start = EditorGUILayout.ColorField("Start Color", start);
            end = EditorGUILayout.ColorField("End Color", end);
            colorOverLifetime = EditorGUILayout.GradientField("Color Over Life", colorOverLifetime);
            curve = EditorGUILayout.CurveField("Size Over Life", curve);
            GUILayout.Space(4f);
            GUILayout.Label("TURBULENCE + TRAILS", RetroVfxGui.TinyStyle);
            noiseProfile = (RetroVfxNoiseProfile)EditorGUILayout.EnumPopup("Noise Profile", noiseProfile);
            using (new EditorGUI.DisabledScope(noiseProfile == RetroVfxNoiseProfile.None))
            {
                GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
                GUILayout.FlexibleSpace();
                noiseStrength = RetroVfxGui.Knob($"layer-{index}-noise-strength", "Strength", noiseStrength, 0f, 4f, 0f, "0.00", "Turbulence strength");
                GUILayout.Space(10f);
                noiseFrequency = RetroVfxGui.Knob($"layer-{index}-noise-frequency", "Frequency", noiseFrequency, 0.01f, 5f, 0.5f, "0.00", "Turbulence scale");
                GUILayout.Space(10f);
                noiseScrollSpeed = RetroVfxGui.Knob($"layer-{index}-noise-scroll", "Scroll", noiseScrollSpeed, -4f, 4f, 0f, "0.00", "Turbulence animation speed");
                GUILayout.Space(10f);
                noiseOctaves = Mathf.RoundToInt(RetroVfxGui.Knob($"layer-{index}-noise-octaves", "Octaves", noiseOctaves, 1f, 3f, 1f, "0", "Turbulence detail passes"));
                GUILayout.Space(10f);
                drag = RetroVfxGui.Knob($"layer-{index}-drag", "Drag", drag, 0f, 1f, 0f, "%", "Velocity damping");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            if (noiseProfile == RetroVfxNoiseProfile.None)
            {
                drag = EditorGUILayout.Slider("Drag", drag, 0f, 1f);
            }
            trailEnabled = EditorGUILayout.Toggle("Particle Trail", trailEnabled);
            using (new EditorGUI.DisabledScope(!trailEnabled))
            {
                GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
                GUILayout.FlexibleSpace();
                trailLifetime = RetroVfxGui.Knob($"layer-{index}-trail-lifetime", "Life", trailLifetime, 0.01f, 3f, 0.15f, "s", "Trail lifetime");
                GUILayout.Space(14f);
                trailRatio = RetroVfxGui.Knob($"layer-{index}-trail-ratio", "Ratio", trailRatio, 0f, 1f, 1f, "%", "Fraction of particles with trails");
                GUILayout.Space(14f);
                trailWidth = RetroVfxGui.Knob($"layer-{index}-trail-width", "Width", trailWidth, 0.001f, 2f, 0.2f, "0.00", "Trail width multiplier");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                trailColor = EditorGUILayout.ColorField("Trail Color", trailColor);
            }
            GUILayout.Space(4f);
            GUILayout.Label("SURFACE + EVENTS", RetroVfxGui.TinyStyle);
            GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
            GUILayout.FlexibleSpace();
            dissolve = RetroVfxGui.Knob($"layer-{index}-dissolve", "Dissolve", dissolve, 0f, 1f, 0f, "%", "Dissolve threshold");
            GUILayout.Space(14f);
            edgeGlow = RetroVfxGui.Knob($"layer-{index}-edge-glow", "Edge Glow", edgeGlow, 0f, 1f, 0.2f, "%", "Luminous dissolve edge");
            GUILayout.Space(14f);
            emission = RetroVfxGui.Knob($"layer-{index}-emission", "Emission", emission, 0f, 2f, 1f, "0.00", "Emission multiplier");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            flowSpeed = EditorGUILayout.Vector2Field("Flow Speed", flowSpeed);
            softParticles = EditorGUILayout.Toggle("Soft Particles", softParticles);
            spawnEvent = (RetroVfxSpawnEvent)EditorGUILayout.EnumPopup("Spawn Event", spawnEvent);
            using (new EditorGUI.DisabledScope(spawnEvent == RetroVfxSpawnEvent.None))
            {
                spawnFromLayer = EditorGUILayout.IntSlider("Parent Layer", spawnFromLayer, -1, Mathf.Max(-1, workingRecipe.layers.Count - 1));
            }
            collisionEnabled = EditorGUILayout.Toggle("World Collision", collisionEnabled);
            using (new EditorGUI.DisabledScope(!collisionEnabled))
            {
                GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
                GUILayout.FlexibleSpace();
                collisionDampen = RetroVfxGui.Knob($"layer-{index}-collision-dampen", "Dampen", collisionDampen, 0f, 1f, 0.4f, "%", "Velocity lost on collision");
                GUILayout.Space(16f);
                collisionBounce = RetroVfxGui.Knob($"layer-{index}-collision-bounce", "Bounce", collisionBounce, 0f, 1f, 0f, "%", "Velocity reflected on collision");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck())
            {
                int captured = index;
                Mutate("Edit Retro VFX Layer", () =>
                {
                    RetroVfxLayer target = workingRecipe.layers[captured];
                    target.name = name;
                    target.phase = phase;
                    target.kind = kind;
                    target.shape = shape;
                    target.spriteStyle = spriteStyle;
                    target.motion = motion;
                    target.blendMode = blendMode;
                    target.sourceMode = sourceMode;
                    target.renderGeometry = renderGeometry;
                    target.sourcePackId = sourcePackId;
                    target.sourceTexture = sourceTexture;
                    target.sourceMesh = sourceMesh;
                    target.materialOverride = materialOverride;
                    target.sourceColumns = sourceColumns;
                    target.sourceRows = sourceRows;
                    target.sourceFramesPerSecond = sourceFps;
                    target.sourceLoop = sourceLoop;
                    target.count = count;
                    target.rateOverTime = rateOverTime;
                    target.burstCount = burstCount;
                    target.burstInterval = burstInterval;
                    target.delay = delay;
                    target.lifetime = lifetime;
                    target.speed = speed;
                    target.speedRandomness = speedRandomness;
                    target.size = size;
                    target.sizeRandomness = sizeRandomness;
                    target.spread = spread;
                    target.emissionRadius = emissionRadius;
                    target.offset = offset;
                    target.aspect = aspect;
                    target.velocity = velocity;
                    target.gravity = gravity;
                    target.rotation = rotation;
                    target.rotationSpeed = rotationSpeed;
                    target.randomRotation = randomRotation;
                    target.stretch = stretch;
                    target.startColor = start;
                    target.endColor = end;
                    target.colorOverLifetime = colorOverLifetime;
                    target.sizeOverLifetime = curve;
                    target.noiseProfile = noiseProfile;
                    target.noiseStrength = noiseStrength;
                    target.noiseFrequency = noiseFrequency;
                    target.noiseScrollSpeed = noiseScrollSpeed;
                    target.noiseOctaves = noiseOctaves;
                    target.drag = drag;
                    target.trailEnabled = trailEnabled;
                    target.trailLifetime = trailLifetime;
                    target.trailRatio = trailRatio;
                    target.trailWidth = trailWidth;
                    target.trailColor = trailColor;
                    target.dissolve = dissolve;
                    target.edgeGlow = edgeGlow;
                    target.emission = emission;
                    target.flowSpeed = flowSpeed;
                    target.softParticles = softParticles;
                    target.spawnFromLayer = spawnFromLayer;
                    target.spawnEvent = spawnEvent;
                    target.collisionEnabled = collisionEnabled;
                    target.collisionDampen = collisionDampen;
                    target.collisionBounce = collisionBounce;
                });
            }
        }

        private void DrawAdvanced()
        {
            DrawDistortion();
            GUILayout.Space(8f);
            DrawCustomRendering();
            GUILayout.Space(8f);
            DrawExternalEffectLayer();
            GUILayout.Space(8f);
            DrawVfxGraph();
            GUILayout.Space(8f);
            DrawFlipbookImport();
            GUILayout.Space(8f);
            DrawLighting();
            GUILayout.Space(8f);
            DrawSceneResponse();
        }

        private void DrawDistortion()
        {
            GUILayout.BeginVertical(RetroVfxGui.InsetStyle);
            GUILayout.Label("DISTORTION", RetroVfxGui.SectionStyle);
            bool enabled = workingRecipe.advanced.distortionEnabled;
            float strength = workingRecipe.advanced.distortionStrength;
            float size = workingRecipe.advanced.distortionSize;
            Material material = workingRecipe.advanced.distortionMaterial;
            EditorGUI.BeginChangeCheck();
            enabled = EditorGUILayout.ToggleLeft("Add a distortion shockwave", enabled);
            using (new EditorGUI.DisabledScope(!enabled))
            {
                GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
                GUILayout.FlexibleSpace();
                strength = RetroVfxGui.Knob("advanced-distortion-strength", "Strength", strength, 0f, 1f, 0.2f, "%", "Distortion intensity");
                GUILayout.Space(24f);
                size = RetroVfxGui.Knob("advanced-distortion-size", "Size", size, 0.05f, 8f, 1f, "0.00", "Distortion shockwave size");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                material = (Material)EditorGUILayout.ObjectField("Override Material", material, typeof(Material), false);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Mutate("Change Retro VFX Distortion", () =>
                {
                    workingRecipe.advanced.distortionEnabled = enabled;
                    workingRecipe.advanced.distortionStrength = strength;
                    workingRecipe.advanced.distortionSize = size;
                    workingRecipe.advanced.distortionMaterial = material;
                });
            }
            if (enabled && material == null && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            {
                EditorGUILayout.HelpBox(
                    "The bundled GrabPass distortion is for the Built-in pipeline. Assign a pipeline-compatible distortion material for URP or HDRP.",
                    MessageType.Warning);
            }
            GUILayout.EndVertical();
        }

        private void DrawCustomRendering()
        {
            GUILayout.BeginVertical(RetroVfxGui.InsetStyle);
            GUILayout.Label("PRODUCTION SHADER", RetroVfxGui.SectionStyle);
            bool productionShader = workingRecipe.advanced.productionShader;
            Material material = workingRecipe.advanced.customMaterial;
            Shader shader = workingRecipe.advanced.customShader;
            Texture2D dissolveTexture = workingRecipe.advanced.dissolveTexture;
            Texture2D flowTexture = workingRecipe.advanced.flowTexture;
            float globalDissolve = workingRecipe.advanced.globalDissolve;
            float globalEmission = workingRecipe.advanced.globalEmission;
            float globalEdgeGlow = workingRecipe.advanced.globalEdgeGlow;
            bool softParticles = workingRecipe.advanced.softParticles;
            bool flipbookBlending = workingRecipe.advanced.flipbookBlending;
            EditorGUI.BeginChangeCheck();
            productionShader = EditorGUILayout.ToggleLeft("Use the Retro VFX production shader", productionShader);
            material = (Material)EditorGUILayout.ObjectField("Material Override", material, typeof(Material), false);
            using (new EditorGUI.DisabledScope(material != null))
            {
                shader = (Shader)EditorGUILayout.ObjectField("Shader Override", shader, typeof(Shader), false);
            }
            dissolveTexture = (Texture2D)EditorGUILayout.ObjectField("Dissolve / Noise", dissolveTexture, typeof(Texture2D), false);
            flowTexture = (Texture2D)EditorGUILayout.ObjectField("Flow Map", flowTexture, typeof(Texture2D), false);
            GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
            GUILayout.FlexibleSpace();
            globalDissolve = RetroVfxGui.Knob("advanced-global-dissolve", "Dissolve", globalDissolve, 0f, 1f, 0f, "%", "Global dissolve threshold");
            GUILayout.Space(20f);
            globalEmission = RetroVfxGui.Knob("advanced-global-emission", "Emission", globalEmission, 0f, 2f, 1f, "0.00", "Global emission multiplier");
            GUILayout.Space(20f);
            globalEdgeGlow = RetroVfxGui.Knob("advanced-global-edge", "Edge Glow", globalEdgeGlow, 0f, 2f, 0.2f, "0.00", "Global dissolve-edge brightness");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            softParticles = EditorGUILayout.Toggle("Soft Particles", softParticles);
            flipbookBlending = EditorGUILayout.Toggle("Flipbook Blending", flipbookBlending);
            if (EditorGUI.EndChangeCheck())
            {
                Mutate("Change Retro VFX Rendering", () =>
                {
                    workingRecipe.advanced.productionShader = productionShader;
                    workingRecipe.advanced.customMaterial = material;
                    workingRecipe.advanced.customShader = shader;
                    workingRecipe.advanced.dissolveTexture = dissolveTexture;
                    workingRecipe.advanced.flowTexture = flowTexture;
                    workingRecipe.advanced.globalDissolve = globalDissolve;
                    workingRecipe.advanced.globalEmission = globalEmission;
                    workingRecipe.advanced.globalEdgeGlow = globalEdgeGlow;
                    workingRecipe.advanced.softParticles = softParticles;
                    workingRecipe.advanced.flipbookBlending = flipbookBlending;
                });
            }
            GUILayout.Label("The bundled shader supplies dissolve edges, emission, flow-map warping, blend modes, and soft particles. Nova Shader materials can be assigned per layer or globally when that package is installed.", RetroVfxGui.HelpStyle);
            GUILayout.EndVertical();
        }

        private void DrawVfxGraph()
        {
            GUILayout.BeginVertical(RetroVfxGui.InsetStyle);
            GUILayout.Label("VFX GRAPH BRIDGE", RetroVfxGui.SectionStyle);
            bool available = RetroVfxEffectBuilder.IsVfxGraphAvailable();
            using (new EditorGUI.DisabledScope(!available))
            {
                Object graph = EditorGUILayout.ObjectField("Visual Effect Asset", workingRecipe.advanced.vfxGraphAsset, typeof(Object), false);
                if (graph != workingRecipe.advanced.vfxGraphAsset)
                {
                    if (RetroVfxEffectBuilder.IsVfxGraphAsset(graph))
                    {
                        Mutate("Attach VFX Graph", () => workingRecipe.advanced.vfxGraphAsset = graph);
                    }
                    else
                    {
                        status = "The selected object is not a VisualEffectAsset.";
                        statusType = MessageType.Error;
                    }
                }
            }
            GUILayout.Label(
                available
                    ? "The graph is attached beside generated particle layers and retained in exported prefabs. Graph-owned exposed parameters remain editable normally."
                    : "Install Unity's Visual Effect Graph package to enable graph bridging. Core Retro VFX remains available.",
                RetroVfxGui.HelpStyle);
            GUILayout.EndVertical();
        }

        private void DrawExternalEffectLayer()
        {
            GUILayout.BeginVertical(RetroVfxGui.InsetStyle);
            GUILayout.Label("THIRD-PARTY EFFECT LAYER", RetroVfxGui.SectionStyle);
            bool enabled = workingRecipe.advanced.externalEffectEnabled;
            GameObject prefab = workingRecipe.advanced.externalEffectPrefab;
            Vector3 position = workingRecipe.advanced.externalEffectPosition;
            Vector3 rotation = workingRecipe.advanced.externalEffectRotation;
            Vector3 scale = workingRecipe.advanced.externalEffectScale;
            EditorGUI.BeginChangeCheck();
            enabled = EditorGUILayout.ToggleLeft("Layer an installed effect prefab", enabled);
            using (new EditorGUI.DisabledScope(!enabled))
            {
                prefab = (GameObject)EditorGUILayout.ObjectField("Effect Prefab", prefab, typeof(GameObject), false);
                position = EditorGUILayout.Vector3Field("Local Position", position);
                rotation = EditorGUILayout.Vector3Field("Local Rotation", rotation);
                scale = EditorGUILayout.Vector3Field("Local Scale", scale);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Mutate("Change External Retro VFX Layer", () =>
                {
                    workingRecipe.advanced.externalEffectEnabled = enabled;
                    workingRecipe.advanced.externalEffectPrefab = prefab;
                    workingRecipe.advanced.externalEffectPosition = position;
                    workingRecipe.advanced.externalEffectRotation = rotation;
                    workingRecipe.advanced.externalEffectScale = scale;
                });
            }
            GUILayout.Label("Use this for Asset Store, Effekseer, or repository prefabs. Exported effects retain references to the installed source package; Retro VFX never duplicates restricted raw assets.", RetroVfxGui.HelpStyle);
            GUILayout.EndVertical();
        }

        private void DrawFlipbookImport()
        {
            GUILayout.BeginVertical(RetroVfxGui.InsetStyle);
            GUILayout.Label("FLIPBOOK IMPORT", RetroVfxGui.SectionStyle);
            Texture2D texture = (Texture2D)EditorGUILayout.ObjectField("Texture", workingRecipe.advanced.importedFlipbook, typeof(Texture2D), false);
            int columns = workingRecipe.advanced.flipbookColumns;
            int rows = workingRecipe.advanced.flipbookRows;
            float fps = workingRecipe.advanced.flipbookFramesPerSecond;
            bool loop = workingRecipe.advanced.flipbookLoop;
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
            GUILayout.FlexibleSpace();
            columns = Mathf.RoundToInt(RetroVfxGui.Knob("advanced-flipbook-columns", "Columns", columns, 1f, 16f, 4f, "0", "Sprite-sheet columns"));
            GUILayout.Space(20f);
            rows = Mathf.RoundToInt(RetroVfxGui.Knob("advanced-flipbook-rows", "Rows", rows, 1f, 16f, 4f, "0", "Sprite-sheet rows"));
            GUILayout.Space(20f);
            fps = RetroVfxGui.Knob("advanced-flipbook-fps", "FPS", fps, 1f, 120f, 24f, "0", "Flipbook playback rate");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            loop = EditorGUILayout.Toggle("Loop", loop);
            if (EditorGUI.EndChangeCheck() || texture != workingRecipe.advanced.importedFlipbook)
            {
                Mutate("Change Retro VFX Flipbook", () =>
                {
                    workingRecipe.advanced.importedFlipbook = texture;
                    workingRecipe.advanced.flipbookColumns = columns;
                    workingRecipe.advanced.flipbookRows = rows;
                    workingRecipe.advanced.flipbookFramesPerSecond = fps;
                    workingRecipe.advanced.flipbookLoop = loop;
                });
            }
            GUILayout.Label(
                texture == null
                    ? "Assign a uniformly tiled texture to layer an existing flipbook into the recipe."
                    : $"{columns * rows} frames  •  {(columns * rows / Mathf.Max(1f, fps)):0.00}s",
                RetroVfxGui.HelpStyle);
            GUILayout.EndVertical();
        }

        private void DrawLighting()
        {
            GUILayout.BeginVertical(RetroVfxGui.InsetStyle);
            GUILayout.Label("ADVANCED LIGHTING", RetroVfxGui.SectionStyle);
            bool enabled = workingRecipe.advanced.lightEnabled;
            LightType type = workingRecipe.advanced.lightType;
            Color color = workingRecipe.advanced.lightColor;
            float intensity = workingRecipe.advanced.lightIntensity;
            float range = workingRecipe.advanced.lightRange;
            LightShadows shadows = workingRecipe.advanced.lightShadows;
            AnimationCurve curve = workingRecipe.advanced.lightIntensityOverLifetime;
            EditorGUI.BeginChangeCheck();
            enabled = EditorGUILayout.ToggleLeft("Add an animated effect light", enabled);
            using (new EditorGUI.DisabledScope(!enabled))
            {
                type = (LightType)EditorGUILayout.EnumPopup("Type", type);
                color = EditorGUILayout.ColorField("Color", color);
                GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
                GUILayout.FlexibleSpace();
                intensity = RetroVfxGui.Knob("advanced-light-intensity", "Intensity", intensity, 0f, 20f, 2f, "0.00", "Peak light intensity");
                GUILayout.Space(24f);
                range = RetroVfxGui.Knob("advanced-light-range", "Range", range, 0.1f, 40f, 5f, "0.0", "Light range");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                shadows = (LightShadows)EditorGUILayout.EnumPopup("Shadows", shadows);
                curve = EditorGUILayout.CurveField("Intensity Over Life", curve);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Mutate("Change Retro VFX Lighting", () =>
                {
                    workingRecipe.advanced.lightEnabled = enabled;
                    workingRecipe.advanced.lightType = type;
                    workingRecipe.advanced.lightColor = color;
                    workingRecipe.advanced.lightIntensity = intensity;
                    workingRecipe.advanced.lightRange = range;
                    workingRecipe.advanced.lightShadows = shadows;
                    workingRecipe.advanced.lightIntensityOverLifetime = curve;
                });
            }
            GUILayout.Label("Exported RetroVfxPlayer components evaluate the intensity curve without requiring an Animator.", RetroVfxGui.HelpStyle);
            GUILayout.EndVertical();
        }

        private void DrawSceneResponse()
        {
            GUILayout.BeginVertical(RetroVfxGui.InsetStyle);
            GUILayout.Label("SCENE RESPONSE", RetroVfxGui.SectionStyle);
            bool shake = workingRecipe.advanced.cameraShakeEnabled;
            float shakeAmplitude = workingRecipe.advanced.cameraShakeAmplitude;
            float shakeDuration = workingRecipe.advanced.cameraShakeDuration;
            bool hitStop = workingRecipe.advanced.hitStopEventEnabled;
            float hitStopDuration = workingRecipe.advanced.hitStopDuration;
            bool decal = workingRecipe.advanced.decalEventEnabled;
            GameObject decalPrefab = workingRecipe.advanced.decalPrefab;
            EditorGUI.BeginChangeCheck();
            shake = EditorGUILayout.ToggleLeft("Publish camera shake request", shake);
            using (new EditorGUI.DisabledScope(!shake))
            {
                GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
                GUILayout.FlexibleSpace();
                shakeAmplitude = RetroVfxGui.Knob("advanced-shake-amplitude", "Amplitude", shakeAmplitude, 0f, 2f, 0.2f, "0.00", "Camera-shake amplitude request");
                GUILayout.Space(24f);
                shakeDuration = RetroVfxGui.Knob("advanced-shake-duration", "Duration", shakeDuration, 0.01f, 2f, 0.15f, "s", "Camera-shake duration request");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            hitStop = EditorGUILayout.ToggleLeft("Publish hit-stop request", hitStop);
            using (new EditorGUI.DisabledScope(!hitStop))
            {
                GUILayout.BeginHorizontal(RetroVfxGui.InsetStyle);
                hitStopDuration = RetroVfxGui.Knob("advanced-hitstop-duration", "Duration", hitStopDuration, 0f, 0.25f, 0.05f, "s", "Hit-stop duration request");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            decal = EditorGUILayout.ToggleLeft("Publish decal / splat request", decal);
            using (new EditorGUI.DisabledScope(!decal))
            {
                decalPrefab = (GameObject)EditorGUILayout.ObjectField("Decal Prefab", decalPrefab, typeof(GameObject), false);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Mutate("Change Retro VFX Scene Response", () =>
                {
                    workingRecipe.advanced.cameraShakeEnabled = shake;
                    workingRecipe.advanced.cameraShakeAmplitude = shakeAmplitude;
                    workingRecipe.advanced.cameraShakeDuration = shakeDuration;
                    workingRecipe.advanced.hitStopEventEnabled = hitStop;
                    workingRecipe.advanced.hitStopDuration = hitStopDuration;
                    workingRecipe.advanced.decalEventEnabled = decal;
                    workingRecipe.advanced.decalPrefab = decalPrefab;
                });
            }
            GUILayout.Label("RetroVfxPlayer raises optional requests. Games decide how cameras, time scale, and decals respond, so generated prefabs stay framework-neutral.", RetroVfxGui.HelpStyle);
            GUILayout.EndVertical();
        }

        private void DrawExportBay()
        {
            GUILayout.BeginVertical(RetroVfxGui.HeaderStyle, GUILayout.Height(142f));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("OUTPUT", RetroVfxGui.SectionStyle);
            exportName = EditorGUILayout.TextField(exportName, RetroVfxGui.TextFieldStyle);
            outputFolder = EditorGUILayout.TextField(outputFolder, RetroVfxGui.TextFieldStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CHOOSE FOLDER", RetroVfxGui.TabStyle, GUILayout.Width(112f)))
            {
                ChooseOutputFolder();
            }
            if (GUILayout.Button(sourceRecipe == null ? "SAVE RECIPE" : "UPDATE RECIPE", RetroVfxGui.TabStyle, GUILayout.Width(112f)))
            {
                SaveRecipe();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(10f);
            GUILayout.BeginVertical(GUILayout.Width(250f));
            outputMode = (RetroVfxOutputMode)EditorGUILayout.EnumPopup("Export", outputMode);
            using (new EditorGUI.DisabledScope(outputMode == RetroVfxOutputMode.ParticlePrefab))
            {
                flipbookFrameSize = EditorGUILayout.IntPopup(
                    "Frame Size",
                    flipbookFrameSize,
                    new[] { "64", "128", "256", "512" },
                    new[] { 64, 128, 256, 512 });
                GUILayout.BeginHorizontal();
                flipbookColumns = EditorGUILayout.IntField("Grid", flipbookColumns);
                GUILayout.Label("×", GUILayout.Width(14f));
                flipbookRows = EditorGUILayout.IntField(flipbookRows, GUILayout.Width(42f));
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("EXPORT VFX", RetroVfxGui.PrimaryStyle))
            {
                Export();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void ApplyPreset(string presetId)
        {
            int seed = NextVariationSeed(presetId);
            Mutate("Generate Retro VFX Preset Variation", () =>
            {
                selectedPresetId = presetId;
                RetroVfxPresetFactory.ApplyVariation(presetId, workingRecipe, seed);
                libraryFamily = workingRecipe.family;
                exportName = workingRecipe.displayName;
            });
            preview?.Restart();
            status = "Generated " + RetroVfxPresetFactory.Find(presetId).Name + " variation " + workingRecipe.seed + ". Click again to reroll.";
            statusType = MessageType.Info;
        }

        private void GenerateVariation()
        {
            int seed = NextVariationSeed(selectedPresetId);
            Mutate("Generate Retro VFX Variation", () => RetroVfxPresetFactory.RandomizeUnlocked(workingRecipe, seed));
            preview?.Restart();
            status = $"Generated deterministic variation {workingRecipe.seed}. Locked layers were preserved.";
            statusType = MessageType.Info;
        }

        private int NextVariationSeed(string presetId)
        {
            int presetHash = string.IsNullOrEmpty(presetId) ? 0 : Animator.StringToHash(presetId);
            int seed = unchecked(workingRecipe.seed * 1103515245 + 12345 + presetHash);
            return seed == workingRecipe.seed ? seed + 1 : seed;
        }

        private void AddLayer()
        {
            Mutate("Add Retro VFX Layer", () =>
            {
                workingRecipe.layers.Add(new RetroVfxLayer
                {
                    name = "New Burst",
                    kind = RetroVfxLayerKind.Burst,
                    startColor = workingRecipe.primaryColor,
                    endColor = new Color(workingRecipe.primaryColor.r, workingRecipe.primaryColor.g, workingRecipe.primaryColor.b, 0f)
                });
            });
            expandedLayers.Add(workingRecipe.layers.Count - 1);
        }

        private void LoadRecipe(RetroVfxRecipe recipe)
        {
            if (recipe == null)
            {
                NewRecipe();
                return;
            }
            sourceRecipe = recipe;
            EditorUtility.CopySerialized(recipe, workingRecipe);
            workingRecipe.hideFlags = HideFlags.HideAndDontSave;
            workingRecipe.Normalize();
            workingDirty = false;
            exportName = workingRecipe.displayName;
            libraryFamily = workingRecipe.family;
            preview?.Invalidate();
            status = "Loaded recipe “" + workingRecipe.displayName + "”. Changes remain nondestructive until Update Recipe.";
            statusType = MessageType.Info;
        }

        private void NewRecipe()
        {
            if (workingDirty && !EditorUtility.DisplayDialog(
                    "Discard unsaved Retro VFX changes?",
                    "The working recipe contains changes that have not been saved.",
                    "Discard",
                    "Cancel"))
            {
                return;
            }
            sourceRecipe = null;
            selectedPresetId = "sharp-impact";
            RetroVfxPresetFactory.Apply(selectedPresetId, workingRecipe);
            workingRecipe.hideFlags = HideFlags.HideAndDontSave;
            workingDirty = false;
            exportName = workingRecipe.displayName;
            libraryFamily = workingRecipe.family;
            preview?.Invalidate();
            status = "Started a fresh Sharp Impact recipe.";
            statusType = MessageType.None;
        }

        private void SaveRecipe()
        {
            try
            {
                sourceRecipe = RetroVfxExportService.SaveRecipe(workingRecipe, sourceRecipe, outputFolder, exportName);
                workingDirty = false;
                status = "Saved recipe “" + sourceRecipe.name + "”.";
                statusType = MessageType.Info;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                status = "Could not save recipe: " + exception.Message;
                statusType = MessageType.Error;
            }
        }

        private void Export()
        {
            RetroVfxExportResult result = RetroVfxExportService.Export(
                workingRecipe,
                outputFolder.Replace('\\', '/'),
                exportName,
                outputMode,
                flipbookFrameSize,
                flipbookColumns,
                flipbookRows);
            status = result.Message;
            statusType = result.Success ? MessageType.Info : MessageType.Error;
        }

        private void ChooseOutputFolder()
        {
            string absolute = EditorUtility.OpenFolderPanel("Retro VFX Output", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(absolute))
            {
                return;
            }
            absolute = absolute.Replace('\\', '/');
            string assets = Application.dataPath.Replace('\\', '/');
            if (!absolute.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
            {
                status = "Choose a folder inside this project's Assets folder.";
                statusType = MessageType.Error;
                return;
            }
            outputFolder = "Assets" + absolute.Substring(assets.Length);
            status = "Output folder set to " + outputFolder + ".";
            statusType = MessageType.None;
        }

        private void ApplyPalette(Color primary, Color secondary)
        {
            Color oldPrimary = workingRecipe.primaryColor;
            Color oldSecondary = workingRecipe.secondaryColor;
            Mutate("Apply Retro VFX Palette", () =>
            {
                workingRecipe.primaryColor = primary;
                workingRecipe.secondaryColor = secondary;
                RecolorMatchingLayers(oldPrimary, oldSecondary, primary, secondary);
            });
        }

        private void RecolorMatchingLayers(Color oldPrimary, Color oldSecondary, Color primary, Color secondary)
        {
            foreach (RetroVfxLayer layer in workingRecipe.layers)
            {
                if (SimilarRgb(layer.startColor, oldPrimary))
                {
                    float alpha = layer.startColor.a;
                    layer.startColor = new Color(primary.r, primary.g, primary.b, alpha);
                }
                else if (SimilarRgb(layer.startColor, oldSecondary))
                {
                    float alpha = layer.startColor.a;
                    layer.startColor = new Color(secondary.r, secondary.g, secondary.b, alpha);
                }
                if (SimilarRgb(layer.endColor, oldPrimary))
                {
                    float alpha = layer.endColor.a;
                    layer.endColor = new Color(primary.r, primary.g, primary.b, alpha);
                }
                else if (SimilarRgb(layer.endColor, oldSecondary))
                {
                    float alpha = layer.endColor.a;
                    layer.endColor = new Color(secondary.r, secondary.g, secondary.b, alpha);
                }
            }
        }

        private void Mutate(string undoName, Action change)
        {
            Undo.RecordObject(workingRecipe, undoName);
            change();
            workingRecipe.Normalize();
            EditorUtility.SetDirty(workingRecipe);
            workingDirty = true;
            preview?.Invalidate();
            Repaint();
        }

        private void OnUndoRedo()
        {
            workingRecipe?.Normalize();
            workingDirty = true;
            preview?.Invalidate();
            Repaint();
        }

        private void HandleShortcuts()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
            {
                return;
            }
            if (current.keyCode == KeyCode.Space)
            {
                preview?.TogglePlayback(workingRecipe);
                current.Use();
            }
            else if (current.keyCode == KeyCode.Escape)
            {
                preview?.Stop();
                current.Use();
            }
            else if (current.keyCode == KeyCode.R && !current.control && !current.command)
            {
                GenerateVariation();
                current.Use();
            }
            else if (current.keyCode == KeyCode.S && (current.control || current.command))
            {
                SaveRecipe();
                current.Use();
            }
        }

        private void ReindexExpanded(int at, int delta)
        {
            HashSet<int> remapped = new HashSet<int>();
            foreach (int index in expandedLayers)
            {
                if (delta < 0 && index == at)
                {
                    continue;
                }
                remapped.Add(index >= at ? Mathf.Max(0, index + delta) : index);
            }
            expandedLayers.Clear();
            foreach (int index in remapped)
            {
                expandedLayers.Add(index);
            }
        }

        private void DrawDisabled()
        {
            GUILayout.BeginArea(new Rect(18f, 18f, position.width - 36f, position.height - 36f));
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(RetroVfxGui.HeaderStyle);
            GUILayout.Label("RETRO VFX IS DISABLED", RetroVfxGui.TitleStyle);
            GUILayout.Label("Enable it from the Dans Toolbox Setup Wizard.", RetroVfxGui.HelpStyle);
            GUILayout.Space(8f);
            if (GUILayout.Button("OPEN SETUP WIZARD", RetroVfxGui.PrimaryStyle))
            {
                EditorApplication.ExecuteMenuItem("Tools/Dans Toolbox/Setup Wizard");
            }
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private static bool SimilarRgb(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) < 0.02f &&
                   Mathf.Abs(left.g - right.g) < 0.02f &&
                   Mathf.Abs(left.b - right.b) < 0.02f;
        }

        private static string FamilyLabel(RetroVfxEffectFamily family)
        {
            return family switch
            {
                RetroVfxEffectFamily.MuzzleFlash => "GUNFIRE",
                RetroVfxEffectFamily.EnergyBurst => "ENERGY",
                RetroVfxEffectFamily.SwordSwing => "SWORDS",
                RetroVfxEffectFamily.ItemShine => "ITEM SHINE",
                RetroVfxEffectFamily.Environment => "WORLD",
                _ => family.ToString().ToUpperInvariant()
            };
        }
    }
}
