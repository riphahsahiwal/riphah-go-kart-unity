// Faithful port of game/game/simulation.ts (Smash Karts Arena).
// Pure C# — no UnityEngine dependency. Logic uses double, as in JS.
// Source path/line comments are kept on non-trivial methods.
// Unity boundary (Runtime adapters) converts double -> float and mirrors Z:
// three.js is right-handed, Unity is left-handed: (x, y, -z), quaternion (-x, -y, z, w).
// See PARITY_MATRIX.md for constants, weapon tables, and accepted differences.
// NOT compiled/tested on the authoring machine (no Unity Editor / .NET SDK here).

using System;
using System.Collections.Generic;
using System.Linq;

namespace SmashKarts.Core
{
    public static class GameConstants
    {
        public const double TickRate = 30;
        public const double RoundSeconds = 180;
        public const int MaxPlayers = 8;
        public const double ArenaX = 46;
        public const double ArenaZ = 38;

        public static readonly string[] Colors =
        {
            "#d7fa52", "#ff7161", "#6bcdff", "#d695ff",
            "#ffa944", "#ff91c4", "#6ce7be", "#f6f1dc",
        };

        public static double Clamp(double n, double a, double b) => Math.Max(a, Math.Min(b, n));
        public static double AngleDiff(double a, double b) => Math.Atan2(Math.Sin(a - b), Math.Cos(a - b));

        // simulation.ts: groundHeight
        public static double GroundHeight(double x, double z)
        {
            if (Math.Abs(x) > 7.5 || Math.Abs(z) > 19) return 0;
            return Math.Abs(z) <= 6 ? 3 : (3 * (19 - Math.Abs(z))) / 13;
        }
    }

    public enum WeaponKind
    {
        Rockets, Bullets, Machinegun, Cannon, Mines, Nuke, Lobgrenuke, Spiky, Star, Snow, Fake
    }

    public static class WeaponKindNames
    {
        public static string ToKey(WeaponKind w) => w switch
        {
            WeaponKind.Rockets => "rockets",
            WeaponKind.Bullets => "bullets",
            WeaponKind.Machinegun => "machinegun",
            WeaponKind.Cannon => "cannon",
            WeaponKind.Mines => "mines",
            WeaponKind.Nuke => "nuke",
            WeaponKind.Lobgrenuke => "lobgrenuke",
            WeaponKind.Spiky => "spiky",
            WeaponKind.Star => "star",
            WeaponKind.Snow => "snow",
            WeaponKind.Fake => "fake",
            _ => "rockets",
        };
    }

    public sealed class WeaponDef
    {
        public string NameKo;
        public string NameEn;
        public string Icon;
        public int Ammo;
        public string Color;
        public string Description;
    }

