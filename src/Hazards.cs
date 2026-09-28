namespace HexenSharp;

/// <summary>
/// New Game+ hazards: three of the hub's maps turn against you on a second run. The Bedrock Depths' floor crumbles
/// if you stand still on it; gusts sweep the Windspire, harder the higher you are; and the Hanging Cisterns flood
/// with coolant every so often, hurting anyone left on the floor. A first campaign has none of them.
/// </summary>
public sealed partial class Game
{
    /// <summary>Seconds standing on one spot in the Depths before the floor cracks, and before it gives way.</summary>
    public const float CrumbleWarn = 0.6f, CrumbleTime = 1.3f;
    public const int CrumbleDamage = 5;
    /// <summary>The Windspire's gusts: one every GustEvery seconds, blowing for GustLength after GustWarn of calm, at up to GustPush cells a second.</summary>
    public const float GustEvery = 9f, GustWarn = 1.5f, GustLength = 2.5f, GustPush = 1.8f;
    /// <summary>The Cisterns' floods: every FloodEvery seconds, a warning FloodWarn ahead, then FloodLength of coolant FloodDepth deep.</summary>
    public const float FloodEvery = 40f, FloodWarn = 4f, FloodLength = 8f, FloodDepth = 0.35f, CoolantTick = 0.5f;
    public const int CoolantDamage = 4;

    public enum HazardKind { None, Crumble, Wind, Flood }

    public static HazardKind HazardOf(Level lv) => lv?.Hazard is { } own ? own switch
    {
        "crumble" => HazardKind.Crumble,
        "wind" => HazardKind.Wind,
        "flood" => HazardKind.Flood,
        _ => HazardKind.None,
    } : lv?.RawName switch
    {
        "Bedrock Depths" => HazardKind.Crumble,
        "Windspire" => HazardKind.Wind,
        "Hanging Cisterns" => HazardKind.Flood,
        _ => HazardKind.None,
    };

    /// <summary>Hazards are New Game+'s, except on a custom map that sets its own (which it has every time).</summary>
    public bool HazardsOn => (NgTier > 0 || Level?.Hazard != null) && Mode == GameMode.Playing && !Relaxed && Rematch == null && Level != null && !Level.Flight;

    /// <summary>A gust is blowing (at GustAngle), the Cisterns are flooding, and you're standing in the coolant.</summary>
    public bool Gusting, Flooding, InCoolant;
    public float GustAngle;
    /// <summary>Seconds into the map's hazard cycle (from arriving).</summary>
    public float HazardClock;
    Level _hazardLevel;
    int _crumbleCell = -1;
    float _crumbleTime, _coolantCd;
    readonly HashSet<(Level, int)> _crumbled = new();

    void HazardTick(float dt)
    {
        Gusting = Flooding = InCoolant = false;
        if (!HazardsOn || HazardOf(Level) == HazardKind.None) { _hazardLevel = null; return; }
        if (_hazardLevel != Level)
        {
            // just arrived: the cycle starts over, and the first time, a word about what's different here
            _hazardLevel = Level; HazardClock = 0; _crumbleCell = -1; _crumbleTime = 0; _coolantCd = 0;
            Say(HazardOf(Level) switch
            {
                HazardKind.Crumble => "New Game+: the floor here crumbles if you stand still.",
                HazardKind.Wind => "New Game+: gusts sweep the heights. Mind the edges.",
                _ => Words.T("New Game+: the cisterns flood now and then. Get up on a ledge when they do."),
            });
        }
        HazardClock += dt;
        switch (HazardOf(Level))
        {
            case HazardKind.Crumble: Crumble(dt); break;
            case HazardKind.Wind: Wind(dt); break;
            case HazardKind.Flood: Flood(dt); break;
        }
    }

    bool CanCrumble(int x, int y) => Level.CanDig(x, y, Level.Face.Floor) && !_crumbled.Contains((Level, y * Level.W + x));

