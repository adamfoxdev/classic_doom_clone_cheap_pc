namespace HexenSharp;

/// <summary>
/// A practice course (Main menu > Practice). Timed courses have a clock, an exit that ends the run once every checkpoint
/// is reached, a leaderboard and a ghost for each class; the free-roam one is just space to move in. Gap courses list
/// their platforms, which the hints use to say how fast you'll need to be for each gap.
/// </summary>
public sealed record Course(string Id, string Name, string About, Func<MapDef> Map, bool Timed, string Intro,
    (int x0, int x1, float floor)[] Platforms = null, float StartAngle = 0, bool Jetpack = false, float Route = 0,
    bool Endless = false, int Seed = 0, bool Range = false, bool Rockets = false, bool Grenades = false)
{
    /// <summary>The leaderboard and ghost key for a class (the Velocity Hangar's are just the class, as they were first).</summary>
    public string Key(PClass cls) => Id == "hangar" ? cls.ToString() : $"{Id}/{cls}";

    /// <summary>How far a run goes, start to finish: a gap course's length (from the start to the exit), or its set route.</summary>
    public float RouteLength => Route > 0 ? Route : Platforms != null ? Platforms[^1].x1 - 3f : 0;

    /// <summary>
    /// The medal target times for a class, in seconds: the time to cover the route at an average of your class's run
    /// speed (bronze), 1.35 times it (silver) and 1.7 times it (gold), rounded up to the half second. Classes that run
    /// slower get more time; skills don't change the targets.
    /// </summary>
    public (float gold, float silver, float bronze) MedalTimes(PClass cls)
    {
        if (!Timed) return (0, 0, 0);
        float run = 3.6f * ClassDef.All[(int)cls].Speed;
        float T(float pace) => MathF.Ceiling(RouteLength / (pace * run) * 2) / 2;
        return (T(Medals.GoldPace), T(Medals.SilverPace), T(Medals.BronzePace));
    }

    /// <summary>The medal a time earns as `cls` (None for no time, or slower than bronze).</summary>
    public Medal MedalFor(PClass cls, float time)
    {
        if (time <= 0 || !Timed) return Medal.None;
        var (gold, silver, bronze) = MedalTimes(cls);
        return time <= gold ? Medal.Gold : time <= silver ? Medal.Silver : time <= bronze ? Medal.Bronze : Medal.None;
    }
}

public enum Medal { None, Bronze, Silver, Gold }

public static class Medals
{
    /// <summary>Average speed over the route, as a multiple of your class's run speed, for each medal.</summary>
    public const float BronzePace = 1f, SilverPace = 1.35f, GoldPace = 1.7f;

    public static uint Colour(Medal m) => m switch
    {
        Medal.Gold => Col.Rgb(255, 204, 60),
        Medal.Silver => Col.Rgb(200, 212, 228),
        Medal.Bronze => Col.Rgb(210, 130, 60),
        _ => Col.Rgb(90, 86, 80),
    };

    public static string Name(Medal m) => m.ToString().ToUpperInvariant();
}

public static class Courses
{
    const string StrafeIntro = "Strafe jumping: jump, then hold A or D and turn the mouse the same way. Hop again the moment you land.";

    public static readonly Course Hangar = new("hangar", "Velocity Hangar",
        "A STRAIGHT RUN OF PLATFORMS OVER GAPS THAT WIDEN FROM 2 CELLS TO 6.", Maps.VelocityCourse, true, StrafeIntro, Maps.CoursePlatforms);

    /// <summary>Descent's platforms, each lower than the last: the drop buys hang time for the wider gaps (3 to 7).</summary>
    public static readonly (int x0, int x1, float floor)[] DescentPlatforms =
        { (1, 14, 6f), (18, 27, 5.5f), (32, 41, 4.75f), (47, 58, 3.75f), (65, 76, 2.5f), (84, 91, 1f) };

    public static readonly Course Descent = new("descent", "Descent",
        "A STAIRWAY OF PLATFORMS DROPPING AWAY. EACH FALL BUYS HANG TIME FOR A WIDER GAP.",
        () => Maps.GapCourse("Descent", "Descent. Each platform drops lower, and each gap is wider: use the fall.", "crypt", DescentPlatforms, 9f),
        true, "Descent: every drop gives you more hang time. Build speed on the top platform, then keep hopping.", DescentPlatforms);

    public static readonly Course Circuit = new("circuit", "Circuit",
        "A LAP OF A LOOPED TRACK. KEEP YOUR SPEED THROUGH THE CORNERS BY STRAFING INTO THEM.",
        CircuitMap, true, "Circuit: one lap, clockwise. Strafe into each corner to carry your speed round it.", StartAngle: MathF.PI,
        Route: 96); // a lap on a line cutting the corners, between the centre line (110) and hugging the island (84)

