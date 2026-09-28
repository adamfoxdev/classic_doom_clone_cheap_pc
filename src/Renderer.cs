namespace HexenSharp;

/// <summary>
/// Pure software renderer into a 320x200 framebuffer: a grid raycaster with textured floors and
/// ceilings, sky, fog, rising doors / see-through gates, depth-buffered sprites, HUD and automap.
/// </summary>
public sealed class Renderer
{
    public const int W = 320, H = 200, HudH = 32, StatusViewH = H - HudH;

    /// <summary>Rows of 3D view this frame: above the status bar, or the whole screen when the HUD style drops it.</summary>
    public int ViewH { get; private set; } = StatusViewH;
    float PlaneLen = 0.75f;                    // set from the fov setting each frame
    float Proj = (W / 2f) / 0.75f;             // pixels per world unit at distance 1
    float _fogDist;
    bool _fullBright;

    public readonly uint[] Fb = new uint[W * H];
    /// <summary>Distance of whatever was drawn at a 3D-view pixel (for tests).</summary>
    public float DepthAt(int x, int y) => _depth[y * W + x];
    readonly float[] _depth = new float[W * H];
    readonly List<(float d, int side, float wallX, int cell)> _doors = new();
    readonly List<(Thing t, float depth)> _sprites = new();

    // per-frame camera
    float _px, _py, _dirX, _dirY, _plX, _plY, _eyeZ, _horizon;
    Theme _theme;

    public void Render(Game g)
    {
        ViewH = g.Vars.Hud == HudStyle.Full || g.Mode is GameMode.Title or GameMode.ClassSelect or GameMode.Victory ? StatusViewH : H;
        switch (g.Mode)
        {
            case GameMode.Title:
                if (g.Menu.Page is null or MenuPage.Main) DrawTitle(g); else StoneBackdrop(g.Time);
                break;
            case GameMode.ClassSelect: DrawClassSelect(g); break;
            case GameMode.Victory: DrawVictory(g); break;
            default: DrawGame(g); break;
        }
        if (g.Menu.Open && g.Menu.Page != MenuPage.Main) DrawMenu(g);
        if (g.Con.Open) DrawConsole(g);
    }

    void DrawGame(Game g)
    {

        DrawView(g);
        DrawWeapon(g);
        DrawScreenTint(g);
        DrawCrosshair(g);
        if (g.Vars.Hud != HudStyle.Off && !g.ShowMap) DrawStrafeHelper(g);
        if (g.ShowMap) DrawAutomap(g);
        DrawHud(g);
        DrawMessages(g);
        if (g.ReadingLore != null) DrawLore(g);
        DrawArenaHud(g);
        DrawAchievementBanner(g);
        if (g.Story != null) DrawStory(g);
        if (g.Vars.Arcade && !g.ShowMap) DrawArcade(g);

        if (g.Mode == GameMode.Dead && g.P.EyeZ <= 0.13f)
            CenterText("YOU DIED", 60, Col.Rgb(220, 40, 30), 3);
        if (g.Mode == GameMode.Dead && g.P.EyeZ <= 0.13f)
            CenterText(g.CanRespawn ? "PRESS ENTER TO RETURN TO THE CHECKPOINT" : "PRESS ENTER TO TRY AGAIN", 90, Col.Rgb(230, 220, 200));
        if (g.Vars.ShowFps) Text(W - 40, 3, $"{g.Fps:0} FPS", Col.Rgb(120, 255, 120));
    }

    // ================================================================ menus

    static readonly uint MenuSel = Col.Rgb(255, 220, 90), MenuText = Col.Rgb(200, 190, 170), MenuDim = Col.Rgb(150, 140, 120);

    /// <summary>Where the pause and options lists sit: first row, row spacing and the footer line under them.</summary>
    public const int TitleTop = 118, TitleRow = 9, TitleFooter = 192, PauseTop = 60, PauseRow = 12, PauseFooter = 160, OptionsTop = 26, OptionsRow = 10, OptionsFooter = 188;

    void MenuItem(string text, int y, bool selected)
    {
        text = text.ToUpperInvariant();
        int x = (W - Font.Width(text)) / 2;
        if (selected)
        {
            Rect(x - 12, y - 2, Font.Width(text) + 22, 11, Col.Rgb(70, 40, 20));
            Text(x - 9, y, ">", MenuSel);
        }
        Text(x, y, text, selected ? MenuSel : MenuText);
    }

    void DrawMenu(Game g)
    {
        var m = g.Menu;
        var page = m.Page.Value;
        var items = m.Items(page);
        if (g.Mode != GameMode.Title) Darken(0, 0, W, H, page == MenuPage.Pause ? 150 : page is MenuPage.Character or MenuPage.Leaderboard or MenuPage.Achievements ? 246 : 230);

        switch (page)
        {
            case MenuPage.Pause:
                CenterText("PAUSED", 30, Col.Rgb(230, 190, 80), 3);
                for (int i = 0; i < items.Length; i++) MenuItem(items[i], PauseTop + i * PauseRow, i == m.Cursor);
                CenterText("ARROWS + ENTER    ESC: RESUME", PauseFooter, MenuDim);
                break;

            case MenuPage.Options:
                CenterText("OPTIONS", 8, Col.Rgb(230, 190, 80), 2);
                for (int i = 0; i < items.Length; i++)
                {
                    int y = OptionsTop + i * OptionsRow;
                    bool sel = i == m.Cursor;
                    string label = items[i].ToUpperInvariant(), val = m.Value(i);
                    if (val == "") { MenuItem(label, y, sel); continue; }
                    if (sel) Rect(40, y - 1, 240, 10, Col.Rgb(70, 40, 20)); // rows are 10 apart
                    Text(48, y, label, sel ? MenuSel : MenuText);
                    string shown = sel ? $"< {val} >" : val;
                    Text(272 - Font.Width(shown), y, shown, sel ? MenuSel : Col.Rgb(170, 200, 255));
                }
                // a notice takes the footer's place, clear of the list
                if (m.NoticeTime <= 0) CenterText("UP/DOWN: SELECT  LEFT/RIGHT: CHANGE  ESC: BACK", OptionsFooter, MenuDim);
                else CenterText(m.Notice.ToUpperInvariant(), OptionsFooter, Col.Rgb(120, 255, 140));
                break;

            case MenuPage.Style:
                CenterText("CHOOSE A STYLE", 24, Col.Rgb(230, 190, 80), 2);
                for (int i = 0; i < items.Length; i++) MenuItem(items[i], 64 + i * 16, i == m.Cursor);
                string[] about = m.Cursor switch
                {
                    0 => new[] { Words.T("FIGHT YOUR WAY THROUGH THE HUB,"), Words.T("SOLVE ITS PUZZLES AND SLAY THE HERESIARCH.") },
                    1 => new[] { Words.T("NO COMBAT: THE CREATURES ARE PEACEFUL."), Words.T("EXPLORE, READ LORE STONES, UNCOVER SECRETS"), Words.T("AND FIND THE HIDDEN RELICS.") },
                    _ => Array.Empty<string>(),
                };
                for (int i = 0; i < about.Length; i++) CenterText(about[i], 128 + i * 10, Col.Rgb(170, 200, 255));
                CenterText("ARROWS + ENTER    ESC: BACK", 186, MenuDim);
                break;

            case MenuPage.Bindings:
                DrawBindings(g);
                break;

            case MenuPage.Character:
                DrawCharacter(g);
                break;

            case MenuPage.Achievements:
                DrawAchievements(g);
                break;

            case MenuPage.Leaderboard:
                DrawLeaderboard(g);
                break;

            case MenuPage.ArenaSetup:
                DrawArenaSetup(g);
                break;

            case MenuPage.Courses:
                CenterText("PRACTICE", 14, Col.Rgb(230, 190, 80), 2);
                for (int i = 0; i < items.Length; i++) MenuItem(items[i], 44 + i * 14, i == m.Cursor);
                if (m.Cursor < Courses.All.Length)
                {
                    var course = Courses.All[m.Cursor];
                    foreach (var (line, k) in Wrap(course.About, 50).Select((l, k) => (l, k))) CenterText(line, 116 + k * 10, Col.Rgb(170, 200, 255));
                    if (course.Timed) DrawMedalTable(g, course, 140);
                }
                CenterText("ARROWS + ENTER    ESC: BACK", 186, MenuDim);
                break;
        }

        if (m.NoticeTime > 0 && page is not (MenuPage.Bindings or MenuPage.Character or MenuPage.Leaderboard or MenuPage.Options or MenuPage.ArenaSetup or MenuPage.Achievements))
            CenterText(m.Notice.ToUpperInvariant(), 166, Col.Rgb(120, 255, 140));
    }

    /// <summary>The character screen: level and experience, skills to spend points on, and your weapons' levels.</summary>
    /// <summary>The practice course leaderboard for one class: the ten fastest runs, the latest one picked out.</summary>
    void DrawLeaderboard(Game g)
    {
        var m = g.Menu;
        uint gold = Col.Rgb(230, 190, 80), blue = Col.Rgb(170, 200, 255), fresh = Col.Rgb(120, 255, 140);
        CenterText("LEADERBOARD", 6, gold, 2);
        var def = ClassDef.All[(int)m.BoardClass];
        if (m.BoardArena) { DrawArenaBoard(g, def); return; }
        CenterText($"{m.BoardCourse.Name.ToUpperInvariant()}   < {def.Name.ToUpperInvariant()} >", 26, blue);
        var (tg, ts, tb) = m.BoardCourse.MedalTimes(m.BoardClass);
        string targets = $"GOLD {tg:0.0}   SILVER {ts:0.0}   BRONZE {tb:0.0}";
        int tx = (W - Font.Width(targets)) / 2;
        Text(tx, 36, $"GOLD {tg:0.0}", Medals.Colour(Medal.Gold));
        Text(tx + Font.Width($"GOLD {tg:0.0}   "), 36, $"SILVER {ts:0.0}", Medals.Colour(Medal.Silver));
        Text(tx + Font.Width($"GOLD {tg:0.0}   SILVER {ts:0.0}   "), 36, $"BRONZE {tb:0.0}", Medals.Colour(Medal.Bronze));
        var runs = g.Profile.Board(m.BoardCourse.Key(m.BoardClass));
        if (runs.Count == 0)
        {
            CenterText("NO RUNS YET.", 70, MenuText);
            CenterText("PICK PRACTICE ON THE TITLE MENU TO SET A TIME.", 82, MenuDim);
        }
        else
        {
            var latest = runs.MaxBy(r => r.When);
            Text(34, 48, "#", MenuDim); Text(64, 48, "TIME", MenuDim); Text(128, 48, "NAME", MenuDim); Text(224, 48, "DATE", MenuDim);
            for (int i = 0; i < runs.Count; i++)
            {
                var r = runs[i];
                int y = 59 + i * 11;
                MedalDot(52, y + 1, m.BoardCourse.MedalFor(m.BoardClass, r.Time));
                uint c = r == latest && r.When != default ? fresh : i == 0 ? gold : MenuText;
                Text(34, y, $"{i + 1,2}", c);
                Text(64, y, $"{r.Time:0.00}", c);
                Text(128, y, r.Name, c);
                Text(224, y, r.When == default ? "-" : r.When.ToString("yyyy-MM-dd"), c);
            }
        }
        CenterText($"NAME: {g.RunnerName}  (CHANGE WITH 'NAME' IN THE CONSOLE)", 172, MenuDim);
        CenterText("LEFT/RIGHT: CLASS   UP/DOWN: BOARD   ESC: BACK", 186, MenuDim);
    }

    /// <summary>The arena leaderboard for one class: the runs that cleared the most waves, then the quickest.</summary>
    void DrawArenaBoard(Game g, ClassDef def)
    {
        var m = g.Menu;
        uint gold = Col.Rgb(230, 190, 80), blue = Col.Rgb(170, 200, 255), fresh = Col.Rgb(120, 255, 140);
        CenterText($"{Words.T("CHAOS ARENA")}   < {def.Name.ToUpperInvariant()} >", 26, blue);
        DrawArenaTargets(36);
        var runs = g.Profile.ArenaBoard(m.BoardClass);
        if (runs.Count == 0)
        {
            CenterText("NO RUNS YET.", 70, MenuText);
            CenterText("PICK ARENA ON THE TITLE MENU AND CLEAR A WAVE.", 82, MenuDim);
        }
        else
        {
            var latest = runs.MaxBy(r => r.When);
            Text(18, 48, "#", MenuDim); Text(46, 48, "SCORE", MenuDim); Text(84, 48, "WV", MenuDim); Text(104, 48, "TIME", MenuDim);
            Text(142, 48, "MODS", MenuDim); Text(172, 48, "NAME", MenuDim); Text(250, 48, "DATE", MenuDim);
            for (int i = 0; i < runs.Count; i++)
            {
                var r = runs[i];
                int y = 59 + i * 11;
                MedalDot(36, y + 1, ArenaMedals.For(r.Waves));
                uint c = r == latest && r.When != default ? fresh : i == 0 ? gold : MenuText;
                Text(18, y, $"{i + 1,2}", c);
                Text(46, y, $"{r.Score,5}", c);
                Text(84, y, $"{r.Waves,2}", c);
                Text(104, y, $"{r.Time:0.0}", c);
                Text(142, y, ArenaModInfo.Letters((ArenaMod)r.Mods, (Difficulty)r.Difficulty), c);
                Text(172, y, r.Name, c);
                Text(250, y, r.When == default ? "-" : r.When.ToString("yyyy-MM-dd"), c);
            }
        }
        CenterText("S FAST FOES  N NO SUPPLIES  M MELEE  R RANDOM", 166, MenuDim);
        CenterText("E EASY (SCORE X0.5)   X NIGHTMARE (SCORE X1.5)", 175, MenuDim);
        CenterText("LEFT/RIGHT: CLASS   UP/DOWN: BOARD   ESC: BACK", 186, MenuDim);
    }

