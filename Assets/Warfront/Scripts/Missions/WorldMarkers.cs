using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>World space marker for objectives and points of interest. Reads clearly without UI.</summary>
    public class ObjectiveMarker : MonoBehaviour
    {
        public string Tag;
        public bool IsActive = true;

        private GameObject _glow;
        private Light _light;
        private float _phase;

        public void BuildVisuals(GameAssets assets, Color color, bool withLight)
        {
            _phase = Random.Range(0f, 6.28f);
            Material material = assets != null ? assets.Lit("Marker_" + Tag, color, 0.3f, 0.2f) : null;

            PrimitiveFactory.CreateMesh("MarkerPole", transform, MeshFactory.Cylinder(8), new Vector3(0f, 0f, 0f),
                new Vector3(0.24f, 3.4f, 0.24f), material, ColliderKind.None, GameLayers.Interactable);
            _glow = PrimitiveFactory.CreateBox("MarkerGlow", transform, new Vector3(0f, 3.7f, 0f),
                new Vector3(0.9f, 0.35f, 0.9f), assets != null ? assets.ObjectiveMarker : null, false, Quaternion.identity, GameLayers.Interactable);
            PrimitiveFactory.CreateBox("MarkerBase", transform, new Vector3(0f, 0.12f, 0f),
                new Vector3(1.4f, 0.24f, 1.4f), material, false, Quaternion.identity, GameLayers.Interactable);

            if (withLight)
            {
                GameObject lightObject = new GameObject("MarkerLight");
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.localPosition = new Vector3(0f, 3.7f, 0f);
                _light = lightObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.range = 9f;
                _light.intensity = 1.1f;
                _light.color = color;
                _light.renderMode = LightRenderMode.ForceVertex;
                _light.shadows = LightShadows.None;
            }
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            if (_glow != null) _glow.SetActive(active);
            if (_light != null) _light.enabled = active;
        }

        private void Update()
        {
            if (!IsActive || _glow == null) return;
            float pulse = 0.8f + Mathf.Sin(Time.time * 2.6f + _phase) * 0.2f;
            _glow.transform.localScale = new Vector3(0.9f * pulse, 0.35f, 0.9f * pulse);
            transform.Rotate(0f, 18f * Time.deltaTime, 0f);
        }
    }

    /// <summary>
    /// "Secure this location" objective. Progress only accrues while the player holds the zone and no
    /// living enemy shares it, which makes securing a genuine fight rather than a timer.
    /// </summary>
    public class SecureZone : MonoBehaviour
    {
        public string Tag;
        public float Radius = 8.5f;
        public float RequiredSeconds = 14f;
        public bool Completed { get; private set; }

        private float _progress;
        private GameObject _ring;
        private Material _activeMaterial;
        private Material _idleMaterial;

        public float Progress01 { get { return WfMath.Clamp01(_progress / Mathf.Max(1f, RequiredSeconds)); } }

        public void Configure(string tag, float radius, float requiredSeconds)
        {
            Tag = tag;
            Radius = Mathf.Max(2f, radius);
            RequiredSeconds = Mathf.Max(3f, requiredSeconds);
        }

        public void BuildVisuals(GameAssets assets, Color color)
        {
            _idleMaterial = assets != null ? assets.Lit("SecureIdle", new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 1f), 0.2f, 0.1f) : null;
            _activeMaterial = assets != null ? assets.Lit("SecureActive", color, 0.35f, 0.2f) : null;

            _ring = PrimitiveFactory.CreateMesh("SecureRing", transform, MeshFactory.Cylinder(20), Vector3.zero,
                new Vector3(Radius * 2f, 0.14f, Radius * 2f), _idleMaterial, ColliderKind.None, GameLayers.Environment);

            int pillars = 6;
            for (int i = 0; i < pillars; i++)
            {
                float angle = i / (float)pillars * Mathf.PI * 2f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * Radius * 0.82f, 0f, Mathf.Sin(angle) * Radius * 0.82f);
                PrimitiveFactory.CreateBox("SecurePillar", transform, position + Vector3.up * 1.1f,
                    new Vector3(0.3f, 2.2f, 0.3f), _idleMaterial, true, Quaternion.identity, GameLayers.Environment);
            }
        }

        /// <summary>
        /// Advances the hold. enemiesInside comes from the AI director so clearing the zone is required.
        /// Returns true exactly once, when the zone becomes secured.
        /// </summary>
        public bool Tick(float deltaTime, bool playerInside, bool enemiesInside)
        {
            if (Completed) return false;

            bool holding = playerInside && !enemiesInside;
            float previous = _progress;
            _progress = holding
                ? Mathf.Min(RequiredSeconds, _progress + deltaTime)
                : Mathf.Max(0f, _progress - deltaTime * 1.5f);

            if (_ring != null)
            {
                Material material = holding ? _activeMaterial : _idleMaterial;
                MeshRenderer renderer = _ring.GetComponent<MeshRenderer>();
                if (renderer != null && material != null) renderer.sharedMaterial = material;
                float scale = holding ? 1f + Mathf.Sin(Time.time * 5f) * 0.02f : 1f;
                _ring.transform.localScale = new Vector3(Radius * 2f * scale, 0.14f, Radius * 2f * scale);
            }

            if (previous < RequiredSeconds && _progress >= RequiredSeconds)
            {
                Completed = true;
                return true;
            }
            return false;
        }

        public bool IsPlayerInside(Vector3 position)
        {
            Vector3 flat = position - transform.position;
            flat.y = 0f;
            return flat.sqrMagnitude <= Radius * Radius;
        }
    }

    /// <summary>
    /// The extraction pad. Purely presentational + a geometry query: the rules live in ExtractionModel
    /// inside Core, so the HUD, mission and marker all read the same source of truth.
    /// </summary>
    public class ExtractionBeacon : MonoBehaviour
    {
        public float Radius = 6.5f;
        public Vector3 Center { get { return transform.position; } }

        private GameObject _glow;
        private Light _light;
        private MeshRenderer _ringRenderer;
        private Material _availableMaterial;
        private Material _lockedMaterial;
        private float _phase;

        public void Configure(float radius)
        {
            Radius = Mathf.Max(2f, radius);
        }

        public void BuildVisuals(GameAssets assets)
        {
            _phase = Random.Range(0f, 6.28f);
            _availableMaterial = assets != null ? assets.ExtractionZone : null;
            _lockedMaterial = assets != null ? assets.BeaconLocked : null;

            GameObject ring = PrimitiveFactory.CreateMesh("ExtractionRing", transform, MeshFactory.Cylinder(24), Vector3.zero,
                new Vector3(Radius * 2f, 0.16f, Radius * 2f), _availableMaterial, ColliderKind.None, GameLayers.Environment);
            _ringRenderer = ring != null ? ring.GetComponent<MeshRenderer>() : null;

            PrimitiveFactory.CreateMesh("ExtractionMast", transform, MeshFactory.Cylinder(10), Vector3.zero,
                new Vector3(0.4f, 7f, 0.4f), assets != null ? assets.Metal : null, ColliderKind.None, GameLayers.Environment);
            _glow = PrimitiveFactory.CreateBox("ExtractionGlow", transform, new Vector3(0f, 7.2f, 0f),
                new Vector3(1.6f, 0.6f, 1.6f), _availableMaterial, false, Quaternion.identity, GameLayers.Environment);

            GameObject lightObject = new GameObject("ExtractionLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 7f, 0f);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 22f;
            _light.intensity = 1.6f;
            _light.renderMode = LightRenderMode.ForceVertex;
            _light.shadows = LightShadows.None;

            int posts = 4;
            for (int i = 0; i < posts; i++)
            {
                float angle = i / (float)posts * Mathf.PI * 2f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * Radius * 0.9f, 0f, Mathf.Sin(angle) * Radius * 0.9f);
                PrimitiveFactory.CreateBox("ExtractionPost", transform, position + Vector3.up * 0.9f,
                    new Vector3(0.25f, 1.8f, 0.25f), assets != null ? assets.Metal : null, false, Quaternion.identity, GameLayers.Environment);
            }
        }

        public void SetState(ExtractionState state)
        {
            bool available = state != ExtractionState.Locked;
            Material material = available ? _availableMaterial : _lockedMaterial;
            if (_ringRenderer != null && material != null) _ringRenderer.sharedMaterial = material;
            if (_glow != null && material != null)
            {
                MeshRenderer glowRenderer = _glow.GetComponent<MeshRenderer>();
                if (glowRenderer != null) glowRenderer.sharedMaterial = material;
            }
            if (_light != null)
            {
                _light.color = available ? new Color(0.4f, 0.95f, 0.55f) : new Color(0.95f, 0.5f, 0.25f);
                _light.intensity = state == ExtractionState.Countdown ? 2.6f : 1.4f;
            }
        }

        public bool IsInside(Vector3 position)
        {
            Vector3 flat = position - transform.position;
            flat.y = 0f;
            return flat.sqrMagnitude <= Radius * Radius;
        }

        private void Update()
        {
            if (_glow == null) return;
            float pulse = 0.85f + Mathf.Sin(Time.time * (2f + _phase * 0.1f)) * 0.15f;
            _glow.transform.localScale = new Vector3(1.6f * pulse, 0.6f, 1.6f * pulse);
        }
    }
}
