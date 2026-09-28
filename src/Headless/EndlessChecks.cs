namespace HexenSharp;

/// <summary>Checks on the endless practice course: the seeded platforms, the ramp, falling, and the board.</summary>
public static partial class Headless
{
    static void EndlessChecks(Action<bool, string> check)
    {
        // the plan: the same for a seed, different between seeds, and harder as it goes
        var a = Endless.Plan(1234);
        check(a.SequenceEqual(Endless.Plan(1234)) && !a.SequenceEqual(Endless.Plan(1235)), "a seed always makes the same course, and another seed a different one");
        bool shape = true, ramp = true, floors = true;
        for (int seed = 1; seed <= 200; seed++)
        {
            var pl = Endless.Plan(seed);
            var gaps = pl.Zip(pl.Skip(1), (x, y) => y.x0 - x.x1 - 1).ToArray();
            shape &= pl.Length == Endless.Count && gaps.All(gp => gp is >= 2 and <= 9) && pl.All(p => p.x1 - p.x0 + 1 >= 3);
            ramp &= gaps.Take(3).All(gp => gp <= 3) && gaps.TakeLast(10).Average() > gaps.Take(10).Average() + 3
                    && pl.TakeLast(10).Average(p => p.x1 - p.x0) < pl.Skip(1).Take(10).Average(p => p.x1 - p.x0);
            floors &= pl.All(p => p.floor >= Endless.LowFloor && p.floor <= Endless.TopFloor)
                      && pl.Zip(pl.Skip(1)).All(t => t.Second.floor - t.First.floor <= Level.FloorStep + 0.001f)
                      && pl.Take(4).All(p => p.floor == pl[0].floor);
        }
        check(shape, $"{Endless.Count} platforms, at least 3 long, over gaps of 2 to 9 (200 seeds)");
        check(ramp, "the first gaps are short; the last ten are much wider, on shorter platforms");
        check(floors, "floors stay between 1 and 6.5, level for the first four, rising at most a step at a time");

        var g = new Game { FixedSeed = 1, AchievementsOn = false, Profile = new Profile() };
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }

        // how fast each gap needs you to be: the start is a running jump, the middle strafe jumping, the end out of reach
        g.StartEndless(PClass.Fighter, 1234);
        var plats = g.Course.Platforms;
        int Need(int k) => g.GapSpeedPercent(plats[k + 1].x0 - plats[k].x1 - 1, plats[k].floor - plats[k + 1].floor);
        var needs = Enumerable.Range(0, plats.Length - 1).Select(Need).ToArray();
        int reachable = Array.FindIndex(needs, n => n > g.Vars.MaxHop * 100);
        check(needs.Take(3).All(n => n <= 100) && reachable >= 20 && reachable < plats.Length - 1,
              $"seed 1234: the first gaps take a running jump; strafe jumping reaches platform {reachable}; past that it's beyond the top speed ({string.Join(" ", needs)})");

        // starting it
        var lv = g.Level;
        var p = g.P;
        check(g.OnEndless && g.Course.Seed == 1234 && lv.RawName == "Endless 1234" && p.FloorZ == plats[0].floor && lv.Checkpoints.Count == plats.Length,
              "'endless 1234' builds seed 1234's course, a checkpoint on each platform");
        check(!g.RunStarted && g.EndlessReached == 0, "the run hasn't started on the first platform");

        void Land(int k)
        {
            p.X = (plats[k].x0 + plats[k].x1) / 2f + 0.5f; p.Y = 5.5f; p.FloorZ = plats[k].floor; p.Z = 0; p.VZ = 0; p.VX = p.VY = 0;
            Tick(default, 2);
        }
        Land(1); Land(2); Land(3);
        check(g.EndlessReached == 3 && g.RunStarted, "landing on platforms counts them, and starts the run");
        check(g.Messages.Any(m => m.text.StartsWith("Platform 3. The next gap is")), "each platform says how fast you'll need to be for the next gap");

        // a fall ends the run: it goes on the board and you start again at the top
        Tick(default, 35);
        p.X = plats[3].x1 + 1.5f; p.FloorZ = 0; p.Z = 0.3f;
        Tick(default, 20);
        var runs = g.Profile.EndlessBoard(PClass.Fighter);
        check(runs.Count == 1 && runs[0].Platforms == 3 && runs[0].Seed == 1234 && runs[0].Time > 0.9f && g.LastEndlessPlace == 1,
              "falling in ends the run: 3 platforms on seed 1234 goes on the board");
        check(g.EndlessReached == 0 && !g.RunStarted && p.X < plats[0].x1 && p.FloorZ == plats[0].floor, "and you're back on the first platform for another go");
        check(g.Messages.Any(m => m.text.Contains("Fell after platform 3") && m.text.Contains("new best")), "it says how far you got");

