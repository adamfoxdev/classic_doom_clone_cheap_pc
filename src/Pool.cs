namespace HexenSharp;

/// <summary>
/// Rocket Pool (Practice > Rocket Pool): you're on a giant pool table with ten balls racked in a triangle, and the
/// rocket launcher (and grenade launcher) for a cue. Blast the balls into the six pockets: sunken holes in the corners
/// and halfway along the long sides. The black 8 goes last; pot it early and it comes back to the foot spot, with ten
/// seconds on your time. The clock starts at your first touch and stops when the table's clear; your times go on a
/// board for each class, and the table racks again. The balls knock into each other, bounce off the cushions and drop
/// into the pockets; you can step down into a pocket and back up out of it.
/// </summary>
public static class Pool
{
    /// <summary>The table: the room's size, the cushions' inner line, the table's height, and the balls' radius.</summary>
    public const int W = 44, H = 24, MidX = 21;
    public const float Table = 0.75f, Radius = 0.45f, Penalty = 10f;
    /// <summary>The balls: slower to stop than the soccer ball, livelier off the cushions, and as hard to shove.</summary>
    public const float Roll = 0.9f, Cushion = 0.8f, Knock = 0.09f, Restitution = 0.95f;
    /// <summary>The foot spot (the rack's apex, and where an early 8 comes back) and your start.</summary>
    public const float FootX = 30f, FootY = 12f, StartX = 10.5f, StartY = 12f;

    public const string About = "A GIANT POOL TABLE: BLAST TEN BALLS INTO THE POCKETS, THE 8 LAST. HOW FAST CAN YOU CLEAR IT?";
    const string Intro = "Rocket Pool: blast the balls into the pockets. The black 8 goes last: pot it early and it's back on the spot, +10s.";

    public static readonly Course Course = new("pool", "Rocket Pool", About, Map, false, Intro, StartAngle: 0, Pool: true);

    /// <summary>
    /// The six pockets: the cells of each (a corner's five, cut two cells along each cushion; a side's three), and the
    /// point to aim a ball at. They're a little over three balls wide, so a ball needn't be dead on to drop.
    /// </summary>
    public static readonly (int x, int y)[][] PocketCells =
    {
        Corner(1, 1, 1, 1), Corner(W - 2, 1, -1, 1), Corner(1, H - 2, 1, -1), Corner(W - 2, H - 2, -1, -1),
        new[] { (MidX - 1, 1), (MidX, 1), (MidX + 1, 1) }, new[] { (MidX - 1, H - 2), (MidX, H - 2), (MidX + 1, H - 2) },
    };
    static (int, int)[] Corner(int x, int y, int dx, int dy) => new[] { (x, y), (x + dx, y), (x + 2 * dx, y), (x, y + dy), (x, y + 2 * dy) };
    public static readonly (float x, float y)[] PocketAim =
    {
        (1.6f, 1.6f), (W - 1.6f, 1.6f), (1.6f, H - 1.6f), (W - 1.6f, H - 1.6f), (MidX + 0.5f, 1.2f), (MidX + 0.5f, H - 1.2f),
    };

    public static bool IsPocket(int x, int y) => PocketCells.Any(p => p.Contains((x, y)));

    public static MapDef Map()
    {
        var rows = new char[H][];
        var floors = new char[H][];
        for (int y = 0; y < H; y++)
        {
            rows[y] = new char[W];
            floors[y] = new char[W];
            for (int x = 0; x < W; x++)
            {
                bool border = x == 0 || y == 0 || x == W - 1 || y == H - 1;
                bool cushion = !border && (x == 1 || y == 1 || x == W - 2 || y == H - 2);
                bool pocket = IsPocket(x, y);
                // the pockets are 'outdoor' cells only so they can have a floor (and an overhead) of their own: black
                rows[y][x] = border ? '#' : pocket ? ',' : cushion ? 'W' : '.';
                floors[y][x] = pocket || border || cushion ? '.' : Maps.FloorGlyph(Table);
            }
        }
        rows[(int)StartY][(int)StartX] = '@';
        return new MapDef("Rocket Pool", "Rocket Pool. Blast the balls into the pockets, the black 8 last.", "hall",
            rows.Select(r => new string(r)).ToArray(), Height: 4f) { Floors = floors.Select(r => new string(r)).ToArray() };
    }

    /// <summary>The balls in the rack, by number: 1 to 7 solids, the black 8, 9 and 10 striped.</summary>
    public static readonly (int r, int g, int b)[] Colours =
    {
        (0, 0, 0), (235, 190, 30), (40, 70, 205), (205, 40, 40), (115, 45, 160), (235, 120, 30), (30, 135, 65), (125, 30, 35),
        (22, 22, 26), (235, 190, 30), (40, 70, 205),
    };
    public static bool Striped(int n) => n >= 9;

