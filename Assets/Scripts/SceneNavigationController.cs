using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SceneNavigationController : MonoBehaviour
{
    public TextMeshProUGUI warningText; // 경고 메시지를 표시할 텍스트

    // 주막 버튼 이벤트
    public void OnTavernButtonClicked()
    {
        GameMgr gameMgr = GameMgr.Instance;
        if (gameMgr == null) {
            Debug.LogError("GameMgr.Instance is null. 씬에 활성화된 GameManager 오브젝트와 GameMgr 컴포넌트가 있는지 확인하세요.");
            return;
        }

        if (gameMgr.time >= 0 && gameMgr.time < 12) // 낮 시간인 경우
        {
            warningText.text = "현재는 주막 운영 시간이 아닙니다.";
        }
        else
        {
            warningText.text = "";
            SceneManager.LoadScene("Restraunt"); // 주막 씬으로 이동
        }
    }

    // 사냥터 버튼 이벤트
    public void OnHuntingGroundButtonClicked()
    {
        GameMgr gameMgr = GameMgr.Instance;
        if (gameMgr == null) {
            Debug.LogError("GameMgr.Instance is null. 씬에 활성화된 GameManager 오브젝트와 GameMgr 컴포넌트가 있는지 확인하세요.");
            return;
        }

        if (gameMgr.time >= 12 && gameMgr.time <= 24) // 밤 시간인 경우
        {
            warningText.text = "현재는 호랑이 출몰 시간입니다. 사냥터에 들어갈 수 없습니다.";
        }
        else
        {
            warningText.text = "";
            if (gameMgr.inventoryControl != null && gameMgr.inventoryControl.draggable != null) {
                gameMgr.inventoryControl.draggable.resetPos();
            }
            SceneManager.LoadScene("Hunting Ground"); // 사냥터 씬으로 이동
        }
    }
}
