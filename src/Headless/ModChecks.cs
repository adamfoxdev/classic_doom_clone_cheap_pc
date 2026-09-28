namespace HexenSharp;

/// <summary>Checks on weapon mods: finding them, fitting them, and what each does.</summary>
public static partial class Headless
{
    static void ModChecks(Action<bool, string> check)
    {
        // rare in chests, commoner in New Game+
        var rng = new Random(5);
        var pl = new Player();
        int Mods(int weight) => Enumerable.Range(0, 3000).Count(_ => Chests.RollLoot(rng, pl, weight).Contains('m'));
        int none = Mods(0), plain = Mods(WeaponMods.ChestWeight(0)), ng2 = Mods(WeaponMods.ChestWeight(2));
        check(none == 0 && plain > 60 && plain < 300 && ng2 > plain * 2, $"mods turn up in about {plain * 100 / 3000}% of chests, {ng2 * 100 / 3000}% at New Game+ 2");
        check(Enumerable.Range(0, 500).All(_ => Chests.RollLoot(rng, pl, 40).Count(c => c == 'm') <= 1), "never more than one in a chest");

        // the test range: a Mage down the great hall, the rest of the monsters gone
        Game g = null;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }
        void Range(PClass cls)
        {
            g = new Game { FixedSeed = 1, AchievementsOn = false };
            g.NewGame(cls);
            g.Level.Things.RemoveAll(t => t is Monster or Pickup);
            g.P.X = 10.5f; g.P.Y = 5.5f; g.P.Angle = 0; g.P.FloorZ = g.Level.FloorAt(10.5f, 5.5f);
            g.Vars.HitStop = false;
        }
        Monster Dummy(float dx, float dy = 0, MonsterDef def = null)
        {
            var m = new Monster(def ?? Monster.Slaughtaur) { X = g.P.X + dx, Y = g.P.Y + dy, Level = g.Level, State = AiState.Chase };
            m.Health = m.MaxHealth = 5000;
            g.Level.Things.Add(m);
            return m;
        }
        void Grab(WeaponMod m)
        {
            var pk = Game.MakeMod(m, g.P.X, g.P.Y);
            pk.Level = g.Level;
            g.Level.Things.Add(pk);
            Tick(default);
        }

        // fitting
        Range(PClass.Mage);
        Grab(WeaponMod.Piercing);
        check(g.P.Mods[0] == WeaponMod.Piercing && g.Messages.Any(m => m.text.StartsWith(WeaponMods.Name(WeaponMod.Piercing) + " fitted to the " + g.P.Def.Weapons[0].Name)),
              "picking up a mod fits it to the weapon in your hand, and says what it does");
        Grab(WeaponMod.Piercing);
        check(g.P.Mods[0] == WeaponMod.Piercing && g.ModRank(0, WeaponMod.Piercing) == 2 && g.Messages.Any(m => m.text.StartsWith(WeaponMods.Name(WeaponMod.Piercing) + " II on the")),
              "one it already has goes up a rank");
        Grab(WeaponMod.Piercing);
        Grab(WeaponMod.Piercing);
        check(g.ModRank(0, WeaponMod.Piercing) == WeaponMods.MaxRank && g.Level.Things.OfType<Pickup>().Any(p => p.Kind == PickupKind.Mod && !p.Removed),
              "to III at most: past that it's left lying");
        g.Level.Things.RemoveAll(t => t is Pickup);
        Grab(WeaponMod.Frost);
        check(g.P.Mods[0] == WeaponMod.Piercing && g.P.Mods2[0] == WeaponMod.Frost && g.ComboOn(0) == WeaponMods.Combo.Shatter
              && g.Messages.Any(m => m.text.Contains("Combo: Shatter")), "a second kind joins it, and the pair makes a combo");
        check(g.ModLabel(0) == "SHATTER", "the HUD shows the combo's name");
        Grab(WeaponMod.Chain);
        check(g.P.Mods[0] == WeaponMod.Frost && g.P.Mods2[0] == WeaponMod.Chain && g.ComboOn(0) == WeaponMods.Combo.FrozenArc
              && g.Messages.Any(m => m.text.Contains("replacing the " + WeaponMods.Name(WeaponMod.Piercing))), "a third replaces the older of the two");
        check(g.P.Mods[1] == WeaponMod.None && g.P.Mods[2] == WeaponMod.None, "the other weapons keep theirs");
        check(Enum.GetValues<WeaponMods.Combo>().Skip(1).All(c => WeaponMods.All.SelectMany(a => WeaponMods.All.Select(b => WeaponMods.ComboOf(a, b))).Count(x => x == c) == 2)
              && WeaponMods.All.All(m => WeaponMods.ComboOf(m, m) == WeaponMods.Combo.None), "every pair of different mods has a combo of its own");