    /// <summary>The rack: rows of 1, 2, 3 and 4 from the apex on the foot spot, the 8 in the middle of the third row.</summary>
    public static (int n, float x, float y)[] Rack()
    {
        int[] order = { 1, 9, 2, 3, 8, 10, 4, 5, 6, 7 };
        var spots = new List<(int, float, float)>();
        int i = 0;
        for (int row = 0; row < 4; row++)
            for (int k = 0; k <= row; k++)
            {
                float x = FootX + row * Radius * 2 * 0.88f, y = FootY + (k - row / 2f) * Radius * 2.04f;
                spots.Add((order[i++], x, y));
            }
        return spots.ToArray();
    }

    static readonly Dictionary<int, Tex[]> _frames = new();
    public static Tex[] Frames(int n)
    {
        if (!_frames.TryGetValue(n, out var f)) _frames[n] = f = Enumerable.Range(0, 8).Select(k => Frame(n, k * MathF.Tau / 8)).ToArray();
        return f;
    }

    /// <summary>A pool ball: its colour (or white with a band of it, striped), a white spot for its number, shaded and shining.</summary>
    static Tex Frame(int n, float rot)
    {
        const int S = 32;
        float R = S * 0.48f;
        var t = new Tex(S, S);
        var c = Colours[n];
        uint colour = Col.Rgb(c.r, c.g, c.b), white = Col.Rgb(240, 238, 230);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float nx = (x + 0.5f - S / 2f) / R, ny = (y + 0.5f - S / 2f) / R, q = nx * nx + ny * ny;
                if (q > 1) continue;
                float nz = MathF.Sqrt(1 - q);
                float rx = nx * MathF.Cos(rot) + nz * MathF.Sin(rot), rz = -nx * MathF.Sin(rot) + nz * MathF.Cos(rot);
                uint px = Striped(n) && MathF.Abs(ny) > 0.42f ? white : colour;
                // the number's spot: a white disc on one side, turning with the ball
                float sy = ny + 0.05f, sz = rz - 0.9f;
                if (rx * rx + sy * sy + sz * sz < 0.16f && rz > 0) px = white;
                float light = Math.Clamp(0.4f + 0.7f * (-0.45f * nx - 0.55f * ny + 0.7f * nz), 0.25f, 1.1f);
                px = Col.Shade(px, (int)(light * 256));
                // a shine, top left
                float hx = nx + 0.35f, hy = ny + 0.4f;
                if (hx * hx + hy * hy < 0.03f) px = Col.Lerp(px, Col.Rgb(255, 255, 255), 200);
                t.Px[y * S + x] = px;
            }
        new Canvas(t).Outline(Col.Rgb(12, 12, 14));
        return t;
    }
}

/// <summary>One cleared table: the time it took (with any penalties), the shots, the fouls, and whose it was.</summary>
public sealed class PoolRun
{
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public float Time { get; set; }
    public int Shots { get; set; }
    public int Fouls { get; set; }
    public DateTime When { get; set; }
}

public sealed partial class Game
{
    public bool OnPool => Practicing && Course.Pool;
    public readonly List<SoccerBall> PoolBalls = new();
    /// <summary>The rack in play: balls potted, shots fired, fouls (an early 8), and the time so far (penalties in).</summary>
    public int PoolPotted, PoolShots, PoolFouls;
    public float PoolTime;
    public PoolRun LastPool;
    public int LastPoolPlace;
    float _rerack = -1, _eightBack = -1;

    /// <summary>On a fresh table: the launchers, green felt, black pockets and a clear view of the whole table.</summary>
    void SetUpPool()
    {
        var p = P;
        p.Loadout = new[] { Rockets.Launcher, Grenades.Launcher };
        p.HasWeapon = new[] { true, true };
        p.Weapon = 0; p.PendingWeapon = -1; p.Raise = 0;
        p.BlueMana = p.GreenMana = 200;
        _unhurt = 0; _regen = 0;
        var th = Level.Theme;
        th.FloorIn = MapColors.Recolour(th.FloorIn, Col.Rgb(30, 118, 58), false); // the felt
        var black = new Tex(Art.TS, Art.TS);
        Array.Fill(black.Px, Col.Rgb(6, 6, 8));
        th.FloorOut = black; // the pockets' floors
        th.Riser = black;    // the table's edge, down into the pockets
        th.Sky = new Tex(256, 128);
        Array.Fill(th.Sky.Px, Col.Rgb(4, 4, 6));
        th.FogDist = MathF.Max(th.FogDist, 44f);
        th.Light = 256;
    }

