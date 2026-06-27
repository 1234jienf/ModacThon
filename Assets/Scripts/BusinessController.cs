using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.EventSystems; // 이벤트 처리를 위해 추가

public class BusinessController : MonoBehaviour, IPointerClickHandler // IPointerClickHandler 인터페이스 구현
{
    public Button businessButton;
    public TextMeshProUGUI statusText;
    public GameObject messageText; // 메시지 패널

    [SerializeField]
    private CustomerManager customerManager;
    [SerializeField]
    private QuestComplete questComplete;
    [SerializeField]
    private BusinessResult businessResult;
    [SerializeField]
    private RestaurantTimer timer;
    [SerializeField]
    private GameObject timerUI;

    private bool isBusinessOpen = false;

    void Start()
    {
        businessButton.onClick.AddListener(ToggleBusinessStatus);
        messageText.AddComponent<EventTrigger>(); // EventTrigger 컴포넌트 동적 추가
    }

     public void OnPointerClick(PointerEventData eventData)
    {
        // 여기서는 패널 클릭 시 아무 동작도 하지 않습니다.
        Debug.Log("패널이 클릭되었지만 아무 동작도 하지 않습니다.");
    }
    public void ToggleBusinessStatus()
{
    isBusinessOpen = !isBusinessOpen;

    if (isBusinessOpen)
    {
        businessButton.GetComponentInChildren<TextMeshProUGUI>().text = "영업 종료";
        StartCoroutine(ShowMessage("영업을 시작합니다"));
        customerManager.SetBusinessStatus(true);
        timer.Play();
        timerUI.SetActive(true);
    }
    else
    {
        businessButton.GetComponentInChildren<TextMeshProUGUI>().text = "영업 시작";
        StartCoroutine(ShowMessage("영업이 종료되었습니다."));
        customerManager.SetBusinessStatus(false);
        timer.ForcedFinish();
        businessResult.AfterBusinessProcess();
    }
}

    IEnumerator ShowMessage(string message)
    {
        TextMeshProUGUI textComponent = messageText.GetComponentInChildren<TextMeshProUGUI>();
        textComponent.text = message; // 메시지 설정
        messageText.SetActive(true);
        yield return new WaitForSeconds(2); // 메시지를 2초간 표시
        messageText.SetActive(false);
    }
}
