using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    public enum EffectKind
    {
        MuzzleFlash = 0,
        Impact = 1,
        Tracer = 2,
        Debris = 3,
        Explosion = 4,
        Beacon = 5
    }

    /// <summary>Self returning pooled effect. Every combat effect in WARFRONT uses this.</summary>
    public class PooledEffect : MonoBehaviour
    {
        public EffectKind Kind;
        public string PoolId;
        public float Lifetime = 0.5f;
        public float Elapsed;
        public float ScaleFrom = 1f;
        public float ScaleTo = 1f;
        public Vector3 Velocity;
        public float Gravity = -9.81f;
        public LineRenderer Line;
        public Transform LineEnd;

        private PoolRegistry _pools;
        private MeshRenderer _renderer;
        private bool _active;

        public void Configure(EffectKind kind, string poolId, PoolRegistry pools, MeshRenderer renderer, LineRenderer line)
        {
            Kind = kind;
            PoolId = poolId;
            _pools = pools;
            _renderer = renderer;
            Line = line;
        }

        public void Play(float lifetime, float scaleFrom, float scaleTo, Vector3 velocity, float gravity)
        {
            Lifetime = Mathf.Max(0.05f, lifetime);
            Elapsed = 0f;
            ScaleFrom = scaleFrom;
            ScaleTo = scaleTo;
            Velocity = velocity;
            Gravity = gravity;
            _active = true;
            transform.localScale = Vector3.one * scaleFrom;
            if (Line != null) Line.enabled = true;
            if (_renderer != null) _renderer.enabled = true;
        }

        public void SetLine(Vector3 from, Vector3 to)
        {
            if (Line == null) return;
            Line.positionCount = 2;
            Line.SetPosition(0, from);
            Line.SetPosition(1, to);
        }

        private void Update()
        {
            if (!_active) return;
            float dt = Time.deltaTime;
            Elapsed += dt;
            float t = WfMath.Clamp01(Elapsed / Lifetime);

            if (Mathf.Abs(Velocity.sqrMagnitude) > 0.0001f)
            {
                Velocity += new Vector3(0f, Gravity, 0f) * dt;
                transform.position += Velocity * dt;
            }

            float scale = Mathf.Lerp(ScaleFrom, ScaleTo, t);
            transform.localScale = new Vector3(scale, scale, scale);

            if (Line != null)
            {
                Line.startWidth = Mathf.Lerp(0.045f, 0.005f, t);
                Line.endWidth = Line.startWidth;
            }

            if (Elapsed >= Lifetime) Return();
        }

        public void Return()
        {
            if (!_active) return;
            _active = false;
            if (_pools != null && !string.IsNullOrEmpty(PoolId)) _pools.Return(PoolId, gameObject);
        }
    }

    /// <summary>
    /// Builds and pools the combat effects once at boot and exposes allocation-free spawn calls.
    /// Hundreds of shots a minute run through this without a single Instantiate.
    /// </summary>
    public class VfxFactory
    {
        public const string MuzzleFlashPool = "vfx_muzzle_flash";
        public const string ImpactPool = "vfx_impact";
        public const string TracerPool = "vfx_tracer";
        public const string BloodPool = "vfx_blood";
        public const string ExplosionPool = "vfx_explosion";
        public const string BulletPool = "proj_bullet";

        private readonly GameAssets _assets;
        private readonly PoolRegistry _pools;
        private readonly Transform _root;

        public VfxFactory(GameAssets assets, PoolRegistry pools, Transform root)
        {
            _assets = assets;
            _pools = pools;
            _root = root;
            RegisterPrototypes();
        }

        private void RegisterPrototypes()
        {
            _pools.Register(MuzzleFlashPool, BuildMuzzleFlashPrototype(), 6, 16);
            _pools.Register(ImpactPool, BuildImpactPrototype(), 10, 32);
            _pools.Register(BloodPool, BuildDebrisPrototype("BloodPrototype", _assets != null ? _assets.Blood : null), 8, 24);
            _pools.Register(ExplosionPool, BuildExplosionPrototype(), 3, 8);
            _pools.Register(TracerPool, BuildTracerPrototype(), 12, 32);
            _pools.Register(BulletPool, BuildBulletPrototype(), 8, 48);
        }

        private GameObject BuildMuzzleFlashPrototype()
        {
            GameObject go = CreateBase("MuzzleFlashPrototype", EffectKind.MuzzleFlash, MuzzleFlashPool, false, false);
            PrimitiveFactory.CreateBox("FlashA", go.transform, Vector3.zero, new Vector3(0.22f, 0.22f, 0.4f), _assets != null ? _assets.MuzzleFlash : null, false, Quaternion.identity, go.layer);
            PrimitiveFactory.CreateBox("FlashB", go.transform, Vector3.zero, new Vector3(0.4f, 0.06f, 0.06f), _assets != null ? _assets.MuzzleFlash : null, false, Quaternion.identity, go.layer);
            PrimitiveFactory.SetShadowsRecursive(go, false);
            return go;
        }

        private GameObject BuildImpactPrototype()
        {
            GameObject go = CreateBase("ImpactPrototype", EffectKind.Impact, ImpactPool, false, false);
            PrimitiveFactory.CreateBox("Sparks", go.transform, Vector3.zero, new Vector3(0.14f, 0.14f, 0.02f), _assets != null ? _assets.Impact : null, false, Quaternion.identity, go.layer);
            PrimitiveFactory.CreateBox("SparksB", go.transform, new Vector3(0f, 0f, 0.02f), new Vector3(0.06f, 0.06f, 0.06f), _assets != null ? _assets.Impact : null, false, Quaternion.identity, go.layer);
            PrimitiveFactory.SetShadowsRecursive(go, false);
            return go;
        }

        private GameObject BuildDebrisPrototype(string name, Material material)
        {
            GameObject go = CreateBase(name, EffectKind.Debris, BloodPool, false, false);
            PrimitiveFactory.CreateBox("ChunkA", go.transform, Vector3.zero, Vector3.one * 0.09f, material, false, Quaternion.identity, go.layer);
            PrimitiveFactory.CreateBox("ChunkB", go.transform, new Vector3(0.06f, 0.04f, 0f), Vector3.one * 0.06f, material, false, Quaternion.identity, go.layer);
            PrimitiveFactory.CreateBox("ChunkC", go.transform, new Vector3(-0.05f, -0.03f, 0.03f), Vector3.one * 0.07f, material, false, Quaternion.identity, go.layer);
            PrimitiveFactory.SetShadowsRecursive(go, false);
            return go;
        }

        private GameObject BuildExplosionPrototype()
        {
            GameObject go = CreateBase("ExplosionPrototype", EffectKind.Explosion, ExplosionPool, false, false);
            PrimitiveFactory.CreateBox("Core", go.transform, Vector3.zero, Vector3.one, _assets != null ? _assets.Impact : null, false, Quaternion.identity, go.layer);
            Light flash = go.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.range = 12f;
            flash.intensity = 2.4f;
            flash.color = new Color(1f, 0.75f, 0.35f);
            flash.renderMode = LightRenderMode.ForceVertex;
            PrimitiveFactory.SetShadowsRecursive(go, false);
            return go;
        }

        private GameObject BuildTracerPrototype()
        {
            GameObject go = CreateBase("TracerPrototype", EffectKind.Tracer, TracerPool, true, true);
            LineRenderer line = go.GetComponent<LineRenderer>();
            if (line != null)
            {
                line.positionCount = 2;
                line.material = _assets != null ? _assets.Tracer : null;
                line.startWidth = 0.05f;
                line.endWidth = 0.05f;
                line.useWorldSpace = true;
            }
            return go;
        }

        private GameObject BuildBulletPrototype()
        {
            GameObject go = CreateBase("BulletPrototype", EffectKind.Debris, BulletPool, false, false);
            PrimitiveFactory.CreateBox("Round", go.transform, Vector3.zero, new Vector3(0.05f, 0.05f, 0.18f), _assets != null ? _assets.Tracer : null, false, Quaternion.identity, go.layer);
            PrimitiveFactory.SetShadowsRecursive(go, false);
            return go;
        }

        private GameObject CreateBase(string name, EffectKind kind, string poolId, bool addLine, bool lineOnRoot)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            if (addLine && lineOnRoot)
            {
                LineRenderer line = go.AddComponent<LineRenderer>();
                line.alignment = LineAlignment.View;
            }
            PooledEffect effect = go.AddComponent<PooledEffect>();
            effect.Configure(kind, poolId, _pools, go.GetComponent<MeshRenderer>(), go.GetComponent<LineRenderer>());
            return go;
        }

        public void SpawnMuzzleFlash(Vector3 position, Quaternion rotation, float scale)
        {
            GameObject go = _pools.Rent(MuzzleFlashPool, position, rotation);
            if (go == null) return;
            PooledEffect effect = go.GetComponent<PooledEffect>();
            if (effect == null) return;
            effect.Play(0.06f, scale * 0.6f, scale * 1.15f, Vector3.zero, 0f);
        }

        public void SpawnImpact(Vector3 position, Vector3 normal, float scale)
        {
            GameObject go = _pools.Rent(ImpactPool, position + normal * 0.02f, Quaternion.LookRotation(normal));
            if (go == null) return;
            PooledEffect effect = go.GetComponent<PooledEffect>();
            if (effect == null) return;
            effect.Play(0.45f, scale * 0.6f, scale * 1.6f, normal * 1.5f, -3f);
        }

        public void SpawnBlood(Vector3 position)
        {
            GameObject go = _pools.Rent(BloodPool, position, Quaternion.identity);
            if (go == null) return;
            PooledEffect effect = go.GetComponent<PooledEffect>();
            if (effect == null) return;
            Vector3 velocity = new Vector3(Random.Range(-1.4f, 1.4f), Random.Range(0.6f, 2.2f), Random.Range(-1.4f, 1.4f));
            effect.Play(0.8f, 0.7f, 1.1f, velocity, -9.81f);
        }

        public void SpawnTracer(Vector3 from, Vector3 to, float width)
        {
            GameObject go = _pools.Rent(TracerPool, from, Quaternion.identity);
            if (go == null) return;
            PooledEffect effect = go.GetComponent<PooledEffect>();
            if (effect == null) return;
            effect.SetLine(from, to);
            effect.Play(0.07f, 1f, 1f, Vector3.zero, 0f);
        }

        public void SpawnExplosion(Vector3 position, float radius, bool withLight)
        {
            GameObject go = _pools.Rent(ExplosionPool, position, Quaternion.identity);
            if (go == null) return;
            PooledEffect effect = go.GetComponent<PooledEffect>();
            if (effect == null) return;
            Light light = go.GetComponent<Light>();
            if (light != null) light.enabled = withLight;
            effect.Play(0.55f, radius * 0.4f, radius * 1.8f, Vector3.up * 0.6f, -1.5f);
            SpawnImpact(position, Vector3.up, radius * 0.5f);
        }

        public GameObject SpawnBullet(Vector3 position, Quaternion rotation, Vector3 velocity, float lifetime)
        {
            GameObject go = _pools.Rent(BulletPool, position, rotation);
            if (go == null) return null;
            PooledEffect effect = go.GetComponent<PooledEffect>();
            if (effect != null) effect.Play(lifetime, 1f, 1f, velocity, -2.2f);
            return go;
        }
    }
}
