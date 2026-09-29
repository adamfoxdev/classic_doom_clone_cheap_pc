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
    }
}
