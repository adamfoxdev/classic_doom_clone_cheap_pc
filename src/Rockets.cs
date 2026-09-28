namespace HexenSharp;

/// <summary>
/// The rocket launcher, made to work like Quake's. A rocket flies straight wherever you aim it, fast (about three
/// times your run speed). A direct hit does 100-120; the blast does up to 120 more to everything around it, falling
/// off with distance in 3D to nothing at 3 cells, and it catches you too: half the damage, but the full push. The push
/// (Quake's knockback) is along the line from the blast to you, as strong as the blast, so a rocket at your feet
/// throws you up and away: a rocket jump. Jump first and fire at the floor behind you to go highest and farthest.
/// It's in the shooting range; elsewhere, 'give rocketlauncher'.
/// </summary>
public static class Rockets
{
    /// <summary>The blast's damage at its centre, and the distance (cells) at which it falls to nothing.</summary>
    public const float SplashMax = 120f, SplashRadius = 3f;
    /// <summary>The share of the blast you take from your own rockets.</summary>
    public const float SelfShare = 0.5f;
    /// <summary>How hard a blast pushes: velocity (cells a second) per point of blast damage (Quake's 8 units a second a point, in cells), on you and on monsters.</summary>
    public const float Knock = 0.09f, MonsterKnock = 0.04f;
    /// <summary>
    /// With the launcher in hand you can look further down (LookDown pixels of tilt rather than the usual 70), down to
    /// the floor at your feet. The rocket goes where the middle of the view points, until the last stretch of the
    /// tilt: from SteepFrom pixels it swings on steeper than the view, to SteepAim degrees at the end, nearly straight
    /// down. The floor marker shows where it will land.
    /// </summary>
    public const float SteepFrom = 110f, SteepAim = 85f, LookDown = 160f;
    /// <summary>The usual limit on looking up and down, in pixels of tilt.</summary>
    public const float NormalPitch = 70f;

    public static readonly WeaponDef Launcher = new()
    {
        Name = "Rocket Launcher", Proj = ProjKind.Rocket, DmgMin = 100, DmgMax = 120, Cooldown = 0.8f, Mana = 2, Cost = 2,
        Speed = 12.5f, Splash = SplashRadius, Rocket = true, ArtIndex = 9, Sound = Sfx.Explode,
    };

    /// <summary>Every weapon in the game: each class's three, then the rocket launcher and the railgun.</summary>
    public static WeaponDef[] AllWeapons() => ClassDef.All.SelectMany(c => c.Weapons).Append(Launcher).Append(Railgun.Gun).ToArray();

    /// <summary>The rocket's climb angle (radians, up positive) for a view pitch, with `proj` the view's projection distance.</summary>
    public static float AimAngle(float pitch, float proj)
    {
        if (pitch >= -SteepFrom) return MathF.Atan(pitch / proj);
        float from = MathF.Atan(SteepFrom / proj), to = SteepAim * MathF.PI / 180f;
        return -(from + (to - from) * Math.Clamp((-pitch - SteepFrom) / (LookDown - SteepFrom), 0f, 1f));
    }

    /// <summary>A blast's damage at a distance from its centre (0 past the radius).</summary>
    public static float Points(float dist) => MathF.Max(0, SplashMax * (1 - dist / SplashRadius));
}

