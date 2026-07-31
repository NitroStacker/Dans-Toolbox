using System;
using UnityEngine;

namespace DansToolbox.EditorTools.Audio
{
    internal enum RetroSfxPreset
    {
        Coin,
        Laser,
        Explosion,
        Jump,
        Hit,
        PowerUp,
        Click,
        BlipSelect,
        Synth,
        Random
    }

    /// <summary>
    /// Ports the family-specific random preset recipes used by sfxr.me into the
    /// editor tool's human-readable frequency and time units.
    /// </summary>
    internal static class RetroSfxPresetFactory
    {
        private const float SfxrOversampling = 8f;
        private const float MaximumUiFrequency = 4000f;
        private const float MaximumUiSlide = 8000f;
        private static readonly object RandomLock = new object();
        private static readonly System.Random SeedSource = new System.Random();

        internal static RetroSfxSettings Create(RetroSfxPreset preset)
        {
            int seed;
            lock (RandomLock)
            {
                seed = SeedSource.Next();
            }

            return Create(preset, seed);
        }

        internal static RetroSfxSettings Create(RetroSfxPreset preset, int seed)
        {
            System.Random random = new System.Random(seed);
            RetroSfxSettings settings = CreateSfxrDefaults(seed);

            switch (preset)
            {
                case RetroSfxPreset.Coin:
                    ApplyCoin(settings, random);
                    break;
                case RetroSfxPreset.Laser:
                    ApplyLaser(settings, random);
                    break;
                case RetroSfxPreset.Explosion:
                    ApplyExplosion(settings, random);
                    break;
                case RetroSfxPreset.Jump:
                    ApplyJump(settings, random);
                    break;
                case RetroSfxPreset.Hit:
                    ApplyHit(settings, random);
                    break;
                case RetroSfxPreset.PowerUp:
                    ApplyPowerUp(settings, random);
                    break;
                case RetroSfxPreset.Click:
                    ApplyClick(settings, random);
                    break;
                case RetroSfxPreset.BlipSelect:
                    ApplyBlipSelect(settings, random);
                    break;
                case RetroSfxPreset.Synth:
                    ApplySynth(settings, random);
                    break;
                case RetroSfxPreset.Random:
                    ApplyRandom(settings, random);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
            }

            return settings;
        }

        private static RetroSfxSettings CreateSfxrDefaults(int seed)
        {
            return new RetroSfxSettings
            {
                WaveType = RetroWaveType.Square,
                MasterVolume = 0.5f,
                AttackTime = 0f,
                SustainTime = EnvelopeSeconds(0.3f),
                SustainPunch = 0f,
                DecayTime = EnvelopeSeconds(0.4f),
                StartFrequency = FrequencyHz(0.3f),
                FrequencySlide = 0f,
                VibratoDepth = 0f,
                VibratoRate = 0f,
                DutyCycle = 0.5f,
                ArpeggioOffset = 0f,
                ArpeggioTime = 0f,
                RepeatRate = 0f,
                BitCrushAmount = 0f,
                Seed = seed
            };
        }

        private static void ApplyCoin(RetroSfxSettings settings, System.Random random)
        {
            float baseFrequency = 0.4f + Frnd(random, 0.5f);
            settings.WaveType = RetroWaveType.Saw;
            settings.StartFrequency = FrequencyHz(baseFrequency);
            settings.AttackTime = 0f;
            settings.SustainTime = EnvelopeSeconds(Frnd(random, 0.1f));
            settings.DecayTime = EnvelopeSeconds(0.1f + Frnd(random, 0.4f));
            settings.SustainPunch = 0.3f + Frnd(random, 0.3f);

            if (Rnd(random, 1) != 0)
            {
                float arpeggioSpeed = 0.5f + Frnd(random, 0.2f);
                float arpeggioAmount = 0.2f + Frnd(random, 0.4f);
                settings.ArpeggioTime = ArpeggioSeconds(arpeggioSpeed);
                settings.ArpeggioOffset = ArpeggioSemitones(arpeggioAmount);
            }
        }

        private static void ApplyLaser(RetroSfxSettings settings, System.Random random)
        {
            int waveType = Rnd(random, 2);
            if (waveType == (int)RetroWaveType.Sine && Rnd(random, 1) != 0)
            {
                waveType = Rnd(random, 1);
            }

            settings.WaveType = (RetroWaveType)waveType;
            float baseFrequency;
            float frequencyRamp;
            if (Rnd(random, 2) == 0)
            {
                baseFrequency = 0.3f + Frnd(random, 0.6f);
                frequencyRamp = -0.35f - Frnd(random, 0.3f);
            }
            else
            {
                baseFrequency = 0.5f + Frnd(random, 0.5f);
                frequencyRamp = -0.15f - Frnd(random, 0.2f);
            }

            float duty = settings.WaveType == RetroWaveType.Saw ? 1f : 0f;
            duty = Rnd(random, 1) != 0
                ? Frnd(random, 0.5f)
                : 0.4f + Frnd(random, 0.5f);

            settings.DutyCycle = DutyCycle(duty);
            settings.AttackTime = 0f;
            settings.SustainTime = EnvelopeSeconds(0.1f + Frnd(random, 0.2f));
            settings.DecayTime = EnvelopeSeconds(Frnd(random, 0.4f));
            if (Rnd(random, 1) != 0)
            {
                settings.SustainPunch = Frnd(random, 0.3f);
            }

            ApplyPitch(settings, baseFrequency, frequencyRamp);
        }

