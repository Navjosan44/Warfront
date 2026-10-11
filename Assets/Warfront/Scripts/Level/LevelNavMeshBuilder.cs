using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Warfront.Game
{
    /// <summary>
    /// Bakes a NavMesh at runtime from the generated level geometry using the core AI module
    /// (no package dependency). If the bake fails for any reason the AI silently falls back to the
    /// deterministic grid A* pathfinder, so navigation never breaks the game.
    /// </summary>
    public static class LevelNavMeshBuilder
    {
        public static bool Build(LevelData level, Transform geometryRoot)
        {
            if (level == null || geometryRoot == null) return false;

            try
            {
                NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
                settings.agentRadius = 0.36f;
                settings.agentHeight = 1.8f;
                settings.agentSlope = 45f;
                settings.agentClimb = 0.42f;
                settings.minRegionArea = 4f;
                settings.overrideVoxelSize = false;

                Bounds bounds = level.Bounds;
                bounds.Expand(4f);

                List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>(256);
                List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>(4);
                NavMeshBuilder.CollectSources(bounds, GameLayers.EnvironmentMask, NavMeshCollectGeometry.PhysicsColliders,
                    0, markups, sources);

                if (sources.Count == 0)
                {
                    Debug.LogWarning("[Warfront] NavMesh bake skipped: no source geometry was collected.");
                    return false;
                }

                NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                if (data == null)
                {
                    Debug.LogWarning("[Warfront] NavMesh data build returned null - AI will use grid pathfinding.");
                    return false;
                }

                NavMeshDataInstance instance = NavMesh.AddNavMeshData(data);
                if (!instance.valid)
                {
                    Debug.LogWarning("[Warfront] NavMesh could not be added - AI will use grid pathfinding.");
                    return false;
                }

                level.NavMeshBuilt = true;
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Warfront] Runtime NavMesh bake failed (" + ex.Message + "). AI will use grid pathfinding.");
                level.NavMeshBuilt = false;
                return false;
            }
        }
    }
}
