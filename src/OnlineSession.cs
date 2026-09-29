using System.Reflection;
using System.Text.Json;

namespace HexenSharp;

public sealed class OnlinePlayer
{
    public readonly string Id;
    public readonly Player State;
    public NetworkAvatar Avatar;
    /// <summary>The game's per-player odds and ends (zoom, the rod's trigger, lift and regen timers), kept while others move.</summary>
    public readonly PlayerLocals Locals = new();
    public OnlinePlayer(string id, Player state) { Id = id; State = state; }
}

/// <summary>What the game keeps in single fields for "the" player, held per player in co-op and swapped in for each one's turn.</summary>
public sealed class PlayerLocals
{
    public float Zoom, Unhurt, Regen;
    public bool RodFireWas, OnLift;
    public int InMove, InStrafe;
}

/// <summary>
/// How a session's messages travel: the matchmaker's relay in play, an in-memory pair in the checks. Messages must
/// arrive in order and none may be lost; Lost says the connection's gone.
/// </summary>
public interface INetLink
{
    void SendNetwork(NetworkMessage message);
    bool TryReceiveNetwork(out NetworkMessage message);
    bool Lost { get; }
}

/// <summary>
/// Host-led deterministic input relay. Every peer advances the same seeded game once per host tick with the same
/// ordered player inputs, so doors, monsters, projectiles and pickups are shared simulation state. A frame must never
/// be skipped (a client that sees a gap in the ticks stops, rather than play on out of step), and once a second the
/// host sends a checksum of the game so a client that has drifted (different maths on another OS, say) finds out at once.
/// </summary>
public sealed class OnlineSession
{
    public const float Step = 1f / 60f;
    /// <summary>A checksum rides on every HashEvery'th frame.</summary>
    public const int HashEvery = 60;
    /// <summary>The host tells the matchmaker how the game's going every StatusEvery ticks (two seconds).</summary>
    public const int StatusEvery = 120;
    readonly string[] _players;
    readonly string _localId;
    readonly bool _host;
    readonly INetLink _link;
    readonly Dictionary<string, InputBuffer> _inputs = new();
    float _accumulator;
    int _tick;

    /// <summary>The next tick to be stepped.</summary>
    public int TickCount => _tick;
    public bool IsHost => _host;

    OnlineSession(INetLink link, string[] players, string localId, bool host)
    {
        _link = link;
        _players = players;
        _localId = localId;
        _host = host;
        foreach (string id in players) _inputs[id] = new InputBuffer();
    }

    /// <summary>The host starts the room's game (the matchmaker has marked it started): the players in order, a seed, the rules.</summary>
    public static void Host(Game game)
    {
        var ticket = game.Matchmaker?.Ticket;
        if (ticket == null || !ticket.IsHost) return;
        string[] players = (ticket.Room.Members ?? Array.Empty<MatchmakerClient.RoomMate>())
            .OrderBy(p => p.IsHost ? 0 : 1).ThenBy(p => p.PlayerId, StringComparer.Ordinal)
            .Select(p => p.PlayerId).ToArray();
        if (players.Length == 0 || !players.Contains(ticket.PlayerId)) players = new[] { ticket.PlayerId };
        StartHost(game, game.Matchmaker, players, ticket.PlayerId, Random.Shared.Next(1, int.MaxValue));
    }

    public static void StartHost(Game game, INetLink link, string[] players, string localId, int seed)
    {
        var settings = CopySettings(game.Vars);
        string mode = ModeName(game.OnlineMode) != null ? game.OnlineMode : Modes[0].Id;
        game.BeginOnlineGame(seed, players, localId, settings, game.Style, mode);
        game.NetSession = new OnlineSession(link, players, localId, true);
        link.SendNetwork(new NetworkMessage { Type = "start", Seed = seed, Players = players, Settings = settings, Style = game.Style, Mode = mode });
    }

    /// <summary>What an online game can be: the campaign together, or Rocket Soccer, team against team.</summary>
    public static readonly (string Id, string Name)[] Modes = { ("campaign", "Campaign co-op"), ("soccer", "Rocket Soccer") };
    public static string ModeName(string id) => Modes.FirstOrDefault(m => m.Id == id).Name;
    public static string NextMode(string id, int dir)
    {
        int i = Array.FindIndex(Modes, m => m.Id == id);
        return Modes[((i < 0 ? 0 : i) + dir + Modes.Length) % Modes.Length].Id;
    }

    /// <summary>A client joins the game its host started.</summary>
    public static void Client(Game game, NetworkMessage start)
    {
        var link = game.NetLink;
        string id = game.NetPlayerId;
        if (link == null || id == null || game.Matchmaker?.Ticket is { IsHost: true } || start.Players == null || !start.Players.Contains(id)) return;
        game.BeginOnlineGame(start.Seed, start.Players, id, start.Settings ?? new GameVars(), start.Style, start.Mode);
        game.NetSession = new OnlineSession(link, start.Players, id, false);
    }

    /// <summary>Leaving: the host tells everyone the game's over.</summary>
    public void Leave(Game game)
    {
        if (_host) _link.SendNetwork(new NetworkMessage { Type = "stop" });
        game.EndOnlineGame();
    }

