namespace HexenSharp;

public enum PClass { Fighter, Cleric, Mage }
public enum GameMode { Title, ClassSelect, Playing, Dead, Victory, Editor }

/// <summary>One frame of player input. Held controls are continuous; the rest are "pressed this frame".</summary>
public struct Input
{
    public float Move, Strafe, Turn;      // -1..1 from keys
    public float LookX, LookY;            // mouse delta in pixels
    public bool Fire, Walk;               // held
    public bool Use, UseItem, Map, Pause, Confirm, Up, Down, Left, Right, Screenshot; // pressed
    public int KeyPressed;                // any key/button code pressed this frame (for rebinding)
    public float MouseX, MouseY;          // mouse position in framebuffer pixels (-1 when unknown)
    public int Slot, Cycle;               // weapon slot 1..3 pressed, wheel -1/+1
    public string Typed;                  // text typed this frame (console / cheat codes)
    public bool ConsoleToggle, Backspace, Tab, PageUp, PageDown, Jump, Slide;
}

public sealed class WeaponDef
{
    public string Name;
    public int Mana;          // 0 none, 1 blue, 2 green
    public int Cost;
    public bool ManaOptional; // usable (weaker) without mana
    public float Cooldown;
    public bool Melee;
    public float Range = 1.3f;
    public int DmgMin, DmgMax;
    public ProjKind Proj;
    public int Count = 1;
    public float Spread, Speed = 10f, Splash;
    public Sfx Sound;
}

public sealed class ClassDef
{
    public string Name, Blurb;
    public float Speed, ArmorSave;
    public WeaponDef[] Weapons;

    public static readonly ClassDef[] All =
    {
        new()
        {
            Name = "Fighter", Blurb = "Baratus. Tough and fast, fights up close.",
            Speed = 1.12f, ArmorSave = 0.5f,
            Weapons = new[]
            {
                new WeaponDef { Name = "Spiked Gauntlets", Melee = true, Range = 1.1f, DmgMin = 14, DmgMax = 28, Cooldown = 0.4f, Sound = Sfx.Swing },
                new WeaponDef { Name = "Timon's Axe", Melee = true, Range = 1.4f, DmgMin = 30, DmgMax = 50, Cooldown = 0.55f, Mana = 1, Cost = 2, ManaOptional = true, Sound = Sfx.Swing },
                new WeaponDef { Name = "Hammer of Retribution", Proj = ProjKind.Hammer, DmgMin = 50, DmgMax = 75, Cooldown = 0.8f, Mana = 2, Cost = 3, Speed = 9f, Splash = 1.2f, Sound = Sfx.Magic },
            },
        },
        new()
        {
            Name = "Cleric", Blurb = "Parias. Balanced: mace, poison and fire.",
            Speed = 1.0f, ArmorSave = 0.4f,
            Weapons = new[]
            {
                new WeaponDef { Name = "Mace of Contrition", Melee = true, Range = 1.3f, DmgMin = 16, DmgMax = 32, Cooldown = 0.5f, Sound = Sfx.Swing },
                new WeaponDef { Name = "Serpent Staff", Proj = ProjKind.Serpent, DmgMin = 10, DmgMax = 18, Cooldown = 0.3f, Mana = 1, Cost = 1, Speed = 12f, Sound = Sfx.Magic },
                new WeaponDef { Name = "Firestorm", Proj = ProjKind.Flame, DmgMin = 35, DmgMax = 55, Cooldown = 0.85f, Mana = 2, Cost = 4, Speed = 8f, Splash = 1.6f, Sound = Sfx.Shoot },
            },
        },
        new()
        {
            Name = "Mage", Blurb = "Daedolon. Frail, but deadly at range.",
            Speed = 0.96f, ArmorSave = 0.3f,
            Weapons = new[]
            {
                new WeaponDef { Name = "Sapphire Wand", Proj = ProjKind.Bolt, DmgMin = 7, DmgMax = 13, Cooldown = 0.35f, Speed = 16f, Sound = Sfx.Magic },
                new WeaponDef { Name = "Frost Shards", Proj = ProjKind.Shard, DmgMin = 10, DmgMax = 16, Count = 3, Spread = 0.1f, Cooldown = 0.55f, Mana = 1, Cost = 3, Speed = 13f, Sound = Sfx.Magic },
                new WeaponDef { Name = "Arc of Death", Proj = ProjKind.Lightning, DmgMin = 40, DmgMax = 60, Cooldown = 0.5f, Mana = 2, Cost = 4, Speed = 20f, Sound = Sfx.Shoot },
            },
        },
    };
}

public sealed class Player
{
    public PClass Class;
    public ClassDef Def => ClassDef.All[(int)Class];
    public float X, Y, Angle, Pitch;
    public float Radius = 0.25f;
    public int Health = 100, Armor, BlueMana = 50, GreenMana, Flasks, Urns, Kills, ChestsOpened, Relics, LoreRead, Secrets;
    public bool[] HasWeapon = { true, false, false };
    public int Weapon, PendingWeapon = -1;
    public float Cooldown, FireAnim, Raise, Bob, BobAmount;
    public float DamageFlash, PickupFlash, TeleportFlash;
    public bool SteelKey, FireKey, PortalLock, Dead;
    public float EyeZ = 0.5f;
    public float Z, VZ;                                   // height above the floor while jumping
    public float SlideTime, SlideCd, SlideDX, SlideDY, SlideLow;
    public const float SlideLength = 0.55f, Height = 0.55f;
    public bool OnGround => Z <= 0f;
    /// <summary>Camera height: eye level, raised by jumps and lowered while sliding.</summary>
    public float ViewZ => Math.Min(0.95f, EyeZ + Z - SlideLow * 0.25f);
    public WeaponDef CurWeapon => Def.Weapons[Weapon];
}

public sealed class Game
{
    public GameMode Mode = GameMode.Title;
    public Level[] Hub;
    public Level Level;
    public Player P;
    public float Time, PlayTime;
    public int MenuIndex;
    public bool ShowMap, Paused, QuitRequested;
    public readonly List<(string text, float time)> Messages = new();
    public Action<Sfx, float> PlaySound = (_, _) => { };
    public readonly GameVars Vars = new();
    public readonly DevConsole Con;
    public float Fps;
    readonly Random _rng = new(1234);
    float _exitMsgCd;

    public readonly Bindings Binds = new();
    public readonly MenuSystem Menu;
    /// <summary>Where options and key bindings are saved; null (as in tests) means don't touch the disk.</summary>
    public string ConfigPath;

    public Game()
    {
        Con = new DevConsole(this);
        Menu = new MenuSystem(this);
        Editor = new Editor(this);
        Menu.Show(MenuPage.Main);
    }

    public void SaveSettings()
    {
        if (ConfigPath != null) Settings.Save(this, ConfigPath);
    }

    public void LoadSettings()
    {
        if (ConfigPath != null) Settings.Load(this, ConfigPath);
    }

    /// <summary>Raw key state (the editor reads keys directly rather than through bindings).</summary>
    public IKeySource Keys;
    /// <summary>Folder for custom maps; null means the editor can't save.</summary>
    public string MapsDir;
    public readonly Editor Editor;
    /// <summary>Where new games get their maps: the built-in hub, or a single map being play-tested.</summary>
    public Func<Level[]> HubSource = Maps.BuildHub;
    public bool TestingMap;

