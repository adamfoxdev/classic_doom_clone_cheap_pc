namespace HexenSharp;

public enum MenuPage { Main, Pause, Options, Bindings, Style, Character, Leaderboard, Courses, ArenaSetup, Achievements, Effects, Codex, Rematch }

/// <summary>
/// Title, pause, options and key-binding menus. Arrow keys (or your movement keys), Enter and Esc drive them;
/// those fixed keys always work, so no binding can lock you out.
/// </summary>
public sealed class MenuSystem
{
    readonly Game _g;
    readonly Stack<(MenuPage page, int cursor)> _back = new();
    public MenuPage? Page;
    public int Cursor, Column, Scroll;
    public bool Capturing;
    bool _captureArmed;
    public string Notice = "";
    public float NoticeTime;
    public const int BindRows = 15, AchievementRows = 11;

    /// <summary>Whose leaderboard the Leaderboard page shows: Left/Right switch class, Up/Down the course.</summary>
    public PClass BoardClass;
    public Course BoardCourse = Courses.Hangar;
    /// <summary>
    /// Which board the Leaderboard page shows: a timed course's id (BoardCourse), or one of the other boards, which
    /// come after the timed courses in the order of OtherBoards.
    /// </summary>
    public string BoardPage = "hangar";
    public static readonly string[] OtherBoards = { "endless", "tower", "range", "rail", "rematch", "arena", "instagib", "daily" };
    bool Is(string page) => BoardPage == page;
    void Set(string page, bool on) { if (on) BoardPage = page; else if (BoardPage == page) BoardPage = BoardCourse.Id; }
    /// <summary>The arena's board (waves) rather than a course's (times).</summary>
    public bool BoardArena { get => Is("arena"); set => Set("arena", value); }
    /// <summary>The daily challenge's board, for the day BoardDay back from today.</summary>
    public bool BoardDaily { get => Is("daily"); set => Set("daily", value); }
    public bool BoardEndless { get => Is("endless"); set => Set("endless", value); }
    /// <summary>The endless rocket tower's board.</summary>
    public bool BoardTower { get => Is("tower"); set => Set("tower", value); }
    /// <summary>The shooting range's drill board, and its rail trials'.</summary>
    public bool BoardRange { get => Is("range"); set => Set("range", value); }
    public bool BoardRail { get => Is("rail"); set => Set("rail", value); }
    /// <summary>A mini-boss's rematch board: which one (in MiniBosses.All).</summary>
    public bool BoardRematch { get => Is("rematch"); set => Set("rematch", value); }
    /// <summary>The instagib arena's board.</summary>
    public bool BoardInstagib { get => Is("instagib"); set => Set("instagib", value); }
    public int BoardBoss;
    public int BoardDay;

    public MenuSystem(Game g) { _g = g; }

    public bool Open => Page != null;

    public void Show(MenuPage p)
    {
        if (Page != null) _back.Push((Page.Value, Cursor));
        Page = p; Cursor = 0; Column = 0; Scroll = 0; Capturing = false; NoticeTime = 0;
        if (p == MenuPage.Leaderboard)
        {
            BoardClass = _g.P?.Class ?? PClass.Fighter;
            BoardCourse = _g.Practicing && _g.Course.Timed ? _g.Course : Courses.Hangar;
            BoardPage = _g.ArenaMode && _g.DailyMode ? "daily" : _g.ArenaMode && _g.InstagibOn ? "instagib" : _g.ArenaMode ? "arena"
                : _g.OnEndless ? "endless" : _g.OnTower ? "tower" : _g.OnRange ? (_g.RailTrial ? "rail" : "range") : _g.Rematch != null ? "rematch" : BoardCourse.Id;
            BoardBoss = _g.Rematch != null ? Array.IndexOf(MiniBosses.All, _g.Rematch) : 0;
            BoardDay = 0;
        }
    }

    public void Close()
    {
        _back.Clear();
        Page = null;
        Capturing = false;
    }

