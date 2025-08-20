using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeightInputController : MonoBehaviour
{
    private TMP_InputField inputField;
    private Button minusBtn;
    private Button plusBtn;

    private float minHeight = 0;
    private float maxHeight = 300;

    private float currentHeight;


    private void Awake()
    {
        minusBtn.onClick.AddListener(DecreaseHeight);
        plusBtn.onClick.AddListener(IncreaseHeight);
    }

    private void OnDestroy()
    {
        minusBtn.onClick.RemoveListener(DecreaseHeight);
        plusBtn.onClick.RemoveListener(IncreaseHeight);
    }

    private void IncreaseHeight()
    {
        ChangeHeight(0.1f);
    }

    private void DecreaseHeight()
    {
        ChangeHeight(-0.1f);
    }

    private void ChangeHeight(float value)
    {
        currentHeight += value;
        currentHeight = Mathf.Clamp(currentHeight, minHeight, maxHeight);
        inputField.text = currentHeight.ToString(CultureInfo.InvariantCulture);
    }
}
