using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using TMPro;

public class Monster : MonoBehaviour
{
    private GameMgr gameMgr;

    private Animator monsterAnim;
    private MonsterSpawnManager spawnManager;
    private NavMeshAgent agent; // NavMeshAgent

    [SerializeField]
    private float minSpeed; // 최소 속도 (평상 시)

    [SerializeField]
    private float maxSpeed ; // 최대 속도 (평상 시)

    [SerializeField]
    private float trackingSpeed; // 추적 중일 때 속도

    [SerializeField]
    private float moveTime = 4f; // 이동 시간
    
    [SerializeField]
    private float restTime = 2f; // 쉬는 시간

    [SerializeField]
    private float maxHp; // 최대 체력
    private float hp; // 체력

    [SerializeField]
    private int atk; // 공격력

    public int exp; // 경험치

    [SerializeField]
    private GameObject healthBar; // 체력 바
    private Slider healthBarSlider; // 체력바에 사용한 슬라이더
    private readonly float healthBarLifeTime = 2f; // 체력 바 수명
    private float latestHealthBarAppeared; // 최근 체력바가 활성화된 시각 
    private bool isHealthBarActive; // 체력 바 활성화 상태

    [SerializeField]
    private SOItemDropTable dropTable; // 드롭 테이블

    [SerializeField]
    private GameObject damageText; // 데미지를 띄우기 위해 사용할 Text 오브젝트
    [SerializeField]
    private Transform damagePos; // 데미지 Text 위치

    [SerializeField]
    private Transform attackHitBoxPos; // 공격 히트 박스 pivot위치
    [SerializeField]
    private Vector2 attackHitBoxSize; // 공격 히트 박스 사이즈

    private Vector3 moveDir; // 이동 방향
    private float moveSpeed; // 이동 속도
    private bool isMove; // 이동 여부
    public bool isTracking; // 플레이어 추적 여부

    private float latestMoveStartTime; // 최근 이동 시작 시간
    private float latestRestStartTime; // 최근 휴식 시작 시간

    public bool is_right;

    private void Awake() {
        gameMgr = GameMgr.Instance;
        monsterAnim = GetComponent<Animator>();
        spawnManager = GameObject.Find("MonsterSpawnManager").GetComponent<MonsterSpawnManager>();
        healthBarSlider = healthBar.transform.Find("HealthBarSlider").GetComponent<Slider>();
    }

    private void Start() {
        hp = maxHp;
        isMove = false;
        isTracking = false;
        latestMoveStartTime = Time.time;
        latestRestStartTime = Time.time;

        // NavMeshAgent 설정
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = trackingSpeed;

        is_right = true;

        healthBar.SetActive(false);
        isHealthBarActive = false;
        latestHealthBarAppeared = Time.time;
    }

    // Update is called once per frame
    void Update()
    {
        // 이동 방향에 따라 스프라이트 회정 상태 조절
        if (is_right)
        {
            transform.eulerAngles = Vector3.zero;
            healthBarSlider.transform.eulerAngles = Vector3.zero;
        }
        else
        {
            transform.eulerAngles = new Vector3(0, 180f, 0);
            healthBarSlider.transform.eulerAngles = Vector3.zero;
        }

        // 체력 바의 수명이 다했다면 비활성화 시켜준다.
        if(isHealthBarActive && Time.time - latestHealthBarAppeared >= healthBarLifeTime) {
            healthBar.SetActive(false);
            isHealthBarActive = false;
        }

        // 피격, 공격, 사망 상태거나 추적 상태면 업데이트 함수를 종료
        if (DoHurtAnim() || DoAttackAnim() || DoDeathAnim() || isTracking) {
            return;
        }

        // 이동 중일 때
        if (isMove) {
            // 이동이 끝나면 휴식으로 진입
            if (Time.time - latestMoveStartTime >= moveTime) {
                StartRest();
            }
            else {
                transform.position += moveSpeed * Time.deltaTime * moveDir;
            }
        }
        // 휴식중일 때
        else {
            // 휴식이 끝나면 이동 시작
            if (Time.time - latestRestStartTime >= restTime) {
                StartMove();
            }
            else {
                return;
            }
        }
    }

    // 현재 공격 애니메이션이 동작 둥인가
    private bool DoAttackAnim() {
        return monsterAnim.GetCurrentAnimatorStateInfo(0).IsName("Attack L")
            || monsterAnim.GetCurrentAnimatorStateInfo(0).IsName("Attack R");
    }

    // 현재 피격 애니메이션이 동작 둥인가
    private bool DoHurtAnim() {
        return monsterAnim.GetCurrentAnimatorStateInfo(0).IsName("Hurt L")
            || monsterAnim.GetCurrentAnimatorStateInfo(0).IsName("Hurt R");
    }