    public void OpenEditor()
    {
        Menu.Close();
        Paused = false;
        Mode = GameMode.Editor;
    }

    /// <summary>Plays a single custom map; Esc > Back to editor (or winning) returns to the editor.</summary>
    public void StartTest(MapDef map, PClass cls)
    {
        HubSource = () => new[] { map.Build() };
        TestingMap = true;
        NewGame(cls);
        Say($"Play-testing '{map.Name}'. Esc > Back to editor to return.");
    }

    public void ReturnToEditor()
    {
        TestingMap = false;
        HubSource = Maps.BuildHub;
        ReadingLore = null;
        OpenEditor();
    }

    public void GoToTitle()
    {
        if (TestingMap) { TestingMap = false; HubSource = Maps.BuildHub; }
        Mode = GameMode.Title;
        Paused = false;
        Menu.Close();
        Menu.Show(MenuPage.Main);
    }

    public int Rand(int lo, int hi) => _rng.Next(lo, hi + 1);
    public float RandF() => (float)_rng.NextDouble();

    public void Say(string s)
    {
        Messages.Add((s, 3.5f));
        if (Messages.Count > 4) Messages.RemoveAt(0);
    }

    void Sound(Sfx s, float x, float y)
    {
        float d = MathF.Sqrt((x - P.X) * (x - P.X) + (y - P.Y) * (y - P.Y));
        float v = Math.Clamp(1f - d / 18f, 0f, 1f);
        if (v > 0.02f) PlaySound(s, v);
    }

    /// <summary>Seed for chest placement and loot; null picks a fresh random layout every game.</summary>
    public int? FixedSeed;
    public int ChestsTotal, RelicsTotal, LoreTotal, SecretsTotal;
    /// <summary>Classic: fight through the hub. Relaxed: no combat; explore, read lore, find relics and secrets.</summary>
    public GameStyle Style = GameStyle.Classic;
    public bool Relaxed => Style == GameStyle.Relaxed;
    /// <summary>Text of the lore stone being read (the game pauses while it's open).</summary>
    public string ReadingLore;
    Random _loot = new();

    public void NewGame(PClass cls)
    {
        Hub = HubSource();
        _loot = new Random(FixedSeed ?? Environment.TickCount);
        ChestsTotal = 0;
        var names = Discovery.RelicNames.OrderBy(_ => _loot.Next()).ToList();
        int nameIndex = 0;
        string NextName() => names[nameIndex++ % names.Count];
        foreach (var lv in Hub)
        {
            Chests.Scatter(lv, _loot, Vars.Chests);
            ChestsTotal += lv.Things.Count(t => t is Chest);

            // treasure in secret nooks: a relic when relaxed, a Mystic Urn in classic
            foreach (var r in lv.Things.OfType<Pickup>().Where(p => p.Kind == PickupKind.Relic).ToList())
            {
                lv.Things.Remove(r);
                lv.Things.Add(Relaxed ? Discovery.MakeRelic(r.X, r.Y, lv, NextName()) : Place(new Pickup(PickupKind.Urn, Art.Urn, 0.45f), r, lv));
            }
            if (Relaxed)
            {
                Discovery.ScatterRelics(lv, _loot, 2, NextName);
                foreach (var m in lv.Things.OfType<Monster>()) { m.State = AiState.Idle; m.StrafeTime = RandF() * 3; }
            }
        }
        RelicsTotal = Hub.Sum(l => l.Things.Count(t => t is Pickup { Kind: PickupKind.Relic }));
        LoreTotal = Hub.Sum(l => l.Things.Count(t => t is LoreStone));
        SecretsTotal = Hub.Sum(l => l.SecretCount);
        ReadingLore = null;
        Level = Hub[0];
        P = new Player { Class = cls, X = Level.StartX, Y = Level.StartY, Angle = Level.StartAngle };
        Messages.Clear();
        Mode = GameMode.Playing;
        PlayTime = 0;
        Paused = false;
        Menu.Close();
        ShowMap = false;
        Say(Level.EntryMessage);
        if (Relaxed)
        {
            Say($"Relaxed mode: the creatures here are peaceful. Find the {RelicsTotal} relics to awaken the exit.");
        }
        else if (!TestingMap) Say($"You are the {P.Def.Name}. Find a way through the hub.");
    }

    static Thing Place(Thing t, Thing at, Level lv)
    {
        t.X = at.X; t.Y = at.Y; t.Level = lv;
        return t;
    }

    // ================================================================ update

    public void Update(Input inp, float dt)
    {
        if (dt > 0) Fps += (1f / dt - Fps) * 0.05f;
        dt = MathF.Min(dt, 0.05f);
        Time += dt;

        // the developer console (~) pauses the game while it is open
        if (inp.ConsoleToggle) Con.Open = !Con.Open;
        if (Con.Open)
        {
            Con.HandleInput(inp);
            return;
        }
        for (int i = Messages.Count - 1; i >= 0; i--)
        {
            var m = Messages[i];
            m.time -= dt;
            if (m.time <= 0) Messages.RemoveAt(i); else Messages[i] = m;
        }

        if (Menu.Open)
        {
            Menu.Update(inp, dt);
            return;
        }

        // reading a lore stone pauses the game until you close it
        if (ReadingLore != null)
        {
            if (inp.Use || inp.Confirm || inp.Pause || inp.Fire || inp.Jump) ReadingLore = null;
            return;
        }

        switch (Mode)
        {
            case GameMode.Title:
                Menu.Show(MenuPage.Main);
                return;
            case GameMode.ClassSelect:
                if (inp.Up) { MenuIndex = (MenuIndex + 2) % 3; PlaySound(Sfx.Swing, 0.6f); }
                if (inp.Down) { MenuIndex = (MenuIndex + 1) % 3; PlaySound(Sfx.Swing, 0.6f); }
                if (inp.Slot >= 1 && inp.Slot <= 3) { MenuIndex = inp.Slot - 1; NewGame((PClass)MenuIndex); PlaySound(Sfx.Teleport, 1); }
                else if (inp.Confirm) { NewGame((PClass)MenuIndex); PlaySound(Sfx.Teleport, 1); }
                if (inp.Pause) GoToTitle();
                return;
            case GameMode.Victory:
                if (inp.Confirm) { if (TestingMap) ReturnToEditor(); else GoToTitle(); }
                return;
            case GameMode.Editor:
                Editor.Update(inp, dt);
                return;
        }

        if (inp.Pause)
        {
            Paused = true;
            Menu.Show(MenuPage.Pause);
            PlaySound(Sfx.Swing, 0.6f);
            return;
        }
        if (inp.Map) ShowMap = !ShowMap;
        if (!string.IsNullOrEmpty(inp.Typed) && Mode == GameMode.Playing)
            foreach (char c in inp.Typed) Con.FeedCheat(c);

        PlayTime += dt;
        UpdatePlayer(inp, dt);
        UpdateWorld(dt);

        if (Mode == GameMode.Dead)
        {
            P.EyeZ = MathF.Max(0.12f, P.EyeZ - dt * 0.8f);
            if ((inp.Confirm || inp.Use) && P.EyeZ <= 0.13f) NewGame(P.Class);
        }
    }