    public static readonly Course FreeRoam = new("free", "Free Roam",
        "A WIDE OPEN FIELD WITH NOTHING IN IT: NO CLOCK, NO EXIT. PRACTISE HOWEVER YOU LIKE.",
        FreeRoamMap, false, "Free Roam: all the room you want, and a jetpack (Q). Nothing to finish; Esc when you're done.", Jetpack: true);

    public static readonly Course[] All = { Hangar, Descent, Circuit, RocketCourse.Course, GrenadeCourse.Course, HexenSharp.Endless.Pick, ShootingRange.Course, FreeRoam };
    public static Course[] Timed => All.Where(c => c.Timed).ToArray();

    /// <summary>
    /// Circuit: a rectangular loop, 8 wide, round a walled island. The four sides are its checkpoint zones (the long
    /// straights sit a quarter step up, so each side is a zone of its own); the finish line is across the south
    /// straight, just behind the start, so you cross it at the end of the lap.
    /// </summary>
    static MapDef CircuitMap()
    {
        const int w = 46, h = 30;
        var rows = new char[h][];
        for (int y = 0; y < h; y++)
        {
            rows[y] = new char[w];
            for (int x = 0; x < w; x++)
                rows[y][x] = y == 0 || y == h - 1 || x == 0 || x == w - 1 || (x >= 9 && x <= 36 && y >= 9 && y <= 20) ? 'O' : '.';
        }
        foreach (var (x, y) in new[] { (1, 1), (44, 1), (1, 28), (44, 28), (22, 1), (22, 28), (1, 14), (44, 14) }) rows[y][x] = 't';
        for (int y = 21; y <= 28; y++) rows[y][35] = 'E';
        rows[24][32] = '@';
        foreach (var (x, y) in new[] { (4, 14), (22, 4), (40, 14), (30, 26) }) rows[y][x] = '+';
        var def = new MapDef("Circuit", "Circuit. One lap round the track, through every checkpoint, back to the line.", "hall",
            rows.Select(r => new string(r)).ToArray(), Height: 3f);
        return Maps.Elevate(def, (9, 1, 36, 8, '1'), (9, 21, 36, 28, '1'));
    }

    /// <summary>Free Roam: a big open field under the sky, walled far off, with nothing in it but you.</summary>
    static MapDef FreeRoamMap()
    {
        const int n = 64;
        var rows = Enumerable.Range(0, n).Select(y => new string(Enumerable.Range(0, n).Select(x =>
            x == 0 || y == 0 || x == n - 1 || y == n - 1 ? '#' : x == n / 2 && y == n / 2 ? '@' : ',').ToArray())).ToArray();
        return new MapDef("Free Roam", "Free Roam. Nothing here but room to move.", "meadow", rows, Height: 3f);
    }
}

/// <summary>
/// The practice demo's pilot: plays a course with strafe jumping so you can watch how it's done, pressing keys and
/// turning the mouse through the same Input a player would. In the air it holds a strafe key and turns with its
/// velocity at the best angle (as the strafe helper shows), switching sides to steer, so it zig-zags along while it
/// speeds up. Round the Circuit and Free Roam it follows a route. On a gap course it plans each platform as it lands on
/// it: it tries zig-zags of different widths in a private copy of the course (a wider one covers a little less ground
/// each hop, so one of them brings a landing to the edge just where a straight hop clears the gap), and follows the
/// quickest that makes it. When none can, it circles to build speed and plans again. The demo runs at a fixed 72 ticks
/// a second, so the copy plays out exactly as the real thing and the demo is the same every time. Caption says what
/// it's doing, for step-by-step viewing.
/// </summary>
public sealed class DemoPilot
{
    public const float Tick = 1f / 72f;
    int _wp, _platform = -1;
    Approach _approach;
    readonly Flight _flight = new();
    float _windUpFrom;
    bool _windingUp, _circling;
    Game _sim;
    public string Caption = "", Step = "";
    /// <summary>The rocket-jump course: the platform it last stood on (and so launched from).</summary>
    internal int RocketFrom;
    /// <summary>The grenade course: which step of it the demo is on, and how long it's been at it.</summary>
    internal int GrenadeStep;
    internal float GrenadeWait;
    internal (float x, float y)? LobSpot;

