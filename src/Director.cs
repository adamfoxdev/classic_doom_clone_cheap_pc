namespace HexenSharp;

/// <summary>
/// The New Game+ director: on a second run the hub doesn't stay empty behind you. Walk back through somewhere you
/// were a while ago, with nothing hunting you, and now and then a small ambush of that map's own monsters closes in
/// from out of sight. It waits between ambushes, keeps each map to a few, and never springs one during a boss fight.
/// </summary>
public sealed partial class Game
{
    /// <summary>How long ago you must have been somewhere for coming back to count as backtracking, in seconds.</summary>
    public const float BacktrackAfter = 45f;
    /// <summary>How often the director looks, and the chance each look springs an ambush when it can.</summary>
    public const float DirectorEvery = 2f, DirectorChance = 0.5f;
    /// <summary>Where an ambush appears: this far from you, in cells.</summary>
    public const float AmbushNear = 5f, AmbushFar = 9f;

    /// <summary>Seconds after one ambush before the next can come, at a tier (60 at tier 1, less higher up, 30 at least).</summary>
    public static float AmbushCooldown(int tier) => MathF.Max(30f, 68f - 8f * tier);
    /// <summary>How many monsters in an ambush, and how many ambushes a map gets, at a tier.</summary>
    public static int AmbushSize(int tier) => Math.Min(4, 1 + tier);
    public static int AmbushesPerMap(int tier) => 2 + tier;

    /// <summary>How long after stepping back somewhere old an ambush can still spring.</summary>
    public const float BacktrackWindow = 10f;

    /// <summary>When you last stood on each cell of each map (PlayTime; 0 for never).</summary>
    readonly Dictionary<Level, float[]> _visited = new();
    readonly Dictionary<Level, int> _ambushes = new();
    /// <summary>The monsters each map started with, the director's choice of who to send.</summary>
    readonly Dictionary<Level, MonsterDef[]> _natives = new();
    float _directorClock, _ambushCd, _backtrackUntil;
    Random _directorRng = new(3);
    /// <summary>The last ambush's monsters (for the checks).</summary>
    public List<Monster> LastAmbush = new();

    /// <summary>A new campaign: forget where you've been, and note who lives where.</summary>
    void ResetDirector()
    {
        _visited.Clear(); _ambushes.Clear(); _natives.Clear();
        _directorClock = 0; _ambushCd = AmbushCooldown(NgTier); _backtrackUntil = 0;
        _directorRng = new Random(RunSeed + 29);
        LastAmbush = new();
        if (Hub == null) return;
        foreach (var lv in Hub)
            _natives[lv] = lv.Things.OfType<Monster>().Where(m => !m.Def.Boss && m.Def.MiniBoss == null && m.Def.Special == Special.None)
                .Select(m => m.Def).Distinct().ToArray();
    }

    bool DirectorOn => NgTier > 0 && Mode == GameMode.Playing && !Relaxed && Rematch == null && !TestingMap && Level != null && !Level.Flight;

    void DirectorTick(float dt)
    {
        if (!DirectorOn) return;
        var p = P;
        if (!_visited.TryGetValue(Level, out var seen)) _visited[Level] = seen = new float[Level.W * Level.H];
        int cx = (int)MathF.Floor(p.X), cy = (int)MathF.Floor(p.Y);
        if (!Level.InBounds(cx, cy)) return;
        int cell = cy * Level.W + cx;
        // stepping back onto somewhere you last stood a good while ago is backtracking: for a few seconds, an ambush can come
        float lastHere = seen[cell];
        if (lastHere > 0 && PlayTime - lastHere > BacktrackAfter) _backtrackUntil = PlayTime + BacktrackWindow;
        seen[cell] = MathF.Max(PlayTime, 0.001f);
        _ambushCd -= dt;
        if ((_directorClock += dt) < DirectorEvery) return;
        _directorClock = 0;
        // backtracking, with nothing already after you, and this map not yet ambushed to its limit
        if (_ambushCd > 0 || PlayTime > _backtrackUntil) return;
        if (_ambushes.GetValueOrDefault(Level) >= AmbushesPerMap(NgTier) || BossInFight() != null || Intro != null) return;
        foreach (var t in Level.Things)
            if (t is Monster m && m.Alive && m.State != AiState.Idle && Dist(m.X, m.Y, p.X, p.Y) < 14f) return;
        if (_directorRng.NextDouble() >= DirectorChance) return;
        Ambush();
    }

    /// <summary>Springs an ambush: a few of this map's monsters, toughened for the tier, closing in from nearby spots out of sight.</summary>
    public bool Ambush()
    {
        var p = P;
        var kinds = _natives.GetValueOrDefault(Level);
        if (kinds == null || kinds.Length == 0) kinds = new[] { Monster.Ettin, Monster.Afrit };
        // candidate spots: open floor at about your height, 5 to 9 cells off, out of sight if there are enough
        var spots = new List<(float x, float y, bool hidden)>();
        int r = (int)AmbushFar + 1;
        for (int y = (int)p.Y - r; y <= (int)p.Y + r; y++)
            for (int x = (int)p.X - r; x <= (int)p.X + r; x++)
            {
                if (!Level.InBounds(x, y) || Level.Blocks(x, y)) continue;
                float sx = x + 0.5f, sy = y + 0.5f, d = Dist(sx, sy, p.X, p.Y);
                if (d < AmbushNear || d > AmbushFar || MathF.Abs(Level.FloorAt(sx, sy) - p.FloorZ) > 1f) continue;
                if (Level.BlocksCircle(sx, sy, 0.4f) || char.IsDigit(Level.Marks[y * Level.W + x])) continue;
                if (Level.Things.Any(t => t.Solid && !t.Removed && Dist(t.X, t.Y, sx, sy) < 0.9f)) continue;
                spots.Add((sx, sy, !Level.Sight(p.X, p.Y, sx, sy)));
            }
        if (spots.Count == 0) return false;
        var hidden = spots.Where(s => s.hidden).ToList();
        var pool = hidden.Count >= AmbushSize(NgTier) ? hidden : spots;
        int n = Math.Min(AmbushSize(NgTier), pool.Count);
        LastAmbush = new();
        for (int k = 0; k < n; k++)
        {
            int i = _directorRng.Next(pool.Count);
            var (sx, sy, _) = pool[i];
            pool.RemoveAt(i);
            var def = kinds[_directorRng.Next(kinds.Length)];
            var am = SpawnMonster(def, sx, sy, NgPlus.Health(NgTier), NgPlus.Damage(NgTier), NgPlus.Speed(NgTier));
            if (_eliteRng.NextDouble() < Elites.Chance(NgTier)) MakeElite(am, Elites.All[_eliteRng.Next(Elites.All.Length)]);
            LastAmbush.Add(am);
        }
        _ambushes[Level] = _ambushes.GetValueOrDefault(Level) + 1;
        _ambushCd = AmbushCooldown(NgTier);
        Say("Ambush! They've been waiting for you.");
        PlaySound(Sfx.BossSight, 0.6f);
        return true;
    }
}
