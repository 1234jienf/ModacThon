using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Hackathon bridge 적이 배회할 수 있는 walkable 영역을 타일맵 기준으로 제한합니다.
/// </summary>
public class TilemapEnemyWalkBounds : MonoBehaviour
{
    private Tilemap _reference;
    private Tilemap _ground;
    private Tilemap _dirt;
    private Tilemap _wall;
    private Tilemap _lake;
    private Vector3 _spawnWorld;
    private float _maxRadius;

    public void Configure(
        Tilemap reference,
        Tilemap ground,
        Tilemap dirt,
        Tilemap wall,
        Tilemap lake,
        Vector3Int spawnCell,
        float maxRoamRadius)
    {
        _reference = reference;
        _ground = ground;
        _dirt = dirt;
        _wall = wall;
        _lake = lake;
        _spawnWorld = reference.GetCellCenterWorld(spawnCell);
        _maxRadius = Mathf.Max(0.5f, maxRoamRadius);
    }

    public bool CanWalkTo(Vector3 worldPosition)
    {
        if (_reference == null)
            return true;

        if (Vector3.Distance(worldPosition, _spawnWorld) > _maxRadius)
            return false;

        Vector3Int cell = _reference.WorldToCell(worldPosition);
        if (HasBlockingTile(_wall, cell) || HasBlockingTile(_lake, cell))
            return false;

        return HasWalkableTile(_ground, cell) || HasWalkableTile(_dirt, cell);
    }

    private static bool HasBlockingTile(Tilemap tilemap, Vector3Int cell)
    {
        return tilemap != null && tilemap.HasTile(cell);
    }

    private static bool HasWalkableTile(Tilemap tilemap, Vector3Int cell)
    {
        return tilemap != null && tilemap.HasTile(cell);
    }
}
