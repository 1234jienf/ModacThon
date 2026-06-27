using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemInfo", menuName = "Items/Item Info")]
public class ItemInfo : ScriptableObject
{
    public List<SOItem> weapon = new List<SOItem>();
    public List<SOItem> vehicle = new List<SOItem>();
    public List<SOItem> ingredients = new List<SOItem>();
    public List<SOItem> recipes = new List<SOItem>();
}
