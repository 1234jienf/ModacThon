using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    private GameMgr gameMgr; // GameManager

    public Animator animator;
    private Rigidbody2D rigid;
    public Vector3 fireDir;
    private SpriteRenderer render;
    public Vector3 sight = Vector3.left; // 플레이어의 시선
    private bool is_hit; // 피격 상태
    private bool is_death = false; // 사망 상태
    public float hit_control;

    [SerializeField]
    private Transform startPoint; // 시작 위치

    [SerializeField]
    private GameObject weapon; // 탄막

    [SerializeField]
    private float moveSpeed = 3f; // 플레이어의 이동 속도
    private float ridingSpeed; // 탈것에 탑승 시 이동속도

    private readonly int maxLevel = 50; // 만렙

    [SerializeField]
    private Slider healthBarSlider; // 체력바에 사용한 슬라이더
    [SerializeField]
    private Slider expBarSlider; // 체력바에 사용한 슬라이더
    [SerializeField]
    private TMP_Text levelText; // 체력바에 사용한 슬라이더

    private int maxHp; // 최대 체력

    private int needExp; // 요구 경험치
    private int atk; // 공격력

    [SerializeField]
    private List<FishingManager> fishingManagers;

    [SerializeField]
    private Transform attackHitBoxPos; // 공격 히트 박스 pivot위치
    [SerializeField]
    private Vector2 attackHitBoxSize; // 공격 히트 박스 사이즈

    public List<Collider2D> triggerItemList; // 현재 플레이어와 닿아있는 드롭 아이템들 리스트

    [SerializeField]
    private InventoryControl myInventory;

    [SerializeField]
    private GameObject damageText; // 데미지를 띄우기 위해 사용할 Text 오브젝트
    [SerializeField]
    private Transform damagePos; // 데미지 Text 위치

    [SerializeField]
    private GameObject itemGain; // 아이템 획득 표시를 위해 사용할 오브젝트
    [SerializeField]
    private Transform itemGainPos; // 아이템 획득 표시 위치

    [SerializeField]
    private GameObject levelUp; // 레벨업 표시를 위해 사용할 오브젝트
    [SerializeField]
    private Transform levelUpPos; // 레벨업 표시 위치
    [SerializeField]
    private AudioSource HitSound; // 때리는 사운드
    [SerializeField]
    private AudioSource ItemGetSound; // 아이템 획득 사운드

    [SerializeField]
    private HuntingManager huntingManager; // HuntingManager

    void Awake()
    {
        gameMgr = FindObjectOfType<GameMgr>();
        if (gameMgr == null)
        {
            GameObject gameMgrObj = new GameObject("GameMgr");
            gameMgr = gameMgrObj.AddComponent<GameMgr>();
        }

        gameMgr.InitOfflineData();

        myInventory = gameMgr.GetComponentInChildren<InventoryControl>();
        if (myInventory == null)
        {
            myInventory = FindObjectOfType<InventoryControl>();
        }

        if (myInventory != null)
        {
            gameMgr.inventoryControl = myInventory;
        }
        
        rigid = GetComponent<Rigidbody2D>();
        triggerItemList = new List<Collider2D>();
        if (fishingManagers == null)
        {
            fishingManagers = new List<FishingManager>();
        }

        GameObject[] fishingManagerObjs = GameObject.FindGameObjectsWithTag("Fishing Manager");
        foreach (GameObject fishingManagerObj in fishingManagerObjs)
        {
            fishingManagers.Add(fishingManagerObj.GetComponent<FishingManager>());
        }
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        // 적용 시각이 0 초과 12미만이라면 그 쪽으로 보내고 없으면 초기 위치로 보낸다.
        if (0 < gameMgr.time && gameMgr.time < 12) {
            transform.position = new Vector3((float)gameMgr.lastXy[0], (float)gameMgr.lastXy[1], 0f);
        }
        else {
            transform.position = startPoint.position;
            gameMgr.lastXy[0] = startPoint.position.x;
            gameMgr.lastXy[1] = startPoint.position.y;
            gameMgr.currentField = "Field 1";
        }

        fireDir = Vector3.down;
        render = GetComponent<SpriteRenderer>();
        is_hit = false;
        hit_control = 0;

        // 게임 정보를 받은 다음 설정해 준다.
        // 레벨
        levelText.text = "Lv. " + gameMgr.lv.ToString();

        // HP
        maxHp = CalculateMaxHp();
        // 적용 시각이 0 초과 12미만이라면 이전 체력을 그대로 사용. 아니면 풀로 채운다.
        if (0 < gameMgr.time && gameMgr.time < 12) {
            healthBarSlider.value = 1f * gameMgr.hp / maxHp;
        }
        else {
            gameMgr.hp = maxHp;
            healthBarSlider.value = 1f * gameMgr.hp / maxHp;
        }

        //EXP
        needExp = CalculateNeedExp();
        expBarSlider.value = 1f * gameMgr.exp / needExp;

        atk = CalculateAtk();

        // 탈것
        // Lv.0 -> 7, Lv.1 -> 8, Lv.2 -> 9, Lv.3 -> 10
        ridingSpeed = 7f + gameMgr.currentVehicle;
        Debug.Log(ridingSpeed);

        if (gameMgr.inventoryControl != null)
        {
            myInventory = gameMgr.inventoryControl;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 사망 상태가 아니고 게임 시간이 흐르는 상태라면 조작 수행
        if (!is_death && Time.timeScale > 0f) {
            // 수평/수직 Input 값
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            // 캐릭터의 이동방향
            Vector3 moveDir = new(h, v, 0f);
            if (moveDir != Vector3.zero)
            {
                if (h > 0)
                {
                    transform.eulerAngles = new Vector3(0, 180f, 0);
                    sight = Vector3.right;
                }
                else if (h < 0)
                {
                    transform.eulerAngles = Vector3.zero;
                    sight = Vector3.left;
                }
                moveDir = moveDir.normalized;
                fireDir = moveDir;
                animator.SetBool("is_walking", true);
            }
            else
            {
                animator.SetBool("is_walking", false);
            }

            // 캐릭터 이동
            transform.position += moveSpeed * Time.deltaTime * moveDir;

            // X 버튼을 누르면 공격
            if (
                Input.GetKeyDown(KeyCode.X) 
                && !animator.GetBool("is_riding")
                && !animator.GetCurrentAnimatorStateInfo(0).IsName("Attack")
            ) {
                Debug.Log(animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"));
                Debug.Log("공격!");
                animator.SetTrigger("is_attacking");
                
                // 공격 히트박스에 들어오는 모든 콜라이더들을 감지한 뒤 몬스터가 있으면 피격한다.
                Collider2D[] collider2Ds = Physics2D.OverlapBoxAll(attackHitBoxPos.position, attackHitBoxSize, 0);
                Debug.Log(collider2Ds.Length);
                foreach (Collider2D collider2D in collider2Ds) {
                    if (collider2D.CompareTag("Monster")) {
                        HitSound.Play();
                        int damage = UnityEngine.Random.Range(Mathf.CeilToInt(atk*0.8f), Mathf.FloorToInt(atk*1.2f)+1);
                        int gainExp = collider2D.GetComponent<Monster>().Damaged(damage);

                        // 얻은 경험치가 있으면 반영한다.
                        if(gainExp > 0) {
                            // 경험치 추가
                            gameMgr.exp += gainExp;

                            // 현재 경험치가 요구 경험치보다 많으면 레벨 업한다.
                            while (gameMgr.lv < maxLevel && gameMgr.exp >= needExp) {
                                LevelUp();
                            }
                            
                            // 체력바 최신화
                            healthBarSlider.value = 1f * gameMgr.hp / maxHp;
                            // 경험치바 최신화
                            if (gameMgr.lv  == maxLevel) { // 플레이어가 만렙이면
                                gameMgr.exp = 0;
                                expBarSlider.value = 1f;
                            }
                            else { // 만렙이 아니면
                                expBarSlider.value = 1f * gameMgr.exp / needExp;
                            }
                        }
                        break;
                    }
                }
            }

            // Z 버튼을 누르면 탈것
            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (animator.GetBool("is_riding"))
                {
                    moveSpeed = 3.0f;
                    animator.SetBool("is_riding", false);
                }
                else
                {
                    moveSpeed = ridingSpeed;
                    animator.SetBool("is_riding", true);
                }
            }

            // 피격 상태면 0.4초동안 빨간색으로 깜빡, 무적
            if(is_hit) {
                if (hit_control < 0.4f)
                {
                    hit_control += Time.deltaTime;
                    if (Mathf.FloorToInt(hit_control/0.1f) % 2 == 0)
                    {
                        render.color = new Color(1, 0, 0, 1);
                    }
                    else
                    {
                        render.color = new Color(1, 1, 1, 1);
                    }
                }
                else
                {
                    hit_control = 0f;
                    is_hit = false;
                    render.color = new Color(1, 1, 1, 1);
                }
            }

            // 스페이스를 누르면 아이템 획득
            // triggerItemList의 첫번째에 해당하는 아이템을 얻는다.
            if (Input.GetKeyDown(KeyCode.Space)) {
                if (triggerItemList.FirstOrDefault() != null) {
                    GameObject getItem = triggerItemList.First().gameObject;
                    ItemGetSound.Play();
                    if(getItem.CompareTag("Livestock Product")) {
                        // 아이템 획득 로직 작동
                        myInventory.addItem(triggerItemList.First().gameObject.GetComponent<LivestockProduct>().soItem);
                        getItem.GetComponent<LivestockProduct>().Get();

                        // 아이템을 획득했음을 보여준다
                        ShowItemGain(getItem.GetComponent<SpriteRenderer>().sprite, 1, Vector3.zero);
                    }
                    else if(getItem.CompareTag("Recipe")) {
                        /*인벤토리에 레시피 추가*/
                        myInventory.addItem(triggerItemList.First().gameObject.GetComponent<RecipeDropItem>().soItem);
                        getItem.GetComponent<RecipeDropItem>().Get();
                    }
                    else if(getItem.CompareTag("Plant")) {
                        // 아이템 획득 로직 작동
                        myInventory.addItem(triggerItemList.First().gameObject.GetComponent<Plant>().soItem);
                        getItem.GetComponent<Plant>().Get();

                        // 아이템을 획득했음을 보여준다
                        ShowItemGain(getItem.GetComponent<SpriteRenderer>().sprite, 1, Vector3.zero);
                    }
                }
            }

            
            // 낚시 로직
            // 디버그용
            Debug.DrawRay(rigid.position, sight * 1f, new Color(0, 0, 1)); 

            // 플레이어의 시선에 호수가 있고 채집 버튼을 누르면 물고기를 얻는다.
            // 이때 닿은 타일의 위치를 얻을 때 (Vector2)rayDir * 0.01f을 더한 벡터를 매개변수로 전달해야 함
            // rayHit.point는 충돌이 일어난 위치를 반환하므로 그대로 사용하면 타일맵 좌표로 변환 시 원하지 않은 결과가 나올 수 있음
            RaycastHit2D rayHit = Physics2D.Raycast(rigid.position, sight, 1f, LayerMask.GetMask("Lake"));
            if(rayHit.collider != null && Input.GetKeyDown(KeyCode.C)) {
                Tilemap tilemap = rayHit.collider.gameObject.GetComponent<Tilemap>();
                Vector3Int tilePos = tilemap.WorldToCell(rayHit.point + (Vector2)sight * 0.01f);
                // FishingManager를 전부 뒤진다.
                foreach (FishingManager fishingManager in fishingManagers) {
                    // 범위에 속하는 FishingManager을 발견하면 Fishing함수를 실행
                    // 그리고 break로 빠져나간다. (각 FishingManager가 관리하는 공간은 서로 겹치지 않기 때문)
                    if (fishingManager.InBounds(tilePos)) {
                        List<Tuple<SOItem, int>> fishList = fishingManager.Fishing(tilePos);

                        float y = 0;
                        foreach((SOItem fish, int quantity) in fishList) {
                            if(quantity > 0) {
                                for (int i = 0; i < quantity; i++) {
                                    myInventory.addItem(fish);
                                }

                                ShowItemGain(fish.icon, quantity, y * Vector3.up);
                                y += 0.8f;
                            }
                        }
                        ItemGetSound.Play();
                        break;
                    }
                }
            }
        }

        gameMgr.lastXy[0] = transform.position.x;
        gameMgr.lastXy[1] = transform.position.y;
    }

    // 플레이어 피격
    public void Damaged(int damage)
    {
        if (gameMgr.hp > 0)
        {
            // 피격 모션 & 체력 감소
            if (!is_hit) {
                is_hit = true;
                
                // 데미지 표시
                GameObject text = Instantiate(damageText, transform.position, Quaternion.identity);
                text.GetComponent<DamageText>().damage = damage;
                text.transform.position = damagePos.position;
                gameMgr.hp -= damage;

                healthBarSlider.value = 1f * gameMgr.hp / maxHp;
            }

            // 체력이 0 이하가 되면 사망
            if (gameMgr.hp <= 0)
            {
                gameMgr.hp = 0;
                Death();
            }
        }
    }

    // 플레이어 사망
    private void Death()
    {
        animator.SetBool("is_dead", true);
        is_death = true;
        Invoke(nameof(ActiveDiePanel), 3f);
    }

    // 사망 판넬 띄우기
    private void ActiveDiePanel() {
        huntingManager.ActiveDiePanel();
    }

    // 아이템을 얻었음을 보여준다.
    public void ShowItemGain(Sprite sprite, int quantity, Vector3 offset) {
        GameObject itemGainObj = Instantiate(itemGain, itemGainPos.position + offset, Quaternion.identity);
        ItemGain itemGainInstance = itemGainObj.GetComponent<ItemGain>();
        itemGainInstance.icon = sprite;
        itemGainInstance.quantity = quantity;
    }

    // 최대 체력 계산
    private int CalculateMaxHp() {
        return 500 + (gameMgr.lv  - 1) * 50;
    }

    // 최대 경험치 계산
    private int CalculateNeedExp() {
        return gameMgr.lv  == maxLevel ? 0 : Mathf.FloorToInt(100 * Mathf.Pow(1.15f, gameMgr.lv -1));
    }

    // 공격력 계산
    private int CalculateAtk() {
        var armCoef = gameMgr.currentArm switch
        {
            1 => 1.2f,
            2 => 1.5f,
            3 => 2f,
            _ => 1f,
        };
        return Mathf.FloorToInt((10 + 2.5f * (gameMgr.lv - 1)) * armCoef);
    }


    // 레벨 업
    private void LevelUp() {
        gameMgr.lv += 1;
        levelText.text = "Lv. " + gameMgr.lv .ToString();
        
        gameMgr.exp -= needExp;

        maxHp = CalculateMaxHp();
        needExp = CalculateNeedExp();
        atk = CalculateAtk();

        gameMgr.hp = maxHp;

        Instantiate(levelUp, levelUpPos.position, Quaternion.identity);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 트리거 된 물체가 드롭 아이템 또는 채집품일 때
        if (
            other.CompareTag("Livestock Product") 
            || other.CompareTag("Recipe")
            || other.CompareTag("Plant")
        )
        {
            // 트리거 목록에 없으면 추가
            if (!triggerItemList.Contains(other))
            {
                triggerItemList.Add(other);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // 트리거 해제 된 물체가 드롭 아이템일 때
        if (
            other.CompareTag("Livestock Product") 
            || other.CompareTag("Recipe")
            || other.CompareTag("Plant")
        )
        {
            // 트리거 목록에 있으면
            if (triggerItemList.Contains(other))
            {
                triggerItemList.Remove(other);
            }
        }
    }

    // 공격 히트 박스를 확인하기 위해 Gizmos 그리기
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(attackHitBoxPos.position, attackHitBoxSize);
    }
}
