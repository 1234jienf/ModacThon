using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BagSlot : MonoBehaviour
{
    public SOItem item; // 획득한 아이템
    public Image itemImage;  // 아이템의 이미지
    public TMP_Text itemCount_text;
    public int itemCount;


    // 인벤토리에 새로운 아이템 슬롯 추가
    public void AddItem(SOItem _item, int _count = 1)
    {
        item = _item;
        itemCount = _count;
        itemImage.sprite = _item.icon;
        itemCount_text.enabled = true;
        itemCount_text.text = itemCount + "";
    }

    // 해당 슬롯의 아이템 갯수 업데이트
    public void SetSlotCount(int _count)
    {
        itemCount += _count;
        itemCount_text.text = itemCount + "";

        if (itemCount <= 0)
            ClearSlot();
    }

    // 해당 슬롯 하나 삭제
    private void ClearSlot()
    {
        Destroy(gameObject);
    }
}