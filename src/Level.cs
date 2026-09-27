namespace HexenSharp;

/// <summary>Visual theme of a map: textures for each wall glyph, flats, sky and fog.</summary>
public sealed class Theme
{
    public Dictionary<char, Tex> Walls = new();
    public Tex FloorIn, CeilIn, FloorOut, Sky;
    public uint FogColor;
    public float FogDist = 14f; // distance at which everything is fully fogged
    public int Light = 256;
    public Tex OutdoorFloor => FloorOut ?? FloorIn;
}

/// <summary>
/// One map of the hub. Levels keep their state (dead monsters, open doors, taken items)
/// when the player leaves, just like Hexen's hub system.
/// </summary>
public sealed class Level
{
    /// <summary>The map's name as written; Name is how it reads in the current style.</summary>
    public readonly string RawName;
    public string Name => Words.T(RawName);
    public readonly int W, H;
    public readonly char[] Cells;      // '\0' for empty, otherwise the wall glyph
    public readonly bool[] Outdoor;
    public readonly char[] Marks;      // floor markers: '1'.. portals, 'E' exit
    public readonly bool[] Seen;       // automap
    public readonly float[] DoorOpen;  // 0 closed .. 1 open
    public readonly sbyte[] DoorMove;  // +1 opening, -1 closing
    public readonly float[] DoorWait;  // seconds until an open door starts closing
    public Theme Theme;
    public string ThemeId = "hall";
    public readonly List<Thing> Things = new();
    public float StartX, StartY, StartAngle;
    public bool BossDead;
    public readonly HashSet<int> PulledLevers = new();
    public int LeverCount;
    public bool LeverPulled => LeverCount > 0 && PulledLevers.Count >= LeverCount;
    public int PlateCount;

    /// <summary>Secret walls ('Z') look like the wall beside them until opened.</summary>
    public readonly Dictionary<int, char> SecretLook = new();
    public readonly HashSet<int> SecretsFound = new();
    public int SecretCount => SecretLook.Count;

