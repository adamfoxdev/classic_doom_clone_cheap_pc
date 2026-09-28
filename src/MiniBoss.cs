namespace HexenSharp;

/// <summary>What makes a mini-boss more than a big monster.</summary>
public enum Special { None, Tunneller, Charger, Summoner, Blinker, Burrower, Dreadnought }

/// <summary>
/// The optional maps' mini-bosses, one each: bigger, recoloured versions of the usual monsters, each with a trick of
/// its own. They're placed when the hub's maps are built (not with map glyphs, so the editor's palette stays as it
/// is), show a health bar while you fight them, and drop a Mystic Urn and armour when they fall.
/// </summary>
public static class MiniBosses
{
    /// <summary>
    /// Deepdelve Quarry: the Quarry Warden (a mining mech in sci-fi) wakes when you come near, even through rock, and
    /// burrows straight through the rubble to you. Up close it slams the ground: jump as it raises its fists.
    /// </summary>
    public static readonly MonsterDef Warden = new()
    {
        Name = "Quarry Warden", Art = "warden", Health = 450, Speed = 1.3f, Radius = 0.42f, Width = 1.3f, Height = 1.35f,
        MeleeRange = 1.1f, MeleeMin = 16, MeleeMax = 26, AttackTime = 0.6f, Cooldown = 0.9f, PainChance = 0.1f,
        Special = Special.Tunneller, WakeRange = 7f, MiniBoss = "warden",
    };

    /// <summary>
    /// The Barren World: the Dust Stalker (a rogue harvester) lowers its head, then charges in a straight line. Step
    /// aside, and if it hits a wall it's stunned for a moment and takes double damage.
    /// </summary>
    public static readonly MonsterDef Stalker = new()
    {
        Name = "Dust Stalker", Art = "stalker", Health = 380, Speed = 2.1f, Radius = 0.4f, Width = 1.25f, Height = 1.2f,
        MeleeRange = 1.1f, MeleeMin = 14, MeleeMax = 22, AttackTime = 0.5f, Cooldown = 0.8f, PainChance = 0.15f,
        SightRange = 16f, Special = Special.Charger, MiniBoss = "stalker",
    };

    /// <summary>The Verdant Moon: the Thornmother (a hive queen) floats above the meadow, fires seekers and calls her brood.</summary>
    public static readonly MonsterDef Thornmother = new()
    {
        Name = "Thornmother", Art = "thornmother", Health = 350, Speed = 1.5f, Radius = 0.36f, Width = 1.05f, Height = 1.15f, FlyZ = 0.35f,
        Missile = ProjKind.Seeker, MissileCount = 3, MissileSpread = 0.4f, AttackTime = 0.6f, Cooldown = 2.0f, PainChance = 0.15f,
        SightRange = 16f, Special = Special.Summoner, MiniBoss = "thornmother",
    };

    /// <summary>
    /// The Hanging Cisterns: the Drowned Keeper (a coolant wraith) flings fireballs and, each time it loses a fifth of its
    /// health, vanishes and reappears somewhere else, often up on a ledge where only a flyer can follow.
    /// </summary>
    public static readonly MonsterDef Keeper = new()
    {
        Name = "Drowned Keeper", Art = "keeper", Health = 320, Speed = 2.0f, Radius = 0.32f, Width = 0.95f, Height = 0.9f, FlyZ = 0.45f,
        Missile = ProjKind.Fireball, MissileCount = 3, MissileSpread = 0.18f, AttackTime = 0.5f, Cooldown = 1.3f, PainChance = 0.2f,
        SightRange = 16f, Special = Special.Blinker, MiniBoss = "keeper",
    };

    /// <summary>
    /// The Bedrock Depths: the Rock Wyrm (a borer drone) swims through the solid rock, unseen and untouchable, with only
    /// dust to give it away. It bursts out beside you to bite, and after a few seconds dives back into the stone.
    /// </summary>
    public static readonly MonsterDef Wyrm = new()
    {
        Name = "Rock Wyrm", Art = "wyrm", Health = 420, Speed = 1.6f, Radius = 0.4f, Width = 1.25f, Height = 1.1f,
        MeleeRange = 1.15f, MeleeMin = 18, MeleeMax = 28, AttackTime = 0.5f, Cooldown = 0.7f, PainChance = 0.1f,
        Special = Special.Burrower, WakeRange = 10f, MiniBoss = "wyrm",
    };

