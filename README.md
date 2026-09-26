# Hexen Sharp

A small Hexen-style first-person dungeon crawler written in C#. It's built to run on cheap PCs: everything is
software-rendered into a 320×200 framebuffer, and all textures, sprites and sounds are generated in code.
There are no asset files.

![Great hall](docs/great_hall.png)
![Frozen Keep](docs/frozen_keep.png)
![Heresiarch](docs/heresiarch.png)
![Darkmere Crypt](docs/darkmere_crypt.png)
![Chaos Arena](docs/chaos_arena.png)
![Console](docs/console.png)

## Features

- **Three classes**, each with their own three weapons, like Hexen:
  | Class | 1 (no mana) | 2 (blue mana) | 3 (green mana) |
  |---|---|---|---|
  | Fighter | Spiked Gauntlets | Timon's Axe | Hammer of Retribution |
  | Cleric | Mace of Contrition | Serpent Staff | Firestorm |
  | Mage | Sapphire Wand | Frost Shards | Arc of Death |
- **Blue and green mana.** The Fighter's axe still works without mana, just weaker.
- **Hub levels.** Portals connect *Winnowing Hall*, *The Frozen Keep*, *Darkmere Crypt* and the optional
  *Chaos Arena*. Each map keeps its state (dead monsters, opened doors, pulled levers) when you leave and come back.
