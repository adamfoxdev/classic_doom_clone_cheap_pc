namespace HexenSharp;

/// <summary>Checks on the optional maps: the Windspire, quarry, dig, flight and planet maps, the Hanging Cisterns and the mini-bosses.</summary>
public static partial class Headless
{
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

    /// <summary>
    /// The Hanging Cisterns' three ledge puzzles, solved by a search over pushes and pulls (tools/puzzles/solve_blocks.py,
    /// which prints these): where to stand, which way the block goes, and whether it's a pull.
    /// </summary>
    static readonly (string ledge, float floor, (bool pull, int x, int y, char dir)[] moves)[] CisternSolutions =
    {
        ("low ledge", 1.5f, new[] { (false, 12, 17, 'E'), (false, 12, 14, 'S'), (false, 12, 15, 'S'), (false, 12, 16, 'S'), (false, 11, 18, 'E'), (false, 13, 17, 'E'), (false, 15, 18, 'N') }),
        ("middle ledge", 3f, new[] { (false, 22, 16, 'W'), (false, 20, 15, 'S'), (false, 22, 14, 'E'), (false, 23, 14, 'E'), (false, 25, 13, 'S'), (false, 24, 15, 'E'), (true, 26, 16, 'S'), (false, 20, 16, 'S') }),
        ("high ledge", 4.5f, new[] { (false, 25, 5, 'S'), (false, 24, 6, 'W'), (false, 23, 6, 'W'), (false, 21, 5, 'S'), (false, 21, 6, 'S'), (false, 21, 7, 'S'), (false, 24, 8, 'E'),
            (false, 25, 8, 'E'), (false, 26, 8, 'E'), (false, 25, 6, 'S'), (false, 24, 8, 'E'), (false, 25, 8, 'E'), (false, 27, 7, 'S'), (false, 26, 9, 'E') }),
    };

