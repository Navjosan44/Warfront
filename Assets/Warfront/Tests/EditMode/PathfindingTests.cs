using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Tests.EditMode
{
    public class PathfindingTests
    {
        private NavigationGrid BuildGrid(int width, int height, float cellSize)
        {
            return new NavigationGrid(width, height, cellSize, new Vector3(0f, 0f, 0f));
        }

        [Test]
        public void GridDefaultsToWalkable()
        {
            NavigationGrid grid = BuildGrid(10, 10, 1f);
            Assert.IsTrue(grid.IsWalkable(0, 0));
            Assert.IsTrue(grid.IsWalkable(9, 9));
            Assert.AreEqual(100, grid.CellCount);
        }

        [Test]
        public void OutOfBoundsCellsAreUnwalkable()
        {
            NavigationGrid grid = BuildGrid(5, 5, 1f);
            Assert.IsFalse(grid.IsWalkable(-1, 0));
            Assert.IsFalse(grid.IsWalkable(0, 99));
        }

        [Test]
        public void StraightPathIsFound()
        {
            NavigationGrid grid = BuildGrid(20, 20, 1f);
            GridPathfinder finder = new GridPathfinder(grid);
            List<Vector3> path = new List<Vector3>();

            int count = finder.FindPath(new Vector3(0.5f, 0f, 0.5f), new Vector3(15.5f, 0f, 0.5f), path, 10000);
            Assert.Greater(count, 0, "a clear straight line must produce a path");
            Assert.Greater(path.Count, 0);
            Assert.Greater(finder.LastExpandedCount, 0);
        }

        [Test]
        public void PathAvoidsBlockedCells()
        {
            NavigationGrid grid = BuildGrid(30, 30, 1f);
            // Wall across the middle with a gap on the right.
            for (int y = 0; y < 25; y++) grid.Set(15, y, NavigationGrid.Unwalkable);

            GridPathfinder finder = new GridPathfinder(grid);
            List<Vector3> path = new List<Vector3>();
            int count = finder.FindPath(new Vector3(2.5f, 0f, 2.5f), new Vector3(28.5f, 0f, 2.5f), path, 20000);

            Assert.Greater(count, 0, "a gap must be found around the wall");
            for (int i = 0; i < path.Count; i++)
            {
                int x;
                int y;
                grid.WorldToCell(path[i], out x, out y);
                Assert.IsTrue(grid.IsWalkable(x, y), "path must never cross a blocked cell at waypoint " + i);
            }
        }

        [Test]
        public void NoPathWhenFullyWalledOff()
        {
            NavigationGrid grid = BuildGrid(20, 20, 1f);
            for (int y = 0; y < 20; y++) grid.Set(10, y, NavigationGrid.Unwalkable);
            for (int x = 0; x < 10; x++) grid.Set(x, 20 - 1, NavigationGrid.Unwalkable);
            for (int x = 0; x < 10; x++) grid.Set(x, 0, NavigationGrid.Unwalkable);
            // Fully enclose the left half.
            for (int x = 0; x <= 10; x++) grid.Set(x, 10, NavigationGrid.Unwalkable);

            GridPathfinder finder = new GridPathfinder(grid);
            List<Vector3> path = new List<Vector3>();
            int count = finder.FindPath(new Vector3(1.5f, 0f, 1.5f), new Vector3(18.5f, 0f, 18.5f), path, 20000);
            Assert.AreEqual(0, count, "an enclosed region must produce no path");
        }

        [Test]
        public void StartEqualsGoalReturnsSingleWaypoint()
        {
            NavigationGrid grid = BuildGrid(10, 10, 1f);
            GridPathfinder finder = new GridPathfinder(grid);
            List<Vector3> path = new List<Vector3>();
            int count = finder.FindPath(new Vector3(5.5f, 0f, 5.5f), new Vector3(5.5f, 0f, 5.5f), path, 1000);
            Assert.AreEqual(1, count);
            Assert.AreEqual(1, path.Count);
        }

        [Test]
        public void BlockedStartSnapsToNearestWalkableCell()
        {
            NavigationGrid grid = BuildGrid(20, 20, 1f);
            grid.FillRect(5.5f, 5.5f, 6f, 6f, NavigationGrid.Unwalkable, 0f);
            GridPathfinder finder = new GridPathfinder(grid);
            List<Vector3> path = new List<Vector3>();

            int count = finder.FindPath(new Vector3(5.5f, 0f, 5.5f), new Vector3(15.5f, 0f, 15.5f), path, 20000);
            Assert.Greater(count, 0, "a blocked spawn must snap to the nearest walkable cell instead of failing");
        }

        [Test]
        public void ExpansionBudgetIsRespected()
        {
            NavigationGrid grid = BuildGrid(120, 120, 1f);
            GridPathfinder finder = new GridPathfinder(grid);
            List<Vector3> path = new List<Vector3>();
            finder.FindPath(new Vector3(1f, 0f, 1f), new Vector3(118f, 0f, 118f), path, 50);
            Assert.LessOrEqual(finder.LastExpandedCount, 52, "AI path budgeting must cap expansions");
        }

        [Test]
        public void FindPathDoesNotThrowOnEmptyOutputList()
        {
            NavigationGrid grid = BuildGrid(10, 10, 1f);
            GridPathfinder finder = new GridPathfinder(grid);
            Assert.DoesNotThrow(() => finder.FindPath(Vector3.zero, new Vector3(5f, 0f, 5f), null, 100));
        }

        [Test]
        public void HighCostCellsAreStillWalkable()
        {
            NavigationGrid grid = BuildGrid(10, 10, 1f);
            grid.Set(4, 4, NavigationGrid.HighCost);
            grid.Set(5, 4, NavigationGrid.Cover);
            Assert.IsTrue(grid.IsWalkable(4, 4));
            Assert.IsTrue(grid.IsWalkable(5, 4));
            Assert.Greater(grid.CellTraversalCost(4, 4), grid.CellTraversalCost(5, 4));
        }

        [Test]
        public void PathIsStableForSameGridAndEndpoints()
        {
            NavigationGrid grid = BuildGrid(24, 24, 1f);
            for (int y = 5; y < 20; y++) grid.Set(12, y, NavigationGrid.Unwalkable);
            GridPathfinder finder = new GridPathfinder(grid);
            List<Vector3> first = new List<Vector3>();
            List<Vector3> second = new List<Vector3>();
            finder.FindPath(new Vector3(2.5f, 0f, 2.5f), new Vector3(20.5f, 0f, 18.5f), first, 20000);
            finder.FindPath(new Vector3(2.5f, 0f, 2.5f), new Vector3(20.5f, 0f, 18.5f), second, 20000);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++) Assert.AreEqual(first[i], second[i]);
        }

        [Test]
        public void WorldToCellAndBackIsConsistent()
        {
            NavigationGrid grid = BuildGrid(30, 30, 2f);
            Vector3 world = new Vector3(9f, 0f, 14f);
            int x;
            int y;
            grid.WorldToCell(world, out x, out y);
            Vector3 back = grid.CellToWorld(x, y);
            Assert.AreEqual(2f, Mathf.Abs(back.x - world.x), 2f);
            Assert.AreEqual(2f, Mathf.Abs(back.z - world.z), 2f);
        }
    }
}