    void UpdatePlayer(Input inp, float dt)
    {
        var p = P;
        p.DamageFlash = MathF.Max(0, p.DamageFlash - dt * 2);
        p.PickupFlash = MathF.Max(0, p.PickupFlash - dt * 3);
        p.TeleportFlash = MathF.Max(0, p.TeleportFlash - dt * 1.5f);
        if (Mode == GameMode.Dead) return;

        // look
        p.Angle += inp.LookX * 0.0025f * Vars.Sens + inp.Turn * 2.6f * dt;
        p.Pitch = Math.Clamp(p.Pitch - inp.LookY * 0.35f * Vars.Sens * (Vars.InvertMouse ? -1 : 1), -70f, 70f);

        // move
        float speed = 3.6f * p.Def.Speed * Vars.Speed * (inp.Walk ? 0.5f : 1f);
        float ca = MathF.Cos(p.Angle), sa = MathF.Sin(p.Angle);
        float mx = (ca * inp.Move - sa * inp.Strafe), my = (sa * inp.Move + ca * inp.Strafe);
        float len = MathF.Sqrt(mx * mx + my * my);
        if (len > 1) { mx /= len; my /= len; }
        float dx = mx * speed * dt, dy = my * speed * dt;

        // jumping (Space): simple ballistic hop, Hexen-style
        if (inp.Jump && p.OnGround && p.SlideTime <= 0 && Vars.JumpPower > 0)
        {
            p.VZ = Vars.JumpPower;
            PlaySound(Sfx.Jump, 0.8f);
        }
        if (!p.OnGround || p.VZ > 0)
        {
            p.VZ -= Vars.Gravity * dt;
            p.Z += p.VZ * dt;
            if (p.Z <= 0)
            {
                if (p.VZ < -2f) PlaySound(Sfx.Land, 0.7f);
                p.Z = 0; p.VZ = 0;
            }
        }

        // sliding (C): a burst of speed in the direction you're moving, camera dropped low
        p.SlideCd -= dt;
        if (inp.Slide && p.OnGround && p.SlideTime <= 0 && p.SlideCd <= 0 && len > 0.1f)
        {
            p.SlideTime = Player.SlideLength;
            p.SlideCd = Player.SlideLength + 0.45f;
            float ml = MathF.Sqrt(mx * mx + my * my);
            p.SlideDX = mx / ml; p.SlideDY = my / ml;
            PlaySound(Sfx.Slide, 0.8f);
        }
        if (p.SlideTime > 0)
        {
            p.SlideTime -= dt;
            float boost = Vars.SlideSpeed * MathF.Max(0, p.SlideTime / Player.SlideLength) * dt;
            dx += p.SlideDX * boost; dy += p.SlideDY * boost;
        }
        float lowTarget = p.SlideTime > 0 ? 1f : 0f;
        p.SlideLow += (lowTarget - p.SlideLow) * MathF.Min(1, dt * 14);

        if (Vars.NoClip)
        {
            p.X = Math.Clamp(p.X + dx, 0.3f, Level.W - 0.3f);
            p.Y = Math.Clamp(p.Y + dy, 0.3f, Level.H - 0.3f);
        }
        else
        {
            if (!Blocked(p.X + dx, p.Y, p.Radius, null)) p.X += dx;
            if (!Blocked(p.X, p.Y + dy, p.Radius, null)) p.Y += dy;
        }
        float moving = p.OnGround && p.SlideTime <= 0 ? MathF.Min(1, len) : 0f;
        p.BobAmount += (moving - p.BobAmount) * MathF.Min(1, dt * 8);
        p.Bob += dt * 9 * moving;

        // portals & exit
        char mark = Level.MarkAt(p.X, p.Y);
        if (char.IsDigit(mark))
        {
            if (!p.PortalLock) Teleport(mark);
        }
        else p.PortalLock = false;
        _exitMsgCd -= dt;
        if (mark == 'E')
        {
            if (Relaxed ? P.Relics >= RelicsTotal : Level.BossDead)
            {
                Mode = GameMode.Victory;
                PlaySound(Sfx.Teleport, 1);
                return;
            }
            if (_exitMsgCd <= 0)
            {
                Say(Relaxed ? $"The exit portal sleeps. Relics found: {P.Relics}/{RelicsTotal}." : "The exit is sealed by the Heresiarch's magic.");
                _exitMsgCd = 3;
            }
        }

        // pickups
        foreach (var t in Level.Things)
            if (t is Pickup pk && !pk.Removed && Dist(t.X, t.Y, p.X, p.Y) < 0.55f)
                TryPickup(pk);

        // actions
        if (inp.Use) UseLine(pull: inp.Walk);
        if (inp.UseItem) UseItem();

        // weapons
        if (inp.Slot >= 1 && inp.Slot <= 3) SelectWeapon(inp.Slot - 1);
        if (inp.Cycle != 0)
            for (int k = 1; k <= 3; k++)
            {
                int w = ((p.Weapon + inp.Cycle * k) % 3 + 3) % 3;
                if (p.HasWeapon[w]) { SelectWeapon(w); break; }
            }
        if (p.PendingWeapon >= 0)
        {
            p.Raise += dt * 5;
            if (p.Raise >= 1) { p.Weapon = p.PendingWeapon; p.PendingWeapon = -1; }
        }
        else p.Raise = MathF.Max(0, p.Raise - dt * 5);

        p.Cooldown -= dt;
        p.FireAnim = MathF.Max(0, p.FireAnim - dt);
        if (inp.Fire && !Relaxed && p.Cooldown <= 0 && p.PendingWeapon < 0 && p.Raise < 0.2f) Fire();
    }

    void SelectWeapon(int w)
    {
        if (!P.HasWeapon[w] || (w == P.Weapon && P.PendingWeapon < 0)) return;
        P.PendingWeapon = w;
        Say(P.Def.Weapons[w].Name);
    }

    int ManaCost(WeaponDef w) => Vars.InfiniteMana ? 0 : (int)MathF.Ceiling(w.Cost * Vars.ManaCost);
    bool HasMana(WeaponDef w) => w.Mana == 0 || (w.Mana == 1 ? P.BlueMana : P.GreenMana) >= ManaCost(w);

