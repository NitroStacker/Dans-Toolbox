using System;
using UnityEngine;

namespace DansToolbox.EditorTools.Audio
{
    [Serializable]
    internal sealed class RetroSfxImportedAudioSettings
    {
        public AudioClip SourceClip;
        public float TrimStart;
        public float TrimEnd = 1f;
        public float FadeIn;
        public float FadeOut;
        public float PitchSemitones;
        public float GainDecibels;
        public float EnvelopeAttack;
        public float EnvelopeDecay;
        public float EnvelopeSustain = 1f;
        public float EnvelopeRelease;

        internal void ResetEdits()
        {
            TrimStart = 0f;
            TrimEnd = 1f;
            FadeIn = 0f;
            FadeOut = 0f;
            PitchSemitones = 0f;
            GainDecibels = 0f;
            EnvelopeAttack = 0f;
            EnvelopeDecay = 0f;
            EnvelopeSustain = 1f;
            EnvelopeRelease = 0f;
        }
    }

    /// <summary>
    /// Reads a project AudioClip, creates a mono 44.1 kHz edit, and leaves the
    /// source asset untouched. The result feeds the same effects chain as synth audio.
    /// </summary>
    internal static class RetroSfxImportedAudioProcessor
    {
        internal const float MaximumOutputDuration = 12f;
        private const int OverviewPointCount = 512;
        private const int MaximumFullOverviewValues = 4_000_000;
        private const int LongClipProbeFrames = 256;

        internal static bool TryGenerate(
            RetroSfxImportedAudioSettings settings,
            out float[] output,
            out bool wasDurationLimited,
            out string error)
        {
            output = Array.Empty<float>();
            wasDurationLimited = false;
            error = string.Empty;

            AudioClip clip = settings?.SourceClip;
            if (clip == null)
            {
                return true;
            }

            if (!EnsureClipLoaded(clip, out error))
            {
                return false;
            }

            int channels = Mathf.Max(1, clip.channels);
            int sourceFrames = Mathf.Max(0, clip.samples);
            if (sourceFrames == 0 || clip.frequency <= 0)
            {
                error = "The selected clip contains no readable sample frames.";
                return false;
            }

            float trimStart = Mathf.Clamp01(settings.TrimStart);
            float trimEnd = Mathf.Clamp(settings.TrimEnd, trimStart + 1f / sourceFrames, 1f);
            int startFrame = Mathf.Clamp(
                Mathf.FloorToInt(trimStart * sourceFrames),
                0,
                sourceFrames - 1);
            int endFrame = Mathf.Clamp(
                Mathf.CeilToInt(trimEnd * sourceFrames),
                startFrame + 1,
                sourceFrames);
            int selectedFrames = endFrame - startFrame;

            float pitchRatio = Mathf.Pow(2f, Mathf.Clamp(settings.PitchSemitones, -24f, 24f) / 12f);
            float sourceStep = clip.frequency * pitchRatio / RetroSfxSettings.SampleRate;
            int naturalOutputFrames = Mathf.Max(1, Mathf.CeilToInt(selectedFrames / sourceStep));
            int maximumOutputFrames = Mathf.RoundToInt(MaximumOutputDuration * RetroSfxSettings.SampleRate);
            int outputFrames = Mathf.Min(naturalOutputFrames, maximumOutputFrames);
            wasDurationLimited = outputFrames < naturalOutputFrames;

            int requiredSourceFrames = Mathf.Min(
                selectedFrames,
                Mathf.CeilToInt(Mathf.Max(0, outputFrames - 1) * sourceStep) + 2);
            float[] interleaved = new float[requiredSourceFrames * channels];
            try
            {
                if (!clip.GetData(interleaved, startFrame))
                {
                    error = GetReadableClipError(clip);
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = $"{GetReadableClipError(clip)} ({exception.Message})";
                return false;
            }

            output = new float[outputFrames];
            float duration = outputFrames / (float)RetroSfxSettings.SampleRate;
            float gain = Mathf.Pow(10f, Mathf.Clamp(settings.GainDecibels, -60f, 12f) / 20f);
            float fadeIn = Mathf.Clamp(settings.FadeIn, 0f, duration);
            float fadeOut = Mathf.Clamp(settings.FadeOut, 0f, duration);
            float attack = Mathf.Clamp(settings.EnvelopeAttack, 0f, duration);
            float decay = Mathf.Clamp(settings.EnvelopeDecay, 0f, duration);
            float sustain = Mathf.Clamp01(settings.EnvelopeSustain);
            float release = Mathf.Clamp(settings.EnvelopeRelease, 0f, duration);

            for (int outputFrame = 0; outputFrame < outputFrames; outputFrame++)
            {
                float sourcePosition = outputFrame * sourceStep;
                int frameA = Mathf.Clamp(Mathf.FloorToInt(sourcePosition), 0, requiredSourceFrames - 1);
                int frameB = Mathf.Min(frameA + 1, requiredSourceFrames - 1);
                float fraction = sourcePosition - frameA;
                float sampleA = DownmixFrame(interleaved, frameA, channels);
                float sampleB = DownmixFrame(interleaved, frameB, channels);
                float sample = Mathf.Lerp(sampleA, sampleB, fraction);
                float time = outputFrame / (float)RetroSfxSettings.SampleRate;

                float fadeGain = 1f;
                if (fadeIn > 0f)
                {
                    fadeGain *= Mathf.Clamp01(time / fadeIn);
                }
                if (fadeOut > 0f)
                {
                    fadeGain *= Mathf.Clamp01((duration - time) / fadeOut);
                }

                float envelopeGain = CalculateEnvelope(
                    time,
                    duration,
                    attack,
                    decay,
                    sustain,
                    release);
                output[outputFrame] = Mathf.Clamp(sample * gain * fadeGain * envelopeGain, -1f, 1f);
            }

            return true;
        }

        internal static bool TryBuildOverview(
            AudioClip clip,
            out float[] minimums,
            out float[] maximums,
            out string error)
        {
            minimums = Array.Empty<float>();
            maximums = Array.Empty<float>();
            error = string.Empty;

            if (clip == null)
            {
                return true;
            }

            if (!EnsureClipLoaded(clip, out error))
            {
                return false;
            }

            int channels = Mathf.Max(1, clip.channels);
            int frames = Mathf.Max(0, clip.samples);
            if (frames == 0)
            {
                error = "The selected clip contains no readable sample frames.";
                return false;
            }

            minimums = new float[OverviewPointCount];
            maximums = new float[OverviewPointCount];
            long valueCount = (long)frames * channels;
            if (valueCount <= MaximumFullOverviewValues)
            {
                float[] samples = new float[(int)valueCount];
                if (!TryGetData(clip, samples, 0, out error))
                {
                    minimums = Array.Empty<float>();
                    maximums = Array.Empty<float>();
                    return false;
                }

                for (int point = 0; point < OverviewPointCount; point++)
                {
                    int start = (int)((long)point * frames / OverviewPointCount);
                    int end = Mathf.Min(
                        frames,
                        Mathf.Max(start + 1, (int)((long)(point + 1) * frames / OverviewPointCount)));
                    MeasureRange(samples, channels, start, end, out minimums[point], out maximums[point]);
                }
                return true;
            }

            int probeFrames = Mathf.Min(LongClipProbeFrames, frames);
            float[] probe = new float[probeFrames * channels];
            for (int point = 0; point < OverviewPointCount; point++)
            {
                int offset = Mathf.RoundToInt(
                    point / (OverviewPointCount - 1f) * Mathf.Max(0, frames - probeFrames));
                if (!TryGetData(clip, probe, offset, out error))
                {
                    minimums = Array.Empty<float>();
                    maximums = Array.Empty<float>();
                    return false;
                }
                MeasureRange(probe, channels, 0, probeFrames, out minimums[point], out maximums[point]);
            }
            return true;
        }

        private static bool EnsureClipLoaded(AudioClip clip, out string error)
        {
            error = string.Empty;
            if (clip.loadState == AudioDataLoadState.Loaded)
            {
                return true;
            }

            if (clip.loadState == AudioDataLoadState.Failed)
            {
                error = GetReadableClipError(clip);
                return false;
            }

            if (!clip.LoadAudioData())
            {
                error = GetReadableClipError(clip);
                return false;
            }

            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                error = "Unity is loading the clip sample data…";
                return false;
            }

            return true;
        }

