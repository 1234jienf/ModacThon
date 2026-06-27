using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapProfile
{
    public string mapName;
    public int width;
    public int height;
    [Range(0f, 1f)]
    public float wallRatio;
    [Range(0f, 1f)]
    public float obstacleRatio;
    public int pathLength;
    [Range(0f, 1f)]
    public float difficulty;

    public static MapProfile Analyze(string mapName, char[,] matrix)
    {
        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);
        int total = Mathf.Max(1, width * height);
        int walls = 0;
        int obstacles = 0;
        Vector2Int start = new Vector2Int(1, 1);
        Vector2Int goal = new Vector2Int(Mathf.Max(1, width - 2), Mathf.Max(1, height - 2));
        bool hasStart = false;
        bool hasGoal = false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char cell = NormalizeCell(matrix[y, x]);
                if (cell == '#')
                {
                    walls++;
                }
                else if (cell == 'O')
                {
                    obstacles++;
                }
                else if (cell == 'S')
                {
                    start = new Vector2Int(x, y);
                    hasStart = true;
                }
                else if (cell == 'G')
                {
                    goal = new Vector2Int(x, y);
                    hasGoal = true;
                }
            }
        }

        int pathLength = hasStart && hasGoal ? FindShortestPathLength(matrix, start, goal) : 0;
        float wallRatio = walls / (float)total;
        float obstacleRatio = obstacles / (float)total;

        return new MapProfile
        {
            mapName = mapName,
            width = width,
            height = height,
            wallRatio = wallRatio,
            obstacleRatio = obstacleRatio,
            pathLength = pathLength,
            difficulty = Mathf.Clamp01(wallRatio * 0.7f + obstacleRatio * 1.5f + Mathf.Clamp01(pathLength / 200f) * 0.3f)
        };
    }

    public static MapProfile Lerp(MapProfile from, MapProfile to, float t)
    {
        t = Mathf.Clamp01(t);
        return new MapProfile
        {
            mapName = $"Transition {t:0.00}",
            width = Mathf.RoundToInt(Mathf.Lerp(from.width, to.width, t)),
            height = Mathf.RoundToInt(Mathf.Lerp(from.height, to.height, t)),
            wallRatio = Mathf.Lerp(from.wallRatio, to.wallRatio, t),
            obstacleRatio = Mathf.Lerp(from.obstacleRatio, to.obstacleRatio, t),
            pathLength = Mathf.RoundToInt(Mathf.Lerp(from.pathLength, to.pathLength, t)),
            difficulty = Mathf.Lerp(from.difficulty, to.difficulty, t)
        };
    }

    public override string ToString()
    {
        return $"{mapName} size={width}x{height}, wall={wallRatio:0.00}, obstacle={obstacleRatio:0.00}, path={pathLength}, difficulty={difficulty:0.00}";
    }

    public static char NormalizeCell(char cell)
    {
        switch (cell)
        {
            case '■':
            case '#':
                return '#';
            case '□':
            case '.':
                return '.';
            case '▩':
            case 'O':
                return 'O';
            case 'Ｓ':
            case 'S':
                return 'S';
            case 'Ｇ':
            case 'G':
                return 'G';
            default:
                return cell;
        }
    }

    private static int FindShortestPathLength(char[,] matrix, Vector2Int start, Vector2Int goal)
    {
        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);
        bool[,] visited = new bool[height, width];
        Queue<Vector2Int> points = new Queue<Vector2Int>();
        Queue<int> distances = new Queue<int>();
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        points.Enqueue(start);
        distances.Enqueue(0);
        visited[start.y, start.x] = true;

        while (points.Count > 0)
        {
            Vector2Int point = points.Dequeue();
            int distance = distances.Dequeue();

            if (point == goal)
            {
                return distance;
            }

            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = point + direction;
                if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height || visited[next.y, next.x])
                {
                    continue;
                }

                char cell = NormalizeCell(matrix[next.y, next.x]);
                if (cell == '#' || cell == 'O' || cell == ' ')
                {
                    continue;
                }

                visited[next.y, next.x] = true;
                points.Enqueue(next);
                distances.Enqueue(distance + 1);
            }
        }

        return 0;
    }
}
