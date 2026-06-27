using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class PathRunSegmentReport
{
    public string map_id;
    public bool success;
    public float elapsed_seconds;
    public float world_distance;
    public int path_tile_steps;
    public int ground_tile_steps;
    public int other_tile_steps;
    public int total_tile_steps;
    public float average_speed;
}

[Serializable]
public class PathRunReport
{
    public string map_id;
    public string created_at;
    public bool success;
    public float elapsed_seconds;
    public float world_distance;
    public int path_tile_steps;
    public int ground_tile_steps;
    public int other_tile_steps;
    public int total_tile_steps;
    public float average_speed;
    public string report_json_path;
    public string report_csv_path;
    public string failure_reason;

    public bool has_baseline_comparison;
    public int baseline_total_tile_steps;
    public float baseline_world_distance;
    public float baseline_estimated_seconds;
    public float elapsed_delta_seconds;
    public float distance_delta;
    public int replan_count;
    public int enemy_count;
    public int difficulty_level;
}

[Serializable]
public struct PathRunLiveStats
{
    public bool isRunning;
    public int difficultyLevel;
    public float elapsedSeconds;
    public float actualRouteDistance;
    public float baselineRouteDistance;
    public float baselineEstimatedSeconds;
    public int replanCount;
    public int enemyCount;
}

public class AutoPathRunner : MonoBehaviour
{
    [Header("Input")]
    public TilemapDataProvider mapProvider;
    public string mapId = "map_test";

    [Header("Runner")]
    public Transform runnerTransform;
    public bool usePlayerOnSameObject = true;
    public bool autoCreateRunner = false;
    public float moveSpeed = 3f;
    public Color runnerColor = new Color(1f, 0.2f, 0.8f, 1f);

    [Header("Player Control")]
    public bool disablePlayerControlDuringRun = true;
    public bool drivePlayerAnimator = true;

    [Header("Run")]
    public bool runOnStart = false;
    public bool autoGoalWhenMissing = true;
    public float startDelay = 0.5f;
    public bool loopRun = false;
    public float loopDelay = 1f;

    [Header("Pathfinding (Visual ASCII)")]
    [Tooltip("BSP PathProcessor가 만든 BridgeMapData_Path.json 으로 이동")]
    public bool useBridgePathJson = true;
    public string bridgePathJsonRelativePath = "tmpOutput/BridgeMapData_Path.json";

    [Tooltip("Visual ASCII(g/p/w) 기준으로 path(p) 타일을 따라 이동 (Field 맵 모드)")]
    public bool followVisualPath = true;
    [Tooltip("path(p), S, G 타일만 우선 이동")]
    public bool pathTilesOnly = true;
    [Tooltip("path가 끊기면 grass(g) 등으로 우회 (A* 비용 높음)")]
    public bool allowGrassDetour = true;
    public float pathTileCost = 1f;
    public float grassTileCost = 20f;

    private bool _allowGrassDetourWhenBlocked = true;

    [Header("Enemy Avoidance")]
    public bool avoidGeneratedEnemies = true;
    public Transform enemyRoot;
    public string generatedEnemiesRootName = "Generated Enemies";
    [Tooltip("Play 직후 EnemyTilemapPlacer가 적을 깔 때까지 A* 시작을 잠깐 대기")]
    public bool waitForGeneratedEnemies = true;
    public float enemySpawnWaitTimeout = 2f;
    public int enemyBlockedRadius = 1;
    public int enemyAvoidanceRadius = 2;
    public float enemyAvoidanceCost = 60f;
    [Tooltip("이동 중 몬스터 위치가 바뀌면 주기적으로 A* 재계산")]
    public bool replanPathDuringRun = true;
    public float replanInterval = 1.5f;

    [Header("Path Visualization")]
    public bool showPathLines = true;
    public Color baselinePathColor = new Color(1f, 0.15f, 0.15f, 0.95f);
    public Color dynamicPathColor = new Color(0.2f, 0.75f, 1f, 0.95f);
    public float pathLineWidth = 0.12f;
    public int pathLineSortingOrder = 5000;
    public string pathLineSortingLayer = "Default";
    public float pathLineZOffset = 0f;

    [Header("Output")]
    public bool exportReport = true;
    public string outputRoot = "HackathonAI/path_run_reports";

    [Header("Latest Result")]
    public PathRunReport latestReport;
    public PathRunLiveStats liveStats;

    public bool IsRunning => _runCoroutine != null;

    public void ApplyDifficultySettings(BridgeDifficultyPreset preset)
    {
        enemyBlockedRadius = preset.enemyBlockedRadius;
        enemyAvoidanceRadius = preset.enemyAvoidanceRadius;
        enemyAvoidanceCost = preset.enemyAvoidanceCost;
        grassTileCost = preset.grassDetourCost;
        _allowGrassDetourWhenBlocked = preset.allowGrassDetourWhenBlocked;
    }

    private Coroutine _runCoroutine;
    private SpriteRenderer _runnerRenderer;
    private Rigidbody2D _runnerRigidbody;
    private Player _player;
    private Animator _animator;
    private bool _playerWasEnabled;
    private LineRenderer _baselinePathLine;
    private LineRenderer _dynamicPathLine;
    private Transform _pathLineRoot;
    private float _liveBaselineDistance;
    private float _liveBaselineEstimatedSeconds;
    private int _liveEnemyCount;
    private int _liveReplanCount;
    private RigidbodyType2D _savedBodyType;
    private bool _savedSimulated;
    private PathRunLiveStats _liveStatsCache;

    private void Awake()
    {
        _player = GetComponent<Player>();
        _animator = GetComponent<Animator>();

        if (usePlayerOnSameObject)
        {
            runnerTransform = transform;
            autoCreateRunner = false;
        }

        if (GetComponent<PathRunStatsUI>() == null)
            gameObject.AddComponent<PathRunStatsUI>();
    }

    private void Start()
    {
        EnsureRunnerTransform();

        if (!runOnStart)
        {
            return;
        }

        if (useBridgePathJson)
        {
            // Bridge 모드: PathProcessor.ProcessRoomPaths() 끝에서 StartRun() 호출.
            // runOnStart만 켜도 Player Start()에서는 기다리지 않음 (JSON 생성 전일 수 있음).
            return;
        }

        StartRun();
    }

