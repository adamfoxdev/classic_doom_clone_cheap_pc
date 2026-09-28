namespace HexenSharp;

/// <summary>
/// The grenade launcher, Quake's: a grenade lobbed along your view with a kick upward, falling under gravity and
/// bouncing off walls and floors, losing speed each time, until it goes off: after two and a half seconds, or at once
/// when it touches a monster. Its blast is the rocket's (up to 120, falling off to nothing at 3 cells, half to
/// yourself and the full push), so you can grenade jump: stand by one as it goes off and jump. It lets you look right
/// down like the rocket launcher, and a ring shows where the grenade will first land. On the range's rack (key =),
/// on the grenade course, or 'give grenadelauncher'.
/// </summary>
public static class Grenades
{
    /// <summary>Quake's 600 units a second along your aim plus 200 up, and its gravity of 800, in cells.</summary>
    public const float Speed = 6.7f, Up = 2.25f, Gravity = 9f;
    /// <summary>Seconds to the bang.</summary>
    public const float Fuse = 2.5f;
    /// <summary>Of its speed into a surface, what bounces back; of its speed along the floor, what's kept at a bounce.</summary>
    public const float Bounce = 0.45f, Scrub = 0.5f;
    /// <summary>Rolling along the floor, it slows by this many cells a second, each second.</summary>
    public const float Roll = 8f;

    public static readonly WeaponDef Launcher = new()
    {
        Name = "Grenade Launcher", Proj = ProjKind.Grenade, DmgMin = 100, DmgMax = 120, Cooldown = 0.6f, Mana = 2, Cost = 2,
        Speed = Speed, Splash = Rockets.SplashRadius, Grenade = true, ArtIndex = Art.GrenadeLauncherArt, Sound = Sfx.Push,
    };

    /// <summary>A grenade's first flight from a launch (x along your aim, z up), to where it first comes down on flat floor at `floor`.</summary>
    public static float FirstLanding(float z0, float angle, float floor)
    {
        float vx = MathF.Cos(angle) * Speed, vz = MathF.Sin(angle) * Speed + Up;
        // z0 + vz t - g t^2 / 2 = floor
        float a = -Gravity / 2, b = vz, c = z0 - floor;
        float t = (-b - MathF.Sqrt(MathF.Max(0, b * b - 4 * a * c))) / (2 * a);
        return 0.2f + vx * t;
    }
}

public sealed partial class Game
{
    /// <summary>A grenade's launch angle: along your view, and like a rocket, steeper at the bottom of the tilt (to drop one at your feet).</summary>
    public float GrenadeAim() => Rockets.AimAngle(P.Pitch, 160f / MathF.Tan(Vars.Fov * MathF.PI / 360f));

    /// <summary>Lobs a grenade along your view, with Quake's kick upward, on a two and a half second fuse.</summary>
    void FireGrenade(WeaponDef w, float launchZ)
    {
        var p = P;
        float a = GrenadeAim(), flat = MathF.Cos(a) * Grenades.Speed;
        var pr = new Projectile
        {
            Kind = ProjKind.Grenade, FromPlayer = true, Splash = Rockets.SplashRadius, Slot = p.Weapon,
            DmgMin = w.DmgMin, DmgMax = w.DmgMax,
            X = p.X + MathF.Cos(p.Angle) * 0.2f, Y = p.Y + MathF.Sin(p.Angle) * 0.2f, Z = launchZ,
            VX = MathF.Cos(p.Angle) * flat, VY = MathF.Sin(p.Angle) * flat, VZ = MathF.Sin(a) * Grenades.Speed + Grenades.Up,
            Level = Level, SpriteW = 0.2f, SpriteH = 0.2f, Life = Grenades.Fuse, Radius = 0.08f,
        };
        GrenadesThrown++;
        Level.Things.Add(pr);
    }

    /// <summary>Grenades lobbed this game.</summary>
    public int GrenadesThrown;

