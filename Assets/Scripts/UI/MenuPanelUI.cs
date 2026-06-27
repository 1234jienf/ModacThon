using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class MenuPanelUI : MonoBehaviour
{
    public TMP_Text MenuNameText;
    public TMP_Text MenuIngredientText;

    public Sprite LoadImage(string filePath)
    {
        if (File.Exists(filePath))
        {
            byte[] imageBytes = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(imageBytes))
            {
                Sprite sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                return sprite;
            }
            else
            {
                Debug.LogError("이미지 로드 실패: 이미지를 텍스처로 변환할 수 없습니다.");
                return null;
            }
        }
        else
        {
            Debug.LogError("이미지 로드 실패: 파일이 존재하지 않습니다. 경로를 확인하세요. 경로: " + filePath);
            return null;
        }
    }

    public void SetItem(string ImgURL, string name, string Ingredient)
    {
        Image image = GetComponent<Image>();

        if (image != null && ImgURL != null)
        {
            // Image 컴포넌트의 sprite를 새로운 스프라이트로 변경합니다.
            Sprite newimage = LoadImage(ImgURL);
            image.sprite = newimage;
        }

        MenuNameText.text = name;
        MenuIngredientText.text = Ingredient;
    }
}
