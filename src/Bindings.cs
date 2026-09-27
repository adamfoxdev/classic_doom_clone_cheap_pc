namespace HexenSharp;

/// <summary>Rebindable player actions.</summary>
public enum Act
{
    Forward, Back, StrafeLeft, StrafeRight, TurnLeft, TurnRight, Attack, Use, Jump, Slide, Walk, UseItem,
    Weapon1, Weapon2, Weapon3, NextWeapon, PrevWeapon, Automap, Console, Screenshot,
}

/// <summary>Where key state comes from (Raylib in the real game, a fake in tests).</summary>
public interface IKeySource
{
    bool Down(int code);
    bool Pressed(int code);
}

/// <summary>
/// Key codes and their names. Keyboard codes are GLFW's (which Raylib uses); mouse buttons and the wheel get
/// codes of their own above 1000 so every action can be bound to either.
/// </summary>
public static class Keys
{
    public const int None = 0;
    public const int Space = 32, Apostrophe = 39, Comma = 44, Minus = 45, Period = 46, Slash = 47;
    public const int Semicolon = 59, Equal = 61, LeftBracket = 91, Backslash = 92, RightBracket = 93, Grave = 96;
    public const int Escape = 256, Enter = 257, Tab = 258, Backspace = 259, Insert = 260, Delete = 261;
    public const int Right = 262, Left = 263, Down = 264, Up = 265, PageUp = 266, PageDown = 267, Home = 268, End = 269;
    public const int CapsLock = 280, F1 = 290, KeypadEnter = 335;
    public const int LeftShift = 340, LeftControl = 341, LeftAlt = 342, RightShift = 344, RightControl = 345, RightAlt = 346;
    public const int Mouse1 = 1001, Mouse2 = 1002, Mouse3 = 1003, Mouse4 = 1004, Mouse5 = 1005;
    public const int WheelUp = 1010, WheelDown = 1011;
    public static int Letter(char c) => char.ToUpperInvariant(c);
    public static int Digit(int d) => '0' + d;

    static readonly Dictionary<int, string> Names = BuildNames();
    static readonly Dictionary<string, int> Codes = Names.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    static Dictionary<int, string> BuildNames()
    {
        var n = new Dictionary<int, string>();
        for (char c = 'A'; c <= 'Z'; c++) n[c] = c.ToString();
        for (int d = 0; d <= 9; d++) n['0' + d] = d.ToString();
        for (int f = 0; f < 12; f++) n[F1 + f] = "F" + (f + 1);
        for (int k = 0; k <= 9; k++) n[320 + k] = "KP" + k;
        n[Space] = "SPACE"; n[Apostrophe] = "'"; n[Comma] = ","; n[Minus] = "-"; n[Period] = "."; n[Slash] = "/";
        n[Semicolon] = ";"; n[Equal] = "="; n[LeftBracket] = "["; n[Backslash] = "BACKSLASH"; n[RightBracket] = "]"; n[Grave] = "GRAVE";
        n[Escape] = "ESCAPE"; n[Enter] = "ENTER"; n[Tab] = "TAB"; n[Backspace] = "BACKSPACE"; n[Insert] = "INSERT"; n[Delete] = "DELETE";
        n[Right] = "RIGHT"; n[Left] = "LEFT"; n[Down] = "DOWN"; n[Up] = "UP"; n[PageUp] = "PGUP"; n[PageDown] = "PGDN";
        n[Home] = "HOME"; n[End] = "END"; n[CapsLock] = "CAPSLOCK"; n[KeypadEnter] = "KPENTER";
        n[330] = "KP."; n[331] = "KP/"; n[332] = "KP*"; n[333] = "KP-"; n[334] = "KP+";
        n[LeftShift] = "LSHIFT"; n[LeftControl] = "LCTRL"; n[LeftAlt] = "LALT";
        n[RightShift] = "RSHIFT"; n[RightControl] = "RCTRL"; n[RightAlt] = "RALT";
        n[Mouse1] = "MOUSE1"; n[Mouse2] = "MOUSE2"; n[Mouse3] = "MOUSE3"; n[Mouse4] = "MOUSE4"; n[Mouse5] = "MOUSE5";
        n[WheelUp] = "WHEELUP"; n[WheelDown] = "WHEELDOWN";
        return n;
    }

    public static string Name(int code) => code == None ? "---" : Names.TryGetValue(code, out var s) ? s : "KEY" + code;
    public static int Parse(string s) =>
        Codes.TryGetValue(s, out int c) ? c : s.StartsWith("KEY", StringComparison.OrdinalIgnoreCase) && int.TryParse(s[3..], out c) ? c : None;
    public static bool Known(int code) => Names.ContainsKey(code);

    /// <summary>Keys that always drive menus, so a bad binding can never lock you out.</summary>
    public static bool Reserved(int code) => code == Escape;
}

/// <summary>Two key slots per action, with defaults, conflict handling and translation to game input.</summary>
public sealed class Bindings
{
    public const int Slots = 2;
    public static readonly int Count = Enum.GetValues<Act>().Length;

    public sealed record Info(Act Act, string Id, string Label, int Key1, int Key2);

