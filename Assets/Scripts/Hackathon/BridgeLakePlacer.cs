using System.Collections.Generic;
using UnityEngine;

public static class BridgeLakePlacer
{
    public const char LakeBlockChar = 'W';

    public struct LakePlacementReport
    {
        public int requestedPatches;
        public int placedPatches;
        public int lakeCellCount;
        public int pathCellsBlocked;
    }

    public static LakePlacementReport TryPlaceLakes(
        char[,] grid,
        int[,] lakeAutotileIndices,
        BridgeDifficultyPreset preset)
    {
        LakePlacementReport report = default;
        if (preset == null || !preset.placeLakes || lakeAutotileIndices == null)
        {
            return report;
        }

        int width = grid.GetLength(1);
        int height = grid.GetLength(0);
        ClearLakeIndices(lakeAutotileIndices);

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
            Debug.LogWarning("BridgeLakePlacer: optimal path not found, skipping lakes.");
            return report;
        }

        bool[,] lakeMask = new bool[height, width];
        int targetLakeCells = ComputeTargetLakeCellCount(grid, preset, width, height);
        int patchAttempts = ComputeTargetPatchCount(grid, preset, width, height);
        report.requestedPatches = patchAttempts;

        int maxAttempts = Mathf.Max(patchAttempts * 6, 48);
        for (int i = 0; i < maxAttempts; i++)
        {
            if (CountLakeMaskCells(lakeMask) >= targetLakeCells)
            {
                break;
            }

            if (TryPlaceLakePatch(grid, start, goal, width, height, preset, optimalPath, lakeMask, ref report))
            {
                report.placedPatches++;
            }
        }

        if (report.placedPatches == 0)
        {
            report.placedPatches += TryFallbackLakePatch(grid, start, goal, width, height, preset, lakeMask);
        }

        if (!HasInteriorLakeCell(lakeMask, width, height))
        {
            if (TryForceMinimumGrassLake(grid, start, goal, width, height, preset, lakeMask))
            {
                report.placedPatches++;
                Debug.Log("BridgeLakePlacer: mandatory grass lake placed.");
            }
        }

        // path 가로지르는 lake는 Hard 전용, grass lake 이후 마지막에만 시도
        TryPlacePathCrossingLakes(grid, start, goal, width, height, preset, optimalPath, lakeMask, ref report);

        ApplyLakeAutotileIndices(grid, lakeMask, lakeAutotileIndices);
        report.lakeCellCount = CountLakeCells(lakeAutotileIndices);

