namespace HexenSharp;

public enum Affix { None, Shielded, Splitting, Vampiric, Teleporting }

/// <summary>
/// Elites: in New Game+ some ordinary monsters come with a gift, shown by a coloured outline. They're a third tougher
/// and pay half as much again in experience. Shielded ones soak up damage with a shield before their health; splitting
/// ones burst into two smaller copies when they fall; vampiric ones heal by what they take from you; and teleporting
/// ones blink away, behind you, when hurt. Bosses, mini-bosses and a brood never are.
/// </summary>
public static class Elites
{
    public static readonly Affix[] All = { Affix.Shielded, Affix.Splitting, Affix.Vampiric, Affix.Teleporting };

    /// <summary>The chance an ordinary monster is an elite, at a New Game+ tier (none in a first campaign).</summary>
    public static float Chance(int tier) => tier <= 0 ? 0 : MathF.Min(0.4f, 0.1f + 0.08f * tier);

    public const float HealthMult = 1.3f, XpMult = 1.5f;
    /// <summary>A shield worth this share of its (elite) health.</summary>
    public const float ShieldShare = 0.5f;
    /// <summary>A splitter's two copies: this share of its health each, and this size.</summary>
    public const int SplitCount = 2;
    public const float SplitHealth = 0.35f, SplitSize = 0.7f;
    /// <summary>A vampire heals by this share of the damage it does you.</summary>
    public const float VampShare = 1f;
    /// <summary>A teleporter's chance to blink when hurt, the wait between blinks, and where it lands: this far from you.</summary>
    public const float BlinkChance = 0.35f, BlinkCooldown = 3f, BlinkNear = 3f, BlinkFar = 6f;

    public static string Name(Affix a) => a.ToString().ToUpperInvariant();

    public static string About(Affix a) => a switch
    {
        Affix.Shielded => "a shield soaks up damage before its health",
        Affix.Splitting => "it bursts into two smaller copies when it falls",
        Affix.Vampiric => "it heals by what it takes from you",
        Affix.Teleporting => "hurt it and it may blink away, behind you",
        _ => "",
    };

    public static uint Colour(Affix a) => a switch
    {
        Affix.Shielded => Col.Rgb(110, 180, 255),
        Affix.Splitting => Col.Rgb(120, 255, 120),
        Affix.Vampiric => Col.Rgb(235, 40, 60),
        Affix.Teleporting => Col.Rgb(200, 110, 255),
        _ => Col.Rgb(255, 255, 255),
    };

    public static bool Eligible(Monster m) => !m.Def.Boss && m.Def.MiniBoss == null && m.Summoner == null && !m.Split && m.Def.Special == Special.None;
}

public sealed partial class Game
{
    Random _eliteRng = new(7);
    readonly HashSet<Affix> _elitesMet = new();
    float _eliteScan;

    /// <summary>Makes a monster an elite with a gift: tougher, and a shield if it's shielded.</summary>
    public void MakeElite(Monster m, Affix a)
    {
        m.Affix = a;
        m.MaxHealth = (int)(m.MaxHealth * Elites.HealthMult);
        m.Health = m.MaxHealth;
        m.Shield = a == Affix.Shielded ? (int)(m.MaxHealth * Elites.ShieldShare) : 0;
    }

    /// <summary>Rolls elites among a map's ordinary monsters: for the New Game+ tier, or a custom map's own chance.</summary>
    void RollElites(Level lv, float? chanceOverride = null)
    {
        float chance = chanceOverride ?? Elites.Chance(NgTier);
        if (chance <= 0) return;
        foreach (var m in lv.Things.OfType<Monster>())
            if (Elites.Eligible(m) && _eliteRng.NextDouble() < chance) MakeElite(m, Elites.All[_eliteRng.Next(Elites.All.Length)]);
    }

    /// <summary>A shield takes what it can of a hit; what's left goes to the monster's health.</summary>
    int EliteShield(Monster m, int dmg)
    {
        if (m.Shield <= 0) return dmg;
        int soaked = Math.Min(m.Shield, dmg);
        m.Shield -= soaked;
        if (m.Shield <= 0)
        {
            SpawnPuff(Art.Bolt[1], m.X, m.Y, Level.FloorAt(m.X, m.Y) + m.Z + m.SpriteH * 0.5f, 0.6f);
            Sound(Sfx.Break, m.X, m.Y);
        }
        return dmg - soaked;
    }