    /// <summary>
    /// The Void Crossing: the Storm Leviathan (a void dreadnought) drops out of the dark near the end of the lane and
    /// keeps pace just ahead of your ship, weaving across and up and down, firing volleys back at you.
    /// </summary>
    public static readonly MonsterDef Dreadnought = new()
    {
        Name = "Storm Leviathan", Art = "dreadnought", Health = 600, Speed = 9f, Radius = 0.7f, Width = 2.0f, Height = 1.5f, FlyZ = 1.2f,
        Missile = ProjKind.BossBall, MissileCount = 3, MissileSpread = 0.2f, AttackTime = 0.6f, Cooldown = 1.6f, PainChance = 0.05f,
        SightRange = 30f, Special = Special.Dreadnought, MiniBoss = "dreadnought",
    };

    public static readonly MonsterDef[] All = { Warden, Stalker, Thornmother, Keeper, Wyrm, Dreadnought };

    /// <summary>Where each one waits: its map and cell.</summary>
    public static readonly (string map, MonsterDef def, float x, float y)[] Places =
    {
        ("Deepdelve Quarry", Warden, 15.5f, 9.5f),      // the sealed cave in the middle of the rock field
        ("Barren World", Stalker, 16.5f, 18.5f),        // the open plain south of the wreck
        ("Verdant Moon", Thornmother, 20.5f, 9.5f),     // the meadow east of the outpost
        ("Hanging Cisterns", Keeper, 18.5f, 9.5f),      // the cistern floor, under the ledges
        ("Bedrock Depths", Wyrm, 5.5f, 5.5f),           // somewhere in the rock
        ("Void Crossing", Dreadnought, 88.5f, 6.5f),    // lying in wait near the end of the lane
    };

    /// <summary>Puts a map's mini-boss in, if it has one.</summary>
    public static void Place(Level lv)
    {
        foreach (var (map, def, x, y) in Places)
            if (lv.RawName == map)
                lv.Things.Add(new Monster(def) { X = x, Y = y, Level = lv, NextBlinkHp = (int)(def.Health * 0.8f), Burrowed = def.Special == Special.Burrower, Solid = def.Special != Special.Burrower });
    }

    /// <summary>Each one's look: the monster it's built from and the colour it's washed with.</summary>
    static readonly (string key, string from, int r, int g, int b)[] Looks =
    {
        ("warden", "ettin", 210, 120, 60),
        ("stalker", "centaur", 230, 190, 120),
        ("thornmother", "bishop", 110, 220, 100),
        ("keeper", "afrit", 90, 170, 255),
        ("wyrm", "slaughtaur", 150, 130, 110),
        ("dreadnought", "heresiarch", 230, 80, 170),
    };

    /// <summary>Makes the mini-bosses' sprites from the current style's monsters (call once those are built).</summary>
    public static void BuildArt()
    {
        foreach (var (key, from, r, g, b) in Looks)
            if (HexenSharp.Art.Monsters.TryGetValue(from, out var set))
                HexenSharp.Art.Monsters[key] = set.Select(t => Wash(t, r, g, b)).ToArray();
    }

    /// <summary>Recolours a sprite: each pixel keeps its brightness but takes on the colour, with the brightest kept a little hotter.</summary>
    static Tex Wash(Tex src, int r, int g, int b)
    {
        var t = new Tex(src.W, src.H);
        for (int i = 0; i < src.Px.Length; i++)
        {
            uint c = src.Px[i];
            int a = Col.A(c);
            if (a == 0) continue;
            int lum = (Col.R(c) * 77 + Col.G(c) * 150 + Col.B(c) * 29) >> 8;
            uint tinted = Col.Rgb(r * lum / 200, g * lum / 200, b * lum / 200);
            uint mixed = Col.Lerp(c, tinted, lum > 220 ? 120 : 190);
            t.Px[i] = (mixed & 0x00FFFFFFu) | ((uint)a << 24);
        }
        return t;
    }
}

