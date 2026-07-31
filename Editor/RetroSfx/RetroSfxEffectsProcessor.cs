using System;
using System.Collections.Generic;
using UnityEngine;

namespace DansToolbox.EditorTools.Audio
{
    internal enum RetroSfxEffectType
    {
        Filter,
        Equalizer,
        Compressor,
        Distortion,
        Chorus,
        Delay,
        Reverb
    }

    internal enum RetroSfxFilterMode
    {
        LowPass,
        BandPass,
        HighPass
    }

    [Serializable]
    internal sealed class RetroSfxEffectSettings
    {
        public string Id = Guid.NewGuid().ToString("N");
        public RetroSfxEffectType Type;
        public RetroSfxFilterMode FilterMode = RetroSfxFilterMode.LowPass;
        public bool Enabled = true;
        public bool Expanded = true;
        public float ParameterA;
        public float ParameterB;
        public float ParameterC;
        public float ParameterD;
        public float ParameterE;

        internal static RetroSfxEffectSettings Create(RetroSfxEffectType type)
        {
            RetroSfxEffectSettings effect = new RetroSfxEffectSettings
            {
                Type = type
            };

            switch (type)
            {
                case RetroSfxEffectType.Filter:
                    effect.ParameterA = 2500f;
                    effect.ParameterB = 0.15f;
                    effect.ParameterC = 1f;
                    break;
                case RetroSfxEffectType.Equalizer:
                    effect.ParameterA = 0f;
                    effect.ParameterB = 0f;
                    effect.ParameterC = 0f;
                    effect.ParameterD = 1f;
                    break;
                case RetroSfxEffectType.Compressor:
                    effect.ParameterA = -18f;
                    effect.ParameterB = 4f;
                    effect.ParameterC = 0.01f;
                    effect.ParameterD = 0.12f;
                    effect.ParameterE = 3f;
                    break;
                case RetroSfxEffectType.Distortion:
                    effect.ParameterA = 4f;
                    effect.ParameterB = 0.65f;
                    effect.ParameterC = 0.5f;
                    break;
                case RetroSfxEffectType.Chorus:
                    effect.ParameterA = 0.8f;
                    effect.ParameterB = 0.004f;
                    effect.ParameterC = 0.012f;
                    effect.ParameterD = 0.35f;
                    break;
                case RetroSfxEffectType.Delay:
                    effect.ParameterA = 0.22f;
                    effect.ParameterB = 0.35f;
                    effect.ParameterC = 0.3f;
                    break;
                case RetroSfxEffectType.Reverb:
                    effect.ParameterA = 0.55f;
                    effect.ParameterB = 1.4f;
                    effect.ParameterC = 0.35f;
                    effect.ParameterD = 0.3f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            return effect;
        }

        internal void EnsureId()
        {
            if (string.IsNullOrEmpty(Id))
            {
                Id = Guid.NewGuid().ToString("N");
            }
        }

        internal RetroSfxEffectSettings Clone()
        {
            return new RetroSfxEffectSettings
            {
                Id = Id,
                Type = Type,
                FilterMode = FilterMode,
                Enabled = Enabled,
                Expanded = Expanded,
                ParameterA = ParameterA,
                ParameterB = ParameterB,
                ParameterC = ParameterC,
                ParameterD = ParameterD,
                ParameterE = ParameterE
            };
        }
    }

    /// <summary>
    /// Offline mono DSP chain used by editor preview and WAV rendering.
    /// Modules are processed strictly in list order.
    /// </summary>
    internal static class RetroSfxEffectsProcessor
    {
        internal delegate float EffectParameterEvaluator(
            RetroSfxEffectSettings effect,
            int parameterIndex,
            int sampleIndex,
            float fallback);

        private const float TwoPi = Mathf.PI * 2f;
        private const float MinimumLevel = 0.000001f;
        private const float MaximumProcessedDuration = 12f;

        internal static float[] Process(
            float[] input,
            int sampleRate,
            IList<RetroSfxEffectSettings> effects)
        {
            return Process(input, sampleRate, effects, null);
        }

