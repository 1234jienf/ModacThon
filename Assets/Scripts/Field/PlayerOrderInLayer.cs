using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerOrderInLayer : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // �÷��̾��� Y�� ��ġ�� ���� Order in Layer�� �����մϴ�.
        float spriteBottomY = transform.position.y;
        if(gameObject.CompareTag("Player")) {
            spriteBottomY -= spriteRenderer.bounds.extents.y;
        }
        spriteRenderer.sortingOrder = Mathf.RoundToInt(spriteBottomY * -100);
    }
}