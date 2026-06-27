using UnityEngine;
using UnityEngine.SceneManagement;

public class SkipTutorial : MonoBehaviour
{
    public void SkipTutorialScene()
    {
        // "Main"을 다음 씬의 이름으로 가정
        SceneManager.LoadScene("Main");
    }
}