    static void MiniBossChecks(Action<bool, string> check)
    {
        // one on each optional map, awake to their own look in both styles
        var hub = Maps.BuildHub();
        foreach (var (map, def, x, y) in MiniBosses.Places)
        {
            var lv = hub.First(l => l.RawName == map);
            var m = lv.Things.OfType<Monster>().SingleOrDefault(t => t.Def == def);
            check(m != null && !lv.BlocksCircle(m.X, m.Y, def.Radius) && m.State == AiState.Idle, $"the {def.Name} waits in the {map}");
        }
        foreach (var style in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
        {
            Art.Init(style);
            bool looks = MiniBosses.All.All(d => Art.Monsters.TryGetValue(d.Art, out var set) && set.Length == Art.Monsters["ettin"].Length
                && set.All(t => t.Px.Count(px => Col.A(px) != 0) > 50));
            bool own = !Art.Monsters["warden"][0].Px.SequenceEqual(Art.Monsters["ettin"][0].Px);
            check(looks && own, $"each has its own recoloured sprites ({style})");
        }
        Art.Init(ArtStyle.SciFi);
        check(Words.T("Quarry Warden") == "Mining Mech" && Words.T("Thornmother") == "Hive Queen", "and sci-fi names");

        Game Fresh(string map, float px, float py)
        {
            var gg = new Game { FixedSeed = 1 };
            gg.NewGame(PClass.Fighter);
            gg.Warp(Array.FindIndex(gg.Hub, l => l.RawName == map));
            gg.Level.Things.RemoveAll(t => t is Monster { Def.MiniBoss: null });
            gg.P.X = px; gg.P.Y = py; gg.P.FloorZ = gg.Level.FloorAt(px, py); gg.P.Health = 400; gg.P.MaxHealth = 400;
            return gg;
        }
        void Run(Game gg, int frames, Func<bool> until = null) { for (int k = 0; k < frames && (until == null || !until()); k++) gg.Update(default, 1f / 35f); }

        // the Quarry Warden hears you through the rock, burrows to you, and slams the ground
        var g = Fresh("Deepdelve Quarry", 14.5f, 4.5f);
        var w = g.Level.Things.OfType<Monster>().Single(t => t.Def == MiniBosses.Warden);
        int rubble0 = g.Level.Cells.Count(c => c == Level.Rubble);
        Run(g, 5);
        check(w.State != AiState.Idle && !g.Level.Sight(w.X, w.Y, g.P.X, g.P.Y), "the Quarry Warden wakes when you come near, though the rock hides you");
        float d0 = Game.Dist(w.X, w.Y, g.P.X, g.P.Y);
        Run(g, 35 * 25, () => Game.Dist(w.X, w.Y, g.P.X, g.P.Y) < 2.4f);
        check(g.Level.Cells.Count(c => c == Level.Rubble) < rubble0 && Game.Dist(w.X, w.Y, g.P.X, g.P.Y) < 2.4f, $"it burrows through the rubble to you ({d0:0.0} -> {Game.Dist(w.X, w.Y, g.P.X, g.P.Y):0.0} away, {rubble0 - g.Level.Cells.Count(c => c == Level.Rubble)} blocks)");
        w.SpecialCd = 0;
        int hp = g.P.Health;
        Run(g, 35 * 2, () => w.SpecialPhase == 1);
        Run(g, 35, () => w.SpecialPhase == 0);
        check(w.SpecialCd > 3f && hp - g.P.Health >= Game.SlamDamage, $"up close it slams the ground ({hp - g.P.Health} damage)");
        w.SpecialCd = 0; w.X = g.P.X + 1.6f; w.Y = g.P.Y; w.AttackCd = 5;
        Run(g, 35, () => w.SpecialPhase == 1);
        Run(g, 35, () => w.SpecialTime < 0.3f); // time the jump: in the air when the fists come down
        hp = g.P.Health;
        g.Update(new Input { Jump = true }, 1f / 35f);
        for (int k = 0; k < 35 && w.SpecialPhase == 1; k++) g.Update(new Input { JumpHeld = true }, 1f / 35f);
        check(w.SpecialPhase == 0 && g.P.Health == hp, "jump as it brings its fists down and the slam misses");

        // the Dust Stalker winds up and charges; dodge it into a wall and it's stunned and takes double damage
        g = Fresh("Barren World", 16.5f, 12.5f);
        var st = g.Level.Things.OfType<Monster>().Single(t => t.Def == MiniBosses.Stalker);
        Run(g, 35 * 6, () => st.SpecialPhase == 2);
        check(st.SpecialPhase == 2, "the Dust Stalker winds up, then charges");
        hp = g.P.Health;
        Run(g, 35 * 2, () => st.SpecialPhase != 2);
        check(hp - g.P.Health >= Game.ChargeDamage - 2, $"standing in its way hurts ({hp - g.P.Health} damage)");
        st.SpecialCd = 0; st.X = 16.5f; st.Y = 12.5f; st.AttackCd = 5; g.P.X = 16.5f; g.P.Y = 7.5f;
        Run(g, 35 * 6, () => st.SpecialPhase == 2);
        g.P.X = 21.5f; // sidestep: it thunders on past, into the rocks up north
        Run(g, 35 * 3, () => st.SpecialPhase == 3);
        check(st.SpecialPhase == 3, "sidestep and it runs into the rocks, stunned");
        int shp = st.Health;
        g.DamageMonster(st, 20, 0);
        check(shp - st.Health == 40, "and takes double damage while it's dazed");

        // the Thornmother calls her brood, four at most, and they die with her
        g = Fresh("Verdant Moon", 14.5f, 9.5f);
        var tm = g.Level.Things.OfType<Monster>().Single(t => t.Def == MiniBosses.Thornmother);
        g.Vars.God = true;
        Run(g, 35 * 9);
        int brood = g.Level.Things.Count(t => t is Monster o && o.Summoner == tm && o.Alive);
        Run(g, 35 * 30);
        int brood2 = g.Level.Things.Count(t => t is Monster o && o.Summoner == tm && o.Alive);
        check(brood == 2 && brood2 == Game.MaxBrood, $"the Thornmother calls her brood, two at a time, four at most ({brood}, then {brood2})");
        g.Vars.God = false;
        int xp0 = g.Profile.TotalXp;
        g.DamageMonster(tm, 100000, 0);
        check(!tm.Alive && g.Level.Things.All(t => t is not Monster o || o.Summoner != tm || !o.Alive), "when she falls, her brood falls with her");
        var drops = g.Level.Things.OfType<Pickup>().Where(pk => Game.Dist(pk.X, pk.Y, tm.X, tm.Y) < 1).Select(pk => pk.Kind).ToList();
        check(drops.Contains(PickupKind.Urn) && drops.Contains(PickupKind.Armor), "she drops a Mystic Urn and armour");
        check(g.Profile.TotalXp - xp0 >= Game.MiniBossXp && g.Profile.MiniBosses.Contains("thornmother"), $"and pays {Game.MiniBossXp} XP on top, remembered in your profile");

        // the Drowned Keeper blinks away as it's hurt, up onto the ledges
        g = Fresh("Hanging Cisterns", 12.5f, 9.5f);
        var kp = g.Level.Things.OfType<Monster>().Single(t => t.Def == MiniBosses.Keeper);
        Run(g, 10);
        check(g.BossInFight() == kp, "a mini-boss you're fighting gets a health bar");
        var r = new Renderer();
        r.Render(g);
        check(r.Fb.Count(px => px == Col.Rgb(220, 50, 40)) > 100, "drawn low in the view");
        float kx = kp.X, ky = kp.Y;
        g.DamageMonster(kp, 70, 0);
        Run(g, 2);
        check(Game.Dist(kx, ky, kp.X, kp.Y) > 2 && Game.Dist(kp.X, kp.Y, g.P.X, g.P.Y) >= 5 && g.Level.FloorAt(kp.X, kp.Y) > 0,
              $"the Drowned Keeper blinks away when hurt, up onto a ledge ({g.Level.FloorAt(kp.X, kp.Y)} up)");

        // relaxed: they're as peaceful as the rest
        var rel = new Game { FixedSeed = 1, Style = GameStyle.Relaxed };
        rel.NewGame(PClass.Fighter);
        rel.Warp(Array.FindIndex(rel.Hub, l => l.RawName == "Deepdelve Quarry"));
        rel.P.X = 14.5f; rel.P.Y = 4.5f;
        int rr = rel.Level.Cells.Count(c => c == Level.Rubble);
        Run(rel, 35 * 10);
        check(rel.Level.Cells.Count(c => c == Level.Rubble) == rr, "in the relaxed style they leave you (and the rock) alone");

        // all four: Big Game Hunter; and the console can summon them
        var hunter = new Game { FixedSeed = 1, Profile = new Profile { MiniBosses = MiniBosses.All.Select(d => d.MiniBoss).ToList() } };
        Achievements.Check(hunter);
        check(hunter.Profile.Achievements.ContainsKey("big_game"), "beating all four is Big Game Hunter");
        hunter.NewGame(PClass.Fighter);
        hunter.Con.Execute("summon stalker");
        check(hunter.Level.Things.OfType<Monster>().Any(m => m.Def == MiniBosses.Stalker && m.State != AiState.Idle), "'summon stalker' brings one to you");
    }

    static void CisternChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 2 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Cleric);
        int ci = Array.FindIndex(g.Hub, l => l.RawName == "Hanging Cisterns");
        check(ci == g.Hub.Length - 1, "the Hanging Cisterns join the end of the hub, so no other map moves");
        g.Warp(ci);
        var lv = g.Level;
        var p = g.P;
        lv.Things.RemoveAll(t => t is Monster or Chest);
        var arrive = lv.FindMark('3').Value;
        check(MathF.Abs(p.X - arrive.x) < 0.01f && lv.PlateCount == 7 && lv.LeverCount == 1 && lv.Checkpoints.Count == 3,
              "you arrive by portal 3; seven plates, one lever, a checkpoint on each of three ledges");
        float Dist(float tx, float ty) => MathF.Sqrt((tx - p.X) * (tx - p.X) + (ty - p.Y) * (ty - p.Y));
        void Face(float tx, float ty) => p.Angle = MathF.Atan2(ty - p.Y, tx - p.X);
        void WalkTo(float tx, float ty) { for (int k = 0; k < 35 * 8 && Dist(tx, ty) > 0.15f; k++) { Face(tx, ty); Tick(new Input { Move = MathF.Min(1, Dist(tx, ty) * 2) }); } }
        bool FlyTo(float tx, float ty, float floor)
        {
            Tick(default, 70); // let the tank recharge
            Tick(new Input { JetHeld = true });
            for (int k = 0; k < 35 * 4 && p.FloorZ + p.Z < floor + 0.5f; k++) Tick(new Input { JetHeld = true });
            for (int k = 0; k < 35 * 8 && Dist(tx, ty) > 0.15f; k++) { Face(tx, ty); Tick(new Input { Move = MathF.Min(1, Dist(tx, ty) * 2), JetHeld = p.FloorZ + p.Z < floor + 0.4f }); }
            for (int k = 0; k < 35 * 5 && p.Flying; k++) Tick(new Input { SlideHeld = true });
            return p.OnGround && MathF.Abs(p.FloorZ - floor) < 0.01f;
        }

