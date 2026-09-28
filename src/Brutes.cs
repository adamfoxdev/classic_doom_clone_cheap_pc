namespace HexenSharp;

/// <summary>
/// Monsters that throw you about, and infighting. The Grenadier lobs Quake grenades that arc, bounce and blast you
/// across the room; the Juggernaut's punch does little damage but shoves you hard, up and away, off whatever ledge
/// you're on. And monsters' missiles hit other monsters now: one hit by another kind's turns on it until one of them
/// falls (as in Doom), so you can set them on each other.
/// </summary>
public static class Brutes
{
    public static readonly MonsterDef Grenadier = new()
    {
        Name = "Grenadier", Art = "grenadier", Health = 150, Speed = 1.4f, Radius = 0.33f, Width = 0.85f, Height = 0.95f,
        Missile = ProjKind.Grenade, AttackTime = 0.6f, Cooldown = 2.4f, PainChance = 0.25f, SightRange = 14f,
    };

    /// <summary>The Juggernaut: a slow, heavy thing whose punch shoves you (Shove cells a second, and a lift).</summary>
    public static readonly MonsterDef Juggernaut = new()
    {
        Name = "Juggernaut", Art = "juggernaut", Health = 240, Speed = 1.5f, Radius = 0.4f, Width = 1.1f, Height = 1.05f,
        MeleeRange = 1.1f, MeleeMin = 5, MeleeMax = 9, AttackTime = 0.55f, Cooldown = 1.1f, PainChance = 0.15f, Shove = 9f,
    };

    /// <summary>A monster grenade's blast: this share of a rocket's damage (the push is the full one).</summary>
    public const float GrenadeShare = 0.25f;
    public const float ShoveLift = 3.2f;

    /// <summary>Their pictures: an ettin in olive webbing, and a slaughtaur in dull steel.</summary>
    public static void BuildArt()
    {
        if (Art.Monsters.TryGetValue("ettin", out var ettin)) Art.Monsters["grenadier"] = ettin.Select(t => MiniBosses.Wash(t, 150, 175, 90)).ToArray();
        if (Art.Monsters.TryGetValue("slaughtaur", out var sl)) Art.Monsters["juggernaut"] = sl.Select(t => MiniBosses.Wash(t, 150, 165, 200)).ToArray();
    }
}

public sealed partial class Game
{
    /// <summary>Who a monster is going for: its enemy (another monster that hit it) while that lives, or you.</summary>
    (float x, float y, float z, float radius, Monster foe) TargetOf(Monster m)
    {
        if (m.Enemy is { Alive: true, Removed: false } e && e.Level == m.Level) return (e.X, e.Y, Level.FloorAt(e.X, e.Y) + e.Z, e.Radius, e);
        m.Enemy = null;
        return (P.X, P.Y, P.FloorZ + P.Z, P.Radius, null);
    }

    /// <summary>Hit by another kind of monster: it turns on the one that hit it (bosses and mini-bosses keep on you).</summary>
    void Provoke(Monster victim, Monster attacker)
    {
        if (attacker == null || victim == attacker || !victim.Alive || !attacker.Alive || victim.Def == attacker.Def) return;
        if (victim.Def.Boss || victim.Def.MiniBoss != null || victim.Target != null) return;
        victim.Enemy = attacker;
        if (victim.State == AiState.Idle) Wake(victim);
        Infights++;
    }

    /// <summary>Monsters set on each other this game.</summary>
    public int Infights;

    /// <summary>A monster's melee blow landing on its enemy (another monster) rather than on you.</summary>
    void MonsterMelee(Monster m, Monster foe)
    {
        Sound(Sfx.Swing, m.X, m.Y);
        DamageMonster(foe, (int)(Rand(m.Def.MeleeMin, m.Def.MeleeMax) * m.DamageMult));
        Provoke(foe, m);
        if (m.Def.Shove > 0) Push(foe, foe.X - m.X, foe.Y - m.Y, Dist(foe.X, foe.Y, m.X, m.Y), m.Def.Shove / Rockets.MonsterKnock);
    }

