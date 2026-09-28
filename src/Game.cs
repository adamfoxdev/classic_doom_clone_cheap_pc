namespace HexenSharp;

public enum PClass { Fighter, Cleric, Mage }
public enum GameMode { Title, ClassSelect, Playing, Dead, Victory }

/// <summary>One frame of player input. Held controls are continuous; the rest are "pressed this frame".</summary>
public struct Input
{
    public float Move, Strafe, Turn;      // -1..1 from keys
    public float LookX, LookY;            // mouse delta in pixels
    public bool Fire, Walk, JumpHeld, SlideHeld, JetHeld, ZoomHeld; // held
    public bool Use, UseItem, Place, Journal, Map, Pause, Confirm, Up, Down, Left, Right, Screenshot, Character, CycleHud; // pressed
    public int KeyPressed;                // any key/button code pressed this frame (for rebinding)
    public int Slot, Cycle;               // weapon slot 1..3 pressed, wheel -1/+1
    public string Typed;                  // text typed this frame (console / cheat codes)
    public bool ConsoleToggle, Backspace, Tab, PageUp, PageDown, Jump, Slide;
}

public sealed class WeaponDef
{
    string _name;
    public string Name { get => Words.T(_name); init => _name = value; }
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
    /// <summary>Its art in Art.Weapons (class * 3 + slot for the classes' own), and whether it's the rocket launcher.</summary>
    public int ArtIndex;
    public bool Rocket, Rail, Grenade, Shotgun, Beam;
    /// <summary>A Quake weapon's ammo (None for the classes' weapons, which use mana); Cost is then rounds a shot.</summary>
    public AmmoKind Ammo;
    /// <summary>The Quake weapons come up fast.</summary>
    public bool Quick => Ammo != AmmoKind.None;
    /// <summary>Lets you look right down at the floor (the rocket and grenade launchers, for jumps).</summary>
    public bool LooksDown => Rocket || Grenade;
}

public sealed class ClassDef
{
    string _name, _blurb;
    public string Name { get => Words.T(_name); init => _name = value; }
    public string Blurb { get => Words.T(_blurb); init => _blurb = value; }
    public float Speed, ArmorSave;
    public WeaponDef[] Weapons;

    static ClassDef()
    {
        for (int c = 0; c < All.Length; c++)
            for (int s = 0; s < All[c].Weapons.Length; s++) All[c].Weapons[s].ArtIndex = c * 3 + s;
    }

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
    /// <summary>A copy to play forward in a separate game (the practice demo's lookahead), sharing nothing that changes.</summary>
    public Player Copy()
    {
        var p = (Player)MemberwiseClone();
        p.HasWeapon = (bool[])HasWeapon.Clone();
        p.Loadout = (WeaponDef[])Loadout?.Clone();
        p.Ammo = (int[])Ammo.Clone();
        p.Mods = (WeaponMod[])Mods.Clone(); p.Mods2 = (WeaponMod[])Mods2.Clone();
        p.ModRanks = (int[])ModRanks.Clone(); p.ModRanks2 = (int[])ModRanks2.Clone();
        return p;
    }

    public PClass Class;
    public ClassDef Def => ClassDef.All[(int)Class];
    public float X, Y, Angle, Pitch;
    public float Radius = 0.25f;
    public int Health = 100, Armor, BlueMana = 50, GreenMana, Flasks, Urns, Kills, ChestsOpened, Relics, LoreRead, Secrets;
    /// <summary>The ship's forward speed on a flight map.</summary>
    public float ShipSpeed;
    /// <summary>Rubble blocks you've broken loose and carry, ready to place (up to a stack of BlockStack).</summary>
    public int Blocks;
    public const int BlockStack = 64;
    /// <summary>Ore you're carrying, by Level.OreGlyphs index (iron, crystal, fuel).</summary>
    public readonly int[] Ore = new int[Level.OreGlyphs.Length];
    /// <summary>Arsenal upgrades picked up in the arena (0 to Arsenal.MaxTier); they power up every weapon there.</summary>
    public int ArenaTier;
    public bool[] HasWeapon = { true, false, false };
    /// <summary>Each weapon's mod (see WeaponMods), and a Charged mod's charge building while Fire is held.</summary>
    public WeaponMod[] Mods = new WeaponMod[3], Mods2 = new WeaponMod[3];
    /// <summary>Each mod's rank, I to III (0 reads as I).</summary>
    public int[] ModRanks = new int[3], ModRanks2 = new int[3];
    public float Charge;
    public bool Charging;
    public int Weapon, PendingWeapon = -1;
    public float Cooldown, FireAnim, Raise, Bob, BobAmount;
    public float DamageFlash, PickupFlash, TeleportFlash;
    public bool SteelKey, FireKey, PortalLock, Dead;
    public float EyeZ = 0.5f;
    /// <summary>Height of the floor you're standing on; Z (jump height) is measured from it.</summary>
    public float FloorZ;
    /// <summary>Camera lag when stepping up, so stairs feel smooth rather than jerky.</summary>
    public float StepLag;
    public float Z, VZ;                                   // height above the floor while jumping
    /// <summary>Horizontal velocity, for Quake movement (classic movement goes straight where your keys say).</summary>
    public float VX, VY;
    /// <summary>A jump pressed just before landing still counts (Quake movement).</summary>
    public float JumpBuffer;
    /// <summary>Time not yet stepped by the fixed-rate Quake movement physics.</summary>
    public float MoveClock;
    public float HSpeed => MathF.Sqrt(VX * VX + VY * VY);
    public float SlideTime, SlideCd, SlideDX, SlideDY, SlideLow;
    public const float SlideLength = 0.55f, Height = 0.55f;
    /// <summary>Jetpack (Wings of Wrath in the fantasy style): fuel in seconds of hovering; it recharges on the ground.</summary>
    public bool HasJetpack, Flying;
    /// <summary>Set when the tank runs dry: the jetpack won't relight until you let go of its key.</summary>
    public bool JetLock;
    public float Fuel, JetSfx;
    public const float FuelMax = 6f, ClimbSpeed = 2.6f, SinkSpeed = 3.2f;
    /// <summary>Health and jetpack fuel limits, raised by the Vitality and Thrusters skills.</summary>
    public int MaxHealth = 100;
    public float MaxFuel = FuelMax;
    public bool OnGround => Z <= 0f;
    /// <summary>Camera height: eye level, raised by jumps and lowered while sliding.</summary>
    public float ViewZ => EyeZ + Z - SlideLow * 0.25f + StepLag;
    /// <summary>The shooting range's loadout (every weapon in the game), in place of your class's three; null elsewhere.</summary>
    public WeaponDef[] Loadout;
    /// <summary>The Quake weapons' ammo, by AmmoKind.</summary>
    public int[] Ammo = new int[QuakeAmmo.Kinds];
    public WeaponDef[] Weapons => Loadout ?? Def.Weapons;
    public WeaponDef CurWeapon => Weapons[Weapon];
    /// <summary>A blast's push lifts the speed cap to this for a while (a rocket jump carries you faster than running).</summary>
    public float Boost;
}

/// <summary>
/// Where you come back after dying: the highest checkpoint pad reached in a map, with the health and armor you
/// had then (keys, weapons and items stay with you, so nothing on the main route can be lost).
/// </summary>
public sealed class Checkpoint
{
    public Level Level;
    public int Index, Health, Armor;
    public float X, Y, Floor, Angle;
}

