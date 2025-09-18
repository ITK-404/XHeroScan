using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BlockPopupBackground : MonoBehaviour
{
    [SerializeField] private Button btn;
    [SerializeField] private GameObject container;
    [SerializeField] private BaseAnimUI baseAnimUI;

    public Image image;
    public UnityAction OnClickBackgroundEvent;

    private void Awake()
    {
        image = container.GetComponent<Image>();

        btn.onClick.AddListener(OnCLickHide);
        baseAnimUI.OnEndHideAnim.AddListener(Hide);
        baseAnimUI.OnStartShowAnim.AddListener(Show);
    }

    private void OnDestroy()
    {
        btn.onClick.RemoveListener(OnCLickHide);
        baseAnimUI.OnEndHideAnim.RemoveListener(Hide);
        baseAnimUI.OnStartShowAnim.RemoveListener(Show);
    }

    private void OnValidate()
    {
        if(transform.childCount > 0)
        {
            container = transform.GetChild(0).gameObject;
        }
    }

    private void OnCLickHide()
    {
        Debug.Log("On Click Handle");
        OnClickBackgroundEvent?.Invoke();
    }

    private void Show()
    {
        Debug.Log("Show block popup background");
        container.SetActive(true);
    }

    private void Hide()
    {
        Debug.Log("Hide block popup background");
        container.SetActive(false);
    }
}