    /// <summary>Pressure plates ('^') with a stone block pushed onto them.</summary>
    public int PlatesCovered
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Marks.Length; i++) if (Marks[i] == '^' && Cells[i] == 'X') n++;
            return n;
        }
    }

    /// <summary>Gates open once every lever is pulled and every pressure plate is weighed down.</summary>
    public bool PuzzleSolved => (LeverCount > 0 || PlateCount > 0) && PulledLevers.Count >= LeverCount && PlatesCovered >= PlateCount;
    readonly string _entry;
    public string EntryMessage => Words.T(_entry);
    public ArenaState Arena;   // non-null on wave-survival maps

    public const string DoorGlyphs = "DSFP";
    public const string WallGlyphs = "#BWMIODSFPLXZ";

    /// <summary>Ceiling height of each cell (1 = the original one-storey rooms). Doors are always 1 tall.</summary>
    public readonly float[] Heights;
    /// <summary>Wall glyph used for the band of wall above an opening into a lower cell.</summary>
    public readonly char[] UpperLook;
    public const float MinHeight = 1f, MaxHeight = 10f;

    /// <summary>
    /// Height grid glyphs: '2'..'9' are 1.0..4.5 in half steps, then 'a'..'k' carry on from 5.0 to 10.0;
    /// anything else means the map's default.
    /// </summary>
    public static float HeightFromGlyph(char c, float fallback) =>
        c is >= '1' and <= '9' ? Math.Clamp((c - '0') * 0.5f, MinHeight, MaxHeight)
        : c is >= 'a' and <= 'k' ? 5f + (c - 'a') * 0.5f
        : fallback;
    public static char GlyphFromHeight(float h)
    {
        int n = Math.Clamp((int)MathF.Round(h * 2), 2, (int)(MaxHeight * 2));
        return n <= 9 ? (char)('0' + n) : (char)('a' + n - 10);
    }
    public static bool IsHeightGlyph(char c) => c is >= '2' and <= '9' or >= 'a' and <= 'k';

    /// <summary>Floor height of each cell (0 = ground). Stairs are runs of cells a step higher each.</summary>
    public readonly float[] Floors;
    /// <summary>The tallest step you can walk up without jumping.</summary>
    public const float MaxStep = 0.5f, FloorStep = 0.25f;

    /// <summary>Floor grid glyphs: '1'..'9' are 0.25..2.25, then 'a'..'z' carry on from 2.5 to 8.75; anything else is ground level.</summary>
    public static float FloorFromGlyph(char c) =>
        c is >= '1' and <= '9' ? (c - '0') * FloorStep
        : c is >= 'a' and <= 'z' ? (10 + c - 'a') * FloorStep
        : 0f;
    public static bool IsFloorGlyph(char c) => c is >= '1' and <= '9' or >= 'a' and <= 'z';

    public float FloorAt(float x, float y)
    {
        int cx = (int)MathF.Floor(x), cy = (int)MathF.Floor(y);
        return InBounds(cx, cy) ? Floors[cy * W + cx] : 0f;
    }

    /// <summary>Highest floor under a circle (you stand on the edge of a step, like in Doom).</summary>
    public float FloorUnder(float x, float y, float r)
    {
        float f = 0;
        for (int cy = (int)MathF.Floor(y - r); cy <= (int)MathF.Floor(y + r); cy++)
            for (int cx = (int)MathF.Floor(x - r); cx <= (int)MathF.Floor(x + r); cx++)
                if (InBounds(cx, cy) && !Blocks(cx, cy)) f = MathF.Max(f, Floors[cy * W + cx]);
        return f;
    }

    /// <summary>True if a circle at (x,y) would need to climb more than `reach` above `from` anywhere under it.</summary>
    public bool TooHigh(float x, float y, float r, float from, float reach)
    {
        for (int cy = (int)MathF.Floor(y - r); cy <= (int)MathF.Floor(y + r); cy++)
            for (int cx = (int)MathF.Floor(x - r); cx <= (int)MathF.Floor(x + r); cx++)
                if (InBounds(cx, cy) && Floors[cy * W + cx] > from + reach + 0.001f) return true;
        return false;
    }

    /// <summary>Can you walk from cell a to its neighbour b? (not up a step taller than MaxStep)</summary>
    public bool Walkable(int a, int b) => Floors[b] <= Floors[a] + MaxStep + 0.001f;

    public float HeightAt(float x, float y)
    {
        int cx = (int)MathF.Floor(x), cy = (int)MathF.Floor(y);
        return InBounds(cx, cy) ? Heights[cy * W + cx] : MinHeight;
    }

    public Level(string name, string entry, string[] rows, Theme theme, string[] heightRows = null, float defaultHeight = 1f, string[] floorRows = null)
    {
        RawName = name;
        _entry = entry;
        Theme = theme;
        H = rows.Length;
        W = rows[0].Length;
        for (int y = 0; y < H; y++)
            if (rows[y].Length != W) throw new InvalidDataException($"{name}: row {y} is {rows[y].Length} wide, expected {W}");

        Cells = new char[W * H];
        Outdoor = new bool[W * H];
        Marks = new char[W * H];
        Heights = new float[W * H];
        Floors = new float[W * H];
        UpperLook = new char[W * H];
        Seen = new bool[W * H];
        DoorOpen = new float[W * H];
        DoorMove = new sbyte[W * H];
        DoorWait = new float[W * H];

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                char ch = rows[y][x];
                int i = y * W + x;
                if (WallGlyphs.IndexOf(ch) >= 0) { Cells[i] = ch; if (ch == 'L') LeverCount++; continue; }
                Outdoor[i] = ch == ',';
                if (ch == '.' || ch == ',') continue;
                if (char.IsDigit(ch) || ch == 'E' || ch == '*' || ch == '!' || ch == '^') { Marks[i] = ch; if (ch == '^') PlateCount++; continue; }
                if (ch == '@') { StartX = x + 0.5f; StartY = y + 0.5f; continue; }
                var t = ThingFactory.Create(ch, x + 0.5f, y + 0.5f);
                if (t == null) throw new InvalidDataException($"{name}: unknown map glyph '{ch}' at {x},{y}");
                t.Level = this;
                Things.Add(t);
            }

        if (Array.IndexOf(Marks, '*') >= 0) Arena = new ArenaState(this);
        // a map without a Heresiarch (e.g. a custom map) has its exit open from the start
        BossDead = !Things.Any(t => t is Monster { Def.Boss: true });

        // ceiling heights: doors stay one storey so they read as doors, with wall above them
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                char hg = heightRows != null && y < heightRows.Length && x < heightRows[y].Length ? heightRows[y][x] : '.';
                char fg = floorRows != null && y < floorRows.Length && x < floorRows[y].Length ? floorRows[y][x] : '.';
                Floors[i] = FloorFromGlyph(fg);
                // ceilings are absolute heights; a raised floor keeps at least one storey of headroom
                Heights[i] = IsDoor(Cells[i]) ? Floors[i] + MinHeight
                    : MathF.Max(HeightFromGlyph(hg, Math.Clamp(defaultHeight, MinHeight, MaxHeight)), Floors[i] + MinHeight);
                var look = new[] { Cell(x + 1, y), Cell(x - 1, y), Cell(x, y + 1), Cell(x, y - 1) }
                    .Where(c => c != '\0' && !IsDoor(c) && c != 'L' && c != 'X')
                    .GroupBy(c => c).OrderByDescending(gr => gr.Count()).Select(gr => gr.Key).FirstOrDefault();
                UpperLook[i] = look == '\0' ? '#' : look;
            }

        foreach (var d in Things.OfType<Decor>())
            if (d.ReachCeiling) d.SpriteH = HeightAt(d.X, d.Y);

        // disguise each secret wall as its most common neighbouring wall
        for (int i = 0; i < Cells.Length; i++)
        {
            if (Cells[i] != 'Z') continue;
            int x = i % W, y = i / W;
            var look = new[] { Cell(x + 1, y), Cell(x - 1, y), Cell(x, y + 1), Cell(x, y - 1) }
                .Where(c => c != '\0' && !IsDoor(c) && c != 'L' && c != 'X')
                .GroupBy(c => c).OrderByDescending(gr => gr.Count()).Select(gr => gr.Key).FirstOrDefault();
            SecretLook[i] = look == '\0' ? '#' : look;
        }

        // lore stones get their text in reading order
        {
            int k = 0;
            foreach (var stone in Things.OfType<LoreStone>()) { stone.Map = RawName; stone.Index = k++; }
        }

        // Cells holding things or markers inherit "outdoor" from their neighbours.
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
            {
                int i = y * W + x;
                char ch = rows[y][x];
                if (ch == '.' || ch == ',' || Cells[i] != '\0') continue;
                int n = 0;
                if (rows[y - 1][x] == ',') n++;
                if (rows[y + 1][x] == ',') n++;
                if (rows[y][x - 1] == ',') n++;
                if (rows[y][x + 1] == ',') n++;
                Outdoor[i] = n >= 2;
            }
    }

    public bool InBounds(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H;
    public char Cell(int x, int y) => InBounds(x, y) ? Cells[y * W + x] : '#';
    public static bool IsDoor(char c) => c == 'D' || c == 'S' || c == 'F' || c == 'P' || c == 'Z';

    /// <summary>True when a cell blocks movement (walls, and doors that are not fully open).</summary>
    public bool Blocks(int x, int y)
    {
        if (!InBounds(x, y)) return true;
        int i = y * W + x;
        char c = Cells[i];
        if (c == '\0') return false;
        if (IsDoor(c)) return DoorOpen[i] < 0.9f;
        return true;
    }

    public bool BlocksPoint(float x, float y) => Blocks((int)MathF.Floor(x), (int)MathF.Floor(y));

    /// <summary>Circle vs grid collision.</summary>
    public bool BlocksCircle(float x, float y, float r)
    {
        int x0 = (int)MathF.Floor(x - r), x1 = (int)MathF.Floor(x + r);
        int y0 = (int)MathF.Floor(y - r), y1 = (int)MathF.Floor(y + r);
        for (int cy = y0; cy <= y1; cy++)
            for (int cx = x0; cx <= x1; cx++)
                if (Blocks(cx, cy)) return true;
        return false;
    }

    public char MarkAt(float x, float y)
    {
        int cx = (int)x, cy = (int)y;
        return InBounds(cx, cy) ? Marks[cy * W + cx] : '\0';
    }

    /// <summary>Where the player first appears: the start spot, else the first portal.</summary>
    public (int x, int y) ArrivalCell()
    {
        if (StartX > 0) return ((int)StartX, (int)StartY);
        int i = Array.FindIndex(Marks, char.IsDigit);
        return i >= 0 ? (i % W, i / W) : (1, 1);
    }

    /// <summary>How far up a move between neighbouring cells may climb: walking up steps, jumping, or flying with the jetpack.</summary>
    public enum Move { Walk, Jump, Fly }

    /// <summary>Tallest rise between neighbouring cells for each way of moving (a jump adds a bit under its apex).</summary>
    public static float Rise(Move m) => m switch { Move.Walk => MaxStep, Move.Jump => MaxStep + 0.4f, _ => float.MaxValue };

    bool CanMove(int a, int b, Move m) => Floors[b] <= Floors[a] + Rise(m) + 0.001f;

    /// <summary>Walking (or jumping, or flying) distance in cells from a start cell to every cell; -1 where unreachable.</summary>
    public int[] Distances(int sx, int sy, Move move = Move.Walk)
    {
        var d = new int[W * H];
        Array.Fill(d, -1);
        var q = new Queue<int>();
        d[sy * W + sx] = 0;
        q.Enqueue(sy * W + sx);
        while (q.Count > 0)
        {
            int c = q.Dequeue(), x = c % W, y = c / W;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (!InBounds(nx, ny)) continue;
                int i = ny * W + nx;
                if (d[i] >= 0) continue;
                char ch = Cells[i];
                if (ch != '\0' && !IsDoor(ch)) continue;
                if (!CanMove(c, i, move)) continue;
                d[i] = d[c] + 1;
                q.Enqueue(i);
            }
        }
        return d;
    }

    int[] _walkable;
    /// <summary>Open floor cells you can get to from the arrival point, by jetpack if need be (cached; used for the explored percentage).</summary>
    public int[] WalkableFloor
    {
        get
        {
            if (_walkable != null) return _walkable;
            var (sx, sy) = ArrivalCell();
            var r = Reachable(sx, sy, move: Move.Fly);
            return _walkable = Enumerable.Range(0, r.Length).Where(i => r[i] && (Cells[i] == '\0' || Cells[i] == 'X')).ToArray();
        }
    }

    /// <summary>Flood fill of cells reachable on foot (or by jumping or flying), treating every door and gate as passable.</summary>
    public bool[] Reachable(int sx, int sy, HashSet<int> blocked = null, Move move = Move.Walk)
    {
        var seen = new bool[W * H];
        var q = new Queue<int>();
        seen[sy * W + sx] = true;
        q.Enqueue(sy * W + sx);
        while (q.Count > 0)
        {
            int c = q.Dequeue(), x = c % W, y = c / W;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (!InBounds(nx, ny)) continue;
                int i = ny * W + nx;
                if (seen[i] || (blocked != null && blocked.Contains(i))) continue;
                char ch = Cells[i];
                if (ch != '\0' && !IsDoor(ch)) continue;
                if (!CanMove(c, i, move)) continue;
                seen[i] = true;
                q.Enqueue(i);
            }
        }
        return seen;
    }

    public (float x, float y)? FindMark(char m)
    {
        for (int i = 0; i < Marks.Length; i++)
            if (Marks[i] == m) return (i % W + 0.5f, i / W + 0.5f);
        return null;
    }

    /// <summary>Can a pushed block move into this cell? Empty floor (or a plate) with nothing standing on it.</summary>
    public bool BlockCanEnter(int x, int y, int fromX = -1, int fromY = -1)
    {
        if (!InBounds(x, y)) return false;
        int i = y * W + x;
        if (Cells[i] != '\0' || (Marks[i] != '\0' && Marks[i] != '^')) return false;
        // blocks slide on level ground only
        if (InBounds(fromX, fromY) && MathF.Abs(Floors[i] - Floors[fromY * W + fromX]) > 0.01f) return false;
        foreach (var t in Things)
        {
            if (t.Removed || t is Projectile or Puff) continue;
            if (t is Monster m && !m.Alive) continue;
            if ((int)t.X == x && (int)t.Y == y) return false;
        }
        return true;
    }

    public void OpenDoor(int x, int y)
    {
        int i = y * W + x;
        DoorMove[i] = 1;
        DoorWait[i] = 4f;
    }

    /// <summary>Line of sight test by marching through the grid.</summary>
    public bool Sight(float x0, float y0, float x1, float y1)
    {
        float dx = x1 - x0, dy = y1 - y0;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        int steps = (int)(dist * 4) + 1;
        for (int s = 1; s < steps; s++)
        {
            float t = s / (float)steps;
            int cx = (int)(x0 + dx * t), cy = (int)(y0 + dy * t);
            char c = Cell(cx, cy);
            if (c == '\0') continue;
            if (c == 'P' || (IsDoor(c) && DoorOpen[cy * W + cx] > 0.6f)) continue; // see through grates and open doors
            return false;
        }
        return true;
    }

    public void UpdateDoors(float dt, Func<int, int, bool> occupied, Action<float, float> onClose)
    {
        for (int i = 0; i < Cells.Length; i++)
        {
            char c = Cells[i];
            if (!IsDoor(c)) continue;
            if (DoorMove[i] > 0)
            {
                DoorOpen[i] = MathF.Min(1f, DoorOpen[i] + dt * 1.5f);
                if (DoorOpen[i] >= 1f) DoorMove[i] = 0;
            }
            else if (DoorMove[i] < 0)
            {
                int x = i % W, y = i / W;
                if (occupied(x, y)) { DoorMove[i] = 1; continue; }
                DoorOpen[i] = MathF.Max(0f, DoorOpen[i] - dt * 1.5f);
                if (DoorOpen[i] <= 0f) DoorMove[i] = 0;
            }
            else if (DoorOpen[i] >= 1f && c == 'D')
            {
                // plain doors swing shut again after a while; key doors and gates stay open
                DoorWait[i] -= dt;
                if (DoorWait[i] <= 0)
                {
                    int x = i % W, y = i / W;
                    if (!occupied(x, y)) { DoorMove[i] = -1; onClose(x + 0.5f, y + 0.5f); }
                    else DoorWait[i] = 1f;
                }
            }
        }
    }
}

