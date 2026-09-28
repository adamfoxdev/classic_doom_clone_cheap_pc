namespace HexenSharp;

/// <summary>
/// Rocket Soccer (Practice > Rocket Soccer): a walled pitch with a goal at each end and a big ball, and a rocket
/// launcher and a grenade launcher to move it with. A blast shoves the ball away from it (a rocket right on the ball
/// hardest, and one under it lifts it), and you can run into it to dribble. One goal is lit: put the ball in it and
/// the other one lights. The wrong one just sends the ball back to the centre spot. Two kicker ramps in the middle
/// throw a ball that rolls up them into the air, and you with it. Two minutes a match from the first touch; your
/// goals go on a board for each class. Your blasts can't kill you, as on the other rocket courses.
/// </summary>
public static class Soccer
{
    public const float Length = 120f;
    /// <summary>The pitch: its walls (x) and the goal mouths' rows, the centre spot, and the recess behind each line.</summary>
    public const int W = 44, H = 28, LineW = 3, LineE = 41, MouthY0 = 11, MouthY1 = 16;
    public const float SpotX = 22f, SpotY = 14f, Crossbar = 2.5f;
    /// <summary>The ball: its radius, its fall, how it bounces off walls and the floor, how fast it slows rolling, and how hard a blast throws it (cells a second a point).</summary>
    public const float Radius = 0.6f, Gravity = 9f, WallBounce = 0.7f, FloorBounce = 0.55f, Roll = 1.2f, Knock = 0.09f;

    /// <summary>
    /// The ramps: kickers eight cells long rising a quarter step a cell to two cells high, and dropping off sheer at the
    /// top. The north one rises east and the south one west, each throwing a ball rolled up it on toward a goal.
    /// </summary>
    public readonly record struct Ramp(int X0, int Y0, int X1, int Y1, int Dir, float Top)
    {
        public int Length => X1 - X0 + 1;
        public bool Holds(float x, float y) => x >= X0 && x < X1 + 1 && y >= Y0 && y < Y1 + 1;
        /// <summary>The ramp's smooth surface at x (the cells step up it in quarters).</summary>
        public float Surface(float x) => Top * Math.Clamp((Dir > 0 ? x - X0 : X1 + 1 - x) / Length, 0f, 1f);
        /// <summary>The floor of a cell of it, the quarter step its top edge reaches.</summary>
        public float CellFloor(int x) => Top * (Dir > 0 ? x - X0 + 1 : X1 + 1 - x) / Length;
    }

    public static readonly Ramp[] Ramps = { new(14, 3, 21, 6, +1, 2f), new(22, 21, 29, 24, -1, 2f) };

    public const string About = "PUT A BIG BALL IN THE LIT GOAL WITH ROCKETS AND GRENADES. TWO MINUTES: HOW MANY GOALS?";
    const string Intro = "Rocket Soccer: blast the ball into the lit goal. Rockets shove it; one under it lifts it. The ramps launch it.";

    public static readonly Course Course = new("soccer", "Rocket Soccer", About, Map, false, Intro, StartAngle: 0, Soccer: true);

    public static MapDef Map()
    {
        var rows = new char[H][];
        for (int y = 0; y < H; y++)
        {
            rows[y] = new char[W];
            for (int x = 0; x < W; x++)
            {
                bool mouth = y >= MouthY0 && y <= MouthY1;
                bool pitch = y > 0 && y < H - 1 && x >= LineW && x < LineE;
                bool net = mouth && (x is >= 1 and < LineW || x >= LineE && x < W - 1);
                // the goals' back and sides are pale (ice), so the mouths stand out in the end walls
                bool netWall = !pitch && !net && y >= MouthY0 - 1 && y <= MouthY1 + 1 && (x < LineW || x >= LineE);
                rows[y][x] = pitch ? ',' : net ? '.' : netWall ? 'I' : '#';
            }
        }
        rows[(int)SpotY][10] = '@';
        foreach (var (x, y) in new[] { (LineW, MouthY0 - 1), (LineW, MouthY1 + 1), (LineE - 1, MouthY0 - 1), (LineE - 1, MouthY1 + 1) }) rows[y][x] = 't'; // torches at the posts
        var heights = Enumerable.Range(0, H).Select(y => new string(Enumerable.Range(0, W).Select(x => rows[y][x] == '.' ? Level.GlyphFromHeight(Crossbar) : '.').ToArray())).ToArray();
        var def = new MapDef("Rocket Soccer", "Rocket Soccer. Blast the ball into the lit goal.", "meadow", rows.Select(r => new string(r)).ToArray(), heights, Height: 10f);
        var floors = new List<(int, int, int, int, char)>();
        foreach (var r in Ramps)
            for (int x = r.X0; x <= r.X1; x++) floors.Add((x, r.Y0, x, r.Y1, Maps.FloorGlyph(r.CellFloor(x))));
        return Maps.Elevate(def, floors.ToArray());
    }

