namespace HexenSharp;

public enum Pose { Walk0, Walk1, Attack, Pain, Die0, Die1, Dead }

/// <summary>Visual style: a space station (default) or the original dark-fantasy look.</summary>
public enum ArtStyle { SciFi, Fantasy }

/// <summary>All textures and sprites, generated procedurally at startup (no asset files needed).</summary>
public static class Art
{
    public const int TS = 64; // wall/floor texture size

    // Walls
    public static Tex Stone, Brick, Wood, Moss, Ice, Door, SteelDoor, FireDoor, Portcullis, LeverOff, LeverOn, Marble, Block, StepRiser, Rubble;
    /// <summary>Rubble at each crack stage (Level.RubbleStages), and a chip of it for the debris when it breaks.</summary>
    public static Tex[] RubbleCracked;
    public static Tex RubbleChunk;
    /// <summary>Ore blocks by Level.OreGlyphs index, each with its crack stages.</summary>
    public static Tex[] Ores;
    public static Tex[][] OreCracked;
    /// <summary>The stranded ship: wrecked, half repaired, ready to fly.</summary>
    public static Tex[] Ship;
    /// <summary>The barren planet: dusty ground, layered cliffs and its sky.</summary>
    public static Tex Dust, Cliff, SkyBarren;
    /// <summary>Open space for flight maps: a tiling starfield, the sky with the next world ahead, and asteroid spin frames.</summary>
    public static Tex Starfield, SkyVoid;
    /// <summary>The green moon's turf.</summary>
    public static Tex Meadow;
    public static Tex[] Asteroid;
    /// <summary>Story mode: the townsfolk you question, evidence markers, and Neon Harbor's streets, fronts and skyline.</summary>
    public static Tex[] People, ClueMark;
    public static Tex ClueFound, Street, Facade, SkyCity;
    /// <summary>The arena's arsenal upgrade: a pulsing power core in a spinning ring.</summary>
    public static Tex[] Upgrade;
    // Flats
    public static Tex FloorStone, FloorWood, Grass, Snow, CeilWood, CeilStone, PortalFloor, ExitFloor, ExitFloorOff, SpawnFloor, AltarFloor, AltarFloorOff, PlateFloor,
        CheckpointFloor, CheckpointFloorOff, LiftFloor;
    public static Tex SkyDusk, SkyIce, SkyNight;
    // Sprites
    public static readonly Dictionary<string, Tex[]> Monsters = new();
    public static Tex[] Fireball, Bolt, Shard, Serpent, Flame, Lightning, Hammer, BossBall, CentaurBolt, Seeker, Rocket, Grenade;
    /// <summary>A rocket's smoke trail puff.</summary>
    public static Tex Smoke;
    public static Tex[] Torch, Relics;
    /// <summary>The practice ghost: a hologram runner (sci-fi) or a pale spectre (fantasy), two strides.</summary>
    public static Tex[] Ghost;
    public static Tex Pillar, Vial, Flask, Urn, BlueMana, GreenMana, SteelKey, FireKey, Armor, ChestClosed, ChestOpen, LoreStone, LoreStoneRead, WeaponPiece2, WeaponPiece3, Tree, Crystal, Jetpack;
    // HUD
    public static Tex HudBack;
    // First-person weapons, indexed [class*3 + slot][frame], then the rocket launcher (9)
    public static Tex[][] Weapons = new Tex[12][];
    public const int RocketLauncherArt = 9, RailgunArt = 10, GrenadeLauncherArt = 11;
    /// <summary>The railgun's beam: its white core and the blue spiral round it.</summary>
    public static Tex RailCore, RailSpiral;

    public static ArtStyle Style = ArtStyle.SciFi;
    /// <summary>Bumped whenever the art is rebuilt, so caches (the editor's icons) know to refresh.</summary>
    public static int Version;
    /// <summary>Use the Blender-rendered art pack over the sci-fi art where it has a replacement.</summary>
    public static bool Rendered;
    public static Tex[] PillarFrames, TreeFrames;

    /// <summary>Builds every texture and sprite. The fantasy art is always built first; the sci-fi style then
    /// replaces it, so every slot is always filled.</summary>
    public static void Init(ArtStyle style = ArtStyle.SciFi)
    {
        Style = style;
        Monsters.Clear();
        BuildWalls();
        BuildFlats();
        BuildSprites();
        BuildWeapons();
        if (style == ArtStyle.SciFi) SciFiArt.Apply();
        if (style == ArtStyle.SciFi && Rendered) RenderedArt.Apply();
        MiniBosses.BuildArt(); // from whichever monsters the style has
        ShootingRange.BuildArt();
        RubbleCracked = CrackStages(Rubble);
        RubbleChunk = Chunk(Rubble);
        Ores = OreColors.Select((c, k) => Veins(Rubble, c, 300 + (uint)k)).ToArray();
        OreCracked = Ores.Select(CrackStages).ToArray();
        Ship = Enumerable.Range(0, 3).Select(s => BuildShip(s, style == ArtStyle.SciFi)).ToArray();
        BuildBarren(style == ArtStyle.SciFi);
        BuildVoid(style == ArtStyle.SciFi);
        BuildTown();
        Upgrade = Enumerable.Range(0, 4).Select(UpgradeFrame).ToArray();
        WeaponMods.BuildArt(); // from the upgrade's emblem
        PillarFrames = new[] { Pillar };
        TreeFrames = new[] { Tree };
        Version++;
    }

    public static Tex PickupSprite(PickupKind k, int variant) => k switch
    {
        PickupKind.Vial => Vial,
        PickupKind.Flask => Flask,
        PickupKind.Urn => Urn,
        PickupKind.BlueMana => BlueMana,
        PickupKind.GreenMana => GreenMana,
        PickupKind.SteelKey => SteelKey,
        PickupKind.FireKey => FireKey,
        PickupKind.Armor => Armor,
        PickupKind.Weapon2 => WeaponPiece2,
        PickupKind.Weapon3 => WeaponPiece3,
        PickupKind.Jetpack => Jetpack,
        PickupKind.Upgrade => Upgrade[0],
        PickupKind.Arms => Weapons[variant % Weapons.Length][0], // the range's rack: the weapon itself
        _ => Relics[variant % Relics.Length],
    };

    public static Tex[] DecorFrames(char kind) => kind switch { 't' => Torch, 'T' => TreeFrames, _ => PillarFrames };

    // ------------------------------------------------------------------ walls

