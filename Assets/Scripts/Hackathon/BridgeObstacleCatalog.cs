using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public class BridgeObstacleEntry
{
    public string id;
    public GameObject sourceObject;
    public char gridChar = 'O';
    [Min(1)] public int width = 1;
    [Min(1)] public int height = 1;
    public string sourceField;
}

public class BridgeObstacleCatalog : MonoBehaviour
{
    [Header("Field References")]
    public GameObject mapA;
    public GameObject mapB;

    [Header("Collected Obstacles")]
    public List<BridgeObstacleEntry> mapAObstacles = new List<BridgeObstacleEntry>();
    public List<BridgeObstacleEntry> mapBObstacles = new List<BridgeObstacleEntry>();
    public List<BridgeObstacleEntry> lakeObstacles = new List<BridgeObstacleEntry>();

    [Header("Auto Collect")]
    public bool autoCollectOnAwake = true;
    public string obstacleContainerName = "Obstacles";

    private void Awake()
    {
        if (autoCollectOnAwake)
        {
            EnsureCollected();
        }
    }

    public void EnsureCollected()
    {
        if (mapA == null || mapB == null)
        {
            BSP_Generator generator = GetComponent<BSP_Generator>();
            if (generator != null)
            {
                mapA = generator.MapA;
                mapB = generator.MapB;
            }
        }

        if (mapA != null)
        {
            mapAObstacles = CollectFromField(mapA, "map_a");
        }

        if (mapB != null)
        {
            mapBObstacles = CollectFromField(mapB, "map_b");
        }

        lakeObstacles = new List<BridgeObstacleEntry>();
        lakeObstacles.AddRange(mapAObstacles.FindAll(entry => entry.gridChar == 'w'));
        lakeObstacles.AddRange(mapBObstacles.FindAll(entry => entry.gridChar == 'w'));

        if (lakeObstacles.Count == 0)
        {
            lakeObstacles.Add(CreateFallbackLake("lake_default"));
        }

        if (mapAObstacles.Count == 0 && mapBObstacles.Count == 0)
        {
            mapAObstacles.Add(CreateFallbackSolid("rock_a"));
            mapBObstacles.Add(CreateFallbackSolid("rock_b"));
        }
    }

    [ContextMenu("Collect Obstacles From Fields")]
    public void CollectFromFieldsNow()
    {
        EnsureCollected();
        Debug.Log(
            $"BridgeObstacleCatalog: mapA={mapAObstacles.Count}, mapB={mapBObstacles.Count}, lakes={lakeObstacles.Count}");
    }

    public BridgeObstacleEntry PickEntry(float normalizedX, bool preferLake)
    {
        List<BridgeObstacleEntry> source = normalizedX < 0.5f ? mapAObstacles : mapBObstacles;
        if (source.Count == 0)
        {
            source = normalizedX < 0.5f ? mapBObstacles : mapAObstacles;
        }

        if (preferLake && lakeObstacles.Count > 0)
        {
            return lakeObstacles[UnityEngine.Random.Range(0, lakeObstacles.Count)];
        }

        List<BridgeObstacleEntry> solids = source.FindAll(entry => entry.gridChar != 'w');
        if (solids.Count == 0)
        {
            solids = source;
        }

        return solids[UnityEngine.Random.Range(0, solids.Count)];
    }

    private static List<BridgeObstacleEntry> CollectFromField(GameObject fieldRoot, string fieldId)
    {
        HashSet<int> seenIds = new HashSet<int>();
        List<BridgeObstacleEntry> entries = new List<BridgeObstacleEntry>();

        TilemapDataProvider provider = fieldRoot.GetComponent<TilemapDataProvider>();
        if (provider != null)
        {
            foreach (GameObject obstacle in provider.obstacles)
            {
                TryAddEntry(entries, seenIds, obstacle, fieldId);
            }
        }

        Transform obstacleRoot = FindChildByName(fieldRoot.transform, "Obstacles");
        if (obstacleRoot != null)
        {
            foreach (Transform child in obstacleRoot)
            {
                TryAddEntry(entries, seenIds, child.gameObject, fieldId);
            }
        }

        foreach (Transform child in fieldRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == fieldRoot.transform)
            {
                continue;
            }

            if (!IsObstacleCandidate(child.gameObject))
            {
                continue;
            }

            TryAddEntry(entries, seenIds, child.gameObject, fieldId);
        }

        return entries;
    }

    private static void TryAddEntry(
        List<BridgeObstacleEntry> entries,
        HashSet<int> seenIds,
        GameObject source,
        string fieldId)
    {
        if (source == null || !seenIds.Add(source.GetInstanceID()))
        {
            return;
        }

        entries.Add(CreateEntry(source, fieldId));
    }

    private static BridgeObstacleEntry CreateEntry(GameObject source, string fieldId)
    {
        char gridChar = ClassifyObstacle(source);
        Vector2Int footprint = EstimateFootprint(source);

        return new BridgeObstacleEntry
        {
            id = source.name,
            sourceObject = source,
            gridChar = gridChar,
            width = footprint.x,
            height = footprint.y,
            sourceField = fieldId
        };
    }

    private static char ClassifyObstacle(GameObject source)
    {
        string lowered = source.name.ToLowerInvariant();
        if (source.CompareTag("Lake") || lowered.Contains("lake") || lowered.Contains("water"))
        {
            return 'w';
        }

        return 'O';
    }

    private static Vector2Int EstimateFootprint(GameObject source)
    {
        Collider2D collider = source.GetComponent<Collider2D>();
        if (collider != null)
        {
            Vector2 size = collider.bounds.size;
            return new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(size.x)),
                Mathf.Max(1, Mathf.RoundToInt(size.y)));
        }

        SpriteRenderer renderer = source.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            Vector2 size = renderer.bounds.size;
            return new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(size.x)),
                Mathf.Max(1, Mathf.RoundToInt(size.y)));
        }

        return Vector2Int.one;
    }

    private static bool IsObstacleCandidate(GameObject source)
    {
        if (source == null || !source.activeInHierarchy)
        {
            return false;
        }

        string lowered = source.name.ToLowerInvariant();
        if (source.CompareTag("Lake") || lowered.Contains("lake") || lowered.Contains("water"))
        {
            return true;
        }

        if (lowered.Contains("obstacle") || lowered.Contains("rock") || lowered.Contains("tree") || lowered.Contains("bush"))
        {
            return true;
        }

        if (source.GetComponent<Collider2D>() != null && source.GetComponent<Tilemap>() == null)
        {
            Transform parent = source.transform.parent;
            if (parent != null)
            {
                string parentName = parent.name.ToLowerInvariant();
                if (parentName.Contains("obstacle") || parentName.Contains("decor") || parentName.Contains("prop"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private static BridgeObstacleEntry CreateFallbackLake(string id)
    {
        return new BridgeObstacleEntry
        {
            id = id,
            gridChar = 'w',
            width = 2,
            height = 2,
            sourceField = "fallback"
        };
    }

    private static BridgeObstacleEntry CreateFallbackSolid(string id)
    {
        return new BridgeObstacleEntry
        {
            id = id,
            gridChar = 'O',
            width = 1,
            height = 1,
            sourceField = "fallback"
        };
    }
}
