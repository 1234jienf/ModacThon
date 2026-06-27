using System;
using UnityEngine;

public enum BridgeDifficultyLevel
{
    Easy = 1,
    Normal = 2,
    Hard = 3
}

[Serializable]
public class BridgeDifficultyPreset
{
    public string id = "normal";
    public string displayName = "Normal";
    [Range(1, 6)] public int divideCount = 3;
    [Range(0.2f, 0.9f)] public float minRoomSizeRatio = 0.5f;
    [Range(0.3f, 1f)] public float maxRoomSizeRatio = 0.9f;
    [Min(4)] public int minNodeSizeToDivide = 10;
    [Range(1, 4)] public int maxRoomConnections = 3;
    public float noiseScale = 0.25f;
    public float noiseMagnitude = 1.8f;
    public bool useRandomPathWidth = true;
    [Range(1, 3)] public int fixedPathWidth = 2;
    [Range(1, 5)] public int transitionBandCount = 3;
    [Range(0f, 1f)] public float mapBThemeAtRight = 0.65f;

    [Header("Obstacles")]
    public bool placeLakes = true;
    public bool placeSolidObstacles = false;
    [Min(0)] public int lakePatchCountMin = 4;
    [Min(0)] public int lakePatchCountMax = 12;
    [Range(4, 16)] public int lakePatchMinSize = 8;
    [Range(4, 16)] public int lakePatchMaxSize = 12;
    [Range(0f, 1f)] public float lakeZoneStart = 0.25f;
    [Range(0f, 1f)] public float lakeZoneEnd = 0.75f;
    [Range(0, 8)] public int lakeMinDistanceFromPath = 0;
    [Tooltip("0이면 상한 없음. Easy는 min만 크게 두어 path에서 멀리 배치.")]
    [Range(0, 32)] public int lakeMaxDistanceFromPath = 0;
    [Range(0f, 1f)] public float lakePathBlockChance = 0f;

    public bool placeObstacles = false;
    [Min(0)] public int obstacleCountMin = 0;
    [Min(0)] public int obstacleCountMax = 0;
    [Range(0, 8)] public int minDistanceFromPath = 3;
    [Range(0, 8)] public int maxDistanceFromPath = 6;
    [Range(0f, 1f)] public float pathBlockChance = 0f;
    [Range(0f, 1f)] public float lakeChance = 0f;
}

public class BridgeDifficultySettings : MonoBehaviour
{
    public static BridgeDifficultySettings Instance { get; private set; }
    public static BridgeDifficultyPreset ActivePreset { get; private set; }
    public static int ActiveLevel { get; private set; } = 2;

    [Header("Play 전 선택 — Lake 배치 난이도")]
    public BridgeDifficultyLevel selectedDifficulty = BridgeDifficultyLevel.Normal;

    [Header("Features (optional)")]
    public bool paintMacroZones = true;
    public bool exportQaReport = true;

    [Header("Presets")]
    public BridgeDifficultyPreset level1Easy = CreateEasy();
    public BridgeDifficultyPreset level2Normal = CreateNormal();
    public BridgeDifficultyPreset level3Hard = CreateHard();

    private void Awake()
    {
        Instance = this;
        ApplySelectedLevel((int)selectedDifficulty);
    }

    public BridgeDifficultyPreset GetPreset(int level)
    {
        switch (Mathf.Clamp(level, 1, 3))
        {
            case 1:
                return level1Easy;
            case 3:
                return level3Hard;
            default:
                return level2Normal;
        }
    }

    public void ApplySelectedLevel(int level)
    {
        ActiveLevel = Mathf.Clamp(level, 1, 3);
        ActivePreset = GetPreset(ActiveLevel);
    }

    [ContextMenu("Apply Selected Difficulty Now")]
    public void ApplySelectedDifficultyNow()
    {
        ApplySelectedLevel((int)selectedDifficulty);
        Debug.Log($"Bridge difficulty applied: L{ActiveLevel} ({ActivePreset.displayName})");
    }

    private static BridgeDifficultyPreset CreateEasy()
    {
        return new BridgeDifficultyPreset
        {
            id = "easy",
            displayName = "Easy (L1)",
            divideCount = 2,
            minRoomSizeRatio = 0.55f,
            maxRoomSizeRatio = 0.88f,
            minNodeSizeToDivide = 12,
            maxRoomConnections = 2,
            noiseScale = 0.22f,
            noiseMagnitude = 1.0f,
            useRandomPathWidth = true,
            fixedPathWidth = 2,
            transitionBandCount = 2,
            mapBThemeAtRight = 0.35f,
            placeLakes = true,
            placeSolidObstacles = false,
            lakePatchCountMin = 4,
            lakePatchCountMax = 12,
            lakePatchMinSize = 8,
            lakePatchMaxSize = 12,
            lakeZoneStart = 0.1f,
            lakeZoneEnd = 0.95f,
            lakeMinDistanceFromPath = 0,
            lakeMaxDistanceFromPath = 0,
            lakePathBlockChance = 0f,
            lakeChance = 0.22f,
            placeObstacles = false
        };
    }

    private static BridgeDifficultyPreset CreateNormal()
    {
        return new BridgeDifficultyPreset
        {
            id = "normal",
            displayName = "Normal (L2)",
            divideCount = 3,
            minRoomSizeRatio = 0.5f,
            maxRoomSizeRatio = 0.9f,
            minNodeSizeToDivide = 10,
            maxRoomConnections = 3,
            noiseScale = 0.25f,
            noiseMagnitude = 1.8f,
            useRandomPathWidth = true,
            fixedPathWidth = 2,
            transitionBandCount = 3,
            mapBThemeAtRight = 0.65f,
            placeLakes = true,
            placeSolidObstacles = false,
            lakePatchCountMin = 6,
            lakePatchCountMax = 16,
            lakePatchMinSize = 9,
            lakePatchMaxSize = 14,
            lakeZoneStart = 0.08f,
            lakeZoneEnd = 0.98f,
            lakeMinDistanceFromPath = 0,
            lakeMaxDistanceFromPath = 0,
            lakePathBlockChance = 0f,
            lakeChance = 0.30f,
            placeObstacles = false
        };
    }

    private static BridgeDifficultyPreset CreateHard()
    {
        return new BridgeDifficultyPreset
        {
            id = "hard",
            displayName = "Hard (L3)",
            divideCount = 4,
            minRoomSizeRatio = 0.35f,
            maxRoomSizeRatio = 0.82f,
            minNodeSizeToDivide = 8,
            maxRoomConnections = 4,
            noiseScale = 0.32f,
            noiseMagnitude = 2.8f,
            useRandomPathWidth = true,
            fixedPathWidth = 2,
            transitionBandCount = 4,
            mapBThemeAtRight = 1.0f,
            placeLakes = true,
            placeSolidObstacles = false,
            lakePatchCountMin = 8,
            lakePatchCountMax = 20,
            lakePatchMinSize = 10,
            lakePatchMaxSize = 16,
            lakeZoneStart = 0.05f,
            lakeZoneEnd = 0.99f,
            lakeMinDistanceFromPath = 0,
            lakeMaxDistanceFromPath = 0,
            lakePathBlockChance = 0f,
            lakeChance = 0.38f,
            placeObstacles = false
        };
    }
}