public sealed partial class Game
{
    /// <summary>A rocket's blast: damage falling off in 3D to everything near, your own share halved, and the push.</summary>
    void RocketBlast(Projectile pr, Monster direct)
    {
        int slot = pr.FromPlayer ? pr.Slot : -1;
        SpawnPuff(Art.Fireball[1], pr.X, pr.Y, pr.Z, 1.4f);
        AddShake(0.25f);
        foreach (var t in Level.Things.ToList())
        {
            if (t is not Monster m || !m.Alive || m == direct) continue;
            float mz = Level.FloorAt(m.X, m.Y) + m.Z + m.SpriteH * 0.5f;
            float dx = m.X - pr.X, dy = m.Y - pr.Y, dz = mz - pr.Z, d = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            float pts = Rockets.Points(d);
            if (pts <= 0 || !Level.Sight(pr.X, pr.Y, m.X, m.Y)) continue;
            Push(m, dx, dy, d, pts);
            DamageMonster(m, (int)pts, slot);
        }
        if (direct != null && direct.Alive) Push(direct, direct.X - pr.X, direct.Y - pr.Y, Dist(direct.X, direct.Y, pr.X, pr.Y), Rockets.SplashMax);
        // you: the push in full, the damage halved (and only your own rockets reach you like this; a monster's hits you as a projectile)
        if (!pr.FromPlayer || Mode != GameMode.Playing) return;
        var p = P;
        float pz = p.FloorZ + p.Z + Player.Height * 0.5f;
        float ex = p.X - pr.X, ey = p.Y - pr.Y, ez = pz - pr.Z, dist = MathF.Sqrt(ex * ex + ey * ey + ez * ez);
        float points = Rockets.Points(dist);
        if (points <= 0 || !Level.Sight(pr.X, pr.Y, p.X, p.Y)) return;
        if (dist < 0.01f) { ex = 0; ey = 0; ez = 1; dist = 1; } // right on top of it: straight up
        float k = points * Rockets.Knock / dist;
        p.VX += ex * k; p.VY += ey * k;
        if (ez * k > 0)
        {
            p.VZ = MathF.Max(p.VZ, 0) + ez * k;
            p.Z = MathF.Max(p.Z, 0.001f); // off the ground: now it's a flight
        }
        p.Boost = MathF.Max(p.Boost, p.HSpeed);
        RocketJumps++;
        int hurt = (int)(points * Rockets.SelfShare);
        if (SafeRockets) hurt = Math.Min(hurt, p.Health - 1); // practice never kills you: jump all you like
        if (hurt > 0) DamagePlayer(hurt);
    }

    /// <summary>Rockets thrown at your feet this game (a blast that pushed you), for the range's count.</summary>
    public int RocketJumps;

    /// <summary>A monster, pushed by a blast: it slides away across the floor, the push dying off quickly.</summary>
    static void Push(Monster m, float dx, float dy, float d, float points)
    {
        if (d < 0.01f) return;
        float k = points * Rockets.MonsterKnock / d;
        m.KnockX += dx * k; m.KnockY += dy * k;
    }

    /// <summary>A monster sliding from a push: it moves where it can, and the push fades over a fraction of a second.</summary>
    void KnockTick(Monster m, float dt)
    {
        if (m.KnockX == 0 && m.KnockY == 0) return;
        float sx = m.KnockX * dt, sy = m.KnockY * dt;
        if (!Blocked(m.X + sx, m.Y, m.Radius, m)) m.X += sx; else m.KnockX = 0;
        if (!Blocked(m.X, m.Y + sy, m.Radius, m)) m.Y += sy; else m.KnockY = 0;
        float keep = MathF.Exp(-8 * dt);
        m.KnockX *= keep; m.KnockY *= keep;
        if (MathF.Abs(m.KnockX) + MathF.Abs(m.KnockY) < 0.05f) m.KnockX = m.KnockY = 0;
    }
}

public sealed partial class Game
{
    /// <summary>
    /// Fires a rocket straight along your view (steeper than the view near the ends of its tilt, so you can put one at
    /// your feet). Looking level-ish at a monster above or below, it still aims at it, as other shots do.
    /// </summary>
    void FireRocket(WeaponDef w, float launchZ, float mult)
    {
        var p = P;
        float proj = 160f / MathF.Tan(Vars.Fov * MathF.PI / 360f);
        float climb = Rockets.AimAngle(p.Pitch, proj);
        if (MathF.Abs(p.Pitch) <= Rockets.NormalPitch && VerticalAim(launchZ, w.Speed) is { } vz) climb = MathF.Atan2(vz, w.Speed);
        float flat = MathF.Cos(climb) * w.Speed;
        var pr = new Projectile
        {
            Kind = ProjKind.Rocket, FromPlayer = true, Splash = Rockets.SplashRadius, Slot = p.Weapon,
            DmgMin = (int)MathF.Round(w.DmgMin * mult), DmgMax = (int)MathF.Round(w.DmgMax * mult),
            X = p.X + MathF.Cos(p.Angle) * 0.2f, Y = p.Y + MathF.Sin(p.Angle) * 0.2f, Z = launchZ,
            VX = MathF.Cos(p.Angle) * flat, VY = MathF.Sin(p.Angle) * flat, VZ = MathF.Sin(climb) * w.Speed, Aimed = true,
            Level = Level, SpriteW = 0.28f, SpriteH = 0.28f, Life = 8f,
        };
        Level.Things.Add(pr);
    }

