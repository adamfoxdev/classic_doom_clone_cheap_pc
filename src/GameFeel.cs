namespace HexenSharp;

/// <summary>A boss's name card: shown for a few seconds when it first wakes, with a line about it.</summary>
public sealed class BossIntro
{
    public const float Length = 2.8f;
    public Monster Boss;
    public string Name, Title;
    /// <summary>Seconds since it started (0 to Length).</summary>
    public float Time;
    /// <summary>How far in the card and the letterbox are, 0 to 1: in over a third of a second, out over the last half.</summary>
    public float Amount => MathF.Min(1, MathF.Min(Time / 0.35f, (Length - Time) / 0.5f));
}

/// <summary>
/// Game feel (Options > Effects): a hit-stop on heavy blows, screen shake, the boss intro cards and how much of the
/// music's tension layer to play.
/// </summary>
public sealed partial class Game
{
    /// <summary>Real seconds of hit-stop left: the world all but stops while it lasts.</summary>
    public float HitStop;
    /// <summary>Screen shake "trauma", 0 to 1: raised by hits, it decays over about a second; the shake goes as its square.</summary>
    public float Shake;
    /// <summary>The boss intro showing (null when none).</summary>
    public BossIntro Intro;

    public const float HitStopHeavy = 0.045f, HitStopKill = 0.07f, HitStopBoss = 0.14f;
    /// <summary>A blow this big (after damage multipliers) is heavy enough for a hit-stop.</summary>
    public const int HeavyHit = 40;
    /// <summary>Killing something with this much health (a Centaur or bigger) is a big kill.</summary>
    public const int BigKill = 100;

    /// <summary>The line under each boss's name on its intro card: (fantasy, sci-fi), by its MiniBoss id ("" for the Heresiarch).</summary>
    static readonly Dictionary<string, (string fantasy, string scifi)> BossTitles = new()
    {
        [""] = ("MASTER OF THE HUB", "THE MIND BEHIND THE STATION"),
        ["warden"] = ("GUARDIAN OF THE DEEP ROCK", "RUNAWAY EXCAVATOR"),
        ["stalker"] = ("HUNTER OF THE BARREN PLAIN", "HARVESTER GONE FERAL"),
        ["thornmother"] = ("MOTHER OF THE BROOD", "SHE CALLS HER SWARM"),
        ["keeper"] = ("WARDEN OF THE DROWNED HALLS", "GHOST IN THE COOLANT LOOP"),
        ["wyrm"] = ("IT SWIMS THROUGH STONE", "IT BORES THROUGH SOLID ROCK"),
        ["dreadnought"] = ("LORD OF THE VOID", "FLAGSHIP OF THE BELT"),
    };

    public static string BossTitle(MonsterDef d)
    {
        var t = BossTitles.TryGetValue(d.MiniBoss ?? "", out var v) ? v : BossTitles[""];
        return Art.Style == ArtStyle.SciFi ? t.scifi : t.fantasy;
    }

    /// <summary>
    /// The world's time step this frame: a crawl during a hit-stop. Also runs down the shake and the intro card, on
    /// real time. (The intro doesn't slow the game: its camera beat is the zoom and the letterbox.)
    /// </summary>
    float FeelStep(float dt)
    {
        Shake = MathF.Max(0, Shake - dt * 1.4f);
        if (Intro != null && (Intro.Time += dt) >= BossIntro.Length) Intro = null;
        if (HitStop <= 0) return dt;
        HitStop -= dt;
        return dt * 0.05f;
    }

    /// <summary>You hit a monster for `dealt`: heavy blows and kills get a hit-stop and a little shake.</summary>
    void FeelHit(Monster m, int dealt, bool kill)
    {
        bool boss = m.Def.Boss || m.Def.MiniBoss != null;
        float stop = kill && boss ? HitStopBoss : kill && m.MaxHealth >= BigKill ? HitStopKill : dealt >= HeavyHit ? HitStopHeavy : 0;
        if (stop > 0 && Vars.HitStop && !Demo) HitStop = MathF.Max(HitStop, stop);
        AddShake(kill && boss ? 0.6f : kill && m.MaxHealth >= BigKill ? 0.25f : dealt >= HeavyHit ? 0.12f : 0);
    }

    /// <summary>You took `dmg`: the screen shakes in proportion.</summary>
    void FeelHurt(int dmg) => AddShake(0.15f + dmg / 45f);

    public void AddShake(float amount) { if (amount > 0) Shake = MathF.Min(1, Shake + amount); }

    /// <summary>
    /// How far the view is thrown this frame: (yaw in radians, pitch in pixels). None with shake off; twice as much
    /// on strong. Smooth noise from a few sines, so it rattles rather than flickers.
    /// </summary>
    public (float yaw, float pitch) ShakeOffset()
    {
        if (Vars.Shake <= 0 || Shake <= 0) return (0, 0);
        float k = Shake * Shake * (Vars.Shake == 2 ? 2f : 1f), t = Time;
        float nx = MathF.Sin(t * 37f) * 0.6f + MathF.Sin(t * 61f + 1.3f) * 0.4f;
        float ny = MathF.Sin(t * 43f + 2.1f) * 0.6f + MathF.Sin(t * 71f + 0.4f) * 0.4f;
        return (nx * 0.03f * k, ny * 7f * k);
    }

    /// <summary>The first time a boss wakes near you, its intro card (once per monster; never in relaxed style).</summary>
    void CheckBossIntros()
    {
        if (Mode != GameMode.Playing || Level == null || Relaxed || Intro != null) return;
        foreach (var t in Level.Things)
        {
            if (t is not Monster m || m.Introduced || !(m.Def.Boss || m.Def.MiniBoss != null) || !m.Alive || m.State == AiState.Idle) continue;
            if (Dist(m.X, m.Y, P.X, P.Y) > 24f) continue;
            m.Introduced = true;
            if (!Vars.BossIntros) continue;
            Intro = new BossIntro { Boss = m, Name = m.Def.Name.ToUpperInvariant(), Title = BossTitle(m.Def) };
            PlaySound(Sfx.BossSight, 1);
            return;
        }
    }

    /// <summary>
    /// How much of the music's tension layer to play: all of it while a monster near you is onto you (chasing,
    /// attacking or hurt) or a boss is in the fight, none otherwise. The mixer eases between the two. Off with dynamic
    /// music off, on the menus, in relaxed style and on the practice courses.
    /// </summary>
    public float MusicIntensity
    {
        get
        {
            if (!Vars.DynamicMusic || Mode != GameMode.Playing || Level == null || P == null || Relaxed || Practicing) return 0;
            foreach (var t in Level.Things)
                if (t is Monster m && m.Alive && m.State is AiState.Chase or AiState.Attack or AiState.Pain && Dist(m.X, m.Y, P.X, P.Y) < 18f)
                    return 1;
            return BossInFight() != null ? 1 : 0;
        }
    }
}