- **Puzzles.** Levers raise portcullises (the crypt's gate needs *both* of its levers), the Fire Key and Steel Key
  open locked doors in other maps, and the exit stays sealed until the Heresiarch is dead.
- **Chaos Arena waves.** Step on the golden altar to start endless waves. Each wave has more monsters and
  tougher types (Afrits from wave 2, Centaurs from 3, Slaughtaurs from 5). Monster health, damage and speed
  scale up every wave, and every fifth wave adds Heresiarchs. Supplies appear at the altar after each wave.
- **Jumping and sliding.** Jump over low missiles and melee swings; slide for a burst of speed and to duck
  under missiles.
- **Developer console (`~`)** for changing game mechanics, plus Hexen's classic cheat codes.
- **Monsters:** Ettins, Afrits (flying fire gargoyles), Centaurs, Slaughtaurs and the Heresiarch boss.
  Monsters wake on sight or on noise, open doors, and use melee and/or missiles.
- **Inventory:** Quartz Flasks and Mystic Urns, used with `F`.
- **Renderer:** grid raycaster with textured floors and ceilings, outdoor sky areas, distance fog (black in
  the hall, white in the Frozen Keep), rising doors, see-through portcullises, mouse look up/down,
  depth-buffered sprites, an automap and screen flashes.
- Synthesized sound effects played through Raylib.

## Running

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download). Windowing, input and audio come from
[Raylib-cs](https://github.com/ChrisDill/Raylib-cs), which NuGet fetches with native binaries for
Windows, Linux and macOS.

```sh
dotnet run -c Release
```

## Controls

| Key | Action |
|---|---|
| `W` `A` `S` `D` / arrow keys | Move / strafe / turn |
| Mouse | Look (including up/down) |
| Left click / `Ctrl` | Attack |
| `E` | Use (doors, levers) |
| `Space` | Jump |
| `C` | Slide (while moving) |
| `1` `2` `3` / mouse wheel | Select weapon |
| `F` | Use a healing item (Quartz Flask, else Mystic Urn) |
| `Shift` | Walk |
| `Tab` / `M` | Automap |
| `Esc` | Pause (`Q` quits from the pause menu) |
| `F12` | Save a screenshot |
| `~` | Developer console |

## Walkthrough (spoilers)

1. In Winnowing Hall, grab the class weapon piece in the north-east room.
2. Pull the lever on the great hall's south wall. The portcullis opens to the courtyard.
3. Step on blue portal **1** to reach the Frozen Keep and grab its weapon piece (middle room).
   The east room is behind a fire door.
4. Take portal **2** (the Keep's west room) to Darkmere Crypt. Pull *both* levers (north-east and south-west rooms)
   to raise the gate to the **Fire Key**.
5. Back in the Keep, open the fire door, pull the lever on the east wall, and take the **Steel Key** from the vault.
6. Return to Winnowing Hall and open the steel door off the courtyard. Kill the Heresiarch, then step on the red
   exit rune.

Optional: portal **3** in the courtyard leads to the Chaos Arena for wave survival.

## Console and cheats

Press `~` to open the console (the game pauses). `Tab` completes names, `Up`/`Down` recall history,
`PgUp`/`PgDn` scroll, `Esc` closes. Type `help` for everything; the most useful commands:

| Command | Effect |
|---|---|
| `vars` | list every tweakable setting and its current value |
| `set <var> <value>` (or just `<var> <value>`) | change a setting, e.g. `speed 1.5`, `fov 90`, `gravity 6` |
| `reset` | restore default settings |
| `god`, `noclip`, `notarget`, `freeze` | toggles |
| `give all\|health\|mana\|weapons\|keys\|items\|armor` | give yourself things |
| `summon <ettin\|afrit\|centaur\|slaughtaur\|heresiarch\|flask\|...>` | spawn something in front of you |
| `map <number\|name>` | warp to a hub map (`map 4` = Chaos Arena) |
| `kill`, `reveal`, `pos`, `tp x y`, `class <name>`, `restart`, `quit` | misc |

Settings: `speed`, `sens`, `damage`, `monsterdamage`, `monsterspeed`, `firerate`, `manacost`, `fog`, `fov`,
`gravity`, `jump`, `slidespeed`, `god`, `noclip`, `notarget`, `freeze`, `infinitemana`, `fullbright`, `showfps`.

Hexen's cheat codes work when typed during play (or in the console):
`satan` (god), `casper` (noclip), `nra` (all weapons & mana), `indiana` (items), `locksmith` (keys),
`clubmed` (health), `butcher` (kill all), `mapsco` (reveal map), `visitN` (warp to hub map N).

## Developer tools

```sh
dotnet run -c Release -- --selftest       # validates maps (reachability) and runs scripted gameplay checks
dotnet run -c Release -- --shots shots    # renders scripted screenshots headlessly into ./shots
```

## Code layout

| File | Purpose |
|---|---|
| `src/Program.cs` | Raylib window, input mapping, framebuffer upload |
| `src/Game.cs` | Game state, player, classes and weapons, monster AI, projectiles, pickups, doors, portals |
| `src/Level.cs` | Map parsing, doors, collision, line of sight, and the hub's four maps |
| `src/Arena.cs` | Wave survival: wave composition, difficulty scaling, spawning, rewards |
| `src/DevConsole.cs` | `~` console, tweakable settings, cheat codes |
| `src/Entities.cs` | Things: monsters (and their stats), projectiles, pickups, decorations |
| `src/Renderer.cs` | Software raycaster, sprites, HUD, automap, menus |
| `src/Art.cs` | Procedural textures, sprites and first-person weapons |
| `src/Audio.cs` | Procedural sound effects |
| `src/Gfx.cs` | Colour helpers, drawing canvas, bitmap font, PNG writer |
| `src/Headless.cs` | `--selftest` and `--shots` |

### Map legend

Maps are ASCII grids in `src/Level.cs`:

| Glyph | Meaning | Glyph | Meaning |
|---|---|---|---|
| `#` `B` `W` `M` `I` `O` | walls (stone, brick, wood, moss, ice, marble) | `.` / `,` | indoor floor / outdoor floor (sky) |
| `D` | door | `@` | player start |
| `S` / `F` | steel-key / fire-key door | `1`–`9` | portal (links to the same digit in another map) |
| `P` | portcullis (opened by a lever) | `E` | exit (sealed until the boss dies) |
| `L` | lever (gates open once every lever in the map is pulled) | `e` `a` `c` `C` `H` | Ettin, Afrit, Centaur, Slaughtaur, Heresiarch |
| `h` `q` `u` | Crystal Vial, Quartz Flask, Mystic Urn | `b` `g` | blue / green mana |
| `k` / `f` | Steel Key / Fire Key | `r` | Mesh Armor |
| `*` | arena spawn rune | `!` | arena altar (starts the waves) |
| `w` `x` | weapon piece for slot 2 / slot 3 | `t` `p` `T` | torch, pillar, tree |
