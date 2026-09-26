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
        RunWindow();
        return 0;
    }

    static void RunWindow()
    {
        const int W = Renderer.W, H = Renderer.H;
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        Raylib.InitWindow(W * 3, H * 3, "Hexen Sharp");
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.SetTargetFPS(120);
        Raylib.InitAudioDevice();

        var game = new Game();
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

        bool captured = false;
        int skipMouse = 0, shotIndex = 0;

        while (!Raylib.WindowShouldClose() && !game.QuitRequested)
        {
            var inp = ReadInput(game.Con.Open);

            bool wantCapture = game.Mode is GameMode.Playing or GameMode.Dead && !game.Paused && !game.Con.Open;
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

        Raylib.UnloadTexture(tex);
        audio?.Dispose();
        Raylib.CloseAudioDevice();
        Raylib.CloseWindow();
    }

    static bool Down(KeyboardKey k) => Raylib.IsKeyDown(k);
    static bool Pressed(KeyboardKey k) => Raylib.IsKeyPressed(k);

    static Input ReadInput(bool consoleOpen)
    {
        var i = new Input();
        if (Down(KeyboardKey.W) || Down(KeyboardKey.Up)) i.Move += 1;
        if (Down(KeyboardKey.S) || Down(KeyboardKey.Down)) i.Move -= 1;
        if (Down(KeyboardKey.D)) i.Strafe += 1;
        if (Down(KeyboardKey.A)) i.Strafe -= 1;
        if (Down(KeyboardKey.Right)) i.Turn += 1;
        if (Down(KeyboardKey.Left)) i.Turn -= 1;
        i.Walk = Down(KeyboardKey.LeftShift) || Down(KeyboardKey.RightShift);
        i.Fire = Raylib.IsMouseButtonDown(MouseButton.Left) || Down(KeyboardKey.LeftControl) || Down(KeyboardKey.RightControl);
        i.Use = Pressed(KeyboardKey.E);
        i.Jump = Pressed(KeyboardKey.Space);
        i.Slide = Pressed(KeyboardKey.C);
        i.ConsoleToggle = Pressed(KeyboardKey.Grave);
        i.Backspace = Pressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace);
        i.Tab = Pressed(KeyboardKey.Tab);
        i.PageUp = Pressed(KeyboardKey.PageUp);
        i.PageDown = Pressed(KeyboardKey.PageDown);
        var typed = new System.Text.StringBuilder();
        for (int c = Raylib.GetCharPressed(); c != 0; c = Raylib.GetCharPressed()) typed.Append((char)c);
        i.Typed = typed.ToString();
        i.UseItem = Pressed(KeyboardKey.F);
        i.Map = Pressed(KeyboardKey.Tab) || Pressed(KeyboardKey.M);
        i.Pause = Pressed(KeyboardKey.Escape);
        i.Confirm = Pressed(KeyboardKey.Enter) || Pressed(KeyboardKey.KpEnter);
        i.Up = Pressed(KeyboardKey.Up) || (!consoleOpen && Pressed(KeyboardKey.W));
        i.Down = Pressed(KeyboardKey.Down) || (!consoleOpen && Pressed(KeyboardKey.S));
        i.Quit = Pressed(KeyboardKey.Q);
        i.Screenshot = Pressed(KeyboardKey.F12);
        if (Pressed(KeyboardKey.One)) i.Slot = 1;
        if (Pressed(KeyboardKey.Two)) i.Slot = 2;
        if (Pressed(KeyboardKey.Three)) i.Slot = 3;
        float wheel = Raylib.GetMouseWheelMove();
        if (wheel > 0) i.Cycle = -1; else if (wheel < 0) i.Cycle = 1;
        return i;
    }
}
