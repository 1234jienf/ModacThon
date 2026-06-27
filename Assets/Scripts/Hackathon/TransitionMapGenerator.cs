using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class TransitionMapGenerator : MonoBehaviour
{
    [Header("Input Maps")]
    public TilemapDataProvider fromMapProvider;
    public TilemapDataProvider toMapProvider;

    [Header("Transition")]
    [Min(1)]
    public int totalTransitionMaps = 2;
    [Min(1)]
    public int transitionIndex = 1;
    public int randomSeed = 12345;
    public bool generateOnStart = true;

    [Header("Output")]
    public PatternTilemapGenerator outputGenerator;
    [TextArea(8, 20)]
    public string latestGeneratedAscii;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateTransitionMap();
        }
    }

    [ContextMenu("Generate Transition Map")]
    public void GenerateTransitionMap()
    {
        if (fromMapProvider == null || toMapProvider == null)
        {
            Debug.LogError("fromMapProvider와 toMapProvider를 모두 지정해 주세요.");
            return;
        }

        if (outputGenerator == null)
        {
            outputGenerator = FindObjectOfType<PatternTilemapGenerator>();
        }

        char[,] fromMatrix = fromMapProvider.GetMapMatrix();
        char[,] toMatrix = toMapProvider.GetMapMatrix();
        MapProfile fromProfile = MapProfile.Analyze(fromMapProvider.gameObject.name, fromMatrix);
        MapProfile toProfile = MapProfile.Analyze(toMapProvider.gameObject.name, toMatrix);
        float t = transitionIndex / (float)(totalTransitionMaps + 1);
        MapProfile transitionProfile = MapProfile.Lerp(fromProfile, toProfile, t);

        char[,] generated = TransitionAsciiMapBuilder.Generate(transitionProfile, randomSeed + transitionIndex);
        latestGeneratedAscii = ToAsciiString(generated);

        Debug.Log($"[From] {fromProfile}");
        Debug.Log($"[To] {toProfile}");
        Debug.Log($"[Generated] {transitionProfile}\n{latestGeneratedAscii}");

        if (outputGenerator != null)
        {
            outputGenerator.mapPattern = latestGeneratedAscii;
            outputGenerator.Generate();
        }
        else
        {
            Debug.LogWarning("Output Generator가 없어 ASCII만 생성했습니다. PatternTilemapGenerator를 연결하면 Tilemap에도 출력됩니다.");
        }
    }

    private string ToAsciiString(char[,] matrix)
    {
        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);
        StringBuilder sb = new StringBuilder();

        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                sb.Append(matrix[y, x]);
            }

            if (y > 0)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}