    /// <summary>Tileable cobblestone: rounded stones (a wrapped Voronoi pattern) lit from the top left, in dark grit.</summary>
    internal static Tex Cobble(uint seed, (int r, int g, int b) baseCol, int varAmt, (int r, int g, int b) gap, int stones = 13)
    {
        var rng = new Rng(seed);
        var pts = new (float x, float y, uint col)[stones];
        for (int i = 0; i < stones; i++)
        {
            int v = rng.Range(-varAmt, varAmt), warm = rng.Range(-6, 6);
            pts[i] = (rng.Range(0f, TS), rng.Range(0f, TS), Col.Rgb(baseCol.r + v + warm, baseCol.g + v, baseCol.b + v - warm));
        }
        static float Wrap(float d) => d > TS / 2f ? d - TS : d < -TS / 2f ? d + TS : d;
        var c = new Canvas(TS, TS);
        for (int y = 0; y < TS; y++)
            for (int x = 0; x < TS; x++)
            {
                float d1 = float.MaxValue, d2 = float.MaxValue, ox = 0, oy = 0;
                int best = 0;
                for (int i = 0; i < stones; i++)
                {
                    float dx = Wrap(x + 0.5f - pts[i].x), dy = Wrap(y + 0.5f - pts[i].y);
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d < d1) { d2 = d1; d1 = d; best = i; ox = dx; oy = dy; }
                    else if (d < d2) d2 = d;
                }
                float edge = (d2 - d1) * 0.5f;
                if (edge < 1.1f) { c.T.Set(x, y, Col.Rgb(gap.r, gap.g, gap.b)); continue; }
                // rounded: brighter toward the top-left of each stone, darker toward its lower-right rim
                int s = 256 - (int)Math.Clamp((ox + oy) * 3.2f, -70, 80);
                if (edge < 2.4f) s = s * 200 / 256;
                c.T.Set(x, y, Col.Shade(pts[best].col, s));
            }
        c.Noise(rng, 22);
        return c.T;
    }

    /// <summary>A texture cracking up in stages: stage 0 is whole, each later stage adds to the last one's cracks.</summary>
    static Tex[] CrackStages(Tex whole)
    {
        var stages = new Tex[Level.RubbleStages];
        stages[0] = whole;
        long r = 0, g = 0, b = 0;
        foreach (uint px in whole.Px) { r += Col.R(px); g += Col.G(px); b += Col.B(px); }
        int n = whole.Px.Length;
        uint avg = Col.Rgb((int)(r / n), (int)(g / n), (int)(b / n));
        uint dark = Col.Shade(avg, 50), lip = Col.Shade(avg, 360);
        var rng = new Rng(97);
        var t = whole;
        for (int s = 1; s < stages.Length; s++)
        {
            t = t.Clone();
            var c = new Canvas(t);
            for (int k = 0; k < 1 + s * 2; k++)
            {
                float x = rng.Range(14f, 50f), y = rng.Range(14f, 50f), a = rng.Range(0f, MathF.Tau);
                for (int seg = 0; seg < 3 + s; seg++)
                {
                    a += rng.Range(-0.9f, 0.9f);
                    float len = rng.Range(4f, 8f), nx = x + MathF.Cos(a) * len, ny = y + MathF.Sin(a) * len;
                    c.Line(x + 1, y + 1, nx + 1, ny + 1, 1, lip);
                    c.Line(x, y, nx, ny, s == stages.Length - 1 ? 2 : 1, dark);
                    x = nx; y = ny;
                }
            }
            stages[s] = t;
        }
        return stages;
    }

    /// <summary>Ore vein colours: iron, crystal, fuel.</summary>
    static readonly (int r, int g, int b)[] OreColors = { (200, 128, 78), (176, 112, 255), (120, 255, 96) };

    /// <summary>A rock texture studded with clusters of ore: dark-rimmed nuggets with a bright glint (crystal and fuel glow).</summary>
    static Tex Veins(Tex rock, (int r, int g, int b) ore, uint seed)
    {
        var t = rock.Clone();
        var c = new Canvas(t);
        var rng = new Rng(seed);
        uint col = Col.Rgb(ore.r, ore.g, ore.b), rim = Col.Shade(col, 90), glint = Col.Lerp(col, Col.Rgb(255, 255, 255), 170);
        bool glows = ore.g > 200 || ore.b > 200;
        for (int k = 0; k < 5; k++)
        {
            float cx = rng.Range(8f, 56f), cy = rng.Range(8f, 56f);
            if (glows) c.Glow(cx, cy, 11, Col.Shade(col, 200));
            for (int n = 0; n < 5; n++)
            {
                float x = cx + rng.Range(-6f, 6f), y = cy + rng.Range(-6f, 6f), s = rng.Range(1.8f, 3.2f);
                c.Tri(x - s - 1, y + s + 1, x + s + 1, y + s + 1, x, y - s - 2, rim);
                c.Tri(x - s, y + s, x + s, y + s, x, y - s - 1, col);
                c.T.Set((int)x, (int)(y - s + 1), glint);
            }
        }
        return t;
    }

    /// <summary>
    /// The stranded ship, side on: a sleek shuttle in sci-fi, a brass-trimmed skyship with a sail in fantasy.
    /// Stage 0 is the wreck (nose in the dust, holed, fin torn off, smoking), 1 patched up, 2 fixed with its engine lit.
    /// </summary>
    static Tex BuildShip(int stage, bool scifi)
    {
        var c = new Canvas(96, 64);
        uint hull = scifi ? Col.Rgb(206, 210, 218) : Col.Rgb(134, 88, 46), trim = scifi ? Col.Rgb(232, 122, 40) : Col.Rgb(214, 172, 70);
        uint dark = scifi ? Col.Rgb(52, 56, 64) : Col.Rgb(56, 34, 18), glass = scifi ? Col.Rgb(80, 180, 255) : Col.Rgb(255, 196, 110);
        int sink = stage == 0 ? 12 : 0;
        float cy = 44 + sink;
        if (!scifi)
        {
            c.Rect(46, 8 + sink, 3, 26, dark);                                                  // mast
            if (stage > 0) c.Tri(49, 10 + sink, 49, 32 + sink, 72, 30 + sink, Col.Rgb(222, 212, 186));
            else c.Tri(49, 18 + sink, 49, 30 + sink, 60, 30 + sink, Col.Rgb(150, 140, 120)); // torn sail
        }
        // legs, then the hull
        c.Line(30, cy + 8, 24, 63, 3, dark);
        if (stage > 0) c.Line(66, cy + 8, 72, 63, 3, dark);
        c.Ellipse(46, cy, 34, 11, hull);
        c.Tri(76, cy - 8, 76, cy + 8, 94, cy + 2, hull);                                      // nose
        c.Rect(12, (int)cy - 3, 64, 3, trim);
        c.Ellipse(66, cy - 5, 8, 4, stage == 2 ? glass : Col.Shade(glass, 110));              // cockpit
        c.Rect(6, (int)cy - 5, 8, 10, dark);                                                   // engine
        if (stage > 0) c.Tri(12, cy - 8, 26, cy - 8, 14, cy - 24, trim);                      // tail fin
        if (stage == 0)
        {
            var dust = scifi ? Col.Rgb(150, 82, 54) : Col.Rgb(118, 110, 100);
            c.Ellipse(52, 63, 40, 7, dust);                                                     // ploughed into the ground
            c.Ellipse(52, 60, 30, 3, Col.Shade(dust, 290));
            foreach (var (x, y, r) in new[] { (34f, cy + 2, 5f), (54f, cy - 4, 4f), (22f, cy - 2, 3f) }) c.Ellipse(x, y, r, r * 0.8f, Col.Rgb(20, 16, 14));
            for (int i = 0; i < 4; i++) c.Circle(30 + i * 6, cy - 16 - i * 7, 5 + i * 1.5f, Col.Rgb(90 - i * 8, 86 - i * 8, 84 - i * 8));
            c.Line(62, cy - 8, 70, cy - 3, 1, Col.Rgb(20, 20, 24));                            // cracked canopy
        }
        if (stage == 1)
            foreach (var (x, y) in new[] { (32, (int)cy), (52, (int)cy - 6), (20, (int)cy - 3) })
            {
                c.Rect(x - 4, y - 3, 8, 6, Col.Shade(hull, 170));
                c.T.Set(x - 3, y - 2, Col.Shade(hull, 280)); c.T.Set(x + 2, y + 1, Col.Shade(hull, 280));
            }
        if (stage == 2)
        {
            c.Glow(4, cy, 9, scifi ? Col.Rgb(120, 220, 255) : Col.Rgb(255, 170, 60));
            c.Glow(66, cy - 5, 6, glass);
        }
        c.Outline(Dark);
        return c.T;
    }

    /// <summary>The barren planet: ash and scree in fantasy, rust-red regolith in sci-fi.</summary>
    static void BuildBarren(bool scifi)
    {
        var ground = scifi ? (r: 150, g: 82, b: 54) : (r: 118, g: 110, b: 100);
        {
            var r = new Rng(1301); var c = new Canvas(TS, TS);
            for (int i = 0; i < TS * TS; i++) { int v = r.Range(-14, 14); c.T.Px[i] = Col.Rgb(ground.r + v, ground.g + v, ground.b + v); }
            for (int i = 0; i < 5; i++)
            {
                float x = r.Range(0f, 64f), y = r.Range(0f, 64f), rad = r.Range(3f, 7f);
                c.Ellipse(x, y, rad, rad * 0.6f, Col.Rgb(ground.r - 34, ground.g - 30, ground.b - 26));
                c.Ellipse(x + 1, y + 1, rad * 0.7f, rad * 0.4f, Col.Rgb(ground.r + 16, ground.g + 12, ground.b + 10));
            }
            for (int i = 0; i < 26; i++) c.Circle(r.Range(0f, 64f), r.Range(0f, 64f), r.Range(0.6f, 1.4f), Col.Rgb(ground.r - 50, ground.g - 46, ground.b - 40));
            Dust = c.T;
        }
        {
            // layered cliff: strata of slightly different rock, split by dark cracks
            var rock = scifi ? (r: 122, g: 68, b: 46) : (r: 108, g: 98, b: 86);
            var r = new Rng(1303); var c = new Canvas(TS, TS);
            int y0 = 0;
            while (y0 < TS)
            {
                int h = r.Range(5, 11), v = r.Range(-16, 16);
                for (int y = y0; y < Math.Min(TS, y0 + h); y++)
                    for (int x = 0; x < TS; x++)
                        c.T.Set(x, y, Col.Rgb(rock.r + v, rock.g + v, rock.b + v));
                c.Rect(0, y0, TS, 1, Col.Rgb(rock.r - 44, rock.g - 40, rock.b - 34));
                y0 += h;
            }
            for (int i = 0; i < 8; i++) { int x = r.Int(TS), y = r.Int(TS); c.Line(x, y, x + r.Range(-3, 3), y + r.Range(6, 14), 1, Col.Rgb(rock.r - 50, rock.g - 46, rock.b - 40)); }
            c.Noise(r, 18);
            Cliff = c.T;
        }
        SkyBarren = scifi
            ? SciFiArt.Space(2207, (18, 6, 4), (196, 96, 52), (74, 32, 22), Col.Rgb(236, 206, 160), false)
            : BuildSky(207, (72, 42, 30), (214, 136, 72), (62, 38, 28), false);
    }

    /// <summary>Open space: stars everywhere, a nebula wash, the green moon on the horizon, and tumbling asteroids.</summary>
    static void BuildVoid(bool scifi)
    {
        {
            var r = new Rng(1401); var c = new Canvas(TS, TS);
            c.Clear(Col.Rgb(4, 4, 12));
            for (int i = 0; i < 70; i++)
            {
                int v = r.Range(60, 255), x = r.Int(TS), y = r.Int(TS);
                c.T.Set(x, y, Col.Rgb(v, v, Math.Min(255, v + r.Range(0, 40))));
                if (v > 220) { c.T.Set(x + 1, y, Col.Rgb(v / 3, v / 3, v / 2)); c.T.Set(x, y + 1, Col.Rgb(v / 3, v / 3, v / 2)); }
            }
            Starfield = c.T;
        }
        {
            var r = new Rng(1402);
            var t = new Tex(256, 128);
            var neb = scifi ? (r: 60, g: 20, b: 90) : (r: 30, g: 40, b: 90);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 256; x++)
                {
                    float n = MathF.Sin(x * 0.05f + MathF.Sin(y * 0.09f) * 2) * MathF.Sin(y * 0.07f + x * 0.013f);
                    int k = (int)(Math.Max(0, n) * 60);
                    t.Px[y * 256 + x] = Col.Rgb(4 + neb.r * k / 60, 4 + neb.g * k / 60, 12 + neb.b * k / 60);
                }
            var c = new Canvas(t);
            for (int i = 0; i < 320; i++) { int v = r.Range(90, 255); c.T.Set(r.Int(256), r.Int(128), Col.Rgb(v, v, Math.Min(255, v + 25))); }
            // the green moon dead ahead: rays heading east land on the sky's left edge, so draw it across the seam
            foreach (int ox in new[] { 0, 256 })
            {
                c.Circle(ox + 2, 92, 22, Col.Rgb(40, 90, 50));
                c.Circle(ox - 2, 88, 18, Col.Rgb(70, 150, 80));
                c.Ellipse(ox + 6, 84, 7, 4, Col.Rgb(110, 190, 120));
                c.Ellipse(ox - 10, 98, 6, 3, Col.Rgb(50, 110, 150));
            }
            SkyVoid = t;
        }
        Asteroid = Enumerable.Range(0, 6).Select(f => AsteroidFrame(f * MathF.Tau / 6)).ToArray();
        {
            // lush turf: blades of green, with flowers (or, in sci-fi, glowing spore caps)
            var r = new Rng(1403); var c = new Canvas(TS, TS);
            for (int i = 0; i < TS * TS; i++) { int v = r.Range(-18, 18); c.T.Px[i] = Col.Rgb(46 + v / 2, 118 + v, 52 + v / 2); }
            for (int i = 0; i < 140; i++) { int x = r.Int(64), y = r.Int(64); c.Rect(x, y, 1, 2, Col.Rgb(90 + r.Int(40), 170 + r.Int(50), 80)); }
            uint bloom = scifi ? Col.Rgb(120, 255, 230) : Col.Rgb(250, 230, 120);
            for (int i = 0; i < 9; i++) { float x = r.Range(2f, 62f), y = r.Range(2f, 62f); c.Circle(x, y, 1.2f, bloom); }
            Meadow = c.T;
        }
    }

    /// <summary>Neon Harbor: wet asphalt, lit building fronts under neon, a skyline, its people and the evidence you find.</summary>
    static void BuildTown()
    {
        {
            var r = new Rng(1501); var c = new Canvas(TS, TS);
            for (int i = 0; i < TS * TS; i++) { int v = r.Range(-8, 8); c.T.Px[i] = Col.Rgb(36 + v, 36 + v, 44 + v); }
            for (int i = 0; i < 12; i++) c.Circle(r.Range(0f, 64f), r.Range(0f, 64f), r.Range(0.6f, 1.4f), Col.Rgb(20, 20, 26));
            c.Ellipse(18, 44, 9, 3, Col.Rgb(80, 40, 90)); c.Ellipse(18, 44, 6, 1.6f, Col.Rgb(200, 80, 200)); // neon in a puddle
            c.Ellipse(48, 16, 7, 2.5f, Col.Rgb(30, 70, 90)); c.Ellipse(48, 16, 4, 1.2f, Col.Rgb(90, 220, 255));
            c.Rect(31, 0, 2, 64, Col.Rgb(90, 88, 60));                                              // faded lane line
            for (int y = 4; y < 64; y += 16) c.Rect(31, y, 2, 6, Col.Rgb(36, 36, 44));
            Street = c.T;
        }
        {
            var r = new Rng(1502); var c = new Canvas(TS, TS);
            c.Clear(Col.Rgb(46, 42, 58));
            for (int y = 0; y < 64; y += 2) c.Rect(0, y, 64, 1, Col.Rgb(42, 38, 54));
            for (int wy = 0; wy < 2; wy++)
                for (int wx = 0; wx < 3; wx++)
                {
                    int x = 5 + wx * 20, y = 14 + wy * 24;
                    bool lit = r.Int(3) > 0;
                    c.Rect(x - 1, y - 1, 14, 14, Col.Rgb(24, 22, 30));
                    c.Rect(x, y, 12, 12, lit ? Col.Rgb(230, 190, 110) : Col.Rgb(30, 34, 50));
                    if (lit) c.Rect(x + 1, y + 1, 4, 10, Col.Rgb(255, 225, 160));
                    c.Rect(x + 5, y, 1, 12, Col.Rgb(24, 22, 30));
                }
            uint neon = r.Int(2) == 0 ? Col.Rgb(255, 70, 200) : Col.Rgb(70, 230, 255);
            c.Rect(0, 3, 64, 3, Col.Shade(neon, 140)); c.Rect(0, 4, 64, 1, neon);
            c.Rect(0, 63, 64, 1, Col.Rgb(20, 18, 26));
            c.Noise(r, 8);
            Facade = c.T;
        }
        {
            var r = new Rng(1503);
            var t = new Tex(256, 128);
            for (int y = 0; y < 128; y++)
            {
                float f = y / 127f;
                uint row = Col.Rgb((int)(20 + 90 * f * f), (int)(10 + 20 * f), (int)(40 + 50 * f));
                for (int x = 0; x < 256; x++) t.Px[y * 256 + x] = row;
            }
            var c = new Canvas(t);
            for (int i = 0; i < 60; i++) c.T.Set(r.Int(256), r.Int(60), Col.Rgb(180, 170, 200));
            // the skyline: towers of every height, their windows lit at random, wrapping round
            for (int x = 0; x < 256;)
            {
                int w = r.Range(8, 20), h = r.Range(14, 60);
                uint bc = Col.Rgb(16 + r.Int(10), 12 + r.Int(8), 26 + r.Int(10));
                c.Rect(x, 128 - h, w, h, bc);
                for (int wy = 128 - h + 3; wy < 126; wy += 4)
                    for (int wx = x + 2; wx < x + w - 1; wx += 3)
                        if (r.Int(4) == 0) c.T.Set(wx, wy, r.Int(5) == 0 ? Col.Rgb(255, 90, 210) : Col.Rgb(240, 200, 120));
                x += w + r.Range(0, 3);
            }
            SkyCity = t;
        }
        People = Enumerable.Range(0, 9).Select(Person).ToArray();
        ClueMark = new[] { EvidenceTag(255), EvidenceTag(190) };
        {
            var c = new Canvas(24, 24);
            c.Tri(4, 20, 20, 20, 12, 4, Col.Rgb(90, 88, 80));
            c.Tri(7, 18, 17, 18, 12, 8, Col.Rgb(130, 126, 112));
            c.Outline(Dark);
            ClueFound = c.T;
        }
    }

    static Tex UpgradeFrame(int f)
    {
        var c = new Canvas(32, 32);
        float pulse = 1 + 0.12f * MathF.Sin(f * MathF.PI / 2);
        c.Glow(16, 16, 15 * pulse, Col.Rgb(255, 120, 230));
        c.Circle(16, 16, 7 * pulse, Col.Rgb(255, 200, 250));
        c.Circle(15, 15, 3.5f, Col.Rgb(255, 255, 255));
        // a tilted ring spinning round the core
        for (int k = 0; k < 24; k++)
        {
            float a = k * MathF.Tau / 24 + f * MathF.PI / 8;
            float x = 16 + MathF.Cos(a) * 13, y = 16 + MathF.Sin(a) * 5;
            if (MathF.Sin(a) < 0 && Math.Abs(x - 16) < 7) continue; // behind the core
            c.Circle(x, y, 1.1f, k % 3 == 0 ? Col.Rgb(255, 240, 120) : Col.Rgb(200, 140, 255));
        }
        return c.T;
    }

    /// <summary>A bright yellow evidence tag with a question mark, at glow `v` (it pulses between frames).</summary>
    static Tex EvidenceTag(int v)
    {
        var c = new Canvas(24, 24);
        c.Tri(2, 22, 22, 22, 12, 2, Col.Shade(Col.Rgb(255, 220, 60), v));
        c.Tri(5, 20, 19, 20, 12, 6, Col.Shade(Col.Rgb(255, 245, 150), v));
        Font.Draw(c.T.Px, 24, 24, 10, 11, "?", Col.Rgb(40, 30, 10), 1, false);
        c.Outline(Dark);
        return c.T;
    }

    /// <summary>One of Neon Harbor's people: coat, trousers, skin, hair and hat all picked from `look`.</summary>
    static Tex Person(int look)
    {
        (int r, int g, int b)[] coats = { (140, 110, 80), (60, 70, 110), (150, 60, 50), (40, 40, 44), (110, 120, 140), (200, 70, 150), (70, 120, 90), (130, 90, 60), (80, 180, 200) };
        (int r, int g, int b)[] skins = { (230, 190, 160), (190, 140, 100), (120, 80, 60), (240, 210, 190), (160, 110, 80) };
        (int r, int g, int b)[] hairs = { (40, 30, 20), (200, 170, 90), (20, 20, 24), (170, 60, 40), (220, 220, 230), (90, 200, 255) };
        var c = new Canvas(32, 64);
        var coat = coats[look % coats.Length];
        var skin = skins[look * 3 % skins.Length];
        var hair = hairs[look * 5 % hairs.Length];
        uint cc = Col.Rgb(coat.r, coat.g, coat.b), dk = Col.Rgb(coat.r * 2 / 3, coat.g * 2 / 3, coat.b * 2 / 3);
        uint sk = Col.Rgb(skin.r, skin.g, skin.b), hr = Col.Rgb(hair.r, hair.g, hair.b);
        c.Rect(11, 44, 4, 19, Col.Rgb(34, 32, 40)); c.Rect(17, 44, 4, 19, Col.Rgb(34, 32, 40));      // legs
        c.Rect(10, 61, 6, 3, Col.Rgb(20, 18, 20)); c.Rect(16, 61, 6, 3, Col.Rgb(20, 18, 20));      // shoes
        c.Tri(8, 48, 24, 48, 16, 18, cc);                                                        // long coat
        c.Rect(9, 20, 14, 26, cc);
        c.Rect(15, 22, 2, 24, dk);                                                               // coat seam
        c.Rect(6, 22, 3, 18, dk); c.Rect(23, 22, 3, 18, dk);                                     // arms
        c.Rect(6, 40, 3, 3, sk); c.Rect(23, 40, 3, 3, sk);                                       // hands
        c.Rect(14, 16, 4, 4, sk);                                                                // neck
        c.Ellipse(16, 11, 5.5f, 6.5f, sk);                                                       // head
        c.Ellipse(16, 7, 5.5f, 3.5f, hr);                                                        // hair
        c.Rect(13, 11, 2, 2, Col.Rgb(20, 20, 24)); c.Rect(18, 11, 2, 2, Col.Rgb(20, 20, 24));  // eyes
        c.Rect(14, 15, 4, 1, Col.Shade(sk, 170));                                                // mouth
        switch (look % 4)
        {
            case 1: c.Rect(8, 4, 16, 2, dk); c.Rect(11, 0, 10, 5, dk); break;                   // fedora
            case 2: c.Rect(10, 3, 12, 3, Col.Rgb(200, 60, 60)); c.Rect(19, 5, 6, 1, Col.Rgb(200, 60, 60)); break; // cap
            case 3: c.Rect(10, 10, 12, 3, Col.Rgb(60, 220, 255)); break;                          // visor
        }
        c.Outline(Dark);
        return c.T;
    }

    /// <summary>One frame of a tumbling asteroid: a lumpy disc lit from the top left, pocked with craters, turned by `rot`.</summary>
    static Tex AsteroidFrame(float rot)
    {
        const int S = 48;
        float R = S * 0.44f;
        var t = new Tex(S, S);
        (float a, float d, float r)[] craters = { (0.4f, 0.45f, 3.5f), (2.2f, 0.55f, 2.6f), (3.9f, 0.3f, 4.2f), (5.1f, 0.6f, 2.2f) };
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x + 0.5f - S / 2f, dy = y + 0.5f - S / 2f, d = MathF.Sqrt(dx * dx + dy * dy);
                float th = MathF.Atan2(dy, dx) - rot;
                float edge = R * (1 + 0.14f * MathF.Sin(3 * th + 0.7f) + 0.08f * MathF.Sin(5 * th + 2.1f) + 0.05f * MathF.Sin(9 * th));
                if (d > edge) continue;
                float round = 1 - (d / edge) * (d / edge);
                int s = 150 + (int)(round * 110) - (int)((dx + dy) / R * 45);
                foreach (var (ca, cd, cr) in craters)
                {
                    float cx = MathF.Cos(ca + rot) * cd * R, cy = MathF.Sin(ca + rot) * cd * R;
                    float q = MathF.Sqrt((dx - cx) * (dx - cx) + (dy - cy) * (dy - cy));
                    if (q < cr) s = s * 150 / 256;
                    else if (q < cr + 1.2f && dx + dy > cx + cy) s = s * 290 / 256;
                }
                t.Px[y * S + x] = Col.Shade(Col.Rgb(122, 110, 98), Math.Clamp(s, 40, 330));
            }
        new Canvas(t).Outline(Dark);
        return t;
    }

    /// <summary>A small rough chip of a texture, for flying debris.</summary>
    static Tex Chunk(Tex from)
    {
        var c = new Canvas(16, 16);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                bool corner = (x < 3 || x > 12) && (y < 3 || y > 12);
                if (!corner) c.T.Set(x, y, Col.Shade(from.Get(24 + x, 24 + y), y < 5 ? 300 : y > 11 ? 190 : 256));
            }
        c.Outline(Dark);
        return c.T;
    }
    static Tex Blocks(uint seed, int bw, int bh, (int r, int g, int b) baseCol, int varAmt, (int r, int g, int b) mortar)
    {
        var rng = new Rng(seed);
        var c = new Canvas(TS, TS);
        c.Clear(Col.Rgb(mortar.r, mortar.g, mortar.b));
        for (int row = 0; row < TS / bh; row++)
        {
            int off = (row & 1) * (bw / 2);
            for (int col = -1; col <= TS / bw; col++)
            {
                int v = rng.Range(-varAmt, varAmt);
                uint bc = Col.Rgb(baseCol.r + v, baseCol.g + v, baseCol.b + v);
                int x0 = col * bw + off;
                for (int y = row * bh + 1; y < row * bh + bh - 1; y++)
                    for (int x = x0 + 1; x < x0 + bw - 1; x++)
                    {
                        int xx = ((x % TS) + TS) % TS;
                        // bevel: lighter top-left edge, darker bottom-right edge
                        int s = 256;
                        if (y == row * bh + 1 || x == x0 + 1) s = 300;
                        else if (y == row * bh + bh - 2 || x == x0 + bw - 2) s = 190;
                        c.T.Set(xx, y, Col.Shade(bc, s));
                    }
            }
        }
        c.Noise(rng, 26);
        return c.T;
    }

    static void BuildWalls()
    {
        Rubble = Cobble(61, (118, 112, 104), 20, (30, 28, 26));
        Stone = Blocks(11, 32, 16, (104, 100, 96), 14, (40, 38, 36));
        Brick = Blocks(23, 16, 8, (120, 58, 44), 16, (50, 34, 28));
        Ice = Blocks(37, 32, 32, (150, 180, 210), 14, (70, 90, 120));
        {
            // cracks in ice
            var c = new Canvas(Ice); var r = new Rng(5);
            for (int i = 0; i < 6; i++)
            {
                float x = r.Range(0, 64), y = r.Range(0, 64);
                for (int k = 0; k < 4; k++) { float nx = x + r.Range(-10, 10), ny = y + r.Range(-10, 10); c.Line(x, y, nx, ny, 1, Col.Rgb(210, 235, 255)); x = nx; y = ny; }
            }
        }
        Moss = Blocks(11, 32, 16, (90, 96, 84), 14, (34, 40, 30));
        {
            var c = new Canvas(Moss); var r = new Rng(77);
            for (int i = 0; i < 22; i++)
            {
                float x = r.Range(0, 64), y = r.Range(0, 64), rad = r.Range(2, 7);
                c.Ellipse(x, y, rad, rad * 0.7f, Col.Rgb(50 + r.Int(30), 90 + r.Int(40), 30 + r.Int(20)));
            }
            // drips down from the top
            for (int i = 0; i < 10; i++) { int x = r.Int(64); c.Rect(x, 0, 2, r.Range(4, 18), Col.Rgb(46, 84, 34)); }
            c.Noise(r, 20);
        }
        Marble = Blocks(91, 64, 32, (170, 160, 150), 8, (90, 80, 70));
        {
            var c = new Canvas(Marble); var r = new Rng(19);
            for (int i = 0; i < 5; i++)
            {
                float x = r.Range(0, 64), y = 0;
                while (y < 64) { float nx = x + r.Range(-4, 4), ny = y + r.Range(3, 8); c.Line(x, y, nx, ny, 1, Col.Rgb(120, 110, 105)); x = nx; y = ny; }
            }
        }

        // wood panelling: vertical planks with dark grain
        {
            var r = new Rng(31); var c = new Canvas(TS, TS);
            for (int p = 0; p < 4; p++)
            {
                int v = r.Range(-14, 14);
                c.Rect(p * 16, 0, 16, 64, Col.Rgb(110 + v, 72 + v, 40 + v));
                c.Rect(p * 16, 0, 1, 64, Col.Rgb(50, 30, 16));
                for (int g = 0; g < 5; g++) { int gx = p * 16 + r.Range(2, 14); c.Rect(gx, 0, 1, 64, Col.Rgb(90 + v, 58 + v, 32 + v)); }
            }
            // iron bands
            c.Rect(0, 8, 64, 4, Col.Rgb(56, 56, 60));
            c.Rect(0, 52, 64, 4, Col.Rgb(56, 56, 60));
            for (int i = 4; i < 64; i += 16) { c.Circle(i, 10, 1.5f, Col.Rgb(140, 140, 150)); c.Circle(i, 54, 1.5f, Col.Rgb(140, 140, 150)); }
            c.Noise(r, 18);
            Wood = c.T;
        }

        // wooden door with iron studs and a ring pull
        {
            var r = new Rng(41); var c = new Canvas(TS, TS);
            c.Clear(Col.Rgb(40, 26, 14));
            for (int p = 0; p < 5; p++)
            {
                int v = r.Range(-12, 12);
                c.Rect(2 + p * 12, 2, 11, 62, Col.Rgb(128 + v, 84 + v, 44 + v));
            }
            for (int y = 10; y < 64; y += 20)
            {
                c.Rect(0, y, 64, 5, Col.Rgb(60, 60, 66));
                for (int x = 5; x < 64; x += 10) c.Circle(x, y + 2.5f, 1.4f, Col.Rgb(160, 160, 170));
            }
            c.Ellipse(50, 34, 4, 5, Col.Rgb(150, 130, 60));
            c.Ellipse(50, 34, 2, 3, Col.Rgb(40, 26, 14));
            c.Noise(r, 16);
            Door = c.T;
        }

        // steel key door: riveted metal with a silver key emblem
        {
            var r = new Rng(43); var c = new Canvas(TS, TS);
            c.Clear(Col.Rgb(70, 74, 82));
            for (int y = 0; y < 64; y += 16) { c.Rect(0, y, 64, 1, Col.Rgb(40, 42, 48)); c.Rect(0, y + 1, 64, 1, Col.Rgb(120, 124, 134)); }
            for (int y = 4; y < 64; y += 16) for (int x = 4; x < 64; x += 8) c.Circle(x, y, 1.3f, Col.Rgb(150, 154, 166));
            c.Circle(32, 32, 11, Col.Rgb(30, 30, 36));
            c.Circle(32, 32, 9, Col.Rgb(190, 196, 210));
            c.Circle(29, 30, 3, Col.Rgb(30, 30, 36));
            c.Rect(31, 30, 9, 3, Col.Rgb(30, 30, 36));
            c.Rect(37, 33, 2, 3, Col.Rgb(30, 30, 36));
            c.Noise(r, 14);
            SteelDoor = c.T;
        }

        // fire key door: scorched iron with a glowing flame emblem
        {
            var r = new Rng(47); var c = new Canvas(TS, TS);
            c.Clear(Col.Rgb(70, 40, 30));
            for (int y = 0; y < 64; y += 16) { c.Rect(0, y, 64, 1, Col.Rgb(30, 16, 10)); c.Rect(0, y + 1, 64, 1, Col.Rgb(130, 70, 40)); }
            for (int y = 4; y < 64; y += 16) for (int x = 4; x < 64; x += 8) c.Circle(x, y, 1.3f, Col.Rgb(180, 110, 60));
            c.Circle(32, 32, 12, Col.Rgb(30, 16, 10));
            c.Glow(32, 32, 12, Col.Rgb(255, 120, 20));
            c.Tri(24, 40, 40, 40, 32, 20, Col.Rgb(255, 140, 30));
            c.Tri(28, 40, 36, 40, 32, 28, Col.Rgb(255, 230, 120));
            c.Noise(r, 14);
            FireDoor = c.T;
        }

        // portcullis: see-through iron grate (transparent between bars)
        {
            var c = new Canvas(TS, TS);
            for (int x = 3; x < 64; x += 12) { c.Rect(x, 0, 4, 64, Col.Rgb(70, 66, 62)); c.Rect(x, 0, 1, 64, Col.Rgb(120, 116, 110)); }
            for (int y = 6; y < 64; y += 14) { c.Rect(0, y, 64, 3, Col.Rgb(60, 56, 52)); c.Rect(0, y, 64, 1, Col.Rgb(110, 106, 100)); }
            for (int x = 3; x < 64; x += 12) c.Tri(x - 1, 60, x + 5, 60, x + 2, 64, Col.Rgb(90, 86, 80));
            Portcullis = c.T;
        }

        // pushable block: a carved cube with a glowing rune, clearly different from the walls
        {
            var r = new Rng(53); var c = new Canvas(TS, TS);
            c.Clear(Col.Rgb(120, 116, 104));
            c.Rect(0, 0, 64, 3, Col.Rgb(170, 166, 150)); c.Rect(0, 0, 3, 64, Col.Rgb(160, 156, 140));
            c.Rect(0, 61, 64, 3, Col.Rgb(60, 58, 50)); c.Rect(61, 0, 3, 64, Col.Rgb(70, 68, 60));
            c.Rect(10, 10, 44, 44, Col.Rgb(96, 92, 82));
            c.Rect(12, 12, 40, 40, Col.Rgb(128, 124, 110));
            c.Line(20, 44, 32, 18, 3, Col.Rgb(80, 220, 200)); c.Line(32, 18, 44, 44, 3, Col.Rgb(80, 220, 200));
            c.Line(24, 36, 40, 36, 3, Col.Rgb(80, 220, 200));
            c.Glow(32, 32, 18, Col.Rgb(60, 200, 180));
            c.Noise(r, 16);
            Block = c.T;
        }

        // the face of a stair step: a dark slab under a worn, lighter lip
        {
            var r = new Rng(57); var c = new Canvas(TS, TS);
            c.Clear(Col.Rgb(78, 74, 70));
            for (int y = 0; y < 64; y += 16)
            {
                c.Rect(0, y, 64, 3, Col.Rgb(140, 134, 124));
                c.Rect(0, y + 3, 64, 1, Col.Rgb(40, 38, 36));
                c.Rect(0, y + 15, 64, 1, Col.Rgb(46, 44, 40));
            }
            for (int x = 0; x < 64; x += 21) c.Rect(x + (x / 21 % 2) * 7, 0, 1, 64, Col.Rgb(52, 50, 46));
            c.Noise(r, 18);
            StepRiser = c.T;
        }

        LeverOff = BuildLever(false);
        LeverOn = BuildLever(true);
    }

    static Tex BuildLever(bool on)
    {
        var t = Stone.Clone(); var c = new Canvas(t);
        c.Rect(22, 20, 20, 28, Col.Rgb(40, 38, 36));
        c.Rect(24, 22, 16, 24, Col.Rgb(80, 70, 50));
        c.Circle(32, 34, 3, Col.Rgb(30, 30, 30));
        if (on) { c.Line(32, 34, 32, 50, 3, Col.Rgb(150, 140, 120)); c.Circle(32, 51, 3.5f, Col.Rgb(60, 200, 90)); }
        else { c.Line(32, 34, 32, 16, 3, Col.Rgb(150, 140, 120)); c.Circle(32, 15, 3.5f, Col.Rgb(200, 50, 40)); }
        return t;
    }

    // ------------------------------------------------------------------ flats

    static void BuildFlats()
    {
        FloorStone = Blocks(101, 32, 32, (86, 82, 78), 12, (36, 34, 32));
        CeilStone = Blocks(103, 16, 16, (66, 62, 60), 8, (30, 28, 26));
        {
            var r = new Rng(107); var c = new Canvas(TS, TS);
            for (int p = 0; p < 8; p++)
            {
                int v = r.Range(-10, 10);
                c.Rect(0, p * 8, 64, 8, Col.Rgb(100 + v, 66 + v, 36 + v));
                c.Rect(0, p * 8, 64, 1, Col.Rgb(46, 28, 14));
                c.Rect(r.Int(64), p * 8, 1, 8, Col.Rgb(46, 28, 14));
            }
            c.Noise(r, 16);
            FloorWood = c.T;
        }
        {
            var r = new Rng(109); var c = new Canvas(TS, TS);
            c.Clear(Col.Rgb(50, 34, 20));
            c.Rect(0, 0, 12, 64, Col.Rgb(80, 52, 28)); c.Rect(32, 0, 12, 64, Col.Rgb(80, 52, 28));
            c.Rect(11, 0, 1, 64, Col.Rgb(30, 18, 10)); c.Rect(43, 0, 1, 64, Col.Rgb(30, 18, 10));
            c.Noise(r, 18);
            CeilWood = c.T;
        }
        {
            var r = new Rng(113); var c = new Canvas(TS, TS);
            for (int i = 0; i < TS * TS; i++) { int v = r.Range(-18, 18); c.T.Px[i] = Col.Rgb(52 + v, 84 + v, 36 + v / 2); }
            for (int i = 0; i < 90; i++) { int x = r.Int(64), y = r.Int(64); c.Rect(x, y, 1, 2, Col.Rgb(90, 130, 60)); }
            for (int i = 0; i < 8; i++) c.Ellipse(r.Range(0, 64), r.Range(0, 64), r.Range(2, 5), r.Range(1, 3), Col.Rgb(80, 64, 44));
            Grass = c.T;
        }
        {
            var r = new Rng(127); var c = new Canvas(TS, TS);
            for (int i = 0; i < TS * TS; i++) { int v = r.Range(-12, 12); c.T.Px[i] = Col.Rgb(200 + v, 208 + v, 220 + v); }
            for (int i = 0; i < 10; i++) c.Ellipse(r.Range(0, 64), r.Range(0, 64), r.Range(3, 8), r.Range(1, 3), Col.Rgb(170, 182, 200));
            Snow = c.T;
        }
        PortalFloor = RuneFloor(Col.Rgb(60, 140, 255));
        ExitFloor = RuneFloor(Col.Rgb(255, 80, 60));
        ExitFloorOff = RuneFloor(Col.Rgb(90, 60, 60));
        {
            var t = FloorStone.Clone(); var c = new Canvas(t);
            c.Rect(8, 8, 48, 48, Col.Rgb(30, 30, 30));
            c.Rect(10, 10, 44, 44, Col.Rgb(90, 110, 106));
            c.Rect(10, 10, 44, 2, Col.Rgb(140, 170, 160));
            c.Line(22, 42, 32, 20, 2, Col.Rgb(80, 220, 200)); c.Line(32, 20, 42, 42, 2, Col.Rgb(80, 220, 200));
            c.Line(25, 35, 39, 35, 2, Col.Rgb(80, 220, 200));
            PlateFloor = t;
        }
        SpawnFloor = RuneFloor(Col.Rgb(160, 60, 220));
        AltarFloor = RuneFloor(Col.Rgb(255, 200, 60));
        AltarFloorOff = RuneFloor(Col.Rgb(110, 90, 50));
        CheckpointFloor = RuneFloor(Col.Rgb(90, 240, 120));
        CheckpointFloorOff = RuneFloor(Col.Rgb(60, 84, 64));
        LiftFloor = RuneFloor(Col.Rgb(210, 240, 255));

        SkyDusk = BuildSky(201, (40, 20, 60), (200, 90, 60), (30, 16, 30), false);
        SkyNight = BuildSky(205, (8, 14, 20), (50, 80, 60), (12, 20, 14), false);
        SkyIce = BuildSky(203, (120, 150, 190), (220, 230, 240), (80, 96, 120), true);
    }

    static Tex RuneFloor(uint glow)
    {
        var t = FloorStone.Clone(); var c = new Canvas(t);
        c.Circle(32, 32, 28, Col.Shade(glow, 110));
        c.Circle(32, 32, 25, Col.Rgb(30, 30, 40));
        c.Circle(32, 32, 22, Col.Shade(glow, 200));
        c.Circle(32, 32, 19, Col.Rgb(20, 20, 30));
        for (int i = 0; i < 5; i++)
        {
            float a0 = i * MathF.Tau / 5, a1 = (i + 2) * MathF.Tau / 5;
            c.Line(32 + MathF.Cos(a0) * 17, 32 + MathF.Sin(a0) * 17, 32 + MathF.Cos(a1) * 17, 32 + MathF.Sin(a1) * 17, 2, glow);
        }
        return t;
    }

    static Tex BuildSky(uint seed, (int r, int g, int b) top, (int r, int g, int b) horizon, (int r, int g, int b) hills, bool snowy)
    {
        var r = new Rng(seed);
        var t = new Tex(256, 128);
        for (int y = 0; y < 128; y++)
        {
            float f = y / 127f;
            uint row = Col.Rgb((int)(top.r + (horizon.r - top.r) * f), (int)(top.g + (horizon.g - top.g) * f), (int)(top.b + (horizon.b - top.b) * f));
            for (int x = 0; x < 256; x++) t.Px[y * 256 + x] = row;
        }
        var c = new Canvas(t);
        // soft cloud streaks
        for (int i = 0; i < 40; i++)
        {
            float cx = r.Range(0, 256), cy = r.Range(10, 80);
            uint cc = Col.Lerp(t.Get((int)cx, (int)cy), Col.Rgb(255, 240, 230), 60);
            for (int k = -1; k <= 1; k++) c.Ellipse((cx + k * 256 + 256) % 256, cy, r.Range(14, 30), r.Range(2, 5), cc);
        }
        // mountain silhouette, wraps horizontally
        float h = 100;
        for (int x = 0; x < 256; x++)
        {
            h += r.Range(-2.2f, 2.2f);
            h += (100 - h) * 0.03f;
            if (x > 240) h += (100 - h) * 0.2f;
            for (int y = (int)h; y < 128; y++)
            {
                uint mc = Col.Rgb(hills.r, hills.g, hills.b);
                if (snowy && y < h + 4) mc = Col.Rgb(230, 236, 245);
                t.Px[y * 256 + x] = mc;
            }
        }
        return t;
    }

    // ------------------------------------------------------------------ sprites

    internal static uint Dark = Col.Rgb(16, 12, 10);

    static void BuildSprites()
    {
        Monsters["ettin"] = PoseSet(DrawEttin);
        Monsters["afrit"] = PoseSet(DrawAfrit);
        Monsters["centaur"] = PoseSet((c, p) => DrawCentaur(c, p, false));
        Monsters["slaughtaur"] = PoseSet((c, p) => DrawCentaur(c, p, true));
        Monsters["heresiarch"] = PoseSet(DrawHeresiarch);
        Monsters["bishop"] = PoseSet(DrawBishop);

        Fireball = new[] { Orb(Col.Rgb(255, 140, 30), Col.Rgb(255, 240, 120), 0), Orb(Col.Rgb(255, 90, 20), Col.Rgb(255, 220, 90), 1) };
        Bolt = new[] { Orb(Col.Rgb(60, 110, 255), Col.Rgb(200, 230, 255), 0), Orb(Col.Rgb(90, 140, 255), Col.Rgb(230, 240, 255), 1) };
        Serpent = new[] { Orb(Col.Rgb(40, 200, 60), Col.Rgb(200, 255, 160), 0), Orb(Col.Rgb(60, 170, 40), Col.Rgb(180, 255, 120), 1) };
        Flame = new[] { Orb(Col.Rgb(255, 80, 10), Col.Rgb(255, 255, 160), 0), Orb(Col.Rgb(240, 40, 10), Col.Rgb(255, 200, 80), 1) };
        BossBall = new[] { Orb(Col.Rgb(170, 40, 220), Col.Rgb(255, 180, 255), 0), Orb(Col.Rgb(130, 30, 200), Col.Rgb(240, 160, 255), 1) };
        CentaurBolt = new[] { Orb(Col.Rgb(220, 30, 30), Col.Rgb(255, 170, 120), 0), Orb(Col.Rgb(180, 20, 40), Col.Rgb(255, 140, 120), 1) };
        Seeker = new[] { Orb(Col.Rgb(60, 220, 120), Col.Rgb(220, 255, 200), 0), Orb(Col.Rgb(30, 180, 90), Col.Rgb(200, 255, 180), 1) };
        Shard = new[] { ShardTex(0), ShardTex(1) };
        Lightning = new[] { LightningTex(1), LightningTex(2) };
        Hammer = new[] { HammerTex(0), HammerTex(1) };
        Smoke = Orb(Col.Rgb(110, 108, 104), Col.Rgb(190, 186, 180), 2);
        RailCore = Orb(Col.Rgb(200, 255, 230), Col.Rgb(255, 255, 255), 0);
        RailSpiral = Orb(Col.Rgb(60, 140, 255), Col.Rgb(160, 210, 255), 0);
        Grenade = new[] { Orb(Col.Rgb(70, 86, 60), Col.Rgb(140, 160, 110), 0), Orb(Col.Rgb(70, 86, 60), Col.Rgb(255, 80, 60), 0) };
        Rocket = new[] { Orb(Col.Rgb(150, 150, 140), Col.Rgb(255, 210, 120), 0), Orb(Col.Rgb(255, 120, 30), Col.Rgb(255, 240, 150), 1) };
        Torch = new[] { TorchTex(0), TorchTex(1), TorchTex(2) };
        Ghost = new[] { GhostTex(0), GhostTex(1) };

        Pillar = Item(c =>
        {
            c.Rect(20, 2, 24, 6, Col.Rgb(150, 140, 130));
            c.Rect(24, 8, 16, 50, Col.Rgb(130, 122, 114));
            for (int x = 26; x < 40; x += 4) c.Rect(x, 8, 1, 50, Col.Rgb(100, 94, 88));
            c.Rect(18, 58, 28, 6, Col.Rgb(150, 140, 130));
        });
        Tree = Item(c =>
        {
            c.Rect(28, 30, 8, 34, Col.Rgb(70, 44, 24));
            c.Line(32, 40, 18, 26, 3, Col.Rgb(70, 44, 24));
            c.Line(32, 36, 46, 22, 3, Col.Rgb(70, 44, 24));
            c.Circle(32, 18, 16, Col.Rgb(34, 70, 30)); c.Circle(20, 26, 10, Col.Rgb(40, 80, 34)); c.Circle(44, 26, 10, Col.Rgb(30, 64, 28));
        });
        Crystal = Item(c =>
        {
            c.Tri(32, 4, 20, 40, 44, 40, Col.Rgb(120, 180, 255));
            c.Tri(32, 4, 32, 40, 44, 40, Col.Rgb(80, 130, 220));
            c.Tri(20, 40, 44, 40, 32, 62, Col.Rgb(60, 100, 190));
            c.Tri(18, 30, 10, 50, 22, 50, Col.Rgb(150, 200, 255));
            c.Tri(46, 30, 54, 50, 42, 50, Col.Rgb(100, 150, 240));
        });
        Vial = Item(c =>
        {
            c.Rect(29, 36, 6, 6, Col.Rgb(180, 180, 190));
            c.Ellipse(32, 52, 9, 10, Col.Rgb(200, 30, 40));
            c.Ellipse(29, 49, 3, 4, Col.Rgb(255, 140, 140));
        });
        Flask = Item(c =>
        {
            c.Rect(28, 28, 8, 10, Col.Rgb(200, 200, 210));
            c.Rect(27, 26, 10, 3, Col.Rgb(120, 80, 40));
            c.Ellipse(32, 50, 13, 13, Col.Rgb(60, 110, 230));
            c.Ellipse(28, 46, 4, 5, Col.Rgb(190, 220, 255));
        });
        Urn = Item(c =>
        {
            c.Ellipse(32, 46, 14, 15, Col.Rgb(170, 130, 40));
            c.Rect(24, 26, 16, 6, Col.Rgb(190, 150, 50));
            c.Rect(22, 44, 20, 3, Col.Rgb(90, 30, 120));
            c.Ellipse(27, 42, 3, 6, Col.Rgb(240, 210, 120));
        });
        BlueMana = ManaTex(Col.Rgb(50, 110, 255), Col.Rgb(200, 230, 255));
        GreenMana = ManaTex(Col.Rgb(40, 190, 60), Col.Rgb(200, 255, 190));
        SteelKey = Item(c =>
        {
            c.Circle(22, 44, 8, Col.Rgb(190, 196, 210)); c.Circle(22, 44, 4, Col.Rgb(0, 0, 0) & 0x00FFFFFF);
            c.Rect(29, 42, 22, 4, Col.Rgb(190, 196, 210));
            c.Rect(44, 46, 3, 6, Col.Rgb(190, 196, 210)); c.Rect(49, 46, 2, 5, Col.Rgb(190, 196, 210));
        });
        FireKey = Item(c =>
        {
            c.Glow(30, 46, 16, Col.Rgb(255, 120, 20));
            c.Circle(22, 44, 8, Col.Rgb(230, 120, 40)); c.Circle(22, 44, 4, Col.Rgb(0, 0, 0) & 0x00FFFFFF);
            c.Rect(29, 42, 22, 4, Col.Rgb(230, 120, 40));
            c.Rect(44, 46, 3, 6, Col.Rgb(230, 120, 40)); c.Rect(49, 46, 2, 5, Col.Rgb(230, 120, 40));
        });
        Armor = Item(c =>
        {
            c.Tri(14, 30, 50, 30, 32, 60, Col.Rgb(150, 150, 160));
            c.Rect(16, 28, 32, 16, Col.Rgb(150, 150, 160));
            c.Rect(10, 28, 8, 8, Col.Rgb(120, 120, 130)); c.Rect(46, 28, 8, 8, Col.Rgb(120, 120, 130));
            c.Ellipse(32, 29, 7, 3, Col.Rgb(0, 0, 0) & 0x00FFFFFF);
            for (int y = 32; y < 56; y += 3) for (int x = 18 + (y % 2); x < 46; x += 3) c.T.Set(x, y, Col.Rgb(100, 100, 110));
        });
        LoreStone = LoreTex(false);
        LoreStoneRead = LoreTex(true);
        Relics = new[]
        {
            // chalice
            Item(c =>
            {
                c.Glow(32, 44, 20, Col.Rgb(255, 220, 120));
                c.Ellipse(32, 38, 11, 7, Col.Rgb(220, 180, 60));
                c.Rect(29, 42, 6, 12, Col.Rgb(200, 160, 50));
                c.Ellipse(32, 56, 9, 3, Col.Rgb(220, 180, 60));
                c.Circle(32, 40, 2.5f, Col.Rgb(220, 30, 60));
            }, 71),
            // crown
            Item(c =>
            {
                c.Glow(32, 46, 20, Col.Rgb(255, 230, 140));
                c.Rect(18, 44, 28, 10, Col.Rgb(230, 190, 60));
                c.Tri(18, 44, 22, 32, 26, 44, Col.Rgb(230, 190, 60));
                c.Tri(28, 44, 32, 30, 36, 44, Col.Rgb(230, 190, 60));
                c.Tri(38, 44, 42, 32, 46, 44, Col.Rgb(230, 190, 60));
                c.Circle(24, 49, 2, Col.Rgb(60, 120, 255)); c.Circle(32, 49, 2, Col.Rgb(220, 30, 60)); c.Circle(40, 49, 2, Col.Rgb(60, 200, 90));
            }, 73),
            // orb on a stand
            Item(c =>
            {
                c.Glow(32, 40, 22, Col.Rgb(170, 110, 255));
                c.Rect(26, 50, 12, 6, Col.Rgb(120, 100, 80));
                c.Circle(32, 42, 9, Col.Rgb(140, 90, 230));
                c.Circle(29, 39, 3, Col.Rgb(230, 210, 255));
            }, 79),
            // codex
            Item(c =>
            {
                c.Glow(32, 46, 20, Col.Rgb(120, 220, 255));
                c.Rect(18, 38, 28, 18, Col.Rgb(110, 40, 40));
                c.Rect(20, 40, 24, 14, Col.Rgb(150, 60, 50));
                c.Rect(18, 38, 3, 18, Col.Rgb(220, 190, 80));
                c.Circle(33, 47, 4, Col.Rgb(120, 220, 255));
            }, 83),
        };
        ChestClosed = ChestTex(false);
        ChestOpen = ChestTex(true);
        // Wings of Wrath: a pair of feathered wings
        Jetpack = Item(c =>
        {
            c.Glow(32, 44, 10, Col.Rgb(255, 230, 150));
            for (int side = -1; side <= 1; side += 2)
                for (int f = 4; f >= 0; f--)
                {
                    // broad feathers fan out and up from the shoulder, longest at the top
                    float ang = -0.25f - f * 0.3f, len = 30 - f * 2;
                    float tx = 32 + side * MathF.Cos(ang) * len, ty = 48 + MathF.Sin(ang) * len;
                    uint col = f % 2 == 0 ? Col.Rgb(240, 236, 224) : Col.Rgb(214, 206, 190);
                    c.Tri(32 + side * 2, 38, 32 + side * 12, 56, tx, ty, col);
                    c.Line(32 + side * 4, 46, tx, ty, 0.8f, Col.Rgb(160, 150, 128));
                }
            c.Ellipse(32, 46, 3, 8, Col.Rgb(210, 170, 60));
            c.Circle(32, 43, 2, Col.Rgb(90, 200, 255));
        }, 111);
        WeaponPiece2 = WeaponIcon(Col.Rgb(80, 150, 255));
        WeaponPiece3 = WeaponIcon(Col.Rgb(80, 230, 90));

        // HUD background: dark stone slab
        HudBack = Blocks(301, 32, 16, (70, 64, 60), 10, (26, 24, 22));
    }

    internal static Tex Item(Action<Canvas> draw, int seed = 7)
    {
        var c = new Canvas(TS, TS);
        draw(c);
        c.Noise(new Rng((uint)seed), 12);
        c.Outline(Dark);
        return c.T;
    }

    static Tex LoreTex(bool read)
    {
        var c = new Canvas(TS, TS);
        uint stone = Col.Rgb(120, 118, 110), glow = read ? Col.Rgb(90, 130, 150) : Col.Rgb(110, 230, 255);
        c.Ellipse(32, 16, 18, 10, stone);
        c.Rect(14, 16, 36, 44, stone);
        c.Rect(10, 58, 44, 6, Col.Rgb(90, 88, 82));
        c.Rect(14, 16, 3, 44, Col.Rgb(150, 148, 140));
        if (!read) c.Glow(32, 34, 20, glow);
        for (int i = 0; i < 6; i++) c.Rect(20 + (i % 2) * 2, 20 + i * 6, 22 - (i % 3) * 4, 2, glow);
        c.Noise(new Rng(read ? 89u : 87u), 12);
        c.Outline(Dark);
        return c.T;
    }

    static Tex ChestTex(bool open)
    {
        var c = new Canvas(64, 48);
        uint wood = Col.Rgb(120, 76, 38), dark = Col.Rgb(70, 42, 20), iron = Col.Rgb(80, 80, 90), gold = Col.Rgb(230, 190, 60);
        // body
        c.Rect(4, 22, 56, 25, wood);
        for (int y = 28; y < 47; y += 6) c.Rect(4, y, 56, 1, dark);
        if (open)
        {
            // lid flung back, glittering treasure inside
            c.Rect(6, 4, 52, 8, dark);
            c.Rect(6, 4, 52, 2, wood);
            c.Rect(6, 16, 52, 7, Col.Rgb(30, 18, 10));
            c.Glow(32, 18, 20, Col.Rgb(255, 210, 80));
            for (int i = 0; i < 9; i++) c.Circle(12 + i * 5, 20 - (i % 3), 2.5f, gold);
            c.Rect(4, 12, 56, 4, wood);
        }
        else
        {
            // rounded lid
            c.Ellipse(32, 20, 28, 12, wood);
            c.Rect(4, 18, 56, 5, wood);
            c.Rect(4, 22, 56, 2, dark);
            for (int y = 10; y < 22; y += 5) c.Rect(6, y, 52, 1, dark);
            c.Rect(26, 20, 12, 12, gold);
            c.Rect(30, 24, 4, 5, Col.Rgb(40, 30, 10));
        }
        // iron bands
        foreach (int x in new[] { 12, 48 })
        {
            c.Rect(x, open ? 12 : 9, 4, open ? 35 : 38, iron);
            c.Rect(x, open ? 12 : 9, 1, open ? 35 : 38, Col.Rgb(150, 150, 160));
        }
        c.Noise(new Rng(open ? 61u : 59u), 14);
        c.Outline(Dark);
        return c.T;
    }

    static Tex ManaTex(uint body, uint hi) => Item(c =>
    {
        c.Tri(32, 28, 20, 46, 44, 46, body);
        c.Tri(20, 46, 44, 46, 32, 62, Col.Shade(body, 170));
        c.Tri(32, 28, 26, 46, 32, 46, hi);
        c.Glow(32, 46, 16, body);
    });

    static Tex WeaponIcon(uint glow) => Item(c =>
    {
        c.Glow(32, 44, 18, glow);
        c.Line(18, 58, 46, 30, 4, Col.Rgb(140, 100, 60));
        c.Tri(40, 26, 54, 22, 50, 36, Col.Rgb(200, 200, 210));
        c.Circle(46, 30, 3, glow);
    });

    static Tex Orb(uint body, uint core, int frame)
    {
        var c = new Canvas(32, 32);
        float r = frame == 0 ? 11 : 13;
        c.Glow(16, 16, r + 3, body);
        c.Circle(16, 16, r * 0.65f, body);
        c.Circle(15, 15, r * 0.35f, core);
        return c.T;
    }

    static Tex ShardTex(int frame)
    {
        var c = new Canvas(32, 32);
        c.Glow(16, 16, 12, Col.Rgb(140, 200, 255));
        if (frame == 0) c.Tri(6, 16, 26, 10, 26, 22, Col.Rgb(220, 240, 255));
        else c.Tri(10, 8, 24, 26, 12, 24, Col.Rgb(220, 240, 255));
        return c.T;
    }

    static Tex LightningTex(int seed)
    {
        var c = new Canvas(32, 32); var r = new Rng((uint)seed);
        c.Glow(16, 16, 14, Col.Rgb(150, 170, 255));
        float x = 16, y = 2;
        while (y < 30) { float nx = x + r.Range(-6, 6), ny = y + r.Range(3, 7); c.Line(x, y, nx, ny, 2, Col.Rgb(240, 240, 255)); x = Math.Clamp(nx, 4, 28); y = ny; }
        return c.T;
    }

    static Tex HammerTex(int frame)
    {
        var c = new Canvas(32, 32);
        c.Glow(16, 16, 15, Col.Rgb(255, 160, 60));
        if (frame == 0) { c.Rect(14, 6, 4, 22, Col.Rgb(120, 80, 40)); c.Rect(8, 4, 16, 9, Col.Rgb(170, 170, 180)); }
        else { c.Rect(6, 14, 22, 4, Col.Rgb(120, 80, 40)); c.Rect(19, 8, 9, 16, Col.Rgb(170, 170, 180)); }
        return c.T;
    }

    static Tex GhostTex(int stride)
    {
        bool scifi = Style == ArtStyle.SciFi;
        uint body = scifi ? Col.Rgb(110, 220, 255) : Col.Rgb(210, 255, 220), edge = scifi ? Col.Rgb(220, 250, 255) : Col.Rgb(250, 255, 250);
        var c = new Canvas(24, 44);
        c.Circle(12, 6, 4.5f, body);                               // head
        c.Rect(8, 11, 8, 14, body);                                // torso
        int s = stride == 0 ? 1 : -1;
        c.Line(9, 12, 9 - 4 * s, 22, 2.5f, body);                  // arms swing against the legs
        c.Line(15, 12, 15 + 4 * s, 22, 2.5f, body);
        c.Line(10, 25, 10 + 4 * s, 43, 3f, body);                  // legs mid-stride
        c.Line(14, 25, 14 - 4 * s, 43, 3f, body);
        // hologram scanlines, and a bright rim so it reads against any wall
        for (int y = 0; y < c.T.H; y += 3)
            for (int x = 0; x < c.T.W; x++)
                if (Col.A(c.T.Get(x, y)) != 0) c.T.Set(x, y, 0);
        var src = (uint[])c.T.Px.Clone();
        for (int y = 0; y < c.T.H; y++)
            for (int x = 0; x < c.T.W; x++)
            {
                if (Col.A(src[y * c.T.W + x]) == 0) continue;
                bool rim = x == 0 || x == c.T.W - 1 || Col.A(src[y * c.T.W + x - 1]) == 0 || Col.A(src[y * c.T.W + x + 1]) == 0;
                if (rim) c.T.Set(x, y, edge);
            }
        return c.T;
    }

    static Tex TorchTex(int frame)
    {
        var c = new Canvas(TS, TS);
        c.Rect(30, 34, 4, 30, Col.Rgb(70, 60, 50));
        c.Rect(24, 30, 16, 5, Col.Rgb(90, 80, 70));
        c.Rect(22, 60, 20, 4, Col.Rgb(90, 80, 70));
        float h = 12 + frame * 2;
        c.Glow(32, 22, 16, Col.Rgb(255, 150, 40));
        c.Tri(24, 30, 40, 30, 32 + (frame - 1) * 3, 30 - h - 6, Col.Rgb(255, 120, 20));
        c.Tri(28, 30, 36, 30, 32 - (frame - 1) * 2, 30 - h, Col.Rgb(255, 230, 120));
        return c.T;
    }

    /// <summary>Monsters are drawn once per live pose; the death frames are derived by collapsing the pain frame.</summary>
    internal static Tex[] PoseSet(Action<Canvas, Pose> draw)
    {
        var live = new Tex[(int)Pose.Pain + 1];
        for (int i = 0; i <= (int)Pose.Pain; i++)
        {
            var c = new Canvas(TS, TS);
            draw(c, (Pose)i);
            c.Noise(new Rng((uint)(i + 3)), 14);
            c.Outline(Dark);
            live[i] = c.T;
        }
        return PoseSet(live);
    }

    /// <summary>The full set of frames from the live poses (walk, walk, attack, pain), in Pose order.</summary>
    internal static Tex[] PoseSet(Tex[] live)
    {
        var frames = new Tex[7];
        Array.Copy(live, frames, (int)Pose.Pain + 1);
        frames[(int)Pose.Die0] = Collapse(frames[(int)Pose.Pain], 0.7f, 90);
        frames[(int)Pose.Die1] = Collapse(frames[(int)Pose.Pain], 0.4f, 140);
        frames[(int)Pose.Dead] = Collapse(frames[(int)Pose.Pain], 0.2f, 180);
        return frames;
    }

    static Tex Collapse(Tex src, float f, int red)
    {
        var t = new Tex(src.W, src.H);
        for (int y = 0; y < src.H; y++)
        {
            int sy = src.H - 1 - (int)((src.H - 1 - y) / f);
            if (sy < 0) continue;
            for (int x = 0; x < src.W; x++)
            {
                int sx = (int)((x - src.W / 2) / (0.8f + (1 - f) * 0.5f)) + src.W / 2;
                if ((uint)sx >= (uint)src.W) continue;
                uint c = src.Get(sx, sy);
                if (Col.A(c) == 0) continue;
                t.Px[y * t.W + x] = Col.Lerp(c, Col.Rgb(120, 10, 10), red);
            }
        }
        var cv = new Canvas(t);
        if (f < 0.5f)
        {
            // blood pool beneath the fallen body
            for (int y = src.H - 5; y < src.H; y++)
                for (int x = 10; x < 54; x++)
                {
                    float dx = (x - 32) / 22f, dy = (y - (src.H - 2.5f)) / 3f;
                    if (dx * dx + dy * dy < 1 && Col.A(t.Get(x, y)) == 0) t.Set(x, y, Col.Rgb(110, 8, 8));
                }
        }
        cv.Outline(Dark);
        return t;
    }

    static void DrawEttin(Canvas c, Pose p)
    {
        uint skin = Col.Rgb(150, 110, 80), cloth = Col.Rgb(90, 70, 50), mace = Col.Rgb(110, 110, 120);
        if (p == Pose.Pain) { skin = Col.Rgb(210, 140, 110); }
        int step = p == Pose.Walk1 ? 3 : p == Pose.Walk0 ? -3 : 0;
        // legs
        c.Rect(20 + step, 44, 9, 20, cloth); c.Rect(35 - step, 44, 9, 20, cloth);
        c.Rect(18 + step, 60, 12, 4, Col.Rgb(60, 40, 30)); c.Rect(34 - step, 60, 12, 4, Col.Rgb(60, 40, 30));
        // body
        c.Ellipse(32, 34, 17, 14, skin);
        c.Rect(18, 38, 28, 8, Col.Rgb(100, 60, 40));
        c.Rect(18, 40, 28, 2, Col.Rgb(160, 150, 90));
        // two heads
        c.Circle(24, 16, 6, skin); c.Circle(40, 16, 6, skin);
        c.Rect(21, 15, 2, 2, Col.Rgb(255, 60, 20)); c.Rect(26, 15, 2, 2, Col.Rgb(255, 60, 20));
        c.Rect(37, 15, 2, 2, Col.Rgb(255, 60, 20)); c.Rect(42, 15, 2, 2, Col.Rgb(255, 60, 20));
        c.Rect(22, 19, 5, 1, Dark); c.Rect(38, 19, 5, 1, Dark);
        // arms and mace
        c.Line(16, 28, 10, 44, 6, skin);
        if (p == Pose.Attack)
        {
            c.Line(48, 28, 54, 12, 6, skin);
            c.Line(54, 12, 50, 2, 3, Col.Rgb(90, 60, 30));
            c.Circle(50, 4, 5, mace);
        }
        else
        {
            c.Line(48, 28, 54, 42, 6, skin);
            c.Line(54, 42, 58, 54, 3, Col.Rgb(90, 60, 30));
            c.Circle(58, 55, 5, mace);
        }
        for (int i = 0; i < 4; i++) c.T.Set(55 + i, 53 + (i % 2), Col.Rgb(200, 200, 210));
    }

    static void DrawAfrit(Canvas c, Pose p)
    {
        uint body = p == Pose.Pain ? Col.Rgb(255, 170, 90) : Col.Rgb(200, 70, 30), wing = Col.Rgb(130, 40, 20);
        float flap = p == Pose.Walk1 ? 8 : p == Pose.Walk0 ? -4 : 0;
        c.Glow(32, 32, 26, Col.Rgb(255, 120, 20));
        c.Tri(28, 26, 2, 12 + flap, 8, 40, wing);
        c.Tri(36, 26, 62, 12 + flap, 56, 40, wing);
        c.Ellipse(32, 34, 9, 13, body);
        c.Circle(32, 18, 7, body);
        c.Tri(26, 14, 22, 4, 29, 12, Col.Rgb(240, 220, 170));
        c.Tri(38, 14, 42, 4, 35, 12, Col.Rgb(240, 220, 170));
        c.Rect(28, 17, 3, 2, Col.Rgb(255, 255, 120)); c.Rect(34, 17, 3, 2, Col.Rgb(255, 255, 120));
        if (p == Pose.Attack) { c.Glow(32, 30, 12, Col.Rgb(255, 255, 150)); c.Circle(32, 24, 3, Col.Rgb(255, 240, 180)); }
        c.Tri(28, 46, 36, 46, 32, 60, Col.Rgb(255, 160, 40));
    }

    static void DrawCentaur(Canvas c, Pose p, bool dark)
    {
        uint hide = dark ? Col.Rgb(70, 60, 80) : Col.Rgb(140, 100, 60);
        uint skin = dark ? Col.Rgb(110, 90, 110) : Col.Rgb(180, 130, 100);
        if (p == Pose.Pain) { hide = Col.Shade(hide, 360); skin = Col.Shade(skin, 360); }
        int step = p == Pose.Walk1 ? 3 : p == Pose.Walk0 ? -3 : 0;
        // horse body and legs
        c.Ellipse(32, 40, 22, 9, hide);
        for (int i = 0; i < 4; i++)
        {
            int lx = 14 + i * 11 + ((i % 2 == 0) ? step : -step);
            c.Rect(lx, 44, 5, 18, hide); c.Rect(lx - 1, 60, 7, 4, Col.Rgb(40, 30, 24));
        }
        c.Line(52, 38, 60, 52, 3, Col.Rgb(40, 30, 24)); // tail
        // human torso
        c.Rect(14, 18, 14, 20, skin);
        c.Rect(13, 22, 16, 10, dark ? Col.Rgb(60, 50, 70) : Col.Rgb(120, 120, 130));
        c.Circle(21, 12, 6, skin);
        c.Rect(15, 5, 12, 5, dark ? Col.Rgb(50, 40, 60) : Col.Rgb(140, 140, 150));
        c.Rect(18, 11, 2, 2, Col.Rgb(255, 40, 30)); c.Rect(23, 11, 2, 2, Col.Rgb(255, 40, 30));
        // shield
        c.Ellipse(9, 30, 6, 10, dark ? Col.Rgb(90, 20, 30) : Col.Rgb(160, 150, 60));
        c.Circle(9, 30, 2, Col.Rgb(220, 220, 220));
        if (dark)
        {
            // staff with glowing tip
            c.Line(30, 26, 38, 4, 3, Col.Rgb(80, 60, 40));
            c.Glow(38, 4, p == Pose.Attack ? 9 : 5, Col.Rgb(255, 40, 40));
        }
        else if (p == Pose.Attack) c.Line(28, 22, 44, 2, 3, Col.Rgb(210, 210, 220));
        else c.Line(28, 26, 40, 40, 3, Col.Rgb(210, 210, 220));
    }

    /// <summary>Dark Bishop: a hovering, legless sorcerer in a tall mitre.</summary>
    static void DrawBishop(Canvas c, Pose p)
    {
        uint robe = p == Pose.Pain ? Col.Rgb(110, 200, 130) : Col.Rgb(40, 90, 60), trim = Col.Rgb(200, 170, 70);
        float bob = p == Pose.Walk1 ? -2 : p == Pose.Walk0 ? 1 : 0;
        c.Glow(32, 38, 26, Col.Rgb(40, 200, 110));
        // robe tapering to a wisp instead of legs
        c.Tri(32, 18 + bob, 12, 52 + bob, 52, 52 + bob, robe);
        c.Tri(20, 52 + bob, 44, 52 + bob, 32, 63, Col.Shade(robe, 170));
        c.Rect(30, 22 + (int)bob, 4, 30, trim);
        c.Rect(18, 40 + (int)bob, 28, 3, trim);
        // hooded face and tall mitre
        c.Circle(32, 16 + bob, 7, Col.Rgb(20, 16, 20));
        c.Tri(24, 12 + bob, 32, -4 + bob, 40, 12 + bob, Col.Rgb(210, 200, 170));
        c.Rect(31, 0 + (int)bob, 2, 12, trim);
        c.Rect(28, 15 + (int)bob, 3, 2, Col.Rgb(255, 50, 40)); c.Rect(34, 15 + (int)bob, 3, 2, Col.Rgb(255, 50, 40));
        if (p == Pose.Attack)
        {
            c.Line(20, 28, 6, 18, 4, robe); c.Line(44, 28, 58, 18, 4, robe);
            c.Glow(6, 18, 9, Col.Rgb(100, 255, 150)); c.Glow(58, 18, 9, Col.Rgb(100, 255, 150));
        }
        else { c.Line(20, 28, 14, 42, 4, robe); c.Line(44, 28, 50, 42, 4, robe); }
    }

    static void DrawHeresiarch(Canvas c, Pose p)
    {
        uint robe = p == Pose.Pain ? Col.Rgb(200, 90, 220) : Col.Rgb(90, 30, 110), trim = Col.Rgb(220, 180, 60);
        float sway = p == Pose.Walk1 ? 2 : p == Pose.Walk0 ? -2 : 0;
        c.Glow(32, 30, 30, Col.Rgb(150, 60, 200));
        c.Tri(32 + sway, 8, 10, 64, 54, 64, robe);
        c.Rect(28, 20, 8, 44, trim);
        c.Circle(32 + sway, 12, 7, Col.Rgb(30, 20, 30));
        c.Tri(24 + sway, 10, 32 + sway, -2, 40 + sway, 10, robe);
        c.Rect(28 + (int)sway, 11, 3, 2, Col.Rgb(255, 230, 60)); c.Rect(34 + (int)sway, 11, 3, 2, Col.Rgb(255, 230, 60));
        if (p == Pose.Attack)
        {
            c.Line(20, 24, 6, 8, 4, robe); c.Line(44, 24, 58, 8, 4, robe);
            c.Glow(6, 8, 8, Col.Rgb(255, 120, 255)); c.Glow(58, 8, 8, Col.Rgb(255, 120, 255));
        }
        else { c.Line(20, 24, 12, 40, 4, robe); c.Line(44, 24, 52, 40, 4, robe); }
        // orbiting cubes
        c.Rect(4, 40, 6, 6, Col.Rgb(200, 60, 60)); c.Rect(54, 40, 6, 6, Col.Rgb(60, 200, 90)); c.Rect(29, 50, 6, 6, Col.Rgb(60, 110, 230));
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
                    c.Noise(new Rng((uint)(cls * 10 + slot + f)), 10);
                    c.Outline(Dark);
                    frames[f] = c.T;
                }
                Weapons[cls * 3 + slot] = frames;
            }
        var rl = new Tex[2];
        for (int f = 0; f < 2; f++)
        {
            var c = new Canvas(WW, WH);
            DrawRocketLauncher(c, f == 1);
            c.Noise(new Rng((uint)(90 + f)), 8);
            c.Outline(Dark);
            rl[f] = c.T;
        }
        Weapons[RocketLauncherArt] = rl;
        var rg = new Tex[2];
        for (int f = 0; f < 2; f++)
        {
            var c = new Canvas(WW, WH);
            DrawRailgun(c, f == 1);
            c.Noise(new Rng((uint)(95 + f)), 6);
            c.Outline(Dark);
            rg[f] = c.T;
        }
        Weapons[RailgunArt] = rg;
        var gl = new Tex[2];
        for (int f = 0; f < 2; f++)
        {
            var c = new Canvas(WW, WH);
            DrawGrenadeLauncher(c, f == 1);
            c.Noise(new Rng((uint)(97 + f)), 8);
            c.Outline(Dark);
            gl[f] = c.T;
        }
        Weapons[GrenadeLauncherArt] = gl;
    }

    /// <summary>The grenade launcher: a stubby wide barrel over a round drum, held low in the middle.</summary>
    static void DrawGrenadeLauncher(Canvas c, bool fire)
    {
        int kick = fire ? 7 : 0;
        uint body = Col.Rgb(78, 90, 66), dark = Col.Rgb(34, 38, 30), band = Col.Rgb(170, 150, 70);
        c.Rect(52, 30 + kick, 26, 34, body);                         // the barrel
        c.Rect(52, 30 + kick, 4, 34, Col.Shade(body, 160));          // its lit edge
        c.Ellipse(65, 31 + kick, 11, 4, dark);                      // the muzzle
        c.Circle(64, 66 + kick, 20, Col.Rgb(64, 74, 56));           // the drum
        for (int k = 0; k < 6; k++) { float a = k * MathF.Tau / 6; c.Circle(64 + MathF.Cos(a) * 12, 66 + kick + MathF.Sin(a) * 12, 4, dark); }
        c.Rect(52, 44 + kick, 26, 3, band);
        if (fire) { c.Glow(65, 28 + kick, 16, Col.Rgb(255, 170, 70)); c.Glow(65, 26 + kick, 10, Col.Rgb(200, 200, 190)); }
        Arm(c, 116, 80, 90, 70, Col.Rgb(90, 90, 100), Col.Rgb(200, 150, 110));
    }

    /// <summary>The railgun: a long slim barrel held low on the right, its coils glowing green (white-hot as it fires).</summary>
    static void DrawRailgun(Canvas c, bool fire)
    {
        int kick = fire ? 5 : 0;
        uint body = Col.Rgb(70, 76, 84), rail = Col.Rgb(120, 128, 140), coil = fire ? Col.Rgb(230, 255, 240) : Col.Rgb(80, 230, 140);
        c.Line(84, 80, 66, 18 + kick, 12, body);                     // the barrel, running up the screen
        c.Line(88, 80, 70, 20 + kick, 3, rail);                      // its lit edge
        for (int k = 0; k < 5; k++)
        {
            float t = 0.15f + k * 0.17f, x = 84 + (66 - 84) * t, y = 80 + (18 + kick - 80) * t;
            c.Ellipse(x, y, 9 - k, 3, coil);                         // the coils down its length
        }
        c.Rect(88, 58 + kick, 14, 22, Col.Rgb(56, 60, 66));          // the stock
        c.Glow(66, 18 + kick, fire ? 12 : 6, coil);
        Arm(c, 118, 80, 96, 70, Col.Rgb(90, 90, 100), Col.Rgb(200, 150, 110));
    }

    /// <summary>The rocket launcher: a heavy square tube held low in the middle, its muzzle a dark hole (flame when it fires).</summary>
    static void DrawRocketLauncher(Canvas c, bool fire)
    {
        int kick = fire ? 6 : 0;
        uint body = Col.Rgb(92, 88, 84), dark = Col.Rgb(46, 44, 42), band = Col.Rgb(150, 120, 60);
        // the tube widens toward you: stacked slices from the muzzle down
        for (int y = 26 + kick; y < WH; y++)
        {
            float t = (y - 26 - kick) / (float)(WH - 26 - kick);
            int half = (int)(12 + 14 * t);
            c.Rect(64 - half, y, half * 2, 1, Col.Shade(body, (int)(90 + 50 * t)));
            c.Rect(64 - half, y, 3, 1, Col.Shade(body, 170)); // lit edge
        }
        c.Rect(50, 26 + kick, 28, 6, Col.Rgb(120, 116, 110));
        c.Ellipse(64, 29 + kick, 9, 3, dark);
        c.Rect(54, 44 + kick, 20, 4, band); c.Rect(46, 64 + kick, 36, 4, band);
        c.Rect(78, 52 + kick, 10, 8, Col.Rgb(70, 66, 62)); // sight
        if (fire) { c.Glow(64, 24 + kick, 26, Col.Rgb(255, 150, 40)); c.Glow(64, 26 + kick, 12, Col.Rgb(255, 240, 160)); }
        Arm(c, 116, 80, 92, 70, Col.Rgb(90, 90, 100), Col.Rgb(200, 150, 110));
    }

    static void Arm(Canvas c, float x, float y, float x1, float y1, uint sleeve, uint skin)
    {
        c.Line(x, y, x1, y1, 16, sleeve);
        c.Circle(x1, y1, 9, skin);
    }

    static void DrawWeapon(Canvas c, int cls, int slot, bool fire)
    {
        uint skin = Col.Rgb(200, 150, 110);
        uint sleeve = cls == 0 ? Col.Rgb(110, 110, 120) : cls == 1 ? Col.Rgb(120, 110, 70) : Col.Rgb(60, 60, 150);
        int lift = fire ? -18 : 0;
        switch (cls * 3 + slot)
        {
            case 0: // Fighter: spiked gauntlets
                {
                    float fx = fire ? 64 : 84, fy = fire ? 30 : 56;
                    c.Line(110, 80, fx, fy, 18, sleeve);
                    c.Circle(fx, fy, 12, Col.Rgb(130, 130, 140));
                    for (int i = 0; i < 4; i++) c.Tri(fx - 10 + i * 6, fy - 9, fx - 7 + i * 6, fy - 17, fx - 4 + i * 6, fy - 9, Col.Rgb(210, 210, 220));
                    c.Line(18, 80, 34, 60, 18, sleeve); c.Circle(34, 60, 12, Col.Rgb(130, 130, 140));
                    break;
                }
            case 1: // Fighter: Timon's axe
                {
                    float hx = fire ? 30 : 80, hy = fire ? 20 : 30;
                    c.Line(96, 80, hx, hy, 6, Col.Rgb(100, 66, 34));
                    c.Tri(hx - 4, hy - 10, hx + 22, hy - 18, hx + 18, hy + 14, Col.Rgb(170, 180, 200));
                    c.Glow(hx + 12, hy - 4, 14, Col.Rgb(80, 140, 255));
                    Arm(c, 120, 80, 98, 72, sleeve, skin);
                    break;
                }
            case 2: // Fighter: Hammer of Retribution
                {
                    float hx = 70, hy = 36 + lift;
                    c.Line(92, 80, hx, hy, 6, Col.Rgb(100, 66, 34));
                    c.Rect((int)hx - 16, (int)hy - 14, 32, 20, Col.Rgb(150, 150, 160));
                    c.Rect((int)hx - 16, (int)hy - 14, 32, 4, Col.Rgb(200, 200, 210));
                    if (fire) c.Glow(hx, hy - 4, 22, Col.Rgb(255, 170, 60));
                    Arm(c, 120, 80, 96, 74, sleeve, skin);
                    break;
                }
            case 3: // Cleric: mace of contrition
                {
                    float hx = fire ? 40 : 80, hy = fire ? 24 : 34;
                    c.Line(100, 80, hx, hy, 5, Col.Rgb(100, 66, 34));
                    c.Circle(hx, hy, 10, Col.Rgb(150, 150, 150));
                    for (int i = 0; i < 6; i++) { float a = i * MathF.Tau / 6; c.Line(hx, hy, hx + MathF.Cos(a) * 14, hy + MathF.Sin(a) * 14, 3, Col.Rgb(190, 190, 190)); }
                    Arm(c, 124, 80, 102, 74, sleeve, skin);
                    break;
                }
            case 4: // Cleric: serpent staff
                {
                    c.Line(90, 80, 70, 20 + lift, 6, Col.Rgb(60, 110, 50));
                    c.Circle(70, 18 + lift, 8, Col.Rgb(60, 140, 60));
                    c.Circle(66, 16 + lift, 2, Col.Rgb(255, 220, 40)); c.Circle(74, 16 + lift, 2, Col.Rgb(255, 220, 40));
                    if (fire) c.Glow(70, 10 + lift, 18, Col.Rgb(100, 255, 90));
                    Arm(c, 116, 80, 92, 70, sleeve, skin);
                    break;
                }
            case 5: // Cleric: firestorm staff
                {
                    c.Line(90, 80, 66, 26 + lift, 6, Col.Rgb(90, 60, 30));
                    c.Tri(58, 30 + lift, 74, 30 + lift, 66, 10 + lift, Col.Rgb(200, 180, 60));
                    c.Glow(66, 22 + lift, fire ? 26 : 12, Col.Rgb(255, 110, 20));
                    Arm(c, 116, 80, 92, 70, sleeve, skin);
                    break;
                }
            case 6: // Mage: sapphire wand
                {
                    c.Line(84, 80, 70, 36 + lift / 2, 4, Col.Rgb(70, 50, 40));
                    c.Circle(70, 34 + lift / 2, 5, Col.Rgb(60, 110, 255));
                    if (fire) c.Glow(70, 30 + lift / 2, 18, Col.Rgb(90, 150, 255));
                    Arm(c, 112, 80, 88, 72, sleeve, skin);
                    break;
                }
            case 7: // Mage: frost shards (open hands)
                {
                    c.Line(10, 80, 38, 58 + lift / 2, 14, sleeve); c.Circle(40, 56 + lift / 2, 9, skin);
                    c.Line(118, 80, 90, 58 + lift / 2, 14, sleeve); c.Circle(88, 56 + lift / 2, 9, skin);
                    c.Glow(64, 50 + lift / 2, fire ? 28 : 12, Col.Rgb(150, 220, 255));
                    break;
                }
            default: // Mage: arc of death
                {
                    c.Line(10, 80, 40, 56 + lift / 2, 14, sleeve); c.Circle(42, 54 + lift / 2, 9, skin);
                    c.Line(118, 80, 88, 56 + lift / 2, 14, sleeve); c.Circle(86, 54 + lift / 2, 9, skin);
                    var r = new Rng(fire ? 3u : 9u);
                    float x = 44, y = 52 + lift / 2;
                    for (int i = 0; i < 6; i++) { float nx = x + 7, ny = y + r.Range(-6, 6); c.Line(x, y, nx, ny, fire ? 3 : 1, Col.Rgb(220, 230, 255)); x = nx; y = ny; }
                    c.Glow(64, 52 + lift / 2, fire ? 24 : 10, Col.Rgb(160, 170, 255));
                    break;
                }
        }
    }
}
