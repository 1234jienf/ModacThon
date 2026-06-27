using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelUp : MonoBehaviour
{
    [SerializeField]
    private float speed;
    [SerializeField]
    private float alphaSpeed;
    [SerializeField]
    private float destroyTime;

    private float createTime; // 생성 시각
    private Vector3 initPos; // 초기 위치

    private SpriteRenderer spriteRenderer; // 스프라이트 렌더러
    private Color rendererColor; // 스프라이트 렌더러에 지정된 색

    // Start is called before the first frame update
    void Start()
    {
        // 스프라이트 얻기
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 초기 컬러 값 저장
        rendererColor = spriteRenderer.color;

        // 생성 시간, 초기 위치 저장
        createTime = Time.time;
        initPos = transform.position;

        // destroyTime초가 지나면 파괴
        Invoke(nameof(DestroyObject), destroyTime);
    }

    // Update is called once per frame
    void Update()
    {
        transform.Translate(new Vector3(0, speed * Time.deltaTime, 0));

        // 스프라이트 알파값 감소
        rendererColor.a = Mathf.Lerp(rendererColor.a, 0, Time.deltaTime * alphaSpeed);
        spriteRenderer.color = rendererColor;
    }

    private void DestroyObject() {
        Destroy(gameObject);
    }
}
