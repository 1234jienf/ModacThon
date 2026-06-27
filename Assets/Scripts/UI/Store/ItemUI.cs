using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 물품 좌클릭시 구매수량 선택, 장바구니로 선택한 만큼, 우클릭시 바로 장바구니에
public class ItemUI : MonoBehaviour, IPointerClickHandler
{
    public SOItem item;
    
    private string itemName;
    public Image itemImg;
    public TMP_Text itemNameText;
    public TMP_Text itemPriceText;
    public TMP_Text itemDescriptionText;

    public BagControl myBag;

    private GameObject popup;

    public void SetItem(SOItem _item)
    {
        item = _item;
        itemName =_item.itemName;
        itemImg.sprite = _item.icon;
        itemNameText.text = itemName;
        if (_item.itemCost == 0)
        {
            itemPriceText.text = "-";
        }
        else
        {
            itemPriceText.text = _item.itemCost + "냥";
        }
        itemDescriptionText.text = _item.itemDescription;
    }

    public void setBag(BagControl bag)
    {
        myBag = bag;
    }

    public void setPop(GameObject p)
    {
        popup = p;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        int max_budget = GameMgr.Instance.money - myBag.total_price;
        confirmUI confirm = popup.GetComponent<confirmUI>();
        // 우클릭을 감지하려면 eventData.button을 확인
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // 우클릭 시 장바구니에 담기
            if (max_budget >= item.itemCost)
                myBag.addItem(item);
        }
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Debug.Log("좌클릭");
            // 좌클릭 시 팝업창(구매수량 관련)
            popup.SetActive(true);
            confirm.setConfirm(item);
        }
    }
}
