using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// PathProcessor가 만든 BridgeMapData_Path.json 읽기 → lake 배치 → JSON/PNG 저장.
/// 길 생성(PathProcessor) 코드는 건드리지 않음.
/// </summary>
public class BridgeLakePostProcessor : MonoBehaviour
{
    [Header("Input / Output")]
    public string pathJsonRelativePath = "tmpOutput/BridgeMapData_Path.json";
    public bool runAfterPathGeneration = true;
    public bool refreshAsciiRenderer = true;

    [Header("Lake")]
    public bool placeLakes = true;

    [Header("Preview")]
    [Range(1, 32)]
    public int previewPointSize = 8;

    [ContextMenu("Place Lakes On Bridge Path JSON")]
    public void ProcessLakes()
    {
        if (!placeLakes)
        {
            TriggerBridgePathRunners();
            return;
        }

        BridgeDifficultySettings settings = BridgeDifficultySettings.Instance;
        if (settings == null)
        {
            settings = GetComponent<BridgeDifficultySettings>();
        }

        if (settings != null)
        {
            settings.ApplySelectedLevel((int)settings.selectedDifficulty);
        }

        BridgeDifficultyPreset preset = BridgeDifficultySettings.ActivePreset;
        if (preset == null)
        {
            BridgeDifficultySettings fallback = GetComponent<BridgeDifficultySettings>();
            if (fallback != null)
            {
                fallback.ApplySelectedLevel((int)fallback.selectedDifficulty);
                preset = BridgeDifficultySettings.ActivePreset;
            }
        }

        if (preset == null || !preset.placeLakes)
        {
            Debug.LogWarning("BridgeLakePostProcessor: preset 없음 또는 placeLakes=false — lake 스킵.");
            TriggerBridgePathRunners();
            return;
        }

        if (!BridgeMapJsonUtility.TryLoadFromFile(pathJsonRelativePath, out InputMapData pathData, out char[,] grid))
        {
            Debug.LogError($"BridgeLakePostProcessor: JSON 없음 — {pathJsonRelativePath}. PathProcessor 먼저 실행하세요.");
            return;
        }

        int width = grid.GetLength(1);
        int height = grid.GetLength(0);
        int[,] lakeAutotileIndices = new int[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                lakeAutotileIndices[y, x] = -1;
            }
        }

        BridgeLakePlacer.LakePlacementReport lakeReport =
            BridgeLakePlacer.TryPlaceLakes(grid, lakeAutotileIndices, preset);

        if (lakeReport.lakeCellCount == 0)
        {
            Debug.LogWarning(
                "BridgeLakePostProcessor: lake 0칸 배치됨. Console의 BridgeLakePlacer 로그 확인, " +
                "또는 Bridge Difficulty Settings → lakeMinDistanceFromPath 를 0~1로 낮춰 보세요.");
        }
        else
        {
            int centerCount = CountLakeAutotileIndex(lakeAutotileIndices, 8);
            Debug.Log(
                $"BridgeLakePostProcessor: lake {lakeReport.lakeCellCount}칸 (center w_8={centerCount}) → " +
                $"tmpOutput/BridgeMapData_Path.json");
        }

        string folderPath = BridgeMapJsonUtility.GetProjectRelativePath("tmpOutput");
        SavePathJson(pathData, grid, lakeAutotileIndices, folderPath);
        SavePreviewImage(grid, width, height, folderPath);

        BridgeDifficultySettings settingsForExport = BridgeDifficultySettings.Instance ?? GetComponent<BridgeDifficultySettings>();
        if (settingsForExport != null && settingsForExport.exportQaReport && lakeReport.lakeCellCount > 0)
        {
            string qaFolder = BridgeQaExporter.ExportLakePathSnapshot(
                grid,
                lakeAutotileIndices,
                preset,
                BridgeDifficultySettings.ActiveLevel,
                folderPath);
            Debug.Log($"BridgeLakePostProcessor: QA snapshot (lake 포함) → {qaFolder}");
        }

        if (refreshAsciiRenderer)
        {
            AsciiMapTilemapRenderer renderer = FindObjectOfType<AsciiMapTilemapRenderer>(true);
            if (renderer != null)
            {
                renderer.inputRelativePath = pathJsonRelativePath;
                renderer.RenderFromInspectorInput();
                int painted = renderer.PaintLakeLayerFromIndices(pathData, lakeAutotileIndices);
                Debug.Log($"BridgeLakePostProcessor: lake tilemap direct paint → {painted} cells");
            }
        }

        TriggerBridgePathRunners();
    }

    private static void SavePathJson(
        InputMapData pathData,
        char[,] grid,
        int[,] lakeAutotileIndices,
        string folderPath)
    {
        OutputPathData outputData = new OutputPathData
        {
            width = pathData.width,
            height = pathData.height,
            startX = pathData.startX,
            startY = pathData.startY
        };

        List<string> outputRows = new List<string>();
        BridgeMapJsonUtility.WriteTokenGridRowsTopFirst(outputRows, grid, lakeAutotileIndices);
        outputData.mapGrid = outputRows;

        string outputPath = Path.Combine(folderPath, "BridgeMapData_Path.json");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        File.WriteAllText(outputPath, JsonUtility.ToJson(outputData, true));
        Debug.Log($"BridgeLakePostProcessor: lake 반영 JSON 저장 — {outputPath}");
    }

    private void SavePreviewImage(char[,] grid, int width, int height, string folderPath)
    {
        int texWidth = width * previewPointSize;
        int texHeight = height * previewPointSize;
        Texture2D mapTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGB24, false);
        mapTexture.filterMode = FilterMode.Point;

        Color wall = Color.black;
        Color path = new Color(0.8f, 0.8f, 0.8f);
        Color ground = Color.white;
        Color lake = new Color(0.2f, 0.45f, 0.95f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color = ground;
                char tile = grid[y, x];
                if (tile == '#') color = wall;
                else if (tile == 'P' || tile == 'S' || tile == 'E') color = path;
                else if (tile == 'G') color = ground;
                else if (tile == BridgeLakePlacer.LakeBlockChar) color = lake;

                int pixelStartX = x * previewPointSize;
                int pixelStartY = (height - 1 - y) * previewPointSize;
                for (int py = 0; py < previewPointSize; py++)
                {
                    for (int px = 0; px < previewPointSize; px++)
                    {
                        mapTexture.SetPixel(pixelStartX + px, pixelStartY + py, color);
                    }
                }
            }
        }

        mapTexture.Apply();
        string imgPath = Path.Combine(folderPath, "BridgeMapData_Path.png");
        File.WriteAllBytes(imgPath, mapTexture.EncodeToPNG());
        DestroyImmediate(mapTexture);
    }

    private static int CountLakeAutotileIndex(int[,] lakeAutotileIndices, int targetIndex)
    {
        int count = 0;
        int height = lakeAutotileIndices.GetLength(0);
        int width = lakeAutotileIndices.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (lakeAutotileIndices[y, x] == targetIndex)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void TriggerBridgePathRunners()
    {
        AutoPathRunner[] runners = Object.FindObjectsOfType<AutoPathRunner>(true);
        foreach (AutoPathRunner runner in runners)
        {
            if (runner.useBridgePathJson)
            {
                runner.StartRun();
            }
        }
    }
}