    public static readonly Info[] All =
    {
        new(Act.Forward, "forward", "Move forward", Keys.Letter('W'), Keys.Up),
        new(Act.Back, "back", "Move back", Keys.Letter('S'), Keys.Down),
        new(Act.StrafeLeft, "strafeleft", "Strafe left", Keys.Letter('A'), Keys.None),
        new(Act.StrafeRight, "straferight", "Strafe right", Keys.Letter('D'), Keys.None),
        new(Act.TurnLeft, "turnleft", "Turn left", Keys.Left, Keys.None),
        new(Act.TurnRight, "turnright", "Turn right", Keys.Right, Keys.None),
        new(Act.Attack, "attack", "Attack", Keys.Mouse1, Keys.LeftControl),
        new(Act.Use, "use", "Use / push", Keys.Letter('E'), Keys.None),
        new(Act.Jump, "jump", "Jump", Keys.Space, Keys.None),
        new(Act.Slide, "slide", "Slide", Keys.Letter('C'), Keys.None),
        new(Act.Walk, "walk", "Walk / pull", Keys.LeftShift, Keys.RightShift),
        new(Act.UseItem, "useitem", "Use item", Keys.Letter('F'), Keys.None),
        new(Act.Weapon1, "weapon1", "Weapon 1", Keys.Digit(1), Keys.None),
        new(Act.Weapon2, "weapon2", "Weapon 2", Keys.Digit(2), Keys.None),
        new(Act.Weapon3, "weapon3", "Weapon 3", Keys.Digit(3), Keys.None),
        new(Act.NextWeapon, "nextweapon", "Next weapon", Keys.WheelDown, Keys.None),
        new(Act.PrevWeapon, "prevweapon", "Previous weapon", Keys.WheelUp, Keys.None),
        new(Act.Automap, "automap", "Automap", Keys.Tab, Keys.Letter('M')),
        new(Act.Console, "console", "Console", Keys.Grave, Keys.None),
        new(Act.Screenshot, "screenshot", "Screenshot", Keys.F1 + 11, Keys.None),
    };

    readonly int[,] _keys = new int[Count, Slots];

    public Bindings() => Reset();

    public void Reset()
    {
        foreach (var b in All) { _keys[(int)b.Act, 0] = b.Key1; _keys[(int)b.Act, 1] = b.Key2; }
    }

    public int Get(Act a, int slot) => _keys[(int)a, slot];

    public static Info Find(string id) =>
        All.FirstOrDefault(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Binds a key to an action slot. The key is removed from wherever else it was bound; the displaced
    /// action is returned so the menu can say so.
    /// </summary>
    public Act? Set(Act a, int slot, int code)
    {
        Act? displaced = null;
        if (code != Keys.None)
            for (int i = 0; i < Count; i++)
                for (int s = 0; s < Slots; s++)
                    if (_keys[i, s] == code && !(i == (int)a && s == slot))
                    {
                        _keys[i, s] = Keys.None;
                        if (i != (int)a) displaced = (Act)i;
                    }
        _keys[(int)a, slot] = code;
        return displaced;
    }

    bool Down(IKeySource k, Act a) => k.Down(_keys[(int)a, 0]) || k.Down(_keys[(int)a, 1]);
    bool Pressed(IKeySource k, Act a) => k.Pressed(_keys[(int)a, 0]) || k.Pressed(_keys[(int)a, 1]);

    /// <summary>Turns the current key state into a frame of game input (mouse look and typed text are added by the host).</summary>
    public Input Read(IKeySource k, bool consoleOpen)
    {
        var i = new Input();
        if (Down(k, Act.Forward)) i.Move += 1;
        if (Down(k, Act.Back)) i.Move -= 1;
        if (Down(k, Act.StrafeRight)) i.Strafe += 1;
        if (Down(k, Act.StrafeLeft)) i.Strafe -= 1;
        if (Down(k, Act.TurnRight)) i.Turn += 1;
        if (Down(k, Act.TurnLeft)) i.Turn -= 1;
        i.Walk = Down(k, Act.Walk);
        i.Fire = Down(k, Act.Attack);
        i.Use = Pressed(k, Act.Use);
        i.Jump = Pressed(k, Act.Jump);
        i.Slide = Pressed(k, Act.Slide);
        i.UseItem = Pressed(k, Act.UseItem);
        i.Map = Pressed(k, Act.Automap);
        i.ConsoleToggle = Pressed(k, Act.Console);
        i.Screenshot = Pressed(k, Act.Screenshot);
        if (Pressed(k, Act.Weapon1)) i.Slot = 1;
        if (Pressed(k, Act.Weapon2)) i.Slot = 2;
        if (Pressed(k, Act.Weapon3)) i.Slot = 3;
        if (Pressed(k, Act.NextWeapon)) i.Cycle = 1;
        if (Pressed(k, Act.PrevWeapon)) i.Cycle = -1;

        // fixed menu keys (arrows, Enter, Esc) plus your movement keys, unless you're typing in the console
        i.Pause = k.Pressed(Keys.Escape);
        i.Confirm = k.Pressed(Keys.Enter) || k.Pressed(Keys.KeypadEnter);
        i.Up = k.Pressed(Keys.Up) || (!consoleOpen && Pressed(k, Act.Forward));
        i.Down = k.Pressed(Keys.Down) || (!consoleOpen && Pressed(k, Act.Back));
        i.Left = k.Pressed(Keys.Left) || (!consoleOpen && Pressed(k, Act.StrafeLeft));
        i.Right = k.Pressed(Keys.Right) || (!consoleOpen && Pressed(k, Act.StrafeRight));
        i.Backspace = k.Pressed(Keys.Backspace);
        i.Tab = k.Pressed(Keys.Tab);
        i.PageUp = k.Pressed(Keys.PageUp);
        i.PageDown = k.Pressed(Keys.PageDown);
        return i;
    }

    /// <summary>The bindings as console commands, for the settings file.</summary>
    public IEnumerable<string> ToCommands() =>
        All.Select(b => $"bind {b.Id} {Keys.Name(Get(b.Act, 0))} {Keys.Name(Get(b.Act, 1))}".TrimEnd());
}
