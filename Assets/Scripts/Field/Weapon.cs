using UnityEngine;

public class Weapon : MonoBehaviour
{
    private float createAt; // 탄막 생성 시간
    public Vector3 dir;
    [SerializeField]
    private float lifeTime; // 탄막 수명

    [SerializeField]
    private float moveSpeed; // 탄막 속도

    // Start is called before the first frame update
    void Start()
    {
        createAt = Time.time;
    }

    public void Initialize(Vector3 direction)
    {
        dir = direction;
    }

    // Update is called once per frame
    void Update()
    {
        // 지정된 수명이 지나면 소멸
        if (Time.time - createAt >= lifeTime) {
            Destroy(gameObject);
        }

        transform.position += moveSpeed * Time.deltaTime * dir;
    }

    // 탄막에 부딫힌 오브젝트가 있으면 특정 이벤트 수행
    private void OnTriggerEnter2D(Collider2D other) {
        // 탄막이 몬스터나 경계에 닿으면 소멸
        if(other.CompareTag("Monster") || other.CompareTag("Border")) {
            Destroy(gameObject);
        }
    }
}
