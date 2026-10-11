using System.Collections.Generic;
using UnityEngine;

namespace Warfront.Core
{
    /// <summary>
    /// Uniform walkability grid generated from level geometry. It backs both the runtime NavMesh
    /// build (as a validation source) and the deterministic A* fallback used when a NavMesh could
    /// not be built - so enemies always have real pathfinding, never a straight-line chase.
    /// </summary>
    public class NavigationGrid
    {
        public const byte Unwalkable = 0;
        public const byte Walkable = 1;
        public const byte Cover = 2;
        public const byte HighCost = 3;

        private readonly byte[] _cells;

        public NavigationGrid(int width, int height, float cellSize, Vector3 origin)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            CellSize = Mathf.Max(0.2f, cellSize);
            Origin = origin;
            _cells = new byte[Width * Height];
            for (int i = 0; i < _cells.Length; i++) _cells[i] = Walkable;
        }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public float CellSize { get; private set; }
        public Vector3 Origin { get; private set; }
        public int CellCount { get { return Width * Height; } }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public byte Get(int x, int y)
        {
            if (!InBounds(x, y)) return Unwalkable;
            return _cells[y * Width + x];
        }

        public void Set(int x, int y, byte value)
        {
            if (!InBounds(x, y)) return;
            _cells[y * Width + x] = value;
        }

        public bool IsWalkable(int x, int y)
        {
            byte cell = Get(x, y);
            return cell == Walkable || cell == Cover || cell == HighCost;
        }

