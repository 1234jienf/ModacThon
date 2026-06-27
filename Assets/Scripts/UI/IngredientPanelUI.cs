using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IngredientPanelUI : MonoBehaviour
{
    public SOItem item;
    public Image itemImage;
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
    public void InsertItem(SOItem _item, int _count = 1)
    {
        item = _item;
        itemCount = _count;
        itemImage.sprite = item.icon;
        itemCount_text.enabled = true;
        itemCount_text.text = itemCount + "";

        SetColor(1);
    }
}
