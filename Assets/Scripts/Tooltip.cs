using UnityEngine;
using UnityEngine.EventSystems; // EventSystems 네임스페이스 추가

public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject tooltipPanel; // 툴팁 패널에 대한 참조

    public void OnPointerEnter(PointerEventData eventData)
    {
        tooltipPanel.SetActive(true); // 마우스가 위에 있을 때 패널 활성화
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltipPanel.SetActive(false); // 마우스가 벗어날 때 패널 비활성화
    }
}
