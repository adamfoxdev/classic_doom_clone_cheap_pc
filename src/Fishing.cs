namespace HexenSharp;

/// <summary>
/// Fishing (Practice > Fishing): a lake sunk below a grassy shore, a wooden dock out into it, and a rod. Fire casts
/// the bobber where you're looking (look up to cast further); it floats until a fish bites, when it dips and splashes:
/// fire again quickly to strike. Then hold fire to reel the fish in, but ease off while it runs (the HUD shows when),
/// or the line's tension climbs until it snaps. Further out, the water's deeper and the fish are bigger and fight
/// harder. Three minutes a session from your first cast; the weight you land goes on a board for each class. Fall in
/// and you're back on the dock.
/// </summary>
public static class Fishing
{
    public const float Length = 180f;
    public const int W = 40, H = 32;
    /// <summary>The lake: an oval, its middle and half-widths, sunk Bank below the shore.</summary>
    public const float LakeX = 20f, LakeY = 13.5f, LakeRX = 16f, LakeRY = 10.5f, Bank = 1f;
    /// <summary>The dock: two cells wide from the south shore out into the lake, at the shore's height; you start at its end.</summary>
    public const int DockX = 19, DockY0 = 17, DockY1 = 25;
    public const float StartX = 20f, StartY = 18f;
    /// <summary>A cast: its speed along your aim and its kick up, and the bobber's fall.</summary>
    public const float CastSpeed = 8.5f, CastUp = 2.2f, Gravity = 9f;
    /// <summary>The strike: how long a bite lasts before the fish lets go.</summary>
    public const float StrikeWindow = 0.9f;
    /// <summary>Reeling: its speed, how fast the tension climbs (reeling while the fish runs, and a little otherwise) and eases, and how far it can run before it's gone.</summary>
    public const float ReelSpeed = 2.2f, RunStrain = 0.85f, RestStrain = 0.12f, Ease = 0.55f, Escape = 18f;

    public const string About = "CAST FROM THE DOCK, STRIKE WHEN IT BITES, AND REEL IT IN WITHOUT SNAPPING THE LINE.";
    const string Intro = "Fishing: fire to cast (look up to cast further). When it bites, fire to strike, then hold fire to reel - but ease off while it runs.";

    public static readonly Course Course = new("fishing", "Fishing", About, Map, false, Intro, StartAngle: -MathF.PI / 2, Fishing: true);

    public static readonly WeaponDef Rod = new()
    {
        Name = "Fishing Rod", Rod = true, ArtIndex = Art.RodArt, Cooldown = 0.25f, Sound = Sfx.Swing,
    };

    public static bool InLake(float x, float y)
    {
        float dx = (x - LakeX) / LakeRX, dy = (y - LakeY) / LakeRY;
        return dx * dx + dy * dy < 1f;
    }

    public static bool OnDock(int x, int y) => x >= DockX && x <= DockX + 1 && y >= DockY0 && y <= DockY1;

    /// <summary>Is the cell water (in the lake and not the dock)?</summary>
    public static bool Water(int x, int y) => InLake(x + 0.5f, y + 0.5f) && !OnDock(x, y);

    public static MapDef Map()
    {
        var rows = new char[H][];
        var floors = new char[H][];
        char shore = Maps.FloorGlyph(Bank);
        for (int y = 0; y < H; y++)
        {
            rows[y] = new char[W];
            floors[y] = new char[W];
            for (int x = 0; x < W; x++)
            {
                bool border = x == 0 || y == 0 || x == W - 1 || y == H - 1;
                rows[y][x] = border ? 'M' : ',';
                floors[y][x] = border || Water(x, y) ? '.' : shore;
            }
        }
        // trees round the shore
        foreach (var (x, y) in new[] { (3, 3), (8, 1), (30, 2), (36, 4), (2, 12), (38, 16), (3, 26), (9, 29), (30, 29), (36, 27), (14, 28), (25, 27) })
            if (!Water(x, y) && !OnDock(x, y)) rows[y][x] = 'T';
        rows[(int)StartY][(int)StartX] = '@';
        return new MapDef("The Lake", "The Lake. Cast from the dock; strike when it bites.", "meadow",
            rows.Select(r => new string(r)).ToArray(), Height: 6f) { Floors = floors.Select(r => new string(r)).ToArray() };
    }

