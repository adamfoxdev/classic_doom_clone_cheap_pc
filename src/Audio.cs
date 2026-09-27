using Raylib_cs;

namespace HexenSharp;

/// <summary>Sound effects synthesized at startup (no audio files), played through Raylib.</summary>
public sealed unsafe class Audio : IDisposable
{
    const int Rate = 22050;
    const int Voices = 4;
    readonly Sound[][] _sounds = new Sound[(int)Sfx.Count][];
    readonly int[] _next = new int[(int)Sfx.Count];
    readonly Random _rng = new(7);

    public Audio()
    {
        for (int i = 0; i < (int)Sfx.Count; i++)
        {
            var samples = Synth((Sfx)i);
            Sound baseSound;
            fixed (short* p = samples)
            {
                var wave = new Wave { SampleCount = (uint)samples.Length, SampleRate = Rate, SampleSize = 16, Channels = 1, Data = p };
                baseSound = Raylib.LoadSoundFromWave(wave);
            }
            _sounds[i] = new Sound[Voices];
            _sounds[i][0] = baseSound;
            for (int v = 1; v < Voices; v++) _sounds[i][v] = Raylib.LoadSoundAlias(baseSound);
        }
    }

    public void Play(Sfx s, float volume)
    {
        int i = (int)s;
        var snd = _sounds[i][_next[i]];
        _next[i] = (_next[i] + 1) % Voices;
        Raylib.SetSoundVolume(snd, Math.Clamp(volume, 0f, 1f) * 0.7f);
        Raylib.SetSoundPitch(snd, 0.92f + (float)_rng.NextDouble() * 0.16f);
        Raylib.PlaySound(snd);
    }

    public void Dispose()
    {
        foreach (var set in _sounds)
        {
            for (int v = 1; v < Voices; v++) Raylib.UnloadSoundAlias(set[v]);
            Raylib.UnloadSound(set[0]);
        }
    }

    // ------------------------------------------------------------ synthesis

