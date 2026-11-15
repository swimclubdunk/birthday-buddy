namespace BirthdayBuddy.Core;

/// <summary>
/// Synthesizes the bundled fanfare: a short brass-like "ta-ta-ta-taaa, ta-ta-ta-TAAA" in C major.
/// Deterministic, so the committed fanfare.wav can be checked against it.
/// </summary>
public static class FanfareSynth
{
    public const int SampleRate = 44_100;

    /// <summary>−1 dBFS: 32767 × 10^(−1/20), rounded.</summary>
    public const short Peak = 29_204;

    // (frequency in Hz, length in seconds, loudness 0–1). Frequency 0 is a rest.
    static readonly (double Hz, double Seconds, double Level)[] Melody =
    [
        (392.00, 0.11, 0.75), (392.00, 0.11, 0.75), (392.00, 0.11, 0.80), (523.25, 0.38, 0.90),
        (0, 0.06, 0),
        (392.00, 0.11, 0.80), (523.25, 0.11, 0.85), (659.25, 0.11, 0.90), (783.99, 1.45, 1.00),
    ];

    public static byte[] CreateWav()
    {
        double total = Melody.Sum(n => n.Seconds) + 0.25; // room for the last note's release
        var buffer = new double[(int)(total * SampleRate)];

        double start = 0;
        foreach (var (hz, seconds, level) in Melody)
        {
            if (hz > 0)
            {
                bool last = seconds > 1;
                AddNote(buffer, start, seconds, hz, level, vibrato: last);
                if (last) AddNote(buffer, start, seconds, hz * 2 / 3, level * 0.45, vibrato: true); // a fifth below, for body
            }
            start += seconds;
        }

        double max = buffer.Max(Math.Abs);
        double scale = Peak / max;
        var samples = buffer.Select(s => (short)Math.Round(s * scale)).ToArray();
        return Wav(samples);
    }

    static void AddNote(double[] buffer, double startSeconds, double seconds, double hz, double level, bool vibrato)
    {
        const double attack = 0.025, release = 0.04;
        int first = (int)(startSeconds * SampleRate);
        int count = (int)((seconds + (vibrato ? 0.2 : 0)) * SampleRate);
        double phase = 0;

        for (int i = 0; i < count && first + i < buffer.Length; i++)
        {
            double t = (double)i / SampleRate;
            double held = vibrato ? seconds : seconds - release;

            // Brass-like envelope: quick attack, slight settle, then release (longer on the final note).
            double env = t < attack ? t / attack
                : t < held ? 0.88 + 0.12 * Math.Exp(-(t - attack) * 12)
                : 0.88 * Math.Max(0, 1 - (t - held) / (vibrato ? 0.2 : release));
            if (vibrato && t < held) env *= 1 + 0.1 * Math.Min(1, t / held); // a small swell into the last note

            double f = hz * (vibrato ? 1 + 0.004 * Math.Sin(2 * Math.PI * 5.5 * t) * Math.Min(1, t / 0.3) : 1);
            phase += 2 * Math.PI * f / SampleRate;

            // Harmonics get brighter as the note gets louder, like a trumpet.
            double sample = 0;
            for (int n = 1; n <= 8; n++)
                sample += Math.Sin(n * phase) * Math.Pow(env, 0.6 + 0.25 * n) / Math.Pow(n, 0.9);

            buffer[first + i] += sample * env * level;
        }
    }

    static byte[] Wav(short[] samples)
    {
        int dataBytes = samples.Length * 2;
        using var stream = new MemoryStream(44 + dataBytes);
        using var w = new BinaryWriter(stream);
        w.Write("RIFF"u8);
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1);             // PCM
        w.Write((short)1);             // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);       // bytes per second
        w.Write((short)2);             // block align
        w.Write((short)16);            // bits per sample
        w.Write("data"u8);
        w.Write(dataBytes);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return stream.ToArray();
    }
}