    void Fire()
    {
        var p = P;
        var w = p.CurWeapon;
        bool powered = HasMana(w);
        if (!powered && !w.ManaOptional)
        {
            // out of mana: fall back to the best weapon that still works
            for (int i = 2; i >= 0; i--)
                if (p.HasWeapon[i] && HasMana(p.Def.Weapons[i])) { SelectWeapon(i); break; }
            p.Cooldown = 0.3f;
            return;
        }
        if (powered && w.Mana == 1) p.BlueMana -= ManaCost(w);
        if (powered && w.Mana == 2) p.GreenMana -= ManaCost(w);
        p.Cooldown = w.Cooldown / MathF.Max(0.05f, Vars.FireRate);
        p.FireAnim = 0.22f;
        PlaySound(w.Sound, 1);
        WakeNear(p.X, p.Y, 10f);

        if (w.Melee)
        {
            Monster best = null;
            float bestD = float.MaxValue;
            foreach (var t in Level.Things)
            {
                if (t is not Monster m || !m.Alive || m.Blurring) continue;
                float d = Dist(m.X, m.Y, p.X, p.Y);
                if (d > w.Range + m.Radius) continue;
                float diff = AngleDiff(MathF.Atan2(m.Y - p.Y, m.X - p.X), p.Angle);
                if (MathF.Abs(diff) > 0.45f || !Level.Sight(p.X, p.Y, m.X, m.Y)) continue;
                if (d < bestD) { bestD = d; best = m; }
            }
            if (best != null)
            {
                int dmg = Rand(w.DmgMin, w.DmgMax);
                if (!powered) dmg /= 2;
                if (powered && w.Mana > 0) SpawnPuff(Art.Bolt[1], best.X, best.Y, best.Z + best.SpriteH * 0.5f, 0.4f);
                else SpawnPuff(Art.Fireball[1], best.X, best.Y, best.Z + best.SpriteH * 0.5f, 0.25f);
                DamageMonster(best, dmg);
                PlaySound(Sfx.Hit, 1);
            }
            return;
        }

        for (int i = 0; i < w.Count; i++)
        {
            float a = p.Angle + (i - (w.Count - 1) / 2f) * w.Spread;
            var pr = new Projectile
            {
                Kind = w.Proj, FromPlayer = true, DmgMin = w.DmgMin, DmgMax = w.DmgMax, Splash = w.Splash, Owner = null,
                X = p.X + MathF.Cos(a) * 0.3f, Y = p.Y + MathF.Sin(a) * 0.3f, Z = 0.32f,
                VX = MathF.Cos(a) * w.Speed, VY = MathF.Sin(a) * w.Speed, Level = Level,
            };
            if (w.Proj == ProjKind.Hammer || w.Proj == ProjKind.Flame) { pr.SpriteW = pr.SpriteH = 0.4f; }
            Level.Things.Add(pr);
        }
    }

    void UseLine(bool pull)
    {
        var p = P;
        if (TryReadLore() || TryOpenChest()) return;
        float ca = MathF.Cos(p.Angle), sa = MathF.Sin(p.Angle);
        for (float d = 0.1f; d < 1.3f; d += 0.05f)
        {
            int cx = (int)(p.X + ca * d), cy = (int)(p.Y + sa * d);
            char c = Level.Cell(cx, cy);
            if (c == '\0') continue;
            int i = cy * Level.W + cx;
            switch (c)
            {
                case 'D':
                    if (Level.DoorOpen[i] < 1f && Level.DoorMove[i] <= 0) { Level.OpenDoor(cx, cy); PlaySound(Sfx.Door, 1); }
                    else if (Level.DoorOpen[i] >= 1f) { Level.DoorMove[i] = -1; PlaySound(Sfx.Door, 1); }
                    break;
                case 'S':
                    if (!p.SteelKey) { Say("You need the Steel Key to open this door."); PlaySound(Sfx.Locked, 1); }
                    else if (Level.DoorOpen[i] < 1f) { Level.OpenDoor(cx, cy); PlaySound(Sfx.Door, 1); }
                    break;
                case 'F':
                    if (!p.FireKey) { Say("You need the Fire Key to open this door."); PlaySound(Sfx.Locked, 1); }
                    else if (Level.DoorOpen[i] < 1f) { Level.OpenDoor(cx, cy); PlaySound(Sfx.Door, 1); }
                    break;
                case 'P':
                    if (Level.DoorOpen[i] < 1f)
                    {
                        Say(Level.PlateCount > 0 ? "The portcullis will not budge. Levers... and pressure plates?" : "The portcullis will not budge. Perhaps a lever...");
                        PlaySound(Sfx.Locked, 1);
                    }
                    break;
                case 'L':
                    if (Level.PulledLevers.Add(i))
                    {
                        PlaySound(Sfx.Lever, 1);
                        CheckPuzzle($"Lever {Level.PulledLevers.Count} of {Level.LeverCount} pulled.");
                    }
                    break;
                case 'Z':
                    if (Level.DoorOpen[i] < 1f && Level.DoorMove[i] <= 0)
                    {
                        Level.OpenDoor(cx, cy);
                        PlaySound(Sfx.Door, 1);
                        if (Level.SecretsFound.Add(i))
                        {
                            p.Secrets++;
                            PlaySound(Sfx.Secret, 1);
                            Say($"A secret passage! ({p.Secrets}/{SecretsTotal} secrets)");
                        }
                    }
                    break;
                case 'X':
                    MoveBlock(cx, cy, pull);
                    break;
            }
            return;
        }
    }

    /// <summary>Reads the lore stone the player is facing, if any.</summary>
    bool TryReadLore()
    {
        LoreStone best = null;
        float bestD = 1.5f;
        foreach (var t in Level.Things)
        {
            if (t is not LoreStone s) continue;
            float d = Dist(s.X, s.Y, P.X, P.Y);
            if (d >= bestD || MathF.Abs(AngleDiff(MathF.Atan2(s.Y - P.Y, s.X - P.X), P.Angle)) > 0.6f) continue;
            best = s; bestD = d;
        }
        if (best == null) return false;
        if (!best.Read) { best.Read = true; P.LoreRead++; }
        ReadingLore = best.Text;
        PlaySound(Sfx.Lore, 1);
        return true;
    }

    /// <summary>Opens the closed chest the player is facing, if any.</summary>
    bool TryOpenChest()
    {
        Chest best = null;
        float bestD = 1.5f;
        foreach (var t in Level.Things)
        {
            if (t is not Chest c || c.Opened) continue;
            float d = Dist(c.X, c.Y, P.X, P.Y);
            if (d >= bestD) continue;
            if (MathF.Abs(AngleDiff(MathF.Atan2(c.Y - P.Y, c.X - P.X), P.Angle)) > 0.6f) continue;
            best = c; bestD = d;
        }
        if (best == null) return false;
        OpenChest(best);
        return true;
    }

    public void OpenChest(Chest c)
    {
        c.Opened = true;
        P.ChestsOpened++;
        PlaySound(Sfx.Chest, 1);

        // spill loot toward the player so it's easy to grab
        float dx = P.X - c.X, dy = P.Y - c.Y, l = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
        dx /= l; dy /= l;
        var loot = Chests.RollLoot(_loot, P);
        for (int i = 0; i < loot.Count; i++)
        {
            float side = (i - (loot.Count - 1) / 2f) * 0.35f;
            float x = c.X + dx * 0.6f - dy * side, y = c.Y + dy * 0.6f + dx * side;
            if (Level.BlocksPoint(x, y)) { x = c.X + dx * 0.5f; y = c.Y + dy * 0.5f; }
            var t = ThingFactory.Create(loot[i], x, y);
            t.Level = Level;
            Level.Things.Add(t);
        }
        SpawnPuff(Art.Fireball[1], c.X, c.Y, 0.35f, 0.3f);

        bool trap = !Relaxed && Level.Arena == null && _loot.NextDouble() < Chests.TrapChance;
        if (trap)
        {
            // a monster bursts out beside the chest
            var def = _loot.Next(3) switch { 0 => Monster.Ettin, 1 => Monster.Afrit, _ => Monster.Centaur };
            for (int k = 0; k < 8; k++)
            {
                float a = MathF.Atan2(dy, dx) + MathF.PI / 2 + k * MathF.PI / 4;
                float x = c.X + MathF.Cos(a) * 0.8f, y = c.Y + MathF.Sin(a) * 0.8f;
                if (Blocked(x, y, def.Radius, c)) continue;
                SpawnMonster(def, x, y, 1f, 1f, 1f);
                Say($"It's a trap! A {def.Name} bursts out!");
                break;
            }
        }
        else Say("Chest: " + string.Join(", ", loot.Select(g => Chests.LootNames[g]).Distinct()));
    }

