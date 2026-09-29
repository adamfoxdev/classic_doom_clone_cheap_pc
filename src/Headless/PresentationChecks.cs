namespace HexenSharp;

/// <summary>Checks on how it looks, sounds and handles: the HUD, options, rendered art, sounds, music and the gamepad.</summary>
public static partial class Headless
{
    static void SoundChecks(Action<bool, string> check)
    {
        foreach (var style in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
        {
            var bad = new List<string>();
            for (int i = 0; i < (int)Sfx.Count; i++)
            {
                var a = Sounds.Make((Sfx)i, style);
                int peak = a.Max(x => Math.Abs((int)x));
                float clipped = a.Count(x => Math.Abs((int)x) >= 29990) / (float)a.Length;
                if (a.Length < 1000 || peak < 3000 || clipped > 0.05f) bad.Add($"{(Sfx)i} (len {a.Length}, peak {peak}, clipped {clipped:P0})");
            }
            check(bad.Count == 0, $"every {style} sound is audible and clean" + (bad.Count > 0 ? ": " + string.Join(", ", bad) : ""));
        }
        var same = Enumerable.Range(0, (int)Sfx.Count).Where(i => Sounds.Make((Sfx)i, ArtStyle.SciFi).SequenceEqual(Sounds.Make((Sfx)i, ArtStyle.Fantasy))).Select(i => (Sfx)i).ToList();
        check(same.Count == 0, "every sci-fi sound differs from its fantasy one" + (same.Count > 0 ? ": " + string.Join(", ", same) : ""));
        check(Sounds.Make(Sfx.Shoot, ArtStyle.SciFi).SequenceEqual(Sounds.Make(Sfx.Shoot, ArtStyle.SciFi)), "sounds synthesize the same every time");
        foreach (var style in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
        {
            var jet = Sounds.Make(Sfx.Jet, style);
            check(Math.Abs((int)jet[0]) < 1500 && Math.Abs((int)jet[^1]) < 1500, $"the {style} thrust sound fades at both ends, so it loops without clicks");
        }
        var wav = Sounds.Wav(new short[] { 1, -1 });
        check(wav.Length == 48 && wav[0] == 'R' && wav[8] == 'W' && BitConverter.ToInt32(wav, 24) == Sounds.Rate, "WAV export writes a valid header");
    }

    static void HudChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        g.NewGame(PClass.Fighter);
        g.Vars.Freeze = true;
        g.Update(default, 1f / 35f);
        g.Messages.Clear();
        var r = new Renderer();
        check(g.Vars.Hud == HudStyle.Full, "the classic status bar is the default HUD");

        // what each style draws in the bottom strip where the status bar lives, and how tall the 3D view is
        var strips = new Dictionary<HudStyle, uint[]>();
        var heights = new Dictionary<HudStyle, int>();
        foreach (var style in Enum.GetValues<HudStyle>())
        {
            g.Vars.Hud = style;
            r.Render(g);
            strips[style] = r.Fb[(Renderer.StatusViewH * Renderer.W)..];
            heights[style] = r.ViewH;
        }
        var hb = Art.HudBack;
        check(heights[HudStyle.Full] == Renderer.StatusViewH && strips[HudStyle.Full][Renderer.W * 10 + 150] != 0,
              "Full keeps the 3D view above the status bar");
        check(new[] { HudStyle.Compact, HudStyle.Minimal, HudStyle.Off }.All(h => heights[h] == Renderer.H),
              "the other styles drop the status bar and give the view the whole screen");
        int Diff(uint[] a, uint[] b) => a.Zip(b).Count(t => t.First != t.Second);
        check(Diff(strips[HudStyle.Full], strips[HudStyle.Off]) > Renderer.W * 20, "with the HUD off, the floor shows where the status bar was");
        check(Diff(strips[HudStyle.Compact], strips[HudStyle.Off]) > Diff(strips[HudStyle.Minimal], strips[HudStyle.Off])
              && Diff(strips[HudStyle.Minimal], strips[HudStyle.Off]) > 40,
              "Compact shows more than Minimal, and Minimal more than Off");
        g.P.Health = 20;
        g.Vars.Hud = HudStyle.Minimal;
        r.Render(g);
        var low = r.Fb[(Renderer.StatusViewH * Renderer.W)..];
        check(Diff(low, strips[HudStyle.Minimal]) > 0, "the minimal HUD still tracks your health");
        g.Vars.Hud = HudStyle.Full;

        // switching: the H key cycles through the styles, the options menu steps either way, and it's saved
        var seen = new List<HudStyle>();
        for (int i = 0; i < 4; i++) { g.Update(new Input { CycleHud = true }, 1f / 35f); seen.Add(g.Vars.Hud); }
        check(seen.SequenceEqual(new[] { HudStyle.Compact, HudStyle.Minimal, HudStyle.Off, HudStyle.Full }), "H cycles Full, Compact, Minimal, Off and back");
        check(g.Messages.Any(m => m.Item1.Contains("HUD: OFF")), "and says which style you're on");
        check(Bindings.Find("cyclehud") is { Key1: var k } && k == Keys.Letter('H'), "cycling the HUD is a rebindable action on H");
        g.Menu.Show(MenuPage.Options);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Options), "HUD style");
        check(g.Menu.Value(g.Menu.Cursor) == "FULL", "Options shows HUD style: FULL");
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        check(g.Vars.Hud == HudStyle.Off && g.Menu.Value(g.Menu.Cursor) == "OFF", "Left steps back to OFF");
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(g.Vars.Hud == HudStyle.Compact, "Right steps forward to COMPACT");
        g.Menu.Close();
        check(Settings.Lines(g).Contains("hud 1"), "the style is saved with the settings");
        g.Con.Execute("hud 2");
        check(g.Vars.Hud == HudStyle.Minimal, "'hud 2' sets it from the console");
        g.Con.Execute("hud 9");
        check(g.Vars.Hud == HudStyle.Off, "out-of-range values clamp");
        g.Vars.Hud = HudStyle.Full;

