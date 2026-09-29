using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RoomDirectory>();
var app = builder.Build();
app.UseWebSockets();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/rooms", (RoomDirectory rooms) => Results.Ok(rooms.List()));
app.MapGet("/api/rooms/{id}", (string id, RoomDirectory rooms) => rooms.Get(id) is { } room ? Results.Ok(room) : Results.NotFound());
app.MapPost("/api/rooms", (CreateRoomRequest request, RoomDirectory rooms) =>
{
    if (!ValidName(request.Name)) return Results.BadRequest(new { error = "Name must be 1–20 characters." });
    var room = rooms.Create(request.Name, request.Version);
    return Results.Created($"/api/rooms/{room.Room.Id}", room);
});
app.MapPost("/api/rooms/{id}/join", (string id, JoinRoomRequest request, RoomDirectory rooms) =>
{
    if (!ValidName(request.Name)) return Results.BadRequest(new { error = "Name must be 1–20 characters." });
    var result = rooms.Join(id, request.Name, request.Version);
    return result.Error switch
    {
        "missing" => Results.NotFound(new { error = "Room no longer exists." }),
        "full" => Results.Conflict(new { error = "Room is full." }),
        "version" => Results.Conflict(new { error = "Game versions do not match." }),
        "started" => Results.Conflict(new { error = "That game has already started." }),
        _ => Results.Ok(result.Value),
    };
});
app.MapPost("/api/rooms/{id}/heartbeat", (string id, PlayerRequest request, RoomDirectory rooms) =>
    rooms.Heartbeat(id, request.PlayerId, request.Token) is { } room ? Results.Ok(room) : Results.NotFound());
app.MapPost("/api/rooms/{id}/leave", (string id, PlayerRequest request, RoomDirectory rooms) =>
    rooms.Leave(id, request.PlayerId, request.Token) ? Results.NoContent() : Results.NotFound());
app.MapPost("/api/rooms/{id}/start", (string id, PlayerRequest request, RoomDirectory rooms) =>
    rooms.Start(id, request.PlayerId, request.Token) is { } room ? Results.Ok(room) : Results.Conflict(new { error = "Only the room host can start an open room." }));
app.Map("/api/rooms/{id}/connect", async (HttpContext context, string id, RoomDirectory rooms) =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    string playerId = context.Request.Query["playerId"].ToString();
    string auth = context.Request.Headers.Authorization.ToString();
    string token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth[7..] : "";
    var channel = rooms.Attach(id, playerId, token);
    if (channel == null) { context.Response.StatusCode = 404; return; }
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    var sender = SendLoop(socket, channel.Reader, stop.Token);
    var receiver = ReceiveLoop(socket, id, playerId, rooms, stop.Token);
    await Task.WhenAny(sender, receiver);
    stop.Cancel();
    try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None); } catch { }
    rooms.Detach(id, playerId, channel);
});

app.Run();

static async Task SendLoop(WebSocket socket, ChannelReader<string> messages, CancellationToken stop)
{
    await foreach (var message in messages.ReadAllAsync(stop))
    {
        if (socket.State != WebSocketState.Open) break;
        await socket.SendAsync(System.Text.Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, stop);
    }
}

static async Task ReceiveLoop(WebSocket socket, string roomId, string playerId, RoomDirectory rooms, CancellationToken stop)
{
    var buffer = new byte[16 * 1024];
    while (socket.State == WebSocketState.Open && !stop.IsCancellationRequested)
    {
        using var data = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, stop);
            if (result.MessageType == WebSocketMessageType.Close) return;
            data.Write(buffer, 0, result.Count);
            if (data.Length > 64 * 1024) return;
        } while (!result.EndOfMessage);
        if (result.MessageType == WebSocketMessageType.Text)
            rooms.Relay(roomId, playerId, System.Text.Encoding.UTF8.GetString(data.ToArray()));
    }
}

static bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 20;

