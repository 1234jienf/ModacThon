using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class MapJsonData
{
    public int width;
    public int height;
    public int startX;
    public int startY;
    public List<string> mapGrid = new List<string>();
}

public class BridgeMapNode
{
    public BridgeMapNode leftNode;
    public BridgeMapNode rightNode;
    public BridgeMapNode parNode;
    public RectInt nodeRect;
    public RectInt roomRect;

    public Vector2Int center
    {
        get
        {
            return new Vector2Int(roomRect.x + roomRect.width / 2, roomRect.y + roomRect.height / 2);
        }
    }
    public BridgeMapNode(RectInt rect)
    {
        this.nodeRect = rect;
    }
}

public class BSP_Generator : MonoBehaviour
{
    [Header("=== 기준 맵 오브젝트 ===")]
    [SerializeField] private GameObject map_A;
    [SerializeField] private GameObject map_B;

    [Header("=== BSP 분할 설정 ===")]
    [SerializeField] private int divideCount = 3;
    [SerializeField] float minimumDevideRate = 0.25f;
    [SerializeField] float maximumDivideRate = 0.75f;

    [Header("=== 방 크기 상세 설정 ===")]
    [Range(0.2f, 0.9f)] [SerializeField] private float minRoomSizeRatio = 0.5f;
    [Range(0.3f, 1.0f)] [SerializeField] private float maxRoomSizeRatio = 0.9f;
    [SerializeField] private int minNodeSizeToDivide = 10;

    [Header("=== 시각화 필터 ===")]
    [SerializeField] private bool drawTotalOutline = true;
    [SerializeField] private bool drawDivideLines = true;
    [SerializeField] private bool drawRoomLines = true;
    [SerializeField] private bool drawCorridorLines = true;

    private List<BridgeMapNode> leafNodes = new List<BridgeMapNode>();
    private List<BridgeMapNode> actualRooms = new List<BridgeMapNode>();
    private RectInt finalBridgeZone;

    private Vector2Int mapACenter;
    private Vector2Int mapBCenter;

    void Start()
    {
        if (map_A == null || map_B == null)
        {
            Debug.LogError("map_A 또는 map_B가 할당되지 않았습니다!");
            return;
        }
        GenerateBridgeMaps();

        PathProcessor pathProcessor = GetComponent<PathProcessor>();
        if (pathProcessor != null)
        {
            pathProcessor.ProcessRoomPaths();
        }
    }

    private void GenerateBridgeMaps()
    {
        leafNodes.Clear();
        actualRooms.Clear();
        MapManager.Instance.ClearMapData();

        Bounds boundsA = GetActualTilemapBounds(map_A, out mapACenter);
        Bounds boundsB = GetActualTilemapBounds(map_B, out mapBCenter);

        finalBridgeZone = CalculateBridgeZone(boundsA, boundsB);
        if (finalBridgeZone.width <= 0 || finalBridgeZone.height <= 0) return;

        if (drawTotalOutline) DrawMapOutline(finalBridgeZone);

        BridgeMapNode root = new BridgeMapNode(finalBridgeZone);
        Divide(root, 0);
        GenerateRoom(root, 0);

        SaveMapToJson();
        MapManager.Instance.LogMapStatus();
    }

