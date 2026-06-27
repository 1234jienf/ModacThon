using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TalkManager : MonoBehaviour
{
    public GameObject talkPanel;
    public TMP_Text talkText;
    public GameObject buyObject;
    public bool isActive = false;

    public void Action(GameObject buyObj)
    {
        if(isActive)
        {
            isActive = false;
        }
        else
        {
            isActive = true;
            buyObject = buyObj;
            talkText.text = buyObject.name + "를 구매했다!";
        }
        talkPanel.SetActive(isActive);
    }
}
