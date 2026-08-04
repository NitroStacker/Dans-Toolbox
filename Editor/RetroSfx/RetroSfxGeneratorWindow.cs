using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using DansToolbox.Editor;

namespace DansToolbox.EditorTools.Audio
{
    internal sealed class RetroSfxGeneratorWindow : EditorWindow
    {
        private enum ToolTab
        {
            Synth,
            Import,
            Effects
        }

        private enum AudioSourceMode
        {
            Synth,
            Imported
        }

        private enum ImportWaveformHandle
        {
            None,
            TrimStart,
            FadeIn,
            FadeOut,
            TrimEnd
        }

        private const string DefaultOutputFolder = "Assets/Audio/GeneratedSfx";
        private const string DefaultExportName = "retro_sfx";
        // Unity's Editor preview mixer does not reliably output runtime-created clips.
        private const string PreviewAssetPath = DansToolboxTransientAssets.RetroSfxPreviewPath;
        private const float MinimumTime = 0f;
        private const float MaximumEnvelopeTime = 2f;
        private const float MinimumFrequency = 20f;
        private const float MaximumFrequency = 4000f;
        private const float MaximumSlide = 8000f;
        private const float MaximumVibratoRate = 30f;
        private const float MaximumRepeatRate = 40f;
        private const float MaximumArpeggioOffset = 24f;

        private RetroSfxSettings settings = new RetroSfxSettings();
        [SerializeField] private RetroSfxImportedAudioSettings importedAudio =
            new RetroSfxImportedAudioSettings();
        private string outputFolder = DefaultOutputFolder;
        private string exportName = DefaultExportName;
        [SerializeField] private ToolTab activeTab;
        [SerializeField] private AudioSourceMode sourceMode;
        [SerializeField] private bool effectsChainExpanded = true;
        private Vector2 synthScrollPosition;
        private Vector2 importScrollPosition;
        private Vector2 effectsScrollPosition;
        private AudioClip previewClip;
        private float[] drySamples;
        private float[] generatedSamples;
        private readonly List<float[]> effectStageSamples = new List<float[]>();
        private float[] importedOverviewMinimums = Array.Empty<float>();
        private float[] importedOverviewMaximums = Array.Empty<float>();
        private int importedOverviewClipId;
        private AudioDataLoadState importedOverviewLoadState;
        private string importedClipError = string.Empty;
        private bool importedDurationLimited;
        private float generatedPeakAmplitude;
        private int generatedSettingsHash = int.MinValue;
        private string statusMessage = "Select a sound family or shape a patch, then audition or render it.";
        private double previewStartedAt;
        private double nextPreviewRepaintAt;
        private double nextHoverRepaintAt;
        private int previewAssetSettingsHash = int.MinValue;
        private bool previewIsActive;
        [NonSerialized] private ReorderableList effectList;
        [NonSerialized] private int pendingEffectRemoval = -1;
        [NonSerialized] private ImportWaveformHandle activeImportWaveformHandle;
        [NonSerialized] private double revealStartedAt;

        private float GeneratedDuration =>
            generatedSamples == null
                ? (sourceMode == AudioSourceMode.Synth ? settings.Duration : 0f)
                : generatedSamples.Length / (float)RetroSfxSettings.SampleRate;

        [MenuItem("Tools/Dans Toolbox/Retro SFX")]
        public static void OpenWindow()
        {
            DansToolboxToolHub.Open(DansToolboxTools.RetroSfxId);
        }

        [MenuItem("Tools/Dans Toolbox/Retro SFX", true)]
        private static bool ValidateOpenWindow()
        {
            return DansToolboxSettings.IsToolEnabled(DansToolboxTools.RetroSfxId);
        }

        private void OnEnable()
        {
            DansToolboxWindowChrome.ApplyCompactTitle(
                this,
                DansToolboxTools.RetroSfxId);
            revealStartedAt = EditorApplication.timeSinceStartup;
            wantsMouseMove = true;
            previewIsActive = false;
            settings ??= new RetroSfxSettings();
            settings.Effects ??= new List<RetroSfxEffectSettings>();
            importedAudio ??= new RetroSfxImportedAudioSettings();
            effectList = null;
            statusMessage = sourceMode == AudioSourceMode.Imported
                ? importedAudio.SourceClip == null
                    ? "Select or drop an AudioClip"
                    : $"Editing {importedAudio.SourceClip.name}"
                : "Select a family or shape a patch";
            CleanupPreviewAsset();
            EditorApplication.update += UpdatePreviewState;
            EnsureWaveformIsCurrent();
        }

        private void OnGUI()
        {
            DrawCanvas();
            HandleKeyboardShortcuts();

            int settingsHashBeforeControls = CalculateCurrentSettingsHash();
            GUILayout.BeginArea(new Rect(10f, 10f, position.width - 20f, position.height - 20f));
            DrawTabBar();

            float tabHeight = Mathf.Max(180f, position.height - 374f);
            if (activeTab == ToolTab.Synth)
            {
                synthScrollPosition = EditorGUILayout.BeginScrollView(
                    synthScrollPosition,
                    false,
                    true,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    GUIStyle.none,
                    GUILayout.Height(tabHeight));
                DrawPresetRack();
                DrawSoundDeck();
                DrawModulationRack();
                EditorGUILayout.EndScrollView();
            }
            else if (activeTab == ToolTab.Import)
            {
                importScrollPosition = EditorGUILayout.BeginScrollView(
                    importScrollPosition,
                    false,
                    true,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    GUIStyle.none,
                    GUILayout.Height(tabHeight));
                DrawImportedAudioRack();
                EditorGUILayout.EndScrollView();
            }
            else
            {
                effectsScrollPosition = EditorGUILayout.BeginScrollView(
                    effectsScrollPosition,
                    false,
                    true,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    GUIStyle.none,
                    GUILayout.Height(tabHeight));
                DrawEffectChainRack();
                EditorGUILayout.EndScrollView();
            }

            int settingsHashAfterControls = CalculateCurrentSettingsHash();
            if (settingsHashBeforeControls != settingsHashAfterControls)
            {
                generatedSettingsHash = int.MinValue;
            }
            EnsureWaveformIsCurrent();
            DrawWaveformDisplay();
            DrawExportBay();
            DrawStatusStrip();
            GUILayout.EndArea();

            if (settingsHashBeforeControls != settingsHashAfterControls)
            {
                Repaint();
            }

            if (Event.current.type == EventType.MouseMove)
            {
                double now = EditorApplication.timeSinceStartup;
                if (now >= nextHoverRepaintAt)
                {
                    nextHoverRepaintAt = now + 1d / 60d;
                    Repaint();
                }
            }

            if (DansToolboxMotion.DrawWindowReveal(
                    new Rect(0f, 0f, position.width, position.height),
                    revealStartedAt))
            {
                Repaint();
            }
        }

