using System.Collections.Generic;
using UnityEngine;

public static class BridgePathfinding
{
    private static readonly Vector2Int[] FourDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public static bool HasRoute(char[,] grid, Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> path = FindPath(grid, start, goal, pathTilesOnly: false, allowGrassDetour: true);
        return path != null && path.Count > 0;
    }

    public static List<Vector2Int> FindPath(
        char[,] grid,
        Vector2Int start,
        Vector2Int goal,
        bool pathTilesOnly,
        bool allowGrassDetour,
        float pathCost = 1f,
        float grassCost = 8f)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        var openSet = new List<AStarNode>();
        var closedSet = new HashSet<Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        gScore[start] = 0f;
        openSet.Add(CreateNode(start, 0f, Heuristic(start, goal)));

        while (openSet.Count > 0)
        {
            openSet.Sort((a, b) => a.f.CompareTo(b.f));
            AStarNode current = openSet[0];
            openSet.RemoveAt(0);

            if (current.position == goal)
            {
                return ReconstructPath(cameFrom, start, goal);
            }

            if (closedSet.Contains(current.position))
            {
                continue;
            }

            closedSet.Add(current.position);

            foreach (Vector2Int direction in FourDirections)
            {
                Vector2Int next = current.position + direction;
                if (!IsInside(grid, next) || !IsWalkable(grid, next, pathTilesOnly, allowGrassDetour))
                {
                    continue;
                }

                char cell = MapProfile.NormalizeCell(grid[next.y, next.x]);
                float stepCost = GetMoveCost(cell, pathCost, grassCost);
                float tentativeG = current.g + stepCost;

                if (gScore.TryGetValue(next, out float knownG) && tentativeG >= knownG)
                {
                    continue;
                }

                gScore[next] = tentativeG;
                cameFrom[next] = current.position;
                openSet.Add(CreateNode(next, tentativeG, Heuristic(next, goal)));
            }
        }

        return null;
    }

    public static Dictionary<Vector2Int, int> BuildDistanceFromPath(List<Vector2Int> path, int width, int height)
    {
        var distances = new Dictionary<Vector2Int, int>();
        var queue = new Queue<Vector2Int>();

        foreach (Vector2Int cell in path)
        {
            distances[cell] = 0;
            queue.Enqueue(cell);
        }

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int nextDistance = distances[current] + 1;

            foreach (Vector2Int direction in FourDirections)
            {
                Vector2Int neighbor = current + direction;
                if (neighbor.x < 0 || neighbor.y < 0 || neighbor.x >= width || neighbor.y >= height)
                {
                    continue;
                }

                if (distances.ContainsKey(neighbor))
                {
                    continue;
                }

                distances[neighbor] = nextDistance;
                queue.Enqueue(neighbor);
            }
        }

        return distances;
    }

    private struct AStarNode
    {
        public Vector2Int position;
        public float g;
        public float f;
    }

    private static AStarNode CreateNode(Vector2Int position, float g, float heuristic)
    {
        return new AStarNode
        {
            position = position,
            g = g,
            f = g + heuristic
        };
    }

    private static float Heuristic(Vector2Int from, Vector2Int to)
    {
        return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
    }

    private static float GetMoveCost(char cell, float pathCost, float grassCost)
    {
        cell = MapProfile.NormalizeCell(cell);
        if (BridgeMapJsonUtility.IsPathTile(cell))
        {
            return pathCost;
        }

        if (cell == 'G' || cell == 'g')
        {
            return grassCost;
        }

        return grassCost;
    }

    private static bool IsInside(char[,] grid, Vector2Int point)
    {
        return point.x >= 0 && point.y >= 0 && point.x < grid.GetLength(1) && point.y < grid.GetLength(0);
    }

    private static bool IsWalkable(char[,] grid, Vector2Int point, bool pathTilesOnly, bool allowGrassDetour)
    {
        if (!IsInside(grid, point))
        {
            return false;
        }

        char cell = MapProfile.NormalizeCell(grid[point.y, point.x]);
        if (BridgeMapJsonUtility.IsBlockedCell(cell))
        {
            return false;
        }

        if (BridgeMapJsonUtility.IsPathTile(cell))
        {
            return true;
        }

        if (pathTilesOnly)
        {
            return false;
        }

        if (cell == 'G' || cell == 'g')
        {
            return true;
        }

        return allowGrassDetour && (cell == 's' || cell == 'd' || cell == '.');
    }

    private static List<Vector2Int> ReconstructPath(
        Dictionary<Vector2Int, Vector2Int> parents,
        Vector2Int start,
        Vector2Int goal)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int current = goal;

        while (true)
        {
            path.Add(current);
            if (current == start)
            {
                break;
            }

            if (!parents.TryGetValue(current, out current))
            {
                return null;
            }
        }

        path.Reverse();
        return path;
    }
}
