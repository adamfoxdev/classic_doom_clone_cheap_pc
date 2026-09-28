namespace HexenSharp;

/// <summary>Gamepad buttons, named for an Xbox-style pad (A at the bottom of the face buttons).</summary>
[Flags]
public enum PadButton
{
    None = 0, A = 1, B = 2, X = 4, Y = 8, LB = 16, RB = 32, Back = 64, Start = 128,
    Up = 256, Down = 512, Left = 1024, Right = 2048, L3 = 4096, R3 = 8192,
}

/// <summary>One frame of a gamepad: sticks and triggers (-1..1 and 0..1) and the buttons held.</summary>
public struct PadState
{
    public bool Connected;
    public float LX, LY, RX, RY, LT, RT;
    public PadButton Held;
}

/// <summary>
/// Turns a gamepad into game input, on top of the keyboard and mouse (both work at once). The layout:
/// left stick moves, right stick looks, RT attacks, LT flies the jetpack, A jumps (and confirms in menus), B slides
/// (and backs out of menus), X uses, Y uses an item, LB/RB change weapon, the d-pad drives menus and the perk choice,
/// Start pauses, Back opens the automap, L3 the character screen and R3 places a block.
/// </summary>
public sealed class Gamepad
{
    public const float Deadzone = 0.2f;
    /// <summary>Right stick at full tilt: turning in radians a second, and looking up or down in degrees a second.</summary>
    public const float TurnRate = 3.2f, PitchRate = 150f;
    PadState _prev;

    /// <summary>A stick axis with the dead zone taken out, rescaled so it still reaches 1.</summary>
    public static float Stick(float v)
    {
        float a = MathF.Abs(v);
        return a < Deadzone ? 0 : MathF.Sign(v) * MathF.Min(1, (a - Deadzone) / (1 - Deadzone));
    }

    /// <summary>
    /// Adds the pad to a frame of input. `inMenu` is true on the menus and the class screen, where the stick and
    /// d-pad step through items and B goes back. `lookSpeed` scales the right stick (the `padlook` setting).
    /// </summary>
    public void Apply(ref Input i, PadState s, float dt, bool inMenu, float lookSpeed = 1f)
    {
        if (!s.Connected) { _prev = default; return; }
        var prev = _prev;
        _prev = s;
        bool Held(PadButton b) => (s.Held & b) != 0;
        bool Pressed(PadButton b) => Held(b) && (prev.Held & b) == 0;
        // a stick pushed past halfway counts as a press, once, for menus
        bool Flick(float now, float before, int dir) => now * dir > 0.6f && before * dir <= 0.6f;

        float lx = Stick(s.LX), ly = Stick(s.LY), rx = Stick(s.RX), ry = Stick(s.RY);
        i.Move = Math.Clamp(i.Move - ly, -1f, 1f);
        i.Strafe = Math.Clamp(i.Strafe + lx, -1f, 1f);
        // the game turns the view by LookX * 0.0025 radians and LookY * 0.35 degrees (times the mouse sensitivity);
        // squaring the stick gives fine aim near the centre
        i.LookX += rx * MathF.Abs(rx) * TurnRate * lookSpeed * dt / 0.0025f;
        i.LookY += ry * MathF.Abs(ry) * PitchRate * lookSpeed * dt / 0.35f;

        i.Fire |= s.RT > 0.5f;
        i.JetHeld |= s.LT > 0.5f;
        i.JumpHeld |= Held(PadButton.A);
        i.Jump |= Pressed(PadButton.A);
        i.Confirm |= Pressed(PadButton.A);
        i.SlideHeld |= Held(PadButton.B);
        i.Slide |= Pressed(PadButton.B);
        i.Use |= Pressed(PadButton.X);
        i.UseItem |= Pressed(PadButton.Y);
        if (Pressed(PadButton.RB)) i.Cycle = 1;
        if (Pressed(PadButton.LB)) i.Cycle = -1;
        i.Pause |= Pressed(PadButton.Start) || (inMenu && Pressed(PadButton.B));
        i.Map |= Pressed(PadButton.Back);
        i.Character |= Pressed(PadButton.L3);
        i.Place |= Pressed(PadButton.R3);

        i.Up |= Pressed(PadButton.Up) || (inMenu && Flick(-s.LY, -prev.LY, 1));
        i.Down |= Pressed(PadButton.Down) || (inMenu && Flick(-s.LY, -prev.LY, -1));
        i.Left |= Pressed(PadButton.Left) || (inMenu && Flick(s.LX, prev.LX, -1));
        i.Right |= Pressed(PadButton.Right) || (inMenu && Flick(s.LX, prev.LX, 1));
    }
}