        private static float ApplyExplosion(RetroSfxSettings settings, System.Random random)
        {
            settings.WaveType = RetroWaveType.Noise;
            float baseFrequency;
            float frequencyRamp;
            if (Rnd(random, 1) != 0)
            {
                baseFrequency = Square(0.1f + Frnd(random, 0.4f));
                frequencyRamp = -0.1f + Frnd(random, 0.4f);
            }
            else
            {
                baseFrequency = Square(0.2f + Frnd(random, 0.7f));
                frequencyRamp = -0.2f - Frnd(random, 0.2f);
            }

            if (Rnd(random, 4) == 0)
            {
                frequencyRamp = 0f;
            }

            if (Rnd(random, 2) == 0)
            {
                settings.RepeatRate = RepeatRate(0.3f + Frnd(random, 0.5f));
            }

            settings.AttackTime = 0f;
            settings.SustainTime = EnvelopeSeconds(0.1f + Frnd(random, 0.3f));
            settings.DecayTime = EnvelopeSeconds(Frnd(random, 0.5f));
            settings.SustainPunch = 0.2f + Frnd(random, 0.6f);

            if (Rnd(random, 1) != 0)
            {
                SetVibrato(settings, Frnd(random, 0.7f), Frnd(random, 0.6f));
            }

            if (Rnd(random, 2) == 0)
            {
                float arpeggioSpeed = 0.6f + Frnd(random, 0.3f);
                float arpeggioAmount = 0.8f - Frnd(random, 1.6f);
                settings.ArpeggioTime = ArpeggioSeconds(arpeggioSpeed);
                settings.ArpeggioOffset = ArpeggioSemitones(arpeggioAmount);
            }

            ApplyPitch(settings, baseFrequency, frequencyRamp);
            return frequencyRamp;
        }

        private static void ApplyPowerUp(RetroSfxSettings settings, System.Random random)
        {
            if (Rnd(random, 1) != 0)
            {
                settings.WaveType = RetroWaveType.Saw;
                settings.DutyCycle = DutyCycle(1f);
            }
            else
            {
                settings.DutyCycle = DutyCycle(Frnd(random, 0.6f));
            }

            float baseFrequency = 0.2f + Frnd(random, 0.3f);
            float frequencyRamp;
            if (Rnd(random, 1) != 0)
            {
                frequencyRamp = 0.1f + Frnd(random, 0.4f);
                settings.RepeatRate = RepeatRate(0.4f + Frnd(random, 0.4f));
            }
            else
            {
                frequencyRamp = 0.05f + Frnd(random, 0.2f);
                if (Rnd(random, 1) != 0)
                {
                    SetVibrato(settings, Frnd(random, 0.7f), Frnd(random, 0.6f));
                }
            }

            settings.AttackTime = 0f;
            settings.SustainTime = EnvelopeSeconds(Frnd(random, 0.4f));
            settings.DecayTime = EnvelopeSeconds(0.1f + Frnd(random, 0.4f));
            ApplyPitch(settings, baseFrequency, frequencyRamp);
        }

        private static float ApplyHit(RetroSfxSettings settings, System.Random random)
        {
            int waveType = Rnd(random, 2);
            if (waveType == (int)RetroWaveType.Sine)
            {
                waveType = (int)RetroWaveType.Noise;
            }

            settings.WaveType = (RetroWaveType)waveType;
            if (settings.WaveType == RetroWaveType.Square)
            {
                settings.DutyCycle = DutyCycle(Frnd(random, 0.6f));
            }
            else if (settings.WaveType == RetroWaveType.Saw)
            {
                settings.DutyCycle = DutyCycle(1f);
            }

            float baseFrequency = 0.2f + Frnd(random, 0.6f);
            float frequencyRamp = -0.3f - Frnd(random, 0.4f);
            settings.AttackTime = 0f;
            settings.SustainTime = EnvelopeSeconds(Frnd(random, 0.1f));
            settings.DecayTime = EnvelopeSeconds(0.1f + Frnd(random, 0.2f));
            ApplyPitch(settings, baseFrequency, frequencyRamp);
            return frequencyRamp;
        }

