using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Perception for enemy AI: field of view cone with an occlusion raycast, a "close awareness"
    /// radius so enemies are not blind in tight corridors, and hearing with distance falloff.
    /// Perception is sampled on a timer rather than every frame - it is the most expensive AI step.
    /// </summary>
    public class EnemySenses
    {
        public const float SampleInterval = 0.15f;
        public const float CloseAwarenessRange = 6.5f;

        private readonly EnemyDef _def;
        private readonly Transform _origin;
        private readonly int _difficulty;
        private readonly Transform _head;

        private float _lastSample;
        private float _lastSeenTime = -999f;
        private float _lastHeardTime = -999f;
        private Vector3 _lastKnownPosition;
        private Vector3 _lastHeardPosition;
        private bool _hasTarget;

        public EnemySenses(EnemyDef def, Transform origin, Transform head, int difficulty)
        {
            _def = def;
            _origin = origin;
            _head = head;
            _difficulty = difficulty;
        }

        public bool HasTarget { get { return _hasTarget; } }
        public Vector3 LastKnownPosition { get { return _lastKnownPosition; } }
        public Vector3 LastHeardPosition { get { return _lastHeardPosition; } }
        public float TimeSinceSeen { get { return Time.time - _lastSeenTime; } }
        public float TimeSinceHeard { get { return Time.time - _lastHeardTime; } }
        public float DetectionRange { get { return _def != null ? _def.ScaledDetectionRange(_difficulty) : 40f; } }
        public float AttackRange { get { return _def != null ? _def.AttackRange : 30f; } }
        public float PreferredRange { get { return _def != null ? _def.PreferredRange : 18f; } }

        public Vector3 EyePosition
        {
            get { return _head != null ? _head.position : _origin.position + Vector3.up * 1.6f; }
        }

        /// <summary>
        /// Refreshes perception. Returns true when the target is currently visible.
        /// </summary>
        public bool Tick(Transform target, bool force)
        {
            if (target == null || _def == null)
            {
                _hasTarget = false;
                return false;
            }

            if (!force && Time.time - _lastSample < SampleInterval) return _hasTarget;
            _lastSample = Time.time;

            Vector3 eye = EyePosition;
            Vector3 targetPoint = target.position + Vector3.up * 1.2f;
            Vector3 toTarget = targetPoint - eye;
            float distance = toTarget.magnitude;

            if (distance > DetectionRange)
            {
                _hasTarget = false;
                return false;
            }

            Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z);
            if (distance > CloseAwarenessRange && flatDirection.sqrMagnitude > 0.001f)
            {
                Vector3 forward = new Vector3(_origin.forward.x, 0f, _origin.forward.z);
                if (forward.sqrMagnitude > 0.001f)
                {
                    float angle = Vector3.Angle(forward.normalized, flatDirection.normalized);
                    if (angle > _def.FieldOfView * 0.5f)
                    {
                        _hasTarget = false;
                        return false;
                    }
                }
            }

            RaycastHit hit;
            if (Physics.Raycast(eye, toTarget.normalized, out hit, distance, GameLayers.BlockingMask, QueryTriggerInteraction.Ignore))
            {
                _hasTarget = false;
                return false;
            }

            _hasTarget = true;
            _lastSeenTime = Time.time;
            _lastKnownPosition = target.position;
            return true;
        }

        /// <summary>Called when a noise reaches this enemy. Hearing has no cone, only distance.</summary>
        public bool OnHeardNoise(Vector3 position, float loudnessScale)
        {
            if (_def == null) return false;
            float distance = Vector3.Distance(_origin.position, position);
            float range = _def.HearingRange * Mathf.Max(0.2f, loudnessScale);
            if (distance > range) return false;

            _lastHeardTime = Time.time;
            _lastHeardPosition = position;
            if (!_hasTarget) _lastKnownPosition = position;
            return true;
        }

        public void ForceKnowTarget(Vector3 position)
        {
            _lastKnownPosition = position;
            _lastHeardTime = Time.time;
        }

        public bool IsMemoryFresh(float graceSeconds)
        {
            return Time.time - _lastSeenTime <= graceSeconds;
        }

        public void ClearTarget()
        {
            _hasTarget = false;
        }
    }
}
