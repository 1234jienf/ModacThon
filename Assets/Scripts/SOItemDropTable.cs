using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public class SOItemDropEntry
{
    public SOItem item;
    public int minCount = 1;
    public int maxCount = 1;
    [Range(0f, 1f)]
    public float dropRate = 1f;
}

[CreateAssetMenu(fileName = "SOItemDropTable", menuName = "Items/Drop Table")]
public class SOItemDropTable : ScriptableObject
{
    public List<SOItemDropEntry> entries = new List<SOItemDropEntry>();

    public List<Tuple<SOItem, int>> CreateItemList()
    {
        List<Tuple<SOItem, int>> itemList = new List<Tuple<SOItem, int>>();

        foreach (SOItemDropEntry entry in entries)
        {
            if (entry.item == null || UnityEngine.Random.value > entry.dropRate)
            {
                continue;
            }

            int count = UnityEngine.Random.Range(entry.minCount, entry.maxCount + 1);
            itemList.Add(new Tuple<SOItem, int>(entry.item, Mathf.Max(1, count)));
        }

        return itemList;
    }

    public void DropItem(Tilemap tilemap, Vector3 position)
    {
        foreach (Tuple<SOItem, int> item in CreateItemList())
        {
            if (item.Item1.prefab == null)
            {
                continue;
            }

            Vector3 spawnPosition = tilemap != null
                ? tilemap.GetCellCenterWorld(tilemap.WorldToCell(position))
                : position;

            for (int i = 0; i < item.Item2; i++)
            {
                UnityEngine.Object.Instantiate(item.Item1.prefab, spawnPosition, Quaternion.identity);
            }
        }
    }
}
