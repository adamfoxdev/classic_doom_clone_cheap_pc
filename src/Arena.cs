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
    /// <summary>The run's modifiers (picked before it starts), and the perks it has earned, with their ranks.</summary>
    public ArenaMod Mods;
    public readonly Dictionary<Perk, int> Perks = new();
    /// <summary>The three perks on offer after a boss wave, until you pick one (the next wave waits); else null.</summary>
    public Perk[] Offer;
    /// <summary>The highlighted perk on offer (Left/Right to move, Enter to take it), for pads and arrow keys.</summary>
    public int OfferCursor;
    float _regen, _mana;

    public int Rank(Perk p) => Perks.TryGetValue(p, out int r) ? r : 0;
    public bool Has(ArenaMod m) => (Mods & m) != 0;

    /// <summary>Takes one of the perks on offer (0-2) and lets the next wave come.</summary>
    public Perk Choose(Game g, int index)
    {
        var perk = Offer[index];
        Perks[perk] = Rank(perk) + 1;
        Offer = null;
        Timer = Intermission;
        if (perk == Perk.Vitality) { g.ApplyProfile(); g.P.Health = g.P.MaxHealth; }
        g.Say($"{PerkInfo.Name(perk)} {PerkInfo.Roman(Rank(perk))}: {PerkInfo.About(perk).ToLowerInvariant()}.");
        g.PlaySound(Sfx.Secret, 1);
        return perk;
    }

    /// <summary>Three different perks to choose from: any that isn't maxed (and no mana perk when you're melee only).</summary>
    public Perk[] RollOffer()
    {
        var pool = Enum.GetValues<Perk>().Where(p => Rank(p) < PerkInfo.MaxRank && !(p == Perk.ManaFont && Has(ArenaMod.MeleeOnly))).ToList();
        var pick = new List<Perk>();
        while (pick.Count < 3 && pool.Count > 0) { int i = _rng.Next(pool.Count); pick.Add(pool[i]); pool.RemoveAt(i); }
        return pick.ToArray();
    }
    public const float Intermission = 6f;
    public const int MaxAlive = 14;

    readonly Level _lv;
    readonly List<(float x, float y)> _spawns = new();
    readonly (float x, float y) _altar;
    readonly Queue<MonsterDef> _pending = new();
    readonly List<Monster> _live = new();
    Random _rng = new(4242);

    /// <summary>Sets the arena's dice (the daily challenge seeds them from the date).</summary>
    public void Reseed(int seed) => _rng = new Random(seed);
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

        if (g.Mode == GameMode.Playing) PerkTick(g, dt);
        if (Offer != null) return; // choosing a perk: the clock and the next wave wait
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
            if (!Has(ArenaMod.NoSupplies)) Reward(g);
            ArsenalDrop(g); // not a supply: it comes even with No supplies
            // after a boss wave, a perk to pick
            if (g.ArenaMode && Wave % 5 == 0)
            {
                Offer = RollOffer();
                OfferCursor = 0;
                if (Offer.Length == 0) Offer = null;
                else { g.Say("Choose a perk: press 1, 2 or 3."); g.PlaySound(Sfx.Lore, 1); }
            }
        }
    }

    /// <summary>The perks that work over time: regeneration and the mana font.</summary>
    void PerkTick(Game g, float dt)
    {
        var p = g.P;
        int regen = Rank(Perk.Regeneration), font = Rank(Perk.ManaFont);
        if (regen > 0 && p.Health < p.MaxHealth)
        {
            _regen += dt * 0.5f * regen;
            while (_regen >= 1 && p.Health < p.MaxHealth) { p.Health++; _regen -= 1; }
        }
        else _regen = 0;
        if (font > 0)
        {
            _mana += dt * font;
            while (_mana >= 1) { p.BlueMana = Math.Min(200, p.BlueMana + 1); p.GreenMana = Math.Min(200, p.GreenMana + 1); _mana -= 1; }
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
            if (Has(ArenaMod.DoubleSpeed)) spd *= 2;
            var m = g.SpawnMonster(def, x, y, hp, dmg, spd);
            _live.Add(m);
            return true;
        }
        return false;
    }

    void Reward(Game g)
    {
        var drops = new List<char> { 'h', 'b', 'g' };
        if (Wave == 1 && !Has(ArenaMod.MeleeOnly)) { drops.Add('w'); drops.Add('x'); }
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

    /// <summary>Every third wave, an arsenal upgrade on the altar itself: your weapons keep up with the waves.</summary>
    void ArsenalDrop(Game g)
    {
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

/// <summary>Perks you pick in the arena after each boss wave (every fifth); each can be taken up to three times.</summary>
public enum Perk { RapidFire, Regeneration, ChainLightning, Might, Swiftness, Vitality, Bloodthirst, ManaFont }

public static class PerkInfo
{
    public const int MaxRank = 3;

    public static string Name(Perk p) => p switch
    {
        Perk.RapidFire => "Rapid Fire",
        Perk.ChainLightning => "Chain Lightning",
        Perk.ManaFont => "Mana Font",
        _ => p.ToString(),
    };

    /// <summary>What one rank does.</summary>
    public static string About(Perk p) => p switch
    {
        Perk.RapidFire => "+20% fire rate",
        Perk.Regeneration => "Heal 1 health every 2 seconds",
        Perk.ChainLightning => "Your hits arc to a nearby foe for 40% damage",
        Perk.Might => "+20% damage",
        Perk.Swiftness => "+10% speed",
        Perk.Vitality => "+25 max health, and a full heal",
        Perk.Bloodthirst => "Heal 4 health for every kill",
        _ => "Refill 1 blue and green mana a second",
    };

    /// <summary>A short name for the HUD's perk list.</summary>
    public static string Short(Perk p) => p switch
    {
        Perk.RapidFire => "RAPID",
        Perk.Regeneration => "REGEN",
        Perk.ChainLightning => "CHAIN",
        Perk.Might => "MIGHT",
        Perk.Swiftness => "SWIFT",
        Perk.Vitality => "VITAL",
        Perk.Bloodthirst => "BLOOD",
        _ => "MANA",
    };

    public static string Roman(int rank) => rank switch { 1 => "I", 2 => "II", 3 => "III", _ => rank.ToString() };
}

/// <summary>
/// Arena modifiers, picked before a run: each makes it harder (or less predictable) and raises its score. The medals
/// still go by the waves cleared.
/// </summary>
[Flags]
public enum ArenaMod { None = 0, DoubleSpeed = 1, NoSupplies = 2, MeleeOnly = 4, RandomClass = 8 }

public static class ArenaModInfo
{
    public static readonly ArenaMod[] All = { ArenaMod.DoubleSpeed, ArenaMod.NoSupplies, ArenaMod.MeleeOnly, ArenaMod.RandomClass };

    public static string Name(ArenaMod m) => m switch
    {
        ArenaMod.DoubleSpeed => "Double-speed monsters",
        ArenaMod.NoSupplies => "No supplies",
        ArenaMod.MeleeOnly => "Melee only",
        _ => "Random class",
    };

    /// <summary>The letter each modifier shows as on the leaderboard.</summary>
    public static char Letter(ArenaMod m) => m switch { ArenaMod.DoubleSpeed => 'S', ArenaMod.NoSupplies => 'N', ArenaMod.MeleeOnly => 'M', _ => 'R' };

    /// <summary>What each modifier adds to the score multiplier.</summary>
    public static float Bonus(ArenaMod m) => m switch { ArenaMod.DoubleSpeed => 0.6f, ArenaMod.NoSupplies => 0.4f, ArenaMod.MeleeOnly => 0.5f, _ => 0.15f };

    public static string About(ArenaMod m) => m switch
    {
        ArenaMod.DoubleSpeed => "MONSTERS MOVE TWICE AS FAST.",
        ArenaMod.NoSupplies => "NO HEALTH, MANA OR ITEMS BETWEEN WAVES AND A BARE ARMOURY (ARSENAL UPGRADES STILL COME).",
        ArenaMod.MeleeOnly => "ONLY YOUR FIRST WEAPON: NO STAFFS, AXES OR SPELLS.",
        _ => "A RANDOM CLASS EACH RUN, INSTEAD OF PICKING ONE.",
    };

    public static float Multiplier(ArenaMod mods) => 1 + All.Where(m => (mods & m) != 0).Sum(Bonus);

    /// <summary>A run's score: 100 a wave, times the modifiers' multiplier and the difficulty's (half on Easy, 1.5 on Nightmare).</summary>
    public static int Score(int waves, ArenaMod mods, Difficulty difficulty = Difficulty.Normal) =>
        (int)MathF.Round(waves * 100 * Multiplier(mods) * Difficulties.ScoreFactor(difficulty));

    public static string Letters(ArenaMod mods) => mods == ArenaMod.None ? "-" : new string(All.Where(m => (mods & m) != 0).Select(Letter).ToArray());

    /// <summary>A leaderboard run's letters: its modifiers, then E (Easy) or X (Nightmare).</summary>
    public static string Letters(ArenaMod mods, Difficulty d) =>
        Difficulties.Letter(d) is { Length: > 0 } dl ? (mods == ArenaMod.None ? dl : Letters(mods) + dl) : Letters(mods);
}
