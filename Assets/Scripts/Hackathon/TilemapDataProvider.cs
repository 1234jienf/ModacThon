using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TilemapDataProvider : MonoBehaviour
{
    [Header("1. 타일 에셋 설정")]
    [Tooltip("기본 땅(Ground) 타일들을 등록합니다.")]
    public List<TileBase> groundTiles = new List<TileBase>();
    
    [Tooltip("길(Road) 타일들을 등록합니다.")]
    public List<TileBase> roadTiles = new List<TileBase>();

    [Tooltip("물, 가시 등 통과 불가능한 타일 에셋(AnimatedTile 등)을 등록합니다.")]
    public List<TileBase> obstacleTiles = new List<TileBase>(); 

    [Header("2. 장애물 프리팹 에셋 설정")]
    [Tooltip("프로젝트 창(Assets)의 장애물/건물 프리팹을 등록합니다. Sprite 크기를 계산해 영역을 채웁니다.")]
    public List<GameObject> obstaclePrefabs = new List<GameObject>();

    [Header("3. 맵 특징 오브젝트 (Option)")]
    public GameObject startPoint;
    public GameObject goalPoint;

    private List<Tilemap> _tilemaps = new List<Tilemap>();
    private BoundsInt _combinedBounds;
    private bool _hasBounds = false;

    void Awake()
    {
        InitializeTilemaps();
    }

    private void InitializeTilemaps()
    {
        _tilemaps.Clear();
        var foundTilemaps = GetComponentsInChildren<Tilemap>(true);
        if (foundTilemaps.Length == 0) return;

        List<Tilemap> sortedList = new List<Tilemap>(foundTilemaps);
        sortedList.Sort((a, b) =>
        {
            var rendA = a.GetComponent<Renderer>(); 
            var rendB = b.GetComponent<Renderer>();
            if (rendA == null || rendB == null) return 0;

            int layerValueA = SortingLayer.GetLayerValueFromID(rendA.sortingLayerID);
            int layerValueB = SortingLayer.GetLayerValueFromID(rendB.sortingLayerID);

            if (layerValueA != layerValueB) return layerValueB.CompareTo(layerValueA);
            return rendB.sortingOrder.CompareTo(rendA.sortingOrder); 
        });

        _tilemaps = sortedList;

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
                _combinedBounds.xMin = Mathf.Min(_combinedBounds.xMin, tm.cellBounds.xMin);
                _combinedBounds.xMax = Mathf.Max(_combinedBounds.xMax, tm.cellBounds.xMax);
                _combinedBounds.yMin = Mathf.Min(_combinedBounds.yMin, tm.cellBounds.yMin);
                _combinedBounds.yMax = Mathf.Max(_combinedBounds.yMax, tm.cellBounds.yMax);
            }
        }
    }

    public char[,] GetMapMatrix()
    {
        InitializeTilemaps();
        if (!_hasBounds) return new char[0, 0];

        int width = _combinedBounds.size.x;
        int height = _combinedBounds.size.y;
        char[,] mapMatrix = new char[height, width];

        // [1단계] 타일맵 드로잉 및 Ground / Road / Obstacle 타일 분류
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3Int tilePos = new Vector3Int(_combinedBounds.xMin + x, _combinedBounds.yMin + y, 0);
                TileBase topTile = null;

                foreach (var tm in _tilemaps)
                {
                    TileBase tile = tm.GetTile(tilePos);
                    if (tile != null)
                    {
                        topTile = tile;
                        break; 
                    }
                }

                if (topTile == null)
                {
                    mapMatrix[y, x] = ' '; 
                    continue;
                }

                string tileName = topTile.name.ToLower();

                // 타일 식별 및 기호 매핑
                if (obstacleTiles.Contains(topTile) || tileName.Contains("cliff") || tileName.Contains("water"))
                {
                    mapMatrix[y, x] = '▩'; // 장애물 타일
                }
                else if (roadTiles.Contains(topTile) || tileName.Contains("road") || tileName.Contains("path"))
                {
                    mapMatrix[y, x] = '□'; // Road (길)
                }
                else if (groundTiles.Contains(topTile) || tileName.Contains("ground") || tileName.Contains("grass"))
                {
                    mapMatrix[y, x] = '■'; // Ground (일반 땅)
                }
                else
                {
                    mapMatrix[y, x] = '？'; // 미지정
                }
            }
        }

        // [2단계] GameObject 장애물 처리 (Sprite 크기 고려 정밀 계산)
        if (_tilemaps.Count > 0)
        {
            Tilemap referenceGrid = _tilemaps[0];

            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (var child in allChildren)
            {
                if (child == this.transform) continue;

                foreach (var prefab in obstaclePrefabs)
                {
                    if (prefab == null) continue;

                    // 이름 매칭 성공 시
                    if (child.name.StartsWith(prefab.name))
                    {
                        // 자식 오브젝트의 SpriteRenderer를 가져옵니다.
                        SpriteRenderer sRenderer = child.GetComponent<SpriteRenderer>();

                        if (sRenderer != null)
                        {
                            // 스프라이트가 차지하는 실제 월드 바운드(영역) 추출
                            Bounds spriteBounds = sRenderer.bounds;

                            // 월드 바운드의 최소/최대 지점을 타일맵 격자(Cell)로 변환
                            Vector3Int minCell = referenceGrid.WorldToCell(spriteBounds.min);
                            Vector3Int maxCell = referenceGrid.WorldToCell(spriteBounds.max);

                            // 스프라이트 크기가 차지하는 모든 타일 영역을 순회하며 장애물 각인
                            for (int cellY = minCell.y; cellY <= maxCell.y; cellY++)
                            {
                                for (int cellX = minCell.x; cellX <= maxCell.x; cellX++)
                                {
                                    SetCellOnMatrix(mapMatrix, cellX, cellY, '▩');
                                }
                            }
                        }
                        else
                        {
                            // SpriteRenderer가 없는 일반 오브젝트인 경우 기존처럼 중심점 1칸만 처리
                            Vector3Int cellPos = referenceGrid.WorldToCell(child.position);
                            SetCellOnMatrix(mapMatrix, cellPos.x, cellPos.y, '▩');
                        }
                        break; 
                    }
                }
            }

            // [3단계] 옵션 특징 오브젝트 마킹 (S, G)
            if (startPoint != null) 
            {
                Vector3Int cell = referenceGrid.WorldToCell(startPoint.transform.position);
                SetCellOnMatrix(mapMatrix, cell.x, cell.y, 'Ｓ');
            }
            if (goalPoint != null) 
            {
                Vector3Int cell = referenceGrid.WorldToCell(goalPoint.transform.position);
                SetCellOnMatrix(mapMatrix, cell.x, cell.y, 'Ｇ');
            }
        }

        return mapMatrix;
    }

    /// <summary>
    /// 계산된 격자 좌표(X, Y)를 배열 범위 체크 후 안전하게 대입하는 내부 함수
    /// </summary>
    private void SetCellOnMatrix(char[,] matrix, int cellX, int cellY, char marker)
    {
        int xIdx = cellX - _combinedBounds.xMin;
        int yIdx = cellY - _combinedBounds.yMin;

        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);

        if (xIdx >= 0 && xIdx < width && yIdx >= 0 && yIdx < height)
        {
            matrix[yIdx, xIdx] = marker;
        }
    }
}