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
        btn = GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.AddListener(OnOpenPage);
    }

    private void Start()
    {
        manager = Instance;
    }

    private void OnOpenPage()
    {
        if (pageType == PageType.None) return;
        manager.Open(pageType);
    }
}