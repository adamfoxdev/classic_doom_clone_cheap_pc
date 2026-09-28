namespace HexenSharp;

/// <summary>
/// The railgun, Quake III's: a slug that hits the instant you fire, wherever the middle of the view points (level or up
/// or down), for 100 to everything in its line, walls excepted. It goes through every monster in the way and leaves
/// a white trail wound round with a blue spiral that fades over a second. It's slow to reload (a second and a half),
/// and a hit knocks a monster back a little. On the range's rack, or 'give railgun'.
/// </summary>
public static class Railgun
{
    public const float Reach = 40f, Width = 0.1f;
    public static readonly WeaponDef Gun = new()
    {
        Name = "Railgun", Proj = ProjKind.Bolt, DmgMin = 100, DmgMax = 100, Cooldown = 1.5f, Ammo = AmmoKind.Slugs, Cost = 1,
        Rail = true, ArtIndex = Art.RailgunArt, Sound = Sfx.Shoot,
    };
}

public sealed partial class Game
{
    /// <summary>The slug's climb angle: along the view (or at a monster above or below, when you're looking near level).</summary>
    public float RailAim() => MathF.Atan(P.Pitch / (160f / MathF.Tan(ViewFov * MathF.PI / 360f)));

    /// <summary>Railgun hits this game (monsters struck, counting each one a slug goes through).</summary>
    public int RailHits;

    /// <summary>Fires the railgun: the slug's line out to the first wall, everything in it hit, and its trail.</summary>
    void FireRail(WeaponDef w, float mult)
    {
        var p = P;
        float z0 = p.FloorZ + p.ViewZ - 0.05f;
        float climb = RailAim();
        if (MathF.Abs(p.Pitch) <= Rockets.NormalPitch && VerticalAim(z0, 1f) is { } slope) climb = MathF.Atan(slope);
        float ca = MathF.Cos(p.Angle), sa = MathF.Sin(p.Angle), flat = MathF.Cos(climb), up = MathF.Sin(climb);

        // how far it goes: to the first wall, floor or ceiling
        const float step = 0.05f;
        float end = 0;
        (int x, int y, Level.Face face)? block = null;
        while (end < Railgun.Reach)
        {
            float t = end + step, x = p.X + ca * flat * t, y = p.Y + sa * flat * t, z = z0 + up * t;
            int cx = (int)MathF.Floor(x), cy = (int)MathF.Floor(y);
            if (Level.BlocksPoint(x, y) || z < Level.FloorAt(x, y) || z > Level.HeightAt(x, y))
            {
                var face = Level.Cell(cx, cy) == Level.Rubble ? Level.Face.Wall : z < Level.FloorAt(x, y) ? Level.Face.Floor : Level.Face.Ceiling;
                if (Level.CanDig(cx, cy, face)) block = (cx, cy, face);
                break;
            }
            end = t;
        }

        // everything along the line, nearest first
        var hits = new List<(Monster m, float t)>();
        foreach (var t in Level.Things)
        {
            if (t is not Monster m || !m.Alive || m.Blurring) continue;
            float dx = m.X - p.X, dy = m.Y - p.Y;
            float along = (dx * ca + dy * sa) / MathF.Max(0.001f, flat); // distance along the slug's (3D) line
            float ahead = along * flat;
            if (along <= 0 || along > end) continue;
            float side = MathF.Abs(-dx * sa + dy * ca);
            if (side > m.Radius + Railgun.Width) continue;
            float z = z0 + up * along, foot = Level.FloorAt(m.X, m.Y) + m.Z;
            if (z < foot - Railgun.Width || z > foot + m.SpriteH + Railgun.Width) continue;
            hits.Add((m, ahead));
        }
        int dmg = (int)MathF.Round(Rand(w.DmgMin, w.DmgMax) * mult);
        foreach (var (m, _) in hits.OrderBy(h => h.t))
        {
            float mz = Level.FloorAt(m.X, m.Y) + m.Z + m.SpriteH * 0.5f;
            SpawnPuff(Art.RailSpiral, m.X, m.Y, mz, 0.4f);
            Push(m, ca, sa, 1f, dmg);
            DamageMonster(m, dmg, p.Weapon);
            RailHits++;
        }
        InstagibShot(hits.Count);
        if (RailTrial) TrialShots++;
        if (block is { } b) HitBlock(b.x, b.y, dmg, b.face, slot: SlotAt(z0 + up * end));

        // the trail: a white core, a blue spiral wound round it, fading together
        float ux = -sa, uy = ca; // across the line, level
        for (float t = 1.2f; t < end; t += 0.22f)
        {
            float x = p.X + ca * flat * t, y = p.Y + sa * flat * t, z = z0 + up * t - 0.08f;
            Level.Things.Add(new Puff(Art.RailCore, 0.035f, 0.9f, 0f) { X = x, Y = y, Z = z, Level = Level });
            float a = t * 5f, r = 0.09f;
            Level.Things.Add(new Puff(Art.RailSpiral, 0.045f, 1.1f, 0.04f)
                { X = x + ux * MathF.Cos(a) * r, Y = y + uy * MathF.Cos(a) * r, Z = z + MathF.Sin(a) * r, Level = Level });
        }
        float ex = p.X + ca * flat * end, ey = p.Y + sa * flat * end;
        SpawnPuff(Art.RailSpiral, ex, ey, z0 + up * end, 0.3f);
        AddShake(0.08f);
    }
}
