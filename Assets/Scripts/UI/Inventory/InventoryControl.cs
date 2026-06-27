using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

// Inventory 정보 type
[System.Serializable]
public class InventoryItem
{
    public string itemCode;
    public int count;
}


public class InventoryControl : MonoBehaviour
{
    private bool inventroy_show;
    public GameObject Inventory;
    public ItemInfo info;
    public int money;
    public int[] capacity;
    public TMP_Text money_text;
    public TMP_Text capacity_text;


    // Category 변경
    public Button button0;
    public Button button1;
    public Button button2;
    public Button button3;
    private Button[] buttons; // 버튼 배열

    public Color selectedColor = Color.white; // 선택된 버튼의 색
    private Color defaultColor; // 기본 버튼 색상


    // category 별 slot
    private int categoryNum;
    public GameObject[] SlotsParent;
    private List<ItemSlot[]> slots;

    // draggableUI 담기용
    public draggableUI draggable;

    // Load Json(Inventory Content)
    public List<ItemSlot[]> GetSlots() { return slots; }

    public void LoadMoney(int _money)
    {
        money = GameMgr.Instance.money;
        money_text.text = money + "냥";
    }
    public void LoadToInven(int _arrayNum, string _itemCode, int _itemCnt)
    {
        for (int i = 0; i < info.weapon.Count; i++)
            if (info.weapon[i].itemCode == _itemCode)
                slots[0][_arrayNum].AddItem(info.weapon[i], _itemCnt);

        for (int i = 0; i < info.vehicle.Count; i++)
            if (info.vehicle[i].itemCode == _itemCode)
                slots[1][_arrayNum].AddItem(info.vehicle[i], _itemCnt);
        
        for (int i = 0; i < info.ingredients.Count; i++)
            if (info.ingredients[i].itemCode == _itemCode)
                slots[2][_arrayNum].AddItem(info.ingredients[i], _itemCnt);
        
        for (int i = 0; i < info.recipes.Count; i++)
            if (info.recipes[i].itemCode == _itemCode)
                slots[3][_arrayNum].AddItem(info.recipes[i], _itemCnt);
           
    }
    
    // 탭 변경
    public void switchCategory()
    {
        foreach (var _slot in SlotsParent)
        {
            _slot.SetActive(false);
        }
        SlotsParent[categoryNum].SetActive(true);
    }
 

    // 아이템 획득
    public void addItem(SOItem _item, int count = 1)
    {
        string category = _item.itemCode.Substring(0, 2);
        int slot_idx;
        switch (category)
        {
            case "wp":
                slot_idx = 0;
                break;

            case "vh":
                slot_idx = 1;
                break;

            case "ig":
                slot_idx = 2;
                break;

            default:
                slot_idx = 3;
                break;
        }
        for (int i = 0; i < slots[slot_idx].Length; i++)
        {
            if (slots[slot_idx][i].item != null)
            {
                if (slots[slot_idx][i].item.itemCode == _item.itemCode)
                {
                    slots[slot_idx][i].SetSlotCount(count);
                    return;
                }
            }
        }

        for (int i = 0; i < slots[slot_idx].Length; i++)
        {
            if (slots[slot_idx][i].item == null)
            {
                slots[slot_idx][i].AddItem(_item, count);
                return;
            }
        }
    }


    // ---버튼조작---
    public void ActivateSlot(int caseNumber)
    {
        // 모든 GameObject를 먼저 비활성화
        foreach (var slot in SlotsParent)
        {
            slot.SetActive(false);
        }

        SlotsParent[caseNumber].SetActive(true);  
    }

    void ButtonClicked(Button clickedButton, int num)
    {
        foreach (var button in buttons)
        {
            button.interactable = true; // 버튼을 다시 클릭 가능하게 합니다.
            SetButtonColor(button, defaultColor); // 기본 색상으로 설정
        }

        // 클릭된 버튼을 비활성화하고 색상을 변경
        clickedButton.interactable = false; // 클릭된 버튼을 비활성화
        SetButtonColor(clickedButton, selectedColor); // 선택된 색상으로 변경

        // 여기에 버튼 클릭에 따른 추가 로직을 구현합니다.
        categoryNum = num;
        ActivateSlot(categoryNum); 
    }
    // 버튼의 색상을 설정하는 메소드
    void SetButtonColor(Button button, Color color)
    {
        ColorBlock cb = button.colors;
        cb.normalColor = color;
        cb.highlightedColor = color;
        button.colors = cb;
    }

    // Start is called before the first frame update
    void Start()
    {
        slots = new List<ItemSlot[]>(); // slots 리스트를 초기화합니다.

        for (int i = 0; i < SlotsParent.Length; i++) // SlotsParent의 길이를 기준으로 반복
        {
            // SlotsParent의 각 GameObject에서 ItemSlot 컴포넌트 배열을 가져와서 slots 리스트에 추가
            slots.Add(SlotsParent[i].GetComponentsInChildren<ItemSlot>());
        }

        inventroy_show = false;
        categoryNum = 0;
        money_text.text = money + "냥";
        capacity_text.text = "010/020";
        

        buttons = new Button[] { button0, button1, button2, button3 };
        defaultColor = button0.colors.normalColor;

        // 각 버튼에 리스너 추가
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            buttons[i].onClick.AddListener(() => ButtonClicked(buttons[index], index));
            capacity[i] = 0;
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (SceneManager.GetActiveScene().name == "Hunting Ground")
        {
            if (Input.GetKeyDown(KeyCode.I))
            {   
                inventroy_show = !inventroy_show;
            }
            
            Inventory.SetActive(inventroy_show);
        }
        else
        {
            if(Inventory.activeSelf) Inventory.SetActive(false);
        }
        
    }
}