    public static class Weapons
    {
        // simulation.ts: WEAPONS — Korean names/descriptions preserved verbatim.
        public static readonly Dictionary<WeaponKind, WeaponDef> Defs = new()
        {
            [WeaponKind.Rockets] = new() { NameKo = "로켓", NameEn = "ROCKETS", Icon = "rocket", Ammo = 3, Color = "#ff8655", Description = "전방으로 직진하는 로켓 3발. 직격 50 피해와 작은 범위 폭발." },
            [WeaponKind.Bullets] = new() { NameKo = "삼연발 탄환", NameEn = "BULLETS", Icon = "crosshair", Ammo = 9, Color = "#ffe36c", Description = "3발씩 9회 발사하는 직진 탄환. 총 27발, 발당 7 피해." },
            [WeaponKind.Machinegun] = new() { NameKo = "기관총", NameEn = "MACHINE GUN", Icon = "scan", Ammo = 166, Color = "#d7fa52", Description = "가까운 상대를 자동 추적하며 6.5초 연속 사격. 한번 시작하면 멈출 수 없고, 멀어질수록 피해가 줄어듭니다." },
            [WeaponKind.Cannon] = new() { NameKo = "곡사포", NameEn = "CANNONBALLS", Icon = "circle", Ammo = 4, Color = "#ffc494", Description = "포물선을 그리는 포탄 4발. 직격 시 즉시 처치. Space를 길게 누르면 다음 탄의 사거리가 늘어납니다." },
            [WeaponKind.Mines] = new() { NameKo = "지뢰", NameEn = "MINES", Icon = "triangle", Ammo = 3, Color = "#ff7979", Description = "카트 뒤에 지뢰 3개를 설치. 상대가 밟으면 폭발하며 즉시 처치." },
            [WeaponKind.Nuke] = new() { NameKo = "핵탄두", NameEn = "NUKE", Icon = "radiation", Ammo = 1, Color = "#ffb74a", Description = "전방으로 거대한 탄두를 발사. 직격과 넓은 폭발 범위로 다수의 상대를 공격." },
            [WeaponKind.Lobgrenuke] = new() { NameKo = "폭탄", NameEn = "LOB-GRENUKE", Icon = "bomb", Ammo = 1, Color = "#fa907a", Description = "앞으로 던지는 근접 감지 폭탄. 상대가 접근하거나 4초가 지나면 폭발." },
            [WeaponKind.Spiky] = new() { NameKo = "회전 철퇴", NameEn = "SPIKY-GO-ROUND", Icon = "orbit", Ammo = 1, Color = "#b9a5ff", Description = "6초 동안 철퇴 5개가 카트 주위를 회전. 닿은 상대를 즉시 처치하지만 공격을 막지는 못합니다." },
            [WeaponKind.Star] = new() { NameKo = "무적", NameEn = "INVINCIBILITY", Icon = "star", Ammo = 1, Color = "#d7fa52", Description = "5초 동안 모든 피해를 막으며, 부딪힌 상대를 즉시 처치." },
            [WeaponKind.Snow] = new() { NameKo = "눈덩이", NameEn = "SNOWBALL BLASTER", Icon = "snow", Ammo = 3, Color = "#91e9ff", Description = "눈덩이 3발. 맞은 상대를 3초 동안 얼리고, 얼어붙은 카트에 부딪히면 처치." },
            [WeaponKind.Fake] = new() { NameKo = "가짜 상자", NameEn = "FAKE LOOT BOX", Icon = "box", Ammo = 1, Color = "#f58eff", Description = "아이템 상자처럼 보이는 함정. 상대가 접촉하면 즉시 처치." },
        };

        public static readonly WeaponKind[] Loot =
        {
            WeaponKind.Rockets, WeaponKind.Rockets, WeaponKind.Rockets,
            WeaponKind.Bullets, WeaponKind.Bullets,
            WeaponKind.Machinegun, WeaponKind.Machinegun,
            WeaponKind.Cannon, WeaponKind.Cannon,
            WeaponKind.Mines, WeaponKind.Mines,
            WeaponKind.Nuke, WeaponKind.Lobgrenuke, WeaponKind.Snow,
            WeaponKind.Spiky, WeaponKind.Star, WeaponKind.Fake,
        };
    }

    public struct Input
    {
        public double Throttle;
        public double Steer;
        public bool Fire;
        public static readonly Input Neutral = new() { Throttle = 0, Steer = 0, Fire = false };
    }

    public static class InputSanitizer
    {
        // simulation.ts: sanitizeInput — finite numbers only, clamped; fire must be exactly true.
        public static Input Sanitize(double? throttle, double? steer, bool fire)
        {
            return new Input
            {
                Throttle = throttle.HasValue && double.IsFinite(throttle.Value) ? GameConstants.Clamp(throttle.Value, -1, 1) : 0,
                Steer = steer.HasValue && double.IsFinite(steer.Value) ? GameConstants.Clamp(steer.Value, -1, 1) : 0,
                Fire = fire,
            };
        }
    }

    public sealed class Obstacle
    {
        public double X, Z, W, D, H;
        public Obstacle(double x, double z, double w, double d, double h) { X = x; Z = z; W = w; D = d; H = h; }
    }

    public sealed class Player
    {
        public string Id;
        public string Name;
        public int Color;
        public bool Bot;
        public double X, Y, Z, Angle, Speed, Vy;
        public double Hp = 100;
        public int Kills, Deaths;
        public WeaponKind? Weapon;
        public int Ammo;
        public double Cooldown, Active, Shield, Frozen, Respawn;
        public Input Input;
        public double FireHeld, MachineClock, LastInput;

        public Player Copy() => (Player)MemberwiseClone();
    }

    public sealed class Projectile
    {
        public int Id;
        public string Owner;
        public WeaponKind Kind;
        public double X, Y, Z, Vx, Vy, Vz, Life, Age;
        public bool Armed;
        public Projectile Copy() => (Projectile)MemberwiseClone();
    }

