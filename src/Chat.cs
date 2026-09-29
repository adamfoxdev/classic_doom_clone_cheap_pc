namespace HexenSharp;

/// <summary>A line of online chat: from a player in your room, or from someone on the matchmaker's who board (Web).</summary>
public sealed class ChatLine
{
    public string Name, Text;
    public bool Web;
    /// <summary>How long ago it came in, in seconds: the view shows it for Game.ChatShow, the chat box always.</summary>
    public float Age;
}

/// <summary>
/// Online chat and the name you go by online. Chat isn't part of the game (nothing in it touches the shared world), so
/// it travels beside the frames: you send a line, the matchmaker stamps your name on it and passes it to everyone in
/// the room, you included, and to its who board.
/// </summary>
public sealed partial class Game
{
    /// <summary>Longest line you can send, and how many lines are kept.</summary>
    public const int ChatMax = 100, ChatKeep = 20;
    /// <summary>How long a line stays on screen in play.</summary>
    public const float ChatShow = 10f;
    public readonly List<ChatLine> Chat = new();
    /// <summary>Typing a line (T in an online game, or Chat on the Online page in a room).</summary>
    public bool Chatting;
    public string ChatDraft = "";

    /// <summary>In a room or an online game: there's someone to talk to.</summary>
    public bool CanChat => NetLink != null && (NetSession != null || Matchmaker?.InRoom == true);

    public void OpenChat()
    {
        if (!CanChat) return;
        Chatting = true;
        ChatDraft = "";
    }

    /// <summary>Typing: letters go in, Backspace takes one out, Tab pastes, Enter sends, Esc gives up.</summary>
    public void ChatInput(Input inp)
    {
        if (!CanChat) { Chatting = false; ChatDraft = ""; return; } // (the game or room ended as you typed)
        if (inp.Pause) { Chatting = false; ChatDraft = ""; return; }
        if (inp.Confirm)
        {
            Chatting = false;
            string text = ChatDraft.Trim();
            ChatDraft = "";
            if (text.Length > 0 && CanChat) NetLink.SendNetwork(new NetworkMessage { Type = "chat", Text = text });
            return;
        }
        if (inp.Tab && PasteText?.Invoke() is { } pasted) Type(pasted.Replace('\n', ' ').Replace('\r', ' '));
        if (inp.Backspace && ChatDraft.Length > 0) ChatDraft = ChatDraft[..^1];
        Type(inp.Typed);

        void Type(string typed)
        {
            if (string.IsNullOrEmpty(typed)) return;
            foreach (char c in typed)
                if (!char.IsControl(c) && ChatDraft.Length < ChatMax) ChatDraft += c;
        }
    }

    /// <summary>A line came in (the matchmaker's said whose it is).</summary>
    public void HearChat(NetworkMessage m)
    {
        if (string.IsNullOrWhiteSpace(m.Text)) return;
        Chat.Add(new ChatLine { Name = string.IsNullOrWhiteSpace(m.Name) ? "?" : m.Name, Text = m.Text, Web = m.From == "web" });
        while (Chat.Count > ChatKeep) Chat.RemoveAt(0);
        PlaySound(Sfx.Item, 0.35f);
    }

    void AgeChat(float dt)
    {
        foreach (var line in Chat) line.Age += dt;
    }

    /// <summary>
    /// The name you go by online, asked for the first time you open the Online page (and changed there with Name:).
    /// Empty until you've picked one.
    /// </summary>
    public string OnlineName = "";

    /// <summary>A name as the matchmaker will take it: printable, trimmed, 1 to 16 characters (empty if nothing's left).</summary>
    public static string CleanOnlineName(string name)
    {
        var s = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        while (s.Contains("  ")) s = s.Replace("  ", " ");
        return s.Length > 16 ? s[..16].TrimEnd() : s;
    }
}