    [ContextMenu("Start Auto Path Run")]
    public void StartRun()
    {
        EnsureRunnerTransform();

        if (_runCoroutine != null)
        {
            StopCoroutine(_runCoroutine);
        }

        if (useBridgePathJson)
        {
            _runCoroutine = StartCoroutine(RunPathRoutine(useBridgePath: true));
            return;
        }

        ResolveMapProvider();

        if (mapProvider == null)
        {
            Debug.LogError("AutoPathRunner: mapProvider를 연결해 주세요. Player와 같은 Field의 TilemapDataProvider를 Inspector에 드래그하세요.");
            return;
        }

        _runCoroutine = StartCoroutine(RunPathRoutine(useBridgePath: false));
    }

    [ContextMenu("Stop Auto Path Run")]
    public void StopRun()
    {
        if (_runCoroutine != null)
        {
            StopCoroutine(_runCoroutine);
            _runCoroutine = null;
        }

        SetWalkingAnimation(false);
        RestorePlayerControl();
        RestoreRunnerPhysics();
        SetPathLinesVisible(false);
    }

    private void EnsureRunnerTransform()
    {
        if (runnerTransform != null)
        {
            if (_runnerRigidbody == null)
                _runnerRigidbody = runnerTransform.GetComponent<Rigidbody2D>();
            return;
        }

        if (_player != null || usePlayerOnSameObject)
        {
            runnerTransform = transform;
            autoCreateRunner = false;
            _runnerRigidbody = GetComponent<Rigidbody2D>();
            return;
        }

        if (!autoCreateRunner)
        {
            runnerTransform = transform;
            return;
        }

        GameObject runnerObject = new GameObject("AutoPathRunnerProxy");
        runnerObject.transform.SetParent(transform, false);
        runnerTransform = runnerObject.transform;

        _runnerRenderer = runnerObject.AddComponent<SpriteRenderer>();
        _runnerRenderer.sprite = CreateCircleSprite();
        _runnerRenderer.color = runnerColor;
        _runnerRenderer.sortingOrder = 20000;
        runnerObject.transform.localScale = Vector3.one * 0.6f;
    }

    private void SetRunnerWorldPosition(Vector3 worldPosition)
    {
        if (runnerTransform != null)
            runnerTransform.position = worldPosition;

        if (_runnerRigidbody != null)
        {
            _runnerRigidbody.velocity = Vector2.zero;
            _runnerRigidbody.angularVelocity = 0f;
            _runnerRigidbody.position = new Vector2(worldPosition.x, worldPosition.y);
        }
    }

    private Vector3 GetRunnerWorldPosition()
    {
        if (runnerTransform != null)
            return runnerTransform.position;

        if (_runnerRigidbody != null)
            return _runnerRigidbody.position;

        return Vector3.zero;
    }

    private IEnumerator RunPathRoutine(bool useBridgePath)
    {
        DisablePlayerControl();
        PrepareRunnerPhysics();

        do
        {
            if (startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
            }

            if (useBridgePath)
            {
                yield return StartCoroutine(ExecuteBridgePathRunCoroutine());
            }
            else
            {
                yield return StartCoroutine(ExecuteRunCoroutine());
            }
            PathRunReport report = latestReport;
            LogReport(report);

            if (exportReport)
            {
                ExportReport(report);
                BridgeDifficultyResultsTracker.Record(report);
                BridgeDifficultyResultsTracker.Export(outputRoot);
            }

            if (!loopRun)
            {
                break;
            }

            yield return new WaitForSeconds(loopDelay);
        }
        while (loopRun);

        SetWalkingAnimation(false);
        RestorePlayerControl();
        RestoreRunnerPhysics();
        _runCoroutine = null;
    }

    private void PrepareRunnerPhysics()
    {
        EnsureRunnerTransform();
        if (_runnerRigidbody == null)
        {
            return;
        }

        _savedBodyType = _runnerRigidbody.bodyType;
        _savedSimulated = _runnerRigidbody.simulated;
        _runnerRigidbody.bodyType = RigidbodyType2D.Kinematic;
        _runnerRigidbody.simulated = false;
        _runnerRigidbody.velocity = Vector2.zero;
        _runnerRigidbody.angularVelocity = 0f;
    }

    private void RestoreRunnerPhysics()
    {
        if (_runnerRigidbody == null)
        {
            return;
        }

        _runnerRigidbody.simulated = _savedSimulated;
        _runnerRigidbody.bodyType = _savedBodyType;
    }

    private void UpdateLiveStats(
        bool isRunning,
        float elapsedSeconds = 0f,
        float actualRouteDistance = 0f)
    {
        _liveStatsCache.isRunning = isRunning;
        _liveStatsCache.difficultyLevel = BridgeDifficultyController.Instance != null
            ? BridgeDifficultyController.Instance.DifficultyLevel
            : 0;
        _liveStatsCache.elapsedSeconds = elapsedSeconds;
        _liveStatsCache.actualRouteDistance = actualRouteDistance;
        _liveStatsCache.baselineRouteDistance = _liveBaselineDistance;
        _liveStatsCache.baselineEstimatedSeconds = _liveBaselineEstimatedSeconds;
        _liveStatsCache.replanCount = _liveReplanCount;
        _liveStatsCache.enemyCount = _liveEnemyCount;
        liveStats = _liveStatsCache;
    }

    private void DisablePlayerControl()
    {
        if (!disablePlayerControlDuringRun || _player == null)
        {
            return;
        }

        _playerWasEnabled = _player.enabled;
        _player.enabled = false;
    }

    private void RestorePlayerControl()
    {
        if (!disablePlayerControlDuringRun || _player == null)
        {
            return;
        }

        _player.enabled = _playerWasEnabled;
    }

    private void ResolveMapProvider()
    {
        if (mapProvider != null)
        {
            return;
        }

        Transform fieldRoot = transform.parent;
        if (fieldRoot != null)
        {
            TilemapDataProvider[] providers = fieldRoot.GetComponentsInChildren<TilemapDataProvider>(true);
            mapProvider = PickBestMapProvider(providers);
        }

        if (mapProvider == null)
        {
            mapProvider = GetComponentInParent<TilemapDataProvider>();
        }
    }

    private static TilemapDataProvider PickBestMapProvider(TilemapDataProvider[] providers)
    {
        if (providers == null || providers.Length == 0)
        {
            return null;
        }

        if (providers.Length == 1)
        {
            return providers[0];
        }

        TilemapDataProvider best = providers[0];
        int bestScore = ScoreMapProvider(best);

        for (int i = 1; i < providers.Length; i++)
        {
            int score = ScoreMapProvider(providers[i]);
            if (score > bestScore)
            {
                bestScore = score;
                best = providers[i];
            }
        }

        return best;
    }