    /// <summary>How deep the water is at a spot: how far it is in from the shore, in cells (0 at the edge).</summary>
    public static float Depth(float x, float y)
    {
        float dx = (x - LakeX) / LakeRX, dy = (y - LakeY) / LakeRY;
        float r = MathF.Sqrt(dx * dx + dy * dy);
        return MathF.Max(0, (1 - r) * MathF.Min(LakeRX, LakeRY));
    }

    // ------------------------------------------------------------------ the fish

    public sealed record Species(string Name, (int r, int g, int b) Colour, (int r, int g, int b) Belly, float MinKg, float MaxKg, float Strength, Func<float, float> Weight, bool Junk = false);

    /// <summary>What's in the lake, and how likely each is at a depth (the weights): small fish near the shore, big ones out deep.</summary>
    public static readonly Species[] All =
    {
        new("Bluegill", (70, 110, 150), (230, 170, 80), 0.1f, 0.5f, 0.6f, d => MathF.Max(0.5f, 6 - d * 0.8f)),
        new("Perch", (120, 150, 60), (230, 200, 120), 0.2f, 0.9f, 0.8f, d => 4f),
        new("Trout", (120, 130, 110), (230, 150, 150), 0.5f, 2.5f, 1.1f, d => 2 + d * 0.3f),
        new("Bass", (70, 100, 50), (210, 210, 170), 0.8f, 3.5f, 1.3f, d => 1 + d * 0.5f),
        new("Pike", (90, 120, 70), (220, 220, 160), 2f, 7f, 1.7f, d => d * 0.45f),
        new("Catfish", (90, 80, 70), (200, 190, 170), 3f, 12f, 2f, d => MathF.Max(0, d - 3) * 0.5f),
        new("Golden Carp", (235, 180, 40), (255, 230, 120), 5f, 9f, 1.5f, d => 0.12f),
        new("Old Boot", (90, 60, 35), (60, 40, 25), 0f, 0f, 0.3f, d => 0.8f, Junk: true),
    };

    /// <summary>A catch: the species (by a roll against the depth's weights) and its weight.</summary>
    public static (Species s, float kg) Roll(float depth, float r1, float r2)
    {
        float total = All.Sum(s => s.Weight(depth)), pick = r1 * total;
        var sp = All[^1];
        foreach (var s in All)
        {
            pick -= s.Weight(depth);
            if (pick <= 0) { sp = s; break; }
        }
        // bigger out deep: the weight leans toward the top of its range with depth
        float lean = MathF.Min(1, depth / 8f), t = MathF.Min(1, r2 * (0.6f + 0.6f * lean));
        return (sp, MathF.Round((sp.MinKg + (sp.MaxKg - sp.MinKg) * t) * 100) / 100);
    }

    static readonly Dictionary<string, Tex> _fishTex = new();

    /// <summary>A fish's picture, side on (facing left): its body, a paler belly, a tail, a fin and an eye; or the boot.</summary>
    public static Tex Picture(Species s)
    {
        if (_fishTex.TryGetValue(s.Name, out var t)) return t;
        var c = new Canvas(40, 20);
        uint body = Col.Rgb(s.Colour.r, s.Colour.g, s.Colour.b), belly = Col.Rgb(s.Belly.r, s.Belly.g, s.Belly.b);
        if (s.Junk)
        {
            c.Rect(14, 2, 10, 12, body); c.Rect(8, 11, 22, 6, body); c.Rect(8, 16, 24, 2, belly); // the leg, the foot, the sole
            c.Rect(15, 3, 8, 1, Col.Shade(body, 150)); c.Line(16, 6, 22, 6, 1, belly); c.Line(16, 9, 22, 9, 1, belly); // laces
        }
        else
        {
            c.Tri(30, 10, 39, 3, 39, 17, Col.Shade(body, 200));   // the tail
            c.Ellipse(17, 10, 14, 6.5f, body);
            c.Ellipse(17, 12.5f, 11, 3.5f, belly);
            c.Tri(14, 4, 22, 4, 20, 0, Col.Shade(body, 180));      // the fin
            if (s.Name == "Pike") c.Ellipse(5, 10, 5, 2.5f, body);  // (a long snout)
            if (s.Name == "Catfish") { c.Line(4, 11, 0, 15, 1, Col.Shade(body, 150)); c.Line(4, 9, 0, 6, 1, Col.Shade(body, 150)); } // whiskers
            c.Circle(8, 8.5f, 1.6f, Col.Rgb(250, 250, 250)); c.Circle(8, 8.5f, 0.8f, Col.Rgb(10, 10, 10));
        }
        c.Outline(Col.Rgb(16, 16, 18));
        return _fishTex[s.Name] = c.T;
    }

