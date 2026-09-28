namespace HexenSharp;

/// <summary>Checks on Fishing: the lake and the dock, casting, bites and strikes, reeling and snapping, the catch, and the session.</summary>
public static partial class Headless
{
    static void FishingChecks(Action<bool, string> check)
    {
        Game g = null;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }
        void Fresh()
        {
            g = new Game { FixedSeed = 1, AchievementsOn = false };
            g.StartPractice(PClass.Fighter, Fishing.Course);
            Tick(new Input(), 10); // (the rod up)
        }
        void Press() { Tick(new Input { Fire = true }); Tick(new Input()); }
        bool Until(Func<bool> done, int max) { for (int k = 0; k < max && !done(); k++) Tick(new Input()); return done(); }
        // reel as a careful angler does: hold fire, letting go while it runs or the line's tight
        bool Reel(int max)
        {
            for (int k = 0; k < max && g.Fish == FishState.Reeling; k++) Tick(new Input { Fire = !g.FishRunning && g.LineTension < 0.7f });
            return g.Fish != FishState.Reeling;
        }

        // the lake: water sunk a cell below the shore, and a dock out into it at the shore's height, where you start
        Fresh();
        var lv = g.Level;
        var p = g.P;
        int water = lv.Water.Count(w => w);
        bool sunk = Enumerable.Range(0, lv.W * lv.H).All(i => !lv.Water[i] || lv.Floors[i] == 0);
        check(g.OnFishing && water > 300 && sunk && lv.FloorAt(3.5f, 28.5f) == Fishing.Bank && lv.FloorAt(Fishing.DockX + 0.5f, Fishing.DockY0 + 0.5f) == Fishing.Bank
              && Fishing.Water(Fishing.DockX, Fishing.DockY0 - 1) && lv.Theme.Water != null,
            $"a lake of {water} water cells lies a cell below the shore, with a dock out into it at the shore's height");
        check(p.Weapons.Length == 1 && p.CurWeapon.Rod && MathF.Abs(p.Y - Fishing.StartY) < 0.6f && Fishing.OnDock((int)p.X, (int)p.Y),
            "you start at the end of the dock, a fishing rod in hand");

        // a cast: the bobber arcs out and lands on the water, and the clock starts
        p.Pitch = 20;
        Press();
        check(g.Fish == FishState.Casting && g.Bobber != null && g.RunStarted, "fire casts the bobber, and starts the clock");
        Until(() => g.Fish != FishState.Casting, 70);
        float dist = Dist(g.Bobber, p);
        check(g.Fish == FishState.Waiting && lv.Water[(int)g.Bobber.Y * lv.W + (int)g.Bobber.X] && dist > 4, $"it lands on the water {dist:0.0} cells out, and waits for a bite");
        // looking up casts further
        Press();
        check(g.Fish == FishState.Idle && g.Bobber == null, "fire while it waits reels it back in");
        Tick(new Input(), 15);
        p.Pitch = 55;
        Press();
        Until(() => g.Fish != FishState.Casting, 90);
        float far = Dist(g.Bobber, p);
        check(far > dist + 1.2f, $"looking higher casts further ({far:0.0} cells)");

        // a bite: the bobber's pulled under; fire in time and it's hooked
        check(Until(() => g.Fish == FishState.Bite, 35 * 15), "a fish bites before long");
        Tick(new Input());
        float under = g.Bobber.Z;
        Tick(new Input { Fire = true });
        check(under < 0 && g.Fish == FishState.Reeling && g.Hooked != null, $"the bobber's tugged under ({under:0.00}); fire strikes, and a {g.Hooked?.Name} is on the line");
        // careless reeling (fire held through its runs) snaps the line
        g.Hooked = Fishing.All.Single(s => s.Name == "Catfish"); g.HookedKg = 10; g.LineOut = 12;
        for (int k = 0; k < 35 * 8 && g.Fish == FishState.Reeling; k++) Tick(new Input { Fire = true });
        check(g.Fish == FishState.Idle && g.FishCount == 0, "holding fire through its runs snaps the line: it's gone");

        // a bite missed: the fish lets go, and the bobber waits for the next
        Tick(new Input(), 30);
        p.Pitch = 30;
        Press();
        Until(() => g.Fish == FishState.Bite, 35 * 15);
        Tick(new Input(), (int)(Fishing.StrikeWindow * 35) + 3);
        check(g.Fish == FishState.Waiting, "too slow to strike, and it lets go; the bobber waits on");

        // careful reeling lands it: its weight to the session, its card on the screen
        Until(() => g.Fish == FishState.Bite, 35 * 15);
        Tick(new Input { Fire = true });
        g.Hooked = Fishing.All.Single(s => s.Name == "Bass"); g.HookedKg = 2.4f;
        bool done = Reel(35 * 40);
        check(done && g.FishCount == 1 && MathF.Abs(g.FishKg - 2.4f) < 0.01f && g.LandedShow > 0 && g.Landed?.Name == "Bass" && g.BiggestName == "Bass",
            $"easing off while it runs lands it: a bass of 2.40 kg ({g.FishKg:0.00} kg landed)");
        // an old boot weighs nothing
        Tick(new Input(), 30);
        Press();
        Until(() => g.Fish == FishState.Bite, 35 * 20);
        Tick(new Input { Fire = true });
        g.Hooked = Fishing.All.Single(s => s.Junk); g.HookedKg = 0;
        Reel(35 * 40);
        check(g.FishCount == 1 && MathF.Abs(g.FishKg - 2.4f) < 0.01f && g.Landed?.Junk == true, "an old boot comes up too, and counts for nothing");

        // out deep, bigger fish
        var rng = new Random(3);
        float Avg(float depth) => Enumerable.Range(0, 800).Average(_ => Fishing.Roll(depth, (float)rng.NextDouble(), (float)rng.NextDouble()).kg);
        float shallow = Avg(0.5f), deep = Avg(8f);
        check(deep > shallow * 2, $"further out the fish are bigger (on average {shallow:0.00} kg by the shore, {deep:0.00} kg out deep)");

        // fall in, and you're back on the dock
        p.X = 10.5f; p.Y = 12.5f; p.FloorZ = 0; p.Z = 0;
        Tick(new Input(), 3);
        check(Fishing.OnDock((int)p.X, (int)p.Y), "fall in the lake and you're back on the dock");

        // the session's end: on the board, and a fresh one
        g.FishingLeft = 0.2f;
        Tick(new Input(), 10);
        check(g.LastFishing?.Kg == 2.4f && g.LastFishing.Fish == 1 && g.Profile.FishingBest(PClass.Fighter) == 2.4f && g.FishKg == 0 && !g.RunStarted && g.FishingLeft == Fishing.Length,
            "at time up the session goes on your board, and a fresh one waits for your first cast");
        g.Menu.Show(MenuPage.Leaderboard);
        check(g.Menu.BoardFishing, "the leaderboard opens on Fishing's board");
        g.Menu.Close();

        // the demo casts, strikes and reels them in
        Fresh();
        g.StartDemo();
        for (int k = 0; k < 35 * 90 && g.Demo && g.FishCount < 2; k++) g.Update(new Input(), 1f / 35f);
        check(g.FishCount >= 2, $"the demo casts, strikes and reels fish in ({g.FishCount} landed, {g.FishKg:0.00} kg)");
        g.EndDemo();
    }

    static float Dist(Thing t, Player p) => MathF.Sqrt((t.X - p.X) * (t.X - p.X) + (t.Y - p.Y) * (t.Y - p.Y));
}
