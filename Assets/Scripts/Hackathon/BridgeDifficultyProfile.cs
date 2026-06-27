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
                    mapBDifficultyWeight = 0.55f
                };
            case 3:
                return new BridgeDifficultyPreset
                {
                    level = 3,
                    divideCount = 7,
                    minNodeSizeToDivide = 7,
                    enemyCount = 300,
                    maxLakeCoverage = 0.16f,
                    lakeMinDistanceFromPath = 0,
                    pathAdjacentLakePatches = 6,
                    enemyRoamRadius = 1.8f,
                    mapBDifficultyWeight = 0.85f
                };
            default:
                return new BridgeDifficultyPreset
                {
                    level = 2,
                    divideCount = 5,
                    minNodeSizeToDivide = 9,
                    enemyCount = 180,
                    maxLakeCoverage = 0.10f,
                    lakeMinDistanceFromPath = 1,
                    pathAdjacentLakePatches = 3,
                    enemyRoamRadius = 2.2f,
                    mapBDifficultyWeight = 0.72f
                };
        }
    }
}
