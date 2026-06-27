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
        WriteTokenGridRowsTopFirst(mapGrid, grid, null);
    }

    public static void WriteTokenGridRowsTopFirst(List<string> mapGrid, char[,] grid, int[,] lakeAutotileIndices)
    {
        mapGrid.Clear();
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);

        for (int gridY = height - 1; gridY >= 0; gridY--)
        {
            StringBuilder row = new StringBuilder();
            for (int x = 0; x < width; x++)
            {
                string token = GetExportToken(grid[gridY, x], gridY, x, lakeAutotileIndices);
                row.Append(token);
            }

            mapGrid.Add(row.ToString());
        }
    }

    public static string GetExportToken(char cell, int y, int x, int[,] lakeAutotileIndices)
    {
        if (lakeAutotileIndices != null &&
            y >= 0 && x >= 0 &&
            y < lakeAutotileIndices.GetLength(0) &&
            x < lakeAutotileIndices.GetLength(1))
        {
            int lakeIndex = lakeAutotileIndices[y, x];
            if (lakeIndex >= 0 && lakeIndex <= 8)
            {
                return $"w_{lakeIndex}";
            }
        }

        return cell.ToString();
    }

    public static char[,] LoadGridFromJson(InputMapData data)
    {
        int width = data.width;
        int height = data.height;
        char[,] grid = new char[height, width];

        for (int row = 0; row < data.mapGrid.Count && row < height; row++)
        {
            int gridY = height - 1 - row;
            string[] tokens = TokenizeMapRow(data.mapGrid[row]);
            for (int x = 0; x < width && x < tokens.Length; x++)
            {
                grid[gridY, x] = TokenToPathCell(tokens[x]);
            }
        }

        return grid;
    }

    public static int[,] LoadLakeAutotileIndicesFromJson(InputMapData data)
    {
        int width = data.width;
        int height = data.height;
        int[,] lakeIndices = new int[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                lakeIndices[y, x] = -1;
            }
        }

        for (int row = 0; row < data.mapGrid.Count && row < height; row++)
        {
            int gridY = height - 1 - row;
            string[] tokens = TokenizeMapRow(data.mapGrid[row]);
            for (int x = 0; x < width && x < tokens.Length; x++)
            {
                if (TryParseLakeToken(tokens[x], out int lakeIndex))
                {
                    lakeIndices[gridY, x] = lakeIndex;
                }
            }
        }

        return lakeIndices;
    }

    public static bool TryParseLakeToken(string token, out int lakeIndex)
    {
        lakeIndex = -1;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        if (token.Length == 1 && token[0] == 'w')
        {
            lakeIndex = 8;
            return true;
        }

        if (!token.StartsWith("w_", System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(token.Substring(2), out lakeIndex))
        {
            return false;
        }

        return lakeIndex >= 0 && lakeIndex <= 8;
    }

    private static char TokenToPathCell(string token)
    {
        if (TryParseLakeToken(token, out _))
        {
            return BridgeLakePlacer.LakeBlockChar;
        }

        if (string.IsNullOrEmpty(token))
        {
            return '.';
        }

        return token[0];
    }

    private static string[] TokenizeMapRow(string row)
    {
        if (string.IsNullOrEmpty(row))
        {
            return new string[0];
        }

        if (row.Contains(","))
        {
            return row.Split(',');
        }

        List<string> tokens = new List<string>();
        int index = 0;
        while (index < row.Length)
        {
            string token = ReadMapToken(row, ref index);
            if (!string.IsNullOrEmpty(token))
            {
                tokens.Add(token);
            }
        }

        return tokens.ToArray();
    }

    private static string ReadMapToken(string row, ref int startIndex)
    {
        string[] knownTokens =
        {
            "d2_0", "d2_1", "d2_2", "d2_3", "d2_4", "d2_5", "d2_6",
            "g_0", "g_1", "g_2", "g_3", "g_4", "g_5", "g_6",
            "d_0", "d_1", "d_2", "d_3", "d_4", "d_5", "d_6",
            "s_0", "s_1", "s_2", "s_3", "s_4", "s_5", "s_6",
            "w_0", "w_1", "w_2", "w_3", "w_4", "w_5", "w_6", "w_7", "w_8"
        };

        foreach (string token in knownTokens)
        {
            if (startIndex + token.Length <= row.Length &&
                row.Substring(startIndex, token.Length) == token)
            {
                startIndex += token.Length;
                return token;
            }
        }

        string single = row[startIndex].ToString();
        startIndex++;
        return single;
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

    public static bool IsBlockedCell(char cell)
    {
        cell = MapProfile.NormalizeCell(cell);
        return cell == '#' || cell == 'w' || cell == 'W' || cell == 'O';
    }

    public static string GetProjectRelativePath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, relativePath);
    }
}
