namespace HexenSharp;

/// <summary>An editable map: a name, a theme and a grid of map glyphs (the same format the hub maps use).</summary>
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
    public MapDef ToDef() => new(Name, Name, ThemeId, Rows(), HasHeights ? HeightRows() : null, DefaultHeight, HasFloors ? FloorRows() : null);

    public static bool IsHeightGlyph(char c) => Level.IsHeightGlyph(c);

    public static MapDoc FromDef(MapDef d)
    {
        var doc = new MapDoc(d.Rows.Max(r => r.Length), d.Rows.Length) { Name = d.Name, ThemeId = d.ThemeId, DefaultHeight = d.Height };
        for (int y = 0; y < doc.H; y++)
            for (int x = 0; x < doc.W; x++)
            {
                doc[x, y] = x < d.Rows[y].Length && Editor.IsKnownGlyph(d.Rows[y][x]) ? d.Rows[y][x] : '.';
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
        $"# Hexen Sharp map\nname: {Name}\ntheme: {ThemeId}\nheight: {DefaultHeight.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\n---\n"
        + string.Join("\n", Rows()) + "\n"
        + (HasHeights || HasFloors ? "---\n" + string.Join("\n", HeightRows()) + "\n" : "")
        + (HasFloors ? "---\n" + string.Join("\n", FloorRows()) + "\n" : "");

    public static MapDoc Parse(string text)
    {
        string name = "Untitled", theme = "hall";
        float height = 1f;
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
        }
        if (rows.Count == 0) throw new InvalidDataException("map has no rows");
        if (!Maps.ThemeIds.Contains(theme)) theme = "hall";
        return FromDef(new MapDef(name, name, theme, rows.ToArray(), heights.Count > 0 ? heights.ToArray() : null, height,
            floors.Count > 0 ? floors.ToArray() : null));
    }
}

/// <summary>
/// The in-game level editor: paint map glyphs onto a grid with the mouse (or keyboard), then play-test
/// the result straight away. Maps are saved as small text files.
/// </summary>
public sealed class Editor
{
    public sealed record Brush(char Glyph, string Label, string Group);

    public static readonly Brush[] Palette =
    {
        new('#', "Stone wall", "Walls"), new('B', "Brick wall", "Walls"), new('W', "Wood wall", "Walls"),
        new('M', "Mossy wall", "Walls"), new('I', "Ice wall", "Walls"), new('O', "Marble wall", "Walls"),
        new('.', "Floor (indoor)", "Floors"), new(',', "Floor (outdoor, sky)", "Floors"),
        new('D', "Door", "Doors"), new('S', "Steel key door", "Doors"), new('F', "Fire key door", "Doors"),
        new('P', "Portcullis", "Doors"), new('L', "Lever", "Doors"), new('Z', "Secret wall", "Doors"),
        new('X', "Push block", "Puzzles"), new('^', "Pressure plate", "Puzzles"),
        new('@', "Player start", "Markers"), new('E', "Exit", "Markers"), new('1', "Portal 1", "Markers"),
        new('2', "Portal 2", "Markers"), new('3', "Portal 3", "Markers"), new('4', "Portal 4", "Markers"), new('*', "Arena spawn rune", "Markers"),
        new('!', "Arena altar", "Markers"),
        new('e', "Ettin", "Monsters"), new('a', "Afrit", "Monsters"), new('c', "Centaur", "Monsters"),
        new('C', "Slaughtaur", "Monsters"), new('d', "Dark Bishop", "Monsters"), new('H', "Heresiarch", "Monsters"),
        new('h', "Crystal vial", "Items"), new('q', "Quartz flask", "Items"), new('u', "Mystic urn", "Items"),
        new('b', "Blue mana", "Items"), new('g', "Green mana", "Items"), new('r', "Mesh armor", "Items"),
        new('k', "Steel key", "Items"), new('f', "Fire key", "Items"), new('w', "Weapon piece 2", "Items"),
        new('x', "Weapon piece 3", "Items"), new('$', "Chest", "Items"), new('%', "Secret treasure", "Items"),
        new('&', "Lore stone", "Items"), new('J', "Jetpack / Wings of Wrath", "Items"),
        new('t', "Torch", "Decor"), new('p', "Pillar", "Decor"), new('T', "Tree", "Decor"),
    };

    public static bool IsKnownGlyph(char c) => Palette.Any(b => b.Glyph == c);

