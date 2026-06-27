using UnityEngine;
using System.Text;

public class MapDebugGenerator : MonoBehaviour
{
    [Header("참조할 타일맵 프로바이더")]
    public TilemapDataProvider targetMapProvider;

    void Start() {
        GenerateAndShowMapDebug();
    }

    [ContextMenu("맵 구조 디버그 창에 출력")]
    public void GenerateAndShowMapDebug()
    {
        if (targetMapProvider == null)
        {
            Debug.LogError("targetMapProvider가 지정되지 않았습니다!");
            return;
        }

        // 프로바이더 코드를 통해 맵 데이터 매릭스를 얻어옴
        char[,] mapMatrix = targetMapProvider.GetMapMatrix();
        
        int height = mapMatrix.GetLength(0);
        int width = mapMatrix.GetLength(1);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"\n=== [{targetMapProvider.gameObject.name}] 변환된 맵 구조 ===");

        // 유니티 타일맵은 y축이 아래에서 위로 증가하지만, 
        // 텍스트로 출력할 때는 상단(yMax)부터 역순으로 내려오며 그려야 시각적으로 올바르게 보입니다.
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
                sb.Append(mapMatrix[y, x]);
            
            if (y > 0) sb.AppendLine(); // 다음 줄로 변경
        }

        sb.AppendLine("\n=======================================");

        // 콘솔 창에 결과 출력
        Debug.Log(sb.ToString());
    }
}