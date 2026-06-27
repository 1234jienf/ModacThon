using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class npc_control : MonoBehaviour
{
    public RectTransform NPC_A;
    public RectTransform NPC_B;
    public RectTransform NPC_C;

    public float transitionTime = 0.2f;


    private RectTransform currentNPC;
    public TMP_Text NPC_name;
    public TMP_Text NPC_dialog;

    void Start()
    {
        transitionTime = 0.2f;
        currentNPC = NPC_A;
        NPC_name.text = "대장장이";
        NPC_dialog.text = "오오... 자네의 주방칼... 꽤 좋아보이는데!\n내가 한번 만져봐도 되겠나?";
        NPC_B.position = NPC_B.position + new Vector3(700, 0, 0);
        NPC_C.position = NPC_C.position + new Vector3(700, 0, 0);
    }

    public void ShowImageA()
    {
        if (currentNPC == NPC_A) return;
        NPC_name.text = "";
        NPC_dialog.text = "";
        StartCoroutine(TransitionImage(currentNPC, NPC_A));
        NPC_name.text = "대장장이";
        NPC_dialog.text = "오오... 자네의 주방칼... 꽤 좋아보이는데!\n내가 한번 만져봐도 되겠나?";
        currentNPC = NPC_A;
    }
    public void ShowImageB()
    {
        if (currentNPC == NPC_B) return;
        NPC_name.text = "";
        NPC_dialog.text = "";
        StartCoroutine(TransitionImage(currentNPC, NPC_B));
        NPC_name.text = "마굿간지기";
        NPC_dialog.text = "말의 훈련을 게을리하지 말게나.";
        currentNPC = NPC_B;

    }
    public void ShowImageC()
    {
        if (currentNPC == NPC_C) return;
        NPC_name.text = "";
        NPC_dialog.text = "";
        StartCoroutine(TransitionImage(currentNPC, NPC_C));
        NPC_name.text = "상점주인";
        NPC_dialog.text = "어서오세요~!";
        currentNPC = NPC_C;
    }

    IEnumerator TransitionImage(RectTransform currentImage, RectTransform nextImage)
    {
        // 현재 이미지를 오른쪽으로 이동
        float elapsedTime = 0;
        Vector3 startPos = currentImage.position;
        Vector3 endPos = startPos + new Vector3(700, 0, 0);

        while (elapsedTime < transitionTime)
        {
            currentImage.position = Vector3.Lerp(startPos, endPos, elapsedTime / transitionTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        currentImage.position = endPos;

        // 다음 이미지를 화면 왼쪽 바깥에서 중앙으로 이동
        elapsedTime = 0;
        startPos = nextImage.position;
        endPos = startPos - new Vector3(700, 0, 0);

        while (elapsedTime < transitionTime)
        {
            nextImage.position = Vector3.Lerp(startPos, endPos, elapsedTime / transitionTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        nextImage.position = endPos;
    }
}
