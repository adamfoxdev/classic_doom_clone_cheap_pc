namespace HexenSharp;

/// <summary>
/// The Quake modes. In the Chaos Arena: Instagib (a railgun with endless slugs, and one slug kills anything; keep your
/// streak going, on its own board) and Rocket Arena (every Quake weapon, full ammo and health again each wave, no
/// pickups, and your own blasts push you but don't hurt). On the range: rail trials, targets popping up far off and
/// sliding about, scored on how fast you hit them and how few slugs you waste.
/// </summary>
public static class RailTrials
{
    public const float Length = 30f, TargetLife = 2.5f, Gap = 0.4f;
    /// <summary>A hit scores 100, and up to 100 more the sooner it comes.</summary>
    public static int Points(float reaction) => 100 + (int)MathF.Round(100 * MathF.Max(0, 1 - reaction / TargetLife));
}

/// <summary>One rail trial: its score, hits, slugs fired, and the average time to a hit.</summary>
public sealed class RailRun
{
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public int Score { get; set; }
    public int Hits { get; set; }
    public int Shots { get; set; }
    public float Reaction { get; set; }
    public DateTime When { get; set; }
    public int Accuracy => Shots == 0 ? 0 : (int)MathF.Round(100f * Hits / Shots);
}

/// <summary>One instagib run: the waves cleared, the longest streak of slugs that hit, and the kills.</summary>
public sealed class InstagibRun
{
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public int Waves { get; set; }
    public int BestStreak { get; set; }
    public int Kills { get; set; }
    public float Time { get; set; }
    public DateTime When { get; set; }
}

public sealed partial class Game
{
    // ------------------------------------------------------------------ the arena's Quake modes

    /// <summary>Instagib: slugs that hit something in a row, and the most this run.</summary>
    public int Streak, BestStreak;
    public InstagibRun LastInstagib;
    public int LastInstagibPlace;

    /// <summary>The start of an arena run with a Quake mode: the weapons, ammo and health it gives.</summary>
    void SetUpQuakeArena()
    {
        Streak = BestStreak = 0;
        if (!InstagibOn && !RocketArenaOn) return;
        var p = P;
        p.Loadout = InstagibOn ? new[] { Railgun.Gun } : QuakeArms.All.ToArray();
        p.HasWeapon = Enumerable.Repeat(true, p.Loadout.Length).ToArray();
        p.Weapon = 0; p.PendingWeapon = -1; p.Raise = 0;
        for (int k = 1; k < QuakeAmmo.Kinds; k++) p.Ammo[k] = QuakeAmmo.Max((AmmoKind)k);
        if (RocketArenaOn) { p.MaxHealth = 200; p.Health = 200; p.Armor = 100; }
        Level.Things.RemoveAll(t => t is Pickup { Kind: not PickupKind.Upgrade }); // no pickups: what you have is what you get
    }

    /// <summary>Each frame of a Quake mode: instagib's slugs never run out.</summary>
    void QuakeArenaTick()
    {
        if (InstagibOn) P.Ammo[(int)AmmoKind.Slugs] = QuakeAmmo.Max(AmmoKind.Slugs);
    }

    /// <summary>A wave cleared in Rocket Arena: full ammo and health again.</summary>
    void QuakeArenaWave()
    {
        if (!RocketArenaOn) return;
        for (int k = 1; k < QuakeAmmo.Kinds; k++) P.Ammo[k] = QuakeAmmo.Max((AmmoKind)k);
        P.Health = P.MaxHealth; P.Armor = Math.Max(P.Armor, 100);
        Say("Rocket Arena: ammo and health restored.");
    }

    /// <summary>A slug fired in instagib: it hit something (the streak goes on) or nothing (it's over).</summary>
    void InstagibShot(int hits)
    {
        if (!InstagibOn) return;
        if (hits == 0) { Streak = 0; return; }
        Streak++;
        if (Streak > BestStreak) BestStreak = Streak;
        if (Streak % 5 == 0) { Say($"{Streak} in a row!"); PlaySound(Sfx.Secret, 0.6f); }
    }

    /// <summary>An instagib run's over: on its own board (by waves, then streak), not the arena's.</summary>
    void EndInstagibRun(ArenaState a)
    {
        var run = new InstagibRun { Name = RunnerName, Class = P.Class.ToString(), Waves = a.BestWave, BestStreak = BestStreak, Kills = P.Kills, Time = a.ClearedAt, When = DateTime.Now };
        LastInstagib = run;
        LastInstagibPlace = Profile.AddInstagibRun(run);
        SaveProfile();
        string msg = $"Instagib over: {a.BestWave} wave{(a.BestWave == 1 ? "" : "s")}, best streak {BestStreak}.";
        Say(LastInstagibPlace == 1 ? msg + " Your best!" : LastInstagibPlace > 0 ? msg + $" #{LastInstagibPlace} on the board." : msg);
    }

