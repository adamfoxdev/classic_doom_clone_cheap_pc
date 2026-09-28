namespace HexenSharp;

/// <summary>
/// Wave survival for maps with spawn runes ('*'). Stepping on the altar ('!') starts the trials.
/// Each wave brings more monsters, introduces tougher types, and scales their health, damage and speed;
/// every fifth wave adds Heresiarchs. Supplies appear at the altar after each cleared wave.
/// </summary>
public sealed class ArenaState
{
    public bool Started, InIntermission;
    public int Wave, BestWave;
    public float Timer, BannerTime;
    /// <summary>Time since the trials began, and when the last wave was cleared (a run's time on the leaderboard).</summary>
    public float RunTime, ClearedAt;
    /// <summary>This run has gone on the arena leaderboard (it ends once: death, restart or leaving).</summary>
    public bool Recorded;
    public const float Intermission = 6f;
    public const int MaxAlive = 14;

    readonly Level _lv;
    readonly List<(float x, float y)> _spawns = new();
    readonly (float x, float y) _altar;
    readonly Queue<MonsterDef> _pending = new();
    readonly List<Monster> _live = new();
    readonly Random _rng = new(4242);
    float _spawnTimer;
    bool _quietShown;

    public ArenaState(Level lv)
    {
        _lv = lv;
        for (int i = 0; i < lv.Marks.Length; i++)
            if (lv.Marks[i] == '*') _spawns.Add((i % lv.W + 0.5f, i / lv.W + 0.5f));
        _altar = lv.FindMark('!') ?? _spawns[0];
    }

    public int Remaining => _pending.Count + _live.Count(m => m.Alive);
    public IReadOnlyList<Monster> Live => _live;

    /// <summary>Difficulty multipliers for a wave (1-based).</summary>
    public static (float health, float damage, float speed) Scale(int wave) =>
        (1f + 0.15f * (wave - 1), 1f + 0.08f * (wave - 1), MathF.Min(1.5f, 1f + 0.04f * (wave - 1)));

    /// <summary>The monsters a wave is made of: more of them, and nastier, as waves go on.</summary>
    public static List<MonsterDef> Compose(int wave, Random rng)
    {
        var pool = new List<(MonsterDef def, int weight)> { (Monster.Ettin, 4) };
        if (wave >= 2) pool.Add((Monster.Afrit, 3));
        if (wave >= 3) pool.Add((Monster.Centaur, 2 + wave / 3));
        if (wave >= 4) pool.Add((Monster.Bishop, 2 + wave / 5));
        if (wave >= 5) pool.Add((Monster.Slaughtaur, 1 + wave / 4));
        int total = pool.Sum(p => p.weight);

        int count = Math.Min(30, 3 + wave * 2);
        var list = new List<MonsterDef>();
        for (int i = 0; i < count; i++)
        {
            int roll = rng.Next(total);
            foreach (var (def, w) in pool)
            {
                if (roll < w) { list.Add(def); break; }
                roll -= w;
            }
        }
        for (int i = 0; i < wave / 5 && wave % 5 == 0; i++) list.Add(Monster.Heresiarch);
        return list;
    }

    public void Update(Game g, float dt)
    {
        BannerTime -= dt;
        if (g.Relaxed)
        {
            // no waves in relaxed mode
            if (!_quietShown && g.Level.MarkAt(g.P.X, g.P.Y) == '!')
            {
                _quietShown = true;
                g.Say("The altar's bell rings softly. The old games sleep, and the arena is at peace.");
                g.PlaySound(Sfx.Lore, 1);
            }
            return;
        }
        if (!Started)
        {
            if (g.Mode == GameMode.Playing && g.Level.MarkAt(g.P.X, g.P.Y) == '!')
            {
                Started = true;
                g.Say("The trials of chaos begin! Survive the waves.");
                g.PlaySound(Sfx.BossSight, 1);
                BeginWave(g, 1);
            }
            return;
        }

        if (g.Mode == GameMode.Playing) RunTime += dt;
        if (InIntermission)
        {
            Timer -= dt;
            if (Timer <= 0) BeginWave(g, Wave + 1);
            return;
        }

        _live.RemoveAll(m => !m.Alive && m.State == AiState.Dead);
        _spawnTimer -= dt;
        if (_pending.Count > 0 && _spawnTimer <= 0 && _live.Count(m => m.Alive) < MaxAlive)
        {
            if (TrySpawn(g, _pending.Peek())) _pending.Dequeue();
            _spawnTimer = MathF.Max(0.25f, 0.9f - Wave * 0.05f);
        }

        if (_pending.Count == 0 && _live.All(m => !m.Alive))
        {
            InIntermission = true;
            Timer = Intermission;
            BannerTime = 2.5f;
            BestWave = Math.Max(BestWave, Wave);
            ClearedAt = RunTime;
            g.PlaySound(Sfx.Item, 1);
            g.Say($"Wave {Wave} cleared! Supplies have appeared at the altar.");
            g.GainXp(Game.Xp.PerWave * Wave);
            if (g.ArenaMode) g.ArenaWaveCleared(Wave);
            Reward(g);
        }
    }

