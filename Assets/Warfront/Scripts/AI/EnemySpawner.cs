using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Spawns and recycles enemies.
    ///
    /// Instances are pooled per archetype (the rig, controller and agent are reused, which keeps a long
    /// firefight allocation free) and the spawn plan is deterministic from the mission seed, so a
    /// mission always plays out its encounters the same way for a given run.
    /// </summary>
    public class EnemySpawner
    {
        private readonly GameContext _context;
        private readonly Transform _root;
        private readonly Dictionary<string, Stack<EnemyController>> _freeLists = new Dictionary<string, Stack<EnemyController>>(8);
        private readonly List<EnemyController> _live = new List<EnemyController>(32);
        private readonly WfRandom _random;

        private MissionDef _mission;
        private int _difficulty = 1;
        private int _budgetRemaining;
        private int _spawnIndex;
        private float _reinforceTimer;

        public EnemySpawner(GameContext context, Transform root, int seed)
        {
            _context = context;
            _root = root;
            _random = new WfRandom(seed);
        }

        public int LiveCount { get { return _live.Count; } }
        public int BudgetRemaining { get { return _budgetRemaining; } }
        public int TotalSpawned { get; private set; }
        public int TotalRecycled { get; private set; }

        public void Begin(MissionDef mission)
        {
            _mission = mission;
            _difficulty = mission != null ? mission.Difficulty : 1;
            float scaled = mission != null ? mission.EnemyBudget * ContentDatabase.DifficultyBudgetScale(_difficulty) : 8;
            _budgetRemaining = Mathf.RoundToInt(scaled);
            _spawnIndex = 0;
            _reinforceTimer = mission != null ? mission.EnemyReinforceInterval : 45f;
        }

        public bool CanSpawn
        {
            get
            {
                if (_mission == null) return false;
                if (_budgetRemaining <= 0) return false;
                return _live.Count < _mission.MaxEnemiesAlive;
            }
        }

        /// <summary>Chooses an archetype appropriate for the mission tier and spawn budget.</summary>
        public EnemyDef PickArchetype(string preferredId)
        {
            if (!string.IsNullOrEmpty(preferredId))
            {
                EnemyDef preferred = _context.Content.GetEnemy(preferredId);
                if (preferred != null && preferred.BudgetCost <= Mathf.Max(1, _budgetRemaining)) return preferred;
            }

            string[] roster = RosterFor(_difficulty);
            for (int attempt = 0; attempt < 6; attempt++)
            {
                EnemyDef candidate = _context.Content.GetEnemy(roster[_random.RangeInt(0, roster.Length)]);
                if (candidate == null) continue;
                if (candidate.BudgetCost > Mathf.Max(1, _budgetRemaining)) continue;
                return candidate;
            }
            return _context.Content.GetEnemy("enemy_soldier");
        }

        private static string[] RosterFor(int difficulty)
        {
            if (difficulty <= 1) return new[] { "enemy_soldier", "enemy_soldier", "enemy_raider" };
            if (difficulty == 2) return new[] { "enemy_soldier", "enemy_raider", "enemy_rifleman", "enemy_rifleman" };
            if (difficulty == 3) return new[] { "enemy_soldier", "enemy_rifleman", "enemy_raider", "enemy_heavy" };
            if (difficulty == 4) return new[] { "enemy_rifleman", "enemy_rifleman", "enemy_heavy", "enemy_raider", "enemy_elite" };
            return new[] { "enemy_rifleman", "enemy_heavy", "enemy_elite", "enemy_soldier" };
        }

        public EnemyController Spawn(EnemyDef def, Vector3 position, string spawnTag, bool isObjectiveTarget,
            List<Vector3> patrolRoute, List<Vector3> coverPoints)
        {
            if (def == null) return null;
            if (_budgetRemaining < def.BudgetCost) return null;

            EnemyController enemy = RentFromPool(def.Id);
            if (enemy == null)
            {
                enemy = CreateNew(def);
                if (enemy == null) return null;
            }

            _spawnIndex++;
            _budgetRemaining -= def.BudgetCost;
            TotalSpawned++;

            enemy.gameObject.SetActive(true);
            enemy.transform.position = position;
            enemy.ResetForReuse(_context, def, _difficulty, _spawnIndex, spawnTag, isObjectiveTarget, patrolRoute, coverPoints, position);

            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(position);

            _live.Add(enemy);
            if (_context.EnemyDirector != null) _context.EnemyDirector.Register(enemy);
            return enemy;
        }

        private EnemyController CreateNew(EnemyDef def)
        {
            GameObject go = new GameObject("Enemy_" + def.Id);
            go.transform.SetParent(_root, false);
            go.layer = GameLayers.Enemy;

            CharacterController controller = go.AddComponent<CharacterController>();
            controller.height = 1.7f * def.BodyScale;
            controller.radius = 0.32f * def.BodyScale;
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.4f;

            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.34f * def.BodyScale;
            agent.height = controller.height;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            go.AddComponent<Damageable>();
            return go.AddComponent<EnemyController>();
        }

        private EnemyController RentFromPool(string archetypeId)
        {
            Stack<EnemyController> list;
            if (!_freeLists.TryGetValue(archetypeId, out list) || list.Count == 0) return null;
            return list.Pop();
        }

        /// <summary>Returns an enemy to its archetype pool (used by the director on despawn/death).</summary>
        public void Recycle(EnemyController enemy)
        {
            if (enemy == null) return;

            int liveIndex = _live.IndexOf(enemy);
            if (liveIndex >= 0) _live.RemoveAt(liveIndex);
            if (_context.EnemyDirector != null) _context.EnemyDirector.Unregister(enemy);
            TotalRecycled++;

            enemy.gameObject.SetActive(false);
            enemy.transform.position = Vector3.zero;

            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();

            string id = enemy.Def != null ? enemy.Def.Id : "unknown";
            Stack<EnemyController> list;
            if (!_freeLists.TryGetValue(id, out list))
            {
                list = new Stack<EnemyController>(4);
                _freeLists[id] = list;
            }
            if (list.Count < 12) list.Push(enemy);
            else Object.Destroy(enemy.gameObject);
        }

        /// <summary>Attempts a reinforcement wave. Returns how many enemies entered the field.</summary>
        public int TryReinforce(List<Vector3> spawnPoints, List<Vector3> coverPoints, float deltaTime)
        {
            if (_mission == null || _mission.EnemyReinforceInterval <= 0f) return 0;
            _reinforceTimer -= deltaTime;
            if (_reinforceTimer > 0f) return 0;
            _reinforceTimer = _mission.EnemyReinforceInterval;

            if (!CanSpawn) return 0;

            int spawned = 0;
            int waves = Mathf.Clamp(Mathf.RoundToInt(_mission.MaxEnemiesAlive * 0.4f), 1, 3);
            for (int i = 0; i < waves && CanSpawn; i++)
            {
                Vector3 point = ChooseSpawnPoint(spawnPoints);
                EnemyDef def = PickArchetype(null);
                if (Spawn(def, point, "reinforcement", false, BuildRoute(point, coverPoints), coverPoints) != null) spawned++;
            }
            return spawned;
        }

        private Vector3 ChooseSpawnPoint(List<Vector3> spawnPoints)
        {
            if (spawnPoints == null || spawnPoints.Count == 0) return _root != null ? _root.position + Vector3.forward * 20f : Vector3.forward * 20f;

            // Never spawn in the player's face: pick a point that is far enough away.
            PlayerController player = _context.Player;
            Vector3 playerPosition = player != null ? player.transform.position : Vector3.zero;
            Vector3 best = spawnPoints[0];
            float bestScore = float.MinValue;

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                float distance = Vector3.Distance(spawnPoints[i], playerPosition);
                if (distance < 25f) continue;
                float score = distance + _random.Range(-6f, 6f);
                if (score <= bestScore) continue;
                bestScore = score;
                best = spawnPoints[i];
            }
            return best;
        }

        private List<Vector3> BuildRoute(Vector3 origin, List<Vector3> coverPoints)
        {
            var route = new List<Vector3>(3);
            route.Add(origin);
            if (coverPoints != null)
            {
                for (int i = 0; i < 2 && coverPoints.Count > 0; i++)
                {
                    Vector3 candidate = coverPoints[_random.RangeInt(0, coverPoints.Count)];
                    if (Vector3.Distance(candidate, origin) < 40f) route.Add(candidate);
                }
            }
            return route;
        }

        public void Clear()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                EnemyController enemy = _live[i];
                if (enemy != null) Recycle(enemy);
            }
            _live.Clear();
        }

        /// <summary>Kills every live enemy instantly (mission end / teardown).</summary>
        public void ForceDespawnAll()
        {
            Clear();
        }
    }
}
