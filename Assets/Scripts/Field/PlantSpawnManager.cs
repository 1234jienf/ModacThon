using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlantSpawnManager : MonoBehaviour
{
    [SerializeField]
    private PlantSpawnPoint[] spawnPoints; // 채집품 스폰포인트들

    // spawnPointId를 아이디 값으로 가지는 SpawnPoint에서 스폰된 채집품이 채집됨
    public void Gathering(int spawnPointId) {
        foreach(PlantSpawnPoint spawnpoint in spawnPoints) {
            Debug.Log(spawnpoint.id);
        }
        int index = Array.FindIndex(spawnPoints, spawnPoint => spawnPoint.id == spawnPointId);
        spawnPoints[index].Gathering();
    }
}
