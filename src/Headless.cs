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

        Console.WriteLine("Dark Bishop:");
        BishopChecks(Check);
        Console.WriteLine("Chests:");
        ChestChecks(Check);

        Console.WriteLine("Options and key bindings:");
        OptionsChecks(Check);

        Console.WriteLine("Relaxed mode and discovery:");
        RelaxedChecks(Check);

        Console.WriteLine("Level editor:");
        EditorChecks(Check);

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
        VerticalAimChecks(Check);

        Console.WriteLine("Audio synthesis:");
        SoundChecks(Check);

        Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
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
        check(si == hub.Length - 1 && si >= 4, "the Windspire joins the hub after the Chaos Arena");
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
        check(copy.Floors.SequenceEqual(lv.Floors) && copy.Heights.SequenceEqual(lv.Heights), "the Windspire's towers survive a save and load in the editor");

        // climb it for real: portal in, grab the spare jetpack, hop ledge to ledge, pull the beacon lever, loot the vault
        var g = new Game { FixedSeed = 3 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.NewGame(PClass.Mage);
        g.Warp(si);
        var sp = g.Level;
        var p = g.P;
        check(sp.RawName == "Windspire" && MathF.Abs(p.X - 3.5f) < 0.01f && MathF.Abs(p.Y - 18.5f) < 0.01f, "warping in lands on the arrival portal");
        sp.Things.RemoveAll(t => t is Monster);
        void Face(float tx, float ty) => p.Angle = MathF.Atan2(ty - p.Y, tx - p.X);
        void WalkTo(float tx, float ty)
        {
            for (int k = 0; k < 35 * 8 && Dist(tx, ty) > 0.15f; k++) { Face(tx, ty); Tick(new Input { Move = MathF.Min(1, Dist(tx, ty) * 2) }); }
        }
        float Dist(float tx, float ty) => MathF.Sqrt((tx - p.X) * (tx - p.X) + (ty - p.Y) * (ty - p.Y));
        bool FlyTo(float tx, float ty, float floor)
        {
            Tick(default, 70); // let the tank recharge
            Tick(new Input { Jump = true, JumpHeld = true });
            for (int k = 0; k < 35 * 4 && p.FloorZ + p.Z < floor + 0.5f; k++) Tick(new Input { JumpHeld = true });
            for (int k = 0; k < 35 * 8 && Dist(tx, ty) > 0.15f; k++)
            {
                Face(tx, ty);
                Tick(new Input { Move = MathF.Min(1, Dist(tx, ty) * 2), JumpHeld = p.FloorZ + p.Z < floor + 0.4f });
            }
            for (int k = 0; k < 35 * 5 && p.Flying; k++) Tick(new Input { SlideHeld = true });
            return p.OnGround && MathF.Abs(p.FloorZ - floor) < 0.01f;
        }

        // without the jetpack you can't get off the ground
        WalkTo(9.9f, 16.5f); WalkTo(9.9f, 13.5f); WalkTo(15.2f, 13.5f);
        for (int k = 0; k < 10; k++) { Tick(new Input { Jump = true, JumpHeld = true, Move = 1 }); Tick(new Input { JumpHeld = true, Move = 1 }, 25); }
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
        WalkTo(10.5f, 13.5f); WalkTo(9.9f, 16.5f); WalkTo(13.5f, 18.5f); WalkTo(15.5f, 18.5f); WalkTo(16.5f, 17.5f);
        check(p.Urns == 1, "the vault's Nano canister is yours");
        WalkTo(18.5f, 18.5f);
        check(p.SteelKey, "and so is the blue keycard");
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
        Tick(new Input { Jump = true, JumpHeld = true });
        for (int k = 0; k < 35 * 2; k++) Tick(new Input { JumpHeld = true });
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

        // without it, holding Jump is just a jump
        float apex = 0;
        Tick(new Input { Jump = true, JumpHeld = true });
        for (int k = 0; k < 35 * 2; k++) { Tick(new Input { JumpHeld = true }); apex = MathF.Max(apex, p.Z); }
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
        Tick(new Input { Jump = true, JumpHeld = true });
        Tick(new Input { JumpHeld = true }, 35);
        check(p.Flying && p.Z > 1.2f, $"holding Jump in the air fires the jetpack and climbs (height {p.Z:0.00})");
        check(sfx.Contains(Sfx.JetStart) && sfx.Count(s => s == Sfx.Jet) >= 5, "the jetpack ignites and roars while it burns");
        Tick(new Input { JumpHeld = true }, 35 * 2);
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
        Tick(new Input { Jump = true, JumpHeld = true });
        Tick(new Input { JumpHeld = true }, 35);
        Tick(new Input { Move = 1 }, 30);
        for (int k = 0; k < 35 * 3 && p.Flying; k++) Tick(new Input { SlideHeld = true });
        check(p.FloorZ == 1.5f && p.OnGround && !p.Flying, $"with the jetpack you fly up and land on the ledge (floor {p.FloorZ})");

        // running dry drops you; the tank refills on the ground
        p.X = 12.5f; p.Y = 6.5f; p.FloorZ = 0; p.Z = 0; p.Fuel = 0.5f;
        sfx.Clear();
        Tick(new Input { Jump = true, JumpHeld = true });
        bool ranDry = false;
        for (int k = 0; k < 35 * 3; k++) { Tick(new Input { JumpHeld = true }); ranDry |= p.Fuel == 0 && !p.Flying && !p.OnGround; }
        check(ranDry && p.OnGround && sfx.Contains(Sfx.JetOut), "when the fuel runs out the jetpack sputters and you fall");
        Tick(default, 35 * 2);
        check(p.Fuel > 2.5f && p.Fuel <= Player.FuelMax, $"the tank recharges on the ground ({p.Fuel:0.0})");
        g.Vars.InfiniteFuel = true; p.Fuel = 0;
        Tick(new Input { Jump = true, JumpHeld = true });
        Tick(new Input { JumpHeld = true }, 35);
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
        check(Editor.IsKnownGlyph('J') && ThingFactory.Create('J', 1, 1) is Pickup { Kind: PickupKind.Jetpack }, "the editor can place jetpacks ('J')");

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

        // title menu: New game / Options / Quit
        check(g.Menu.Page == MenuPage.Main, "title shows the main menu");
        Press(Keys.Down); Press(Keys.Down);
        Press(Keys.Enter);
        check(g.Menu.Page == MenuPage.Options, "main menu opens Options");
        Press(Keys.Escape);
        check(g.Menu.Page == MenuPage.Main && g.Menu.Cursor == 2, "Esc goes back to the main menu");
        Press(Keys.Up); Press(Keys.Up); Press(Keys.Enter);
        check(g.Menu.Page == MenuPage.Style, "New game asks for a play style");
        Press(Keys.Enter);
        check(g.Mode == GameMode.ClassSelect && g.Style == GameStyle.Classic, "Classic goes to class select");
        Press(Keys.Enter);
        check(g.Mode == GameMode.Playing, "choosing a class starts the game");

        // Esc in game: pause menu -> Options -> Key bindings
        Press(Keys.Escape);
        check(g.Paused && g.Menu.Page == MenuPage.Pause, "Esc during play opens the pause menu");
        Press(Keys.Down); Press(Keys.Enter);
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
        for (int k = 0; k < 3; k++) Press(Keys.Down);
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
        check(classic.SecretsTotal == 5 && classic.LoreTotal == 20, $"5 secrets and 20 lore stones in the hub ({classic.SecretsTotal}, {classic.LoreTotal})");
        check(classic.RelicsTotal == 0 && classic.Hub.All(l => !l.Things.Any(t => t is Pickup { Kind: PickupKind.Relic })), "classic mode has no relics");
        check(classic.Hub.Sum(l => l.Things.Count(t => t is Pickup { Kind: PickupKind.Urn })) >= 4, "classic secret nooks hold Mystic Urns");
        check(classic.Hub.SelectMany(l => l.Things.OfType<LoreStone>()).All(st => !st.Text.Contains("worn away")), "every lore stone has text");

        foreach (var lv in classic.Hub)
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
        int relicCount = Maps.Hub.Length * 3;
        check(g.RelicsTotal == relicCount, $"{relicCount} relics hidden across the hub ({g.RelicsTotal})");
        foreach (var lv in g.Hub)
        {
            var relics = lv.Things.OfType<Pickup>().Where(p => p.Kind == PickupKind.Relic).ToList();
            var (sx, sy) = lv.ArrivalCell();
            var reach = lv.Reachable(sx, sy, move: Level.Move.Fly);
            check(relics.Count == 3 && relics.All(r => reach[(int)r.Y * lv.W + (int)r.X] && !lv.BlocksPoint(r.X, r.Y)),
                  $"{lv.Name}: 3 reachable relics");
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
        string Where(Game gg) => string.Join(";", gg.Hub[0].Things.OfType<Pickup>().Where(p => p.Kind == PickupKind.Relic).Select(p => $"{p.X},{p.Y}"));
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
        g.Warp(3);
        var altar = g.Level.FindMark('!').Value;
        g.P.X = altar.x; g.P.Y = altar.y;
        Tick(default, 35 * 3);
        check(!g.Level.Arena.Started && !g.Level.Things.Any(t => t is Monster), "the arena stays quiet");

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

    static void EditorChecks(Action<bool, string> check)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"hexensharp-maps-{Environment.ProcessId}");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var keys = new FakeKeys();
        var g = new Game { FixedSeed = 1, Keys = keys, MapsDir = dir };
        var ed = g.Editor;
        // one frame: keys pressed this frame, keys held, and the mouse position
        void Frame(int[] hit = null, int[] held = null, float mx = -1, float my = -1, string typed = null)
        {
            keys.Hit.Clear(); keys.Held.Clear();
            foreach (var k in hit ?? Array.Empty<int>()) keys.Hit.Add(k);
            foreach (var k in held ?? Array.Empty<int>()) keys.Held.Add(k);
            var inp = g.Binds.Read(keys, g.Con.Open);
            inp.MouseX = mx; inp.MouseY = my; inp.Typed = typed;
            g.Update(inp, 1f / 35f);
        }
        (float, float) CellPos(int x, int y) => ((x - ed.CamX) * ed.CellSize + 3, (y - ed.CamY) * ed.CellSize + 3);
        void Click(int button, int x, int y) { var (mx, my) = CellPos(x, y); Frame(new[] { button }, new[] { button }, mx, my); Frame(mx: mx, my: my); }
        void Select(char glyph) => ed.BrushIndex = Array.FindIndex(Editor.Palette, b => b.Glyph == glyph);

        // title menu -> Level editor
        Frame(new[] { Keys.Down }); Frame(new[] { Keys.Enter });
        check(g.Mode == GameMode.Editor, "title menu opens the level editor");
        ed.NewMap(20, 16);
        check(ed.Doc.W == 20 && ed.Doc[0, 0] == '#' && ed.Doc[5, 5] == '.' && ed.Doc[2, 2] == '@', "a new map is walled, with a player start");

        // paint a wall stroke by dragging with the left button
        Select('B');
        for (int x = 4; x <= 8; x++) { var (mx, my) = CellPos(x, 6); Frame(x == 4 ? new[] { Keys.Mouse1 } : null, new[] { Keys.Mouse1 }, mx, my); }
        Frame(mx: 0, my: 0);
        check(Enumerable.Range(4, 5).All(x => ed.Doc[x, 6] == 'B'), "dragging with the left button paints a line");
        ed.Undo();
        check(Enumerable.Range(4, 5).All(x => ed.Doc[x, 6] == '.'), "one undo removes the whole stroke");
        ed.Redo();
        check(ed.Doc[8, 6] == 'B', "redo puts it back");
        Click(Keys.Mouse2, 6, 6);
        check(ed.Doc[6, 6] == '.', "right click erases");
        Click(Keys.Mouse3, 5, 6);
        check(ed.Current.Glyph == 'B', "middle click picks up a glyph");

        // palette: click an icon, or use the wheel
        int ettin = Array.FindIndex(Editor.Palette, b => b.Glyph == 'e');
        float pmx = Editor.PaletteX + (ettin % Editor.PaletteCols) * Editor.PaletteCell + 5;
        float pmy = Editor.PaletteY + (ettin / Editor.PaletteCols) * Editor.PaletteCell + 5;
        Frame(new[] { Keys.Mouse1 }, new[] { Keys.Mouse1 }, pmx, pmy); Frame(mx: pmx, my: pmy);
        check(ed.Current.Glyph == 'e', "clicking the palette selects Ettin");
        Frame(new[] { Keys.WheelDown });
        check(ed.Current.Glyph == Editor.Palette[ettin + 1].Glyph, "the mouse wheel steps through the palette");

        // keyboard only: move the cursor and paint with Space
        Select('h');
        ed.CursorX = 3; ed.CursorY = 3;
        Frame(new[] { Keys.Right }); Frame(new[] { Keys.Down });
        Frame(new[] { Keys.Space });
        check(ed.Doc[4, 4] == 'h', "arrows + Space paint without a mouse");

        // one player start at most
        Select('@');
        Click(Keys.Mouse1, 10, 10);
        check(ed.Doc.Cells.Count(c => c == '@') == 1 && ed.Doc[10, 10] == '@', "placing a new start moves it");

        // fill tool: fill a walled pocket
        Select('#');
        foreach (var (x, y) in new[] { (14, 3), (15, 3), (16, 3), (14, 4), (16, 4), (14, 5), (15, 5), (16, 5) }) ed.Doc[x, y] = '#';
        Select(',');
        Frame(new[] { Keys.Letter('F') });
        Click(Keys.Mouse1, 15, 4);
        check(ed.FillTool && ed.Doc[15, 4] == ',' && ed.Doc[13, 4] == '.', "fill tool fills an enclosed pocket only");
        Frame(new[] { Keys.Letter('F') });

        // theme, rename, zoom
        string theme = ed.Doc.ThemeId;
        Frame(new[] { Keys.Letter('T') });
        check(ed.Doc.ThemeId != theme, "T cycles the theme");
        Frame(new[] { Keys.Letter('R') });
        Frame(typed: "Test Grotto"); Frame(new[] { Keys.Enter });
        check(ed.Doc.Name == "Test Grotto" && ed.RenameText == null, "R renames the map");
        int cs = ed.CellSize;
        Frame(new[] { Keys.Equal });
        check(ed.CellSize > cs, "= zooms in");
        Frame(new[] { Keys.Minus });

        // validation and play-test
        ed.Doc[10, 10] = '.';
        Frame(new[] { Keys.Letter('P') });
        check(g.Mode == GameMode.Editor && ed.Status.Contains("player start"), "play-test refuses a map without a start");
        Select('@'); Click(Keys.Mouse1, 3, 12);
        Select('E'); Click(Keys.Mouse1, 12, 12);
        Select('e'); Click(Keys.Mouse1, 17, 12);
        Frame(new[] { Keys.Letter('P') });
        check(g.Mode == GameMode.Playing && g.TestingMap && g.Hub.Length == 1, "P play-tests the map");
        check(g.Level.Name == "Test Grotto" && (int)g.P.X == 3 && (int)g.P.Y == 12, "you start at the map's @");
        check(g.Level.Cell(8, 6) == 'B' && g.Level.Things.Any(t => t is Monster && (int)t.X == 17), "the level matches what was painted");
        check(g.Level.BossDead, "a map with no Heresiarch has its exit open");

        // pause > Back to editor
        Frame(new[] { Keys.Escape });
        check(g.Menu.Items(MenuPage.Pause)[3] == "Back to editor", "the pause menu offers Back to editor");
        Frame(new[] { Keys.Down }); Frame(new[] { Keys.Down }); Frame(new[] { Keys.Down }); Frame(new[] { Keys.Enter });
        check(g.Mode == GameMode.Editor && !g.TestingMap && ed.Doc.Name == "Test Grotto", "Back to editor keeps your map");

        // winning a play-test also returns to the editor
        Frame(new[] { Keys.Letter('P') });
        var exit = g.Level.FindMark('E').Value;
        g.P.X = exit.x; g.P.Y = exit.y;
        Frame();
        check(g.Mode == GameMode.Victory, "reaching the exit wins the play-test");
        Frame(new[] { Keys.Enter });
        check(g.Mode == GameMode.Editor, "Enter on victory returns to the editor");

        // save / open / load
        Frame(new[] { Keys.Letter('S') }, new[] { Keys.LeftControl });
        string file = Path.Combine(dir, "test_grotto.hxm");
        check(File.Exists(file) && !ed.Dirty, "Ctrl+S saves test_grotto.hxm");
        var reloaded = MapDoc.Parse(File.ReadAllText(file));
        check(reloaded.Name == "Test Grotto" && reloaded.Rows().SequenceEqual(ed.Doc.Rows()) && reloaded.ThemeId == ed.Doc.ThemeId, "the saved file loads back identically");
        Frame(new[] { Keys.Letter('O') }, new[] { Keys.LeftControl });
        check(ed.OpenList != null && ed.OpenList.Count == Maps.Hub.Length + 1, "Ctrl+O lists the built-in maps and your map");
        Frame(new[] { Keys.Enter });
        check(ed.Doc.Name == "Winnowing Hall" && ed.Doc.Rows().SequenceEqual(Maps.Hub[0].Rows), "open a built-in map as a template");
        check(Maps.Hub.All(d => MapDoc.Parse(MapDoc.FromDef(d).Serialize()).Rows().SequenceEqual(d.Rows)), "every built-in map survives save and load");
        check(MapDoc.Parse("name: X\n---\n#####\n#@?Q#\n#####\n")[2, 1] == '.', "unknown glyphs in a file become floor");

        // leaving with unsaved changes needs a second Esc
        ed.Paint(5, 5, '#');
        Frame(new[] { Keys.Escape });
        check(g.Mode == GameMode.Editor && ed.Status.Contains("Unsaved"), "Esc warns about unsaved changes");
        Frame(new[] { Keys.Escape });
        check(g.Mode == GameMode.Title, "a second Esc leaves");

        // play a saved map from the console
        g.Con.Execute("playmap test grotto");
        check(g.Mode == GameMode.Playing && g.Level.Name == "Test Grotto", "console 'playmap test grotto'");
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
        check(wh.HeightAt(4.5f, 18.5f) == 3.5f && hub[3].HeightAt(15.5f, 8.5f) == 3.5f, "the boss arena and Chaos Arena tower at 3.5");
        check(wh.HeightAt(17.5f, 12.5f) == 1f, "corridors stay one storey");
        check(Enumerable.Range(0, wh.Cells.Length).Where(i => Level.IsDoor(wh.Cells[i])).All(i => wh.Heights[i] == 1f), "doors are always one storey");
        check(hub[2].HeightAt(23.5f, 8.5f) == 1f, "the crypt's block-puzzle room stays one storey");

        // rendering: the same view, with and without heights
        var g = new Game { FixedSeed = 1 };
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster or Decor or Chest or LoreStone);
        var r = new Renderer();
        float proj = 160f / MathF.Tan(g.Vars.Fov * MathF.PI / 360f), horizon = Renderer.ViewH / 2f;
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
        check(Maps.Hub.All(d => MapDoc.FromDef(d).ToDef().Build().Heights.SequenceEqual(d.Build().Heights)), "built-in maps keep their heights when opened in the editor");

        // editor: height mode painting, fill, undo, and play-testing the result
        var keys = new FakeKeys();
        var eg = new Game { FixedSeed = 1, Keys = keys };
        var ed = eg.Editor;
        void Frame(int[] hit = null, int[] held = null, float mx = -1, float my = -1)
        {
            keys.Hit.Clear(); keys.Held.Clear();
            foreach (var k in hit ?? Array.Empty<int>()) keys.Hit.Add(k);
            foreach (var k in held ?? Array.Empty<int>()) keys.Held.Add(k);
            var inp = eg.Binds.Read(keys, false);
            inp.MouseX = mx; inp.MouseY = my;
            eg.Update(inp, 1f / 35f);
        }
        eg.OpenEditor();
        ed.NewMap(20, 16);
        check(ed.Doc.DefaultHeight == 1.5f, "new maps start 1.5 tall");
        Frame(new[] { Keys.Letter('G') });
        Frame(new[] { Keys.Digit(8) });
        check(ed.HeightMode && ed.CurrentHeight == '8', "G enters height mode; 8 picks 4.0");
        float cx = 6 * ed.CellSize + 3, cy = 6 * ed.CellSize + 3;
        Frame(new[] { Keys.Mouse1 }, new[] { Keys.Mouse1 }, cx, cy); Frame(mx: cx, my: cy);
        check(ed.Doc.Heights[6 * 20 + 6] == '8' && ed.Doc[6, 6] == '.', "painting in height mode changes the height, not the tile");
        Frame(new[] { Keys.Letter('F') });
        Frame(new[] { Keys.Digit(4) });
        Frame(new[] { Keys.Mouse1 }, new[] { Keys.Mouse1 }, 10 * ed.CellSize + 3, 10 * ed.CellSize + 3);
        check(ed.Doc.Heights[10 * 20 + 10] == '4' && ed.Doc.Heights[1 * 20 + 1] == '4' && ed.Doc.Heights[6 * 20 + 6] == '8', "height fill covers the room but not other heights");
        ed.Undo();
        check(ed.Doc.Heights[10 * 20 + 10] == '.' && ed.Doc.Heights[6 * 20 + 6] == '8', "undo reverts a height fill");
        Frame(new[] { Keys.Letter('F') });
        Frame(new[] { Keys.Letter('G') });
        check(!ed.HeightMode, "G returns to tile mode");
        Frame(new[] { Keys.Letter('P') });
        check(eg.Mode == GameMode.Playing && eg.Level.HeightAt(6.5f, 6.5f) == 4f && eg.Level.HeightAt(8.5f, 8.5f) == 1.5f, "play-testing uses the painted heights");
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
        int riserRow = (int)(Renderer.ViewH / 2f - (0.12f - 0.5f) * proj / 3.5f);
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

        // editor: floors layer, stair brush, files, play-test
        var keys = new FakeKeys();
        var eg = new Game { FixedSeed = 1, Keys = keys };
        var ed = eg.Editor;
        void Frame(int[] hit = null, int[] held = null, float mx = -1, float my = -1)
        {
            keys.Hit.Clear(); keys.Held.Clear();
            foreach (var k in hit ?? Array.Empty<int>()) keys.Hit.Add(k);
            foreach (var k in held ?? Array.Empty<int>()) keys.Held.Add(k);
            var inp = eg.Binds.Read(keys, false);
            inp.MouseX = mx; inp.MouseY = my;
            eg.Update(inp, 1f / 35f);
        }
        eg.OpenEditor();
        ed.NewMap(20, 16);
        Frame(new[] { Keys.Letter('G') }); Frame(new[] { Keys.Letter('G') });
        check(ed.Mode == Editor.Layer.Floors, "G, G reaches the floors layer");
        Frame(new[] { Keys.Digit(1) }); Frame(new[] { Keys.Letter('K') });
        check(ed.StairBrush && ed.CurrentFloor == '1', "1 picks 0.25 and K turns on the stair brush");
        for (int x = 5; x <= 8; x++)
        {
            float mx = x * ed.CellSize + 3, my = 8 * ed.CellSize + 3;
            Frame(x == 5 ? new[] { Keys.Mouse1 } : null, new[] { Keys.Mouse1 }, mx, my);
            Frame(null, new[] { Keys.Mouse1 }, mx, my);
        }
        Frame();
        check(new string(Enumerable.Range(5, 4).Select(x => ed.Doc.Floors[8 * 20 + x]).ToArray()) == "1234", "dragging the stair brush builds a staircase 1-2-3-4");
        ed.Undo();
        check(ed.Doc.Floors[8 * 20 + 6] == '.', "undo removes the staircase");
        ed.Redo();
        var back = MapDoc.Parse(ed.Doc.Serialize());
        check(back.Floors.SequenceEqual(ed.Doc.Floors) && back.Heights.SequenceEqual(ed.Doc.Heights), "floors survive save and load");
        Frame(new[] { Keys.Letter('G') });
        check(ed.Mode == Editor.Layer.Tiles, "G cycles back to tiles");
        ed.Doc[2, 2] = '.'; ed.Doc[4, 8] = '@';
        Frame(new[] { Keys.Letter('P') });
        check(eg.Mode == GameMode.Playing && eg.Level.FloorAt(8.5f, 8.5f) == 1f && eg.Level.HeightAt(8.5f, 8.5f) >= 2f,
              "play-testing uses the painted floors (with headroom kept above them)");
        eg.P.Angle = 0;
        for (int k = 0; k < 35 * 3 && eg.P.X < 8.5f; k++) eg.Update(new Input { Move = 1 }, 1f / 35f);
        check(eg.P.FloorZ == 1f && eg.P.X >= 8.5f, "and you can walk up the painted staircase");
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

        // level editor: a fresh map with help, a built-in map as a template, the open dialog, and a play-test
        {
            string mdir = Path.Combine(Path.GetTempPath(), $"hexensharp-shots-{Environment.ProcessId}");
            g.MapsDir = mdir;
            g.GoToTitle();
            g.OpenEditor();
            var ed = g.Editor;
            ed.NewMap(32, 24);
            ed.Doc.Name = "My Grotto";
            void Box(int x0, int y0, int x1, int y1, char c) { for (int x = x0; x <= x1; x++) { ed.Doc[x, y0] = c; ed.Doc[x, y1] = c; } for (int y = y0; y <= y1; y++) { ed.Doc[x0, y] = c; ed.Doc[x1, y] = c; } }
            Box(8, 3, 20, 12, 'M'); ed.Doc[8, 7] = 'D'; ed.Doc[14, 12] = 'Z';
            for (int y = 4; y < 12; y++) for (int x = 9; x < 20; x++) ed.Doc[x, y] = ',';
            ed.Doc[12, 6] = 'e'; ed.Doc[17, 9] = 'd'; ed.Doc[18, 4] = '$'; ed.Doc[10, 10] = 'T'; ed.Doc[15, 5] = '&'; ed.Doc[4, 18] = 'E';
            ed.Doc[14, 14] = '%'; ed.BrushIndex = Array.FindIndex(Editor.Palette, b => b.Glyph == 'd');
            ed.CursorX = 17; ed.CursorY = 9;
            Tick(default, 1);
            Shot("27_editor_help");
            ed.ShowHelp = false;
            Tick(default, 1);
            Shot("28_editor_map");
            ed.Load(MapDoc.FromDef(Maps.Hub[2]), "Opened");
            ed.ZoomIndex = 1; ed.CursorX = 23; ed.CursorY = 9;
            Tick(default, 1);
            Shot("29_editor_crypt");
            ed.ShowOpenList();
            Shot("30_editor_open");
            ed.OpenList = null;
            ed.NewMap(32, 24);
            ed.Doc.Name = "My Grotto";
            Box(8, 3, 20, 12, 'M'); ed.Doc[8, 7] = 'D';
            for (int y = 4; y < 12; y++) for (int x = 9; x < 20; x++) ed.Doc[x, y] = ',';
            ed.Doc[12, 6] = 'e'; ed.Doc[17, 9] = 'd'; ed.Doc[18, 4] = '$'; ed.Doc[10, 10] = 'T'; ed.Doc[15, 5] = '&';
            ed.Doc[2, 2] = '.'; ed.Doc[3, 7] = '@';
            ed.PlayTest();
            g.P.Angle = 0.12f;
            g.Vars.Freeze = true;
            Tick(default, 60);
            Shot("31_editor_playtest");
            g.Vars.Freeze = false;
            g.ReturnToEditor();
            g.GoToTitle();
            g.MapsDir = null;
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
        g.OpenEditor();
        g.Editor.Load(MapDoc.FromDef(Maps.Hub[0]), "Opened");
        g.Editor.HeightMode = true; g.Editor.ShowHelp = false; g.Editor.ZoomIndex = 2; g.Editor.CursorX = 14; g.Editor.CursorY = 5;
        Tick(default, 1);
        Shot("35_editor_heights");
        g.Editor.HeightMode = false;
        g.GoToTitle();

        // stairs: up to the dais, the view from the top, the Keep terrace, and the floors layer in the editor
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
        g.OpenEditor();
        g.Editor.Load(MapDoc.FromDef(Maps.Hub[0]), "Opened");
        g.Editor.Mode = Editor.Layer.Floors; g.Editor.StairBrush = true; g.Editor.ShowHelp = false;
        g.Editor.ZoomIndex = 3; g.Editor.CursorX = 15; g.Editor.CursorY = 4;
        Tick(default, 1);
        Shot("40_editor_floors");
        g.Editor.Mode = Editor.Layer.Tiles;
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
        Tick(new Input { Jump = true, JumpHeld = true });
        Tick(new Input { JumpHeld = true }, 30);
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
        Tick(new Input { JumpHeld = false }, 1); PlaceCam(16.5f, 6.5f, 0, 3.4f, MathF.PI + 0.35f, 12);
        Shot("48_spire_climb");
        PlaceCam(9.3f, 10.75f, 8.5f, 0, -1.2f, -25);
        Tick(default, 1); PlaceCam(9.3f, 10.75f, 8.5f, 0, -1.2f, -25);
        Shot("49_spire_summit");
        g.SetArtStyle(ArtStyle.Fantasy);
        PlaceCam(10.2f, 14.2f, 0, 0, -MathF.PI / 2 - 0.5f, 60);
        Shot("50_windspire_fantasy");
        g.SetArtStyle(ArtStyle.SciFi);
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