    /// <summary>A grenade in flight or rolling: gravity, bounces off walls, floors and ceilings, and the bang.</summary>
    void UpdateGrenade(Projectile pr, float dt)
    {
        if (pr.Life <= 0) { Explode(pr, null); return; } // (UpdateProjectile counts the fuse down)
        int steps = Math.Max(1, (int)(MathF.Sqrt(pr.VX * pr.VX + pr.VY * pr.VY + pr.VZ * pr.VZ) * dt / 0.05f) + 1);
        float h = dt / steps;
        for (int s = 0; s < steps; s++)
        {
            float floor = Level.FloorAt(pr.X, pr.Y);
            bool resting = pr.Z <= floor + 0.001f && pr.VZ == 0;
            if (!resting) pr.VZ -= Grenades.Gravity * h;
            else
            {
                // rolling: it slows to a stop
                float sp = MathF.Sqrt(pr.VX * pr.VX + pr.VY * pr.VY);
                float keep = sp > 0 ? MathF.Max(0, sp - Grenades.Roll * h) / sp : 0;
                pr.VX *= keep; pr.VY *= keep;
            }
            // across: a wall, or the face of a ledge higher than it is, turns it back
            float nx = pr.X + pr.VX * h, ny = pr.Y + pr.VY * h;
            if (Level.BlocksPoint(nx, pr.Y) || Level.FloorAt(nx, pr.Y) > pr.Z + 0.02f) { pr.VX = -pr.VX * Grenades.Bounce; Clink(pr); }
            else pr.X = nx;
            if (Level.BlocksPoint(pr.X, ny) || Level.FloorAt(pr.X, ny) > pr.Z + 0.02f) { pr.VY = -pr.VY * Grenades.Bounce; Clink(pr); }
            else pr.Y = ny;
            // up and down: the floor and the ceiling
            pr.Z += pr.VZ * h;
            floor = Level.FloorAt(pr.X, pr.Y);
            if (pr.Z <= floor)
            {
                pr.Z = floor;
                if (pr.VZ < -1.2f) { pr.VZ = -pr.VZ * Grenades.Bounce; pr.VX *= Grenades.Scrub; pr.VY *= Grenades.Scrub; Clink(pr); }
                else pr.VZ = 0;
            }
            float ceiling = Level.HeightAt(pr.X, pr.Y) - 0.1f;
            if (pr.Z > ceiling) { pr.Z = ceiling; if (pr.VZ > 0) pr.VZ = -pr.VZ * Grenades.Bounce; }
            // a monster it touches sets it off
            foreach (var t in Level.Things)
                if (t is Monster m && m.Alive && !m.Blurring && Dist(m.X, m.Y, pr.X, pr.Y) < m.Radius + pr.Radius)
                {
                    float foot = Level.FloorAt(m.X, m.Y) + m.Z;
                    if (pr.Z >= foot - 0.05f && pr.Z <= foot + m.SpriteH) { Explode(pr, null); return; }
                }
        }
        // a wisp of smoke as it flies
        if ((int)(pr.Life * 12) != (int)((pr.Life + dt) * 12) && pr.VZ != 0)
            Level.Things.Add(new Puff(Art.Smoke, 0.07f, 0.35f, 0.2f) { X = pr.X, Y = pr.Y, Z = pr.Z, Level = Level, FullBright = false });
    }

    float _clinkCd;
    void Clink(Projectile pr)
    {
        if (Time - _clinkCd < 0.08f) return;
        _clinkCd = Time;
        Sound(Sfx.Hit, pr.X, pr.Y);
    }

    /// <summary>Where a grenade lobbed now first comes down (its distance ahead of you, and the floor there), for the marker.</summary>
    (float dist, float z)? GrenadeLanding()
    {
        var p = P;
        float a = GrenadeAim(), ca = MathF.Cos(p.Angle), sa = MathF.Sin(p.Angle);
        float vx = MathF.Cos(a) * Grenades.Speed, vz = MathF.Sin(a) * Grenades.Speed + Grenades.Up;
        float d = 0.2f, z = p.FloorZ + p.Z + 0.32f;
        const float h = 0.01f;
        for (int k = 0; k < 600; k++)
        {
            vz -= Grenades.Gravity * h;
            d += vx * h; z += vz * h;
            float x = p.X + ca * d, y = p.Y + sa * d;
            if (Level.BlocksPoint(x, y)) return null;
            float floor = Level.FloorAt(x, y);
            if (z <= floor) return (d, floor);
        }
        return null;
    }

    // ------------------------------------------------------------------ the grenade course

    /// <summary>On the grenade course: how many of its targets are still standing (0 anywhere else).</summary>
    public int CourseTargetsLeft => Practicing && Course.Grenades && Level != null
        ? Level.Things.Count(t => t is Monster { Target: not null } m && m.Alive) : 0;

