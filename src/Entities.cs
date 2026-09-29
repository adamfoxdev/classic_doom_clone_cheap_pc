namespace HexenSharp;

public enum Sfx
{
    Swing, Hit, Shoot, Magic, Explode, Sight, Death, Pickup, Item, Door, Lever,
    Pain, PlayerPain, PlayerDeath, Teleport, Locked, BossSight, Heal, Jump, Land, Slide, Chest, Push, Blur, Secret, Lore, Relic, JetStart, Jet, JetOut, Break, Count
}

public abstract class Thing
{
    public float X, Y, Z;
    public float Radius = 0.25f;
    public float SpriteW = 0.6f, SpriteH = 0.6f;
    public bool Solid, FullBright, Removed;
    public Level Level;
    /// <summary>Opacity 0..256; below 256 the sprite is drawn see-through.</summary>
    public virtual int Alpha => 256;
    public abstract Tex Sprite(float time);
}

// ---------------------------------------------------------------- practice ghost

/// <summary>
/// Your best practice run, replayed: a see-through figure that retraces the recorded path in time with your clock.
/// It has no body: nothing collides with it, shoots at it or picks it up. Its Z is an absolute height.
/// </summary>
public sealed class GhostRunner : Thing
{
    public readonly GhostTrack Track;
    public readonly float Time;
    /// <summary>How far through the run it is, in seconds of the run clock.</summary>
    public float At;
    public GhostRunner(GhostTrack track, float time)
    {
        Track = track; Time = time; SpriteW = 0.34f; SpriteH = 0.62f; FullBright = true; Radius = 0.2f;
        (X, Y, Z) = track.At(0);
    }
    public void Seek(float t) { At = t; (X, Y, Z) = Track.At(t); }
    public bool Finished => At >= Track.Duration;
    public override int Alpha => Finished ? 70 : 140;
    public override Tex Sprite(float time) => Art.Ghost[At > 0 && !Finished ? (int)(At * 7) % 2 : 0];
}

/// <summary>A co-op partner rendered in the world; the host's fixed input frames own its movement.</summary>
public sealed class NetworkAvatar : Thing
{
    public readonly string PlayerId;
    public Player Player;
    public NetworkAvatar(string playerId, Player player)
    {
        PlayerId = playerId; Player = player; SpriteW = 0.34f; SpriteH = Player.Height; Radius = Player.Radius; FullBright = true;
    }
    /// <summary>Its colour: a team's in a match, a crewmate's own in co-op (0 for the plain ghost).</summary>
    public uint Tint;
    public override int Alpha => Player.Dead ? 75 : 230;
    public override Tex Sprite(float time)
    {
        var frame = Art.Ghost[(int)(time * 7) % 2];
        if (Tint == 0) return frame;
        var key = (frame, Tint);
        if (!_tinted.TryGetValue(key, out var t)) _tinted[key] = t = Tinted(frame, Tint);
        return t;
    }
    static readonly Dictionary<(Tex, uint), Tex> _tinted = new();
    static Tex Tinted(Tex from, uint tint)
    {
        var t = new Tex(from.W, from.H);
        for (int i = 0; i < from.Px.Length; i++)
            t.Px[i] = Col.A(from.Px[i]) == 0 ? from.Px[i] : Col.Lerp(from.Px[i], tint, 150);
        return t;
    }
}

// ---------------------------------------------------------------- decorations

public sealed class Decor : Thing
{
    /// <summary>Map glyph of the decoration (t torch, p pillar, T tree); its art comes from the current style.</summary>
    public readonly char Kind;
    /// <summary>Pillars reach all the way up to their room's ceiling.</summary>
    public bool ReachCeiling;
    public Decor(char kind, float w, float h, bool solid, bool bright)
    {
        Kind = kind; SpriteW = w; SpriteH = h; Solid = solid; FullBright = bright; Radius = 0.3f;
    }
    public override Tex Sprite(float time)
    {
        var f = Art.DecorFrames(Kind);
        return f[(int)(time * 8) % f.Length];
    }
}

// ---------------------------------------------------------------- pickups