    private void ResolveBridgeScenePoints(out Transform sceneStart, out Transform sceneGoal)
    {
        sceneStart = null;
        sceneGoal = null;

        TilemapDataProvider[] providers = FindObjectsOfType<TilemapDataProvider>(true);
        foreach (TilemapDataProvider provider in providers)
        {
            if (provider == null)
            {
                continue;
            }

            if (provider.startPoint != null)
            {
                sceneStart = provider.startPoint.transform;
            }

            if (provider.goalPoint != null)
            {
                sceneGoal = provider.goalPoint.transform;
            }

            if (sceneStart != null && sceneGoal != null)
            {
                return;
            }
        }
    }

    private static int ScoreMapProvider(TilemapDataProvider provider)
    {
        int score = 0;
        if (provider.startPoint != null)
        {
            score += 100;
        }

        if (provider.goalPoint != null)
        {
            score += 50;
        }

        char[,] matrix = provider.GetMapMatrix();
        score += matrix.GetLength(0) * matrix.GetLength(1) / 100;
        return score;
    }

    private IEnumerator ExecuteBridgePathRunCoroutine()
    {
        if (!BridgeMapJsonUtility.TryLoadFromFile(
                bridgePathJsonRelativePath,
                out InputMapData bridgeData,
                out char[,] pathMatrix))
        {
            latestReport = BuildFailureReport(
                $"Bridge path JSON을 찾지 못했습니다: {bridgePathJsonRelativePath}. BSP_Generator + PathProcessor가 Play에서 실행되는지 확인하세요.");
            yield break;
        }

        ResolveBridgeScenePoints(out Transform sceneStart, out Transform sceneGoal);

        if (!BridgeMapJsonUtility.TryResolveBridgeEndpoints(
                bridgeData,
                pathMatrix,
                sceneStart,
                sceneGoal,
                out Vector2Int start,
                out Vector2Int goal))
        {
            latestReport = BuildFailureReport("Bridge map에서 S/E(좌→우 path) 끝점을 찾지 못했습니다.");
            yield break;
        }

        EnsureRunnerTransform();
        SetRunnerWorldPosition(BridgeMapJsonUtility.GridCellToWorld(bridgeData, start.x, start.y));

        Debug.Log(
            $"AutoPathRunner [{mapId}]: bridge start=({start.x},{start.y}) goal=({goal.x},{goal.y}) " +
            $"world=({bridgeData.startX},{bridgeData.startY}) size={bridgeData.width}x{bridgeData.height}");

        List<Vector2Int> baselinePath = ComputeBridgePath(bridgeData, pathMatrix, start, goal, useEnemyAvoidance: false);
        if (baselinePath == null || baselinePath.Count == 0)
        {
            latestReport = BuildFailureReport("Baseline bridge path(P only)를 찾지 못했습니다.");
            yield break;
        }

        float baselineWorldDistance = MeasureBridgePathWorldDistance(bridgeData, baselinePath);
        float baselineEstimatedSeconds = moveSpeed > 0f ? baselineWorldDistance / moveSpeed : 0f;
        _liveBaselineDistance = baselineWorldDistance;
        _liveBaselineEstimatedSeconds = baselineEstimatedSeconds;
        UpdatePathLine(GetBaselinePathLine(), bridgeData, baselinePath);

        if (waitForGeneratedEnemies && avoidGeneratedEnemies)
            yield return WaitForGeneratedEnemies();

        int enemyCount = CountGeneratedEnemies();
        _liveEnemyCount = enemyCount;
        _liveReplanCount = 0;
        List<Vector2Int> path = ComputeBridgePath(bridgeData, pathMatrix, start, goal, useEnemyAvoidance: true);
        if (!IsUsablePath(path))
        {
            Debug.LogWarning(
                "AutoPathRunner: enemy-avoidance path unavailable; using baseline path. " +
                "Path may be fully blocked by enemies — try soft detour or fewer blockers.");
            path = baselinePath;
        }

        if (path == null || path.Count <= 1)
        {
            latestReport = BuildFailureReport("Bridge map A→B path(P)가 끊겨 있습니다. PathProcessor 결과를 확인하세요.");
            yield break;
        }

        UpdatePathLine(GetDynamicPathLine(), bridgeData, path);
        SetPathLinesVisible(showPathLines);

        SetRunnerWorldPosition(BridgeMapJsonUtility.GridCellToWorld(bridgeData, start.x, start.y));

        float elapsed = 0f;
        float worldDistance = 0f;
        int pathSteps = 0;
        int groundSteps = 0;
        int otherSteps = 0;
        float replanTimer = 0f;
        int pathIndex = 1;
        int replanCount = 0;

        UpdateLiveStats(true, 0f, 0f);

        while (pathIndex < path.Count)
        {
            Vector2Int cell = path[pathIndex];
            char marker = pathMatrix[cell.y, cell.x];
            CountBridgeMarker(marker, ref pathSteps, ref groundSteps, ref otherSteps);

            Vector3 target = BridgeMapJsonUtility.GridCellToWorld(bridgeData, cell.x, cell.y);
            Vector3 previous = GetRunnerWorldPosition();
            SetWalkingAnimation(true);
            UpdateFacing(target - GetRunnerWorldPosition());

            while (Vector3.Distance(GetRunnerWorldPosition(), target) > 0.02f)
            {
                float step = moveSpeed * Time.deltaTime;
                SetRunnerWorldPosition(Vector3.MoveTowards(GetRunnerWorldPosition(), target, step));
                elapsed += Time.deltaTime;
                replanTimer += Time.deltaTime;
                UpdateLiveStats(
                    true,
                    elapsed,
                    worldDistance + Vector3.Distance(GetRunnerWorldPosition(), target));

                if (replanPathDuringRun && avoidGeneratedEnemies && replanTimer >= replanInterval)
                {
                    replanTimer = 0f;
                    Vector2Int currentCell = WorldToBridgeCell(bridgeData, GetRunnerWorldPosition());
                    List<Vector2Int> newPath = ComputeBridgePath(bridgeData, pathMatrix, currentCell, goal, useEnemyAvoidance: true);
                    if (newPath != null && newPath.Count > 1)
                    {
                        path = newPath;
                        pathIndex = 1;
                        replanCount++;
                        _liveReplanCount = replanCount;
                        UpdatePathLine(GetDynamicPathLine(), bridgeData, path);
                        cell = path[pathIndex];
                        target = BridgeMapJsonUtility.GridCellToWorld(bridgeData, cell.x, cell.y);
                        UpdateFacing(target - runnerTransform.position);
                    }
                }

                yield return null;
            }

            SetRunnerWorldPosition(target);
            worldDistance += Vector3.Distance(previous, target);
            pathIndex++;
        }

        SetWalkingAnimation(false);
        liveStats = new PathRunLiveStats { isRunning = false };

        int totalSteps = Mathf.Max(1, path.Count - 1);
        int difficultyLevel = BridgeDifficultyController.Instance != null ? BridgeDifficultyController.Instance.DifficultyLevel : 0;
        latestReport = new PathRunReport
        {
            map_id = mapId,
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            success = true,
            elapsed_seconds = elapsed,
            world_distance = worldDistance,
            path_tile_steps = pathSteps,
            ground_tile_steps = groundSteps,
            other_tile_steps = otherSteps,
            total_tile_steps = totalSteps,
            average_speed = elapsed > 0f ? worldDistance / elapsed : 0f,
            has_baseline_comparison = true,
            baseline_total_tile_steps = Mathf.Max(0, baselinePath.Count - 1),
            baseline_world_distance = baselineWorldDistance,
            baseline_estimated_seconds = baselineEstimatedSeconds,
            elapsed_delta_seconds = elapsed - baselineEstimatedSeconds,
            distance_delta = worldDistance - baselineWorldDistance,
            replan_count = replanCount,
            enemy_count = enemyCount,
            difficulty_level = difficultyLevel
        };
    }