        // a fall at the first gap, or at a slow game speed, doesn't count
        p.X = plats[0].x1 + 1.5f; p.FloorZ = 0; p.Z = 0.3f;
        Tick(default, 20);
        g.PracticeSpeed = 0.5f;
        Land(1); Land(2); Land(3); Land(4);
        p.X = plats[4].x1 + 1.5f; p.FloorZ = 0; p.Z = 0.3f;
        Tick(default, 20);
        g.PracticeSpeed = 1f;
        check(g.Profile.EndlessRuns.Count == 1, "falling at the first gap, or at half speed, isn't recorded");

        // the board: most platforms first, then quickest, ten a class
        var pr = new Profile();
        for (int i = 0; i < 14; i++) pr.AddEndlessRun(new EndlessRun { Name = "x", Class = "Fighter", Platforms = i % 7, Time = 100 - i, Seed = i + 1 });
        pr.AddEndlessRun(new EndlessRun { Name = "y", Class = "Mage", Platforms = 2, Time = 5 });
        var board = pr.EndlessBoard(PClass.Fighter);
        check(board.Count == 10 && board[0].Platforms == 6 && board[0].Time == 87 && board.Zip(board.Skip(1)).All(t => t.First.Platforms > t.Second.Platforms || t.First.Platforms == t.Second.Platforms && t.First.Time <= t.Second.Time)
              && pr.EndlessRuns.Count == 11 && pr.EndlessBest(PClass.Mage) == 2,
              "the board ranks by platforms, then time, and keeps each class's best ten");

        // going all the way
        g.StartEndless(PClass.Cleric, 99);
        plats = g.Course.Platforms; p = g.P;
        Tick(default);
        for (int k = 1; k < plats.Length; k++) Land(k);
        var exit = g.Level.FindMark('E').Value;
        p.X = exit.x; p.Y = exit.y;
        Tick(default);
        check(g.LastEndless?.Platforms == plats.Length - 1 && g.LastEndless.Seed == 99 && g.EndlessReached == 0 && g.Messages.Any(m => m.text.StartsWith("You made it to the end")),
              $"reaching the exit counts all {plats.Length - 1} platforms past the start, and starts again");

        // from the menus and the console
        g.GoToTitle();
        g.Menu.Show(MenuPage.Main);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Main), "Practice");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Courses), "Endless");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        Tick(new Input { Slot = 3 });
        int first = g.Course.Seed;
        check(g.OnEndless && first > 0 && g.P.Class == PClass.Mage, "Practice > Endless > a class starts it on a fresh seed");
        g.Paused = true; g.Menu.Show(MenuPage.Pause);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Pause), "Restart");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.OnEndless && g.Course.Seed == first, "Restart keeps the seed");
        g.Con.Execute("endless 4242", quiet: true);
        check(g.OnEndless && g.Course.Seed == 4242 && g.Level.RawName == "Endless 4242", "'endless 4242' plays that seed");
        g.Con.Execute("endless", quiet: true);
        check(g.OnEndless && g.Course.Seed != 4242, "'endless' on its own rolls a new one");
        g.Con.Execute("endless -3");
        check(g.Con.Log.Last().Contains("whole number above 0"), "and a bad seed is refused");

        // the leaderboard opens on the endless board there
        g.Paused = true; g.Menu.Show(MenuPage.Pause); g.Menu.Show(MenuPage.Leaderboard);
        check(g.Menu.BoardEndless, "on the endless course, the leaderboard opens on its board");
        g.Menu.Close(); g.Paused = false;

        // Into the Unknown at platform 25
        var ach = new Game { FixedSeed = 1, Profile = new Profile() };
        check(Achievements.Find("endless_25").Progress(ach) == (0, 25), "Into the Unknown counts toward platform 25");
        ach.Profile.AddEndlessRun(new EndlessRun { Name = "x", Class = "Cleric", Platforms = 25, Time = 60 });
        Achievements.Check(ach);
        check(ach.Profile.Achievements.ContainsKey("endless_25"), "and unlocks when a run reaches it");

        // the demo can play it too: it gets a good way before the gaps outgrow it
        var d = new Game { FixedSeed = 1 };
        d.StartEndless(PClass.Fighter, 1234);
        d.StartDemo();
        int most = 0;
        for (int f = 0; f < 60 * 90 && d.Demo; f++) { d.Update(default, 1f / 60f); most = Math.Max(most, d.EndlessReached); }
        check(most >= 5, $"the demo plays the endless course (reaching platform {most} in 90 seconds)");
    }
}
