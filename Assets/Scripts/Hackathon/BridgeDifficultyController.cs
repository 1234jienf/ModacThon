using UnityEngine;

[DefaultExecutionOrder(-200)]
public class BridgeDifficultyController : MonoBehaviour
{
    public static BridgeDifficultyController Instance { get; private set; }

    [Range(1, 3)]
    [SerializeField] private int difficultyLevel = 2;

    [SerializeField] private BSP_Generator bspGenerator;
    [SerializeField] private PathProcessor pathProcessor;
    [SerializeField] private EnemyTilemapPlacer enemyPlacer;

    public int DifficultyLevel => difficultyLevel;
    public BridgeDifficultyPreset ActivePreset { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (bspGenerator == null)
            bspGenerator = GetComponent<BSP_Generator>();
        if (pathProcessor == null)
            pathProcessor = GetComponent<PathProcessor>();
        if (enemyPlacer == null)
            enemyPlacer = FindObjectOfType<EnemyTilemapPlacer>();
    }

    public void ApplyDifficulty(int level)
    {
        difficultyLevel = Mathf.Clamp(level, 1, 3);
        ActivePreset = BridgeDifficultyProfile.GetPreset(difficultyLevel);

        if (bspGenerator != null)
            bspGenerator.ApplyDifficultySettings(ActivePreset);

        if (pathProcessor != null)
            pathProcessor.ApplyDifficultySettings(ActivePreset);

        if (enemyPlacer != null)
            enemyPlacer.ApplyDifficultySettings(ActivePreset);

        Debug.Log(
            $"BridgeDifficulty L{ActivePreset.level}: rooms={ActivePreset.divideCount}, " +
            $"enemies={ActivePreset.enemyCount}, lakes={ActivePreset.maxLakeCoverage:P0}, " +
            $"pathLakePatches={ActivePreset.pathAdjacentLakePatches}");
    }
}
