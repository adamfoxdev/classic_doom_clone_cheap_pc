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

    public const int MaxRank = 3;
    public static string Numeral(int rank) => rank switch { 2 => "II", 3 => "III", _ => "" };

    // what each rank does: rank 1 is the mod as found; a duplicate raises it, to III at most
    public static int PierceCount(int rank) => 2 * rank;
    public static int CleaveExtra(int rank) => rank;
    public static float ChainChanceAt(int rank) => ChainChance + 0.1f * (rank - 1);
    public static int ChainTargetsAt(int rank) => ChainTargets + (rank - 1);
    public static float ChargeMultAt(int rank) => ChargeMult + 0.5f * (rank - 1);
    public static float ChargeTimeAt(int rank) => ChargeTime - 0.1f * (rank - 1);
    public static float FrostTimeAt(int rank) => FrostTime + (rank - 1);
    public static float FrostSlowAt(int rank) => FrostSlow - 0.1f * (rank - 1);

    /// <summary>What a rank adds, for the pickup message.</summary>
    public static string RankAbout(WeaponMod m, int rank) => m switch
    {
        WeaponMod.Piercing => $"shots go through {PierceCount(rank)} more, blows cleave {CleaveExtra(rank)} more",
        WeaponMod.Chain => $"{ChainChanceAt(rank) * 100:0}% of hits arc to {ChainTargetsAt(rank)} more",
        WeaponMod.Charged => $"up to {ChargeMultAt(rank)}x, full in {ChargeTimeAt(rank):0.0}s",
        WeaponMod.Frost => $"hits slow to {FrostSlowAt(rank) * 100:0}% speed for {FrostTimeAt(rank)}s",
        _ => "",
    };

    /// <summary>
    /// Combinations: a weapon carrying two different mods gets the pair's extra, one for each of the six pairs. A
    /// "charged hit" is one from a shot charged past double damage.
    /// </summary>
    public enum Combo { None, FrozenArc, Lance, DeepFreeze, Shatter, Thunderclap, StormLance }

    public static Combo ComboOf(WeaponMod a, WeaponMod b) => (a < b ? (a, b) : (b, a)) switch
    {
        (WeaponMod.Chain, WeaponMod.Frost) => Combo.FrozenArc,
        (WeaponMod.Piercing, WeaponMod.Charged) => Combo.Lance,
        (WeaponMod.Charged, WeaponMod.Frost) => Combo.DeepFreeze,
        (WeaponMod.Piercing, WeaponMod.Frost) => Combo.Shatter,
        (WeaponMod.Chain, WeaponMod.Charged) => Combo.Thunderclap,
        (WeaponMod.Piercing, WeaponMod.Chain) => Combo.StormLance,
        _ => Combo.None,
    };

    public static string ComboName(Combo c) => c switch
    {
        Combo.FrozenArc => "Frozen Arc",
        Combo.Lance => "Lance",
        Combo.DeepFreeze => "Deep Freeze",
        Combo.Shatter => "Shatter",
        Combo.Thunderclap => "Thunderclap",
        Combo.StormLance => "Storm Lance",
        _ => "",
    };

    public static string ComboAbout(Combo c) => c switch
    {
        Combo.FrozenArc => "arcs chill what they hit",
        Combo.Lance => "a charged shot goes through everything in its path",
        Combo.DeepFreeze => $"a charged hit freezes the monster solid for {DeepFreezeTime}s",
        Combo.Shatter => $"chilled monsters take {ShatterBonus * 100:0}% more damage from this weapon",
        Combo.Thunderclap => "a charged hit always arcs, to twice as many",
        Combo.StormLance => "arcs leap on once more from each monster they hit",
        _ => "",
    };

    public const float DeepFreezeTime = 1.5f, ShatterBonus = 0.5f, ChargedHit = 2f;

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
    int _chainDepth;
    /// <summary>The charge behind the hit being dealt (1 for an ordinary one), for the charged combos.</summary>
    float _hitPower = 1;

    /// <summary>A weapon's first mod (None for none); see ModsOn for both.</summary>
    public WeaponMod ModOf(int slot) => P != null && slot is >= 0 and < 3 ? P.Mods[slot] : WeaponMod.None;

    /// <summary>Does the weapon in `slot` carry mod `m`, and at what rank (0 if not)?</summary>
    public int ModRank(int slot, WeaponMod m)
    {
        if (P == null || slot is < 0 or > 2 || m == WeaponMod.None) return 0;
        if (P.Mods[slot] == m) return Math.Max(1, P.ModRanks[slot]);
        if (P.Mods2[slot] == m) return Math.Max(1, P.ModRanks2[slot]);
        return 0;
    }

    public bool HasMod(int slot, WeaponMod m) => ModRank(slot, m) > 0;

    public WeaponMods.Combo ComboOn(int slot) => P == null || slot is < 0 or > 2 ? WeaponMods.Combo.None : WeaponMods.ComboOf(P.Mods[slot], P.Mods2[slot]);

    /// <summary>The HUD's line for a weapon's mods: the combo's name if it has one, else its mods and ranks.</summary>
    public string ModLabel(int slot)
    {
        if (P == null || slot is < 0 or > 2 || P.Mods[slot] == WeaponMod.None) return null;
        string One(WeaponMod m) => m == WeaponMod.None ? null : (WeaponMods.Tag(m) + " " + WeaponMods.Numeral(ModRank(slot, m))).Trim();
        var combo = ComboOn(slot);
        if (combo != WeaponMods.Combo.None) return WeaponMods.ComboName(combo).ToUpperInvariant();
        return string.Join("+", new[] { One(P.Mods[slot]), One(P.Mods2[slot]) }.Where(t => t != null));
    }

    /// <summary>A mod pickup, lying where it can be grabbed.</summary>
    public static Pickup MakeMod(WeaponMod m, float x, float y) => new(PickupKind.Mod, 0.42f, (int)m) { X = x, Y = y };

    public WeaponMod RandomMod() => WeaponMods.All[_modRng.Next(WeaponMods.All.Length)];

    /// <summary>
    /// Fits a mod picked up to the weapon in your hand. A weapon carries two different mods; one it has already goes
    /// up a rank (to III); a third kind replaces the older of the two. False (left lying) if that mod's at III already.
    /// </summary>
    bool FitMod(WeaponMod m, out string msg)
    {
        var p = P;
        int w = p.Weapon;
        string weapon = p.CurWeapon.Name;
        msg = null;
        int rank = ModRank(w, m);
        if (rank >= WeaponMods.MaxRank) return false;
        if (rank > 0)
        {
            if (p.Mods[w] == m) p.ModRanks[w] = rank + 1; else p.ModRanks2[w] = rank + 1;
            msg = $"{WeaponMods.Name(m)} {WeaponMods.Numeral(rank + 1)} on the {weapon}: {WeaponMods.RankAbout(m, rank + 1)}.";
            return true;
        }
        string replaced = null;
        if (p.Mods[w] == WeaponMod.None) { p.Mods[w] = m; p.ModRanks[w] = 1; }
        else if (p.Mods2[w] == WeaponMod.None) { p.Mods2[w] = m; p.ModRanks2[w] = 1; }
        else
        {
            replaced = WeaponMods.Name(p.Mods[w]);
            (p.Mods[w], p.ModRanks[w]) = (p.Mods2[w], p.ModRanks2[w]);
            (p.Mods2[w], p.ModRanks2[w]) = (m, 1);
        }
        p.Charge = 0;
        msg = $"{WeaponMods.Name(m)} fitted to the {weapon}" + (replaced != null ? $", replacing the {replaced}" : "") + $": {WeaponMods.About(m)}.";
        var combo = ComboOn(w);
        if (combo != WeaponMods.Combo.None) msg += $" Combo: {WeaponMods.ComboName(combo)} - {WeaponMods.ComboAbout(combo)}!";
        return true;
    }

    /// <summary>
    /// The trigger, for a weapon with a Charged mod: holding Fire charges it (up to a full charge in its ChargeTime),
    /// and letting go fires with the charge's extra damage. A tap fires as usual. True when it handled the trigger.
    /// </summary>
    bool ChargeTrigger(Input inp, float dt)
    {
        var p = P;
        int rank = ModRank(p.Weapon, WeaponMod.Charged);
        if (rank == 0) { p.Charge = 0; p.Charging = false; return false; }
        bool ready = !Relaxed && !StoryMode && p.Cooldown <= 0 && p.PendingWeapon < 0 && p.Raise < 0.2f;
        if (inp.Fire && ready)
        {
            p.Charging = true;
            p.Charge = MathF.Min(1, p.Charge + dt / WeaponMods.ChargeTimeAt(rank));
        }
        else if (p.Charging && !inp.Fire)
        {
            float power = 1 + (WeaponMods.ChargeMultAt(rank) - 1) * p.Charge;
            p.Charging = false; p.Charge = 0;
            Fire(power);
        }
        return true;
    }

    /// <summary>Shatter: a chilled monster takes more from a weapon with Piercing and Frost.</summary>
    int ModDamage(Monster m, int dmg, int slot) =>
        ComboOn(slot) == WeaponMods.Combo.Shatter && m.SlowTime > 0 ? (int)MathF.Round(dmg * (1 + WeaponMods.ShatterBonus)) : dmg;

    /// <summary>A hit with one of your weapons landed: its Frost and Chain mods, and their combos.</summary>
    void ModHit(Monster m, int dmg, int slot)
    {
        var combo = ComboOn(slot);
        bool charged = _hitPower >= WeaponMods.ChargedHit;
        int frost = ModRank(slot, WeaponMod.Frost);
        if (frost > 0 && m.Alive && (_chainDepth == 0 || combo == WeaponMods.Combo.FrozenArc))
        {
            m.SlowTime = WeaponMods.FrostTimeAt(frost);
            m.SlowFactor = WeaponMods.FrostSlowAt(frost);
            SpawnPuff(Art.Bolt[1], m.X, m.Y, Level.FloorAt(m.X, m.Y) + m.Z + m.SpriteH * 0.6f, 0.25f);
        }
        if (combo == WeaponMods.Combo.DeepFreeze && charged && m.Alive && _chainDepth == 0)
        {
            m.FrozenTime = WeaponMods.DeepFreezeTime;
            m.SlowTime = MathF.Max(m.SlowTime, WeaponMods.DeepFreezeTime);
        }
        int chain = ModRank(slot, WeaponMod.Chain);
        if (chain == 0) return;
        int maxDepth = combo == WeaponMods.Combo.StormLance ? 2 : 1;
        if (_chainDepth >= maxDepth) return;
        bool clap = combo == WeaponMods.Combo.Thunderclap && charged && _chainDepth == 0;
        if (!clap && _modRng.NextDouble() >= WeaponMods.ChainChanceAt(chain)) return;
        int targets = WeaponMods.ChainTargetsAt(chain) * (clap ? 2 : 1);
        var near = Level.Things.OfType<Monster>()
            .Where(o => o != m && o.Alive && !o.Blurring && Dist(o.X, o.Y, m.X, m.Y) < WeaponMods.ChainRange && Level.Sight(m.X, m.Y, o.X, o.Y))
            .OrderBy(o => Dist(o.X, o.Y, m.X, m.Y)).Take(targets).ToList();
        if (near.Count == 0) return;
        _chainDepth++;
        float power = _hitPower;
        _hitPower = 1; // arcs aren't charged hits
        foreach (var o in near)
        {
            SpawnPuff(Art.Lightning[1], o.X, o.Y, Level.FloorAt(o.X, o.Y) + o.Z + o.SpriteH * 0.5f, 0.35f);
            DamageMonster(o, Math.Max(1, (int)(dmg * WeaponMods.ChainShare)), slot);
        }
        _hitPower = power;
        _chainDepth--;
        Sound(Sfx.Magic, m.X, m.Y);
    }
}
