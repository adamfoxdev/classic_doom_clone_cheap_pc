namespace HexenSharp;

/// <summary>
/// Movement tricks: wall slides (a blast's push that meets a wall carries on along it instead of dying there) and
/// named callouts for the combo jumps: a wall kick (a rocket into a wall beside you in mid-air), a combo jump (a grenade
/// and a rocket going off under you together, or two rockets), and a chain jump (a blast that catches you already
/// flying from the last). Each shows its name across the view and counts toward the game's tricks.
/// </summary>
public static class Tricks
{
    /// <summary>Of your speed into a wall, the share that carries on along it while a blast's push has you flying.</summary>
    public const float SlideKeep = 0.75f;
    /// <summary>Two blasts this close together are one combo jump.</summary>
    public const float ComboWindow = 0.3f;
    /// <summary>How long a callout stays up.</summary>
    public const float CalloutTime = 1.6f;

    public static string Name(Trick t) => t switch
    {
        Trick.WallKick => "WALL KICK",
        Trick.ComboJump => "COMBO JUMP",
        Trick.DoubleRocket => "DOUBLE ROCKET",
        Trick.ChainJump => "CHAIN JUMP",
        _ => "",
    };
}

public enum Trick { None, WallKick, ComboJump, DoubleRocket, ChainJump }

public sealed partial class Game
{
    /// <summary>The last trick, how long ago (for its callout), and how many of each this game.</summary>
    public Trick LastTrick;
    public float TrickAge = 99f;
    public readonly int[] TrickCounts = new int[5];
    float _lastBlastAt = -99f;
    ProjKind _lastBlastKind;

    /// <summary>
    /// Blocked across one axis (`into` the wall): stop that way, and while a blast has you flying (Boost), turn most of
    /// that speed along the wall, the way you were already sliding.
    /// </summary>
    static void WallSlide(Player p, ref float into, ref float along)
    {
        if (p.Boost > 0 && MathF.Abs(along) > 0.05f) along += MathF.Sign(along) * MathF.Abs(into) * Tricks.SlideKeep;
        into = 0;
    }

    /// <summary>A blast of yours just pushed you: which trick, if any, that makes.</summary>
    void BlastTrick(Projectile pr, bool airborne, bool wall)
    {
        float since = Time - _lastBlastAt;
        var trick = Trick.None;
        if (since < Tricks.ComboWindow)
            trick = (_lastBlastKind, pr.Kind) is (ProjKind.Rocket, ProjKind.Rocket) ? Trick.DoubleRocket : Trick.ComboJump;
        else if (airborne && wall) trick = Trick.WallKick;
        else if (airborne && P.Boost > 0 && since < 2f) trick = Trick.ChainJump;
        _lastBlastAt = Time; _lastBlastKind = pr.Kind;
        if (trick == Trick.None) return;
        LastTrick = trick; TrickAge = 0;
        TrickCounts[(int)trick]++;
        PlaySound(Sfx.Secret, 0.5f);
    }

    void TrickTick(float dt) => TrickAge += dt;
}