/// <summary>The hub's maps. Legend: see README.</summary>
/// <summary>A map's source: its name, arrival message, theme and ASCII rows. The level editor reads and writes these.</summary>
public sealed record MapDef(string Name, string Entry, string ThemeId, string[] Rows, string[] Heights = null, float Height = 1f, string[] Floors = null)
{
    public Level Build() => new(Name, Entry, Rows, Maps.ThemeById(ThemeId), Heights, Height, Floors) { ThemeId = ThemeId };
}

/// <summary>The hub's maps and the visual themes they (and custom maps) can use. Legend: see README.</summary>
public static class Maps
{
    public static readonly string[] ThemeIds = { "hall", "ice", "crypt", "arena", "spire" };

    public static Theme ThemeById(string id)
    {
        var t = FantasyTheme(id);
        if (Art.Style == ArtStyle.SciFi)
        {
            // same texture slots (already swapped by the sci-fi art), station-appropriate fog and light
            (t.FogColor, t.FogDist, t.Light) = id switch
            {
                "ice" => (Col.Rgb(150, 196, 210), 12f, 250),
                "crypt" => (Col.Rgb(6, 26, 18), 12f, 236),
                "arena" => (Col.Rgb(30, 6, 12), 18f, 256),
                "spire" => (Col.Rgb(8, 10, 26), 24f, 256),
                _ => (Col.Rgb(4, 8, 16), 15f, 256),
            };
        }
        return t;
    }

