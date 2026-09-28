namespace HexenSharp;

/// <summary>Checks on the Chaos Arena: waves, the Arena mode, perks and modifiers, arsenal upgrades and Arcade mode.</summary>
public static partial class Headless
{
    static void ArcadeChecks(Action<bool, string> check)
    {
        var opts = new Game().Menu.Items(MenuPage.Options);
        check(opts.Contains("Arcade mode"), "Options has an Arcade mode toggle");
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.Con.Execute("arcade 1", quiet: true);
        check(g.Vars.Arcade && Settings.Lines(g).Contains("arcade 1"), "it's saved with your settings");
        g.StartArena(PClass.Fighter);
        var a = g.Arcade;
        check(a.Score == 0 && a.Rank == 0 && !a.Active, "a run starts with no score and no rank");

        // punch a monster: a damage number pops out and it scores
        g.Vars.God = true;
        var m = new Monster(Monster.Ettin) { X = g.P.X + 0.9f, Y = g.P.Y, Level = g.Level };
        g.Level.Things.Add(m);
        g.P.Angle = 0;
        for (int k = 0; k < 35 && a.Floaters.Count == 0; k++) Tick(new Input { Fire = true });
        check(a.Score > 0 && a.Floaters.Count > 0 && int.TryParse(a.Floaters[0].Text, out int shown) && shown > 0, "hits pop up their damage and score");
        long first = a.Score;
        for (int k = 0; k < 35 * 6 && m.Alive; k++) Tick(new Input { Fire = true });
        check(!m.Alive && a.Floaters.Any(f => f.Text.StartsWith("+")) && a.Score > first, "a kill pays a bonus");

        // a flurry climbs the style ranks, which multiply the score
        var b = new Arcade();
        for (int k = 0; k < 40; k++) b.Hit(0, 0, 0, 30, k % 3, k % 4 == 3, 100, false, k % 5 == 0);
        check(b.Rank >= 4 && b.Multiplier == b.Rank + 1, $"keep it up and the rank climbs ({Arcade.Ranks[b.Rank]}, x{b.Multiplier})");
        long before = b.Score;
        b.Hit(0, 0, 0, 10, 0, false, 100, false, false);
        check(b.Score - before == 10 * 10 * b.Multiplier, "points are multiplied by the rank");
        int rank = b.Rank;
        b.Hurt();
        check(b.Rank == rank - 1 && b.Combo == 0, "getting hurt drops a rank and breaks the combo");
        rank = b.Rank;
        for (int k = 0; k < 35 * 30; k++) b.Update(1f / 35f);
        check(b.Rank < rank && b.Floaters.Count == 0, "stop fighting and the rank drains away");
        check(Arcade.Ranks.Length == 7 && Arcade.Ranks[^1] == "SSS", "ranks run from D to SSS");
    }

    static void ArenaPerkChecks(Action<bool, string> check)
    {
        Game g = null;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }
        void ClearTo(int waves)
        {
            var a = g.Level.Arena;
            var altar = g.Level.FindMark('!').Value;
            if (!a.Started) { g.P.X = altar.x; g.P.Y = altar.y; }
            for (int f = 0; f < 35 * 60 * 3 && a.BestWave < waves; f++) { Tick(default); g.KillAll(); }
        }

