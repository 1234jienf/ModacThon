using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;
using System.Collections.Generic;

// --- JSON 저장을 위한 데이터 구조체 정의 ---
[System.Serializable]
public class MapJsonData
{
    public int width;
    public int height;
    public int startX;
    public int startY;
    public List<string> mapGrid = new List<string>(); // 한 줄씩 저장되는 2차원 맵 데이터
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

    [Header("=== BSP 분할 설정 (A안) ===")]
    [Tooltip("두 맵 사이의 공백 공간을 총 몇 번 쪼갤지 결정합니다.")]
    [SerializeField] private int divideCount = 3; 
    [SerializeField] float minimumDevideRate = 0.25f; // 불규칙성을 위해 범위 확장
    [SerializeField] float maximumDivideRate = 0.75f; 

    [Header("=== [NEW] 방 크기 상세 설정 ===")]
    [Tooltip("분할된 상자(노드) 크기 대비 '최소' 몇 % 크기로 방을 만들지 결정합니다. (0.1 ~ 1.0)")]
    [Range(0.2f, 0.9f)] [SerializeField] private float minRoomSizeRatio = 0.5f; // 기존 0.3f에서 0.5f(50%)로 상향
    [Tooltip("분할된 상자(노드) 크기 대비 '최대' 몇 % 크기로 방을 만들지 결정합니다. (0.1 ~ 1.0)")]
    [Range(0.3f, 1.0f)] [SerializeField] private float maxRoomSizeRatio = 0.9f; 
    [Tooltip("상자의 가로나 세로가 이 수치 이하로 작아지면 더 이상 쪼개지 않습니다.")]
    [SerializeField] private int minNodeSizeToDivide = 10; // 기존 고정값 4에서 인스펙터 노출 및 기본값 상향

    [Header("=== 시각화 필터 ===")]
    [SerializeField] private bool drawTotalOutline = true;  
    [SerializeField] private bool drawDivideLines = true;   
    [SerializeField] private bool drawRoomLines = true;     

    // 모든 리프 노드(최종 방 구획)를 추적하기 위한 리스트
    private List<BridgeMapNode> leafNodes = new List<BridgeMapNode>();
    private RectInt finalBridgeZone;

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

        Bounds boundsA = GetActualTilemapBounds(map_A);
        Bounds boundsB = GetActualTilemapBounds(map_B);

        finalBridgeZone = CalculateBridgeZone(boundsA, boundsB);

        if (finalBridgeZone.width <= 0 || finalBridgeZone.height <= 0)
        {
            Debug.LogError($"[오류] 사잇공간을 생성할 수 없습니다. 크기: {finalBridgeZone.width}x{finalBridgeZone.height}.");
            return;
        }

        if (drawTotalOutline)
        {
            DrawMapOutline(finalBridgeZone); 
        }

        BridgeMapNode root = new BridgeMapNode(finalBridgeZone);
        Divide(root, 0);
        GenerateRoom(root, 0);