    // ================================================================ puzzles

    /// <summary>
    /// E pushes a stone block one cell away from you; Shift+E pulls it one cell toward you (you step back).
    /// Pulling means a block can never get permanently stuck against a wall.
    /// </summary>
    void MoveBlock(int bx, int by, bool pull)
    {
        var p = P;
        float fx = bx + 0.5f - p.X, fy = by + 0.5f - p.Y;
        int sx = 0, sy = 0;
        if (MathF.Abs(fx) >= MathF.Abs(fy)) sx = Math.Sign(fx); else sy = Math.Sign(fy);

        int tx, ty;
        float nx = p.X, ny = p.Y;
        if (!pull)
        {
            tx = bx + sx; ty = by + sy;
            if (!Level.BlockCanEnter(tx, ty) || PlayerTouchesCell(tx, ty)) { Say("The block won't budge that way."); PlaySound(Sfx.Locked, 0.6f); return; }
        }
        else
        {
            tx = bx - sx; ty = by - sy;
            // step back one cell (snapping to its centre on the pull axis)
            if (sx != 0) nx = tx - sx + 0.5f; else ny = ty - sy + 0.5f;
            if (!Level.BlockCanEnter(tx, ty) || Level.BlocksCircle(nx, ny, p.Radius) || Blocked(nx, ny, p.Radius, null))
            {
                Say("No room to pull the block.");
                PlaySound(Sfx.Locked, 0.6f);
                return;
            }
            p.X = nx; p.Y = ny;
        }

        int from = by * Level.W + bx, to = ty * Level.W + tx;
        bool wasOnPlate = Level.Marks[from] == '^', nowOnPlate = Level.Marks[to] == '^';
        Level.Cells[from] = '\0';
        Level.Cells[to] = 'X';
        PlaySound(Sfx.Push, 1);
        SpawnPuff(Art.Shard[1], tx + 0.5f - sx * 0.5f, ty + 0.5f - sy * 0.5f, 0.1f, 0.4f);

        if (nowOnPlate) { PlaySound(Sfx.Lever, 0.8f); CheckPuzzle($"A pressure plate sinks under the block ({Level.PlatesCovered}/{Level.PlateCount})."); }
        else if (wasOnPlate) { PlaySound(Sfx.Lever, 0.5f); CheckPuzzle("A pressure plate clicks back up."); }
    }

    bool PlayerTouchesCell(int cx, int cy)
    {
        float qx = Math.Clamp(P.X, cx, cx + 1), qy = Math.Clamp(P.Y, cy, cy + 1);
        return Dist(qx, qy, P.X, P.Y) < P.Radius;
    }

    /// <summary>Opens the map's gates when its levers and plates are all set; plate gates drop again if not.</summary>
    void CheckPuzzle(string progress)
    {
        var lv = Level;
        if (lv.PuzzleSolved)
        {
            bool opened = false;
            for (int k = 0; k < lv.Cells.Length; k++)
                if (lv.Cells[k] == 'P' && lv.DoorOpen[k] < 1f && lv.DoorMove[k] <= 0) { lv.DoorMove[k] = 1; opened = true; }
            if (opened) Say("You hear a gate grind open...");
            return;
        }
        if (progress != null)
        {
            string rest = "";
            if (lv.LeverCount > 0 && lv.PlateCount > 0) rest = $" (levers {lv.PulledLevers.Count}/{lv.LeverCount}, plates {lv.PlatesCovered}/{lv.PlateCount})";
            Say(progress + rest);
        }
        if (lv.PlateCount == 0) return;
        bool closed = false;
        for (int k = 0; k < lv.Cells.Length; k++)
            if (lv.Cells[k] == 'P' && (lv.DoorOpen[k] > 0f || lv.DoorMove[k] > 0)) { lv.DoorMove[k] = -1; closed = true; }
        if (closed) { Say("The gate rumbles shut!"); PlaySound(Sfx.Door, 1); }
    }

    public static void OpenGates(Level lv)
    {
        for (int k = 0; k < lv.Cells.Length; k++)
            if (lv.Cells[k] == 'P' && lv.DoorOpen[k] < 1f) lv.DoorMove[k] = 1;
    }

    void UseItem()
    {
        var p = P;
        if (p.Health >= 100) { Say("You are already at full health."); return; }
        if (p.Flasks > 0 && (p.Health > 50 || p.Urns == 0)) { p.Flasks--; p.Health = Math.Min(100, p.Health + 25); Say("Quartz Flask: +25 health"); }
        else if (p.Urns > 0) { p.Urns--; p.Health = 100; Say("Mystic Urn: fully healed!"); }
        else { Say("You have no healing items."); return; }
        p.PickupFlash = 1;
        PlaySound(Sfx.Heal, 1);
    }

    void TryPickup(Pickup pk)
    {
        var p = P;
        string msg;
        switch (pk.Kind)
        {
            case PickupKind.Vial:
                if (p.Health >= 100) return;
                p.Health = Math.Min(100, p.Health + 10); msg = "Crystal Vial"; break;
            case PickupKind.Flask:
                if (p.Flasks >= 9) return;
                p.Flasks++; msg = "Quartz Flask (press F to use)"; break;
            case PickupKind.Urn:
                if (p.Urns >= 3) return;
                p.Urns++; msg = "Mystic Urn (press F to use)"; break;
            case PickupKind.BlueMana:
                if (p.BlueMana >= 200) return;
                p.BlueMana = Math.Min(200, p.BlueMana + 25); msg = "Blue Mana"; break;
            case PickupKind.GreenMana:
                if (p.GreenMana >= 200) return;
                p.GreenMana = Math.Min(200, p.GreenMana + 25); msg = "Green Mana"; break;
            case PickupKind.SteelKey:
                p.SteelKey = true; msg = "Steel Key! It must open a door somewhere in the hub."; break;
            case PickupKind.FireKey:
                p.FireKey = true; msg = "Fire Key! A scorched door awaits it."; break;
            case PickupKind.Relic:
                p.Relics++;
                msg = $"Relic found: {pk.Name ?? "an ancient relic"} ({p.Relics}/{RelicsTotal})";
                if (Relaxed && p.Relics >= RelicsTotal) msg += " - the exit portal awakens!";
                pk.Removed = true;
                p.PickupFlash = 1;
                PlaySound(Sfx.Relic, 1);
                Say(msg);
                return;
            case PickupKind.Armor:
                if (p.Armor >= 100) return;
                p.Armor = Math.Min(100, p.Armor + 50); msg = "Mesh Armor"; break;
            case PickupKind.Weapon2:
            case PickupKind.Weapon3:
                {
                    int slot = pk.Kind == PickupKind.Weapon2 ? 1 : 2;
                    bool had = p.HasWeapon[slot];
                    p.HasWeapon[slot] = true;
                    if (slot == 1) p.BlueMana = Math.Min(200, p.BlueMana + 25); else p.GreenMana = Math.Min(200, p.GreenMana + 25);
                    msg = p.Def.Weapons[slot].Name + (had ? " (mana)" : "!");
                    if (!had) SelectWeapon(slot);
                    break;
                }
            default: return;
        }
        pk.Removed = true;
        p.PickupFlash = 1;
        PlaySound(pk.Kind is PickupKind.Weapon2 or PickupKind.Weapon3 or PickupKind.SteelKey or PickupKind.FireKey ? Sfx.Item : Sfx.Pickup, 1);
        Say(msg);
    }

