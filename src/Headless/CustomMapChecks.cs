namespace HexenSharp;

/// <summary>Checks on what custom maps can hold: mini-bosses, weapon mods, a hazard and elites.</summary>
public static partial class Headless
{
    static void MapFeatureChecks(Action<bool, string> check)
    {
        const string text = "# Hexen Sharp map\nname: Proving Ground\ntheme: arena\nheight: 2\nhazard: wind\nelites: 0.5\n---\n"
            + "################\n"
            + "#@.....e.e.e...#\n"
            + "#..m...e.e.e...#\n"
            + "#......G.R.Y...#\n"
            + "#......o.KyK..E#\n"
            + "################\n";
        var doc = MapDoc.Parse(text);
        check(doc.Hazard == "wind" && doc.Elites == 0.5f && doc.Serialize() == text, "a map file keeps its hazard and chance of elites, and saves back byte for byte");
        check(MapDoc.Parse(text.Replace("hazard: wind", "hazard: volcano")).Hazard == "none" && MapDoc.Parse(text.Replace("elites: 0.5", "elites: 3")).Elites == 0.5f,
              "an unknown hazard reads as none, and elites are capped at half");
        check("GRYoym".All(MapDoc.IsKnownGlyph), "the mini-bosses (G R Y o y) and a weapon mod (m) are map glyphs");
        check(!doc.Validate().Any(i => i.StartsWith("!")), "and the map checks out");

        var g = new Game { FixedSeed = 1, AchievementsOn = false, Profile = new Profile() };
        g.StartTest(doc.ToDef(), PClass.Fighter);
        var lv = g.Level;
        var bosses = lv.Things.OfType<Monster>().Where(m => m.Def.MiniBoss != null).Select(m => m.Def).ToHashSet();
        check(bosses.SetEquals(new[] { MiniBosses.Warden, MiniBosses.Stalker, MiniBosses.Thornmother, MiniBosses.Keeper, MiniBosses.Wyrm }),
              "play-testing it, each mini-boss glyph is its mini-boss");
        var wyrm = lv.Things.OfType<Monster>().Single(m => m.Def == MiniBosses.Wyrm);
        check(wyrm.Burrowed && !wyrm.Solid, "the Rock Wyrm starts inside the rock");
        var mod = lv.Things.OfType<Pickup>().Single(p => p.Kind == PickupKind.Mod);
        check(WeaponMods.All.Contains((WeaponMod)mod.Variant), "a weapon mod glyph rolls one of the four when the game starts");
        var ordinary = lv.Things.OfType<Monster>().Where(m => m.Def.MiniBoss == null).ToList();
        int elites = ordinary.Count(m => m.Affix != Affix.None);
        check(g.NgTier == 0 && elites >= 2 && elites <= 8 && lv.Things.OfType<Monster>().Where(m => m.Def.MiniBoss != null).All(m => m.Affix == Affix.None),
              $"its chance of elites applies, even outside New Game+ ({elites} of {ordinary.Count} at 50%), never to a mini-boss");
        int gusting = 0;
        g.Vars.NoTarget = true;
        for (int k = 0; k < 35 * (int)Game.GustEvery; k++) { g.Update(default, 1f / 35f); if (g.Gusting) gusting++; }
        check(gusting > 0, "and so does its hazard: wind gusts in a play-test");
        var plain = new Game { FixedSeed = 1, AchievementsOn = false, Profile = new Profile() };
        plain.StartTest(MapDoc.Parse(text.Replace("hazard: wind\n", "").Replace("elites: 0.5\n", "")).ToDef(), PClass.Fighter);
        for (int k = 0; k < 35 * (int)Game.GustEvery; k++) plain.Update(default, 1f / 35f);
        check(!plain.Level.Things.OfType<Monster>().Any(m => m.Affix != Affix.None) && Game.HazardOf(plain.Level) == Game.HazardKind.None,
              "without them, a custom map has neither");
    }
}
