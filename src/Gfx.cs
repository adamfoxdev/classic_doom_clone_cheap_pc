using System.IO.Compression;

namespace HexenSharp;

/// <summary>Packed 32-bit colours in the byte order Raylib's R8G8B8A8 textures expect (0xAABBGGRR).</summary>
public static class Col
{
    public static uint Rgb(int r, int g, int b) =>
        0xFF000000u | ((uint)Math.Clamp(b, 0, 255) << 16) | ((uint)Math.Clamp(g, 0, 255) << 8) | (uint)Math.Clamp(r, 0, 255);

    public static uint Rgba(int r, int g, int b, int a) =>
        ((uint)Math.Clamp(a, 0, 255) << 24) | ((uint)Math.Clamp(b, 0, 255) << 16) | ((uint)Math.Clamp(g, 0, 255) << 8) | (uint)Math.Clamp(r, 0, 255);

    public static int R(uint c) => (int)(c & 0xFF);
    public static int G(uint c) => (int)((c >> 8) & 0xFF);
    public static int B(uint c) => (int)((c >> 16) & 0xFF);
    public static int A(uint c) => (int)(c >> 24);

    /// <summary>Scale brightness; s is 0..256 (256 = unchanged, above brightens).</summary>
    public static uint Shade(uint c, int s)
    {
        int r = (R(c) * s) >> 8, g = (G(c) * s) >> 8, b = (B(c) * s) >> 8;
        if (r > 255) r = 255;
        if (g > 255) g = 255;
        if (b > 255) b = 255;
        return 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | (uint)r;
    }

    /// <summary>Blend from a to b; t is 0..256.</summary>
    public static uint Lerp(uint a, uint b, int t)
    {
        int r = R(a) + (((R(b) - R(a)) * t) >> 8);
        int g = G(a) + (((G(b) - G(a)) * t) >> 8);
        int bl = B(a) + (((B(b) - B(a)) * t) >> 8);
        return 0xFF000000u | ((uint)bl << 16) | ((uint)g << 8) | (uint)r;
    }

    /// <summary>Apply light then fog: vis 0..256 (0 = fully fogged).</summary>
    public static uint Fog(uint c, int light, int vis, uint fog)
    {
        int r = (R(c) * light) >> 8, g = (G(c) * light) >> 8, b = (B(c) * light) >> 8;
        int fr = R(fog), fg = G(fog), fb = B(fog);
        r = fr + (((r - fr) * vis) >> 8);
        g = fg + (((g - fg) * vis) >> 8);
        b = fb + (((b - fb) * vis) >> 8);
        if (r > 255) r = 255;
        if (g > 255) g = 255;
        if (b > 255) b = 255;
        return 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | (uint)r;
    }
}

/// <summary>Small deterministic xorshift generator so procedural art is identical every run.</summary>
public sealed class Rng
{
    uint _s;
    public Rng(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; }
    public uint Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
    public int Int(int max) => (int)(Next() % (uint)max);
    public int Range(int lo, int hi) => lo + Int(hi - lo + 1);
    public float Float() => (Next() & 0xFFFFFF) / 16777216f;
    public float Range(float lo, float hi) => lo + Float() * (hi - lo);
}

/// <summary>A 32-bit image. Alpha 0 marks transparent texels in sprites.</summary>
public sealed class Tex
{
    public readonly int W, H;
    public readonly uint[] Px;
    public Tex(int w, int h) { W = w; H = h; Px = new uint[w * h]; }

    public uint Get(int x, int y) => Px[y * W + x];
    public void Set(int x, int y, uint c)
    {
        if ((uint)x < (uint)W && (uint)y < (uint)H) Px[y * W + x] = c;
    }

    public Tex Clone()
    {
        var t = new Tex(W, H);
        Array.Copy(Px, t.Px, Px.Length);
        return t;
    }
}

/// <summary>Primitive drawing onto a Tex, used to build all procedural art.</summary>
public sealed class Canvas
{
    public readonly Tex T;
    public Canvas(Tex t) { T = t; }
    public Canvas(int w, int h) { T = new Tex(w, h); }

    public void Clear(uint c) => Array.Fill(T.Px, c);