    /// <summary>The arena's setup page: the modifiers to switch on, what each does, and the score multiplier they make.</summary>
    void DrawArenaSetup(Game g)
    {
        var m = g.Menu;
        var items = m.Items(MenuPage.ArenaSetup);
        uint gold = Col.Rgb(230, 190, 80), blue = Col.Rgb(170, 200, 255), on = Col.Rgb(120, 255, 140);
        CenterText(Words.T("CHAOS ARENA"), 10, gold, 2);
        CenterText("MODIFIERS MAKE A RUN HARDER AND RAISE ITS SCORE", 30, MenuDim);
        int n = ArenaModInfo.All.Length;
        for (int i = 0; i < items.Length; i++)
        {
            int y = 46 + i * 13 + (i >= n ? 6 : 0);
            bool sel = i == m.Cursor;
            if (i >= n) { MenuItem(items[i], y, sel); continue; }
            var mod = ArenaModInfo.All[i];
            bool set = (g.ArenaMods & mod) != 0;
            if (sel) Rect(40, y - 2, 240, 11, Col.Rgb(70, 40, 20));
            Text(48, y, items[i].ToUpperInvariant(), sel ? MenuSel : MenuText);
            string val = set ? $"ON  +{ArenaModInfo.Bonus(mod) * 100:0}%" : "OFF";
            Text(272 - Font.Width(val), y, val, set ? on : sel ? MenuSel : MenuDim);
        }
        if (m.Cursor < n)
            foreach (var (line, k) in Wrap(ArenaModInfo.About(ArenaModInfo.All[m.Cursor]), 50).Select((l, k) => (l, k)))
                CenterText(line, 132 + k * 10, blue);
        var diff = Difficulties.Of(g.Vars);
        CenterText(diff == Difficulty.Custom ? "CUSTOM DIFFICULTY: RUNS AREN'T RECORDED"
            : $"SCORE: 100 A WAVE  X{ArenaModInfo.Multiplier(g.ArenaMods) * Difficulties.ScoreFactor(diff):0.00}" + (diff == Difficulty.Normal ? "" : $"  ({Difficulties.Name(diff)})"), 156, gold);
        DrawArenaTargets(168);
        CenterText("ENTER/LEFT/RIGHT: SWITCH   ESC: BACK", 186, MenuDim);
    }

    /// <summary>After a boss wave: the three perks to choose from, with what each does.</summary>
    void DrawPerkOffer(Game g, ArenaState a)
    {
        uint gold = Col.Rgb(230, 190, 80), blue = Col.Rgb(170, 200, 255);
        // left of the perk list in the top-right corner, which stays in view
        const int left = 4, width = 270;
        int top = 36, h = 20 + a.Offer.Length * 24;
        void Line(string t, int y, uint c) => Text(left + (width - Font.Width(t)) / 2, y, t, c);
        Darken(left, top, width, h, 200);
        Line("CHOOSE A PERK: 1, 2, 3 OR ARROWS + ENTER", top + 5, gold);
        for (int i = 0; i < a.Offer.Length; i++)
        {
            var perk = a.Offer[i];
            int y = top + 20 + i * 24, rank = a.Rank(perk) + 1;
            if (i == a.OfferCursor) Rect(left + 6, y - 3, width - 12, 22, Col.Rgb(70, 40, 20));
            Line($"{i + 1}  {PerkInfo.Name(perk).ToUpperInvariant()} {PerkInfo.Roman(rank)}", y, Col.Rgb(255, 230, 120));
            Line(PerkInfo.About(perk).ToUpperInvariant(), y + 10, blue);
        }
    }

    /// <summary>The arena's medal targets on one line, each in its medal's colour.</summary>
    void DrawArenaTargets(int y)
    {
        string g1 = $"GOLD WAVE {ArenaMedals.Gold}", s1 = $"SILVER WAVE {ArenaMedals.Silver}", b1 = $"BRONZE WAVE {ArenaMedals.Bronze}";
        int x = (W - Font.Width($"{g1}   {s1}   {b1}")) / 2;
        Text(x, y, g1, Medals.Colour(Medal.Gold));
        Text(x + Font.Width(g1 + "   "), y, s1, Medals.Colour(Medal.Silver));
        Text(x + Font.Width($"{g1}   {s1}   "), y, b1, Medals.Colour(Medal.Bronze));
    }

    /// <summary>For each class on a course: the medal your best time earned, the time, and the targets.</summary>
    void DrawMedalTable(Game g, Course course, int y)
    {
        Text(110, y, "BEST", MenuDim); Text(160, y, "GOLD", MenuDim); Text(206, y, "SILVER", MenuDim); Text(252, y, "BRONZE", MenuDim);
        y += 11;
        foreach (var cls in Enum.GetValues<PClass>())
        {
            float best = g.Profile.CourseBestTime(course.Key(cls));
            var medal = course.MedalFor(cls, best);
            var (tg, ts, tb) = course.MedalTimes(cls);
            MedalDot(30, y + 1, medal);
            Text(42, y, ClassDef.All[(int)cls].Name.ToUpperInvariant(), MenuText);
            Text(110, y, best > 0 ? $"{best:0.00}" : "-", best <= 0 ? MenuDim : medal == Medal.None ? MenuText : Medals.Colour(medal));
            Text(160, y, $"{tg:0.0}", Medals.Colour(Medal.Gold));
            Text(206, y, $"{ts:0.0}", Medals.Colour(Medal.Silver));
            Text(252, y, $"{tb:0.0}", Medals.Colour(Medal.Bronze));
            y += 10;
        }
    }

    void DrawCharacter(Game g)
    {
        var m = g.Menu;
        var pr = g.Profile;
        var items = m.Items(MenuPage.Character);
        uint gold = Col.Rgb(230, 190, 80), blue = Col.Rgb(170, 200, 255);
        CenterText("CHARACTER", 6, gold, 2);
        string xp = pr.Level >= Profile.MaxLevel ? "MAX LEVEL" : $"XP {pr.Xp}/{Profile.XpToNext(pr.Level)}";
        CenterText($"LEVEL {pr.Level}    {xp}    POINTS {pr.Points}", 26, pr.Points > 0 ? Col.Rgb(120, 255, 140) : blue);
        Bar(60, 36, 200, 3, pr.Level >= Profile.MaxLevel ? 1f : pr.Xp / (float)Profile.XpToNext(pr.Level), gold);

        for (int i = 0; i < items.Length; i++)
        {
            int y = 44 + i * 12;
            bool sel = i == m.Cursor;
            if (i >= Profile.Skills.Length)
            {
                // Achievements (with how many you've got) and Back
                string label = items[i] == "Achievements" ? $"ACHIEVEMENTS  {pr.Achievements.Count}/{Achievements.All.Length}" : items[i];
                MenuItem(label, y + 2, sel);
                continue;
            }
            var s = Profile.Skills[i];
            int rank = pr.Rank(s);
            if (sel) Rect(14, y - 2, 292, 11, Col.Rgb(70, 40, 20));
            Text(20, y, items[i].ToUpperInvariant(), sel ? MenuSel : MenuText);
            for (int k = 0; k < Profile.MaxRank; k++)
                Rect(96 + k * 8, y, 6, 6, k < rank ? (sel ? MenuSel : gold) : Col.Rgb(60, 52, 44));
            Text(180, y, rank > 0 ? Profile.Effect(s, rank) : "-", rank > 0 ? blue : MenuDim);
        }

        var cls = g.P?.Class ?? PClass.Fighter;
        var def = ClassDef.All[(int)cls];
        Text(20, 130, $"WEAPONS ({def.Name.ToUpperInvariant()})", MenuDim);
        for (int slot = 0; slot < 3; slot++)
        {
            var w = pr.Weapon(cls, slot);
            int y = 141 + slot * 10;
            string name = def.Weapons[slot].Name.ToUpperInvariant();
            Text(20, y, name.Length > 22 ? name[..22] : name, MenuText);
            Text(160, y, $"LV {w.Level}", w.Level >= Profile.MaxWeaponLevel ? Col.Rgb(120, 255, 140) : blue);
            Text(198, y, $"+{(w.Level - 1) * 8}%", MenuDim);
            Bar(232, y + 2, 70, 3, w.Level >= Profile.MaxWeaponLevel ? 1f : w.Xp / (float)Profile.WeaponXpToNext(w.Level), gold);
        }
        if (m.NoticeTime > 0) CenterText(m.Notice.ToUpperInvariant(), 174, Col.Rgb(120, 255, 140));
        else CenterText("ENTER: SPEND A POINT    ESC: BACK", 174, MenuDim);
        CenterText($"KILLS {pr.TotalKills}    WINS {pr.Wins}    TOTAL XP {pr.TotalXp}", 186, MenuDim);
    }

    /// <summary>The achievements: each one's name and experience, ticked when earned, with the highlighted one's details below.</summary>
    void DrawAchievements(Game g)
    {
        var m = g.Menu;
        var pr = g.Profile;
        var all = Achievements.All;
        uint gold = Col.Rgb(230, 190, 80), blue = Col.Rgb(170, 200, 255), done = Col.Rgb(120, 255, 140);
        CenterText("ACHIEVEMENTS", 6, gold, 2);
        int earned = all.Where(a => pr.Achievements.ContainsKey(a.Id)).Sum(a => a.Xp);
        CenterText($"UNLOCKED {pr.Achievements.Count}/{all.Length}    XP {earned}/{all.Sum(a => a.Xp)}", 26, blue);
        for (int r = 0; r < MenuSystem.AchievementRows && m.Scroll + r < all.Length; r++)
        {
            int i = m.Scroll + r, y = 40 + r * 11;
            var a = all[i];
            bool got = pr.Achievements.ContainsKey(a.Id), sel = i == m.Cursor;
            if (sel) Rect(12, y - 2, 296, 11, Col.Rgb(70, 40, 20));
            Rect(16, y, 7, 7, Col.Rgb(20, 18, 16));
            if (got) Rect(17, y + 1, 5, 5, done);
            Text(30, y, a.Name.ToUpperInvariant(), sel ? MenuSel : got ? gold : MenuText);
            string xp = $"+{a.Xp} XP";
            Text(302 - Font.Width(xp), y, xp, got ? done : MenuDim);
        }
        if (m.Scroll > 0) Text(302, 32, "^", MenuDim);
        if (m.Scroll + MenuSystem.AchievementRows < all.Length) Text(302, 160, "V", MenuDim);
        if (m.Cursor < all.Length)
        {
            var a = all[m.Cursor];
            CenterText(a.About.ToUpperInvariant(), 164, blue);
            string status = pr.Achievements.TryGetValue(a.Id, out var when) ? $"UNLOCKED {when:yyyy-MM-dd}"
                : a.Progress?.Invoke(g) is var (have, need) ? $"{Math.Min(have, need)} / {need}" : "NOT YET";
            CenterText(status, 174, pr.Achievements.ContainsKey(a.Id) ? done : MenuDim);
        }
        else MenuItem("Back", 168, true);
        CenterText("UP/DOWN: BROWSE    ESC: BACK", 188, MenuDim);
    }

    /// <summary>A freshly unlocked achievement, for a few seconds under the top of the view.</summary>
    void DrawAchievementBanner(Game g)
    {
        if (g.AchievementTime <= 0 || g.AchievementBanner is not { } a) return;
        string name = a.Name.ToUpperInvariant(), xp = $"+{a.Xp} XP";
        int w = Math.Max(Font.Width("ACHIEVEMENT UNLOCKED"), Font.Width(name)) + 24, x = (W - w) / 2, y = 56;
        Darken(x, y, w, 30, 210);
        Rect(x, y, w, 1, Col.Rgb(230, 190, 80)); Rect(x, y + 29, w, 1, Col.Rgb(230, 190, 80));
        CenterText("ACHIEVEMENT UNLOCKED", y + 4, Col.Rgb(230, 190, 80));
        CenterText(name, y + 13, Col.Rgb(250, 245, 230));
        CenterText(xp, y + 22 - 1, Col.Rgb(120, 255, 140));
    }

    void Bar(int x, int y, int w, int h, float fill, uint color)
    {
        Rect(x - 1, y - 1, w + 2, h + 2, Col.Rgb(20, 18, 16));
        int f = (int)MathF.Round(w * Math.Clamp(fill, 0f, 1f));
        if (f > 0) Rect(x, y, f, h, color);
    }

