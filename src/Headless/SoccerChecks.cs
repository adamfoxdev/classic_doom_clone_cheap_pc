namespace HexenSharp;

/// <summary>Checks on Rocket Soccer: the pitch and its ramps, the ball's physics, blasts, goals, the clock and the demo.</summary>
public static partial class Headless
{
    static void SoccerChecks(Action<bool, string> check)
    {
        Game g = null;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }
        void Fresh()
        {
            g = new Game { FixedSeed = 1, AchievementsOn = false };
            g.StartPractice(PClass.Fighter, Soccer.Course);
        }

        // the pitch: goal mouths in the end walls under a crossbar, and two kicker ramps rising a quarter step a cell
        Fresh();
        var lv = g.Level;
        var p = g.P;
        bool mouths = Enumerable.Range(Soccer.MouthY0, Soccer.MouthY1 - Soccer.MouthY0 + 1).All(y => !lv.Blocks(1, y) && !lv.Blocks(Soccer.LineE + 1, y)
                          && MathF.Abs(lv.HeightAt(1.5f, y + 0.5f) - Soccer.Crossbar) < 0.01f)
                      && lv.Blocks(1, Soccer.MouthY0 - 1) && lv.Blocks(Soccer.LineE + 1, Soccer.MouthY1 + 1);
        check(mouths, "the pitch has a goal at each end, six cells wide under a crossbar");
        bool ramps = Soccer.Ramps.All(r =>
        {
            var steps = Enumerable.Range(r.X0, r.Length).Select(x => lv.FloorAt(x + 0.5f, r.Y0 + 0.5f)).ToList();
            if (r.Dir < 0) steps.Reverse();
            return steps.Zip(steps.Skip(1)).All(q => MathF.Abs(q.Second - q.First - 0.25f) < 0.01f) && MathF.Abs(steps[^1] - r.Top) < 0.01f
                   && lv.FloorAt(r.Dir > 0 ? r.X1 + 1.5f : r.X0 - 0.5f, r.Y0 + 0.5f) == 0;
        });
        check(Soccer.Ramps.Length == 2 && Soccer.Ramps[0].Dir != Soccer.Ramps[1].Dir && ramps,
            "two kicker ramps in midfield, facing opposite ways, rise a quarter step a cell to two cells and drop off sheer");
        check(g.OnSoccer && p.Weapons.Length == 2 && p.Weapons[0].Rocket && p.Weapons[1].Grenade && g.SafeRockets,
            "you have the rocket launcher and the grenade launcher, and your blasts can't kill you");
        var ball = g.Ball;
        check(ball != null && ball.X == Soccer.SpotX && ball.Y == Soccer.SpotY && g.LitGoal == 1 && !g.RunStarted && g.SoccerLeft == Soccer.Length,
            "the ball waits on the centre spot, the east goal lit, the clock not yet running");
        Tick(new Input(), 70);
        check(ball.Grounded && ball.Z < 0.01f && MathF.Abs(ball.VX) + MathF.Abs(ball.VY) < 0.01f && !g.RunStarted, "dropped on the spot, it bounces and settles");

        // a rocket fired at the ball from behind shoves it on, hard, and starts the clock
        p.X = Soccer.SpotX - 4; p.Y = Soccer.SpotY; p.Angle = 0; p.Pitch = -12; p.Cooldown = 0;
        Tick(new Input { Fire = true });
        float fastest = 0;
        for (int k = 0; k < 20; k++) { Tick(new Input()); fastest = MathF.Max(fastest, ball.VX); }
        check(fastest > 5 && MathF.Abs(ball.VY) < 1 && g.RunStarted && g.SoccerShots == 1, $"a rocket into the ball's back sends it on at {fastest:0.0} cells a second, and starts the clock");

