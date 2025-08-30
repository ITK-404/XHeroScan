using UnityEngine;

public class BottomSheetKeyboardHandler : MonoBehaviour
{
    [SerializeField] private RectTransform sheet;
    [SerializeField] private float animDuration = 0.2f;
    [SerializeField] private bool fakeTest;
    private Vector2 basePos;
    
    private void Awake()
    {
        basePos = sheet.anchoredPosition;
    }
    
    private void Update()
    {
        // float height = TouchScreenKeyboard.area.height;
        float height = 500;
        bool isVisible;
        if (fakeTest)
        {
            float scaleHeight = height * ((RectTransform)sheet.parent).rect.height / Screen.height;
            sheet.anchoredPosition = basePos + new Vector2(0, scaleHeight);
            // sheet.DOAnchorPos(basePos + new Vector2(0, scaleHeight), animDuration).SetEase(Ease.OutCubic);

        }
        else
        {
            sheet.anchoredPosition = basePos;
            // sheet.DOAnchorPos(basePos, animDuration).SetEase(Ease.OutCubic);
        }
    }
}