namespace HexenSharp;

/// <summary>Checks on the monster codex.</summary>
public static partial class Headless
{
    static void CodexChecks(Action<bool, string> check)
    {
        var defs = typeof(Monster).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(MonsterDef)).Select(f => (MonsterDef)f.GetValue(null)).Concat(MiniBosses.All).Distinct().ToList();
        check(defs.All(d => Codex.Find(d.Art) is { } e && e.Def == d) && Codex.All.Select(e => e.Id).Distinct().Count() == Codex.All.Length,
              $"an entry for every one of the {defs.Count} monsters");
        check(Codex.All.All(e => e.Lore.Length > 20 && e.SciFiLore.Length > 20 && e.Weakness.Length > 20 && e.Lore != e.SciFiLore),
              "each with lore in both styles, and how to beat it");

        var g = new Game { FixedSeed = 1, AchievementsOn = false, Profile = new Profile() };
        g.NewGame(PClass.Fighter);
        check(Codex.Found(g.Profile) == 0 && g.Menu.Items(MenuPage.Codex).Take(Codex.All.Length).All(s => s == "???"), "a new profile has found none");
        var ettin = g.Level.Things.OfType<Monster>().First(m => m.Def == Monster.Ettin);
        g.Messages.Clear();
        g.DamageMonster(ettin, 100000, 0);
        var entry = Codex.Find("ettin");
        check(Codex.Kills(g.Profile, entry) == 1 && Codex.Unlocked(g.Profile, entry) && g.Messages.Any(m => m.text == $"New codex entry: {Monster.Ettin.Name}."),
              "your first kill of a monster opens its entry, and says so");
        check(g.Menu.Items(MenuPage.Codex)[Array.IndexOf(Codex.All, entry)] == Monster.Ettin.Name, "and it shows by name in the list");
        var second = g.Level.Things.OfType<Monster>().First(m => m.Def == Monster.Ettin && m.Alive);
        g.Messages.Clear();
        g.DamageMonster(second, 100000, 0);
        check(Codex.Kills(g.Profile, entry) == 2 && !g.Messages.Any(m => m.text.StartsWith("New codex entry")), "later kills just count up");
        var other = g.Level.Things.OfType<Monster>().First(m => m.Alive && m.Def != Monster.Ettin);
        g.DamageMonster(other, 100000);
        check(!Codex.Unlocked(g.Profile, Codex.Find(other.Def.Art)), "a monster killed by something else (a splash, a trap) doesn't count");

        var pr = new Game { FixedSeed = 1, AchievementsOn = false, Profile = new Profile() };
        pr.StartArena(PClass.Fighter);
        var wave = pr.Level.Things.OfType<Monster>().FirstOrDefault(m => m.Alive);
        if (wave != null) pr.DamageMonster(wave, 100000, 0);
        check(wave == null || Codex.Kills(pr.Profile, Codex.Find(wave.Def.Art)) == 1, "arena kills count");

        var vet = new Profile { MiniBosses = new() { "warden" } };
        check(Codex.Unlocked(vet, Codex.Find("warden")) && Codex.Found(vet) == 1, "a mini-boss you'd already beaten is in it");

        string path = Path.Combine(Path.GetTempPath(), $"hexensharp-codex-{Environment.ProcessId}.json");
        g.Profile.Save(path);
        var loaded = Profile.Load(path);
        File.Delete(path);
        check(Codex.Kills(loaded, entry) == 2, "kills are kept in your profile");

        // Character > Codex, drawn: a locked entry is a silhouette, an open one in colour with its lore
        g.Menu.Show(MenuPage.Character);
        g.Menu.Cursor = Array.IndexOf(g.Menu.Items(MenuPage.Character), "Codex");
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Menu.Page == MenuPage.Codex, "Character > Codex opens it");
        var r = new Renderer();
        g.Menu.Cursor = Array.IndexOf(Codex.All, entry);
        r.Render(g);
        int lit = Enumerable.Range(41, 48).Sum(y => Enumerable.Range(133, 48).Count(x => r.Fb[y * Renderer.W + x] != Col.Rgb(24, 20, 18) && r.Fb[y * Renderer.W + x] != Col.Rgb(8, 6, 6)));
        g.Menu.Cursor = Array.IndexOf(Codex.All, Codex.Find("heresiarch"));
        r.Render(g);
        int dark = Enumerable.Range(41, 48).Sum(y => Enumerable.Range(133, 48).Count(x => r.Fb[y * Renderer.W + x] == Col.Rgb(8, 6, 6)));
        check(lit > 200 && dark > 200, "an open entry shows the monster, a locked one only its silhouette");
        g.Menu.Cursor = Codex.All.Length;
        g.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(g.Menu.Page == MenuPage.Character, "Back returns to the Character screen");
    }
}
