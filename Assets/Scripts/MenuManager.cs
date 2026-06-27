using System.Collections.Generic; // For using List
using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class MenuItem {
    public string name;
    public int requiredAmount;
    public int currentAmount;
    public string itemCode;
    public int MenuCost;
    public Sprite image;

    public Dictionary<Ingredient, int> NeedIngredient;
}

[System.Serializable]
public class Ingredient {
    public string itemcode;
    public int amount;
}

public class MenuManager : MonoBehaviour {

    public List<MenuItem> todaysMenu; // List is part of System.Collections.Generic
    public List<MenuItem> menuItems;
    public List<Ingredient> ingredients;
    public Dictionary<MenuItem, int> SetMenus;
    private MaxMenuCountControl MenuCount;
    public GameObject itemPrefab;
    public Transform contentParent; // ScrollView�� Content Transform
    public List<MenuItem> SoldItems;
    public GameObject businessbutton;

    public TextMeshProUGUI RemainMenu;
    public string RemainMenuString;

    public bool isAnyMenu() {
        if (menuItems.Count == 0) {
            return false;
        }
        return true;
    }
    public int isIn(MenuItem menuItem) {
        for (int i = 0; i < menuItems.Count; ++i) {
            if (menuItems[i].name == menuItem.name)
                return i;
        }
        return -1;
    }
    public void LoadCount(string _itemCode, int _itemCnt) {
        for (int i = 0; i < MenuCount.info.ingredients.Count; i++)
            if (MenuCount.info.ingredients[i].itemCode == _itemCode){
                Ingredient ing = new Ingredient();
                ing.itemcode = _itemCode;
                ing.amount = _itemCnt;
                ingredients.Add(ing);
            }
    }

    public MenuItem GetRandomFoodItem() {
        if (todaysMenu.Count == 0) {
            Debug.LogError("Todays menu is empty.");
            return null;
        }
        
        int randomIndex = Random.Range(0, todaysMenu.Count);

        return todaysMenu[randomIndex];
    }

    public bool CanIncrease(ref MenuItem menuItem) {
        // 재료 체크 로직

        if (menuItem.name == "밥" || menuItem.name == "김치" || menuItem.name == "도토리묵") {
            if (menuItem.currentAmount < 3) {
                return true;
            }
            else {
                return false;
            }
        }

        if (ingredients.Count == 0) {
            return false;
        }
        foreach (KeyValuePair<Ingredient, int> ingredient in menuItem.NeedIngredient) {
            bool flag = false;
            // Debug.Log(ingredient.Key.itemcode);
            for (int i = 0; i < ingredients.Count; ++i) {
                if (ingredient.Key.itemcode == ingredients[i].itemcode) {
                    if (ingredient.Value <= ingredients[i].amount) {
                        flag = true;
                    }
                }
            }
            if(!flag) {
                return false;
            }
        }
        return true;
    }

    public void UpdateIngredientsInc(ref MenuItem menuItem) {
        // 재료 업데이트 로직
        foreach (KeyValuePair<Ingredient, int> ingredient in menuItem.NeedIngredient) {
                for (int i = 0; i < ingredients.Count; ++i) {
                    if (ingredients[i].itemcode == ingredient.Key.itemcode) {
                        ingredients[i].amount -= ingredient.Value;
                        break;
                    }
                }
            }

        int index = isIn(menuItem);
        if (index == -1) {
            MenuItem Ing = new MenuItem();
            Ing.currentAmount = 1;
            Ing.image = menuItem.image;
            Ing.MenuCost = menuItem.MenuCost;
            Ing.name = menuItem.name;
            Ing.NeedIngredient = menuItem.NeedIngredient;

            for (int i = 0; i < MenuCount.info.recipes.Count; ++i) {
                if (MenuCount.info.recipes[i].itemName == menuItem.name) {
                    Ing.itemCode = MenuCount.info.recipes[i].itemCode;
                    break;
                }
            }

            menuItems.Add(Ing);
        }
        else {
            menuItems[index].currentAmount++;
        }
        menuItem.currentAmount++;
    }