public enum PickupKind { Vial, Flask, Urn, BlueMana, GreenMana, SteelKey, FireKey, Armor, Weapon2, Weapon3, Relic, Jetpack, Upgrade, Mod, Arms, Ammo }

public sealed class Pickup : Thing
{
    public readonly PickupKind Kind;
    public string Name;                // relics have a name
    public readonly int Variant;       // which relic sprite
    public Pickup(PickupKind kind, float size = 0.4f, int variant = 0)
    {
        Kind = kind; Variant = variant; SpriteW = size; SpriteH = size;
        FullBright = kind is PickupKind.BlueMana or PickupKind.GreenMana or PickupKind.Weapon2 or PickupKind.Weapon3 or PickupKind.Relic or PickupKind.Upgrade or PickupKind.Mod or PickupKind.Arms or PickupKind.Ammo;
        if (kind == PickupKind.Arms) SpriteH = size * 0.62f; // a weapon lies long and low
    }
    public override Tex Sprite(float time) => Kind == PickupKind.Upgrade ? Art.Upgrade[(int)(time * 6) % Art.Upgrade.Length]
        : Kind == PickupKind.Mod ? WeaponMods.SpriteFrames(Variant)[(int)(time * 6) % Art.Upgrade.Length]
        : Art.PickupSprite(Kind, Variant);
}

// ---------------------------------------------------------------- monsters

public enum AiState { Idle, Chase, Attack, Pain, Dying, Dead, Wander }

public sealed class MonsterDef
{
    string _name;
    public string Name { get => Words.T(_name); init => _name = value; }
    public string Art;
    public int Health;
    public float Speed, Radius, Width, Height, FlyZ;
    public float MeleeRange;           // 0 = no melee
    public int MeleeMin, MeleeMax;
    public ProjKind? Missile;          // null = no ranged attack
    public int MissileCount = 1;
    public float MissileSpread;
    public float AttackTime = 0.5f, Cooldown = 1.5f, PainChance = 0.5f, SightRange = 14f;
    public bool Boss;
    public bool Blurs;                 // dodges by turning see-through and darting sideways
    /// <summary>A mini-boss's trick (see MiniBosses), its id for your profile, and how close it wakes without seeing you.</summary>
    public Special Special;
    public string MiniBoss;
    /// <summary>A melee blow that shoves you this hard (cells a second), up and away (the Juggernaut's).</summary>
    public float Shove;
    public float WakeRange;
}

public sealed class Monster : Thing
{
    public readonly MonsterDef Def;
    public int Health, MaxHealth;                   // MaxHealth: raised by New Game+, for the health bar
    public AiState State = AiState.Idle;
    public float StateTime, AttackCd, Anim, StuckTime, StrafeTime;
    public float StuckDX, StuckDY, StrafeSign = 1;
    public bool AttackFired;
    public float DamageMult = 1f, SpeedMult = 1f;   // raised by arena waves
    public float BlurTime, BlurDX, BlurDY;          // Dark Bishop dodge
    // mini-boss state: its trick's cooldown and timer, which step of it (wind-up, charge, stun), the charge's heading,
    // the health at which it next blinks away, and (for a brood) who summoned it
    public float SpecialCd, SpecialTime, DashX, DashY;
    public int SpecialPhase, NextBlinkHp;
    public Monster Summoner;
    /// <summary>A blast's push (cells a second), sliding it away and dying off (see Game.KnockTick).</summary>
    public float KnockX, KnockY;
    /// <summary>A shooting range dummy's place and kind (null for every real monster).</summary>
    public RangeTarget Target;
    /// <summary>Another monster it's fighting (it hit it with a missile or a blow), instead of you; null for you.</summary>
    public Monster Enemy;
    /// <summary>Out of reach: a Dark Bishop's blur, or the Rock Wyrm inside the rock (unseen as well).</summary>
    public bool Blurring => BlurTime > 0 || Burrowed;
    public bool Burrowed;
    /// <summary>Seconds left slowed by a Frost mod.</summary>
    public float SlowTime, SlowFactor = 0.5f;
    /// <summary>Seconds left frozen solid by a Deep Freeze: it can't move or attack.</summary>
    public float FrozenTime;
    /// <summary>An elite's gift (New Game+), what's left of a shielded one's shield, and whether it's a splitter's smaller copy.</summary>
    public Affix Affix;
    public int Shield;
    public bool Split;
    /// <summary>Its intro card has been shown (bosses only; see Game.CheckBossIntros).</summary>
    public bool Introduced;
    public override int Alpha => Burrowed ? 0 : BlurTime > 0 ? 90 : 256;