    /// <summary>The ground under the ball's middle: a ramp's smooth surface, or the cell's floor.</summary>
    public static float Ground(Level lv, float x, float y)
    {
        foreach (var r in Ramps) if (r.Holds(x, y)) return r.Surface(x);
        return lv.FloorAt(x, y);
    }

    /// <summary>Which goal (0 west, 1 east) a ball at x is wholly inside, or -1.</summary>
    public static int GoalAt(float x) => x + Radius < LineW ? 0 : x - Radius > LineE ? 1 : -1;

    /// <summary>The middle of a goal's line.</summary>
    public static (float x, float y) Mouth(int goal) => (goal == 0 ? LineW : LineE, (MouthY0 + MouthY1 + 1) / 2f);

    /// <summary>The ball's frames: a white ball with black patches, turning.</summary>
    public static Tex[] Frames => _frames ??= Enumerable.Range(0, 8).Select(f => Frame(f * MathF.Tau / 8)).ToArray();
    static Tex[] _frames;

    static Tex Frame(float rot)
    {
        const int S = 40;
        float R = S * 0.48f;
        var t = new Tex(S, S);
        // the patches: the twelve corners of an icosahedron
        float g = (1 + MathF.Sqrt(5)) / 2;
        var dots = new List<(float x, float y, float z)>();
        foreach (var a in new[] { -1f, 1f })
            foreach (var b in new[] { -g, g })
            {
                dots.Add((0, a, b)); dots.Add((a, b, 0)); dots.Add((b, 0, a));
            }
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float nx = (x + 0.5f - S / 2f) / R, ny = (y + 0.5f - S / 2f) / R, q = nx * nx + ny * ny;
                if (q > 1) continue;
                float nz = MathF.Sqrt(1 - q);
                // turn it about the vertical, and a little about the view axis so it reads as rolling
                float rx = nx * MathF.Cos(rot) + nz * MathF.Sin(rot), rz = -nx * MathF.Sin(rot) + nz * MathF.Cos(rot);
                bool patch = dots.Any(d => (rx * d.x + ny * d.y + rz * d.z) / MathF.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z) > 0.92f);
                float light = Math.Clamp(0.35f + 0.75f * (-0.45f * nx - 0.55f * ny + 0.7f * nz), 0.2f, 1.1f);
                uint c = patch ? Col.Rgb(34, 34, 40) : Col.Rgb(236, 232, 222);
                t.Px[y * S + x] = Col.Shade(c, (int)(light * 256));
            }
        new Canvas(t).Outline(Col.Rgb(20, 18, 16));
        return t;
    }
}

/// <summary>The ball. Its Z is the height of its bottom (absolute, like a projectile's).</summary>
public sealed class SoccerBall : Thing
{
    public float VX, VY, VZ, Spin;
    public bool Grounded;
    /// <summary>Its look (the soccer ball's unless set), how fast it slows rolling, how it comes off walls, and how hard blasts throw it.</summary>
    public Tex[] Frames;
    public float Roll = Soccer.Roll, WallBounce = Soccer.WallBounce, Knock = Soccer.Knock;
    /// <summary>A pool ball's number (0 for the soccer ball).</summary>
    public int Number;
    public SoccerBall(float radius = Soccer.Radius) { Radius = radius; SpriteW = SpriteH = radius * 2; Solid = false; }
    public float MidZ => Z + Radius;
    public override Tex Sprite(float time) => (Frames ?? Soccer.Frames)[(int)(Spin * 3) & 7];
}

/// <summary>One soccer match: the goals scored, the shots it took, and whose it was.</summary>
public sealed class SoccerRun
{
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public int Goals { get; set; }
    public int Shots { get; set; }
    public DateTime When { get; set; }
}