        // piercing: through three in a row, where a plain shot stops at the first
        int[] Line(WeaponMod mod)
        {
            Range(PClass.Mage);
            g.P.Mods[0] = mod;
            var row = new[] { Dummy(2), Dummy(3.2f), Dummy(4.4f) };
            Tick(new Input { Fire = true });
            Tick(default, 20);
            return row.Select(m => 5000 - m.Health).ToArray();
        }
        var plainHits = Line(WeaponMod.None);
        var pierced = Line(WeaponMod.Piercing);
        check(plainHits[0] > 0 && plainHits[1] == 0 && pierced.All(h => h > 0), $"a piercing shot goes through all three in a row ({string.Join(",", pierced)}), a plain one stops at the first");
        Range(PClass.Fighter);
        g.P.Mods[0] = WeaponMod.Piercing;
        var pair = new[] { Dummy(0.7f, -0.15f), Dummy(0.75f, 0.15f) };
        Tick(new Input { Fire = true });
        check(pair.All(m => m.Health < 5000), "and a piercing blow cleaves one more");

        // chain: some hits arc on to the monsters near the one you hit
        int Arcs(WeaponMod mod)
        {
            Range(PClass.Mage);
            g.P.Mods[0] = mod;
            var target = Dummy(2);
            var by = new[] { Dummy(2.5f, 1.2f), Dummy(2.5f, -1.2f) };
            int arcs = 0;
            for (int k = 0; k < 60; k++)
            {
                int before = by.Sum(m => m.Health);
                g.DamageMonster(target, 20, 0);
                if (by.Sum(m => m.Health) < before) arcs++;
            }
            return arcs;
        }
        int chained = Arcs(WeaponMod.Chain), unchained = Arcs(WeaponMod.None);
        check(chained > 8 && chained < 35 && unchained == 0, $"a Chain mod arcs about a third of hits to monsters nearby ({chained} of 60)");

        // charged: hold to charge (nothing fires), let go for a bigger shot
        Range(PClass.Mage);
        g.P.Mods[0] = WeaponMod.Charged;
        Tick(new Input { Fire = true }, 35);
        int flying = g.Level.Things.Count(t => t is Projectile { FromPlayer: true });
        check(flying == 0 && g.P.Charging && g.P.Charge >= 1, "holding Fire charges the shot instead of firing");
        Tick(default);
        var shot = g.Level.Things.OfType<Projectile>().SingleOrDefault(t => t.FromPlayer);
        var wand = g.P.Def.Weapons[0];
        float full = wand.DmgMin * g.PlayerDamageMult(0) * WeaponMods.ChargeMult;
        check(shot != null && Math.Abs(shot.DmgMin - full) <= 1 && shot.SpriteW > 0.35f, $"letting go fires it at {WeaponMods.ChargeMult}x damage, bigger ({shot?.DmgMin} vs {wand.DmgMin})");
        g.Level.Things.RemoveAll(t => t is Projectile);
        Tick(default, 20);
        Tick(new Input { Fire = true });
        Tick(default);
        var tap = g.Level.Things.OfType<Projectile>().SingleOrDefault(t => t.FromPlayer);
        check(tap != null && tap.DmgMin < wand.DmgMin * g.PlayerDamageMult(0) * 1.2f, "a tap still fires straight away, at about the usual damage");

