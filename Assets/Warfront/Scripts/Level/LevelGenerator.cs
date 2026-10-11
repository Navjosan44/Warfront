using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Builds a mission map from a seed.
    ///
    /// The generator lays out a streets-and-porches industrial compound out of procedural geometry,
    /// then derives everything gameplay needs: walkability grid, cover points, patrol lanes, enemy spawn
    /// points, objective sites and the extraction pad. The same seed always produces the same map, which
    /// makes bug reports and QA reproducible.
    /// </summary>
    public class LevelGenerator
    {
        private const float GridCell = 1.5f;
        private const float BlockSize = 26f;

        private WfRandom _random;
        private GameAssets _assets;
        private MissionDef _mission;
        private LevelData _level;
        private Transform _root;
        private readonly List<Vector3> _structureInteriors = new List<Vector3>(32);
        private readonly List<Vector3> _streetPoints = new List<Vector3>(128);

        public LevelData Generate(MissionDef mission, GameAssets assets, Transform parent)
        {
            _mission = mission;
            _assets = assets;
            _random = new WfRandom(mission != null && mission.Seed != 0 ? mission.Seed : 1337);

            float mapSize = mission != null ? Mathf.Max(60f, mission.MapSize) : 120f;

            GameObject root = new GameObject("Level_" + (mission != null ? mission.Id : "unknown"));
            root.transform.SetParent(parent, false);
            _root = root.transform;

            _level = new LevelData();
            _level.Root = root;
            _level.MapSize = mapSize;
            _level.Bounds = new Bounds(Vector3.zero, new Vector3(mapSize, 32f, mapSize));
            _level.Grid = new NavigationGrid(
                Mathf.CeilToInt(mapSize / GridCell),
                Mathf.CeilToInt(mapSize / GridCell),
                GridCell,
                new Vector3(-mapSize * 0.5f, 0f, -mapSize * 0.5f));

            BuildGround(mapSize);
            BuildPerimeter(mapSize);
            BuildBlocks(mapSize);
            BuildStreetProps(mapSize);
            BuildObjectiveSites();
            BuildExtraction(mapSize);

            _level.PlayerSpawn = _level.FindWalkableNear(ChoosePlayerSpawn(mapSize), 12f);
            if (_level.PlayerSpawn == Vector3.zero) _level.PlayerSpawn = new Vector3(0f, 0.1f, -mapSize * 0.35f);

            BuildPatrolRoutes();
            BuildEnemySpawnPoints(mapSize);

            _level.NavMeshBuilt = LevelNavMeshBuilder.Build(_level, _root);
            return _level;
        }

        // ------------------------------------------------------------------ ground

        private void BuildGround(float mapSize)
        {
            GameObject ground = PrimitiveFactory.CreateBox("Ground", _root, new Vector3(0f, -0.5f, 0f),
                new Vector3(mapSize, 1f, mapSize), _assets != null ? _assets.Ground : null, true);
            ground.isStatic = true;

            // Two crossing streets give the map readable lanes for patrols and firefights.
            PrimitiveFactory.CreateBox("StreetNS", _root, new Vector3(0f, 0.02f, 0f),
                new Vector3(14f, 0.06f, mapSize * 0.92f), _assets != null ? _assets.Asphalt : null, false);
            PrimitiveFactory.CreateBox("StreetEW", _root, new Vector3(0f, 0.02f, 0f),
                new Vector3(mapSize * 0.92f, 0.06f, 14f), _assets != null ? _assets.Asphalt : null, false);

            // Cratered dirt patches break up the flat ground visually without costing colliders.
            for (int i = 0; i < 10; i++)
            {
                float x = _random.Range(-mapSize * 0.45f, mapSize * 0.45f);
                float z = _random.Range(-mapSize * 0.45f, mapSize * 0.45f);
                PrimitiveFactory.CreateBox("DirtPatch", _root, new Vector3(x, 0.015f, z),
                    new Vector3(_random.Range(4f, 9f), 0.04f, _random.Range(4f, 9f)),
                    _assets != null ? _assets.Sand : null, false, Quaternion.Euler(0f, _random.Range(0f, 90f), 0f), GameLayers.Environment);
            }
        }

        private void BuildPerimeter(float mapSize)
        {
            float half = mapSize * 0.5f;
            float thickness = 1.6f;
            float height = 5.5f;
            AddWall(new Vector3(0f, 0f, half), new Vector3(mapSize, height, thickness));
            AddWall(new Vector3(0f, 0f, -half), new Vector3(mapSize, height, thickness));
            AddWall(new Vector3(half, 0f, 0f), new Vector3(thickness, height, mapSize));
            AddWall(new Vector3(-half, 0f, 0f), new Vector3(thickness, height, mapSize));
        }

        private void AddWall(Vector3 position, Vector3 size)
        {
            GameObject wall = PrimitiveFactory.CreateBox("PerimeterWall", _root, position + Vector3.up * (size.y * 0.5f),
                size, _assets != null ? _assets.Concrete : null, true);
            wall.isStatic = true;
            _level.Grid.FillRect(position.x, position.z, size.x, size.z, NavigationGrid.Unwalkable, 0.2f);
        }

        // -------------------------------------------------------------- structures

        private void BuildBlocks(float mapSize)
        {
            int blocks = Mathf.Max(2, Mathf.FloorToInt(mapSize / BlockSize));
            float half = mapSize * 0.5f;
            float origin = -half + BlockSize * 0.5f;
            float spawnClearRadius = 22f;

            for (int bx = 0; bx < blocks; bx++)
            {
                for (int bz = 0; bz < blocks; bz++)
                {
                    float centerX = origin + bx * BlockSize;
                    float centerZ = origin + bz * BlockSize;

                    // Keep the map centre (spawn street) and a far corner (extraction) clear.
                    if (Mathf.Abs(centerX) < 16f || Mathf.Abs(centerZ) < 16f) continue;
                    if (new Vector2(centerX, centerZ).magnitude < spawnClearRadius) continue;
                    if (new Vector2(centerX - half * 0.55f, centerZ - half * 0.55f).magnitude < 18f) continue;

                    float roll = _random.NextFloat();
                    if (roll < 0.22f)
                    {
                        _streetPoints.Add(new Vector3(centerX, 0f, centerZ));
                        continue;   // open yard
                    }

                    if (roll < 0.62f)
                    {
                        AddPorchStructure(centerX, centerZ);
                    }
                    else if (roll < 0.86f)
                    {
                        AddSolidBlock(centerX, centerZ);
                    }
                    else
                    {
                        AddWatchtower(centerX, centerZ);
                    }
                }
            }
        }

        /// <summary>Open fronted concrete shelter: walkable inside, solid on three sides.</summary>
        private void AddPorchStructure(float centerX, float centerZ)
        {
            float width = _random.Range(8f, 12f);
            float depth = _random.Range(8f, 11f);
            float height = 4.2f;
            float wallThickness = 0.6f;
            float yaw = _random.NextFloat() < 0.5f ? 0f : 90f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

            Transform groupRoot = NewGroup("Porch", new Vector3(centerX, 0f, centerZ), yaw);

            // Back wall
            PrimitiveFactory.CreateBox("Back", groupRoot, new Vector3(0f, height * 0.5f, depth * 0.5f),
                new Vector3(width, height, wallThickness), _assets != null ? _assets.Concrete : null, true);
            // Side walls
            PrimitiveFactory.CreateBox("SideL", groupRoot, new Vector3(-width * 0.5f, height * 0.5f, 0f),
                new Vector3(wallThickness, height, depth), _assets != null ? _assets.Concrete : null, true);
            PrimitiveFactory.CreateBox("SideR", groupRoot, new Vector3(width * 0.5f, height * 0.5f, 0f),
                new Vector3(wallThickness, height, depth), _assets != null ? _assets.Concrete : null, true);
            // Roof and columns keep the silhouette readable while leaving the front open.
            PrimitiveFactory.CreateBox("Roof", groupRoot, new Vector3(0f, height, 0f),
                new Vector3(width + 1.2f, 0.6f, depth + 1.2f), _assets != null ? _assets.Metal : null, true);
            PrimitiveFactory.CreateBox("ColumnA", groupRoot, new Vector3(-width * 0.5f + 0.4f, height * 0.5f, -depth * 0.5f),
                new Vector3(0.5f, height, 0.5f), _assets != null ? _assets.Concrete : null, true);
            PrimitiveFactory.CreateBox("ColumnB", groupRoot, new Vector3(width * 0.5f - 0.4f, height * 0.5f, -depth * 0.5f),
                new Vector3(0.5f, height, 0.5f), _assets != null ? _assets.Concrete : null, true);
            PrimitiveFactory.CreateBox("Hazard", groupRoot, new Vector3(0f, 0.05f, -depth * 0.5f - 0.2f),
                new Vector3(width, 0.1f, 1.4f), _assets != null ? _assets.Rust : null, false);

            MarkFootprint(centerX, centerZ, width, depth, yaw, false);
            _structureInteriors.Add(new Vector3(centerX, 0f, centerZ) + rotation * new Vector3(0f, 0f, -depth * 0.15f));

            // Interior cover and a cover point at each front corner.
            PrimitiveFactory.CreateBox("InteriorCover", groupRoot, new Vector3(_random.Range(-2f, 2f), 0.5f, _random.Range(-1f, 1f)),
                new Vector3(2.4f, 1f, 1.1f), _assets != null ? _assets.Cover : null, true);
            _level.CoverPoints.Add(new Vector3(centerX, 0f, centerZ) + rotation * new Vector3(-width * 0.5f + 1.2f, 0f, -depth * 0.5f + 1.2f));
            _level.CoverPoints.Add(new Vector3(centerX, 0f, centerZ) + rotation * new Vector3(width * 0.5f - 1.2f, 0f, -depth * 0.5f + 1.2f));
            _level.EnemySpawnPoints.Add(new Vector3(centerX, 0f, centerZ) + rotation * new Vector3(0f, 0f, depth * 0.25f));
        }

        /// <summary>Solid concrete block - pure obstacle and line of sight breaker.</summary>
        private void AddSolidBlock(float centerX, float centerZ)
        {
            float width = _random.Range(9f, 15f);
            float depth = _random.Range(9f, 14f);
            float height = _random.Range(4.5f, 7.5f);
            float yaw = _random.Range(0f, 90f);

            Transform groupRoot = NewGroup("Block", new Vector3(centerX, 0f, centerZ), yaw);
            GameObject body = PrimitiveFactory.CreateBox("Body", groupRoot, new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width, height, depth), _assets != null ? _assets.Concrete : null, true, Quaternion.identity, GameLayers.Environment);
            body.isStatic = true;
            PrimitiveFactory.CreateBox("Trim", groupRoot, new Vector3(0f, height + 0.15f, 0f),
                new Vector3(width + 0.8f, 0.3f, depth + 0.8f), _assets != null ? _assets.Rust : null, false);

            MarkFootprint(centerX, centerZ, width, depth, yaw, true);

            // Sandbag lines at the corners give the AI real cover to use around every block.
            AddSandbagLine(centerX + width * 0.5f + 2.4f, centerZ - depth * 0.25f, yaw + 90f);
            AddSandbagLine(centerX - width * 0.5f - 2.4f, centerZ + depth * 0.25f, yaw + 90f);
        }

        private void AddWatchtower(float centerX, float centerZ)
        {
            float height = _random.Range(6f, 8.5f);
            Transform groupRoot = NewGroup("Watchtower", new Vector3(centerX, 0f, centerZ), _random.Range(0f, 360f));

            float spread = 2.4f;
            float legSize = 0.5f;
            Vector3[] offsets =
            {
                new Vector3(-spread, 0f, -spread),
                new Vector3(spread, 0f, -spread),
                new Vector3(-spread, 0f, spread),
                new Vector3(spread, 0f, spread)
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                PrimitiveFactory.CreateBox("Leg", groupRoot, offsets[i] + Vector3.up * (height * 0.5f),
                    new Vector3(legSize, height, legSize), _assets != null ? _assets.Metal : null, true);
            }
            PrimitiveFactory.CreateBox("Platform", groupRoot, new Vector3(0f, height, 0f),
                new Vector3(spread * 2f + 1.4f, 0.5f, spread * 2f + 1.4f), _assets != null ? _assets.Wood : null, true);
            PrimitiveFactory.CreateBox("Railing", groupRoot, new Vector3(0f, height + 0.8f, spread + 0.6f),
                new Vector3(spread * 2f + 1.4f, 1.1f, 0.2f), _assets != null ? _assets.Metal : null, false);
            PrimitiveFactory.CreateBox("Stairs", groupRoot, new Vector3(0f, height * 0.5f, -spread - 2.2f),
                new Vector3(2f, 0.4f, 6f), _assets != null ? _assets.Wood : null, true,
                Quaternion.Euler(38f, 0f, 0f), GameLayers.Environment);

            MarkFootprint(centerX, centerZ, spread * 2f + 2f, spread * 2f + 2f, 0f, false);
            _level.CoverPoints.Add(new Vector3(centerX, 0f, centerZ + spread + 1.6f));
            _level.EnemySpawnPoints.Add(new Vector3(centerX, height + 0.8f, centerZ));
        }

        private void AddSandbagLine(float centerX, float centerZ, float yaw)
        {
            Transform groupRoot = NewGroup("Sandbags", new Vector3(centerX, 0f, centerZ), yaw);
            int count = _random.RangeInt(3, 6);
            for (int i = 0; i < count; i++)
            {
                float offset = (i - count * 0.5f) * 1.3f;
                PrimitiveFactory.CreateBox("Sandbag", groupRoot, new Vector3(offset, 0.4f, 0f),
                    new Vector3(1.25f, 0.8f, 0.7f), _assets != null ? _assets.Sand : null, true);
            }
            _level.CoverPoints.Add(new Vector3(centerX, 0f, centerZ));
            _level.Grid.FillRect(centerX, centerZ, count * 1.3f, 1f, NavigationGrid.Cover, 0.2f);
        }

        private void BuildStreetProps(float mapSize)
        {
            int crates = Mathf.RoundToInt(mapSize * 0.28f);
            for (int i = 0; i < crates; i++)
            {
                Vector3 point = RandomStreetPoint(mapSize, 12f);
                if (point == Vector3.zero) continue;

                float roll = _random.NextFloat();
                if (roll < 0.4f)
                {
                    Transform group = NewGroup("CrateStack", point, _random.Range(0f, 90f));
                    int stack = _random.RangeInt(1, 3);
                    for (int s = 0; s < stack; s++)
                    {
                        PrimitiveFactory.CreateBox("Crate", group, new Vector3(_random.Range(-0.2f, 0.2f), 0.65f + s * 1.3f, _random.Range(-0.2f, 0.2f)),
                            new Vector3(1.3f, 1.3f, 1.3f), _assets != null ? _assets.Wood : null, true);
                    }
                    _level.CoverPoints.Add(point);
                    _level.Grid.FillRect(point.x, point.z, 1.8f, 1.8f, NavigationGrid.Cover, 0.3f);
                }
                else if (roll < 0.7f)
                {
                    Transform group = NewGroup("Container", point, _random.NextFloat() < 0.5f ? 0f : 90f);
                    PrimitiveFactory.CreateBox("Box", group, new Vector3(0f, 1.35f, 0f),
                        new Vector3(6.2f, 2.7f, 2.7f), _assets != null ? _assets.Rust : null, true);
                    PrimitiveFactory.CreateBox("Rib", group, new Vector3(0f, 2.75f, 0f),
                        new Vector3(6.4f, 0.2f, 2.9f), _assets != null ? _assets.Metal : null, false);
                    _level.CoverPoints.Add(point + Vector3.right * 3.8f);
                    _level.Grid.FillRect(point.x, point.z, 6.4f, 2.9f, NavigationGrid.Unwalkable, 0.4f);
                }
                else if (roll < 0.85f)
                {
                    Transform group = NewGroup("Barrier", point, _random.Range(0f, 180f));
                    PrimitiveFactory.CreateBox("Jersey", group, new Vector3(0f, 0.5f, 0f),
                        new Vector3(3.4f, 1f, 1f), _assets != null ? _assets.Concrete : null, true);
                    _level.CoverPoints.Add(point);
                    _level.Grid.FillRect(point.x, point.z, 3.6f, 1.2f, NavigationGrid.Cover, 0.3f);
                }
                else
                {
                    Transform group = NewGroup("Rock", point, _random.Range(0f, 360f));
                    float scale = _random.Range(1.6f, 3.2f);
                    PrimitiveFactory.CreateMesh("Boulder", group, MeshFactory.Rock((int)_random.NextUInt()),
                        Vector3.zero, Vector3.one * scale, _assets != null ? _assets.Rust : null, ColliderKind.Sphere, GameLayers.Environment);
                    _level.CoverPoints.Add(point);
                    _level.Grid.FillRect(point.x, point.z, scale, scale, NavigationGrid.Unwalkable, 0.2f);
                }
            }

            // Pipes and pylons break up sight lines across the open lanes.
            int pylons = Mathf.Max(4, Mathf.RoundToInt(mapSize * 0.06f));
            for (int i = 0; i < pylons; i++)
            {
                Vector3 point = RandomStreetPoint(mapSize, 16f);
                if (point == Vector3.zero) continue;
                Transform group = NewGroup("Pylon", point, 0f);
                float height = _random.Range(7f, 11f);
                PrimitiveFactory.CreateBox("Mast", group, new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(0.7f, height, 0.7f), _assets != null ? _assets.Metal : null, true);
                PrimitiveFactory.CreateBox("Arm", group, new Vector3(1.8f, height - 0.6f, 0f),
                    new Vector3(3.6f, 0.4f, 0.4f), _assets != null ? _assets.Metal : null, false);
                _level.Grid.FillRect(point.x, point.z, 1f, 1f, NavigationGrid.Unwalkable, 0.1f);
            }
        }

        // ------------------------------------------------------------ objectives

        private void BuildObjectiveSites()
        {
            if (_mission == null) return;
            List<Vector3> sites = PickInteriorSites(12);

            int siteIndex = 0;
            for (int i = 0; i < _mission.Objectives.Count; i++)
            {
                ObjectiveDef objective = _mission.Objectives[i];
                switch (objective.Type)
                {
                    case ObjectiveType.CollectIntel:
                        for (int c = 0; c < objective.TargetCount; c++)
                        {
                            Vector3 position = NextSite(sites, ref siteIndex);
                            _level.IntelPositions.Add(position);
                        }
                        break;
                    case ObjectiveType.RecoverSupplies:
                        for (int c = 0; c < objective.TargetCount; c++)
                        {
                            Vector3 position = NextSite(sites, ref siteIndex);
                            _level.SupplyPositions.Add(position);
                        }
                        break;
                    case ObjectiveType.ActivateDevice:
                    {
                        Vector3 position = NextSite(sites, ref siteIndex);
                        _level.DevicePositions.Add(position);
                        break;
                    }
                    case ObjectiveType.SecureLocation:
                    {
                        Vector3 position = NextSite(sites, ref siteIndex);
                        SecureZoneInfo zone;
                        zone.Center = position;
                        zone.Radius = 8.5f;
                        string tag = string.IsNullOrEmpty(objective.TargetTag) ? objective.Id : objective.TargetTag;
                        _level.SecureZones[tag] = zone;
                        break;
                    }
                    case ObjectiveType.EliminateTarget:
                    {
                        Vector3 position = NextSite(sites, ref siteIndex);
                        _level.TargetSpawnPoints.Add(position);
                        break;
                    }
                    default:
                        break;
                }
            }
        }

        private List<Vector3> PickInteriorSites(int count)
        {
            var sites = new List<Vector3>(_structureInteriors);
            // Deterministic shuffle so objective placement varies per seed but is reproducible.
            for (int i = sites.Count - 1; i > 0; i--)
            {
                int j = _random.RangeInt(0, i + 1);
                Vector3 temp = sites[i];
                sites[i] = sites[j];
                sites[j] = temp;
            }
            if (sites.Count == 0) sites.Add(new Vector3(8f, 0f, 8f));
            while (sites.Count < count) sites.Add(sites[sites.Count % sites.Count] + new Vector3(_random.Range(-8f, 8f), 0f, _random.Range(-8f, 8f)));
            return sites;
        }

        private Vector3 NextSite(List<Vector3> sites, ref int index)
        {
            Vector3 site = sites[index % sites.Count];
            index++;
            return _level.FindWalkableNear(site, 6f);
        }

        private void BuildExtraction(float mapSize)
        {
            float half = mapSize * 0.5f;
            float sign = _random.NextFloat() < 0.5f ? -1f : 1f;
            Vector3 position = new Vector3(half * 0.62f * sign, 0f, half * 0.62f * sign);
            _level.ExtractionPosition = _level.FindWalkableNear(position, 18f);
            _level.ExtractionRadius = 6.5f;

            // Keep the extraction pad clear of geometry so it is always reachable.
            _level.Grid.FillRect(_level.ExtractionPosition.x, _level.ExtractionPosition.z,
                _level.ExtractionRadius * 2f, _level.ExtractionRadius * 2f, NavigationGrid.Walkable, 0f);
        }

        private Vector3 ChoosePlayerSpawn(float mapSize)
        {
            float half = mapSize * 0.5f;
            Vector3 preferred = new Vector3(0f, 0f, -half * 0.7f);
            Vector3 extraction = _level.ExtractionPosition;
            if (Vector3.Distance(preferred, extraction) < mapSize * 0.45f)
            {
                preferred = new Vector3(-half * 0.7f, 0f, half * 0.7f);
            }
            return preferred;
        }

        private void BuildPatrolRoutes()
        {
            float mapSize = _level.MapSize;
            int routeCount = Mathf.Clamp(Mathf.RoundToInt(mapSize * 0.07f), 4, 12);
            for (int i = 0; i < routeCount; i++)
            {
                var route = new List<Vector3>(4);
                Vector3 anchor = RandomStreetPoint(mapSize, 14f);
                if (anchor == Vector3.zero) continue;

                route.Add(_level.FindWalkableNear(anchor, 6f));
                for (int step = 0; step < 2; step++)
                {
                    Vector3 next = anchor + new Vector3(_random.Range(-22f, 22f), 0f, _random.Range(-22f, 22f));
                    next = _level.ClampToBounds(next, 8f);
                    route.Add(_level.FindWalkableNear(next, 8f));
                }
                _level.PatrolRoutes.Add(route);
            }
        }

        private void BuildEnemySpawnPoints(float mapSize)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt(mapSize * 0.16f), 8, 40);
            for (int i = 0; i < count; i++)
            {
                Vector3 point = RandomStreetPoint(mapSize, 10f);
                if (point == Vector3.zero) continue;
                _level.EnemySpawnPoints.Add(_level.FindWalkableNear(point, 5f));
            }

            // Guarantee a few spawns near the extraction so the finale has pressure.
            for (int i = 0; i < 3; i++)
            {
                Vector3 around = _level.ExtractionPosition + new Vector3(_random.Range(-18f, 18f), 0f, _random.Range(-18f, 18f));
                _level.EnemySpawnPoints.Add(_level.FindWalkableNear(around, 8f));
            }
        }

        // ------------------------------------------------------------------ utils

        private Transform NewGroup(string name, Vector3 position, float yaw)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(_root, false);
            group.transform.position = position;
            group.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return group.transform;
        }

        private void MarkFootprint(float centerX, float centerZ, float width, float depth, float yaw, bool solid)
        {
            // Footprints are axis aligned in practice (0/90 degrees), so a rect fill is accurate enough
            // for both A* and cover placement.
            bool rotated = Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < 1f;
            float sizeX = rotated ? depth : width;
            float sizeZ = rotated ? width : depth;
            _level.Grid.FillRect(centerX, centerZ, sizeX, sizeZ,
                solid ? NavigationGrid.Unwalkable : NavigationGrid.Walkable, solid ? 0.4f : -0.6f);
        }

        private Vector3 RandomStreetPoint(float mapSize, float margin)
        {
            float half = mapSize * 0.5f - margin;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float x = _random.Range(-half, half);
                float z = _random.Range(-half, half);

                int gridX;
                int gridY;
                _level.Grid.WorldToCell(new Vector3(x, 0f, z), out gridX, out gridY);
                if (!_level.Grid.IsWalkable(gridX, gridY)) continue;
                if (_level.Grid.Get(gridX, gridY) == NavigationGrid.Unwalkable) continue;

                var point = new Vector3(x, 0f, z);
                _streetPoints.Add(point);
                return point;
            }
            return Vector3.zero;
        }
    }
}
