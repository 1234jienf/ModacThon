using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Image 사용을 위해 추가
using TMPro;
using Unity.VisualScripting;

public class LearnRecipeManager : MonoBehaviour
{
    public static LearnRecipeManager Instance { get; private set; }
    public TextMeshProUGUI levelText; // 레시피 레벨을 표시할 필드
    public TextMeshProUGUI ingredientText; // 레시피 재료를 표시할 필드
    public TextMeshProUGUI messageText; // 요리를 배울지 묻는 메시지를 표시할 필드
    public Image foodImage; // 음식 이미지를 표시할 필드
    public Recipe _recipe;
    public RPListControl listControl;
    public MaxMenuCountControl ForInfo;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        ForInfo = FindObjectOfType<MaxMenuCountControl>();
    }

    public void ShowRecipeDetails(Recipe recipe)
    {
        if (recipe != null)
        {
            _recipe = recipe;
            string levelTextValue = "";
            switch (recipe.Level)
            {
                case 1:
                    levelTextValue = "<초급 요리>";
                    break;
                case 2:
                    levelTextValue = "<중급 요리>";
                    break;
                case 3:
                    levelTextValue = "<고급 요리>";
                    break;
            }
            levelText.text = levelTextValue;
            ingredientText.text = "재료: " + recipe.Ingredient;
            messageText.text = $"{recipe.Name} 요리를 배우시겠습니까?";
            foodImage.sprite = recipe.Food; // 이미지 설정
        }
    }

    public void learnRecipe()
    {
        bool flag = false;

        string code = "";

        for (int i = 0; i < ForInfo.info.recipes.Count; ++i) {
            if (ForInfo.info.recipes[i].itemName == _recipe.Name) {
                code = ForInfo.info.recipes[i].itemCode;
            }
        }

        for (int i = 0; i < GameMgr.Instance.learnedList.Count; ++i) {
            if (GameMgr.Instance.learnedList[i] == code) {
                flag = true;
            }
        }

        if (!flag) {
            // 배운 목록에 추가
            GameMgr.Instance.learnedList.Add(code);

            // UI 제거
            GameObject.Find(_recipe.Name).SetActive(false);

            // 인벤토리에서 레시피 제거
            for(int i = 0; i < GameMgr.Instance.inventoryControl.GetSlots()[3].Length; ++i) {
                ItemSlot itemSlot = GameMgr.Instance.inventoryControl.GetSlots()[3][i];
                if (itemSlot.item != null && itemSlot.item.itemCode == code) {
                    itemSlot.ClearSlot();
                }
            }
        }
        listControl.addRecipe(_recipe);
    }
}
