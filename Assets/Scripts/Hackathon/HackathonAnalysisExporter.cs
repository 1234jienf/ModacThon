using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class HackathonMapExportInfo
{
    public string map_id;
    public string ascii_path;
    public string structure_ascii_path;
    public string visual_ascii_path;
    public string structure_json_path;
    public string structure_preview_path;
    public string visual_preview_path;
    public string screenshot_path;
    public string tiles_directory;
}

[Serializable]
public class HackathonAnalysisRequest
{
    public string user_prompt;
    public string created_at;
    public string blended_tiles_directory;
    public HackathonMapExportInfo map_a;
    public HackathonMapExportInfo map_b;
}

public class HackathonAnalysisExporter : MonoBehaviour
{
    [Header("Input Maps")]
    public TilemapDataProvider mapAProvider;
    public TilemapDataProvider mapBProvider;
    public string mapAId = "map_a";
    public string mapBId = "map_b";

    [Header("Analysis Prompt")]
    [TextArea(3, 8)]
    public string userPrompt = "Analyze two maps and produce structure JSON, visual JSON, and transition_sequence JSON.";

    [Header("Export")]
    public bool exportOnStart = true;
    public string outputRoot = "HackathonAI/runs";
    public int screenshotWidth = 1024;
    public Color screenshotBackground = new Color(0.18f, 0.18f, 0.18f, 1f);
    public bool exportTileImages = true;
    public int tileImageSize = 64;

    [Header("Python API Runner")]
    public bool runPythonAfterExport = true;
    public string pythonExecutable = "python3";
    public string analyzerScriptPath = "HackathonAI/tools/analyze_maps.py";

    [Header("Latest Output")]
    public string latestRunDirectory;
    public string latestRequestJsonPath;

    private void Start()
    {
        if (exportOnStart)
        {
            ExportAndMaybeAnalyze();
        }
    }

    [ContextMenu("Export And Analyze")]
    public void ExportAndMaybeAnalyze()
    {
        if (mapAProvider == null || mapBProvider == null)
        {
            UnityEngine.Debug.LogError("mapAProvider와 mapBProvider를 연결해 주세요.");
            return;
        }

        latestRunDirectory = CreateRunDirectory();
        HackathonMapExportInfo mapA = ExportMap(mapAId, mapAProvider, latestRunDirectory, exportTileImages);
        HackathonMapExportInfo mapB = ExportMap(mapBId, mapBProvider, latestRunDirectory, exportTileImages);

        string blendedTilesDirectory = null;
        if (exportTileImages)
        {
            blendedTilesDirectory = Path.Combine(latestRunDirectory, "blended_tiles");
            int exportedCount = PathTileBlendExporter.ExportBlendTiles(
                mapAProvider,
                mapBProvider,
                blendedTilesDirectory,
                tileImageSize
            );
            UnityEngine.Debug.Log($"Generated {exportedCount} tile images:\n{blendedTilesDirectory}");
        }

        HackathonAnalysisRequest request = new HackathonAnalysisRequest
        {
            user_prompt = userPrompt,
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            blended_tiles_directory = blendedTilesDirectory != null
                ? ToProjectRelativePath(blendedTilesDirectory)
                : null,
            map_a = mapA,
            map_b = mapB
        };

        latestRequestJsonPath = Path.Combine(latestRunDirectory, "request.json");
        File.WriteAllText(latestRequestJsonPath, JsonUtility.ToJson(request, true), Encoding.UTF8);
        UnityEngine.Debug.Log($"Hackathon analysis request exported:\n{latestRequestJsonPath}");

        if (runPythonAfterExport)
        {
            RunPythonAnalyzer(latestRequestJsonPath);
        }
    }

