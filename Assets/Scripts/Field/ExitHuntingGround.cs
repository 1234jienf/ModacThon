using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExitHuntingGround : MonoBehaviour
{
    // HuntingManager
    [SerializeField]
    private HuntingManager huntingManager;

    // 박스 콜라이더에 닿는 순간 이벤트 발생
    private void OnTriggerEnter2D(Collider2D other) {
        // 워프 지점에 닿은 오브젝트가 플레이어라면 LeavePanel을 띄운다.
        if (other.CompareTag("Player")) {
            huntingManager.ActiveLeavePanel();
        }
    } 
}
