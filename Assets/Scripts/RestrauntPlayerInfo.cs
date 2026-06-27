using UnityEngine;
using TMPro;

public class RestrauntPlayerInfo : MonoBehaviour
{
    public TextMesh levelText;
    public TextMesh questSuccess;
    public TextMeshProUGUI level;
    public TextMeshProUGUI money;

    void Start()
    {
        UpdateLevelDisplay();
    }

    void UpdateLevelDisplay()
    {
        if (GameMgr.Instance != null)
        {
            Debug.Log($"현재 username: {GameMgr.Instance.username}");

            string levelName = GetLevelName(GameMgr.Instance.questSuccess);
            string formattedUsername = $"[{GameMgr.Instance.username}]";

            levelText.text = $"{levelName}";
            questSuccess.text = formattedUsername;
            level.text = $"LV.{GameMgr.Instance.lv}";
            money.text = $"보유 자산 : {GameMgr.Instance.inventoryControl.money}냥";
        }
        else
        {
            Debug.LogError("GameMgr 인스턴스를 찾을 수 없습니다.");
        }
    }

    private string GetLevelName(int questSuccess)
    {
        switch (questSuccess)
        {
            case 0: return "견습생";
            case 1: return "초급요리사";
            case 2: return "중급요리사";
            case 3: return "고급요리사";
            case 4: return "궁중요리사";
            default: return "알 수 없는 레벨";
        }
    }

}
