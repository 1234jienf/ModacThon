using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ItemGain : MonoBehaviour
{
    [SerializeField]
    private float speed;
    [SerializeField]
    private float alphaSpeed;
    [SerializeField]
    private float bounceTime;
    [SerializeField]
    private float destroyTime;

    private float createTime; // 생성 시각
    private Vector3 initPos; // 초기 위치

    [HideInInspector]
    public Sprite icon; // 아이템 아이콘

    [HideInInspector]
    public int quantity; // 획득 수량

    private SpriteRenderer spriteRenderer; // 스프라이트 렌더러
    private TextMeshPro text;
    private Color rendererColor; // 스프라이트 렌더러에 지정된 색
    private Color textColor; // 텍스트 색

    // Start is called before the first frame update
    void Start()
    {
        // 텍스트 반영
        text = transform.Find("Text").GetComponent<TextMeshPro>();
        text.text = "+" + quantity.ToString();

        // 스프라이트 반영
        spriteRenderer = transform.Find("Item Icon").GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = icon;

        // 초기 컬러 값 저장
        rendererColor = spriteRenderer.color;
        textColor = text.color;

        // 생성 시간, 초기 위치 저장
        createTime = Time.time;
        initPos = transform.position;

        // destroyTime초가 지나면 파괴
        Invoke(nameof(DestroyObject), destroyTime);
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time - createTime <= bounceTime) {
            // 통통 튀는 효과
            float size = EaseOutElastic((Time.time - createTime) / bounceTime);
            transform.localScale = size * Vector3.one;
        }

        // 데미지 텍스트 위로 이동
        transform.Translate(new Vector3(0, speed * Time.deltaTime, 0));

        // 글자색 알파값 감소
        textColor.a = Mathf.Lerp(textColor.a, 0, Time.deltaTime * alphaSpeed);
        text.color = textColor;

        // 스프라이트 알파값 감소
        rendererColor.a = Mathf.Lerp(rendererColor.a, 0, Time.deltaTime * alphaSpeed);
        spriteRenderer.color = rendererColor;
    }

    private void DestroyObject() {
        Destroy(gameObject);
    }

    private float EaseOutElastic(float x) {
        float c4 = (2 * Mathf.PI) / 3;

        return x == 0
        ? 0
        : x == 1
        ? 1
        : Mathf.Pow(2, -10 * x) * Mathf.Sin((x * 10 - 0.75f) * c4) + 1;
    }
}
