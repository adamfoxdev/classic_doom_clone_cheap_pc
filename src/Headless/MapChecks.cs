namespace HexenSharp;

/// <summary>Checks on maps: the hub, heights and stairs, map files, custom maps and the art styles.</summary>
public static partial class Headless
{
    static void MapFileChecks(Action<bool, string> check)
    {
        // the HTML editor's built-in map menu must match the maps in src/Level.cs
        var js = MapFiles.EditorMapsPath;
        if (js != null && File.Exists(js))
            check(File.ReadAllText(js).Replace("\r", "") == MapFiles.BuiltinMapsJs(),
                  "tools/editor/builtin-maps.js is up to date (else run: dotnet run -- --export-editor-maps)");
        else Console.WriteLine("  skip builtin-maps.js check (not running from the repository)");

        var dir = Path.Combine(Path.GetTempPath(), $"hexen_maps_{Environment.ProcessId}");
        Directory.CreateDirectory(dir);
        try
        {
            var outWriter = Console.Out;
            Console.SetOut(TextWriter.Null);
            MapFiles.Export(dir);
            Console.SetOut(outWriter);
            var files = Directory.GetFiles(dir, "*.hxm");
            check(files.Length == Maps.Builtin.Count() && files.All(f => MapDoc.Parse(File.ReadAllText(f)).Rows().Length > 0),
                  $"--export-maps writes every built-in map ({files.Length})");

            // a map to play-test: start, exit, a wall between them with a door
            string path = Path.Combine(dir, "test.hxm");
            var doc = new MapDoc(12, 8) { Name = "Reload Test" };
            doc[2, 2] = '@'; doc[9, 5] = 'E';
            File.WriteAllText(path, doc.Serialize());
            check(MapFiles.TryLoad(path, out _) != null && MapFiles.TryLoad(Path.Combine(dir, "missing.hxm"), out var err) == null && err != null,
                  "map files load, and a missing one reports why");
            File.WriteAllText(Path.Combine(dir, "nostart.hxm"), new MapDoc(6, 6).Serialize());
            check(MapFiles.TryLoad(Path.Combine(dir, "nostart.hxm"), out var err2) == null && err2.Contains("player start"), "a map without a start is refused");

            var g = new Game { FixedSeed = 1 };
            var loaded = MapFiles.TryLoad(path, out _);
            g.StartTest(loaded.ToDef(), PClass.Mage);
            var watcher = new MapWatcher(path);
            g.P.X = 6.5f; g.P.Y = 3.5f; g.P.Angle = 1.2f;
            check(!watcher.Poll(g, 1f), "nothing reloads until the file changes");
            doc[6, 5] = 'e';
            File.WriteAllText(path, doc.Serialize());
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
            check(watcher.Poll(g, 1f) && g.TestingMap && g.Level.Things.Any(t => t is Monster) && g.P.Class == PClass.Mage,
                  "saving the file reloads the play-test with the new map");
            check(MathF.Abs(g.P.X - 6.5f) < 0.01f && MathF.Abs(g.P.Y - 3.5f) < 0.01f && MathF.Abs(g.P.Angle - 1.2f) < 0.01f,
                  "and keeps you where you were");
            File.WriteAllText(path, "garbage");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(10));
            check(!watcher.Poll(g, 1f) && g.Messages.Last().text.StartsWith("Can't reload"), "a broken save is reported, and the game keeps running");

            // --check-map prints the same checks as the editors
            var sw = new StringWriter();
            Console.SetOut(sw);
            File.WriteAllText(path, doc.Serialize());
            int code = MapFiles.Check(path);
            Console.SetOut(outWriter);
            check(code == 0 && sw.ToString().Trim() == "ok", $"--check-map passes a good map ({sw.ToString().Trim()})");
        }
        finally { Directory.Delete(dir, true); }
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

        // portal 2 leads to Darkmere Crypt, which holds the Fire Key behind a gate needing two levers and two plates
        var p2 = g.Level.FindMark('2').Value;
        g.P.X = p2.x; g.P.Y = p2.y; Tick(default);
        check(g.Level == g.Hub[2], "portal 2 leads to Darkmere Crypt");
        var crypt = g.Level;
        crypt.Things.RemoveAll(t => t is Monster);
        check(crypt.LeverCount == 2 && crypt.PlateCount == 2, "crypt has two levers and two pressure plates");
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
        check(crypt.DoorOpen[gate] == 0f, "levers alone don't open the gate while plates are empty");

