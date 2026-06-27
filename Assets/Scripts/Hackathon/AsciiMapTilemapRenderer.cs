using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class AsciiMapTilemapRenderer : MonoBehaviour
{
    public enum OriginMode
    {
        ZeroBased,
        UseJsonStart
    }

    [Header("Input")]
    public string inputRelativePath = "tmpOutput/BridgeMapData_Path.json";
    public bool loadFromFileOnStart = true;
    public bool preferFileInput = true;

    [TextArea(8, 30)]
    public string inputText =
@"#####
#GGG#
#GPG#
#GGG#
#####";

    [Header("Output")]
    [Tooltip("Layer-specific Tilemap이 비어 있을 때 사용하는 fallback Tilemap입니다.")]
    public Tilemap targetTilemap;
    public Tilemap groundTilemap;
    public Tilemap dirtTilemap;
    public Tilemap lakeTilemap;
    public Tilemap wallTilemap;
    public Tilemap markerTilemap;
    public bool clearBeforeRender = false;
    public bool clearSpawnedPrefabsBeforeRender = true;
    public OriginMode originMode = OriginMode.UseJsonStart;
    public Vector3Int extraOffset = Vector3Int.zero;
    public bool flipRowsVertically = true;

    [Header("Layer Rendering")]
    public bool renderGroundTiles = true;
    public bool fillGroundArea = true;
    public bool renderDirtTiles = true;
    public bool renderLakeTiles = true;
    public bool renderWallTiles = true;

    [Header("Images")]
    public string imageAssetFolder = "Assets/modak_image_test";
    public string blendedTilesFolder = "HackathonAI/runs/20260627_224203/blended_tiles";
    public bool useBlendedTiles = true;
    public bool autoLoadSpritesFromAssetFolder = true;
    [Tooltip("Sprite 크기를 타일 셀 안에서 조금 키우거나 줄입니다. 1은 원본 크기입니다.")]
    public float tileSpriteScale = 1.05f;
    public bool dotMeansGrass = true;

    [Header("Start / Goal")]
    public bool drawFloorUnderMarkers = true;
    public Sprite startSprite;
    public Sprite goalSprite;
    public GameObject startPrefab;
    public GameObject goalPrefab;

    private readonly Dictionary<string, TileBase> _runtimeTiles = new Dictionary<string, TileBase>();
    private readonly Dictionary<string, Sprite> _spriteLookup = new Dictionary<string, Sprite>();
    private readonly HashSet<string> _missingSpriteWarnings = new HashSet<string>();
    private const string SpawnRootName = "ASCII Map Spawned Objects";

    private void Start()
    {
        if (loadFromFileOnStart)
            RenderFromInspectorInput();
    }

    [ContextMenu("Render ASCII Map")]
    public void RenderFromInspectorInput()
    {
        if (!HasAnyOutputTilemap())
        {
            Debug.LogError("Assign at least one output Tilemap.");
            return;
        }

        string input = GetInputText();
        AsciiTilemapJsonData mapData = ParseInputMap(input);
        if (mapData == null || mapData.mapGrid == null || mapData.mapGrid.Length == 0)
        {
            Debug.LogError("ASCII map input has no rows.");
            return;
        }

        Render(mapData);
    }

    public void Render(AsciiTilemapJsonData mapData)
    {
        if (!HasAnyOutputTilemap())
        {
            Debug.LogError("Assign at least one output Tilemap.");
            return;
        }

        LoadSpriteLookup();
        _missingSpriteWarnings.Clear();

        if (clearBeforeRender)
            ClearOutputTilemaps();
        if (clearSpawnedPrefabsBeforeRender)
            ClearSpawnedPrefabs();

        string[][] tokenRows = TokenizeRows(mapData.mapGrid);
        int height = mapData.height > 0 ? mapData.height : tokenRows.Length;
        int width = mapData.width > 0 ? mapData.width : GetMaxTokenRowWidth(tokenRows);
        Vector3Int origin = GetOrigin(mapData);

        if (renderGroundTiles && fillGroundArea)
            FillGroundArea(origin, width, height);

        for (int row = 0; row < height; row++)
        {
            string[] tokens = row < tokenRows.Length ? tokenRows[row] : new string[0];

            for (int x = 0; x < width; x++)
            {
                string token = x < tokens.Length ? tokens[x] : string.Empty;
                int cellY = flipRowsVertically ? height - row - 1 : row;
                Vector3Int cell = origin + new Vector3Int(x, cellY, 0);
                RenderToken(cell, token, tokenRows, row, x, cellY, width, height);
            }
        }

        RefreshOutputTilemaps();
        Debug.Log($"Rendered ASCII map to Tilemap. Size: {width}x{height}");
    }

    private void RenderToken(
        Vector3Int cell,
        string token,
        string[][] tokenRows,
        int row,
        int x,
        int cellY,
        int width,
        int height)
    {
        if (string.IsNullOrEmpty(token) || token == " ")
            return;

        if (token == "S")
        {
            RenderMarker(cell, startSprite, startPrefab);
            return;
        }

        string spriteKey = GetSpriteKey(token, tokenRows, row, x, cellY, width, height);
        if (string.IsNullOrEmpty(spriteKey))
            return;

        SetSpriteTile(cell, spriteKey);
    }

    private void FillGroundArea(Vector3Int origin, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                SetSpriteTile(origin + new Vector3Int(x, y, 0), GetPositionAwareSpriteKey("g_0", x, y, width, height));
            }
        }
    }

    private void RenderMarker(Vector3Int cell, Sprite markerSprite, GameObject markerPrefab)
    {
        if (drawFloorUnderMarkers)
            SetSpriteTile(cell, "g_0");

        if (markerSprite != null)
            GetMarkerTilemap().SetTile(cell, GetOrCreateTile($"marker-{markerSprite.name}", markerSprite));

        if (markerPrefab != null)
        {
            Vector3 worldPosition = GetReferenceTilemap().GetCellCenterWorld(cell);
            Instantiate(markerPrefab, worldPosition, Quaternion.identity, GetSpawnRoot());
        }
    }

    private string GetSpriteKey(string token, string[][] tokenRows, int row, int x, int cellY, int width, int height)
    {
        string baseKey;
        switch (token)
        {
            case "#":
                return "stone_wall";
            case "w":
                return "water";
            case ".":
                baseKey = dotMeansGrass ? "g_0" : string.Empty;
                break;
            case "G":
            case "g":
            case "E":
                baseKey = "g_0";
                break;
            case "P":
                baseKey = GetDustSpriteKey(tokenRows, row, x);
                break;
            case "d":
                baseKey = "d_0";
                break;
            case "d2":
                baseKey = "d2_0";
                break;
            case "s":
                baseKey = "s_0";
                break;
            case "g_0":
            case "g_1":
            case "g_2":
            case "g_3":
            case "g_4":
            case "g_5":
            case "g_6":
            case "d_0":
            case "d_1":
            case "d_2":
            case "d_3":
            case "d_4":
            case "d_5":
            case "d_6":
            case "d2_0":
            case "d2_1":
            case "d2_2":
            case "d2_3":
            case "d2_4":
            case "d2_5":
            case "d2_6":
            case "s_0":
            case "s_1":
            case "s_2":
            case "s_3":
            case "s_4":
            case "s_5":
            case "s_6":
                baseKey = token;
                break;
            default:
                Debug.LogWarning($"Unknown ASCII tile token skipped: {token}");
                return string.Empty;
        }

        return GetPositionAwareSpriteKey(baseKey, x, cellY, width, height);
    }

    private string GetPositionAwareSpriteKey(string baseKey, int x, int y, int width, int height)
    {
        if (!useBlendedTiles || string.IsNullOrEmpty(baseKey))
            return baseKey;

        string blendSet = GetBlendSetForSpriteKey(baseKey);
        string directionFileStem = GetBlendDirectionFileStem(baseKey);
        if (string.IsNullOrEmpty(blendSet) || string.IsNullOrEmpty(directionFileStem))
            return baseKey;

        return $"{blendSet}_{directionFileStem}_{GetBlendRatioName(x, y, width, height)}";
    }

    private static string GetBlendSetForSpriteKey(string spriteKey)
    {
        if (spriteKey.StartsWith("g_", StringComparison.Ordinal) || spriteKey.StartsWith("s_", StringComparison.Ordinal))
            return "ground";
        if (spriteKey.StartsWith("d_", StringComparison.Ordinal) || spriteKey.StartsWith("d2_", StringComparison.Ordinal))
            return "path";

        return string.Empty;
    }

    private static string GetBlendDirectionFileStem(string spriteKey)
    {
        if (string.IsNullOrEmpty(spriteKey) || spriteKey.Length < 3)
            return string.Empty;

        char variant = spriteKey[spriteKey.Length - 1];
        switch (variant)
        {
            case '0':
                return "06_center";
            case '1':
                return "04_bottom";
            case '2':
                return "01_top";
            case '3':
                return "03_bottom_left";
            case '4':
                return "00_top_left";
            case '5':
                return "05_bottom_right";
            case '6':
                return "02_top_right";
            default:
                return string.Empty;
        }
    }

    private static string GetBlendRatioName(int x, int y, int width, int height)
    {
        float xRatio = width <= 1 ? 0.5f : x / (float)(width - 1);
        float yRatio = height <= 1 ? 0.5f : y / (float)(height - 1);
        float mapBWeight = (xRatio + yRatio) * 0.5f;

        if (mapBWeight < 0.38f)
            return "a_70_b_30";
        if (mapBWeight > 0.62f)
            return "a_30_b_70";

        return "a_50_b_50";
    }

    private static string GetDustSpriteKey(string[][] tokenRows, int row, int x)
    {
        bool grassUp = IsGrassTokenAt(tokenRows, row - 1, x);
        bool grassDown = IsGrassTokenAt(tokenRows, row + 1, x);
        bool grassLeft = IsGrassTokenAt(tokenRows, row, x - 1);
        bool grassRight = IsGrassTokenAt(tokenRows, row, x + 1);

        if (grassLeft && grassDown)
            return "d_3";
        if (grassLeft && grassUp)
            return "d_4";
        if (grassRight && grassDown)
            return "d_5";
        if (grassRight && grassUp)
            return "d_6";
        if (grassDown)
            return "d_1";
        if (grassUp)
            return "d_2";

        return "d_0";
    }

    private static bool IsGrassTokenAt(string[][] tokenRows, int row, int x)
    {
        if (tokenRows == null || row < 0 || row >= tokenRows.Length)
            return false;

        string[] tokens = tokenRows[row];
        if (tokens == null || x < 0 || x >= tokens.Length)
            return false;

        return IsGrassToken(tokens[x]);
    }

    private static bool IsGrassToken(string token)
    {
        return token == "G" ||
               token == "g" ||
               token == "." ||
               token == "g_0" ||
               token == "g_1" ||
               token == "g_2" ||
               token == "g_3" ||
               token == "g_4" ||
               token == "g_5" ||
               token == "g_6";
    }

    private void SetSpriteTile(Vector3Int cell, string spriteKey)
    {
        Tilemap outputTilemap = GetTilemapForSpriteKey(spriteKey);
        if (outputTilemap == null)
            return;

        if (!_spriteLookup.TryGetValue(spriteKey, out Sprite sprite) || sprite == null)
        {
            sprite = LoadSpriteForKey(spriteKey);
            if (sprite != null)
            {
                _spriteLookup[spriteKey] = sprite;
            }
            else
            {
                if (_missingSpriteWarnings.Add(spriteKey))
                    Debug.LogWarning($"Sprite not found for ASCII tile token: {spriteKey}");
                return;
            }
        }

        outputTilemap.SetTile(cell, GetOrCreateTile(spriteKey, sprite));
    }

    private Tilemap GetTilemapForSpriteKey(string spriteKey)
    {
        if (spriteKey == "stone_wall")
            return renderWallTiles ? GetLayerTilemap(wallTilemap) : null;
        if (spriteKey == "water")
            return renderLakeTiles ? GetLayerTilemap(lakeTilemap) : null;
        if (spriteKey.StartsWith("path_", StringComparison.Ordinal) ||
            spriteKey.StartsWith("d_", StringComparison.Ordinal) ||
            spriteKey.StartsWith("d2_", StringComparison.Ordinal))
        {
            return renderDirtTiles ? GetLayerTilemap(dirtTilemap) : null;
        }
        if (spriteKey.StartsWith("ground_", StringComparison.Ordinal) ||
            spriteKey.StartsWith("g_", StringComparison.Ordinal) ||
            spriteKey.StartsWith("s_", StringComparison.Ordinal))
        {
            return renderGroundTiles ? GetLayerTilemap(groundTilemap) : null;
        }

        return GetLayerTilemap(targetTilemap);
    }

    private Tilemap GetLayerTilemap(Tilemap preferredTilemap)
    {
        return preferredTilemap != null ? preferredTilemap : targetTilemap;
    }

    private Tilemap GetMarkerTilemap()
    {
        if (markerTilemap != null)
            return markerTilemap;
        if (targetTilemap != null)
            return targetTilemap;

        return GetReferenceTilemap();
    }

    private Tilemap GetReferenceTilemap()
    {
        if (targetTilemap != null)
            return targetTilemap;
        if (groundTilemap != null)
            return groundTilemap;
        if (dirtTilemap != null)
            return dirtTilemap;
        if (lakeTilemap != null)
            return lakeTilemap;
        if (wallTilemap != null)
            return wallTilemap;

        return markerTilemap;
    }

    private bool HasAnyOutputTilemap()
    {
        return targetTilemap != null ||
               groundTilemap != null ||
               dirtTilemap != null ||
               lakeTilemap != null ||
               wallTilemap != null ||
               markerTilemap != null;
    }

    private void ClearOutputTilemaps()
    {
        HashSet<Tilemap> cleared = new HashSet<Tilemap>();

        if (renderGroundTiles)
            ClearTilemapOnce(GetLayerTilemap(groundTilemap), cleared);
        if (renderDirtTiles)
            ClearTilemapOnce(GetLayerTilemap(dirtTilemap), cleared);
        if (renderLakeTiles)
            ClearTilemapOnce(GetLayerTilemap(lakeTilemap), cleared);
        if (renderWallTiles)
            ClearTilemapOnce(GetLayerTilemap(wallTilemap), cleared);

        ClearTilemapOnce(markerTilemap, cleared);
    }

    private static void ClearTilemapOnce(Tilemap tilemap, HashSet<Tilemap> cleared)
    {
        if (tilemap == null || cleared.Contains(tilemap))
            return;

        tilemap.ClearAllTiles();
        cleared.Add(tilemap);
    }

    private void RefreshOutputTilemaps()
    {
        HashSet<Tilemap> refreshed = new HashSet<Tilemap>();
        RefreshTilemapOnce(targetTilemap, refreshed);
        RefreshTilemapOnce(groundTilemap, refreshed);
        RefreshTilemapOnce(dirtTilemap, refreshed);
        RefreshTilemapOnce(lakeTilemap, refreshed);
        RefreshTilemapOnce(wallTilemap, refreshed);
        RefreshTilemapOnce(markerTilemap, refreshed);
    }

    private static void RefreshTilemapOnce(Tilemap tilemap, HashSet<Tilemap> refreshed)
    {
        if (tilemap == null || refreshed.Contains(tilemap))
            return;

        tilemap.RefreshAllTiles();
        refreshed.Add(tilemap);
    }

    private TileBase GetOrCreateTile(string key, Sprite sprite)
    {
        if (_runtimeTiles.TryGetValue(key, out TileBase cachedTile))
            return cachedTile;

        UnityEngine.Tilemaps.Tile tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        tile.name = $"ASCII Tile {key}";
        tile.sprite = sprite;
        tile.color = Color.white;
        tile.transform = GetSpriteScaleMatrix(sprite);
        _runtimeTiles[key] = tile;
        return tile;
    }

    private Matrix4x4 GetSpriteScaleMatrix(Sprite sprite)
    {
        if (sprite == null || tileSpriteScale <= 0f)
            return Matrix4x4.identity;

        return Matrix4x4.Scale(new Vector3(tileSpriteScale, tileSpriteScale, 1f));
    }

    private void LoadSpriteLookup()
    {
        RegisterSprite("stone_wall", "stone_wall.png");
        RegisterSprite("water", "water.png");

        RegisterSprite("g_0", "all_grass.png");
        RegisterSprite("g_1", "up_grass.png");
        RegisterSprite("g_2", "down_grass.png");
        RegisterSprite("g_3", "rightdown_grass.png");
        RegisterSprite("g_4", "rightup_grass.png");
        RegisterSprite("g_5", "leftdown_grass.png");
        RegisterSprite("g_6", "leftup_grass.png");

        RegisterSprite("d_0", "all_dust.png");
        RegisterSprite("d_1", "up_dust.png");
        RegisterSprite("d_2", "down_dust.png");
        RegisterSprite("d_3", "rightdown_dust.png");
        RegisterSprite("d_4", "rightup_dust.png");
        RegisterSprite("d_5", "leftdown_dust.png");
        RegisterSprite("d_6", "leftup_dust.png");

        RegisterSprite("d2_0", "all_dust2.png");
        RegisterSprite("d2_1", "up_dust2.png");
        RegisterSprite("d2_2", "down_dust2.png");
        RegisterSprite("d2_3", "rightdown_dust2.png");
        RegisterSprite("d2_4", "rightup_dust2.png");
        RegisterSprite("d2_5", "leftdown_dust2.png");
        RegisterSprite("d2_6", "leftup_dust2.png");

        RegisterSprite("s_0", "all_snow.png");
        RegisterSprite("s_1", "up_snow.png");
        RegisterSprite("s_2", "down_snow.png");
        RegisterSprite("s_3", "rightdown_snow.png");
        RegisterSprite("s_4", "rightup_snow.png");
        RegisterSprite("s_5", "leftdown_snow.png");
        RegisterSprite("s_6", "leftup_snow.png");
    }

    private Sprite LoadSpriteForKey(string spriteKey)
    {
        if (useBlendedTiles)
        {
            if (spriteKey.StartsWith("ground_", StringComparison.Ordinal))
                return LoadSpriteFromRelativePath($"{blendedTilesFolder.TrimEnd('/', '\\')}/ground/{spriteKey}.png");
            if (spriteKey.StartsWith("path_", StringComparison.Ordinal))
                return LoadSpriteFromRelativePath($"{blendedTilesFolder.TrimEnd('/', '\\')}/path/{spriteKey}.png");
        }

        return null;
    }

    private void RegisterSprite(string key, string fileName)
    {
        if (_spriteLookup.ContainsKey(key))
            return;

        Sprite sprite = LoadSprite(fileName);
        if (sprite != null)
            _spriteLookup[key] = sprite;
    }

    private Sprite LoadSprite(string fileName)
    {
        string assetPath = $"{imageAssetFolder.TrimEnd('/', '\\')}/{fileName}";

#if UNITY_EDITOR
        if (autoLoadSpritesFromAssetFolder)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                return sprite;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                Rect rect = new Rect(0, 0, texture.width, texture.height);
                Vector2 pivot = new Vector2(0.5f, 0.5f);
                return Sprite.Create(texture, rect, pivot, texture.width);
            }
        }
