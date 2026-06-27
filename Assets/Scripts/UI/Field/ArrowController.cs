using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ArrowController : MonoBehaviour
{
    public static int UP = 0;
    public static int RIGHT = 1;
    public static int DOWN = 2;
    public static int LEFT = 3;

    // 화살표들
    private Image[] arrows;
    
    // 화살표들의 활성화 상태
    private bool[] isActive;

    void Awake() {
        Image arrowUp = transform.Find("ArrowUp").GetComponent<Image>();
        Image arrowRight = transform.Find("ArrowRight").GetComponent<Image>();
        Image arrowDown = transform.Find("ArrowDown").GetComponent<Image>();
        Image arrowLeft = transform.Find("ArrowLeft").GetComponent<Image>();

        arrows = new Image[]{arrowUp, arrowRight, arrowDown, arrowLeft};
        isActive = new bool[4]{false, false, false, false};
    }

    // Update is called once per frame
    void Update() {
        // isActive에 맞춰서 화살표를 업데이트 시켜준다.
        for (int i = 0; i < 4; i++) {
            arrows[i].enabled = isActive[i];
        }
    }
    
    // 화살표 활성화 상태 변경
    public void ChangeArrowEnabled(int dir, bool enabled) {
        isActive[dir] = enabled;
    }
}
