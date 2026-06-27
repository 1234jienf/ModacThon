using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }
    public InventoryControl inventoryControl;

    public int weapon;
    public int vehicle;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            inventoryControl = GetComponentInChildren<InventoryControl>();
            vehicle = 0;
            weapon = 0;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

}
