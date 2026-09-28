namespace HexenSharp;

/// <summary>Checks on recolouring the current map's floor, ceiling, sky and fog from the console.</summary>
public static partial class Headless
{
    static void MapColourChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.StartPractice(PClass.Fighter, Soccer.Course);
        var th = g.Level.Theme;
        Tex floorIn = th.FloorIn, floorOut = th.FloorOut, ceil = th.CeilIn, sky = th.Sky;
        uint fog = th.FogColor;
        uint[] sharedBefore = (uint[])floorIn.Px.Clone();

        g.Con.Execute("floorcolor #203040 flat", quiet: true);
        uint want = Col.Rgb(0x20, 0x30, 0x40);
        check(th.FloorIn.Px.All(c => c == want) && th.FloorOut.Px.All(c => c == want) && floorIn.Px.SequenceEqual(sharedBefore),
            "'floorcolor #203040 flat' paints this map's floors one colour, indoors and out (the shared texture untouched)");

        g.Con.Execute("floorcolor red", quiet: true);
        uint avg = MapColors.Average(th.FloorOut), red = Col.Rgb(200, 40, 40);
        check(Math.Abs(Col.R(avg) - Col.R(red)) < 20 && Math.Abs(Col.G(avg) - Col.G(red)) < 12 && th.FloorOut.Px.Distinct().Count() > 4,
            $"'floorcolor red' tints it: its pattern kept, red on average ({MapColors.Hex(avg)})");

        g.Con.Execute("ceilcolor 10 20 30", quiet: true);
        uint ca = MapColors.Average(th.Sky);
        check(th.CeilIn != ceil && th.Sky != sky && Math.Abs(Col.B(ca) - 30) < 8 && Math.Abs(Col.R(ca) - 10) < 8, "'ceilcolor 10 20 30' tints the ceiling and the sky");

        g.Con.Execute("fogcolor navy", quiet: true);
        check(th.FogColor == Col.Rgb(20, 28, 70), "'fogcolor navy' sets the fog");

        var walls = new Dictionary<char, Tex>(th.Walls);
        g.Con.Execute("wallcolor teal flat", quiet: true);
        uint teal = Col.Rgb(40, 140, 140);
        check(th.Walls.Count == walls.Count && th.Walls.Values.All(w => w.Px.All(c => c == teal)) && th.Riser != null && th.Riser.Px.All(c => c == teal) && Art.Stone.Px.Any(c => c != teal),
            "'wallcolor teal flat' paints every kind of wall, and the faces of steps, one colour (the shared textures untouched)");
        g.Con.Execute("wallcolor #804020", quiet: true);
        uint wa = MapColors.Average(th.Walls['#']);
        check(Math.Abs(Col.R(wa) - 0x80) < 16 && Math.Abs(Col.B(wa) - 0x20) < 12 && th.Walls['#'].Px.Distinct().Count() > 4, $"'wallcolor #804020' tints them, keeping their pattern ({MapColors.Hex(wa)})");
        g.Con.Execute("wallcolor off", quiet: true);
        check(walls.All(kv => th.Walls[kv.Key] == kv.Value) && th.Riser == null, "'wallcolor off' puts the map's own walls back");

        var before = th.FloorIn;
        g.Con.Execute("floorcolor notacolour", quiet: true);
        check(th.FloorIn == before, "a colour it can't read changes nothing");

        g.Con.Execute("floorcolor off", quiet: true);
        g.Con.Execute("ceilcolor off", quiet: true);
        g.Con.Execute("fogcolor off", quiet: true);
        check(th.FloorIn == floorIn && th.FloorOut == floorOut && th.CeilIn == ceil && th.Sky == sky && th.FogColor == fog,
            "'off' puts the map's own floor, ceiling, sky and fog back");

        g.Con.Execute("floorcolor white flat", quiet: true);
        var r = new Renderer();
        r.Render(g);
        g.NewGame(PClass.Fighter);
        check(g.Level.Theme.FloorIn == floorIn || g.Level.Theme.FloorIn.Px.Any(c => c != Col.Rgb(255, 255, 255)),
            "it draws, and lasts only for this copy of the map: a restart has its own floor back");
    }
}
