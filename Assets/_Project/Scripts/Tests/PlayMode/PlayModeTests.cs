// PlayMode tests: Runtime integration while the game runs.
// These run in the Unity Test Runner (PlayMode), not on the authoring
// machine: no Unity Editor is available here, so they are file-checked
// only until CI runs them. They need no hand-authored scene: the test
// runner provides an empty scene, and GameManager/ArenaBuilder/KartView
// build their objects procedurally, as in the game itself.
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Linq;
using SmashKarts.Core;
using SmashKarts.Runtime;

namespace SmashKarts.Tests
{
    public sealed class PlayModeTests
    {
        private GameObject _root;

        [TearDown]
        public void Cleanup()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }

        [UnityTest]
        public IEnumerator ArenaBuildsSeventeenLootBoxes()
        {
            _root = new GameObject("ArenaTest");
            var arena = _root.AddComponent<ArenaBuilder>();
            arena.Build();
            Assert.AreEqual(17, arena.LootBoxes.Count);
            Assert.Greater(_root.transform.childCount, 50);
            yield return null;
        }

        [UnityTest]
        public IEnumerator KartViewCreatesFourWheels()
        {
            var p = new Player { Id = "t1", Name = "Tester", Color = 0, X = 0, Y = 0, Z = 0, Angle = 0 };
            var view = KartView.Create(p);
            _root = view.gameObject;
            Assert.AreEqual(4, view.Wheels.Count);
            Assert.IsNotNull(view.Body);
            Assert.AreEqual("t1", view.PlayerId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PracticeStartsFivePlayersAndBotsMove()
        {
            _root = new GameObject("GameManagerTest");
            var manager = _root.AddComponent<GameManager>();
            yield return null; // GameManager.Start builds arena, UI, lobby.
            Assert.IsNotNull(manager.GetComponent<LobbyUI>());

            manager.StartPractice("Tester", 0);
            var before = manager.GetCurrentSnapshot();
            Assert.IsNotNull(before);
            Assert.AreEqual(5, before.Players.Count); // player + 4 bots.
            var botBefore = before.Players.First(p => p.Bot);
            var bx = botBefore.X; var bz = botBefore.Z;

            yield return new WaitForSeconds(1.0f); // 30 Hz ticks run in Update.
            var after = manager.GetCurrentSnapshot();
            Assert.IsNotNull(after);
            var botAfter = after.Players.First(p => p.Id == botBefore.Id);
            var moved = System.Math.Sqrt((botAfter.X - bx) * (botAfter.X - bx) + (botAfter.Z - bz) * (botAfter.Z - bz));
            Assert.Greater(moved, 0.1, "A bot should have driven away from its spawn in 1 second.");
        }
    }
}
