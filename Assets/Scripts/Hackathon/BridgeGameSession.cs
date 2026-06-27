using UnityEngine;

[DefaultExecutionOrder(-300)]
public class BridgeGameSession : MonoBehaviour
{
    public static BridgeGameSession Instance { get; private set; }

    [SerializeField] private BSP_Generator bspGenerator;
    [SerializeField] private bool waitForLevelSelect = true;
    [SerializeField] private bool useFixedGenerationSeed = true;
    [SerializeField] private int generationSeed = 20260628;

    public bool IsStarted { get; private set; }
    public int SelectedLevel { get; private set; } = 1;
    public bool UseFixedGenerationSeed => useFixedGenerationSeed;
    public int GenerationSeed => generationSeed;

    private void Awake()
    {
        Instance = this;
        if (bspGenerator == null)
            bspGenerator = FindObjectOfType<BSP_Generator>();

        EnsureCameraFollow();
        BridgeDifficultyResultsTracker.LoadCachedResults();

        if (GetComponent<BridgeLevelSelectUI>() == null)
            gameObject.AddComponent<BridgeLevelSelectUI>();

        if (GetComponent<BridgeMapOverviewUI>() == null)
            gameObject.AddComponent<BridgeMapOverviewUI>();

        if (GetComponent<BridgeDifficultyComparisonUI>() == null)
            gameObject.AddComponent<BridgeDifficultyComparisonUI>();
    }

    private void Start()
    {
        if (!waitForLevelSelect)
            StartSession(SelectedLevel);
    }

    public void StartSession(int level)
    {
        if (IsStarted)
            return;

        SelectedLevel = Mathf.Clamp(level, 1, 3);
        IsStarted = true;

        if (BridgeDifficultyController.Instance != null)
            BridgeDifficultyController.Instance.ApplyDifficulty(SelectedLevel);
        else if (bspGenerator != null)
            bspGenerator.GetComponent<BridgeDifficultyController>()?.ApplyDifficulty(SelectedLevel);

        if (bspGenerator != null)
            bspGenerator.BeginGeneration();

        BridgeDifficultyResultsTracker.Export("HackathonAI/path_run_reports");
        Debug.Log($"BridgeGameSession started at difficulty L{SelectedLevel}");
    }

    private static void EnsureCameraFollow()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        if (mainCamera.GetComponent<BridgeCameraFollow>() == null)
            mainCamera.gameObject.AddComponent<BridgeCameraFollow>();
    }
}