        Debug.Log(
            $"BridgeLakePlacer L{BridgeDifficultySettings.ActiveLevel}: patches={report.placedPatches}/{report.requestedPatches}, " +
            $"cells={report.lakeCellCount}/{targetLakeCells}, pathBlocked={report.pathCellsBlocked}, lakeChance={preset.lakeChance:F2}");
        return report;
    }

    private static int ComputeTargetLakeCellCount(
        char[,] grid,
        BridgeDifficultyPreset preset,
        int width,
        int height)
    {
        if (preset.lakeChance <= 0f)
        {
            int avg = (preset.lakePatchMinSize + preset.lakePatchMaxSize + 1) / 2;
            int fallbackPatches = (preset.lakePatchCountMin + preset.lakePatchCountMax + 1) / 2;
            return fallbackPatches * avg * avg;
        }

        int grassInZone = CountGrassInLakeZone(grid, preset, width, height);
        return Mathf.Max(
            preset.lakePatchMinSize * preset.lakePatchMinSize,
            Mathf.RoundToInt(grassInZone * preset.lakeChance));
    }

    private static int CountLakeMaskCells(bool[,] lakeMask)
    {
        int count = 0;
        int height = lakeMask.GetLength(0);
        int width = lakeMask.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (lakeMask[y, x])
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int PickPatchSize(BridgeDifficultyPreset preset)
    {
        int min = preset.lakePatchMinSize;
        int max = preset.lakePatchMaxSize;
        if (max <= min)
        {
            return min;
        }

        // 큰 lake 우선 — Random^0.2 → max 쪽 skew
        float t = Mathf.Pow(Random.value, 0.2f);
        return min + Mathf.RoundToInt(t * (max - min));
    }

    // NESW bitmask (N=8,E=4,S=2,W=1) — bit set = that neighbor is lake
    // index: 0=l, 1=tl, 2=t, 3=tr, 4=r, 5=br, 6=b, 7=bl, 8=c
    private static readonly int[] LakeAutotileByMask =
    {
        8, // 0  none
        0, // 1  W
        6, // 2  S
        3, // 3  SW → tr
        4, // 4  E
        2, // 5  EW → t
        1, // 6  ES → tl
        2, // 7  ESW → t
        2, // 8  N
        5, // 9  NW → br
        0, // 10 NS → l
        4, // 11 NSW → r
        7, // 12 NE → bl
        6, // 13 NEW → b
        0, // 14 NES → l
        8, // 15 all → c
    };

    public static int ComputeAutotileIndex(bool[,] lakeMask, int x, int y, int width, int height)
    {
        bool n = IsLakeAt(lakeMask, x, y + 1, width, height);
        bool e = IsLakeAt(lakeMask, x + 1, y, width, height);
        bool s = IsLakeAt(lakeMask, x, y - 1, width, height);
        bool w = IsLakeAt(lakeMask, x - 1, y, width, height);

        int mask = (n ? 8 : 0) | (e ? 4 : 0) | (s ? 2 : 0) | (w ? 1 : 0);
        return LakeAutotileByMask[mask];
    }

    private static int ComputeTargetPatchCount(
        char[,] grid,
        BridgeDifficultyPreset preset,
        int width,
        int height)
    {
        int minPatches = preset.lakePatchCountMin;
        int maxPatches = Mathf.Max(minPatches, preset.lakePatchCountMax);

        if (preset.lakeChance <= 0f)
        {
            return Random.Range(minPatches, maxPatches + 1);
        }

        int grassInZone = CountGrassInLakeZone(grid, preset, width, height);
        int avgSize = (preset.lakePatchMinSize + preset.lakePatchMaxSize + 1) / 2;
        int cellsPerPatch = Mathf.Max(1, avgSize * avgSize);
        int targetLakeCells = Mathf.RoundToInt(grassInZone * preset.lakeChance);
        int estimated = Mathf.CeilToInt(targetLakeCells / (float)cellsPerPatch);
        estimated = Mathf.Max(estimated, minPatches);

        return Mathf.Clamp(estimated, minPatches, maxPatches);
    }

    private static int CountGrassInLakeZone(
        char[,] grid,
        BridgeDifficultyPreset preset,
        int width,
        int height)
    {
        int count = 0;
        int minX = Mathf.RoundToInt(width * preset.lakeZoneStart);
        int maxX = Mathf.RoundToInt(width * preset.lakeZoneEnd);

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                char cell = grid[y, x];
                if (cell == 'G' || cell == 'g')
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void TryPlacePathCrossingLakes(
        char[,] grid,
        Vector2Int start,
        Vector2Int goal,
        int width,
        int height,
        BridgeDifficultyPreset preset,
        List<Vector2Int> optimalPath,
        bool[,] lakeMask,
        ref LakePlacementReport report)
    {
        if (preset.lakePathBlockChance <= 0f || BridgeDifficultySettings.ActiveLevel < 3)
        {
            return;
        }

        const int patchSize = 3;
        List<Vector2Int> candidates = new List<Vector2Int>();
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();

        foreach (Vector2Int pathCell in optimalPath)
        {
            if (pathCell == start || pathCell == goal || !seen.Add(pathCell))
            {
                continue;
            }

            if (grid[pathCell.y, pathCell.x] != 'P')
            {
                continue;
            }

            candidates.Add(pathCell);
        }

        Shuffle(candidates);
        int target = Mathf.Max(1, Mathf.RoundToInt(candidates.Count * preset.lakePathBlockChance * 0.15f));
        int placed = 0;

        foreach (Vector2Int anchor in candidates)
        {
            if (placed >= target)
            {
                break;
            }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                int offsetX = attempt % 3 - 1;
                int offsetY = attempt / 3 % 3 - 1;
                int ax = anchor.x + offsetX;
                int ay = anchor.y + offsetY;
                List<Vector2Int> cells = BuildPatchCells(ax, ay, patchSize, patchSize);

                if (!IsPathCrossingPatchValid(grid, cells, width, height))
                {
                    continue;
                }

                if (!TryApplyPatch(grid, start, goal, cells, lakeMask, allowPathCells: true))
                {
                    continue;
                }

                placed++;
                report.pathCellsBlocked += CountPathCellsInPatch(grid, cells);
                break;
            }
        }
    }

    private static bool IsPathCrossingPatchValid(
        char[,] grid,
        List<Vector2Int> cells,
        int width,
        int height)
    {
        bool hasPathCell = false;

        foreach (Vector2Int cell in cells)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
            {
                return false;
            }

            char current = grid[cell.y, cell.x];
            if (current == 'P' || current == 'p')
            {
                hasPathCell = true;
                continue;
            }

            if (current != 'G' && current != 'g')
            {
                return false;
            }
        }

        return hasPathCell;
    }

    private static int CountPathCellsInPatch(char[,] grid, List<Vector2Int> cells)
    {
        int count = 0;
        foreach (Vector2Int cell in cells)
        {
            char current = grid[cell.y, cell.x];
            if (current == 'P' || current == 'p')
            {
                count++;
            }
        }

        return count;
    }

    private static int PickLakeAnchorX(int minX, int maxX, float mapBThemeAtRight)
    {
        if (maxX <= minX)
        {
            return minX;
        }

        // Field3(오른쪽) 쪽 배치 비중 — mapBThemeAtRight ↑ → anchor X ↑
        float power = Mathf.Lerp(1.15f, 0.2f, Mathf.Clamp01(mapBThemeAtRight));
        float t = Mathf.Pow(Random.value, power);
        return minX + Mathf.RoundToInt(t * (maxX - minX));
    }

    private static bool TryPlaceLakePatch(
        char[,] grid,
        Vector2Int start,
        Vector2Int goal,
        int width,
        int height,
        BridgeDifficultyPreset preset,
        List<Vector2Int> optimalPath,
        bool[,] lakeMask,
        ref LakePlacementReport report)
    {
        int patchSize = PickPatchSize(preset);
        int patchWidth = patchSize;
        int patchHeight = patchSize;

        int minX = Mathf.RoundToInt(width * preset.lakeZoneStart);
        int maxX = Mathf.RoundToInt(width * preset.lakeZoneEnd) - patchWidth;
        minX = Mathf.Clamp(minX, 1, width - patchWidth - 1);
        maxX = Mathf.Clamp(maxX, minX, width - patchWidth - 1);

        for (int attempt = 0; attempt < 256; attempt++)
        {
            int anchorX = PickLakeAnchorX(minX, maxX, preset.mapBThemeAtRight);
            int maxY = Mathf.Max(2, height - patchHeight + 1);
            int anchorY = Random.Range(1, maxY);
            List<Vector2Int> cells = BuildPatchCells(anchorX, anchorY, patchWidth, patchHeight);

            if (!IsPatchValid(grid, cells, preset, optimalPath, width, height))
            {
                continue;
            }

            if (!TryApplyPatch(grid, start, goal, cells, lakeMask, allowPathCells: false))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsPatchValid(
        char[,] grid,
        List<Vector2Int> cells,
        BridgeDifficultyPreset preset,
        List<Vector2Int> optimalPath,
        int width,
        int height)
    {
        Dictionary<Vector2Int, int> distanceFromPath =
            BridgePathfinding.BuildDistanceFromPath(optimalPath, width, height);

        foreach (Vector2Int cell in cells)
        {
            char current = grid[cell.y, cell.x];
            if (current != 'G' && current != 'g')
            {
                return false;
            }

            if (current == '#' || current == 'S' || current == 'E')
            {
                return false;
            }

            if (!distanceFromPath.TryGetValue(cell, out int distance))
            {
                distance = int.MaxValue;
            }

            if (distance < preset.lakeMinDistanceFromPath)
            {
                return false;
            }

            if (preset.lakeMaxDistanceFromPath > 0 && distance > preset.lakeMaxDistanceFromPath)
            {
                return false;
            }
        }

        return true;
    }

    private static int TryFallbackLakePatch(
        char[,] grid,
        Vector2Int start,
        Vector2Int goal,
        int width,
        int height,
        BridgeDifficultyPreset preset,
        bool[,] lakeMask)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        int minX = Mathf.RoundToInt(width * preset.lakeZoneStart);
        int maxX = Mathf.RoundToInt(width * preset.lakeZoneEnd);

        for (int y = 1; y < height - 2; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                if (grid[y, x] != 'G')
                {
                    continue;
                }

                candidates.Add(new Vector2Int(x, y));
            }
        }

        Shuffle(candidates);
        int[] patchSizes = { 12, 10, 8, 6 };
        foreach (int patchSize in patchSizes)
        {
            foreach (Vector2Int anchor in candidates)
            {
                if (anchor.x + patchSize >= width || anchor.y + patchSize >= height)
                {
                    continue;
                }

                List<Vector2Int> cells = BuildPatchCells(anchor.x, anchor.y, patchSize, patchSize);
                if (!IsPureGrassPatch(grid, cells))
                {
                    continue;
                }

                if (TryApplyPatch(grid, start, goal, cells, lakeMask, allowPathCells: false))
                {
                    Debug.LogWarning($"BridgeLakePlacer: fallback {patchSize}x{patchSize} grass lake 배치.");
                    return 1;
                }
            }
        }

        return 0;
    }

    private static bool TryForceMinimumGrassLake(
        char[,] grid,
        Vector2Int start,
        Vector2Int goal,
        int width,
        int height,
        BridgeDifficultyPreset preset,
        bool[,] lakeMask)
    {
        List<Vector2Int> anchors = new List<Vector2Int>();
        int minX = Mathf.RoundToInt(width * preset.lakeZoneStart);
        int maxX = Mathf.RoundToInt(width * preset.lakeZoneEnd);

        for (int y = 1; y < height - 5; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                anchors.Add(new Vector2Int(x, y));
            }
        }

        anchors.Sort((a, b) => ScoreGrassLakeAnchor(b, grid, lakeMask, width, height, preset)
            .CompareTo(ScoreGrassLakeAnchor(a, grid, lakeMask, width, height, preset)));
        Shuffle(anchors);

        int[] patchSizes = { 12, 10, 8, 6 };
        foreach (int patchSize in patchSizes)
        {
            foreach (Vector2Int anchor in anchors)
            {
                if (anchor.x + patchSize >= width || anchor.y + patchSize >= height)
                {
                    continue;
                }

                List<Vector2Int> cells = BuildPatchCells(anchor.x, anchor.y, patchSize, patchSize);
                if (!IsPureGrassPatch(grid, cells) || PatchOverlapsLake(cells, lakeMask))
                {
                    continue;
                }

                if (TryApplyPatch(grid, start, goal, cells, lakeMask, allowPathCells: false))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPureGrassPatch(char[,] grid, List<Vector2Int> cells)
    {
        foreach (Vector2Int cell in cells)
        {
            char current = grid[cell.y, cell.x];
            if (current != 'G' && current != 'g')
            {
                return false;
            }
        }

        return true;
    }

    private static bool PatchOverlapsLake(List<Vector2Int> cells, bool[,] lakeMask)
    {
        foreach (Vector2Int cell in cells)
        {
            if (lakeMask[cell.y, cell.x])
            {
                return true;
            }
        }

        return false;
    }

    private static int ScoreGrassLakeAnchor(
        Vector2Int anchor,
        char[,] grid,
        bool[,] lakeMask,
        int width,
        int height,
        BridgeDifficultyPreset preset)
    {
        int score = 0;
        List<Vector2Int> cells = BuildPatchCells(anchor.x, anchor.y, 3, 3);
        foreach (Vector2Int cell in cells)
        {
            if (cell.x <= 0 || cell.y <= 0 || cell.x >= width - 1 || cell.y >= height - 1)
            {
                score -= 4;
                continue;
            }

            if (grid[cell.y, cell.x] == 'G')
            {
                score += 2;
            }

            if (lakeMask[cell.y, cell.x])
            {
                score -= 8;
            }
        }

        float xRatio = width <= 1 ? 0.5f : anchor.x / (float)(width - 1);
        score += Mathf.RoundToInt(xRatio * 12f * Mathf.Clamp01(preset.mapBThemeAtRight));

        return score;
    }

    private static bool HasInteriorLakeCell(bool[,] lakeMask, int width, int height)
    {
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (!lakeMask[y, x])
                {
                    continue;
                }

                if (lakeMask[y + 1, x] && lakeMask[y - 1, x] && lakeMask[y, x + 1] && lakeMask[y, x - 1])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryApplyPatch(
        char[,] grid,
        Vector2Int start,
        Vector2Int goal,
        List<Vector2Int> cells,
        bool[,] lakeMask,
        bool allowPathCells)
    {
        List<(Vector2Int cell, char previous)> backups = new List<(Vector2Int, char)>();

        foreach (Vector2Int cell in cells)
        {
            char current = grid[cell.y, cell.x];
            if (!IsLakeEligibleCell(current, allowPathCells))
            {
                return false;
            }

            if (cell == start || cell == goal)
            {
                return false;
            }

            backups.Add((cell, current));
        }

        foreach (Vector2Int cell in cells)
        {
            lakeMask[cell.y, cell.x] = true;
            grid[cell.y, cell.x] = LakeBlockChar;
        }

        if (!BridgePathfinding.HasRoute(grid, start, goal))
        {
            foreach ((Vector2Int cell, char previous) in backups)
            {
                grid[cell.y, cell.x] = previous;
                lakeMask[cell.y, cell.x] = false;
            }

            return false;
        }

        return true;
    }

    private static bool IsLakeEligibleCell(char cell, bool allowPathCells)
    {
        if (cell == 'G' || cell == 'g')
        {
            return true;
        }

        return allowPathCells && (cell == 'P' || cell == 'p');
    }

    // 단일 칸 lake는 autotile이 깨져서 patch 단위만 사용

    private static void ApplyLakeAutotileIndices(char[,] grid, bool[,] lakeMask, int[,] lakeAutotileIndices)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!lakeMask[y, x])
                {
                    continue;
                }

                int index = ComputeAutotileIndex(lakeMask, x, y, width, height);
                lakeAutotileIndices[y, x] = index;
                grid[y, x] = LakeBlockChar;
            }
        }
    }

    private static List<Vector2Int> BuildPatchCells(int anchorX, int anchorY, int patchWidth, int patchHeight)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int y = 0; y < patchHeight; y++)
        {
            for (int x = 0; x < patchWidth; x++)
            {
                cells.Add(new Vector2Int(anchorX + x, anchorY + y));
            }
        }

        return cells;
    }

    private static bool IsLakeAt(bool[,] lakeMask, int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return false;
        }

        return lakeMask[y, x];
    }

    private static void ClearLakeIndices(int[,] lakeAutotileIndices)
    {
        int height = lakeAutotileIndices.GetLength(0);
        int width = lakeAutotileIndices.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                lakeAutotileIndices[y, x] = -1;
            }
        }
    }

    private static int CountLakeCells(int[,] lakeAutotileIndices)
    {
        int count = 0;
        int height = lakeAutotileIndices.GetLength(0);
        int width = lakeAutotileIndices.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (lakeAutotileIndices[y, x] >= 0)
                {
                    count++;
                }
            }
        }

        return count;
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
