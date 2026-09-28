using System.Diagnostics;

namespace HexenSharp;

/// <summary>
/// The renderer benchmark (--bench [frames]): renders fixed views, from ordinary rooms to the tall open maps, over and
/// over, and reports the time per frame for each. Nothing moves between frames, so the numbers only change when the
/// renderer does.
/// </summary>
public static partial class Headless
{
    /// <summary>The views: a map, where you stand (floor, height above it), which way you look, and how far up or down.</summary>
    public static readonly (string name, string map, float x, float y, float floor, float z, float angle, float pitch)[] BenchViews =
    {
        ("great hall", "Winnowing Hall", 10.5f, 5.5f, 0f, 0f, 0f, 0f),
        ("courtyard", "Winnowing Hall", 12.5f, 20.5f, 0f, 0f, -0.4f, 0f),
        ("windspire foot, looking up", "Windspire", 10.2f, 14.2f, 0f, 0f, -MathF.PI / 2 - 0.5f, 60f),
        ("windspire, mid-air", "Windspire", 16.5f, 6.5f, 0f, 3.4f, MathF.PI + 0.35f, 12f),
        ("windspire summit, looking down", "Windspire", 9.3f, 10.75f, 8.5f, 0f, -1.2f, -25f),
        ("cisterns floor", "Hanging Cisterns", 9.5f, 10.5f, 0f, 0f, 0.55f, 18f),
        ("cisterns, over the ledges", "Hanging Cisterns", 18.6f, 12.4f, 0f, 3.9f, 0.62f, -30f),
        ("barren world", "Barren World", 16.5f, 12.5f, 0f, 0f, -1.4f, 0f),
        ("bedrock depths (walled in)", "Bedrock Depths", 0f, 0f, 0f, 0f, 0.3f, -20f),
    };

    public static int Bench(int frames)
    {
        var g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.NewGame(PClass.Fighter);
        g.Vars.Freeze = true;
        var r = new Renderer();
        int cores = Renderer.Threads;
        foreach (int threads in cores > 1 ? new[] { 1, cores } : new[] { 1 })
        {
            Renderer.Threads = threads;
            Console.WriteLine($"-- {threads} thread{(threads == 1 ? "" : "s")}");
            BenchViewsOnce(g, r, frames);
        }
        Renderer.Threads = cores;
        return 0;
    }

    static void BenchViewsOnce(Game g, Renderer r, int frames)
    {
        Console.WriteLine($"{"view",-32} {"ms/frame",9} {"p95",7} {"fps",6}");
        double total = 0;
        foreach (var (name, map, x, y, floor, z, angle, pitch) in BenchViews)
        {
            g.Warp(Array.FindIndex(g.Hub, l => l.RawName == map));
            var p = g.P;
            if (x > 0) { p.X = x; p.Y = y; }
            p.FloorZ = floor; p.Z = z; p.Flying = z > 0; p.Angle = angle; p.Pitch = pitch; p.TeleportFlash = 0;
            g.Messages.Clear();
            for (int k = 0; k < 20; k++) r.Render(g); // warm up
            var times = new double[frames];
            var sw = new Stopwatch();
            for (int k = 0; k < frames; k++)
            {
                sw.Restart();
                r.Render(g);
                times[k] = sw.Elapsed.TotalMilliseconds;
            }
            Array.Sort(times);
            double mean = times.Average(), p95 = times[(int)(frames * 0.95)];
            total += mean;
            Console.WriteLine($"{name,-32} {mean,9:0.000} {p95,7:0.000} {1000 / mean,6:0}");
        }
        Console.WriteLine($"{"average",-32} {total / BenchViews.Length,9:0.000}");
    }
}
