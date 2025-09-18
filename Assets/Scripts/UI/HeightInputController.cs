using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeightInputController : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button minusBtn;
    [SerializeField] private Button plusBtn;

    [SerializeField] private float maxHeight = 300;
    [SerializeField] private float currentHeight;

    private float minHeight = 0.1f;

    private void Awake()
    {
        minusBtn.onClick.AddListener(DecreaseHeight);
        plusBtn.onClick.AddListener(IncreaseHeight);

    }

    private void OnDestroy()
    {
        minusBtn.onClick.RemoveListener(DecreaseHeight);
        plusBtn.onClick.RemoveListener(IncreaseHeight);

        inputField.onValidateInput = ValidateChar;
    }

    private char ValidateChar(string text, int charIndex, char addedChar)
    {
        // Ví dụ: chỉ cho nhập số và dấu chấm
        if (char.IsDigit(addedChar) || addedChar == '.')
            return addedChar;

        if (addedChar == '.' && text.Contains("."))
            return '\0';

        int dotIndex = text.IndexOf('.');
        if (dotIndex >= 0 && char.IsDigit(addedChar))
        {
            int decimals = text.Length - dotIndex - 1;
            if (charIndex > dotIndex && decimals >= 2)
                return '\0';
        }

        // Nếu không hợp lệ, trả về '\0' (ký tự null) => TMP bỏ qua
        return '\0';
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
        inputField.text = $"{currentHeight:F1}";
    }
}