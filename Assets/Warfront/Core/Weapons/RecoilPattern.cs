using UnityEngine;

namespace Warfront.Core
{
    /// <summary>
    /// Deterministic recoil pattern: each weapon class has a predictable climb shape so players can
    /// learn to control it, with a seeded horizontal drift. Sample values are in degrees.
    /// </summary>
    public static class RecoilPattern
    {
        private const int PatternLength = 24;

        public static void Sample(WeaponClass weaponClass, int shotIndex, int seed, out float vertical, out float horizontal)
        {
            int index = WfMath.RepeatIndex(shotIndex, PatternLength);
            float ramp = 1f - Mathf.Exp(-index * 0.18f);          // first shots kick hardest, then plateau
            float bias = HorizontalBias(weaponClass);
            var rng = new WfRandom(WfMath.Combine(seed, index));

            vertical = ramp * VerticalScale(weaponClass);
            horizontal = rng.Range(-1f, 1f) * bias * ramp;
        }

        public static float VerticalScale(WeaponClass weaponClass)
        {
            switch (weaponClass)
            {
                case WeaponClass.SMG: return 0.75f;
                case WeaponClass.AssaultRifle: return 1f;
                case WeaponClass.LightMachineGun: return 1.15f;
                case WeaponClass.MarksmanRifle: return 1.4f;
                case WeaponClass.SniperRifle: return 2.2f;
                case WeaponClass.Shotgun: return 1.8f;
                default: return 0.9f;
            }
        }

        public static float HorizontalBias(WeaponClass weaponClass)
        {
            switch (weaponClass)
            {
                case WeaponClass.SMG: return 0.9f;
                case WeaponClass.LightMachineGun: return 1.1f;
                case WeaponClass.SniperRifle: return 0.25f;
                case WeaponClass.MarksmanRifle: return 0.35f;
                case WeaponClass.Shotgun: return 0.5f;
                default: return 0.6f;
            }
        }
    }
}
