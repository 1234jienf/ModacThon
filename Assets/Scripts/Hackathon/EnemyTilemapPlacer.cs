using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnemyTilemapPlacer : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap referenceTilemap;
    public Tilemap groundTilemap;
    public Tilemap dirtTilemap;
    public Tilemap wallTilemap;
    public Tilemap lakeTilemap;

    [Header("Placement")]
    public bool placeOnStart = true;
    public bool clearBeforePlace = true;
    public bool avoidLake = true;
    public int enemyCount = 90;
    public int randomSeed = 20260628;
    public bool useRandomSeed = true;
    public float minDistanceBetweenEnemies = 3f;
    public float dirtSearchRadius = 8f;
    [Range(0f, 1f)] public float dirtPlacementWeight = 0.8f;
    [Range(0f, 1f)] public float maxDirtPlacementRatio = 0.45f;
    public float groundPlacementWeight = 2.5f;
    public float dirtTilePlacementWeight = 2.0f;
    public float nearDirtGroundBonus = 1.25f;
    [Range(0f, 1f)] public float mapBDifficultyWeight = 0.75f;
    public float enemyRoamRadius = 2.2f;
    public Vector3 spawnOffset = new Vector3(0f, 0f, -0.1f);

    public void ApplyDifficultySettings(BridgeDifficultyPreset preset)
    {
        enemyCount = preset.enemyCount;
        mapBDifficultyWeight = preset.mapBDifficultyWeight;
        enemyRoamRadius = preset.enemyRoamRadius;

        if (BridgeGameSession.Instance != null && BridgeGameSession.Instance.UseFixedGenerationSeed)
        {
            useRandomSeed = true;
            randomSeed = BridgeGameSession.Instance.GenerationSeed + preset.level * 1000;
        }
    }

    [Header("Sprites")]
    public string spriteRootFolder = "Assets/Sprites";
    public float animationFps = 6f;
    public int sortingOrder = 10;
    public string sortingLayerName = "Default";
    public List<EnemyDefinition> enemies = new List<EnemyDefinition>
    {
        new EnemyDefinition { enemyName = "Bird 2", minDifficulty = 0f, pixelsPerUnit = 32f, minSpeed = 0.55f, maxSpeed = 1.1f, moveTime = 2f, restTime = 1f },
        new EnemyDefinition { enemyName = "Rat", minDifficulty = 0.30f, pixelsPerUnit = 32f, minSpeed = 0.4f, maxSpeed = 0.85f, moveTime = 2.5f, restTime = 1.2f },
        new EnemyDefinition { enemyName = "Cat", minDifficulty = 0.60f, pixelsPerUnit = 32f, minSpeed = 0.35f, maxSpeed = 0.75f, moveTime = 3f, restTime = 1.5f },
        new EnemyDefinition { enemyName = "Dog", minDifficulty = 0.82f, pixelsPerUnit = 32f, minSpeed = 0.3f, maxSpeed = 0.65f, moveTime = 3.5f, restTime = 1.8f }
    };

    private const string EnemyRootName = "Generated Enemies";

    private void Start()
    {
        if (!placeOnStart)
            return;

        if (BridgeGameSession.Instance != null)
            return;

        StartCoroutine(PlaceEnemiesAfterTilemapRender());
    }

    private IEnumerator PlaceEnemiesAfterTilemapRender()
    {
        yield return null;
        PlaceEnemies();
    }

    [ContextMenu("Place Enemies")]
    public void PlaceEnemies()
    {
        Tilemap reference = GetReferenceTilemap();
        if (reference == null)
        {
            Debug.LogError("EnemyTilemapPlacer needs at least one Tilemap reference.");
            return;
        }

        if (clearBeforePlace)
            ClearGeneratedEnemies();

        List<EnemyDefinition> usableEnemies = GetUsableEnemies();
        if (usableEnemies.Count == 0)
        {
            Debug.LogError("No enemy definitions are configured.");
            return;
        }

        BoundsInt bounds;
        if (!TryGetPlacementBounds(out bounds))
        {
            Debug.LogError("Could not find Tilemap bounds for enemy placement.");
            return;
        }

        System.Random random = useRandomSeed ? new System.Random(randomSeed) : new System.Random();
        List<CandidateCell> candidates = BuildCandidates(bounds);
        List<Vector3Int> placedCells = new List<Vector3Int>();
        Transform root = GetEnemyRoot();

        int placedCount = 0;
        int placedDirtCount = 0;
        for (int i = 0; i < enemyCount && candidates.Count > 0; i++)
        {
            CandidateCell candidate;
            if (!TryPickCandidate(candidates, placedCells, placedDirtCount, random, out candidate))
                break;

            EnemyDefinition enemy = SelectEnemyForDifficulty(usableEnemies, candidate.difficulty, random);
            CreateEnemy(enemy, candidate.cell, reference, root);
            placedCells.Add(candidate.cell);
            if (candidate.isDirtTile)
                placedDirtCount++;
            placedCount++;
        }

        Debug.Log($"Placed {placedCount}/{enemyCount} enemies. Dirt: {placedDirtCount}, Ground: {placedCount - placedDirtCount}");
    }

    [ContextMenu("Clear Generated Enemies")]
    public void ClearGeneratedEnemies()
    {
        Transform root = transform.Find(EnemyRootName);
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

    private List<CandidateCell> BuildCandidates(BoundsInt bounds)
    {
        List<CandidateCell> candidates = new List<CandidateCell>();
        int xMax = bounds.xMin + bounds.size.x - 1;
        int yMax = bounds.yMin + bounds.size.y - 1;

        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            Vector3Int pos = new Vector3Int(cell.x, cell.y, 0);
            if (HasTile(wallTilemap, pos))
                continue;
            if (avoidLake && HasTile(lakeTilemap, pos))
                continue;
            if (!HasAnyWalkableBaseTile(pos))
                continue;

            bool isDirtTile = HasTile(dirtTilemap, pos);
            bool isGroundTile = HasTile(groundTilemap, pos);
            float mapBWeight = GetMapBWeight(pos, bounds.xMin, xMax, bounds.yMin, yMax);
            float dirtAffinity = GetDirtAffinity(pos);
            float placementWeight = GetPlacementWeight(isGroundTile, isDirtTile, dirtAffinity, mapBWeight);
            float difficulty = Mathf.Clamp01(mapBWeight * mapBDifficultyWeight + dirtAffinity * (1f - mapBDifficultyWeight));

            candidates.Add(new CandidateCell
            {
                cell = pos,
                placementWeight = placementWeight,
                difficulty = difficulty,
                isDirtTile = isDirtTile
            });
        }

        return candidates;
    }

    private float GetPlacementWeight(bool isGroundTile, bool isDirtTile, float dirtAffinity, float mapBWeight)
    {
        float mapBPlacementBonus = Mathf.Lerp(0.5f, 1.5f, mapBWeight);
        if (isDirtTile)
            return Mathf.Max(0.01f, dirtTilePlacementWeight + mapBPlacementBonus * dirtPlacementWeight);

        float baseWeight = isGroundTile ? groundPlacementWeight : 1f;
        float nearDirtBonus = dirtAffinity * nearDirtGroundBonus;
        return Mathf.Max(0.01f, baseWeight + nearDirtBonus + mapBPlacementBonus);
    }

    private bool HasAnyWalkableBaseTile(Vector3Int cell)
    {
        if (groundTilemap == null && dirtTilemap == null && lakeTilemap == null)
            return true;

        return HasTile(groundTilemap, cell) || HasTile(dirtTilemap, cell) || (!avoidLake && HasTile(lakeTilemap, cell));
    }

    private float GetMapBWeight(Vector3Int cell, int xMin, int xMax, int yMin, int yMax)
    {
        float xRatio = xMax == xMin ? 0.5f : Mathf.InverseLerp(xMin, xMax, cell.x);
        float yRatio = yMax == yMin ? 0.5f : Mathf.InverseLerp(yMin, yMax, cell.y);
        return Mathf.Clamp01((xRatio + yRatio) * 0.5f);
    }

    private float GetDirtAffinity(Vector3Int cell)
    {
        if (dirtTilemap == null || dirtSearchRadius <= 0f)
            return 0f;
        if (dirtTilemap.HasTile(cell))
            return 1f;

        int radius = Mathf.CeilToInt(dirtSearchRadius);
        float bestDistance = float.MaxValue;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Vector3Int check = new Vector3Int(cell.x + x, cell.y + y, 0);
                if (!dirtTilemap.HasTile(check))
                    continue;

                float distance = Mathf.Sqrt(x * x + y * y);
                if (distance < bestDistance)
                    bestDistance = distance;
            }
        }

        if (bestDistance > dirtSearchRadius)
            return 0f;

        return Mathf.Clamp01(1f - bestDistance / dirtSearchRadius);
    }

    private bool TryPickCandidate(
        List<CandidateCell> candidates,
        List<Vector3Int> placedCells,
        int placedDirtCount,
        System.Random random,
        out CandidateCell picked)
    {
        List<CandidateCell> filtered = new List<CandidateCell>();
        int maxDirtCount = Mathf.FloorToInt(enemyCount * maxDirtPlacementRatio);
        foreach (CandidateCell candidate in candidates)
        {
            if (!IsFarEnough(candidate.cell, placedCells))
                continue;
            if (candidate.isDirtTile && placedDirtCount >= maxDirtCount)
                continue;

            filtered.Add(candidate);
        }

        if (filtered.Count == 0)
        {
            picked = default(CandidateCell);
            return false;
        }

        float totalWeight = 0f;
        foreach (CandidateCell candidate in filtered)
            totalWeight += Mathf.Max(0.01f, candidate.placementWeight);

        double roll = random.NextDouble() * totalWeight;
        float cumulative = 0f;
        foreach (CandidateCell candidate in filtered)
        {
            cumulative += Mathf.Max(0.01f, candidate.placementWeight);
            if (roll <= cumulative)
            {
                picked = candidate;
                candidates.Remove(candidate);
                return true;
            }
        }

        picked = filtered[filtered.Count - 1];
        candidates.Remove(picked);
        return true;
    }

    private bool IsFarEnough(Vector3Int cell, List<Vector3Int> placedCells)
    {
        float minSqrDistance = minDistanceBetweenEnemies * minDistanceBetweenEnemies;
        foreach (Vector3Int placedCell in placedCells)
        {
            Vector2 delta = new Vector2(cell.x - placedCell.x, cell.y - placedCell.y);
            if (delta.sqrMagnitude < minSqrDistance)
                return false;
        }

        return true;
    }

    private EnemyDefinition SelectEnemyForDifficulty(List<EnemyDefinition> usableEnemies, float difficulty, System.Random random)
    {
        EnemyDefinition selected = usableEnemies[0];
        foreach (EnemyDefinition enemy in usableEnemies)
        {
            if (difficulty >= enemy.minDifficulty)
                selected = enemy;
        }

        int selectedIndex = usableEnemies.IndexOf(selected);
        if (selectedIndex > 0 && random.NextDouble() < 0.18f)
            return usableEnemies[selectedIndex - 1];
        if (selectedIndex < usableEnemies.Count - 1 && random.NextDouble() < 0.12f)
            return usableEnemies[selectedIndex + 1];

        return selected;
    }

    private void CreateEnemy(EnemyDefinition enemy, Vector3Int cell, Tilemap reference, Transform root)
    {
        Sprite[] frames = LoadWalkFrames(enemy);
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning($"Could not load walk sprites for enemy: {enemy.enemyName}");
            return;
        }

        GameObject enemyObject = new GameObject(enemy.enemyName);
        enemyObject.transform.SetParent(root);
        enemyObject.transform.position = reference.GetCellCenterWorld(cell) + spawnOffset;

        SpriteRenderer renderer = enemyObject.AddComponent<SpriteRenderer>();
        renderer.sprite = frames[0];
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;

        TilemapEnemyAnimator animator = enemyObject.AddComponent<TilemapEnemyAnimator>();
        animator.frames = frames;
        animator.framesPerSecond = animationFps;

        TilemapEnemyRoam roam = enemyObject.AddComponent<TilemapEnemyRoam>();
        roam.Configure(enemy.minSpeed, enemy.maxSpeed, enemy.moveTime, enemy.restTime, enemy.startDelayMax);

        TilemapEnemyWalkBounds walkBounds = enemyObject.AddComponent<TilemapEnemyWalkBounds>();
        walkBounds.Configure(reference, groundTilemap, dirtTilemap, wallTilemap, lakeTilemap, cell, enemyRoamRadius);
        roam.BindWalkBounds(walkBounds);
    }

    private Sprite[] LoadWalkFrames(EnemyDefinition enemy)
    {
        if (enemy.walkFrames != null && enemy.walkFrames.Length > 0)
            return enemy.walkFrames;

#if UNITY_EDITOR
        string assetPath = $"{spriteRootFolder.TrimEnd('/', '\\')}/{enemy.enemyName}/Walk.png";
        Sprite[] sprites = LoadSlicedSprites(assetPath);
        if (sprites.Length > 1)
        {
            enemy.walkFrames = sprites;
            return enemy.walkFrames;
        }

        Texture2D texture = LoadTextureFromProjectPath(assetPath);
        if (texture == null)
            return new Sprite[0];

        enemy.walkFrames = SliceTextureHorizontally(texture, enemy.pixelsPerUnit);
        return enemy.walkFrames;
#else
        return enemy.walkFrames != null ? enemy.walkFrames : new Sprite[0];
#endif
    }