    private HackathonMapExportInfo ExportMap(string mapId, TilemapDataProvider provider, string runDirectory, bool exportTiles)
    {
        string mapDirectory = Path.Combine(runDirectory, mapId);
        Directory.CreateDirectory(mapDirectory);

        char[,] matrix = TrimEmptyBorder(provider.GetMapMatrix());
        char[,] visualMatrix = TrimEmptyBorder(provider.GetVisualMapMatrix());
        string ascii = MatrixToAscii(matrix);
        string visualAscii = MatrixToAscii(visualMatrix);
        MapProfile profile = MapProfile.Analyze(mapId, matrix);

        string asciiPath = Path.Combine(mapDirectory, "ascii.txt");
        string structureAsciiPath = Path.Combine(mapDirectory, "structure_ascii.txt");
        string visualAsciiPath = Path.Combine(mapDirectory, "visual_ascii.txt");
        string structurePath = Path.Combine(mapDirectory, "structure.json");
        string structurePreviewPath = Path.Combine(mapDirectory, "structure_preview.png");
        string visualPreviewPath = Path.Combine(mapDirectory, "visual_preview.png");
        string screenshotPath = Path.Combine(mapDirectory, "screenshot.png");

        File.WriteAllText(asciiPath, ascii, Encoding.UTF8);
        File.WriteAllText(structureAsciiPath, ascii, Encoding.UTF8);
        File.WriteAllText(visualAsciiPath, visualAscii, Encoding.UTF8);
        File.WriteAllText(structurePath, JsonUtility.ToJson(profile, true), Encoding.UTF8);
        WriteStructurePreview(matrix, structurePreviewPath);
        WriteVisualPreview(visualMatrix, visualPreviewPath);
        CaptureProviderScreenshot(provider, screenshotPath);
        string tilesDirectory = exportTiles
            ? PathTileBlendExporter.ExportSourceTiles(provider, mapDirectory, tileImageSize)
            : null;

        return new HackathonMapExportInfo
        {
            map_id = mapId,
            ascii_path = ToProjectRelativePath(asciiPath),
            structure_ascii_path = ToProjectRelativePath(structureAsciiPath),
            visual_ascii_path = ToProjectRelativePath(visualAsciiPath),
            structure_json_path = ToProjectRelativePath(structurePath),
            structure_preview_path = ToProjectRelativePath(structurePreviewPath),
            visual_preview_path = ToProjectRelativePath(visualPreviewPath),
            screenshot_path = ToProjectRelativePath(screenshotPath),
            tiles_directory = tilesDirectory != null ? ToProjectRelativePath(tilesDirectory) : null
        };
    }

