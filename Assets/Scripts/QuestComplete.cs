using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuestComplete : MonoBehaviour
{
    int CurQuestSuccess;
    public Dictionary<string, int> SoldFood = new Dictionary<string, int>();
    MenuManager menuManager;
    bool[] QuestCompleted = {false, false, false, false, false};

    void Start() {
        CurQuestSuccess = GameMgr.Instance.questSuccess;
    }

    // Update is called once per frame
    public void QuestSceneChange() {
        for(int i = 0; i < SoldFood.Count; i++) {
            Debug.Log(SoldFood.Keys.ToList()[i]);
            Debug.Log(SoldFood.Values.ToList()[i]);
        }

        for (int i = 0; i < CurQuestSuccess; ++i) {
            QuestCompleted[i] = true;
        }
        
        if (CurQuestSuccess == 0 && !QuestCompleted[CurQuestSuccess] && SoldFood.ContainsKey("야채죽") && SoldFood.ContainsKey("생선구이(초급)") && 
            SoldFood["야채죽"] >= 1 && SoldFood["생선구이(초급)"] >= 1) {
            QuestCompleted[CurQuestSuccess] = true;
            CurQuestSuccess++;

            // 데이터 저장 로직
            GameMgr.Instance.questSuccess = CurQuestSuccess;

            SceneManager.LoadScene("Quest1_complete");
        }

        else if (CurQuestSuccess == 1 && !QuestCompleted[CurQuestSuccess] && SoldFood.ContainsKey("불고기") && SoldFood.ContainsKey("비빔밥") && 
            SoldFood["불고기"] >= 1 && SoldFood["비빔밥"] >= 1) {
            QuestCompleted[CurQuestSuccess] = true;
            CurQuestSuccess++;

            // 데이터 저장 로직
            GameMgr.Instance.questSuccess = CurQuestSuccess;

            SceneManager.LoadScene("Quest2_complete");
        }

        else if (CurQuestSuccess == 2 && !QuestCompleted[CurQuestSuccess] && SoldFood.ContainsKey("잡채") && SoldFood.ContainsKey("라면") && 
            SoldFood["잡채"] >= 1 && SoldFood["라면"] >= 1) {
            QuestCompleted[CurQuestSuccess] = true;
            CurQuestSuccess++;

            // 데이터 저장 로직
            GameMgr.Instance.questSuccess = CurQuestSuccess;

            SceneManager.LoadScene("Quest3_complete");
        }

        else if (CurQuestSuccess == 3 && !QuestCompleted[CurQuestSuccess] && SoldFood.ContainsKey("삼계탕") && SoldFood.ContainsKey("찜닭") && SoldFood.ContainsKey("해물파전") &&  
            SoldFood["삼계탕"] >= 1 && SoldFood["찜닭"] >= 1 && SoldFood["해물파전"] >= 1) {
            QuestCompleted[CurQuestSuccess] = true;
            CurQuestSuccess++;

            // 데이터 저장 로직
            GameMgr.Instance.questSuccess = CurQuestSuccess;

            SceneManager.LoadScene("Quest4_complete");
        }

        else if ((CurQuestSuccess == 4 && !QuestCompleted[CurQuestSuccess] && SoldFood.ContainsKey("타락죽") && SoldFood.ContainsKey("구절판") && SoldFood.ContainsKey("신선로") && SoldFood.ContainsKey("궁중떡볶이") && 
            SoldFood["타락죽"] >= 1 && SoldFood["구절판"] >= 1 && SoldFood["신선로"] >= 1 && SoldFood["궁중떡볶이"] >= 1) || CurQuestSuccess == 5) {
            QuestCompleted[CurQuestSuccess] = true;
            
            if (CurQuestSuccess < 5){
                CurQuestSuccess++;
            }

            // 데이터 저장 로직
            GameMgr.Instance.questSuccess = CurQuestSuccess;

            SceneManager.LoadScene("Quest5_complete");
        }

        else {
            SceneManager.LoadScene("Main");
        }
    }
}
