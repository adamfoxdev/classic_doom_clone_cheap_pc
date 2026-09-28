namespace HexenSharp;

/// <summary>
/// Recolouring the current map's floor, ceiling (and sky), walls (and the faces of steps) and fog from the console, to try out what reads best in a
/// mode. A colour tints the texture, keeping its pattern with the colour as its average; 'flat' paints it one solid
/// colour; 'off' puts the map's own back. It lasts until you leave the map (or it's rebuilt, as on Restart).
/// </summary>
public static class MapColors
{
    static readonly Dictionary<string, uint> Names = new()
    {
        ["black"] = Col.Rgb(0, 0, 0), ["white"] = Col.Rgb(255, 255, 255), ["grey"] = Col.Rgb(128, 128, 128), ["gray"] = Col.Rgb(128, 128, 128),
        ["dark"] = Col.Rgb(40, 40, 44), ["light"] = Col.Rgb(200, 200, 204), ["red"] = Col.Rgb(200, 40, 40), ["green"] = Col.Rgb(50, 170, 60),
        ["blue"] = Col.Rgb(50, 80, 200), ["navy"] = Col.Rgb(20, 28, 70), ["sky"] = Col.Rgb(120, 170, 230), ["yellow"] = Col.Rgb(220, 200, 60),
        ["orange"] = Col.Rgb(220, 130, 40), ["purple"] = Col.Rgb(120, 60, 170), ["brown"] = Col.Rgb(110, 76, 44), ["sand"] = Col.Rgb(200, 176, 128),
        ["teal"] = Col.Rgb(40, 140, 140),
    };

    public static IEnumerable<string> ColourNames => Names.Keys;

    /// <summary>A colour from the words after the command: #rrggbb, rrggbb, three numbers 0-255, or a name.</summary>
    public static bool TryParse(string[] words, out uint col)
    {
        col = 0;
        if (words.Length == 0) return false;
        if (words.Length >= 3 && int.TryParse(words[0], out int r) && int.TryParse(words[1], out int g) && int.TryParse(words[2], out int b))
        {
            if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255) return false;
            col = Col.Rgb(r, g, b);
            return true;
        }
        string w = words[0].ToLowerInvariant();
        if (Names.TryGetValue(w, out col)) return true;
        w = w.TrimStart('#');
        if (w.Length == 6 && int.TryParse(w, System.Globalization.NumberStyles.HexNumber, null, out int hex))
        {
            col = Col.Rgb(hex >> 16 & 0xFF, hex >> 8 & 0xFF, hex & 0xFF);
            return true;
        }
        return false;
    }

    public static string Hex(uint c) => $"#{Col.R(c):x2}{Col.G(c):x2}{Col.B(c):x2}";

    /// <summary>A texture's average colour.</summary>
    public static uint Average(Tex t)
    {
        long r = 0, g = 0, b = 0;
        foreach (uint c in t.Px) { r += Col.R(c); g += Col.G(c); b += Col.B(c); }
        int n = Math.Max(1, t.Px.Length);
        return Col.Rgb((int)(r / n), (int)(g / n), (int)(b / n));
    }

    /// <summary>The texture recoloured: each texel's brightness (against the texture's average) times the colour; or the colour alone, flat.</summary>
    public static Tex Recolour(Tex src, uint col, bool flat)
    {
        var t = new Tex(src.W, src.H);
        int cr = Col.R(col), cg = Col.G(col), cb = Col.B(col);
        if (flat) { Array.Fill(t.Px, Col.Rgb(cr, cg, cb)); return t; }
        static int Luma(uint c) => (Col.R(c) * 77 + Col.G(c) * 150 + Col.B(c) * 29) >> 8;
        long sum = 0;
        foreach (uint c in src.Px) sum += Luma(c);
        float avg = MathF.Max(1, sum / (float)Math.Max(1, src.Px.Length));
        for (int i = 0; i < src.Px.Length; i++)
        {
            float k = Luma(src.Px[i]) / avg;
            t.Px[i] = Col.Rgb(Math.Min(255, (int)(cr * k)), Math.Min(255, (int)(cg * k)), Math.Min(255, (int)(cb * k)));
        }
        return t;
    }
}

