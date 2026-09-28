namespace HexenSharp;

/// <summary>
/// One achievement: what it's called, what it asks, the experience it pays, when it's done, and (for the ones that
/// build up) how far along you are.
/// </summary>
public sealed record AchievementDef(string Id, string Name, string About, int Xp, Func<Game, bool> Done, Func<Game, (int have, int need)?> Progress = null);

/// <summary>
/// Achievements, kept with your profile and listed on the Character screen. Each pays experience once, when it
/// unlocks; they're checked a few times a second, and never while you're cheating (god mode and the like, console
/// commands such as give or kill, or console-tweaked settings), so they stay earned. Ones already met by your
/// profile (kills, levels, medals, wins) unlock the next time the game checks.
/// </summary>
public static class Achievements
{
    static Profile Pr(Game g) => g.Profile;
    /// <summary>A game through the hub (not a practice course, the arena, a story case or a custom map).</summary>
    static bool InHub(Game g) => g.P != null && !g.TestingMap && !g.StoryMode && g.Mode is GameMode.Playing or GameMode.Victory;
    static int ArenaBest(Game g) => Math.Max(Enum.GetValues<PClass>().Max(c => Pr(g).ArenaBestWave(c)), g.ArenaMode && g.Level?.Arena is { } a ? a.BestWave : 0);
    static int GoldCourses(Game g) => Courses.Timed.Count(c => Enum.GetValues<PClass>().Any(cls => c.MedalFor(cls, Pr(g).CourseBestTime(c.Key(cls))) == Medal.Gold));
    static int Bits(int m) => System.Numerics.BitOperations.PopCount((uint)m);

    public static readonly AchievementDef[] All =
    {
        new("first_blood", "First Blood", "Kill a monster", 25, g => Pr(g).TotalKills >= 1),
        new("slayer", "Slayer", "Kill 500 monsters", 250, g => Pr(g).TotalKills >= 500, g => (Pr(g).TotalKills, 500)),
        new("treasure", "Treasure Hunter", "Open 50 treasure chests", 150, g => Pr(g).ChestsOpened >= 50, g => (Pr(g).ChestsOpened, 50)),
        new("secrets", "Secret Keeper", "Find every secret in one game", 200,
            g => InHub(g) && g.SecretsTotal > 0 && g.P.Secrets >= g.SecretsTotal, g => InHub(g) ? (g.P.Secrets, g.SecretsTotal) : null),
        new("lore", "Loremaster", "Read every lore stone in one game", 200,
            g => InHub(g) && g.LoreTotal > 0 && g.P.LoreRead >= g.LoreTotal, g => InHub(g) ? (g.P.LoreRead, g.LoreTotal) : null),
        new("heresiarch", "Heresiarch Slain", "Win the game in the classic style", 300, g => Pr(g).ClassicWins >= 1),
        new("flawless", "Untouchable", "Win in the classic style without dying once", 500, g => Pr(g).FlawlessWins >= 1),
        new("nightmare", "Nightmare Walker", "Win on Nightmare, start to finish", 750, g => Pr(g).NightmareWins >= 1),
        new("pilgrim", "Pilgrim", "Win in the relaxed style: find every relic", 300, g => Pr(g).RelaxedWins >= 1),
        new("all_classes", "Jack of All Trades", "Win as all three classes", 500,
            g => Pr(g).ClassWins.Distinct().Count() >= 3, g => (Pr(g).ClassWins.Distinct().Count(), 3)),
        new("podium", "On the Podium", "Earn a medal on a practice course", 50,
            g => Courses.Timed.Any(c => Enum.GetValues<PClass>().Any(cls => c.MedalFor(cls, Pr(g).CourseBestTime(c.Key(cls))) != Medal.None))),
        new("gold_all", "Gold Standard", "Earn gold on every timed practice course", 500,
            g => GoldCourses(g) >= Courses.Timed.Length, g => (GoldCourses(g), Courses.Timed.Length)),
        new("speed", "Speed Demon", "Reach 250% of your run speed", 150,
            g => g.P != null && g.Mode == GameMode.Playing && !g.Level.Flight && g.P.HSpeed >= 2.5f * g.RunSpeed),
        new("arena_5", "Gladiator", "Clear wave 5 in the arena", 100, g => ArenaBest(g) >= 5, g => (Math.Min(ArenaBest(g), 5), 5)),
        new("arena_20", "Champion of Chaos", "Clear wave 20 in the arena", 750, g => ArenaBest(g) >= 20, g => (Math.Min(ArenaBest(g), 20), 20)),
        new("arena_mods", "Glutton for Punishment", "Clear wave 5 in the arena with three or more modifiers", 300,
            g => Pr(g).ArenaRuns.Values.SelectMany(b => b).Any(r => r.Waves >= 5 && Bits(r.Mods) >= 3)
                 || (g.ArenaMode && g.Level?.Arena is { } a && a.BestWave >= 5 && Bits((int)a.Mods) >= 3)),
        new("perk_max", "Overcharged", "Take an arena perk to rank III", 100,
            g => g.ArenaMode && g.Level?.Arena is { } a && a.Perks.Values.Any(r => r >= PerkInfo.MaxRank)),
        new("veteran", "Veteran", "Reach level 10", 150, g => Pr(g).Level >= 10, g => (Math.Min(Pr(g).Level, 10), 10)),
        new("master", "Master of Arms", "Raise a weapon to level 10", 250,
            g => Pr(g).Weapons.Values.Any(w => w.Level >= Profile.MaxWeaponLevel),
            g => (Pr(g).Weapons.Values.Select(w => w.Level).DefaultIfEmpty(1).Max(), Profile.MaxWeaponLevel)),
        new("case_closed", "Case Closed", "Solve every case in Story mode", 300, g => Pr(g).StoryWins >= 1),
    };

    public static AchievementDef Find(string id) => All.FirstOrDefault(a => a.Id == id);

    /// <summary>True when something's been switched on that would make an achievement too easy.</summary>
    public static bool Cheating(Game g)
    {
        var v = g.Vars;
        return g.Cheated || v.God || v.NoClip || v.NoTarget || v.InfiniteMana || v.InfiniteFuel
               || v.Speed != 1 || v.FireRate != 1 || v.ManaCost != 1 || Difficulties.Of(v) == Difficulty.Custom;
    }

    /// <summary>Unlocks every achievement that's now done (unless you're cheating); returns the ones it unlocked.</summary>
    public static List<AchievementDef> Check(Game g)
    {
        var got = new List<AchievementDef>();
        if (Cheating(g)) { g.Cheated = true; return got; }
        foreach (var a in All)
        {
            if (g.Profile.Achievements.ContainsKey(a.Id) || !a.Done(g)) continue;
            g.Profile.Achievements[a.Id] = DateTime.Now;
            got.Add(a);
            g.AchievementUnlocked(a);
        }
        return got;
    }
}
