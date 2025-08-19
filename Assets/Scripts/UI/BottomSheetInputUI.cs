using TMPro;
using UnityEngine;

public class BottomSheetInputUI : BottomSheetUI
{
    [SerializeField] private TMP_InputField inputField;
    private float delayTime = 1;
    private bool previousState;

    protected override void Update()
    {
        base.Update();
        bool isVisible = TouchScreenKeyboard.visible;

        if (isVisible != previousState && container.gameObject.activeSelf)
        {
            if (delayTime > 0)
            {
                delayTime -= Time.deltaTime;
                return;
            }

            previousState = TouchScreenKeyboard.visible;
            delayTime = 1;
            OnInputFocus();
        }
    }

    public void OnInputFocus()
    {
        float height = KeyboardHeight.GetHeight();
        float scaleHeight = height * ((RectTransform)rectContainer.parent).rect.height / Screen.height;
        PlayAnim(openPos + new Vector2(0, scaleHeight), openDuration, showEase);
        Debug.Log($"On Input Focus: {height} {scaleHeight} {TouchScreenKeyboard.visible}");
    }
}