public sealed partial class Game
{
    /// <summary>The map's own floor, ceiling, sky and fog, kept while they're recoloured (so 'off' can put them back).</summary>
    sealed record ThemeLook(Tex FloorIn, Tex FloorOut, Tex CeilIn, Tex Sky, uint Fog, Dictionary<char, Tex> Walls, Tex Riser, int Light);
    readonly System.Runtime.CompilerServices.ConditionalWeakTable<Theme, ThemeLook> _looks = new();

    ThemeLook Look(Theme th) => _looks.GetValue(th, t => new ThemeLook(t.FloorIn, t.FloorOut, t.CeilIn, t.Sky, t.FogColor, new Dictionary<char, Tex>(t.Walls), t.Riser, t.Light));

    public enum MapSurface { Floor, Ceiling, Walls, Fog }

    /// <summary>Recolours a surface of the current map (null puts its own back); says what it did.</summary>
    public string SetMapColour(MapSurface what, uint? col, bool flat = false)
    {
        if (Level?.Theme is not { } th) return "no map loaded";
        var own = Look(th);
        switch (what)
        {
            case MapSurface.Floor:
                th.FloorIn = col is { } f ? MapColors.Recolour(own.FloorIn, f, flat) : own.FloorIn;
                th.FloorOut = own.FloorOut == null ? null : col is { } fo ? MapColors.Recolour(own.FloorOut, fo, flat) : own.FloorOut;
                break;
            case MapSurface.Ceiling:
                th.CeilIn = col is { } c ? MapColors.Recolour(own.CeilIn, c, flat) : own.CeilIn;
                th.Sky = col is { } s ? MapColors.Recolour(own.Sky, s, flat) : own.Sky;
                break;
            case MapSurface.Walls:
                // every kind of plain wall (each tinted from its own texture, so bricks stay bricks), and step faces;
                // doors, levers, blocks, rubble and ore keep their looks, so you can still tell them apart
                foreach (var (glyph, tex) in own.Walls) th.Walls[glyph] = col is { } w ? MapColors.Recolour(tex, w, flat) : tex;
                th.Riser = col is { } r ? MapColors.Recolour(own.Riser ?? Art.StepRiser, r, flat) : own.Riser;
                break;
            case MapSurface.Fog:
                th.FogColor = col ?? own.Fog;
                break;
        }
        string name = what switch { MapSurface.Floor => "floor", MapSurface.Ceiling => "ceiling and sky", MapSurface.Walls => "walls", _ => "fog" };
        return col is { } k ? $"{name}: {MapColors.Hex(k)}{(flat && what != MapSurface.Fog ? " (flat)" : "")}" : $"{name}: the map's own";
    }

    /// <summary>The current map's floor, ceiling, sky and fog colours (the textures' averages).</summary>
    public string MapColourReport()
    {
        if (Level?.Theme is not { } th) return "no map loaded";
        return $"floor {MapColors.Hex(MapColors.Average(Level.Outdoor.Any(o => o) ? th.OutdoorFloor : th.FloorIn))}  ceiling {MapColors.Hex(MapColors.Average(th.CeilIn))}"
               + $"  walls {MapColors.Hex(MapColors.Average(th.Walls.TryGetValue('#', out var w) ? w : Art.Stone))}  sky {MapColors.Hex(MapColors.Average(th.Sky))}  fog {MapColors.Hex(th.FogColor)}";
    }
}

/// <summary>
/// Whole looks for a room, from the console ('roomlook tron' / 'roomlook matrix'), made for Rocket Soccer's pitch but
/// good on any map. Tron: black, with glowing cyan lines round every floor cell, wall panel and step, a glowing
/// horizon, and orange goals (any ice walls). Matrix: black, with green code raining down the walls and the sky,
/// a faint green grid underfoot, and the goals (ice walls) in denser, paler rain. Vaporwave: a hot-pink grid on deep
/// purple underfoot, walls fading from pink to violet behind thin blinds, a striped sun setting in a pink and orange
/// sky, and teal goals. 'roomlook off' puts the map's own back.
/// </summary>
public static class RoomLooks
{
    public static readonly string[] Names = { "tron", "matrix", "vaporwave" };
    const int S = Art.TS;

