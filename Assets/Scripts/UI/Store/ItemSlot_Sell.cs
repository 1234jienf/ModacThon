using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemSlot_Sell : MonoBehaviour, IPointerClickHandler
{
    public SOItem item;
    public Image itemImage;  
    public TMP_Text itemCount_text;
    public int itemCount;
    public BagControl myBag;

    private GameObject popup;
    private void SetColor(float _alpha)
    {
        Color color = itemImage.color;
        color.a = _alpha;
        itemImage.color = color;
    }
    public void SetBag(BagControl bag)
    {
        myBag = bag;
    }
    public void setPop(GameObject p)
    {
        popup = p;
    }
    public void AddItem(SOItem _item, int _count = 1)
    {
        item = _item;
        itemCount = _count;
        itemImage.sprite = item.icon;
        itemCount_text.enabled = true;
        itemCount_text.text = itemCount + "";

        SetColor(1);
    }

    public void SetSlotCount(int _count)
    {
        itemCount += _count;
        itemCount_text.text = itemCount + "";

        if (itemCount <= 0)
            ClearSlot();
    }

    public void ClearSlot()
    {
        item = null;
        itemCount = 0;
        itemImage.sprite = null;
        SetColor(0);

        itemCount_text.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        confirmUI confirm = popup.GetComponent<confirmUI>();
        // 우클릭을 감지하려면 eventData.button을 확인
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            myBag.addItem(item);
            SetSlotCount(-1);
        }
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Debug.Log("좌클릭");
            // 좌클릭 시 팝업창(구매수량 관련)
            if (item!= null) {
                popup.SetActive(true);
                confirm.setConfirm(item, itemCount);
                confirm.setSlot(this);
            }
        }
    }
}