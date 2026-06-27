using UnityEngine;

[CreateAssetMenu(fileName = "SOItem", menuName = "Items/Item")]
public class SOItem : ScriptableObject
{
    public string itemCode;
    public string itemName;
    [TextArea]
    public string itemDescription;
    public int itemCost;
    public Sprite icon;
    public GameObject prefab;
}