    void Teleport(char mark)
    {
        foreach (var lv in Hub)
        {
            if (lv == Level) continue;
            var dest = lv.FindMark(mark);
            if (dest == null) continue;
            Level = lv;
            P.X = dest.Value.x; P.Y = dest.Value.y;
            P.PortalLock = true;
            P.TeleportFlash = 1;
            // drop any in-flight projectiles from the level we left
            foreach (var t in Hub.SelectMany(l => l.Things)) if (t is Projectile or Puff) t.Removed = true;
            PlaySound(Sfx.Teleport, 1);
            Say(lv.EntryMessage);
            return;
        }
    }

    // ================================================================ world

    void UpdateWorld(float dt)
    {
        var lv = Level;
        lv.Arena?.Update(this, dt);
        lv.UpdateDoors(dt, (x, y) => CellOccupied(x, y), (x, y) => Sound(Sfx.Door, x, y));

        for (int i = 0; i < lv.Things.Count; i++)
        {
            var t = lv.Things[i];
            if (t.Removed) continue;
            switch (t)
            {
                case Monster m:
                    if (!Vars.Freeze || !m.Alive) UpdateMonster(m, dt);
                    break;
                case Projectile pr: UpdateProjectile(pr, dt); break;
                case Puff pf: pf.Tick(dt); break;
            }
        }
        lv.Things.RemoveAll(t => t.Removed);
    }

    bool CellOccupied(int cx, int cy)
    {
        if ((int)P.X == cx && (int)P.Y == cy) return true;
        if (Mode != GameMode.Dead && Dist(P.X, P.Y, cx + 0.5f, cy + 0.5f) < 0.5f + P.Radius) return true;
        foreach (var t in Level.Things)
            if (t is Monster m && m.Alive && (int)m.X == cx && (int)m.Y == cy) return true;
        return false;
    }

    /// <summary>Would a circle at (x,y) hit a wall or a solid thing (other than `self`)?</summary>
    bool Blocked(float x, float y, float r, Thing self)
    {
        if (Level.BlocksCircle(x, y, r)) return true;
        foreach (var t in Level.Things)
        {
            if (t == self || !t.Solid || t.Removed) continue;
            if (t is Monster m && !m.Alive) continue;
            float rr = r + t.Radius;
            float dx = t.X - x, dy = t.Y - y;
            if (dx * dx + dy * dy < rr * rr)
            {
                // allow moving away from something we're already overlapping
                float cx = self?.X ?? P.X, cy = self?.Y ?? P.Y;
                float odx = t.X - cx, ody = t.Y - cy;
                if (odx * odx + ody * ody < rr * rr && dx * dx + dy * dy >= odx * odx + ody * ody) continue;
                return true;
            }
        }
        if (self != null && Mode != GameMode.Dead)
        {
            float rr = r + P.Radius;
            if ((P.X - x) * (P.X - x) + (P.Y - y) * (P.Y - y) < rr * rr) return true;
        }
        return false;
    }

    void WakeNear(float x, float y, float range)
    {
        if (Vars.NoTarget) return;
        foreach (var t in Level.Things)
            if (t is Monster m && m.State == AiState.Idle && Dist(m.X, m.Y, x, y) < range)
                Wake(m);
    }

    void Wake(Monster m)
    {
        if (m.State != AiState.Idle) return;
        m.State = AiState.Chase;
        m.AttackCd = 0.5f + RandF();
        Sound(m.Def.Boss ? Sfx.BossSight : Sfx.Sight, m.X, m.Y);
        if (m.Def.Boss) Say("The Heresiarch awakens!");
    }

    void SetState(Monster m, AiState s) { m.State = s; m.StateTime = 0; m.AttackFired = false; }

    void UpdateMonster(Monster m, float dt)
    {
        m.StateTime += dt;
        float dist = Dist(m.X, m.Y, P.X, P.Y);
        bool playerAlive = Mode != GameMode.Dead;

        if (Relaxed && m.Alive) { Wander(m, dt, dist); return; }

        // Dark Bishop blur: dart sideways, see-through and untouchable
        if (m.Blurring)
        {
            m.BlurTime -= dt;
            float s = 6f * m.SpeedMult * Vars.MonsterSpeed * dt;
            float nx = m.X + m.BlurDX * s, ny = m.Y + m.BlurDY * s;
            if (!Blocked(nx, ny, m.Radius, m)) { m.X = nx; m.Y = ny; }
            else { m.BlurDX = -m.BlurDX; m.BlurDY = -m.BlurDY; }
            if (m.State == AiState.Chase) return;
        }

        switch (m.State)
        {
            case AiState.Idle:
                if (playerAlive && !Vars.NoTarget && dist < m.Def.SightRange && Level.Sight(m.X, m.Y, P.X, P.Y)) Wake(m);
                break;

            case AiState.Chase:
                {
                    m.Anim += dt;
                    m.AttackCd -= dt;
                    if (!playerAlive || Vars.NoTarget) { ChaseMove(m, dt, wander: true); break; }
                    if (m.Def.Blurs && m.AttackCd > 0.3f && dist < 12f && RandF() < dt * 0.35f) { StartBlur(m); break; }
                    bool canMelee = m.Def.MeleeRange > 0 && dist <= m.Def.MeleeRange + P.Radius;
                    if (canMelee && m.AttackCd <= 0) { SetState(m, AiState.Attack); break; }
                    if (m.Def.Missile != null && m.AttackCd <= 0 && dist < 18f && RandF() < dt * 2.5f && Level.Sight(m.X, m.Y, P.X, P.Y))
                    {
                        SetState(m, AiState.Attack);
                        break;
                    }
                    if (!canMelee) ChaseMove(m, dt, wander: false);
                    break;
                }

            case AiState.Attack:
                {
                    if (!m.AttackFired && m.StateTime >= m.Def.AttackTime * 0.5f)
                    {
                        m.AttackFired = true;
                        if (m.Def.MeleeRange > 0 && dist <= m.Def.MeleeRange + P.Radius + 0.25f)
                        {
                            Sound(Sfx.Swing, m.X, m.Y);
                            if (playerAlive && P.Z < 0.3f) DamagePlayer((int)(Rand(m.Def.MeleeMin, m.Def.MeleeMax) * m.DamageMult));
                        }
                        else if (m.Def.Missile != null) FireMissile(m);
                    }
                    if (m.StateTime >= m.Def.AttackTime)
                    {
                        SetState(m, AiState.Chase);
                        m.AttackCd = m.Def.Cooldown * (0.7f + RandF() * 0.6f);
                    }
                    break;
                }

            case AiState.Pain:
                if (m.StateTime >= 0.25f) SetState(m, AiState.Chase);
                break;

            case AiState.Dying:
                if (m.StateTime >= 0.45f) { SetState(m, AiState.Dead); m.Solid = false; }
                break;
        }
    }