        private void DrawCanvas()
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(
                    new Rect(0f, 0f, position.width, position.height),
                    RetroSfxSynthGui.Canvas);
            }
        }

        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal(RetroSfxSynthGui.InsetStyle, GUILayout.Height(42f));
            bool synthSelected = GUILayout.Toggle(
                activeTab == ToolTab.Synth,
                new GUIContent("SYNTH", "Oscillator, envelope, presets, and modulation"),
                RetroSfxSynthGui.PresetButtonStyle,
                GUILayout.Width(84f));
            bool importSelected = GUILayout.Toggle(
                activeTab == ToolTab.Import,
                new GUIContent("IMPORT", "Load and non-destructively edit an AudioClip"),
                RetroSfxSynthGui.PresetButtonStyle,
                GUILayout.Width(84f));
            bool effectsSelected = GUILayout.Toggle(
                activeTab == ToolTab.Effects,
                new GUIContent("EFFECTS", "Reorderable processing chain for the active source"),
                RetroSfxSynthGui.PresetButtonStyle,
                GUILayout.Width(84f));

            if (synthSelected && activeTab != ToolTab.Synth)
            {
                activeTab = ToolTab.Synth;
                SetSourceMode(AudioSourceMode.Synth);
            }
            else if (importSelected && activeTab != ToolTab.Import)
            {
                activeTab = ToolTab.Import;
                SetSourceMode(AudioSourceMode.Imported);
            }
            else if (effectsSelected && activeTab != ToolTab.Effects)
            {
                activeTab = ToolTab.Effects;
            }

            GUILayout.FlexibleSpace();
            if (RetroSfxSynthGui.TransportButton(
                    RetroSfxSynthGui.TransportIcon.Play,
                    "Play preview (Space)",
                    previewIsActive,
                    26f))
            {
                Preview();
            }

            GUILayout.Space(4f);
            if (RetroSfxSynthGui.TransportButton(
                    RetroSfxSynthGui.TransportIcon.Stop,
                    "Stop preview (Esc)",
                    false,
                    26f))
            {
                StopPreview();
            }

            GUILayout.Space(4f);
            if (RetroSfxSynthGui.TransportButton(
                    RetroSfxSynthGui.TransportIcon.Save,
                    "Render WAV to the selected project folder",
                    false,
                    26f))
            {
                SaveWav();
            }

            GUILayout.Space(4f);
            if (RetroSfxSynthGui.TransportButton(
                    RetroSfxSynthGui.TransportIcon.Reset,
                    "Reset every parameter",
                    false,
                    26f))
            {
                ResetSettings();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawImportedAudioRack()
        {
            GUILayout.BeginVertical(RetroSfxSynthGui.PanelStyle);
            GUILayout.Label("SOURCE AUDIO", RetroSfxSynthGui.SectionTitleStyle);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            AudioClip selectedClip = (AudioClip)EditorGUILayout.ObjectField(
                importedAudio.SourceClip,
                typeof(AudioClip),
                false,
                GUILayout.Height(22f));
            if (EditorGUI.EndChangeCheck())
            {
                SetImportedClip(selectedClip);
            }

            GUILayout.Space(6f);
            AudioClip projectSelection = Selection.activeObject as AudioClip;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = projectSelection != null && projectSelection != importedAudio.SourceClip;
            if (GUILayout.Button(
                    new GUIContent("USE SELECTED", "Load the AudioClip selected in the Project window"),
                    RetroSfxSynthGui.PresetButtonStyle,
                    GUILayout.Width(104f),
                    GUILayout.Height(22f)))
            {
                SetImportedClip(projectSelection);
            }
            GUI.enabled = previousEnabled;

            if (GUILayout.Button(
                    new GUIContent("CLEAR", "Unload the source clip"),
                    RetroSfxSynthGui.PresetButtonStyle,
                    GUILayout.Width(58f),
                    GUILayout.Height(22f)))
            {
                SetImportedClip(null);
            }
            GUILayout.EndHorizontal();

            if (importedAudio.SourceClip == null)
            {
                GUILayout.Space(6f);
                GUILayout.BeginHorizontal(RetroSfxSynthGui.InsetStyle, GUILayout.Height(68f));
                GUILayout.Label(
                    "DROP AN AUDIOCLIP ABOVE  ·  edits are non-destructive and render to a new WAV",
                    RetroSfxSynthGui.StatusStyle);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                return;
            }

            AudioClip clip = importedAudio.SourceClip;
            GUILayout.Space(5f);
            GUILayout.Label(
                $"{clip.name}  ·  {clip.length:0.000} s  ·  {clip.frequency / 1000f:0.0} kHz  ·  " +
                $"{clip.channels} CH  ·  {clip.loadState.ToString().ToUpperInvariant()}",
                RetroSfxSynthGui.HelpStyle);
            GUILayout.EndVertical();

            EnsureImportedOverviewIsCurrent();
            if (importedOverviewMinimums.Length == 0)
            {
                if (string.IsNullOrEmpty(importedClipError))
                {
                    importedClipError = "Unity could not build a waveform overview for this clip.";
                }
                DrawImportedClipError();
                return;
            }

            DrawImportedWaveformEditor();
            DrawImportedShapingDeck();
            DrawImportedEnvelopeDeck();
            NormalizeImportedAudioSettings();
        }

        private void DrawImportedClipError()
        {
            GUILayout.BeginVertical(RetroSfxSynthGui.PanelStyle);
            GUILayout.Label("CLIP DATA UNAVAILABLE", RetroSfxSynthGui.SectionTitleStyle);
            GUILayout.Space(4f);
            GUILayout.Label(importedClipError, RetroSfxSynthGui.HelpStyle);
            GUILayout.Space(6f);
            if (CanRepairImportedClip() &&
                GUILayout.Button(
                    new GUIContent(
                        "MAKE PCM READABLE",
                        "Set this clip to Decompress On Load, then reimport it"),
                    RetroSfxSynthGui.PrimaryButtonStyle,
                    GUILayout.Width(154f),
                    GUILayout.Height(28f)))
            {
                MakeImportedClipReadable();
            }
            GUILayout.EndVertical();
        }

        private void DrawImportedWaveformEditor()
        {
            GUILayout.BeginVertical(RetroSfxSynthGui.PanelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("TRIM + FADES", RetroSfxSynthGui.SectionTitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                importedDurationLimited
                    ? $"OUTPUT {GeneratedDuration:0.000} s  ·  CAPPED AT {RetroSfxImportedAudioProcessor.MaximumOutputDuration:0} s"
                    : $"OUTPUT {GetImportedEditedDuration():0.000} s",
                RetroSfxSynthGui.TinyStyle,
                GUILayout.Width(184f));
            GUILayout.EndHorizontal();

            Rect waveformRect = GUILayoutUtility.GetRect(
                100f,
                126f,
                GUILayout.ExpandWidth(true));
            DrawImportedWaveform(waveformRect);

            AudioClip clip = importedAudio.SourceClip;
            Rect trimControls = GUILayoutUtility.GetRect(
                100f,
                42f,
                GUILayout.ExpandWidth(true));
            const float gap = 12f;
            float width = (trimControls.width - gap) * 0.5f;
            Rect trimInRect = new Rect(trimControls.x, trimControls.y, width, trimControls.height);
            Rect trimOutRect = new Rect(trimControls.x + width + gap, trimControls.y, width, trimControls.height);
            float trimInSeconds = RetroSfxSynthGui.EffectSlider(
                trimInRect,
                "import-trim-in",
                "Trim In",
                importedAudio.TrimStart * clip.length,
                0f,
                clip.length,
                "s",
                "Start time in the source clip");
            float trimOutSeconds = RetroSfxSynthGui.EffectSlider(
                trimOutRect,
                "import-trim-out",
                "Trim Out",
                importedAudio.TrimEnd * clip.length,
                0f,
                clip.length,
                "s",
                "End time in the source clip");
            float minimumGap = 1f / Mathf.Max(1, clip.samples);
            importedAudio.TrimStart = Mathf.Clamp(
                trimInSeconds / Mathf.Max(0.0001f, clip.length),
                0f,
                importedAudio.TrimEnd - minimumGap);
            importedAudio.TrimEnd = Mathf.Clamp(
                trimOutSeconds / Mathf.Max(0.0001f, clip.length),
                importedAudio.TrimStart + minimumGap,
                1f);
            GUILayout.EndVertical();
        }

        private void DrawImportedWaveform(Rect rect)
        {
            AudioClip clip = importedAudio.SourceClip;
            if (clip == null)
            {
                return;
            }

            Rect dataRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
            float trimStartX = Mathf.Lerp(dataRect.x, dataRect.xMax, importedAudio.TrimStart);
            float trimEndX = Mathf.Lerp(dataRect.x, dataRect.xMax, importedAudio.TrimEnd);
            float pitchRatio = Mathf.Pow(2f, importedAudio.PitchSemitones / 12f);
            float fadeInNormalized = importedAudio.FadeIn * pitchRatio / Mathf.Max(0.0001f, clip.length);
            float fadeOutNormalized = importedAudio.FadeOut * pitchRatio / Mathf.Max(0.0001f, clip.length);
            float fadeInX = Mathf.Lerp(
                dataRect.x,
                dataRect.xMax,
                Mathf.Clamp(importedAudio.TrimStart + fadeInNormalized, importedAudio.TrimStart, importedAudio.TrimEnd));
            float fadeOutX = Mathf.Lerp(
                dataRect.x,
                dataRect.xMax,
                Mathf.Clamp(importedAudio.TrimEnd - fadeOutNormalized, importedAudio.TrimStart, importedAudio.TrimEnd));

            int controlId = GUIUtility.GetControlID(
                "retro-sfx-import-waveform".GetHashCode(),
                FocusType.Passive,
                rect);
            Event currentEvent = Event.current;
            switch (currentEvent.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (currentEvent.button == 0 && dataRect.Contains(currentEvent.mousePosition))
                    {
                        activeImportWaveformHandle = PickImportedWaveformHandle(
                            currentEvent.mousePosition,
                            dataRect,
                            trimStartX,
                            fadeInX,
                            fadeOutX,
                            trimEndX);
                        if (activeImportWaveformHandle != ImportWaveformHandle.None)
                        {
                            GUIUtility.hotControl = controlId;
                            currentEvent.Use();
                        }
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        float normalized = Mathf.InverseLerp(dataRect.x, dataRect.xMax, currentEvent.mousePosition.x);
                        float minimumGap = 1f / Mathf.Max(1, clip.samples);
                        switch (activeImportWaveformHandle)
                        {
                            case ImportWaveformHandle.TrimStart:
                                importedAudio.TrimStart = Mathf.Clamp(
                                    normalized,
                                    0f,
                                    importedAudio.TrimEnd - minimumGap);
                                break;
                            case ImportWaveformHandle.TrimEnd:
                                importedAudio.TrimEnd = Mathf.Clamp(
                                    normalized,
                                    importedAudio.TrimStart + minimumGap,
                                    1f);
                                break;
                            case ImportWaveformHandle.FadeIn:
                                importedAudio.FadeIn =
                                    Mathf.Max(0f, normalized - importedAudio.TrimStart) *
                                    clip.length / pitchRatio;
                                break;
                            case ImportWaveformHandle.FadeOut:
                                importedAudio.FadeOut =
                                    Mathf.Max(0f, importedAudio.TrimEnd - normalized) *
                                    clip.length / pitchRatio;
                                break;
                        }
                        GUI.changed = true;
                        currentEvent.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        activeImportWaveformHandle = ImportWaveformHandle.None;
                        currentEvent.Use();
                    }
                    break;
            }

            if (currentEvent.type != EventType.Repaint)
            {
                return;
            }

            RetroSfxSynthGui.DrawWaveformGrid(rect);
            float centerY = dataRect.center.y;
            float halfHeight = dataRect.height * 0.42f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(dataRect.width));
            for (int column = 0; column < columns; column++)
            {
                float normalized = column / Mathf.Max(1f, columns - 1f);
                int overviewIndex = Mathf.Clamp(
                    Mathf.RoundToInt(normalized * (importedOverviewMinimums.Length - 1)),
                    0,
                    importedOverviewMinimums.Length - 1);
                float minimum = importedOverviewMinimums[overviewIndex];
                float maximum = importedOverviewMaximums[overviewIndex];
                float top = centerY - maximum * halfHeight;
                float bottom = centerY - minimum * halfHeight;
                bool selected = normalized >= importedAudio.TrimStart && normalized <= importedAudio.TrimEnd;
                Color color = selected
                    ? RetroSfxSynthGui.Signal
                    : new Color(
                        RetroSfxSynthGui.MutedText.r,
                        RetroSfxSynthGui.MutedText.g,
                        RetroSfxSynthGui.MutedText.b,
                        0.55f);
                EditorGUI.DrawRect(
                    new Rect(dataRect.x + column, top, 1f, Mathf.Max(1f, bottom - top)),
                    color);
            }

            Color shade = new Color(0f, 0f, 0f, 0.5f);
            EditorGUI.DrawRect(
                new Rect(dataRect.x, dataRect.y, Mathf.Max(0f, trimStartX - dataRect.x), dataRect.height),
                shade);
            EditorGUI.DrawRect(
                new Rect(trimEndX, dataRect.y, Mathf.Max(0f, dataRect.xMax - trimEndX), dataRect.height),
                shade);
            DrawImportedFadeLines(dataRect, trimStartX, fadeInX, fadeOutX, trimEndX);
            EditorGUI.DrawRect(new Rect(Mathf.Round(trimStartX) - 1f, dataRect.y, 2f, dataRect.height), Color.white);
            EditorGUI.DrawRect(new Rect(Mathf.Round(trimEndX) - 1f, dataRect.y, 2f, dataRect.height), Color.white);
            EditorGUI.DrawRect(new Rect(Mathf.Round(fadeInX) - 3f, dataRect.y + 16f, 6f, 6f), RetroSfxSynthGui.Accent);
            EditorGUI.DrawRect(new Rect(Mathf.Round(fadeOutX) - 3f, dataRect.y + 16f, 6f, 6f), RetroSfxSynthGui.Accent);

            GUI.Label(
                new Rect(dataRect.x + 6f, dataRect.y + 4f, 100f, 16f),
                $"{importedAudio.TrimStart * clip.length:0.000} s",
                RetroSfxSynthGui.TinyStyle);
            GUI.Label(
                new Rect(dataRect.xMax - 106f, dataRect.y + 4f, 100f, 16f),
                $"{importedAudio.TrimEnd * clip.length:0.000} s",
                RetroSfxSynthGui.RightTinyStyle);
        }

        private static ImportWaveformHandle PickImportedWaveformHandle(
            Vector2 mousePosition,
            Rect dataRect,
            float trimStartX,
            float fadeInX,
            float fadeOutX,
            float trimEndX)
        {
            if (mousePosition.y <= dataRect.y + 14f)
            {
                if (Mathf.Abs(mousePosition.x - trimStartX) <= 9f)
                {
                    return ImportWaveformHandle.TrimStart;
                }
                if (Mathf.Abs(mousePosition.x - trimEndX) <= 9f)
                {
                    return ImportWaveformHandle.TrimEnd;
                }
            }

            if (Mathf.Abs(mousePosition.x - fadeInX) <= 9f)
            {
                return ImportWaveformHandle.FadeIn;
            }
            if (Mathf.Abs(mousePosition.x - fadeOutX) <= 9f)
            {
                return ImportWaveformHandle.FadeOut;
            }
            if (Mathf.Abs(mousePosition.x - trimStartX) <= 9f)
            {
                return ImportWaveformHandle.TrimStart;
            }
            if (Mathf.Abs(mousePosition.x - trimEndX) <= 9f)
            {
                return ImportWaveformHandle.TrimEnd;
            }
            return ImportWaveformHandle.None;
        }

        private static void DrawImportedFadeLines(
            Rect rect,
            float trimStartX,
            float fadeInX,
            float fadeOutX,
            float trimEndX)
        {
            Handles.BeginGUI();
            Handles.color = RetroSfxSynthGui.Accent;
            Handles.DrawLine(
                new Vector3(trimStartX, rect.yMax - 5f),
                new Vector3(fadeInX, rect.y + 20f));
            Handles.DrawLine(
                new Vector3(fadeOutX, rect.y + 20f),
                new Vector3(trimEndX, rect.yMax - 5f));
            Handles.EndGUI();
        }

        private void DrawImportedShapingDeck()
        {
            float duration = Mathf.Max(0.01f, GetImportedEditedDuration());
            float maximumFade = Mathf.Min(5f, duration);
            GUILayout.BeginVertical(RetroSfxSynthGui.PanelStyle);
            GUILayout.Label("CLIP SHAPING", RetroSfxSynthGui.SectionTitleStyle);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            importedAudio.PitchSemitones = RetroSfxSynthGui.Knob(
                "import-pitch",
                "Pitch",
                importedAudio.PitchSemitones,
                -24f,
                24f,
                0f,
                "st",
                "Playback pitch in semitones",
                112f);
            GUILayout.FlexibleSpace();
            importedAudio.GainDecibels = RetroSfxSynthGui.Knob(
                "import-gain",
                "Gain",
                importedAudio.GainDecibels,
                -60f,
                12f,
                0f,
                "dB",
                "Pre-effects clip gain",
                112f);
            GUILayout.FlexibleSpace();
            importedAudio.FadeIn = RetroSfxSynthGui.Knob(
                "import-fade-in",
                "Fade In",
                importedAudio.FadeIn,
                0f,
                maximumFade,
                0f,
                "s",
                "Linear fade at the trimmed start",
                112f);
            GUILayout.FlexibleSpace();
            importedAudio.FadeOut = RetroSfxSynthGui.Knob(
                "import-fade-out",
                "Fade Out",
                importedAudio.FadeOut,
                0f,
                maximumFade,
                0f,
                "s",
                "Linear fade at the trimmed end",
                112f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawImportedEnvelopeDeck()
        {
            float duration = Mathf.Max(0.01f, GetImportedEditedDuration());
            float maximumStageTime = Mathf.Min(5f, duration);
            GUILayout.BeginVertical(RetroSfxSynthGui.PanelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("AMPLITUDE ENVELOPE", RetroSfxSynthGui.SectionTitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("POST-TRIM  ·  PRE-FX", RetroSfxSynthGui.TinyStyle, GUILayout.Width(108f));
            GUILayout.EndHorizontal();

            Rect graphRect = GUILayoutUtility.GetRect(100f, 72f, GUILayout.ExpandWidth(true));
            DrawImportedEnvelopeGraph(graphRect, duration);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            importedAudio.EnvelopeAttack = RetroSfxSynthGui.Knob(
                "import-envelope-attack",
                "Attack",
                importedAudio.EnvelopeAttack,
                0f,
                maximumStageTime,
                0f,
                "s",
                "Envelope rise time",
                112f);
            GUILayout.FlexibleSpace();
            importedAudio.EnvelopeDecay = RetroSfxSynthGui.Knob(
                "import-envelope-decay",
                "Decay",
                importedAudio.EnvelopeDecay,
                0f,
                maximumStageTime,
                0f,
                "s",
                "Time to reach the sustain level",
                112f);
            GUILayout.FlexibleSpace();
            importedAudio.EnvelopeSustain = RetroSfxSynthGui.Knob(
                "import-envelope-sustain",
                "Sustain",
                importedAudio.EnvelopeSustain,
                0f,
                1f,
                1f,
                "%",
                "Level held after decay",
                112f);
            GUILayout.FlexibleSpace();
            importedAudio.EnvelopeRelease = RetroSfxSynthGui.Knob(
                "import-envelope-release",
                "Release",
                importedAudio.EnvelopeRelease,
                0f,
                maximumStageTime,
                0f,
                "s",
                "Envelope fall time at the trimmed end",
                112f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawImportedEnvelopeGraph(Rect rect, float duration)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            RetroSfxSynthGui.DrawWaveformGrid(rect);
            Rect graph = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f);
            float attackX = Mathf.Lerp(
                graph.x,
                graph.xMax,
                Mathf.Clamp01(importedAudio.EnvelopeAttack / duration));
            float decayX = Mathf.Lerp(
                graph.x,
                graph.xMax,
                Mathf.Clamp01((importedAudio.EnvelopeAttack + importedAudio.EnvelopeDecay) / duration));
            float releaseX = Mathf.Lerp(
                graph.x,
                graph.xMax,
                Mathf.Clamp01((duration - importedAudio.EnvelopeRelease) / duration));
            float sustainY = Mathf.Lerp(graph.yMax, graph.y, importedAudio.EnvelopeSustain);

            Handles.BeginGUI();
            Handles.color = RetroSfxSynthGui.Signal;
            Handles.DrawLine(new Vector3(graph.x, graph.yMax), new Vector3(attackX, graph.y));
            Handles.DrawLine(new Vector3(attackX, graph.y), new Vector3(decayX, sustainY));
            Handles.DrawLine(new Vector3(decayX, sustainY), new Vector3(releaseX, sustainY));
            Handles.DrawLine(new Vector3(releaseX, sustainY), new Vector3(graph.xMax, graph.yMax));
            Handles.EndGUI();
        }

        private void NormalizeImportedAudioSettings()
        {
            AudioClip clip = importedAudio.SourceClip;
            if (clip == null)
            {
                return;
            }

            float minimumGap = 1f / Mathf.Max(1, clip.samples);
            importedAudio.TrimStart = Mathf.Clamp(importedAudio.TrimStart, 0f, 1f - minimumGap);
            importedAudio.TrimEnd = Mathf.Clamp(importedAudio.TrimEnd, importedAudio.TrimStart + minimumGap, 1f);
            float duration = Mathf.Max(0f, GetImportedEditedDuration());
            importedAudio.FadeIn = Mathf.Clamp(importedAudio.FadeIn, 0f, duration);
            importedAudio.FadeOut = Mathf.Clamp(importedAudio.FadeOut, 0f, duration);
            importedAudio.EnvelopeAttack = Mathf.Clamp(importedAudio.EnvelopeAttack, 0f, duration);
            importedAudio.EnvelopeDecay = Mathf.Clamp(importedAudio.EnvelopeDecay, 0f, duration);
            importedAudio.EnvelopeSustain = Mathf.Clamp01(importedAudio.EnvelopeSustain);
            importedAudio.EnvelopeRelease = Mathf.Clamp(importedAudio.EnvelopeRelease, 0f, duration);
            importedAudio.PitchSemitones = Mathf.Clamp(importedAudio.PitchSemitones, -24f, 24f);
            importedAudio.GainDecibels = Mathf.Clamp(importedAudio.GainDecibels, -60f, 12f);
        }

        private float GetImportedEditedDuration()
        {
            AudioClip clip = importedAudio.SourceClip;
            if (clip == null)
            {
                return 0f;
            }

            float sourceDuration = clip.length * Mathf.Max(0f, importedAudio.TrimEnd - importedAudio.TrimStart);
            float pitchRatio = Mathf.Pow(2f, importedAudio.PitchSemitones / 12f);
            return Mathf.Min(
                RetroSfxImportedAudioProcessor.MaximumOutputDuration,
                sourceDuration / Mathf.Max(0.0001f, pitchRatio));
        }

        private void SetSourceMode(AudioSourceMode mode)
        {
            if (sourceMode == mode)
            {
                return;
            }

            StopPreview();
            sourceMode = mode;
            generatedSettingsHash = int.MinValue;
            EnsureWaveformIsCurrent();
            statusMessage = mode == AudioSourceMode.Synth
                ? "SOURCE  ·  synthesizer"
                : importedAudio.SourceClip == null
                    ? "SOURCE  ·  select or drop an AudioClip"
                    : $"SOURCE  ·  {importedAudio.SourceClip.name}";
            Repaint();
        }

        private void SetImportedClip(AudioClip clip)
        {
            if (importedAudio.SourceClip == clip)
            {
                return;
            }

            StopPreview();
            importedAudio.SourceClip = clip;
            importedAudio.ResetEdits();
            sourceMode = AudioSourceMode.Imported;
            importedOverviewClipId = 0;
            importedOverviewMinimums = Array.Empty<float>();
            importedOverviewMaximums = Array.Empty<float>();
            importedClipError = string.Empty;
            importedDurationLimited = false;
            generatedSettingsHash = int.MinValue;
            if (clip != null)
            {
                exportName = SanitizeFileName($"{clip.name}_edit");
            }
            EnsureImportedOverviewIsCurrent();
            EnsureWaveformIsCurrent();
            statusMessage = clip == null
                ? "IMPORT  ·  source clip cleared"
                : $"IMPORT  ·  {clip.name} loaded";
            Repaint();
        }

        private void EnsureImportedOverviewIsCurrent()
        {
            AudioClip clip = importedAudio.SourceClip;
            if (clip == null)
            {
                importedOverviewClipId = 0;
                importedOverviewMinimums = Array.Empty<float>();
                importedOverviewMaximums = Array.Empty<float>();
                importedClipError = string.Empty;
                return;
            }

            int clipId = clip.GetInstanceID();
            if (importedOverviewClipId == clipId &&
                importedOverviewLoadState == clip.loadState &&
                importedOverviewMinimums.Length > 0)
            {
                return;
            }

            importedOverviewClipId = clipId;
            importedOverviewLoadState = clip.loadState;
            if (!RetroSfxImportedAudioProcessor.TryBuildOverview(
                    clip,
                    out importedOverviewMinimums,
                    out importedOverviewMaximums,
                    out string error))
            {
                importedClipError = error;
                return;
            }
            importedClipError = string.Empty;
        }

        private bool CanRepairImportedClip()
        {
            AudioClip clip = importedAudio.SourceClip;
            return clip != null &&
                AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) is AudioImporter;
        }

        private void MakeImportedClipReadable()
        {
            AudioClip clip = importedAudio.SourceClip;
            string assetPath = clip == null ? string.Empty : AssetDatabase.GetAssetPath(clip);
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
            {
                statusMessage = "The selected clip is not backed by an editable AudioImporter.";
                return;
            }

            AudioImporterSampleSettings sampleSettings = importer.defaultSampleSettings;
            sampleSettings.loadType = AudioClipLoadType.DecompressOnLoad;
            sampleSettings.preloadAudioData = true;
            importer.defaultSampleSettings = sampleSettings;
            importer.loadInBackground = false;
            importer.SaveAndReimport();

            importedAudio.SourceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            importedOverviewClipId = 0;
            generatedSettingsHash = int.MinValue;
            EnsureImportedOverviewIsCurrent();
            EnsureWaveformIsCurrent();
            statusMessage = $"IMPORT  ·  {importedAudio.SourceClip.name} is now PCM-readable";
            Repaint();
        }

        private void DrawPresetRack()
        {
            GUILayout.BeginVertical(RetroSfxSynthGui.PanelStyle);
            GUILayout.Label("SOUND FAMILY  ·  CLICK AGAIN TO REROLL", RetroSfxSynthGui.SectionTitleStyle);
            GUILayout.Space(3f);

            GUILayout.BeginHorizontal();
            if (RetroSfxSynthGui.PresetButton("PICKUP / COIN", "Bright pickup and coin variations"))
                ApplyPreset(RetroSfxPreset.Coin);
            if (RetroSfxSynthGui.PresetButton("LASER", "Laser and shoot variations"))
                ApplyPreset(RetroSfxPreset.Laser);
            if (RetroSfxSynthGui.PresetButton("EXPLOSION", "Noise-based explosion variations"))
                ApplyPreset(RetroSfxPreset.Explosion);
            if (RetroSfxSynthGui.PresetButton("POWER UP", "Rising power-up variations"))
                ApplyPreset(RetroSfxPreset.PowerUp);
            if (RetroSfxSynthGui.PresetButton("HIT / HURT", "Impact and damage variations"))
                ApplyPreset(RetroSfxPreset.Hit);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (RetroSfxSynthGui.PresetButton("JUMP", "Rising jump variations"))
                ApplyPreset(RetroSfxPreset.Jump);
            if (RetroSfxSynthGui.PresetButton("CLICK", "Short bright click variations"))
                ApplyPreset(RetroSfxPreset.Click);
            if (RetroSfxSynthGui.PresetButton("BLIP / SELECT", "UI blip and selection variations"))
                ApplyPreset(RetroSfxPreset.BlipSelect);
            if (RetroSfxSynthGui.PresetButton("SYNTH", "Tonal synthesizer variations"))
                ApplyPreset(RetroSfxPreset.Synth);
            if (RetroSfxSynthGui.PresetButton("RANDOM", "Fully random patch"))
                ApplyPreset(RetroSfxPreset.Random);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawSoundDeck()
        {
            float availableWidth = Mathf.Max(584f, position.width - 36f);
            float panelWidth = (availableWidth - 8f) * 0.5f;
            GUILayout.BeginHorizontal();
            DrawOscillatorPanel(panelWidth);
            GUILayout.Space(8f);
            DrawEnvelopePanel(panelWidth);
            GUILayout.EndHorizontal();
        }

        private void DrawOscillatorPanel(float panelWidth)
        {
            GUILayout.BeginVertical(
                RetroSfxSynthGui.PanelStyle,
                GUILayout.Width(panelWidth),
                GUILayout.Height(196f),
                GUILayout.ExpandWidth(false));
            GUILayout.Label("OSCILLATOR + PITCH", RetroSfxSynthGui.SectionTitleStyle);
            GUILayout.Space(3f);

            GUILayout.BeginHorizontal();
            DrawWaveButton(RetroWaveType.Square);
            DrawWaveButton(RetroWaveType.Saw);
            DrawWaveButton(RetroWaveType.Sine);
            DrawWaveButton(RetroWaveType.Noise);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            settings.StartFrequency = RetroSfxSynthGui.Knob(
                "start-frequency",
                "Tune",
                settings.StartFrequency,
                MinimumFrequency,
                MaximumFrequency,
                440f,
                "Hz",
                "Starting oscillator frequency",
                53f);
            GUILayout.FlexibleSpace();
            settings.FrequencySlide = RetroSfxSynthGui.Knob(
                "frequency-slide",
                "Slide",
                settings.FrequencySlide,
                -MaximumSlide,
                MaximumSlide,
                0f,
                "Hz/s",
                "Frequency movement over time",
                53f);
            GUILayout.FlexibleSpace();
            settings.DutyCycle = RetroSfxSynthGui.Knob(
                "duty-cycle",
                "Duty",
                settings.DutyCycle,
                0.05f,
                0.95f,
                0.5f,
                "%",
                "Square-wave pulse width",
                53f);
            GUILayout.FlexibleSpace();
            settings.ArpeggioOffset = RetroSfxSynthGui.Knob(
                "arpeggio-offset",
                "Arp",
                settings.ArpeggioOffset,
                -MaximumArpeggioOffset,
                MaximumArpeggioOffset,
                0f,
                "st",
                "Single arpeggio pitch change",
                53f);
            GUILayout.FlexibleSpace();
            settings.ArpeggioTime = RetroSfxSynthGui.Knob(
                "arpeggio-time",
                "Arp Time",
                settings.ArpeggioTime,
                MinimumTime,
                MaximumEnvelopeTime,
                0f,
                "s",
                "Delay before the arpeggio pitch change",
                53f);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawWaveButton(RetroWaveType waveType)
        {
            if (RetroSfxSynthGui.WaveButton(waveType, settings.WaveType == waveType))
            {
                settings.WaveType = waveType;
            }
        }

        private void DrawEnvelopePanel(float panelWidth)
        {
            GUILayout.BeginVertical(
                RetroSfxSynthGui.PanelStyle,
                GUILayout.Width(panelWidth),
                GUILayout.Height(196f),
                GUILayout.ExpandWidth(false));
            GUILayout.BeginHorizontal();
            GUILayout.Label("AMPLITUDE ENVELOPE", RetroSfxSynthGui.SectionTitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{settings.Duration:0.000} s", RetroSfxSynthGui.TinyStyle);
            GUILayout.EndHorizontal();

            Rect envelopeRect = GUILayoutUtility.GetRect(100f, 60f, GUILayout.ExpandWidth(true));
            RetroSfxSynthGui.DrawEnvelopeGraph(envelopeRect, settings);
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            settings.AttackTime = RetroSfxSynthGui.Knob(
                "attack-time",
                "Attack",
                settings.AttackTime,
                MinimumTime,
                MaximumEnvelopeTime,
                0.01f,
                "s",
                "Time to reach full level",
                62f);
            GUILayout.FlexibleSpace();
            settings.SustainTime = RetroSfxSynthGui.Knob(
                "sustain-time",
                "Sustain",
                settings.SustainTime,
                MinimumTime,
                MaximumEnvelopeTime,
                0.15f,
                "s",
                "Time held at full level",
                62f);
            GUILayout.FlexibleSpace();
            settings.SustainPunch = RetroSfxSynthGui.Knob(
                "sustain-punch",
                "Punch",
                settings.SustainPunch,
                0f,
                1f,
                0f,
                "%",
                "Extra transient energy during sustain",
                62f);
            GUILayout.FlexibleSpace();
            settings.DecayTime = RetroSfxSynthGui.Knob(
                "decay-time",
                "Decay",
                settings.DecayTime,
                MinimumTime,
                MaximumEnvelopeTime,
                0.2f,
                "s",
                "Fade time after sustain",
                62f);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawModulationRack()
        {
            GUILayout.BeginVertical(RetroSfxSynthGui.PanelStyle);
            GUILayout.Label("MODULATION + CHARACTER", RetroSfxSynthGui.SectionTitleStyle);
            GUILayout.Space(3f);
            GUILayout.BeginHorizontal();
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            settings.VibratoDepth = RetroSfxSynthGui.Knob(
                "vibrato-depth",
                "Vib Depth",
                settings.VibratoDepth,
                0f,
                1f,
                0f,
                "%",
                "Vibrato pitch depth",
                64f);
            GUILayout.FlexibleSpace();
            settings.VibratoRate = RetroSfxSynthGui.Knob(
                "vibrato-rate",
                "Vib Rate",
                settings.VibratoRate,
                0f,
                MaximumVibratoRate,
                8f,
                "0.0 Hz",
                "Vibrato speed",
                64f);
            GUILayout.FlexibleSpace();
            settings.RepeatRate = RetroSfxSynthGui.Knob(
                "repeat-rate",
                "Retrigger",
                settings.RepeatRate,
                0f,
                MaximumRepeatRate,
                0f,
                "0.0 Hz",
                "Oscillator retrigger rate",
                64f);
            GUILayout.FlexibleSpace();
            settings.BitCrushAmount = RetroSfxSynthGui.Knob(
                "bit-crush",
                "Crush",
                settings.BitCrushAmount,
                0f,
                1f,
                0f,
                "%",
                "Bit-depth reduction",
                64f);
            GUILayout.FlexibleSpace();
            settings.MasterVolume = RetroSfxSynthGui.Knob(
                "master-volume",
                "Output",
                settings.MasterVolume,
                0f,
                1f,
                0.5f,
                "%",
                "Preview and export level",
                64f);

            GUILayout.EndHorizontal();
            GUILayout.Space(12f);
            Rect divider = GUILayoutUtility.GetRect(1f, 82f, GUILayout.Width(1f), GUILayout.Height(82f));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(divider, RetroSfxSynthGui.Border);
            }
            GUILayout.Space(12f);
            GUILayout.BeginVertical(GUILayout.Width(132f));
            GUILayout.Space(5f);
            GUILayout.Label("NOISE SEED", RetroSfxSynthGui.SectionTitleStyle);
            settings.Seed = EditorGUILayout.IntField(settings.Seed, RetroSfxSynthGui.FieldStyle);
            GUILayout.Space(6f);
            GUILayout.Label(
                "Repeatable noise texture.",
                RetroSfxSynthGui.HelpStyle,
                GUILayout.Height(20f));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawEffectChainRack()
        {
            GUILayout.BeginVertical(RetroSfxSynthGui.PanelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(
                    effectsChainExpanded ? "▼  EFFECT CHAIN" : "▶  EFFECT CHAIN",
                    RetroSfxSynthGui.PresetButtonStyle,
                    GUILayout.Width(142f)))
            {
                effectsChainExpanded = !effectsChainExpanded;
            }

            GUILayout.Label(
                $"{settings.Effects.Count} DEVICE{(settings.Effects.Count == 1 ? string.Empty : "S")}",
                RetroSfxSynthGui.TinyStyle,
                GUILayout.Width(74f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                    "+ ADD EFFECT",
                    RetroSfxSynthGui.PresetButtonStyle,
                    GUILayout.Width(108f)))
            {
                ShowAddEffectMenu();
            }
            GUILayout.EndHorizontal();

            if (effectsChainExpanded)
            {
                GUILayout.Space(4f);
                if (settings.Effects.Count == 0)
                {
                    GUILayout.BeginHorizontal(RetroSfxSynthGui.InsetStyle, GUILayout.Height(54f));
                    GUILayout.Label(
                        "NO EFFECTS  ·  Add a device to process the synthesized signal.",
                        RetroSfxSynthGui.StatusStyle);
                    GUILayout.EndHorizontal();
                }
                else
                {
                    EnsureEffectList();
                    effectList.DoLayoutList();
                    ApplyPendingEffectRemoval();
                }
            }

            GUILayout.EndVertical();
        }

        private void EnsureEffectList()
        {
            settings.Effects ??= new List<RetroSfxEffectSettings>();
            if (effectList != null && ReferenceEquals(effectList.list, settings.Effects))
            {
                return;
            }

            effectList = new ReorderableList(
                settings.Effects,
                typeof(RetroSfxEffectSettings),
                true,
                false,
                false,
                false)
            {
                showDefaultBackground = false,
                headerHeight = 0f,
                footerHeight = 0f,
                elementHeight = 34f,
                drawElementCallback = DrawEffectElement,
                drawElementBackgroundCallback = (rect, index, active, focused) => { },
                elementHeightCallback = index =>
                {
                    if (index < 0 || index >= settings.Effects.Count)
                    {
                        return 34f;
                    }

                    RetroSfxEffectSettings effect = settings.Effects[index];
                    return effect != null && effect.Expanded ? 164f : 34f;
                }
            };
            effectList.onReorderCallback = _ =>
            {
                generatedSettingsHash = int.MinValue;
                statusMessage = "EFFECT CHAIN  ·  device order updated";
                Repaint();
            };
        }

        private void DrawEffectElement(Rect rect, int index, bool active, bool focused)
        {
            if (index < 0 || index >= settings.Effects.Count)
            {
                return;
            }

            RetroSfxEffectSettings effect = settings.Effects[index];
            if (effect == null)
            {
                effect = RetroSfxEffectSettings.Create(RetroSfxEffectType.Filter);
                settings.Effects[index] = effect;
            }

            Rect cardRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            Rect headerRect = new Rect(cardRect.x, cardRect.y, cardRect.width, 28f);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(cardRect, RetroSfxSynthGui.PanelInset);
                RetroSfxSynthGui.DrawBorder(cardRect, RetroSfxSynthGui.Border);
                EditorGUI.DrawRect(
                    new Rect(headerRect.x + 1f, headerRect.y + 1f, headerRect.width - 2f, headerRect.height - 2f),
                    effect.Enabled ? RetroSfxSynthGui.PanelRaised : RetroSfxSynthGui.Panel);
                EditorGUI.DrawRect(
                    new Rect(headerRect.x, headerRect.yMax - 1f, headerRect.width, 1f),
                    RetroSfxSynthGui.Border);
            }

            Rect dragRect = new Rect(headerRect.x + 5f, headerRect.y, 14f, headerRect.height);
            RetroSfxSynthGui.DrawDragHandle(dragRect);

            Rect foldoutRect = new Rect(headerRect.x + 21f, headerRect.y + 5f, 18f, 18f);
            if (RetroSfxSynthGui.FoldoutButton(
                    foldoutRect,
                    effect.Expanded,
                    effect.Expanded ? "Collapse device" : "Expand device"))
            {
                effect.Expanded = !effect.Expanded;
                Repaint();
            }

            Rect bypassRect = new Rect(headerRect.x + 43f, headerRect.y + 5f, 34f, 18f);
            if (RetroSfxSynthGui.RectButton(
                    bypassRect,
                    effect.Enabled ? "ON" : "OFF",
                    "Bypass this effect",
                    effect.Enabled))
            {
                effect.Enabled = !effect.Enabled;
                GUI.changed = true;
            }

            Rect removeRect = new Rect(headerRect.xMax - 25f, headerRect.y + 5f, 20f, 18f);
            if (RetroSfxSynthGui.RectButton(
                    removeRect,
                    "×",
                    "Remove this device",
                    false,
                    true))
            {
                pendingEffectRemoval = index;
            }

            Rect orderRect = new Rect(removeRect.x - 38f, headerRect.y + 5f, 34f, 18f);
            GUI.Label(orderRect, $"#{index + 1:00}", RetroSfxSynthGui.InlineValueStyle);

            Rect nameRect = new Rect(
                bypassRect.xMax + 8f,
                headerRect.y,
                Mathf.Max(40f, orderRect.x - bypassRect.xMax - 14f),
                headerRect.height);
            GUIStyle nameStyle = RetroSfxSynthGui.EffectNameStyle;
            nameStyle.normal.textColor = effect.Enabled
                ? RetroSfxSynthGui.Text
                : RetroSfxSynthGui.MutedText;
            if (GUI.Button(
                    nameRect,
                    new GUIContent(string.Empty, "Expand or collapse this device"),
                    GUIStyle.none))
            {
                effect.Expanded = !effect.Expanded;
                Repaint();
            }
            GUI.Label(nameRect, GetEffectDisplayName(effect.Type), nameStyle);

            if (effect.Expanded)
            {
                Rect bodyRect = new Rect(
                    cardRect.x + 8f,
                    headerRect.yMax + 7f,
                    cardRect.width - 16f,
                    cardRect.height - headerRect.height - 12f);
                Rect visualizationRect = new Rect(
                    bodyRect.x,
                    bodyRect.y,
                    bodyRect.width,
                    66f);
                float[] stageInput = index == 0
                    ? drySamples
                    : GetEffectStageSamples(index - 1);
                float[] stageOutput = GetEffectStageSamples(index);
                RetroSfxEffectVisualizer.Draw(
                    visualizationRect,
                    effect,
                    stageInput,
                    stageOutput,
                    GetPreviewTime(),
                    previewIsActive);

                Rect parametersRect = new Rect(
                    bodyRect.x,
                    visualizationRect.yMax + 8f,
                    bodyRect.width,
                    Mathf.Max(38f, bodyRect.yMax - visualizationRect.yMax - 8f));
                DrawEffectParameters(parametersRect, index, effect);
            }
        }

        private float[] GetEffectStageSamples(int index)
        {
            return index >= 0 && index < effectStageSamples.Count
                ? effectStageSamples[index]
                : generatedSamples;
        }

        private float GetPreviewTime()
        {
            if (!previewIsActive || GeneratedDuration <= 0f)
            {
                return 0f;
            }

            float elapsed = (float)(EditorApplication.timeSinceStartup - previewStartedAt);
            return Mathf.Clamp(elapsed, 0f, GeneratedDuration);
        }

        private void DrawEffectParameters(
            Rect rect,
            int index,
            RetroSfxEffectSettings effect)
        {
            switch (effect.Type)
            {
                case RetroSfxEffectType.Filter:
                {
                    Rect[] cells = GetEffectParameterCells(rect, 4);
                    DrawFilterModeControl(cells[0], effect);
                    effect.ParameterA = RetroSfxSynthGui.EffectSlider(
                        cells[1], $"fx-{index}-cutoff", "Cutoff", effect.ParameterA, 30f, 18000f, "Hz", "Filter cutoff");
                    effect.ParameterB = RetroSfxSynthGui.EffectSlider(
                        cells[2], $"fx-{index}-resonance", "Resonance", effect.ParameterB, 0f, 1f, "%", "Filter resonance");
                    effect.ParameterC = RetroSfxSynthGui.EffectSlider(
                        cells[3], $"fx-{index}-filter-mix", "Mix", effect.ParameterC, 0f, 1f, "%", "Wet/dry balance");
                    break;
                }
                case RetroSfxEffectType.Equalizer:
                {
                    Rect[] cells = GetEffectParameterCells(rect, 4);
                    effect.ParameterA = RetroSfxSynthGui.EffectSlider(
                        cells[0], $"fx-{index}-eq-low", "Low", effect.ParameterA, -18f, 18f, "dB", "Low-band gain");
                    effect.ParameterB = RetroSfxSynthGui.EffectSlider(
                        cells[1], $"fx-{index}-eq-mid", "Mid", effect.ParameterB, -18f, 18f, "dB", "Mid-band gain");
                    effect.ParameterC = RetroSfxSynthGui.EffectSlider(
                        cells[2], $"fx-{index}-eq-high", "High", effect.ParameterC, -18f, 18f, "dB", "High-band gain");
                    effect.ParameterD = RetroSfxSynthGui.EffectSlider(
                        cells[3], $"fx-{index}-eq-mix", "Mix", effect.ParameterD, 0f, 1f, "%", "Wet/dry balance");
                    break;
                }
                case RetroSfxEffectType.Compressor:
                {
                    Rect[] cells = GetEffectParameterCells(rect, 5);
                    effect.ParameterA = RetroSfxSynthGui.EffectSlider(
                        cells[0], $"fx-{index}-threshold", "Threshold", effect.ParameterA, -48f, 0f, "dB", "Compression threshold");
                    effect.ParameterB = RetroSfxSynthGui.EffectSlider(
                        cells[1], $"fx-{index}-ratio", "Ratio", effect.ParameterB, 1f, 20f, "ratio", "Compression ratio");
                    effect.ParameterC = RetroSfxSynthGui.EffectSlider(
                        cells[2], $"fx-{index}-attack", "Attack", effect.ParameterC, 0.001f, 0.1f, "s", "Gain reduction attack");
                    effect.ParameterD = RetroSfxSynthGui.EffectSlider(
                        cells[3], $"fx-{index}-release", "Release", effect.ParameterD, 0.01f, 0.5f, "s", "Gain reduction release");
                    effect.ParameterE = RetroSfxSynthGui.EffectSlider(
                        cells[4], $"fx-{index}-makeup", "Makeup", effect.ParameterE, 0f, 18f, "dB", "Output makeup gain");
                    break;
                }
                case RetroSfxEffectType.Distortion:
                {
                    Rect[] cells = GetEffectParameterCells(rect, 3);
                    effect.ParameterA = RetroSfxSynthGui.EffectSlider(
                        cells[0], $"fx-{index}-drive", "Drive", effect.ParameterA, 1f, 20f, "x", "Saturation drive");
                    effect.ParameterB = RetroSfxSynthGui.EffectSlider(
                        cells[1], $"fx-{index}-tone", "Tone", effect.ParameterB, 0f, 1f, "%", "Post-drive brightness");
                    effect.ParameterC = RetroSfxSynthGui.EffectSlider(
                        cells[2], $"fx-{index}-distortion-mix", "Mix", effect.ParameterC, 0f, 1f, "%", "Wet/dry balance");
                    break;
                }
                case RetroSfxEffectType.Chorus:
                {
                    Rect[] cells = GetEffectParameterCells(rect, 4);
                    effect.ParameterA = RetroSfxSynthGui.EffectSlider(
                        cells[0], $"fx-{index}-chorus-rate", "Rate", effect.ParameterA, 0.05f, 8f, "Hz1", "Modulation rate");
                    effect.ParameterB = RetroSfxSynthGui.EffectSlider(
                        cells[1], $"fx-{index}-chorus-depth", "Depth", effect.ParameterB, 0f, 0.012f, "s", "Delay modulation depth");
                    effect.ParameterC = RetroSfxSynthGui.EffectSlider(
                        cells[2], $"fx-{index}-chorus-delay", "Delay", effect.ParameterC, 0.004f, 0.03f, "s", "Base chorus delay");
                    effect.ParameterD = RetroSfxSynthGui.EffectSlider(
                        cells[3], $"fx-{index}-chorus-mix", "Mix", effect.ParameterD, 0f, 1f, "%", "Wet/dry balance");
                    break;
                }
                case RetroSfxEffectType.Delay:
                {
                    Rect[] cells = GetEffectParameterCells(rect, 3);
                    effect.ParameterA = RetroSfxSynthGui.EffectSlider(
                        cells[0], $"fx-{index}-delay-time", "Time", effect.ParameterA, 0.03f, 1f, "s", "Echo time");
                    effect.ParameterB = RetroSfxSynthGui.EffectSlider(
                        cells[1], $"fx-{index}-feedback", "Feedback", effect.ParameterB, 0f, 0.9f, "%", "Echo feedback");
                    effect.ParameterC = RetroSfxSynthGui.EffectSlider(
                        cells[2], $"fx-{index}-delay-mix", "Mix", effect.ParameterC, 0f, 1f, "%", "Wet/dry balance");
                    break;
                }
                case RetroSfxEffectType.Reverb:
                {
                    Rect[] cells = GetEffectParameterCells(rect, 4);
                    effect.ParameterA = RetroSfxSynthGui.EffectSlider(
                        cells[0], $"fx-{index}-room", "Room", effect.ParameterA, 0f, 1f, "%", "Virtual room size");
                    effect.ParameterB = RetroSfxSynthGui.EffectSlider(
                        cells[1], $"fx-{index}-decay", "Decay", effect.ParameterB, 0.2f, 4f, "s", "Reverb decay time");
                    effect.ParameterC = RetroSfxSynthGui.EffectSlider(
                        cells[2], $"fx-{index}-damping", "Damping", effect.ParameterC, 0f, 1f, "%", "High-frequency damping");
                    effect.ParameterD = RetroSfxSynthGui.EffectSlider(
                        cells[3], $"fx-{index}-reverb-mix", "Mix", effect.ParameterD, 0f, 1f, "%", "Wet/dry balance");
                    break;
                }
            }
        }

        private static Rect[] GetEffectParameterCells(Rect rect, int count)
        {
            const float gap = 8f;
            float cellWidth = (rect.width - gap * (count - 1)) / count;
            Rect[] cells = new Rect[count];
            for (int index = 0; index < count; index++)
            {
                cells[index] = new Rect(
                    rect.x + index * (cellWidth + gap),
                    rect.y,
                    cellWidth,
                    rect.height);
            }
            return cells;
        }

        private static void DrawFilterModeControl(
            Rect rect,
            RetroSfxEffectSettings effect)
        {
            GUI.Label(
                new Rect(rect.x, rect.y, rect.width, 14f),
                "MODE",
                RetroSfxSynthGui.TinyStyle);
            Rect buttonsRect = new Rect(rect.x, rect.y + 20f, rect.width, 18f);
            float width = (buttonsRect.width - 4f) / 3f;
            RetroSfxFilterMode[] modes =
            {
                RetroSfxFilterMode.LowPass,
                RetroSfxFilterMode.BandPass,
                RetroSfxFilterMode.HighPass
            };
            string[] labels = { "LP", "BP", "HP" };
            for (int index = 0; index < modes.Length; index++)
            {
                Rect buttonRect = new Rect(
                    buttonsRect.x + index * (width + 2f),
                    buttonsRect.y,
                    width,
                    buttonsRect.height);
                if (RetroSfxSynthGui.RectButton(
                        buttonRect,
                        labels[index],
                        $"{modes[index]} filter",
                        effect.FilterMode == modes[index]))
                {
                    effect.FilterMode = modes[index];
                    GUI.changed = true;
                }
            }
        }

        private void ShowAddEffectMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (RetroSfxEffectType type in Enum.GetValues(typeof(RetroSfxEffectType)))
            {
                RetroSfxEffectType capturedType = type;
                menu.AddItem(
                    new GUIContent(GetEffectDisplayName(type)),
                    false,
                    () => AddEffect(capturedType));
            }
            menu.ShowAsContext();
        }

        private void AddEffect(RetroSfxEffectType type)
        {
            settings.Effects.Add(RetroSfxEffectSettings.Create(type));
            effectList = null;
            effectsChainExpanded = true;
            generatedSettingsHash = int.MinValue;
            statusMessage = $"EFFECT CHAIN  ·  {GetEffectDisplayName(type)} added";
            Repaint();
        }

        private void ApplyPendingEffectRemoval()
        {
            if (pendingEffectRemoval < 0 || pendingEffectRemoval >= settings.Effects.Count)
            {
                pendingEffectRemoval = -1;
                return;
            }

            string removedName = GetEffectDisplayName(settings.Effects[pendingEffectRemoval].Type);
            settings.Effects.RemoveAt(pendingEffectRemoval);
            pendingEffectRemoval = -1;
            effectList = null;
            generatedSettingsHash = int.MinValue;
            statusMessage = $"EFFECT CHAIN  ·  {removedName} removed";
            Repaint();
        }

        private static string GetEffectDisplayName(RetroSfxEffectType type)
        {
            return type == RetroSfxEffectType.Equalizer
                ? "EQ"
                : type.ToString().ToUpperInvariant();
        }

        private void DrawWaveformDisplay()
        {
            GUILayout.BeginVertical(RetroSfxSynthGui.PanelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("OUTPUT WAVEFORM", RetroSfxSynthGui.SectionTitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                $"{generatedSamples.Length:N0} SAMPLES  ·  PK {generatedPeakAmplitude:0.00}",
                RetroSfxSynthGui.TinyStyle,
                GUILayout.Width(165f));
            GUILayout.EndHorizontal();

            Rect waveformRect = GUILayoutUtility.GetRect(100f, 138f, GUILayout.ExpandWidth(true));
            DrawWaveform(waveformRect);
            GUILayout.EndVertical();
        }

        private void DrawWaveform(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            RetroSfxSynthGui.DrawWaveformGrid(rect);
            Rect dataRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
            float centerY = dataRect.center.y;
            int columnCount = Mathf.Max(1, Mathf.FloorToInt(dataRect.width));
            float halfHeight = dataRect.height * 0.46f;

            for (int column = 0; column < columnCount; column++)
            {
                int startSample = (int)((long)column * generatedSamples.Length / columnCount);
                int endSample = Mathf.Max(
                    startSample + 1,
                    (int)((long)(column + 1) * generatedSamples.Length / columnCount));
                endSample = Mathf.Min(endSample, generatedSamples.Length);

                float minimum = 0f;
                float maximum = 0f;
                for (int sampleIndex = startSample; sampleIndex < endSample; sampleIndex++)
                {
                    float sample = generatedSamples[sampleIndex];
                    minimum = Mathf.Min(minimum, sample);
                    maximum = Mathf.Max(maximum, sample);
                }

                minimum = Mathf.Clamp(minimum, -1f, 1f);
                maximum = Mathf.Clamp(maximum, -1f, 1f);
                float top = centerY - maximum * halfHeight;
                float bottom = centerY - minimum * halfHeight;
                EditorGUI.DrawRect(
                    new Rect(dataRect.x + column, top, 1f, Mathf.Max(1f, bottom - top)),
                    RetroSfxSynthGui.Signal);
            }

            if (previewIsActive && GeneratedDuration > 0f)
            {
                float elapsed = (float)(EditorApplication.timeSinceStartup - previewStartedAt);
                float progress = Mathf.Clamp01(elapsed / GeneratedDuration);
                float playheadX = Mathf.Round(Mathf.Lerp(dataRect.x, dataRect.xMax, progress));
                EditorGUI.DrawRect(
                    new Rect(playheadX, dataRect.y, 2f, dataRect.height),
                    Color.white);
            }

            GUI.Label(
                new Rect(dataRect.x + 6f, dataRect.y + 4f, 160f, 18f),
                $"{GeneratedDuration:0.000} s",
                RetroSfxSynthGui.TinyStyle);
        }

        private void DrawExportBay()
        {
            GUILayout.BeginVertical(RetroSfxSynthGui.PanelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("RENDER", RetroSfxSynthGui.SectionTitleStyle, GUILayout.Width(58f));
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("FOLDER", RetroSfxSynthGui.TinyStyle, GUILayout.Width(44f));
            outputFolder = EditorGUILayout.TextField(outputFolder, RetroSfxSynthGui.FieldStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("NAME", RetroSfxSynthGui.TinyStyle, GUILayout.Width(44f));
            exportName = EditorGUILayout.TextField(exportName, RetroSfxSynthGui.FieldStyle);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(8f);
            if (GUILayout.Button(
                    new GUIContent("RENDER WAV", "Save a 16-bit mono WAV asset"),
                    RetroSfxSynthGui.PrimaryButtonStyle,
                    GUILayout.Width(108f),
                    GUILayout.Height(48f)))
            {
                SaveWav();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawStatusStrip()
        {
            GUILayout.BeginHorizontal(RetroSfxSynthGui.InsetStyle);
            Rect ledRect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.Width(10f), GUILayout.Height(10f));
            if (Event.current.type == EventType.Repaint)
            {
                Color indicator = previewIsActive
                    ? RetroSfxSynthGui.Accent
                    : RetroSfxSynthGui.MutedText;
                EditorGUI.DrawRect(
                    new Rect(ledRect.x + 2f, ledRect.y + 2f, 6f, 6f),
                    indicator);
            }
            GUILayout.Space(3f);
            GUILayout.Label(statusMessage, RetroSfxSynthGui.StatusStyle);
            GUILayout.EndHorizontal();
        }

        private void HandleKeyboardShortcuts()
        {
            Event currentEvent = Event.current;
            if (currentEvent.type != EventType.KeyDown || GUIUtility.keyboardControl != 0)
            {
                return;
            }

            if (currentEvent.keyCode == KeyCode.Space)
            {
                Preview();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                StopPreview();
                currentEvent.Use();
            }
        }

        private void Preview()
        {
            StopPreview();
            EnsureWaveformIsCurrent();
            if (sourceMode == AudioSourceMode.Imported && importedAudio.SourceClip == null)
            {
                statusMessage = "Select an AudioClip in the IMPORT tab before previewing.";
                return;
            }
            if (sourceMode == AudioSourceMode.Imported && !string.IsNullOrEmpty(importedClipError))
            {
                statusMessage = $"Preview unavailable: {importedClipError}";
                return;
            }
            if (generatedSamples == null || generatedSamples.Length == 0)
            {
                statusMessage = "The active source contains no audio to preview.";
                return;
            }
            if (generatedPeakAmplitude < 0.001f)
            {
                statusMessage = sourceMode == AudioSourceMode.Imported
                    ? "No audible signal. Raise Gain or adjust the clip envelope."
                    : "No audible signal. Raise Output or increase the envelope duration.";
                return;
            }

            try
            {
                if (previewClip == null || previewAssetSettingsHash != generatedSettingsHash)
                {
                    WavFileWriter.WriteMono16(
                        Path.GetFullPath(PreviewAssetPath),
                        generatedSamples,
                        RetroSfxSettings.SampleRate);
                    AssetDatabase.ImportAsset(
                        PreviewAssetPath,
                        ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    previewClip = AssetDatabase.LoadAssetAtPath<AudioClip>(PreviewAssetPath);
                    previewAssetSettingsHash = previewClip == null ? int.MinValue : generatedSettingsHash;
                }
            }
            catch (Exception exception)
            {
                CleanupPreviewAsset();
                statusMessage = $"Preview could not be prepared: {exception.Message}";
                return;
            }

            if (previewClip == null)
            {
                CleanupPreviewAsset();
                statusMessage = "Unity could not import the temporary preview WAV.";
                return;
            }

            if (EditorAudioPreviewService.TryPlay(previewClip, out string failureReason))
            {
                previewStartedAt = EditorApplication.timeSinceStartup;
                previewIsActive = true;
                statusMessage =
                    $"AUDITIONING  ·  {GeneratedDuration:0.000} s  ·  peak {generatedPeakAmplitude:0.00}";
            }
            else
            {
                CleanupPreviewAsset();
                statusMessage = $"Preview could not start: {failureReason}";
            }
        }

        private void SaveWav()
        {
            string normalizedFolder = outputFolder.Replace('\\', '/').TrimEnd('/');
            if (!normalizedFolder.StartsWith("Assets", StringComparison.Ordinal))
            {
                statusMessage = "Render folder must be inside this project's Assets folder.";
                return;
            }

            string sanitizedName = SanitizeFileName(exportName);
            if (string.IsNullOrEmpty(sanitizedName))
            {
                statusMessage = "Enter a valid render name.";
                return;
            }

            Directory.CreateDirectory(normalizedFolder);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{normalizedFolder}/{sanitizedName}.wav");
            EnsureWaveformIsCurrent();
            if (sourceMode == AudioSourceMode.Imported && importedAudio.SourceClip == null)
            {
                statusMessage = "Select an AudioClip in the IMPORT tab before rendering.";
                return;
            }
            if (sourceMode == AudioSourceMode.Imported && !string.IsNullOrEmpty(importedClipError))
            {
                statusMessage = $"Render unavailable: {importedClipError}";
                return;
            }
            if (generatedSamples == null || generatedSamples.Length == 0)
            {
                statusMessage = "The active source contains no audio to render.";
                return;
            }
            WavFileWriter.WriteMono16(assetPath, generatedSamples, RetroSfxSettings.SampleRate);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AudioClip importedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            EditorGUIUtility.PingObject(importedClip);
            Selection.activeObject = importedClip;
            statusMessage = $"RENDERED  ·  {assetPath}";
        }

        private void ResetSettings()
        {
            StopPreview();
            if (sourceMode == AudioSourceMode.Imported)
            {
                AudioClip sourceClip = importedAudio.SourceClip;
                importedAudio = new RetroSfxImportedAudioSettings
                {
                    SourceClip = sourceClip
                };
                settings.Effects.Clear();
                importedClipError = string.Empty;
                importedDurationLimited = false;
                statusMessage = sourceClip == null
                    ? "Clip edits and effects reset."
                    : $"Clip edits and effects reset for {sourceClip.name}.";
            }
            else
            {
                settings = new RetroSfxSettings();
                statusMessage = "Patch reset to the neutral starting sound.";
            }
            effectList = null;
            generatedSettingsHash = int.MinValue;
            EnsureWaveformIsCurrent();
            Repaint();
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreviewState;
            StopPreview();
            CleanupPreviewAsset();
        }

        private void UpdatePreviewState()
        {
            if (!previewIsActive)
            {
                if (sourceMode == AudioSourceMode.Imported &&
                    importedAudio.SourceClip != null &&
                    importedAudio.SourceClip.loadState == AudioDataLoadState.Loading)
                {
                    importedOverviewClipId = 0;
                    generatedSettingsHash = int.MinValue;
                    Repaint();
                }
                return;
            }

            bool reportedPlaying = EditorAudioPreviewService.IsPlaying(previewClip);
            bool durationElapsed =
                EditorApplication.timeSinceStartup - previewStartedAt > GeneratedDuration + 0.1d;
            if (durationElapsed ||
                !reportedPlaying && EditorApplication.timeSinceStartup - previewStartedAt > 0.1d)
            {
                previewIsActive = false;
                statusMessage = "Preview finished";
            }

            double now = EditorApplication.timeSinceStartup;
            if (now >= nextPreviewRepaintAt)
            {
                nextPreviewRepaintAt = now + 1d / 60d;
                Repaint();
            }
        }

        private void StopPreview()
        {
            bool wasActive = previewIsActive;
            EditorAudioPreviewService.Stop();
            previewIsActive = false;
            if (wasActive)
            {
                statusMessage = "STOPPED  ·  preview ended";
            }
            Repaint();
        }

        private void CleanupPreviewAsset()
        {
            previewClip = null;
            previewAssetSettingsHash = int.MinValue;
            if (AssetDatabase.LoadMainAssetAtPath(PreviewAssetPath) != null ||
                File.Exists(PreviewAssetPath))
            {
                AssetDatabase.DeleteAsset(PreviewAssetPath);
            }
        }

        private void EnsureWaveformIsCurrent()
        {
            int settingsHash = CalculateCurrentSettingsHash();
            bool stageCacheIsCurrent =
                drySamples != null &&
                effectStageSamples.Count == settings.Effects.Count;
            if (generatedSamples != null &&
                generatedSettingsHash == settingsHash &&
                stageCacheIsCurrent)
            {
                return;
            }

            if (sourceMode == AudioSourceMode.Synth)
            {
                importedClipError = string.Empty;
                importedDurationLimited = false;
                drySamples = RetroSfxSynthesizer.GenerateDrySamples(settings);
            }
            else if (!RetroSfxImportedAudioProcessor.TryGenerate(
                         importedAudio,
                         out drySamples,
                         out importedDurationLimited,
                         out importedClipError))
            {
                drySamples = Array.Empty<float>();
            }

            generatedSamples = RetroSfxEffectsProcessor.Process(
                drySamples,
                RetroSfxSettings.SampleRate,
                settings.Effects,
                effectStageSamples);
            generatedPeakAmplitude = CalculatePeakAmplitude(generatedSamples);
            generatedSettingsHash = settingsHash;
        }

        private int CalculateCurrentSettingsHash()
        {
            unchecked
            {
                int hash = CalculateSettingsHash(settings);
                hash = hash * 31 + (int)sourceMode;
                if (sourceMode != AudioSourceMode.Imported)
                {
                    return hash;
                }

                AudioClip clip = importedAudio.SourceClip;
                hash = hash * 31 + (clip == null ? 0 : clip.GetInstanceID());
                hash = hash * 31 + (clip == null ? 0 : (int)clip.loadState);
                hash = hash * 31 + importedAudio.TrimStart.GetHashCode();
                hash = hash * 31 + importedAudio.TrimEnd.GetHashCode();
                hash = hash * 31 + importedAudio.FadeIn.GetHashCode();
                hash = hash * 31 + importedAudio.FadeOut.GetHashCode();
                hash = hash * 31 + importedAudio.PitchSemitones.GetHashCode();
                hash = hash * 31 + importedAudio.GainDecibels.GetHashCode();
                hash = hash * 31 + importedAudio.EnvelopeAttack.GetHashCode();
                hash = hash * 31 + importedAudio.EnvelopeDecay.GetHashCode();
                hash = hash * 31 + importedAudio.EnvelopeSustain.GetHashCode();
                hash = hash * 31 + importedAudio.EnvelopeRelease.GetHashCode();
                return hash;
            }
        }

        private static int CalculateSettingsHash(RetroSfxSettings currentSettings)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)currentSettings.WaveType;
                hash = hash * 31 + currentSettings.MasterVolume.GetHashCode();
                hash = hash * 31 + currentSettings.AttackTime.GetHashCode();
                hash = hash * 31 + currentSettings.SustainTime.GetHashCode();
                hash = hash * 31 + currentSettings.SustainPunch.GetHashCode();
                hash = hash * 31 + currentSettings.DecayTime.GetHashCode();
                hash = hash * 31 + currentSettings.StartFrequency.GetHashCode();
                hash = hash * 31 + currentSettings.FrequencySlide.GetHashCode();
                hash = hash * 31 + currentSettings.VibratoDepth.GetHashCode();
                hash = hash * 31 + currentSettings.VibratoRate.GetHashCode();
                hash = hash * 31 + currentSettings.DutyCycle.GetHashCode();
                hash = hash * 31 + currentSettings.ArpeggioOffset.GetHashCode();
                hash = hash * 31 + currentSettings.ArpeggioTime.GetHashCode();
                hash = hash * 31 + currentSettings.RepeatRate.GetHashCode();
                hash = hash * 31 + currentSettings.BitCrushAmount.GetHashCode();
                hash = hash * 31 + currentSettings.Seed;
                hash = hash * 31 + currentSettings.Effects.Count;
                foreach (RetroSfxEffectSettings effect in currentSettings.Effects)
                {
                    if (effect == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    hash = hash * 31 + (int)effect.Type;
                    hash = hash * 31 + (int)effect.FilterMode;
                    hash = hash * 31 + (effect.Enabled ? 1 : 0);
                    hash = hash * 31 + effect.ParameterA.GetHashCode();
                    hash = hash * 31 + effect.ParameterB.GetHashCode();
                    hash = hash * 31 + effect.ParameterC.GetHashCode();
                    hash = hash * 31 + effect.ParameterD.GetHashCode();
                    hash = hash * 31 + effect.ParameterE.GetHashCode();
                }
                return hash;
            }
        }

        private static float CalculatePeakAmplitude(float[] samples)
        {
            float peakAmplitude = 0f;
            if (samples == null)
            {
                return peakAmplitude;
            }
            foreach (float sample in samples)
            {
                peakAmplitude = Mathf.Max(peakAmplitude, Mathf.Abs(sample));
            }
            return peakAmplitude;
        }

        private void ApplyPreset(RetroSfxPreset preset)
        {
            sourceMode = AudioSourceMode.Synth;
            List<RetroSfxEffectSettings> existingEffects =
                settings.Effects ?? new List<RetroSfxEffectSettings>();
            settings = RetroSfxPresetFactory.Create(preset);
            settings.Effects = existingEffects;
            effectList = null;
            generatedSettingsHash = int.MinValue;
            EnsureWaveformIsCurrent();
            Repaint();
            Preview();
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidCharacter.ToString(), string.Empty);
            }
            return value.Trim();
        }
    }
}
