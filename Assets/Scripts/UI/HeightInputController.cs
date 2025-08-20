using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeightInputController : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button minusBtn;
    [SerializeField] private Button plusBtn;

    [SerializeField] private float minHeight = 0;
    [SerializeField] private float maxHeight = 300;

    [SerializeField] private float currentHeight;


    private void Awake()
    {
        minusBtn.onClick.AddListener(DecreaseHeight);
        plusBtn.onClick.AddListener(IncreaseHeight);

        inputField.onValueChanged.AddListener(OnHeightInputChanged);
    }

    private void OnDestroy()
    {
        minusBtn.onClick.RemoveListener(DecreaseHeight);
        plusBtn.onClick.RemoveListener(IncreaseHeight);

        inputField.onValueChanged.RemoveListener(OnHeightInputChanged);
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

    void OnHeightInputChanged(string input)
    {
        if (int.TryParse(input, out int newHeight))
        {
            currentHeight = Mathf.Clamp(newHeight, minHeight, maxHeight);
            inputField.text = currentHeight.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            inputField.text = currentHeight.ToString(CultureInfo.InvariantCulture);
        }
    }
}