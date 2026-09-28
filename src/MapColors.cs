namespace HexenSharp;

/// <summary>
/// Recolouring the current map's floor, ceiling (and sky) and fog from the console, to try out what reads best in a
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
    sealed record ThemeLook(Tex FloorIn, Tex FloorOut, Tex CeilIn, Tex Sky, uint Fog);
    readonly System.Runtime.CompilerServices.ConditionalWeakTable<Theme, ThemeLook> _looks = new();

    ThemeLook Look(Theme th) => _looks.GetValue(th, t => new ThemeLook(t.FloorIn, t.FloorOut, t.CeilIn, t.Sky, t.FogColor));

    public enum MapSurface { Floor, Ceiling, Fog }

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
            case MapSurface.Fog:
                th.FogColor = col ?? own.Fog;
                break;
        }
        string name = what switch { MapSurface.Floor => "floor", MapSurface.Ceiling => "ceiling and sky", _ => "fog" };
        return col is { } k ? $"{name}: {MapColors.Hex(k)}{(flat && what != MapSurface.Fog ? " (flat)" : "")}" : $"{name}: the map's own";
    }

    /// <summary>The current map's floor, ceiling, sky and fog colours (the textures' averages).</summary>
    public string MapColourReport()
    {
        if (Level?.Theme is not { } th) return "no map loaded";
        return $"floor {MapColors.Hex(MapColors.Average(Level.Outdoor.Any(o => o) ? th.OutdoorFloor : th.FloorIn))}  ceiling {MapColors.Hex(MapColors.Average(th.CeilIn))}"
               + $"  sky {MapColors.Hex(MapColors.Average(th.Sky))}  fog {MapColors.Hex(th.FogColor)}";
    }
}
