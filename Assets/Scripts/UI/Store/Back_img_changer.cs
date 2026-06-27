using UnityEngine;


public class Back_img_changer : MonoBehaviour
{
    public Sprite bakcground_1;
    public Sprite bakcground_2;
    private void Start()
    {
        SpriteRenderer spriterenderer = GetComponent<SpriteRenderer>();
        if (GameMgr.Instance.time >= 0 && GameMgr.Instance.time < 12)
        {
            spriterenderer.sprite = bakcground_1;
        }
        else
        {
            spriterenderer.sprite = bakcground_2;
        }
    }
    
}
