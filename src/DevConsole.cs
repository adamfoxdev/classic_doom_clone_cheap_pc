using System.Globalization;

namespace HexenSharp;

/// <summary>
/// How much the HUD shows. Full is the classic status bar; the others drop it for a full-height view with
/// a small overlay (Compact: health, armor, ammo, keys and items; Minimal: just health and ammo) or none (Off).
/// </summary>
public enum HudStyle { Full, Compact, Minimal, Off }

/// <summary>The aiming mark drawn at the centre of the view (off by default).</summary>
public enum CrosshairStyle { Off, Dot, Cross, Circle }

/// <summary>Tweakable game mechanics, changed from the console with "set name value".</summary>
public sealed class GameVars
{
    public float Speed = 1f, Sens = 1f, Damage = 1f, MonsterDamage = 1f, MonsterSpeed = 1f;
    public float FireRate = 1f, ManaCost = 1f, Fog = 1f, Fov = 74f;
    public float Gravity = 12f, JumpPower = 3.3f, SlideSpeed = 4.5f, Chests = 2f;
    /// <summary>Quake movement (momentum, air strafing, bunny hopping) and its tuning.</summary>
    public bool QuakeMove = true;
    public float Accel = 10f, AirAccel = 10f, Friction = 6f, MaxHop = 3f;
    /// <summary>Race a ghost of your best run on the practice course.</summary>
    public bool Ghost = true;
    /// <summary>The on-screen strafe helper: 0 off, 1 on the practice course, 2 everywhere.</summary>
    public int StrafeHelp = 1;
    public bool God, NoClip, NoTarget, Freeze, InfiniteMana, InfiniteFuel, FullBright, ShowFps, InvertMouse;
    public HudStyle Hud;
    public CrosshairStyle Crosshair;

    public sealed record Var(string Name, string Help, Func<GameVars, float> Get, Action<GameVars, float> Set, bool IsBool = false);

