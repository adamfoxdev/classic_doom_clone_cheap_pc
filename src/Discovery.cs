namespace HexenSharp;

public enum GameStyle { Classic, Relaxed }

/// <summary>A carved tablet you can read with Use. Found in both modes.</summary>
public sealed class LoreStone : Thing
{
    /// <summary>Which map and which stone (in reading order) this is; the text follows the visual style.</summary>
    public string Map;
    public int Index = -1;
    public string Text => Discovery.LoreText(Map, Index);
    public bool Read;
    public LoreStone() { Solid = true; Radius = 0.25f; SpriteW = 0.5f; SpriteH = 0.62f; }
    public override Tex Sprite(float time) => Read ? Art.LoreStoneRead : Art.LoreStone;
}

/// <summary>Relics, lore text and exploration tracking for the relaxed "discovery" mode.</summary>
public static class Discovery
{
    public static string[] RelicNames => Art.Style == ArtStyle.SciFi ? SciFiRelicNames : FantasyRelicNames;

    static readonly string[] SciFiRelicNames =
    {
        "Xeno Idol", "Quantum Core", "Ancient Probe", "Star Chart Crystal", "Void Compass", "Captain's Log",
        "Alien Skull", "Gravity Pearl", "Plasma Lily", "Signal Beacon", "Cryo Seed", "Founders' Badge",
        "Ion Hourglass", "Dark Matter Shard", "Singing Monolith", "Station Seal", "Ore Heart", "Miner's Lamp", "Core Sample", "Drill Bit Zero", "Flight Recorder", "Dust Rose",
    };

    public static string LoreText(string map, int index)
    {
        var table = Art.Style == ArtStyle.SciFi ? SciFiLore : Lore;
        if (map != null && table.TryGetValue(map, out var texts) && index >= 0 && index < texts.Length) return texts[index];
        return Art.Style == ArtStyle.SciFi ? "The data is corrupted beyond recovery." : "The carving has worn away.";
    }

    /// <summary>Station logs for the sci-fi style, in the same order as the fantasy lore.</summary>
    public static readonly Dictionary<string, string[]> SciFiLore = new()
    {
        ["Winnowing Hall"] = new[]
        {
            "Hab Ring log: crew quarters were evacuated after the Overmind came online. Life support still hums for no one.",
            "Engineering note: the gate switch in this hall drops the force field to the observation deck.",
            "Maintenance crawlspace. Someone scratched the names of the whole construction crew into the plating.",
            "Three teleporters were installed by the Overmind itself. None of them are on the station schematics.",
        },
        ["Frozen Keep"] = new[]
        {
            "Cryo Labs, day 212: the samples stay frozen. We do not. The heaters failed a week ago.",
            "Security: the vault field switch is behind a red-keycard door. The red card was taken to Hydroponics.",
            "The blue keycard was sent up the Comms Spire for safekeeping. This teleporter goes there. Bring a jetpack.",
            "A heated nook behind a panel. Someone slept here, and taped a star map to the wall.",
        },
        ["Darkmere Crypt"] = new[]
        {
            "Hydroponics was meant to feed the station. The psi wraiths drift between the vats now, humming.",
            "Two switches and two pressure pads: the vault door only trusts a patient technician.",
            "Past the vault field: the red keycard, locked away by crew who feared what waits in the Cryo Labs.",
            "Condensation has pooled in this forgotten service bay for years. It tastes of rust and coolant.",
        },
        ["Windspire"] = new[]
        {
            "Relay deck 3. Technicians used to jet between these decks every shift. The antenna array above still hums.",
            "Hidden storage locker. Whoever stashed this knew that nobody without a jetpack would ever find it.",
            "Summit console: this switch unlocks the vault at the base of the spire, where the blue keycard is stored.",
            "Maintenance ledge. Someone taped a note to the rail: 'Watch your fuel gauge. It's a long way down.'",
            "Comms Spire access. The decks rise far beyond any jump. Grab a jetpack and fly to the summit console.",
        },
        ["Chaos Arena"] = new[]
        {
            "The combat simulator trained the station's marines. The spawn pads still remember every drill.",
            "Step on the central pad to restart the simulation. Management accepts no responsibility.",
            "Behind the scoreboard: a quiet room where the champions rested between rounds.",
        },
        ["Deepdelve Quarry"] = new[]
        {
            "Asteroid Mine, shaft 2. The crew collapsed every tunnel to slow the Overmind's drones. Loose rock breaks if you hit it hard enough.",
            "Foreman's lockup. Last log entry: quotas met, and a sealed panel in the east wall that 'officially does not exist'.",
        },
        ["Barren World"] = new[]
        {
            "Shuttle repair checklist: 6 titanium for the hull, 4 power crystals for the drive, 3 fuel ore for the tanks. The rocks here are full of it.",
        },
    };

