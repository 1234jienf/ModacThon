using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class AsciiMapTilemapRenderer : MonoBehaviour
{
    public enum OriginMode
    {
        ZeroBased,
        UseJsonStart
    }

    [Header("Input")]
    public string inputRelativePath = "tmpOutput/BridgeMapData_Path.json";
    public bool loadFromFileOnStart = true;
    public bool preferFileInput = true;

    [TextArea(8, 30)]
    public string inputText =
@"#####
#GGG#
#GPG#
#GGG#
#####";

    [Header("Output")]
    [Tooltip("Layer-specific Tilemap이 비어 있을 때 사용하는 fallback Tilemap입니다.")]
    public Tilemap targetTilemap;
    public Tilemap groundTilemap;
    public Tilemap dirtTilemap;
    public Tilemap lakeTilemap;
    public Tilemap wallTilemap;
    public Tilemap markerTilemap;
    public bool clearBeforeRender = false;
    public bool clearSpawnedPrefabsBeforeRender = true;
    public OriginMode originMode = OriginMode.UseJsonStart; // 앞의 o를 대문자 O로 수정
    public Vector3Int extraOffset = Vector3Int.zero;
    public bool flipRowsVertically = true;

    [Header("Layer Rendering")]
    public bool renderGroundTiles = true;
    public bool fillGroundArea = true;
    public bool renderDirtTiles = true;
    public bool renderLakeTiles = true;
    public bool renderWallTiles = true;

    [Header("Images")]
    public string imageAssetFolder = "Assets/modak_image_test";
    public string blendedTilesFolder = "HackathonAI/runs/20260627_222834/blended_tiles";
    public bool useBlendedTiles = true;
    public bool autoLoadSpritesFromAssetFolder = true;
    [Tooltip("Sprite 크기를 타일 셀 안에서 조금 키우거나 줄입니다. 1은 원본 크기입니다.")]
    public float tileSpriteScale = 1.05f;
    public bool dotMeansGrass = true;

    [Range(0.01f, 0.5f)]
    [Tooltip("map_A와 map_B가 섞이는 경계면의 두께(거리)입니다. 값이 클수록 넓은 범위가 확률적으로 섞입니다.")]
    public float blending = 0.15f;

    [Header("Start / Goal")]
    public bool drawFloorUnderMarkers = true;
    public Sprite startSprite;
    public Sprite goalSprite;
    public GameObject startPrefab;
    public GameObject goalPrefab;

    private readonly Dictionary<string, TileBase> _runtimeTiles = new Dictionary<string, TileBase>();
    private readonly Dictionary<string, Sprite> _spriteLookup = new Dictionary<string, Sprite>();
    private readonly HashSet<string> _missingSpriteWarnings = new HashSet<string>();
    private bool? _blendedTilesAvailable;
    private const string SpawnRootName = "ASCII Map Spawned Objects";
    private const string LakeWaterAnimationsFolder =
        "Assets/Sprites/map/Tilesets/Wastelands/Animated Water/Animations";

    private void Start()
    {
        if (loadFromFileOnStart)
            RenderFromInspectorInput();
    }

    [ContextMenu("Render ASCII Map")]
    public void RenderFromInspectorInput()
    {
        if (!HasAnyOutputTilemap())
        {
            Debug.LogError("Assign at least one output Tilemap.");
            return;
        }

        string input = GetInputText();
        AsciiTilemapJsonData mapData = ParseInputMap(input);
        if (mapData == null || mapData.mapGrid == null || mapData.mapGrid.Length == 0)
        {
            Debug.LogError("ASCII map input has no rows.");
            return;
        }

        Render(mapData);
    }

    public void Render(AsciiTilemapJsonData mapData)
    {
        if (!HasAnyOutputTilemap())
        {
            Debug.LogError("Assign at least one output Tilemap.");
            return;
        }

        LoadSpriteLookup();
        _missingSpriteWarnings.Clear();
        _blendedTilesAvailable = null;

        // 1. 초기화 순서 제어 (순서가 뒤섞여 지워지는 문제 방지)
        if (clearBeforeRender)
            ClearOutputTilemaps();
        if (clearSpawnedPrefabsBeforeRender)
            ClearSpawnedPrefabs();

        string[][] tokenRows = TokenizeRows(mapData.mapGrid);
        int height = mapData.height > 0 ? mapData.height : tokenRows.Length;
        int width = mapData.width > 0 ? mapData.width : GetMaxTokenRowWidth(tokenRows);
        Vector3Int origin = GetOrigin(mapData);

        if (renderGroundTiles && fillGroundArea)
            FillGroundArea(origin, width, height);

        // 2. [수정] 선행 레이어(Dirt 패스) 선 배치 프로세스
        // Wall을 배치하기 전에, 모든 Grid를 돌며 벽 밑에 깔려야 할 Dirt 타일을 먼저 완벽하게 굽습니다.
        for (int row = 0; row < height; row++)
        {
            string[] tokens = row < tokenRows.Length ? tokenRows[row] : new string[0];
            for (int x = 0; x < width; x++)
            {
                string token = x < tokens.Length ? tokens[x] : string.Empty;
                if (token == "#")
                {
                    int cellY = flipRowsVertically ? height - row - 1 : row;
                    Vector3Int cell = origin + new Vector3Int(x, cellY, 0);
                    CheckAndApplyUnderWallPath(tokenRows, row, x, cell);
                }
            }
        }

        // 3. 메인 레이어(Wall 및 일반 토큰) 배치 프로세스
        for (int row = 0; row < height; row++)
        {
            string[] tokens = row < tokenRows.Length ? tokenRows[row] : new string[0];
            for (int x = 0; x < width; x++)
            {
                string token = x < tokens.Length ? tokens[x] : string.Empty;
                int cellY = flipRowsVertically ? height - row - 1 : row;
                Vector3Int cell = origin + new Vector3Int(x, cellY, 0);
                RenderToken(cell, token, tokenRows, row, x, cellY, width, height);
            }
        }

        RefreshOutputTilemaps();
        Debug.Log($"Rendered ASCII map to Tilemap. Size: {width}x{height}");
    }

    private void RenderToken(
        Vector3Int cell,
        string token,
        string[][] tokenRows,
        int row,
        int x,
        int cellY,
        int width,
        int height)
    {
        if (string.IsNullOrEmpty(token) || token == " ")
            return;

        if (token == "S")
        {
            RenderMarker(cell, startSprite, startPrefab);
            return;
        }

        string spriteKey = GetSpriteKey(token, tokenRows, row, x, cellY, width, height);
        if (string.IsNullOrEmpty(spriteKey))
            return;

        SetSpriteTile(cell, spriteKey);
    }

    private void FillGroundArea(Vector3Int origin, int width, int height)
    {
        for (int row = 0; row < height; row++)
        {
            int cellY = flipRowsVertically ? height - row - 1 : row;

            for (int x = 0; x < width; x++)
            {
                SetSpriteTile(
                    origin + new Vector3Int(x, cellY, 0),
                    GetPositionAwareSpriteKey("g_0", x, cellY, width, height));
            }
        }
    }

    private void RenderMarker(Vector3Int cell, Sprite markerSprite, GameObject markerPrefab)
    {
        if (drawFloorUnderMarkers)
            SetSpriteTile(cell, "g_0");

        if (markerSprite != null)
            GetMarkerTilemap().SetTile(cell, GetOrCreateTile($"marker-{markerSprite.name}", markerSprite));

        if (markerPrefab != null)
        {
            Vector3 worldPosition = GetReferenceTilemap().GetCellCenterWorld(cell);
            Instantiate(markerPrefab, worldPosition, Quaternion.identity, GetSpawnRoot());
        }
    }

    private string GetSpriteKey(string token, string[][] tokenRows, int row, int x, int cellY, int width, int height)
    {
        string baseKey;
        switch (token)
        {
            case "#":
                return GetWallDirectionalKey(tokenRows, row, x);
            case "D":
                return "blocker_wall";
            case "w":
                baseKey = GetLakeSpriteKey(tokenRows, row, x);
                break;
            case ".":
                baseKey = dotMeansGrass ? "g_0" : string.Empty;
                break;
            case "G":
            case "g":
            case "E":
                baseKey = "g_0";
                break;
            case "P":
                baseKey = GetDustSpriteKey(tokenRows, row, x);
                break;
            case "d":
                baseKey = "d_0";
                break;
            case "d2":
                baseKey = "d2_0";
                break;
            case "s":
                baseKey = "s_0";
                break;
            case "g_0":
            case "g_1":
            case "g_2":
            case "g_3":
            case "g_4":
            case "g_5":
            case "g_6":
            case "d_0":
            case "d_1":
            case "d_2":
            case "d_3":
            case "d_4":
            case "d_5":
            case "d_6":
            case "d2_0":
            case "d2_1":
            case "d2_2":
            case "d2_3":
            case "d2_4":
            case "d2_5":
            case "d2_6":
            case "s_0":
            case "s_1":
            case "s_2":
            case "s_3":
            case "s_4":
            case "s_5":
            case "s_6":
            case "w_0":
            case "w_1":
            case "w_2":
            case "w_3":
            case "w_4":
            case "w_5":
            case "w_6":
            case "w_7":
            case "w_8":
                baseKey = token;
                break;
            default:
                Debug.LogWarning($"Unknown ASCII tile token skipped: {token}");
                return string.Empty;
        }

        return GetPositionAwareSpriteKey(baseKey, x, cellY, width, height);
    }

    private string GetPositionAwareSpriteKey(string baseKey, int x, int y, int width, int height)
    {
        if (!useBlendedTiles || string.IsNullOrEmpty(baseKey) || !HasBlendedTilesAvailable())
            return baseKey;

        string blendSet = GetBlendSetForSpriteKey(baseKey);
        string directionFileStem = GetBlendDirectionFileStem(baseKey);
        if (string.IsNullOrEmpty(blendSet) || string.IsNullOrEmpty(directionFileStem))
            return baseKey;

        return $"{blendSet}_{directionFileStem}_{GetBlendRatioName(x, y, width, height)}";
    }

    private bool HasBlendedTilesAvailable()
    {
        if (!_blendedTilesAvailable.HasValue)
        {
            string groundDirectory = GetProjectRelativePath($"{blendedTilesFolder.TrimEnd('/', '\\')}/ground");
            _blendedTilesAvailable = Directory.Exists(groundDirectory);
        }

        return _blendedTilesAvailable.Value;
    }

    private static string GetBlendSetForSpriteKey(string spriteKey)
    {
        if (spriteKey.StartsWith("ground_", StringComparison.Ordinal) || spriteKey.StartsWith("g_", StringComparison.Ordinal) || spriteKey.StartsWith("s_", StringComparison.Ordinal))
            return "ground";
        if (spriteKey.StartsWith("path_", StringComparison.Ordinal) || spriteKey.StartsWith("d_", StringComparison.Ordinal) || spriteKey.StartsWith("d2_", StringComparison.Ordinal))
            return "path";

        return string.Empty;
    }

    private string GetWallDirectionalKey(string[][] tokenRows, int row, int x)
    {
        // 12시부터 시계방향 8방향 이웃 검사
        bool N = IsWallTokenAt(tokenRows, row - 1, x);     // 0: 북
        bool NE = IsWallTokenAt(tokenRows, row - 1, x + 1); // 1: 북동
        bool E = IsWallTokenAt(tokenRows, row, x + 1);     // 2: 동
        bool SE = IsWallTokenAt(tokenRows, row + 1, x + 1); // 3: 남동
        bool S = IsWallTokenAt(tokenRows, row + 1, x);     // 4: 남
        bool SW = IsWallTokenAt(tokenRows, row + 1, x - 1); // 5: 남서
        bool W = IsWallTokenAt(tokenRows, row, x - 1);     // 6: 서
        bool NW = IsWallTokenAt(tokenRows, row - 1, x - 1); // 7: 북서

        // -------------------------------------------------------------------------
        // 1. 안쪽 코너 판정 (Inner Corners)
        // -------------------------------------------------------------------------

        // -------------------------------------------------------------------------
        // 2. 외곽 모서리 코너 판정 (Outer Corners)
        // -------------------------------------------------------------------------
        if (E && SE && S && !W && !N) return "stone_wall_left_up";
        if (S && SW && W && !E && !N) return "stone_wall_right_up";
        if (N && NE && E && !S && !W) return "stone_wall_left_bottom";
        if (N && W && NW && !S && !E) return "stone_wall_right_bottom";

        // -------------------------------------------------------------------------
        // 3. 수직 높이 데이터 추적 연산
        // -------------------------------------------------------------------------
        int downCount = 0;
        while (IsWallTokenAt(tokenRows, row + (downCount + 1), x)) downCount++;

        int upCount = 0;
        while (IsWallTokenAt(tokenRows, row - (upCount + 1), x)) upCount++;

        int totalHeight = upCount + downCount + 1;
        int distanceFromBottom = downCount;

        // -------------------------------------------------------------------------
        // 4. [핵심 수정] 수직 외벽라인 높이 규칙 연동 (Left / Right Wall 정렬)
        // -------------------------------------------------------------------------
        if (N && S)
        {
            // [오른쪽 외벽선 라인]
            if (W && !E)
            {
                if (distanceFromBottom == 0) return "stone_wall_right_bottom";
                if (distanceFromBottom == 1) return "stone_wall_right_side"; // 중간은 side 마감

                // 만약 내 위쪽 코너가 뚫리는 지점(상단 마감점) 근처라면 우측 코너 에셋 유도
                if (!IsWallTokenAt(tokenRows, row - 1, x + 1) && !N) return "stone_wall_right_up";
                return "stone_wall_right"; // 그 외에는 순수 wall 마감
            }

            // [왼쪽 외벽선 라인]
            if (E && !W)
            {
                if (distanceFromBottom == 0) return "stone_wall_left_bottom";
                if (distanceFromBottom == 1) return "stone_wall_left_side";  // 중간은 side 마감

                if (!IsWallTokenAt(tokenRows, row - 1, x - 1) && !N) return "stone_wall_left_up";
                return "stone_wall_left";  // 그 외에는 순수 wall 마감
            }

            // [양측이 트인 미들 기둥 구조]
            if (!E && !W)
            {
                if (distanceFromBottom == 0) return "stone_wall_middle_bottom";
                if (distanceFromBottom == 1) return "stone_wall_middle_side";  // 중간은 side 마감
                return "stone_wall_down";
            }
        }

        // -------------------------------------------------------------------------
        // 5. 높이 미달 지형 가드 및 수평 마감 처리
        // -------------------------------------------------------------------------
        if (!S || totalHeight < 3)
        {
            if (!N)
            {
                if (E && !W) return "stone_wall_left_up";
                if (W && !E) return "stone_wall_right_up";
                return "stone_wall_up";
            }
            if (E && !W) return "stone_wall_left_down";
            if (W && !E) return "stone_wall_right_down";
            return "stone_wall_down";
        }

        if (E && W && !N) return "stone_wall_up";
        if (E && W && !S) return "stone_wall_down";

        // -------------------------------------------------------------------------
        // 6. 비정형 완화 가드 (특정 대각선이 비어 만나는 외벽 접점 보정)
        // -------------------------------------------------------------------------
        if (N && !S) return "stone_wall_down";
        if (!N && S) return "stone_wall_up";
        if (E && !W) return "stone_wall_left";
        if (W && !E) return "stone_wall_right";

        if (N && E && S && W)
        {
            if (NE && SE && !SW && NW) return "stone_wall_left_down_in";   // 5(남서)만 비어있음
            if (!NE && SE && SW && NW) return "stone_wall_right_up_in";     // 1(북동)만 비어있음
            if (NE && !SE && SW && NW) return "stone_wall_right_down_in";    // 3(남동)만 비어있음
            if (NE && SE && SW && !NW) return "stone_wall_left_up_in";  // 7(북서)만 비어있음
            return "stone_wall";
        }

        return "stone_wall_down";
    }

    // [요구사항 4] Dirt 선배치 시스템 고도화
    private void CheckAndApplyUnderWallPath(string[][] tokenRows, int row, int x, Vector3Int targetCell)
    {
        // 주변(현재 칸 포함 혹은 아랫줄)이 길 영역("P", "d", "d2")과 단 1칸이라도 접해 있는지 검사
        bool isPathAdjacent = IsTokenAt(tokenRows, row, x - 1, "P") || IsTokenAt(tokenRows, row, x - 1, "d") || IsTokenAt(tokenRows, row, x - 1, "d2") ||
                              IsTokenAt(tokenRows, row, x + 1, "P") || IsTokenAt(tokenRows, row, x + 1, "d") || IsTokenAt(tokenRows, row, x + 1, "d2") ||
                              IsTokenAt(tokenRows, row + 1, x, "P") || IsTokenAt(tokenRows, row + 1, x, "d") || IsTokenAt(tokenRows, row + 1, x, "d2") ||
                              IsTokenAt(tokenRows, row - 1, x, "P") || IsTokenAt(tokenRows, row - 1, x, "d") || IsTokenAt(tokenRows, row - 1, x, "d2");

        if (isPathAdjacent)
        {
            Tilemap layerTilemap = GetLayerTilemap(dirtTilemap);
            if (layerTilemap != null)
            {
                if (_spriteLookup.TryGetValue("d_0", out Sprite pathSprite) && pathSprite != null)
                {
                    TileBase pathTile = GetOrCreateTile("d_0", pathSprite);
                    if (pathTile != null)
                    {
                        // 벽 레이어가 그려지기 전 하단 타일맵 스페이스에 안전하게 선마킹
                        layerTilemap.SetTile(targetCell, pathTile);
                    }
                }
            }
        }
    }

    private static bool IsTokenAt(string[][] tokenRows, int row, int x, string targetToken)
    {
        if (tokenRows == null || row < 0 || row >= tokenRows.Length) return false;
        string[] tokens = tokenRows[row];
        if (tokens == null || x < 0 || x >= tokens.Length) return false;
        return tokens[x] == targetToken;
    }

    private static bool IsWallTokenAt(string[][] tokenRows, int row, int x)
    {
        if (tokenRows == null || row < 0 || row >= tokenRows.Length)
            return false;

        string[] tokens = tokenRows[row];
        if (tokens == null || x < 0 || x >= tokens.Length)
            return false;

        return tokens[x] == "#";
    }

    private static string GetBlendDirectionFileStem(string spriteKey)
    {
        if (string.IsNullOrEmpty(spriteKey) || spriteKey.Length < 3)
            return string.Empty;

        char variant = spriteKey[spriteKey.Length - 1];
        switch (variant)
        {
            case '0': return "06_center";
            case '1': return "04_bottom";
            case '2': return "01_top";
            case '3': return "03_bottom_left";
            case '4': return "00_top_left";
            case '5': return "05_bottom_right";
            case '6': return "02_top_right";
            default: return string.Empty;
        }
    }

    private string GetBlendRatioName(int x, int y, int width, int height)
    {
        float xRatio = width <= 1 ? 0.5f : x / (float)(width - 1);
        float yRatio = height <= 1 ? 0.5f : y / (float)(height - 1);
        float mapBWeight = (xRatio + yRatio) * 0.5f;

        float firstCenter = 0.38f;
        float secondCenter = 0.62f;
        float randomVal = UnityEngine.Random.value;

        if (mapBWeight < (firstCenter + secondCenter) * 0.5f)
        {
            float minBound = firstCenter - blending;
            float maxBound = firstCenter + blending;

            if (mapBWeight < minBound) return "a_70_b_30";
            else if (mapBWeight > maxBound) return "a_50_b_50";
            else
            {
                float t = (mapBWeight - minBound) / (blending * 2f);
                return (randomVal < t) ? "a_50_b_50" : "a_70_b_30";
            }
        }
        else
        {
            float minBound = secondCenter - blending;
            float maxBound = secondCenter + blending;

            if (mapBWeight < minBound) return "a_50_b_50";
            else if (mapBWeight > maxBound) return "a_30_b_70";
            else
            {
                float t = (mapBWeight - minBound) / (blending * 2f);
                return (randomVal < t) ? "a_30_b_70" : "a_50_b_50";
            }
        }
    }

    private static string GetDustSpriteKey(string[][] tokenRows, int row, int x)
    {
        bool grassUp = IsGrassTokenAt(tokenRows, row - 1, x);
        bool grassDown = IsGrassTokenAt(tokenRows, row + 1, x);
        bool grassLeft = IsGrassTokenAt(tokenRows, row, x - 1);
        bool grassRight = IsGrassTokenAt(tokenRows, row, x + 1);

        if (grassLeft && grassDown) return "d_3";
        if (grassLeft && grassUp) return "d_4";
        if (grassRight && grassDown) return "d_5";
        if (grassRight && grassUp) return "d_6";
        if (grassDown) return "d_1";
        if (grassUp) return "d_2";

        return "d_0";
    }

    private static string GetLakeSpriteKey(string[][] tokenRows, int row, int x)
    {
        return $"w_{BridgeLakeRandomPlacer.ResolveLakeTileIndex(tokenRows, row, x)}";
    }

    private static bool IsGrassTokenAt(string[][] tokenRows, int row, int x)
    {
        if (tokenRows == null || row < 0 || row >= tokenRows.Length)
            return false;

        string[] tokens = tokenRows[row];
        if (tokens == null || x < 0 || x >= tokens.Length)
            return false;

        return IsGrassToken(tokens[x]);
    }

    private static bool IsGrassToken(string token)
    {
        return token == "G" || token == "g" || token == "." ||
               token == "g_0" || token == "g_1" || token == "g_2" ||
               token == "g_3" || token == "g_4" || token == "g_5" || token == "g_6";
    }

    private Tilemap GetTilemapForSpriteKey(string spriteKey)
    {
        if (spriteKey == "blocker_wall" || spriteKey.StartsWith("stone_wall", StringComparison.Ordinal))
            return renderWallTiles ? GetLayerTilemap(wallTilemap) : null;
        if (spriteKey == "water" || spriteKey.StartsWith("w_", StringComparison.Ordinal))
            return renderLakeTiles ? GetLayerTilemap(lakeTilemap) : null;
        if (spriteKey.StartsWith("path_", StringComparison.Ordinal) ||
            spriteKey.StartsWith("d_", StringComparison.Ordinal) ||
            spriteKey.StartsWith("d2_", StringComparison.Ordinal))
        {
            return renderDirtTiles ? GetLayerTilemap(dirtTilemap) : null;
        }
        if (spriteKey.StartsWith("ground_", StringComparison.Ordinal) ||
            spriteKey.StartsWith("g_", StringComparison.Ordinal) ||
            spriteKey.StartsWith("s_", StringComparison.Ordinal))
        {
            return renderGroundTiles ? GetLayerTilemap(groundTilemap) : null;
        }

        return GetLayerTilemap(targetTilemap);
    }

    private Tilemap GetLayerTilemap(Tilemap preferredTilemap)
    {
        return preferredTilemap != null ? preferredTilemap : targetTilemap;
    }

    private Tilemap GetMarkerTilemap()
    {
        if (markerTilemap != null) return markerTilemap;
        if (targetTilemap != null) return targetTilemap;
        return GetReferenceTilemap();
    }

    private Tilemap GetReferenceTilemap()
    {
        if (targetTilemap != null) return targetTilemap;
        if (groundTilemap != null) return groundTilemap;
        if (dirtTilemap != null) return dirtTilemap;
        if (lakeTilemap != null) return lakeTilemap;
        if (wallTilemap != null) return wallTilemap;
        return markerTilemap;
    }

    private bool HasAnyOutputTilemap()
    {
        return targetTilemap != null || groundTilemap != null || dirtTilemap != null ||
               lakeTilemap != null || wallTilemap != null || markerTilemap != null;
    }

    private void ClearOutputTilemaps()
    {
        HashSet<Tilemap> cleared = new HashSet<Tilemap>();
        if (renderGroundTiles) ClearTilemapOnce(GetLayerTilemap(groundTilemap), cleared);
        if (renderDirtTiles) ClearTilemapOnce(GetLayerTilemap(dirtTilemap), cleared);
        if (renderLakeTiles) ClearTilemapOnce(GetLayerTilemap(lakeTilemap), cleared);
        if (renderWallTiles) ClearTilemapOnce(GetLayerTilemap(wallTilemap), cleared);
        ClearTilemapOnce(markerTilemap, cleared);
    }

    private static void ClearTilemapOnce(Tilemap tilemap, HashSet<Tilemap> cleared)
    {
        if (tilemap == null || cleared.Contains(tilemap)) return;
        tilemap.ClearAllTiles();
        cleared.Add(tilemap);
    }

    private void RefreshOutputTilemaps()
    {
        HashSet<Tilemap> refreshed = new HashSet<Tilemap>();
        RefreshTilemapOnce(targetTilemap, refreshed);
        RefreshTilemapOnce(groundTilemap, refreshed);
        RefreshTilemapOnce(dirtTilemap, refreshed);
        RefreshTilemapOnce(lakeTilemap, refreshed);
        RefreshTilemapOnce(wallTilemap, refreshed);
        RefreshTilemapOnce(markerTilemap, refreshed);
    }

    private static void RefreshTilemapOnce(Tilemap tilemap, HashSet<Tilemap> refreshed)
    {
        if (tilemap == null || refreshed.Contains(tilemap)) return;
        tilemap.RefreshAllTiles();
        refreshed.Add(tilemap);
    }

    private TileBase GetOrCreateTile(string key, Sprite sprite)
    {
        if (_runtimeTiles.TryGetValue(key, out TileBase cachedTile)) return cachedTile;

        UnityEngine.Tilemaps.Tile tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        tile.name = $"ASCII Tile {key}";
        tile.sprite = sprite;
        tile.color = Color.white;
        tile.transform = GetSpriteScaleMatrix(sprite);
        _runtimeTiles[key] = tile;
        return tile;
    }

    private Matrix4x4 GetSpriteScaleMatrix(Sprite sprite)
    {
        if (sprite == null || tileSpriteScale <= 0f) return Matrix4x4.identity;
        return Matrix4x4.Scale(new Vector3(tileSpriteScale, tileSpriteScale, 1f));
    }

    private readonly Dictionary<string, string> _tileAssetPaths = new Dictionary<string, string>();

    private void LoadSpriteLookup()
    {
        RegisterTileAsset("stone_wall", "Assets/modak_image_test/wall_middle.asset");
        RegisterTileAsset("stone_wall_down", "Assets/modak_image_test/wall_down.asset");
        RegisterTileAsset("stone_wall_up", "Assets/modak_image_test/wall_up.asset");
        RegisterTileAsset("stone_wall_left", "Assets/modak_image_test/wall_left.asset");
        RegisterTileAsset("stone_wall_left_up", "Assets/modak_image_test/wall_left_up.asset");
        RegisterTileAsset("stone_wall_left_down", "Assets/modak_image_test/wall_left_down.asset");
        RegisterTileAsset("stone_wall_right", "Assets/modak_image_test/wall_right.asset");
        RegisterTileAsset("stone_wall_right_up", "Assets/modak_image_test/wall_right_up.asset");
        RegisterTileAsset("stone_wall_right_down", "Assets/modak_image_test/wall_right_down.asset");

        RegisterTileAsset("stone_wall_right_side", "Assets/modak_image_test/wall_right_side.asset");
        RegisterTileAsset("stone_wall_middle_side", "Assets/modak_image_test/wall_middle_side.asset");
        RegisterTileAsset("stone_wall_left_side", "Assets/modak_image_test/wall_left_side.asset");

        RegisterTileAsset("stone_wall_right_bottom", "Assets/modak_image_test/wall_right_bottom.asset");
        RegisterTileAsset("stone_wall_middle_bottom", "Assets/modak_image_test/wall_middle_bottom.asset");
        RegisterTileAsset("stone_wall_left_bottom", "Assets/modak_image_test/wall_left_bottom.asset");

        RegisterTileAsset("stone_wall_right_up_in", "Assets/modak_image_test/wall_right_up_in.asset");
        RegisterTileAsset("stone_wall_right_down_in", "Assets/modak_image_test/wall_right_down_in.asset");
        RegisterTileAsset("stone_wall_left_up_in", "Assets/modak_image_test/wall_left_up_in.asset");
        RegisterTileAsset("stone_wall_left_down_in", "Assets/modak_image_test/wall_left_down_in.asset");

        RegisterSprite("water", "water.png");
        RegisterSpriteFromRelativePath(
            "blocker_wall",
            "Assets/Sprites/map/Tilesets/Wastelands/Sprites/RA_Wasteland.png",
            "RA_Wasteland_19");

        // 0=L, 1=TL, 2=T, 3=TR, 4=R, 5=BR, 6=B, 7=BL, 8=C
        RegisterTileAsset("w_0", $"{LakeWaterAnimationsFolder}/Ani_Water 11.asset");
        RegisterTileAsset("w_1", $"{LakeWaterAnimationsFolder}/Ani_Water 8.asset");
        RegisterTileAsset("w_2", $"{LakeWaterAnimationsFolder}/Ani_Water 9.asset");
        RegisterTileAsset("w_3", $"{LakeWaterAnimationsFolder}/Ani_Water 10.asset");
        RegisterTileAsset("w_4", $"{LakeWaterAnimationsFolder}/Ani_Water 13.asset");
        RegisterTileAsset("w_5", $"{LakeWaterAnimationsFolder}/Ani_Water 16.asset");
        RegisterTileAsset("w_6", $"{LakeWaterAnimationsFolder}/Ani_Water 15.asset");
        RegisterTileAsset("w_7", $"{LakeWaterAnimationsFolder}/Ani_Water 14.asset");
        RegisterTileAsset("w_8", $"{LakeWaterAnimationsFolder}/Ani_Water 12.asset");

        RegisterSprite("g_0", "all_grass.png");
        RegisterSprite("g_1", "up_grass.png");
        RegisterSprite("g_2", "down_grass.png");
        RegisterSprite("g_3", "rightdown_grass.png");
        RegisterSprite("g_4", "rightup_grass.png");
        RegisterSprite("g_5", "leftdown_grass.png");
        RegisterSprite("g_6", "leftup_grass.png");

        RegisterSprite("d_0", "all_dust.png");
        RegisterSprite("d_1", "up_dust.png");
        RegisterSprite("d_2", "down_dust.png");
        RegisterSprite("d_3", "rightdown_dust.png");
        RegisterSprite("d_4", "rightup_dust.png");
        RegisterSprite("d_5", "leftdown_dust.png");
        RegisterSprite("d_6", "leftup_dust.png");

        RegisterSprite("d2_0", "all_dust2.png");
        RegisterSprite("d2_1", "up_dust2.png");
        RegisterSprite("d2_2", "down_dust2.png");
        RegisterSprite("d2_3", "rightdown_dust2.png");
        RegisterSprite("d2_4", "rightup_dust2.png");
        RegisterSprite("d2_5", "leftdown_dust2.png");
        RegisterSprite("d2_6", "leftup_dust2.png");

        RegisterSprite("s_0", "all_snow.png");
        RegisterSprite("s_1", "up_snow.png");
        RegisterSprite("s_2", "down_snow.png");
        RegisterSprite("s_3", "rightdown_snow.png");
        RegisterSprite("s_4", "rightup_snow.png");
        RegisterSprite("s_5", "leftdown_snow.png");
        RegisterSprite("s_6", "leftup_snow.png");
    }

    private void RegisterTileAsset(string key, string assetPath)
    {
        if (!_tileAssetPaths.ContainsKey(key)) _tileAssetPaths[key] = assetPath;
    }

    private void SetSpriteTile(Vector3Int cell, string spriteKey)
    {
        if (TrySetSpriteTile(cell, spriteKey))
            return;

        string fallbackKey = GetBaseKeyFromBlendedSpriteKey(spriteKey);
        if (!string.IsNullOrEmpty(fallbackKey) && fallbackKey != spriteKey)
            TrySetSpriteTile(cell, fallbackKey);
    }

    private bool TrySetSpriteTile(Vector3Int cell, string spriteKey)
    {
        Tilemap outputTilemap = GetTilemapForSpriteKey(spriteKey);
        if (outputTilemap == null)
            return false;

        if (_runtimeTiles.TryGetValue(spriteKey, out TileBase cachedTile) && cachedTile != null)
        {
            outputTilemap.SetTile(cell, cachedTile);
            return true;
        }

#if UNITY_EDITOR
        if (autoLoadSpritesFromAssetFolder && _tileAssetPaths.TryGetValue(spriteKey, out string assetPath))
        {
            TileBase tileAsset = AssetDatabase.LoadAssetAtPath<TileBase>(assetPath);
            if (tileAsset != null)
            {
                _runtimeTiles[spriteKey] = tileAsset;
                _runtimeTiles[spriteKey] = tileAsset;
                outputTilemap.SetTile(cell, tileAsset);
                return true;
            }

            Debug.LogError($"[.asset 로드 실패] 경로를 다시 확인하세요: {assetPath}");
        }
#endif

        if (!_spriteLookup.TryGetValue(spriteKey, out Sprite sprite) || sprite == null)
        {
            sprite = LoadSpriteForKey(spriteKey);
            if (sprite != null)
                _spriteLookup[spriteKey] = sprite;
        }

        if (sprite == null)
        {
            if (_missingSpriteWarnings.Add(spriteKey))
                Debug.LogWarning($"Tile 에셋 또는 Sprite를 찾을 수 없음: {spriteKey}");
            return false;
        }

        outputTilemap.SetTile(cell, GetOrCreateTile(spriteKey, sprite));
        return true;
    }

    private static string GetBaseKeyFromBlendedSpriteKey(string spriteKey)
    {
        if (spriteKey.StartsWith("ground_", StringComparison.Ordinal))
            return TryGetVariantBaseKey(spriteKey, "g_");

        if (spriteKey.StartsWith("path_", StringComparison.Ordinal))
            return TryGetVariantBaseKey(spriteKey, "d_");

        return string.Empty;
    }

    private static string TryGetVariantBaseKey(string spriteKey, string prefix)
    {
        string directionStem = ExtractBlendDirectionStem(spriteKey);
        if (string.IsNullOrEmpty(directionStem))
            return string.Empty;

        char? variant = GetVariantFromDirectionStem(directionStem);
        return variant.HasValue ? $"{prefix}{variant.Value}" : string.Empty;
    }

    private static string ExtractBlendDirectionStem(string spriteKey)
    {
        int firstUnderscore = spriteKey.IndexOf('_');
        if (firstUnderscore < 0 || firstUnderscore >= spriteKey.Length - 1)
            return string.Empty;

        int ratioMarker = spriteKey.LastIndexOf("_a_", StringComparison.Ordinal);
        if (ratioMarker <= firstUnderscore)
            return string.Empty;

        return spriteKey.Substring(firstUnderscore + 1, ratioMarker - firstUnderscore - 1);
    }

    private static char? GetVariantFromDirectionStem(string directionStem)
    {
        switch (directionStem)
        {
            case "06_center":
                return '0';
            case "04_bottom":
                return '1';
            case "01_top":
                return '2';
            case "03_bottom_left":
                return '3';
            case "00_top_left":
                return '4';
            case "05_bottom_right":
                return '5';
            case "02_top_right":
                return '6';
            default:
                return null;
        }
    }

    private Sprite LoadSpriteForKey(string spriteKey)
    {
        if (useBlendedTiles)
        {
            if (spriteKey.StartsWith("ground_", StringComparison.Ordinal))
                return LoadSpriteFromRelativePath($"{blendedTilesFolder.TrimEnd('/', '\\')}/ground/{spriteKey}.png");
            if (spriteKey.StartsWith("path_", StringComparison.Ordinal))
                return LoadSpriteFromRelativePath($"{blendedTilesFolder.TrimEnd('/', '\\')}/path/{spriteKey}.png");
        }
        return null;
    }

    private void RegisterSprite(string key, string fileName)
    {
        if (_spriteLookup.ContainsKey(key)) return;
        Sprite sprite = LoadSprite(fileName);
        if (sprite != null) _spriteLookup[key] = sprite;
    }

    private void RegisterSpriteFromRelativePath(string key, string relativePath, string spriteName = null)
    {
        Sprite sprite = LoadSpriteFromRelativePath(relativePath, spriteName);
        if (sprite != null)
        {
            _spriteLookup[key] = sprite;
            _runtimeTiles.Remove(key);
        }
        else if (_missingSpriteWarnings.Add(key))
        {
            Debug.LogWarning($"Sprite not found at relative path: {relativePath} name={spriteName}");
        }
    }

    private Sprite LoadSprite(string fileName)
    {
        string assetPath = $"{imageAssetFolder.TrimEnd('/', '\\')}/{fileName}";
#if UNITY_EDITOR
        if (autoLoadSpritesFromAssetFolder)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null) return sprite;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                Rect rect = new Rect(0, 0, texture.width, texture.height);
                Vector2 pivot = new Vector2(0.5f, 0.5f);
                return Sprite.Create(texture, rect, pivot, texture.width);
            }
        }