    public sealed class GameEvent
    {
        public int Id;
        public double T;
        public string Kind; // shot | explosion | hit | smash | pickup | freeze | shield
        public double X, Y, Z;
        public string Color;
        public double Size;
        public string Owner, Target, Name, Victim;
        public WeaponKind? Weapon;
        public double? Tx, Ty, Tz;
        public GameEvent Copy() => (GameEvent)MemberwiseClone();
    }

    public sealed class BoxState
    {
        public int Id;
        public double X, Z, Ready;
        public BoxState Copy() => (BoxState)MemberwiseClone();
    }

    public sealed class Snapshot
    {
        public string Code;
        public double T, Remaining;
        public string Phase; // countdown | playing | results
        public int Round;
        public List<Player> Players = new();
        public List<Projectile> Projectiles = new();
        public List<BoxState> Boxes = new();
        public List<GameEvent> Events = new();
    }

    public sealed class GameRoom
    {
        // simulation.ts: GameRoom
        public string Code;
        public Dictionary<string, Player> PlayersById = new();
        public List<Player> Players = new();
        public List<Projectile> Projectiles = new();
        public List<GameEvent> Events = new();
        public List<BoxState> Boxes = new();
        public double T;
        public double Elapsed = -3;
        public int Round = 1;
        public int Serial;
        public Func<double> Random;

        public static readonly Obstacle[] Obstacles =
        {
            new(-21, -16, 10, 6, 3.6), new(21, 16, 10, 6, 3.6),
            new(23, -16, 7, 9, 3.3), new(-23, 16, 7, 9, 3.3),
            new(-37, 0, 4, 10, 2.5), new(37, 0, 4, 10, 2.5),
        };

        public static readonly double[][] BoxPositions =
        {
            new double[]{-34,-26}, new double[]{-10,-27}, new double[]{13,-28}, new double[]{35,-26},
            new double[]{-33,27}, new double[]{-12,27}, new double[]{12,27}, new double[]{34,27},
            new double[]{-27,-4}, new double[]{27,4}, new double[]{-13,7}, new double[]{13,-7},
            new double[]{0,0}, new double[]{0,14}, new double[]{0,-14}, new double[]{33,13}, new double[]{-33,-13},
        };

        private static readonly double[][] Spawns =
        {
            new double[]{-36,-30,0.7}, new double[]{36,30,3.8}, new double[]{36,-30,-0.7}, new double[]{-36,30,2.4},
            new double[]{-15,-30,0}, new double[]{15,30,Math.PI}, new double[]{-31,8,Math.PI/2}, new double[]{31,-8,-Math.PI/2},
        };

        private static readonly string[] BotNames = { "Bolt", "Miso", "Turbo", "Mochi", "Blaze", "Pixel", "Rocket" };

        public GameRoom(string code = "PRACTICE", Func<double> random = null)
        {
            Code = code;
            Random = random ?? (() => System.Random.Shared.NextDouble());
            for (int i = 0; i < BoxPositions.Length; i++)
                Boxes.Add(new BoxState { Id = i, X = BoxPositions[i][0], Z = BoxPositions[i][1], Ready = 0 });
        }

        public string Phase => Elapsed < 0 ? "countdown" : Elapsed >= GameConstants.RoundSeconds ? "results" : "playing";

        public Player AddPlayer(string id, string name, int color = 0, bool bot = false)
        {
            if (Players.Count >= GameConstants.MaxPlayers) return null;
            var trimmed = (name ?? "").Trim();
            if (trimmed.Length > 16) trimmed = trimmed.Substring(0, 16);
            var p = new Player
            {
                Id = id, Name = trimmed.Length == 0 ? "Driver" : trimmed,
                // TS: clamp(Math.floor(color) || 0, 0, 7). For integer colours this is clamp(color, 0, 7).
                Color = (int)GameConstants.Clamp(Math.Floor((double)color), 0, 7),
                Bot = bot, Hp = 100, Shield = 2, Input = Input.Neutral, LastInput = T,
            };
            PlayersById[id] = p;
            Players.Add(p);
            Spawn(p);
            return p;
        }

        public void RemovePlayer(string id)
        {
            if (PlayersById.TryGetValue(id, out var p))
            {
                PlayersById.Remove(id);
                Players.Remove(p);
            }
        }