    public Monster(MonsterDef def)
    {
        Def = def; Health = MaxHealth = def.Health; Radius = def.Radius; SpriteW = def.Width; SpriteH = def.Height; Z = def.FlyZ;
        Solid = true;
    }

    public bool Alive => State != AiState.Dying && State != AiState.Dead;

    public override Tex Sprite(float time)
    {
        var f = Art.Monsters[Def.Art];
        return State switch
        {
            AiState.Attack => f[(int)Pose.Attack],
            AiState.Pain => f[(int)Pose.Pain],
            AiState.Dying => f[StateTime < 0.15f ? (int)Pose.Die0 : StateTime < 0.3f ? (int)Pose.Die1 : (int)Pose.Dead],
            AiState.Dead => f[(int)Pose.Dead],
            AiState.Chase or AiState.Wander => f[((int)(Anim * 4)) % 2],
            _ => f[((int)(time * 1.5f + X)) % 2],
        };
    }

    public static readonly MonsterDef Ettin = new()
    {
        Name = "Ettin", Art = "ettin", Health = 70, Speed = 1.7f, Radius = 0.32f, Width = 0.8f, Height = 0.85f,
        MeleeRange = 0.9f, MeleeMin = 8, MeleeMax = 18, AttackTime = 0.5f, Cooldown = 0.6f, PainChance = 0.5f,
    };
    public static readonly MonsterDef Afrit = new()
    {
        Name = "Afrit", Art = "afrit", Health = 35, Speed = 2.4f, Radius = 0.25f, Width = 0.55f, Height = 0.5f, FlyZ = 0.35f,
        Missile = ProjKind.Fireball, AttackTime = 0.45f, Cooldown = 1.4f, PainChance = 0.8f,
    };
    public static readonly MonsterDef Centaur = new()
    {
        Name = "Centaur", Art = "centaur", Health = 100, Speed = 2.0f, Radius = 0.35f, Width = 0.95f, Height = 0.9f,
        MeleeRange = 1.0f, MeleeMin = 12, MeleeMax = 22, AttackTime = 0.5f, Cooldown = 0.7f, PainChance = 0.3f,
    };
    public static readonly MonsterDef Slaughtaur = new()
    {
        Name = "Slaughtaur", Art = "slaughtaur", Health = 130, Speed = 1.9f, Radius = 0.35f, Width = 0.95f, Height = 0.9f,
        MeleeRange = 1.0f, MeleeMin = 14, MeleeMax = 24, Missile = ProjKind.CentaurBolt, MissileCount = 3, MissileSpread = 0.12f,
        AttackTime = 0.55f, Cooldown = 1.6f, PainChance = 0.25f,
    };
    public static readonly MonsterDef Bishop = new()
    {
        Name = "Dark Bishop", Art = "bishop", Health = 90, Speed = 2.1f, Radius = 0.28f, Width = 0.65f, Height = 0.75f, FlyZ = 0.2f,
        Missile = ProjKind.Seeker, MissileCount = 2, MissileSpread = 0.5f, AttackTime = 0.6f, Cooldown = 1.9f,
        PainChance = 0.3f, Blurs = true,
    };
    public static readonly MonsterDef Heresiarch = new()
    {
        Name = "Heresiarch", Art = "heresiarch", Health = 700, Speed = 1.2f, Radius = 0.4f, Width = 1.1f, Height = 1.0f,
        Missile = ProjKind.BossBall, MissileCount = 5, MissileSpread = 0.14f, AttackTime = 0.7f, Cooldown = 1.3f,
        PainChance = 0.08f, SightRange = 9f, Boss = true,
    };
}

// ---------------------------------------------------------------- projectiles

public enum ProjKind { Fireball, CentaurBolt, BossBall, Seeker, Bolt, Shard, Serpent, Flame, Lightning, Hammer, Rocket, Grenade }