sealed record CreateRoomRequest(string Name, string Version);
sealed record JoinRoomRequest(string Name, string Version);
sealed record PlayerRequest(string PlayerId, string Token);
sealed record RoomMember(string PlayerId, string Name, bool IsHost);
sealed record RoomSummary(string Id, string Code, string Host, string Version, int Players, int Capacity, DateTimeOffset UpdatedAt, RoomMember[] Members, bool Started);
sealed record PlayerTicket(RoomSummary Room, string PlayerId, string Token, bool IsHost);

sealed class RoomDirectory
{
    const int Capacity = 4;
    static readonly TimeSpan Expiry = TimeSpan.FromSeconds(45);
    readonly ConcurrentDictionary<string, Room> _rooms = new();

    public IReadOnlyList<RoomSummary> List()
    {
        Prune();
        return _rooms.Values.Select(Summary).OrderBy(r => r.UpdatedAt).ToArray();
    }

    public RoomSummary? Get(string id) => _rooms.TryGetValue(id, out var room) ? Summary(room) : null;

    public PlayerTicket Create(string name, string version)
    {
        Prune();
        string id;
        do id = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant(); while (_rooms.ContainsKey(id));
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();
        var room = new Room(id, code, version ?? "dev");
        var player = room.Add(name.Trim(), host: true);
        _rooms[id] = room;
        return new PlayerTicket(Summary(room), player.Id, player.Token, true);
    }

    public (string? Error, PlayerTicket? Value) Join(string id, string name, string version)
    {
        Prune();
        if (!_rooms.TryGetValue(id, out var room)) return ("missing", null);
        lock (room.Gate)
        {
            if (!string.Equals(room.Version, version ?? "dev", StringComparison.Ordinal)) return ("version", null);
            if (room.Started) return ("started", null);
            if (room.Players.Count >= Capacity) return ("full", null);
            var player = room.Add(name.Trim(), host: false);
            return (null, new PlayerTicket(Summary(room), player.Id, player.Token, false));
        }
    }

