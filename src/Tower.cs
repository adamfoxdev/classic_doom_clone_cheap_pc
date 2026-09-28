namespace HexenSharp;

/// <summary>
/// The endless rocket tower (Practice > Rocket Tower): a climb from a seed, with only the rocket launcher. The tower is
/// a stack of shafts, each rising about eight cells by platforms spiralling up its walls; reach a shaft's top platform
/// and you're carried to the foot of the next one, the climb carrying on. The rises and gaps grow as you go, and the
/// platforms shrink. One fall to a shaft's floor ends the run; your score is the height you reached. Each pick from
/// the Practice menu rolls a new seed; 'tower &lt;seed&gt;' in the console replays one.
/// </summary>
public static class Tower
{
    public const int Shafts = 12, Room = 12, H = Room + 2;
    public const float Base = 0.5f;

    public const string About = "AN ENDLESS CLIMB FROM A SEED, ROCKET JUMP BY ROCKET JUMP. ONE FALL ENDS THE RUN: HOW HIGH CAN YOU GET?";
    const string Intro = "Rocket Tower: rocket jump up the platforms. Reach a shaft's top and you're carried on up. One fall ends the run.";

    /// <summary>The Practice menu's entry: picking it rolls a fresh seed.</summary>
    public static readonly Course Pick = new("tower", "Rocket Tower", About, () => Map(1), false, Intro, StartAngle: 0, Tower: true);

    public static Course For(int seed) => Pick with { Map = () => Map(seed), Seed = seed };

    /// <summary>A platform: its shaft, its corner cell (in the shaft's own 12 by 12), its size and its floor.</summary>
    public readonly record struct Plat(int Shaft, int X, int Y, int Size, float Floor);

    /// <summary>
    /// The platforms for a seed, shaft by shaft (the first of each is its base, where you arrive). They spiral round the
    /// shaft: each is the next corner or side along, `gap` cells from the last, `rise` higher. Early shafts rise two
    /// cells a platform over gaps of two; later ones rise up to 2.6 over gaps up to four, on smaller platforms.
    /// </summary>
    public static Plat[] Plan(int seed)
    {
        var rng = new Random(seed);
        var plats = new List<Plat>();
        // spots round the shaft, anticlockwise from the base corner (x, y of the corner cell for a 3-wide platform)
        (int x, int y)[] ring = { (1, 8), (8, 8), (8, 1), (1, 1) };
        for (int s = 0; s < Shafts; s++)
        {
            float t = s / (float)(Shafts - 1);
            float floor = Base;
            int start = rng.Next(4);
            plats.Add(new Plat(s, ring[start].x, ring[start].y, 3, Base));
            for (int k = 1; k <= 3; k++) // three rises round the ring (a fourth would land back on the base)
            {
                float rise = MathF.Round((1.8f + 0.8f * t + (float)rng.NextDouble() * 0.2f) * 4) / 4; // in floor steps
                if (floor + rise > 8.5f) break;
                floor += rise;
                int size = t < 0.4f ? 3 : 2;
                var (x, y) = ring[(start + k) % 4];
                // early, the platforms sit in from the corners (gaps of two); later out in them (four), and smaller (five)
                int inset = t < 0.5f ? 1 : 0;
                x += x > 4 ? -inset : inset; y += y > 4 ? -inset : inset;
                if (size == 2) { x += x > 4 ? 1 : 0; y += y > 4 ? 1 : 0; } // (a smaller one keeps to the outside)
                plats.Add(new Plat(s, x, y, size, floor));
            }
        }
        return plats.ToArray();
    }

    /// <summary>The cell a shaft's local (x, y) is at in the map.</summary>
    public static (int x, int y) Cell(int shaft, int x, int y) => (1 + shaft * (Room + 1) + x, 1 + y);

    public static MapDef Map(int seed)
    {
        var plan = Plan(seed);
        int w = Shafts * (Room + 1) + 1;
        var rows = new char[H][];
        for (int y = 0; y < H; y++)
        {
            rows[y] = new char[w];
            for (int x = 0; x < w; x++)
                rows[y][x] = y == 0 || y == H - 1 || x % (Room + 1) == 0 ? 'O' : '='; // walls between the shafts; lift pads the floor
        }
        foreach (var pl in plan)
        {
            var (cx, cy) = Cell(pl.Shaft, pl.X, pl.Y);
            for (int dy = 0; dy < pl.Size; dy++)
                for (int dx = 0; dx < pl.Size; dx++) rows[cy + dy][cx + dx] = '.';
            rows[cy][cx] = '+';
        }
        var (sx, sy) = Cell(0, plan[0].X + 1, plan[0].Y + 1);
        rows[sy][sx] = '@';
        var def = new MapDef($"Rocket Tower {seed}", $"The Rocket Tower, seed {seed}. How high can you get?", "spire", rows.Select(r => new string(r)).ToArray(), Height: 10f);
        return Maps.Elevate(def, plan.Select(pl =>
        {
            var (cx, cy) = Cell(pl.Shaft, pl.X, pl.Y);
            return (cx, cy, cx + pl.Size - 1, cy + pl.Size - 1, Maps.FloorGlyph(pl.Floor));
        }).ToArray());
    }

