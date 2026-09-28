namespace HexenSharp;

/// <summary>
/// Mini-boss rematches (Arena > Rematch): fight any mini-boss you've beaten again, alone on its own map, against the
/// clock. The time runs from the start until it falls; each boss has a board of the ten quickest (any class). There's
/// no experience, loot or codex count to farm, and nothing is saved; die and it starts over.
/// </summary>
public static class Rematches
{
    /// <summary>Where you start against each: a spot a little way off it (on the Depths, the way in; in the Void Crossing, the start of the lane).</summary>
    public static readonly Dictionary<string, (float x, float y)?> Starts = new()
    {
        ["warden"] = (14.5f, 4.5f),
        ["stalker"] = (16.5f, 12.5f),
        ["thornmother"] = (14.5f, 9.5f),
        ["keeper"] = (12.5f, 9.5f),
        ["wyrm"] = null,
        ["dreadnought"] = null,
    };

    /// <summary>In a rematch the Leviathan waits this far down the lane from the start, not near the end.</summary>
    public const float LeviathanAhead = 30f;

    public static string Map(MonsterDef d) => MiniBosses.Places.First(p => p.def == d).map;
}

/// <summary>One rematch won: who, as what, against which, and how quickly.</summary>
public sealed class RematchRun
{
    public string Boss { get; set; } = "";
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public float Time { get; set; }
    public DateTime When { get; set; }
}

public sealed partial class Game
{
    /// <summary>The mini-boss this rematch is against (null outside a rematch).</summary>
    public MonsterDef Rematch;
    /// <summary>Picking a class from the class screen starts a rematch against this one.</summary>
    public MonsterDef PendingRematch;
    /// <summary>The rematch's clock, and whether it's won.</summary>
    public float RematchTime;
    public bool RematchWon;
    public RematchRun LastRematch;
    public int LastRematchPlace;

    /// <summary>Starts a rematch against a mini-boss, as a class.</summary>
    public void StartRematch(PClass cls, MonsterDef boss)
    {
        string map = Rematches.Map(boss);
        HubSource = () => new[] { Maps.BuildHub().First(l => l.RawName == map) };
        TestingMap = true; Practicing = false; ArenaMode = false; DailyMode = false; StoryMode = false; Story = null; Demo = false;
        Style = GameStyle.Classic; NgTier = 0;
        Rematch = boss;
        NewGame(cls); // sets the fight up (SetUpRematch) once the map is built
    }

    /// <summary>On a fresh copy of the boss's map (starting, Restart, or after dying): just you, it, and the clock.</summary>
    void SetUpRematch()
    {
        var lv = Level;
        lv.Things.RemoveAll(t => t is Monster m && m.Def != Rematch || t is Chest);
        var boss = lv.Things.OfType<Monster>().First(m => m.Def == Rematch);
        if (lv.Flight)
        {
            EnterFlight();
            boss.X = lv.StartX + Rematches.LeviathanAhead;
        }
        else
        {
            float x, y;
            if (Rematches.Starts[Rematch.MiniBoss] is { } s) (x, y) = s;
            else { var (cx, cy) = lv.ArrivalCell(); (x, y) = (cx + 0.5f, cy + 0.5f); }
            MoveTo(x, y, MathF.Atan2(boss.Y - y, boss.X - x));
            P.TeleportFlash = 0;
        }
        RematchTime = 0; RematchWon = false;
        Messages.Clear();
        var best = Profile.RematchBoard(Rematch.MiniBoss).FirstOrDefault(r => r.Name == RunnerName);
        Say($"Rematch: the {Rematch.Name}." + (best != null ? $" Your best: {best.Time:0.00}s." : " Beat it as quickly as you can."));
    }

    void RematchTick(float dt)
    {
        if (Rematch == null || RematchWon || Mode != GameMode.Playing) return;
        RematchTime += dt;
    }

    /// <summary>The boss fell: the time goes on its board.</summary>
    void RematchDown()
    {
        RematchWon = true;
        var run = new RematchRun { Boss = Rematch.MiniBoss, Name = RunnerName, Class = P.Class.ToString(), Time = RematchTime, When = DateTime.Now };
        float best = Profile.RematchBoard(Rematch.MiniBoss).Select(r => r.Time).DefaultIfEmpty(0).First();
        int place = Profile.AddRematchRun(run);
        LastRematch = run; LastRematchPlace = place;
        if (place > 0) SaveProfile();
        Messages.Clear();
        Say(place == 1 ? $"The {Rematch.Name} falls in {RematchTime:0.00}s - a new best!"
            : place > 0 ? $"The {Rematch.Name} falls in {RematchTime:0.00}s - #{place} on the board (best {best:0.00}s)."
            : $"The {Rematch.Name} falls in {RematchTime:0.00}s (best {best:0.00}s).");
        Say("Restart from the pause menu for another go.");
    }
}
