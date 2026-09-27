namespace HexenSharp;

/// <summary>
/// Sound effects synthesized in code, one bank per visual style: the fantasy bank of grunts, whooshes and
/// chimes, and a sci-fi bank of lasers, servos, pneumatic doors and computer bleeps. Pure C# (no audio
/// device), so the self-test can check every sound and <c>--sounds</c> can export them as WAV files.
/// </summary>
public static class Sounds
{
    public const int Rate = 22050;
    const float Tau = MathF.Tau;

    public static short[] Make(Sfx s, ArtStyle style) => style == ArtStyle.SciFi ? SciFi(s) : Fantasy(s);

    // ------------------------------------------------------------ fantasy (the original bank)

    static short[] Fantasy(Sfx s)
    {
        var r = new Rng((uint)s + 17);
        return s switch
        {
            Sfx.Swing => Gen(0.18f, t => r.Range(-1f, 1f) * Env(t, 0.3f, 0.18f) * 0.5f, lowpass: 0.25f),
            Sfx.Hit => Gen(0.14f, t => (r.Range(-1f, 1f) * 0.6f + MathF.Sin(t * 120 * Tau) * 0.6f) * Decay(t, 25)),
            Sfx.Shoot => Gen(0.25f, t => Square(t * (500 - 1400 * t)) * 0.3f * Decay(t, 10) + r.Range(-1f, 1f) * 0.2f * Decay(t, 20)),
            Sfx.Magic => Gen(0.28f, t => MathF.Sin(Tau * (400 * t + 900 * t * t) + MathF.Sin(t * 60) * 2) * 0.45f * Decay(t, 7)),
            Sfx.Explode => Gen(0.6f, t => r.Range(-1f, 1f) * Decay(t, 5) * 0.9f, lowpass: 0.12f),
            Sfx.Sight => Gen(0.45f, t => (Saw(t * (90 + 30 * MathF.Sin(t * 20))) * 0.5f + r.Range(-1f, 1f) * 0.25f) * Env(t, 0.05f, 0.45f), lowpass: 0.3f),
            Sfx.Death => Gen(0.6f, t => (Saw(t * (160 - 180 * t)) * 0.6f + r.Range(-1f, 1f) * 0.2f) * Decay(t, 4), lowpass: 0.35f),
            Sfx.Pickup => Gen(0.14f, t => Square(t * (t < 0.06f ? 880 : 1320)) * 0.25f * Env(t, 0.005f, 0.14f)),
            Sfx.Item => Gen(0.42f, t => MathF.Sin(Tau * t * (t < 0.12f ? 523 : t < 0.24f ? 659 : 784)) * 0.45f * Env(t, 0.01f, 0.42f)),
            Sfx.Door => Gen(0.5f, t => (r.Range(-1f, 1f) * 0.6f + Saw(t * 55) * 0.4f) * Env(t, 0.05f, 0.5f), lowpass: 0.08f),
            Sfx.Lever => Gen(0.3f, t => (r.Range(-1f, 1f) * Decay(t, 30) + MathF.Sin(t * 180 * Tau) * Decay(t, 12)) * 0.7f, lowpass: 0.3f),
            Sfx.Pain => Gen(0.18f, t => Saw(t * (220 - 200 * t)) * 0.45f * Env(t, 0.01f, 0.18f), lowpass: 0.4f),
            Sfx.PlayerPain => Gen(0.22f, t => Saw(t * (170 - 150 * t)) * 0.5f * Env(t, 0.01f, 0.22f), lowpass: 0.3f),
            Sfx.PlayerDeath => Gen(0.9f, t => Saw(t * (200 - 190 * t)) * 0.55f * Env(t, 0.02f, 0.9f), lowpass: 0.3f),
            Sfx.Teleport => Gen(0.7f, t => MathF.Sin(Tau * (300 * t + 1200 * t * t)) * MathF.Sin(t * 90) * 0.5f * Env(t, 0.05f, 0.7f)),
            Sfx.Locked => Gen(0.3f, t => Square(t * 110) * 0.3f * ((int)(t * 13) % 2 == 0 ? 1 : 0) * Env(t, 0.01f, 0.3f), lowpass: 0.3f),
            Sfx.BossSight => Gen(1.2f, t => (Saw(t * (60 + 20 * MathF.Sin(t * 9))) * 0.6f + r.Range(-1f, 1f) * 0.3f) * Env(t, 0.1f, 1.2f), lowpass: 0.2f),
            Sfx.Heal => Gen(0.5f, t => MathF.Sin(Tau * t * (600 + 600 * t)) * 0.4f * Env(t, 0.02f, 0.5f)),
            Sfx.Jump => Gen(0.16f, t => MathF.Sin(Tau * (180 * t + 900 * t * t)) * 0.35f * Env(t, 0.01f, 0.16f)),
            Sfx.Land => Gen(0.12f, t => (r.Range(-1f, 1f) * 0.5f + MathF.Sin(t * 70 * Tau) * 0.6f) * Decay(t, 30), lowpass: 0.2f),
            Sfx.Slide => Gen(0.45f, t => r.Range(-1f, 1f) * Env(t, 0.03f, 0.45f) * 0.45f, lowpass: 0.1f),
            Sfx.Chest => Gen(0.6f, t => t < 0.25f
                ? Saw(t * (90 + 60 * t)) * 0.35f * Env(t, 0.02f, 0.25f)                                // creak
                : MathF.Sin(Tau * t * (t < 0.37f ? 784 : t < 0.49f ? 988 : 1319)) * 0.35f * Env(t - 0.25f, 0.01f, 0.35f)),
            Sfx.Push => Gen(0.45f, t => (r.Range(-1f, 1f) * 0.7f + Saw(t * 40) * 0.3f) * Env(t, 0.03f, 0.45f), lowpass: 0.06f),
            Sfx.Blur => Gen(0.35f, t => MathF.Sin(Tau * (900 * t - 1400 * t * t)) * MathF.Sin(t * 140) * 0.4f * Env(t, 0.02f, 0.35f)),
            Sfx.Secret => Gen(0.9f, t => MathF.Sin(Tau * t * (t < 0.15f ? 523 : t < 0.3f ? 659 : t < 0.45f ? 784 : 1047)) * 0.4f * Env(t, 0.01f, 0.9f)),
            Sfx.Lore => Gen(0.8f, t => (MathF.Sin(Tau * 220 * t) + MathF.Sin(Tau * 330 * t) * 0.6f) * 0.3f * Env(t, 0.15f, 0.8f)),
            Sfx.Relic => Gen(0.8f, t => MathF.Sin(Tau * t * (880 + 440 * MathF.Floor(t * 8) / 4)) * MathF.Exp(-(t % 0.125f) * 20) * 0.4f * Env(t, 0.01f, 0.8f)),
            // Wings of Wrath: a great wing beat, a soft flutter while aloft, and a fading chime when they give out
            Sfx.JetStart => Gen(0.32f, t => r.Range(-1f, 1f) * Env(t, 0.06f, 0.32f) * 0.9f, lowpass: 0.09f),
            Sfx.Jet => Gen(0.16f, t => r.Range(-1f, 1f) * Window(t, 0.16f) * (0.6f + 0.4f * MathF.Sin(Tau * 12 * t)) * 0.55f, lowpass: 0.08f),
            Sfx.JetOut => Gen(0.6f, t => MathF.Sin(Tau * (700 * t - 500 * t * t)) * 0.35f * Env(t, 0.01f, 0.6f)),
            _ => new short[1],
        };
    }

