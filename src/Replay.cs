using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace HexenSharp;

/// <summary>
/// Deterministic input replays. The game is a pure function of its inputs: given the same start (the run's seed, the
/// mode, the class, every setting and the profile) and the same Input and frame time for every Update, it plays out
/// exactly the same. So a replay is just that start and the list of frames. Every run is recorded as you play (from
/// New game, a course, the arena and so on, and again at each Restart); 'replaysave' keeps one, 'replayplay' watches
/// it, and --replay plays one headlessly and prints where it ended. The self-test replays a few recorded runs and
/// checks each ends exactly where it did when recorded: whole-level regression tests.
/// </summary>
public sealed class Replay
{
    public const string Prefix = "HXR1.";

    /// <summary>How a run starts: enough to set it up again exactly.</summary>
    public sealed class StartInfo
    {
        public string Kind { get; set; } = "";   // campaign, practice, endless, arena, daily, rematch, story
        public int Seed { get; set; }
        public string Class { get; set; } = "";
        public string Style { get; set; } = "";
        public int NgTier { get; set; }
        public string Course { get; set; }
        public int CourseSeed { get; set; }
        public int ArenaMods { get; set; }
        public string Date { get; set; }
        public string Boss { get; set; }
        public int Case { get; set; }
        public string Name { get; set; } = "";
        public Dictionary<string, string> Vars { get; set; } = new();
        public string Profile { get; set; } = "";
    }

    public StartInfo Start = new();
    public readonly List<(float dt, Input inp)> Frames = new();
    public float Duration => Frames.Sum(f => f.dt);

    // ------------------------------------------------------------ recording

    /// <summary>How the run `g` just started, or null for a start a replay can't reproduce (a continued save, a play-tested map file).</summary>
    public static StartInfo Capture(Game g)
    {
        var s = new StartInfo
        {
            Seed = g.RunSeed, Class = g.P.Class.ToString(), Style = g.Style.ToString(), NgTier = g.NgTier, Name = g.RunnerName,
            Vars = SaveVars(g.Vars), Profile = g.Profile.ToJson(),
        };
        if (g.Practicing && g.Course.Endless) { s.Kind = "endless"; s.CourseSeed = g.Course.Seed; }
        else if (g.Practicing) { s.Kind = "practice"; s.Course = g.Course.Id; }
        else if (g.ArenaMode && g.DailyMode) { s.Kind = "daily"; s.Date = g.DailyDate.ToString("yyyy-MM-dd"); }
        else if (g.ArenaMode) { s.Kind = "arena"; s.ArenaMods = (int)g.ArenaMods; }
        else if (g.Rematch != null) { s.Kind = "rematch"; s.Boss = g.Rematch.MiniBoss; }
        else if (g.StoryMode) { s.Kind = "story"; s.Case = g.StoryCase; }
        else if (!g.TestingMap) s.Kind = "campaign";
        else return null;
        return s;
    }

