namespace HexenSharp;

/// <summary>The scripted screenshot tour (--shots) and the art review sheets.</summary>
public static partial class Headless
{
    static Tex[] SheetItems() => new[]
    {
        Art.Vial, Art.Flask, Art.Urn, Art.BlueMana, Art.GreenMana, Art.SteelKey, Art.FireKey, Art.Armor, Art.Jetpack,
        Art.WeaponPiece2, Art.WeaponPiece3, Art.Stone, Art.Marble, Art.Brick, Art.FloorStone,
    };

    /// <summary>Each monster's walk, walk, attack, pain and two death frames, one monster per row.</summary>
    static Tex[] SheetMonsters() =>
        new[] { "afrit", "ettin", "centaur", "slaughtaur", "bishop", "heresiarch" }
            .SelectMany(m => new[] { Pose.Walk0, Pose.Walk1, Pose.Attack, Pose.Pain, Pose.Die1, Pose.Dead }.Select(p => Art.Monsters[m][(int)p]))
            .ToArray();

    /// <summary>Every class's first-person weapons, resting and firing: procedural on the left, rendered on the right.</summary>
    static void WeaponSheet(string path)
    {
        bool was = Art.Rendered;
        var style = Art.Style;
        Tex[][] Grab() => Enumerable.Range(0, 9).Select(i => Art.Weapons[i].ToArray()).ToArray();
        Art.Rendered = false; Art.Init(ArtStyle.SciFi);
        var before = Grab();
        Art.Rendered = true; Art.Init(ArtStyle.SciFi);
        var after = Grab();
        Art.Rendered = was; Art.Init(style);

        const int s = 2, cw = 128 * s + 8, ch = 80 * s + 8, pad = 8;
        int w = pad * 2 + cw * 4 + pad, h = pad * 2 + ch * 9;
        var px = new uint[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = Col.Rgb(46, 50, 58);
        void Blit(Tex t, int ox, int oy)
        {
            for (int y = 0; y < t.H * s; y++)
                for (int x = 0; x < t.W * s; x++)
                {
                    uint c = t.Px[(y / s) * t.W + x / s];
                    bool checker = ((x / 8) + (y / 8)) % 2 == 0;
                    px[(oy + y) * w + ox + x] = Col.A(c) == 0 ? (checker ? Col.Rgb(60, 64, 72) : Col.Rgb(70, 74, 82)) : c;
                }
        }
        for (int i = 0; i < 9; i++)
        {
            int oy = pad + i * ch;
            Blit(before[i][0], pad, oy); Blit(before[i][1], pad + cw, oy);
            Blit(after[i][0], pad * 2 + cw * 2, oy); Blit(after[i][1], pad * 2 + cw * 3, oy);
        }
        Png.Save(path, px, w, h);
        Console.WriteLine($"wrote {path}");
    }

    /// <summary>Procedural art (top row of each pair) against the Blender-rendered pack (bottom row), for reviewing the pack.</summary>
    static void RenderedArtSheet(string path, Func<Tex[]> Pick, int cols)
    {
        bool was = Art.Rendered;
        var style = Art.Style;
        Art.Rendered = false; Art.Init(ArtStyle.SciFi);
        var before = Pick();
        Art.Rendered = true; Art.Init(ArtStyle.SciFi);
        var after = Pick();
        Art.Rendered = was; Art.Init(style);

        const int cell = 66 * 2, pad = 6;
        int rows = (before.Length + cols - 1) / cols;
        int w = cols * cell + pad * 2, h = rows * 2 * cell + pad * 2 + rows * pad;
        var px = new uint[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = Col.Rgb(46, 50, 58);
        void Blit(Tex t, int ox, int oy)
        {
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    uint c = t.Px[(y / 2) * t.W + x / 2];
                    bool checker = ((x / 8) + (y / 8)) % 2 == 0;
                    px[(oy + y) * w + ox + x] = Col.A(c) == 0 ? (checker ? Col.Rgb(60, 64, 72) : Col.Rgb(70, 74, 82)) : c;
                }
        }
        for (int i = 0; i < before.Length; i++)
        {
            int cx = pad + (i % cols) * cell, cy = pad + (i / cols) * (2 * cell + pad);
            Blit(before[i], cx, cy);
            Blit(after[i], cx, cy + cell);
        }
        Png.Save(path, px, w, h);
        Console.WriteLine($"wrote {path}");
    }

    public static int Screenshots(string dir)
    {
        Directory.CreateDirectory(dir);
        var g = new Game { FixedSeed = 1, AchievementsOn = false };
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
        Shot("02_style_select");
        Tick(new Input { Up = true });
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
        g.StartArena(PClass.Cleric);
        g.Vars.God = true; g.Vars.Freeze = false;
        var altar = g.Level.FindMark('!').Value;
        g.P.X = altar.x; g.P.Y = altar.y;
        Tick(default, 1);
        g.P.X = altar.x - 3; g.P.Angle = MathF.PI + 0.1f;
        Tick(default, 35 * 4);
        g.Vars.Freeze = true;
        g.P.X = altar.x + 2; g.P.Y = altar.y; g.P.Angle = MathF.PI;
        Tick(default, 2);
        Shot("14_arena_wave");
        g.Vars.Arcade = true;
        foreach (var m in g.Level.Things.OfType<Monster>().Where(m => m.Alive)
                     .OrderBy(m => MathF.Abs(Game.AngleDiff(MathF.Atan2(m.Y - g.P.Y, m.X - g.P.X), g.P.Angle))).Take(4))
            for (int k = 0; k < 3; k++)
                g.Arcade.Hit(m.X, m.Y, g.Level.FloorAt(m.X, m.Y) + m.SpriteH, 18 + k * 7, k % 2, k == 2, m.Def.Health, false, false);
        Shot("14b_arena_arcade");
        g.Vars.Arcade = false;
        g.Arcade.Reset();
        g.P.ArenaTier = 4;
        g.P.Weapon = Math.Max(0, Array.FindLastIndex(g.P.HasWeapon, w => w));
        Shot("14c_arena_arsenal");
        g.P.ArenaTier = 0;

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

        // the arena's own records: its leaderboard, and your best and next medal as a run starts
        {
            var arenaDay = new DateTime(2026, 9, 20);
            foreach (var (w, t, k, n, d, mods) in new[] { (16, 612.4f, 391, "ACE-1", 5, 0), (9, 455.0f, 262, "RAIL", 1, 7), (11, 431.7f, 240, "PLAYER", 7, 3), (12, 318.2f, 170, "NOVA", 3, 0), (6, 190.5f, 88, "RAIL", 0, 8), (4, 121.9f, 41, "PLAYER", 2, 0) })
                g.Profile.AddArenaRun(PClass.Cleric, new ArenaRun { Waves = w, Time = t, Kills = k, Name = n, When = arenaDay.AddDays(d), Mods = mods });
            g.Paused = true; g.Menu.Show(MenuPage.Pause); g.Menu.Show(MenuPage.Leaderboard);
            Shot("96_arena_leaderboard");
            g.Menu.Close(); g.Paused = false; g.Vars.Freeze = false; g.Vars.God = false;
            g.StartArena(PClass.Cleric);
            g.Vars.Freeze = true;
            Tick(default, 2);
            Shot("97_arena_start");
            g.Vars.Freeze = false;

            // a perk to pick after wave 5, with two taken already
            var pa = g.Level.Arena;
            pa.Perks[Perk.RapidFire] = 1; pa.Perks[Perk.ChainLightning] = 2;
            pa.Started = true; pa.Wave = 5; pa.BestWave = 5; pa.InIntermission = true;
            pa.Offer = new[] { Perk.Regeneration, Perk.ChainLightning, Perk.Might };
            g.Vars.Freeze = true;
            g.Messages.Clear();
            Tick(default, 2);
            Shot("98_perk_choice");
            pa.Offer = null;
            g.Vars.Freeze = false;
            g.GoToTitle();

            // the arena's setup page, with two modifiers on
            g.ArenaMods = ArenaMod.DoubleSpeed | ArenaMod.MeleeOnly;
            g.Menu.Show(MenuPage.ArenaSetup); g.Menu.Cursor = 1;
            Shot("99_arena_setup");
            // the daily challenge, on the setup page and on its board
            g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.ArenaSetup), "Daily challenge");
            Shot("109_arena_daily");
            g.ArenaMods = ArenaMod.None;
            g.GoToTitle();
            var today = Daily.Today;
            string k0 = today.ToString("yyyy-MM-dd");
            foreach (var (n, sc, w, t) in new[] { ("ACE-1", 2250, 9, 402.5f), ("NOVA", 1800, 8, 377.0f), (g.RunnerName, 1650, 7, 290.4f), ("RAIL", 900, 4, 150.1f) })
                g.Profile.DailyRuns.Add(new DailyRun { Date = k0, Name = n, Score = sc, Waves = w, Time = t });
            foreach (int d in new[] { 1, 2, 3 }) g.Profile.DailyRuns.Add(new DailyRun { Date = today.AddDays(-d).ToString("yyyy-MM-dd"), Name = g.RunnerName, Score = 800 + d * 150, Waves = 5 });
            g.Menu.Show(MenuPage.Leaderboard);
            g.Menu.BoardDaily = true; g.Menu.BoardArena = false;
            Shot("110_daily_board");
            g.Menu.Close();
            g.GoToTitle();

            // the endless course: a few platforms in, looking on down the gaps; then its board
            g.StartEndless(PClass.Fighter, 1234);
            var ep = g.Course.Platforms;
            foreach (int k in new[] { 1, 2, 3, 4, 5 })
            {
                g.P.X = (ep[k].x0 + ep[k].x1) / 2f; g.P.Y = 5.5f; g.P.FloorZ = ep[k].floor; g.P.Z = 0;
                Tick(default, 2);
            }
            g.P.X = ep[5].x1 - 0.5f; g.P.Y = 4.2f; g.P.Angle = 0.12f; g.P.Pitch = -6f;
            g.Messages.RemoveAll(m => !m.text.StartsWith("Platform 6"));
            Tick(default);
            Shot("115_endless");
            foreach (var (n, pl, seed, t) in new[] { (g.RunnerName, 27, 1234, 88.4f), ("ACE-1", 24, 90210, 71.2f), (g.RunnerName, 19, 555, 60.9f), ("NOVA", 12, 1234, 41.5f) })
                g.Profile.AddEndlessRun(new EndlessRun { Name = n, Class = "Fighter", Platforms = pl, Seed = seed, Time = t, When = new DateTime(2026, 9, 20 + pl % 7) });
            g.Menu.Show(MenuPage.Leaderboard);
            g.Menu.BoardEndless = true; g.Menu.BoardDaily = g.Menu.BoardArena = false; g.Menu.BoardClass = PClass.Fighter;
            Shot("116_endless_board");
            g.Menu.Close();
            g.Profile.EndlessRuns.Clear();
            g.GoToTitle();
        }

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

