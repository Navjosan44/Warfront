using UnityEngine;

namespace Warfront.Core
{
    /// <summary>
    /// Small deterministic helpers shared by every gameplay system.
    /// Everything here is pure so it can be unit tested without the engine.
    /// </summary>
    public static class WfMath
    {
        public const float Epsilon = 0.0001f;

        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static int ClampInt(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static float Clamp01(float v)
        {
            return Clamp(v, 0f, 1f);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp01(t);
        }

        public static float LerpUnclamped(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>Frame-rate independent exponential smoothing.</summary>
        public static float Damp(float current, float target, float sharpness, float dt)
        {
            if (sharpness <= 0f) return target;
            float t = 1f - Mathf.Exp(-sharpness * dt);
            return current + (target - current) * t;
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            float diff = target - current;
            if (Mathf.Abs(diff) <= maxDelta) return target;
            return current + Mathf.Sign(diff) * maxDelta;
        }

        public static int RepeatIndex(int value, int count)
        {
            if (count <= 0) return 0;
            int r = value % count;
            return r < 0 ? r + count : r;
        }

        /// <summary>Deterministic 32 bit hash - used to derive loot/save seeds from stable ids.</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                int hash = 23;
                if (text != null)
                {
                    for (int i = 0; i < text.Length; i++) hash = hash * 31 + text[i];
                }
                return hash == 0 ? 1 : hash;
            }
        }

        public static int Combine(int a, int b)
        {
            unchecked { return a * 486187739 + b * 1000003 + 17; }
        }
    }

    /// <summary>
    /// Fast deterministic XorShift RNG. Used instead of UnityEngine.Random so loot rolls,
    /// level layout and mission content can be reproduced from a seed for debugging and tests.
    /// </summary>
    public struct WfRandom
    {
        private uint _state;

        public WfRandom(int seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform float in [0,1).</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int RangeInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        public int WeightedIndex(int[] weights)
        {
            if (weights == null || weights.Length == 0) return -1;
            int total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0) total += weights[i];
            }
            if (total <= 0) return -1;

            int roll = RangeInt(0, total);
            int acc = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0) continue;
                acc += weights[i];
                if (roll < acc) return i;
            }
            return weights.Length - 1;
        }
    }
}
