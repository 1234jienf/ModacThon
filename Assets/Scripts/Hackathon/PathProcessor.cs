using UnityEngine;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class OutputPathData
{
    public int width;
    public int height;
    public int startX;
    public int startY;
    public List<string> mapGrid = new List<string>();
}

public class PathProcessor : MonoBehaviour
{
    [Header("=== 펄린 노이즈 하이퍼파라미터 ===")]
    [Tooltip("펄린 노이즈 주파수 (낮을수록 완만한 곡선, 높을수록 자잘하게 꺾임)")]
    [SerializeField] private float noiseScale = 0.25f;
    
    [Tooltip("길의 왜곡 강도 (방 안에서 구불거리는 최대 반경)")]
    [SerializeField] private float noiseMagnitude = 1.8f;
    
    [Header("=== 길 두께 확률 설정 ===")]
    [Tooltip("체크 시 하이퍼파라미터를 무시하고 방마다 1(40%), 2(50%), 3(10%) 확률로 두께를 결정합니다.")]
    [SerializeField] private bool useRandomPathWidth = true;

    [Tooltip("useRandomPathWidth가 꺼져있을 때 사용할 고정 길의 너비 (1 ~ 3)")]
    [Range(1, 3)] 
    [SerializeField] private int pathWidth = 2;

    [Header("=== 이미지 출력 설정 ===")]
    [Tooltip("하나의 타일(점)을 가로세로 몇 픽셀로 표현할지 설정 (값이 클수록 고해상도)")]
    [Range(1, 32)]
    [SerializeField] private int pointSize = 8;

    [Header("=== Lake 배치 ===")]
    [SerializeField] private bool placeLakesAfterPaths = true;
    [SerializeField] private TilemapDataProvider mapAProvider;
    [SerializeField] private TilemapDataProvider mapBProvider;
    [Range(0, 4)]
    [SerializeField] private int lakeMinDistanceFromPath = 1;
    [Range(1, 3)]
    [SerializeField] private int lakePatchRadiusMin = 1;
    [Range(1, 4)]
    [SerializeField] private int lakePatchRadiusMax = 2;
    [SerializeField] private bool useFieldLakeRatios = true;
    [Range(0f, 0.3f)]
    [SerializeField] private float maxLakeCoverage = 0.12f;
    [Range(0f, 1f)]
    [SerializeField] private float groundLakeChanceLeft = 0.06f;
    [Range(0f, 1f)]
    [SerializeField] private float groundLakeChanceRight = 0.14f;
    [SerializeField] private int lakeRandomSeed = 0;

    [ContextMenu("방별 중심점 기반 펄린 패스 생성 및 이미지 저장")]
    public void ProcessRoomPaths()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string inputFilePath = Path.Combine(projectRoot, "tmpOutput", "BridgeMapData.json");

        if (!File.Exists(inputFilePath))
        {
            Debug.LogError($"원본 브릿지 데이터 파일이 없습니다! 경로: {inputFilePath}");
            return;
        }

        string jsonString = File.ReadAllText(inputFilePath);
        InputMapData originalData = JsonUtility.FromJson<InputMapData>(jsonString);

        int w = originalData.width;
        int h = originalData.height;

        char[,] grid = BridgeMapJsonUtility.LoadGridFromJson(originalData);

        string folderPath = Path.Combine(projectRoot, "tmpOutput");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        // ====================================================================
        // [신규 요구사항] 길을 만들기 전, 원본 비교용 이미지 저장 (#: 검은색, .: 흰색)
        // ====================================================================
        SaveBeforeMapAsImage(grid, w, h, folderPath);


        // 1. 맵 안에서 '.'으로 구성된 독립된 방들의 구역(RectInt) 추출
        List<RectInt> detectedRooms = FindIndividualRooms(grid, w, h);
        
        float seedX = Random.Range(0f, 5000f);
        float seedY = Random.Range(0f, 5000f);

        // 2. 각 방을 순회하며 중심점 기반 펄린 노이즈 연산 수행
        foreach (RectInt room in detectedRooms)
        {
            Vector2Int center = new Vector2Int(room.x + room.width / 2, room.y + room.height / 2);
            List<Vector2Int> entrances = FindRoomEntrances(grid, room, w, h);

            foreach (Vector2Int entrance in entrances)
            {
                DigNoisePathToCenter(grid, entrance, center, room, seedX, seedY);
            }

            // [우선순위 제약] 중심점은 무조건 P로 확정
            if (center.x >= 0 && center.x < w && center.y >= 0 && center.y < h)
            {
                grid[center.y, center.x] = 'P';
            }

            // 노이즈 굴착 종료 후 남아있는 '.' 타일들은 'G'로 채움
            for (int y = room.y; y < room.yMax; y++)
            {
                for (int x = room.x; x < room.xMax; x++)
                {
                    if (grid[y, x] == '.')
                    {
                        grid[y, x] = 'G';
                    }
                }
            }
        }

