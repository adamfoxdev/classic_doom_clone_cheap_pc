namespace HexenSharp;

/// <summary>
/// The sci-fi look: a space-station reskin of every texture and sprite. It runs after the fantasy builders
/// and replaces their results, so every art slot always has something in it.
/// </summary>
public static class SciFiArt
{
    const int TS = Art.TS;
    static uint Dark => Art.Dark;
    static uint C(int r, int g, int b) => Col.Rgb(r, g, b);

    public static void Apply()
    {
        BuildWalls();
        BuildFlats();
        BuildSprites();
        BuildWeapons();
    }

    // ------------------------------------------------------------------ walls

    /// <summary>A riveted metal wall panel: seams, bevels and a light top-to-bottom gradient.</summary>
    static Canvas Panel(uint seed, (int r, int g, int b) b, int ph = 32, int pw = 32)
    {
        var r = new Rng(seed);
        var c = new Canvas(TS, TS);
        for (int y = 0; y < TS; y++)
        {
            int shade = 12 - (y % ph) * 24 / ph;
            for (int x = 0; x < TS; x++) c.T.Px[y * TS + x] = C(b.r + shade, b.g + shade, b.b + shade);
        }
        for (int y = 0; y < TS; y += ph)
        {
            c.Rect(0, y, TS, 1, C(b.r / 3, b.g / 3, b.b / 3));
            c.Rect(0, y + 1, TS, 1, C(b.r + 40, b.g + 40, b.b + 40));
        }
        for (int x = 0; x < TS; x += pw)
        {
            c.Rect(x, 0, 1, TS, C(b.r / 3, b.g / 3, b.b / 3));
            c.Rect(x + 1, 0, 1, TS, C(b.r + 30, b.g + 30, b.b + 30));
        }
        for (int y = 0; y < TS; y += ph)
            for (int x = 0; x < TS; x += pw)
                foreach (var (dx, dy) in new[] { (4, 5), (pw - 5, 5), (4, ph - 4), (pw - 5, ph - 4) })
                {
                    c.Circle(x + dx, y + dy, 1.3f, C(b.r / 2, b.g / 2, b.b / 2));
                    c.T.Set(x + dx - 1, y + dy - 1, C(b.r + 60, b.g + 60, b.b + 60));
                }
        c.Noise(r, 8);
        return c;
    }

