namespace HexenSharp;

/// <summary>
/// A map file (.hxm): a name, a theme and a grid of map glyphs, the same format the hub maps use. Maps are made in
/// the HTML editor (tools/editor/index.html), which reads and writes this format and makes the same checks.
/// </summary>
public sealed class MapDoc
{
    public string Name = "Untitled";
    public string ThemeId = "hall";
    public int W, H;
    public char[] Cells;
    /// <summary>Ceiling height per cell: '2'..'9' (1.0..4.5), or '.' for the map's default height.</summary>
    public char[] Heights;
    public float DefaultHeight = 1f;
    /// <summary>Floor height per cell: '1'..'9' (0.25..2.25), or '.' for ground level.</summary>
    public char[] Floors;
    /// <summary>The map's hazard ("none", "wind" or "flood") and its chance of elites (0 to 0.5), every time it's played.</summary>
    public string Hazard = "none";
    public float Elites;
    public static readonly string[] Hazards = { "none", "wind", "flood" };

    public MapDoc(int w, int h)
    {
        W = w; H = h;
        Heights = Enumerable.Repeat('.', w * h).ToArray();
        Floors = Enumerable.Repeat('.', w * h).ToArray();
        Cells = new char[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                Cells[y * w + x] = x == 0 || y == 0 || x == w - 1 || y == h - 1 ? '#' : '.';
    }

    public char this[int x, int y]
    {
        get => (uint)x < (uint)W && (uint)y < (uint)H ? Cells[y * W + x] : '#';
        set { if ((uint)x < (uint)W && (uint)y < (uint)H) Cells[y * W + x] = value; }
    }

    public string[] Rows() => Enumerable.Range(0, H).Select(y => new string(Cells, y * W, W)).ToArray();
    public string[] HeightRows() => Enumerable.Range(0, H).Select(y => new string(Heights, y * W, W)).ToArray();
    public bool HasHeights => Heights.Any(h => h != '.');
    public string[] FloorRows() => Enumerable.Range(0, H).Select(y => new string(Floors, y * W, W)).ToArray();
    public bool HasFloors => Floors.Any(f => f != '.');
    public MapDef ToDef() => new(Name, Name, ThemeId, Rows(), HasHeights ? HeightRows() : null, DefaultHeight, HasFloors ? FloorRows() : null,
        Hazard: Hazard == "none" ? null : Hazard, Elites: Elites);

    public static bool IsHeightGlyph(char c) => Level.IsHeightGlyph(c);

    public static MapDoc FromDef(MapDef d)
    {
        var doc = new MapDoc(d.Rows.Max(r => r.Length), d.Rows.Length) { Name = d.Name, ThemeId = d.ThemeId, DefaultHeight = d.Height,
            Hazard = Hazards.Contains(d.Hazard) ? d.Hazard : "none", Elites = Math.Clamp(d.Elites, 0, 0.5f) };
        for (int y = 0; y < doc.H; y++)
            for (int x = 0; x < doc.W; x++)
            {
                doc[x, y] = x < d.Rows[y].Length && IsKnownGlyph(d.Rows[y][x]) ? d.Rows[y][x] : '.';
                char h = d.Heights != null && y < d.Heights.Length && x < d.Heights[y].Length ? d.Heights[y][x] : '.';
                // store only what differs from the default, so the default stays editable
                doc.Heights[y * doc.W + x] = IsHeightGlyph(h) && Level.HeightFromGlyph(h, 1f) != doc.DefaultHeight ? h : '.';
                char f = d.Floors != null && y < d.Floors.Length && x < d.Floors[y].Length ? d.Floors[y][x] : '.';
                doc.Floors[y * doc.W + x] = Level.IsFloorGlyph(f) ? f : '.';
            }
        return doc;
    }

    // ---- file format: "name:" and "theme:" header lines, a "---" line, then the rows

    public string Serialize() =>
        $"# Hexen Sharp map\nname: {Name}\ntheme: {ThemeId}\nheight: {DefaultHeight.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\n"
        + (Hazard != "none" ? $"hazard: {Hazard}\n" : "")
        + (Elites > 0 ? $"elites: {Elites.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}\n" : "")
        + "---\n"
        + string.Join("\n", Rows()) + "\n"
        + (HasHeights || HasFloors ? "---\n" + string.Join("\n", HeightRows()) + "\n" : "")
        + (HasFloors ? "---\n" + string.Join("\n", FloorRows()) + "\n" : "");

    public static MapDoc Parse(string text)
    {
        string name = "Untitled", theme = "hall", hazard = "none";
        float height = 1f, elites = 0;
        var rows = new List<string>();
        var heights = new List<string>();
        var floors = new List<string>();
        int section = 0; // 0 header, 1 map rows, 2 height rows, 3 floor rows
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            if (raw.Trim() == "---") { section++; continue; }
            if (section == 1) { if (raw.Length > 0) rows.Add(raw); continue; }
            if (section == 2) { if (raw.Length > 0) heights.Add(raw); continue; }
            if (section >= 3) { if (raw.Length > 0) floors.Add(raw); continue; }
            var line = raw.Trim();
            if (line.StartsWith("name:")) name = line[5..].Trim();
            else if (line.StartsWith("theme:")) theme = line[6..].Trim();
            else if (line.StartsWith("height:") && float.TryParse(line[7..].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h))
                height = Math.Clamp(MathF.Round(h * 2) / 2, Level.MinHeight, Level.MaxHeight);
            else if (line.StartsWith("hazard:")) hazard = line[7..].Trim().ToLowerInvariant();
            else if (line.StartsWith("elites:") && float.TryParse(line[7..].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float e))
                elites = Math.Clamp(MathF.Round(e * 100) / 100, 0, 0.5f);
        }
        if (rows.Count == 0) throw new InvalidDataException("map has no rows");
        if (!Maps.ThemeIds.Contains(theme)) theme = "hall";
        return FromDef(new MapDef(name, name, theme, rows.ToArray(), heights.Count > 0 ? heights.ToArray() : null, height,
            floors.Count > 0 ? floors.ToArray() : null, Hazard: hazard, Elites: elites));
    }

