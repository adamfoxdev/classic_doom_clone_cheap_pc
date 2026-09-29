using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;

namespace HexenSharp;

/// <summary>
/// The browser version's side of the page (wwwroot/main.js is the other half). The page gathers the keys, mouse and
/// pad each animation frame and calls <see cref="Frame"/>, which runs the very same frame the desktop does
/// (<see cref="Host"/>) and hands back the picture. Saved files live in the browser's storage between visits.
/// </summary>
public static partial class WebHost
{
    /// <summary>Where the game's files go (settings, profile, save, maps), mirrored to the browser's localStorage.</summary>
    const string Root = "/hexen";
    static Game _game;
    static Host _host;
    static readonly WebKeys Keys = new();
    static readonly Dictionary<string, string> Stored = new();
    static float _syncIn;
    static bool _mouseOn;
    static int _skipMouse;

    public static void Main() { } // everything starts from Start, once the page has its functions in place

    /// <summary>
    /// Sets the game up: the files saved on an earlier visit (JSON, path to text), and the matchmaker the page came
    /// from (empty for the default), which is used unless you've picked one yourself.
    /// </summary>
    [JSExport]
    public static void Start(string matchmakerUrl, string storedFiles)
    {
        Art.Init();
        Renderer.Threads = 1; // (a browser tab runs the game on one thread)
        try
        {
            foreach (var (path, text) in JsonSerializer.Deserialize<Dictionary<string, string>>(storedFiles ?? "{}") ?? new())
                if (path.StartsWith(Root + "/", StringComparison.Ordinal) && !path.Contains(".."))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, text);
                    Stored[path] = text;
                }
        }
        catch (Exception e) { Console.WriteLine("couldn't restore saved files: " + e.Message); }

        _game = new Game
        {
            ConfigPath = Root + "/settings.cfg",
            MapsDir = Root + "/maps",
            ProfilePath = Root + "/profile.json",
            SavePath = Root + "/save.json",
            Matchmaker = new MatchmakerClient(Game.DefaultMatchmakerUrl, Host.Version),
            MatchmakerVersion = Host.Version,
            CanQuit = false,
            CopyText = CopyText,
            PasteText = () => null, // (Ctrl+V pastes: the page types what's pasted in)
        };
        _game.LoadSettings();
        if (!string.IsNullOrEmpty(matchmakerUrl) && _game.SavedMatchmakerUrl == null) _game.SetMatchmakerUrl(matchmakerUrl, save: false);
        _game.LoadProfile();

        var audio = new WebAudio();
        _game.PlaySound = audio.Play;
        _host = new Host(_game) { Audio = audio, SaveShot = (name, png) => Download(name, png) };
    }

    /// <summary>
    /// One animation frame: the keys held and pressed since the last (by the game's key codes: GLFW's, and 1001 up for
    /// the mouse buttons and wheel), the mouse's movement, what was typed, the first gamepad, and whether the mouse is
    /// locked to the page. Returns whether the game wants the mouse (for aiming), so the page can ask for it.
    /// </summary>
    [JSExport]
    public static bool Frame(double dt, byte[] down, byte[] pressed, double mouseX, double mouseY, string typed,
        bool padOn, double[] padAxes, int padButtons, string padName, bool mouseLocked)
    {
        Keys.Set(down, pressed);
        var inp = _game.Binds.Read(Keys, _game.Con.Open);
        inp.KeyPressed = Keys.FirstPressed();
        inp.Typed = typed ?? "";

        // aiming: the first movement after the mouse is locked can be a jump, so it's skipped
        bool mouseOn = _host.WantsMouse && mouseLocked;
        if (mouseOn != _mouseOn) { _mouseOn = mouseOn; _skipMouse = 1; }
        if (mouseOn && _skipMouse-- <= 0) { inp.LookX = (float)mouseX; inp.LookY = (float)mouseY; }

        var pad = new PadState();
        if (padOn && padAxes is { Length: >= 6 })
            pad = new PadState
            {
                Connected = true,
                LX = (float)padAxes[0], LY = (float)padAxes[1], RX = (float)padAxes[2], RY = (float)padAxes[3],
                LT = (float)padAxes[4], RT = (float)padAxes[5], Held = (PadButton)padButtons,
            };

        _host.Frame(inp, pad, padName, (float)Math.Clamp(dt, 0, 0.1));
        _game.QuitRequested = false; // (a tab can't close itself: the console's quit does nothing)
        Present(MemoryMarshal.AsBytes(_host.Renderer.Fb.AsSpan()));

        if ((_syncIn -= (float)dt) <= 0) { _syncIn = 2; Flush(); }
        return _host.WantsMouse;
    }

    /// <summary>Copies any file the game wrote since last time into the browser's storage (and forgets deleted ones).</summary>
    [JSExport]
    public static void Flush()
    {
        try
        {
            var seen = new HashSet<string>();
            if (Directory.Exists(Root))
                foreach (var path in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                {
                    seen.Add(path);
                    string text = File.ReadAllText(path);
                    if (Stored.TryGetValue(path, out var old) && old == text) continue;
                    StoreFile(path, text);
                    Stored[path] = text;
                }
            foreach (var gone in Stored.Keys.Where(p => !seen.Contains(p)).ToList())
            {
                RemoveFile(gone);
                Stored.Remove(gone);
            }
        }
        catch (Exception e) { Console.WriteLine("couldn't store files: " + e.Message); }
    }

    /// <summary>For the tests: where the game is (its mode, and online, the room and the session's tick and checksum).</summary>
    [JSExport]
    public static string Describe() => JsonSerializer.Serialize(new
    {
        mode = _game.Mode.ToString(),
        menu = _game.Menu.Page?.ToString(),
        inRoom = _game.Matchmaker?.InRoom == true,
        players = _game.Matchmaker?.Ticket?.Room?.Players ?? 0,
        status = _game.Matchmaker?.Status,
        online = _game.NetSession != null,
        tick = _game.NetSession?.TickCount ?? 0,
        hash = _game.NetSession != null ? _game.OnlineHash().ToString("x16") : null,
        chat = _game.Chat.Select(c => c.Name + ": " + c.Text).ToArray(),
    });

    /// <summary>The cross-play check: hashes of a scripted two-player session, to compare with the desktop's.</summary>
    [JSExport]
    public static string SyncProbe() => Headless.SyncProbe();

    [JSImport("present", "host")]
    static partial void Present([JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);
    [JSImport("storeFile", "host")]
    static partial void StoreFile(string path, string text);
    [JSImport("removeFile", "host")]
    static partial void RemoveFile(string path);
    [JSImport("copyText", "host")]
    static partial void CopyText(string text);
    [JSImport("download", "host")]
    static partial void Download(string name, [JSMarshalAs<JSType.MemoryView>] Span<byte> data);
}

/// <summary>The keys as the page last saw them.</summary>
sealed class WebKeys : IKeySource
{
    byte[] _down = Array.Empty<byte>(), _pressed = Array.Empty<byte>();

    public void Set(byte[] down, byte[] pressed) { _down = down ?? Array.Empty<byte>(); _pressed = pressed ?? Array.Empty<byte>(); }
    public bool Down(int code) => code > 0 && code < _down.Length && _down[code] != 0;
    public bool Pressed(int code) => code > 0 && code < _pressed.Length && _pressed[code] != 0;

    /// <summary>A key pressed this frame (for rebinding): a keyboard key first, then a mouse button, then the wheel.</summary>
    public int FirstPressed()
    {
        for (int c = 1; c < _pressed.Length; c++) if (_pressed[c] != 0 && c < 1000) return c;
        for (int c = HexenSharp.Keys.Mouse1; c <= HexenSharp.Keys.Mouse5; c++) if (Pressed(c)) return c;
        if (Pressed(HexenSharp.Keys.WheelUp)) return HexenSharp.Keys.WheelUp;
        if (Pressed(HexenSharp.Keys.WheelDown)) return HexenSharp.Keys.WheelDown;
        return HexenSharp.Keys.None;
    }
}

/// <summary>
/// Sound through the page's Web Audio: the effects are made here once (the same synthesis as the desktop) and handed
/// over to play; the music is mixed here a block at a time and queued up there a little ahead.
/// </summary>
sealed partial class WebAudio : IHostAudio
{
    const int MusicBlock = 4096;
    /// <summary>How far ahead the music is queued, in seconds: enough to ride out a slow frame.</summary>
    const double Ahead = 0.4;
    readonly MusicMixer _mixer = new();
    readonly short[] _musicBuf = new short[MusicBlock];
    readonly Random _rng = new(7);

    public WebAudio()
    {
        foreach (var style in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
            for (int i = 0; i < (int)Sfx.Count; i++)
            {
                var samples = Sounds.Make((Sfx)i, style);
                LoadSound((int)style, i, Sounds.Rate, MemoryMarshal.AsBytes(samples.AsSpan()));
            }
    }

    public void Play(Sfx s, float volume) =>
        PlaySound((int)Art.Style, (int)s, Math.Clamp(volume, 0f, 1f) * 0.7f, 0.92 + _rng.NextDouble() * 0.16);

    public void UpdateMusic(string track, float volume, float intensity)
    {
        _mixer.Play(volume > 0 ? track : null, Art.Style);
        _mixer.Intensity = intensity;
        for (int blocks = 0; blocks < 4 && MusicAhead() < Ahead; blocks++)
        {
            _mixer.Fill(_musicBuf, Math.Clamp(volume, 0f, 1f) * 0.6f);
            PushMusic(MusicGen.Rate, MemoryMarshal.AsBytes(_musicBuf.AsSpan()));
        }
    }

    [JSImport("loadSound", "host")]
    static partial void LoadSound(int style, int id, int rate, [JSMarshalAs<JSType.MemoryView>] Span<byte> pcm16);
    [JSImport("playSound", "host")]
    static partial void PlaySound(int style, int id, double volume, double rate);
    /// <summary>Seconds of music queued and not yet heard (a lot while sound is off, so none is made).</summary>
    [JSImport("musicAhead", "host")]
    private static partial double MusicAhead();
    [JSImport("pushMusic", "host")]
    static partial void PushMusic(int rate, [JSMarshalAs<JSType.MemoryView>] Span<byte> pcm16);
}
