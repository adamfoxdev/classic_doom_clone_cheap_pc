namespace HexenSharp;

/// <summary>The Quake weapons' ammo: shells, rockets (for both launchers), cells and slugs. None is the classes' mana.</summary>
public enum AmmoKind { None, Shells, Rockets, Cells, Slugs }

public static class QuakeAmmo
{
    public const int Kinds = 5;
    /// <summary>The Quake weapons come up about twice as fast as the classes' own (Raise per second).</summary>
    public const float SwitchSpeed = 12f;

    /// <summary>The most you can carry: Quake's 100 shells, rockets and cells; 50 slugs.</summary>
    public static int Max(AmmoKind k) => k switch { AmmoKind.Slugs => 50, AmmoKind.None => 0, _ => 100 };

    /// <summary>A box of it: 20 shells, 5 rockets, 30 cells or 10 slugs.</summary>
    public static int Box(AmmoKind k) => k switch { AmmoKind.Shells => 20, AmmoKind.Rockets => 5, AmmoKind.Cells => 30, AmmoKind.Slugs => 10, _ => 0 };

    /// <summary>What a weapon comes loaded with when you find it.</summary>
    public static int WithWeapon(AmmoKind k) => k switch { AmmoKind.Shells => 10, AmmoKind.Rockets => 8, AmmoKind.Cells => 30, AmmoKind.Slugs => 10, _ => 0 };

    public static string Name(AmmoKind k) => k switch { AmmoKind.Shells => "shells", AmmoKind.Rockets => "rockets", AmmoKind.Cells => "cells", AmmoKind.Slugs => "slugs", _ => "" };
    /// <summary>A letter for the HUD.</summary>
    public static string Short(AmmoKind k) => k switch { AmmoKind.Shells => "S", AmmoKind.Rockets => "R", AmmoKind.Cells => "C", AmmoKind.Slugs => "U", _ => "" };

    public static uint Colour(AmmoKind k) => k switch
    {
        AmmoKind.Shells => Col.Rgb(230, 90, 60),
        AmmoKind.Rockets => Col.Rgb(240, 170, 60),
        AmmoKind.Cells => Col.Rgb(110, 180, 255),
        AmmoKind.Slugs => Col.Rgb(120, 230, 140),
        _ => Col.Rgb(200, 200, 200),
    };
}

/// <summary>
/// The Quake weapons together: the rocket launcher, railgun and grenade launcher (their own files), and here the super
/// shotgun and the lightning gun. They use their own ammo rather than mana, come up fast, and in the campaign each one
/// is hidden on a plinth somewhere in the hub, too high to reach without a jump the weapon before it makes possible.
/// </summary>
public static class QuakeArms
{
    /// <summary>
    /// The super shotgun, Quake's: 14 pellets of 4 at once (56 point blank), spread 0.14 across and 0.08 up and down,
    /// two shells a shot. Pellets that miss leave marks on the walls.
    /// </summary>
    public const int Pellets = 14;
    public const float SpreadAcross = 0.14f, SpreadUp = 0.08f, ShotReach = 23f;
    public static readonly WeaponDef SuperShotgun = new()
    {
        Name = "Super Shotgun", Proj = ProjKind.Bolt, DmgMin = 4, DmgMax = 4, Count = Pellets, Cooldown = 0.7f,
        Ammo = AmmoKind.Shells, Cost = 2, Shotgun = true, ArtIndex = Art.ShotgunArt, Sound = Sfx.Explode,
    };

    /// <summary>
    /// The lightning gun, Quake's: a beam 600 units long (6.7 cells) that does 30 ten times a second to the first thing
    /// it touches, a cell a tick, with a small push. Hold Fire and sweep it over them.
    /// </summary>
    public const float BeamReach = 6.7f;
    public static readonly WeaponDef LightningGun = new()
    {
        Name = "Lightning Gun", Proj = ProjKind.Lightning, DmgMin = 30, DmgMax = 30, Cooldown = 0.1f,
        Ammo = AmmoKind.Cells, Cost = 1, Beam = true, ArtIndex = Art.LightningGunArt, Sound = Sfx.Magic,
    };

