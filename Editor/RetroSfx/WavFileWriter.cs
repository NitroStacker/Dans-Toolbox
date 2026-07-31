using System;
using System.IO;

namespace DansToolbox.EditorTools.Audio
{
    internal static class WavFileWriter
    {
        private const short ChannelCount = 1;
        private const short BitsPerSample = 16;
        private const int HeaderSize = 44;

        /// <summary>Writes mono floating-point PCM samples to a 16-bit WAV file.</summary>
        public static void WriteMono16(string filePath, float[] samples, int sampleRate)
        {
            WritePcm16(filePath, samples, sampleRate, ChannelCount);
        }

        /// <summary>Writes interleaved stereo floating-point PCM samples to a 16-bit WAV file.</summary>
        public static void WriteStereo16(string filePath, float[] interleavedSamples, int sampleRate)
        {
            if (interleavedSamples.Length % 2 != 0)
            {
                throw new ArgumentException(
                    "Stereo sample data must contain complete left/right frame pairs.",
                    nameof(interleavedSamples));
            }

            WritePcm16(filePath, interleavedSamples, sampleRate, 2);
        }

        private static void WritePcm16(
            string filePath,
            float[] samples,
            int sampleRate,
            short channelCount)
        {
            int dataSize = samples.Length * sizeof(short);
            using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create, FileAccess.Write)))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(HeaderSize - 8 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channelCount);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channelCount * BitsPerSample / 8);
                writer.Write((short)(channelCount * BitsPerSample / 8));
                writer.Write(BitsPerSample);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);

                foreach (float sample in samples)
                {
                    int pcmValue = (int)Math.Round(sample * short.MaxValue);
                    pcmValue = Math.Max(short.MinValue, Math.Min(short.MaxValue, pcmValue));
                    writer.Write((short)pcmValue);
                }
            }
        }
    }
}
