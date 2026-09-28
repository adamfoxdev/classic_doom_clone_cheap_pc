namespace HexenSharp;

/// <summary>
/// The shooting range (Practice > Shooting Range): every weapon in the game on a rack, yours to pick up whatever your
/// class, and a field of target dummies to try them on. Some stand still, some slide back and forth, and some wait up
/// on ledges you can only reach with a rocket jump. Mana never runs out, your health comes back, and your own rockets
/// can't finish you. Press Use for a one-minute drill: every dummy you drop scores (more for the moving and the high
/// ones), and the best drills go on your class's board.
/// </summary>
public static class ShootingRange
{
    public const string About = "EVERY WEAPON ON A RACK, TARGETS STILL, MOVING AND UP HIGH, AND LEDGES TO ROCKET JUMP ONTO.";
    const string Intro = "The shooting range: walk along the rack to take every weapon (1-9, 0 for the rocket launcher). Press Use for a drill.";

    public static readonly Course Course = new("range", "Shooting Range", About, Map, false, Intro, StartAngle: -MathF.PI / 2, Range: true);

    public const int W = 40, H = 30;
    /// <summary>The rack: one pickup per weapon, in a row across the back of the range.</summary>
    public const int RackY = 25, RackX0 = 11, RackStep = 2;
    public const float DrillTime = 60f, RespawnTime = 1.5f;
    /// <summary>Health back per second, after this long unhurt.</summary>
    public const float RegenRate = 60f, RegenDelay = 1.5f;
    /// <summary>A mover's slide: half its track's length, and its speed (radians a second through the sway).</summary>
    public const float MoveSpan = 5f, MoveRate = 0.9f;
    /// <summary>The ledges: rocket-jump height (2 cells up), and double that for the far one.</summary>
    public const float LowLedge = 2f, HighLedge = 3.5f;

    public enum Kind { Still, Moving, High }

    /// <summary>The key for a weapon on the rack: 1 to 9, then 0, -, =, [ and ] for the Quake weapons.</summary>
    public static string KeyFor(int slot) => slot < 9 ? (slot + 1).ToString() : slot switch { 9 => "0", 10 => "-", 11 => "=", 12 => "[", _ => "]" };

    public static int Points(Kind k) => k switch { Kind.Moving => 150, Kind.High => 200, _ => 100 };

    /// <summary>The dummies: where each stands and what kind it is.</summary>
    public static readonly (float x, float y, Kind kind)[] Targets =
    {
        (8.5f, 14.5f, Kind.Still), (14.5f, 13.5f, Kind.Still), (20.5f, 14.5f, Kind.Still), (26.5f, 13.5f, Kind.Still),
        (13.5f, 8.5f, Kind.Moving), (25.5f, 5.5f, Kind.Moving),
        (35.5f, 13.5f, Kind.High), (35.5f, 4.5f, Kind.High), (4.5f, 4.5f, Kind.High),
    };

    public static readonly MonsterDef Dummy = new()
    {
        Name = "Target Dummy", Art = "dummy", Health = 100, Speed = 0, Radius = 0.3f, Width = 0.7f, Height = 0.95f, PainChance = 0,
        SightRange = 0,
    };

    /// <summary>
    /// The range: an open yard walled round, the rack across the south end, the firing line in front of it, and the
    /// field to the north, with a ledge on the east side (2 cells up), a higher one in the north-east corner, and one in
    /// the north-west.
    /// </summary>
    public static MapDef Map()
    {
        var rows = new char[H][];
        for (int y = 0; y < H; y++)
        {
            rows[y] = new char[W];
            for (int x = 0; x < W; x++) rows[y][x] = x == 0 || y == 0 || x == W - 1 || y == H - 1 ? '#' : ',';
        }
        foreach (var (x, y) in new[] { (1, 28), (38, 28), (1, 20), (38, 20), (9, 28), (30, 28) }) rows[y][x] = 't';
        rows[27][20] = '@';
        var def = new MapDef("Shooting Range", "The shooting range. Every weapon on the rack is yours.", "meadow",
            rows.Select(r => new string(r)).ToArray(), Height: 8f);
        return Maps.Elevate(def,
            (33, 10, 38, 17, Maps.FloorGlyph(LowLedge)),
            (33, 1, 38, 7, Maps.FloorGlyph(HighLedge)),
            (1, 1, 7, 7, Maps.FloorGlyph(LowLedge)));
    }

    /// <summary>A target dummy's picture: a straw man on a post with a painted target on its chest.</summary>
    public static void BuildArt() => Art.Monsters["dummy"] = Art.PoseSet(DrawDummy);

