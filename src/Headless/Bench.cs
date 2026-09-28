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

/// <summary>
/// Comparing two benchmarks (--bench-compare limit base.txt... -- head.txt...), for CI: each side's --bench output
/// (one or more runs; each view's best is taken, which shrugs off a noisy run), the single-thread timings view by
/// view. It fails (exit 1) if the head is more than `limit` times slower than the base on average, or on any one view
/// by more than ViewLimit times and MinSlowdownMs (single views are noisier, so only a big jump there counts).
/// </summary>
public static class BenchCompare
{
    public const double MinSlowdownMs = 0.05, ViewLimit = 1.5;

    /// <summary>The single-thread section of a --bench run: each view's time per frame, in ms.</summary>
    public static Dictionary<string, double> Parse(string text)
    {
        var views = new Dictionary<string, double>();
        bool single = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("-- ")) { if (single && views.Count > 0) break; single = line.StartsWith("-- 1 thread") && !line.StartsWith("-- 1 threads"); continue; }
            if (!single || line.StartsWith("view ") || line.StartsWith("average")) continue;
            // "name ... ms p95 fps": the name is everything before the last three numbers
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;
            if (!double.TryParse(parts[^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ms)) continue;
            string name = string.Join(' ', parts[..^3]);
            views[name] = views.TryGetValue(name, out double had) ? Math.Min(had, ms) : ms;
        }
        return views;
    }

    /// <summary>Several runs: each view's best.</summary>
    public static Dictionary<string, double> Best(IEnumerable<string> texts)
    {
        var best = new Dictionary<string, double>();
        foreach (var t in texts)
            foreach (var (k, v) in Parse(t))
                best[k] = best.TryGetValue(k, out double had) ? Math.Min(had, v) : v;
        return best;
    }

    /// <summary>The verdict, and a Markdown table of it.</summary>
    public static (bool ok, string report) Compare(Dictionary<string, double> baseline, Dictionary<string, double> head, double limit)
    {
        var sb = new System.Text.StringBuilder();
        var common = baseline.Keys.Where(head.ContainsKey).ToList();
        if (common.Count == 0) return (true, "No views in common to compare (the benchmark changed): skipped.");
        sb.AppendLine("| View | Base ms | This PR ms | Change |");
        sb.AppendLine("|---|---:|---:|---:|");
        bool ok = true;
        foreach (var k in common)
        {
            double b = baseline[k], h = head[k], ratio = h / Math.Max(1e-6, b);
            bool bad = ratio > Math.Max(limit, ViewLimit) && h - b > MinSlowdownMs;
            ok &= !bad;
            sb.AppendLine($"| {k} | {b:0.000} | {h:0.000} | {Pct(ratio)}{(bad ? " **too slow**" : "")} |");
        }
        double bAvg = common.Average(k => baseline[k]), hAvg = common.Average(k => head[k]), avgRatio = hAvg / Math.Max(1e-6, bAvg);
        bool avgBad = avgRatio > limit;
        ok &= !avgBad;
        sb.AppendLine($"| **average** | {bAvg:0.000} | {hAvg:0.000} | {Pct(avgRatio)}{(avgBad ? " **too slow**" : "")} |");
        sb.AppendLine();
        sb.AppendLine(ok ? $"Within {(limit - 1) * 100:0}% of the base." : $"Slower than the base by more than {(limit - 1) * 100:0}%.");
        return (ok, sb.ToString());
    }

    static string Pct(double ratio) { int p = (int)Math.Round((ratio - 1) * 100); return (p > 0 ? "+" : "") + p + "%"; }

    public static int Run(string[] args)
    {
        // --bench-compare limit base files... -- head files...
        int at = Array.IndexOf(args, "--bench-compare");
        var rest = args.Skip(at + 1).ToList();
        if (rest.Count < 4 || !double.TryParse(rest[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double limit) || !rest.Contains("--"))
        {
            Console.WriteLine("usage: --bench-compare limit base.txt... -- head.txt...");
            return 2;
        }
        int sep = rest.IndexOf("--");
        var baseFiles = rest.Skip(1).Take(sep - 1).ToList();
        var headFiles = rest.Skip(sep + 1).ToList();
        var (ok, report) = Compare(Best(baseFiles.Select(File.ReadAllText)), Best(headFiles.Select(File.ReadAllText)), limit);
        Console.WriteLine(report);
        string summary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (!string.IsNullOrEmpty(summary)) File.AppendAllText(summary, "## Renderer benchmark\n\n" + report + "\n");
        return ok ? 0 : 1;
    }
}

public static partial class Headless
{
    /// <summary>--replay file: plays a replay headlessly and prints where it ended.</summary>
    public static int PlayReplay(string path)
    {
        var r = File.Exists(path) ? Replay.Decode(File.ReadAllText(path)) : null;
        if (r == null) { Console.WriteLine("not a replay: " + path); return 2; }
        var g = r.Play();
        Console.WriteLine($"{r.Start.Kind} as the {r.Start.Class}, seed {r.Start.Seed}: {r.Frames.Count} frames, {r.Duration:0.00}s");
        Console.WriteLine(Replay.StateHash(g));
        return 0;
    }
}
