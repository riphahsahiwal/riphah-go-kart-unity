// Port of game/tests/simulation.test.ts (Unity Test Framework / NUnit).
// Fixture matches the TS fixture: fixed random 0.4, elapsed 0, boxes disabled,
// players Alpha at (10,-10) angle 0 and Bravo at (10,0) angle pi, shields 0.
// These tests run in Unity (EditMode), not on the authoring machine:
// no Unity Editor / .NET SDK is available here, so they are file-checked only.
using NUnit.Framework;
using SmashKarts.Core;
using System.Linq;

namespace SmashKarts.Tests
{
    public sealed class SimulationTests
    {
        private (GameRoom r, Player a, Player b) Fixture()
        {
            var r = new GameRoom("TEST", () => 0.4);
            r.Elapsed = 0;
            foreach (var box in r.Boxes) box.Ready = double.PositiveInfinity;
            var a = r.AddPlayer("a", "Alpha");
            var b = r.AddPlayer("b", "Bravo");
            a.X = 10; a.Z = -10; a.Y = 0; a.Angle = 0; a.Shield = 0;
            b.X = 10; b.Z = 0; b.Y = 0; b.Angle = System.Math.PI; b.Shield = 0;
            return (r, a, b);
        }

        private static void Advance(GameRoom r, double seconds)
        {
            for (int i = 0; i < System.Math.Ceiling(seconds * 30); i++) r.Step();
        }

        private static void Equip(GameRoom r, Player p, WeaponKind w)
        {
            r.GiveWeapon(p, w);
            p.Cooldown = 0;
        }

        [Test]
        public void InputSanitizerBoundsFiniteControlsOnly()
        {
            var s = InputSanitizer.Sanitize(double.PositiveInfinity, -999, false);
            Assert.AreEqual(0, s.Throttle);
            Assert.AreEqual(-1, s.Steer);
            Assert.AreEqual(false, s.Fire);
            var n = InputSanitizer.Sanitize(null, null, false);
            Assert.AreEqual(0, n.Throttle);
            Assert.AreEqual(0, n.Steer);
        }

        [Test]
        public void DriveReverseAndStaleInput()
        {
            var (r, a, _) = Fixture();
            r.SetInput("a", new Input { Throttle = 1, Steer = 0, Fire = false });
            Advance(r, 0.4);
            Assert.Greater(a.Z, -10);
            double speed = a.Speed;
            Advance(r, 1);
            Assert.Less(a.Speed, speed / 3);
            r.SetInput("a", new Input { Throttle = -1, Steer = 0, Fire = false });
            Advance(r, 0.4);
            Assert.Less(a.Speed, 0);
        }

        [Test]
        public void BoundariesAndCoverPreventDrivingThroughWalls()
        {
            var (r, a, _) = Fixture();
            a.X = 45; a.Z = -10; a.Angle = System.Math.PI / 2; a.Speed = 23;
            r.SetInput("a", new Input { Throttle = 1 });
            Advance(r, 0.4);
            Assert.Less(a.X, 46);
            Assert.IsTrue(r.Blocked(-21, 0, -16));
            Assert.IsFalse(r.Blocked(-21, 5, -16));
        }

        [Test]
        public void RampsHaveMatchingElevatedSurface()
        {
            Assert.AreEqual(3, GameConstants.GroundHeight(0, 0));
            Assert.AreEqual(0, GameConstants.GroundHeight(0, 19));
            Assert.Greater(GameConstants.GroundHeight(0, 12), 1);
            Assert.Less(GameConstants.GroundHeight(0, 12), 2);
            Assert.AreEqual(0, GameConstants.GroundHeight(10, 0));
        }

        [Test]
        public void RoomCapsAtEightAndNamesAtSixteen()
        {
            var r = new GameRoom();
            for (int i = 0; i < GameConstants.MaxPlayers; i++)
                Assert.IsNotNull(r.AddPlayer(i.ToString(), "abcdefghijklmnopEXTRA"));
            Assert.IsNull(r.AddPlayer("extra", "Extra"));
            Assert.AreEqual(16, r.PlayersById["0"].Name.Length);
        }

        [Test]
        public void TwoDirectRocketsKillAndRespawnWithoutLosingScore()
        {
            var (r, a, b) = Fixture();
            Equip(r, a, WeaponKind.Rockets);
            r.Shoot(a);
            Advance(r, 0.3);
            Assert.AreEqual(50, b.Hp);
            a.Cooldown = 0;
            r.Shoot(a);
            Advance(r, 0.3);
            Assert.AreEqual(0, b.Hp);
            Assert.AreEqual(1, a.Kills);
            Assert.AreEqual(1, b.Deaths);
            Assert.Greater(b.Respawn, 0);
            Advance(r, 2.6);
            Assert.AreEqual(100, b.Hp);
            Assert.Greater(b.Shield, 0);
            Assert.AreEqual(1, b.Deaths);
        }

