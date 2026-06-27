using System;
using System.IO;
using UnityEngine;

public static class BridgeQaExporter
{
    [Serializable]
    public class BridgeQaManifest
    {
        public string created_at;
        public int difficulty_level;
        public string difficulty_id;
        public string difficulty_name;
        public int bridge_width;
        public int bridge_height;
        public int path_tile_count;
        public int grass_tile_count;
        public int dirt_tile_count;
        public int snow_tile_count;
        public string structure_json_path;
        public string structure_png_path;
        public string visual_ascii_path;
        public string visual_png_path;
        public string preset_json_path;
    }

    public static BridgeQaManifest ExportRun(
        char[,] structureGrid,
        char[,] visualGrid,
        BridgeDifficultyPreset preset,
        int difficultyLevel,
        string sourceFolder)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string outputDirectory = Path.Combine(
            BridgeMapJsonUtility.GetProjectRelativePath("HackathonAI/bridge_qa_runs"),
            $"L{difficultyLevel}_{timestamp}"
        );
        Directory.CreateDirectory(outputDirectory);

        int height = structureGrid.GetLength(0);
        int width = structureGrid.GetLength(1);
        CountVisualTiles(visualGrid, out int pathCount, out int grassCount, out int dirtCount, out int snowCount);

        string structureJsonPath = Path.Combine(outputDirectory, "BridgeMapData_Path.json");
        string structurePngPath = Path.Combine(outputDirectory, "BridgeMapData_Path.png");
        string visualAsciiPath = Path.Combine(outputDirectory, "visual_ascii.txt");
        string visualPngPath = Path.Combine(outputDirectory, "BridgeMapData_Visual.png");
        string presetJsonPath = Path.Combine(outputDirectory, "difficulty_preset.json");

        File.Copy(Path.Combine(sourceFolder, "BridgeMapData_Path.json"), structureJsonPath, true);
        if (File.Exists(Path.Combine(sourceFolder, "BridgeMapData_Path.png")))
        {
            File.Copy(Path.Combine(sourceFolder, "BridgeMapData_Path.png"), structurePngPath, true);
        }

        File.WriteAllText(visualAsciiPath, BridgeMacroZonePainter.BuildVisualAscii(visualGrid));
        SaveVisualGridAsImage(visualGrid, visualPngPath, 8);
        File.WriteAllText(presetJsonPath, JsonUtility.ToJson(preset, true));

        BridgeQaManifest manifest = new BridgeQaManifest
        {
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            difficulty_level = difficultyLevel,
            difficulty_id = preset.id,
            difficulty_name = preset.displayName,
            bridge_width = width,
            bridge_height = height,
            path_tile_count = pathCount,
            grass_tile_count = grassCount,
            dirt_tile_count = dirtCount,
            snow_tile_count = snowCount,
            structure_json_path = structureJsonPath,
            structure_png_path = structurePngPath,
            visual_ascii_path = visualAsciiPath,
            visual_png_path = visualPngPath,
            preset_json_path = presetJsonPath
        };