    void DrawBindings(Game g)
    {
        var m = g.Menu;
        var items = m.Items(MenuPage.Bindings);
        CenterText("KEY BINDINGS", 6, Col.Rgb(230, 190, 80), 2);
        const int colA = 14, colP = 150, colS = 232, top = 38, row = 9;
        Text(colA, 28, "ACTION", MenuDim);
        Text(colP, 28, "PRIMARY", MenuDim);
        Text(colS, 28, "SECONDARY", MenuDim);
        Rect(10, 36, W - 20, 1, Col.Rgb(120, 90, 50));

        for (int r = 0; r < MenuSystem.BindRows && m.Scroll + r < items.Length; r++)
        {
            int i = m.Scroll + r, y = top + r * row;
            bool sel = i == m.Cursor;
            if (i >= Bindings.Count)
            {
                MenuItem(items[i], y, sel);
                continue;
            }
            var b = Bindings.All[i];
            if (sel) Rect(10, y - 1, W - 20, row, Col.Rgb(55, 32, 16));
            Text(colA, y, b.Label.ToUpperInvariant(), sel ? MenuSel : MenuText);
            for (int s = 0; s < Bindings.Slots; s++)
            {
                int x = s == 0 ? colP : colS;
                bool cell = sel && m.Column == s;
                string key = cell && m.Capturing ? "PRESS A KEY" : Keys.Name(g.Binds.Get(b.Act, s));
                if (cell) Rect(x - 3, y - 1, 78, row, m.Capturing ? Col.Rgb(140, 40, 30) : Col.Rgb(110, 70, 30));
                uint kc = cell ? Col.Rgb(255, 255, 255) : g.Binds.Get(b.Act, s) == Keys.None ? Col.Rgb(100, 90, 80) : Col.Rgb(170, 200, 255);
                if (!(cell && m.Capturing && ((int)(g.Time * 3) & 1) == 1)) Text(x, y, key, kc);
            }
        }
        // scroll markers
        if (m.Scroll > 0) Text(W - 12, top, "^", MenuSel);
        if (m.Scroll + MenuSystem.BindRows < items.Length) Text(W - 12, top + (MenuSystem.BindRows - 1) * row, "V", MenuSel);

        int fy = top + MenuSystem.BindRows * row + 3;
        Rect(10, fy - 2, W - 20, 1, Col.Rgb(120, 90, 50));
        if (m.NoticeTime > 0) CenterText(m.Notice.ToUpperInvariant(), fy + 2, Col.Rgb(120, 255, 140));
        string help = m.Capturing
            ? "PRESS A KEY OR MOUSE BUTTON.  ESC: CANCEL"
            : "ENTER: BIND  BKSP: CLEAR  L/R: SLOT  ESC: BACK";
        CenterText(help, H - 10, MenuDim);
    }

    // ================================================================ 3D view

    Tex WallTex(Level lv, char c, int cell)
    {
        switch (c)
        {
            case 'D': return Art.Door;
            case 'S': return Art.SteelDoor;
            case 'F': return Art.FireDoor;
            case 'P': return Art.Portcullis;
            case 'L': return lv.PulledLevers.Contains(cell) ? Art.LeverOn : Art.LeverOff;
            case 'X': return Art.Block;
            case Level.Rubble: return Art.RubbleCracked[lv.CrackStage(cell)];
            case 'N': case 'Q': case 'U': return Art.OreCracked[Level.OreIndex(c)][lv.CrackStage(cell)];
            case 'Z': return lv.Theme.Walls.TryGetValue(lv.SecretLook[cell], out var look) ? look : Art.Stone;
        }
        return lv.Theme.Walls.TryGetValue(c, out var t) ? t : Art.Stone;
    }

    int Vis(Theme th, float d) => _fullBright ? 256 : (int)(256 * Math.Clamp(1f - d / _fogDist, 0f, 1f));
    int Light(Theme th) => _fullBright ? 256 : th.Light;

    void DrawView(Game g)
    {
        var lv = g.Level;
        var th = lv.Theme;
        _theme = th;
        var p = g.P;
        _px = p.X; _py = p.Y;
        _dirX = MathF.Cos(p.Angle); _dirY = MathF.Sin(p.Angle);
        PlaneLen = MathF.Tan(g.Vars.Fov * MathF.PI / 360f);
        Proj = (W / 2f) / PlaneLen;
        _fogDist = th.FogDist * g.Vars.Fog;
        _fullBright = g.Vars.FullBright;
        _plX = -_dirY * PlaneLen; _plY = _dirX * PlaneLen;
        _eyeZ = MathF.Min(p.FloorZ + p.ViewZ + MathF.Sin(p.Bob) * 0.025f * p.BobAmount, lv.HeightAt(p.X, p.Y) - 0.05f);
        _horizon = ViewH / 2f + p.Pitch;
        uint fog = th.FogColor;
        int baseLight = Light(th);
        _hiCell = g.DigTarget is (var hx, var hy, _, _) ? hy * lv.W + hx : -1;
        _hiFace = g.DigTarget?.face ?? Level.Face.Wall;
        _hiSlot = lv.Dig && _hiFace == Level.Face.Wall;
        _hiLo = g.DigTarget?.slot ?? 0f;
        _hiGlow = 320 + (int)(35 * MathF.Sin(g.PlayTime * 6f));
        _dig = lv.Dig;
        _viewFloor = p.FloorZ;

        for (int x = 0; x < W; x++)
        {
            float camX = 2f * (x + 0.5f) / W - 1f;
            float rdx = _dirX + _plX * camX, rdy = _dirY + _plY * camX;
            int mapX = (int)MathF.Floor(_px), mapY = (int)MathF.Floor(_py);
            float ddx = MathF.Abs(1f / (rdx == 0 ? 1e-6f : rdx)), ddy = MathF.Abs(1f / (rdy == 0 ? 1e-6f : rdy));
            int stepX, stepY;
            float sideX, sideY;
            if (rdx < 0) { stepX = -1; sideX = (_px - mapX) * ddx; } else { stepX = 1; sideX = (mapX + 1f - _px) * ddx; }
            if (rdy < 0) { stepY = -1; sideY = (_py - mapY) * ddy; } else { stepY = 1; sideY = (mapY + 1f - _py) * ddy; }
            int side = 0;

            _doors.Clear();
            float rayAng = MathF.Atan2(rdy, rdx);
            // walk the ray cell by cell, front to back. Each open cell draws its ceiling at its own height;
            // where the next cell's ceiling is lower, the band of wall above the opening is drawn; the first
            // solid wall reaches up to the ceiling in front of it. clipTop tracks how far down the column
            // (from the top) has been drawn already, which is what hides farther, taller things.
            // Front to back, clipTop / clipBot track how much of the column is already drawn from the top
            // and from the bottom. Each open cell draws its ceiling and floor at its own heights; where the
            // next cell's ceiling is lower a band of wall hangs down, where its floor is higher a step face
            // (riser) rises; the first solid wall fills whatever is left between them.
            float clipTop = 0f, clipBot = ViewH;
            int curCell = lv.InBounds(mapX, mapY) ? mapY * lv.W + mapX : -1;
            float curH = curCell >= 0 ? lv.Heights[curCell] : Level.MinHeight;
            float curF = curCell >= 0 ? lv.Floors[curCell] : 0f;
            if (curCell >= 0) lv.Seen[curCell] = true;
            for (int guard = 0; guard < 128 && clipTop < clipBot - 0.5f; guard++)
            {
                if (sideX < sideY) { sideX += ddx; mapX += stepX; side = 0; }
                else { sideY += ddy; mapY += stepY; side = 1; }
                float d = MathF.Max(0.001f, side == 0 ? sideX - ddx : sideY - ddy);
                CeilingSpan(lv, x, curCell, curH, ref clipTop, clipBot, RowOf(curH, d), rdx, rdy, rayAng, baseLight);
                FloorSpan(lv, x, curCell, curF, clipTop, ref clipBot, RowOf(curF, d), rdx, rdy, baseLight);
                float wx = side == 0 ? _py + d * rdy : _px + d * rdx;
                wx -= MathF.Floor(wx);
                if (!lv.InBounds(mapX, mapY))
                {
                    WallSpan(x, Art.Stone, d, side, wx, curH, curF, clipTop, clipBot, rdx, rdy, baseLight);
                    break;
                }
                int ci = mapY * lv.W + mapX;
                lv.Seen[ci] = true;
                char c = lv.Cells[ci];
                bool door = Level.IsDoor(c);
                if (c != '\0' && !door)
                {
                    WallSpan(x, WallTex(lv, c, ci), d, side, wx, curH, curF, clipTop, clipBot, rdx, rdy, baseLight, c is 'L' or 'X' ? UpperTex(lv, ci) : null, Hi(ci, Level.Face.Wall));
                    break;
                }
                float newH = lv.Heights[ci], newF = lv.Floors[ci];
                if (newH < curH)
                {
                    // the ceiling steps down: a band of wall hangs over the opening
                    WallSpan(x, lv.Dig ? Art.RubbleCracked[lv.CrackStage(ci, Level.Face.Ceiling)] : UpperTex(lv, ci), d, side, wx, curH, newH, clipTop, clipBot, rdx, rdy, baseLight, null, Hi(ci, Level.Face.Ceiling));
                    clipTop = MathF.Max(clipTop, RowOf(newH, d));
                }
                if (newF > curF)
                {
                    // the floor steps up: the face of the step
                    WallSpan(x, lv.Dig ? Art.RubbleCracked[lv.CrackStage(ci, Level.Face.Floor)] : Art.StepRiser, d, side, wx, newF, curF, clipTop, clipBot, rdx, rdy, baseLight, null, Hi(ci, Level.Face.Floor));
                    clipBot = MathF.Min(clipBot, RowOf(newF, d));
                }
                if (door)
                {
                    float open = lv.DoorOpen[ci];
                    if (open <= 0f && c != 'P')
                    {
                        WallSpan(x, WallTex(lv, c, ci), d, side, wx, newH, newF, clipTop, clipBot, rdx, rdy, baseLight);
                        break;
                    }
                    if (open < 1f) _doors.Add((d, side, wx, ci));
                }
                curCell = ci;
                curH = newH;
                curF = newF;
            }

            // ---- doors and gates, far to near, depth tested
            for (int k = _doors.Count - 1; k >= 0; k--)
            {
                var (d, dside, dwx, ci) = _doors[k];
                char c = lv.Cells[ci];
                var dt = WallTex(lv, c, ci);
                float open = lv.DoorOpen[ci];
                int dtx = (int)(dwx * dt.W);
                if (dside == 0 && rdx < 0) dtx = dt.W - 1 - dtx;
                if (dside == 1 && rdy > 0) dtx = dt.W - 1 - dtx;
                dtx = Math.Clamp(dtx, 0, dt.W - 1);
                float s = Proj / d, df = lv.Floors[ci], dh = lv.Heights[ci] - df;
                float dTop = _horizon - (df + dh - _eyeZ) * s;
                float dBot = _horizon - (df + open * dh - _eyeZ) * s;
                int y0 = Math.Max(0, (int)MathF.Ceiling(dTop - 0.5f)), y1 = Math.Min(ViewH, (int)MathF.Ceiling(dBot - 0.5f));
                int dl = dside == 1 ? baseLight * 200 >> 8 : baseLight;
                int dv = Vis(th, d);
                for (int y = y0; y < y1; y++)
                {
                    int idx = y * W + x;
                    if (d >= _depth[idx]) continue;
                    float z = _eyeZ + (_horizon - (y + 0.5f)) / s;
                    float zp = z - df - open * dh;
                    float v = 1f - (zp - MathF.Floor(zp));
                    int ty = Math.Clamp((int)(v * dt.H), 0, dt.H - 1);
                    uint texel = dt.Px[ty * dt.W + dtx];
                    if (Col.A(texel) == 0) continue;
                    Fb[idx] = Col.Fog(texel, dl, dv, fog);
                    _depth[idx] = d;
                }
            }
        }

        DrawSprites(g);
    }

    /// <summary>Screen row where height z appears at distance d.</summary>
    float RowOf(float z, float d) => _horizon - (z - _eyeZ) * Proj / d;

    Tex UpperTex(Level lv, int cell) => lv.Theme.Walls.TryGetValue(lv.UpperLook[cell], out var t) ? t : Art.Stone;

    // the block your next swing would break: it glows, pulsing, with a bright outline round its face
    int _hiCell = -1, _hiGlow = 256;
    Level.Face _hiFace;
    // on a dig map only the slot of rubble that will open (from _hiLo up one storey) lights up
    bool _hiSlot;
    float _hiLo;
    static readonly uint HiEdge = Col.Rgb(255, 236, 140);
    bool Hi(int cell, Level.Face face) => cell == _hiCell && face == _hiFace;
    uint Highlight(uint c, bool edge) => edge ? Col.Lerp(c, HiEdge, 210) : Col.Shade(c, _hiGlow);