    private string MatrixToAscii(char[,] matrix)
    {
        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);
        StringBuilder sb = new StringBuilder();

        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                sb.Append(matrix[y, x]);
            }

            if (y > 0)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private char[,] TrimEmptyBorder(char[,] matrix)
    {
        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);
        int minX = width;
        int maxX = -1;
        int minY = height;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (matrix[y, x] == ' ')
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return new char[0, 0];
        }

        int trimmedWidth = maxX - minX + 1;
        int trimmedHeight = maxY - minY + 1;
        char[,] trimmed = new char[trimmedHeight, trimmedWidth];

        for (int y = 0; y < trimmedHeight; y++)
        {
            for (int x = 0; x < trimmedWidth; x++)
            {
                trimmed[y, x] = matrix[minY + y, minX + x];
            }
        }

        return trimmed;
    }

    private void WriteStructurePreview(char[,] matrix, string outputPath)
    {
        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);
        const int scale = 6;
        Texture2D texture = new Texture2D(width * scale, height * scale, TextureFormat.RGB24, false);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color = GetStructureColor(matrix[y, x]);
                int pixelY = y * scale;

                for (int py = 0; py < scale; py++)
                {
                    for (int px = 0; px < scale; px++)
                    {
                        texture.SetPixel(x * scale + px, pixelY + py, color);
                    }
                }
            }
        }

        texture.Apply();
        File.WriteAllBytes(outputPath, texture.EncodeToPNG());
        Destroy(texture);
    }

    private Color GetStructureColor(char marker)
    {
        switch (marker)
        {
            case '#':
                return Color.black;
            case '.':
                return Color.white;
            case 'S':
                return Color.green;
            case 'G':
                return Color.red;
            case 'O':
                return new Color(1f, 0.5f, 0f);
            case '?':
                return Color.gray;
            default:
                return new Color(0.15f, 0.15f, 0.15f);
        }
    }

    private void WriteVisualPreview(char[,] matrix, string outputPath)
    {
        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);
        const int scale = 6;
        Texture2D texture = new Texture2D(width * scale, height * scale, TextureFormat.RGB24, false);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color = GetVisualColor(matrix[y, x]);
                int pixelY = y * scale;

                for (int py = 0; py < scale; py++)
                {
                    for (int px = 0; px < scale; px++)
                    {
                        texture.SetPixel(x * scale + px, pixelY + py, color);
                    }
                }
            }
        }

        texture.Apply();
        File.WriteAllBytes(outputPath, texture.EncodeToPNG());
        Destroy(texture);
    }

    private Color GetVisualColor(char marker)
    {
        switch (marker)
        {
            case '#':
                return Color.black;
            case 'g':
                return new Color(0.25f, 0.48f, 0.12f);
            case 'p':
                return new Color(0.35f, 0.22f, 0.14f);
            case 'w':
                return new Color(0.05f, 0.35f, 0.42f);
            case 's':
                return new Color(0.85f, 0.95f, 1f);
            case '.':
                return Color.white;
            case 'S':
                return Color.green;
            case 'G':
                return Color.red;
            case 'O':
                return new Color(1f, 0.5f, 0f);
            case '?':
                return Color.gray;
            default:
                return new Color(0.15f, 0.15f, 0.15f);
        }
    }

    private void CaptureProviderScreenshot(TilemapDataProvider provider, string outputPath)
    {
        Bounds bounds = provider.GetWorldBounds();
        float boundsAspect = Mathf.Max(0.01f, bounds.size.x / Mathf.Max(0.01f, bounds.size.y));
        int captureWidth = Mathf.Max(64, screenshotWidth);
        int captureHeight = Mathf.Max(64, Mathf.RoundToInt(captureWidth / boundsAspect));
        float orthographicSize = Mathf.Max(1f, bounds.size.y * 0.5f * 1.02f);

        GameObject cameraObject = new GameObject($"CaptureCamera_{provider.gameObject.name}");
        Camera captureCamera = cameraObject.AddComponent<Camera>();
        captureCamera.orthographic = true;
        captureCamera.orthographicSize = orthographicSize;
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = screenshotBackground;
        captureCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);

        RenderTexture renderTexture = new RenderTexture(captureWidth, captureHeight, 24);
        Texture2D texture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = captureCamera.targetTexture;
        captureCamera.targetTexture = renderTexture;
        RenderTexture.active = renderTexture;

        captureCamera.Render();
        texture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        texture.Apply();

        File.WriteAllBytes(outputPath, texture.EncodeToPNG());

        captureCamera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        renderTexture.Release();
        Destroy(texture);
        Destroy(renderTexture);
        Destroy(cameraObject);
    }

    private void RunPythonAnalyzer(string requestJsonPath)
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string scriptPath = Path.GetFullPath(Path.Combine(projectRoot, analyzerScriptPath));

        if (!File.Exists(scriptPath))
        {
            UnityEngine.Debug.LogError($"Python analyzer script not found: {scriptPath}");
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = $"\"{scriptPath}\" \"{Path.GetFullPath(requestJsonPath)}\"",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            Process process = Process.Start(startInfo);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                UnityEngine.Debug.Log(stdout);
            }

            if (process.ExitCode != 0)
            {
                UnityEngine.Debug.LogError($"Python analyzer failed ({process.ExitCode}):\n{stderr}");
            }
            else if (!string.IsNullOrWhiteSpace(stderr))
            {
                UnityEngine.Debug.LogWarning(stderr);
            }
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError($"Python analyzer 실행 실패: {exception.Message}");
        }
    }

    private string CreateRunDirectory()
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string runId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string runDirectory = Path.Combine(projectRoot, outputRoot, runId);
        Directory.CreateDirectory(runDirectory);
        return runDirectory;
    }

    private string ToProjectRelativePath(string path)
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(projectRoot, StringComparison.Ordinal))
        {
            return fullPath.Substring(projectRoot.Length + 1);
        }

        return fullPath;
    }
}