    private Bounds GetActualTilemapBounds(GameObject mapObj, out Vector2Int centerPos)
    {
        centerPos = Vector2Int.zero;
        Tilemap tilemap = mapObj.GetComponentInChildren<Tilemap>();
        if (tilemap != null)
        {
            BoundsInt cellBounds = tilemap.cellBounds;
            Vector3 minWorld = Vector3.one * float.MaxValue;
            Vector3 maxWorld = Vector3.one * float.MinValue;
            bool hasTile = false;
            List<Vector3Int> validCells = new List<Vector3Int>();

            foreach (var pos in cellBounds.allPositionsWithin)
            {
                if (tilemap.HasTile(pos))
                {
                    hasTile = true;
                    validCells.Add(pos);
                    Vector3 worldPos = tilemap.CellToWorld(pos);
                    minWorld = Vector3.Min(minWorld, worldPos);
                    maxWorld = Vector3.Max(maxWorld, worldPos + tilemap.layoutGrid.cellSize);
                }
            }
            if (hasTile)
            {
                float avgX = 0, avgY = 0;
                foreach (var cell in validCells)
                {
                    Vector3 wPos = tilemap.CellToWorld(cell);
                    avgX += wPos.x; avgY += wPos.y;
                }
                centerPos = new Vector2Int(Mathf.FloorToInt(avgX / validCells.Count), Mathf.FloorToInt(avgY / validCells.Count));
                Bounds b = new Bounds(); b.SetMinMax(minWorld, maxWorld); return b;
            }
        }
        Renderer rend = mapObj.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            centerPos = new Vector2Int(Mathf.FloorToInt(rend.bounds.center.x), Mathf.FloorToInt(rend.bounds.center.y));
            return rend.bounds;
        }
        centerPos = new Vector2Int(Mathf.FloorToInt(mapObj.transform.position.x), Mathf.FloorToInt(mapObj.transform.position.y));
        return new Bounds(mapObj.transform.position, Vector3.one * 5f);
    }

    private RectInt CalculateBridgeZone(Bounds bA, Bounds bB)
    {
        int x = 0; int w = 0;
        if (bA.max.x <= bB.min.x) { x = Mathf.FloorToInt(bA.max.x); w = Mathf.CeilToInt(bB.min.x) - x; }
        else if (bB.max.x <= bA.min.x) { x = Mathf.FloorToInt(bB.max.x); w = Mathf.CeilToInt(bA.min.x) - x; }
        else { float gapMinX = Mathf.Max(bA.min.x, bB.min.x); float gapMaxX = Mathf.Min(bA.max.x, bB.max.x); x = Mathf.FloorToInt(gapMinX); w = Mathf.CeilToInt(gapMaxX) - x; }
        int y = Mathf.FloorToInt(Mathf.Min(bA.min.y, bB.min.y));
        int h = Mathf.CeilToInt(Mathf.Max(bA.max.y, bB.max.y)) - y;
        return new RectInt(x, y, w, h);
    }

    void Divide(BridgeMapNode tree, int n)
    {
        // 1. minNodeSizeToDivide - 2 규격 조건 반영
        int limitSize = Mathf.Max(4, minNodeSizeToDivide - 2);
        if (n == divideCount || tree.nodeRect.width < limitSize || tree.nodeRect.height < limitSize) return;

        // 2. Map_A(시작점)와 Map_B(끝점) 사이의 상대적 위치 비율 계산 (0.0 ~ 1.0)
        float nodeCenterX = tree.nodeRect.x + tree.nodeRect.width / 2f;
        float progress = Mathf.InverseLerp(finalBridgeZone.x, finalBridgeZone.xMax, nodeCenterX);

        if (progress < 0.5f) // 중간 미만 (Map_A에 가까운 구역)
        {
            // Map_A에 가까울수록(progress가 0에 가까울수록) 분할 확률이 낮아짐
            // 예: progress가 0이면 0%, 중간(0.5)에 가까워질수록 설정한 최대 확률(45%)에 수렴
            float maxProbabilityAtCenter = 0.45f;
            float currentDivideProbability = Mathf.Lerp(0f, maxProbabilityAtCenter, progress / 0.5f);

            if (Random.value > currentDivideProbability) return; // 확률을 뚫지 못하면 넓은 방으로 유지
        }
        // 중간 이상(progress >= 0.5f) 영역은 무조건(100%) 분할 진행

        int maxLength = Mathf.Max(tree.nodeRect.width, tree.nodeRect.height);
        int split = Mathf.RoundToInt(Random.Range(maxLength * minimumDevideRate, maxLength * maximumDivideRate));
        if (maxLength <= 4 || split <= 1 || (maxLength - split) <= 1) return;

        if (tree.nodeRect.width >= tree.nodeRect.height)
        {
            tree.leftNode = new BridgeMapNode(new RectInt(tree.nodeRect.x, tree.nodeRect.y, split, tree.nodeRect.height));
            tree.rightNode = new BridgeMapNode(new RectInt(tree.nodeRect.x + split, tree.nodeRect.y, tree.nodeRect.width - split, tree.nodeRect.height));
            if (drawDivideLines) DrawLine(new Vector2(tree.nodeRect.x + split, tree.nodeRect.y), new Vector2(tree.nodeRect.x + split, tree.nodeRect.y + tree.nodeRect.height), Color.red, 0.06f);
        }
        else
        {
            tree.leftNode = new BridgeMapNode(new RectInt(tree.nodeRect.x, tree.nodeRect.y, tree.nodeRect.width, split));
            tree.rightNode = new BridgeMapNode(new RectInt(tree.nodeRect.x, tree.nodeRect.y + split, tree.nodeRect.width, tree.nodeRect.height - split));
            if (drawDivideLines) DrawLine(new Vector2(tree.nodeRect.x, tree.nodeRect.y + split), new Vector2(tree.nodeRect.x + tree.nodeRect.width, tree.nodeRect.y + split), Color.red, 0.06f);
        }
        tree.leftNode.parNode = tree; tree.rightNode.parNode = tree;
        Divide(tree.leftNode, n + 1); Divide(tree.rightNode, n + 1);
    }

    private RectInt GenerateRoom(BridgeMapNode tree, int n)
    {
        RectInt rect;
        if (n == divideCount || (tree.leftNode == null && tree.rightNode == null))
        {
            rect = tree.nodeRect;

            // 벽 두께 2칸 이상을 보장하기 위해 노드 패딩 마진 최소 크기 체크 확장
            if (rect.width <= 5 || rect.height <= 5) { leafNodes.Add(tree); return rect; }

            int minW = Mathf.Max(2, Mathf.RoundToInt(rect.width * minRoomSizeRatio));
            int maxW = Mathf.Max(minW + 1, Mathf.RoundToInt(rect.width * maxRoomSizeRatio));
            int minH = Mathf.Max(2, Mathf.RoundToInt(rect.height * minRoomSizeRatio));
            int maxH = Mathf.Max(minH + 1, Mathf.RoundToInt(rect.height * maxRoomSizeRatio));

            int width = Random.Range(minW, Mathf.Min(maxW, rect.width - 4)); // 최소 양쪽 2칸 확보용 서브트랙트
            int height = Random.Range(minH, Mathf.Min(maxH, rect.height - 4));

            if (width <= 0) width = 1;
            if (height <= 0) height = 1;

            // 시작 좌표 마진을 Random 1에서 최소 '2' 이상의 벽 두께를 갖도록 수정
            int x = rect.x + Random.Range(2, Mathf.Max(3, rect.width - width - 1));
            int y = rect.y + Random.Range(2, Mathf.Max(3, rect.height - height - 1));

            rect = new RectInt(x, y, width, height);
            tree.roomRect = rect;
            leafNodes.Add(tree); actualRooms.Add(tree);

            MapManager.Instance.RegisterRoom(rect);

            if (drawRoomLines) DrawRectangle(rect);
            return rect;
        }
        else
        {
            if (tree.leftNode != null) tree.leftNode.roomRect = GenerateRoom(tree.leftNode, n + 1);
            if (tree.rightNode != null) tree.rightNode.roomRect = GenerateRoom(tree.rightNode, n + 1);
            return tree.leftNode != null ? tree.leftNode.roomRect : tree.nodeRect;
        }
    }

    private void ConnectMapsToBridge(char[,] grid, int w, int h)
    {
        if (actualRooms.Count == 0) return;

        int targetY_A = Mathf.Clamp(mapACenter.y, finalBridgeZone.y, finalBridgeZone.yMax - 1);
        Vector2Int entryPointA = new Vector2Int(finalBridgeZone.x, targetY_A);
        MapManager.Instance.EntryPointA = entryPointA;

        BridgeMapNode closestRoomA = null;
        float minDistA = float.MaxValue;
        foreach (var room in actualRooms)
        {
            float dist = Vector2Int.Distance(entryPointA, room.center);
            if (dist < minDistA) { minDistA = dist; closestRoomA = room; }
        }
        if (closestRoomA != null) DigCorridor(entryPointA, closestRoomA.center, grid, w, h);

        int targetY_B = Mathf.Clamp(mapBCenter.y, finalBridgeZone.y, finalBridgeZone.yMax - 1);
        Vector2Int entryPointB = new Vector2Int(finalBridgeZone.xMax - 1, targetY_B);
        MapManager.Instance.EntryPointB = entryPointB;

        BridgeMapNode closestRoomB = null;
        float minDistB = float.MaxValue;
        foreach (var room in actualRooms)
        {
            float dist = Vector2Int.Distance(entryPointB, room.center);
            if (dist < minDistB) { minDistB = dist; closestRoomB = room; }
        }
        if (closestRoomB != null) DigCorridor(entryPointB, closestRoomB.center, grid, w, h);
    }

    private void ConnectAdjacentRooms(char[,] grid, int w, int h)
    {
        if (actualRooms.Count < 2) return;

        HashSet<string> connectedPairs = new HashSet<string>();

        for (int i = 0; i < actualRooms.Count; i++)
        {
            BridgeMapNode current = actualRooms[i];

            BridgeMapNode upRoom = null; float upMinDist = float.MaxValue;
            BridgeMapNode downRoom = null; float downMinDist = float.MaxValue;
            BridgeMapNode leftRoom = null; float leftMinDist = float.MaxValue;
            BridgeMapNode rightRoom = null; float rightMinDist = float.MaxValue;

            for (int j = 0; j < actualRooms.Count; j++)
            {
                if (i == j) continue;
                BridgeMapNode target = actualRooms[j];

                Vector2Int heading = target.center - current.center;
                float distance = heading.magnitude;

                if (Mathf.Abs(heading.x) > Mathf.Abs(heading.y))
                {
                    if (heading.x > 0 && distance < rightMinDist) { rightMinDist = distance; rightRoom = target; }
                    else if (heading.x < 0 && distance < leftMinDist) { leftMinDist = distance; leftRoom = target; }
                }
                else
                {
                    if (heading.y > 0 && distance < upMinDist) { upMinDist = distance; upRoom = target; }
                    else if (heading.y < 0 && distance < downMinDist) { downMinDist = distance; downRoom = target; }
                }
            }

            List<BridgeMapNode> validDirections = new List<BridgeMapNode>();
            if (upRoom != null) validDirections.Add(upRoom);
            if (downRoom != null) validDirections.Add(downRoom);
            if (leftRoom != null) validDirections.Add(leftRoom);
            if (rightRoom != null) validDirections.Add(rightRoom);

            List<BridgeMapNode> filteredDirections = new List<BridgeMapNode>();
            foreach (var target in validDirections)
            {
                int targetIdx = actualRooms.IndexOf(target);
                string pairKey = i < targetIdx ? $"{i}_{targetIdx}" : $"{targetIdx}_{i}";
                if (!connectedPairs.Contains(pairKey)) filteredDirections.Add(target);
            }

            int connectionsCount = Mathf.Min(filteredDirections.Count, Mathf.Max(3, filteredDirections.Count));

            for (int k = 0; k < connectionsCount; k++)
            {
                BridgeMapNode target = filteredDirections[k];
                int targetIdx = actualRooms.IndexOf(target);
                string pairKey = i < targetIdx ? $"{i}_{targetIdx}" : $"{targetIdx}_{i}";

                DigCorridor(current.center, target.center, grid, w, h);
                connectedPairs.Add(pairKey);
            }
        }
    }

    private void DigCorridor(Vector2Int start, Vector2Int end, char[,] grid, int w, int h)
    {
        int gridStartX = start.x - finalBridgeZone.x;
        int gridStartY = start.y - finalBridgeZone.y;
        int gridEndX = end.x - finalBridgeZone.x;
        int gridEndY = end.y - finalBridgeZone.y;

        if (drawCorridorLines)
        {
            DrawLine(new Vector2(start.x, start.y), new Vector2(end.x, start.y), Color.yellow, 0.18f);
            DrawLine(new Vector2(end.x, start.y), new Vector2(end.x, end.y), Color.yellow, 0.18f);
        }

        // 2칸 두께 여백 안전성 검증 범위 확장
        bool IsPathBlocked(int x, int y, int dx, int dy)
        {
            for (int i = -2; i <= 2; i++)
            {
                for (int j = -2; j <= 2; j++)
                {
                    int ny = y + i;
                    int nx = x + j;
                    if (ny >= 0 && ny < h && nx >= 0 && nx < w && grid[ny, nx] == 'P')
                        return true;
                }
            }
            return false;
        }

        int minX = Mathf.Min(gridStartX, gridEndX);
        int maxX = Mathf.Max(gridStartX, gridEndX);
        int currentY = gridStartY;

        if (IsPathBlocked(gridStartX, currentY, 1, 0)) currentY += (currentY < h - 3) ? 2 : -2;

        for (int x = minX; x <= maxX; x++)
        {
            for (int offset = -1; offset <= 1; offset++)
            {
                int ty = currentY + offset;
                if (x >= 0 && x < w && ty >= 0 && ty < h && grid[ty, x] == '#')
                {
                    grid[ty, x] = 'P';
                }
            }
        }

        int minY = Mathf.Min(gridStartY, gridEndY);
        int maxY = Mathf.Max(gridStartY, gridEndY);
        int currentX = gridEndX;

        if (IsPathBlocked(currentX, gridEndY, 0, 1)) currentX += (currentX < w - 3) ? 2 : -2;

        for (int y = minY; y <= maxY; y++)
        {
            for (int offset = -1; offset <= 1; offset++)
            {
                int tx = currentX + offset;
                if (tx >= 0 && tx < w && y >= 0 && y < h && grid[y, tx] == '#')
                {
                    grid[y, tx] = 'P';
                }
            }
        }
    }

    private void FindAndRegisterEntrances(char[,] grid, int w, int h)
    {
        foreach (var room in MapManager.Instance.GetAllRooms())
        {
            RectInt b = room.bounds;
            int startX = b.x - finalBridgeZone.x;
            int startY = b.y - finalBridgeZone.y;

            HashSet<Vector2Int> perimeterCorridors = new HashSet<Vector2Int>();

            for (int x = startX; x < startX + b.width; x++)
            {
                if (IsCorridorTile(x, startY - 1, grid, w, h)) perimeterCorridors.Add(new Vector2Int(x, startY - 1));
                if (IsCorridorTile(x, startY + b.height, grid, w, h)) perimeterCorridors.Add(new Vector2Int(x, startY + b.height));
            }
            for (int y = startY; y < startY + b.height; y++)
            {
                if (IsCorridorTile(startX - 1, y, grid, w, h)) perimeterCorridors.Add(new Vector2Int(startX - 1, y));
                if (IsCorridorTile(startX + b.width, y, grid, w, h)) perimeterCorridors.Add(new Vector2Int(startX + b.width, y));
            }

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            List<List<Vector2Int>> clusters = new List<List<Vector2Int>>();

            foreach (var pos in perimeterCorridors)
            {
                if (visited.Contains(pos)) continue;

                List<Vector2Int> cluster = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();

                queue.Enqueue(pos);
                visited.Add(pos);

                while (queue.Count > 0)
                {
                    Vector2Int curr = queue.Dequeue();
                    cluster.Add(curr);

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            Vector2Int neighbor = new Vector2Int(curr.x + dx, curr.y + dy);

                            if (perimeterCorridors.Contains(neighbor) && !visited.Contains(neighbor))
                            {
                                visited.Add(neighbor);
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                }
                clusters.Add(cluster);
            }

            foreach (var cluster in clusters)
            {
                if (cluster.Count == 0) continue;

                float avgX = 0; float avgY = 0;
                foreach (var pt in cluster) { avgX += pt.x; avgY += pt.y; }
                Vector2 centerOfCluster = new Vector2(avgX / cluster.Count, avgY / cluster.Count);

                Vector2Int bestTile = cluster[0];
                float minDistance = float.MaxValue;

                foreach (var pt in cluster)
                {
                    float dist = Vector2.Distance(centerOfCluster, new Vector2(pt.x, pt.y));
                    if (dist < minDistance) { minDistance = dist; bestTile = pt; }
                }

                Vector2Int worldPos = new Vector2Int(bestTile.x + finalBridgeZone.x, bestTile.y + finalBridgeZone.y);
                if (!room.entrancePositions.Contains(worldPos))
                {
                    room.entrancePositions.Add(worldPos);
                }
            }
        }
    }

    private bool IsCorridorTile(int x, int y, char[,] grid, int w, int h)
    {
        return (x >= 0 && x < w && y >= 0 && y < h && grid[y, x] == 'P');
    }

    private void SaveMapToJson()
    {
        int w = finalBridgeZone.width;
        int h = finalBridgeZone.height;

        char[,] grid = new char[h, w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) grid[y, x] = '#';

        foreach (var node in actualRooms)
        {
            int startX = node.roomRect.x - finalBridgeZone.x;
            int startY = node.roomRect.y - finalBridgeZone.y;
            for (int y = startY; y < startY + node.roomRect.height; y++)
            {
                for (int x = startX; x < startX + node.roomRect.width; x++)
                {
                    if (x >= 0 && x < w && y >= 0 && y < h) grid[y, x] = '.';
                }
            }
        }

        ConnectAdjacentRooms(grid, w, h);
        ConnectMapsToBridge(grid, w, h);
        FindAndRegisterEntrances(grid, w, h);

        MapJsonData jsonData = new MapJsonData();
        jsonData.width = w; jsonData.height = h;
        jsonData.startX = finalBridgeZone.x; jsonData.startY = finalBridgeZone.y;

        BridgeMapJsonUtility.WriteGridRowsTopFirst(jsonData.mapGrid, grid);

        string jsonString = JsonUtility.ToJson(jsonData, true);
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folderPath = Path.Combine(projectRoot, "tmpOutput");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        string filePath = Path.Combine(folderPath, "BridgeMapData.json");
        if (File.Exists(filePath)) File.Delete(filePath);
        File.WriteAllText(filePath, jsonString);

        Debug.Log($"[JSON 저장완료] 경로: {filePath}");
    }

    private void DrawMapOutline(RectInt rect) { GameObject go = new GameObject("Bridge_Total_Outline"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, Color.white, 0.15f); lr.positionCount = 4; lr.loop = true; lr.SetPosition(0, new Vector2(rect.x, rect.y)); lr.SetPosition(1, new Vector2(rect.x + rect.width, rect.y)); lr.SetPosition(2, new Vector2(rect.x + rect.width, rect.y + rect.height)); lr.SetPosition(3, new Vector2(rect.x, rect.y + rect.height)); }
    private void DrawLine(Vector2 from, Vector2 to, Color color, float width) { GameObject go = new GameObject("BSP_Line"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, color, width); lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, to); }
    private void DrawRectangle(RectInt rect) { GameObject go = new GameObject("Bridge_Room_Line"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, Color.green, 0.09f); lr.positionCount = 4; lr.loop = true; lr.SetPosition(0, new Vector2(rect.x, rect.y)); lr.SetPosition(1, new Vector2(rect.x + rect.width, rect.y)); lr.SetPosition(2, new Vector2(rect.x + rect.width, rect.y + rect.height)); lr.SetPosition(3, new Vector2(rect.x, rect.y + rect.height)); }
    private void SetupLineRenderer(LineRenderer lr, Color color, float width) { lr.startWidth = width; lr.endWidth = width; lr.useWorldSpace = true; Material defaultMat = new Material(Shader.Find("Sprites/Default")); lr.material = defaultMat; lr.startColor = color; lr.endColor = color; }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || MapManager.Instance == null) return;

        GUIStyle roomStyle = new GUIStyle();
        roomStyle.normal.textColor = Color.green;
        roomStyle.fontSize = 14;
        roomStyle.fontStyle = FontStyle.Bold;

        GUIStyle entranceStyle = new GUIStyle();
        entranceStyle.normal.textColor = Color.yellow;
        entranceStyle.fontSize = 11;

        foreach (var room in MapManager.Instance.GetAllRooms())
        {
            Vector3 roomCenter = new Vector3(room.bounds.x + room.bounds.width / 2f, room.bounds.y + room.bounds.height / 2f, 0);
            UnityEditor.Handles.Label(roomCenter, $"[Room {room.id}]", roomStyle);

            for (int i = 0; i < room.entrancePositions.Count; i++)
            {
                Vector2Int entPos = room.entrancePositions[i];
                Vector3 entWorldPos = new Vector3(entPos.x, entPos.y, 0);

                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(entWorldPos, 0.2f);
                UnityEditor.Handles.Label(entWorldPos + Vector3.up * 0.3f, $"Ent_{i}\n({entPos.x}, {entPos.y})", entranceStyle);
            }
        }

        GUIStyle entryStyle = new GUIStyle();
        entryStyle.normal.textColor = Color.cyan;
        entryStyle.fontSize = 12;
        entryStyle.fontStyle = FontStyle.Bold;

        Vector2Int epA = MapManager.Instance.EntryPointA;
        Vector2Int epB = MapManager.Instance.EntryPointB;

        if (epA != Vector2Int.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(new Vector3(epA.x, epA.y, 0), 0.3f);
            UnityEditor.Handles.Label(new Vector3(epA.x, epA.y, 0) + Vector3.left * 1.5f, $"[Entry A]\n({epA.x}, {epA.y})", entryStyle);
        }
        if (epB != Vector2Int.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(new Vector3(epB.x, epB.y, 0), 0.3f);
            UnityEditor.Handles.Label(new Vector3(epB.x, epB.y, 0) + Vector3.right * 0.5f, $"[Entry B]\n({epB.x}, {epB.y})", entryStyle);
        }
    }
#endif
}