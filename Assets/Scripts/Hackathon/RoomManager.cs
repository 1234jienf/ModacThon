using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Room
{
    public int id;
    public RectInt bounds;
    public List<Vector2Int> entrancePositions = new List<Vector2Int>();

    public Room(int id, RectInt bounds)
    {
        this.id = id;
        this.bounds = bounds;
    }
}

public class MapManager
{
    private static MapManager instance;
    public static MapManager Instance => instance ?? (instance = new MapManager());

    private Dictionary<int, Room> roomDict = new Dictionary<int, Room>();
    private int idCounter = 0;

    // [추가] 외부 맵(map_A, map_B)과 브릿지를 연결하는 외부 진입로 좌표
    public Vector2Int EntryPointA { get; set; }
    public Vector2Int EntryPointB { get; set; }

    // 새로운 맵을 만들기 전에 기존 방 데이터 및 진입로 데이터를 초기화합니다.
    public void ClearMapData()
    {
        roomDict.Clear();
        idCounter = 0;
        EntryPointA = Vector2Int.zero;
        EntryPointB = Vector2Int.zero;
    }

    public void RegisterRoom(RectInt bounds)
    {
        Room room = new Room(idCounter++, bounds);
        roomDict.Add(room.id, room);
    }

    public Room GetRoom(int id) => roomDict.ContainsKey(id) ? roomDict[id] : null;
    public int GetTotalCount() => roomDict.Count;
    public Dictionary<int, Room>.ValueCollection GetAllRooms() => roomDict.Values;

    // 콘솔창에 모든 맵 정보(방, 내부 입구, 외부 진입로)를 출력합니다.
    public void LogMapStatus()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<color=cyan>====== [MapManager] 전체 맵 데이터 정보 ======</color>");

        // [추가] 외부 진입로 디버깅 로그
        sb.AppendLine($"[외부 진입로] Map_A 진입점: {EntryPointA} | Map_B 진입점: {EntryPointB}");
        sb.AppendLine("--------------------------------------------------");

        if (roomDict.Count == 0)
        {
            sb.AppendLine("저장된 내부 방이 하나도 없습니다.");
        }
        else
        {
            foreach (var kvp in roomDict)
            {
                Room r = kvp.Value;
                Vector2 center = new Vector2(r.bounds.x + r.bounds.width / 2f, r.bounds.y + r.bounds.height / 2f);

                sb.Append($"[방 ID: {r.id}] 위치: (X: {r.bounds.x}, Y: {r.bounds.y}) | 크기: {r.bounds.width}x{r.bounds.height} | 중심점: {center}\n");
                sb.Append($"   └─ 내부 입구 개수: {r.entrancePositions.Count}개 -> ");
                if (r.entrancePositions.Count > 0)
                {
                    List<string> posStrings = new List<string>();
                    foreach (var pos in r.entrancePositions) posStrings.Add(pos.ToString());
                    sb.AppendLine(string.Join(", ", posStrings));
                }
                else
                {
                    sb.AppendLine("없음 (통로와 연결되지 않음)");
                }
            }
        }

        sb.AppendLine("<color=cyan>===============================================</color>");
        Debug.Log(sb.ToString());
    }
}