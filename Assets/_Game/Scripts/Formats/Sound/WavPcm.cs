using System;
using System.IO;

namespace Arcanum.Formats.Sound
{
    /// <summary>A decoded WAV: interleaved float samples in −1..1 (Unity's AudioClip.SetData shape).</summary>
    public sealed class PcmData
    {
        public float[] Samples; // interleaved
        public int Channels;
        public int SampleRate;
        public int FrameCount => Channels > 0 ? Samples.Length / Channels : 0;
    }

    /// <summary>
    /// Minimal RIFF/WAVE decoder for the game's sfx (PCM 8/16-bit, mono/stereo) — walks the chunk list
    /// for <c>fmt </c> and <c>data</c>, tolerating extra chunks (LIST/cue). Unity-free: the runtime wraps
    /// the PCM in an AudioClip.
    /// </summary>
    public static class WavPcm
    {
        public static PcmData Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 44) return null;
            using var r = new BinaryReader(new MemoryStream(bytes, false));
            if (r.ReadUInt32() != 0x46464952) return null; // "RIFF"
            r.ReadUInt32(); // riff size
            if (r.ReadUInt32() != 0x45564157) return null; // "WAVE"

            int channels = 0, rate = 0, bits = 0, format = 0;
            byte[] data = null;
            while (r.BaseStream.Position + 8 <= r.BaseStream.Length)
            {
                uint id = r.ReadUInt32();
                int size = r.ReadInt32();
                if (size < 0) break; // corrupt chunk header — stop parsing, keep what we have
                long next = r.BaseStream.Position + size + (size & 1); // chunks are word-aligned
                if (id == 0x20746D66) // "fmt "
                {
                    format = r.ReadUInt16();
                    channels = r.ReadUInt16();
                    rate = r.ReadInt32();
                    r.ReadInt32(); // byte rate
                    r.ReadUInt16(); // block align
                    bits = r.ReadUInt16();
                }
                else if (id == 0x61746164) // "data"
                {
                    data = r.ReadBytes(Math.Min(size, (int)(r.BaseStream.Length - r.BaseStream.Position)));
                }

                if (next < 0 || next > r.BaseStream.Length) break;
                r.BaseStream.Position = next;
            }

            if (data == null || channels <= 0 || rate <= 0 || format != 1) return null; // PCM only

            float[] samples;
            if (bits == 16)
            {
                samples = new float[data.Length / 2];
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = BitConverter.ToInt16(data, i * 2) / 32768f;
            }
            else if (bits == 8)
            {
                samples = new float[data.Length];
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = (data[i] - 128) / 128f;
            }
            else return null;

            return new PcmData { Samples = samples, Channels = channels, SampleRate = rate };
        }
    }
}
