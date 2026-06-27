using UnityEngine;
using UnityEngine.Tilemaps;
public class MainCamera : MonoBehaviour
{
    private GameMgr gameMgr; // GameManager
    private bool gameMgrWarningLogged;

    [SerializeField]
    private Transform target; // 카메라의 목표물

    [SerializeField]
    private Transform startPoint; // 카메라의 위치 기본값

    [SerializeField]
    private float boundaryWeight; // 실질적 카메라 경계, 플레이어가 이를 넘어가려고 하면 카메라가 이동

    // 카메라에 보여지는 화살표가 그려진 캔버스
    [SerializeField]
    private Canvas arrowCanvas;

    // 카메라의 너비와 높이 (World Point 단위)
    private float cameraWidth;
    private float cameraHeight;

    void Awake() {
        gameMgr = FindObjectOfType<GameMgr>();
    }

    private bool TrySetGameMgr()
    {
        if (gameMgr == null)
        {
            gameMgr = FindObjectOfType<GameMgr>();
        }

        if (gameMgr == null)
        {
            if (!gameMgrWarningLogged)
            {
                Debug.LogWarning("GameMgr이 없어서 카메라 필드 처리를 건너뜁니다.");
                gameMgrWarningLogged = true;
            }

            return false;
        }

        gameMgrWarningLogged = false;
        return true;
    }

    // Start is called before the first frame update
    void Start()
    {   
        // 설정한 목표물이 없으면 리턴
        if (target == null) return;
        if (!TrySetGameMgr()) return;

        // 저장된 위치 값이 있고 적용 시각이 0 초과 12미만이라면 그 쪽으로 보내고 없으면 초기 위치로 보낸다.
        if (gameMgr.lastXy != null && gameMgr.lastXy.Length >= 2 && 0 < gameMgr.time && gameMgr.time < 12) {
            transform.position = new Vector3((float)gameMgr.lastXy[0], (float)gameMgr.lastXy[1], transform.position.z);
        }
        else {
            transform.position = new Vector3(startPoint.position.x, startPoint.position.y, transform.position.z);
        }

        // 카메라의 너비와 높이 계산 (World Point 단위)
        (cameraWidth, cameraHeight) = CalcCameraSize();
    }

    // Update is called once per frame
    void Update()
    {
        // 설정한 목표물이 없으면 리턴
        if (target == null) return;

        // 오프셋 계산
        Vector3 offset = transform.position - target.position;
        offset.x = Mathf.Clamp(offset.x, - boundaryWeight / 2 * cameraWidth, boundaryWeight / 2 * cameraWidth);
        offset.y = Mathf.Clamp(offset.y, - boundaryWeight / 2 * cameraHeight, boundaryWeight / 2 * cameraHeight);

        // 목표 위치 계산
        Vector3 nextPos = target.position + offset;

        // 카메라를 목표 위치로 이동
        transform.position = nextPos;

        // 카메라의 위치에따라 화살표 상태 업데이트
        ControlArrows();
    }

    // 카메라의 크기 계산 (World Point 단위)
    private (float, float) CalcCameraSize() {
        Vector3 rightUp = Camera.main.ViewportToWorldPoint(new Vector3(1f, 1f, 0));
        Vector3 leftDown = Camera.main.ViewportToWorldPoint(new Vector3(0f, 0f, 0));

        return (rightUp.x - leftDown.x, rightUp.y - leftDown.y);
    }

    // 카메라의 위치에 따라 화살표 컨트롤
    private void ControlArrows() {
        if (!TrySetGameMgr()) return;
        if (string.IsNullOrEmpty(gameMgr.currentField)) return;

        // 현재 플레이어가 위치한 타일맵을 얻는다.
        Debug.Log($"currentField: [{gameMgr.currentField}]");

        GameObject field = GameObject.Find(gameMgr.currentField);
        Debug.Log($"field: {field}");
        if (field == null) return;

        Transform ground = field.transform.Find("Ground");
        Debug.Log($"ground: {ground}");
        if (ground == null) return;

        Tilemap tilemap = ground.GetComponent<Tilemap>();
        Debug.Log($"tilemap: {tilemap}");
        if (tilemap == null) return;

        if (arrowCanvas == null) {
            Debug.Log("arrowCanvas is null");
            return;
        }

        // 타일맵의 바운더리와 ArrowController를 얻고
        BoundsInt bounds = tilemap.cellBounds;
        ArrowController arrowController = arrowCanvas.GetComponent<ArrowController>();
        Debug.Log($"arrowController: {arrowController}");
        if (arrowController == null) return;

        // 카메라의 위치에 따라 화살표의 상태를 변경한다.
        arrowController.ChangeArrowEnabled(
            ArrowController.UP, 
            transform.position.y + cameraHeight / 2 <= tilemap.transform.position.y + bounds.yMax
        );
        arrowController.ChangeArrowEnabled(
            ArrowController.RIGHT, 
            transform.position.x + cameraWidth / 2 <= tilemap.transform.position.x + bounds.xMax
        );
        arrowController.ChangeArrowEnabled(
            ArrowController.DOWN, 
            transform.position.y - cameraHeight / 2 >= tilemap.transform.position.y + bounds.yMin
        );
        arrowController.ChangeArrowEnabled(
            ArrowController.LEFT, 
            transform.position.x - cameraWidth / 2 >= tilemap.transform.position.x + bounds.xMin
        );
    }
}
