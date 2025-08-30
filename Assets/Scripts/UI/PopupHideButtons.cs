using System;
using UnityEngine;
using UnityEngine.UI;

public class PopupHideButtons : MonoBehaviour
{
    [SerializeField] private Button[] buttons;

    public Action OnClickBtnHide;
    
    private void Awake()
    {
        buttons = GetComponentsInChildren<Button>(includeInactive:true);

        foreach (var button in buttons)
        {
            button.onClick.AddListener(HidePopup);
        }
    }

    private void HidePopup()
    {
        OnClickBtnHide?.Invoke();
    }
}