using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    public struct SecureZoneInfo
    {
        public Vector3 Center;
        public float Radius;
    }

    /// <summary>
    /// Everything the mission needs to know about the generated map: navigation, spawns, patrol routes,
    /// cover, objective locations and the interaction list. It is produced by LevelGenerator and is the
    /// only contract between level building and gameplay.
    /// </summary>
    public class LevelData
    {
        public GameObject Root;
        public NavigationGrid Grid;
        public Bounds Bounds;
        public float MapSize = 120f;
        public bool NavMeshBuilt;

        public Vector3 PlayerSpawn;
        public Vector3 ExtractionPosition;
        public float ExtractionRadius = 6f;

        public readonly List<Vector3> EnemySpawnPoints = new List<Vector3>(64);
        public readonly List<Vector3> CoverPoints = new List<Vector3>(64);
        public readonly List<List<Vector3>> PatrolRoutes = new List<List<Vector3>>(16);
        public readonly List<Vector3> LootContainerPositions = new List<Vector3>(16);
        public readonly List<Vector3> IntelPositions = new List<Vector3>(8);
        public readonly List<Vector3> SupplyPositions = new List<Vector3>(8);
        public readonly List<Vector3> DevicePositions = new List<Vector3>(4);
        public readonly List<Vector3> TargetSpawnPoints = new List<Vector3>(4);
        public readonly Dictionary<string, SecureZoneInfo> SecureZones = new Dictionary<string, SecureZoneInfo>(4);

        public GridPathfinder _pathfinder;

        /// <summary>Cached pathfinder over the walkability grid (created once per level).</summary>
        public GridPathfinder CreatePathfinder()
        {
            if (_pathfinder == null && Grid != null) _pathfinder = new GridPathfinder(Grid);
            return _pathfinder;
        }

        public Vector3 ClampToBounds(Vector3 position, float margin)
        {
            float half = MapSize * 0.5f - margin;
            return new Vector3(
                WfMath.Clamp(position.x, -half, half),
                position.y,
                WfMath.Clamp(position.z, -half, half));
        }

        /// <summary>Finds a walkable point near 'origin' - used to unstick spawns.</summary>
        public Vector3 FindWalkableNear(Vector3 origin, float searchRadius)
        {
            if (Grid == null) return origin;
            int x;
            int y;
            if (Grid.TryGetWalkableCell(origin, out x, out y, Mathf.RoundToInt(searchRadius / Mathf.Max(0.5f, Grid.CellSize))))
            {
                return Grid.CellToWorld(x, y);
            }
            return origin;
        }
    }
}
