using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecipeUIManager : MonoBehaviour
{
    public Recipe recipe;
    public Image foodImage; // 음식 이미지를 표시할 필드
    public GameObject obj;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI ingredientText;
    public TextMeshProUGUI process1Text;
    public TextMeshProUGUI process2Text;
    public TextMeshProUGUI process3Text;
    public TextMeshProUGUI process4Text;
    public TextMeshProUGUI process5Text;
    public TextMeshProUGUI process6Text;
    public TextMeshProUGUI process7Text;
    public MaxMenuCountControl ForInfo;
    public bool AlreadyLearned;

    public void SelectRecipe()
    {
        LearnRecipeManager.Instance.ShowRecipeDetails(recipe);
    }
    
    void Start()
    {
        AlreadyLearned = false;
        ForInfo = FindObjectOfType<MaxMenuCountControl>();
        
        string code = "";

        for (int i = 0; i < ForInfo.info.recipes.Count; ++i) {
            if (ForInfo.info.recipes[i].itemName == recipe.Name) {
                code = ForInfo.info.recipes[i].itemCode;
                break;
            }
        }

        if (obj != null) {
            obj.SetActive(false);
        }   
        

        for (int i = 0; i < GameMgr.Instance.learnedList.Count; ++i) {
            if (code == GameMgr.Instance.learnedList[i]) {
                AlreadyLearned = true;
            }
        }

        for (int j = 0; j < GameMgr.Instance.inventoryControl.GetSlots()[3].Length; ++j) {
            ItemSlot itemSlot = GameMgr.Instance.inventoryControl.GetSlots()[3][j];
            if (obj != null && itemSlot.item != null && code == itemSlot.item.itemCode && !AlreadyLearned) {
                obj.SetActive(true);
                break;
            }
        }

        if (recipe == null)
        {
            Debug.LogError("Recipe is not assigned in RecipeUIManager.");
            return;
        }

        nameText.text = recipe.Name ?? ""; // recipe.Name이 null이면 빈 문자열을 할당
        if (ingredientText != null) {
            ingredientText.text = recipe.Ingredient ?? ""; // recipe.Ingredient가 null이면 빈 문자열을 할당
        }
        

        // foodImage.sprite = recipe.Food; // recipe.Food가 null이더라도 예외가 발생하지 않습니다.

        // SetProcessText(process1Text, recipe.Process1);
        // SetProcessText(process2Text, recipe.Process2);
        // SetProcessText(process3Text, recipe.Process3);
        // SetProcessText(process4Text, recipe.Process4);
        // SetProcessText(process5Text, recipe.Process5);
        // SetProcessText(process6Text, recipe.Process6);
        // SetProcessText(process7Text, recipe.Process7);
        
    }
    
    private void SetProcessText(TextMeshProUGUI textComponent, string value)
    {
        if (textComponent != null)
        {
        textComponent.text = value ?? "";
        }
    }

    public void setRecipe(Recipe r)
    {
        recipe = r;
        nameText.text = r.Name ?? ""; // recipe.Name이 null이면 빈 문자열을 할당
        //ingredientText.text = r.Ingredient ?? ""; // recipe.Ingredient가 null이면 빈 문자열을 할당

        foodImage.sprite = r.Food; // recipe.Food가 null이더라도 예외가 발생하지 않습니다.

        SetProcessText(process1Text, r.Process1);
        SetProcessText(process2Text, r.Process2);
        SetProcessText(process3Text, r.Process3);
        SetProcessText(process4Text, r.Process4);
        SetProcessText(process5Text, r.Process5);
        SetProcessText(process6Text, r.Process6);
        SetProcessText(process7Text, r.Process7);
    }

}