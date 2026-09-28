namespace HexenSharp;

/// <summary>Checks on New Game+ hazards: crumbling floors, gusts and floods.</summary>
public static partial class Headless
{
    static void HazardChecks(Action<bool, string> check)
    {
        Game At(string map, int tier)
        {
            var gg = new Game { FixedSeed = 1, AchievementsOn = false, Profile = new Profile { NgUnlocked = 3 } };
            if (tier > 0) gg.StartNewGamePlus(PClass.Fighter, tier); else gg.NewGame(PClass.Fighter);
            gg.Warp(Array.FindIndex(gg.Hub, l => l.RawName == map));
            gg.Level.Things.RemoveAll(t => t is Monster);
            gg.P.Health = gg.P.MaxHealth = 1000; gg.P.Armor = 0;
            return gg;
        }
        void Run(Game gg, int frames, Input inp = default) { for (int k = 0; k < frames; k++) gg.Update(inp, 1f / 35f); }

        // the Bedrock Depths: stand still in a tunnel you've dug and the floor gives way
        Game Tunnel(int tier, out int cx, out int cy)
        {
            var gg = At("Bedrock Depths", tier);
            int ax = (int)gg.P.X, ay = (int)gg.P.Y;
            float fl = gg.P.FloorZ;
            for (int k = 1; k <= 4; k++) gg.Level.DamageBlock(ax + k, ay, 100000, Level.Face.Wall, fl);
            cx = ax + 2; cy = ay;
            gg.P.X = cx + 0.5f; gg.P.Y = cy + 0.5f; gg.P.FloorZ = gg.Level.FloorAt(gg.P.X, gg.P.Y); gg.P.Z = 0;
            return gg;
        }
        var d = Tunnel(1, out int cx, out int cy);
        int cell = cy * d.Level.W + cx;
        float floor0 = d.Level.Floors[cell];
        Run(d, 1);
        check(d.Messages.Any(m => m.text.StartsWith("New Game+: the floor here crumbles")), "arriving in New Game+, the Depths warn you their floor crumbles");
        check(d.Level.CanDig(cx, cy, Level.Face.Floor), "(a tunnel floor)");
        Run(d, (int)(35 * Game.CrumbleWarn) + 2);
        check(d.Messages.Any(m => m.text == "The floor is cracking under you!") && d.Level.Floors[cell] == floor0, "stand still and it cracks first");
        int hp = d.P.Health;
        Run(d, (int)(35 * (Game.CrumbleTime - Game.CrumbleWarn)) + 2);
        check(MathF.Abs(d.Level.Floors[cell] - (floor0 - Level.DigStep)) < 0.001f && d.P.Health == hp - Game.CrumbleDamage,
              $"then drops a step under you, for {Game.CrumbleDamage} damage");
        Run(d, 35 * 3);
        check(MathF.Abs(d.Level.Floors[cell] - (floor0 - Level.DigStep)) < 0.001f && Level.DigStep <= Level.MaxStep, "only once per spot, so you can always step back out");
        var keep = Tunnel(1, out _, out _);
        for (int k = 0; k < 8; k++)
        {
            Run(keep, 30);
            keep.P.X = cx + (k % 2 == 0 ? 1.5f : 0.5f); keep.P.FloorZ = keep.Level.FloorAt(keep.P.X, keep.P.Y);
        }
        check(keep.Level.Floors[cell] == floor0 && keep.Level.Floors[cell + 1] == floor0, "keep moving and it holds");
        var first = Tunnel(0, out _, out _);
        Run(first, 35 * 4);
        check(first.Level.Floors[cell] == floor0 && !first.Messages.Any(m => m.text.Contains("crumble")), "a first campaign's floor never crumbles");

        // the Windspire: gusts, now and then, that push you
        var w = At("Windspire", 1);
        w.Vars.NoTarget = true;
        int gustFrames = 0;
        float x0 = w.P.X, y0 = w.P.Y;
        for (int k = 0; k < 35 * (int)Game.GustEvery; k++) { w.Update(default, 1f / 35f); if (w.Gusting) gustFrames++; }
        float drift = Game.Dist(w.P.X, w.P.Y, x0, y0);
        check(MathF.Abs(gustFrames - 35 * Game.GustLength) <= 2, $"gusts blow on the Windspire, {Game.GustLength}s in every {Game.GustEvery}");
        check(drift > 0.3f, $"and push you ({drift:0.00} cells on the ground)");
        var calm = At("Windspire", 0);
        x0 = calm.P.X; y0 = calm.P.Y;
        Run(calm, 35 * (int)Game.GustEvery);
        check(Game.Dist(calm.P.X, calm.P.Y, x0, y0) < 0.01f, "none in a first campaign");

        // the Hanging Cisterns: floods that hurt on the floor, not up on a ledge
        var c = At("Hanging Cisterns", 1);
        float rise = Game.FloodEvery - Game.FloodLength;
        Run(c, (int)(35 * (rise - Game.FloodWarn)) + 2);
        check(c.Messages.Any(m => m.text == Words.T("The water is rising! Get up on a ledge!")) && !c.Flooding, "the Cisterns warn you before they flood");
        hp = c.P.Health;
        Run(c, (int)(35 * Game.FloodWarn) + (int)(35 * Game.FloodLength) - 4);
        int lost = hp - c.P.Health;
        check(lost >= Game.CoolantDamage * 14 && lost <= Game.CoolantDamage * 17, $"then flood, hurting you on the floor ({lost} damage over {Game.FloodLength}s)");
        Run(c, 10);
        check(!c.Flooding, "and drain again");
        var dry = At("Hanging Cisterns", 1);
        var lv = dry.Level;
        int high = Enumerable.Range(0, lv.Cells.Length).Where(i => lv.Cells[i] == '\0').OrderByDescending(i => lv.Floors[i]).First();
        Run(dry, (int)(35 * rise) + 10);
        dry.P.X = high % lv.W + 0.5f; dry.P.Y = high / lv.W + 0.5f; dry.P.FloorZ = lv.Floors[high]; dry.P.Z = 0;
        hp = dry.P.Health;
        Run(dry, 35 * 3);
        check(dry.Flooding && !dry.InCoolant && dry.P.Health == hp, "up on a ledge you're safe");
        var none = At("Hanging Cisterns", 0);
        hp = none.P.Health;
        Run(none, 35 * (int)Game.FloodEvery);
        check(none.P.Health == hp && !none.Messages.Any(m => m.text.Contains("rising")), "a first campaign's cisterns stay dry");
    }
}
