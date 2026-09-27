namespace HexenSharp;

/// <summary>Windowless tools: map validation and scripted screenshots (useful for CI and debugging).</summary>
public static class Headless
{
    /// <summary>Checks every map loads and that the critical path is reachable.</summary>
    public static int SelfTest()
    {
        var hub = Maps.BuildHub();
        int failures = 0;
        void Check(bool ok, string what)
        {
            Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
            if (!ok) failures++;
        }

        foreach (var lv in hub)
        {
            Console.WriteLine($"{lv.Name}: {lv.W}x{lv.H}, {lv.Things.Count} things");
            // player start (or arrival portal) reaches every portal/exit/key when doors, gates and key doors are passable
            var arrival = Enumerable.Range(0, lv.Marks.Length).First(i => char.IsDigit(lv.Marks[i]));
            var start = lv == hub[0] ? ((int)lv.StartX, (int)lv.StartY) : (arrival % lv.W, arrival / lv.W);
            var reach = lv.Reachable(start.Item1, start.Item2);
            for (int i = 0; i < lv.Marks.Length; i++)
                if (lv.Marks[i] != '\0') Check(reach[i], $"mark '{lv.Marks[i]}' at {i % lv.W},{i / lv.W} reachable");
            foreach (var t in lv.Things)
            {
                if (t is Pickup pk && pk.Kind is PickupKind.SteelKey or PickupKind.FireKey or PickupKind.Weapon2 or PickupKind.Weapon3)
                    Check(reach[(int)t.Y * lv.W + (int)t.X], $"{pk.Kind} reachable");
                if (t is Monster m && m.Def.Boss) Check(reach[(int)t.Y * lv.W + (int)t.X], "boss reachable");
                Check(!lv.BlocksPoint(t.X, t.Y), $"{t.GetType().Name} at {t.X - 0.5f},{t.Y - 0.5f} not inside a wall");
            }
            // levers must be touchable from an open cell
            for (int i = 0; i < lv.Cells.Length; i++)
                if (lv.Cells[i] == 'L')
                {
                    int x = i % lv.W, y = i / lv.W;
                    bool ok = (x > 0 && reach[i - 1]) || (x < lv.W - 1 && reach[i + 1]) || (y > 0 && reach[i - lv.W]) || (y < lv.H - 1 && reach[i + lv.W]);
                    Check(ok, $"lever at {x},{y} usable");
                }
        }
        Console.WriteLine("Gameplay:");
        GameplayChecks(Check);

        // every portal digit must exist in exactly two maps
        foreach (char d in hub.SelectMany(l => l.Marks).Where(char.IsDigit).Distinct())
            Check(hub.Count(l => l.FindMark(d) != null) == 2, $"portal {d} links exactly two maps");

        Console.WriteLine("Hub progression:");
        HubChecks(Check);
        Console.WriteLine("Jumping and sliding:");
        MovementChecks(Check);
        Console.WriteLine("Console and cheats:");
        ConsoleChecks(Check);
        Console.WriteLine("Chaos Arena waves:");
        ArenaChecks(Check);

        Console.WriteLine("Chests:");
        ChestChecks(Check);

        Console.WriteLine("Audio synthesis:");
        bool audioOk = true;
        for (int i = 0; i < (int)Sfx.Count; i++) audioOk &= Audio.Synth((Sfx)i).Length > 1000;
        Check(audioOk, "all sound effects synthesize");

        Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    static void GameplayChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);

        // melee: stand next to the ettin in the great hall and punch until it dies
        var ettin = g.Level.Things.OfType<Monster>().First(m => (int)m.X == 14 && (int)m.Y == 4);
        g.P.X = 12.8f; g.P.Y = 4.5f; g.P.Angle = 0;
        int startHp = g.P.Health;
        for (int k = 0; k < 35 * 20 && ettin.Alive; k++)
        {
            g.P.Angle = MathF.Atan2(ettin.Y - g.P.Y, ettin.X - g.P.X);
            Tick(new Input { Fire = true });
            if (g.Mode == GameMode.Dead) break;
        }
        check(!ettin.Alive, "gauntlets kill an ettin");
        check(g.P.Kills >= 1, "kill counted");

