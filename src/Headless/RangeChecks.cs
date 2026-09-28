namespace HexenSharp;

/// <summary>Checks on the rocket launcher (Quake's rules: blast, self-damage, knockback, rocket jumps) and the shooting range.</summary>
public static partial class Headless
{
    static void RangeChecks(Action<bool, string> check)
    {
        Game g = null;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }

        // the range: a full loadout, but only your class's first weapon in hand
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.StartPractice(PClass.Cleric, ShootingRange.Course);
        var p = g.P;
        check(g.OnRange && p.Weapons.Length == 10 && p.HasWeapon.Count(h => h) == 1 && p.CurWeapon == ClassDef.All[1].Weapons[0],
            "on the shooting range, every weapon in the game is there to take, starting with your class's first in hand");
        var rack = g.Level.Things.OfType<Pickup>().Where(k => k.Kind == PickupKind.Arms).ToList();
        check(rack.Count == 10 && rack.Select(k => k.Variant).Distinct().Count() == 10, "the rack holds all ten: three for each class and the rocket launcher");
        foreach (var pk in rack) { p.X = pk.X; p.Y = pk.Y; p.FloorZ = 0; Tick(new Input()); }
        check(p.HasWeapon.All(h => h) && rack.All(k => !k.Removed), "walking along the rack takes every weapon, and the rack stays full");
        Tick(new Input { Slot = 10 }); Tick(new Input(), 10);
        check(p.CurWeapon.Rocket && g.PracticeSpeed == 1, "0 takes up the rocket launcher");
        Tick(new Input { Slot = 2 }); Tick(new Input(), 10);
        check(p.CurWeapon == ClassDef.All[0].Weapons[1] && g.PracticeSpeed == 1, "on the range 1-3 pick weapons (Timon's Axe on 2), not the game speed");
        check(g.Menu.Items(MenuPage.Pause).Contains("Leaderboard") && !g.Menu.Items(MenuPage.Pause).Contains("Watch demo"), "the range's pause menu has its leaderboard and no demo");

        // steep aim: the view only tilts so far, so the launcher's aim swings on past it
        float proj = 160f / MathF.Tan(g.Vars.Fov * MathF.PI / 360f);
        float deg(float a) => a * 180f / MathF.PI;
        check(MathF.Abs(Rockets.AimAngle(20, proj) - MathF.Atan(20 / proj)) < 1e-5f && MathF.Abs(deg(Rockets.AimAngle(-70, proj)) + 85) < 0.01f,
            $"a rocket follows your view near level, and aims {deg(-Rockets.AimAngle(-70, proj)):0} degrees down at the bottom of the tilt");

        // rocket damage: 100-120 direct, the blast falling off in 3D to nothing at 3 cells, and a push
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.NewGame(PClass.Fighter);
        g.Level.Things.RemoveAll(t => t is Monster or Pickup);
        p = g.P;
        p.X = 10.5f; p.Y = 5.5f; p.Angle = 0; p.FloorZ = g.Level.FloorAt(10.5f, 5.5f); p.Pitch = 0;
        g.GiveRocketLauncher();
        Tick(new Input(), 25);
        check(p.CurWeapon.Rocket && p.Weapons.Length == 4 && p.HasWeapon[3], "'give rocketlauncher' puts it on the key after your class's three");
        Monster Put(float dx, float dy)
        {
            var m = new Monster(Monster.Slaughtaur) { X = p.X + dx, Y = p.Y + dy, Level = g.Level, State = AiState.Idle };
            m.Health = m.MaxHealth = 5000;
            g.Level.Things.Add(m);
            return m;
        }
        var target = Put(4, 0);
        var near = Put(4.2f, 1.4f);
        var far = Put(4, -3.6f);
        g.Vars.Freeze = true;
        Tick(new Input { Fire = true });
        for (int k = 0; k < 40 && target.Health == 5000; k++) Tick(new Input());
        int direct = 5000 - target.Health, splash = 5000 - near.Health;
        check(direct >= 100 && direct <= 120, $"a direct hit does 100-120 ({direct})");
        check(splash > 30 && splash < 100 && far.Health == 5000, $"the blast catches a monster 1.4 cells off ({splash}) but not one 3.6 off");
        check(near.KnockY > 0.5f && target.KnockX > 0.5f, "and throws them back, away from it");
        g.Vars.Freeze = false;

