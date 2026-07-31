using System;
using UnityEngine;

namespace DansToolbox.EditorTools.Audio
{
    internal readonly struct RetroSfxEffectParameter
    {
        internal readonly string Name;
        internal readonly float Minimum;
        internal readonly float Maximum;
        internal readonly string Unit;
        internal readonly string Tooltip;

        internal RetroSfxEffectParameter(
            string name,
            float minimum,
            float maximum,
            string unit,
            string tooltip)
        {
            Name = name;
            Minimum = minimum;
            Maximum = maximum;
            Unit = unit;
            Tooltip = tooltip;
        }
    }

    /// <summary>
    /// Shared device metadata used by the sound designer, song arranger, automation,
    /// and renderer so every surface agrees about parameter names and ranges.
    /// </summary>
    internal static class RetroSfxEffectCatalog
    {
        internal static string GetDisplayName(RetroSfxEffectType type)
        {
            return type == RetroSfxEffectType.Equalizer ? "EQ THREE" : type.ToString().ToUpperInvariant();
        }

        internal static int GetParameterCount(RetroSfxEffectType type)
        {
            switch (type)
            {
                case RetroSfxEffectType.Compressor:
                    return 5;
                case RetroSfxEffectType.Equalizer:
                case RetroSfxEffectType.Chorus:
                case RetroSfxEffectType.Reverb:
                    return 4;
                default:
                    return 3;
            }
        }

        internal static RetroSfxEffectParameter GetParameter(
            RetroSfxEffectType type,
            int parameterIndex)
        {
            switch (type)
            {
                case RetroSfxEffectType.Filter:
                    return Pick(parameterIndex,
                        new RetroSfxEffectParameter("Cutoff", 30f, 18000f, "Hz", "Filter cutoff"),
                        new RetroSfxEffectParameter("Resonance", 0f, 1f, "%", "Filter resonance"),
                        new RetroSfxEffectParameter("Mix", 0f, 1f, "%", "Wet/dry balance"));
                case RetroSfxEffectType.Equalizer:
                    return Pick(parameterIndex,
                        new RetroSfxEffectParameter("Low", -18f, 18f, "dB", "Low-band gain"),
                        new RetroSfxEffectParameter("Mid", -18f, 18f, "dB", "Mid-band gain"),
                        new RetroSfxEffectParameter("High", -18f, 18f, "dB", "High-band gain"),
                        new RetroSfxEffectParameter("Mix", 0f, 1f, "%", "Wet/dry balance"));
                case RetroSfxEffectType.Compressor:
                    return Pick(parameterIndex,
                        new RetroSfxEffectParameter("Threshold", -48f, 0f, "dB", "Compression threshold"),
                        new RetroSfxEffectParameter("Ratio", 1f, 20f, "ratio", "Compression ratio"),
                        new RetroSfxEffectParameter("Attack", 0.001f, 0.1f, "s", "Gain reduction attack"),
                        new RetroSfxEffectParameter("Release", 0.01f, 0.5f, "s", "Gain reduction release"),
                        new RetroSfxEffectParameter("Makeup", 0f, 18f, "dB", "Output makeup gain"));
                case RetroSfxEffectType.Distortion:
                    return Pick(parameterIndex,
                        new RetroSfxEffectParameter("Drive", 1f, 20f, "x", "Saturation drive"),
                        new RetroSfxEffectParameter("Tone", 0f, 1f, "%", "Post-drive brightness"),
                        new RetroSfxEffectParameter("Mix", 0f, 1f, "%", "Wet/dry balance"));
                case RetroSfxEffectType.Chorus:
                    return Pick(parameterIndex,
                        new RetroSfxEffectParameter("Rate", 0.05f, 8f, "Hz1", "Modulation rate"),
                        new RetroSfxEffectParameter("Depth", 0f, 0.012f, "s", "Delay modulation depth"),
                        new RetroSfxEffectParameter("Delay", 0.004f, 0.03f, "s", "Base chorus delay"),
                        new RetroSfxEffectParameter("Mix", 0f, 1f, "%", "Wet/dry balance"));
                case RetroSfxEffectType.Delay:
                    return Pick(parameterIndex,
                        new RetroSfxEffectParameter("Time", 0.03f, 1f, "s", "Echo time"),
                        new RetroSfxEffectParameter("Feedback", 0f, 0.9f, "%", "Echo feedback"),
                        new RetroSfxEffectParameter("Mix", 0f, 1f, "%", "Wet/dry balance"));
                case RetroSfxEffectType.Reverb:
                    return Pick(parameterIndex,
                        new RetroSfxEffectParameter("Room", 0f, 1f, "%", "Virtual room size"),
                        new RetroSfxEffectParameter("Decay", 0.2f, 4f, "s", "Reverb decay time"),
                        new RetroSfxEffectParameter("Damping", 0f, 1f, "%", "High-frequency damping"),
                        new RetroSfxEffectParameter("Mix", 0f, 1f, "%", "Wet/dry balance"));
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        internal static float GetValue(RetroSfxEffectSettings effect, int parameterIndex)
        {
            switch (parameterIndex)
            {
                case 0: return effect.ParameterA;
                case 1: return effect.ParameterB;
                case 2: return effect.ParameterC;
                case 3: return effect.ParameterD;
                case 4: return effect.ParameterE;
                default: return 0f;
            }
        }

        internal static void SetValue(
            RetroSfxEffectSettings effect,
            int parameterIndex,
            float value)
        {
            RetroSfxEffectParameter parameter = GetParameter(effect.Type, parameterIndex);
            value = Mathf.Clamp(value, parameter.Minimum, parameter.Maximum);
            switch (parameterIndex)
            {
                case 0:
                    effect.ParameterA = value;
                    break;
                case 1:
                    effect.ParameterB = value;
                    break;
                case 2:
                    effect.ParameterC = value;
                    break;
                case 3:
                    effect.ParameterD = value;
                    break;
                case 4:
                    effect.ParameterE = value;
                    break;
            }
        }

        internal static void Normalize(RetroSfxEffectSettings effect)
        {
            if (effect == null)
            {
                return;
            }
            effect.EnsureId();
            int count = GetParameterCount(effect.Type);
            for (int index = 0; index < count; index++)
            {
                SetValue(effect, index, GetValue(effect, index));
            }
        }

        private static RetroSfxEffectParameter Pick(
            int index,
            params RetroSfxEffectParameter[] parameters)
        {
            if (index < 0 || index >= parameters.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, null);
            }
            return parameters[index];
        }
    }
}
