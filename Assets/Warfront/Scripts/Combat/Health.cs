using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>Marks a collider as a damage zone and reports which zone it represents.</summary>
    public class Hitbox : MonoBehaviour
    {
        public HitZone Zone = HitZone.Body;
        public Damageable Owner;
        public float DamageMultiplier = 1f;

        public void Configure(Damageable owner, HitZone zone, float multiplier)
        {
            Owner = owner;
            Zone = zone;
            DamageMultiplier = multiplier;
        }
    }

    /// <summary>
    /// Engine bridge over HealthModel. It owns no gameplay rules: it forwards Unity events (triggers,
    /// collisions, ticks) into the pure model and republishes the results on the event bus.
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        private HealthModel _model;
        private float _time;

        public HealthModel Model { get { return _model; } }
        public bool IsPlayer { get; private set; }
        public int TeamId { get; private set; }
        public bool IsDead { get { return _model == null || _model.IsDead; } }

        /// <summary>Set by the owner so damage can be attributed (missions, kill credit).</summary>
        public System.Action<DamageResult, HitZone> DamagedCallback;
        public System.Action<DamageResult> DiedCallback;

        public void Initialize(float maxHealth, float maxArmor, bool isPlayer, int teamId)
        {
            _model = new HealthModel(maxHealth, maxArmor);
            IsPlayer = isPlayer;
            TeamId = teamId;
            if (isPlayer)
            {
                _model.HealthChanged += (health, max) => GameEvents.RaisePlayerHealthChanged(health, max);
                _model.ArmorChanged += (armor, max) => GameEvents.RaisePlayerArmorChanged(armor, max);
                GameEvents.RaisePlayerHealthChanged(_model.Health, _model.MaxHealth);
                GameEvents.RaisePlayerArmorChanged(_model.Armor, _model.MaxArmor);
            }
        }

        private void Update()
        {
            if (_model == null) return;
            _time += Time.deltaTime;
        }

        public void ResetHealth(float health, float armor)
        {
            if (_model == null) return;
            _model.Reset(health, armor);
        }

        public void Heal(float amount)
        {
            if (_model == null) return;
            _model.Restore(amount);
        }

        public void GrantArmor(float amount)
        {
            if (_model == null) return;
            _model.AddArmor(amount);
        }

        public void SetInvulnerable(float duration)
        {
            if (_model == null) return;
            _model.SetInvulnerable(_time, duration);
        }

        /// <summary>Applies a damage request that was already built by the weapon or explosion code.</summary>
        public DamageResult ApplyDamage(DamageRequest request, float critRoll)
        {
            DamageResult empty;
            empty.Applied = 0f;
            empty.Absorbed = 0f;
            empty.Killed = false;
            empty.WasCritical = false;
            empty.Zone = request.Zone;
            if (_model == null) return empty;

            DamageResult result = _model.ApplyDamage(request, critRoll, _time);

            if (result.Applied > 0f)
            {
                if (DamagedCallback != null) DamagedCallback(result, request.Zone);
                if (IsPlayer) GameEvents.RaisePlayerDamaged(result, request.Zone);
            }

            if (result.Killed)
            {
                if (DiedCallback != null) DiedCallback(result);
            }
            return result;
        }

        /// <summary>Resolves a raycast hit into a damage request against this damageable.</summary>
        public static DamageResult ResolveHit(RaycastHit hit, DamageRequest request, float critRoll, out HitZone zone, out Damageable target)
        {
            target = null;
            zone = request.Zone;

            Hitbox hitbox = hit.collider != null ? hit.collider.GetComponent<Hitbox>() : null;
            if (hitbox != null)
            {
                zone = hitbox.Zone;
                target = hitbox.Owner;
            }
            else if (hit.collider != null)
            {
                target = hit.collider.GetComponent<Damageable>();
                if (target == null) target = hit.collider.GetComponentInParent<Damageable>();
            }

            DamageResult empty;
            empty.Applied = 0f;
            empty.Absorbed = 0f;
            empty.Killed = false;
            empty.WasCritical = false;
            empty.Zone = zone;
            if (target == null) return empty;

            request.Zone = zone;
            request.Distance = hit.distance;
            return target.ApplyDamage(request, critRoll);
        }
    }
}
