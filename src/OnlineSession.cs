using System.Reflection;

namespace HexenSharp;

public sealed class OnlinePlayer
{
    public readonly string Id;
    public readonly Player State;
    public NetworkAvatar Avatar;
    public OnlinePlayer(string id, Player state) { Id = id; State = state; }
}

/// <summary>
/// Host-led deterministic input relay. Every peer advances the same seeded game once per host tick with the same
/// ordered player inputs, so doors, monsters, projectiles and pickups are shared simulation state.
/// </summary>
public sealed class OnlineSession
{
    public const float Step = 1f / 60f;
    readonly string[] _players;
    readonly string _localId;
    readonly bool _host;
    readonly Dictionary<string, InputBuffer> _inputs = new();
    float _accumulator;
    int _tick;

    OnlineSession(string[] players, string localId, bool host)
    {
        _players = players;
        _localId = localId;
        _host = host;
        foreach (string id in players) _inputs[id] = new InputBuffer();
    }

    public static void Host(Game game)
    {
        var ticket = game.Matchmaker?.Ticket;
        if (ticket == null || !ticket.IsHost) return;
        string[] players = (ticket.Room.Members ?? Array.Empty<MatchmakerClient.RoomMate>())
            .OrderBy(p => p.IsHost ? 0 : 1).ThenBy(p => p.PlayerId, StringComparer.Ordinal)
            .Select(p => p.PlayerId).ToArray();
        if (players.Length == 0 || !players.Contains(ticket.PlayerId)) players = new[] { ticket.PlayerId };
        int seed = Random.Shared.Next(1, int.MaxValue);
        var settings = CopySettings(game.Vars);
        game.BeginOnlineGame(seed, players, ticket.PlayerId, settings, game.Style);
        game.NetSession = new OnlineSession(players, ticket.PlayerId, true);
        game.Matchmaker.SendNetwork(new NetworkMessage { Type = "start", Seed = seed, Players = players, Settings = settings, Style = game.Style });
    }

    public static void Client(Game game, NetworkMessage start)
    {
        var ticket = game.Matchmaker?.Ticket;
        if (ticket == null || ticket.IsHost || start.Players == null || !start.Players.Contains(ticket.PlayerId)) return;
        game.BeginOnlineGame(start.Seed, start.Players, ticket.PlayerId, start.Settings ?? new GameVars(), start.Style);
        game.NetSession = new OnlineSession(start.Players, ticket.PlayerId, false);
    }

    public void Tick(Game game, Input localInput, float dt)
    {
        if (localInput.Pause)
        {
            if (_host) game.Matchmaker.SendNetwork(new NetworkMessage { Type = "stop" });
            game.EndOnlineGame();
            return;
        }

        if (!_host)
        {
            game.Matchmaker.SendNetwork(new NetworkMessage { Type = "input", PlayerId = _localId, Input = NetworkInput.Capture(localInput) });
            while (game.Matchmaker.TryReceiveNetwork(out var message))
            {
                if (message.Type == "stop") { game.EndOnlineGame(); return; }
                if (message.Type != "frame" || message.Tick < _tick || message.Inputs == null) continue;
                _tick = message.Tick + 1;
                game.StepOnline(message.Inputs, Step, _localId);
            }
            return;
        }

        while (game.Matchmaker.TryReceiveNetwork(out var message))
        {
            if (message.Type == "stop") { game.EndOnlineGame(); return; }
            if (message.Type == "input" && message.PlayerId != _localId && message.Input != null && _inputs.TryGetValue(message.PlayerId, out var buffer))
                buffer.Push(message.Input);
            else if (message.Type == "peer-left" && _inputs.TryGetValue(message.PlayerId, out var departed))
                departed.Reset();
        }
        _inputs[_localId].Push(NetworkInput.Capture(localInput));
        _accumulator = MathF.Min(_accumulator + MathF.Min(dt, 0.1f), 0.2f);
        int steps = 0;
        while (_accumulator >= Step && steps++ < 6)
        {
            var frame = _players.Select(id => new NetworkPlayerInput { PlayerId = id, Input = _inputs[id].Pop() }).ToArray();
            game.StepOnline(frame, Step, _localId);
            game.Matchmaker.SendNetwork(new NetworkMessage { Type = "frame", Tick = _tick++, Inputs = frame });
            _accumulator -= Step;
        }
    }

    public Player NearestTarget(Game game, Monster monster)
    {
        return game.OnlinePlayers.Where(p => !p.State.Dead)
            .OrderBy(p => Game.Dist(monster.X, monster.Y, p.State.X, p.State.Y)).Select(p => p.State).FirstOrDefault()
            ?? game.OnlinePlayers.FirstOrDefault()?.State ?? game.P;
    }

    static GameVars CopySettings(GameVars from)
    {
        var copy = new GameVars();
        CopyFields(from, copy);
        return copy;
    }

    public static void CopyFields(GameVars from, GameVars to)
    {
        foreach (var field in typeof(GameVars).GetFields(BindingFlags.Instance | BindingFlags.Public))
            field.SetValue(to, field.GetValue(from));
    }

    sealed class InputBuffer
    {
        NetworkInput _held = new();
        bool _use, _useItem, _place, _jump, _slide;
        int _slot, _cycle;
        public void Push(NetworkInput input)
        {
            _held = input.Copy();
            _use |= input.Use; _useItem |= input.UseItem; _place |= input.Place; _jump |= input.Jump; _slide |= input.Slide;
            if (input.Slot != 0) _slot = input.Slot;
            if (input.Cycle != 0) _cycle = input.Cycle;
        }
        public NetworkInput Pop()
        {
            var value = _held.Copy();
            value.Use = _use; value.UseItem = _useItem; value.Place = _place; value.Jump = _jump; value.Slide = _slide;
            value.Slot = _slot; value.Cycle = _cycle;
            _use = _useItem = _place = _jump = _slide = false; _slot = _cycle = 0;
            return value;
        }
        public void Reset()
        {
            _held = new NetworkInput();
            _use = _useItem = _place = _jump = _slide = false;
            _slot = _cycle = 0;
        }
    }
}
