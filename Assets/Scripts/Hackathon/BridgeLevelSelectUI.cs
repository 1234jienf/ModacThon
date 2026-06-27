using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BridgeLevelSelectUI : MonoBehaviour
{
    private const string CanvasName = "BridgeLevelSelectCanvas";

    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private TextMeshProUGUI descriptionText;

    private void Awake()
    {
        EnsureEventSystem();
        EnsureUi();
    }

    private void Start()
    {
        EnsureEventSystem();
        ShowPanel();

        if (BridgeGameSession.Instance != null && BridgeGameSession.Instance.IsStarted)
            Hide();
    }

    private void ShowPanel()
    {
        if (panelGroup == null)
            return;

        panelGroup.alpha = 1f;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;
    }

    private void Update()
    {
        if (panelGroup == null || !panelGroup.blocksRaycasts)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            OnLevelSelected(1, "Fewer rooms, fewer enemies, lakes away from path.");
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            OnLevelSelected(2, "Balanced rooms, enemies, and lakes near path.");
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            OnLevelSelected(3, "More rooms, 300 enemies, lakes overlapping path.");
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            eventSystem = FindObjectOfType<EventSystem>();

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<StandaloneInputModule>() == null &&
            eventSystem.GetComponent<BaseInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }

        eventSystem.enabled = true;
    }

    public void Hide()
    {
        if (panelGroup == null)
            return;

        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;
    }

    private void EnsureUi()
    {
        if (panelGroup != null)
            return;

        Canvas canvas = null;
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas existing in canvases)
        {
            if (existing != null && existing.gameObject.name == CanvasName)
            {
                canvas = existing;
                break;
            }
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(CanvasName);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99999;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
        }
        else if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        GameObject panel = new GameObject("LevelSelectPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.72f);

        panelGroup = panel.AddComponent<CanvasGroup>();
        panelGroup.alpha = 1f;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;

        GameObject box = new GameObject("LevelSelectBox");
        box.transform.SetParent(panel.transform, false);
        RectTransform boxRect = box.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(520f, 360f);

        Image boxImage = box.AddComponent<Image>();
        boxImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        GameObject titleObject = new GameObject("Title");
        titleObject.transform.SetParent(box.transform, false);
        RectTransform titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(460f, 48f);

        TextMeshProUGUI title = titleObject.AddComponent<TextMeshProUGUI>();
        title.text = "Select Difficulty";
        title.fontSize = 34f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;
        title.raycastTarget = false;

        GameObject descObject = new GameObject("Description");
        descObject.transform.SetParent(box.transform, false);
        RectTransform descRect = descObject.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.5f, 1f);
        descRect.anchorMax = new Vector2(0.5f, 1f);
        descRect.pivot = new Vector2(0.5f, 1f);
        descRect.anchoredPosition = new Vector2(0f, -78f);
        descRect.sizeDelta = new Vector2(460f, 72f);

        descriptionText = descObject.AddComponent<TextMeshProUGUI>();
        descriptionText.text = "Choose a level, then bridge generation and path run will start.\n(Or press 1 / 2 / 3)";
        descriptionText.fontSize = 18f;
        descriptionText.alignment = TextAlignmentOptions.Center;
        descriptionText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        descriptionText.raycastTarget = false;

        CreateLevelButton(box.transform, "Level 1  Easy", new Vector2(0f, 40f), 1,
            "Fewer rooms, fewer enemies, lakes away from path.");
        CreateLevelButton(box.transform, "Level 2  Normal", new Vector2(0f, -40f), 2,
            "Balanced rooms, enemies, and lakes near path.");
        CreateLevelButton(box.transform, "Level 3  Hard", new Vector2(0f, -120f), 3,
            "More rooms, 300 enemies, lakes overlapping path.");
    }

    private void CreateLevelButton(Transform parent, string label, Vector2 anchoredPosition, int level, string detail)
    {
        GameObject buttonObject = new GameObject($"Level{level}Button");
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(420f, 56f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = level == 2 ? new Color(0.25f, 0.55f, 0.95f, 1f) : new Color(0.22f, 0.22f, 0.22f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        int selectedLevel = level;
        button.onClick.AddListener(() => OnLevelSelected(selectedLevel, detail));

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private void OnLevelSelected(int level, string detail)
    {
        Debug.Log($"BridgeLevelSelectUI: Level {level} selected");

        if (descriptionText != null)
            descriptionText.text = detail;

        if (BridgeGameSession.Instance != null)
            BridgeGameSession.Instance.StartSession(level);
        else
            Debug.LogError("BridgeLevelSelectUI: BridgeGameSession.Instance is null");

        Hide();
    }
}
