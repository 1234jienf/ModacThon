using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TilemapDataProvider : MonoBehaviour
{
    [Header("타일 에셋 설정")]
    public List<TileBase> wallTiles = new List<TileBase>();
    public List<TileBase> planeTiles = new List<TileBase>();

    [Header("타일셋 Path/Ground (0 TL, 1 T, 2 TR, 3 BL, 4 B, 5 BR, 6 C)")]
    public TileBase[] pathTiles = new TileBase[7];
    public TileBase[] groundTiles = new TileBase[7];

    [Header("타일셋 Lake (0 L, 1 TL, 2 T, 3 TR, 4 R, 5 BR, 6 B, 7 BL, 8 C)")]
    public TileBase[] lakeTiles = new TileBase[9];

    [Header("맵 특징 오브젝트 (Option)")]
    public GameObject startPoint;
    public GameObject goalPoint;
    public List<GameObject> obstacles = new List<GameObject>();

    // 하위의 모든 타일맵을 연산하기 위한 리스트
    private List<Tilemap> _tilemaps = new List<Tilemap>();
    private BoundsInt _combinedBounds;
    private bool _hasBounds = false;

    void Awake()
    {
        InitializeTilemaps();
    }

    [ContextMenu("Auto Fill Blend Tiles From Tilemaps")]
    public void AutoFillBlendTilesFromTilemaps()
    {
        InitializeTilemaps();
        int pathCount = FillTileArrayFromTilemap(pathTiles, IsPathTilemap);
        int groundCount = FillTileArrayFromTilemap(groundTiles, IsGroundTilemap);
        int lakeCount = FillLakeTilesFromTilemap();

        Debug.Log(
            $"{gameObject.name}: blend tile auto-fill 완료\n" +
            $"path={pathCount}/7, ground={groundCount}/7, lake={lakeCount}/9\n" +
            "Lake는 Ani_Water 타일(Animations/) 9종: l, tl, t, tr, r, br, b, bl, c"
        );
    }

    private int FillLakeTilesFromTilemap()
    {
        Tilemap source = FindTilemap(IsWaterTilemap);
        if (source == null)
        {
            return 0;
        }

        return LakeTileMatcher.FillLakeTiles(lakeTiles, source);
    }

    private int FillTileArrayFromTilemap(TileBase[] target, System.Func<string, bool> matcher)
    {
        if (target == null)
        {
            return 0;
        }

        Tilemap source = FindTilemap(matcher);
        if (source == null)
        {
            return 0;
        }

        List<TileBase> uniqueTiles = CollectUniqueTiles(source);
        uniqueTiles.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        for (int i = 0; i < target.Length; i++)
        {
            target[i] = null;
        }

        int assignCount = Mathf.Min(target.Length, uniqueTiles.Count);
        for (int i = 0; i < assignCount; i++)
        {
            target[i] = uniqueTiles[i];
        }

        return assignCount;
    }

    private Tilemap FindTilemap(System.Func<string, bool> matcher)
    {
        foreach (Tilemap tilemap in _tilemaps)
        {
            if (matcher(tilemap.gameObject.name.ToLower()))
            {
                return tilemap;
            }
        }

        return null;
    }

    private static List<TileBase> CollectUniqueTiles(Tilemap tilemap)
    {
        HashSet<TileBase> unique = new HashSet<TileBase>();
        BoundsInt bounds = tilemap.cellBounds;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                TileBase tile = tilemap.GetTile(new Vector3Int(x, y, 0));
                if (tile != null)
                {
                    unique.Add(tile);
                }
            }
        }

        return new List<TileBase>(unique);
    }

    /// <summary>
    /// 1번 요건: 하위 타일맵을 자동으로 찾고 전체 경계 영역(Bounds)을 계산합니다.
    /// </summary>
    private void InitializeTilemaps()
    {
        _tilemaps.Clear();
        // 자식 오브젝트를 포함하여 모든 Tilemap 컴포넌트 수집
        _tilemaps.AddRange(GetComponentsInChildren<Tilemap>(true));

        if (_tilemaps.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name} 하위에 Tilemap 컴포넌트가 없습니다.");
            return;
        }

        // 모든 타일맵을 아우르는 거대한 하나의 Bounds(범위) 계산
        _hasBounds = false;
        foreach (var tm in _tilemaps)
        {
            tm.CompressBounds();
            if (!_hasBounds)
            {
                _combinedBounds = tm.cellBounds;
                _hasBounds = true;
            }
            else
            {
                // 범위를 계속 확장하며 합침
                _combinedBounds.xMin = Mathf.Min(_combinedBounds.xMin, tm.cellBounds.xMin);
                _combinedBounds.xMax = Mathf.Max(_combinedBounds.xMax, tm.cellBounds.xMax);
                _combinedBounds.yMin = Mathf.Min(_combinedBounds.yMin, tm.cellBounds.yMin);
                _combinedBounds.yMax = Mathf.Max(_combinedBounds.yMax, tm.cellBounds.yMax);
            }
        }
    }

    public char[,] GetMapMatrix()
    {
        // 에디터 모드 대응을 위해 매번 다시 초기화 검사
        InitializeTilemaps();

        if (!_hasBounds) return new char[0, 0];

        int width = _combinedBounds.size.x;
        int height = _combinedBounds.size.y;
        char[,] mapMatrix = new char[height, width];

        // 1단계: 바닥 및 벽 타일맵 먼저 그리기
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3Int tilePos = new Vector3Int(_combinedBounds.xMin + x, _combinedBounds.yMin + y, 0);
                
                mapMatrix[y, x] = GetStructureMarker(tilePos);
            }
        }

        // 2단계: 2&3번 요건 - 일반 GameObject(시작, 목적지, 장애물) 좌표 보정 후 맵에 덮어쓰기
        // 기준이 될 첫 번째 타일맵을 사용해 좌표 변환을 수행합니다.
        if (_tilemaps.Count > 0)
        {
            Tilemap referenceGrid = _tilemaps[0];

            // 장애물들 배치 ('O')
            foreach (var obstacle in obstacles)
            {
                if (obstacle != null) 
                    SetObjectOnMatrix(mapMatrix, referenceGrid, obstacle.transform.position, 'O'); // 장애물
            }

            // 시작점 배치 ('Ｓ')
            if (startPoint != null) 
                SetObjectOnMatrix(mapMatrix, referenceGrid, startPoint.transform.position, 'S');

            // 도착점 배치 ('Ｇ')
            if (goalPoint != null) 
                SetObjectOnMatrix(mapMatrix, referenceGrid, goalPoint.transform.position, 'G');
        }

        return mapMatrix;
    }

    public char[,] GetVisualMapMatrix()
    {
        InitializeTilemaps();

        if (!_hasBounds) return new char[0, 0];

        int width = _combinedBounds.size.x;
        int height = _combinedBounds.size.y;
        char[,] mapMatrix = new char[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3Int tilePos = new Vector3Int(_combinedBounds.xMin + x, _combinedBounds.yMin + y, 0);
                mapMatrix[y, x] = GetVisualMarker(tilePos);
            }
        }

        if (_tilemaps.Count > 0)
        {
            Tilemap referenceGrid = _tilemaps[0];

            foreach (var obstacle in obstacles)
            {
                if (obstacle != null)
                    SetObjectOnMatrix(mapMatrix, referenceGrid, obstacle.transform.position, 'O');
            }

            if (startPoint != null)
                SetObjectOnMatrix(mapMatrix, referenceGrid, startPoint.transform.position, 'S');

            if (goalPoint != null)
                SetObjectOnMatrix(mapMatrix, referenceGrid, goalPoint.transform.position, 'G');
        }

        return mapMatrix;
    }

    public bool TryGetReferenceTilemap(out Tilemap tilemap)
    {
        InitializeTilemaps();
        tilemap = _tilemaps.Count > 0 ? _tilemaps[0] : null;
        return tilemap != null;
    }

    public Vector3 MatrixIndexToWorldCenter(int matrixX, int matrixY)
    {
        InitializeTilemaps();

        if (!TryGetReferenceTilemap(out Tilemap tilemap))
        {
            return transform.position;
        }

        Vector3Int cell = new Vector3Int(_combinedBounds.xMin + matrixX, _combinedBounds.yMin + matrixY, 0);
        return tilemap.GetCellCenterWorld(cell);
    }

    public bool TryWorldToMatrixIndex(Vector3 worldPosition, out Vector2Int matrixIndex)
    {
        InitializeTilemaps();
        matrixIndex = default;

        if (!_hasBounds || !TryGetReferenceTilemap(out Tilemap tilemap))
        {
            return false;
        }

        Vector3Int cell = tilemap.WorldToCell(worldPosition);
        int x = cell.x - _combinedBounds.xMin;
        int y = cell.y - _combinedBounds.yMin;
        int width = _combinedBounds.size.x;
        int height = _combinedBounds.size.y;

        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return false;
        }

        matrixIndex = new Vector2Int(x, y);
        return true;
    }

    public Bounds GetWorldBounds()
    {
        InitializeTilemaps();

        bool hasTileBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one);

        foreach (var tilemap in _tilemaps)
        {
            tilemap.CompressBounds();
            BoundsInt cellBounds = tilemap.cellBounds;

            for (int y = cellBounds.yMin; y < cellBounds.yMax; y++)
            {
                for (int x = cellBounds.xMin; x < cellBounds.xMax; x++)
                {
                    Vector3Int cellPosition = new Vector3Int(x, y, 0);
                    if (tilemap.GetTile(cellPosition) == null)
                    {
                        continue;
                    }

                    Bounds tileWorldBounds = GetTileWorldBounds(tilemap, cellPosition);
                    if (!hasTileBounds)
                    {
                        bounds = tileWorldBounds;
                        hasTileBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(tileWorldBounds);
                    }
                }
            }
        }

        return bounds;
    }

    private Bounds GetTileWorldBounds(Tilemap tilemap, Vector3Int cellPosition)
    {
        Vector3 bottomLeft = tilemap.CellToWorld(cellPosition);
        Vector3 topRight = tilemap.CellToWorld(cellPosition + new Vector3Int(1, 1, 0));
        Vector3 center = (bottomLeft + topRight) * 0.5f;
        Vector3 size = new Vector3(
            Mathf.Abs(topRight.x - bottomLeft.x),
            Mathf.Abs(topRight.y - bottomLeft.y),
            0.1f
        );

        return new Bounds(center, size);
    }

    private char GetStructureMarker(Vector3Int tilePos)
    {
        bool hasWalkableTile = false;
        bool hasUnknownTile = false;

        foreach (var tilemap in _tilemaps)
        {
            TileBase tile = tilemap.GetTile(tilePos);
            if (tile == null)
            {
                continue;
            }

            char marker = ClassifyTile(tile, tilemap);
            if (marker == '#' || marker == 'w')
            {
                return '#';
            }

            if (marker == '.' || marker == 'g' || marker == 'p' || marker == 's')
            {
                hasWalkableTile = true;
            }
            else
            {
                hasUnknownTile = true;
            }
        }

        if (hasWalkableTile)
        {
            return '.';
        }

        return hasUnknownTile ? '?' : ' ';
    }

    private char GetVisualMarker(Vector3Int tilePos)
    {
        char bestMarker = ' ';
        int bestPriority = -1;

        foreach (var tilemap in _tilemaps)
        {
            TileBase tile = tilemap.GetTile(tilePos);
            if (tile == null)
            {
                continue;
            }

            char marker = ClassifyTile(tile, tilemap);
            int priority = GetVisualPriority(marker);
            if (priority > bestPriority)
            {
                bestMarker = marker;
                bestPriority = priority;
            }
        }

        return bestMarker;
    }

    private char ClassifyTile(TileBase tile, Tilemap tilemap)
    {
        string tileName = tile.name.ToLower();
        string tilemapName = tilemap.gameObject.name.ToLower();

        if (tileName.Contains("cliff") || wallTiles.Contains(tile) || IsWallTilemap(tilemapName))
        {
            return '#';
        }

        if (IsWaterTilemap(tilemapName))
        {
            return 'w';
        }

        if (IsPathTilemap(tilemapName))
        {
            return 'p';
        }

        if (IsSnowTilemap(tilemapName))
        {
            return 's';
        }

        if (IsGroundTilemap(tilemapName) || planeTiles.Contains(tile))
        {
            return 'g';
        }

        return '?';
    }

    private int GetVisualPriority(char marker)
    {
        switch (marker)
        {
            case '#':
                return 60;
            case 'w':
                return 50;
            case 'p':
                return 40;
            case 's':
                return 30;
            case 'g':
                return 20;
            case '.':
                return 10;
            case '?':
                return 0;
            default:
                return -1;
        }
    }

    private bool IsWallTilemap(string tilemapName)
    {
        return tilemapName.Contains("tree")
            || tilemapName.Contains("border")
            || tilemapName.Contains("wall")
            || tilemapName.Contains("cliff");
    }

    private bool IsWaterTilemap(string tilemapName)
    {
        return tilemapName.Contains("lake")
            || tilemapName.Contains("water");
    }

    private bool IsPathTilemap(string tilemapName)
    {
        return tilemapName.Contains("dirt")
            || tilemapName.Contains("road")
            || tilemapName.Contains("path");
    }

    private bool IsSnowTilemap(string tilemapName)
    {
        return tilemapName.Contains("snow")
            || tilemapName.Contains("ice");
    }

    private bool IsGroundTilemap(string tilemapName)
    {
        return tilemapName.Contains("grass")
            || tilemapName.Contains("ground")
            || tilemapName.Contains("base")
            || tilemapName.Contains("surface");
    }

    /// <summary>
    /// 월드 좌표를 타일맵 격자 좌표로 보정하여 2차원 배열에 각인하는 함수
    /// </summary>
    private void SetObjectOnMatrix(char[,] matrix, Tilemap refGrid, Vector3 worldPos, char marker)
    {
        // 월드 좌표 -> 타일맵 Cell (격자) 좌표로 보정 (치명적인 오차 해결)
        Vector3Int cellPos = refGrid.WorldToCell(worldPos);

        // 거대한 통합 Bounds 격자 내의 상대 인덱스로 변환
        int xIdx = cellPos.x - _combinedBounds.xMin;
        int yIdx = cellPos.y - _combinedBounds.yMin;

        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);

        // 오브젝트가 타일맵 바깥 영역에 실수로 나가지 않았는지 예외 처리
        if (xIdx >= 0 && xIdx < width && yIdx >= 0 && yIdx < height)
        {
            matrix[yIdx, xIdx] = marker;
        }
    }

}