using UnityEngine;
using UnityEngine.UI;

public static class BridgeUiFactory
{
    public static Canvas GetOrCreateCanvas(string canvasName, int sortingOrder)
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        foreach (Canvas existing in canvases)
        {
            if (existing != null && existing.gameObject.name == canvasName)
            {
                if (existing.GetComponent<GraphicRaycaster>() == null)
                    existing.gameObject.AddComponent<GraphicRaycaster>();
                return existing;
            }
        }

        GameObject canvasObject = new GameObject(canvasName);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}