    /// <summary>The height a platform stands at in the whole climb: the shafts below it, and its rise in its own.</summary>
    public static float Height(Plat[] plan, int index)
    {
        float h = 0;
        for (int s = 0; s < plan[index].Shaft; s++) h += plan.Where(p => p.Shaft == s).Max(p => p.Floor) - Base;
        return h + plan[index].Floor - Base;
    }
}

/// <summary>One tower run: the height it reached, on which seed, and how long it took.</summary>
public sealed class TowerRun
{
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public float Height { get; set; }
    public int Seed { get; set; }
    public float Time { get; set; }
    public DateTime When { get; set; }
}

public sealed partial class Game
{
    public bool OnTower => Practicing && Course.Tower;
    /// <summary>On the tower: the highest this run has climbed, and the platform it's on (by the plan's order).</summary>
    public float TowerHeight;
    public int TowerPlatform;
    public TowerRun LastTower;
    public int LastTowerPlace;
    Tower.Plat[] _towerPlan;
    float _towerCarry = -1;

    public void StartTower(PClass cls, int seed = 0) => StartPractice(cls, Tower.For(seed > 0 ? seed : Endless.NewSeed()));

    /// <summary>On a fresh tower: the rocket launcher alone, as on the rocket course.</summary>
    void SetUpTower()
    {
        _towerPlan = Tower.Plan(Course.Seed);
        SetUpRocketCourse();
        TowerHeight = 0; TowerPlatform = 0; _towerCarry = -1;
    }

    /// <summary>Which platform of the plan a checkpoint pad is on.</summary>
    int TowerPlatformAt(int pad)
    {
        int x = pad % Level.W, y = pad / Level.W;
        for (int i = 0; i < _towerPlan.Length; i++)
        {
            var pl = _towerPlan[i];
            var (cx, cy) = Tower.Cell(pl.Shaft, pl.X, pl.Y);
            if (x >= cx && x < cx + pl.Size && y >= cy && y < cy + pl.Size) return i;
        }
        return -1;
    }

    /// <summary>Landed on a new platform: the height it counts for, and at a shaft's top, the carry on up.</summary>
    void TowerLanded(int zone)
    {
        int i = TowerPlatformAt(Level.Checkpoints[zone]);
        if (i < 0) return;
        TowerPlatform = i;
        if (i > 0) RunStarted = true;
        TowerHeight = MathF.Max(TowerHeight, Tower.Height(_towerPlan, i));
        bool top = i + 1 >= _towerPlan.Length || _towerPlan[i + 1].Shaft != _towerPlan[i].Shaft;
        if (!top) return;
        if (i + 1 >= _towerPlan.Length) { EndTowerRun(true); return; }
        _towerCarry = 0.5f; // a moment to take in the top, then on up
        Say($"The top of shaft {_towerPlan[i].Shaft + 1}! On up...");
    }

    string TowerHint(int zone)
    {
        int i = TowerPlatformAt(Level.Checkpoints[zone]);
        if (i <= 0 || i + 1 >= _towerPlan.Length) return Course.Intro;
        var (a, b) = (_towerPlan[i], _towerPlan[i + 1]);
        if (a.Shaft != b.Shaft) return $"{TowerHeight:0.0} cells up.";
        return $"{TowerHeight:0.0} cells up. The next is {b.Floor - a.Floor:0.##} higher.";
    }

    /// <summary>Each frame on the tower: the carry to the next shaft's foot.</summary>
    void TowerTick(float dt)
    {
        if (!OnTower || _towerCarry < 0) return;
        if ((_towerCarry -= dt) > 0) return;
        _towerCarry = -1;
        int next = TowerPlatform + 1;
        var pl = _towerPlan[next];
        var (cx, cy) = Tower.Cell(pl.Shaft, pl.X + 1, pl.Y + 1);
        MoveTo(cx + 0.5f, cy + 0.5f, P.Angle);
        TowerPlatform = next;
        PlaySound(Sfx.Teleport, 1);
    }

