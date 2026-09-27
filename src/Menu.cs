namespace HexenSharp;

public enum MenuPage { Main, Pause, Options, Bindings, Style, Character }

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

    public MenuSystem(Game g) { _g = g; }

    public bool Open => Page != null;

    public void Show(MenuPage p)
    {
        if (Page != null) _back.Push((Page.Value, Cursor));
        Page = p; Cursor = 0; Column = 0; Scroll = 0; Capturing = false; NoticeTime = 0;
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
        MenuPage.Main => new[] { "New game", "Practice", "Character", "Options", "Quit" },
        MenuPage.Pause => new[] { "Resume", "Character", "Options", "Restart", "Quit to title", "Quit game" },
        MenuPage.Character => Profile.Skills.Select(SkillName).Append("Back").ToArray(),
        MenuPage.Style => new[] { "Classic", "Relaxed", "Back" },
        MenuPage.Options => new[] { "Key bindings", "Mouse sensitivity", "Invert mouse", "Field of view", "Show FPS", "Visual style", "Rendered art", "HUD style", "Crosshair", "Movement", "Back" },
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
                default: if (inp.Confirm) Back(); return;
            }
            _g.PlaySound(Sfx.Pickup, 0.6f);
            return;
        }

        if (!inp.Confirm) return;
        _g.PlaySound(Sfx.Item, 0.8f);
        switch (Page, Cursor)
        {
            case (MenuPage.Main, 0): Show(MenuPage.Style); Cursor = (int)_g.Style; break;
            case (MenuPage.Style, 0 or 1): _g.Style = (GameStyle)Cursor; _g.PendingPractice = false; Close(); _g.Mode = GameMode.ClassSelect; _g.MenuIndex = 0; break;
            case (MenuPage.Main, 1): _g.Style = GameStyle.Classic; _g.PendingPractice = true; Close(); _g.Mode = GameMode.ClassSelect; _g.MenuIndex = 0; break;
            case (MenuPage.Style, 2): Back(); break;
            case (MenuPage.Main, 2): Show(MenuPage.Character); break;
            case (MenuPage.Main, 3): Show(MenuPage.Options); break;
            case (MenuPage.Main, 4): _g.QuitRequested = true; break;
            case (MenuPage.Pause, 0): Close(); _g.Paused = false; break;
            case (MenuPage.Pause, 1): Show(MenuPage.Character); break;
            case (MenuPage.Pause, 2): Show(MenuPage.Options); break;
            case (MenuPage.Pause, 3): Close(); _g.NewGame(_g.P.Class); break;
            case (MenuPage.Pause, 4): Close(); _g.GoToTitle(); break;
            case (MenuPage.Pause, 5): _g.QuitRequested = true; break;
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