    /// <summary>A teleporting elite, hurt: sometimes it blinks to a spot a few cells off, behind you if it can.</summary>
    void EliteHurt(Monster m)
    {
        if (m.Affix != Affix.Teleporting || !m.Alive || m.SpecialCd > 0 || _eliteRng.NextDouble() >= Elites.BlinkChance) return;
        var p = P;
        (float x, float y)? best = null;
        float bestScore = float.MaxValue;
        for (int k = 0; k < 24; k++)
        {
            float a = p.Angle + MathF.PI + (float)(_eliteRng.NextDouble() - 0.5) * 2.4f; // mostly behind you
            float d = Elites.BlinkNear + (float)_eliteRng.NextDouble() * (Elites.BlinkFar - Elites.BlinkNear);
            float x = p.X + MathF.Cos(a) * d, y = p.Y + MathF.Sin(a) * d;
            if (Blocked(x, y, m.Radius, m) || MathF.Abs(Level.FloorAt(x, y) - p.FloorZ) > 1f) continue;
            float score = MathF.Abs(AngleDiff(a, p.Angle + MathF.PI));
            if (score < bestScore) (best, bestScore) = ((x, y), score);
        }
        if (best is not { } spot) return;
        SpawnPuff(Art.BossBall[1], m.X, m.Y, Level.FloorAt(m.X, m.Y) + 0.5f, 0.6f);
        (m.X, m.Y) = spot;
        SpawnPuff(Art.BossBall[1], m.X, m.Y, Level.FloorAt(m.X, m.Y) + 0.5f, 0.6f);
        Sound(Sfx.Teleport, m.X, m.Y);
        m.SpecialCd = Elites.BlinkCooldown;
    }

    /// <summary>A splitting elite fell: two smaller copies burst out of it.</summary>
    void EliteDied(Monster m)
    {
        if (m.Affix != Affix.Splitting) return;
        for (int k = 0; k < Elites.SplitCount; k++)
        {
            float a = k * MathF.PI + MathF.Atan2(P.Y - m.Y, P.X - m.X) + MathF.PI / 2;
            float x = m.X + MathF.Cos(a) * 0.6f, y = m.Y + MathF.Sin(a) * 0.6f;
            if (Level.BlocksCircle(x, y, m.Radius * 0.8f)) (x, y) = (m.X, m.Y);
            var c = SpawnMonster(m.Def, x, y, 1, m.DamageMult, m.SpeedMult);
            c.MaxHealth = c.Health = Math.Max(1, (int)(m.MaxHealth * Elites.SplitHealth));
            MakeSplit(c);
        }
        Say($"The {m.Def.Name} splits in two!");
    }

    /// <summary>A splitter's copy: smaller, and never an elite itself.</summary>
    public static void MakeSplit(Monster c)
    {
        c.Split = true;
        c.SpriteW = c.Def.Width * Elites.SplitSize; c.SpriteH = c.Def.Height * Elites.SplitSize; c.Radius = c.Def.Radius * 0.8f;
    }

    /// <summary>A vampiric elite hurt you: it heals by as much (up to its full health).</summary>
    void EliteHit(Monster attacker, int dealt)
    {
        if (attacker is not { Affix: Affix.Vampiric } v || !v.Alive || dealt <= 0) return;
        v.Health = Math.Min(v.MaxHealth, v.Health + (int)(dealt * Elites.VampShare));
        SpawnPuff(Art.Fireball[1], v.X, v.Y, Level.FloorAt(v.X, v.Y) + v.Z + v.SpriteH, 0.3f);
    }

    /// <summary>The first time you meet each kind of elite, a word about what its outline means.</summary>
    void EliteTick(float dt)
    {
        if ((_eliteScan -= dt) > 0 || Level == null || P == null) return;
        _eliteScan = 0.5f;
        foreach (var t in Level.Things)
            if (t is Monster { Affix: not Affix.None } m && m.Alive && m.State != AiState.Idle && !_elitesMet.Contains(m.Affix) && Dist(m.X, m.Y, P.X, P.Y) < 14f)
            {
                _elitesMet.Add(m.Affix);
                Say($"A {Elites.Name(m.Affix).ToLowerInvariant()} elite: {Elites.About(m.Affix)}.");
                return;
            }
    }
}