    public void Rect(int x, int y, int w, int h, uint c)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                T.Set(i, j, c);
    }

    public void Ellipse(float cx, float cy, float rx, float ry, uint c)
    {
        for (int j = (int)(cy - ry - 1); j <= (int)(cy + ry + 1); j++)
            for (int i = (int)(cx - rx - 1); i <= (int)(cx + rx + 1); i++)
            {
                float dx = (i + 0.5f - cx) / rx, dy = (j + 0.5f - cy) / ry;
                if (dx * dx + dy * dy <= 1f) T.Set(i, j, c);
            }
    }

    public void Circle(float cx, float cy, float r, uint c) => Ellipse(cx, cy, r, r, c);

    /// <summary>Radial glow: colour fades to transparent at the edge.</summary>
    public void Glow(float cx, float cy, float r, uint c)
    {
        for (int j = (int)(cy - r - 1); j <= (int)(cy + r + 1); j++)
            for (int i = (int)(cx - r - 1); i <= (int)(cx + r + 1); i++)
            {
                if ((uint)i >= (uint)T.W || (uint)j >= (uint)T.H) continue;
                float dx = i + 0.5f - cx, dy = j + 0.5f - cy;
                float d = MathF.Sqrt(dx * dx + dy * dy) / r;
                if (d >= 1f) continue;
                int a = (int)((1f - d) * 255);
                uint old = T.Get(i, j);
                if (Col.A(old) == 0)
                {
                    if (a > 60) T.Set(i, j, Col.Shade(c, 128 + a / 2));
                }
                else T.Set(i, j, Col.Lerp(old, c, a));
            }
    }

    public void Line(float x0, float y0, float x1, float y1, float thick, uint c)
    {
        float dx = x1 - x0, dy = y1 - y0;
        int steps = (int)MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) * 2 + 1;
        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            Circle(x0 + dx * t, y0 + dy * t, thick * 0.5f, c);
        }
    }

    public void Tri(float x0, float y0, float x1, float y1, float x2, float y2, uint c)
    {
        int minX = (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2)));
        int maxX = (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2)));
        int minY = (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2)));
        int maxY = (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2)));
        for (int j = minY; j <= maxY; j++)
            for (int i = minX; i <= maxX; i++)
            {
                float px = i + 0.5f, py = j + 0.5f;
                float d0 = (x1 - x0) * (py - y0) - (y1 - y0) * (px - x0);
                float d1 = (x2 - x1) * (py - y1) - (y2 - y1) * (px - x1);
                float d2 = (x0 - x2) * (py - y2) - (y0 - y2) * (px - x2);
                bool neg = d0 < 0 || d1 < 0 || d2 < 0, pos = d0 > 0 || d1 > 0 || d2 > 0;
                if (!(neg && pos)) T.Set(i, j, c);
            }
    }

    /// <summary>Randomly vary the brightness of opaque pixels for a gritty hand-painted look.</summary>
    public void Noise(Rng r, int amount)
    {
        for (int i = 0; i < T.Px.Length; i++)
        {
            uint c = T.Px[i];
            if (Col.A(c) == 0) continue;
            T.Px[i] = Col.Shade(c, 256 + r.Range(-amount, amount));
        }
    }

    /// <summary>Darken opaque pixels that touch transparency so sprites read clearly.</summary>
    public void Outline(uint c)
    {
        var src = T.Clone();
        for (int y = 0; y < T.H; y++)
            for (int x = 0; x < T.W; x++)
            {
                if (Col.A(src.Get(x, y)) != 0) continue;
                bool edge = false;
                for (int k = 0; k < 4 && !edge; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if ((uint)nx < (uint)T.W && (uint)ny < (uint)T.H && Col.A(src.Get(nx, ny)) != 0 && Col.A(src.Get(nx, ny)) != 1) edge = true;
                }
                if (edge) T.Set(x, y, c);
            }
    }

    public void Blit(Tex src, int dx, int dy)
    {
        for (int y = 0; y < src.H; y++)
            for (int x = 0; x < src.W; x++)
            {
                uint c = src.Get(x, y);
                if (Col.A(c) != 0) T.Set(dx + x, dy + y, c);
            }
    }
}