    // ------------------------------------------------------------ sci-fi

    static short[] SciFi(Sfx s)
    {
        var r = new Rng((uint)s * 7 + 101);
        float Noise() => r.Range(-1f, 1f);
        return s switch
        {
            // servo whoosh (melee swings, menu moves)
            Sfx.Swing => Gen(0.16f, t => Noise() * 0.35f * Env(t, 0.04f, 0.16f) + MathF.Sin(Tau * (1400 * t - 2500 * t * t)) * 0.2f * Decay(t, 18), lowpass: 0.5f),
            // metallic clang: inharmonic partials
            Sfx.Hit => Gen(0.22f, t => (MathF.Sin(Tau * 523 * t) + MathF.Sin(Tau * 1370 * t) * 0.7f + MathF.Sin(Tau * 2130 * t) * 0.5f) * 0.3f * Decay(t, 22)
                                        + Noise() * 0.4f * Decay(t, 60)),
            // heavy laser: a square wave diving from 1.8 kHz
            Sfx.Shoot => Gen(0.3f, t => Square(Dive(t, 1800, 120, 14)) * 0.3f * Decay(t, 8) + Noise() * 0.25f * Decay(t, 30), lowpass: 0.6f),
            // blaster "pew"
            Sfx.Magic => Gen(0.22f, t => (MathF.Sin(Tau * Dive(t, 2400, 300, 18)) * 0.45f + Square(Dive(t, 1200, 150, 18)) * 0.12f) * Decay(t, 9)),
            // explosion: low boom, rumble and crackle
            Sfx.Explode => Gen(0.9f, t => Noise() * Decay(t, 4) * 0.7f + MathF.Sin(Tau * (55 * t - 20 * t * t)) * 0.8f * Decay(t, 5)
                                          + (r.Float() > 0.96f ? 0.8f : 0f) * Decay(t, 3), lowpass: 0.2f),
            // robot alert chirp, bit-crushed
            Sfx.Sight => Gen(0.26f, t => Crush((t < 0.08f ? Square(t * 1200) : t > 0.11f && t < 0.2f ? Square(t * 1600) : 0f) * 0.3f, 6), lowpass: 0.5f),
            // electrical short-out: a sagging buzz broken up by sparks
            Sfx.Death => Gen(0.7f, t => (Saw(Dive(t, 400, 40, 4)) * 0.4f + Noise() * 0.4f * ((int)(t * 40) % 3 == 0 ? 1f : 0.2f)) * Env(t, 0.01f, 0.7f), lowpass: 0.4f),
            // data blip
            Sfx.Pickup => Gen(0.1f, t => Square(t * (t < 0.04f ? 1500 : 2200)) * 0.22f * Env(t, 0.003f, 0.1f), lowpass: 0.7f),
            // rising square arpeggio
            Sfx.Item => Gen(0.36f, t => Square(t * Notes(t, 0.07f, 660, 880, 1100, 1320)) * 0.22f * Env(t, 0.005f, 0.36f), lowpass: 0.6f),
            // pneumatic door: a hiss over a servo whine
            Sfx.Door => Gen(0.6f, Highpass(Noise, 0.3f, (t, hiss) => hiss * 0.55f * Env(t, 0.02f, 0.6f) + Saw(t * (90 + 60 * t)) * 0.15f * Env(t, 0.1f, 0.6f))),
            // switch click, then a confirming beep
            Sfx.Lever => Gen(0.2f, t => Noise() * Decay(t, 200) * 0.8f + (t > 0.06f ? MathF.Sin(Tau * 1000 * t) * 0.3f * Env(t - 0.06f, 0.005f, 0.12f) : 0f)),
            // robot glitch: a square hopping between random pitches
            Sfx.Pain => Gen(0.2f, t => Square(t * (200 + Hash((int)(t * 50)) % 13 * 60)) * 0.3f * Env(t, 0.005f, 0.2f), lowpass: 0.5f),
            // your grunt, over radio static
            Sfx.PlayerPain => Gen(0.22f, t => (Saw(t * (170 - 150 * t)) * 0.45f + Noise() * 0.12f) * Env(t, 0.01f, 0.22f), lowpass: 0.35f),
            // a groan, then the heart monitor flatlines
            Sfx.PlayerDeath => Gen(1.4f, t => t < 0.4f
                ? Saw(t * (200 - 190 * t)) * 0.5f * Env(t, 0.02f, 0.4f)
                : MathF.Sin(Tau * 1000 * t) * 0.25f * Env(t - 0.4f, 0.01f, 1.0f), lowpass: 0.4f),
            // teleporter: a rising sweep with tremolo
            Sfx.Teleport => Gen(0.8f, t => (MathF.Sin(Tau * (200 * t + 900 * t * t)) * (0.5f + 0.5f * MathF.Sin(Tau * 30 * t)) * 0.4f
                                          + MathF.Sin(Tau * (400 * t + 1800 * t * t)) * 0.15f) * Env(t, 0.05f, 0.8f)),
            // access denied: two low buzzes
            Sfx.Locked => Gen(0.35f, t => (t < 0.12f || (t > 0.18f && t < 0.3f) ? (Square(t * 160) + Square(t * 163)) * 0.18f : 0f), lowpass: 0.4f),
            // boss alarm: a wailing siren over a drone
            Sfx.BossSight => Gen(1.4f, t => (Saw(300 * t - 150 / (Tau * 2) * MathF.Cos(Tau * 2 * t)) * 0.35f + Saw(t * 55) * 0.3f) * Env(t, 0.1f, 1.4f), lowpass: 0.3f),
            // medical beeps
            Sfx.Heal => Gen(0.36f, t => MathF.Sin(Tau * t * Notes(t, 0.12f, 880, 1175, 1568)) * 0.4f * Gate(t, 0.12f, 0.08f)),
            // thruster puff
            Sfx.Jump => Gen(0.14f, t => Noise() * 0.4f * Env(t, 0.01f, 0.14f) + MathF.Sin(Tau * (300 * t + 600 * t * t)) * 0.15f, lowpass: 0.3f),
            // boots on deck plating
            Sfx.Land => Gen(0.16f, t => MathF.Sin(Tau * 90 * t) * Decay(t, 25) * 0.7f + MathF.Sin(Tau * 740 * t) * 0.15f * Decay(t, 40) + Noise() * 0.3f * Decay(t, 50), lowpass: 0.4f),
            // metal scrape
            Sfx.Slide => Gen(0.45f, t => (Noise() * 0.45f + MathF.Sin(Tau * 1900 * t) * 0.08f) * Env(t, 0.03f, 0.45f), lowpass: 0.3f),
            // crate unlocks: a hiss of seals, then three blips
            Sfx.Chest => Gen(0.6f, Highpass(Noise, 0.3f, (t, hiss) => t < 0.22f
                ? hiss * 0.5f * Env(t, 0.01f, 0.22f)
                : Square(t * Notes(t - 0.25f, 0.1f, 784, 988, 1319)) * 0.2f * Gate(t - 0.25f, 0.1f, 0.07f))),
            // heavy machinery grinding
            Sfx.Push => Gen(0.5f, t => (Saw(t * 48) * 0.35f + Saw(t * 48.7f) * 0.3f + Noise() * 0.25f) * Env(t, 0.05f, 0.5f), lowpass: 0.08f),
            // psi wraith blink: phaser warble
            Sfx.Blur => Gen(0.35f, t => MathF.Sin(Tau * 700 * t + 6 * MathF.Sin(Tau * 23 * t)) * 0.35f * Env(t, 0.02f, 0.35f)),
            // access granted
            Sfx.Secret => Gen(0.8f, t => Square(t * Notes(t, 0.1f, 523, 784, 1047, 1568)) * 0.2f * Env(t, 0.005f, 0.8f), lowpass: 0.5f),
            // data terminal: chattering bleeps
            Sfx.Lore => Gen(0.7f, t => Square(t * (600 + Hash((int)(t * 33)) % 8 * 110)) * 0.18f * Gate(t, 0.03f, 0.021f) * Env(t, 0.01f, 0.7f), lowpass: 0.5f),
            // alien artifact: two shimmering FM bells
            Sfx.Relic => Gen(1.0f, t => MathF.Sin(Tau * 880 * t + 3 * Decay(t, 3) * MathF.Sin(Tau * 1230 * t)) * 0.3f * Decay(t, 3)
                                        + (t > 0.15f ? MathF.Sin(Tau * 1320 * t + 2 * MathF.Sin(Tau * 1850 * t)) * 0.25f * Decay(t - 0.15f, 3) : 0f)),
            // jetpack: ignition roar, a loopable thrust chunk, and a sputtering cut-out
            Sfx.JetStart => Gen(0.35f, Sweep(Noise, 0.05f, 0.35f, 0.35f, (t, roar) => roar * 0.8f * Env(t, 0.02f, 0.35f) + MathF.Sin(Tau * (80 * t + 300 * t * t)) * 0.2f)),
            Sfx.Jet => Gen(0.16f, t => (Noise() * 0.6f + Saw(t * 70) * 0.15f) * Window(t, 0.16f), lowpass: 0.18f),
            Sfx.JetOut => Gen(0.6f, t => Noise() * 0.5f * ((int)(t * 25) % 2 == 0 ? 1f : 0.1f) * Decay(t, 4)
                                         + MathF.Sin(Tau * Dive(t, 600, 150, 4)) * 0.2f * Env(t, 0.01f, 0.6f), lowpass: 0.25f),
            _ => new short[1],
        };
    }

