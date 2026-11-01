// Online client, ported from game/game/connection.ts (Phase 9).
// Protocol is the original Node server's, unchanged:
//   server -> client: hello, joined {id, code, token}, state {state},
//                     pong {at}, error {message}
//   client -> server: join {action, name, color, code?, bots} or resume {token},
//                     input {input} at 30 Hz, ping {at} every 1.5 s, leave.
// Snapshot JSON is parsed with Newtonsoft.Json (see Packages/manifest.json)
// into Core Snapshot types, with weapon keys mapped to WeaponKind.
// Reconnect: up to 6 attempts with a token, backoff min(2000, 400*attempt) ms,
// resume window 20 s on the server. No new protocol is invented here.
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmashKarts.Core;

namespace SmashKarts.Runtime
{
    public sealed class OnlineClient : MonoBehaviour
    {
        public string Endpoint = "ws://localhost:3001/ws";
        public event Action<string, string> OnJoined; // (myId, code)
        public event Action<string> OnStatus, OnError;
        public event Action<Snapshot> OnSnapshot;
        public event Action<int> OnPing;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private string _token, _myId;
        private string _action, _name, _code;
        private int _color;
        private bool _bots, _closed;
        private int _attempts;
        private SmashKarts.Core.Input _lastInput;
        private float _inputTimer, _pingTimer;

        private static readonly Dictionary<string, WeaponKind> WeaponByKey = new()
        {
            ["rockets"] = WeaponKind.Rockets, ["bullets"] = WeaponKind.Bullets,
            ["machinegun"] = WeaponKind.Machinegun, ["cannon"] = WeaponKind.Cannon,
            ["mines"] = WeaponKind.Mines, ["nuke"] = WeaponKind.Nuke,
            ["lobgrenuke"] = WeaponKind.Lobgrenuke, ["spiky"] = WeaponKind.Spiky,
            ["star"] = WeaponKind.Star, ["snow"] = WeaponKind.Snow, ["fake"] = WeaponKind.Fake,
        };

        public void Configure(string endpoint, string action, string name, int color, string code, bool bots)
        {
            Endpoint = endpoint; _action = action; _name = name; _color = color; _code = code; _bots = bots;
        }

        public void SetInput(SmashKarts.Core.Input input) => _lastInput = input;

        public async void Connect()
        {
            _closed = false; _attempts = 0;
            await Dial();
        }

        private async Task Dial()
        {
            if (_closed) return;
            OnStatus?.Invoke(string.IsNullOrEmpty(_token) ? "서버 연결 중…" : "재접속 중…");
            _cts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            try
            {
                var connectTask = _socket.ConnectAsync(new Uri(Endpoint), _cts.Token);
                if (await Task.WhenAny(connectTask, Task.Delay(7000)) != connectTask)
                {
                    _socket.Dispose();
                    if (string.IsNullOrEmpty(_token)) { _closed = true; OnError?.Invoke("서버에 연결할 수 없습니다. 서버 실행 상태와 주소를 확인해 주세요."); }
                    else await Reconnect();
                    return;
                }
                await connectTask;
                _ = ReceiveLoop();
            }
            catch (Exception) { await Reconnect(); }
        }

        private async Task Reconnect()
        {
            if (_closed || string.IsNullOrEmpty(_token) || _attempts++ >= 6)
            {
                _closed = true;
                OnError?.Invoke(string.IsNullOrEmpty(_token)
                    ? "게임 서버에 연결하지 못했습니다. 서버 주소를 확인하거나 봇 연습을 시작하세요."
                    : "연결이 끊겼습니다. 방 코드로 다시 입장할 수 있습니다.");
                return;
            }
            OnStatus?.Invoke("연결 끊김 · 재접속 중…");
            await Task.Delay(Math.Min(2000, 400 * _attempts));
            await Dial();
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[65536];
            try
            {
                while (_socket.State == WebSocketState.Open && !_closed)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleMessage(json);
                }
            }
            catch (Exception) { /* fall through to reconnect */ }
            if (!_closed) await Reconnect();
        }

        private void HandleMessage(string json)
        {
            JObject m;
            try { m = JObject.Parse(json); } catch { return; }
            string type = (string)m["type"];
            if (type == "hello")
            {
                string payload = string.IsNullOrEmpty(_token)
                    ? $"{{\"type\":\"join\",\"action\":\"{_action}\",\"name\":{JsonString(_name)},\"color\":{_color},\"code\":{JsonString(_code ?? "")},\"bots\":{(_bots ? "true" : "false")}}}"
                    : $"{{\"type\":\"resume\",\"token\":{JsonString(_token)}}}";
                _ = SendRaw(payload);
            }
            else if (type == "joined")
            {
                _token = (string)m["token"];
                _myId = (string)m["id"];
                _attempts = 0;
                OnJoined?.Invoke(_myId, (string)m["code"]);
                OnStatus?.Invoke("온라인");
            }
            else if (type == "state")
            {
                var snap = ParseSnapshot((JObject)m["state"]);
                if (snap != null) OnSnapshot?.Invoke(snap);
            }
            else if (type == "pong")
            {
                double at = (double)m["at"];
                OnPing?.Invoke((int)Math.Round((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds - at));
            }
            else if (type == "error")
            {
                _closed = true;
                OnError?.Invoke((string)m["message"]);
            }
        }

        private static string JsonString(string s) => Newtonsoft.Json.JsonConvert.ToString(s ?? "");

        private async Task SendRaw(string json)
        {
            if (_socket?.State != WebSocketState.Open) return;
            await _socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, _cts.Token);
        }