    static Theme FantasyTheme(string id)
    {
        switch (id)
        {
            case "hall":
            {
                var t = new Theme
                {
                    FloorIn = Art.FloorStone, CeilIn = Art.CeilWood, FloorOut = Art.Grass, Sky = Art.SkyDusk,
                    FogColor = Col.Rgb(6, 4, 8), FogDist = 15f, Light = 256,
                };
                t.Walls['#'] = Art.Stone; t.Walls['B'] = Art.Brick; t.Walls['W'] = Art.Wood; t.Walls['M'] = Art.Moss;
                t.Walls['O'] = Art.Marble; t.Walls['I'] = Art.Ice;
                return t;
            }
            case "ice":
            {
                var t = new Theme
                {
                    FloorIn = Art.FloorWood, CeilIn = Art.CeilStone, FloorOut = Art.Snow, Sky = Art.SkyIce,
                    FogColor = Col.Rgb(150, 164, 184), FogDist = 11f, Light = 240,
                };
                t.Walls['#'] = Art.Stone; t.Walls['I'] = Art.Ice; t.Walls['W'] = Art.Wood; t.Walls['M'] = Art.Moss;
                t.Walls['O'] = Art.Marble; t.Walls['B'] = Art.Brick;
                return t;
            }
            case "crypt":
            {
                var t = new Theme
                {
                    FloorIn = Art.FloorStone, CeilIn = Art.CeilStone, FloorOut = Art.Grass, Sky = Art.SkyNight,
                    FogColor = Col.Rgb(8, 20, 12), FogDist = 12f, Light = 230,
                };
                t.Walls['#'] = Art.Stone; t.Walls['M'] = Art.Moss; t.Walls['B'] = Art.Brick; t.Walls['W'] = Art.Wood;
                t.Walls['O'] = Art.Marble; t.Walls['I'] = Art.Ice;
                return t;
            }
            case "spire":
            {
                // an open-topped tower: long sight lines so you can see the ledges far above
                var t = new Theme
                {
                    FloorIn = Art.FloorStone, CeilIn = Art.CeilStone, FloorOut = Art.FloorStone, Sky = Art.SkyDusk,
                    FogColor = Col.Rgb(26, 16, 34), FogDist = 24f, Light = 256,
                };
                t.Walls['#'] = Art.Stone; t.Walls['O'] = Art.Marble; t.Walls['B'] = Art.Brick; t.Walls['M'] = Art.Moss;
                t.Walls['W'] = Art.Wood; t.Walls['I'] = Art.Ice;
                return t;
            }
            case "arena":
            {
                var t = new Theme
                {
                    FloorIn = Art.FloorStone, CeilIn = Art.CeilStone, FloorOut = Art.FloorStone, Sky = Art.SkyDusk,
                    FogColor = Col.Rgb(30, 10, 24), FogDist = 18f, Light = 250,
                };
                t.Walls['#'] = Art.Stone; t.Walls['O'] = Art.Marble; t.Walls['B'] = Art.Brick; t.Walls['M'] = Art.Moss;
                t.Walls['W'] = Art.Wood; t.Walls['I'] = Art.Ice;
                return t;
            }
            default:
                return FantasyTheme("hall");
        }
    }