    /// <summary>The climb angle a rocket fired now would take (ignoring the aim at a monster).</summary>
    public float RocketAim() => Rockets.AimAngle(P.Pitch, 160f / MathF.Tan(Vars.Fov * MathF.PI / 360f));

    /// <summary>
    /// Where a rocket fired now would hit the floor, for the marker: its distance ahead of you and the floor's height
    /// there. Null when it wouldn't come down within 12 cells, or would hit a wall first.
    /// </summary>
    public (float dist, float z)? RocketLanding()
    {
        var p = P;
        float a = RocketAim();
        if (a >= 0) return null;
        float ca = MathF.Cos(p.Angle), sa = MathF.Sin(p.Angle), flat = MathF.Cos(a), down = MathF.Sin(a);
        float d = 0.2f, z = p.FloorZ + p.Z + 0.32f;
        for (int k = 0; k < 400 && d < 12f; k++)
        {
            const float step = 0.05f;
            d += flat * step; z += down * step;
            float x = p.X + ca * d, y = p.Y + sa * d;
            if (Level.BlocksPoint(x, y)) return null;
            float floor = Level.FloorAt(x, y);
            if (z <= floor) return (d, floor);
        }
        return null;
    }

    /// <summary>How far down you can look: further with the rocket launcher in hand, easing back when you put it away.</summary>
    void PitchLimit(Player p, float dt)
    {
        float low = p.CurWeapon.Rocket && p.PendingWeapon < 0 && !Level.Flight ? -Rockets.LookDown : -Rockets.NormalPitch;
        if (p.Pitch < low) p.Pitch = MathF.Min(low, p.Pitch + dt * 300f);
    }

    /// <summary>A rocket's smoke: a puff left behind every so often as it flies.</summary>
    void RocketTrail(Projectile pr, float dt)
    {
        if (pr.Kind != ProjKind.Rocket || pr.Life > 7.9f || (int)(pr.Life * 30) == (int)((pr.Life + dt) * 30)) return;
        Level.Things.Add(new Puff(Art.Smoke, 0.1f, 0.45f, 0.3f)
            { X = pr.X - pr.VX * 0.02f, Y = pr.Y - pr.VY * 0.02f, Z = pr.Z, Level = Level, FullBright = false, VZ = 0.3f });
    }
}

public sealed partial class Game
{
    /// <summary>Hands you the rocket launcher (on the key after your class's weapons) and a stock of mana for it.</summary>
    public void GiveRocketLauncher() => GiveExtra(Rockets.Launcher);

    /// <summary>Hands you one of the Quake weapons, on the next key after what you carry, and a stock of mana.</summary>
    public void GiveExtra(WeaponDef weapon)
    {
        var p = P;
        int i = p.Loadout == null ? -1 : Array.IndexOf(p.Loadout, weapon);
        if (i < 0)
        {
            p.Loadout = p.Weapons.Append(weapon).ToArray();
            i = p.Loadout.Length - 1;
            var had = p.HasWeapon;
            p.HasWeapon = new bool[p.Loadout.Length];
            had.CopyTo(p.HasWeapon, 0);
        }
        p.HasWeapon[i] = true;
        p.BlueMana = Math.Max(p.BlueMana, 200); p.GreenMana = Math.Max(p.GreenMana, 200);
        SelectWeapon(i);
    }
}
