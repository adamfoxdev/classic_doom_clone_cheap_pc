using System.Text.Json;

namespace HexenSharp;

/// <summary>
/// A saved campaign (save.json, next to your profile): enough to rebuild the hub exactly as you left it. The maps
/// themselves are rebuilt from their definitions, then everything that can change as you play is put back over them:
/// doors, levers, broken rubble and moved blocks, dug floors and ceilings, secrets, checkpoints, what's on the automap,
/// every monster, pickup, chest and lore stone, the ship, and you. Only campaign games are saved (not practice, the
/// arena, Story mode or a map you're play-testing), and never while you're dead or flying through the Void Crossing;
/// the game saves as you enter each map, at each checkpoint, and when you leave.
/// </summary>
public sealed class SaveGame
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public DateTime When { get; set; }
    public string Style { get; set; }
    public float PlayTime { get; set; }
    public int RunXp { get; set; }
    public int RunDeaths { get; set; }
    public bool Cheated { get; set; }
    public bool NightmareThroughout { get; set; }
    public int ChestsTotal { get; set; }
    public int RelicsTotal { get; set; }
    public int LoreTotal { get; set; }
    public int SecretsTotal { get; set; }
    public int LevelIndex { get; set; }
    public int NgTier { get; set; }
    public PlayerSave Player { get; set; }
    public CheckpointSave Checkpoint { get; set; }
    public List<LevelSave> Levels { get; set; } = new();

    /// <summary>What the title menu says about it: where you are, as whom, and how long you've played.</summary>
    public string Summary(Level[] hub = null)
    {
        int t = (int)PlayTime;
        string map = hub != null && LevelIndex < hub.Length ? hub[LevelIndex].Name : Levels.Count > LevelIndex ? Words.T(Levels[LevelIndex].Name) : "?";
        var cls = Enum.TryParse<PClass>(Player?.Class, out var c) ? ClassDef.All[(int)c].Name : "?";
        return $"{(NgTier > 0 ? NgPlus.Name(NgTier) + " " : "")}{cls.ToUpperInvariant()} IN {map.ToUpperInvariant()}  {t / 3600}:{t / 60 % 60:00}:{t % 60:00}";
    }

    // ------------------------------------------------------------ files

    static readonly JsonSerializerOptions Json = new() { IncludeFields = true };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static SaveGame FromJson(string json)
    {
        var s = JsonSerializer.Deserialize<SaveGame>(json, Json);
        return s is { Version: CurrentVersion, Player: not null } && s.Levels.Count > 0 ? s : null;
    }

    /// <summary>Reads a save, or null if there's none or it can't be read (a damaged save is left alone, not deleted).</summary>
    public static SaveGame Load(string path)
    {
        try { return path != null && File.Exists(path) ? FromJson(File.ReadAllText(path)) : null; }
        catch (Exception) { return null; }
    }

    public void Save(string path)
    {
        if (path == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            // write alongside, then swap in, so a crash mid-write never leaves half a save
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, ToJson());
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception) { /* the game carries on; the next save tries again */ }
    }
}

public sealed class PlayerSave
{
    public string Class;
    public float X, Y, Angle, Pitch, FloorZ, Fuel;
    public int Health, Armor, BlueMana, GreenMana, Flasks, Urns, Kills, ChestsOpened, Relics, LoreRead, Secrets, Blocks, Weapon;
    public int[] Ore;
    public bool[] HasWeapon;
    public bool SteelKey, FireKey, HasJetpack;
}

public sealed class CheckpointSave
{
    public int LevelIndex, Index, Health, Armor;
    public float X, Y, Floor, Angle;
}

public sealed class LevelSave
{
    public string Name;
    public string Cells;
    public float[] Floors, Heights, DoorOpen, DoorWait;
    public sbyte[] DoorMove;
    public int[] BlockHp, FloorHp, CeilHp;
    public string Seen, Outdoor;
    public int[] PulledLevers, CheckpointsReached, SecretsFound;
    public bool BossDead;
    public List<ThingSave> Things = new();
}

/// <summary>One thing on a map. Which fields matter depends on its Type (Monster, Pickup, Chest, Lore, Decor, Ship, Asteroid).</summary>
public sealed class ThingSave
{
    public string Type;
    public float X, Y, Z;
    // monsters
    public string Def, State;
    public int Health, MaxHealth, NextBlinkHp, Summoner = -1;
    public bool Burrowed;
    public float DamageMult = 1, SpeedMult = 1;
    // pickups and decorations
    public string Kind, Name;
    public int Variant;
    public float W, H;
    public bool Solid, Bright, ReachCeiling;
    // chests and lore stones
    public bool Opened, Read;
    public string Map;
    public int Index;
    // the ship
    public int[] Delivered;
    // asteroids
    public float BaseZ, Phase;
}

