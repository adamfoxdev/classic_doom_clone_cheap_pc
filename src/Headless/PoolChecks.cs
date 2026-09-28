namespace HexenSharp;

/// <summary>Checks on Rocket Pool: the table and its pockets, the rack, balls knocking into each other, pots, the 8, the clock and the demo.</summary>
public static partial class Headless
{
    static void PoolChecks(Action<bool, string> check)
    {
        Game g = null;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }
        void Fresh()
        {
            g = new Game { FixedSeed = 1, AchievementsOn = false };
            g.StartPractice(PClass.Fighter, Pool.Course);
        }
        SoccerBall Only(int n)
        {
            foreach (var b in g.PoolBalls) if (b.Number != n) b.Removed = true;
            g.Level.Things.RemoveAll(t => t is SoccerBall { Removed: true });
            g.PoolBalls.RemoveAll(b => b.Removed);
            return g.PoolBalls.Single();
        }
        void Roll(SoccerBall b, float x, float y, float vx, float vy) { b.X = x; b.Y = y; b.Z = Pool.Table; b.Grounded = true; b.VX = vx; b.VY = vy; b.VZ = 0; }

        // the table: felt at 0.75 up, cushions all round, and six pockets sunk to the floor
        Fresh();
        var lv = g.Level;
        var p = g.P;
        bool pockets = Pool.PocketCells.Length == 6 && Pool.PocketCells.All(pc => pc.All(c => lv.Floors[c.y * lv.W + c.x] == 0 && !lv.Blocks(c.x, c.y)));
        check(pockets && lv.FloorAt(20.5f, 12.5f) == Pool.Table && lv.Blocks(5, 1) && lv.Blocks(Pool.W - 2, 12),
            "the table stands 0.75 up inside its cushions, with six pockets sunk to the floor: four corners and two sides");
        check(g.OnPool && p.Weapons.Length == 2 && p.Weapons[0].Rocket && p.Weapons[1].Grenade && g.SafeRockets,
            "you have the rocket launcher and the grenade launcher for a cue, and your blasts can't kill you");
        var balls = g.PoolBalls;
        bool apart = balls.All(a => balls.All(b => a == b || MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) >= a.Radius + b.Radius - 0.001f));
        var eight = balls.Single(b => b.Number == 8);
        check(balls.Count == 10 && balls.Select(b => b.Number).Distinct().Count() == 10 && apart && MathF.Abs(eight.Y - Pool.FootY) < 0.01f
              && balls.Min(b => b.X) == Pool.FootX && !g.RunStarted,
            "ten balls are racked in a triangle on the foot spot, the 8 in the middle of the third row, the clock waiting");
        Tick(new Input(), 35);
        check(balls.All(b => MathF.Abs(b.VX) + MathF.Abs(b.VY) < 0.01f && MathF.Abs(b.Z - Pool.Table) < 0.01f), "the rack sits still on the felt");

        // a ball rolled into another stops, and the other goes on (nearly all the speed passes across)
        Fresh();
        var a1 = g.PoolBalls.Single(b => b.Number == 1);
        var cue = g.PoolBalls.Single(b => b.Number == 2);
        foreach (var b in g.PoolBalls.Where(b => b != a1 && b != cue)) b.Removed = true;
        g.PoolBalls.RemoveAll(b => b.Removed);
        Roll(a1, 18, 12, 0, 0);
        Roll(cue, 14, 12, 6, 0);
        Tick(new Input(), 40);
        check(MathF.Abs(cue.VX) < 0.8f && a1.VX > 3.5f && a1.X > 18.5f, $"a ball rolled straight into another stops ({cue.VX:0.0}) and sends it on ({a1.VX:0.0})");

        // a rocket blast shoves a ball, and starts the clock
        Fresh();
        var one = Only(1);
        Tick(new Input(), 35); // (the launcher up)
        Roll(one, 20, 12, 0, 0);
        p = g.P; p.X = 15; p.Y = 12; p.Angle = 0; p.Pitch = -10; p.Cooldown = 0;
        Tick(new Input { Fire = true });
        float fastest = 0;
        for (int k = 0; k < 20; k++) { Tick(new Input()); fastest = MathF.Max(fastest, one.VX); }
        check(fastest > 5 && g.RunStarted && g.PoolShots == 1, $"a rocket into a ball sends it off at {fastest:0.0} cells a second, and starts the clock");

        // into a corner pocket: potted
        Fresh();
        var three = g.PoolBalls.Single(b => b.Number == 3);
        Roll(three, 6, 6, -5, -5);
        Tick(new Input(), 40);
        check(three.Removed && g.PoolPotted == 1 && g.PoolBalls.Count(b => !b.Removed) == 9, "a ball rolled into a corner pocket drops in: one potted, nine left");
        // and a side pocket
        var four = g.PoolBalls.Single(b => b.Number == 4);
        Roll(four, Pool.MidX + 0.5f, 6, 0, -6);
        Tick(new Input(), 40);
        check(four.Removed && g.PoolPotted == 2, "and one rolled into a side pocket");

        // the 8 early: a foul, ten seconds on, and back on the spot
        eight = g.PoolBalls.Single(b => b.Number == 8 && !b.Removed);
        g.RunStarted = true;
        float before = g.PoolTime;
        Roll(eight, 6, Pool.H - 6, -5, 5);
        Tick(new Input(), 90);
        var back = g.PoolBalls.SingleOrDefault(b => b.Number == 8 && !b.Removed);
        check(g.PoolFouls == 1 && g.PoolTime >= before + Pool.Penalty && back != null && MathF.Abs(back.Y - Pool.FootY) < 0.01f && g.PoolPotted == 2,
            "potting the 8 before the rest is a foul: +10s, and it's back on the foot spot");

        // clearing the table: the last ball, then the 8, and the time goes on the board
        Fresh();
        foreach (var b in g.PoolBalls.Where(b => b.Number != 8 && b.Number != 5)) b.Removed = true;
        g.PoolBalls.RemoveAll(b => b.Removed);
        g.RunStarted = true;
        var five = g.PoolBalls.Single(b => b.Number == 5);
        Roll(five, 6, 6, -5, -5);
        Tick(new Input(), 40);
        eight = g.PoolBalls.Single(b => b.Number == 8);
        Roll(eight, Pool.W - 6, 6, 5, -5);
        Tick(new Input(), 40);
        check(g.LastPool != null && g.LastPool.Time > 0 && g.Profile.PoolBest(PClass.Fighter) == g.LastPool.Time && g.PoolBalls.All(b => b.Removed),
            $"the 8 last clears the table: {g.LastPool?.Time:0.0}s on your board");
        Tick(new Input(), 35 * 3 + 5);
        check(g.PoolBalls.Count(b => !b.Removed) == 10 && g.PoolTime == 0 && !g.RunStarted, "and the table racks again a moment later");
        g.Menu.Show(MenuPage.Leaderboard);
        check(g.Menu.BoardPool, "the leaderboard opens on Rocket Pool's board");
        g.Menu.Close();

        // you can drop into a pocket and jump back out
        Fresh();
        p = g.P;
        p.X = 2.5f; p.Y = 1.5f; p.Angle = 0;
        Tick(new Input(), 10);
        float inPocket = p.FloorZ;
        p.Angle = MathF.PI / 2;
        for (int k = 0; k < 40; k++) Tick(new Input { Move = 1, Jump = k % 12 == 0 });
        check(inPocket == 0 && p.FloorZ == Pool.Table, "you can step down into a pocket and jump back out onto the table");

        // the demo gets behind balls and pots them
        Fresh();
        g.StartDemo();
        int potted = 0;
        for (int k = 0; k < 35 * 90 && g.Demo && potted < 4; k++) { g.Update(new Input(), 1f / 35f); potted = Math.Max(potted, g.PoolPotted); }
        check(potted >= 4, $"the demo lines balls up with pockets and blasts them in ({potted} inside 90 seconds)");
        g.EndDemo();
    }
}
