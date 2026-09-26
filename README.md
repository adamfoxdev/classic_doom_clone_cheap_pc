# Hexen Sharp

A small Hexen-style first-person dungeon crawler written in C#. It's built to run on cheap PCs: everything is
software-rendered into a 320×200 framebuffer, and all textures, sprites and sounds are generated in code.
There are no asset files.

![Great hall](docs/great_hall.png)
![Frozen Keep](docs/frozen_keep.png)
![Heresiarch](docs/heresiarch.png)

## Features

- **Three classes**, each with their own three weapons, like Hexen:
  | Class | 1 (no mana) | 2 (blue mana) | 3 (green mana) |
  |---|---|---|---|
  | Fighter | Spiked Gauntlets | Timon's Axe | Hammer of Retribution |
  | Cleric | Mace of Contrition | Serpent Staff | Firestorm |
  | Mage | Sapphire Wand | Frost Shards | Arc of Death |
- **Blue and green mana.** The Fighter's axe still works without mana, just weaker.
- **Hub levels.** Portals connect *Winnowing Hall* and *The Frozen Keep*. Each map keeps its state
  (dead monsters, opened doors, pulled levers) when you leave and come back.
- **Puzzles.** A lever raises a portcullis elsewhere in the map, the Steel Key opens a locked door, and the exit
  stays sealed until the Heresiarch is dead.
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
| `E` / `Space` | Use (doors, levers) |
| `1` `2` `3` / mouse wheel | Select weapon |
| `F` | Use a healing item (Quartz Flask, else Mystic Urn) |
| `Shift` | Walk |
| `Tab` / `M` | Automap |
| `Esc` | Pause (`Q` quits from the pause menu) |
| `F12` | Save a screenshot |

## Walkthrough (spoilers)

1. In Winnowing Hall, grab the class weapon piece in the north-east room.
2. Pull the lever on the great hall's south wall. The portcullis opens to the courtyard.
3. Step on the blue portal to reach the Frozen Keep. Find the Keep's weapon piece, then pull the lever
   on the east wall to open the vault with the **Steel Key**.
4. Take the portal back and open the steel door off the courtyard. Kill the Heresiarch, then step on the red
   exit rune.

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
| `src/Level.cs` | Map parsing, doors, collision, line of sight, and the hub's two maps |
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
| `S` | steel-key door | `1`–`9` | portal (links to the same digit in another map) |
| `P` | portcullis (opened by a lever) | `E` | exit (sealed until the boss dies) |
| `L` | lever | `e` `a` `c` `C` `H` | Ettin, Afrit, Centaur, Slaughtaur, Heresiarch |
| `h` `q` `u` | Crystal Vial, Quartz Flask, Mystic Urn | `b` `g` | blue / green mana |
| `k` | Steel Key | `r` | Mesh Armor |
| `w` `x` | weapon piece for slot 2 / slot 3 | `t` `p` `T` | torch, pillar, tree |