        private static void ApplyJump(RetroSfxSettings settings, System.Random random)
        {
            settings.WaveType = RetroWaveType.Square;
            settings.DutyCycle = DutyCycle(Frnd(random, 0.6f));
            float baseFrequency = 0.3f + Frnd(random, 0.3f);
            float frequencyRamp = 0.1f + Frnd(random, 0.2f);
            settings.AttackTime = 0f;
            settings.SustainTime = EnvelopeSeconds(0.1f + Frnd(random, 0.3f));
            settings.DecayTime = EnvelopeSeconds(0.1f + Frnd(random, 0.2f));
            ApplyPitch(settings, baseFrequency, frequencyRamp);
        }

        private static void ApplyClick(RetroSfxSettings settings, System.Random random)
        {
            float normalizedRamp;
            if (Rnd(random, 1) == 0)
            {
                normalizedRamp = ApplyExplosion(settings, random);
            }
            else
            {
                normalizedRamp = ApplyHit(settings, random);
            }

            if (Rnd(random, 1) != 0)
            {
                normalizedRamp = -0.5f + Frnd(random, 1f);
            }

            if (Rnd(random, 1) != 0)
            {
                float sustainScale = Frnd(random, 0.4f) + 0.2f;
                float decayScale = Frnd(random, 0.4f) + 0.2f;
                settings.SustainTime *= sustainScale * sustainScale;
                settings.DecayTime *= decayScale * decayScale;
            }

            if (Rnd(random, 3) == 0)
            {
                settings.AttackTime = EnvelopeSeconds(Frnd(random, 0.3f));
            }

            float clickBaseFrequency = 1f - Frnd(random, 0.25f);
            ApplyPitch(settings, clickBaseFrequency, normalizedRamp);
        }

        private static void ApplyBlipSelect(RetroSfxSettings settings, System.Random random)
        {
            settings.WaveType = (RetroWaveType)Rnd(random, 1);
            settings.DutyCycle = settings.WaveType == RetroWaveType.Square
                ? DutyCycle(Frnd(random, 0.6f))
                : DutyCycle(1f);
            settings.StartFrequency = FrequencyHz(0.2f + Frnd(random, 0.4f));
            settings.AttackTime = 0f;
            settings.SustainTime = EnvelopeSeconds(0.1f + Frnd(random, 0.1f));
            settings.DecayTime = EnvelopeSeconds(Frnd(random, 0.2f));
        }

        private static void ApplySynth(RetroSfxSettings settings, System.Random random)
        {
            settings.WaveType = (RetroWaveType)Rnd(random, 1);
            float[] noteFrequencies =
            {
                0.2723171360931539f,
                0.19255692561524382f,
                0.13615778746815113f
            };
            settings.StartFrequency = FrequencyHz(noteFrequencies[Rnd(random, 2)]);
            settings.AttackTime = Rnd(random, 4) > 3
                ? EnvelopeSeconds(Frnd(random, 0.5f))
                : 0f;
            settings.SustainTime = EnvelopeSeconds(Frnd(random, 1f));
            settings.SustainPunch = Frnd(random, 1f);
            settings.DecayTime = EnvelopeSeconds(Frnd(random, 0.9f) + 0.1f);

            float[] arpeggioAmounts = { 0f, 0f, 0f, 0f, -0.3162f, 0.7454f, 0.7454f };
            float arpeggioAmount = arpeggioAmounts[Rnd(random, 6)];
            settings.ArpeggioOffset = Mathf.Clamp(
                ArpeggioSemitones(arpeggioAmount),
                -24f,
                24f);
            settings.ArpeggioTime = ArpeggioSeconds(Frnd(random, 0.5f) + 0.4f);
            settings.DutyCycle = DutyCycle(Frnd(random, 1f));
        }

        private static void ApplyRandom(RetroSfxSettings settings, System.Random random)
        {
            settings.WaveType = (RetroWaveType)Rnd(random, 3);

            float baseFrequency = Rnd(random, 1) != 0
                ? Cube(Frnd(random, 2f) - 1f) + 0.5f
                : Square(Frnd(random, 1f));
            float frequencyRamp = Mathf.Pow(Frnd(random, 2f) - 1f, 5f);
            if (baseFrequency > 0.7f && frequencyRamp > 0.2f ||
                baseFrequency < 0.2f && frequencyRamp < -0.05f)
            {
                frequencyRamp = -frequencyRamp;
            }

            settings.DutyCycle = DutyCycle(Frnd(random, 2f) - 1f);
            SetVibrato(
                settings,
                Cube(Frnd(random, 2f) - 1f),
                RandomRange(random, -1f, 1f));

            float attack = Cube(RandomRange(random, -1f, 1f));
            float sustain = Square(RandomRange(random, -1f, 1f));
            float decay = RandomRange(random, -1f, 1f);
            settings.SustainPunch = Square(Frnd(random, 0.8f));
            if (attack + sustain + decay < 0.2f)
            {
                sustain += 0.2f + Frnd(random, 0.3f);
                decay += 0.2f + Frnd(random, 0.3f);
            }

            settings.AttackTime = EnvelopeSeconds(attack);
            settings.SustainTime = EnvelopeSeconds(sustain);
            settings.DecayTime = EnvelopeSeconds(decay);

            float repeatSpeed = Frnd(random, 2f) - 1f;
            settings.RepeatRate = RepeatRate(repeatSpeed);
            float arpeggioSpeed = Frnd(random, 2f) - 1f;
            float arpeggioAmount = Frnd(random, 2f) - 1f;
            settings.ArpeggioTime = ArpeggioSeconds(arpeggioSpeed);
            settings.ArpeggioOffset = Mathf.Clamp(
                ArpeggioSemitones(arpeggioAmount),
                -24f,
                24f);
            ApplyPitch(settings, baseFrequency, frequencyRamp);
        }