        public void Spawn(Player p)
        {
            // simulation.ts: spawn — clearest spawn wins; pick randomly among top 3.
            var choices = Spawns.Select(s =>
            {
                double clear = 999;
                foreach (var q in Players)
                {
                    if (q.Id != p.Id && q.Respawn <= 0)
                        clear = Math.Min(clear, Math.Hypot(q.X - s[0], q.Z - s[1]));
                }
                return (s, clear);
            }).OrderByDescending(c => c.clear).ToList();
            var chosen = choices[(int)Math.Floor(Random() * Math.Min(3, choices.Count))].s;
            p.X = chosen[0]; p.Z = chosen[1]; p.Y = 0; p.Angle = chosen[2];
            p.Speed = 0; p.Vy = 0; p.Hp = 100; p.Weapon = null; p.Ammo = 0;
            p.Active = 0; p.Shield = 2; p.Frozen = 0; p.Respawn = 0;
            p.Cooldown = 0; p.FireHeld = 0; p.MachineClock = 0;
        }

        public void AddBots(int total = 4)
        {
            for (int i = 0; Players.Count < Math.Min(total, 8); i++)
            {
                var id = $"bot-{++Serial}";
                AddPlayer(id, BotNames[i % BotNames.Length], (i + 1) % 8, true);
            }
        }

        public void SetInput(string id, Input input)
        {
            if (PlayersById.TryGetValue(id, out var p) && !p.Bot)
            {
                p.Input = input;
                p.LastInput = T;
            }
        }

        public void SetInputSanitized(string id, double? throttle, double? steer, bool fire)
            => SetInput(id, InputSanitizer.Sanitize(throttle, steer, fire));

        private void Emit(GameEvent e)
        {
            e.Id = ++Serial;
            e.T = T;
            Events.Add(e);
        }

        public void GiveWeapon(Player p, WeaponKind w)
        {
            p.Weapon = w;
            p.Ammo = Weapons.Defs[w].Ammo;
            p.Cooldown = 0.25;
            p.Active = 0;
            p.MachineClock = 0;
        }

        public void Damage(Player target, double amount, string owner, WeaponKind weapon)
        {
            // simulation.ts: damage
            if (target.Respawn > 0 || target.Shield > 0 ||
                (target.Weapon == WeaponKind.Star && target.Active > 0) ||
                target.Id == owner) return;
            target.Hp = Math.Max(0, target.Hp - amount);
            Emit(new GameEvent { Kind = "hit", X = target.X, Y = target.Y + 1, Z = target.Z, Color = Weapons.Defs[weapon].Color, Size = amount / 35, Owner = owner, Target = target.Id, Weapon = weapon });
            if (target.Hp <= 0)
            {
                PlayersById.TryGetValue(owner, out var killer);
                if (killer != null) killer.Kills++;
                target.Deaths++;
                target.Respawn = 2.5;
                target.Speed = 0;
                target.Active = 0;
                target.Frozen = 0;
                target.Weapon = null;
                target.Ammo = 0;
                Emit(new GameEvent { Kind = "smash", X = target.X, Y = target.Y + 1, Z = target.Z, Color = GameConstants.Colors[target.Color], Size = 5, Owner = owner, Target = target.Id, Weapon = weapon, Name = killer?.Name ?? "Arena", Victim = target.Name });
            }
        }

        public bool Blocked(double x, double y, double z, double r = 0)
        {
            // simulation.ts: blocked
            if (Math.Abs(x) > GameConstants.ArenaX - r || Math.Abs(z) > GameConstants.ArenaZ - r) return true;
            foreach (var o in Obstacles)
                if (y < o.H && Math.Abs(x - o.X) < o.W / 2 + r && Math.Abs(z - o.Z) < o.D / 2 + r) return true;
            return false;
        }

        private bool LineClear(double ax, double ay, double az, double bx, double by, double bz)
        {
            int n = (int)Math.Ceiling(Math.Hypot(bx - ax, bz - az) / 1);
            for (int i = 1; i < n; i++)
            {
                double t = (double)i / n;
                if (Blocked(ax + (bx - ax) * t, ay + (by - ay) * t + 0.8, az + (bz - az) * t)) return false;
            }
            return true;
        }

