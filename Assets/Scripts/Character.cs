using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    public CharacterSide Side;
    public Status Stat;
    // public CharacterAnimation Ani;
    public Tile currentStand;
    public List<Tile> prevMoveTile = new List<Tile>();
    protected List<Tile> moveableArea = new List<Tile>();
    protected Coroutine moveCoroutine;
    public bool isMove;
    public Tile tempTile;
    public bool isWalking;

    public void SetMoveableArea(List<Tile> area)
    {
        moveableArea.Clear();
        moveableArea = area;
        foreach (var v in moveableArea)
        {
            v.SetSelected();
        }
    }
    public bool OnMove(Tile t)
    {
        tempTile = t;
        isMove = true;
        return true;
    }

    public bool CheckMove(Tile t)
    {
        if (moveableArea.Contains(t))
        {
            prevMoveTile.Clear();
            while (t != null)
            {
                prevMoveTile.Insert(0, t);
                if (t.idea.prev != null)
                    t = Map.Instance.GetTile(t.idea.prev.coord);
                else
                    t = null;
            }

            stopCurrentMove();
            moveCoroutine = StartCoroutine(moveWithAnimation(prevMoveTile));
            return true;
        }

        return false;
    }

    public void stopCurrentMove()
    {
        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);
        moveCoroutine = null;
    }

    IEnumerator moveWithAnimation(List<Tile> moveRoute)
    {
        foreach (var v in moveableArea)
        {
            v.ReleaseSelected();
        }
        moveableArea.Clear();
        isWalking = true;
        
        for (int i = 0; i < moveRoute.Count - 1; i++)
        {
            Tile start = moveRoute[i];
            Tile dest = moveRoute[i + 1];
            transform.forward = (dest.transform.position - start.transform.position).normalized;
            // transform.position = Vector2.Lerp(start.transform.position, dest.transform.position, Time.deltaTime);
            transform.Translate(transform.forward * Time.deltaTime);
            yield return new WaitForSeconds(1f);
            // Ani.PlayAnimation("isMove");
            // while (Ani.ani.IsPlaying("isMove"))
            // {
            //     yield return null;
            // }
            start.ReleaseOwner(this);
            dest.SetOwner(this);
        }
        isWalking = false;
        yield return null;
    }
    // Ani.PlayAnimation("isNormal");
    private void Start() {
        isMove = false;
    }
    private void Update() {
        
        if (isMove)
        {
            isMove = false;
            Debug.Log("isMove ON");
            CheckMove(tempTile);
        }
    }
}
[System.Serializable]
public class Status
{
    public int HP;
    public int ATK;
    public int Mov;
}


public enum CharacterSide
{
    Alliance,
    Enemy,
}

public enum CharacterType
{
    Hyeji,
}