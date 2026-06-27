using UnityEngine;
using UnityEngine.UI;

public class DataReceiver : MonoBehaviour
{
    public Slider ExpSlider; // Unity 에디터에서 연결할 Slider UI 요소
    public Slider HpSlider; // Unity 에디터에서 연결할 Slider UI 요소
    private readonly int maxLevel = 50; // 만렙

    void Start()
    {
        if (GameMgr.Instance != null)
        {
            // GameMgr에서 hp와 exp, lv 값을 가져옵니다.
            int hp = GameMgr.Instance.hp;
            int exp = GameMgr.Instance.exp;
            int lv = GameMgr.Instance.lv;

            //최대 체력과 요구 경험치 계산
            int maxHp = CalculateMaxHp();
            int needExp = CalculateNeedExp();

            Debug.Log($"HP: {hp}, EXP: {exp}");

            // 가져온 값을 슬라이더에 설정합니다.
            ExpSlider.value = lv == maxLevel ? 1f : 1f * exp / needExp; // expSlider의 value를 GameMgr의 exp 값으로 설정
            HpSlider.value = 1f * hp / maxHp; // hpSlider의 value를 GameMgr의 hp 값으로 설정
        }
        else
        {
            Debug.LogError("GameMgr 인스턴스를 찾을 수 없습니다.");
        }
    }

    // 최대 체력 계산
    private int CalculateMaxHp() {
        return 500 + (GameMgr.Instance.lv - 1) * 50;
    }

    // 최대 경험치 계산
    private int CalculateNeedExp() {
        return GameMgr.Instance.lv  == maxLevel ? 0 : Mathf.FloorToInt(100 * Mathf.Pow(1.15f, GameMgr.Instance.lv - 1));
    }
}