    public static readonly Var[] All =
    {
        new("speed", "player move speed multiplier", v => v.Speed, (v, x) => v.Speed = Math.Clamp(x, 0.1f, 5f)),
        new("sens", "mouse sensitivity multiplier", v => v.Sens, (v, x) => v.Sens = Math.Clamp(x, 0.05f, 10f)),
        new("invertmouse", "invert mouse up/down", v => B(v.InvertMouse), (v, x) => v.InvertMouse = x != 0, true),
        new("damage", "damage you deal (multiplier)", v => v.Damage, (v, x) => v.Damage = Math.Clamp(x, 0f, 100f)),
        new("monsterdamage", "damage monsters deal (multiplier)", v => v.MonsterDamage, (v, x) => v.MonsterDamage = Math.Clamp(x, 0f, 100f)),
        new("monsterspeed", "monster move speed multiplier", v => v.MonsterSpeed, (v, x) => v.MonsterSpeed = Math.Clamp(x, 0f, 5f)),
        new("firerate", "attack speed multiplier", v => v.FireRate, (v, x) => v.FireRate = Math.Clamp(x, 0.1f, 10f)),
        new("manacost", "mana cost multiplier", v => v.ManaCost, (v, x) => v.ManaCost = Math.Clamp(x, 0f, 10f)),
        new("fog", "fog distance multiplier", v => v.Fog, (v, x) => v.Fog = Math.Clamp(x, 0.2f, 10f)),
        new("fov", "horizontal field of view in degrees", v => v.Fov, (v, x) => v.Fov = Math.Clamp(x, 40f, 120f)),
        new("gravity", "gravity (units/s^2)", v => v.Gravity, (v, x) => v.Gravity = Math.Clamp(x, 1f, 60f)),
        new("jump", "jump launch speed", v => v.JumpPower, (v, x) => v.JumpPower = Math.Clamp(x, 0f, 10f)),
        new("slidespeed", "extra speed at the start of a slide", v => v.SlideSpeed, (v, x) => v.SlideSpeed = Math.Clamp(x, 0f, 20f)),
        new("quakemove", "Quake movement: momentum, air strafing, bunny hopping", v => B(v.QuakeMove), (v, x) => v.QuakeMove = x != 0, true),
        new("accel", "ground acceleration (Quake movement)", v => v.Accel, (v, x) => v.Accel = Math.Clamp(x, 1f, 100f)),
        new("airaccel", "air acceleration (Quake movement)", v => v.AirAccel, (v, x) => v.AirAccel = Math.Clamp(x, 0f, 100f)),
        new("friction", "ground friction (Quake movement)", v => v.Friction, (v, x) => v.Friction = Math.Clamp(x, 0f, 20f)),
        new("strafehelp", "strafe-jumping helper: 0 off, 1 on the practice course, 2 everywhere", v => v.StrafeHelp, (v, x) => v.StrafeHelp = Math.Clamp((int)MathF.Round(x), 0, 2)),
        new("ghost", "race a ghost of your best run on the practice course", v => B(v.Ghost), (v, x) => v.Ghost = x != 0, true),
        new("maxhop", "top speed from strafe jumping, times your run speed", v => v.MaxHop, (v, x) => v.MaxHop = Math.Clamp(x, 1f, 5f)),
        new("chests", "chests per 200 floor cells (next game)", v => v.Chests, (v, x) => v.Chests = Math.Clamp(x, 0f, 20f)),
        new("god", "invulnerability", v => B(v.God), (v, x) => v.God = x != 0, true),
        new("noclip", "walk through walls", v => B(v.NoClip), (v, x) => v.NoClip = x != 0, true),
        new("notarget", "monsters ignore you", v => B(v.NoTarget), (v, x) => v.NoTarget = x != 0, true),
        new("freeze", "monsters stop moving", v => B(v.Freeze), (v, x) => v.Freeze = x != 0, true),
        new("infinitemana", "weapons cost no mana", v => B(v.InfiniteMana), (v, x) => v.InfiniteMana = x != 0, true),
        new("infinitefuel", "the jetpack never runs dry", v => B(v.InfiniteFuel), (v, x) => v.InfiniteFuel = x != 0, true),
        new("fullbright", "disable lighting and fog", v => B(v.FullBright), (v, x) => v.FullBright = x != 0, true),
        new("showfps", "show frames per second", v => B(v.ShowFps), (v, x) => v.ShowFps = x != 0, true),
        new("hud", "HUD style: 0 full, 1 compact, 2 minimal, 3 off", v => (int)v.Hud, (v, x) => v.Hud = (HudStyle)Math.Clamp((int)MathF.Round(x), 0, 3)),
        new("crosshair", "crosshair: 0 off, 1 dot, 2 cross, 3 circle", v => (int)v.Crosshair, (v, x) => v.Crosshair = (CrosshairStyle)Math.Clamp((int)MathF.Round(x), 0, 3)),
    };