        // [핵심] 방 생성이 모두 끝난 후, 수집된 데이터를 바탕으로 문자열 맵을 가공하고 JSON으로 저장합니다.
        SaveMapToJson();
    }

    // --- (이전과 동일한 맵 경계선 구하기 로직) ---
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
        Collider2D col = mapObj.GetComponentInChildren<Collider2D>();
        if (col != null) return col.bounds;
        return new Bounds(mapObj.transform.position, Vector3.one * 5f);
    }

    private RectInt CalculateBridgeZone(Bounds bA, Bounds bB)
    {
        int x = 0; int w = 0;
        if (bA.max.x <= bB.min.x) { x = Mathf.FloorToInt(bA.max.x); w = Mathf.CeilToInt(bB.min.x) - x; }
        else if (bB.max.x <= bA.min.x) { x = Mathf.FloorToInt(bB.max.x); w = Mathf.CeilToInt(bA.min.x) - x; }
        else { float gapMinX = Mathf.Max(bA.min.x, bB.min.x); float gapMaxX = Mathf.Min(bA.max.x, bB.max.x); x = Mathf.FloorToInt(gapMinX); w = Mathf.CeilToInt(gapMaxX) - x; }
        int y = Mathf.FloorToInt(Mathf.Min(bA.min.y, bB.min.y));
        float maxY = Mathf.Max(bA.max.y, bB.max.y);
        int h = Mathf.CeilToInt(maxY) - y;
        return new RectInt(x, y, w, h);
    }

    void Divide(BridgeMapNode tree, int n)
    {
        if (n == divideCount) return; 

        int maxLength = Mathf.Max(tree.nodeRect.width, tree.nodeRect.height);
        int split = Mathf.RoundToInt(Random.Range(maxLength * minimumDevideRate, maxLength * maximumDivideRate));

        // [수정] 인스펙터에서 설정한 최소 노드 크기(minNodeSizeToDivide)를 기준으로 분할 중단 여부 결정
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

            // [수정] 인스펙터에서 조절 가능한 비율(Ratio)을 적용하여 최소/최대 방 크기 계산
            int minW = Mathf.Max(2, Mathf.RoundToInt(rect.width * minRoomSizeRatio));
            int maxW = Mathf.Max(minW, Mathf.RoundToInt(rect.width * maxRoomSizeRatio));
            int minH = Mathf.Max(2, Mathf.RoundToInt(rect.height * minRoomSizeRatio));
            int maxH = Mathf.Max(minH, Mathf.RoundToInt(rect.height * maxRoomSizeRatio));

            // 가끔 max - 1이 min보다 작아지는 오차 방지를 위해 안전 마진 추가
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

    /// <summary>
    /// [새로 추가] 생성된 구역 데이터들을 바탕으로 차원 문자열 매트릭스를 만들어 JSON으로 추출합니다.
    /// </summary>
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
                        if (x >= 0 && x < w && y >= 0 && y < h)
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
            jsonData.mapGrid.Add(sb.ToString()); // 대문자 Add 반영
        }

        string jsonString = JsonUtility.ToJson(jsonData, true);

        // 프로젝트 루트 폴더 기준 하위 tmpOutput 폴더 설정
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folderPath = Path.Combine(projectRoot, "tmpOutput");
        
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // 최종 파일 저장 경로
        string filePath = Path.Combine(folderPath, "BridgeMapData.json");
        
        // ================= [강제 덮어쓰기 로직 추가] =================
        // 이미 파일이 존재한다면, 권한 꼬임이나 파일 잠금을 방지하기 위해 먼저 완전히 삭제합니다.
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        
        // 새롭게 파일을 생성하고 스트림을 완전히 닫아 안전하게 저장합니다.
        File.WriteAllText(filePath, jsonString);
        // ============================================================

        Debug.Log($"[JSON 덮어쓰기 완료] 커스텀 경로: {filePath}\n디버그 미리보기:\n" + string.Join("\n", jsonData.mapGrid));
    }

    // --- (이전과 동일한 라인 렌더러 시각화 헬퍼 기능들) ---
    private void DrawMapOutline(RectInt rect) { GameObject go = new GameObject("Bridge_Total_Outline"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, Color.white, 0.15f); lr.positionCount = 4; lr.loop = true; lr.SetPosition(0, new Vector2(rect.x, rect.y)); lr.SetPosition(1, new Vector2(rect.x + rect.width, rect.y)); lr.SetPosition(2, new Vector2(rect.x + rect.width, rect.y + rect.height)); lr.SetPosition(3, new Vector2(rect.x, rect.y + rect.height)); }
    private void DrawLine(Vector2 from, Vector2 to) { GameObject go = new GameObject("BSP_Divide_Line"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, Color.red, 0.06f); lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, to); }
    private void DrawRectangle(RectInt rect) { GameObject go = new GameObject("Bridge_Room_Line"); go.transform.SetParent(this.transform); LineRenderer lr = go.AddComponent<LineRenderer>(); SetupLineRenderer(lr, Color.green, 0.09f); lr.positionCount = 4; lr.loop = true; lr.SetPosition(0, new Vector2(rect.x, rect.y)); lr.SetPosition(1, new Vector2(rect.x + rect.width, rect.y)); lr.SetPosition(2, new Vector2(rect.x + rect.width, rect.y + rect.height)); lr.SetPosition(3, new Vector2(rect.x, rect.y + rect.height)); }
    private void SetupLineRenderer(LineRenderer lr, Color color, float width) { lr.startWidth = width; lr.endWidth = width; lr.useWorldSpace = true; Material defaultMat = new Material(Shader.Find("Sprites/Default")); lr.material = defaultMat; lr.startColor = color; lr.endColor = color; }
}