public sealed partial class Game
{
    /// <summary>A mini-boss's extra experience when it falls, on top of the usual kill.</summary>
    public const int MiniBossXp = 250;

    /// <summary>
    /// A mini-boss's special behaviour, run before its usual AI. True when it has the monster's turn to itself
    /// (winding up, charging, slamming, stunned).
    /// </summary>
    bool MiniBossTick(Monster m, float dt, float dist)
    {
        var def = m.Def;
        m.SpecialCd -= dt;
        bool playerAlive = Mode != GameMode.Dead;
        switch (def.Special)
        {
            case Special.Tunneller:
                if (m.State == AiState.Idle && playerAlive && !Vars.NoTarget && dist < def.WakeRange) { Wake(m); return true; }
                if (m.State != AiState.Chase && m.SpecialPhase == 0) return false;
                if (m.SpecialPhase == 1)
                {
                    // fists raised: then the ground shakes, and only a jump saves you
                    m.SpecialTime -= dt;
                    m.State = AiState.Attack;
                    if (m.SpecialTime > 0) return true;
                    m.SpecialPhase = 0;
                    SetState(m, AiState.Chase);
                    m.SpecialCd = 4f;
                    Sound(Sfx.Explode, m.X, m.Y);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * MathF.Tau / 8;
                        SpawnPuff(Art.RubbleChunk, m.X + MathF.Cos(a) * 1.3f, m.Y + MathF.Sin(a) * 1.3f, Level.FloorAt(m.X, m.Y) + 0.2f, 0.5f);
                    }
                    if (playerAlive && dist < SlamRadius && P.Z < 0.15f && MathF.Abs(Level.FloorAt(m.X, m.Y) - P.FloorZ) < 0.8f)
                    {
                        DamagePlayer((int)(SlamDamage * m.DamageMult));
                        Say($"The {def.Name} shakes the ground! Jump when it raises its fists.");
                    }
                    return true;
                }
                if (dist < 2.6f && m.SpecialCd <= 0 && playerAlive && !Vars.NoTarget)
                {
                    m.SpecialPhase = 1; m.SpecialTime = 0.7f;
                    Sound(Sfx.BossSight, m.X, m.Y);
                    return true;
                }
                Tunnel(m, dt);
                return false;

            case Special.Charger:
                if (m.SpecialPhase == 0)
                {
                    if (m.State == AiState.Chase && m.SpecialCd <= 0 && playerAlive && !Vars.NoTarget && dist > 3f && dist < 14f && Level.Sight(m.X, m.Y, P.X, P.Y))
                    {
                        m.SpecialPhase = 1; m.SpecialTime = 0.7f;
                        Sound(Sfx.Sight, m.X, m.Y);
                        return true;
                    }
                    return false;
                }
                m.SpecialTime -= dt;
                if (m.SpecialPhase == 1)
                {
                    // head down, pawing: it charges where you are when it goes
                    m.State = AiState.Attack;
                    float tx = P.X - m.X, ty = P.Y - m.Y, l = MathF.Max(0.01f, MathF.Sqrt(tx * tx + ty * ty));
                    m.DashX = tx / l; m.DashY = ty / l;
                    if (m.SpecialTime <= 0) { m.SpecialPhase = 2; m.SpecialTime = 1.4f; Sound(Sfx.Slide, m.X, m.Y); }
                    return true;
                }
                if (m.SpecialPhase == 2)
                {
                    m.State = AiState.Chase;
                    m.Anim += dt * 3;
                    float step = ChargeSpeed * m.SpeedMult * Vars.MonsterSpeed * dt;
                    float nx = m.X + m.DashX * step, ny = m.Y + m.DashY * step;
                    if (playerAlive && Dist(nx, ny, P.X, P.Y) < m.Radius + P.Radius + 0.15f)
                    {
                        DamagePlayer((int)(ChargeDamage * m.DamageMult));
                        P.VX += m.DashX * 6; P.VY += m.DashY * 6;
                        EndCharge(m, 3.5f);
                        return true;
                    }
                    if (Blocked(nx, ny, m.Radius, m))
                    {
                        // into the wall: dazed, and wide open
                        m.SpecialPhase = 3; m.SpecialTime = StunTime;
                        SetState(m, AiState.Pain);
                        Sound(Sfx.Land, m.X, m.Y);
                        Say($"The {def.Name} is stunned!");
                        return true;
                    }
                    m.X = nx; m.Y = ny;
                    if (m.SpecialTime <= 0) EndCharge(m, 3f);
                    return true;
                }
                // stunned
                m.State = AiState.Pain; m.StateTime = 0;
                if (m.SpecialTime <= 0) EndCharge(m, 3f);
                return true;

            case Special.Summoner:
                if (m.State == AiState.Chase && m.SpecialCd <= 0 && playerAlive && !Vars.NoTarget && Level.Sight(m.X, m.Y, P.X, P.Y))
                {
                    m.SpecialCd = 8f;
                    int brood = Level.Things.Count(t => t is Monster o && o.Summoner == m && o.Alive);
                    for (int k = 0; k < 2 && brood < MaxBrood; k++)
                    {
                        for (int tries = 0; tries < 8; tries++)
                        {
                            float a = RandF() * MathF.Tau, x = m.X + MathF.Cos(a) * 1.4f, y = m.Y + MathF.Sin(a) * 1.4f;
                            if (Level.BlocksCircle(x, y, Monster.Afrit.Radius) || Blocked(x, y, Monster.Afrit.Radius, null)) continue;
                            SpawnMonster(Monster.Afrit, x, y, 1, 1, 1).Summoner = m;
                            brood++;
                            break;
                        }
                    }
                    Say($"The {def.Name} calls her brood!");
                }
                return false;

            case Special.Burrower:
                return WyrmTick(m, dt, dist);

            case Special.Dreadnought:
                return DreadnoughtTick(m, dt, dist);

            case Special.Blinker:
                if (m.State is AiState.Chase or AiState.Pain && m.Health <= m.NextBlinkHp && m.Health > 0)
                {
                    m.NextBlinkHp -= m.MaxHealth / 5;
                    Blink(m);
                    return true;
                }
                return false;
        }
        return false;
    }

    public const float SlamRadius = 3f, ChargeSpeed = 9f, StunTime = 1.6f, BurrowSpeed = 2.4f, SurfaceTime = 4f;

    /// <summary>
    /// The Rock Wyrm: it swims through the rock toward you (untouchable, only dust shows where), bursts out of the
    /// stone beside you, fights for a few seconds, and dives back in.
    /// </summary>
    bool WyrmTick(Monster m, float dt, float dist)
    {
        bool playerAlive = Mode != GameMode.Dead;
        if (m.Burrowed)
        {
            m.Solid = false;
            if (m.State == AiState.Idle)
            {
                if (playerAlive && !Vars.NoTarget && dist < m.Def.WakeRange) { m.State = AiState.Chase; Say($"Something is moving in the rock..."); Sound(Sfx.Break, m.X, m.Y); }
                return true;
            }
            // through the stone, straight at you; a puff of dust now and then gives it away
            float tx = P.X - m.X, ty = P.Y - m.Y, l = MathF.Max(0.01f, MathF.Sqrt(tx * tx + ty * ty));
            float step = BurrowSpeed * Vars.MonsterSpeed * dt;
            m.X = Math.Clamp(m.X + tx / l * step, 1.5f, Level.W - 1.5f);
            m.Y = Math.Clamp(m.Y + ty / l * step, 1.5f, Level.H - 1.5f);
            if ((m.SpecialTime -= dt) <= 0)
            {
                m.SpecialTime = 0.35f;
                SpawnPuff(Art.RubbleChunk, m.X, m.Y, P.FloorZ + 0.2f, 0.35f);
                if (dist < 6) Sound(Sfx.Push, m.X, m.Y);
            }
            if (playerAlive && dist < 1.9f && dist > 0.8f)
            {
                // out it comes: the stone where it is gives way at your level
                int cx = (int)m.X, cy = (int)m.Y;
                if (Level.Cell(cx, cy) == Level.Rubble) Level.DamageBlock(cx, cy, 100000, Level.Face.Wall, P.FloorZ);
                if (Level.Cell(cx, cy) != '\0') return true; // bedrock: keep circling
                m.X = cx + 0.5f; m.Y = cy + 0.5f;
                m.Burrowed = false; m.Solid = true;
                m.SpecialTime = SurfaceTime;
                SetState(m, AiState.Chase);
                m.AttackCd = 0.4f;
                Sound(Sfx.Break, m.X, m.Y);
                Sound(Sfx.BossSight, m.X, m.Y);
                for (int k = 0; k < 5; k++) SpawnPuff(Art.RubbleChunk, m.X + (RandF() - 0.5f), m.Y + (RandF() - 0.5f), P.FloorZ + 0.3f + RandF() * 0.5f, 0.4f);
            }
            return true;
        }
        // out in the open: a few seconds of fighting (the clock runs on through its bites), then back into the rock
        m.SpecialTime -= dt;
        if (m.SpecialTime <= 0 && m.State is AiState.Chase or AiState.Pain)
        {
            m.Burrowed = true; m.Solid = false;
            m.SpecialTime = 0.35f;
            SetState(m, AiState.Chase);
            Sound(Sfx.Break, m.X, m.Y);
            SpawnPuff(Art.RubbleChunk, m.X, m.Y, P.FloorZ + 0.3f, 0.6f);
            return true;
        }
        return false;
    }

    /// <summary>
    /// The Storm Leviathan: once you're near the end of the lane it keeps a few lengths ahead of you, weaving across the
    /// lane and up and down, and turns its guns back on you. It never sits on the portal home.
    /// </summary>
    bool DreadnoughtTick(Monster m, float dt, float dist)
    {
        if (m.State == AiState.Idle)
        {
            if (Mode != GameMode.Dead && !Vars.NoTarget && P.X > m.X - 18f) { Wake(m); Say($"The {m.Def.Name} drops out of the dark ahead!"); }
            return true;
        }
        m.Anim += dt;
        float lane = Level.H - 2f;
        float wantX = MathF.Min(P.X + DreadnoughtLead, Level.W - 8f);
        float wantY = 1f + lane * (0.5f + 0.38f * MathF.Sin(m.Anim * 0.9f));
        float wantZ = 0.5f + 1.6f * (0.5f + 0.5f * MathF.Sin(m.Anim * 1.3f + 1f));
        float k = MathF.Min(1, dt * 2.5f), maxStep = m.Def.Speed * Vars.MonsterSpeed * dt;
        m.X += Math.Clamp((wantX - m.X) * k, -maxStep, maxStep);
        m.Y += Math.Clamp((wantY - m.Y) * k, -maxStep, maxStep);
        m.Z += (wantZ - m.Z) * k;
        if (m.State != AiState.Chase) return false; // attacking or in pain: the usual AI fires the volley
        m.AttackCd -= dt;
        if (m.AttackCd <= 0 && Mode != GameMode.Dead && !Vars.NoTarget && dist < 18f) SetState(m, AiState.Attack);
        return true;
    }

    public const float DreadnoughtLead = 7f;
    public const int SlamDamage = 25, ChargeDamage = 28, MaxBrood = 4;

    void EndCharge(Monster m, float cooldown)
    {
        m.SpecialPhase = 0;
        m.SpecialCd = cooldown;
        SetState(m, AiState.Chase);
        m.AttackCd = 0.5f;
    }

    /// <summary>The Warden's way through: rubble in its path, toward you, crumbles as it pushes on.</summary>
    void Tunnel(Monster m, float dt)
    {
        float tx = P.X - m.X, ty = P.Y - m.Y, l = MathF.Max(0.01f, MathF.Sqrt(tx * tx + ty * ty));
        int cx = (int)(m.X + tx / l * (m.Radius + 0.35f)), cy = (int)(m.Y + ty / l * (m.Radius + 0.35f));
        if (Level.Cell(cx, cy) != Level.Rubble) { m.SpecialTime = 0; return; }
        // also along each axis, so it doesn't stall on a diagonal
        m.SpecialTime += dt;
        if (m.SpecialTime < 0.5f) return;
        m.SpecialTime = 0;
        foreach (var (bx, by) in new[] { (cx, cy), ((int)(m.X + MathF.Sign(tx) * (m.Radius + 0.35f)), (int)m.Y), ((int)m.X, (int)(m.Y + MathF.Sign(ty) * (m.Radius + 0.35f))) })
        {
            if (Level.Cell(bx, by) != Level.Rubble) continue;
            Level.DamageBlock(bx, by, 100000, Level.Face.Wall, Level.FloorAt(m.X, m.Y));
            Sound(Sfx.Break, bx + 0.5f, by + 0.5f);
            SpawnPuff(Art.RubbleChunk, bx + 0.5f, by + 0.5f, Level.FloorAt(m.X, m.Y) + 0.5f, 0.6f);
        }
        m.StuckTime = 0;
    }

    /// <summary>The Keeper vanishes and reappears on open floor away from you, preferring somewhere higher up.</summary>
    void Blink(Monster m)
    {
        var lv = Level;
        (float x, float y)? best = null;
        float bestScore = float.MinValue;
        for (int tries = 0; tries < 60; tries++)
        {
            int cx = 1 + (int)(RandF() * (lv.W - 2)), cy = 1 + (int)(RandF() * (lv.H - 2));
            int i = cy * lv.W + cx;
            float x = cx + 0.5f, y = cy + 0.5f;
            // a teleport, so no step is too high: the spot just has to be clear, and all on one level
            if (lv.Cells[i] != '\0' || lv.Marks[i] != '\0' || lv.BlocksCircle(x, y, m.Radius) || lv.TooHigh(x, y, m.Radius, lv.Floors[i], 0.01f)) continue;
            if (lv.Things.Any(t => t != m && t.Solid && !t.Removed && (t is not Monster o || o.Alive) && Dist(t.X, t.Y, x, y) < t.Radius + m.Radius)) continue;
            float d = Dist(x, y, P.X, P.Y);
            if (d < 5f || d > 16f || !lv.Sight(x, y, P.X, P.Y)) continue;
            float score = lv.Floors[i] * 2 + RandF() * 3;
            if (score > bestScore) { bestScore = score; best = (x, y); }
        }
        if (best is not var (bx, by)) return;
        SpawnPuff(Art.BossBall[1], m.X, m.Y, Level.FloorAt(m.X, m.Y) + 0.5f, 0.9f);
        m.X = bx; m.Y = by;
        SpawnPuff(Art.BossBall[1], m.X, m.Y, Level.FloorAt(m.X, m.Y) + 0.5f, 0.9f);
        Sound(Sfx.Teleport, m.X, m.Y);
        SetState(m, AiState.Chase);
        m.AttackCd = 0.8f;
    }

    /// <summary>A mini-boss falls: its loot, its experience, and it's crossed off your list for good.</summary>
    void MiniBossDown(Monster m)
    {
        Say($"The {m.Def.Name} falls!");
        PlaySound(Sfx.BossSight, 0.8f);
        foreach (var (kind, dx) in new[] { (PickupKind.Urn, -0.35f), (PickupKind.Armor, 0.35f) })
        {
            float x = m.X + dx, y = m.Y;
            if (Level.BlocksPoint(x, y)) x = m.X;
            // in flight, the loot hangs in the lane where it fell, for the ship to fly through
            Level.Things.Add(new Pickup(kind, 0.45f) { X = x, Y = y, Z = Level.Flight ? m.Z : 0, Level = Level });
        }
        GainXp(MiniBossXp);
        if (!NoXp && !Profile.MiniBosses.Contains(m.Def.MiniBoss)) Profile.MiniBosses.Add(m.Def.MiniBoss);
        // the brood goes with her
        foreach (var t in Level.Things.OfType<Monster>().Where(o => o.Summoner == m && o.Alive).ToList()) DamageMonster(t, 100000);
        SaveProfile();
    }

    /// <summary>The mini-boss (or the Heresiarch) you're fighting, for the health bar: awake, alive, and close.</summary>
    public Monster BossInFight() =>
        P == null || Level == null ? null
        : Level.Things.OfType<Monster>()
            .Where(m => (m.Def.MiniBoss != null || m.Def.Boss) && m.Alive && m.State != AiState.Idle && Dist(m.X, m.Y, P.X, P.Y) < 20f)
            .OrderBy(m => Dist(m.X, m.Y, P.X, P.Y)).FirstOrDefault();
}