        // after the boss wave (the fifth), a choice of three perks; the next wave waits for it
        g = new Game { FixedSeed = 1 };
        g.StartArena(PClass.Fighter);
        g.Vars.God = true;
        var ar = g.Level.Arena;
        ClearTo(4);
        check(ar.Offer == null, "no perk after an ordinary wave");
        ClearTo(5);
        check(ar.Offer is { Length: 3 } o && o.Distinct().Count() == 3, "after wave 5, three different perks to choose from");
        float timer = ar.Timer, clock = ar.RunTime;
        Tick(default, 35 * 10);
        check(ar.Offer != null && ar.Timer == timer && ar.RunTime == clock && ar.Wave == 5, "the next wave and the run's clock wait while you choose");
        var r = new Renderer();
        r.Render(g);
        check(r.Fb.Count(px => px == Col.Rgb(255, 230, 120)) > 100, "the choice is drawn over the view");
        var second = ar.Offer[1];
        int weapon = g.P.Weapon;
        Tick(new Input { Slot = 2 });
        check(ar.Offer == null && ar.Rank(second) == 1 && g.P.Weapon == weapon && g.P.PendingWeapon < 0, $"2 takes the second ({PerkInfo.Name(second)}), without switching weapon");
        Tick(default, (int)(35 * ArenaState.Intermission) + 2);
        check(ar.Wave == 6, "and the waves come again");
        check(Enumerable.Range(0, 40).All(_ => ar.RollOffer().Distinct().Count() == 3), "offers never repeat a perk");
        foreach (var pk in Enum.GetValues<Perk>()) ar.Perks[pk] = PerkInfo.MaxRank;
        check(ar.RollOffer().Length == 0, "a perk at rank 3 isn't offered again");
        ar.Perks.Clear();
        g.ApplyProfile(); // back to no perks

        // what they do
        var p = g.P;
        g.Level.Things.RemoveAll(t => t is Monster);
        float run0 = g.RunSpeed, dmg0 = g.PlayerDamageMult(0);
        ar.Perks[Perk.Swiftness] = 2; ar.Perks[Perk.Might] = 1;
        check(MathF.Abs(g.RunSpeed / run0 - 1.2f) < 0.001f && MathF.Abs(g.PlayerDamageMult(0) / dmg0 - 1.2f) < 0.001f, "Swiftness II: +20% speed; Might: +20% damage");
        g.Vars.Freeze = true;
        p.Cooldown = 0; Tick(new Input { Fire = true });
        float cd0 = p.Cooldown;
        ar.Perks[Perk.RapidFire] = 1;
        p.Cooldown = 0; p.FireAnim = 0; Tick(default, 20); p.Cooldown = 0; Tick(new Input { Fire = true });
        check(cd0 > 0 && MathF.Abs(cd0 / p.Cooldown - 1.2f) < 0.02f, $"Rapid Fire: 20% quicker ({cd0:0.000}s -> {p.Cooldown:0.000}s)");
        g.Vars.God = false;
        int max0 = p.MaxHealth;
        ar.Offer = new[] { Perk.Vitality }; p.Health = 30;
        Tick(new Input { Slot = 1 });
        check(p.MaxHealth == max0 + 25 && p.Health == p.MaxHealth, "Vitality: +25 max health and a full heal");
        g.Con.Execute("skill Vitality");
        check(p.MaxHealth >= max0 + 25, "which a skill point doesn't undo");
        ar.Perks[Perk.Regeneration] = 1; p.Health = 50;
        Tick(default, 35 * 4 + 2);
        check(p.Health == 52, $"Regeneration: 1 health every 2 seconds ({p.Health})");
        ar.Perks[Perk.ManaFont] = 2; p.BlueMana = p.GreenMana = 0;
        Tick(default, 35 * 3 + 2);
        check(p.BlueMana == 6 && p.GreenMana == 6, $"Mana Font II: 2 of each mana a second ({p.BlueMana}, {p.GreenMana})");

        // Chain Lightning and Bloodthirst, with the Fighter's fists on two ettins in a row
        ar.Perks.Clear();
        p.Angle = 0; p.Weapon = 0; p.PendingWeapon = -1;
        var near = new Monster(Monster.Ettin) { X = p.X + 0.9f, Y = p.Y, Level = g.Level };
        var far = new Monster(Monster.Ettin) { X = p.X + 2.4f, Y = p.Y, Level = g.Level };
        g.Level.Things.Add(near); g.Level.Things.Add(far);
        int farHp = far.Health;
        p.Cooldown = 0; Tick(new Input { Fire = true });
        check(far.Health == farHp, "without Chain Lightning, a punch hits one");
        ar.Perks[Perk.ChainLightning] = 1;
        near.Health = near.Def.Health;
        p.Cooldown = 0; Tick(default, 20); p.Cooldown = 0; Tick(new Input { Fire = true });
        check(far.Health < farHp, $"with it, the hit arcs on to the next ({farHp} -> {far.Health})");
        ar.Perks[Perk.Bloodthirst] = 1;
        p.Health = 40; near.Health = 1;
        p.Cooldown = 0; Tick(default, 20); p.Cooldown = 0; Tick(new Input { Fire = true });
        check(!near.Alive && p.Health == 44, $"Bloodthirst: a kill heals 4 ({p.Health})");
        g.Vars.Freeze = false;