    // ------------------------------------------------------------ building blocks

    static float Decay(float t, float k) => MathF.Exp(-t * k);

    static float Env(float t, float attack, float len)
    {
        if (t < 0) return 0;
        if (t < attack) return t / attack;
        return MathF.Max(0, 1 - (t - attack) / (len - attack));
    }

    /// <summary>A smooth hump over the whole sound, so overlapping copies blend into a steady loop.</summary>
    static float Window(float t, float len) => MathF.Sin(MathF.PI * Math.Clamp(t / len, 0f, 1f));

    /// <summary>On for the first <paramref name="on"/> seconds of every <paramref name="period"/>.</summary>
    static float Gate(float t, float period, float on) => t >= 0 && t % period < on ? 1f : 0f;

    static float Square(float phase) => (phase - MathF.Floor(phase)) < 0.5f ? 1f : -1f;
    static float Saw(float phase) => 2f * (phase - MathF.Floor(phase)) - 1f;
    static float Crush(float x, int levels) => MathF.Round(x * levels) / levels;
    static int Hash(int i) => (int)(((uint)i * 2654435761u) >> 20);

    /// <summary>Phase of a pitch that falls exponentially from <paramref name="from"/> Hz toward <paramref name="to"/> Hz.</summary>
    static float Dive(float t, float from, float to, float k) => (from - to) / k * (1 - MathF.Exp(-k * t)) + to * t;