    // dig maps: rock below your feet darkens and warms the deeper it lies, and each layer of blocks reads apart
    bool _dig;
    float _viewFloor;
    static readonly uint DeepTint = Col.Rgb(110, 60, 34);
    uint Layered(uint c, float z, bool seam)
    {
        int layer = (int)MathF.Floor(z / Level.DigStep + 0.001f);
        int s = (layer & 1) == 0 ? 268 : 244;
        float depth = _viewFloor - z;
        if (depth <= 0.01f) return Col.Shade(c, s);
        int k = (int)MathF.Min(110 + depth * 60, 220);
        c = Col.Lerp(c, DeepTint, k * 3 / 4);
        s -= k / 3;
        return Col.Shade(c, seam ? s * 140 >> 8 : s);
    }
    static bool Seam(float z)
    {
        float f = z / Level.DigStep;
        f -= MathF.Floor(f);
        return f < 0.04f || f > 0.96f;
    }

    /// <summary>
    /// A vertical slice of wall between heights `bottom` and `top` at distance d, below clipTop.
    /// The texture repeats every unit of height, anchored to the floor.
    /// </summary>
    void WallSpan(int x, Tex tex, float d, int side, float wallX, float top, float bottom, float clipTop, float clipBot, float rdx, float rdy, int baseLight, Tex above = null, bool hi = false)
    {
        float s = Proj / d;
        float yT = MathF.Max(clipTop, RowOf(top, d)), yB = MathF.Min(clipBot, RowOf(bottom, d));
        int y0 = Math.Max(0, (int)MathF.Ceiling(yT - 0.5f)), y1 = Math.Min(ViewH, (int)MathF.Ceiling(yB - 0.5f));
        if (y0 >= y1) return;
        int tx = (int)(wallX * tex.W);
        if (side == 0 && rdx < 0) tx = tex.W - 1 - tx;
        if (side == 1 && rdy > 0) tx = tex.W - 1 - tx;
        tx = Math.Clamp(tx, 0, tex.W - 1);
        int light = side == 1 ? baseLight * 200 >> 8 : baseLight;
        int vis = Vis(_theme, d);
        float hiTop = hi && _hiSlot ? MathF.Min(top, _hiLo + Level.MinHeight) : top, hiBot = hi && _hiSlot ? MathF.Max(bottom, _hiLo) : bottom;
        for (int y = y0; y < y1; y++)
        {
            float z = _eyeZ + (_horizon - (y + 0.5f)) / s;
            float zr = z - bottom; // texture anchored to the wall's base
            float v = 1f - (zr - MathF.Floor(zr));
            var t = above != null && z >= bottom + 1f ? above : tex; // e.g. a lever only on the bottom storey
            int ty = Math.Clamp((int)(v * t.H), 0, t.H - 1);
            int idx = y * W + x;
            uint texel = t.Px[ty * t.W + Math.Min(tx, t.W - 1)];
            if (_dig) texel = Layered(texel, z, Seam(z));
            if (hi && z >= hiBot && z <= hiTop) texel = Highlight(texel, tx < 2 || tx >= tex.W - 2 || z > hiTop - 2.5f / s || z < hiBot + 2.5f / s);
            Fb[idx] = Col.Fog(texel, light, vis, _theme.FogColor);
            _depth[idx] = d;
        }
    }

    /// <summary>Ceiling (or sky, outdoors) of one cell, from clipTop down to row yEnd.</summary>
    void CeilingSpan(Level lv, int x, int cell, float h, ref float clipTop, float clipBot, float yEnd, float rdx, float rdy, float rayAng, int baseLight)
    {
        int y0 = Math.Max(0, (int)MathF.Ceiling(clipTop - 0.5f)), y1 = Math.Min((int)MathF.Ceiling(clipBot - 0.5f), (int)MathF.Ceiling(yEnd - 0.5f));
        y1 = Math.Min(y1, ViewH);
        bool outdoor = cell >= 0 && lv.Outdoor[cell];
        if (!outdoor && h <= _eyeZ + 0.001f) { clipTop = MathF.Max(clipTop, yEnd); return; }
        var ct = lv.Dig && cell >= 0 ? Art.RubbleCracked[lv.CrackStage(cell, Level.Face.Ceiling)] : _theme.CeilIn;
        bool hi = Hi(cell, Level.Face.Ceiling);
        for (int y = y0; y < y1; y++)
        {
            int idx = y * W + x;
            float dy = _horizon - (y + 0.5f);
            if (outdoor || dy <= 0.01f) { Fb[idx] = SkyPixel(_theme, rayAng, y); _depth[idx] = 999; continue; }
            float rowDist = (h - _eyeZ) * Proj / dy;
            float wx = _px + rdx * rowDist, wy = _py + rdy * rowDist;
            int u = (int)((wx - MathF.Floor(wx)) * ct.W) & (ct.W - 1), vv = (int)((wy - MathF.Floor(wy)) * ct.H) & (ct.H - 1);
            uint texel = ct.Px[vv * ct.W + u];
            if (_dig) texel = Layered(texel, h + 0.005f, false);
            if (hi) texel = Highlight(texel, u < 2 || u >= ct.W - 2 || vv < 2 || vv >= ct.H - 2);
            Fb[idx] = Col.Fog(texel, baseLight * 220 >> 8, Vis(_theme, rowDist), _theme.FogColor);
            _depth[idx] = rowDist;
        }
        clipTop = MathF.Max(clipTop, yEnd);
    }

    /// <summary>Floor of one cell at height f, from row yStart down to clipBot (only visible from above).</summary>
    void FloorSpan(Level lv, int x, int cell, float f, float clipTop, ref float clipBot, float yStart, float rdx, float rdy, int baseLight)
    {
        if (f >= _eyeZ - 0.001f) { clipBot = MathF.Min(clipBot, yStart); return; }
        int y0 = Math.Max(Math.Max(0, (int)MathF.Ceiling(clipTop - 0.5f)), (int)MathF.Ceiling(yStart - 0.5f));
        int y1 = Math.Min(ViewH, (int)MathF.Ceiling(clipBot - 0.5f));
        var th = _theme;
        Tex ft = th.FloorIn;
        int fl = baseLight;
        if (cell >= 0)
        {
            char mk = lv.Marks[cell];
            if (mk == 'E') { ft = lv.BossDead ? Art.ExitFloor : Art.ExitFloorOff; fl = 300; }
            else if (mk == '*') { ft = Art.SpawnFloor; fl = 280; }
            else if (mk == '^') ft = Art.PlateFloor;
            else if (mk == '+') { ft = lv.CheckpointsReached.Contains(lv.CheckpointZone[cell]) ? Art.CheckpointFloor : Art.CheckpointFloorOff; fl = 300; }
            else if (mk == '=') { ft = Art.LiftFloor; fl = 300; }
            else if (mk == '!') { ft = lv.Arena?.Started == true ? Art.AltarFloorOff : Art.AltarFloor; fl = 300; }
            else if (mk != '\0') { ft = Art.PortalFloor; fl = 300; }
            else if (lv.Outdoor[cell]) ft = th.OutdoorFloor;
            else if (lv.Dig) ft = Art.RubbleCracked[lv.CrackStage(cell, Level.Face.Floor)];
        }
        bool hi = Hi(cell, Level.Face.Floor);
        for (int y = y0; y < y1; y++)
        {
            float dy = y + 0.5f - _horizon;
            if (dy <= 0.01f) continue;
            float rowDist = (_eyeZ - f) * Proj / dy;
            float wx = _px + rdx * rowDist, wy = _py + rdy * rowDist;
            int u = (int)((wx - MathF.Floor(wx)) * ft.W) & (ft.W - 1), vv = (int)((wy - MathF.Floor(wy)) * ft.H) & (ft.H - 1);
            int idx = y * W + x;
            uint texel = ft.Px[vv * ft.W + u];
            if (_dig && cell >= 0 && lv.Marks[cell] == '\0') texel = Layered(texel, f - 0.005f, false);
            if (hi) texel = Highlight(texel, u < 2 || u >= ft.W - 2 || vv < 2 || vv >= ft.H - 2);
            Fb[idx] = Col.Fog(texel, fl, Vis(th, rowDist), th.FogColor);
            _depth[idx] = rowDist;
        }
        clipBot = MathF.Min(clipBot, yStart);
    }

    uint SkyPixel(Theme th, float rayAng, int y)
    {
        var sky = th.Sky;
        int u = (int)(rayAng / MathF.Tau * sky.W * 4) % sky.W;
        if (u < 0) u += sky.W;
        int v = Math.Clamp(sky.H - 1 + (int)((y + 0.5f - _horizon) * 0.8f), 0, sky.H - 1);
        return sky.Px[v * sky.W + u];
    }

    void DrawSprites(Game g)
    {
        var lv = g.Level;
        var th = lv.Theme;
        _sprites.Clear();
        float invDet = 1f / (_plX * _dirY - _dirX * _plY);
        foreach (var t in lv.Things)
        {
            if (t.Removed) continue;
            float rx = t.X - _px, ry = t.Y - _py;
            float ty = invDet * (-_plY * rx + _plX * ry);
            if (ty < 0.15f || (!_fullBright && ty > _fogDist + 1)) continue;
            _sprites.Add((t, ty));
        }
        _sprites.Sort((a, b) => b.depth.CompareTo(a.depth));

        foreach (var (t, depth) in _sprites)
        {
            float rx = t.X - _px, ry = t.Y - _py;
            float tX = invDet * (_dirY * rx - _dirX * ry);
            float screenX = W / 2f * (1 + tX / depth);
            float scale = Proj / depth;
            float sw = t.SpriteW * scale, sh = t.SpriteH * scale;
            float baseZ = t is Projectile or Puff or GhostRunner ? t.Z : lv.FloorAt(t.X, t.Y) + t.Z;
            float left = screenX - sw / 2, top = _horizon - (baseZ + t.SpriteH - _eyeZ) * scale;
            int x0 = Math.Max(0, (int)MathF.Ceiling(left)), x1 = Math.Min(W, (int)MathF.Ceiling(left + sw));
            int y0 = Math.Max(0, (int)MathF.Ceiling(top)), y1 = Math.Min(ViewH, (int)MathF.Ceiling(top + sh));
            if (x0 >= x1 || y0 >= y1) continue;
            var tex = t.Sprite(g.Time);
            int light = t.FullBright ? 256 : Light(th);
            int vis = t.FullBright ? Math.Max(Vis(th, depth), 160) : Vis(th, depth);
            bool painFlash = t is Monster m && m.State == AiState.Pain;
            int alpha = t.Alpha;
            for (int x = x0; x < x1; x++)
            {
                int u = Math.Clamp((int)((x - left) / sw * tex.W), 0, tex.W - 1);
                for (int y = y0; y < y1; y++)
                {
                    int idx = y * W + x;
                    if (depth >= _depth[idx]) continue;
                    int v = Math.Clamp((int)((y - top) / sh * tex.H), 0, tex.H - 1);
                    uint c = tex.Px[v * tex.W + u];
                    if (Col.A(c) == 0) continue;
                    if (painFlash) c = Col.Lerp(c, Col.Rgb(255, 255, 255), 70);
                    c = Col.Fog(c, light, vis, th.FogColor);
                    if (alpha < 256) { Fb[idx] = Col.Lerp(Fb[idx], c, alpha); continue; } // see-through: no depth write
                    Fb[idx] = c;
                    _depth[idx] = depth;
                }
            }
        }
    }

    /// <summary>Flying: the canopy frame and dashboard, a gunsight, and readouts for speed, altitude and how far you've come.</summary>
    void DrawCockpit(Game g)
    {
        var p = g.P;
        var lv = g.Level;
        bool scifi = Art.Style == ArtStyle.SciFi;
        uint frame = scifi ? Col.Rgb(60, 66, 78) : Col.Rgb(92, 60, 32), lip = scifi ? Col.Rgb(130, 140, 156) : Col.Rgb(200, 160, 70);
        uint glow = scifi ? Col.Rgb(90, 220, 255) : Col.Rgb(255, 200, 110);
        // canopy struts from the dashboard corners up to the top of the view
        for (int y = 0; y < ViewH; y++)
        {
            int inset = 6 + y * 22 / ViewH;
            for (int k = 0; k < 5; k++) { Put(inset + k - 6, y, frame); Put(W - inset - k + 5, y, frame); }
            Put(inset - 1, y, lip); Put(W - inset, y, lip);
        }
        Rect(0, 0, W, 4, frame);
        Rect(0, 4, W, 1, lip);
        // dashboard
        int dy = ViewH - 26;
        for (int y = dy; y < ViewH; y++)
        {
            int curve = (int)(18 * MathF.Pow((y - dy) / 26f, 0.5f));
            for (int x = 40 - curve; x < W - 40 + curve; x++) Put(x, y, y == dy ? lip : Col.Shade(frame, 200 + (y - dy) * 3));
        }
        int bx = (int)(MathF.Sin(g.Time * 11) * p.FireAnim * 20);
        Text(56, dy + 5, $"SPD {p.ShipSpeed * 20:0}", glow);
        Text(56, dy + 15, $"ALT {p.Z * 100:0}", glow);
        // progress along the lane, west to east
        float along = Math.Clamp((p.X - lv.StartX) / Math.Max(1f, lv.W - 4 - lv.StartX), 0f, 1f);
        int px0 = 140, pw = 110;
        Rect(px0, dy + 8, pw, 5, Col.Rgb(20, 22, 28));
        Rect(px0, dy + 8, (int)(pw * along), 5, glow);
        Text(px0, dy + 16, Words.T("TO THE MOON"), Col.Shade(glow, 180));
        // gunsight
        int cx = W / 2 + bx, cy = (int)(ViewH / 2f + p.Pitch);
        for (int k = 3; k <= 7; k++) { Put(cx - k, cy, glow); Put(cx + k, cy, glow); Put(cx, cy - k, glow); Put(cx, cy + k, glow); }
    }