        // the spare jetpack by the portal, then out through the door into the cistern
        WalkTo(4.5f, 18.5f);
        check(p.HasJetpack, "a spare jetpack waits by the portal");
        WalkTo(6.5f, 16.5f); p.Angle = 0;
        Tick(new Input { Use = true }); Tick(default, 35);
        WalkTo(8.6f, 16.5f);
        check(p.FloorZ == 0 && lv.FloorAt(12.5f, 16.5f) == 1.5f && !lv.PuzzleSolved, "the cistern floor, with the ledges far above it");
        int gate = Array.IndexOf(lv.Cells, 'P');
        check(lv.DoorOpen[gate] <= 0, "the vault's portcullis is shut");

        // fly up to each ledge in turn, and solve it
        var landings = new[] { (10.5f, 14.5f), (19.5f, 13.5f), (21.5f, 5.5f) };
        int movesOk = 0, movesAll = 0;
        for (int li = 0; li < CisternSolutions.Length; li++)
        {
            var (name, floor, moves) = CisternSolutions[li];
            bool landed = FlyTo(landings[li].Item1, landings[li].Item2, floor);
            check(landed, $"fly up to the {name} ({floor} up)");
            if (!landed) return;
            int plates0 = lv.PlatesCovered;
            foreach (var (pull, x, y, dir) in moves)
            {
                movesAll++;
                var (dx, dy) = dir switch { 'E' => (1, 0), 'W' => (-1, 0), 'S' => (0, 1), _ => (0, -1) };
                // the block sits beside you: ahead for a push, and for a pull it's on the far side, coming toward you
                int bx = pull ? x - dx : x + dx, by = pull ? y - dy : y + dy;
                p.X = x + 0.5f; p.Y = y + 0.5f; p.FloorZ = floor; p.Z = 0; p.VX = p.VY = 0;
                Face(bx + 0.5f, by + 0.5f);
                Tick(new Input { Use = true, Walk = pull }); Tick(default);
                if (lv.Cell(bx + dx, by + dy) == 'X' && lv.Cell(bx, by) != 'X') movesOk++;
            }
            check(lv.PlatesCovered - plates0 == new[] { 2, 2, 3 }[li], $"the {name}'s blocks all sit on its plates ({lv.PlatesCovered - plates0} more covered)");
        }
        check(movesOk == movesAll && lv.PlatesCovered == lv.PlateCount && !lv.PuzzleSolved, $"every push and pull works ({movesOk}/{movesAll}); the lever's still to pull");

