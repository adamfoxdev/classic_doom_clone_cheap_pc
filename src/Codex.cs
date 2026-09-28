namespace HexenSharp;

/// <summary>
/// The monster codex (Character > Codex): every monster in the game, locked until you first kill one. An entry shows
/// the monster, how many you've killed, its health, a line of lore (in the style you're playing) and how to beat it.
/// Your kills of each are kept in your profile.
/// </summary>
public static class Codex
{
    public sealed record Entry(MonsterDef Def, string Lore, string SciFiLore, string Weakness)
    {
        public string Id => Def.Art;
        public string Text => Art.Style == ArtStyle.SciFi ? SciFiLore : Lore;
    }

    public static readonly Entry[] All =
    {
        new(Monster.Ettin, "Two-headed brutes from the hub's lower halls, too thick to fear anything.",
            "Security mechs on crab legs. They swing; they don't think.",
            "Slow and melee only: keep your distance, or hop back out of its swing."),
        new(Monster.Afrit, "Fire spirits that hang in the air and spit flame.",
            "Hover drones with a plasma spitter.",
            "Frail. Quick shots bring one down before it's close; sidestep its fireballs."),
        new(Monster.Centaur, "Armoured half-horses that charge in with a blade.",
            "Walkers built for breaching doors, all blade and armour.",
            "Melee only: backpedal and shoot, or slide past it."),
        new(Monster.Slaughtaur, "A centaur grown huge and cruel, throwing bolts in threes.",
            "The heavy strider: a blade up close and a three-bolt volley.",
            "Its volley fans out: stand close and strafe, or far enough for the gaps."),
        new(Monster.Bishop, "Dark Bishops blur aside from your blows and send seekers after you.",
            "Psionic wraiths that phase out of your fire and loose homing bolts.",
            "Hit it the moment the blur ends. Seekers skim low: jump them."),
        new(Monster.Heresiarch, "Master of the hub and warden of the exit. It does not fall easily.",
            "The station's mind, guarding the way out. It does not shut down easily.",
            "Five-shot volleys: keep moving sideways and use your heaviest weapon."),
        new(Brutes.Grenadier, "A brute with a sling of black-powder bombs that bounce before they burst.",
            "A demolition mech that lobs bouncing charges.",
            "Its grenades bounce, then burst: keep moving, and don't stand by one. It throws a slow arc: close in."),
        new(Brutes.Juggernaut, "A walking wall of iron. Its blows barely hurt, but they send you flying.",
            "A riot mech built to clear doorways. It hits to shove, not to kill.",
            "Keep away from ledges when it's close, and shoot it from range: it's slow."),
        new(MiniBosses.Warden, "A giant sealed in the quarry rock, woken by footsteps.",
            "A runaway mining mech that tunnels to anything that moves.",
            "When its fists come down, jump: the slam can't reach you in the air."),
        new(MiniBosses.Stalker, "A hunter of the barren plain that runs its prey down.",
            "A harvester gone feral, charging whatever it sees.",
            "Sidestep the charge: if it hits rock it's stunned and takes double damage."),
        new(MiniBosses.Thornmother, "The mother of the brood, drifting over the meadow.",
            "The hive queen, calling her swarm to her side.",
            "Kill her and the brood dies with her: ignore the small ones."),
        new(MiniBosses.Keeper, "The keeper of the drowned halls, never where you left it.",
            "A ghost in the coolant loop that jumps from tank to tank.",
            "Each fifth of its health it blinks away, often up a ledge: bring the jetpack."),
        new(MiniBosses.Wyrm, "It swims through the stone of the Depths as if it were water.",
            "A borer drone that tunnels through solid rock around you.",
            "Untouchable in the rock. Wait for it to burst out beside you, then hit hard."),
        new(MiniBosses.Dreadnought, "Lord of the Void, sailing ahead of your skyship.",
            "The flagship of the belt, holding the lane ahead of you.",
            "It weaves across the lane: fire as it swings back to the middle."),
    };

    public static Entry Find(string id) => All.FirstOrDefault(e => e.Id == id);

    /// <summary>How many of `e` you've killed.</summary>
    public static int Kills(Profile p, Entry e) => p.KillsBy.TryGetValue(e.Id, out int n) ? n : 0;

    /// <summary>An entry opens at your first kill (a mini-boss you beat before the codex existed counts too).</summary>
    public static bool Unlocked(Profile p, Entry e) => Kills(p, e) > 0 || (e.Def.MiniBoss != null && p.MiniBosses.Contains(e.Def.MiniBoss));

    public static int Found(Profile p) => All.Count(e => Unlocked(p, e));
}

public sealed partial class Game
{
    /// <summary>You killed `m`: one more in the codex (the first opens its entry).</summary>
    void CodexKill(Monster m)
    {
        if (NoXp || Codex.Find(m.Def.Art) is not { } e) return;
        bool first = !Codex.Unlocked(Profile, e);
        Profile.KillsBy[e.Id] = Codex.Kills(Profile, e) + 1;
        if (first) Say($"New codex entry: {m.Def.Name}.");
    }
}
