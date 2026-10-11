using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Enemy movement. Uses a real NavMeshAgent when a navmesh exists at the enemy position, and falls
    /// back to the deterministic grid A* from Core when it does not (very small maps, blocked spawns
    /// or a failed runtime bake). Either way enemies follow actual paths - never a straight line to the
    /// player.
    /// </summary>
    public class EnemyLocomotion
    {
        private readonly Transform _transform;
        private readonly CharacterController _controller;
        private readonly NavMeshAgent _agent;
        private readonly GridPathfinder _pathfinder;
        private readonly bool _useAgent;

        private readonly List<Vector3> _path = new List<Vector3>(32);
        private int _pathIndex;
        private float _repathTimer;
        private float _verticalVelocity;
        private Vector3 _lastDestination;
        private float _stuckTimer;
        private Vector3 _lastPosition;

        public EnemyLocomotion(Transform transform, CharacterController controller, NavMeshAgent agent, GridPathfinder pathfinder)
        {
            _transform = transform;
            _controller = controller;
            _agent = agent;
            _pathfinder = pathfinder;
            _useAgent = agent != null;
            if (_useAgent)
            {
                agent.speed = 3.2f;
                agent.angularSpeed = 420f;
                agent.acceleration = 16f;
                agent.stoppingDistance = 0.4f;
                agent.autoBraking = true;
                agent.updateRotation = false;
                agent.updatePosition = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            }
        }

        public bool UsingNavMesh { get { return _useAgent && _agent != null && _agent.isOnNavMesh; } }
        public bool HasDestination { get; private set; }
        public float Speed { get; private set; }
        public bool ReachedDestination
        {
            get
            {
                if (!HasDestination) return true;
                if (UsingNavMesh) return _agent.remainingDistance <= _agent.stoppingDistance + 0.15f && !_agent.pathPending;
                if (_pathIndex >= _path.Count) return true;
                return false;
            }
        }

        public void SetSpeed(float speed, bool sprint)
        {
            float value = sprint ? speed * 1.5f : speed;
            if (_useAgent && _agent != null) _agent.speed = value;
            _baseSpeed = value;
        }

        private float _baseSpeed = 3.2f;

        public void SetDestination(Vector3 destination, bool force)
        {
            if (!force && HasDestination && (destination - _lastDestination).sqrMagnitude < 0.36f) return;

            _lastDestination = destination;
            HasDestination = true;
            _pathIndex = 0;
            _repathTimer = 0.6f;

            if (_useAgent && _agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(destination);
                return;
            }

            RepathTo(destination);
        }

        private void RepathTo(Vector3 destination)
        {
            if (_pathfinder == null) return;
            int count = _pathfinder.FindPath(_transform.position, destination, _path, 6000);
            _pathIndex = 0;
            if (count <= 0)
            {
                HasDestination = false;
            }
        }

        public void Stop()
        {
            HasDestination = false;
            if (_useAgent && _agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
            _path.Clear();
            _pathIndex = 0;
            Speed = 0f;
        }

        /// <summary>Moves the enemy along its path and returns the current ground speed.</summary>
        public float Tick(float deltaTime, float turnSpeed, Vector3? faceTarget)
        {
            if (_controller == null || !_controller.enabled)
            {
                Speed = 0f;
                return 0f;
            }

            _repathTimer = Mathf.Max(0f, _repathTimer - deltaTime);
            Vector3 moveDirection = Vector3.zero;

            if (UsingNavMesh)
            {
                if (_agent.hasPath && !_agent.pathPending)
                {
                    Vector3 next = _agent.steeringTarget;
                    Vector3 toNext = next - _transform.position;
                    toNext.y = 0f;
                    if (toNext.sqrMagnitude > 0.01f) moveDirection = toNext.normalized;
                    _agent.nextPosition = _transform.position;
                }
            }
            else if (HasDestination && _pathIndex < _path.Count)
            {
                Vector3 waypoint = _path[_pathIndex];
                Vector3 toWaypoint = waypoint - _transform.position;
                toWaypoint.y = 0f;

                if (toWaypoint.sqrMagnitude < 0.35f)
                {
                    _pathIndex++;
                }
                else
                {
                    moveDirection = toWaypoint.normalized;
                    // Simple obstacle avoidance: steer away from anything directly ahead.
                    RaycastHit hit;
                    if (Physics.SphereCast(_transform.position + Vector3.up * 0.9f, 0.32f, moveDirection, out hit, 1.2f,
                            GameLayers.BlockingMask, QueryTriggerInteraction.Ignore))
                    {
                        Vector3 sideways = Vector3.Cross(Vector3.up, moveDirection);
                        float sign = Vector3.Dot(hit.normal, sideways) > 0f ? -1f : 1f;
                        moveDirection = (moveDirection + sideways * sign * 0.9f).normalized;
                    }
                }

                if (_repathTimer <= 0f && _pathIndex >= _path.Count)
                {
                    HasDestination = false;
                }
            }

            // Stuck detection: an enemy wedged on geometry re-paths instead of grinding in place.
            if (_lastPosition != Vector3.zero && (moveDirection.sqrMagnitude > 0.01f))
            {
                float moved = (_transform.position - _lastPosition).magnitude;
                if (moved < deltaTime * 0.25f) _stuckTimer += deltaTime;
                else _stuckTimer = 0f;
                if (_stuckTimer > 1.2f)
                {
                    _stuckTimer = 0f;
                    if (HasDestination) SetDestination(_lastDestination, true);
                }
            }
            _lastPosition = _transform.position;

            Vector3 motion = moveDirection * _baseSpeed;
            Speed = motion.magnitude;

            if (_controller.isGrounded) _verticalVelocity = -2f;
            else _verticalVelocity += -18f * deltaTime;

            Vector3 displacement = (motion + Vector3.up * _verticalVelocity) * deltaTime;
            _controller.Move(displacement);

            FaceDirection(faceTarget.HasValue ? faceTarget.Value : (moveDirection.sqrMagnitude > 0.01f ? _transform.position + moveDirection : _transform.position + _transform.forward), turnSpeed, deltaTime);
            return Speed;
        }

        private void FaceDirection(Vector3 target, float turnSpeed, float deltaTime)
        {
            Vector3 direction = target - _transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
            _transform.rotation = Quaternion.RotateTowards(_transform.rotation, look, turnSpeed * deltaTime);
        }

        /// <summary>Teleports the agent (used when spawning/despawning) and keeps NavMesh in sync.</summary>
        public void Warp(Vector3 position)
        {
            if (_controller != null) _controller.enabled = false;
            _transform.position = position;
            if (_controller != null) _controller.enabled = true;
            if (_useAgent && _agent != null && _agent.isOnNavMesh) _agent.Warp(position);
            HasDestination = false;
            _path.Clear();
            _pathIndex = 0;
            _verticalVelocity = 0f;
        }

        public void Disable()
        {
            Stop();
            if (_agent != null) _agent.enabled = false;
        }
    }
}