    void DrawWeapon(Game g)
    {
        var p = g.P;
        if (g.Level.Flight) { DrawCockpit(g); return; }
        if (g.Mode == GameMode.Dead || g.Relaxed || g.StoryMode) return; // relaxed mode and cases: weapons stay sheathed
        int slot = p.Weapon;
        var frames = Art.Weapons[(int)p.Class * 3 + slot];
        var tex = p.FireAnim > 0.06f ? frames[1] : frames[0];
        int bx = (int)(MathF.Cos(p.Bob * 0.5f) * 5 * p.BobAmount);
        int by = (int)(MathF.Abs(MathF.Sin(p.Bob * 0.5f)) * 5 * p.BobAmount);
        int x0 = W / 2 - tex.W / 2 + 20 + bx;
        int y0 = ViewH - tex.H + 4 + by + (int)(p.Raise * tex.H);
        int light = Math.Max(g.Level.Theme.Light, 200);
        // an upgraded arsenal glows with its tier's colour, pulsing
        int tier = g.ArsenalTier;
        uint tint = Arsenal.Colour(tier);
        int glow = tier == 0 ? 0 : 40 + 14 * tier + (int)(24 * MathF.Sin(g.Time * 5));
        for (int y = 0; y < tex.H; y++)
        {
            int sy = y0 + y;
            if ((uint)sy >= ViewH) continue;
            for (int x = 0; x < tex.W; x++)
            {
                int sx = x0 + x;
                if ((uint)sx >= W) continue;
                uint c = tex.Px[y * tex.W + x];
                if (Col.A(c) == 0) continue;
                if (glow > 0) c = Col.Lerp(c, tint, glow);
                Fb[sy * W + sx] = Col.Shade(c, light);
            }
        }
    }

    /// <summary>
    /// The aiming mark, where your shots go: the centre of the view, on the horizon, so it follows vertical look.
    /// Light with a dark outline so it reads on any wall. The cockpit has its own gunsight.
    /// </summary>
    void DrawCrosshair(Game g)
    {
        var style = g.Vars.Crosshair;
        if (style == CrosshairStyle.Off || g.Level.Flight || g.ShowMap || g.Mode == GameMode.Dead) return;
        int cx = W / 2, cy = (int)MathF.Round(ViewH / 2f + g.P.Pitch);
        var pts = new List<(int x, int y)>();
        switch (style)
        {
            case CrosshairStyle.Dot:
                pts.AddRange(new[] { (0, 0), (1, 0), (0, 1), (1, 1) });
                break;
            case CrosshairStyle.Cross:
                for (int k = 2; k <= 5; k++) pts.AddRange(new[] { (k, 0), (-k, 0), (0, k), (0, -k) });
                break;
            case CrosshairStyle.Circle:
                for (int y = -5; y <= 5; y++)
                    for (int x = -5; x <= 5; x++)
                        if (MathF.Abs(MathF.Sqrt(x * x + y * y) - 4f) < 0.5f) pts.Add((x, y));
                pts.Add((0, 0));
                break;
        }
        uint dark = Col.Rgb(12, 10, 14), light = Col.Rgb(240, 236, 220);
        foreach (var (x, y) in pts)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if ((dx == 0) != (dy == 0)) Put(cx + x + dx, cy + y + dy, dark);
        foreach (var (x, y) in pts) Put(cx + x, cy + y, light);
    }

    /// <summary>
    /// The strafe helper, above the aim point: the keys to hold (green when you're holding them, red for a key that's
    /// costing you speed), a JUMP bar that lights as you land, and in the air a turn gauge: the green band is the
    /// view angles that gain speed, the yellow tick the best one, the white mark your view. Turn the mouse to keep the
    /// tick on the mark: it keeps moving, since your velocity swings round as you strafe.
    /// </summary>
    void DrawStrafeHelper(Game g)
    {
        if (g.StrafeAdvice() is not { } tip) return;
        int cx = W / 2, cy = (int)MathF.Round(ViewH / 2f + g.P.Pitch);
        uint on = Col.Rgb(90, 230, 120), want = Col.Rgb(255, 220, 90), wrong = Col.Rgb(240, 80, 60), idle = Col.Rgb(70, 74, 86), ink = Col.Rgb(12, 12, 16);
        bool blink = ((int)(g.Time * 6) & 1) == 0;

        void Key(int x, int y, int w, string label, bool wanted, bool held)
        {
            uint fill = held ? (wanted ? on : wrong) : wanted ? (blink ? want : Col.Shade(want, 150)) : idle;
            Rect(x - 1, y - 1, w + 2, 12, ink);
            Rect(x, y, w, 10, fill);
            Font.Draw(Fb, W, H, x + (w - Font.Width(label)) / 2, y + 1, label, held || wanted ? ink : Col.Rgb(150, 154, 166), 1, false);
        }
        int ky = cy - 50;
        bool wantW = !tip.Air || tip.WantForward, wantA = tip.Air && tip.Side < 0, wantD = tip.Air && tip.Side > 0;
        Key(cx - 21, ky, 12, "A", wantA, g.InStrafe < 0);
        Key(cx - 6, ky, 12, "W", wantW, g.InMove > 0);
        Key(cx + 9, ky, 12, "D", wantD, g.InStrafe > 0);
        Key(cx - 21, ky + 13, 42, "JUMP", tip.Jump, false);

        if (!tip.Air) return;
        // the turn gauge: 2 pixels a degree, 30 degrees each side of your view
        const float px = 2 * 180 / MathF.PI;
        int gy = cy - 22, half = 60;
        Rect(cx - half, gy, half * 2 + 1, 1, idle);
        int zl = Math.Clamp(cx + (int)MathF.Round(tip.Lo * px), cx - half, cx + half), zh = Math.Clamp(cx + (int)MathF.Round(tip.Hi * px), cx - half, cx + half);
        if (zh > zl) Rect(zl, gy - 1, zh - zl + 1, 3, tip.InZone ? on : Col.Shade(on, 150));
        int tx = cx + (int)MathF.Round(tip.Target * px);
        if (tx < cx - half) Text(cx - half - 8, gy - 3, "<", want);
        else if (tx > cx + half) Text(cx + half + 3, gy - 3, ">", want);
        else Rect(tx, gy - 3, 1, 7, want);
        Rect(cx, gy + 2, 1, 3, Col.Rgb(250, 250, 250));
        Rect(cx - 1, gy + 4, 3, 1, Col.Rgb(250, 250, 250));
        // your velocity swings round as you gain speed, so there's no holding still: in the zone, keep turning the way
        // your strafe key curves you; outside it, turn toward the tick (faster, or ease off)
        string hint = tip.InZone ? (tip.Side > 0 ? "GOOD, KEEP TURNING >" : "< GOOD, KEEP TURNING") : tip.Target > 0 ? "TURN >" : "< TURN";
        uint hc = tip.InZone ? on : want;
        Text(cx - Font.Width(hint) / 2, gy + 7, hint, hc);
    }

    void DrawScreenTint(Game g)
    {
        var p = g.P;
        if (p.DamageFlash > 0) Tint(Col.Rgb(200, 0, 0), (int)(p.DamageFlash * 140));
        if (p.PickupFlash > 0) Tint(Col.Rgb(255, 210, 90), (int)(p.PickupFlash * 60));
        if (p.TeleportFlash > 0) Tint(Col.Rgb(255, 255, 255), (int)(p.TeleportFlash * 200));
        if (g.Mode == GameMode.Dead) Tint(Col.Rgb(120, 0, 0), 90);
    }

    void Tint(uint c, int amt)
    {
        amt = Math.Clamp(amt, 0, 256);
        for (int i = 0; i < W * ViewH; i++) Fb[i] = Col.Lerp(Fb[i], c, amt);
    }

