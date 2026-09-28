using System.IO.Compression;
using System.Text;

namespace HexenSharp;

/// <summary>
/// Ghost codes: a practice run, or an endless run, packed into a line of text you can send a friend. They load it
/// ('ghostload') and race your ghost on the same course (or the same endless seed). The code carries the course, the
/// seed, the class, the name and time, and the path: positions every 50 ms, stored as small steps and compressed.
/// </summary>
public sealed record GhostCode(string Course, int Seed, string Class, string Name, float Time, GhostTrack Track)
{
    public const string Prefix = "HXG1.";

    /// <summary>The code: "HXG1." then the run, deflated and in URL-safe base64.</summary>
    public string Encode()
    {
        using var raw = new MemoryStream();
        using (var w = new BinaryWriter(raw, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Course); w.Write(Seed); w.Write(Class); w.Write(Name); w.Write(Time);
            w.Write(Track.Points.Count);
            // each point as the step from the one before, in 1/64ths of a cell: small numbers that compress well
            int px = 0, py = 0, pz = 0;
            foreach (var (x, y, z) in Track.Points)
            {
                int qx = (int)MathF.Round(x * 64), qy = (int)MathF.Round(y * 64), qz = (int)MathF.Round(z * 64);
                Write7(w, Zig(qx - px)); Write7(w, Zig(qy - py)); Write7(w, Zig(qz - pz));
                (px, py, pz) = (qx, qy, qz);
            }
        }
        using var packed = new MemoryStream();
        using (var d = new DeflateStream(packed, CompressionLevel.SmallestSize, leaveOpen: true)) raw.WriteTo(d);
        return Prefix + Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Reads a code back (whitespace and line breaks are ignored); null if it isn't one.</summary>
    public static GhostCode Decode(string code)
    {
        if (code == null) return null;
        code = new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (!code.StartsWith(Prefix)) return null;
        try
        {
            string b64 = code[Prefix.Length..].Replace('-', '+').Replace('_', '/');
            b64 += new string('=', (4 - b64.Length % 4) % 4);
            using var d = new DeflateStream(new MemoryStream(Convert.FromBase64String(b64)), CompressionMode.Decompress);
            using var r = new BinaryReader(d, Encoding.UTF8);
            string course = r.ReadString(); int seed = r.ReadInt32(); string cls = r.ReadString(); string name = r.ReadString(); float time = r.ReadSingle();
            int n = r.ReadInt32();
            if (n < 2 || n > 200_000 || !float.IsFinite(time) || time <= 0) return null;
            var track = new GhostTrack();
            int px = 0, py = 0, pz = 0;
            for (int i = 0; i < n; i++)
            {
                px += Unzig(Read7(r)); py += Unzig(Read7(r)); pz += Unzig(Read7(r));
                track.Points.Add((px / 64f, py / 64f, pz / 64f));
            }
            return new GhostCode(course, seed, cls, Game.CleanName(name), time, track);
        }
        catch (Exception e) when (e is FormatException or InvalidDataException or EndOfStreamException or IOException) { return null; }
    }

    static uint Zig(int v) => (uint)((v << 1) ^ (v >> 31));
    static int Unzig(uint v) => (int)(v >> 1) ^ -(int)(v & 1);
    static void Write7(BinaryWriter w, uint v) { while (v >= 0x80) { w.Write((byte)(v | 0x80)); v >>= 7; } w.Write((byte)v); }
    static uint Read7(BinaryReader r)
    {
        uint v = 0;
        for (int shift = 0; shift < 35; shift += 7)
        {
            byte b = r.ReadByte();
            v |= (uint)(b & 0x7F) << shift;
            if (b < 0x80) return v;
        }
        throw new InvalidDataException("bad number in a ghost code");
    }
}

public sealed partial class Game
{
    /// <summary>A friend's ghost you've loaded, raced instead of your own on its course (or endless seed).</summary>
    public GhostCode Rival;
    /// <summary>The path of your last endless run, for its ghost code.</summary>
    public GhostTrack LastEndlessTrack;
    /// <summary>Copies text to the clipboard (set by the windowed game; null headless).</summary>
    public Action<string> CopyText;
    /// <summary>Reads the clipboard (set by the windowed game; null headless).</summary>
    public Func<string> PasteText;
    /// <summary>Where ghost code files go (next to your profile).</summary>
    public string GhostDir => Path.Combine(Path.GetDirectoryName(ProfilePath ?? ConfigPath ?? Path.GetTempPath()) ?? ".", "ghosts");

    /// <summary>Is the rival's ghost for where you are now?</summary>
    public bool RivalHere => Rival != null && Practicing && Rival.Course == Course.Id && (!Course.Endless || Rival.Seed == Course.Seed);

    /// <summary>
    /// The code for a run to share: on the endless course your last run there (this session); on a timed course
    /// your best as this class. Null, with why, when there's nothing to share.
    /// </summary>
    public GhostCode ShareableGhost(out string why)
    {
        why = null;
        if (!Practicing || (!Course.Timed && !Course.Endless)) { why = "start a timed practice course or the endless course first"; return null; }
        if (Course.Endless)
        {
            if (LastEndless == null || LastEndlessTrack == null || LastEndless.Seed != Course.Seed || LastEndlessTrack.Points.Count < 2)
            { why = "finish an endless run on this seed first"; return null; }
            return new GhostCode(Course.Id, Course.Seed, LastEndless.Class, LastEndless.Name, LastEndless.Time, LastEndlessTrack);
        }
        if (!Profile.Ghosts.TryGetValue(Course.Key(P.Class), out var best)) { why = $"set a time on {Course.Name} as the {P.Def.Name} first"; return null; }
        return new GhostCode(Course.Id, 0, P.Class.ToString(), RunnerName, best.Time, GhostTrack.Decode(best.Path));
    }

    /// <summary>
    /// Loads a friend's ghost from a code: goes to its course (or endless seed) if you're not already there, as your
    /// class (or theirs, from the title), and races it. False, with why, if the code won't do.
    /// </summary>
    public bool LoadGhost(GhostCode code, out string why)
    {
        why = null;
        var course = Courses.All.FirstOrDefault(c => c.Id == code.Course);
        if (course == null || (!course.Timed && !course.Endless)) { why = "that code isn't for a course in this game"; return false; }
        var cls = Mode == GameMode.Playing && P != null ? P.Class : Enum.TryParse<PClass>(code.Class, out var c) ? c : PClass.Fighter;
        Rival = code;
        bool here = Practicing && Course.Id == code.Course && (!course.Endless || Course.Seed == code.Seed);
        if (!here)
        {
            Style = GameStyle.Classic;
            if (course.Endless) StartEndless(cls, code.Seed); else StartPractice(cls, course);
            Rival = code; // starting a course keeps it
        }
        else ShowGhost();
        Say($"Racing {code.Name}'s ghost ({code.Time:0.00}s" + (course.Endless ? $", seed {code.Seed})." : ")."));
        return true;
    }

    /// <summary>Saves a code to a file in the ghosts folder, named for the course, class and time; returns its path.</summary>
    public string SaveGhostFile(GhostCode code)
    {
        Directory.CreateDirectory(GhostDir);
        string name = $"{code.Course}{(code.Seed > 0 ? "-" + code.Seed : "")}-{code.Class.ToLowerInvariant()}-{code.Time:0.00}.hxghost".Replace(',', '.');
        string path = Path.Combine(GhostDir, name);
        File.WriteAllText(path, code.Encode() + Environment.NewLine);
        return path;
    }
}
