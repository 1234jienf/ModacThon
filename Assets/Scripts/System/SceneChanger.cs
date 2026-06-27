using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class SceneChanger : MonoBehaviour
{
    public void toMainScene()
    {
        SceneManager.LoadScene("Main");
    }
    public void toMarketScene()
    {
        SceneManager.LoadScene("Store");

    }
    public void toGameStartScene()
    {
        GameMgr.Instance.Logout();
        SceneManager.LoadScene("GameStart");
    }
    
}
