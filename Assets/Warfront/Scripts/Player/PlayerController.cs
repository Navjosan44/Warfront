using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Third person player movement. CharacterController based with explicit acceleration curves so
    /// the feel is identical regardless of frame rate, and mobile friendly (no physics fighting the
    /// controller, no root motion dependencies).
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public const float WalkSpeed = 3.3f;
        public const float SprintSpeed = 5.9f;
        public const float AimWalkSpeed = 2.1f;
        public const float CrouchSpeed = 1.75f;
        public const float DodgeSpeed = 9.5f;
        public const float DodgeDuration = 0.26f;
        public const float DodgeCooldown = 1.1f;
        public const float JumpVelocity = 5.4f;
        public const float Gravity = -19f;
        public const float StandHeight = 1.78f;
        public const float CrouchHeight = 1.15f;

        private CharacterController _controller;
        private Transform _cameraPivot;
        private GameContext _context;
        private Vector3 _velocity;
        private Vector3 _horizontalVelocity;
        private float _dodgeTimer;
        private float _dodgeCooldown;
        private Vector3 _dodgeDirection;
        private bool _crouching;
        private bool _sprinting;
        private float _stepDistance;
        private float _lastNoiseTime;
        private float _slopeSlide;

        public bool IsCrouching { get { return _crouching; } }
        public bool IsSprinting { get { return _sprinting; } }
        public bool IsDodging { get { return _dodgeTimer > 0f; } }
        public bool IsGrounded { get; private set; }
        public bool IsDead { get; private set; }
        public float Speed { get { return _horizontalVelocity.magnitude; } }
        public float Speed01 { get { return WfMath.Clamp01(Speed / SprintSpeed); } }
        public Vector3 Velocity { get { return _velocity; } }
        public float Yaw { get; private set; }

        /// <summary>Vertical pitch applied to the body rig when aiming (set by the camera each frame).</summary>
        public float AimPitchDegrees { get; set; }

        public CharacterRig Rig { get; private set; }
        public Damageable Health { get; private set; }
        public PlayerLoadout Loadout { get; private set; }

        private bool _inputEnabled = true;
        private float _movingTime;

        public void Initialize(GameContext context, CharacterController controller, CharacterRig rig, Damageable health, PlayerLoadout loadout)
        {
            _context = context;
            _controller = controller;
            Rig = rig;
            Health = health;
            Loadout = loadout;
            _controller.height = StandHeight;
            _controller.radius = 0.34f;
            _controller.center = new Vector3(0f, StandHeight * 0.5f, 0f);
            _controller.slopeLimit = 50f;
            _controller.stepOffset = 0.4f;
            _controller.skinWidth = 0.03f;
        }

        public void SetCameraPivot(Transform pivot)
        {
            _cameraPivot = pivot;
        }

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
            if (!enabled)
            {
                _horizontalVelocity = Vector3.zero;
                _velocity = Vector3.zero;
            }
        }

        public void Teleport(Vector3 position)
        {
            if (_controller == null) return;
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _horizontalVelocity = Vector3.zero;
            _velocity = Vector3.zero;
        }

        public void OnDeath()
        {
            IsDead = true;
            SetInputEnabled(false);
            if (Rig != null) Rig.PlayDeath();
        }

        public void OnRespawn(Vector3 position)
        {
            IsDead = false;
            SetInputEnabled(true);
            if (Rig != null) Rig.ResetPose();
            Teleport(position);
        }

        private void Update()
        {
            if (_controller == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            InputService input = _context != null ? _context.Input : null;
            bool canAct = _inputEnabled && !IsDead && (_context == null || !_context.Flow.IsPaused);

            Vector2 moveInput = canAct && input != null ? input.Move : Vector2.zero;
            bool wantsSprint = canAct && input != null && input.SprintHeld && !_crouching && moveInput.y > 0.15f;
            bool wantsCrouch = canAct && input != null && input.CrouchPressed;
            bool wantsDodge = canAct && input != null && input.DodgePressed;
            bool wantsJump = canAct && input != null && input.JumpPressed;
            bool aiming = canAct && input != null && input.AimHeld;

            if (wantsCrouch) SetCrouch(!_crouching);

            // Dodge: a short directional burst with a cooldown, so it is strong but not spammable.
            _dodgeCooldown = Mathf.Max(0f, _dodgeCooldown - dt);
            if (wantsDodge && _dodgeCooldown <= 0f && IsGrounded && !IsDodging)
            {
                _dodgeTimer = DodgeDuration;
                _dodgeCooldown = DodgeCooldown;
                _dodgeDirection = DirectionFromInput(moveInput);
                if (_dodgeDirection.sqrMagnitude < 0.01f) _dodgeDirection = transform.forward;
                if (_context != null && _context.Audio != null) _context.Audio.PlayUi(AudioCue.Dodge);
            }

            Vector3 desired = DirectionFromInput(moveInput);
            float targetSpeed;
            if (_crouching) targetSpeed = CrouchSpeed;
            else if (aiming) targetSpeed = AimWalkSpeed;
            else if (wantsSprint) targetSpeed = SprintSpeed;
            else targetSpeed = WalkSpeed;

            _sprinting = wantsSprint && !_crouching && !aiming;

            float acceleration = IsGrounded ? 14f : 5f;
            Vector3 targetVelocity = desired * targetSpeed;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, acceleration * dt);

            if (_dodgeTimer > 0f)
            {
                _dodgeTimer -= dt;
                _horizontalVelocity = _dodgeDirection * DodgeSpeed;
            }

            // Gravity + ground stick. A small downward bias keeps the controller glued to slopes.
            if (IsGrounded && _velocity.y <= 0f)
            {
                _velocity.y = -2.5f;
                if (wantsJump && !IsDodging)
                {
                    _velocity.y = JumpVelocity;
                    if (_context != null && _context.Audio != null) _context.Audio.PlayUi(AudioCue.Jump);
                }
            }
            else
            {
                _velocity.y += Gravity * dt;
            }

            Vector3 motion = _horizontalVelocity + new Vector3(0f, _velocity.y, 0f);
            CollisionFlags flags = _controller.Move(motion * dt);
            IsGrounded = (flags & CollisionFlags.Below) != 0 || _controller.isGrounded;
            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f) _velocity.y = 0f;

            FaceMovement(desired, aiming, dt);
            Footsteps(dt);
            UpdateRig(aiming, dt);
            EmitNoise(desired);
        }

        private Vector3 DirectionFromInput(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude < 0.0001f) return Vector3.zero;
            float yaw = _cameraPivot != null ? _cameraPivot.eulerAngles.y : transform.eulerAngles.y;
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 direction = forward * moveInput.y + right * moveInput.x;
            return Vector3.ClampMagnitude(direction, 1f);
        }

        private void FaceMovement(Vector3 desired, bool aiming, float dt)
        {
            Vector3 facing = desired;
            if (aiming && _cameraPivot != null)
            {
                facing = _cameraPivot.forward;
                facing.y = 0f;
            }
            if (facing.sqrMagnitude < 0.001f) return;

            Quaternion target = Quaternion.LookRotation(facing.normalized, Vector3.up);
            float turnRate = aiming ? 14f : 11f;
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Mathf.Clamp01(turnRate * dt));
            Yaw = transform.eulerAngles.y;
        }

        private void Footsteps(float dt)
        {
            float speed = _horizontalVelocity.magnitude;
            if (speed < 0.4f)
            {
                _movingTime = 0f;
                return;
            }

            _movingTime += dt;
            _stepDistance += speed * dt;
            float strideLength = _crouching ? 2.6f : _sprinting ? 2.1f : 2.4f;
            if (_stepDistance >= strideLength)
            {
                _stepDistance = 0f;
                if (_context != null && _context.Audio != null)
                {
                    _context.Audio.PlayFootstep(_crouching ? 0.35f : _sprinting ? 1f : 0.7f,
                        transform.position + Vector3.up * 0.1f);
                }
            }
        }

        private void EmitNoise(Vector3 desired)
        {
            // Movement noise feeds AI hearing. Crouching and aiming are quiet, sprinting is loud.
            float loudness = 0f;
            if (desired.sqrMagnitude > 0.01f)
            {
                loudness = _crouching ? 0.25f : _sprinting ? 1f : 0.55f;
            }
            if (IsDodging) loudness = 1.2f;

            if (loudness <= 0f || Time.time - _lastNoiseTime < 0.4f) return;
            _lastNoiseTime = Time.time;
            if (loudness > 0.5f) GameEvents.RaiseNoise(transform.position);
        }

        private void UpdateRig(bool aiming, float dt)
        {
            if (Rig == null) return;
            Rig.Tick(dt, Speed01, aiming, AimPitchDegrees, _crouching, IsGrounded);
        }

        public void SetCrouch(bool crouch)
        {
            if (_crouching == crouch) return;
            _crouching = crouch;
            if (_controller != null)
            {
                _controller.height = crouch ? CrouchHeight : StandHeight;
                _controller.center = new Vector3(0f, _controller.height * 0.5f, 0f);
            }
            if (_context != null && _context.Audio != null) _context.Audio.PlayUi(AudioCue.Crouch);
        }

        /// <summary>Head position used for AI line of sight checks and camera pivot.</summary>
        public Vector3 EyePosition
        {
            get { return transform.position + Vector3.up * (_crouching ? 1.05f : 1.62f); }
        }

        public Vector3 ChestPosition
        {
            get { return transform.position + Vector3.up * (_crouching ? 0.8f : 1.25f); }
        }
    }
}