        internal static float[] Process(
            float[] input,
            int sampleRate,
            IList<RetroSfxEffectSettings> effects,
            IList<float[]> stageOutputs)
        {
            return Process(input, sampleRate, effects, stageOutputs, null);
        }

        internal static float[] Process(
            float[] input,
            int sampleRate,
            IList<RetroSfxEffectSettings> effects,
            IList<float[]> stageOutputs,
            EffectParameterEvaluator parameterEvaluator)
        {
            return Process(
                input,
                sampleRate,
                effects,
                stageOutputs,
                parameterEvaluator,
                Mathf.CeilToInt(MaximumProcessedDuration * sampleRate));
        }

        internal static float[] Process(
            float[] input,
            int sampleRate,
            IList<RetroSfxEffectSettings> effects,
            IList<float[]> stageOutputs,
            EffectParameterEvaluator parameterEvaluator,
            int maximumOutputSamples)
        {
            maximumOutputSamples = Mathf.Max(input?.Length ?? 0, maximumOutputSamples);
            stageOutputs?.Clear();
            if (input == null || input.Length == 0)
            {
                float[] empty = input ?? Array.Empty<float>();
                if (stageOutputs != null && effects != null)
                {
                    for (int index = 0; index < effects.Count; index++)
                    {
                        stageOutputs.Add(empty);
                    }
                }
                return empty;
            }

            if (effects == null || effects.Count == 0)
            {
                return input;
            }

            float[] output = input;
            bool processed = false;
            foreach (RetroSfxEffectSettings effect in effects)
            {
                if (effect == null || !effect.Enabled)
                {
                    stageOutputs?.Add(output);
                    continue;
                }

                processed = true;
                switch (effect.Type)
                {
                    case RetroSfxEffectType.Filter:
                        output = ApplyFilter(output, sampleRate, effect, parameterEvaluator);
                        break;
                    case RetroSfxEffectType.Equalizer:
                        output = ApplyEqualizer(output, sampleRate, effect, parameterEvaluator);
                        break;
                    case RetroSfxEffectType.Compressor:
                        output = ApplyCompressor(output, sampleRate, effect, parameterEvaluator);
                        break;
                    case RetroSfxEffectType.Distortion:
                        output = ApplyDistortion(output, sampleRate, effect, parameterEvaluator);
                        break;
                    case RetroSfxEffectType.Chorus:
                        output = ApplyChorus(output, sampleRate, effect, parameterEvaluator);
                        break;
                    case RetroSfxEffectType.Delay:
                        output = ApplyDelay(
                            output,
                            sampleRate,
                            effect,
                            parameterEvaluator,
                            maximumOutputSamples);
                        break;
                    case RetroSfxEffectType.Reverb:
                        output = ApplyReverb(
                            output,
                            sampleRate,
                            effect,
                            parameterEvaluator,
                            maximumOutputSamples);
                        break;
                }

                stageOutputs?.Add(output);
            }

            if (!processed)
            {
                return input;
            }

            for (int index = 0; index < output.Length; index++)
            {
                float sample = output[index];
                output[index] = float.IsNaN(sample) || float.IsInfinity(sample)
                    ? 0f
                    : Mathf.Clamp(sample, -1f, 1f);
            }

            return output;
        }

