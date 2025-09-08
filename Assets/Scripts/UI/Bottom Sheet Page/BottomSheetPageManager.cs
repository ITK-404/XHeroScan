using System.Collections.Generic;
using UnityEngine;

public partial class BottomSheetPageManager : MonoBehaviour
{
    private BottomSheetPage[] bottomSheetPages;
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
    }

    private void CloseAll()
    {
        Debug.Log("Close All");
        foreach(var page in GetComponentsInChildren<BaseAnimUI>())
        {
            page.Close();
        }
    }

    public void Open(PageType pageType)
    {
        if(pageType == PageType.None)
        {
            CloseAll();
            return;
        }

        int highestOrder = transform.childCount;
        foreach(var page in bottomSheetPages)
        {
            if(page.pageType == pageType)
            {
                page.transform.SetSiblingIndex(highestOrder);
                page.Open();
                break;
            }
        }
    }

    public void OpenMenu()
    {
        foreach(var page in bottomSheetPages)
        {
            if(page.transform != transform)
            {
                page.Close();
            }
        }
    }
}