        // the crypt's block puzzle room
        g.FixedSeed = 1;
        g.NewGame(PClass.Cleric);
        g.Warp(2);
        g.Level.Things.RemoveAll(t => t is Monster);
        g.P.X = 21.6f; g.P.Y = 10.5f; g.P.Angle = -0.95f; g.P.Pitch = -8;
        Tick(default, 50);
        Shot("18_block_puzzle");

        // Dark Bishops: one solid, one mid-blur, and a homing missile on its way
        g.Level.Things.RemoveAll(t => t is Monster or Projectile);
        g.P.X = 13.5f; g.P.Y = 6.5f; g.P.Angle = -MathF.PI / 2; g.P.Pitch = 12;
        var bishopA = new Monster(Monster.Bishop) { X = 12.4f, Y = 3.6f, Level = g.Level, State = AiState.Chase };
        var bishopB = new Monster(Monster.Bishop) { X = 14.9f, Y = 3.2f, Level = g.Level, State = AiState.Attack, BlurTime = 5f };
        g.Level.Things.Add(bishopA); g.Level.Things.Add(bishopB);
        g.Level.Things.Add(new Projectile { Kind = ProjKind.Seeker, X = 13.2f, Y = 4.8f, Z = 0.3f, Level = g.Level });
        g.Vars.Freeze = true;
        Tick(default, 2);
        Shot("19_dark_bishop");
        g.Vars.Freeze = false;

        // menus: pause over the game, options, key bindings (mid-capture, scrolled)
        g.NewGame(PClass.Mage);
        Tick(default, 40);
        g.Paused = true; g.Menu.Show(MenuPage.Pause);
        Shot("20_pause_menu");
        g.Menu.Show(MenuPage.Options); g.Menu.Cursor = 1;
        Shot("21_options");
        g.Menu.Show(MenuPage.Bindings);
        g.Binds.Set(Act.Jump, 1, Keys.Mouse2);
        g.Menu.Cursor = 8; g.Menu.Column = 1; g.Menu.Capturing = true;
        g.Menu.Notice = "Use / push: E"; g.Menu.NoticeTime = 2;
        Shot("22_key_bindings");
        g.Binds.Reset();
        g.GoToTitle();
        Shot("23_title_menu");
        // with a campaign saved: Continue heads the menu, and says where you'll pick up
        {
            string savePath = Path.Combine(Path.GetTempPath(), $"hexensharp-shot-save-{Environment.ProcessId}.json");
            g.NewGame(PClass.Cleric);
            g.Warp(2);
            g.PlayTime = 2 * 3600 + 17 * 60 + 5;
            g.SavePath = savePath;
            g.SaveNow();
            g.GoToTitle();
            g.Menu.Cursor = 0;
            Shot("108_title_continue");
            g.DeleteSave();
            g.SavePath = null;
            g.Menu.Show(MenuPage.Main);
        }