    static void DrawDummy(Canvas c, Pose p)
    {
        uint straw = Col.Rgb(214, 180, 96), wood = Col.Rgb(110, 76, 40), red = Col.Rgb(210, 40, 40), white = Col.Rgb(240, 236, 224);
        int lean = p == Pose.Pain ? 4 : p == Pose.Walk1 ? 1 : 0;
        c.Rect(30, 34, 4, 30, wood);                       // post
        c.Rect(12 + lean, 22, 40, 5, wood);                // cross bar (arms)
        c.Ellipse(32 + lean, 34, 13, 16, straw);           // body
        c.Circle(32 + lean, 12, 8, straw);                 // head
        c.Circle(32 + lean, 34, 9, white);
        c.Circle(32 + lean, 34, 6, red);
        c.Circle(32 + lean, 34, 3, white);
        c.Circle(32 + lean, 34, 1.5f, red);
        if (p == Pose.Pain) c.Glow(32 + lean, 34, 12, Col.Rgb(255, 220, 120));
    }
}

/// <summary>One drill on the shooting range: its score, kills and shots.</summary>
public sealed class RangeRun
{
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public int Score { get; set; }
    public int Kills { get; set; }
    public int Shots { get; set; }
    public DateTime When { get; set; }
}

/// <summary>A dummy's place on the range: where it stands, its kind, and (once down) when it stands back up.</summary>
public sealed class RangeTarget
{
    public float HomeX, HomeY, Phase;
    public ShootingRange.Kind Kind;
    public float DownFor;
}

public sealed partial class Game
{
    public bool OnRange => Practicing && Course.Range;
    /// <summary>A drill is on: seconds left, the score so far, dummies dropped and shots fired.</summary>
    public bool Drilling;
    public float DrillLeft;
    public int DrillScore, DrillKills, DrillShots;
    /// <summary>The last drill that ended, and its place on your class's board (0 if off it).</summary>
    public RangeRun LastDrill;
    public int LastDrillPlace;
    float _unhurt, _regen;
    /// <summary>
    /// The range and the rocket-jump course: mana never runs out, health comes back, and your own rockets can't kill you.
    /// </summary>
    /// <summary>A rail trial is on (see RailTrials).</summary>
    public bool RailTrial;
    public bool SafeRockets => Practicing && (Course.Range || Course.Rockets || Course.Grenades || Course.Tower);

    /// <summary>On a fresh range: the full loadout (your class's first weapon in hand), the rack, and the dummies.</summary>
    void SetUpRange()
    {
        var p = P;
        p.Loadout = Rockets.AllWeapons();
        p.HasWeapon = new bool[p.Loadout.Length];
        p.Weapon = (int)p.Class * 3; p.PendingWeapon = -1;
        p.HasWeapon[p.Weapon] = true;
        p.BlueMana = p.GreenMana = 200;
        for (int i = 0; i < p.Loadout.Length; i++)
        {
            var pk = new Pickup(PickupKind.Arms, 0.6f, i)
                { X = ShootingRange.RackX0 + i * ShootingRange.RackStep + 0.5f, Y = ShootingRange.RackY + 0.5f, Level = Level };
            pk.SpriteH = 0.38f;
            Level.Things.Add(pk);
        }
        foreach (var (x, y, kind) in ShootingRange.Targets)
        {
            var m = new Monster(ShootingRange.Dummy) { X = x, Y = y, Level = Level, State = AiState.Idle };
            m.Target = new RangeTarget { HomeX = x, HomeY = y, Kind = kind, Phase = x };
            Level.Things.Add(m);
        }
        Drilling = false; DrillLeft = 0; _unhurt = 0;
    }

    /// <summary>Takes a weapon from the rack (it stays there: the rack never empties).</summary>
    void TakeFromRack(Pickup pk)
    {
        var p = P;
        if (p.Loadout == null || pk.Variant >= p.HasWeapon.Length || p.HasWeapon[pk.Variant]) return;
        p.HasWeapon[pk.Variant] = true;
        p.PickupFlash = 1;
        PlaySound(Sfx.Item, 1);
        var w = p.Loadout[pk.Variant];
        Say($"{w.Name}! (key {ShootingRange.KeyFor(pk.Variant)})" + (w.Rocket ? " Fire at your feet as you jump to rocket jump."
            : w.Rail ? " One slug goes through everything in its line."
            : w.Grenade ? " Grenades bounce, and go off after two and a half seconds or on a monster." : ""));
        SelectWeapon(pk.Variant);
    }

