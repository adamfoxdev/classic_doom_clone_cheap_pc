namespace HexenSharp;

/// <summary>Someone you can question on a case.</summary>
public sealed record Suspect(string Name, string Role, int Look, float X, float Y, string Greeting, string Alibi,
    Dictionary<string, string> OnClue, bool Culprit)
{
    /// <summary>A statement starting with '!' is a lie that the evidence gives away.</summary>
    public string About(string clue) => OnClue.TryGetValue(clue, out var s) ? s : "Never seen it. Can't help you there.";
}

/// <summary>A piece of evidence on a case: where it is, what you see when you examine it, and the note it leaves in your journal.</summary>
public sealed record Clue(string Id, string Name, float X, float Y, string Text, string Note);

/// <summary>Two clues that, found together, reveal a pattern.</summary>
public sealed record Insight(string A, string B, string Text);

/// <summary>A job: the brief, the map, who to question, what to find, and what it takes to make an accusation stick.</summary>
public sealed record CaseFile(string Title, string Brief, MapDef Map, Suspect[] Suspects, Clue[] Clues, Insight[] Insights,
    string[] Keys, string Solved);

/// <summary>A suspect standing in the level.</summary>
public sealed class Npc : Thing
{
    public readonly Suspect S;
    public bool Met;
    public Npc(Suspect s) { S = s; X = s.X; Y = s.Y; Solid = true; Radius = 0.3f; SpriteW = 0.55f; SpriteH = 1.0f; }
    public override Tex Sprite(float time) => Art.People[S.Look % Art.People.Length];
}

/// <summary>A clue lying in the level: a glowing evidence marker until you've examined it.</summary>
public sealed class ClueMark : Thing
{
    public readonly Clue C;
    public bool Found;
    public ClueMark(Clue c) { C = c; X = c.X; Y = c.Y; SpriteW = SpriteH = 0.35f; FullBright = true; }
    public override Tex Sprite(float time) => Found ? Art.ClueFound : Art.ClueMark[(int)(time * 3) % Art.ClueMark.Length];
}

/// <summary>How the current case is going: what you've found and heard, who you're talking to, and your strikes.</summary>
public sealed class StoryState
{
    public readonly CaseFile Case;
    public readonly HashSet<string> Found = new(), Heard = new(), Contradictions = new(), Insights = new();
    /// <summary>The journal, in the order you learnt things; flagged entries are lies and patterns.</summary>
    public readonly List<(string text, bool flag)> Journal = new();
    public int Strikes;
    public bool Solved;
    public Npc Talking;
    public string Line;
    public int Cursor;
    public bool JournalOpen;
    public StoryState(CaseFile c) => Case = c;

    public const int MaxStrikes = 3;
    public bool HasKeys => Case.Keys.All(Found.Contains);
}

/// <summary>
/// Story mode: you're a private eye taking jobs around Neon Harbor. On each case, question the suspects (their alibis,
/// and what they say about each clue you've found), examine the evidence, watch for patterns, and accuse the culprit
/// once you can prove it. Three wrong accusations and the case goes cold.
/// </summary>
public static class Story
{
    public const string Town = "Neon Harbor";

    /// <summary>What you can say to whoever you're talking to: alibi, each clue you've found, accuse, leave.</summary>
    public static List<(string label, string key)> Options(StoryState s)
    {
        var o = new List<(string, string)> { ("Where were you last night?", "alibi") };
        foreach (var c in s.Case.Clues.Where(c => s.Found.Contains(c.Id))) o.Add(($"About the {c.Name.ToLowerInvariant()}...", "clue:" + c.Id));
        o.Add(("I'm accusing you.", "accuse"));
        o.Add(("That's all for now.", "bye"));
        return o;
    }

    public static void Talk(Game g, Npc n)
    {
        var s = g.Story;
        s.Talking = n;
        s.Cursor = 0;
        s.Line = n.Met ? "Back again, detective?" : n.S.Greeting;
        if (!n.Met) Note(s, $"{n.S.Name}, {n.S.Role.ToLowerInvariant()}.", false);
        n.Met = true;
        g.PlaySound(Sfx.Lore, 0.6f);
    }

