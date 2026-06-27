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

    [Header("=== [중요] 타일 정밀도 설정 ===")]
    [Tooltip("타일 한 칸의 픽셀 크기입니다. (예: 16x16 이면 16 입력)")]
    [SerializeField] private int pixelsPerTile = 16;

    [Header("=== BSP 분할 설정 (A안) ===")]
    [Tooltip("두 맵 사이의 공백 공간을 총 몇 번 쪼갤지 결정합니다.")]
    [SerializeField] private int divideCount = 3; 
    [SerializeField] float minimumDevideRate = 0.3f; 
    [SerializeField] float maximumDivideRate = 0.7f; 

    [Header("=== 방 크기 상세 설정 ===")]
    [Range(0.4f, 0.9f)] [SerializeField] private float minRoomSizeRatio = 0.6f; 
    [Range(0.5f, 1.0f)] [SerializeField] private float maxRoomSizeRatio = 0.95f; 
    [SerializeField] private int minNodeSizeToDivide = 8; 

    [Header("=== 시각화 필터 ===")]
    [SerializeField] private bool drawTotalOutline = true;  
    [SerializeField] private bool drawDivideLines = true;   
    [SerializeField] private bool drawRoomLines = true;     

    private List<BridgeMapNode> leafNodes = new List<BridgeMapNode>();
    private RectInt finalBridgeZone;
    private Grid unityGrid; // 타일맵 셀 변환용 기준 그리드

    void Start()
    {
        if (map_A == null || map_B == null)
        {
            Debug.LogError("map_A 또는 map_B가 할당되지 않았습니다!");
            return;
        }

        // 씬 내의 Grid 컴포넌트 자동 탐색 (없으면 Fallback)
        unityGrid = FindFirstObjectByType<Grid>();

        GenerateBridgeMaps();
    }

    private void GenerateBridgeMaps()
    {
        leafNodes.Clear();

        Bounds boundsA = GetActualTilemapBounds(map_A);
        Bounds boundsB = GetActualTilemapBounds(map_B);

        // 16픽셀 그리드 단위가 반영된 정밀 사잇공간 획득
        finalBridgeZone = CalculateBridgeZone(boundsA, boundsB);

        if (finalBridgeZone.width <= 0 || finalBridgeZone.height <= 0)
        {
            Debug.LogError($"[오류] 16픽셀 그리드 기준 사잇공간 빌딩 실패. 크기: {finalBridgeZone.width}x{finalBridgeZone.height}.");
            return;
        }

        if (drawTotalOutline)
        {
            DrawMapOutline(finalBridgeZone); 
        }

        BridgeMapNode root = new BridgeMapNode(finalBridgeZone);
        Divide(root, 0);
        GenerateRoom(root, 0);

        SaveMapToJson();
    }

    private Bounds GetActualTilemapBounds(GameObject mapObj)
    {
        Tilemap tilemap = mapObj.GetComponentInChildren<Tilemap>();
        
        if (tilemap != null)
        {
            BoundsInt cellBounds = tilemap.cellBounds;
            Vector3 minWorld = Vector3.one * float.MaxValue;
            Vector3 maxWorld = Vector3.one * float.MinValue;
            bool hasTile = false;

            foreach (var pos in cellBounds.allPositionsWithin)
            {
                if (tilemap.HasTile(pos))
                {
                    hasTile = true;
                    // 셀 좌표를 정확히 월드 좌표로 변환
                    Vector3 worldPos = tilemap.CellToWorld(pos);
                    minWorld = Vector3.Min(minWorld, worldPos);
                    maxWorld = Vector3.Max(maxWorld, worldPos + tilemap.layoutGrid.cellSize);
                }
            }

            if (hasTile)
            {
                Bounds actualBounds = new Bounds();
                actualBounds.SetMinMax(minWorld, maxWorld);
                return actualBounds;
            }
        }

        Renderer rend = mapObj.GetComponentInChildren<Renderer>();
        if (rend != null) return rend.bounds;

        return new Bounds(mapObj.transform.position, Vector3.one * 5f);
    }

    /// <summary>
    /// [핵심 수정] 유니티 월드 좌표를 16픽셀 단위 그리드(Cell) 인덱스로 완벽하게 동기화합니다.
    /// </summary>
    private RectInt CalculateBridgeZone(Bounds bA, Bounds bB)
    {
        // 타일의 물리적 단위 크기 계산 (보통 PPU가 16이면 cellSize는 1.0 혹은 타일 설정에 따라 달라짐)
        float tileUnitSize = (unityGrid != null) ? unityGrid.cellSize.x : 1.0f;

        // 월드 미터 단위를 타일 셀 단위 개수(정수)로 픽셀-퍼펙트 변환
        int aMinX = Mathf.FloorToInt(bA.min.x / tileUnitSize);
        int aMaxX = Mathf.CeilToInt(bA.max.x / tileUnitSize);
        int bMinX = Mathf.FloorToInt(bB.min.x / tileUnitSize);
        int bMaxX = Mathf.CeilToInt(bB.max.x / tileUnitSize);

        int aMinY = Mathf.FloorToInt(bA.min.y / tileUnitSize);
        int aMaxY = Mathf.CeilToInt(bA.max.y / tileUnitSize);
        int bMinY = Mathf.FloorToInt(bB.min.y / tileUnitSize);
        int bMaxY = Mathf.CeilToInt(bB.max.y / tileUnitSize);

        int x = 0;
        int w = 0;

        // X축: 격자 단위로 딱 붙도록 조건 처리
        if (aMaxX <= bMinX) // A가 왼쪽, B가 오른쪽
        {
            x = aMaxX;
            w = bMinX - x;
        }
        else if (bMaxX <= aMinX) // B가 왼쪽, A가 오른쪽
        {
            x = bMaxX;
            w = aMinX - x;
        }
        else // 대각선 상에서 X축이 교차하는 경우 교집합을 셀 단 정렬
        {
            x = Mathf.Max(aMinX, bMinX);
            w = Mathf.Min(aMaxX, bMaxX) - x;
        }

        // Y축: 상하 양끝 타일을 완벽하게 포함하도록 셀 그리드 병합
        int y = Mathf.Min(aMinY, bMinY);
        int h = Mathf.Max(aMaxY, bMaxY) - y;

        return new RectInt(x, y, w, h);
    }

    void Divide(BridgeMapNode tree, int n)
    {
        if (n == divideCount) return; 

        int maxLength = Mathf.Max(tree.nodeRect.width, tree.nodeRect.height);
        int split = Mathf.RoundToInt(Random.Range(maxLength * minimumDevideRate, maxLength * maximumDivideRate));

        if (maxLength <= minNodeSizeToDivide || split <= 1 || (maxLength - split) <= 1) return;

        if (tree.nodeRect.width >= tree.nodeRect.height) 
        {
            tree.leftNode = new BridgeMapNode(new RectInt(tree.nodeRect.x, tree.nodeRect.y, split, tree.nodeRect.height));
            tree.rightNode = new BridgeMapNode(new RectInt(tree.nodeRect.x + split, tree.nodeRect.y, tree.nodeRect.width - split, tree.nodeRect.height));
            
            if (drawDivideLines) DrawLine(new Vector2(tree.nodeRect.x + split, tree.nodeRect.y), new Vector2(tree.nodeRect.x + split, tree.nodeRect.y + tree.nodeRect.height));
        }
        else
        {
            tree.leftNode = new BridgeMapNode(new RectInt(tree.nodeRect.x, tree.nodeRect.y, tree.nodeRect.width, split));
            tree.rightNode = new BridgeMapNode(new RectInt(tree.nodeRect.x, tree.nodeRect.y + split, tree.nodeRect.width, tree.nodeRect.height - split));
            
            if (drawDivideLines) DrawLine(new Vector2(tree.nodeRect.x, tree.nodeRect.y + split), new Vector2(tree.nodeRect.x + tree.nodeRect.width, tree.nodeRect.y + split));
        }

        tree.leftNode.parNode = tree; 
        tree.rightNode.parNode = tree;

        Divide(tree.leftNode, n + 1); 
        Divide(tree.rightNode, n + 1);
    }

    private RectInt GenerateRoom(BridgeMapNode tree, int n)
    {
        RectInt rect;
        if (n == divideCount || (tree.leftNode == null && tree.rightNode == null)) 
        {
            rect = tree.nodeRect;
            if (rect.width <= 2 || rect.height <= 2) 
            {
                leafNodes.Add(tree);
                return rect; 
            }

            int minW = Mathf.Max(2, Mathf.RoundToInt(rect.width * minRoomSizeRatio));
            int maxW = Mathf.Max(minW, Mathf.RoundToInt(rect.width * maxRoomSizeRatio));
            int minH = Mathf.Max(2, Mathf.RoundToInt(rect.height * minRoomSizeRatio));
            int maxH = Mathf.Max(minH, Mathf.RoundToInt(rect.height * maxRoomSizeRatio));

            int width = Random.Range(minW, Mathf.Max(minW + 1, maxW)); 
            int height = Random.Range(minH, Mathf.Max(minH + 1, maxH));  
            
            int x = rect.x + Random.Range(0, rect.width - width + 1);
            int y = rect.y + Random.Range(0, rect.height - height + 1);        
           
            rect = new RectInt(x, y, width, height);
            tree.roomRect = rect; 

            leafNodes.Add(tree); 

            if (drawRoomLines) DrawRectangle(rect);         
            return rect; 
        }
        else
        {
            if (tree.leftNode != null) tree.leftNode.roomRect = GenerateRoom(tree.leftNode, n + 1);
            if (tree.rightNode != null) tree.rightNode.roomRect = GenerateRoom(tree.rightNode, n + 1);
            rect = tree.leftNode != null ? tree.leftNode.roomRect : tree.nodeRect;
            return rect; 
        }
    }

    private void SaveMapToJson()
    {
        int w = finalBridgeZone.width;
        int h = finalBridgeZone.height;

        char[,] grid = new char[h, w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                grid[y, x] = '#';
            }
        }

        foreach (var node in leafNodes)
        {
            if (node.roomRect.width > 0 && node.roomRect.height > 0)
            {
                int startX = node.roomRect.x - finalBridgeZone.x;
                int startY = node.roomRect.y - finalBridgeZone.y;

                for (int y = startY; y < startY + node.roomRect.height; y++)
                {
                    for (int x = startX; x < startX + node.roomRect.width; x++)
                    {
                        // 사방 1칸 외곽 테두리선 보장
                        if (x > 0 && x < w - 1 && y > 0 && y < h - 1)
                        {
                            grid[y, x] = '.';
                        }
                    }
                }
            }
        }

        MapJsonData jsonData = new MapJsonData();
        jsonData.width = w;
        jsonData.height = h;
        jsonData.startX = finalBridgeZone.x;
        jsonData.startY = finalBridgeZone.y;

        for (int y = 0; y < h; y++)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int x = 0; x < w; x++)
            {
                sb.Append(grid[y, x]);
            }
            jsonData.mapGrid.Add(sb.ToString());
        }

        string jsonString = JsonUtility.ToJson(jsonData, true);

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folderPath = Path.Combine(projectRoot, "tmpOutput");
        
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string filePath = Path.Combine(folderPath, "BridgeMapData.json");
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        
        File.WriteAllText(filePath, jsonString);

        Debug.Log($"[JSON 덮어쓰기 완료] 16px 그리드 맞춤 적용 | 경로: {filePath}");
    }

    // --- (라인 렌더러 시각화에도 타일 유닛 스케일을 곱해 완벽 매칭) ---
    private void DrawMapOutline(RectInt rect) {
        float s = (unityGrid != null) ? unityGrid.cellSize.x : 1.0f;
        GameObject go = new GameObject("Bridge_Total_Outline"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, Color.white, 0.15f); lr.positionCount = 4; lr.loop = true;
        lr.SetPosition(0, new Vector2(rect.x * s, rect.y * s)); lr.SetPosition(1, new Vector2((rect.x + rect.width) * s, rect.y * s)); lr.SetPosition(2, new Vector2((rect.x + rect.width) * s, (rect.y + rect.height) * s)); lr.SetPosition(3, new Vector2(rect.x * s, (rect.y + rect.height) * s));
    }
    private void DrawLine(Vector2 from, Vector2 to) {
        float s = (unityGrid != null) ? unityGrid.cellSize.x : 1.0f;
        GameObject go = new GameObject("BSP_Divide_Line"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, Color.red, 0.06f); lr.positionCount = 2;
        lr.SetPosition(0, from * s); lr.SetPosition(1, to * s);
    }
    private void DrawRectangle(RectInt rect) {
        float s = (unityGrid != null) ? unityGrid.cellSize.x : 1.0f;
        GameObject go = new GameObject("Bridge_Room_Line"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, Color.green, 0.09f); lr.positionCount = 4; lr.loop = true;
        lr.SetPosition(0, new Vector2(rect.x * s, rect.y * s)); lr.SetPosition(1, new Vector2((rect.x + rect.width) * s, rect.y * s)); lr.SetPosition(2, new Vector2((rect.x + rect.width) * s, (rect.y + rect.height) * s)); lr.SetPosition(3, new Vector2(rect.x * s, (rect.y + rect.height) * s));
    }
    private void SetupLineRenderer(LineRenderer lr, Color color, float width) { lr.startWidth = width; lr.endWidth = width; lr.useWorldSpace = true; Material defaultMat = new Material(Shader.Find("Sprites/Default")); lr.material = defaultMat; lr.startColor = color; lr.endColor = color; }
}