    /// <summary>On a fresh grenade course: the grenade launcher alone in hand, and its targets.</summary>
    void SetUpGrenadeCourse()
    {
        var p = P;
        p.Loadout = new[] { Grenades.Launcher };
        p.HasWeapon = new[] { true };
        p.Weapon = 0; p.PendingWeapon = -1; p.Raise = 0;
        p.BlueMana = p.GreenMana = 200;
        _unhurt = 0; _regen = 0;
        foreach (var (x, y) in GrenadeCourse.Targets)
        {
            var m = new Monster(ShootingRange.Dummy) { X = x, Y = y, Level = Level, State = AiState.Idle };
            m.Target = new RangeTarget { HomeX = x, HomeY = y, Kind = ShootingRange.Kind.Still };
            Level.Things.Add(m);
        }
    }

    /// <summary>Starting the run over (Restart, the demo): every target back up, and no grenades left lying about.</summary>
    void ResetCourseTargets()
    {
        Level.Things.RemoveAll(t => t is Projectile { Kind: ProjKind.Grenade });
        foreach (var m in Level.Things.OfType<Monster>().Where(m => m.Target != null && !m.Alive).ToList()) StandUp(m);
    }

    void GrenadeTargetDown()
    {
        int left = CourseTargetsLeft;
        PlaySound(Sfx.Secret, 0.7f);
        Say(left > 0 ? $"Target down! {left} to go." : "Every target down! The exit is open: get up to it.");
    }
}

/// <summary>
/// The grenade course (Practice > Grenades): three targets to knock down, and an exit that only opens when they're
/// all down, up on a high ledge. The first hides behind a wall too high to see over, so you lob a grenade over it;
/// the second stands up on a ledge, so you lob one up onto it; the third is on the high ledge by the exit. The ledges
/// are too high to jump, so you grenade jump up them: stand with a grenade at your feet and jump as it goes off. Timed,
/// with medals, a board, a ghost and a demo. The grenade launcher is your only weapon, your grenades can't kill you,
/// your health comes back and mana never runs out.
/// </summary>
public static class GrenadeCourse
{
    public const int W = 44, H = 11;
    /// <summary>The low wall across the yard (x), too high to jump or see over, with a way round it at the north end.</summary>
    public const int WallX = 10;
    public const float WallTop = 1f;
    /// <summary>The first ledge (from x) and the high ledge with the exit (from x), and their floors.</summary>
    public const int Ledge1X = 20, Ledge2X = 32;
    public const float Ledge1 = 2f, Ledge2 = 4f;

    public static readonly (float x, float y)[] Targets = { (14.5f, 5.5f), (Ledge1X + 1.4f, 5.5f), (Ledge2X + 1.4f, 5.5f) };

    public const string About = "LOB GRENADES OVER WALLS AND UP ONTO LEDGES AT THREE TARGETS, THEN GRENADE JUMP UP TO THE EXIT.";
    const string Intro = "Grenades: knock down all three targets. Lob one over the wall; grenade jump up the ledges (jump as it goes off).";

    public static readonly Course Course = new("grenades", "Grenades", About, Map, true, Intro, Grenades: true, Route: 110); // the demo earns silver; gold wants a cleaner run

    /// <summary>
    /// The course: a yard, the low wall across it (a gap at the north end), then the first ledge (2 up) and the high
    /// ledge (2 more), the exit at the far end. A checkpoint on each level.
    /// </summary>
    public static MapDef Map()
    {
        var rows = new char[H][];
        for (int y = 0; y < H; y++)
        {
            rows[y] = new char[W];
            for (int x = 0; x < W; x++) rows[y][x] = x == 0 || y == 0 || x == W - 1 || y == H - 1 ? 'O' : '.';
        }
        foreach (var (x, y) in new[] { (1, 1), (1, 9), (19, 1), (19, 9), (31, 1), (31, 9), (42, 1), (42, 9) }) rows[y][x] = 't';
        rows[5][3] = '@';
        rows[8][2] = '+';
        rows[8][Ledge1X + 1] = '+';
        rows[8][Ledge2X + 1] = '+';
        for (int y = 4; y <= 6; y++) rows[y][W - 3] = 'E';
        var def = new MapDef("Grenades", "The grenade course. Every target down, then up to the exit.", "hall",
            rows.Select(r => new string(r)).ToArray(), Height: 8f);
        return Maps.Elevate(def,
            (WallX, 3, WallX, 9, Maps.FloorGlyph(WallTop)),
            (Ledge1X, 1, Ledge2X - 1, 9, Maps.FloorGlyph(Ledge1)),
            (Ledge2X, 1, W - 2, 9, Maps.FloorGlyph(Ledge2)));
    }
}

