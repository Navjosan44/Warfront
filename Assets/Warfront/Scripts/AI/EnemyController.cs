using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// One enemy soldier.
    ///
    /// States: Idle -> Patrol -> Alert -> Investigate -> Chase -> Attack -> Cover/Retreat -> Dead.
    /// All five archetypes share this FSM and differ only through EnemyDef, so behaviour is authored in
    /// one place. Perception is sampled on a timer, the AI tick is budgeted by the director, and
    /// movement goes through NavMesh (or grid A*) - never a straight line at the player.
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        public EnemyDef Def { get; private set; }
        public Damageable Health { get; private set; }
        public CharacterRig Rig { get; private set; }
        public int Difficulty { get; private set; }
        public string SpawnTag { get; private set; }
        public bool IsObjectiveTarget { get; private set; }
        public bool IsAlive { get { return _alive; } }
        public string Callsign { get { return Def != null ? Def.DisplayName : "Enemy"; } }
        public string StateName { get { return _fsm != null ? _fsm.CurrentName : "None"; } }

        private GameContext _context;
        private MissionRuntime _mission;
        private EnemyDirector _director;
        private EnemySenses _senses;
        private EnemyLocomotion _locomotion;
        private StateMachine<EnemyController> _fsm;
        private NavMeshAgent _agent;
        private WfRandom _random;

        private readonly List<Vector3> _patrolPoints = new List<Vector3>(4);
        private int _patrolIndex;
        private float _stateTimer;
        private float _fireCooldown;
        private float _aimProgress;
        private int _burstShotsLeft;
        private float _reactionTimer;
        private float _deathTimer;
        private float _strafeTimer;
        private float _strafeSign = 1f;
        private bool _alive;
        private bool _alertedAllies;
        private Vector3 _spawnPosition;
        private int _shotsFiredThisBurst;
        private List<Vector3> _coverPoints;

        public Vector3 SpawnPosition { get { return _spawnPosition; } }
        public Vector3 Position { get { return transform.position; } }
        public Vector3 ChestPosition { get { return transform.position + Vector3.up * (Def != null ? 1.25f * Def.BodyScale : 1.25f); } }

        public void Initialize(GameContext context, EnemyDef def, int difficulty, int spawnIndex, string spawnTag, bool isObjectiveTarget,
            List<Vector3> patrolPoints, List<Vector3> coverPoints)
        {
            _context = context;
            _mission = context != null ? context.Mission : null;
            _director = context != null ? context.EnemyDirector : null;
            Def = def;
            Difficulty = Mathf.Max(1, difficulty);
            SpawnTag = spawnTag;
            IsObjectiveTarget = isObjectiveTarget;
            _random = new WfRandom(WfMath.Combine(WfMath.Hash(def != null ? def.Id : "enemy"), spawnIndex * 7919 + 13));
            _spawnPosition = transform.position;
            _alive = true;
            _alertedAllies = false;
            _deathTimer = 0f;

            _patrolPoints.Clear();
            if (patrolPoints != null && patrolPoints.Count > 1)
            {
                for (int i = 0; i < patrolPoints.Count; i++) _patrolPoints.Add(patrolPoints[i]);
                int offset = Mathf.Abs(spawnIndex) % _patrolPoints.Count;
                _patrolIndex = offset;
            }

            _coverPoints = coverPoints;

            if (Health == null) Health = gameObject.AddComponent<Damageable>();
            float health = def.ScaledHealth(Difficulty);
            float armor = def.Armor * (1f + (Difficulty - 1) * 0.12f);
            Health.Initialize(health, armor, false, 1);
            Health.DamagedCallback = OnDamaged;
            Health.DiedCallback = OnDied;

            BuildRig();
            BuildLocomotion();
            BuildStateMachine();
            _stateTimer = 0f;
        }

        private void BuildRig()
        {
            if (Rig == null) Rig = new CharacterRig();
            if (Rig.Built) return;

            GameAssets assets = _context != null ? _context.Assets : null;
            Material body = assets != null ? CreateTintedMaterial("EnemyBody_" + Def.Id, Def.BodyColor) : null;
            Material accent = assets != null ? CreateTintedMaterial("EnemyAccent_" + Def.Id, Def.AccentColor) : null;
            Rig.Build(transform, body, accent, Def.BodyScale, false, Health, true);
        }

        private Material CreateTintedMaterial(string name, Color color)
        {
            GameAssets assets = _context.Assets;
            return assets.Lit(name, color, 0.18f, 0.08f);
        }

        private void BuildLocomotion()
        {
            CharacterController controller = GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<CharacterController>();
                controller.height = 1.7f * Def.BodyScale;
                controller.radius = 0.32f * Def.BodyScale;
                controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
                controller.slopeLimit = 50f;
                controller.stepOffset = 0.4f;
                controller.skinWidth = 0.03f;
            }

            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.enabled = true;
            _agent.radius = 0.34f * Def.BodyScale;
            _agent.height = controller.height;
            _agent.baseOffset = 0f;

            GridPathfinder pathfinder = _context != null && _context.Mission != null && _context.Mission.Level != null
                ? _context.Mission.Level.CreatePathfinder()
                : null;

            _locomotion = new EnemyLocomotion(transform, controller, _agent, pathfinder);
            _locomotion.SetSpeed(Def.MoveSpeed, false);
            _senses = new EnemySenses(Def, transform, Rig != null ? Rig.Head : null, Difficulty);
        }

        private void BuildStateMachine()
        {
            _fsm = new StateMachine<EnemyController>(this);
            _fsm.Add(new ActionState<EnemyController>("Idle", EnterIdle, TickIdle, null));
            _fsm.Add(new ActionState<EnemyController>("Patrol", EnterPatrol, TickPatrol, null));
            _fsm.Add(new ActionState<EnemyController>("Alert", EnterAlert, TickAlert, null));
            _fsm.Add(new ActionState<EnemyController>("Investigate", EnterInvestigate, TickInvestigate, null));
            _fsm.Add(new ActionState<EnemyController>("Chase", EnterChase, TickChase, null));
            _fsm.Add(new ActionState<EnemyController>("Attack", EnterAttack, TickAttack, null));
            _fsm.Add(new ActionState<EnemyController>("Cover", EnterCover, TickCover, null));
            _fsm.Add(new ActionState<EnemyController>("Retreat", EnterRetreat, TickRetreat, null));
            _fsm.Add(new ActionState<EnemyController>("Dead", EnterDead, TickDead, null));
            _fsm.Change(_patrolPoints.Count > 0 ? "Patrol" : "Idle");
        }

        private void Update()
        {
            if (!_alive || Def == null) return;
            if (_context != null && _context.Flow != null && _context.Flow.IsPaused) return;

            float dt = Time.deltaTime;

            // AI level of detail: distant enemies think and move less often.
            int stride = _director != null ? _director.TickStride : 1;
            if (stride > 1)
            {
                _lodAccumulator += dt;
                float interval = stride / 30f;
                if (_lodAccumulator < interval) return;
                dt = _lodAccumulator;
                _lodAccumulator = 0f;
            }

            PlayerController player = _director != null ? _director.PlayerTarget : null;
            bool forcedSense = _fsm.CurrentName == "Attack" || _fsm.CurrentName == "Chase";
            if (player != null && !player.IsDead)
            {
                _senses.Tick(player.transform, forcedSense);
            }
            else
            {
                _senses.ClearTarget();
            }

            _fsm.Tick(dt);
            UpdateRig(dt, player);
        }

        private float _lodAccumulator;

        private void UpdateRig(float dt, PlayerController player)
        {
            if (Rig == null) return;
            float speed01 = WfMath.Clamp01(_locomotion != null ? _locomotion.Speed / Mathf.Max(0.1f, Def.ChaseSpeed) : 0f);
            float aimPitch = 0f;
            bool aiming = _fsm != null && (_fsm.CurrentName == "Attack" || _fsm.CurrentName == "Cover");
            if (aiming && player != null)
            {
                Vector3 toTarget = (player.ChestPosition - ChestPosition);
                aimPitch = -Mathf.Asin(Mathf.Clamp(toTarget.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            }
            Rig.Tick(dt, speed01, aiming, aimPitch, false, _locomotion != null && _locomotion.Speed > 0.05f);
        }

        // ------------------------------------------------------------------ states

        private void EnterIdle(EnemyController self)
        {
            _stateTimer = _random.Range(1.6f, 4.5f);
            if (_locomotion != null) _locomotion.Stop();
        }

        private void TickIdle(EnemyController self, float dt)
        {
            _stateTimer -= dt;
            if (_senses.HasTarget) { _fsm.Change("Alert"); return; }
            if (_senses.TimeSinceHeard < 3f) { _fsm.Change("Investigate"); return; }
            if (_stateTimer <= 0f) _fsm.Change(_patrolPoints.Count > 0 ? "Patrol" : "Idle");
        }

        private void EnterPatrol(EnemyController self)
        {
            if (_patrolPoints.Count == 0)
            {
                _fsm.Change("Idle");
                return;
            }
            Vector3 point = _patrolPoints[_patrolIndex % _patrolPoints.Count];
            _patrolIndex++;
            if (_locomotion != null)
            {
                _locomotion.SetSpeed(Def.MoveSpeed, false);
                _locomotion.SetDestination(point, true);
            }
        }

        private void TickPatrol(EnemyController self, float dt)
        {
            if (_senses.HasTarget) { _fsm.Change("Alert"); return; }
            if (_senses.TimeSinceHeard < 3f) { _fsm.Change("Investigate"); return; }
            if (_locomotion != null && _locomotion.ReachedDestination) _fsm.Change("Idle");
        }

        private void EnterAlert(EnemyController self)
        {
            _reactionTimer = Def.ReactionTime * (1f - (Difficulty - 1) * 0.04f);
            _reactionTimer = Mathf.Max(0.12f, _reactionTimer);
            if (_locomotion != null) _locomotion.Stop();
            if (_director != null) _director.ReportContact(this);
            if (_context != null && _context.Audio != null) _context.Audio.PlayEnemyAlert(transform.position, Def.Archetype);
            GameEvents.RaiseEnemyAlerted(Callsign);
        }

        private void TickAlert(EnemyController self, float dt)
        {
            _reactionTimer -= dt;
            PlayerController player = _director != null ? _director.PlayerTarget : null;
            if (player != null && _locomotion != null)
            {
                _locomotion.Stop();
                FaceTowards(player.ChestPosition, dt, Def.TurnSpeed * 0.9f);
            }
            if (_reactionTimer <= 0f) _fsm.Change("Chase");
        }

        private void EnterInvestigate(EnemyController self)
        {
            _stateTimer = Def.SearchDuration;
            Vector3 point = _senses.TimeSinceHeard < _senses.TimeSinceSeen ? _senses.LastHeardPosition : _senses.LastKnownPosition;
            if (_locomotion != null)
            {
                _locomotion.SetSpeed(Def.ChaseSpeed, false);
                _locomotion.SetDestination(point, true);
            }
        }

        private void TickInvestigate(EnemyController self, float dt)
        {
            if (_senses.HasTarget) { _fsm.Change("Alert"); return; }
            _stateTimer -= dt;
            if (_locomotion != null && _locomotion.ReachedDestination)
            {
                // Look around the point of interest before giving up.
                transform.Rotate(0f, 55f * dt, 0f);
            }
            if (_stateTimer <= 0f) _fsm.Change(_patrolPoints.Count > 0 ? "Patrol" : "Idle");
        }

        private void EnterChase(EnemyController self)
        {
            if (_locomotion != null) _locomotion.SetSpeed(Def.ChaseSpeed, true);
        }

        private void TickChase(EnemyController self, float dt)
        {
            PlayerController player = _director != null ? _director.PlayerTarget : null;
            if (player == null) { _fsm.Change("Patrol"); return; }

            if (_senses.HasTarget)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance <= Def.AttackRange) { _fsm.Change("Attack"); return; }

                if (_locomotion != null)
                {
                    Vector3 goal = player.transform.position;
                    if (!Def.ChargeBehaviour) goal -= (player.transform.position - transform.position).normalized * Def.PreferredRange * 0.75f;
                    _locomotion.SetDestination(goal, false);
                }
            }
            else if (!_senses.IsMemoryFresh(Def.LoseTargetGraceSeconds))
            {
                _fsm.Change("Investigate");
            }
            else if (_locomotion != null)
            {
                _locomotion.SetDestination(_senses.LastKnownPosition, false);
            }
        }

        private void EnterAttack(EnemyController self)
        {
            _aimProgress = 0f;
            _burstShotsLeft = Mathf.Max(1, Def.BurstSize);
            _shotsFiredThisBurst = 0;
            _fireCooldown = 0.15f;
            _strafeTimer = _random.Range(0.8f, 1.8f);
            _strafeSign = _random.NextFloat() < 0.5f ? -1f : 1f;
            if (_locomotion != null) _locomotion.Stop();
        }

        private void TickAttack(EnemyController self, float dt)
        {
            PlayerController player = _director != null ? _director.PlayerTarget : null;
            if (player == null || player.IsDead) { _fsm.Change("Patrol"); return; }

            float health01 = Health != null && Health.Model != null ? Health.Model.HealthNormalized : 1f;
            if (health01 < Def.RetreatHealthThreshold && Def.Archetype != EnemyArchetype.Elite)
            {
                _fsm.Change(Def.UsesCover ? "Cover" : "Retreat");
                return;
            }

            if (!_senses.HasTarget)
            {
                if (_senses.IsMemoryFresh(Def.LoseTargetGraceSeconds)) { _fsm.Change("Chase"); return; }
                _fsm.Change("Investigate");
                return;
            }

            Vector3 targetPoint = player.ChestPosition;
            FaceTowards(targetPoint, dt, Def.TurnSpeed);

            float distance = Vector3.Distance(transform.position, targetPoint);
            if (distance > Def.AttackRange * 1.15f) { _fsm.Change("Chase"); return; }

            // Ranged archetypes keep their distance: back off when the player closes in.
            if (Def.PrefersRanged && distance < Def.MinEngagementRange)
            {
                if (_locomotion != null)
                {
                    Vector3 away = (transform.position - player.transform.position).normalized;
                    _locomotion.SetDestination(transform.position + away * 4f, false);
                    _locomotion.Tick(dt, Def.TurnSpeed, targetPoint);
                }
            }
            else if (!Def.PrefersRanged && distance > Def.PreferredRange * 1.35f && !Def.ChargeBehaviour)
            {
                if (_locomotion != null)
                {
                    _locomotion.SetDestination(player.transform.position, false);
                    _locomotion.Tick(dt, Def.TurnSpeed, targetPoint);
                }
            }
            else
            {
                // In position: strafe so the fight is not a static exchange.
                _strafeTimer -= dt;
                if (_strafeTimer <= 0f)
                {
                    _strafeTimer = _random.Range(1.1f, 2.4f);
                    _strafeSign = -_strafeSign;
                }
                if (_locomotion != null)
                {
                    Vector3 sideways = Vector3.Cross(Vector3.up, (targetPoint - transform.position).normalized) * _strafeSign;
                    Vector3 goal = transform.position + sideways * 2.2f;
                    _locomotion.SetDestination(goal, false);
                    _locomotion.Tick(dt, Def.TurnSpeed, targetPoint);
                }
            }

            _aimProgress += dt;
            if (_aimProgress < Def.AimTime) return;

            _fireCooldown -= dt;
            if (_fireCooldown > 0f) return;
            if (!HasClearShot(targetPoint)) return;

            FireOneRound(targetPoint);

            _shotsFiredThisBurst++;
            if (_shotsFiredThisBurst >= _burstShotsLeft)
            {
                _shotsFiredThisBurst = 0;
                _fireCooldown = Def.BurstPause + _random.Range(-0.12f, 0.25f);
                _burstShotsLeft = Mathf.Max(1, Def.BurstSize);
            }
            else
            {
                _fireCooldown = Def.SecondsBetweenShots;
            }
        }

        private void EnterCover(EnemyController self)
        {
            _stateTimer = Def.RetreatDuration;
            if (_locomotion != null)
            {
                _locomotion.SetSpeed(Def.ChaseSpeed, false);
                _locomotion.SetDestination(FindCoverPoint(), true);
            }
        }

        private void TickCover(EnemyController self, float dt)
        {
            _stateTimer -= dt;
            if (_locomotion != null && !_locomotion.HasDestination)
            {
                _locomotion.SetDestination(FindCoverPoint(), false);
            }
            PlayerController player = _director != null ? _director.PlayerTarget : null;
            if (player != null && _senses.HasTarget)
            {
                FaceTowards(player.ChestPosition, dt, Def.TurnSpeed);
                _fireCooldown -= dt;
                if (_fireCooldown <= 0f && HasClearShot(player.ChestPosition))
                {
                    FireOneRound(player.ChestPosition);
                    _fireCooldown = Def.SecondsBetweenShots * 2.4f;
                }
            }

            if (_stateTimer <= 0f)
            {
                float health01 = Health != null && Health.Model != null ? Health.Model.HealthNormalized : 1f;
                _fsm.Change(health01 < Def.RetreatHealthThreshold ? "Retreat" : "Chase");
            }
        }

        private void EnterRetreat(EnemyController self)
        {
            _stateTimer = Def.RetreatDuration + 1f;
            if (_locomotion != null && _director != null && _director.PlayerTarget != null)
            {
                Vector3 away = (transform.position - _director.PlayerTarget.transform.position).normalized;
                _locomotion.SetSpeed(Def.ChaseSpeed, true);
                _locomotion.SetDestination(transform.position + away * 12f, true);
            }
        }

        private void TickRetreat(EnemyController self, float dt)
        {
            _stateTimer -= dt;
            PlayerController player = _director != null ? _director.PlayerTarget : null;
            if (player != null)
            {
                FaceTowards(player.ChestPosition, dt, Def.TurnSpeed * 0.7f);
                _fireCooldown -= dt;
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (_fireCooldown <= 0f && distance < Def.AttackRange * 0.8f && HasClearShot(player.ChestPosition))
                {
                    FireOneRound(player.ChestPosition);
                    _fireCooldown = Def.SecondsBetweenShots * 3f;
                }
            }
            if (_stateTimer <= 0f) _fsm.Change("Chase");
        }

        private void EnterDead(EnemyController self)
        {
            _deathTimer = 0f;
            if (_locomotion != null) _locomotion.Disable();
            if (Rig != null)
            {
                Rig.PlayDeath();
                Rig.SetHitboxesEnabled(false);
            }
        }

        private void TickDead(EnemyController self, float dt)
        {
            _deathTimer += dt;
            if (_director != null && _deathTimer > 6f) _director.Despawn(this);
        }

        // ------------------------------------------------------------------ combat

        private bool HasClearShot(Vector3 targetPoint)
        {
            Vector3 origin = ChestPosition;
            Vector3 direction = targetPoint - origin;
            float distance = direction.magnitude;
            if (distance < 0.3f) return true;
            RaycastHit hit;
            if (Physics.Raycast(origin, direction / distance, out hit, distance, GameLayers.BlockingMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            return true;
        }

        private void FireOneRound(Vector3 targetPoint)
        {
            if (_context == null || _context.Pools == null) return;

            Vector3 origin = Rig != null && Rig.MuzzlePoint != null ? Rig.MuzzlePoint.position : ChestPosition;
            Vector3 toTarget = (targetPoint - origin).normalized;

            // Accuracy degrades with distance and improves with difficulty.
            float accuracy = WfMath.Clamp01(Def.Accuracy + (Difficulty - 1) * 0.02f);
            float spreadDegrees = Mathf.Lerp(5.5f, 0.6f, accuracy);
            float distance = Vector3.Distance(origin, targetPoint);
            spreadDegrees *= 1f + distance / 90f;
            Vector3 direction = Quaternion.Euler(
                _random.Range(-spreadDegrees, spreadDegrees),
                _random.Range(-spreadDegrees, spreadDegrees),
                0f) * toTarget;

            GameObject instance = _context.Pools.Rent(Projectile.PoolId, origin, Quaternion.LookRotation(direction, Vector3.up));
            if (instance == null) return;
            Projectile projectile = instance.GetComponent<Projectile>();
            if (projectile == null) return;

            DamageRequest template = new DamageRequest();
            template.BaseDamage = Def.ScaledDamage(Difficulty);
            template.Source = DamageSource.Gunfire;
            template.FalloffStart = Def.AttackRange;
            template.FalloffEnd = Def.AttackRange * 1.8f;
            template.MinFalloffMultiplier = 0.6f;
            template.HeadshotMultiplier = 1.5f;
            template.ArmorPenetration = 0.1f;
            template.Zone = HitZone.Body;
            template.ShooterId = GetInstanceID();

            projectile.Launch(_context, _context.Pools, template, origin, direction, 48f, 3f, -3.5f, 1);

            if (_context.Audio != null) _context.Audio.PlayEnemyShot(Def, origin);
            if (_context.Vfx != null) _context.Vfx.SpawnMuzzleFlash(origin, Quaternion.LookRotation(direction, Vector3.up), 0.8f);
        }

        private void FaceTowards(Vector3 target, float dt, float turnSpeed)
        {
            Vector3 direction = target - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);
        }

        private Vector3 FindCoverPoint()
        {
            PlayerController player = _director != null ? _director.PlayerTarget : null;
            if (_coverPoints == null || _coverPoints.Count == 0 || player == null) return transform.position;

            Vector3 best = transform.position;
            float bestScore = float.MinValue;
            Vector3 awayFromPlayer = (transform.position - player.transform.position).normalized;

            for (int i = 0; i < _coverPoints.Count; i++)
            {
                Vector3 candidate = _coverPoints[i];
                float distance = Vector3.Distance(transform.position, candidate);
                if (distance > 16f) continue;
                Vector3 direction = (candidate - transform.position).normalized;
                float score = Vector3.Dot(direction, awayFromPlayer) * 2f - distance / 16f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        // ------------------------------------------------------------------ damage

        private void OnDamaged(DamageResult result, HitZone zone)
        {
            if (!_alive) return;
            if (Rig != null) Rig.PlayHitReaction(WfMath.Clamp01(result.Applied / 40f));
            if (_context != null && _context.Audio != null) _context.Audio.PlayEnemyHit(transform.position, zone == HitZone.Head);

            // Being shot at snaps the enemy out of patrol, and the squad gets told where from.
            if (_fsm != null && (_fsm.CurrentName == "Idle" || _fsm.CurrentName == "Patrol" || _fsm.CurrentName == "Investigate"))
            {
                _senses.ForceKnowTarget(_director != null && _director.PlayerTarget != null ? _director.PlayerTarget.transform.position : transform.position);
                _fsm.Change("Alert");
            }

            if (!_alertedAllies && _director != null)
            {
                _alertedAllies = true;
                _director.ReportContact(this);
            }
        }

        private void OnDied(DamageResult result)
        {
            if (!_alive) return;
            _alive = false;
            if (_locomotion != null) _locomotion.Stop();
            if (_director != null) _director.OnEnemyKilled(this);
            if (_mission != null) _mission.RegisterEnemyKilled(this, result.Zone == HitZone.Head);
            if (_context != null)
            {
                if (_context.Audio != null) _context.Audio.PlayEnemyDeath(transform.position, Def.Archetype);
                if (_context.Vfx != null) _context.Vfx.SpawnBlood(Position + Vector3.up * 1.1f);
            }
            DropLoot();
            _fsm.Change("Dead");
        }

        private void DropLoot()
        {
            if (_context == null || _mission == null || Def == null) return;
            if (_random.NextFloat() > Def.LootChance) return;
            _mission.SpawnEnemyLoot(Def, Position, (int)_random.NextUInt());
        }

        /// <summary>Hearing hook used by the director so squad mates react to gunfire.</summary>
        public bool HearNoise(Vector3 position, float loudness)
        {
            if (!_alive || _senses == null) return false;
            if (!_senses.OnHeardNoise(position, loudness)) return false;
            if (_fsm != null && (_fsm.CurrentName == "Idle" || _fsm.CurrentName == "Patrol")) _fsm.Change("Investigate");
            return true;
        }

        /// <summary>Squad alert propagation: an ally spotted the player nearby.</summary>
        public void AllySpottedPlayer(Vector3 playerPosition)
        {
            if (!_alive || _senses == null) return;
            if (_senses.HasTarget) return;
            _senses.ForceKnowTarget(playerPosition);
            if (_fsm != null && (_fsm.CurrentName == "Idle" || _fsm.CurrentName == "Patrol" || _fsm.CurrentName == "Investigate"))
            {
                _fsm.Change("Chase");
            }
        }

        /// <summary>Resets a pooled enemy for reuse without rebuilding geometry.</summary>
        public void ResetForReuse(GameContext context, EnemyDef def, int difficulty, int spawnIndex, string spawnTag,
            bool isObjectiveTarget, List<Vector3> patrolPoints, List<Vector3> coverPoints, Vector3 spawnPosition)
        {
            transform.position = spawnPosition;
            Initialize(context, def, difficulty, spawnIndex, spawnTag, isObjectiveTarget, patrolPoints, coverPoints);
            if (Rig != null)
            {
                Rig.ResetPose();
                Rig.SetHitboxesEnabled(true);
                Rig.SetVisible(true);
            }
            if (_agent != null)
            {
                _agent.enabled = true;
                if (_agent.isOnNavMesh) _agent.Warp(spawnPosition);
            }
        }
    }
}