    void Back()
    {
        if (Page is MenuPage.Options or MenuPage.Bindings or MenuPage.Effects) _g.SaveSettings();
        if (_back.Count > 0) { (Page, Cursor) = _back.Pop(); Column = 0; Capturing = false; return; }
        if (Page is MenuPage.Pause or MenuPage.Character) { Close(); _g.Paused = false; }
    }

    void Say(string s) { Notice = s; NoticeTime = 3f; }

    /// <summary>A skill's name in the current style (the jetpack is the Wings of Wrath in fantasy).</summary>
    public static string SkillName(Skill s) => s == Skill.Thrusters && Art.Style == ArtStyle.Fantasy ? "Wings" : s.ToString();

    // ------------------------------------------------------------ page contents

    public string[] Items(MenuPage p) => p switch
    {
        // Continue heads the list when there's a saved campaign to pick up
        MenuPage.Main => (_g.CheckSave() != null ? new[] { "Continue" } : Array.Empty<string>())
            .Concat(new[] { "New game" }).Concat(_g.Profile.NgUnlocked > 0 ? new[] { "New Game+" } : Array.Empty<string>())
            .Concat(new[] { "Practice", "Arena", "Story", "Leaderboard", "Character", "Options", "Quit" }).ToArray(),
        MenuPage.Pause => _g.Practicing && !_g.OnRange
            ? new[] { "Resume", _g.Demo ? "Stop demo" : "Watch demo", "Character", "Leaderboard", "Options", "Restart", "Quit to title", "Quit game" }
            : _g.ArenaMode || _g.OnRange
            ? new[] { "Resume", "Character", "Leaderboard", "Options", "Restart", "Quit to title", "Quit game" }
            : new[] { "Resume", "Character", "Options", "Restart", "Quit to title", "Quit game" },
        MenuPage.Leaderboard => new[] { "Back" },
        MenuPage.Courses => Courses.All.Select(c => c.Name).Append("Back").ToArray(),
        MenuPage.ArenaSetup => ArenaModInfo.All.Select(ArenaModInfo.Name).Append("Start").Append("Daily challenge").Append("Rematch").Append("Back").ToArray(),
        MenuPage.Rematch => MiniBosses.All.Select(d => _g.Profile.MiniBosses.Contains(d.MiniBoss) ? d.Name : "???").Append("Back").ToArray(),
        MenuPage.Character => Profile.Skills.Select(SkillName).Append("Achievements").Append("Codex").Append("Back").ToArray(),
        MenuPage.Codex => HexenSharp.Codex.All.Select(e => HexenSharp.Codex.Unlocked(_g.Profile, e) ? e.Def.Name : "???").Append("Back").ToArray(),
        MenuPage.Achievements => HexenSharp.Achievements.All.Select(a => a.Name).Append("Back").ToArray(),
        MenuPage.Style => new[] { "Classic", "Relaxed", "Back" },
        MenuPage.Options => new[] { "Key bindings", "Mouse sensitivity", "Invert mouse", "Field of view", "Show FPS", "Visual style", "Rendered art", "HUD style", "Crosshair", "Movement", "Practice ghost", "Strafe helper", "Arcade mode", "Difficulty", "Music volume", "Effects", "Back" },
        MenuPage.Effects => new[] { "Screen shake", "Hit-stop", "Damage numbers", "Boss intros", "Dynamic music", "Back" },
        _ => Bindings.All.Select(b => b.Label).Concat(new[] { "Reset to defaults", "Back" }).ToArray(),
    };

