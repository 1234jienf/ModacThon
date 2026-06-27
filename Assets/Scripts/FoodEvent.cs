using System.Collections;
using UnityEngine;

public class FoodEvent : MonoBehaviour
{

    public MenuItem currentHeldFood; // 플레이어가 현재 들고 있는 음식

    public MenuManager menuManager;
    private Movement2D thePlayer;
    private bool flag = false;
    private int currentFoodIndex = 0; // 현재 선택된 음식 인덱스
    public GameObject foodHolder; // 플레이어 안의 음식을 보여줄 GameObject

    void Start()
    {
        thePlayer = FindObjectOfType<Movement2D>();
        menuManager = FindObjectOfType<MenuManager>();
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!flag && Input.GetKey(KeyCode.Z) && thePlayer.GetAnimatorFloat("DirX") == -1f)
        {
            flag = true;
            StartCoroutine(EventCoroutine());
        }
    }

    IEnumerator EventCoroutine()
    {
        if (menuManager.todaysMenu.Count > 0)
        {
            // 현재 음식 아이템을 가져오고 인덱스를 업데이트합니다.
            currentFoodIndex = Random.Range(0, menuManager.todaysMenu.Count);
            var currentItem = menuManager.todaysMenu[currentFoodIndex];
            // 음식 아이템의 스프라이트를 플레이어의 foodHolder에 설정합니다.
           currentHeldFood = menuManager.todaysMenu[currentFoodIndex];
            var spriteRenderer = foodHolder.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = currentHeldFood.image;
            foodHolder.SetActive(true); // 음식 오브젝트 활성화
            Debug.Log("선택된 음식: " + currentHeldFood.name + ", 홀더 스프라이트: " + foodHolder.GetComponent<SpriteRenderer>().sprite.name);
        }
        else
        {
            Debug.Log("오늘의 메뉴가 비어있습니다.");
        }

        yield return new WaitForSeconds(0.5f); // 다음 입력까지 잠시 대기

        flag = false; // 다시 입력 받을 수 있도록 플래그를 리셋합니다.
    }
}