        // frost: a hit slows the monster to half speed for a few seconds
        Range(PClass.Mage);
        g.P.Mods[0] = WeaponMod.Frost;
        var cold = Dummy(6, 0, Monster.Ettin);
        var warm = Dummy(6, 2, Monster.Ettin);
        g.DamageMonster(cold, 5, 0);
        check(cold.SlowTime == WeaponMods.FrostTime && warm.SlowTime == 0, $"a Frost hit chills it for {WeaponMods.FrostTime} seconds");
        g.P.Health = 1000;
        float cx = cold.X, wx = warm.X;
        Tick(default, 20);
        float coldMoved = Game.Dist(cold.X, cold.Y, cx, cold.Y), warmMoved = Game.Dist(warm.X, warm.Y, wx, warm.Y);
        check(coldMoved > 0 && coldMoved < warmMoved * 0.7f, $"and it moves at half speed ({coldMoved:0.00} against {warmMoved:0.00})");
        Tick(default, 35 * 3);
        check(cold.SlowTime == 0, "then warms up again");

        // ranks: Piercing III goes through six
        {
            Range(PClass.Mage);
            g.P.Mods[0] = WeaponMod.Piercing; g.P.ModRanks[0] = 3;
            var row = Enumerable.Range(0, 7).Select(k => Dummy(2 + k * 1.1f)).ToArray();
            Tick(new Input { Fire = true });
            Tick(default, 25);
            check(row.Take(7).All(m => m.Health < 5000), "a rank III Piercing mod goes through six more");
        }
        // Frost II and III chill longer and harder
        {
            Range(PClass.Mage);
            g.P.Mods[0] = WeaponMod.Frost; g.P.ModRanks[0] = 3;
            var cold3 = Dummy(6, 0, Monster.Ettin);
            g.DamageMonster(cold3, 5, 0);
            check(cold3.SlowTime == WeaponMods.FrostTimeAt(3) && MathF.Abs(cold3.SlowFactor - WeaponMods.FrostSlowAt(3)) < 0.001f && WeaponMods.FrostSlowAt(3) < WeaponMods.FrostSlowAt(1),
                  $"Frost III chills for {WeaponMods.FrostTimeAt(3)}s, to {WeaponMods.FrostSlowAt(3) * 100:0}% speed");
        }
        // combos
        Game Combo(WeaponMod a, WeaponMod b)
        {
            Range(PClass.Mage);
            g.P.Mods[0] = a; g.P.Mods2[0] = b;
            return g;
        }
        {
            // Lance: a charged shot goes through everything
            Combo(WeaponMod.Piercing, WeaponMod.Charged);
            g.Vars.Freeze = true; // they'd walk out of line while it charges
            var row = Enumerable.Range(0, 6).Select(k => Dummy(2 + k * 1.1f)).ToArray();
            Tick(new Input { Fire = true }, 35);
            Tick(default, 25);
            check(row.All(m => m.Health < 5000), $"Lance (Piercing + Charged): a full charge goes through all six in a row");
        }
        {
            // Deep Freeze: a charged hit stops it dead
            Combo(WeaponMod.Charged, WeaponMod.Frost);
            var ice = Dummy(2);
            Tick(new Input { Fire = true }, 35);
            Tick(default, 8);
            float ix = ice.X, iy = ice.Y;
            g.P.Health = 1000;
            Tick(default, 20);
            check(ice.FrozenTime > 0 && ice.X == ix && ice.Y == iy, "Deep Freeze (Charged + Frost): a charged hit freezes it solid");
        }
        {
            // Shatter: chilled monsters take half as much again
            Combo(WeaponMod.Piercing, WeaponMod.Frost);
            var s1 = Dummy(3);
            g.DamageMonster(s1, 20, 0);
            int before = s1.Health;
            g.DamageMonster(s1, 20, 0);
            check(before - s1.Health == 30, "Shatter (Piercing + Frost): a chilled monster takes 50% more");
        }
        {
            // Frozen Arc: arcs chill what they hit
            Combo(WeaponMod.Chain, WeaponMod.Frost);
            var t = Dummy(2);
            var by = Dummy(2.5f, 1.2f);
            for (int k = 0; k < 40 && by.SlowTime == 0; k++) g.DamageMonster(t, 5, 0);
            check(by.SlowTime > 0 && by.Health < 5000, "Frozen Arc (Chain + Frost): arcs chill what they hit");
            var plainChain = Combo(WeaponMod.Chain, WeaponMod.None);
            var t2 = Dummy(2);
            var by2 = Dummy(2.5f, 1.2f);
            g.P.Mods2[0] = WeaponMod.None;
            for (int k = 0; k < 40; k++) g.DamageMonster(t2, 5, 0);
            check(by2.Health < 5000 && by2.SlowTime == 0, "(a plain Chain's arcs don't)");
        }
        {
            // Thunderclap: a charged hit always arcs, to twice as many
            Combo(WeaponMod.Chain, WeaponMod.Charged);
            var t = Dummy(2);
            var ring = new[] { Dummy(2.5f, 1.2f), Dummy(2.5f, -1.2f), Dummy(3.2f, 1.6f), Dummy(3.2f, -1.6f) };
            Tick(new Input { Fire = true }, 35);
            Tick(default, 10);
            check(t.Health < 5000 && ring.All(m => m.Health < 5000), "Thunderclap (Chain + Charged): a charged hit arcs to four");
        }
        {
            // Storm Lance: arcs leap on once more
            Combo(WeaponMod.Piercing, WeaponMod.Chain);
            var t = Dummy(2);
            var hop1 = Dummy(4.5f);
            var hop2 = Dummy(7.5f);
            for (int k = 0; k < 80 && hop2.Health == 5000; k++) g.DamageMonster(t, 5, 0);
            check(hop1.Health < 5000 && hop2.Health < 5000, "Storm Lance (Piercing + Chain): arcs leap on from each monster they hit");
        }

