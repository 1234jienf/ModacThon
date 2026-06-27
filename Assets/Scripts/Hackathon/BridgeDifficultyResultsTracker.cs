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
    public const string ComparisonCsvRelativePath = "HackathonAI/path_run_reports/bridge_difficulty_comparison.csv";

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

        string htmlPath = Path.Combine(directory, "bridge_difficulty_comparison.html");
        File.WriteAllText(htmlPath, BuildHtmlTable(), Encoding.UTF8);

        Debug.Log($"Bridge difficulty comparison exported:\n{markdownPath}\n{csvPath}\n{htmlPath}");
    }

    public static string BuildUiTableTextFromExportedCsv()
    {
        string csvPath = BridgeMapJsonUtility.GetProjectRelativePath(ComparisonCsvRelativePath);
        if (!File.Exists(csvPath))
            return BuildUiTableText();

        return BuildUiTableTextFromCsv(File.ReadAllText(csvPath, Encoding.UTF8));
    }

    public static string BuildUiTableTextFromCsv(string csvText)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Difficulty Comparison</b>  <size=14><color=#aaaaaa>(from CSV)</color></size>");
        sb.AppendLine();

        string currentSection = string.Empty;
        foreach (string rawLine in csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (rawLine.StartsWith("section,", StringComparison.Ordinal))
                continue;

            string[] parts = rawLine.Split(',');
            if (parts.Length < 5)
                continue;

            string section = parts[0];
            string metric = parts[1];
            string l1 = DisplayCsvCell(parts[2]);
            string l2 = DisplayCsvCell(parts[3]);
            string l3 = DisplayCsvCell(parts[4]);

            if (section != currentSection)
            {
                currentSection = section;
                sb.AppendLine();
                sb.AppendLine(section == "preset"
                    ? "<size=16><b>Presets</b></size>"
                    : "<size=16><b>Path Run Results</b></size>");
                sb.AppendLine("Metric           L1      L2      L3");
            }

            sb.AppendLine($"{FormatMetricLabel(metric).PadRight(17)}{l1.PadLeft(8)}{l2.PadLeft(8)}{l3.PadLeft(8)}");
        }

        return sb.ToString();
    }

    public static string BuildHtmlTable()
    {
        string csv = BuildCsv();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ko\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<title>Bridge Difficulty Comparison</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:32px;background:#111;color:#eee;}");
        sb.AppendLine("h1{font-size:24px;margin-bottom:8px;} h2{font-size:18px;margin-top:28px;color:#8fd3ff;}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;max-width:920px;margin-top:12px;}");
        sb.AppendLine("th,td{border:1px solid #333;padding:10px 12px;text-align:right;}");
        sb.AppendLine("th:first-child,td:first-child{text-align:left;}");
        sb.AppendLine("th{background:#1d1d1d;} tr:nth-child(even){background:#171717;}");
        sb.AppendLine(".note{margin-top:20px;color:#aaa;font-size:14px;line-height:1.6;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<h1>Bridge Difficulty Comparison</h1>");

        string currentSection = string.Empty;
        bool headerWritten = false;
        foreach (string rawLine in csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (rawLine.StartsWith("section,", StringComparison.Ordinal))
                continue;

            string[] parts = rawLine.Split(',');
            if (parts.Length < 5)
                continue;

            string section = parts[0];
            if (section != currentSection)
            {
                if (headerWritten)
                    sb.AppendLine("</tbody></table>");

                currentSection = section;
                headerWritten = true;
                sb.AppendLine(section == "preset"
                    ? "<h2>Map / Spawn Presets</h2>"
                    : "<h2>Path Run Results (S → G)</h2>");
                sb.AppendLine("<table><thead><tr><th>Metric</th><th>L1 Easy</th><th>L2 Normal</th><th>L3 Hard</th></tr></thead><tbody>");
            }

            sb.AppendLine(
                $"<tr><td>{HtmlEscape(FormatMetricLabel(parts[1]))}</td>" +
                $"<td>{HtmlEscape(DisplayCsvCell(parts[2]))}</td>" +
                $"<td>{HtmlEscape(DisplayCsvCell(parts[3]))}</td>" +
                $"<td>{HtmlEscape(DisplayCsvCell(parts[4]))}</td></tr>");
        }

        if (headerWritten)
            sb.AppendLine("</tbody></table>");

        sb.AppendLine("<div class=\"note\">");
        sb.AppendLine("<div>Red line = baseline path-only A*</div>");
        sb.AppendLine("<div>Cyan line = dynamic enemy-avoidance A*</div>");
        sb.AppendLine("<div>Generated from bridge_difficulty_comparison.csv</div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string FormatMetricLabel(string metric)
    {
        switch (metric)
        {
            case "divideCount": return "Rooms (BSP)";
            case "enemyCount": return "Enemies";
            case "maxLakeCoverage": return "Lake coverage";
            case "pathAdjacentLakePatches": return "Path lake patches";
            case "baseline_estimated_seconds": return "Baseline time (s)";
            case "elapsed_seconds": return "Actual time (s)";
            case "elapsed_delta_seconds": return "Time delta (s)";
            case "baseline_world_distance": return "Baseline route";
            case "world_distance": return "Actual route";
            case "distance_delta": return "Route delta";
            case "ground_tile_steps": return "Grass detours";
            case "replan_count": return "Replans";
            default: return metric;
        }
    }

    private static string DisplayCsvCell(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-")
            return "-";

        if (value == "0" || value == "0.00")
            return "0.00";

        return value;
    }

    private static string HtmlEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
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