    public static void Choose(Game g, string key)
    {
        var s = g.Story;
        var sus = s.Talking.S;
        if (key == "bye") { s.Talking = null; if (s.Solved) g.CaseSolved(); return; }
        if (key == "alibi")
        {
            s.Line = sus.Alibi;
            if (s.Heard.Add(sus.Name + "|alibi")) Note(s, $"{sus.Name}: \"{sus.Alibi}\"", false);
            return;
        }
        if (key.StartsWith("clue:"))
        {
            string id = key[5..];
            var clue = s.Case.Clues.First(c => c.Id == id);
            string said = sus.About(id);
            bool lie = said.StartsWith('!');
            s.Line = lie ? said[1..] : said;
            if (s.Heard.Add(sus.Name + "|" + id)) Note(s, $"{sus.Name} on the {clue.Name.ToLowerInvariant()}: \"{s.Line}\"", false);
            if (lie && s.Contradictions.Add(sus.Name + "|" + id))
            {
                Note(s, $"{sus.Name}'s story doesn't add up: the {clue.Name.ToLowerInvariant()} says otherwise.", true);
                g.Say($"That doesn't add up. {sus.Name} is hiding something.");
                g.PlaySound(Sfx.Secret, 0.8f);
            }
            return;
        }
        // an accusation
        if (!sus.Culprit)
        {
            s.Strikes++;
            s.Line = "Me? You're way off, detective. Get out of my face.";
            g.PlaySound(Sfx.Locked, 1);
            if (s.Strikes >= StoryState.MaxStrikes) { s.Talking = null; g.CaseGoesCold(); return; }
            g.Say($"Wrong call. Strike {s.Strikes} of {StoryState.MaxStrikes}.");
            return;
        }
        if (!s.HasKeys)
        {
            s.Line = "You've got nothing on me. Come back when you can prove it.";
            g.Say("Right suspect, maybe, but you can't prove it yet. Find more evidence.");
            g.PlaySound(Sfx.Locked, 0.6f);
            return;
        }
        s.Solved = true;
        s.Line = s.Case.Solved;
        Note(s, $"Case closed: {sus.Name} did it.", true);
        g.PlaySound(Sfx.Item, 1);
    }

    public static void Examine(Game g, ClueMark m)
    {
        var s = g.Story;
        bool first = !m.Found;
        m.Found = true;
        g.ReadingLore = $"{m.C.Name}\n\n{m.C.Text}";
        g.PlaySound(first ? Sfx.Secret : Sfx.Lore, 0.8f);
        if (!first) return;
        s.Found.Add(m.C.Id);
        Note(s, $"{m.C.Name}: {m.C.Note}", false);
        g.Say($"Clue found: {m.C.Name} ({s.Found.Count}/{s.Case.Clues.Length}).");
        foreach (var ins in s.Case.Insights)
            if (s.Found.Contains(ins.A) && s.Found.Contains(ins.B) && s.Insights.Add(ins.A + ins.B))
            {
                Note(s, "Pattern: " + ins.Text, true);
                g.Say("You spot a pattern. Check your journal (J).");
            }
    }

    static void Note(StoryState s, string text, bool flag) => s.Journal.Add((text, flag));

    // ------------------------------------------------------------ the cases

    static Dictionary<string, string> D(params (string clue, string line)[] lines) => lines.ToDictionary(l => l.clue, l => l.line);