public static class TransitionAsciiMapBuilder
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public static char[,] Generate(MapProfile profile, int seed)
    {
        int width = Mathf.Max(10, profile.width);
        int height = Mathf.Max(10, profile.height);
        char[,] map = CreateFilledMap(width, height, '#');
        System.Random random = new System.Random(seed);
        List<RectInt> rooms = GenerateRooms(profile, width, height, random);

        if (rooms.Count == 0)
        {
            rooms.Add(new RectInt(1, 1, Mathf.Max(3, width - 2), Mathf.Max(3, height - 2)));
        }

        foreach (RectInt room in rooms)
        {
            CarveRoom(map, room);
        }

        for (int i = 1; i < rooms.Count; i++)
        {
            CarveCorridor(map, CenterOf(rooms[i - 1]), CenterOf(rooms[i]), random);
        }

        Vector2Int start = CenterOf(rooms[0]);
        Vector2Int goal = CenterOf(rooms[rooms.Count - 1]);
        map[start.y, start.x] = 'S';
        map[goal.y, goal.x] = 'G';

        if (!HasPath(map, start, goal))
        {
            CarveCorridor(map, start, goal, random);
            map[start.y, start.x] = 'S';
            map[goal.y, goal.x] = 'G';
        }

        PlaceObjects(map, 'O', Mathf.RoundToInt(width * height * profile.obstacleRatio), random);
        PlaceObjects(map, 'E', Mathf.RoundToInt(Mathf.Lerp(1, 8, profile.difficulty)), random);

        return map;
    }

    private static char[,] CreateFilledMap(int width, int height, char cell)
    {
        char[,] map = new char[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                map[y, x] = cell;
            }
        }

        return map;
    }

    private static List<RectInt> GenerateRooms(MapProfile profile, int width, int height, System.Random random)
    {
        List<RectInt> rooms = new List<RectInt>();
        int roomCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4, 10, 1f - profile.wallRatio)), 3, 12);
        int maxRoomWidth = Mathf.Max(4, width / 3);
        int maxRoomHeight = Mathf.Max(4, height / 3);

        for (int i = 0; i < roomCount * 4 && rooms.Count < roomCount; i++)
        {
            int roomWidth = random.Next(3, maxRoomWidth + 1);
            int roomHeight = random.Next(3, maxRoomHeight + 1);
            int x = random.Next(1, Mathf.Max(2, width - roomWidth - 1));
            int y = random.Next(1, Mathf.Max(2, height - roomHeight - 1));
            RectInt candidate = new RectInt(x, y, roomWidth, roomHeight);

            bool overlaps = false;
            foreach (RectInt room in rooms)
            {
                if (OverlapsWithMargin(candidate, room, 1))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                rooms.Add(candidate);
            }
        }

        rooms.Sort((a, b) => CenterOf(a).x.CompareTo(CenterOf(b).x));
        return rooms;
    }

    private static void CarveRoom(char[,] map, RectInt room)
    {
        for (int y = room.yMin; y < room.yMax; y++)
        {
            for (int x = room.xMin; x < room.xMax; x++)
            {
                if (InBounds(map, x, y))
                {
                    map[y, x] = '.';
                }
            }
        }
    }

    private static void CarveCorridor(char[,] map, Vector2Int from, Vector2Int to, System.Random random)
    {
        Vector2Int current = from;
        bool horizontalFirst = random.NextDouble() < 0.5;

        if (horizontalFirst)
        {
            CarveHorizontal(map, current.x, to.x, current.y);
            current.x = to.x;
            CarveVertical(map, current.y, to.y, current.x);
        }
        else
        {
            CarveVertical(map, current.y, to.y, current.x);
            current.y = to.y;
            CarveHorizontal(map, current.x, to.x, current.y);
        }
    }

    private static void CarveHorizontal(char[,] map, int fromX, int toX, int y)
    {
        int min = Mathf.Min(fromX, toX);
        int max = Mathf.Max(fromX, toX);
        for (int x = min; x <= max; x++)
        {
            if (InBounds(map, x, y))
            {
                map[y, x] = '.';
            }
        }
    }

    private static void CarveVertical(char[,] map, int fromY, int toY, int x)
    {
        int min = Mathf.Min(fromY, toY);
        int max = Mathf.Max(fromY, toY);
        for (int y = min; y <= max; y++)
        {
            if (InBounds(map, x, y))
            {
                map[y, x] = '.';
            }
        }
    }

    private static void PlaceObjects(char[,] map, char marker, int count, System.Random random)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        int placed = 0;
        int attempts = 0;

        while (placed < count && attempts < count * 20)
        {
            attempts++;
            int x = random.Next(1, width - 1);
            int y = random.Next(1, height - 1);
            if (map[y, x] != '.')
            {
                continue;
            }

            map[y, x] = marker;
            placed++;
        }
    }

    private static bool HasPath(char[,] map, Vector2Int start, Vector2Int goal)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        bool[,] visited = new bool[height, width];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        visited[start.y, start.x] = true;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == goal)
            {
                return true;
            }

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;
                if (!InBounds(map, next.x, next.y) || visited[next.y, next.x])
                {
                    continue;
                }

                char cell = map[next.y, next.x];
                if (cell == '#')
                {
                    continue;
                }

                visited[next.y, next.x] = true;
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static Vector2Int CenterOf(RectInt rect)
    {
        return new Vector2Int(rect.x + rect.width / 2, rect.y + rect.height / 2);
    }

    private static bool InBounds(char[,] map, int x, int y)
    {
        return x >= 0 && x < map.GetLength(1) && y >= 0 && y < map.GetLength(0);
    }

    private static bool OverlapsWithMargin(RectInt a, RectInt b, int margin)
    {
        return a.xMin - margin < b.xMax
            && a.xMax + margin > b.xMin
            && a.yMin - margin < b.yMax
            && a.yMax + margin > b.yMin;
    }
}