        if (placeLakesAfterPaths)
        {
            TryResolveMapProviders();
            BridgeLakeRandomPlacer.Settings lakeSettings = BuildLakeSettings();
            int lakeCells = BridgeLakeRandomPlacer.PlaceLakes(grid, mapAProvider, mapBProvider, lakeSettings);
            Debug.Log($"[Lake 배치] {lakeCells} cells -> JSON에 'w' 토큰으로 저장됩니다.");
        }

        // 3. 변환이 완료된 그리드를 최종 BridgeMapData_Path.json 파일로 저장
        BridgeMapJsonUtility.MarkBridgeEndpoints(grid);

        OutputPathData outputData = new OutputPathData();
        outputData.width = w;
        outputData.height = h;
        outputData.startX = originalData.startX;
        outputData.startY = originalData.startY;

        List<string> outputRows = new List<string>();
        BridgeMapJsonUtility.WriteGridRowsTopFirst(outputRows, grid);
        outputData.mapGrid = outputRows;

        string outputPath = Path.Combine(folderPath, "BridgeMapData_Path.json");
        if (File.Exists(outputPath)) File.Delete(outputPath);
        File.WriteAllText(outputPath, JsonUtility.ToJson(outputData, true));

        // 4. 연산 완료 후 최종 PNG 이미지 빌드 및 비트맵 출력 (#: 검은색, P: 옅은 회색, G: 흰색)
        SaveAfterMapAsImage(grid, w, h, folderPath);

        Debug.Log($"[가공 완료] 비교용 전/후 이미지 및 JSON 데이터 저장 완료! (경로: {folderPath})");