    /// <summary>The Quake weapons, in the order of their keys on the range (0, -, =, [, ]).</summary>
    public static readonly WeaponDef[] All = { Rockets.Launcher, Railgun.Gun, Grenades.Launcher, SuperShotgun, LightningGun };

    /// <summary>
    /// The order the campaign hides them in, map by map, each plinth higher than the last: the grenade launcher on a
    /// plinth you can climb with two jumps (a step beside it), then plinths only a grenade or rocket jump reaches.
    /// </summary>
    public static readonly (WeaponDef weapon, float height)[] Stashes =
    {
        (Grenades.Launcher, 1.5f), (SuperShotgun, 2.5f), (Rockets.Launcher, 2.5f), (LightningGun, 3f), (Railgun.Gun, 3.5f),
    };
    public const float StepHeight = 0.75f;
}

/// <summary>A place a stash went: which map, the plinth's corner cell, its height, and the weapon on it.</summary>
public sealed record Stash(int Map, int X, int Y, float Height, WeaponDef Weapon);

public sealed partial class Game
{
    /// <summary>Where this campaign's Quake weapons are hidden (for the tests and the console's 'stashes').</summary>
    public readonly List<Stash> StashPlaces = new();

    /// <summary>
    /// Hides the Quake weapons through the hub, one a map in the Stashes order: a 2 by 2 plinth raised in the most open
    /// spot of each map that has headroom for it, the weapon and a box of its ammo on top, and (for the first) a step
    /// beside it. A map with nowhere to put one passes it on to the next. Later maps also get boxes of the ammo the
    /// weapons found so far use.
    /// </summary>
    void PlaceStashes()
    {
        StashPlaces.Clear();
        int next = 0;
        var kinds = new List<AmmoKind>();
        for (int i = 0; i < Hub.Length; i++)
        {
            var lv = Hub[i];
            if (lv.Flight || lv.Dig || lv.Arena != null || lv.PlateCount > 0) continue; // (a block puzzle's floor is its own)
            if (next < QuakeArms.Stashes.Length)
            {
                var (weapon, height) = QuakeArms.Stashes[next];
                if (FindStashSpot(lv, height, next == 0) is { } spot)
                {
                    RaiseStash(lv, spot.x, spot.y, height, weapon, next == 0);
                    StashPlaces.Add(new Stash(i, spot.x, spot.y, height, weapon));
                    if (!kinds.Contains(weapon.Ammo)) kinds.Add(weapon.Ammo);
                    next++;
                }
            }
            // boxes of the ammo found so far, about the map
            foreach (var k in kinds)
                foreach (var (x, y) in OpenSpots(lv, 2, (int)k * 7 + i))
                    lv.Things.Add(new Pickup(PickupKind.Ammo, 0.36f, (int)k) { X = x, Y = y, Level = lv });
        }
    }

    /// <summary>Cells free for a stash: open floor with nothing on it, no marks, no doors.</summary>
    static bool Open(Level lv, int x, int y) => lv.InBounds(x, y) && lv.Cells[y * lv.W + x] == '\0' && lv.Marks[y * lv.W + x] == '\0';

    /// <summary>
    /// The most open spot for a plinth: a 2 by 2 plinth with a ring of open floor round it (all one level), nothing
    /// fixed standing near, and away from the start. (The ceiling over it is raised to fit: see RaiseStash.)
    /// </summary>
    static (int x, int y)? FindStashSpot(Level lv, float height, bool step)
    {
        (int x, int y)? best = null;
        int bestOpen = -1;
        for (int y = 2; y < lv.H - 3; y++)
            for (int x = 2; x < lv.W - 3; x++)
            {
                float floor = lv.Floors[y * lv.W + x];
                bool ok = true;
                for (int dy = -1; dy <= 2 && ok; dy++)
                    for (int dx = -1; dx <= 2 && ok; dx++)
                    {
                        int cx = x + dx, cy = y + dy, c = cy * lv.W + cx;
                        ok = Open(lv, cx, cy) && MathF.Abs(lv.Floors[c] - floor) < 0.001f;
                    }
                if (!ok) continue;
                float mx = x + 1, my = y + 1;
                if (MathF.Abs(lv.StartX - mx) + MathF.Abs(lv.StartY - my) < 8) continue;
                if (lv.Things.Any(t => t is not Monster && MathF.Abs(t.X - mx) < 2.5f && MathF.Abs(t.Y - my) < 2.5f)) continue; // (monsters wander off)
                if (lv.Things.Any(t => t is Monster { Def.MiniBoss: not null } && MathF.Abs(t.X - mx) + MathF.Abs(t.Y - my) < 12f)) continue; // a mini-boss's ground is its own
                // how much open floor at this level around it (room for a run-up)
                int open = 0;
                for (int dy = -4; dy <= 5; dy++)
                    for (int dx = -4; dx <= 5; dx++)
                        if (Open(lv, x + dx, y + dy) && MathF.Abs(lv.Floors[(y + dy) * lv.W + x + dx] - floor) < 0.001f) open++;
                if (open > bestOpen) (best, bestOpen) = ((x, y), open);
            }
        return best;
    }

