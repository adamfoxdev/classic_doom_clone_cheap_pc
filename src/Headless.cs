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
            var start = lv == hub[0] ? ((int)lv.StartX, (int)lv.StartY) : ((int)lv.FindMark('1')!.Value.x, (int)lv.FindMark('1')!.Value.y);
            var reach = Flood(lv, start.Item1, start.Item2);
            for (int i = 0; i < lv.Marks.Length; i++)
                if (lv.Marks[i] != '\0') Check(reach[i], $"mark '{lv.Marks[i]}' at {i % lv.W},{i / lv.W} reachable");
            foreach (var t in lv.Things)
            {
                if (t is Pickup pk && pk.Kind is PickupKind.SteelKey or PickupKind.Weapon2 or PickupKind.Weapon3)
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

        Console.WriteLine("Audio synthesis:");
        bool audioOk = true;
        for (int i = 0; i < (int)Sfx.Count; i++) audioOk &= Audio.Synth((Sfx)i).Length > 1000;
        Check(audioOk, "all sound effects synthesize");

        Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    static void GameplayChecks(Action<bool, string> check)
    {
        var g = new Game();
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

    static bool[] Flood(Level lv, int sx, int sy)
    {
        var seen = new bool[lv.W * lv.H];
        var q = new Queue<(int, int)>();
        q.Enqueue((sx, sy));
        seen[sy * lv.W + sx] = true;
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (!lv.InBounds(nx, ny)) continue;
                int i = ny * lv.W + nx;
                if (seen[i]) continue;
                char c = lv.Cells[i];
                if (c != '\0' && !Level.IsDoor(c)) continue;
                seen[i] = true;
                q.Enqueue((nx, ny));
            }
        }
        return seen;
    }

    /// <summary>Drives the game with scripted input and writes PNGs (3x upscaled) to a folder.</summary>
    public static int Screenshots(string dir)
    {
        Directory.CreateDirectory(dir);
        var g = new Game();
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
        g.Level.LeverPulled = true;
        for (int i = 0; i < g.Level.Cells.Length; i++) if (g.Level.Cells[i] == 'P') g.Level.DoorOpen[i] = 0.45f;
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

        // victory screen
        g.Mode = GameMode.Victory;
        Shot("12_victory");
        return 0;
    }
}
