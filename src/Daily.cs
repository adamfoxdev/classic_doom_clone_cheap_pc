namespace HexenSharp;

/// <summary>
/// The daily challenge: an arena run set by the date (UTC), the same for everyone that day. The date picks the class,
/// one or two modifiers, and seeds the arena's own dice (which monsters each wave brings, where they appear, the perks
/// on offer), so every attempt that day faces the same waves. Your first finished run of the day is your score for it;
/// later runs are practice. Daily runs go on their own board, not the arena's.
/// </summary>
public static class Daily
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>A number from the date, the same on every machine (not string.GetHashCode, which changes every run).</summary>
    public static int Seed(DateOnly d) => d.Year * 10000 + d.Month * 100 + d.Day;

    /// <summary>The day's class and modifiers: one or two of double speed, no supplies and melee only (never random class).</summary>
    public static (PClass cls, ArenaMod mods) For(DateOnly d)
    {
        var rng = new Random(Seed(d));
        var cls = (PClass)rng.Next(3);
        var pool = new List<ArenaMod> { ArenaMod.DoubleSpeed, ArenaMod.NoSupplies, ArenaMod.MeleeOnly };
        var mods = ArenaMod.None;
        int count = rng.Next(3) == 0 ? 2 : 1;
        for (int k = 0; k < count; k++) { int i = rng.Next(pool.Count); mods |= pool[i]; pool.RemoveAt(i); }
        return (cls, mods);
    }

    public static string Describe(DateOnly d)
    {
        var (cls, mods) = For(d);
        var names = ArenaModInfo.All.Where(m => (mods & m) != 0).Select(m => ArenaModInfo.Name(m).ToLowerInvariant());
        return $"{ClassDef.All[(int)cls].Name}, {string.Join(" and ", names)}";
    }

    /// <summary>Consecutive days (ending today, or yesterday if today's not done yet) with a daily run under this name.</summary>
    public static int Streak(Profile pr, string name, DateOnly today)
    {
        var days = pr.DailyRuns.Where(r => r.Name == name).Select(r => r.Date).ToHashSet();
        var d = days.Contains(today.ToString("yyyy-MM-dd")) ? today : today.AddDays(-1);
        int n = 0;
        while (days.Contains(d.ToString("yyyy-MM-dd"))) { n++; d = d.AddDays(-1); }
        return n;
    }
}

/// <summary>One name's scored daily run: the first they finished that day.</summary>
public sealed class DailyRun
{
    public string Date { get; set; } = "";
    public string Name { get; set; } = "";
    public int Score { get; set; }
    public int Waves { get; set; }
    public float Time { get; set; }
    public int Kills { get; set; }
    public string Class { get; set; } = "";
    public int Mods { get; set; }
}
