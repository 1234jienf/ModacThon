using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSlot : MonoBehaviour
{
    public SOItem item; // ȹ���� ������
    public Image itemImage;  // �������� �̹���
    public TMP_Text itemCount_text;
    public int itemCount;

    // ������ �̹����� ������ ����
    private void SetColor(float _alpha)
    {
        Color color = itemImage.color;
        color.a = _alpha;
        itemImage.color = color;
    }

    // �κ��丮�� ���ο� ������ ���� �߰�
    public void AddItem(SOItem _item, int _count = 1)
    {
        item = _item;
        itemCount = _count;
        itemImage.sprite = item.icon;
        itemCount_text.enabled = true;
        itemCount_text.text = itemCount + "";

        SetColor(1);
    }

    // �ش� ������ ������ ���� ������Ʈ
    public void SetSlotCount(int _count)
    {
        itemCount += _count;
        itemCount_text.text = itemCount + "";

        if (itemCount <= 0)
            ClearSlot();
    }

    // �ش� ���� �ϳ� ����
    public void ClearSlot()
    {
        item = null;
        itemCount = 0;
        itemImage.sprite = null;
        SetColor(0);

        itemCount_text.enabled = false;
    }
}