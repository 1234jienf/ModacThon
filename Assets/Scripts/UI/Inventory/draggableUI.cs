using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class draggableUI : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    private Vector2 offset; // 클릭한 지점과 패널의 중심점 간의 오프셋
    private Vector2 startPos; // 확인창 초기 pos
    public void OnBeginDrag(PointerEventData eventData)
    {
        offset = eventData.position - (Vector2)transform.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position - offset;
    }

    void Awake()
    {
        startPos = transform.position;
    }

    public void resetPos()
    {
        transform.position = startPos;
    }

}
