namespace HexenSharp;

/// <summary>Checks on deterministic replays, and the whole-level regression replays in tests/replays.</summary>
public static partial class Headless
{
    /// <summary>A minute or so of seeded, random-but-purposeful play: running, strafing, turning, jumping, firing, using.</summary>
    public static void Bot(Game g, int seed, float seconds, bool varyFrames = true)
    {
        var rng = new Random(seed);
        var inp = new Input();
        float t = 0, hold = 0;
        while (t < seconds)
        {
            if ((hold -= 0.1f) <= 0)
            {
                hold = (float)rng.NextDouble() * 1.5f;
                inp = new Input
                {
                    Move = rng.Next(4) == 0 ? -1 : 1, Strafe = rng.Next(3) - 1, Fire = rng.Next(3) > 0, JumpHeld = rng.Next(5) == 0,
                    LookX = (float)(rng.NextDouble() - 0.5) * 40, LookY = (float)(rng.NextDouble() - 0.5) * 6,
                };
            }
            var frame = inp;
            frame.Jump = inp.JumpHeld && rng.Next(4) == 0;
            frame.Use = rng.Next(40) == 0;
            frame.Slot = rng.Next(90) == 0 ? rng.Next(1, 4) : 0;
            float dt = varyFrames ? 1f / (30 + rng.Next(60)) : 1f / 35f;
            g.Update(g.Mode == GameMode.Dead ? default : frame, dt); // if it dies, it stays down (a restart would start a new recording)
            t += dt;
        }
    }

    static void ReplayChecks(Action<bool, string> check)
    {
        // a run recorded as it's played replays to exactly the same place, whatever the frame rate
        var g = new Game { AchievementsOn = false, Profile = new Profile { Level = 6, Ranks = new() { ["Power"] = 2, ["Agility"] = 1 } } };
        g.Vars.MonsterDamage = 0.15f; // so the bot lives through the minute (the replay keeps the setting)
        g.NewGame(PClass.Fighter);
        Bot(g, 11, 60);
        var rec = g.CurrentReplay;
        check(rec != null && rec.Start.Kind == "campaign" && rec.Start.Seed == g.RunSeed && rec.Frames.Count > 1500, $"every run is recorded as it's played ({rec?.Frames.Count} frames, seed {g.RunSeed})");
        string live = Replay.StateHash(g);
        var played = rec.Play();
        check(Replay.StateHash(played) == live, $"and replays to exactly where it ended: {live}");
        var round = Replay.Decode(rec.Encode());
        check(round != null && round.Frames.Count == rec.Frames.Count && Replay.StateHash(round.Play()) == live, $"through a saved file too ({rec.Encode().Length / 1024} KB for a minute)");
        check(Replay.Decode("HXR1.nonsense") == null && Replay.Decode("hello") == null, "a damaged replay is refused");
        check(played.Replaying && !played.CanSave && played.CurrentReplay == null, "a replay being played records and saves nothing");

        // every kind of start
        (string kind, Action<Game> start)[] starts =
        {
            ("practice", x => x.StartPractice(PClass.Mage, Courses.Descent)),
            ("endless", x => x.StartEndless(PClass.Cleric, 4242)),
            ("arena", x => { x.ArenaMods = ArenaMod.DoubleSpeed; x.StartArena(PClass.Fighter); }),
            ("daily", x => x.StartDaily(new DateOnly(2026, 9, 1))),
            ("rematch", x => x.StartRematch(PClass.Mage, MiniBosses.Keeper)),
            ("campaign", x => x.StartNewGamePlus(PClass.Cleric, 2)),
        };
        bool all = true;
        var bad = new List<string>();
        foreach (var (kind, start) in starts)
        {
            var k = new Game { AchievementsOn = false, Profile = new Profile { NgUnlocked = 2 } };
            k.Vars.Sens = 1.7f; k.Vars.MonsterDamage = 0.15f;
            start(k);
            Bot(k, kind.Length, 20);
            bool same = k.CurrentReplay?.Start.Kind == kind && Replay.StateHash(k.CurrentReplay.Play()) == Replay.StateHash(k);
            if (!same) bad.Add(kind);
            all &= same;
        }
        check(all, "a course, the endless course, the arena, a daily, a rematch and New Game+ all replay exactly" + (bad.Count > 0 ? $" (not {string.Join(", ", bad)})" : ""));
        var restart = new Game { AchievementsOn = false };
        restart.StartPractice(PClass.Fighter, Courses.Hangar);
        Bot(restart, 3, 5);
        restart.NewGame(PClass.Fighter);
        check(restart.CurrentReplay.Frames.Count == 0, "Restart starts a fresh recording");
        restart.GoToTitle();
        check(restart.CurrentReplay == null && restart.LastReplay != null, "and going back to the title keeps the last one to save or watch");

        // watching one, frame by frame on real time
        var w = new Game { AchievementsOn = false };
        w.WatchReplay(rec);
        for (int k = 0; k < 20000 && w.Watch != null; k++) w.WatchStep(default, 1f / 60f);
        check(w.Watch == null && w.Messages.Any(m => m.text == "Replay over."), "watching one plays it through to the end");
        w.WatchReplay(rec);
        w.WatchStep(default, 1f / 60f);
        w.WatchStep(new Input { Pause = true }, 1f / 60f);
        check(w.Watch == null, "Esc stops it");

        // exact ghosts: a lap of the Velocity Hangar, driven by the demo pilot as if it were a player, then made a ghost from its replay
        var lap = new Game { AchievementsOn = false, Profile = new Profile() };
        lap.StartPractice(PClass.Fighter, Courses.Hangar);
        var pilot = new DemoPilot();
        for (int k = 0; k < 72 * 40 && lap.LastRun == 0; k++) lap.Update(pilot.Next(lap, DemoPilot.Tick), DemoPilot.Tick);
        var exact = lap.LastRun > 0 ? Game.GhostFromReplay(lap.CurrentReplay, out _) : null;
        check(exact != null && exact.Verified && exact.Time == lap.LastRun && exact.Course == "hangar" && exact.Track.Points.Count > 20,
              $"a replay of a course makes an exact ghost, its time measured by the game ({exact?.Time:0.00}s)");
        var racer = new Game { AchievementsOn = false, Profile = new Profile() };
        check(exact != null && racer.LoadGhost(exact, out _) && racer.Messages.Any(m => m.text.Contains("verified from a replay")), "and racing it says so");
        check(Game.GhostFromReplay(rec, out string whyNot) == null && whyNot.Contains("isn't of a practice course"), "a campaign replay can't make one");

        // the regression replays: each still ends exactly where it did when it was recorded
        string dir = ReplayTestDir();
        var files = dir == null ? Array.Empty<string>() : Directory.GetFiles(dir, "*.hxreplay").OrderBy(f => f).ToArray();
        // (a release build run on its own has no tests folder: then there's nothing to check)
        check(dir == null || files.Length >= 4, dir == null ? "(no tests/replays here: regression replays skipped)" : $"the regression replays are there ({files.Length} in tests/replays)");
        foreach (var f in files)
        {
            var lines = File.ReadAllLines(f);
            var r = Replay.Decode(lines[0]);
            string want = lines.Length > 1 ? lines[1].Trim() : "";
            string got = r == null ? "(damaged)" : Replay.StateHash(r.Play());
            check(got == want, $"{Path.GetFileNameWithoutExtension(f)} replays as recorded" + (got == want ? "" : $"\n         want {want}\n         got  {got}\n         (if the change is meant, run --update-replays)"));
        }
    }

