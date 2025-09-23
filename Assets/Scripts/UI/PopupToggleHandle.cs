using UnityEngine;
using UnityEngine.UI;

public class PopupToggleHandle : MonoBehaviour
{
    [SerializeField] private Button toggleBtn;
    [SerializeField] private BaseAnimUI popupUI;
    [SerializeField] private PopupHideButtons popupHideButtons;
    [SerializeField] private bool isToggle;
    
    private void Start()
    {
        toggleBtn.onClick.AddListener(Toggle);
        if(popupHideButtons)
            popupHideButtons.OnClickBtnHide = () => { ToggleByState(false); };

        ToggleByState(isToggle);  
    }

    private void OnValidate()
    {
        if (!popupUI)
            popupUI = GetComponent<BaseAnimUI>();

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

    public void SetToggle(bool state)
    {
        isToggle = state;
    }
}