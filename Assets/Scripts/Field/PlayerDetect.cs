using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerDetect : MonoBehaviour
{
    [SerializeField]
    private float fov; // 시야각

    [SerializeField]
    private float attackRange; // 공격 범위

    public Monster monster; // 몬스터 오브젝트
    private float latestAttack = 0f; // 최근 공격 시간

    [SerializeField]
    private float attackCoolDown; // 공격 쿨타임

    private void Awake() {
        monster = transform.parent.gameObject.GetComponent<Monster>();
    }

    // 몬스터가 시야각에 들어오는 순간부터 추격 개시
    private void OnTriggerStay2D(Collider2D other) {
        // 감지된 대상이 플레이어라면
        if (other.CompareTag("Player")) {
            // 콜라이더 중심 기준 위치와 몬스터의 시야 방향 얻고
            Vector3 playerDir = other.transform.position - transform.position;
            Vector3 sight = monster.GetSight();

            // 내적 계산
            float innerProd = playerDir.x*sight.x+playerDir.y*sight.y+playerDir.z*sight.z;

            // 시야각 내에 있고
            if (innerProd >= playerDir.magnitude * Mathf.Cos(fov/360*Mathf.PI)) {
                // 추적 상태가 아니면 추적 시작
                if (!monster.isTracking) {
                    monster.StartTracking();
                }
            }

            if (monster.isTracking) {
                // 공격 범위 내에 있으면 공격
                if (playerDir.sqrMagnitude <= attackRange * attackRange && Time.time - latestAttack >= attackCoolDown) {
                    monster.Attack();
                    latestAttack = Time.time;
                }
                // 아니면 플레이어를 따라간다.
                else {
                    monster.Tracking(other.transform.position);
                }
            }
        }
    }

    // 플레이어가 몬스터 반경에서 벗어나면 몬스터는 추적 중지
    private void OnTriggerExit2D(Collider2D other) {
        if(other.CompareTag("Player")) {
            monster.StopTracking();
        }
    }
}
