using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.Text;

public class Tile : MonoBehaviour
{
    Color selectColor;
    public Tile Parent;
    public IdeaTile idea;
    public EventTrigger trigger;

    public Character Owner {get; private set;}
    public bool hasOwner;

    public SpriteRenderer Renderer;

    public int F {get {return G + H;}}
    public int G {get; private set;}
    public int H {get; private set;}

    public void Execute(Tile parent, Tile destination)
    {
        Parent = parent;
        G = CalcGValue(parent, this);
        int diffX = Mathf.Abs(destination.idea.coord.x - idea.coord.x);
        int diffY = Mathf.Abs(destination.idea.coord.y - idea.coord.y);
        H = (diffX + diffY) * 10;
    }

    public static int CalcGValue(Tile parent, Tile current)
    {
        int value = 10;
        return parent.G + value;
    }

    public void ClearPath()
    {
        Parent = null;
        G = H = 0;
    }

    public void SetSelected()
    {
        Renderer.color = selectColor;
    }

    public void ReleaseSelected()
    {
        Renderer.color = new Color(22, 22, 0);
    }

    public void SetIdea(IdeaTile ideaTile)
    {
        idea = ideaTile;
    }

    public void SetOwner(Character c)
    {
        Owner = c;
        hasOwner = true;
        idea.State = TileState.Full;
        c.currentStand = this;
    }

    public void ReleaseOwner (Character c)
    {
        Owner = null;
        hasOwner = false;
        idea.State = TileState.Empty;
    }

    public void SetSearchInfo(IdeaTile ideaTile)
    {
        idea.SetSearchInfo(ideaTile);
    }
    public void ClearSearch()
    {
        idea.ClearSearchInfo();
    }
    public void Click(PointerEventData data)
    {
        Map.Instance.CheckSelectTile(this);
    }
    private void OnMouseDown() {
        Map.Instance.CheckSelectTile(this);
    }
    private void Start()
    {
        Renderer = GetComponent<SpriteRenderer>();
        selectColor = Renderer.color;
        ReleaseSelected();
    }
}