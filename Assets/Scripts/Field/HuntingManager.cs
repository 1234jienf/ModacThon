using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HuntingManager : MonoBehaviour
{
    private GameMgr gameMgr;

    [SerializeField]
    private GameObject leavePanel;
    private bool showLeavePanel;

    [SerializeField]
    private GameObject timeOverPanel;
    private bool showTimeOverPanel;

    [SerializeField]
    private GameObject diePanel;
    private bool showDiePanel;

    [SerializeField]
    private HuntingTimer timer; // 타이머

    // Start is called before the first frame update
    void Start()
    {
        // 모든 팝업창 비활성화
        InActiveLeavePanel();
        InActiveTimeOverPanel();
        InActiveDiePanel();
    }

    // LeavePanel 활성화
    public void ActiveLeavePanel() {
        showLeavePanel = true;
        leavePanel.SetActive(true);
        timer.Pause();
    }

    // LeavePanel 비활성화
    public void InActiveLeavePanel() {
        showLeavePanel = false;
        leavePanel.SetActive(false);
        timer.Play();
    }

    // LeavePanel에서 확인 누를 시
    public void ConfirmLeave() {
        timer.ForcedFinish(); // 강제로 밤 시간대로 보내고
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main"); // 메인 씬으로 이동
    }

    // LeavePanel에서 취소 누를 시
    public void CancelLeave() {
        InActiveLeavePanel(); // 판넬 비활성화
    }

    // TimeOverPanel 활성화
    public void ActiveTimeOverPanel() {
        showTimeOverPanel = true;
        timeOverPanel.SetActive(true);
        timer.Pause();
    }

    // TimeOverPanel 비활성화
    public void InActiveTimeOverPanel() {
        showTimeOverPanel = false;
        timeOverPanel.SetActive(false);
        timer.Play();
    }

    // TimeOverPanel에서 확인 누를 시
    public void ConfirmTimeOver() {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main"); // 메인 씬으로 이동
    }

    // DiePanel 활성화
    public void ActiveDiePanel() {
        showDiePanel = true;
        diePanel.SetActive(true);
        timer.Pause();
    }

    // DiePanel 비활성화
    public void InActiveDiePanel() {
        showDiePanel = false;
        diePanel.SetActive(false);
        timer.Play();
    }

    // DiePanel에서 취소 누를 시
    public void ConfirmDie() {
        timer.ForcedFinish(); // 강제로 밤 시간대로 보내고
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main"); // 메인 씬으로 이동
    }
}
