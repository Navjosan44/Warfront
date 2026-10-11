using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Third person orbit camera.
    ///
    /// Runs in LateUpdate so the player is already moved, handles orbit/zoom/aim-down-sights,
    /// pushes the camera out of walls with a sphere cast, and exposes the aim ray that weapons use -
    /// which guarantees the reticle and the bullet agree on where the player is pointing.
    /// </summary>
    public class PlayerCamera : MonoBehaviour
    {
        public const float HipDistance = 4.1f;
        public const float AimDistance = 2.35f;
        public const float ShoulderOffsetHip = 0.35f;
        public const float ShoulderOffsetAim = 0.62f;
        public const float MinPitch = -55f;
        public const float MaxPitch = 68f;
        public const float HipFov = 62f;

        private Transform _target;
        private PlayerController _player;
        private GameContext _context;
        private Camera _camera;

        private float _yaw;
        private float _pitch = 8f;
        private float _distance = HipDistance;
        private float _fov = HipFov;
        private Vector3 _smoothedPivot;
        private Vector3 _recoilOffset;
        private Vector3 _recoilVelocity;
        private float _aimBlend;
        private float _shake;
        private bool _initialized;

        public bool IsAiming { get; private set; }
        public float AimBlend { get { return _aimBlend; } }
        public float Pitch { get { return _pitch; } }
        public Camera Camera { get { return _camera; } }

        /// <summary>Ray through the reticle. Weapons fire along this so ADS always feels aligned.</summary>
        public Ray AimRay
        {
            get
            {
                if (_camera != null) return _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                return new Ray(transform.position, transform.forward);
            }
        }

        public void Initialize(GameContext context, Camera camera, Transform target, PlayerController player, float startYaw)
        {
            _context = context;
            _camera = camera;
            _target = target;
            _player = player;
            _yaw = startYaw;
            _smoothedPivot = target.position + Vector3.up * 1.45f;
            _initialized = true;
            ApplyTransform(0f);
        }

        public void AddRecoil(float vertical, float horizontal)
        {
            _recoilOffset += new Vector3(-vertical, horizontal * Random.Range(-1f, 1f), 0f) * 0.85f;
        }

        public void AddShake(float amount)
        {
            _shake = Mathf.Min(1.2f, _shake + amount);
        }

        public void SnapToTarget()
        {
            if (_target == null) return;
            _smoothedPivot = _target.position + Vector3.up * 1.45f;
            _recoilOffset = Vector3.zero;
            _recoilVelocity = Vector3.zero;
            ApplyTransform(0f);
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            float dt = Time.deltaTime;
            ApplyTransform(dt);
        }

        private void ApplyTransform(float dt)
        {
            if (_target == null) return;

            InputService input = _context != null ? _context.Input : null;
            bool acceptLook = _context == null || !_context.Flow.IsPaused;
            bool alive = _player == null || !_player.IsDead;

            if (alive && acceptLook && input != null)
            {
                Vector2 look = input.LookDelta;
                if (_aimBlend > 0.5f) look *= 0.62f;   // finer control while aiming
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, MinPitch, MaxPitch);
            }

            IsAiming = alive && input != null && input.AimHeld && (_context == null || !_context.Flow.IsPaused);
            _aimBlend = Mathf.MoveTowards(_aimBlend, IsAiming ? 1f : 0f, dt * 9f);

            // Recoil spring: push the sight picture then recover.
            _recoilOffset = Vector3.SmoothDamp(_recoilOffset, Vector3.zero, ref _recoilVelocity, 0.16f, 12f, Mathf.Max(dt, 0.0001f));
            _shake = Mathf.Max(0f, _shake - dt * 3.2f);

            Vector3 pivotTarget = _target.position + Vector3.up * (_player != null && _player.IsCrouching ? 1.15f : 1.48f);
            _smoothedPivot = _smoothedPivot == Vector3.zero
                ? pivotTarget
                : Vector3.Lerp(_smoothedPivot, pivotTarget, Mathf.Clamp01(dt * 14f));

            Quaternion orbit = Quaternion.Euler(_pitch + _recoilOffset.x, _yaw + _recoilOffset.y, 0f);
            Vector3 lookDirection = orbit * Vector3.forward;
            float distanceTarget = Mathf.Lerp(HipDistance, AimDistance, _aimBlend);
            _distance = Mathf.Lerp(_distance, distanceTarget, Mathf.Clamp01(dt * 8f));

            Vector3 shoulder = orbit * Vector3.right * Mathf.Lerp(ShoulderOffsetHip, ShoulderOffsetAim, _aimBlend);
            Vector3 desiredPosition = _smoothedPivot + shoulder - lookDirection * _distance + Vector3.up * 0.12f;

            // Never let geometry clip between the player and the camera.
            Vector3 castOrigin = _smoothedPivot + Vector3.up * 0.12f;
            Vector3 castDirection = desiredPosition - castOrigin;
            float castDistance = castDirection.magnitude;
            if (castDistance > 0.01f)
            {
                RaycastHit hit;
                if (Physics.SphereCast(castOrigin, 0.28f, castDirection / castDistance, out hit, castDistance,
                        GameLayers.BlockingMask, QueryTriggerInteraction.Ignore))
                {
                    desiredPosition = castOrigin + castDirection.normalized * Mathf.Max(0.6f, hit.distance - 0.12f);
                }
            }

            if (_shake > 0.001f)
            {
                desiredPosition += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * _shake * 0.08f;
            }

            transform.position = desiredPosition;
            transform.rotation = Quaternion.LookRotation(_smoothedPivot + Vector3.up * 0.05f - desiredPosition, Vector3.up);

            float targetFov = Mathf.Lerp(HipFov, ResolveAimFov(), _aimBlend);
            _fov = Mathf.Lerp(_fov, targetFov, Mathf.Clamp01(dt * 10f));
            if (_camera != null) _camera.fieldOfView = _fov;

            if (_player != null) _player.AimPitchDegrees = Mathf.DeltaAngle(0f, _pitch);
        }

        private float ResolveAimFov()
        {
            if (_context == null || _context.Player == null || _context.Player.Loadout == null) return 42f;
            WeaponStats stats = _context.Player.Loadout.CurrentStats;
            return stats.AdsFov > 1f ? stats.AdsFov : 42f;
        }
    }
}
