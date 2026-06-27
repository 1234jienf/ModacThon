using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;


public class SettingManager : MonoBehaviour
{
    public bool settingShow;
    public bool isMute;

    private bool isPaused = false;
    public GameObject settingCanvas;

    public Button btn_save, btn_mute, btn_exit;
    void Start()
    {
        settingShow = false;
        isMute = false;
        isPaused = false;
        settingCanvas.SetActive(settingShow);
        btn_save.onClick.AddListener(saveBtn);
        btn_mute.onClick.AddListener(muteCtlBtn);
        btn_exit.onClick.AddListener(exitBtn);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0f : 1f;
            settingShow = !settingShow;
            settingCanvas.SetActive(settingShow);
        }
    }

    private void saveBtn()
    {
        GameMgr.Instance.SaveGame();
    }
    private void muteCtlBtn()
    {
        isMute = !isMute;
        AudioListener.volume = isMute ? 0f : 1f;
    }
    private void exitBtn()
    {
        Application.Quit();
    }
}