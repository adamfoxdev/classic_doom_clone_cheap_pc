namespace HexenSharp;

/// <summary>Checks on the super shotgun, the lightning gun, Quake ammo, and the Quake weapons hidden through the campaign.</summary>
public static partial class Headless
{
    static void QuakeArmsChecks(Action<bool, string> check)
    {
        Game g = null;
        void Tick(Input i, int n = 1) { for (int k = 0; k < n; k++) g.Update(i, 1f / 35f); }
        Monster Put(float x, float y)
        {
            var m = new Monster(Monster.Slaughtaur) { X = x, Y = y, Level = g.Level, State = AiState.Idle };
            m.Health = m.MaxHealth = 5000;
            g.Level.Things.Add(m);
            return m;
        }
        void Arena(WeaponDef w)
        {
            g = new Game { FixedSeed = 1, AchievementsOn = false };
            g.NewGame(PClass.Fighter);
            g.Level.Things.RemoveAll(t => t is Monster or Pickup);
            var p = g.P;
            p.X = 10.5f; p.Y = 5.5f; p.Angle = 0; p.FloorZ = g.Level.FloorAt(10.5f, 5.5f); p.Pitch = 0;
            g.GiveExtra(w);
            Tick(new Input(), 20);
            g.Vars.Freeze = true;
        }

        // the super shotgun: 14 pellets of 4, all of them at point blank, fewer further off; two shells; marks on the wall
        Arena(QuakeArms.SuperShotgun);
        var p = g.P;
        var close = Put(p.X + 1.2f, p.Y);
        int shells = p.Ammo[(int)AmmoKind.Shells];
        Tick(new Input { Fire = true });
        int pointBlank = 5000 - close.Health;
        check(p.CurWeapon.Shotgun && pointBlank == QuakeArms.Pellets * 4 && p.Ammo[(int)AmmoKind.Shells] == shells - 2,
            $"a super shotgun blast point blank lands all 14 pellets ({pointBlank}) for two shells");
        g.Level.Things.Remove(close);
        var far = Put(p.X + 6f, p.Y);
        p.Cooldown = 0;
        Tick(new Input { Fire = true });
        int spread = 5000 - far.Health;
        check(spread > 0 && spread < pointBlank, $"further off the spread lets pellets past ({spread} at 6 cells)");
        check(g.Level.Things.OfType<Puff>().Count(t => t.Sprite(0) == Art.PelletMark) >= 3, "and the ones that miss mark the wall");

        // the lightning gun: 30 ten times a second to the first thing in the beam, a cell a tick, out to 6.7 cells
        Arena(QuakeArms.LightningGun);
        p = g.P;
        var near = Put(p.X + 4f, p.Y);
        var behind = Put(p.X + 5f, p.Y);
        int cells = p.Ammo[(int)AmmoKind.Cells];
        Tick(new Input { Fire = true }, 35);
        int burned = 5000 - near.Health, used = cells - p.Ammo[(int)AmmoKind.Cells];
        check(p.CurWeapon.Beam && burned >= 270 && burned <= 330 && used >= 9 && used <= 11 && behind.Health == 5000,
            $"held for a second, the lightning gun does {burned} ({used} cells) to the first monster in the beam, none to the one behind");
        check(near.KnockX > 0, "and pushes it back a little");
        g.Level.Things.Remove(near); g.Level.Things.Remove(behind);
        var beyond = Put(p.X + 8f, p.Y);
        Tick(new Input { Fire = true }, 10);
        check(beyond.Health == 5000, "it doesn't reach 8 cells");

        // ammo: a box adds to it (up to the most you can carry); with none the weapon won't fire and you switch
        Arena(Rockets.Launcher);
        p = g.P;
        p.Ammo[(int)AmmoKind.Rockets] = 98;
        var box = new Pickup(PickupKind.Ammo, 0.36f, (int)AmmoKind.Rockets) { X = p.X, Y = p.Y, Level = g.Level };
        g.Level.Things.Add(box);
        Tick(new Input());
        check(p.Ammo[(int)AmmoKind.Rockets] == QuakeAmmo.Max(AmmoKind.Rockets) && box.Removed, "a box of rockets tops you up, to 100 at most");
        p.Ammo[(int)AmmoKind.Rockets] = 0; p.Cooldown = 0;
        int rockets = g.Level.Things.OfType<Projectile>().Count();
        Tick(new Input { Fire = true });
        Tick(new Input(), 20);
        check(g.Level.Things.OfType<Projectile>().Count() == rockets && !p.CurWeapon.Rocket, "out of rockets, the launcher won't fire, and you switch to a weapon that will");
        g.Vars.Freeze = false;

        // the Quake weapons come up faster than the classes' own
        float Raise(int slot)
        {
            Tick(new Input { Slot = 1 }); Tick(new Input(), 30);
            Tick(new Input { Slot = slot });
            int n = 0;
            while (p.PendingWeapon >= 0 && n < 100) { Tick(new Input()); n++; }
            return n / 35f;
        }
        g.GiveExtra(Rockets.Launcher);
        p.HasWeapon[1] = true;
        float quick = Raise(4), slow = Raise(2);
        check(quick < slow * 0.6f, $"a Quake weapon comes up in {quick:0.00}s, a class weapon in {slow:0.00}s");

        // the campaign: each Quake weapon hidden on a plinth, one a map, each higher than the last
        g = new Game { FixedSeed = 1, AchievementsOn = false };
        g.NewGame(PClass.Mage);
        var st = g.StashPlaces;
        check(st.Count == QuakeArms.Stashes.Length && st.Select(s => s.Weapon).SequenceEqual(QuakeArms.Stashes.Select(s => s.weapon)) && st.Select(s => s.Map).Distinct().Count() == st.Count,
            "the campaign hides all five Quake weapons, one a map: " + string.Join(", ", st.Select(s => $"{s.Weapon.Name} in {g.Hub[s.Map].RawName}")));
        check(st.Zip(st.Skip(1)).All(pr => pr.First.Height <= pr.Second.Height) && st.All(s => g.Hub[s.Map].Things.OfType<Pickup>()
                .Any(k => k.Kind == PickupKind.Arms && MathF.Abs(g.Hub[s.Map].FloorAt(k.X, k.Y) - (g.Hub[s.Map].FloorAt(s.X - 0.5f, s.Y + 1.5f) + s.Height)) < 0.3f)),
            "each sits up on its plinth, no lower than the one before");

        // can they be reached? Stand off a plinth facing away, blast yourself up and back onto it; the first by two jumps
        bool Reach(Stash s, WeaponDef with)
        {
            var lv = g.Hub[s.Map];
            float cx = s.X + 1f, cy = s.Y + 1f, floor = lv.Floors[s.Y * lv.W + s.X - 1 - (s == st[0] ? 1 : 0) * 0];
            foreach (var (dx, dy) in new[] { (-1f, 0f), (1f, 0f), (0f, -1f), (0f, 1f) })
                foreach (float back in new[] { 1.6f, 2f, 2.5f, 3f, 3.5f })
                {
                    float sx = cx + dx * back, sy = cy + dy * back;
                    // a clear run from there to the plinth, all at its foot's level
                    bool clear = true;
                    for (float t = 1.05f; t <= back && clear; t += 0.25f) clear = !lv.BlocksCircle(cx + dx * t, cy + dy * t, 0.26f) && MathF.Abs(lv.FloorAt(cx + dx * t, cy + dy * t) - lv.FloorAt(sx, sy)) < 0.01f;
                    if (!clear) continue;
                    g.Warp(s.Map);
                    g.Level.Things.RemoveAll(t => t is Monster);
                    var q = g.P;
                    q.X = sx; q.Y = sy; q.FloorZ = lv.FloorAt(sx, sy); q.Z = 0; q.VX = q.VY = q.VZ = 0; q.Health = 200; q.MaxHealth = 200;
                    q.Angle = MathF.Atan2(dy, dx); q.Pitch = -Rockets.LookDown;
                    g.GiveExtra(with);
                    Tick(new Input(), 15);
                    q.Pitch = -Rockets.LookDown; q.Cooldown = 0;
                    float goal = lv.FloorAt(cx, cy);
                    if (with.Rocket) Tick(new Input { Jump = true, Fire = true });
                    else
                    {
                        Tick(new Input { Fire = true });
                        var nade = g.Level.Things.OfType<Projectile>().LastOrDefault(t => t.Kind == ProjKind.Grenade);
                        if (nade == null) continue;
                        while (!nade.Removed && nade.Life > 0.06f)
                        {
                            bool resting = nade.VZ == 0 && MathF.Abs(nade.VX) + MathF.Abs(nade.VY) < 0.05f;
                            if (resting) { q.X = nade.X - dx * 0.45f; q.Y = nade.Y - dy * 0.45f; }
                            Tick(new Input());
                        }
                        Tick(new Input { Jump = true, Move = -1 });
                    }
                    for (int k = 0; k < 90; k++) { Tick(new Input { Move = -1 }); if (q.OnGround && k > 5) break; }
                    for (int k = 0; k < 20 && !q.OnGround; k++) Tick(new Input());
                    if (MathF.Abs(q.FloorZ - goal) < 0.01f) return true;
                }
            return false;
        }
        var reachable = st.Skip(1).Select(s => (s, Reach(s, s.Height <= 2.5f ? Grenades.Launcher : Rockets.Launcher))).ToList();
        check(reachable.All(r => r.Item2), "every later plinth can be reached: " + string.Join(", ", reachable.Select(r => $"{r.s.Weapon.Name} ({r.s.Height}) with a {(r.s.Height <= 2.5f ? "grenade" : "rocket")} jump: {(r.Item2 ? "yes" : "NO")}")));

        // the first: two jumps, by the step (and no way up without it)
        {
            var s = st[0];
            g.Warp(s.Map);
            g.Level.Things.RemoveAll(t => t is Monster);
            var q = g.P;
            var lv = g.Level;
            float top = lv.FloorAt(s.X + 0.5f, s.Y + 0.5f);
            void Hop(float angle)
            {
                q.Angle = angle;
                for (int k = 0; k < 15; k++) Tick(new Input { Move = 1, Walk = true }); // up to it, then jump
                Tick(new Input { Jump = true, Move = 1, Walk = true });
                for (int k = 0; k < 25; k++) Tick(new Input { Move = 1, Walk = true });
            }
            void Stand(float x, float y) { q.X = x; q.Y = y; q.FloorZ = lv.FloorAt(x, y); q.Z = 0; q.VX = q.VY = q.VZ = 0; q.Pitch = 0; }
            // from the ring below the step: north up onto it, then east up onto the plinth
            Stand(s.X - 0.5f, s.Y + 1.5f);
            Hop(-MathF.PI / 2);
            bool onStep = MathF.Abs(q.FloorZ - lv.FloorAt(s.X - 0.5f, s.Y + 0.5f)) < 0.01f && q.FloorZ > lv.FloorAt(s.X - 0.5f, s.Y + 1.5f);
            Hop(0);
            bool byStep = onStep && MathF.Abs(q.FloorZ - top) < 0.01f;
            // from the far side, no step: never
            Stand(s.X + 2.5f, s.Y + 1.5f);
            for (int h = 0; h < 3; h++) Hop(MathF.PI);
            bool without = MathF.Abs(q.FloorZ - top) < 0.01f;
            check(byStep && !without, $"the grenade launcher's plinth ({s.Height}) takes a jump onto the step beside it and another onto it (by the step: {byStep}; from the far side: {without})");
        }

        // taking one: it's yours on the next key, loaded, and kept in a save with its ammo
        string dir = Path.Combine(Path.GetTempPath(), $"hexensharp-quake-{Environment.ProcessId}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "save.json");
        g = new Game { FixedSeed = 1, AchievementsOn = false, SavePath = path };
        g.NewGame(PClass.Cleric);
        st = g.StashPlaces;
        g.Warp(st[0].Map);
        p = g.P;
        var gl = g.Level.Things.OfType<Pickup>().First(k => k.Kind == PickupKind.Arms);
        p.X = gl.X; p.Y = gl.Y; p.FloorZ = g.Level.FloorAt(gl.X, gl.Y);
        Tick(new Input(), 25);
        check(p.Weapons.Length == 4 && p.CurWeapon.Grenade && p.Ammo[(int)AmmoKind.Rockets] >= QuakeAmmo.WithWeapon(AmmoKind.Rockets) && gl.Removed,
            $"picking up a stash's weapon puts it on key 4, loaded ({p.Ammo[(int)AmmoKind.Rockets]} rockets), and in hand");
        g.SaveNow();
        var back = new Game { FixedSeed = 1, AchievementsOn = false, SavePath = path, Profile = g.Profile };
        check(back.Continue() && back.P.Weapons.Length == 4 && back.P.Weapons[3] == Grenades.Launcher && back.P.HasWeapon[3] && back.P.CurWeapon.Grenade
              && back.P.Ammo[(int)AmmoKind.Rockets] == p.Ammo[(int)AmmoKind.Rockets] && !back.Level.Things.OfType<Pickup>().Any(k => k.Kind == PickupKind.Arms),
            "a save keeps the Quake weapons you've found, their ammo, and the empty plinth");
        Directory.Delete(dir, true);

        // monsters drop ammo for the Quake weapons you carry, now and then
        int drops = 0;
        for (int k = 0; k < 40; k++)
        {
            var m = Put(p.X + 3, p.Y);
            int before = g.Level.Things.OfType<Pickup>().Count(t => t.Kind == PickupKind.Ammo);
            g.DamageMonster(m, 100000, 0);
            if (g.Level.Things.OfType<Pickup>().Count(t => t.Kind == PickupKind.Ammo) > before) drops++;
        }
        check(drops is > 4 and < 25, $"once you carry one, monsters sometimes drop its ammo ({drops} of 40)");
    }
}
