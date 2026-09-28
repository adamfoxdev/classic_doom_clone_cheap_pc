namespace HexenSharp;

public sealed partial class Game
{
    /// <summary>Where the campaign is saved; null (as in tests) means no save file.</summary>
    public string SavePath;
    /// <summary>The save on disk, if there is one (read when the title menu needs it, and kept up to date as the game saves).</summary>
    public SaveGame SavedGame;
    bool _saveChecked;
    float _autoSaveClock;
    /// <summary>How often the campaign saves itself while you play, besides at every map and checkpoint.</summary>
    public const float AutoSaveEvery = 120f;

    public bool NightmareThroughout { get => _nightmareThroughout; set => _nightmareThroughout = value; }

    /// <summary>Reads the save file once (for the title menu's Continue).</summary>
    public SaveGame CheckSave()
    {
        if (!_saveChecked) { _saveChecked = true; SavedGame = SaveGame.Load(SavePath); }
        return SavedGame;
    }

    /// <summary>
    /// Whether the game in progress is one to save: a campaign through the hub (not practice, the arena, Story mode or a
    /// map being play-tested), with you alive, and not mid-flight through the Void Crossing (that goes back to your
    /// last save, on the Barren World).
    /// </summary>
    public bool CanSave => !Replaying && P != null && Hub != null && Level != null && Mode == GameMode.Playing && !TestingMap && !Practicing
                           && !ArenaMode && !StoryMode && !Level.Flight && Array.IndexOf(Hub, Level) >= 0;

    /// <summary>Saves the campaign now, if it's one to save. True when it did.</summary>
    public bool SaveNow()
    {
        if (!CanSave) return false;
        _autoSaveClock = 0;
        SavedGame = Saves.Capture(this);
        _saveChecked = true;
        SavedGame.Save(SavePath);
        return true;
    }

    /// <summary>The campaign's finished (or given up): there's nothing to continue.</summary>
    public void DeleteSave()
    {
        SavedGame = null;
        _saveChecked = true;
        try { if (SavePath != null && File.Exists(SavePath)) File.Delete(SavePath); } catch (Exception) { }
    }

    /// <summary>Counts play time towards the next autosave.</summary>
    void AutoSaveTick(float dt)
    {
        if (!CanSave) return;
        _autoSaveClock += dt;
        if (_autoSaveClock >= AutoSaveEvery) SaveNow();
    }

    /// <summary>Picks the saved campaign up where it was left. False if there's no save or it no longer fits the maps.</summary>
    public bool Continue(SaveGame s = null)
    {
        s ??= CheckSave();
        if (s == null) return false;
        if (!Enum.TryParse<PClass>(s.Player.Class, out var cls) || !Enum.TryParse<GameStyle>(s.Style, out var style)) return false;
        // a fresh hub, the usual way; then everything the save remembers goes back over it
        HubSource = Maps.BuildHub;
        TestingMap = false; Practicing = false; ArenaMode = false; StoryMode = false; Story = null; Demo = false; Rematch = null;
        Style = style;
        NgTier = s.NgTier;
        NewGame(cls);
        if (s.Levels.Count != Hub.Length || s.Levels.Where((l, i) => l.Name != Hub[i].RawName).Any()) return FailContinue();
        try { for (int i = 0; i < Hub.Length; i++) Saves.RestoreLevel(Hub[i], s.Levels[i]); }
        catch (InvalidDataException) { return FailContinue(); }

        ChestsTotal = s.ChestsTotal; RelicsTotal = s.RelicsTotal; LoreTotal = s.LoreTotal; SecretsTotal = s.SecretsTotal;
        PlayTime = s.PlayTime; RunXp = s.RunXp; RunDeaths = s.RunDeaths; Cheated = s.Cheated; NightmareThroughout = s.NightmareThroughout;
        Level = Hub[Math.Clamp(s.LevelIndex, 0, Hub.Length - 1)];
        var ps = s.Player;
        var p = P;
        p.X = ps.X; p.Y = ps.Y; p.Angle = ps.Angle; p.Pitch = ps.Pitch; p.FloorZ = ps.FloorZ; p.Z = 0;
        p.Health = ps.Health; p.Armor = ps.Armor; p.BlueMana = ps.BlueMana; p.GreenMana = ps.GreenMana; p.Flasks = ps.Flasks; p.Urns = ps.Urns;
        p.Kills = ps.Kills; p.ChestsOpened = ps.ChestsOpened; p.Relics = ps.Relics; p.LoreRead = ps.LoreRead; p.Secrets = ps.Secrets;
        p.Blocks = ps.Blocks; ps.Ore?.CopyTo(p.Ore, 0); p.HasWeapon = (bool[])ps.HasWeapon.Clone();
        // the Quake weapons found, after the class's three (a save from before them has none)
        var extras = (ps.Extras ?? Array.Empty<int>()).Where(i => i >= 0 && i < Rockets.AllWeapons().Length).Select(i => Rockets.AllWeapons()[i]).ToArray();
        p.Loadout = extras.Length > 0 ? p.Def.Weapons.Concat(extras).ToArray() : null;
        if (p.HasWeapon.Length != p.Weapons.Length) { var had = p.HasWeapon; p.HasWeapon = new bool[p.Weapons.Length]; Array.Copy(had, p.HasWeapon, Math.Min(had.Length, p.HasWeapon.Length)); }
        if (ps.Ammo is { Length: QuakeAmmo.Kinds }) ps.Ammo.CopyTo(p.Ammo, 0);
        static WeaponMod[] ModsFrom(int[] a) => a is { Length: 3 } ? a.Select(m => Enum.IsDefined((WeaponMod)m) ? (WeaponMod)m : WeaponMod.None).ToArray() : new WeaponMod[3];
        static int[] RanksFrom(int[] a) => a is { Length: 3 } ? a.Select(r => Math.Clamp(r, 0, WeaponMods.MaxRank)).ToArray() : new int[3];
        p.Mods = ModsFrom(ps.Mods); p.Mods2 = ModsFrom(ps.Mods2); p.ModRanks = RanksFrom(ps.ModRanks); p.ModRanks2 = RanksFrom(ps.ModRanks2);
        p.Weapon = ps.Weapon >= 0 && ps.Weapon < p.HasWeapon.Length && p.HasWeapon[ps.Weapon] ? ps.Weapon : 0; p.PendingWeapon = -1;
        p.SteelKey = ps.SteelKey; p.FireKey = ps.FireKey; p.HasJetpack = ps.HasJetpack; p.Fuel = ps.Fuel;
        // standing on the portal you arrived by: it waits until you step off
        p.PortalLock = char.IsDigit(Level.MarkAt(p.X, p.Y));
        if (s.Checkpoint is { } c && c.LevelIndex >= 0 && c.LevelIndex < Hub.Length)
            Checkpoint = new Checkpoint { Level = Hub[c.LevelIndex], Index = c.Index, Health = c.Health, Armor = c.Armor, X = c.X, Y = c.Y, Floor = c.Floor, Angle = c.Angle };
        Messages.Clear();
        Say($"Welcome back. {Level.EntryMessage}");
        SavedGame = s;
        CurrentReplay = null; // a replay can't start from a save
        _autoSaveClock = 0;
        return true;
    }

    bool FailContinue()
    {
        Mode = GameMode.Title; // so going back to the title doesn't save this half-built game over the old one
        GoToTitle();
        Say("The saved game doesn't match these maps any more; it can't be continued.");
        Menu.Notice = "THE SAVED GAME NO LONGER FITS THE MAPS."; Menu.NoticeTime = 4;
        return false;
    }
}
