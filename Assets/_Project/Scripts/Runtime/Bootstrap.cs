// Entry point that needs no scene wiring: on scene load, if no GameManager
// exists, create one. This makes the project playable from an empty scene
// while a hand-authored Main.unity is still a Phase 7 polish task.
// Do not add a second GameManager in the scene while this is active.
using UnityEngine;

namespace SmashKarts.Runtime
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartGame()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return;
            var go = new GameObject("Game");
            go.AddComponent<GameManager>();
        }
    }
}
