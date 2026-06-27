using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class AsciiMapPathRepairer : MonoBehaviour
{
    [Header("File IO")]
    public string inputRelativePath = "tmpOutput/BridgeMapData.json";
    public string outputRelativePath = "tmpOutput/RepairedBridgeMapData.json";
    public bool preferFileInput = true;

    [Header("Input JSON")]
    [TextArea(12, 40)]
    public string inputJsonMap =
@"{
    ""width"": 20,
    ""height"": 9,
    ""startX"": 0,
    ""startY"": 0,
    ""mapGrid"": [
        ""####################"",
        ""#S.....########....#"",
        ""###.####....########"",
        ""#......E...........#"",
        ""#.##################"",
        ""#............E.....#"",
        ""####################"",
        ""#....E........EEEEG#"",
        ""####################""
    ]
}";

    public List<AsciiMapData> inputMaps = new List<AsciiMapData>();

    [Header("Repair Rules")]
    public bool preserveExistingStartGoal = true;
    public bool forceLeftToRightEndpoints = false;
    public bool treatEnemiesAsPassable = true;
    public bool connectDisconnectedAreas = true;

    [Header("Output")]
    public List<AsciiMapData> repairedMaps = new List<AsciiMapData>();
    public List<string> repairedAsciiMaps = new List<string>();
    public List<string> validationReports = new List<string>();

    [TextArea(12, 40)]
    public string repairedJson;
    [TextArea(12, 40)]
    public string repairedAscii;

    private const char Wall = '#';
    private const char Floor = '.';
    private const char Start = 'S';
    private const char Goal = 'G';
    private const char Enemy = 'E';

    [ContextMenu("Validate And Repair JSON Map")]
    public void ValidateAndRepairJsonMapFromInspector()
    {
        string sourceJson = inputJsonMap;
        string inputPath = GetProjectRelativePath(inputRelativePath);

        if (preferFileInput && File.Exists(inputPath))
        {
            sourceJson = File.ReadAllText(inputPath);
            inputJsonMap = sourceJson;
        }
        else if (preferFileInput)
        {
            Debug.LogWarning($"Input file not found. Falling back to Input Json Map: {inputPath}");
        }

        List<AsciiMapData> sourceMaps = inputMaps.Count > 0
            ? CloneMapDataList(inputMaps)
            : ParseInputMaps(sourceJson);

        repairedMaps = ValidateAndRepairMaps(sourceMaps, out validationReports);
        repairedAsciiMaps = new List<string>();

        foreach (AsciiMapData map in repairedMaps)
            repairedAsciiMaps.Add(ToAscii(map.mapGrid));

        repairedAscii = JoinAsciiOutputs(repairedAsciiMaps);
        repairedJson = ToJsonOutput(repairedMaps);
        SaveRepairedJson(repairedJson);

        StringBuilder log = new StringBuilder();
        log.AppendLine("=== ASCII Map Path Repair ===");
        log.AppendLine($"Input: {inputPath}");
        log.AppendLine($"Output: {GetProjectRelativePath(outputRelativePath)}");
        foreach (string report in validationReports)
            log.AppendLine(report);
        log.AppendLine();
        log.AppendLine(repairedAscii);
        log.AppendLine();
        log.AppendLine("=== Repaired JSON ===");
        log.AppendLine(repairedJson);
        Debug.Log(log.ToString());
    }

    private void SaveRepairedJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("No repaired JSON was generated. Output file was not written.");
            return;
        }

        string outputPath = GetProjectRelativePath(outputRelativePath);
        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    private static string GetProjectRelativePath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
    }

    public List<AsciiMapData> ValidateAndRepairMaps(List<AsciiMapData> maps, out List<string> reports)
    {
        reports = new List<string>();
        List<AsciiMapData> results = new List<AsciiMapData>();

        if (maps == null || maps.Count == 0)
        {
            reports.Add("No input maps.");
            return results;
        }

        for (int i = 0; i < maps.Count; i++)
        {
            AsciiMapData source = CloneMapData(maps[i]);
            char[,] grid = ParseGrid(source);
            RepairResult result = RepairSingleMap(grid, i);
            AsciiMapData repaired = BuildMapData(source, result.grid);

            results.Add(repaired);
            reports.Add(result.report);
        }

        return results;
    }

    public bool HasValidPath(AsciiMapData map)
    {
        char[,] grid = ParseGrid(map);
        Vector2Int start;
        Vector2Int goal;
        if (!TryFindMarker(grid, Start, out start) || !TryFindMarker(grid, Goal, out goal))
            return false;

        return FindPath(grid, start, goal, false, treatEnemiesAsPassable) != null;
    }

    private RepairResult RepairSingleMap(char[,] grid, int mapIndex)
    {
        int areaCountBefore = CountConnectedAreas(grid, treatEnemiesAsPassable);

        bool hadStart = TryFindMarker(grid, Start, out Vector2Int originalStart);
        bool hadGoal = TryFindMarker(grid, Goal, out Vector2Int originalGoal);
        bool hadPath = hadStart && hadGoal && FindPath(grid, originalStart, originalGoal, false, treatEnemiesAsPassable) != null;

        ClearMarkers(grid, Start);
        ClearMarkers(grid, Goal);

        Vector2Int start = SelectStart(grid, hadStart, originalStart);
        Vector2Int goal = SelectGoal(grid, hadGoal, originalGoal);

        grid[start.y, start.x] = Floor;
        grid[goal.y, goal.x] = Floor;

        List<Vector2Int> path = FindPath(grid, start, goal, false, treatEnemiesAsPassable);
        bool dugWalls = false;

        if (path == null && connectDisconnectedAreas)
        {
            path = FindPath(grid, start, goal, true, treatEnemiesAsPassable);
            dugWalls = path != null;
        }

        if (path == null && connectDisconnectedAreas)
        {
            path = CreateFallbackLPath(start, goal);
            dugWalls = true;
        }

        if (path != null)
        {
            foreach (Vector2Int pos in path)
            {
                if (grid[pos.y, pos.x] == Wall)
                    dugWalls = true;

                grid[pos.y, pos.x] = Floor;
            }
        }

        grid[start.y, start.x] = Start;
        grid[goal.y, goal.x] = Goal;

        bool finalPath = FindPath(grid, start, goal, false, treatEnemiesAsPassable) != null;
        int areaCountAfter = CountConnectedAreas(grid, treatEnemiesAsPassable);
        bool endpointsChanged = (hadStart && start != originalStart) || (hadGoal && goal != originalGoal);
        bool repaired = !hadStart || !hadGoal || !hadPath || dugWalls || endpointsChanged;

        return new RepairResult
        {
            grid = grid,
            report = BuildReport(
                mapIndex,
                hadStart,
                hadGoal,
                hadPath,
                repaired,
                finalPath,
                start,
                goal,
                dugWalls,
                areaCountBefore,
                areaCountAfter)
        };
    }

    private Vector2Int SelectStart(char[,] grid, bool hadStart, Vector2Int originalStart)
    {
        int height = grid.GetLength(0);
        if (preserveExistingStartGoal && hadStart && !forceLeftToRightEndpoints)
            return ClampInside(grid, originalStart);

        int preferredRow = hadStart ? originalStart.y : height / 2;
        return new Vector2Int(1, FindNearestUsableRow(grid, 1, preferredRow));
    }

    private Vector2Int SelectGoal(char[,] grid, bool hadGoal, Vector2Int originalGoal)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        if (preserveExistingStartGoal && hadGoal && !forceLeftToRightEndpoints)
            return ClampInside(grid, originalGoal);

        int preferredRow = hadGoal ? originalGoal.y : height / 2;
        return new Vector2Int(width - 2, FindNearestUsableRow(grid, width - 2, preferredRow));
    }

    private static Vector2Int ClampInside(char[,] grid, Vector2Int position)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        return new Vector2Int(
            Mathf.Clamp(position.x, 1, width - 2),
            Mathf.Clamp(position.y, 1, height - 2));
    }

    private static string BuildReport(
        int mapIndex,
        bool hadStart,
        bool hadGoal,
        bool hadPath,
        bool repaired,
        bool finalPath,
        Vector2Int start,
        Vector2Int goal,
        bool dugWalls,
        int areaCountBefore,
        int areaCountAfter)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append($"Map {mapIndex + 1}: ");
        builder.Append(finalPath ? "valid" : "invalid");
        builder.Append(repaired ? ", repaired" : ", unchanged");
        builder.Append($", areas {areaCountBefore}->{areaCountAfter}");

        if (!hadStart)
            builder.Append(", missing S fixed");
        if (!hadGoal)
            builder.Append(", missing G fixed");
        if (hadStart && hadGoal && !hadPath)
            builder.Append(", S-G path fixed");
        if (dugWalls)
            builder.Append(", walls opened");

        builder.Append($", S=({start.x},{start.y}), G=({goal.x},{goal.y})");
        return builder.ToString();
    }

    private static List<AsciiMapData> ParseInputMaps(string json)
    {
        List<AsciiMapData> maps = new List<AsciiMapData>();
        if (string.IsNullOrWhiteSpace(json))
            return maps;

        string trimmed = json.Trim();
        try
        {
            if (trimmed.Contains("\"maps\""))
            {
                AsciiMapDataList wrapper = JsonUtility.FromJson<AsciiMapDataList>(trimmed);
                if (wrapper != null && wrapper.maps != null)
                    maps.AddRange(CloneMapDataArray(wrapper.maps));
            }
            else
            {
                AsciiMapData single = JsonUtility.FromJson<AsciiMapData>(trimmed);
                if (single != null && single.mapGrid != null && single.mapGrid.Length > 0)
                    maps.Add(CloneMapData(single));
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to parse ASCII map JSON: {exception.Message}");
        }

        return maps;
    }

    private static char[,] ParseGrid(AsciiMapData map)
    {
        string[] rows = map != null && map.mapGrid != null ? map.mapGrid : new string[0];
        int height = Mathf.Max(5, map != null && map.height > 0 ? map.height : rows.Length);
        int width = Mathf.Max(5, map != null && map.width > 0 ? map.width : GetMaxRowWidth(rows));

        char[,] grid = new char[height, width];
        for (int y = 0; y < height; y++)
        {
            string row = y < rows.Length && rows[y] != null ? rows[y] : string.Empty;
            for (int x = 0; x < width; x++)
            {
                char value = x < row.Length ? row[x] : Wall;
                grid[y, x] = IsKnownTile(value) ? value : Floor;
            }
        }

        EnsureBorderWalls(grid);
        return grid;
    }

    private static AsciiMapData BuildMapData(AsciiMapData source, char[,] grid)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        AsciiMapData result = CloneMapData(source);
        result.width = width;
        result.height = height;
        result.mapGrid = ToRows(grid);
        return result;
    }

    private static string[] ToRows(char[,] grid)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        string[] rows = new string[height];

        for (int y = 0; y < height; y++)
        {
            StringBuilder builder = new StringBuilder(width);
            for (int x = 0; x < width; x++)
                builder.Append(grid[y, x]);

            rows[y] = builder.ToString();
        }

        return rows;
    }

    private static string ToAscii(string[] rows)
    {
        if (rows == null)
            return string.Empty;

        return string.Join("\n", rows);
    }

    private static string JoinAsciiOutputs(List<string> asciiMaps)
    {
        if (asciiMaps == null || asciiMaps.Count == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < asciiMaps.Count; i++)
        {
            if (i > 0)
                builder.AppendLine("\n---");

            builder.AppendLine(asciiMaps[i]);
        }

        return builder.ToString().TrimEnd();
    }

    private static string ToJsonOutput(List<AsciiMapData> maps)
    {
        if (maps == null || maps.Count == 0)
            return string.Empty;

        if (maps.Count == 1)
            return JsonUtility.ToJson(maps[0], true);

        AsciiMapDataList wrapper = new AsciiMapDataList { maps = maps.ToArray() };
        return JsonUtility.ToJson(wrapper, true);
    }

    private static int CountConnectedAreas(char[,] grid, bool enemiesArePassable)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        bool[,] visited = new bool[height, width];
        int count = 0;

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (visited[y, x] || !IsPassable(grid[y, x], enemiesArePassable))
                    continue;

                count++;
                FloodFillArea(grid, visited, new Vector2Int(x, y), enemiesArePassable);
            }
        }

        return count;
    }

    private static void FloodFillArea(char[,] grid, bool[,] visited, Vector2Int start, bool enemiesArePassable)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        visited[start.y, start.x] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int next in GetNeighbors(current))
            {
                if (next.x <= 0 || next.y <= 0 || next.x >= width - 1 || next.y >= height - 1)
                    continue;
                if (visited[next.y, next.x] || !IsPassable(grid[next.y, next.x], enemiesArePassable))
                    continue;

                visited[next.y, next.x] = true;
                queue.Enqueue(next);
            }
        }
    }

    private static bool IsKnownTile(char value)
    {
        return value == Wall || value == Floor || value == Start || value == Goal || value == Enemy;
    }

    private static void EnsureBorderWalls(char[,] grid)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            grid[0, x] = Wall;
            grid[height - 1, x] = Wall;
        }

        for (int y = 0; y < height; y++)
        {
            grid[y, 0] = Wall;
            grid[y, width - 1] = Wall;
        }
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

        position = Vector2Int.zero;
        return false;
    }

    private static void ClearMarkers(char[,] grid, char marker)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (grid[y, x] == marker)
                    grid[y, x] = Floor;
            }
        }
    }

    private static int FindNearestUsableRow(char[,] grid, int x, int preferredRow)
    {
        int height = grid.GetLength(0);
        int clampedPreferred = Mathf.Clamp(preferredRow, 1, height - 2);

        if (IsPassable(grid[clampedPreferred, x], true))
            return clampedPreferred;

        for (int offset = 1; offset < height; offset++)
        {
            int up = clampedPreferred - offset;
            if (up >= 1 && IsPassable(grid[up, x], true))
                return up;

            int down = clampedPreferred + offset;
            if (down <= height - 2 && IsPassable(grid[down, x], true))
                return down;
        }

        return clampedPreferred;
    }

    private static List<Vector2Int> FindPath(
        char[,] grid,
        Vector2Int start,
        Vector2Int goal,
        bool canDigWalls,
        bool enemiesArePassable)
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        List<PathNode> open = new List<PathNode>();
        Dictionary<Vector2Int, PathNode> allNodes = new Dictionary<Vector2Int, PathNode>();
        HashSet<Vector2Int> closed = new HashSet<Vector2Int>();

        PathNode startNode = new PathNode(start, null, 0, Manhattan(start, goal));
        open.Add(startNode);
        allNodes[start] = startNode;

        while (open.Count > 0)
        {
            int currentIndex = GetBestOpenIndex(open);
            PathNode current = open[currentIndex];
            open.RemoveAt(currentIndex);

            if (current.position == goal)
                return BuildPath(current);

            closed.Add(current.position);

            foreach (Vector2Int next in GetNeighbors(current.position))
            {
                if (next.x <= 0 || next.y <= 0 || next.x >= width - 1 || next.y >= height - 1)
                    continue;
                if (closed.Contains(next))
                    continue;

                char nextTile = grid[next.y, next.x];
                bool isWall = nextTile == Wall;
                if (isWall && !canDigWalls)
                    continue;
                if (!isWall && !IsPassable(nextTile, enemiesArePassable))
                    continue;

                int moveCost = isWall ? 8 : 1;
                int nextCost = current.gCost + moveCost;

                PathNode existing;
                if (allNodes.TryGetValue(next, out existing))
                {
                    if (nextCost >= existing.gCost)
                        continue;

                    existing.parent = current;
                    existing.gCost = nextCost;
                    existing.hCost = Manhattan(next, goal);
                    if (!open.Contains(existing))
                        open.Add(existing);
                }
                else
                {
                    PathNode node = new PathNode(next, current, nextCost, Manhattan(next, goal));
                    allNodes[next] = node;
                    open.Add(node);
                }
            }
        }

        return null;
    }

    private static bool IsPassable(char tile, bool enemiesArePassable)
    {
        if (tile == Wall)
            return false;
        if (tile == Enemy)
            return enemiesArePassable;

        return true;
    }

    private static IEnumerable<Vector2Int> GetNeighbors(Vector2Int position)
    {
        yield return new Vector2Int(position.x + 1, position.y);
        yield return new Vector2Int(position.x - 1, position.y);
        yield return new Vector2Int(position.x, position.y + 1);
        yield return new Vector2Int(position.x, position.y - 1);
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static int GetBestOpenIndex(List<PathNode> open)
    {
        int bestIndex = 0;
        int bestCost = open[0].FCost;
        int bestH = open[0].hCost;

        for (int i = 1; i < open.Count; i++)
        {
            int cost = open[i].FCost;
            if (cost < bestCost || cost == bestCost && open[i].hCost < bestH)
            {
                bestIndex = i;
                bestCost = cost;
                bestH = open[i].hCost;
            }
        }

        return bestIndex;
    }

    private static List<Vector2Int> BuildPath(PathNode node)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        PathNode current = node;
        while (current != null)
        {
            path.Add(current.position);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    private static List<Vector2Int> CreateFallbackLPath(Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        int xStep = start.x <= goal.x ? 1 : -1;
        int yStep = start.y <= goal.y ? 1 : -1;

        for (int x = start.x; x != goal.x; x += xStep)
            path.Add(new Vector2Int(x, start.y));

        for (int y = start.y; y != goal.y; y += yStep)
            path.Add(new Vector2Int(goal.x, y));

        path.Add(goal);
        return path;
    }

    private static int GetMaxRowWidth(string[] rows)
    {
        int width = 0;
        if (rows == null)
            return width;

        foreach (string row in rows)
        {
            if (row != null)
                width = Mathf.Max(width, row.Length);
        }

        return width;
    }

    private static List<AsciiMapData> CloneMapDataList(List<AsciiMapData> maps)
    {
        List<AsciiMapData> clones = new List<AsciiMapData>();
        if (maps == null)
            return clones;

        foreach (AsciiMapData map in maps)
            clones.Add(CloneMapData(map));

        return clones;
    }

    private static List<AsciiMapData> CloneMapDataArray(AsciiMapData[] maps)
    {
        List<AsciiMapData> clones = new List<AsciiMapData>();
        if (maps == null)
            return clones;

        foreach (AsciiMapData map in maps)
            clones.Add(CloneMapData(map));

        return clones;
    }

    private static AsciiMapData CloneMapData(AsciiMapData source)
    {
        AsciiMapData clone = new AsciiMapData();
        if (source == null)
            return clone;

        clone.width = source.width;
        clone.height = source.height;
        clone.startX = source.startX;
        clone.startY = source.startY;
        clone.mapGrid = source.mapGrid == null ? new string[0] : (string[])source.mapGrid.Clone();
        return clone;
    }

    private struct RepairResult
    {
        public char[,] grid;
        public string report;
    }

    private class PathNode
    {
        public Vector2Int position;
        public PathNode parent;
        public int gCost;
        public int hCost;
        public int FCost { get { return gCost + hCost; } }

        public PathNode(Vector2Int position, PathNode parent, int gCost, int hCost)
        {
            this.position = position;
            this.parent = parent;
            this.gCost = gCost;
            this.hCost = hCost;
        }
    }
}

[Serializable]
public class AsciiMapData
{
    public int width;
    public int height;
    public int startX;
    public int startY;
    public string[] mapGrid;
}

[Serializable]
public class AsciiMapDataList
{
    public AsciiMapData[] maps;
}