        // your own rocket at your feet: half the blast's damage to you, and the full push: a rocket jump
        (float rise, int hurt, float speed) RocketJump(Game game, float angle, int frames = 70, float pitch = -70)
        {
            var q = game.P;
            q.Angle = angle; q.Pitch = pitch; q.Cooldown = 0;
            int before = q.Health;
            float top = 0, fast = 0;
            game.Update(new Input { Jump = true, Fire = true }, 1f / 35f);
            for (int k = 0; k < frames; k++)
            {
                game.Update(new Input(), 1f / 35f);
                top = MathF.Max(top, q.FloorZ + q.Z);
                fast = MathF.Max(fast, q.HSpeed);
            }
            return (top, before - q.Health, fast);
        }
        g.Level.Things.RemoveAll(t => t is Monster);
        p.Health = p.MaxHealth = 200;
        var (rise, hurt, _) = RocketJump(g, 0);
        p.Health = p.MaxHealth;
        var (_, _, speed) = RocketJump(g, 0, pitch: -55); // a rocket a little way ahead: more of the push is along
        float hop = g.Vars.JumpPower * g.Vars.Gravity > 0 ? g.Vars.JumpPower * g.Vars.JumpPower / (2 * g.Vars.Gravity) : 0;
        check(rise > hop * 4 && rise > 2f, $"a rocket at your feet as you jump throws you {rise:0.0} cells up (a jump is {hop:0.00})");
        check(hurt >= 35 && hurt <= 60, $"it hurts you for half the blast ({hurt})");
        check(speed > g.RunSpeed * 1.2f && g.RocketJumps == 2, $"and throws you along faster than you run ({speed:0.0} against {g.RunSpeed:0.0})");

        // on the range: up onto the east ledge, which a jump can't reach, and your rockets can't finish you
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.StartPractice(PClass.Mage, ShootingRange.Course);
        p = g.P;
        int rl = Array.FindIndex(p.Weapons, w => w.Rocket);
        p.HasWeapon[rl] = true;
        Tick(new Input { Slot = rl + 1 }); Tick(new Input(), 10);
        p.X = 31.5f; p.Y = 13.5f; p.FloorZ = 0;
        Tick(new Input { Jump = true }, 40);
        check(p.FloorZ == 0, "a jump alone doesn't reach the ledge");
        RocketJump(g, MathF.PI);
        check(p.FloorZ == ShootingRange.LowLedge, $"a rocket jump (facing away, rocket at your feet) lands you up on the ledge ({p.FloorZ:0.00})");
        p.X = 31.5f; p.Y = 4.5f; p.FloorZ = 0; p.Z = 0; p.VX = p.VY = 0;
        RocketJump(g, MathF.PI);
        check(p.FloorZ == ShootingRange.HighLedge, $"and the same up onto the high one, {ShootingRange.HighLedge} cells up ({p.FloorZ:0.00})");
        p.X = 20.5f; p.Y = 20.5f; p.FloorZ = 0; p.Z = 0; p.VX = p.VY = 0;
        p.Health = 10;
        RocketJump(g, 0, 5);
        check(p.Health >= 1 && g.Mode == GameMode.Playing, "on the range your own rockets can't kill you");
        Tick(new Input(), 35 * 6);
        check(p.Health == p.MaxHealth, "and your health comes back");

        // the dummies and the drill
        var dummies = g.Level.Things.OfType<Monster>().Where(m => m.Target != null).ToList();
        check(dummies.Count == ShootingRange.Targets.Length && dummies.Any(m => m.Target.Kind == ShootingRange.Kind.High && g.Level.FloorAt(m.X, m.Y) >= ShootingRange.LowLedge),
            "the field has target dummies, some up on the ledges");
        var mover = dummies.First(m => m.Target.Kind == ShootingRange.Kind.Moving);
        float x0 = mover.X;
        Tick(new Input(), 20);
        check(MathF.Abs(mover.X - x0) > 0.3f, "moving dummies slide along their tracks");
        Tick(new Input { Use = true });
        check(g.Drilling && g.DrillLeft > 59, "Use starts a one-minute drill");
        var still = dummies.First(m => m.Target.Kind == ShootingRange.Kind.Still);
        var high = dummies.First(m => m.Target.Kind == ShootingRange.Kind.High);
        g.DamageMonster(still, 1000, 0); g.DamageMonster(mover, 1000, 0); g.DamageMonster(high, 1000, 0);
        check(g.DrillScore == 450 && g.DrillKills == 3, $"a dummy scores 100, a moving one 150, one up high 200 ({g.DrillScore})");
        Tick(new Input(), 60);
        check(still.Alive && still.Health == still.MaxHealth && still.X == still.Target.HomeX, "a fallen dummy stands back up at its spot");
        g.DrillLeft = 0.01f;
        Tick(new Input());
        check(!g.Drilling && g.LastDrill?.Score == 450 && g.LastDrillPlace == 1 && g.Profile.RangeBest(PClass.Mage) == 450, "when the minute's up, the drill goes on your class's board");
        g.Paused = true; g.Menu.Show(MenuPage.Leaderboard);
        check(g.Menu.BoardRange, "on the range, the leaderboard opens on its board");
        g.Menu.Close(); g.Paused = false;

        // a borrowed rocket launcher isn't saved with the campaign
        string dir = Path.Combine(Path.GetTempPath(), $"hexensharp-range-{Environment.ProcessId}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "save.json");
        g = new Game { FixedSeed = 1, AchievementsOn = false, SavePath = path };
        g.NewGame(PClass.Fighter);
        g.GiveRocketLauncher();
        Tick(new Input(), 10);
        g.SaveNow();
        var back = new Game { FixedSeed = 1, AchievementsOn = false, SavePath = path, Profile = g.Profile };
        check(back.Continue() && back.P.Weapons.Length == 3 && back.P.HasWeapon.Length == 3 && !back.P.CurWeapon.Rocket, "the rocket launcher from the console isn't kept in a save");
        Directory.Delete(dir, true);
    }
}
