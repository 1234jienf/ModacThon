using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class StoryDialog_Quest1 : MonoBehaviour
{
	[SerializeField]
	private DialogSystem	Dialog0;

	GameObject Background1;
	
	private IEnumerator Start()
	{
		Background1 = GameObject.Find("ForFade");
		Background1.GetComponent<FadeInEffect>().StartFadeIn();
		yield return new WaitForSeconds(3f);
		yield return new WaitUntil(()=>Dialog0.UpdateDialog());
		Background1.GetComponent<FadeInEffect>().StartFadeOut();

		yield return new WaitForSeconds(3f);
		SceneManager.LoadScene("Main");
	}
}