        // the block puzzle in the south-east room, solved with real Use presses
        const float N = -MathF.PI / 2, W_ = MathF.PI, E = 0f;
        void Act(float x, float y, float angle, bool pull = false)
        {
            g.P.X = x; g.P.Y = y; g.P.Angle = angle;
            Tick(new Input { Use = true, Walk = pull });
        }
        bool BlockAt(int x, int y) => crypt.Cell(x, y) == 'X';
        check(BlockAt(23, 9) && BlockAt(24, 9), "two stone blocks start in the puzzle room");
        Act(23.5f, 10.5f, N); check(BlockAt(23, 8) && !BlockAt(23, 9), "E pushes a block away from you");
        Act(23.5f, 9.5f, N);
        Act(24.5f, 7.5f, W_); Act(23.5f, 7.5f, W_);
        check(BlockAt(21, 7) && crypt.PlatesCovered == 1, "block pushed onto the first plate");
        Act(22.5f, 7.5f, W_); check(BlockAt(21, 7), "a block can't be pushed into a wall");
        Tick(default, 35);
        check(crypt.DoorOpen[gate] == 0f, "one plate is not enough");
        Act(24.5f, 10.5f, N); Act(24.5f, 9.5f, N);
        Act(23.5f, 7.5f, E); Act(24.5f, 7.5f, E);
        check(BlockAt(26, 7) && crypt.PlatesCovered == 2, "block pushed onto the second plate");
        Tick(default, 35 * 2);
        check(crypt.DoorOpen[gate] >= 1f, "levers + plates raise the crypt gate");

        // Shift+E pulls a block toward you; lifting a plate drops the gate again
        Act(25.5f, 7.5f, E, pull: true);
        check(BlockAt(25, 7) && !BlockAt(26, 7) && MathF.Abs(g.P.X - 24.5f) < 0.01f, "Shift+E pulls the block and steps you back");
        Tick(default, 35 * 2);
        check(crypt.DoorOpen[gate] == 0f, "gate closes when a plate is uncovered");
        Act(24.5f, 7.5f, E);
        Tick(default, 35 * 2);
        check(BlockAt(26, 7) && crypt.DoorOpen[gate] >= 1f, "pushing it back reopens the gate");
        var key = crypt.Things.OfType<Pickup>().First(p => p.Kind == PickupKind.FireKey);
        g.P.X = key.X; g.P.Y = key.Y; Tick(default);
        check(g.P.FireKey, "Fire Key picked up");

        g.Warp(1);
        g.P.X = fx + 0.5f; g.P.Y = fy - 0.6f; g.P.Angle = MathF.PI / 2;
        Tick(new Input { Use = true }); Tick(default, 35);
        check(g.Level.DoorOpen[fire] >= 1f, "Fire Key opens the fire door");

        // the east room's lever raises the vault gate; portal 4 inside leads up to the Windspire and back
        var keepLv = g.Level;
        keepLv.Things.RemoveAll(t => t is Monster);
        int kgate = Array.IndexOf(keepLv.Cells, 'P');
        g.P.X = 30.5f; g.P.Y = 9.5f; g.P.Angle = 0;
        Tick(new Input { Use = true }); Tick(default, 35 * 2);
        check(keepLv.LeverPulled && keepLv.DoorOpen[kgate] >= 1f, "the Keep's lever raises the vault gate");
        var p4 = keepLv.FindMark('4').Value;
        g.P.X = p4.x; g.P.Y = p4.y; g.P.PortalLock = false; Tick(default);
        check(g.Level.RawName == "Windspire", "portal 4 in the Keep's vault leads to the Windspire");
        g.P.X += 1.2f; Tick(default);
        g.P.X -= 1.2f; Tick(default);
        check(g.Level == keepLv, "and brings you back to the vault");

