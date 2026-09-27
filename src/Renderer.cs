namespace HexenSharp;

/// <summary>
/// Pure software renderer into a 320x200 framebuffer: a grid raycaster with textured floors and
/// ceilings, sky, fog, rising doors / see-through gates, depth-buffered sprites, HUD and automap.
/// </summary>
public sealed class Renderer
{
    public const int W = 320, H = 200, HudH = 32, ViewH = H - HudH;
    float PlaneLen = 0.75f;                    // set from the fov setting each frame
    float Proj = (W / 2f) / 0.75f;             // pixels per world unit at distance 1
    float _fogDist;
    bool _fullBright;

    public readonly uint[] Fb = new uint[W * H];
    readonly float[] _depth = new float[W * ViewH];
    readonly List<(float d, int side, float wallX, int cell)> _doors = new();
    readonly List<(Thing t, float depth)> _sprites = new();

    // per-frame camera
    float _px, _py, _dirX, _dirY, _plX, _plY, _eyeZ, _horizon;

    public void Render(Game g)
    {
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
        if (g.ShowMap) DrawAutomap(g);
        DrawHud(g);
        DrawMessages(g);
        if (g.ReadingLore != null) DrawLore(g);
        DrawArenaHud(g);

        if (g.Mode == GameMode.Dead && g.P.EyeZ <= 0.13f)
            CenterText("YOU DIED", 60, Col.Rgb(220, 40, 30), 3);
        if (g.Mode == GameMode.Dead && g.P.EyeZ <= 0.13f)
            CenterText("PRESS ENTER TO TRY AGAIN", 90, Col.Rgb(230, 220, 200));
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
        if (g.Mode != GameMode.Title) Darken(0, 0, W, H, page == MenuPage.Pause ? 150 : 230);

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
                    0 => new[] { "FIGHT YOUR WAY THROUGH THE HUB,", "SOLVE ITS PUZZLES AND SLAY THE HERESIARCH." },
                    1 => new[] { "NO COMBAT: THE CREATURES ARE PEACEFUL.", "EXPLORE, READ LORE STONES, UNCOVER SECRETS", "AND FIND THE HIDDEN RELICS." },
                    _ => Array.Empty<string>(),
                };
                for (int i = 0; i < about.Length; i++) CenterText(about[i], 128 + i * 10, Col.Rgb(170, 200, 255));
                CenterText("ARROWS + ENTER    ESC: BACK", 186, MenuDim);
                break;

