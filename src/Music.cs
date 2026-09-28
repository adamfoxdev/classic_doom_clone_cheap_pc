namespace HexenSharp;

/// <summary>
/// Procedural background music: a short seamless loop for each map theme (plus the title and the practice
/// courses), composed in code from a few numbers per track (tempo, key, mode, chord progression, drums) and a
/// seeded random melody. Fantasy tracks use organ pads, plucked strings and a soft flute; sci-fi tracks the same
/// tunes on detuned saw pads, square arpeggios and drum machines. Pure C# like <see cref="Sounds"/>, so the self-test
/// can check every track.
/// </summary>
public static class MusicGen
{
    public const int Rate = Sounds.Rate;
    const float Tau = MathF.Tau;

    /// <summary>How a track goes: beats a minute, root note (MIDI), the mode's steps, the chord for each of its 8 bars (scale degrees), and drums.</summary>
    sealed record Tune(float Bpm, int Root, int[] Mode, int[] Chords, int Drums, float Busy);

    static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 }, Major = { 0, 2, 4, 5, 7, 9, 11 }, Dorian = { 0, 2, 3, 5, 7, 9, 10 },
        Phrygian = { 0, 1, 3, 5, 7, 8, 10 }, Lydian = { 0, 2, 4, 6, 7, 9, 11 }, Mixolydian = { 0, 2, 4, 5, 7, 9, 10 };

    // drums: 0 none, 1 a soft pulse, 2 a steady beat, 3 driving
    static readonly Dictionary<string, Tune> Tunes = new()
    {
        ["title"] = new(84, 50, Minor, new[] { 0, 5, 2, 6, 0, 5, 3, 4 }, 1, 0.5f),
        ["hall"] = new(92, 45, Dorian, new[] { 0, 3, 0, 4, 0, 3, 5, 4 }, 1, 0.55f),
        ["ice"] = new(70, 52, Minor, new[] { 0, 5, 3, 4, 0, 5, 3, 6 }, 0, 0.35f),
        ["crypt"] = new(64, 48, Phrygian, new[] { 0, 1, 0, 6, 0, 1, 5, 6 }, 1, 0.3f),
        ["arena"] = new(132, 40, Minor, new[] { 0, 5, 6, 4, 0, 5, 6, 6 }, 3, 0.75f),
        ["spire"] = new(100, 43, Lydian, new[] { 0, 1, 4, 0, 0, 1, 5, 4 }, 2, 0.6f),
        ["barren"] = new(76, 41, Minor, new[] { 0, 6, 5, 6, 0, 6, 3, 4 }, 1, 0.4f),
        ["void"] = new(110, 47, Minor, new[] { 0, 5, 2, 6, 0, 5, 2, 4 }, 2, 0.65f),
        ["meadow"] = new(96, 55, Major, new[] { 0, 4, 5, 3, 0, 4, 3, 4 }, 1, 0.6f),
        ["practice"] = new(120, 48, Mixolydian, new[] { 0, 6, 3, 4, 0, 6, 3, 4 }, 3, 0.7f),
        ["town"] = new(80, 45, Dorian, new[] { 0, 3, 6, 4, 0, 3, 5, 4 }, 1, 0.45f), // a slow walk through the streets, for the story
    };

    public static IEnumerable<string> Tracks => Tunes.Keys;

    /// <summary>The track for a name: a map theme, "title" or "practice" (anything else plays the hall's).</summary>
    public static string Resolve(string track) => track != null && Tunes.ContainsKey(track) ? track : "hall";

    /// <summary>A track's loop length in samples: 8 bars of 4 beats.</summary>
    public static int Length(string track) => (int)(32 * 60f / Tunes[Resolve(track)].Bpm * Rate);

    static float Hz(int midi) => 440f * MathF.Pow(2, (midi - 69) / 12f);

    /// <summary>The note `degree` steps up the mode from the root (degrees past 7 go up an octave).</summary>
    static int Note(Tune t, int degree)
    {
        int oct = (int)MathF.Floor(degree / 7f), d = ((degree % 7) + 7) % 7;
        return t.Root + t.Mode[d] + 12 * oct;
    }

    public static short[] Make(string track, ArtStyle style)
    {
        track = Resolve(track);
        var t = Tunes[track];
        bool sci = style == ArtStyle.SciFi;
        int len = Length(track);
        var mix = new float[len];
        float beat = 60f / t.Bpm, bar = beat * 4;
        var rng = new Rng((uint)track.Sum(c => c * 31) + (sci ? 7u : 0u)); // not GetHashCode: that changes every run

        // adds a note, wrapping round the end of the loop so it joins up seamlessly
        void Add(float start, float dur, Func<float, float> voice)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            for (int i = 0; i < n; i++) mix[(s0 + i) % len] += voice(i / (float)Rate);
        }

        for (int b = 0; b < 8; b++)
        {
            int chord = t.Chords[b];
            float at = b * bar;
            // pad: the chord's root, third and fifth, held for the bar with a slow swell
            foreach (int step in new[] { 0, 2, 4 })
            {
                float f = Hz(Note(t, chord + step) + 12);
                Add(at, bar + 0.3f, x =>
                {
                    float env = MathF.Min(1, x / 0.4f) * MathF.Min(1, (bar + 0.3f - x) / 0.5f);
                    float v = sci
                        ? (Saw(x * f) + Saw(x * f * 1.006f)) * 0.5f * 0.35f
                        : (MathF.Sin(Tau * f * x) + 0.4f * MathF.Sin(Tau * 2 * f * x) + 0.2f * MathF.Sin(Tau * 3 * f * x)) * 0.35f;
                    return v * env * 0.22f;
                });
            }
            // bass: the chord root on the beats (on every beat when the drums drive)
            for (int k = 0; k < 4; k++)
            {
                if (t.Drums < 3 && k % 2 == 1) continue;
                float f = Hz(Note(t, chord) - 12), dur = beat * (t.Drums < 3 ? 1.8f : 0.9f);
                Add(at + k * beat, dur, x =>
                {
                    float env = MathF.Min(1, x / 0.01f) * MathF.Exp(-x * 2.5f);
                    float v = sci ? Square(x * f) * 0.6f : MathF.Sin(Tau * f * x) + 0.3f * MathF.Sin(Tau * 2 * f * x);
                    return v * env * 0.3f;
                });
            }
            // melody: a wander over the scale in eighth notes, resting now and then, landing on chord tones
            int deg = chord + 7;
            for (int e = 0; e < 8; e++)
            {
                if (rng.Next() % 100 >= (int)(t.Busy * 100)) continue;
                int move = (int)(rng.Next() % 5) - 2;
                deg = Math.Clamp(deg + move, chord + 4, chord + 11);
                if (e == 0) deg = chord + 7 + (int)(rng.Next() % 3) * 2;
                float f = Hz(Note(t, deg) + 12), dur = beat * (rng.Next() % 4 == 0 ? 1f : 0.5f);
                Add(at + e * beat / 2, dur + 0.2f, x =>
                {
                    float env = sci ? MathF.Exp(-x * 7f) : MathF.Min(1, x / 0.03f) * MathF.Exp(-x * (dur > beat * 0.6f ? 2.2f : 4f));
                    float v = sci ? Square(x * f) * 0.5f : MathF.Sin(Tau * f * x) + 0.25f * MathF.Sin(Tau * 2 * f * x + MathF.Sin(x * 30));
                    return v * env * 0.16f;
                });
            }
            // drums
            for (int k = 0; k < 8 && t.Drums > 0; k++)
            {
                float st = at + k * beat / 2;
                bool onBeat = k % 2 == 0, kick = t.Drums == 1 ? k == 0 : onBeat && (t.Drums == 3 || k % 4 == 0);
                if (kick) Add(st, 0.3f, x => MathF.Sin(Tau * (55 * x + 110 * (1 - MathF.Exp(-x * 30)) / 30)) * MathF.Exp(-x * 12) * (sci ? 0.55f : 0.4f));
                if (t.Drums >= 2 && k % 4 == 2)
                {
                    var nr = new Rng((uint)(b * 8 + k + 1));
                    Add(st, 0.2f, x => nr.Range(-1f, 1f) * MathF.Exp(-x * 18) * 0.22f);
                }
                if (t.Drums >= 2 && sci)
                {
                    var hr = new Rng((uint)(b * 16 + k + 5));
                    float prev = 0;
                    Add(st, 0.05f, x => { float n = hr.Range(-1f, 1f); float h = n - prev; prev = n; return h * MathF.Exp(-x * 80) * 0.07f; });
                }
            }
        }

        // a soft low-pass for the fantasy tracks, then scale to leave headroom for the sound effects
        if (!sci)
        {
            float y = mix[^1];
            for (int i = 0; i < len; i++) { y += (mix[i] - y) * 0.35f; mix[i] = y; }
        }
        float peak = mix.Max(MathF.Abs);
        float gain = peak > 0 ? 0.7f / peak : 0;
        var outp = new short[len];
        for (int i = 0; i < len; i++) outp[i] = (short)(Math.Clamp(mix[i] * gain, -1f, 1f) * 32767);
        return outp;
    }

    static float Saw(float phase) => 2 * (phase - MathF.Floor(phase + 0.5f));
    static float Square(float phase) => phase - MathF.Floor(phase) < 0.5f ? 1 : -1;

    /// <summary>A small xorshift generator, so a track is the same on every machine.</summary>
    sealed class Rng
    {
        uint _s;
        public Rng(uint seed) { _s = seed * 2654435761u + 1; if (_s == 0) _s = 1; }
        public uint Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
        public float Range(float lo, float hi) => lo + (hi - lo) * (Next() & 0xFFFFFF) / (float)0x1000000;
    }
}

