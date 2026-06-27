using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class ShopManager : MonoBehaviour
{
    private GameMgr gameMgr;
    public GameObject itemPrefab; // 아이템 Prefab


    public GameObject WP_enchant;
    public GameObject wp_event;

    public GameObject VH_train;
    public GameObject vh_event;

    public GameObject IG_list_buy;
    public GameObject IG_list_sell;
    public GameObject IG_list_scroll_parent;
    private ItemSlot_Sell[] Inven_IG_list;
    public GameObject ig_event;

    public ItemInfo itemInfoDB;

    public Button btn_w, btn_v, btn_i;

    public Toggle toggle_buy, toggle_sell;
    public bool is_buy;
    public TMP_Text btn_buy_text;

    public BagControl mybag;
    public int weapon;
    public int vehicle;

    public Button btn_enchant, btn_train;
    public TMP_Text money_text;

    public GameObject popup;

    private void Awake()
    {
        gameMgr = GameMgr.Instance;
    }
    void Start()
    {
        weapon = gameMgr.currentArm;
        vehicle = gameMgr.currentVehicle;

        // button eventListner
        btn_w.onClick.AddListener(() => showTap(0));
        btn_v.onClick.AddListener(() => showTap(1));
        btn_i.onClick.AddListener(() => showTap(2));

        btn_enchant.onClick.AddListener(() => enchant());
        btn_train.onClick.AddListener(() => train());
        
        // wp_enchant 내용 표시
        updateWeapon();

        // vh_train 내용 표시
        updateVehicle();

        // ig_list 내용 표시 - 구매용
        for (int i = 0; i < itemInfoDB.ingredients.Count; i++)
        {
            GameObject newItem = Instantiate(itemPrefab, IG_list_buy.transform);
            newItem.GetComponent<ItemUI>().SetItem(itemInfoDB.ingredients[i]);
            newItem.GetComponent<ItemUI>().setBag(mybag);
            newItem.GetComponent<ItemUI>().setPop(popup);
        }

        // ig_list 내용 표시 - 판매용(inventory)
        Inven_IG_list = IG_list_scroll_parent.GetComponentsInChildren<ItemSlot_Sell>();
        updateSell();


        // toggle buy <-> sell
        toggle_buy.onValueChanged.AddListener((isOn) => {
            if (isOn)
            {
                mybag.EmptyBag();
                is_buy = true;
                btn_buy_text.text = "구매하기";
                IG_list_sell.SetActive(false);
                IG_list_buy.SetActive(true);
            }
        });

        toggle_sell.onValueChanged.AddListener((isOn) => {
            if (isOn)
            {
                mybag.EmptyBag();
                updateSell();
                is_buy = false;
                btn_buy_text.text = "판매하기";
                IG_list_buy.SetActive(false);
                IG_list_sell.SetActive(true);
            }
        });

        popup.SetActive(false);

        VH_train.SetActive(false);
        vh_event.SetActive(false);
        IG_list_buy.SetActive(false);
        IG_list_sell.SetActive(false);
        ig_event.SetActive(false);

        toggle_buy.isOn = true;
    }

    public void showTap(int itemTap)
    {
        WP_enchant.SetActive(false);
        wp_event.SetActive(false);
        VH_train.SetActive(false);
        vh_event.SetActive(false);
        IG_list_buy.SetActive(false);
        IG_list_sell .SetActive(false);
        ig_event.SetActive(false);
        popup.SetActive(false);

        switch (itemTap)
        {
            case 0:
                WP_enchant.SetActive(true);
                wp_event.SetActive(true);
                break;
            case 1:
                VH_train.SetActive(true);
                vh_event.SetActive(true);
                break;
            case 2:
                toggle_buy.isOn = true;
                IG_list_buy.SetActive(true);
                ig_event.SetActive(true);
                break;

            default:
                WP_enchant.SetActive(true);
                wp_event.SetActive(true);
                break;
        }
    }

    private void updateWeapon()
    {
        switch (weapon)
        {
            case 0:
                WP_enchant.GetComponent<ItemUI>().SetItem(itemInfoDB.weapon[0]);
                break;
            case 1:
                WP_enchant.GetComponent<ItemUI>().SetItem(itemInfoDB.weapon[1]);
                break;
            case 2:
                WP_enchant.GetComponent<ItemUI>().SetItem(itemInfoDB.weapon[2]);
                break;
            case 3:
                WP_enchant.GetComponent<ItemUI>().SetItem(itemInfoDB.weapon[3]);
                break;
        }
    }
    
    private void updateVehicle()
    {
        switch (vehicle)
        {
            case 0:
                VH_train.GetComponent<ItemUI>().SetItem(itemInfoDB.vehicle[0]);
                break;
            case 1:
                VH_train.GetComponent<ItemUI>().SetItem(itemInfoDB.vehicle[1]);
                break;
            case 2:
                VH_train.GetComponent<ItemUI>().SetItem(itemInfoDB.vehicle[2]);
                break;
            case 3:
                VH_train.GetComponent<ItemUI>().SetItem(itemInfoDB.vehicle[3]);
                break;
        }
    }

    public void updateSell()
    {
        foreach (var slot in Inven_IG_list)
        {
            slot.ClearSlot(); 
        }
        for (int i = 0; i < gameMgr.inventoryControl.GetSlots()[2].Length; i++)
        {
            ItemSlot temp = gameMgr.inventoryControl.GetSlots()[2][i];
            if (temp.item != null)
            {
                Inven_IG_list[i].AddItem(temp.item, temp.itemCount);
                Inven_IG_list[i].SetBag(mybag);
                Inven_IG_list[i].setPop(popup);
            }            
        }
    }

    private void enchant()
    {
        Debug.Log("강화");
        Debug.Log(WP_enchant.GetComponent<ItemUI>().item.itemCost);
        int money = gameMgr.GetComponentInChildren<InventoryControl>().money;
        if (weapon < 3)
        {
            int price = WP_enchant.GetComponent<ItemUI>().item.itemCost;
            if (money >= price)
            {
                weapon++;
                gameMgr.currentArm = weapon;
                money -= price;
                gameMgr.money = money;
                gameMgr.inventoryControl.LoadMoney(money);
                money_text.text = "현재가진 금액\n" + money + "냥";
                updateWeapon();
            }
        }
    }

    private void train()
    {
        Debug.Log("훈련");
        int money = gameMgr.GetComponentInChildren<InventoryControl>().money;
        if (vehicle < 3)
        {
            int price = VH_train.GetComponent<ItemUI>().item.itemCost;
            if (money >= price)
            {
                vehicle++;
                gameMgr.currentVehicle = vehicle;
                money -= price;
                gameMgr.money = money;
                gameMgr.inventoryControl.LoadMoney(money);
                money_text.text = "현재가진 금액\n" + money + "냥";
                updateVehicle();
            }
        }
    }
}
