namespace HexenSharp;

/// <summary>Checks on New Game+: unlocking it, what a tier does to the hub, and that it survives a save.</summary>
public static partial class Headless
{
    static void NgPlusChecks(Action<bool, string> check)
    {
        static void Win(Game w)
        {
            w.Level = w.Hub[0];
            w.Level.BossDead = true;
            var ex = w.Level.FindMark('E').Value;
            w.P.X = ex.x; w.P.Y = ex.y;
            w.Update(default, 1f / 35f);
        }

        var g = new Game { FixedSeed = 1, Profile = new Profile { Level = 7 }, AchievementsOn = false };
        check(!g.Menu.Items(MenuPage.Main).Contains("New Game+"), "no New Game+ on the title before you've won");
        g.NewGame(PClass.Cleric);
        check(g.NgTier == 0, "a first campaign is tier 0");
        var plain = g.Hub.Select(l => l.Things.OfType<Pickup>().Select(p => (p.X, p.Y, p.Kind)).ToList()).ToList();
        int plainChests = g.ChestsTotal;
        Win(g);
        check(g.Mode == GameMode.Victory && g.OffersNgPlus && g.Profile.NgUnlocked == 1, "a classic win opens New Game+ and the victory screen offers it");
        check(g.Menu.Items(MenuPage.Main).Contains("New Game+"), "and the title menu lists it from then on");

        int level = g.Profile.Level;
        g.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Mode == GameMode.Playing && g.NgTier == 1 && g.P.Class == PClass.Cleric && g.Profile.Level == level,
              "Enter starts New Game+ as the same class, keeping your level");

        // tougher monsters
        var ms = g.Hub.SelectMany(l => l.Things.OfType<Monster>()).Where(m => m.Affix == Affix.None).ToList(); // elites are tougher still
        check(ms.Count > 0 && ms.All(m => m.Health == (int)(m.Def.Health * 1.5f) && m.MaxHealth == m.Health && Math.Abs(m.DamageMult - 1.25f) < 0.01f && m.SpeedMult > 1.05f),
              "every monster has 50% more health, hits 25% harder and moves faster");
        var keeper = ms.First(m => m.Def == MiniBosses.Keeper);
        check(keeper.NextBlinkHp == (int)(keeper.MaxHealth * 0.8f), "the Drowned Keeper blinks at the same share of its (bigger) health");

        // remixed loot: the same spots on each map, with the supplies shuffled between them; keys and the like stay put
        var remixed = g.Hub.Select(l => l.Things.OfType<Pickup>().Select(p => (p.X, p.Y, p.Kind)).ToList()).ToList();
        bool sameSpots = true, fixedStay = true, anyMoved = false;
        for (int i = 0; i < plain.Count; i++)
        {
            sameSpots &= plain[i].Select(p => (p.X, p.Y)).OrderBy(p => p).SequenceEqual(remixed[i].Select(p => (p.X, p.Y)).OrderBy(p => p));
            foreach (var p in plain[i])
            {
                var now = remixed[i].First(r => r.X == p.X && r.Y == p.Y).Kind;
                if (p.Kind is PickupKind.SteelKey or PickupKind.FireKey or PickupKind.Weapon2 or PickupKind.Weapon3 or PickupKind.Jetpack) fixedStay &= now == p.Kind;
                anyMoved |= now != p.Kind;
            }
        }
        check(sameSpots && fixedStay && anyMoved, "the loot is remixed: supplies change places, keys and weapons stay where they are");
        check(g.ChestsTotal > plainChests, $"and there are more chests ({g.ChestsTotal} against {plainChests})");

        // more XP a kill
        var ettin = g.Hub[0].Things.OfType<Monster>().First(m => m.Def == Monster.Ettin);
        g.Level = g.Hub[0];
        int before = g.RunXp;
        g.DamageMonster(ettin, 100000, 0);
        check(g.RunXp - before == (int)(Game.Xp.Kill(Monster.Ettin) * 1.25f), "a kill pays a quarter more XP");