        TriggerBridgePathRunners();
        TriggerAsciiMapRenderers();
    }

    private BridgeLakeRandomPlacer.Settings BuildLakeSettings()
    {
        BridgeLakeRandomPlacer.Settings settings = BridgeLakeRandomPlacer.CreateDefault();
        settings.enabled = true;
        settings.groundLakeChanceLeft = groundLakeChanceLeft;
        settings.groundLakeChanceRight = groundLakeChanceRight;
        settings.minDistanceFromPath = lakeMinDistanceFromPath;
        settings.patchRadiusMin = lakePatchRadiusMin;
        settings.patchRadiusMax = lakePatchRadiusMax;
        settings.useFieldLakeRatios = useFieldLakeRatios;
        settings.maxLakeCoverage = maxLakeCoverage;
        settings.randomSeed = lakeRandomSeed;
        return settings;
    }

    private void TryResolveMapProviders()
    {
        if (mapAProvider != null && mapBProvider != null)
            return;

        TilemapDataProvider[] providers = FindObjectsOfType<TilemapDataProvider>(true);
        foreach (TilemapDataProvider provider in providers)
        {
            if (provider == null)
                continue;

            string name = provider.gameObject.name.ToLowerInvariant();
            if (mapAProvider == null && (name.Contains("field 1") || name.Contains("field1") || name.Contains("map_a")))
                mapAProvider = provider;
            if (mapBProvider == null && (name.Contains("field 3") || name.Contains("field3") || name.Contains("map_b")))
                mapBProvider = provider;
        }
    }

    private void TriggerAsciiMapRenderers()
    {
        AsciiMapTilemapRenderer[] renderers = FindObjectsOfType<AsciiMapTilemapRenderer>(true);
        foreach (AsciiMapTilemapRenderer renderer in renderers)
        {
            if (renderer != null)
                renderer.RenderFromInspectorInput();
        }
    }

    private void TriggerBridgePathRunners()
    {
        AutoPathRunner[] runners = FindObjectsOfType<AutoPathRunner>(true);
        foreach (AutoPathRunner runner in runners)
        {
            if (runner.useBridgePathJson)
            {
                runner.StartRun();
            }
        }
    }

    // [신규 추가] 연산 전 원본 비교용 이미지 저장 메서드 (#: 검은색, .: 흰색, 기존 외곽선 P도 기본 노출)
    private void SaveBeforeMapAsImage(char[,] grid, int w, int h, string folderPath)
    {
        int texWidth = w * pointSize;
        int texHeight = h * pointSize;

        Texture2D mapTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGB24, false);
        mapTexture.filterMode = FilterMode.Point;

        Color colorWall = Color.black; // #: 검은색
        Color colorRoom = Color.white; // .: 흰색
        Color colorGate = new Color(0.8f, 0.8f, 0.8f); // 기존 외곽선 P는 구분을 위해 옅은 회색 처리

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color targetColor = colorWall;
                char tile = grid[y, x];

                if (tile == '#') targetColor = colorWall;
                else if (tile == '.') targetColor = colorRoom;
                else if (tile == 'P') targetColor = colorGate;

                int pixelStartX = x * pointSize;
                int pixelStartY = (h - 1 - y) * pointSize;

                for (int py = 0; py < pointSize; py++)
                {
                    for (int px = 0; px < pointSize; px++)
                    {
                        mapTexture.SetPixel(pixelStartX + px, pixelStartY + py, targetColor);
                    }
                }
            }
        }

        mapTexture.Apply();
        byte[] pngBytes = mapTexture.EncodeToPNG();
        string imgPath = Path.Combine(folderPath, "BridgeMapData_Before.png");
        
        if (File.Exists(imgPath)) File.Delete(imgPath);
        File.WriteAllBytes(imgPath, pngBytes);
        DestroyImmediate(mapTexture);
    }

    // 연산 완료 후 최종 결과 이미지 저장 메서드 (#: 검은색, P: 옅은 회색, G: 흰색)
    private void SaveAfterMapAsImage(char[,] grid, int w, int h, string folderPath)
    {
        int texWidth = w * pointSize;
        int texHeight = h * pointSize;

        Texture2D mapTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGB24, false);
        mapTexture.filterMode = FilterMode.Point; 

        Color colorWall = Color.black;                 // # : 검은색
        Color colorPath = new Color(0.8f, 0.8f, 0.8f); // P : 옅은 회색
        Color colorGround = Color.white;               // G : 흰색

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color targetColor = colorGround;
                char tile = grid[y, x];

                if (tile == '#') targetColor = colorWall;
                else if (tile == 'P') targetColor = colorPath;
                else if (tile == 'G') targetColor = colorGround;

                int pixelStartX = x * pointSize;
                int pixelStartY = (h - 1 - y) * pointSize;

                for (int py = 0; py < pointSize; py++)
                {
                    for (int px = 0; px < pointSize; px++)
                    {
                        mapTexture.SetPixel(pixelStartX + px, pixelStartY + py, targetColor);
                    }
                }
            }
        }

        mapTexture.Apply();
        byte[] pngBytes = mapTexture.EncodeToPNG();
        string imgPath = Path.Combine(folderPath, "BridgeMapData_Path.png");
        
        if (File.Exists(imgPath)) File.Delete(imgPath);
        File.WriteAllBytes(imgPath, pngBytes);
        DestroyImmediate(mapTexture); 
    }

    // 끊김 현상을 완벽히 방지하기 위해 연속성 채우기(Bresenham/Line Fill 방식)가 도입된 굴착 메서드
    private void DigNoisePathToCenter(char[,] grid, Vector2Int start, Vector2Int center, RectInt room, float seedX, float seedY)
    {
        // 1. 가중치 확률에 따른 방별 두께 결정 (1: 40%, 2: 50%, 3: 10%)
        int currentWidth = pathWidth;
        if (useRandomPathWidth)
        {
            int rand = Random.Range(0, 100); 
            if (rand < 40) currentWidth = 1;       
            else if (rand < 90) currentWidth = 2;  
            else currentWidth = 3;                 
        }

        int h = grid.GetLength(0);
        int w = grid.GetLength(1);

        // 이전 단계의 좌표를 기억하기 위한 변수 (첫 시작은 start 위치)
        Vector2Int lastPos = start;

        bool isHorizontal = Mathf.Abs(center.x - start.x) >= Mathf.Abs(center.y - start.y);

        if (isHorizontal)
        {
            // start.x에서 center.x로 순방향이든 역방향이든 올바르게 루프가 돌도록 방향 가중치 설정
            int stepX = (center.x >= start.x) ? 1 : -1;
            int currentX = start.x;
            bool keepGoing = true;

            while (keepGoing)
            {
                if (currentX == center.x) keepGoing = false;

                // 선형 보간 비율 계산
                float t = 0f;
                if (center.x != start.x)
                    t = (float)(currentX - start.x) / (center.x - start.x);

                float baseLineY = Mathf.Lerp(start.y, center.y, t);

                // 펄린 노이즈 기반 최종 Y 좌표 산출
                float perlin = Mathf.PerlinNoise(currentX * noiseScale + seedX, baseLineY * noiseScale + seedY);
                float offset = (perlin - 0.5f) * 2.0f * noiseMagnitude;
                int targetY = Mathf.RoundToInt(baseLineY + offset);

                // [핵심 보완] 직전 좌표(lastPos.y)와 현재 좌표(targetY) 사이의 급격한 경사가 생기면 그 사이를 메워줌
                int minBufY = Mathf.Min(lastPos.y, targetY);
                int maxBufY = Mathf.Max(lastPos.y, targetY);

                for (int fillY = minBufY; fillY <= maxBufY; fillY++)
                {
                    int radius = currentWidth / 2;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int evalY = fillY + dy;
                        // P 최우선 순위 유지 (전체 맵 경계 체크)
                        if (evalY >= 0 && evalY < h && currentX >= 0 && currentX < w)
                        {
                            grid[evalY, currentX] = 'P';
                        }
                    }
                }

                // 직전 위치 업데이트 및 X축 전진
                lastPos = new Vector2Int(currentX, targetY);
                currentX += stepX;
            }
        }
        else
        {
            // 세로축 중심 진행형 루프
            int stepY = (center.y >= start.y) ? 1 : -1;
            int currentY = start.y;
            bool keepGoing = true;

            while (keepGoing)
            {
                if (currentY == center.y) keepGoing = false;

                float t = 0f;
                if (center.y != start.y)
                    t = (float)(currentY - start.y) / (center.y - start.y);

                float baseLineX = Mathf.Lerp(start.x, center.x, t);

                float perlin = Mathf.PerlinNoise(baseLineX * noiseScale + seedX, currentY * noiseScale + seedY);
                float offset = (perlin - 0.5f) * 2.0f * noiseMagnitude;
                int targetX = Mathf.RoundToInt(baseLineX + offset);

                // [핵심 보완] 직전 좌표(lastPos.x)와 현재 좌표(targetX) 사이의 공백을 메워줌
                int minBufX = Mathf.Min(lastPos.x, targetX);
                int maxBufX = Mathf.Max(lastPos.x, targetX);

                for (int fillX = minBufX; fillX <= maxBufX; fillX++)
                {
                    int radius = currentWidth / 2;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int evalX = fillX + dx;
                        if (currentY >= 0 && currentY < h && evalX >= 0 && evalX < w)
                        {
                            grid[currentY, evalX] = 'P';
                        }
                    }
                }

                lastPos = new Vector2Int(targetX, currentY);
                currentY += stepY;
            }
        }
    }

    private List<RectInt> FindIndividualRooms(char[,] grid, int w, int h)
    {
        List<RectInt> rooms = new List<RectInt>();
        bool[,] visited = new bool[h, w];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (grid[y, x] == '.' && !visited[y, x])
                {
                    int minX = x, maxX = x;
                    int minY = y, maxY = y;

                    Queue<Vector2Int> queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(x, y));
                    visited[y, x] = true;

                    while (queue.Count > 0)
                    {
                        Vector2Int curr = queue.Dequeue();
                        minX = Mathf.Min(minX, curr.x); maxX = Mathf.Max(maxX, curr.x);
                        minY = Mathf.Min(minY, curr.y); maxY = Mathf.Max(maxY, curr.y);

                        Vector2Int[] neighbors = { new Vector2Int(curr.x+1, curr.y), new Vector2Int(curr.x-1, curr.y), new Vector2Int(curr.x, curr.y+1), new Vector2Int(curr.x, curr.y-1) };
                        foreach (var next in neighbors)
                        {
                            if (next.x >= 0 && next.x < w && next.y >= 0 && next.y < h)
                            {
                                if (grid[next.y, next.x] == '.' && !visited[next.y, next.x])
                                {
                                    visited[next.y, next.x] = true;
                                    queue.Enqueue(next);
                                }
                            }
                        }
                    }
                    rooms.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
                }
            }
        }
        return rooms;
    }

    private List<Vector2Int> FindRoomEntrances(char[,] grid, RectInt room, int w, int h)
    {
        List<Vector2Int> entrances = new List<Vector2Int>();
        HashSet<Vector2Int> uniqueEntrances = new HashSet<Vector2Int>();

        for (int x = room.x; x < room.xMax; x++)
        {
            if (room.y - 1 >= 0 && grid[room.y - 1, x] == 'P') uniqueEntrances.Add(new Vector2Int(x, room.y));
            if (room.yMax < h && grid[room.yMax, x] == 'P') uniqueEntrances.Add(new Vector2Int(x, room.yMax - 1));
        }
        for (int y = room.y; y < room.yMax; y++)
        {
            if (room.x - 1 >= 0 && grid[y, room.x - 1] == 'P') uniqueEntrances.Add(new Vector2Int(room.x, y));
            if (room.xMax < w && grid[y, room.xMax] == 'P') uniqueEntrances.Add(new Vector2Int(room.xMax - 1, y));
        }

        entrances.AddRange(uniqueEntrances);
        return entrances;
    }
}