    public Channel<string>? Attach(string id, string playerId, string token)
    {
        if (!_rooms.TryGetValue(id, out var room)) return null;
        lock (room.Gate)
        {
            if (!room.Players.TryGetValue(playerId, out var player) || !TokenMatches(player.Token, token)) return null;
            player.Outgoing?.Writer.TryComplete();
            player.Outgoing = Channel.CreateBounded<string>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false });
            if (room.StartMessage != null) player.Outgoing.Writer.TryWrite(room.StartMessage);
            return player.Outgoing;
        }
    }

    public void Detach(string id, string playerId, Channel<string> channel)
    {
        if (!_rooms.TryGetValue(id, out var room)) return;
        lock (room.Gate)
            if (room.Players.TryGetValue(playerId, out var player) && ReferenceEquals(player.Outgoing, channel))
            {
                player.Outgoing = null; channel.Writer.TryComplete();
                if (player.IsHost)
                {
                    foreach (var other in room.Players.Values)
                        if (!other.IsHost) other.Outgoing?.Writer.TryWrite("{\"type\":\"stop\"}");
                }
                else if (room.Players.TryGetValue(room.HostId, out var host))
                    host.Outgoing?.Writer.TryWrite(JsonSerializer.Serialize(new { type = "peer-left", playerId }));
            }
    }

    public void Relay(string id, string senderId, string json)
    {
        if (!_rooms.TryGetValue(id, out var room)) return;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); } catch { return; }
        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("type", out var typeElement)) return;
            string type = typeElement.GetString() ?? "";
            lock (room.Gate)
            {
                if (!room.Players.TryGetValue(senderId, out var sender) || sender.Outgoing == null) return;
                if (type == "input" && room.StartMessage != null && !sender.IsHost && room.Players.TryGetValue(room.HostId, out var host))
                {
                    var relay = JsonSerializer.Serialize(new { type = "input", playerId = senderId, input = doc.RootElement.GetProperty("input") });
                    host.Outgoing?.Writer.TryWrite(relay);
                }
                else if (sender.IsHost && type == "start" && room.Started && room.StartMessage == null)
                {
                    room.StartMessage = json;
                    foreach (var player in room.Players.Values)
                        if (!player.IsHost) player.Outgoing?.Writer.TryWrite(json);
                }
                else if (sender.IsHost && type == "frame" && room.Started)
                {
                    foreach (var player in room.Players.Values)
                        if (!player.IsHost) player.Outgoing?.Writer.TryWrite(json);
                }
                else if (sender.IsHost && type == "stop")
                {
                    foreach (var player in room.Players.Values)
                        if (!player.IsHost) player.Outgoing?.Writer.TryWrite(json);
                }
            }
        }
    }

    public RoomSummary? Heartbeat(string id, string playerId, string token)
    {
        if (!_rooms.TryGetValue(id, out var room)) return null;
        lock (room.Gate)
        {
            if (!room.Players.TryGetValue(playerId, out var player) || !TokenMatches(player.Token, token)) return null;
            player.Seen = DateTimeOffset.UtcNow;
            return Summary(room);
        }
    }

    public RoomSummary? Start(string id, string playerId, string token)
    {
        if (!_rooms.TryGetValue(id, out var room)) return null;
        lock (room.Gate)
        {
            if (!room.Players.TryGetValue(playerId, out var player) || !player.IsHost || !TokenMatches(player.Token, token) || room.Started) return null;
            room.Started = true;
            return Summary(room);
        }
    }

    public bool Leave(string id, string playerId, string token)
    {
        if (!_rooms.TryGetValue(id, out var room)) return false;
        lock (room.Gate)
        {
            if (!room.Players.TryGetValue(playerId, out var player) || !TokenMatches(player.Token, token)) return false;
            if (player.IsHost)
            {
                foreach (var other in room.Players.Values)
                    if (!other.IsHost) other.Outgoing?.Writer.TryWrite("{\"type\":\"stop\"}");
            }
            else if (room.Players.TryGetValue(room.HostId, out var host))
                host.Outgoing?.Writer.TryWrite(JsonSerializer.Serialize(new { type = "peer-left", playerId }));
            room.Players.TryRemove(playerId, out _);
            if (room.Players.IsEmpty) _rooms.TryRemove(id, out _);
            return true;
        }
    }

    void Prune()
    {
        var cutoff = DateTimeOffset.UtcNow - Expiry;
        foreach (var (id, room) in _rooms)
        {
            lock (room.Gate)
            {
                foreach (var (playerId, player) in room.Players)
                    if (player.Seen < cutoff) room.Players.TryRemove(playerId, out _);
                if (room.Players.IsEmpty) _rooms.TryRemove(id, out _);
            }
        }
    }

    static RoomSummary Summary(Room room)
    {
        lock (room.Gate)
            return new RoomSummary(room.Id, room.Code, room.Players.TryGetValue(room.HostId, out var host) ? host.Name : "", room.Version,
                room.Players.Count, Capacity, room.Players.Values.Select(p => p.Seen).DefaultIfEmpty(DateTimeOffset.UtcNow).Max(),
                room.Players.Values.OrderBy(p => p.Joined).Select(p => new RoomMember(p.Id, p.Name, p.IsHost)).ToArray(), room.Started);
    }

    static bool TokenMatches(string expected, string actual)
    {
        if (string.IsNullOrEmpty(actual) || expected.Length != actual.Length) return false;
        return CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(actual));
    }

    sealed class Room(string id, string code, string version)
    {
        public readonly object Gate = new();
        public readonly string Id = id, Code = code, Version = version;
        public string HostId = "";
        public bool Started;
        public string? StartMessage;
        public readonly ConcurrentDictionary<string, Member> Players = new();
        public Member Add(string name, bool host)
        {
            var member = new Member(Guid.NewGuid().ToString("N"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)), name, DateTimeOffset.UtcNow, host);
            Players[member.Id] = member;
            if (host) HostId = member.Id;
            return member;
        }
    }
    sealed class Member(string id, string token, string name, DateTimeOffset seen, bool host)
    {
        public readonly string Id = id, Token = token, Name = name;
        public readonly bool IsHost = host;
        public readonly DateTimeOffset Joined = seen;
        public DateTimeOffset Seen = seen;
        public Channel<string>? Outgoing;
    }
}
