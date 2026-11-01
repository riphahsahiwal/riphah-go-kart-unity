// Vertical-slice match driver: local bot practice, as in connection.ts
// practice mode — GameRoom, player 'you', bots to 5 total, 30 Hz tick,
// snapshot each tick, views synced from the snapshot.
// Online play is OnlineClient.cs (Phase 9); this file does not fake it.
using UnityEngine;
using SmashKarts.Core;
using System.Collections.Generic;
using System.Linq;

namespace SmashKarts.Runtime
{
    public sealed class GameManager : MonoBehaviour
    {
        public string PlayerName = "Driver";
        public int PlayerColor = 0;

        private GameRoom _room;
        private OnlineClient _online;
        private Snapshot _onlineSnapshot;
        private string _onlineMyId = "you";
        private bool _isOnline;
        private ArenaBuilder _arena;
        private Effects _effects;
        private SynthAudio _audio;
        private CameraFollow _camera;
        private GameUI _ui;
        private LobbyUI _lobby;
        private Dictionary<string, KartView> _karts = new();
        private Dictionary<int, GameObject> _shots = new();
        private HashSet<int> _seenEvents = new();
        private float _acc, _clock;

        private void Start()
        {
            // Main camera + lights are set up by ArenaBuilder; ensure a camera exists.
            if (Camera.main == null) new GameObject("Main Camera").AddComponent<Camera>();
            Camera.main.fieldOfView = 57; Camera.main.nearClipPlane = 0.1f; Camera.main.farClipPlane = 350f;
            Camera.main.transform.position = new Vector3(54, 42, 62);
            Camera.main.transform.LookAt(Vector3.zero);

            _arena = gameObject.AddComponent<ArenaBuilder>(); _arena.Build();
            _effects = gameObject.AddComponent<Effects>();
            _audio = gameObject.AddComponent<SynthAudio>(); _audio.StartAudio();
            _camera = gameObject.AddComponent<CameraFollow>();
            _ui = gameObject.AddComponent<GameUI>(); _ui.Build();

            // Lobby first (Phase 7). The match starts only when the player
            // chooses practice or an online join. Until then the camera runs
            // in demo orbit mode, as in the source lobby.
            var lobby = gameObject.AddComponent<LobbyUI>();
            _lobby = lobby;
            _online = gameObject.AddComponent<OnlineClient>();
            _online.OnSnapshot += snap => _onlineSnapshot = snap;
            _online.OnJoined += (id, code) => { _onlineMyId = id; _lobby?.Hide(); };
            _online.OnError += msg => _lobby?.ShowStatus(msg);
            lobby.Build(this, _online);
        }

        public void StartOnline(string action, string name, int color, string code, bool bots, string endpoint)
        {
            PlayerName = name; PlayerColor = color;
            _isOnline = true; _onlineMyId = "you";
            _online.Configure(endpoint, action, name, color, code, bots);
            _online.Connect();
        }

        // Test API (no gameplay change): PlayMode tests read the match
        // state through these instead of private fields.
        public bool HasPracticeRoom => _room != null;
        public Snapshot GetCurrentSnapshot() => _room != null ? _room.Snapshot() : _onlineSnapshot;

        public void StartPractice(string name, int color)
        {
            PlayerName = name; PlayerColor = color;
            _room = new GameRoom("PRACTICE");
            _room.AddPlayer("you", PlayerName, PlayerColor, false);
            _room.AddBots(5); // connection.ts practice: total 5 players, i.e. 4 bots.
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            if (_isOnline)
            {
                _online.SetInput(InputProvider.Get());
                if (_onlineSnapshot != null) SyncOnline(_onlineSnapshot);
                else _camera.UpdateCamera(null, null, Time.deltaTime);
                return;
            }
            if (_room == null)
            {
                _camera.UpdateCamera(null, null, Time.deltaTime);
                return;
            }
            _acc += Mathf.Min(Time.deltaTime, 0.2f);
            while (_acc >= 1f / 30f)
            {
                _room.SetInput("you", InputProvider.Get());
                _room.Step();
                _acc -= 1f / 30f;
            }
            var snap = _room.Snapshot();
            Sync(snap);
        }

        private void SyncOnline(Snapshot snap) => Sync(snap);