        // the Chaos Arena has its own title menu item; portal 3 in the courtyard leads to the Hanging Cisterns now
        check(g.Hub.All(l => l.Arena == null && l.RawName != "Chaos Arena"), "the Chaos Arena isn't in the hub any more (it's Arena on the title menu)");
        g.Warp(0);
        Tick(default);
        var p3 = g.Level.FindMark('3').Value;
        g.P.X = p3.x; g.P.Y = p3.y; g.P.PortalLock = false; Tick(default);
        check(g.Level.RawName == "Hanging Cisterns", "portal 3 in the courtyard leads to the Hanging Cisterns");
    }

    /// <summary>Custom maps: the file format, play-testing one, and the in-game editor being gone.</summary>
    static void CustomMapChecks(Action<bool, string> check)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"hexensharp-maps-{Environment.ProcessId}");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        var g = new Game { FixedSeed = 1, MapsDir = dir };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }

        check(!g.Menu.Items(MenuPage.Main).Contains("Level editor") && g.Menu.Items(MenuPage.Main).SequenceEqual(new[] { "New game", "Practice", "Arena", "Story", "Online", "Leaderboard", "Character", "Options", "Quit" }),
              "the title menu no longer has a level editor (maps are made in tools/editor)");
        g.Con.Execute("edit");
        check(g.Con.Log.Last().Contains("unknown"), "the 'edit' console command is gone");

        // the file format the HTML editor writes
        var doc = new MapDoc(20, 16) { Name = "Test Grotto", ThemeId = "crypt", DefaultHeight = 2f };
        check(doc[0, 0] == '#' && doc[5, 5] == '.', "a new map is walled");
        doc[3, 12] = '@'; doc[12, 12] = 'E'; doc[17, 12] = 'e';
        for (int x = 4; x <= 8; x++) doc[x, 6] = 'B';
        doc.Floors[3 * 20 + 5] = '4'; doc.Heights[3 * 20 + 6] = 'a';
        string text = doc.Serialize();
        var back = MapDoc.Parse(text);
        check(back.Serialize() == text && back.Name == "Test Grotto" && back.ThemeId == "crypt" && back.DefaultHeight == 2f,
              "a map saves and loads back identically, heights and floors included");
        check(Maps.Hub.All(d => MapDoc.Parse(MapDoc.FromDef(d).Serialize()).Rows().SequenceEqual(d.Rows)), "every built-in map survives save and load");
        check(MapDoc.Parse("name: X\n---\n#####\n#@?Q#\n#####\n")[2, 1] == '.', "unknown glyphs in a file become floor");
        check(MapDoc.FileName("Test Grotto!") == "test_grotto.hxm" && MapDoc.FileName("??") == "untitled.hxm", "file names are lower-case with underscores");
        check(new MapDoc(8, 8).Validate().SequenceEqual(new[] { "! Place a player start (@) first." }), "a map without a start can't be played");
        check(doc.Validate().Count == 0, $"a map with a start and a reachable exit passes ({string.Join("; ", doc.Validate())})");

        // play-testing a map
        g.StartTest(doc.ToDef(), PClass.Cleric);
        check(g.Mode == GameMode.Playing && g.TestingMap && g.Hub.Length == 1 && g.Level.Name == "Test Grotto", "a custom map plays on its own");
        check((int)g.P.X == 3 && (int)g.P.Y == 12 && g.Level.Cell(8, 6) == 'B' && g.Level.Things.Any(t => t is Monster && (int)t.X == 17),
              "the level matches the file");
        check(g.Level.BossDead, "a map with no Heresiarch has its exit open");
        g.Level.Things.RemoveAll(t => t is Monster);
        var exit = g.Level.FindMark('E').Value;
        g.P.X = exit.x; g.P.Y = exit.y;
        Tick(default);
        check(g.Mode == GameMode.Victory, "reaching the exit wins the play-test");
        Tick(new Input { Confirm = true });
        check(g.Mode == GameMode.Playing && g.TestingMap && (int)g.P.X == 3 && g.P.Class == PClass.Cleric, "Enter on victory plays the map again");
        Tick(new Input { Pause = true });
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Pause), "Quit to title");
        check(g.Menu.Cursor >= 0, "the pause menu quits to the title");
        Tick(new Input { Confirm = true });
        check(g.Mode == GameMode.Title && !g.TestingMap, "and that ends the play-test");

        // playmap: a file path, or a name in the maps folder
        string file = Path.Combine(dir, MapDoc.FileName(doc.Name));
        File.WriteAllText(file, text);
        g.Con.Execute("playmap test grotto");
        check(g.Mode == GameMode.Playing && g.Level.Name == "Test Grotto", "console 'playmap test grotto' finds it in the maps folder");
        g.GoToTitle();
        g.Con.Execute("playmap " + file);
        check(g.Mode == GameMode.Playing && g.Level.Name == "Test Grotto", "console 'playmap <path>' plays a file anywhere");
        g.GoToTitle();
        g.NewGame(PClass.Fighter);
        check(g.Hub.Length == Maps.Hub.Length, "normal games still use the full hub afterwards");
        Directory.Delete(dir, true);
    }

    static void HeightChecks(Action<bool, string> check)
    {
        check(Level.HeightFromGlyph('2', 1) == 1f && Level.HeightFromGlyph('6', 1) == 3f && Level.HeightFromGlyph('9', 1) == 4.5f
              && Level.HeightFromGlyph('.', 1.5f) == 1.5f && Level.HeightFromGlyph('1', 1) == 1f, "height glyphs: 2..9 = 1.0..4.5, others = default");
        var hub = Maps.BuildHub();
        var wh = hub[0];
        check(wh.HeightAt(14.5f, 5.5f) == 3f, "Winnowing Hall's great hall is 3 tall");
        check(wh.HeightAt(4.5f, 18.5f) == 3.5f && Maps.ChaosArena.Build().HeightAt(15.5f, 8.5f) == 3.5f, "the boss arena and Chaos Arena tower at 3.5");
        check(wh.HeightAt(17.5f, 12.5f) == 1f, "corridors stay one storey");
        check(Enumerable.Range(0, wh.Cells.Length).Where(i => Level.IsDoor(wh.Cells[i])).All(i => wh.Heights[i] == 1f), "doors are always one storey");
        check(hub[2].HeightAt(23.5f, 8.5f) == 1f, "the crypt's block-puzzle room stays one storey");

        // rendering: the same view, with and without heights
        var g = new Game { FixedSeed = 1 };
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster or Decor or Chest or LoreStone);
        var r = new Renderer();
        float proj = 160f / MathF.Tan(g.Vars.Fov * MathF.PI / 360f), horizon = Renderer.StatusViewH / 2f;
        int RowOf(float z, float d) => (int)(horizon - (z - 0.5f) * proj / d);

        // great hall, facing the north wall 7.5 away: its top reaches far higher than one storey
        g.P.X = 14.5f; g.P.Y = 8.5f; g.P.Angle = -MathF.PI / 2; g.P.Pitch = 0;
        r.Render(g);
        int y25 = RowOf(2.5f, 7.5f);
        check(MathF.Abs(r.DepthAt(160, y25) - 7.5f) < 0.2f, "a 3-tall hall's far wall is drawn 2.5 units up");
        var flat = new MapDef("Flat", "Flat", "hall", Maps.Hub[0].Rows).Build();
        flat.Things.RemoveAll(t => t is Monster or Decor or Chest or LoreStone);
        var tall = g.Level;
        g.Level = flat;
        r.Render(g);
        check(r.DepthAt(160, y25) < 7f, "without heights the same spot is ceiling");
        g.Level = tall;

        // start room (1.5 tall) facing the closed door 3 away: wall fills the space above the door
        g.P.X = 4f; g.P.Y = 3.5f; g.P.Angle = 0;
        r.Render(g);
        float dd = 7f - 4f;
        int doorTop = RowOf(1f, dd), roomTop = RowOf(1.5f, dd);
        bool lintel = Enumerable.Range(roomTop + 2, Math.Max(1, doorTop - roomTop - 4)).All(y => MathF.Abs(r.DepthAt(160, y) - dd) < 0.05f);
        check(lintel && roomTop + 2 < doorTop - 2, "wall is drawn above a doorway in a taller room");

        // the camera never pokes through a low ceiling
        var low = new MapDef("Low", "Low", "hall", new[] { "#####", "#@..#", "#####" }).Build();
        g.Level = low; g.P.X = 1.5f; g.P.Y = 1.5f; g.P.Z = 0.45f;
        r.Render(g);
        check(r.DepthAt(160, 0) > 0, "jumping under a one-storey ceiling renders fine");
        g.P.Z = 0;

        // map files: heights survive save/load; files without heights stay flat
        var doc = new MapDoc(10, 8) { Name = "Tower", DefaultHeight = 2f };
        doc[2, 2] = '@';
        doc.Heights[3 * 10 + 4] = '8';
        var back = MapDoc.Parse(doc.Serialize());
        check(back.DefaultHeight == 2f && back.Heights[3 * 10 + 4] == '8' && back.Heights[3 * 10 + 5] == '.', "heights and the default height round-trip through a file");
        var built = back.ToDef().Build();
        check(built.HeightAt(4.5f, 3.5f) == 4f && built.HeightAt(5.5f, 3.5f) == 2f, "a saved tower builds with its heights");
        check(MapDoc.Parse("name: Old\n---\n#####\n#@..#\n#####\n").ToDef().Build().HeightAt(2.5f, 1.5f) == 1f, "old map files without heights are one storey");
        check(Maps.Hub.All(d => MapDoc.FromDef(d).ToDef().Build().Heights.SequenceEqual(d.Build().Heights)), "built-in maps keep their heights when saved as map files");

        // a map file with its own heights plays with them
        var tallMap = new MapDoc(20, 16) { DefaultHeight = 1.5f };
        tallMap[2, 2] = '@';
        tallMap.Heights[6 * 20 + 6] = '8';
        var eg = new Game { FixedSeed = 1 };
        eg.StartTest(MapDoc.Parse(tallMap.Serialize()).ToDef(), PClass.Fighter);
        check(eg.Mode == GameMode.Playing && eg.Level.HeightAt(6.5f, 6.5f) == 4f && eg.Level.HeightAt(8.5f, 8.5f) == 1.5f,
              "a map file's ceiling heights (and its default height) are what you play");
    }

    static void StairChecks(Action<bool, string> check)
    {
        var hub = Maps.BuildHub();
        check(hub[0].FloorAt(15.5f, 1.5f) == 0.75f && hub[0].FloorAt(15.5f, 3.5f) == 0.5f && hub[0].FloorAt(15.5f, 4.5f) == 0.25f,
              "the great hall has a dais up three steps");
        check(hub[0].FloorAt(4.5f, 18.5f) == 0.75f, "the Heresiarch stands on a stepped platform");
        check(hub[1].FloorAt(27.5f, 2.5f) == 0.75f && hub[1].FloorAt(23.5f, 2.5f) == 0.25f, "the Frozen Keep has a terrace with stairs");
        check(hub[0].HeightAt(15.5f, 1.5f) == 3f, "ceilings stay put when the floor rises");

        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster or Chest or Decor or LoreStone);
        var wh = g.Level;

        // walk up the dais steps; the camera eases up rather than jumping
        g.P.X = 15.5f; g.P.Y = 7.5f; g.P.Angle = -MathF.PI / 2;
        float lastEye = g.P.FloorZ + g.P.ViewZ, maxJump = 0;
        for (int k = 0; k < 35 * 3; k++)
        {
            Tick(new Input { Move = 1 });
            float eye = g.P.FloorZ + g.P.ViewZ;
            maxJump = MathF.Max(maxJump, MathF.Abs(eye - lastEye));
            lastEye = eye;
        }
        check(g.P.FloorZ == 0.75f && g.P.Y < 3f, $"walking forward climbs the stairs onto the dais (floor {g.P.FloorZ})");
        check(maxJump < 0.12f, $"the camera rises smoothly (largest change per frame {maxJump:0.00})");

        // the Keep terrace is too tall to walk onto, but you can jump up; walking off drops you
        g.Warp(1);
        g.Level.Things.RemoveAll(t => t is Monster or Chest or Decor or LoreStone or Pickup);
        g.P.X = 27.5f; g.P.Y = 5.5f; g.P.Angle = -MathF.PI / 2;
        Tick(new Input { Move = 1 }, 35);
        check(g.P.FloorZ == 0f && g.P.Y > 5.2f, "a 0.75 ledge blocks walking");
        Tick(new Input { Jump = true, Move = 1 });
        Tick(new Input { Move = 1 }, 35);
        check(g.P.FloorZ == 0.75f && g.P.Y < 4.8f, "jumping gets you up onto the ledge");
        g.P.X = 27.5f; g.P.Y = 3.5f; g.P.Angle = MathF.PI / 2;
        bool airborne = false;
        for (int k = 0; k < 35; k++) { Tick(new Input { Move = 1 }); airborne |= g.P.Z > 0.05f; }
        Tick(default, 20);
        check(airborne && g.P.FloorZ == 0f && g.P.OnGround, "walking off the edge falls and lands");
        g.P.X = 22.5f; g.P.Y = 2.5f; g.P.Angle = 0;
        Tick(new Input { Move = 1 }, 35 * 2);
        check(g.P.FloorZ == 0.75f, "the terrace stairs lead up");

        // missiles hit the face of a ledge
        var bolt = new Projectile { Kind = ProjKind.Bolt, FromPlayer = true, DmgMin = 1, DmgMax = 1, X = 27.5f, Y = 5.8f, Z = 0.35f, VY = -8, Level = g.Level };
        g.Level.Things.Add(bolt);
        g.P.X = 20.5f; g.P.Y = 8.5f;
        float boltY = 0;
        for (int k = 0; k < 35 && !bolt.Removed; k++) { Tick(default); boltY = bolt.Y; }
        check(bolt.Removed && boltY > 4.85f && boltY < 5.3f, $"a low missile stops at the face of the ledge (y {boltY:0.00})");

        // monsters climb stairs too
        g.Warp(0);
        g.Vars.God = true;
        g.P.X = 15.5f; g.P.Y = 1.5f;
        var ettin = new Monster(Monster.Ettin) { X = 15.5f, Y = 7.5f, Level = g.Level };
        g.Level.Things.Add(ettin);
        for (int k = 0; k < 35 * 10 && g.Level.FloorAt(ettin.X, ettin.Y) < 0.5f; k++) Tick(default);
        check(g.Level.FloorAt(ettin.X, ettin.Y) >= 0.5f, "an ettin climbs the stairs after you");
        g.Vars.God = false;

        // the step faces render: looking at the dais from the hall floor
        var r = new Renderer();
        g.Level.Things.RemoveAll(t => t is Monster);
        g.P.X = 15.5f; g.P.Y = 8.5f; g.P.FloorZ = 0; g.P.Z = 0; g.P.Angle = -MathF.PI / 2; g.P.Pitch = 0;
        r.Render(g);
        float proj = 160f / MathF.Tan(g.Vars.Fov * MathF.PI / 360f);
        int riserRow = (int)(Renderer.StatusViewH / 2f - (0.12f - 0.5f) * proj / 3.5f);
        check(MathF.Abs(r.DepthAt(160, riserRow) - 3.5f) < 0.15f, "the first step's face is drawn 3.5 away");
        var flatDef = Maps.Hub[0] with { Floors = null };
        g.Level = flatDef.Build();
        r.Render(g);
        check(MathF.Abs(r.DepthAt(160, riserRow) - 3.5f) > 0.3f, "without floors the same pixel is plain floor");
        g.Level = wh;

        // reachability respects steps: a platform without stairs can't be reached on foot
        var plat = new MapDef("Plat", "Plat", "hall", new[] { "#######", "#@...h#", "#######" }, null, 1.5f,
                              new[] { ".......", ".....3.", "......." }).Build();
        check(!plat.Reachable(1, 1)[5] && plat.Walkable(4, 4) && !plat.Walkable(1 * 7 + 4, 1 * 7 + 5), "a 0.75 step with no stairs is unreachable");
        check(!plat.BlockCanEnter(5, 1, 4, 1), "stone blocks only slide over level ground");

        // a map file with a staircase: floors survive save and load, and you can walk up them
        var stairs = new MapDoc(20, 16);
        stairs[4, 8] = '@';
        for (int x = 5; x <= 8; x++) stairs.Floors[8 * 20 + x] = (char)('1' + x - 5);
        var back = MapDoc.Parse(stairs.Serialize());
        check(back.Floors.SequenceEqual(stairs.Floors) && back.Heights.SequenceEqual(stairs.Heights), "floors survive save and load");
        var eg = new Game { FixedSeed = 1 };
        eg.StartTest(back.ToDef(), PClass.Fighter);
        check(eg.Mode == GameMode.Playing && eg.Level.FloorAt(8.5f, 8.5f) == 1f && eg.Level.HeightAt(8.5f, 8.5f) >= 2f,
              "a map file's floors are what you play (with headroom kept above them)");
        eg.P.Angle = 0;
        for (int k = 0; k < 35 * 3 && eg.P.X < 8.5f; k++) eg.Update(new Input { Move = 1 }, 1f / 35f);
        check(eg.P.FloorZ == 1f && eg.P.X >= 8.5f, "and you can walk up its staircase");
    }

    static void StyleChecks(Action<bool, string> check)
    {
        check(Art.Style == ArtStyle.SciFi, "sci-fi is the default look");
        var g = new Game { FixedSeed = 1 };
        g.NewGame(PClass.Fighter);
        var r = new Renderer();
        check(g.P.Def.Name == "Marine" && g.Level.Name == "Hab Ring" && g.P.Def.Weapons[0].Name == "Power Fist", "sci-fi names: Marine, Hab Ring, Power Fist");
        var stone = g.Level.Things.OfType<LoreStone>().First();
        string scifiLore = stone.Text;
        var vial = g.Level.Things.OfType<Pickup>().First(p => p.Kind == PickupKind.Vial);
        var sciStone = Art.Stone; var sciVial = vial.Sprite(0);
        check(g.Level.Theme.Walls['#'] == Art.Stone, "the level uses the sci-fi wall texture");

        // switch mid-game: art, names, themes and lore all follow
        g.SetArtStyle(ArtStyle.Fantasy);
        check(Art.Style == ArtStyle.Fantasy && Art.Stone != sciStone && vial.Sprite(0) != sciVial, "switching rebuilds the art, and existing items pick it up");
        check(g.Level.Theme.Walls['#'] == Art.Stone, "the current level's walls switch too");
        check(g.P.Def.Name == "Fighter" && g.Level.Name == "Winnowing Hall" && g.P.Def.Weapons[1].Name == "Timon's Axe", "fantasy names come back: Fighter, Winnowing Hall, Timon's Axe");
        check(stone.Text != scifiLore && stone.Text.Length > 20, "lore text follows the style");
        bool renders = true;
        foreach (var style in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
        {
            g.SetArtStyle(style);
            foreach (var lv in g.Hub)
            {
                g.Level = lv;
                var (ax, ay) = lv.ArrivalCell();
                g.P.X = ax + 0.5f; g.P.Y = ay + 0.5f;
                try { r.Render(g); } catch { renders = false; }
            }
        }
        check(renders, "every map renders in both styles");
        g.SetArtStyle(ArtStyle.Fantasy);
        var fantasyRelics = Art.Relics.ToArray();
        g.SetArtStyle(ArtStyle.SciFi);
        check(Art.Relics.Length == 6 && fantasyRelics.Length == 4 && Art.Relics.All(t => !fantasyRelics.Contains(t)),
              "sci-fi relics are their own six artifact designs");
        check(Words.T("Relic found: Data Crystal (3/12)") == "Artifact found: Data Crystal (3/12)" && Words.T("RELICS") == "ARTIFACTS",
            "sci-fi messages call relics artifacts");
        g.SetArtStyle(ArtStyle.Fantasy);
        check(Words.T("Relic found: x (1/2)") == "Relic found: x (1/2)", "fantasy messages keep relics");
        g.SetArtStyle(ArtStyle.SciFi);
        var relicNames = new Game { FixedSeed = 3, Style = GameStyle.Relaxed };
        relicNames.NewGame(PClass.Mage);
        var variants = relicNames.Hub.SelectMany(l => l.Things.OfType<Pickup>()).Where(p => p.Kind == PickupKind.Relic).Select(p => p.Variant % Art.Relics.Length).Distinct().Count();
        check(variants >= 4, $"a relaxed game shows a spread of artifact designs ({variants} of 6)");
        g.Level = g.Hub[0];

        // the Options menu toggles it, and it's saved with your settings
        g.Menu.Show(MenuPage.Options);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Options), "Visual style");
        check(g.Menu.Value(g.Menu.Cursor) == "SCI-FI", "Options shows the visual style");
        g.Update(new Input { Right = true }, 1f / 35f);
        check(Art.Style == ArtStyle.Fantasy, "Options > Visual style switches to fantasy");
        string path = Path.Combine(Path.GetTempPath(), $"hexensharp-style-{Environment.ProcessId}.cfg");
        g.ConfigPath = path;
        g.SaveSettings();
        g.SetArtStyle(ArtStyle.SciFi);
        var g2 = new Game { ConfigPath = path };
        g2.LoadSettings();
        check(Art.Style == ArtStyle.Fantasy, "the chosen style is restored from settings");
        File.Delete(path);
        g2.Con.Execute("artstyle scifi");
        check(Art.Style == ArtStyle.SciFi, "console 'artstyle scifi'");
    }
}
