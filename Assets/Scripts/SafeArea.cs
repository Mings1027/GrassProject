using UnityEngine;

public class SafeArea : MonoBehaviour
{
    private void Start()
    {
        Init();
    }

    private void Init()
    {
        var safeAreaMin = Screen.safeArea.min;
        if (safeAreaMin.x <= 0) return;
        var rect = GetComponent<RectTransform>();
        var parentRect = transform.parent.GetComponent<RectTransform>();
        var thisRect = parentRect.rect;

        rect.anchorMin = new Vector2(0.5f, 1);
        rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);

        thisRect.width -= safeAreaMin.x * 2;
        thisRect.height -= Screen.safeArea.position.y;

        rect.sizeDelta = new Vector2(thisRect.width, thisRect.height);
    }
}