        private static float[] ApplyFilter(
            float[] input,
            int sampleRate,
            RetroSfxEffectSettings effect,
            EffectParameterEvaluator evaluator)
        {
            float[] output = new float[input.Length];
            float x1 = 0f;
            float x2 = 0f;
            float y1 = 0f;
            float y2 = 0f;
            for (int index = 0; index < input.Length; index++)
            {
                float cutoff = Mathf.Clamp(
                    Evaluate(evaluator, effect, 0, index, effect.ParameterA),
                    30f,
                    sampleRate * 0.45f);
                float resonance = Mathf.Clamp01(
                    Evaluate(evaluator, effect, 1, index, effect.ParameterB));
                float mix = Mathf.Clamp01(
                    Evaluate(evaluator, effect, 2, index, effect.ParameterC));
                float omega = TwoPi * cutoff / sampleRate;
                float cosine = Mathf.Cos(omega);
                float sine = Mathf.Sin(omega);
                float quality = Mathf.Lerp(0.5f, 12f, resonance);
                float alpha = sine / (2f * quality);
                float b0;
                float b1;
                float b2;
                switch (effect.FilterMode)
                {
                    case RetroSfxFilterMode.BandPass:
                        b0 = alpha;
                        b1 = 0f;
                        b2 = -alpha;
                        break;
                    case RetroSfxFilterMode.HighPass:
                        b0 = (1f + cosine) * 0.5f;
                        b1 = -(1f + cosine);
                        b2 = b0;
                        break;
                    default:
                        b0 = (1f - cosine) * 0.5f;
                        b1 = 1f - cosine;
                        b2 = b0;
                        break;
                }
                float a0 = 1f + alpha;
                float a1 = -2f * cosine;
                float a2 = 1f - alpha;
                b0 /= a0;
                b1 /= a0;
                b2 /= a0;
                a1 /= a0;
                a2 /= a0;

                float dry = input[index];
                float wet = b0 * dry + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = dry;
                y2 = y1;
                y1 = wet;
                output[index] = Mathf.Lerp(dry, wet, mix);
            }

            return output;
        }

        private static float[] ApplyEqualizer(
            float[] input,
            int sampleRate,
            RetroSfxEffectSettings effect,
            EffectParameterEvaluator evaluator)
        {
            float lowCoefficient = 1f - Mathf.Exp(-TwoPi * 250f / sampleRate);
            float highCoefficient = 1f - Mathf.Exp(-TwoPi * 4000f / sampleRate);
            float lowState = 0f;
            float highLowPassState = 0f;
            float[] output = new float[input.Length];

            for (int index = 0; index < input.Length; index++)
            {
                float lowGain = DecibelsToLinear(Mathf.Clamp(
                    Evaluate(evaluator, effect, 0, index, effect.ParameterA),
                    -18f,
                    18f));
                float midGain = DecibelsToLinear(Mathf.Clamp(
                    Evaluate(evaluator, effect, 1, index, effect.ParameterB),
                    -18f,
                    18f));
                float highGain = DecibelsToLinear(Mathf.Clamp(
                    Evaluate(evaluator, effect, 2, index, effect.ParameterC),
                    -18f,
                    18f));
                float mix = Mathf.Clamp01(
                    Evaluate(evaluator, effect, 3, index, effect.ParameterD));
                float dry = input[index];
                lowState += lowCoefficient * (dry - lowState);
                highLowPassState += highCoefficient * (dry - highLowPassState);
                float low = lowState;
                float high = dry - highLowPassState;
                float mid = dry - low - high;
                float wet = low * lowGain + mid * midGain + high * highGain;
                output[index] = Mathf.Lerp(dry, wet, mix);
            }

            return output;
        }

        private static float[] ApplyCompressor(
            float[] input,
            int sampleRate,
            RetroSfxEffectSettings effect,
            EffectParameterEvaluator evaluator)
        {
            float envelope = 0f;
            float[] output = new float[input.Length];

            for (int index = 0; index < input.Length; index++)
            {
                float threshold = Mathf.Clamp(
                    Evaluate(evaluator, effect, 0, index, effect.ParameterA),
                    -48f,
                    0f);
                float ratio = Mathf.Clamp(
                    Evaluate(evaluator, effect, 1, index, effect.ParameterB),
                    1f,
                    20f);
                float attack = Mathf.Clamp(
                    Evaluate(evaluator, effect, 2, index, effect.ParameterC),
                    0.001f,
                    0.1f);
                float release = Mathf.Clamp(
                    Evaluate(evaluator, effect, 3, index, effect.ParameterD),
                    0.01f,
                    0.5f);
                float makeup = Mathf.Clamp(
                    Evaluate(evaluator, effect, 4, index, effect.ParameterE),
                    0f,
                    18f);
                float attackCoefficient = Mathf.Exp(-1f / (attack * sampleRate));
                float releaseCoefficient = Mathf.Exp(-1f / (release * sampleRate));
                float sample = input[index];
                float level = Mathf.Abs(sample);
                float coefficient = level > envelope ? attackCoefficient : releaseCoefficient;
                envelope = coefficient * envelope + (1f - coefficient) * level;
                float levelDecibels = 20f * Mathf.Log10(Mathf.Max(MinimumLevel, envelope));
                float over = Mathf.Max(0f, levelDecibels - threshold);
                float reduction = over - over / ratio;
                output[index] = sample * DecibelsToLinear(makeup - reduction);
            }

            return output;
        }