        private void Explode(Projectile q, Player direct = null)
        {
            double radius = q.Kind == WeaponKind.Nuke ? 10 : q.Kind == WeaponKind.Lobgrenuke ? 7 : q.Kind == WeaponKind.Cannon ? 5 : 3.5;
            Emit(new GameEvent { Kind = "explosion", X = q.X, Y = q.Y, Z = q.Z, Color = Weapons.Defs[q.Kind].Color, Size = radius, Owner = q.Owner, Weapon = q.Kind });
            foreach (var p in Players.ToList())
            {
                double dist = Math.Sqrt(Math.Pow(p.X - q.X, 2) + Math.Pow(p.Z - q.Z, 2) + Math.Pow(p.Y + 0.7 - q.Y, 2));
                if (p == direct)
                {
                    Damage(p, q.Kind == WeaponKind.Rockets ? 50 : 150, q.Owner, q.Kind);
                    continue;
                }
                if (dist < radius && LineClear(q.X, q.Y, q.Z, p.X, p.Y, p.Z))
                {
                    double max = q.Kind == WeaponKind.Nuke ? 150 : q.Kind == WeaponKind.Lobgrenuke ? 120 : q.Kind == WeaponKind.Cannon ? 45 : 25;
                    Damage(p, max * (1 - dist / radius), q.Owner, q.Kind);
                }
            }
        }

        public void Shoot(Player p)
        {
            // simulation.ts: shoot
            var w = p.Weapon;
            if (w == null || p.Cooldown > 0 || p.Ammo <= 0 || p.Frozen > 0) return;
            var kind = w.Value;
            if (kind == WeaponKind.Star || kind == WeaponKind.Spiky)
            {
                if (p.Active <= 0)
                {
                    p.Active = kind == WeaponKind.Star ? 5 : 6;
                    p.Ammo = 0;
                    Emit(new GameEvent { Kind = "shield", X = p.X, Y = p.Y + 1, Z = p.Z, Color = Weapons.Defs[kind].Color, Size = 4, Owner = p.Id, Weapon = kind });
                }
                return;
            }
            if (kind == WeaponKind.Machinegun)
            {
                if (p.Active <= 0) { p.Active = 6.5; p.MachineClock = 0; }
                return;
            }
            p.Ammo--;
            p.Cooldown = kind == WeaponKind.Bullets ? 0.22 : kind == WeaponKind.Snow ? 0.3 : kind == WeaponKind.Cannon ? 0.42 : 0.35;
            int count = kind == WeaponKind.Bullets ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                double a = p.Angle + (kind == WeaponKind.Bullets ? (i - 1) * 0.035 : 0);
                double dx = Math.Sin(a), dz = Math.Cos(a);
                double speed = kind == WeaponKind.Bullets ? 70 : kind == WeaponKind.Rockets ? 43 : kind == WeaponKind.Nuke ? 30 : kind == WeaponKind.Snow ? 40 : 22;
                if (kind == WeaponKind.Cannon) speed = 17 + Math.Min(p.FireHeld, 1.7) * 9;
                bool dropped = kind == WeaponKind.Mines || kind == WeaponKind.Fake;
                if (dropped) speed = 0;
                double distance = dropped ? -2.5 : 2.3;
                var q = new Projectile
                {
                    Id = ++Serial, Owner = p.Id, Kind = kind,
                    X = p.X + dx * distance, Y = p.Y + 1, Z = p.Z + dz * distance,
                    Vx = dx * speed, Vy = kind == WeaponKind.Cannon ? 12 : kind == WeaponKind.Lobgrenuke ? 9 : 0, Vz = dz * speed,
                    Life = dropped ? 30 : kind == WeaponKind.Lobgrenuke ? 4.8 : kind == WeaponKind.Snow ? 1.5 : 3,
                    Age = 0, Armed = false,
                };
                if (dropped) q.Y = GameConstants.GroundHeight(q.X, q.Z) + 0.25;
                Projectiles.Add(q);
                Emit(new GameEvent { Kind = "shot", X = q.X, Y = q.Y, Z = q.Z, Color = Weapons.Defs[kind].Color, Size = kind == WeaponKind.Bullets ? 0.6 : 1.3, Owner = p.Id, Weapon = kind });
            }
            if (p.Ammo <= 0) p.Weapon = null;
        }