    /// <summary>
    /// Raises the plinth (and the first one's step), opens the ceiling above it and the floor round it into a shaft
    /// tall enough to jump up there, and puts the weapon and a box of its ammo on top.
    /// </summary>
    static void RaiseStash(Level lv, int x, int y, float height, WeaponDef weapon, bool step)
    {
        float floor = lv.Floors[y * lv.W + x];
        float ceiling = MathF.Min(Level.MaxHeight, floor + height + 3f);
        for (int cy = y - 4; cy <= y + 5; cy++)
            for (int cx = x - 4; cx <= x + 5; cx++)
                if (lv.InBounds(cx, cy) && lv.Cells[cy * lv.W + cx] == '\0')
                    lv.Heights[cy * lv.W + cx] = MathF.Max(lv.Heights[cy * lv.W + cx], ceiling);
        for (int dy = 0; dy < 2; dy++)
            for (int dx = 0; dx < 2; dx++)
                lv.Floors[(y + dy) * lv.W + x + dx] = floor + height;
        if (step) lv.Floors[y * lv.W + x - 1] = floor + QuakeArms.StepHeight;
        int index = Array.IndexOf(Rockets.AllWeapons(), weapon);
        lv.Things.Add(new Pickup(PickupKind.Arms, 0.6f, index) { X = x + 0.6f, Y = y + 1f, Level = lv });
        lv.Things.Add(new Pickup(PickupKind.Ammo, 0.36f, (int)weapon.Ammo) { X = x + 1.5f, Y = y + 1.4f, Level = lv });
    }

    /// <summary>`n` open cells about a map for ammo boxes, picked the same way every time (by `salt`), well apart.</summary>
    static IEnumerable<(float x, float y)> OpenSpots(Level lv, int n, int salt)
    {
        var picked = new List<(float x, float y)>();
        var cells = Enumerable.Range(0, lv.W * lv.H)
            .Where(c => Open(lv, c % lv.W, c / lv.W))
            .OrderBy(c => (uint)((c * 2654435761u) ^ (uint)(salt * 40503)))
            .ToList();
        foreach (int c in cells)
        {
            float x = c % lv.W + 0.5f, y = c / lv.W + 0.5f;
            if (lv.Things.Any(t => MathF.Abs(t.X - x) < 1.2f && MathF.Abs(t.Y - y) < 1.2f)) continue;
            if (picked.Any(q => MathF.Abs(q.x - x) + MathF.Abs(q.y - y) < 6)) continue;
            picked.Add((x, y));
            if (picked.Count >= n) break;
        }
        return picked;
    }

