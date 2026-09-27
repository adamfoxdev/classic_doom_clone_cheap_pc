namespace HexenSharp;

public enum MenuPage { Main, Pause, Options, Bindings, Style, Character, Leaderboard, Courses }

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
    public const int BindRows = 15;

    /// <summary>Whose leaderboard the Leaderboard page shows: Left/Right switch class, Up/Down the course.</summary>
    public PClass BoardClass;
    public Course BoardCourse = Courses.Hangar;
    /// <summary>The Leaderboard page shows the arena's board (waves) rather than a course's (times).</summary>
    public bool BoardArena;

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
            BoardArena = _g.ArenaMode;
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
        if (Page is MenuPage.Options or MenuPage.Bindings) _g.SaveSettings();
        if (_back.Count > 0) { (Page, Cursor) = _back.Pop(); Column = 0; Capturing = false; return; }
        if (Page is MenuPage.Pause or MenuPage.Character) { Close(); _g.Paused = false; }
    }

    void Say(string s) { Notice = s; NoticeTime = 3f; }

    /// <summary>A skill's name in the current style (the jetpack is the Wings of Wrath in fantasy).</summary>
    public static string SkillName(Skill s) => s == Skill.Thrusters && Art.Style == ArtStyle.Fantasy ? "Wings" : s.ToString();

    // ------------------------------------------------------------ page contents

    public string[] Items(MenuPage p) => p switch
    {
        MenuPage.Main => new[] { "New game", "Practice", "Arena", "Leaderboard", "Character", "Options", "Quit" },
        MenuPage.Pause => _g.Practicing
            ? new[] { "Resume", _g.Demo ? "Stop demo" : "Watch demo", "Character", "Leaderboard", "Options", "Restart", "Quit to title", "Quit game" }
            : _g.ArenaMode
            ? new[] { "Resume", "Character", "Leaderboard", "Options", "Restart", "Quit to title", "Quit game" }
            : new[] { "Resume", "Character", "Options", "Restart", "Quit to title", "Quit game" },
        MenuPage.Leaderboard => new[] { "Back" },
        MenuPage.Courses => Courses.All.Select(c => c.Name).Append("Back").ToArray(),
        MenuPage.Character => Profile.Skills.Select(SkillName).Append("Back").ToArray(),
        MenuPage.Style => new[] { "Classic", "Relaxed", "Back" },
        MenuPage.Options => new[] { "Key bindings", "Mouse sensitivity", "Invert mouse", "Field of view", "Show FPS", "Visual style", "Rendered art", "HUD style", "Crosshair", "Movement", "Practice ghost", "Strafe helper", "Back" },
        _ => Bindings.All.Select(b => b.Label).Concat(new[] { "Reset to defaults", "Back" }).ToArray(),
    };

    /// <summary>Right-hand value shown next to an options item.</summary>
    public string Value(int item)
    {
        var v = _g.Vars;
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
            if (Cursor >= Profile.Skills.Length) { if (inp.Confirm) Back(); return; }
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
                BoardClass = (PClass)(((int)BoardClass + (inp.Left ? 2 : 1)) % 3);
                _g.PlaySound(Sfx.Swing, 0.5f);
            }
            if (inp.Up || inp.Down)
            {
                // the timed courses, then the arena, round and round
                var timed = Courses.Timed;
                int n = timed.Length + 1, i = BoardArena ? timed.Length : Array.IndexOf(timed, BoardCourse);
                i = (i + (inp.Up ? n - 1 : 1)) % n;
                BoardArena = i == timed.Length;
                if (!BoardArena) BoardCourse = timed[i];
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
                default: if (inp.Confirm) Back(); return;
            }
            _g.PlaySound(Sfx.Pickup, 0.6f);
            return;
        }

        if (!inp.Confirm) return;
        _g.PlaySound(Sfx.Item, 0.8f);
        if (Page == MenuPage.Main)
        {
            switch (items[Cursor])
            {
                case "New game": Show(MenuPage.Style); Cursor = (int)_g.Style; break;
                case "Practice": Show(MenuPage.Courses); break;
                case "Arena":
                    _g.Style = GameStyle.Classic; _g.PendingArena = true; _g.PendingPractice = false;
                    Close(); _g.Mode = GameMode.ClassSelect; _g.MenuIndex = 0;
                    break;
                case "Leaderboard": Show(MenuPage.Leaderboard); break;
                case "Character": Show(MenuPage.Character); break;
                case "Options": Show(MenuPage.Options); break;
                case "Quit": _g.QuitRequested = true; break;
            }
            return;
        }
        switch (Page, Cursor)
        {
            case (MenuPage.Style, 0 or 1): _g.Style = (GameStyle)Cursor; _g.PendingPractice = _g.PendingArena = false; Close(); _g.Mode = GameMode.ClassSelect; _g.MenuIndex = 0; break;
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
        yield return "name " + g.RunnerName;
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