public static class Saves
{
    /// <summary>Every monster the maps can hold, by the name of its definition (Monster.Ettin is "Ettin").</summary>
    static readonly Dictionary<string, MonsterDef> Defs = typeof(Monster).GetFields().Concat(typeof(MiniBosses).GetFields())
        .Where(f => f.IsStatic && f.FieldType == typeof(MonsterDef))
        .ToDictionary(f => f.Name, f => (MonsterDef)f.GetValue(null));
    static readonly Dictionary<MonsterDef, string> DefNames = Defs.ToDictionary(kv => kv.Value, kv => kv.Key);

    static string Bits(bool[] a) => new(a.Select(b => b ? '1' : '0').ToArray());
    static void Bits(string s, bool[] a) { for (int i = 0; i < a.Length && i < s.Length; i++) a[i] = s[i] == '1'; }

    public static SaveGame Capture(Game g)
    {
        var p = g.P;
        var s = new SaveGame
        {
            When = DateTime.Now, Style = g.Style.ToString(), PlayTime = g.PlayTime, RunXp = g.RunXp, RunDeaths = g.RunDeaths,
            Cheated = g.Cheated, NightmareThroughout = g.NightmareThroughout, ChestsTotal = g.ChestsTotal, RelicsTotal = g.RelicsTotal,
            LoreTotal = g.LoreTotal, SecretsTotal = g.SecretsTotal, LevelIndex = Array.IndexOf(g.Hub, g.Level), NgTier = g.NgTier,
            Player = new PlayerSave
            {
                Class = p.Class.ToString(), X = p.X, Y = p.Y, Angle = p.Angle, Pitch = p.Pitch, FloorZ = p.FloorZ, Fuel = p.Fuel,
                Health = p.Health, Armor = p.Armor, BlueMana = p.BlueMana, GreenMana = p.GreenMana, Flasks = p.Flasks, Urns = p.Urns,
                Kills = p.Kills, ChestsOpened = p.ChestsOpened, Relics = p.Relics, LoreRead = p.LoreRead, Secrets = p.Secrets,
                Blocks = p.Blocks, Weapon = p.PendingWeapon >= 0 ? p.PendingWeapon : p.Weapon, Ore = (int[])p.Ore.Clone(),
                HasWeapon = (bool[])p.HasWeapon.Clone(), SteelKey = p.SteelKey, FireKey = p.FireKey, HasJetpack = p.HasJetpack,
            },
        };
        if (g.Checkpoint is { } c)
            s.Checkpoint = new CheckpointSave { LevelIndex = Array.IndexOf(g.Hub, c.Level), Index = c.Index, Health = c.Health, Armor = c.Armor, X = c.X, Y = c.Y, Floor = c.Floor, Angle = c.Angle };
        foreach (var lv in g.Hub) s.Levels.Add(CaptureLevel(lv));
        return s;
    }

    static LevelSave CaptureLevel(Level lv)
    {
        var ls = new LevelSave
        {
            Name = lv.RawName, Cells = new string(lv.Cells), Floors = (float[])lv.Floors.Clone(), Heights = (float[])lv.Heights.Clone(),
            DoorOpen = (float[])lv.DoorOpen.Clone(), DoorWait = (float[])lv.DoorWait.Clone(), DoorMove = (sbyte[])lv.DoorMove.Clone(),
            BlockHp = (int[])lv.BlockHp.Clone(), FloorHp = (int[])lv.FloorHp.Clone(), CeilHp = (int[])lv.CeilHp.Clone(),
            Seen = Bits(lv.Seen), Outdoor = Bits(lv.Outdoor), PulledLevers = lv.PulledLevers.ToArray(),
            CheckpointsReached = lv.CheckpointsReached.ToArray(), SecretsFound = lv.SecretsFound.ToArray(), BossDead = lv.BossDead,
        };
        var kept = lv.Things.Where(t => !t.Removed && t is Monster or Pickup or Chest or LoreStone or Decor or Ship or Asteroid).ToList();
        foreach (var t in kept)
        {
            var ts = new ThingSave { Type = t.GetType().Name, X = t.X, Y = t.Y, Z = t.Z };
            switch (t)
            {
                case Monster m:
                    ts.Def = DefNames[m.Def]; ts.Health = m.Health; ts.MaxHealth = m.MaxHealth; ts.NextBlinkHp = m.NextBlinkHp; ts.Burrowed = m.Burrowed;
                    ts.DamageMult = m.DamageMult; ts.SpeedMult = m.SpeedMult;
                    // the dying finish dying; the rest wake up where they were (asleep if they hadn't seen you)
                    ts.State = !m.Alive ? "Dead" : m.State == AiState.Idle ? "Idle" : "Chase";
                    ts.Summoner = m.Summoner != null ? kept.IndexOf(m.Summoner) : -1;
                    break;
                case Pickup pk: ts.Kind = pk.Kind.ToString(); ts.Variant = pk.Variant; ts.Name = pk.Name; ts.W = pk.SpriteW; break;
                case Chest ch: ts.Opened = ch.Opened; break;
                case LoreStone ls2: ts.Map = ls2.Map; ts.Index = ls2.Index; ts.Read = ls2.Read; break;
                case Decor d: ts.Kind = d.Kind.ToString(); ts.W = d.SpriteW; ts.H = d.SpriteH; ts.Solid = d.Solid; ts.Bright = d.FullBright; ts.ReachCeiling = d.ReachCeiling; break;
                case Ship sh: ts.Delivered = (int[])sh.Delivered.Clone(); break;
                case Asteroid a: ts.Health = a.Health; ts.W = a.SpriteW; ts.BaseZ = a.BaseZ; ts.Phase = a.Phase; break;
            }
            ls.Things.Add(ts);
        }
        return ls;
    }

