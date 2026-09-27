using System.Reflection;

namespace HexenSharp;

/// <summary>
/// The optional "rendered art" pack: sci-fi sprites, textures and monster frames rendered in Blender by
/// tools/blender/build_scifi_assets.py and embedded in the game as PNGs. When the option is on (and the style is
/// sci-fi) they replace the matching procedural art; anything the pack doesn't cover keeps its procedural look.
/// </summary>
public static class RenderedArt
{
    /// <summary>Which art slot each rendered sprite and texture replaces.</summary>
    static readonly (string file, Action<Tex> set)[] Slots =
    {
        ("sprites/vial", t => Art.Vial = t),
        ("sprites/flask", t => Art.Flask = t),
        ("sprites/urn", t => Art.Urn = t),
        ("sprites/bluemana", t => Art.BlueMana = t),
        ("sprites/greenmana", t => Art.GreenMana = t),
        ("sprites/steelkey", t => Art.SteelKey = t),
        ("sprites/firekey", t => Art.FireKey = t),
        ("sprites/armor", t => Art.Armor = t),
        ("sprites/jetpack", t => Art.Jetpack = t),
        ("sprites/weapon2", t => Art.WeaponPiece2 = t),
        ("sprites/weapon3", t => Art.WeaponPiece3 = t),
        ("textures/stone", t => Art.Stone = t),
        ("textures/marble", t => Art.Marble = t),
        ("textures/brick", t => Art.Brick = t),
        ("textures/floor", t => Art.FloorStone = t),
        ("textures/rubble", t => Art.Rubble = t),
    };

    /// <summary>Monsters with rendered frames: the four live poses; the death frames are derived as usual.</summary>
    static readonly string[] Monsters = { "afrit", "ettin", "centaur", "slaughtaur", "bishop", "heresiarch" };
    static readonly string[] LivePoses = { "walk0", "walk1", "attack", "pain" };

    static Dictionary<string, byte[]> _files;

    /// <summary>The embedded PNGs by path under assets/scifi (e.g. "sprites/jetpack"), without the extension.</summary>
    public static IReadOnlyDictionary<string, byte[]> Files => _files ??= LoadFiles();

    static Dictionary<string, byte[]> LoadFiles()
    {
        var asm = Assembly.GetExecutingAssembly();
        var files = new Dictionary<string, byte[]>();
        foreach (var name in asm.GetManifestResourceNames())
        {
            var path = name.Replace('\\', '/');
            const string prefix = "assets/scifi/";
            if (!path.StartsWith(prefix) || !path.EndsWith(".png")) continue;
            using var s = asm.GetManifestResourceStream(name)!;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            files[path[prefix.Length..^4]] = ms.ToArray();
        }
        return files;
    }

    public static bool Available => Files.Count > 0;

    /// <summary>Every art slot the pack fills, for the self-test and the options screen.</summary>
    public static IEnumerable<string> Covered =>
        Slots.Select(s => s.file).Where(Files.ContainsKey)
            .Concat(Monsters.Where(m => LivePoses.All(p => Files.ContainsKey($"monsters/{m}_{p}"))).Select(m => "monsters/" + m));

    public static Tex Load(string file) => Files.TryGetValue(file, out var png) ? Png.Load(png) : null;

    /// <summary>Swaps the rendered art in over the sci-fi art that Art.Init just built.</summary>
    public static void Apply()
    {
        foreach (var (file, set) in Slots)
        {
            var t = Load(file);
            if (t != null) set(t);
        }
        foreach (var m in Monsters)
        {
            var live = LivePoses.Select(p => Load($"monsters/{m}_{p}")).ToArray();
            if (live.Any(t => t == null)) continue;
            Art.Monsters[m] = Art.PoseSet(live);
        }
    }
}
