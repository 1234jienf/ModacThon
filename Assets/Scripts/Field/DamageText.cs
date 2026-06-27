using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.Rendering;

public class DamageText : MonoBehaviour
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
    public int damage;
    private TextMeshPro text;
    private Color color;

    // Start is called before the first frame update
    void Start()
    {
        text = GetComponent<TextMeshPro>();
        text.text = damage.ToString();
        color = text.color;

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
            text.fontSize = 5f * EaseOutElastic((Time.time - createTime) / bounceTime);
        }

        // 데미지 텍스트 위로 이동
        transform.Translate(new Vector3(0, speed * Time.deltaTime, 0));

        // 글자색 알파값 감소
        color.a = Mathf.Lerp(color.a, 0, Time.deltaTime * alphaSpeed);
        text.color = color;
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