    static uint Glow(int r, int g, int b, float k) => Col.Rgb(Math.Min(255, (int)(r * k)), Math.Min(255, (int)(g * k)), Math.Min(255, (int)(b * k)));

    /// <summary>A panel: a dark fill, and glowing lines round its edges (and across it, every `every` pixels) with a soft halo.</summary>
    public static Tex Panel(uint fill, (int r, int g, int b) line, int every = 0, float bright = 1f, bool vertical = true)
    {
        var t = new Tex(S, S);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int dx = Math.Min(x, S - 1 - x), dy = Math.Min(y, S - 1 - y);
                if (every > 0) dy = Math.Min(dy, Math.Abs(y % every - every / 2) == 0 ? 0 : 99);
                int d = vertical ? Math.Min(dx, dy) : dy;
                float k = d == 0 ? 1f : d == 1 ? 0.45f : d == 2 ? 0.16f : 0f;
                t.Px[y * S + x] = k > 0 ? Col.Lerp(fill, Glow(line.r, line.g, line.b, bright), (int)(k * 256)) : fill;
            }
        return t;
    }

    /// <summary>A sky: black overhead, a deep glow toward the horizon, and a bright line along it.</summary>
    public static Tex Horizon((int r, int g, int b) glow)
    {
        var t = new Tex(256, 128);
        for (int y = 0; y < 128; y++)
        {
            float f = y / 127f, k = f * f * f * 0.5f;
            if (y >= 124) k = 1f; else if (y >= 120) k = 0.55f;
            uint c = Glow(glow.r, glow.g, glow.b, k);
            for (int x = 0; x < 256; x++) t.Px[y * 256 + x] = c;
        }
        // faint lines across the glow, like a far-off grid
        for (int y = 96; y < 120; y += 6)
            for (int x = 0; x < 256; x++) t.Px[y * 256 + x] = Col.Lerp(t.Px[y * 256 + x], Glow(glow.r, glow.g, glow.b, 0.8f), 90);
        return t;
    }

    /// <summary>A vaporwave wall: pink at the top fading to deep violet, crossed by thin dark blinds, with glowing edges.</summary>
    public static Tex Blinds((int r, int g, int b) top, (int r, int g, int b) bottom, (int r, int g, int b) edge)
    {
        var t = new Tex(S, S);
        for (int y = 0; y < S; y++)
        {
            float f = y / (S - 1f);
            uint c = Col.Rgb((int)(top.r + (bottom.r - top.r) * f), (int)(top.g + (bottom.g - top.g) * f), (int)(top.b + (bottom.b - top.b) * f));
            bool blind = y % 8 == 7;
            for (int x = 0; x < S; x++)
            {
                int d = Math.Min(Math.Min(x, S - 1 - x), Math.Min(y, S - 1 - y));
                uint px = blind ? Col.Lerp(c, Col.Rgb(20, 0, 40), 150) : c;
                if (d <= 1) px = Col.Lerp(px, Col.Rgb(edge.r, edge.g, edge.b), d == 0 ? 230 : 110);
                t.Px[y * S + x] = px;
            }
        }
        return t;
    }

    /// <summary>A vaporwave sky: violet overhead through pink to orange low down, with a big sun, striped across its lower half, sinking into it.</summary>
    public static Tex Sunset()
    {
        const int w = 256, h = 128;
        var t = new Tex(w, h);
        // (the warm colours sit high, so they show over the walls of a tall room)
        (float at, (int r, int g, int b) c)[] stops = { (0f, (24, 6, 52)), (0.3f, (90, 20, 120)), (0.55f, (230, 60, 160)), (0.8f, (255, 140, 90)), (1f, (255, 170, 110)) };
        for (int y = 0; y < h; y++)
        {
            float f = y / (h - 1f);
            int i = 0;
            while (i < stops.Length - 2 && f > stops[i + 1].at) i++;
            float k = Math.Clamp((f - stops[i].at) / (stops[i + 1].at - stops[i].at), 0, 1);
            var (a, b) = (stops[i].c, stops[i + 1].c);
            uint c = Col.Rgb((int)(a.r + (b.r - a.r) * k), (int)(a.g + (b.g - a.g) * k), (int)(a.b + (b.b - a.b) * k));
            for (int x = 0; x < w; x++) t.Px[y * w + x] = c;
        }
        // the sun: yellow at its top to hot pink, its lower half cut by widening bands
        float cx = w / 2f, cy = 50, rad = 30;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy > rad * rad) continue;
                float down = (y - (cy - rad)) / (2 * rad);
                if (y > cy - rad * 0.35f)
                {
                    int band = (int)((y - (cy - rad * 0.35f)) / 5);
                    if ((y - (cy - rad * 0.35f)) % 5 < 1 + band * 0.7f) continue; // the stripes, wider as they go down
                }
                t.Px[y * w + x] = Col.Rgb(255, (int)(230 - 170 * down), (int)(90 + 80 * down));
            }
        return t;
    }

    /// <summary>
    /// Code rain on a texture: columns of little random glyphs, each column with a bright head falling down it and a
    /// fading trail; the glyphs flicker as they go. Update redraws it for a moment in time.
    /// </summary>
    public sealed class Rain
    {
        public readonly Tex Tex;
        readonly int _cols, _rows, _cycle;
        readonly float[] _speed, _phase;
        readonly int[] _glyph;
        readonly ushort[] _shapes;
        readonly float _density;
        readonly (int r, int g, int b) _colour;
        const int CW = 4, CH = 8;

        public Rain(int w, int h, int seed, float density = 1f, (int r, int g, int b)? colour = null)
        {
            _colour = colour ?? (30, 255, 90);
            Tex = new Tex(w, h);
            _cols = w / CW; _rows = h / CH; _cycle = _rows + 6; _density = density;
            var rng = new Random(seed);
            _speed = Enumerable.Range(0, _cols).Select(_ => 4f + (float)rng.NextDouble() * 7f).ToArray();
            _phase = Enumerable.Range(0, _cols).Select(_ => (float)rng.NextDouble() * 40f).ToArray();
            _glyph = Enumerable.Range(0, _cols * _rows).Select(_ => rng.Next(32)).ToArray();
            // 3 by 5 glyphs: random bits, each with a solid stroke so none are empty
            _shapes = Enumerable.Range(0, 32).Select(_ => (ushort)(rng.Next(1 << 15) | (1 << rng.Next(15)) | 0x1248)).ToArray();
            Update(0);
        }

        public void Update(float time)
        {
            var px = Tex.Px;
            Array.Fill(px, Col.Rgb(0, 6, 2));
            int flick = (int)(time * 9);
            for (int c = 0; c < _cols; c++)
            {
                float head = (time * _speed[c] + _phase[c]) % _cycle;
                for (int r = 0; r < _rows; r++)
                {
                    float behind = head - r;
                    if (behind < 0) behind += _cycle;
                    float trail = 7f * _density;
                    if (behind > trail) continue;
                    float k = 1 - behind / trail;
                    uint col = behind < 1 ? Col.Rgb(220, 255, 225) : Glow(_colour.r, _colour.g, _colour.b, 0.25f + 0.85f * k);
                    int gi = _glyph[r * _cols + c];
                    if ((c * 7 + r * 13 + flick) % 11 == 0) gi = (gi + flick) & 31; // a flicker as it falls
                    ushort shape = _shapes[gi];
                    for (int gy = 0; gy < 5; gy++)
                        for (int gx = 0; gx < 3; gx++)
                            if ((shape >> (gy * 3 + gx) & 1) != 0) px[(r * CH + 1 + gy) * Tex.W + c * CW + gx] = col;
                }
            }
        }
    }
}

