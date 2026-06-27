using System.Net;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class confirmUI : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    public SOItem item;

    public Image item_img;
    public TMP_Text item_name;
    public TMP_Text item_description;
    public TMP_Text food_list;

    public TMP_InputField numberInputField;
    public Button btn_up, btn_down, btn_max, btn_add_bag;
    public int count;

    public BagControl myBag;

    public bool is_buy;
    public int max_budget;
    public int max_count;
    public ItemSlot_Sell slot;

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

    void Start()
    {
        btn_up.onClick.AddListener(IncreaseCount);
        btn_down.onClick.AddListener(DecreaseCount);
        btn_max.onClick.AddListener(MaxCount);
        btn_add_bag.onClick.AddListener(AddBag);
        numberInputField.onValueChanged.AddListener(HandleValueChange);
    }


    void IncreaseCount()
    {
        if (count < max_count)
        {
            count++;
            UpdateInputFieldValue();
        }    
    }
    void DecreaseCount()
    {
        if (count > 0)
        {
            count--;
            UpdateInputFieldValue();
        } 
    }
    void MaxCount()
    {
        count = max_count;
        UpdateInputFieldValue();
    }
    void AddBag()
    {
        myBag.addItem(item, count);
        if (!is_buy)
        {
            slot.SetSlotCount(-count);
        }
        gameObject.SetActive(false);
    }

    void UpdateInputFieldValue()
    {
        Debug.Log(count);
        numberInputField.text = count.ToString();
    }
    public void HandleValueChange(string value)
    {
        if (int.TryParse(value, out int new_value))
        {
            if(new_value < 0)
            {
                count = 0;
            }
            else if (new_value > max_count)
            {
                count = max_count;
            }
            else
            {
                count = new_value;
            }
            UpdateInputFieldValue();
        }
        else
        {
            Debug.Log("유효하지 않은 입력값");
            count = 0;
            UpdateInputFieldValue();
        }
    }
    public void setConfirm(SOItem _item, int cnt = 1)
    {
        item = _item;
        item_img.sprite = _item.icon;
        item_name.text = _item.itemName;
        item_description.text = _item.itemDescription;

        food_list.text = "임시값";
        count = 0;
        UpdateInputFieldValue();
        transform.position = startPos;
        is_buy = myBag.is_buy;
        max_budget = (GameMgr.Instance.money - myBag.total_price);
        if (is_buy)
        {
            max_count = (max_budget / item.itemCost);
        }
        else
        {
            max_count = cnt;
        }
    }

    public void setSlot(ItemSlot_Sell _slot)
    {
        slot = _slot;
    }

}