    internal static short[] Synth(Sfx s)
    {
        var r = new Rng((uint)s + 17);
        return s switch
        {
            Sfx.Swing => Gen(0.18f, (t, n) => r.Range(-1f, 1f) * Env(t, 0.3f, 0.18f) * 0.5f, lowpass: 0.25f),
            Sfx.Hit => Gen(0.14f, (t, n) => (r.Range(-1f, 1f) * 0.6f + MathF.Sin(t * 120 * MathF.Tau) * 0.6f) * Decay(t, 25)),
            Sfx.Shoot => Gen(0.25f, (t, n) => Square(t * (500 - 1400 * t)) * 0.3f * Decay(t, 10) + r.Range(-1f, 1f) * 0.2f * Decay(t, 20)),
            Sfx.Magic => Gen(0.28f, (t, n) => MathF.Sin(MathF.Tau * (400 * t + 900 * t * t) + MathF.Sin(t * 60) * 2) * 0.45f * Decay(t, 7)),
            Sfx.Explode => Gen(0.6f, (t, n) => r.Range(-1f, 1f) * Decay(t, 5) * 0.9f, lowpass: 0.12f),
            Sfx.Sight => Gen(0.45f, (t, n) => (Saw(t * (90 + 30 * MathF.Sin(t * 20))) * 0.5f + r.Range(-1f, 1f) * 0.25f) * Env(t, 0.05f, 0.45f), lowpass: 0.3f),
            Sfx.Death => Gen(0.6f, (t, n) => (Saw(t * (160 - 180 * t)) * 0.6f + r.Range(-1f, 1f) * 0.2f) * Decay(t, 4), lowpass: 0.35f),
            Sfx.Pickup => Gen(0.14f, (t, n) => Square(t * (t < 0.06f ? 880 : 1320)) * 0.25f * Env(t, 0.005f, 0.14f)),
            Sfx.Item => Gen(0.42f, (t, n) => MathF.Sin(MathF.Tau * t * (t < 0.12f ? 523 : t < 0.24f ? 659 : 784)) * 0.45f * Env(t, 0.01f, 0.42f)),
            Sfx.Door => Gen(0.5f, (t, n) => (r.Range(-1f, 1f) * 0.6f + Saw(t * 55) * 0.4f) * Env(t, 0.05f, 0.5f), lowpass: 0.08f),
            Sfx.Lever => Gen(0.3f, (t, n) => (r.Range(-1f, 1f) * Decay(t, 30) + MathF.Sin(t * 180 * MathF.Tau) * Decay(t, 12)) * 0.7f, lowpass: 0.3f),
            Sfx.Pain => Gen(0.18f, (t, n) => Saw(t * (220 - 200 * t)) * 0.45f * Env(t, 0.01f, 0.18f), lowpass: 0.4f),
            Sfx.PlayerPain => Gen(0.22f, (t, n) => Saw(t * (170 - 150 * t)) * 0.5f * Env(t, 0.01f, 0.22f), lowpass: 0.3f),
            Sfx.PlayerDeath => Gen(0.9f, (t, n) => Saw(t * (200 - 190 * t)) * 0.55f * Env(t, 0.02f, 0.9f), lowpass: 0.3f),
            Sfx.Teleport => Gen(0.7f, (t, n) => MathF.Sin(MathF.Tau * (300 * t + 1200 * t * t)) * MathF.Sin(t * 90) * 0.5f * Env(t, 0.05f, 0.7f)),
            Sfx.Locked => Gen(0.3f, (t, n) => Square(t * 110) * 0.3f * ((int)(t * 13) % 2 == 0 ? 1 : 0) * Env(t, 0.01f, 0.3f), lowpass: 0.3f),
            Sfx.BossSight => Gen(1.2f, (t, n) => (Saw(t * (60 + 20 * MathF.Sin(t * 9))) * 0.6f + r.Range(-1f, 1f) * 0.3f) * Env(t, 0.1f, 1.2f), lowpass: 0.2f),
            Sfx.Heal => Gen(0.5f, (t, n) => MathF.Sin(MathF.Tau * t * (600 + 600 * t)) * 0.4f * Env(t, 0.02f, 0.5f)),
            Sfx.Jump => Gen(0.16f, (t, n) => MathF.Sin(MathF.Tau * (180 * t + 900 * t * t)) * 0.35f * Env(t, 0.01f, 0.16f)),
            Sfx.Land => Gen(0.12f, (t, n) => (r.Range(-1f, 1f) * 0.5f + MathF.Sin(t * 70 * MathF.Tau) * 0.6f) * Decay(t, 30), lowpass: 0.2f),
            Sfx.Slide => Gen(0.45f, (t, n) => r.Range(-1f, 1f) * Env(t, 0.03f, 0.45f) * 0.45f, lowpass: 0.1f),
            Sfx.Chest => Gen(0.6f, (t, n) => t < 0.25f
                ? Saw(t * (90 + 60 * t)) * 0.35f * Env(t, 0.02f, 0.25f)                                // creak
                : MathF.Sin(MathF.Tau * t * (t < 0.37f ? 784 : t < 0.49f ? 988 : 1319)) * 0.35f * Env(t - 0.25f, 0.01f, 0.35f)),
            _ => new short[1],
        };
    }

    static float Decay(float t, float k) => MathF.Exp(-t * k);

    static float Env(float t, float attack, float len)
    {
        if (t < attack) return t / attack;
        return MathF.Max(0, 1 - (t - attack) / (len - attack));
    }

    static float Square(float phase) => (phase - MathF.Floor(phase)) < 0.5f ? 1f : -1f;
    static float Saw(float phase) => 2f * (phase - MathF.Floor(phase)) - 1f;

    static short[] Gen(float seconds, Func<float, int, float> f, float lowpass = 1f)
    {
        int n = (int)(seconds * Rate);
        var buf = new short[n];
        float y = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float x = f(t, i);
            y += (x - y) * lowpass; // one-pole low-pass filter
            buf[i] = (short)(Math.Clamp(y, -1f, 1f) * 30000);
        }
        return buf;
    }
}