public sealed class Projectile : Thing
{
    public ProjKind Kind;
    public float VX, VY, VZ;
    public int DmgMin, DmgMax;
    public float Splash;               // radius of splash damage, 0 = none
    public bool FromPlayer, Exploding;
    /// <summary>The player who fired it (online, whose share of its blast they take; others only get the push).</summary>
    public Player ByPlayer;
    /// <summary>Climbs or dives at VZ in a straight line (aimed up or down at a target) instead of levelling out.</summary>
    public bool Aimed;
    /// <summary>Which of your weapons fired it (-1 for monsters' shots), so its kills level that weapon up.</summary>
    public int Slot = -1;
    public float Homing;               // turn rate toward the player in radians/second (0 = flies straight)
    public float Life = 6f, ExplodeTime;
    /// <summary>A piercing shot: how many more monsters it goes through, and those it's already been through.</summary>
    public int Pierce;
    /// <summary>The charge behind the shot (1 for an ordinary one), for the charged mod combos.</summary>
    public float Power = 1;
    /// <summary>It struck a wall (or the face of a ledge) rather than a floor or ceiling: a wall kick, if it pushes you.</summary>
    public bool HitWall;
    public HashSet<Monster> Pierced;
    public Thing Owner;

    public Projectile() { Radius = 0.12f; FullBright = true; SpriteW = 0.3f; SpriteH = 0.3f; }

    public Tex[] Frames => Kind switch
    {
        ProjKind.Fireball => Art.Fireball,
        ProjKind.CentaurBolt => Art.CentaurBolt,
        ProjKind.BossBall => Art.BossBall,
        ProjKind.Seeker => Art.Seeker,
        ProjKind.Bolt => Art.Bolt,
        ProjKind.Shard => Art.Shard,
        ProjKind.Serpent => Art.Serpent,
        ProjKind.Flame => Art.Flame,
        ProjKind.Lightning => Art.Lightning,
        ProjKind.Rocket => Art.Rocket,
        ProjKind.Grenade => Art.Grenade,
        _ => Art.Hammer,
    };

    public override Tex Sprite(float time) => Frames[(int)(time * 12) % 2];
}

/// <summary>
/// A wrecked ship on a stranded map. Feed it the ore it needs (by Use) and it's repaired; Use it again to fly home.
/// </summary>
public sealed class Ship : Thing
{
    /// <summary>Ore needed, by Level.OreGlyphs index: iron, crystal, fuel.</summary>
    public static readonly int[] Need = { 6, 4, 3 };
    public readonly int[] Delivered = new int[Need.Length];
    public bool Built => Enumerable.Range(0, Need.Length).All(k => Delivered[k] >= Need[k]);
    /// <summary>0 wrecked, 1 half repaired, 2 ready to fly.</summary>
    public int Stage => Built ? 2 : Delivered.Sum() * 2 >= Need.Sum() ? 1 : 0;
    public Ship() { Solid = true; Radius = 0.55f; SpriteW = 1.7f; SpriteH = 1.2f; }
    public override Tex Sprite(float time) => Art.Ship[Stage];
}

/// <summary>
/// A drifting, spinning asteroid on a flight map. It bobs up and down across your path; ram it and your hull takes
/// the blow, shoot it enough and it shatters. Z is its lowest point above the (starfield) floor.
/// </summary>
public sealed class Asteroid : Thing
{
    public int Health = 40;
    public float Phase, Bob, Spin;
    public float BaseZ;
    public Asteroid(float size, float z, float phase)
    {
        SpriteW = SpriteH = size; Radius = size * 0.42f; BaseZ = Z = z; Phase = phase; Bob = 0.35f + phase % 0.4f; Spin = 3f + phase % 3f;
    }
    /// <summary>Height of its middle, for 3D hits.</summary>
    public float MidZ => Z + SpriteH * 0.5f;
    public override Tex Sprite(float time) => Art.Asteroid[(int)(time * Spin + Phase * 10) % Art.Asteroid.Length];
}

