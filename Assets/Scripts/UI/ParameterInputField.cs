using TMPro;
using UnityEngine;

public class ParameterInputField : MonoBehaviour
{
    public IntParameterType parameterType;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TextMeshProUGUI inputPlaceHolder;

 
    public TMP_InputField InputField => inputField;
  
    public void Initialize(string labelText, string placeholderText, TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard)
    {
        label.text = labelText;
        inputPlaceHolder.text = placeholderText;
        inputField.text = "0";
        inputField.contentType = contentType;
    }

    public void SetValue(float value)
    {
        inputField.text = value.ToString();
    }

    public void ResetValue()
    {
        inputField.text = "";
    }
}