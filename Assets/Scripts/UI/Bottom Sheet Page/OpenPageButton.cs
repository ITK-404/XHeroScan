using UnityEngine;
using UnityEngine.UI;
using static BottomSheetPageManager;
public class OpenPageButton : MonoBehaviour
{
    public PageType pageType;
    private BottomSheetPageManager manager;
    private Button btn;
    private void Awake()
    {
        manager = GetComponentInParent<BottomSheetPageManager>();
        btn = GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.AddListener(OnOpenPage);
    }

    private void OnOpenPage()
    {
        manager.Open(pageType);
    }
}