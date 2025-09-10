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
        if (container.gameObject.activeSelf == false) return;

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
