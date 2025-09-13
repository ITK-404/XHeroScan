using TMPro;
using UnityEngine;

public class BottomSheetInputUI : BottomSheetUI
{
    private float delayTime = 1;
    private bool previousState;


    protected override void Update()
    {
        base.Update();
        if (container.gameObject.activeSelf == false) return;

        //bool isVisible = TouchScreenKeyboard.visible;

        //if (isVisible != previousState && container.gameObject.activeSelf)
        //{
        //    if (delayTime > 0)
        //    {
        //        delayTime -= Time.deltaTime;
        //        return;
        //    }

        //    previousState = TouchScreenKeyboard.visible;
        //    delayTime = 0.5f;
        //    OnInputFocus();
        //}
        float height = KeyboardHeight.GetHeight();
        float scaleHeight = height * ((RectTransform)rectContainer.parent).rect.height / Screen.height;
        Vector3 lerpPosition = Vector3.Lerp(rectContainer.anchoredPosition, openPos + new Vector2(0, scaleHeight), Time.deltaTime * 10);
        rectContainer.anchoredPosition = lerpPosition;
        Debug.Log($"On Show Keyboard: {TouchScreenKeyboard.visible} {TouchScreenKeyboard.area}");
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
