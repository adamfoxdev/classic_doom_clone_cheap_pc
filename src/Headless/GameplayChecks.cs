namespace HexenSharp;

/// <summary>Checks on play: combat and movement, the console, checkpoints, the jetpack, chests, bishops, relaxed mode and difficulty.</summary>
public static partial class Headless
{
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
        check(hub.Where(l => l != lv && l.RawName != "Hanging Cisterns").All(l => l.Checkpoints.Count == 0), "only the Windspire and the Hanging Cisterns have checkpoints");

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

        // Walking still collides with an enemy, but a slide can carry the player through it.
        g.Vars.Freeze = true;
        var enemy = new Monster(Monster.Ettin) { X = 3.2f, Y = 2.5f, Level = g.Level };
        g.Level.Things.Add(enemy);
        g.P.X = 2.5f; g.P.Y = 2.5f; g.P.Angle = 0; g.P.SlideTime = 0;
        Tick(new Input { Move = 1 }, 12);
        check(g.P.X < enemy.X - g.P.Radius, "walking remains blocked by enemies");
        g.P.X = 2.5f; g.P.SlideCd = 0;
        Tick(new Input { Move = 1, Slide = true }); Tick(new Input { Move = 1 }, 15);
        check(g.P.X > enemy.X + enemy.Radius, "sliding passes through enemies");
        g.Level.Things.Remove(enemy);
        g.P.X = 6.5f; g.P.Y = 2.5f; g.P.SlideTime = 0; g.P.SlideCd = 0;
        Tick(new Input { Move = 1, Slide = true }); Tick(new Input { Move = 1 }, 15);
        check(g.P.X > 6.5f && g.P.X <= 7f - g.P.Radius, "sliding still stops at walls");
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

    static void DifficultyChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        check(Difficulties.Of(g.Vars) == Difficulty.Normal, "the game starts on Normal");
        g.Con.Execute("difficulty easy");
        check(Difficulties.Of(g.Vars) == Difficulty.Easy && g.Vars.MonsterDamage == 0.5f && g.Vars.Damage == 1.25f && g.Vars.MonsterSpeed == 0.85f,
              "'difficulty easy': more damage dealt, half taken, slower monsters");
        g.Con.Execute("difficulty nightmare");
        check(g.Vars.MonsterDamage == 1.75f && g.Vars.MonsterSpeed == 1.35f && Settings.Lines(g).Contains("difficulty nightmare"), "'difficulty nightmare', saved with the settings");
        g.Con.Execute("monsterdamage 3");
        check(Difficulties.Of(g.Vars) == Difficulty.Custom && !Settings.Lines(g).Any(l => l.StartsWith("difficulty")), "a console tweak makes it Custom, which isn't saved");

        // the Options menu steps through the presets
        g.Menu.Show(MenuPage.Options);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Options), "Difficulty");
        check(g.Menu.Value(g.Menu.Cursor) == "CUSTOM", "Options shows Custom");
        var seen = new List<string>();
        foreach (var inp in new[] { new Input { Right = true }, new Input { Right = true }, new Input { Right = true }, new Input { Left = true }, new Input { Left = true }, new Input { Left = true }, new Input { Confirm = true } })
        {
            g.Menu.Update(inp, 1f / 35f);
            seen.Add(g.Menu.Value(g.Menu.Cursor));
        }
        check(seen.SequenceEqual(new[] { "NORMAL", "NIGHTMARE", "NIGHTMARE", "NORMAL", "EASY", "EASY", "NORMAL" }), $"Left/Right step Easy, Normal, Nightmare; Enter goes round ({string.Join(", ", seen)})");
        g.Menu.Close();

        // it takes effect: half damage on Easy
        g.Con.Execute("difficulty easy");
        g.NewGame(PClass.Fighter);
        g.P.Armor = 0;
        int hp = g.P.Health;
        g.DamagePlayer(20);
        check(hp - g.P.Health == 10, $"on Easy a 20-damage hit costs 10 ({hp - g.P.Health})");

        // arena scores: half on Easy, 1.5 times on Nightmare, and custom settings aren't recorded
        check(ArenaModInfo.Score(10, ArenaMod.None, Difficulty.Easy) == 500 && ArenaModInfo.Score(10, ArenaMod.None, Difficulty.Nightmare) == 1500,
              "arena scores: x0.5 on Easy, x1.5 on Nightmare");
        check(ArenaModInfo.Letters(ArenaMod.None, Difficulty.Nightmare) == "X" && ArenaModInfo.Letters(ArenaMod.MeleeOnly, Difficulty.Easy) == "ME" && ArenaModInfo.Letters(ArenaMod.None, Difficulty.Normal) == "-",
              "and the board marks them E and X");
        foreach (var (d, waves) in new[] { ("nightmare", 2), ("custom", 1) })
        {
            if (d == "custom") g.Con.Execute("monsterspeed 0.2"); else g.Con.Execute("difficulty " + d);
            g.GoToTitle();
            g.StartArena(PClass.Mage);
            g.Vars.God = true;
            var a = g.Level.Arena;
            var altar = g.Level.FindMark('!').Value;
            g.P.X = altar.x; g.P.Y = altar.y;
            for (int f = 0; f < 35 * 60 * 2 && a.BestWave < waves; f++) { g.Update(default, 1f / 35f); g.KillAll(); }
            g.GoToTitle();
        }
        var board = g.Profile.ArenaBoard(PClass.Mage);
        check(board.Count == 1 && board[0].Difficulty == (int)Difficulty.Nightmare && board[0].Score == 300, $"a Nightmare run scores 1.5 times; a custom one isn't recorded ({board.Count} runs)");
        g.Con.Execute("difficulty normal");
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
        check(classic.SecretsTotal == 6 && classic.LoreTotal == 25, $"6 secrets and 25 lore stones in the hub ({classic.SecretsTotal}, {classic.LoreTotal})");
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
}
