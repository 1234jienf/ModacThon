using System.Collections;
using UnityEngine;

public class Enemy : Character
{
    public bool AiTest;
    public bool isMoving{ get { return moveCoroutine != null;}}
    public Color lineColor;
    public void StartAI()
    {
        stopCurrentMove();
        moveCoroutine = StartCoroutine(aiRoute());
    }

    IEnumerator aiRoute()
    {
        currentStand.Click(null);
        yield return new WaitForSeconds(1f);
        var target = Map.Instance.GetNearestTile(currentStand, moveableArea, Stat.Mov);
        target.Click(null);
        while (isMoving)
            yield return null;
    }

    IEnumerator testMoveRoute(Vector2Int factor)
    {
        var target = Map.Instance.GetTile(currentStand.idea.coord + factor);
        if (target != null && target.idea.State == TileState.Empty)
        {
            currentStand.Click(null);
            yield return new WaitForSeconds(1f);
            target.Click(null);
        }
        else
        {
            Debug.LogError("못감");
        }
    }

    void ShowLoad()
    {
        var path = Map.Instance.GetMinimunPath(currentStand, Map.Instance.testC.currentStand);
        for (int i = 0; i < path.Count - 1; i++)
        {
            var start = path[i];
            var dest = path[i + 1];
            Debug.DrawLine(start.transform.position, dest.transform.position, lineColor);
        }
    }

    void LoadOff()
    {

    }
    private void Awake() {
    }

    private void Update() {
        // rb.MovePosition
        // (rb.position + speed * direction * Time.deltaTime);
        if (AiTest)
        {
            AiTest = false;
            StartCoroutine(aiRoute());
        }

        if (Input.GetKey(KeyCode.Space))
        {
            ShowLoad();
        }
        else
        {
            LoadOff();
        }
    }
}