    /// <summary>Right-hand value shown next to an options item.</summary>
    public string Value(int item)
    {
        var v = _g.Vars;
        if (Page == MenuPage.Effects)
            return item switch
            {
                0 => v.Shake switch { 0 => "OFF", 1 => "NORMAL", _ => "STRONG" },
                1 => v.HitStop ? "ON" : "OFF",
                2 => v.Arcade ? "ON (ARCADE)" : v.DamageNumbers ? "ON" : "OFF",
                3 => v.BossIntros ? "ON" : "OFF",
                4 => v.DynamicMusic ? "ON" : "OFF",
                _ => "",
            };
        return item switch
        {
            1 => v.Sens.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
            2 => v.InvertMouse ? "ON" : "OFF",
            3 => $"{v.Fov:0}",
            4 => v.ShowFps ? "ON" : "OFF",
            5 => Art.Style == ArtStyle.SciFi ? "SCI-FI" : "FANTASY",
            6 => !RenderedArt.Available ? "N/A" : Art.Rendered ? (Art.Style == ArtStyle.SciFi ? "ON" : "ON (SCI-FI)") : "OFF",
            7 => Game.HudName(v.Hud),
            8 => v.Crosshair.ToString().ToUpperInvariant(),
            9 => v.QuakeMove ? "QUAKE" : "CLASSIC",
            10 => v.Ghost ? "ON" : "OFF",
            11 => v.StrafeHelp switch { 0 => "OFF", 1 => "PRACTICE", _ => "ALWAYS" },
            12 => v.Arcade ? "ON" : "OFF",
            13 => Difficulties.Name(Difficulties.Of(v)),
            14 => v.Music <= 0 ? "OFF" : $"{v.Music * 100:0}%",
            _ => "",
        };
    }

    // ------------------------------------------------------------ input

