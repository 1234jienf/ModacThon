using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plant : MonoBehaviour
{
    private GameMgr gameMgr;

    public SOItem soItem;

    [HideInInspector]
    public int spawnPointId; // 해당 채집품이 스폰된 SpawnPoint의 Id

    private void Awake() {
        gameMgr = GameMgr.Instance;
    }

    public void Get() {
        PlantSpawnManager spawnManager 
            = GameObject.Find(gameMgr.currentField)
                .transform
                .Find("PlantSpawnManager")
                .GetComponent<PlantSpawnManager>();
        spawnManager.Gathering(spawnPointId);
        Destroy(gameObject);
    }
}
