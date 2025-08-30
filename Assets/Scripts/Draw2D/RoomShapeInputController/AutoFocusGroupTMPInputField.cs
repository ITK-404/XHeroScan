using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class AutoFocusGroupTMPInputField : MonoBehaviour
{
    [SerializeField] private TMP_InputField[] inputFieldList;

    private void OnEnable()
    {
        if (inputFieldList == null || inputFieldList.Length == 0)
        {
            inputFieldList = GetComponentsInChildren<TMP_InputField>();
        }

        foreach (var item in inputFieldList)
        {
            if (string.IsNullOrWhiteSpace(item.text))
            {
                StartCoroutine(FocusLengthInputNextFrame(item));
                break;
            }
        }   
    }
    private IEnumerator FocusLengthInputNextFrame(TMP_InputField inputField)
    {
        yield return null; // Đợi 1 frame

        if (inputField != null)
        {
            // Set selected game object để Unity UI focus đúng
            EventSystem.current.SetSelectedGameObject(inputField.gameObject);
            inputField.OnPointerClick(new PointerEventData(EventSystem.current)); // kích hoạt caret
        }
    }
}