    private static void CountBridgeMarker(char marker, ref int pathSteps, ref int groundSteps, ref int otherSteps)
    {
        if (BridgeMapJsonUtility.IsPathTile(marker))
        {
            pathSteps++;
            return;
        }

        if (marker == 'G' || marker == 'g')
        {
            groundSteps++;
            return;
        }

        otherSteps++;
    }

    private List<Vector2Int> ComputeBridgePath(
        InputMapData bridgeData,
        char[,] matrix,
        Vector2Int start,
        Vector2Int goal,
        bool useEnemyAvoidance)
    {
        if (!useEnemyAvoidance || !avoidGeneratedEnemies)
        {
            return FindPathAStar(
                matrix, start, goal, true, false, pathTileCost, grassTileCost, null, null);
        }

        BuildBridgeEnemyAvoidance(
            bridgeData, matrix, start, goal, out HashSet<Vector2Int> blockedCells, out Dictionary<Vector2Int, float> extraCosts);

        List<Vector2Int> path = FindPathAStar(
            matrix, start, goal, true, false, pathTileCost, grassTileCost, blockedCells, extraCosts);
        if (IsUsablePath(path))
            return path;

        path = FindPathAStar(
            matrix, start, goal, true, false, pathTileCost, grassTileCost, null, extraCosts);
        if (IsUsablePath(path))
            return path;

        if (!_allowGrassDetourWhenBlocked || !allowGrassDetour)
            return null;

        path = FindPathAStar(
            matrix, start, goal, false, true, pathTileCost, grassTileCost, null, extraCosts);
        if (IsUsablePath(path))
            return path;

        path = FindPathAStar(
            matrix, start, goal, false, true, pathTileCost, grassTileCost, blockedCells, extraCosts);
        return IsUsablePath(path) ? path : null;
    }

    private static bool IsUsablePath(List<Vector2Int> path)
    {
        return path != null && path.Count > 1;
    }

    private static float MeasureBridgePathWorldDistance(InputMapData bridgeData, List<Vector2Int> path)
    {
        if (path == null || path.Count <= 1)
            return 0f;

        float distance = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 previous = BridgeMapJsonUtility.GridCellToWorld(bridgeData, path[i - 1].x, path[i - 1].y);
            Vector3 current = BridgeMapJsonUtility.GridCellToWorld(bridgeData, path[i].x, path[i].y);
            distance += Vector3.Distance(previous, current);
        }