        // relaxed mode: HUD, a lore stone, reading it, and a secret nook
        g.Style = GameStyle.Relaxed;
        g.FixedSeed = 7;
        g.NewGame(PClass.Fighter);
        {
            var lv = g.Level;
            var stone = lv.Things.OfType<LoreStone>().First(st => st.Y > 14); // the courtyard stone
            g.P.X = stone.X - 2.2f; g.P.Y = stone.Y + 1.4f;
            g.P.Angle = MathF.Atan2(stone.Y - g.P.Y, stone.X - g.P.X) - 0.35f; g.P.Pitch = 6;
            var e = lv.Things.OfType<Monster>().First(m => m.Def == Monster.Centaur && m.X > 20);
            e.X = stone.X - 1.4f; e.Y = stone.Y - 0.6f; e.State = AiState.Wander;
            g.P.Relics = 5; g.P.LoreRead = 3; g.P.Secrets = 1;
            g.Vars.Freeze = true;
            Tick(default, 60);
            Shot("24_relaxed_courtyard");
            g.ReadingLore = stone.Text;
            Shot("25_lore_stone");
            g.ReadingLore = null;

            int z = Array.IndexOf(lv.Cells, 'Z');
            lv.DoorOpen[z] = 1f;
            g.P.X = z % lv.W + 0.5f; g.P.Y = z / lv.W + 2.3f; g.P.Angle = -MathF.PI / 2 - 0.25f; g.P.Pitch = 0;
            Tick(default, 2);
            Shot("26_secret_nook");
            g.Vars.Freeze = false;
        }
        g.Style = GameStyle.Classic;

        // a custom map (as made in tools/editor/index.html) being play-tested
        {
            var doc = new MapDoc(32, 24) { Name = "My Grotto" };
            void Box(int x0, int y0, int x1, int y1, char c) { for (int x = x0; x <= x1; x++) { doc[x, y0] = c; doc[x, y1] = c; } for (int y = y0; y <= y1; y++) { doc[x0, y] = c; doc[x1, y] = c; } }
            Box(8, 3, 20, 12, 'M'); doc[8, 7] = 'D';
            for (int y = 4; y < 12; y++) for (int x = 9; x < 20; x++) doc[x, y] = ',';
            doc[12, 6] = 'e'; doc[17, 9] = 'd'; doc[18, 4] = '$'; doc[10, 10] = 'T'; doc[15, 5] = '&';
            doc[3, 7] = '@'; doc[4, 18] = 'E';
            g.StartTest(MapDoc.Parse(doc.Serialize()).ToDef(), PClass.Fighter);
            g.P.Angle = 0.12f;
            g.Vars.Freeze = true;
            Tick(default, 60);
            Shot("31_custom_map_playtest");
            g.Vars.Freeze = false;
            g.GoToTitle();
        }

        // raised roofs: looking up in the great hall, and the wall above the start room's door
        g.Style = GameStyle.Classic;
        g.FixedSeed = 1;
        g.NewGame(PClass.Cleric);
        g.Level.Things.RemoveAll(t => t is Monster);
        g.Vars.Freeze = true;
        g.P.X = 20.2f; g.P.Y = 9.6f; g.P.Angle = -MathF.PI * 0.8f; g.P.Pitch = 55;
        Tick(default, 50);
        Shot("32_great_hall_tall");
        g.P.X = 3.5f; g.P.Y = 3.5f; g.P.Angle = 0; g.P.Pitch = 30;
        Tick(default, 1);
        Shot("33_door_lintel");
        g.P.X = 20.5f; g.P.Y = 21.0f; g.P.Angle = -MathF.PI / 2 - 0.5f; g.P.Pitch = 30;
        Tick(default, 1);
        Shot("34_courtyard_walls");
        g.Vars.Freeze = false;
        g.GoToTitle();

