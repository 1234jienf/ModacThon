using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement2D : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 5f;
    private Vector2 moveDirection;
    private Rigidbody2D rb;
    private Animator animator;

    void Start() {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    public float GetAnimatorFloat(string name)
        {
            return animator.GetFloat(name);
        }

    void Update()
    {
    }
    
    public void MoveTo(Vector2 direction)
    {
        moveDirection = direction.normalized;

    }

    void FixedUpdate()
    {
        if (moveDirection != Vector2.zero) {
            rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime);
            animator.SetBool("Walking", true);
            animator.SetFloat("DirX", moveDirection.x);
            animator.SetFloat("DirY", moveDirection.y);

        } else {
            animator.SetBool("Walking", false);

        }
    }

}
