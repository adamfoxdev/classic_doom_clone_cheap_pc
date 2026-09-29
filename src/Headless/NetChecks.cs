using System.Text.Json;

namespace HexenSharp;

/// <summary>
/// Checks on online co-op, a host and a client in one process joined by an in-memory link that round-trips every
/// message through the wire's JSON: the two stay in step, mouse movement is neither lost nor doubled, each player
/// keeps their own sensitivity, a missing frame or a drift is caught, Esc opens a menu rather than ending the game,
/// monsters don't walk through the player they're after, and a downed crew gets back up.
/// </summary>
public static partial class Headless
{
    sealed class LoopLink : INetLink
    {
        public LoopLink Peer;
        readonly Queue<NetworkMessage> _in = new();
        public Func<NetworkMessage, bool> Drop;
        public bool Lost { get; set; }
        public void SendNetwork(NetworkMessage m)
        {
            if (Drop?.Invoke(m) == true || Peer == null) return;
            Peer._in.Enqueue(JsonSerializer.Deserialize<NetworkMessage>(JsonSerializer.SerializeToUtf8Bytes(m, OnlineSession.Json), OnlineSession.Json));
        }
        public bool TryReceiveNetwork(out NetworkMessage m) => _in.TryDequeue(out m);
    }

    static void NetChecks(Action<bool, string> check)
    {
        const float Frame = 1f / 60f;
        (Game h, Game c, LoopLink hl, LoopLink cl) Pair(Action<Game> prepHost = null, Action<Game> prepClient = null)
        {
            var h = new Game { AchievementsOn = false };
            var c = new Game { AchievementsOn = false };
            prepHost?.Invoke(h); prepClient?.Invoke(c);
            var hl = new LoopLink(); var cl = new LoopLink();
            hl.Peer = cl; cl.Peer = hl;
            h.UseNetLink(hl, "a"); c.UseNetLink(cl, "b");
            OnlineSession.StartHost(h, hl, new[] { "a", "b" }, "a", 4242);
            c.Update(new Input(), Frame); // (the start arrives)
            return (h, c, hl, cl);
        }
        OnlinePlayer Of(Game g, string id) => g.OnlinePlayers.Single(p => p.Id == id);

        // starting: both copies hold the same game, each seen from its own player
        var (host, client, hostLink, clientLink) = Pair();
        check(host.NetSession?.IsHost == true && client.NetSession is { IsHost: false } && client.P == Of(client, "b").State && host.P == Of(host, "a").State
              && host.OnlineHash() == client.OnlineHash(),
            "the client joins the host's game: the same world, seen from its own player");
        check(host.Level.Things.OfType<NetworkAvatar>().Count() == 2 && client.Level.Things.OfType<NetworkAvatar>().Count() == 2,
            "every player has an avatar on every machine (your own just isn't drawn), so both hold the same things");

        // ten seconds of play, both players moving, turning, jumping and firing: still in step, tick for tick
        var startA = (Of(host, "a").State.X, Of(host, "a").State.Y);
        var startB = (Of(host, "b").State.X, Of(host, "b").State.Y);
        for (int k = 0; k < 600; k++)
        {
            host.Update(new Input { Move = k % 200 < 120 ? 1 : -1, LookX = k % 90 < 30 ? 4 : 0, Fire = k % 50 == 0, Jump = k % 70 == 0 }, Frame);
            client.Update(new Input { Strafe = k % 160 < 80 ? 1 : -1, Move = 0.5f, LookX = -3, LookY = k % 40 < 20 ? 1 : -1, Fire = k % 45 == 0, Jump = k % 60 == 0 }, Frame);
        }
        client.Update(new Input(), 0); // (the last frames in)
        var a = Of(host, "a").State; var b = Of(host, "b").State;
        bool moved = Game.Dist(a.X, a.Y, startA.X, startA.Y) > 1 && Game.Dist(b.X, b.Y, startB.X, startB.Y) > 1;
        check(client.NetSession != null && host.NetSession != null && client.NetSession.TickCount == host.NetSession.TickCount && host.NetSession.TickCount >= 590
              && host.OnlineHash() == client.OnlineHash() && moved,
            $"after ten seconds of both playing, the two copies are still identical ({host.NetSession?.TickCount} ticks, both players moved)");

        // every two seconds the host tells the matchmaker how it's going, for its who board; the client pays it no mind
        (host, client, hostLink, _) = Pair();
        var statuses = new List<NetworkMessage>();
        hostLink.Drop = m => { if (m.Type == "status") statuses.Add(m); return false; };
        for (int k = 0; k < 250; k++) { host.Update(new Input(), Frame); client.Update(new Input(), Frame); }
        client.Update(new Input(), 0);
        var said = statuses.LastOrDefault();
        check(statuses.Count == 3 && said?.Status == $"{host.Level.Name}, 0 kills" && said.Scores?.Length == 2 && said.Scores.All(sc => sc.Health == 100 && sc.Team == -1 && !sc.Dead)
              && said.Scores.Select(sc => sc.PlayerId).SequenceEqual(new[] { "a", "b" }) && client.NetSession != null && host.OnlineHash() == client.OnlineHash(),
            $"every two seconds (at once, too) the host sends the matchmaker's who board a status ({statuses.Count} in {host.NetSession?.TickCount} ticks: \"{said?.Status}\", each player's health and kills); the game plays on");

        // mouse movement adds up between ticks: at 144 frames a second, none is lost
        (host, client, _, _) = Pair();
        float before = host.P.Angle;
        for (int k = 0; k < 144; k++) host.Update(new Input { LookX = 5 }, 1f / 144f);
        float turned = host.P.Angle - before, want = 144 * 5 * 0.0025f;
        check(MathF.Abs(turned - want) < 0.05f, $"at 144 fps the host's mouse turns it the whole way ({turned:0.00} of {want:0.00} radians)");
        var buffer = new OnlineSession.InputBuffer();
        buffer.Push(new NetworkInput { LookX = 3, Move = 1 }); buffer.Push(new NetworkInput { LookX = 4, Move = 1, Jump = true });
        var first = buffer.Pop(); var second = buffer.Pop();
        check(first.LookX == 7 && first.Jump && second.LookX == 0 && !second.Jump && second.Move == 1,
            "between ticks, look adds up and presses wait for the next tick; held keys carry on, and nothing counts twice");

        // each player's own sensitivity: the client's is double the host's, so the same mouse turns it twice as far
        (host, client, _, _) = Pair(prepClient: g => { g.Vars.Sens = 2f; g.Vars.Hud = HudStyle.Minimal; });
        before = Of(host, "b").State.Angle;
        for (int k = 0; k < 60; k++) { host.Update(new Input(), Frame); client.Update(new Input { LookX = 5 }, Frame); }
        host.Update(new Input(), Frame); host.Update(new Input(), Frame);
        turned = Of(host, "b").State.Angle - before;
        check(client.Vars.Sens == host.Vars.Sens && client.Vars.Hud == HudStyle.Minimal && MathF.Abs(turned - 60 * 5 * 0.0025f * 2) < 0.05f,
            $"the client turns by its own sensitivity ({turned:0.00} radians), and keeps its own HUD");

        // a frame that never arrives: the client stops, rather than play on out of step
        (host, client, hostLink, _) = Pair();
        hostLink.Drop = m => m.Type == "frame" && m.Tick == 30;
        for (int k = 0; k < 60; k++) { host.Update(new Input { Move = 1 }, Frame); client.Update(new Input(), Frame); }
        check(client.NetSession == null && client.OnlineNotice?.Contains("never arrived") == true, $"a missing frame is caught: \"{client.OnlineNotice}\"");

        // a drift is caught by the checksum
        (host, client, _, _) = Pair();
        for (int k = 0; k < 30; k++) { host.Update(new Input(), Frame); client.Update(new Input(), Frame); }
        Of(client, "b").State.X += 0.001f;
        for (int k = 0; k < 120 && client.NetSession != null; k++) { host.Update(new Input(), Frame); client.Update(new Input(), Frame); }
        check(client.NetSession == null && client.OnlineNotice?.Contains("Out of sync") == true, $"a copy that drifts finds out within a second: \"{client.OnlineNotice}\"");

        // a dropped connection ends the game with a message, not a frozen screen
        (host, client, _, clientLink) = Pair();
        clientLink.Lost = true;
        client.Update(new Input(), Frame);
        check(client.NetSession == null && client.OnlineNotice?.Contains("connection") == true && client.Menu.Page == MenuPage.Online, "a lost connection ends it, back on the Online page with why");

        // Esc: a menu, while the game goes on; leaving from it ends the game for everyone when it's the host
        (host, client, _, _) = Pair();
        host.Update(new Input { Pause = true }, Frame);
        int tick = host.NetSession.TickCount;
        for (int k = 0; k < 30; k++) { host.Update(new Input(), Frame); client.Update(new Input(), Frame); }
        check(host.Menu.Page == MenuPage.Pause && host.NetSession != null && host.NetSession.TickCount >= tick + 29 && host.Menu.Items(MenuPage.Pause).SequenceEqual(new[] { "Resume", "Leave game" }),
            "Esc opens a small menu (Resume, Leave game) while the game goes on");
        host.Menu.Cursor = 1;
        host.Update(new Input { Confirm = true }, Frame);
        client.Update(new Input(), Frame);
        check(host.NetSession == null && client.NetSession == null && client.OnlineNotice?.Contains("host ended") == true, "the host leaving ends it for everyone");

        // monsters don't walk through the player they're after
        (host, _, _, _) = Pair(prepHost: g => g.Vars.God = true);
        var pa = Of(host, "a").State;
        var ettin = new Monster(Monster.Ettin) { Level = host.Level, State = AiState.Chase };
        foreach (var (dx, dy) in new[] { (2f, 0f), (-2f, 0f), (0f, 2f), (0f, -2f) })
            if (!host.Level.BlocksCircle(pa.X + dx, pa.Y + dy, 0.5f)) { ettin.X = pa.X + dx; ettin.Y = pa.Y + dy; break; }
        host.Level.Things.Add(ettin);
        float closest = 99;
        for (int k = 0; k < 180; k++) { host.Update(new Input(), Frame); closest = MathF.Min(closest, Game.Dist(ettin.X, ettin.Y, pa.X, pa.Y)); }
        check(ettin.X != 0 && closest >= ettin.Radius + pa.Radius - 0.02f, $"a monster closes in on the player it's after, but stops at arm's length ({closest:0.00})");

        // the whole crew down: a few seconds later, everyone's back up
        (host, _, _, _) = Pair();
        foreach (var o in host.OnlinePlayers) { o.State.Dead = true; o.State.Health = 0; }
        host.Update(new Input(), Frame);
        bool down = host.Mode == GameMode.Dead;
        for (int k = 0; k < 60 * 5; k++) host.Update(new Input(), Frame);
        check(down && host.Mode == GameMode.Playing && host.OnlinePlayers.All(o => !o.State.Dead && o.State.Health > 0), "with the whole crew down, everyone's back up a few seconds later");

        // ---------------------------------------------------------------- the matchmaker's address

        var g = new Game { AchievementsOn = false };
        check(g.MatchmakerUrl == Game.DefaultMatchmakerUrl && Game.DefaultMatchmakerUrl == "https://matchmaker-tranquil-fire-5888.fly.dev/",
            "online play uses the public matchmaker unless you choose another");
        string bad = g.SetMatchmakerUrl("ftp://example.org");
        string ok = g.SetMatchmakerUrl("example.org:5080");
        check(bad != null && ok == null && g.MatchmakerUrl == "https://example.org:5080/" && Settings.Lines(g).Contains("matchmaker https://example.org:5080/"),
            "a host name (https assumed) sets it, and it's saved; something that isn't an http(s) address doesn't");
        g.Con.Execute("matchmaker default", quiet: true);
        check(g.MatchmakerUrl == Game.DefaultMatchmakerUrl && !Settings.Lines(g).Any(l => l.StartsWith("matchmaker")), "'matchmaker default' goes back to the public one");
        g.Menu.Show(MenuPage.Online);
        var online = g.Menu.Items(MenuPage.Online);
        g.Menu.Cursor = Array.FindIndex(online, i => i.StartsWith("Server: "));
        g.Menu.Update(new Input { Confirm = true }, Frame);
        bool editing = g.Menu.EditingServer;
        for (int k = 0; k < 80; k++) g.Menu.Update(new Input { Backspace = true }, Frame);
        g.Menu.Update(new Input { Typed = "http://127.0.0.1:5080" }, Frame);
        g.Menu.Update(new Input { Confirm = true }, Frame);
        check(online.Contains("Server: matchmaker-tranquil-fire-5888.fly.dev") && editing && !g.Menu.EditingServer && g.MatchmakerUrl == "http://127.0.0.1:5080/",
            "Online > Server: type another address (a local one, say) and Enter to use it");
        check(OnlineSession.NextMode("campaign", 1) == "soccer" && OnlineSession.NextMode("soccer", 1) == "campaign" && OnlineSession.ModeName("soccer") == "Rocket Soccer",
            "the host picks the game: Campaign co-op or Rocket Soccer");

        // ---------------------------------------------------------------- Rocket Soccer online: Blue against Red

        (host, client, _, _) = Pair(prepHost: h => h.OnlineMode = "soccer");
        void Run(int frames) { for (int k = 0; k < frames; k++) { host.Update(new Input(), Frame); client.Update(new Input(), Frame); } client.Update(new Input(), 0); }
        void Both(Action<Game> change) { change(host); change(client); } // (the same change on both copies, in step)
        a = Of(host, "a").State; b = Of(host, "b").State;
        check(client.OnSoccer && host.SoccerVersus && client.SoccerVersus && a.X < Soccer.SpotX && b.X > Soccer.SpotX && Of(client, "b").State.Weapons[0].Rocket
              && host.Level.Things.OfType<NetworkAvatar>().Select(v => v.Tint).Distinct().Count() == 2,
            "an online game can be Rocket Soccer: Blue kicks off from the west half and Red from the east, each with the launchers, in their team's colour");
        check(host.SoccerTarget(a) == 1 && host.SoccerTarget(b) == 0 && client.SoccerTarget(client.P) == 0, "Blue shoots for the east goal, Red for the west");
        Run(60);
        Both(g2 => { var ball = g2.Ball; ball.X = Soccer.SpotX + 4; ball.Y = Of(g2, "b").State.Y; ball.Z = 0; ball.Grounded = true; ball.VX = ball.VY = ball.VZ = 0; });
        Run(2);
        float ballFrom = host.Ball.X;
        client.Update(new Input { Fire = true }, Frame); host.Update(new Input(), Frame);
        client.Update(new Input(), Frame); host.Update(new Input(), Frame);
        Run(60);
        check(host.Ball.X < ballFrom - 1 && host.OnlineHash() == client.OnlineHash(), $"Red's rocket knocks the ball west, on both machines alike ({ballFrom:0.0} to {host.Ball.X:0.0})");
        Both(g2 => { var ball = g2.Ball; ball.X = Soccer.LineE - 2; ball.Y = Soccer.SpotY; ball.Z = 0; ball.Grounded = true; ball.VX = 6; ball.VY = ball.VZ = 0; });
        Run(40);
        check(host.TeamGoals[0] == 1 && client.TeamGoals[0] == 1 && host.TeamGoals[1] == 0 && host.OnlineHash() == client.OnlineHash(), "into the east goal: a goal for Blue, on both machines");
        var board = host.OnlineStatus();
        check(board.Status.StartsWith("Blue 1 - 0 Red, ") && board.Status.EndsWith(" left") && board.Scores.Single(sc => sc.PlayerId == "b").Team == 1
              && board.Scores.Single(sc => sc.PlayerId == "a").Team == 0,
            $"the who board sees the score and the teams (\"{board.Status}\")");
        // a blast shoves everyone near, but only hurts its own firer
        Both(g2 =>
        {
            var pb = Of(g2, "b").State;
            pb.X = 20; pb.Y = 20; pb.VX = pb.VY = 0; pb.Z = 0;
            var boom = new Projectile { Kind = ProjKind.Rocket, FromPlayer = true, Splash = Rockets.SplashRadius, DmgMin = 100, DmgMax = 120, X = 19.2f, Y = 20, Z = pb.FloorZ + 0.2f, Level = g2.Level, ByPlayer = Of(g2, "a").State };
            g2.Level.Things.Add(boom);
            typeof(Game).GetMethod("Explode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(g2, new object[] { boom, null, null });
        });
        var shoved = Of(host, "b").State;
        check(shoved.VX > 2 && shoved.Health == 100, $"Blue's blast beside a Red player shoves them ({shoved.VX:0.0} cells a second) without hurting them");
        Both(g2 => Of(g2, "b").State.Ammo[(int)AmmoKind.Rockets] = 0);
        Run(3);
        check(Of(host, "b").State.Ammo[(int)AmmoKind.Rockets] > 0 && Of(client, "b").State.Ammo[(int)AmmoKind.Rockets] > 0, "every player's rockets are topped up, not just the host's");
        Both(g2 => { g2.RunStarted = true; g2.SoccerLeft = 0.2f; });
        Run(30);
        check(host.TeamGoals[0] == 0 && host.Messages.Any(m => m.text.Contains("Blue wins")) && Of(host, "a").State.X < Soccer.SpotX && host.OnlineHash() == client.OnlineHash(),
            "at full time Blue's 1-0 win is called, and the teams line up for the next match");
    }
}