    /// <summary>Height-mode palette: the map default, then 1.0 to 10.0 in half steps.</summary>
    public static readonly char[] HeightPalette = ".23456789abcdefghijk".ToCharArray();
    /// <summary>Floors-mode palette: ground level, 0.25 to 2.25 in quarter steps, then half steps up to 8.5 (towers and ledges).</summary>
    public static readonly char[] FloorPalette = ".123456789acegikmoqsuwy".ToCharArray();
    public static string FloorLabel(char c) =>
        c == '.' ? "Ground (0)" : Level.FloorFromGlyph(c).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    public enum Layer { Tiles, Ceilings, Floors }
    public static string HeightLabel(char c, float def) =>
        c == '.' ? $"Default ({def.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)})"
                 : Level.HeightFromGlyph(c, 1f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

    public const int MapViewW = 240, MapViewH = 188;
    public static readonly int[] Zooms = { 4, 6, 8, 12, 16 };

    readonly Game _g;
    public MapDoc Doc = new(32, 24);
    public int CursorX = 2, CursorY = 2, CamX, CamY, ZoomIndex = 2, BrushIndex;
    public bool FillTool, ShowHelp = true, Dirty, StairBrush;
    public Layer Mode = Layer.Tiles;
    public bool HeightMode { get => Mode == Layer.Ceilings; set => Mode = value ? Layer.Ceilings : Layer.Tiles; }
    public int HeightIndex = 4, FloorIndex = 1;
    public char CurrentHeight => HeightPalette[HeightIndex];
    public char CurrentFloor => FloorPalette[FloorIndex];

    // the height layer being painted (ceilings or floors)
    char[] LayerCells => Mode == Layer.Floors ? Doc.Floors : Doc.Heights;
    char[] LayerPalette => Mode == Layer.Floors ? FloorPalette : HeightPalette;
    int LayerIndex
    {
        get => Mode == Layer.Floors ? FloorIndex : HeightIndex;
        set { if (Mode == Layer.Floors) FloorIndex = value; else HeightIndex = value; }
    }
    char CurrentLayer => LayerPalette[LayerIndex];
    string LayerLabel(char c) => Mode == Layer.Floors ? "Floor " + FloorLabel(c) : "Ceiling " + HeightLabel(c, Doc.DefaultHeight);
    int _stairLastCell = -1;
    char _stairLast;
    public string Status = "", RenameText;
    public float StatusTime;
    public PClass PlayClass = PClass.Fighter;
    public List<(string label, string path, MapDef builtin)> OpenList; // non-null while the open dialog is up
    public int OpenCursor;
    float _leaveArmed;
    bool _stroke;
    readonly List<(char[] cells, char[] heights, char[] floors)> _undo = new(), _redo = new();

    public Editor(Game g) { _g = g; }

    public int CellSize => Zooms[ZoomIndex];
    public Brush Current => Palette[BrushIndex];

    public void Say(string s, float time = 3f) { Status = s; StatusTime = time; }

    // ------------------------------------------------------------ editing primitives

    void PushUndo()
    {
        _undo.Add(((char[])Doc.Cells.Clone(), (char[])Doc.Heights.Clone(), (char[])Doc.Floors.Clone()));
        if (_undo.Count > 100) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public void Undo()
    {
        if (_undo.Count == 0) { Say("Nothing to undo."); return; }
        _redo.Add((Doc.Cells, Doc.Heights, Doc.Floors));
        (Doc.Cells, Doc.Heights, Doc.Floors) = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Dirty = true;
        Say("Undo.");
    }

    public void Redo()
    {
        if (_redo.Count == 0) { Say("Nothing to redo."); return; }
        _undo.Add((Doc.Cells, Doc.Heights, Doc.Floors));
        (Doc.Cells, Doc.Heights, Doc.Floors) = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        Dirty = true;
        Say("Redo.");
    }

    public void Paint(int x, int y, char glyph)
    {
        if ((uint)x >= (uint)Doc.W || (uint)y >= (uint)Doc.H || Doc[x, y] == glyph) return;
        // only one player start per map
        if (glyph == '@')
            for (int i = 0; i < Doc.Cells.Length; i++) if (Doc.Cells[i] == '@') Doc.Cells[i] = '.';
        Doc[x, y] = glyph;
        Dirty = true;
    }

    public void PaintHeight(int x, int y, char h)
    {
        var layer = LayerCells;
        if ((uint)x >= (uint)Doc.W || (uint)y >= (uint)Doc.H || layer[y * Doc.W + x] == h) return;
        layer[y * Doc.W + x] = h;
        Dirty = true;
    }

    /// <summary>Height fill: every open cell of the same height connected to the clicked one (walls bound it).</summary>
    public int FillHeight(int x, int y, char h)
    {
        if ((uint)x >= (uint)Doc.W || (uint)y >= (uint)Doc.H) return 0;
        var layer = LayerCells;
        char target = layer[y * Doc.W + x];
        if (target == h) return 0;
        bool Open(int cx, int cy) => !"#BWMIO".Contains(Doc[cx, cy]);
        var q = new Queue<(int, int)>();
        q.Enqueue((x, y));
        layer[y * Doc.W + x] = h;
        int n = 1;
        while (q.Count > 0)
        {
            var (cx, cy) = q.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = cx + dx, ny = cy + dy;
                if ((uint)nx >= (uint)Doc.W || (uint)ny >= (uint)Doc.H || layer[ny * Doc.W + nx] != target || !Open(nx, ny)) continue;
                layer[ny * Doc.W + nx] = h;
                q.Enqueue((nx, ny));
                n++;
            }
        }
        Dirty = true;
        return n;
    }

    /// <summary>Flood-fills the connected area of the clicked glyph (4-way) with the brush.</summary>
    public int Fill(int x, int y, char glyph)
    {
        char target = Doc[x, y];
        if (target == glyph || (uint)x >= (uint)Doc.W || (uint)y >= (uint)Doc.H) return 0;
        var q = new Queue<(int, int)>();
        q.Enqueue((x, y));
        Doc[x, y] = glyph;
        int n = 1;
        while (q.Count > 0)
        {
            var (cx, cy) = q.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = cx + dx, ny = cy + dy;
                if ((uint)nx >= (uint)Doc.W || (uint)ny >= (uint)Doc.H || Doc[nx, ny] != target) continue;
                Doc[nx, ny] = glyph;
                q.Enqueue((nx, ny));
                n++;
            }
        }
        Dirty = true;
        return n;
    }

