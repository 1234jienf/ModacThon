using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecipeDropItem : MonoBehaviour
{
    [HideInInspector]
    public SOItem soItem;
    
    public void Get() {
        Destroy(gameObject);
    }
}
