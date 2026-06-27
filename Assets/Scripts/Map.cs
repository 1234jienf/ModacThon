using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Map : MonoBehaviour
{
    public static Map Instance;

    public static Dictionary<Vector2Int, Tile> tiles = new Dictionary<Vector2Int, Tile>();
    public Tile currentFocus;
    public Character testC;
    public Enemy testE;
    public List<Tile> enemyTile;
    public List<Enemy> enemy;
    public int NumColumns, NumRows;

    Vector2Int[] checkDir = new Vector2Int[4]
    {
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0)
    };

    public Tile prefabTile;

    public void Initialize()
    {
        tiles.Clear();
        NumColumns = transform.childCount;
        NumRows = transform.GetChild(0).childCount;

        for (int i = 0; i < NumColumns; i++)
        {
            var child = transform.GetChild(i);
            for (int j = 0; j < NumRows; j++)
            {
                var final = child.GetChild(j);
                var tile = final.GetComponent<Tile>();
                Vector2Int coord = new Vector2Int(i, j);
                IdeaTile idea = tile.idea;
                idea.coord = coord;
                tiles.Add(coord, tile);
            }
        }

        tiles[Vector2Int.zero].SetOwner(testC);
        testC.transform.position = tiles[Vector2Int.zero].transform.position;

        // enemyTile.Add(tiles[new Vector2Int(0, 1)]);
        // enemyTile[0].SetOwner(testE);
        // testE.transform.position = enemyTile[0].transform.position;
    }

    public void ClearTiles()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        transform.DetachChildren();
    }

    private Tile CreateTile(int row, int column, IdeaTile idea)
    {
        var tile = Instantiate(prefabTile, transform);
        tile.transform.localPosition = new Vector2(-1 - 2*row, 1 + 2 * column);
        tile.name = string.Format("{0}{1}x{2}", tile.name, row, column);
        tile.SetIdea(idea);
        return tile;
    }

    public void CheckSelectTile(Tile tile)
    {
        if (currentFocus != null)
        {
            if (currentFocus.Owner.OnMove(tile))
                currentFocus = null;
        }
        else
        {
            if (tile.hasOwner)
            {
                currentFocus = tile;
                var moveArea = GetMoveArea(tile, tile.Owner.Stat.Mov);
                tile.Owner.SetMoveableArea(moveArea);
            }
        }

    }
    
    public List<Tile> GetMoveArea(Tile moveStart, int moveDistance)
    {
        ClearSearch();

        List<Tile> rtv = new List<Tile>();
        rtv.Add(moveStart);

        Queue<Tile> checkNext = new Queue<Tile>();
        Queue<Tile> checkNow = new Queue<Tile>();

        moveStart.idea.distance = 0;
        checkNow.Enqueue(moveStart);

        while (checkNow.Count > 0)
        {
            Tile t = checkNow.Dequeue();
            for (int i = 0; i < checkDir.Length; i++)
            {
                Tile next = GetTile(t.idea.coord + checkDir[i]);
                if (next == null || next.idea.distance <= t.idea.distance + 1 || next.idea.State != TileState.Empty)
                    continue;
                if (t.idea.distance + 1 <= moveDistance)
                {
                    next.SetSearchInfo(t.idea);
                    checkNext.Enqueue(next);
                    rtv.Add(next);
                }
            }
            if (checkNow.Count == 0)
                SwapRefrence(ref checkNow, ref checkNext);
        }
        return rtv;
    }

    public int GetDistance(Tile a, Tile b)
    {
        return Mathf.Abs(a.idea.coord.x - b.idea.coord.x) + Mathf.Abs(a.idea.coord.y - b.idea.coord.y);
    }

    List<Tile> RetraceTilePath(Tile startNode, Tile endNode)
    {
        List<Tile> path = new List<Tile>();
        Tile currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.Parent;
        }

        path.Reverse();

        return path;
    }

    public bool isNearTarget(Tile current)
    {
        return GetDistance(current, testC.currentStand) == 1;
    }

    List<Tile> GetNear(Tile node)
    {
        List<Tile> rtv = new List<Tile>();

        for (int i = 0; i < checkDir.Length; i++)
        {
            var dir = checkDir[i];
            var target = GetTile(node.idea.coord + dir);
            if (target != null && target.idea.State != TileState.Hurdle)
                rtv.Add(target);
        }
        return rtv;
    }

    public List<Tile> GetMinimunPath(Tile startTile, Tile targetTile)
    {
        ClearPath();

        List<Tile> openList = new List<Tile>();
        List<Tile> closeList = new List<Tile>();
        List<Tile> path = new List<Tile>();

        Vector2Int coord = Vector2Int.zero;

        openList.Add(startTile);
        Tile currentTile = null;

        while (openList.Count > 0)
        {
            currentTile = openList[0];

            for (int i = 0; i < openList.Count; i++)
            {
                var checkTile = openList[i];
                if (checkTile.F < currentTile.F || (checkTile.F == currentTile.F && checkTile.H < currentTile.H))
                {
                    if (!currentTile != checkTile)
                    {
                        currentTile = checkTile;
                    }
                }
            }

            openList.Remove(currentTile);
            closeList.Add(currentTile);

            if (currentTile == targetTile)
            {
                path = RetraceTilePath(startTile, targetTile);
                break;
            }

            foreach (Tile near in GetNear(currentTile))
            {
                if (!closeList.Contains(near))
                {
                    if (Tile.CalcGValue(currentTile, near) < near.G || !openList.Contains(near))
                    {
                        near.Execute(currentTile, targetTile);
                        if (!openList.Contains(near))
                            openList.Add(near);
                    }
                }
            }
        }

        return path;
    }

    public Tile GetNearestTile(Tile moveStart, Tile dest, List<Tile> moveArea, int moveDistance)
    {
        var path = GetMinimunPath(moveStart, dest);

        List<Tile> final = new List<Tile>();
        for (int i = 0; i < path.Count; i++)
        {
            var check = path[i];
            if (moveArea.Contains(check))
                final.Add(check);
        }
        return final.Last();
    }

    public Tile GetNearestTile(Tile moveStart, List<Tile> candidate, int moveDistance)
    {
        Tile dest = testC.currentStand;
        if (GetDistance(moveStart, dest) == 1)
            return moveStart;
        var path = GetMinimunPath(moveStart, dest);

        List<Tile> final = new List<Tile>();
        for (int i = 0; i < path.Count; i++)
        {
            var check = path[i];
            if (candidate.Contains(check))
                final.Add(check);
        }
        return final[final.Count - 1];
    }

    void ClearPath()
    {
        foreach(var v in tiles.Values)
        {
            v.ClearPath();
        }
    }

    void ClearSearch()
    {
        foreach(var v in tiles.Values)
        {
            v.ClearSearch();
        }
    }

    public Tile GetTile(Vector2Int coord)
    {
        return tiles.ContainsKey(coord) ? tiles[coord] : null;
    }

    void SwapRefrence(ref Queue<Tile> a, ref Queue<Tile> b)
    {
        Queue<Tile> temp = a;
        a = b;
        b = temp;
    }

    private void Start()
    {
        Initialize();
    }

    private void Awake() {
        currentFocus = null;
        Instance = this;
    }
}

public enum TileState
{
    Empty,
    Full,
    Hurdle,
}

public enum GameState
{
    MyTurn,
    EnemyTurn,
}

[System.Serializable]
public class IdeaTile
{
    public TileState State;
    public Vector2Int coord;
    public IdeaTile prev;
    public IdeaTile Parent;
    public int distance;

    public void ClearSearchInfo()
    {
        prev = null;
        distance = int.MaxValue;
    }

    public void SetSearchInfo(IdeaTile idea)
    {
        prev = idea;
        distance = idea.distance + 1;
    }

    public IdeaTile(Vector2Int coord, TileState state)
    {
        this.coord = coord;
        this.State = state;
        ClearSearchInfo();
    }
}

public enum Directions
{
    Up,
    Right,
    Down,
    Left,
}

public static class DirectionsExtensions
{
    public static Directions GetDirections(this Tile t1, Tile t2)
    {
        if (t1.idea.coord.y < t2.idea.coord.y)
            return Directions.Up;
        if (t1.idea.coord.x < t2.idea.coord.x)
            return Directions.Right;
        if (t1.idea.coord.y > t2.idea.coord.y)
            return Directions.Down;
        return Directions.Left;
    }
}