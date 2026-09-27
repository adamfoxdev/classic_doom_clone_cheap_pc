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
            var flyTo = lv.Reachable(start.Item1, start.Item2, move: Level.Move.Fly);
            for (int i = 0; i < lv.Marks.Length; i++)
                if (lv.Marks[i] != '\0') Check(lv.Marks[i] == '+' ? flyTo[i] : reach[i], $"mark '{lv.Marks[i]}' at {i % lv.W},{i / lv.W} reachable");
            foreach (var t in lv.Things)
            {
                if (t is Pickup pk && pk.Kind is PickupKind.SteelKey or PickupKind.FireKey or PickupKind.Weapon2 or PickupKind.Weapon3)
                    Check(reach[(int)t.Y * lv.W + (int)t.X], $"{pk.Kind} reachable");
                if (t is Monster m && m.Def.Boss) Check(reach[(int)t.Y * lv.W + (int)t.X], "boss reachable");
                Check(!lv.BlocksPoint(t.X, t.Y), $"{t.GetType().Name} at {t.X - 0.5f},{t.Y - 0.5f} not inside a wall");
            }
            // levers must be touchable from an open cell (the Windspire's needs the jetpack to get to)
            var flyReach = lv.Reachable(start.Item1, start.Item2, move: Level.Move.Fly);
            for (int i = 0; i < lv.Cells.Length; i++)
                if (lv.Cells[i] == 'L')
                {
                    int x = i % lv.W, y = i / lv.W;
                    bool ok = (x > 0 && flyReach[i - 1]) || (x < lv.W - 1 && flyReach[i + 1]) || (y > 0 && flyReach[i - lv.W]) || (y < lv.H - 1 && flyReach[i + lv.W]);
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
        Console.WriteLine("Arcade mode:");
        ArcadeChecks(Check);
        Console.WriteLine("Arena mode:");
        ArenaModeChecks(Check);

        Console.WriteLine("Dark Bishop:");
        BishopChecks(Check);
        Console.WriteLine("Chests:");
        ChestChecks(Check);

        Console.WriteLine("Options and key bindings:");
        OptionsChecks(Check);

        Console.WriteLine("Relaxed mode and discovery:");
        RelaxedChecks(Check);

        Console.WriteLine("Custom maps:");
        CustomMapChecks(Check);

        Console.WriteLine("Ceiling heights:");
        HeightChecks(Check);

        Console.WriteLine("Stairs and floors:");
        StairChecks(Check);

        Console.WriteLine("Visual styles:");
        StyleChecks(Check);

        Console.WriteLine("Jetpack:");
        JetpackChecks(Check);
        Console.WriteLine("Windspire:");
        SpireChecks(Check);
        Console.WriteLine("Deepdelve Quarry and rubble:");
        QuarryChecks(Check);
        VerticalAimChecks(Check);
        Console.WriteLine("Checkpoints:");
        CheckpointChecks(Check);
        Console.WriteLine("Character progression:");
        RpgChecks(Check);

        Console.WriteLine("Quake movement:");
        QuakeMoveChecks(Check);
        Console.WriteLine("Strafe-jumping practice:");
        PracticeChecks(Check);
        Console.WriteLine("Practice ghost:");
        GhostChecks(Check);
        Console.WriteLine("More practice courses:");
        CourseChecks(Check);
        Console.WriteLine("Medals:");
        MedalChecks(Check);
        Console.WriteLine("Practice demo:");
        DemoChecks(Check);
        Console.WriteLine("Strafe helper:");
        StrafeHelperChecks(Check);
        Console.WriteLine("HUD styles:");
        HudChecks(Check);
        Console.WriteLine("Rendered art pack:");
        RenderedArtChecks(Check);
        Console.WriteLine("Map files and the HTML editor:");
        MapFileChecks(Check);

        Console.WriteLine("Audio synthesis:");
        SoundChecks(Check);

        Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    static void QuarryChecks(Action<bool, string> check)
    {
        var hub = Maps.BuildHub();
        int qi = Array.FindIndex(hub, l => l.RawName == "Deepdelve Quarry");
        check(qi == 4 && hub.Count(l => l.FindMark('5') != null) == 2 && hub[0].FindMark('5') != null,
              "portal 5 in Winnowing Hall's courtyard leads to Deepdelve Quarry");
        var lv = hub[qi];
        var (ax, ay) = lv.ArrivalCell();
        var rubble = Enumerable.Range(0, lv.Cells.Length).Where(i => lv.Cells[i] == Level.Rubble).ToHashSet();
        var walled = lv.Reachable(ax, ay, rubble);
        var dug = lv.Reachable(ax, ay);
        int gallery = 3 * lv.W + 14, vault = 16 * lv.W + 11;
        check(rubble.Count > 150 && !walled[gallery] && dug[gallery] && dug[vault] && !walled[vault],
              $"the gallery and the strongroom are sealed behind rubble ({rubble.Count} blocks)");
        check(lv.WalkableFloor.Contains(9 * lv.W + 5), "rubble counts as floor to explore");
        check(lv.BlockHp[2 * lv.W + 6] == Level.RubbleHp && lv.CrackStage(2 * lv.W + 6) == 0 && lv.Blocks(6, 2), "rubble starts whole and solid");

        var g = new Game { FixedSeed = 5 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);
        g.Warp(qi);
        var q = g.Level;
        q.Things.RemoveAll(t => t is Monster or Chest);
        var p = g.P;
        // punch through the plug east of the arrival room
        p.X = 5.5f; p.Y = 2.5f; p.Angle = 0;
        int plug = 2 * q.W + 6;
        var stages = new HashSet<int>();
        for (int k = 0; k < 35 * 10 && q.Cells[plug] == Level.Rubble; k++) { stages.Add(q.CrackStage(plug)); Tick(new Input { Fire = true }); }
        check(q.Cells[plug] == '\0' && !q.Blocks(6, 2), "gauntlets smash a rubble block");
        check(stages.Count >= 2, $"it cracks up as you hit it ({stages.Count} stages seen)");
        check(q.Things.Any(t => t is Puff), "it bursts into debris");
        // walk into the hole
        Tick(new Input { Move = 1 }, 12);
        check(p.X > 6.1f, "you can walk into the hole you made");

        // prying by hand works too (it's the only way in relaxed mode)
        p.X = 5.5f; p.Y = 3.5f; p.Angle = 0;
        int pry = 3 * q.W + 6;
        for (int k = 0; k < 35 * 6 && q.Cells[pry] == Level.Rubble; k++) Tick(new Input { Use = k % 2 == 0 });
        check(q.Cells[pry] == '\0', "Use pries a rubble block loose");

        // a splash weapon chips every block around the blast
        var mg = new Game { FixedSeed = 5 };
        mg.NewGame(PClass.Cleric);
        mg.Warp(qi);
        var mq = mg.Level;
        mq.Things.RemoveAll(t => t is Monster or Chest);
        mg.P.HasWeapon[2] = true; mg.P.GreenMana = 200; mg.P.Weapon = 2;
        mg.P.X = 14.5f; mg.P.Y = 5.5f; mg.P.Angle = MathF.PI / 2;
        for (int k = 0; k < 35 * 2; k++) mg.Update(new Input { Fire = k < 3 }, 1f / 35f);
        int chipped = rubble.Count(i => mq.Cells[i] != Level.Rubble || mq.BlockHp[i] < Level.RubbleHp);
        check(chipped >= 3, $"a firestorm blast chips several blocks ({chipped})");

        // monsters' shots don't dig
        q.Things.Add(new Projectile { Kind = ProjKind.Fireball, FromPlayer = false, DmgMin = 90, DmgMax = 90, X = 2.5f, Y = 5.9f, VX = 0, VY = 6f, Level = q });
        Tick(default, 20);
        check(q.Cells[6 * q.W + 2] == Level.Rubble && q.BlockHp[6 * q.W + 2] == Level.RubbleHp, "monster fire doesn't break rubble");

        // each style has its own rubble, with distinct crack stages
        foreach (var style in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
        {
            g.SetArtStyle(style);
            var s = Art.RubbleCracked;
            check(s.Length == Level.RubbleStages && s[0] == Art.Rubble && Enumerable.Range(1, s.Length - 1).All(k => !s[k].Px.SequenceEqual(s[k - 1].Px)),
                  $"{style} rubble has {Level.RubbleStages} crack stages");
        }
        g.SetArtStyle(ArtStyle.SciFi);
        DigChecks(check);
        PlaceChecks(check);
        PlanetChecks(check);
        FlightChecks(check);
    }

    static void PlaceChecks(Action<bool, string> check)
    {
        // in the quarry: break a block, carry it, build a wall with it
        var g = new Game { FixedSeed = 3 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);
        int qi = Array.FindIndex(g.Hub, l => l.RawName == "Deepdelve Quarry");
        g.Warp(qi);
        var q = g.Level;
        var p = g.P;
        q.Things.RemoveAll(t => t is Monster or Chest);
        p.X = 3.5f; p.Y = 2.5f; p.Angle = 0; p.PortalLock = true;
        Tick(new Input { Place = true });
        check(p.Blocks == 0 && q.Cells[2 * q.W + 4] == '\0', "nothing to place until you've broken a block");
        g.HitBlock(6, 2, 999);
        check(p.Blocks == 1, "breaking rubble puts the block in your pack");
        Tick(new Input { Place = true });
        check(p.Blocks == 0 && q.Cells[2 * q.W + 4] == Level.Rubble && q.BlockHp[2 * q.W + 4] == Level.RubbleHp, "Place builds it into the cell ahead of you");
        g.HitBlock(4, 2, 999);
        check(p.Blocks == 1 && q.Cells[2 * q.W + 4] == '\0', "and you can break it back out");
        p.X = 4.5f; p.Y = 2.5f; p.Angle = 0;
        Tick(new Input { Place = true });
        check(q.Cells[2 * q.W + 4] == '\0' && q.Cells[2 * q.W + 5] == Level.Rubble, "you never build a block on top of yourself");
        g.HitBlock(5, 2, 999);
        check(p.Blocks == 1, "your own block comes back to you");

        // on a dig map: build a step up under yourself, or bring the ceiling down
        int di = Array.FindIndex(g.Hub, l => l.Dig);
        g.Warp(di);
        var d = g.Level;
        var (ax, ay) = d.ArrivalCell();
        p.X = ax + 0.5f; p.Y = ay + 0.5f; p.Angle = 0; p.Pitch = 0; p.PortalLock = true;
        g.HitBlock(ax + 1, ay, 999, slot: d.Floors[ay * d.W + ax]);
        int e = ay * d.W + ax + 1;
        for (int k = 0; k < 3; k++) g.HitBlock(ax + 1, ay, 999, Level.Face.Ceiling);
        p.X = ax + 1.5f; Tick(default, 3);
        float floor = d.Floors[e], roof = d.Heights[e];
        p.Pitch = -70; p.Blocks = 4;
        Tick(new Input { Place = true }); Tick(default, 10);
        check(d.Floors[e] == floor + Level.DigStep && MathF.Abs(p.FloorZ - d.Floors[e]) < 0.01f, "look down and place: a block under your feet lifts you a step");
        p.Pitch = 70;
        Tick(new Input { Place = true }); Tick(default);
        Tick(new Input { Place = true }); Tick(default);
        check(d.Heights[e] == roof - 2 * Level.DigStep && d.Heights[e] - d.Floors[e] == Level.MinHeight, "look up and place: the ceiling comes down a block at a time");
        Tick(new Input { Place = true });
        check(d.Heights[e] - d.Floors[e] == Level.MinHeight && p.Blocks == 1, "but never lower than a storey above the floor");

        // ore you mine goes to the ship, not your block pack
        int bi = Array.FindIndex(g.Hub, l => l.Ship != null);
        g.Warp(bi);
        int vein = Array.FindIndex(g.Level.Cells, c => c == 'N');
        int before = p.Blocks;
        g.HitBlock(vein % g.Level.W, vein / g.Level.W, 999);
        check(p.Blocks == before && p.Ore[0] == 1, "ore goes to the ship, not your block pack");
    }

    static void FlightChecks(Action<bool, string> check)
    {
        var hub = Maps.BuildHub();
        int fi = Array.FindIndex(hub, l => l.Flight), mi = Array.FindIndex(hub, l => l.RawName == "Verdant Moon");
        var lane = hub[fi];
        check(fi == 7 && mi == 8 && hub.Count(l => l.Flight) == 1, "the Void Crossing is flown, and the Verdant Moon lies beyond it");
        check(hub.Count(l => l.FindMark('8') != null) == 2 && lane.FindMark('8') != null && hub[mi].FindMark('8') != null, "portal 8 at the end of the crossing lands on the moon");
        check(hub.Count(l => l.FindMark('9') != null) == 2 && hub[0].FindMark('9') != null && hub[mi].FindMark('9') != null, "portal 9 on the moon leads home to Winnowing Hall");
        var rocks = lane.Things.OfType<Asteroid>().ToList();
        int firstHalf = rocks.Count(a => a.X < lane.W / 2f);
        check(rocks.Count > 60 && firstHalf < rocks.Count - firstHalf, $"asteroids thicken along the crossing ({firstHalf} then {rocks.Count - firstHalf})");
        check(lane.Things.OfType<Monster>().Count() >= 6 && lane.Things.OfType<Monster>().All(m => m.Def.FlyZ > 0), "only flyers come at you out there");

        var g = new Game { FixedSeed = 12 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);
        g.Warp(fi);
        var f = g.Level;
        var p = g.P;
        check(f.Flight && p.Z > 1f && p.ShipSpeed == Game.FlightCruise && g.CanRespawn, "into the pilot's seat, with a checkpoint at the start");
        f.Things.RemoveAll(t => t is Monster or Asteroid or Pickup);
        float x0 = p.X;
        Tick(default, 35);
        check(p.X > x0 + Game.FlightCruise * 0.8f, "the ship cruises forward by itself");
        float cruise = p.X;
        Tick(new Input { Move = 1 }, 35);
        check(p.X - cruise > Game.FlightCruise * 1.1f && p.ShipSpeed > Game.FlightCruise, "forward speeds it up");
        float z0 = p.Z;
        Tick(new Input { JumpHeld = true }, 20);
        check(p.Z > z0 + 0.5f, "Jump climbs");
        Tick(new Input { SlideHeld = true }, 100);
        check(p.Z <= 0.11f, "Slide dives, down to just above the stars");
        float y0 = p.Y;
        Tick(new Input { Strafe = 1 }, 20);
        check(p.Y > y0 + 0.8f, "strafing slides the ship across the lane");
        Tick(new Input { Strafe = 1 }, 200);
        check(p.Y < f.H - 1 - p.Radius + 0.01f, "the edge of the lane holds you in");

        // a rock dead ahead: ram it and the hull takes the blow
        p.Y = f.H / 2 + 0.5f; p.Z = 1.2f; p.Angle = 0; p.Pitch = 0;
        var rock = new Asteroid(0.9f, 1.0f, 0f) { X = p.X + 1.2f, Y = p.Y, Level = f, Bob = 0 };
        f.Things.Add(rock);
        int hp = p.Health;
        Tick(default, 20);
        check(rock.Removed && p.Health < hp, "ramming an asteroid shatters it and dents the hull");
        // or shoot it first
        var rock2 = new Asteroid(0.9f, p.Z + 0.28f - 0.45f, 0f) { X = p.X + 5f, Y = p.Y, Level = f, Bob = 0 };
        rock2.BaseZ = rock2.Z;
        f.Things.Add(rock2);
        hp = p.Health;
        for (int k = 0; k < 20 && !rock2.Removed; k++) Tick(new Input { Fire = true, Move = -1 });
        check(rock2.Removed && p.Health == hp, "the lasers blast asteroids out of your way");

        // lose the hull and you're back at the start of the lane
        g.Vars.Freeze = true;
        p.Health = 1;
        var rock3 = new Asteroid(0.9f, p.Z + 0.28f - 0.45f, 0f) { X = p.X + 0.3f, Y = p.Y, Level = f, Bob = 0 };
        rock3.BaseZ = rock3.Z;
        f.Things.Add(rock3);
        Tick(default, 2);
        check(g.Mode == GameMode.Dead, "the hull gives out");
        g.RespawnAtCheckpoint();
        check(g.Mode == GameMode.Playing && g.Level == f && MathF.Abs(p.X - f.StartX) < 0.01f && p.Z > 1f && p.Health == 100, "and you start the crossing again");
        g.Vars.Freeze = false;

        // fly to the end and you land on the moon
        var end = f.FindMark('8').Value;
        p.X = end.x - 0.6f; p.Y = end.y;
        Tick(default, 10);
        check(g.Level == g.Hub[mi] && !g.Level.Flight, "reach the end of the crossing to land on the Verdant Moon");
        // and taking off again from the moon's pad starts the crossing over
        var pad = g.Level.FindMark('8').Value;
        p.X = pad.x + 1f; p.Y = pad.y; Tick(default, 2);
        p.X = pad.x; Tick(default, 2);
        check(g.Level == f && MathF.Abs(p.X - f.StartX) < 0.2f, "the moon's landing pad launches you back into the crossing");
    }

    static void PlanetChecks(Action<bool, string> check)
    {
        var hub = Maps.BuildHub();
        int bi = Array.FindIndex(hub, l => l.RawName == "Barren World");
        var lv = hub[bi];
        check(bi == 6 && lv.Ship != null && lv.Ship.Stage == 0 && lv.ThemeId == "barren", "the Barren World has a wrecked ship");
        check(hub[0].FindMark('7') != null && lv.FindMark('7') != null && hub.Count(l => l.FindMark('7') != null) == 2, "portal 7 in Winnowing Hall's courtyard leads to the Barren World");
        for (int k = 0; k < Ship.Need.Length; k++)
        {
            int veins = lv.Cells.Count(c => Level.OreIndex(c) == k);
            check(veins >= Ship.Need[k] + 2, $"enough {Game.OreNames[k]} in the rocks ({veins} veins for {Ship.Need[k]})");
        }

        var g = new Game { FixedSeed = 6 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);
        g.Warp(bi);
        var w = g.Level;
        var p = g.P;
        w.Things.RemoveAll(t => t is Monster);
        var (ax, ay) = w.ArrivalCell();
        check(g.Level == w && w.Ship is { Built: false }, "stranded on arrival");
        // stepping back onto the dead portal goes nowhere
        p.X = ax + 1.5f; Tick(default, 2);
        p.X = ax + 0.5f; Tick(default, 2);
        check(g.Level == w, "the burnt-out portal won't take you home");

        // mine an iron vein: the ore goes into your pack
        int vein = Array.FindIndex(w.Cells, c => c == 'N');
        int vx = vein % w.W, vy = vein / w.W;
        g.HitBlock(vx, vy, 999);
        check(w.Cells[vein] == '\0' && p.Ore[0] == 1, "breaking an iron vein gives you iron ore");
        g.HitBlock(vx, vy, 999);
        check(p.Ore[0] == 1, "and only once");

        // hand it over at the ship, then everything else it needs
        var s = w.Ship;
        void FaceShip() { p.X = s.X - 1.2f; p.Y = s.Y; p.Angle = 0; p.PortalLock = true; Tick(default); }
        FaceShip();
        Tick(new Input { Use = true });
        check(s.Delivered[0] == 1 && p.Ore[0] == 0 && !s.Built, "Use hands your ore over to the ship");
        for (int k = 0; k < Ship.Need.Length; k++) p.Ore[k] = Ship.Need[k] + 1;
        Tick(default); Tick(new Input { Use = true });
        check(s.Built && s.Stage == 2 && p.Ore[0] == 2 && p.Ore[1] == 1 && p.Ore[2] == 1, "the ship takes only what it needs, and is repaired");
        Tick(default); Tick(new Input { Use = true });
        check(g.Level.Flight && MathF.Abs(p.X - g.Level.StartX) < 0.01f && p.Z > 1f, "the repaired ship takes off into the Void Crossing");
        g.Warp(0);

        // with the ship fixed, the portal works both ways
        var home = g.Hub[0].FindMark('7').Value;
        p.X = home.x + 1f; Tick(default, 2);
        p.X = home.x; p.Y = home.y; Tick(default, 2);
        check(g.Level == w, "portal 7 takes you back");
        p.X = ax + 1.5f; p.Y = ay + 0.5f; Tick(default, 2);
        p.X = ax + 0.5f; Tick(default, 2);
        check(g.Level == g.Hub[0], "and, with the ship repaired, home again");

        // art for both styles
        foreach (var style in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
        {
            g.SetArtStyle(style);
            check(Art.Ship.Length == 3 && Art.Ship.Distinct().Count() == 3 && Art.Ores.Length == 3 && Art.OreCracked.All(o => o.Length == Level.RubbleStages)
                  && Art.Dust != null && Art.Cliff != null && Art.SkyBarren != null, $"{style} has ship, ore and barren-world art");
        }
        g.SetArtStyle(ArtStyle.SciFi);
    }

    static void DigChecks(Action<bool, string> check)
    {
        var hub = Maps.BuildHub();
        int di = Array.FindIndex(hub, l => l.RawName == "Bedrock Depths");
        var lv = hub[di];
        check(di == 5 && lv.Dig && hub.Count(l => l.Dig) == 1, "the Bedrock Depths is the hub's only dig map");
        check(lv.FindMark('6') != null && hub[4].FindMark('6') != null && hub.Count(l => l.FindMark('6') != null) == 2,
              "portal 6 in the quarry's strongroom leads to the Bedrock Depths");
        int open = Enumerable.Range(0, lv.Cells.Length).Count(i => lv.Cells[i] == '\0');
        int interior = (lv.W - 2) * (lv.H - 2);
        check(open == 1 && lv.Cells.Count(c => c == Level.Rubble) == interior - 1, $"solid rock but the arrival cell ({interior - 1} blocks)");

        var g = new Game { FixedSeed = 2 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);
        g.Warp(di);
        var d = g.Level;
        var p = g.P;
        var (ax, ay) = d.ArrivalCell();
        int here = ay * d.W + ax, e = here + 1;
        void Punch(Func<bool> done)
        {
            for (int k = 0; k < 35 * 8 && !done(); k++) Tick(new Input { Fire = true });
            Tick(default, 25);
        }
        p.X = ax + 0.5f; p.Y = ay + 0.5f; p.Angle = 0; p.Pitch = 0;
        float start = d.Floors[here];
        Tick(default);
        check(g.DigTarget is (var t1x, var t1y, Level.Face.Wall, var t1s) && t1x == ax + 1 && t1y == ay && t1s == start, "the block ahead is highlighted, at your level");
        p.Pitch = 70; Tick(default);
        check(g.DigTarget is (var t2x, var t2y, Level.Face.Ceiling, _) && t2x == ax && t2y == ay, "looking up highlights the rock overhead");
        p.Pitch = 0;
        Punch(() => d.Cells[e] == '\0');
        check(d.Cells[e] == '\0' && d.Floors[e] == start && d.Heights[e] == start + 1, "punch a tunnel ahead, at your own level");
        check(!d.CanDig(ax, ay, Level.Face.Floor), "the portal's floor can't be dug away");

        // step into the tunnel and dig down, then up
        p.X = ax + 1.5f;
        Tick(default, 2);
        p.Pitch = -70;
        Tick(default);
        check(g.DigTarget is (var t3x, var t3y, Level.Face.Floor, _) && t3x == ax + 1 && t3y == ay, "looking down highlights the rock underfoot");
        Punch(() => d.Floors[e] < start);
        check(d.Floors[e] == start - Level.DigStep, "look down to dig out the rock under your feet");
        check(MathF.Abs(p.FloorZ - d.Floors[e]) < 0.01f && p.Z < 0.01f, "and you drop into the hole");
        float roof = d.Heights[e];
        p.Pitch = 70;
        Punch(() => d.Heights[e] > roof);
        check(d.Heights[e] == roof + Level.DigStep, "look up to dig into the rock overhead");

        p.Pitch = 0; p.Angle = 0;
        Punch(() => d.Cells[e + 1] == '\0');
        check(d.Floors[e + 1] == start - Level.DigStep, "tunnels dug from lower down open lower down");

        // aim a little high at the rock ahead and it opens a step up: dig a staircase and climb it
        int south = e + d.W;
        float low = p.FloorZ;
        p.Angle = MathF.PI / 2; p.Pitch = 20;
        Tick(default);
        check(g.DigTarget is (_, _, Level.Face.Wall, var up) && up == low + Level.DigStep, "aiming high marks a slot a step up");
        Punch(() => d.Cells[south] == '\0');
        check(d.Floors[south] == low + Level.DigStep && d.Heights[south] == low + Level.DigStep + 1, "it opens a step up");
        p.Pitch = 0;
        Tick(new Input { Move = 1 }, 20);
        check(MathF.Abs(p.FloorZ - (low + Level.DigStep)) < 0.01f, "and you walk up onto it");
        p.X = ax + 1.5f; p.Y = ay + 0.5f; p.Angle = 0;
        Tick(default, 25);

        // Use digs too (the way to dig in relaxed mode)
        p.Pitch = 70;
        float before = d.Heights[e];
        for (int k = 0; k < 35 * 4 && d.Heights[e] == before; k++) Tick(new Input { Use = true });
        check(d.Heights[e] == before + Level.DigStep, "Use digs upward");
        check(d.CrackStage(e, Level.Face.Ceiling) == 0, "each new layer starts whole");
        Tick(default, 25);

        // half a step down, so you can walk back up onto the portal
        p.Pitch = 0; p.Angle = MathF.PI;
        Tick(new Input { Move = 1 }, 40);
        check(g.Level == g.Hub[4], "walk up out of the hole and back onto the portal to the quarry");

        // floors stop at the bedrock and ceilings at the roof
        d.Floors[e] = 0;
        check(!d.CanDig(ax + 1, ay, Level.Face.Floor) && !d.DamageBlock(ax + 1, ay, 999, Level.Face.Floor) && d.Floors[e] == 0, "nothing to dig below the bedrock");
        d.Heights[e] = Level.MaxHeight;
        check(!d.CanDig(ax + 1, ay, Level.Face.Ceiling), "nor above the roof");

        // floors and ceilings of ordinary maps stay put
        var hall = hub[0];
        check(!hall.CanDig(3, 2, Level.Face.Floor) && !hall.CanDig(3, 2, Level.Face.Ceiling), "you can't dig through an ordinary map's floor");

        // relaxed mode buries a couple of relics in the rock
        var rg = new Game { FixedSeed = 4, Style = GameStyle.Relaxed };
        rg.NewGame(PClass.Mage);
        var rd = rg.Hub[di];
        var relics = rd.Things.OfType<Pickup>().Where(t => t.Kind == PickupKind.Relic).ToList();
        check(relics.Count == 2 && relics.All(r => new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.All(o => rd.Cell((int)r.X + o.Item1, (int)r.Y + o.Item2) == Level.Rubble)),
              "relaxed mode buries relics deep in the rock");
    }

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

    static void SoundChecks(Action<bool, string> check)
    {
        foreach (var style in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
        {
            var bad = new List<string>();
            for (int i = 0; i < (int)Sfx.Count; i++)
            {
                var a = Sounds.Make((Sfx)i, style);
                int peak = a.Max(x => Math.Abs((int)x));
                float clipped = a.Count(x => Math.Abs((int)x) >= 29990) / (float)a.Length;
                if (a.Length < 1000 || peak < 3000 || clipped > 0.05f) bad.Add($"{(Sfx)i} (len {a.Length}, peak {peak}, clipped {clipped:P0})");
            }
            check(bad.Count == 0, $"every {style} sound is audible and clean" + (bad.Count > 0 ? ": " + string.Join(", ", bad) : ""));
        }
        var same = Enumerable.Range(0, (int)Sfx.Count).Where(i => Sounds.Make((Sfx)i, ArtStyle.SciFi).SequenceEqual(Sounds.Make((Sfx)i, ArtStyle.Fantasy))).Select(i => (Sfx)i).ToList();
        check(same.Count == 0, "every sci-fi sound differs from its fantasy one" + (same.Count > 0 ? ": " + string.Join(", ", same) : ""));
        check(Sounds.Make(Sfx.Shoot, ArtStyle.SciFi).SequenceEqual(Sounds.Make(Sfx.Shoot, ArtStyle.SciFi)), "sounds synthesize the same every time");
        foreach (var style in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
        {
            var jet = Sounds.Make(Sfx.Jet, style);
            check(Math.Abs((int)jet[0]) < 1500 && Math.Abs((int)jet[^1]) < 1500, $"the {style} thrust sound fades at both ends, so it loops without clicks");
        }
        var wav = Sounds.Wav(new short[] { 1, -1 });
        check(wav.Length == 48 && wav[0] == 'R' && wav[8] == 'W' && BitConverter.ToInt32(wav, 24) == Sounds.Rate, "WAV export writes a valid header");
    }

    static void SpireChecks(Action<bool, string> check)
    {
        var hub = Maps.BuildHub();
        int si = Array.FindIndex(hub, l => l.RawName == "Windspire");
        check(si == 3, "the Windspire joins the hub after Darkmere Crypt");
        var lv = hub[si];
        var (ax, ay) = lv.ArrivalCell();
        var walk = lv.Reachable(ax, ay);
        var jump = lv.Reachable(ax, ay, move: Level.Move.Jump);
        var fly = lv.Reachable(ax, ay, move: Level.Move.Fly);
        int lever = Array.IndexOf(lv.Cells, 'L');
        int summit = lever + 1;
        check(lv.Floors[summit] == 8.5f && lv.HeightAt(lever % lv.W + 1.5f, lever / lv.W + 0.5f) == 10f, "the beacon sits 8.5 up, under a 10-unit sky");
        int raised = Enumerable.Range(0, lv.Cells.Length).Count(i => lv.Cells[i] == '\0' && lv.Floors[i] > 0);
        check(raised > 60 && Enumerable.Range(0, lv.Cells.Length).All(i => lv.Floors[i] == 0 || !jump[i]),
              $"no ledge ({raised} raised cells) can be walked or jumped onto from the ground");
        check(fly[summit] && !jump[summit] && !walk[summit], "the summit lever can only be reached by flying");
        var urn = lv.Things.OfType<Pickup>().First(p => p.Kind == PickupKind.Urn);
        check(walk[(int)urn.Y * lv.W + (int)urn.X] && lv.Cells[Array.IndexOf(lv.Cells, 'P')] == 'P', "the vault at the foot of the tower sits behind a gate");
        check(lv.Things.Any(t => t is Pickup { Kind: PickupKind.Jetpack } && walk[(int)t.Y * lv.W + (int)t.X]), "a spare jetpack waits by the arrival portal");
        var keep = hub[1];
        var k4 = keep.FindMark('4');
        check(k4 != null && lv.FindMark('4') != null && hub[0].FindMark('4') == null, "portal 4 links the Frozen Keep's vault and the Windspire");
        int keepGate = Array.IndexOf(keep.Cells, 'P'), k4i = (int)k4.Value.y * keep.W + (int)k4.Value.x;
        var (kx, ky) = keep.ArrivalCell();
        check(!keep.Reachable(kx, ky, new HashSet<int> { keepGate })[k4i] && keep.Reachable(kx, ky)[k4i], "the Windspire portal sits behind the Keep's vault gate");
        var keyMaps = hub.Where(l => l.Things.Any(t => t is Pickup { Kind: PickupKind.SteelKey })).ToList();
        check(keyMaps.Count == 1 && keyMaps[0] == lv, "the Steel Key is kept in the Windspire, and nowhere else");
        var steelKey = lv.Things.OfType<Pickup>().First(t => t.Kind == PickupKind.SteelKey);
        int ski = (int)steelKey.Y * lv.W + (int)steelKey.X;
        check(!lv.Reachable(ax, ay, new HashSet<int> { Array.IndexOf(lv.Cells, 'P') }, Level.Move.Fly)[ski], "the Steel Key is locked in the vault until the beacon lever is pulled");
        check(Level.HeightFromGlyph('k', 1) == 10f && Level.HeightFromGlyph('a', 1) == 5f && Level.GlyphFromHeight(10f) == 'k' && Level.GlyphFromHeight(3f) == '6'
              && Level.FloorFromGlyph('a') == 2.5f && Level.FloorFromGlyph('y') == 8.5f && Level.FloorFromGlyph('9') == 2.25f,
              "tall glyphs: ceilings 'a'-'k' = 5-10, floors 'a'-'z' = 2.5-8.75");
        var def = Maps.Hub[si];
        var copy = MapDoc.Parse(MapDoc.FromDef(def).Serialize()).ToDef().Build();
        check(copy.Floors.SequenceEqual(lv.Floors) && copy.Heights.SequenceEqual(lv.Heights), "the Windspire's towers survive being saved to a map file and loaded back");

        // climb it for real: portal in, grab the spare jetpack, hop ledge to ledge, pull the beacon lever, loot the vault
        var g = new Game { FixedSeed = 3 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Mage);
        g.Warp(si);
        var sp = g.Level;
        var p = g.P;
        check(sp.RawName == "Windspire" && MathF.Abs(p.X - 3.5f) < 0.01f && MathF.Abs(p.Y - 18.5f) < 0.01f, "warping in lands on the arrival portal");
        sp.Things.RemoveAll(t => t is Monster or Chest); // chests land at random, and one can sit on the flight path
        void Face(float tx, float ty) => p.Angle = MathF.Atan2(ty - p.Y, tx - p.X);
        void WalkTo(float tx, float ty)
        {
            for (int k = 0; k < 35 * 8 && Dist(tx, ty) > 0.15f; k++) { Face(tx, ty); Tick(new Input { Move = MathF.Min(1, Dist(tx, ty) * 2) }); }
        }
        float Dist(float tx, float ty) => MathF.Sqrt((tx - p.X) * (tx - p.X) + (ty - p.Y) * (ty - p.Y));
        bool FlyTo(float tx, float ty, float floor)
        {
            Tick(default, 70); // let the tank recharge
            Tick(new Input { JetHeld = true });
            for (int k = 0; k < 35 * 4 && p.FloorZ + p.Z < floor + 0.5f; k++) Tick(new Input { JetHeld = true });
            for (int k = 0; k < 35 * 8 && Dist(tx, ty) > 0.15f; k++)
            {
                Face(tx, ty);
                Tick(new Input { Move = MathF.Min(1, Dist(tx, ty) * 2), JetHeld = p.FloorZ + p.Z < floor + 0.4f });
            }
            for (int k = 0; k < 35 * 5 && p.Flying; k++) Tick(new Input { SlideHeld = true });
            return p.OnGround && MathF.Abs(p.FloorZ - floor) < 0.01f;
        }

        // without the jetpack you can't get off the ground
        WalkTo(9.9f, 16.5f); WalkTo(9.9f, 13.5f); WalkTo(15.2f, 13.5f);
        for (int k = 0; k < 10; k++) { Tick(new Input { Jump = true, JetHeld = true, Move = 1 }); Tick(new Input { JetHeld = true, Move = 1 }, 25); }
        check(!p.HasJetpack && p.FloorZ == 0f, "without a jetpack you're stuck on the ground floor");
        WalkTo(9.9f, 16.5f); WalkTo(7.5f, 19.5f);
        check(p.HasJetpack, "the spare jetpack by the portal");
        WalkTo(9.9f, 16.5f); WalkTo(9.9f, 13.5f);

        var route = new (float x, float y, float floor, string name)[]
        {
            (17.5f, 13.5f, 1.5f, "south-east ledge"), (19.5f, 7.5f, 2.5f, "east pillar"), (17.5f, 2.0f, 3.5f, "north-east ledge"),
            (10.5f, 1.5f, 4.5f, "north pillar"), (3.0f, 2.2f, 5.5f, "north-west ledge"), (1.5f, 7.5f, 6.5f, "west pillar"),
            (3.0f, 12.5f, 7.5f, "south-west ledge"), (9.5f, 9.5f, 8.5f, "summit"),
        };
        var reached = new List<string>();
        foreach (var r in route) { if (!FlyTo(r.x, r.y, r.floor)) break; reached.Add(r.name); }
        check(reached.Count == route.Length, $"fly up every ledge to the summit ({string.Join(", ", reached)})");
        WalkTo(10.5f, 9.6f); p.Angle = -MathF.PI / 2;
        Tick(new Input { Use = true });
        int gate = Array.IndexOf(sp.Cells, 'P');
        Tick(default, 35 * 2);
        check(sp.LeverPulled && sp.DoorOpen[gate] >= 1f, "pulling the beacon lever opens the vault gate far below");
        check(g.Messages.Any(m => m.text.Contains("force field")), "the sci-fi message says the force field powered down");

        // step off the summit, glide down and collect the reward
        WalkTo(12.9f, 9.6f); WalkTo(14.5f, 9.6f);
        for (int k = 0; k < 35 * 3 && !p.OnGround; k++) Tick(default);
        check(p.FloorZ == 0f && p.OnGround && p.Health == 100, "stepping off the summit drops you safely to the ground");
        // back into the entry hall, around the lift pad (which would carry you straight back up to the summit)
        WalkTo(10.5f, 13.5f); WalkTo(9.9f, 16.5f); WalkTo(9.9f, 18.5f); WalkTo(13.5f, 18.5f); WalkTo(15.5f, 18.5f); WalkTo(16.5f, 17.5f);
        check(p.Urns == 1, "the vault's Nano canister is yours");
        WalkTo(18.5f, 18.5f);
        check(p.SteelKey, "and so is the blue keycard");

        // every ledge's checkpoint lit on the way up; dying now puts you back on the summit, key and all
        check(sp.CheckpointsReached.Count == 8 && g.Checkpoint?.Floor == 8.5f, $"all 8 ledge checkpoints lit, the summit's is the one you'd return to ({sp.CheckpointsReached.Count})");
        g.DamagePlayer(500);
        check(g.Mode == GameMode.Dead && g.CanRespawn && g.Messages.Last().text.Contains("checkpoint"), "dying in the Windspire offers the checkpoint");
        Tick(default, 35);
        Tick(new Input { Confirm = true }); Tick(default);
        check(g.Mode == GameMode.Playing && p.FloorZ == 8.5f && sp.CheckpointZone[(int)p.Y * sp.W + (int)p.X] == g.Checkpoint.Index,
              "Enter respawns you on the summit's checkpoint pad");
        check(p.Health >= 50 && p.SteelKey && p.Urns == 1 && p.HasJetpack && p.Fuel == Player.FuelMax && g.Level == sp,
              "you keep the key, your items and the jetpack, with health restored and a full tank");

        // the lift pad at the foot of the tower beams you straight back up
        WalkTo(12.9f, 9.6f); WalkTo(14.5f, 9.6f);
        for (int k = 0; k < 35 * 3 && !p.OnGround; k++) Tick(default);
        WalkTo(10.5f, 13.5f); WalkTo(9.9f, 16.5f);
        for (int k = 0; k < 35 * 3 && p.FloorZ == 0; k++) { Face(11.5f, 17.5f); Tick(new Input { Move = 1 }); }
        check(p.FloorZ == 8.5f && g.Messages.Any(m => m.text.Contains("beams you up")), "stepping on the lift pad takes you back up to the summit");
    }

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
        check(g.Menu.Page == MenuPage.Courses && g.Menu.Items(MenuPage.Courses).SequenceEqual(new[] { "Velocity Hangar", "Descent", "Circuit", "Free Roam", "Back" }),
              "it lists the courses: Velocity Hangar, Descent, Circuit and Free Roam");
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
        int main = g.Menu.Items(MenuPage.Main).Length;
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
        for (int k = 0; k < 4; k++) { g.Menu.Update(new Input { Down = true }, 1f / 35f); seen.Add(g.Menu.BoardArena ? "arena" : g.Menu.BoardCourse.Id); }
        check(seen.SequenceEqual(new[] { "arena", "hangar", "descent", "circuit" }), "Up/Down step through the timed courses and the arena (Free Roam has no board)");
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

    static void HudChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        g.NewGame(PClass.Fighter);
        g.Vars.Freeze = true;
        g.Update(default, 1f / 35f);
        g.Messages.Clear();
        var r = new Renderer();
        check(g.Vars.Hud == HudStyle.Full, "the classic status bar is the default HUD");

        // what each style draws in the bottom strip where the status bar lives, and how tall the 3D view is
        var strips = new Dictionary<HudStyle, uint[]>();
        var heights = new Dictionary<HudStyle, int>();
        foreach (var style in Enum.GetValues<HudStyle>())
        {
            g.Vars.Hud = style;
            r.Render(g);
            strips[style] = r.Fb[(Renderer.StatusViewH * Renderer.W)..];
            heights[style] = r.ViewH;
        }
        var hb = Art.HudBack;
        check(heights[HudStyle.Full] == Renderer.StatusViewH && strips[HudStyle.Full][Renderer.W * 10 + 150] != 0,
              "Full keeps the 3D view above the status bar");
        check(new[] { HudStyle.Compact, HudStyle.Minimal, HudStyle.Off }.All(h => heights[h] == Renderer.H),
              "the other styles drop the status bar and give the view the whole screen");
        int Diff(uint[] a, uint[] b) => a.Zip(b).Count(t => t.First != t.Second);
        check(Diff(strips[HudStyle.Full], strips[HudStyle.Off]) > Renderer.W * 20, "with the HUD off, the floor shows where the status bar was");
        check(Diff(strips[HudStyle.Compact], strips[HudStyle.Off]) > Diff(strips[HudStyle.Minimal], strips[HudStyle.Off])
              && Diff(strips[HudStyle.Minimal], strips[HudStyle.Off]) > 40,
              "Compact shows more than Minimal, and Minimal more than Off");
        g.P.Health = 20;
        g.Vars.Hud = HudStyle.Minimal;
        r.Render(g);
        var low = r.Fb[(Renderer.StatusViewH * Renderer.W)..];
        check(Diff(low, strips[HudStyle.Minimal]) > 0, "the minimal HUD still tracks your health");
        g.Vars.Hud = HudStyle.Full;

        // switching: the H key cycles through the styles, the options menu steps either way, and it's saved
        var seen = new List<HudStyle>();
        for (int i = 0; i < 4; i++) { g.Update(new Input { CycleHud = true }, 1f / 35f); seen.Add(g.Vars.Hud); }
        check(seen.SequenceEqual(new[] { HudStyle.Compact, HudStyle.Minimal, HudStyle.Off, HudStyle.Full }), "H cycles Full, Compact, Minimal, Off and back");
        check(g.Messages.Any(m => m.Item1.Contains("HUD: OFF")), "and says which style you're on");
        check(Bindings.Find("cyclehud") is { Key1: var k } && k == Keys.Letter('H'), "cycling the HUD is a rebindable action on H");
        g.Menu.Show(MenuPage.Options);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Options), "HUD style");
        check(g.Menu.Value(g.Menu.Cursor) == "FULL", "Options shows HUD style: FULL");
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        check(g.Vars.Hud == HudStyle.Off && g.Menu.Value(g.Menu.Cursor) == "OFF", "Left steps back to OFF");
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(g.Vars.Hud == HudStyle.Compact, "Right steps forward to COMPACT");
        g.Menu.Close();
        check(Settings.Lines(g).Contains("hud 1"), "the style is saved with the settings");
        g.Con.Execute("hud 2");
        check(g.Vars.Hud == HudStyle.Minimal, "'hud 2' sets it from the console");
        g.Con.Execute("hud 9");
        check(g.Vars.Hud == HudStyle.Off, "out-of-range values clamp");
        g.Vars.Hud = HudStyle.Full;

        // the crosshair: off by default, each style marks the centre of the view, and it follows vertical look
        check(g.Vars.Crosshair == CrosshairStyle.Off, "the crosshair is off by default");
        uint[] Centre(int pitch)
        {
            g.P.Pitch = pitch;
            r.Render(g);
            var box = new List<uint>();
            int cy = Renderer.StatusViewH / 2 + pitch;
            for (int y = cy - 7; y <= cy + 7; y++)
                for (int x = Renderer.W / 2 - 7; x <= Renderer.W / 2 + 7; x++) box.Add(r.Fb[y * Renderer.W + x]);
            return box.ToArray();
        }
        var bare = Centre(0);
        var marks = new Dictionary<CrosshairStyle, uint[]>();
        foreach (var cs in new[] { CrosshairStyle.Dot, CrosshairStyle.Cross, CrosshairStyle.Circle })
        {
            g.Vars.Crosshair = cs;
            marks[cs] = Centre(0);
        }
        check(marks.Values.All(m => Diff(m, bare) > 4), "dot, cross and circle each draw at the centre of the view");
        check(Diff(marks[CrosshairStyle.Dot], marks[CrosshairStyle.Cross]) > 0 && Diff(marks[CrosshairStyle.Cross], marks[CrosshairStyle.Circle]) > 0,
              "and they look different");
        g.Vars.Crosshair = CrosshairStyle.Cross;
        var lookUp = Centre(20);
        g.Vars.Crosshair = CrosshairStyle.Off;
        check(Diff(lookUp, Centre(20)) > 4, "it moves with the horizon when you look up or down");
        g.Vars.Crosshair = CrosshairStyle.Cross;
        g.ShowMap = true;
        var onMap = Centre(0);
        g.Vars.Crosshair = CrosshairStyle.Off;
        check(Diff(onMap, Centre(0)) == 0, "it's hidden on the automap");
        g.ShowMap = false;
        g.P.Pitch = 0;

        g.Menu.Show(MenuPage.Options);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Options), "Crosshair");
        check(g.Menu.Value(g.Menu.Cursor) == "OFF", "Options shows Crosshair: OFF");
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(g.Vars.Crosshair == CrosshairStyle.Cross && g.Menu.Value(g.Menu.Cursor) == "CROSS", "Right steps through DOT to CROSS");
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        check(g.Vars.Crosshair == CrosshairStyle.Circle, "and Left wraps round to CIRCLE");
        g.Menu.Close();
        check(Settings.Lines(g).Contains("crosshair 3"), "the crosshair is saved with the settings");
        g.Con.Execute("crosshair 1");
        check(g.Vars.Crosshair == CrosshairStyle.Dot, "'crosshair 1' sets it from the console");
        g.Vars.Crosshair = CrosshairStyle.Off;
    }

    static void RpgChecks(Action<bool, string> check)
    {
        // levels and points
        var pr = new Profile();
        check(Profile.XpToNext(1) == 100 && Profile.XpToNext(2) == 282 && Profile.XpToNext(4) == 800, "the level curve: 100, 282, ... 800 XP");
        check(pr.AddXp(99) == 0 && pr.Level == 1 && pr.AddXp(1) == 1 && pr.Level == 2 && pr.Points == 1 && pr.Xp == 0, "100 XP reaches level 2 and a skill point");
        check(pr.AddXp(282 + 519) == 2 && pr.Level == 4 && pr.Points == 3, "big gains can level up more than once");
        var maxed = new Profile();
        maxed.AddXp(100_000_000);
        check(maxed.Level == Profile.MaxLevel && maxed.Xp == 0 && maxed.Points == Profile.MaxLevel - 1, $"levels stop at {Profile.MaxLevel}");
        check(pr.Spend(Skill.Power) && pr.Rank(Skill.Power) == 1 && pr.Points == 2, "a point buys a rank");
        var none = new Profile();
        check(!none.Spend(Skill.Power), "no points, no rank");
        for (int i = 0; i < 12; i++) maxed.Spend(Skill.Agility);
        check(maxed.Rank(Skill.Agility) == Profile.MaxRank && maxed.Points == Profile.MaxLevel - 1 - Profile.MaxRank, $"skills stop at rank {Profile.MaxRank}");
        check(!pr.AddWeaponXp(PClass.Fighter, 1, 59) && pr.AddWeaponXp(PClass.Fighter, 1, 1) && pr.Weapon(PClass.Fighter, 1).Level == 2
              && MathF.Abs(pr.WeaponMult(PClass.Fighter, 1) - 1.08f) < 0.001f && pr.Weapon(PClass.Mage, 1).Level == 1,
              "each weapon levels up on its own, for +8% damage a level");

        // saving
        var path = Path.Combine(Path.GetTempPath(), $"hexen_profile_{Environment.ProcessId}.json");
        pr.Save(path);
        var loaded = Profile.Load(path);
        check(loaded.ToJson() == pr.ToJson(), "the profile saves and loads back exactly");
        File.WriteAllText(path, "{ not json");
        check(Profile.Load(path).Level == 1, "a damaged profile file starts fresh instead of crashing");
        File.Delete(path);
        check(Profile.Load(path).Level == 1 && Profile.Load(null).Level == 1, "no file means a fresh profile");

        // experience from play
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        check(g.ProfilePath == null && g.Profile.Level == 1, "tests keep the profile in memory");
        g.NewGame(PClass.Fighter);
        var ettin = g.Level.Things.OfType<Monster>().First(m => (int)m.X == 14 && (int)m.Y == 4);
        g.Level.Things.RemoveAll(t => t is Monster && t != ettin);
        g.P.X = 12.8f; g.P.Y = 4.5f; g.P.Angle = 0;
        g.Vars.God = true;
        for (int k = 0; k < 35 * 20 && ettin.Alive; k++) Tick(new Input { Fire = true });
        int killXp = Game.Xp.Kill(Monster.Ettin);
        check(!ettin.Alive && g.Profile.TotalXp == killXp && g.RunXp == killXp && g.Profile.TotalKills == 1, $"killing an Ettin gives {killXp} XP");
        check(g.Profile.Weapon(PClass.Fighter, 0).Xp == killXp, "and the same to the weapon that did it");
        check(g.XpPopup == killXp && g.XpPopupTime > 0, "a +XP pop-up shows by the level bar");
        g.GainXp(Profile.XpToNext(1));
        check(g.Profile.Level == 2 && g.Messages.Any(m => m.text.StartsWith("Level up! You are level 2") && m.text.Contains("Press K")), "levelling up says so, and names the key");

        var stone = g.Level.Things.OfType<LoreStone>().First();
        int before = g.Profile.TotalXp;
        g.P.X = stone.X - 0.8f; g.P.Y = stone.Y; g.P.Angle = 0;
        if (g.Level.BlocksCircle(g.P.X, g.P.Y, g.P.Radius)) { g.P.X = stone.X + 0.8f; g.P.Angle = MathF.PI; }
        if (g.Level.BlocksCircle(g.P.X, g.P.Y, g.P.Radius)) { g.P.X = stone.X; g.P.Y = stone.Y + 0.8f; g.P.Angle = -MathF.PI / 2; }
        Tick(new Input { Use = true });
        check(g.Profile.TotalXp == before + Game.Xp.Lore && g.ReadingLore != null, $"reading a lore stone gives {Game.Xp.Lore} XP");
        g.ReadingLore = null;
        Tick(new Input { Use = true }); g.ReadingLore = null;
        check(g.Profile.TotalXp == before + Game.Xp.Lore, "but only the first time");

        // skills change how you play
        var p = g.P;
        g.Profile.Points = 20;
        check(g.SpendSkill(Skill.Vitality) && p.MaxHealth == 110, "Vitality: +10 max health");
        p.Health = 100; p.Flasks = 1;
        g.Con.Execute("give health");
        check(p.Health == 110, "healing fills the bigger health bar");
        p.Health = 90; p.Flasks = 1;
        Tick(new Input { UseItem = true });
        check(p.Health == 110 && p.Flasks == 0, "a flask can heal past 100");

        float Walk()
        {
            var w = new Game { FixedSeed = 1, Profile = g.Profile };
            w.NewGame(PClass.Fighter);
            w.Level.Things.RemoveAll(t => t is Monster);
            w.P.X = 10.5f; w.P.Y = 8.5f; w.P.Angle = 0;
            for (int k = 0; k < 20; k++) w.Update(new Input { Move = 1 }, 1f / 35f);
            return w.P.X - 10.5f;
        }
        float slow = Walk();
        for (int i = 0; i < 5; i++) g.SpendSkill(Skill.Agility);
        float fast = Walk();
        check(MathF.Abs(fast / slow - 1.2f) < 0.02f, $"Agility: rank 5 walks 20% faster ({fast / slow:0.00}x)");

        var mage = new Game { FixedSeed = 1, Profile = new Profile() };
        mage.NewGame(PClass.Mage);
        mage.Level.Things.RemoveAll(t => t is Monster);
        int Shot()
        {
            mage.Level.Things.RemoveAll(t => t is Projectile);
            mage.P.Cooldown = 0;
            mage.Update(new Input { Fire = true }, 1f / 35f);
            return mage.Level.Things.OfType<Projectile>().First().DmgMax;
        }
        int wand = Shot();
        mage.Profile.Points = 20;
        for (int i = 0; i < 5; i++) mage.SpendSkill(Skill.Power);
        check(wand == 13 && Shot() == 18, "Power: rank 5 hits 40% harder");
        mage.Profile.AddWeaponXp(PClass.Mage, 0, 60);
        check(Shot() == 20 && mage.Level.Things.OfType<Projectile>().First().Slot == 0, "a levelled-up weapon hits harder still, and its shots remember it");

        var focus = new Game { FixedSeed = 1, Profile = new Profile { Points = 20 } };
        focus.NewGame(PClass.Mage);
        focus.Level.Things.RemoveAll(t => t is Monster);
        focus.P.HasWeapon[1] = true; focus.P.Weapon = 1; focus.P.BlueMana = 100;
        (int, float) Shards()
        {
            int mana = focus.P.BlueMana;
            focus.P.Cooldown = 0;
            focus.Update(new Input { Fire = true }, 1f / 35f);
            return (mana - focus.P.BlueMana, focus.P.Cooldown);
        }
        var (costBefore, cdBefore) = Shards();
        for (int i = 0; i < 10; i++) focus.SpendSkill(Skill.Focus);
        var (costAfter, cdAfter) = Shards();
        check(costBefore == 3 && costAfter == 2 && cdAfter < cdBefore * 0.7f, $"Focus: cheaper, faster shots (mana {costBefore} -> {costAfter}, cooldown {cdBefore:0.00} -> {cdAfter:0.00})");

        var jet = new Game { FixedSeed = 1, Profile = new Profile { Points = 5 } };
        jet.NewGame(PClass.Fighter);
        jet.Con.Execute("give jetpack");
        jet.SpendSkill(Skill.Thrusters); jet.SpendSkill(Skill.Thrusters);
        check(MathF.Abs(jet.P.MaxFuel - Player.FuelMax * 1.3f) < 0.01f && MathF.Abs(jet.P.Fuel - jet.P.MaxFuel) < 0.01f, "Thrusters: a bigger tank, topped up");
        var jet2 = new Game { FixedSeed = 1, Profile = jet.Profile };
        jet2.NewGame(PClass.Fighter);
        check(MathF.Abs(jet2.P.MaxFuel - Player.FuelMax * 1.3f) < 0.01f && jet2.P.MaxHealth == 100, "skills carry into the next game");

        // the character screen
        var ui = new Game { FixedSeed = 1, Profile = new Profile() };
        ui.NewGame(PClass.Cleric);
        ui.Profile.AddXp(100);
        check(ui.Binds.Get(Act.Character, 0) == Keys.Letter('K'), "K is the character key");
        ui.Update(new Input { Character = true }, 1f / 35f);
        check(ui.Paused && ui.Menu.Page == MenuPage.Character, "K opens the character screen and pauses");
        check(ui.Menu.Items(MenuPage.Character).SequenceEqual(new[] { "Vitality", "Power", "Agility", "Focus", "Thrusters", "Back" }), "it lists the five skills");
        ui.Update(new Input { Confirm = true }, 1f / 35f);
        check(ui.Profile.Rank(Skill.Vitality) == 1 && ui.P.MaxHealth == 110 && ui.Profile.Points == 0, "Enter spends a point on the selected skill");
        ui.Update(new Input { Confirm = true }, 1f / 35f);
        check(ui.Profile.Rank(Skill.Vitality) == 1 && ui.Menu.Notice.StartsWith("No skill points"), "without points it says how to get more");
        ui.Update(new Input { Character = true }, 1f / 35f);
        check(!ui.Paused && !ui.Menu.Open, "K again closes it and resumes");
        ui.Update(new Input { Pause = true }, 1f / 35f);
        check(ui.Menu.Items(MenuPage.Pause)[1] == "Character" && ui.Menu.Items(MenuPage.Main).Contains("Character"), "the pause and title menus have Character too");
        ui.Menu.Close(); ui.Paused = false;

        // no experience from play-testing custom maps; a win pays out and counts
        var test = new Game { FixedSeed = 1, Profile = new Profile() };
        var doc = new MapDoc(10, 8); doc[2, 2] = '@'; doc[6, 5] = 'E';
        test.StartTest(doc.ToDef(), PClass.Fighter);
        test.GainXp(500);
        check(test.Profile.TotalXp == 0, "play-testing a custom map earns no experience");
        var win = new Game { FixedSeed = 1, Profile = new Profile() };
        win.NewGame(PClass.Fighter);
        win.Level.BossDead = true;
        var exit = win.Level.FindMark('E').Value;
        win.P.X = exit.x; win.P.Y = exit.y;
        win.Update(default, 1f / 35f);
        check(win.Mode == GameMode.Victory && win.Profile.Wins == 1 && win.Profile.TotalXp == Game.Xp.Victory, $"winning adds a win and {Game.Xp.Victory} XP");

        // console
        var c = new Game { FixedSeed = 1, Profile = new Profile() };
        c.NewGame(PClass.Mage);
        c.Con.Execute("xp 500");
        check(c.Profile.Level == 3 && c.Profile.Points == 2, "'xp 500' levels you up");
        c.Con.Execute("skill pow");
        check(c.Profile.Rank(Skill.Power) == 1, "'skill pow' spends a point on Power");
        c.Con.Execute("profile reset");
        check(c.Profile.Level == 1 && c.Profile.Rank(Skill.Power) == 0 && c.Profile.TotalXp == 0, "'profile reset' starts over");
    }

    static void RenderedArtChecks(Action<bool, string> check)
    {
        // the PNG reader round-trips what the screenshot writer saves
        var tmp = Path.Combine(Path.GetTempPath(), $"hexen_png_{Environment.ProcessId}.png");
        var src = new uint[37 * 23];
        for (int i = 0; i < src.Length; i++) src[i] = Col.Rgb(i * 7 % 256, i * 13 % 256, i * 29 % 256);
        Png.Save(tmp, src, 37, 23);
        var back = Png.Load(File.ReadAllBytes(tmp));
        File.Delete(tmp);
        check(back.W == 37 && back.H == 23 && back.Px.SequenceEqual(src), "PNG reader round-trips a saved image");

        var covered = RenderedArt.Covered.ToList();
        check(RenderedArt.Available && covered.Count == 31, $"the pack covers 11 pickups, 5 textures, all 6 monsters and all 9 weapons ({covered.Count})");
        check(new[] { "afrit", "ettin", "centaur", "slaughtaur", "bishop", "heresiarch" }.All(m => covered.Contains("monsters/" + m)), "every monster has rendered frames");
        bool shapes = true;
        foreach (var (file, png) in RenderedArt.Files)
        {
            var t = Png.Load(png);
            int clear = t.Px.Count(c => Col.A(c) == 0);
            shapes &= file.StartsWith("weapons/")
                ? t.W == 128 && t.H == 80 && clear > 128 * 80 / 3 && clear < 128 * 80 - 400
                : t.W == 64 && t.H == 64 && (file.StartsWith("textures/") ? clear == 0 : clear > 400 && clear < 64 * 64 - 300);
        }
        check(shapes, "every rendered asset is the right size: 64x64 sprites cut out, textures solid, 128x80 weapon frames");

        var g = new Game { FixedSeed = 1 };
        g.NewGame(PClass.Fighter);
        var hall = g.Level;
        check(!Art.Rendered && Art.Style == ArtStyle.SciFi, "rendered art is off by default");
        var procJet = Art.Jetpack;
        var procFist = Art.Weapons[0][0];
        g.SetRenderedArt(true);
        check(Art.Jetpack.Px.SequenceEqual(RenderedArt.Load("sprites/jetpack").Px) && !Art.Jetpack.Px.SequenceEqual(procJet.Px),
              "turning it on swaps in the rendered jetpack");
        check(hall.Theme.Walls['#'] == Art.Stone && Art.Stone.Px.SequenceEqual(RenderedArt.Load("textures/stone").Px),
              "maps pick up the rendered wall textures straight away");
        var drone = Art.Monsters["afrit"];
        check(drone.Length == 7 && drone[0].Px.SequenceEqual(RenderedArt.Load("monsters/afrit_walk0").Px) && drone[(int)Pose.Dead] != null,
              "the drone uses its rendered frames, with death frames derived from them");
        check(Art.Pillar != null && Art.Monsters["ettin"].Length == 7, "art the pack doesn't cover stays procedural");
        string[] classes = { "fighter", "cleric", "mage" };
        check(Enumerable.Range(0, 9).All(i => Art.Weapons[i].Length == 2
                  && Art.Weapons[i][0].Px.SequenceEqual(RenderedArt.Load($"weapons/{classes[i / 3]}_{i % 3}_idle").Px)
                  && Art.Weapons[i][1].Px.SequenceEqual(RenderedArt.Load($"weapons/{classes[i / 3]}_{i % 3}_fire").Px)),
              "every class's weapons use their rendered idle and firing frames");
        g.SetArtStyle(ArtStyle.Fantasy);
        check(!Art.Jetpack.Px.SequenceEqual(RenderedArt.Load("sprites/jetpack").Px)
              && !Art.Weapons[0][0].Px.SequenceEqual(RenderedArt.Load("weapons/fighter_0_idle").Px), "the fantasy style ignores the sci-fi pack");
        g.SetArtStyle(ArtStyle.SciFi);
        check(Art.Jetpack.Px.SequenceEqual(RenderedArt.Load("sprites/jetpack").Px), "and it comes back with the sci-fi style");
        check(Settings.Lines(g).Contains("renderedart 1"), "the choice is saved with the settings");

        // the options menu and console toggle it
        g.Menu.Show(MenuPage.Options);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Options), "Rendered art");
        check(g.Menu.Value(g.Menu.Cursor) == "ON", "Options shows Rendered art: ON");
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(!Art.Rendered && g.Menu.Value(g.Menu.Cursor) == "OFF" && Art.Jetpack.Px.SequenceEqual(procJet.Px)
              && Art.Weapons[0][0].Px.SequenceEqual(procFist.Px), "Left/Right in Options turns it off, weapons included");
        g.Menu.Close();
        g.Con.Execute("renderedart 1");
        check(Art.Rendered, "'renderedart 1' turns it on from the console");
        g.Con.Execute("renderedart 0");
        check(!Art.Rendered && !Art.Stone.Px.SequenceEqual(RenderedArt.Load("textures/stone").Px), "'renderedart 0' turns it off again");
    }

    static void CheckpointChecks(Action<bool, string> check)
    {
        var hub = Maps.BuildHub();
        var lv = hub.First(l => l.RawName == "Windspire");
        var padFloors = lv.Checkpoints.Select(c => lv.Floors[c]).OrderBy(f => f).ToList();
        check(padFloors.SequenceEqual(new[] { 1.5f, 2.5f, 3.5f, 4.5f, 5.5f, 6.5f, 7.5f, 8.5f }), "the Windspire has one checkpoint pad on each of its 8 ledges");
        int shaftLedgeCells = 0, covered = 0;
        for (int y = 1; y <= 14; y++)
            for (int x = 1; x <= 20; x++)
            {
                int i = y * lv.W + x;
                if (lv.Cells[i] != '\0' || lv.Floors[i] <= 0) continue;
                shaftLedgeCells++;
                if (lv.CheckpointZone[i] >= 0 && lv.Floors[lv.Checkpoints[lv.CheckpointZone[i]]] == lv.Floors[i]) covered++;
            }
        check(covered == shaftLedgeCells, $"landing anywhere on a ledge counts for its checkpoint ({covered}/{shaftLedgeCells} cells)");
        check(lv.Marks.Count(m => m == '=') == 1 && lv.Floors[Array.IndexOf(lv.Marks, '=')] == 0, "one lift pad on the ground floor");
        check(hub.Where(l => l != lv).All(l => l.Checkpoints.Count == 0), "only the Windspire has checkpoints");

        var g = new Game { FixedSeed = 5 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);
        int si = Array.FindIndex(g.Hub, l => l.RawName == "Windspire");
        g.Warp(si);
        var sp = g.Level;
        var p = g.P;
        sp.Things.RemoveAll(t => t is Monster);
        void Stand(float x, float y) { p.X = x; p.Y = y; p.FloorZ = sp.FloorAt(x, y); p.Z = 0; p.VZ = 0; p.Flying = false; Tick(default); }

        // the lift is dark until you've reached a checkpoint
        Stand(11.5f, 17.5f);
        check(p.FloorZ == 0 && g.Checkpoint == null && g.Messages.Any(m => m.text.Contains("lift pad is dark")), "the lift pad does nothing before you reach a checkpoint");
        Stand(12.5f, 18.5f);

        Stand(17.5f, 2.5f);
        check(g.Checkpoint?.Floor == 3.5f && g.Messages.Last().text.StartsWith("Checkpoint reached (1 of 8)"), "landing on a ledge sets a checkpoint");
        Stand(18.5f, 12.5f);
        check(g.Checkpoint.Floor == 3.5f && sp.CheckpointsReached.Count == 2, "dropping to a lower ledge lights it but keeps the higher checkpoint");
        Stand(4.5f, 13.5f);
        check(g.Checkpoint.Floor == 7.5f, "a higher ledge moves the checkpoint up");

        // dying elsewhere in the hub is a normal restart
        g.Warp(0);
        check(!g.CanRespawn, "the Windspire's checkpoint only applies inside the Windspire");
        g.DamagePlayer(500);
        Tick(default, 35); Tick(new Input { Confirm = true });
        check(g.Mode == GameMode.Playing && g.Level == g.Hub[0] && g.Checkpoint == null && g.Hub[si].CheckpointsReached.Count == 0,
              "dying outside it restarts the game, clearing checkpoints");
    }

    static void VerticalAimChecks(Action<bool, string> check)
    {
        // shots climb and dive to meet targets above and below, so fights between ledges work
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Mage);
        int si = Array.FindIndex(g.Hub, l => l.RawName == "Windspire");
        g.Warp(si);
        var lv = g.Level;
        var p = g.P;
        lv.Things.RemoveAll(t => t is Monster or LoreStone or Pickup);
        // you at the east edge of the summit, an Afrit hovering over the ground to the east
        p.X = 12.7f; p.Y = 9.5f; p.FloorZ = 8.5f; p.Angle = 0;
        var target = new Monster(Monster.Afrit) { X = 17.5f, Y = 9.5f, Level = lv };
        lv.Things.Add(target);
        g.Vars.Freeze = true;
        int hp = target.Health;
        for (int k = 0; k < 35 * 3 && target.Health == hp; k++) Tick(new Input { Fire = true });
        check(target.Health < hp, "shooting down from the summit auto-aims at a monster far below");
        lv.Things.Remove(target);

        // an Afrit on the ground fires up at you as you hover
        p.X = 6.5f; p.Y = 9.5f; p.FloorZ = 0; p.HasJetpack = true; p.Fuel = Player.FuelMax;
        g.Vars.InfiniteFuel = true;
        Tick(new Input { JetHeld = true });
        for (int k = 0; k < 35 * 2; k++) Tick(new Input { JetHeld = true });
        float alt = p.Z;
        var shooter = new Monster(Monster.Afrit) { X = 12.5f, Y = 12.5f, Level = lv };
        lv.Things.Add(shooter);
        g.Vars.Freeze = false; g.Vars.God = true;
        int hurt = 0;
        g.PlaySound = (s, _) => { if (s == Sfx.PlayerPain) hurt++; };
        var shots = new List<Projectile>();
        for (int k = 0; k < 35 * 8 && hurt == 0; k++)
        {
            Tick(default);
            shots.AddRange(lv.Things.OfType<Projectile>().Where(pr => !pr.FromPlayer && !shots.Contains(pr)));
        }
        check(alt > 2f && shots.Count > 0 && shots.All(s => s.Aimed && s.VZ > 0), $"monsters aim their missiles up at you while you fly ({alt:0.0} up, {shots.Count} shots)");
        check(hurt > 0 || shots.Any(s => s.Removed), "and those missiles can reach you up there");
        g.Vars.God = false; g.Vars.InfiniteFuel = false;
    }

    static void JetpackChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        var sfx = new List<Sfx>();
        g.PlaySound = (s, _) => sfx.Add(s);
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster);
        var p = g.P;
        check(!p.HasJetpack, "you start without a jetpack");
        var pack = g.Level.Things.OfType<Pickup>().FirstOrDefault(t => t.Kind == PickupKind.Jetpack);
        check(pack != null && MathF.Abs(pack.X - p.X) + MathF.Abs(pack.Y - p.Y) < 4, "a jetpack waits near the start of the Hab Ring");

        // without it, the jetpack key does nothing and Jump is just a jump
        float apex = 0;
        Tick(new Input { Jump = true, JumpHeld = true, JetHeld = true });
        for (int k = 0; k < 35 * 2; k++) { Tick(new Input { JumpHeld = true, JetHeld = true }); apex = MathF.Max(apex, p.Z); }
        check(!p.Flying && apex < 0.6f && p.OnGround, $"without a jetpack you only hop (apex {apex:0.00})");

        // pick it up
        p.X = pack.X - 0.6f; p.Y = pack.Y; p.Angle = 0;
        Tick(new Input { Move = 1 }, 20);
        check(p.HasJetpack && p.Fuel == Player.FuelMax && pack.Removed, "walking over the jetpack equips it with a full tank");
        check(g.Messages.Any(m => m.text.StartsWith("Jetpack!")), "the sci-fi pickup message names the jetpack");

        // take off in the great hall (ceiling 3 units up)
        g.Level.Things.RemoveAll(t => t is Decor or Chest or LoreStone);
        p.X = 12.5f; p.Y = 6.5f; p.Angle = MathF.PI / 2; p.FloorZ = g.Level.FloorAt(p.X, p.Y);
        sfx.Clear();
        Tick(new Input { JetHeld = true });
        Tick(new Input { JetHeld = true }, 35);
        check(p.Flying && p.Z > 1.2f, $"holding the jetpack key takes off from the floor and climbs (height {p.Z:0.00})");
        check(sfx.Contains(Sfx.JetStart) && sfx.Count(s => s == Sfx.Jet) >= 5, "the jetpack ignites and roars while it burns");
        Tick(new Input { JetHeld = true }, 35 * 2);
        float top = g.Level.HeightAt(p.X, p.Y) - p.FloorZ;
        check(p.Z + Player.Height < top && p.Z > top - 0.9f, $"you rise until your head nears the ceiling ({p.Z + Player.Height:0.00} of {top:0.00})");

        // hover, then sink with Slide
        float fuel = p.Fuel, z0 = p.Z;
        p.Z -= 0.8f; z0 = p.Z; p.VZ = 0;
        Tick(default, 35);
        check(p.Flying && MathF.Abs(p.Z - z0) < 0.15f && p.Fuel < fuel, $"letting go hovers in place, burning fuel ({z0:0.00} -> {p.Z:0.00})");
        for (int k = 0; k < 35 * 3 && p.Flying; k++) Tick(new Input { SlideHeld = true });
        check(!p.Flying && p.OnGround && p.SlideTime <= 0, "holding Slide sinks you gently back to the floor");

        // fly up onto a ledge far too tall to jump onto
        var lv = g.Level;
        foreach (var (cx, cy) in new[] { (12, 9), (13, 9), (12, 10), (13, 10) }) lv.Floors[cy * lv.W + cx] = 1.5f;
        p.X = 12.5f; p.Y = 7.5f; p.Angle = MathF.PI / 2; p.FloorZ = 0; p.Fuel = Player.FuelMax;
        Tick(new Input { Move = 1 }, 35);
        check(p.Y < 8.8f && p.FloorZ == 0, "a 1.5-unit ledge blocks you on foot");
        Tick(new Input { JetHeld = true });
        Tick(new Input { JetHeld = true }, 35);
        Tick(new Input { Move = 1 }, 30);
        for (int k = 0; k < 35 * 3 && p.Flying; k++) Tick(new Input { SlideHeld = true });
        check(p.FloorZ == 1.5f && p.OnGround && !p.Flying, $"with the jetpack you fly up and land on the ledge (floor {p.FloorZ})");

        // running dry drops you; the tank refills on the ground
        p.X = 12.5f; p.Y = 6.5f; p.FloorZ = 0; p.Z = 0; p.Fuel = 0.5f;
        sfx.Clear();
        Tick(new Input { JetHeld = true });
        bool ranDry = false;
        for (int k = 0; k < 35 * 3; k++) { Tick(new Input { JetHeld = true }); ranDry |= p.Fuel == 0 && !p.Flying && !p.OnGround; }
        check(ranDry && p.OnGround && sfx.Contains(Sfx.JetOut), "when the fuel runs out the jetpack sputters and you fall");
        check(p.Fuel > 0.25f && !p.Flying, "and it stays off while you keep holding the key, though the tank refills");
        Tick(default);
        Tick(new Input { JetHeld = true });
        check(p.Flying, "let go and press it again to relight it");
        for (int k = 0; k < 35 * 3 && p.Flying; k++) Tick(new Input { SlideHeld = true });
        Tick(default, 35 * 2);
        check(p.Fuel > 2.5f && p.Fuel <= Player.FuelMax, $"the tank recharges on the ground ({p.Fuel:0.0})");
        g.Vars.InfiniteFuel = true; p.Fuel = 0;
        Tick(new Input { JetHeld = true });
        Tick(new Input { JetHeld = true }, 35);
        check(p.Flying && p.Fuel == 0, "'infinitefuel' lets you fly on an empty tank");
        g.Vars.InfiniteFuel = false;

        // console and cheat
        var g2 = new Game { FixedSeed = 2 };
        g2.NewGame(PClass.Mage);
        g2.Con.Execute("give jetpack");
        check(g2.P.HasJetpack && g2.P.Fuel == Player.FuelMax, "'give jetpack' hands you a full jetpack");
        var g3 = new Game { FixedSeed = 2 };
        g3.NewGame(PClass.Cleric);
        foreach (char c in "icarus") g3.Con.FeedCheat(c);
        check(g3.P.HasJetpack, "the 'icarus' cheat gives the jetpack");
        check(MapDoc.IsKnownGlyph('J') && ThingFactory.Create('J', 1, 1) is Pickup { Kind: PickupKind.Jetpack }, "maps can place jetpacks ('J')");

        // fantasy style calls it the Wings of Wrath
        g.SetArtStyle(ArtStyle.Fantasy);
        check(Words.T("Wings of Wrath") == "Wings of Wrath" && Art.Jetpack != null, "fantasy style keeps the Wings of Wrath");
        var wings = Art.Jetpack;
        g.SetArtStyle(ArtStyle.SciFi);
        check(Words.T("Wings of Wrath") == "Jetpack" && Art.Jetpack != wings, "sci-fi style has its own jetpack sprite and name");
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
        var spire = g.Hub.First(l => l.RawName == "Windspire");
        var key = spire.Things.OfType<Pickup>().First(p => p.Kind == PickupKind.SteelKey);
        g.Level = spire; g.P.X = key.X; g.P.Y = key.Y;
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

        // the Chaos Arena has moved to its own title menu item: no portal 3, and no arena, in the hub
        check(g.Hub.All(l => l.FindMark('3') == null) && g.Hub.All(l => l.Arena == null && l.RawName != "Chaos Arena"),
              "the Chaos Arena isn't in the hub any more (it's Arena on the title menu)");
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
        check(g.Level == g.Hub[3], "typing 'visit4' warps to the fourth map");
        foreach (char c in "mapsco") Tick(new Input { Typed = c.ToString() });
        check(g.Level.Seen.All(s => s), "typing 'mapsco' reveals the map");
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
        check(g.Mode == GameMode.ClassSelect && g.PendingArena, "and asks for a class");
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

    static void BishopChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Mage);
        g.Warp(2);
        check(g.Level.Things.OfType<Monster>().Count(m => m.Def == Monster.Bishop) == 2, "Darkmere Crypt has two Dark Bishops");
        check(g.Hub[1].Things.OfType<Monster>().Any(m => m.Def == Monster.Bishop), "a Dark Bishop haunts the Frozen Keep");
        g.Level.Things.RemoveAll(t => t is Monster);
        float px = 13.5f, py = 3.5f;

        // a homing missile fired sideways curves round and hits; a plain one flies straight past
        (bool hit, float curve) Fire(float homing)
        {
            g.P.X = px; g.P.Y = py; g.P.Health = 100; g.P.Armor = 0;
            var pr = new Projectile { Kind = ProjKind.Seeker, DmgMin = 5, DmgMax = 5, X = px + 2.5f, Y = py, Z = 0.3f, VX = 0, VY = 4.8f, Homing = homing, Level = g.Level };
            g.Level.Things.Add(pr);
            float minX = pr.X;
            for (int k = 0; k < 70 && !pr.Removed; k++) { Tick(default); minX = MathF.Min(minX, pr.X); }
            pr.Removed = true;
            return (g.P.Health < 100, px + 2.5f - minX);
        }
        var (hitStraight, _) = Fire(0f);
        var (hitHoming, curve) = Fire(1.9f);
        check(!hitStraight, "an unguided missile fired sideways misses");
        check(hitHoming && curve > 1f, $"a homing missile curves toward you and hits (curved {curve:0.0} units)");

        // ...but its height only follows you slowly, so a well-timed jump lets it pass underneath
        {
            g.P.X = px; g.P.Y = py; g.P.Health = 100; g.P.Armor = 0;
            var pr = new Projectile { Kind = ProjKind.Seeker, DmgMin = 5, DmgMax = 5, X = px + 4f, Y = py, Z = 0.3f, VX = -4.8f, VY = 0, Homing = 1.9f, Life = 4.5f, Level = g.Level };
            g.Level.Things.Add(pr);
            for (int k = 0; k < 70 && !pr.Removed; k++)
            {
                bool jump = Game.Dist(pr.X, pr.Y, g.P.X, g.P.Y) < 1.4f && g.P.OnGround && g.P.VZ == 0;
                Tick(new Input { Jump = jump });
            }
            pr.Removed = true;
            Tick(default, 35);
            check(g.P.Health == 100, "a well-timed jump dodges a homing missile");
        }

        // blur: untouchable while see-through, hittable afterwards
        var b = new Monster(Monster.Bishop) { X = px + 3f, Y = py, Level = g.Level, State = AiState.Chase };
        g.Level.Things.Add(b);
        g.Vars.Freeze = true;
        b.BlurTime = 10f;
        check(b.Alpha < 256, "a blurring bishop is drawn see-through");
        g.P.X = px; g.P.Y = py; g.P.Angle = 0;
        Tick(new Input { Fire = true }); Tick(default, 20);
        check(b.Health == b.Def.Health, "attacks pass through a blurring bishop");
        b.BlurTime = 0;
        for (int k = 0; k < 35 * 2 && b.Health == b.Def.Health; k++) Tick(new Input { Fire = true });
        check(b.Health < b.Def.Health, "a solid bishop can be hurt");
        g.Vars.Freeze = false;

        // awake bishops blur on their own and fire homing missiles
        g.Level.Things.RemoveAll(t => t is Monster or Projectile);
        g.Vars.God = true;
        var b2 = new Monster(Monster.Bishop) { X = px + 4f, Y = py, Level = g.Level };
        g.Level.Things.Add(b2);
        bool blurred = false, seekers = false;
        for (int k = 0; k < 35 * 20; k++)
        {
            Tick(default);
            blurred |= b2.Blurring;
            seekers |= g.Level.Things.OfType<Projectile>().Any(p => p.Kind == ProjKind.Seeker && p.Homing > 0);
        }
        check(blurred, "a bishop blurs during a fight");
        check(seekers, "a bishop fires homing missiles");

        var r = new Random(2);
        check(Enumerable.Range(0, 50).All(_ => !ArenaState.Compose(3, r).Contains(Monster.Bishop)), "no bishops before arena wave 4");
        check(Enumerable.Range(0, 50).Any(_ => ArenaState.Compose(6, r).Contains(Monster.Bishop)), "bishops join the arena from wave 4");
    }

    /// <summary>Scripted keyboard for tests: keys held down, and keys pressed this frame.</summary>
    sealed class FakeKeys : IKeySource
    {
        public readonly HashSet<int> Held = new(), Hit = new();
        public bool Down(int code) => code != Keys.None && Held.Contains(code);
        public bool Pressed(int code) => code != Keys.None && Hit.Contains(code);
    }

    static void OptionsChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        var keys = new FakeKeys();
        // press one key for a frame, the way the real host reports it
        void Press(int code, string typed = null)
        {
            keys.Hit.Add(code);
            var inp = g.Binds.Read(keys, g.Con.Open);
            inp.KeyPressed = code; inp.Typed = typed;
            g.Update(inp, 1f / 35f);
            keys.Hit.Clear();
            g.Update(g.Binds.Read(keys, g.Con.Open), 1f / 35f);
        }
        Input Read() => g.Binds.Read(keys, false);

        // defaults
        keys.Held.Add(Keys.Letter('W')); keys.Held.Add(Keys.Mouse1);
        var r = Read();
        check(r.Move == 1 && r.Fire, "default W moves forward, left mouse attacks");
        keys.Held.Clear();
        keys.Hit.Add(Keys.WheelDown); check(Read().Cycle == 1, "mouse wheel cycles weapons"); keys.Hit.Clear();
        keys.Hit.Add(Keys.Space); check(Read().Jump, "Space jumps"); keys.Hit.Clear();

        // title menu: New game / Practice / Arena / Leaderboard / Character / Options / Quit
        check(g.Menu.Page == MenuPage.Main, "title shows the main menu");
        for (int k = 0; k < 5; k++) Press(Keys.Down);
        Press(Keys.Enter);
        check(g.Menu.Page == MenuPage.Options, "main menu opens Options");
        Press(Keys.Escape);
        check(g.Menu.Page == MenuPage.Main && g.Menu.Cursor == 5, "Esc goes back to the main menu");
        for (int k = 0; k < 5; k++) Press(Keys.Up);
        Press(Keys.Enter);
        check(g.Menu.Page == MenuPage.Style, "New game asks for a play style");
        Press(Keys.Enter);
        check(g.Mode == GameMode.ClassSelect && g.Style == GameStyle.Classic, "Classic goes to class select");
        Press(Keys.Enter);
        check(g.Mode == GameMode.Playing, "choosing a class starts the game");

        // Esc in game: pause menu -> Options -> Key bindings
        Press(Keys.Escape);
        check(g.Paused && g.Menu.Page == MenuPage.Pause, "Esc during play opens the pause menu");
        Press(Keys.Down); Press(Keys.Down); Press(Keys.Enter);
        check(g.Menu.Page == MenuPage.Options, "pause menu opens Options");
        Press(Keys.Enter);
        check(g.Menu.Page == MenuPage.Bindings, "Options opens Key bindings");

        // rebind Jump (primary) to J
        int jump = (int)Act.Jump;
        for (int k = 0; k < jump; k++) Press(Keys.Down);
        check(g.Menu.Cursor == jump, "cursor on Jump");
        Press(Keys.Enter);
        check(g.Menu.Capturing, "Enter waits for a new key");
        Press(Keys.Letter('J'));
        check(!g.Menu.Capturing && g.Binds.Get(Act.Jump, 0) == Keys.Letter('J'), "pressing J binds Jump to J");
        keys.Hit.Add(Keys.Letter('J')); check(Read().Jump, "J now jumps"); keys.Hit.Clear();
        keys.Hit.Add(Keys.Space); check(!Read().Jump, "Space no longer jumps"); keys.Hit.Clear();

        // second slot, conflicts and reserved keys
        Press(Keys.Right); Press(Keys.Enter); Press(Keys.Letter('E'));
        check(g.Binds.Get(Act.Jump, 1) == Keys.Letter('E') && g.Binds.Get(Act.Use, 0) == Keys.None, "binding E to Jump takes it off Use");
        check(g.Menu.Notice.Contains("removed from Use"), "the conflict is reported");
        Press(Keys.Enter); Press(Keys.Escape);
        check(!g.Menu.Capturing && g.Binds.Get(Act.Jump, 1) == Keys.Letter('E'), "Esc cancels capture without changing the key");
        Press(Keys.Backspace);
        check(g.Binds.Get(Act.Jump, 1) == Keys.None, "Backspace clears a slot");
        Press(Keys.Enter); Press(Keys.Mouse2);
        check(g.Binds.Get(Act.Jump, 1) == Keys.Mouse2, "mouse buttons can be bound");
        Press(Keys.Enter); Press(Keys.Escape); Press(Keys.Enter); Press(Keys.Escape);
        check(Keys.Reserved(Keys.Escape) && g.Binds.Get(Act.Jump, 1) == Keys.Mouse2, "Escape can't be bound (it cancels instead)");

        // settings file round trip
        string path = Path.Combine(Path.GetTempPath(), $"hexensharp-test-{Environment.ProcessId}.cfg");
        g.ConfigPath = path;
        g.Vars.Sens = 1f;
        Press(Keys.Escape); // back to Options (saves)
        Press(Keys.Down); Press(Keys.Right); Press(Keys.Right);
        check(MathF.Abs(g.Vars.Sens - 1.2f) < 0.001f, "Right raises mouse sensitivity");
        Press(Keys.Down); Press(Keys.Enter);
        check(g.Vars.InvertMouse, "Enter toggles invert mouse");
        Press(Keys.Escape); // back to pause menu (saves)
        check(File.Exists(path), "leaving Options saves the settings file");
        var g2 = new Game { ConfigPath = path };
        g2.LoadSettings();
        check(g2.Binds.Get(Act.Jump, 0) == Keys.Letter('J') && g2.Binds.Get(Act.Jump, 1) == Keys.Mouse2
              && g2.Binds.Get(Act.Use, 0) == Keys.None, "bindings survive a restart");
        check(MathF.Abs(g2.Vars.Sens - 1.2f) < 0.001f && g2.Vars.InvertMouse, "options survive a restart");
        check(g2.Con.Log.Count == 1, "loading settings is silent in the console");
        File.Delete(path);

        // invert mouse flips looking up/down
        Press(Keys.Escape);
        check(!g.Paused && !g.Menu.Open, "Esc on the pause menu resumes");
        g.P.Pitch = 0;
        g.Update(new Input { LookY = 10 }, 1f / 35f);
        check(g.P.Pitch > 0, "with invert on, moving the mouse down looks up");
        g.Vars.InvertMouse = false; g.P.Pitch = 0;
        g.Update(new Input { LookY = 10 }, 1f / 35f);
        check(g.P.Pitch < 0, "with invert off, moving the mouse down looks down");

        // reset and console binding
        g.Con.Execute("bind use e f");
        check(g.Binds.Get(Act.Use, 0) == Keys.Letter('E') && g.Binds.Get(Act.Use, 1) == Keys.Letter('F')
              && g.Binds.Get(Act.UseItem, 0) == Keys.None, "console 'bind use e f' (moving F off Use item)");
        g.Con.Execute("bind jump escape");
        check(g.Binds.Get(Act.Jump, 0) == Keys.Letter('J'), "console refuses to bind Escape");
        g.Con.Execute("binddefaults");
        check(g.Binds.Get(Act.Jump, 0) == Keys.Space && g.Binds.Get(Act.UseItem, 0) == Keys.Letter('F'), "'binddefaults' restores the defaults");
        check(Bindings.All.All(b => Keys.Known(b.Key1)) && Bindings.All.Select(b => b.Id).Distinct().Count() == Bindings.Count,
              "every action has a named default key and a unique id");

        // pause menu: quit to title, then Quit from the main menu
        Press(Keys.Escape);
        for (int k = 0; k < 4; k++) Press(Keys.Down);
        Press(Keys.Enter);
        check(g.Mode == GameMode.Title && g.Menu.Page == MenuPage.Main, "Quit to title");
        Press(Keys.Up); Press(Keys.Enter);
        check(g.QuitRequested, "Quit exits");
    }

    static void RelaxedChecks(Action<bool, string> check)
    {
        // choosing Relaxed from the menu
        var mg = new Game { FixedSeed = 1 };
        mg.Update(new Input { Confirm = true }, 1f / 35f);                 // New game
        mg.Update(new Input { Down = true }, 1f / 35f);                    // -> Relaxed
        mg.Update(new Input { Confirm = true }, 1f / 35f);
        check(mg.Style == GameStyle.Relaxed && mg.Mode == GameMode.ClassSelect, "menu: New game -> Relaxed -> class select");
        mg.Update(new Input { Confirm = true }, 1f / 35f);
        check(mg.Mode == GameMode.Playing && mg.Relaxed, "relaxed game starts");

        // secrets and lore exist in both modes
        var classic = new Game { FixedSeed = 4 };
        classic.NewGame(PClass.Fighter);
        check(classic.SecretsTotal == 5 && classic.LoreTotal == 21, $"5 secrets and 21 lore stones in the hub ({classic.SecretsTotal}, {classic.LoreTotal})");
        check(classic.RelicsTotal == 0 && classic.Hub.All(l => !l.Things.Any(t => t is Pickup { Kind: PickupKind.Relic })), "classic mode has no relics");
        check(classic.Hub.Sum(l => l.Things.Count(t => t is Pickup { Kind: PickupKind.Urn })) >= 4, "classic secret nooks hold Mystic Urns");
        check(classic.Hub.SelectMany(l => l.Things.OfType<LoreStone>()).All(st => !st.Text.Contains("worn away")), "every lore stone has text");

        foreach (var lv in classic.Hub.Where(l => l.SecretCount > 0))
        {
            // a secret really is secret: with the Z wall shut, its treasure can't be reached
            int z = Array.IndexOf(lv.Cells, 'Z');
            var (sx, sy) = lv.ArrivalCell();
            var treasure = lv.Things.OfType<Pickup>().First(p => p.Kind == PickupKind.Urn && Math.Abs(p.X - (z % lv.W + 0.5f)) + Math.Abs(p.Y - (z / lv.W + 0.5f)) < 6.5f);
            int ti = (int)treasure.Y * lv.W + (int)treasure.X;
            // (the Windspire's nook sits off a high ledge, so reach it by jetpack)
            var mv = Level.Move.Fly;
            check(!lv.Reachable(sx, sy, new HashSet<int> { z }, mv)[ti] && lv.Reachable(sx, sy, move: mv)[ti], $"{lv.Name}: secret nook only reachable through its hidden wall");
            check("#BWMIO".Contains(lv.SecretLook[z]), $"{lv.Name}: secret wall disguised as '{lv.SecretLook[z]}'");
        }

        var g = new Game { FixedSeed = 7, Style = GameStyle.Relaxed };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Cleric);
        // every map hides 2 relics, plus 1 in its secret nook if it has one
        int RelicsIn(MapDef d) => d.Rows.Any(r => r.Contains('%')) ? 3 : 2;
        int relicCount = Maps.Hub.Sum(RelicsIn);
        check(g.RelicsTotal == relicCount, $"{relicCount} relics hidden across the hub ({g.RelicsTotal})");
        for (int li = 0; li < g.Hub.Length; li++)
        {
            var lv = g.Hub[li];
            var relics = lv.Things.OfType<Pickup>().Where(p => p.Kind == PickupKind.Relic).ToList();
            var (sx, sy) = lv.ArrivalCell();
            var reach = lv.Reachable(sx, sy, move: Level.Move.Fly);
            check(relics.Count == RelicsIn(Maps.Hub[li]) && relics.All(r => reach[(int)r.Y * lv.W + (int)r.X] && !lv.BlocksPoint(r.X, r.Y)),
                  $"{lv.Name}: {RelicsIn(Maps.Hub[li])} reachable relics");
            check(relics.All(r => !string.IsNullOrEmpty(r.Name)), $"{lv.Name}: relics are named");
        }
        check(g.Hub.SelectMany(l => l.Things.OfType<Pickup>()).Where(p => p.Kind == PickupKind.Relic).Select(p => p.Name).Distinct().Count() == relicCount, "relic names are unique");
        int short_ = 0;
        for (int seed = 0; seed < 60; seed++)
        {
            var sg = new Game { FixedSeed = seed, Style = GameStyle.Relaxed };
            sg.NewGame(PClass.Mage);
            if (sg.RelicsTotal != relicCount) short_++;
        }
        check(short_ == 0, $"60 random games all hide exactly {relicCount} relics");
        var g2 = new Game { FixedSeed = 8, Style = GameStyle.Relaxed }; g2.NewGame(PClass.Cleric);
        string Where(Game gg) => string.Join(";", gg.Hub.SelectMany(l => l.Things.OfType<Pickup>()).Where(p => p.Kind == PickupKind.Relic).Select(p => $"{p.X},{p.Y}"));
        check(Where(g) != Where(g2), "relic spots change between games");

        // peaceful creatures: stand among them for 20 seconds
        var lv0 = g.Level;
        var ettin = lv0.Things.OfType<Monster>().First(m => (int)m.X == 14 && (int)m.Y == 4);
        g.P.X = 13.5f; g.P.Y = 5.5f;
        var start = lv0.Things.OfType<Monster>().Select(m => (m, m.X, m.Y)).ToList();
        bool hostile = false, projectiles = false;
        for (int k = 0; k < 35 * 20; k++)
        {
            Tick(new Input { Fire = true });
            hostile |= lv0.Things.OfType<Monster>().Any(m => m.State is AiState.Chase or AiState.Attack or AiState.Pain);
            projectiles |= lv0.Things.OfType<Projectile>().Any();
        }
        check(!hostile && g.P.Health == 100, "creatures never chase or attack");
        check(!projectiles && ettin.Health == ettin.Def.Health, "your weapon stays sheathed");
        check(start.Count(s => Game.Dist(s.m.X, s.m.Y, s.X, s.Y) > 0.5f) >= start.Count / 2, "creatures wander about");
        ettin.X = g.P.X + 1.2f; ettin.Y = g.P.Y;
        float before = Game.Dist(ettin.X, ettin.Y, g.P.X, g.P.Y);
        Tick(default, 35);
        check(Game.Dist(ettin.X, ettin.Y, g.P.X, g.P.Y) > before, "creatures shy away when you come close");

        // lore: face a stone and press Use; reading pauses the world
        var stone = lv0.Things.OfType<LoreStone>().First();
        g.P.X = stone.X - 1f; g.P.Y = stone.Y; g.P.Angle = 0;
        if (lv0.BlocksCircle(g.P.X, g.P.Y, 0.25f)) { g.P.X = stone.X; g.P.Y = stone.Y + 1f; g.P.Angle = -MathF.PI / 2; }
        Tick(new Input { Use = true });
        check(g.ReadingLore == stone.Text && g.P.LoreRead == 1, "Use on a lore stone shows its text");
        var mover = lv0.Things.OfType<Monster>().First();
        float mx = mover.X, my = mover.Y;
        Tick(default, 35 * 2);
        check(mover.X == mx && mover.Y == my && g.ReadingLore != null, "the world pauses while you read");
        Tick(new Input { Use = true });
        check(g.ReadingLore == null, "Use closes the lore panel");
        Tick(new Input { Use = true }); Tick(new Input { Use = true });
        check(g.P.LoreRead == 1, "re-reading a stone doesn't count twice");

        // secret wall
        int zc = Array.IndexOf(lv0.Cells, 'Z');
        g.P.X = zc % lv0.W + 0.5f; g.P.Y = zc / lv0.W + 1.5f; g.P.Angle = -MathF.PI / 2;
        Tick(new Input { Use = true }); Tick(default, 35);
        check(lv0.DoorOpen[zc] >= 1f && g.P.Secrets == 1, "Use on a hidden wall opens a secret");
        Tick(new Input { Use = true }); Tick(default, 35 * 6);
        check(g.P.Secrets == 1 && lv0.DoorOpen[zc] >= 1f, "a found secret stays open and counts once");

        // no traps, quiet arena
        int traps = 0;
        for (int k = 0; k < 100; k++)
        {
            int n = lv0.Things.Count(t => t is Monster);
            var c = new Chest { X = 14.5f, Y = 7.5f, Level = lv0 };
            lv0.Things.Add(c);
            g.OpenChest(c);
            if (lv0.Things.Count(t => t is Monster) > n) traps++;
            lv0.Things.RemoveAll(t => t == c || t is Pickup { Kind: not PickupKind.Relic });
        }
        check(traps == 0, "chests are never traps");
        {
            // an arena map played relaxed (a custom map, say) stays quiet; the Arena mode itself is always classic
            var q = new Game { FixedSeed = 1, Style = GameStyle.Relaxed };
            q.StartTest(Maps.ChaosArena, PClass.Fighter);
            var altar = q.Level.FindMark('!').Value;
            q.P.X = altar.x; q.P.Y = altar.y;
            for (int k = 0; k < 35 * 3; k++) q.Update(default, 1f / 35f);
            check(!q.Level.Arena.Started && !q.Level.Things.Any(t => t is Monster), "an arena stays quiet");
            q.StartArena(PClass.Fighter);
            check(!q.Relaxed && q.ArenaMode, "but the Arena on the title menu is always a fight");
        }

        // exploring raises the explored percentage
        float e0 = Discovery.Explored(g.Hub);
        var r = new Renderer();
        g.Warp(1);
        for (int k = 0; k < 8; k++) { g.P.Angle = k * MathF.PI / 4; r.Render(g); }
        check(Discovery.Explored(g.Hub) > e0, $"looking around raises exploration ({e0 * 100:0}% -> {Discovery.Explored(g.Hub) * 100:0}%)");

        // the exit wakes once every relic is found
        g.Warp(0);
        var exit = g.Level.FindMark('E').Value;
        g.P.X = exit.x; g.P.Y = exit.y; Tick(default);
        check(g.Mode == GameMode.Playing, "the exit sleeps until every relic is found");
        foreach (var lv in g.Hub)
            foreach (var relic in lv.Things.OfType<Pickup>().Where(p => p.Kind == PickupKind.Relic).ToList())
            {
                g.Level = lv; g.P.X = relic.X; g.P.Y = relic.Y; g.P.PortalLock = true;
                g.P.FloorZ = lv.FloorAt(relic.X, relic.Y); g.P.Z = 0; g.P.VZ = 0;
                Tick(default);
            }
        check(g.P.Relics == relicCount, $"all relics collected ({g.P.Relics})");
        g.Level = g.Hub[0]; g.P.X = exit.x; g.P.Y = exit.y; Tick(default);
        check(g.Mode == GameMode.Victory, "with every relic found, the exit wins the game");

        g.Con.Execute("mode classic");
        check(!g.Relaxed && g.Mode == GameMode.Playing && g.RelicsTotal == 0, "console 'mode classic'");
    }

    /// <summary>Custom maps: the file format, play-testing one, and the in-game editor being gone.</summary>
    static void CustomMapChecks(Action<bool, string> check)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"hexensharp-maps-{Environment.ProcessId}");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        var g = new Game { FixedSeed = 1, MapsDir = dir };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }

        check(!g.Menu.Items(MenuPage.Main).Contains("Level editor") && g.Menu.Items(MenuPage.Main).SequenceEqual(new[] { "New game", "Practice", "Arena", "Leaderboard", "Character", "Options", "Quit" }),
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

    static void ChestChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 99 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Fighter);

        foreach (var lv in g.Hub)
        {
            var chests = lv.Things.OfType<Chest>().ToList();
            check(chests.Count >= 1 || lv.Flight, $"{lv.Name}: {chests.Count} chest(s) placed");
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
    /// <summary>Writes every sound effect in both styles as WAV files, for listening outside the game.</summary>
    public static int ExportSounds(string dir)
    {
        foreach (var style in new[] { ArtStyle.SciFi, ArtStyle.Fantasy })
        {
            var sub = Path.Combine(dir, style == ArtStyle.SciFi ? "scifi" : "fantasy");
            Directory.CreateDirectory(sub);
            for (int i = 0; i < (int)Sfx.Count; i++)
                File.WriteAllBytes(Path.Combine(sub, ((Sfx)i).ToString().ToLowerInvariant() + ".wav"), Sounds.Wav(Sounds.Make((Sfx)i, style)));
        }
        Console.WriteLine($"wrote {(int)Sfx.Count * 2} sounds to {dir}");
        return 0;
    }

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
            foreach (var (w, t, k, n, d) in new[] { (16, 612.4f, 391, "ACE-1", 5), (12, 455.0f, 262, "RAIL", 1), (11, 431.7f, 240, "PLAYER", 7), (9, 318.2f, 170, "NOVA", 3), (6, 190.5f, 88, "RAIL", 0), (4, 121.9f, 41, "PLAYER", 2) })
                g.Profile.AddArenaRun(PClass.Cleric, new ArenaRun { Waves = w, Time = t, Kills = k, Name = n, When = arenaDay.AddDays(d) });
            g.Paused = true; g.Menu.Show(MenuPage.Pause); g.Menu.Show(MenuPage.Leaderboard);
            Shot("96_arena_leaderboard");
            g.Menu.Close(); g.Paused = false; g.Vars.Freeze = false; g.Vars.God = false;
            g.StartArena(PClass.Cleric);
            g.Vars.Freeze = true;
            Tick(default, 2);
            Shot("97_arena_start");
            g.Vars.Freeze = false;
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
            g.Menu.Close(); g.Paused = false;
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
        return 0;
    }
}