    // 현재 사망 애니메이션이 동작 둥인가
    private bool DoDeathAnim() {
        return monsterAnim.GetCurrentAnimatorStateInfo(0).IsName("Death L")
            || monsterAnim.GetCurrentAnimatorStateInfo(0).IsName("Death R");
    }

    // 이동 시작
    void StartMove() {
        // 다음 이동방향 및 속도 지정
        float radius = Random.Range(-180, 180);
        moveDir = new(Mathf.Cos(radius), Mathf.Sin(radius), 0f);
        moveSpeed = Random.Range(minSpeed, maxSpeed);

        isMove = true;
        monsterAnim.SetBool("isWalking", true);
        if (moveDir.x != 0)
        {
            //monsterAnim.SetInteger("hDir", moveDir.x > 0 ? 1 : -1);
            if (moveDir.x < 0)
            {
                is_right = false;
            }
            else
            {
                is_right = true;
            }
        }
        latestMoveStartTime = Time.time;
    }

    // 휴식 시작
    void StartRest() {
        isMove = false;
        monsterAnim.SetBool("isWalking", false);
        latestRestStartTime = Time.time;
    }
    
    // 피격
    public int Damaged(int damage) {
        if (hp > 0) {
            // 피격 모션
            monsterAnim.SetTrigger("Damaged");

            // 데미지 표시
            GameObject text = Instantiate(damageText, transform.position, Quaternion.identity);
            text.GetComponent<DamageText>().damage = damage;
            text.transform.position = damagePos.position;
            hp -= damage;

            healthBarSlider.value = 1f * hp / maxHp;

            healthBar.SetActive(true);
            isHealthBarActive = true;
            latestHealthBarAppeared = Time.time;

            // 체력이 0 이하가 되면 사망
            if (hp <= 0) {
                healthBar.SetActive(false);
                isHealthBarActive = false;
                Death();

                return exp;
            }
        }

        return 0;
    }

    // 사망
    private void Death() {
        // 현재 플레이어가 위치한 타일맵을 얻는다.
        Player player = GameObject.Find("Player").GetComponent<Player>();
        Tilemap tilemap = GameObject.Find(gameMgr.currentField).transform.Find("Ground").GetComponent<Tilemap>();

        monsterAnim.SetBool("isDeath", true); // 사망 모션
        dropTable.DropItem(tilemap, transform.position); // 아이템 드롭
        spawnManager.monsterCount--; // SpawnManager의 monsterCount 1 감소
    }

    // 추적 시작
    public void StartTracking() {
        isTracking = true;
        isMove = true;
        latestMoveStartTime = Time.time;
        monsterAnim.SetBool("isWalking", true);
        moveSpeed = trackingSpeed;
    }

    // 추적
    public void Tracking(Vector3 playerPos) {
        // 피격, 공격, 사망 상태가 아니면 플레이어 방향으로 이동한다.
        if (!DoHurtAnim() && !DoAttackAnim() && !DoDeathAnim()) {
            // 목표물을 플레이어로 설정
            agent.SetDestination(playerPos);

            // 이동방향 재 설정
            moveDir = agent.desiredVelocity.normalized;

            // 몬스터의 움직임에 따라 애니메이션 설정
            if (agent.desiredVelocity.x != 0)
            {
                //monsterAnim.SetInteger("hDir", agent.desiredVelocity.x > 0 ? 1 : -1);
                if (agent.desiredVelocity.x < 0)
                {
                    is_right = false;
                }
                else
                {
                    is_right = true;
                }
            }
        }
        // 피격, 공격, 사망 상태면 잠시 추적 중지
        else {
            if (agent.hasPath) {
                agent.ResetPath();
            }
        }
    }

    // 추적 중지
    public void StopTracking() {
        isTracking = false;
        if (agent.hasPath) {
            agent.ResetPath();
        }
    }

    // 몬스터의 시야를 방향 벡터로 반환
    public Vector3 GetSight() {
        int h_dir = is_right ? 1 : -1;
        return new(1.0f * h_dir, 0f, 0f);
    }

    // 공격
    public void Attack() {
        monsterAnim.SetTrigger("Attack");

        // 공격 히트박스에 들어오는 모든 콜라이더들을 감지한 뒤 몬스터가 있으면 피격한다.
        Collider2D[] collider2Ds = Physics2D.OverlapBoxAll(attackHitBoxPos.position, attackHitBoxSize, 0);
        Debug.Log(collider2Ds.Length);
        foreach (Collider2D collider2D in collider2Ds) {
            if (collider2D.CompareTag("Player")) {
                int damage = UnityEngine.Random.Range(Mathf.CeilToInt(atk*0.8f), Mathf.FloorToInt(atk*1.2f)+1);
                collider2D.GetComponent<Player>().Damaged(damage);
                break;
            }
        }
    }

    // 공격 히트 박스를 확인하기 위해 Gizmos 그리기
    private void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(attackHitBoxPos.position, attackHitBoxSize);
    }
}
