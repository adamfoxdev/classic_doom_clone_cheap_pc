namespace HexenSharp;

/// <summary>Checks on the Quake modes: rail trials on the range, and Instagib and Rocket Arena in the Chaos Arena.</summary>
public static partial class Headless
{
    static void QuakeModeChecks(Action<bool, string> check)
    {
        Game g = null;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }
        void Aim(Thing t)
        {
            g.P.Angle = MathF.Atan2(t.Y - g.P.Y, t.X - g.P.X);
            g.P.Pitch = 0;
        }

        // rail trials: Use with the railgun in hand; targets pop up far off, and a quick hit scores more
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.StartPractice(PClass.Mage, ShootingRange.Course);
        var p = g.P;
        g.GiveExtra(Railgun.Gun);
        Tick(new Input(), 20);
        p.X = 20.5f; p.Y = 22.5f; p.FloorZ = 0;
        Tick(new Input { Use = true });
        check(g.RailTrial && !g.Drilling && g.TrialLeft > 29, "on the range, Use with the railgun in hand starts a rail trial");
        Monster Next() { for (int k = 0; k < 70; k++) { var t = g.Level.Things.OfType<Monster>().FirstOrDefault(m => m.Target?.Kind == ShootingRange.Kind.Trial && m.Alive); if (t != null) return t; Tick(new Input()); } return null; }
        var target = Next();
        check(target != null && Game.Dist(target.X, target.Y, p.X, p.Y) > 12, $"a target pops up far down the field ({(target == null ? 0 : Game.Dist(target.X, target.Y, p.X, p.Y)):0} cells off)");
        Aim(target); p.Cooldown = 0;
        Tick(new Input { Fire = true });
        int first = g.TrialScore;
        check(!target.Alive && first > 150, $"hit at once, it scores {first} (100, and up to 100 more for speed)");
        var second = Next();
        p.Angle = MathF.PI / 2; p.Cooldown = 0; // miss: fire back at the rack
        Tick(new Input { Fire = true });
        check(g.TrialShots == 2 && g.TrialHits == 1, "a slug that misses counts against your accuracy");
        g.TrialLeft = 0.01f;
        Tick(new Input());
        check(!g.RailTrial && g.LastRail is { Hits: 1, Shots: 2, Accuracy: 50 } && g.LastRailPlace == 1 && g.Profile.RailBest(PClass.Mage) == first,
            "when the half minute's up, the trial goes on your class's rail board (score, hits and accuracy)");
        check(!g.Level.Things.OfType<Monster>().Any(m => m.Target?.Kind == ShootingRange.Kind.Trial && !m.Removed), "and its targets are gone");

        // instagib: a railgun alone, slugs that never run out, one kill a slug, and a streak
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.ArenaMods = ArenaMod.Instagib;
        g.StartArena(PClass.Fighter);
        p = g.P;
        check(g.InstagibOn && p.Weapons.Length == 1 && p.CurWeapon.Rail && !g.Level.Things.OfType<Pickup>().Any(k => k.Kind != PickupKind.Upgrade),
            "Instagib: a railgun alone, and no pickups");
        Monster Brute(float dx)
        {
            var m = new Monster(Monster.Heresiarch) { X = p.X + dx, Y = p.Y, Level = g.Level, State = AiState.Idle };
            m.Health = m.MaxHealth = 5000;
            g.Level.Things.Add(m);
            return m;
        }
        g.Vars.Freeze = true;
        p.Angle = 0; p.Pitch = 0;
        var boss = Brute(3);
        Tick(new Input(), 20); p.Cooldown = 0;
        Tick(new Input { Fire = true });
        check(!boss.Alive && g.Streak == 1 && p.Ammo[(int)AmmoKind.Slugs] == QuakeAmmo.Max(AmmoKind.Slugs), "one slug kills even a 5000-health brute, and the slugs never run out");
        for (int k = 0; k < 4; k++) { Brute(3); p.Cooldown = 0; Tick(new Input { Fire = true }); }
        int streak = g.Streak;
        p.Angle = MathF.PI; p.Cooldown = 0;
        Tick(new Input { Fire = true });
        check(streak == 5 && g.Streak == 0 && g.BestStreak == 5, $"slugs that hit build a streak ({streak}); a miss ends it, the best kept (5)");
        g.Vars.Freeze = false;
        g.Level.Arena.Started = true; g.Level.Arena.BestWave = 3;
        int arenaBefore = g.Profile.ArenaBoard(PClass.Fighter).Count;
        g.EndArenaRun();
        check(g.LastInstagib is { Waves: 3, BestStreak: 5 } && g.Profile.InstagibBoard(PClass.Fighter).Count == 1 && g.Profile.ArenaBoard(PClass.Fighter).Count == arenaBefore,
            "an instagib run goes on its own board (waves, then streak), not the arena's");

        // Rocket Arena: every Quake weapon, full ammo, 200 health, and your own blasts only push
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.ArenaMods = ArenaMod.RocketArena;
        g.StartArena(PClass.Mage);
        p = g.P;
        check(g.RocketArenaOn && p.Weapons.SequenceEqual(QuakeArms.All) && p.Health == 200 && p.Ammo[(int)AmmoKind.Rockets] == 100,
            "Rocket Arena: all five Quake weapons, full ammo, 200 health");
        Tick(new Input(), 10);
        // out on the arena floor, under its tall roof (the armoury's ceiling is low)
        var lv = g.Level;
        int tall = Enumerable.Range(0, lv.W * lv.H).First(c => lv.Cells[c] == '\0' && lv.Heights[c] - lv.Floors[c] >= 3f
            && !lv.BlocksCircle(c % lv.W + 0.5f, c / lv.W + 0.5f, 0.9f) && lv.Heights[c + 1] - lv.Floors[c + 1] >= 3f);
        p.X = tall % lv.W + 0.5f; p.Y = tall / lv.W + 0.5f; p.FloorZ = lv.Floors[tall]; p.Z = 0;
        p.Pitch = -Rockets.LookDown; p.Cooldown = 0;
        Tick(new Input { Jump = true, Fire = true });
        float top = 0;
        for (int k = 0; k < 40; k++) { Tick(new Input()); top = MathF.Max(top, p.Z); }
        check(top > 1.5f && p.Health == 200, $"a rocket jump there costs no health ({top:0.0} cells up, {p.Health} health)");
        p.Ammo[(int)AmmoKind.Rockets] = 3; p.Health = 50;
        g.ArenaWaveCleared(1);
        check(p.Ammo[(int)AmmoKind.Rockets] == 100 && p.Health == 200, "each wave cleared, ammo and health come back in full");

        // the Quake modes don't mix with each other, or with melee only
        g = new Game { FixedSeed = 1 };
        g.GoToTitle();
        g.Menu.Show(MenuPage.ArenaSetup);
        void Toggle(ArenaMod m) { g.Menu.Cursor = Array.IndexOf(ArenaModInfo.All, m); g.Menu.Update(new Input { Right = true }, 1f / 35f); }
        Toggle(ArenaMod.MeleeOnly); Toggle(ArenaMod.Instagib);
        var a1 = g.ArenaMods;
        Toggle(ArenaMod.RocketArena);
        check(a1 == ArenaMod.Instagib && g.ArenaMods == ArenaMod.RocketArena, "picking a Quake mode turns off the other, and melee only");
    }
}