        // the high lever, and the vault opens
        p.X = 28.5f; p.Y = 7.5f; p.FloorZ = 4.5f; p.Angle = 0;
        Tick(new Input { Use = true }); Tick(default, 35 * 2);
        check(lv.PuzzleSolved && lv.DoorOpen[gate] >= 1f, "the lever up on the high ledge, with every plate weighed down, raises the vault's portcullis");
        var reach = lv.Reachable((int)arrive.x, (int)arrive.y);
        check(lv.Things.OfType<Pickup>().Where(t => t.X < 7 && t.Y < 6).All(t => reach[(int)t.Y * lv.W + (int)t.X]), "the vault's treasure can be walked to");

        // lore, and the secret nook in the antechamber
        check(Enumerable.Range(0, lv.Things.OfType<LoreStone>().Count()).All(i => !Discovery.LoreText("Hanging Cisterns", i).Contains("worn away")), "every lore stone has its text");
        int z = Array.IndexOf(lv.Cells, 'Z');
        p.X = z % lv.W + 1.5f; p.Y = z / lv.W + 0.5f; p.FloorZ = 0; p.Angle = MathF.PI;
        int secrets = p.Secrets;
        Tick(new Input { Use = true }); Tick(default, 35);
        check(p.Secrets == secrets + 1, "a secret wall in the antechamber's corner");
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
}
