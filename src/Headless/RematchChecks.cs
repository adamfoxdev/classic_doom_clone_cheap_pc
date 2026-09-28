namespace HexenSharp;

/// <summary>Checks on mini-boss rematches.</summary>
public static partial class Headless
{
    static void RematchChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1, AchievementsOn = false, Profile = new Profile() };
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }

        // the menu: locked until beaten in the campaign
        g.Menu.Show(MenuPage.ArenaSetup);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.ArenaSetup), "Rematch");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Menu.Page == MenuPage.Rematch && g.Menu.Items(MenuPage.Rematch).Take(6).All(s => s == "???"), "Arena > Rematch lists the six mini-bosses, locked until you've beaten them");
        g.Menu.Cursor = 0;
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Menu.Page == MenuPage.Rematch && g.Menu.Notice == "Beat it in the campaign first.", "a locked one won't start");
        g.Profile.MiniBosses.AddRange(MiniBosses.All.Select(d => d.MiniBoss));
        check(g.Menu.Items(MenuPage.Rematch).Take(6).SequenceEqual(MiniBosses.All.Select(d => d.Name)), "beaten ones show by name");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Mode == GameMode.ClassSelect && g.PendingRematch == MiniBosses.Warden, "picking one goes to the class choice");
        Tick(new Input { Slot = 2 });
        var lv = g.Level;
        check(g.Rematch == MiniBosses.Warden && g.P.Class == PClass.Cleric && lv.RawName == "Deepdelve Quarry"
              && lv.Things.OfType<Monster>().Count() == 1 && !lv.Things.Any(t => t is Chest),
              "it starts on the boss's own map: just you and it, no chests");
        check(!g.SaveNow() && g.Hub.Length == 1, "a rematch isn't saved");

        // every boss: a sensible start, not in a wall
        bool starts = true;
        foreach (var boss in MiniBosses.All)
        {
            var r = new Game { FixedSeed = 1, AchievementsOn = false, Profile = g.Profile };
            r.StartRematch(PClass.Fighter, boss);
            var b = r.Level.Things.OfType<Monster>().Single();
            bool ok = b.Def == boss && !r.Level.Blocks((int)r.P.X, (int)r.P.Y) && r.Mode == GameMode.Playing;
            if (r.Level.Flight) ok &= r.P.Flying && MathF.Abs(b.X - (r.Level.StartX + Rematches.LeviathanAhead)) < 0.01f;
            else ok &= Game.Dist(r.P.X, r.P.Y, b.X, b.Y) < 16 && !b.Alive == false;
            starts &= ok;
        }
        check(starts, "each of the six starts you a little way off it (the Leviathan waits 30 cells down the lane)");

        // the clock, and winning
        Tick(default, 35);
        check(g.RematchTime > 0.95f && g.RematchTime < 1.05f, $"the clock runs from the start ({g.RematchTime:0.00}s)");
        var warden = lv.Things.OfType<Monster>().Single();
        int xp = g.Profile.TotalXp;
        g.DamageMonster(warden, 1000000, 0);
        float won = g.RematchTime;
        Tick(default, 35);
        var board = g.Profile.RematchBoard("warden");
        check(g.RematchWon && board.Count == 1 && board[0].Class == "Cleric" && MathF.Abs(board[0].Time - won) < 0.001f && g.RematchTime == won && g.LastRematchPlace == 1,
              "felling it stops the clock and puts the time on its board");
        check(g.Profile.TotalXp == xp && !lv.Things.OfType<Pickup>().Any(p => Game.Dist(p.X, p.Y, warden.X, warden.Y) < 1.2f), $"with no experience or loot to farm (xp +{g.Profile.TotalXp - xp})");
        check(g.Messages.Any(m => m.text.Contains("a new best")), "and says so");

        // Restart and dying both start it over
        g.Paused = true; g.Menu.Show(MenuPage.Pause);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Pause), "Restart");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Rematch == MiniBosses.Warden && !g.RematchWon && g.RematchTime == 0 && g.Level.Things.OfType<Monster>().Single().Alive, "Restart gives you another go");
        g.P.Armor = 0;
        g.DamagePlayer(100000);
        Tick(default, 35 * 3);
        Tick(new Input { Confirm = true });
        check(g.Mode == GameMode.Playing && g.Rematch == MiniBosses.Warden && g.Level.Things.OfType<Monster>().Single().Alive && g.RematchTime < 0.1f, "and so does dying");

        // the board: ten quickest per boss
        var pr = new Profile();
        for (int k = 0; k < 13; k++) pr.AddRematchRun(new RematchRun { Boss = "keeper", Name = "x", Class = "Mage", Time = 50 - k });
        pr.AddRematchRun(new RematchRun { Boss = "stalker", Name = "y", Class = "Fighter", Time = 99 });
        var kb = pr.RematchBoard("keeper");
        check(kb.Count == 10 && kb[0].Time == 38 && kb.Zip(kb.Skip(1)).All(t => t.First.Time <= t.Second.Time) && pr.RematchBoard("stalker").Count == 1,
              "each boss keeps its ten quickest");

        // the leaderboard opens on this boss's board; Left/Right steps through the bosses
        g.Paused = true; g.Menu.Show(MenuPage.Pause); g.Menu.Show(MenuPage.Leaderboard);
        check(g.Menu.BoardRematch && MiniBosses.All[g.Menu.BoardBoss] == MiniBosses.Warden, "in a rematch the leaderboard opens on that boss's board");
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(MiniBosses.All[g.Menu.BoardBoss] == MiniBosses.Stalker, "Right goes on to the next boss");
        g.Menu.Close(); g.Paused = false;

        // back to the title, and a new campaign is the whole hub again
        g.GoToTitle();
        check(g.Rematch == null, "leaving ends the rematch");
        g.Style = GameStyle.Classic;
        g.HubSource = Maps.BuildHub;
        g.NewGame(PClass.Fighter);
        check(g.Hub.Length > 5 && g.Rematch == null && g.Level.Things.OfType<Monster>().Count() > 5, "and a new game is the campaign as usual");
    }
}
