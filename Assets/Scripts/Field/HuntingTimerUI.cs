using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class HuntingTimerUI : MonoBehaviour
{   
    [SerializeField]
    private HuntingTimer timer; // UI에 연결된 타이머

    private TMP_Text timerPanelText; // 타이머 패널에 들어간 텍스트
    private Transform maskTransform; // 마스크 오브젝트의 Transform
    private Transform moveTransform; // 움직이는 오브젝트의 Transform

    [SerializeField]
    private float orbitRadius; // 움직이는 오브젝트의 궤적 반지름
    private Vector3 orbitCenter; // 궤적 중심

    private float initTheta;

    private void Awake() {
        timerPanelText = transform.Find("TimeText").GetComponent<TMP_Text>();
        maskTransform = transform.Find("Mask");
        moveTransform = maskTransform.Find("Move");
    }

    // Start is called before the first frame update
    void Start()
    {
        // 궤적의 중심값 구하기
        orbitCenter = new Vector3(0f, -orbitRadius, 0f);

        // 초기 theta 값을 구한다.
        // Mask와 Move 오브젝트는 전부 원형이라고 가정
        float radiusMask = maskTransform.gameObject.GetComponent<RectTransform>().sizeDelta.x/2;
        float radiusMove = moveTransform.gameObject.GetComponent<RectTransform>().sizeDelta.x/2;

        initTheta = Mathf.Acos(1f-0.5f*((radiusMask+radiusMove)/orbitRadius)*((radiusMask+radiusMove)/orbitRadius));
    }

    // Update is called once per frame
    void Update()
    {
        // 잔여 시간 (12시간 == 720으로 해서 치환)
        float remain = timer.limit - timer.time > 0 ? (timer.limit - timer.time) / timer.limit * 720f : 0;
        int remainInt = Mathf.CeilToInt(remain);

        // 잔여 시간 텍스트화
        string remainText = "";
        if (remainInt == 0) {
            remainText = "0분";
        }
        else {
            if (remainInt / 60 > 0) {
                remainText += (remainInt / 60).ToString() + "시간";
            }
            if (remainInt % 60 > 0) {
                remainText += (remainText != "" ? " " : "") + (remainInt % 60).ToString() + "분";
            }
        }

        // UI 최신화
        float theta = initTheta * remain / 720f;
        moveTransform.position = maskTransform.position + orbitCenter + orbitRadius * new Vector3(Mathf.Sin(theta), Mathf.Cos(theta), 1);
        timerPanelText.text = "밤이 되기까지 앞으로 " + remainText;
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.Find("Mask").position + orbitCenter, orbitRadius);
    }
}
