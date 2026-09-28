namespace HexenSharp;

/// <summary>Checks on zooming, wall slides and the trick callouts.</summary>
public static partial class Headless
{
    static void TrickChecks(Action<bool, string> check)
    {
        Game g = null;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }

        // zoom: hold it and the view narrows to 30 degrees, the mouse slowing to match; let go and it comes back
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.StartPractice(PClass.Fighter, ShootingRange.Course);
        var p = g.P;
        float wide = g.ViewFov;
        Tick(new Input { ZoomHeld = true }, 20);
        float narrow = g.ViewFov, before = p.Angle;
        Tick(new Input { ZoomHeld = true, LookX = 100 });
        float zoomedTurn = p.Angle - before;
        Tick(new Input(), 20);
        before = p.Angle;
        Tick(new Input { LookX = 100 });
        float plainTurn = p.Angle - before;
        check(wide == g.Vars.Fov && narrow == Game.ZoomFov && g.ViewFov == wide, $"holding zoom narrows the view from {wide:0} to {narrow:0} degrees, and letting go brings it back");
        check(zoomedTurn > 0 && zoomedTurn < plainTurn * 0.5f, $"zoomed in, the mouse turns you less ({zoomedTurn:0.000} against {plainTurn:0.000})");

        // a wall slide: flying from a blast, meeting a wall at an angle carries you on along it
        float Slide(float boost)
        {
            // north-east into the north wall, just short of it, in the air
            p.X = 20.5f; p.Y = 1.6f; p.FloorZ = 0; p.Z = 1.5f; p.VZ = 0; p.Angle = 0;
            p.VX = 3f; p.VY = -6f; p.Boost = boost;
            Tick(new Input(), 3);
            return MathF.Abs(p.VX);
        }
        float slid = Slide(8f), stopped = Slide(0f);
        check(slid > 5f && stopped < 3.5f, $"flying from a blast into a wall at an angle, you slide along it ({slid:0.0} cells a second along the wall, {stopped:0.0} without the blast)");

        // callouts: a rocket into the wall beside you in mid-air is a wall kick; two blasts together a combo jump
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.StartPractice(PClass.Fighter, ShootingRange.Course);
        g.Level.Things.RemoveAll(t => t is Monster);
        p = g.P;
        g.GiveRocketLauncher();
        Tick(new Input(), 20);
        // in the air beside the east wall, facing it: the rocket hits the wall a cell away and kicks you back west
        p.X = 37.5f; p.Y = 22.5f; p.FloorZ = 0; p.Z = 1f; p.VZ = 2f; p.Angle = 0; p.Pitch = 0; p.Cooldown = 0;
        Tick(new Input { Fire = true });
        for (int k = 0; k < 10 && g.LastTrick == Trick.None; k++) Tick(new Input());
        check(g.LastTrick == Trick.WallKick && p.VX < -2f, $"a rocket into the wall beside you in mid-air: {Tricks.Name(g.LastTrick)} (kicked away at {-p.VX:0.0})");
        // a grenade at your feet going off as you rocket jump
        Tick(new Input(), 60);
        p.X = 20.5f; p.Y = 20.5f; p.FloorZ = 0; p.Z = 0; p.VX = p.VY = p.VZ = 0; p.Angle = 0; p.Pitch = -Rockets.LookDown;
        var nade = new Projectile
        {
            Kind = ProjKind.Grenade, FromPlayer = true, Splash = Rockets.SplashRadius, X = p.X - 0.3f, Y = p.Y, Z = 0, Level = g.Level,
            Life = 0.12f, Radius = 0.08f, DmgMin = 100, DmgMax = 120,
        };
        g.Level.Things.Add(nade);
        p.Cooldown = 0;
        Tick(new Input { Jump = true, Fire = true });
        float top = 0;
        for (int k = 0; k < 60; k++) { Tick(new Input()); top = MathF.Max(top, p.Z); }
        check(g.LastTrick == Trick.ComboJump && g.TrickCounts[(int)Trick.ComboJump] == 1, $"a grenade and a rocket going off under you together: {Tricks.Name(g.LastTrick)} ({top:0.0} cells up)");
    }
}