        // a rocket under the ball lifts it
        Fresh(); ball = g.Ball; p = g.P;
        Tick(new Input(), 70);
        var blast = new Projectile { Kind = ProjKind.Rocket, FromPlayer = true, Splash = Rockets.SplashRadius, DmgMin = 100, DmgMax = 120, X = ball.X - 0.3f, Y = ball.Y, Z = 0.05f, Level = g.Level };
        g.Level.Things.Add(blast);
        typeof(Game).GetMethod("Explode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(g, new object[] { blast, null, null });
        float high = 0;
        for (int k = 0; k < 70; k++) { Tick(new Input()); high = MathF.Max(high, ball.Z); }
        check(high > 1.5f, $"a blast under the ball throws it up ({high:0.0} cells)");

        // up a ramp and off the top: into the air
        Fresh(); ball = g.Ball;
        var ramp = Soccer.Ramps[0];
        ball.X = ramp.X0 - 2; ball.Y = ramp.Y0 + 2; ball.Z = 0; ball.Grounded = true; ball.VX = 11; ball.VY = 0; ball.VZ = 0;
        g.P.X = 30; g.P.Y = 20;
        float flight = 0, beyond = 0;
        for (int k = 0; k < 70; k++)
        {
            Tick(new Input());
            if (ball.X > ramp.X1 + 1.5f) { flight = MathF.Max(flight, ball.Z); beyond = MathF.Max(beyond, ball.X); }
        }
        check(flight > 1.8f && beyond > ramp.X1 + 5, $"a ball rolled up a kicker leaves its top in the air ({flight:0.0} cells up, on to x {beyond:0.0})");
        // rolling in from the high side, the ramp's face turns it back
        Fresh(); ball = g.Ball;
        ball.X = ramp.X1 + 3; ball.Y = ramp.Y0 + 2; ball.Z = 0; ball.Grounded = true; ball.VX = -6; ball.VY = 0; ball.VZ = 0;
        Tick(new Input(), 35);
        check(ball.X > ramp.X1 + 1 && ball.VX > 0, "from the high end, the kicker's sheer face bounces it back");

        // a goal in the lit goal counts and lights the other; the ball comes back to the spot
        Fresh(); ball = g.Ball; p = g.P;
        ball.X = Soccer.LineE - 2; ball.Y = Soccer.SpotY; ball.Z = 0; ball.Grounded = true; ball.VX = 6;
        Tick(new Input(), 20);
        check(g.SoccerGoals == 1 && g.LitGoal == 0, "into the lit (east) goal: a goal, and the west goal lights");
        Tick(new Input(), 50);
        ball = g.Ball;
        check(ball != null && !ball.Removed && MathF.Abs(ball.X - Soccer.SpotX) < 0.5f, "and the ball comes back to the centre spot");
        // the wrong one: no goal, just back to the spot
        ball.X = Soccer.LineE - 2; ball.Y = Soccer.SpotY; ball.Z = 0; ball.Grounded = true; ball.VX = 6; ball.VY = 0;
        Tick(new Input(), 80);
        check(g.SoccerGoals == 1 && g.LitGoal == 0 && MathF.Abs(g.Ball.X - Soccer.SpotX) < 0.5f, "into the unlit goal: no goal, back to the spot");
        // over the crossbar it can't go in
        ball = g.Ball;
        ball.X = Soccer.LineW + 3; ball.Y = Soccer.SpotY; ball.Z = Soccer.Crossbar + 0.3f; ball.Grounded = false; ball.VX = -7; ball.VY = 0; ball.VZ = 0;
        Tick(new Input(), 12);
        check(g.SoccerGoals == 1 && ball.X > Soccer.LineW, "a ball flying at the goal above the crossbar bounces off it");

        // running into the ball dribbles it
        Fresh(); ball = g.Ball; p = g.P;
        Tick(new Input(), 70);
        p.X = Soccer.SpotX - 2; p.Y = Soccer.SpotY; p.Angle = 0;
        Tick(new Input { Move = 1 }, 35);
        check(ball.X > Soccer.SpotX + 0.5f && g.RunStarted, $"running into the ball pushes it along (to x {ball.X:0.0})");

        // full time: the match goes on your board, and a new one starts
        Fresh(); ball = g.Ball; p = g.P;
        ball.X = Soccer.LineE - 2; ball.Y = Soccer.SpotY; ball.Z = 0; ball.Grounded = true; ball.VX = 6;
        g.RunStarted = true;
        Tick(new Input(), 20);
        g.SoccerLeft = 0.5f;
        Tick(new Input(), 30);
        check(g.LastSoccer?.Goals == 1 && g.Profile.SoccerBest(PClass.Fighter) == 1 && g.SoccerGoals == 0 && !g.RunStarted && g.SoccerLeft == Soccer.Length && g.LitGoal == 1,
            "at full time the match goes on your board, and the next one waits for its first touch");
        g.Menu.Show(MenuPage.Leaderboard);
        check(g.Menu.BoardSoccer, "the leaderboard opens on Rocket Soccer's board");
        g.Menu.Close();

        // the demo gets behind the ball and scores
        Fresh();
        g.StartDemo();
        int scored = 0;
        for (int k = 0; k < 35 * 60 && g.Demo && scored < 2; k++) { g.Update(new Input(), 1f / 35f); scored = g.SoccerGoals; }
        check(scored >= 2, $"the demo gets behind the ball and blasts it in ({scored} goals in a minute)");
        g.EndDemo();
    }
}
