namespace HexenSharp;

public enum WeaponMod { None, Piercing, Chain, Charged, Frost }

/// <summary>
/// Weapon mods: rare attachments found in chests (more often in New Game+, and a New Game+ mini-boss always drops
/// one). Picking one up fits it to the weapon in your hand, replacing whatever mod it had; each of your three weapons
/// carries one at most, for the rest of the campaign.
/// </summary>
public static class WeaponMods
{
    public static readonly WeaponMod[] All = { WeaponMod.Piercing, WeaponMod.Chain, WeaponMod.Charged, WeaponMod.Frost };

    /// <summary>Seconds of holding Fire to charge a shot fully, and what a full charge multiplies its damage by.</summary>
    public const float ChargeTime = 0.8f, ChargeMult = 2.5f;
    /// <summary>How many more monsters a piercing shot goes through after the first.</summary>
    public const int Pierce = 2;
    /// <summary>The chance a hit arcs, to how many more monsters, within what range, for what share of the hit.</summary>
    public const float ChainChance = 0.33f, ChainRange = 4f, ChainShare = 0.5f;
    public const int ChainTargets = 2;
    public const float FrostTime = 2.5f, FrostSlow = 0.5f;

    public static string Name(WeaponMod m) => (m, Art.Style == ArtStyle.SciFi) switch
    {
        (WeaponMod.Piercing, false) => "Rune of Piercing",
        (WeaponMod.Piercing, true) => "Piercing Core",
        (WeaponMod.Chain, false) => "Storm Rune",
        (WeaponMod.Chain, true) => "Arc Coil",
        (WeaponMod.Charged, false) => "Rune of Gathering",
        (WeaponMod.Charged, true) => "Capacitor",
        (WeaponMod.Frost, false) => "Frost Rune",
        (WeaponMod.Frost, true) => "Cryo Emitter",
        _ => "",
    };

    /// <summary>A one-word tag for the HUD.</summary>
    public static string Tag(WeaponMod m) => m.ToString().ToUpperInvariant();

    public static string About(WeaponMod m) => m switch
    {
        WeaponMod.Piercing => "shots go through two more monsters, and blows cleave one more",
        WeaponMod.Chain => "a third of your hits arc on to two more monsters nearby, for half",
        WeaponMod.Charged => $"hold Fire to charge, let go for up to {ChargeMult}x damage",
        WeaponMod.Frost => $"hits slow monsters to half speed for {FrostTime} seconds",
        _ => "",
    };

    public static uint Colour(WeaponMod m) => m switch
    {
        WeaponMod.Piercing => Col.Rgb(255, 210, 90),
        WeaponMod.Chain => Col.Rgb(150, 190, 255),
        WeaponMod.Charged => Col.Rgb(255, 120, 220),
        WeaponMod.Frost => Col.Rgb(140, 240, 255),
        _ => Col.Rgb(200, 200, 200),
    };

    /// <summary>A mod's pickup: the arsenal upgrade's spinning emblem, washed in the mod's colour.</summary>
    public static Tex[][] Sprites = Array.Empty<Tex[]>();

    public static void BuildArt()
    {
        Sprites = All.Select(m => Art.Upgrade.Select(t => MiniBosses.Wash(t, Col.R(Colour(m)), Col.G(Colour(m)), Col.B(Colour(m)))).ToArray()).ToArray();
    }

    public static Tex[] SpriteFrames(int variant) => Sprites.Length == 0 ? Art.Upgrade : Sprites[Math.Clamp(variant - 1, 0, Sprites.Length - 1)];

    /// <summary>Chest weight for a mod (out of about 80 for the rest): rare, and commoner at each New Game+ tier.</summary>
    public static int ChestWeight(int ngTier) => 2 + 3 * ngTier;
}

public sealed partial class Game
{
    Random _modRng = new(1);
    bool _modChaining;

    public WeaponMod ModOf(int slot) => P != null && slot is >= 0 and < 3 ? P.Mods[slot] : WeaponMod.None;

    /// <summary>A mod pickup, lying where it can be grabbed.</summary>
    public static Pickup MakeMod(WeaponMod m, float x, float y) => new(PickupKind.Mod, 0.42f, (int)m) { X = x, Y = y };

    public WeaponMod RandomMod() => WeaponMods.All[_modRng.Next(WeaponMods.All.Length)];

    /// <summary>Fits a mod picked up to the weapon in your hand. False if it has that one already.</summary>
    bool FitMod(WeaponMod m, out string msg)
    {
        var p = P;
        var had = p.Mods[p.Weapon];
        string weapon = p.CurWeapon.Name;
        msg = null;
        if (had == m) return false;
        p.Mods[p.Weapon] = m;
        p.Charge = 0;
        msg = $"{WeaponMods.Name(m)} fitted to the {weapon}" + (had != WeaponMod.None ? $", replacing the {WeaponMods.Name(had)}" : "") + $": {WeaponMods.About(m)}.";
        return true;
    }

    /// <summary>
    /// The trigger, for a weapon with a Charged mod: holding Fire charges it (up to a full charge in ChargeTime), and
    /// letting go fires with the charge's extra damage. A tap fires as usual. True when it handled the trigger.
    /// </summary>
    bool ChargeTrigger(Input inp, float dt)
    {
        var p = P;
        if (ModOf(p.Weapon) != WeaponMod.Charged) { p.Charge = 0; p.Charging = false; return false; }
        bool ready = !Relaxed && !StoryMode && p.Cooldown <= 0 && p.PendingWeapon < 0 && p.Raise < 0.2f;
        if (inp.Fire && ready)
        {
            p.Charging = true;
            p.Charge = MathF.Min(1, p.Charge + dt / WeaponMods.ChargeTime);
        }
        else if (p.Charging && !inp.Fire)
        {
            float power = 1 + (WeaponMods.ChargeMult - 1) * p.Charge;
            p.Charging = false; p.Charge = 0;
            Fire(power);
        }
        return true;
    }

    /// <summary>A hit with one of your weapons landed: its Chain and Frost mods, if it has them.</summary>
    void ModHit(Monster m, int dmg, int slot)
    {
        var mod = ModOf(slot);
        if (mod == WeaponMod.Frost && m.Alive)
        {
            m.SlowTime = WeaponMods.FrostTime;
            SpawnPuff(Art.Bolt[1], m.X, m.Y, Level.FloorAt(m.X, m.Y) + m.Z + m.SpriteH * 0.6f, 0.25f);
        }
        if (mod != WeaponMod.Chain || _modChaining || _modRng.NextDouble() >= WeaponMods.ChainChance) return;
        var near = Level.Things.OfType<Monster>()
            .Where(o => o != m && o.Alive && !o.Blurring && Dist(o.X, o.Y, m.X, m.Y) < WeaponMods.ChainRange && Level.Sight(m.X, m.Y, o.X, o.Y))
            .OrderBy(o => Dist(o.X, o.Y, m.X, m.Y)).Take(WeaponMods.ChainTargets).ToList();
        if (near.Count == 0) return;
        _modChaining = true;
        foreach (var o in near)
        {
            SpawnPuff(Art.Lightning[1], o.X, o.Y, Level.FloorAt(o.X, o.Y) + o.Z + o.SpriteH * 0.5f, 0.35f);
            DamageMonster(o, Math.Max(1, (int)(dmg * WeaponMods.ChainShare)), slot);
        }
        _modChaining = false;
        Sound(Sfx.Magic, m.X, m.Y);
    }
}
