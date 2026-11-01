# Riphah Go Kart Unity — Smash Karts Arena port

Unity 6 LTS, C#, URP port of the TypeScript game in
https://github.com/riphahsahiwal/riphah-go-karts
(original: https://github.com/amsminn/gpt-6-astra-smash-karts).

See `../PARITY_MATRIX.md` (in the port working folder) for the full
feature-parity matrix, exact constants, weapon tables, and accepted differences.
That matrix is the contract for this port; do not retune balance during the port.

## How to open

1. Unity Hub, Unity 6 LTS (6000.x). Confirm the version in
   `ProjectSettings/ProjectVersion.txt` — the pinned project version wins
   over any version named in a skill file.
2. Open this folder as a Unity project. Let URP / Input System packages import.
3. Press Play. The lobby appears first (driver name, colour, quick/create/join/practice, help, settings). Choose 혼자 연습하기 for bot practice (local player + 4 bots). A Bootstrap component creates the GameManager on scene load, so no scene wiring is needed; a hand-authored Main.unity is still a polish task.
   Online play connects to the original Node game server (see below).

## Controls

W / Up accelerate, S / Down brake/reverse, A/D / Left/Right steer,
Space fire (hold to charge cannon), Tab leaderboard, Esc menu (match continues).
Touch buttons appear on touch devices (planned in GameUI).

## Structure

- `Assets/_Project/Scripts/Core/` — pure C#, no UnityEngine dependency.
  `Simulation.cs` is a faithful port of `game/game/simulation.ts`.
  Logic uses `double` for parity; Unity boundary converts to `float`.
- `Assets/_Project/Scripts/Runtime/` — MonoBehaviour adapters: arena/kart
  building, camera, UI, audio synthesis, local match, online WebSocket client.
- `Assets/_Project/Scripts/Tests/EditMode/` — ported simulation tests.
- `Assets/_Project/Scenes/Main.unity` — bootstrap scene; the arena, karts,
  boxes, and effects are built procedurally at runtime, as in the source.

## Online play (v1)

The Unity client speaks the same WebSocket JSON protocol as the original
Node server (`/ws`): `hello`, `join`, `resume`, `input`, `ping`, `state`,
`pong`, `error`. It does NOT replace the server. Run the original server
(`game/server/index.ts`, port 3001) and set the server URL in Settings.
Discord Activity is out of scope for v1.

## Verification status — read before claiming done

- Core simulation: authored from the TS source, file-checked here.
  NOT compiled or Play Mode tested here: no Unity Editor and no .NET SDK
  are available on the authoring machine. Run EditMode tests and a Play
  Mode run in Unity 6 before claiming parity.
- See PARITY_MATRIX.md § Verification plan and § Accepted differences.
