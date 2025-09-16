using TMPro;
using UnityEngine;

public class BottomSheetInputUI : BottomSheetUI
{
    private float prevousHeight;
    private float timer;
    protected override void Update()
    {
        base.Update();
        if (container.gameObject.activeSelf == false) return;

        float height = KeyboardHeight.GetHeight();
        if (prevousHeight != height)
        {
            timer = 0.2f; // Reset timer khi chiều cao thay đổi
            prevousHeight = height;
        }
        else
        {
            timer -= Time.deltaTime;
            if (timer > 0)
                return; // Chưa đủ thời gian ổn định, không cập nhật UI
        }
        float scaleHeight = height * ((RectTransform)rectContainer.parent).rect.height / Screen.height;
        Vector3 lerpPosition = Vector3.Lerp(rectContainer.anchoredPosition, openPos + new Vector2(0, scaleHeight), Time.deltaTime * 10);
        rectContainer.anchoredPosition = lerpPosition;
        //Debug.Log($"On Show Keyboard: {TouchScreenKeyboard.visible} {TouchScreenKeyboard.area}");
    }

    public void OnInputFocus()
    {
        float height = KeyboardHeight.GetHeight();
        float scaleHeight = height * ((RectTransform)rectContainer.parent).rect.height / Screen.height;
        PlayAnim(openPos + new Vector2(0, scaleHeight), openDuration, showEase);
        Debug.Log($"On Input Focus: {height} {scaleHeight} {TouchScreenKeyboard.visible}");
    }

   
}

public enum UIType
{
    None = 0,
    InputField = 5,
    TextMeshProUGUI = 10,
}