        private void Update()
        {
            if (_socket?.State != WebSocketState.Open || string.IsNullOrEmpty(_token)) return;
            _inputTimer += Time.deltaTime;
            if (_inputTimer >= 1f / 30f)
            {
                _inputTimer = 0;
                _ = SendRaw($"{{\"type\":\"input\",\"input\":{{\"throttle\":{_lastInput.Throttle},\"steer\":{_lastInput.Steer},\"fire\":{(_lastInput.Fire ? "true" : "false")}}}}}");
            }
            _pingTimer += Time.deltaTime;
            if (_pingTimer >= 1.5f)
            {
                _pingTimer = 0;
                double nowMs = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
                _ = SendRaw($"{{\"type\":\"ping\",\"at\":{nowMs}}}");
            }
        }

        public void Close()
        {
            _closed = true;
            try { _ = SendRaw("{\"type\":\"leave\"}"); } catch { }
            try { _cts?.Cancel(); _socket?.Dispose(); } catch { }
        }

        private void OnDestroy() => Close();

        // --- Snapshot parsing (field names match the TS Snapshot exactly) ---
        private static double D(JToken t, double def = 0) => t == null ? def : (double)t;
        private static int I(JToken t, int def = 0) => t == null ? def : (int)t;
        private static string S(JToken t, string def = "") => t == null ? def : (string)t;

        private static WeaponKind? ParseWeapon(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return null;
            return WeaponByKey.TryGetValue((string)t, out var w) ? w : null;
        }

        public static Snapshot ParseSnapshot(JObject s)
        {
            if (s == null) return null;
            var snap = new Snapshot
            {
                Code = S(s["code"]), T = D(s["t"]), Remaining = D(s["remaining"]),
                Phase = S(s["phase"]), Round = I(s["round"], 1),
            };
            foreach (var pj in s["players"] ?? new JArray())
            {
                var p = new Player
                {
                    Id = S(pj["id"]), Name = S(pj["name"]), Color = I(pj["color"]),
                    Bot = (bool?)pj["bot"] ?? false,
                    X = D(pj["x"]), Y = D(pj["y"]), Z = D(pj["z"]),
                    Angle = D(pj["angle"]), Speed = D(pj["speed"]), Vy = D(pj["vy"]),
                    Hp = D(pj["hp"], 100), Kills = I(pj["kills"]), Deaths = I(pj["deaths"]),
                    Weapon = ParseWeapon(pj["weapon"]), Ammo = I(pj["ammo"]),
                    Cooldown = D(pj["cooldown"]), Active = D(pj["active"]),
                    Shield = D(pj["shield"]), Frozen = D(pj["frozen"]), Respawn = D(pj["respawn"]),
                    FireHeld = D(pj["fireHeld"]), MachineClock = D(pj["machineClock"]), LastInput = D(pj["lastInput"]),
                };
                var inp = pj["input"];
                if (inp != null) p.Input = new SmashKarts.Core.Input { Throttle = D(inp["throttle"]), Steer = D(inp["steer"]), Fire = (bool?)inp["fire"] ?? false };
                snap.Players.Add(p);
            }
            foreach (var qj in s["projectiles"] ?? new JArray())
            {
                snap.Projectiles.Add(new Projectile
                {
                    Id = I(qj["id"]), Owner = S(qj["owner"]), Kind = ParseWeapon(qj["kind"]) ?? WeaponKind.Rockets,
                    X = D(qj["x"]), Y = D(qj["y"]), Z = D(qj["z"]),
                    Vx = D(qj["vx"]), Vy = D(qj["vy"]), Vz = D(qj["vz"]),
                    Life = D(qj["life"]), Age = D(qj["age"]), Armed = (bool?)qj["armed"] ?? false,
                });
            }
            foreach (var bj in s["boxes"] ?? new JArray())
                snap.Boxes.Add(new BoxState { Id = I(bj["id"]), X = D(bj["x"]), Z = D(bj["z"]), Ready = D(bj["ready"]) });
            foreach (var ej in s["events"] ?? new JArray())
            {
                snap.Events.Add(new GameEvent
                {
                    Id = I(ej["id"]), T = D(ej["t"]), Kind = S(ej["kind"]),
                    X = D(ej["x"]), Y = D(ej["y"]), Z = D(ej["z"]),
                    Color = S(ej["color"]), Size = D(ej["size"]),
                    Owner = S(ej["owner"]), Target = S(ej["target"]),
                    Weapon = ParseWeapon(ej["weapon"]),
                    Tx = ej["tx"] == null ? null : D(ej["tx"]),
                    Ty = ej["ty"] == null ? null : D(ej["ty"]),
                    Tz = ej["tz"] == null ? null : D(ej["tz"]),
                    Name = S(ej["name"]), Victim = S(ej["victim"]),
                });
            }
            return snap;
        }
    }
}
