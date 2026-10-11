using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Runs one mission from deploy to extraction.
    ///
    /// It owns the generated level, the objective content in the world, the enemy spawner/director
    /// bridge and the extraction contract, and it is the only place that talks to MissionModel. The
    /// MonoBehaviour side (player, AI, UI) never mutates mission state directly.
    /// </summary>
    public class MissionRuntime
    {
        private readonly GameContext _context;
        private readonly List<IInteractable> _interactables = new List<IInteractable>(48);
        private readonly List<SecureZone> _secureZones = new List<SecureZone>(2);
        private readonly List<ObjectiveMarker> _markers = new List<ObjectiveMarker>(8);
        private readonly Transform _root;
        private readonly LevelGenerator _generator = new LevelGenerator();
        private readonly List<Vector3> _coverPoints;
        private readonly List<Vector3> _spawnPoints;

        private MissionModel _model;
        private EnemySpawner _spawner;
        private ExtractionBeacon _beacon;
        private WfRandom _random;
        private float _statusTimer;
        private float _pruneTimer;
        private float _missionClock;
        private bool _reinforcementEnabled;
        private ExtractionStatus _lastStatus;

        public MissionRuntime(GameContext context, Transform root)
        {
            _context = context;
            _root = root;
            _coverPoints = new List<Vector3>(64);
            _spawnPoints = new List<Vector3>(64);
        }

        public MissionModel Model { get { return _model; } }
        public MissionDef Def { get { return _model != null ? _model.Def : null; } }
        public LevelData Level { get; private set; }
        public EnemySpawner Spawner { get { return _spawner; } }
        public ExtractionBeacon Extraction { get { return _beacon; } }
        public List<IInteractable> Interactables { get { return _interactables; } }
        public ExtractionStatus Status { get { return _lastStatus; } }
        public bool IsRunning { get { return _model != null && _model.State == MissionState.Active; } }
        public bool IsFinished { get { return _model != null && (_model.State == MissionState.Success || _model.State == MissionState.Failed); } }
        public float MissionClock { get { return _missionClock; } }

        /// <summary>Builds the level and objective content. Called before the player exists.</summary>
        public void Build(MissionDef def, int runId)
        {
            _model = new MissionModel(def, _context.Content, runId);
            _random = new WfRandom(WfMath.Combine(def.Seed, runId));
            _model.ObjectiveProgressed += progress => GameEvents.RaiseObjectiveUpdated(progress);
            _model.ObjectiveCompleted += progress =>
            {
                GameEvents.RaiseObjectiveCompleted(progress);
                if (_context.Hud != null) _context.Hud.OnObjectiveCompleted(progress);
                if (_context.Audio != null) _context.Audio.PlayUi(AudioCue.ObjectiveComplete);
                if (_context.Haptics != null) _context.Haptics.Objective();
            };

            Level = _generator.Generate(def, _context.Assets, _root);
            _coverPoints.Clear();
            _coverPoints.AddRange(Level.CoverPoints);
            _spawnPoints.Clear();
            _spawnPoints.AddRange(Level.EnemySpawnPoints);

            SpawnContent(def);
            SpawnObjectiveTargets(def);
        }

        /// <summary>Starts the mission clock, spawns the first wave and switches on reinforcements.</summary>
        public void Begin()
        {
            _model.Begin();
            _missionClock = 0f;
            _reinforcementEnabled = true;

            if (_spawner == null) _spawner = new EnemySpawner(_context, _root, _model.RunId);
            _spawner.Begin(_model.Def);

            SpawnMissionTargets();

            int initial = Mathf.Clamp(Mathf.RoundToInt(_model.Def.MaxEnemiesAlive * 0.6f), 2, 6);
            for (int i = 0; i < initial; i++) SpawnRandomPatrol(false);

            if (_context.Audio != null) _context.Audio.PlayMissionStinger(true, false);
            GameEvents.RaiseMissionStarted(_model.Def);
            GameEvents.RaiseMissionStateChanged(_model.State);
        }

        public void Tick(float deltaTime)
        {
            if (_model == null) return;

            PlayerController player = _context.Player;
            if (player == null) return;

            if (_model.State == MissionState.Active)
            {
                _missionClock += deltaTime;

                Vector3 playerPosition = player.transform.position;
                Vector3 zonePosition = _beacon != null ? _beacon.Center : Vector3.zero;
                float zoneRadius = _beacon != null ? _beacon.Radius : 1f;
                bool inZone = _beacon != null && _beacon.IsInside(playerPosition);

                _model.Tick(deltaTime, inZone, playerPosition, zonePosition, zoneRadius);

                TickSecureZones(deltaTime, playerPosition);

                if (_reinforcementEnabled && _spawner != null)
                {
                    _spawner.TryReinforce(_spawnPoints, _coverPoints, deltaTime);
                }

                if (inZone && _context.Haptics != null && _model.Extraction.State == ExtractionState.Countdown)
                {
                    _context.Haptics.ExtractionPulse();
                }

                if (_model.State != MissionState.Active) OnMissionResolved();
            }

            if (_context.EnemyDirector != null) _context.EnemyDirector.Tick(deltaTime);

            PublishStatus(deltaTime);
            PruneInteractables(deltaTime);
        }

        private void TickSecureZones(float deltaTime, Vector3 playerPosition)
        {
            for (int i = 0; i < _secureZones.Count; i++)
            {
                SecureZone zone = _secureZones[i];
                if (zone == null || zone.Completed) continue;

                bool playerInside = zone.IsPlayerInside(playerPosition);
                bool enemiesInside = _context.EnemyDirector != null && _context.EnemyDirector.AnyAliveWithin(zone.transform.position, zone.Radius * 1.25f);
                if (zone.Tick(deltaTime, playerInside, enemiesInside))
                {
                    ReportObjective(ObjectiveType.SecureLocation, zone.Tag, 1);
                    zone.gameObject.SetActive(false);
                }
            }
        }

        private void OnMissionResolved()
        {
            GameEvents.RaiseMissionStateChanged(_model.State);
            if (_context.Flow != null) _context.Flow.OnMissionResolved(_model);
        }

        private void PublishStatus(float deltaTime)
        {
            _statusTimer -= deltaTime;
            if (_statusTimer > 0f) return;
            _statusTimer = 0.1f;

            PlayerController player = _context.Player;
            if (player == null || _beacon == null) return;

            _lastStatus = _model.Extraction.GetStatus(player.transform.position, _beacon.Center, _beacon.Radius);
            _beacon.SetState(_lastStatus.State);
            GameEvents.RaiseExtractionChanged(_lastStatus);

            // Markers switch off once their objective is done so the world reads cleanly.
            for (int i = 0; i < _markers.Count; i++)
            {
                ObjectiveMarker marker = _markers[i];
                if (marker == null) continue;
                ObjectiveProgress progress = FindObjectiveByTag(marker.Tag);
                bool done = progress != null && progress.Completed;
                if (marker.IsActive == !done) continue;
                marker.SetActive(!done);
            }
        }

        private ObjectiveProgress FindObjectiveByTag(string tag)
        {
            if (_model == null || string.IsNullOrEmpty(tag)) return null;
            for (int i = 0; i < _model.Objectives.Count; i++)
            {
                ObjectiveProgress progress = _model.Objectives[i];
                if (progress.Def.TargetTag == tag) return progress;
            }
            return null;
        }

        private void PruneInteractables(float deltaTime)
        {
            _pruneTimer -= deltaTime;
            if (_pruneTimer > 0f) return;
            _pruneTimer = 2f;

            for (int i = _interactables.Count - 1; i >= 0; i--)
            {
                Object unity = _interactables[i] as Object;
                if (unity == null) _interactables.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ content

        private void SpawnContent(MissionDef def)
        {
            // Extraction pad first: everything else avoids it.
            GameObject extractionObject = new GameObject("Extraction");
            extractionObject.transform.SetParent(_root, false);
            extractionObject.transform.position = Level.ExtractionPosition;
            _beacon = extractionObject.AddComponent<ExtractionBeacon>();
            _beacon.Configure(Level.ExtractionRadius);
            _beacon.BuildVisuals(_context.Assets);

            // Objective items.
            for (int i = 0; i < Level.IntelPositions.Count; i++)
            {
                CreateObjectiveContainer(Level.IntelPositions[i], ObjectiveType.CollectIntel, "intel", "intel_data_slate", "Intel cache", "loot_secure");
            }
            for (int i = 0; i < Level.SupplyPositions.Count; i++)
            {
                CreateObjectiveContainer(Level.SupplyPositions[i], ObjectiveType.RecoverSupplies, "supply", "obj_supply_crate", "Supply cache", "loot_supply_crate");
            }
            for (int i = 0; i < Level.DevicePositions.Count; i++)
            {
                CreateObjectiveContainer(Level.DevicePositions[i], ObjectiveType.ActivateDevice, "device", "obj_comms_beacon", "Comms relay", "loot_secure");
            }

            // Secure zones.
            foreach (KeyValuePair<string, SecureZoneInfo> pair in Level.SecureZones)
            {
                GameObject zoneObject = new GameObject("SecureZone_" + pair.Key);
                zoneObject.transform.SetParent(_root, false);
                zoneObject.transform.position = pair.Value.Center;
                SecureZone zone = zoneObject.AddComponent<SecureZone>();
                zone.Configure(pair.Key, pair.Value.Radius, Mathf.Max(10f, def.ExtractionHoldSeconds * 0.85f));
                zone.BuildVisuals(_context.Assets, new Color(0.85f, 0.75f, 0.3f));
                _secureZones.Add(zone);
                AddMarker(pair.Value.Center + Vector3.up * 0.2f, pair.Key, new Color(0.9f, 0.8f, 0.3f));
            }

            // Generic loot containers spread across the map.
            int containers = Mathf.Max(2, def.LootContainers);
            for (int i = 0; i < containers; i++)
            {
                Vector3 position = Level.FindWalkableNear(RandomPoint(12f), 8f);
                CreateLootContainer(position, "loot_" + i, def.LootTableId, WfMath.Combine(def.Seed, i * 7919), 0.05f + def.Difficulty * 0.03f, 0);
            }
        }

        private Vector3 RandomPoint(float margin)
        {
            float half = Level.MapSize * 0.5f - margin;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float x = _random.Range(-half, half);
                float z = _random.Range(-half, half);
                int cellX;
                int cellY;
                Level.Grid.WorldToCell(new Vector3(x, 0f, z), out cellX, out cellY);
                if (!Level.Grid.IsWalkable(cellX, cellY)) continue;
                return new Vector3(x, 0f, z);
            }
            return Level.ExtractionPosition + new Vector3(6f, 0f, 6f);
        }

        private void CreateObjectiveContainer(Vector3 position, ObjectiveType type, string tag, string itemId, string label, string tableId)
        {
            GameObject go = new GameObject("Objective_" + label + "_" + tag);
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            go.layer = GameLayers.Interactable;

            LootContainer container = go.AddComponent<LootContainer>();
            container.Initialize(_context, go.name, tableId, WfMath.Hash(go.name), 0.35f, 1);
            container.ConfigureObjective(type, tag, itemId);
            container.BuildVisuals(_context.Assets, _context.Assets != null ? _context.Assets.ObjectiveMarker : null);
            _interactables.Add(container);
            AddMarker(position + Vector3.up * 0.2f, tag, new Color(0.95f, 0.8f, 0.25f));
        }

        private void CreateLootContainer(Vector3 position, string id, string tableId, int seed, float luck, int rollBonus)
        {
            GameObject go = new GameObject("Loot_" + id);
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            go.layer = GameLayers.Interactable;

            LootContainer container = go.AddComponent<LootContainer>();
            container.Initialize(_context, id, tableId, seed, luck, rollBonus);
            container.BuildVisuals(_context.Assets, _context.Assets != null ? _context.Assets.Beacon : null);
            _interactables.Add(container);
        }

        private void AddMarker(Vector3 position, string tag, Color color)
        {
            GameObject markerObject = new GameObject("Marker_" + tag);
            markerObject.transform.SetParent(_root, false);
            markerObject.transform.position = position;
            ObjectiveMarker marker = markerObject.AddComponent<ObjectiveMarker>();
            marker.Tag = tag;
            marker.BuildVisuals(_context.Assets, color, tag == "extraction");
            _markers.Add(marker);
        }

        private void SpawnObjectiveTargets(MissionDef def)
        {
            int index = 0;
            for (int i = 0; i < def.Objectives.Count; i++)
            {
                ObjectiveDef objective = def.Objectives[i];
                if (objective.Type != ObjectiveType.EliminateTarget) continue;

                Vector3 position = index < Level.TargetSpawnPoints.Count
                    ? Level.TargetSpawnPoints[index]
                    : Level.ExtractionPosition + new Vector3(8f, 0f, 8f);
                index++;

                _pendingTargets.Add(new PendingTarget
                {
                    Position = position,
                    Tag = string.IsNullOrEmpty(objective.TargetTag) ? "target_elite" : objective.TargetTag
                });
                AddMarker(position + Vector3.up * 0.2f, objective.TargetTag, new Color(0.9f, 0.3f, 0.25f));
            }
        }

        private struct PendingTarget
        {
            public Vector3 Position;
            public string Tag;
        }

        private readonly List<PendingTarget> _pendingTargets = new List<PendingTarget>(2);

        /// <summary>Spawns the mission's named targets once the player exists (called from Begin).</summary>
        private void SpawnTargets()
        {
            for (int i = 0; i < _pendingTargets.Count; i++)
            {
                PendingTarget target = _pendingTargets[i];
                EnemyDef def = _context.Content.GetEnemy("enemy_elite");
                _spawner.Spawn(def, target.Position, target.Tag, true, BuildRoute(target.Position), _coverPoints);
            }
        }

        private List<Vector3> BuildRoute(Vector3 origin)
        {
            var route = new List<Vector3>(3);
            route.Add(origin);
            if (_coverPoints.Count > 0)
            {
                route.Add(_coverPoints[_random.RangeInt(0, _coverPoints.Count)]);
                route.Add(_coverPoints[_random.RangeInt(0, _coverPoints.Count)]);
            }
            return route;
        }

        private void SpawnRandomPatrol(bool elite)
        {
            if (_spawner == null || !_spawner.CanSpawn) return;

            Vector3 position = _spawnPoints.Count > 0
                ? _spawnPoints[_random.RangeInt(0, _spawnPoints.Count)]
                : Level.ExtractionPosition;

            if (Vector3.Distance(position, Level.PlayerSpawn) < 22f) return;

            EnemyDef def = _spawner.PickArchetype(elite ? "enemy_elite" : null);
            List<Vector3> route = Level.PatrolRoutes.Count > 0
                ? Level.PatrolRoutes[_random.RangeInt(0, Level.PatrolRoutes.Count)]
                : BuildRoute(position);

            _spawner.Spawn(def, position, "patrol", false, route, _coverPoints);
        }

        // ------------------------------------------------------------------ reports

        public void ReportObjective(ObjectiveType type, string tag, int amount)
        {
            if (_model == null) return;
            _model.ReportProgress(type, tag, amount);
        }

        public void ReportLoot(LootDrop drop)
        {
            if (_model != null) _model.RegisterLoot(drop);
        }

        public void RegisterEnemyKilled(EnemyController enemy, bool headshot)
        {
            if (_model == null || enemy == null) return;
            // Kill XP is paid out with the rest of the reward bundle at extraction, so a failed run
            // cannot be farmed for XP by repeatedly killing respawning enemies.
            _model.RegisterKill(headshot, enemy.IsObjectiveTarget);
            GameEvents.RaiseEnemyKilled(enemy, headshot);
        }

        /// <summary>Called by Begin so mission targets appear with the first wave.</summary>
        public void SpawnMissionTargets()
        {
            if (_spawner != null) SpawnTargets();
        }

        public void SpawnEnemyLoot(EnemyDef def, Vector3 position, int seed)
        {
            if (def == null) return;
            LootRoller roller = new LootRoller(_context.Content, WfMath.Combine(seed, WfMath.Hash(def.Id)));
            List<LootDrop> drops = new List<LootDrop>(4);
            roller.Roll(def.LootTableId, Mathf.Clamp01((def.ThreatLevel - 1) * 0.06f + _model.Def.Difficulty * 0.03f), 0, drops);

            for (int i = 0; i < drops.Count; i++)
            {
                Vector3 scatter = position + new Vector3(Random.Range(-1.6f, 1.6f), 0.35f, Random.Range(-1.6f, 1.6f));
                SpawnPickup(drops[i].ItemId, drops[i].Quantity, drops[i].Rarity, drops[i].UpgradeLevel, scatter);
            }
        }

        public WorldItemPickup SpawnPickup(string itemId, int quantity, Rarity rarity, int upgradeLevel, Vector3 position)
        {
            ItemDef item = _context.Content.GetItem(itemId);
            if (item == null || quantity <= 0) return null;

            GameObject go = new GameObject("Pickup_" + itemId);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(position.x, 0.35f, position.z);
            go.layer = GameLayers.Interactable;

            WorldItemPickup pickup = go.AddComponent<WorldItemPickup>();
            pickup.Initialize(_context, new ItemStack(itemId, quantity, rarity, upgradeLevel), item.Value * quantity);
            pickup.BuildVisuals(_context.Assets);
            _interactables.Add(pickup);
            return pickup;
        }

        /// <summary>Player failed the mission (death, out of time, abandoned).</summary>
        public void Fail(FailureReason reason)
        {
            if (_model == null) return;
            _model.Fail(reason);
            OnMissionResolved();
        }

        /// <summary>Forces a successful extraction (used by the tutorial and tests).</summary>
        public bool ForceExtract()
        {
            if (_model == null) return false;
            for (int i = 0; i < _model.Objectives.Count; i++)
            {
                ObjectiveProgress progress = _model.Objectives[i];
                if (progress.Required && !progress.Completed) _model.CompleteObjective(progress.Def.Id);
            }
            return _model.Succeed();
        }

        public void Teardown()
        {
            _reinforcementEnabled = false;
            if (_spawner != null) _spawner.Clear();
            if (_context.EnemyDirector != null) _context.EnemyDirector.Clear();
            if (_context.Pools != null) _context.Pools.ReturnAll();

            _interactables.Clear();
            _secureZones.Clear();
            _markers.Clear();
            _pendingTargets.Clear();

            if (Level != null && Level.Root != null) Object.Destroy(Level.Root);
            Level = null;
            _beacon = null;
            _spawner = null;
            _model = null;
            MeshFactory.ClearCache();
        }

        public Vector3 PlayerSpawn
        {
            get { return Level != null ? Level.PlayerSpawn : Vector3.zero; }
        }

        public Vector3 ExtractionPosition
        {
            get { return _beacon != null ? _beacon.Center : Vector3.zero; }
        }

        public int AliveEnemyCount
        {
            get { return _context.EnemyDirector != null ? _context.EnemyDirector.ActiveCount : 0; }
        }

        /// <summary>Convenience for the HUD: distance to the active objective.</summary>
        public bool TryGetObjectiveTarget(out Vector3 position, out string label)
        {
            position = Vector3.zero;
            label = null;
            if (_model == null) return false;

            if (_model.AllObjectivesCompleteExceptExtraction)
            {
                position = ExtractionPosition;
                label = "EXTRACTION";
                return true;
            }

            for (int i = 0; i < _model.Objectives.Count; i++)
            {
                ObjectiveProgress progress = _model.Objectives[i];
                if (!progress.Required || progress.Completed) continue;
                if (progress.Def.Type == ObjectiveType.ReachExtraction) continue;

                position = ObjectiveWorldPosition(progress.Def);
                label = progress.Def.Title;
                return true;
            }
            return false;
        }

        private Vector3 ObjectiveWorldPosition(ObjectiveDef def)
        {
            if (def == null) return ExtractionPosition;
            switch (def.Type)
            {
                case ObjectiveType.CollectIntel:
                    return Level != null && Level.IntelPositions.Count > 0 ? Level.IntelPositions[0] : ExtractionPosition;
                case ObjectiveType.RecoverSupplies:
                    return Level != null && Level.SupplyPositions.Count > 0 ? Level.SupplyPositions[0] : ExtractionPosition;
                case ObjectiveType.ActivateDevice:
                    return Level != null && Level.DevicePositions.Count > 0 ? Level.DevicePositions[0] : ExtractionPosition;
                case ObjectiveType.EliminateTarget:
                    return _pendingTargets.Count > 0 ? _pendingTargets[0].Position : ExtractionPosition;
                case ObjectiveType.SecureLocation:
                    foreach (KeyValuePair<string, SecureZoneInfo> pair in Level.SecureZones) return pair.Value.Center;
                    return ExtractionPosition;
                default:
                    return ExtractionPosition;
            }
        }
    }
}