        public Input BotInput(Player p)
        {
            // simulation.ts: botInput
            var enemies = Players.Where(q => q.Id != p.Id && q.Respawn <= 0).ToList();
            double tx, tz;
            if (p.Weapon == null)
            {
                var box = Boxes.Where(b => b.Ready <= T).OrderBy(b => Math.Hypot(p.X - b.X, p.Z - b.Z)).FirstOrDefault();
                if (box != null) { tx = box.X; tz = box.Z; }
                else { tx = 0; tz = 25; }
            }
            else
            {
                var e = enemies.OrderBy(q => Math.Hypot(p.X - q.X, p.Z - q.Z)).FirstOrDefault();
                if (e != null) { tx = e.X; tz = e.Z; }
                else { tx = 0; tz = 25; }
            }
            double desired = Math.Atan2(tx - p.X, tz - p.Z);
            const double look = 6;
            if (Blocked(p.X + Math.Sin(p.Angle) * look, p.Y, p.Z + Math.Cos(p.Angle) * look, 1.5))
                desired = p.Angle + 1.6;
            double delta = GameConstants.AngleDiff(desired, p.Angle);
            double dist = Math.Hypot(p.X - tx, p.Z - tz);
            bool fire = p.Weapon != null && (
                p.Weapon == WeaponKind.Mines || p.Weapon == WeaponKind.Fake ||
                (dist < 27 && Math.Abs(delta) < 0.45) ||
                (p.Weapon == WeaponKind.Machinegun && dist < 16) ||
                (p.Weapon == WeaponKind.Star && dist < 13) ||
                (p.Weapon == WeaponKind.Spiky && dist < 13));
            return new Input { Throttle = 1, Steer = GameConstants.Clamp(delta * 1.5, -1, 1), Fire = fire };
        }