        private static float[] ApplyDistortion(
            float[] input,
            int sampleRate,
            RetroSfxEffectSettings effect,
            EffectParameterEvaluator evaluator)
        {
            float toneState = 0f;
            float[] output = new float[input.Length];

            for (int index = 0; index < input.Length; index++)
            {
                float drive = Mathf.Clamp(
                    Evaluate(evaluator, effect, 0, index, effect.ParameterA),
                    1f,
                    20f);
                float tone = Mathf.Clamp01(
                    Evaluate(evaluator, effect, 1, index, effect.ParameterB));
                float mix = Mathf.Clamp01(
                    Evaluate(evaluator, effect, 2, index, effect.ParameterC));
                float toneCutoff = Mathf.Lerp(800f, 16000f, tone);
                float coefficient = 1f - Mathf.Exp(-TwoPi * toneCutoff / sampleRate);
                float normalization = Mathf.Max(0.001f, (float)Math.Tanh(drive));
                float dry = input[index];
                float shaped = (float)Math.Tanh(dry * drive) / normalization;
                toneState += coefficient * (shaped - toneState);
                output[index] = Mathf.Lerp(dry, toneState, mix);
            }

            return output;
        }

        private static float[] ApplyChorus(
            float[] input,
            int sampleRate,
            RetroSfxEffectSettings effect,
            EffectParameterEvaluator evaluator)
        {
            float[] output = new float[input.Length];

            for (int index = 0; index < input.Length; index++)
            {
                float rate = Mathf.Clamp(
                    Evaluate(evaluator, effect, 0, index, effect.ParameterA),
                    0.05f,
                    8f);
                float depth = Mathf.Clamp(
                    Evaluate(evaluator, effect, 1, index, effect.ParameterB),
                    0f,
                    0.012f);
                float delay = Mathf.Clamp(
                    Evaluate(evaluator, effect, 2, index, effect.ParameterC),
                    0.004f,
                    0.03f);
                float mix = Mathf.Clamp01(
                    Evaluate(evaluator, effect, 3, index, effect.ParameterD));
                float modulation = Mathf.Sin(TwoPi * rate * index / sampleRate);
                float delaySamples = Mathf.Max(1f, (delay + depth * modulation) * sampleRate);
                float readPosition = index - delaySamples;
                float delayed = ReadInterpolated(input, readPosition);
                float wet = (input[index] + delayed) * 0.7f;
                output[index] = Mathf.Lerp(input[index], wet, mix);
            }

            return output;
        }

        private static float[] ApplyDelay(
            float[] input,
            int sampleRate,
            RetroSfxEffectSettings effect,
            EffectParameterEvaluator evaluator,
            int maximumOutputSamples)
        {
            int outputLength = Mathf.Min(
                maximumOutputSamples,
                input.Length + Mathf.CeilToInt(3f * sampleRate));
            float[] feedbackLine = new float[outputLength];
            float[] output = new float[outputLength];

            for (int index = 0; index < outputLength; index++)
            {
                float delaySeconds = Mathf.Clamp(
                    Evaluate(evaluator, effect, 0, index, effect.ParameterA),
                    0.03f,
                    1f);
                float feedback = Mathf.Clamp(
                    Evaluate(evaluator, effect, 1, index, effect.ParameterB),
                    0f,
                    0.9f);
                float mix = Mathf.Clamp01(
                    Evaluate(evaluator, effect, 2, index, effect.ParameterC));
                int delaySamples = Mathf.Max(1, Mathf.RoundToInt(delaySeconds * sampleRate));
                float dry = index < input.Length ? input[index] : 0f;
                float delayed = index >= delaySamples ? feedbackLine[index - delaySamples] : 0f;
                feedbackLine[index] = dry + delayed * feedback;
                output[index] = dry * (1f - mix) + delayed * mix;
            }

            return output;
        }

