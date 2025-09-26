using System;
using UnityEngine;
using UnityEngine.UI;

public class FocusFunctionFieldUI : MonoBehaviour
{
    public bool isEdit = false;
    [SerializeField] private Transform container;
    [SerializeField] private Button btn;
    private Action clickButtonCallback;
    [Header("UI Component")]
    [SerializeField] BaseAnimUI[] UIList;
    private void Awake()
    {
        btn.onClick.AddListener(OnClickButton);

        container.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        btn.onClick.RemoveListener(OnClickButton);
    }

    public void Open(Action callback)
    {
        isEdit = true;
        container.gameObject.SetActive(true);
        foreach(var UI in UIList)
        {
            UI.Close();
        }
        clickButtonCallback = callback;
    }

    public void Close()
    {
        isEdit = false;
        container.gameObject.SetActive(false);

        foreach (var UI in UIList)
        {
            UI.Open();
        }
    }

    private void OnClickButton()
    {
        Debug.Log("On Click Focus Function Field");
        clickButtonCallback?.Invoke();
        clickButtonCallback = null;

        Close();
    }
}