    public static readonly CaseFile[] Cases =
    {
        new("The Missing Shipment",
            "Dockside. A crate of medical nanites vanished from the Voss warehouse overnight. The owners want it back, quietly. " +
            "Question the foreman, the night guard and the pilot, and look around.",
            new MapDef("Dockside", "Dockside, Neon Harbor.", "town", new[]
            {
                "############################",
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,,@,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,,,,,######D#####,,,,,,,,,#",
                "#,,,,,#..........#,,,####,,#",
                "#,,,,,#..........#,,,#..#,,#",
                "#,,,,,#..........D,,,D..#,,#",
                "#,,,,,#..........#,,,#..#,,#",
                "#,,,,,############,,,####,,#",
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,,######D######,,,,,,,,,,,#",
                "#,,#...........#,,,,,,,,,,,#",
                "#,,#...........#,,,,,,,,,,,#",
                "#,,#...........#,,,,,,,,,,,#",
                "#,,#############,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,#",
                "############################",
            }, Height: 2f),
            new[]
            {
                new Suspect("Mara Voss", "Warehouse foreman", 0, 9.5f, 5.5f,
                    "You're the private eye? Good. That crate was worth more than this whole pier.",
                    "I locked up at ten and went straight home. My neighbour saw me come in.",
                    D(("gate", "The gate opened at 1:40? Nobody should have been here. Only the guard and I carry badges."),
                      ("boots", "Those are guard boots. I wear deck shoes, look."),
                      ("note", "Pier 9 is where the smugglers tie up. Whoever wrote that knew it."),
                      ("tab", "Juno drinks at the Rust Bucket most nights. Everyone knows it."),
                      ("hauler", "Juno's hauler has been grounded all week. Blown coupling.")), false),
                new Suspect("Dex Kollins", "Night guard", 1, 22.5f, 5.5f,
                    "Yeah, I work nights. What of it?",
                    "Walking my rounds, all night, same as always. Didn't see a thing. Didn't open a thing.",
                    D(("gate", "!My badge? Must've been cloned. I... lost it last week. Meant to report it."),
                      ("boots", "Lots of people wear boots like mine."),
                      ("note", "!Never seen that before in my life. Someone planted it."),
                      ("tab", "Juno? Always at the bar."),
                      ("hauler", "That hauler hasn't moved in days.")), true),
                new Suspect("Juno Pike", "Cargo pilot", 2, 6.5f, 13.5f,
                    "If you're selling something, I'm not buying. If you're buying, the next round's on you.",
                    "My hauler's grounded for repairs, so I was right here at the Rust Bucket until two.",
                    D(("gate", "Not my badge. I'm not even allowed on that side of the pier."),
                      ("boots", "Pilot boots have soft soles. Those aren't mine."),
                      ("note", "Pier 9 at 2am? Someone was doing a deal, and it wasn't me. I was here."),
                      ("tab", "See? Stamped five to two. I told you."),
                      ("hauler", "She'll fly again when the coupling comes in. Not before.")), false),
            },
            new[]
            {
                new Clue("gate", "Gate log", 15.5f, 4.5f,
                    "The loading gate's access log. Last night: 01:40, gate opened by badge #0417, D. Kollins. Closed 01:52.",
                    "the loading gate was opened with Dex Kollins's badge at 01:40."),
                new Clue("boots", "Boot prints", 10.5f, 7.5f,
                    "Oily prints lead from the empty rack to the east door. Heavy tread, size 11. Security-issue boots.",
                    "heavy security-issue boot prints by the empty rack."),
                new Clue("note", "Crumpled note", 23.5f, 7.5f,
                    "Stuffed in the back of the guard's locker: \"Pier 9, 2am. Cash on delivery. Come alone.\"",
                    "a note in the guard's locker about a deal at Pier 9 at 2am."),
                new Clue("tab", "Bar tab", 13.5f, 12.5f,
                    "A Rust Bucket bar tab in Juno Pike's name. Last drink stamped 01:55.",
                    "Juno Pike was drinking at the Rust Bucket at 01:55."),
                new Clue("hauler", "Grounded hauler", 22.5f, 13.5f,
                    "Juno's cargo hauler. Engine cold, a repair tag on the coupling dated three days ago. It hasn't flown.",
                    "Juno's hauler hasn't flown for three days."),
            },
            new[] { new Insight("gate", "note", "the gate opened at 01:40 with the guard's badge, twenty minutes before a 2am deal at Pier 9.") },
            new[] { "gate", "note" },
            "Dex breaks: \"The badge was mine. Somebody offered me more than a year's pay for one crate.\" The nanites are " +
            "recovered at Pier 9, and your fee clears by morning."),

        new("Static on Channel 7",
            "The Chrome Lounge. Holo-news anchor Lyra Sol collapsed after one drink last night; she'll live, but someone spiked " +
            "it. Her producer, her rival and the bartender were all there. Find out who, and whether it's happened before.",
            new MapDef("Chrome Lounge", "The Chrome Lounge, Neon Harbor.", "town", new[]
            {
                "############################",
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,@,,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,,,,##########D##########,#",
                "#,,,,#...................#,#",
                "#,,,,#...................#,#",
                "#,,,,#..WWWWW............#,#",
                "#,,,,#...................#,#",
                "#,,,,#...........#####D###,#",
                "#,,,,#...........#.......#,#",
                "#,,,,#...........#.......#,#",
                "#,,,,######D######.......#,#",
                "#,,,,,,,,,,,,,,,,#########,#",
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,,,,,,,####,,#",
                "#,,,,,,,,,,,,,,,,,,,,#..D,,#",
                "#,,,,,,,,,,,,,,,,,,,,####,,#",
                "############################",
            }, Height: 2f),
            new[]
            {
                new Suspect("Tesh Moreau", "Bartender", 3, 14.5f, 6.5f,
                    "What'll it be? Kidding. You're here about Lyra. Terrible thing.",
                    "I've only ever worked here at the Chrome. Never set foot in the Neon Mile or the Grotto.",
                    D(("glass", "Sure, I garnish every drink with a twist. Doesn't mean I put anything else in it."),
                      ("roster", "!Guest shifts? Okay, maybe once or twice. So what? A bartender's gotta eat."),
                      ("reports", "!Spiked drinks at other clubs? Never heard about it. Bad crowd out there."),
                      ("contract", "Ilan and Lyra fought like cats. Everyone heard it."),
                      ("camera", "The booth camera? Never looked at it.")), true),
                new Suspect("Ilan Brask", "Producer", 4, 21.5f, 10.5f,
                    "Lyra's my star. Whoever did this is costing me ratings.",
                    "I was in the control booth all night. Ask anyone. Ask the camera.",
                    D(("glass", "I never went near the bar. I had my own coffee in the booth."),
                      ("roster", "Guest bartenders? News to me. I don't book the staff."),
                      ("reports", "Two other clubs? Then this isn't about Lyra at all, is it."),
                      ("contract", "Yes, we're fighting over money. I want her on air, not in hospital."),
                      ("camera", "See? I never left the booth. Not once.")), false),
                new Suspect("Nova Quill", "Rival anchor", 5, 20.5f, 5.5f,
                    "I'd love to say I'm sorry. I'm mostly sorry it wasn't my scoop.",
                    "I had one drink and left at ten past eleven. The rest of the night I was editing at home.",
                    D(("glass", "I left before her drink was even poured."),
                      ("roster", "The same bartender at three clubs? Now that's a story."),
                      ("reports", "I covered the Neon Mile one. Same sedative, same kind of crowd."),
                      ("contract", "Ilan's a shark, but he's a shark who needs her."),
                      ("camera", "There, 23:10, out the door. Told you.")), false),
            },
            new[]
            {
                new Clue("glass", "Lyra's glass", 18.5f, 7.5f,
                    "A tall glass with a lime twist. The lab strip turns blue: a club sedative. Only bar staff add the twist.",
                    "Lyra's drink was spiked with a club sedative, and garnished by the bar staff."),
                new Clue("roster", "Staff roster", 7.5f, 4.5f,
                    "The agency roster behind the bar. Tesh Moreau: guest shifts at the Neon Mile (the 3rd), the Blue Grotto (the 11th), the Chrome Lounge (last night).",
                    "Tesh Moreau worked guest shifts at the Neon Mile and the Blue Grotto this month."),
                new Clue("reports", "Police bulletins", 22.5f, 15.5f,
                    "The precinct kiosk's bulletin board: drinks spiked at the Neon Mile on the 3rd and the Blue Grotto on the 11th. Same sedative.",
                    "drinks were spiked at the Neon Mile on the 3rd and the Blue Grotto on the 11th."),
                new Clue("contract", "Contract dispute", 19.5f, 9.5f,
                    "A marked-up contract on the booth table. Ilan and Lyra are fighting over her renewal. Ugly, but it's about money.",
                    "Ilan Brask and Lyra are in a contract dispute."),
                new Clue("camera", "Booth camera log", 23.5f, 11.5f,
                    "The booth camera's log: Ilan Brask at the desk all night. In the lounge feed, Nova Quill leaves at 23:10, before Lyra's drink is poured.",
                    "Ilan never left the booth, and Nova left before the drink was poured."),
            },
            new[]
            {
                new Insight("roster", "reports", "every spiked drink this month happened on a night Tesh Moreau worked the bar. The Neon Mile, the Blue Grotto, the Chrome."),
                new Insight("glass", "camera", "the drink was poured after Nova left, and Ilan never came down from the booth. That leaves the bar."),
            },
            new[] { "glass", "roster", "reports" },
            "Tesh folds: \"They pay me to take the spotlight off people for a night. Lyra was just the next name.\" The precinct " +
            "takes it from here, and Channel 7 runs your name on the evening bulletin."),

        new("The Ghost in the Grid",
            "The Grid District. Every few nights the whole district blacks out, midnight to four. The city fixer wants to know " +
            "who's stealing the power. The substation engineer, a junk dealer and a teenage hacker all have the know-how.",
            new MapDef("Grid District", "The Grid District, Neon Harbor.", "town", new[]
            {
                "############################",
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,@,,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,,,####D####,#",
                "#,,B,,B,,B,,,,,,,#.......#,#",
                "#,,,,,,,,,,,,,,,,#..WW...#,#",
                "#,,B,,B,,B,,,,,,,#.......#,#",
                "#,,,,,,,,,,,,,,,,#########,#",
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,,###D#########,,,,,,,,,,,#",
                "#,,#.....#.....#,,,,,,,,,,,#",
                "#,,#...........#,,,,,,,,,,,#",
                "#,,#.....#.....#,,,,,,,,,,,#",
                "#,,#############,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,,,,,,,,,,,,,#",
                "############################",
            }, Height: 2f),
            new[]
            {
                new Suspect("Orin Hale", "Grid engineer", 6, 19.5f, 4.5f,
                    "Blackouts? Old cables, aging transformers. I've told the city for years.",
                    "I'm off shift at midnight. Home by half past, every night. Check my door log if you like.",
                    D(("outage", "Every third night, huh? Probably a load cycle in the old relays."),
                      ("maint", "!Those are... routine checks. They've got nothing to do with the outages."),
                      ("rig", "!Never seen that thing. Anyone could scratch a work ID onto a case."),
                      ("gens", "Sable's diesels? They're not wired to anything of mine."),
                      ("deck", "Kids and their decks. Harmless, mostly.")), true),
                new Suspect("Sable Kray", "Junk dealer", 7, 5.5f, 5.5f,
                    "Buying or selling? I don't do questions for free. ...Fine. Ask.",
                    "I run my generators every night because the grid's no good. I'm asleep by eleven.",
                    D(("outage", "Every third night, like clockwork. That's not old cables, that's somebody."),
                      ("maint", "The engineer checks the substation on blackout nights? Funny coincidence."),
                      ("rig", "A mining rig on grid power? That'll eat a district's worth."),
                      ("gens", "All diesel. Not one of them's wired to the grid. Look for yourself."),
                      ("deck", "Pip couldn't hack a toaster. Sweet kid, though.")), false),
                new Suspect("Pip Tanaka", "Teen hacker", 8, 12.5f, 11.5f,
                    "I didn't do it! Whatever it is. Okay, what is it?",
                    "Blackout nights I'm stuck at home with a dead deck and my mum. Every time.",
                    D(("outage", "Every third night? I can fetch you the grid's load charts. It spikes right before."),
                      ("maint", "Maintenance on exactly those nights? That's not maintenance."),
                      ("rig", "That's a mining rig! Those things drink power."),
                      ("gens", "Sable's diesels are loud, but they're not on the grid."),
                      ("deck", "Game mods and homework. Don't tell anyone about the game mods.")), false),
            },
            new[]
            {
                new Clue("outage", "Outage records", 13.5f, 8.5f,
                    "The public grid terminal: blackouts on the 3rd, 6th, 9th, 12th and 15th, each from midnight to four.",
                    "blackouts every third night, midnight to four."),
                new Clue("maint", "Maintenance log", 19.5f, 6.5f,
                    "The substation's log: O. Hale, 'relay inspection', 23:50 on the 3rd, 6th, 9th, 12th and 15th.",
                    "Orin Hale logs maintenance just before midnight on the 3rd, 6th, 9th, 12th and 15th."),
                new Clue("rig", "Mining rig", 23.5f, 4.5f,
                    "Behind the switchgear, a server rack hums, patched straight into the district feed. Engraved on the casing: work ID 3319, O. Hale.",
                    "a crypto-mining rig tapped into the district feed, engraved with Orin Hale's work ID."),
                new Clue("gens", "Diesel generators", 8.5f, 5.5f,
                    "Sable's junkyard generators: every one of them diesel, none wired to the grid.",
                    "Sable Kray's generators are all diesel, off the grid."),
                new Clue("deck", "Hacking deck", 5.5f, 12.5f,
                    "Pip's deck: game mods, school notes and a very bad poem. Nothing that touches the grid.",
                    "Pip's deck has nothing to do with the grid."),
            },
            new[]
            {
                new Insight("outage", "maint", "the blackouts start just after Orin Hale's 'relay inspections', every third night, like clockwork."),
                new Insight("rig", "maint", "the rig carries Orin Hale's work ID, and he's in the substation every blackout night."),
            },
            new[] { "outage", "maint", "rig" },
            "Orin sighs: \"Engineers' pay, detective. The rig made more in a night than the city pays me in a month.\" The " +
            "district's lights stay on, and the fixer calls you the best in Neon Harbor."),
    };
}
