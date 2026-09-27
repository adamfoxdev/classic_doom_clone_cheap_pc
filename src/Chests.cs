namespace HexenSharp;

/// <summary>A treasure chest. Open it with Use; it spills random loot (or, sometimes, a monster).</summary>
public sealed class Chest : Thing
{
    public bool Opened;
    public Chest() { Solid = true; Radius = 0.3f; SpriteW = 0.6f; SpriteH = 0.45f; }
    public override Tex Sprite(float time) => Opened ? Art.ChestOpen : Art.ChestClosed;
}

/// <summary>Random chest placement and loot.</summary>
public static class Chests
{
    /// <summary>Chance that a chest is a trap and releases a monster.</summary>
    public const float TrapChance = 0.12f;

    /// <summary>
    /// Scatters chests over a map: `perCells` chests per 200 open floor cells (at least one).
    /// Chests sit against a wall, away from doors, portals and other things, and are only placed
    /// where they can't cut off any part of the map.
    /// </summary>
    public static int Scatter(Level lv, Random rng, float perCells)
    {
        var (sx, sy) = lv.ArrivalCell();
        var reach = lv.Reachable(sx, sy);
        int open = reach.Count(r => r);
        int want = perCells <= 0 ? 0 : Math.Clamp((int)MathF.Round(perCells * open / 200f), 1, 12);

        var blocked = new HashSet<int>();
        int placed = 0;
        for (int attempt = 0; attempt < 600 && placed < want; attempt++)
        {
            int i = rng.Next(lv.Cells.Length);
            if (lv.Dig)
            {
                // on a dig map chests are buried treasure: a pocket carved deep in the rock
                int bx = i % lv.W, by = i / lv.W;
                if (lv.Cells[i] != Level.Rubble || Math.Abs(bx - sx) + Math.Abs(by - sy) < 3) continue;
                if (new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) }.Any(o => lv.Cell(bx + o.Item1, by + o.Item2) != Level.Rubble)) continue;
                lv.Cells[i] = '\0';
                lv.BlockHp[i] = 0;
                lv.Things.Add(new Chest { X = bx + 0.5f, Y = by + 0.5f, Level = lv });
                placed++;
                continue;
            }
            if (!reach[i] || blocked.Contains(i) || !Suitable(lv, i, sx, sy, strict: attempt < 400)) continue;

            // make sure the chest doesn't cut the map in two
            blocked.Add(i);
            int after = lv.Reachable(sx, sy, blocked).Count(r => r);
            if (after != open - blocked.Count) { blocked.Remove(i); continue; }

            var c = new Chest { X = i % lv.W + 0.5f, Y = i / lv.W + 0.5f, Level = lv };
            // nudge toward the wall it sits against so it doesn't crowd the room
            int x = i % lv.W, y = i / lv.W;
            if (lv.Blocks(x - 1, y)) c.X -= 0.15f; else if (lv.Blocks(x + 1, y)) c.X += 0.15f;
            if (lv.Blocks(x, y - 1)) c.Y -= 0.15f; else if (lv.Blocks(x, y + 1)) c.Y += 0.15f;
            lv.Things.Add(c);
            placed++;
        }
        return placed;
    }

    static bool Suitable(Level lv, int i, int sx, int sy, bool strict)
    {
        int x = i % lv.W, y = i / lv.W;
        if (lv.Cells[i] != '\0' || lv.Marks[i] != '\0') return false;
        if (Math.Abs(x - sx) + Math.Abs(y - sy) < 3) return false;

        int walls = 0;
        bool horiz = false, vert = false;
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            char c = lv.Cell(x + dx, y + dy);
            if (Level.IsDoor(c) || c == 'L' || Level.IsRubble(c)) return false; // never block doors, levers or rubble you'll dig through
            if (c != '\0') { walls++; if (dx != 0) horiz = true; else vert = true; }
        }
        // against one wall (or tucked in a corner); never in a corridor or out in the open
        if (walls == 0 || walls > 2) return false;
        if (walls == 2 && !(horiz && vert)) return false;
        if (strict && walls != 1) return false;

        // keep clear of puzzle blocks, portals, the exit, arena runes and other things
        for (int j = 0; j < lv.Cells.Length; j++)
            if (lv.Cells[j] == 'X' && Math.Abs(j % lv.W - x) + Math.Abs(j / lv.W - y) < 4) return false;
        for (int j = 0; j < lv.Marks.Length; j++)
            if (lv.Marks[j] != '\0' && Math.Abs(j % lv.W - x) + Math.Abs(j / lv.W - y) < 3) return false;
        foreach (var t in lv.Things)
            if (Game.Dist(t.X, t.Y, x + 0.5f, y + 0.5f) < 1.3f) return false;
        return true;
    }

    public static readonly Dictionary<char, string> LootNames = new()
    {
        ['h'] = "Crystal Vial", ['q'] = "Quartz Flask", ['u'] = "Mystic Urn", ['b'] = "Blue Mana",
        ['g'] = "Green Mana", ['r'] = "Mesh Armor", ['w'] = "a weapon piece", ['x'] = "a weapon piece",
    };

    /// <summary>Rolls 1-3 items. Weapon pieces only drop for weapons the player doesn't have yet.</summary>
    public static List<char> RollLoot(Random rng, Player p)
    {
        var table = new List<(char glyph, int weight)>
        {
            ('h', 20), ('q', 12), ('b', 18), ('g', 14), ('r', 8), ('u', 4),
        };
        if (!p.HasWeapon[1]) table.Add(('w', 5));
        if (!p.HasWeapon[2]) table.Add(('x', 3));
        int total = table.Sum(t => t.weight);
        var loot = new List<char>();
        int n = rng.Next(1, 4);
        for (int k = 0; k < n; k++)
        {
            int roll = rng.Next(total);
            foreach (var (g, w) in table)
            {
                if (roll < w) { loot.Add(g); break; }
                roll -= w;
            }
            if (loot[^1] is 'w' or 'x') table.RemoveAll(t => t.glyph == loot[^1]); // one of each at most
            total = table.Sum(t => t.weight);
        }
        return loot;
    }
}