        public void Step(double dt = 1.0 / GameConstants.TickRate)
        {
            // simulation.ts: step
            dt = GameConstants.Clamp(dt, 0, 0.1);
            T += dt;
            Elapsed += dt;
            Events = Events.Where(e => T - e.T < 1.1).ToList();
            if (Elapsed > GameConstants.RoundSeconds + 10)
            {
                Elapsed = -3;
                Round++;
                Projectiles = new List<Projectile>();
                Events = new List<GameEvent>();
                foreach (var b in Boxes) b.Ready = 0;
                foreach (var p in Players) { p.Kills = 0; p.Deaths = 0; Spawn(p); }
            }
            if (Phase != "playing") return;

            foreach (var p in Players.ToList())
            {
                if (p.Respawn > 0)
                {
                    p.Respawn -= dt;
                    if (p.Respawn <= 0) Spawn(p);
                    continue;
                }
                p.Shield = Math.Max(0, p.Shield - dt);
                p.Cooldown = Math.Max(0, p.Cooldown - dt);
                p.Frozen = Math.Max(0, p.Frozen - dt);
                var input = p.Bot ? BotInput(p) : (T - p.LastInput > 0.5 ? Input.Neutral : p.Input);
                p.FireHeld = input.Fire ? p.FireHeld + dt : 0;
                if (p.Frozen <= 0)
                {
                    double target = input.Throttle * (input.Throttle >= 0 ? 23 : 11);
                    p.Speed += (target - p.Speed) * Math.Min(1, dt * (input.Throttle != 0 ? 2.8 : 4));
                    p.Angle += input.Steer * 2.5 * GameConstants.Clamp(Math.Abs(p.Speed) / 7, 0, 1) * Math.Sign(p.Speed == 0 ? 1 : p.Speed) * dt;
                    double dx = Math.Sin(p.Angle) * p.Speed * dt;
                    double dz = Math.Cos(p.Angle) * p.Speed * dt;
                    if (!Blocked(p.X + dx, p.Y, p.Z, 1.1)) p.X += dx; else p.Speed *= 0.5;
                    if (!Blocked(p.X, p.Y, p.Z + dz, 1.1)) p.Z += dz; else p.Speed *= 0.5;
                    double ground = GameConstants.GroundHeight(p.X, p.Z);
                    p.Vy -= 24 * dt;
                    p.Y += p.Vy * dt;
                    if (p.Y <= ground) { p.Y = ground; p.Vy = 0; }
                }
                else p.Speed = 0;

                if (p.Active > 0)
                {
                    p.Active = Math.Max(0, p.Active - dt);
                    if (p.Weapon == WeaponKind.Machinegun)
                    {
                        p.MachineClock -= dt;
                        while (p.MachineClock <= 0 && p.Ammo > 0)
                        {
                            p.MachineClock += 6.5 / 166;
                            p.Ammo--;
                            var targets = Players.Where(q => q.Id != p.Id && q.Respawn <= 0 && Math.Hypot(q.X - p.X, q.Z - p.Z) < 17 && LineClear(p.X, p.Y, p.Z, q.X, q.Y, q.Z))
                                .OrderBy(q => Math.Hypot(q.X - p.X, q.Z - p.Z)).ToList();
                            var target = targets.FirstOrDefault();
                            double dx = Math.Sin(p.Angle), dz = Math.Cos(p.Angle);
                            Emit(new GameEvent { Kind = "shot", X = p.X + dx * 1.8, Y = p.Y + 1.3, Z = p.Z + dz * 1.8, Color = "#edff86", Size = 0.4, Owner = p.Id, Weapon = WeaponKind.Machinegun, Tx = target != null ? target.X : p.X + dx * 15, Ty = (target != null ? target.Y : p.Y) + 1, Tz = target != null ? target.Z : p.Z + dz * 15 });
                            if (target != null)
                                Damage(target, 1.25 * GameConstants.Clamp(1 - Math.Hypot(target.X - p.X, target.Z - p.Z) / 22, 0.2, 1), p.Id, WeaponKind.Machinegun);
                        }
                    }
                    if (p.Active == 0 || (p.Weapon == WeaponKind.Machinegun && p.Ammo == 0))
                    {
                        p.Active = 0; p.Weapon = null; p.Ammo = 0;
                    }
                }

                if (input.Fire) Shoot(p);

                if (p.Weapon == null)
                {
                    var b = Boxes.FirstOrDefault(box => box.Ready <= T && Math.Hypot(box.X - p.X, box.Z - p.Z) < 2 && Math.Abs(GameConstants.GroundHeight(box.X, box.Z) - p.Y) < 1.5);
                    if (b != null)
                    {
                        b.Ready = T + 5;
                        var w = Weapons.Loot[(int)Math.Floor(Random() * Weapons.Loot.Length)];
                        GiveWeapon(p, w);
                        Emit(new GameEvent { Kind = "pickup", X = p.X, Y = p.Y + 1, Z = p.Z, Color = Weapons.Defs[w].Color, Size = 1, Owner = p.Id, Weapon = w });
                    }
                }
            }

            var alive = Players.Where(p => p.Respawn <= 0).ToList();
            for (int i = 0; i < alive.Count; i++)
                for (int j = i + 1; j < alive.Count; j++)
                {
                    var a = alive[i]; var b = alive[j];
                    if (a.Respawn > 0 || b.Respawn > 0) continue;
                    double dist = Math.Hypot(a.X - b.X, a.Z - b.Z);
                    if (Math.Abs(a.Y - b.Y) > 2) continue;
                    if (dist < 4.4)
                    {
                        if (a.Weapon == WeaponKind.Spiky && a.Active > 0) Damage(b, 150, a.Id, WeaponKind.Spiky);
                        if (b.Weapon == WeaponKind.Spiky && b.Active > 0) Damage(a, 150, b.Id, WeaponKind.Spiky);
                    }
                    if (dist < 2.4)
                    {
                        if (a.Weapon == WeaponKind.Star && a.Active > 0) Damage(b, 150, a.Id, WeaponKind.Star);
                        if (b.Weapon == WeaponKind.Star && b.Active > 0) Damage(a, 150, b.Id, WeaponKind.Star);
                        if (a.Frozen > 0) Damage(a, 150, b.Id, WeaponKind.Snow);
                        if (b.Frozen > 0) Damage(b, 150, a.Id, WeaponKind.Snow);
                        double angle = Math.Atan2(a.X - b.X, a.Z - b.Z);
                        double push = (2.4 - dist) / 2;
                        foreach (var (pl, dir) in new[] { (a, 1.0), (b, -1.0) })
                        {
                            double x = pl.X + Math.Sin(angle) * push * dir;
                            double z = pl.Z + Math.Cos(angle) * push * dir;
                            if (!Blocked(x, pl.Y, z, 1.1)) { pl.X = x; pl.Z = z; }
                        }
                    }
                }
            StepProjectiles(dt);
        }

