namespace HexenSharp;

/// <summary>
/// Player-facing names in the current visual style. The game is written with the fantasy names; in the
/// sci-fi style whole phrases are swapped for their space-station equivalents. Anything not listed
/// (custom map names, numbers, most prose) passes through unchanged.
/// </summary>
public static class Words
{
    static readonly Dictionary<string, string> SciFi = new()
    {
        // places
        ["Winnowing Hall"] = "Hab Ring",
        ["Frozen Keep"] = "Cryo Labs",
        ["The Frozen Keep"] = "The Cryo Labs",
        ["Darkmere Crypt"] = "Hydroponics Bay",
        ["Chaos Arena"] = "Combat Sim",
        ["Windspire"] = "Comms Spire",
        ["The Windspire. Only the winged may reach the beacon at its crown."] = "The Comms Spire. Only a jetpack will get you to the summit console.",
        ["The Chaos Arena - step on the altar to begin"] = "The Combat Sim - step on the pad to begin",
        ["Deepdelve Quarry"] = "Asteroid Mine",
        ["Bedrock Depths"] = "Asteroid Core",
        ["Barren World"] = "Barren Planet",
        ["Void Crossing"] = "Asteroid Belt",
        ["The Void Crossing. Steer the skyship through the drifting rocks to the green moon ahead. Jump climbs, Slide dives, Fire shoots."] = "The Asteroid Belt. Fly the shuttle through the drifting rocks to the green moon ahead. Jump climbs, Slide dives, Fire shoots.",
        ["The Verdant Moon. You made it across! Portal 9 in the meadow leads home."] = "The Verdant Moon. You made it across! Teleporter 9 in the meadow leads back to the station.",
        ["Barren World. The portal burnt out behind you - mine ore to repair your wrecked skyship and fly home."] = "The Barren Planet. The teleporter burnt out behind you - mine ore to repair your wrecked shuttle and fly home.",
        ["The portal is burnt out. Repair your skyship to get home."] = "The teleporter is burnt out. Repair your shuttle to get home.",
        ["The skyship is repaired! Use it again to take off."] = "The shuttle is repaired! Use it again to take off.",
        ["The skyship needs"] = "The shuttle needs",
        ["Lift-off! You leave the barren world behind and make it home."] = "Lift-off! You leave the barren planet behind and make it back to the station.",
        ["SKYSHIP READY - USE IT TO TAKE OFF"] = "SHUTTLE READY - USE IT TO TAKE OFF",
        ["SKYSHIP REPAIRS"] = "SHUTTLE REPAIRS",
        ["USE THE SKYSHIP TO LOAD ORE"] = "USE THE SHUTTLE TO LOAD ORE",
        ["iron ore"] = "titanium ore",
        ["moonstone"] = "power crystal",
        ["brimstone"] = "fuel ore",
        ["IRON ORE"] = "TITANIUM ORE",
        ["MOONSTONE"] = "POWER CRYSTAL",
        ["BRIMSTONE"] = "FUEL ORE",
        ["The Bedrock Depths. Solid rock all around - dig ahead, below or above."] = "The Asteroid Core. Solid rock all around - dig ahead, below or above.",
        ["Deepdelve Quarry. The miners sealed every tunnel with rubble - smash your way through."] = "The Asteroid Mine. Cave-ins have sealed every tunnel - blast your way through.",
        // classes
        ["Fighter"] = "Marine",
        ["Cleric"] = "Engineer",
        ["Mage"] = "Psion",
        ["Baratus. Tough and fast, fights up close."] = "Sgt. Baratus. Tough and fast, fights up close.",
        ["Parias. Balanced: mace, poison and fire."] = "Parias. Balanced: baton, bio-gun and flamer.",
        ["Daedolon. Frail, but deadly at range."] = "Daedolon. Frail, but deadly at range.",
        // weapons
        ["Spiked Gauntlets"] = "Power Fist",
        ["Timon's Axe"] = "Vibro Blade",
        ["Hammer of Retribution"] = "Grav Launcher",
        ["Mace of Contrition"] = "Shock Baton",
        ["Serpent Staff"] = "Bio Rifle",
        ["Firestorm"] = "Flamer",
        ["Sapphire Wand"] = "Blaster",
        ["Frost Shards"] = "Shard Gun",
        ["Arc of Death"] = "Arc Rifle",
        [" (mana)"] = " (ammo)",
        // monsters
        ["Ettin"] = "Brute Mech",
        ["Afrit"] = "Drone",
        ["Centaur"] = "Strider",
        ["Slaughtaur"] = "Siege Strider",
        ["Dark Bishop"] = "Psi Wraith",
        ["Heresiarch"] = "Overmind",
        // items
        ["Crystal Vial"] = "Stim pack",
        ["Quartz Flask"] = "Medkit",
        ["Mystic Urn"] = "Nano canister",
        ["Blue Mana"] = "Energy cells",
        ["Green Mana"] = "Plasma cells",
        ["Mesh Armor"] = "Armor vest",
        ["a weapon piece"] = "a weapon crate",
        ["Quartz Flask (press F to use)"] = "Medkit (press F to use)",
        ["Mystic Urn (press F to use)"] = "Nano canister (press F to use)",
        ["Quartz Flask: +25 health"] = "Medkit: +25 health",
        ["Mystic Urn: fully healed!"] = "Nano canister: fully healed!",
        ["Steel Key! It must open a door somewhere in the hub."] = "Blue keycard! It must open a door somewhere on the station.",
        ["Fire Key! A scorched door awaits it."] = "Red keycard! A sealed door awaits it.",
        ["Chest: "] = "Crate: ",
        ["Wings of Wrath"] = "Jetpack",
        ["Wings of Wrath: recharged"] = "Jetpack: refuelled",
        ["Wings of Wrath! Hold Q to fly. Hold Slide to sink."] = "Jetpack! Hold Q to fly. Hold Slide to sink.",
        ["The Wings of Wrath falter!"] = "Jetpack out of fuel!",
        ["WINGS"] = "JET",
        // doors, switches and the boss
        ["You need the Steel Key to open this door."] = "You need the blue keycard to open this door.",
        ["You need the Fire Key to open this door."] = "You need the red keycard to open this door.",
        ["The portcullis will not budge. Perhaps a lever..."] = "The force field holds. Perhaps a switch...",
        ["The portcullis will not budge. Levers... and pressure plates?"] = "The force field holds. Switches... and pressure pads?",
        ["You hear a gate grind open..."] = "You hear a force field power down...",
        ["The gate rumbles shut!"] = "The force field snaps back on!",
        ["The exit is sealed by the Heresiarch's magic."] = "The exit is sealed by the Overmind's shield.",
        ["The Heresiarch awakens!"] = "The Overmind awakens!",
        ["The Heresiarch is vanquished! The exit portal awakens."] = "The Overmind is destroyed! The exit teleporter powers up.",
        ["A Heresiarch falls!"] = "An Overmind falls!",
        ["The trials of chaos begin! Survive the waves."] = "Combat simulation started! Survive the waves.",
        ["The altar's bell rings softly. The old games sleep, and the arena is at peace."] = "The pad chimes softly. The simulation is offline; the arena is at peace.",
        // screens and HUD
        ["BLUE"] = "CELLS",
        ["GREEN"] = "PLSMA",
        [" (BLUE MANA)"] = " (ENERGY CELLS)",
        [" (GREEN MANA)"] = " (PLASMA CELLS)",
        ["THE HERESIARCH HAS FALLEN."] = "THE OVERMIND HAS FALLEN.",
        ["A HERESIARCH APPROACHES"] = "AN OVERMIND APPROACHES",
        ["CHAOS ARENA"] = "COMBAT SIM",
        ["THE CHAOS ARENA: SURVIVE THE WAVES"] = "THE COMBAT SIM: SURVIVE THE WAVES",
        ["Step on the altar when you're ready."] = "Step on the pad when you're ready.",
        ["A TINY HEXEN-STYLE DUNGEON CRAWLER IN C#"] = "A TINY SCI-FI STATION SHOOTER IN C#",
        ["NO COMBAT: THE CREATURES ARE PEACEFUL."] = "NO COMBAT: THE MACHINES ARE PEACEFUL.",
        ["FIGHT YOUR WAY THROUGH THE HUB,"] = "FIGHT YOUR WAY THROUGH THE STATION,",
        ["SOLVE ITS PUZZLES AND SLAY THE HERESIARCH."] = "SOLVE ITS PUZZLES AND DESTROY THE OVERMIND.",
        ["Secret treasure"] = "Secret cache",
        ["The lift carries you up to your checkpoint."] = "The lift pad beams you up to your checkpoint.",
        ["Lore stone"] = "Data terminal",
        ["LORE STONE"] = "DATA LOG",
    };

    /// <summary>Whole words swapped inside any sentence, for messages built with numbers or names in them.</summary>
    static readonly (System.Text.RegularExpressions.Regex from, string to)[] SciFiWords = new[]
    {
        ("RELICS", "ARTIFACTS"), ("RELIC", "ARTIFACT"), ("Relics", "Artifacts"), ("Relic", "Artifact"),
        ("relics", "artifacts"), ("relic", "artifact"), ("an ancient artifact", "an alien artifact"),
        ("LORE STONES", "DATA LOGS"), ("lore stones", "data logs"),
    }.Select(w => (new System.Text.RegularExpressions.Regex(@"\b" + w.Item1 + @"\b"), w.Item2)).ToArray();

    /// <summary>The phrase in the current visual style.</summary>
    public static string T(string s)
    {
        if (Art.Style != ArtStyle.SciFi || s == null) return s;
        if (SciFi.TryGetValue(s, out var r)) return r;
        foreach (var (from, to) in SciFiWords) s = from.Replace(s, to);
        return s;
    }
}