    static void Hazard(Canvas c, int y0, int h)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = 0; x < TS; x++)
                c.T.Set(x, y, ((x + y) / 6) % 2 == 0 ? C(230, 180, 30) : C(30, 28, 24));
        c.Rect(0, y0, TS, 1, C(20, 18, 16));
        c.Rect(0, y0 + h - 1, TS, 1, C(20, 18, 16));
    }

    static void BuildWalls()
    {
        // '#': grey-blue hull panels with a vent
        {
            var c = Panel(1101, (88, 96, 110));
            c.Rect(38, 40, 20, 14, C(40, 44, 52));
            for (int y = 42; y < 53; y += 3) c.Rect(40, y, 16, 1, C(20, 22, 26));
            Art.Stone = c.T;
        }
        // 'B': rusty industrial plating with a hazard band
        {
            var c = Panel(1102, (116, 80, 58), 32, 16);
            var r = new Rng(9);
            for (int i = 0; i < 30; i++) c.Circle(r.Range(0, 64), r.Range(0, 50), r.Range(1, 3), C(140 + r.Int(30), 70 + r.Int(20), 40));
            Hazard(c, 52, 12);
            Art.Brick = c.T;
        }
        // 'W': computer consoles with glowing screens and buttons
        {
            var r = new Rng(1103); var c = Panel(1103, (46, 52, 62), 64, 64);
            c.Rect(7, 7, 50, 30, C(20, 24, 28));
            c.Rect(9, 9, 46, 26, C(10, 40, 44));
            for (int y = 11; y < 33; y += 4) c.Rect(11, y, r.Range(10, 40), 2, C(90, 255, 170));
            c.Rect(9, 9, 46, 1, C(60, 120, 120));
            for (int i = 0; i < 6; i++)
            {
                uint col = i % 3 == 0 ? C(255, 80, 60) : i % 3 == 1 ? C(80, 200, 255) : C(255, 220, 80);
                c.Rect(9 + i * 8, 44, 5, 4, col);
            }
            c.Rect(8, 54, 48, 3, C(30, 34, 40));
            Art.Wood = c.T;
        }
        // 'M': bio-infested panels
        {
            var c = Panel(1104, (80, 90, 88));
            var r = new Rng(77);
            for (int i = 0; i < 20; i++)
            {
                float x = r.Range(0, 64), y = r.Range(0, 64), rad = r.Range(2, 7);
                c.Ellipse(x, y, rad, rad * 0.8f, C(60 + r.Int(40), 150 + r.Int(60), 60 + r.Int(30)));
                c.Circle(x - 1, y - 1, rad * 0.3f, C(180, 255, 150));
            }
            for (int i = 0; i < 8; i++) { int x = r.Int(64); c.Rect(x, 0, 2, r.Range(6, 22), C(70, 180, 80)); }
            Art.Moss = c.T;
        }
        // 'I': white clean-room panels with a cyan light strip
        {
            var c = Panel(1105, (196, 210, 222));
            c.Rect(29, 0, 6, 64, C(40, 60, 70));
            c.Rect(30, 0, 4, 64, C(150, 250, 255));
            for (int y = 0; y < 64; y++) { c.T.Set(28, y, C(120, 200, 220)); c.T.Set(35, y, C(120, 200, 220)); }
            Art.Ice = c.T;
        }
        // 'O': white ceramic hull with orange trim
        {
            var c = Panel(1106, (210, 208, 200), 64, 32);
            c.Rect(0, 26, 64, 5, C(230, 120, 30));
            c.Rect(0, 26, 64, 1, C(255, 170, 80));
            c.Rect(0, 56, 64, 3, C(60, 60, 64));
            Art.Marble = c.T;
        }
        // 'D': blast door with hazard chevrons and a centre seam
        {
            var c = Panel(1107, (120, 124, 130), 64, 64);
            Hazard(c, 24, 16);
            c.Rect(31, 0, 2, 64, C(30, 30, 34));
            c.Rect(0, 0, 64, 4, C(70, 72, 78)); c.Rect(0, 60, 64, 4, C(70, 72, 78));
            Art.Door = c.T;
        }
        Art.SteelDoor = KeyDoor(1108, C(60, 140, 255));
        Art.FireDoor = KeyDoor(1109, C(255, 60, 50));

        // 'P': laser-grid force field (see-through)
        {
            var c = new Canvas(TS, TS);
            for (int y = 4; y < 64; y += 7)
            {
                c.Rect(0, y, 64, 1, C(120, 255, 255));
                c.Rect(0, y + 1, 64, 1, C(40, 160, 200));
            }
            for (int x = 8; x < 64; x += 16) c.Rect(x, 0, 1, 64, C(60, 200, 230));
            c.Rect(0, 0, 3, 64, C(70, 74, 80)); c.Rect(61, 0, 3, 64, C(70, 74, 80));
            Art.Portcullis = c.T;
        }
        Art.LeverOff = Switch(false);
        Art.LeverOn = Switch(true);

        // 'X': cargo crate
        {
            var c = new Canvas(TS, TS);
            c.Clear(C(110, 104, 60));
            c.Rect(0, 0, 64, 6, C(70, 66, 40)); c.Rect(0, 58, 64, 6, C(70, 66, 40));
            c.Rect(0, 0, 6, 64, C(70, 66, 40)); c.Rect(58, 0, 6, 64, C(70, 66, 40));
            c.Line(6, 6, 58, 58, 5, C(86, 80, 48)); c.Line(58, 6, 6, 58, 5, C(86, 80, 48));
            c.Rect(20, 26, 24, 12, C(40, 38, 26));
            Font.Draw(c.T.Px, TS, TS, 23, 29, "X-7", C(230, 200, 80), 1, false);
            c.Noise(new Rng(1110), 12);
            Art.Block = c.T;
        }
        // 'K': cave-in rubble, chunks of asteroid rock with glinting ore
        {
            var t = Art.Cobble(1112, (100, 90, 84), 16, (20, 18, 22), 11);
            var c = new Canvas(t);
            var r = new Rng(1113);
            for (int i = 0; i < 7; i++)
            {
                float x = r.Range(4f, 60f), y = r.Range(4f, 60f);
                c.Tri(x - 2, y + 1, x + 2, y + 1, x, y - 3, C(70, 200, 230));
                c.T.Set((int)x, (int)y - 1, C(200, 250, 255));
            }
            Art.Rubble = t;
        }
        // stair step face: dark steel with a hazard lip
        {
            var c = Panel(1111, (70, 74, 80), 16, 32);
            for (int y = 0; y < 64; y += 16) { Hazard(c, y, 3); }
            Art.StepRiser = c.T;
        }
    }

    static Tex KeyDoor(uint seed, uint light)
    {
        var c = Panel(seed, (96, 100, 110), 64, 32);
        c.Rect(31, 0, 2, 64, C(30, 30, 34));
        c.Rect(20, 22, 24, 20, C(26, 28, 32));
        c.Glow(32, 32, 16, light);
        c.Rect(24, 26, 16, 12, Col.Shade(light, 200));
        c.Rect(27, 30, 10, 2, C(20, 20, 24));
        c.Rect(0, 0, 64, 3, light); c.Rect(0, 61, 64, 3, light);
        return c.T;
    }

    static Tex Switch(bool on)
    {
        var c = Panel(1112, (88, 96, 110));
        c.Rect(18, 18, 28, 30, C(30, 32, 38));
        c.Rect(20, 20, 24, 26, C(56, 60, 70));
        uint lamp = on ? C(80, 255, 120) : C(255, 60, 50);
        c.Glow(32, 25, 9, lamp);
        c.Circle(32, 25, 3, lamp);
        if (on) c.Rect(29, 32, 6, 12, C(200, 200, 210)); else c.Rect(29, 30, 6, 8, C(200, 200, 210));
        return c.T;
    }

    // ------------------------------------------------------------------ flats and skies

    static void BuildFlats()
    {
        // tread plate
        {
            var c = new Canvas(TS, TS);
            c.Clear(C(92, 96, 102));
            for (int y = 2; y < 64; y += 8)
                for (int x = (y / 8 % 2) * 4 + 2; x < 64; x += 8)
                {
                    c.Line(x - 2, y + 2, x + 2, y - 2, 2, C(130, 134, 140));
                    c.T.Set(x + 1, y - 1, C(160, 164, 170));
                }
            c.Rect(0, 0, 64, 1, C(50, 52, 56)); c.Rect(0, 0, 1, 64, C(50, 52, 56));
            c.Noise(new Rng(1201), 10);
            Art.FloorStone = c.T;
        }
        // grating
        {
            var c = new Canvas(TS, TS);
            c.Clear(C(70, 74, 78));
            for (int y = 0; y < 64; y += 8)
                for (int x = 0; x < 64; x += 8)
                    c.Rect(x + 2, y + 2, 5, 5, C(18, 20, 24));
            c.Noise(new Rng(1202), 10);
            Art.FloorWood = c.T;
        }
        // ceiling light panels
        Art.CeilWood = CeilingTiles(1203, C(80, 84, 92), C(240, 250, 255));
        Art.CeilStone = CeilingTiles(1204, C(60, 64, 70), C(180, 230, 255));
        // alien soil (outdoors)
        {
            var r = new Rng(1205); var c = new Canvas(TS, TS);
            for (int i = 0; i < TS * TS; i++) { int v = r.Range(-16, 16); c.T.Px[i] = C(70 + v, 50 + v, 86 + v); }
            for (int i = 0; i < 12; i++) c.Ellipse(r.Range(0, 64), r.Range(0, 64), r.Range(2, 6), r.Range(1, 3), C(50, 34, 64));
            for (int i = 0; i < 20; i++) c.Circle(r.Range(0, 64), r.Range(0, 64), 1, C(80, 230, 200));
            Art.Grass = c.T;
        }
        // lunar regolith
        {
            var r = new Rng(1206); var c = new Canvas(TS, TS);
            for (int i = 0; i < TS * TS; i++) { int v = r.Range(-12, 12); c.T.Px[i] = C(176 + v, 180 + v, 186 + v); }
            for (int i = 0; i < 6; i++)
            {
                float x = r.Range(0, 64), y = r.Range(0, 64), rad = r.Range(3, 8);
                c.Ellipse(x, y, rad, rad * 0.6f, C(140, 144, 150));
                c.Ellipse(x + 1, y + 1, rad * 0.7f, rad * 0.4f, C(196, 200, 206));
            }
            Art.Snow = c.T;
        }
        Art.PortalFloor = Pad(C(60, 160, 255));
        Art.ExitFloor = Pad(C(255, 90, 60));
        Art.ExitFloorOff = Pad(C(90, 60, 60));
        Art.SpawnFloor = Pad(C(190, 70, 255));
        Art.AltarFloor = Pad(C(255, 210, 60));
        Art.AltarFloorOff = Pad(C(110, 90, 50));
        Art.CheckpointFloor = Pad(C(80, 255, 140));
        Art.CheckpointFloorOff = Pad(C(50, 80, 60));
        Art.LiftFloor = Pad(C(220, 240, 255));
        {
            var t = Art.FloorStone.Clone(); var c = new Canvas(t);
            c.Rect(8, 8, 48, 48, C(30, 32, 36));
            c.Rect(10, 10, 44, 44, C(70, 90, 96));
            c.Rect(10, 10, 44, 2, C(120, 220, 220));
            c.Tri(32, 18, 22, 32, 42, 32, C(120, 255, 230));
            c.Tri(32, 46, 22, 32, 42, 32, C(60, 180, 170));
            Art.PlateFloor = t;
        }

        Art.SkyDusk = Space(2201, (4, 2, 12), (70, 20, 30), (40, 12, 16), C(220, 120, 80), true);
        Art.SkyNight = Space(2205, (2, 8, 8), (20, 70, 50), (10, 24, 20), C(120, 220, 170), false);
        Art.SkyIce = Space(2203, (8, 12, 30), (120, 160, 210), (150, 170, 190), C(200, 230, 255), true);
    }

    static Tex CeilingTiles(uint seed, uint b, uint light)
    {
        var c = new Canvas(TS, TS);
        c.Clear(b);
        c.Rect(0, 0, 64, 2, Col.Shade(b, 150)); c.Rect(0, 0, 2, 64, Col.Shade(b, 150));
        c.Rect(0, 31, 64, 2, Col.Shade(b, 150)); c.Rect(31, 0, 2, 64, Col.Shade(b, 150));
        c.Rect(8, 12, 16, 8, C(40, 44, 50)); c.Rect(9, 13, 14, 6, light);
        c.Rect(40, 44, 16, 8, C(40, 44, 50)); c.Rect(41, 45, 14, 6, light);
        c.Noise(new Rng(seed), 8);
        return c.T;
    }

    static Tex Pad(uint glow)
    {
        var t = Art.FloorStone.Clone(); var c = new Canvas(t);
        c.Rect(4, 4, 56, 56, C(34, 36, 42));
        c.Circle(32, 32, 26, Col.Shade(glow, 90));
        c.Circle(32, 32, 23, C(24, 26, 32));
        c.Circle(32, 32, 19, Col.Shade(glow, 180));
        c.Circle(32, 32, 15, C(20, 22, 28));
        c.Glow(32, 32, 14, glow);
        for (int i = 0; i < 6; i++)
        {
            float a = i * MathF.Tau / 6;
            c.Circle(32 + MathF.Cos(a) * 21, 32 + MathF.Sin(a) * 21, 1.8f, C(255, 255, 255));
        }
        return t;
    }

    /// <summary>Space sky: stars, a nebula glow, a big planet, and a jagged horizon.</summary>
    internal static Tex Space(uint seed, (int r, int g, int b) top, (int r, int g, int b) horizon, (int r, int g, int b) land, uint planet, bool rings)
    {
        var r = new Rng(seed);
        var t = new Tex(256, 128);
        for (int y = 0; y < 128; y++)
        {
            float f = MathF.Pow(y / 127f, 2.2f);
            uint row = C((int)(top.r + (horizon.r - top.r) * f), (int)(top.g + (horizon.g - top.g) * f), (int)(top.b + (horizon.b - top.b) * f));
            for (int x = 0; x < 256; x++) t.Px[y * 256 + x] = row;
        }
        var c = new Canvas(t);
        for (int i = 0; i < 260; i++)
        {
            int x = r.Int(256), y = r.Int(100);
            int v = r.Range(120, 255);
            c.T.Set(x, y, C(v, v, Math.Min(255, v + 20)));
        }
        for (int i = 0; i < 6; i++)
        {
            float x = r.Range(0, 256), y = r.Range(10, 60);
            for (int k = -1; k <= 1; k++) c.Glow((x + k * 256 + 256) % 256, y, r.Range(14, 26), Col.Shade(planet, 70));
        }
        // planet
        float px = 70 + r.Range(0, 40), py = 38, pr = 20;
        c.Circle(px, py, pr, Col.Shade(planet, 150));
        c.Circle(px - 5, py - 5, pr * 0.75f, planet);
        c.Circle(px - 9, py - 9, pr * 0.3f, Col.Lerp(planet, C(255, 255, 255), 120));
        if (rings) c.Line(px - pr * 1.7f, py + 6, px + pr * 1.7f, py - 6, 2, Col.Lerp(planet, C(255, 255, 255), 90));
        // horizon: rocky ground with station spires
        float h = 104;
        for (int x = 0; x < 256; x++)
        {
            h += r.Range(-1.8f, 1.8f);
            h += (104 - h) * 0.05f;
            if (x > 240) h += (104 - h) * 0.2f;
            float top_ = h;
            if (x % 64 is >= 20 and < 24) top_ = h - 14;          // spire
            if (x % 64 is >= 21 and < 23) top_ = h - 20;
            for (int y = (int)top_; y < 128; y++) t.Px[y * 256 + x] = C(land.r, land.g, land.b);
            if (x % 64 == 22) c.Glow(x, top_ - 1, 3, C(255, 80, 60));
        }
        return t;
    }

    // ------------------------------------------------------------------ sprites

    static Tex Item(Action<Canvas> draw, int seed = 7) => Art.Item(draw, seed);

    static void BuildSprites()
    {
        Art.Monsters["ettin"] = Art.PoseSet(DrawBrute);
        Art.Monsters["afrit"] = Art.PoseSet(DrawDrone);
        Art.Monsters["centaur"] = Art.PoseSet((c, p) => DrawStrider(c, p, false));
        Art.Monsters["slaughtaur"] = Art.PoseSet((c, p) => DrawStrider(c, p, true));
        Art.Monsters["bishop"] = Art.PoseSet(DrawWraith);
        Art.Monsters["heresiarch"] = Art.PoseSet(DrawOvermind);

        Art.Torch = new[] { Pylon(0), Pylon(1), Pylon(2) };
        Art.Pillar = Item(c =>
        {
            c.Rect(20, 0, 24, 64, C(80, 86, 96));
            c.Rect(20, 0, 3, 64, C(130, 136, 146));
            c.Rect(30, 0, 4, 64, C(120, 230, 255));
            c.Rect(18, 0, 28, 5, C(60, 64, 72)); c.Rect(18, 59, 28, 5, C(60, 64, 72));
        });
        Art.Tree = Item(c =>
        {
            c.Line(32, 64, 30, 30, 4, C(60, 110, 90));
            c.Line(31, 44, 16, 26, 2, C(60, 110, 90)); c.Line(31, 40, 48, 22, 2, C(60, 110, 90));
            foreach (var (x, y, r) in new[] { (30f, 22f, 7f), (15f, 23f, 5f), (48f, 19f, 5f), (38f, 32f, 4f) })
            {
                c.Glow(x, y, r + 5, C(120, 255, 220));
                c.Circle(x, y, r, C(90, 230, 200));
                c.Circle(x - r * 0.3f, y - r * 0.3f, r * 0.35f, C(220, 255, 250));
            }
        });
        Art.Vial = Item(c =>
        {
            c.Rect(22, 42, 20, 16, C(230, 232, 236));
            c.Rect(30, 44, 4, 12, C(220, 40, 40)); c.Rect(26, 48, 12, 4, C(220, 40, 40));
        });
        Art.Flask = Item(c =>
        {
            c.Rect(14, 34, 36, 26, C(234, 236, 240));
            c.Rect(26, 28, 12, 6, C(90, 90, 96));
            c.Rect(29, 38, 6, 18, C(220, 40, 40)); c.Rect(23, 44, 18, 6, C(220, 40, 40));
            c.Rect(14, 34, 36, 2, C(255, 255, 255));
        });
        Art.Urn = Item(c =>
        {
            c.Glow(32, 44, 18, C(255, 210, 80));
            c.Rect(24, 28, 16, 32, C(210, 170, 60));
            c.Rect(24, 28, 16, 4, C(120, 110, 90)); c.Rect(24, 56, 16, 4, C(120, 110, 90));
            c.Rect(28, 34, 8, 20, C(255, 240, 160));
        });
        Art.BlueMana = Cell(C(60, 140, 255));
        Art.GreenMana = Cell(C(80, 240, 110));
        Art.SteelKey = Keycard(C(60, 140, 255));
        Art.FireKey = Keycard(C(255, 70, 60));
        Art.Armor = Item(c =>
        {
            c.Rect(18, 30, 28, 28, C(70, 110, 70));
            c.Rect(12, 30, 8, 12, C(60, 96, 60)); c.Rect(44, 30, 8, 12, C(60, 96, 60));
            c.Ellipse(32, 30, 7, 3, C(0, 0, 0) & 0x00FFFFFF);
            c.Rect(22, 38, 20, 3, C(200, 200, 90));
            c.Rect(22, 46, 20, 3, C(200, 200, 90));
        });
        Art.WeaponPiece2 = WeaponCrate(C(80, 160, 255));
        Art.WeaponPiece3 = WeaponCrate(C(80, 240, 110));
        Art.ChestClosed = SupplyCrate(false);
        Art.ChestOpen = SupplyCrate(true);
        Art.LoreStone = Terminal(false);
        Art.LoreStoneRead = Terminal(true);
        Art.Relics = BuildArtifacts();
        // jetpack: twin fuel tanks on a harness, nozzles glowing
        Art.Jetpack = Item(c =>
        {
            c.Glow(32, 58, 16, C(90, 190, 255));
            c.Rect(26, 26, 12, 22, C(70, 74, 84));
            foreach (int x in new[] { 18, 38 })
            {
                c.Ellipse(x + 4, 24, 5, 4, C(200, 206, 214));
                c.Rect(x - 1, 24, 10, 26, C(200, 206, 214));
                c.Rect(x + 1, 26, 2, 22, C(240, 244, 250));
                c.Rect(x - 1, 34, 10, 3, C(230, 170, 40));
                c.Tri(x, 50, x + 8, 50, x + 4, 56, C(90, 94, 104));
                c.Tri(x + 1, 56, x + 7, 56, x + 4, 62, C(120, 220, 255));
            }
            c.Rect(28, 30, 8, 4, C(60, 220, 120));
        }, 113);

        // HUD background: dark metal
        var hud = Panel(1301, (52, 56, 64), 16, 32);
        Art.HudBack = hud.T;
    }

    /// <summary>
    /// Six alien artifacts for the sci-fi relics: a data crystal, a quantum core in a cage, a xeno idol,
    /// an ancient probe, a gravity pearl on a plinth and a glyph-covered monolith shard.
    /// </summary>
    static Tex[] BuildArtifacts() => new[]
    {
        // data crystal: a faceted prism with scrolling light inside
        Item(c =>
        {
            c.Glow(32, 42, 20, C(90, 220, 255));
            c.Tri(32, 18, 22, 40, 42, 40, C(120, 220, 255));
            c.Tri(22, 40, 42, 40, 32, 60, C(60, 150, 220));
            c.Tri(32, 18, 32, 40, 42, 40, C(80, 180, 240));
            for (int y = 28; y < 52; y += 5) c.Rect(29, y, 6, 1, C(230, 255, 255));
            c.Rect(24, 58, 16, 4, C(70, 74, 82));
        }, 91),
        // quantum core: a glowing sphere held in a metal cage
        Item(c =>
        {
            c.Glow(32, 42, 22, C(255, 170, 60));
            c.Circle(32, 42, 9, C(255, 200, 90));
            c.Circle(30, 40, 4, C(255, 250, 210));
            c.Rect(20, 28, 24, 3, C(110, 116, 126)); c.Rect(20, 54, 24, 4, C(110, 116, 126));
            foreach (int x in new[] { 20, 30, 41 }) c.Rect(x, 28, 3, 30, C(90, 96, 106));
        }, 93),
        // xeno idol: an elongated alien head carved from dark stone, eyes lit
        Item(c =>
        {
            c.Glow(32, 40, 18, C(170, 90, 255));
            c.Ellipse(32, 36, 11, 15, C(60, 50, 80));
            c.Ellipse(32, 28, 8, 7, C(80, 66, 104));
            c.Ellipse(27, 38, 3, 2, C(200, 140, 255)); c.Ellipse(37, 38, 3, 2, C(200, 140, 255));
            c.Rect(29, 46, 6, 2, C(40, 30, 56));
            c.Rect(22, 52, 20, 8, C(70, 74, 82));
        }, 95),
        // ancient probe: a scorched pod with an antenna and a blinking lamp
        Item(c =>
        {
            c.Ellipse(32, 46, 14, 10, C(140, 120, 100));
            c.Ellipse(32, 43, 12, 6, C(170, 150, 120));
            c.Rect(18, 46, 28, 2, C(90, 70, 50));
            c.Line(40, 38, 50, 22, 1, C(200, 200, 210));
            c.Glow(50, 21, 6, C(255, 80, 60));
            c.Circle(26, 44, 3, C(90, 220, 255));
            c.Line(14, 52, 8, 60, 2, C(110, 100, 90)); c.Line(50, 52, 56, 60, 2, C(110, 100, 90));
        }, 97),
        // gravity pearl: a dark orb floating above a plinth, ringed by light
        Item(c =>
        {
            c.Rect(24, 54, 16, 8, C(80, 86, 96));
            c.Rect(22, 52, 20, 3, C(120, 126, 136));
            c.Glow(32, 50, 10, C(120, 255, 200));
            c.Circle(32, 36, 8, C(24, 30, 40));
            c.Circle(30, 34, 3, C(120, 140, 170));
            c.Ellipse(32, 36, 15, 3, C(120, 255, 200));
            c.Ellipse(32, 36, 12, 2, C(24, 30, 40));
            c.Circle(32, 36, 8, C(24, 30, 40));
            c.Circle(30, 34, 3, C(120, 140, 170));
        }, 99),
        // monolith shard: a black slab etched with glowing glyphs
        Item(c =>
        {
            c.Glow(32, 42, 18, C(90, 255, 140));
            c.Tri(24, 60, 40, 60, 38, 22, C(30, 34, 36));
            c.Tri(24, 60, 26, 26, 38, 22, C(40, 46, 48));
            for (int i = 0; i < 5; i++) c.Rect(28 + (i % 2) * 2, 30 + i * 6, 5 - (i % 3), 2, C(120, 255, 160));
        }, 101),
    };

    static Tex Pylon(int frame)
    {
        var c = new Canvas(TS, TS);
        c.Rect(26, 56, 12, 8, C(70, 74, 80));
        c.Rect(29, 10, 6, 48, C(60, 64, 70));
        c.Glow(32, 30, 14 + frame, C(120, 230, 255));
        c.Rect(30, 12 + frame, 4, 38, C(200, 250, 255));
        c.Rect(26, 8, 12, 4, C(70, 74, 80));
        return c.T;
    }

    static Tex Cell(uint col) => Item(c =>
    {
        c.Glow(32, 46, 16, col);
        c.Rect(24, 30, 16, 28, C(50, 54, 62));
        c.Rect(26, 34, 12, 20, col);
        c.Rect(28, 26, 8, 4, C(170, 174, 180));
        c.Rect(26, 34, 3, 20, Col.Lerp(col, C(255, 255, 255), 140));
    });

    static Tex Keycard(uint col) => Item(c =>
    {
        c.Glow(32, 46, 14, col);
        c.Rect(18, 38, 28, 18, col);
        c.Rect(20, 44, 24, 3, C(20, 20, 24));
        c.Rect(36, 40, 7, 3, C(240, 220, 120));
    });

    static Tex WeaponCrate(uint glow) => Item(c =>
    {
        c.Glow(32, 44, 18, glow);
        c.Rect(12, 38, 40, 20, C(70, 76, 84));
        c.Rect(12, 38, 40, 3, C(120, 126, 134));
        c.Rect(18, 44, 28, 6, C(30, 32, 36));
        c.Rect(20, 45, 18, 4, glow);
    });

    static Tex SupplyCrate(bool open)
    {
        var c = new Canvas(64, 48);
        c.Rect(4, 18, 56, 29, C(96, 100, 110));
        c.Rect(4, 18, 56, 2, C(150, 154, 164));
        c.Rect(4, 30, 56, 6, C(230, 130, 30));
        if (open)
        {
            c.Rect(6, 4, 52, 8, C(70, 74, 82));
            c.Rect(6, 14, 52, 5, C(20, 22, 26));
            c.Glow(32, 16, 18, C(120, 220, 255));
            for (int i = 0; i < 5; i++) c.Rect(12 + i * 9, 12, 5, 6, i % 2 == 0 ? C(80, 160, 255) : C(80, 240, 110));
        }
        else
        {
            c.Rect(2, 12, 60, 8, C(80, 84, 92));
            c.Rect(26, 14, 12, 8, C(40, 42, 48));
            c.Circle(32, 18, 2, C(255, 70, 60));
        }
        c.Noise(new Rng(open ? 1311u : 1312u), 10);
        c.Outline(Dark);
        return c.T;
    }

    static Tex Terminal(bool read)
    {
        var c = new Canvas(TS, TS);
        c.Rect(26, 40, 12, 22, C(60, 64, 72));
        c.Rect(18, 60, 28, 4, C(50, 54, 60));
        c.Rect(12, 12, 40, 30, C(40, 44, 52));
        uint screen = read ? C(40, 70, 70) : C(20, 60, 64);
        c.Rect(15, 15, 34, 24, screen);
        uint text = read ? C(90, 150, 140) : C(120, 255, 200);
        if (!read) c.Glow(32, 27, 20, C(80, 220, 200));
        for (int i = 0; i < 5; i++) c.Rect(18, 18 + i * 4, 28 - (i % 3) * 7, 2, text);
        c.Noise(new Rng(read ? 1321u : 1320u), 8);
        c.Outline(Dark);
        return c.T;
    }

    // monsters: robots, drones, walkers, a psychic wraith and the Overmind

    static void DrawBrute(Canvas c, Pose p)
    {
        uint hull = p == Pose.Pain ? C(210, 200, 190) : C(110, 116, 124), dark = C(60, 64, 70), glow = C(255, 60, 40);
        int step = p == Pose.Walk1 ? 3 : p == Pose.Walk0 ? -3 : 0;
        c.Rect(20 + step, 42, 10, 18, dark); c.Rect(34 - step, 42, 10, 18, dark);
        c.Rect(17 + step, 58, 15, 6, hull); c.Rect(32 - step, 58, 15, 6, hull);
        c.Rect(14, 20, 36, 26, hull);
        c.Rect(14, 20, 36, 3, C(170, 176, 184));
        c.Rect(22, 30, 20, 8, dark);
        c.Glow(32, 34, 6, C(255, 150, 60));
        // twin sensor heads
        c.Rect(18, 10, 12, 10, hull); c.Rect(34, 10, 12, 10, hull);
        c.Rect(20, 13, 8, 3, glow); c.Rect(36, 13, 8, 3, glow);
        c.Line(12, 24, 6, 42, 7, dark);
        if (p == Pose.Attack)
        {
            c.Line(52, 24, 58, 8, 7, dark);
            c.Rect(52, 0, 11, 10, hull);
            c.Glow(57, 5, 8, C(255, 200, 80));
        }
        else
        {
            c.Line(52, 24, 58, 42, 7, dark);
            c.Rect(52, 42, 11, 12, hull);
        }
    }

    static void DrawDrone(Canvas c, Pose p)
    {
        uint hull = p == Pose.Pain ? C(240, 220, 200) : C(150, 156, 166), eye = C(255, 90, 40);
        float tilt = p == Pose.Walk1 ? 3 : p == Pose.Walk0 ? -3 : 0;
        c.Glow(32, 52, 12, C(120, 200, 255));
        c.Ellipse(32, 34 + tilt * 0.3f, 24, 9, C(80, 86, 96));
        c.Ellipse(32, 31 + tilt * 0.3f, 22, 7, hull);
        c.Rect(8, 30, 6, 10, C(70, 74, 82)); c.Rect(50, 30, 6, 10, C(70, 74, 82));
        c.Glow(11, 44, 6, C(120, 220, 255)); c.Glow(53, 44, 6, C(120, 220, 255));
        c.Circle(32, 34, 7, C(30, 30, 36));
        c.Circle(32, 34, p == Pose.Attack ? 6 : 4, eye);
        if (p == Pose.Attack) c.Glow(32, 34, 14, C(255, 200, 100));
        c.Line(32, 24, 32 + tilt, 12, 1, C(200, 200, 210));
        c.Circle(32 + tilt, 11, 2, C(255, 60, 60));
    }

    static void DrawStrider(Canvas c, Pose p, bool siege)
    {
        uint hull = siege ? C(110, 60, 60) : C(120, 126, 110), dark = C(50, 54, 58);
        if (p == Pose.Pain) hull = Col.Shade(hull, 380);
        int step = p == Pose.Walk1 ? 3 : p == Pose.Walk0 ? -3 : 0;
        for (int i = 0; i < 4; i++)
        {
            int lx = 12 + i * 12 + ((i % 2 == 0) ? step : -step);
            c.Line(lx + 2, 40, lx - 2, 52, 4, dark);
            c.Line(lx - 2, 52, lx + 2, 63, 4, dark);
        }
        c.Ellipse(32, 36, 24, 9, hull);
        c.Rect(18, 30, 28, 4, Col.Shade(hull, 150));
        // cockpit / turret
        c.Rect(22, 14, 20, 16, hull);
        c.Rect(26, 18, 12, 5, C(90, 220, 255));
        if (siege)
        {
            c.Rect(40, 18, 20, 6, dark);
            if (p == Pose.Attack) c.Glow(60, 21, 9, C(255, 60, 40));
        }
        else if (p == Pose.Attack) c.Line(42, 22, 60, 6, 3, C(200, 230, 255));
        else c.Line(42, 26, 56, 38, 3, C(200, 230, 255));
        c.Glow(32, 44, 5, C(255, 140, 60));
    }

    static void DrawWraith(Canvas c, Pose p)
    {
        uint robe = p == Pose.Pain ? C(200, 170, 255) : C(70, 50, 120), glow = C(180, 120, 255);
        float bob = p == Pose.Walk1 ? -2 : p == Pose.Walk0 ? 1 : 0;
        c.Glow(32, 36, 26, glow);
        c.Tri(32, 18 + bob, 12, 54 + bob, 52, 54 + bob, robe);
        c.Tri(20, 54 + bob, 44, 54 + bob, 32, 63, Col.Shade(robe, 170));
        c.Rect(30, 22 + (int)bob, 4, 30, C(120, 230, 255));
        // bulbous alien head
        c.Ellipse(32, 12 + bob, 10, 11, C(160, 190, 170));
        c.Ellipse(32, 8 + bob, 7, 5, C(200, 150, 210));
        c.Ellipse(27, 14 + bob, 3, 2, C(20, 10, 30)); c.Ellipse(37, 14 + bob, 3, 2, C(20, 10, 30));
        c.Circle(27, 14 + bob, 1, glow); c.Circle(37, 14 + bob, 1, glow);
        if (p == Pose.Attack)
        {
            c.Line(20, 28, 6, 18, 4, robe); c.Line(44, 28, 58, 18, 4, robe);
            c.Glow(6, 18, 9, C(140, 255, 180)); c.Glow(58, 18, 9, C(140, 255, 180));
        }
        else { c.Line(20, 28, 14, 42, 4, robe); c.Line(44, 28, 50, 42, 4, robe); }
    }

    static void DrawOvermind(Canvas c, Pose p)
    {
        uint brain = p == Pose.Pain ? C(255, 200, 220) : C(210, 130, 170), dome = C(150, 220, 255);
        float sway = p == Pose.Walk1 ? 2 : p == Pose.Walk0 ? -2 : 0;
        c.Glow(32, 30, 30, C(200, 80, 220));
        // hover base and cables
        c.Rect(14, 48, 36, 10, C(70, 74, 84));
        c.Rect(14, 48, 36, 2, C(130, 136, 146));
        c.Glow(32, 60, 10, C(120, 220, 255));
        c.Line(20, 50, 8 + sway, 62, 3, C(60, 50, 70)); c.Line(44, 50, 56 - sway, 62, 3, C(60, 50, 70));
        // dome and brain
        c.Ellipse(32 + sway, 28, 20, 20, Col.Shade(dome, 120));
        c.Ellipse(32 + sway, 28, 18, 18, C(30, 40, 60));
        c.Ellipse(32 + sway, 30, 15, 13, brain);
        for (int i = 0; i < 5; i++) c.Line(20 + sway + i * 6, 22, 22 + sway + i * 6, 38, 1, Col.Shade(brain, 150));
        c.Circle(26 + sway, 20, 3, C(255, 255, 255));
        if (p == Pose.Attack)
        {
            c.Glow(8, 20, 9, C(255, 120, 255)); c.Glow(56, 20, 9, C(255, 120, 255));
            c.Line(16, 30, 8, 20, 2, C(255, 160, 255)); c.Line(48, 30, 56, 20, 2, C(255, 160, 255));
        }
        // three orbiting defence cores
        c.Rect(4, 40, 6, 6, C(255, 80, 80)); c.Rect(54, 40, 6, 6, C(80, 255, 120)); c.Rect(29, 2, 6, 6, C(80, 150, 255));
    }

    // ------------------------------------------------------------------ first-person weapons

    const int WW = 128, WH = 80;

    static void BuildWeapons()
    {
        for (int cls = 0; cls < 3; cls++)
            for (int slot = 0; slot < 3; slot++)
            {
                var frames = new Tex[2];
                for (int f = 0; f < 2; f++)
                {
                    var c = new Canvas(WW, WH);
                    DrawWeapon(c, cls, slot, f == 1);
                    c.Noise(new Rng((uint)(cls * 10 + slot + f + 500)), 8);
                    c.Outline(Dark);
                    frames[f] = c.T;
                }
                Art.Weapons[cls * 3 + slot] = frames;
            }
    }

    static void Gun(Canvas c, int x, int y, int len, int thick, uint body, uint accent)
    {
        c.Rect(x, y, thick + 8, 26, body);                        // grip / body
        c.Rect(x + 2, y - len, thick, len, Col.Shade(body, 130)); // barrel pointing up the screen
        c.Rect(x + 2, y - len, thick, 3, accent);
        c.Rect(x + thick + 6, y + 4, 4, 12, accent);
    }

    static void DrawWeapon(Canvas c, int cls, int slot, bool fire)
    {
        uint armour = cls == 0 ? C(70, 100, 70) : cls == 1 ? C(200, 120, 40) : C(90, 70, 150);
        uint glove = C(50, 54, 60), metal = C(110, 116, 126);
        int lift = fire ? -14 : 0;
        switch (cls * 3 + slot)
        {
            case 0: // power fist
                {
                    float fx = fire ? 64 : 84, fy = fire ? 30 : 56;
                    c.Line(112, 80, fx, fy, 18, armour);
                    c.Rect((int)fx - 12, (int)fy - 10, 24, 20, metal);
                    c.Glow(fx, fy - 8, fire ? 18 : 8, C(120, 200, 255));
                    c.Line(16, 80, 34, 60, 18, armour); c.Rect(24, 50, 22, 18, metal);
                    break;
                }
            case 1: // vibro blade
                {
                    float hx = fire ? 30 : 76, hy = fire ? 18 : 26;
                    c.Line(98, 80, hx + 8, hy + 16, 6, glove);
                    c.Line(hx + 8, hy + 16, hx - 4, hy - 16, 5, C(200, 240, 255));
                    c.Glow(hx + 2, hy, fire ? 20 : 12, C(120, 220, 255));
                    c.Line(122, 80, 100, 72, 14, armour);
                    break;
                }
            case 2: // grav launcher
                {
                    Gun(c, 52, 58 + lift / 2, 30, 16, metal, C(200, 120, 255));
                    c.Glow(62, 34 + lift / 2, fire ? 22 : 10, C(200, 120, 255));
                    c.Line(118, 80, 76, 72 + lift / 2, 14, armour);
                    break;
                }
            case 3: // shock baton
                {
                    float hx = fire ? 40 : 78, hy = fire ? 20 : 30;
                    c.Line(100, 80, hx, hy, 6, C(60, 60, 66));
                    c.Rect((int)hx - 3, (int)hy - 6, 7, 10, C(120, 200, 255));
                    c.Glow(hx, hy - 2, fire ? 20 : 10, C(120, 200, 255));
                    c.Line(124, 80, 102, 74, 14, armour);
                    break;
                }
            case 4: // bio rifle
                {
                    Gun(c, 54, 58 + lift / 2, 28, 10, C(80, 90, 80), C(120, 255, 120));
                    c.Rect(70, 58 + lift / 2, 12, 16, C(60, 200, 80));
                    if (fire) c.Glow(59, 28, 18, C(120, 255, 120));
                    c.Line(118, 80, 82, 74 + lift / 2, 14, armour);
                    break;
                }
            case 5: // flamer
                {
                    Gun(c, 52, 58 + lift / 2, 26, 12, C(100, 90, 80), C(255, 150, 40));
                    c.Rect(68, 56 + lift / 2, 14, 20, C(200, 80, 40));
                    c.Glow(58, 30 + lift / 2, fire ? 26 : 6, C(255, 140, 40));
                    c.Line(118, 80, 82, 74 + lift / 2, 14, armour);
                    break;
                }
            case 6: // blaster
                {
                    Gun(c, 58, 60 + lift / 2, 18, 8, metal, C(80, 160, 255));
                    if (fire) c.Glow(62, 40 + lift / 2, 16, C(90, 160, 255));
                    c.Line(116, 80, 76, 74 + lift / 2, 13, armour);
                    break;
                }
            case 7: // shard gun (three barrels)
                {
                    Gun(c, 50, 60 + lift / 2, 22, 6, metal, C(160, 230, 255));
                    Gun(c, 60, 60 + lift / 2, 26, 6, metal, C(160, 230, 255));
                    Gun(c, 70, 60 + lift / 2, 22, 6, metal, C(160, 230, 255));
                    if (fire) c.Glow(64, 36 + lift / 2, 22, C(160, 230, 255));
                    c.Line(118, 80, 86, 74 + lift / 2, 13, armour);
                    break;
                }
            default: // arc rifle with coils
                {
                    Gun(c, 54, 60 + lift / 2, 30, 12, metal, C(170, 180, 255));
                    for (int i = 0; i < 4; i++) c.Rect(54, 36 + i * 6 + lift / 2, 20, 2, C(200, 120, 60));
                    var r = new Rng(fire ? 3u : 9u);
                    float x = 62, y = 30 + lift / 2;
                    for (int i = 0; i < 5; i++) { float ny = y - 6, nx = x + r.Range(-5, 5); c.Line(x, y, nx, ny, fire ? 2 : 1, C(220, 230, 255)); x = nx; y = ny; }
                    c.Glow(62, 26 + lift / 2, fire ? 22 : 8, C(170, 180, 255));
                    c.Line(118, 80, 82, 74 + lift / 2, 13, armour);
                    break;
                }
        }
    }
}