        // the crosshair: off by default, each style marks the centre of the view, and it follows vertical look
        check(g.Vars.Crosshair == CrosshairStyle.Off, "the crosshair is off by default");
        uint[] Centre(int pitch)
        {
            g.P.Pitch = pitch;
            r.Render(g);
            var box = new List<uint>();
            int cy = Renderer.StatusViewH / 2 + pitch;
            for (int y = cy - 7; y <= cy + 7; y++)
                for (int x = Renderer.W / 2 - 7; x <= Renderer.W / 2 + 7; x++) box.Add(r.Fb[y * Renderer.W + x]);
            return box.ToArray();
        }
        var bare = Centre(0);
        var marks = new Dictionary<CrosshairStyle, uint[]>();
        foreach (var cs in new[] { CrosshairStyle.Dot, CrosshairStyle.Cross, CrosshairStyle.Circle })
        {
            g.Vars.Crosshair = cs;
            marks[cs] = Centre(0);
        }
        check(marks.Values.All(m => Diff(m, bare) > 4), "dot, cross and circle each draw at the centre of the view");
        check(Diff(marks[CrosshairStyle.Dot], marks[CrosshairStyle.Cross]) > 0 && Diff(marks[CrosshairStyle.Cross], marks[CrosshairStyle.Circle]) > 0,
              "and they look different");
        g.Vars.Crosshair = CrosshairStyle.Cross;
        var lookUp = Centre(20);
        g.Vars.Crosshair = CrosshairStyle.Off;
        check(Diff(lookUp, Centre(20)) > 4, "it moves with the horizon when you look up or down");
        g.Vars.Crosshair = CrosshairStyle.Cross;
        g.ShowMap = true;
        var onMap = Centre(0);
        g.Vars.Crosshair = CrosshairStyle.Off;
        check(Diff(onMap, Centre(0)) == 0, "it's hidden on the automap");
        g.ShowMap = false;
        g.P.Pitch = 0;