    /// <summary>The water: blue-green with ripples drifting across it, redrawn as it moves.</summary>
    public static void DrawWater(Tex t, float time)
    {
        int s = t.W;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x * MathF.Tau / s, v = y * MathF.Tau / s;
                float w = MathF.Sin(u * 2 + v + time * 1.3f) + MathF.Sin(v * 3 - u + time * 0.9f) * 0.6f + MathF.Sin((u + v) * 4 + time * 2.1f) * 0.3f;
                int k = (int)(w * 14);
                uint c = Col.Rgb(Math.Clamp(28 + k, 0, 255), Math.Clamp(92 + k * 2, 0, 255), Math.Clamp(120 + k * 2, 0, 255));
                if (w > 1.35f) c = Col.Lerp(c, Col.Rgb(200, 235, 245), 120); // a glint on a crest
                t.Px[y * s + x] = c;
            }
    }

    /// <summary>The bobber's picture: red over white, a dark band between.</summary>
    public static readonly Tex BobberTex = MakeBobber();
    static Tex MakeBobber()
    {
        var c = new Canvas(12, 16);
        c.Rect(5, 0, 2, 4, Col.Rgb(40, 40, 40));
        c.Ellipse(6, 8, 5, 5, Col.Rgb(235, 235, 230));
        c.Rect(1, 3, 10, 5, Col.Rgb(215, 40, 40));
        c.Ellipse(6, 5, 4.5f, 2.5f, Col.Rgb(215, 40, 40));
        c.Rect(1, 7, 10, 1, Col.Rgb(30, 30, 30));
        c.Outline(Col.Rgb(20, 20, 20));
        return c.T;
    }
}

/// <summary>The bobber on the end of the line. Its Z is absolute (its bottom), as a projectile's.</summary>
public sealed class Bobber : Thing
{
    public float VX, VY, VZ;
    public Bobber() { SpriteW = 0.26f; SpriteH = 0.34f; FullBright = true; Radius = 0.1f; }
    public override Tex Sprite(float time) => Fishing.BobberTex;
}

/// <summary>One fishing session: the weight landed, the fish, and the biggest.</summary>
public sealed class FishingRun
{
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public float Kg { get; set; }
    public int Fish { get; set; }
    public float Biggest { get; set; }
    public string BiggestName { get; set; } = "";
    public DateTime When { get; set; }
}

public enum FishState { Idle, Casting, Waiting, Bite, Reeling }

public sealed partial class Game
{
    public bool OnFishing => Practicing && Course.Fishing;
    public FishState Fish = FishState.Idle;
    public Bobber Bobber;
    /// <summary>The session: seconds left, weight landed, fish caught, the biggest; the fish on the line and the fight.</summary>
    public float FishingLeft = Fishing.Length, FishKg, BiggestKg;
    public int FishCount;
    public string BiggestName = "";
    public Fishing.Species Hooked;
    public float HookedKg, LineTension, LineOut, FishRunFor, FishRestFor;
    public bool FishRunning;
    /// <summary>The last catch, and how long its card has left on the screen.</summary>
    public Fishing.Species Landed;
    public float LandedKg, LandedShow;
    public FishingRun LastFishing;
    public int LastFishingPlace;
    float _biteIn, _biteLeft, _castAngle, _splashCd, _waterClock, _waterTime;
    bool _rodFireWas;
    Tex _water;

    /// <summary>On a fresh lake: the rod in hand, rippling water, a muddy bank, and a clear view across.</summary>
    void SetUpFishing()
    {
        var p = P;
        p.Loadout = new[] { Fishing.Rod };
        p.HasWeapon = new[] { true };
        p.Weapon = 0; p.PendingWeapon = -1; p.Raise = 0;
        var th = Level.Theme;
        _water = new Tex(Art.TS, Art.TS);
        Fishing.DrawWater(_water, 0);
        th.Water = _water;
        th.Riser = MapColors.Recolour(Art.StepRiser, Col.Rgb(90, 70, 45), false); // the bank
        th.FogDist = MathF.Max(th.FogDist, 40f);
        var lv = Level;
        lv.Water = new bool[lv.W * lv.H];
        for (int y = 0; y < lv.H; y++)
            for (int x = 0; x < lv.W; x++) lv.Water[y * lv.W + x] = Fishing.Water(x, y);
    }

