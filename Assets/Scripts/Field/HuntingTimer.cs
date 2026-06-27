using System.Collections;
using System.Collections.Generic;
// using SuperTiled2Unity;
using TMPro;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class HuntingTimer : MonoBehaviour
{
    private GameMgr gameMgr;
    private bool gameMgrWarningLogged;

    [HideInInspector]
    public float time; // 시간 (초 단위)

    public float limit; // 최대 사냥 가능 시간 (limit sec == 12시간)

    private bool isPlay; // 타이머 재생 여부
    private bool timeOver; // 시간 초과 여부

    [SerializeField]
    private HuntingManager huntingManager; // HuntingManager

    private void Awake() {
        gameMgr = FindObjectOfType<GameMgr>();
    }

    private bool TrySetGameMgr()
    {
        if (gameMgr == null)
        {
            gameMgr = FindObjectOfType<GameMgr>();
        }

        if (gameMgr == null)
        {
            if (!gameMgrWarningLogged)
            {
                Debug.LogWarning("GameMgr이 없어서 사냥 시간 저장을 건너뜁니다.");
                gameMgrWarningLogged = true;
            }

            return false;
        }

        gameMgrWarningLogged = false;
        return true;
    }

    void Start()
    {
        // time 초기화
        if (TrySetGameMgr())
        {
            time = gameMgr.time / 12f * limit;
        }
        else
        {
            time = 0f;
        }

        timeOver = false;
        isPlay = true;
    }

    // Update is called once per frame
    void Update()
    {
        if(isPlay && !timeOver) {
            // 시간을 늘린다.
            time += Time.deltaTime;
            if (TrySetGameMgr())
            {
                gameMgr.time = time / limit * 12f;
            }

            // 사냥 제한 시간을 넘기면
            if(time >= limit) {
                timeOver = true;
                time = limit;
                if (TrySetGameMgr())
                {
                    gameMgr.time = time / limit * 12f;
                }

                if (huntingManager != null)
                {
                    huntingManager.ActiveTimeOverPanel();
                }
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

    // 타이머 강제 완료
    public void ForcedFinish() {
        time = limit;
        if (TrySetGameMgr())
        {
            gameMgr.time = time / limit * 12f;
        }

        timeOver = true;
    }
}
