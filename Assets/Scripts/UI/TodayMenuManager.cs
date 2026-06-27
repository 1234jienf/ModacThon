using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;


[System.Serializable]
public class MenuPanelItem
{
    public string Name;
    public string Img;
    public string Ingredient;
}

[System.Serializable]
public class FoodDatabase
{ 
    public List<MenuPanelItem> cook;
}

public class TodayMenuManager : MonoBehaviour
{
    public GameObject itemPrefab;
    public Transform contentParent; // ScrollView�� Content Transform
    public FoodDatabase FoodDB;

    public static FoodDatabase LoadJsonData(string fileName)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            FoodDatabase jsonDB = JsonUtility.FromJson<FoodDatabase>(json);
            return jsonDB;
        }
        Debug.LogError("Cannot find file!");
        return null;
    }

    public static void SaveJsonData(string path, FoodDatabase modifiedDB)
    {
        string json = JsonUtility.ToJson(modifiedDB, true);
        File.WriteAllText(path, json);
    }

    void Start()
    {
        FoodDB = LoadJsonData("cancook.json");

        //playerInventory
        if (FoodDB.cook.Count != 0)
        {
            foreach (var food in FoodDB.cook)
            {
                GameObject newItem = Instantiate(itemPrefab, contentParent);
                MenuPanelUI itemUI = newItem.GetComponent<MenuPanelUI>();
                itemUI.SetItem(food.Img, food.Name, food.Ingredient);
            }
        }
    }
}
