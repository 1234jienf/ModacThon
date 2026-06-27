using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartPoint : MonoBehaviour
{
    [SerializeField]
    private string fieldName; // 스타팅 포인트가 있는 필드 이름

    private GameObject player; // 플레이어
    private GameObject mainCamera; // 카메라

    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.Find("Player");
        mainCamera = GameObject.Find("Main Camera");

        // 플레이어와 카메라의 위치를 스타트 포인트로 이동
        // 단, 카메라의 z좌표는 -10으로 설정해준다.
        player.transform.position = transform.position;
        mainCamera.transform.position = transform.position + Vector3.forward * -10f;
    }
}
