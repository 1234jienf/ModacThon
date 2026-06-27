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
        get {
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
    }

    private void GenerateBridgeMaps()
    {
        leafNodes.Clear();
        actualRooms.Clear();

        Bounds boundsA = GetActualTilemapBounds(map_A, out mapACenter);
        Bounds boundsB = GetActualTilemapBounds(map_B, out mapBCenter);

        finalBridgeZone = CalculateBridgeZone(boundsA, boundsB);
        if (finalBridgeZone.width <= 0 || finalBridgeZone.height <= 0) return;

        if (drawTotalOutline) DrawMapOutline(finalBridgeZone); 

        BridgeMapNode root = new BridgeMapNode(finalBridgeZone);
        Divide(root, 0);
        GenerateRoom(root, 0);

        SaveMapToJson();
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
                foreach(var cell in validCells)
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
        if (n == divideCount || tree.nodeRect.width < minNodeSizeToDivide || tree.nodeRect.height < minNodeSizeToDivide) return;
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
            if (rect.width <= 2 || rect.height <= 2) { leafNodes.Add(tree); return rect; }

            int minW = Mathf.Max(2, Mathf.RoundToInt(rect.width * minRoomSizeRatio));
            int maxW = Mathf.Max(minW + 1, Mathf.RoundToInt(rect.width * maxRoomSizeRatio));
            int minH = Mathf.Max(2, Mathf.RoundToInt(rect.height * minRoomSizeRatio));
            int maxH = Mathf.Max(minH + 1, Mathf.RoundToInt(rect.height * maxRoomSizeRatio));

            int width = Random.Range(minW, Mathf.Min(maxW, rect.width)); 
            int height = Random.Range(minH, Mathf.Min(maxH, rect.height));  
            int x = rect.x + Random.Range(1, rect.width - width);
            int y = rect.y + Random.Range(1, rect.height - height);        
           
            rect = new RectInt(x, y, width, height);
            tree.roomRect = rect; 
            leafNodes.Add(tree); actualRooms.Add(tree); 

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

    /// <summary>
    /// [수정] map_A의 실제 중간 높이에서 통로 1개 라인, map_B의 실제 중간 높이에서 통로 1개 라인만 깔끔하게 연결합니다.
    /// </summary>
    private void ConnectMapsToBridge(char[,] grid, int w, int h)
    {
        if (actualRooms.Count == 0) return;

        // --- map_A 연결 (좌측 진입로 1개 라인) ---
        int targetY_A = Mathf.Clamp(mapACenter.y, finalBridgeZone.y, finalBridgeZone.yMax - 1);
        Vector2Int entryPointA = new Vector2Int(finalBridgeZone.x, targetY_A);

        BridgeMapNode closestRoomA = null;
        float minDistA = float.MaxValue;
        foreach (var room in actualRooms)
        {
            float dist = Vector2Int.Distance(entryPointA, room.center);
            if (dist < minDistA) { minDistA = dist; closestRoomA = room; }
        }
        if (closestRoomA != null) DigCorridor(entryPointA, closestRoomA.center, grid, w, h);

        // --- map_B 연결 (우측 진입로 1개 라인) ---
        int targetY_B = Mathf.Clamp(mapBCenter.y, finalBridgeZone.y, finalBridgeZone.yMax - 1);
        Vector2Int entryPointB = new Vector2Int(finalBridgeZone.xMax - 1, targetY_B);

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

            BridgeMapNode upRoom = null;    float upMinDist = float.MaxValue;
            BridgeMapNode downRoom = null;  float downMinDist = float.MaxValue;
            BridgeMapNode leftRoom = null;  float leftMinDist = float.MaxValue;
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

    /// <summary>
    /// [수정] 모든 내부 및 외부 통로의 두께를 완벽히 3칸(line3)으로 확장하여 타일을 생성합니다.
    /// </summary>
    private void DigCorridor(Vector2Int start, Vector2Int end, char[,] grid, int w, int h)
    {
        int gridStartX = start.x - finalBridgeZone.x;
        int gridStartY = start.y - finalBridgeZone.y;
        int gridEndX = end.x - finalBridgeZone.x;
        int gridEndY = end.y - finalBridgeZone.y;

        if (drawCorridorLines)
        {
            // 인게임 에디터 기즈모/라인 두께도 3칸 느낌에 맞춰 살짝 확장 조정
            DrawLine(new Vector2(start.x, start.y), new Vector2(end.x, start.y), Color.yellow, 0.18f);
            DrawLine(new Vector2(end.x, start.y), new Vector2(end.x, end.y), Color.yellow, 0.18f);
        }

        // 가로축 이동 시 기준선을 포함해 아래/위 총 3칸(offset: -1, 0, 1) 파내기
        int minX = Mathf.Min(gridStartX, gridEndX);
        int maxX = Mathf.Max(gridStartX, gridEndX);
        for (int x = minX; x <= maxX; x++)
        {
            for (int offset = -1; offset <= 1; offset++)
            {
                int ty = gridStartY + offset;
                if (x >= 0 && x < w && ty >= 0 && ty < h) grid[ty, x] = '.';
            }
        }

        // 세로축 이동 시 기준선을 포함해 좌/우 총 3칸(offset: -1, 0, 1) 파내기
        int minY = Mathf.Min(gridStartY, gridEndY);
        int maxY = Mathf.Max(gridStartY, gridEndY);
        for (int y = minY; y <= maxY; y++)
        {
            for (int offset = 0; offset <= 1; offset++)
            {
                int tx = gridEndX + offset;
                if (tx >= 0 && tx < w && y >= 0 && y < h) grid[y, tx] = '.';
            }
        }
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

        MapJsonData jsonData = new MapJsonData();
        jsonData.width = w; jsonData.height = h;
        jsonData.startX = finalBridgeZone.x; jsonData.startY = finalBridgeZone.y;

        for (int y = 0; y < h; y++)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int x = 0; x < w; x++) sb.Append(grid[y, x]);
            jsonData.mapGrid.Add(sb.ToString());
        }

        string jsonString = JsonUtility.ToJson(jsonData, true);
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folderPath = Path.Combine(projectRoot, "tmpOutput");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        string filePath = Path.Combine(folderPath, "BridgeMapData.json");
        if (File.Exists(filePath)) File.Delete(filePath);
        File.WriteAllText(filePath, jsonString);

        Debug.Log($"[JSON 폭3칸/중앙단일진입 저장완료] 경로: {filePath}");
    }

    private void DrawMapOutline(RectInt rect) { GameObject go = new GameObject("Bridge_Total_Outline"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, Color.white, 0.15f); lr.positionCount = 4; lr.loop = true; lr.SetPosition(0, new Vector2(rect.x, rect.y)); lr.SetPosition(1, new Vector2(rect.x + rect.width, rect.y)); lr.SetPosition(2, new Vector2(rect.x + rect.width, rect.y + rect.height)); lr.SetPosition(3, new Vector2(rect.x, rect.y + rect.height)); }
    private void DrawLine(Vector2 from, Vector2 to, Color color, float width) { GameObject go = new GameObject("BSP_Line"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, color, width); lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, to); }
    private void DrawRectangle(RectInt rect) { GameObject go = new GameObject("Bridge_Room_Line"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, Color.green, 0.09f); lr.positionCount = 4; lr.loop = true; lr.SetPosition(0, new Vector2(rect.x, rect.y)); lr.SetPosition(1, new Vector2(rect.x + rect.width, rect.y)); lr.SetPosition(2, new Vector2(rect.x + rect.width, rect.y + rect.height)); lr.SetPosition(3, new Vector2(rect.x, rect.y + rect.height)); }
    private void SetupLineRenderer(LineRenderer lr, Color color, float width) { lr.startWidth = width; lr.endWidth = width; lr.useWorldSpace = true; Material defaultMat = new Material(Shader.Find("Sprites/Default")); lr.material = defaultMat; lr.startColor = color; lr.endColor = color; }
}