    /// <summary>Puts a saved level's state back over a freshly built one of the same map.</summary>
    public static void RestoreLevel(Level lv, LevelSave ls)
    {
        if (ls.Cells.Length != lv.Cells.Length) throw new InvalidDataException($"{lv.RawName} has changed shape since this save");
        ls.Cells.CopyTo(0, lv.Cells, 0, lv.Cells.Length);
        ls.Floors.CopyTo(lv.Floors, 0); ls.Heights.CopyTo(lv.Heights, 0);
        ls.DoorOpen.CopyTo(lv.DoorOpen, 0); ls.DoorWait.CopyTo(lv.DoorWait, 0); ls.DoorMove.CopyTo(lv.DoorMove, 0);
        ls.BlockHp.CopyTo(lv.BlockHp, 0); ls.FloorHp.CopyTo(lv.FloorHp, 0); ls.CeilHp.CopyTo(lv.CeilHp, 0);
        Bits(ls.Seen, lv.Seen); Bits(ls.Outdoor, lv.Outdoor);
        lv.PulledLevers.Clear(); lv.PulledLevers.UnionWith(ls.PulledLevers);
        lv.CheckpointsReached.Clear(); lv.CheckpointsReached.UnionWith(ls.CheckpointsReached);
        lv.SecretsFound.Clear(); lv.SecretsFound.UnionWith(ls.SecretsFound);
        lv.BossDead = ls.BossDead;

        lv.Things.Clear();
        lv.Ship = null;
        var made = new List<Thing>();
        foreach (var ts in ls.Things)
        {
            Thing t = ts.Type switch
            {
                nameof(Monster) => new Monster(Defs[ts.Def]) { Health = ts.Health, MaxHealth = ts.MaxHealth > 0 ? ts.MaxHealth : Defs[ts.Def].Health, NextBlinkHp = ts.NextBlinkHp, DamageMult = ts.DamageMult, SpeedMult = ts.SpeedMult, Burrowed = ts.Burrowed, Solid = !ts.Burrowed },
                nameof(Pickup) => new Pickup(Enum.Parse<PickupKind>(ts.Kind), ts.W, ts.Variant) { Name = ts.Name },
                nameof(Chest) => new Chest { Opened = ts.Opened },
                nameof(LoreStone) => new LoreStone { Map = ts.Map, Index = ts.Index, Read = ts.Read },
                nameof(Decor) => new Decor(ts.Kind[0], ts.W, ts.H, ts.Solid, ts.Bright) { ReachCeiling = ts.ReachCeiling },
                nameof(Ship) => new Ship(),
                nameof(Asteroid) => new Asteroid(ts.W, ts.BaseZ, ts.Phase) { Health = ts.Health },
                _ => null,
            };
            made.Add(t);
            if (t == null) continue;
            t.X = ts.X; t.Y = ts.Y; t.Z = ts.Z; t.Level = lv;
            if (t is Monster m && ts.State == "Dead") { m.State = AiState.Dead; m.Solid = false; m.Health = Math.Min(m.Health, 0); }
            else if (t is Monster mc && ts.State == "Chase") mc.State = AiState.Chase;
            if (t is Ship sh) { ts.Delivered?.CopyTo(sh.Delivered, 0); lv.Ship = sh; }
            lv.Things.Add(t);
        }
        for (int i = 0; i < ls.Things.Count; i++)
            if (made[i] is Monster m && ls.Things[i].Summoner >= 0 && made[ls.Things[i].Summoner] is Monster boss) m.Summoner = boss;
    }
}
