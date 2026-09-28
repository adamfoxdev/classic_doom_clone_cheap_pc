namespace HexenSharp;

/// <summary>
/// The rocket-jump course (Practice > Rocket Jump): towers of platforms too high to jump to and gaps too wide to hop,
/// crossed with the rocket launcher, the only weapon here. Rocket jump up and along from each one to the next: run
/// backwards to the edge, looking down, then jump and fire at your feet. Your rockets can't kill you, your health
/// comes back and mana never runs out; a fall lands you on a lift pad back to your last platform. Timed, with medals,
/// a board, a ghost and a demo, like the other courses.
/// </summary>
public static class RocketCourse
{
    /// <summary>The platforms, each a rocket jump from the last: up two cells, up again, a long drop across, then the two biggest climbs.</summary>
    public static readonly (int x0, int x1, float floor)[] Platforms =
        { (1, 12, 0.5f), (16, 21, 2.5f), (26, 31, 4.5f), (37, 42, 3f), (47, 51, 5.5f), (57, 64, 7f) };

    public const string About = "TOWERS TOO HIGH TO JUMP, WITH ONLY A ROCKET LAUNCHER. ROCKET JUMP FROM EACH PLATFORM TO THE NEXT.";
    const string Intro = "Rocket Jump: back up to the edge looking down (mouse right down), then jump and fire at your feet.";

    public static readonly Course Course = new("rocketjump", "Rocket Jump", About,
        () => Maps.GapCourse("Rocket Jump", "Rocket Jump. Every platform is out of reach: rocket jump to it.", "spire", Platforms, 10f),
        true, Intro, Platforms, Rockets: true, Route: 90); // the demo earns silver; gold wants a cleaner run

    /// <summary>What each platform says about the next: how far up (or down) and across it is.</summary>
    public static string Hint(int zone)
    {
        var (a, b) = (Platforms[zone], Platforms[zone + 1]);
        int gap = b.x0 - a.x1 - 1;
        float rise = b.floor - a.floor;
        string up = rise > 0 ? $"{rise:0.#} up" : $"{-rise:0.#} down";
        string how = rise >= 2.5f ? " Jump as you fire, for the extra height."
            : rise < 0 ? " It's a drop: look less far down, so the blast throws you further along."
            : "";
        return $"Platform {zone + 1}. The next is {up} and {gap} across.{how}";
    }
}

public sealed partial class Game
{
    /// <summary>On a fresh rocket-jump course: the rocket launcher, and nothing else, in hand.</summary>
    void SetUpRocketCourse()
    {
        var p = P;
        p.Loadout = new[] { Rockets.Launcher };
        p.HasWeapon = new[] { true };
        p.Weapon = 0; p.PendingWeapon = -1; p.Raise = 0;
        p.BlueMana = p.GreenMana = 200;
        _unhurt = 0; _regen = 0;
    }
}

/// <summary>
/// The rocket-jump course's demo pilot. On each platform it backs up to the edge facing the way it came, looking right
/// down, then jumps and fires at its feet at the edge, and steers in the air (holding Back to carry on, W to brake)
/// to come down on the middle of the next platform.
/// </summary>
static class RocketJumper
{
    public static Input Next(Game g, DemoPilot pilot)
    {
        var p = g.P;
        var plats = RocketCourse.Platforms;
        if (p.OnGround)
        {
            int on = Array.FindIndex(plats, pl => p.X >= pl.x0 && p.X <= pl.x1 + 1 && MathF.Abs(p.FloorZ - pl.floor) < 0.01f);
            if (on >= 0) pilot.RocketFrom = on;
        }
        int k = pilot.RocketFrom;
        var here = plats[k];
        float look = Wrap(MathF.PI - p.Angle) / (0.0025f * g.Vars.Sens); // face back down the course
        var inp = new Input { LookX = look, LookY = (Rockets.LookDown + p.Pitch) / (0.35f * g.Vars.Sens) + 1 };
        if (k >= plats.Length - 1)
        {
            // the last platform: on to the exit (facing back, so backwards)
            var exit = g.Level.FindMark('E') ?? (here.x1, 5.5f);
            pilot.Say("EXIT: back into the exit to finish");
            inp.Move = -1;
            inp.Strafe = Math.Sign(exit.y - p.Y) * (MathF.Abs(exit.y - p.Y) > 0.3f ? -1 : 0);
            return inp;
        }
        var next = plats[k + 1];
        float target = next.x0 + 2f;
        if (!p.OnGround)
        {
            // in the air: carry on to the next platform's middle, then brake
            bool over = p.X > target - 0.5f;
            pilot.Say(over ? "BRAKE: over the platform, hold W to stop short of its far edge" : "FLY: hold S to carry on through the air");
            inp.Move = over ? 1 : -1;
            return inp;
        }
        float edge = here.x1 + 1 - 0.35f;
        // steer back to the middle of the course (y 5.5) as you go
        inp.Strafe = MathF.Abs(p.Y - 5.5f) > 0.4f ? Math.Sign(p.Y - 5.5f) : 0;
        if (p.X < edge)
        {
            pilot.Say("BACK UP: face the way you came, look right down, and back up to the edge");
            inp.Move = -1;
            return inp;
        }
        pilot.Say("JUMP AND FIRE: at the edge, jump and fire at your feet");
        inp.Move = -1; inp.Jump = true; inp.Fire = p.Cooldown <= 0 && p.Pitch <= -Rockets.LookDown + 1;
        return inp;
    }

    static float Wrap(float a) => MathF.IEEERemainder(a, MathF.Tau);
}
