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
    public readonly string Name;
    public readonly int W, H;
    public readonly char[] Cells;      // '\0' for empty, otherwise the wall glyph
    public readonly bool[] Outdoor;
    public readonly char[] Marks;      // floor markers: '1'.. portals, 'E' exit
    public readonly bool[] Seen;       // automap
    public readonly float[] DoorOpen;  // 0 closed .. 1 open
    public readonly sbyte[] DoorMove;  // +1 opening, -1 closing
    public readonly float[] DoorWait;  // seconds until an open door starts closing
    public readonly Theme Theme;
    public readonly List<Thing> Things = new();
    public float StartX, StartY, StartAngle;
    public bool BossDead;
    public readonly HashSet<int> PulledLevers = new();
    public int LeverCount;
    /// <summary>Gates open once every lever in the map has been pulled.</summary>
    public bool LeverPulled => LeverCount > 0 && PulledLevers.Count >= LeverCount;
    public readonly string EntryMessage;
    public ArenaState Arena;   // non-null on wave-survival maps

    public const string DoorGlyphs = "DSFP";
    public const string WallGlyphs = "#BWMIODSFPL";

    public Level(string name, string entry, string[] rows, Theme theme)
    {
        Name = name;
        EntryMessage = entry;
        Theme = theme;
        H = rows.Length;
        W = rows[0].Length;
        for (int y = 0; y < H; y++)
            if (rows[y].Length != W) throw new InvalidDataException($"{name}: row {y} is {rows[y].Length} wide, expected {W}");

        Cells = new char[W * H];
        Outdoor = new bool[W * H];
        Marks = new char[W * H];
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
                if (char.IsDigit(ch) || ch == 'E' || ch == '*' || ch == '!') { Marks[i] = ch; continue; }
                if (ch == '@') { StartX = x + 0.5f; StartY = y + 0.5f; continue; }
                var t = ThingFactory.Create(ch, x + 0.5f, y + 0.5f);
                if (t == null) throw new InvalidDataException($"{name}: unknown map glyph '{ch}' at {x},{y}");
                t.Level = this;
                Things.Add(t);
            }

        if (Array.IndexOf(Marks, '*') >= 0) Arena = new ArenaState(this);

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
    public static bool IsDoor(char c) => c == 'D' || c == 'S' || c == 'F' || c == 'P';

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

    public (float x, float y)? FindMark(char m)
    {
        for (int i = 0; i < Marks.Length; i++)
            if (Marks[i] == m) return (i % W + 0.5f, i / W + 0.5f);
        return null;
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
public static class Maps
{
    public static Level[] BuildHub()
    {
        var hall = new Theme
        {
            FloorIn = Art.FloorStone, CeilIn = Art.CeilWood, FloorOut = Art.Grass, Sky = Art.SkyDusk,
            FogColor = Col.Rgb(6, 4, 8), FogDist = 15f, Light = 256,
        };
        hall.Walls['#'] = Art.Stone; hall.Walls['B'] = Art.Brick; hall.Walls['W'] = Art.Wood; hall.Walls['M'] = Art.Moss;
        hall.Walls['O'] = Art.Marble; hall.Walls['I'] = Art.Ice;

        var ice = new Theme
        {
            FloorIn = Art.FloorWood, CeilIn = Art.CeilStone, FloorOut = Art.Snow, Sky = Art.SkyIce,
            FogColor = Col.Rgb(150, 164, 184), FogDist = 11f, Light = 240,
        };
        ice.Walls['#'] = Art.Stone; ice.Walls['I'] = Art.Ice; ice.Walls['W'] = Art.Wood; ice.Walls['M'] = Art.Moss;
        ice.Walls['O'] = Art.Marble; ice.Walls['B'] = Art.Brick;

        // Winnowing Hall: the hub's start. The lever in the great hall raises the gate to the courtyard,
        // whose portal leads to the Frozen Keep. The steel door guards the Heresiarch.
        var winnowing = new Level("Winnowing Hall", "Winnowing Hall", new[]
        {
            "################################",
            "#......#..............#........#",
            "#.@....#..p.......p...#..b..e..#",
            "#......D..............D........#",
            "#..h...#......e.......#....w...#",
            "#......#..p.......p...#..b.....#",
            "###D####..............##########",
            "#......#......t.......#........#",
            "#..e...#..............D...r....#",
            "#......D..p.......p...#..h..e..#",
            "#..q...#.............e#........#",
            "########WWWLWWWWWPWWWW##########",
            "#################.##############",
            "#################.##############",
            "#OOOOOOO#,,,,,,,,,,,,,,,,,,,,,,#",
            "#O.....O#,,c,,,,,,,,,,,,,,c,,,,#",
            "#O.....O#,,,,T,,,,,,,,T,,,,,,,,#",
            "#O.t.t.O#,,,,,,,,,,,,,,,,,,3,,,#",
            "#OE.H...S,,,,,,,,,,1,,,,,,,,,,,#",
            "#O.t.t.O#,,,,,,,,,,,,,,,,,,,,,,#",
            "#O.....O#,,,,T,,,,,,,,T,,,,g,,,#",
            "#O..u..O#,,e,,,,,,,,,,,,,,,e,,,#",
            "#OOOOOOO#,,,,,,b,,,,,,,h,,,,,,,#",
            "################################",
        }, hall);
        winnowing.StartAngle = 0f;

        // Frozen Keep: fog-bound ice fortress. Portal 2 leads to Darkmere Crypt; its Fire Key opens the fire door
        // to the east room, whose lever raises the gate to the steel key vault.
        var keep = new Level("Frozen Keep", "The Frozen Keep", new[]
        {
            "IIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII",
            "I,,,,,,,,,,,,,,I,,,,,,,,,,,,,,,I",
            "I,,1,,,,,,,,,,,D,,,,,a,,,,g,,,,I",
            "I,,,,,,,,,,,,,,I,,,,,,,,,,,,,,,I",
            "I,,,,T,,,,T,,,,I,,,,C,,,,,,b,,,I",
            "I,,,,,,,,,,,,,,I,,,,,,,,,,,,,,,I",
            "IIIIIIIDIIIIIIIIIIIIIIIIIFIIIIII",
            "I...........I......I...........I",
            "I..a....c...D......I...c....h..I",
            "I.........2.I..x...I...........L",
            "I....q......I......I...a.......I",
            "IIIIIIIIIIIIIIIPIIIIIIIIIIIIIIII",
            "IIIIIIIIII............IIIIIIIIII",
            "IIIIIIIIII.C...k...C..IIIIIIIIII",
            "IIIIIIIIII............IIIIIIIIII",
            "IIIIIIIIII..u....g....IIIIIIIIII",
            "IIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII",
        }, ice);

        var crypt = new Theme
        {
            FloorIn = Art.FloorStone, CeilIn = Art.CeilStone, FloorOut = Art.Grass, Sky = Art.SkyNight,
            FogColor = Col.Rgb(8, 20, 12), FogDist = 12f, Light = 230,
        };
        crypt.Walls['#'] = Art.Stone; crypt.Walls['M'] = Art.Moss; crypt.Walls['B'] = Art.Brick; crypt.Walls['W'] = Art.Wood;
        crypt.Walls['O'] = Art.Marble; crypt.Walls['I'] = Art.Ice;

        // Darkmere Crypt: a swampy crypt. Two levers must both be pulled to raise the gate to the Fire Key,
        // which opens the fire door in the Frozen Keep.
        var darkmere = new Level("Darkmere Crypt", "Darkmere Crypt", new[]
        {
            "MMMMMMMMMMMMMMMMMMMMMMMMMMMM",
            "M2.....M,,,,,,,,,,,,M......M",
            "M......D,,,e,,,,T,,,D..b...M",
            "M..h...M,,,,,,,,,,,,M...a..M",
            "M......M,,T,,,,,,,,,M......L",
            "MMMDMMMM,,,,,,a,,,,,MMMMMMMM",
            "M......M,,,,,,,,,,,,M......M",
            "M..e...M,,,,,,,,T,,,M..c...M",
            "M......MMMMMMDMMMMMMM......M",
            "M..g...M....p..p....M..q...M",
            "M......D............D......M",
            "ML.....M..e......e..M.....rM",
            "MMMMMMMM....t..t....MMMMMMMM",
            "BBBBBBBBMMMMMPMMMMMMBBBBBBBB",
            "BBBBBBBBBC........CBBBBBBBBB",
            "BBBBBBBBB....f.....BBBBBBBBB",
            "BBBBBBBBB..u....g..BBBBBBBBB",
            "BBBBBBBBBBBBBBBBBBBBBBBBBBBB",
        }, crypt);

        var chaos = new Theme
        {
            FloorIn = Art.FloorStone, CeilIn = Art.CeilStone, FloorOut = Art.FloorStone, Sky = Art.SkyDusk,
            FogColor = Col.Rgb(30, 10, 24), FogDist = 18f, Light = 250,
        };
        chaos.Walls['#'] = Art.Stone; chaos.Walls['O'] = Art.Marble; chaos.Walls['B'] = Art.Brick; chaos.Walls['M'] = Art.Moss;
        chaos.Walls['W'] = Art.Wood; chaos.Walls['I'] = Art.Ice;

        // Chaos Arena: optional wave survival. Step on the golden altar to start; monsters pour out of the
        // purple spawn runes in ever harder waves. Portal 3 in Winnowing Hall's courtyard leads here.
        var arena = new Level("Chaos Arena", "The Chaos Arena - step on the altar to begin", new[]
        {
            "OOOOOOOOOOOOOOOOOOOOOOOOOO",
            "O.....O,*,,,,,,,,,,,,,,*,O",
            "O.3...O,,,,,,,,,,,,,,,,,,O",
            "O.....O,,,p,,,,,,,,,,p,,,O",
            "O..h..O,,,,,,,,,,,,,,,,,,O",
            "O.....D,,,,,,,,,,,,,,,,,,O",
            "O..b..O,*,,,,,,,,,,,,,,*,O",
            "O.....O,,,,,,,,,,,,,,,,,,O",
            "OOOOOOO,,,,,,,,!,,,,,,,,,O",
            "OOOOOOO,,,,,,,,,,,,,,,,,,O",
            "OOOOOOO,*,,,,,,,,,,,,,,*,O",
            "OOOOOOO,,,p,,,,,,,,,,p,,,O",
            "OOOOOOO,,,,,,,,,,,,,,,,,,O",
            "OOOOOOO,*,,,,,,,*,,,,,,*,O",
            "OOOOOOOOOOOOOOOOOOOOOOOOOO",
        }, chaos);

        return new[] { winnowing, keep, darkmere, arena };
    }
}
