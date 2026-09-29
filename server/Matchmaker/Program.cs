using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RoomDirectory>();
var app = builder.Build();
// the who board (wwwroot/index.html) at the root: who's online, what they're playing, and the server's stats
app.UseDefaultFiles();
app.UseStaticFiles();
// a ping every 20 s keeps a lobby's quiet connection from being closed as idle by a proxy (Fly.io's, say)
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/rooms", (RoomDirectory rooms) => Results.Ok(rooms.List()));
app.MapGet("/api/board", (RoomDirectory rooms) => Results.Ok(rooms.Board()));
// the who board talks to the players: to one room, or to every room ("all")
app.MapPost("/api/rooms/{id}/say", (string id, SayRequest request, HttpContext context, RoomDirectory rooms) =>
{
    // behind Fly's proxy the caller's address is in Fly-Client-IP; elsewhere, the connection's
    string caller = context.Request.Headers["Fly-Client-IP"].ToString() is { Length: > 0 } fly ? fly : context.Connection.RemoteIpAddress?.ToString() ?? "?";
    return rooms.Say(id, request.Name, request.Text, caller) switch
    {
        "name" => Results.BadRequest(new { error = "Name must be 1–16 characters." }),
        "text" => Results.BadRequest(new { error = $"Say something (up to {RoomDirectory.ChatMax} characters)." }),
        "missing" => Results.NotFound(new { error = "That room has gone." }),
        "slow" => Results.StatusCode(StatusCodes.Status429TooManyRequests),
        _ => Results.Accepted(),
    };
});
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
sealed record SayRequest(string Name, string Text);
sealed record RoomMember(string PlayerId, string Name, bool IsHost);
sealed record RoomSummary(string Id, string Code, string Host, string Version, int Players, int Capacity, DateTimeOffset UpdatedAt, RoomMember[] Members, bool Started);
sealed record PlayerTicket(RoomSummary Room, string PlayerId, string Token, bool IsHost);

/// <summary>
/// The who board: public, so names and games only, never a player's id or token. Players online counts everyone in a
/// room; History is players online at each minute since the server started (the last RoomDirectory.Minutes of them), oldest first,
/// ending with now.
/// </summary>
sealed record Board(DateTimeOffset Now, DateTimeOffset ServerStarted, int PlayersOnline, int RoomsOpen, int GamesPlaying,
    BoardTotals Totals, int[] History, BoardRoom[] Rooms, BoardGame[] Recent, BoardChat[] Chat);
/// <summary>A line of chat: Room is whose game it was said in ("Sam's game"), or "everyone" for the board's to all rooms.</summary>
sealed record BoardChat(DateTimeOffset When, string Room, string Name, string Text, string From);
sealed record BoardTotals(long RoomsCreated, long PlayersJoined, long GamesStarted, int PeakPlayers, long FramesRelayed, long BytesRelayed,
    double LongestGameSeconds, IReadOnlyDictionary<string, long> GamesByMode);
/// <summary>A room: State is lobby, playing, or over (the host ended it and the rest haven't left yet).</summary>
sealed record BoardRoom(string Id, string Host, string State, string? Mode, string? ModeName, string Version, int Players, int Capacity,
    DateTimeOffset Opened, double? PlayingSeconds, string? Status, BoardPlayer[] Members);
/// <summary>A player: Kills, Health, Dead and Team come from the host's status (newer games send one every couple of seconds).</summary>
sealed record BoardPlayer(string Name, bool IsHost, bool Connected, int? Kills, int? Health, bool? Dead, int? Team);
sealed record BoardGame(string Host, string[] Players, string? Mode, string? ModeName, DateTimeOffset Started, double Seconds, string? Status);

sealed class RoomDirectory
{
    const int Capacity = 4;
    static readonly TimeSpan Expiry = TimeSpan.FromSeconds(45);
    readonly ConcurrentDictionary<string, Room> _rooms = new();
    readonly DateTimeOffset _started = DateTimeOffset.UtcNow;
    readonly object _statsGate = new();
    readonly Dictionary<string, long> _gamesByMode = new();
    readonly Queue<BoardGame> _recent = new();
    readonly Queue<BoardChat> _chat = new();
    readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _sayers = new();
    readonly int[] _history = new int[Minutes];
    long _roomsCreated, _playersJoined, _gamesStarted, _framesRelayed, _bytesRelayed;
    int _peakPlayers, _historyAt, _samples;
    double _longestGame;
    readonly Timer _sampler;
    /// <summary>How many minutes of players-online the board's chart covers, and how many finished games it lists.</summary>
    public const int Minutes = 120, RecentGames = 20;
    /// <summary>Chat: the longest line, and how many lines the board keeps.</summary>
    public const int ChatMax = 100, ChatKept = 50;

