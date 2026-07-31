using System;
using System.Collections.Generic;
using UnityEngine;

namespace DansToolbox.EditorTools.Audio
{
    internal enum RetroWaveType
    {
        Square,
        Saw,
        Sine,
        Noise
    }

    [Serializable]
    internal sealed class RetroSfxSettings
    {
        internal const int SampleRate = 44100;
        internal const float MaximumDuration = 8f;

        public RetroWaveType WaveType = RetroWaveType.Square;
        public float MasterVolume = 0.5f;
        public float AttackTime = 0.01f;
        public float SustainTime = 0.15f;
        public float SustainPunch = 0f;
        public float DecayTime = 0.2f;
        public float StartFrequency = 440f;
        public float FrequencySlide = 0f;
        public float VibratoDepth = 0f;
        public float VibratoRate = 8f;
        public float DutyCycle = 0.5f;
        public float ArpeggioOffset = 0f;
        public float ArpeggioTime = 0f;
        public float RepeatRate = 0f;
        public float BitCrushAmount = 0f;
        public int Seed = 12345;
        public List<RetroSfxEffectSettings> Effects = new List<RetroSfxEffectSettings>();

        public float Duration => Mathf.Min(MaximumDuration, AttackTime + SustainTime + DecayTime);
    }

    internal static class RetroSfxSynthesizer
    {
        private const float TwoPi = Mathf.PI * 2f;
        private const float MinimumFrequency = 20f;
        private const float MaximumFrequency = 18000f;

        /// <summary>Generates mono PCM data for the supplied retro sound settings.</summary>
        public static float[] GenerateSamples(RetroSfxSettings settings)
        {
            return RetroSfxEffectsProcessor.Process(
                GenerateDrySamples(settings),
                RetroSfxSettings.SampleRate,
                settings.Effects);
        }

        /// <summary>Generates the unprocessed signal used as the effects-chain input.</summary>
        internal static float[] GenerateDrySamples(RetroSfxSettings settings)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(settings.Duration * RetroSfxSettings.SampleRate));
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(settings.Seed);
            float phase = 0f;
            int arpeggioSample = Mathf.RoundToInt(settings.ArpeggioTime * RetroSfxSettings.SampleRate);
            int repeatSamples = settings.RepeatRate > 0f ? Mathf.Max(1, Mathf.RoundToInt(RetroSfxSettings.SampleRate / settings.RepeatRate)) : 0;

            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                int cycleIndex = repeatSamples > 0 ? sampleIndex % repeatSamples : sampleIndex;
                float time = cycleIndex / (float)RetroSfxSettings.SampleRate;
                float frequency = settings.StartFrequency + settings.FrequencySlide * time;
                if (arpeggioSample > 0 && cycleIndex >= arpeggioSample)
                {
                    frequency *= Mathf.Pow(2f, settings.ArpeggioOffset / 12f);
                }

                frequency = Mathf.Clamp(frequency, MinimumFrequency, MaximumFrequency);
                float vibrato = 1f + settings.VibratoDepth * Mathf.Sin(TwoPi * settings.VibratoRate * time);
                phase = Mathf.Repeat(phase + frequency * vibrato / RetroSfxSettings.SampleRate, 1f);
                float wave = EvaluateWave(settings.WaveType, phase, settings.DutyCycle, random);
                float sample = wave * CalculateEnvelope(time, settings) * settings.MasterVolume;
                samples[sampleIndex] = ApplyBitCrush(sample, settings.BitCrushAmount);
            }

            return samples;
        }

        private static float EvaluateWave(RetroWaveType waveType, float phase, float dutyCycle, System.Random random)
        {
            switch (waveType)
            {
                case RetroWaveType.Saw:
                    return phase * 2f - 1f;
                case RetroWaveType.Sine:
                    return Mathf.Sin(phase * TwoPi);
                case RetroWaveType.Noise:
                    return (float)(random.NextDouble() * 2d - 1d);
                default:
                    return phase < Mathf.Clamp(dutyCycle, 0.05f, 0.95f) ? 1f : -1f;
            }
        }

        private static float CalculateEnvelope(float time, RetroSfxSettings settings)
        {
            if (time < settings.AttackTime)
            {
                return settings.AttackTime <= 0f ? 1f : time / settings.AttackTime;
            }

            float decayStart = settings.AttackTime + settings.SustainTime;
            if (time < decayStart)
            {
                float sustainProgress = settings.SustainTime <= 0f
                    ? 1f
                    : Mathf.Clamp01((time - settings.AttackTime) / settings.SustainTime);
                return 1f + (1f - sustainProgress) * 2f * settings.SustainPunch;
            }

            return settings.DecayTime <= 0f ? 0f : Mathf.Clamp01(1f - (time - decayStart) / settings.DecayTime);
        }

        private static float ApplyBitCrush(float sample, float amount)
        {
            int steps = Mathf.RoundToInt(Mathf.Lerp(65536f, 8f, Mathf.Clamp01(amount)));
            return Mathf.Round(sample * steps) / steps;
        }
    }
}