    public void Tick(Game game, Input localInput, float dt)
    {
        if (_link.Lost) { game.EndOnlineGame("Lost the connection to the game."); return; }
        var mine = game.LocalNetInput(localInput);

        if (!_host)
        {
            _link.SendNetwork(new NetworkMessage { Type = "input", PlayerId = _localId, Input = mine });
            while (_link.TryReceiveNetwork(out var message))
            {
                if (message.Type == "stop") { game.EndOnlineGame("The host ended the game."); return; }
                if (message.Type == "chat") { game.HearChat(message); continue; }
                if (message.Type != "frame" || message.Inputs == null || message.Tick < _tick) continue; // (not a frame, or one already stepped)
                if (message.Tick > _tick)
                {
                    game.EndOnlineGame($"Lost sync with the host: frames {_tick} to {message.Tick - 1} never arrived.");
                    return;
                }
                _tick++;
                game.StepOnline(message.Inputs, Step, _localId);
                if (message.Hash is long hash && hash != game.OnlineHash())
                {
                    game.EndOnlineGame($"Out of sync with the host at tick {message.Tick}. (Are you on the same game version and OS?)");
                    return;
                }
            }
            return;
        }

        while (_link.TryReceiveNetwork(out var message))
        {
            if (message.Type == "stop") { game.EndOnlineGame(); return; }
            if (message.Type == "chat") game.HearChat(message);
            else if (message.Type == "input" && message.PlayerId != _localId && message.Input != null && _inputs.TryGetValue(message.PlayerId, out var buffer))
                buffer.Push(message.Input);
            else if (message.Type == "peer-left" && message.PlayerId != null && _inputs.TryGetValue(message.PlayerId, out var departed))
            {
                departed.Reset();
                game.Say("A crewmate left the game.");
            }
        }
        _inputs[_localId].Push(mine);
        _accumulator = MathF.Min(_accumulator + MathF.Min(dt, 0.1f), 0.2f);
        int steps = 0;
        while (_accumulator >= Step && steps++ < 6)
        {
            var frame = _players.Select(id => new NetworkPlayerInput { PlayerId = id, Input = _inputs[id].Pop() }).ToArray();
            game.StepOnline(frame, Step, _localId);
            long? hash = _tick % HashEvery == 0 ? game.OnlineHash() : null;
            _link.SendNetwork(new NetworkMessage { Type = "frame", Tick = _tick++, Inputs = frame, Hash = hash });
            // now and then, how it's going, for the matchmaker's who board (it keeps it; nobody else is sent it)
            if (_tick % StatusEvery == 1) _link.SendNetwork(game.OnlineStatus());
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

    /// <summary>
    /// The settings that only change what you see and hear, never the game itself, so each player keeps their own:
    /// fog, HUD, crosshair, the strafe helper, shake, damage numbers, music, the frame counter, gamepad look speed.
    /// Everything else (sensitivity included: see LocalNetInput) is the host's, so every copy of the game runs alike.
    /// </summary>
    public static readonly string[] LocalSettings =
    {
        nameof(GameVars.Fog), nameof(GameVars.Ghost), nameof(GameVars.StrafeHelp), nameof(GameVars.Shake), nameof(GameVars.DamageNumbers),
        nameof(GameVars.DynamicMusic), nameof(GameVars.Music), nameof(GameVars.PadLook), nameof(GameVars.ShowFps), nameof(GameVars.Hud),
        nameof(GameVars.Crosshair), nameof(GameVars.FullBright),
    };

    public static void CopyLocalSettings(GameVars from, GameVars to)
    {
        foreach (string name in LocalSettings)
        {
            var field = typeof(GameVars).GetField(name)!;
            field.SetValue(to, field.GetValue(from));
        }
    }

    /// <summary>
    /// A player's inputs between two ticks, merged: held controls as they were last, presses kept until the tick that
    /// takes them, and mouse movement added up (so none is lost when frames outnumber ticks, nor counted twice when
    /// ticks outnumber frames).
    /// </summary>
    public sealed class InputBuffer
    {
        NetworkInput _held = new();
        bool _use, _useItem, _place, _jump, _slide;
        int _slot, _cycle;
        float _lookX, _lookY;
        public void Push(NetworkInput input)
        {
            _held = input.Copy();
            _use |= input.Use; _useItem |= input.UseItem; _place |= input.Place; _jump |= input.Jump; _slide |= input.Slide;
            if (input.Slot != 0) _slot = input.Slot;
            if (input.Cycle != 0) _cycle = input.Cycle;
            _lookX += input.LookX; _lookY += input.LookY;
        }
        public NetworkInput Pop()
        {
            var value = _held.Copy();
            value.Use = _use; value.UseItem = _useItem; value.Place = _place; value.Jump = _jump; value.Slide = _slide;
            value.Slot = _slot; value.Cycle = _cycle;
            value.LookX = _lookX; value.LookY = _lookY;
            _use = _useItem = _place = _jump = _slide = false; _slot = _cycle = 0;
            _lookX = _lookY = 0;
            return value;
        }
        public void Reset()
        {
            _held = new NetworkInput();
            _use = _useItem = _place = _jump = _slide = false;
            _slot = _cycle = 0;
            _lookX = _lookY = 0;
        }
    }

    /// <summary>The wire format: web JSON, fields included (the checks round-trip through it too).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { IncludeFields = true };
}
