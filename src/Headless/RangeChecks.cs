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
        check(g.OnRange && p.Weapons.Length == 14 && p.HasWeapon.Count(h => h) == 1 && p.CurWeapon == ClassDef.All[1].Weapons[0],
            "on the shooting range, every weapon in the game is there to take, starting with your class's first in hand");
        var rack = g.Level.Things.OfType<Pickup>().Where(k => k.Kind == PickupKind.Arms).ToList();
        check(rack.Count == 14 && rack.Select(k => k.Variant).Distinct().Count() == 14, "the rack holds all fourteen: three for each class and the five Quake weapons");
        foreach (var pk in rack) { p.X = pk.X; p.Y = pk.Y; p.FloorZ = 0; Tick(new Input()); }
        check(p.HasWeapon.All(h => h) && rack.All(k => !k.Removed), "walking along the rack takes every weapon, and the rack stays full");
        Tick(new Input { Slot = 10 }); Tick(new Input(), 10);
        check(p.CurWeapon.Rocket && g.PracticeSpeed == 1, "0 takes up the rocket launcher");
        Tick(new Input { Slot = 11 }); Tick(new Input(), 30);
        check(p.CurWeapon.Rail, "and - the railgun");
        Tick(new Input { Slot = 10 }); Tick(new Input(), 30);
        Tick(new Input { Slot = 2 }); Tick(new Input(), 10);
        check(p.CurWeapon == ClassDef.All[0].Weapons[1] && g.PracticeSpeed == 1, "on the range 1-3 pick weapons (Timon's Axe on 2), not the game speed");
        check(g.Menu.Items(MenuPage.Pause).Contains("Leaderboard") && !g.Menu.Items(MenuPage.Pause).Contains("Watch demo"), "the range's pause menu has its leaderboard and no demo");

        // steep aim: the view only tilts so far, so the launcher's aim swings on past it
        float proj = 160f / MathF.Tan(g.Vars.Fov * MathF.PI / 360f);
        float deg(float a) => a * 180f / MathF.PI;
        check(MathF.Abs(Rockets.AimAngle(20, proj) - MathF.Atan(20 / proj)) < 1e-5f && MathF.Abs(Rockets.AimAngle(-Rockets.SteepFrom, proj) + MathF.Atan(Rockets.SteepFrom / proj)) < 1e-5f
              && MathF.Abs(deg(Rockets.AimAngle(-Rockets.LookDown, proj)) + 85) < 0.01f,
            $"a rocket goes where the view points, then swings steeper, to {deg(-Rockets.AimAngle(-Rockets.LookDown, proj)):0} degrees down at the bottom of the tilt");

        // with the launcher in hand you can look much further down, to the floor at your feet
        Tick(new Input { Slot = 10 }); Tick(new Input(), 30);
        p.Pitch = 0;
        Tick(new Input { LookY = 5000 });
        float deep = p.Pitch;
        var landing = g.RocketLanding();
        Tick(new Input { Slot = 1 }); Tick(new Input(), 30);
        float after = p.Pitch;
        Tick(new Input { LookY = 5000 });
        check(deep == -Rockets.LookDown && landing is { dist: < 0.6f } && after == -Rockets.NormalPitch && p.Pitch == -Rockets.NormalPitch,
            $"the rocket launcher lets you look down {Rockets.LookDown:0} px (to a landing {landing?.dist:0.00} cells ahead); put it away and the view eases back to {Rockets.NormalPitch:0}");
        p.Pitch = 0;

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
        (float rise, int hurt, float speed) RocketJump(Game game, float angle, int frames = 70, float pitch = -Rockets.LookDown)
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
        var (_, _, speed) = RocketJump(g, 0, pitch: -100); // a rocket a little way ahead: more of the push is along
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

        // the rocket-jump course: only the launcher, every platform out of a jump's reach, and the demo gets round
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.StartPractice(PClass.Fighter, RocketCourse.Course);
        p = g.P;
        var rp = RocketCourse.Platforms;
        check(g.Course.Timed && p.Weapons.Length == 1 && p.CurWeapon.Rocket, "on the rocket-jump course the rocket launcher is your only weapon");
        check(Enumerable.Range(1, rp.Length - 1).All(k => rp[k].floor - rp[k - 1].floor > Level.MaxStep + 0.45f || rp[k].x0 - rp[k - 1].x1 - 1 >= 4),
            "every platform is too high or too far for a jump");
        g.StartDemo();
        int lowest = p.Health;
        for (int k = 0; k < 35 * 60 && g.Demo; k++) { g.Update(new Input(), 1f / 35f); lowest = Math.Min(lowest, p.Health); }
        var (_, silver, _) = RocketCourse.Course.MedalTimes(PClass.Fighter);
        check(!g.Demo && g.DemoTime > 0 && g.DemoTime <= silver && lowest >= 1,
            $"the demo rocket jumps the whole course in {g.DemoTime:0.00}s (silver is {silver:0.0}), and its own rockets never finish it (lowest health {lowest})");

        // the railgun: 100 the instant you fire, to everything in its line, and no further than a wall
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.NewGame(PClass.Mage);
        g.Level.Things.RemoveAll(t => t is Monster or Pickup);
        p = g.P;
        p.X = 10.5f; p.Y = 5.5f; p.Angle = 0; p.FloorZ = g.Level.FloorAt(10.5f, 5.5f); p.Pitch = 0;
        g.GiveExtra(Railgun.Gun);
        Tick(new Input(), 30);
        var line = new[] { Put(2, 0), Put(3.5f, 0.1f), Put(5, -0.1f) };
        var aside = Put(4, 1.5f);
        float wall = 0;
        for (float t = 0; t < 40 && wall == 0; t += 0.05f) if (g.Level.BlocksPoint(p.X + t, p.Y)) wall = t;
        var past = Put(wall + 1.5f, 0);
        g.Vars.Freeze = true;
        Tick(new Input { Fire = true });
        check(p.CurWeapon.Rail && line.All(m => m.Health == 4900), $"a railgun slug does 100 to every monster in its line at once ({string.Join(", ", line.Select(m => 5000 - m.Health))})");
        check(aside.Health == 5000 && past.Health == 5000, "but not to one off to the side, or behind a wall");
        check(g.Level.Things.OfType<Puff>().Count(pf => pf.Sprite(0) == Art.RailSpiral) > 10, "and it leaves a spiral trail");
        int before = line[0].Health;
        Tick(new Input { Fire = true }, 35);
        check(line[0].Health == before, $"it reloads for a second and a half between shots ({before - line[0].Health} in the next second)");
        g.Vars.Freeze = false;

        // the grenade launcher: an arc, bounces, a fuse, a monster sets it off, and grenade jumps
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.StartPractice(PClass.Cleric, ShootingRange.Course);
        g.Level.Things.RemoveAll(t => t is Monster);
        p = g.P;
        int gl = Array.FindIndex(p.Weapons, w => w.Grenade);
        p.HasWeapon[gl] = true;
        Tick(new Input { Slot = gl + 1 }); Tick(new Input(), 30);
        check(p.CurWeapon.Grenade && ShootingRange.KeyFor(gl) == "=", "= takes up the grenade launcher on the range");
        p.Pitch = 0;
        Tick(new Input { LookY = -5000 });
        float up = p.Pitch;
        Tick(new Input { LookY = 5000 });
        check(up == Rockets.LookDown && p.Pitch == -Rockets.LookDown, $"with the grenade launcher you can look {Rockets.LookDown:0} px up (to lob) as well as down");
        p.X = 20.5f; p.Y = 22.5f; p.Angle = -MathF.PI / 2; p.Pitch = 0; p.Cooldown = 0;
        var marker = g.RocketLanding();
        Tick(new Input { Fire = true });
        var nade = g.Level.Things.OfType<Projectile>().Single(t => t.Kind == ProjKind.Grenade);
        float peak = 0, firstDown = 0; int bounces = 0; float lastVz = nade.VZ; bool rested = false;
        for (int k = 0; k < 35 * 3 && !nade.Removed; k++)
        {
            Tick(new Input());
            peak = MathF.Max(peak, nade.Z);
            if (lastVz < 0 && nade.VZ > 0) { bounces++; if (firstDown == 0) firstDown = 22.5f - nade.Y; }
            lastVz = nade.VZ;
            if (!nade.Removed && nade.VZ == 0 && MathF.Abs(nade.VX) + MathF.Abs(nade.VY) < 0.01f) rested = true;
        }
        check(peak > 0.5f && bounces >= 1 && rested, $"a grenade arcs ({peak:0.00} high), bounces ({bounces}) and comes to rest");
        check(marker is { } mk && MathF.Abs(mk.dist - firstDown) < 0.6f, $"the ring marks where it first comes down ({marker?.dist:0.0} cells, it came down at {firstDown:0.0})");
        check(nade.Removed && g.Level.Things.OfType<Puff>().Any(), "and goes off after its fuse");
        var victim = new Monster(Monster.Slaughtaur) { X = 20.5f, Y = 19.5f, Level = g.Level, State = AiState.Idle };
        victim.Health = victim.MaxHealth = 5000;
        g.Level.Things.Add(victim);
        g.Vars.Freeze = true;
        p.X = 20.5f; p.Y = 22.5f; p.Angle = -MathF.PI / 2; p.Pitch = 0; p.Cooldown = 0;
        Tick(new Input { Fire = true });
        int ticks = 0;
        while (victim.Health == 5000 && ticks < 35 * 3) { Tick(new Input()); ticks++; }
        check(victim.Health < 5000 && ticks < 35, $"one that touches a monster goes off at once ({5000 - victim.Health} after {ticks / 35f:0.00}s)");
        g.Vars.Freeze = false;
        g.Level.Things.Remove(victim);
        // a grenade jump: a grenade dropped at your feet, stand just past it and jump as it goes off
        p.X = 20.5f; p.Y = 22.5f; p.Angle = MathF.PI; p.Pitch = -Rockets.LookDown; p.Cooldown = 0; p.Health = p.MaxHealth;
        Tick(new Input { Fire = true });
        nade = g.Level.Things.OfType<Projectile>().Last(t => t.Kind == ProjKind.Grenade);
        check(MathF.Abs(nade.X - p.X) < 0.6f, "looking right down, a grenade drops at your feet");
        while (!nade.Removed && nade.Life > 0.06f) { p.X = nade.X + 0.45f; p.Y = nade.Y; Tick(new Input()); }
        float top = 0;
        Tick(new Input { Jump = true });
        for (int k = 0; k < 60; k++) { Tick(new Input()); top = MathF.Max(top, p.FloorZ + p.Z); }
        check(top > 2f, $"stand just past it and jump as it goes off: a grenade jump, {top:0.0} cells up");

        // the grenade course: every target down, then up to the exit; the demo gets round
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.StartPractice(PClass.Fighter, GrenadeCourse.Course);
        p = g.P;
        check(g.Course.Timed && p.Weapons.Length == 1 && p.CurWeapon.Grenade && g.CourseTargetsLeft == 3,
            "on the grenade course the grenade launcher is your only weapon, and there are three targets to knock down");
        var exitAt = g.Level.FindMark('E')!.Value;
        foreach (var k in Enumerable.Range(0, g.Level.Checkpoints.Count)) g.Level.CheckpointsReached.Add(k);
        g.Messages.Clear();
        p.X = exitAt.x; p.Y = exitAt.y; p.FloorZ = GrenadeCourse.Ledge2;
        Tick(new Input());
        check(g.Messages.Any(m => m.text.Contains("every target")) && !g.Messages.Any(m => m.text.Contains("made it")), "the exit stays shut while a target stands");
        g.StartDemo();
        int low = p.Health;
        for (int k = 0; k < 35 * 60 && g.Demo; k++) { g.Update(new Input(), 1f / 35f); low = Math.Min(low, p.Health); }
        var (_, gSilver, _) = GrenadeCourse.Course.MedalTimes(PClass.Fighter);
        check(!g.Demo && g.DemoTime > 0 && g.DemoTime <= gSilver && low >= 1,
            $"the demo lobs down all three and grenade jumps up both ledges in {g.DemoTime:0.00}s (silver is {gSilver:0.0}; lowest health {low})");
        check(g.CourseTargetsLeft == 3, "and the targets stand back up for your go");

    }
}