        string manifestPath = Path.Combine(outputDirectory, "bridge_qa_manifest.json");
        File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
        Debug.Log($"Bridge QA exported (L{difficultyLevel}): {outputDirectory}");
        return manifest;
    }

    public static string ExportLakePathSnapshot(
        char[,] grid,
        int[,] lakeAutotileIndices,
        BridgeDifficultyPreset preset,
        int difficultyLevel,
        string sourceFolder)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string outputDirectory = Path.Combine(
            BridgeMapJsonUtility.GetProjectRelativePath("HackathonAI/bridge_qa_runs"),
            $"L{difficultyLevel}_{timestamp}"
        );
        Directory.CreateDirectory(outputDirectory);

        string jsonDest = Path.Combine(outputDirectory, "BridgeMapData_Path.json");
        string pngDest = Path.Combine(outputDirectory, "BridgeMapData_Path.png");
        string presetDest = Path.Combine(outputDirectory, "difficulty_preset.json");
        string manifestDest = Path.Combine(outputDirectory, "bridge_qa_manifest.json");

        File.Copy(Path.Combine(sourceFolder, "BridgeMapData_Path.json"), jsonDest, true);
        if (File.Exists(Path.Combine(sourceFolder, "BridgeMapData_Path.png")))
        {
            File.Copy(Path.Combine(sourceFolder, "BridgeMapData_Path.png"), pngDest, true);
        }

        int lakeCells = CountLakeCells(lakeAutotileIndices);
        File.WriteAllText(presetDest, JsonUtility.ToJson(preset, true));

        BridgeQaManifest manifest = new BridgeQaManifest
        {
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            difficulty_level = difficultyLevel,
            difficulty_id = preset?.id ?? "unknown",
            difficulty_name = preset?.displayName ?? "unknown",
            bridge_width = grid.GetLength(1),
            bridge_height = grid.GetLength(0),
            path_tile_count = lakeCells,
            structure_json_path = jsonDest,
            structure_png_path = pngDest,
            preset_json_path = presetDest
        };
        File.WriteAllText(manifestDest, JsonUtility.ToJson(manifest, true));

        Debug.Log($"Bridge QA lake snapshot (L{difficultyLevel}, {lakeCells} lake tiles): {outputDirectory}");
        return outputDirectory;
    }

    private static int CountLakeCells(int[,] lakeAutotileIndices)
    {
        if (lakeAutotileIndices == null)
        {
            return 0;
        }

        int count = 0;
        int height = lakeAutotileIndices.GetLength(0);
        int width = lakeAutotileIndices.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (lakeAutotileIndices[y, x] >= 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void CountVisualTiles(
        char[,] visualGrid,
        out int pathCount,
        out int grassCount,
        out int dirtCount,
        out int snowCount)
    {
        pathCount = 0;
        grassCount = 0;
        dirtCount = 0;
        snowCount = 0;
        int height = visualGrid.GetLength(0);
        int width = visualGrid.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                switch (visualGrid[y, x])
                {
                    case 'p':
                    case 'P':
                        pathCount++;
                        break;
                    case 'g':
                        grassCount++;
                        break;
                    case 'd':
                        dirtCount++;
                        break;
                    case 's':
                        snowCount++;
                        break;
                }
            }
        }
    }

    public static void SaveVisualGridAsImage(char[,] grid, string filePath, int pointSize)
    {
        int width = grid.GetLength(1);
        int height = grid.GetLength(0);
        Texture2D texture = new Texture2D(width * pointSize, height * pointSize, TextureFormat.RGB24, false);
        texture.filterMode = FilterMode.Point;

        Color wall = Color.black;
        Color path = new Color(0.72f, 0.55f, 0.35f);
        Color grass = new Color(0.35f, 0.72f, 0.35f);
        Color dirt = new Color(0.58f, 0.48f, 0.32f);
        Color snow = new Color(0.86f, 0.92f, 0.98f);
        Color start = Color.cyan;
        Color goal = Color.magenta;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color = wall;
                switch (grid[y, x])
                {
                    case '#':
                        color = wall;
                        break;
                    case 'p':
                    case 'P':
                        color = path;
                        break;
                    case 'g':
                        color = grass;
                        break;
                    case 'd':
                        color = dirt;
                        break;
                    case 's':
                        color = snow;
                        break;
                    case 'S':
                        color = start;
                        break;
                    case 'E':
                    case 'G':
                        color = goal;
                        break;
                }

                int pixelStartX = x * pointSize;
                int pixelStartY = (height - 1 - y) * pointSize;
                for (int py = 0; py < pointSize; py++)
                {
                    for (int px = 0; px < pointSize; px++)
                    {
                        texture.SetPixel(pixelStartX + px, pixelStartY + py, color);
                    }
                }
            }
        }

        texture.Apply();
        File.WriteAllBytes(filePath, texture.EncodeToPNG());
        UnityEngine.Object.Destroy(texture);
    }
}