    /// <summary>A new session: nothing landed, the line in, the clock waiting for your first cast.</summary>
    void ResetFishing()
    {
        FishingLeft = Fishing.Length; FishKg = BiggestKg = 0; FishCount = 0; BiggestName = "";
        LandedShow = 0;
        ReelIn();
        RunStarted = false;
    }

    /// <summary>The line in: no bobber out, nothing on it.</summary>
    void ReelIn()
    {
        if (Bobber != null) Bobber.Removed = true;
        Bobber = null; Hooked = null;
        Fish = FishState.Idle; LineTension = 0; FishRunning = false;
    }

    /// <summary>The rod, from your fire button: cast, reel the line back in, strike, and (held) reel a fish.</summary>
    void RodInput(Input inp, float dt)
    {
        bool press = inp.Fire && !_rodFireWas;
        _rodFireWas = inp.Fire;
        var p = P;
        if (Mode != GameMode.Playing || p.PendingWeapon >= 0 || p.Raise > 0.2f) return;
        switch (Fish)
        {
            case FishState.Idle when press && p.Cooldown <= 0:
                Cast();
                break;
            case FishState.Waiting when press:
                ReelIn();
                p.Cooldown = 0.3f;
                Say("Reeled in.");
                break;
            case FishState.Bite when press:
                Strike();
                break;
            case FishState.Reeling:
                ReelTick(inp.Fire, dt);
                break;
        }
    }

    void Cast()
    {
        var p = P;
        float a = GrenadeAim(), flat = MathF.Cos(a) * Fishing.CastSpeed;
        _castAngle = p.Angle;
        Bobber = new Bobber
        {
            X = p.X + MathF.Cos(p.Angle) * 0.3f, Y = p.Y + MathF.Sin(p.Angle) * 0.3f, Z = p.FloorZ + p.Z + 0.6f,
            VX = MathF.Cos(p.Angle) * flat, VY = MathF.Sin(p.Angle) * flat, VZ = MathF.Sin(a) * Fishing.CastSpeed + Fishing.CastUp, Level = Level,
        };
        Level.Things.Add(Bobber);
        Fish = FishState.Casting;
        p.FireAnim = 0.25f; p.Cooldown = 0.4f;
        PlaySound(Sfx.Swing, 0.8f);
        if (!RunStarted) RunStarted = true;
    }

    void Strike()
    {
        float depth = Fishing.Depth(Bobber.X, Bobber.Y);
        (Hooked, HookedKg) = Fishing.Roll(depth, RandF(), RandF());
        Fish = FishState.Reeling;
        LineTension = 0.1f;
        LineOut = Dist(P.X, P.Y, Bobber.X, Bobber.Y);
        FishRunning = true; FishRunFor = 0.6f + RandF() * 0.6f; FishRestFor = 0;
        PlaySound(Sfx.Hit, 1);
        AddShake(0.15f);
        Say(Hooked.Junk ? "Hooked something! It's not fighting much..." : "Hooked! Hold fire to reel - ease off while it runs!");
    }