        private static bool TryGetData(
            AudioClip clip,
            float[] samples,
            int offsetFrames,
            out string error)
        {
            error = string.Empty;
            try
            {
                if (clip.GetData(samples, offsetFrames))
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                error = $"{GetReadableClipError(clip)} ({exception.Message})";
                return false;
            }

            error = GetReadableClipError(clip);
            return false;
        }

        private static void MeasureRange(
            float[] samples,
            int channels,
            int startFrame,
            int endFrame,
            out float minimum,
            out float maximum)
        {
            minimum = 0f;
            maximum = 0f;
            int frameCount = Mathf.Max(1, endFrame - startFrame);
            int stride = Mathf.Max(1, frameCount / 96);
            for (int frame = startFrame; frame < endFrame; frame += stride)
            {
                float sample = DownmixFrame(samples, frame, channels);
                minimum = Mathf.Min(minimum, sample);
                maximum = Mathf.Max(maximum, sample);
            }
        }

        private static float DownmixFrame(float[] interleaved, int frame, int channels)
        {
            int baseIndex = frame * channels;
            float sum = 0f;
            for (int channel = 0; channel < channels; channel++)
            {
                sum += interleaved[baseIndex + channel];
            }
            return sum / channels;
        }

        private static float CalculateEnvelope(
            float time,
            float duration,
            float attack,
            float decay,
            float sustain,
            float release)
        {
            float gain;
            if (attack > 0f && time < attack)
            {
                gain = Mathf.Clamp01(time / attack);
            }
            else if (decay > 0f && time < attack + decay)
            {
                gain = Mathf.Lerp(1f, sustain, Mathf.Clamp01((time - attack) / decay));
            }
            else
            {
                gain = sustain;
            }

            if (release > 0f)
            {
                gain *= Mathf.Clamp01((duration - time) / release);
            }
            return gain;
        }

        private static string GetReadableClipError(AudioClip clip)
        {
            return $"Unity could not read PCM data from “{clip.name}”. Set its Load Type to Decompress On Load.";
        }
    }
}