    /// <summary>
    /// Someone on the who board says something, to one room or (id "all") to every room. One line every 3 seconds and
    /// 10 a minute from each address; it goes out marked as from the web, so nobody can pass for a player.
    /// Returns an error ("name", "text", "missing", "slow"), or null when it's sent.
    /// </summary>
    public string? Say(string id, string? name, string? text, string caller)
    {
        name = Clip(name, 16);
        text = Clip(text, ChatMax);
        if (string.IsNullOrEmpty(name)) return "name";
        if (string.IsNullOrEmpty(text)) return "text";
        Prune();
        var targets = id == "all" ? _rooms.Values.ToArray() : _rooms.TryGetValue(id, out var one) ? new[] { one } : Array.Empty<Room>();
        if (targets.Length == 0 && id != "all") return "missing";
        var now = DateTimeOffset.UtcNow;
        var times = _sayers.GetOrAdd(caller, _ => new Queue<DateTimeOffset>());
        lock (times)
        {
            while (times.Count > 0 && now - times.Peek() > TimeSpan.FromMinutes(1)) times.Dequeue();
            if (times.Count >= 10 || (times.Count > 0 && now - times.Last() < TimeSpan.FromSeconds(3))) return "slow";
            times.Enqueue(now);
        }
        if (_sayers.Count > 10_000) _sayers.Clear(); // (a bound on the memory; it only forgets who spoke lately)
        if (id == "all")
        {
            foreach (var room in targets)
                lock (room.Gate) Broadcast(room, name, text, "web");
            Remember(new BoardChat(now, "everyone", name, text, "web"));
        }
        else
            lock (targets[0].Gate) Chat(targets[0], name, text, "web");
        return null;
    }

    /// <summary>A line said in a room: to everyone in it (the speaker too, so they see it went), and onto the board.</summary>
    void Chat(Room room, string name, string? text, string from)
    {
        text = Clip(text, ChatMax);
        if (string.IsNullOrEmpty(text)) return;
        Broadcast(room, name, text, from);
        Remember(new BoardChat(DateTimeOffset.UtcNow, RoomLabel(room), name, text, from));
    }

    static void Broadcast(Room room, string name, string text, string from)
    {
        string json = JsonSerializer.Serialize(new { type = "chat", name, text, from });
        foreach (var player in room.Players.Values) Send(player, json);
    }

    void Remember(BoardChat line)
    {
        lock (_statsGate)
        {
            _chat.Enqueue(line);
            while (_chat.Count > ChatKept) _chat.Dequeue();
        }
    }

    static string RoomLabel(Room room) => (room.Players.TryGetValue(room.HostId, out var host) ? host.Name : room.StartedNames.FirstOrDefault() ?? "?") + "'s game";

