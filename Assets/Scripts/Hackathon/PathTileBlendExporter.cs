using System;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PathTileBlendExporter : MonoBehaviour
{
    [Header("Input Providers")]
    public TilemapDataProvider mapAProvider;
    public TilemapDataProvider mapBProvider;

    [Header("Output")]
    public bool exportOnStart = false;
    public string outputRoot = "HackathonAI/runs/manual_tile_export";
    public int tileSize = 64;

    private void Start()
    {
        if (exportOnStart)
        {
            ExportPathTiles();
        }
    }

    [ContextMenu("Export Path Blend Tiles")]
    public void ExportPathTiles()
    {
        if (mapAProvider == null || mapBProvider == null)
        {
            Debug.LogError("mapAProvider와 mapBProvider를 연결해 주세요.");
            return;
        }

        string outputDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            outputRoot,
            DateTime.Now.ToString("yyyyMMdd_HHmmss"),
            "blended_tiles"
        );
        int exportedCount = ExportBlendTiles(mapAProvider, mapBProvider, outputDirectory, tileSize);
        Debug.Log($"Generated {exportedCount} tile images:\n{outputDirectory}");
    }

    public static string ExportSourceTiles(TilemapDataProvider provider, string mapDirectory, int tileSize)
    {
        string tilesDirectory = Path.Combine(mapDirectory, "tiles");
        int exportedCount = 0;

        exportedCount += ExportSourceCategory("path", provider.pathTiles, tilesDirectory, tileSize);
        exportedCount += ExportSourceCategory("ground", provider.groundTiles, tilesDirectory, tileSize);
        exportedCount += ExportSourceCategory("lake", provider.lakeTiles, tilesDirectory, tileSize);

        if (exportedCount == 0)
        {
            Debug.LogWarning(
                $"{provider.gameObject.name}: path/ground/lake 타일이 비어 있어 source tile PNG를 만들지 못했습니다. " +
                "TilemapDataProvider의 Path/Ground[0~6], Lake[0~8]을 채워 주세요."
            );
            return null;
        }

        return tilesDirectory;
    }

    public static int ExportBlendTiles(
        TilemapDataProvider mapAProvider,
        TilemapDataProvider mapBProvider,
        string outputDirectory,
        int tileSize)
    {
        Directory.CreateDirectory(outputDirectory);

        int exportedCount = 0;
        exportedCount += ExportCategory("path", mapAProvider.pathTiles, mapBProvider.pathTiles, outputDirectory, tileSize);
        exportedCount += ExportCategory("ground", mapAProvider.groundTiles, mapBProvider.groundTiles, outputDirectory, tileSize);
        exportedCount += ExportCategory("lake", mapAProvider.lakeTiles, mapBProvider.lakeTiles, outputDirectory, tileSize);

        if (exportedCount == 0)
        {
            Debug.LogWarning(
                "블렌딩 타일 PNG를 만들지 못했습니다. Field 1 / Field 3의 TilemapDataProvider에 " +
                "Path/Ground/Lake 타일을 연결해 주세요. Lake는 9칸(l,tl,t,tr,r,br,b,bl,c)입니다."
            );
        }

        return exportedCount;
    }

    private static int ExportSourceCategory(string category, TileBase[] tiles, string tilesDirectory, int tileSize)
    {
        string categoryDirectory = Path.Combine(tilesDirectory, category);
        int exportedCount = 0;

        string[] slotNames = TileBlendLayouts.GetSlotNames(category);
        for (int i = 0; i < slotNames.Length; i++)
        {
            Texture2D texture = RenderTile(GetTileAt(tiles, i), tileSize);
            if (texture == null)
            {
                continue;
            }

            Directory.CreateDirectory(categoryDirectory);
            WriteTexture(texture, Path.Combine(categoryDirectory, $"{category}_{i:00}_{slotNames[i]}.png"));
            Destroy(texture);
            exportedCount++;
        }

        return exportedCount;
    }

    private static int ExportCategory(
        string category,
        TileBase[] mapATiles,
        TileBase[] mapBTiles,
        string outputDirectory,
        int tileSize)
    {
        string categoryDirectory = Path.Combine(outputDirectory, category);
        int exportedCount = 0;

        string[] slotNames = TileBlendLayouts.GetSlotNames(category);
        for (int i = 0; i < slotNames.Length; i++)
        {
            Texture2D textureA = RenderTile(GetTileAt(mapATiles, i), tileSize);
            Texture2D textureB = RenderTile(GetTileAt(mapBTiles, i), tileSize);

            if (textureA == null && textureB == null)
            {
                continue;
            }

            Directory.CreateDirectory(categoryDirectory);

            if (textureA != null)
            {
                WriteTexture(textureA, Path.Combine(categoryDirectory, $"source_a_{i:00}_{slotNames[i]}.png"));
                exportedCount++;
            }

            if (textureB != null)
            {
                WriteTexture(textureB, Path.Combine(categoryDirectory, $"source_b_{i:00}_{slotNames[i]}.png"));
                exportedCount++;
            }

            exportedCount += ExportBlend(category, textureA, textureB, 70, 30, categoryDirectory, i, slotNames[i], tileSize);
            exportedCount += ExportBlend(category, textureA, textureB, 50, 50, categoryDirectory, i, slotNames[i], tileSize);
            exportedCount += ExportBlend(category, textureA, textureB, 30, 70, categoryDirectory, i, slotNames[i], tileSize);

            if (textureA != null)
            {
                Destroy(textureA);
            }

            if (textureB != null)
            {
                Destroy(textureB);
            }
        }

        return exportedCount;
    }

    private static TileBase GetTileAt(TileBase[] tiles, int index)
    {
        return tiles != null && index < tiles.Length ? tiles[index] : null;
    }

    private static int ExportBlend(
        string category,
        Texture2D textureA,
        Texture2D textureB,
        int aPercent,
        int bPercent,
        string outputDirectory,
        int index,
        string slotName,
        int tileSize)
    {
        Texture2D blended = BlendTextures(textureA, textureB, aPercent / 100f, tileSize);
        if (blended == null)
        {
            return 0;
        }

        string fileName = $"{category}_{index:00}_{slotName}_a_{aPercent}_b_{bPercent}.png";
        WriteTexture(blended, Path.Combine(outputDirectory, fileName));
        Destroy(blended);
        return 1;
    }

    private static Texture2D RenderTile(TileBase tile, int tileSize)
    {
        Sprite sprite = GetTileSprite(tile);
        if (sprite == null || sprite.texture == null)
        {
            return null;
        }

        Rect rect = sprite.textureRect;
        int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Texture2D texture = new Texture2D(tileSize, tileSize, TextureFormat.RGBA32, false);
        Texture2D rawTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.clear);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, width, height, 0);

            Rect drawRect = new Rect(0, 0, width, height);
            Rect sourceRect = new Rect(
                rect.x / sprite.texture.width,
                rect.y / sprite.texture.height,
                rect.width / sprite.texture.width,
                rect.height / sprite.texture.height
            );

            Graphics.DrawTexture(drawRect, sprite.texture, sourceRect, 0, 0, 0, 0);
            GL.PopMatrix();

            rawTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            rawTexture.Apply();

            Color[] scaledPixels = ScalePixels(rawTexture, tileSize, tileSize);
            texture.SetPixels(scaledPixels);
            texture.Apply();
            return texture;
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
            Destroy(rawTexture);
        }
    }

    private static Sprite GetTileSprite(TileBase tile)
    {
        return TileSpriteUtility.GetSprite(tile);
    }

    private static Color[] ScalePixels(Texture2D source, int width, int height)
    {
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sourceX = Mathf.Clamp(Mathf.FloorToInt(x / (float)width * source.width), 0, source.width - 1);
                int sourceY = Mathf.Clamp(Mathf.FloorToInt(y / (float)height * source.height), 0, source.height - 1);
                pixels[y * width + x] = source.GetPixel(sourceX, sourceY);
            }
        }

        return pixels;
    }

    private static Texture2D BlendTextures(Texture2D textureA, Texture2D textureB, float mapARatio, int tileSize)
    {
        if (textureA == null && textureB == null)
        {
            return null;
        }

        if (textureA == null)
        {
            return CopyTexture(textureB);
        }

        if (textureB == null)
        {
            return CopyTexture(textureA);
        }

        Texture2D output = new Texture2D(tileSize, tileSize, TextureFormat.RGBA32, false);
        float mapBRatio = 1f - mapARatio;

        for (int y = 0; y < tileSize; y++)
        {
            for (int x = 0; x < tileSize; x++)
            {
                Color a = textureA.GetPixel(x, y);
                Color b = textureB.GetPixel(x, y);
                Color blended = a * mapARatio + b * mapBRatio;
                blended.a = Mathf.Max(a.a, b.a);
                output.SetPixel(x, y, blended);
            }
        }

        output.Apply();
        return output;
    }

    private static Texture2D CopyTexture(Texture2D source)
    {
        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.SetPixels(source.GetPixels());
        copy.Apply();
        return copy;
    }

    private static void WriteTexture(Texture2D texture, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllBytes(outputPath, texture.EncodeToPNG());
    }
}