    public void Update(Input inp, float dt)
    {
        NoticeTime -= dt;
        if (Page == null) return;
        var items = Items(Page.Value);

        if (Capturing)
        {
            if (!_captureArmed) { _captureArmed = true; return; } // ignore the Enter that started it
            if (inp.Pause) { Capturing = false; Say("Cancelled."); return; }
            if (inp.KeyPressed == Keys.None) return;
            Capturing = false;
            if (Keys.Reserved(inp.KeyPressed)) { Say($"{Keys.Name(inp.KeyPressed)} is reserved for menus."); return; }
            var b = Bindings.All[Cursor];
            var displaced = _g.Binds.Set(b.Act, Column, inp.KeyPressed);
            string msg = $"{b.Label}: {Keys.Name(inp.KeyPressed)}";
            if (displaced is Act d) msg += $" (removed from {Bindings.All[(int)d].Label})";
            Say(msg);
            _g.PlaySound(Sfx.Pickup, 0.8f);
            return;
        }

        if (inp.Pause || (inp.Character && Page == MenuPage.Character)) { _g.PlaySound(Sfx.Swing, 0.5f); Back(); return; }
        if (inp.Up) { Cursor = (Cursor + items.Length - 1) % items.Length; _g.PlaySound(Sfx.Swing, 0.5f); }
        if (inp.Down) { Cursor = (Cursor + 1) % items.Length; _g.PlaySound(Sfx.Swing, 0.5f); }

        if (Page == MenuPage.Codex)
        {
            if (inp.Confirm && Cursor == items.Length - 1) Back();
            return;
        }

        if (Page == MenuPage.Achievements)
        {
            if (Cursor < Scroll) Scroll = Cursor;
            if (Cursor >= Scroll + AchievementRows) Scroll = Cursor - AchievementRows + 1;
            Scroll = Math.Clamp(Scroll, 0, Math.Max(0, items.Length - 1 - AchievementRows)); // Back sits below the list, not in it
            if (inp.Confirm && Cursor == items.Length - 1) Back();
            return;
        }

        if (Page == MenuPage.Bindings)
        {
            bool onAction = Cursor < Bindings.Count;
            if (onAction && (inp.Left || inp.Right)) Column = 1 - Column;
            if (Cursor < Scroll) Scroll = Cursor;
            if (Cursor >= Scroll + BindRows) Scroll = Cursor - BindRows + 1;
            if (onAction && inp.Backspace)
            {
                var b = Bindings.All[Cursor];
                _g.Binds.Set(b.Act, Column, Keys.None);
                Say($"{b.Label}: slot cleared");
            }
            if (inp.Confirm)
            {
                if (onAction) { Capturing = true; _captureArmed = false; }
                else if (Cursor == Bindings.Count) { _g.Binds.Reset(); Say("Key bindings reset to defaults."); _g.PlaySound(Sfx.Item, 1); }
                else Back();
            }
            return;
        }

        if (Page == MenuPage.Character && (inp.Right || inp.Confirm))
        {
            if (Cursor >= Profile.Skills.Length)
            {
                if (inp.Confirm) { if (items[Cursor] == "Achievements") Show(MenuPage.Achievements); else if (items[Cursor] == "Codex") Show(MenuPage.Codex); else Back(); }
                return;
            }
            var skill = Profile.Skills[Cursor];
            var pr = _g.Profile;
            if (_g.SpendSkill(skill)) { Say($"{SkillName(skill)} rank {pr.Rank(skill)}: {Profile.Effect(skill, pr.Rank(skill)).ToLowerInvariant()}"); _g.PlaySound(Sfx.Item, 1); }
            else { Say(pr.Rank(skill) >= Profile.MaxRank ? $"{SkillName(skill)} is maxed out." : "No skill points. Earn experience to level up."); _g.PlaySound(Sfx.Locked, 0.6f); }
            return;
        }

        if (Page == MenuPage.Leaderboard)
        {
            if (inp.Left || inp.Right)
            {
                // on the daily board, Left goes back a day (Right forward, up to today); elsewhere, the class
                if (BoardDaily) BoardDay = Math.Max(0, BoardDay + (inp.Left ? 1 : -1));
                else if (BoardRematch) BoardBoss = (BoardBoss + (inp.Left ? MiniBosses.All.Length - 1 : 1)) % MiniBosses.All.Length;
                else BoardClass = (PClass)(((int)BoardClass + (inp.Left ? 2 : 1)) % 3);
                _g.PlaySound(Sfx.Swing, 0.5f);
            }
            if (inp.Up || inp.Down)
            {
                // the timed courses, then the other boards (endless, the tower, the range, ... the daily challenge), round and round
                var pages = Courses.Timed.Select(c => c.Id).Concat(OtherBoards).ToArray();
                int n = pages.Length, i = Array.IndexOf(pages, BoardPage);
                i = ((i < 0 ? 0 : i) + (inp.Up ? n - 1 : 1)) % n;
                BoardPage = pages[i];
                if (Courses.Timed.FirstOrDefault(c => c.Id == BoardPage) is { } course) BoardCourse = course;
                BoardDay = 0;
                _g.PlaySound(Sfx.Swing, 0.5f);
            }
            Cursor = 0;
            if (inp.Confirm) Back();
            return;
        }

        if (Page == MenuPage.Options && (inp.Left || inp.Right || inp.Confirm))
        {
            int dir = inp.Left ? -1 : 1;
            var v = _g.Vars;
            switch (Cursor)
            {
                case 0: if (inp.Confirm) Show(MenuPage.Bindings); return;
                case 1: v.Sens = MathF.Round(Math.Clamp(v.Sens + 0.1f * dir, 0.1f, 5f), 1); break;
                case 2: v.InvertMouse = !v.InvertMouse; break;
                case 3: v.Fov = Math.Clamp(v.Fov + 5 * dir, 50f, 110f); break;
                case 4: v.ShowFps = !v.ShowFps; break;
                case 5: _g.SetArtStyle(Art.Style == ArtStyle.SciFi ? ArtStyle.Fantasy : ArtStyle.SciFi); break;
                case 6:
                    if (!RenderedArt.Available) { Say("No rendered art pack in this build."); return; }
                    _g.SetRenderedArt(!Art.Rendered);
                    Say(Art.Rendered ? "Blender-rendered art on (sci-fi style)." : "Procedural art.");
                    break;
                case 7:
                    int n = Enum.GetValues<HudStyle>().Length;
                    v.Hud = (HudStyle)(((int)v.Hud + dir + n) % n);
                    break;
                case 8:
                    int cn = Enum.GetValues<CrosshairStyle>().Length;
                    v.Crosshair = (CrosshairStyle)(((int)v.Crosshair + dir + cn) % cn);
                    break;
                case 9:
                    v.QuakeMove = !v.QuakeMove;
                    Say(v.QuakeMove ? "Quake movement: strafe-jump to build speed." : "Classic movement.");
                    break;
                case 10:
                    v.Ghost = !v.Ghost;
                    Say(v.Ghost ? "Your best practice run races you as a ghost." : "No practice ghost.");
                    break;
                case 11:
                    v.StrafeHelp = (v.StrafeHelp + dir + 3) % 3;
                    Say(v.StrafeHelp switch { 0 => "No strafe helper.", 1 => "Strafe helper on the practice course.", _ => "Strafe helper everywhere." });
                    break;
                case 12:
                    v.Arcade = !v.Arcade;
                    Say(v.Arcade ? "Arcade mode: damage numbers, score and a style rank. Keep the combo going!" : "Arcade mode off.");
                    break;
                case 13:
                    {
                        // from a custom setting, Right goes to Normal and Left to Easy
                        var presets = Difficulties.Presets;
                        int at = Array.IndexOf(presets, Difficulties.Of(v));
                        at = at < 0 ? (dir > 0 ? 1 : 0) : inp.Confirm ? (at + 1) % presets.Length : Math.Clamp(at + dir, 0, presets.Length - 1);
                        Difficulties.Apply(v, presets[at]);
                        Say($"{Difficulties.Name(presets[at])}: {Difficulties.About(presets[at])}.");
                        break;
                    }
                case 14:
                    v.Music = MathF.Round(Math.Clamp(v.Music + 0.1f * dir, 0f, 1f), 1);
                    break;
                case 15: if (inp.Confirm) Show(MenuPage.Effects); return;
                default: if (inp.Confirm) Back(); return;
            }
            _g.PlaySound(Sfx.Pickup, 0.6f);
            return;
        }

        if (Page == MenuPage.Effects && (inp.Left || inp.Right || inp.Confirm))
        {
            int dir = inp.Left ? -1 : 1;
            var v = _g.Vars;
            switch (Cursor)
            {
                case 0:
                    v.Shake = (v.Shake + dir + 3) % 3;
                    Say(v.Shake switch { 0 => "No screen shake.", 1 => "Screen shake: normal.", _ => "Screen shake: strong." });
                    if (v.Shake > 0) _g.AddShake(0.5f); // a taste of it
                    break;
                case 1: v.HitStop = !v.HitStop; Say(v.HitStop ? "Heavy blows freeze the action for a split second." : "No hit-stop."); break;
                case 2: v.DamageNumbers = !v.DamageNumbers; Say(v.DamageNumbers ? "Damage numbers pop out of what you hit." : v.Arcade ? "Arcade mode keeps its damage numbers." : "No damage numbers."); break;
                case 3: v.BossIntros = !v.BossIntros; Say(v.BossIntros ? "A name card when a boss wakes." : "No boss intros."); break;
                case 4: v.DynamicMusic = !v.DynamicMusic; Say(v.DynamicMusic ? "The music swells when monsters are onto you." : "The music stays calm."); break;
                default: if (inp.Confirm) Back(); return;
            }
            _g.PlaySound(Sfx.Pickup, 0.6f);
            return;
        }

        if (Page == MenuPage.ArenaSetup)
        {
            // the modifiers toggle with Left/Right or Enter; Start goes on to the class (or straight in, for a random one)
            int n = ArenaModInfo.All.Length;
            if (Cursor < n && (inp.Left || inp.Right || inp.Confirm))
            {
                var mod = ArenaModInfo.All[Cursor];
                _g.ArenaMods ^= mod;
                // the Quake modes set your weapons, so they don't mix with each other or with melee only
                if ((_g.ArenaMods & mod) != 0)
                    foreach (var other in new[] { ArenaMod.Instagib, ArenaMod.RocketArena, ArenaMod.MeleeOnly })
                        if (other != mod && (ArenaModInfo.Quake(mod) || ArenaModInfo.Quake(other))) _g.ArenaMods &= ~other;
                _g.SaveSettings();
                _g.PlaySound(Sfx.Pickup, 0.6f);
                return;
            }
            if (!inp.Confirm) return;
            _g.PlaySound(Sfx.Item, 0.8f);
            var items2 = Items(MenuPage.ArenaSetup);
            if (items2[Cursor] == "Start")
            {
                _g.Style = GameStyle.Classic; _g.PendingPractice = false;
                Close();
                if ((_g.ArenaMods & ArenaMod.RandomClass) != 0) { _g.PendingArena = false; _g.StartArena(PClass.Fighter); _g.PlaySound(Sfx.Teleport, 1); }
                else { _g.PendingArena = true; _g.Mode = GameMode.ClassSelect; _g.MenuIndex = 0; }
            }
            else if (items2[Cursor] == "Daily challenge")
            {
                // the day sets the class and the modifiers: straight in
                _g.PendingPractice = _g.PendingArena = false;
                Close();
                _g.StartDaily();
                _g.PlaySound(Sfx.Teleport, 1);
            }
            else if (items2[Cursor] == "Rematch") Show(MenuPage.Rematch);
            else Back();
            return;
        }

        if (Page == MenuPage.Rematch)
        {
            if (!inp.Confirm) return;
            if (Cursor >= MiniBosses.All.Length) { Back(); return; }
            var boss = MiniBosses.All[Cursor];
            if (!_g.Profile.MiniBosses.Contains(boss.MiniBoss)) { Say("Beat it in the campaign first."); _g.PlaySound(Sfx.Locked, 0.6f); return; }
            _g.PlaySound(Sfx.Item, 0.8f);
            _g.Style = GameStyle.Classic; _g.PendingPractice = _g.PendingArena = false; _g.PendingRematch = boss;
            Close(); _g.Mode = GameMode.ClassSelect; _g.MenuIndex = 0;
            return;
        }

        if (!inp.Confirm) return;
        _g.PlaySound(Sfx.Item, 0.8f);
        if (Page == MenuPage.Main)
        {
            switch (items[Cursor])
            {
                case "Continue":
                    Close();
                    if (_g.Continue()) _g.PlaySound(Sfx.Teleport, 1);
                    break;
                case "New game": Show(MenuPage.Style); Cursor = (int)_g.Style; break;
                case "New Game+":
                    // the highest tier you've opened, in classic style; pick a class and go
                    _g.Style = GameStyle.Classic; _g.NgTier = _g.Profile.NgUnlocked; _g.PendingPractice = _g.PendingArena = false; _g.PendingRematch = null;
                    Close(); _g.Mode = GameMode.ClassSelect; _g.MenuIndex = 0;
                    break;
                case "Story": Close(); _g.StartStory(0); break;
                case "Practice": Show(MenuPage.Courses); break;
                case "Arena": Show(MenuPage.ArenaSetup); Cursor = ArenaModInfo.All.Length; break;
                case "Leaderboard": Show(MenuPage.Leaderboard); break;
                case "Character": Show(MenuPage.Character); break;
                case "Options": Show(MenuPage.Options); break;
                case "Quit": _g.QuitRequested = true; break;
            }
            return;
        }
        switch (Page, Cursor)
        {
            case (MenuPage.Style, 0 or 1): _g.Style = (GameStyle)Cursor; _g.NgTier = 0; _g.PendingPractice = _g.PendingArena = false; _g.PendingRematch = null; Close(); _g.Mode = GameMode.ClassSelect; _g.MenuIndex = 0; break;
            case (MenuPage.Courses, var c) when c < Courses.All.Length:
                _g.Style = GameStyle.Classic; _g.PendingPractice = true; _g.PendingArena = false; _g.PendingCourse = Courses.All[c];
                Close(); _g.Mode = GameMode.ClassSelect; _g.MenuIndex = 0;
                break;
            case (MenuPage.Courses, _): Back(); break;
            case (MenuPage.Style, 2): Back(); break;
            case (MenuPage.Pause, _):
                switch (items[Cursor])
                {
                    case "Resume": Close(); _g.Paused = false; break;
                    case "Watch demo": Close(); _g.Paused = false; _g.StartDemo(); break;
                    case "Stop demo": Close(); _g.Paused = false; _g.EndDemo(); break;
                    case "Character": Show(MenuPage.Character); break;
                    case "Leaderboard": Show(MenuPage.Leaderboard); break;
                    case "Options": Show(MenuPage.Options); break;
                    case "Restart": Close(); _g.NewGame(_g.P.Class); break;
                    case "Quit to title": Close(); _g.GoToTitle(); break;
                    case "Quit game": _g.QuitRequested = true; break;
                }
                break;
        }
    }
}