        // a New Game+ mini-boss drops one; a mod is kept in a save
        string dir = Path.Combine(Path.GetTempPath(), $"hexensharp-mods-{Environment.ProcessId}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "save.json");
        var ng = new Game { FixedSeed = 1, AchievementsOn = false, SavePath = path, Profile = new Profile { NgUnlocked = 1 } };
        ng.StartNewGamePlus(PClass.Cleric, 1);
        ng.Warp(Array.FindIndex(ng.Hub, l => l.RawName == "Hanging Cisterns"));
        var keeper = ng.Level.Things.OfType<Monster>().Single(t => t.Def == MiniBosses.Keeper);
        ng.DamageMonster(keeper, 1000000, 0);
        check(ng.Level.Things.OfType<Pickup>().Any(p => p.Kind == PickupKind.Mod && Game.Dist(p.X, p.Y, keeper.X, keeper.Y) < 1), "in New Game+, a mini-boss drops a weapon mod");
        ng.P.Mods[2] = WeaponMod.Chain; ng.P.Mods2[2] = WeaponMod.Frost; ng.P.ModRanks2[2] = 3;
        ng.SaveNow();
        var back = new Game { FixedSeed = 1, AchievementsOn = false, SavePath = path, Profile = ng.Profile };
        check(back.Continue() && back.P.Mods[2] == WeaponMod.Chain && back.P.Mods2[2] == WeaponMod.Frost && back.ModRank(2, WeaponMod.Frost) == 3 && back.P.Mods[0] == WeaponMod.None, "your weapons' mods and their ranks are kept in the save");
        Directory.Delete(dir, true);
    }
}
