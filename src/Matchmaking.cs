using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Collections.Concurrent;

namespace HexenSharp;

/// <summary>Anonymous lobby discovery against a self-hosted Hexen Sharp matchmaker.</summary>
public sealed class MatchmakerClient : IDisposable, INetLink
{
    readonly HttpClient _http;
    readonly string _version;
    readonly Channel<NetworkMessage> _send = Channel.CreateUnbounded<NetworkMessage>();
    readonly ConcurrentQueue<NetworkMessage> _received = new();
    static JsonSerializerOptions _json => OnlineSession.Json;
    CancellationTokenSource _socketStop;
    Task _work;
    float _refreshIn, _heartbeatIn;
    int _startReady;
    public IReadOnlyList<MatchRoom> Rooms { get; private set; } = Array.Empty<MatchRoom>();
    public MatchTicket Ticket { get; private set; }
    public string Status { get; private set; } = "Press Enter to find a game or host one.";
    public bool Busy { get; private set; }
    public bool InRoom => Ticket != null;
    /// <summary>The relay connection dropped while in a room (not by leaving it).</summary>
    public bool Lost { get; private set; }

    public MatchmakerClient(string endpoint, string version)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Matchmaker URL must be an absolute http or https URL.", nameof(endpoint));
        if (!uri.AbsoluteUri.EndsWith('/')) uri = new Uri(uri.AbsoluteUri + "/");
        _http = new HttpClient { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(5) };
        _version = version;
    }

    public void Refresh()
    {
        if (Busy || InRoom) return;
        _refreshIn = 5;
        Run(async () =>
        {
            Rooms = (await _http.GetFromJsonAsync<MatchRoom[]>("api/rooms") ?? Array.Empty<MatchRoom>())
                .Where(r => r.Version == _version && r.Players < r.Capacity && !r.Started).Take(6).ToArray();
            Status = Rooms.Count == 0 ? "No open games found. Host one to start." : $"Found {Rooms.Count} open game{(Rooms.Count == 1 ? "" : "s")}.";
        });
    }

    public void QuickMatch()
    {
        if (Busy || InRoom) return;
        Run(async () =>
        {
            Rooms = (await _http.GetFromJsonAsync<MatchRoom[]>("api/rooms") ?? Array.Empty<MatchRoom>())
                .Where(r => r.Version == _version && r.Players < r.Capacity && !r.Started).ToArray();
            var room = Rooms.FirstOrDefault(r => r.Version == _version && r.Players < r.Capacity);
            if (room == null) { Status = "No open games found. Host one to start."; return; }
            await JoinCore(room);
        });
    }

    public void Host(string name)
    {
        if (Busy || InRoom) return;
        Run(async () =>
        {
            using var response = await _http.PostAsJsonAsync("api/rooms", new CreateRoom(name, _version));
            response.EnsureSuccessStatusCode();
            Ticket = await response.Content.ReadFromJsonAsync<MatchTicket>();
            Joined(Ticket);
            Status = $"Room {Ticket.Room.Code.ToUpperInvariant()} created. Waiting for players.";
        });
    }

    public void Join(MatchRoom room)
    {
        if (Busy || InRoom) return;
        Run(() => JoinCore(room));
    }

    public void RequestStart()
    {
        if (Busy || Ticket is not { IsHost: true } ticket) return;
        Status = "Starting co-op session…";
        Run(async () =>
        {
            using var response = await _http.PostAsJsonAsync($"api/rooms/{Uri.EscapeDataString(ticket.Room.Id)}/start",
                new Heartbeat(ticket.PlayerId, ticket.Token));
            response.EnsureSuccessStatusCode();
            var room = await response.Content.ReadFromJsonAsync<MatchRoom>();
            if (room == null) throw new InvalidDataException("Matchmaker returned an empty room.");
            Ticket = ticket with { Room = room };
            Status = "Starting shared game…";
            Interlocked.Exchange(ref _startReady, 1);
        });
    }

    public bool TakeStartReady() => Interlocked.Exchange(ref _startReady, 0) != 0;

    async Task JoinCore(MatchRoom room)
    {
        using var response = await _http.PostAsJsonAsync($"api/rooms/{Uri.EscapeDataString(room.Id)}/join", new JoinRoom(Name(), _version));
        response.EnsureSuccessStatusCode();
        Ticket = await response.Content.ReadFromJsonAsync<MatchTicket>();
        Joined(Ticket);
        Status = $"Joined room {Ticket.Room.Code.ToUpperInvariant()}.";
    }

    void Joined(MatchTicket ticket)
    {
        _heartbeatIn = 0;
        Lost = false;
        while (_received.TryDequeue(out _)) { }
        _socketStop = new CancellationTokenSource();
        _ = SocketLoop(ticket, _socketStop.Token);
    }

    async Task SocketLoop(MatchTicket ticket, CancellationToken stop)
    {
        try
        {
            var baseUri = _http.BaseAddress;
            var builder = new UriBuilder(baseUri)
            {
                Scheme = baseUri.Scheme == "https" ? "wss" : "ws",
                Path = baseUri.AbsolutePath.TrimEnd('/') + $"/api/rooms/{Uri.EscapeDataString(ticket.Room.Id)}/connect",
                Query = "playerId=" + Uri.EscapeDataString(ticket.PlayerId),
            };
            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Authorization", "Bearer " + ticket.Token);
            await socket.ConnectAsync(builder.Uri, stop);
            Status = "Connected to lobby relay.";
            var sender = SendLoop(socket, stop);
            var buffer = new byte[16 * 1024];
            while (socket.State == WebSocketState.Open && !stop.IsCancellationRequested)
            {
                using var data = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, stop);
                    if (result.MessageType == WebSocketMessageType.Close) { if (!stop.IsCancellationRequested) Lost = true; return; }
                    data.Write(buffer, 0, result.Count);
                    if (data.Length > 64 * 1024) throw new InvalidDataException("Matchmaker message exceeded 64 KB.");
                } while (!result.EndOfMessage);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = JsonSerializer.Deserialize<NetworkMessage>(data.ToArray(), _json);
                    if (message != null) _received.Enqueue(message);
                }
            }
            if (!stop.IsCancellationRequested) Lost = true;
            await sender;
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (!stop.IsCancellationRequested) { Lost = true; Status = "Lobby connection lost: " + e.Message; } }
    }

    async Task SendLoop(ClientWebSocket socket, CancellationToken stop)
    {
        await foreach (var message in _send.Reader.ReadAllAsync(stop))
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, _json);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, stop);
        }
    }

    public void SendNetwork(NetworkMessage message)
    {
        if (InRoom) _send.Writer.TryWrite(message);
    }

    public bool TryReceiveNetwork(out NetworkMessage message) => _received.TryDequeue(out message);

    public void Leave()
    {
        if (Busy || Ticket == null) return;
        var ticket = Ticket;
        Ticket = null;
        _socketStop?.Cancel();
        _socketStop?.Dispose();
        _socketStop = null;
        Run(async () =>
        {
            using var response = await _http.PostAsJsonAsync($"api/rooms/{Uri.EscapeDataString(ticket.Room.Id)}/leave",
                new Heartbeat(ticket.PlayerId, ticket.Token));
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();
            Status = "You left the room.";
            Refresh();
        });
    }

    public void Tick(float dt)
    {
        if (_work is { IsCompleted: true }) { _work = null; Busy = false; }
        if (Busy) return;
        if (Ticket is { } ticket)
        {
            _heartbeatIn -= dt;
            if (_heartbeatIn <= 0)
            {
                _heartbeatIn = 2;
                Run(async () =>
                {
                    using var response = await _http.PostAsJsonAsync($"api/rooms/{Uri.EscapeDataString(ticket.Room.Id)}/heartbeat",
                        new Heartbeat(ticket.PlayerId, ticket.Token));
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Ticket = null;
                        Status = "Room expired. Refresh to find another game.";
                    }
                    else
                    {
                        response.EnsureSuccessStatusCode();
                        Ticket = ticket with { Room = await response.Content.ReadFromJsonAsync<MatchRoom>() };
                    }
                });
            }
        }
        else
        {
            _refreshIn -= dt;
            if (_refreshIn <= 0) Refresh();
        }
    }

    void Run(Func<Task> action)
    {
        if (Busy) return;
        Busy = true;
        _work = Work(action);
    }

    async Task Work(Func<Task> action)
    {
        try { await action(); }
        catch (Exception e) { Status = "Matchmaker unavailable: " + e.Message; }
        finally { Busy = false; }
    }

    static string Name() => "Player" + Random.Shared.Next(1000, 9999);
    public void Dispose()
    {
        _socketStop?.Cancel();
        _socketStop?.Dispose();
        _http.Dispose();
    }

    public sealed record MatchRoom(string Id, string Code, string Host, string Version, int Players, int Capacity, DateTimeOffset UpdatedAt, RoomMate[] Members, bool Started);
    public sealed record RoomMate(string PlayerId, string Name, bool IsHost);
    public sealed record MatchTicket(MatchRoom Room, string PlayerId, string Token, bool IsHost);
    sealed record CreateRoom(string Name, string Version);
    sealed record JoinRoom(string Name, string Version);
    sealed record Heartbeat(string PlayerId, string Token);
}

