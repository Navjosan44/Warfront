using UnityEngine;

namespace Warfront.Core
{
    public enum HitZone
    {
        Body = 0,
        Head = 1,
        Limbs = 2,
        Armor = 3
    }

    public enum DamageSource
    {
        Gunfire,
        Explosion,
        Melee,
        Fall,
        Scripted
    }

    /// <summary>Everything the damage calculator needs. Immutable value type - no allocations per hit.</summary>
    public struct DamageRequest
    {
        public float BaseDamage;
        public HitZone Zone;
        public DamageSource Source;
        public float Distance;
        public float FalloffStart;
        public float FalloffEnd;
        public float MinFalloffMultiplier;
        public float HeadshotMultiplier;
        public float CritChance;
        public float CritMultiplier;
        public float ArmorPenetration;
        public bool IsCritical;
        public int ShooterId;
    }

    public struct DamageResult
    {
        public float Applied;
        public float Absorbed;
        public bool Killed;
        public bool WasCritical;
        public HitZone Zone;
    }

    /// <summary>
    /// Pure damage maths. Kept free of MonoBehaviours so the balance rules are unit tested.
    /// Damage = base * zone * distance falloff * crit, then mitigated by armor (with penetration).
    /// </summary>
    public static class DamageCalculator
    {
        public static float ZoneMultiplier(HitZone zone, float headshotMultiplier)
        {
            switch (zone)
            {
                case HitZone.Head: return Mathf.Max(1f, headshotMultiplier);
                case HitZone.Limbs: return 0.75f;
                case HitZone.Armor: return 0.6f;
                default: return 1f;
            }
        }

        public static float DistanceMultiplier(float distance, float falloffStart, float falloffEnd, float minMultiplier)
        {
            if (falloffEnd <= falloffStart || distance <= falloffStart) return 1f;
            if (distance >= falloffEnd) return WfMath.Clamp01(minMultiplier);
            float t = (distance - falloffStart) / (falloffEnd - falloffStart);
            return WfMath.Lerp(1f, WfMath.Clamp01(minMultiplier), t);
        }

        /// <summary>Raw damage before armor. Roll is the pre-computed random value in [0,1).</summary>
        public static float RawDamage(DamageRequest request, float critRoll)
        {
            float damage = Mathf.Max(0f, request.BaseDamage);
            damage *= ZoneMultiplier(request.Zone, request.HeadshotMultiplier);
            damage *= DistanceMultiplier(request.Distance, request.FalloffStart, request.FalloffEnd, request.MinFalloffMultiplier);
            bool crit = request.IsCritical || (request.CritChance > 0f && critRoll < request.CritChance);
            if (crit) damage *= Mathf.Max(1f, request.CritMultiplier);
            return Mathf.Max(0f, damage);
        }

        public static float Mitigate(float rawDamage, float armor, float armorPenetration)
        {
            if (rawDamage <= 0f) return 0f;
            if (armor <= 0f) return rawDamage;

            float effectiveArmor = Mathf.Max(0f, armor * (1f - WfMath.Clamp01(armorPenetration)));
            // 100 armor removes 50% of incoming damage, with diminishing returns.
            float reduction = effectiveArmor / (effectiveArmor + 100f);
            return rawDamage * (1f - reduction);
        }

        /// <summary>Full resolution used by the runtime and by tests.</summary>
        public static DamageResult Resolve(DamageRequest request, float critRoll, float targetArmor, float targetHealth)
        {
            float raw = RawDamage(request, critRoll);
            float applied = Mitigate(raw, targetArmor, request.ArmorPenetration);
            float absorbedByArmor = Mathf.Max(0f, raw - applied);

            bool crit = request.IsCritical || (request.CritChance > 0f && critRoll < request.CritChance);
            bool kills = applied >= targetHealth;

            DamageResult result;
            result.Applied = applied;
            result.Absorbed = absorbedByArmor;
            result.Killed = kills;
            result.WasCritical = crit;
            result.Zone = request.Zone;
            return result;
        }
    }
}
