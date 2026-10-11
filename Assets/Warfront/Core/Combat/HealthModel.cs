using System;
using UnityEngine;

namespace Warfront.Core
{
    /// <summary>
    /// Engine independent health/armor model. The MonoBehaviour wrapper (Health) only forwards
    /// engine events into this so the rules stay testable and cannot desync from the runtime.
    /// </summary>
    public class HealthModel
    {
        public event Action<DamageResult> Damaged;
        public event Action<DamageResult> Died;
        public event Action<float, float> HealthChanged;
        public event Action<float, float> ArmorChanged;

        private float _health;
        private float _armor;
        private bool _dead;
        private float _invulnerableUntil;

        public HealthModel(float maxHealth, float maxArmor)
        {
            MaxHealth = Mathf.Max(1f, maxHealth);
            MaxArmor = Mathf.Max(0f, maxArmor);
            _health = MaxHealth;
            _armor = MaxArmor;
        }

        public float MaxHealth { get; private set; }
        public float MaxArmor { get; private set; }
        public float Health { get { return _health; } }
        public float Armor { get { return _armor; } }
        public bool IsDead { get { return _dead; } }
        public float HealthNormalized { get { return MaxHealth <= 0f ? 0f : WfMath.Clamp01(_health / MaxHealth); } }
        public float ArmorNormalized { get { return MaxArmor <= 0f ? 0f : WfMath.Clamp01(_armor / MaxArmor); } }

        public void SetMaxHealth(float max, bool refill)
        {
            MaxHealth = Mathf.Max(1f, max);
            if (refill) _health = MaxHealth;
            else _health = Mathf.Min(_health, MaxHealth);
            RaiseHealth();
        }

        public void SetMaxArmor(float max, bool refill)
        {
            MaxArmor = Mathf.Max(0f, max);
            if (refill) _armor = MaxArmor;
            else _armor = Mathf.Min(_armor, MaxArmor);
            RaiseArmor();
        }

        public void Restore(float amount)
        {
            if (amount <= 0f) return;
            float before = _health;
            _health = Mathf.Min(MaxHealth, _health + amount);
            if (!Mathf.Approximately(before, _health)) RaiseHealth();
        }

        public void AddArmor(float amount)
        {
            if (amount == 0f) return;
            _armor = WfMath.Clamp(_armor + amount, 0f, MaxArmor);
            RaiseArmor();
        }

        /// <summary>Full reset used on respawn/restart. Never leaves a dead-but-playable state.</summary>
        public void Reset(float health, float armor)
        {
            _dead = false;
            _invulnerableUntil = 0f;
            _health = WfMath.Clamp(health <= 0f ? MaxHealth : health, 0f, MaxHealth);
            _armor = WfMath.Clamp(armor, 0f, MaxArmor);
            RaiseHealth();
            RaiseArmor();
        }

        public void SetInvulnerable(float now, float duration)
        {
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, now + Mathf.Max(0f, duration));
        }

        public bool IsInvulnerable(float now)
        {
            return now < _invulnerableUntil;
        }

        public DamageResult ApplyDamage(DamageRequest request, float critRoll, float now)
        {
            if (_dead || IsInvulnerable(now))
            {
                DamageResult ignored;
                ignored.Applied = 0f;
                ignored.Absorbed = 0f;
                ignored.Killed = false;
                ignored.WasCritical = false;
                ignored.Zone = request.Zone;
                return ignored;
            }

            DamageResult result = DamageCalculator.Resolve(request, critRoll, _armor, _health);

            float armorBefore = _armor;
            if (result.Absorbed > 0f)
            {
                // Armor takes a share of the raw hit and degrades as it protects.
                _armor = Mathf.Max(0f, _armor - result.Absorbed * 0.5f);
            }

            _health = Mathf.Max(0f, _health - result.Applied);
            if (_health <= 0f)
            {
                _dead = true;
                result.Killed = true;
            }

            if (!Mathf.Approximately(armorBefore, _armor)) RaiseArmor();
            RaiseHealth();

            if (Damaged != null) Damaged(result);
            if (result.Killed && Died != null) Died(result);
            return result;
        }

        private void RaiseHealth()
        {
            if (HealthChanged != null) HealthChanged(_health, MaxHealth);
        }

        private void RaiseArmor()
        {
            if (ArmorChanged != null) ArmorChanged(_armor, MaxArmor);
        }
    }
}