        private void StepProjectiles(double dt)
        {
            // simulation.ts: stepProjectiles
            var kept = new List<Projectile>();
            foreach (var q in Projectiles)
            {
                q.Life -= dt;
                q.Age += dt;
                bool lob = q.Kind == WeaponKind.Cannon || q.Kind == WeaponKind.Lobgrenuke;
                bool drop = q.Kind == WeaponKind.Mines || q.Kind == WeaponKind.Fake;
                if (q.Life <= 0)
                {
                    if (q.Kind == WeaponKind.Lobgrenuke || q.Kind == WeaponKind.Nuke || q.Kind == WeaponKind.Rockets) Explode(q);
                    continue;
                }
                if (lob && !q.Armed) q.Vy -= 18 * dt;
                int steps = Math.Max(1, (int)Math.Ceiling(Math.Hypot(q.Vx * dt, q.Vz * dt) / 0.6));
                bool alive = true;
                for (int i = 0; i < steps && alive; i++)
                {
                    q.X += (q.Vx * dt) / steps;
                    q.Z += (q.Vz * dt) / steps;
                    q.Y += (q.Vy * dt) / steps;
                    double ground = GameConstants.GroundHeight(q.X, q.Z);
                    if (q.Kind == WeaponKind.Lobgrenuke && (q.Y <= ground + 0.35 || Blocked(q.X, q.Y, q.Z)))
                    {
                        q.Y = Math.Max(ground + 0.35, q.Y);
                        q.Vx = q.Vy = q.Vz = 0;
                        q.Armed = true;
                    }
                    if (!drop && q.Kind != WeaponKind.Lobgrenuke && (Blocked(q.X, q.Y, q.Z) || q.Y < ground + 0.1))
                    {
                        if (q.Kind == WeaponKind.Rockets || q.Kind == WeaponKind.Nuke || q.Kind == WeaponKind.Cannon) Explode(q);
                        else Emit(new GameEvent { Kind = "hit", X = q.X, Y = q.Y, Z = q.Z, Color = Weapons.Defs[q.Kind].Color, Size = 0.5, Weapon = q.Kind });
                        alive = false;
                        break;
                    }
                    foreach (var p in Players.ToList())
                    {
                        if (p.Id == q.Owner || p.Respawn > 0) continue;
                        double distance = Math.Sqrt(Math.Pow(p.X - q.X, 2) + Math.Pow(p.Z - q.Z, 2) + Math.Pow(p.Y + 0.75 - q.Y, 2));
                        double radius = q.Kind == WeaponKind.Lobgrenuke && q.Armed ? 3 : drop ? 1.6 : 1.4;
                        if (distance > radius || (drop && q.Age < 0.5)) continue;
                        if (q.Kind == WeaponKind.Snow)
                        {
                            if (p.Shield <= 0 && !(p.Weapon == WeaponKind.Star && p.Active > 0))
                            {
                                p.Frozen = 3;
                                if (p.Weapon == WeaponKind.Spiky) { p.Active = 0; p.Weapon = null; p.Ammo = 0; }
                                Damage(p, 7, q.Owner, WeaponKind.Snow);
                                Emit(new GameEvent { Kind = "freeze", X = p.X, Y = p.Y + 1, Z = p.Z, Color = "#a9f4ff", Size = 2, Owner = q.Owner, Target = p.Id, Weapon = WeaponKind.Snow });
                            }
                            alive = false;
                            break;
                        }
                        if (q.Kind == WeaponKind.Bullets) { Damage(p, 7, q.Owner, q.Kind); alive = false; break; }
                        if (drop)
                        {
                            Damage(p, 150, q.Owner, q.Kind);
                            Emit(new GameEvent { Kind = "explosion", X = q.X, Y = q.Y, Z = q.Z, Color = Weapons.Defs[q.Kind].Color, Size = 3, Owner = q.Owner, Weapon = q.Kind });
                            alive = false;
                            break;
                        }
                        Explode(q, p);
                        alive = false;
                        break;
                    }
                }
                if (alive && Math.Abs(q.X) < GameConstants.ArenaX + 3 && Math.Abs(q.Z) < GameConstants.ArenaZ + 3) kept.Add(q);
            }
            Projectiles = kept;
        }

        public Snapshot Snapshot()
        {
            // simulation.ts: snapshot
            double remaining = Phase == "countdown" ? -Elapsed : Phase == "results" ? GameConstants.RoundSeconds + 10 - Elapsed : GameConstants.RoundSeconds - Elapsed;
            return new Snapshot
            {
                Code = Code, T = T, Remaining = remaining, Phase = Phase, Round = Round,
                Players = Players.Select(p => p.Copy()).ToList(),
                Projectiles = Projectiles.Select(q => q.Copy()).ToList(),
                Boxes = Boxes.Select(b => b.Copy()).ToList(),
                Events = Events.Select(e => e.Copy()).ToList(),
            };
        }
    }

    // Coordinate conversion (master skill §14.6): three.js raw data -> Unity.
    // Apply ONLY to raw TS numbers, never on top of imported meshes.
    public static class FromThree
    {
        public static (double x, double y, double z) Position(double x, double y, double z) => (x, y, -z);
    }
}
