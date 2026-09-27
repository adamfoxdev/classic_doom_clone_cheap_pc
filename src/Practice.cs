namespace HexenSharp;

/// <summary>
/// A practice course (Main menu > Practice). Timed courses have a clock, an exit that ends the run once every checkpoint
/// is reached, a leaderboard and a ghost for each class; the free-roam one is just space to move in. Gap courses list
/// their platforms, which the hints use to say how fast you'll need to be for each gap.
/// </summary>
public sealed record Course(string Id, string Name, string About, Func<MapDef> Map, bool Timed, string Intro,
    (int x0, int x1, float floor)[] Platforms = null, float StartAngle = 0, bool Jetpack = false)
{
    /// <summary>The leaderboard and ghost key for a class (the Velocity Hangar's are just the class, as they were first).</summary>
    public string Key(PClass cls) => Id == "hangar" ? cls.ToString() : $"{Id}/{cls}";
}

public static class Courses
{
    const string StrafeIntro = "Strafe jumping: jump, then hold A or D and turn the mouse the same way. Hop again the moment you land.";

    public static readonly Course Hangar = new("hangar", "Velocity Hangar",
        "A STRAIGHT RUN OF PLATFORMS OVER GAPS THAT WIDEN FROM 2 CELLS TO 6.", Maps.VelocityCourse, true, StrafeIntro, Maps.CoursePlatforms);

    /// <summary>Descent's platforms, each lower than the last: the drop buys hang time for the wider gaps (3 to 7).</summary>
    public static readonly (int x0, int x1, float floor)[] DescentPlatforms =
        { (1, 14, 6f), (18, 27, 5.5f), (32, 41, 4.75f), (47, 58, 3.75f), (65, 76, 2.5f), (84, 91, 1f) };

    public static readonly Course Descent = new("descent", "Descent",
        "A STAIRWAY OF PLATFORMS DROPPING AWAY. EACH FALL BUYS HANG TIME FOR A WIDER GAP.",
        () => Maps.GapCourse("Descent", "Descent. Each platform drops lower, and each gap is wider: use the fall.", "crypt", DescentPlatforms, 9f),
        true, "Descent: every drop gives you more hang time. Build speed on the top platform, then keep hopping.", DescentPlatforms);

    public static readonly Course Circuit = new("circuit", "Circuit",
        "A LAP OF A LOOPED TRACK. KEEP YOUR SPEED THROUGH THE CORNERS BY STRAFING INTO THEM.",
        CircuitMap, true, "Circuit: one lap, clockwise. Strafe into each corner to carry your speed round it.", StartAngle: MathF.PI);

    public static readonly Course FreeRoam = new("free", "Free Roam",
        "A WIDE OPEN FIELD WITH NOTHING IN IT: NO CLOCK, NO EXIT. PRACTISE HOWEVER YOU LIKE.",
        FreeRoamMap, false, "Free Roam: all the room you want, and a jetpack (Q). Nothing to finish; Esc when you're done.", Jetpack: true);

    public static readonly Course[] All = { Hangar, Descent, Circuit, FreeRoam };
    public static Course[] Timed => All.Where(c => c.Timed).ToArray();

    /// <summary>
    /// Circuit: a rectangular loop, 8 wide, round a walled island. The four sides are its checkpoint zones (the long
    /// straights sit a quarter step up, so each side is a zone of its own); the finish line is across the south
    /// straight, just behind the start, so you cross it at the end of the lap.
    /// </summary>
    static MapDef CircuitMap()
    {
        const int w = 46, h = 30;
        var rows = new char[h][];
        for (int y = 0; y < h; y++)
        {
            rows[y] = new char[w];
            for (int x = 0; x < w; x++)
                rows[y][x] = y == 0 || y == h - 1 || x == 0 || x == w - 1 || (x >= 9 && x <= 36 && y >= 9 && y <= 20) ? 'O' : '.';
        }
        foreach (var (x, y) in new[] { (1, 1), (44, 1), (1, 28), (44, 28), (22, 1), (22, 28), (1, 14), (44, 14) }) rows[y][x] = 't';
        for (int y = 21; y <= 28; y++) rows[y][35] = 'E';
        rows[24][32] = '@';
        foreach (var (x, y) in new[] { (4, 14), (22, 4), (40, 14), (30, 26) }) rows[y][x] = '+';
        var def = new MapDef("Circuit", "Circuit. One lap round the track, through every checkpoint, back to the line.", "hall",
            rows.Select(r => new string(r)).ToArray(), Height: 3f);
        return Maps.Elevate(def, (9, 1, 36, 8, '1'), (9, 21, 36, 28, '1'));
    }

    /// <summary>Free Roam: a big open field under the sky, walled far off, with nothing in it but you.</summary>
    static MapDef FreeRoamMap()
    {
        const int n = 64;
        var rows = Enumerable.Range(0, n).Select(y => new string(Enumerable.Range(0, n).Select(x =>
            x == 0 || y == 0 || x == n - 1 || y == n - 1 ? '#' : x == n / 2 && y == n / 2 ? '@' : ',').ToArray())).ToArray();
        return new MapDef("Free Roam", "Free Roam. Nothing here but room to move.", "meadow", rows, Height: 3f);
    }
}
