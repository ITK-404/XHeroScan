using UnityEngine;
using UnityEngine.UI;
using static BottomSheetPageManager;
public class OpenPageButton : MonoBehaviour
{
    public PageType pageType;
    private BottomSheetPageManager manager;
    private Button btn;
    [SerializeField] private bool isHidePage = false;
    [SerializeField] private bool autoOpenPageWhenClick = true;
    private void Awake()
    {
        btn = GetComponent<Button>();
        if (btn == null) return;
        if(autoOpenPageWhenClick)
            btn.onClick.AddListener(OnOpenPage);
        
    }

    private void OnDestroy()
    {
        if (autoOpenPageWhenClick)
            btn.onClick.RemoveListener(OnOpenPage);
    }

    private void Start()
    {
        manager = Instance;
    }

    public void OnOpenPage()
    {
        if (pageType == PageType.None && isHidePage == false) return;
        manager.Open(pageType);
    }
}