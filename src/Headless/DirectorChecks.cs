namespace HexenSharp;

/// <summary>Checks on the New Game+ director's ambushes.</summary>
public static partial class Headless
{
    static void DirectorChecks(Action<bool, string> check)
    {
        Game Hall(int tier)
        {
            var gg = new Game { FixedSeed = 1, AchievementsOn = false, Profile = new Profile { NgUnlocked = 3 } };
            if (tier > 0) gg.StartNewGamePlus(PClass.Fighter, tier); else gg.NewGame(PClass.Fighter);
            gg.Level.Things.RemoveAll(t => t is Monster);
            gg.P.X = 10.5f; gg.P.Y = 5.5f; gg.P.FloorZ = gg.Level.FloorAt(10.5f, 5.5f);
            gg.P.Health = gg.P.MaxHealth = 5000; gg.Vars.NoTarget = true;
            return gg;
        }
        void Run(Game gg, float secs) { for (int k = 0; k < (int)(secs * 35); k++) gg.Update(default, 1f / 35f); }
        // stand here, go away for a while (somewhere else on the map), then come back
        void Backtrack(Game gg, float away)
        {
            Run(gg, 0.2f);
            float hx = gg.P.X, hy = gg.P.Y;
            gg.P.X = 4.5f; gg.P.Y = 3.5f; gg.P.FloorZ = gg.Level.FloorAt(4.5f, 3.5f);
            Run(gg, away);
            gg.P.X = hx; gg.P.Y = hy; gg.P.FloorZ = gg.Level.FloorAt(hx, hy);
        }

        var g = Hall(1);
        check(!g.Level.Blocks(4, 3) && !g.Level.Blocks(10, 5), "(two open spots in the hall)");
        Backtrack(g, 20);
        Run(g, 10);
        check(g.LastAmbush.Count == 0, "coming back somewhere you were only 20 seconds ago is nothing");
        var g2 = Hall(1);
        Backtrack(g2, Game.AmbushCooldown(1) + 1);
        check(g2.LastAmbush.Count == 0, "standing in one spot for a minute isn't backtracking");
        for (int k = 0; k < 35 * 10 && g2.LastAmbush.Count == 0; k++) g2.Update(default, 1f / 35f);
        var am = g2.LastAmbush;
        check(am.Count == Game.AmbushSize(1) && g2.Messages.Any(m => m.text.StartsWith("Ambush!")), $"coming back springs an ambush of {Game.AmbushSize(1)}, soon after");
        check(am.All(m => m.Alive && m.State != AiState.Idle && Game.Dist(m.X, m.Y, g2.P.X, g2.P.Y) is >= Game.AmbushNear - 0.1f and <= Game.AmbushFar + 0.1f),
              "awake, 5 to 9 cells off");
        check(am.All(m => m.MaxHealth == (int)((int)(m.Def.Health * NgPlus.Health(1)) * (m.Affix != Affix.None ? Elites.HealthMult : 1)) && MathF.Abs(m.DamageMult - NgPlus.Damage(1)) < 0.01f), "toughened for the tier");
        var natives = new Game { FixedSeed = 1 };
        natives.NewGame(PClass.Fighter);
        var hallKinds = natives.Hub[0].Things.OfType<Monster>().Where(m => !m.Def.Boss).Select(m => m.Def).ToHashSet();
        check(am.All(m => hallKinds.Contains(m.Def)), "and the map's own kind of monster");

        // not straight away again, while it's busy, and a few to a map
        foreach (var m in am) g2.DamageMonster(m, 1000000, 0);
        int before = g2.Level.Things.Count(t => t is Monster);
        Run(g2, 20);
        check(g2.Level.Things.Count(t => t is Monster) == before, $"then it waits a while ({Game.AmbushCooldown(1)}s at tier 1)");
        var busy = Hall(1);
        Backtrack(busy, Game.AmbushCooldown(1) + 1);
        var hunter = new Monster(Monster.Ettin) { X = busy.P.X + 3, Y = busy.P.Y, Level = busy.Level, State = AiState.Chase };
        busy.Level.Things.Add(hunter);
        busy.Vars.Freeze = true;
        Run(busy, 20);
        check(busy.LastAmbush.Count == 0, "no ambush while something's already after you");
        var cap = Hall(2);
        bool sizes = true;
        for (int k = 0; k < Game.AmbushesPerMap(2); k++)
        {
            cap.Ambush();
            sizes &= cap.LastAmbush.Count == Game.AmbushSize(2);
            foreach (var m in cap.LastAmbush) cap.DamageMonster(m, 1000000, 0);
        }
        var last = cap.LastAmbush;
        Backtrack(cap, Game.AmbushCooldown(2) + 1);
        Run(cap, 12);
        check(sizes && Game.AmbushSize(2) == 3 && ReferenceEquals(cap.LastAmbush, last), $"bigger ambushes at higher tiers (3 at tier 2), and no more than {Game.AmbushesPerMap(2)} on a map");

        // a first campaign has no director
        var plain = Hall(0);
        Backtrack(plain, 80);
        Run(plain, 30);
        check(plain.LastAmbush.Count == 0 && !plain.Level.Things.Any(t => t is Monster), "a first campaign stays quiet behind you");
    }
}
