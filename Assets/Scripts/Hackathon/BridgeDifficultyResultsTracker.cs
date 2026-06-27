using System;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class BridgeDifficultyResultsCache
{
    public PathRunReport level1;
    public PathRunReport level2;
    public PathRunReport level3;
}

public static class BridgeDifficultyResultsTracker
{
    private static readonly PathRunReport[] ResultsByLevel = new PathRunReport[3];
    private const string CacheRelativePath = "HackathonAI/path_run_reports/difficulty_results_cache.json";

    public static void LoadCachedResults()
    {
        string path = BridgeMapJsonUtility.GetProjectRelativePath(CacheRelativePath);
        if (!File.Exists(path))
            return;

        BridgeDifficultyResultsCache cache = JsonUtility.FromJson<BridgeDifficultyResultsCache>(File.ReadAllText(path));
        if (cache == null)
            return;

        ResultsByLevel[0] = cache.level1;
        ResultsByLevel[1] = cache.level2;
        ResultsByLevel[2] = cache.level3;
        BridgeDifficultyComparisonUI.RefreshTable();
    }

    public static void Record(PathRunReport report)
    {
        if (report == null || !report.success)
            return;

        int index = report.difficulty_level - 1;
        if (index < 0 || index >= ResultsByLevel.Length)
            return;

        ResultsByLevel[index] = report;
        SaveCachedResults();
        BridgeDifficultyComparisonUI.RefreshTable();
    }

    private static void SaveCachedResults()
    {
        string path = BridgeMapJsonUtility.GetProjectRelativePath(CacheRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        BridgeDifficultyResultsCache cache = new BridgeDifficultyResultsCache
        {
            level1 = ResultsByLevel[0],
            level2 = ResultsByLevel[1],
            level3 = ResultsByLevel[2]
        };

        File.WriteAllText(path, JsonUtility.ToJson(cache, true), Encoding.UTF8);
    }

    public static string BuildMarkdownTable()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# Bridge Difficulty Comparison");
        sb.AppendLine();
        sb.AppendLine("## Map / Spawn Presets");
        sb.AppendLine();
        sb.AppendLine("| Setting | L1 Easy | L2 Normal | L3 Hard |");
        sb.AppendLine("|---|---:|---:|---:|");
        AppendPresetRow(sb, "BSP divideCount", p => p.divideCount);
        AppendPresetRow(sb, "Min node size to divide", p => p.minNodeSizeToDivide);
        AppendPresetRow(sb, "Enemy count", p => p.enemyCount);
        AppendPresetRow(sb, "Max lake coverage", p => $"{p.maxLakeCoverage:P0}");
        AppendPresetRow(sb, "Lake min dist from path", p => p.lakeMinDistanceFromPath);
        AppendPresetRow(sb, "Path-adjacent lake patches", p => p.pathAdjacentLakePatches);
        AppendPresetRow(sb, "Enemy roam radius", p => $"{p.enemyRoamRadius:0.0}");
        AppendPresetRow(sb, "Map B difficulty weight", p => $"{p.mapBDifficultyWeight:0.00}");

        sb.AppendLine();
        sb.AppendLine("## Path Run Results (S → G)");
        sb.AppendLine();
        sb.AppendLine("| Metric | L1 Easy | L2 Normal | L3 Hard |");
        sb.AppendLine("|---|---:|---:|---:|");
        AppendResultRow(sb, "Baseline time (s)", r => FormatFloat(r?.baseline_estimated_seconds));
        AppendResultRow(sb, "Actual time (s)", r => FormatFloat(r?.elapsed_seconds));
        AppendResultRow(sb, "Time delta (s)", r => FormatFloat(r?.elapsed_delta_seconds));
        AppendResultRow(sb, "Baseline route length", r => FormatFloat(r?.baseline_world_distance));
        AppendResultRow(sb, "Actual route length", r => FormatFloat(r?.world_distance));
        AppendResultRow(sb, "Route delta", r => FormatFloat(r?.distance_delta));
        AppendResultRow(sb, "Baseline tile steps", r => FormatInt(r?.baseline_total_tile_steps));
        AppendResultRow(sb, "Actual tile steps", r => FormatInt(r?.total_tile_steps));
        AppendResultRow(sb, "Grass detours", r => FormatInt(r?.ground_tile_steps));
        AppendResultRow(sb, "Replans", r => FormatInt(r?.replan_count));
        AppendResultRow(sb, "Enemies on map", r => FormatInt(r?.enemy_count));

        sb.AppendLine();
        sb.AppendLine("- Red line = baseline path-only A*");
        sb.AppendLine("- Cyan line = dynamic enemy-avoidance A*");
        sb.AppendLine("- Cells marked `-` were not run yet in this session.");

        return sb.ToString();
    }

