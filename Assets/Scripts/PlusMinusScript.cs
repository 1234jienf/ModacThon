using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlusMinusScript : MonoBehaviour
{
    public TMP_Text ScriptText;
    private MenuManager menuManager;
    public RecipeUIManager recipeUIManager;
    public MenuItem menuItem;
    private Recipe recipe;
    private MaxMenuCountControl control;

    // Start is called before the first frame update
    void Start()
    {
        ScriptText.text = "0";
        menuManager = FindObjectOfType<MenuManager>();
    
        recipe = recipeUIManager.recipe;
        control = FindObjectOfType<MaxMenuCountControl>();

        if (menuItem.NeedIngredient == null)
        {
            menuItem.NeedIngredient = new Dictionary<Ingredient, int>();
        }

        menuItem.image = recipe.Food;
        menuItem.name = recipe.Name;

        for (int i = 0; i < control.info.recipes.Count; ++i) {
            if (menuItem.name == control.info.recipes[i].itemName) {
                menuItem.MenuCost = control.info.recipes[i].itemCost;
            }
        }

        for (int i = 0; i < recipe.recipeDetail.Count; ++i) {
            Ingredient ing = new Ingredient();
            ing.itemcode = recipe.recipeDetail[i].ig_code.itemCode;
            ing.amount = 1;
            menuItem.NeedIngredient.Add(ing, recipe.recipeDetail[i].amount);
            ing = null;
        }
    }

    public void Plus()
    {
        if (menuManager.CanIncrease(ref menuItem))
        {
            //Debug.Log(menuItem.name);
            menuManager.UpdateIngredientsInc(ref menuItem);
            // Debug.Log(menuItem.currentAmount);
            int number = menuItem.currentAmount;
            ScriptText.text = number.ToString();
        }
        
    }

    public void Minus()
    {
        int CurNumber = menuItem.currentAmount;
        if (CurNumber > 0) {
            //Debug.Log(menuItem.name);
            menuManager.UpdateIngredientsDec(ref menuItem);
            // Debug.Log(menuItem.currentAmount);
            int number = menuItem.currentAmount;
            ScriptText.text = number.ToString();
        }
    }
}