/// <summary>Loads and saves options and key bindings as a small script of console commands.</summary>
public static class Settings
{
    public static IEnumerable<string> Lines(Game g)
    {
        yield return "// Hexen Sharp settings. Plain console commands; edit freely.";
        foreach (var l in g.Binds.ToCommands()) yield return l;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        yield return "sens " + g.Vars.Sens.ToString("0.##", inv);
        yield return "invertmouse " + (g.Vars.InvertMouse ? 1 : 0);
        yield return "fov " + g.Vars.Fov.ToString("0", inv);
        yield return "showfps " + (g.Vars.ShowFps ? 1 : 0);
        yield return "artstyle " + (Art.Style == ArtStyle.SciFi ? "scifi" : "fantasy");
        yield return "renderedart " + (Art.Rendered ? 1 : 0);
        yield return "hud " + (int)g.Vars.Hud;
        yield return "crosshair " + (int)g.Vars.Crosshair;
        yield return "quakemove " + (g.Vars.QuakeMove ? 1 : 0);
        yield return "ghost " + (g.Vars.Ghost ? 1 : 0);
        yield return "strafehelp " + g.Vars.StrafeHelp;
        yield return "arcade " + (g.Vars.Arcade ? 1 : 0);
        yield return "music " + g.Vars.Music.ToString("0.##", inv);
        yield return "shake " + g.Vars.Shake;
        yield return "hitstop " + (g.Vars.HitStop ? 1 : 0);
        yield return "damagenumbers " + (g.Vars.DamageNumbers ? 1 : 0);
        yield return "bossintros " + (g.Vars.BossIntros ? 1 : 0);
        yield return "dynamicmusic " + (g.Vars.DynamicMusic ? 1 : 0);
        yield return "padlook " + g.Vars.PadLook.ToString("0.##", inv);
        yield return "renderthreads " + Renderer.Threads;
        // a preset is saved; console tweaks to the damage settings last only the session
        if (Difficulties.Of(g.Vars) is var d && d != Difficulty.Custom) yield return "difficulty " + d.ToString().ToLowerInvariant();
        yield return "name " + g.RunnerName;
        yield return "arenamods " + ArenaModInfo.Letters(g.ArenaMods);
    }

    public static void Save(Game g, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, Lines(g));
        }
        catch (Exception e) { g.Con.Print("could not save settings: " + e.Message); }
    }

    public static void Load(Game g, string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            foreach (var line in File.ReadAllLines(path))
                if (!line.TrimStart().StartsWith("//")) g.Con.Execute(line, quiet: true);
        }
        catch (Exception e) { g.Con.Print("could not load settings: " + e.Message); }
    }
}