        // a New Game+ campaign saves and continues as one
        string dir = Path.Combine(Path.GetTempPath(), $"hexensharp-ng-{Environment.ProcessId}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "save.json");
        g.SavePath = path;
        var hurt = g.Hub[0].Things.OfType<Monster>().First(m => m.Alive && m.Affix == Affix.None);
        hurt.Health = 20;
        check(g.SaveNow(), "(saved)");
        var back = new Game { FixedSeed = 2, SavePath = path, Profile = g.Profile, AchievementsOn = false };
        check(back.Continue() && back.NgTier == 1, "Continue picks the New Game+ tier back up");
        var bm = back.Hub[0].Things.OfType<Monster>().First(m => m.Alive && m.Health == 20);
        check(bm.MaxHealth == (int)(bm.Def.Health * 1.5f) && Math.Abs(bm.DamageMult - 1.25f) < 0.01f, "with its monsters as tough as they were");
        check(back.CheckSave()!.Summary().StartsWith("NEW GAME+ "), "and the title's Continue line says it's New Game+");

        // winning a tier opens the next, and earns Once More, With Feeling
        back.AchievementsOn = true;
        Win(back);
        check(back.Profile.NgUnlocked == 2 && back.Profile.NgBest == 1 && back.Profile.Achievements.ContainsKey("ng_plus"),
              "winning New Game+ opens tier 2 and earns Once More, With Feeling");
        back.Update(new Input { Pause = true }, 1f / 35f);
        check(back.Mode == GameMode.Title, "Esc on the victory screen goes back to the title instead");
        Directory.Delete(dir, true);

        // from the title: New Game+ starts the highest tier opened; New game is a plain one again
        var t = new Game { FixedSeed = 1, Profile = new Profile { NgUnlocked = 2 }, AchievementsOn = false };
        t.Menu.Show(MenuPage.Main);
        t.Menu.Cursor = Array.IndexOf(t.Menu.Items(MenuPage.Main), "New Game+");
        t.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(t.Mode == GameMode.ClassSelect && t.NgTier == 2, "the title's New Game+ goes to the class choice at the highest tier");
        t.Update(new Input { Slot = 2 }, 1f / 35f);
        check(t.Mode == GameMode.Playing && t.NgTier == 2 && t.P.Class == PClass.Cleric && t.Hub[0].Things.OfType<Monster>().Where(m => m.Affix == Affix.None).All(m => m.Health == m.Def.Health * 2),
              "and starts it: monsters with double health at tier 2");
        t.GoToTitle();
        t.Menu.Show(MenuPage.Main);
        t.Menu.Cursor = Array.IndexOf(t.Menu.Items(MenuPage.Main), "New game");
        t.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        t.Menu.Cursor = 0;
        t.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(t.Mode == GameMode.ClassSelect && t.NgTier == 0, "New game is a first campaign again");

        // only the classic campaign: practice, the arena and a relaxed win don't touch it
        var r = new Game { FixedSeed = 1, Profile = new Profile(), AchievementsOn = false, Style = GameStyle.Relaxed };
        r.NewGame(PClass.Mage);
        r.P.Relics = r.RelicsTotal;
        Win(r);
        check(r.Mode == GameMode.Victory && !r.OffersNgPlus && r.Profile.NgUnlocked == 0, "a relaxed win doesn't open New Game+");
        var pr = new Game { FixedSeed = 1, Profile = new Profile { NgUnlocked = 3 }, AchievementsOn = false, NgTier = 3 };
        pr.StartPractice(PClass.Fighter, Courses.Hangar);
        check(pr.NgTier == 0, "practice courses are never New Game+");
        pr.NgTier = 3;
        pr.StartArena(PClass.Fighter);
        check(pr.NgTier == 0 && pr.Level.Things.OfType<Monster>().All(m => m.MaxHealth == m.Def.Health), "nor is the arena");
    }
}
