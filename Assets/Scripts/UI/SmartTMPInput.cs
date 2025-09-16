using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SmartTMPInput : MonoBehaviour
{
    private Button btn;
    private TMP_InputField inputField;
    private void Awake()
    {
        btn = GetComponent<Button>();
        inputField = GetComponentInChildren<TMP_InputField>();

        //btn.onClick.AddListener(OnClickInputField);
    }

    private void OnClickInputField()
    {
        inputField.ActivateInputField();
    }
}
