using System.Numerics;
using Raylib_cs;

namespace HexenSharp;

public static class Program
{
    public static int Main(string[] args)
    {
        Art.Init();
        if (args.Contains("--selftest")) return Headless.SelfTest();
        if (args.Contains("--shots")) return Headless.Screenshots(args.SkipWhile(a => a != "--shots").Skip(1).FirstOrDefault() ?? "shots");
        if (args.Contains("--sounds")) return Headless.ExportSounds(Arg(args, "--sounds") ?? "sounds");
        if (args.Contains("--check-map")) return Arg(args, "--check-map") is { } check ? MapFiles.Check(check) : Usage();
        if (args.Contains("--export-maps")) return MapFiles.Export(Arg(args, "--export-maps") ?? "maps");
        if (args.Contains("--export-editor-maps")) return MapFiles.ExportEditorMaps();

        // --play map.hxm [--class fighter|cleric|mage] [--relaxed]: play-test a map file, reloading it on every save
        string play = Arg(args, "--play");
        if (args.Contains("--play") && play == null) return Usage();
        var cls = (Arg(args, "--class") ?? "fighter").ToLowerInvariant() switch
        {
            "cleric" or "engineer" => PClass.Cleric,
            "mage" or "psion" => PClass.Mage,
            _ => PClass.Fighter,
        };
        RunWindow(play, cls, args.Contains("--relaxed"));
        return 0;
    }

    static string Arg(string[] args, string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault(a => !a.StartsWith("--"));

    static int Usage()
    {
        Console.WriteLine("usage: HexenSharp [--play map.hxm [--class fighter|cleric|mage] [--relaxed]] | --check-map map.hxm |");
        Console.WriteLine("       --export-maps dir | --export-editor-maps | --selftest | --shots dir | --sounds dir");
        return 2;
    }

    static void RunWindow(string play, PClass cls, bool relaxed)
    {
        const int W = Renderer.W, H = Renderer.H;
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        Raylib.InitWindow(W * 3, H * 3, "Hexen Sharp");
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.SetTargetFPS(120);
        Raylib.InitAudioDevice();

        var game = new Game
        {
            ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HexenSharp", "settings.cfg"),
        };
        game.MapsDir = Path.Combine(Path.GetDirectoryName(game.ConfigPath)!, "maps");
        game.ProfilePath = Path.Combine(Path.GetDirectoryName(game.ConfigPath)!, "profile.json");
        game.LoadSettings();
        game.LoadProfile();
        var keys = new RaylibKeys();
        var renderer = new Renderer();
        Audio audio = null;
        if (Raylib.IsAudioDeviceReady())
        {
            audio = new Audio();
            game.PlaySound = audio.Play;
        }

        var img = Raylib.GenImageColor(W, H, Color.Black);
        var tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.SetTextureFilter(tex, TextureFilter.Point);

        MapWatcher watcher = null;
        if (play != null)
        {
            var doc = MapFiles.TryLoad(play, out var error);
            if (doc == null) Console.WriteLine($"can't play {play}: {error}");
            else
            {
                game.Style = relaxed ? GameStyle.Relaxed : GameStyle.Classic;
                game.StartTest(doc.ToDef(), cls);
                watcher = new MapWatcher(play);
            }
        }

        bool captured = false;
        int skipMouse = 0, shotIndex = 0;

        while (!Raylib.WindowShouldClose() && !game.QuitRequested)
        {
            var inp = ReadInput(game, keys);

            bool wantCapture = game.Mode is GameMode.Playing or GameMode.Dead && !game.Menu.Open && !game.Con.Open;
            if (wantCapture != captured)
            {
                if (wantCapture) Raylib.DisableCursor(); else Raylib.EnableCursor();
                captured = wantCapture;
                skipMouse = 2; // the first delta after capturing is a jump; ignore it
            }
            if (captured && skipMouse-- <= 0)
            {
                Vector2 md = Raylib.GetMouseDelta();
                inp.LookX = md.X;
                inp.LookY = md.Y;
            }

            game.Update(inp, Raylib.GetFrameTime());
            watcher?.Poll(game, Raylib.GetFrameTime());
            renderer.Render(game);
            Raylib.UpdateTexture(tex, renderer.Fb);

            if (inp.Screenshot)
            {
                string path = $"hexen_shot_{shotIndex++:000}.png";
                Png.Save(path, renderer.Fb, W, H);
                game.Say("Saved " + path);
            }

            // letterbox to the framebuffer's aspect ratio
            int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
            float scale = MathF.Min(sw / (float)W, sh / (float)H);
            float dw = W * scale, dh = H * scale;
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Raylib.DrawTexturePro(tex, new Rectangle(0, 0, W, H), new Rectangle((sw - dw) / 2, (sh - dh) / 2, dw, dh), Vector2.Zero, 0, Color.White);
            Raylib.EndDrawing();
        }

        game.SaveSettings();
        game.SaveProfile();
        Raylib.UnloadTexture(tex);
        audio?.Dispose();
        Raylib.CloseAudioDevice();
        Raylib.CloseWindow();
    }

    /// <summary>Raylib key and mouse state, exposed through the game's key codes.</summary>
    sealed class RaylibKeys : IKeySource
    {
        float _wheel;
        public int AnyPressed;

        /// <summary>Call once per frame before reading.</summary>
        public void Poll()
        {
            _wheel = Raylib.GetMouseWheelMove();
            AnyPressed = Keys.None;
            for (int k = Raylib.GetKeyPressed(); k != 0; k = Raylib.GetKeyPressed())
                if (AnyPressed == Keys.None) AnyPressed = k;
            for (int b = 0; b < 5 && AnyPressed == Keys.None; b++)
                if (Raylib.IsMouseButtonPressed((MouseButton)b)) AnyPressed = Keys.Mouse1 + b;
            if (AnyPressed == Keys.None && _wheel != 0) AnyPressed = _wheel > 0 ? Keys.WheelUp : Keys.WheelDown;
        }

        public bool Down(int code) => code switch
        {
            Keys.None => false,
            >= Keys.Mouse1 and <= Keys.Mouse5 => Raylib.IsMouseButtonDown((MouseButton)(code - Keys.Mouse1)),
            >= 1000 => false,
            _ => Raylib.IsKeyDown((KeyboardKey)code),
        };

        public bool Pressed(int code) => code switch
        {
            Keys.None => false,
            >= Keys.Mouse1 and <= Keys.Mouse5 => Raylib.IsMouseButtonPressed((MouseButton)(code - Keys.Mouse1)),
            Keys.WheelUp => _wheel > 0,
            Keys.WheelDown => _wheel < 0,
            >= 1000 => false,
            _ => Raylib.IsKeyPressed((KeyboardKey)code) || (code == Keys.Backspace && Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)),
        };
    }

    static Input ReadInput(Game game, RaylibKeys keys)
    {
        keys.Poll();
        var i = game.Binds.Read(keys, game.Con.Open);
        i.KeyPressed = keys.AnyPressed;
        var typed = new System.Text.StringBuilder();
        for (int c = Raylib.GetCharPressed(); c != 0; c = Raylib.GetCharPressed()) typed.Append((char)c);
        i.Typed = typed.ToString();
        return i;
    }
}
