using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlantSpawnPoint : MonoBehaviour
{
    public int id; // 스폰 포인트 id

    [SerializeField]
    private GameObject plant; // 스폰될 채집품

    [SerializeField]
    private float spawnTime; // 채집 이후 스폰되는데까지 걸리는 시간

    private float latestGatheringTime; // 제일 최근에 채집한 시간
    private bool isSpawn; // 스폰 여부

    // Start is called before the first frame update
    void Start()
    {
        isSpawn = false;
        latestGatheringTime = Time.time;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isSpawn && Time.time - latestGatheringTime >= spawnTime) {
            GameObject plantObj = Instantiate(plant, transform.position, Quaternion.identity);
            plantObj.GetComponent<Plant>().spawnPointId = id;
            latestGatheringTime = Time.time;
            isSpawn = true;
        }
    }

    // 채집
    public void Gathering() {
        latestGatheringTime = Time.time;
        isSpawn = false;
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}