    /// <summary>A fresh rack: the ten balls in their triangle, nothing potted, the clock waiting for your first touch.</summary>
    void ResetPool()
    {
        foreach (var b in PoolBalls) b.Removed = true;
        Level.Things.RemoveAll(t => t is SoccerBall);
        PoolBalls.Clear();
        foreach (var (n, x, y) in Pool.Rack()) PoolBalls.Add(PoolBall(n, x, y));
        PoolPotted = PoolShots = PoolFouls = 0; PoolTime = 0; _rerack = -1; _eightBack = -1;
        RunStarted = false;
    }

    SoccerBall PoolBall(int n, float x, float y)
    {
        var b = new SoccerBall(Pool.Radius)
        {
            Number = n, Frames = Pool.Frames(n), Roll = Pool.Roll, WallBounce = Pool.Cushion, Knock = Pool.Knock,
            X = x, Y = y, Z = Pool.Table, Grounded = true, Level = Level,
        };
        Level.Things.Add(b);
        return b;
    }

    /// <summary>Each frame on the table: balls knocking into each other, balls dropping into pockets, the clock, and the re-rack.</summary>
    void PoolTick(float dt)
    {
        if (!OnPool) return;
        if (_rerack >= 0)
        {
            if ((_rerack -= dt) < 0) { ResetPool(); PlaySound(Sfx.Teleport, 0.8f); Say("Racked again. Break!"); }
            return;
        }
        BallCollisions();
        foreach (var b in PoolBalls.ToList())
            if (!b.Removed && Pool.IsPocket((int)MathF.Floor(b.X), (int)MathF.Floor(b.Y))) Potted(b);
        if (_eightBack >= 0 && (_eightBack -= dt) < 0) EightBack();
        if (RunStarted && Mode == GameMode.Playing) PoolTime += dt;
    }

    /// <summary>Balls that meet bounce off each other (nearly all their speed along the line between them passing across).</summary>
    void BallCollisions()
    {
        var live = PoolBalls.Where(b => !b.Removed).ToList();
        for (int i = 0; i < live.Count; i++)
            for (int j = i + 1; j < live.Count; j++)
            {
                var (a, b) = (live[i], live[j]);
                float dx = b.X - a.X, dy = b.Y - a.Y, reach = a.Radius + b.Radius;
                if (MathF.Abs(dx) >= reach || MathF.Abs(dy) >= reach || MathF.Abs(a.Z - b.Z) >= reach) continue;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d >= reach) continue;
                if (d < 0.001f) { dx = 1; dy = 0; d = 1; }
                float nx = dx / d, ny = dy / d;
                float closing = (a.VX - b.VX) * nx + (a.VY - b.VY) * ny;
                if (closing > 0)
                {
                    float j2 = closing * (1 + Pool.Restitution) / 2;
                    a.VX -= j2 * nx; a.VY -= j2 * ny;
                    b.VX += j2 * nx; b.VY += j2 * ny;
                    if (closing > 1f) Sound(Sfx.Hit, a.X, a.Y);
                }
                // apart: each moves half the overlap, if it can
                float push = (reach - d) / 2 + 0.001f;
                if (!Level.BlocksCircle(a.X - nx * push, a.Y - ny * push, a.Radius)) { a.X -= nx * push; a.Y -= ny * push; }
                if (!Level.BlocksCircle(b.X + nx * push, b.Y + ny * push, b.Radius)) { b.X += nx * push; b.Y += ny * push; }
            }
    }

    /// <summary>A ball down a pocket: one fewer on the table; the 8 before the rest is a foul, and it comes back.</summary>
    void Potted(SoccerBall b)
    {
        b.Removed = true;
        SpawnPuff(Art.Smoke, b.X, b.Y, b.Z + 0.3f, 0.6f);
        int left = PoolBalls.Count(o => !o.Removed);
        if (b.Number == 8 && left > 0)
        {
            PoolFouls++;
            PoolTime += Pool.Penalty;
            _eightBack = 1f;
            PlaySound(Sfx.Locked, 0.9f);
            Say($"Foul! The 8 goes last. It's back on the spot, +{Pool.Penalty:0}s.");
            return;
        }
        PoolPotted++;
        PlaySound(Sfx.Pickup, 1);
        if (left == 0) { EndPoolRack(); return; }
        bool eightLeft = left == 1 && PoolBalls.Any(o => !o.Removed && o.Number == 8);
        Say(eightLeft ? $"The {b.Number} ball! Now the black 8." : $"The {b.Number} ball! {left} left.");
    }

    /// <summary>The 8, back on the foot spot (or the nearest clear place along the table from it).</summary>
    void EightBack()
    {
        _eightBack = -1;
        float x = Pool.FootX, y = Pool.FootY;
        for (int k = 0; k < 40 && PoolBalls.Any(o => !o.Removed && Dist(o.X, o.Y, x, y) < Pool.Radius * 2.1f); k++) x += 0.25f;
        PoolBalls.Add(PoolBall(8, x, y));
    }

    /// <summary>The table's clear: on the board, and a new rack in a moment.</summary>
    void EndPoolRack()
    {
        Messages.Clear();
        float time = PoolTime;
        if (Demo || PracticeSpeed < 1)
            Say($"Table cleared in {time:0.0}s{(Demo ? "" : $" at {PracticeSpeed * 100:0}% speed")}.");
        else
        {
            var run = new PoolRun { Name = RunnerName, Class = P.Class.ToString(), Time = time, Shots = PoolShots, Fouls = PoolFouls, When = DateTime.Now };
            float best = Profile.PoolBest(P.Class);
            int place = Profile.AddPoolRun(run);
            if (place > 0) SaveProfile();
            LastPool = run; LastPoolPlace = place;
            PlaySound(place == 1 ? Sfx.Secret : Sfx.Teleport, 1);
            string how = $"Table cleared in {time:0.0}s, {PoolShots} shots{(PoolFouls > 0 ? $", {PoolFouls} foul{(PoolFouls == 1 ? "" : "s")}" : "")}";
            Say(place == 1 ? $"{how} - a new best!" : place > 0 ? $"{how} - #{place} on the board (best {best:0.0}s)." : $"{how} (best {best:0.0}s).");
        }
        RunStarted = false;
        _rerack = 3f;
    }
}