    /// <summary>
    /// The fight, each frame: the fish runs for a moment, then rests. Reeling brings it in; reeling while it runs strains
    /// the line hard, and it pulls out line while it runs; letting go eases the strain.
    /// </summary>
    void ReelTick(bool reeling, float dt)
    {
        var s = Hooked;
        float strength = s.Strength * (0.8f + 0.25f * HookedKg / MathF.Max(1, s.MaxKg));
        if (FishRunning)
        {
            if ((FishRunFor -= dt) <= 0) { FishRunning = false; FishRestFor = 0.8f + RandF() * 1.4f / strength; }
            LineOut += strength * 0.8f * dt;
        }
        else if ((FishRestFor -= dt) <= 0) { FishRunning = true; FishRunFor = 0.4f + RandF() * 0.5f * strength; }
        if (reeling)
        {
            LineOut -= Fishing.ReelSpeed * dt;
            LineTension += (FishRunning ? Fishing.RunStrain * strength : Fishing.RestStrain) * dt;
            if ((_splashCd -= dt) <= 0) { _splashCd = 0.35f; Sound(Sfx.Slide, Bobber.X, Bobber.Y); }
        }
        else LineTension = MathF.Max(0, LineTension - Fishing.Ease * dt);
        // the bobber follows the fish: along the line from you, twitching sideways while it runs
        var p = P;
        float side = FishRunning ? MathF.Sin(PlayTime * 13) * 0.35f : 0;
        float bx = p.X + MathF.Cos(_castAngle) * LineOut - MathF.Sin(_castAngle) * side, by = p.Y + MathF.Sin(_castAngle) * LineOut + MathF.Cos(_castAngle) * side;
        if (Level.Water?[(int)by * Level.W + (int)bx] == true || LineOut < 1.5f) { Bobber.X = bx; Bobber.Y = by; }
        Bobber.Z = Level.FloorAt(Bobber.X, Bobber.Y) + (FishRunning ? -0.08f : 0.02f);
        if (FishRunning && RandF() < dt * 8) SpawnPuff(Art.Smoke, Bobber.X, Bobber.Y, Bobber.Z + 0.1f, 0.25f);
        if (LineTension >= 1f)
        {
            PlaySound(Sfx.Break, 1);
            Say($"SNAP! The line broke - {(s.Junk ? "whatever it was" : "the fish")} got away.");
            ReelIn();
            P.Cooldown = 0.6f;
            return;
        }
        if (LineOut > Fishing.Escape)
        {
            Say("It ran out all your line and got away.");
            ReelIn();
            return;
        }
        if (LineOut <= 1f) Land();
    }

    /// <summary>A fish on the dock: its weight to the session, its card on the screen.</summary>
    void Land()
    {
        var s = Hooked;
        Landed = s; LandedKg = HookedKg; LandedShow = 3f;
        if (!s.Junk)
        {
            FishCount++;
            FishKg += HookedKg;
            if (HookedKg > BiggestKg) { BiggestKg = HookedKg; BiggestName = s.Name; }
            PlaySound(s.Name == "Golden Carp" || HookedKg >= 5 ? Sfx.Secret : Sfx.Pickup, 1);
            Say(s.Name == "Golden Carp" ? $"A GOLDEN CARP! {HookedKg:0.00} kg!" : $"A {s.Name}, {HookedKg:0.00} kg!");
        }
        else
        {
            PlaySound(Sfx.Locked, 0.7f);
            Say("An old boot. Back it goes.");
        }
        ReelIn();
        P.Cooldown = 0.5f;
    }

    /// <summary>Each frame at the lake: the water moves, the bobber flies and floats, fish bite, you fall in, and the clock.</summary>
    void FishingTick(float dt)
    {
        if (!OnFishing) return;
        LandedShow = MathF.Max(0, LandedShow - dt);
        _waterTime += dt;
        if ((_waterClock += dt) >= 0.1f && _water != null) { _waterClock = 0; Fishing.DrawWater(_water, _waterTime); }
        var p = P;
        // in the lake: splash, and back on the dock
        int cell = (int)p.Y * Level.W + (int)p.X;
        if (Mode == GameMode.Playing && Level.Water?[cell] == true && p.OnGround && p.FloorZ < 0.5f)
        {
            SpawnPuff(Art.Smoke, p.X, p.Y, 0.3f, 0.9f);
            PlaySound(Sfx.Slide, 1);
            ReelIn();
            MoveTo(Fishing.StartX, Fishing.StartY, Course.StartAngle);
            Say("Splash! You fell in. Back on the dock.");
        }
        var b = Bobber;
        switch (Fish)
        {
            case FishState.Casting when b != null:
                b.VZ -= Fishing.Gravity * dt;
                b.X += b.VX * dt; b.Y += b.VY * dt; b.Z += b.VZ * dt;
                if (Level.BlocksPoint(b.X, b.Y)) { b.X -= b.VX * dt; b.Y -= b.VY * dt; b.VX = b.VY = 0; }
                float ground = Level.FloorAt(b.X, b.Y);
                if (b.Z <= ground)
                {
                    b.Z = ground;
                    if (Level.Water?[(int)b.Y * Level.W + (int)b.X] == true)
                    {
                        Fish = FishState.Waiting;
                        _biteIn = 2f + RandF() * 4f + Fishing.Depth(b.X, b.Y) * 0.25f;
                        SpawnPuff(Art.Smoke, b.X, b.Y, b.Z + 0.1f, 0.5f);
                        Sound(Sfx.Slide, b.X, b.Y);
                    }
                    else { Say("That landed on the bank. Cast again, out over the water."); ReelIn(); }
                }
                break;
            case FishState.Waiting when b != null:
                b.Z = Level.FloorAt(b.X, b.Y) + 0.02f + MathF.Sin(PlayTime * 2.5f) * 0.02f;
                if ((_biteIn -= dt) <= 0)
                {
                    Fish = FishState.Bite; _biteLeft = Fishing.StrikeWindow;
                    SpawnPuff(Art.Smoke, b.X, b.Y, b.Z + 0.1f, 0.6f);
                    PlaySound(Sfx.Sight, 0.9f);
                    Say("BITE! Strike!");
                }
                break;
            case FishState.Bite when b != null:
                b.Z = Level.FloorAt(b.X, b.Y) - 0.12f + MathF.Sin(PlayTime * 30) * 0.05f; // tugged under
                if ((_biteLeft -= dt) <= 0)
                {
                    Fish = FishState.Waiting; _biteIn = 1.5f + RandF() * 4f;
                    Say("Too slow - it let go. Wait for the next bite.");
                }
                break;
        }
        if (!RunStarted || Mode != GameMode.Playing) return;
        FishingLeft -= dt;
        if (FishingLeft <= 0) EndFishing();
    }

