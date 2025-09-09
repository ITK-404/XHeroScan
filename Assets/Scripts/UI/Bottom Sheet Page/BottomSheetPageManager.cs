using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class BottomSheetPageManager : MonoBehaviour
{
    private BottomSheetPage[] bottomSheetPages;
    [SerializeField] private Button closeAllBtn;
    public enum PageType
    {
        None,
        Menu,
        Furniture,
        Structure,
        Door,
        Window
    }
    private void Awake()
    {
        bottomSheetPages = GetComponentsInChildren<BottomSheetPage>();
        foreach (var closePageBtn in bottomSheetPages)
        {
            closePageBtn.hideButton.onClick.AddListener(OpenMenu);
            closePageBtn.closeAllBtn.onClick.AddListener(CloseAll);
        }
        closeAllBtn.onClick.AddListener(CloseAll);
    }

    private void CloseAll()
    {
        Debug.Log("Close All");
        foreach (var page in GetComponentsInChildren<BaseAnimUI>())
        {
            page.Close();
        }
        closeAllBtn.gameObject.SetActive(false);
    }

    public void Open(PageType pageType)
    {
        if (pageType == PageType.None)
        {
            CloseAll();
            return;
        }

        int highestOrder = transform.childCount;
        foreach (var page in bottomSheetPages)
        {
            if (page.pageType == pageType)
            {
                Debug.Log("Open Page: " + pageType);
                page.transform.SetSiblingIndex(highestOrder);
                page.Open();
                break;
            }
        }
    }

    public void OpenMenu()
    {
        Debug.Log("Open Menu");
        foreach (var page in bottomSheetPages)
        {
            if (page.pageType != PageType.Menu)
            {
                page.Close();
            }
        }
    }
}
