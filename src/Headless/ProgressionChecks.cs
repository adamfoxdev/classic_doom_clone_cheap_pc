namespace HexenSharp;

/// <summary>Checks on progression: levels and skills, achievements and Story mode.</summary>
public static partial class Headless
{
    static void StoryChecks(Action<bool, string> check)
    {
        check(Story.Cases.Length >= 3, $"{Story.Cases.Length} cases around {Story.Town}");
        foreach (var c in Story.Cases)
        {
            var lv = c.Map.Build();
            var reach = lv.Reachable((int)lv.StartX, (int)lv.StartY);
            var spots = c.Suspects.Select(s => (s.X, s.Y, s.Name)).Concat(c.Clues.Select(k => (k.X, k.Y, k.Name))).ToList();
            var bad = spots.Where(p => lv.BlocksPoint(p.X, p.Y) || !reach[(int)p.Y * lv.W + (int)p.X]).Select(p => p.Name).ToList();
            check(bad.Count == 0, $"{c.Title}: every suspect and clue is on open, reachable floor" + (bad.Count > 0 ? ": " + string.Join(", ", bad) : ""));
            var ids = c.Clues.Select(k => k.Id).ToHashSet();
            check(c.Suspects.Count(s => s.Culprit) == 1 && c.Keys.All(ids.Contains) && c.Insights.All(i => ids.Contains(i.A) && ids.Contains(i.B)),
                  $"{c.Title}: one culprit, and the key evidence and patterns are real clues");
            var culprit = c.Suspects.First(s => s.Culprit);
            check(c.Keys.Any(k => culprit.About(k).StartsWith('!')) && c.Suspects.Where(s => !s.Culprit).All(s => ids.All(k => !s.About(k).StartsWith('!'))),
                  $"{c.Title}: only the culprit lies, and a key clue catches them out");
            check(c.Suspects.All(s => ids.All(s.OnClue.ContainsKey)), $"{c.Title}: everyone has something to say about every clue");
        }

        var g = new Game { FixedSeed = 1 };
        void Tick(Input i, int frames = 1) { for (int k = 0; k < frames; k++) g.Update(i, 1f / 35f); }
        g.Menu.Show(MenuPage.Main);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Main), "Story");
        Tick(new Input { Confirm = true });
        check(g.StoryMode && g.StoryCase == 0 && g.Story != null && g.ReadingLore != null && g.ReadingLore.Contains("Missing Shipment"),
              "Story on the title menu opens the first case with its brief");
        Tick(new Input { Confirm = true });
        var s = g.Story;
        var map = g.Level;
        check(map.Things.OfType<Npc>().Count() == 3 && map.Things.OfType<ClueMark>().Count() == 5 && !map.Things.Any(t => t is Monster or Chest),
              "the case map has its suspects and clues, and nothing to fight");
        Tick(new Input { Fire = true }, 10);
        check(!map.Things.Any(t => t is Projectile), "a private eye keeps the gun holstered");

        // walk up to someone and press E to question them
        var dex = map.Things.OfType<Npc>().First(n => n.S.Name == "Dex Kollins");
        g.P.X = dex.X - 1f; g.P.Y = dex.Y; g.P.Angle = 0;
        Tick(new Input { Use = true });
        check(s.Talking == dex && s.Line == dex.S.Greeting, "E questions the person you're facing");
        var opts = Story.Options(s);
        check(opts.Select(o => o.key).SequenceEqual(new[] { "alibi", "accuse", "bye" }), "with no clues yet you can only ask their alibi, accuse or leave");
        s.Cursor = 0; Tick(new Input { Confirm = true });
        check(s.Line == dex.S.Alibi && s.Journal.Any(j => j.text.Contains("Didn't see a thing")), "alibis go in your journal");
        Tick(new Input { Pause = true });
        check(s.Talking == null, "Esc ends the conversation");

        // find clues, then press the guard about the gate log
        foreach (var id in new[] { "gate", "tab" })
            Story.Examine(g, map.Things.OfType<ClueMark>().First(m => m.C.Id == id));
        check(s.Found.SetEquals(new[] { "gate", "tab" }) && g.ReadingLore != null, "examining a clue reads it and adds it to the case");
        g.ReadingLore = null;
        Story.Talk(g, dex);
        opts = Story.Options(s);
        check(opts.Count == 5 && opts.Any(o => o.key == "clue:gate"), "each clue you've found is something to ask about");
        Story.Choose(g, "clue:gate");
        check(s.Contradictions.Count == 1 && s.Journal.Any(j => j.flag && j.text.Contains("doesn't add up")), "a lie the evidence gives away is flagged in the journal");
        Story.Choose(g, "accuse");
        check(!s.Solved && s.Strikes == 0 && s.Line.Contains("nothing on me"), "accusing the right person without proof gets you nowhere, but costs nothing");
        Story.Choose(g, "bye");

        // a wrong accusation is a strike
        var mara = map.Things.OfType<Npc>().First(n => n.S.Name == "Mara Voss");
        Story.Talk(g, mara);
        Story.Choose(g, "accuse");
        check(s.Strikes == 1 && !s.Solved, "accusing the wrong person is a strike");
        Story.Choose(g, "bye");

        // the journal
        Tick(new Input { Journal = true });
        check(s.JournalOpen, "J opens the journal");
        Tick(new Input { Journal = true });
        check(!s.JournalOpen, "and closes it");

        // the note completes the pattern: now the accusation sticks
        Story.Examine(g, map.Things.OfType<ClueMark>().First(m => m.C.Id == "note"));
        g.ReadingLore = null;
        check(s.Insights.Count == 1 && s.Journal.Any(j => j.flag && j.text.StartsWith("Pattern:")) && s.HasKeys, "finding both halves of a pattern notes it");
        Story.Talk(g, dex);
        Story.Choose(g, "accuse");
        check(s.Solved && s.Line == Story.Cases[0].Solved, "with the evidence, the culprit confesses");
        Story.Choose(g, "bye");
        check(g.StoryCase == 1 && g.Story.Case == Story.Cases[1] && g.Story.Strikes == 0, "closing the case brings the next job");
        g.ReadingLore = null;

        // three wrong calls and the case goes cold
        var innocent = g.Level.Things.OfType<Npc>().First(n => !n.S.Culprit);
        var cold = g.Story;
        Story.Examine(g, g.Level.Things.OfType<ClueMark>().First());
        g.ReadingLore = null;
        for (int k = 0; k < 3; k++) { Story.Talk(g, innocent); Story.Choose(g, "accuse"); }
        check(g.Story != cold && g.Story.Strikes == 0 && g.Story.Found.Count == 0 && g.StoryCase == 1, "three strikes and the case goes cold: start it over");
        g.ReadingLore = null;

        // solve the rest and the story ends
        for (int ci = g.StoryCase; ci < Story.Cases.Length; ci++)
        {
            var cs = g.Story;
            foreach (var m in g.Level.Things.OfType<ClueMark>().ToList()) Story.Examine(g, m);
            g.ReadingLore = null;
            var culprit = g.Level.Things.OfType<Npc>().First(n => n.S.Culprit);
            Story.Talk(g, culprit);
            Story.Choose(g, "accuse");
            check(cs.Solved, $"case {ci + 1} ({cs.Case.Title}) can be solved");
            Story.Choose(g, "bye");
            g.ReadingLore = null;
        }
        check(g.Mode == GameMode.Victory && g.StoryMode, "closing the last case ends the story");
        Tick(new Input { Confirm = true });
        check(g.Mode == GameMode.Title && !g.StoryMode, "and Enter goes back to the title");
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
        check(ui.Menu.Items(MenuPage.Character).SequenceEqual(new[] { "Vitality", "Power", "Agility", "Focus", "Thrusters", "Achievements", "Back" }), "it lists the five skills, then Achievements");
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
        int achieved = win.Profile.Achievements.Keys.Sum(id => Achievements.Find(id).Xp); // the first win unlocks a few
        check(win.Mode == GameMode.Victory && win.Profile.Wins == 1 && win.Profile.TotalXp - achieved == Game.Xp.Victory, $"winning adds a win and {Game.Xp.Victory} XP");

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

    static void AchievementChecks(Action<bool, string> check)
    {
        var all = Achievements.All;
        check(all.Length >= 20 && all.Select(a => a.Id).Distinct().Count() == all.Length && all.All(a => a.Xp > 0 && a.Name.Length <= 26),
              $"{all.Length} achievements, each with its own id and some experience");

        // the first kill with a weapon: First Blood, its experience, a message and a banner, once
        var g = new Game { FixedSeed = 1 };
        g.Update(default, 1f / 35f);
        check(g.Profile.Achievements.Count == 0, "a new profile has none");
        g.NewGame(PClass.Fighter);
        var p = g.P;
        g.Level.Things.RemoveAll(t => t is Monster);
        var ettin = new Monster(Monster.Ettin) { X = p.X + 0.9f, Y = p.Y, Level = g.Level, Health = 1 };
        g.Level.Things.Add(ettin);
        p.Angle = 0;
        int xp0 = g.Profile.TotalXp;
        for (int f = 0; f < 35 && ettin.Alive; f++) g.Update(new Input { Fire = true }, 1f / 35f);
        for (int f = 0; f < 10; f++) g.Update(default, 1f / 35f);
        check(!ettin.Alive && g.Profile.Achievements.ContainsKey("first_blood") && g.AchievementBanner?.Id == "first_blood"
              && g.Messages.Any(m => m.text == "Achievement unlocked: First Blood! +25 XP"), "a first kill unlocks First Blood, with a message and a banner");
        check(g.Profile.TotalXp - xp0 == Game.Xp.Kill(Monster.Ettin) + 25, "and pays its 25 XP on top of the kill's");
        var r = new Renderer();
        r.Render(g);
        check(r.Fb.Count(px => px == Col.Rgb(230, 190, 80)) > 150, "the banner shows over the view");
        int xp1 = g.Profile.TotalXp;
        Achievements.Check(g);
        check(g.Profile.TotalXp == xp1, "each one pays once");

        // cheats: nothing unlocks for the rest of that game
        p.VX = 2.6f * g.RunSpeed; p.VY = 0;
        g.Con.Execute("god"); g.Con.Execute("god");
        Achievements.Check(g);
        check(g.Cheated && !g.Profile.Achievements.ContainsKey("speed"), "using a cheat (even switched off again) blocks achievements that game");
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster);
        g.Con.Execute("set fov 90");
        g.Con.Execute("sens 2");
        check(!g.Cheated, "a new game clears it; looks and feel settings aren't cheats");
        g.Con.Execute("set gravity 4");
        check(g.Cheated, "but gameplay ones are");
        g.Con.Execute("set gravity 12");
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster);

        // speed, secrets and lore in the game you're playing
        g.P.VX = 2.6f * g.RunSpeed; g.P.VY = 0;
        Achievements.Check(g);
        check(g.Profile.Achievements.ContainsKey("speed"), "Speed Demon: 250% of your run speed");
        var sec = Achievements.Find("secrets");
        check(sec.Progress(g) == (0, g.SecretsTotal), $"progress shows how far along you are (0 of {g.SecretsTotal} secrets)");
        g.P.Secrets = g.SecretsTotal; g.P.LoreRead = g.LoreTotal;
        Achievements.Check(g);
        check(g.Profile.Achievements.ContainsKey("secrets") && g.Profile.Achievements.ContainsKey("lore"), "every secret, every lore stone in one game");
        var pr = new Game { FixedSeed = 1 };
        pr.StartPractice(PClass.Fighter, Courses.Hangar);
        pr.P.Secrets = 99; pr.P.LoreRead = 99;
        Achievements.Check(pr);
        check(!pr.Profile.Achievements.ContainsKey("secrets"), "which a practice course's own counts don't satisfy");

        // ones your profile has already earned unlock at the title
        var old = new Game { FixedSeed = 1, Profile = new Profile { TotalKills = 612, Level = 12 } };
        old.Update(default, 1f / 35f);
        check(new[] { "first_blood", "slayer", "veteran" }.All(old.Profile.Achievements.ContainsKey), "a profile that already has 600 kills and level 12 gets theirs straight away");

        // winning: classic, flawless, nightmare, relaxed, every class
        Game Win(PClass cls, GameStyle style = GameStyle.Classic, bool die = false, string difficulty = "normal")
        {
            var w = new Game { FixedSeed = 1, Profile = new Profile() };
            w.Con.Execute("difficulty " + difficulty, quiet: true);
            w.Style = style;
            w.NewGame(cls);
            w.Level.Things.RemoveAll(t => t is Monster);
            if (die) { w.Checkpoint = new Checkpoint { Level = w.Level, X = w.P.X, Y = w.P.Y, Health = 100 }; w.DamagePlayer(100000); w.Update(new Input { Confirm = true }, 1f / 35f); for (int f = 0; f < 200 && w.Mode == GameMode.Dead; f++) w.Update(new Input { Confirm = f % 2 == 0 }, 1f / 35f); }
            if (style == GameStyle.Relaxed) w.P.Relics = w.RelicsTotal;
            w.Level = w.Hub[0];
            w.Level.BossDead = true;
            var ex = w.Level.FindMark('E').Value;
            w.P.X = ex.x; w.P.Y = ex.y;
            w.Update(default, 1f / 35f);
            return w;
        }
        var w1 = Win(PClass.Fighter);
        check(w1.Mode == GameMode.Victory && w1.Profile.Achievements.ContainsKey("heresiarch") && w1.Profile.Achievements.ContainsKey("flawless")
              && !w1.Profile.Achievements.ContainsKey("nightmare"), "a classic win without dying: Heresiarch Slain and Untouchable");
        var w2 = Win(PClass.Fighter, die: true);
        check(w2.Profile.ClassicWins == 1 && w2.RunDeaths == 1 && !w2.Profile.Achievements.ContainsKey("flawless"), "a win after dying isn't Untouchable");
        var w3 = Win(PClass.Mage, difficulty: "nightmare");
        check(w3.Profile.Achievements.ContainsKey("nightmare"), "a win on Nightmare is Nightmare Walker");
        var w4 = Win(PClass.Cleric, GameStyle.Relaxed);
        check(w4.Profile.Achievements.ContainsKey("pilgrim") && !w4.Profile.Achievements.ContainsKey("heresiarch"), "a relaxed win is Pilgrim");
        var prof = new Profile { ClassWins = new() { "Fighter", "Cleric" } };
        var three = new Game { FixedSeed = 1, Profile = prof };
        check(Achievements.Find("all_classes").Progress(three) == (2, 3), "Jack of All Trades counts the classes you've won as");
        prof.ClassWins.Add("Mage");
        Achievements.Check(three);
        check(prof.Achievements.ContainsKey("all_classes"), "and unlocks with the third");

        // practice medals and the arena
        var med = new Game { FixedSeed = 1 };
        med.Profile.AddCourseRun(Courses.Hangar.Key(PClass.Fighter), 18f, "A", DateTime.Now);
        Achievements.Check(med);
        check(med.Profile.Achievements.ContainsKey("podium") && !med.Profile.Achievements.ContainsKey("gold_all"), "a bronze is On the Podium");
        foreach (var c in Courses.Timed) med.Profile.AddCourseRun(c.Key(PClass.Fighter), c.MedalTimes(PClass.Fighter).gold - 0.1f, "A", DateTime.Now);
        Achievements.Check(med);
        check(med.Profile.Achievements.ContainsKey("gold_all"), "gold on every timed course is Gold Standard");
        var ar = new Game { FixedSeed = 1 };
        ar.ArenaMods = ArenaMod.DoubleSpeed | ArenaMod.NoSupplies | ArenaMod.MeleeOnly;
        ar.StartArena(PClass.Fighter);
        ar.Level.Arena.Started = true; ar.Level.Arena.BestWave = 5;
        Achievements.Check(ar);
        check(ar.Profile.Achievements.ContainsKey("arena_5") && ar.Profile.Achievements.ContainsKey("arena_mods") && !ar.Profile.Achievements.ContainsKey("arena_20"),
              "clearing wave 5 with three modifiers: Gladiator and Glutton for Punishment");

        // kept with the profile, and listed on the Character screen
        var back = System.Text.Json.JsonSerializer.Deserialize<Profile>(ar.Profile.ToJson());
        check(back.Achievements.ContainsKey("arena_mods"), "saved with your profile");
        ar.Paused = true; ar.Menu.Show(MenuPage.Character);
        ar.Menu.Cursor = Array.IndexOf(ar.Menu.Items(MenuPage.Character), "Achievements");
        ar.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(ar.Menu.Page == MenuPage.Achievements, "Character > Achievements opens the list");
        for (int k = 0; k < all.Length; k++) ar.Menu.Update(new Input { Down = true }, 1f / 35f);
        check(ar.Menu.Cursor == all.Length && ar.Menu.Scroll == all.Length - MenuSystem.AchievementRows, "it scrolls to the end");
        r.Render(ar);
        ar.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(ar.Menu.Page == MenuPage.Character, "and Back returns to the Character screen");
    }
}
