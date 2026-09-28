namespace HexenSharp;

/// <summary>Checks on ghost codes: packing a run into a code, and racing a friend's.</summary>
public static partial class Headless
{
    static void GhostCodeChecks(Action<bool, string> check)
    {
        // a run, packed and unpacked
        var track = new GhostTrack();
        for (int i = 0; i < 260; i++) track.Record(i * GhostTrack.Step, 2.5f + i * 0.31f, 5.5f + MathF.Sin(i * 0.2f) * 1.7f, 2.5f + MathF.Max(0, MathF.Sin(i * 0.35f)) * 0.8f);
        var code = new GhostCode("hangar", 0, "Mage", "ACE-1", 13.05f, track);
        string text = code.Encode();
        var back = GhostCode.Decode(text);
        bool close = back != null && back.Track.Points.Count == track.Points.Count
            && back.Track.Points.Zip(track.Points).All(t => MathF.Abs(t.First.x - t.Second.x) <= 1 / 64f && MathF.Abs(t.First.y - t.Second.y) <= 1 / 64f && MathF.Abs(t.First.z - t.Second.z) <= 1 / 64f);
        check(text.StartsWith(GhostCode.Prefix) && text[GhostCode.Prefix.Length..].All(c => char.IsLetterOrDigit(c) || c is '-' or '_'), "a code is one line of plain, URL-safe text");
        check(close && back.Course == "hangar" && back.Class == "Mage" && back.Name == "ACE-1" && back.Time == 13.05f, "and reads back as the same run, to within 1/64 of a cell");
        check(text.Length < 1500, $"a 13-second run is {text.Length} characters");
        check(GhostCode.Decode(text[..(text.Length / 2)]) == null && GhostCode.Decode("HXG1.hello") == null && GhostCode.Decode("not a code") == null,
              "a damaged or cut-short code is refused");
        check(GhostCode.Decode(" " + text[..40] + "\n" + text[40..] + "\r\n")?.Time == 13.05f, "line breaks and spaces (from pasting) don't matter");

        string dir = Path.Combine(Path.GetTempPath(), $"hexensharp-ghosts-{Environment.ProcessId}");
        Directory.CreateDirectory(dir);
        Game G() => new() { FixedSeed = 1, AchievementsOn = false, Profile = new Profile(), ProfilePath = Path.Combine(dir, "profile.json") };

        // sharing: your best on a timed course, your last endless run
        var g = G();
        g.StartPractice(PClass.Fighter, Courses.FreeRoam);
        check(g.ShareableGhost(out string why) == null && why.Contains("timed practice course"), "nothing to share in Free Roam");
        g.StartPractice(PClass.Fighter, Courses.Hangar);
        check(g.ShareableGhost(out why) == null && why.StartsWith("set a time"), "nor before you've set a time");
        g.Profile.Ghosts[Courses.Hangar.Key(PClass.Fighter)] = new CourseGhost { Time = 12.4f, Path = track.Encode() };
        string copied = null;
        g.CopyText = t => copied = t;
        g.Con.Execute("ghostcode");
        var shared = GhostCode.Decode(copied);
        var files = Directory.GetFiles(g.GhostDir, "*.hxghost");
        check(shared != null && shared.Course == "hangar" && shared.Time == 12.4f && shared.Name == g.RunnerName && shared.Class == "Fighter",
              "'ghostcode' copies your best on the course as a code");
        check(files.Length == 1 && GhostCode.Decode(File.ReadAllText(files[0]))?.Time == 12.4f && g.Con.Log.Any(l => l.StartsWith(GhostCode.Prefix[..4])),
              "prints it, and saves it to a .hxghost file in your ghosts folder");

        var e = G();
        e.StartEndless(PClass.Cleric, 777);
        var plats = e.Course.Platforms;
        void Tick(int n) { for (int k = 0; k < n; k++) e.Update(default, 1f / 35f); }
        Tick(1);
        foreach (int k in new[] { 1, 2, 3 })
        {
            for (int s = 0; s < 20; s++) { e.P.X = plats[k].x0 + 1.5f + s * 0.1f; e.P.Y = 5.5f; e.P.FloorZ = plats[k].floor; e.P.Z = 0; Tick(1); }
        }
        e.P.X = plats[3].x1 + 1.5f; e.P.FloorZ = 0; e.P.Z = 0.3f;
        Tick(20);
        var endless = e.ShareableGhost(out why);
        check(endless != null && endless.Course == "endless" && endless.Seed == 777 && endless.Track.Points.Count > 20 && endless.Time == e.LastEndless.Time,
              "on the endless course, your last run there, with its seed");

        // racing a friend's
        var f = G();
        check(f.LoadGhost(code, out why) && f.Practicing && f.Course == Courses.Hangar && f.RivalHere, "loading a code from the title takes you to its course");
        f.Update(default, 1f / 35f);
        check(f.Ghost != null && f.Ghost.Time == 13.05f && f.P.Class == PClass.Mage, "racing its ghost, as its class");
        check(f.Messages.Any(m => m.text == "Racing ACE-1's ghost (13.05s)."), "and says whose");
        f.Con.Execute("ghostclear");
        f.Update(default, 1f / 35f);
        check(f.Rival == null && f.Ghost == null, "'ghostclear' goes back to your own ghost (none yet here)");

        var h = G();
        h.PasteText = () => endless.Encode();
        h.Con.Execute("ghostload");
        h.Update(default, 1f / 35f);
        check(h.OnEndless && h.Course.Seed == 777 && h.Ghost != null && h.Ghost.Time == endless.Time, "'ghostload' reads a code off the clipboard: an endless one takes you to its seed, with the ghost");
        var gone = G();
        gone.PasteText = () => "nonsense";
        gone.Con.Execute("ghostload");
        check(gone.Con.Log.Last().Contains("damaged or incomplete") && gone.Rival == null, "a bad code is refused");
        var byFile = G();
        byFile.Con.Execute("ghostload " + Path.GetFileName(files[0]));
        check(byFile.RivalHere && byFile.Rival.Time == 12.4f, "and 'ghostload <file>' loads one from your ghosts folder");
        var other = G();
        other.LoadGhost(code, out _);
        other.StartPractice(PClass.Mage, Courses.Descent);
        other.Update(default, 1f / 35f);
        check(!other.RivalHere && other.Ghost == null, "a friend's ghost only runs on its own course");
        Directory.Delete(dir, true);
    }
}
