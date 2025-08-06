using UnityEngine;
public class ToggleGroupHeaderUI : MonoBehaviour
{
    [SerializeField] private ToggleButtonHeaderUI[] toggleButtons;

    private StuctureHeaderType globalType;
    
    private void Awake()
    {
        toggleButtons = GetComponentsInChildren<ToggleButtonHeaderUI>();
        
        foreach (ToggleButtonHeaderUI item in toggleButtons)
        {
            item.btn.onClick.AddListener(() =>
            {
                OnSelectThis(item);
            });   
        }

        OnSelectThis(toggleButtons[0]);
    }

    private void OnSelectThis(ToggleButtonHeaderUI btn)
    {
        foreach (var item in toggleButtons)
        {
            var isActive = item == btn;
            item.ChangeState(isActive ? ToggleButtonUIBase.State.Active : ToggleButtonUIBase.State.DeActive);
            if (isActive)
            {
                globalType = item.localType;
            }
        }
    }
}
public enum StuctureHeaderType
{
    Cong_Trinh,
    Phan_Cung,
    Luan_Giai,
    Hoa_Giai
}