    static float B(bool b) => b ? 1f : 0f;
    public static Var Find(string name) => All.FirstOrDefault(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Quake-style drop-down console (toggle with ~) plus Hexen's classic cheat codes, which can also be
/// typed directly during play.
/// </summary>
public sealed class DevConsole
{
    readonly Game _g;
    public bool Open;
    public string Line = "";
    public readonly List<string> Log = new();
    public int Scroll;
    readonly List<string> _history = new();
    int _histIndex = -1;
    string _cheatBuf = "";

    sealed record Command(string Name, string Usage, string Help, Action<string[]> Run);
    readonly List<Command> _commands = new();

    // Hexen's cheat codes
    static readonly (string code, string cmd)[] Cheats =
    {
        ("satan", "god"), ("casper", "noclip"), ("nra", "give all"), ("indiana", "give items"),
        ("locksmith", "give keys"), ("icarus", "give jetpack"), ("clubmed", "give health"), ("butcher", "kill"), ("mapsco", "reveal"),
    };

    public DevConsole(Game g)
    {
        _g = g;
        Add("help", "", "list commands", _ => Help());
        Add("cheats", "", "list the classic cheat codes", _ =>
        {
            foreach (var (code, cmd) in Cheats) Print($"  {code,-10} = {cmd}");
            Print("  visitN     = warp to hub map N (e.g. visit3)");
            Print("Type them during play, or here.");
        });
        Add("vars", "", "list tweakable settings", _ =>
        {
            foreach (var v in GameVars.All) Print($"  {v.Name,-13} {Fmt(v)}  {v.Help}");
        });
        Add("set", "<var> <value>", "change a setting", a =>
        {
            if (a.Length < 3) { Print("usage: set <var> <value>   (see 'vars')"); return; }
            var v = GameVars.Find(a[1]);
            if (v == null) { Print($"unknown var '{a[1]}'"); return; }
            if (!TryNum(a[2], out float x)) { Print($"'{a[2]}' is not a number"); return; }
            v.Set(_g.Vars, x);
            Print($"{v.Name} = {Fmt(v)}");
        });
        Add("get", "<var>", "show a setting", a =>
        {
            var v = a.Length > 1 ? GameVars.Find(a[1]) : null;
            Print(v == null ? "usage: get <var>   (see 'vars')" : $"{v.Name} = {Fmt(v)}");
        });
        Add("reset", "", "restore every setting to its default", _ =>
        {
            var d = new GameVars();
            foreach (var v in GameVars.All) v.Set(_g.Vars, v.Get(d));
            Print("settings reset");
        });
        Add("god", "", "toggle invulnerability", _ => Toggle("god"));
        Add("noclip", "", "toggle walking through walls", _ => Toggle("noclip"));
        Add("notarget", "", "toggle monsters ignoring you", _ => Toggle("notarget"));
        Add("freeze", "", "toggle frozen monsters", _ => Toggle("freeze"));
        Add("give", "<all|health|mana|weapons|keys|items|armor|jetpack>", "give yourself things", a => Give(a.Length > 1 ? a[1] : "all"));
        Add("kill", "", "kill every monster on this map", _ =>
        {
            if (!InGame()) return;
            Print($"killed {_g.KillAll()} monster(s)");
        });
        Add("reveal", "", "reveal the whole automap", _ =>
        {
            if (!InGame()) return;
            Array.Fill(_g.Level.Seen, true);
            Print("map revealed");
        });
        Add("map", "<number|name>", "warp to a hub map", a => Map(a.Length > 1 ? a[1] : null));
        Add("summon", "<thing>", "spawn a monster or item in front of you", a => Summon(a.Length > 1 ? a[1] : null));
        Add("pos", "", "print your position", _ =>
        {
            if (!InGame()) return;
            Print($"{_g.Level.Name}: x={_g.P.X:0.00} y={_g.P.Y:0.00} angle={_g.P.Angle * 180 / MathF.PI % 360:0}");
        });
        Add("tp", "<x> <y>", "teleport within this map", a =>
        {
            if (!InGame()) return;
            if (a.Length < 3 || !TryNum(a[1], out float x) || !TryNum(a[2], out float y)) { Print("usage: tp <x> <y>"); return; }
            if (!_g.Level.InBounds((int)x, (int)y)) { Print("out of bounds"); return; }
            _g.P.X = x; _g.P.Y = y;
            Print($"teleported to {x:0.00},{y:0.00}");
        });
        Add("class", "<fighter|cleric|mage>", "start a new game as a class", a =>
        {
            var names = Enum.GetNames<PClass>();
            var n = a.Length > 1 ? names.FirstOrDefault(c => c.StartsWith(a[1], StringComparison.OrdinalIgnoreCase)) : null;
            if (n == null) { Print("usage: class <fighter|cleric|mage>"); return; }
            _g.NewGame(Enum.Parse<PClass>(n));
            Print($"new game as {n}");
        });
        Add("mode", "<classic|relaxed>", "start a new game in a play style", a =>
        {
            var style = a.Length > 1 ? Enum.GetNames<GameStyle>().FirstOrDefault(n => n.StartsWith(a[1], StringComparison.OrdinalIgnoreCase)) : null;
            if (style == null) { Print($"mode = {_g.Style.ToString().ToLowerInvariant()}. usage: mode <classic|relaxed>"); return; }
            _g.Style = Enum.Parse<GameStyle>(style);
            _g.NewGame(_g.P?.Class ?? PClass.Fighter);
            Print($"new {style.ToLowerInvariant()} game");
        });
        Add("xp", "<amount>", "give yourself experience", a =>
        {
            if (a.Length < 2 || !int.TryParse(a[1], out int n) || n <= 0) { Print("usage: xp <amount>"); return; }
            int before = _g.Profile.Level;
            _g.GainXp(n);
            Print($"+{n} xp: level {_g.Profile.Level}{(_g.Profile.Level > before ? " (level up!)" : "")}, {_g.Profile.Points} point(s) to spend");
        });
        Add("profile", "[reset]", "show your character's progress, or start it over", a =>
        {
            var pr = _g.Profile;
            if (a.Length > 1 && a[1] == "reset")
            {
                _g.Profile = new Profile();
                _g.ApplyProfile();
                _g.SaveProfile();
                Print("profile reset: level 1, no skills, weapons back to level 1");
                return;
            }
            Print($"level {pr.Level}, xp {pr.Xp}/{Profile.XpToNext(pr.Level)}, {pr.Points} point(s), {pr.TotalKills} kills, {pr.Wins} wins");
            Print("skills: " + string.Join(", ", Profile.Skills.Select(s => $"{s.ToString().ToLowerInvariant()} {pr.Rank(s)}")));
        });
        Add("skill", "<name>", "spend a skill point (vitality, power, agility, focus, thrusters)", a =>
        {
            var s = a.Length > 1 ? Profile.Skills.FirstOrDefault(k => k.ToString().StartsWith(a[1], StringComparison.OrdinalIgnoreCase), (Skill)(-1)) : (Skill)(-1);
            if ((int)s < 0) { Print("usage: skill <vitality|power|agility|focus|thrusters>"); return; }
            Print(_g.SpendSkill(s) ? $"{s.ToString().ToLowerInvariant()} is now rank {_g.Profile.Rank(s)}" : "no points to spend, or that skill is maxed");
        });
        Add("arena", "[fighter|cleric|mage]", "start a run in the Chaos Arena (your class unless you name one)", a =>
        {
            var names = Enum.GetNames<PClass>();
            var n = a.Length > 1 ? names.FirstOrDefault(c => c.StartsWith(a[1], StringComparison.OrdinalIgnoreCase)) : null;
            if (a.Length > 1 && n == null) { Print("usage: arena [fighter|cleric|mage]"); return; }
            _g.StartArena(n != null ? Enum.Parse<PClass>(n) : _g.P?.Class ?? PClass.Fighter);
            Open = false;
        });
        Add("arenamods", "[letters|-]", "the arena's modifiers: S double-speed monsters, N no supplies, M melee only, R random class (- for none)", a =>
        {
            if (a.Length > 1)
            {
                var mods = ArenaMod.None;
                foreach (char c in a[1].ToUpperInvariant())
                    foreach (var m in ArenaModInfo.All) if (ArenaModInfo.Letter(m) == c) mods |= m;
                _g.ArenaMods = mods;
                _g.SaveSettings();
            }
            Print($"arena modifiers: {ArenaModInfo.Letters(_g.ArenaMods)} (score x{ArenaModInfo.Multiplier(_g.ArenaMods):0.00})");
        });
        Add("demo", "", "on a practice course: watch the demo play it (1/2/3 speed, E step by step, move to take over)", a =>
        {
            if (!_g.Practicing) { Print("demo: start a practice course first (title menu > Practice)"); return; }
            _g.StartDemo();
            Open = false;
        });
        Add("name", "[name]", "the name your practice course times go on the leaderboard under", a =>
        {
            if (a.Length < 2) { Print($"name = {_g.RunnerName}"); return; }
            _g.RunnerName = Game.CleanName(string.Join(' ', a[1..]));
            Print($"name = {_g.RunnerName}");
            _g.SaveSettings();
        });
        Add("renderedart", "[0|1]", "use the Blender-rendered sci-fi art pack", a =>
        {
            if (a.Length < 2) { Print($"renderedart = {(Art.Rendered ? 1 : 0)} ({RenderedArt.Covered.Count()} rendered assets available)"); return; }
            bool on = a[1] is "1" or "on" or "true";
            if (on && !RenderedArt.Available) { Print("no rendered art pack in this build"); return; }
            _g.SetRenderedArt(on);
            Print($"renderedart = {(on ? 1 : 0)}");
            _g.SaveSettings();
        });
        Add("artstyle", "[scifi|fantasy]", "switch the visual style", a =>
        {
            if (a.Length < 2) { Print("artstyle = " + (Art.Style == ArtStyle.SciFi ? "scifi" : "fantasy")); return; }
            var style = a[1].ToLowerInvariant() switch { "scifi" or "sci-fi" or "sf" => (ArtStyle?)ArtStyle.SciFi, "fantasy" or "doom" or "hexen" => ArtStyle.Fantasy, _ => null };
            if (style == null) { Print("usage: artstyle <scifi|fantasy>"); return; }
            _g.SetArtStyle(style.Value);
            Print("artstyle = " + a[1].ToLowerInvariant());
            _g.SaveSettings();
        });
        Add("playmap", "<file|name>", "play a custom map file (made in tools/editor/index.html)", a =>
        {
            if (a.Length < 2) { Print("usage: playmap <path/to/map.hxm | name in the maps folder>"); return; }
            string arg = string.Join(' ', a.Skip(1));
            string path = File.Exists(arg) ? arg : _g.MapsDir != null ? Path.Combine(_g.MapsDir, MapDoc.FileName(arg)) : arg;
            if (!File.Exists(path)) { Print($"no map file {arg}"); return; }
            try
            {
                var doc = MapDoc.Parse(File.ReadAllText(path));
                if (!doc.Cells.Contains('@')) { Print("that map has no player start"); return; }
                _g.StartTest(doc.ToDef(), _g.P?.Class ?? PClass.Fighter);
                Open = false;
            }
            catch (Exception e) { Print("can't load map: " + e.Message); }
        });
        Add("restart", "", "restart with the current class", _ =>
        {
            _g.NewGame(_g.P?.Class ?? PClass.Fighter);
            Print("restarted");
        });
        Add("chests", "", "list the chests on this map", _ =>
        {
            if (!InGame()) return;
            var list = _g.Level.Things.OfType<Chest>().ToList();
            Print($"{_g.P.ChestsOpened}/{_g.ChestsTotal} chests opened in the hub; {list.Count} on this map:");
            foreach (var c in list) Print($"  {c.X:0.0},{c.Y:0.0}  {(c.Opened ? "open" : "closed")}");
        });
        Add("seed", "[n|random]", "fix the chest layout seed (applies on restart)", a =>
        {
            if (a.Length < 2) { Print(_g.FixedSeed is int s ? $"seed = {s}" : "seed = random"); return; }
            if (a[1] == "random") { _g.FixedSeed = null; Print("seed = random"); return; }
            if (!int.TryParse(a[1], out int n)) { Print("usage: seed <number|random>"); return; }
            _g.FixedSeed = n;
            Print($"seed = {n}; 'restart' to apply");
        });
        Add("bind", "[action] [key] [key2]", "show or change key bindings", a =>
        {
            if (a.Length == 1)
            {
                foreach (var b in Bindings.All)
                    Print($"  {b.Id,-12} {Keys.Name(_g.Binds.Get(b.Act, 0)),-9} {Keys.Name(_g.Binds.Get(b.Act, 1))}");
                return;
            }
            var info = Bindings.Find(a[1]);
            if (info == null) { Print($"unknown action '{a[1]}'. Actions: " + string.Join(" ", Bindings.All.Select(b => b.Id))); return; }
            if (a.Length == 2) { Print($"{info.Id} = {Keys.Name(_g.Binds.Get(info.Act, 0))} {Keys.Name(_g.Binds.Get(info.Act, 1))}"); return; }
            for (int slot = 0; slot < Bindings.Slots && slot + 2 < a.Length; slot++)
            {
                int code = Keys.Parse(a[slot + 2]);
                if (code == Keys.None && a[slot + 2] != "---") { Print($"unknown key '{a[slot + 2]}'"); return; }
                if (Keys.Reserved(code)) { Print($"{Keys.Name(code)} is reserved for menus"); return; }
                var d = _g.Binds.Set(info.Act, slot, code);
                if (d is Act other) Print($"  (removed {Keys.Name(code)} from {Bindings.All[(int)other].Id})");
            }
            if (a.Length == 3) _g.Binds.Set(info.Act, 1, Keys.None);
            Print($"{info.Id} = {Keys.Name(_g.Binds.Get(info.Act, 0))} {Keys.Name(_g.Binds.Get(info.Act, 1))}");
            _g.SaveSettings();
        });
        Add("unbind", "<action>", "clear an action's keys", a =>
        {
            var info = a.Length > 1 ? Bindings.Find(a[1]) : null;
            if (info == null) { Print("usage: unbind <action>"); return; }
            _g.Binds.Set(info.Act, 0, Keys.None);
            _g.Binds.Set(info.Act, 1, Keys.None);
            Print($"{info.Id} unbound");
            _g.SaveSettings();
        });
        Add("binddefaults", "", "restore the default key bindings", _ =>
        {
            _g.Binds.Reset();
            Print("key bindings reset");
            _g.SaveSettings();
        });
        Add("clear", "", "clear the console", _ => { Log.Clear(); Scroll = 0; });
        Add("quit", "", "exit the game", _ => _g.QuitRequested = true);

        Print("Hexen Sharp console. Type 'help' for commands, 'cheats' for cheat codes.");
    }

    void Add(string name, string usage, string help, Action<string[]> run) => _commands.Add(new Command(name, usage, help, run));

    public void Print(string s)
    {
        if (_quiet > 0) return;
        Log.Add(s);
        if (Log.Count > 200) Log.RemoveAt(0);
        Scroll = 0;
    }

    string Fmt(GameVars.Var v)
    {
        float x = v.Get(_g.Vars);
        return v.IsBool ? (x != 0 ? "on" : "off") : x.ToString("0.##", CultureInfo.InvariantCulture);
    }

    static bool TryNum(string s, out float x)
    {
        switch (s.ToLowerInvariant())
        {
            case "on": case "true": case "yes": x = 1; return true;
            case "off": case "false": case "no": x = 0; return true;
        }
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out x);
    }

    bool InGame()
    {
        if (_g.P != null && _g.Mode is GameMode.Playing or GameMode.Dead) return true;
        Print("start a game first (or use 'class <name>')");
        return false;
    }

    void Toggle(string name)
    {
        var v = GameVars.Find(name);
        v.Set(_g.Vars, v.Get(_g.Vars) != 0 ? 0 : 1);
        string msg = $"{name} {Fmt(v)}";
        Print(msg);
        _g.Say(msg.ToUpperInvariant());
    }

    void Help()
    {
        foreach (var c in _commands) Print($"  {(c.Name + " " + c.Usage).Trim()} - {c.Help}");
    }

    void Give(string what)
    {
        if (!InGame()) return;
        var p = _g.P;
        what = what.ToLowerInvariant();
        bool all = what == "all";
        bool any = false;
        if (all || what == "health") { p.Health = p.MaxHealth; any = true; }
        if (all || what == "armor") { p.Armor = 100; any = true; }
        if (all || what == "weapons") { p.HasWeapon[1] = p.HasWeapon[2] = true; any = true; }
        if (all || what == "mana" || what == "weapons") { p.BlueMana = p.GreenMana = 200; any = true; }
        if (all || what == "keys") { p.SteelKey = p.FireKey = true; any = true; }
        if (all || what == "items") { p.Flasks = 9; p.Urns = 3; any = true; }
        if (all || what == "jetpack") { p.HasJetpack = true; p.Fuel = p.MaxFuel; any = true; }
        if (!any) { Print("usage: give <all|health|mana|weapons|keys|items|armor|jetpack>"); return; }
        Print($"given: {what}");
        _g.Say($"Cheater! ({what})");
        _g.PlaySound(Sfx.Item, 1);
    }

    void Map(string arg)
    {
        if (!InGame()) return;
        var hub = _g.Hub;
        if (arg == null)
        {
            for (int i = 0; i < hub.Length; i++) Print($"  {i + 1}: {hub[i].Name}{(hub[i] == _g.Level ? "  (here)" : "")}");
            Print("usage: map <number|name>");
            return;
        }
        int idx = int.TryParse(arg, out int n) ? n - 1
            : Array.FindIndex(hub, l => l.Name.Contains(arg, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || idx >= hub.Length) { Print($"no map '{arg}'"); return; }
        _g.Warp(idx);
        Print($"warped to {hub[idx].Name}");
    }

    static readonly (string name, char glyph)[] Summonable =
    {
        ("ettin", 'e'), ("afrit", 'a'), ("centaur", 'c'), ("slaughtaur", 'C'), ("bishop", 'd'), ("heresiarch", 'H'),
        ("vial", 'h'), ("flask", 'q'), ("urn", 'u'), ("bluemana", 'b'), ("greenmana", 'g'), ("armor", 'r'),
        ("chest", '$'), ("lore", '&'), ("steelkey", 'k'), ("firekey", 'f'), ("weapon2", 'w'), ("weapon3", 'x'), ("jetpack", 'J'), ("torch", 't'), ("pillar", 'p'), ("tree", 'T'),
    };

    void Summon(string what)
    {
        if (!InGame()) return;
        var match = what == null ? default : Summonable.FirstOrDefault(s => s.name.StartsWith(what, StringComparison.OrdinalIgnoreCase));
        if (match.name == null)
        {
            Print("usage: summon <" + string.Join("|", Summonable.Select(s => s.name)) + ">");
            return;
        }
        Print(_g.Summon(match.glyph) ? $"summoned {match.name}" : "no room in front of you");
    }

    int _quiet;

    /// <summary>Runs a command. Quiet mode (used when loading settings) prints nothing.</summary>
    public void Execute(string line, bool quiet)
    {
        if (!quiet) { Execute(line); return; }
        _quiet++;
        try { Execute(line); }
        finally { _quiet--; }
    }

    public void Execute(string line)
    {
        line = line.Trim();
        if (line.Length == 0) return;
        Print("> " + line);
        var args = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string name = args[0].ToLowerInvariant();

        var cheat = Cheats.FirstOrDefault(c => c.code == name);
        if (cheat.code != null) { Execute(cheat.cmd); return; }
        if (name.StartsWith("visit") && name.Length > 5) { Map(name[5..]); return; }

        var cmd = _commands.FirstOrDefault(c => c.Name == name);
        if (cmd != null) { cmd.Run(args); return; }

        // "speed 2" works as shorthand for "set speed 2"; "speed" alone prints it
        var v = GameVars.Find(name);
        if (v != null)
        {
            if (args.Length > 1) Execute($"set {name} {args[1]}");
            else Print($"{v.Name} = {Fmt(v)}");
            return;
        }
        Print($"unknown command '{name}'. Type 'help'.");
    }

    public void HandleInput(Input inp)
    {
        if (inp.Pause) { Open = false; return; }
        if (!string.IsNullOrEmpty(inp.Typed))
            foreach (char c in inp.Typed)
                if (c >= ' ' && c < 127 && c != '`' && c != '~' && Line.Length < 48) Line += c;
        if (inp.Backspace && Line.Length > 0) Line = Line[..^1];
        if (inp.Confirm)
        {
            if (Line.Trim().Length > 0) { _history.Add(Line); _histIndex = -1; }
            Execute(Line);
            Line = "";
        }
        if (inp.Up && _history.Count > 0)
        {
            _histIndex = _histIndex < 0 ? _history.Count - 1 : Math.Max(0, _histIndex - 1);
            Line = _history[_histIndex];
        }
        if (inp.Down && _histIndex >= 0)
        {
            _histIndex++;
            if (_histIndex >= _history.Count) { _histIndex = -1; Line = ""; }
            else Line = _history[_histIndex];
        }
        if (inp.PageUp) Scroll = Math.Min(Math.Max(0, Log.Count - 1), Scroll + 5);
        if (inp.PageDown) Scroll = Math.Max(0, Scroll - 5);
        if (inp.Tab) Complete();
    }

    void Complete()
    {
        var parts = Line.Split(' ');
        IEnumerable<string> pool = parts.Length == 1
            ? _commands.Select(c => c.Name).Concat(GameVars.All.Select(v => v.Name))
            : parts[0] is "set" or "get" && parts.Length == 2 ? GameVars.All.Select(v => v.Name)
            : parts[0] == "summon" && parts.Length == 2 ? Summonable.Select(s => s.name)
            : Enumerable.Empty<string>();
        string last = parts[^1];
        var hits = pool.Where(n => n.StartsWith(last, StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
        if (hits.Count == 1) { parts[^1] = hits[0]; Line = string.Join(' ', parts) + " "; }
        else if (hits.Count > 1) Print("  " + string.Join("  ", hits));
    }

    /// <summary>Called with every character typed during play; fires cheat codes like Hexen.</summary>
    public void FeedCheat(char c)
    {
        _cheatBuf = (_cheatBuf + char.ToLowerInvariant(c));
        if (_cheatBuf.Length > 16) _cheatBuf = _cheatBuf[^16..];
        foreach (var (code, cmd) in Cheats)
            if (_cheatBuf.EndsWith(code))
            {
                _cheatBuf = "";
                Print($"cheat: {code}");
                Execute(cmd);
                return;
            }
        int v = _cheatBuf.LastIndexOf("visit", StringComparison.Ordinal);
        if (v >= 0 && _cheatBuf.Length == v + 6 && char.IsDigit(_cheatBuf[^1]))
        {
            _cheatBuf = "";
            Print($"cheat: visit{c}");
            Map(c.ToString());
        }
    }
}
