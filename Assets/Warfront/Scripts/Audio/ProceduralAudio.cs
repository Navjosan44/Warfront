using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Every sound in WARFRONT is synthesised at boot from DSP math.
    ///
    /// This keeps the build free of licensed audio, adds no download weight to the APK, and gives the
    /// game a coherent original sound identity (dry, mechanical, industrial). Clips are generated once
    /// and cached.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 24000;

        /// <summary>Short noise burst with a pitch/decay profile - gunshots, impacts and footsteps.</summary>
        public static AudioClip NoiseBurst(string name, int seed, float duration, float decay, float lowPass, float punch, float amplitude)
        {
            int samples = Mathf.Max(64, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[samples];
            WfRandom random = new WfRandom(seed);
            float previous = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float noise = random.Range(-1f, 1f);

                // One-pole low pass: lowPass is the cutoff amount (0 = open, 1 = muffled).
                previous = Mathf.Lerp(noise, previous, WfMath.Clamp01(lowPass));

                float envelope = Mathf.Exp(-t * decay);
                float body = Mathf.Sin(t * Mathf.PI * 2f * (70f + punch * 120f)) * Mathf.Exp(-t * decay * 2.2f) * punch;
                data[i] = WfMath.Clamp((previous * 0.85f + body) * envelope * amplitude, -1f, 1f);
            }
            return CreateClip(name, data);
        }

        public static AudioClip SineBlip(string name, float frequency, float duration, float amplitude, float decay)
        {
            int samples = Mathf.Max(32, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * decay);
                float tone = Mathf.Sin(i / (float)SampleRate * Mathf.PI * 2f * frequency);
                data[i] = WfMath.Clamp(tone * envelope * amplitude, -1f, 1f);
            }
            return CreateClip(name, data);
        }

        public static AudioClip TwoToneBeep(string name, float lowFrequency, float highFrequency, float stepSeconds, float duration, float amplitude)
        {
            int samples = Mathf.Max(64, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[samples];
            int stepSamples = Mathf.Max(1, Mathf.RoundToInt(stepSeconds * SampleRate));

            for (int i = 0; i < samples; i++)
            {
                float frequency = (i / stepSamples) % 2 == 0 ? lowFrequency : highFrequency;
                float edgeFade = Mathf.Min(1f, Mathf.Sin((float)(i % stepSamples) / stepSamples * Mathf.PI) * 3f);
                data[i] = Mathf.Sin(i / (float)SampleRate * Mathf.PI * 2f * frequency) * amplitude * edgeFade;
            }
            return CreateClip(name, data);
        }

        /// <summary>Filtered noise texture used for wind, machinery rumble and room tone.</summary>
        public static AudioClip AmbientLoop(string name, int seed, float duration, float amplitude, float brightness)
        {
            int samples = Mathf.Max(1024, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[samples];
            WfRandom random = new WfRandom(seed);
            float slow = 0f;
            float fast = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                fast = Mathf.Lerp(random.Range(-1f, 1f), fast, WfMath.Clamp01(0.35f + brightness * 0.4f));
                slow = Mathf.Lerp(fast, slow, 0.995f);

                float swell = 0.7f + Mathf.Sin(t * Mathf.PI * 4f) * 0.15f + Mathf.Sin(t * Mathf.PI * 1.3f) * 0.15f;
                data[i] = WfMath.Clamp(slow * amplitude * swell, -1f, 1f);
            }
            return CreateClip(name, data);
        }

        /// <summary>
        /// Short dark music bed: a slow minor progression over a low drone, loop friendly.
        /// </summary>
        public static AudioClip MusicLoop(string name, float duration, float amplitude)
        {
            int samples = Mathf.Max(1024, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[samples];

            // A minor - F major - G major - E minor, as scale degrees of A minor.
            float[] roots = { 220f, 174.61f, 196f, 164.81f };
            float[] thirds = { 261.63f, 220f, 246.94f, 196f };
            float[] fifths = { 329.63f, 349.23f, 392f, 329.63f };

            int chordSamples = samples / roots.Length;
            for (int i = 0; i < samples; i++)
            {
                int chord = Mathf.Clamp(i / chordSamples, 0, roots.Length - 1);
                float t = (float)i / samples;
                float local = (i % chordSamples) / (float)chordSamples;

                float attack = Mathf.Min(1f, local * 6f);
                float release = Mathf.Min(1f, (1f - local) * 4f);
                float envelope = attack * release;

                float time = i / (float)SampleRate;
                float drone = Mathf.Sin(time * Mathf.PI * 2f * roots[chord] * 0.5f) * 0.45f;
                float pad = Mathf.Sin(time * Mathf.PI * 2f * thirds[chord]) * 0.22f
                            + Mathf.Sin(time * Mathf.PI * 2f * fifths[chord]) * 0.18f;
                float shimmer = Mathf.Sin(time * Mathf.PI * 2f * (fifths[chord] * 2.01f)) * 0.06f * Mathf.Sin(t * Mathf.PI * 8f);

                data[i] = WfMath.Clamp((drone + pad) * envelope * amplitude + shimmer, -1f, 1f);
            }
            return CreateClip(name, data);
        }

        private static AudioClip CreateClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            if (clip == null) return null;
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Weapon class specific shot: pitch, decay and body all differ per platform.</summary>
        public static AudioClip WeaponShot(WeaponDef def)
        {
            if (def == null) return NoiseBurst("shot_default", 11, 0.22f, 14f, 0.25f, 0.5f, 0.85f);

            int seed = WfMath.Hash(def.Id);
            switch (def.Class)
            {
                case WeaponClass.Pistol:
                    return NoiseBurst("shot_pistol", seed, 0.16f, 18f, 0.2f, 0.45f, 0.75f);
                case WeaponClass.SMG:
                    return NoiseBurst("shot_smg", seed, 0.12f, 24f, 0.3f, 0.35f, 0.65f);
                case WeaponClass.Shotgun:
                    return NoiseBurst("shot_shotgun", seed, 0.4f, 9f, 0.42f, 0.9f, 0.95f);
                case WeaponClass.MarksmanRifle:
                    return NoiseBurst("shot_dmr", seed, 0.3f, 12f, 0.18f, 0.7f, 0.9f);
                case WeaponClass.SniperRifle:
                    return NoiseBurst("shot_sniper", seed, 0.55f, 7f, 0.12f, 1f, 1f);
                case WeaponClass.LightMachineGun:
                    return NoiseBurst("shot_lmg", seed, 0.24f, 13f, 0.36f, 0.6f, 0.85f);
                default:
                    return NoiseBurst("shot_ar", seed, 0.2f, 15f, 0.24f, 0.55f, 0.85f);
            }
        }

        public static AudioClip ReloadClick(int seed, bool magazineOut)
        {
            return magazineOut
                ? NoiseBurst("reload_out", seed, 0.1f, 30f, 0.55f, 0.25f, 0.5f)
                : NoiseBurst("reload_in", seed + 7, 0.14f, 22f, 0.45f, 0.35f, 0.6f);
        }

        public static AudioClip Footstep(int seed, float intensity)
        {
            float amplitude = Mathf.Lerp(0.14f, 0.4f, WfMath.Clamp01(intensity));
            return NoiseBurst("step", seed, 0.11f, 26f, 0.62f, 0.18f, amplitude);
        }

        public static AudioClip Impact(bool flesh)
        {
            return flesh
                ? NoiseBurst("impact_flesh", 501, 0.14f, 20f, 0.7f, 0.3f, 0.55f)
                : NoiseBurst("impact_solid", 902, 0.16f, 24f, 0.25f, 0.45f, 0.6f);
        }

        public static AudioClip EnemyAlert(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Heavy:
                    return NoiseBurst("alert_heavy", 6111, 0.5f, 8f, 0.55f, 0.85f, 0.7f);
                case EnemyArchetype.Elite:
                    return TwoToneBeep("alert_elite", 320f, 480f, 0.12f, 0.5f, 0.4f);
                default:
                    return NoiseBurst("alert_grunt", 3111, 0.3f, 12f, 0.42f, 0.55f, 0.5f);
            }
        }

        public static AudioClip EnemyDeath(EnemyArchetype archetype)
        {
            return NoiseBurst("death", 7331 + (int)archetype, 0.6f, 6f, 0.6f, 0.4f, 0.55f);
        }
    }
}