    /// <summary>The Depths: stand still and the floor under you cracks, then drops a step (once per spot, so you can always climb out).</summary>
    void Crumble(float dt)
    {
        var p = P;
        int cx = (int)MathF.Floor(p.X), cy = (int)MathF.Floor(p.Y), cell = cy * Level.W + cx;
        if (!p.OnGround || p.Flying || cell != _crumbleCell) { _crumbleCell = p.OnGround ? cell : -1; _crumbleTime = 0; return; }
        if (!CanCrumble(cx, cy)) return;
        float before = _crumbleTime;
        _crumbleTime += dt;
        float floor = Level.Floors[cell];
        if (before < CrumbleWarn && _crumbleTime >= CrumbleWarn)
        {
            for (int k = 0; k < 3; k++) Dust(cx + 0.2f + RandF() * 0.6f, cy + 0.2f + RandF() * 0.6f, floor);
            Sound(Sfx.Hit, p.X, p.Y);
            Say("The floor is cracking under you!");
        }
        if (_crumbleTime < CrumbleTime) return;
        Level.DamageBlock(cx, cy, 100000, Level.Face.Floor);
        _crumbled.Add((Level, cell));
        for (int k = 0; k < 6; k++) Dust(cx + RandF(), cy + RandF(), floor);
        Sound(Sfx.Break, p.X, p.Y);
        DamagePlayer(CrumbleDamage);
        AddShake(0.35f);
        Say("The floor gives way!");
        _crumbleTime = 0;
    }

    void Dust(float x, float y, float z) =>
        Level.Things.Add(new Puff(Art.RubbleChunk, 0.1f + RandF() * 0.08f, 0.4f + RandF() * 0.3f, 0f)
            { X = x, Y = y, Z = z + 0.05f, Level = Level, FullBright = false, VZ = 0.5f + RandF(), Gravity = 6f });

    /// <summary>The Windspire: a gust every few seconds pushes you one way, gently on the ground and hard up high.</summary>
    void Wind(float dt)
    {
        var p = P;
        float t = HazardClock % GustEvery;
        if (t < dt) GustAngle = RandF() * MathF.Tau; // a new gust, a new direction
        if (t < GustWarn || t >= GustWarn + GustLength) return;
        Gusting = true;
        float height = p.FloorZ + p.Z;
        float push = GustPush * Math.Clamp(height / 3f, 0.25f, 1f) * dt;
        float dx = MathF.Cos(GustAngle) * push, dy = MathF.Sin(GustAngle) * push;
        if (!Vars.NoClip)
        {
            if (!Blocked(p.X + dx, p.Y, p.Radius, null)) p.X += dx;
            if (!Blocked(p.X, p.Y + dy, p.Radius, null)) p.Y += dy;
        }
        if (RandF() < dt * 6)
        {
            // streaks of dust on the wind
            float a = GustAngle + MathF.PI + (RandF() - 0.5f);
            Level.Things.Add(new Puff(Art.RubbleChunk, 0.06f, 0.5f, 0f)
                { X = p.X + MathF.Cos(a) * 1.5f, Y = p.Y + MathF.Sin(a) * 1.5f, Z = p.FloorZ + p.Z + 0.3f + RandF() * 0.6f, Level = Level, FullBright = false });
        }
    }

    /// <summary>The Cisterns: now and then the coolant floods the floor for a few seconds; standing in it hurts.</summary>
    void Flood(float dt)
    {
        var p = P;
        float t = HazardClock % FloodEvery, rise = FloodEvery - FloodLength;
        if (t - dt < rise - FloodWarn && t >= rise - FloodWarn)
        {
            Say(Words.T("The water is rising! Get up on a ledge!"));
            PlaySound(Sfx.Locked, 0.8f);
        }
        if (t < rise) return;
        Flooding = true;
        float low = FloodFloor(Level);
        if (p.FloorZ + p.Z >= low + FloodDepth) { _coolantCd = 0; return; }
        InCoolant = true;
        _coolantCd -= dt;
        if (_coolantCd <= 0)
        {
            _coolantCd = CoolantTick;
            DamagePlayer(CoolantDamage);
            Level.Things.Add(new Puff(Art.Bolt[1], 0.15f, 0.5f, 0.6f) { X = p.X + RandF() - 0.5f, Y = p.Y + RandF() - 0.5f, Z = low + 0.1f, Level = Level });
        }
    }

    /// <summary>The lowest floor on a map: where a flood lies.</summary>
    static float FloodFloor(Level lv)
    {
        float low = float.MaxValue;
        for (int i = 0; i < lv.Cells.Length; i++) if (lv.Cells[i] == '\0') low = MathF.Min(low, lv.Floors[i]);
        return low == float.MaxValue ? 0 : low;
    }
}