#endif

        return null;
    }

    private Sprite LoadSpriteFromRelativePath(string relativePath)
    {
        string normalizedRelativePath = relativePath.Replace('\\', '/');

#if UNITY_EDITOR
        if (normalizedRelativePath.StartsWith("Assets/", StringComparison.Ordinal))
        {
            Sprite assetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(normalizedRelativePath);
            if (assetSprite != null)
                return assetSprite;
        }
#endif

        string fullPath = GetProjectRelativePath(normalizedRelativePath);
        if (!File.Exists(fullPath))
            return null;

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
            return null;

        texture.filterMode = FilterMode.Point;
        Rect rect = new Rect(0, 0, texture.width, texture.height);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        return Sprite.Create(texture, rect, pivot, texture.width);
    }

    private string GetInputText()
    {
        string path = GetProjectRelativePath(inputRelativePath);
        if (preferFileInput && File.Exists(path))
            return File.ReadAllText(path);

        if (preferFileInput)
            Debug.LogWarning($"Input file not found. Falling back to Input Text: {path}");

        return inputText;
    }

    private static AsciiTilemapJsonData ParseInputMap(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        string trimmed = input.Trim();
        if (!trimmed.StartsWith("{"))
            return ParsePlainTextMap(input);

        try
        {
            return JsonUtility.FromJson<AsciiTilemapJsonData>(trimmed);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to parse ASCII map JSON: {exception.Message}");
            return null;
        }
    }

    private static AsciiTilemapJsonData ParsePlainTextMap(string input)
    {
        string normalized = input.Replace("\r\n", "\n").Replace('\r', '\n').TrimStart('\ufeff').Trim('\n');
        string[] rows = normalized.Split('\n');
        string[][] tokenRows = TokenizeRows(rows);

        return new AsciiTilemapJsonData
        {
            width = GetMaxTokenRowWidth(tokenRows),
            height = rows.Length,
            startX = 0,
            startY = 0,
            mapGrid = rows
        };
    }

    private Vector3Int GetOrigin(AsciiTilemapJsonData mapData)
    {
        Vector3Int origin = extraOffset;
        if (originMode == OriginMode.UseJsonStart)
            origin += new Vector3Int(mapData.startX, mapData.startY, 0);

        return origin;
    }

    private static string[][] TokenizeRows(string[] rows)
    {
        if (rows == null)
            return new string[0][];

        string[][] result = new string[rows.Length][];
        for (int i = 0; i < rows.Length; i++)
            result[i] = TokenizeRow(rows[i]);

        return result;
    }

    private static string[] TokenizeRow(string row)
    {
        if (string.IsNullOrEmpty(row))
            return new string[0];

        if (row.Contains(","))
            return SplitTokenRow(row, ',');

        List<string> tokens = new List<string>();
        int index = 0;
        while (index < row.Length)
        {
            string token = ReadNextToken(row, index);
            tokens.Add(token);
            index += token.Length;
        }

        return tokens.ToArray();
    }

    private static string ReadNextToken(string row, int startIndex)
    {
        string[] knownTokens =
        {
            "d2_0", "d2_1", "d2_2", "d2_3", "d2_4", "d2_5", "d2_6",
            "g_0", "g_1", "g_2", "g_3", "g_4", "g_5", "g_6",
            "d_0", "d_1", "d_2", "d_3", "d_4", "d_5", "d_6",
            "s_0", "s_1", "s_2", "s_3", "s_4", "s_5", "s_6",
            "d2"
        };

        foreach (string token in knownTokens)
        {
            if (startIndex + token.Length <= row.Length &&
                string.Compare(row, startIndex, token, 0, token.Length, StringComparison.Ordinal) == 0)
            {
                return token;
            }
        }

        return row[startIndex].ToString();
    }

    private static string[] SplitTokenRow(string row, char separator)
    {
        string[] rawTokens = row.Split(separator);
        List<string> tokens = new List<string>();
        foreach (string rawToken in rawTokens)
        {
            string token = rawToken.Trim();
            if (!string.IsNullOrEmpty(token))
                tokens.Add(token);
        }

        return tokens.ToArray();
    }

    private static int GetMaxTokenRowWidth(string[][] rows)
    {
        int width = 0;
        if (rows == null)
            return width;

        foreach (string[] row in rows)
            width = Mathf.Max(width, row != null ? row.Length : 0);

        return width;
    }

    private Transform GetSpawnRoot()
    {
        Transform existing = transform.Find(SpawnRootName);
        if (existing != null)
            return existing;

        GameObject root = new GameObject(SpawnRootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        return root.transform;
    }

    private void ClearSpawnedPrefabs()
    {
        Transform root = transform.Find(SpawnRootName);
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private static string GetProjectRelativePath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
    }
}

[Serializable]
public class AsciiTilemapJsonData
{
    public int width;
    public int height;
    public int startX;
    public int startY;
    public string[] mapGrid;
}