    /// <summary>tests/replays, found from the working directory or the build output upwards.</summary>
    static string ReplayTestDir()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                if (Directory.Exists(Path.Combine(d.FullName, "tests", "replays"))) return Path.Combine(d.FullName, "tests", "replays");
        return null;
    }

    /// <summary>
    /// --make-replays: records the regression replays afresh (a bot through the hub, the arena and New Game+, the
    /// demo pilot on a course and the endless course) into tests/replays. --update-replays keeps the inputs and just
    /// rewrites each one's expected ending, after a change that's meant to alter how they play.
    /// </summary>
    public static int MakeReplays(bool keepInputs)
    {
        string dir = ReplayTestDir() ?? Path.Combine(Directory.GetCurrentDirectory(), "tests", "replays");
        Directory.CreateDirectory(dir);
        if (keepInputs)
        {
            foreach (var f in Directory.GetFiles(dir, "*.hxreplay"))
            {
                var r = Replay.Decode(File.ReadAllLines(f)[0]);
                if (r == null) { Console.WriteLine("damaged: " + f); continue; }
                File.WriteAllLines(f, new[] { r.Encode(), Replay.StateHash(r.Play()) });
                Console.WriteLine("updated " + f);
            }
            return 0;
        }
        void Save(string name, Game g)
        {
            var r = g.CurrentReplay;
            File.WriteAllLines(Path.Combine(dir, name + ".hxreplay"), new[] { r.Encode(), Replay.StateHash(r.Play()) });
            Console.WriteLine($"wrote {name}: {r.Frames.Count} frames, {Replay.StateHash(g)}");
        }
        var hub = new Game { FixedSeed = 101, AchievementsOn = false, Profile = new Profile() };
        hub.Vars.MonsterDamage = 0.15f;
        hub.NewGame(PClass.Fighter);
        Bot(hub, 101, 90);
        Save("hub-fighter", hub);
        var arena = new Game { FixedSeed = 102, AchievementsOn = false, Profile = new Profile() };
        arena.Vars.MonsterDamage = 0.15f;
        arena.StartArena(PClass.Mage);
        Bot(arena, 102, 60);
        Save("arena-mage", arena);
        var ng = new Game { FixedSeed = 103, AchievementsOn = false, Profile = new Profile { NgUnlocked = 2 } };
        ng.Vars.MonsterDamage = 0.15f;
        ng.StartNewGamePlus(PClass.Cleric, 2);
        Bot(ng, 103, 60);
        Save("ngplus2-cleric", ng);
        var demo = new Game { FixedSeed = 104, AchievementsOn = false, Profile = new Profile() };
        demo.StartPractice(PClass.Fighter, Courses.Descent);
        demo.StartDemo();
        for (int k = 0; k < 60 * 30 && demo.Demo; k++) demo.Update(default, 1f / 60f);
        Save("descent-demo", demo);
        var endless = new Game { FixedSeed = 105, AchievementsOn = false, Profile = new Profile() };
        endless.StartEndless(PClass.Cleric, 1234);
        endless.StartDemo();
        for (int k = 0; k < 60 * 40; k++) endless.Update(default, 1f / 60f);
        Save("endless-1234-demo", endless);
        return 0;
    }
}