    public static readonly MapDef[] Hub =
    {
        // Winnowing Hall: the hub's start. The lever in the great hall raises the gate to the courtyard,
        // whose portal leads to the Frozen Keep. The steel door guards the Heresiarch.
        Elevate(Raise(new("Winnowing Hall", "Winnowing Hall", "hall", new[]
        {
            "################################",
            "#....&.#............&.#........#",
            "#.@....#..p.......p...#..b..e..#",
            "#...J..D..............D........#",
            "#..h...#......e.......#....w...#",
            "#......#..p.......p...#..b.....#",
            "###D####..............##########",
            "#......#......t.......#........#",
            "#..e...#..............D...r....#",
            "#......D..p.......p...#..h..e..#",
            "#..q...#.............e#........#",
            "########WWWLWWWWWPWWWW##########",
            "#################.#####.%.&.####",
            "#################.#######Z######",
            "#OOOOOOO#,,,,,,,,,,,,,,,,,,,,,,#",
            "#O.....O#,,c,,,,,,,,,,,,,,c,,&,#",
            "#O.....O#,,,,T,,,,,,,,T,,,,,,,,#",
            "#O.t.t.O#,,,,,,,,,,,,,,,,,,3,,,#",
            "#OE.H...S,,,,,,,,,,1,,,,,,,,,,,#",
            "#O.t.t.O#,,,,,,,,,,,,,,,,,,,,,,#",
            "#O.....O#,,,,T,,,,,,,,T,,,,g,,,#",
            "#O..u..O#,,e,,,,,,,,,,,,,,,e,,,#",
            "#OOOOOOO#,,,,,,b,,,,,,,h,,,,,,,#",
            "################################",
        }),
            (1, 1, 6, 5, '3'), (8, 1, 21, 10, '6'), (23, 1, 30, 5, '3'), (1, 7, 6, 10, '3'), (23, 7, 30, 10, '3'), (9, 14, 30, 22, '5'), (1, 14, 7, 22, '7')),
            (10, 1, 20, 2, '3'), (10, 3, 20, 3, '2'), (10, 4, 20, 4, '1'), (3, 16, 5, 20, '1'), (4, 17, 4, 19, '2'), (4, 18, 4, 18, '3')),
        // Frozen Keep: fog-bound ice fortress. Portal 2 leads to Darkmere Crypt; its Fire Key opens the fire door
        // to the east room, whose lever raises the gate to the vault. Portal 4 in the vault leads to the Windspire,
        // where the Steel Key waits.
        Elevate(Raise(new("Frozen Keep", "The Frozen Keep", "ice", new[]
        {
            "IIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII",
            "I,,,,,,,,,,,,&,I,,,,,,,,,,,,,,,I",
            "I,,1,,,,,,,,,,,D,,,,,a,,,,g,,,,I",
            "I,,,,,,,,,,,,,,I,,,,,,,,d,,,,,,I",
            "I,,,,T,,,,T,,,,I,,,,C,,,,,,b,,,I",
            "I,,,,,,,,,,,,,,I,,,,,,,,,,,,,,,I",
            "IIIIIIIDIIIIIIIIIIIIIIIIIFIIIIII",
            "I...........I......I..........&I",
            "I..a....c...D......I...c....h..I",
            "I.........2.I..x...I...........L",
            "I....q......I......I...a.......I",
            "IIIIZIIIIIIIIIIPIIIIIIIIIIIIIIII",
            "II.....III...........&IIIIIIIIII",
            "II.....III.C...4...C..IIIIIIIIII",
            "II%...&III............IIIIIIIIII",
            "IIIIIIIIII..u....g....IIIIIIIIII",
            "IIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII",
        }),
            (1, 1, 14, 5, '4'), (16, 1, 30, 5, '4'), (1, 7, 11, 10, '3'), (13, 7, 18, 10, '3'), (20, 7, 30, 10, '3'), (10, 12, 21, 15, '5')),
            (23, 1, 23, 4, '1'), (24, 1, 24, 4, '2'), (25, 1, 30, 4, '3')),
        // Darkmere Crypt: a swampy crypt haunted by Dark Bishops. The gate to the Fire Key (which opens the
        // fire door in the Frozen Keep) needs both levers pulled AND both pressure plates in the south-east
        // room weighed down with the pushable stone blocks.
        Raise(new("Darkmere Crypt", "Darkmere Crypt", "crypt", new[]
        {
            "MMMMMMMMMMMMMMMMMMMMMMMMMMMM",
            "M2.....M,,,,,,,,,,,&M......M",
            "M......D,,,e,,,,T,,,D..b...M",
            "M..h...M,,,,,,,,,,,,M...d..M",
            "M.....&M,,T,,,,,,,,,M......L",
            "MMMDMMMM,,,,,,d,,,,,MMMMMMMM",
            "M......M,,,,,,,,,,,,M......M",
            "M..e...M,,,,,,,,T,,,M^....^M",
            "M......MMMMMMDMMMMMMM......M",
            "M..g...M....p..p....M..XX..M",
            "M......D............D......M",
            "ML.....M..e......e..M.....rM",
            "MMMMZMMM....t..t...&MMMMMMMM",
            "BB.....BMMMMMPMMMMMMBBBBBBBB",
            "BB.....BBC........CBBBBBBBBB",
            "BB%...&BB....f.....BBBBBBBBB",
            "BBBBBBBBB..u....g..BBBBBBBBB",
            "BBBBBBBBBBBBBBBBBBBBBBBBBBBB",
        }),
            (1, 1, 6, 4, '3'), (8, 1, 19, 7, '4'), (21, 1, 26, 4, '3'), (1, 6, 6, 11, '3'), (21, 6, 26, 11, '2'), (8, 9, 19, 12, '5'), (9, 14, 18, 16, '4')),
        // Chaos Arena: optional wave survival. Step on the golden altar to start; monsters pour out of the
        // purple spawn runes in ever harder waves. Portal 3 in Winnowing Hall's courtyard leads here.
        Raise(new("Chaos Arena", "The Chaos Arena - step on the altar to begin", "arena", new[]
        {
            "OOOOOOOOOOOOOOOOOOOOOOOOOO",
            "O.....O,*,,,,,,,,,,,,,,*,O",
            "O.3...O,,,,,,,,,,,,,,,,,,O",
            "O.....O,,,p,,,,,,,,,,p,,,O",
            "O..h..O,,,,,,,,,,,,,,,,,,O",
            "O.....D,,,,,,,,,,,,,,,,,,O",
            "O..b..O,*,,,,,,,,,,,,,,*,O",
            "O....&O,,,,,,,,,,,,,,,,,&O",
            "OOOZOOO,,,,,,,,!,,,,,,,,,O",
            "OO...OO,,,,,,,,,,,,,,,,,,O",
            "OO...OO,*,,,,,,,,,,,,,,*,O",
            "OO%.&OO,,,p,,,,,,,,,,p,,,O",
            "OOOOOOO,,,,,,,,,,,,,,,,,,O",
            "OOOOOOO,*,,,,,,,*,,,,,,*,O",
            "OOOOOOOOOOOOOOOOOOOOOOOOOO",
        }),
            (1, 1, 5, 7, '3'), (7, 1, 24, 13, '7')),
        // Windspire: an open-topped tower whose ledges climb far beyond any jump. Portal 4 in the Frozen Keep's vault
        // leads here. Fly ledge to ledge with the jetpack (a spare waits by the portal) up to the beacon at the top;
        // its lever opens the vault at the foot of the tower, which holds the Steel Key. A secret wall off the north-east
        // ledge hides a nook that only a flyer can reach.
        Elevate(Raise(new("Windspire", "The Windspire. Only the winged may reach the beacon at its crown.", "spire", new[]
        {
            "OOOOOOOOOOOOOOOOOOOOOOOOO",
            "O&,,,,,,,,,,,,,,,,,,,O&.O",
            "O,,q,,,,,,,,,,,,,,b,,Z.%O",
            "O,,,,,,,,,,,,,,,,,,,,O..O",
            "O,,,,,,,,,,,,d,,,,,,,OOOO",
            "O,,,,,,,a,,,,,,,,,,,,OOOO",
            "O,,,,,,,,,,,,,,,,,,,,OOOO",
            "O,,,,,,,,,,,,,,,,,,,,OOOO",
            "Og,,,,,,,,L,,,,,,,,,,OOOO",
            "O,,,,,,,,,,,,,a,,,,,,OOOO",
            "O,,,,,,,,,,,&,,,,,,,,OOOO",
            "O,,,,,a,,,,,,,,,,,,,,OOOO",
            "O,,,,,,,,,,,,,,,,,,h,OOOO",
            "O,,r,,,,,,,,,,,,,,,,,OOOO",
            "O&,,,,,,,,,,,,,,,,,,,OOOO",
            "OOOOOOOOO..OOOOOOOOOOOOOO",
            "O.....&.......O......OOOO",
            "O.............O.u..r.OOOO",
            "O..4..........P...k..OOOO",
            "O......J......O.g..b.OOOO",
            "O.............O......OOOO",
            "OOOOOOOOOOOOOOOOOOOOOOOOO",
        }),
            (1, 1, 20, 14, 'k'), (9, 15, 10, 15, '4'), (1, 16, 13, 20, '4'), (15, 16, 20, 20, '3'), (22, 1, 23, 3, 'a')),
            (16, 11, 20, 14, '6'), (19, 7, 20, 8, 'a'), (15, 1, 20, 3, 'e'), (10, 1, 11, 2, 'i'), (1, 1, 5, 3, 'm'),
            (1, 7, 2, 8, 'q'), (1, 12, 5, 14, 'u'), (9, 7, 12, 10, 'y'), (21, 2, 23, 3, 'e'), (22, 1, 23, 1, 'e')),
    };

    public static Level[] BuildHub() => Hub.Select(d => d.Build()).ToArray();

    /// <summary>Adds a floor grid to a map: rectangles (inclusive) of floor-height glyphs over ground level.</summary>
    static MapDef Elevate(MapDef d, params (int x0, int y0, int x1, int y1, char f)[] regions)
    {
        var grid = d.Rows.Select(r => Enumerable.Repeat('.', r.Length).ToArray()).ToArray();
        foreach (var (x0, y0, x1, y1, f) in regions)
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    grid[y][x] = f;
        return d with { Floors = grid.Select(r => new string(r)).ToArray() };
    }

    /// <summary>Adds a height grid to a map: rectangles (inclusive) of ceiling-height glyphs over a default of 1.</summary>
    static MapDef Raise(MapDef d, params (int x0, int y0, int x1, int y1, char h)[] regions)
    {
        var grid = d.Rows.Select(r => Enumerable.Repeat('2', r.Length).ToArray()).ToArray();
        foreach (var (x0, y0, x1, y1, h) in regions)
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    grid[y][x] = h;
        return d with { Heights = grid.Select(r => new string(r)).ToArray() };
    }
}