/// <summary>
/// Plays the music: loops the current track and crossfades (over a second) when it changes, a block of samples at
/// a time for the audio stream. Tracks are made the first time they're needed and kept. Pure C#, so it's tested
/// without an audio device.
/// </summary>
public sealed class MusicMixer
{
    public const float Fade = 1f;
    readonly System.Collections.Concurrent.ConcurrentDictionary<(string, ArtStyle), Lazy<short[]>> _cache = new();
    short[] _cur, _old;
    int _curPos, _oldPos;
    float _fade = 1; // 0 -> 1 as the new track comes in
    public string Track { get; private set; }
    public ArtStyle Style { get; private set; }

    public short[] Get(string track, ArtStyle style)
    {
        track = MusicGen.Resolve(track);
        return _cache.GetOrAdd((track, style), k => new Lazy<short[]>(() => MusicGen.Make(k.Item1, k.Item2))).Value;
    }

    /// <summary>Makes every track for a style on a background thread, so changing maps never waits for one.</summary>
    public void Warm(ArtStyle style) => Task.Run(() => { foreach (var t in MusicGen.Tracks) Get(t, style); });

    /// <summary>Switches to a track (or to silence, for null), crossfading from the one playing.</summary>
    public void Play(string track, ArtStyle style)
    {
        string resolved = track == null ? null : MusicGen.Resolve(track);
        if (resolved == Track && style == Style) return;
        (_old, _oldPos) = (_cur, _curPos);
        _cur = resolved == null ? null : Get(resolved, style);
        _curPos = 0;
        _fade = _old == null ? 1 : 0;
        Track = resolved; Style = style;
    }

    /// <summary>Fills `buf` with the next samples at `volume` (0-1).</summary>
    public void Fill(short[] buf, float volume)
    {
        float step = 1f / (Fade * MusicGen.Rate);
        for (int i = 0; i < buf.Length; i++)
        {
            float v = 0;
            if (_cur != null) { v += _cur[_curPos] * _fade; _curPos = (_curPos + 1) % _cur.Length; }
            if (_old != null && _fade < 1) { v += _old[_oldPos] * (1 - _fade); _oldPos = (_oldPos + 1) % _old.Length; }
            if (_fade < 1) { _fade = MathF.Min(1, _fade + step); if (_fade >= 1) _old = null; }
            buf[i] = (short)Math.Clamp(v * volume, -32767f, 32767f);
        }
    }
}