    static readonly string[] FantasyRelicNames =
    {
        "Chalice of Ages", "Crown of Winnowing", "Orb of Dusk", "Codex of Silence", "Serpent Idol", "Bell of the Keep",
        "Marsh Lantern", "Bishop's Mitre", "Star Map", "Ember Heart", "Frost Circlet", "Champion's Laurel",
        "Mirror of Tides", "Obsidian Quill", "Hourglass of Ash", "Seal of the Hub", "Quarryman's Pick", "Geode of Echoes", "Bedrock Crown", "Delver's Candle", "Skyship Compass", "Ashen Bloom",
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
            "The Steel Key was cast from the Keep's last bell and carried up the Windspire, where only the winged could fetch it.",
            "A frost-rimed nook. Someone sheltered here through a long winter and scratched a map of the stars on the wall.",
        },
        ["Darkmere Crypt"] = new[]
        {
            "Darkmere's bishops once blessed the marsh. Now they drift through it, blurred and whispering.",
            "Two levers and two stones: the crypt gate opens only for those who are patient.",
            "Beyond the gate lies the Fire Key, sealed away by those who feared what burns behind the Keep's door.",
            "Rain has pooled in this forgotten cell for centuries. It tastes of iron and old prayers.",
        },
        ["Windspire"] = new[]
        {
            "Halfway up, the wind is old. It remembers when the spire's keepers flew every dawn to light the beacon.",
            "A hidden eyrie. The spire's keepers hid their treasure where only the winged could ever steal it.",
            "The beacon of the Windspire. Pull the lever, and the vault far below yields the Steel Key to whoever climbed this high.",
            "Few reached this ledge without wings. Those who did carved their names here, and a warning: rest before you leap.",
            "The Windspire. Its ledges climb far beyond any leap; only the Wings of Wrath will carry you to the beacon.",
        },
        ["Chaos Arena"] = new[]
        {
            "The Chaos Arena was built for sport. The crowds are long gone, but the spawning runes still remember.",
            "In gentler days the altar only rang a bell. Step on it, if you dare to wake the old games.",
            "Behind the champions' wall lies a quiet room where victors rested and the defeated were mourned.",
        },
        ["Deepdelve Quarry"] = new[]
        {
            "Deepdelve gave the hub its stone. When the Heresiarch came, the quarrymen brought the roof down behind them. Strike the rubble; it gives way.",
            "The quarrymen's strongroom. The foreman's last tally: forty carts of marble, one of gold, and a hollow in the east wall nobody was to speak of.",
        },
        ["Barren World"] = new[]
        {
            "A shipwright's note, scratched on the hull: six of iron for the keel, four moonstones for the lift, three of brimstone for the burners. The rocks here hold all of it.",
        },
    };

    /// <summary>
    /// Hides relics in a map, favouring dead ends and far-flung corners, away from doors, puzzles and other things.
    /// </summary>
    public static int ScatterRelics(Level lv, Random rng, int count, Func<string> nextName)
    {
        var (sx, sy) = lv.ArrivalCell();
        // anywhere you can get to, flying up to high ledges included
        var dist = lv.Distances(sx, sy, Level.Move.Fly);
        var candidates = new List<(int cell, float score)>();
        for (int i = 0; i < lv.Cells.Length; i++)
        {
            // on a dig map relics are buried: a pocket carved deep inside the rock
            bool buried = lv.Dig && lv.Cells[i] == Level.Rubble;
            if (dist[i] < 4 || (lv.Cells[i] != '\0' && !buried) || lv.Marks[i] != '\0') continue;
            int x = i % lv.W, y = i / lv.W, walls = 0;
            if (buried)
            {
                if (new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(o => lv.Cell(x + o.Item1, y + o.Item2) != Level.Rubble)) continue;
                candidates.Add((i, dist[i] + (float)rng.NextDouble() * 8));
                continue;
            }
            bool nearDoor = false;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                char c = lv.Cell(x + dx, y + dy);
                if (Level.IsDoor(c) || c == 'L' || Level.IsRubble(c)) nearDoor = true;
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
            if (lv.Cells[cell] == Level.Rubble) { lv.Cells[cell] = '\0'; lv.BlockHp[cell] = 0; }
            lv.Things.Add(MakeRelic(x + 0.5f, y + 0.5f, lv, nextName()));
            placed++;
        }
        return placed;
    }

    public static Pickup MakeRelic(float x, float y, Level lv, string name)
    {
        int variant = name.Sum(ch => ch) % Art.Relics.Length;
        return new Pickup(PickupKind.Relic, 0.42f, variant) { X = x, Y = y, Level = lv, Name = name };
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
