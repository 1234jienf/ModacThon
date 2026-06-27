using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TransferField : MonoBehaviour
{
    private GameMgr gameMgr;

    // 워프 목표물
    [SerializeField]
    private Transform target;

    [SerializeField]
    private string transferFieldName; // 이동할 필드의 이름

    private void Awake() {
        gameMgr = GameMgr.Instance;
    }

    // 박스 콜라이더에 닿는 순간 이벤트 발생
    private void OnTriggerEnter2D(Collider2D other) {
        // 워프 지점에 닿은 오브젝트가 플레이어라면 워프 실시
        if (other.CompareTag("Player")) {
            // 플레이어 카메라 이동
            other.transform.position = target.position;
            Camera.main.transform.position = target.position + Vector3.forward * -10f;

            // 플레이어의 현재 필드 위치 최신화
            gameMgr.currentField = transferFieldName;
            Debug.Log(transferFieldName);
        }
    } 
}
