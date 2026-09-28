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
    /// <summary>Wins by kind, and the classes you've won as, for the achievements.</summary>
    public int ClassicWins { get; set; }
    public int RelaxedWins { get; set; }
    public int FlawlessWins { get; set; }
    public int NightmareWins { get; set; }
    public int StoryWins { get; set; }
    /// <summary>The highest New Game+ tier opened (0 until the campaign's been won), and the highest one won.</summary>
    public int NgUnlocked { get; set; }
    public int NgBest { get; set; }
    public List<string> ClassWins { get; set; } = new();
    public int ChestsOpened { get; set; }
    /// <summary>The mini-bosses you've beaten (their ids), for the achievement.</summary>
    public List<string> MiniBosses { get; set; } = new();
    /// <summary>Unlocked achievements, by id, with when.</summary>
    public Dictionary<string, DateTime> Achievements { get; set; } = new();
    /// <summary>Best practice course times by class, from before the leaderboard; folded into CourseRuns when read.</summary>
    public Dictionary<string, float> CourseBest { get; set; } = new();
    /// <summary>The practice course leaderboard: the fastest runs by class, quickest first.</summary>
    public Dictionary<string, List<CourseRun>> CourseRuns { get; set; } = new();
    public const int BoardSize = 10;
    /// <summary>The recorded path of your best practice run for each class, raced as a ghost on later runs.</summary>
    public Dictionary<string, CourseGhost> Ghosts { get; set; } = new();

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

    /// <summary>Scored daily challenge runs: one per name per day, the first they finished.</summary>
    public List<DailyRun> DailyRuns { get; set; } = new();

    /// <summary>The arena leaderboard by class: the runs that cleared the most waves, the quickest first among equals.</summary>
    public Dictionary<string, List<ArenaRun>> ArenaRuns { get; set; } = new();

    /// <summary>A class's arena leaderboard, best first.</summary>
    public List<ArenaRun> ArenaBoard(PClass cls)
    {
        ArenaRuns ??= new();
        string key = cls.ToString();
        if (!ArenaRuns.TryGetValue(key, out var runs)) ArenaRuns[key] = runs = new List<ArenaRun>();
        return runs;
    }

    /// <summary>The most waves you've cleared in the arena as `cls` (0 if none), whatever the modifiers.</summary>
    public int ArenaBestWave(PClass cls) => ArenaBoard(cls) is { Count: > 0 } b ? b.Max(r => r.Waves) : 0;

    /// <summary>Your best arena score as `cls` (0 if none).</summary>
    public int ArenaBestScore(PClass cls) => ArenaBoard(cls) is { Count: > 0 } b ? b[0].Score : 0;

    /// <summary>Puts a finished arena run on its class's leaderboard: its place (1 is the best), or 0 if it's outside the top ten.</summary>
    public int AddArenaRun(PClass cls, ArenaRun run)
    {
        var runs = ArenaBoard(cls);
        if (run.Score == 0) run.Score = ArenaModInfo.Score(run.Waves, (ArenaMod)run.Mods, (Difficulty)run.Difficulty);
        int place = runs.Count(r => !run.Beats(r));
        if (place >= BoardSize) return 0;
        runs.Insert(place, run);
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
            p.ArenaRuns ??= new();
            p.ClassWins ??= new();
            p.MiniBosses ??= new();
            p.DailyRuns ??= new();
            p.Achievements ??= new();
            // runs saved before scores: 100 a wave, as a run with no modifiers scores
            foreach (var board in p.ArenaRuns.Values)
            {
                foreach (var r in board) if (r.Score == 0) r.Score = ArenaModInfo.Score(r.Waves, (ArenaMod)r.Mods, (Difficulty)r.Difficulty);
                board.Sort((a, b) => a.Beats(b) ? -1 : b.Beats(a) ? 1 : 0);
            }
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

/// <summary>One arena run: its score, the waves it cleared, how long those took, its kills, modifiers, and who and when.</summary>
public sealed class ArenaRun
{
    /// <summary>100 a wave, times the modifiers' multiplier (<see cref="ArenaModInfo.Score"/>).</summary>
    public int Score { get; set; }
    public int Waves { get; set; }
    /// <summary>The run's modifiers (an <see cref="ArenaMod"/> as a number, so the profile stays plain JSON).</summary>
    public int Mods { get; set; }
    /// <summary>The difficulty it was played on (a <see cref="HexenSharp.Difficulty"/>; 0, Normal, for runs from before).</summary>
    public int Difficulty { get; set; }
    public float Time { get; set; }
    public int Kills { get; set; }
    public string Name { get; set; } = "";
    public DateTime When { get; set; }

    /// <summary>Better than `other`: a higher score, then more waves, then as many in less time (a tie keeps the older run ahead).</summary>
    public bool Beats(ArenaRun other) =>
        Score != other.Score ? Score > other.Score : Waves != other.Waves ? Waves > other.Waves : Time < other.Time;
}

/// <summary>A saved practice-course ghost: the run's time and its path (a GhostTrack, packed as base64).</summary>
public sealed class CourseGhost
{
    public float Time { get; set; }
    public string Path { get; set; } = "";
}

/// <summary>
/// Where you were through a practice run, sampled every <see cref="Step"/> seconds of the run clock: position and
/// height (floor plus jump), so a ghost can retrace it. Packed as base64 of little-endian floats for the profile.
/// </summary>
public sealed class GhostTrack
{
    public const float Step = 0.05f;
    public readonly List<(float x, float y, float z)> Points = new();
    public float Duration => Math.Max(0, Points.Count - 1) * Step;

    /// <summary>Adds samples up to run time `t` (repeating the current spot to fill any gap in the frames).</summary>
    public void Record(float t, float x, float y, float z)
    {
        while (Points.Count * Step <= t + 1e-4f) Points.Add((x, y, z));
    }

    /// <summary>Where the run was at time `t`, blending between samples; its start before, its end after.</summary>
    public (float x, float y, float z) At(float t)
    {
        if (Points.Count == 0) return (0, 0, 0);
        float f = Math.Clamp(t / Step, 0, Points.Count - 1);
        int i = (int)f;
        if (i >= Points.Count - 1) return Points[^1];
        float k = f - i;
        var (a, b) = (Points[i], Points[i + 1]);
        // a teleport (the lift) jumps rather than sliding across the hangar
        if (MathF.Abs(b.x - a.x) + MathF.Abs(b.y - a.y) > 2f) return k < 0.5f ? a : b;
        return (a.x + (b.x - a.x) * k, a.y + (b.y - a.y) * k, a.z + (b.z - a.z) * k);
    }

    /// <summary>The first time the run stood on or past column `x` at height `floor`, or -1 if it never did.</summary>
    public float TimeAt(float x, float floor)
    {
        for (int i = 0; i < Points.Count; i++)
            if (Points[i].x >= x && MathF.Abs(Points[i].z - floor) < 0.05f) return i * Step;
        return -1;
    }

    public string Encode()
    {
        var bytes = new byte[Points.Count * 12];
        for (int i = 0; i < Points.Count; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 12), Points[i].x);
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 12 + 4), Points[i].y);
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 12 + 8), Points[i].z);
        }
        return Convert.ToBase64String(bytes);
    }

    public static GhostTrack Decode(string s)
    {
        var t = new GhostTrack();
        try
        {
            var bytes = Convert.FromBase64String(s ?? "");
            for (int i = 0; i + 12 <= bytes.Length; i += 12)
                t.Points.Add((BitConverter.ToSingle(bytes, i), BitConverter.ToSingle(bytes, i + 4), BitConverter.ToSingle(bytes, i + 8)));
        }
        catch (FormatException) { t.Points.Clear(); }
        return t;
    }
}
