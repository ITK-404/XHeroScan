using UnityEngine;
using UnityEngine.UI;

public class PopupToggleHandle : MonoBehaviour
{
    [SerializeField] private Button toggleBtn;
    [SerializeField] private FadePopupUI popupUI;
    [SerializeField] private PopupHideButtons popupHideButtons;
    [SerializeField] private bool isToggle;
    
    private void Start()
    {
        toggleBtn.onClick.AddListener(Toggle);
        popupHideButtons.OnClickBtnHide = () => { ToggleByState(false); };
    }

    private void OnValidate()
    {
        if (!popupUI)
            popupUI = GetComponent<FadePopupUI>();

        if (!popupHideButtons)
            popupHideButtons = GetComponent<PopupHideButtons>();
    }

    private void OnDestroy()
    {
        toggleBtn.onClick.RemoveListener(Toggle);
    }

    private void Toggle()
    {
        isToggle = !isToggle;
        ToggleByState(isToggle);
    }

    public void ToggleByState(bool state)
    {
        if (state)
        {
            popupUI.Open();
        }
        else
        {
            popupUI.Close();
        }

        isToggle = state;
    }
}