#endif
        return null;
    }

    private Sprite LoadSpriteFromRelativePath(string relativePath, string spriteName = null)
    {
        string normalizedRelativePath = relativePath.Replace('\\', '/');
#if UNITY_EDITOR
        if (normalizedRelativePath.StartsWith("Assets/", StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(spriteName))
            {
                Sprite namedSprite = FindNamedSpriteAtPath(normalizedRelativePath, spriteName);
                if (namedSprite != null) return namedSprite;
            }

            Sprite assetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(normalizedRelativePath);
            if (assetSprite != null) return assetSprite;
        }
#endif
        string fullPath = GetProjectRelativePath(normalizedRelativePath);
        if (!File.Exists(fullPath)) return null;

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes)) return null;

        texture.filterMode = FilterMode.Point;
        Rect rect = new Rect(0, 0, texture.width, texture.height);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        return Sprite.Create(texture, rect, pivot, texture.width);
    }

#if UNITY_EDITOR
    private static Sprite FindNamedSpriteAtPath(string assetPath, string spriteName)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite && sprite.name == spriteName)
                return sprite;
        }

        return null;
    }
#endif

    private string GetInputText()
    {
        string path = GetProjectRelativePath(inputRelativePath);
        if (preferFileInput && File.Exists(path)) return File.ReadAllText(path);
        return inputText;
    }

    private static AsciiTilemapJsonData ParseInputMap(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        string trimmed = input.Trim();
        if (!trimmed.StartsWith("{")) return ParsePlainTextMap(input);

        try { return JsonUtility.FromJson<AsciiTilemapJsonData>(trimmed); }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to parse ASCII map JSON: {exception.Message}");
            return null;
        }
    }

    private static AsciiTilemapJsonData ParsePlainTextMap(string input)
    {
        string normalized = input.Replace("\r\n", "\n").Replace('\r', '\n').TrimStart('\ufeff').Trim('\n');
        string[] rows = normalized.Split('\n');
        string[][] tokenRows = TokenizeRows(rows);

        return new AsciiTilemapJsonData
        {
            width = GetMaxTokenRowWidth(tokenRows),
            height = rows.Length,
            startX = 0,
            startY = 0,
            mapGrid = rows
        };
    }

    private Vector3Int GetOrigin(AsciiTilemapJsonData mapData)
    {
        Vector3Int origin = extraOffset;
        if (originMode == OriginMode.UseJsonStart) origin += new Vector3Int(mapData.startX, mapData.startY, 0);
        return origin;
    }

    private static string[][] TokenizeRows(string[] rows)
    {
        if (rows == null) return new string[0][];
        string[][] result = new string[rows.Length][];
        for (int i = 0; i < rows.Length; i++) result[i] = TokenizeRow(rows[i]);
        return result;
    }

    private static string[] TokenizeRow(string row)
    {
        if (string.IsNullOrEmpty(row)) return new string[0];
        if (row.Contains(",")) return SplitTokenRow(row, ',');

        List<string> tokens = new List<string>();
        int index = 0;
        while (index < row.Length)
        {
            string token = ReadNextToken(row, index);
            tokens.Add(token);
            index += token.Length;
        }
        return tokens.ToArray();
    }

    private static string ReadNextToken(string row, int startIndex)
    {
        string[] knownTokens = {
            "d2_0", "d2_1", "d2_2", "d2_3", "d2_4", "d2_5", "d2_6",
            "g_0", "g_1", "g_2", "g_3", "g_4", "g_5", "g_6",
            "d_0", "d_1", "d_2", "d_3", "d_4", "d_5", "d_6",
            "s_0", "s_1", "s_2", "s_3", "s_4", "s_5", "s_6",
            "w_0", "w_1", "w_2", "w_3", "w_4", "w_5", "w_6", "w_7", "w_8",
            "d2"
        };
        foreach (string token in knownTokens)
        {
            if (startIndex + token.Length <= row.Length &&
                string.Compare(row, startIndex, token, 0, token.Length, StringComparison.Ordinal) == 0) return token;
        }
        return row[startIndex].ToString();
    }

    private static string[] SplitTokenRow(string row, char separator)
    {
        string[] rawTokens = row.Split(separator);
        List<string> tokens = new List<string>();
        foreach (string rawToken in rawTokens)
        {
            string token = rawToken.Trim();
            if (!string.IsNullOrEmpty(token)) tokens.Add(token);
        }
        return tokens.ToArray();
    }

    private static int GetMaxTokenRowWidth(string[][] rows)
    {
        int width = 0;
        if (rows == null) return width;
        foreach (string[] row in rows) width = Mathf.Max(width, row != null ? row.Length : 0);
        return width;
    }

    private Transform GetSpawnRoot()
    {
        Transform existing = transform.Find(SpawnRootName);
        if (existing != null) return existing;
        GameObject root = new GameObject(SpawnRootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        return root.transform;
    }

    private void ClearSpawnedPrefabs()
    {
        Transform root = transform.Find(SpawnRootName);
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private static string GetProjectRelativePath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
    }
}

[Serializable]
public class AsciiTilemapJsonData
{
    public int width;
    public int height;
    public int startX;
    public int startY;
    public string[] mapGrid;
}
