using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BusinessResult : MonoBehaviour
{
    public TextMeshProUGUI ResultText;
    public TextMeshProUGUI ResultSum;
    public GameObject Panel;
    public MenuManager menuManager;
    public QuestComplete questComplete;
    string ResultContent;
    int ResultCash = 0;

    // Start is called before the first frame update
    void Start()
    {
        menuManager = FindObjectOfType<MenuManager>();
        questComplete = FindObjectOfType<QuestComplete>();
    }

    public void AfterBusinessProcess() {
        
        Panel.SetActive(true);

        for (int i = 0; i < menuManager.SoldItems.Count; ++i) {
            questComplete.SoldFood.Add(menuManager.SoldItems[i].name, menuManager.SoldItems[i].currentAmount);

            bool flag = false;
            for (int j = 0; j < GameMgr.Instance.soldFood.Count; ++j) {
                if (GameMgr.Instance.soldFood[j].code == menuManager.SoldItems[i].itemCode) {
                    GameMgr.Instance.soldFood[j].count++;
                    flag = true;
                    break;
                }
            }

            if (!flag) {
                LoginManager.Food newItem = new LoginManager.Food();
                newItem.code = menuManager.SoldItems[i].itemCode;
                newItem.count = 1;
                GameMgr.Instance.soldFood.Add(newItem);
            }

            ResultContent += menuManager.SoldItems[i].name + ' ' + menuManager.SoldItems[i].currentAmount.ToString() + '개' + '\n';
            ResultCash += menuManager.SoldItems[i].MenuCost * menuManager.SoldItems[i].currentAmount;
        }

        ResultText.text = ResultContent;
        ResultSum.text = ResultCash.ToString() + '냥';

        int my_curr_money = GameMgr.Instance.money;
        my_curr_money += ResultCash;
        GameMgr.Instance.money = my_curr_money;
        GameMgr.Instance.inventoryControl.LoadMoney(my_curr_money);
        

        List<ItemSlot[]> itemslot = GameMgr.Instance.inventoryControl.GetSlots();

        for (int i = 0; i < menuManager.SoldItems.Count; ++i) {
            foreach (KeyValuePair<Ingredient, int> ing in menuManager.SoldItems[i].NeedIngredient) {
                for (int j = 0; j < itemslot[2].Length; ++j) {
                    if(itemslot[2][j].item != null) {
                        if (ing.Key.itemcode == itemslot[2][j].item.itemCode) {
                            GameMgr.Instance.inventoryControl.addItem(itemslot[2][j].item, -1 * (ing.Value * menuManager.SoldItems[i].currentAmount));
                            break;
                        }
                    }
                }
            }
        }
    }
}