public sealed partial class Game
{
    public bool OnSoccer => Practicing && Course.Soccer;
    public SoccerBall Ball;
    /// <summary>The match: goals scored, rockets and grenades fired, the lit goal (0 west, 1 east), and the clock.</summary>
    public int SoccerGoals, SoccerShots, LitGoal = 1;
    public float SoccerLeft = Soccer.Length;
    public SoccerRun LastSoccer;
    public int LastSoccerPlace;
    /// <summary>After a goal: seconds until the ball comes back to the spot (negative when it's in play).</summary>
    float _kickoff = -1, _beacon, _ballLastX, _ballLastY;

    /// <summary>On a fresh pitch: the rocket launcher (1) and the grenade launcher (2), mana that never runs out.</summary>
    void SetUpSoccer()
    {
        var p = P;
        p.Loadout = new[] { Rockets.Launcher, Grenades.Launcher };
        p.HasWeapon = new[] { true, true };
        p.Weapon = 0; p.PendingWeapon = -1; p.Raise = 0;
        p.BlueMana = p.GreenMana = 200;
        _unhurt = 0; _regen = 0;
        Level.Theme.FogDist = MathF.Max(Level.Theme.FogDist, 46f); // a clear day: the far goal in sight from the other end
    }

    /// <summary>A new match: nil each, the east goal lit, the ball on the spot, the clock waiting for the first touch.</summary>
    void ResetSoccer()
    {
        SoccerGoals = SoccerShots = 0; LitGoal = 1; SoccerLeft = Soccer.Length;
        KickOff();
    }

    /// <summary>The ball dropped on the centre spot, still.</summary>
    void KickOff()
    {
        _kickoff = -1;
        if (Ball == null || Ball.Level != Level || !Level.Things.Contains(Ball))
        {
            Ball = new SoccerBall { Level = Level };
            Level.Things.Add(Ball);
        }
        Ball.Removed = false;
        Ball.X = Soccer.SpotX; Ball.Y = Soccer.SpotY; Ball.Z = 1.5f;
        Ball.VX = Ball.VY = Ball.VZ = 0; Ball.Grounded = false;
    }

    /// <summary>The ball touched: the clock starts.</summary>
    void BallTouched()
    {
        if (!(OnSoccer || OnPool) || RunStarted) return;
        RunStarted = true;
    }

    /// <summary>Each frame of a match: the clock, the goals, the lit goal's sparkle, and the kick-off after a goal.</summary>
    void SoccerTick(float dt)
    {
        if (!OnSoccer || Ball == null) return;
        if (_kickoff >= 0)
        {
            if ((_kickoff -= dt) < 0) { KickOff(); PlaySound(Sfx.Teleport, 0.7f); }
        }
        else
        {
            int goal = Soccer.GoalAt(Ball.X);
            if (goal >= 0) Scored(goal);
        }
        if ((_beacon -= dt) <= 0)
        {
            _beacon = 0.2f;
            var (mx, _) = Soccer.Mouth(LitGoal);
            foreach (float y in new[] { Soccer.MouthY0 + 0.1f, Soccer.MouthY1 + 0.9f })
                SpawnPuff(Art.RailSpiral, mx, y, Soccer.Crossbar * RandF(), 0.3f);
        }
        if (!RunStarted || Mode != GameMode.Playing) return;
        SoccerLeft -= dt;
        if (SoccerLeft <= 0) EndSoccerMatch();
    }

    /// <summary>The ball's gone in a goal: the lit one scores and lights the other; the wrong one sends it back.</summary>
    void Scored(int goal)
    {
        _kickoff = 1.2f;
        Ball.VX = Ball.VY = 0;
        for (int k = 0; k < 6; k++) SpawnPuff(Art.Fireball[1], Ball.X, Ball.Y + (k - 2.5f) * 0.6f, Ball.MidZ + RandF(), 0.8f);
        Ball.Removed = true;
        if (goal != LitGoal)
        {
            PlaySound(Sfx.Locked, 0.8f);
            Say("Wrong goal! Back to the spot.");
            return;
        }
        SoccerGoals++;
        LitGoal = 1 - LitGoal;
        PlaySound(Sfx.Secret, 1);
        AddShake(0.3f);
        Say($"GOAL! {SoccerGoals}. Now the {(LitGoal == 0 ? "west" : "east")} goal.");
    }

