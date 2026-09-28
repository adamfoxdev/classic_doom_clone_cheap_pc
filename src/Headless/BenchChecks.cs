namespace HexenSharp;

/// <summary>Checks on the CI benchmark comparison (the benchmark itself is timed, so it isn't run here).</summary>
public static partial class Headless
{
    static void BenchChecks(Action<bool, string> check)
    {
        string Run(params (string view, double ms)[] views) =>
            "-- 1 thread\nview                              ms/frame     p95    fps\n"
            + string.Concat(views.Select(v => $"{v.view,-32} {v.ms,9:0.000} {v.ms * 1.2,7:0.000} {1000 / v.ms,6:0}\n"))
            + "average                              1.000\n-- 4 threads\nview                              ms/frame     p95    fps\n"
            + string.Concat(views.Select(v => $"{v.view,-32} {v.ms / 3,9:0.000} {v.ms / 2,7:0.000} {3000 / v.ms,6:0}\n"));
        var parsed = BenchCompare.Parse(Run(("great hall", 2.0), ("windspire, mid-air", 1.5)));
        check(parsed.Count == 2 && parsed["great hall"] == 2.0 && parsed["windspire, mid-air"] == 1.5, "the comparison reads each view's single-thread time from --bench's output");
        var best = BenchCompare.Best(new[] { Run(("great hall", 2.4)), Run(("great hall", 2.0)) });
        check(best["great hall"] == 2.0, "and takes each view's best over several runs");
        var basis = BenchCompare.Parse(Run(("a", 1.0), ("b", 2.0), ("c", 0.02)));
        check(BenchCompare.Compare(basis, BenchCompare.Parse(Run(("a", 1.2), ("b", 2.3), ("c", 0.02))), 1.25).ok, "a head within 25% on average passes");
        check(!BenchCompare.Compare(basis, BenchCompare.Parse(Run(("a", 1.3), ("b", 2.7), ("c", 0.03))), 1.25).ok, "one 30% slower on average fails");
        check(!BenchCompare.Compare(basis, BenchCompare.Parse(Run(("a", 1.6), ("b", 2.0), ("c", 0.02))), 1.25).ok, "so does one view 60% slower");
        check(BenchCompare.Compare(basis, BenchCompare.Parse(Run(("a", 1.0), ("b", 2.0), ("c", 0.05))), 1.25).ok, "but not a tiny view's jitter (0.02 to 0.05 ms)");
        check(BenchCompare.Compare(basis, BenchCompare.Parse(Run(("new view", 9.0))), 1.25) is (true, var r) && r.Contains("skipped"), "with no views in common it skips");
        var report = BenchCompare.Compare(basis, BenchCompare.Parse(Run(("a", 0.9), ("b", 2.0), ("c", 0.02))), 1.25).report;
        check(report.Contains("| a | 1.000 | 0.900 | -10% |") && report.Contains("| **average** |"), "the report is a table of each view and the average");
    }
}
