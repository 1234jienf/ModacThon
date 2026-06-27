using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class DialogTest : MonoBehaviour
{
	[SerializeField]
	private	DialogSystem	DialogSystem01;
	[SerializeField]
	private DialogSystem	Dialog0;
	[SerializeField]
	private DialogSystem	Dialog2;
	[SerializeField]
	private DialogSystem	Dialog3;
	[SerializeField]
	private DialogSystem	Dialog4;
	[SerializeField]
	private DialogSystem	Dialog5;
	[SerializeField]
	private DialogSystem	Dialog6;

	GameObject Background1;
	GameObject Year2024;
	GameObject Year1524;
	GameObject Chosun_Hanyang;
	GameObject Chosun_Merchant;
	GameObject Watch;
	
	private IEnumerator Start()
	{
		Background1 = GameObject.Find("Background1_Fade");
		Year1524 = GameObject.Find("Fade_1524");
		Year2024 = GameObject.Find("Fade_2024");
		Chosun_Hanyang = GameObject.Find("Fade_Chosun_Hanyang");
		Chosun_Merchant = GameObject.Find("Fade_Chosun_merchant");
		Watch = GameObject.Find("Fade_Watch");

		yield return new WaitUntil(()=>Dialog0.UpdateDialog());
		
		Background1.GetComponent<FadeInEffect>().StartFadeOut();
		yield return new WaitForSeconds(2f);
		yield return new WaitUntil(()=>DialogSystem01.UpdateDialog());
		Background1.GetComponent<FadeInEffect>().StartFadeIn();
		yield return new WaitForSeconds(4f);
		Year2024.GetComponent<FadeInEffect>().StartFadeOut();
		yield return new WaitForSeconds(1f);
		Year1524.GetComponent<FadeInEffect>().StartFadeOut();
		Year2024.GetComponent<FadeInEffect>().StartFadeIn();
		yield return new WaitForSeconds(2f);
		Year1524.GetComponent<FadeInEffect>().StartFadeIn();

		yield return new WaitForSeconds(2f);
		Chosun_Hanyang.GetComponent<FadeInEffect>().StartFadeOut();
		yield return new WaitForSeconds(2f);
		yield return new WaitUntil(()=>Dialog2.UpdateDialog());
		
		Chosun_Merchant.GetComponent<FadeInEffect>().StartFadeOut();
		yield return new WaitForSeconds(2f);
		yield return new WaitUntil(()=>Dialog3.UpdateDialog());

		Watch.GetComponent<FadeInEffect>().StartFadeOut();
		yield return new WaitForSeconds(3f);
		Watch.GetComponent<FadeInEffect>().StartFadeIn();
		yield return new WaitForSeconds(2f);

		yield return new WaitUntil(()=>Dialog4.UpdateDialog());

		Chosun_Merchant.GetComponent<FadeInEffect>().StartFadeIn();
		yield return new WaitForSeconds(2f);

		yield return new WaitUntil(()=>Dialog5.UpdateDialog());

		Chosun_Hanyang.GetComponent<FadeInEffect>().StartFadeIn();
		yield return new WaitForSeconds(2f);

		yield return new WaitUntil(()=>Dialog6.UpdateDialog());

		yield return new WaitForSeconds(2f);
		SceneManager.LoadScene("Main");
	}
}