/// <summary>
/// The grenade course's demo pilot, step by step: from a spot short of each target it picks the pitch whose lob comes
/// down on it (checking the arc clears whatever's in the way), fires, and waits to see it fall, trying again if not.
/// To get up a ledge it stands at the foot of it facing back the way it came, drops a grenade at its feet, and jumps
/// just as it goes off, holding S to carry on up and over the lip.
/// </summary>
static class GrenadePilot
{
    // what it does, in order: go to a spot (facing the way it should), lob at a target, or grenade jump up a ledge
    enum Kind { Go, Lob, Jump }
    static readonly (Kind kind, float x, float y, int target, float floor)[] Plan =
    {
        (Kind.Go, 7.5f, 5.5f, -1, 0),
        (Kind.Lob, 7.5f, 5.5f, 0, 0),
        (Kind.Go, 8.5f, 1.8f, -1, 0),
        (Kind.Go, 12.5f, 1.8f, -1, 0),
        (Kind.Go, 15.5f, 5.5f, -1, 0),
        (Kind.Lob, 15.5f, 5.5f, 1, 0),
        (Kind.Jump, JumpFrom, 5.5f, -1, GrenadeCourse.Ledge1),
        (Kind.Go, 27.5f, 5.5f, -1, GrenadeCourse.Ledge1),
        (Kind.Lob, 27.5f, 5.5f, 2, GrenadeCourse.Ledge1),
        (Kind.Jump, JumpFrom + GrenadeCourse.Ledge2X - GrenadeCourse.Ledge1X, 5.5f, -1, GrenadeCourse.Ledge2),
        (Kind.Go, 41.5f, 5.5f, -1, GrenadeCourse.Ledge2),
    };

    /// <summary>Where it drops the grenade for a jump (this far short of the ledge), and how far in front of it it stands.</summary>
    const float JumpFrom = GrenadeCourse.Ledge1X - 3.5f, StandOff = 0.45f;

    public static Input Next(Game g, DemoPilot pilot)
    {
        var p = g.P;
        float sens = 0.0025f * g.Vars.Sens;
        if (pilot.GrenadeStep >= Plan.Length) return new Input { Move = 1 };
        var (kind, x, y, target, floor) = Plan[pilot.GrenadeStep];
        pilot.GrenadeWait += DemoPilot.Tick;
        Input Look(float angle, float pitch) => new() { LookX = Wrap(angle - p.Angle) / sens, LookY = (p.Pitch - pitch) / (0.35f * g.Vars.Sens) };
        void Next() { pilot.GrenadeStep++; pilot.GrenadeWait = 0; pilot.LobSpot = null; }

        switch (kind)
        {
            case Kind.Go:
            {
                float dx = x - p.X, dy = y - p.Y, d = MathF.Sqrt(dx * dx + dy * dy);
                if (d < 0.15f && p.OnGround) { Next(); return new Input(); }
                pilot.Say("GO: on to the next spot");
                var inp = Look(MathF.Atan2(dy, dx), 0);
                inp.Move = 1; inp.Walk = d < 0.8f;
                return inp;
            }
            case Kind.Lob:
            {
                var t = g.Level.Things.OfType<Monster>().Where(m => m.Target != null).ElementAtOrDefault(target);
                if (t == null || !t.Alive) { Next(); return new Input(); }
                bool inFlight = g.Level.Things.Any(o => o is Projectile { Kind: ProjKind.Grenade, Removed: false });
                // somewhere on the line to the target, near where it stands now, that a lob lands from
                pilot.LobSpot ??= LobSpot(g, t);
                var (sx, sy) = pilot.LobSpot ?? (p.X, p.Y);
                float gx = sx - p.X, gy = sy - p.Y;
                if (gx * gx + gy * gy > 0.12f * 0.12f)
                {
                    pilot.Say("SPOT: find where a lob can reach it, over what's in the way");
                    var walk = Look(MathF.Atan2(gy, gx), 0);
                    walk.Move = inFlight ? 0 : 1; walk.Walk = true;
                    return walk;
                }
                float tx = t.X - p.X, ty = t.Y - p.Y, dist = MathF.Sqrt(tx * tx + ty * ty);
                if (LobPitch(g, p.X, p.Y, p.FloorZ, dist, g.Level.FloorAt(t.X, t.Y), t) is not { } pitch) { pilot.LobSpot = null; return new Input(); }
                pilot.Say("LOB: aim high, and the grenade arcs over onto the target");
                var inp = Look(MathF.Atan2(ty, tx), pitch);
                // fire once lined up; if it misses, try again when that grenade's done
                bool ready = MathF.Abs(p.Pitch - pitch) < 0.5f && MathF.Abs(Wrap(MathF.Atan2(ty, tx) - p.Angle)) < 0.01f;
                inp.Fire = ready && !inFlight && p.Cooldown <= 0;
                return inp;
            }
            default:
            {
                // grenade jump: facing back the way it came (west), looking down at its feet
                bool onTop = p.OnGround && MathF.Abs(p.FloorZ - floor) < 0.01f;
                if (onTop) { Next(); return new Input(); }
                var grenade = g.Level.Things.OfType<Projectile>().FirstOrDefault(o => o.Kind == ProjKind.Grenade && !o.Removed);
                var inp = Look(MathF.PI, -Rockets.LookDown);
                if (!p.OnGround)
                {
                    pilot.Say("FLY: hold S to carry on up over the lip");
                    inp.Move = -1;
                    return inp;
                }
                // facing west, Move -1 walks east (toward the ledge) and +1 west
                float Toward(float to) => MathF.Abs(p.X - to) < 0.06f ? 0 : p.X < to ? -1 : 1;
                if (grenade == null)
                {
                    // a run-up short of the ledge: drop one at your feet
                    inp.Move = Toward(x); inp.Walk = true;
                    pilot.Say("DROP: a few steps short of the ledge, look right down and fire a grenade at your feet");
                    inp.Fire = inp.Move == 0 && p.Cooldown <= 0 && p.Pitch <= -Rockets.LookDown + 1;
                    return inp;
                }
                bool rolling = grenade.VZ != 0 || MathF.Abs(grenade.VX) + MathF.Abs(grenade.VY) > 0.05f;
                pilot.Say(rolling ? "WAIT: let it come to rest" : "STAND: just in front of it, then jump the moment it goes off");
                if (!rolling) { inp.Move = Toward(grenade.X + StandOff); inp.Walk = MathF.Abs(p.X - grenade.X - StandOff) < 0.5f; }
                if (grenade.Life < 0.06f) { inp.Jump = true; inp.Move = -1; inp.Walk = false; }
                return inp;
            }
        }
    }

