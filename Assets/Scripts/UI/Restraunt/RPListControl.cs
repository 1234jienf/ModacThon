using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class recipe_DB
{
    public List<Recipe> recipes;
}

public class RPListControl : MonoBehaviour
{
    public GameObject recipe;
    public Transform ContentParent;

    public MaxMenuCountControl ForInfo;
    public recipe_DB rpList;

    void Start()
    {
        ForInfo = FindObjectOfType<MaxMenuCountControl>();

        foreach (var rp in rpList.recipes)
        {
            GameObject newRp = Instantiate(recipe, ContentParent);
            newRp.GetComponent<RecipeUIManager>().setRecipe(rp);
            newRp.SetActive(false);

            List<string> learned = GameMgr.Instance.learnedList;

            string code = "";

            for (int i = 0; i < ForInfo.info.recipes.Count; ++i) {
                if (ForInfo.info.recipes[i].itemName == rp.Name) {
                    code = ForInfo.info.recipes[i].itemCode;
                    break;
                }
            }

            for (int i = 0; i < learned.Count; ++i) {
                if (code == learned[i]) {
                    newRp.SetActive(true);
                    break;
                }
            }
        }
    }

    // ������ recipe�� �ش��ϴ� object�� �ٽ� ǥ��
    public void addRecipe(Recipe newRecipe)
    {
        foreach (Transform child in ContentParent)
        {           
            RecipeUIManager rpUI = child.gameObject.GetComponent<RecipeUIManager>();
            if (rpUI.recipe != null && rpUI.recipe.name == newRecipe.name)
            {
                // ��ġ�ϴ� �����Ǹ� ã������ �ش� GameObject�� Ȱ��ȭ�մϴ�.
                child.gameObject.SetActive(true);
                break;
            }
        }
    }
}
