namespace HexenSharp;

/// <summary>Checks on the Grenadier, the Juggernaut, and monsters fighting each other.</summary>
public static partial class Headless
{
    static void BruteChecks(Action<bool, string> check)
    {
        Game g = null;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }
        Monster Put(MonsterDef def, float x, float y, bool awake = true)
        {
            var m = new Monster(def) { X = x, Y = y, Level = g.Level, State = awake ? AiState.Chase : AiState.Idle };
            g.Level.Things.Add(m);
            return m;
        }
        void Yard()
        {
            g = new Game { FixedSeed = 1, AchievementsOn = false };
            g.StartPractice(PClass.Fighter, Courses.FreeRoam);
            g.Level.Things.RemoveAll(t => t is Monster);
            g.P.X = 32.5f; g.P.Y = 32.5f; g.P.Angle = 0;
        }

        // the Grenadier: its grenade arcs over to you, and the blast throws you
        Yard();
        var p = g.P;
        var nadier = Put(Brutes.Grenadier, p.X + 6, p.Y);
        nadier.AttackCd = 0;
        int health = p.Health;
        float pushed = 0;
        bool thrown = false;
        for (int k = 0; k < 35 * 8 && !thrown; k++)
        {
            Tick(new Input());
            pushed = MathF.Max(pushed, p.HSpeed);
            thrown = p.Health < health && pushed > 2;
        }
        check(thrown && health - p.Health <= 35, $"a Grenadier lobs a grenade that throws you about ({pushed:0.0} cells a second) for a little damage ({health - p.Health})");

        // the Juggernaut: its punch shoves you away and up, hard
        Yard();
        p = g.P;
        var jug = Put(Brutes.Juggernaut, p.X + 1.2f, p.Y);
        jug.AttackCd = 0;
        health = p.Health;
        float x0 = p.X, flung = 0, lift = 0;
        for (int k = 0; k < 35 * 3; k++)
        {
            Tick(new Input());
            flung = MathF.Max(flung, x0 - p.X);
            lift = MathF.Max(lift, p.Z);
            if (flung > 2) break;
        }
        check(flung > 2 && lift > 0.2f && health - p.Health <= 12, $"a Juggernaut's punch shoves you {flung:0.0} cells away and up ({lift:0.00}) for little damage ({health - p.Health})");

        // infighting: a missile from one kind of monster hits another, which turns on the thrower
        Yard();
        p = g.P;
        var afrit = Put(Monster.Afrit, p.X + 8, p.Y);
        var ettin = Put(Monster.Ettin, p.X + 5, p.Y);
        var twin = Put(Monster.Afrit, p.X + 6.5f, p.Y + 3);
        Projectile Ball(Monster from, Monster at)
        {
            float a = MathF.Atan2(at.Y - from.Y, at.X - from.X);
            var pr = new Projectile
            {
                Kind = ProjKind.Fireball, FromPlayer = false, Owner = from, DmgMin = 8, DmgMax = 8, Level = g.Level,
                X = from.X + MathF.Cos(a) * 0.5f, Y = from.Y + MathF.Sin(a) * 0.5f, Z = g.Level.FloorAt(from.X, from.Y) + from.Z + from.SpriteH * 0.45f,
                VX = MathF.Cos(a) * 6.5f, VY = MathF.Sin(a) * 6.5f,
            };
            g.Level.Things.Add(pr);
            return pr;
        }
        g.Vars.Freeze = true;
        int ettinHp = ettin.Health;
        Ball(afrit, ettin);
        Tick(new Input(), 20);
        check(ettin.Health < ettinHp && ettin.Enemy == afrit, "an afrit's fireball that hits an ettin hurts it, and it turns on the afrit");
        int twinHp = twin.Health;
        var through = Ball(afrit, twin);
        Tick(new Input(), 20);
        check(twin.Health == twinHp && twin.Enemy == null, "one of its own kind it passes through, unharmed and unprovoked");
        g.Vars.Freeze = false;
        int afritHp = afrit.Health;
        g.Level.Things.Remove(twin);
        for (int k = 0; k < 35 * 10 && afrit.Health == afritHp; k++) Tick(new Input());
        check(afrit.Health < afritHp, "the ettin goes after the afrit and hits it, not you");
        check(g.Infights >= 1, $"infights counted ({g.Infights})");
    }
}