    /// <summary>Time's up: the session on your board (if you landed anything), and a fresh one.</summary>
    void EndFishing()
    {
        Messages.Clear();
        if (Demo || PracticeSpeed < 1 || FishCount == 0)
            Say(FishCount == 0 ? "Time's up: nothing landed. Strike the moment it bites, and ease off while it runs." : $"Time's up: {FishKg:0.00} kg at {PracticeSpeed * 100:0}% speed.");
        else
        {
            var run = new FishingRun { Name = RunnerName, Class = P.Class.ToString(), Kg = MathF.Round(FishKg * 100) / 100, Fish = FishCount, Biggest = BiggestKg, BiggestName = BiggestName, When = DateTime.Now };
            float best = Profile.FishingBest(P.Class);
            int place = Profile.AddFishingRun(run);
            if (place > 0) SaveProfile();
            LastFishing = run; LastFishingPlace = place;
            PlaySound(place == 1 ? Sfx.Secret : Sfx.Teleport, 1);
            string how = $"Time's up: {run.Kg:0.00} kg in {FishCount} fish, the biggest a {BiggestName} of {BiggestKg:0.00} kg";
            Say(place == 1 ? $"{how} - a new best!" : place > 0 ? $"{how} - #{place} on the board (best {best:0.00} kg)." : $"{how} (best {best:0.00} kg).");
        }
        ResetRun();
    }
}

/// <summary>
/// The fishing demo: from the dock it casts straight out, looking up a little; it strikes the moment a fish bites,
/// then reels, letting go whenever the fish runs or the line's tension gets high.
/// </summary>
static class FishingPilot
{
    public static Input Next(Game g, DemoPilot pilot)
    {
        var p = g.P;
        float sens = 0.0025f * g.Vars.Sens;
        var inp = new Input
        {
            LookX = MathF.IEEERemainder(-MathF.PI / 2 + 0.25f * MathF.Sin(g.FishCount * 1.7f) - p.Angle, MathF.Tau) / sens,
            LookY = (p.Pitch - 22f) / (0.35f * g.Vars.Sens),
        };
        pilot.FishTick = !pilot.FishTick;
        switch (g.Fish)
        {
            case FishState.Idle:
                pilot.Say("CAST: face out over the water, look up a little, and fire");
                inp.Fire = pilot.FishTick && p.Cooldown <= 0;
                break;
            case FishState.Casting:
            case FishState.Waiting:
                pilot.Say("WAIT: watch the bobber for a bite");
                break;
            case FishState.Bite:
                pilot.Say("STRIKE: it's bitten - fire now!");
                inp.Fire = pilot.FishTick;
                break;
            case FishState.Reeling:
                bool ease = g.FishRunning || g.LineTension > 0.7f;
                pilot.Say(ease ? "EASE OFF: it's running - let go of fire" : "REEL: hold fire while it rests");
                inp.Fire = !ease;
                break;
        }
        return inp;
    }
}