        private void Sync(Snapshot snap)
        {
            // Remove departed karts / shots.
            foreach (var id in _karts.Keys.Where(id => snap.Players.All(p => p.Id != id)).ToList())
            { Destroy(_karts[id].gameObject); _karts.Remove(id); }
            foreach (var id in _shots.Keys.Where(id => snap.Projectiles.All(q => q.Id != id)).ToList())
            { Destroy(_shots[id]); _shots.Remove(id); }

            Player me = snap.Players.FirstOrDefault(p => p.Id == _onlineMyId);
            foreach (var p in snap.Players)
            {
                if (!_karts.TryGetValue(p.Id, out var view)) { view = KartView.Create(p); _karts[p.Id] = view; }
                view.Sync(p, Time.deltaTime, _clock, _onlineMyId);
                if (p.Id == _onlineMyId) _audio.SetSpeed(p.Speed);
            }

            // Loot boxes: visible + rotating + bobbing, as in renderer.ts.
            for (int i = 0; i < _arena.LootBoxes.Count && i < snap.Boxes.Count; i++)
            {
                var box = snap.Boxes[i];
                var go = _arena.LootBoxes[i];
                go.SetActive(box.Ready <= snap.T);
                go.transform.rotation = Quaternion.Euler(0, _clock * 0.9f + i, 0);
                var pos = go.transform.position;
                go.transform.position = new Vector3(pos.x, (float)(GameConstants.GroundHeight(box.X, box.Z) + 1.35 + Mathf.Sin(_clock * 2 + i) * 0.2), pos.z);
            }

            // Projectiles.
            foreach (var q in snap.Projectiles)
            {
                if (!_shots.TryGetValue(q.Id, out var go))
                {
                    go = MakeProjectile(q);
                    _shots[q.Id] = go;
                }
                go.transform.position = Coord.ToUnity(q);
                var vel = new Vector3((float)q.Vx, (float)q.Vy, (float)q.Vz);
                if (vel.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(vel);
            }

            // New events -> effects + audio + camera shake.
            foreach (var e in snap.Events)
            {
                if (!_seenEvents.Add(e.Id)) continue;
                _effects.OnEvent(e);
                if (e.Kind == "shot") _audio.Shot(e.Weapon == WeaponKind.Cannon || e.Weapon == WeaponKind.Nuke);
                else if (e.Kind == "explosion" || e.Kind == "smash") _audio.Explosion(e.Size / 5f);
                else if (e.Kind == "pickup") _audio.Pickup();
                if (e.Kind == "explosion" && me != null) _camera.Shake = Mathf.Max(_camera.Shake, (float)e.Size * 0.05f);
            }
            // Events older than the snapshot window are dropped by Core; prune seen set occasionally.
            if (_seenEvents.Count > 500) _seenEvents.Clear();

            _camera.UpdateCamera(me, me != null && _karts.TryGetValue(me.Id, out var mv) ? mv.transform : null, Time.deltaTime);
            _ui.UpdateUI(snap, me);
        }

        private GameObject MakeProjectile(Projectile q)
        {
            var col = Weapons.Defs[q.Kind].Color;
            GameObject go;
            if (q.Kind == WeaponKind.Mines || q.Kind == WeaponKind.Fake)
            {
                go = new GameObject("Projectile");
                Coord.Box(go.transform, 1.65f, 1.65f, 1.65f, 0, 0, 0, "#bacf46");
            }
            else if (q.Kind == WeaponKind.Bullets || q.Kind == WeaponKind.Snow)
            {
                go = Coord.Primitive(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.9f, col);
            }
            else
            {
                // Parent + child: child cylinder height is along local Z, so the
                // parent's LookRotation(velocity) in Sync points the missile
                // along its flight path. Without this, LookRotation would leave
                // the cylinder sideways.
                go = new GameObject("Projectile");
                var child = Coord.Primitive(PrimitiveType.Cylinder, Vector3.zero,
                    q.Kind == WeaponKind.Nuke ? new Vector3(1.7f, 2.5f, 1.7f) : new Vector3(0.5f, 1.3f, 0.5f), col, go.transform);
                child.transform.rotation = Quaternion.Euler(90, 0, 0);
            }
            return go;
        }
    }
}