    /// <summary>The note playing at time t in a run of equal-length notes (the last one holds).</summary>
    static float Notes(float t, float each, params float[] hz) => hz[Math.Clamp((int)(MathF.Max(0, t) / each), 0, hz.Length - 1)];

    /// <summary>Feeds a high-passed noise source to <paramref name="f"/>, for hisses.</summary>
    static Func<float, float> Highpass(Func<float> src, float k, Func<float, float, float> f)
    {
        float lp = 0;
        return t => { float x = src(); lp += (x - lp) * k; return f(t, x - lp); };
    }

    /// <summary>Feeds noise through a low-pass filter that opens from <paramref name="k0"/> to <paramref name="k1"/>, for a rising roar.</summary>
    static Func<float, float> Sweep(Func<float> src, float k0, float k1, float len, Func<float, float, float> f)
    {
        float lp = 0;
        return t => { lp += (src() - lp) * (k0 + (k1 - k0) * Math.Clamp(t / len, 0f, 1f)); return f(t, lp * 2f); };
    }

    static short[] Gen(float seconds, Func<float, float> f, float lowpass = 1f)
    {
        int n = (int)(seconds * Rate);
        var buf = new short[n];
        float y = 0;
        for (int i = 0; i < n; i++)
        {
            float x = f(i / (float)Rate);
            y += (x - y) * lowpass; // one-pole low-pass filter
            buf[i] = (short)(Math.Clamp(y, -1f, 1f) * 30000);
        }
        return buf;
    }

    /// <summary>A 16-bit mono WAV file.</summary>
    public static byte[] Wav(short[] samples)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + samples.Length * 2); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(samples.Length * 2);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }
}