        // lever raises the portcullis
        g.NewGame(PClass.Cleric);
        int gate = Array.IndexOf(g.Level.Cells, 'P');
        g.P.X = 11.5f; g.P.Y = 10.5f; g.P.Angle = MathF.PI / 2;
        Tick(new Input { Use = true });
        Tick(default, 35 * 2);
        check(g.Level.LeverPulled && g.Level.DoorOpen[gate] >= 1f, "lever opens the portcullis");

        // plain door opens on use
        g.P.X = 6.4f; g.P.Y = 3.5f; g.P.Angle = 0;
        Tick(new Input { Use = true });
        Tick(default, 35);
        check(g.Level.DoorOpen[3 * g.Level.W + 7] >= 1f, "wooden door opens");

        // steel door refuses without the key
        g.P.X = 9.5f; g.P.Y = 18.5f; g.P.Angle = MathF.PI;
        Tick(new Input { Use = true });
        Tick(default, 35);
        int steel = 18 * g.Level.W + 8;
        check(g.Level.DoorOpen[steel] == 0f, "steel door stays locked without key");
        g.P.SteelKey = true;
        Tick(new Input { Use = true });
        Tick(default, 35);
        check(g.Level.DoorOpen[steel] >= 1f, "steel door opens with key");

        // portal travel between hub maps keeps level state
        var portal = g.Level.FindMark('1').Value;
        g.P.X = portal.x; g.P.Y = portal.y;
        Tick(default);
        check(g.Level == g.Hub[1], "portal leads to the Frozen Keep");
        g.P.X += 1.2f; Tick(default);
        g.P.X -= 1.2f; Tick(default);
        check(g.Level == g.Hub[0] && g.Level.LeverPulled, "portal returns to Winnowing Hall with its state kept");

        // pickups
        g.NewGame(PClass.Mage);
        var key = g.Hub[1].Things.OfType<Pickup>().First(p => p.Kind == PickupKind.SteelKey);
        g.Level = g.Hub[1]; g.P.X = key.X; g.P.Y = key.Y;
        Tick(default);
        check(g.P.SteelKey, "steel key picked up");

        // projectile weapon damages, boss death unseals the exit, exit wins
        g.NewGame(PClass.Mage);
        var boss = g.Level.Things.OfType<Monster>().First(m => m.Def.Boss);
        boss.Health = 20;
        g.P.X = 7.5f; g.P.Y = 18.5f; g.P.Angle = MathF.PI; g.P.Health = 1000;
        for (int k = 0; k < 35 * 10 && boss.Alive; k++)
        {
            g.P.Angle = MathF.Atan2(boss.Y - g.P.Y, boss.X - g.P.X);
            Tick(new Input { Fire = true });
        }
        check(!boss.Alive && g.Level.BossDead, "sapphire wand kills the (weakened) Heresiarch");
        var exit = g.Level.FindMark('E').Value;
        g.P.X = exit.x; g.P.Y = exit.y;
        Tick(default);
        check(g.Mode == GameMode.Victory, "stepping on the exit after the boss wins the game");