    /// <summary>Full time: on the board if you scored, and a fresh match.</summary>
    void EndSoccerMatch()
    {
        Messages.Clear();
        if (Demo || PracticeSpeed < 1 || SoccerGoals == 0)
            Say(SoccerGoals == 0 ? "Full time: no goals. Get behind the ball, facing the lit goal, and blast it from close." : $"Full time: {SoccerGoals} goals at {PracticeSpeed * 100:0}% speed.");
        else
        {
            var run = new SoccerRun { Name = RunnerName, Class = P.Class.ToString(), Goals = SoccerGoals, Shots = SoccerShots, When = DateTime.Now };
            int best = Profile.SoccerBest(P.Class);
            int place = Profile.AddSoccerRun(run);
            if (place > 0) SaveProfile();
            LastSoccer = run; LastSoccerPlace = place;
            PlaySound(place == 1 ? Sfx.Secret : Sfx.Teleport, 1);
            string how = $"Full time: {SoccerGoals} goal{(SoccerGoals == 1 ? "" : "s")} from {SoccerShots} shots";
            Say(place == 1 ? $"{how} - a new best!" : place > 0 ? $"{how} - #{place} on the board (best {best})." : $"{how} (best {best}).");
        }
        MoveTo(Level.StartX, Level.StartY, Course.StartAngle);
        ResetRun();
    }

    /// <summary>
    /// The ball's physics, each frame: it falls and bounces, rolls and slows, runs up the ramps (and off the top, into
    /// the air), glances off walls, ledges and the crossbar, and is shoved along when you run into it.
    /// </summary>
    void BallTick(SoccerBall b, float dt)
    {
        float r = b.Radius;
        float sp = MathF.Sqrt(b.VX * b.VX + b.VY * b.VY + b.VZ * b.VZ);
        int steps = Math.Max(1, (int)(sp * dt / 0.05f) + 1);
        float h = dt / steps;
        for (int s = 0; s < steps; s++)
        {
            if (b.Grounded)
            {
                // rolling: it slows, and a slope pulls it back down
                float flat = MathF.Sqrt(b.VX * b.VX + b.VY * b.VY);
                float keep = flat > 0 ? MathF.Max(0, flat - b.Roll * h) / flat : 0;
                b.VX *= keep; b.VY *= keep;
                if (OnSoccer)
                    foreach (var ramp in Soccer.Ramps)
                        if (ramp.Holds(b.X, b.Y)) b.VX -= Soccer.Gravity * ramp.Dir * ramp.Top / ramp.Length * h;
            }
            else b.VZ -= Soccer.Gravity * h;
            // across: a wall, the crossbar, or a face higher than it can roll up turns it back
            bool Stops(float x, float y, float dx, float dy)
            {
                if (Level.BlocksCircle(x, y, r)) return true;
                float ax = x + dx * r * 0.7f, ay = y + dy * r * 0.7f;
                return BallGround(ax, ay) > b.Z + 0.3f || Level.HeightAt(ax, ay) < b.Z + 2 * r;
            }
            float nx = b.X + b.VX * h, ny = b.Y + b.VY * h;
            if (b.VX != 0 && Stops(nx, b.Y, MathF.Sign(b.VX), 0)) { if (MathF.Abs(b.VX) > 1.5f) Thud(b); b.VX = -b.VX * b.WallBounce; }
            else b.X = nx;
            if (b.VY != 0 && Stops(b.X, ny, 0, MathF.Sign(b.VY))) { if (MathF.Abs(b.VY) > 1.5f) Thud(b); b.VY = -b.VY * b.WallBounce; }
            else b.Y = ny;
            // up and down
            float under = BallGround(b.X, b.Y);
            if (b.Grounded)
            {
                // on the ground it follows the surface: up a ramp it climbs (and keeps the climb, off the top), off an edge it falls
                float drop = b.Z - under, follow = MathF.Sqrt(b.VX * b.VX + b.VY * b.VY) * h * 0.5f + 0.002f;
                if (drop > follow) { b.Grounded = false; }
                else { b.VZ = MathF.Min((under - b.Z) / h, MathF.Sqrt(b.VX * b.VX + b.VY * b.VY) * 0.3f); b.Z = under; }
                if (b.Grounded && b.VZ < 0) b.VZ = 0; // downhill: no fall speed to carry off the bottom
            }
            else
            {
                b.Z += b.VZ * h;
                if (b.Z <= under)
                {
                    b.Z = under;
                    if (b.VZ < -1.5f) { if (b.VZ < -3f) Thud(b); b.VZ = -b.VZ * Soccer.FloorBounce; }
                    else { b.VZ = 0; b.Grounded = true; }
                }
            }
            float ceiling = Level.HeightAt(b.X, b.Y);
            if (b.Z + 2 * r > ceiling) { b.Z = ceiling - 2 * r; if (b.VZ > 0) b.VZ = -b.VZ * Soccer.WallBounce; b.Grounded = false; }
        }
        b.Spin += MathF.Sqrt(b.VX * b.VX + b.VY * b.VY) * dt / (MathF.Tau * r) * 8;
        BallMeetsPlayer(b, dt);
    }