public sealed partial class Game
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
    Random _rng = new(1234);
    /// <summary>The seed this run's dice were rolled from (FixedSeed, or the clock): a replay starts from it.</summary>
    public int RunSeed;
    float _exitMsgCd;

    public readonly Bindings Binds = new();
    public readonly MenuSystem Menu;
    /// <summary>Where options and key bindings are saved; null (as in tests) means don't touch the disk.</summary>
    public string ConfigPath;

    public Game()
    {
        Con = new DevConsole(this);
        Menu = new MenuSystem(this);
        Menu.Show(MenuPage.Main);
    }

    bool _loadingSettings;

    public void SaveSettings()
    {
        // commands replayed from the settings file must not rewrite it halfway through loading
        if (ConfigPath != null && !_loadingSettings) Settings.Save(this, ConfigPath);
    }

    public void LoadSettings()
    {
        if (ConfigPath == null) return;
        _loadingSettings = true;
        try { Settings.Load(this, ConfigPath); }
        finally { _loadingSettings = false; }
    }

    /// <summary>Switches the look (sci-fi or fantasy) at any time, including mid-game.</summary>
    public void SetArtStyle(ArtStyle style)
    {
        if (style == Art.Style) return;
        RebuildArt(style);
    }

    /// <summary>Turns the Blender-rendered art pack (sci-fi style only) on or off.</summary>
    public void SetRenderedArt(bool on)
    {
        if (on == Art.Rendered) return;
        Art.Rendered = on;
        RebuildArt(Art.Style);
    }

    void RebuildArt(ArtStyle style)
    {
        Art.Init(style);
        if (Hub != null)
            foreach (var lv in Hub) lv.Theme = Maps.ThemeById(lv.ThemeId);
    }

    /// <summary>Folder where `playmap <name>` looks for custom maps.</summary>
    public string MapsDir;
    /// <summary>Where new games get their maps: the built-in hub, or a single map being play-tested.</summary>
    public Func<Level[]> HubSource = Maps.BuildHub;
    public bool TestingMap;

    /// <summary>On a practice course (Main menu > Practice): the run's clock, and whether it has started.</summary>
    public bool Practicing, RunStarted;
    public float RunTime;
    /// <summary>The practice course you're on (or were last on).</summary>
    public Course Course = Courses.Hangar;
    /// <summary>Picking a class from the class screen starts this practice course rather than a new game.</summary>
    public bool PendingPractice;
    public Course PendingCourse = Courses.Hangar;

    /// <summary>Starts a practice course (the Velocity Hangar unless you say) with the given class.</summary>
    public void StartPractice(PClass cls, Course course = null)
    {
        Course = course ?? Courses.Hangar;
        Rematch = null;
        if (Course.Endless && Course.Seed <= 0) Course = Endless.For(Endless.NewSeed()); // picked from the menu: a fresh seed
        if (Course.Tower && Course.Seed <= 0) Course = Tower.For(Endless.NewSeed());
        if (Course.Range) Style = GameStyle.Classic; // weapons out: it's a range
        var c = Course;
        HubSource = () => new[] { c.Map().Build() };
        TestingMap = true;
        Practicing = true;
        ArenaMode = false;
        StoryMode = false;
        Demo = DemoPaused = DemoSteps = false; Pilot = null; DemoTrack = null; PracticeSpeed = 1f;
        NewGame(cls); // sets the course up (SetUpCourse) once the map is built
        if (!Vars.QuakeMove) Say("Tip: turn on Quake movement in Options to build speed.");
        float best = Course.Timed ? Profile.CourseBestTime(Course.Key(P.Class)) : 0;
        if (best > 0) Say($"Your best as the {P.Def.Name}: {best:0.00}s.");
    }

    /// <summary>In the Chaos Arena (Main menu > Arena): wave survival, with its own leaderboard and medals.</summary>
    public bool ArenaMode;
    /// <summary>Picking a class from the class screen starts the arena rather than a new game.</summary>
    public bool PendingArena;
    /// <summary>
    /// The music that fits what's on screen: the title's on the menus, the practice track on a course, else the map's
    /// theme (custom maps included).
    /// </summary>
    public string MusicTrack =>
        Mode is GameMode.Title or GameMode.ClassSelect or GameMode.Victory || Level == null ? "title"
        : Practicing ? "practice" : Level.ThemeId;

    /// <summary>The modifiers for arena runs (picked on the arena's setup page; kept for Restart and between runs).</summary>
    public ArenaMod ArenaMods;

    /// <summary>A perk's rank in the arena run you're on (0 anywhere else).</summary>
    public int PerkRank(Perk p) => ArenaMode && Level?.Arena is { } a ? a.Rank(p) : 0;
    /// <summary>The last arena run that ended, and its place on the leaderboard (0 if off it).</summary>
    public ArenaRun LastArena;
    public int LastArenaPlace;

    /// <summary>Starts a run in the Chaos Arena with the given class. Always classic: the waves need a fight.</summary>
    public void StartArena(PClass cls) => StartArena(cls, daily: false);

    void StartArena(PClass cls, bool daily)
    {
        HubSource = () => new[] { Maps.ChaosArena.Build() };
        TestingMap = true;
        Rematch = null;
        Practicing = false; Demo = false; PracticeSpeed = 1f;
        ArenaMode = true;
        DailyMode = daily;
        StoryMode = false;
        Style = GameStyle.Classic;
        NewGame(cls);
        if (daily) return;
        int best = Profile.ArenaBestWave(cls);
        var (next, at) = ArenaMedals.Next(best);
        Say(best > 0 ? $"Your best as the {P.Def.Name}: {best} wave{(best == 1 ? "" : "s")}." : "Step on the altar when you're ready.");
        if (next != Medal.None) Say($"{Medals.Name(next)}: clear wave {at}.");
    }

    /// <summary>A wave is cleared in arena mode: a medal when it reaches one, and a new best past your old one.</summary>
    public void ArenaWaveCleared(int wave)
    {
        QuakeArenaWave();
        if (Vars.Arcade) Say($"Wave bonus: +{Arcade.Bonus(wave * 1000)}");
        int best = Profile.ArenaBestWave(P.Class);
        var medal = ArenaMedals.For(wave);
        if (medal != ArenaMedals.For(wave - 1) && medal > ArenaMedals.For(best)) { Say($"New medal: {Medals.Name(medal)}!"); PlaySound(Sfx.BossSight, 0.6f); }
        if (wave == best + 1 && best > 0) Say($"A new best: wave {wave}!");
    }

    /// <summary>
    /// Ends the arena run in progress (on death, Restart, leaving for the title or quitting) and puts it on the
    /// leaderboard if it cleared a wave. Each run is recorded once.
    /// </summary>
    public void EndArenaRun()
    {
        if (!ArenaMode || Level?.Arena is not { Started: true, Recorded: false } a || P == null) return;
        a.Recorded = true;
        if (a.BestWave == 0) { LastArena = null; LastArenaPlace = 0; return; }
        var difficulty = Difficulties.Of(Vars);
        if (difficulty == Difficulty.Custom)
        {
            LastArena = null; LastArenaPlace = 0;
            Say($"Run over: {a.BestWave} waves. Not recorded: the damage settings were changed in the console (set a difficulty in Options).");
            return;
        }
        if (DailyMode) { EndDailyRun(a, difficulty); return; }
        if (InstagibOn) { EndInstagibRun(a); return; }
        LastArena = new ArenaRun
        {
            Waves = a.BestWave, Time = a.ClearedAt, Kills = P.Kills, Name = RunnerName, When = DateTime.Now,
            Mods = (int)a.Mods, Difficulty = (int)difficulty, Score = ArenaModInfo.Score(a.BestWave, a.Mods, difficulty),
        };
        LastArenaPlace = Profile.AddArenaRun(P.Class, LastArena);
        SaveProfile();
        string msg = $"Run over: {a.BestWave} wave{(a.BestWave == 1 ? "" : "s")} in {a.ClearedAt:0.0}s, {P.Kills} kills. Score {LastArena.Score}.";
        if (LastArenaPlace == 1) msg += " Your best!";
        else if (LastArenaPlace > 0) msg += $" #{LastArenaPlace} on the leaderboard.";
        Say(msg);
    }

    /// <summary>The demo is playing the course for you to watch (Esc > Watch demo, or 'demo').</summary>
    public bool Demo;
    /// <summary>Step mode: the demo stops at each step of the technique until you press Enter.</summary>
    public bool DemoSteps, DemoPaused;
    public DemoPilot Pilot;
    /// <summary>The last demo's run, raced as a ghost on your next go, and its time.</summary>
    public GhostTrack DemoTrack;
    public float DemoTime;
    /// <summary>Game speed on a practice course (1/2/3: 100%, 50%, 25%), for the demo or your own slow-motion runs.</summary>
    public float PracticeSpeed = 1f;
    float _demoClock;

    /// <summary>Starts the demo from the start line.</summary>
    public void StartDemo()
    {
        if (!Practicing) return;
        MoveTo(Level.StartX, Level.StartY, Course.StartAngle);
        P.TeleportFlash = 0;
        Demo = true; DemoPaused = false;
        Pilot = new DemoPilot();
        ResetRun();
        Messages.Clear();
        Say("Demo: watch the keys light up and the turn gauge. 1/2/3 speed, E step by step, move to take over.");
    }

    /// <summary>Stops the demo (pause menu > Stop demo).</summary>
    public void EndDemo() { if (Demo) StopDemo("Demo stopped. Your turn!"); }

    /// <summary>Ends the demo and hands you the controls back at the start line.</summary>
    void StopDemo(string message)
    {
        Demo = false; DemoPaused = false; DemoSteps = false; Pilot = null;
        MoveTo(Level.StartX, Level.StartY, Course.StartAngle);
        P.TeleportFlash = 0;
        ResetRun();
        Messages.Clear();
        Say(message);
    }

    /// <summary>
    /// Practice-only controls, before the player moves: 1/2/3 set the game speed; during the demo, E toggles step mode,
    /// Enter steps on, any movement takes over, and otherwise the pilot supplies the input. False when the frame stops
    /// here (paused on a step).
    /// </summary>
    bool PracticeControls(ref Input inp, ref float dt)
    {
        if (inp.Slot is >= 1 and <= 3 && !Course.Range) // on the range, 1 to 0 are the weapons
        {
            PracticeSpeed = inp.Slot == 1 ? 1f : inp.Slot == 2 ? 0.5f : 0.25f;
            Say(Demo ? $"Demo at {PracticeSpeed * 100:0}% speed." : $"Game speed {PracticeSpeed * 100:0}%" + (PracticeSpeed < 1 ? ": practise slowly (slow runs don't go on the leaderboard)." : "."));
            inp.Slot = 0;
        }
        if (Demo)
        {
            bool takeOver = inp.Move != 0 || inp.Strafe != 0 || inp.Jump || inp.JetHeld || inp.Fire || MathF.Abs(inp.LookX) > 3;
            if (takeOver && Course.Timed) { StopDemo("Your turn! Race the demo's ghost."); return false; }
            if (takeOver) { StopDemo("Your turn!"); return false; }
            if (inp.Use)
            {
                DemoSteps = !DemoSteps; DemoPaused = false;
                Say(DemoSteps ? "Step by step: the demo stops at each step. Enter for the next." : "Step by step off.");
            }
            if (DemoPaused)
            {
                if (!inp.Confirm) return false;
                DemoPaused = false;
            }
            // the demo runs on a fixed clock of 72 ticks a second (slowed by the game speed), so it plays the same on
            // every machine; step by step, it stops before each new step of the technique
            _demoClock = MathF.Min(_demoClock + dt * PracticeSpeed, 0.1f);
            while (Demo && _demoClock >= DemoPilot.Tick)
            {
                string before = Pilot.Caption;
                var pilot = Pilot.Next(this, DemoPilot.Tick);
                if (DemoSteps && Pilot.Caption != before && before != "") { DemoPaused = true; _demoClock = 0; break; }
                _demoClock -= DemoPilot.Tick;
                PlayTime += DemoPilot.Tick;
                UpdatePlayer(pilot, DemoPilot.Tick);
                UpdateWorld(DemoPilot.Tick);
                RangeTick(DemoPilot.Tick); // the demo's health and mana come back as yours do
                TowerTick(DemoPilot.Tick);
                SoccerTick(DemoPilot.Tick);
            }
            return false;
        }
        dt *= PracticeSpeed;
        return true;
    }

    /// <summary>On a fresh copy of the course (starting, or Restart): face down the course, hand out the free-roam jetpack, reset the run.</summary>
    void SetUpCourse()
    {
        P.Angle = Course.StartAngle;
        if (Course.Jetpack) { P.HasJetpack = true; P.Fuel = P.MaxFuel; }
        if (Course.Range) SetUpRange();
        if (Course.Rockets) SetUpRocketCourse();
        if (Course.Grenades) SetUpGrenadeCourse();
        if (Course.Tower) SetUpTower();
        if (Course.Soccer) SetUpSoccer();
        ResetRun();
    }

    /// <summary>
    /// Speed you need, as a percentage of your run, to clear a gap `cells` wide with a running jump onto a landing
    /// `drop` lower, rounded up to 5%. It's the exact arc, which high frame rates follow closely; at low ones each jump
    /// carries a little further, so the figure is on the safe side.
    /// </summary>
    public int GapSpeedPercent(int cells, float drop = 0)
    {
        float j = Vars.JumpPower, air = (j + MathF.Sqrt(MathF.Max(0, j * j + 2 * Vars.Gravity * drop))) / Vars.Gravity;
        float need = (cells - 2 * P.Radius) / air;
        return (int)(MathF.Ceiling(need / RunSpeed * 20) * 5);
    }

    /// <summary>
    /// What the course tells you as you reach each checkpoint. On a gap course, how fast you'll need to be for the next
    /// gap; elsewhere, how many checkpoints you've reached.
    /// </summary>
    string CourseHint(int zone)
    {
        var plats = Course.Platforms;
        int reached = Level.CheckpointsReached.Count, total = Level.Checkpoints.Count;
        if (Course.Tower) return TowerHint(zone);
        if (Course.Grenades)
            return reached <= 1 ? Course.Intro
                : $"Checkpoint {reached} of {total}." + (CourseTargetsLeft > 0 ? $" {CourseTargetsLeft} target{(CourseTargetsLeft == 1 ? "" : "s")} still standing." : " Every target down: on to the exit.");
        if (plats == null)
            return reached <= 1 ? Course.Intro
                : reached >= total ? "Every checkpoint! Cross the line to finish." : $"Checkpoint {reached} of {total}.";
        if (zone == 0) return Course.Intro;
        if (zone >= plats.Length - 1) return "Made it! Step into the exit to finish the run.";
        if (Course.Rockets) return RocketCourse.Hint(zone);
        int gap = plats[zone + 1].x0 - plats[zone].x1 - 1;
        string zig = zone == 2 ? " Zig-zag: switch strafe keys and turn the other way each hop." : "";
        return $"Platform {(Course.Endless ? zone : zone + 1)}. The next gap is {gap} wide: hit about {GapSpeedPercent(gap, plats[zone].floor - plats[zone + 1].floor)}% speed.{zig}";
    }

    /// <summary>Crossed the finish: report the time and its place on your class's leaderboard, and start the run over.</summary>
    void FinishRun()
    {
        if (OnEndless) { EndEndlessRun(true); return; }
        if (Demo)
        {
            Recording.Record(RunTime, P.X, P.Y, P.FloorZ + P.Z);
            DemoTrack = Recording; DemoTime = RunTime;
            var dm = Course.MedalFor(P.Class, RunTime);
            StopDemo($"The demo made it in {RunTime:0.00}s" + (dm != Medal.None ? $" ({Medals.Name(dm)})" : "") + ". Your turn: race its ghost!");
            return;
        }
        if (PracticeSpeed < 1)
        {
            Messages.Clear();
            Say($"Cleared in {RunTime:0.00}s at {PracticeSpeed * 100:0}% speed. Press 1 for full speed to set times.");
            Level.CheckpointsReached.Clear(); Checkpoint = null;
            MoveTo(Level.StartX, Level.StartY, Course.StartAngle);
            ResetRun();
            return;
        }
        DemoTrack = null; // your own run: back to racing your own best
        string key = Course.Key(P.Class);
        float best = Profile.CourseBestTime(key);
        var had = Course.MedalFor(P.Class, best);
        var medal = Course.MedalFor(P.Class, RunTime);
        int place = Profile.AddCourseRun(key, RunTime, RunnerName, DateTime.Now);
        if (place > 0) SaveProfile();
        PlaySound(place == 1 || medal > had ? Sfx.Secret : Sfx.Teleport, 1);
        Messages.Clear();
        string earned = medal == Medal.None ? "" : $" {Medals.Name(medal)}.";
        Say(place == 1 ? $"Course cleared in {RunTime:0.00}s - a new best!{earned}"
            : place > 0 ? $"Course cleared in {RunTime:0.00}s - #{place} on the leaderboard (best {best:0.00}s).{earned}"
            : $"Course cleared in {RunTime:0.00}s (best {best:0.00}s).{earned}");
        if (medal > had) Say($"New medal: {Medals.Name(medal)}!");
        var mine = medal > had ? medal : had;
        if (mine < Medal.Gold)
        {
            var next = mine + 1;
            var t = Course.MedalTimes(P.Class);
            Say($"{Medals.Name(next)} is {(next == Medal.Gold ? t.gold : next == Medal.Silver ? t.silver : t.bronze):0.00}s.");
        }
        LastMedal = medal;
        LastRun = RunTime;
        LastPlace = place;
        // the ghost is your best run: this one replaces it if it's the new best, or if there's no ghost yet
        Recording.Record(RunTime, P.X, P.Y, P.FloorZ + P.Z);
        LapFinished?.Invoke(RunTime, Recording);
        if (place == 1 || !Profile.Ghosts.ContainsKey(key))
        {
            Profile.Ghosts[key] = new CourseGhost { Time = RunTime, Path = Recording.Encode() };
            SaveProfile();
        }
        Level.CheckpointsReached.Clear();
        Checkpoint = null;
        MoveTo(Level.StartX, Level.StartY, Level.StartAngle);
        ResetRun();
    }

    /// <summary>The run you're making now, recorded for the ghost.</summary>
    public GhostTrack Recording = new();
    /// <summary>Your best run racing you on the practice course (null when there isn't one, or 'ghost 0').</summary>
    public GhostRunner Ghost;

    /// <summary>Back to the start line: the clock at zero, a fresh recording, and the ghost waiting beside you.</summary>
    void ResetRun()
    {
        RunTime = 0; RunStarted = false; EndlessReached = 0;
        if (Practicing && Course.Grenades) ResetCourseTargets();
        if (Practicing && Course.Tower) { TowerHeight = 0; TowerPlatform = 0; _towerCarry = -1; }
        if (Practicing && Course.Soccer) ResetSoccer();
        Level.CheckpointsReached.Clear();
        Checkpoint = null;
        Recording = new GhostTrack();
        ShowGhost();
    }

    /// <summary>Puts your class's saved ghost on the course (or takes it off when ghosts are turned off).</summary>
    public void ShowGhost()
    {
        if (Ghost != null) { Ghost.Removed = true; Level.Things.Remove(Ghost); Ghost = null; }
        if (!Practicing || !(Course.Timed || RivalHere) || !Vars.Ghost || Demo) return;
        GhostTrack track;
        float time;
        if (DemoTrack != null) (track, time) = (DemoTrack, DemoTime); // after a demo, race it
        else if (RivalHere) (track, time) = (Rival.Track, Rival.Time); // a friend's ghost, from a code
        else if (Profile.Ghosts.TryGetValue(Course.Key(P.Class), out var saved)) (track, time) = (GhostTrack.Decode(saved.Path), saved.Time);
        else return;
        if (track.Points.Count < 2) return;
        Ghost = new GhostRunner(track, time) { Level = Level };
        Ghost.Seek(RunTime);
        Level.Things.Add(Ghost);
    }

    /// <summary>At a checkpoint: how far ahead of (negative) or behind your ghost you are, as " (+0.84s vs ghost)".</summary>
    string GhostSplit(int zone)
    {
        if (Ghost == null || !RunStarted) return "";
        float then = GhostZoneTime(Ghost.Track, zone);
        if (then < 0) return "";
        float d = RunTime - then;
        return $" ({(d <= 0 ? "-" : "+")}{MathF.Abs(d):0.00}s vs ghost)";
    }

    /// <summary>When a recorded run first stood in a checkpoint's zone (on its floor), or -1 if it never did.</summary>
    float GhostZoneTime(GhostTrack track, int zone)
    {
        var lv = Level;
        for (int i = 0; i < track.Points.Count; i++)
        {
            var (x, y, z) = track.Points[i];
            int cx = (int)MathF.Floor(x), cy = (int)MathF.Floor(y);
            if (!lv.InBounds(cx, cy)) continue;
            int cell = cy * lv.W + cx;
            if (lv.CheckpointZone[cell] == zone && MathF.Abs(z - lv.Floors[cell]) < 0.05f) return i * GhostTrack.Step;
        }
        return -1;
    }

    /// <summary>The time of the last finished practice run, and its place on the leaderboard (0 if off it).</summary>
    public float LastRun;
    public int LastPlace;
    /// <summary>The medal the last finished practice run earned.</summary>
    public Medal LastMedal;

    /// <summary>The best medal the run in progress can still make (None once it's slower than bronze), and its time.</summary>
    public (Medal medal, float time) NextMedal()
    {
        var (gold, silver, bronze) = Course.MedalTimes(P.Class);
        return RunTime <= gold ? (Medal.Gold, gold) : RunTime <= silver ? (Medal.Silver, silver) : RunTime <= bronze ? (Medal.Bronze, bronze) : (Medal.None, 0);
    }

    /// <summary>The name your practice runs go on the leaderboard under (`name` in the console).</summary>
    public string RunnerName = CleanName(Environment.UserName);

    /// <summary>A name the pixel font can draw: letters, digits, spaces and dashes, upper case, at most 12 long.</summary>
    public static string CleanName(string name)
    {
        var s = new string((name ?? "").ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ' or '-').ToArray()).Trim();
        if (s.Length > 12) s = s[..12].TrimEnd();
        return s.Length > 0 ? s : "PLAYER";
    }

    /// <summary>Plays a single custom map (from --play or `playmap`). Restart or winning plays it again.</summary>
    public void StartTest(MapDef map, PClass cls)
    {
        HubSource = () => new[] { map.Build() };
        TestingMap = true;
        Practicing = false;
        ArenaMode = false;
        StoryMode = false;
        NewGame(cls);
        Say($"Play-testing '{map.Name}'.");
    }

    /// <summary>
    /// Restarts the play-test on a new version of the map (the HTML editor saved it again), keeping you where you
    /// were if that spot is still open floor.
    /// </summary>
    public void ReloadTest(MapDef map)
    {
        var p = P;
        (float x, float y, float angle, float pitch) = p != null ? (p.X, p.Y, p.Angle, p.Pitch) : (0f, 0f, 0f, 0f);
        var cls = p?.Class ?? PClass.Fighter;
        StartTest(map, cls);
        int cx = (int)MathF.Floor(x), cy = (int)MathF.Floor(y);
        if (p != null && Level.InBounds(cx, cy) && !Level.BlocksCircle(x, y, P.Radius))
        {
            P.X = x; P.Y = y; P.Angle = angle; P.Pitch = pitch;
            P.FloorZ = Level.FloorUnder(x, y, P.Radius);
        }
        Messages.Clear();
        Say($"Reloaded '{map.Name}'.");
    }

    public void GoToTitle()
    {
        SaveNow();
        EndArenaRun();
        ArenaMode = false;
        DailyMode = false;
        StoryMode = false; Story = null;
        SaveProfile();
        if (TestingMap) { TestingMap = false; HubSource = Maps.BuildHub; }
        Rematch = null; PendingRematch = null;
        if (CurrentReplay is { Frames.Count: > 0 }) LastReplay = CurrentReplay;
        CurrentReplay = null;
        Practicing = false; Demo = false; PracticeSpeed = 1f;
        Mode = GameMode.Title;
        Paused = false;
        Menu.Close();
        Menu.Show(MenuPage.Main);
    }

    public int Rand(int lo, int hi) => _rng.Next(lo, hi + 1);
    public float RandF() => (float)_rng.NextDouble();

    /// <summary>Steps the HUD style (Full, Compact, Minimal, Off) forward or back, and says which one you're on.</summary>
    public void CycleHud(int dir)
    {
        int n = Enum.GetValues<HudStyle>().Length;
        Vars.Hud = (HudStyle)(((int)Vars.Hud + dir + n) % n);
        Say("HUD: " + HudName(Vars.Hud));
    }

    public static string HudName(HudStyle h) => h switch
    {
        HudStyle.Full => "FULL",
        HudStyle.Compact => "COMPACT",
        HudStyle.Minimal => "MINIMAL",
        _ => "OFF",
    };

    public void Say(string s)
    {
        Messages.Add((Words.T(s), 3.5f));
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
    /// <summary>The checkpoint you respawn at if you die in its map; null before you reach one.</summary>
    public Checkpoint Checkpoint;
    bool _onLift;
    float _liftMsgCd;
    /// <summary>Classic: fight through the hub. Relaxed: no combat; explore, read lore, find relics and secrets.</summary>
    public GameStyle Style = GameStyle.Classic;
    public bool Relaxed => Style == GameStyle.Relaxed;
    /// <summary>Text of the lore stone being read (the game pauses while it's open).</summary>
    public string ReadingLore;
    Random _loot = new();
    /// <summary>Score, style rank and damage numbers, shown when Arcade mode is on (always tracked).</summary>
    public readonly Arcade Arcade = new();

    /// <summary>Story mode (Main menu > Story): private-eye cases around Neon Harbor, one map each.</summary>
    public bool StoryMode;
    public int StoryCase;
    /// <summary>The case in progress: clues, statements, strikes, and who you're talking to.</summary>
    public StoryState Story;

    /// <summary>Takes on a case: its map, its suspects and clues. Always on foot and unarmed.</summary>
    public void StartStory(int caseIndex)
    {
        StoryCase = Math.Clamp(caseIndex, 0, HexenSharp.Story.Cases.Length - 1);
        Rematch = null;
        var c = HexenSharp.Story.Cases[StoryCase];
        HubSource = () => new[] { c.Map.Build() };
        TestingMap = true;
        Practicing = false; Demo = false; PracticeSpeed = 1f;
        ArenaMode = false;
        StoryMode = true;
        Style = GameStyle.Classic;
        NewGame(PClass.Fighter); // sets the case up (SetUpCase) once the map is built
    }

    void SetUpCase()
    {
        var c = HexenSharp.Story.Cases[StoryCase];
        Story = new StoryState(c);
        foreach (var s in c.Suspects) Level.Things.Add(new Npc(s) { Level = Level });
        foreach (var clue in c.Clues) Level.Things.Add(new ClueMark(clue) { Level = Level });
        Level.Things.RemoveAll(t => t is Monster or Chest);
        Messages.Clear();
        Say($"Case {StoryCase + 1}: {c.Title}");
        ReadingLore = $"Case {StoryCase + 1}: {c.Title}\n\n{c.Brief}\n\nE: question people and examine clues.  J: your journal.";
    }

    /// <summary>The culprit's confessed and you've closed the dialogue: on to the next job, or the end of the story.</summary>
    public void CaseSolved()
    {
        if (StoryCase + 1 >= HexenSharp.Story.Cases.Length)
        {
            Mode = GameMode.Victory; PlaySound(Sfx.Relic, 1);
            Profile.StoryWins++;
            Achievements.Check(this);
            SaveProfile();
            return;
        }
        StartStory(StoryCase + 1);
        Say("A new job comes in over the wire.");
    }

    /// <summary>Three wrong accusations: the trail goes cold and the case starts over.</summary>
    public void CaseGoesCold()
    {
        PlaySound(Sfx.PlayerDeath, 0.6f);
        StartStory(StoryCase);
        Say("Three wrong calls. The trail went cold, so you start the case over.");
    }

    /// <summary>E in story mode: talk to the person or examine the clue you're facing, if any.</summary>
    bool TryDetective()
    {
        if (Story == null) return false;
        Thing best = null;
        float bestD = 1.6f;
        foreach (var t in Level.Things)
        {
            if (t is not (Npc or ClueMark)) continue;
            float d = Dist(t.X, t.Y, P.X, P.Y);
            if (d >= bestD || MathF.Abs(AngleDiff(MathF.Atan2(t.Y - P.Y, t.X - P.X), P.Angle)) > 0.6f) continue;
            best = t; bestD = d;
        }
        if (best is Npc n) HexenSharp.Story.Talk(this, n);
        else if (best is ClueMark m) HexenSharp.Story.Examine(this, m);
        return best != null;
    }

    /// <summary>Talking or reading the journal pauses play: returns true while it has the input.</summary>
    bool StoryInput(Input inp)
    {
        var s = Story;
        if (s == null || Mode != GameMode.Playing) return false;
        if (s.JournalOpen)
        {
            if (inp.Journal || inp.Pause || inp.Confirm || inp.Use) s.JournalOpen = false;
            return true;
        }
        if (s.Talking != null)
        {
            var opts = HexenSharp.Story.Options(s);
            if (inp.Up) { s.Cursor = (s.Cursor + opts.Count - 1) % opts.Count; PlaySound(Sfx.Swing, 0.4f); }
            if (inp.Down) { s.Cursor = (s.Cursor + 1) % opts.Count; PlaySound(Sfx.Swing, 0.4f); }
            s.Cursor = Math.Clamp(s.Cursor, 0, opts.Count - 1);
            if (inp.Pause) HexenSharp.Story.Choose(this, "bye");
            else if (inp.Confirm || inp.Use) HexenSharp.Story.Choose(this, opts[s.Cursor].key);
            return true;
        }
        if (inp.Journal) { s.JournalOpen = true; PlaySound(Sfx.Lore, 0.5f); return true; }
        return false;
    }

    public void NewGame(PClass cls)
    {
        EndArenaRun(); // Restart, or trying again after dying
        Arcade.Reset();
        TrickAge = 99f; // (no callout left over from the last game)
        Hub = HubSource();
        // every die this run rolls comes from one seed, so a replay of it (see Replay) plays out the same
        RunSeed = FixedSeed ?? Environment.TickCount;
        _rng = new Random(RunSeed + 1233);
        _loot = new Random(RunSeed);
        _modRng = new Random(RunSeed + 17);
        _eliteRng = new Random(RunSeed + 41);
        _elitesMet.Clear();
        if (ArenaMode && DailyMode) cls = Daily.For(DailyDate).cls; // the day's class, every attempt
        else if (ArenaMode && (ArenaMods & ArenaMod.RandomClass) != 0) cls = (PClass)_loot.Next(3); // a fresh roll every run
        ChestsTotal = 0;
        Checkpoint = null;
        _onLift = false;
        RunTime = 0; RunStarted = false;
        Recording = new GhostTrack();
        Ghost = null;
        var names = Discovery.RelicNames.OrderBy(_ => _loot.Next()).ToList();
        int nameIndex = 0;
        string NextName() => names[nameIndex++ % names.Count];
        // the Quake weapons, hidden through the hub (before the chests, so they always go in the same places)
        if (!TestingMap && !Practicing && !ArenaMode && !StoryMode && !Relaxed) PlaceStashes();
        foreach (var lv in Hub)
        {
            if (!Practicing && !ArenaMode && !StoryMode) Chests.Scatter(lv, _loot, Vars.Chests * NgPlus.Chests(NgEligible ? NgTier : 0)); // practice courses, the arena and cases stay clear
            ChestsTotal += lv.Things.Count(t => t is Chest);

            // treasure in secret nooks: a relic when relaxed, a Mystic Urn in classic
            foreach (var r in lv.Things.OfType<Pickup>().Where(p => p.Kind == PickupKind.Relic).ToList())
            {
                lv.Things.Remove(r);
                lv.Things.Add(Relaxed ? Discovery.MakeRelic(r.X, r.Y, lv, NextName()) : Place(new Pickup(PickupKind.Urn, 0.45f), r, lv));
            }
            if (Relaxed)
            {
                Discovery.ScatterRelics(lv, _loot, 2, NextName);
                foreach (var m in lv.Things.OfType<Monster>()) { m.State = AiState.Idle; m.StrafeTime = RandF() * 3; }
            }
        }
        ApplyNgPlus();
        foreach (var lv in Hub)
        {
            // a custom map's own elites and random weapon mods
            if (lv.EliteChance > 0 && NgTier == 0) RollElites(lv, lv.EliteChance);
            foreach (var pk in lv.Things.OfType<Pickup>().Where(p => p.Kind == PickupKind.Mod && p.Variant == 0).ToList())
                lv.Things[lv.Things.IndexOf(pk)] = Place(MakeMod(RandomMod(), 0, 0), pk, lv);
        }
        RelicsTotal = Hub.Sum(l => l.Things.Count(t => t is Pickup { Kind: PickupKind.Relic }));
        LoreTotal = Hub.Sum(l => l.Things.Count(t => t is LoreStone));
        SecretsTotal = Hub.Sum(l => l.SecretCount);
        ReadingLore = null;
        Level = Hub[0];
        if (ArenaMode && Level.Arena != null)
        {
            Level.Arena.Mods = RunMods;
            if (DailyMode) Level.Arena.Reseed(Daily.Seed(DailyDate)); // the same waves and perk offers for everyone today
            if ((RunMods & ArenaMod.NoSupplies) != 0) Level.Things.RemoveAll(t => t is Pickup);
        }
        P = new Player { Class = cls, X = Level.StartX, Y = Level.StartY, Angle = Level.StartAngle };
        ApplyProfile();
        P.Health = P.MaxHealth;
        RailTrial = false;
        if (ArenaMode) SetUpQuakeArena();
        if (Level.Quake && !Practicing && !ArenaMode) SetUpQuakeMap();
        RunXp = 0; XpPopup = 0;
        Cheated = false; RunDeaths = 0;
        Intro = null; HitStop = 0; Shake = 0;
        ResetDirector();
        _nightmareThroughout = Difficulties.Of(Vars) == Difficulty.Nightmare;
        P.FloorZ = Level.FloorUnder(P.X, P.Y, P.Radius);
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
        if (Practicing) SetUpCourse(); // starting a course, or Restart on one
        if (StoryMode) SetUpCase();
        if (Rematch != null) SetUpRematch();
        BeginReplay();
    }

    static Thing Place(Thing t, Thing at, Level lv)
    {
        t.X = at.X; t.Y = at.Y; t.Level = lv;
        return t;
    }

    // ================================================================ update

    public void Update(Input inp, float dt)
    {
        CurrentReplay?.Frames.Add((dt, inp)); // the run's replay: every frame, as it came
        if (dt > 0) Fps += (1f / dt - Fps) * 0.05f;
        dt = MathF.Min(dt, 0.05f);
        Time += dt;
        AchievementTime -= dt;
        if (Difficulties.Of(Vars) != Difficulty.Nightmare) _nightmareThroughout = false;
        if (AchievementsOn && (_achieveCheck -= dt) <= 0) { _achieveCheck = 0.25f; Achievements.Check(this); }

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
        if (StoryInput(inp)) return;

        switch (Mode)
        {
            case GameMode.Title:
                Menu.Show(MenuPage.Main);
                return;
            case GameMode.ClassSelect:
                if (inp.Up) { MenuIndex = (MenuIndex + 2) % 3; PlaySound(Sfx.Swing, 0.6f); }
                if (inp.Down) { MenuIndex = (MenuIndex + 1) % 3; PlaySound(Sfx.Swing, 0.6f); }
                if (inp.Slot >= 1 && inp.Slot <= 3) MenuIndex = inp.Slot - 1;
                if ((inp.Slot >= 1 && inp.Slot <= 3) || inp.Confirm)
                {
                    if (PendingPractice) { PendingPractice = false; StartPractice((PClass)MenuIndex, PendingCourse); }
                    else if (PendingArena) { PendingArena = false; StartArena((PClass)MenuIndex); }
                    else if (PendingRematch is { } boss) { PendingRematch = null; StartRematch((PClass)MenuIndex, boss); }
                    else NewGame((PClass)MenuIndex);
                    PlaySound(Sfx.Teleport, 1);
                }
                if (inp.Pause) { PendingPractice = PendingArena = false; PendingRematch = null; GoToTitle(); }
                return;
            case GameMode.Victory:
                // a play-tested map starts over, so you can keep iterating; the hub goes back to the title
                // a classic campaign win offers the next New Game+ tier on Enter; Esc goes back to the title
                if (inp.Confirm && OffersNgPlus) { StartNewGamePlus(P.Class, NgTier + 1); PlaySound(Sfx.Teleport, 1); }
                else if (inp.Confirm || (inp.Pause && OffersNgPlus)) { if (StoryMode) GoToTitle(); else if (TestingMap) NewGame(P.Class); else GoToTitle(); }
                return;
        }

        if (inp.Pause || inp.Character)
        {
            Paused = true;
            Menu.Show(inp.Pause ? MenuPage.Pause : MenuPage.Character);
            PlaySound(Sfx.Swing, 0.6f);
            return;
        }
        if (inp.Map) ShowMap = !ShowMap;
        if (inp.CycleHud) CycleHud(1);
        if (!string.IsNullOrEmpty(inp.Typed) && Mode == GameMode.Playing)
            foreach (char c in inp.Typed) Con.FeedCheat(c);

        if (Practicing && Mode == GameMode.Playing && !PracticeControls(ref inp, ref dt)) return;
        if (ArenaMode && Mode == GameMode.Playing && Level.Arena?.Offer is { } offer)
        {
            // 1/2/3 take a perk straight away; Left/Right and Enter (or a pad's d-pad and A) pick one too
            var ar = Level.Arena;
            if (inp.Left || inp.Right) { ar.OfferCursor = (ar.OfferCursor + (inp.Left ? offer.Length - 1 : 1)) % offer.Length; PlaySound(Sfx.Swing, 0.5f); }
            if (inp.Slot >= 1 && inp.Slot <= offer.Length) ar.Choose(this, inp.Slot - 1);
            else if (inp.Confirm) ar.Choose(this, ar.OfferCursor);
            inp.Slot = 0;
        }

        PlayTime += dt;
        AutoSaveTick(dt);
        float step = FeelStep(dt); // a hit-stop, or a boss intro's slow motion
        UpdatePlayer(inp, step);
        HazardTick(step);
        DirectorTick(step);
        UpdateWorld(step);
        RematchTick(step);
        EliteTick(step);
        RangeTick(step);
        TrickTick(dt);
        TowerTick(step);
        SoccerTick(step);
        RoomLookTick(dt);
        QuakeArenaTick();
        TargetsTick(step);
        CheckBossIntros();
        Arcade.Update(dt);
        DigTarget = Mode == GameMode.Playing && !Level.Flight ? MineTarget(P.CurWeapon.Melee && !Relaxed ? P.CurWeapon.Range + 0.3f : 1.3f) : null;

        if (Mode == GameMode.Dead)
        {
            P.EyeZ = MathF.Max(0.12f, P.EyeZ - dt * 0.8f);
            if ((inp.Confirm || inp.Use) && P.EyeZ <= 0.13f)
            {
                if (CanRespawn) RespawnAtCheckpoint();
                else NewGame(P.Class);
            }
        }
    }

    /// <summary>The movement keys held last frame, -1/0/1 (forward/back, left/right), for the strafe helper.</summary>
    public int InMove, InStrafe;

    /// <summary>
    /// For a key combination whose wish direction points `phi` radians from your view (strafe right +pi/2, forward +
    /// right +pi/4, strafe left -pi/2), the view angles relative to your velocity's heading where air strafing adds
    /// speed (lo..hi) and where it adds the most (best). From Quake's air acceleration: speed is added only while your
    /// speed along the wish direction is under the air cap, and most when the wish direction sits just past square-on
    /// to your velocity, which narrows as you go faster.
    /// </summary>
    public (float best, float lo, float hi) StrafeZone(float phi)
    {
        float run = RunSpeed, v = MathF.Max(P.HSpeed, 0.01f), cap = AirCap * run, amax = Vars.AirAccel * run * MoveStep;
        float thLo = MathF.Acos(Math.Clamp(cap / v, -1f, 1f)), thBest = MathF.Acos(Math.Clamp((cap - amax) / v, -1f, 1f));
        float thHi = MathF.Acos(Math.Clamp(-amax / (2 * v), -1f, 1f));
        float side = phi < 0 ? -1 : 1;
        float a = side * thLo - phi, b = side * thHi - phi;
        return (side * thBest - phi, MathF.Min(a, b), MathF.Max(a, b));
    }

    /// <summary>What the strafe helper shows this frame (see StrafeAdvice).</summary>
    public readonly record struct StrafeTip(bool Air, int Side, bool WantForward, bool HeldOk, float Target, float Lo, float Hi, bool InZone, bool Jump);

    /// <summary>
    /// The strafe helper's advice, or null when it's hidden (turned off, not practising and not set to show
    /// everywhere, classic movement, flying, or standing still). In the air: the strafe key to hold (the side your view
    /// is on, or the combination you're already holding), and where to turn: Target, Lo and Hi are radians from your
    /// view to the best angle and the edges of the speed-gaining zone (positive to the right, the way the mouse turns
    /// you). On the ground: run forward, and jump once you're up to speed (or the moment you land, when you're fast).
    /// </summary>
    public StrafeTip? StrafeAdvice()
    {
        var p = P;
        if (p == null || Vars.StrafeHelp == 0 || (Vars.StrafeHelp == 1 && !Practicing) || !Vars.QuakeMove) return null;
        if (Mode != GameMode.Playing || Level.Flight || p.Flying) return null;
        float run = RunSpeed, v = p.HSpeed;
        bool air = !p.OnGround;
        if (!air) return new StrafeTip(false, 0, true, InMove > 0 && InStrafe == 0, 0, 0, 0, false, v > run * 0.9f); // jump once you're up to speed
        if (v < run * 0.3f) return null;
        static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
        float heading = MathF.Atan2(p.VY, p.VX);
        bool heldStrafe = InStrafe != 0 && InMove >= 0;
        float phi = heldStrafe ? MathF.Atan2(InStrafe, InMove) : (Wrap(p.Angle - heading) >= 0 ? MathF.PI / 2 : -MathF.PI / 2);
        var (best, lo, hi) = StrafeZone(phi);
        float target = Wrap(heading + best - p.Angle), zLo = Wrap(heading + lo - p.Angle), zHi = Wrap(heading + hi - p.Angle);
        bool falling = p.VZ < 0 && p.Z < 0.15f;
        return new StrafeTip(true, phi > 0 ? 1 : -1, heldStrafe && InMove > 0, heldStrafe, target, zLo, zHi, zLo <= 0 && zHi >= 0, falling);
    }

    /// <summary>Your full running speed, in map units a second.</summary>
    public float RunSpeed => 3.6f * P.Def.Speed * Vars.Speed * Profile.SpeedMult * (1 + 0.1f * PerkRank(Perk.Swiftness));

    const float JumpBufferTime = 0.15f;
    /// <summary>Quake's stop speed and air-control cap, as fractions of your run speed (100 and 30 of its 320).</summary>
    const float StopSpeed = 0.31f, AirCap = 0.094f;
    const float MoveStep = 1f / 72f;

    /// <summary>
    /// Quake movement. On the ground, friction slows you and you accelerate toward where your keys point, topping out
    /// at your run speed. In the air there's no friction, and acceleration only adds speed along your wish direction
    /// up to a small cap: pushing straight ahead gains nothing, but strafing sideways while you turn keeps adding a
    /// little. So strafe-jumping in a smooth arc builds speed, and jumping again the instant you land (bunny hopping)
    /// skips the ground friction and keeps it. On the jetpack you steer directly, as in classic movement, so you can
    /// set down on a narrow ledge.
    /// Returns this frame's displacement.
    /// </summary>
    (float dx, float dy) QuakeMove(Player p, float move, float strafe, float angle0, float wishSpeed, float runSpeed, bool jumping, float dt)
    {
        float wx = 0, wy = 0;
        void Wish(float angle)
        {
            float ca = MathF.Cos(angle), sa = MathF.Sin(angle);
            float mx = ca * move - sa * strafe, my = sa * move + ca * strafe, len = MathF.Sqrt(mx * mx + my * my);
            (wx, wy) = len > 0 ? (mx / len, my / len) : (0f, 0f);
        }
        Wish(p.Angle);
        if (p.Flying)
        {
            // the jetpack steers exactly where you point, so you can set down on a narrow ledge
            p.VX = wx * wishSpeed; p.VY = wy * wishSpeed;
        }
        else
        {
            // stepped at a fixed 72 Hz, as Quake's server was, each step steering by where you were looking at that
            // moment (this frame's turn is spread across its steps, as if the mouse moved smoothly), so strafing builds
            // speed the same at 35 frames a second as at 120
            float carried = p.MoveClock, turn = p.Angle - angle0;
            p.MoveClock = MathF.Min(p.MoveClock + dt, 0.25f);
            for (int k = 1; p.MoveClock >= MoveStep; k++)
            {
                p.MoveClock -= MoveStep;
                Wish(angle0 + turn * Math.Clamp((k * MoveStep - carried) / dt, 0f, 1f));
                if (p.OnGround && !jumping)
                {
                    float sp = p.HSpeed;
                    if (sp > 0)
                    {
                        float control = MathF.Max(sp, StopSpeed * runSpeed);
                        float keep = MathF.Max(0, sp - control * Vars.Friction * MoveStep) / sp;
                        p.VX *= keep; p.VY *= keep;
                    }
                    Accelerate(p, wx, wy, wishSpeed, wishSpeed, Vars.Accel, MoveStep);
                }
                else Accelerate(p, wx, wy, MathF.Min(wishSpeed, AirCap * runSpeed), wishSpeed, Vars.AirAccel, MoveStep);
            }
        }

        float max = MathF.Max(runSpeed * Vars.MaxHop, p.Boost), now = p.HSpeed;
        p.Boost = MathF.Max(0, p.Boost - dt * (p.OnGround ? 20f : 1.5f));
        if (now > max) { p.VX *= max / now; p.VY *= max / now; }
        return (p.VX * dt, p.VY * dt);
    }

    /// <summary>Quake's accelerate: add speed along (wx, wy) until your speed in that direction reaches `cap`.</summary>
    static void Accelerate(Player p, float wx, float wy, float cap, float wishSpeed, float accel, float dt)
    {
        float add = cap - (p.VX * wx + p.VY * wy);
        if (add <= 0) return;
        float a = MathF.Min(accel * wishSpeed * dt, add);
        p.VX += a * wx; p.VY += a * wy;
    }

    void UpdatePlayer(Input inp, float dt)
    {
        var p = P;
        XpPopupTime = MathF.Max(0, XpPopupTime - dt);
        p.DamageFlash = MathF.Max(0, p.DamageFlash - dt * 2);
        p.PickupFlash = MathF.Max(0, p.PickupFlash - dt * 3);
        p.TeleportFlash = MathF.Max(0, p.TeleportFlash - dt * 1.5f);
        if (Mode == GameMode.Dead) { p.Flying = false; p.Z = MathF.Max(0, p.Z - dt * 4f); return; }
        if (Level.Flight) { UpdateFlight(inp, dt); return; }

        // look
        float angle0 = p.Angle;
        ZoomTick(inp, dt);
        inp.LookX *= ZoomSens; inp.LookY *= ZoomSens; // zoomed in, the mouse slows to match
        p.Angle += inp.LookX * 0.0025f * Vars.Sens + inp.Turn * 2.6f * dt;
        // looking up and down stops at the limit (further down with the rocket and grenade launchers, and further up
        // with the grenade launcher, to lob); past it, having just put one away, you can't go further, and PitchLimit
        // eases you back
        bool ready = p.PendingWeapon < 0;
        float lookDown = p.CurWeapon.LooksDown && ready ? Rockets.LookDown : Rockets.NormalPitch;
        float lookUp = p.CurWeapon.Grenade && ready ? Rockets.LookDown : Rockets.NormalPitch;
        p.Pitch = Math.Clamp(p.Pitch - inp.LookY * 0.35f * Vars.Sens * (Vars.InvertMouse ? -1 : 1), MathF.Min(p.Pitch, -lookDown), MathF.Max(p.Pitch, lookUp));
        PitchLimit(p, dt);

        // move
        InMove = Math.Sign(inp.Move); InStrafe = Math.Sign(inp.Strafe);
        float speed = RunSpeed * (inp.Walk ? 0.5f : 1f);
        float ca = MathF.Cos(p.Angle), sa = MathF.Sin(p.Angle);
        float mx = (ca * inp.Move - sa * inp.Strafe), my = (sa * inp.Move + ca * inp.Strafe);
        float len = MathF.Sqrt(mx * mx + my * my);
        if (len > 1) { mx /= len; my /= len; }
        p.JumpBuffer = inp.Jump ? JumpBufferTime : MathF.Max(0, p.JumpBuffer - dt);
        bool jump = (inp.Jump || (Vars.QuakeMove && p.JumpBuffer > 0)) && p.OnGround && p.SlideTime <= 0 && Vars.JumpPower > 0;
        float dx, dy;
        if (Vars.QuakeMove) (dx, dy) = QuakeMove(p, inp.Move, inp.Strafe, angle0, MathF.Min(1, len) * speed, RunSpeed, jump, dt);
        else
        {
            // classic movement goes where your keys say, plus what's left of a blast's push
            dx = (mx * speed + p.VX) * dt; dy = (my * speed + p.VY) * dt;
            float keep = MathF.Exp(-(p.OnGround ? 6f : 0.5f) * dt);
            p.VX *= keep; p.VY *= keep;
            if (p.HSpeed < 0.05f) p.VX = p.VY = 0;
        }

        // jumping (Space): simple ballistic hop, Hexen-style
        if (jump)
        {
            p.JumpBuffer = 0;
            p.VZ = Vars.JumpPower;
            PlaySound(Sfx.Jump, 0.8f);
        }
        // jetpack: its own key (Q), so Jump stays free for bunny hopping. Hold it to take off (from the ground or mid-air)
        // and climb, hold Slide to sink, let go of both to hover.
        if (!inp.JetHeld) p.JetLock = false;
        if (!p.Flying && p.HasJetpack && inp.JetHeld && !p.JetLock && (p.Fuel > 0.25f || Vars.InfiniteFuel))
        {
            p.Flying = true; p.JetSfx = 0;
            PlaySound(Sfx.JetStart, 0.9f);
        }
        if (p.Flying)
        {
            float target = inp.JetHeld ? Player.ClimbSpeed : inp.SlideHeld ? -Player.SinkSpeed : 0f;
            p.VZ += (target - p.VZ) * MathF.Min(1, dt * 6);
            p.Z += p.VZ * dt;
            if (!Vars.InfiniteFuel) p.Fuel -= (inp.JetHeld ? 1f : 0.6f) * dt;
            p.JetSfx -= dt;
            if (p.JetSfx <= 0) { PlaySound(Sfx.Jet, inp.JetHeld ? 0.6f : 0.35f); p.JetSfx = 0.1f; }
            if (p.Fuel <= 0 && !Vars.InfiniteFuel) { p.Fuel = 0; p.Flying = false; p.JetLock = true; PlaySound(Sfx.JetOut, 1); Say("The Wings of Wrath falter!"); }
            if (p.Z <= 0) { p.Z = 0; p.VZ = 0; p.Flying = false; PlaySound(Sfx.Land, 0.4f); }
        }
        else if (!p.OnGround || p.VZ > 0)
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
            int steps = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) / 0.2f));
            float sx = dx / steps, sy = dy / steps;
            for (int i = 0; i < steps; i++)
            {
                if (sx != 0 && !Blocked(p.X + sx, p.Y, p.Radius, null, p.SlideTime > 0)) p.X += sx;
                else if (sx != 0) { sx = 0; WallSlide(p, ref p.VX, ref p.VY); }
                if (sy != 0 && !Blocked(p.X, p.Y + sy, p.Radius, null, p.SlideTime > 0)) p.Y += sy;
                else if (sy != 0) { sy = 0; WallSlide(p, ref p.VY, ref p.VX); }
            }
        }

        // stairs and ledges: step up smoothly, fall off edges
        float floor = Vars.NoClip ? Level.FloorAt(p.X, p.Y) : Level.FloorUnder(p.X, p.Y, p.Radius);
        if (floor < p.FloorZ) p.Z += p.FloorZ - floor;                 // walked off an edge: now airborne
        else if (floor > p.FloorZ)
        {
            float up = floor - p.FloorZ;
            if (p.Z >= up) p.Z -= up;                                   // landed on a ledge mid-jump
            else { p.StepLag -= up - p.Z; p.Z = 0; }                   // stepped up: ease the camera
        }
        p.FloorZ = floor;
        p.StepLag *= MathF.Exp(-dt * 14f);
        if (p.Flying && p.Z <= 0) { p.Z = 0; p.VZ = 0; p.Flying = false; PlaySound(Sfx.Land, 0.4f); } // touched down on a ledge
        if (p.OnGround && !p.Flying && p.HasJetpack) p.Fuel = MathF.Min(p.MaxFuel, p.Fuel + dt * 1.5f * Profile.FuelMult);
        // bump your head on low ceilings
        float headroom = Level.HeightAt(p.X, p.Y) - p.FloorZ - Player.Height - 0.05f;
        if (p.Z > headroom) { p.Z = MathF.Max(0, headroom); if (p.VZ > 0) p.VZ = 0; }
        float moving = p.OnGround && p.SlideTime <= 0 ? MathF.Min(1, len) : 0f;
        p.BobAmount += (moving - p.BobAmount) * MathF.Min(1, dt * 8);
        p.Bob += dt * 9 * moving;

        UpdateCheckpoints(p, dt);
        if (Practicing)
        {
            // the clock starts when you leave the spot you started on
            if (!RunStarted && Course.Timed && !Course.Endless && Dist(p.X, p.Y, Level.StartX, Level.StartY) > 0.3f) RunStarted = true;
            if (RunStarted)
            {
                RunTime += dt;
                Recording.Record(RunTime, p.X, p.Y, p.FloorZ + p.Z);
            }
            bool wantGhost = Vars.Ghost && !Demo && (Course.Timed || RivalHere) && (DemoTrack != null || RivalHere || Profile.Ghosts.ContainsKey(Course.Key(p.Class)));
            if (wantGhost != (Ghost != null)) ShowGhost();
            Ghost?.Seek(RunTime);
        }

        // portals & exit
        char mark = Level.MarkAt(p.X, p.Y);
        if (char.IsDigit(mark))
        {
            if (!p.PortalLock && Level.Ship is { Built: false })
            {
                p.PortalLock = true;
                Say(Words.T("The portal is burnt out. Repair your skyship to get home."));
                PlaySound(Sfx.Locked, 0.8f);
            }
            if (!p.PortalLock) Teleport(mark);
        }
        else p.PortalLock = false;
        _exitMsgCd -= dt;
        if (mark == 'E' && Practicing)
        {
            // a practice exit finishes the run once you've been through every checkpoint (no cutting the lap short)
            int targetsLeft = CourseTargetsLeft;
            if (Level.CheckpointsReached.Count >= Level.Checkpoints.Count && targetsLeft == 0) { FinishRun(); return; }
            if (targetsLeft > 0 && _exitMsgCd <= 0)
            {
                Say($"The exit opens once every target is down ({targetsLeft} left).");
                _exitMsgCd = 3;
            }
            if (_exitMsgCd <= 0)
            {
                Say($"Reach every checkpoint first ({Level.CheckpointsReached.Count} of {Level.Checkpoints.Count}).");
                _exitMsgCd = 3;
            }
        }
        else if (mark == 'E')
        {
            if (Relaxed ? P.Relics >= RelicsTotal : Level.BossDead)
            {
                Mode = GameMode.Victory;
                PlaySound(Sfx.Teleport, 1);
                if (!TestingMap) DeleteSave(); // the campaign's over: nothing to continue
                if (!TestingMap)
                {
                    Profile.Wins++;
                    if (Relaxed) Profile.RelaxedWins++;
                    else
                    {
                        Profile.ClassicWins++;
                        if (RunDeaths == 0) Profile.FlawlessWins++;
                        if (_nightmareThroughout && Difficulties.Of(Vars) == Difficulty.Nightmare) Profile.NightmareWins++;
                        if (!Profile.ClassWins.Contains(P.Class.ToString())) Profile.ClassWins.Add(P.Class.ToString());
                        NgPlusWon();
                    }
                    GainXp(Xp.Victory);
                    Achievements.Check(this);
                }
                SaveProfile();
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
            if (t is Pickup pk && !pk.Removed && Dist(t.X, t.Y, p.X, p.Y) < 0.55f
                && MathF.Abs(Level.FloorAt(t.X, t.Y) - (p.FloorZ + p.Z)) < 0.8f)
                TryPickup(pk);

        // actions
        if (inp.Use && OnRange) { if (P.CurWeapon.Rail) StartRailTrial(); else StartDrill(); } // the railgun in hand: a rail trial
        else if (inp.Use) UseLine(pull: inp.Walk);
        if (inp.UseItem) UseItem();
        if (inp.Place) PlaceBlock();

        // weapons
        int nw = p.HasWeapon.Length;
        if (inp.Slot >= 1 && inp.Slot <= nw) SelectWeapon(inp.Slot - 1);
        if (inp.Cycle != 0)
            for (int k = 1; k <= nw; k++)
            {
                int w = ((p.Weapon + inp.Cycle * k) % nw + nw) % nw;
                if (p.HasWeapon[w]) { SelectWeapon(w); break; }
            }
        if (p.PendingWeapon >= 0)
        {
            p.Raise += dt * (p.Weapons[p.PendingWeapon].Quick ? QuakeAmmo.SwitchSpeed : 5);
            if (p.Raise >= 1) { p.Weapon = p.PendingWeapon; p.PendingWeapon = -1; }
        }
        else p.Raise = MathF.Max(0, p.Raise - dt * (p.CurWeapon.Quick ? QuakeAmmo.SwitchSpeed : 5));

        p.Cooldown -= dt;
        p.FireAnim = MathF.Max(0, p.FireAnim - dt);
        if (!ChargeTrigger(inp, dt) && inp.Fire && !Relaxed && !StoryMode && p.Cooldown <= 0 && p.PendingWeapon < 0 && p.Raise < 0.2f) Fire();
    }

    void SelectWeapon(int w)
    {
        if (w > 0 && ArenaMode && Level.Arena is { } a && a.Has(ArenaMod.MeleeOnly))
        {
            if (P.HasWeapon[w]) Say("Melee only!");
            return;
        }
        if (w >= P.HasWeapon.Length || !P.HasWeapon[w] || (w == P.Weapon && P.PendingWeapon < 0)) return;
        P.PendingWeapon = w;
        Say(P.Weapons[w].Name);
    }

    int ManaCost(WeaponDef w) => Vars.InfiniteMana ? 0 : (int)MathF.Ceiling(w.Cost * Vars.ManaCost * Profile.ManaMult);
    bool HasMana(WeaponDef w) => w.Ammo != AmmoKind.None ? Vars.InfiniteMana || P.Ammo[(int)w.Ammo] >= w.Cost
        : w.Mana == 0 || (w.Mana == 1 ? P.BlueMana : P.GreenMana) >= ManaCost(w);

    /// <summary>Fires the weapon in hand; `power` multiplies its damage (a Charged mod's charged shot).</summary>
    void Fire(float power = 1)
    {
        var p = P;
        var w = p.CurWeapon;
        int pierce = ModRank(p.Weapon, WeaponMod.Piercing);
        bool lance = ComboOn(p.Weapon) == WeaponMods.Combo.Lance && power >= WeaponMods.ChargedHit;
        bool powered = HasMana(w);
        if (!powered && !w.ManaOptional)
        {
            // out of mana: fall back to the best weapon that still works
            for (int i = p.HasWeapon.Length - 1; i >= 0; i--)
                if (p.HasWeapon[i] && HasMana(p.Weapons[i])) { SelectWeapon(i); break; }
            p.Cooldown = 0.3f;
            return;
        }
        if (Drilling) DrillShots++;
        if (w.Ammo != AmmoKind.None && !Vars.InfiniteMana) p.Ammo[(int)w.Ammo] -= w.Cost;
        if (powered && w.Mana == 1) p.BlueMana -= ManaCost(w);
        if (powered && w.Mana == 2) p.GreenMana -= ManaCost(w);
        int tier = ArsenalTier;
        p.Cooldown = w.Cooldown / MathF.Max(0.05f, Vars.FireRate * Profile.FireRateMult * Arsenal.FireRate(tier) * (1 + 0.2f * PerkRank(Perk.RapidFire)));
        p.FireAnim = 0.22f;
        PlaySound(w.Sound, 1);
        WakeNear(p.X, p.Y, 10f);

        if (w.Melee)
        {
            // an upgraded arsenal reaches further, and from the second upgrade cleaves through several at once
            float range = w.Range + Arsenal.Reach(tier);
            var hits = new List<(Monster m, float d)>();
            foreach (var t in Level.Things)
            {
                if (t is not Monster m || !m.Alive || m.Blurring) continue;
                float d = Dist(m.X, m.Y, p.X, p.Y);
                if (d > range + m.Radius) continue;
                float diff = AngleDiff(MathF.Atan2(m.Y - p.Y, m.X - p.X), p.Angle);
                if (MathF.Abs(diff) > 0.45f + (tier >= 2 ? 0.25f : 0f) || !Level.Sight(p.X, p.Y, m.X, m.Y)) continue;
                hits.Add((m, d));
            }
            if (hits.Count > 0)
            {
                foreach (var (best, _) in hits.OrderBy(h => h.d).Take(Arsenal.Cleave(tier) + (pierce > 0 ? WeaponMods.CleaveExtra(pierce) : 0) + (lance ? 3 : 0)))
                {
                    int dmg = (int)MathF.Round(Rand(w.DmgMin, w.DmgMax) * PlayerDamageMult(p.Weapon) * Arsenal.Damage(tier) * power);
                    if (!powered) dmg /= 2;
                    float bz = Level.FloorAt(best.X, best.Y) + best.Z + best.SpriteH * 0.5f;
                    if (tier > 0) SpawnPuff(Art.Lightning[1], best.X, best.Y, bz, 0.35f + 0.08f * tier);
                    else if (powered && w.Mana > 0) SpawnPuff(Art.Bolt[1], best.X, best.Y, bz, 0.4f);
                    else SpawnPuff(Art.Fireball[1], best.X, best.Y, bz, 0.25f);
                    _hitPower = power;
                    DamageMonster(best, dmg, p.Weapon);
                    _hitPower = 1;
                }
                PlaySound(Sfx.Hit, 1);
            }
            else
            {
                var target = MineTarget(w.Range + 0.3f);
                int dmg = Rand(w.DmgMin, w.DmgMax);
                if (target is (var bx, var by, var face, var slot)) HitBlock(bx, by, powered ? dmg : dmg / 2, face, slot: slot);
            }
            return;
        }

        float launchZ = p.FloorZ + p.Z + 0.32f;
        if (w.Rocket) { FireRocket(w, launchZ, PlayerDamageMult(p.Weapon) * Arsenal.Damage(tier) * power); return; }
        if (w.Rail) { FireRail(w, PlayerDamageMult(p.Weapon) * Arsenal.Damage(tier) * power); return; }
        if (w.Grenade) { FireGrenade(w, launchZ); return; }
        if (w.Shotgun) { FireShotgun(w, PlayerDamageMult(p.Weapon) * Arsenal.Damage(tier) * power); return; }
        if (w.Beam) { FireBeam(w, PlayerDamageMult(p.Weapon) * Arsenal.Damage(tier) * power); return; }
        float? vz = VerticalAim(launchZ, w.Speed);
        float mult = PlayerDamageMult(p.Weapon) * Arsenal.Damage(tier) * power;
        // upgrades add shots to the volley, fanned out either side of your aim
        int count = w.Count + Arsenal.ExtraShots(tier);
        float spread = w.Spread > 0 ? w.Spread : 0.07f;
        for (int i = 0; i < count; i++)
        {
            float a = p.Angle + (i - (count - 1) / 2f) * spread;
            var pr = new Projectile
            {
                Kind = w.Proj, FromPlayer = true, Splash = Arsenal.Splash(w.Splash, tier), Owner = null, Slot = p.Weapon,
                DmgMin = (int)MathF.Round(w.DmgMin * mult), DmgMax = (int)MathF.Round(w.DmgMax * mult),
                X = p.X + MathF.Cos(a) * 0.3f, Y = p.Y + MathF.Sin(a) * 0.3f, Z = launchZ,
                VX = MathF.Cos(a) * w.Speed, VY = MathF.Sin(a) * w.Speed, Level = Level,
                VZ = vz ?? 0f, Aimed = vz != null,
            };
            if (w.Proj == ProjKind.Hammer || w.Proj == ProjKind.Flame) { pr.SpriteW = pr.SpriteH = 0.4f; }
            if (power > 1.2f) { pr.SpriteW *= 1 + (power - 1) * 0.4f; pr.SpriteH = pr.SpriteW; } // a charged shot is bigger
            if (pierce > 0) pr.Pierce = WeaponMods.PierceCount(pierce);
            if (lance) pr.Pierce = 1000; // a Lance goes through everything
            pr.Power = power;
            Level.Things.Add(pr);
        }
    }

    /// <summary>
    /// Climb rate for your shots when there's height to cover: straight at a monster above or below you
    /// (Hexen-style vertical auto-aim), or along your view when you're up in the air or looking well up or down.
    /// Null means the usual level shot, which follows the floor.
    /// </summary>
    float? VerticalAim(float launchZ, float speed)
    {
        var p = P;
        Monster best = null;
        float bestDiff = 0.15f, bestD = 0;
        foreach (var t in Level.Things)
        {
            if (t is not Monster m || !m.Alive || m.Blurring) continue;
            float d = Dist(m.X, m.Y, p.X, p.Y);
            if (d > 24f || d < 0.3f) continue;
            float diff = MathF.Abs(AngleDiff(MathF.Atan2(m.Y - p.Y, m.X - p.X), p.Angle));
            if (diff >= bestDiff || !Level.Sight(p.X, p.Y, m.X, m.Y)) continue;
            best = m; bestDiff = diff; bestD = d;
        }
        if (best != null)
        {
            float mz = Level.FloorAt(best.X, best.Y) + best.Z + best.SpriteH * 0.5f;
            if (MathF.Abs(mz - launchZ) > 0.45f) return (mz - launchZ) * speed / bestD;
            return null;
        }
        if (p.Z > 0.6f || MathF.Abs(p.Pitch) > 25f)
        {
            float proj = 160f / MathF.Tan(ViewFov * MathF.PI / 360f);
            return speed * p.Pitch / proj;
        }
        return null;
    }

    /// <summary>Dying in the map of your last checkpoint sends you back there instead of restarting the game.</summary>
    public bool CanRespawn => Checkpoint != null && Checkpoint.Level == Level;

    /// <summary>Landing on a ledge lights its checkpoint pad; the lift pad takes you back up to the highest one.</summary>
    void UpdateCheckpoints(Player p, float dt)
    {
        _liftMsgCd -= dt;
        var lv = Level;
        int cx = (int)MathF.Floor(p.X), cy = (int)MathF.Floor(p.Y);
        if (!lv.InBounds(cx, cy)) return;
        int cell = cy * lv.W + cx;
        if (p.OnGround && !p.Flying && MathF.Abs(lv.Floors[cell] - p.FloorZ) < 0.01f)
        {
            int zone = lv.CheckpointZone[cell];
            if (zone >= 0 && lv.CheckpointsReached.Add(zone))
            {
                int pad = lv.Checkpoints[zone];
                float floor = lv.Floors[pad];
                // respawn at the highest pad you've lit, so dropping back to a lower ledge doesn't lose progress (on
                // ledges of equal height, the newest)
                if (Checkpoint == null || Checkpoint.Level != lv || floor >= Checkpoint.Floor || Practicing)
                    Checkpoint = new Checkpoint
                    {
                        Level = lv, Index = zone, X = pad % lv.W + 0.5f, Y = pad / lv.W + 0.5f, Floor = floor, Angle = p.Angle,
                        Health = Math.Max(p.Health, 50), Armor = p.Armor,
                    };
                PlaySound(Sfx.Secret, 0.7f);
                if (OnEndless) EndlessPlatform(zone);
                if (OnTower) TowerLanded(zone);
                if (Practicing) Say(CourseHint(zone) + GhostSplit(zone));
                else { Say($"Checkpoint reached ({lv.CheckpointsReached.Count} of {lv.Checkpoints.Count})."); SaveNow(); }
            }
        }

        // on the lift pad itself: down at its floor, not standing on a ledge's lip with your middle over it
        bool onLift = lv.Marks[cell] == '=' && p.OnGround && MathF.Abs(p.FloorZ - lv.Floors[cell]) < 0.01f;
        if (onLift && !_onLift)
        {
            if (OnEndless) { EndEndlessRun(false); onLift = false; } // on the endless course a fall ends the run
            else if (OnTower) { EndTowerRun(false); onLift = false; } // on the tower too
            else if (Checkpoint != null && Checkpoint.Level == lv)
            {
                MoveTo(Checkpoint.X, Checkpoint.Y, Checkpoint.Angle);
                PlaySound(Sfx.Teleport, 1);
                Say("The lift carries you up to your checkpoint.");
                onLift = false;
            }
            else if (_liftMsgCd <= 0)
            {
                Say(lv.Checkpoints.Count > 0 ? "The lift pad is dark. Reach a ledge's checkpoint first." : "The lift pad is dark.");
                _liftMsgCd = 3;
            }
        }
        _onLift = onLift;
    }

    void MoveTo(float x, float y, float angle)
    {
        var p = P;
        p.X = x; p.Y = y; p.Angle = angle;
        p.FloorZ = Level.FloorUnder(x, y, p.Radius); p.Z = 0; p.VZ = 0; p.VX = p.VY = 0; p.Flying = false;
        p.StepLag = 0; p.SlideTime = 0; p.SlideLow = 0;
        p.TeleportFlash = 1;
    }

    /// <summary>Back on your feet at the checkpoint: its health and armor, a full jetpack, everything else kept.</summary>
    public void RespawnAtCheckpoint()
    {
        var c = Checkpoint;
        var p = P;
        foreach (var t in Level.Things) if (t is Projectile or Puff) t.Removed = true;
        MoveTo(c.X, c.Y, c.Angle);
        p.Dead = false;
        p.Health = Math.Max(c.Health, 50);
        p.Armor = Math.Max(p.Armor, c.Armor);
        p.EyeZ = 0.5f; p.DamageFlash = 0; p.Pitch = 0;
        if (p.HasJetpack) p.Fuel = p.MaxFuel;
        Mode = GameMode.Playing;
        Messages.Clear();
        PlaySound(Sfx.Teleport, 1);
        Say("Back at your checkpoint.");
        if (Level.Flight) { p.Z = FlightStartZ; p.ShipSpeed = FlightCruise; p.Health = p.MaxHealth; }
    }

    void UseLine(bool pull)
    {
        var p = P;
        if (TryDetective() || TryReadLore() || TryOpenChest() || TryUseShip()) return;
        if (Level.Dig && MineTarget(1.3f) is (var mx, var my, var mf, var ms))
        {
            if (p.Cooldown <= 0) { HitBlock(mx, my, Level.RubbleHp / 3 + 1, mf, slot: ms); p.Cooldown = 0.45f; }
            return;
        }
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
                            GainXp(Xp.Secret);
                            PlaySound(Sfx.Secret, 1);
                            Say($"A secret passage! ({p.Secrets}/{SecretsTotal} secrets)");
                        }
                    }
                    break;
                case 'X':
                    MoveBlock(cx, cy, pull);
                    break;
                case Level.Rubble:
                case 'N':
                case 'Q':
                case 'U':
                    // prying at it by hand works too, slowly (and it's the only way in relaxed mode)
                    if (p.Cooldown <= 0) { HitBlock(cx, cy, Level.RubbleHp / 3 + 1); p.Cooldown = 0.45f; }
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
        if (!best.Read) { best.Read = true; P.LoreRead++; GainXp(Xp.Lore); }
        ReadingLore = best.Text;
        PlaySound(Sfx.Lore, 1);
        return true;
    }

    /// <summary>Ore names by Level.OreGlyphs index, as written (Words.T gives the sci-fi ones).</summary>
    public static readonly string[] OreNames = { "iron ore", "moonstone", "brimstone" };

    /// <summary>What the ship still needs, e.g. "2 iron ore, 3 brimstone".</summary>
    static string ShipNeeds(Ship s) => string.Join(", ", Enumerable.Range(0, Ship.Need.Length)
        .Where(k => s.Delivered[k] < Ship.Need[k]).Select(k => $"{Ship.Need[k] - s.Delivered[k]} {Words.T(OreNames[k])}"));

    /// <summary>Use on the wrecked ship: hand over the ore it needs; once repaired, Use it again to fly home.</summary>
    bool TryUseShip()
    {
        var s = Level.Ship;
        if (s == null || Dist(s.X, s.Y, P.X, P.Y) > s.Radius + 1.2f) return false;
        if (MathF.Abs(AngleDiff(MathF.Atan2(s.Y - P.Y, s.X - P.X), P.Angle)) > 0.7f) return false;
        if (s.Built) { Launch(); return true; }
        int given = 0;
        for (int k = 0; k < Ship.Need.Length; k++)
        {
            int n = Math.Min(P.Ore[k], Ship.Need[k] - s.Delivered[k]);
            P.Ore[k] -= n; s.Delivered[k] += n; given += n;
        }
        if (s.Built) { Say(Words.T("The skyship is repaired! Use it again to take off.")); PlaySound(Sfx.Item, 1); }
        else if (given > 0) { Say($"{Words.T("Repairs under way.")} {Words.T("Still needed:")} {ShipNeeds(s)}"); PlaySound(Sfx.Lever, 1); }
        else { Say($"{Words.T("The skyship needs")} {ShipNeeds(s)}. {Words.T("Mine the ore veins in the rocks.")}"); PlaySound(Sfx.Locked, 0.6f); }
        return true;
    }

    /// <summary>Take off in the repaired ship: out into the flight lane if the hub has one, else home through the portal link.</summary>
    void Launch()
    {
        P.Health = Math.Max(P.Health, P.MaxHealth);
        int lane = Array.FindIndex(Hub, l => l.Flight);
        if (lane >= 0) { Warp(lane); return; }
        Teleport(Level.Marks.FirstOrDefault(char.IsDigit));
        Messages.Clear();
        Say(Words.T("Lift-off! You leave the barren world behind and make it home."));
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
        Profile.ChestsOpened++;
        GainXp(Xp.Chest);
        PlaySound(Sfx.Chest, 1);

        // spill loot toward the player so it's easy to grab
        float dx = P.X - c.X, dy = P.Y - c.Y, l = MathF.Max(0.01f, MathF.Sqrt(dx * dx + dy * dy));
        dx /= l; dy /= l;
        var loot = Chests.RollLoot(_loot, P, WeaponMods.ChestWeight(NgTier));
        for (int i = 0; i < loot.Count; i++)
        {
            float side = (i - (loot.Count - 1) / 2f) * 0.35f;
            float x = c.X + dx * 0.6f - dy * side, y = c.Y + dy * 0.6f + dx * side;
            if (Level.BlocksPoint(x, y)) { x = c.X + dx * 0.5f; y = c.Y + dy * 0.5f; }
            var t = loot[i] == 'm' ? MakeMod(RandomMod(), x, y) : ThingFactory.Create(loot[i], x, y);
            t.Level = Level;
            Level.Things.Add(t);
        }
        SpawnPuff(Art.Fireball[1], c.X, c.Y, Level.FloorAt(c.X, c.Y) + 0.35f, 0.3f);

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
        else Say(Words.T("Chest: ") + string.Join(", ", loot.Select(g => Words.T(Chests.LootNames[g])).Distinct()));
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
            if (!Level.BlockCanEnter(tx, ty, bx, by) || PlayerTouchesCell(tx, ty)) { Say("The block won't budge that way."); PlaySound(Sfx.Locked, 0.6f); return; }
        }
        else
        {
            tx = bx - sx; ty = by - sy;
            // step back one cell (snapping to its centre on the pull axis)
            if (sx != 0) nx = tx - sx + 0.5f; else ny = ty - sy + 0.5f;
            if (!Level.BlockCanEnter(tx, ty, bx, by) || Level.BlocksCircle(nx, ny, p.Radius) || Blocked(nx, ny, p.Radius, null))
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
        SpawnPuff(Art.Shard[1], tx + 0.5f - sx * 0.5f, ty + 0.5f - sy * 0.5f, Level.Floors[to] + 0.1f, 0.4f);

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

    // ================================================================ character progression

    /// <summary>Your progress, kept between games (see Profile).</summary>
    public Profile Profile = new();
    /// <summary>Where the profile is saved; null (as in tests) keeps it in memory only.</summary>
    public string ProfilePath;
    /// <summary>Experience earned this game, and the "+XP" pop-up by the level bar.</summary>
    public int RunXp, XpPopup;

    /// <summary>A cheat's been used this game (console give, kill, god mode...): no achievements until a new one.</summary>
    public bool Cheated;
    /// <summary>Check for achievements as you play (the screenshot tool turns it off, so no banner photobombs a shot).</summary>
    public bool AchievementsOn = true;
    /// <summary>Deaths this game, and whether it's been on Nightmare all along, for the achievements.</summary>
    public int RunDeaths;
    bool _nightmareThroughout;
    float _achieveCheck;
    /// <summary>The last achievement unlocked, shown as a banner for a few seconds.</summary>
    public AchievementDef AchievementBanner;
    public float AchievementTime;

    /// <summary>An achievement's just unlocked: say so, show the banner, and pay its experience (anywhere, practice included).</summary>
    public void AchievementUnlocked(AchievementDef a)
    {
        Say($"Achievement unlocked: {a.Name}! +{a.Xp} XP");
        AchievementBanner = a;
        AchievementTime = 4f;
        PlaySound(Sfx.Secret, 1);
        GainXp(a.Xp, always: true);
        SaveProfile();
    }
    public float XpPopupTime;

    /// <summary>Experience for everything you do.</summary>
    public static class Xp
    {
        public const int Secret = 50, Lore = 25, Chest = 15, Relic = 40, Victory = 300, BossBonus = 500, PerWave = 20;
        public static int Kill(MonsterDef d) => Math.Max(5, d.Health / 2) + (d.Boss ? BossBonus : 0);
    }

    public void LoadProfile() => Profile = Profile.Load(ProfilePath);
    public void SaveProfile() => Profile.Save(ProfilePath);

    /// <summary>Pushes the profile's skills onto the player (after a new game or spending a point).</summary>
    public void ApplyProfile()
    {
        var p = P;
        if (p == null) return;
        int oldMax = p.MaxHealth;
        p.MaxHealth = Profile.MaxHealth + 25 * PerkRank(Perk.Vitality);
        if (p.MaxHealth > oldMax) p.Health += p.MaxHealth - oldMax;
        p.Health = Math.Min(p.Health, p.MaxHealth);
        float oldFuel = p.MaxFuel;
        p.MaxFuel = Player.FuelMax * Profile.FuelMult;
        if (p.HasJetpack && p.MaxFuel > oldFuel) p.Fuel += p.MaxFuel - oldFuel;
    }

    internal float PlayerDamageMult(int slot) => Profile.DamageMult * LoadoutWeaponMult(slot) * (1 + 0.2f * PerkRank(Perk.Might));

    /// <summary>A weapon's level bonus: your class's own by slot, or in the range's loadout the one it belongs to (the rocket launcher has none).</summary>
    float LoadoutWeaponMult(int slot)
    {
        if (P.Loadout == null) return Profile.WeaponMult(P.Class, slot);
        var w = slot >= 0 && slot < P.Loadout.Length ? P.Loadout[slot] : null;
        if (w == null || w.Quick || w.ArtIndex >= 9) return 1f;
        return Profile.WeaponMult((PClass)(w.ArtIndex / 3), w.ArtIndex % 3);
    }
    /// <summary>Your arsenal upgrades, which only count in the arena they were won in.</summary>
    public int ArsenalTier => Level?.Arena != null ? P.ArenaTier : 0;

    /// <summary>Play-testing a custom map, or on a practice course, earns no experience; the arena does.</summary>
    bool NoXp => TestingMap && !ArenaMode;

    /// <summary>Adds experience, announcing level-ups.</summary>
    public void GainXp(int amount, bool always = false)
    {
        if (amount <= 0 || (NoXp && !always)) return;
        RunXp += amount;
        XpPopup = XpPopupTime > 0 ? XpPopup + amount : amount;
        XpPopupTime = 1.6f;
        int levels = Profile.AddXp(amount);
        if (levels > 0)
        {
            Say($"Level up! You are level {Profile.Level}. Press {Keys.Name(Binds.Get(Act.Character, 0))} to spend skill points.");
            PlaySound(Sfx.Secret, 1);
            if (P != null) P.PickupFlash = 1;
            SaveProfile();
        }
    }

    void KilledWith(Monster m, int slot)
    {
        if (NoXp) return;
        int xp = (int)(Xp.Kill(m.Def) * NgPlus.Xp(NgTier) * (m.Affix != Affix.None ? Elites.XpMult : 1));
        Profile.TotalKills++;
        GainXp(xp);
        if (slot is >= 0 and < 3 && P.Weapons[slot].ArtIndex == (int)P.Class * 3 + slot && Profile.AddWeaponXp(P.Class, slot, xp))
        {
            var w = P.Def.Weapons[slot];
            Say($"{w.Name} is now level {Profile.Weapon(P.Class, slot).Level}!");
            PlaySound(Sfx.Item, 1);
            SaveProfile();
        }
    }

    /// <summary>Spends a skill point; the effect applies at once.</summary>
    public bool SpendSkill(Skill s)
    {
        if (!Profile.Spend(s)) return false;
        ApplyProfile();
        SaveProfile();
        return true;
    }

    void UseItem()
    {
        var p = P;
        if (p.Health >= p.MaxHealth) { Say("You are already at full health."); return; }
        if (p.Flasks > 0 && (p.Health > p.MaxHealth / 2 || p.Urns == 0)) { p.Flasks--; p.Health = Math.Min(p.MaxHealth, p.Health + 25); Say("Quartz Flask: +25 health"); }
        else if (p.Urns > 0) { p.Urns--; p.Health = p.MaxHealth; Say("Mystic Urn: fully healed!"); }
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
                if (p.Health >= p.MaxHealth) return;
                p.Health = Math.Min(p.MaxHealth, p.Health + 10); msg = "Crystal Vial"; break;
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
                GainXp(Xp.Relic);
                msg = $"Relic found: {pk.Name ?? "an ancient relic"} ({p.Relics}/{RelicsTotal})";
                if (Relaxed && p.Relics >= RelicsTotal) msg += " - the exit portal awakens!";
                pk.Removed = true;
                p.PickupFlash = 1;
                PlaySound(Sfx.Relic, 1);
                Say(msg);
                return;
            case PickupKind.Arms:
                if (OnRange) TakeFromRack(pk); else TakeStash(pk);
                return;
            case PickupKind.Ammo:
                if (!TakeAmmo(pk)) return;
                pk.Removed = true;
                return;
            case PickupKind.Mod:
                if (!FitMod((WeaponMod)pk.Variant, out msg)) return;
                pk.Removed = true;
                p.PickupFlash = 1;
                PlaySound(Sfx.Item, 1);
                PlaySound(Sfx.Magic, 0.6f);
                Say(msg);
                return;
            case PickupKind.Upgrade:
                if (p.ArenaTier >= Arsenal.MaxTier) return;
                p.ArenaTier++;
                msg = $"Arsenal upgrade: {Words.T(Arsenal.Name(p.ArenaTier))}! {Arsenal.Describe(p.ArenaTier)}";
                pk.Removed = true;
                p.PickupFlash = 1;
                PlaySound(Sfx.BossSight, 0.7f);
                PlaySound(Sfx.Item, 1);
                Say(msg);
                return;
            case PickupKind.Jetpack:
                if (p.HasJetpack && p.Fuel >= p.MaxFuel) return;
                msg = p.HasJetpack ? "Wings of Wrath: recharged" : "Wings of Wrath! Hold Q to fly. Hold Slide to sink.";
                p.HasJetpack = true; p.Fuel = p.MaxFuel;
                break;
            case PickupKind.Armor:
                if (p.Armor >= 100) return;
                p.Armor = Math.Min(100, p.Armor + 50); msg = "Mesh Armor"; break;
            case PickupKind.Weapon2:
            case PickupKind.Weapon3:
                {
                    int slot = pk.Kind == PickupKind.Weapon2 ? 1 : 2;
                    if (p.Loadout != null) return; // a practice loadout has no room for the class's pieces
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
        PlaySound(pk.Kind is PickupKind.Weapon2 or PickupKind.Weapon3 or PickupKind.SteelKey or PickupKind.FireKey or PickupKind.Jetpack ? Sfx.Item : Sfx.Pickup, 1);
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
            P.FloorZ = lv.FloorUnder(P.X, P.Y, P.Radius); P.Z = 0; P.VZ = 0; P.VX = P.VY = 0; P.Flying = false;
            P.PortalLock = true;
            P.TeleportFlash = 1;
            // drop any in-flight projectiles from the level we left
            foreach (var t in Hub.SelectMany(l => l.Things)) if (t is Projectile or Puff) t.Removed = true;
            PlaySound(Sfx.Teleport, 1);
            Say(lv.EntryMessage);
            if (lv.Flight) EnterFlight();
            else SaveNow();
            return;
        }
    }

    // ================================================================ flight

    public const float FlightSlow = 3f, FlightCruise = 5f, FlightFast = 8f, FlightTop = 3.4f, FlightStartZ = 1.3f;
    const float ShipHalfHeight = 0.28f;

    /// <summary>
    /// Arriving on a flight map: into the pilot's seat at the start of the lane, which is also where you come back
    /// if the hull gives out. Pickups float up into the lanes and flyers take to the air around you.
    /// </summary>
    void EnterFlight()
    {
        var lv = Level;
        MoveTo(lv.StartX, lv.StartY, 0);
        P.Z = FlightStartZ; P.Flying = true; P.ShipSpeed = FlightCruise; P.Pitch = 0; P.PortalLock = true;
        P.Health = Math.Max(P.Health, P.MaxHealth);
        Checkpoint = new Checkpoint { Level = lv, X = lv.StartX, Y = lv.StartY, Floor = 0, Angle = 0, Health = P.MaxHealth, Armor = P.Armor };
        foreach (var t in lv.Things)
        {
            float h = (MathF.Sin(t.X * 1.7f + t.Y * 3.1f) + 1) * 0.5f;
            if (t is Pickup && t.Z < 0.01f) t.Z = 0.5f + h * 1.8f;
            if (t is Monster { Def.FlyZ: > 0 } m && m.Z < 0.5f) m.Z = 0.6f + h * 2.0f;
        }
    }

    /// <summary>
    /// Piloting: the ship cruises east on its own. Forward/back speed it up or slow it down, strafe (and a little yaw
    /// from the mouse or turn keys) slides it across the lane, Jump climbs and Slide dives, Fire shoots twin lasers.
    /// </summary>
    void UpdateFlight(Input inp, float dt)
    {
        var p = P;
        p.Flying = true;
        p.Angle = Math.Clamp(p.Angle + inp.LookX * 0.0025f * Vars.Sens + inp.Turn * 1.4f * dt, -0.45f, 0.45f);
        p.Pitch = Math.Clamp(p.Pitch - inp.LookY * 0.35f * Vars.Sens * (Vars.InvertMouse ? -1 : 1), -70f, 70f);
        float target = inp.Move > 0.1f ? FlightFast : inp.Move < -0.1f ? FlightSlow : FlightCruise;
        p.ShipSpeed += (target - p.ShipSpeed) * MathF.Min(1, dt * 2.5f);
        float side = inp.Strafe * 4.5f + MathF.Sin(p.Angle) * p.ShipSpeed;
        float dx = p.ShipSpeed * dt, dy = side * dt;
        float climb = inp.JumpHeld ? 2.6f : inp.SlideHeld ? -2.6f : 0f;
        p.VZ += (climb - p.VZ) * MathF.Min(1, dt * 6);
        p.Z = Math.Clamp(p.Z + p.VZ * dt, 0.1f, FlightTop);
        if (!Level.BlocksCircle(p.X + dx, p.Y, p.Radius)) p.X += dx;
        if (!Level.BlocksCircle(p.X, p.Y + dy, p.Radius)) p.Y += dy;
        else if (MathF.Abs(side) > 1f) { p.DamageFlash = MathF.Max(p.DamageFlash, 0.2f); }   // scraping the edge of the lane
        p.FloorZ = 0;
        p.JetSfx -= dt;
        if (p.JetSfx <= 0) { PlaySound(Sfx.Jet, 0.2f + 0.05f * p.ShipSpeed); p.JetSfx = 0.12f; }

        // collisions: rocks and flyers in 3D, around the middle of the ship
        float sz = p.Z + ShipHalfHeight;
        foreach (var t in Level.Things.ToList())
        {
            if (t.Removed) continue;
            if (t is Asteroid a && Dist(a.X, a.Y, p.X, p.Y) < a.Radius + p.Radius && MathF.Abs(a.MidZ - sz) < a.SpriteH * 0.45f + ShipHalfHeight)
            {
                Shatter(a);
                DamagePlayer(18);
                p.ShipSpeed = FlightSlow;
                Say(Words.T("Hull breach! Watch the rocks."));
            }
            else if (t is Monster { Alive: true } m && Dist(m.X, m.Y, p.X, p.Y) < m.Radius + p.Radius
                     && MathF.Abs(m.Z + m.SpriteH * 0.5f - sz) < m.SpriteH * 0.5f + ShipHalfHeight)
            {
                DamageMonster(m, 80);
                DamagePlayer(12);
                p.ShipSpeed = FlightSlow;
            }
            else if (t is Pickup pk && Dist(pk.X, pk.Y, p.X, p.Y) < 0.6f && MathF.Abs(pk.Z + pk.SpriteH * 0.5f - sz) < 0.7f)
                TryPickup(pk);
        }
        if (Mode != GameMode.Playing) return;

        // the far end of the lane
        char mark = Level.MarkAt(p.X, p.Y);
        if (char.IsDigit(mark)) { if (!p.PortalLock) { Teleport(mark); return; } }
        else p.PortalLock = false;

        p.Cooldown -= dt;
        p.FireAnim = MathF.Max(0, p.FireAnim - dt);
        if (inp.Fire && !Relaxed && p.Cooldown <= 0) FireShipGuns();
    }

    /// <summary>Twin lasers from the wingtips, straight along your view.</summary>
    void FireShipGuns()
    {
        var p = P;
        p.Cooldown = 0.22f;
        p.FireAnim = 0.12f;
        PlaySound(Sfx.Shoot, 0.7f);
        float proj = 160f / MathF.Tan(ViewFov * MathF.PI / 360f), speed = 22f + p.ShipSpeed;
        float ca = MathF.Cos(p.Angle), sa = MathF.Sin(p.Angle), z = p.Z + ShipHalfHeight;
        foreach (float wing in new[] { -0.22f, 0.22f })
            Level.Things.Add(new Projectile
            {
                Kind = ProjKind.Bolt, FromPlayer = true, DmgMin = 14, DmgMax = 22, Level = Level, Life = 1.2f,
                X = p.X + ca * 0.4f - sa * wing, Y = p.Y + sa * 0.4f + ca * wing, Z = z,
                VX = ca * speed, VY = sa * speed, VZ = speed * p.Pitch / proj, Aimed = true,
            });
    }

    /// <summary>An asteroid bursts into drifting rubble.</summary>
    void Shatter(Asteroid a)
    {
        a.Removed = true;
        Sound(Sfx.Break, a.X, a.Y);
        SpawnPuff(Art.Fireball[1], a.X, a.Y, a.MidZ, a.SpriteW * 0.8f);
        for (int k = 0; k < 6; k++)
            Level.Things.Add(new Puff(Art.RubbleChunk, 0.1f + RandF() * 0.12f, 0.5f + RandF() * 0.4f, 0f)
            {
                X = a.X + (RandF() - 0.5f) * a.SpriteW, Y = a.Y + (RandF() - 0.5f) * a.SpriteW, Z = a.MidZ + (RandF() - 0.5f) * a.SpriteH,
                Level = Level, FullBright = false, VZ = (RandF() - 0.5f) * 1.5f, Gravity = 0.3f,
            });
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
                case SoccerBall ball: BallTick(ball, dt); break;
                case Puff pf: pf.Tick(dt); break;
                case Asteroid a: a.Z = MathF.Max(0.05f, a.BaseZ + MathF.Sin(PlayTime * a.Bob + a.Phase) * 0.5f); break;
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
    bool Blocked(float x, float y, float r, Thing self, bool phaseMonsters = false)
    {
        if (Level.BlocksCircle(x, y, r)) return true;
        // steps: you can walk up MaxStep; jumping (or flying) lifts you higher
        {
            float from = self == null ? P.FloorZ : Level.FloorAt(self.X, self.Y);
            float lift = self == null ? P.Z : self is Monster fm ? fm.Z : 0f;
            if (Level.TooHigh(x, y, r, from, Level.MaxStep + lift)) return true;
        }
        foreach (var t in Level.Things)
        {
            if (t == self || !t.Solid || t.Removed) continue;
            if (t is Monster m && !m.Alive) continue;
            if (phaseMonsters && t is Monster) continue;
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
        m.SlowTime = MathF.Max(0, m.SlowTime - dt);
        if (m.Alive) KnockTick(m, dt);
        if (m.Target != null) { DummyTick(m, dt); return; }
        if (m.FrozenTime > 0 && m.Alive) { m.FrozenTime -= dt; return; } // frozen solid (Deep Freeze): not a twitch
        float dist = Dist(m.X, m.Y, P.X, P.Y);
        bool playerAlive = Mode != GameMode.Dead;

        if (Relaxed && m.Alive) { Wander(m, dt, dist); return; }

        // Dark Bishop blur: dart sideways, see-through and untouchable
        if (m.BlurTime > 0)
        {
            m.BlurTime -= dt;
            float s = 6f * m.SpeedMult * Vars.MonsterSpeed * dt;
            float nx = m.X + m.BlurDX * s, ny = m.Y + m.BlurDY * s;
            if (!Blocked(nx, ny, m.Radius, m)) { m.X = nx; m.Y = ny; }
            else { m.BlurDX = -m.BlurDX; m.BlurDY = -m.BlurDY; }
            if (m.State == AiState.Chase) return;
        }

        if (m.Def.Special != Special.None && m.Alive && MiniBossTick(m, dt, dist)) return;

        switch (m.State)
        {
            case AiState.Idle:
                if (playerAlive && !Vars.NoTarget && dist < m.Def.SightRange && Level.Sight(m.X, m.Y, P.X, P.Y)) Wake(m);
                break;

            case AiState.Chase:
                {
                    m.Anim += dt;
                    m.AttackCd -= dt;
                    // after you, or after another monster that hit it
                    var tg = TargetOf(m);
                    float tdist = Dist(m.X, m.Y, tg.x, tg.y);
                    if (tg.foe == null && (!playerAlive || Vars.NoTarget)) { ChaseMove(m, dt, wander: true); break; }
                    if (m.Def.Blurs && m.AttackCd > 0.3f && tdist < 12f && RandF() < dt * 0.35f) { StartBlur(m); break; }
                    bool canMelee = m.Def.MeleeRange > 0 && tdist <= m.Def.MeleeRange + tg.radius;
                    if (canMelee && m.AttackCd <= 0) { SetState(m, AiState.Attack); break; }
                    if (m.Def.Missile != null && m.AttackCd <= 0 && tdist < 18f && RandF() < dt * 2.5f && Level.Sight(m.X, m.Y, tg.x, tg.y))
                    {
                        SetState(m, AiState.Attack);
                        break;
                    }
                    if (!canMelee) ChaseMove(m, dt, wander: false, tg.x, tg.y);
                    break;
                }

            case AiState.Attack:
                {
                    if (!m.AttackFired && m.StateTime >= m.Def.AttackTime * 0.5f)
                    {
                        m.AttackFired = true;
                        var tg = TargetOf(m);
                        float tdist = Dist(m.X, m.Y, tg.x, tg.y);
                        if (tg.foe != null && m.Def.MeleeRange > 0 && tdist <= m.Def.MeleeRange + tg.radius + 0.25f) MonsterMelee(m, tg.foe);
                        else if (tg.foe == null && m.Def.MeleeRange > 0 && dist <= m.Def.MeleeRange + P.Radius + 0.25f)
                        {
                            Sound(Sfx.Swing, m.X, m.Y);
                            if (playerAlive && P.Z < 0.3f && MathF.Abs(Level.FloorAt(m.X, m.Y) - P.FloorZ) < 0.8f)
                            {
                                EliteHit(m, DamagePlayer((int)(Rand(m.Def.MeleeMin, m.Def.MeleeMax) * m.DamageMult)));
                                if (m.Def.Shove > 0) ShovePlayer(m);
                            }
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

    void ChaseMove(Monster m, float dt, bool wander) => ChaseMove(m, dt, wander, P.X, P.Y);

    void ChaseMove(Monster m, float dt, bool wander, float goalX, float goalY)
    {
        float step = m.Def.Speed * m.SpeedMult * Vars.MonsterSpeed * dt * (m.SlowTime > 0 ? m.SlowFactor : 1);
        float dx, dy;
        if (m.StuckTime > 0)
        {
            m.StuckTime -= dt;
            dx = m.StuckDX; dy = m.StuckDY;
        }
        else
        {
            float tx = goalX - m.X, ty = goalY - m.Y;
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
        var tg = TargetOf(m);
        if (kind == ProjKind.Grenade) { FireGrenadeAt(m, tg.x, tg.y, tg.z); return; }
        float baseA = MathF.Atan2(tg.y - m.Y, tg.x - m.X);
        (int lo, int hi, float speed) = kind switch
        {
            ProjKind.Fireball => (6, 12, 6.5f),
            ProjKind.CentaurBolt => (8, 14, 7.5f),
            ProjKind.Seeker => (6, 11, 4.8f),
            _ => (10, 18, 6.0f),
        };
        // aim up or down at you when you're well above or below (on a ledge, or flying)
        float launchZ = Level.FloorAt(m.X, m.Y) + m.Z + m.SpriteH * 0.45f;
        float chest = tg.z + (tg.foe?.SpriteH ?? Player.Height) * 0.55f, dist = MathF.Max(0.5f, Dist(m.X, m.Y, tg.x, tg.y));
        bool aimed = kind != ProjKind.Seeker && MathF.Abs(chest - launchZ) > 0.6f;
        float vz = aimed ? (chest - launchZ) * speed / dist : 0f;
        for (int i = 0; i < m.Def.MissileCount; i++)
        {
            float a = baseA + (i - (m.Def.MissileCount - 1) / 2f) * m.Def.MissileSpread + (RandF() - 0.5f) * 0.06f;
            Level.Things.Add(new Projectile
            {
                Kind = kind, FromPlayer = false, DmgMin = (int)(lo * m.DamageMult), DmgMax = (int)(hi * m.DamageMult), Owner = m, Level = Level,
                X = m.X + MathF.Cos(a) * (m.Radius + 0.1f), Y = m.Y + MathF.Sin(a) * (m.Radius + 0.1f),
                Z = launchZ, VX = MathF.Cos(a) * speed, VY = MathF.Sin(a) * speed, VZ = vz, Aimed = aimed,
                Homing = kind == ProjKind.Seeker ? 1.9f : 0f, Life = kind == ProjKind.Seeker ? 4.5f : 6f,
            });
        }
        Sound(Sfx.Shoot, m.X, m.Y);
    }

    void UpdateProjectile(Projectile pr, float dt)
    {
        pr.Life -= dt;
        if (pr.Kind == ProjKind.Grenade) { UpdateGrenade(pr, dt); return; }
        if (pr.Life <= 0) { pr.Removed = true; return; }
        RocketTrail(pr, dt);
        // aim player shots gently toward eye-level as they fly
        if (pr.Aimed) pr.Z += pr.VZ * dt;
        else if (pr.FromPlayer) pr.Z += (Level.FloorAt(pr.X, pr.Y) + 0.4f - pr.Z) * MathF.Min(1, dt * 2);
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
            float tz = P.FloorZ + 0.22f * (1f - 0.4f * P.SlideLow); // skims low and tracks your stance, not jumps: hop over them
            pr.Z += Math.Clamp(tz - pr.Z, -0.6f * dt, 0.6f * dt);
        }
        float sp = MathF.Sqrt(pr.VX * pr.VX + pr.VY * pr.VY);
        int steps = Math.Max(1, (int)(sp * dt / 0.1f) + 1);
        float sx = pr.VX * dt / steps, sy = pr.VY * dt / steps;
        for (int s = 0; s < steps; s++)
        {
            pr.X += sx; pr.Y += sy;
            // walls, the face of a ledge, or a low ceiling
            if (Level.BlocksPoint(pr.X, pr.Y) || pr.Z < Level.FloorAt(pr.X, pr.Y) - 0.02f || pr.Z > Level.HeightAt(pr.X, pr.Y))
            {
                int hx = (int)MathF.Floor(pr.X), hy = (int)MathF.Floor(pr.Y);
                pr.HitWall = Level.BlocksPoint(pr.X, pr.Y) || (pr.Z < Level.FloorAt(pr.X, pr.Y) - 0.02f && pr.Z > Level.FloorAt(pr.X - sx, pr.Y - sy) + 0.05f);
                var face = Level.Cell(hx, hy) == Level.Rubble ? Level.Face.Wall : pr.Z < Level.FloorAt(pr.X, pr.Y) ? Level.Face.Floor : Level.Face.Ceiling;
                pr.X -= sx; pr.Y -= sy;
                (int, Level.Face)? direct = null;
                if (pr.FromPlayer && Level.CanDig(hx, hy, face)) { direct = (hy * Level.W + hx, face); HitBlock(hx, hy, Rand(pr.DmgMin, pr.DmgMax), face, slot: SlotAt(pr.Z)); }
                Explode(pr, null, direct);
                return;
            }
            if (pr.FromPlayer)
            {
                if (HitsBall(pr.X, pr.Y, pr.Z, pr.Radius)) { Explode(pr, null); return; } // Rocket Soccer: the ball takes it
                foreach (var t in Level.Things)
                    if (t is Asteroid a && !a.Removed && Dist(a.X, a.Y, pr.X, pr.Y) < a.Radius + pr.Radius && MathF.Abs(a.MidZ - pr.Z) < a.SpriteH * 0.5f)
                    {
                        a.Health -= Rand(pr.DmgMin, pr.DmgMax);
                        if (a.Health <= 0) Shatter(a); else Sound(Sfx.Hit, a.X, a.Y);
                        Explode(pr, null);
                        return;
                    }
                foreach (var t in Level.Things.ToList())
                    if (t is Monster m && m.Alive && !m.Blurring && Dist(m.X, m.Y, pr.X, pr.Y) < m.Radius + pr.Radius && !(pr.Pierced?.Contains(m) ?? false))
                    {
                        _hitPower = pr.Power;
                        DamageMonster(m, Rand(pr.DmgMin, pr.DmgMax), pr.Slot);
                        _hitPower = 1;
                        if (pr.Pierce > 0)
                        {
                            // a piercing shot goes on through, into the next
                            pr.Pierce--;
                            (pr.Pierced ??= new()).Add(m);
                            SpawnPuff(pr.Frames[1], pr.X, pr.Y, pr.Z, 0.3f);
                            Sound(Sfx.Hit, pr.X, pr.Y);
                            continue;
                        }
                        Explode(pr, m);
                        return;
                    }
            }
            else
            {
                // a monster's missile: another kind of monster in the way takes it, and turns on the one that threw it
                foreach (var t in Level.Things)
                    if (t is Monster other && other != pr.Owner && other.Alive && !other.Blurring && other.Target == null
                        && (pr.Owner as Monster)?.Def != other.Def && Dist(other.X, other.Y, pr.X, pr.Y) < other.Radius + pr.Radius)
                    {
                        float foot = Level.FloorAt(other.X, other.Y) + other.Z;
                        if (pr.Z < foot - 0.05f || pr.Z > foot + other.SpriteH) continue;
                        DamageMonster(other, Rand(pr.DmgMin, pr.DmgMax));
                        Provoke(other, pr.Owner as Monster);
                        Explode(pr, other);
                        return;
                    }
                if (Mode != GameMode.Dead && Dist(P.X, P.Y, pr.X, pr.Y) < P.Radius + pr.Radius && HitsPlayerHeight(pr.Z))
                {
                    EliteHit(pr.Owner as Monster, DamagePlayer(Rand(pr.DmgMin, pr.DmgMax)));
                    Explode(pr, null);
                    return;
                }
            }
        }
    }

    /// <summary>Is height z within the player's body? Jumping lifts it, sliding shrinks it.</summary>
    bool HitsPlayerHeight(float z)
    {
        float feet = P.FloorZ + P.Z, top = feet + Player.Height * (1f - 0.5f * P.SlideLow);
        return z >= feet - 0.05f && z <= top;
    }

    void Explode(Projectile pr, Monster direct, (int cell, Level.Face face)? directBlock = null)
    {
        pr.Removed = true;
        SpawnPuff(pr.Frames[1], pr.X, pr.Y, pr.Z, pr.Splash > 0 ? 0.7f : 0.35f);
        Sound(pr.Splash > 0 ? Sfx.Explode : Sfx.Hit, pr.X, pr.Y);
        if (pr.Splash <= 0) return;
        float splash = pr.Splash;
        if (pr.Kind is ProjKind.Rocket or ProjKind.Grenade) { RocketBlast(pr, direct); splash = 1.2f; } // Quake's blast; it only chips the rubble close by
        else
            foreach (var t in Level.Things.ToList())
                if (t is Monster m && m != direct && m.Alive)
                {
                    float d = Dist(m.X, m.Y, pr.X, pr.Y);
                    if (d < pr.Splash) DamageMonster(m, (int)(pr.DmgMax * 0.6f * (1 - d / pr.Splash)), pr.FromPlayer ? pr.Slot : -1);
                }
        if (!pr.FromPlayer) return;
        // a blast chips the rubble around it, and on a dig map the rock above and below too
        // (measured to the nearest point of each block)
        int r = (int)MathF.Ceiling(splash);
        for (int cy = (int)pr.Y - r; cy <= (int)pr.Y + r; cy++)
            for (int cx = (int)pr.X - r; cx <= (int)pr.X + r; cx++)
            {
                if (!Level.InBounds(cx, cy)) continue;
                int i = cy * Level.W + cx;
                float flat = Dist(Math.Clamp(pr.X, cx, cx + 1), Math.Clamp(pr.Y, cy, cy + 1), pr.X, pr.Y);
                foreach (var face in new[] { Level.Face.Wall, Level.Face.Floor, Level.Face.Ceiling })
                {
                    if (!Level.CanDig(cx, cy, face) || directBlock == (i, face)) continue;
                    float up = face == Level.Face.Floor ? MathF.Max(0, pr.Z - Level.Floors[i]) : face == Level.Face.Ceiling ? MathF.Max(0, Level.Heights[i] - pr.Z) : 0;
                    float d = MathF.Sqrt(flat * flat + up * up);
                    if (d < splash) HitBlock(cx, cy, (int)(pr.DmgMax * 0.6f * (1 - d / splash)), face, quiet: true, slot: SlotAt(pr.Z));
                }
            }
    }

    /// <summary>The block your next swing (or Use) would hit, highlighted in the view; null if none.
    /// For rubble on a dig map, Slot is the floor of the one-storey opening it would leave.</summary>
    public (int x, int y, Level.Face face, float slot)? DigTarget;

    /// <summary>On a dig map, rubble opens as a one-storey slot around where you hit it (aim high for a step up).</summary>
    static float SlotAt(float z) => Math.Clamp(MathF.Floor((z - 0.2f) / Level.DigStep) * Level.DigStep, 0f, Level.MaxHeight - Level.MinHeight);

    /// <summary>
    /// Where a block you place would go: into the open cell in front of the wall you're looking at (or at the end of
    /// your reach), or, on a dig map, onto the floor or under the ceiling you're looking at. Null if nowhere fits.
    /// </summary>
    public (int x, int y, Level.Face face)? PlaceTarget()
    {
        var p = P;
        float reach = Level.Dig ? 2f : 1.6f;
        float pitch = Level.Dig ? Math.Clamp(p.Pitch / 70f, -1f, 1f) * 85f * MathF.PI / 180f : 0f;
        float flat = MathF.Cos(pitch), dz = MathF.Sin(pitch);
        float dx = MathF.Cos(p.Angle) * flat, dy = MathF.Sin(p.Angle) * flat, eye = p.FloorZ + p.ViewZ;
        int px = -1, py = -1, fx = -1, fy = -1;
        for (float d = 0.05f; d < reach; d += 0.04f)
        {
            int cx = (int)MathF.Floor(p.X + dx * d), cy = (int)MathF.Floor(p.Y + dy * d);
            if (Level.Blocks(cx, cy)) return px >= 0 ? Placeable(px, py, Level.Face.Wall) : null;
            if (Level.Dig)
            {
                int i = cy * Level.W + cx;
                float z = eye + dz * d;
                if (z < Level.Floors[i]) return Placeable(cx, cy, Level.Face.Floor);
                if (z > Level.Heights[i]) return Placeable(cx, cy, Level.Face.Ceiling);
            }
            if (!PlayerTouchesCell(cx, cy)) { px = cx; py = cy; if (fx < 0) { fx = cx; fy = cy; } }
        }
        // nothing to build against: the cell just ahead of you
        return fx >= 0 ? Placeable(fx, fy, Level.Face.Wall) : null;
    }

    (int x, int y, Level.Face face)? Placeable(int x, int y, Level.Face f)
    {
        if (!Level.CanPlace(x, y, f)) return null;
        var p = P;
        if (PlayerTouchesCell(x, y))
        {
            int i = y * Level.W + x;
            // building under yourself lifts you up a step; building over yourself mustn't squash you
            if (f == Level.Face.Wall) return null;
            if (f == Level.Face.Floor && Level.Floors[i] + Level.DigStep > p.FloorZ + p.Z + Level.MaxStep + 0.001f) return null;
            if (f == Level.Face.Ceiling && Level.Heights[i] - Level.DigStep < p.FloorZ + p.Z + Player.Height + 0.05f) return null;
        }
        return (x, y, f);
    }

    /// <summary>Places one of your carried rubble blocks where you're aiming.</summary>
    void PlaceBlock()
    {
        var p = P;
        if (Level.Flight) return;
        if (p.Blocks <= 0) { Say("You have no blocks to place. Break some rubble first."); PlaySound(Sfx.Locked, 0.5f); return; }
        if (PlaceTarget() is not (var x, var y, var face) || !Level.PlaceBlock(x, y, face)) { PlaySound(Sfx.Locked, 0.4f); return; }
        p.Blocks--;
        PlaySound(Sfx.Land, 1);
        SpawnPuff(Art.RubbleChunk, x + 0.5f, y + 0.5f, Level.Floors[y * Level.W + x] + 0.3f, 0.25f);
    }

    /// <summary>
    /// The breakable block you're looking at within reach, or null. On a dig map this is aimed in 3D, and looking
    /// all the way down (or up) aims straight down (or up), so you can dig out the rock under your feet.
    /// </summary>
    (int x, int y, Level.Face face, float slot)? MineTarget(float reach)
    {
        var p = P;
        if (Level.Dig) reach = MathF.Max(reach, 2f); // a miner's reach, so a raised ceiling stays in range
        float pitch = Level.Dig ? Math.Clamp(p.Pitch / 70f, -1f, 1f) * 85f * MathF.PI / 180f : 0f;
        float flat = MathF.Cos(pitch), dz = MathF.Sin(pitch);
        float dx = MathF.Cos(p.Angle) * flat, dy = MathF.Sin(p.Angle) * flat, eye = p.FloorZ + p.ViewZ;
        for (float d = 0.05f; d < reach; d += 0.04f)
        {
            int cx = (int)MathF.Floor(p.X + dx * d), cy = (int)MathF.Floor(p.Y + dy * d);
            float z = eye + dz * d;
            // never more than a step above you, so the opening is always one you can walk up into
            if (Level.Blocks(cx, cy)) return Level.CanDig(cx, cy, Level.Face.Wall) ? (cx, cy, Level.Face.Wall, MathF.Min(SlotAt(z), p.FloorZ + Level.DigStep)) : null;
            if (!Level.Dig) continue;
            int i = cy * Level.W + cx;
            if (z < Level.Floors[i]) return Level.CanDig(cx, cy, Level.Face.Floor) ? (cx, cy, Level.Face.Floor, 0f) : null;
            if (z > Level.Heights[i]) return Level.CanDig(cx, cy, Level.Face.Ceiling) ? (cx, cy, Level.Face.Ceiling, 0f) : null;
        }
        return null;
    }

    /// <summary>Chips a block; when it gives way it bursts into debris. Rubble dug on a dig map opens with its floor
    /// at `slot` (default: your own level).</summary>
    public void HitBlock(int cx, int cy, int dmg, Level.Face face = Level.Face.Wall, bool quiet = false, float? slot = null)
    {
        if (!Level.CanDig(cx, cy, face)) return;
        int i = cy * Level.W + cx;
        float x = cx + 0.5f, y = cy + 0.5f;
        int before = Level.CrackStage(i, face);
        char was = Level.Cells[i];
        if (!Level.DamageBlock(cx, cy, (int)MathF.Round(dmg * Vars.Damage), face, slot ?? P.FloorZ))
        {
            if (!quiet || Level.CrackStage(i, face) != before) Sound(Sfx.Hit, x, y);
            return;
        }
        Sound(Sfx.Break, x, y);
        // the rock you break loose is yours to build with (ore goes to the ship instead)
        if ((face != Level.Face.Wall || was == Level.Rubble) && P.Blocks < Player.BlockStack) P.Blocks++;
        if (face == Level.Face.Wall && Level.OreIndex(was) is var ore and >= 0)
        {
            P.Ore[ore]++;
            P.PickupFlash = 1;
            PlaySound(Sfx.Pickup, 1);
            if (Level.Ship is { } ship)
            {
                int have = ship.Delivered[ore] + P.Ore[ore];
                Say($"+1 {Words.T(OreNames[ore])} ({Math.Min(have, Ship.Need[ore])}/{Ship.Need[ore]})" + (have == Ship.Need[ore] ? Words.T(" - that's enough!") : ""));
            }
            else Say($"+1 {Words.T(OreNames[ore])} ({P.Ore[ore]} carried)");
        }
        float z = face == Level.Face.Ceiling ? Level.Heights[i] - Level.DigStep - 0.2f : Level.Floors[i];
        for (int k = 0; k < 5; k++)
            Level.Things.Add(new Puff(Art.RubbleChunk, RandF() * 0.08f + 0.1f, 0.35f + RandF() * 0.25f, 0f)
            {
                X = x + (RandF() - 0.5f) * 0.7f, Y = y + (RandF() - 0.5f) * 0.7f, Z = z + 0.2f + RandF() * 0.6f, Level = Level,
                FullBright = false, VZ = RandF() * 1.5f, Gravity = 9f,
            });
    }

    void SpawnPuff(Tex tex, float x, float y, float z, float size)
    {
        Level.Things.Add(new Puff(tex, size, 0.3f, 1.2f) { X = x, Y = y, Z = z - size * 0.5f, Level = Level });
    }

    /// <summary>Hurts a monster. `slot` is the weapon you hit it with (-1 when it wasn't you), for experience.</summary>
    internal void DamageMonster(Monster m, int dmg, int slot = -1)
    {
        if (!m.Alive || dmg <= 0 || m.Blurring) return;
        if (slot >= 0) dmg = ModDamage(m, dmg, slot);
        if (m.Shield > 0)
        {
            dmg = EliteShield(m, dmg); // a shielded elite's shield soaks it up first
            if (dmg <= 0) { if (m.State == AiState.Idle) Wake(m); Sound(Sfx.Hit, m.X, m.Y); return; }
        }
        if (m.Def.Special == Special.Charger && m.SpecialPhase == 3) dmg *= 2; // a stunned Stalker is wide open
        if (InstagibOn && slot >= 0) { m.Shield = 0; dmg = (int)MathF.Ceiling((m.Health + 1) / MathF.Max(0.01f, Vars.Damage)); } // instagib: one slug, one kill
        int dealt = Math.Min(Math.Max(1, (int)MathF.Round(dmg * Vars.Damage)), Math.Max(1, m.Health));
        m.Health -= Math.Max(1, (int)MathF.Round(dmg * Vars.Damage));
        if (slot >= 0)
            Arcade.Hit(m.X, m.Y, Level.FloorAt(m.X, m.Y) + m.Z + m.SpriteH, dealt, slot, m.Health <= 0, m.Def.Health, m.Def.Boss, !P.OnGround);
        if (slot >= 0) FeelHit(m, dealt, m.Health <= 0);
        if (slot >= 0) ModHit(m, dealt, slot);
        if (m.Target != null)
        {
            // a range dummy: it falls (and stands back up), and scores in a drill
            if (m.Health <= 0) { SetState(m, AiState.Dying); Sound(Sfx.Death, m.X, m.Y); }
            else Sound(Sfx.Hit, m.X, m.Y);
            DummyHit(m, m.Health <= 0);
            return;
        }
        if (m.State == AiState.Idle) Wake(m);
        if (m.Health <= 0)
        {
            SetState(m, AiState.Dying);
            m.FrozenTime = 0;
            Sound(Sfx.Death, m.X, m.Y);
            P.Kills++;
            if (slot >= 0) CodexKill(m);
            if (slot >= 0) KilledWith(m, slot);
            if (m.Def.MiniBoss != null) MiniBossDown(m);
            EliteDied(m);
            QuakeDrop(m);
            int blood = PerkRank(Perk.Bloodthirst);
            if (blood > 0 && slot >= 0) P.Health = Math.Min(P.MaxHealth, P.Health + 4 * blood);
            if (slot >= 0) ChainLightning(m, dmg, slot);
            if (m.Def.Boss)
            {
                Level.BossDead = true;
                Say(Level.FindMark('E') != null ? "The Heresiarch is vanquished! The exit portal awakens." : "A Heresiarch falls!");
                PlaySound(Sfx.BossSight, 1);
            }
            return;
        }
        if (slot >= 0) ChainLightning(m, dmg, slot);
        EliteHurt(m);
        if (m.Def.Blurs && m.State != AiState.Attack && RandF() < 0.4f) { StartBlur(m); return; }
        if (RandF() < m.Def.PainChance && m.State != AiState.Attack)
        {
            SetState(m, AiState.Pain);
            Sound(Sfx.Pain, m.X, m.Y);
        }
    }

    bool _chaining;

    /// <summary>The Chain Lightning perk: your hit arcs on to the nearest other monster close by, for 40% of it (per rank).</summary>
    void ChainLightning(Monster from, int dmg, int slot)
    {
        int rank = PerkRank(Perk.ChainLightning);
        if (rank == 0 || _chaining) return;
        Monster next = null;
        float best = 4f;
        foreach (var t in Level.Things)
            if (t is Monster o && o != from && o.Alive && !o.Blurring && Dist(o.X, o.Y, from.X, from.Y) is var d && d < best && Level.Sight(from.X, from.Y, o.X, o.Y))
                (next, best) = (o, d);
        if (next == null) return;
        _chaining = true;
        SpawnPuff(Art.Bolt[1], next.X, next.Y, Level.FloorAt(next.X, next.Y) + next.Z + next.SpriteH * 0.5f, 0.35f);
        DamageMonster(next, Math.Max(1, (int)(dmg * 0.4f * rank)), slot);
        _chaining = false;
    }

    /// <summary>Hurts you; returns how much health it took.</summary>
    internal int DamagePlayer(int dmg)
    {
        var p = P;
        if (Mode != GameMode.Playing || Vars.God || Relaxed) return 0;
        dmg = Math.Max(0, (int)MathF.Round(dmg * Vars.MonsterDamage));
        if (dmg == 0) return 0;
        int saved = Math.Min(p.Armor, (int)(dmg * p.Def.ArmorSave));
        p.Armor -= saved;
        p.Health -= dmg - saved;
        p.DamageFlash = MathF.Min(1, p.DamageFlash + 0.4f + dmg / 40f);
        FeelHurt(dmg);
        Arcade.Hurt();
        if (p.Health <= 0)
        {
            p.Health = 0;
            p.Dead = true;
            RunDeaths++;
            p.Z = 0; p.VZ = 0; p.VX = p.VY = 0; p.SlideTime = 0; p.SlideLow = 0;
            Mode = GameMode.Dead;
            SaveProfile();
            PlaySound(Sfx.PlayerDeath, 1);
            if (ArenaMode) { EndArenaRun(); Say("Press Enter to try again."); return dmg - saved; }
            Say(CanRespawn ? "You have died. Press Enter to return to the checkpoint." : "You have died. Press Enter to try again.");
        }
        else PlaySound(Sfx.PlayerPain, 1);
        return dmg - saved;
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
            else { var (cx, cy) = lv.ArrivalCell(); (x, y) = (cx + 0.5f, cy + 0.5f); } // e.g. the Windspire's portal 4
        }
        foreach (var t in Level.Things) if (t is Projectile or Puff) t.Removed = true;
        Level = lv;
        P.X = x; P.Y = y;
        P.FloorZ = lv.FloorUnder(x, y, P.Radius); P.Z = 0; P.VZ = 0; P.VX = P.VY = 0;
        P.PortalLock = true;
        P.TeleportFlash = 1;
        PlaySound(Sfx.Teleport, 1);
        Say(lv.EntryMessage);
        if (lv.Flight) EnterFlight();
    }

    /// <summary>Spawns an already-awake monster with a teleport flash (used by arena waves).</summary>
    public Monster SpawnMonster(MonsterDef def, float x, float y, float healthMult, float damageMult, float speedMult)
    {
        var m = new Monster(def) { X = x, Y = y, Level = Level, DamageMult = damageMult, SpeedMult = speedMult };
        m.Health = m.MaxHealth = (int)(def.Health * healthMult);
        Level.Things.Add(m);
        SpawnPuff(Art.BossBall[1], x, y, Level.FloorAt(x, y) + 0.5f, 0.8f);
        Sound(Sfx.Teleport, x, y);
        SetState(m, AiState.Chase);
        m.AttackCd = 1f + RandF();
        return m;
    }

    /// <summary>Spawns a thing (by map glyph) a short distance in front of the player.</summary>
    public bool Summon(char glyph) => SummonThing(ThingFactory.Create(glyph, 0, 0));

    /// <summary>Puts a monster (a mini-boss, say) in front of you, awake.</summary>
    public bool SummonMonster(MonsterDef def) => SummonThing(new Monster(def) { NextBlinkHp = (int)(def.Health * 0.8f) });

    bool SummonThing(Thing t)
    {
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
