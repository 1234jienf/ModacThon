using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RestaurantTimer : MonoBehaviour
{
    private GameMgr gameMgr;
    
    [HideInInspector]
    public float time; // 시간 (초 단위)

    public float limit; // 최대 사냥 가능 시간 (limit sec == 12시간)

    private bool isPlay; // 타이머 재생 여부
    private bool timeOver; // 시간 초과 여부

    [SerializeField]
    private BusinessController businessController; // usinessController

    private void Awake() {
        gameMgr = GameMgr.Instance;
    }

    void Start()
    {
        // time 초기화
        // time = (gameMgr.time - 12f) / 12f * limit;
        Reset();
        timeOver = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(isPlay && !timeOver) {
            // 시간을 늘린다.
            time += Time.deltaTime;
            gameMgr.time = 12f + time / limit * 12f;

            // 사냥 제한 시간을 넘기면
            if(time >= limit) {
                timeOver = true;
                time = limit;
                gameMgr.time = 0f;
                businessController.ToggleBusinessStatus();
            }
        }
    }

    // 타이머 재생
    public void Play() {
        isPlay = true;
        Time.timeScale = 1f;
    }

    // 타이머 일시정지
    public void Pause() {
        isPlay = false;
        Time.timeScale = 0f;
    }

    // 타이머 초기화
    public void Reset() {
        // 재생 중지
        isPlay = false;

        // 0초로 초기화
        time = 0f;
        gameMgr.time = 12f;
    }

    // 타이머 강제 완료
    public void ForcedFinish() {
        time = limit;
        gameMgr.time = 0f;
        timeOver = true;
    }
}
