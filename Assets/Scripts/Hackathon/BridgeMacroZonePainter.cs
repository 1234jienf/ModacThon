using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Bridge structure grid(P/G/#) → visual ASCII(g/p/s) with left→right MapA→MapB theme bands.
/// </summary>
public static class BridgeMacroZonePainter
{
    public static char[,] BuildVisualGrid(char[,] structureGrid, BridgeDifficultyPreset preset)
    {
        int height = structureGrid.GetLength(0);
        int width = structureGrid.GetLength(1);
        char[,] visual = new char[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                visual[y, x] = MapCell(structureGrid[y, x], x, width, preset);
            }
        }

        return visual;
    }

    public static string BuildVisualAscii(char[,] visualGrid)
    {
        int height = visualGrid.GetLength(0);
        int width = visualGrid.GetLength(1);
        List<string> rows = new List<string>();
        BridgeMapJsonUtility.WriteGridRowsTopFirst(rows, visualGrid);
        return string.Join("\n", rows);
    }

    public static float GetThemeProgress(int x, int width, BridgeDifficultyPreset preset)
    {
        if (width <= 1)
        {
            return preset.mapBThemeAtRight;
        }

        float normalizedX = x / (float)(width - 1);
        int bands = Mathf.Max(1, preset.transitionBandCount);
        int bandIndex = Mathf.Clamp(Mathf.FloorToInt(normalizedX * bands), 0, bands - 1);
        float bandCenter = (bandIndex + 0.5f) / bands;
        return bandCenter * preset.mapBThemeAtRight;
    }

    private static char MapCell(char structureCell, int x, int width, BridgeDifficultyPreset preset)
    {
        switch (structureCell)
        {
            case '#':
                return '#';
            case 'S':
                return 'S';
            case 'E':
                return 'G';
            case 'P':
            case 'p':
                return 'p';
            case 'G':
            case 'g':
            case '.':
                return PickGroundTheme(x, width, preset);
            default:
                return PickGroundTheme(x, width, preset);
        }
    }

    private static char PickGroundTheme(int x, int width, BridgeDifficultyPreset preset)
    {
        float theme = GetThemeProgress(x, width, preset);
        if (theme < 0.34f)
        {
            return 'g';
        }

        if (theme < 0.67f)
        {
            return 'd';
        }

        return 's';
    }
}