    public static string BuildUiTableText()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Difficulty Comparison</b>");
        sb.AppendLine();
        sb.AppendLine("<size=16><b>Presets</b></size>");
        sb.AppendLine("Setting          L1      L2      L3");
        sb.AppendLine("Rooms(BSP)       " + RowPreset(p => p.divideCount));
        sb.AppendLine("Enemies          " + RowPreset(p => p.enemyCount));
        sb.AppendLine("Lake coverage    " + RowPreset(p => $"{p.maxLakeCoverage:P0}"));
        sb.AppendLine("Path lake patch  " + RowPreset(p => p.pathAdjacentLakePatches));
        sb.AppendLine();
        sb.AppendLine("<size=16><b>Path Run Results</b></size>");
        sb.AppendLine("Metric           L1      L2      L3");
        sb.AppendLine("Baseline time(s) " + RowResult(r => FormatFloat(r?.baseline_estimated_seconds)));
        sb.AppendLine("Actual time(s)   " + RowResult(r => FormatFloat(r?.elapsed_seconds)));
        sb.AppendLine("Time delta(s)    " + RowResult(r => FormatFloat(r?.elapsed_delta_seconds)));
        sb.AppendLine("Baseline route   " + RowResult(r => FormatFloat(r?.baseline_world_distance)));
        sb.AppendLine("Actual route     " + RowResult(r => FormatFloat(r?.world_distance)));
        sb.AppendLine("Route delta      " + RowResult(r => FormatFloat(r?.distance_delta)));
        sb.AppendLine("Grass detours    " + RowResult(r => FormatInt(r?.ground_tile_steps)));
        sb.AppendLine("Replans          " + RowResult(r => FormatInt(r?.replan_count)));
        return sb.ToString();
    }

    public static void Export(string outputRoot)
    {
        string directory = Path.Combine(Directory.GetCurrentDirectory(), outputRoot);
        Directory.CreateDirectory(directory);

        string markdownPath = Path.Combine(directory, "bridge_difficulty_comparison.md");
        File.WriteAllText(markdownPath, BuildMarkdownTable(), Encoding.UTF8);

        string csvPath = Path.Combine(directory, "bridge_difficulty_comparison.csv");
        File.WriteAllText(csvPath, BuildCsv(), Encoding.UTF8);

        Debug.Log($"Bridge difficulty comparison exported:\n{markdownPath}\n{csvPath}");
    }

    private static string BuildCsv()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("section,metric,l1_easy,l2_normal,l3_hard");
        AppendCsvPresetRow(sb, "preset", "divideCount", p => p.divideCount.ToString());
        AppendCsvPresetRow(sb, "preset", "enemyCount", p => p.enemyCount.ToString());
        AppendCsvPresetRow(sb, "preset", "maxLakeCoverage", p => p.maxLakeCoverage.ToString("0.###"));
        AppendCsvPresetRow(sb, "preset", "pathAdjacentLakePatches", p => p.pathAdjacentLakePatches.ToString());
        AppendCsvResultRow(sb, "result", "baseline_estimated_seconds", r => FormatFloat(r?.baseline_estimated_seconds));
        AppendCsvResultRow(sb, "result", "elapsed_seconds", r => FormatFloat(r?.elapsed_seconds));
        AppendCsvResultRow(sb, "result", "elapsed_delta_seconds", r => FormatFloat(r?.elapsed_delta_seconds));
        AppendCsvResultRow(sb, "result", "baseline_world_distance", r => FormatFloat(r?.baseline_world_distance));
        AppendCsvResultRow(sb, "result", "world_distance", r => FormatFloat(r?.world_distance));
        AppendCsvResultRow(sb, "result", "distance_delta", r => FormatFloat(r?.distance_delta));
        AppendCsvResultRow(sb, "result", "ground_tile_steps", r => FormatInt(r?.ground_tile_steps));
        AppendCsvResultRow(sb, "result", "replan_count", r => FormatInt(r?.replan_count));
        return sb.ToString();
    }

    private delegate string ResultFormatter(PathRunReport report);

    private static void AppendPresetRow(StringBuilder sb, string label, System.Func<BridgeDifficultyPreset, object> selector)
    {
        BridgeDifficultyPreset l1 = BridgeDifficultyProfile.GetPreset(1);
        BridgeDifficultyPreset l2 = BridgeDifficultyProfile.GetPreset(2);
        BridgeDifficultyPreset l3 = BridgeDifficultyProfile.GetPreset(3);
        sb.AppendLine($"| {label} | {selector(l1)} | {selector(l2)} | {selector(l3)} |");
    }

    private static void AppendResultRow(StringBuilder sb, string label, ResultFormatter formatter)
    {
        sb.AppendLine($"| {label} | {formatter(ResultsByLevel[0])} | {formatter(ResultsByLevel[1])} | {formatter(ResultsByLevel[2])} |");
    }

    private static void AppendCsvPresetRow(StringBuilder sb, string section, string metric, System.Func<BridgeDifficultyPreset, string> selector)
    {
        sb.AppendLine($"{section},{metric},{selector(BridgeDifficultyProfile.GetPreset(1))},{selector(BridgeDifficultyProfile.GetPreset(2))},{selector(BridgeDifficultyProfile.GetPreset(3))}");
    }

    private static void AppendCsvResultRow(StringBuilder sb, string section, string metric, ResultFormatter formatter)
    {
        sb.AppendLine($"{section},{metric},{formatter(ResultsByLevel[0])},{formatter(ResultsByLevel[1])},{formatter(ResultsByLevel[2])}");
    }

    private static string RowPreset(System.Func<BridgeDifficultyPreset, object> selector)
    {
        return Pad(selector(BridgeDifficultyProfile.GetPreset(1))) +
               Pad(selector(BridgeDifficultyProfile.GetPreset(2))) +
               Pad(selector(BridgeDifficultyProfile.GetPreset(3)));
    }

    private static string RowResult(ResultFormatter formatter)
    {
        return Pad(formatter(ResultsByLevel[0])) +
               Pad(formatter(ResultsByLevel[1])) +
               Pad(formatter(ResultsByLevel[2]));
    }

    private static string Pad(object value)
    {
        return (value?.ToString() ?? "-").PadLeft(8);
    }

    private static string FormatFloat(float? value)
    {
        return value.HasValue ? value.Value.ToString("0.00") : "-";
    }

    private static string FormatInt(int? value)
    {
        return value.HasValue ? value.Value.ToString() : "-";
    }
}
