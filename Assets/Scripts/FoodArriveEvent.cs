using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FoodArriveEvent : MonoBehaviour
{
    
    public delegate void FoodDeliveryHandler(bool isCorrect, GameObject customer);
    public static event FoodDeliveryHandler OnFoodDelivered;
    private FoodEvent foodEvent;
    private CustomerManager customerManager;
    private Movement2D thePlayer;
    private MenuManager menuManager;

    void Start()
    {
        thePlayer = FindObjectOfType<Movement2D>();
        foodEvent = FindObjectOfType<FoodEvent>();
        customerManager = FindObjectOfType<CustomerManager>();
        menuManager = FindObjectOfType<MenuManager>();
    }

    private void OnTriggerStay2D(Collider2D collision)
{
    // 'X' 키를 누르고 있는지 확인하고, 상호작용 중인 객체가 플레이어인지 확인합니다.
    if (Input.GetKey(KeyCode.X) && collision.gameObject == thePlayer.gameObject)
    {

        // 플레이어와 NPC 사이의 거리를 계산합니다.
        float distance = Vector2.Distance(thePlayer.transform.position, transform.position);
        
        // 거리가 특정 값보다 작을 때만 음식 배달 검사를 실행합니다.
        // 예를 들어, 3 유닛 이내일 때만 실행하도록 설정합니다.
        if (distance <= 5f)
        {
            // Debug.Log("가까워서");
            CheckFoodDelivery();
            foodEvent.currentHeldFood = null;
            foodEvent.foodHolder.GetComponent<SpriteRenderer>().sprite = null;
        }
    }
}

    private void CheckFoodDelivery() {
    if (foodEvent.currentHeldFood == null || customerManager.foodImageUI == null) {
        Debug.Log("No food to compare.");
        return;
    }

    Sprite playerFoodSprite = foodEvent.currentHeldFood.image;
    Sprite customerFoodSprite = customerManager.foodImageUI.GetComponent<SpriteRenderer>().sprite;

    if (playerFoodSprite == customerFoodSprite) {
        // Debug.Log("CORRECT FOOD");
        menuManager.ItemSold(customerFoodSprite);
        OnFoodDelivered?.Invoke(true, this.gameObject); // 이 게임 오브젝트를 이벤트와 함께 전달합니다.
    } else {
        // Debug.Log("INCORRECT FOOD");
        OnFoodDelivered?.Invoke(false, this.gameObject);
    }
}
}