/// <summary>Short-lived visual effect (hit sparks, explosions, teleport flashes).</summary>
public sealed class Puff : Thing
{
    readonly Tex _tex;
    public float Life, MaxLife;
    public float Grow;
    /// <summary>Debris falls: upward speed, and how fast it drops (0 for puffs that just hang and fade).</summary>
    public float VZ, Gravity;
    readonly float _baseW;
    public Puff(Tex tex, float size, float life, float grow)
    {
        _tex = tex; SpriteW = SpriteH = _baseW = size; Life = MaxLife = life; Grow = grow; FullBright = true;
    }
    public void Tick(float dt)
    {
        Life -= dt;
        float s = _baseW * (1 + Grow * (1 - Life / MaxLife));
        Z -= (s - SpriteW) * 0.5f;
        SpriteW = SpriteH = s;
        if (Gravity > 0)
        {
            VZ -= Gravity * dt;
            Z = MathF.Max(Level?.FloorAt(X, Y) ?? 0f, Z + VZ * dt);
        }
        if (Life <= 0) Removed = true;
    }
    public override Tex Sprite(float time) => _tex;
}

public static class ThingFactory
{
    /// <summary>Asteroid size, height and spin all follow from where it sits, so a map always looks the same.</summary>
    static Asteroid MakeAsteroid(float x, float y)
    {
        uint h = (uint)((int)(x * 92821) ^ (int)(y * 68917)) * 2654435761u;
        float a = (h & 0xFFFF) / 65535f, b = (h >> 16) / 65535f;
        return new Asteroid(0.55f + a * 0.8f, 0.2f + b * 2.4f, a * 6.28f);
    }

    public static Thing Create(char c, float x, float y)
    {
        Thing t = c switch
        {
            'e' => new Monster(Monster.Ettin),
            'a' => new Monster(Monster.Afrit),
            'c' => new Monster(Monster.Centaur),
            'C' => new Monster(Monster.Slaughtaur),
            'H' => new Monster(Monster.Heresiarch),
            // the mini-bosses, for custom maps (the hub places its own; see MiniBosses.Places)
            'G' => MiniBosses.Create(MiniBosses.Warden),
            'R' => MiniBosses.Create(MiniBosses.Stalker),
            'Y' => MiniBosses.Create(MiniBosses.Thornmother),
            'o' => MiniBosses.Create(MiniBosses.Keeper),
            'y' => MiniBosses.Create(MiniBosses.Wyrm),
            'm' => new Pickup(PickupKind.Mod, 0.42f), // a weapon mod, which one rolled when the game starts
            'd' => new Monster(Monster.Bishop),
            'n' => new Monster(Brutes.Grenadier),
            'j' => new Monster(Brutes.Juggernaut),
            'i' => ShootingRange.MakeTarget(x, y), // a practice target (custom maps): it stands back up when knocked down
            'h' => new Pickup(PickupKind.Vial, 0.35f),
            'q' => new Pickup(PickupKind.Flask, 0.4f),
            'u' => new Pickup(PickupKind.Urn, 0.45f),
            'b' => new Pickup(PickupKind.BlueMana, 0.4f),
            'g' => new Pickup(PickupKind.GreenMana, 0.4f),
            'k' => new Pickup(PickupKind.SteelKey, 0.4f),
            'f' => new Pickup(PickupKind.FireKey, 0.4f),
            'r' => new Pickup(PickupKind.Armor, 0.5f),
            'w' => new Pickup(PickupKind.Weapon2, 0.5f),
            'x' => new Pickup(PickupKind.Weapon3, 0.5f),
            't' => new Decor('t', 0.5f, 0.75f, true, true),
            'p' => new Decor('p', 0.6f, 1.0f, true, false) { ReachCeiling = true },
            'T' => new Decor('T', 1.0f, 1.3f, true, false),
            '$' => new Chest(),
            '&' => new LoreStone(),
            '%' => new Pickup(PickupKind.Relic, 0.42f),
            'J' => new Pickup(PickupKind.Jetpack, 0.5f),
            'V' => new Ship(),
            'A' => MakeAsteroid(x, y),
            _ => null,
        };
        if (t == null) return null;
        t.X = x; t.Y = y;
        return t;
    }
}
