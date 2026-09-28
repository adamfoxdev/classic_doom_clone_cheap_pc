namespace HexenSharp;

/// <summary>Checks on game feel: boss intro cards, hit-stop, screen shake, damage numbers and the dynamic music.</summary>
public static partial class Headless
{
    static void FeelChecks(Action<bool, string> check)
    {
        Game Fresh(string map, float px, float py)
        {
            var gg = new Game { FixedSeed = 1, AchievementsOn = false };
            gg.NewGame(PClass.Fighter);
            gg.Warp(Array.FindIndex(gg.Hub, l => l.RawName == map));
            gg.Level.Things.RemoveAll(t => t is Monster { Def.MiniBoss: null });
            gg.P.X = px; gg.P.Y = py; gg.P.FloorZ = gg.Level.FloorAt(px, py); gg.P.Health = 400; gg.P.MaxHealth = 400;
            return gg;
        }
        void Run(Game gg, int frames, Func<bool> until = null) { for (int k = 0; k < frames && (until == null || !until()); k++) gg.Update(default, 1f / 35f); }

        // ---------------------------------------------------------------- boss intros
        var g = Fresh("Hanging Cisterns", 12.5f, 9.5f);
        var kp = g.Level.Things.OfType<Monster>().Single(t => t.Def == MiniBosses.Keeper);
        check(g.Intro == null, "no intro before the boss wakes");
        Run(g, 10, () => g.Intro != null);
        check(g.Intro is { } i1 && i1.Boss == kp && i1.Name == kp.Def.Name.ToUpperInvariant() && i1.Title == Game.BossTitle(kp.Def),
              $"when a mini-boss wakes near you, its card shows its name and a line about it ({g.Intro?.Name}: {g.Intro?.Title})");
        Run(g, 14);
        var r = new Renderer();
        r.Render(g);
        bool barsDown = Enumerable.Range(0, Renderer.W).All(x => r.Fb[4 * Renderer.W + x] == Col.Rgb(0, 0, 0));
        check(barsDown && g.Intro.Amount >= 1, "letterbox bars slide in over the view");
        check(r.Fb.Count(px => px == Col.Rgb(220, 50, 40)) > 100, "the boss's health bar still shows, over the letterbox");
        float before = g.P.X;
        Run(g, (int)(BossIntro.Length * 35) + 2);
        check(g.Intro == null && kp.Alive && kp.State != AiState.Idle, $"it's gone after {BossIntro.Length} seconds");
        Run(g, 35);
        check(g.Intro == null, "and it only ever shows once for that boss");
        var tmp = new Game { FixedSeed = 1 };
        var bossDefs = MiniBosses.All.Append(Monster.Heresiarch).ToArray();
        var titles = new HashSet<string>();
        foreach (var st in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
        {
            tmp.SetArtStyle(st);
            foreach (var bd in bossDefs) titles.Add(Game.BossTitle(bd));
        }
        tmp.SetArtStyle(ArtStyle.SciFi);
        check(titles.Count == bossDefs.Length * 2, $"each of the {bossDefs.Length} bosses has its own line, in both styles");

        var off = Fresh("Hanging Cisterns", 12.5f, 9.5f);
        off.Con.Execute("bossintros 0", quiet: true);
        Run(off, 20);
        var okp = off.Level.Things.OfType<Monster>().Single(t => t.Def == MiniBosses.Keeper);
        check(off.Intro == null && okp.State != AiState.Idle && okp.Introduced && Settings.Lines(off).Contains("bossintros 0"), "'bossintros 0' turns them off (and it's saved)");
        var rel = new Game { FixedSeed = 1, AchievementsOn = false, Style = GameStyle.Relaxed };
        rel.NewGame(PClass.Fighter);
        var hb = rel.Hub[0].Things.OfType<Monster>().First(m => m.Def.Boss);
        rel.P.X = hb.X + 2; rel.P.Y = hb.Y;
        hb.State = AiState.Chase;
        Run(rel, 5);
        check(rel.Intro == null, "the relaxed style has none (nothing's hostile)");

        // ---------------------------------------------------------------- hit-stop and shake
        var h = new Game { FixedSeed = 1, AchievementsOn = false };
        h.NewGame(PClass.Fighter);
        h.Level.Things.RemoveAll(t => t is Monster);
        Monster Ettin(float dx)
        {
            var m = new Monster(Monster.Ettin) { X = h.P.X + dx, Y = h.P.Y, Level = h.Level, State = AiState.Chase };
            h.Level.Things.Add(m);
            return m;
        }
        var e = Ettin(2);
        h.DamageMonster(e, 10, 0);
        check(h.HitStop <= 0, "a light blow doesn't stop anything");
        h.DamageMonster(e, Game.HeavyHit, 0);
        check(MathF.Abs(h.HitStop - Game.HitStopHeavy) < 0.001f, $"a heavy blow ({Game.HeavyHit}+) freezes the action for {Game.HitStopHeavy * 1000:0} ms");
        // a puff of smoke: how much of its life goes in a frame, during the hit-stop and after
        var puff = new Puff(Art.BossBall[1], 0.3f, 10f, 0f) { X = h.P.X, Y = h.P.Y, Level = h.Level };
        h.Level.Things.Add(puff);
        float Age() { float l = puff.Life; h.Update(default, 1f / 35f); return l - puff.Life; }
        float crawl = Age();
        Run(h, 3);
        float normal = Age();
        check(crawl < 0.002f && normal > 0.02f, $"the world all but stops while it lasts, then carries on ({crawl:0.0000}s then {normal:0.0000}s a frame)");
        h.HitStop = 0;
        var big = new Monster(Monster.Centaur) { X = h.P.X + 2, Y = h.P.Y, Level = h.Level, State = AiState.Chase };
        h.Level.Things.Add(big);
        h.DamageMonster(big, 100000, 0);
        check(MathF.Abs(h.HitStop - Game.HitStopKill) < 0.001f, "killing something big holds a little longer");
        h.HitStop = 0;
        var mb = new Monster(MiniBosses.Warden) { X = h.P.X + 3, Y = h.P.Y, Level = h.Level, State = AiState.Chase };
        h.Level.Things.Add(mb);
        h.DamageMonster(mb, 100000, 0);
        check(MathF.Abs(h.HitStop - Game.HitStopBoss) < 0.001f && h.Shake > 0.5f, "and felling a boss longest, with a big shake");
        h.HitStop = 0;
        h.Con.Execute("hitstop 0", quiet: true);
        h.DamageMonster(Ettin(2), 100000, 0);
        check(h.HitStop <= 0 && Settings.Lines(h).Contains("hitstop 0"), "'hitstop 0' turns it off");

        h.Shake = 0;
        h.P.Armor = 0;
        h.DamagePlayer(20);
        float trauma = h.Shake;
        var (yaw, pitch) = h.ShakeOffset();
        check(trauma > 0.4f && (MathF.Abs(yaw) > 0 || MathF.Abs(pitch) > 0), $"taking a hit shakes the view (trauma {trauma:0.00})");
        h.Vars.Shake = 2;
        var strong = h.ShakeOffset();
        h.Vars.Shake = 0;
        var none = h.ShakeOffset();
        h.Vars.Shake = 1;
        check(MathF.Abs(strong.yaw - 2 * yaw) < 1e-5f && MathF.Abs(strong.pitch - 2 * pitch) < 1e-4f && none == (0, 0), "twice as much on strong, none with it off");
        var calm = new Renderer();
        float keep = h.Shake;
        h.Shake = 0; calm.Render(h);
        var still = (uint[])calm.Fb.Clone();
        h.Shake = 1; calm.Render(h);
        check(!still.SequenceEqual(calm.Fb), "the rendered view moves with it");
        h.Shake = keep;
        h.Level.Things.RemoveAll(t => t is Monster);
        Run(h, 35);
        check(h.Shake == 0, "it settles within a second");

        // ---------------------------------------------------------------- damage numbers
        var d = new Game { FixedSeed = 1, AchievementsOn = false };
        d.NewGame(PClass.Fighter);
        d.Level.Things.RemoveAll(t => t is Monster);
        // down the great hall, with the monster 4 off: far enough that the score pop over its head is in view
        d.P.X = 10.5f; d.P.Y = 5.5f; d.P.Angle = 0; d.P.FloorZ = d.Level.FloorAt(d.P.X, d.P.Y);
        var ahead = (x: d.P.X + 4, y: d.P.Y);
        check(Enumerable.Range(0, 6).All(s => !d.Level.Blocks((int)d.P.X + s, (int)d.P.Y)), "(the hall is open ahead)");
        int Numbers(bool numbers, bool arcade, uint colour)
        {
            d.Vars.DamageNumbers = numbers; d.Vars.Arcade = arcade;
            d.Arcade.Floaters.Clear();
            var m = new Monster(Monster.Ettin) { X = ahead.x, Y = ahead.y, Level = d.Level, State = AiState.Chase };
            d.Level.Things.Add(m);
            d.DamageMonster(m, 12, 0);
            d.DamageMonster(m, 100000, 0);
            var rr = new Renderer();
            rr.Render(d);
            return rr.Fb.Count(px => px == colour);
        }
        uint white = Col.Rgb(240, 236, 225), score = Col.Rgb(255, 240, 150);
        check(Numbers(false, false, white) == 0, "no damage numbers by default");
        check(Numbers(true, false, white) > 5 && Numbers(true, false, score) == 0, "'damagenumbers 1' pops them out of what you hit, without arcade mode's score");
        check(Numbers(false, true, score) > 5, "arcade mode still shows its numbers and score");
        d.Vars.Arcade = false;

        // ---------------------------------------------------------------- the Effects page
        var o = new Game { FixedSeed = 1 };
        o.Menu.Show(MenuPage.Options);
        o.Menu.Cursor = Array.IndexOf(o.Menu.Items(MenuPage.Options), "Effects");
        o.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(o.Menu.Page == MenuPage.Effects && o.Menu.Items(MenuPage.Effects).SequenceEqual(new[] { "Screen shake", "Hit-stop", "Damage numbers", "Boss intros", "Dynamic music", "Back" }),
              "Options > Effects has screen shake, hit-stop, damage numbers, boss intros and dynamic music");
        check(o.Menu.Value(0) == "NORMAL" && o.Menu.Value(1) == "ON" && o.Menu.Value(2) == "OFF" && o.Menu.Value(3) == "ON" && o.Menu.Value(4) == "ON",
              "shake normal, hit-stop on, damage numbers off, boss intros on and dynamic music on to start with");
        o.Menu.Update(new Input { Right = true }, 1f / 35f);
        check(o.Vars.Shake == 2 && o.Menu.Value(0) == "STRONG", "Right steps the shake up to strong");
        o.Menu.Cursor = 2;
        o.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(o.Vars.DamageNumbers && o.Menu.Value(2) == "ON", "and Enter switches damage numbers on");
        o.Menu.Cursor = 5;
        o.Menu.Update(new Input { Confirm = true }, 1f / 35f);
        check(o.Menu.Page == MenuPage.Options && Settings.Lines(o).Contains("shake 2") && Settings.Lines(o).Contains("damagenumbers 1"), "Back returns to Options, saving them");

        // ---------------------------------------------------------------- dynamic music
        bool tLen = true, tLoud = true, tRoom = true, tSeam = true, differs = true;
        foreach (var t in MusicGen.Tracks)
            foreach (var st in new[] { ArtStyle.Fantasy, ArtStyle.SciFi })
            {
                var ten = MusicGen.MakeTension(t, st);
                var bas = MusicGen.Make(t, st);
                tLen &= ten.Length == bas.Length;
                tLoud &= Math.Sqrt(ten.Average(x => (double)x * x)) / 32767 > 0.03;
                int peak = ten.Max(x => Math.Abs((int)x));
                tRoom &= peak > 13000 && peak < 16000;
                int maxStep = 0;
                for (int k = 1; k < ten.Length; k++) maxStep = Math.Max(maxStep, Math.Abs(ten[k] - ten[k - 1]));
                tSeam &= Math.Abs(ten[0] - ten[^1]) <= maxStep;
                differs &= !ten.Take(4000).SequenceEqual(bas.Take(4000));
            }
        check(tLen && tSeam, "every track has a tension layer, the same length, looping without a click");
        check(tLoud && tRoom && differs, "not silent, peaking at 45%, and not just the track again");

        var mx = new MusicMixer();
        mx.Play("hall", ArtStyle.Fantasy);
        var basT = mx.Get("hall", ArtStyle.Fantasy);
        var tenT = mx.Get("hall", ArtStyle.Fantasy, tension: true);
        var buf = new short[1000];
        mx.Fill(buf, 1f);
        check(mx.Level == 0 && buf.Select((v, k) => Math.Abs(v - basT[k]) <= 1).All(b => b), "calm, the mixer plays just the track");
        mx.Intensity = 1;
        var swell = new short[(int)(MusicMixer.SwellTime * MusicGen.Rate) + 200];
        mx.Fill(swell, 1f);
        int at = (1000 + swell.Length - 1) % basT.Length;
        check(mx.Level == 1 && Math.Abs(swell[^1] - (basT[at] * 0.85f + tenT[at])) <= 2, $"a fight swells the tension layer in over {MusicMixer.SwellTime} seconds");
        mx.Intensity = 0;
        var second = new short[MusicGen.Rate];
        mx.Fill(second, 1f);
        float after1 = mx.Level;
        for (int k = 0; k < 5; k++) mx.Fill(second, 1f);
        check(after1 > 0.75f && after1 < 0.85f && mx.Level == 0, $"and it dies away slowly once it's clear, over {MusicMixer.CalmTime} seconds");

        var mg = new Game { FixedSeed = 1, AchievementsOn = false };
        check(mg.MusicIntensity == 0, "no tension on the title");
        mg.NewGame(PClass.Fighter);
        mg.Level.Things.RemoveAll(t => t is Monster);
        check(mg.MusicIntensity == 0, "none with nothing after you");
        var hunter = new Monster(Monster.Ettin) { X = mg.P.X + 4, Y = mg.P.Y, Level = mg.Level };
        mg.Level.Things.Add(hunter);
        check(mg.MusicIntensity == 0, "nor with a monster that hasn't noticed you");
        hunter.State = AiState.Chase;
        check(mg.MusicIntensity == 1, "a monster onto you brings in the tension");
        mg.DamageMonster(hunter, 100000, 0);
        check(mg.MusicIntensity == 0, "and killing it lets the music calm again");
        hunter = new Monster(Monster.Ettin) { X = mg.P.X + 4, Y = mg.P.Y, Level = mg.Level, State = AiState.Chase };
        mg.Level.Things.Add(hunter);
        mg.Con.Execute("dynamicmusic 0", quiet: true);
        check(mg.MusicIntensity == 0 && Settings.Lines(mg).Contains("dynamicmusic 0"), "'dynamicmusic 0' keeps it calm");
    }
}
