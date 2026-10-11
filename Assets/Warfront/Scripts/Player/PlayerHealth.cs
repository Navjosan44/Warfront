using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Player health behaviour. Bridges the shared Damageable into player specific feedback: rig
    /// flinch, camera shake, audio, haptics and the mission failure handoff on death.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        public const float BaseHealth = 100f;
        public const float BaseArmor = 50f;

        private GameContext _context;
        private Damageable _damageable;
        private PlayerController _player;

        public Damageable Damageable { get { return _damageable; } }

        public void Initialize(GameContext context, Damageable damageable, PlayerController player)
        {
            _context = context;
            _damageable = damageable;
            _player = player;

            _damageable.Initialize(BaseHealth, BaseArmor, true, 0);
            _damageable.DamagedCallback = OnDamaged;
            _damageable.DiedCallback = OnDied;
        }

        public void ResetForMission()
        {
            if (_damageable == null) return;
            _damageable.ResetHealth(BaseHealth, BaseArmor);
            _damageable.SetInvulnerable(2f);
            if (_player != null) _player.OnRespawn(_player.transform.position);
        }

        public void ApplyArmorUpgrade(float bonusArmor)
        {
            if (_damageable == null || _damageable.Model == null) return;
            _damageable.Model.SetMaxArmor(BaseArmor + Mathf.Max(0f, bonusArmor), true);
        }

        private void OnDamaged(DamageResult result, HitZone zone)
        {
            if (result.Applied <= 0f) return;

            float strength = WfMath.Clamp01(result.Applied / 45f);
            if (_player != null && _player.Rig != null) _player.Rig.PlayHitReaction(strength);
            if (_context != null)
            {
                if (_context.Camera != null) _context.Camera.AddShake(0.25f + strength * 0.6f);
                if (_context.Audio != null) _context.Audio.PlayPlayerHit(strength);
                if (_context.Haptics != null) _context.Haptics.Damage(result.Applied);
            }
        }

        private void OnDied(DamageResult result)
        {
            if (_player != null) _player.OnDeath();
            if (_context != null)
            {
                if (_context.Audio != null) _context.Audio.PlayPlayerDeath();
                if (_context.Flow != null) _context.Flow.OnPlayerDied();
            }
        }
    }
}