public sealed partial class Game
{
    /// <summary>The room look in force ('tron', 'matrix'), on which map's theme, and its code rain (the Matrix's).</summary>
    public string RoomLook { get; private set; }
    Theme _roomLookTheme;
    readonly List<RoomLooks.Rain> _rain = new();
    float _rainClock, _rainTime;

    /// <summary>Dresses the current map in a room look (null or "off" puts its own back); says what it did.</summary>
    public string SetRoomLook(string look)
    {
        if (Level?.Theme is not { } th) return "no map loaded";
        var own = Look(th);
        look = look?.ToLowerInvariant();
        _rain.Clear();
        // back to the map's own first, so looks don't stack
        th.FloorIn = own.FloorIn; th.FloorOut = own.FloorOut; th.CeilIn = own.CeilIn; th.Sky = own.Sky; th.FogColor = own.Fog; th.Riser = own.Riser;
        foreach (var (g, t) in own.Walls) th.Walls[g] = t;
        th.Light = own.Light;
        RoomLook = null; _roomLookTheme = null;
        if (look is null or "off" or "none") return "room look: the map's own";
        uint black = Col.Rgb(2, 3, 8);
        switch (look)
        {
            case "tron":
            {
                var cyan = (0, 220, 255);
                th.FloorIn = RoomLooks.Panel(black, cyan);
                th.FloorOut = th.FloorIn;
                th.CeilIn = RoomLooks.Panel(Col.Rgb(0, 0, 4), cyan, bright: 0.45f);
                var wall = RoomLooks.Panel(black, cyan, every: 32);
                foreach (var g in own.Walls.Keys.ToList()) th.Walls[g] = wall;
                th.Walls['I'] = RoomLooks.Panel(Col.Rgb(12, 4, 0), (255, 140, 20), every: 16, bright: 1.1f); // the goals: Tron orange
                th.Riser = RoomLooks.Panel(black, cyan, vertical: false);
                th.Sky = RoomLooks.Horizon((0, 170, 230));
                th.FogColor = Col.Rgb(0, 4, 10);
                th.Light = 256;
                break;
            }
            case "matrix":
            {
                var green = (20, 200, 70);
                th.FloorIn = RoomLooks.Panel(Col.Rgb(0, 8, 3), green, bright: 0.55f);
                th.FloorOut = th.FloorIn;
                th.CeilIn = RoomLooks.Panel(Col.Rgb(0, 4, 1), green, bright: 0.3f);
                var wall = new RoomLooks.Rain(Art.TS, Art.TS, 7);
                var goal = new RoomLooks.Rain(Art.TS, Art.TS, 11, 2.2f, (170, 255, 200)); // the goals: denser, paler rain
                var sky = new RoomLooks.Rain(256, 128, 5, 0.8f);
                _rain.AddRange(new[] { wall, goal, sky });
                foreach (var g in own.Walls.Keys.ToList()) th.Walls[g] = wall.Tex;
                th.Walls['I'] = goal.Tex;
                th.Riser = RoomLooks.Panel(Col.Rgb(0, 8, 3), green, vertical: false);
                th.Sky = sky.Tex;
                th.FogColor = Col.Rgb(0, 10, 4);
                th.Light = 256;
                break;
            }
            case "vaporwave":
            {
                var pink = (255, 60, 200);
                th.FloorIn = RoomLooks.Panel(Col.Rgb(26, 4, 48), pink, bright: 1.1f);
                th.FloorOut = th.FloorIn;
                th.CeilIn = RoomLooks.Panel(Col.Rgb(20, 4, 40), (90, 220, 255), bright: 0.5f);
                var wall = RoomLooks.Blinds((255, 130, 210), (70, 20, 130), (90, 230, 255));
                foreach (var g in own.Walls.Keys.ToList()) th.Walls[g] = wall;
                th.Walls['I'] = RoomLooks.Blinds((120, 255, 230), (20, 110, 140), (255, 255, 255)); // the goals: teal
                th.Riser = RoomLooks.Panel(Col.Rgb(40, 8, 70), pink, vertical: false);
                th.Sky = RoomLooks.Sunset();
                th.FogColor = Col.Rgb(70, 20, 90);
                th.Light = 256;
                break;
            }
            default:
                return $"unknown room look '{look}' (try: {string.Join(", ", RoomLooks.Names)}, off)";
        }
        RoomLook = look; _roomLookTheme = th;
        return $"room look: {look}";
    }

    /// <summary>Each frame: the Matrix's code rain falls (redrawn fifteen times a second); a look ends with its map.</summary>
    void RoomLookTick(float dt)
    {
        if (RoomLook == null) return;
        if (Level?.Theme != _roomLookTheme) { RoomLook = null; _rain.Clear(); return; }
        _rainTime += dt;
        if ((_rainClock += dt) < 1f / 15f) return;
        _rainClock = 0;
        foreach (var r in _rain) r.Update(_rainTime);
    }
}
