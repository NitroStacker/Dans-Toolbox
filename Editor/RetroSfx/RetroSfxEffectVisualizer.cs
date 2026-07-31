using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.Audio
{
    /// <summary>
    /// Compact, allocation-free effect displays. Each view combines a parameter
    /// response with the real signal at that point in the offline DSP chain.
    /// </summary>
    internal static class RetroSfxEffectVisualizer
    {
        private const int CurvePointCount = 72;
        private const int TracePointCount = 80;
        private static readonly Vector3[] CurvePoints = new Vector3[CurvePointCount];
        private static readonly Vector3[] TracePoints = new Vector3[TracePointCount];
        private static GUIStyle activeLabelStyle;
        private static GUIStyle bypassedLabelStyle;

        internal static void Draw(
            Rect rect,
            RetroSfxEffectSettings effect,
            float[] input,
            float[] output,
            float previewTime,
            bool isPlaying)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            RetroSfxSynthGui.DrawWaveformGrid(rect);
            Rect graph = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f);

            if (effect.Enabled)
            {
                switch (effect.Type)
                {
                    case RetroSfxEffectType.Filter:
                        DrawFilterResponse(graph, effect);
                        break;
                    case RetroSfxEffectType.Equalizer:
                        DrawEqualizerResponse(graph, effect);
                        break;
                    case RetroSfxEffectType.Compressor:
                        DrawCompressorTransfer(graph, effect, input, output, previewTime, isPlaying);
                        break;
                    case RetroSfxEffectType.Distortion:
                        DrawDistortionTransfer(graph, effect, input, output, previewTime, isPlaying);
                        break;
                    case RetroSfxEffectType.Chorus:
                        DrawChorusMotion(graph, effect, previewTime);
                        break;
                    case RetroSfxEffectType.Delay:
                        DrawDelayTaps(graph, effect, previewTime);
                        break;
                    case RetroSfxEffectType.Reverb:
                        DrawReverbTail(graph, effect, previewTime);
                        break;
                }
            }

            DrawSignalLayer(graph, input, output, previewTime, isPlaying, effect.Enabled);

            GUI.Label(
                new Rect(graph.x + 3f, graph.y + 1f, 92f, 14f),
                effect.Enabled ? (isPlaying ? "LIVE SIGNAL" : "STAGE RESPONSE") : "BYPASSED",
                GetLabelStyle(effect.Enabled));
        }

        private static void DrawFilterResponse(Rect rect, RetroSfxEffectSettings effect)
        {
            float cutoff = Mathf.Clamp(effect.ParameterA, 30f, 18000f);
            float resonance = Mathf.Clamp01(effect.ParameterB);
            float mix = Mathf.Clamp01(effect.ParameterC);

            for (int index = 0; index < CurvePointCount; index++)
            {
                float normalized = index / (CurvePointCount - 1f);
                float frequency = 20f * Mathf.Pow(1000f, normalized);
                float ratio = Mathf.Max(0.0001f, frequency / cutoff);
                float response;
                switch (effect.FilterMode)
                {
                    case RetroSfxFilterMode.HighPass:
                        response = 1f / Mathf.Sqrt(1f + Mathf.Pow(1f / ratio, 4f));
                        break;
                    case RetroSfxFilterMode.BandPass:
                    {
                        float width = Mathf.Lerp(1.25f, 0.2f, resonance);
                        float distance = Mathf.Log(ratio, 2f) / width;
                        response = Mathf.Exp(-distance * distance);
                        break;
                    }
                    default:
                        response = 1f / Mathf.Sqrt(1f + Mathf.Pow(ratio, 4f));
                        break;
                }

                float resonancePeak = resonance * 0.42f *
                    Mathf.Exp(-Mathf.Pow(Mathf.Log(ratio, 2f) / 0.28f, 2f));
                response = Mathf.Clamp01(Mathf.Lerp(1f, response + resonancePeak, mix));
                CurvePoints[index] = new Vector3(
                    Mathf.Lerp(rect.x, rect.xMax, normalized),
                    Mathf.Lerp(rect.yMax - 5f, rect.y + 7f, response));
            }

            DrawCurve(RetroSfxSynthGui.Signal, 2f);
        }

        private static void DrawEqualizerResponse(Rect rect, RetroSfxEffectSettings effect)
        {
            float mix = Mathf.Clamp01(effect.ParameterD);
            for (int index = 0; index < CurvePointCount; index++)
            {
                float normalized = index / (CurvePointCount - 1f);
                float logFrequency = Mathf.Lerp(Mathf.Log10(20f), Mathf.Log10(20000f), normalized);
                float lowWeight = Gaussian(logFrequency, Mathf.Log10(120f), 0.42f);
                float midWeight = Gaussian(logFrequency, Mathf.Log10(1000f), 0.5f);
                float highWeight = Gaussian(logFrequency, Mathf.Log10(8000f), 0.42f);
                float decibels = (
                    effect.ParameterA * lowWeight +
                    effect.ParameterB * midWeight +
                    effect.ParameterC * highWeight) * mix;
                float y = Mathf.Lerp(rect.yMax - 5f, rect.y + 5f, Mathf.InverseLerp(-18f, 18f, decibels));
                CurvePoints[index] = new Vector3(
                    Mathf.Lerp(rect.x, rect.xMax, normalized),
                    y);
            }

            EditorGUI.DrawRect(
                new Rect(rect.x, Mathf.Round(rect.center.y), rect.width, 1f),
                new Color(RetroSfxSynthGui.MutedText.r, RetroSfxSynthGui.MutedText.g, RetroSfxSynthGui.MutedText.b, 0.55f));
            DrawCurve(RetroSfxSynthGui.Signal, 2f);
        }

        private static void DrawCompressorTransfer(
            Rect rect,
            RetroSfxEffectSettings effect,
            float[] input,
            float[] output,
            float previewTime,
            bool isPlaying)
        {
            float threshold = Mathf.Clamp(effect.ParameterA, -48f, 0f);
            float ratio = Mathf.Clamp(effect.ParameterB, 1f, 20f);
            float makeup = Mathf.Clamp(effect.ParameterE, 0f, 18f);

            for (int index = 0; index < CurvePointCount; index++)
            {
                float normalized = index / (CurvePointCount - 1f);
                float inputDb = Mathf.Lerp(-48f, 0f, normalized);
                float outputDb = inputDb <= threshold
                    ? inputDb + makeup
                    : threshold + (inputDb - threshold) / ratio + makeup;
                CurvePoints[index] = new Vector3(
                    Mathf.Lerp(rect.x + 2f, rect.xMax - 2f, normalized),
                    Mathf.Lerp(rect.yMax - 3f, rect.y + 3f, Mathf.InverseLerp(-48f, 0f, outputDb)));
            }

            DrawCurve(RetroSfxSynthGui.Accent, 2f);
            float thresholdX = Mathf.Lerp(rect.x, rect.xMax, Mathf.InverseLerp(-48f, 0f, threshold));
            EditorGUI.DrawRect(
                new Rect(Mathf.Round(thresholdX), rect.y + 2f, 1f, rect.height - 4f),
                RetroSfxSynthGui.Signal);
            DrawTransferPoint(rect, input, output, previewTime, isPlaying);
        }

        private static void DrawDistortionTransfer(
            Rect rect,
            RetroSfxEffectSettings effect,
            float[] input,
            float[] output,
            float previewTime,
            bool isPlaying)
        {
            float drive = Mathf.Clamp(effect.ParameterA, 1f, 20f);
            float mix = Mathf.Clamp01(effect.ParameterC);
            float normalization = Mathf.Max(0.001f, (float)System.Math.Tanh(drive));
            for (int index = 0; index < CurvePointCount; index++)
            {
                float normalized = index / (CurvePointCount - 1f);
                float x = Mathf.Lerp(-1f, 1f, normalized);
                float shaped = (float)System.Math.Tanh(x * drive) / normalization;
                float y = Mathf.Lerp(x, shaped, mix);
                CurvePoints[index] = new Vector3(
                    Mathf.Lerp(rect.x + 2f, rect.xMax - 2f, normalized),
                    Mathf.Lerp(rect.yMax - 3f, rect.y + 3f, (y + 1f) * 0.5f));
            }

            EditorGUI.DrawRect(new Rect(rect.center.x, rect.y, 1f, rect.height), RetroSfxSynthGui.Border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.center.y, rect.width, 1f), RetroSfxSynthGui.Border);
            DrawCurve(RetroSfxSynthGui.Accent, 2f);
            DrawTransferPoint(rect, input, output, previewTime, isPlaying);
        }

        private static void DrawChorusMotion(
            Rect rect,
            RetroSfxEffectSettings effect,
            float previewTime)
        {
            float rate = Mathf.Clamp(effect.ParameterA, 0.05f, 8f);
            float depth = Mathf.Clamp01(effect.ParameterB / 0.012f);
            for (int index = 0; index < CurvePointCount; index++)
            {
                float normalized = index / (CurvePointCount - 1f);
                float phase = normalized * Mathf.PI * 4f + previewTime * rate * Mathf.PI * 2f;
                float y = rect.center.y + Mathf.Sin(phase) * rect.height * (0.08f + depth * 0.24f);
                CurvePoints[index] = new Vector3(Mathf.Lerp(rect.x, rect.xMax, normalized), y);
            }
            DrawCurve(RetroSfxSynthGui.Signal, 1.8f);

            for (int index = 0; index < CurvePointCount; index++)
            {
                float normalized = index / (CurvePointCount - 1f);
                float phase = normalized * Mathf.PI * 4f + previewTime * rate * Mathf.PI * 2f + 0.8f;
                float y = rect.center.y + Mathf.Sin(phase) * rect.height * 0.2f;
                CurvePoints[index] = new Vector3(Mathf.Lerp(rect.x, rect.xMax, normalized), y);
            }
            DrawCurve(new Color(RetroSfxSynthGui.Accent.r, RetroSfxSynthGui.Accent.g, RetroSfxSynthGui.Accent.b, 0.65f), 1.4f);
        }

        private static void DrawDelayTaps(
            Rect rect,
            RetroSfxEffectSettings effect,
            float previewTime)
        {
            float feedback = Mathf.Clamp(effect.ParameterB, 0f, 0.9f);
            float mix = Mathf.Clamp01(effect.ParameterC);
            float delay = Mathf.Max(0.03f, effect.ParameterA);
            int activeTap = Mathf.FloorToInt(previewTime / delay);
            const int tapCount = 7;

            for (int tap = 0; tap < tapCount; tap++)
            {
                float x = Mathf.Lerp(rect.x + 12f, rect.xMax - 8f, tap / (tapCount - 1f));
                float strength = tap == 0 ? 1f - mix : mix * Mathf.Pow(feedback, tap - 1);
                float height = Mathf.Max(3f, strength * (rect.height - 15f));
                Color color = tap == activeTap % tapCount
                    ? RetroSfxSynthGui.Signal
                    : RetroSfxSynthGui.Accent;
                EditorGUI.DrawRect(new Rect(Mathf.Round(x), rect.yMax - height - 3f, 3f, height), color);
            }
        }

        private static void DrawReverbTail(
            Rect rect,
            RetroSfxEffectSettings effect,
            float previewTime)
        {
            float decay = Mathf.Clamp(effect.ParameterB, 0.2f, 4f);
            float room = Mathf.Clamp01(effect.ParameterA);
            float damping = Mathf.Clamp01(effect.ParameterC);

            for (int index = 0; index < CurvePointCount; index++)
            {
                float normalized = index / (CurvePointCount - 1f);
                float time = normalized * Mathf.Max(0.3f, decay);
                float envelope = Mathf.Exp(-3.2f * time / decay);
                float diffusion = Mathf.Sin(normalized * Mathf.PI * Mathf.Lerp(18f, 38f, room) + previewTime * 5f);
                float y = rect.center.y - diffusion * envelope * rect.height * Mathf.Lerp(0.33f, 0.17f, damping);
                CurvePoints[index] = new Vector3(Mathf.Lerp(rect.x, rect.xMax, normalized), y);
            }
            DrawCurve(RetroSfxSynthGui.Signal, 1.7f);

            for (int tap = 1; tap < 8; tap++)
            {
                float x = Mathf.Lerp(rect.x + 8f, rect.xMax - 8f, tap / 8f);
                float alpha = Mathf.Lerp(0.65f, 0.15f, tap / 8f);
                EditorGUI.DrawRect(
                    new Rect(Mathf.Round(x), rect.center.y - 5f, 1f, 10f),
                    new Color(RetroSfxSynthGui.Accent.r, RetroSfxSynthGui.Accent.g, RetroSfxSynthGui.Accent.b, alpha));
            }
        }

        private static void DrawSignalLayer(
            Rect rect,
            float[] input,
            float[] output,
            float previewTime,
            bool isPlaying,
            bool enabled)
        {
            if (output == null || output.Length == 0)
            {
                return;
            }

            if (!isPlaying)
            {
                DrawEnvelope(rect, output, enabled ? RetroSfxSynthGui.AccentSoft : RetroSfxSynthGui.MutedText);
                return;
            }

            if (input != null && input.Length > 0)
            {
                DrawLiveTrace(
                    rect,
                    input,
                    previewTime,
                    new Color(RetroSfxSynthGui.MutedText.r, RetroSfxSynthGui.MutedText.g, RetroSfxSynthGui.MutedText.b, 0.55f),
                    1.1f);
            }
            DrawLiveTrace(
                rect,
                output,
                previewTime,
                enabled ? RetroSfxSynthGui.Signal : RetroSfxSynthGui.MutedText,
                1.8f);
        }

        private static void DrawEnvelope(Rect rect, float[] samples, Color color)
        {
            int columns = Mathf.Min(TracePointCount, Mathf.Max(1, Mathf.FloorToInt(rect.width / 4f)));
            Color envelopeColor = new Color(color.r, color.g, color.b, 0.28f);
            for (int column = 0; column < columns; column++)
            {
                int start = (int)((long)column * samples.Length / columns);
                int end = Mathf.Min(
                    samples.Length,
                    Mathf.Max(start + 1, (int)((long)(column + 1) * samples.Length / columns)));
                float peak = 0f;
                int stride = Mathf.Max(1, (end - start) / 32);
                for (int sample = start; sample < end; sample += stride)
                {
                    peak = Mathf.Max(peak, Mathf.Abs(samples[sample]));
                }

                float x = Mathf.Lerp(rect.x, rect.xMax, column / Mathf.Max(1f, columns - 1f));
                float height = Mathf.Clamp01(peak) * rect.height * 0.38f;
                EditorGUI.DrawRect(new Rect(Mathf.Round(x), rect.center.y - height, 1f, height * 2f), envelopeColor);
            }
        }

        private static void DrawLiveTrace(
            Rect rect,
            float[] samples,
            float previewTime,
            Color color,
            float width)
        {
            int centerSample = Mathf.Clamp(
                Mathf.RoundToInt(previewTime * RetroSfxSettings.SampleRate),
                0,
                samples.Length - 1);
            int span = Mathf.Min(samples.Length, RetroSfxSettings.SampleRate / 80);
            int start = Mathf.Clamp(centerSample - span / 2, 0, Mathf.Max(0, samples.Length - span));

            for (int index = 0; index < TracePointCount; index++)
            {
                float normalized = index / (TracePointCount - 1f);
                int sampleIndex = Mathf.Clamp(
                    start + Mathf.RoundToInt(normalized * Mathf.Max(0, span - 1)),
                    0,
                    samples.Length - 1);
                float sample = Mathf.Clamp(samples[sampleIndex], -1f, 1f);
                TracePoints[index] = new Vector3(
                    Mathf.Lerp(rect.x, rect.xMax, normalized),
                    rect.center.y - sample * rect.height * 0.42f);
            }

            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(width, TracePoints);
            Handles.EndGUI();
        }

        private static void DrawTransferPoint(
            Rect rect,
            float[] input,
            float[] output,
            float previewTime,
            bool isPlaying)
        {
            if (!isPlaying || input == null || output == null || input.Length == 0 || output.Length == 0)
            {
                return;
            }

            float inputSample = SampleAt(input, previewTime);
            float outputSample = SampleAt(output, previewTime);
            Vector3 point = new Vector3(
                Mathf.Lerp(rect.x + 2f, rect.xMax - 2f, (inputSample + 1f) * 0.5f),
                Mathf.Lerp(rect.yMax - 3f, rect.y + 3f, (outputSample + 1f) * 0.5f));
            Handles.BeginGUI();
            Handles.color = Color.white;
            Handles.DrawSolidDisc(point, Vector3.forward, 3f);
            Handles.EndGUI();
        }

        private static void DrawCurve(Color color, float width)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(width, CurvePoints);
            Handles.EndGUI();
        }

        private static float SampleAt(float[] samples, float previewTime)
        {
            int index = Mathf.RoundToInt(Mathf.Max(0f, previewTime) * RetroSfxSettings.SampleRate);
            if (index < 0 || index >= samples.Length)
            {
                return 0f;
            }

            return Mathf.Clamp(samples[index], -1f, 1f);
        }

        private static float Gaussian(float value, float center, float width)
        {
            float normalized = (value - center) / Mathf.Max(0.0001f, width);
            return Mathf.Exp(-normalized * normalized);
        }

        private static GUIStyle GetLabelStyle(bool enabled)
        {
            if (enabled)
            {
                if (activeLabelStyle == null)
                {
                    activeLabelStyle = new GUIStyle(RetroSfxSynthGui.TinyStyle)
                    {
                        alignment = TextAnchor.UpperLeft
                    };
                    activeLabelStyle.normal.textColor = RetroSfxSynthGui.Text;
                }

                return activeLabelStyle;
            }

            if (bypassedLabelStyle == null)
            {
                bypassedLabelStyle = new GUIStyle(RetroSfxSynthGui.TinyStyle)
                {
                    alignment = TextAnchor.UpperLeft
                };
                bypassedLabelStyle.normal.textColor = RetroSfxSynthGui.MutedText;
            }

            return bypassedLabelStyle;
        }
    }
}
