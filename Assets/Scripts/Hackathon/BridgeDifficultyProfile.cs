using System;
using UnityEngine;

[Serializable]
public struct BridgeDifficultyPreset
{
    public int level;
    public int divideCount;
    public int minNodeSizeToDivide;
    public int enemyCount;
    public float maxLakeCoverage;
    public int lakeMinDistanceFromPath;
    public int pathAdjacentLakePatches;
    public float enemyRoamRadius;
    public float mapBDifficultyWeight;
    public int enemyBlockedRadius;
    public int enemyAvoidanceRadius;
    public float enemyAvoidanceCost;
    public float grassDetourCost;
    public bool allowGrassDetourWhenBlocked;
}

public static class BridgeDifficultyProfile
{
    public static BridgeDifficultyPreset GetPreset(int level)
    {
        switch (Mathf.Clamp(level, 1, 3))
        {
            case 1:
                return new BridgeDifficultyPreset
                {
                    level = 1,
                    divideCount = 3,
                    minNodeSizeToDivide = 12,
                    enemyCount = 80,
                    maxLakeCoverage = 0.06f,
                    lakeMinDistanceFromPath = 2,
                    pathAdjacentLakePatches = 0,
                    enemyRoamRadius = 2.5f,
                    mapBDifficultyWeight = 0.55f,
                    enemyBlockedRadius = 1,
                    enemyAvoidanceRadius = 2,
                    enemyAvoidanceCost = 55f,
                    grassDetourCost = 18f,
                    allowGrassDetourWhenBlocked = true
                };
            case 3:
                return new BridgeDifficultyPreset
                {
                    level = 3,
                    divideCount = 8,
                    minNodeSizeToDivide = 6,
                    enemyCount = 300,
                    maxLakeCoverage = 0.16f,
                    lakeMinDistanceFromPath = 0,
                    pathAdjacentLakePatches = 6,
                    enemyRoamRadius = 1.8f,
                    mapBDifficultyWeight = 0.85f,
                    enemyBlockedRadius = 2,
                    enemyAvoidanceRadius = 3,
                    enemyAvoidanceCost = 180f,
                    grassDetourCost = 55f,
                    allowGrassDetourWhenBlocked = true
                };
            default:
                return new BridgeDifficultyPreset
                {
                    level = 2,
                    divideCount = 6,
                    minNodeSizeToDivide = 8,
                    enemyCount = 180,
                    maxLakeCoverage = 0.10f,
                    lakeMinDistanceFromPath = 1,
                    pathAdjacentLakePatches = 3,
                    enemyRoamRadius = 2.2f,
                    mapBDifficultyWeight = 0.72f,
                    enemyBlockedRadius = 1,
                    enemyAvoidanceRadius = 2,
                    enemyAvoidanceCost = 95f,
                    grassDetourCost = 32f,
                    allowGrassDetourWhenBlocked = true
                };
        }
    }
}
