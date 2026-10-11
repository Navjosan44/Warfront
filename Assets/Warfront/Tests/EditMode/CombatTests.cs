using NUnit.Framework;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Tests.EditMode
{
    public class DamageCalculatorTests
    {
        private static DamageRequest BasicRequest(float damage, HitZone zone)
        {
            DamageRequest request = new DamageRequest();
            request.BaseDamage = damage;
            request.Zone = zone;
            request.Source = DamageSource.Gunfire;
            request.Distance = 5f;
            request.FalloffStart = 50f;
            request.FalloffEnd = 100f;
            request.MinFalloffMultiplier = 0.5f;
            request.HeadshotMultiplier = 2f;
            request.CritChance = 0f;
            request.CritMultiplier = 1.5f;
            request.ArmorPenetration = 0f;
            return request;
        }

        [Test]
        public void HeadshotDoublesBodyDamage()
        {
            DamageRequest body = BasicRequest(20f, HitZone.Body);
            DamageRequest head = BasicRequest(20f, HitZone.Head);

            Assert.AreEqual(20f, DamageCalculator.RawDamage(body, 0.99f), 0.001f);
            Assert.AreEqual(40f, DamageCalculator.RawDamage(head, 0.99f), 0.001f);
        }

        [Test]
        public void LimbHitsDealReducedDamage()
        {
            DamageRequest limb = BasicRequest(20f, HitZone.Limbs);
            Assert.AreEqual(15f, DamageCalculator.RawDamage(limb, 0.99f), 0.001f);
        }

        [Test]
        public void DistanceFalloffInterpolatesAndClamps()
        {
            Assert.AreEqual(1f, DamageCalculator.DistanceMultiplier(10f, 50f, 100f, 0.5f), 0.0001f);
            Assert.AreEqual(0.75f, DamageCalculator.DistanceMultiplier(75f, 50f, 100f, 0.5f), 0.0001f);
            Assert.AreEqual(0.5f, DamageCalculator.DistanceMultiplier(120f, 50f, 100f, 0.5f), 0.0001f);
        }

        [Test]
        public void DamageBeyondRangeIsHalvedByFalloffFloor()
        {
            DamageRequest request = BasicRequest(40f, HitZone.Body);
            request.Distance = 200f;
            Assert.AreEqual(20f, DamageCalculator.RawDamage(request, 0.99f), 0.001f);
        }

        [Test]
        public void CriticalHitUsesCritMultiplier()
        {
            DamageRequest request = BasicRequest(10f, HitZone.Body);
            request.CritChance = 1f;
            request.CritMultiplier = 2f;
            Assert.AreEqual(20f, DamageCalculator.RawDamage(request, 0.5f), 0.001f);
        }

        [Test]
        public void CritRollAboveChanceDoesNotCrit()
        {
            DamageRequest request = BasicRequest(10f, HitZone.Body);
            request.CritChance = 0.2f;
            request.CritMultiplier = 3f;
            Assert.AreEqual(10f, DamageCalculator.RawDamage(request, 0.9f), 0.001f);
        }

        [Test]
        public void ArmorMitigationFollowsDiminishingReturnsCurve()
        {
            Assert.AreEqual(100f, DamageCalculator.Mitigate(100f, 0f, 0f), 0.001f);
            Assert.AreEqual(50f, DamageCalculator.Mitigate(100f, 100f, 0f), 0.001f);
            Assert.AreEqual(25f, DamageCalculator.Mitigate(100f, 300f, 0f), 0.001f);
        }

        [Test]
        public void ArmorPenetrationReducesMitigation()
        {
            float withoutPen = DamageCalculator.Mitigate(100f, 100f, 0f);
            float withPen = DamageCalculator.Mitigate(100f, 100f, 1f);
            Assert.Greater(withPen, withoutPen);
            Assert.AreEqual(100f, withPen, 0.001f);
        }

        [Test]
        public void NegativeBaseDamageIsClampedToZero()
        {
            DamageRequest request = BasicRequest(-50f, HitZone.Body);
            Assert.AreEqual(0f, DamageCalculator.RawDamage(request, 0.5f), 0.0001f);
        }

        [Test]
        public void ResolveReportsKillAndAbsorption()
        {
            DamageRequest request = BasicRequest(60f, HitZone.Body);
            DamageResult result = DamageCalculator.Resolve(request, 0.5f, 100f, 50f);
            Assert.AreEqual(30f, result.Applied, 0.001f);
            Assert.AreEqual(30f, result.Absorbed, 0.001f);
            Assert.IsFalse(result.Killed, "60 damage through 50 armour mitigation should not kill a 50hp target");
        }
    }

    public class HealthModelTests
    {
        [Test]
        public void StartsAtFullHealthAndArmor()
        {
            HealthModel health = new HealthModel(100f, 50f);
            Assert.AreEqual(100f, health.Health, 0.001f);
            Assert.AreEqual(50f, health.Armor, 0.001f);
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(1f, health.HealthNormalized, 0.001f);
        }

        [Test]
        public void DamageReducesHealthAndConsumesArmor()
        {
            HealthModel health = new HealthModel(100f, 100f);
            DamageRequest request = new DamageRequest();
            request.BaseDamage = 40f;
            request.Zone = HitZone.Body;

            DamageResult result = health.ApplyDamage(request, 0.99f, 0f);
            Assert.AreEqual(20f, result.Applied, 0.01f);
            Assert.AreEqual(80f, health.Health, 0.01f);
            Assert.Less(health.Armor, 100f);
        }

        [Test]
        public void LethalDamageKillsOnceAndRaisesEvent()
        {
            HealthModel health = new HealthModel(30f, 0f);
            int deaths = 0;
            health.Died += r => deaths++;

            DamageRequest request = new DamageRequest();
            request.BaseDamage = 50f;
            request.Zone = HitZone.Body;

            health.ApplyDamage(request, 0.99f, 0f);
            DamageResult second = health.ApplyDamage(request, 0.99f, 0f);

            Assert.AreEqual(1, deaths);
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0f, second.Applied, 0.001f);
            Assert.AreEqual(0f, health.Health, 0.001f);
        }

        [Test]
        public void InvulnerableTargetIgnoresDamage()
        {
            HealthModel health = new HealthModel(100f, 0f);
            health.SetInvulnerable(0f, 2f);
            DamageRequest request = new DamageRequest();
            request.BaseDamage = 500f;

            DamageResult result = health.ApplyDamage(request, 0.99f, 1f);
            Assert.AreEqual(0f, result.Applied, 0.001f);
            Assert.AreEqual(100f, health.Health, 0.001f);
        }

        [Test]
        public void ResetClearsDeathState()
        {
            HealthModel health = new HealthModel(50f, 20f);
            DamageRequest request = new DamageRequest();
            request.BaseDamage = 500f;
            health.ApplyDamage(request, 0.99f, 0f);
            Assert.IsTrue(health.IsDead);

            health.Reset(0f, 0f);
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(50f, health.Health, 0.001f);
        }

        [Test]
        public void HealNeverExceedsMaxHealth()
        {
            HealthModel health = new HealthModel(100f, 0f);
            DamageRequest request = new DamageRequest();
            request.BaseDamage = 30f;
            health.ApplyDamage(request, 0.99f, 0f);
            health.Restore(500f);
            Assert.AreEqual(100f, health.Health, 0.001f);
        }

        [Test]
        public void ArmorCannotBecomeNegative()
        {
            HealthModel health = new HealthModel(1000f, 10f);
            DamageRequest request = new DamageRequest();
            request.BaseDamage = 900f;
            health.ApplyDamage(request, 0.99f, 0f);
            Assert.GreaterOrEqual(health.Armor, 0f);
        }
    }
}
