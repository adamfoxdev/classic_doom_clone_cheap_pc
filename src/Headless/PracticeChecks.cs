namespace HexenSharp;

/// <summary>Checks on Quake movement and practice: the courses, ghosts, the demo, medals and the strafe helper.</summary>
public static partial class Headless
{
    static void QuakeMoveChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster);
        g.Vars.NoClip = true; // open space to run circles in
        var p = g.P;
        float fps = 35;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / fps); }
        float run = g.RunSpeed;
        check(g.Vars.QuakeMove, "Quake movement is on by default");

        // on the ground you top out at your run speed, and coast a little when you let go
        Tick(new Input { Move = 1 }, 35);
        check(MathF.Abs(p.HSpeed - run) < 0.01f, $"running tops out at your run speed ({p.HSpeed:0.00} of {run:0.00})");
        float x0 = p.X, y0 = p.Y;
        Tick(default, 35);
        float coast = MathF.Sqrt((p.X - x0) * (p.X - x0) + (p.Y - y0) * (p.Y - y0));
        check(coast > 0.1f && coast < 0.8f && p.HSpeed == 0, $"let go and you slide to a stop ({coast:0.00} units)");

        // pushing straight ahead in the air adds nothing
        Tick(new Input { Move = 1 }, 35);
        Tick(new Input { Move = 1, Jump = true });
        float peak = 0;
        while (!p.OnGround) { Tick(new Input { Move = 1 }); peak = MathF.Max(peak, p.HSpeed); }
        check(peak <= run * 1.001f, "holding forward in the air doesn't speed you up");

        // strafe jumping: strafe while turning with your velocity, hop the moment you land
        float StrafeHops(int hops)
        {
            Tick(new Input { Move = 1 }, (int)fps);
            for (int h = 0; h < hops; h++)
            {
                Tick(new Input { Jump = true, Strafe = 1 });
                // turn with the mouse to keep up with your velocity as it swings round (35 frames a second, like a slow PC)
                while (!p.OnGround)
                {
                    // a good strafer: wish direction just past square-on to the velocity, at Quake's best angle
                    float amax = g.Vars.AirAccel * run / 72f, v = MathF.Max(p.HSpeed, 0.01f);
                    float best = MathF.Asin(Math.Clamp((amax - 0.094f * run) / v, 0f, 1f));
                    float want = MathF.Atan2(p.VY, p.VX) + best, turn = MathF.IEEERemainder(want - p.Angle, MathF.Tau);
                    Tick(new Input { Strafe = 1, LookX = turn / (0.0025f * g.Vars.Sens) });
                }
            }
            return p.HSpeed;
        }
        float after5 = StrafeHops(5);
        check(after5 > run * 1.25f, $"five strafe jumps build speed well past a run ({after5 / run * 100:0}%)");
        // the same five hops at 120 frames a second, from a standstill: about the same gain
        Tick(default, 70);
        fps = 120;
        float fast5 = StrafeHops(5);
        fps = 35;
        check(MathF.Abs(fast5 - after5) < after5 * 0.1f, $"and about the same at 120 frames a second ({fast5 / run * 100:0}%)");
        float after20 = StrafeHops(20);
        check(after20 > after5 && after20 <= run * g.Vars.MaxHop + 0.001f, $"it keeps building, up to the cap ({after20 / run * 100:0}% of {g.Vars.MaxHop * 100:0}%)");
        var r = new Renderer();
        r.Render(g);
        check(r.Fb.Skip(Renderer.W * (r.ViewH / 2 + 20)).Take(Renderer.W * 8).Distinct().Count() > 1, "a speed readout shows while you're past your run speed");

        // land and stop hopping: ground friction bleeds it back to a run
        Tick(new Input { Move = 1 }, 35);
        p.X = g.Level.StartX; p.Y = g.Level.StartY; p.FloorZ = g.Level.FloorUnder(p.X, p.Y, p.Radius); // back out of the walls
        check(MathF.Abs(p.HSpeed - run) < 0.01f, "stop hopping and friction brings you back to a run");

        // a jump pressed just before you land still counts
        Tick(new Input { Jump = true });
        while (p.Z > 0.08f || p.VZ > 0) Tick(default);
        Tick(new Input { Jump = true });
        int wait = 0;
        while (!p.OnGround && wait++ < 10) Tick(default);
        Tick(default);
        check(!p.OnGround && p.VZ > 0, "a jump pressed just before landing hops as soon as you touch down");
        while (!p.OnGround) Tick(default);

        // Jump never lights the jetpack, even held, so hopping is safe with one on; its own key does
        p.HasJetpack = true; p.Fuel = p.MaxFuel;
        Tick(new Input { Jump = true, JumpHeld = true });
        bool lit = false;
        while (!p.OnGround) { Tick(new Input { JumpHeld = true }); lit |= p.Flying; }
        check(!lit, "holding Jump is just a jump, even with a jetpack");
        Tick(new Input { JetHeld = true });
        check(p.Flying, "the jetpack key takes off");
        for (int k = 0; k < 175 && p.Flying; k++) Tick(new Input { SlideHeld = true });

        // classic movement: you go exactly where the keys say and stop dead
        g.Con.Execute("quakemove 0");
        Tick(new Input { Move = 1 }, 10);
        x0 = p.X; y0 = p.Y;
        Tick(default, 5);
        check(!g.Vars.QuakeMove && p.X == x0 && p.Y == y0, "'quakemove 0' brings back classic movement: you stop dead");
        g.Vars.QuakeMove = true;

        // the options menu toggles it, and it's saved
        g.Menu.Show(MenuPage.Options);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Options), "Movement");
        check(g.Menu.Value(g.Menu.Cursor) == "QUAKE", "Options shows Movement: QUAKE");
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(!g.Vars.QuakeMove && g.Menu.Value(g.Menu.Cursor) == "CLASSIC", "and switches it to CLASSIC");
        check(Settings.Lines(g).Contains("quakemove 0"), "the choice is saved with the settings");
        g.Menu.Close();
    }

    static void PracticeChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        float fps = 35;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / fps); }
        g.GoToTitle();
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Main), "Practice");
        check(g.Menu.Cursor == 1, "Practice sits under New game on the title menu");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Menu.Page == MenuPage.Courses && g.Menu.Items(MenuPage.Courses).SequenceEqual(new[] { "Velocity Hangar", "Descent", "Circuit", "Endless", "Shooting Range", "Free Roam", "Back" }),
              "it lists the courses: Velocity Hangar, Descent, Circuit, Endless, Shooting Range and Free Roam");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Mode == GameMode.ClassSelect, "picking one asks for a class, since each runs at its own speed");
        Tick(new Input { Confirm = true });
        var p = g.P;
        var lv = g.Level;
        var plats = Maps.CoursePlatforms;
        check(g.Practicing && lv.RawName == "Velocity Hangar" && p.Class == PClass.Fighter && p.FloorZ == 2.5f,
              "picking the Marine starts the Velocity Hangar course, on the first platform");
        Tick(default);
        check(g.Messages.Any(m => m.text.Contains("hold A or D and turn the mouse")), "the first platform tells you how to strafe jump");

        // the layout: raised platforms over gaps of 2 to 5 cells, every gap floor a lift pad, a checkpoint on each platform
        var gaps = plats.Zip(plats.Skip(1), (a, b) => b.x0 - a.x1 - 1).ToArray();
        check(gaps.SequenceEqual(new[] { 2, 4, 5, 6 }), $"four gaps, 2 to 6 wide ({string.Join(", ", gaps)})");
        bool lifts = true;
        for (int y = 1; y < lv.H - 1; y++)
            for (int x = 1; x < lv.W - 1; x++)
            {
                int i = y * lv.W + x;
                var on = plats.Where(pl => x >= pl.x0 && x <= pl.x1).ToList();
                lifts &= on.Count > 0 ? lv.Floors[i] == on[0].floor && lv.Marks[i] != '=' : lv.Floors[i] == 0 && lv.Marks[i] == '=';
            }
        check(lifts && plats[^1].floor < plats[0].floor, "the platforms stand 2.5 up (the finish a step lower) and every gap floor is a lift pad");
        check(lv.Checkpoints.Count == plats.Length, "each platform has a checkpoint");

        // each gap: a running jump off the edge at a given speed, no keys held in the air
        bool Clears(int k, float speed)
        {
            var (a, b) = (plats[k], plats[k + 1]);
            g.Level.CheckpointsReached.Clear(); g.Checkpoint = null; // so a miss stays down in the gap
            p.X = a.x1 + 1 + p.Radius - 0.01f; p.Y = 5.5f; p.Angle = 0; p.Z = 0; p.VZ = 0; p.FloorZ = a.floor;
            p.VX = speed; p.VY = 0;
            Tick(new Input { Jump = true });
            for (int t = 0; t < 2 * fps && !p.OnGround; t++) Tick(default);
            return p.OnGround && p.FloorZ == b.floor && p.X > b.x0 - p.Radius - 0.01f;
        }
        float run = g.RunSpeed;
        check(Clears(0, run), "a plain running jump clears the first gap");
        check(!Clears(1, run), "but not the second: that takes speed");
        bool allGood = true, allTight = true;
        for (int k = 0; k < gaps.Length; k++)
        {
            float need = g.GapSpeedPercent(gaps[k], plats[k].floor - plats[k + 1].floor) / 100f * run;
            bool ok = Clears(k, need);
            allGood &= ok;
            allTight &= k == 0 || !Clears(k, need * 0.8f);
        }
        check(allGood, "each gap clears at the speed its platform's hint gives");
        fps = 120;
        allGood = true;
        for (int k = 0; k < gaps.Length; k++) allGood &= Clears(k, g.GapSpeedPercent(gaps[k], plats[k].floor - plats[k + 1].floor) / 100f * run);
        fps = 35;
        check(allGood, "at 120 frames a second too");
        check(allTight, "and falls short well below it");
        int Hardest(Game gm) => Enumerable.Range(0, gaps.Length).Max(k => gm.GapSpeedPercent(gaps[k], plats[k].floor - plats[k + 1].floor));
        int hardest = Hardest(g);
        check(hardest <= g.Vars.MaxHop * 100 * 0.7f, $"the hardest gap needs {hardest}%, well inside the strafe-jumping cap");
        foreach (var cls in new[] { PClass.Cleric, PClass.Mage })
        {
            var g2 = new Game { FixedSeed = 1 };
            g2.StartPractice(cls);
            check(Hardest(g2) > hardest && Hardest(g2) <= g2.Vars.MaxHop * 100 * 0.8f,
                  $"slower classes need more ({g2.P.Def.Name}: {Hardest(g2)}%), still within reach");
        }

        // falling in: the lift takes you back to the last platform you reached (they're all level, so the newest)
        g.Level.CheckpointsReached.Clear(); g.Checkpoint = null;
        p.X = plats[1].x0 + 2.5f; p.Y = 5.5f; p.FloorZ = 2.5f; p.Z = 0; p.VX = p.VY = 0;
        Tick(default, 2);
        g.Messages.Clear();
        p.X = plats[2].x0 + 2.5f; p.Y = 5.5f; p.FloorZ = 2.5f; p.Z = 0; p.VX = p.VY = 0;
        Tick(default, 2);
        check(g.Messages.Any(m => m.text.Contains("next gap is 5 wide") && m.text.Contains("Zig-zag")), "reaching platform 3 names the next gap and the speed it needs");
        p.X = plats[2].x1 + 2.5f; p.FloorZ = 0; p.Z = 0.4f;
        Tick(default, 20);
        check(p.FloorZ == 2.5f && p.X > plats[2].x0 && p.X < plats[2].x1, "dropping into a gap, the lift carries you back to platform 3");

        // the clock: waits for you to move, runs, and the exit ends the run and keeps your best
        g.StartPractice(PClass.Fighter);
        Tick(default, 35);
        check(g.RunTime == 0 && !g.RunStarted, "the clock waits until you set off");
        Tick(new Input { Move = 1 }, 35);
        check(g.RunStarted && g.RunTime > 0.5f, "and runs once you do");
        var r = new Renderer();
        r.Render(g);
        g.Messages.Clear();
        var bare = new Renderer();
        g.Practicing = false; bare.Render(g); g.Practicing = true;
        r.Render(g);
        int Corner(Renderer rr) => Enumerable.Range(3, 8).Sum(y => Enumerable.Range(Renderer.W - 80, 76).Count(x => rr.Fb[y * Renderer.W + x] == Col.Rgb(240, 236, 220)));
        check(Corner(r) > 20 && Corner(bare) == 0, "the run's time shows in the top-right corner");
        var exit = lv.FindMark('E');
        p = g.P;
        p.X = exit.Value.x - 1; p.Y = exit.Value.y; p.FloorZ = plats[^1].floor; p.Angle = 0;
        g.Messages.Clear();
        Tick(new Input { Move = 1 }, 20);
        check(g.RunStarted && g.LastPlace == 0 && g.Messages.Any(m => m.text.StartsWith("Reach every checkpoint first")),
              "the exit won't finish a run that skipped checkpoints");
        g.Level.CheckpointsReached.UnionWith(Enumerable.Range(0, g.Level.Checkpoints.Count)); // as if you'd come the long way
        p.X = exit.Value.x - 1; p.Y = exit.Value.y; p.FloorZ = plats[^1].floor; p.Angle = 0;
        float time = g.RunTime;
        for (int k = 0; k < 35 && g.RunStarted; k++) Tick(new Input { Move = 1 });
        float best = g.Profile.CourseBestTime("Fighter");
        check(best > time && g.Messages.Any(m => m.text.Contains("a new best")), $"the exit finishes the run: {best:0.00}s, a new best");
        check(MathF.Abs(p.X - g.Level.StartX) < 0.01f && g.RunTime == 0 && g.Mode == GameMode.Playing, "and puts you back at the start for another go");
        check(g.Level.CheckpointsReached.Count <= 1 && g.Checkpoint == null || g.Checkpoint.X < plats[0].x1, "with the checkpoints reset");
        Tick(new Input { Move = 1 }, 10);
        g.RunTime = best + 5;
        g.Level.CheckpointsReached.UnionWith(Enumerable.Range(0, g.Level.Checkpoints.Count)); // as if you'd come the long way
        p.X = exit.Value.x - 1; p.Y = exit.Value.y; p.FloorZ = plats[^1].floor; p.Angle = 0;
        for (int k = 0; k < 35 && g.RunStarted; k++) Tick(new Input { Move = 1 });
        check(g.Profile.CourseBestTime("Fighter") == best && g.Messages.Any(m => m.text.Contains($"(best {best:0.00}s)")), "a slower run keeps your best, and tells you it");

        // the leaderboard: every finished run goes on its class's board, quickest first, under your name
        var board = g.Profile.Board("Fighter");
        check(board.Count == 2 && board[0].Time == best && board[1].Time > best && board.All(r => r.Name == g.RunnerName && r.When != default),
              "both runs are on the Marine's leaderboard, quickest first, with your name and the date");
        check(g.LastPlace == 2 && g.Messages.Any(m => m.text.Contains("#2 on the leaderboard")), "and finishing tells you your place");
        g.Con.Execute("name  ace-1 zoë! ");
        check(g.RunnerName == "ACE-1 ZO" && Settings.Lines(g).Contains("name ACE-1 ZO"), "'name' sets the name runs go under (upper case, letters and digits), saved with the settings");
        for (int k = 0; k < 12; k++) g.Profile.AddCourseRun("Fighter", best + 1 + k, "FILLER", DateTime.Now);
        check(board.Count == Profile.BoardSize && board.Zip(board.Skip(1)).All(t => t.First.Time <= t.Second.Time), "the board keeps the ten fastest, in order");
        int slow = g.Profile.AddCourseRun("Fighter", best + 100, "SLOW", DateTime.Now);
        int quick = g.Profile.AddCourseRun("Fighter", best + 0.5f, "QUICK", DateTime.Now);
        check(slow == 0 && quick == 2 && board[1].Name == "QUICK" && board.Count == Profile.BoardSize && board.All(r => r.Name != "SLOW"),
              "a run slower than all ten stays off it; a quick one slots into its place");
        var legacy = new Profile { CourseBest = { ["Mage"] = 20f } };
        check(legacy.CourseBestTime("Mage") == 20f && legacy.Board("Mage").Count == 1 && !legacy.CourseBest.ContainsKey("Mage"),
              "a best time saved before the leaderboard joins it");
        var tmp = Path.Combine(Path.GetTempPath(), $"hexen_board_{Environment.ProcessId}.json");
        g.Profile.Save(tmp);
        var back = Profile.Load(tmp);
        File.Delete(tmp);
        check(back.Board("Fighter").Select(r => (r.Time, r.Name)).SequenceEqual(board.Select(r => (r.Time, r.Name))), "the leaderboard is saved with your profile");

        // it's on the pause menu while you practise, and on the title menu
        g.Paused = true; g.Menu.Show(MenuPage.Pause);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Pause), "Leaderboard");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Menu.Page == MenuPage.Leaderboard && g.Menu.BoardClass == PClass.Fighter, "the pause menu on the course opens the leaderboard, on your class");
        var rb = new Renderer();
        rb.Render(g);
        int gold = rb.Fb.Count(c => c == Col.Rgb(230, 190, 80));
        check(gold > 200, "it draws the board");
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(g.Menu.BoardClass == PClass.Cleric, "Right shows the next class's board");
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        check(g.Menu.BoardClass == PClass.Mage, "and Left wraps round");
        g.Menu.Update(new Input { Pause = true }, 1f / 35f);
        check(g.Menu.Page == MenuPage.Pause, "Esc goes back to the pause menu");
        g.Menu.Close(); g.Paused = false;

        // restart and quitting
        g.Paused = true; g.Menu.Show(MenuPage.Pause);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Pause), "Restart");
        Tick(new Input { Move = 1 }, 5);
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Practicing && g.Level.RawName == "Velocity Hangar" && g.RunTime == 0, "Restart starts the course over");
        g.GoToTitle();
        check(!g.Practicing, "quitting to the title leaves practice");
        g.GoToTitle();
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Main), "New game");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        Tick(new Input { Confirm = true });
        check(!g.Practicing && g.Level.RawName != "Velocity Hangar", "and New game is the hub as usual");
        check(!g.Menu.Items(MenuPage.Pause).Contains("Leaderboard"), "outside practice the pause menu has no leaderboard");
        g.GoToTitle();
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Main), "Leaderboard");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Menu.Cursor >= 0 && g.Menu.Page == MenuPage.Leaderboard, "the title menu opens it too");
        g.Menu.Update(new Input { Pause = true }, 1f / 35f);
        check(g.Menu.Page == MenuPage.Main, "and Esc goes back to the title");
    }

    static void GhostChecks(Action<bool, string> check)
    {
        // the track: samples on the run clock, blended between, packed for the profile
        var tr = new GhostTrack();
        for (int k = 0; k <= 20; k++) tr.Record(k * 0.05f, 2 + k * 0.1f, 5.5f, 2.5f);
        tr.Record(1.2f, 40, 5.5f, 2.5f); // a lift teleport, with a frame gap before it
        var (mx, _, _) = tr.At(0.525f);
        check(tr.Points.Count == 25 && MathF.Abs(mx - 3.05f) < 0.001f && tr.At(-1).x == 2 && tr.At(99).x == 40,
              "a ghost track samples every 0.05s of the run and blends between samples");
        check(tr.At(1.18f).x == 40 || tr.At(1.18f).x == tr.Points[21].x, "a teleport jumps instead of sliding across the map");
        var back = GhostTrack.Decode(tr.Encode());
        check(back.Points.SequenceEqual(tr.Points) && GhostTrack.Decode("not base64!").Points.Count == 0, "it packs to text for the profile and back (and bad data is ignored)");
        check(MathF.Abs(tr.TimeAt(3f, 2.5f) - 0.5f) < 0.001f && tr.TimeAt(99, 2.5f) < 0, "and finds when the run first reached a spot");

        // the first finished run becomes the ghost
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }
        g.StartPractice(PClass.Fighter);
        check(g.Ghost == null && !g.Level.Things.OfType<GhostRunner>().Any(), "no ghost before you've finished a run");
        var p = g.P;
        Tick(new Input { Move = 1 }, 35);
        var exit = g.Level.FindMark('E').Value;
        g.Level.CheckpointsReached.UnionWith(Enumerable.Range(0, g.Level.Checkpoints.Count)); // as if you'd come the long way
        p.X = exit.x - 1; p.Y = exit.y; p.FloorZ = Maps.CoursePlatforms[^1].floor; p.Angle = 0;
        for (int k = 0; k < 35 && g.RunStarted; k++) Tick(new Input { Move = 1 });
        var saved = g.Profile.Ghosts.GetValueOrDefault("Fighter");
        var path = saved == null ? new GhostTrack() : GhostTrack.Decode(saved.Path);
        check(saved != null && saved.Time == g.LastRun && path.Points.Count > 20 && path.Points[0].x < 4 && path.Points[^1].x > exit.x - 1,
              $"finishing saves the run as your ghost ({path.Points.Count} samples over {g.LastRun:0.00}s)");
        var ghost = g.Ghost;
        check(ghost != null && g.Level.Things.Contains(ghost) && !ghost.Solid && ghost.Alpha < 256 && MathF.Abs(ghost.X - path.Points[0].x) < 0.01f,
              "and it waits at the start line, see-through, for your next go");

        // it races you: in step with your clock, and where you were
        Tick(new Input { Move = 1 }, 17);
        var (ex, ey, _) = path.At(g.RunTime);
        check(g.RunStarted && MathF.Abs(ghost.X - ex) < 0.01f && MathF.Abs(ghost.Y - ey) < 0.01f && ghost.X > path.Points[0].x,
              "once you set off it retraces the recorded path in time with your clock");
        float gx = ghost.X;
        p.X = ghost.X - 0.6f; p.Y = ghost.Y; p.Angle = 0;
        ghost.Seek(g.RunTime); // stand it still in front of you
        Tick(new Input { Move = 1 }, 20);
        check(p.X > gx + 0.3f, "you run straight through it");

        // splits: each platform says how you're doing against it
        var line = new GhostTrack();
        float gs = 5f; // a steady ghost, 5 units a second along the middle of the course
        for (float t = 0; t <= 20; t += 0.05f) line.Record(t, 2.5f + gs * t, 5.5f, 2.5f);
        g.Profile.Ghosts["Fighter"] = new CourseGhost { Time = 20, Path = line.Encode() };
        g.StartPractice(PClass.Fighter);
        p = g.P;
        g.RunStarted = true; g.RunTime = 2f;
        g.Messages.Clear();
        var p2 = Maps.CoursePlatforms[1];
        p.X = p2.x0 + 1.5f; p.Y = 5.5f; p.FloorZ = 2.5f; p.Z = 0;
        Tick(default);
        // the ghost gets there at (19 - 2.5) / 5 = 3.3s (the first sample on it, 3.3 or 3.35); you're there at 2.0
        check(g.Messages.Any(m => m.text.EndsWith("(-1.30s vs ghost)") || m.text.EndsWith("(-1.35s vs ghost)")),
              $"reaching platform 2 ahead of the ghost says by how much ({g.Messages.LastOrDefault().text})");

        // a slower run leaves the ghost alone; a new best replaces it
        float old = g.Profile.CourseBestTime("Fighter");
        g.RunTime = old + 3;
        g.Level.CheckpointsReached.UnionWith(Enumerable.Range(0, g.Level.Checkpoints.Count)); // as if you'd come the long way
        p.X = exit.x - 1; p.Y = exit.y; p.FloorZ = Maps.CoursePlatforms[^1].floor; p.Angle = 0;
        string keep = g.Profile.Ghosts["Fighter"].Path;
        for (int k = 0; k < 35 && g.RunStarted; k++) Tick(new Input { Move = 1 });
        check(g.Profile.Ghosts["Fighter"].Path == keep, "a slower run keeps the ghost you had");
        Tick(new Input { Move = 1 }, 5);
        g.RunTime = old * 0.5f;
        g.Level.CheckpointsReached.UnionWith(Enumerable.Range(0, g.Level.Checkpoints.Count)); // as if you'd come the long way
        p.X = exit.x - 1; p.Y = exit.y; p.FloorZ = Maps.CoursePlatforms[^1].floor; p.Angle = 0;
        for (int k = 0; k < 35 && g.RunStarted; k++) Tick(new Input { Move = 1 });
        check(g.LastPlace == 1 && g.Profile.Ghosts["Fighter"].Path != keep && g.Profile.Ghosts["Fighter"].Time == g.LastRun, "a new best becomes the ghost");

        // switching it off and on, and it's drawn
        g.Con.Execute("ghost 0");
        Tick(default);
        check(g.Ghost == null && !g.Level.Things.OfType<GhostRunner>().Any(), "'ghost 0' takes it off the course");
        g.Con.Execute("ghost 1");
        Tick(default);
        check(g.Ghost != null && Settings.Lines(g).Contains("ghost 1"), "'ghost 1' brings it back, and it's saved with the settings");
        var gr = new Renderer();
        g.Ghost.Seek(0);
        p.X = g.Ghost.X - 1.5f; p.Y = g.Ghost.Y; p.Angle = 0; p.Pitch = 0; p.FloorZ = 2.5f; p.Z = 0;
        gr.Render(g); var shown = (uint[])gr.Fb.Clone();
        g.Vars.Ghost = false; g.ShowGhost();
        gr.Render(g);
        check(shown.Zip(gr.Fb).Count(t => t.First != t.Second) > 30, "the ghost is drawn, see-through, on the course");
        g.Vars.Ghost = true;
        g.Menu.Show(MenuPage.Options);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Options), "Practice ghost");
        check(g.Menu.Value(g.Menu.Cursor) == "ON", "Options shows Practice ghost: ON");
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(!g.Vars.Ghost && g.Menu.Value(g.Menu.Cursor) == "OFF", "and turns it off");
        g.Menu.Close();

        // the menus fit: every Options item and the practice pause menu sit above their footers
        int opts = g.Menu.Items(MenuPage.Options).Length, pause = g.Menu.Items(MenuPage.Pause).Length;
        check(g.Practicing && pause == 8 && Renderer.PauseTop + (pause - 1) * Renderer.PauseRow + 9 < Renderer.PauseFooter,
              "the pause menu's items all fit above its footer");
        check(Renderer.OptionsTop + (opts - 1) * Renderer.OptionsRow + 9 < Renderer.OptionsFooter && Renderer.OptionsFooter + 8 <= Renderer.H,
              $"so do all {opts} Options items");
        int main = g.Menu.Items(MenuPage.Main).Length + (g.Menu.Items(MenuPage.Main).Contains("Continue") ? 0 : 1); // with Continue, when there's a save
        check(Renderer.TitleTop + (main - 1) * Renderer.TitleRow + 9 < Renderer.TitleFooter && Renderer.TitleFooter + 8 <= Renderer.H,
              $"and all {main} title menu items");
    }

    static void CourseChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        float fps = 35;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / fps); }

        // Descent: platforms dropping away over gaps of 3 to 7
        g.StartPractice(PClass.Fighter, Courses.Descent);
        var lv = g.Level;
        var p = g.P;
        var plats = Courses.DescentPlatforms;
        var gaps = plats.Zip(plats.Skip(1), (a, b) => b.x0 - a.x1 - 1).ToArray();
        check(lv.RawName == "Descent" && p.FloorZ == 6f && g.Course == Courses.Descent, "Descent starts on its top platform");
        check(gaps.SequenceEqual(new[] { 3, 4, 5, 6, 7 }) && plats.Zip(plats.Skip(1)).All(t => t.Second.floor < t.First.floor) && lv.Checkpoints.Count == plats.Length,
              "its platforms drop away over gaps of 3 to 7, a checkpoint on each");
        bool Clears(int k, float speed)
        {
            var (a, b) = (plats[k], plats[k + 1]);
            g.Level.CheckpointsReached.Clear(); g.Checkpoint = null;
            p.X = a.x1 + 1 + p.Radius - 0.01f; p.Y = 5.5f; p.Angle = 0; p.Z = 0; p.VZ = 0; p.FloorZ = a.floor; p.VX = speed; p.VY = 0;
            Tick(new Input { Jump = true });
            for (int t = 0; t < 3 * fps && !p.OnGround; t++) Tick(default);
            return p.OnGround && p.FloorZ == b.floor && p.X > b.x0 - p.Radius - 0.01f;
        }
        float run = g.RunSpeed;
        int Need(Game gm, int k) => gm.GapSpeedPercent(gaps[k], plats[k].floor - plats[k + 1].floor);
        bool hinted = true, tight = true, fast = true;
        for (int k = 0; k < gaps.Length; k++)
        {
            hinted &= Clears(k, Need(g, k) / 100f * run);
            tight &= k == 0 || !Clears(k, Need(g, k) / 100f * run * 0.8f);
        }
        fps = 120;
        for (int k = 0; k < gaps.Length; k++) fast &= Clears(k, Need(g, k) / 100f * run);
        fps = 35;
        check(hinted && fast && tight, "each gap clears at its hinted speed (at 35 and 120 frames a second) and falls short at 80% of it");
        check(Clears(0, run) && !Clears(1, run), "the first gap takes a plain running jump, the rest need speed");
        int hardest = Enumerable.Range(0, gaps.Length).Max(k => Need(g, k));
        var psion = new Game { FixedSeed = 1 };
        psion.StartPractice(PClass.Mage, Courses.Descent);
        int psionHardest = Enumerable.Range(0, gaps.Length).Max(k => Need(psion, k));
        check(hardest <= g.Vars.MaxHop * 70 && psionHardest <= g.Vars.MaxHop * 80, $"the hardest gap needs {hardest}% as the Marine, {psionHardest}% as the Psion");
        g.Level.CheckpointsReached.Clear(); g.Checkpoint = null;
        p.X = plats[2].x0 + 2.5f; p.Y = 5.5f; p.FloorZ = plats[2].floor; p.Z = 0; p.VX = p.VY = 0;
        Tick(default, 2);
        p.X = plats[2].x1 + 2.5f; p.FloorZ = 0; p.Z = 0.3f;
        Tick(default, 20);
        check(p.FloorZ == plats[2].floor && p.X > plats[2].x0 && p.X < plats[2].x1,
              "falling in, the lift takes you to the last platform you reached, though it's lower than the first");

        // Circuit: a lap through four checkpoint zones, over the line behind the start
        g.StartPractice(PClass.Fighter, Courses.Circuit);
        lv = g.Level; p = g.P;
        Tick(default);
        var zones = lv.Checkpoints.Select(c => lv.CheckpointZone[c]).Distinct().Count();
        check(lv.RawName == "Circuit" && lv.Checkpoints.Count == 4 && zones == 4, "Circuit has four checkpoint zones, one for each side of the loop");
        check(MathF.Abs(p.Angle - MathF.PI) < 0.01f && lv.CheckpointsReached.Count == 1, "you start on the south straight, facing west along it");
        var line = Enumerable.Range(0, lv.W * lv.H).Where(i => lv.Marks[i] == 'E').ToList();
        check(line.Count == 8 && line.All(i => i % lv.W == line[0] % lv.W) && line[0] % lv.W > p.X, "the finish line spans the track just behind you");
        g.Messages.Clear();
        p.X = line[0] % lv.W - 1.5f; p.Y = 24.5f; p.Angle = 0;
        Tick(new Input { Move = 1 }, 20);
        check(g.LastPlace == 0 && g.Messages.Any(m => m.text.StartsWith("Reach every checkpoint first")), "backing over the line at the start doesn't count");
        // the lap: round the west side, the north straight and the east side, then over the line
        foreach (var (x, y) in new[] { (4.5f, 14.5f), (22.5f, 4.5f), (40.5f, 14.5f) })
        {
            p.X = x; p.Y = y; p.FloorZ = lv.FloorAt(x, y); p.Z = 0; p.VX = p.VY = 0;
            Tick(new Input { Move = 1 }, 3);
        }
        check(lv.CheckpointsReached.Count == 4 && g.Messages.Any(m => m.text.StartsWith("Every checkpoint")), "each side's checkpoint counts, and it tells you to cross the line");
        p.X = line[0] % lv.W + 1.5f; p.Y = 24.5f; p.FloorZ = lv.FloorAt(p.X, p.Y); p.Angle = MathF.PI;
        for (int k = 0; k < 35 && g.RunStarted; k++) Tick(new Input { Move = 1 });
        check(g.LastPlace == 1 && g.Profile.Board("circuit/Fighter").Count == 1 && g.Profile.Ghosts.ContainsKey("circuit/Fighter"),
              "crossing the line finishes the lap, on the Circuit's own leaderboard, with its own ghost");
        check(!g.Profile.Board("Fighter").Any() && !g.Profile.Ghosts.ContainsKey("Fighter"), "the Velocity Hangar's board and ghost are separate");
        check(g.Ghost != null && g.Messages.Any(m => m.text.Contains("new best")), "and next lap, the ghost of it races you");
        Tick(new Input { Move = 1 }, 10);
        p.X = 4.5f; p.Y = 14.5f; p.FloorZ = lv.FloorAt(p.X, p.Y); p.Z = 0; p.VX = p.VY = 0;
        g.Messages.Clear();
        Tick(new Input { Move = 1 }, 2);
        check(g.Messages.Any(m => m.text.Contains("vs ghost")), "and each side's checkpoint says how you're doing against it");

        // Free Roam: just room
        g.StartPractice(PClass.Fighter, Courses.FreeRoam);
        lv = g.Level; p = g.P;
        int open = Enumerable.Range(0, lv.W * lv.H).Count(i => lv.Cells[i] == '\0');
        check(lv.W == 64 && lv.H == 64 && open == 62 * 62 && lv.Things.Count == 0 && lv.Checkpoints.Count == 0,
              "Free Roam is a 64-by-64 field with nothing in it");
        Tick(new Input { Move = 1 }, 70);
        check(!g.RunStarted && g.RunTime == 0 && g.Ghost == null && g.StrafeAdvice() != null, "no clock and no ghost, but the strafe helper is there");
        check(p.HasJetpack, "and you have a jetpack");
        Tick(new Input { JetHeld = true }, 35);
        check(p.Flying && p.Z > 1, "which flies");
        var r = new Renderer();
        r.Render(g);
        int clock = Enumerable.Range(3, 8).Sum(y => Enumerable.Range(Renderer.W - 80, 76).Count(x => r.Fb[y * Renderer.W + x] == Col.Rgb(240, 236, 220)));
        check(clock == 0, "with no clock in the corner");

        check(Courses.All.All(c => { g.StartPractice(PClass.Fighter, c); return !g.Level.Things.Any(t => t is Chest); }),
              "no treasure chests get scattered on the practice courses");

        // the leaderboard steps through the timed courses
        g.StartPractice(PClass.Fighter, Courses.Circuit);
        g.Paused = true; g.Menu.Show(MenuPage.Pause);
        g.Menu.Show(MenuPage.Leaderboard);
        check(g.Menu.BoardCourse == Courses.Circuit, "on a course, the leaderboard opens on it");
        var seen = new List<string>();
        for (int k = 0; k < 8; k++) { g.Menu.Update(new Input { Down = true }, 1f / 35f); seen.Add(g.Menu.BoardDaily ? "daily" : g.Menu.BoardArena ? "arena" : g.Menu.BoardEndless ? "endless" : g.Menu.BoardRange ? "range" : g.Menu.BoardRematch ? "rematch" : g.Menu.BoardCourse.Id); }
        check(seen.SequenceEqual(new[] { "endless", "range", "rematch", "arena", "daily", "hangar", "descent", "circuit" }), "Up/Down step through the timed courses, the endless course, the shooting range, the rematches, the arena and the daily challenge (Free Roam has no board)");
        g.Menu.Close(); g.Paused = false;

        // picking Free Roam from the menus
        g.GoToTitle();
        g.Menu.Cursor = 1;
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Courses), "Free Roam");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        Tick(new Input { Slot = 3 });
        check(g.Practicing && g.Course == Courses.FreeRoam && g.P.Class == PClass.Mage, "Practice > Free Roam > a class starts it");
        g.GoToTitle();
        g.Menu.Cursor = 1;
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        g.Menu.Update(new Input { Pause = true }, 1f / 35f);
        check(g.Menu.Page == MenuPage.Main, "Esc from the course list goes back to the title");
    }

    static void DemoChecks(Action<bool, string> check)
    {
        // it finishes every timed course as every class, never falling in, at silver pace or better
        bool finished = true, clean = true, fast = true;
        var times = new List<string>();
        foreach (var course in Courses.Timed)
            foreach (var cls in Enum.GetValues<PClass>())
            {
                var g = new Game { FixedSeed = 1 };
                g.StartPractice(cls, course);
                g.StartDemo();
                for (int f = 0; f < 60 * 60 && g.Demo; f++)
                {
                    g.Update(default, 1f / 60f);
                    if (course.Platforms != null && g.P.OnGround && g.P.FloorZ == 0) clean = false;
                }
                finished &= !g.Demo && g.DemoTime > 0;
                var medal = course.MedalFor(cls, g.DemoTime);
                fast &= medal >= Medal.Silver;
                times.Add($"{course.Id[0]}{cls.ToString()[0]} {g.DemoTime:0.0} {Medals.Name(medal)[0]}");
            }
        check(finished, "the demo finishes every timed course as every class");
        check(clean, "without once falling into a gap");
        check(fast, $"at silver pace or better: {string.Join(", ", times)}");

        // the same run however fast the frames come: it plays on a fixed 72-tick clock
        float At(float fps)
        {
            var g = new Game { FixedSeed = 1 };
            g.StartPractice(PClass.Cleric, Courses.Hangar);
            g.StartDemo();
            for (int f = 0; f < fps * 60 && g.Demo; f++) g.Update(default, 1f / fps);
            return g.DemoTime;
        }
        float t35 = At(35), t144 = At(144);
        check(t35 > 0 && MathF.Abs(t35 - t144) < 0.001f, $"it plays exactly the same at 35 and 144 frames a second ({t35:0.000}s, {t144:0.000}s)");

        var d = new Game { FixedSeed = 1 };
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) d.Update(i, 1f / 60f); }
        d.StartPractice(PClass.Fighter, Courses.Hangar);
        check(d.Menu.Items(MenuPage.Pause).Contains("Watch demo"), "the practice pause menu has Watch demo");
        d.Paused = true; d.Menu.Show(MenuPage.Pause);
        d.Menu.Cursor = Array.IndexOf(d.Menu.Items(MenuPage.Pause), "Watch demo");
        d.Menu.Update(new Input { Confirm = true }, 1f / 60f);
        check(d.Demo && !d.Paused && d.Pilot != null, "which starts it");

        // it shows what it's doing, and the strafe helper lights the keys it holds
        var captions = new HashSet<string>();
        bool keysLit = false;
        for (int f = 0; f < 60 * 60 && d.Demo; f++)
        {
            Tick(default);
            if (d.Pilot != null) captions.Add(d.Pilot.Step);
            keysLit |= d.StrafeAdvice() is { Air: true, HeldOk: true };
        }
        check(captions.IsSupersetOf(new[] { "RUN", "JUMP", "STRAFE", "SWITCH" }) && captions.Overlaps(new[] { "GO", "LINE UP", "WIND UP" }), $"it names each step as it goes ({string.Join(", ", captions)})");
        check(keysLit, "and the strafe helper lights the keys it's holding");

        // slower: 2 for half speed, 3 for a quarter
        d.Con.Execute("demo");
        float x0 = d.P.X;
        Tick(default, 60);
        float full = d.P.X - x0;
        d.Con.Execute("demo");
        Tick(new Input { Slot = 3 });
        x0 = d.P.X;
        Tick(default, 60);
        float quarter = d.P.X - x0;
        check(d.PracticeSpeed == 0.25f && quarter < full * 0.4f && quarter > 0, $"3 plays it at a quarter speed ({quarter:0.00} units a second against {full:0.00})");
        Tick(new Input { Slot = 1 });

        // step by step: E, then it stops before each new step until Enter
        d.Con.Execute("demo");
        Tick(new Input { Use = true });
        check(d.DemoSteps, "E turns on step by step");
        int stops = 0;
        var seen = new List<string>();
        for (int f = 0; f < 60 * 20 && d.Demo && stops < 6; f++)
        {
            Tick(default);
            if (d.DemoPaused)
            {
                stops++;
                seen.Add(d.Pilot.Step);
                float px = d.P.X;
                Tick(default, 30);
                check(stops > 1 || (d.DemoPaused && d.P.X == px), "on a step it waits, frozen");
                Tick(new Input { Confirm = true });
            }
        }
        check(stops == 6 && seen.Distinct().Count() > 2, $"and Enter goes on to the next ({string.Join(" > ", seen)})");

        // moving takes over, back at the start line
        Tick(new Input { Move = 1 });
        check(!d.Demo && MathF.Abs(d.P.X - d.Level.StartX) < 0.01f && d.Messages.Any(m => m.text.StartsWith("Your turn")), "moving takes over, from the start line");

        // a finished demo: nothing on the leaderboard or saved as your ghost, but you race its ghost next
        d.Con.Execute("demo");
        d.PracticeSpeed = 1;
        for (int f = 0; f < 60 * 60 && d.Demo; f++) Tick(default);
        check(!d.Demo && d.DemoTrack != null && d.Profile.Board("Fighter").Count == 0 && !d.Profile.Ghosts.ContainsKey("Fighter"),
              "a finished demo doesn't go on the leaderboard or become your saved ghost");
        check(d.Ghost != null && d.Ghost.Track == d.DemoTrack && d.Messages.Any(m => m.text.Contains("race its ghost")), "but you race its ghost on your next go");

        // your own slow-motion runs don't count
        Tick(new Input { Slot = 2 });
        Tick(new Input { Move = 1 }, 40);
        d.Level.CheckpointsReached.UnionWith(Enumerable.Range(0, d.Level.Checkpoints.Count));
        var exit = d.Level.FindMark('E').Value;
        d.P.X = exit.x - 1; d.P.Y = exit.y; d.P.FloorZ = Maps.CoursePlatforms[^1].floor; d.P.Angle = 0;
        for (int f = 0; f < 60 && d.RunStarted; f++) Tick(new Input { Move = 1 });
        check(!d.RunStarted && d.Profile.Board("Fighter").Count == 0 && d.Messages.Any(m => m.text.Contains("at 50% speed")), "your own runs in slow motion don't count");
        Tick(new Input { Slot = 1 });
        check(d.PracticeSpeed == 1, "and 1 is back to full speed");

        // stopping from the pause menu
        d.Con.Execute("demo");
        d.Paused = true; d.Menu.Show(MenuPage.Pause);
        d.Menu.Cursor = Array.IndexOf(d.Menu.Items(MenuPage.Pause), "Stop demo");
        d.Menu.Update(new Input { Confirm = true }, 1f / 60f);
        check(!d.Demo, "Stop demo on the pause menu ends it");

        // Free Roam: it just strafes round, getting faster
        var fr = new Game { FixedSeed = 1 };
        fr.StartPractice(PClass.Fighter, Courses.FreeRoam);
        fr.StartDemo();
        float top = 0;
        for (int f = 0; f < 60 * 20; f++) { fr.Update(default, 1f / 60f); top = MathF.Max(top, fr.P.HSpeed / fr.RunSpeed); }
        check(fr.Demo && top > 2f, $"in Free Roam it strafes round the field, up to {top * 100:0}%");
    }

    static void MedalChecks(Action<bool, string> check)
    {
        // targets: gold, silver and bronze for each timed course and class
        var hangar = Courses.Hangar;
        check(hangar.RouteLength == 78 && hangar.MedalTimes(PClass.Fighter) == (11.5f, 14.5f, 19.5f),
              "the Velocity Hangar's Marine targets: gold 11.5s, silver 14.5s, bronze 19.5s (a 78-unit route at 170%, 135% and 100% of a run)");
        bool ordered = true, fairer = true, possible = true;
        foreach (var c in Courses.Timed)
        {
            var (fg, fs, fb) = c.MedalTimes(PClass.Fighter);
            var (mg, ms, mb) = c.MedalTimes(PClass.Mage);
            ordered &= fg < fs && fs < fb && mg < ms && ms < mb;
            fairer &= mg > fg && mb > fb;
            // gold asks for an average of 170% of a run: well under the 300% strafe jumping can reach
            possible &= fg >= c.RouteLength / (new Game().Vars.MaxHop * 3.6f * ClassDef.All[0].Speed) * 1.5f;
        }
        check(ordered, "on every timed course gold is quicker than silver, and silver than bronze");
        check(fairer, "the slower Psion gets more time than the Marine");
        check(possible, "gold is well within reach of strafe jumping's top speed");
        check(Courses.FreeRoam.MedalTimes(PClass.Fighter) == (0f, 0f, 0f) && Courses.FreeRoam.MedalFor(PClass.Fighter, 5) == Medal.None, "Free Roam has no medals");
        check(hangar.MedalFor(PClass.Fighter, 11.5f) == Medal.Gold && hangar.MedalFor(PClass.Fighter, 11.51f) == Medal.Silver
              && hangar.MedalFor(PClass.Fighter, 19.5f) == Medal.Bronze && hangar.MedalFor(PClass.Fighter, 19.6f) == Medal.None && hangar.MedalFor(PClass.Fighter, 0) == Medal.None,
              "a time on the target earns that medal; a hair over drops to the next");

        // finishing: the medal the run earns, a new medal when you beat your old one, and the next one to aim for
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }
        g.StartPractice(PClass.Fighter);
        var exit = g.Level.FindMark('E').Value;
        void Finish(float time)
        {
            Tick(new Input { Move = 1 }, 10);
            g.Level.CheckpointsReached.UnionWith(Enumerable.Range(0, g.Level.Checkpoints.Count));
            g.RunTime = time;
            var p = g.P;
            p.X = exit.x - 1; p.Y = exit.y; p.FloorZ = Maps.CoursePlatforms[^1].floor; p.Angle = 0;
            for (int k = 0; k < 35 && g.RunStarted; k++) Tick(new Input { Move = 1 });
        }
        bool Said(string text) => g.Messages.Any(m => m.text.Contains(text));
        Finish(25f);
        check(g.LastMedal == Medal.None && !Said("New medal") && Said("BRONZE is 19.50s."), "a run slower than bronze earns nothing, and says what bronze takes");
        Finish(18f);
        check(g.LastMedal == Medal.Bronze && Said("BRONZE.") && Said("New medal: BRONZE!") && Said("SILVER is 14.50s."), "a bronze run earns a new medal, and names the next");
        Finish(19f);
        check(g.LastMedal == Medal.Bronze && !Said("New medal"), "another bronze isn't a new medal");
        Finish(11f);
        check(g.LastMedal == Medal.Gold && Said("New medal: GOLD!") && !Said(" is "), "gold, and nothing left to aim for");
        check(hangar.MedalFor(PClass.Fighter, g.Profile.CourseBestTime("Fighter")) == Medal.Gold, "your medal comes from your best time, so it's kept with your profile");

        // during a run, the clock shows the best medal you can still make
        g.RunStarted = true;
        g.RunTime = 5; var a = g.NextMedal();
        g.RunTime = 12; var b = g.NextMedal();
        g.RunTime = 16; var c2 = g.NextMedal();
        g.RunTime = 30; var d = g.NextMedal();
        check(a == (Medal.Gold, 11.5f) && b == (Medal.Silver, 14.5f) && c2 == (Medal.Bronze, 19.5f) && d.medal == Medal.None,
              "the clock's medal counts down: gold, then silver, then bronze, then none");
        var r = new Renderer();
        g.RunTime = 5;
        r.Render(g);
        int goldPx = Enumerable.Range(0, 40).Sum(y => Enumerable.Range(Renderer.W - 100, 100).Count(x => r.Fb[y * Renderer.W + x] == Medals.Colour(Medal.Gold)));
        check(goldPx > 20, "and it's drawn under the clock, in the medal's colour");

        // the course list and leaderboard show medals
        g.Paused = true; g.Menu.Show(MenuPage.Leaderboard);
        r.Render(g);
        int medalPx = r.Fb.Count(px => px == Medals.Colour(Medal.Gold)) ;
        check(medalPx > 40, "the leaderboard shows the targets and a medal by each time");
        g.Menu.Close(); g.Paused = false;
        g.GoToTitle();
        g.Menu.Show(MenuPage.Courses);
        r.Render(g);
        check(r.Fb.Count(px => px == Medals.Colour(Medal.Gold)) > 40 && r.Fb.Count(px => px == Medals.Colour(Medal.Bronze)) > 20,
              "and the course list shows each class's medal and the targets");
    }

    static void StrafeHelperChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        float fps = 35;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / fps); }
        g.StartPractice(PClass.Fighter);
        g.Vars.NoClip = true; // room to run circles
        var p = g.P;
        float run = g.RunSpeed, deg = 180 / MathF.PI;

        // the zone, from Quake's air acceleration: just past square-on to your velocity, narrowing with speed
        p.VX = run; p.VY = 0;
        var (best, lo, hi) = g.StrafeZone(MathF.PI / 2);
        check(best * deg > 0 && best * deg < 5 && lo < 0 && hi > best, $"holding D at run speed, the best view is {best * deg:0.0} degrees right of your velocity, in a zone {lo * deg:0.0} to {hi * deg:0.0}");
        var (bestA, loA, hiA) = g.StrafeZone(-MathF.PI / 2);
        check(MathF.Abs(bestA + best) < 1e-5f && MathF.Abs(loA + hi) < 1e-5f, "holding A mirrors it");
        check(MathF.Abs(g.StrafeZone(MathF.PI / 4).best - (MathF.PI / 4 + best)) < 1e-4f, "with W and D the view sits 45 degrees further round");
        p.VX = run * 2;
        var (_, lo2, hi2) = g.StrafeZone(MathF.PI / 2);
        check(hi2 - lo2 < (hi - lo) * 0.7f, $"at double speed the zone is narrower ({(hi2 - lo2) * deg:0.0} vs {(hi - lo) * deg:0.0} degrees)");

        // it's right: aiming at the best angle gains speed, outside the zone loses it
        float Hop(float offset)
        {
            p.X = g.Level.StartX; p.Y = g.Level.StartY; p.Z = 0; p.VZ = 0; p.VX = run * 1.5f; p.VY = 0; p.Angle = 0;
            Tick(new Input { Jump = true, Strafe = 1 });
            while (!p.OnGround) { p.Angle = MathF.Atan2(p.VY, p.VX) + g.StrafeZone(MathF.PI / 2).best + offset; Tick(new Input { Strafe = 1 }); }
            return p.HSpeed / (run * 1.5f);
        }
        float atBest = Hop(0), past = Hop(10 / deg), square = Hop(-12 / deg);
        check(atBest > 1.02f && past < 1f && square < 1.005f, $"a hop at the best angle gains ({atBest:0.000}x); past the zone you lose speed ({past:0.000}x), short of it you gain none ({square:0.000}x)");

        // following the helper, and nothing else, builds speed: hold the key it lights, turn the way it says, jump when it says
        float Follow(int hops)
        {
            p.X = g.Level.StartX; p.Y = g.Level.StartY; p.Z = 0; p.VZ = 0; p.VX = p.VY = 0; p.Angle = 0;
            for (int k = 0; k < 2 * fps && g.StrafeAdvice() is not { Jump: true }; k++) Tick(new Input { Move = 1 });
            for (int h = 0; h < hops; h++)
            {
                Tick(new Input { Jump = true, Move = 1 });
                while (!p.OnGround)
                {
                    if (g.StrafeAdvice() is not { } tip) { Tick(default); continue; }
                    Tick(new Input { Strafe = tip.Side, Move = tip.WantForward ? 1 : 0, LookX = tip.Target / (0.0025f * g.Vars.Sens) });
                }
            }
            return p.HSpeed / run;
        }
        float followed = Follow(6);
        check(followed > 1.5f, $"doing what the helper shows builds speed: {followed * 100:0}% of a run after six hops");
        fps = 120;
        float fast = Follow(6);
        fps = 35;
        check(fast > 1.5f, $"at 120 frames a second too ({fast * 100:0}%)");

        // what it shows
        p.X = g.Level.StartX; p.Y = g.Level.StartY; p.Z = 0; p.VX = run; p.VY = 0; p.Angle = 0;
        p.VX = 0;
        Tick(new Input { Move = 1 });
        check(g.StrafeAdvice() is { Air: false, WantForward: true, Jump: false }, "on the ground it says run forward");
        Tick(new Input { Move = 1 }, 20);
        check(g.StrafeAdvice() is { Air: false, Jump: true }, "and jump once you're up to speed");
        Tick(new Input { Jump = true, Move = 1 });
        p.Angle = MathF.Atan2(p.VY, p.VX) + 0.3f;
        Tick(default);
        var air = g.StrafeAdvice().Value;
        check(air.Air && air.Side == 1 && !air.HeldOk && air.Target < 0 && !air.InZone, "in the air, looking right of your velocity, it asks for D and a turn back left");
        Tick(new Input { Strafe = 1 });
        p.Angle = MathF.Atan2(p.VY, p.VX) + g.StrafeZone(MathF.PI / 2).best;
        air = g.StrafeAdvice().Value;
        check(air.HeldOk && air.InZone && MathF.Abs(air.Target) < 0.01f / deg, "holding D and aimed at the best angle, you're in the zone with the tick on your view");
        Tick(new Input { Strafe = 1 });
        air = g.StrafeAdvice().Value;
        check(air.Target > 0, "a frame later your velocity has swung right, so it says keep turning right");
        var r = new Renderer();
        r.Render(g); var shown = (uint[])r.Fb.Clone();
        g.Vars.StrafeHelp = 0;
        check(g.StrafeAdvice() == null, "'strafehelp 0' turns it off");
        r.Render(g);
        int drawn = shown.Zip(r.Fb).Count(t => t.First != t.Second);
        check(drawn > 150, $"it's drawn above the aim point ({drawn} pixels)");
        g.Vars.StrafeHelp = 1;
        g.Vars.QuakeMove = false;
        check(g.StrafeAdvice() == null, "it's hidden with classic movement");
        g.Vars.QuakeMove = true;
        while (!p.OnGround) Tick(default);

        var hub = new Game { FixedSeed = 1 };
        hub.NewGame(PClass.Fighter);
        hub.P.VX = hub.RunSpeed;
        hub.Update(new Input { Move = 1 }, 1f / 35f);
        check(hub.StrafeAdvice() == null, "by default it only shows on the practice course");
        hub.Con.Execute("strafehelp 2");
        check(hub.StrafeAdvice() != null && Settings.Lines(hub).Contains("strafehelp 2"), "'strafehelp 2' shows it everywhere, saved with the settings");
        hub.Menu.Show(MenuPage.Options);
        hub.Menu.Cursor = Array.IndexOf(hub.Menu.Items(MenuPage.Options), "Strafe helper");
        check(hub.Menu.Value(hub.Menu.Cursor) == "ALWAYS", "Options shows Strafe helper: ALWAYS");
        hub.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(hub.Vars.StrafeHelp == 0 && hub.Menu.Value(hub.Menu.Cursor) == "OFF", "and steps round to OFF");
    }
}
