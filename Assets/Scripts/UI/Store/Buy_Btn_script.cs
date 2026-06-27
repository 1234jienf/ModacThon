using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Buy_Btn_script : MonoBehaviour
{
    public Button myButton;
    public BagControl bag;
    public ShopManager sm;
    void Start()
    {
        myButton.onClick.AddListener(bag.BuyItems);
        myButton.onClick.AddListener(sm.updateSell);
    }
}
