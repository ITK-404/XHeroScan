using System.Collections.Generic;
using UnityEngine;

public class UIHomeNavigation : MonoBehaviour
{
    public UIHomePage[] pages;
    public static UIHomeNavigation Instance;
    Stack<string> pageNameStack;
    private void Awake()
    {
        Instance = this;
        pages = GetComponentsInChildren<UIHomePage>();
    }

    public void ChangePage(string pageName)
    {
        foreach (var item in pages)
        {
            if (item.PageName == pageName)
            {
                item.Open();
            }
            else
            {
                item.Close();
            }
        }
    }
}
