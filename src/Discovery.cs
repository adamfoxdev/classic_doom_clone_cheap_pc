namespace HexenSharp;

public enum GameStyle { Classic, Relaxed }

/// <summary>A carved tablet you can read with Use. Found in both modes.</summary>
public sealed class LoreStone : Thing
{
    public string Text = "The carving has worn away.";
    public bool Read;
    public LoreStone() { Solid = true; Radius = 0.25f; SpriteW = 0.5f; SpriteH = 0.62f; }
    public override Tex Sprite(float time) => Read ? Art.LoreStoneRead : Art.LoreStone;
}

/// <summary>Relics, lore text and exploration tracking for the relaxed "discovery" mode.</summary>
public static class Discovery
{
    public static readonly string[] RelicNames =
    {
        "Chalice of Ages", "Crown of Winnowing", "Orb of Dusk", "Codex of Silence", "Serpent Idol", "Bell of the Keep",
        "Marsh Lantern", "Bishop's Mitre", "Star Map", "Ember Heart", "Frost Circlet", "Champion's Laurel",
        "Mirror of Tides", "Obsidian Quill", "Hourglass of Ash", "Seal of the Hub",
    };

    /// <summary>Lore for each map, in the order its stones ('&') appear reading the map row by row.</summary>
    public static readonly Dictionary<string, string[]> Lore = new()
    {
        ["Winnowing Hall"] = new[]
        {
            "Winnowing Hall once echoed with hymns. Now only the wind sings here, sifting dust through the rafters.",
            "The lever in this hall was forged to hold back the courtyard gate, and whatever waited beyond it.",
            "You found the builders' hollow. They carved their names into the stone and asked to be remembered.",
            "Three portals were raised by the Heresiarch's hand. Each leads somewhere he wished forgotten.",
        },
        ["Frozen Keep"] = new[]
        {
            "The Frozen Keep was a summer palace, before the Serpent Riders stole the sun from these hills.",
            "This lever raises the vault gate. Its keeper hid the key to his fire door far away, in the crypt below the marsh.",
            "The Steel Key was cast from the Keep's last bell, melted down so no one could ring the alarm.",
            "A frost-rimed nook. Someone sheltered here through a long winter and scratched a map of the stars on the wall.",
        },
        ["Darkmere Crypt"] = new[]
        {
            "Darkmere's bishops once blessed the marsh. Now they drift through it, blurred and whispering.",
            "Two levers and two stones: the crypt gate opens only for those who are patient.",
            "Beyond the gate lies the Fire Key, sealed away by those who feared what burns behind the Keep's door.",
            "Rain has pooled in this forgotten cell for centuries. It tastes of iron and old prayers.",
        },
        ["Chaos Arena"] = new[]
        {
            "The Chaos Arena was built for sport. The crowds are long gone, but the spawning runes still remember.",
            "In gentler days the altar only rang a bell. Step on it, if you dare to wake the old games.",
            "Behind the champions' wall lies a quiet room where victors rested and the defeated were mourned.",
        },
    };

    /// <summary>
    /// Hides relics in a map, favouring dead ends and far-flung corners, away from doors, puzzles and other things.
    /// </summary>
    public static int ScatterRelics(Level lv, Random rng, int count, Func<string> nextName)
    {
        var (sx, sy) = lv.ArrivalCell();
        var dist = lv.Distances(sx, sy);
        var candidates = new List<(int cell, float score)>();
        for (int i = 0; i < lv.Cells.Length; i++)
        {
            if (dist[i] < 4 || lv.Cells[i] != '\0' || lv.Marks[i] != '\0') continue;
            int x = i % lv.W, y = i / lv.W, walls = 0;
            bool nearDoor = false;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                char c = lv.Cell(x + dx, y + dy);
                if (Level.IsDoor(c) || c == 'L') nearDoor = true;
                else if (c != '\0') walls++;
            }
            if (nearDoor) continue;
            bool nearPuzzle = false;
            for (int j = 0; j < lv.Cells.Length && !nearPuzzle; j++)
                if ((lv.Cells[j] == 'X' || lv.Marks[j] == '^') && Math.Abs(j % lv.W - x) + Math.Abs(j / lv.W - y) < 4) nearPuzzle = true;
            if (nearPuzzle) continue;
            if (lv.Things.Any(t => Game.Dist(t.X, t.Y, x + 0.5f, y + 0.5f) < 1.2f)) continue;
            float score = dist[i] + (walls == 3 ? 12 : walls == 2 ? 5 : 0) + (float)rng.NextDouble() * 8;
            candidates.Add((i, score));
        }
        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        int placed = 0;
        var chosen = new List<int>();
        foreach (var (cell, _) in candidates)
        {
            if (placed >= count) break;
            int x = cell % lv.W, y = cell / lv.W;
            if (chosen.Any(c => Math.Abs(c % lv.W - x) + Math.Abs(c / lv.W - y) < 6)) continue; // spread them out
            chosen.Add(cell);
            lv.Things.Add(MakeRelic(x + 0.5f, y + 0.5f, lv, nextName()));
            placed++;
        }
        return placed;
    }

    public static Pickup MakeRelic(float x, float y, Level lv, string name)
    {
        int variant = Math.Abs(name.GetHashCode(StringComparison.Ordinal)) % Art.Relics.Length;
        return new Pickup(PickupKind.Relic, Art.Relics[variant], 0.42f) { X = x, Y = y, Level = lv, Name = name };
    }

    /// <summary>Share of each map's walkable floor you've laid eyes on (0..1), across the whole hub.</summary>
    public static float Explored(Level[] hub)
    {
        int seen = 0, total = 0;
        foreach (var lv in hub)
            foreach (int i in lv.WalkableFloor)
            {
                total++;
                if (lv.Seen[i]) seen++;
            }
        return total == 0 ? 0 : seen / (float)total;
    }
}