    public Input Next(Game g, float dt)
    {
        if (g.Course.Rockets) return RocketJumper.Next(g, this);
        if (g.Course.Grenades) return GrenadePilot.Next(g, this);
        var p = g.P;
        var c = g.Course;
        float run = g.RunSpeed, v = p.HSpeed;
        if (p.OnGround && v < run * 0.85f)
        {
            // get up to speed on foot first, down the course (or on toward the route)
            _approach = null; _windingUp = false; _platform = -1;
            float down = c.Platforms != null ? MathF.Atan2(5.5f - p.Y, 6f) : RouteHeading(g);
            Say("RUN: hold W to get up to speed");
            return new Input { Move = 1, LookX = Wrap(down - p.Angle) / (0.0025f * g.Vars.Sens) };
        }

        if (c.Platforms is { } plats)
        {
            int k = Math.Max(0, Array.FindLastIndex(plats, pl => p.X >= pl.x0 - 0.5f));
            if (k >= plats.Length - 1)
            {
                // the last platform: on to the exit
                var exit = g.Level.FindMark('E') ?? (plats[^1].x1, 5.5f);
                return Steer(g, MathF.Atan2(exit.y - p.Y, exit.x - p.X), dt, null);
            }
            // plan on landing on a new platform; winding up, look again at every landing, to go the moment it can
            if (p.OnGround && (k != _platform || _windingUp))
            {
                _platform = k;
                _approach = PlanPlatform(g, plats[k], plats[k + 1]);
                _windingUp = _approach == null;
                _windUpFrom = v;
                _circling = false;
            }
            if (_windingUp)
            {
                // hop to the platform's middle, then circle there, one strafe side held: the speed climbs lap by lap
                var pl = plats[k];
                float cx = (pl.x0 + pl.x1) / 2f, off = MathF.Sqrt((cx - p.X) * (cx - p.X) + (5.5f - p.Y) * (5.5f - p.Y));
                if (off < 1.2f) _circling = true;
                else if (off > 3f) _circling = false;
                Say("WIND UP: not fast enough for the next gap yet. Circle, strafing, to build speed");
                if (p.OnGround) return Hop(g);
                return _circling ? _flight.Circle(g, dt) : _flight.Toward(g, MathF.Atan2(5.5f - p.Y, cx - p.X), dt);
            }
            if (_approach == null) return Steer(g, MathF.Atan2(5.5f - p.Y, 8f), dt, null); // until the next landing plans
            var act = _approach.Act(g, plats[k], plats[k + 1], dt);
            Say(_approach.Go ? "GO: hop straight across the gap"
                : _approach.Width > 0.1f ? "LINE UP: zig-zag wider so a landing comes down right at the edge"
                : act.Strafe > 0 ? "STRAFE: let go of W, hold D and turn the mouse right with your velocity"
                : act.Strafe < 0 ? "SWITCH: hold A and turn left, to steer back on course"
                : "JUMP: hop again the moment you land, before friction slows you");
            return act;
        }
        return Steer(g, RouteHeading(g), dt, null);
    }

    /// <summary>Hop on landing; in the air, strafe toward `heading`.</summary>
    Input Steer(Game g, float heading, float dt, string caption)
    {
        var p = g.P;
        if (p.OnGround) { Say(caption ?? "JUMP: hop again the moment you land, before friction slows you"); return Hop(g); }
        var inp = _flight.Toward(g, heading, dt);
        Say(caption ?? (inp.Strafe > 0 ? "STRAFE: let go of W, hold D and turn the mouse right with your velocity"
            : "SWITCH: hold A and turn left, to steer back on course"));
        return inp;
    }

    static Input Hop(Game g) => new() { Jump = true, Move = 1, LookX = Wrap(MathF.Atan2(g.P.VY, g.P.VX) - g.P.Angle) / (0.0025f * g.Vars.Sens) };

    float RouteHeading(Game g)
    {
        var p = g.P;
        var route = g.Course.Id == "circuit"
            ? new[] { (6f, 24.5f), (6f, 5f), (39.5f, 5f), (39.5f, 24.5f), (28f, 24.5f) }
            : new[] { (16f, 16f), (48f, 16f), (48f, 48f), (16f, 48f) };
        var (tx, ty) = route[_wp % route.Length];
        if (MathF.Sqrt((tx - p.X) * (tx - p.X) + (ty - p.Y) * (ty - p.Y)) < 4f) _wp++;
        (tx, ty) = route[_wp % route.Length];
        return MathF.Atan2(ty - p.Y, tx - p.X);
    }

    /// <summary>
    /// Plans a platform: plays out approaches of each zig-zag width (0 to 50 degrees each side, starting either way) in
    /// the copy of the course, and returns the one that reaches the next platform soonest, or null if none can yet.
    /// </summary>
    Approach PlanPlatform(Game g, (int x0, int x1, float floor) a, (int x0, int x1, float floor) b)
    {
        if (_sim == null)
        {
            _sim = new Game { FixedSeed = 1, Replaying = true }; // a private copy: it records and saves nothing
            _sim.Vars.Ghost = false; _sim.Vars.StrafeHelp = 0; _sim.Vars.QuakeMove = g.Vars.QuakeMove;
            _sim.StartPractice(g.P.Class, g.Course);
        }
        Approach best = null;
        float bestTime = float.MaxValue, bestSpeed = 0;
        for (int deg = 0; deg <= 50; deg += 5)
            foreach (int sign in deg == 0 ? new[] { 1 } : new[] { 1, -1 })
            {
                var trial = new Approach(deg * MathF.PI / 180, sign, _flight);
                var (made, time, speed) = Rollout(g, trial, a, b);
                if (made && (time < bestTime - 0.01f || (time < bestTime + 0.01f && speed > bestSpeed)))
                    (best, bestTime, bestSpeed) = (new Approach(deg * MathF.PI / 180, sign, _flight), time, speed);
            }
        return best;
    }

