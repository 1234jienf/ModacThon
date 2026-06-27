using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class LakeTileMatcher
{
    private static readonly (string slot, string[] keywords)[] SlotKeywords =
    {
        ("c", new[] { "center", "_c", " c", "middle" }),
        ("tl", new[] { "top_left", "topleft", "top left", "_tl", " tl" }),
        ("tr", new[] { "top_right", "topright", "top right", "_tr", " tr" }),
        ("bl", new[] { "bottom_left", "bottomleft", "bottom left", "_bl", " bl" }),
        ("br", new[] { "bottom_right", "bottomright", "bottom right", "_br", " br" }),
        ("t", new[] { "top", "_t", " t" }),
        ("b", new[] { "bottom", "_b", " b" }),
        ("l", new[] { "left", "_l", " l" }),
        ("r", new[] { "right", "_r", " r" })
    };

    public static int FillLakeTiles(TileBase[] lakeTiles, Tilemap source)
    {
        if (lakeTiles == null || source == null)
        {
            return 0;
        }

        List<TileBase> uniqueTiles = CollectUniqueTiles(source);
        HashSet<TileBase> used = new HashSet<TileBase>();

        for (int i = 0; i < lakeTiles.Length; i++)
        {
            lakeTiles[i] = null;
        }

        int assigned = 0;
        foreach ((string slot, string[] keywords) in SlotKeywords)
        {
            int index = Array.IndexOf(TileBlendLayouts.LakeSlotNames, slot);
            if (index < 0 || index >= lakeTiles.Length)
            {
                continue;
            }

            TileBase matched = FindBestMatch(uniqueTiles, used, keywords);
            if (matched == null)
            {
                continue;
            }

            lakeTiles[index] = matched;
            used.Add(matched);
            assigned++;
        }

        return assigned;
    }

    private static TileBase FindBestMatch(List<TileBase> tiles, HashSet<TileBase> used, string[] keywords)
    {
        TileBase best = null;
        int bestScore = int.MinValue;

        foreach (TileBase tile in tiles)
        {
            if (used.Contains(tile))
            {
                continue;
            }

            int score = ScoreTileName(tile.name, keywords);
            if (score > bestScore)
            {
                bestScore = score;
                best = tile;
            }
        }

        return bestScore > 0 ? best : null;
    }

    private static int ScoreTileName(string tileName, string[] keywords)
    {
        string normalized = tileName.ToLower().Replace("-", "_").Replace(" ", "_");
        int best = 0;

        foreach (string keyword in keywords)
        {
            string key = keyword.ToLower().Replace("-", "_").Replace(" ", "_");
            if (normalized.Contains(key))
            {
                best = Mathf.Max(best, key.Length);
            }
        }

        return best;
    }

    private static List<TileBase> CollectUniqueTiles(Tilemap tilemap)
    {
        HashSet<TileBase> unique = new HashSet<TileBase>();
        BoundsInt bounds = tilemap.cellBounds;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                TileBase tile = tilemap.GetTile(new Vector3Int(x, y, 0));
                if (tile != null)
                {
                    unique.Add(tile);
                }
            }
        }

        return new List<TileBase>(unique);
    }
}