    void Thud(SoccerBall b) => Sound(Sfx.Hit, b.X, b.Y);

    /// <summary>The ground under a ball: the pitch's ramps on Rocket Soccer, else the floor.</summary>
    float BallGround(float x, float y) => OnSoccer ? Soccer.Ground(Level, x, y) : Level.FloorAt(x, y);

    /// <summary>The balls in play: the soccer ball, or the pool balls on the table.</summary>
    IEnumerable<SoccerBall> LiveBalls()
    {
        if (OnSoccer && Ball is { Removed: false } b && b.Level == Level) yield return b;
        if (OnPool) foreach (var pb in PoolBalls) if (!pb.Removed && pb.Level == Level) yield return pb;
    }

    /// <summary>Your velocity this frame, for running into balls: Quake movement's, or how far you've come since the last frame.</summary>
    (float x, float y) RunVelocity(float dt)
    {
        if (PlayTime == _runVelAt) return _runVel;
        var p = P;
        float vx = p.VX, vy = p.VY;
        if (dt > 0 && vx == 0 && vy == 0) { vx = (p.X - _ballLastX) / dt; vy = (p.Y - _ballLastY) / dt; }
        if (MathF.Abs(vx) + MathF.Abs(vy) > 20) vx = vy = 0; // (a teleport, not a run)
        _ballLastX = p.X; _ballLastY = p.Y;
        _runVelAt = PlayTime;
        return _runVel = (vx, vy);
    }
    float _runVelAt = -1;
    (float x, float y) _runVel;

    /// <summary>You run into the ball: it's pushed off you, at least as fast as you were going into it.</summary>
    void BallMeetsPlayer(SoccerBall b, float dt)
    {
        var p = P;
        var (pvx, pvy) = RunVelocity(dt);
        if (Mode != GameMode.Playing || OnSoccer && _kickoff >= 0) return;
        float feet = p.FloorZ + p.Z;
        if (feet > b.Z + 2 * b.Radius - 0.15f || feet + Player.Height < b.Z) return;
        float dx = b.X - p.X, dy = b.Y - p.Y, d = MathF.Sqrt(dx * dx + dy * dy), reach = b.Radius + p.Radius;
        if (d >= reach) return;
        if (d < 0.001f) { dx = MathF.Cos(p.Angle); dy = MathF.Sin(p.Angle); d = 1; }
        float ux = dx / d, uy = dy / d;
        float into = pvx * ux + pvy * uy, away = b.VX * ux + b.VY * uy;
        float want = MathF.Max(into * 1.3f + 0.6f, 0.8f);
        if (away < want) { b.VX += ux * (want - away); b.VY += uy * (want - away); }
        // out of you: the ball moves if it can, else you do
        float push = reach - d + 0.01f;
        if (!Level.BlocksCircle(b.X + ux * push, b.Y + uy * push, b.Radius)) { b.X += ux * push; b.Y += uy * push; }
        else if (!Level.BlocksCircle(p.X - ux * push, p.Y - uy * push, p.Radius)) { p.X -= ux * push; p.Y -= uy * push; }
        BallTouched();
    }

    /// <summary>A rocket's or grenade's blast shoves the balls away from it, harder the nearer their surfaces are.</summary>
    void BallBlast(Projectile pr)
    {
        foreach (var b in LiveBalls().ToList()) BallBlast(pr, b);
    }

    void BallBlast(Projectile pr, SoccerBall b)
    {
        float dx = b.X - pr.X, dy = b.Y - pr.Y, dz = b.MidZ - pr.Z, d = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        float pts = Rockets.Points(MathF.Max(0, d - b.Radius));
        if (pts <= 0 || !Level.Sight(pr.X, pr.Y, b.X, b.Y)) return;
        if (d < 0.01f) { dx = 0; dy = 0; dz = 1; d = 1; }
        float k = pts * b.Knock / d;
        b.VX += dx * k; b.VY += dy * k; b.VZ += dz * k;
        if (b.VZ > 0.5f) b.Grounded = false;
        Sound(Sfx.Push, b.X, b.Y);
        if (pr.FromPlayer) BallTouched();
    }