        // stairs: up to the dais, the view from the top and the Keep terrace
        g.FixedSeed = 1;
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster);
        g.Vars.Freeze = true;
        void Place(float x, float y, float angle, float pitch)
        {
            g.P.X = x; g.P.Y = y; g.P.Angle = angle; g.P.Pitch = pitch;
            g.P.FloorZ = g.Level.FloorUnder(x, y, g.P.Radius); g.P.Z = 0; g.P.StepLag = 0;
        }
        Place(13.2f, 7.2f, -MathF.PI / 2 + 0.25f, -8);
        Tick(default, 50);
        Shot("36_dais_stairs");
        Place(15.5f, 1.6f, MathF.PI / 2 + 0.2f, -22);
        Tick(default, 1);
        Shot("37_from_the_dais");
        Place(6.8f, 18.5f, MathF.PI, 5);
        Tick(default, 1);
        Shot("38_heresiarch_platform");
        g.Warp(1);
        g.Level.Things.RemoveAll(t => t is Monster);
        Place(20.5f, 4.6f, -0.35f, 0);
        Tick(default, 50);
        Shot("39_keep_terrace");
        g.Vars.Freeze = false;
        g.GoToTitle();

        // the six sci-fi artifacts lined up on a pedestal row
        g.Style = GameStyle.Relaxed;
        g.FixedSeed = 1;
        g.NewGame(PClass.Mage);
        g.Style = GameStyle.Classic;
        g.Level.Things.RemoveAll(t => t is Monster || t is Pickup);
        for (int i = 0; i < 6; i++)
            g.Level.Things.Add(new Pickup(PickupKind.Relic, 0.42f, i) { X = 12.1f + i * 0.56f, Y = 6.5f, Level = g.Level, Name = "x" });
        g.P.X = 13.5f; g.P.Y = 8.9f; g.P.FloorZ = 0; g.P.Angle = -MathF.PI / 2; g.P.Pitch = -18;
        g.Vars.Freeze = true;
        Tick(default, 50);
        Shot("43_artifacts");
        g.Vars.Freeze = false;

        // the jetpack on its pickup spot, then flying high over the great hall
        g.FixedSeed = 1;
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster);
        g.P.X = 2.5f; g.P.Y = 3.5f; g.P.Angle = 0; g.P.Pitch = -12;
        Tick(default, 5);
        Shot("44_jetpack_pickup");
        g.Con.Execute("give jetpack");
        g.Messages.Clear();
        g.P.X = 9.5f; g.P.Y = 9.5f; g.P.Angle = -MathF.PI / 4; g.P.Pitch = -20; g.P.FloorZ = 0;
        Tick(new Input { JetHeld = true });
        Tick(new Input { JetHeld = true }, 30);
        Tick(default, 10);
        Shot("45_jetpack_flight");
        g.SetArtStyle(ArtStyle.Fantasy);
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster);
        g.P.X = 2.5f; g.P.Y = 3.5f; g.P.Angle = 0; g.P.Pitch = -12;
        Tick(default, 5);
        Shot("46_wings_fantasy");
        g.SetArtStyle(ArtStyle.SciFi);

        // the Windspire: looking up from the foot of the tower, mid-climb, and down from the beacon
        g.NewGame(PClass.Mage);
        int spire = Array.FindIndex(g.Hub, l => l.RawName == "Windspire");
        g.Warp(spire);
        g.Con.Execute("give jetpack");
        g.Messages.Clear();
        g.Vars.Freeze = true;
        void PlaceCam(float x, float y, float floor, float z, float angle, float pitch)
        {
            g.P.X = x; g.P.Y = y; g.P.FloorZ = floor; g.P.Z = z; g.P.VZ = 0; g.P.Flying = z > 0; g.P.Angle = angle; g.P.Pitch = pitch;
            g.P.StepLag = 0; g.P.TeleportFlash = 0; g.P.PickupFlash = 0;
        }
        PlaceCam(10.2f, 14.2f, 0, 0, -MathF.PI / 2 - 0.5f, 60);
        Tick(default, 3); PlaceCam(10.2f, 14.2f, 0, 0, -MathF.PI / 2 - 0.5f, 60);
        Shot("47_spire_foot");
        PlaceCam(16.5f, 6.5f, 0, 3.4f, MathF.PI + 0.35f, 12);
        Tick(new Input { JetHeld = false }, 1); PlaceCam(16.5f, 6.5f, 0, 3.4f, MathF.PI + 0.35f, 12);
        Shot("48_spire_climb");
        PlaceCam(9.3f, 10.75f, 8.5f, 0, -1.2f, -25);
        Tick(default, 1); PlaceCam(9.3f, 10.75f, 8.5f, 0, -1.2f, -25);
        Shot("49_spire_summit");
        PlaceCam(18.6f, 2.4f, 3.5f, 0, MathF.PI + 0.25f, -35);
        Tick(default, 1); PlaceCam(18.6f, 2.4f, 3.5f, 0, MathF.PI + 0.25f, -35);
        g.Messages.RemoveAll(m => !m.text.StartsWith("Checkpoint"));
        Shot("51_spire_checkpoint");
        g.Messages.Clear();

        // the Hanging Cisterns: the ledges from the cistern floor, then the middle ledge's blocks and plates from above
        {
            int ci = Array.FindIndex(g.Hub, l => l.RawName == "Hanging Cisterns");
            g.Warp(ci);
            g.Level.Things.RemoveAll(t => t is Monster);
            g.Messages.Clear();
            PlaceCam(9.5f, 10.5f, 0, 0, 0.55f, 18);
            Tick(default, 3); PlaceCam(9.5f, 10.5f, 0, 0, 0.55f, 18);
            g.Messages.Clear();
            Shot("102_cisterns_floor");
            PlaceCam(18.6f, 12.4f, 0, 3.9f, 0.62f, -30);
            Tick(default, 1); PlaceCam(18.6f, 12.4f, 0, 3.9f, 0.62f, -30);
            g.Messages.Clear();
            Shot("103_cisterns_ledge");
            g.P.Flying = false;
        }

        // the mini-bosses, each awake and facing you in its own map, with its health bar
        g.Vars.BossIntros = false; // these show the bosses themselves, without their intro cards
        foreach (var (name, map, dx, dy, pitch) in new[]
        {
            ("104_quarry_warden", "Deepdelve Quarry", -2.6f, 0f, 0f),
            ("105_dust_stalker", "Barren World", 0f, -3.2f, 0f),
            ("106_thornmother", "Verdant Moon", -3.2f, 0.6f, 6f),
            ("107_drowned_keeper", "Hanging Cisterns", -3.0f, 0.4f, 6f),
        })
        {
            g.Warp(Array.FindIndex(g.Hub, l => l.RawName == map));
            var boss = g.Level.Things.OfType<Monster>().FirstOrDefault(t => t.Def.MiniBoss != null);
            if (boss == null)
            {
                // an earlier shot cleared this map's monsters: put its mini-boss back
                MiniBosses.Place(g.Level);
                boss = g.Level.Things.OfType<Monster>().First(t => t.Def.MiniBoss != null);
            }
            g.Level.Things.RemoveAll(t => t is Monster { Def.MiniBoss: null });
            // the quarry's warden is shown having burrowed out into its gallery
            if (map == "Deepdelve Quarry") { boss.X = 14.5f; boss.Y = 3.5f; }
            float cx = boss.X + dx, cy = boss.Y + dy;
            g.Vars.Freeze = true;
            PlaceCam(cx, cy, g.Level.FloorAt(cx, cy), 0, MathF.Atan2(boss.Y - cy, boss.X - cx), pitch);
            boss.State = AiState.Chase; boss.Health = (int)(boss.Def.Health * 0.7f);
            Tick(default, 2);
            PlaceCam(cx, cy, g.Level.FloorAt(cx, cy), 0, MathF.Atan2(boss.Y - cy, boss.X - cx), pitch);
            g.Messages.Clear();
            Shot(name);
            g.Vars.Freeze = false;
        }
        {
            // the Rock Wyrm, just burst out of the rock beside you in the Bedrock Depths
            g.Warp(Array.FindIndex(g.Hub, l => l.RawName == "Bedrock Depths"));
            var d = g.Level;
            var wyrm = d.Things.OfType<Monster>().FirstOrDefault(t => t.Def == MiniBosses.Wyrm);
            if (wyrm == null) { MiniBosses.Place(d); wyrm = d.Things.OfType<Monster>().First(t => t.Def == MiniBosses.Wyrm); }
            var (ax, ay) = d.ArrivalCell();
            float fl = g.P.FloorZ;
            foreach (var (bx, by) in new[] { (ax + 1, ay), (ax + 2, ay), (ax + 3, ay), (ax + 3, ay - 1), (ax + 3, ay + 1), (ax + 4, ay) })
                d.DamageBlock(bx, by, 100000, Level.Face.Wall, fl);
            wyrm.X = ax + 3.5f; wyrm.Y = ay + 0.5f; wyrm.Burrowed = false; wyrm.Solid = true; wyrm.State = AiState.Chase; wyrm.Health = 300;
            g.Vars.Freeze = true;
            PlaceCam(ax + 0.5f, ay + 0.5f, fl, 0, 0, 0);
            Tick(default, 2);
            PlaceCam(ax + 0.5f, ay + 0.5f, fl, 0, 0, 0);
            g.Messages.Clear();
            Shot("111_rock_wyrm");
            g.Vars.Freeze = false;

            // the Storm Leviathan, weaving ahead of your ship near the end of the Void Crossing
            g.Warp(Array.FindIndex(g.Hub, l => l.Flight));
            var lane = g.Level;
            lane.Things.RemoveAll(t => t is Monster { Def.MiniBoss: null });
            var ship = lane.Things.OfType<Monster>().FirstOrDefault(t => t.Def == MiniBosses.Dreadnought);
            if (ship == null) { MiniBosses.Place(lane); ship = lane.Things.OfType<Monster>().First(t => t.Def == MiniBosses.Dreadnought); }
            g.Vars.Freeze = true;
            for (int k = 0; k < 3; k++)
            {
                g.P.X = 80f; g.P.Y = 6.5f; g.P.Z = 1.1f; g.P.Angle = 0.05f; g.P.Pitch = 4;
                ship.X = 86.5f; ship.Y = 7.2f; ship.Z = 1.0f; ship.State = AiState.Chase; ship.Health = 420;
                Tick(default, 1);
            }
            g.Messages.Clear();
            Shot("112_storm_leviathan");
            g.Vars.Freeze = false;
        }
        g.Vars.BossIntros = true;
        {
            // the Thornmother's intro card, the moment she notices you
            g.Warp(Array.FindIndex(g.Hub, l => l.RawName == "Verdant Moon"));
            var tm = g.Level.Things.OfType<Monster>().FirstOrDefault(t => t.Def == MiniBosses.Thornmother);
            if (tm == null) { MiniBosses.Place(g.Level); tm = g.Level.Things.OfType<Monster>().First(t => t.Def == MiniBosses.Thornmother); }
            g.Level.Things.RemoveAll(t => t is Monster { Def.MiniBoss: null });
            tm.Introduced = false; tm.State = AiState.Idle; tm.Health = tm.MaxHealth;
            float cx = tm.X - 3.6f, cy = tm.Y + 0.6f;
            g.Vars.Freeze = true;
            PlaceCam(cx, cy, g.Level.FloorAt(cx, cy), 0, MathF.Atan2(tm.Y - cy, tm.X - cx), 6f);
            tm.State = AiState.Chase;
            Tick(default, 16);
            PlaceCam(cx, cy, g.Level.FloorAt(cx, cy), 0, MathF.Atan2(tm.Y - cy, tm.X - cx), 6f);
            g.Messages.Clear();
            Shot("118_boss_intro");
            g.Intro = null;

            // damage numbers on their own (Options > Effects), down the Hab Ring's great hall
            g.Warp(0);
            g.Level.Things.RemoveAll(t => t is Monster);
            PlaceCam(10.5f, 5.5f, g.Level.FloorAt(10.5f, 5.5f), 0, 0, 0);
            g.Vars.DamageNumbers = true;
            g.Arcade.Floaters.Clear();
            foreach (var (dx, dy, hits) in new[] { (4f, -0.8f, new[] { 14, 52 }), (5.5f, 0.9f, new[] { 9, 11, 23 }) })
            {
                var m = new Monster(Monster.Slaughtaur) { X = 10.5f + dx, Y = 5.5f + dy, Level = g.Level, State = AiState.Chase };
                g.Level.Things.Add(m);
                foreach (int hit in hits) g.DamageMonster(m, hit, 0);
            }
            g.HitStop = 0; g.Shake = 0;
            Tick(default, 4);
            g.Messages.Clear();
            Shot("119_damage_numbers");
            g.Vars.DamageNumbers = false;
            g.Vars.Freeze = false;
            g.Level.Things.RemoveAll(t => t is Monster);
            g.Arcade.Floaters.Clear();

            // weapon mods: the four lying in the hall, one fitted (its tag by the level bar) and a charge building
            g.Warp(0);
            g.Level.Things.RemoveAll(t => t is Monster or Pickup);
            PlaceCam(10.5f, 5.5f, g.Level.FloorAt(10.5f, 5.5f), 0, 0, 0);
            foreach (var (m, k) in WeaponMods.All.Select((m, k) => (m, k)))
            {
                var pk = Game.MakeMod(m, 13.2f + k * 0.2f, 4.1f + k * 0.95f);
                pk.Level = g.Level;
                g.Level.Things.Add(pk);
            }
            var had = g.P.Mods[g.P.Weapon];
            g.P.Mods[g.P.Weapon] = WeaponMod.Charged; g.P.Charging = true; g.P.Charge = 0.65f;
            g.Messages.Clear();
            Shot("121_weapon_mods");
            g.P.Mods[g.P.Weapon] = had; g.P.Charging = false; g.P.Charge = 0;
            g.Level.Things.RemoveAll(t => t is Pickup { Kind: PickupKind.Mod });

            // Options > Effects
            g.Paused = true;
            g.Menu.Show(MenuPage.Options);
            g.Menu.Show(MenuPage.Effects);
            g.Menu.Cursor = 0;
            Shot("120_effects_menu");
            g.Menu.Close(); g.Paused = false;
        }

        // the Blender-rendered art pack (Options > Rendered art): a review sheet, then the Hab Ring with it on
        RenderedArtSheet(Path.Combine(dir, "52_rendered_sheet.png"), SheetItems, 8);
        RenderedArtSheet(Path.Combine(dir, "55_rendered_monsters.png"), SheetMonsters, 6);
        WeaponSheet(Path.Combine(dir, "75_rendered_weapons.png"));
        g.SetRenderedArt(true);
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster);
        g.Level.Things.Add(new Pickup(PickupKind.Jetpack, 0.5f) { X = 4.5f, Y = 3.5f, Level = g.Level });
        g.Level.Things.Add(new Pickup(PickupKind.Flask, 0.4f) { X = 5.3f, Y = 2.3f, Level = g.Level });
        g.Level.Things.Add(new Pickup(PickupKind.BlueMana, 0.4f) { X = 5.5f, Y = 4.4f, Level = g.Level });
        g.Level.Things.Add(new Monster(Monster.Afrit) { X = 6.2f, Y = 3.2f, Level = g.Level });
        g.Vars.Freeze = true;
        PlaceCam(1.6f, 3.2f, 0, 0, 0.05f, -8);
        Tick(default, 1); PlaceCam(1.6f, 3.2f, 0, 0, 0.05f, -8);
        g.Messages.Clear();
        Shot("53_rendered_hab_ring");
        g.SetRenderedArt(false);
        Shot("54_procedural_hab_ring");

        // every monster lined up in the great hall, rendered then procedural
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster or Decor or Pickup or Chest or LoreStone);
        var lineup = new[] { Monster.Afrit, Monster.Ettin, Monster.Centaur, Monster.Slaughtaur, Monster.Bishop, Monster.Heresiarch };
        for (int i = 0; i < lineup.Length; i++)
            g.Level.Things.Add(new Monster(lineup[i]) { X = 11.3f + i * 1.25f, Y = 5.2f + (i % 2) * 0.9f, Level = g.Level });
        PlaceCam(14.5f, 10.4f, 0, 0, -MathF.PI / 2, 4);
        g.SetRenderedArt(true);
        Tick(default, 1); PlaceCam(14.5f, 10.4f, 0, 0, -MathF.PI / 2, 4);
        g.Messages.Clear();
        Shot("56_rendered_monsters_ingame");
        g.SetRenderedArt(false);
        Shot("57_procedural_monsters_ingame");

        // the Psion firing the arc rifle, rendered then procedural
        g.NewGame(PClass.Mage);
        g.Level.Things.RemoveAll(t => t is Monster);
        g.P.HasWeapon[2] = true; g.P.Weapon = 2; g.P.Raise = 0;
        PlaceCam(1.6f, 3.2f, 0, 0, 0.05f, -8);
        g.SetRenderedArt(true);
        Tick(default, 1); PlaceCam(1.6f, 3.2f, 0, 0, 0.05f, -8);
        g.P.FireAnim = 0.2f; g.Messages.Clear();
        Shot("76_rendered_weapon_ingame");
        g.SetRenderedArt(false);
        Shot("77_procedural_weapon_ingame");

        // the HUD styles, on the same view with a key, some items and a jetpack to show
        g.P.SteelKey = true; g.P.Flasks = 2; g.P.HasJetpack = true; g.P.FireAnim = 0;
        foreach (var (style, name) in new[] { (HudStyle.Compact, "78_hud_compact"), (HudStyle.Minimal, "79_hud_minimal"), (HudStyle.Off, "80_hud_off") })
        {
            g.Vars.Hud = style;
            Shot(name);
        }
        g.Vars.Hud = HudStyle.Full;
        g.Vars.Crosshair = CrosshairStyle.Cross;
        Shot("81_crosshair");
        g.Vars.Crosshair = CrosshairStyle.Off;
        // strafe jumping: airborne at 180% of a run, with the speed readout
        g.P.Z = 0.3f; g.P.VX = MathF.Cos(g.P.Angle) * g.RunSpeed * 1.8f; g.P.VY = MathF.Sin(g.P.Angle) * g.RunSpeed * 1.8f;
        Shot("82_strafe_speed");
        g.P.Z = 0; g.P.VX = g.P.VY = 0;

        // the strafe-jumping practice course: the view down the hangar from the start, then mid-hop over the 5-wide gap
        g.StartPractice(PClass.Fighter);
        g.Vars.Freeze = true;
        Tick(default, 2);
        PlaceCam(3.5f, 5.5f, 2.5f, 0, 0, -6);
        Tick(default, 1); PlaceCam(3.5f, 5.5f, 2.5f, 0, 0, -6);
        Shot("83_velocity_hangar");
        g.Messages.Clear();
        g.RunStarted = true; g.RunTime = 14.62f;
        PlaceCam(Maps.CoursePlatforms[2].x1 + 2.2f, 4.8f, 0, 2.9f, 0.12f, -4);
        g.P.Flying = false; g.P.VX = g.RunSpeed * 2.1f;
        Shot("84_velocity_gap");
        g.P.VX = 0; g.Vars.Freeze = false;

        // the leaderboard, opened from the pause menu on the course
        var boardDay = new DateTime(2026, 9, 20);
        foreach (var (t, n, d) in new[] { (21.84f, "RAIL", 1), (23.10f, "ACE-1", 3), (24.57f, "RAIL", 0), (26.02f, "NOVA", 5), (27.93f, "ACE-1", 2), (31.40f, "PLAYER", 6) })
            g.Profile.AddCourseRun("Fighter", t, n, boardDay.AddDays(d));
        g.Profile.AddCourseRun("Fighter", 22.75f, "ACE-1", boardDay.AddDays(7).AddHours(1));
        g.Paused = true; g.Menu.Show(MenuPage.Pause); g.Menu.Show(MenuPage.Leaderboard);
        Shot("85_leaderboard");
        g.Menu.Close(); g.Paused = false;

        // the other practice courses: the course list, Descent from its top platform, the Circuit's first corner, Free Roam
        g.Menu.Close(); g.Paused = false;
        g.GoToTitle();
        g.Menu.Show(MenuPage.Courses); g.Menu.Cursor = 1;
        Shot("90_practice_courses");
        g.Menu.Cursor = Array.IndexOf(Courses.All, Endless.Pick);
        Shot("117_practice_endless");
        g.Menu.Close();
        foreach (var (course, name, cam) in new[]
        {
            (Courses.Descent, "91_descent", (x: 14.2f, y: 4.6f, a: 0.12f, pitch: -38f)),
            (Courses.Circuit, "92_circuit", (x: 14.5f, y: 24.5f, a: MathF.PI + 0.55f, pitch: -4f)),
            (Courses.FreeRoam, "93_free_roam", (x: 32.5f, y: 32.5f, a: 0.6f, pitch: -4f)),
        })
        {
            g.StartPractice(PClass.Fighter, course);
            g.Vars.Freeze = true;
            Tick(default, 2);
            PlaceCam(cam.x, cam.y, g.Level.FloorAt(cam.x, cam.y), 0, cam.a, cam.pitch);
            Tick(default, 1); PlaceCam(cam.x, cam.y, g.Level.FloorAt(cam.x, cam.y), 0, cam.a, cam.pitch);
            g.Messages.Clear();
            Shot(name);
            g.Vars.Freeze = false;
        }

        // the ghost of your best run, a stride ahead across the first platform, then the practice pause menu
        var ghostLine = new GhostTrack();
        for (float t = 0; t <= 8; t += 0.05f) ghostLine.Record(t, 2.5f + 5.2f * t, 4.6f + 1.4f * MathF.Sin(t * 2.2f), 2.5f + MathF.Max(0, MathF.Sin(t * 5.8f)) * 0.4f);
        g.Profile.Ghosts["Fighter"] = new CourseGhost { Time = 21.84f, Path = ghostLine.Encode() };
        g.StartPractice(PClass.Fighter);
        g.Vars.Freeze = true;
        Tick(default, 2);
        g.RunStarted = true; g.RunTime = 1.35f; g.Ghost.Seek(g.RunTime);
        PlaceCam(5.2f, 5.5f, 2.5f, 0, 0.02f, -8);
        g.Messages.Clear();
        Shot("86_practice_ghost");

        // the strafe helper mid-hop at 170% of a run, holding D: a little behind the turn, then in the zone
        g.Ghost.Seek(0.2f);
        PlaceCam(8.5f, 5.5f, 2.5f, 0.35f, 0, -4);
        g.P.Flying = false; g.P.VZ = 0.8f;
        float hv = g.RunSpeed * 1.7f, head = 0.35f;
        g.P.VX = MathF.Cos(head) * hv; g.P.VY = MathF.Sin(head) * hv;
        g.InMove = 0; g.InStrafe = 1;
        g.P.Angle = head + g.StrafeZone(MathF.PI / 2).best - 9 * MathF.PI / 180;
        Shot("88_strafe_helper_turn");
        g.P.Angle = head + g.StrafeZone(MathF.PI / 2).best;
        Shot("89_strafe_helper_zone");
        g.P.VX = g.P.VY = 0; g.P.Z = 0;
        g.Paused = true; g.Menu.Show(MenuPage.Pause);
        Shot("87_practice_pause");
        g.Menu.Close(); g.Paused = false; g.Vars.Freeze = false;

        // the demo: strafing across the first gap, then stopped at a step, at half speed
        g.Profile.Ghosts.Remove("Fighter");
        g.StartPractice(PClass.Fighter);
        g.StartDemo();
        for (int f = 0; f < 35 * 6 && g.Demo && g.Pilot?.Step != "STRAFE"; f++) Tick(default);
        Tick(default, 8);
        g.Messages.Clear();
        Shot("94_demo");
        g.StartPractice(PClass.Fighter);
        g.StartDemo();
        Tick(new Input { Slot = 2 });
        Tick(new Input { Use = true });
        for (int f = 0; f < 35 * 20 && g.Demo && !(g.DemoPaused && g.Pilot?.Step == "SWITCH"); f++)
            Tick(g.DemoPaused ? new Input { Confirm = true } : default);
        g.Messages.Clear();
        Shot("95_demo_step");
        g.EndDemo();
        g.GoToTitle();
        g.Vars.Freeze = false;

        // character progression: the HUD's level bar with an XP pop-up, and the character screen
        {
            var saved = g.Profile;
            g.Profile = new Profile();
            g.Profile.AddXp(1900);
            foreach (var sk in new[] { Skill.Vitality, Skill.Vitality, Skill.Power, Skill.Agility, Skill.Thrusters }) g.Profile.Spend(sk);
            g.Profile.AddWeaponXp(PClass.Fighter, 0, 400); g.Profile.AddWeaponXp(PClass.Fighter, 1, 150);
            g.NewGame(PClass.Fighter);
            g.Level.Things.RemoveAll(t => t is Monster);
            g.GainXp(35);
            g.Messages.Clear();
            PlaceCam(12.5f, 8.5f, 0, 0, -MathF.PI / 2, 0);
            Tick(default, 1);
            Shot("58_level_bar");
            g.Update(new Input { Character = true }, 1f / 35f);
            g.Menu.Cursor = 1;
            Shot("59_character_screen");
            // the achievements: a few earned, the highlighted one showing its progress
            var achDay = new DateTime(2026, 9, 21);
            foreach (var (id, d) in new[] { ("first_blood", 0), ("treasure", 2), ("podium", 3), ("speed", 3), ("arena_5", 5), ("veteran", 6), ("secrets", 7) })
                g.Profile.Achievements[id] = achDay.AddDays(d);
            g.Profile.TotalKills = 212;
            g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Character), "Achievements");
            g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
            g.Menu.Cursor = 1;
            Shot("100_achievements");
            g.Menu.Close(); g.Paused = false;
            // the banner as one unlocks
            g.Messages.Clear();
            g.AchievementUnlocked(Achievements.Find("lore"));
            g.Messages.Clear();
            Shot("101_achievement_banner");
            g.AchievementTime = 0;
            g.Profile = saved;
        }
        g.SetArtStyle(ArtStyle.Fantasy);
        PlaceCam(10.2f, 14.2f, 0, 0, -MathF.PI / 2 - 0.5f, 60);
        Shot("50_windspire_fantasy");
        g.SetArtStyle(ArtStyle.SciFi);
        g.Vars.Freeze = false;

        // Deepdelve Quarry: a rubble plug cracking under your fists, then the tunnel you dug into the gallery
        g.NewGame(PClass.Fighter);
        int quarry = Array.FindIndex(g.Hub, l => l.RawName == "Deepdelve Quarry");
        g.Warp(quarry);
        var ql = g.Level;
        ql.Things.RemoveAll(t => t is Monster);
        g.Messages.Clear();
        g.HitBlock(6, 2, 25); g.HitBlock(6, 3, 45);
        g.Vars.Freeze = true;
        PlaceCam(3.2f, 2.9f, 0, 0, 0.1f, 0);
        Tick(default, 1); PlaceCam(3.2f, 2.9f, 0, 0, 0.1f, 0);
        Shot("58_quarry_rubble");
        for (int x = 6; x <= 8; x++) g.HitBlock(x, 2, 999);
        g.HitBlock(6, 3, 999);
        g.Vars.Freeze = false;
        Tick(default, 35);
        g.Vars.Freeze = true;
        PlaceCam(4.6f, 2.5f, 0, 0, 0.0f, 0);
        Tick(default, 1); PlaceCam(4.6f, 2.5f, 0, 0, 0.0f, 0);
        g.Messages.Clear();
        Shot("59_quarry_tunnel");
        g.SetRenderedArt(true);
        Shot("61_quarry_rendered");
        g.SetRenderedArt(false);
        g.SetArtStyle(ArtStyle.Fantasy);
        Shot("60_quarry_fantasy");
        g.SetArtStyle(ArtStyle.SciFi);
        g.Vars.Freeze = false;

        // Bedrock Depths: boxed in by rock on arrival, then a dug-out tunnel with a pit, a shaft and a cracked roof
        int depths = Array.FindIndex(g.Hub, l => l.RawName == "Bedrock Depths");
        g.Warp(depths);
        var dl = g.Level;
        var (dax, day) = dl.ArrivalCell();
        g.Messages.Clear();
        g.Vars.Freeze = true;
        PlaceCam(dax + 0.5f, day + 0.5f, dl.Floors[day * dl.W + dax], 0, 0, 0);
        Tick(default, 1); PlaceCam(dax + 0.5f, day + 0.5f, dl.Floors[day * dl.W + dax], 0, 0, 0);
        g.HitBlock(dax + 1, day, 30);
        Shot("62_depths_arrival");
        for (int x = dax + 1; x <= dax + 5; x++) g.HitBlock(x, day, 999);
        g.HitBlock(dax + 2, day - 1, 999);
        for (int k = 0; k < 3; k++) g.HitBlock(dax + 3, day, 999, Level.Face.Floor);
        g.HitBlock(dax + 4, day, 999, Level.Face.Floor);
        for (int k = 0; k < 4; k++) g.HitBlock(dax + 2, day, 999, Level.Face.Ceiling);
        g.HitBlock(dax + 5, day, 40, Level.Face.Floor);
        g.HitBlock(dax + 1, day, 45, Level.Face.Ceiling);
        g.Vars.Freeze = false;
        Tick(default, 35);
        g.Vars.Freeze = true;
        PlaceCam(dax + 0.5f, day + 0.5f, dl.Floors[day * dl.W + dax], 0, 0.05f, -20);
        Tick(default, 1); PlaceCam(dax + 0.5f, day + 0.5f, dl.Floors[day * dl.W + dax], 0, 0.05f, -20);
        g.Messages.Clear();
        Shot("63_depths_dug");
        PlaceCam(dax + 1.8f, day + 0.5f, dl.Floors[day * dl.W + dax + 1], 0, 0.3f, -55);
        Tick(default, 1); PlaceCam(dax + 1.8f, day + 0.5f, dl.Floors[day * dl.W + dax + 1], 0, 0.3f, -55);
        Shot("65_depths_pit");
        PlaceCam(dax + 1.5f, day + 0.5f, dl.Floors[day * dl.W + dax + 1], 0, MathF.PI / 2, 20);
        Tick(default, 1); PlaceCam(dax + 1.5f, day + 0.5f, dl.Floors[day * dl.W + dax + 1], 0, MathF.PI / 2, 20);
        Shot("66_depths_step_up");
        g.SetArtStyle(ArtStyle.Fantasy);
        Shot("64_depths_fantasy");
        g.SetArtStyle(ArtStyle.SciFi);
        g.Vars.Freeze = false;

        // Barren World: the crash site, mining an ore vein, and the repaired ship
        int barren = Array.FindIndex(g.Hub, l => l.RawName == "Barren World");
        g.Warp(barren);
        var bw = g.Level;
        bw.Things.RemoveAll(t => t is Monster);
        g.Vars.Freeze = true;
        void BarrenShot(string name, float x, float y, float angle, float pitch)
        {
            PlaceCam(x, y, 0, 0, angle, pitch);
            Tick(default, 1); PlaceCam(x, y, 0, 0, angle, pitch);
            g.Messages.Clear();
            Shot(name);
        }
        BarrenShot("67_barren_crash", 10.5f, 13.5f, -0.64f, 0);
        g.HitBlock(6, 11, 999);
        g.HitBlock(5, 11, 35);
        g.P.Ore[0] = 2; g.P.Ore[1] = 1; bw.Ship.Delivered[0] = 3;
        BarrenShot("68_barren_mining", 7.2f, 11.5f, MathF.PI, 0);
        for (int k = 0; k < Ship.Need.Length; k++) bw.Ship.Delivered[k] = Ship.Need[k];
        BarrenShot("69_barren_repaired", 10.5f, 13.5f, -0.64f, 0);
        for (int k = 0; k < Ship.Need.Length; k++) bw.Ship.Delivered[k] = 0;
        g.SetArtStyle(ArtStyle.Fantasy);
        BarrenShot("70_barren_fantasy", 10.5f, 13.5f, -0.64f, 0);
        g.SetArtStyle(ArtStyle.SciFi);
        g.Vars.Freeze = false;

        // Void Crossing: in the cockpit at the start, then deep in the rocks, then landing on the Verdant Moon
        int crossing = Array.FindIndex(g.Hub, l => l.Flight);
        g.Warp(crossing);
        var cl = g.Level;
        g.Vars.Freeze = true;
        void FlyShot(string name, float x, float y, float z, float angle, float pitch)
        {
            g.P.X = x; g.P.Y = y; g.P.Z = z; g.P.Angle = angle; g.P.Pitch = pitch; g.P.TeleportFlash = 0; g.P.DamageFlash = 0;
            Tick(default, 1);
            g.P.X = x; g.P.Y = y; g.P.Z = z; g.P.Angle = angle; g.P.Pitch = pitch;
            g.Messages.Clear();
            Shot(name);
        }
        FlyShot("71_crossing_start", cl.StartX, cl.StartY, 1.3f, 0, 0);
        FlyShot("72_crossing_rocks", 70.5f, 6.5f, 1.6f, 0.1f, -6);
        g.SetArtStyle(ArtStyle.Fantasy);
        FlyShot("73_crossing_fantasy", 70.5f, 6.5f, 1.6f, 0.1f, -6);
        g.SetArtStyle(ArtStyle.SciFi);
        g.Vars.Freeze = false;
        int moon = Array.FindIndex(g.Hub, l => l.RawName == "Verdant Moon");
        g.Warp(moon);
        g.Level.Things.RemoveAll(t => t is Monster);
        g.Vars.Freeze = true;
        PlaceCam(4.5f, 8.5f, 0, 0, -0.5f, 0);
        Tick(default, 1); PlaceCam(4.5f, 8.5f, 0, 0, -0.5f, 0);
        g.Messages.Clear();
        Shot("74_verdant_moon");
        g.Vars.Freeze = false;

        // Story mode: the case brief, the docks at night, questioning the guard, and the journal
        g.StartStory(0);
        Shot("75_story_brief");
        g.ReadingLore = null;
        PlaceCam(12.5f, 9.5f, 0, 0, -0.35f, 0);
        Tick(default, 30); PlaceCam(12.5f, 9.5f, 0, 0, -0.35f, 0);
        g.Messages.Clear();
        Shot("76_story_dockside");
        var sl = g.Level;
        foreach (var id in new[] { "gate", "boots" }) Story.Examine(g, sl.Things.OfType<ClueMark>().First(m => m.C.Id == id));
        g.ReadingLore = null;
        var guard = sl.Things.OfType<Npc>().First(n => n.S.Culprit);
        PlaceCam(guard.X - 0.1f, guard.Y + 1.3f, 0, 0, -MathF.PI / 2, 0);
        Tick(default, 1); PlaceCam(guard.X - 0.1f, guard.Y + 1.3f, 0, 0, -MathF.PI / 2, 0);
        Story.Talk(g, guard);
        Story.Choose(g, "clue:gate");
        g.Story.Cursor = 2;
        g.Messages.Clear();
        Shot("77_story_questioning");
        Story.Choose(g, "bye");
        g.Story.JournalOpen = true;
        Shot("78_story_journal");
        g.GoToTitle();

        // the original fantasy look, kept as an option
        g.SetArtStyle(ArtStyle.Fantasy);
        g.FixedSeed = 1;
        g.NewGame(PClass.Fighter);
        Tick(default, 5);
        Shot("41_fantasy_style");
        g.GoToTitle();
        Shot("42_fantasy_title");
        g.SetArtStyle(ArtStyle.SciFi);

        // victory screen
        g.Mode = GameMode.Victory;
        Shot("12_victory");

        // New Game+: the victory screen offering the next tier, and the class choice for it
        g.Profile.NgUnlocked = 2; g.NgTier = 1;
        Shot("113_ng_plus_victory");
        g.Mode = GameMode.ClassSelect; g.NgTier = 2; g.MenuIndex = 0;
        Shot("114_ng_plus_class");
        return 0;
    }
}