    void Darken(int x, int y, int w, int h, int amt)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                Fb[j * W + i] = Col.Shade(Fb[j * W + i], 256 - amt);
    }

    // ================================================================ console & arena

    void DrawConsole(Game g)
    {
        const int ch = 112;
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < W; x++)
                Fb[y * W + x] = Col.Lerp(Fb[y * W + x], Col.Rgb(20, 10, 16), 210);
        Rect(0, ch, W, 1, Col.Rgb(200, 150, 60));
        var con = g.Con;
        int rows = (ch - 14) / 9;
        int end = con.Log.Count - con.Scroll;
        int start = Math.Max(0, end - rows);
        int y0 = ch - 14 - (end - start) * 9;
        for (int i = start; i < end; i++, y0 += 9)
        {
            string l = con.Log[i];
            if (l.Length > 52) l = l[..52];
            Text(3, y0, l, l.StartsWith(">") ? Col.Rgb(255, 220, 120) : Col.Rgb(210, 200, 185));
        }
        string cursor = ((int)(g.Time * 3) & 1) == 0 ? "_" : " ";
        Text(3, ch - 11, "] " + con.Line + cursor, Col.Rgb(255, 255, 255));
        if (con.Scroll > 0) Text(W - 30, 2, "^^^", Col.Rgb(200, 150, 60));
    }

    /// <summary>
    /// Arcade mode: damage numbers floating up out of what you hit, the score across the top, and the style rank on
    /// the right: a big letter, its title, the meter to the next rank, the multiplier and the combo count.
    /// </summary>
    void DrawArcade(Game g)
    {
        var a = g.Arcade;
        float invDet = 1f / (_plX * _dirY - _dirX * _plY);
        foreach (var f in a.Floaters)
        {
            float rx = f.X - _px, ry = f.Y - _py;
            float depth = invDet * (-_plY * rx + _plX * ry);
            if (depth < 0.2f) continue;
            float tX = invDet * (_dirY * rx - _dirX * ry);
            int sx = (int)(W / 2f * (1 + tX / depth)), sy = (int)(_horizon - (f.Z - _eyeZ) * Proj / depth);
            if (sx < 0 || sx >= W || sy < 0 || sy >= ViewH - 8 || depth > _depth[sy * W + sx] + 0.6f) continue; // behind a wall
            float life = f.Life / f.MaxLife;
            uint c = Col.Shade(f.Colour, (int)(120 + 136 * MathF.Min(1, life * 2)));
            int tw = Font.Width(f.Text) * f.Scale;
            Text(sx - tw / 2 + 1, sy + 1, f.Text, Col.Rgb(10, 8, 8), f.Scale);
            Text(sx - tw / 2, sy, f.Text, c, f.Scale);
        }

        if (g.Vars.Hud == HudStyle.Off) return;
        int top = (g.Vars.ShowFps ? 12 : 3) + (g.ArenaMode && g.Level.Arena != null ? 42 : 0) + (g.Level.Ship != null ? 50 : 0);
        string score = $"SCORE {a.Score:000000}";
        Text(W - 6 - Font.Width(score), top, score, Col.Rgb(255, 240, 200));
        if (!a.Active) return;
        top += 12;
        int r = a.Rank;
        uint rc = Arcade.Colours[r];
        if (a.RankFlash > 0) rc = Col.Lerp(rc, Col.Rgb(255, 255, 255), (int)(a.RankFlash * 200));
        string letter = Arcade.Ranks[r];
        int scale = 3 + (a.RankFlash > 0.5f ? 1 : 0);
        int lx = W - 6 - Font.Width(letter) * scale;
        Text(lx + 1, top + 1, letter, Col.Rgb(10, 8, 8), scale);
        Text(lx, top, letter, rc, scale);
        int y = top + 8 * scale + 2;
        string title = Arcade.Titles[r];
        Text(W - 6 - Font.Width(title), y, title, rc);
        // the meter towards the next rank
        const int mw = 60;
        Rect(W - 6 - mw, y + 10, mw, 3, Col.Rgb(30, 26, 26));
        Rect(W - 6 - mw, y + 10, (int)(mw * a.Meter), 3, rc);
        string mult = $"x{a.Multiplier}";
        Text(W - 6 - Font.Width(mult), y + 16, mult, Col.Rgb(255, 230, 120));
        if (a.Combo > 1)
        {
            string combo = $"{a.Combo} HITS";
            Text(W - 6 - Font.Width(combo), y + 26, combo, Col.Rgb(230, 220, 200));
        }
    }

    /// <summary>
    /// Story mode: the case and your progress in the top-right corner; the conversation panel while you question
    /// someone; the journal (J) with everything you've found and heard, lies and patterns picked out.
    /// </summary>
    void DrawStory(Game g)
    {
        var s = g.Story;
        uint gold = Col.Rgb(255, 210, 110), dim = Col.Rgb(170, 160, 150), flag = Col.Rgb(255, 120, 90);
        if (g.ReadingLore == null && s.Talking == null && !s.JournalOpen && g.Vars.Hud != HudStyle.Off)
        {
            int top = g.Vars.ShowFps ? 12 : 3;
            string title = $"CASE {g.StoryCase + 1} OF {Story.Cases.Length}";
            Text(W - 4 - Font.Width(title), top, title, gold);
            string prog = $"CLUES {s.Found.Count}/{s.Case.Clues.Length}";
            Text(W - 4 - Font.Width(prog), top + 10, prog, dim);
            string strikes = $"STRIKES {s.Strikes}/{StoryState.MaxStrikes}";
            Text(W - 4 - Font.Width(strikes), top + 20, strikes, s.Strikes > 0 ? flag : dim);
            Text(W - 4 - Font.Width("J: JOURNAL"), top + 30, "J: JOURNAL", dim);
        }
        if (s.Talking != null)
        {
            var opts = Story.Options(s);
            int h = 26 + Wrap(s.Line, 48).Count() * 9 + opts.Count * 9;
            int y = ViewH - h - 4;
            Darken(6, y, W - 12, h, 200);
            Rect(6, y, W - 12, 1, gold);
            var sus = s.Talking.S;
            Text(12, y + 4, sus.Name.ToUpperInvariant(), gold);
            Text(12 + Font.Width(sus.Name) + 8, y + 4, sus.Role.ToUpperInvariant(), dim);
            int ly = y + 16;
            foreach (var line in Wrap(s.Line, 48)) { Text(12, ly, line, Col.Rgb(235, 228, 215)); ly += 9; }
            ly += 4;
            for (int i = 0; i < opts.Count; i++, ly += 9)
            {
                bool sel = i == s.Cursor;
                uint c = opts[i].key == "accuse" ? flag : sel ? MenuSel : MenuText;
                Text(14, ly, (sel ? "> " : "  ") + opts[i].label, sel ? MenuSel : c);
            }
        }
        if (s.JournalOpen)
        {
            Darken(0, 0, W, H, 225);
            CenterText("CASE JOURNAL", 6, gold, 2);
            CenterText(s.Case.Title.ToUpperInvariant(), 24, Col.Rgb(230, 190, 80));
            int y = 36;
            var lines = new List<(string text, uint col)>();
            foreach (var (text, isFlag) in s.Journal)
                foreach (var l in Wrap(text, 50)) lines.Add((l, isFlag ? flag : Col.Rgb(220, 212, 200)));
            if (lines.Count == 0) lines.Add(("NOTHING YET. QUESTION PEOPLE AND LOOK FOR CLUES.", dim));
            int max = (H - 20 - y) / 9;
            foreach (var (text, col) in lines.Skip(Math.Max(0, lines.Count - max))) { Text(8, y, text, col); y += 9; }
            string foot = s.HasKeys ? "YOU HAVE THE EVIDENCE. NAME THE CULPRIT." : "J: CLOSE";
            CenterText(foot, H - 12, s.HasKeys ? Col.Rgb(120, 255, 140) : dim);
        }
    }

    void DrawArenaHud(Game g)
    {
        var a = g.Level.Arena;
        if (a == null) return;
        int top = g.Vars.ShowFps ? 12 : 3;
        if (g.ArenaMode && g.Vars.Hud != HudStyle.Off)
        {
            // under the wave count: your best, and the next medal you haven't got (by this run or your best)
            int best = g.Profile.ArenaBestWave(g.P.Class);
            string line = best > 0 ? $"BEST {best} WAVE{(best == 1 ? "" : "S")}" : "NO BEST YET";
            Text(W - 4 - Font.Width(line), top + 10, line, Col.Rgb(255, 220, 90));
            var (next, at) = ArenaMedals.Next(Math.Max(best, a.BestWave));
            if (next != Medal.None)
            {
                string nm = $"{Medals.Name(next)} AT {at}";
                Text(W - 4 - Font.Width(nm) - 9, top + 20, nm, Medals.Colour(next));
                MedalDot(W - 9, top + 21, next);
            }
            else
            {
                Text(W - 4 - Font.Width("GOLD") - 9, top + 20, "GOLD", Medals.Colour(Medal.Gold));
                MedalDot(W - 9, top + 21, Medal.Gold);
            }
        }
        if (g.ArenaMode && g.Vars.Hud != HudStyle.Off)
        {
            // the perks you've picked, one a line under the medal
            int py = top + 32;
            foreach (var (perk, rank) in a.Perks.OrderBy(kv => kv.Key))
            {
                string line = $"{PerkInfo.Short(perk)} {PerkInfo.Roman(rank)}";
                Text(W - 4 - Font.Width(line), py, line, Col.Rgb(170, 200, 255));
                py += 9;
            }
        }
        if (g.ArenaMode && a.Offer != null) DrawPerkOffer(g, a);
        if (!a.Started) return;
        if (g.ArsenalTier > 0 && g.Vars.Hud != HudStyle.Off)
        {
            string ars = $"ARSENAL {Words.T(Arsenal.Name(g.ArsenalTier)).ToUpperInvariant()}";
            Text(W - 4 - Font.Width(ars), top + 30, ars, Arsenal.Colour(g.ArsenalTier));
        }
        string status = a.InIntermission ? $"WAVE {a.Wave} CLEARED" : $"WAVE {a.Wave}  LEFT {a.Remaining}";
        if (g.Vars.Hud != HudStyle.Off) Text(W - 4 - Font.Width(status), top, status, Col.Rgb(230, 120, 255));
        if (a.BannerTime > 0)
        {
            string big = a.InIntermission ? "WAVE CLEARED!" : $"WAVE {a.Wave}";
            CenterText(big, 40, a.InIntermission ? Col.Rgb(120, 255, 140) : Col.Rgb(255, 90, 60), 3);
            if (!a.InIntermission && a.Wave % 5 == 0) CenterText(Words.T("A HERESIARCH APPROACHES"), 68, Col.Rgb(230, 120, 255));
        }
        if (a.InIntermission && a.Offer == null) CenterText($"NEXT WAVE IN {MathF.Ceiling(a.Timer):0}", 80, Col.Rgb(240, 225, 170));
    }

    // ================================================================ automap

    void DrawAutomap(Game g)
    {
        var lv = g.Level;
        Darken(0, 0, W, ViewH, 190);
        int cs = Math.Max(2, Math.Min((W - 16) / lv.W, (ViewH - 24) / lv.H));
        int ox = (W - lv.W * cs) / 2, oy = 14 + (ViewH - 14 - lv.H * cs) / 2;
        for (int y = 0; y < lv.H; y++)
            for (int x = 0; x < lv.W; x++)
            {
                int i = y * lv.W + x;
                if (!lv.Seen[i]) continue;
                char c = lv.Cells[i];
                uint col;
                if (c == '\0')
                {
                    char mk = lv.Marks[i];
                    col = mk == 'E' ? Col.Rgb(200, 50, 40) : mk == '*' ? Col.Rgb(150, 50, 200) : mk == '!' ? Col.Rgb(230, 190, 60) : mk == '^' ? Col.Rgb(40, 120, 110) : mk != '\0' ? Col.Rgb(60, 120, 255) : lv.Outdoor[i] ? Col.Rgb(34, 50, 30) : Col.Rgb(40, 36, 32);
                }
                else col = c switch
                {
                    'D' => Col.Rgb(210, 170, 60),
                    'S' => Col.Rgb(150, 170, 230),
                    'F' => Col.Rgb(240, 110, 40),
                    'X' => Col.Rgb(80, 220, 200),
                    Level.Rubble => Col.Rgb(120, 104, 90),
                    'N' => Col.Rgb(200, 128, 78),
                    'Q' => Col.Rgb(176, 112, 255),
                    'U' => Col.Rgb(120, 255, 96),
                    'P' => Col.Rgb(140, 140, 140),
                    'L' => Col.Rgb(80, 220, 90),
                    _ => Col.Rgb(150, 110, 70),
                };
                for (int yy = 0; yy < cs; yy++)
                    for (int xx = 0; xx < cs; xx++)
                    {
                        bool edge = xx == cs - 1 || yy == cs - 1;
                        Put(ox + x * cs + xx, oy + y * cs + yy, c == '\0' || !edge ? col : Col.Shade(col, 150));
                    }
            }
        // lore stones you've seen: cyan until read
        foreach (var t in lv.Things)
            if (t is LoreStone ls && lv.Seen[(int)t.Y * lv.W + (int)t.X])
            {
                uint lc = ls.Read ? Col.Rgb(60, 110, 130) : Col.Rgb(110, 230, 255);
                int lx = ox + (int)(t.X * cs), ly = oy + (int)(t.Y * cs);
                for (int yy = -1; yy <= 1; yy++) for (int xx = -1; xx <= 1; xx++) if (xx == 0 || yy == 0) Put(lx + xx, ly + yy, lc);
            }

        // chests you've seen: gold when closed, brown once looted
        foreach (var t in lv.Things)
            if (t is Chest ch && lv.Seen[(int)t.Y * lv.W + (int)t.X])
            {
                uint cc = ch.Opened ? Col.Rgb(110, 70, 30) : Col.Rgb(255, 210, 60);
                int cx = ox + (int)(t.X * cs), cy = oy + (int)(t.Y * cs);
                for (int yy = -1; yy <= 1; yy++) for (int xx = -1; xx <= 1; xx++) Put(cx + xx, cy + yy, cc);
            }

        // player arrow
        float px = ox + g.P.X * cs, py = oy + g.P.Y * cs;
        float ca = MathF.Cos(g.P.Angle), sa = MathF.Sin(g.P.Angle);
        for (float t = -cs; t <= cs * 1.3f; t += 0.5f) Put((int)(px + ca * t), (int)(py + sa * t), Col.Rgb(255, 255, 255));
        for (float t = 0; t <= cs * 0.8f; t += 0.5f)
        {
            Put((int)(px + ca * (cs * 1.3f - t) - sa * t * 0.6f), (int)(py + sa * (cs * 1.3f - t) + ca * t * 0.6f), Col.Rgb(255, 255, 255));
            Put((int)(px + ca * (cs * 1.3f - t) + sa * t * 0.6f), (int)(py + sa * (cs * 1.3f - t) - ca * t * 0.6f), Col.Rgb(255, 255, 255));
        }
        CenterText(lv.Name.ToUpperInvariant(), 3, Col.Rgb(230, 190, 80));
    }

    void Put(int x, int y, uint c)
    {
        if ((uint)x < W && (uint)y < ViewH) Fb[y * W + x] = c;
    }

    // ================================================================ HUD

    void DrawHud(Game g)
    {
        var p = g.P;
        var style = g.Vars.Hud;
        uint label = Col.Rgb(200, 180, 140);
        // the bottom of the view the corner readouts sit on: above the cockpit's dashboard when flying
        int bottom = g.Level.Flight ? ViewH - 28 : ViewH;
        if (style != HudStyle.Off)
        {
            // what sits in the view's corners is lifted clear of the compact and minimal readouts
            int lift = style switch { HudStyle.Compact => 22, HudStyle.Minimal => 12, _ => 0 };
            if (p.HasJetpack) DrawFuel(p, label, bottom - lift);
            if (!g.Level.Flight && style != HudStyle.Minimal) DrawLevelBar(g, bottom - lift); // the cockpit dashboard fills that corner when flying
            if (g.Level.Ship != null) DrawShipPanel(g);
            if (p.Blocks > 0 && !g.Level.Flight)
            {
                // above the level bar and its +XP pop-up
                int y = bottom - lift - (style == HudStyle.Minimal ? 16 : 34);
                Icon(Art.Rubble, 4, y, 12);
                Text(19, y + 4, $"x{p.Blocks}", Col.Rgb(230, 220, 200));
            }
        }
        DrawSpeed(g);
        if (g.Practicing && g.Course.Timed && style != HudStyle.Off) DrawRunClock(g);
        if (g.Practicing) DrawDemoBanner(g);
        switch (style)
        {
            case HudStyle.Full: DrawStatusBar(g, label); break;
            case HudStyle.Compact: DrawCompactHud(g, bottom); break;
            case HudStyle.Minimal: DrawMinimalHud(g, bottom); break;
        }
    }

    /// <summary>With Quake movement, how fast you're going against your run speed, while strafe jumping takes you past it.</summary>
    void DrawSpeed(Game g)
    {
        var p = g.P;
        if (!g.Vars.QuakeMove || g.Vars.Hud == HudStyle.Off || g.Level.Flight || p.Flying) return;
        int pct = (int)MathF.Round(p.HSpeed / g.RunSpeed * 100);
        if (pct < 110) return;
        string s = $"SPEED {pct}%";
        uint c = pct >= 200 ? Col.Rgb(255, 120, 60) : pct >= 150 ? Col.Rgb(255, 220, 90) : Col.Rgb(200, 230, 255);
        CenterText(s, ViewH / 2 + 20, c);
    }

    /// <summary>
    /// Watching the demo: what it's doing now (the step of the technique), the game speed, and the controls; stopped on a
    /// step, the prompt to go on. Practising in slow motion yourself: the speed, and that the run won't count.
    /// </summary>
    void DrawDemoBanner(Game g)
    {
        int y = (g.Level.Flight ? ViewH - 28 : ViewH) - 60;
        uint gold = Col.Rgb(230, 190, 80), dim = Col.Rgb(200, 190, 170), green = Col.Rgb(120, 255, 140);
        string pct = g.PracticeSpeed < 1 ? $"  {g.PracticeSpeed * 100:0}% SPEED" : "";
        if (!g.Demo)
        {
            if (g.PracticeSpeed < 1) CenterText($"SLOW MO {g.PracticeSpeed * 100:0}%: RUNS DON'T COUNT (1: FULL)", y + 36, gold);
            return;
        }
        CenterText((g.DemoSteps ? "DEMO: STEP BY STEP" : "DEMO") + pct, y, gold);
        var lines = Wrap(g.Pilot.Caption.ToUpperInvariant(), (W - 16) / Font.CharW).Take(2).ToList();
        for (int i = 0; i < lines.Count; i++) CenterText(lines[i], y + 11 + i * 9, Col.Rgb(250, 245, 230));
        if (g.DemoPaused) CenterText(((int)(g.Time * 3) & 1) == 0 ? "ENTER: NEXT STEP" : "", y + 36, green);
        else CenterText("1/2/3 SPEED  E STEPS  MOVE: TAKE OVER", y + 36, dim);
    }

    /// <summary>On the practice course: the run's time and your best, in the top-right corner (messages keep clear of it).</summary>
    void DrawRunClock(Game g)
    {
        float best = g.Profile.CourseBestTime(g.Course.Key(g.P.Class));
        string time = $"TIME {g.RunTime:0.00}", top = $"BEST {best:0.00}";
        int y = g.Vars.ShowFps ? 12 : 3;
        Text(W - 4 - Font.Width(time), y, time, g.RunStarted ? Col.Rgb(240, 236, 220) : Col.Rgb(150, 150, 160));
        if (best > 0) Text(W - 4 - Font.Width(top), y + 10, top, Col.Rgb(255, 220, 90));
        // the medal this run can still make, counting down through gold, silver and bronze as the clock runs
        var (medal, at) = g.NextMedal();
        if (medal != Medal.None)
        {
            string next = $"{Medals.Name(medal)} {at:0.00}";
            int ny = y + (best > 0 ? 20 : 10);
            Text(W - 4 - Font.Width(next) - 9, ny, next, Medals.Colour(medal));
            MedalDot(W - 9, ny + 1, medal);
        }
    }

    /// <summary>A little medal: a coloured disc with a dark rim (grey and hollow for none).</summary>
    void MedalDot(int x, int y, Medal m)
    {
        uint c = Medals.Colour(m), rim = Col.Rgb(20, 16, 12);
        void Dot(int px, int py, uint col) { if ((uint)px < W && (uint)py < H) Fb[py * W + px] = col; }
        for (int j = -1; j <= 6; j++)
            for (int i = -1; i <= 6; i++)
            {
                float d = MathF.Sqrt((i - 2.5f) * (i - 2.5f) + (j - 2.5f) * (j - 2.5f));
                if (d <= 3.9f) Dot(x + i, y + j, rim);
                if (d <= 3f && (m != Medal.None || d > 1.9f)) Dot(x + i, y + j, j < 2 && i < 3 ? Col.Lerp(c, Col.Rgb(255, 255, 255), 90) : c);
            }
    }

    /// <summary>Room for the practice clock in the top-right corner.</summary>
    const int RunClockW = 84, ArenaHudW = 100;

    /// <summary>The classic status bar along the bottom of the screen.</summary>
    void DrawStatusBar(Game g, uint label)
    {
        var p = g.P;
        var hb = Art.HudBack;
        for (int y = 0; y < HudH; y++)
            for (int x = 0; x < W; x++)
                Fb[(ViewH + y) * W + x] = Col.Shade(hb.Px[(y & (hb.H - 1)) * hb.W + (x & (hb.W - 1))], y == 0 ? 400 : y == 1 ? 60 : 200);

        int by = ViewH + 3;
        if (g.Relaxed) { DrawDiscoveryHud(g, by, label); return; }
        Text(6, by, "HEALTH", label);
        uint hcol = p.Health > p.MaxHealth / 2 ? Col.Rgb(240, 230, 210) : p.Health > p.MaxHealth / 4 ? Col.Rgb(250, 200, 60) : Col.Rgb(250, 60, 40);
        Text(8, by + 11, p.Health.ToString(), hcol, 2);

        Text(52, by, "ARMOR", label);
        Text(54, by + 11, p.Armor.ToString(), Col.Rgb(170, 190, 230), 2);

        // mana bars
        ManaBar(96, by + 1, Words.T("BLUE"), p.BlueMana, Col.Rgb(60, 120, 255));
        ManaBar(96, by + 14, Words.T("GREEN"), p.GreenMana, Col.Rgb(60, 210, 80));

        // weapon slots, underlined with the mana colour the current weapon uses
        Text(202, by, "ARMS", label);
        for (int i = 0; i < 3; i++)
        {
            uint c = !p.HasWeapon[i] ? Col.Rgb(70, 60, 50) : i == p.Weapon ? Col.Rgb(255, 220, 90) : Col.Rgb(200, 190, 170);
            Text(202 + i * 9, by + 12, (i + 1).ToString(), c);
        }
        var w = p.CurWeapon;
        if (w.Mana > 0) Rect(202, by + 22, 24, 3, w.Mana == 1 ? Col.Rgb(60, 120, 255) : Col.Rgb(60, 210, 80));

        // inventory (F uses a healing item) and keys
        Icon(Art.Flask, 234, by + 2, 18);
        Text(250, by + 13, p.Flasks.ToString(), Col.Rgb(240, 230, 210));
        Icon(Art.Urn, 258, by + 2, 18);
        Text(274, by + 13, p.Urns.ToString(), Col.Rgb(240, 230, 210));
        if (p.SteelKey) Icon(Art.SteelKey, 284, by + 6, 20);
        if (p.FireKey) Icon(Art.FireKey, 298, by + 6, 20);

        string cls = p.Def.Name.ToUpperInvariant();
        Text(W - 4 - Font.Width(cls), by - 1, cls, Col.Rgb(230, 190, 80));
    }

    static uint HealthColour(Player p) =>
        p.Health > p.MaxHealth / 2 ? Col.Rgb(240, 230, 210) : p.Health > p.MaxHealth / 4 ? Col.Rgb(250, 200, 60) : Col.Rgb(250, 60, 40);

    static readonly uint BlueCol = Col.Rgb(60, 120, 255), GreenCol = Col.Rgb(60, 210, 80);

    /// <summary>A small red cross, the health marker on the overlay HUDs.</summary>
    void Cross(int x, int y)
    {
        Rect(x - 1, y + 1, 9, 5, Col.Rgb(20, 12, 12));
        Rect(x + 1, y - 1, 5, 9, Col.Rgb(20, 12, 12));
        Rect(x, y + 2, 7, 3, Col.Rgb(230, 50, 40));
        Rect(x + 2, y, 3, 7, Col.Rgb(230, 50, 40));
    }

    /// <summary>No status bar: health and armor in the bottom-left corner, items beside them, ammo and keys on the right.</summary>
    void DrawCompactHud(Game g, int bottom)
    {
        var p = g.P;
        int y = bottom - 18;
        if (g.Relaxed)
        {
            uint val = Col.Rgb(240, 230, 210), lab = Col.Rgb(200, 180, 140);
            Text(4, y, $"{Words.T("RELICS")} {p.Relics}/{g.RelicsTotal}   LORE {p.LoreRead}/{g.LoreTotal}", val);
            Text(4, y + 9, $"SECRETS {p.Secrets}/{g.SecretsTotal}   EXPLORED {(int)(Discovery.Explored(g.Hub) * 100)}%", lab);
        }
        else
        {
            Cross(4, y + 4);
            string hp = p.Health.ToString();
            Text(15, y, hp, HealthColour(p), 2);
            int ax = 19 + Font.Width(hp, 2);
            Rect(ax, y + 3, 6, 8, Col.Rgb(20, 22, 30));
            Rect(ax + 1, y + 4, 4, 6, Col.Rgb(170, 190, 230));
            Text(ax + 9, y + 3, p.Armor.ToString(), Col.Rgb(170, 190, 230));
            // healing items
            int ix = ax + 12 + Font.Width(p.Armor.ToString());
            Icon(Art.Flask, ix, y + 2, 12);
            Text(ix + 12, y + 6, p.Flasks.ToString(), Col.Rgb(240, 230, 210));
            Icon(Art.Urn, ix + 20, y + 2, 12);
            Text(ix + 32, y + 6, p.Urns.ToString(), Col.Rgb(240, 230, 210));

            // ammo: both kinds, the one your weapon uses lit up
            var w = p.CurWeapon;
            string blue = $"{Words.T("BLUE")} {p.BlueMana}", green = $"{Words.T("GREEN")} {p.GreenMana}";
            Text(W - 4 - Font.Width(blue), y, blue, w.Mana == 1 ? BlueCol : Col.Shade(BlueCol, 130));
            Text(W - 4 - Font.Width(green), y + 9, green, w.Mana == 2 ? GreenCol : Col.Shade(GreenCol, 130));
        }
        int kx = W - 4 - Font.Width($"{Words.T("GREEN")} 000") - 14;
        if (p.SteelKey) Icon(Art.SteelKey, kx, y - 1, 12);
        if (p.FireKey) Icon(Art.FireKey, kx, y + 8, 12);
    }

    /// <summary>Just the essentials: health in the bottom-left corner, ammo for the weapon in hand in the bottom-right.</summary>
    void DrawMinimalHud(Game g, int bottom)
    {
        var p = g.P;
        int y = bottom - 10;
        if (g.Relaxed)
            Text(4, y, $"{Words.T("RELICS")} {p.Relics}/{g.RelicsTotal}", Col.Rgb(240, 230, 210));
        else
        {
            Cross(4, y);
            Text(14, y, p.Health.ToString(), HealthColour(p));
            var w = p.CurWeapon;
            if (w.Mana > 0)
            {
                string ammo = (w.Mana == 1 ? p.BlueMana : p.GreenMana).ToString();
                Text(W - 4 - Font.Width(ammo), y, ammo, w.Mana == 1 ? BlueCol : GreenCol);
            }
        }
        int kx = 40;
        if (p.SteelKey) { Icon(Art.SteelKey, kx, y - 2, 10); kx += 11; }
        if (p.FireKey) Icon(Art.FireKey, kx, y - 2, 10);
    }

    /// <summary>Your level and experience, in the bottom-left corner of the view, with a pop-up as XP comes in.</summary>
    void DrawLevelBar(Game g, int bottom)
    {
        var pr = g.Profile;
        int y = bottom - 9;
        string lv = $"LV {pr.Level}";
        Text(4, y, lv, pr.Points > 0 ? Col.Rgb(120, 255, 140) : Col.Rgb(230, 190, 80));
        int bx = 8 + Font.Width(lv);
        Bar(bx, y + 2, 48, 3, pr.Level >= Profile.MaxLevel ? 1f : pr.Xp / (float)Profile.XpToNext(pr.Level), Col.Rgb(230, 190, 80));
        if (pr.Points > 0) Text(bx + 52, y, "+", Col.Rgb(120, 255, 140));
        if (g.XpPopupTime > 0) Text(4, y - 10, $"+{g.XpPopup} XP", Col.Rgb(255, 230, 120));
    }

    /// <summary>On a stranded map: ore carried, and how much of each the ship still needs, in the view's top-right corner.</summary>
    void DrawShipPanel(Game g)
    {
        var s = g.Level.Ship;
        uint[] cols = { Col.Rgb(220, 150, 96), Col.Rgb(190, 140, 255), Col.Rgb(140, 255, 120) };
        string title = s.Built ? Words.T("SKYSHIP READY - USE IT TO TAKE OFF") : Words.T("SKYSHIP REPAIRS");
        int x = W - 4, y = 3;
        Text(x - Font.Width(title), y, title, s.Built ? Col.Rgb(120, 255, 140) : Col.Rgb(230, 190, 80));
        bool carrying = false;
        for (int k = 0; k < Ship.Need.Length && !s.Built; k++)
        {
            y += 9;
            // what you've gathered so far: handed over plus in your pack
            int have = Math.Min(Ship.Need[k], s.Delivered[k] + g.P.Ore[k]);
            carrying |= g.P.Ore[k] > 0 && s.Delivered[k] < Ship.Need[k];
            string line = $"{Words.T(Game.OreNames[k].ToUpperInvariant())} {have}/{Ship.Need[k]}";
            Text(x - Font.Width(line), y, line, have >= Ship.Need[k] ? Col.Rgb(120, 255, 140) : cols[k]);
        }
        if (carrying)
        {
            string hint = Words.T("USE THE SKYSHIP TO LOAD ORE");
            Text(x - Font.Width(hint), y + 11, hint, Col.Rgb(200, 190, 170));
        }
    }

    /// <summary>Jetpack fuel gauge, tucked into the bottom-right corner of the view.</summary>
    void DrawFuel(Player p, uint label, int bottom)
    {
        const int h = 40, bw = 6;
        int x = W - 12, y0 = bottom - 6 - h;
        string name = Words.T("WINGS");
        Text(W - 3 - Font.Width(name), y0 - 9, name, p.Flying ? Col.Rgb(255, 230, 120) : label);
        Rect(x - 1, y0 - 1, bw + 2, h + 2, Col.Rgb(20, 20, 24));
        int fill = (int)MathF.Round(h * Math.Clamp(p.Fuel / p.MaxFuel, 0f, 1f));
        float f = p.Fuel / p.MaxFuel;
        uint c = f < 0.25f ? Col.Rgb(250, 70, 50) : Art.Style == ArtStyle.SciFi ? Col.Rgb(80, 190, 255) : Col.Rgb(250, 220, 120);
        if (fill > 0) Rect(x, y0 + h - fill, bw, fill, c);
    }

    /// <summary>Relaxed-mode HUD: what you've discovered instead of health and ammo.</summary>
    void DrawDiscoveryHud(Game g, int by, uint label)
    {
        var p = g.P;
        uint val = Col.Rgb(240, 230, 210), done = Col.Rgb(120, 255, 140);
        void Stat(int x, string name, int have, int total)
        {
            Text(x, by, name, label);
            Text(x + 2, by + 11, $"{have}/{total}", have >= total && total > 0 ? done : val, 2);
        }
        Stat(6, Words.T("RELICS"), p.Relics, g.RelicsTotal);
        Stat(70, "LORE", p.LoreRead, g.LoreTotal);
        Stat(134, "SECRETS", p.Secrets, g.SecretsTotal);
        int pct = (int)(Discovery.Explored(g.Hub) * 100);
        Text(198, by, "EXPLORED", label);
        Text(200, by + 11, $"{pct}%", pct >= 100 ? done : val, 2);
        if (p.SteelKey) Icon(Art.SteelKey, 262, by + 6, 20);
        if (p.FireKey) Icon(Art.FireKey, 276, by + 6, 20);
        string cls = p.Def.Name.ToUpperInvariant();
        Text(W - 4 - Font.Width(cls), by - 1, cls, Col.Rgb(230, 190, 80));
    }

    void DrawLore(Game g)
    {
        // case notes (story mode) get a bigger sheet than a lore stone's few lines
        int x0 = g.StoryMode ? 12 : 28, y0 = g.StoryMode ? 12 : 26, w = W - 2 * x0, h = g.StoryMode ? 166 : 118;
        Rect(x0 - 2, y0 - 2, w + 4, h + 4, Col.Rgb(40, 26, 14));
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                int n = ((x * 7 + y * 13) ^ (x * y)) & 15;
                Fb[y * W + x] = Col.Rgb(206 + n - 8, 186 + n - 8, 142 + n - 8);
            }
        uint ink = Col.Rgb(60, 36, 20);
        string title = g.StoryMode ? "CASE NOTES" : Words.T("LORE STONE");
        Font.Draw(Fb, W, H, (W - Font.Width(title)) / 2, y0 + 6, title, Col.Rgb(120, 40, 20), 1, false);
        Rect(x0 + 20, y0 + 16, w - 40, 1, Col.Rgb(150, 110, 70));
        int maxChars = (w - 16) / Font.CharW, ly = y0 + 24;
        foreach (var line in Wrap(g.ReadingLore.ToUpperInvariant(), maxChars))
        {
            Font.Draw(Fb, W, H, x0 + 8, ly, line, ink, 1, false);
            ly += 10;
        }
        string foot = "E / ENTER: CLOSE";
        Font.Draw(Fb, W, H, (W - Font.Width(foot)) / 2, y0 + h - 11, foot, Col.Rgb(120, 90, 60), 1, false);
    }

    static IEnumerable<string> Wrap(string text, int max)
    {
        foreach (var para in text.Split('\n'))
        {
            if (para.Length == 0) { yield return ""; continue; }
            var line = "";
            foreach (var word in para.Split(' '))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > max) { yield return line; line = ""; }
                line = line.Length == 0 ? word : line + " " + word;
            }
            if (line.Length > 0) yield return line;
        }
    }

    void ManaBar(int x, int y, string name, int val, uint col)
    {
        Text(x, y, name, Col.Rgb(200, 180, 140));
        int bx = x + 33, bw = 44;
        Rect(bx - 1, y - 1, bw + 2, 9, Col.Rgb(10, 8, 6));
        Rect(bx, y, (int)(bw * Math.Clamp(val / 200f, 0, 1)), 7, col);
        Text(bx + bw + 3, y, val.ToString(), Col.Rgb(240, 230, 210));
    }

    void Icon(Tex t, int x, int y, int size)
    {
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                uint c = t.Px[(j * t.H / size) * t.W + i * t.W / size];
                if (Col.A(c) == 0) continue;
                int sx = x + i, sy = y + j;
                if ((uint)sx < W && (uint)sy < H) Fb[sy * W + sx] = c;
            }
    }

    void Rect(int x, int y, int w, int h, uint c)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                if ((uint)i < W && (uint)j < H) Fb[j * W + i] = c;
    }

    void DrawMessages(Game g)
    {
        int y = 3;
        if (g.ShowMap) y = 14;
        int width = W - 8 - (g.Practicing ? RunClockW : g.ArenaMode || g.Vars.Arcade || g.StoryMode ? ArenaHudW : 0);
        foreach (var (text, _) in g.Messages)
            foreach (var line in Wrap(text, width / Font.CharW))
            {
                Text(4, y, line, Col.Rgb(240, 225, 170));
                y += 9;
            }
    }

    void Text(int x, int y, string s, uint c, int scale = 1) => Font.Draw(Fb, W, H, x, y, s, c, scale);
    void CenterText(string s, int y, uint c, int scale = 1) => Text((W - Font.Width(s, scale)) / 2, y, s, c, scale);

    // ================================================================ menus

    void StoneBackdrop(float time)
    {
        var t = Art.Stone;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int flick = 100 + (int)(20 * MathF.Sin(time * 3 + y * 0.05f));
                Fb[y * W + x] = Col.Shade(t.Px[(y & 63) * 64 + (x & 63)], flick);
            }
    }

    void DrawTitle(Game g)
    {
        StoneBackdrop(g.Time);
        CenterText("HEXEN SHARP", 28, Col.Rgb(230, 170, 50), 4);
        CenterText(Words.T("A TINY HEXEN-STYLE DUNGEON CRAWLER IN C#"), 64, Col.Rgb(210, 200, 180));
        // a few monsters for show
        var e = Art.Monsters["ettin"][(int)(g.Time * 2) % 2];
        var a = Art.Monsters["afrit"][(int)(g.Time * 3) % 2];
        var c = Art.Monsters["centaur"][(int)(g.Time * 2) % 2];
        Icon(e, 56, 74, 48); Icon(c, 136, 74, 48); Icon(a, 216, 72, 48);
        var items = g.Menu.Items(MenuPage.Main);
        for (int i = 0; i < items.Length; i++) MenuItem(items[i], TitleTop + i * TitleRow, g.Menu.Page == MenuPage.Main && i == g.Menu.Cursor);
        CenterText("ARROWS + ENTER.  CONTROLS ARE IN OPTIONS.", TitleFooter, Col.Rgb(150, 140, 120));
    }

    void DrawClassSelect(Game g)
    {
        StoneBackdrop(g.Time);
        CenterText("CHOOSE YOUR CLASS", 10, Col.Rgb(230, 170, 50), 2);
        if (g.PendingArena) CenterText(Words.T("THE CHAOS ARENA: SURVIVE THE WAVES"), 28, Col.Rgb(230, 120, 255));
        else if (g.PendingPractice) CenterText($"PRACTICE: {g.PendingCourse.Name.ToUpperInvariant()}", 28, Col.Rgb(170, 200, 255));
        else CenterText(g.Relaxed ? "RELAXED MODE" : "CLASSIC MODE", 28, g.Relaxed ? Col.Rgb(120, 255, 140) : Col.Rgb(200, 150, 120));
        // the difficulty, under the classes (relaxed mode has no fighting, so none to show)
        if (!g.Relaxed && !g.PendingPractice)
            Text(18, 110, $"DIFFICULTY: {Difficulties.Name(Difficulties.Of(g.Vars))}", Col.Rgb(170, 160, 140));
        for (int i = 0; i < 3; i++)
        {
            var cd = ClassDef.All[i];
            bool sel = i == g.MenuIndex;
            int y = 40 + i * 22;
            if (sel) { Rect(14, y - 4, 140, 17, Col.Rgb(70, 40, 20)); Text(18, y, ">", Col.Rgb(255, 220, 90), 1); }
            Text(28, y, $"{i + 1} {cd.Name.ToUpperInvariant()}", sel ? Col.Rgb(255, 220, 90) : Col.Rgb(190, 180, 160), 1);
        }
        var d = ClassDef.All[g.MenuIndex];
        Text(18, 128, d.Blurb, Col.Rgb(230, 220, 200));
        for (int i = 0; i < 3; i++)
        {
            var w = d.Weapons[i];
            string mana = w.Mana == 0 ? "" : Words.T(w.Mana == 1 ? " (BLUE MANA)" : " (GREEN MANA)");
            Text(18, 142 + i * 10, $"{i + 1}. {w.Name}{mana}", Col.Rgb(200, 190, 170));
        }
        var wt = Art.Weapons[g.MenuIndex * 3 + ((int)(g.Time) % 3)][((int)(g.Time * 3) % 3 == 0) ? 1 : 0];
        Rect(170, 37, 140, 83, Col.Rgb(20, 16, 14)); // clear of the mode line above
        Icon2(wt, 176, 38);
        CenterText("UP/DOWN + ENTER, OR PRESS 1-3", 188, Col.Rgb(170, 160, 140));
    }

    void Icon2(Tex t, int x, int y)
    {
        for (int j = 0; j < t.H; j++)
            for (int i = 0; i < t.W; i++)
            {
                uint c = t.Px[j * t.W + i];
                if (Col.A(c) != 0 && (uint)(x + i) < W && (uint)(y + j) < H) Fb[(y + j) * W + x + i] = c;
            }
    }

    void DrawVictory(Game g)
    {
        StoneBackdrop(g.Time);
        CenterText("VICTORY!", 30, Col.Rgb(255, 220, 90), 4);
        var p = g.P;
        int t = (int)g.PlayTime;
        uint stat = Col.Rgb(170, 200, 255);
        if (g.StoryMode)
        {
            CenterText("EVERY CASE CLOSED.", 72, Col.Rgb(230, 220, 200));
            CenterText($"{Story.Town.ToUpperInvariant()} SLEEPS A LITTLE EASIER TONIGHT.", 84, Col.Rgb(230, 220, 200));
            CenterText("YOU HANG UP THE COAT, UNTIL THE NEXT CALL.", 96, Col.Rgb(230, 220, 200));
            CenterText($"CASES: {Story.Cases.Length}    TIME ON THE LAST JOB: {t / 60}:{t % 60:00}", 120, stat);
        }
        else if (g.Relaxed)
        {
            CenterText(Words.T("EVERY RELIC IS FOUND."), 72, Col.Rgb(230, 220, 200));
            CenterText($"THE {p.Def.Name.ToUpperInvariant()} STEPS THROUGH THE PORTAL, AT PEACE.", 84, Col.Rgb(230, 220, 200));
            CenterText(Words.T($"RELICS: {p.Relics}/{g.RelicsTotal}    LORE: {p.LoreRead}/{g.LoreTotal}    SECRETS: {p.Secrets}/{g.SecretsTotal}"), 108, stat);
            CenterText($"EXPLORED: {(int)(Discovery.Explored(g.Hub) * 100)}%    CHESTS: {p.ChestsOpened}/{g.ChestsTotal}    TIME: {t / 60}:{t % 60:00}", 120, stat);
        }
        else
        {
            CenterText(Words.T("THE HERESIARCH HAS FALLEN."), 80, Col.Rgb(230, 220, 200));
            CenterText($"THE {p.Def.Name.ToUpperInvariant()} STEPS THROUGH THE PORTAL...", 94, Col.Rgb(230, 220, 200));
            CenterText($"KILLS: {p.Kills}    CHESTS: {p.ChestsOpened}/{g.ChestsTotal}    TIME: {t / 60}:{t % 60:00}", 116, stat);
            CenterText($"SECRETS: {p.Secrets}/{g.SecretsTotal}    LORE: {p.LoreRead}/{g.LoreTotal}", 128, stat);
        }
        if (!g.TestingMap)
            CenterText($"LEVEL {g.Profile.Level}    +{g.RunXp} XP THIS RUN", 144, g.Profile.Points > 0 ? Col.Rgb(120, 255, 140) : Col.Rgb(230, 190, 80));
        CenterText("PRESS ENTER", 160, Col.Rgb(255, 230, 120), 2);
    }
}