            case MenuPage.Bindings:
                DrawBindings(g);
                break;
        }

        if (m.NoticeTime > 0 && page != MenuPage.Bindings) CenterText(m.Notice.ToUpperInvariant(), 166, Col.Rgb(120, 255, 140));
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
        var p = g.P;
        _px = p.X; _py = p.Y;
        _dirX = MathF.Cos(p.Angle); _dirY = MathF.Sin(p.Angle);
        PlaneLen = MathF.Tan(g.Vars.Fov * MathF.PI / 360f);
        Proj = (W / 2f) / PlaneLen;
        _fogDist = th.FogDist * g.Vars.Fog;
        _fullBright = g.Vars.FullBright;
        _plX = -_dirY * PlaneLen; _plY = _dirX * PlaneLen;
        _eyeZ = p.ViewZ + MathF.Sin(p.Bob) * 0.025f * p.BobAmount;
        _horizon = ViewH / 2f + p.Pitch;
        uint fog = th.FogColor;
        int baseLight = Light(th);
        int hitCell = -1;

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

            _doors.Clear();
            float perp = 64f, wallX = 0;
            int side = 0;
            char hit = '#';
            if (lv.InBounds(mapX, mapY)) lv.Seen[mapY * lv.W + mapX] = true;
            for (int guard = 0; guard < 96; guard++)
            {
                if (sideX < sideY) { sideX += ddx; mapX += stepX; side = 0; }
                else { sideY += ddy; mapY += stepY; side = 1; }
                if (!lv.InBounds(mapX, mapY)) { perp = side == 0 ? sideX - ddx : sideY - ddy; break; }
                int ci = mapY * lv.W + mapX;
                lv.Seen[ci] = true;
                char c = lv.Cells[ci];
                if (c == '\0') continue;
                float d = side == 0 ? sideX - ddx : sideY - ddy;
                float wx = side == 0 ? _py + d * rdy : _px + d * rdx;
                wx -= MathF.Floor(wx);
                if (Level.IsDoor(c) && (lv.DoorOpen[ci] > 0f || c == 'P'))
                {
                    if (lv.DoorOpen[ci] < 1f) _doors.Add((d, side, wx, ci));
                    continue;
                }
                perp = d; wallX = wx; hit = c; hitCell = ci;
                break;
            }
            if (perp < 0.01f) perp = 0.01f;

            // ---- solid wall column
            var tex = WallTex(lv, hit, hitCell);
            int tx = (int)(wallX * tex.W);
            if (side == 0 && rdx < 0) tx = tex.W - 1 - tx;
            if (side == 1 && rdy > 0) tx = tex.W - 1 - tx;
            tx = Math.Clamp(tx, 0, tex.W - 1);
            float hScale = Proj / perp;
            float top = _horizon - (1f - _eyeZ) * hScale, bot = _horizon + _eyeZ * hScale;
            int yTop = Math.Max(0, (int)MathF.Ceiling(top - 0.5f)), yBot = Math.Min(ViewH, (int)MathF.Ceiling(bot - 0.5f));
            int light = side == 1 ? baseLight * 200 >> 8 : baseLight;
            int vis = Vis(th, perp);
            float vStep = 1f / hScale;
            for (int y = yTop; y < yBot; y++)
            {
                float v = (y + 0.5f - top) * vStep;
                int ty = Math.Clamp((int)(v * tex.H), 0, tex.H - 1);
                int idx = y * W + x;
                Fb[idx] = Col.Fog(tex.Px[ty * tex.W + tx], light, vis, fog);
                _depth[idx] = perp;
            }

            // ---- floor
            for (int y = Math.Max(yBot, 0); y < ViewH; y++)
            {
                float dy = y + 0.5f - _horizon;
                int idx = y * W + x;
                if (dy <= 0.01f) { Fb[idx] = fog; _depth[idx] = 999; continue; }
                float rowDist = _eyeZ * Proj / dy;
                float wx = _px + rdx * rowDist, wy = _py + rdy * rowDist;
                int cx = (int)MathF.Floor(wx), cy = (int)MathF.Floor(wy);
                Tex ft = th.FloorIn;
                int fl = baseLight;
                if (lv.InBounds(cx, cy))
                {
                    int ci = cy * lv.W + cx;
                    char mk = lv.Marks[ci];
                    if (mk == 'E') { ft = lv.BossDead ? Art.ExitFloor : Art.ExitFloorOff; fl = 300; }
                    else if (mk == '*') { ft = Art.SpawnFloor; fl = 280; }
                    else if (mk == '^') ft = Art.PlateFloor;
                    else if (mk == '!') { ft = lv.Arena?.Started == true ? Art.AltarFloorOff : Art.AltarFloor; fl = 300; }
                    else if (mk != '\0') { ft = Art.PortalFloor; fl = 300; }
                    else if (lv.Outdoor[ci]) ft = th.OutdoorFloor;
                }
                int u = (int)((wx - cx) * ft.W) & (ft.W - 1), vv = (int)((wy - cy) * ft.H) & (ft.H - 1);
                Fb[idx] = Col.Fog(ft.Px[vv * ft.W + u], fl, Vis(th, rowDist), fog);
                _depth[idx] = rowDist;
            }

            // ---- ceiling / sky
            float rayAng = MathF.Atan2(rdy, rdx);
            int yCeilEnd = Math.Min(yTop, ViewH);
            for (int y = 0; y < yCeilEnd; y++)
            {
                float dy = _horizon - (y + 0.5f);
                int idx = y * W + x;
                _depth[idx] = 999;
                bool sky;
                float rowDist = 0;
                int cx = 0, cy = 0;
                if (dy <= 0.01f) sky = true;
                else
                {
                    rowDist = (1f - _eyeZ) * Proj / dy;
                    float wx = _px + rdx * rowDist, wy = _py + rdy * rowDist;
                    cx = (int)MathF.Floor(wx); cy = (int)MathF.Floor(wy);
                    sky = lv.InBounds(cx, cy) && lv.Outdoor[cy * lv.W + cx];
                    if (!sky)
                    {
                        var ct = th.CeilIn;
                        int u = (int)((wx - cx) * ct.W) & (ct.W - 1), vv = (int)((wy - cy) * ct.H) & (ct.H - 1);
                        Fb[idx] = Col.Fog(ct.Px[vv * ct.W + u], baseLight * 220 >> 8, Vis(th, rowDist), fog);
                        _depth[idx] = rowDist;
                        continue;
                    }
                }
                Fb[idx] = SkyPixel(th, rayAng, y);
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
                float s = Proj / d;
                float dTop = _horizon - (1f - _eyeZ) * s;
                float dBot = _horizon - (open - _eyeZ) * s;
                int y0 = Math.Max(0, (int)MathF.Ceiling(dTop - 0.5f)), y1 = Math.Min(ViewH, (int)MathF.Ceiling(dBot - 0.5f));
                int dl = dside == 1 ? baseLight * 200 >> 8 : baseLight;
                int dv = Vis(th, d);
                for (int y = y0; y < y1; y++)
                {
                    int idx = y * W + x;
                    if (d >= _depth[idx]) continue;
                    float z = _eyeZ + (_horizon - (y + 0.5f)) / s;
                    float v = 1f - z + open;
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
            float left = screenX - sw / 2, top = _horizon - (t.Z + t.SpriteH - _eyeZ) * scale;
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

    void DrawWeapon(Game g)
    {
        var p = g.P;
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
        Text(W - 4 - Font.Width(status), g.Vars.ShowFps ? 12 : 3, status, Col.Rgb(230, 120, 255));
        if (a.BannerTime > 0)
        {
            string big = a.InIntermission ? "WAVE CLEARED!" : $"WAVE {a.Wave}";
            CenterText(big, 40, a.InIntermission ? Col.Rgb(120, 255, 140) : Col.Rgb(255, 90, 60), 3);
            if (!a.InIntermission && a.Wave % 5 == 0) CenterText("A HERESIARCH APPROACHES", 68, Col.Rgb(230, 120, 255));
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
        var hb = Art.HudBack;
        for (int y = 0; y < HudH; y++)
            for (int x = 0; x < W; x++)
                Fb[(ViewH + y) * W + x] = Col.Shade(hb.Px[(y & (hb.H - 1)) * hb.W + (x & (hb.W - 1))], y == 0 ? 400 : y == 1 ? 60 : 200);

        int by = ViewH + 3;
        uint label = Col.Rgb(200, 180, 140);
        if (g.Relaxed) { DrawDiscoveryHud(g, by, label); return; }
        Text(6, by, "HEALTH", label);
        uint hcol = p.Health > 50 ? Col.Rgb(240, 230, 210) : p.Health > 25 ? Col.Rgb(250, 200, 60) : Col.Rgb(250, 60, 40);
        Text(8, by + 11, p.Health.ToString(), hcol, 2);

        Text(52, by, "ARMOR", label);
        Text(54, by + 11, p.Armor.ToString(), Col.Rgb(170, 190, 230), 2);

        // mana bars
        ManaBar(96, by + 1, "BLUE", p.BlueMana, Col.Rgb(60, 120, 255));
        ManaBar(96, by + 14, "GREEN", p.GreenMana, Col.Rgb(60, 210, 80));

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
        Stat(6, "RELICS", p.Relics, g.RelicsTotal);
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
        string title = "LORE STONE";
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
        CenterText("A TINY HEXEN-STYLE DUNGEON CRAWLER IN C#", 68, Col.Rgb(210, 200, 180));
        // a few monsters for show
        var e = Art.Monsters["ettin"][(int)(g.Time * 2) % 2];
        var a = Art.Monsters["afrit"][(int)(g.Time * 3) % 2];
        var c = Art.Monsters["centaur"][(int)(g.Time * 2) % 2];
        Icon(e, 40, 82, 64); Icon(c, 128, 82, 64); Icon(a, 216, 80, 64);
        var items = g.Menu.Items(MenuPage.Main);
        for (int i = 0; i < items.Length; i++) MenuItem(items[i], 150 + i * 12, g.Menu.Page == MenuPage.Main && i == g.Menu.Cursor);
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
            string mana = w.Mana == 0 ? "" : w.Mana == 1 ? " (BLUE MANA)" : " (GREEN MANA)";
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
            CenterText("EVERY RELIC IS FOUND.", 72, Col.Rgb(230, 220, 200));
            CenterText($"THE {p.Def.Name.ToUpperInvariant()} STEPS THROUGH THE PORTAL, AT PEACE.", 84, Col.Rgb(230, 220, 200));
            CenterText($"RELICS: {p.Relics}/{g.RelicsTotal}    LORE: {p.LoreRead}/{g.LoreTotal}    SECRETS: {p.Secrets}/{g.SecretsTotal}", 108, stat);
            CenterText($"EXPLORED: {(int)(Discovery.Explored(g.Hub) * 100)}%    CHESTS: {p.ChestsOpened}/{g.ChestsTotal}    TIME: {t / 60}:{t % 60:00}", 120, stat);
        }
        else
        {
            CenterText("THE HERESIARCH HAS FALLEN.", 80, Col.Rgb(230, 220, 200));
            CenterText($"THE {p.Def.Name.ToUpperInvariant()} STEPS THROUGH THE PORTAL...", 94, Col.Rgb(230, 220, 200));
            CenterText($"KILLS: {p.Kills}    CHESTS: {p.ChestsOpened}/{g.ChestsTotal}    TIME: {t / 60}:{t % 60:00}", 116, stat);
            CenterText($"SECRETS: {p.Secrets}/{g.SecretsTotal}    LORE: {p.LoreRead}/{g.LoreTotal}", 128, stat);
        }
        CenterText("PRESS ENTER", 160, Col.Rgb(255, 230, 120), 2);
    }
}
