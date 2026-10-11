using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Central AI manager.
    ///
    /// Owns the live enemy list, distributes noise and squad alerts, throttles distant enemies so the
    /// CPU budget stays flat, and hands despawns back to the spawner. Enemies never search the scene
    /// for each other - the director is the only thing that knows all of them.
    /// </summary>
    public class EnemyDirector
    {
        private const float AlertPropagationRadius = 26f;
        private const float AlertPropagationDelay = 0.45f;
        private const float LodDistanceNear = 45f;
        private const float LodDistanceMid = 80f;
        private const float DespawnDistance = 140f;

        private readonly List<EnemyController> _enemies = new List<EnemyController>(32);
        private readonly List<float> _alertTimers = new List<float>(32);
        private readonly GameContext _context;

        private EnemySpawner _spawner;

        public EnemyDirector(GameContext context)
        {
            _context = context;
            GameEvents.NoiseEmitted += OnNoise;
        }

        public PlayerController PlayerTarget { get; set; }
        public int ActiveCount { get { return _enemies.Count; } }

        /// <summary>AI tick stride for the last registered enemy (1 = every frame).</summary>
        public int TickStride { get; private set; }

        public void SetSpawner(EnemySpawner spawner)
        {
            _spawner = spawner;
        }

        public void Register(EnemyController enemy)
        {
            if (enemy == null || _enemies.Contains(enemy)) return;
            _enemies.Add(enemy);
            _alertTimers.Add(0f);
        }

        public void Unregister(EnemyController enemy)
        {
            int index = _enemies.IndexOf(enemy);
            if (index < 0) return;
            _enemies.RemoveAt(index);
            _alertTimers.RemoveAt(index);
        }

        /// <summary>Called each frame by the mission to keep proximity data and LOD state fresh.</summary>
        public void Tick(float deltaTime)
        {
            if (PlayerTarget == null) return;

            Vector3 playerPosition = PlayerTarget.transform.position;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyController enemy = _enemies[i];
                if (enemy == null) continue;
                float distance = Vector3.Distance(enemy.Position, playerPosition);
                if (distance < bestDistance) bestDistance = distance;

                // Delayed squad alerting: contacts ripple outward instead of instantly.
                if (_alertTimers[i] > 0f)
                {
                    _alertTimers[i] -= deltaTime;
                    if (_alertTimers[i] <= 0f) enemy.AllySpottedPlayer(playerPosition);
                }

                if (distance > DespawnDistance && enemy.IsAlive && _spawner != null)
                {
                    // Long range enemies recycle into the spawn budget instead of costing CPU.
                    _spawner.Recycle(enemy);
                }
            }

            // The stride is shared by all enemies for the frame; near enemies always run at full rate.
            TickStride = bestDistance < LodDistanceNear ? 1 : bestDistance < LodDistanceMid ? 2 : 3;
        }

        /// <summary>An enemy has spotted the player: alert the nearby squad after a short delay.</summary>
        public void ReportContact(EnemyController reporter)
        {
            if (reporter == null) return;
            Vector3 position = reporter.Position;

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyController enemy = _enemies[i];
                if (enemy == null || ReferenceEquals(enemy, reporter)) continue;
                if (Vector3.Distance(enemy.Position, position) > AlertPropagationRadius) continue;
                if (_alertTimers[i] <= 0f) _alertTimers[i] = AlertPropagationDelay;
            }
        }

        public void OnEnemyKilled(EnemyController enemy)
        {
            Unregister(enemy);
        }

        public void Despawn(EnemyController enemy)
        {
            Unregister(enemy);
            if (_spawner != null) _spawner.Recycle(enemy);
        }

        private void OnNoise(Vector3 position)
        {
            // Noise is a squad wide event: every enemy within hearing range investigates.
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyController enemy = _enemies[i];
                if (enemy == null) continue;
                enemy.HearNoise(position, 1f);
            }
        }

        public void Clear()
        {
            GameEvents.NoiseEmitted -= OnNoise;
            _enemies.Clear();
            _alertTimers.Clear();
            TickStride = 1;
        }

        /// <summary>How many living enemies are inside a radius (used by secure-zone objectives).</summary>
        public bool AnyAliveWithin(Vector3 center, float radius)
        {
            float sqrRadius = radius * radius;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyController enemy = _enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                if ((enemy.Position - center).sqrMagnitude <= sqrRadius) return true;
            }
            return false;
        }

        /// <summary>Nearest living enemy to a point - used by the HUD threat indicator.</summary>
        public EnemyController NearestEnemy(Vector3 position, float maxDistance)
        {
            EnemyController best = null;
            float bestDistance = maxDistance;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyController enemy = _enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                float distance = Vector3.Distance(enemy.Position, position);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = enemy;
            }
            return best;
        }
    }
}
