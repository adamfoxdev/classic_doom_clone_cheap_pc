namespace HexenSharp;

/// <summary>What a platform plays sound through: the desktop's Raylib, or the browser's Web Audio.</summary>
public interface IHostAudio
{
    void Play(Sfx s, float volume);
    /// <summary>Called every frame: the track to play (null for none), its volume (0-1) and its tension layer (0-1).</summary>
    void UpdateMusic(string track, float volume, float intensity);
}

/// <summary>
/// One frame of the game as every platform runs it: the desktop window (Program) and the browser (web/WebHost) both
/// read their keys, mouse and pad into an <see cref="Input"/> and hand it here, so what a frame does (the pad, a
/// replay being watched, the music, the picture, screenshots) is the same code in both.
/// </summary>
public sealed class Host
{
    public readonly Game Game;
    public readonly Renderer Renderer = new();
    public IHostAudio Audio;
    /// <summary>Where a screenshot goes: a file beside the game on the desktop, a download in the browser.</summary>
    public Action<string, byte[]> SaveShot = (name, png) => File.WriteAllBytes(name, png);
    readonly Gamepad _pad = new();
    bool _padSeen;
    int _shotIndex;

    public Host(Game game) { Game = game; }

    /// <summary>
    /// The mouse aims (it's captured: hidden, and moves turn you) while you're playing, not in a menu or the console.
    /// The platform captures it when this says so, and passes its movement in the input's LookX and LookY.
    /// </summary>
    public bool WantsMouse => Game.Mode is GameMode.Playing or GameMode.Dead && !Game.Menu.Open && !Game.Con.Open;

    /// <summary>A frame: the input (keys and mouse), the first gamepad's state and name, and the time since the last.</summary>
    public void Frame(Input inp, PadState pad, string padName, float dt)
    {
        var game = Game;
        if (pad.Connected != _padSeen)
        {
            _padSeen = pad.Connected;
            game.Say(_padSeen ? $"Gamepad connected: {padName}" : "Gamepad disconnected.");
        }
        bool inMenu = game.Menu.Open || game.Mode is GameMode.Title or GameMode.ClassSelect or GameMode.Victory;
        if (!game.Con.Open) _pad.Apply(ref inp, pad, dt, inMenu, game.Vars.PadLook);

        // watching a replay, it plays instead (Esc stops it); otherwise the game takes your input
        if (game.Watch != null) game.WatchStep(inp, dt);
        else game.Update(inp, dt);
        var shown = game.Watch ?? game;
        Audio?.UpdateMusic(shown.MusicTrack, game.Vars.Music, shown.MusicIntensity);
        Renderer.Render(shown);

        if (inp.Screenshot)
        {
            string name = $"hexen_shot_{_shotIndex++:000}.png";
            SaveShot(name, Png.Encode(Renderer.Fb, Renderer.W, Renderer.H));
            game.Say("Saved " + name);
        }
    }

    /// <summary>The version: set by release builds (-p:Version=), "dev" otherwise.</summary>
    public static string Version =>
        typeof(Host).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] is { } v && v != "1.0.0" ? v : "dev";
}
