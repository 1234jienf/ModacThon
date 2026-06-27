using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class BridgeMapOverviewUI : MonoBehaviour
{
    public static BridgeMapOverviewUI Instance { get; private set; }

    [SerializeField] private string mapJsonRelativePath = "tmpOutput/BridgeMapData_Path.json";
    [SerializeField] private Vector2 panelSize = new Vector2(580f, 440f);
    [SerializeField] private Vector2 panelMargin = new Vector2(12f, -12f);

    private RawImage mapImage;
    private RectTransform mapImageRect;
    private RectTransform playerMarker;
    private Texture2D mapTexture;
    private InputMapData mapData;
    private char[,] grid;
    private Transform playerTransform;

    private void Awake()
    {
        Instance = this;
        EnsureUi();
    }

    private void LateUpdate()
    {
        UpdatePlayerMarker();
    }

    public void RefreshFromLatestMap()
    {
        if (!BridgeMapJsonUtility.TryLoadFromFile(mapJsonRelativePath, out mapData, out grid))
            return;

        BuildMapTexture();
        if (mapImage != null)
            mapImage.texture = mapTexture;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }
    }

    private void EnsureUi()
    {
        if (mapImage != null)
            return;

        Canvas canvas = BridgeUiFactory.GetOrCreateCanvas("BridgeHudCanvas", 8500);

        GameObject panel = new GameObject("MapOverviewPanel");
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = panelMargin;
        panelRect.sizeDelta = panelSize;

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject titleObject = new GameObject("Title");
        titleObject.transform.SetParent(panel.transform, false);
        RectTransform titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -8f);
        titleRect.sizeDelta = new Vector2(-16f, 28f);

        Text title = titleObject.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.text = "Full Map  (# wall  P path  G grass  blue=lake  S/E)";
        title.fontSize = 20;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.raycastTarget = false;

        GameObject imageObject = new GameObject("MapImage");
        imageObject.transform.SetParent(panel.transform, false);
        mapImageRect = imageObject.AddComponent<RectTransform>();
        mapImageRect.anchorMin = new Vector2(0f, 0f);
        mapImageRect.anchorMax = new Vector2(1f, 1f);
        mapImageRect.offsetMin = new Vector2(10f, 10f);
        mapImageRect.offsetMax = new Vector2(-10f, -40f);

        mapImage = imageObject.AddComponent<RawImage>();
        mapImage.color = Color.white;

        AspectRatioFitter fitter = imageObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1f;

        GameObject markerObject = new GameObject("PlayerMarker");
        markerObject.transform.SetParent(mapImageRect, false);
        playerMarker = markerObject.AddComponent<RectTransform>();
        playerMarker.sizeDelta = new Vector2(8f, 8f);
        playerMarker.anchorMin = Vector2.zero;
        playerMarker.anchorMax = Vector2.zero;
        playerMarker.pivot = new Vector2(0.5f, 0.5f);

        Image markerImage = markerObject.AddComponent<Image>();
        markerImage.color = new Color(1f, 0.92f, 0.2f, 1f);
        markerImage.raycastTarget = false;
    }

    private void BuildMapTexture()
    {
        int height = grid.GetLength(0);
        int width = grid.GetLength(1);
        if (width <= 0 || height <= 0)
            return;

        if (mapTexture != null)
            Destroy(mapTexture);

        mapTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        mapTexture.filterMode = FilterMode.Point;
        mapTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // gridY=0 is south (low world Y) → texture y=0 is UI bottom
                mapTexture.SetPixel(x, y, ColorForCell(grid[y, x]));
            }
        }

        mapTexture.Apply();

        if (mapImageRect != null)
        {
            AspectRatioFitter fitter = mapImageRect.GetComponent<AspectRatioFitter>();
            if (fitter != null)
                fitter.aspectRatio = width / (float)height;
        }
    }

    private static Color ColorForCell(char tile)
    {
        switch (tile)
        {
            case '#':
                return new Color(0.12f, 0.12f, 0.12f, 1f);
            case 'P':
            case 'p':
                return new Color(0.72f, 0.72f, 0.72f, 1f);
            case 'S':
                return new Color(0.2f, 0.9f, 0.35f, 1f);
            case 'E':
                return new Color(0.95f, 0.25f, 0.25f, 1f);
            case 'w':
            case 'W':
                return new Color(0.2f, 0.45f, 0.95f, 1f);
            case 'G':
            case 'g':
                return new Color(0.45f, 0.72f, 0.38f, 1f);
            case '.':
                return new Color(0.92f, 0.92f, 0.92f, 1f);
            default:
                return new Color(0.55f, 0.55f, 0.55f, 1f);
        }
    }

    private void UpdatePlayerMarker()
    {
        if (playerMarker == null || mapData == null || mapImageRect == null)
            return;

        if (playerTransform == null)
        {
            playerMarker.gameObject.SetActive(false);
            return;
        }

        int gridX = Mathf.FloorToInt(playerTransform.position.x - mapData.startX);
        int gridY = Mathf.FloorToInt(playerTransform.position.y - mapData.startY);
        int width = grid.GetLength(1);
        int height = grid.GetLength(0);

        if (gridX < 0 || gridY < 0 || gridX >= width || gridY >= height)
        {
            playerMarker.gameObject.SetActive(false);
            return;
        }

        playerMarker.gameObject.SetActive(true);
        float normalizedX = (gridX + 0.5f) / width;
        float normalizedY = (gridY + 0.5f) / height;
        playerMarker.anchorMin = new Vector2(normalizedX, normalizedY);
        playerMarker.anchorMax = new Vector2(normalizedX, normalizedY);
        playerMarker.anchoredPosition = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (mapTexture != null)
            Destroy(mapTexture);
    }
}
