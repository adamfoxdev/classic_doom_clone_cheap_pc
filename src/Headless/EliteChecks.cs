namespace HexenSharp;

/// <summary>Checks on New Game+ elites.</summary>
public static partial class Headless
{
    static void EliteChecks(Action<bool, string> check)
    {
        Game Ng(int tier)
        {
            var gg = new Game { FixedSeed = 1, AchievementsOn = false, Profile = new Profile { NgUnlocked = 3 } };
            if (tier > 0) gg.StartNewGamePlus(PClass.Fighter, tier); else gg.NewGame(PClass.Fighter);
            return gg;
        }
        List<Monster> All(Game gg) => gg.Hub.SelectMany(l => l.Things.OfType<Monster>()).ToList();

        check(!All(Ng(0)).Any(m => m.Affix != Affix.None), "a first campaign has no elites");
        var t1 = All(Ng(1));
        int elites = t1.Count(m => m.Affix != Affix.None);
        int eliteSum = 0, ordinarySum = 0;
        for (int seed = 1; seed <= 8; seed++)
        {
            var gs = new Game { FixedSeed = seed, AchievementsOn = false, Profile = new Profile { NgUnlocked = 1 } };
            gs.StartNewGamePlus(PClass.Fighter, 1);
            var ms = All(gs);
            eliteSum += ms.Count(m => m.Affix != Affix.None);
            ordinarySum += ms.Count(Elites.Eligible);
        }
        float share = eliteSum / (float)ordinarySum;
        check(share > 0.12f && share < 0.25f, $"at New Game+ 1 about {Elites.Chance(1) * 100:0}% of ordinary monsters are elites ({share * 100:0}% over 8 games)");
        var t3 = All(Ng(3));
        check(Elites.All.All(a => t3.Any(m => m.Affix == a)) && t3.Count(m => m.Affix != Affix.None) > elites, "more at higher tiers, with all four gifts");
        check(t3.Concat(t1).Where(m => m.Def.Boss || m.Def.MiniBoss != null).All(m => m.Affix == Affix.None), "never a boss or a mini-boss");
        check(t1.Where(m => m.Affix != Affix.None).All(m => m.MaxHealth == (int)((int)(m.Def.Health * NgPlus.Health(1)) * Elites.HealthMult)), "an elite has a third more health");

        // a test range in the great hall
        Game g = null;
        void Tick(int n = 1) { for (int k = 0; k < n; k++) g.Update(default, 1f / 35f); }
        Monster Elite(Affix a, float dx, MonsterDef def = null)
        {
            var m = new Monster(def ?? Monster.Ettin) { X = g.P.X + dx, Y = g.P.Y, Level = g.Level, State = AiState.Chase };
            g.Level.Things.Add(m);
            g.MakeElite(m, a);
            return m;
        }
        void Range()
        {
            g = Ng(1);
            g.Level.Things.RemoveAll(t => t is Monster);
            g.P.X = 10.5f; g.P.Y = 5.5f; g.P.Angle = 0; g.P.FloorZ = g.Level.FloorAt(10.5f, 5.5f);
            g.P.Health = g.P.MaxHealth = 5000; g.Vars.HitStop = false;
        }

        // shielded: the shield soaks up damage first
        Range();
        var sh = Elite(Affix.Shielded, 3);
        int hp = sh.Health, shield = sh.Shield;
        g.DamageMonster(sh, 20, 0);
        check(shield == (int)(sh.MaxHealth * Elites.ShieldShare) && sh.Shield == shield - 20 && sh.Health == hp, "a shielded elite's shield takes the first hits");
        g.DamageMonster(sh, shield, 0);
        check(sh.Shield == 0 && sh.Health == hp - 20, "then, once it breaks, its health");

        // splitting: two smaller copies when it falls, which don't split again
        Range();
        var sp = Elite(Affix.Splitting, 3, Monster.Centaur);
        g.DamageMonster(sp, 1000000, 0);
        var kids = g.Level.Things.OfType<Monster>().Where(m => m.Alive && m.Split).ToList();
        check(kids.Count == Elites.SplitCount && kids.All(k => k.Def == Monster.Centaur && k.Affix == Affix.None && k.SpriteW < Monster.Centaur.Width && k.MaxHealth == (int)(sp.MaxHealth * Elites.SplitHealth)),
              "a splitting elite bursts into two smaller copies, a third as tough");
        g.DamageMonster(kids[0], 1000000, 0);
        check(g.Level.Things.OfType<Monster>().Count(m => m.Alive) == 1, "and they don't split again");

        // vampiric: heals by what it takes from you
        Range();
        var vp = Elite(Affix.Vampiric, 0.9f);
        vp.Health = vp.MaxHealth / 2;
        int vh = vp.Health, php = g.P.Health;
        for (int k = 0; k < 35 * 4 && g.P.Health == php; k++) Tick();
        check(g.P.Health < php && vp.Health == vh + (php - g.P.Health), $"a vampiric elite heals by what it takes from you ({php - g.P.Health})");

        // teleporting: blinks away behind you when hurt
        Range();
        var tp = Elite(Affix.Teleporting, 3);
        tp.Health = tp.MaxHealth = 100000;
        float tx = tp.X;
        for (int k = 0; k < 40 && tp.X == tx; k++) { tp.SpecialCd = 0; g.DamageMonster(tp, 5, 0); }
        float away = Game.Dist(tp.X, tp.Y, g.P.X, g.P.Y);
        check(tp.X != tx && away >= Elites.BlinkNear - 0.01f && away <= Elites.BlinkFar + 0.01f && tp.X < g.P.X, $"a teleporting elite, hurt, blinks away behind you ({away:0.0} cells)");
        float bx = tp.X;
        for (int k = 0; k < 10; k++) g.DamageMonster(tp, 5, 0);
        check(tp.X == bx, $"then waits {Elites.BlinkCooldown}s before it can again");

        // worth more, noticed the first time, drawn with an outline, saved
        Range();
        int xp0 = g.Profile.TotalXp;
        g.DamageMonster(Elite(Affix.Shielded, 3), 1000000, 0);
        int eliteXp = g.Profile.TotalXp - xp0;
        check(eliteXp == (int)(Game.Xp.Kill(Monster.Ettin) * NgPlus.Xp(1) * Elites.XpMult), "an elite pays half as much again in experience");
        Range();
        var seen = Elite(Affix.Vampiric, 5);
        g.Messages.Clear();
        Tick(20);
        check(g.Messages.Any(m => m.text == $"A vampiric elite: {Elites.About(Affix.Vampiric)}."), "the first of each kind you meet, the game says what its gift is");
        g.Vars.Freeze = true;
        var r = new Renderer();
        r.Render(g);
        var lit = (uint[])r.Fb.Clone();
        seen.Affix = Affix.None;
        r.Render(g);
        seen.Affix = Affix.Vampiric;
        check(!lit.SequenceEqual(r.Fb), "and it's drawn with an outline in its gift's colour");

        string dir = Path.Combine(Path.GetTempPath(), $"hexensharp-elites-{Environment.ProcessId}");
        Directory.CreateDirectory(dir);
        g.SavePath = Path.Combine(dir, "save.json");
        var keepSh = Elite(Affix.Shielded, 4);
        keepSh.Shield = 7;
        var keepKid = Elite(Affix.None, 6, Monster.Afrit);
        Game.MakeSplit(keepKid);
        check(g.SaveNow(), "(saved)");
        var back = new Game { FixedSeed = 1, AchievementsOn = false, SavePath = g.SavePath, Profile = g.Profile };
        check(back.Continue(), "(continued)");
        var bs = back.Hub[0].Things.OfType<Monster>().FirstOrDefault(m => m.Affix == Affix.Shielded && m.Shield == 7);
        var bk = back.Hub[0].Things.OfType<Monster>().FirstOrDefault(m => m.Split);
        check(bs != null && bk != null && bk.SpriteW < Monster.Afrit.Width, "elites, their shields and splitters' copies are kept in the save");
        Directory.Delete(dir, true);
    }
}
