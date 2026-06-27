using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[System.Serializable]
public class InputMapData
{
    public int width;
    public int height;
    public int startX;
    public int startY;
    public List<string> mapGrid = new List<string>();
}

/// <summary>
/// Bridge BSP JSON: mapGrid row 0 = 월드 상단(북쪽), 마지막 row = 월드 하단.
/// 내부 char[,] 인덱스는 gridY=0 이 startY(남쪽) 기준.
/// </summary>
public static class BridgeMapJsonUtility
{
    public static void WriteGridRowsTopFirst(List<string> mapGrid, char[,] grid)
    {
        mapGrid.Clear();
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);

        for (int gridY = height - 1; gridY >= 0; gridY--)
        {
            StringBuilder row = new StringBuilder(width);
            for (int x = 0; x < width; x++)
            {
                row.Append(grid[gridY, x]);
            }

            mapGrid.Add(row.ToString());
        }
    }

    public static char[,] LoadGridFromJson(InputMapData data)
    {
        int width = data.width;
        int height = data.height;
        char[,] grid = new char[height, width];

        for (int row = 0; row < data.mapGrid.Count && row < height; row++)
        {
            int gridY = height - 1 - row;
            string line = data.mapGrid[row] ?? string.Empty;
            for (int x = 0; x < width && x < line.Length; x++)
            {
                grid[gridY, x] = line[x];
            }
        }

        return grid;
    }

    public static bool TryLoadFromFile(string relativePath, out InputMapData data, out char[,] grid)
    {
        data = null;
        grid = null;

        string fullPath = GetProjectRelativePath(relativePath);
        if (!File.Exists(fullPath))
        {
            return false;
        }

        string json = File.ReadAllText(fullPath);
        data = JsonUtility.FromJson<InputMapData>(json);
        if (data == null || data.mapGrid == null || data.mapGrid.Count == 0)
        {
            return false;
        }

        grid = LoadGridFromJson(data);
        return true;
    }

    public static Vector3 GridCellToWorld(InputMapData data, int gridX, int gridY)
    {
        return new Vector3(data.startX + gridX + 0.5f, data.startY + gridY + 0.5f, 0f);
    }

    public static bool TryFindBridgeEndpoints(char[,] grid, out Vector2Int start, out Vector2Int goal)
    {
        start = default;
        goal = default;

        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        int minPathX = width;
        int maxPathX = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsPathTile(grid[y, x]))
                {
                    continue;
                }

                minPathX = Mathf.Min(minPathX, x);
                maxPathX = Mathf.Max(maxPathX, x);
            }
        }

        if (minPathX >= width || maxPathX < 0)
        {
            return false;
        }

        if (TryFindMarker(grid, 'S', out start))
        {
            // marked start
        }
        else
        {
            start = PickEdgePathCell(grid, minPathX);
        }

        if (TryFindMarker(grid, 'E', out goal))
        {
            return start != goal;
        }

        goal = PickEdgePathCell(grid, maxPathX);
        return start != goal && IsPathTile(grid[start.y, start.x]) && IsPathTile(grid[goal.y, goal.x]);
    }

    public static void MarkBridgeEndpoints(char[,] grid)
    {
        if (!TryFindBridgeEndpoints(grid, out Vector2Int start, out Vector2Int goal))
        {
            return;
        }

        grid[start.y, start.x] = 'S';
        grid[goal.y, goal.x] = 'E';
    }

    private static Vector2Int PickEdgePathCell(char[,] grid, int edgeX)
    {
        int height = grid.GetLength(0);
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int y = 0; y < height; y++)
        {
            if (IsPathTile(grid[y, edgeX]))
            {
                candidates.Add(new Vector2Int(edgeX, y));
            }
        }

        if (candidates.Count == 0)
        {
            return default;
        }

        candidates.Sort((a, b) => a.y.CompareTo(b.y));
        return candidates[candidates.Count / 2];
    }

    private static bool TryFindMarker(char[,] grid, char marker, out Vector2Int position)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (grid[y, x] == marker)
                {
                    position = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        position = default;
        return false;
    }

    public static bool IsPathTile(char cell)
    {
        return cell == 'P' || cell == 'p' || cell == 'S' || cell == 'E';
    }

    public static string GetProjectRelativePath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, relativePath);
    }
}