    /// <summary>A Quake weapon found in the campaign: yours (on the next key), loaded, and in hand.</summary>
    void TakeStash(Pickup pk)
    {
        var all = Rockets.AllWeapons();
        if (pk.Variant < 0 || pk.Variant >= all.Length) return;
        var weapon = all[pk.Variant];
        var p = P;
        p.Loadout ??= p.Def.Weapons.ToArray();
        int i = Array.IndexOf(p.Loadout, weapon);
        if (i < 0)
        {
            p.Loadout = p.Loadout.Append(weapon).ToArray();
            i = p.Loadout.Length - 1;
            var had = p.HasWeapon;
            p.HasWeapon = new bool[p.Loadout.Length];
            had.CopyTo(p.HasWeapon, 0);
        }
        bool fresh = !p.HasWeapon[i];
        p.HasWeapon[i] = true;
        int k = (int)weapon.Ammo;
        p.Ammo[k] = Math.Min(QuakeAmmo.Max(weapon.Ammo), p.Ammo[k] + QuakeAmmo.WithWeapon(weapon.Ammo));
        pk.Removed = true;
        p.PickupFlash = 1;
        PlaySound(Sfx.Secret, 0.8f);
        PlaySound(Sfx.Item, 1);
        Say(fresh ? $"You found the {weapon.Name}! Key {(i + 1) % 10}." : $"{weapon.Name}: more {QuakeAmmo.Name(weapon.Ammo)}.");
        if (fresh) SelectWeapon(i);
    }

    /// <summary>A box of Quake ammo: taken if you've room for any of it.</summary>
    bool TakeAmmo(Pickup pk)
    {
        var kind = (AmmoKind)pk.Variant;
        int k = (int)kind, max = QuakeAmmo.Max(kind);
        if (k <= 0 || k >= QuakeAmmo.Kinds || P.Ammo[k] >= max) return false;
        int got = Math.Min(max - P.Ammo[k], QuakeAmmo.Box(kind));
        P.Ammo[k] += got;
        P.PickupFlash = 1;
        PlaySound(Sfx.Pickup, 1);
        Say($"+{got} {QuakeAmmo.Name(kind)}");
        return true;
    }

    /// <summary>Once you carry a Quake weapon, a fallen monster sometimes leaves a box of ammo for one you have (Quake's dropped packs).</summary>
    void QuakeDrop(Monster m)
    {
        if (Practicing || m.Target != null || P.Loadout == null) return;
        var kinds = P.Loadout.Where(w => w.Quick).Select(w => w.Ammo).Distinct().ToList();
        if (kinds.Count == 0 || _loot.NextDouble() >= 0.3) return;
        var kind = kinds[_loot.Next(kinds.Count)];
        Level.Things.Add(new Pickup(PickupKind.Ammo, 0.36f, (int)kind) { X = m.X, Y = m.Y, Level = Level });
    }

    // ------------------------------------------------------------------ hitscan: the super shotgun and the lightning gun

    /// <summary>
    /// Along a line from (x, y, z), `angle` across and `climb` up, out to `reach`: the first monster in the way (and how
    /// far along), and where the line ends (the first wall, floor or ceiling, or its reach).
    /// </summary>
    (Monster m, float end) Trace(float x0, float y0, float z0, float angle, float climb, float reach)
    {
        float ca = MathF.Cos(angle), sa = MathF.Sin(angle), flat = MathF.Cos(climb), up = MathF.Sin(climb);
        const float step = 0.05f;
        float end = 0;
        while (end < reach)
        {
            float t = end + step, x = x0 + ca * flat * t, y = y0 + sa * flat * t, z = z0 + up * t;
            if (Level.BlocksPoint(x, y) || z < Level.FloorAt(x, y) || z > Level.HeightAt(x, y)) break;
            end = t;
        }
        Monster best = null;
        float bestAlong = end;
        foreach (var t in Level.Things)
        {
            if (t is not Monster m || !m.Alive || m.Blurring) continue;
            float dx = m.X - x0, dy = m.Y - y0;
            float along = (dx * ca + dy * sa) / MathF.Max(0.001f, flat);
            if (along <= 0 || along > bestAlong) continue;
            if (MathF.Abs(-dx * sa + dy * ca) > m.Radius + 0.05f) continue;
            float z = z0 + up * along, foot = Level.FloorAt(m.X, m.Y) + m.Z;
            if (z < foot - 0.05f || z > foot + m.SpriteH + 0.05f) continue;
            (best, bestAlong) = (m, along);
        }
        return (best, best != null ? bestAlong : end);
    }

    /// <summary>The line up for a hitscan shot: along your view, or at a monster above or below you when you're looking near level.</summary>
    float HitscanClimb(float z0)
    {
        float climb = RailAim();
        if (MathF.Abs(P.Pitch) <= Rockets.NormalPitch && VerticalAim(z0, 1f) is { } slope) climb = MathF.Atan(slope);
        return climb;
    }

