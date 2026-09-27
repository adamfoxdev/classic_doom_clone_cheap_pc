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
        if (g.ShowMap) DrawAutomap(g);
        DrawHud(g);
        DrawMessages(g);
        if (g.ReadingLore != null) DrawLore(g);
        DrawArenaHud(g);

        if (g.Mode == GameMode.Dead && g.P.EyeZ <= 0.13f)
            CenterText("YOU DIED", 60, Col.Rgb(220, 40, 30), 3);
        if (g.Mode == GameMode.Dead && g.P.EyeZ <= 0.13f)
            CenterText(g.CanRespawn ? "PRESS ENTER TO RETURN TO THE CHECKPOINT" : "PRESS ENTER TO TRY AGAIN", 90, Col.Rgb(230, 220, 200));
        if (g.Vars.ShowFps) Text(W - 40, 3, $"{g.Fps:0} FPS", Col.Rgb(120, 255, 120));
    }

    // ================================================================ menus

    static readonly uint MenuSel = Col.Rgb(255, 220, 90), MenuText = Col.Rgb(200, 190, 170), MenuDim = Col.Rgb(150, 140, 120);

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
        if (g.Mode != GameMode.Title) Darken(0, 0, W, H, page == MenuPage.Pause ? 150 : page == MenuPage.Character ? 246 : 230);

        switch (page)
        {
            case MenuPage.Pause:
                CenterText("PAUSED", 34, Col.Rgb(230, 190, 80), 3);
                for (int i = 0; i < items.Length; i++) MenuItem(items[i], 70 + i * 14, i == m.Cursor);
                CenterText("ARROWS + ENTER    ESC: RESUME", 150, MenuDim);
                break;

            case MenuPage.Options:
                CenterText("OPTIONS", 16, Col.Rgb(230, 190, 80), 2);
                for (int i = 0; i < items.Length; i++)
                {
                    int y = 48 + i * 16;
                    bool sel = i == m.Cursor;
                    string label = items[i].ToUpperInvariant(), val = m.Value(i);
                    if (val == "") { MenuItem(label, y, sel); continue; }
                    if (sel) Rect(40, y - 3, 240, 13, Col.Rgb(70, 40, 20));
                    Text(48, y, label, sel ? MenuSel : MenuText);
                    string shown = sel ? $"< {val} >" : val;
                    Text(272 - Font.Width(shown), y, shown, sel ? MenuSel : Col.Rgb(170, 200, 255));
                }
                CenterText("UP/DOWN: SELECT  LEFT/RIGHT: CHANGE  ESC: BACK", 186, MenuDim);
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
        }

        if (m.NoticeTime > 0 && page is not (MenuPage.Bindings or MenuPage.Character))
            CenterText(m.Notice.ToUpperInvariant(), 166, Col.Rgb(120, 255, 140));
    }

    /// <summary>The character screen: level and experience, skills to spend points on, and your weapons' levels.</summary>
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
            int y = 46 + i * 13;
            bool sel = i == m.Cursor;
            if (i == Profile.Skills.Length) { MenuItem(items[i], y + 2, sel); break; }
            var s = Profile.Skills[i];
            int rank = pr.Rank(s);
            if (sel) Rect(14, y - 3, 292, 12, Col.Rgb(70, 40, 20));
            Text(20, y, items[i].ToUpperInvariant(), sel ? MenuSel : MenuText);
            for (int k = 0; k < Profile.MaxRank; k++)
                Rect(96 + k * 8, y, 6, 6, k < rank ? (sel ? MenuSel : gold) : Col.Rgb(60, 52, 44));
            Text(180, y, rank > 0 ? Profile.Effect(s, rank) : "-", rank > 0 ? blue : MenuDim);
        }

        var cls = g.P?.Class ?? PClass.Fighter;
        var def = ClassDef.All[(int)cls];
        Text(20, 128, $"WEAPONS ({def.Name.ToUpperInvariant()})", MenuDim);
        for (int slot = 0; slot < 3; slot++)
        {
            var w = pr.Weapon(cls, slot);
            int y = 139 + slot * 10;
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
            float baseZ = t is Projectile or Puff ? t.Z : lv.FloorAt(t.X, t.Y) + t.Z;
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
        if (g.Mode == GameMode.Dead || g.Relaxed) return; // relaxed mode: weapons stay sheathed
        int slot = p.Weapon;
        var frames = Art.Weapons[(int)p.Class * 3 + slot];
        var tex = p.FireAnim > 0.06f ? frames[1] : frames[0];
        int bx = (int)(MathF.Cos(p.Bob * 0.5f) * 5 * p.BobAmount);
        int by = (int)(MathF.Abs(MathF.Sin(p.Bob * 0.5f)) * 5 * p.BobAmount);
        int x0 = W / 2 - tex.W / 2 + 20 + bx;
        int y0 = ViewH - tex.H + 4 + by + (int)(p.Raise * tex.H);
        int light = Math.Max(g.Level.Theme.Light, 200);
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

    void DrawArenaHud(Game g)
    {
        var a = g.Level.Arena;
        if (a == null || !a.Started) return;
        string status = a.InIntermission ? $"WAVE {a.Wave} CLEARED" : $"WAVE {a.Wave}   ENEMIES {a.Remaining}";
        if (g.Vars.Hud != HudStyle.Off) Text(W - 4 - Font.Width(status), g.Vars.ShowFps ? 12 : 3, status, Col.Rgb(230, 120, 255));
        if (a.BannerTime > 0)
        {
            string big = a.InIntermission ? "WAVE CLEARED!" : $"WAVE {a.Wave}";
            CenterText(big, 40, a.InIntermission ? Col.Rgb(120, 255, 140) : Col.Rgb(255, 90, 60), 3);
            if (!a.InIntermission && a.Wave % 5 == 0) CenterText(Words.T("A HERESIARCH APPROACHES"), 68, Col.Rgb(230, 120, 255));
        }
        if (a.InIntermission) CenterText($"NEXT WAVE IN {MathF.Ceiling(a.Timer):0}", 80, Col.Rgb(240, 225, 170));
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
        switch (style)
        {
            case HudStyle.Full: DrawStatusBar(g, label); break;
            case HudStyle.Compact: DrawCompactHud(g, bottom); break;
            case HudStyle.Minimal: DrawMinimalHud(g, bottom); break;
        }
    }

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
        const int x0 = 28, y0 = 26, w = W - 56, h = 118;
        Rect(x0 - 2, y0 - 2, w + 4, h + 4, Col.Rgb(40, 26, 14));
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                int n = ((x * 7 + y * 13) ^ (x * y)) & 15;
                Fb[y * W + x] = Col.Rgb(206 + n - 8, 186 + n - 8, 142 + n - 8);
            }
        uint ink = Col.Rgb(60, 36, 20);
        string title = Words.T("LORE STONE");
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
        var line = "";
        foreach (var word in text.Split(' '))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > max) { yield return line; line = ""; }
            line = line.Length == 0 ? word : line + " " + word;
        }
        if (line.Length > 0) yield return line;
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
        foreach (var (text, _) in g.Messages)
            foreach (var line in Wrap(text, (W - 8) / Font.CharW))
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
        CenterText(Words.T("A TINY HEXEN-STYLE DUNGEON CRAWLER IN C#"), 68, Col.Rgb(210, 200, 180));
        // a few monsters for show
        var e = Art.Monsters["ettin"][(int)(g.Time * 2) % 2];
        var a = Art.Monsters["afrit"][(int)(g.Time * 3) % 2];
        var c = Art.Monsters["centaur"][(int)(g.Time * 2) % 2];
        Icon(e, 40, 82, 64); Icon(c, 128, 82, 64); Icon(a, 216, 80, 64);
        var items = g.Menu.Items(MenuPage.Main);
        for (int i = 0; i < items.Length; i++) MenuItem(items[i], 148 + i * 10, g.Menu.Page == MenuPage.Main && i == g.Menu.Cursor);
        CenterText("ARROWS + ENTER.  CONTROLS ARE IN OPTIONS.", 190, Col.Rgb(150, 140, 120));
    }

    void DrawClassSelect(Game g)
    {
        StoneBackdrop(g.Time);
        CenterText("CHOOSE YOUR CLASS", 10, Col.Rgb(230, 170, 50), 2);
        CenterText(g.Relaxed ? "RELAXED MODE" : "CLASSIC MODE", 28, g.Relaxed ? Col.Rgb(120, 255, 140) : Col.Rgb(200, 150, 120));
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
        Rect(170, 34, 140, 86, Col.Rgb(20, 16, 14));
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
        if (g.Relaxed)
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
