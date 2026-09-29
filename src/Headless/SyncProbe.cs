namespace HexenSharp;

public static partial class Headless
{
    /// <summary>
    /// The cross-play check: a scripted two-player session (the campaign, then Rocket Soccer), and the game's checksum
    /// every second of it. Online play keeps every player's copy of the game in step by running the same frames, so
    /// the desktop and the browser can only play together while this comes out the same on both (CI compares them).
    /// </summary>
    public static string SyncProbe()
    {
        const float Frame = 1f / 60f;
        var lines = new List<string>();
        foreach (string mode in new[] { "campaign", "soccer" })
        {
            var h = new Game { AchievementsOn = false, OnlineMode = mode };
            var c = new Game { AchievementsOn = false };
            var hl = new LoopLink(); var cl = new LoopLink();
            hl.Peer = cl; cl.Peer = hl;
            h.UseNetLink(hl, "a"); c.UseNetLink(cl, "b");
            OnlineSession.StartHost(h, hl, new[] { "a", "b" }, "a", 4242);
            c.Update(new Input(), Frame);
            var hashes = new List<string>();
            for (int k = 0; k < 1200; k++)
            {
                // both moving, turning, looking, jumping and firing, on patterns that don't line up
                h.Update(new Input { Move = k % 200 < 120 ? 1 : -1, Strafe = k % 330 < 90 ? 1 : 0, LookX = k % 90 < 30 ? 4 : -1.5f, LookY = k % 70 < 20 ? 0.7f : -0.3f,
                    Fire = k % 50 == 0, Jump = k % 70 == 0, Slot = k == 400 ? 2 : 0 }, Frame);
                c.Update(new Input { Strafe = k % 160 < 80 ? 1 : -1, Move = 0.5f, LookX = -3, LookY = k % 40 < 20 ? 1 : -1, Fire = k % 45 == 0, Jump = k % 60 == 0 }, Frame);
                if (k % 60 == 59) hashes.Add(h.OnlineHash().ToString("x16"));
            }
            c.Update(new Input(), 0);
            bool together = c.NetSession != null && h.OnlineHash() == c.OnlineHash();
            lines.Add($"{mode} {(together ? "in-step" : "OUT-OF-STEP")} {string.Join(' ', hashes)}");
        }
        return string.Join('\n', lines);
    }

    /// <summary>
    /// Cross-play: the desktop and the browser run the same frames, so the game's maths must come out the same on both.
    /// The probe runs twice alike here (CI also runs it in a browser and compares), and nothing in the game's code
    /// reaches past the game's own <see cref="MathF"/> to a platform maths library (which differ in the last bit).
    /// </summary>
    static void CrossPlayChecks(Action<bool, string> check)
    {
        string probe = SyncProbe();
        check(!probe.Contains("OUT-OF-STEP") && probe == SyncProbe(), "the cross-play probe runs the same twice, the two players in step");
        check(MathF.Sin(1) == (float)DetMath.Sin(1) && MathF.Atan2(1, -1) == (float)DetMath.Atan2(1, -1) && MathF.Pow(2, 10) == 1024,
            "the game's MathF is the deterministic one");
        if (!Directory.Exists("src")) return; // (run from elsewhere than the repo: there's no source to look through)
        var platformMaths = new System.Text.RegularExpressions.Regex(
            @"\bSystem\.MathF\.(Sin|Cos|Tan|Asin|Acos|Atan|Atan2|Exp|Log|Log2|Log10|Pow|Cbrt|Sinh|Cosh|Tanh|SinCos)\b|(?<![\w.])Math\.(Sin|Cos|Tan|Asin|Acos|Atan|Atan2|Exp|Log|Log2|Log10|Pow|Cbrt|Sinh|Cosh|Tanh|SinCos)\(");
        var offenders = Directory.EnumerateFiles("src", "*.cs", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) != "MathF.cs")
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .Where(x => platformMaths.IsMatch(x.line))
            .Select(x => $"{x.f}:{x.i + 1}").ToList();
        check(offenders.Count == 0, "the game's code uses its own MathF and DetMath, never the platform's sin, atan, exp or pow"
            + (offenders.Count > 0 ? ": " + string.Join(", ", offenders) : ""));
    }
}