    /// <summary>Plays an approach in the copy of the course from where we are now: does it reach the next platform, how soon, how fast.</summary>
    (bool made, float time, float speed) Rollout(Game g, Approach plan, (int x0, int x1, float floor) a, (int x0, int x1, float floor) b)
    {
        var s = _sim;
        s.P = g.P.Copy();
        s.Level.CheckpointsReached.Clear(); s.Checkpoint = null; // the lift stays dark: a fall stays a fall
        for (int t = 0; t < 72 * 5; t++)
        {
            s.Update(plan.Act(s, a, b, Tick), Tick);
            var p = s.P;
            if (p.OnGround && p.X >= b.x0 - p.Radius && MathF.Abs(p.FloorZ - b.floor) < 0.01f) return (true, t * Tick, p.HSpeed);
            // fallen in: down below both platforms (just flying over the gap is fine)
            if (p.FloorZ + p.Z < MathF.Min(a.floor, b.floor) - 0.2f || p.HSpeed < g.RunSpeed * 0.5f) return (false, 0, 0);
        }
        return (false, 0, 0);
    }

    static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);

    internal void Say(string s)
    {
        if (s == Caption) return;
        Caption = s;
        Step = s[..s.IndexOf(':')];
    }

    /// <summary>Strafing in the air: which key, and where to turn the mouse. Keeps its own strafe side between calls.</summary>
    sealed class Flight
    {
        public int Side = 1;
        public Flight Copy() => new() { Side = Side };

        /// <summary>Hold the strafe key that turns the velocity toward `heading` (keeping the current one near it), turning with it at the best angle.</summary>
        public Input Toward(Game g, float heading, float dt)
        {
            float err = Wrap(MathF.Atan2(g.P.VY, g.P.VX) - heading);
            if (err > 0.2f) Side = -1; else if (err < -0.2f) Side = 1;
            return Strafe(g, dt);
        }

        public Input Circle(Game g, float dt) { Side = 1; return Strafe(g, dt); }

        Input Strafe(Game g, float dt)
        {
            var p = g.P;
            float run = g.RunSpeed, v = p.HSpeed, h = MathF.Atan2(p.VY, p.VX);
            var best = g.StrafeZone(Side * MathF.PI / 2).best;
            // a frame longer than a physics step lets the velocity swing round a/v a step: turn ahead to keep up
            float lead = Side * MathF.Max(0, dt * 72f - 1) * (g.Vars.AirAccel * run / 72f) / MathF.Max(v, 0.1f);
            return new Input { Strafe = Side, LookX = Wrap(h + best + lead - p.Angle) / (0.0025f * g.Vars.Sens) };
        }
    }

    /// <summary>
    /// Approaching a gap: hop after hop, zig-zag at ±Width about the course's line (alternating sides each hop, drawn back
    /// toward the middle), until a straight hop from where it lands would clear the gap; then go straight over.
    /// </summary>
    sealed class Approach
    {
        public readonly float Width;
        readonly Flight _flight;
        int _sign;
        bool _inAir;
        public bool Go;
        public Approach(float width, int sign, Flight flight) { Width = width; _sign = sign; _flight = flight.Copy(); }

        public Input Act(Game g, (int x0, int x1, float floor) a, (int x0, int x1, float floor) b, float dt)
        {
            var p = g.P;
            if (p.OnGround)
            {
                if (_inAir) { _sign = -_sign; _inAir = false; }
                float j = g.Vars.JumpPower, drop = a.floor - b.floor;
                float air = (j + MathF.Sqrt(j * j + 2 * g.Vars.Gravity * drop)) / g.Vars.Gravity;
                float edge = a.x1 + 1 + p.Radius - 0.05f, far = b.x0 - p.Radius + 0.3f;
                Go = p.X <= edge && p.X + p.VX * air * 0.98f >= far && MathF.Abs(MathF.Atan2(p.VY, p.VX)) < 0.35f;
                return Hop(g);
            }
            _inAir = true;
            float line = MathF.Atan2(5.5f - p.Y, 8f);
            return _flight.Toward(g, Go ? 0 : line + _sign * Width, dt);
        }
    }
}
