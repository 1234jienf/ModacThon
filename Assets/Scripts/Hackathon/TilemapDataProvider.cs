using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TilemapDataProvider : MonoBehaviour
{
    [Header("1. 타일 에셋 설정")]
    [Tooltip("외곽벽이나 절벽 등 절대 통과할 수 없는 벽 타일들을 등록합니다.")]
    public List<TileBase> wallTiles = new List<TileBase>();
    
    [Tooltip("플레이어가 이동할 수 있는 일반 바닥 타일들을 등록합니다.")]
    public List<TileBase> planeTiles = new List<TileBase>();

    [Tooltip("물, 가시 등 Collider가 붙어있거나 충돌 처리가 필요한 애니메이션 타일(AnimatedTile) 등을 등록합니다.")]
    public List<TileBase> obstacleTiles = new List<TileBase>(); 

    [Header("2. 장애물 프리팹 에셋 설정")]
    [Tooltip("프로젝트 창(Assets)에 있는 장애물/건물 등의 원본 프리팹 에셋들을 등록합니다. 하위 오브젝트 이름 매칭으로 자동 탐색합니다.")]
    public List<GameObject> obstaclePrefabs = new List<GameObject>();

    [Header("3. 맵 특징 오브젝트 (Scene 인스턴스)")]
    [Tooltip("씬에 배치된 시작점 게임 오브젝트를 등록합니다. (선택사항)")]
    public GameObject startPoint;
    
    [Tooltip("씬에 배치된 도착점 게임 오브젝트를 등록합니다. (선택사항)")]
    public GameObject goalPoint;

    private List<Tilemap> _tilemaps = new List<Tilemap>();
    private BoundsInt _combinedBounds;
    private bool _hasBounds = false;

    void Awake()
    {
        InitializeTilemaps();
    }

    /// <summary>
    /// 하위 타일맵을 수집하고, 렌더링 정렬 순서(Order in Layer)가 높은 순(가장 상단)으로 정렬 및 Bounds를 계산합니다.
    /// </summary>
    private void InitializeTilemaps()
    {
        _tilemaps.Clear();
        var foundTilemaps = GetComponentsInChildren<Tilemap>(true);
        if (foundTilemaps.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} 하위에 Tilemap 컴포넌트가 없습니다.");
            return;
        }

        List<Tilemap> sortedList = new List<Tilemap>(foundTilemaps);
        sortedList.Sort((a, b) =>
        {
            // 부모 클래스인 Renderer로 가져옴 (정상)
            var rendA = a.GetComponent<Renderer>(); 
            var rendB = b.GetComponent<Renderer>();
            if (rendA == null || rendB == null) return 0;

            int layerValueA = SortingLayer.GetLayerValueFromID(rendA.sortingLayerID);
            int layerValueB = SortingLayer.GetLayerValueFromID(rendB.sortingLayerID);

            if (layerValueA != layerValueB) return layerValueB.CompareTo(layerValueA);
            
            // [수정] rendB.orderInLayer -> rendB.sortingOrder 로 변경
            return rendB.sortingOrder.CompareTo(rendA.sortingOrder); 
        });

        _tilemaps = sortedList;

        // (이하 Bounds 계산 로직은 이전과 동일)
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

    /// <summary>
    /// 외부(Generator 등)에서 요청 시 맵의 모든 정보를 취합하여 2차원 문자 배열로 반환합니다.
    /// </summary>
    public char[,] GetMapMatrix()
    {
        // 에디터 모드 디버깅을 위해 매 요청마다 레이어 구조 및 영역 갱신
        InitializeTilemaps();
        if (!_hasBounds) return new char[0, 0];

        int width = _combinedBounds.size.x;
        int height = _combinedBounds.size.y;
        char[,] mapMatrix = new char[height, width];

        // [1단계] 타일맵 드로잉 및 타일형 장애물(AnimatedTile 등) 판별
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3Int tilePos = new Vector3Int(_combinedBounds.xMin + x, _combinedBounds.yMin + y, 0);
                TileBase topTile = null;

                // 정렬된 레이어 중 가장 눈에 잘 보이는 상단 타일을 우선 획득
                foreach (var tm in _tilemaps)
                {
                    TileBase tile = tm.GetTile(tilePos);
                    if (tile != null)
                    {
                        topTile = tile;
                        break; // 찾았다면 하위 레이어 검사는 스킵 (가장 상단만 처리 요건 만족)
                    }
                }

                if (topTile == null)
                {
                    mapMatrix[y, x] = ' '; // 빈 공간
                    continue;
                }

                string tileName = topTile.name.ToLower();

                // 조건별 문자 매핑 (문자 가로폭 정렬을 위해 전각 기호 사용)
                if (tileName.Contains("cliff") || wallTiles.Contains(topTile))
                {
                    mapMatrix[y, x] = '■'; // 벽
                }
                else if (obstacleTiles.Contains(topTile) || tileName.Contains("water") || tileName.Contains("lava"))
                {
                    mapMatrix[y, x] = '▩'; // 장애물 타일 (AnimatedTile 등)
                }
                else if (planeTiles.Contains(topTile))
                {
                    mapMatrix[y, x] = '□'; // 일반 평지 바닥
                }
                else
                {
                    mapMatrix[y, x] = '？'; // 미지정 타일
                }
            }
        }

        // [2단계] 하위 GameObject(프리팹 역추적 및 마커 오브젝트) 위치 보정 및 덮어쓰기
        if (_tilemaps.Count > 0)
        {
            Tilemap referenceGrid = _tilemaps[0]; // 좌표 변환의 기준이 될 메인 그리드

            // 2-A. 하위에 실시간 배치된 프리팹 인스턴스 자동 추적 ('▩')
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (var child in allChildren)
            {
                if (child == this.transform) continue; // 자기 자신 제외

                foreach (var prefab in obstaclePrefabs)
                {
                    if (prefab == null) continue;

                    // 생성된 자식 오브젝트의 이름이 에셋 폴더 원본 프리팹 이름으로 시작하는지 매칭
                    if (child.name.StartsWith(prefab.name))
                    {
                        SetObjectOnMatrix(mapMatrix, referenceGrid, child.position, '▩');
                        break; 
                    }
                }
            }

            // 2-B. 시작점 배치 ('Ｓ')
            if (startPoint != null) 
                SetObjectOnMatrix(mapMatrix, referenceGrid, startPoint.transform.position, 'Ｓ');

            // 2-C. 도착점 배치 ('Ｇ')
            if (goalPoint != null) 
                SetObjectOnMatrix(mapMatrix, referenceGrid, goalPoint.transform.position, 'Ｇ');
        }

        return mapMatrix;
    }

    /// <summary>
    /// 실제 월드 좌표를 정밀 격자 좌표로 복원하여 배열에 가로세로 폭을 맞춰 대입하는 내부 헬퍼 함수
    /// </summary>
    private void SetObjectOnMatrix(char[,] matrix, Tilemap refGrid, Vector3 worldPos, char marker)
    {
        // 월드 좌표의 실숫값을 타일 내부 정수 셀 좌표로 보정
        Vector3Int cellPos = refGrid.WorldToCell(worldPos);

        // 통합 월드 범위 기준 내부 인덱스로 계산 변경
        int xIdx = cellPos.x - _combinedBounds.xMin;
        int yIdx = cellPos.y - _combinedBounds.yMin;

        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);

        // 맵 바깥 예외 영역으로 튀어 나간 소품 오브젝트 필터링
        if (xIdx >= 0 && xIdx < width && yIdx >= 0 && yIdx < height)
        {
            matrix[yIdx, xIdx] = marker;
        }
    }
}