    void BeginWave(Game g, int wave)
    {
        Wave = wave;
        InIntermission = false;
        BannerTime = 2.5f;
        _spawnTimer = 0.5f;
        _live.Clear();
        foreach (var def in Compose(wave, _rng)) _pending.Enqueue(def);
        g.PlaySound(wave % 5 == 0 ? Sfx.BossSight : Sfx.Sight, 1);
    }

    bool TrySpawn(Game g, MonsterDef def)
    {
        // pick a random spawn rune that is clear and not right next to the player
        int start = _rng.Next(_spawns.Count);
        for (int k = 0; k < _spawns.Count; k++)
        {
            var (x, y) = _spawns[(start + k) % _spawns.Count];
            if (Game.Dist(x, y, g.P.X, g.P.Y) < 2.5f) continue;
            bool busy = _lv.Things.Any(t => t.Solid && !t.Removed && (t is not Monster mm || mm.Alive) && Game.Dist(t.X, t.Y, x, y) < t.Radius + def.Radius + 0.1f);
            if (busy) continue;

            var (hp, dmg, spd) = Scale(Wave);
            if (def.Boss) hp *= 0.5f; // arena Heresiarchs are a bit lighter than the real one
            var m = g.SpawnMonster(def, x, y, hp, dmg, spd);
            _live.Add(m);
            return true;
        }
        return false;
    }

    void Reward(Game g)
    {
        var drops = new List<char> { 'h', 'b', 'g' };
        if (Wave == 1) { drops.Add('w'); drops.Add('x'); }
        if (Wave % 2 == 0) drops.Add('q');
        if (Wave % 3 == 0) drops.Add('r');
        if (Wave % 5 == 0) drops.Add('u');
        for (int i = 0; i < drops.Count; i++)
        {
            float a = i * MathF.Tau / drops.Count;
            float x = _altar.x + MathF.Cos(a) * 1.2f, y = _altar.y + MathF.Sin(a) * 1.2f;
            if (_lv.BlocksPoint(x, y)) (x, y) = _altar;
            var t = ThingFactory.Create(drops[i], x, y);
            t.Level = _lv;
            _lv.Things.Add(t);
        }
        // every third wave, an arsenal upgrade on the altar itself: your weapons keep up with the waves
        if (Wave % Arsenal.Every == 0 && Wave / Arsenal.Every <= Arsenal.MaxTier)
        {
            _lv.Things.Add(new Pickup(PickupKind.Upgrade, 0.5f) { X = _altar.x, Y = _altar.y, Level = _lv });
            g.Say("An arsenal upgrade waits on the altar!");
        }
    }
}

/// <summary>
/// Arena arsenal upgrades: one drops on the altar after every third wave, five in all. Each one makes every weapon hit
/// harder and fire faster; the second adds shots to every volley and lets melee blows cleave through several monsters,
/// the third makes every shot explode, the fourth widens the volleys and cleaves again, the fifth is everything at full.
/// </summary>
public static class Arsenal
{
    public const int MaxTier = 5, Every = 3;
    static readonly string[] Names = { "", "Blessed", "Runed", "Exalted", "Mythic", "Divine" };
    static readonly string[] Effects =
    {
        "",
        "Every weapon hits harder and fires faster.",
        "Volleys fan out and melee blows cleave.",
        "Every shot explodes on impact.",
        "Wider volleys, wider cleaves.",
        "Full power!",
    };
    static readonly uint[] Colours = { 0, Col.Rgb(90, 220, 255), Col.Rgb(120, 255, 140), Col.Rgb(255, 220, 80), Col.Rgb(255, 140, 50), Col.Rgb(255, 80, 230) };

    public static string Name(int tier) => Names[Math.Clamp(tier, 0, MaxTier)];
    public static string Describe(int tier) => Effects[Math.Clamp(tier, 0, MaxTier)];
    public static uint Colour(int tier) => Colours[Math.Clamp(tier, 0, MaxTier)];
    public static float Damage(int tier) => 1f + 0.35f * tier;
    public static float FireRate(int tier) => 1f + 0.12f * tier;
    public static float Reach(int tier) => 0.15f * tier;
    public static int Cleave(int tier) => tier >= 4 ? 4 : tier >= 2 ? 3 : 1;
    public static int ExtraShots(int tier) => tier >= 4 ? 4 : tier >= 2 ? 2 : 0;
    public static float Splash(float splash, int tier) => splash > 0 ? splash * (1 + 0.2f * tier) : tier >= 3 ? 0.6f + 0.1f * (tier - 3) : 0f;
}

/// <summary>
/// Arena medals: for the waves a run clears, the same for every class. Every fifth wave brings Heresiarchs, so each
/// medal is for getting past one more of those.
/// </summary>
public static class ArenaMedals
{
    public const int Bronze = 5, Silver = 10, Gold = 15;

    public static Medal For(int waves) => waves >= Gold ? Medal.Gold : waves >= Silver ? Medal.Silver : waves >= Bronze ? Medal.Bronze : Medal.None;

    /// <summary>The next medal after `waves` cleared, and the wave it takes (None, 0 once you have gold).</summary>
    public static (Medal medal, int wave) Next(int waves) =>
        waves < Bronze ? (Medal.Bronze, Bronze) : waves < Silver ? (Medal.Silver, Silver) : waves < Gold ? (Medal.Gold, Gold) : (Medal.None, 0);
}
