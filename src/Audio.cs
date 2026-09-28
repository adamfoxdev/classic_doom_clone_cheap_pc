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
    // the music: one stream, fed from the mixer a block at a time
    const int MusicBlock = 4096;
    readonly MusicMixer _mixer = new();
    readonly short[] _musicBuf = new short[MusicBlock];
    AudioStream _music;
    bool _musicOn;

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

    /// <summary>
    /// Keeps the music going: call once a frame with the track to play (null for none), its volume (0-1) and how much
    /// of its tension layer to bring in (0-1).
    /// </summary>
    public void UpdateMusic(string track, float volume, float intensity = 0)
    {
        if (!_musicOn)
        {
            Raylib.SetAudioStreamBufferSizeDefault(MusicBlock);
            _music = Raylib.LoadAudioStream(MusicGen.Rate, 16, 1);
            Raylib.PlayAudioStream(_music);
            _musicOn = true;
            _mixer.Warm(Art.Style);
            _mixer.Warm(Art.Style == ArtStyle.SciFi ? ArtStyle.Fantasy : ArtStyle.SciFi);
        }
        _mixer.Play(volume > 0 ? track : null, Art.Style);
        _mixer.Intensity = intensity;
        while (Raylib.IsAudioStreamProcessed(_music))
        {
            _mixer.Fill(_musicBuf, Math.Clamp(volume, 0f, 1f) * 0.6f);
            fixed (short* p = _musicBuf) Raylib.UpdateAudioStream(_music, p, MusicBlock);
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
        if (_musicOn) Raylib.UnloadAudioStream(_music);
        foreach (var bank in _banks)
            foreach (var set in bank)
            {
                for (int v = 1; v < Voices; v++) Raylib.UnloadSoundAlias(set[v]);
                Raylib.UnloadSound(set[0]);
            }
    }
}
