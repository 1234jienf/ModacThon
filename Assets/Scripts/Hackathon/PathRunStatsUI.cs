using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PathRunStatsUI : MonoBehaviour
{
    [SerializeField] private AutoPathRunner pathRunner;
    [SerializeField] private TextMeshProUGUI statsText;

    private void Awake()
    {
        if (pathRunner == null)
            pathRunner = FindObjectOfType<AutoPathRunner>();

        EnsureUi();
    }

    private void Update()
    {
        if (statsText == null || pathRunner == null)
            return;

        PathRunLiveStats live = pathRunner.liveStats;
        PathRunReport report = pathRunner.latestReport;

        if (live.isRunning)
        {
            statsText.text =
                $"Difficulty L{live.difficultyLevel}\n" +
                $"S → G  time: {live.elapsedSeconds:0.00}s\n" +
                $"Actual route: {live.actualRouteDistance:0.0}\n" +
                $"Baseline A* route: {live.baselineRouteDistance:0.0}\n" +
                $"Baseline est time: {live.baselineEstimatedSeconds:0.00}s\n" +
                $"Enemies: {live.enemyCount}  Replans: {live.replanCount}\n" +
                $"<color=#ff4444>■</color> baseline   <color=#33ccff>■</color> dynamic";
            return;
        }

        if (report != null && report.success)
        {
            statsText.text =
                $"Difficulty L{report.difficulty_level}  DONE\n" +
                $"S → G  time: {report.elapsed_seconds:0.00}s  (+{report.elapsed_delta_seconds:0.00}s)\n" +
                $"Actual route: {report.world_distance:0.0}  (+{report.distance_delta:0.0})\n" +
                $"Baseline A* route: {report.baseline_world_distance:0.0}\n" +
                $"Baseline est time: {report.baseline_estimated_seconds:0.00}s\n" +
                $"Grass detours: {report.ground_tile_steps}  Replans: {report.replan_count}\n" +
                $"Report: {report.report_csv_path}";
            return;
        }

        if (BridgeGameSession.Instance != null && !BridgeGameSession.Instance.IsStarted)
        {
            statsText.text = "Select difficulty to start";
            return;
        }

        int level = BridgeDifficultyController.Instance != null ? BridgeDifficultyController.Instance.DifficultyLevel : 0;
        statsText.text = $"Difficulty L{level}\nWaiting for path run...";
    }

    private void EnsureUi()
    {
        if (statsText != null)
            return;

        Canvas canvas = BridgeUiFactory.GetOrCreateCanvas("PathRunStatsCanvas", 8000);

        GameObject panel = new GameObject("PathRunStatsPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(12f, -12f);
        panelRect.sizeDelta = new Vector2(420f, 220f);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("StatsText");
        textObject.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 10f);
        textRect.offsetMax = new Vector2(-10f, -10f);

        statsText = textObject.AddComponent<TextMeshProUGUI>();
        statsText.fontSize = 18f;
        statsText.color = Color.white;
        statsText.alignment = TextAlignmentOptions.TopLeft;
        statsText.richText = true;
    }
}
