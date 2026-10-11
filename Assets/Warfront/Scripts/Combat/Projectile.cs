using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Visible enemy projectile. Enemies fire these (rather than hitscan) so incoming fire can be
    /// seen, dodged and read - which matters a lot on a touch screen. Fully pooled.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public const string PoolId = "proj_enemy";

        private Vector3 _velocity;
        private float _damage;
        private float _life;
        private float _elapsed;
        private float _gravity;
        private int _teamId;
        private DamageRequest _template;
        private GameContext _context;
        private PoolRegistry _pools;
        private Vector3 _previousPosition;
        private bool _active;

        public static void RegisterPool(PoolRegistry pools, GameAssets assets, Transform root)
        {
            if (pools == null || pools.Has(PoolId)) return;

            GameObject prototype = new GameObject("ProjectilePrototype");
            prototype.transform.SetParent(root, false);
            prototype.layer = GameLayers.Projectile;

            Material material = assets != null ? assets.Tracer : null;
            PrimitiveFactory.CreateBox("Slug", prototype.transform, Vector3.zero, new Vector3(0.07f, 0.07f, 0.24f), material, false, Quaternion.identity, GameLayers.Projectile);
            PrimitiveFactory.CreateBox("Glow", prototype.transform, new Vector3(0f, 0f, 0.16f), new Vector3(0.05f, 0.05f, 0.08f), assets != null ? assets.Impact : null, false, Quaternion.identity, GameLayers.Projectile);
            PrimitiveFactory.SetShadowsRecursive(prototype, false);
            prototype.AddComponent<Projectile>();

            pools.Register(PoolId, prototype, 12, 64);
        }

        public void Launch(GameContext context, PoolRegistry pools, DamageRequest template, Vector3 position, Vector3 direction,
            float speed, float lifetime, float gravity, int teamId)
        {
            _context = context;
            _pools = pools;
            _template = template;
            _damage = template.BaseDamage;
            _velocity = direction.normalized * Mathf.Max(1f, speed);
            _life = Mathf.Max(0.05f, lifetime);
            _gravity = gravity;
            _teamId = teamId;
            _elapsed = 0f;
            _active = true;
            transform.position = position;
            _previousPosition = position;
            transform.rotation = Quaternion.LookRotation(_velocity.normalized, Vector3.up);
        }

        private void Update()
        {
            if (!_active) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _elapsed += dt;
            if (_elapsed >= _life)
            {
                Return();
                return;
            }

            _velocity += new Vector3(0f, _gravity, 0f) * dt;
            Vector3 step = _velocity * dt;
            float distance = step.magnitude;
            _previousPosition = transform.position;

            if (distance > 0.0001f)
            {
                RaycastHit hit;
                if (Physics.Raycast(_previousPosition, step / distance, out hit, distance, GameLayers.ShootableMask, QueryTriggerInteraction.Collide))
                {
                    Impact(hit);
                    return;
                }
            }

            transform.position += step;
            if (_velocity.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(_velocity.normalized, Vector3.up);
        }

        private void Impact(RaycastHit hit)
        {
            Damageable damageable = null;
            HitZone zone = HitZone.Body;
            DamageRequest request = _template;
            request.BaseDamage = _damage;
            DamageResult result = Damageable.ResolveHit(hit, request, Random.value, out zone, out damageable);

            if (damageable != null && result.Applied > 0f)
            {
                if (_context != null && _context.Vfx != null)
                {
                    if (damageable.IsPlayer) _context.Vfx.SpawnBlood(hit.point);
                    else _context.Vfx.SpawnImpact(hit.point, hit.normal, 0.6f);
                }
            }
            else if (_context != null && _context.Vfx != null)
            {
                _context.Vfx.SpawnImpact(hit.point, hit.normal, 1f);
            }

            if (_context != null && _context.Audio != null) _context.Audio.PlayImpact(hit.point, damageable != null && damageable.IsPlayer);
            Return();
        }

        private void Return()
        {
            if (!_active) return;
            _active = false;
            if (_pools != null) _pools.Return(PoolId, gameObject);
        }
    }
}
