using UnityEngine;

public class BridgeCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private bool snapOnStart = true;

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
        if (snapOnStart && target != null)
            SnapToTarget();
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        if (snapOnStart && target != null)
            SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }

    private void SnapToTarget()
    {
        transform.position = target.position + offset;
    }
}