    /// <summary>Every setting (they all can matter: mouse sensitivity turns you, difficulty hurts you).</summary>
    static Dictionary<string, string> SaveVars(GameVars v) =>
        typeof(GameVars).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.FieldType.IsValueType)
            .ToDictionary(f => f.Name, f => Convert.ToString(f.GetValue(v), System.Globalization.CultureInfo.InvariantCulture));

    static void LoadVars(GameVars v, Dictionary<string, string> vars)
    {
        foreach (var f in typeof(GameVars).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!vars.TryGetValue(f.Name, out var text)) continue;
            object val = f.FieldType.IsEnum ? Enum.Parse(f.FieldType, text)
                : Convert.ChangeType(text, f.FieldType, System.Globalization.CultureInfo.InvariantCulture);
            f.SetValue(v, val);
        }
    }

    // ------------------------------------------------------------ playing back

    /// <summary>A fresh game set up exactly as the run started, not yet stepped (nothing it does is saved).</summary>
    public Game Begin()
    {
        var s = Start;
        var g = new Game { FixedSeed = s.Seed, AchievementsOn = false, Profile = HexenSharp.Profile.FromJson(s.Profile), Replaying = true, RunnerName = s.Name };
        LoadVars(g.Vars, s.Vars);
        var cls = Enum.Parse<PClass>(s.Class);
        g.Style = Enum.TryParse<GameStyle>(s.Style, out var st) ? st : GameStyle.Classic;
        switch (s.Kind)
        {
            case "campaign":
                if (s.NgTier > 0) g.StartNewGamePlus(cls, s.NgTier);
                else { g.HubSource = Maps.BuildHub; g.NewGame(cls); }
                break;
            case "practice": g.StartPractice(cls, Courses.All.First(c => c.Id == s.Course)); break;
            case "endless": g.StartEndless(cls, s.CourseSeed); break;
            case "arena": g.ArenaMods = (ArenaMod)s.ArenaMods; g.StartArena(cls); break;
            case "daily": g.StartDaily(DateOnly.Parse(s.Date)); break;
            case "rematch": g.StartRematch(cls, MiniBosses.All.First(d => d.MiniBoss == s.Boss)); break;
            case "story": g.StartStory(s.Case); break;
            default: throw new InvalidDataException("unknown replay start: " + s.Kind);
        }
        return g;
    }

    /// <summary>Plays the whole replay (or its first `frames`) headlessly; returns the game where it ended.</summary>
    public Game Play(int frames = int.MaxValue)
    {
        var g = Begin();
        for (int i = 0; i < Frames.Count && i < frames; i++) g.Update(Frames[i].inp, Frames[i].dt);
        return g;
    }

    /// <summary>
    /// Where a game stands, as a line of text: two runs that played out the same give the same line. The mode and map,
    /// where you are and how you are, what you've done, and what's left alive and how hurt.
    /// </summary>
    public static string StateHash(Game g)
    {
        var p = g.P;
        var ms = g.Level.Things.OfType<Monster>().Where(m => m.Alive).ToList();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return string.Create(inv, $"{g.Mode} {g.Level.RawName} | at {p.X:0.000},{p.Y:0.000},{p.FloorZ + p.Z:0.000} facing {p.Angle:0.000}"
            + $" | hp {p.Health} armor {p.Armor} mana {p.BlueMana}/{p.GreenMana} kills {p.Kills} xp {g.RunXp}"
            + $" | monsters {ms.Count} hp {ms.Sum(m => m.Health)} | time {g.PlayTime:0.000} run {g.RunTime:0.000}");
    }

    // ------------------------------------------------------------ files

    /// <summary>The replay as one line of text: "HXR1." then the start (JSON) and the frames, deflated, in URL-safe base64.</summary>
    public string Encode()
    {
        using var raw = new MemoryStream();
        using (var w = new BinaryWriter(raw, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(JsonSerializer.Serialize(Start));
            w.Write(Frames.Count);
            foreach (var (dt, i) in Frames)
            {
                w.Write(dt);
                w.Write(Bits(i));
                w.Write(i.Move); w.Write(i.Strafe); w.Write(i.Turn); w.Write(i.LookX); w.Write(i.LookY);
                w.Write(i.KeyPressed); w.Write((sbyte)i.Slot); w.Write((sbyte)i.Cycle);
                w.Write(i.Typed ?? "");
            }
        }
        using var packed = new MemoryStream();
        using (var d = new DeflateStream(packed, CompressionLevel.SmallestSize, leaveOpen: true)) raw.WriteTo(d);
        return Prefix + Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static Replay Decode(string text)
    {
        if (text == null) return null;
        text = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (!text.StartsWith(Prefix)) return null;
        try
        {
            string b64 = text[Prefix.Length..].Replace('-', '+').Replace('_', '/');
            b64 += new string('=', (4 - b64.Length % 4) % 4);
            using var d = new DeflateStream(new MemoryStream(Convert.FromBase64String(b64)), CompressionMode.Decompress);
            using var r = new BinaryReader(d, Encoding.UTF8);
            var rep = new Replay { Start = JsonSerializer.Deserialize<StartInfo>(r.ReadString()) ?? new StartInfo() };
            int n = r.ReadInt32();
            if (n < 0 || n > 10_000_000) return null;
            for (int k = 0; k < n; k++)
            {
                float dt = r.ReadSingle();
                var i = FromBits(r.ReadInt32());
                i.Move = r.ReadSingle(); i.Strafe = r.ReadSingle(); i.Turn = r.ReadSingle(); i.LookX = r.ReadSingle(); i.LookY = r.ReadSingle();
                i.KeyPressed = r.ReadInt32(); i.Slot = r.ReadSByte(); i.Cycle = r.ReadSByte();
                string typed = r.ReadString();
                i.Typed = typed.Length == 0 ? null : typed;
                rep.Frames.Add((dt, i));
            }
            return rep;
        }
        catch (Exception e) when (e is FormatException or InvalidDataException or EndOfStreamException or IOException or JsonException) { return null; }
    }

    // the held and pressed buttons, one bit each
    static int Bits(Input i)
    {
        bool[] b = { i.Fire, i.Walk, i.JumpHeld, i.SlideHeld, i.JetHeld, i.Use, i.UseItem, i.Place, i.Journal, i.Map, i.Pause, i.Confirm, i.Up, i.Down,
                     i.Left, i.Right, i.Screenshot, i.Character, i.CycleHud, i.ConsoleToggle, i.Backspace, i.Tab, i.PageUp, i.PageDown, i.Jump, i.Slide, i.ZoomHeld };
        int bits = 0;
        for (int k = 0; k < b.Length; k++) if (b[k]) bits |= 1 << k;
        return bits;
    }

    static Input FromBits(int bits)
    {
        bool B(int k) => (bits & (1 << k)) != 0;
        return new Input
        {
            Fire = B(0), Walk = B(1), JumpHeld = B(2), SlideHeld = B(3), JetHeld = B(4), Use = B(5), UseItem = B(6), Place = B(7), Journal = B(8), Map = B(9),
            Pause = B(10), Confirm = B(11), Up = B(12), Down = B(13), Left = B(14), Right = B(15), Screenshot = B(16), Character = B(17), CycleHud = B(18),
            ConsoleToggle = B(19), Backspace = B(20), Tab = B(21), PageUp = B(22), PageDown = B(23), Jump = B(24), Slide = B(25), ZoomHeld = B(26),
        };
    }
}

public sealed partial class Game
{
    /// <summary>This game is a replay being played back: it records nothing and saves nothing.</summary>
    public bool Replaying;
    /// <summary>The run being recorded (from its start), and the last one that ended (back at the title).</summary>
    public Replay CurrentReplay, LastReplay;
    /// <summary>A replay being watched: its game (shown and heard instead of this one) and how far through it is.</summary>
    public Game Watch;
    public Replay Watching;
    int _watchFrame;
    float _watchClock;

    /// <summary>A run has just started (NewGame): start recording it, if a replay can reproduce its start.</summary>
    void BeginReplay()
    {
        if (Replaying) return;
        if (CurrentReplay is { Frames.Count: > 0 }) LastReplay = CurrentReplay; // Restart: the run before is the last one
        var start = Replay.Capture(this);
        CurrentReplay = start == null ? null : new Replay { Start = start };
    }

    /// <summary>The replay to save or watch: the run you're in, or the last one.</summary>
    public Replay RecentReplay => CurrentReplay is { Frames.Count: > 0 } ? CurrentReplay : LastReplay;

    /// <summary>Starts watching a replay (in place of this game, until it ends or Esc).</summary>
    public void WatchReplay(Replay r)
    {
        Watch = r.Begin();
        Watching = r;
        _watchFrame = 0; _watchClock = 0;
        Menu.Close(); Paused = false;
    }

    /// <summary>Plays the watched replay on by `realDt` of real time; false once it's over (or on Esc).</summary>
    public bool WatchStep(Input live, float realDt)
    {
        if (Watch == null) return false;
        if (live.Pause || _watchFrame >= Watching.Frames.Count) { StopWatching(); return false; }
        _watchClock += MathF.Min(realDt, 0.25f);
        for (int steps = 0; steps < 16 && _watchFrame < Watching.Frames.Count && _watchClock >= Watching.Frames[_watchFrame].dt; steps++)
        {
            var (dt, inp) = Watching.Frames[_watchFrame++];
            _watchClock -= dt;
            Watch.Update(inp, dt);
        }
        return true;
    }

    public void StopWatching()
    {
        Watch = null; Watching = null;
        Say("Replay over.");
    }

    /// <summary>Where saved replays go (next to your profile).</summary>
    public string ReplayDir => Path.Combine(Path.GetDirectoryName(ProfilePath ?? ConfigPath ?? Path.GetTempPath()) ?? ".", "replays");
}