        [Test]
        public void BulletBurstUsesThreeProjectilesAndSevenDamageEach()
        {
            var (r, a, b) = Fixture();
            Equip(r, a, WeaponKind.Bullets);
            r.Shoot(a);
            Assert.AreEqual(3, r.Projectiles.Count);
            Assert.AreEqual(8, a.Ammo);
            Advance(r, 0.2);
            Assert.AreEqual(79, b.Hp);
        }

        [Test]
        public void SpikyContactIsLethalButDoesNotGrantInvincibility()
        {
            var (r, a, b) = Fixture();
            Equip(r, a, WeaponKind.Spiky);
            r.Shoot(a);
            Assert.AreEqual(6, a.Active);
            b.Z = -7;
            r.Step();
            Assert.AreEqual(0, b.Hp);
            r.Damage(a, 50, b.Id, WeaponKind.Rockets);
            Assert.AreEqual(50, a.Hp);
        }

        [Test]
        public void StarAndSpawnProtectionRejectDamageAndStarExpires()
        {
            var (r, a, b) = Fixture();
            a.Shield = 1;
            r.Damage(a, 150, b.Id, WeaponKind.Nuke);
            Assert.AreEqual(100, a.Hp);
            a.Shield = 0;
            Equip(r, a, WeaponKind.Star);
            r.Shoot(a);
            r.Damage(a, 150, b.Id, WeaponKind.Nuke);
            Assert.AreEqual(100, a.Hp);
            Advance(r, 5.1);
            Assert.IsNull(a.Weapon);
            r.Damage(a, 50, b.Id, WeaponKind.Rockets);
            Assert.AreEqual(50, a.Hp);
        }

        [Test]
        public void SnowFreezesAndCollisionShattersFrozenKart()
        {
            var (r, a, b) = Fixture();
            Equip(r, a, WeaponKind.Snow);
            r.Shoot(a);
            Advance(r, 0.25);
            Assert.Greater(b.Frozen, 2.8);
            Assert.AreEqual(93, b.Hp);
            a.Z = b.Z - 2; a.Shield = 0;
            r.Step();
            Assert.AreEqual(0, b.Hp);
            Assert.AreEqual(1, a.Kills);
        }

        [Test]
        public void MachinegunRunsWithRangeAttenuation()
        {
            var (r, a, b) = Fixture();
            Equip(r, a, WeaponKind.Machinegun);
            r.Shoot(a);
            Advance(r, 1);
            Assert.Less(a.Ammo, 150);
            Assert.Greater(a.Ammo, 130);
            Assert.Less(b.Hp, 100);
            Assert.Greater(a.Active, 5);
            Advance(r, 5.6);
            Assert.IsNull(a.Weapon);
            Assert.AreEqual(0, a.Ammo);
        }

        [Test]
        public void CannonFiresFourArcingShotsAndHoldingExtendsRange()
        {
            var (r, a, _) = Fixture();
            Equip(r, a, WeaponKind.Cannon);
            r.Shoot(a);
            var first = r.Projectiles[0];
            Assert.Greater(first.Vy, 0);
            a.FireHeld = 1.5; a.Cooldown = 0;
            r.Shoot(a);
            Assert.Greater(r.Projectiles[1].Vz, first.Vz);
            a.Cooldown = 0; r.Shoot(a);
            a.Cooldown = 0; r.Shoot(a);
            Assert.AreEqual(4, r.Projectiles.Count);
            Assert.IsNull(a.Weapon);
        }

        [Test]
        public void MinesArmBehindKartAndKillEnemiesNotOwner()
        {
            var (r, a, b) = Fixture();
            Equip(r, a, WeaponKind.Mines);
            r.Shoot(a);
            var q = r.Projectiles[0];
            Assert.Less(q.Z, a.Z);
            b.X = q.X; b.Z = q.Z;
            r.Step();
            Assert.AreEqual(100, b.Hp);
            Advance(r, 0.6);
            Assert.AreEqual(0, b.Hp);
            Assert.AreEqual(100, a.Hp);
        }

        [Test]
        public void FakeLootBoxTrapIsLethalAndSingleUse()
        {
            var (r, a, b) = Fixture();
            Equip(r, a, WeaponKind.Fake);
            r.Shoot(a);
            Assert.IsNull(a.Weapon);
            var q = r.Projectiles[0];
            b.X = q.X; b.Z = q.Z;
            Advance(r, 0.6);
            Assert.AreEqual(0, b.Hp);
        }
    }
}
