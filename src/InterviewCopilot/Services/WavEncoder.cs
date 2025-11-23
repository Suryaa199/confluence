using System.Buffers.Binary;

namespace InterviewCopilot.Services;

public static class WavEncoder
{
    // 16-bit PCM mono WAV @ 16kHz
    public static byte[] EncodePcm16kMono(float[] samples)
    {
        const int sampleRate = 16000;
        var clamped = new short[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            var v = Math.Clamp(samples[i], -1f, 1f);
            clamped[i] = (short)Math.Round(v * short.MaxValue);
        }
        int dataSize = clamped.Length * 2;
        int fmtSize = 16;
        int headerSize = 44;
        int totalSize = headerSize + dataSize;
        var buf = new byte[totalSize];

        // RIFF header
        WriteAscii(buf, 0, "RIFF");
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4), totalSize - 8);
        WriteAscii(buf, 8, "WAVE");

        // fmt chunk
        WriteAscii(buf, 12, "fmt ");
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(16), fmtSize);
        BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(20), 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(22), 1); // mono
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(24), sampleRate);
        int byteRate = sampleRate * 2;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(28), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(32), 2); // block align
        BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(34), 16); // bits per sample

        // data chunk
        WriteAscii(buf, 36, "data");
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(40), dataSize);
        Buffer.BlockCopy(clamped, 0, buf, 44, dataSize);
        return buf;
    }

    private static void WriteAscii(byte[] dest, int offset, string s)
    {
        for (int i = 0; i < s.Length; i++) dest[offset + i] = (byte)s[i];
    }
}