        public void FillRect(float worldCenterX, float worldCenterZ, float sizeX, float sizeZ, byte value, float padding)
        {
            float halfX = sizeX * 0.5f + Mathf.Max(0f, padding);
            float halfZ = sizeZ * 0.5f + Mathf.Max(0f, padding);
            int minX = Mathf.FloorToInt((worldCenterX - halfX - Origin.x) / CellSize);
            int maxX = Mathf.CeilToInt((worldCenterX + halfX - Origin.x) / CellSize);
            int minY = Mathf.FloorToInt((worldCenterZ - halfZ - Origin.z) / CellSize);
            int maxY = Mathf.CeilToInt((worldCenterZ + halfZ - Origin.z) / CellSize);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Set(x, y, value);
                }
            }
        }

        public Vector3 CellToWorld(int x, int y)
        {
            return new Vector3(Origin.x + (x + 0.5f) * CellSize, Origin.y, Origin.z + (y + 0.5f) * CellSize);
        }

        public void WorldToCell(Vector3 world, out int x, out int y)
        {
            x = Mathf.FloorToInt((world.x - Origin.x) / CellSize);
            y = Mathf.FloorToInt((world.z - Origin.z) / CellSize);
        }

        public bool TryGetCell(Vector3 world, out int x, out int y)
        {
            WorldToCell(world, out x, out y);
            return InBounds(x, y);
        }

        public bool TryGetWalkableCell(Vector3 world, out int x, out int y, int maxRadius)
        {
            WorldToCell(world, out x, out y);
            if (IsWalkable(x, y)) return true;

            for (int radius = 1; radius <= Mathf.Max(1, maxRadius); radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;
                        int cx = x + dx;
                        int cy = y + dy;
                        if (!IsWalkable(cx, cy)) continue;
                        x = cx;
                        y = cy;
                        return true;
                    }
                }
            }
            return false;
        }

        public float CellTraversalCost(int x, int y)
        {
            switch (Get(x, y))
            {
                case HighCost: return 2.5f;
                case Cover: return 1.15f;
                default: return 1f;
            }
        }
    }

    /// <summary>
    /// Deterministic A* over a NavigationGrid with octile movement, corner cutting prevention
    /// and a fixed-size binary heap (no allocations during a search).
    /// </summary>
    public class GridPathfinder
    {
        private readonly NavigationGrid _grid;
        private readonly float[] _gScore;
        private readonly float[] _fScore;
        private readonly int[] _cameFrom;
        private readonly bool[] _closed;
        private readonly int[] _heap;
        private readonly int[] _heapIndex;
        private int _heapCount;

        /// <summary>Cells expanded during the last search - surfaced for profiling/AI budgeting.</summary>
        public int LastExpandedCount { get; private set; }

        public GridPathfinder(NavigationGrid grid)
        {
            _grid = grid;
            int count = Mathf.Max(1, grid.CellCount);
            _gScore = new float[count];
            _fScore = new float[count];
            _cameFrom = new int[count];
            _closed = new bool[count];
            _heap = new int[count + 1];
            _heapIndex = new int[count];
        }

        private static readonly int[] DirX = { 0, 0, 1, -1, 1, 1, -1, -1 };
        private static readonly int[] DirY = { 1, -1, 0, 0, 1, -1, 1, -1 };
        private const float Sqrt2 = 1.41421356f;

        /// <summary>
        /// Finds a path from start to goal in world space. Returns the number of waypoints written
        /// into 'output' (world space, straightened at corners). 0 means no path.
        /// </summary>
        public int FindPath(Vector3 startWorld, Vector3 goalWorld, List<Vector3> output, int maxExpansions)
        {
            if (output == null) return 0;
            output.Clear();
            LastExpandedCount = 0;

            int startX;
            int startY;
            int goalX;
            int goalY;
            if (!_grid.TryGetWalkableCell(startWorld, out startX, out startY, 6)) return 0;
            if (!_grid.TryGetWalkableCell(goalWorld, out goalX, out goalY, 8)) return 0;

            int start = CellIndex(startX, startY);
            int goal = CellIndex(goalX, goalY);
            if (start == goal)
            {
                output.Add(_grid.CellToWorld(goalX, goalY));
                return 1;
            }

            for (int i = 0; i < _grid.CellCount; i++)
            {
                _gScore[i] = float.MaxValue;
                _fScore[i] = float.MaxValue;
                _cameFrom[i] = -1;
                _closed[i] = false;
                _heapIndex[i] = -1;
            }

            _heapCount = 0;
            _gScore[start] = 0f;
            _fScore[start] = Heuristic(startX, startY, goalX, goalY);
            HeapPush(start);

            int expanded = 0;
            int maxSteps = maxExpansions > 0 ? maxExpansions : _grid.CellCount;

            while (_heapCount > 0)
            {
                int current = HeapPop();
                if (current == goal) return BuildPath(start, goal, output);

                if (_closed[current]) continue;
                _closed[current] = true;
                expanded++;
                LastExpandedCount = expanded;
                if (expanded > maxSteps) break;

                int cx = current % _grid.Width;
                int cy = current / _grid.Width;

                for (int dir = 0; dir < 8; dir++)
                {
                    int nx = cx + DirX[dir];
                    int ny = cy + DirY[dir];
                    if (!_grid.IsWalkable(nx, ny)) continue;

                    bool diagonal = DirX[dir] != 0 && DirY[dir] != 0;
                    // Never cut corners through blocked cells - prevents enemies clipping geometry.
                    if (diagonal && (!_grid.IsWalkable(cx + DirX[dir], cy) || !_grid.IsWalkable(cx, cy + DirY[dir]))) continue;

                    int neighbour = CellIndex(nx, ny);
                    if (_closed[neighbour]) continue;

                    float step = (diagonal ? Sqrt2 : 1f) * _grid.CellTraversalCost(nx, ny);
                    float tentative = _gScore[current] + step;
                    if (tentative >= _gScore[neighbour]) continue;

                    _cameFrom[neighbour] = current;
                    _gScore[neighbour] = tentative;
                    _fScore[neighbour] = tentative + Heuristic(nx, ny, goalX, goalY) * 1.05f;
                    HeapPush(neighbour);
                }
            }

            return 0;
        }

        private static float Heuristic(int x, int y, int goalX, int goalY)
        {
            int dx = Mathf.Abs(x - goalX);
            int dy = Mathf.Abs(y - goalY);
            int min = Mathf.Min(dx, dy);
            return (dx + dy) + (Sqrt2 - 2f) * min;
        }

        private int CellIndex(int x, int y)
        {
            return y * _grid.Width + x;
        }

        private int BuildPath(int start, int goal, List<Vector3> output)
        {
            // Walk backwards then reverse, collapsing collinear cells (string pulling).
            int current = goal;
            int guard = 0;
            List<int> cells = new List<int>(32);
            while (current != -1 && guard++ < _grid.CellCount)
            {
                cells.Add(current);
                if (current == start) break;
                current = _cameFrom[current];
            }

            if (cells.Count == 0 || cells[cells.Count - 1] != start) return 0;

            for (int i = cells.Count - 1; i >= 0; i--)
            {
                int index = cells[i];
                int x = index % _grid.Width;
                int y = index / _grid.Width;

                if (output.Count >= 2)
                {
                    // Skip a waypoint when the previous and next leg share a direction.
                    Vector3 prev = output[output.Count - 1];
                    Vector3 prevPrev = output[output.Count - 2];
                    Vector3 dirA = new Vector3(prev.x - prevPrev.x, 0f, prev.z - prevPrev.z).normalized;
                    Vector3 candidate = _grid.CellToWorld(x, y);
                    Vector3 dirB = new Vector3(candidate.x - prev.x, 0f, candidate.z - prev.z).normalized;
                    if (Vector3.Dot(dirA, dirB) > 0.999f) continue;
                }

                output.Add(_grid.CellToWorld(x, y));
            }
            return output.Count;
        }

        private void HeapPush(int index)
        {
            if (_heapIndex[index] >= 0)
            {
                HeapifyUp(_heapIndex[index]);
                return;
            }
            if (_heapCount >= _heap.Length) return;
            _heap[_heapCount] = index;
            _heapIndex[index] = _heapCount;
            _heapCount++;
            HeapifyUp(_heapCount - 1);
        }

        private int HeapPop()
        {
            int top = _heap[0];
            _heapIndex[top] = -1;
            _heapCount--;
            if (_heapCount > 0)
            {
                _heap[0] = _heap[_heapCount];
                _heapIndex[_heap[0]] = 0;
                HeapifyDown(0);
            }
            return top;
        }

        private void HeapifyUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_fScore[_heap[parent]] <= _fScore[_heap[i]]) break;
                Swap(parent, i);
                i = parent;
            }
        }

        private void HeapifyDown(int i)
        {
            while (true)
            {
                int left = i * 2 + 1;
                int right = left + 1;
                int smallest = i;
                if (left < _heapCount && _fScore[_heap[left]] < _fScore[_heap[smallest]]) smallest = left;
                if (right < _heapCount && _fScore[_heap[right]] < _fScore[_heap[smallest]]) smallest = right;
                if (smallest == i) return;
                Swap(smallest, i);
                i = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            int tmp = _heap[a];
            _heap[a] = _heap[b];
            _heap[b] = tmp;
            _heapIndex[_heap[a]] = a;
            _heapIndex[_heap[b]] = b;
        }
    }
}
