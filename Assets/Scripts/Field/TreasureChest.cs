using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class TreasurChest : MonoBehaviour
{
    private GameMgr gameMgr; // GameManager

    private bool isActive = false; // 활성화 여부 (처음에는 false)
    private bool isOpen = false; // 상자 오픈 여부
    private Animator chestAnim; // 상자 애니메이터
    private SpriteRenderer spriteRenderer; // SpriteRenderer
    
    [SerializeField]
    private SOItem soItem; // 해당 상자에 연결된 레시피
    [SerializeField]
    private AudioSource OpenSound; // 해당 상자에 연결된 레시피

    private void Awake() {
        gameMgr = GameMgr.Instance;

        // 이미 배운 요리면 해당 레시피가 담긴 보물상자 제거 
        if (gameMgr.learnedList.FindIndex(recipeCode => recipeCode == soItem.itemCode) != -1) {
            Destroy(gameObject);
        }

        // 배우지는 않았지만 획득한 레시피라면 해당 레시피가 담긴 보물상자 제거
        if (
            Array.FindIndex(
                gameMgr.inventoryControl.GetSlots()[3], 
                itemSlot => itemSlot.item != null && itemSlot.item.itemCode == soItem.itemCode
            ) != -1
        ) {
            Destroy(gameObject);
        }

        chestAnim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Start is called before the first frame update
    void Start()
    {
        // isActive에 따라 활성화 여부 조절
        spriteRenderer.color = new Color(1, 1, 1, isActive ? 1 : 0);
    }

    // Update is called once per frame
    void Update()
    {
        // 상자가 열리지 않은 상태에서만 상자와 상호작용 가능
        if(!isOpen) {
            // 상자가 활성화된 상태에서 C 버튼을 누르면
            if (isActive && Input.GetKeyDown(KeyCode.C)) {
                // 상자가 열린다.
                OpenSound.Play();
                Open();
            }
        }
    }

    // 상자 오픈
    private void Open() {
        isOpen = true;
        chestAnim.SetTrigger("doOpen");

        // 상자에 담긴 레시피 소환
        GameObject recipeObj = Instantiate(soItem.prefab, transform.position, Quaternion.identity);
        recipeObj.GetComponent<RecipeDropItem>().soItem = soItem;
    }

    // 플레이어가 범위 안으로 들어오면 상자 활성화
    private void OnTriggerEnter2D(Collider2D other) {
        if(other.CompareTag("Player")) {
            Debug.Log("상자 ON");
            isActive = true;
            spriteRenderer.color = new Color(1, 1, 1, 1);
        }
    }

    // 플레이어가 범위 밖으로 나가면 상자 비활성화
    private void OnTriggerExit2D(Collider2D other) {
        if(other.CompareTag("Player")) {
            isActive = false;
            spriteRenderer.color = new Color(1, 1, 1, 0);
        }
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(1.5f, 1.5f, 0));
    }
}
