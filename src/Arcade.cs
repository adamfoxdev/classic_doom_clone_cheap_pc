namespace HexenSharp;

/// <summary>A number or word that pops out of a monster you hit, rises and fades.</summary>
public sealed class Floater
{
    public float X, Y, Z, Life, MaxLife;
    public string Text;
    public uint Colour;
    public int Scale = 1;
    /// <summary>A score pop (arcade mode only), not a damage number.</summary>
    public bool Score;
    /// <summary>Its colour when shown as a plain damage number, outside arcade mode.</summary>
    public uint Plain;
}

/// <summary>
/// Arcade mode (Options > Arcade mode): damage numbers pop out of what you hit, every hit scores, and a style rank from
/// D up to SSS climbs while you keep the pressure on. The rank multiplies your score; it drains if you stop fighting and
/// drops a whole grade when you get hurt. Mixing weapons and hitting from the air earn extra style.
/// </summary>
public sealed class Arcade
{
    public static readonly string[] Ranks = { "D", "C", "B", "A", "S", "SS", "SSS" };
    public static readonly string[] Titles = { "DULL", "COOL", "BRUTAL", "AWESOME", "SAVAGE!", "SUPERB!!", "STYLISH!!!" };
    public static readonly uint[] Colours =
    {
        Col.Rgb(170, 170, 180), Col.Rgb(120, 200, 255), Col.Rgb(120, 255, 140), Col.Rgb(255, 230, 90),
        Col.Rgb(255, 150, 60), Col.Rgb(255, 80, 60), Col.Rgb(255, 90, 230),
    };
    /// <summary>How long a combo survives between hits, in seconds.</summary>
    public const float ComboWindow = 2.5f;

    public long Score, Best;
    /// <summary>Style from 0 up to Ranks.Length: the whole part is the rank, the fraction is the meter towards the next.</summary>
    public float Style;
    public int Combo;
    public float ComboTime, RankFlash;
    public readonly List<Floater> Floaters = new();
    int _lastSlot = -1, _lastRank;

    public int Rank => Math.Clamp((int)Style, 0, Ranks.Length - 1);
    public float Meter => Style >= Ranks.Length ? 1f : Style - MathF.Floor(Style);
    public int Multiplier => Rank + 1;
    /// <summary>Show the rank only once you've got something going.</summary>
    public bool Active => Combo > 0 || Style > 0.05f;

    public void Reset()
    {
        Best = Math.Max(Best, Score);
        Score = 0; Style = 0; Combo = 0; ComboTime = 0; RankFlash = 0; _lastSlot = -1; _lastRank = 0;
        Floaters.Clear();
    }

    /// <summary>You hit a monster for `dmg` with weapon `slot` (at x,y,z, the top of it); `kill` if that finished it off.</summary>
    public void Hit(float x, float y, float z, int dmg, int slot, bool kill, int maxHealth, bool boss, bool airborne)
    {
        Combo++;
        ComboTime = ComboWindow;
        float gain = 0.1f + dmg / 250f;
        if (slot != _lastSlot && _lastSlot >= 0) gain += 0.2f;   // variety
        if (airborne) gain += 0.12f;
        if (kill) gain += boss ? 2f : 0.45f + maxHealth / 400f;
        _lastSlot = slot;
        Style = MathF.Min(Ranks.Length - 0.001f, Style + gain / (1 + Rank * 0.35f)); // higher ranks are harder to climb
        int points = dmg * 10 * Multiplier;
        Score += points;
        // outside arcade mode (damage numbers on their own) heavy blows show orange, the rest white
        Pop(x, y, z, dmg.ToString(), Colours[Rank], 1, 0.8f, plain: dmg >= Game.HeavyHit ? Col.Rgb(255, 170, 70) : Col.Rgb(240, 236, 225));
        if (kill)
        {
            int bonus = maxHealth * (boss ? 50 : 20) * Multiplier;
            Score += bonus;
            Pop(x, y, z + 0.3f, $"+{bonus}", Col.Rgb(255, 240, 150), 1, 1.3f, score: true);
        }
        if (Rank > _lastRank) RankFlash = 1f;
        _lastRank = Rank;
    }

    /// <summary>A bonus for something big, like clearing an arena wave.</summary>
    public int Bonus(int basePoints)
    {
        int pts = basePoints * Multiplier;
        Score += pts;
        return pts;
    }

    /// <summary>You got hurt: the combo breaks and the style rank drops a grade.</summary>
    public void Hurt()
    {
        Combo = 0;
        Style = MathF.Max(0, MathF.Floor(Style) - 1);
        _lastRank = Rank;
    }

    public void Update(float dt)
    {
        ComboTime -= dt;
        if (ComboTime <= 0) Combo = 0;
        // the meter drains faster at the higher ranks, and faster still once the combo has lapsed
        Style = MathF.Max(0, Style - dt * (0.05f + 0.05f * Rank) * (Combo > 0 ? 1f : 3f));
        _lastRank = Math.Min(_lastRank, Rank);
        RankFlash = MathF.Max(0, RankFlash - dt * 2.5f);
        foreach (var f in Floaters) { f.Life -= dt; f.Z += dt * 0.6f; }
        Floaters.RemoveAll(f => f.Life <= 0);
    }

    void Pop(float x, float y, float z, string text, uint colour, int scale, float life, bool score = false, uint plain = 0)
    {
        // spread repeated pops a little so a flurry of hits stays readable
        float j = (Floaters.Count % 5 - 2) * 0.22f;
        Floaters.Add(new Floater { X = x + j, Y = y - j, Z = z + Floaters.Count % 3 * 0.15f, Text = text, Colour = colour, Scale = scale, Life = life, MaxLife = life, Score = score, Plain = plain });
    }
}