/// <summary>
/// The pool demo: it picks the ball nearest a pocket (the 8 only when it's the last), gets behind it on the line from
/// that pocket, and blasts it in; it sticks with its pick for a while so it doesn't dither between balls.
/// </summary>
static class PoolPilot
{
    public static Input Next(Game g, DemoPilot pilot)
    {
        var live = g.PoolBalls.Where(b => !b.Removed).ToList();
        if (live.Count == 0) { pilot.Say("WAIT: the table racks again"); return new Input(); }
        var pick = pilot.PoolTarget;
        if (pick == null || pick.Removed || g.PlayTime > pilot.PoolUntil)
        {
            var choices = live.Where(b => b.Number != 8 || live.Count == 1).DefaultIfEmpty(live[0]);
            (pick, pilot.PoolPocket) = choices
                .SelectMany(b => Enumerable.Range(0, Pool.PocketAim.Length).Select(k => (b, k)))
                .MinBy(c => Dist(c.b, Pool.PocketAim[c.k]) + Offside(g, c.b, Pool.PocketAim[c.k]));
            pilot.PoolTarget = pick;
            pilot.PoolUntil = g.PlayTime + 8f;
        }
        var (gx, gy) = Pool.PocketAim[pilot.PoolPocket];
        return BallPilot.Shoot(g, pilot, pick, gx, gy, (2.6f, Pool.W - 2.6f, 2.6f, Pool.H - 2.6f),
            $"SHOOT: the {pick.Number} ball's between you and the pocket: blast it low in the back",
            $"GET BEHIND: round to the far side of the {pick.Number} ball from its pocket", lineUp: 0.998f, aim: 0.02f);
    }

    static float Dist(SoccerBall b, (float x, float y) p) => MathF.Sqrt((b.X - p.x) * (b.X - p.x) + (b.Y - p.y) * (b.Y - p.y));

    /// <summary>How awkward a pot is: the spot behind the ball is off the table, or you're on the wrong side of it.</summary>
    static float Offside(Game g, SoccerBall b, (float x, float y) pocket)
    {
        float tx = pocket.x - b.X, ty = pocket.y - b.Y, tl = MathF.Max(0.01f, MathF.Sqrt(tx * tx + ty * ty));
        float sx = b.X - tx / tl * 4.5f, sy = b.Y - ty / tl * 4.5f;
        float off = MathF.Max(0, 2.6f - sx) + MathF.Max(0, sx - (Pool.W - 2.6f)) + MathF.Max(0, 2.6f - sy) + MathF.Max(0, sy - (Pool.H - 2.6f));
        float px = g.P.X - b.X, py = g.P.Y - b.Y;
        return off * 3 + ((px * tx + py * ty) > 0 ? 4 : 0);
    }
}
