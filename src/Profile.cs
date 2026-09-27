using System.Text.Json;

namespace HexenSharp;

/// <summary>Skills you spend points on as you level up. Each has up to <see cref="Profile.MaxRank"/> ranks.</summary>
public enum Skill { Vitality, Power, Agility, Focus, Thrusters }

/// <summary>A weapon's own progress: it levels up from the kills it makes.</summary>
public sealed class WeaponProgress
{
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
}

/// <summary>
/// Your character's progress, kept between games: experience and level, skill ranks bought with the points each
/// level gives, and every weapon's level. Saved as profile.json next to settings.cfg.
/// </summary>
public sealed class Profile
{
    public const int MaxLevel = 50, MaxRank = 10, MaxWeaponLevel = 10;
    public static readonly Skill[] Skills = Enum.GetValues<Skill>();

    public int Level { get; set; } = 1;
    /// <summary>Experience toward the next level.</summary>
    public int Xp { get; set; }
    /// <summary>Unspent skill points.</summary>
    public int Points { get; set; }
    public Dictionary<string, int> Ranks { get; set; } = new();
    /// <summary>Weapon progress by "Class/slot", e.g. "Fighter/1" for the Fighter's axe.</summary>
    public Dictionary<string, WeaponProgress> Weapons { get; set; } = new();
    public int TotalXp { get; set; }
    public int TotalKills { get; set; }
    public int Wins { get; set; }
    /// <summary>Best practice course times by class, from before the leaderboard; folded into CourseRuns when read.</summary>
    public Dictionary<string, float> CourseBest { get; set; } = new();
    /// <summary>The practice course leaderboard: the fastest runs by class, quickest first.</summary>
    public Dictionary<string, List<CourseRun>> CourseRuns { get; set; } = new();
    public const int BoardSize = 10;

    /// <summary>A class's leaderboard, quickest first (a best time saved before the leaderboard existed joins it).</summary>
    public List<CourseRun> Board(string cls)
    {
        if (!CourseRuns.TryGetValue(cls, out var runs)) CourseRuns[cls] = runs = new List<CourseRun>();
        if (CourseBest.Remove(cls, out float old) && old > 0 && !runs.Any(r => r.Time == old))
        {
            runs.Add(new CourseRun { Time = old, Name = "-" });
            runs.Sort((a, b) => a.Time.CompareTo(b.Time));
            if (runs.Count > BoardSize) runs.RemoveRange(BoardSize, runs.Count - BoardSize);
        }
        return runs;
    }

    /// <summary>Your best time on the course as `cls`, or 0 if you haven't finished it.</summary>
    public float CourseBestTime(string cls) => Board(cls) is { Count: > 0 } b ? b[0].Time : 0f;

    /// <summary>Puts a finished run on its class's leaderboard: its place (1 is the best), or 0 if it's outside the top ten.</summary>
    public int AddCourseRun(string cls, float time, string name, DateTime when)
    {
        var runs = Board(cls);
        int place = runs.Count(r => r.Time <= time);
        if (place >= BoardSize) return 0;
        runs.Insert(place, new CourseRun { Time = time, Name = name, When = when });
        if (runs.Count > BoardSize) runs.RemoveAt(runs.Count - 1);
        return place + 1;
    }

    /// <summary>Experience needed to go from `level` to the next: 100, 282, 519, 800...</summary>
    public static int XpToNext(int level) => (int)(100 * Math.Pow(level, 1.5));
    public static int WeaponXpToNext(int level) => (int)(60 * Math.Pow(level, 1.4));

    public int Rank(Skill s) => Ranks.TryGetValue(s.ToString(), out int r) ? Math.Clamp(r, 0, MaxRank) : 0;

    /// <summary>Adds experience; returns how many levels you gained (each gives a skill point).</summary>
    public int AddXp(int amount)
    {
        if (amount <= 0) return 0;
        TotalXp += amount;
        if (Level >= MaxLevel) return 0;
        Xp += amount;
        int gained = 0;
        while (Level < MaxLevel && Xp >= XpToNext(Level))
        {
            Xp -= XpToNext(Level);
            Level++;
            Points++;
            gained++;
        }
        if (Level >= MaxLevel) Xp = 0;
        return gained;
    }

    /// <summary>Spends a point on a skill, if you have one and the skill isn't maxed.</summary>
    public bool Spend(Skill s)
    {
        if (Points <= 0 || Rank(s) >= MaxRank) return false;
        Ranks[s.ToString()] = Rank(s) + 1;
        Points--;
        return true;
    }

    static string Key(PClass cls, int slot) => $"{cls}/{slot}";

    public WeaponProgress Weapon(PClass cls, int slot) =>
        Weapons.TryGetValue(Key(cls, slot), out var w) ? w : Weapons[Key(cls, slot)] = new WeaponProgress();

    /// <summary>Adds experience to a weapon; true when it levels up.</summary>
    public bool AddWeaponXp(PClass cls, int slot, int amount)
    {
        var w = Weapon(cls, slot);
        if (w.Level >= MaxWeaponLevel || amount <= 0) return false;
        w.Xp += amount;
        bool up = false;
        while (w.Level < MaxWeaponLevel && w.Xp >= WeaponXpToNext(w.Level))
        {
            w.Xp -= WeaponXpToNext(w.Level);
            w.Level++;
            up = true;
        }
        if (w.Level >= MaxWeaponLevel) w.Xp = 0;
        return up;
    }

    // ------------------------------------------------------------ what the skills do

    public int MaxHealth => 100 + 10 * Rank(Skill.Vitality);
    public float DamageMult => 1 + 0.08f * Rank(Skill.Power);
    public float SpeedMult => 1 + 0.04f * Rank(Skill.Agility);
    public float ManaMult => 1 - 0.06f * Rank(Skill.Focus);
    public float FireRateMult => 1 + 0.05f * Rank(Skill.Focus);
    public float FuelMult => 1 + 0.15f * Rank(Skill.Thrusters);
    public float WeaponMult(PClass cls, int slot) => 1 + 0.08f * (Weapon(cls, slot).Level - 1);

    /// <summary>What one more rank of a skill (or the current total) gives, for the character screen.</summary>
    public static string Effect(Skill s, int rank) => s switch
    {
        Skill.Vitality => $"+{10 * rank} MAX HEALTH",
        Skill.Power => $"+{8 * rank}% DAMAGE",
        Skill.Agility => $"+{4 * rank}% SPEED",
        Skill.Focus => $"-{6 * rank}% MANA, +{5 * rank}% RATE",
        _ => $"+{15 * rank}% FUEL",
    };

    // ------------------------------------------------------------ saving

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>Reads a profile; a missing or damaged file gives a fresh one rather than an error.</summary>
    public static Profile Load(string path)
    {
        try
        {
            if (path == null || !File.Exists(path)) return new Profile();
            var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path)) ?? new Profile();
            p.Level = Math.Clamp(p.Level, 1, MaxLevel);
            p.Xp = Math.Max(0, p.Xp);
            p.Points = Math.Max(0, p.Points);
            p.Ranks ??= new();
            p.Weapons ??= new();
            foreach (var w in p.Weapons.Values) w.Level = Math.Clamp(w.Level, 1, MaxWeaponLevel);
            return p;
        }
        catch (Exception) { return new Profile(); }
    }

    public void Save(string path)
    {
        if (path == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, ToJson());
        }
        catch (Exception) { /* progress is kept in memory; the next save tries again */ }
    }
}

/// <summary>One finished run of the practice course.</summary>
public sealed class CourseRun
{
    public float Time { get; set; }
    public string Name { get; set; } = "";
    public DateTime When { get; set; }
}