        g.Menu.Show(MenuPage.Options);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Options), "Crosshair");
        check(g.Menu.Value(g.Menu.Cursor) == "OFF", "Options shows Crosshair: OFF");
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(g.Vars.Crosshair == CrosshairStyle.Cross && g.Menu.Value(g.Menu.Cursor) == "CROSS", "Right steps through DOT to CROSS");
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        check(g.Vars.Crosshair == CrosshairStyle.Circle, "and Left wraps round to CIRCLE");
        g.Menu.Close();
        check(Settings.Lines(g).Contains("crosshair 3"), "the crosshair is saved with the settings");
        g.Con.Execute("crosshair 1");
        check(g.Vars.Crosshair == CrosshairStyle.Dot, "'crosshair 1' sets it from the console");
        g.Vars.Crosshair = CrosshairStyle.Off;
    }

    static void RenderedArtChecks(Action<bool, string> check)
    {
        // the PNG reader round-trips what the screenshot writer saves
        var tmp = Path.Combine(Path.GetTempPath(), $"hexen_png_{Environment.ProcessId}.png");
        var src = new uint[37 * 23];
        for (int i = 0; i < src.Length; i++) src[i] = Col.Rgb(i * 7 % 256, i * 13 % 256, i * 29 % 256);
        Png.Save(tmp, src, 37, 23);
        var back = Png.Load(File.ReadAllBytes(tmp));
        File.Delete(tmp);
        check(back.W == 37 && back.H == 23 && back.Px.SequenceEqual(src), "PNG reader round-trips a saved image");

        var covered = RenderedArt.Covered.ToList();
        check(RenderedArt.Available && covered.Count == 31, $"the pack covers 11 pickups, 5 textures, all 6 monsters and all 9 weapons ({covered.Count})");
        check(new[] { "afrit", "ettin", "centaur", "slaughtaur", "bishop", "heresiarch" }.All(m => covered.Contains("monsters/" + m)), "every monster has rendered frames");
        bool shapes = true;
        foreach (var (file, png) in RenderedArt.Files)
        {
            var t = Png.Load(png);
            int clear = t.Px.Count(c => Col.A(c) == 0);
            shapes &= file.StartsWith("weapons/")
                ? t.W == 128 && t.H == 80 && clear > 128 * 80 / 3 && clear < 128 * 80 - 400
                : t.W == 64 && t.H == 64 && (file.StartsWith("textures/") ? clear == 0 : clear > 400 && clear < 64 * 64 - 300);
        }
        check(shapes, "every rendered asset is the right size: 64x64 sprites cut out, textures solid, 128x80 weapon frames");

        var g = new Game { FixedSeed = 1 };
        g.NewGame(PClass.Fighter);
        var hall = g.Level;
        check(!Art.Rendered && Art.Style == ArtStyle.SciFi, "rendered art is off by default");
        var procJet = Art.Jetpack;
        var procFist = Art.Weapons[0][0];
        g.SetRenderedArt(true);
        check(Art.Jetpack.Px.SequenceEqual(RenderedArt.Load("sprites/jetpack").Px) && !Art.Jetpack.Px.SequenceEqual(procJet.Px),
              "turning it on swaps in the rendered jetpack");
        check(hall.Theme.Walls['#'] == Art.Stone && Art.Stone.Px.SequenceEqual(RenderedArt.Load("textures/stone").Px),
              "maps pick up the rendered wall textures straight away");
        var drone = Art.Monsters["afrit"];
        check(drone.Length == 7 && drone[0].Px.SequenceEqual(RenderedArt.Load("monsters/afrit_walk0").Px) && drone[(int)Pose.Dead] != null,
              "the drone uses its rendered frames, with death frames derived from them");
        check(Art.Pillar != null && Art.Monsters["ettin"].Length == 7, "art the pack doesn't cover stays procedural");
        string[] classes = { "fighter", "cleric", "mage" };
        check(Enumerable.Range(0, 9).All(i => Art.Weapons[i].Length == 2
                  && Art.Weapons[i][0].Px.SequenceEqual(RenderedArt.Load($"weapons/{classes[i / 3]}_{i % 3}_idle").Px)
                  && Art.Weapons[i][1].Px.SequenceEqual(RenderedArt.Load($"weapons/{classes[i / 3]}_{i % 3}_fire").Px)),
              "every class's weapons use their rendered idle and firing frames");
        g.SetArtStyle(ArtStyle.Fantasy);
        check(!Art.Jetpack.Px.SequenceEqual(RenderedArt.Load("sprites/jetpack").Px)
              && !Art.Weapons[0][0].Px.SequenceEqual(RenderedArt.Load("weapons/fighter_0_idle").Px), "the fantasy style ignores the sci-fi pack");
        g.SetArtStyle(ArtStyle.SciFi);
        check(Art.Jetpack.Px.SequenceEqual(RenderedArt.Load("sprites/jetpack").Px), "and it comes back with the sci-fi style");
        check(Settings.Lines(g).Contains("renderedart 1"), "the choice is saved with the settings");

        // the options menu and console toggle it
        g.Menu.Show(MenuPage.Options);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Options), "Rendered art");
        check(g.Menu.Value(g.Menu.Cursor) == "ON", "Options shows Rendered art: ON");
        g.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(!Art.Rendered && g.Menu.Value(g.Menu.Cursor) == "OFF" && Art.Jetpack.Px.SequenceEqual(procJet.Px)
              && Art.Weapons[0][0].Px.SequenceEqual(procFist.Px), "Left/Right in Options turns it off, weapons included");
        g.Menu.Close();
        g.Con.Execute("renderedart 1");
        check(Art.Rendered, "'renderedart 1' turns it on from the console");
        g.Con.Execute("renderedart 0");
        check(!Art.Rendered && !Art.Stone.Px.SequenceEqual(RenderedArt.Load("textures/stone").Px), "'renderedart 0' turns it off again");
    }

    static void MusicChecks(Action<bool, string> check)
    {
        // a loop for every theme, the title and the practice courses, in both styles
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tracks = MusicGen.Tracks.ToArray();
        var made = new Dictionary<(string, ArtStyle), short[]>();
        foreach (var t in tracks)
            foreach (var st in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
                made[(t, st)] = MusicGen.Make(t, st);
        sw.Stop();
        check(Maps.ThemeIds.All(id => tracks.Contains(id)) && tracks.Contains("title") && tracks.Contains("practice"),
              $"a track for every map theme, the title and practice ({tracks.Length})");
        bool lengths = true, loud = true, headroom = true, seams = true;
        foreach (var ((t, st), s) in made)
        {
            float secs = s.Length / (float)MusicGen.Rate;
            lengths &= s.Length == MusicGen.Length(t) && secs > 12 && secs < 40;
            double rms = Math.Sqrt(s.Average(x => (double)x * x)) / 32767;
            loud &= rms > 0.05;
            int peak = s.Max(x => Math.Abs((int)x));
            headroom &= peak > 20000 && peak < 24000;
            // the loop joins up: the jump from the last sample back to the first is no bigger than the track's own jumps
            int maxStep = 0;
            for (int i = 1; i < s.Length; i++) maxStep = Math.Max(maxStep, Math.Abs(s[i] - s[i - 1]));
            seams &= Math.Abs(s[0] - s[^1]) <= maxStep;
        }
        check(lengths, "each loops 8 bars, between 12 and 40 seconds");
        check(loud && headroom, "not silent, and peaking at 70%, leaving room for the sound effects");
        check(seams, "every loop joins up without a click");
        check(MusicGen.Make("crypt", ArtStyle.Fantasy).SequenceEqual(made[("crypt", ArtStyle.Fantasy)]), "the same every time");
        check(!made[("hall", ArtStyle.Fantasy)].Take(5000).SequenceEqual(made[("hall", ArtStyle.SciFi)].Take(5000)) &&
              made.Values.Select(v => v.Length).Distinct().Count() > 4, "each style and theme sounds different");
        check(sw.ElapsedMilliseconds < 20000, $"all {made.Count} made in {sw.ElapsedMilliseconds} ms");
        check(MusicGen.Resolve("mycustomtheme") == "hall" && MusicGen.Resolve(null) == "hall", "an unknown theme plays the hall's");

        // the mixer: loops the track at the volume, crossfades to the next, and goes quiet for none
        var mx = new MusicMixer();
        mx.Play("hall", ArtStyle.Fantasy);
        var buf = new short[1000];
        mx.Fill(buf, 0.5f);
        var hall = mx.Get("hall", ArtStyle.Fantasy);
        check(buf.Select((v, i) => Math.Abs(v - hall[i] * 0.5f) <= 1).All(b => b), "it plays the track at the volume asked");
        mx.Play("ice", ArtStyle.Fantasy);
        var fade = new short[(int)(MusicMixer.Fade * MusicGen.Rate) + 10];
        mx.Fill(fade, 1f);
        var ice = mx.Get("ice", ArtStyle.Fantasy);
        int k = fade.Length - 1;
        check(fade[k] == ice[k] && fade[5] != ice[5], "switching crossfades over a second to the new track");
        mx.Play("ice", ArtStyle.Fantasy);
        check(mx.Track == "ice", "asking for the same track again doesn't restart it");
        mx.Play(null, ArtStyle.Fantasy);
        mx.Fill(fade, 1f); mx.Fill(buf, 1f);
        check(buf.All(v => v == 0), "and no track fades to silence");

        // what the game asks for
        var g = new Game { FixedSeed = 1 };
        string title = g.MusicTrack;
        g.NewGame(PClass.Fighter);
        string hub = g.MusicTrack;
        g.StartPractice(PClass.Fighter);
        string practice = g.MusicTrack;
        g.GoToTitle(); g.StartArena(PClass.Fighter);
        string arena = g.MusicTrack;
        check(title == "title" && hub == "hall" && practice == "practice" && arena == "arena", $"the title, each map and practice have their own ({title}, {hub}, {practice}, {arena})");
        g.GoToTitle();
        g.Menu.Show(MenuPage.Options);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Options), "Music volume");
        float m0 = g.Vars.Music;
        g.Menu.Update(new Input { Left = true }, 1f / 35f);
        check(MathF.Abs(g.Vars.Music - (m0 - 0.1f)) < 0.001f && g.Menu.Value(g.Menu.Cursor) == $"{g.Vars.Music * 100:0}%", "Options > Music volume turns it down");
        g.Vars.Music = 0;
        check(g.Menu.Value(g.Menu.Cursor) == "OFF" && Settings.Lines(g).Contains("music 0"), "all the way to off, and it's saved");
        g.Vars.Music = 0.6f;
        g.Menu.Close();
    }

    static void RenderChecks(Action<bool, string> check)
    {
        // the 3D view is drawn by several threads at once, a block of columns each: it has to be the same picture
        var g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.NewGame(PClass.Fighter);
        g.Vars.Freeze = true;
        var r = new Renderer();
        int cores = Renderer.Threads, same = 0;
        foreach (var (name, map, x, y, floor, z, angle, pitch) in BenchViews)
        {
            g.Warp(Array.FindIndex(g.Hub, l => l.RawName == map));
            if (x > 0) { g.P.X = x; g.P.Y = y; }
            g.P.FloorZ = floor; g.P.Z = z; g.P.Flying = z > 0; g.P.Angle = angle; g.P.Pitch = pitch; g.P.TeleportFlash = 0;
            Renderer.Threads = 1; r.Render(g); var one = (uint[])r.Fb.Clone();
            Renderer.Threads = 8; r.Render(g);
            if (one.AsSpan().SequenceEqual(r.Fb)) same++;
        }
        Renderer.Threads = cores;
        check(same == BenchViews.Length, $"one thread or eight draw exactly the same picture ({same} of {BenchViews.Length} views)");
        g.Con.Execute("renderthreads 2");
        check(Renderer.Threads == 2 && Settings.Lines(g).Contains("renderthreads 2"), "'renderthreads' sets how many, and it's saved");
        Renderer.Threads = cores;
    }

    static void GamepadChecks(Action<bool, string> check)
    {
        check(Gamepad.Stick(0.15f) == 0 && Gamepad.Stick(1f) == 1 && MathF.Abs(Gamepad.Stick(-0.6f) + 0.5f) < 0.001f, "sticks have a dead zone, and still reach full tilt");
        var pad = new Gamepad();
        Input Frame(PadState s, bool menu = false, Input start = default) { var i = start; pad.Apply(ref i, s, 1f / 60f, menu); return i; }
        var none = new PadState { Connected = true };

        var i1 = Frame(new PadState { Connected = true, LY = -1, LX = 1, RT = 1, LT = 1 });
        check(i1.Move == 1 && i1.Strafe == 1 && i1.Fire && i1.JetHeld, "left stick moves, RT attacks, LT flies");
        check(Frame(new PadState { Connected = true, LY = -1 }, start: new Input { Move = 1 }).Move == 1, "and adds to the keyboard without going over full speed");
        var a1 = Frame(new PadState { Connected = true, Held = PadButton.A });
        var a2 = Frame(new PadState { Connected = true, Held = PadButton.A });
        check(a1.Jump && a1.Confirm && a1.JumpHeld && !a2.Jump && a2.JumpHeld, "A jumps (and confirms) once a press, and holds");
        Frame(none);
        var bGame = Frame(new PadState { Connected = true, Held = PadButton.B });
        Frame(none);
        var bMenu = Frame(new PadState { Connected = true, Held = PadButton.B }, menu: true);
        check(bGame.Slide && !bGame.Pause && bMenu.Pause, "B slides in play, and backs out of menus");
        Frame(none);
        var face = Frame(new PadState { Connected = true, Held = PadButton.X | PadButton.Y | PadButton.RB | PadButton.Start | PadButton.Back });
        check(face.Use && face.UseItem && face.Cycle == 1 && face.Pause && face.Map, "X uses, Y uses an item, RB next weapon, Start pauses, Back the map");
        Frame(none);
        var f1 = Frame(new PadState { Connected = true, LY = 1 }, menu: true);
        var f2 = Frame(new PadState { Connected = true, LY = 1 }, menu: true);
        var d1 = Frame(new PadState { Connected = true, Held = PadButton.Down }, menu: true);
        check(f1.Down && !f2.Down && d1.Down, "in menus, a stick flick or the d-pad steps once");
        var off = new Input { Move = 0.5f };
        pad.Apply(ref off, new PadState { Connected = false, LY = -1, Held = PadButton.A }, 1f / 60f, false);
        check(off.Move == 0.5f && !off.Jump, "no pad, no change");

        // the right stick turns at the same rate at any frame rate
        float Turned(float fps)
        {
            var g = new Game { FixedSeed = 1 };
            g.NewGame(PClass.Fighter);
            g.Level.Things.RemoveAll(t => t is Monster);
            var p2 = new Gamepad();
            float a0 = g.P.Angle;
            for (int f = 0; f < (int)fps; f++) { var i = new Input(); p2.Apply(ref i, new PadState { Connected = true, RX = 1 }, 1f / fps, false); g.Update(i, 1f / fps); }
            return g.P.Angle - a0;
        }
        float t60 = Turned(60), t144 = Turned(144);
        check(MathF.Abs(t60 - Gamepad.TurnRate) < 0.05f && MathF.Abs(t144 - t60) < 0.05f, $"right stick turns {Gamepad.TurnRate} radians a second at full tilt ({t60:0.00} at 60 fps, {t144:0.00} at 144)");

        // driving the title menu and the arena's perk choice with a pad
        var m = new Game { FixedSeed = 1 };
        var mp = new Gamepad();
        void Pad(PadState s) { var i = new Input(); mp.Apply(ref i, s, 1f / 35f, m.Menu.Open || m.Mode is GameMode.Title or GameMode.ClassSelect); m.Update(i, 1f / 35f); }
        Pad(none);
        Pad(new PadState { Connected = true, Held = PadButton.Down }); Pad(none);
        Pad(new PadState { Connected = true, Held = PadButton.A }); Pad(none);
        check(m.Menu.Page == MenuPage.Courses, "the d-pad and A pick Practice on the title menu");
        Pad(new PadState { Connected = true, Held = PadButton.B }); Pad(none);
        check(m.Menu.Page == MenuPage.Main, "and B goes back");
        m.StartArena(PClass.Fighter);
        var ar = m.Level.Arena;
        ar.Offer = new[] { Perk.Might, Perk.Swiftness, Perk.Vitality };
        Pad(none);
        Pad(new PadState { Connected = true, Held = PadButton.Right }); Pad(none);
        Pad(new PadState { Connected = true, Held = PadButton.A });
        check(ar.Offer == null && ar.Rank(Perk.Swiftness) == 1, "d-pad right and A take the second perk");
    }

    static void OptionsChecks(Action<bool, string> check)
    {
        var g = new Game { FixedSeed = 1 };
        var keys = new FakeKeys();
        // press one key for a frame, the way the real host reports it
        void Press(int code, string typed = null)
        {
            keys.Hit.Add(code);
            var inp = g.Binds.Read(keys, g.Con.Open);
            inp.KeyPressed = code; inp.Typed = typed;
            g.Update(inp, 1f / 35f);
            keys.Hit.Clear();
            g.Update(g.Binds.Read(keys, g.Con.Open), 1f / 35f);
        }
        Input Read() => g.Binds.Read(keys, false);

        // defaults
        keys.Held.Add(Keys.Letter('W')); keys.Held.Add(Keys.Mouse1);
        var r = Read();
        check(r.Move == 1 && r.Fire, "default W moves forward, left mouse attacks");
        keys.Held.Clear();
        keys.Hit.Add(Keys.WheelDown); check(Read().Cycle == 1, "mouse wheel cycles weapons"); keys.Hit.Clear();
        keys.Hit.Add(Keys.Space); check(Read().Jump, "Space jumps"); keys.Hit.Clear();

        // title menu: New game / Practice / Arena / Story / Online / Leaderboard / Character / Options / Quit
        check(g.Menu.Page == MenuPage.Main, "title shows the main menu");
        for (int k = 0; k < 7; k++) Press(Keys.Down);
        Press(Keys.Enter);
        check(g.Menu.Page == MenuPage.Options, "main menu opens Options");
        Press(Keys.Escape);
        check(g.Menu.Page == MenuPage.Main && g.Menu.Cursor == 7, "Esc goes back to the main menu");
        for (int k = 0; k < 7; k++) Press(Keys.Up);
        Press(Keys.Enter);
        check(g.Menu.Page == MenuPage.Style, "New game asks for a play style");
        Press(Keys.Enter);
        check(g.Mode == GameMode.ClassSelect && g.Style == GameStyle.Classic, "Classic goes to class select");
        Press(Keys.Enter);
        check(g.Mode == GameMode.Playing, "choosing a class starts the game");

        // Esc in game: pause menu -> Options -> Key bindings
        Press(Keys.Escape);
        check(g.Paused && g.Menu.Page == MenuPage.Pause, "Esc during play opens the pause menu");
        Press(Keys.Down); Press(Keys.Down); Press(Keys.Enter);
        check(g.Menu.Page == MenuPage.Options, "pause menu opens Options");
        Press(Keys.Enter);
        check(g.Menu.Page == MenuPage.Bindings, "Options opens Key bindings");

        // rebind Jump (primary) to J
        int jump = (int)Act.Jump;
        for (int k = 0; k < jump; k++) Press(Keys.Down);
        check(g.Menu.Cursor == jump, "cursor on Jump");
        Press(Keys.Enter);
        check(g.Menu.Capturing, "Enter waits for a new key");
        Press(Keys.Letter('J'));
        check(!g.Menu.Capturing && g.Binds.Get(Act.Jump, 0) == Keys.Letter('J'), "pressing J binds Jump to J");
        keys.Hit.Add(Keys.Letter('J')); check(Read().Jump, "J now jumps"); keys.Hit.Clear();
        keys.Hit.Add(Keys.Space); check(!Read().Jump, "Space no longer jumps"); keys.Hit.Clear();

        // second slot, conflicts and reserved keys
        Press(Keys.Right); Press(Keys.Enter); Press(Keys.Letter('E'));
        check(g.Binds.Get(Act.Jump, 1) == Keys.Letter('E') && g.Binds.Get(Act.Use, 0) == Keys.None, "binding E to Jump takes it off Use");
        check(g.Menu.Notice.Contains("removed from Use"), "the conflict is reported");
        Press(Keys.Enter); Press(Keys.Escape);
        check(!g.Menu.Capturing && g.Binds.Get(Act.Jump, 1) == Keys.Letter('E'), "Esc cancels capture without changing the key");
        Press(Keys.Backspace);
        check(g.Binds.Get(Act.Jump, 1) == Keys.None, "Backspace clears a slot");
        Press(Keys.Enter); Press(Keys.Mouse2);
        check(g.Binds.Get(Act.Jump, 1) == Keys.Mouse2, "mouse buttons can be bound");
        Press(Keys.Enter); Press(Keys.Escape); Press(Keys.Enter); Press(Keys.Escape);
        check(Keys.Reserved(Keys.Escape) && g.Binds.Get(Act.Jump, 1) == Keys.Mouse2, "Escape can't be bound (it cancels instead)");

        // settings file round trip
        string path = Path.Combine(Path.GetTempPath(), $"hexensharp-test-{Environment.ProcessId}.cfg");
        g.ConfigPath = path;
        g.Vars.Sens = 1f;
        Press(Keys.Escape); // back to Options (saves)
        Press(Keys.Down); Press(Keys.Right); Press(Keys.Right);
        check(MathF.Abs(g.Vars.Sens - 1.2f) < 0.001f, "Right raises mouse sensitivity");
        Press(Keys.Down); Press(Keys.Enter);
        check(g.Vars.InvertMouse, "Enter toggles invert mouse");
        Press(Keys.Escape); // back to pause menu (saves)
        check(File.Exists(path), "leaving Options saves the settings file");
        var g2 = new Game { ConfigPath = path };
        g2.LoadSettings();
        check(g2.Binds.Get(Act.Jump, 0) == Keys.Letter('J') && g2.Binds.Get(Act.Jump, 1) == Keys.Mouse2
              && g2.Binds.Get(Act.Use, 0) == Keys.None, "bindings survive a restart");
        check(MathF.Abs(g2.Vars.Sens - 1.2f) < 0.001f && g2.Vars.InvertMouse, "options survive a restart");
        check(g2.Con.Log.Count == 1, "loading settings is silent in the console");
        File.Delete(path);

        // invert mouse flips looking up/down
        Press(Keys.Escape);
        check(!g.Paused && !g.Menu.Open, "Esc on the pause menu resumes");
        g.P.Pitch = 0;
        g.Update(new Input { LookY = 10 }, 1f / 35f);
        check(g.P.Pitch > 0, "with invert on, moving the mouse down looks up");
        g.Vars.InvertMouse = false; g.P.Pitch = 0;
        g.Update(new Input { LookY = 10 }, 1f / 35f);
        check(g.P.Pitch < 0, "with invert off, moving the mouse down looks down");

        // reset and console binding
        g.Con.Execute("bind use e f");
        check(g.Binds.Get(Act.Use, 0) == Keys.Letter('E') && g.Binds.Get(Act.Use, 1) == Keys.Letter('F')
              && g.Binds.Get(Act.UseItem, 0) == Keys.None, "console 'bind use e f' (moving F off Use item)");
        g.Con.Execute("bind jump escape");
        check(g.Binds.Get(Act.Jump, 0) == Keys.Letter('J'), "console refuses to bind Escape");
        g.Con.Execute("binddefaults");
        check(g.Binds.Get(Act.Jump, 0) == Keys.Space && g.Binds.Get(Act.UseItem, 0) == Keys.Letter('F'), "'binddefaults' restores the defaults");
        check(Bindings.All.All(b => Keys.Known(b.Key1)) && Bindings.All.Select(b => b.Id).Distinct().Count() == Bindings.Count,
              "every action has a named default key and a unique id");

        // pause menu: quit to title, then Quit from the main menu
        Press(Keys.Escape);
        for (int k = 0; k < 4; k++) Press(Keys.Down);
        Press(Keys.Enter);
        check(g.Mode == GameMode.Title && g.Menu.Page == MenuPage.Main, "Quit to title");
        Press(Keys.Up); Press(Keys.Enter);
        check(g.QuitRequested, "Quit exits");
    }
}