        private static void ApplyPitch(
            RetroSfxSettings settings,
            float normalizedBaseFrequency,
            float normalizedRamp)
        {
            settings.StartFrequency = FrequencyHz(normalizedBaseFrequency);
            if (Mathf.Approximately(normalizedRamp, 0f) || settings.Duration <= 0f)
            {
                settings.FrequencySlide = 0f;
                return;
            }

            float periodMultiplier = 1f - normalizedRamp * normalizedRamp * normalizedRamp * 0.01f;
            int sampleCount = Mathf.Max(
                1,
                Mathf.RoundToInt(settings.Duration * RetroSfxSettings.SampleRate));
            double endFrequency = settings.StartFrequency /
                                  Math.Pow(periodMultiplier, sampleCount);
            endFrequency = Math.Max(20d, Math.Min(18000d, endFrequency));
            settings.FrequencySlide = Mathf.Clamp(
                ((float)endFrequency - settings.StartFrequency) / settings.Duration,
                -MaximumUiSlide,
                MaximumUiSlide);
        }

        private static void SetVibrato(
            RetroSfxSettings settings,
            float normalizedStrength,
            float normalizedSpeed)
        {
            settings.VibratoDepth = Mathf.Clamp01(normalizedStrength * 0.5f);
            settings.VibratoRate = Mathf.Clamp(
                normalizedSpeed * normalizedSpeed * 0.01f *
                RetroSfxSettings.SampleRate / (Mathf.PI * 2f),
                0f,
                30f);
        }

        private static float FrequencyHz(float normalizedFrequency)
        {
            float frequency = RetroSfxSettings.SampleRate * SfxrOversampling *
                              (normalizedFrequency * normalizedFrequency + 0.001f) / 100f;
            return Mathf.Clamp(frequency, 20f, MaximumUiFrequency);
        }

        private static float EnvelopeSeconds(float normalizedTime)
        {
            return normalizedTime * normalizedTime * 100000f / RetroSfxSettings.SampleRate;
        }

        private static float DutyCycle(float normalizedDuty)
        {
            return Mathf.Clamp(0.5f - normalizedDuty * 0.5f, 0.05f, 0.95f);
        }

        private static float ArpeggioSeconds(float normalizedSpeed)
        {
            if (Mathf.Approximately(normalizedSpeed, 1f))
            {
                return 0f;
            }

            float sampleCount = Square(1f - normalizedSpeed) * 20000f + 32f;
            return sampleCount / RetroSfxSettings.SampleRate;
        }

        private static float ArpeggioSemitones(float normalizedAmount)
        {
            float periodMultiplier = normalizedAmount >= 0f
                ? 1f - Square(normalizedAmount) * 0.9f
                : 1f + Square(normalizedAmount) * 10f;
            float frequencyMultiplier = 1f / Mathf.Max(0.0001f, periodMultiplier);
            return 12f * Mathf.Log(frequencyMultiplier, 2f);
        }

        private static float RepeatRate(float normalizedSpeed)
        {
            if (Mathf.Approximately(normalizedSpeed, 0f))
            {
                return 0f;
            }

            float sampleCount = Square(1f - normalizedSpeed) * 20000f + 32f;
            return Mathf.Clamp(RetroSfxSettings.SampleRate / sampleCount, 0f, 40f);
        }

        private static int Rnd(System.Random random, int inclusiveMaximum)
        {
            return random.Next(inclusiveMaximum + 1);
        }

        private static float Frnd(System.Random random, float range)
        {
            return (float)random.NextDouble() * range;
        }

        private static float RandomRange(System.Random random, float minimum, float maximum)
        {
            return minimum + Frnd(random, maximum - minimum);
        }

        private static float Square(float value)
        {
            return value * value;
        }

        private static float Cube(float value)
        {
            return value * value * value;
        }
    }
}
