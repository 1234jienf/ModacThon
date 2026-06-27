using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeInEffect : MonoBehaviour
{
    public Image fadeImage; // 페이드 효과를 줄 UI Image. 인스펙터에서 할당해야 합니다.
    public float fadeDuration; // 페이드인/아웃에 걸리는 시간

    void OnEnable()
    {
        Time.timeScale = 1; // 씬 전환 후 Time.timeScale을 1로 설정
    }

    // 페이드인 코루틴
    IEnumerator FadeIn()
    {
        float elapsedTime = 0f;
        Color color = fadeImage.color;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration); // 1에서 0으로 알파값을 변경
            fadeImage.color = color;
            yield return null;
        }
    }

    // 페이드아웃 코루틴
    IEnumerator FadeOut()
    {
        float elapsedTime = 0f;
        Color color = fadeImage.color;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration); // 0에서 1로 알파값을 변경
            fadeImage.color = color;
            yield return null;
        }
    }

    // 외부에서 페이드인/아웃을 시작할 수 있는 메서드
    public void StartFadeIn()
    {
        StartCoroutine(FadeIn());
    }

    public void StartFadeOut()
    {
        StartCoroutine(FadeOut());
    }
}