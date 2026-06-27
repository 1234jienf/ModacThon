using System.Collections.Generic;
using UnityEngine;

public static class BridgeObstaclePlacer
{
    public struct PlacementReport
    {
        public int requestedCount;
        public int placedCount;
        public int lakeCount;
        public int solidCount;
        public int pathBlockCount;
    }

    public static PlacementReport TryPlaceObstacles(
        char[,] grid,
        BridgeDifficultyPreset preset,
        BridgeObstacleCatalog catalog)
    {
        PlacementReport report = default;
        if (preset == null || !preset.placeObstacles || catalog == null)
        {
            return report;
        }

        catalog.EnsureCollected();

        int width = grid.GetLength(1);
        int height = grid.GetLength(0);
        if (!BridgeMapJsonUtility.TryFindBridgeEndpoints(grid, out Vector2Int start, out Vector2Int goal))
        {
            BridgeMapJsonUtility.MarkBridgeEndpoints(grid);
            if (!BridgeMapJsonUtility.TryFindBridgeEndpoints(grid, out start, out goal))
            {
                return report;
            }
        }

        List<Vector2Int> optimalPath = BridgePathfinding.FindPath(grid, start, goal, true, false);
        if (optimalPath == null || optimalPath.Count == 0)
        {
            Debug.LogWarning("BridgeObstaclePlacer: optimal path not found, skipping obstacles.");
            return report;
        }

        Dictionary<Vector2Int, int> distanceFromPath =
            BridgePathfinding.BuildDistanceFromPath(optimalPath, width, height);

        int targetCount = Random.Range(preset.obstacleCountMin, preset.obstacleCountMax + 1);
        report.requestedCount = targetCount;

        List<PlacementCandidate> onPathCandidates = BuildOnPathCandidates(grid, optimalPath, start, goal);
        List<PlacementCandidate> offPathCandidates = BuildOffPathCandidates(
            grid,
            distanceFromPath,
            preset.minDistanceFromPath,
            preset.maxDistanceFromPath);

        int onPathTarget = Mathf.RoundToInt(targetCount * preset.pathBlockChance);
        int offPathTarget = targetCount - onPathTarget;

        Shuffle(onPathCandidates);
        Shuffle(offPathCandidates);

        PlaceFromCandidates(grid, start, goal, width, height, catalog, preset, onPathCandidates, onPathTarget, true, ref report);
        PlaceFromCandidates(grid, start, goal, width, height, catalog, preset, offPathCandidates, offPathTarget, false, ref report);

        Debug.Log(
            $"BridgeObstaclePlacer L{BridgeDifficultySettings.ActiveLevel}: " +
            $"placed={report.placedCount}/{report.requestedCount}, lakes={report.lakeCount}, " +
            $"solids={report.solidCount}, pathBlocks={report.pathBlockCount}");
        return report;
    }

    private struct PlacementCandidate
    {
        public Vector2Int anchor;
        public int distanceFromPath;
        public bool isPathCell;
    }

    private static void PlaceFromCandidates(
        char[,] grid,
        Vector2Int start,
        Vector2Int goal,
        int width,
        int height,
        BridgeObstacleCatalog catalog,
        BridgeDifficultyPreset preset,
        List<PlacementCandidate> candidates,
        int targetCount,
        bool countsAsPathBlock,
        ref PlacementReport report)
    {
        int placed = 0;
        foreach (PlacementCandidate candidate in candidates)
        {
            if (placed >= targetCount)
            {
                break;
            }

            bool preferLake = false;
            BridgeObstacleEntry entry = catalog.PickEntry(candidate.anchor.x / (float)Mathf.Max(1, width - 1), preferLake);

            if (TryPlaceEntry(grid, start, goal, width, height, candidate.anchor, entry, out int placedCells))
            {
                placed++;
                report.placedCount += placedCells;
                if (entry.gridChar == 'w')
                {
                    report.lakeCount += placedCells;
                }
                else
                {
                    report.solidCount += placedCells;
                }

                if (countsAsPathBlock)
                {
                    report.pathBlockCount += placedCells;
                }
            }
        }
    }

    private static bool TryPlaceEntry(
        char[,] grid,
        Vector2Int start,
        Vector2Int goal,
        int width,
        int height,
        Vector2Int anchor,
        BridgeObstacleEntry entry,
        out int placedCells)
    {
        placedCells = 0;
        List<Vector2Int> cells = BuildFootprintCells(anchor, entry, width, height);
        if (cells.Count == 0)
        {
            return false;
        }

        List<(Vector2Int cell, char previous)> backups = new List<(Vector2Int, char)>();
        foreach (Vector2Int cell in cells)
        {
            if (cell == start || cell == goal)
            {
                RestoreCells(grid, backups);
                return false;
            }

            char current = grid[cell.y, cell.x];
            if (current == '#' || current == 'S' || current == 'E')
            {
                RestoreCells(grid, backups);
                return false;
            }

            backups.Add((cell, current));
            grid[cell.y, cell.x] = 'O';
        }

        if (!BridgePathfinding.HasRoute(grid, start, goal))
        {
            RestoreCells(grid, backups);
            return false;
        }

        placedCells = cells.Count;
        return true;
    }

    private static List<Vector2Int> BuildFootprintCells(
        Vector2Int anchor,
        BridgeObstacleEntry entry,
        int width,
        int height)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int y = 0; y < entry.height; y++)
        {
            for (int x = 0; x < entry.width; x++)
            {
                Vector2Int cell = new Vector2Int(anchor.x + x, anchor.y + y);
                if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
                {
                    return new List<Vector2Int>();
                }

                cells.Add(cell);
            }
        }

        return cells;
    }

    private static void RestoreCells(char[,] grid, List<(Vector2Int cell, char previous)> backups)
    {
        foreach ((Vector2Int cell, char previous) in backups)
        {
            grid[cell.y, cell.x] = previous;
        }
    }

    private static List<PlacementCandidate> BuildOnPathCandidates(
        char[,] grid,
        List<Vector2Int> optimalPath,
        Vector2Int start,
        Vector2Int goal)
    {
        List<PlacementCandidate> candidates = new List<PlacementCandidate>();
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();

        foreach (Vector2Int cell in optimalPath)
        {
            if (cell == start || cell == goal || !seen.Add(cell))
            {
                continue;
            }

            if (grid[cell.y, cell.x] != 'P')
            {
                continue;
            }

            candidates.Add(new PlacementCandidate
            {
                anchor = cell,
                distanceFromPath = 0,
                isPathCell = true
            });
        }

        return candidates;
    }

    private static List<PlacementCandidate> BuildOffPathCandidates(
        char[,] grid,
        Dictionary<Vector2Int, int> distanceFromPath,
        int minDistance,
        int maxDistance)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        List<PlacementCandidate> candidates = new List<PlacementCandidate>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char cell = grid[y, x];
                if (cell != 'G' && cell != 'g')
                {
                    continue;
                }

                Vector2Int position = new Vector2Int(x, y);
                if (!distanceFromPath.TryGetValue(position, out int distance))
                {
                    continue;
                }

                if (distance < minDistance || distance > maxDistance)
                {
                    continue;
                }

                candidates.Add(new PlacementCandidate
                {
                    anchor = position,
                    distanceFromPath = distance,
                    isPathCell = false
                });
            }
        }

        candidates.Sort((a, b) => a.distanceFromPath.CompareTo(b.distanceFromPath));
        return candidates;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }
}