    void ChaseMove(Monster m, float dt, bool wander)
    {
        float step = m.Def.Speed * m.SpeedMult * Vars.MonsterSpeed * dt;
        float dx, dy;
        if (m.StuckTime > 0)
        {
            m.StuckTime -= dt;
            dx = m.StuckDX; dy = m.StuckDY;
        }
        else
        {
            float tx = P.X - m.X, ty = P.Y - m.Y;
            if (wander) { tx = MathF.Cos(m.Anim); ty = MathF.Sin(m.Anim * 0.7f); }
            float l = MathF.Sqrt(tx * tx + ty * ty) + 1e-4f;
            dx = tx / l; dy = ty / l;
            // weave side to side a little so they're harder to hit
            m.StrafeTime -= dt;
            if (m.StrafeTime <= 0) { m.StrafeTime = 0.8f + RandF(); m.StrafeSign = RandF() < 0.5f ? -1 : 1; }
            float weave = m.Def.Missile != null ? 0.6f : 0.25f;
            dx += -dy * weave * m.StrafeSign; dy += dx * weave * m.StrafeSign;
            l = MathF.Sqrt(dx * dx + dy * dy) + 1e-4f;
            dx /= l; dy /= l;
        }

        float nx = m.X + dx * step, ny = m.Y + dy * step;
        if (!Blocked(nx, ny, m.Radius, m)) { m.X = nx; m.Y = ny; return; }
        if (MathF.Abs(dx) > 0.1f && !Blocked(nx, m.Y, m.Radius, m)) { m.X = nx; return; }
        if (MathF.Abs(dy) > 0.1f && !Blocked(m.X, ny, m.Radius, m)) { m.Y = ny; return; }

        // monsters can open plain doors, like in Hexen
        int cx = (int)(m.X + dx * (m.Radius + 0.2f)), cy = (int)(m.Y + dy * (m.Radius + 0.2f));
        if (Level.Cell(cx, cy) == 'D' && Level.DoorOpen[cy * Level.W + cx] < 1f && Level.DoorMove[cy * Level.W + cx] <= 0)
        {
            Level.OpenDoor(cx, cy);
            Sound(Sfx.Door, cx + 0.5f, cy + 0.5f);
        }
        float a = RandF() * MathF.Tau;
        m.StuckDX = MathF.Cos(a); m.StuckDY = MathF.Sin(a);
        m.StuckTime = 0.3f + RandF() * 0.4f;
    }

    /// <summary>Relaxed mode: creatures amble about, pause, and shy away if you come close. They never attack.</summary>
    void Wander(Monster m, float dt, float dist)
    {
        m.StrafeTime -= dt;
        if (dist < 2.2f)
        {
            // drift away from the player
            float ax = m.X - P.X, ay = m.Y - P.Y, l = MathF.Max(0.01f, MathF.Sqrt(ax * ax + ay * ay));
            m.StuckDX = ax / l; m.StuckDY = ay / l;
            m.StrafeTime = MathF.Max(m.StrafeTime, 0.8f);
        }
        else if (m.StrafeTime <= 0)
        {
            m.StrafeTime = 1.5f + RandF() * 2.5f;
            if (RandF() < 0.4f) { m.StuckDX = m.StuckDY = 0; } // rest a while
            else { float a = RandF() * MathF.Tau; m.StuckDX = MathF.Cos(a); m.StuckDY = MathF.Sin(a); }
        }
        bool moving = m.StuckDX != 0 || m.StuckDY != 0;
        if (moving)
        {
            float s = m.Def.Speed * 0.45f * Vars.MonsterSpeed * dt;
            float nx = m.X + m.StuckDX * s, ny = m.Y + m.StuckDY * s;
            if (!Blocked(nx, ny, m.Radius, m)) { m.X = nx; m.Y = ny; m.Anim += dt; }
            else { float a = RandF() * MathF.Tau; m.StuckDX = MathF.Cos(a); m.StuckDY = MathF.Sin(a); }
        }
        m.State = moving ? AiState.Wander : AiState.Idle;
    }

    void StartBlur(Monster m)
    {
        float a = MathF.Atan2(P.Y - m.Y, P.X - m.X) + (RandF() < 0.5f ? MathF.PI / 2 : -MathF.PI / 2);
        m.BlurDX = MathF.Cos(a); m.BlurDY = MathF.Sin(a);
        m.BlurTime = 0.45f;
        Sound(Sfx.Blur, m.X, m.Y);
    }

    void FireMissile(Monster m)
    {
        var kind = m.Def.Missile.Value;
        float baseA = MathF.Atan2(P.Y - m.Y, P.X - m.X);
        (int lo, int hi, float speed) = kind switch
        {
            ProjKind.Fireball => (6, 12, 6.5f),
            ProjKind.CentaurBolt => (8, 14, 7.5f),
            ProjKind.Seeker => (6, 11, 4.8f),
            _ => (10, 18, 6.0f),
        };
        for (int i = 0; i < m.Def.MissileCount; i++)
        {
            float a = baseA + (i - (m.Def.MissileCount - 1) / 2f) * m.Def.MissileSpread + (RandF() - 0.5f) * 0.06f;
            Level.Things.Add(new Projectile
            {
                Kind = kind, FromPlayer = false, DmgMin = (int)(lo * m.DamageMult), DmgMax = (int)(hi * m.DamageMult), Owner = m, Level = Level,
                X = m.X + MathF.Cos(a) * (m.Radius + 0.1f), Y = m.Y + MathF.Sin(a) * (m.Radius + 0.1f),
                Z = m.Z + m.SpriteH * 0.45f, VX = MathF.Cos(a) * speed, VY = MathF.Sin(a) * speed,
                Homing = kind == ProjKind.Seeker ? 1.9f : 0f, Life = kind == ProjKind.Seeker ? 4.5f : 6f,
            });
        }
        Sound(Sfx.Shoot, m.X, m.Y);
    }

    void UpdateProjectile(Projectile pr, float dt)
    {
        pr.Life -= dt;
        if (pr.Life <= 0) { pr.Removed = true; return; }
        // aim player shots gently toward eye-level as they fly
        if (pr.FromPlayer) pr.Z += (0.4f - pr.Z) * MathF.Min(1, dt * 2);
        // homing missiles steer toward you at a limited turn rate (tighter up close so they don't just
        // orbit you), and burn out after a few seconds; strafing hard shakes them off
        if (pr.Homing > 0 && Mode != GameMode.Dead && pr.Life > 1.5f)
        {
            float speed = MathF.Sqrt(pr.VX * pr.VX + pr.VY * pr.VY);
            float cur = MathF.Atan2(pr.VY, pr.VX);
            float near = Dist(pr.X, pr.Y, P.X, P.Y);
            if (near < 0.8f) pr.Homing = 0; // committed on the final approach: dodge now and it flies on past
            float rate = pr.Homing * Math.Clamp(4.5f / MathF.Max(0.1f, near), 1f, 4f);
            float turn = Math.Clamp(AngleDiff(MathF.Atan2(P.Y - pr.Y, P.X - pr.X), cur), -rate * dt, rate * dt);
            cur += turn;
            pr.VX = MathF.Cos(cur) * speed; pr.VY = MathF.Sin(cur) * speed;
            float tz = 0.22f * (1f - 0.4f * P.SlideLow); // skims low and tracks your stance, not jumps: hop over them
            pr.Z += Math.Clamp(tz - pr.Z, -0.6f * dt, 0.6f * dt);
        }
        float sp = MathF.Sqrt(pr.VX * pr.VX + pr.VY * pr.VY);
        int steps = Math.Max(1, (int)(sp * dt / 0.1f) + 1);
        float sx = pr.VX * dt / steps, sy = pr.VY * dt / steps;
        for (int s = 0; s < steps; s++)
        {
            pr.X += sx; pr.Y += sy;
            if (Level.BlocksPoint(pr.X, pr.Y)) { pr.X -= sx; pr.Y -= sy; Explode(pr, null); return; }
            if (pr.FromPlayer)
            {
                foreach (var t in Level.Things)
                    if (t is Monster m && m.Alive && !m.Blurring && Dist(m.X, m.Y, pr.X, pr.Y) < m.Radius + pr.Radius)
                    {
                        DamageMonster(m, Rand(pr.DmgMin, pr.DmgMax));
                        Explode(pr, m);
                        return;
                    }
            }
            else if (Mode != GameMode.Dead && Dist(P.X, P.Y, pr.X, pr.Y) < P.Radius + pr.Radius && HitsPlayerHeight(pr.Z))
            {
                DamagePlayer(Rand(pr.DmgMin, pr.DmgMax));
                Explode(pr, null);
                return;
            }
        }
    }

