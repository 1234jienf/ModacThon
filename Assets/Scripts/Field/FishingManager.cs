using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FishingManager : MonoBehaviour
{
    [SerializeField]
    private Tilemap lakeTilemap; // 호수 타일 맵

    [SerializeField]
    private SOItemDropTable[] dropTable; // 드롭 테이블

    [SerializeField]
    private float[] cooldown; // 쿨타임

    [SerializeField]
    private GameObject[] fishes; // 호수 안 물고기 들

    private bool[] canFishing; // 낚시 가능 여부

    private float[] latestFishing; // 최근 낚시 시각

    private int[ , ] lakeArray; // 호수 정보를 저장하는 배열
    private (int x, int y) offset; // 오프셋
    private int lakeNum = 0; // 호수의 갯수

    // BFS
    private void BFS((int x, int y) start, int id) {
        Queue<(int, int)> q = new Queue<(int, int)>();
        q.Enqueue(start); lakeArray[start.x, start.y] = id;

        int[] dx = {0, -1, 1, 0};
        int[] dy = {-1, 0, 0, 1};

        while(q.Count > 0) {
            (int x, int y) = q.Dequeue();

            for(int k = 0; k < 4; k++) {
                int nx = x + dx[k];
                int ny = y + dy[k];

                if (
                    0 <= nx && nx < lakeArray.GetLength(0) 
                    && 0 <= ny && ny < lakeArray.GetLength(1)
                    && lakeTilemap.GetTile(new Vector3Int(offset.x + nx, offset.y + ny, 0)) != null
                    && lakeArray[nx, ny] == 0
                ) {
                    q.Enqueue((nx, ny)); lakeArray[nx, ny] = id;
                }
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        // 초기화
        BoundsInt bounds = lakeTilemap.cellBounds;
        lakeArray = new int[bounds.xMax - bounds.xMin, bounds.yMax - bounds.yMin];
        offset = (bounds.xMin, bounds.yMin);

        // bfs를 돌면서 호수 정보 얻기
        for (int x = 0; x < lakeArray.GetLength(0); x++) {
            for (int y = 0; y < lakeArray.GetLength(1); y++) {
                if (lakeArray[x, y] == 0 && lakeTilemap.GetTile(new Vector3Int(offset.x + x, offset.y + y, 0)) != null) {
                    lakeNum++;
                    BFS((x, y), lakeNum); 
                }
            }
        }

        canFishing = new bool[lakeNum];

        // 최근 낚시 시각 초기화 및 낚시 가능 상태 초기화
        latestFishing = new float[lakeNum];
        for(int i = 0; i < lakeNum; i++) {
            latestFishing[i] = Time.time;
            canFishing[i] = false;
            fishes[i].SetActive(false);
        }
    }
    
    void Update() {
        for (int i = 0; i < lakeNum; i++) {
            // 쿨타임이 지났는데 낚시가 불가능한 상태로 되어 있으면
            if (Time.time - latestFishing[i] >= cooldown[i] && !canFishing[i]) {
                // 상태를 바꿔준다.
                canFishing[i] = true;
                fishes[i].SetActive(true);
            }
        }
    }

    // 해당 위치가 해당 FishingManager가 관리하는 범위에 속하는지 확인
    public bool InBounds(Vector3Int tilePos) {
        return lakeTilemap.cellBounds.xMin <= tilePos.x && tilePos.x <= lakeTilemap.cellBounds.xMax
            && lakeTilemap.cellBounds.yMin <= tilePos.y && tilePos.y <= lakeTilemap.cellBounds.yMax;
    }

    // 낚시 시도
    public List<Tuple<SOItem, int>> Fishing(Vector3Int tilePos) {
        int lakeId = lakeArray[tilePos.x - offset.x, tilePos.y - offset.y];
        List<Tuple<SOItem, int>> emptyList = new List<Tuple<SOItem, int>>();

        if (lakeId > dropTable.Length || lakeId > cooldown.Length) {
            Debug.Log("드롭테이블 또는 쿨타임을 설정해 주세요!!!!");
            return emptyList;
        }

        // 쿨타임이 지났으면 아이템을 얻는다.
        if (canFishing[lakeId - 1]) {
            List<Tuple<SOItem, int>> itemList = dropTable[lakeId - 1].CreateItemList();
            latestFishing[lakeId - 1] = Time.time;
            canFishing[lakeId - 1] = false;
            fishes[lakeId - 1].SetActive(false);

            return itemList;
        }
        // 아니라면 아직 시간이 안됐다는 메시지 출력
        else {
            Debug.Log("Lake" + lakeId + " 쿨타임이 " + (cooldown[lakeId - 1] - Time.time + latestFishing[lakeId - 1]) + "초 남았습니다.");
            return emptyList;
        }
    }
}
