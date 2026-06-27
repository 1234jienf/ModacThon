using UnityEngine;

/// <summary>
/// Hackathon bridge 적의 가벼운 배회 AI. Monster.cs처럼 이동/휴식을 반복합니다.
/// </summary>
public class TilemapEnemyRoam : MonoBehaviour
{
    [SerializeField] private float minSpeed = 0.35f;
    [SerializeField] private float maxSpeed = 0.85f;
    [SerializeField] private float moveTime = 2.5f;
    [SerializeField] private float restTime = 1.5f;
    [SerializeField] private float startDelayMax = 1.5f;

    private TilemapEnemyAnimator _animator;
    private TilemapEnemyWalkBounds _walkBounds;
    private Vector3 _moveDir;
    private float _moveSpeed;
    private bool _isMoving;
    private float _moveStartTime;
    private float _restStartTime;
    private float _startDelay;
    private bool _facingRight = true;

    public void Configure(float min, float max, float moveDuration, float restDuration, float delayMax)
    {
        minSpeed = min;
        maxSpeed = max;
        moveTime = moveDuration;
        restTime = restDuration;
        startDelayMax = delayMax;
    }

    public void BindWalkBounds(TilemapEnemyWalkBounds walkBounds)
    {
        _walkBounds = walkBounds;
    }

    private void Awake()
    {
        _animator = GetComponent<TilemapEnemyAnimator>();
        _startDelay = Random.Range(0f, startDelayMax);
        _restStartTime = Time.time;
        _isMoving = false;
        SetWalking(false);
    }

    private void Update()
    {
        if (Time.time < _startDelay)
            return;

        if (_isMoving)
        {
            if (Time.time - _moveStartTime >= moveTime)
            {
                BeginRest();
                return;
            }

            Vector3 nextPosition = transform.position + _moveSpeed * Time.deltaTime * _moveDir;
            if (_walkBounds != null && !_walkBounds.CanWalkTo(nextPosition))
            {
                BeginMove();
                return;
            }

            transform.position = nextPosition;
            ApplyFacing();
        }
        else if (Time.time - _restStartTime >= restTime)
        {
            BeginMove();
        }
    }

    private void BeginMove()
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 probe = transform.position + direction * 0.35f;
            if (_walkBounds != null && !_walkBounds.CanWalkTo(probe))
                continue;

            _moveDir = direction;
            _moveSpeed = Random.Range(minSpeed, maxSpeed);

            if (Mathf.Abs(_moveDir.x) > 0.01f)
                _facingRight = _moveDir.x > 0f;

            _isMoving = true;
            _moveStartTime = Time.time;
            SetWalking(true);
            ApplyFacing();
            return;
        }

        BeginRest();
    }

    private void BeginRest()
    {
        _isMoving = false;
        _restStartTime = Time.time;
        SetWalking(false);
    }

    private void SetWalking(bool walking)
    {
        if (_animator != null)
            _animator.isMoving = walking;
    }

    private void ApplyFacing()
    {
        Vector3 scale = transform.localScale;
        float absX = Mathf.Abs(scale.x) > 0.001f ? Mathf.Abs(scale.x) : 1f;
        scale.x = _facingRight ? absX : -absX;
        transform.localScale = scale;
    }
}
