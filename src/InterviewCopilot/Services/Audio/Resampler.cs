namespace InterviewCopilot.Services.Audio;

public static class Resampler
{
    // Simple linear resampler to target sample rate.
    public static float[] ToSampleRate(float[] samples, int srcRate, int dstRate)
    {
        if (srcRate == dstRate) return samples;
        double ratio = (double)dstRate / srcRate;
        int dstLen = (int)Math.Round(samples.Length * ratio);
        var dst = new float[dstLen];
        for (int i = 0; i < dstLen; i++)
        {
            double srcPos = i / ratio;
            int s0 = (int)Math.Floor(srcPos);
            int s1 = Math.Min(s0 + 1, samples.Length - 1);
            double frac = srcPos - s0;
            dst[i] = (float)(samples[s0] * (1 - frac) + samples[s1] * frac);
        }
        return dst;
    }
}