        private static float[] ApplyReverb(
            float[] input,
            int sampleRate,
            RetroSfxEffectSettings effect,
            EffectParameterEvaluator evaluator,
            int maximumOutputSamples)
        {
            int outputLength = Mathf.Min(
                maximumOutputSamples,
                input.Length + Mathf.CeilToInt(4f * sampleRate));
            float[] delaySeconds = { 0.0297f, 0.0371f, 0.0411f, 0.0437f };
            float[][] buffers = new float[delaySeconds.Length][];
            int[] positions = new int[delaySeconds.Length];
            float[] dampingStates = new float[delaySeconds.Length];

            for (int comb = 0; comb < delaySeconds.Length; comb++)
            {
                int bufferLength = Mathf.Max(
                    2,
                    Mathf.CeilToInt(delaySeconds[comb] * 1.5f * sampleRate) + 1);
                buffers[comb] = new float[bufferLength];
            }

            float[] output = new float[outputLength];
            for (int index = 0; index < outputLength; index++)
            {
                float roomSize = Mathf.Clamp01(
                    Evaluate(evaluator, effect, 0, index, effect.ParameterA));
                float decay = Mathf.Clamp(
                    Evaluate(evaluator, effect, 1, index, effect.ParameterB),
                    0.2f,
                    4f);
                float damping = Mathf.Clamp01(
                    Evaluate(evaluator, effect, 2, index, effect.ParameterC));
                float mix = Mathf.Clamp01(
                    Evaluate(evaluator, effect, 3, index, effect.ParameterD));
                float roomScale = Mathf.Lerp(0.75f, 1.5f, roomSize);
                float dry = index < input.Length ? input[index] : 0f;
                float wet = 0f;
                for (int comb = 0; comb < buffers.Length; comb++)
                {
                    float scaledDelay = delaySeconds[comb] * roomScale;
                    int delaySamples = Mathf.Clamp(
                        Mathf.RoundToInt(scaledDelay * sampleRate),
                        1,
                        buffers[comb].Length - 1);
                    int readPosition =
                        (positions[comb] - delaySamples + buffers[comb].Length) %
                        buffers[comb].Length;
                    float delayed = buffers[comb][readPosition];
                    float filtered = delayed * (1f - damping) + dampingStates[comb] * damping;
                    dampingStates[comb] = filtered;
                    float feedback = Mathf.Pow(0.001f, scaledDelay / decay);
                    buffers[comb][positions[comb]] = dry + filtered * feedback;
                    positions[comb] = (positions[comb] + 1) % buffers[comb].Length;
                    wet += delayed;
                }

                wet /= buffers.Length;
                output[index] = dry * (1f - mix) + wet * mix;
            }

            return output;
        }

        private static float ReadInterpolated(float[] samples, float position)
        {
            if (position <= 0f)
            {
                return 0f;
            }

            int lower = Mathf.FloorToInt(position);
            if (lower >= samples.Length - 1)
            {
                return samples[samples.Length - 1];
            }

            float fraction = position - lower;
            return Mathf.Lerp(samples[lower], samples[lower + 1], fraction);
        }

        private static float Evaluate(
            EffectParameterEvaluator evaluator,
            RetroSfxEffectSettings effect,
            int parameterIndex,
            int sampleIndex,
            float fallback)
        {
            return evaluator == null
                ? fallback
                : evaluator(effect, parameterIndex, sampleIndex, fallback);
        }

        private static float DecibelsToLinear(float decibels)
        {
            return Mathf.Pow(10f, decibels / 20f);
        }
    }
}
