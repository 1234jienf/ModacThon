using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager instance; // Declare the singleton instance

    public GameObject target; // 카메라가 따라갈 대상
    public float moveSpeed; // 카메라가 따라갈 속도
    private Vector3 targetPosition; //대상의 현재 위치값
    // Start is called before the first frame update
    public BoxCollider2D Bound;
    private Vector3 minBound;
    private Vector3 maxBound;
    // 박스 콜라이더 영역의 최대 최소 xy 값을 지님

    private float halfWidth;
    private float halfHeight;

    private Camera theCamera;

    private void Awake() 
    {
        if (instance != null)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }
    }
    void Start()
    {
        theCamera = GetComponent<Camera>();
        minBound = Bound.bounds.min;
        maxBound = Bound.bounds.max;
        halfHeight = theCamera.orthographicSize;
        halfWidth = halfHeight * Screen.width / Screen.height;

    }

    // Update is called once per frame
    void Update()
    {
        if (target.gameObject != null)
        {
            targetPosition.Set(target.transform.position.x, target.transform.position.y, this.transform.position.z);

            this.transform.position = Vector3.Lerp(this.transform.position, targetPosition, moveSpeed * Time.deltaTime);
            
            // float clampedX = Mathf.Clamp(this.transform.position.x, minBound.x + halfWidth, maxBound.x - halfWidth);
            // float clampedY = Mathf.Clamp(this.transform.position.y, minBound.y + halfHeight, maxBound.y - halfHeight);
        
            // this.transform.position = new Vector3(clampedX, clampedY, this.transform.position.z);
        


        }


    }
}