    /// <summary>
    /// The pitch (pixels) whose lob comes down on a target `dist` away standing on `floor`: the lowest one whose arc
    /// reaches it at body height, clearing the low wall or a ledge's lip on the way.
    /// </summary>
    static float? LobPitch(Game g, float px, float py, float pz, float dist, float floor, Monster t)
    {
        float proj = 160f / MathF.Tan(g.Vars.Fov * MathF.PI / 360f);
        float ca = MathF.Cos(MathF.Atan2(t.Y - py, t.X - px)), sa = MathF.Sin(MathF.Atan2(t.Y - py, t.X - px));
        for (float pitch = -20; pitch <= Rockets.LookDown; pitch += 0.5f)
        {
            float a = MathF.Atan(pitch / proj), vx = MathF.Cos(a) * Grenades.Speed, vz = MathF.Sin(a) * Grenades.Speed + Grenades.Up;
            float d = 0.2f, z = pz + 0.32f;
            bool ok = false;
            for (int k = 0; k < 400; k++)
            {
                const float h = 0.008f;
                vz -= Grenades.Gravity * h; d += vx * h; z += vz * h;
                float x = px + ca * d, y = py + sa * d;
                if (g.Level.BlocksPoint(x, y) || z < g.Level.FloorAt(x, y)) break;
                if (d >= dist - t.Radius * 0.5f) { ok = z >= floor + 0.05f && z <= floor + t.SpriteH - 0.1f; break; }
            }
            if (ok) return pitch;
        }
        return null;
    }

    /// <summary>The nearest spot to stand, on the line from the target back toward you (on your own level), that a lob reaches it from.</summary>
    static (float x, float y)? LobSpot(Game g, Monster t)
    {
        var p = g.P;
        float dx = p.X - t.X, dy = p.Y - t.Y, len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f) return null;
        dx /= len; dy /= len;
        (float x, float y)? best = null;
        float bestGap = float.MaxValue, floor = g.Level.FloorAt(t.X, t.Y);
        for (float back = 1.5f; back <= 7f; back += 0.25f)
        {
            float x = t.X + dx * back, y = t.Y + dy * back;
            if (g.Level.BlocksCircle(x, y, p.Radius) || MathF.Abs(g.Level.FloorAt(x, y) - p.FloorZ) > 0.01f) continue;
            if (LobPitch(g, x, y, p.FloorZ, back, floor, t) == null) continue;
            float gap = MathF.Abs(back - len);
            if (gap < bestGap) (best, bestGap) = ((x, y), gap);
        }
        return best;
    }

    static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
}