        // modifiers: each adds to the score multiplier
        check(ArenaModInfo.Multiplier(ArenaMod.None) == 1f && MathF.Abs(ArenaModInfo.Multiplier((ArenaMod)15) - 2.65f) < 0.001f,
              "no modifiers score x1; all four x2.65");
        check(ArenaModInfo.Score(10, ArenaMod.None) == 1000 && ArenaModInfo.Score(10, ArenaMod.DoubleSpeed | ArenaMod.NoSupplies) == 2000, "100 a wave, times the multiplier");

        // no supplies: a bare armoury and nothing between waves
        g = new Game { FixedSeed = 1 };
        g.ArenaMods = ArenaMod.NoSupplies;
        g.StartArena(PClass.Cleric);
        g.Vars.God = true;
        check(!g.Level.Things.Any(t => t is Pickup), "No supplies: the armoury is bare");
        ClearTo(1);
        check(g.Level.Arena.Has(ArenaMod.NoSupplies) && !g.Level.Things.Any(t => t is Pickup), "and nothing appears at the altar");
        ClearTo(3);
        check(g.Level.Things.OfType<Pickup>().Select(pk => pk.Kind).SequenceEqual(new[] { PickupKind.Upgrade }), "except the arsenal upgrade after wave 3");

        // melee only: your other weapons won't come out
        g.ArenaMods = ArenaMod.MeleeOnly;
        g.StartArena(PClass.Mage);
        g.P.HasWeapon[1] = g.P.HasWeapon[2] = true;
        g.Messages.Clear();
        Tick(new Input { Slot = 3 }); Tick(default, 20);
        check(g.P.Weapon == 0 && g.Messages.Any(m => m.text == "Melee only!"), "Melee only: your other weapons stay put");
        ClearTo(1);
        check(!g.Level.Things.Any(t => t is Pickup { Kind: PickupKind.Weapon2 or PickupKind.Weapon3 }), "and the altar doesn't hand you any");

        // double speed: monsters come twice as fast
        g.ArenaMods = ArenaMod.DoubleSpeed;
        g.StartArena(PClass.Fighter);
        var alt = g.Level.FindMark('!').Value;
        g.P.X = alt.x; g.P.Y = alt.y;
        Tick(default, 35 * 3);
        check(g.Level.Arena.Live.Count > 0 && g.Level.Arena.Live.All(m => m.SpeedMult == 2f), "Double-speed monsters: wave 1 comes at twice the speed");

