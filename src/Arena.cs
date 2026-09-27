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
            g.PlaySound(Sfx.Item, 1);
            g.Say($"Wave {Wave} cleared! Supplies have appeared at the altar.");
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
    }
}