    /// <summary>Every glyph a map can use (the map legend in the README). Anything else loads as floor.</summary>
    public const string KnownGlyphs = "#BWMIO.,DSFPLZX^KNQU@E123456789*!+=eacCdHGRYoyhqubgrkfwxm$%&JVAtpT";

    public static bool IsKnownGlyph(char c) => KnownGlyphs.IndexOf(c) >= 0;

    /// <summary>The file a map saves to: its name, lower-cased, with anything but letters and digits as '_'.</summary>
    public static string FileName(string name)
    {
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray()).Trim('_');
        return (safe.Length == 0 ? "untitled" : safe) + ".hxm";
    }

    /// <summary>
    /// Problems with a map. Entries starting with "!" mean it can't be played. The HTML editor makes exactly these
    /// checks with the same messages; its test compares them through --check-map.
    /// </summary>
    public List<string> Validate()
    {
        var issues = new List<string>();
        int starts = Cells.Count(c => c == '@');
        if (starts == 0) { issues.Add("! Place a player start (@) first."); return issues; }
        var lv = ToDef().Build();
        // a map with a jetpack in it can be flown around; otherwise only what you can walk to counts
        var move = lv.Things.Any(t => t is Pickup { Kind: PickupKind.Jetpack }) ? Level.Move.Fly : Level.Move.Walk;
        var reach = lv.Reachable((int)lv.StartX, (int)lv.StartY, move: move);
        int unreachable = lv.Things.Count(t => t is Monster or Pickup or Chest && !reach[(int)t.Y * lv.W + (int)t.X]);
        var exit = lv.FindMark('E');
        if (exit == null) issues.Add("No exit (E): the map can't be won.");
        else if (!reach[(int)exit.Value.y * lv.W + (int)exit.Value.x]) issues.Add("The exit can't be reached from the start.");
        if (unreachable > 0) issues.Add($"{unreachable} monster(s)/item(s) can't be reached.");
        if (Cells.Any(char.IsDigit)) issues.Add("Portals only link maps in the built-in hub.");
        return issues;
    }
}
