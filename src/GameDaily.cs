namespace HexenSharp;

public sealed partial class Game
{
    /// <summary>In the daily challenge (Arena > Daily challenge): an arena run set by the date.</summary>
    public bool DailyMode;
    public DateOnly DailyDate;
    /// <summary>The last daily run that ended: its score, and whether it was the day's scored one.</summary>
    public DailyRun LastDaily;
    public bool LastDailyScored;

    /// <summary>The modifiers this arena run plays with: the day's in the daily challenge, else the ones you picked.</summary>
    public ArenaMod RunMods => DailyMode ? Daily.For(DailyDate).mods : ArenaMods;

    /// <summary>Today's scored run under your name, if you've finished one.</summary>
    public DailyRun TodaysRun(DateOnly day) => Profile.DailyRuns.FirstOrDefault(r => r.Date == day.ToString("yyyy-MM-dd") && r.Name == RunnerName);

    /// <summary>Starts the daily challenge (today's unless you say): the day's class and modifiers, and its dice.</summary>
    public void StartDaily(DateOnly? date = null)
    {
        DailyDate = date ?? Daily.Today;
        StartArena(Daily.For(DailyDate).cls, daily: true);
        Messages.Clear();
        Say($"Daily challenge {DailyDate:yyyy-MM-dd}: {Daily.Describe(DailyDate)}.");
        Say(DailyDate != Daily.Today ? "An old day's challenge: practice only."
            : TodaysRun(DailyDate) is { } done ? $"Your score today: {done.Score}. More runs are practice."
            : "Your first finished run today is your score. Step on the altar when you're ready.");
    }

    /// <summary>A daily run is over: the day's first goes on the daily board; later ones only tell you how they went.</summary>
    void EndDailyRun(ArenaState a, Difficulty difficulty)
    {
        LastDaily = new DailyRun
        {
            Date = DailyDate.ToString("yyyy-MM-dd"), Name = RunnerName, Waves = a.BestWave, Time = a.ClearedAt, Kills = P.Kills,
            Class = P.Class.ToString(), Mods = (int)a.Mods, Score = ArenaModInfo.Score(a.BestWave, a.Mods, difficulty),
        };
        var earlier = TodaysRun(DailyDate);
        // only today's challenge scores (an old day played again from the console is practice)
        LastDailyScored = earlier == null && DailyDate == Daily.Today;
        string head = $"Run over: {a.BestWave} wave{(a.BestWave == 1 ? "" : "s")}, score {LastDaily.Score}.";
        if (LastDailyScored)
        {
            Profile.DailyRuns.Add(LastDaily);
            SaveProfile();
            int streak = Daily.Streak(Profile, RunnerName, DailyDate);
            Say($"{head} That's your score for {LastDaily.Date}." + (streak > 1 ? $" {streak} days in a row!" : ""));
        }
        else if (earlier != null) Say($"{head} Practice: your score today stays {earlier.Score}.");
        else Say($"{head} Practice: only today's challenge is scored.");
    }
}