    /// <summary>The range, every frame: health coming back, dummies standing back up and moving, and the drill's clock.</summary>
    void RangeTick(float dt)
    {
        if (!SafeRockets || Mode != GameMode.Playing) return;
        var p = P;
        _unhurt += dt;
        if (p.DamageFlash > 0.5f) _unhurt = 0;
        if (_unhurt > ShootingRange.RegenDelay && p.Health < p.MaxHealth)
        {
            _regen += ShootingRange.RegenRate * dt;
            int heal = (int)_regen;
            _regen -= heal;
            p.Health = Math.Min(p.MaxHealth, p.Health + heal);
        }
        p.BlueMana = p.GreenMana = 200;
        for (int k = 1; k < QuakeAmmo.Kinds; k++) p.Ammo[k] = QuakeAmmo.Max((AmmoKind)k);
        if (!OnRange) return;
        foreach (var m in Level.Things.OfType<Monster>().Where(m => m.Target != null && !m.Alive).ToList())
            if ((m.Target.DownFor += dt) >= ShootingRange.RespawnTime) StandUp(m);
        if (!Drilling) return;
        DrillLeft -= dt;
        if (DrillLeft <= 0) EndDrill();
    }

    /// <summary>A dummy stands back up at its spot, whole.</summary>
    void StandUp(Monster m)
    {
        var tg = m.Target;
        m.X = tg.HomeX; m.Y = tg.HomeY; m.KnockX = m.KnockY = 0;
        m.Health = m.MaxHealth;
        m.State = AiState.Idle; m.StateTime = 0;
        tg.DownFor = 0;
        SpawnPuff(Art.Bolt[1], m.X, m.Y, Level.FloorAt(m.X, m.Y) + 0.5f, 0.5f);
    }

    /// <summary>A dummy's own "thinking": a mover slides along its track; a still one drifts home after a push.</summary>
    void DummyTick(Monster m, float dt)
    {
        var tg = m.Target;
        if (!m.Alive) return;
        if (m.State == AiState.Pain && m.StateTime > 0.2f) m.State = AiState.Idle;
        if (tg.Kind == ShootingRange.Kind.Moving)
        {
            tg.Phase += dt * ShootingRange.MoveRate;
            m.X = tg.HomeX + MathF.Sin(tg.Phase) * ShootingRange.MoveSpan;
            m.Anim += dt;
            return;
        }
        if (m.KnockX != 0 || m.KnockY != 0) return;
        float k = MathF.Min(1, dt * 1.5f);
        float nx = m.X + (tg.HomeX - m.X) * k, ny = m.Y + (tg.HomeY - m.Y) * k;
        if (!Blocked(nx, ny, m.Radius, m)) (m.X, m.Y) = (nx, ny);
    }

    /// <summary>A dummy was hit: it flinches; if it went down in a drill, its points.</summary>
    void DummyHit(Monster m, bool down)
    {
        if (!down) { m.State = AiState.Pain; m.StateTime = 0; return; }
        m.Target.DownFor = 0;
        if (Course.Grenades) { GrenadeTargetDown(); return; }
        if (!Drilling) return;
        int pts = ShootingRange.Points(m.Target.Kind);
        DrillScore += pts; DrillKills++;
    }

    /// <summary>Use on the range: starts a drill (or restarts one), every dummy up and the clock at a minute.</summary>
    public void StartDrill()
    {
        if (!OnRange) return;
        foreach (var m in Level.Things.OfType<Monster>().Where(m => m.Target != null).ToList()) StandUp(m);
        Drilling = true; DrillLeft = ShootingRange.DrillTime; DrillScore = DrillKills = DrillShots = 0;
        Messages.Clear();
        PlaySound(Sfx.BossSight, 0.6f);
        Say("Drill! One minute: 100 a dummy, 150 a moving one, 200 one up on a ledge.");
    }

    /// <summary>The drill's time is up: its score, on your class's board if it's good enough.</summary>
    void EndDrill()
    {
        Drilling = false; DrillLeft = 0;
        Messages.Clear();
        if (PracticeSpeed < 1)
        {
            Say($"Drill over: {DrillScore} at {PracticeSpeed * 100:0}% speed. Press 1 for full speed to set a record.");
            return;
        }
        var run = new RangeRun { Name = RunnerName, Class = P.Class.ToString(), Score = DrillScore, Kills = DrillKills, Shots = DrillShots, When = DateTime.Now };
        int best = Profile.RangeBest(P.Class);
        int place = DrillScore > 0 ? Profile.AddRangeRun(run) : 0;
        if (place > 0) SaveProfile();
        LastDrill = run; LastDrillPlace = place;
        PlaySound(place == 1 ? Sfx.Secret : Sfx.Teleport, 1);
        string how = $"Drill over: {DrillScore} points, {DrillKills} down, {DrillShots} shots";
        Say(place == 1 ? $"{how} - a new best!" : place > 0 ? $"{how} - #{place} on the board (best {best})." : $"{how} (best {best}).");
    }
}