    /// <summary>Does a shot at (x, y, z) touch a ball?</summary>
    bool HitsBall(float x, float y, float z, float pad)
    {
        foreach (var b in LiveBalls())
        {
            float dx = b.X - x, dy = b.Y - y, dz = b.MidZ - z;
            if (dx * dx + dy * dy + dz * dz < (b.Radius + pad) * (b.Radius + pad)) return true;
        }
        return false;
    }
}

/// <summary>
/// The soccer demo: it gets behind the ball on the line from the lit goal through it, then runs at it, and fires a
/// rocket into the ball's back when it's lined up and a few cells off, following it up the pitch.
/// </summary>
static class SoccerPilot
{
    public static Input Next(Game g, DemoPilot pilot)
    {
        var p = g.P;
        var b = g.Ball;
        if (b == null || b.Removed) { pilot.Say("WAIT: the ball comes back to the spot"); return new Input(); }
        var (gx, gy) = Soccer.Mouth(g.LitGoal);
        // aim for the middle of the goal, or at a corner of it when the ball is off to one side
        gy = Math.Clamp(b.Y, Soccer.MouthY0 + 1.5f, Soccer.MouthY1 - 0.5f);
        return BallPilot.Shoot(g, pilot, b, gx, gy, (Soccer.LineW + 0.6f, Soccer.LineE - 0.6f, 1.6f, Soccer.H - 2.6f),
            "SHOOT: the ball's between you and the goal: blast it low in the back", "GET BEHIND: round to the far side of the ball from the lit goal");
    }
}

/// <summary>
/// Shooting a ball at a target (a goal, a pocket): get behind it on the line from the target through it, giving it a
/// wide berth, then fire a rocket low into its back from a few cells off, out of reach of the blast.
/// </summary>
static class BallPilot
{
    public static Input Shoot(Game g, DemoPilot pilot, SoccerBall b, float gx, float gy, (float x0, float x1, float y0, float y1) room, string shoot, string behind, float lineUp = 0.9f, float aim = 0.08f)
    {
        var p = g.P;
        float tx = gx - b.X, ty = gy - b.Y, tl = MathF.Max(0.01f, MathF.Sqrt(tx * tx + ty * ty));
        tx /= tl; ty /= tl;
        // the spot behind the ball, from which a shot sends it at the goal
        float back = 4.5f, sx = b.X - tx * back, sy = b.Y - ty * back;
        sx = Math.Clamp(sx, room.x0, room.x1); sy = Math.Clamp(sy, room.y0, room.y1);
        float toSpot = MathF.Sqrt((sx - p.X) * (sx - p.X) + (sy - p.Y) * (sy - p.Y));
        float bx = b.X - p.X, by = b.Y - p.Y, toBall = MathF.Sqrt(bx * bx + by * by);
        float lined = (bx * tx + by * ty) / MathF.Max(0.01f, toBall); // 1 when the ball is straight between you and the goal
        float sens = 0.0025f * g.Vars.Sens;
        var inp = new Input();
        float face;
        if (lined > lineUp && toBall < 7f)
        {
            // lined up: look at the ball's lower half and shoot
            face = MathF.Atan2(by, bx);
            float drop = p.FloorZ + p.Z + 0.32f - (b.Z + b.Radius * 0.6f);
            float want = -MathF.Atan2(drop, toBall) * (160f / MathF.Tan(g.ViewFov * MathF.PI / 360f));
            inp.LookY = (p.Pitch - want) / (0.35f * g.Vars.Sens);
            inp.Move = toBall > 5f ? 1 : toBall < 4f ? -1 : 0; // far enough off that the blast doesn't catch it too
            inp.Fire = p.Cooldown <= 0 && toBall > 3.8f && MathF.Abs(Game.AngleDiff(face, p.Angle)) < aim && MathF.Abs(p.Pitch - want) < 6f;
            pilot.Say(shoot);
        }
        else
        {
            // round behind it: to the spot, giving the ball a wide berth
            float ax = sx - p.X, ay = sy - p.Y;
            if (toBall < 2f && lined < 0.5f) { ax += -by * 2; ay += bx * 2; } // step round it rather than through it
            face = MathF.Atan2(ay, ax);
            inp.Move = toSpot > 0.4f ? 1 : 0;
            inp.LookY = p.Pitch / (0.35f * g.Vars.Sens);
            pilot.Say(behind);
        }
        inp.LookX = MathF.IEEERemainder(face - p.Angle, MathF.Tau) / sens;
        return inp;
    }
}
