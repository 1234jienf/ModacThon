using UnityEngine;
using UnityEngine.Tilemaps;

public class PatternTilemapGenerator : MonoBehaviour
{
    [Header("Tilemap")]
    public Tilemap tilemap;

    [Header("Tiles")]
    public TileBase wallTile;
    public TileBase floorTile;

    [TextArea(3, 10)]
    public string mapPattern =
@"###
#..
###";
    void Start() {
        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        tilemap.ClearAllTiles();

        string[] rows = mapPattern.Split('\n');

        for (int y = 0; y < rows.Length; y++)
        {
            string row = rows[y];

            for (int x = 0; x < row.Length; x++)
            {
                Vector3Int pos =
                    new Vector3Int(
                        x,
                        rows.Length - y - 1,
                        0
                    );

                switch (row[x])
                {
                    case '#':
                        tilemap.SetTile(
                            pos,
                            wallTile
                        );
                        break;

                    case '.':
                        tilemap.SetTile(
                            pos,
                            floorTile
                        );
                        break;
                }
            }
        }
    }
}