    readonly Queue<Puff> _marks = new();
    /// <summary>Pellet marks kept on the walls at once; older ones go.</summary>
    public const int MaxMarks = 160;

    /// <summary>Fires the super shotgun: 14 pellets at once, each stopping in the first monster or wall; the damage adds up per monster.</summary>
    void FireShotgun(WeaponDef w, float mult)
    {
        var p = P;
        float z0 = p.FloorZ + p.ViewZ - 0.05f, climb = HitscanClimb(z0);
        var hits = new Dictionary<Monster, int>();
        for (int i = 0; i < QuakeArms.Pellets; i++)
        {
            float a = p.Angle + (RandF() - 0.5f) * 2 * QuakeArms.SpreadAcross, c = climb + (RandF() - 0.5f) * 2 * QuakeArms.SpreadUp;
            var (m, end) = Trace(p.X, p.Y, z0, a, c, QuakeArms.ShotReach);
            if (m != null) { hits[m] = hits.GetValueOrDefault(m) + (int)MathF.Round(w.DmgMin * mult); continue; }
            if (end >= QuakeArms.ShotReach - 0.1f) continue;
            // a mark where it struck, just this side of the surface
            float back = MathF.Max(0, end - 0.03f), flat = MathF.Cos(c);
            float x = p.X + MathF.Cos(a) * flat * back, y = p.Y + MathF.Sin(a) * flat * back, z = z0 + MathF.Sin(c) * back;
            var mark = new Puff(Art.PelletMark, 0.05f, 12f, 0f) { X = x, Y = y, Z = z - 0.025f, Level = Level, FullBright = false };
            Level.Things.Add(mark);
            _marks.Enqueue(mark);
            while (_marks.Count > QuakeArms.Pellets && (_marks.Count > MaxMarks || _marks.Peek().Removed)) _marks.Dequeue().Removed = true;
        }
        foreach (var (m, dmg) in hits)
        {
            float mz = Level.FloorAt(m.X, m.Y) + m.Z + m.SpriteH * 0.5f;
            SpawnPuff(Art.Fireball[1], m.X, m.Y, mz, 0.25f);
            Push(m, m.X - p.X, m.Y - p.Y, Dist(m.X, m.Y, p.X, p.Y), dmg);
            DamageMonster(m, dmg, p.Weapon);
        }
        AddShake(0.14f);
    }

    int _beamTick;

    /// <summary>Fires a tick of the lightning gun: the beam to the first thing it touches, 30 to it and a small push, and the crackle along it.</summary>
    void FireBeam(WeaponDef w, float mult)
    {
        var p = P;
        float z0 = p.FloorZ + p.ViewZ - 0.08f, climb = HitscanClimb(z0);
        var (m, end) = Trace(p.X, p.Y, z0, p.Angle, climb, QuakeArms.BeamReach);
        float ca = MathF.Cos(p.Angle), sa = MathF.Sin(p.Angle), flat = MathF.Cos(climb), up = MathF.Sin(climb);
        if (m != null)
        {
            int dmg = (int)MathF.Round(w.DmgMin * mult);
            Push(m, ca, sa, 1f, dmg * 0.4f);
            DamageMonster(m, dmg, p.Weapon);
        }
        // the crackle: a jagged line of sparks, fresh each tick
        for (float t = 0.5f; t < end; t += 0.18f)
        {
            float j = 0.06f * MathF.Min(1, t);
            float x = p.X + ca * flat * t + (RandF() - 0.5f) * j, y = p.Y + sa * flat * t + (RandF() - 0.5f) * j, z = z0 + up * t + (RandF() - 0.5f) * j - 0.1f;
            Level.Things.Add(new Puff(Art.Lightning[(int)(t * 5) & 1], 0.05f, 0.1f, 0f) { X = x, Y = y, Z = z, Level = Level });
        }
        SpawnPuff(Art.Bolt[1], p.X + ca * flat * end, p.Y + sa * flat * end, z0 + up * end, 0.2f);
        if (_beamTick++ % 3 == 0) PlaySound(w.Sound, 0.5f); // not every tick: it would drone
    }
}
