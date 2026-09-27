using Raylib_cs;

namespace HexenSharp;

/// <summary>
/// Plays the synthesized sound effects (see <see cref="Sounds"/>) through Raylib. Both style banks are built at
/// startup; each sound plays from the bank matching the current visual style.
/// </summary>
public sealed unsafe class Audio : IDisposable
{
    const int Voices = 4;
    static readonly ArtStyle[] Styles = { ArtStyle.Fantasy, ArtStyle.SciFi };
    readonly Sound[][][] _banks = new Sound[Styles.Length][][];
    readonly int[] _next = new int[(int)Sfx.Count];
    readonly Random _rng = new(7);

    public Audio()
    {
        foreach (var style in Styles)
        {
            var bank = _banks[(int)style] = new Sound[(int)Sfx.Count][];
            for (int i = 0; i < (int)Sfx.Count; i++)
            {
                var samples = Sounds.Make((Sfx)i, style);
                Sound baseSound;
                fixed (short* p = samples)
                {
                    var wave = new Wave { SampleCount = (uint)samples.Length, SampleRate = Sounds.Rate, SampleSize = 16, Channels = 1, Data = p };
                    baseSound = Raylib.LoadSoundFromWave(wave);
                }
                bank[i] = new Sound[Voices];
                bank[i][0] = baseSound;
                for (int v = 1; v < Voices; v++) bank[i][v] = Raylib.LoadSoundAlias(baseSound);
            }
        }
    }

    public void Play(Sfx s, float volume)
    {
        int i = (int)s;
        var snd = _banks[(int)Art.Style][i][_next[i]];
        _next[i] = (_next[i] + 1) % Voices;
        Raylib.SetSoundVolume(snd, Math.Clamp(volume, 0f, 1f) * 0.7f);
        Raylib.SetSoundPitch(snd, 0.92f + (float)_rng.NextDouble() * 0.16f);
        Raylib.PlaySound(snd);
    }

    public void Dispose()
    {
        foreach (var bank in _banks)
            foreach (var set in bank)
            {
                for (int v = 1; v < Voices; v++) Raylib.UnloadSoundAlias(set[v]);
                Raylib.UnloadSound(set[0]);
            }
    }
}
