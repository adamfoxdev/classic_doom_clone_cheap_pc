namespace HexenSharp;

/// <summary>Checks on saving and continuing the campaign.</summary>
public static partial class Headless
{
    static void SaveChecks(Action<bool, string> check)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"hexensharp-save-{Environment.ProcessId}");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "save.json");
        Game Fresh() => new Game { FixedSeed = 1, SavePath = path, AchievementsOn = false };
        int Hub(Game gg, string name) => Array.FindIndex(gg.Hub, l => l.RawName == name);

        var g = Fresh();
        check(g.CheckSave() == null && !g.Menu.Items(MenuPage.Main).Contains("Continue"), "no save, no Continue on the title menu");
        g.NewGame(PClass.Mage);

        // change something of every kind, all over the hub
        var hall = g.Hub[0];
        int lever = Array.IndexOf(hall.Cells, 'L'), door = Array.IndexOf(hall.Cells, 'D'), secret = Array.IndexOf(hall.Cells, 'Z');
        hall.PulledLevers.Add(lever); hall.DoorOpen[door] = 1f; hall.SecretsFound.Add(secret); hall.DoorOpen[secret] = 1f;
        for (int i = 0; i < hall.Seen.Length; i += 3) hall.Seen[i] = true;
        var monsters = hall.Things.OfType<Monster>().ToList();
        g.Level = hall;
        g.DamageMonster(monsters[0], 100000, 0);
        monsters[1].Health = 13; monsters[1].State = AiState.Chase; monsters[1].X += 0.3f;
        hall.Things.OfType<Pickup>().First().Removed = true;
        if (hall.Things.OfType<Chest>().FirstOrDefault() is { } chest) chest.Opened = true;
        var lore = hall.Things.OfType<LoreStone>().First(); lore.Read = true;

        var quarry = g.Hub[Hub(g, "Deepdelve Quarry")];
        int rubble = Array.IndexOf(quarry.Cells, Level.Rubble);
        quarry.DamageBlock(rubble % quarry.W, rubble / quarry.W, 100000);
        var crypt = g.Hub[Hub(g, "Darkmere Crypt")];
        int block = Array.IndexOf(crypt.Cells, 'X');
        crypt.Cells[block] = '\0'; crypt.Cells[block - 1] = 'X';
        var depths = g.Hub[Hub(g, "Bedrock Depths")];
        int dig = Enumerable.Range(0, depths.Cells.Length).First(i => depths.CanDig(i % depths.W, i / depths.W, Level.Face.Floor));
        depths.DamageBlock(dig % depths.W, dig / depths.W, 100000, Level.Face.Floor);
        var spire = g.Hub[Hub(g, "Windspire")];
        spire.CheckpointsReached.Add(2);
        g.Checkpoint = new Checkpoint { Level = spire, Index = 2, X = 3.5f, Y = 2.5f, Floor = 5.5f, Angle = 1.2f, Health = 80, Armor = 20 };
        var barren = g.Hub[Hub(g, "Barren World")];
        barren.Ship.Delivered[0] = 4; barren.Ship.Delivered[2] = 1;
        var moon = g.Hub[Hub(g, "Verdant Moon")];
        var queen = moon.Things.OfType<Monster>().First(m => m.Def == MiniBosses.Thornmother);
        var brood = new Monster(Monster.Afrit) { X = queen.X + 1, Y = queen.Y, Level = moon, Summoner = queen, State = AiState.Chase };
        moon.Things.Add(brood);

        g.Level = g.Hub[Hub(g, "Frozen Keep")];
        var p = g.P;
        p.X = 5.5f; p.Y = 3.5f; p.Angle = 0.7f; p.Pitch = 5; p.Health = 57; p.Armor = 33; p.BlueMana = 71; p.GreenMana = 12;
        p.Flasks = 2; p.Urns = 1; p.Kills = 9; p.Relics = 0; p.LoreRead = 1; p.Secrets = 1; p.Blocks = 5; p.Ore[1] = 3;
        p.HasWeapon[1] = true; p.Weapon = 1; p.SteelKey = true; p.HasJetpack = true; p.Fuel = 2.5f;
        g.PlayTime = 1234.5f; g.RunDeaths = 1;

        check(g.SaveNow() && File.Exists(path), "a campaign game saves");
        var saved = g;

        // pick it up in a brand new game, from the title menu
        var h = Fresh();
        check(h.Menu.Items(MenuPage.Main)[0] == "Continue" && h.CheckSave().Summary().Contains(ClassDef.All[(int)PClass.Mage].Name.ToUpperInvariant()),
              $"then Continue heads the title menu, saying where you are ({h.CheckSave().Summary()})");
        h.Menu.Cursor = 0;
        h.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(h.Mode == GameMode.Playing && h.P.Class == PClass.Mage && h.Level.RawName == "Frozen Keep", "and puts you back in the game where you were");

        bool levelsSame = true;
        var differs = new List<string>();
        for (int i = 0; i < h.Hub.Length; i++)
        {
            Level a = saved.Hub[i], b = h.Hub[i];
            bool same = a.Cells.SequenceEqual(b.Cells) && a.Floors.SequenceEqual(b.Floors) && a.Heights.SequenceEqual(b.Heights)
                && a.DoorOpen.SequenceEqual(b.DoorOpen) && a.Seen.SequenceEqual(b.Seen) && a.Outdoor.SequenceEqual(b.Outdoor)
                && a.PulledLevers.SetEquals(b.PulledLevers) && a.SecretsFound.SetEquals(b.SecretsFound) && a.CheckpointsReached.SetEquals(b.CheckpointsReached)
                && a.BlockHp.SequenceEqual(b.BlockHp) && a.FloorHp.SequenceEqual(b.FloorHp);
            string Things(Level l) => string.Join(";", l.Things.Where(t => !t.Removed && t is not (Projectile or Puff)).Select(t => t switch
            {
                Monster m => $"M{m.Def.Name}{m.Alive}{m.Health}{m.X:0.00}{m.Y:0.00}{(m.Alive ? m.State.ToString() : "")}",
                Pickup pk => $"P{pk.Kind}{pk.X:0.00}{pk.Name}",
                Chest c => $"C{c.Opened}{c.X:0.00}",
                LoreStone ls => $"L{ls.Read}{ls.Index}",
                Ship sh => $"S{string.Join(",", sh.Delivered)}",
                _ => t.GetType().Name + $"{t.X:0.00}{t.Y:0.00}",
            }).OrderBy(x => x));
            if (!same || Things(a) != Things(b)) { levelsSame = false; differs.Add(a.RawName); }
        }
        check(levelsSame, "every map comes back as it was: doors, levers, rubble, blocks, dug floors, the automap, monsters, pickups, chests, lore, the ship" + (differs.Count > 0 ? $" (differs: {string.Join(", ", differs)})" : ""));
        var q = h.P;
        check(q.X == p.X && q.Y == p.Y && q.Health == 57 && q.Armor == 33 && q.BlueMana == 71 && q.GreenMana == 12 && q.Flasks == 2 && q.Urns == 1
              && q.Kills == 9 && q.Blocks == 5 && q.Ore[1] == 3 && q.HasWeapon[1] && q.Weapon == 1 && q.SteelKey && !q.FireKey && q.HasJetpack && q.Fuel == 2.5f,
              "and so do you: health, armor, mana, items, weapons, keys, ore and the jetpack");
        check(h.Checkpoint is { Index: 2, Floor: 5.5f } c2 && c2.Level == h.Hub[Hub(h, "Windspire")] && h.PlayTime == 1234.5f && h.RunDeaths == 1
              && h.SecretsTotal == saved.SecretsTotal && h.ChestsTotal == saved.ChestsTotal,
              "your checkpoint, play time and the game's totals");
        check(h.Hub[Hub(h, "Barren World")].Ship is { } ship && ship.Delivered[0] == 4 && h.Hub[Hub(h, "Barren World")].Things.Contains(ship), "the ship's repairs");
        var hmoon = h.Hub[Hub(h, "Verdant Moon")];
        check(hmoon.Things.OfType<Monster>().Any(m => m.Summoner != null && m.Summoner.Def == MiniBosses.Thornmother), "even whose brood is whose");
        var r = new Renderer();
        for (int k = 0; k < 35 * 3; k++) h.Update(new Input { Move = k % 20 < 10 ? 1 : -1 }, 1f / 35f);
        r.Render(h);
        check(h.Mode == GameMode.Playing, "and the game carries on from there");

        // when it saves by itself: entering a map, a checkpoint, every two minutes, and leaving
        var a2 = Fresh();
        a2.NewGame(PClass.Fighter);
        a2.Level.Things.RemoveAll(t => t is Monster);
        var portal = a2.Level.FindMark('1').Value;
        a2.P.X = portal.x; a2.P.Y = portal.y; a2.P.PortalLock = false;
        a2.Update(default, 1f / 35f);
        check(a2.Level.RawName == "Frozen Keep" && SaveGame.Load(path) is { } s1 && a2.Hub[s1.LevelIndex].RawName == "Frozen Keep", "it saves as you arrive in a map");
        var cont = Fresh();
        cont.Continue();
        check(cont.P.PortalLock && cont.Level.RawName == "Frozen Keep", "continuing on the portal you came through doesn't send you straight back");
        File.WriteAllText(path, "{}");
        for (int k = 0; k < (int)(35 * (Game.AutoSaveEvery + 1)); k++) a2.Update(default, 1f / 35f);
        check(SaveGame.Load(path) != null, $"and every {Game.AutoSaveEvery / 60:0} minutes of play");
        a2.P.X += 1; a2.GoToTitle();
        check(SaveGame.Load(path)?.Player.X == a2.P.X, "and when you quit to the title");

        // what isn't saved
        var other = Fresh();
        File.Delete(path);
        other.StartPractice(PClass.Fighter);
        bool practice = other.SaveNow();
        other.StartArena(PClass.Fighter);
        bool arena = other.SaveNow();
        other.GoToTitle();
        other.NewGame(PClass.Fighter);
        other.Warp(Array.FindIndex(other.Hub, l => l.Flight));
        bool flight = other.SaveNow();
        other.Warp(0);
        other.DamagePlayer(100000);
        bool dead = other.SaveNow();
        check(!practice && !arena && !flight && !dead && !File.Exists(path), "practice, the arena, the Void Crossing and being dead don't save");

        // a win ends the campaign; a damaged save, or one from different maps, isn't used (or lost)
        var w = Fresh();
        w.NewGame(PClass.Fighter);
        w.SaveNow();
        check(File.Exists(path), "(saved)");
        w.Level.BossDead = true;
        var exit = w.Level.FindMark('E').Value;
        w.P.X = exit.x; w.P.Y = exit.y;
        w.Update(default, 1f / 35f);
        check(w.Mode == GameMode.Victory && !File.Exists(path) && w.CheckSave() == null, "winning ends the campaign: the save goes, and so does Continue");
        File.WriteAllText(path, "not a save at all {");
        check(Fresh().CheckSave() == null, "a damaged save is ignored");
        var odd = Saves.Capture(saved);
        odd.Levels.RemoveAt(odd.Levels.Count - 1);
        odd.Save(path);
        var f = Fresh();
        bool ok = f.Continue();
        check(!ok && f.Mode == GameMode.Title && SaveGame.Load(path)?.Levels.Count == odd.Levels.Count, "a save from different maps won't load, and isn't overwritten");
        var style = Fresh();
        style.CheckSave();
        style.Menu.Show(MenuPage.Style);
        r.Render(style);
        check(r.Fb.Count(px => px == Col.Rgb(255, 170, 90)) > 30, "with a save, New game warns it'll be replaced");

        // relaxed games keep their relics' names
        var rel = Fresh();
        rel.Style = GameStyle.Relaxed;
        rel.NewGame(PClass.Cleric);
        var names = rel.Hub.SelectMany(l => l.Things.OfType<Pickup>()).Where(pk => pk.Kind == PickupKind.Relic).Select(pk => pk.Name).OrderBy(n => n).ToList();
        rel.SaveNow();
        var rel2 = Fresh();
        rel2.Continue();
        check(rel2.Relaxed && rel2.Hub.SelectMany(l => l.Things.OfType<Pickup>()).Where(pk => pk.Kind == PickupKind.Relic).Select(pk => pk.Name).OrderBy(n => n).SequenceEqual(names),
              "a relaxed game comes back relaxed, its relics where they were and named as they were");
        Directory.Delete(dir, true);
    }
}
