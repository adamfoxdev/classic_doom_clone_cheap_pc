namespace HexenSharp;

/// <summary>
/// New Game+: after beating the campaign (classic style) you can go round again, tier by tier. Your level, skills and
/// weapon training come with you as they always do (they live in your profile); what changes is the hub. Every monster
/// has more health, hits harder and moves a little faster, the supplies lying about are shuffled between their spots
/// (some vials grown into flasks), there are more chests, and kills pay more XP.
/// </summary>
public static class NgPlus
{
    public const int MaxTier = 9;

    public static float Health(int tier) => 1 + 0.5f * tier;
    public static float Damage(int tier) => 1 + 0.25f * tier;
    public static float Speed(int tier) => MathF.Min(1.4f, 1 + 0.08f * tier);
    public static float Chests(int tier) => 1 + 0.25f * tier;
    public static float Xp(int tier) => 1 + 0.25f * tier;
    /// <summary>The chance a vial on the floor has grown into a flask.</summary>
    public static float Upgrade(int tier) => MathF.Min(0.5f, 0.15f * tier);

    public static string Name(int tier) => tier <= 1 ? "NEW GAME+" : $"NEW GAME+{tier}";

    /// <summary>What a tier does, for the title menu and the README.</summary>
    public static string About(int tier) =>
        $"monsters x{Health(tier):0.##} health, x{Damage(tier):0.##} damage; loot remixed; x{Xp(tier):0.##} kill XP";

    /// <summary>The class screen's one-liner under the tier's name.</summary>
    public static string Short(int tier) => $"MONSTERS x{Health(tier):0.##} HEALTH, x{Damage(tier):0.##} DAMAGE";

    static readonly PickupKind[] Supplies = { PickupKind.Vial, PickupKind.Flask, PickupKind.Urn, PickupKind.BlueMana, PickupKind.GreenMana, PickupKind.Armor };

    static float Size(PickupKind k) => k switch
    {
        PickupKind.Vial => 0.35f,
        PickupKind.Urn => 0.45f,
        PickupKind.Armor => 0.5f,
        _ => 0.4f,
    };

    /// <summary>Toughens every monster on the map.</summary>
    public static void Toughen(Level lv, int tier)
    {
        if (tier <= 0) return;
        foreach (var m in lv.Things.OfType<Monster>())
        {
            m.MaxHealth = (int)(m.Def.Health * Health(tier));
            m.Health = m.MaxHealth;
            m.NextBlinkHp = (int)(m.MaxHealth * 0.8f);
            m.DamageMult *= Damage(tier);
            m.SpeedMult *= Speed(tier);
        }
    }

    /// <summary>
    /// Shuffles the supplies on a map between their spots, so a remembered route finds different things, and grows some
    /// vials into flasks. Keys, weapons, relics, the jetpack and the like stay where the map puts them.
    /// </summary>
    public static void Remix(Level lv, Random rng, int tier)
    {
        if (tier <= 0) return;
        var spots = lv.Things.OfType<Pickup>().Where(p => Supplies.Contains(p.Kind)).ToList();
        var kinds = spots.Select(p => p.Kind).OrderBy(_ => rng.Next()).ToList();
        for (int i = 0; i < spots.Count; i++)
        {
            var k = kinds[i];
            if (k == PickupKind.Vial && rng.NextDouble() < Upgrade(tier)) k = PickupKind.Flask;
            var old = spots[i];
            var fresh = new Pickup(k, Size(k)) { X = old.X, Y = old.Y, Z = old.Z, Level = lv };
            lv.Things[lv.Things.IndexOf(old)] = fresh;
        }
    }
}

public sealed partial class Game
{
    /// <summary>The New Game+ tier of this campaign (0 for a first run, and for everything that isn't the campaign).</summary>
    public int NgTier;

    bool NgEligible => !Practicing && !ArenaMode && !StoryMode && !TestingMap && !Relaxed;

    /// <summary>Round again: the same class, the next tier up.</summary>
    public void StartNewGamePlus(PClass cls, int tier)
    {
        HubSource = Maps.BuildHub;
        TestingMap = false; Practicing = false; ArenaMode = false; StoryMode = false; Story = null; Demo = false;
        Style = GameStyle.Classic;
        NgTier = Math.Clamp(tier, 1, NgPlus.MaxTier);
        NewGame(cls);
        Say($"{NgPlus.Name(NgTier)}: the hub stirs, meaner than before. Your level comes with you.");
    }

    /// <summary>Called from NewGame once the hub is built: toughens it and remixes its loot for the tier.</summary>
    void ApplyNgPlus()
    {
        if (!NgEligible) NgTier = 0;
        if (NgTier <= 0) return;
        foreach (var lv in Hub)
        {
            NgPlus.Toughen(lv, NgTier);
            NgPlus.Remix(lv, _loot, NgTier);
        }
    }

    /// <summary>A campaign win opens the next tier.</summary>
    void NgPlusWon()
    {
        if (!NgEligible) return;
        Profile.NgUnlocked = Math.Min(NgPlus.MaxTier, Math.Max(Profile.NgUnlocked, NgTier + 1));
        Profile.NgBest = Math.Max(Profile.NgBest, NgTier);
    }

    /// <summary>Whether the victory screen offers New Game+ (a classic campaign win).</summary>
    public bool OffersNgPlus => Mode == GameMode.Victory && NgEligible && Hub != null && Profile.NgUnlocked > NgTier;
}
