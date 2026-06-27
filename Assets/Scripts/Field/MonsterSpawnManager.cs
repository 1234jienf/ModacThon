using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawnManager : MonoBehaviour
{
    [SerializeField]
    private Transform[] spawnPoints; // 스폰 위치들

    [SerializeField]
    private GameObject[] monsters; // 소환될 몬스터들

    [SerializeField]
    private float spawnInterval = 3f; // 스폰 간격

    [SerializeField]
    private int maxSpawn = 10; // 해당 필드에서 소환할 최대 몬스터 수
    
    public int monsterCount = 0; // 현재 소환된 몬스터 수
    
    private float latestSpawnTime; // 최근 몬스터 소환 시각

    void Start() {
        latestSpawnTime = Time.time;
    }

    // Update is called once per frame
    void Update()
    {
        // 스폰 쿨타임이 지나면 스폰을 시도한다.
        if (Time.time - latestSpawnTime >= spawnInterval) {
            // 현재 소환된 몬스터의 수가 최대 스폰 수보다 작으면 스폰 시도
            if (monsterCount < maxSpawn) {
                int spawnPointIndex = Random.Range(0, spawnPoints.Length);
                int monsterIndex = Random.Range(0, monsters.Length);
                SpawnMonster(spawnPointIndex, monsterIndex);
                latestSpawnTime = Time.time;
            }
        }
    }

    // 몬스터 소환
    void SpawnMonster(int spawnPointIndex, int monsterIndex) {
        Debug.Log("몬스터 소환! " + Time.time + " " + monsterCount);
        Instantiate(monsters[monsterIndex], spawnPoints[spawnPointIndex].position, Quaternion.identity);
        monsterCount++;
    }
}
