using System;
using System.IO;

namespace AudioMW.Editor
{
    public static class WavWriter
    {
        public static void Write(string path, float[] interleaved, int channels, int sampleRate)
        {
            if (interleaved == null || channels <= 0 || sampleRate <= 0)
            {
                return;
            }

            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int frameCount = interleaved.Length / channels;
                int dataBytes = frameCount * channels * 2;

                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });

                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);

                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);

                for (int i = 0; i < interleaved.Length; i++)
                {
                    float clamped = Math.Max(-1f, Math.Min(1f, interleaved[i]));
                    writer.Write((short)Math.Round(clamped * 32767f));
                }
            }
        }
    }
}
