using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RecipeDetail
{
    public SOItem ig_code;
    public int amount = 1;
}

[CreateAssetMenu(fileName = "Recipe", menuName = "Items/Recipe")]
public class Recipe : ScriptableObject
{
    public string Name;
    public int Level;
    public Sprite Food;
    [TextArea]
    public string Ingredient;
    [TextArea]
    public string Process1;
    [TextArea]
    public string Process2;
    [TextArea]
    public string Process3;
    [TextArea]
    public string Process4;
    [TextArea]
    public string Process5;
    [TextArea]
    public string Process6;
    [TextArea]
    public string Process7;
    public List<RecipeDetail> recipeDetail = new List<RecipeDetail>();
}
