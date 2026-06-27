using UnityEngine;

public class StoreCamera : MonoBehaviour
{
    public float moveSpeed = 10.0f; // 카메라 이동 속도
    private Vector3 targetPosition; // 카메라가 이동할 목표 위치

    private void Start()
    {
        // 초기 목표 위치를 현재 카메라의 위치로 설정
        targetPosition = transform.position;
    }

    private void Update()
    {
        // 현재 카메라의 위치를 목표 위치로 부드럽게 이동
        transform.position = Vector3.Lerp(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }

    // 버튼 1 클릭 시 호출될 함수
    public void MoveToPosition1()
    {
        Debug.Log("대장간 이동");
        targetPosition = new Vector3(0, transform.position.y, transform.position.z);
    }

    // 버튼 2 클릭 시 호출될 함수
    public void MoveToPosition2()
    {
        Debug.Log("마굿간 이동");
        targetPosition = new Vector3(17, transform.position.y, transform.position.z);
    }

    // 버튼 3 클릭 시 호출될 함수
    public void MoveToPosition3()
    {
        Debug.Log("상점 이동");
        targetPosition = new Vector3(33, transform.position.y, transform.position.z);
    }
}