    /// <summary>Is height z within the player's body? Jumping lifts it, sliding shrinks it.</summary>
    bool HitsPlayerHeight(float z)
    {
        float top = P.Z + Player.Height * (1f - 0.5f * P.SlideLow);
        return z >= P.Z - 0.05f && z <= top;
    }

    void Explode(Projectile pr, Monster direct)
    {
        pr.Removed = true;
        SpawnPuff(pr.Frames[1], pr.X, pr.Y, pr.Z, pr.Splash > 0 ? 0.7f : 0.35f);
        Sound(pr.Splash > 0 ? Sfx.Explode : Sfx.Hit, pr.X, pr.Y);
        if (pr.Splash <= 0) return;
        foreach (var t in Level.Things.ToList())
            if (t is Monster m && m != direct && m.Alive)
            {
                float d = Dist(m.X, m.Y, pr.X, pr.Y);
                if (d < pr.Splash) DamageMonster(m, (int)(pr.DmgMax * 0.6f * (1 - d / pr.Splash)));
            }
    }

    void SpawnPuff(Tex tex, float x, float y, float z, float size)
    {
        Level.Things.Add(new Puff(tex, size, 0.3f, 1.2f) { X = x, Y = y, Z = z - size * 0.5f, Level = Level });
    }

    void DamageMonster(Monster m, int dmg)
    {
        if (!m.Alive || dmg <= 0 || m.Blurring) return;
        m.Health -= Math.Max(1, (int)MathF.Round(dmg * Vars.Damage));
        if (m.State == AiState.Idle) Wake(m);
        if (m.Health <= 0)
        {
            SetState(m, AiState.Dying);
            Sound(Sfx.Death, m.X, m.Y);
            P.Kills++;
            if (m.Def.Boss)
            {
                Level.BossDead = true;
                Say(Level.FindMark('E') != null ? "The Heresiarch is vanquished! The exit portal awakens." : "A Heresiarch falls!");
                PlaySound(Sfx.BossSight, 1);
            }
            return;
        }
        if (m.Def.Blurs && m.State != AiState.Attack && RandF() < 0.4f) { StartBlur(m); return; }
        if (RandF() < m.Def.PainChance && m.State != AiState.Attack)
        {
            SetState(m, AiState.Pain);
            Sound(Sfx.Pain, m.X, m.Y);
        }
    }

    void DamagePlayer(int dmg)
    {
        var p = P;
        if (Mode != GameMode.Playing || Vars.God || Relaxed) return;
        dmg = Math.Max(0, (int)MathF.Round(dmg * Vars.MonsterDamage));
        if (dmg == 0) return;
        int saved = Math.Min(p.Armor, (int)(dmg * p.Def.ArmorSave));
        p.Armor -= saved;
        p.Health -= dmg - saved;
        p.DamageFlash = MathF.Min(1, p.DamageFlash + 0.4f + dmg / 40f);
        if (p.Health <= 0)
        {
            p.Health = 0;
            p.Dead = true;
            p.Z = 0; p.VZ = 0; p.SlideTime = 0; p.SlideLow = 0;
            Mode = GameMode.Dead;
            PlaySound(Sfx.PlayerDeath, 1);
            Say("You have died. Press Enter to try again.");
        }
        else PlaySound(Sfx.PlayerPain, 1);
    }

    // ================================================================ console / cheat helpers

    /// <summary>Kills every living monster on the current map. Returns how many died.</summary>
    public int KillAll()
    {
        int n = 0;
        foreach (var m in Level.Things.OfType<Monster>().ToList())
            if (m.Alive) { m.Health = 1; DamageMonsterRaw(m, 100000); n++; }
        return n;
    }

    void DamageMonsterRaw(Monster m, int dmg)
    {
        float saved = Vars.Damage;
        Vars.Damage = 1;
        DamageMonster(m, dmg);
        Vars.Damage = saved;
    }

    /// <summary>Moves the player to a hub map (0-based), at its start or first portal.</summary>
    public void Warp(int index)
    {
        var lv = Hub[index];
        float x = lv.StartX, y = lv.StartY;
        if (x == 0)
        {
            var mark = lv.FindMark('1') ?? lv.FindMark('2') ?? lv.FindMark('E');
            if (mark != null) (x, y) = mark.Value;
        }
        foreach (var t in Level.Things) if (t is Projectile or Puff) t.Removed = true;
        Level = lv;
        P.X = x; P.Y = y;
        P.PortalLock = true;
        P.TeleportFlash = 1;
        PlaySound(Sfx.Teleport, 1);
        Say(lv.EntryMessage);
    }

    /// <summary>Spawns an already-awake monster with a teleport flash (used by arena waves).</summary>
    public Monster SpawnMonster(MonsterDef def, float x, float y, float healthMult, float damageMult, float speedMult)
    {
        var m = new Monster(def) { X = x, Y = y, Level = Level, DamageMult = damageMult, SpeedMult = speedMult };
        m.Health = (int)(def.Health * healthMult);
        Level.Things.Add(m);
        SpawnPuff(Art.BossBall[1], x, y, 0.5f, 0.8f);
        Sound(Sfx.Teleport, x, y);
        SetState(m, AiState.Chase);
        m.AttackCd = 1f + RandF();
        return m;
    }

    /// <summary>Spawns a thing (by map glyph) a short distance in front of the player.</summary>
    public bool Summon(char glyph)
    {
        var t = ThingFactory.Create(glyph, 0, 0);
        if (t == null) return false;
        for (float d = 1.6f; d >= 0.6f; d -= 0.2f)
        {
            float x = P.X + MathF.Cos(P.Angle) * d, y = P.Y + MathF.Sin(P.Angle) * d;
            if (Level.BlocksCircle(x, y, t.Radius)) continue;
            t.X = x; t.Y = y; t.Level = Level;
            Level.Things.Add(t);
            if (t is Monster m) Wake(m);
            return true;
        }
        return false;
    }

    public static float Dist(float x0, float y0, float x1, float y1)
    {
        float dx = x1 - x0, dy = y1 - y0;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public static float AngleDiff(float a, float b)
    {
        float d = a - b;
        while (d > MathF.PI) d -= MathF.Tau;
        while (d < -MathF.PI) d += MathF.Tau;
        return d;
    }
}