        return distance;
    }

    private int CountGeneratedEnemies()
    {
        int count = 0;
        foreach (Transform _ in GetEnemyTransforms())
            count++;
        return count;
    }

    private void EnsurePathLineRenderers()
    {
        if (_baselinePathLine == null)
            _baselinePathLine = CreatePathLineRenderer("BaselinePathLine", baselinePathColor);
        if (_dynamicPathLine == null)
            _dynamicPathLine = CreatePathLineRenderer("DynamicPathLine", dynamicPathColor);
    }

    private LineRenderer GetBaselinePathLine()
    {
        EnsurePathLineRenderers();
        return _baselinePathLine;
    }

    private LineRenderer GetDynamicPathLine()
    {
        EnsurePathLineRenderers();
        return _dynamicPathLine;
    }

    private Transform GetPathLineRoot()
    {
        if (_pathLineRoot != null)
            return _pathLineRoot;

        GameObject existing = GameObject.Find("BridgePathLines");
        if (existing == null)
            existing = new GameObject("BridgePathLines");

        _pathLineRoot = existing.transform;
        return _pathLineRoot;
    }

    private LineRenderer CreatePathLineRenderer(string objectName, Color color)
    {
        Transform root = GetPathLineRoot();
        Transform existing = root.Find(objectName);
        GameObject lineObject = existing != null ? existing.gameObject : new GameObject(objectName);
        if (existing == null)
            lineObject.transform.SetParent(root, false);

        LineRenderer lineRenderer = lineObject.GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = lineObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.startWidth = pathLineWidth;
        lineRenderer.endWidth = pathLineWidth;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.sortingOrder = pathLineSortingOrder;
        lineRenderer.sortingLayerName = pathLineSortingLayer;
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        lineRenderer.material = new Material(shader);
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.enabled = false;
        return lineRenderer;
    }

    private void UpdatePathLine(LineRenderer lineRenderer, InputMapData bridgeData, List<Vector2Int> gridPath)
    {
        EnsurePathLineRenderers();
        if (lineRenderer == null || gridPath == null || gridPath.Count == 0)
            return;

        lineRenderer.positionCount = gridPath.Count;
        for (int i = 0; i < gridPath.Count; i++)
        {
            Vector3 world = BridgeMapJsonUtility.GridCellToWorld(bridgeData, gridPath[i].x, gridPath[i].y);
            world.z += pathLineZOffset;
            lineRenderer.SetPosition(i, world);
        }
    }

    private void SetPathLinesVisible(bool visible)
    {
        EnsurePathLineRenderers();
        if (_baselinePathLine != null)
            _baselinePathLine.enabled = visible && showPathLines;
        if (_dynamicPathLine != null)
            _dynamicPathLine.enabled = visible && showPathLines;
    }

    private static Vector2Int WorldToBridgeCell(InputMapData bridgeData, Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x - bridgeData.startX),
            Mathf.FloorToInt(worldPosition.y - bridgeData.startY));
    }

    private IEnumerator WaitForGeneratedEnemies()
    {
        float elapsed = 0f;
        while (elapsed < enemySpawnWaitTimeout)
        {
            Transform root = enemyRoot != null ? enemyRoot : FindGeneratedEnemiesRoot();
            if (root != null && root.childCount > 0)
            {
                Debug.Log($"AutoPathRunner [{mapId}]: waiting for enemies done ({root.childCount} enemies).");
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning($"AutoPathRunner [{mapId}]: enemy spawn wait timed out ({enemySpawnWaitTimeout}s).");
    }

    private void BuildBridgeEnemyAvoidance(
        InputMapData bridgeData,
        char[,] matrix,
        Vector2Int start,
        Vector2Int goal,
        out HashSet<Vector2Int> blockedCells,
        out Dictionary<Vector2Int, float> extraCosts)
    {
        blockedCells = new HashSet<Vector2Int>();
        extraCosts = new Dictionary<Vector2Int, float>();

        foreach (Transform enemy in GetEnemyTransforms())
        {
            Vector2Int enemyCell = BridgeMapJsonUtility.WorldToBridgeCell(bridgeData, enemy.position);
            AddEnemyAvoidanceCell(enemyCell, matrix, start, goal, blockedCells, extraCosts);
        }
    }

    private void BuildMapProviderEnemyAvoidance(
        char[,] matrix,
        Vector2Int start,
        Vector2Int goal,
        out HashSet<Vector2Int> blockedCells,
        out Dictionary<Vector2Int, float> extraCosts)
    {
        blockedCells = new HashSet<Vector2Int>();
        extraCosts = new Dictionary<Vector2Int, float>();

        if (mapProvider == null)
        {
            return;
        }

        foreach (Transform enemy in GetEnemyTransforms())
        {
            if (mapProvider.TryWorldToMatrixIndex(enemy.position, out Vector2Int cell))
            {
                AddEnemyAvoidanceCell(cell, matrix, start, goal, blockedCells, extraCosts);
            }
        }
    }

    private IEnumerable<Transform> GetEnemyTransforms()
    {
        if (!avoidGeneratedEnemies)
        {
            yield break;
        }

        Transform root = enemyRoot != null ? enemyRoot : FindGeneratedEnemiesRoot();
        if (root == null)
        {
            yield break;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null && child.gameObject.activeInHierarchy)
            {
                yield return child;
            }
        }
    }

    private Transform FindGeneratedEnemiesRoot()
    {
        if (string.IsNullOrWhiteSpace(generatedEnemiesRootName))
        {
            return null;
        }

        GameObject rootObject = GameObject.Find(generatedEnemiesRootName);
        return rootObject != null ? rootObject.transform : null;
    }

    private void AddEnemyAvoidanceCell(
        Vector2Int enemyCell,
        char[,] matrix,
        Vector2Int start,
        Vector2Int goal,
        HashSet<Vector2Int> blockedCells,
        Dictionary<Vector2Int, float> extraCosts)
    {
        if (!IsInside(matrix, enemyCell))
        {
            return;
        }

        int blockedRadius = Mathf.Max(0, enemyBlockedRadius);
        int avoidanceRadius = Mathf.Max(blockedRadius, enemyAvoidanceRadius);

        for (int y = -avoidanceRadius; y <= avoidanceRadius; y++)
        {
            for (int x = -avoidanceRadius; x <= avoidanceRadius; x++)
            {
                Vector2Int cell = new Vector2Int(enemyCell.x + x, enemyCell.y + y);
                if (!IsInside(matrix, cell) || cell == start || cell == goal)
                {
                    continue;
                }

                int distance = Mathf.Abs(x) + Mathf.Abs(y);
                if (distance > avoidanceRadius)
                {
                    continue;
                }

                if (distance <= blockedRadius)
                {
                    char tile = MapProfile.NormalizeCell(matrix[cell.y, cell.x]);
                    bool isPathTile = tile == 'p' || tile == 'P' || tile == 'S' || tile == 'E';
                    if (isPathTile)
                    {
                        blockedCells.Add(cell);
                    }

                    continue;
                }

                if (enemyAvoidanceCost <= 0f)
                {
                    continue;
                }

                float falloff = 1f - distance / (avoidanceRadius + 1f);
                float cost = enemyAvoidanceCost * Mathf.Max(0.1f, falloff);
                if (!extraCosts.TryGetValue(cell, out float existing) || cost > existing)
                {
                    extraCosts[cell] = cost;
                }
            }
        }
    }

    private IEnumerator ExecuteRunCoroutine()
    {
        char[,] structureMatrix = mapProvider.GetMapMatrix();
        char[,] visualMatrix = mapProvider.GetVisualMapMatrix();

        int matrixHeight = structureMatrix.GetLength(0);
        int matrixWidth = structureMatrix.GetLength(1);
        if (matrixHeight == 0 || matrixWidth == 0)
        {
            latestReport = BuildFailureReport(
                $"맵 매트릭스가 비어 있습니다. {mapProvider.gameObject.name} 하위 Tilemap이 있는지 확인하세요.");
            yield break;
        }

        char[,] pathMatrix = followVisualPath ? visualMatrix : structureMatrix;

        if (!TryResolveStartAndGoal(
                structureMatrix,
                pathMatrix,
                out Vector2Int start,
                out Vector2Int goal,
                out string resolveMessage))
        {
            latestReport = BuildFailureReport(resolveMessage);
            yield break;
        }

        Debug.Log($"AutoPathRunner [{mapId}]: start=({start.x},{start.y}), goal=({goal.x},{goal.y}) {resolveMessage}");

        HashSet<Vector2Int> blockedCells;
        Dictionary<Vector2Int, float> extraCosts;
        BuildMapProviderEnemyAvoidance(pathMatrix, start, goal, out blockedCells, out extraCosts);

        List<Vector2Int> path = FindPathAStar(pathMatrix, start, goal, pathTilesOnly, false, pathTileCost, grassTileCost, blockedCells, extraCosts);
        if ((path == null || path.Count == 0) && pathTilesOnly && allowGrassDetour)
        {
            path = FindPathAStar(pathMatrix, start, goal, false, true, pathTileCost, grassTileCost, blockedCells, extraCosts);
            if (path != null && path.Count > 0)
            {
                Debug.LogWarning($"AutoPathRunner [{mapId}]: path(p)가 끊겨 grass 우회 경로를 사용합니다.");
            }
        }

        if (path == null || path.Count == 0)
        {
            string tileHint = followVisualPath ? "path(p)" : "walkable(.)";
            latestReport = BuildFailureReport(
                $"S에서 G까지 {tileHint} 타일 경로를 찾지 못했습니다. 길이 이어져 있는지 Visual ASCII를 확인하세요.");
            yield break;
        }

        runnerTransform.position = mapProvider.MatrixIndexToWorldCenter(path[0].x, path[0].y);

        float elapsed = 0f;
        float worldDistance = 0f;
        int pathSteps = 0;
        int groundSteps = 0;
        int otherSteps = 0;

        for (int i = 1; i < path.Count; i++)
        {
            Vector2Int cell = path[i];
            char visualMarker = GetVisualMarker(visualMatrix, cell.x, cell.y);
            CountVisualMarker(visualMarker, ref pathSteps, ref groundSteps, ref otherSteps);

            Vector3 target = mapProvider.MatrixIndexToWorldCenter(cell.x, cell.y);
            Vector3 previous = runnerTransform.position;
            SetWalkingAnimation(true);
            UpdateFacing(target - runnerTransform.position);

            while (Vector3.Distance(runnerTransform.position, target) > 0.02f)
            {
                float step = moveSpeed * Time.deltaTime;
                runnerTransform.position = Vector3.MoveTowards(runnerTransform.position, target, step);
                elapsed += Time.deltaTime;
                yield return null;
            }

            runnerTransform.position = target;
            worldDistance += Vector3.Distance(previous, target);
        }

        SetWalkingAnimation(false);

        int totalSteps = Mathf.Max(1, path.Count - 1);
        latestReport = new PathRunReport
        {
            map_id = mapId,
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            success = true,
            elapsed_seconds = elapsed,
            world_distance = worldDistance,
            path_tile_steps = pathSteps,
            ground_tile_steps = groundSteps,
            other_tile_steps = otherSteps,
            total_tile_steps = totalSteps,
            average_speed = elapsed > 0f ? worldDistance / elapsed : 0f
        };
    }

    private void SetWalkingAnimation(bool isWalking)
    {
        if (!drivePlayerAnimator || _animator == null)
        {
            return;
        }

        _animator.SetBool("is_walking", isWalking);
    }

    private void UpdateFacing(Vector3 direction)
    {
        if (direction.x > 0.01f)
        {
            runnerTransform.eulerAngles = new Vector3(0f, 180f, 0f);
        }
        else if (direction.x < -0.01f)
        {
            runnerTransform.eulerAngles = Vector3.zero;
        }
    }

    private bool TryResolveStartAndGoal(
        char[,] structureMatrix,
        char[,] pathMatrix,
        out Vector2Int start,
        out Vector2Int goal,
        out string message)
    {
        message = string.Empty;
        start = default;
        goal = default;

        if (TryFindMarker(structureMatrix, 'S', out start))
        {
            message = "start from ASCII S";
        }
        else if (TryFindMarker(pathMatrix, 'S', out start))
        {
            message = "start from visual ASCII S";
        }
        else if (mapProvider.startPoint != null &&
                 mapProvider.TryWorldToMatrixIndex(mapProvider.startPoint.transform.position, out start))
        {
            message = "start from Start Point object";
        }
        else if (mapProvider.TryWorldToMatrixIndex(runnerTransform.position, out start))
        {
            message = "start from Player position";
        }
        else
        {
            start = default;
            message = "시작 위치를 찾지 못했습니다. Player를 길 위에 두거나 Start Point를 연결하세요.";
            return false;
        }

        if (!IsPathWalkable(pathMatrix, start, pathTilesOnly, allowGrassDetour) &&
            !IsStructureWalkable(structureMatrix, start))
        {
            if (!TryFindNearestPathCell(pathMatrix, start, pathTilesOnly, allowGrassDetour, out start))
            {
                message = "Start 위치가 path(p) 타일이 아닙니다. Start_point를 갈색 길 위에 놓아 주세요.";
                return false;
            }

            message += " (snapped to nearest path)";
        }

        if (TryFindMarker(structureMatrix, 'G', out goal))
        {
            message += ", goal from ASCII G";
            return ValidateGoal(pathMatrix, ref goal, ref message);
        }

        if (TryFindMarker(pathMatrix, 'G', out goal))
        {
            message += ", goal from visual ASCII G";
            return ValidateGoal(pathMatrix, ref goal, ref message);
        }

        if (mapProvider.goalPoint != null &&
            mapProvider.TryWorldToMatrixIndex(mapProvider.goalPoint.transform.position, out goal))
        {
            message += ", goal from Goal Point object";
            return ValidateGoal(pathMatrix, ref goal, ref message);
        }

        if (!autoGoalWhenMissing)
        {
            message = "Goal Point가 없습니다. TilemapDataProvider에 Goal Point를 연결하거나 Auto Goal When Missing을 켜 주세요.";
            return false;
        }

        goal = FindFarthestPathCell(pathMatrix, start, pathTilesOnly, allowGrassDetour);
        if (goal == start)
        {
            message = "자동 Goal을 찾지 못했습니다. 맵에 path(p) 타일이 없습니다.";
            return false;
        }

        message += ", goal auto (farthest path tile)";
        return true;
    }

    private bool ValidateGoal(char[,] pathMatrix, ref Vector2Int goal, ref string message)
    {
        if (IsPathWalkable(pathMatrix, goal, pathTilesOnly, allowGrassDetour))
        {
            return true;
        }

        if (TryFindNearestPathCell(pathMatrix, goal, pathTilesOnly, allowGrassDetour, out Vector2Int snapped))
        {
            goal = snapped;
            message += " (goal snapped to nearest path)";
            return true;
        }

        message = "Goal 위치가 path(p) 타일이 아닙니다. End_point를 갈색 길 위에 놓아 주세요.";
        return false;
    }

    private static bool TryFindNearestPathCell(
        char[,] matrix,
        Vector2Int origin,
        bool pathOnly,
        bool allowGrass,
        out Vector2Int result)
    {
        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);
        bool[,] visited = new bool[height, width];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(origin);
        visited[origin.y, origin.x] = true;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (IsPathWalkable(matrix, current, pathOnly, allowGrass))
            {
                result = current;
                return true;
            }

            foreach (Vector2Int direction in FourDirections)
            {
                Vector2Int next = current + direction;
                if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height || visited[next.y, next.x])
                {
                    continue;
                }

                char cell = MapProfile.NormalizeCell(matrix[next.y, next.x]);
                if (cell == '#' || cell == 'w' || cell == ' ')
                {
                    continue;
                }

                visited[next.y, next.x] = true;
                queue.Enqueue(next);
            }
        }

        result = origin;
        return false;
    }

    private static Vector2Int FindFarthestPathCell(
        char[,] matrix,
        Vector2Int start,
        bool pathOnly,
        bool allowGrass)
    {
        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);
        bool[,] visited = new bool[height, width];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Queue<int> distances = new Queue<int>();
        Vector2Int farthest = start;
        int maxDistance = 0;

        if (!IsInside(matrix, start))
        {
            return start;
        }

        queue.Enqueue(start);
        distances.Enqueue(0);
        visited[start.y, start.x] = true;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int distance = distances.Dequeue();

            if (IsPathWalkable(matrix, current, pathOnly, allowGrass) && distance >= maxDistance)
            {
                maxDistance = distance;
                farthest = current;
            }

            foreach (Vector2Int direction in FourDirections)
            {
                Vector2Int next = current + direction;
                if (!IsInside(matrix, next) || visited[next.y, next.x])
                {
                    continue;
                }

                if (!IsPathWalkable(matrix, next, pathOnly, allowGrass))
                {
                    continue;
                }

                visited[next.y, next.x] = true;
                queue.Enqueue(next);
                distances.Enqueue(distance + 1);
            }
        }

        return farthest;
    }

    private static bool IsInside(char[,] matrix, Vector2Int point)
    {
        return point.x >= 0 && point.x < matrix.GetLength(1) && point.y >= 0 && point.y < matrix.GetLength(0);
    }

    private static readonly Vector2Int[] FourDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private PathRunReport BuildFailureReport(string reason)
    {
        Debug.LogWarning($"AutoPathRunner [{mapId}]: {reason}");
        return new PathRunReport
        {
            map_id = mapId,
            created_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            success = false,
            failure_reason = reason
        };
    }

    private static bool TryFindMarker(char[,] matrix, char marker, out Vector2Int position)
    {
        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (MapProfile.NormalizeCell(matrix[y, x]) == marker)
                {
                    position = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        position = default;
        return false;
    }

    private static List<Vector2Int> FindPathAStar(
        char[,] matrix,
        Vector2Int start,
        Vector2Int goal,
        bool pathOnly,
        bool allowGrass,
        float pathCost,
        float grassCost,
        HashSet<Vector2Int> blockedCells = null,
        Dictionary<Vector2Int, float> extraCosts = null)
    {
        int height = matrix.GetLength(0);
        int width = matrix.GetLength(1);
        var openSet = new List<AStarNode>();
        var closedSet = new HashSet<Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        gScore[start] = 0f;
        openSet.Add(CreateAStarNode(start, 0f, Heuristic(start, goal)));

        while (openSet.Count > 0)
        {
            openSet.Sort((a, b) => a.f.CompareTo(b.f));
            AStarNode current = openSet[0];
            openSet.RemoveAt(0);

            if (current.position == goal)
            {
                return ReconstructPath(cameFrom, start, goal);
            }

            if (closedSet.Contains(current.position))
            {
                continue;
            }

            closedSet.Add(current.position);

            foreach (Vector2Int direction in FourDirections)
            {
                Vector2Int next = current.position + direction;
                if (!IsInside(matrix, next))
                {
                    continue;
                }

                if (!IsPathWalkable(matrix, next, pathOnly, allowGrass))
                {
                    continue;
                }

                if (blockedCells != null && blockedCells.Contains(next))
                {
                    continue;
                }

                char cell = MapProfile.NormalizeCell(matrix[next.y, next.x]);
                float stepCost = GetTileMoveCost(cell, pathCost, grassCost);
                if (extraCosts != null && extraCosts.TryGetValue(next, out float extraCost))
                {
                    stepCost += extraCost;
                }

                float tentativeG = current.g + stepCost;

                if (gScore.TryGetValue(next, out float knownG) && tentativeG >= knownG)
                {
                    continue;
                }

                gScore[next] = tentativeG;
                cameFrom[next] = current.position;
                openSet.Add(CreateAStarNode(next, tentativeG, Heuristic(next, goal)));
            }
        }

        return null;
    }

    private struct AStarNode
    {
        public Vector2Int position;
        public float g;
        public float f;
    }

    private static AStarNode CreateAStarNode(Vector2Int position, float g, float heuristic)
    {
        return new AStarNode
        {
            position = position,
            g = g,
            f = g + heuristic
        };
    }

    private static float Heuristic(Vector2Int from, Vector2Int to)
    {
        return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
    }

    private static float GetTileMoveCost(char cell, float pathCost, float grassCost)
    {
        cell = MapProfile.NormalizeCell(cell);
        if (cell == 'p' || cell == 'P' || cell == 'S' || cell == 'E')
        {
            return pathCost;
        }

        if (cell == 'G' || cell == 'g')
        {
            return grassCost;
        }

        return grassCost;
    }

    private static bool IsPathWalkable(char[,] matrix, Vector2Int point, bool pathOnly, bool allowGrass)
    {
        if (!IsInside(matrix, point))
        {
            return false;
        }

        return IsPathWalkableCell(MapProfile.NormalizeCell(matrix[point.y, point.x]), pathOnly, allowGrass);
    }

    private static bool IsPathWalkableCell(char cell, bool pathOnly, bool allowGrass)
    {
        if (cell == '#' || cell == 'w' || cell == 'O' || cell == ' ')
        {
            return false;
        }

        if (cell == 'p' || cell == 'P' || cell == 'S' || cell == 'E')
        {
            return true;
        }

        if (!pathOnly && cell == 'G')
        {
            return true;
        }

        if (!pathOnly && allowGrass)
        {
            return cell == 'g' || cell == 's' || cell == '.' || cell == '?';
        }

        return false;
    }

    private static bool IsStructureWalkable(char[,] matrix, Vector2Int point)
    {
        if (!IsInside(matrix, point))
        {
            return false;
        }

        return IsStructureWalkableCell(MapProfile.NormalizeCell(matrix[point.y, point.x]));
    }

    private static bool IsStructureWalkableCell(char cell)
    {
        return cell == '.' || cell == 'S' || cell == 'G' || cell == '?';
    }

    private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> parents, Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int current = goal;

        while (true)
        {
            path.Add(current);
            if (current == start)
            {
                break;
            }

            current = parents[current];
        }

        path.Reverse();
        return path;
    }

    private static char GetVisualMarker(char[,] visualMatrix, int x, int y)
    {
        if (visualMatrix.GetLength(0) == 0 || visualMatrix.GetLength(1) == 0)
        {
            return '.';
        }

        if (x < 0 || y < 0 || x >= visualMatrix.GetLength(1) || y >= visualMatrix.GetLength(0))
        {
            return '?';
        }

        return visualMatrix[y, x];
    }

    private static void CountVisualMarker(char marker, ref int pathSteps, ref int groundSteps, ref int otherSteps)
    {
        switch (marker)
        {
            case 'p':
                pathSteps++;
                break;
            case 'g':
                groundSteps++;
                break;
            default:
                otherSteps++;
                break;
        }
    }

    private void LogReport(PathRunReport report)
    {
        if (!report.success)
        {
            Debug.LogWarning($"[AutoPathRunner] {report.map_id} FAILED: {report.failure_reason}");
            return;
        }

        Debug.Log(
            "[AutoPathRunner]\n" +
            $"map={report.map_id}\n" +
            $"time={report.elapsed_seconds:0.00}s\n" +
            $"distance={report.world_distance:0.00}\n" +
            $"avg_speed={report.average_speed:0.00}\n" +
            $"steps: path(p)={report.path_tile_steps}, ground(g)={report.ground_tile_steps}, other={report.other_tile_steps}, total={report.total_tile_steps}" +
            (report.has_baseline_comparison
                ? $"\n--- baseline (path only) ---\n" +
                  $"baseline_steps={report.baseline_total_tile_steps}, baseline_distance={report.baseline_world_distance:0.00}\n" +
                  $"baseline_est={report.baseline_estimated_seconds:0.00}s, delta_time=+{report.elapsed_delta_seconds:0.00}s, delta_dist=+{report.distance_delta:0.00}\n" +
                  $"enemies={report.enemy_count}, replans={report.replan_count}"
                : string.Empty));
    }

    private void ExportReport(PathRunReport report)
    {
        string runId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string directory = Path.Combine(Directory.GetCurrentDirectory(), outputRoot, runId);
        Directory.CreateDirectory(directory);

        string jsonPath = Path.Combine(directory, $"{report.map_id}_path_run.json");
        string csvPath = Path.Combine(directory, $"{report.map_id}_path_run.csv");
        string comparisonPath = Path.Combine(directory, $"{report.map_id}_path_comparison.json");

        File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true), Encoding.UTF8);
        File.WriteAllText(csvPath, BuildCsv(report), Encoding.UTF8);
        if (report.has_baseline_comparison)
            File.WriteAllText(comparisonPath, BuildComparisonJson(report), Encoding.UTF8);

        report.report_json_path = jsonPath;
        report.report_csv_path = csvPath;

        if (report.has_baseline_comparison)
            ExportSummaryTable(report, directory);

        Debug.Log(
            $"AutoPathRunner report exported:\n{jsonPath}\n{csvPath}" +
            (report.has_baseline_comparison ? $"\n{comparisonPath}" : string.Empty));
    }

    private void ExportSummaryTable(PathRunReport report, string directory)
    {
        string tablePath = Path.Combine(directory, $"{report.map_id}_path_summary.md");
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# Bridge Path Run Summary");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Baseline (path-only A*) | Actual (enemy avoid) | Delta |");
        sb.AppendLine($"|---|---:|---:|---:|");
        sb.AppendLine($"| Difficulty | {report.difficulty_level} | {report.difficulty_level} | |");
        sb.AppendLine($"| Time (s) | {report.baseline_estimated_seconds:0.00} | {report.elapsed_seconds:0.00} | +{report.elapsed_delta_seconds:0.00} |");
        sb.AppendLine($"| Route length | {report.baseline_world_distance:0.00} | {report.world_distance:0.00} | +{report.distance_delta:0.00} |");
        sb.AppendLine($"| Tile steps | {report.baseline_total_tile_steps} | {report.total_tile_steps} | +{report.total_tile_steps - report.baseline_total_tile_steps} |");
        sb.AppendLine($"| Grass detours | 0 | {report.ground_tile_steps} | +{report.ground_tile_steps} |");
        sb.AppendLine($"| Enemies | 0 | {report.enemy_count} | +{report.enemy_count} |");
        sb.AppendLine($"| Replans | 0 | {report.replan_count} | +{report.replan_count} |");
        sb.AppendLine();
        sb.AppendLine("- Red line = baseline path-only A*");
        sb.AppendLine("- Cyan line = dynamic enemy-avoidance A*");

        File.WriteAllText(tablePath, sb.ToString(), Encoding.UTF8);
        Debug.Log($"AutoPathRunner summary table exported:\n{tablePath}");
    }

    private static string BuildComparisonJson(PathRunReport report)
    {
        return JsonUtility.ToJson(new PathRunComparisonExport
        {
            map_id = report.map_id,
            created_at = report.created_at,
            baseline_label = "path_only_astar",
            dynamic_label = "enemy_avoidance_astar",
            baseline_total_tile_steps = report.baseline_total_tile_steps,
            baseline_world_distance = report.baseline_world_distance,
            baseline_estimated_seconds = report.baseline_estimated_seconds,
            actual_elapsed_seconds = report.elapsed_seconds,
            actual_world_distance = report.world_distance,
            elapsed_delta_seconds = report.elapsed_delta_seconds,
            distance_delta = report.distance_delta,
            enemy_count = report.enemy_count,
            replan_count = report.replan_count,
            ground_tile_steps = report.ground_tile_steps,
            difficulty_level = report.difficulty_level
        }, true);
    }

    [Serializable]
    private class PathRunComparisonExport
    {
        public string map_id;
        public string created_at;
        public string baseline_label;
        public string dynamic_label;
        public int baseline_total_tile_steps;
        public float baseline_world_distance;
        public float baseline_estimated_seconds;
        public float actual_elapsed_seconds;
        public float actual_world_distance;
        public float elapsed_delta_seconds;
        public float distance_delta;
        public int enemy_count;
        public int replan_count;
        public int ground_tile_steps;
        public int difficulty_level;
    }

    private static string BuildCsv(PathRunReport report)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("map_id,success,failure_reason,elapsed_seconds,world_distance,average_speed,path_tile_steps,ground_tile_steps,other_tile_steps,total_tile_steps,baseline_total_tile_steps,baseline_world_distance,baseline_estimated_seconds,elapsed_delta_seconds,distance_delta,enemy_count,replan_count");
        sb.AppendLine(string.Join(",",
            report.map_id,
            report.success,
            EscapeCsv(report.failure_reason),
            report.elapsed_seconds.ToString("0.###"),
            report.world_distance.ToString("0.###"),
            report.average_speed.ToString("0.###"),
            report.path_tile_steps,
            report.ground_tile_steps,
            report.other_tile_steps,
            report.total_tile_steps,
            report.baseline_total_tile_steps,
            report.baseline_world_distance.ToString("0.###"),
            report.baseline_estimated_seconds.ToString("0.###"),
            report.elapsed_delta_seconds.ToString("0.###"),
            report.distance_delta.ToString("0.###"),
            report.enemy_count,
            report.replan_count));
        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = distance <= radius ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