    /// <summary>The Juggernaut's shove: you're thrown away from it and up, hard enough to go over an edge.</summary>
    void ShovePlayer(Monster m)
    {
        var p = P;
        float dx = p.X - m.X, dy = p.Y - m.Y, d = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
        p.VX += dx / d * m.Def.Shove; p.VY += dy / d * m.Def.Shove;
        p.VZ = MathF.Max(p.VZ, Brutes.ShoveLift); p.Z = MathF.Max(p.Z, 0.001f);
        p.Boost = MathF.Max(p.Boost, p.HSpeed);
        AddShake(0.3f);
    }

    /// <summary>
    /// A Grenadier's throw: the grenade's launch angle to come down at the target (the flatter of the two arcs that
    /// reach), with its kick upward; null if it can't reach.
    /// </summary>
    static float? LobAngle(float dist, float dz)
    {
        for (float deg = -10; deg <= 70; deg += 1)
        {
            float a = deg * MathF.PI / 180, vx = MathF.Cos(a) * Grenades.Speed, vz = MathF.Sin(a) * Grenades.Speed + Grenades.Up;
            float t = dist / vx, z = vz * t - Grenades.Gravity * t * t / 2;
            if (MathF.Abs(z - dz) < 0.25f) return a;
        }
        return null;
    }

    /// <summary>Lobs a grenade at the monster's target: it arcs, bounces, and goes off on a fuse or on touching someone.</summary>
    void FireGrenadeAt(Monster m, float tx, float ty, float tz)
    {
        float launchZ = Level.FloorAt(m.X, m.Y) + m.Z + m.SpriteH * 0.6f;
        float dist = MathF.Max(0.5f, Dist(m.X, m.Y, tx, ty));
        float a = LobAngle(dist, tz + 0.3f - launchZ) ?? 0.6f, dir = MathF.Atan2(ty - m.Y, tx - m.X) + (RandF() - 0.5f) * 0.08f;
        float flat = MathF.Cos(a) * Grenades.Speed;
        Level.Things.Add(new Projectile
        {
            Kind = ProjKind.Grenade, FromPlayer = false, Owner = m, Splash = Rockets.SplashRadius, DmgMin = 100, DmgMax = 120,
            X = m.X + MathF.Cos(dir) * (m.Radius + 0.15f), Y = m.Y + MathF.Sin(dir) * (m.Radius + 0.15f), Z = launchZ,
            VX = MathF.Cos(dir) * flat, VY = MathF.Sin(dir) * flat, VZ = MathF.Sin(a) * Grenades.Speed + Grenades.Up,
            Level = Level, SpriteW = 0.2f, SpriteH = 0.2f, Life = Grenades.Fuse, Radius = 0.08f,
        });
        Sound(Sfx.Push, m.X, m.Y);
    }

    /// <summary>A monster's grenade going off: a quarter of the blast's damage to you, but all of its push.</summary>
    void MonsterBlastHitsPlayer(Projectile pr)
    {
        var p = P;
        if (Mode != GameMode.Playing) return;
        float pz = p.FloorZ + p.Z + Player.Height * 0.5f;
        float ex = p.X - pr.X, ey = p.Y - pr.Y, ez = pz - pr.Z, dist = MathF.Sqrt(ex * ex + ey * ey + ez * ez);
        float points = Rockets.Points(dist);
        if (points <= 0 || !Level.Sight(pr.X, pr.Y, p.X, p.Y)) return;
        if (dist < 0.01f) { ex = 0; ey = 0; ez = 1; dist = 1; }
        float k = points * Rockets.Knock / dist;
        p.VX += ex * k; p.VY += ey * k;
        if (ez * k > 0) { p.VZ = MathF.Max(p.VZ, 0) + ez * k; p.Z = MathF.Max(p.Z, 0.001f); }
        p.Boost = MathF.Max(p.Boost, p.HSpeed);
        float mult = pr.Owner is Monster o ? o.DamageMult : 1;
        EliteHit(pr.Owner as Monster, DamagePlayer((int)(points * Brutes.GrenadeShare * mult)));
    }
}