    // ------------------------------------------------------------------ rail trials

    public float TrialLeft;
    public int TrialScore, TrialHits, TrialShots;
    float _railReactions, _railWait;
    Monster _railTarget;
    public RailRun LastRail;
    public int LastRailPlace;

    /// <summary>Use with the railgun in hand on the range: thirty seconds of targets popping up far off.</summary>
    public void StartRailTrial()
    {
        if (!OnRange) return;
        Drilling = false;
        RailTrial = true; TrialLeft = RailTrials.Length; TrialScore = TrialHits = TrialShots = 0; _railReactions = 0; _railWait = 0.8f;
        ClearRailTarget();
        Messages.Clear();
        PlaySound(Sfx.BossSight, 0.6f);
        Say("Rail trial! Targets pop up far off: hit each before it goes. Quicker scores more; missed slugs cost accuracy.");
    }

    void ClearRailTarget()
    {
        if (_railTarget != null) { _railTarget.Removed = true; Level.Things.Remove(_railTarget); }
        _railTarget = null;
    }

    /// <summary>Each frame of a trial: the clock, the target's slide and time, and the next one.</summary>
    void RailTick(float dt)
    {
        if (!RailTrial) return;
        TrialLeft -= dt;
        if (TrialLeft <= 0) { EndRailTrial(); return; }
        if (_railTarget is { } t)
        {
            var tg = t.Target;
            tg.DownFor += dt; // (its age)
            tg.Phase += dt;
            t.X = Math.Clamp(tg.HomeX + MathF.Sin(tg.Phase * 1.3f) * 4f, 2.5f, ShootingRange.W - 2.5f);
            if (!t.Alive || tg.DownFor >= RailTrials.TargetLife) { ClearRailTarget(); _railWait = RailTrials.Gap; }
            return;
        }
        if ((_railWait -= dt) > 0) return;
        // a new one, far down the field, somewhere across it (from the trial's own roll of the dice)
        float x = 5.5f + RandF() * 28f, y = 3.5f + RandF() * 5f;
        _railTarget = new Monster(ShootingRange.Dummy) { X = x, Y = y, Level = Level, State = AiState.Idle };
        _railTarget.Target = new RangeTarget { HomeX = x, HomeY = y, Kind = ShootingRange.Kind.Trial, Phase = RandF() * 6 };
        Level.Things.Add(_railTarget);
        SpawnPuff(Art.RailSpiral, x, y, Level.FloorAt(x, y) + 0.5f, 0.6f);
    }

    /// <summary>A trial's target down: 100, and up to 100 more for speed.</summary>
    void RailTargetDown(Monster m)
    {
        float reaction = m.Target.DownFor;
        int pts = RailTrials.Points(reaction);
        TrialScore += pts; TrialHits++; _railReactions += reaction;
        Say($"+{pts} ({reaction:0.00}s)");
    }

    void EndRailTrial()
    {
        RailTrial = false;
        ClearRailTarget();
        Messages.Clear();
        var run = new RailRun
        {
            Name = RunnerName, Class = P.Class.ToString(), Score = TrialScore, Hits = TrialHits, Shots = TrialShots,
            Reaction = TrialHits > 0 ? _railReactions / TrialHits : 0, When = DateTime.Now,
        };
        if (PracticeSpeed < 1 || TrialScore == 0)
        {
            Say(TrialScore == 0 ? "Rail trial over: no hits. Lead the moving ones a little." : $"Rail trial over: {TrialScore} at {PracticeSpeed * 100:0}% speed (not recorded).");
            return;
        }
        int best = Profile.RailBest(P.Class);
        LastRail = run;
        LastRailPlace = Profile.AddRailRun(run);
        if (LastRailPlace > 0) SaveProfile();
        PlaySound(LastRailPlace == 1 ? Sfx.Secret : Sfx.Teleport, 1);
        string how = $"Rail trial over: {TrialScore} points, {TrialHits} hits, {run.Accuracy}% accuracy, {run.Reaction:0.00}s to a hit";
        Say(LastRailPlace == 1 ? $"{how} - a new best!" : LastRailPlace > 0 ? $"{how} - #{LastRailPlace} on the board (best {best})." : $"{how} (best {best}).");
    }
}