        // random class: Start skips the class screen, and each run rolls again
        g = new Game { FixedSeed = 1 };
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Main), "Arena");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        g.Menu.Cursor = 3;
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        g.Menu.Cursor = ArenaModInfo.All.Length; // Start
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.ArenaMods == ArenaMod.RandomClass && g.ArenaMode && g.Mode == GameMode.Playing, "Random class: Start goes straight in");
        var classes = new HashSet<PClass>();
        for (int k = 0; k < 12; k++) { g.FixedSeed = k; g.NewGame(PClass.Fighter); classes.Add(g.P.Class); }
        check(classes.Count == 3, "with a class rolled for each run");

        // the run's score and modifiers go on the leaderboard, which ranks by score
        g = new Game { FixedSeed = 1 };
        g.ArenaMods = ArenaMod.DoubleSpeed | ArenaMod.NoSupplies | ArenaMod.MeleeOnly;
        g.StartArena(PClass.Fighter);
        g.Vars.God = true;
        ClearTo(3);
        g.GoToTitle();
        g.Profile.AddArenaRun(PClass.Fighter, new ArenaRun { Waves = 6, Time = 200 });
        var board = g.Profile.ArenaBoard(PClass.Fighter);
        check(board[0].Waves == 3 && board[0].Score == 750 && board[0].Mods == 7 && board[1].Score == 600,
              $"a run's score and modifiers are recorded, and a hard 3-wave run (750) beats a plain 6 (600)");
        check(g.Profile.ArenaBestWave(PClass.Fighter) == 6 && g.Profile.ArenaBestScore(PClass.Fighter) == 750, "your best wave still counts for medals");
        g.Paused = true; g.Menu.Show(MenuPage.Leaderboard); g.Menu.BoardArena = true; g.Menu.BoardClass = PClass.Fighter;
        r.Render(g);
        g.Menu.Close(); g.Paused = false;

        // runs saved before scores get one; the modifiers are kept with the settings
        string dir = Path.Combine(Path.GetTempPath(), $"hexensharp-arena-{Environment.ProcessId}");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "profile.json");
        File.WriteAllText(file, "{\"ArenaRuns\":{\"Mage\":[{\"Waves\":4,\"Time\":90},{\"Waves\":7,\"Time\":300}]}}");
        var old = Profile.Load(file);
        check(old.ArenaBoard(PClass.Mage) is [{ Waves: 7, Score: 700 }, { Waves: 4, Score: 400 }], "old runs score 100 a wave, and stay in order");
        Directory.Delete(dir, true);
        g.Con.Execute("arenamods mr");
        check(g.ArenaMods == (ArenaMod.MeleeOnly | ArenaMod.RandomClass) && Settings.Lines(g).Contains("arenamods MR"), "'arenamods' sets them, and they're saved with the settings");
        g.Con.Execute("arenamods -");
        check(g.ArenaMods == ArenaMod.None, "'arenamods -' clears them");
    }

    static void ArenaModeChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        var said = new List<string>();
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) { g.Update(i, 1f / 35f); said.AddRange(g.Messages.Select(m => m.text)); } }
        bool Said(string s) => said.Any(m => m.Contains(s));

        // Arena on the title menu, then a class, puts you in the Chaos Arena's armoury
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Main), "Arena");
        check(g.Menu.Cursor == 2, "Arena sits under Practice on the title menu");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Menu.Page == MenuPage.ArenaSetup && g.Menu.Items(MenuPage.ArenaSetup)[g.Menu.Cursor] == "Start", "which opens the arena's setup, on Start");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Mode == GameMode.ClassSelect && g.PendingArena, "and Start asks for a class");
        Tick(new Input { Slot = 3 });
        var a = g.Level.Arena;
        check(g.ArenaMode && g.P.Class == PClass.Mage && g.Level.RawName == "Chaos Arena" && g.Hub.Length == 1 && !g.Relaxed,
              "picking one starts the Chaos Arena on its own");
        check(a != null && !a.Started && g.Level.MarkAt(g.P.X, g.P.Y) != '!' && !g.Level.Things.Any(t => t is Chest),
              "you start in the armoury, with no chests about, and the waves wait for the altar");
        var pause = g.Menu.Items(MenuPage.Pause);
        check(pause.Contains("Leaderboard") && !pause.Contains("Watch demo"), "its pause menu has the leaderboard");

        // clear waves: medals at 5, 10 and 15, the same for every class
        check(ArenaMedals.For(4) == Medal.None && ArenaMedals.For(5) == Medal.Bronze && ArenaMedals.For(10) == Medal.Silver && ArenaMedals.For(15) == Medal.Gold && ArenaMedals.For(30) == Medal.Gold,
              "medals for clearing 5, 10 and 15 waves");
        check(ArenaMedals.Next(0) == (Medal.Bronze, 5) && ArenaMedals.Next(7) == (Medal.Silver, 10) && ArenaMedals.Next(15) == (Medal.None, 0), "and the next one to aim for");
        g.Vars.God = true;
        var altar = g.Level.FindMark('!').Value;
        g.P.X = altar.x; g.P.Y = altar.y;
        void ClearTo(int waves)
        {
            for (int f = 0; f < 35 * 60 * 3 && a.BestWave < waves; f++) { Tick(default); g.KillAll(); }
        }
        int xp0 = g.Profile.TotalXp;
        ClearTo(5);
        check(g.Profile.TotalXp > xp0, "clearing waves earns experience, as it always has");
        check(a.Started && a.BestWave == 5 && a.ClearedAt > 0 && Said("New medal: BRONZE!"), $"clearing wave 5 earns bronze ({a.BestWave} waves in {a.ClearedAt:0.0}s)");

        // dying ends the run and puts it on the leaderboard
        g.Vars.God = false;
        g.P.Kills = 42;
        g.DamagePlayer(100000);
        Tick(default);
        var board = g.Profile.ArenaBoard(PClass.Mage);
        check(g.Mode == GameMode.Dead && board.Count == 1 && board[0].Waves == 5 && board[0].Kills == 42 && board[0].Name == g.RunnerName && g.LastArenaPlace == 1,
              "dying ends the run: its waves, time and kills go on the leaderboard");
        check(Said("Run over: 5 waves") && Said("Your best!"), "and it tells you how you did");
        check(g.Profile.ArenaBestWave(PClass.Mage) == 5 && g.Profile.ArenaBestWave(PClass.Fighter) == 0, "each class has its own board");
        for (int f = 0; f < 35 * 3 && g.Mode == GameMode.Dead; f++) Tick(new Input { Confirm = f % 2 == 0 });
        check(g.Mode == GameMode.Playing && g.ArenaMode && !g.Level.Arena.Started && board.Count == 1, "Enter tries again from the armoury, and the run isn't counted twice");

        // a run that clears nothing isn't recorded; leaving mid-run records it
        a = g.Level.Arena;
        g.Paused = true; g.Menu.Show(MenuPage.Pause);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Pause), "Restart");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(board.Count == 1 && g.ArenaMode, "restarting before the altar doesn't record a run");
        a = g.Level.Arena;
        g.Vars.God = true;
        g.P.X = altar.x; g.P.Y = altar.y;
        ClearTo(2);
        g.GoToTitle();
        check(board.Count == 2 && board[0].Waves == 5 && board[1].Waves == 2 && !g.ArenaMode, "quitting to the title ends the run and records it, below a better one");

        // more waves beats fewer; as many waves, the quicker run is ahead
        var pr = new Profile();
        pr.AddArenaRun(PClass.Fighter, new ArenaRun { Waves = 3, Time = 90 });
        pr.AddArenaRun(PClass.Fighter, new ArenaRun { Waves = 6, Time = 300 });
        int place = pr.AddArenaRun(PClass.Fighter, new ArenaRun { Waves = 3, Time = 80 });
        var fb = pr.ArenaBoard(PClass.Fighter);
        check(place == 2 && fb.Select(r => (r.Waves, r.Time)).SequenceEqual(new[] { (6, 300f), (3, 80f), (3, 90f) }), "more waves first, then the quicker");
        for (int i = 0; i < 12; i++) pr.AddArenaRun(PClass.Fighter, new ArenaRun { Waves = 4, Time = 100 + i });
        check(fb.Count == Profile.BoardSize && fb[0].Waves == 6, "the board keeps the best ten");
        var back = System.Text.Json.JsonSerializer.Deserialize<Profile>(pr.ToJson());
        check(back.ArenaBestWave(PClass.Fighter) == 6 && back.ArenaBoard(PClass.Fighter).Count == Profile.BoardSize, "and it's saved with your profile");

        // the leaderboard: the arena's board is one Up/Down away, and opens on it from the arena's pause menu
        g.Con.Execute("arena mage");
        check(g.ArenaMode && g.P.Class == PClass.Mage && g.Level.RawName == "Chaos Arena", "'arena mage' in the console starts it too");
        g.Paused = true; g.Menu.Show(MenuPage.Pause); g.Menu.Show(MenuPage.Leaderboard);
        check(g.Menu.BoardArena && g.Menu.BoardClass == PClass.Mage, "in the arena, the leaderboard opens on the arena's board");
        var r = new Renderer();
        r.Render(g);
        check(r.Fb.Count(px => px == Medals.Colour(Medal.Bronze)) > 20, "with a medal by each run and the wave targets");
        g.Menu.Close(); g.Paused = false;

        // the HUD shows your best and the next medal
        r.Render(g);
        int bestPx = Enumerable.Range(10, 10).Sum(y => Enumerable.Range(Renderer.W - 100, 96).Count(x => r.Fb[y * Renderer.W + x] == Col.Rgb(255, 220, 90)));
        int nextPx = Enumerable.Range(20, 10).Sum(y => Enumerable.Range(Renderer.W - 100, 96).Count(x => r.Fb[y * Renderer.W + x] == Medals.Colour(Medal.Silver)));
        check(bestPx > 20 && nextPx > 20, "the HUD shows your best, and the next medal to go for");
        g.GoToTitle();
    }

    static void ArenaChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.StartArena(PClass.Fighter);
        g.Vars.God = true;
        var a = g.Level.Arena;
        check(a != null && !a.Started, "arena waits for the player");
        var altar = g.Level.FindMark('!').Value;
        g.P.X = altar.x; g.P.Y = altar.y;
        Tick(default);
        check(a.Started && a.Wave == 1, "stepping on the altar starts wave 1");
        int w1 = a.Remaining;
        Tick(default, 35 * 8);
        check(a.Live.Count > 0 && a.Live.All(m => m.State != AiState.Idle), "wave monsters spawn awake");
        float hp1 = a.Live.Max(m => m.Health / (float)m.Def.Health);

        var tiers = new List<int>();
        for (int wave = 1; wave <= 5; wave++)
        {
            for (int k = 0; k < 35 * 60 && !a.InIntermission; k++) { g.KillAll(); Tick(default); }
            if (wave == 1) check(a.InIntermission, "killing everything clears the wave");
            if (wave == 1) check(g.Level.Things.OfType<Pickup>().Any(), "supplies appear after a wave");
            if (a.Offer != null) Tick(new Input { Slot = 1 }); // after the boss wave, take a perk
            Tick(default, (int)(35 * ArenaState.Intermission) + 2);
            tiers.Add(g.P.ArenaTier);
        }
        check(tiers.SequenceEqual(new[] { 0, 0, 1, 1, 1 }), $"an arsenal upgrade drops on the altar after wave 3, not before (tiers {string.Join(",", tiers)})");
        check(a.Wave == 6, $"waves keep coming (now on wave {a.Wave})");
        Tick(default, 35 * 3);
        float hp6 = a.Live.Max(m => m.Health / (float)m.Def.Health);
        check(ArenaState.Compose(6, new Random(1)).Count > w1, "later waves have more monsters");
        check(hp6 > hp1, $"later waves are tougher (health x{hp1:0.00} -> x{hp6:0.00})");
        check(ArenaState.Compose(5, new Random(1)).Any(d => d.Boss), "wave 5 brings a Heresiarch");
        check(!ArenaState.Compose(1, new Random(1)).Any(d => d != Monster.Ettin), "wave 1 is only ettins");
        ArsenalChecks(check);
    }

    static void ArsenalChecks(Action<bool, string> check)
    {
        check(Enumerable.Range(0, 5).All(t => Arsenal.Damage(t + 1) > Arsenal.Damage(t) && Arsenal.FireRate(t + 1) > Arsenal.FireRate(t)),
              "each arsenal tier hits harder and fires faster");

        // a melee swing: one monster at tier 0, several at once from tier 2
        int Cleaved(int tier)
        {
            var g = new Game { FixedSeed = 1 };
            g.StartArena(PClass.Fighter);
            g.Vars.God = true;
            g.Level.Things.RemoveAll(t => t is Monster);
            g.P.ArenaTier = tier; g.P.Angle = 0; g.P.Weapon = 0;
            var ms = new[] { (0.8f, 0f), (0.9f, 0.3f), (0.9f, -0.3f) }
                .Select(o => new Monster(Monster.Ettin) { X = g.P.X + o.Item1, Y = g.P.Y + o.Item2, Level = g.Level }).ToList();
            g.Level.Things.AddRange(ms);
            for (int k = 0; k < 35 && ms.All(m => m.Health == m.Def.Health); k++) g.Update(new Input { Fire = k == 0 || ms.All(m => m.Health == m.Def.Health) }, 1f / 35f);
            return ms.Count(m => m.Health < m.Def.Health);
        }
        int c0 = Cleaved(0), c2 = Cleaved(2);
        check(c0 == 1 && c2 >= 2, $"an upgraded melee swing cleaves through several monsters ({c0} -> {c2})");

        // a ranged shot: more projectiles, hitting harder; outside the arena upgrades do nothing
        (int shots, int dmg) Volley(int tier, bool arena)
        {
            var g = new Game { FixedSeed = 1 };
            if (arena) g.StartArena(PClass.Mage); else g.NewGame(PClass.Mage);
            g.Level.Things.RemoveAll(t => t is Monster);
            g.P.ArenaTier = tier; g.P.Weapon = 0;
            for (int k = 0; k < 35 && !g.Level.Things.OfType<Projectile>().Any(p => p.FromPlayer); k++) g.Update(new Input { Fire = true }, 1f / 35f);
            var ps = g.Level.Things.OfType<Projectile>().Where(p => p.FromPlayer).ToList();
            return (ps.Count, ps.Count == 0 ? 0 : ps.Max(p => p.DmgMax));
        }
        var v0 = Volley(0, true); var v4 = Volley(4, true); var vOut = Volley(5, false);
        check(v0.shots > 0 && v4.shots >= v0.shots + 4 && v4.dmg > v0.dmg, $"upgraded shots fan out and hit harder ({v0.shots}x{v0.dmg} -> {v4.shots}x{v4.dmg})");
        check(vOut.shots == v0.shots && vOut.dmg == v0.dmg, "upgrades only count in the arena");

        // five in all: a sixth stays on the altar
        var h = new Game { FixedSeed = 1 };
        h.StartArena(PClass.Cleric);
        h.P.ArenaTier = Arsenal.MaxTier;
        var up = new Pickup(PickupKind.Upgrade, 0.5f) { X = h.P.X, Y = h.P.Y, Level = h.Level };
        h.Level.Things.Add(up);
        h.Update(default, 1f / 35f);
        check(h.P.ArenaTier == Arsenal.MaxTier && !up.Removed, "the arsenal tops out at five upgrades");
        h.StartArena(PClass.Cleric);
        check(h.P.ArenaTier == 0, "a new run starts with a plain arsenal");
    }

    static void DailyChecks(Action<bool, string> check)
    {
        var today = Daily.Today;
        var days = Enumerable.Range(0, 60).Select(k => today.AddDays(-k)).Select(Daily.For).ToList();
        check(Daily.For(today) == Daily.For(today) && days.Select(d => d.cls).Distinct().Count() == 3 && days.Select(d => d.mods).Distinct().Count() >= 4,
              "each day has its own class and modifiers, the same all day");
        check(days.All(d => (d.mods & ArenaMod.RandomClass) == 0 && System.Numerics.BitOperations.PopCount((uint)d.mods) is 1 or 2),
              "one or two modifiers, never a random class");

        // Arena > Daily challenge: the day's class and modifiers, without touching your own modifier picks
        var g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.ArenaMods = ArenaMod.MeleeOnly;
        g.Menu.Show(MenuPage.ArenaSetup);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.ArenaSetup), "Daily challenge");
        check(g.Menu.Cursor > 0, "the Arena's setup page has the daily challenge");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        var (cls, mods) = Daily.For(today);
        check(g.DailyMode && g.ArenaMode && g.P.Class == cls && g.Level.Arena.Mods == mods, $"it goes straight in, as the day's class with its modifiers ({Daily.Describe(today)})");
        check(g.ArenaMods == ArenaMod.MeleeOnly && Settings.Lines(g).Contains("arenamods M"), "and your own modifier picks stay as they were");

        // the same dice for everyone today
        var other = new Game { FixedSeed = 99, AchievementsOn = false };
        other.StartDaily();
        var o1 = Enumerable.Range(0, 5).SelectMany(_ => g.Level.Arena.RollOffer()).ToList();
        var o2 = Enumerable.Range(0, 5).SelectMany(_ => other.Level.Arena.RollOffer()).ToList();
        var yesterday = new Game { FixedSeed = 1, AchievementsOn = false };
        yesterday.StartDaily(today.AddDays(-1));
        var o3 = Enumerable.Range(0, 5).SelectMany(_ => yesterday.Level.Arena.RollOffer()).ToList();
        check(o1.SequenceEqual(o2) && !o1.SequenceEqual(o3), "every attempt today rolls the same perks (and waves), another day's differ");

        // the day's first finished run is your score; the rest are practice
        void Play(Game gg, int waves)
        {
            gg.Vars.God = true;
            var a = gg.Level.Arena;
            var altar = gg.Level.FindMark('!').Value;
            gg.P.X = altar.x; gg.P.Y = altar.y;
            for (int f = 0; f < 35 * 60 * 3 && a.BestWave < waves; f++) { gg.Update(default, 1f / 35f); gg.KillAll(); if (a.Offer != null) gg.Update(new Input { Slot = 1 }, 1f / 35f); }
            gg.Vars.God = false;
        }
        g.RunnerName = "TESTER";
        Play(g, 2);
        g.GoToTitle();
        var mine = g.Profile.DailyRuns.Where(r => r.Name == "TESTER").ToList();
        check(mine.Count == 1 && mine[0].Date == today.ToString("yyyy-MM-dd") && mine[0].Waves == 2 && mine[0].Score == ArenaModInfo.Score(2, mods) && g.LastDailyScored,
              $"your first finished run today is your score ({mine[0].Score})");
        check(g.Profile.ArenaBoard(cls).Count == 0 && !g.DailyMode, "(it goes on the daily board, not the arena's)");
        g.StartDaily();
        Play(g, 3);
        g.GoToTitle();
        check(g.Profile.DailyRuns.Count(r => r.Name == "TESTER") == 1 && !g.LastDailyScored && g.LastDaily.Waves == 3, "another go today is practice: the score stays");
        g.Con.Execute("daily " + today.AddDays(-3).ToString("yyyy-MM-dd"));
        check(g.DailyMode && g.DailyDate == today.AddDays(-3), "'daily <date>' plays an old day's challenge");
        Play(g, 1);
        g.GoToTitle();
        check(g.Profile.DailyRuns.Count(r => r.Name == "TESTER") == 1, "but only today's is scored");

        // streaks, the board, and the Regular achievement
        g.Profile.DailyRuns.Add(new DailyRun { Date = today.AddDays(-1).ToString("yyyy-MM-dd"), Name = "TESTER", Score = 500 });
        g.Profile.DailyRuns.Add(new DailyRun { Date = today.AddDays(-2).ToString("yyyy-MM-dd"), Name = "TESTER", Score = 900 });
        g.Profile.DailyRuns.Add(new DailyRun { Date = today.AddDays(-4).ToString("yyyy-MM-dd"), Name = "TESTER", Score = 100 });
        g.Profile.DailyRuns.Add(new DailyRun { Date = today.ToString("yyyy-MM-dd"), Name = "RIVAL", Score = 99999, Waves = 30 });
        check(Daily.Streak(g.Profile, "TESTER", today) == 3 && Daily.Streak(g.Profile, "TESTER", today.AddDays(1)) == 3, "a streak counts the days in a row, up to today (or yesterday, till you play)");
        g.Paused = true; g.Menu.Show(MenuPage.Leaderboard);
        for (int k = 0; k < 16 && !g.Menu.BoardDaily; k++) g.Menu.Update(new Input { Down = true }, 1f / 35f);
        var r = new Renderer();
        r.Render(g);
        int top = r.Fb.Count(px => px == Col.Rgb(230, 190, 80)), mineLit = r.Fb.Count(px => px == Col.Rgb(120, 255, 140));
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        check(g.Menu.BoardDaily && g.Menu.BoardDay == 1 && top > 50 && mineLit > 50, "the leaderboard's Daily page ranks the day's names, yours picked out; Left goes back a day");
        g.Menu.Close(); g.Paused = false;
        foreach (int k in new[] { 5, 6, 7 }) g.Profile.DailyRuns.Add(new DailyRun { Date = today.AddDays(-k).ToString("yyyy-MM-dd"), Name = "TESTER" });
        check(Achievements.Find("daily_7").Done(g), "seven different days is the Regular achievement");
    }
}
