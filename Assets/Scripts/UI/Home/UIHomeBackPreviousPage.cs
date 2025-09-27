using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIHomeBackPreviousPage : MonoBehaviour
{
    public UnityAction OnClickBtnEvent;
    private Button btn;
    private void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClickButton);
    }
    private void OnDestroy()
    {
        btn.onClick.RemoveListener(OnClickButton);
    }

    private void OnClickButton()
    {
        UIHomeNavigation.Instance.BackPreviousPage();
        OnClickBtnEvent?.Invoke();
    }
}