public sealed class NetworkInput
{
    public float Move, Strafe, Turn, LookX, LookY;
    public bool Fire, Walk, JumpHeld, SlideHeld, JetHeld, ZoomHeld, Use, UseItem, Place, Jump, Slide;
    public int Slot, Cycle;
    public static NetworkInput Capture(Input i) => new()
    {
        Move = i.Move, Strafe = i.Strafe, Turn = i.Turn, LookX = i.LookX, LookY = i.LookY,
        Fire = i.Fire, Walk = i.Walk, JumpHeld = i.JumpHeld, SlideHeld = i.SlideHeld, JetHeld = i.JetHeld, ZoomHeld = i.ZoomHeld,
        Use = i.Use, UseItem = i.UseItem, Place = i.Place, Jump = i.Jump, Slide = i.Slide, Slot = i.Slot, Cycle = i.Cycle,
    };
    public Input ToInput() => new()
    {
        Move = Move, Strafe = Strafe, Turn = Turn, LookX = LookX, LookY = LookY,
        Fire = Fire, Walk = Walk, JumpHeld = JumpHeld, SlideHeld = SlideHeld, JetHeld = JetHeld, ZoomHeld = ZoomHeld,
        Use = Use, UseItem = UseItem, Place = Place, Jump = Jump, Slide = Slide, Slot = Slot, Cycle = Cycle,
    };
    public NetworkInput Copy() => (NetworkInput)MemberwiseClone();
}

public sealed class NetworkPlayerInput
{
    public string PlayerId { get; set; }
    public NetworkInput Input { get; set; }
}

public sealed class NetworkMessage
{
    public string Type { get; set; }
    public string PlayerId { get; set; }
    public int Seed { get; set; }
    public int Tick { get; set; }
    public string[] Players { get; set; }
    public NetworkPlayerInput[] Inputs { get; set; }
    public NetworkInput Input { get; set; }
    public GameVars Settings { get; set; }
    public GameStyle Style { get; set; }
    /// <summary>On every OnlineSession.HashEvery'th frame: the host's checksum of the game after it (see Game.OnlineHash).</summary>
    public long? Hash { get; set; }
}
