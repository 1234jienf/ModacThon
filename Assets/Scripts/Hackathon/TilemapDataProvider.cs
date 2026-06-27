using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TilemapDataProvider : MonoBehaviour
{
    [Header("타일 에셋 설정")]
    public List<TileBase> wallTiles = new List<TileBase>();
    public List<TileBase> planeTiles = new List<TileBase>();

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
                
                // 여러 레이어(타일맵) 중 해당 좌표에 타일이 있는지 순회 검사
                TileBase tile = null;
                foreach (var tm in _tilemaps)
                {
                    tile = tm.GetTile(tilePos);
                    if (tile != null) break; // 위에 쌓인 타일을 우선시함
                }

                if (tile == null)
                {
                    mapMatrix[y, x] = ' '; 
                    continue;
                }

                if (tile.name.ToLower().Contains("cliff") || wallTiles.Contains(tile))
                {
                    mapMatrix[y, x] = '■'; // 벽
                }
                else if (planeTiles.Contains(tile))
                {
                    mapMatrix[y, x] = '□'; // 바닥
                }
                else
                {
                    mapMatrix[y, x] = '？';
                }
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
                    SetObjectOnMatrix(mapMatrix, referenceGrid, obstacle.transform.position, '▩'); // 장애물은 격자무늬 블록
            }

            // 시작점 배치 ('Ｓ')
            if (startPoint != null) 
                SetObjectOnMatrix(mapMatrix, referenceGrid, startPoint.transform.position, 'Ｓ');

            // 도착점 배치 ('Ｇ')
            if (goalPoint != null) 
                SetObjectOnMatrix(mapMatrix, referenceGrid, goalPoint.transform.position, 'Ｇ');
        }

        return mapMatrix;
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