    /// <summary>The run's over (a fall, or the very top): on the board if it climbed at all, and back to the foot.</summary>
    void EndTowerRun(bool finished)
    {
        Messages.Clear();
        float height = TowerHeight;
        if (Demo || PracticeSpeed < 1 || height <= 0)
        {
            Say(height <= 0 ? "Fell at the first platform. Face away from it, look right down, jump and fire." : $"Reached {height:0.0} cells at {PracticeSpeed * 100:0}% speed.");
        }
        else
        {
            var run = new TowerRun { Name = RunnerName, Class = P.Class.ToString(), Height = height, Seed = Course.Seed, Time = RunTime, When = DateTime.Now };
            float best = Profile.TowerBest(P.Class);
            int place = Profile.AddTowerRun(run);
            if (place > 0) SaveProfile();
            LastTower = run; LastTowerPlace = place;
            PlaySound(place == 1 ? Sfx.Secret : Sfx.Teleport, 1);
            string how = finished ? $"The very top! {height:0.0} cells" : $"Fell from {height:0.0} cells";
            Say(place == 1 ? $"{how} - a new best!" : place > 0 ? $"{how} - #{place} on the board (best {best:0.0})." : $"{how} (best {best:0.0}).");
        }
        Level.CheckpointsReached.Clear();
        Checkpoint = null;
        MoveTo(Level.StartX, Level.StartY, Course.StartAngle);
        ResetRun();
        TowerHeight = 0; TowerPlatform = 0; _towerCarry = -1;
    }
}

/// <summary>
/// A rocket-jumping pilot for any climb of platforms (the tower's demo): from the platform it's on to the next, it backs
/// up to the edge facing away from it, looking right down, jumps and fires at its feet at the edge, then in the air
/// holds back toward the next platform's middle and brakes over it.
/// </summary>
static class TowerPilot
{
    public static Input Next(Game g, DemoPilot pilot)
    {
        var p = g.P;
        var plan = Tower.Plan(g.Course.Seed);
        int i = Math.Clamp(g.TowerPlatform, 0, plan.Length - 1);
        if (i + 1 >= plan.Length || plan[i + 1].Shaft != plan[i].Shaft) { pilot.Say("WAIT: on up to the next shaft"); return new Input(); }
        var (a, b) = (plan[i], plan[i + 1]);
        var (ax, ay) = Tower.Cell(a.Shaft, a.X, a.Y);
        var (bx, by) = Tower.Cell(b.Shaft, b.X, b.Y);
        float acx = ax + a.Size / 2f, acy = ay + a.Size / 2f, bcx = bx + b.Size / 2f, bcy = by + b.Size / 2f;
        float dx = bcx - acx, dy = bcy - acy, len = MathF.Sqrt(dx * dx + dy * dy);
        dx /= len; dy /= len;
        float sens = 0.0025f * g.Vars.Sens;
        var inp = new Input
        {
            LookX = MathF.IEEERemainder(MathF.Atan2(-dy, -dx) - p.Angle, MathF.Tau) / sens, // face away from the next one
            LookY = (Rockets.LookDown + p.Pitch) / (0.35f * g.Vars.Sens) + 1,
        };
        // in the air: on toward the next platform's middle, then brake over it
        float along = (p.X - acx) * dx + (p.Y - acy) * dy, toGo = (bcx - p.X) * dx + (bcy - p.Y) * dy;
        float side = -(p.X - acx) * dy + (p.Y - acy) * dx; // off the line between them
        if (!p.OnGround)
        {
            pilot.Say(toGo > 0.3f ? "FLY: hold S to carry on" : "BRAKE: over it, hold W");
            inp.Move = toGo > 0.3f ? -1 : 1;
            inp.Strafe = MathF.Abs(side) > 0.2f ? MathF.Sign(side) : 0; // facing away, strafing right moves you left of the line
            return inp;
        }
        // on the platform: back up to its edge toward the next, then jump and fire
        float edge = MathF.Min(a.Size / 2f - 0.3f, MathF.Abs(dx) > MathF.Abs(dy) ? a.Size / 2f / MathF.Abs(dx) - 0.3f : a.Size / 2f / MathF.Abs(dy) - 0.3f);
        inp.Strafe = MathF.Abs(side) > 0.15f ? MathF.Sign(side) : 0;
        if (along < edge) { pilot.Say("BACK UP: face away from the next platform, look right down, back up to the edge"); inp.Move = -1; return inp; }
        pilot.Say("JUMP AND FIRE: at the edge");
        inp.Move = -1; inp.Jump = true; inp.Fire = p.Cooldown <= 0 && p.Pitch <= -Rockets.LookDown + 1;
        return inp;
    }
}

public sealed partial class Game
{
    /// <summary>The arena with its instagib modifier: a railgun, and one slug kills.</summary>
    public bool InstagibOn => ArenaMode && (ArenaMods & ArenaMod.Instagib) != 0;
    /// <summary>The arena as Rocket Arena: every Quake weapon, full ammo, and no self-damage.</summary>
    public bool RocketArenaOn => ArenaMode && !InstagibOn && (ArenaMods & ArenaMod.RocketArena) != 0;
}
