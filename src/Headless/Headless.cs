namespace HexenSharp;

/// <summary>Windowless tools: the self-test runner and sound export (the checks themselves live in the other Headless/*.cs files).</summary>
public static partial class Headless
{
    /// <summary>Checks every map loads and that the critical path is reachable.</summary>
    public static int SelfTest()
    {
        var hub = Maps.BuildHub();
        int failures = 0;
        void Check(bool ok, string what)
        {
            Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
            if (!ok) failures++;
        }

        foreach (var lv in hub)
        {
            Console.WriteLine($"{lv.Name}: {lv.W}x{lv.H}, {lv.Things.Count} things");
            // player start (or arrival portal) reaches every portal/exit/key when doors, gates and key doors are passable
            var arrival = Enumerable.Range(0, lv.Marks.Length).First(i => char.IsDigit(lv.Marks[i]));
            var start = lv == hub[0] ? ((int)lv.StartX, (int)lv.StartY) : (arrival % lv.W, arrival / lv.W);
            var reach = lv.Reachable(start.Item1, start.Item2);
            var flyTo = lv.Reachable(start.Item1, start.Item2, move: Level.Move.Fly);
            for (int i = 0; i < lv.Marks.Length; i++)
                if (lv.Marks[i] != '\0') Check(lv.Marks[i] is '+' or '^' ? flyTo[i] : reach[i], $"mark '{lv.Marks[i]}' at {i % lv.W},{i / lv.W} reachable"); // pads and plates may be up a jetpack flight
            foreach (var t in lv.Things)
            {
                if (t is Pickup pk && pk.Kind is PickupKind.SteelKey or PickupKind.FireKey or PickupKind.Weapon2 or PickupKind.Weapon3)
                    Check(reach[(int)t.Y * lv.W + (int)t.X], $"{pk.Kind} reachable");
                if (t is Monster m && m.Def.Boss) Check(reach[(int)t.Y * lv.W + (int)t.X], "boss reachable");
                if (t is not Monster { Burrowed: true }) // the Rock Wyrm lives in the rock
                    Check(!lv.BlocksPoint(t.X, t.Y), $"{t.GetType().Name} at {t.X - 0.5f},{t.Y - 0.5f} not inside a wall");
            }
            // levers must be touchable from an open cell (the Windspire's needs the jetpack to get to)
            var flyReach = lv.Reachable(start.Item1, start.Item2, move: Level.Move.Fly);
            for (int i = 0; i < lv.Cells.Length; i++)
                if (lv.Cells[i] == 'L')
                {
                    int x = i % lv.W, y = i / lv.W;
                    bool ok = (x > 0 && flyReach[i - 1]) || (x < lv.W - 1 && flyReach[i + 1]) || (y > 0 && flyReach[i - lv.W]) || (y < lv.H - 1 && flyReach[i + lv.W]);
                    Check(ok, $"lever at {x},{y} usable");
                }
        }
        Console.WriteLine("Gameplay:");
        GameplayChecks(Check);

        // every portal digit must exist in exactly two maps
        foreach (char d in hub.SelectMany(l => l.Marks).Where(char.IsDigit).Distinct())
            Check(hub.Count(l => l.FindMark(d) != null) == 2, $"portal {d} links exactly two maps");

        Console.WriteLine("Hub progression:");
        HubChecks(Check);
        Console.WriteLine("Jumping and sliding:");
        MovementChecks(Check);
        Console.WriteLine("Console and cheats:");
        ConsoleChecks(Check);
        Console.WriteLine("Chaos Arena waves:");
        ArenaChecks(Check);
        Console.WriteLine("Arcade mode:");
        ArcadeChecks(Check);
        Console.WriteLine("Story mode:");
        StoryChecks(Check);
        Console.WriteLine("Arena mode:");
        ArenaModeChecks(Check);
        Console.WriteLine("Save and continue:");
        SaveChecks(Check);
        Console.WriteLine("Custom map features:");
        MapFeatureChecks(Check);
        Console.WriteLine("Replays:");
        ReplayChecks(Check);
        Console.WriteLine("Benchmark comparison:");
        BenchChecks(Check);
        Console.WriteLine("Elites:");
        EliteChecks(Check);
        Console.WriteLine("Ghost codes:");
        GhostCodeChecks(Check);
        Console.WriteLine("New Game+ director:");
        DirectorChecks(Check);
        Console.WriteLine("New Game+ hazards:");
        HazardChecks(Check);
        Console.WriteLine("Mini-boss rematches:");
        RematchChecks(Check);
        Console.WriteLine("Monster codex:");
        CodexChecks(Check);
        Console.WriteLine("Weapon mods:");
        ModChecks(Check);
        Console.WriteLine("Rocket launcher and shooting range:");
        RangeChecks(Check);
        Console.WriteLine("Quake weapons in the campaign:");
        QuakeArmsChecks(Check);
        Console.WriteLine("Zoom and movement tricks:");
        TrickChecks(Check);
        Console.WriteLine("Game feel:");
        FeelChecks(Check);
        Console.WriteLine("Endless course:");
        EndlessChecks(Check);
        Console.WriteLine("New Game+:");
        NgPlusChecks(Check);
        Console.WriteLine("Achievements:");
        AchievementChecks(Check);
        Console.WriteLine("Music:");
        MusicChecks(Check);
        Console.WriteLine("Renderer:");
        RenderChecks(Check);
        Console.WriteLine("Gamepad:");
        GamepadChecks(Check);
        Console.WriteLine("Difficulty:");
        DifficultyChecks(Check);
        Console.WriteLine("Daily challenge:");
        DailyChecks(Check);
        Console.WriteLine("Arena perks and modifiers:");
        ArenaPerkChecks(Check);

        Console.WriteLine("Dark Bishop:");
        BishopChecks(Check);
        Console.WriteLine("Chests:");
        ChestChecks(Check);

        Console.WriteLine("Options and key bindings:");
        OptionsChecks(Check);

        Console.WriteLine("Relaxed mode and discovery:");
        RelaxedChecks(Check);

        Console.WriteLine("Custom maps:");
        CustomMapChecks(Check);

        Console.WriteLine("Ceiling heights:");
        HeightChecks(Check);

        Console.WriteLine("Stairs and floors:");
        StairChecks(Check);

        Console.WriteLine("Visual styles:");
        StyleChecks(Check);

        Console.WriteLine("Jetpack:");
        JetpackChecks(Check);
        Console.WriteLine("Windspire:");
        SpireChecks(Check);
        Console.WriteLine("Hanging Cisterns:");
        CisternChecks(Check);
        Console.WriteLine("Mini-bosses:");
        MiniBossChecks(Check);
        Console.WriteLine("Deepdelve Quarry and rubble:");
        QuarryChecks(Check);
        VerticalAimChecks(Check);
        Console.WriteLine("Checkpoints:");
        CheckpointChecks(Check);
        Console.WriteLine("Character progression:");
        RpgChecks(Check);

        Console.WriteLine("Quake movement:");
        QuakeMoveChecks(Check);
        Console.WriteLine("Strafe-jumping practice:");
        PracticeChecks(Check);
        Console.WriteLine("Practice ghost:");
        GhostChecks(Check);
        Console.WriteLine("More practice courses:");
        CourseChecks(Check);
        Console.WriteLine("Medals:");
        MedalChecks(Check);
        Console.WriteLine("Practice demo:");
        DemoChecks(Check);
        Console.WriteLine("Strafe helper:");
        StrafeHelperChecks(Check);
        Console.WriteLine("HUD styles:");
        HudChecks(Check);
        Console.WriteLine("Rendered art pack:");
        RenderedArtChecks(Check);
        Console.WriteLine("Map files and the HTML editor:");
        MapFileChecks(Check);

        Console.WriteLine("Audio synthesis:");
        SoundChecks(Check);

        Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>Drives the game with scripted input and writes PNGs (3x upscaled) to a folder.</summary>
    /// <summary>Writes every sound effect in both styles as WAV files, for listening outside the game.</summary>
    public static int ExportSounds(string dir)
    {
        foreach (var style in new[] { ArtStyle.SciFi, ArtStyle.Fantasy })
        {
            var sub = Path.Combine(dir, style == ArtStyle.SciFi ? "scifi" : "fantasy");
            Directory.CreateDirectory(sub);
            for (int i = 0; i < (int)Sfx.Count; i++)
                File.WriteAllBytes(Path.Combine(sub, ((Sfx)i).ToString().ToLowerInvariant() + ".wav"), Sounds.Wav(Sounds.Make((Sfx)i, style)));
            foreach (var track in MusicGen.Tracks)
                File.WriteAllBytes(Path.Combine(sub, "music_" + track + ".wav"), Sounds.Wav(MusicGen.Make(track, style)));
        }
        Console.WriteLine($"wrote {(int)Sfx.Count * 2} sounds and {MusicGen.Tracks.Count() * 2} music loops to {dir}");
        return 0;
    }
}
