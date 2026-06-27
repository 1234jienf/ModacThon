using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BridgeDifficultyComparisonUI : MonoBehaviour
{
    private static BridgeDifficultyComparisonUI instance;

    [SerializeField] private TextMeshProUGUI tableText;
    [SerializeField] private Vector2 panelSize = new Vector2(760f, 250f);

    private void Awake()
    {
        instance = this;
        EnsureUi();
        RefreshTable();
    }

    public static void RefreshTable()
    {
        if (instance == null || instance.tableText == null)
            return;

        instance.tableText.text = BridgeDifficultyResultsTracker.BuildUiTableTextFromExportedCsv();
    }

    private void EnsureUi()
    {
        if (tableText != null)
            return;

        Canvas canvas = BridgeUiFactory.GetOrCreateCanvas("BridgeHudCanvas", 8500);

        GameObject panel = new GameObject("DifficultyComparisonPanel");
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-12f, 12f);
        panelRect.sizeDelta = panelSize;

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.68f);

        GameObject textObject = new GameObject("ComparisonText");
        textObject.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 12f);
        textRect.offsetMax = new Vector2(-12f, -12f);

        tableText = textObject.AddComponent<TextMeshProUGUI>();
        tableText.fontSize = 16f;
        tableText.color = Color.white;
        tableText.alignment = TextAlignmentOptions.TopLeft;
        tableText.richText = true;
    }
}