/// <summary>A compact 5x7 bitmap font rendered straight into the framebuffer.</summary>
public static class Font
{
    static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['A'] = new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
        ['B'] = new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " },
        ['C'] = new[] { " ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### " },
        ['D'] = new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " },
        ['E'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" },
        ['F'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " },
        ['G'] = new[] { " ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ####" },
        ['H'] = new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
        ['I'] = new[] { " ### ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
        ['J'] = new[] { "  ###", "   # ", "   # ", "   # ", "   # ", "#  # ", " ##  " },
        ['K'] = new[] { "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #" },
        ['L'] = new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" },
        ['M'] = new[] { "#   #", "## ##", "# # #", "# # #", "#   #", "#   #", "#   #" },
        ['N'] = new[] { "#   #", "#   #", "##  #", "# # #", "#  ##", "#   #", "#   #" },
        ['O'] = new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
        ['P'] = new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " },
        ['Q'] = new[] { " ### ", "#   #", "#   #", "#   #", "# # #", "#  # ", " ## #" },
        ['R'] = new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" },
        ['S'] = new[] { " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### " },
        ['T'] = new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " },
        ['U'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
        ['V'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  " },
        ['W'] = new[] { "#   #", "#   #", "#   #", "# # #", "# # #", "# # #", " # # " },
        ['X'] = new[] { "#   #", "#   #", " # # ", "  #  ", " # # ", "#   #", "#   #" },
        ['Y'] = new[] { "#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  " },
        ['Z'] = new[] { "#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####" },
        ['0'] = new[] { " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### " },
        ['1'] = new[] { "  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
        ['2'] = new[] { " ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####" },
        ['3'] = new[] { "#####", "   # ", "  #  ", "   # ", "    #", "#   #", " ### " },
        ['4'] = new[] { "   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # " },
        ['5'] = new[] { "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### " },
        ['6'] = new[] { "  ## ", " #   ", "#    ", "#### ", "#   #", "#   #", " ### " },
        ['7'] = new[] { "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   " },
        ['8'] = new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " },
        ['9'] = new[] { " ### ", "#   #", "#   #", " ####", "    #", "   # ", " ##  " },
        ['.'] = new[] { "     ", "     ", "     ", "     ", "     ", " ##  ", " ##  " },
        [','] = new[] { "     ", "     ", "     ", "     ", " ##  ", "  #  ", " #   " },
        ['!'] = new[] { "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "     ", "  #  " },
        ['?'] = new[] { " ### ", "#   #", "    #", "   # ", "  #  ", "     ", "  #  " },
        [':'] = new[] { "     ", " ##  ", " ##  ", "     ", " ##  ", " ##  ", "     " },
        ['-'] = new[] { "     ", "     ", "     ", "#####", "     ", "     ", "     " },
        ['+'] = new[] { "     ", "  #  ", "  #  ", "#####", "  #  ", "  #  ", "     " },
        ['='] = new[] { "     ", "     ", "#####", "     ", "#####", "     ", "     " },
        ['/'] = new[] { "    #", "    #", "   # ", "  #  ", " #   ", "#    ", "#    " },
        ['\''] = new[] { "  #  ", "  #  ", " #   ", "     ", "     ", "     ", "     " },
        ['('] = new[] { "   # ", "  #  ", " #   ", " #   ", " #   ", "  #  ", "   # " },
        [')'] = new[] { " #   ", "  #  ", "   # ", "   # ", "   # ", "  #  ", " #   " },
        ['%'] = new[] { "##   ", "##  #", "   # ", "  #  ", " #   ", "#  ##", "   ##" },
        ['>'] = new[] { " #   ", "  #  ", "   # ", "    #", "   # ", "  #  ", " #   " },
        ['#'] = new[] { " # # ", " # # ", "#####", " # # ", "#####", " # # ", " # # " },
        ['_'] = new[] { "     ", "     ", "     ", "     ", "     ", "     ", "#####" },
        ['['] = new[] { " ### ", " #   ", " #   ", " #   ", " #   ", " #   ", " ### " },
        [']'] = new[] { " ### ", "   # ", "   # ", "   # ", "   # ", "   # ", " ### " },
        ['^'] = new[] { "  #  ", " # # ", "#   #", "     ", "     ", "     ", "     " },
        ['|'] = new[] { "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " },
        ['*'] = new[] { "     ", "# # #", " ### ", "#####", " ### ", "# # #", "     " },
        ['"'] = new[] { " # # ", " # # ", "     ", "     ", "     ", "     ", "     " },
        [';'] = new[] { "     ", " ##  ", " ##  ", "     ", " ##  ", "  #  ", " #   " },
        ['<'] = new[] { "   # ", "  #  ", " #   ", "#    ", " #   ", "  #  ", "   # " },
    };

    public const int CharW = 6, CharH = 8;

    public static int Width(string s, int scale = 1) => s.Length * CharW * scale;

    public static void Draw(uint[] fb, int fbW, int fbH, int x, int y, string s, uint color, int scale = 1, bool shadow = true)
    {
        if (shadow) DrawRaw(fb, fbW, fbH, x + scale, y + scale, s, Col.Rgb(0, 0, 0), scale);
        DrawRaw(fb, fbW, fbH, x, y, s, color, scale);
    }

    static void DrawRaw(uint[] fb, int fbW, int fbH, int x, int y, string s, uint color, int scale)
    {
        foreach (char ch in s)
        {
            if (Glyphs.TryGetValue(char.ToUpperInvariant(ch), out var g))
            {
                for (int gy = 0; gy < 7; gy++)
                    for (int gx = 0; gx < 5; gx++)
                    {
                        if (g[gy][gx] == ' ') continue;
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                            {
                                int px = x + gx * scale + sx, py = y + gy * scale + sy;
                                if ((uint)px < (uint)fbW && (uint)py < (uint)fbH) fb[py * fbW + px] = color;
                            }
                    }
            }
            x += CharW * scale;
        }
    }
}

/// <summary>Minimal PNG encoder for screenshots, and a decoder for the rendered-art pack (8-bit RGB/RGBA, not interlaced).</summary>
public static class Png
{
    static readonly uint[] CrcTable = BuildCrc();

    static uint[] BuildCrc()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    static uint Crc(byte[] data, int start, int len)
    {
        uint c = 0xFFFFFFFFu;
        for (int i = start; i < start + len; i++) c = CrcTable[(c ^ data[i]) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    public static void Save(string path, uint[] px, int w, int h) => File.WriteAllBytes(path, Encode(px, w, h));

    /// <summary>The picture as a PNG file's bytes (8-bit RGB).</summary>
    public static byte[] Encode(uint[] px, int w, int h)
    {
        using var raw = new MemoryStream();
        for (int y = 0; y < h; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < w; x++)
            {
                uint c = px[y * w + x];
                raw.WriteByte((byte)Col.R(c));
                raw.WriteByte((byte)Col.G(c));
                raw.WriteByte((byte)Col.B(c));
            }
        }
        using var zbuf = new MemoryStream();
        using (var z = new ZLibStream(zbuf, CompressionLevel.Optimal, true)) raw.WriteTo(z);

        using var fs = new MemoryStream();
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        WriteBE(ihdr, 0, (uint)w);
        WriteBE(ihdr, 4, (uint)h);
        ihdr[8] = 8; ihdr[9] = 2; // 8-bit RGB
        Chunk(fs, "IHDR", ihdr);
        Chunk(fs, "IDAT", zbuf.ToArray());
        Chunk(fs, "IEND", Array.Empty<byte>());
        return fs.ToArray();
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var buf = new byte[data.Length + 12];
        WriteBE(buf, 0, (uint)data.Length);
        for (int i = 0; i < 4; i++) buf[4 + i] = (byte)type[i];
        Array.Copy(data, 0, buf, 8, data.Length);
        WriteBE(buf, 8 + data.Length, Crc(buf, 4, data.Length + 4));
        s.Write(buf);
    }

    /// <summary>Decodes an 8-bit RGB or RGBA PNG. Alpha is made all-or-nothing, as the renderer expects.</summary>
    public static Tex Load(byte[] png)
    {
        if (png.Length < 8 || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71) throw new InvalidDataException("not a PNG");
        int w = 0, h = 0, type = 0, pos = 8;
        using var idat = new MemoryStream();
        while (pos + 8 <= png.Length)
        {
            int len = (int)ReadBE(png, pos);
            string tag = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            int data = pos + 8;
            if (tag == "IHDR")
            {
                w = (int)ReadBE(png, data); h = (int)ReadBE(png, data + 4);
                type = png[data + 9];
                if (png[data + 8] != 8 || (type != 2 && type != 6) || png[data + 12] != 0)
                    throw new InvalidDataException("only 8-bit RGB/RGBA, non-interlaced PNGs are supported");
            }
            else if (tag == "IDAT") idat.Write(png, data, len);
            else if (tag == "IEND") break;
            pos = data + len + 4;
        }
        int bpp = type == 6 ? 4 : 3, stride = w * bpp;
        var raw = new byte[(stride + 1) * h];
        idat.Position = 0;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.ReadExactly(raw);

        var cur = new byte[stride];
        var prev = new byte[stride];
        var tex = new Tex(w, h);
        for (int y = 0; y < h; y++)
        {
            int f = raw[y * (stride + 1)];
            Array.Copy(raw, y * (stride + 1) + 1, cur, 0, stride);
            for (int i = 0; i < stride; i++)
            {
                int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                cur[i] = (byte)(cur[i] + f switch
                {
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => 0,
                });
            }
            for (int x = 0; x < w; x++)
            {
                int o = x * bpp;
                int alpha = bpp == 4 ? cur[o + 3] : 255;
                tex.Px[y * w + x] = alpha < 128 ? 0u : Col.Rgb(cur[o], cur[o + 1], cur[o + 2]);
            }
            (prev, cur) = (cur, prev);
        }
        return tex;
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    static uint ReadBE(byte[] b, int o) => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);

    static void WriteBE(byte[] b, int o, uint v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }
}