        // player can die and restart
        g.NewGame(PClass.Fighter);
        g.P.Armor = 0;
        g.Level.Things.Add(new Projectile { Kind = ProjKind.BossBall, DmgMin = 500, DmgMax = 500, X = g.P.X + 0.5f, Y = g.P.Y, VX = -5, Level = g.Level });
        Tick(default, 10);
        check(g.Mode == GameMode.Dead, "player dies from a lethal hit");
        Tick(default, 35 * 2);
        Tick(new Input { Confirm = true });
        check(g.Mode == GameMode.Playing && g.P.Health == 100, "restart after death");
    }

    static void HubChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Cleric);
        g.Vars.God = true;

        // Frozen Keep: the fire door guards the lever room
        g.Warp(1);
        int fire = Array.IndexOf(g.Level.Cells, 'F');
        int fx = fire % g.Level.W, fy = fire / g.Level.W;
        g.P.X = fx + 0.5f; g.P.Y = fy - 0.6f; g.P.Angle = MathF.PI / 2;
        Tick(new Input { Use = true }); Tick(default, 35);
        check(g.Level.DoorOpen[fire] == 0f, "fire door stays locked without the Fire Key");

        // portal 2 leads to Darkmere Crypt, which holds the Fire Key behind a two-lever gate
        var p2 = g.Level.FindMark('2').Value;
        g.P.X = p2.x; g.P.Y = p2.y; Tick(default);
        check(g.Level == g.Hub[2], "portal 2 leads to Darkmere Crypt");
        var crypt = g.Level;
        check(crypt.LeverCount == 2, "crypt has two levers");
        int gate = Array.IndexOf(crypt.Cells, 'P');
        var levers = Enumerable.Range(0, crypt.Cells.Length).Where(i => crypt.Cells[i] == 'L').ToList();
        foreach (var (li, n) in levers.Select((l, n) => (l, n)))
        {
            int lx = li % crypt.W, ly = li / crypt.W;
            // stand on the open side of the lever and face it
            var (sx, sy, a) = !crypt.Blocks(lx - 1, ly) ? (lx - 0.5f, ly + 0.5f, 0f) : (lx + 1.5f, ly + 0.5f, MathF.PI);
            g.P.X = sx; g.P.Y = sy; g.P.Angle = a;
            Tick(new Input { Use = true }); Tick(default, 35 * 2);
            if (n == 0) check(crypt.DoorOpen[gate] == 0f, "one lever is not enough");
        }
        check(crypt.DoorOpen[gate] >= 1f, "both levers raise the crypt gate");
        var key = crypt.Things.OfType<Pickup>().First(p => p.Kind == PickupKind.FireKey);
        g.P.X = key.X; g.P.Y = key.Y; Tick(default);
        check(g.P.FireKey, "Fire Key picked up");

        g.Warp(1);
        g.P.X = fx + 0.5f; g.P.Y = fy - 0.6f; g.P.Angle = MathF.PI / 2;
        Tick(new Input { Use = true }); Tick(default, 35);
        check(g.Level.DoorOpen[fire] >= 1f, "Fire Key opens the fire door");

        // portal 3 in the courtyard reaches the arena
        g.Warp(0);
        Tick(default); // step off the arrival spot so portals re-arm
        var p3 = g.Level.FindMark('3').Value;
        g.P.X = p3.x; g.P.Y = p3.y; Tick(default);
        check(g.Level == g.Hub[3], "portal 3 leads to the Chaos Arena");
    }

    static void MovementChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster);

        Tick(new Input { Jump = true });
        float peak = 0;
        for (int k = 0; k < 35; k++) { Tick(default); peak = MathF.Max(peak, g.P.Z); }
        check(peak > 0.35f && g.P.OnGround, $"jump rises (peak {peak:0.00}) and lands");

        // a low missile passes under a jumping player
        g.P.X = 3.5f; g.P.Y = 2.5f; g.P.Health = 100;
        Tick(new Input { Jump = true }); Tick(default, 5);
        g.Level.Things.Add(new Projectile { Kind = ProjKind.CentaurBolt, DmgMin = 10, DmgMax = 10, X = g.P.X + 0.4f, Y = g.P.Y, Z = 0.2f, VX = -3, Level = g.Level });
        Tick(default, 3);
        check(g.P.Health == 100, "jumping dodges a low missile");
        Tick(default, 35);

        // sliding ducks under a chest-high missile and covers more ground
        g.P.X = 2.5f; g.P.Y = 2.5f; g.P.Angle = 0;
        float x0 = g.P.X;
        Tick(new Input { Move = 1 }, 10);
        float walked = g.P.X - x0;
        g.P.X = 2.5f; x0 = g.P.X;
        Tick(new Input { Move = 1, Slide = true }); Tick(new Input { Move = 1 }, 5);
        g.Level.Things.Add(new Projectile { Kind = ProjKind.Fireball, DmgMin = 10, DmgMax = 10, X = g.P.X + 0.35f, Y = g.P.Y, Z = 0.45f, VX = -3, Level = g.Level });
        Tick(new Input { Move = 1 }, 4);
        float slid = g.P.X - x0;
        check(slid > walked * 1.3f, $"slide covers more ground ({slid:0.00} vs {walked:0.00})");
        check(g.P.Health == 100, "sliding ducks under a fireball");
    }

    static void ConsoleChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Mage);

        Tick(new Input { ConsoleToggle = true });
        check(g.Con.Open, "~ opens the console");
        float ang = g.P.Angle;
        Tick(new Input { Typed = "give all", LookX = 300 });
        Tick(new Input { Confirm = true });
        check(g.P.HasWeapon[2] && g.P.SteelKey && g.P.FireKey && g.P.GreenMana == 200, "'give all' via typed input");
        check(g.P.Angle == ang, "game input is ignored while the console is open");
        g.Con.Execute("set speed 2");
        check(g.Vars.Speed == 2f, "'set speed 2'");
        g.Con.Execute("monsterdamage 0.5");
        check(g.Vars.MonsterDamage == 0.5f, "shorthand 'monsterdamage 0.5'");
        g.Con.Execute("reset");
        check(g.Vars.Speed == 1f && g.Vars.MonsterDamage == 1f, "'reset' restores defaults");
        g.Con.Execute("bogus");
        check(g.Con.Log[^1].Contains("unknown"), "unknown command reported");
        g.Con.Execute("map 3");
        check(g.Level == g.Hub[2], "'map 3' warps to Darkmere Crypt");
        int before = g.Level.Things.Count;
        g.Con.Execute("summon ettin");
        check(g.Level.Things.Count == before + 1, "'summon ettin'");
        g.Con.Execute("kill");
        check(g.Level.Things.OfType<Monster>().All(m => !m.Alive), "'kill' clears the map");
        Tick(new Input { Pause = true });
        check(!g.Con.Open, "Esc closes the console");

        // classic cheat codes typed during play
        foreach (char c in "satan") Tick(new Input { Typed = c.ToString() });
        check(g.Vars.God, "typing 'satan' enables god mode");
        g.Level.Things.Add(new Projectile { Kind = ProjKind.BossBall, DmgMin = 500, DmgMax = 500, X = g.P.X + 0.4f, Y = g.P.Y, Z = 0.3f, VX = -5, Level = g.Level });
        Tick(default, 5);
        check(g.Mode == GameMode.Playing && g.P.Health > 0, "god mode survives a lethal hit");
        foreach (char c in "visit4") Tick(new Input { Typed = c.ToString() });
        check(g.Level == g.Hub[3], "typing 'visit4' warps to the arena");
        foreach (char c in "mapsco") Tick(new Input { Typed = c.ToString() });
        check(g.Level.Seen.All(s => s), "typing 'mapsco' reveals the map");
    }

    static void ArenaChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);
        g.Vars.God = true;
        g.Warp(3);
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

        for (int wave = 1; wave <= 5; wave++)
        {
            for (int k = 0; k < 35 * 60 && !a.InIntermission; k++) { g.KillAll(); Tick(default); }
            if (wave == 1) check(a.InIntermission, "killing everything clears the wave");
            if (wave == 1) check(g.Level.Things.OfType<Pickup>().Any(), "supplies appear after a wave");
            Tick(default, (int)(35 * ArenaState.Intermission) + 2);
        }
        check(a.Wave == 6, $"waves keep coming (now on wave {a.Wave})");
        Tick(default, 35 * 3);
        float hp6 = a.Live.Max(m => m.Health / (float)m.Def.Health);
        check(ArenaState.Compose(6, new Random(1)).Count > w1, "later waves have more monsters");
        check(hp6 > hp1, $"later waves are tougher (health x{hp1:0.00} -> x{hp6:0.00})");
        check(ArenaState.Compose(5, new Random(1)).Any(d => d.Boss), "wave 5 brings a Heresiarch");
        check(!ArenaState.Compose(1, new Random(1)).Any(d => d != Monster.Ettin), "wave 1 is only ettins");
    }

    static void ChestChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 99 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);

        foreach (var lv in g.Hub)
        {
            var chests = lv.Things.OfType<Chest>().ToList();
            check(chests.Count >= 1, $"{lv.Name}: {chests.Count} chest(s) placed");
            var (sx, sy) = lv.ArrivalCell();
            var cells = chests.Select(c => (int)c.Y * lv.W + (int)c.X).ToHashSet();
            var open = lv.Reachable(sx, sy);
            var withChests = lv.Reachable(sx, sy, cells);
            check(open.Count(r => r) - cells.Count == withChests.Count(r => r), $"{lv.Name}: chests never cut off part of the map");
            check(cells.All(c => open[c]), $"{lv.Name}: every chest is reachable");
            check(chests.All(c => lv.MarkAt(c.X, c.Y) == '\0' && !lv.BlocksPoint(c.X, c.Y)), $"{lv.Name}: chests avoid walls and runes");
        }
        check(g.ChestsTotal == g.Hub.Sum(l => l.Things.Count(t => t is Chest)), $"chest total tracked ({g.ChestsTotal})");

        // many random layouts: chests must never block the way or sit on top of anything
        int bad = 0, min = int.MaxValue, max = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            var sg = new Game { FixedSeed = seed };
            sg.NewGame(PClass.Fighter);
            min = Math.Min(min, sg.ChestsTotal); max = Math.Max(max, sg.ChestsTotal);
            foreach (var lv in sg.Hub)
            {
                var (sx, sy) = lv.ArrivalCell();
                var cells = lv.Things.OfType<Chest>().Select(c => (int)c.Y * lv.W + (int)c.X).ToHashSet();
                if (lv.Reachable(sx, sy).Count(r => r) - cells.Count != lv.Reachable(sx, sy, cells).Count(r => r)) bad++;
                foreach (var c in lv.Things.OfType<Chest>())
                    if (lv.Things.Any(t => t != c && Game.Dist(t.X, t.Y, c.X, c.Y) < 0.9f)) bad++;
            }
        }
        check(bad == 0, $"100 random layouts: no blocked paths or overlaps ({min}-{max} chests per game)");

        // same seed, same layout; different seed, different layout
        string Layout(Game gg) => string.Join(";", gg.Hub.SelectMany(l => l.Things.OfType<Chest>()).Select(c => $"{c.X},{c.Y}"));
        var g2 = new Game { FixedSeed = 99 }; g2.NewGame(PClass.Mage);
        var g3 = new Game { FixedSeed = 12345 }; g3.NewGame(PClass.Mage);
        check(Layout(g) == Layout(g2), "same seed gives the same chest layout");
        check(Layout(g) != Layout(g3), "a different seed moves the chests");

        // opening: face the chest and press Use
        var lv0 = g.Level;
        var chest = lv0.Things.OfType<Chest>().First();
        float ax = chest.X, ay = chest.Y;
        foreach (var (dx, dy) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
            if (!lv0.BlocksCircle(chest.X + dx, chest.Y + dy, 0.26f)) { ax = chest.X + dx; ay = chest.Y + dy; break; }
        g.P.X = ax; g.P.Y = ay; g.P.Angle = MathF.Atan2(chest.Y - ay, chest.X - ax);
        int pickups = lv0.Things.Count(t => t is Pickup);
        Tick(new Input { Use = true });
        check(chest.Opened && g.P.ChestsOpened == 1, "Use opens the chest you're facing");
        check(lv0.Things.Count(t => t is Pickup) > pickups, "the chest spills loot");
        Tick(new Input { Use = true });
        check(g.P.ChestsOpened == 1, "an open chest can't be looted twice");

        // loot table and traps over many rolls
        var rng = new Random(3);
        var p = new Player();
        var rolls = Enumerable.Range(0, 2000).Select(_ => Chests.RollLoot(rng, p)).ToList();
        check(rolls.All(r => r.Count is >= 1 and <= 3), "each chest holds 1-3 items");
        check(rolls.All(r => r.Count(c => c == 'w') <= 1), "at most one weapon piece of a kind per chest");
        p.HasWeapon[1] = p.HasWeapon[2] = true;
        check(Enumerable.Range(0, 500).All(_ => !Chests.RollLoot(rng, p).Any(c => c is 'w' or 'x')), "no weapon pieces once you own the weapons");

        int traps = 0;
        var tg = new Game { FixedSeed = 5 };
        tg.NewGame(PClass.Fighter);
        tg.Vars.God = true;
        for (int k = 0; k < 200; k++)
        {
            int before = tg.Level.Things.Count(t => t is Monster);
            var c = new Chest { X = 14.5f, Y = 7.5f, Level = tg.Level };
            tg.Level.Things.Add(c);
            tg.OpenChest(c);
            if (tg.Level.Things.Count(t => t is Monster) > before) traps++;
            tg.Level.Things.RemoveAll(t => t is Monster m && m.State == AiState.Chase || t == c || t is Pickup);
        }
        check(traps is > 8 and < 50, $"some chests are traps ({traps}/200)");

        // console: set chests 0 then restart, and summon chest
        var cg = new Game { FixedSeed = 1 };
        cg.NewGame(PClass.Cleric);
        cg.Con.Execute("set chests 0");
        cg.Con.Execute("restart");
        check(cg.ChestsTotal == 0, "'set chests 0' + restart removes chests");
        cg.Con.Execute("summon chest");
        check(cg.Level.Things.OfType<Chest>().Count() == 1, "'summon chest'");
    }

    /// <summary>Drives the game with scripted input and writes PNGs (3x upscaled) to a folder.</summary>
    public static int Screenshots(string dir)
    {
        Directory.CreateDirectory(dir);
        var g = new Game { FixedSeed = 1 };
        var r = new Renderer();
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        void Shot(string name)
        {
            r.Render(g);
            var big = new uint[Renderer.W * 3 * Renderer.H * 3];
            for (int y = 0; y < Renderer.H * 3; y++)
                for (int x = 0; x < Renderer.W * 3; x++)
                    big[y * Renderer.W * 3 + x] = r.Fb[(y / 3) * Renderer.W + x / 3];
            string path = Path.Combine(dir, name + ".png");
            Png.Save(path, big, Renderer.W * 3, Renderer.H * 3);
            Console.WriteLine("wrote " + path);
        }

        Tick(default, 10);
        Shot("01_title");
        Tick(new Input { Confirm = true });
        Tick(new Input { Down = true });
        Shot("02_class_select");

        foreach (PClass cls in Enum.GetValues<PClass>())
        {
            g.NewGame(cls);
            Tick(default, 5);
            Shot($"03_start_{cls.ToString().ToLowerInvariant()}");
        }

        // Great hall, looking at the lever/gate wall with the Fighter's axe
        g.NewGame(PClass.Fighter);
        g.P.HasWeapon[1] = true; g.P.Weapon = 1;
        g.P.X = 14.5f; g.P.Y = 5.5f; g.P.Angle = MathF.PI / 2;
        Tick(default, 3);
        Shot("04_great_hall");
        Tick(new Input { Fire = true }, 3);
        Shot("05_attack");

        // gate half-raised, seen from inside the hall
        g.P.X = 17.5f; g.P.Y = 8.5f; g.P.Angle = MathF.PI / 2; g.P.Pitch = -10;
        for (int i = 0; i < g.Level.Cells.Length; i++)
        {
            if (g.Level.Cells[i] == 'L') g.Level.PulledLevers.Add(i);
            if (g.Level.Cells[i] == 'P') g.Level.DoorOpen[i] = 0.45f;
        }
        Tick(default, 1);
        Shot("06_portcullis");

        // outdoor courtyard with sky, looking at the portal
        g.P.X = 20.5f; g.P.Y = 21.5f; g.P.Angle = -MathF.PI / 2 - 0.3f; g.P.Pitch = 20;
        Tick(default, 1);
        Shot("07_courtyard");

        // through the portal to the Frozen Keep (foggy)
        var cleric = PClass.Cleric;
        g.NewGame(cleric);
        g.P.HasWeapon[2] = true; g.P.Weapon = 2; g.P.GreenMana = 100;
        var portal = g.Level.FindMark('1').Value;
        g.P.X = portal.x; g.P.Y = portal.y;
        Tick(default, 2);
        g.P.X = 7.5f; g.P.Y = 3.5f; g.P.Angle = 0.1f;
        Tick(default, 20);
        Shot("08_frozen_keep");
        Tick(new Input { Fire = true }, 4);
        Shot("09_firestorm");

        // boss arena as the Mage
        g.NewGame(PClass.Mage);
        g.P.SteelKey = true;
        g.P.X = 7.5f; g.P.Y = 18.5f; g.P.Angle = MathF.PI;
        Tick(default, 25);
        Shot("10_heresiarch");

        // automap
        g.ShowMap = true;
        Shot("11_automap");
        g.ShowMap = false;

        // Darkmere Crypt courtyard, looking toward the lever-gated hall
        g.NewGame(PClass.Fighter);
        g.Warp(2);
        g.P.X = 13.5f; g.P.Y = 6.5f; g.P.Angle = -MathF.PI / 2 + 0.4f; g.P.Pitch = 10;
        g.Level.Things.RemoveAll(t => t is Monster m && m.Def != Monster.Ettin);
        Tick(default, 50); // let the teleport flash fade
        Shot("13_darkmere_crypt");

        // Chaos Arena mid-wave
        g.NewGame(PClass.Cleric);
        g.Vars.God = true; g.Vars.Freeze = false;
        g.Warp(3);
        var altar = g.Level.FindMark('!').Value;
        g.P.X = altar.x; g.P.Y = altar.y;
        Tick(default, 1);
        g.P.X = altar.x - 3; g.P.Angle = MathF.PI + 0.1f;
        Tick(default, 35 * 4);
        g.Vars.Freeze = true;
        g.P.X = altar.x + 2; g.P.Y = altar.y; g.P.Angle = MathF.PI;
        Tick(default, 2);
        Shot("14_arena_wave");

        // jumping (camera raised) with the console open
        g.P.VZ = 3.3f;
        for (int k = 0; k < 4; k++) g.Update(default, 1f / 35f);
        g.Vars.God = false; g.Vars.Freeze = false;
        g.Con.Open = true;
        g.Con.Execute("help");
        g.Con.Execute("set fov 90");
        g.Con.Line = "summon here";
        Shot("15_console");
        g.Con.Open = false;
        g.Vars.Fov = 74;

        // a chest, closed then opened
        g.FixedSeed = 3;
        g.NewGame(PClass.Fighter);
        {
            var lv = g.Level;
            var chest = lv.Things.OfType<Chest>().First(c => !lv.Outdoor[(int)c.Y * lv.W + (int)c.X]);
            foreach (var (dx, dy) in new[] { (1.4f, 0f), (-1.4f, 0f), (0f, 1.4f), (0f, -1.4f) })
                if (!lv.BlocksCircle(chest.X + dx, chest.Y + dy, 0.26f)) { g.P.X = chest.X + dx; g.P.Y = chest.Y + dy; break; }
            g.P.Angle = MathF.Atan2(chest.Y - g.P.Y, chest.X - g.P.X);
            g.P.Pitch = -25;
            g.Vars.Freeze = true;
            Tick(default, 50);
            Shot("16_chest_closed");
            g.OpenChest(chest);
            Tick(default, 12);
            Shot("17_chest_open");
            g.Vars.Freeze = false;
        }

        // victory screen
        g.Mode = GameMode.Victory;
        Shot("12_victory");
        return 0;
    }
}
