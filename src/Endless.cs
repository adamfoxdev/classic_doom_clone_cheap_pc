namespace HexenSharp;

/// <summary>
/// The endless practice course: a run of platforms over gaps, built from a seed, that gets harder the further you go.
/// The first few gaps are short and level; then the gaps widen, the platforms shrink and the floor starts rising and
/// dropping, until the last stretch is beyond almost anyone. There's no clock to beat: your score is how many platforms
/// you reach before you fall, and a fall ends the run (it starts again from the top, on the same seed).
/// Each pick from the Practice menu rolls a new seed; `endless &lt;seed&gt;` in the console replays one.
/// </summary>
public static class Endless
{
    public const int Count = 50;
    public const float TopFloor = 6.5f, LowFloor = 1f, Height = 9f;
    static readonly string[] Themes = { "spire", "crypt", "hall", "meadow" };

    public const string About = "PLATFORMS OVER GAPS FROM A SEED, HARDER AS YOU GO. ONE FALL ENDS THE RUN: HOW FAR CAN YOU GET?";
    const string Intro = "Endless: how far can you get? The gaps widen as you go, and one fall ends the run.";

    /// <summary>The Practice menu's entry: picking it rolls a fresh seed.</summary>
    public static readonly Course Pick = new("endless", "Endless", About, () => Map(1), false, Intro, Plan(1), Endless: true);

    public static int NewSeed() => Random.Shared.Next(1, 1_000_000);

    /// <summary>The course for one seed.</summary>
    public static Course For(int seed) => Pick with { Map = () => Map(seed), Platforms = Plan(seed), Seed = seed };

    /// <summary>
    /// The platforms (x0, x1, floor) for a seed. Step k of the way (t from 0 to 1): gaps of about 2 + 6t cells, shorter
    /// platforms (12 cells down to 3), and from the fifth platform on, floors that drop (more, later) or rise a step. A
    /// drop buys hang time, so its gap is a cell wider; a rise costs some, so its gap is narrower.
    /// </summary>
    public static (int x0, int x1, float floor)[] Plan(int seed)
    {
        var rng = new Random(seed);
        var plats = new List<(int x0, int x1, float floor)> { (1, 14, 5f) };
        float floor = 5f;
        for (int k = 1; k < Count; k++)
        {
            float t = k / (float)(Count - 1);
            float was = floor;
            if (k >= 4)
            {
                int r = rng.Next(10);
                floor += r < 4 ? -Level.FloorStep * (1 + rng.Next(1 + (int)(t * 4))) : r < 7 ? 0 : Level.FloorStep;
                // near the bottom, climb back; near the top, drop
                if (floor < LowFloor) floor = was + Level.FloorStep;
                if (floor > TopFloor) floor = was - Level.FloorStep * 2;
            }
            float dz = floor - was;
            float g = 2 + 6f * t + (float)rng.NextDouble() * 1.5f - 0.5f - dz * 2;
            int gap = Math.Clamp((int)MathF.Round(g), 2, 9);
            int len = k == Count - 1 ? 6 : Math.Clamp(12 - (int)(9 * t) + rng.Next(-1, 3), 3, 12);
            int x0 = plats[^1].x1 + 1 + gap;
            plats.Add((x0, x0 + len - 1, floor));
        }
        return plats.ToArray();
    }

    public static MapDef Map(int seed)
    {
        var theme = Themes[(int)((uint)seed % Themes.Length)];
        return Maps.GapCourse($"Endless {seed}", $"Endless, seed {seed}. How far can you get?", theme, Plan(seed), Height);
    }
}

/// <summary>One endless run: how many platforms it reached, on which seed, and how long it took to get there.</summary>
public sealed class EndlessRun
{
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public int Platforms { get; set; }
    public int Seed { get; set; }
    public float Time { get; set; }
    public DateTime When { get; set; }
}

public sealed partial class Game
{
    /// <summary>On the endless course: the furthest platform this run has reached (0 is the start).</summary>
    public int EndlessReached;
    /// <summary>The last endless run that ended, and its place on your class's board (0 if off it).</summary>
    public EndlessRun LastEndless;
    public int LastEndlessPlace;

    public bool OnEndless => Practicing && Course.Endless;

    /// <summary>Starts the endless course on a seed (a fresh one unless you say).</summary>
    public void StartEndless(PClass cls, int seed = 0) => StartPractice(cls, Endless.For(seed > 0 ? seed : Endless.NewSeed()));

    /// <summary>A new platform lit on the endless course.</summary>
    void EndlessPlatform(int zone)
    {
        EndlessReached = Math.Max(EndlessReached, zone);
        // the run starts when you land on the second platform (the first is where you wind up)
        if (zone > 0) RunStarted = true;
    }

    /// <summary>
    /// The run's over: you fell, or (rarely) reached the exit. Records it on your class's board if it got anywhere, and
    /// starts again from the top on the same seed.
    /// </summary>
    void EndEndlessRun(bool finished)
    {
        int reached = finished ? Course.Platforms.Length - 1 : EndlessReached; // every platform past the first
        Messages.Clear();
        if (Demo || PracticeSpeed < 1 || reached == 0)
        {
            Say(reached == 0 ? "Fell at the first gap. Build speed on the first platform, then go."
                : $"Reached platform {reached} at {PracticeSpeed * 100:0}% speed. Press 1 for full speed to set a record.");
        }
        else
        {
            var run = new EndlessRun { Name = RunnerName, Class = P.Class.ToString(), Platforms = reached, Seed = Course.Seed, Time = RunTime, When = DateTime.Now };
            int best = Profile.EndlessBest(P.Class);
            int place = Profile.AddEndlessRun(run);
            if (place > 0) SaveProfile();
            LastEndless = run; LastEndlessPlace = place;
            PlaySound(place == 1 ? Sfx.Secret : Sfx.Teleport, 1);
            string how = finished ? $"You made it to the end! All {reached} platforms" : $"Fell after platform {reached}";
            Say(place == 1 ? $"{how} - a new best!" : place > 0 ? $"{how} - #{place} on the board (best {best})." : $"{how} (best {best}).");
        }
        Level.CheckpointsReached.Clear();
        Checkpoint = null;
        MoveTo(Level.StartX, Level.StartY, Course.StartAngle);
        ResetRun();
    }
}