    public void UpdateIngredientsDec(ref MenuItem menuItem) {
        // 재료 업데이트 로직
        foreach (KeyValuePair<Ingredient, int> ingredient in menuItem.NeedIngredient) {
                for (int i = 0; i < ingredients.Count; ++i) {
                    if (ingredients[i].itemcode == ingredient.Key.itemcode) {
                        ingredients[i].amount += ingredient.Value;
                        break;
                    }
                }
            }
        
        int index = isIn(menuItem);
        menuItems[index].currentAmount--;

        if (menuItems[index].currentAmount == 0) {
            menuItems.RemoveAt(index);
        }

        menuItem.currentAmount--;
    }


    public void SaveMenu() {
        // 메뉴 저장 로직
        todaysMenu = menuItems;

        for (int j = 0; j < todaysMenu.Count; ++j) {
            for (int i = 0; i < MenuCount.info.recipes.Count; ++i) {
                if (todaysMenu[j].name == MenuCount.info.recipes[i].itemName) {
                    todaysMenu[j].MenuCost = MenuCount.info.recipes[i].itemCost;
                    break;
                }
            }
        }

        if (!isAnyMenu()) {
            businessbutton.SetActive(false);
        }
        else {
            businessbutton.SetActive(true);
        }
    }

    public void ItemSold(Sprite menuItem) {
        
        bool flag = false;

        for (int i = 0; i < SoldItems.Count; ++i) {
            if (SoldItems[i].image == menuItem) {
                SoldItems[i].currentAmount++;
                flag = true;
                break;
            }
        }

        if (!flag) {
            for (int i = 0; i < todaysMenu.Count; ++i) {
                if (todaysMenu[i].image == menuItem) {
                    MenuItem Ing = new MenuItem();
                    Ing.currentAmount = 1;
                    Ing.image = menuItem;
                    Ing.MenuCost = todaysMenu[i].MenuCost;
                    Ing.name = todaysMenu[i].name;
                    Ing.MenuCost = todaysMenu[i].MenuCost;
                    Ing.itemCode = todaysMenu[i].itemCode;
                    Ing.NeedIngredient = todaysMenu[i].NeedIngredient;
                    SoldItems.Add(Ing);
                    break;
                }
            }
        }

        for (int i = 0; i < todaysMenu.Count; ++i) {
            if (todaysMenu[i].image == menuItem) {
                todaysMenu[i].currentAmount--;

                if (todaysMenu[i].currentAmount == 0) {
                    todaysMenu.RemoveAt(i);
                }
                break;
            }
        }

        UpdateTodaysMenu();
    }

    public void UpdateTodaysMenu() {
        RemainMenuString = "";
        for (int i = 0; i < todaysMenu.Count; ++i) {
            RemainMenuString += todaysMenu[i].name + ' ' + todaysMenu[i].MenuCost + "냥 " + todaysMenu[i].currentAmount + '개' + '\n';
        }
        RemainMenu.text = RemainMenuString;
    }

    public void LoadData()
    {
        List<ItemSlot[]> itemslot = GameMgr.Instance.inventoryControl.GetSlots();
        for (int i = 0; i < itemslot[2].Length; ++i) {
            if(itemslot[2][i].item != null) {
                for (int j = 0; j < MenuCount.info.ingredients.Count; ++j) {
                    if (MenuCount.info.ingredients[j].itemCode == itemslot[2][i].item.itemCode) {
                        LoadCount(itemslot[2][i].item.itemCode, itemslot[2][i].itemCount);
                        break;
                    }
                }
            }
        }

        // for (int i = 0; i < ingredients.Count; ++i) {
        //     Debug.Log(ingredients[i].itemcode);
        //     Debug.Log(ingredients[i].amount);
        // }
    }

    void Start()
    {
        businessbutton.SetActive(false);

        MenuCount = FindObjectOfType<MaxMenuCountControl>();
        LoadData();
    }
}
