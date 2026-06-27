using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BSP/Path 생성 후 브릿지 grid의 ground(G) 타일에 lake('w')를 비율 기반으로 랜덤 배치합니다.
/// Field 1 / Field 3의 lake 비율을 읽어 좌→우로 보간할 수 있습니다.
/// </summary>
public static class BridgeLakeRandomPlacer
{
    public struct Settings
    {
        public bool enabled;
        public float groundLakeChanceLeft;
        public float groundLakeChanceRight;
        public float maxLakeCoverage;
        public int minDistanceFromPath;
        public int patchRadiusMin;
        public int patchRadiusMax;
        public bool useFieldLakeRatios;
        public int randomSeed;
    }

    public static Settings CreateDefault()
    {
        return new Settings
        {
            enabled = true,
            groundLakeChanceLeft = 0.06f,
            groundLakeChanceRight = 0.14f,
            maxLakeCoverage = 0.12f,
            minDistanceFromPath = 0,
            patchRadiusMin = 1,
            patchRadiusMax = 2,
            useFieldLakeRatios = true,
            randomSeed = 0
        };
    }

    /// <summary>
    /// Field visual matrix에서 lake(w) / walkable(g,p,.,w) 비율을 추정합니다.
    /// </summary>
    public static float EstimateLakeRatio(TilemapDataProvider provider)
    {
        if (provider == null)
        {
            return 0f;
        }

        char[,] visual = provider.GetVisualMapMatrix();
        if (visual == null || visual.GetLength(0) == 0 || visual.GetLength(1) == 0)
        {
            return 0f;
        }

        int lakeCells = 0;
        int walkableCells = 0;
        int height = visual.GetLength(0);
        int width = visual.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char cell = visual[y, x];
                if (cell == 'w')
                {
                    lakeCells++;
                    walkableCells++;
                }
                else if (cell == 'g' || cell == 'p' || cell == '.')
                {
                    walkableCells++;
                }
            }
        }

        return walkableCells > 0 ? lakeCells / (float)walkableCells : 0f;
    }

    public static int PlaceLakes(
        char[,] grid,
        TilemapDataProvider mapA,
        TilemapDataProvider mapB,
        Settings settings)
    {
        if (grid == null || !settings.enabled)
        {
            return 0;
        }

        int width = grid.GetLength(1);
        int height = grid.GetLength(0);
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        float chanceLeft = settings.groundLakeChanceLeft;
        float chanceRight = settings.groundLakeChanceRight;
        if (settings.useFieldLakeRatios)
        {
            float ratioA = EstimateLakeRatio(mapA);
            float ratioB = EstimateLakeRatio(mapB);
            if (ratioA > 0f || ratioB > 0f)
            {
                chanceLeft = ratioA > 0f ? ratioA : chanceLeft;
                chanceRight = ratioB > 0f ? ratioB : chanceRight;
            }
        }

        chanceLeft = Mathf.Clamp01(chanceLeft);
        chanceRight = Mathf.Clamp01(chanceRight);
        float maxCoverage = Mathf.Clamp01(settings.maxLakeCoverage > 0f ? settings.maxLakeCoverage : 0.12f);
        chanceLeft = Mathf.Min(chanceLeft, maxCoverage);
        chanceRight = Mathf.Min(chanceRight, maxCoverage);

        Random.State previousState = Random.state;
        Random.InitState(settings.randomSeed != 0 ? settings.randomSeed : unchecked((int)System.DateTime.Now.Ticks));

        bool[,] pathProximity = BuildPathProximity(grid, settings.minDistanceFromPath);
        List<Vector2Int> groundCells = CollectGroundCells(grid, pathProximity);
        Shuffle(groundCells);

        // Field lake ratio / Inspector chance = 목표 커버리지(%).
        // 예전처럼 G 타일마다 확률 굴리면 패치가 겹쳐 맵 전체가 lake가 됩니다.
        float averageTargetRatio = (chanceLeft + chanceRight) * 0.5f;
        int targetLakeCells = Mathf.Max(0, Mathf.RoundToInt(groundCells.Count * averageTargetRatio));

        int placedCells = 0;
        foreach (Vector2Int cell in groundCells)
        {
            if (placedCells >= targetLakeCells)
            {
                break;
            }

            int radius = Random.Range(settings.patchRadiusMin, settings.patchRadiusMax + 1);
            placedCells += StampLakePatch(grid, cell.x, cell.y, radius, pathProximity);
        }

        Random.state = previousState;

        float actualCoverage = groundCells.Count > 0 ? placedCells / (float)groundCells.Count : 0f;
        Debug.Log(
            $"BridgeLakeRandomPlacer: lake {placedCells} cells " +
            $"(target={targetLakeCells}, coverage={actualCoverage:P1}, " +
            $"ratio L={chanceLeft:P1} R={chanceRight:P1}, candidates={groundCells.Count})");
        return placedCells;
    }

    public static int ResolveLakeTileIndex(string[][] tokenRows, int row, int x)
    {
        bool landNorth = !IsLakeTokenAt(tokenRows, row - 1, x);
        bool landSouth = !IsLakeTokenAt(tokenRows, row + 1, x);
        bool landWest = !IsLakeTokenAt(tokenRows, row, x - 1);
        bool landEast = !IsLakeTokenAt(tokenRows, row, x + 1);

        int landCount = 0;
        if (landNorth) landCount++;
        if (landSouth) landCount++;
        if (landWest) landCount++;
        if (landEast) landCount++;

        // 0=L, 1=TL, 2=T, 3=TR, 4=R, 5=BR, 6=B, 7=BL, 8=C
        switch (landCount)
        {
            case 0:
                return 8;
            case 1:
                if (landNorth) return 2;
                if (landSouth) return 6;
                if (landWest) return 0;
                return 4;
            case 2:
                if (landNorth && landSouth) return 8;
                if (landWest && landEast) return 8;
                if (landNorth && landWest) return 1;
                if (landNorth && landEast) return 3;
                if (landSouth && landWest) return 7;
                return 5;
            case 3:
                if (!landNorth) return 2;
                if (!landSouth) return 6;
                if (!landWest) return 0;
                return 4;
            default:
                return 8;
        }
    }

    private static bool IsLakeTokenAt(string[][] tokenRows, int row, int x)
    {
        if (tokenRows == null || row < 0 || row >= tokenRows.Length)
        {
            return false;
        }

        string[] tokens = tokenRows[row];
        if (tokens == null || x < 0 || x >= tokens.Length)
        {
            return false;
        }

        return IsLakeToken(tokens[x]);
    }

    private static bool IsLakeToken(string token)
    {
        if (token == "w")
        {
            return true;
        }

        if (string.IsNullOrEmpty(token) || token.Length != 3 || token[0] != 'w' || token[1] != '_')
        {
            return false;
        }

        return token[2] >= '0' && token[2] <= '8';
    }

    private static bool[,] BuildPathProximity(char[,] grid, int minDistanceFromPath)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        bool[,] nearPath = new bool[height, width];

        if (minDistanceFromPath <= 0)
        {
            return nearPath;
        }

        int radius = minDistanceFromPath;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!BridgeMapJsonUtility.IsPathTile(grid[y, x]))
                {
                    continue;
                }

                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        {
                            continue;
                        }

                        if (Mathf.Abs(dx) + Mathf.Abs(dy) <= radius)
                        {
                            nearPath[ny, nx] = true;
                        }
                    }
                }
            }
        }

        return nearPath;
    }

    private static List<Vector2Int> CollectGroundCells(char[,] grid, bool[,] pathProximity)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char cell = grid[y, x];
                if (cell != 'G' && cell != 'g' && cell != '.')
                {
                    continue;
                }

                if (pathProximity[y, x])
                {
                    continue;
                }

                cells.Add(new Vector2Int(x, y));
            }
        }

        return cells;
    }

    private static int StampLakePatch(char[,] grid, int centerX, int centerY, int radius, bool[,] pathProximity)
    {
        int placed = 0;
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        int effectiveRadius = Mathf.Max(0, radius);

        for (int dy = -effectiveRadius; dy <= effectiveRadius; dy++)
        {
            for (int dx = -effectiveRadius; dx <= effectiveRadius; dx++)
            {
                if (dx * dx + dy * dy > effectiveRadius * effectiveRadius + effectiveRadius)
                {
                    continue;
                }

                int x = centerX + dx;
                int y = centerY + dy;
                if (x < 0 || x >= width || y < 0 || y >= height)
                {
                    continue;
                }

                if (pathProximity[y, x] || !CanBecomeLake(grid[y, x]))
                {
                    continue;
                }

                grid[y, x] = 'w';
                placed++;
            }
        }

        return placed;
    }

    private static bool CanBecomeLake(char cell)
    {
        return cell == 'G' || cell == 'g' || cell == '.';
    }

    private static void Shuffle(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2Int temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