    public void NewMap(int w, int h)
    {
        Doc = new MapDoc(w, h) { DefaultHeight = 1.5f };
        Doc[2, 2] = '@';
        _undo.Clear(); _redo.Clear();
        CursorX = CursorY = 2; CamX = CamY = 0;
        Dirty = false;
        Say($"New {w}x{h} map.");
    }

    public void Load(MapDoc doc, string how)
    {
        Doc = doc;
        _undo.Clear(); _redo.Clear();
        CursorX = Math.Min(2, doc.W - 1); CursorY = Math.Min(2, doc.H - 1); CamX = CamY = 0;
        Dirty = false;
        Say($"{how} '{doc.Name}'.");
    }

    // ------------------------------------------------------------ files

    public static string FileName(string name)
    {
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray()).Trim('_');
        return (safe.Length == 0 ? "untitled" : safe) + ".hxm";
    }

    public bool Save()
    {
        if (_g.MapsDir == null) { Say("No folder to save maps in."); return false; }
        try
        {
            Directory.CreateDirectory(_g.MapsDir);
            string path = Path.Combine(_g.MapsDir, FileName(Doc.Name));
            File.WriteAllText(path, Doc.Serialize());
            Dirty = false;
            Say($"Saved {Path.GetFileName(path)}");
            return true;
        }
        catch (Exception e) { Say("Save failed: " + e.Message); return false; }
    }

    public void ShowOpenList()
    {
        OpenList = new();
        foreach (var d in Maps.Hub) OpenList.Add(($"{d.Name} (built-in)", null, d));
        if (_g.MapsDir != null && Directory.Exists(_g.MapsDir))
            foreach (var f in Directory.GetFiles(_g.MapsDir, "*.hxm").OrderBy(f => f))
                OpenList.Add((Path.GetFileNameWithoutExtension(f), f, null));
        OpenCursor = 0;
    }

    void OpenSelected()
    {
        var (label, path, builtin) = OpenList[OpenCursor];
        OpenList = null;
        try { Load(builtin != null ? MapDoc.FromDef(builtin) : MapDoc.Parse(File.ReadAllText(path)), "Opened"); }
        catch (Exception e) { Say($"Can't open {label}: {e.Message}"); }
    }

    // ------------------------------------------------------------ validation and play-testing

    /// <summary>Problems with the map. Entries starting with "!" stop a play-test.</summary>
    public List<string> Validate()
    {
        var issues = new List<string>();
        int starts = Doc.Cells.Count(c => c == '@');
        if (starts == 0) { issues.Add("! Place a player start (@) first."); return issues; }
        var lv = Doc.ToDef().Build();
        // a map with a jetpack in it can be flown around; otherwise only what you can walk to counts
        var move = lv.Things.Any(t => t is Pickup { Kind: PickupKind.Jetpack }) ? Level.Move.Fly : Level.Move.Walk;
        var reach = lv.Reachable((int)lv.StartX, (int)lv.StartY, move: move);
        int unreachable = lv.Things.Count(t => t is Monster or Pickup or Chest && !reach[(int)t.Y * lv.W + (int)t.X]);
        var exit = lv.FindMark('E');
        if (exit == null) issues.Add("No exit (E): the map can't be won.");
        else if (!reach[(int)exit.Value.y * lv.W + (int)exit.Value.x]) issues.Add("The exit can't be reached from the start.");
        if (unreachable > 0) issues.Add($"{unreachable} monster(s)/item(s) can't be reached.");
        if (Doc.Cells.Any(char.IsDigit)) issues.Add("Portals only link maps in the built-in hub.");
        return issues;
    }

    public bool PlayTest()
    {
        var issues = Validate();
        var blocking = issues.FirstOrDefault(i => i.StartsWith("!"));
        if (blocking != null) { Say(blocking[2..]); return false; }
        _g.StartTest(Doc.ToDef(), PlayClass);
        if (issues.Count > 0) _g.Say("Note: " + issues[0]);
        return true;
    }

    // ------------------------------------------------------------ input

    void KeepCursorVisible()
    {
        int cs = CellSize, cols = MapViewW / cs, rows = MapViewH / cs;
        if (CursorX < CamX) CamX = CursorX;
        if (CursorY < CamY) CamY = CursorY;
        if (CursorX >= CamX + cols) CamX = CursorX - cols + 1;
        if (CursorY >= CamY + rows) CamY = CursorY - rows + 1;
        CamX = Math.Clamp(CamX, 0, Math.Max(0, Doc.W - cols));
        CamY = Math.Clamp(CamY, 0, Math.Max(0, Doc.H - rows));
    }

    /// <summary>Which palette entry is at a screen position, or -1.</summary>
    public static int PaletteAt(float mx, float my, int count)
    {
        int col = (int)((mx - PaletteX) / PaletteCell), row = (int)((my - PaletteY) / PaletteCell);
        if (mx < PaletteX || my < PaletteY || col >= PaletteCols) return -1;
        int i = row * PaletteCols + col;
        return i >= 0 && i < count ? i : -1;
    }

    public const int PaletteX = 242, PaletteY = 12, PaletteCell = 15, PaletteCols = 5;

    public void Update(Input inp, float dt)
    {
        StatusTime -= dt;
        _leaveArmed -= dt;
        var k = _g.Keys ?? NoKeys.Instance;
        bool ctrl = k.Down(Keys.LeftControl) || k.Down(Keys.RightControl);

        if (RenameText != null)
        {
            if (!string.IsNullOrEmpty(inp.Typed))
                foreach (char c in inp.Typed) if (c >= ' ' && c < 127 && RenameText.Length < 28) RenameText += c;
            if (inp.Backspace && RenameText.Length > 0) RenameText = RenameText[..^1];
            if (inp.Confirm && RenameText.Trim().Length > 0) { Doc.Name = RenameText.Trim(); Dirty = true; RenameText = null; Say($"Renamed to '{Doc.Name}'."); }
            if (inp.Pause) RenameText = null;
            return;
        }

        if (OpenList != null)
        {
            if (inp.Up) OpenCursor = (OpenCursor + OpenList.Count - 1) % OpenList.Count;
            if (inp.Down) OpenCursor = (OpenCursor + 1) % OpenList.Count;
            if (inp.Confirm) OpenSelected();
            else if (inp.Pause) OpenList = null;
            return;
        }

        if (inp.Pause)
        {
            if (!Dirty || _leaveArmed > 0) { _g.GoToTitle(); return; }
            _leaveArmed = 3f;
            Say("Unsaved changes! Press Esc again to leave, or Ctrl+S to save.");
            return;
        }

        // ---- commands
        if (ctrl && k.Pressed(Keys.Letter('Z'))) Undo();
        else if (ctrl && k.Pressed(Keys.Letter('Y'))) Redo();
        else if (ctrl && k.Pressed(Keys.Letter('S'))) Save();
        else if (ctrl && k.Pressed(Keys.Letter('O'))) { ShowOpenList(); return; }
        else if (ctrl && k.Pressed(Keys.Letter('N')))
        {
            // cycle through three sizes on repeated presses
            int[][] sizes = { new[] { 32, 24 }, new[] { 48, 32 }, new[] { 20, 16 } };
            int next = Array.FindIndex(sizes, s => s[0] == Doc.W && s[1] == Doc.H) + 1;
            var sz = sizes[next % sizes.Length];
            PushUndo();
            NewMap(sz[0], sz[1]);
        }
        else if (!ctrl)
        {
            if (k.Pressed(Keys.F1 + 4) || k.Pressed(Keys.Letter('P'))) { if (PlayTest()) return; }
            if (k.Pressed(Keys.Letter('H')) || k.Pressed(Keys.F1)) ShowHelp = !ShowHelp;
            if (k.Pressed(Keys.Letter('F'))) { FillTool = !FillTool; Say(FillTool ? "Fill tool." : "Brush tool."); }
            if (k.Pressed(Keys.Letter('G')))
            {
                Mode = (Layer)(((int)Mode + 1) % 3);
                Say(Mode switch
                {
                    Layer.Ceilings => "Ceiling mode: paint ceiling heights (2-9 pick a height).",
                    Layer.Floors => "Floor mode: paint floor heights (0-9); K toggles the stair brush.",
                    _ => "Tile mode.",
                });
            }
            if (Mode == Layer.Ceilings)
                for (int dgt = 0; dgt <= 9; dgt++)
                    if (k.Pressed(Keys.Digit(dgt)))
                    {
                        HeightIndex = dgt <= 1 ? 0 : dgt - 1;
                        Say($"Height: {HeightLabel(CurrentHeight, Doc.DefaultHeight)}");
                    }
            if (Mode == Layer.Floors)
            {
                for (int dgt = 0; dgt <= 9; dgt++)
                    if (k.Pressed(Keys.Digit(dgt))) { FloorIndex = dgt; Say($"Floor: {FloorLabel(CurrentFloor)}"); }
                if (k.Pressed(Keys.Letter('K')))
                {
                    StairBrush = !StairBrush;
                    Say(StairBrush ? "Stair brush: each cell you drag over is one step higher." : "Stair brush off.");
                }
            }
            if (k.Pressed(Keys.Letter('T')))
            {
                int ti = Array.IndexOf(Maps.ThemeIds, Doc.ThemeId);
                Doc.ThemeId = Maps.ThemeIds[(ti + 1) % Maps.ThemeIds.Length];
                Dirty = true;
                Say($"Theme: {Doc.ThemeId}.");
            }
            if (k.Pressed(Keys.Letter('R'))) { RenameText = ""; return; }
            if (k.Pressed(Keys.Letter('C'))) { PlayClass = (PClass)(((int)PlayClass + 1) % 3); Say($"Play-test as {PlayClass}."); }
            if (k.Pressed(Keys.Letter('V')))
            {
                _g.Style = _g.Relaxed ? GameStyle.Classic : GameStyle.Relaxed;
                Say($"Play-test style: {_g.Style}.");
            }
            if (k.Pressed(Keys.Minus)) ZoomIndex = Math.Max(0, ZoomIndex - 1);
            if (k.Pressed(Keys.Equal)) ZoomIndex = Math.Min(Zooms.Length - 1, ZoomIndex + 1);
            int step = (k.Pressed(Keys.LeftBracket) || k.Pressed(Keys.WheelUp) ? -1 : 0) + (k.Pressed(Keys.RightBracket) || k.Pressed(Keys.WheelDown) ? 1 : 0);
            if (step != 0 && Mode != Layer.Tiles) LayerIndex = (LayerIndex + step + LayerPalette.Length) % LayerPalette.Length;
            else if (step != 0) BrushIndex = (BrushIndex + step + Palette.Length) % Palette.Length;
        }

        // ---- cursor: follows the mouse over the map, or the arrow keys / WASD
        int cs = CellSize;
        bool mouseInMap = inp.MouseX >= 0 && inp.MouseX < MapViewW && inp.MouseY >= 0 && inp.MouseY < MapViewH;
        if (mouseInMap && (inp.MouseX != _lastMouseX || inp.MouseY != _lastMouseY))
        {
            CursorX = Math.Clamp(CamX + (int)(inp.MouseX / cs), 0, Doc.W - 1);
            CursorY = Math.Clamp(CamY + (int)(inp.MouseY / cs), 0, Doc.H - 1);
        }
        _lastMouseX = inp.MouseX; _lastMouseY = inp.MouseY;
        if (!ctrl)
        {
            if (k.Pressed(Keys.Left) || k.Pressed(Keys.Letter('A'))) CursorX = Math.Max(0, CursorX - 1);
            if (k.Pressed(Keys.Right) || k.Pressed(Keys.Letter('D'))) CursorX = Math.Min(Doc.W - 1, CursorX + 1);
            if (k.Pressed(Keys.Up) || k.Pressed(Keys.Letter('W'))) CursorY = Math.Max(0, CursorY - 1);
            if (k.Pressed(Keys.Down) || k.Pressed(Keys.Letter('S'))) CursorY = Math.Min(Doc.H - 1, CursorY + 1);
        }
        KeepCursorVisible();

        // ---- palette clicks
        if (k.Pressed(Keys.Mouse1))
        {
            int pi = PaletteAt(inp.MouseX, inp.MouseY, Mode != Layer.Tiles ? LayerPalette.Length : Palette.Length);
            if (pi >= 0 && Mode != Layer.Tiles) { LayerIndex = pi; Say(LayerLabel(CurrentLayer)); return; }
            if (pi >= 0) { BrushIndex = pi; Say(Words.T(Current.Label)); return; }
        }

        // ---- painting
        bool paint = (mouseInMap && k.Down(Keys.Mouse1)) || k.Pressed(Keys.Space) || k.Pressed(Keys.Enter);
        bool erase = (mouseInMap && k.Down(Keys.Mouse2)) || k.Pressed(Keys.Delete) || k.Pressed(Keys.Backspace);
        bool pick = (mouseInMap && k.Pressed(Keys.Mouse3)) || k.Pressed(Keys.Letter('Q'));
        if (pick && Mode != Layer.Tiles)
        {
            LayerIndex = Math.Max(0, Array.IndexOf(LayerPalette, LayerCells[CursorY * Doc.W + CursorX]));
            Say($"Picked {LayerLabel(CurrentLayer)}.");
        }
        else if (pick)
        {
            int bi = Array.FindIndex(Palette, b => b.Glyph == Doc[CursorX, CursorY]);
            if (bi >= 0) { BrushIndex = bi; Say($"Picked {Current.Label}."); }
        }
        if (paint || erase)
        {
            if (!_stroke) { PushUndo(); _stroke = true; _stairLastCell = -1; }
            if (Mode != Layer.Tiles)
            {
                char hg = erase ? '.' : CurrentLayer;
                int cell = CursorY * Doc.W + CursorX;
                if (Mode == Layer.Floors && StairBrush && !erase && !FillTool)
                {
                    // each new cell of the stroke is one step above the previous one
                    if (cell == _stairLastCell) return;
                    if (_stairLastCell >= 0) hg = _stairLast == '.' ? '1' : _stairLast == '9' ? 'a' : _stairLast == 'z' ? 'z' : (char)(_stairLast + 1);
                    _stairLastCell = cell; _stairLast = hg;
                }
                if (FillTool && !erase)
                {
                    if (k.Pressed(Keys.Mouse1) || k.Pressed(Keys.Space) || k.Pressed(Keys.Enter)) Say($"Set {FillHeight(CursorX, CursorY, hg)} cells.");
                }
                else PaintHeight(CursorX, CursorY, hg);
                return;
            }
            char glyph = erase ? '.' : Current.Glyph;
            if (FillTool && !erase)
            {
                if (k.Pressed(Keys.Mouse1) || k.Pressed(Keys.Space) || k.Pressed(Keys.Enter)) Say($"Filled {Fill(CursorX, CursorY, glyph)} cells.");
            }
            else Paint(CursorX, CursorY, glyph);
        }
        else _stroke = false;
    }

    float _lastMouseX = -1, _lastMouseY = -1;

    sealed class NoKeys : IKeySource
    {
        public static readonly NoKeys Instance = new();
        public bool Down(int code) => false;
        public bool Pressed(int code) => false;
    }
}