#if UNITY_EDITOR
    private static Sprite[] LoadSlicedSprites(string assetPath)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        List<Sprite> sprites = new List<Sprite>();
        foreach (UnityEngine.Object asset in assets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null)
                sprites.Add(sprite);
        }

        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return sprites.ToArray();
    }
#endif

    private static Sprite[] SliceTextureHorizontally(Texture2D texture, float pixelsPerUnit)
    {
        if (texture == null)
            return new Sprite[0];

        int frameHeight = texture.height;
        int frameCount = Mathf.Max(1, Mathf.RoundToInt(texture.width / (float)frameHeight));
        int frameWidth = texture.width / frameCount;
        Sprite[] frames = new Sprite[frameCount];
        float ppu = pixelsPerUnit > 0f ? pixelsPerUnit : frameHeight;

        for (int i = 0; i < frameCount; i++)
        {
            Rect rect = new Rect(i * frameWidth, 0, frameWidth, frameHeight);
            frames[i] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), ppu);
            frames[i].name = $"{texture.name}_{i}";
        }

        return frames;
    }

    private static Texture2D LoadTextureFromProjectPath(string projectRelativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath));
        if (!File.Exists(fullPath))
            return null;

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        if (!texture.LoadImage(bytes))
            return null;

        return texture;
    }

    private bool TryGetPlacementBounds(out BoundsInt bounds)
    {
        bool hasBounds = false;
        bounds = new BoundsInt();
        AddTilemapBounds(groundTilemap, ref bounds, ref hasBounds);
        AddTilemapBounds(dirtTilemap, ref bounds, ref hasBounds);
        AddTilemapBounds(lakeTilemap, ref bounds, ref hasBounds);
        AddTilemapBounds(wallTilemap, ref bounds, ref hasBounds);
        AddTilemapBounds(referenceTilemap, ref bounds, ref hasBounds);
        return hasBounds;
    }

    private static void AddTilemapBounds(Tilemap tilemap, ref BoundsInt bounds, ref bool hasBounds)
    {
        if (tilemap == null)
            return;

        tilemap.CompressBounds();
        BoundsInt tilemapBounds = tilemap.cellBounds;
        if (tilemapBounds.size.x <= 0 || tilemapBounds.size.y <= 0)
            return;

        if (!hasBounds)
        {
            bounds = tilemapBounds;
            hasBounds = true;
            return;
        }

        int xMin = Mathf.Min(bounds.xMin, tilemapBounds.xMin);
        int yMin = Mathf.Min(bounds.yMin, tilemapBounds.yMin);
        int xMax = Mathf.Max(bounds.xMax, tilemapBounds.xMax);
        int yMax = Mathf.Max(bounds.yMax, tilemapBounds.yMax);
        bounds = new BoundsInt(xMin, yMin, 0, xMax - xMin, yMax - yMin, 1);
    }

    private Tilemap GetReferenceTilemap()
    {
        if (referenceTilemap != null)
            return referenceTilemap;
        if (groundTilemap != null)
            return groundTilemap;
        if (dirtTilemap != null)
            return dirtTilemap;
        if (wallTilemap != null)
            return wallTilemap;

        return lakeTilemap;
    }

    private Transform GetEnemyRoot()
    {
        Transform existing = transform.Find(EnemyRootName);
        if (existing != null)
            return existing;

        GameObject root = new GameObject(EnemyRootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        return root.transform;
    }

    private List<EnemyDefinition> GetUsableEnemies()
    {
        List<EnemyDefinition> usableEnemies = new List<EnemyDefinition>();
        foreach (EnemyDefinition enemy in enemies)
        {
            if (enemy != null && !string.IsNullOrWhiteSpace(enemy.enemyName))
                usableEnemies.Add(enemy);
        }

        usableEnemies.Sort((a, b) => a.minDifficulty.CompareTo(b.minDifficulty));
        return usableEnemies;
    }

    private static bool HasTile(Tilemap tilemap, Vector3Int cell)
    {
        return tilemap != null && tilemap.HasTile(cell);
    }

    private struct CandidateCell
    {
        public Vector3Int cell;
        public float placementWeight;
        public float difficulty;
        public bool isDirtTile;
    }
}

[Serializable]
public class EnemyDefinition
{
    public string enemyName;
    [Range(0f, 1f)] public float minDifficulty;
    public float pixelsPerUnit = 32f;
    public float minSpeed = 0.35f;
    public float maxSpeed = 0.85f;
    public float moveTime = 2.5f;
    public float restTime = 1.5f;
    public float startDelayMax = 1.5f;
    public Sprite[] walkFrames;
}

public class TilemapEnemyAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 6f;
    public bool isMoving = true;

    private SpriteRenderer _spriteRenderer;
    private float _timer;
    private int _frameIndex;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (!isMoving || _spriteRenderer == null || frames == null || frames.Length <= 1 || framesPerSecond <= 0f)
            return;

        if ((Time.frameCount + GetInstanceID()) % 2 != 0)
            return;

        _timer += Time.deltaTime;
        float frameDuration = 1f / framesPerSecond;
        while (_timer >= frameDuration)
        {
            _timer -= frameDuration;
            _frameIndex = (_frameIndex + 1) % frames.Length;
            _spriteRenderer.sprite = frames[_frameIndex];
        }
    }
}