    public RoomDirectory()
    {
        // once a minute: drop the rooms whose players have gone quiet (even with nobody browsing), and note who's on
        _sampler = new Timer(_ => Sample(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    void Sample()
    {
        Prune();
        int online = PlayersOnline();
        lock (_statsGate) { _history[_historyAt] = online; _historyAt = (_historyAt + 1) % Minutes; _samples++; }
    }

    int PlayersOnline() => _rooms.Values.Sum(r => r.Players.Count);

    void Joined()
    {
        int online = PlayersOnline();
        lock (_statsGate) { _playersJoined++; _peakPlayers = Math.Max(_peakPlayers, online); }
    }

    public Board Board()
    {
        Prune();
        var now = DateTimeOffset.UtcNow;
        var rooms = _rooms.Values.Select(r => BoardRoom(r, now)).OrderByDescending(r => r.Players).ThenBy(r => r.Opened).ToArray();
        lock (_statsGate)
        {
            // the minutes sampled so far (up to Minutes), oldest first, then now
            int n = Math.Min(_samples, Minutes - 1);
            var history = new int[n + 1];
            for (int i = 0; i < n; i++) history[i] = _history[(_historyAt - n + i + Minutes) % Minutes];
            history[^1] = rooms.Sum(r => r.Players);
            var totals = new BoardTotals(_roomsCreated, _playersJoined, _gamesStarted, _peakPlayers, _framesRelayed, _bytesRelayed,
                _longestGame, new Dictionary<string, long>(_gamesByMode));
            return new Board(now, _started, rooms.Sum(r => r.Players), rooms.Length, rooms.Count(r => r.State == "playing"),
                totals, history, rooms, _recent.Reverse().ToArray(), _chat.Reverse().ToArray());
        }
    }

    static BoardRoom BoardRoom(Room room, DateTimeOffset now)
    {
        lock (room.Gate)
        {
            string state = room.Over ? "over" : room.StartMessage != null ? "playing" : "lobby";
            var members = room.Players.Values.OrderBy(p => p.Joined).Select(p =>
            {
                room.Scores.TryGetValue(p.Id, out var score);
                return new BoardPlayer(p.Name, p.IsHost, p.Outgoing != null, score?.Kills, score?.Health, score?.Dead, score?.Team);
            }).ToArray();
            return new BoardRoom(room.Id, room.Players.TryGetValue(room.HostId, out var host) ? host.Name : "", state, room.Mode, ModeName(room.Mode),
                room.Version, room.Players.Count, Capacity, room.Opened, room.StartMessage != null ? room.LastTick / 60.0 : null, room.Status, members);
        }
    }

    /// <summary>The game's names for its online modes (OnlineSession.Modes in the game); an unknown one reads as itself.</summary>
    static string? ModeName(string? mode) => mode switch
    {
        null => null,
        "campaign" => "Campaign co-op",
        "soccer" => "Rocket Soccer",
        _ => mode,
    };

    /// <summary>A started game is over (the host stopped it, left, or the room emptied): it goes on the recent list, once.</summary>
    void GameOver(Room room)
    {
        if (room.StartMessage == null || room.Recorded) return;
        room.Recorded = room.Over = true;
        var names = room.Players.Values.OrderBy(p => p.Joined).Select(p => p.Name).ToArray();
        if (names.Length == 0) names = room.StartedNames;
        var game = new BoardGame(room.StartedNames.FirstOrDefault() ?? "", names, room.Mode, ModeName(room.Mode), room.GameStarted,
            room.LastTick / 60.0, room.Status);
        lock (_statsGate)
        {
            _recent.Enqueue(game);
            while (_recent.Count > RecentGames) _recent.Dequeue();
            _longestGame = Math.Max(_longestGame, game.Seconds);
        }
    }

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
        lock (_statsGate) _roomsCreated++;
        Joined();
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
            var ticket = new PlayerTicket(Summary(room), player.Id, player.Token, false);
            Joined();
            return (null, ticket);
        }
    }

    public Channel<string>? Attach(string id, string playerId, string token)
    {
        if (!_rooms.TryGetValue(id, out var room)) return null;
        lock (room.Gate)
        {
            if (!room.Players.TryGetValue(playerId, out var player) || !TokenMatches(player.Token, token)) return null;
            player.Outgoing?.Writer.TryComplete();
            // never drop: the game is lockstep, so a lost frame puts a client out of sync for good (see Send)
            player.Outgoing = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            if (room.StartMessage != null) Send(player, room.StartMessage);
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
                        if (!other.IsHost) Send(other, "{\"type\":\"stop\"}");
                    GameOver(room);
                }
                else if (room.Players.TryGetValue(room.HostId, out var host))
                    Send(host, JsonSerializer.Serialize(new { type = "peer-left", playerId }));
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
                var root = doc.RootElement;
                if (type == "input" && room.StartMessage != null && !sender.IsHost && room.Players.TryGetValue(room.HostId, out var host))
                {
                    var relay = JsonSerializer.Serialize(new { type = "input", playerId = senderId, input = doc.RootElement.GetProperty("input") });
                    Send(host, relay);
                }
                else if (sender.IsHost && type == "start" && room.Started && room.StartMessage == null)
                {
                    room.StartMessage = json;
                    room.Mode = root.TryGetProperty("mode", out var mode) && mode.ValueKind == JsonValueKind.String ? Clip(mode.GetString(), 20) : "campaign";
                    room.GameStarted = DateTimeOffset.UtcNow;
                    room.StartedNames = room.Players.Values.OrderBy(p => !p.IsHost).ThenBy(p => p.Joined).Select(p => p.Name).ToArray();
                    lock (_statsGate)
                    {
                        _gamesStarted++;
                        _gamesByMode[room.Mode!] = _gamesByMode.GetValueOrDefault(room.Mode!) + 1;
                    }
                    foreach (var player in room.Players.Values)
                        if (!player.IsHost) Send(player, json);
                }
                else if (sender.IsHost && type == "frame" && room.Started)
                {
                    if (root.TryGetProperty("tick", out var tick) && tick.TryGetInt32(out int t)) room.LastTick = t + 1;
                    int sent = 0;
                    foreach (var player in room.Players.Values)
                        if (!player.IsHost) { Send(player, json); sent++; }
                    lock (_statsGate) { _framesRelayed++; _bytesRelayed += (long)json.Length * sent; }
                }
                else if (type == "chat")
                {
                    if (root.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String && sender.MayChat(DateTimeOffset.UtcNow))
                        Chat(room, sender.Name, text.GetString(), "player");
                }
                else if (sender.IsHost && type == "status" && room.StartMessage != null)
                    TakeStatus(room, root); // (for the board only: nobody else needs it)
                else if (sender.IsHost && type == "stop")
                {
                    foreach (var player in room.Players.Values)
                        if (!player.IsHost) Send(player, json);
                    GameOver(room);
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
                    if (!other.IsHost) Send(other, "{\"type\":\"stop\"}");
                GameOver(room);
            }
            else if (room.Players.TryGetValue(room.HostId, out var host))
                Send(host, JsonSerializer.Serialize(new { type = "peer-left", playerId }));
            room.Players.TryRemove(playerId, out _);
            if (room.Players.IsEmpty) { GameOver(room); _rooms.TryRemove(id, out _); }
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
                if (room.Players.IsEmpty) { GameOver(room); _rooms.TryRemove(id, out _); }
            }
        }
    }

    /// <summary>
    /// The host's status: a line on how the game's going ("Blue 2 - 1 Red, 3:10 left") and each player's kills and
    /// health. Only players in the room count, and the line is clipped: it's shown to anyone who opens the board.
    /// </summary>
    static void TakeStatus(Room room, JsonElement root)
    {
        if (root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String) room.Status = Clip(status.GetString(), 80);
        if (!root.TryGetProperty("scores", out var scores) || scores.ValueKind != JsonValueKind.Array) return;
        foreach (var s in scores.EnumerateArray().Take(Capacity))
        {
            if (s.ValueKind != JsonValueKind.Object || !s.TryGetProperty("playerId", out var pid) || pid.ValueKind != JsonValueKind.String) continue;
            string id = pid.GetString()!;
            if (!room.Players.ContainsKey(id)) continue;
            room.Scores[id] = new Score(Int(s, "kills"), Int(s, "health"),
                s.TryGetProperty("dead", out var dead) && dead.ValueKind is JsonValueKind.True, Int(s, "team") is int team and >= 0 ? team : null);
        }
    }

    static int? Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : null;

    static string? Clip(string? text, int max)
    {
        if (text == null) return null;
        text = new string(text.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return text.Length <= max ? text : text[..max];
    }

    static RoomSummary Summary(Room room)
    {
        lock (room.Gate)
            return new RoomSummary(room.Id, room.Code, room.Players.TryGetValue(room.HostId, out var host) ? host.Name : "", room.Version,
                room.Players.Count, Capacity, room.Players.Values.Select(p => p.Seen).DefaultIfEmpty(DateTimeOffset.UtcNow).Max(),
                room.Players.Values.OrderBy(p => p.Joined).Select(p => new RoomMember(p.Id, p.Name, p.IsHost)).ToArray(), room.Started);
    }

    /// <summary>
    /// Queues a message for a player. Nothing is ever dropped (a lockstep game can't skip a frame), but a player a whole
    /// minute of frames behind isn't coming back: their connection is closed, and their game says so.
    /// </summary>
    static void Send(Member member, string message)
    {
        var outgoing = member.Outgoing;
        if (outgoing == null) return;
        if (outgoing.Reader.CanCount && outgoing.Reader.Count >= MaxQueued) { outgoing.Writer.TryComplete(); return; }
        outgoing.Writer.TryWrite(message);
    }

    /// <summary>How many messages may wait for one player: a minute of the host's 60 frames a second, and some.</summary>
    const int MaxQueued = 60 * 60 + 256;

    static bool TokenMatches(string expected, string actual)
    {
        if (string.IsNullOrEmpty(actual) || expected.Length != actual.Length) return false;
        return CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(actual));
    }

    sealed record Score(int? Kills, int? Health, bool Dead, int? Team);

    sealed class Room(string id, string code, string version)
    {
        public readonly object Gate = new();
        public readonly string Id = id, Code = code, Version = version;
        public readonly DateTimeOffset Opened = DateTimeOffset.UtcNow;
        public string HostId = "";
        public bool Started;
        public string? StartMessage;
        // for the board: what's being played, since when and by whom, how far in (the host's frames), and how it's going
        public string? Mode, Status;
        public DateTimeOffset GameStarted;
        public string[] StartedNames = Array.Empty<string>();
        public int LastTick;
        public bool Over, Recorded;
        public readonly ConcurrentDictionary<string, Score> Scores = new();
        public readonly ConcurrentDictionary<string, Member> Players = new();
        public Member Add(string name, bool host)
        {
            // two Sams in a room would be hard to tell apart in chat: the second is "Sam 2"
            string wanted = name;
            for (int n = 2; Players.Values.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)); n++) name = $"{wanted} {n}";
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
        readonly Queue<DateTimeOffset> _said = new();

        /// <summary>A player may say 5 lines in any 5 seconds (more is dropped: a stuck key, or a flood).</summary>
        public bool MayChat(DateTimeOffset now)
        {
            while (_said.Count > 0 && now - _said.Peek() > TimeSpan.FromSeconds(5)) _said.Dequeue();
            if (_said.Count >= 5) return false;
            _said.Enqueue(now);
            return true;
        }
    }
}
