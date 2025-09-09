using System;
using UnityEngine;
using UnityEngine.UI;

public class PopupHideButtons : MonoBehaviour
{
    [SerializeField] private Button[] buttons;
    [SerializeField] private bool findAllButtonsInChildren;   
    public Action OnClickBtnHide;
    
    private void Awake()
    {
        if(findAllButtonsInChildren)
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