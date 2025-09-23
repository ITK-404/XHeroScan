using System;
using System.Collections.Generic;
using UnityEngine;

public class UIHomeNavigation : MonoBehaviour
{
    public static UIHomeNavigation Instance;
    [Dropdown(typeof(HomePageName))] public string startPage;
    public UIHomePage[] pages;
    public GameObject top;
    public GameObject appNameTitle;
    public GameObject topPanel;

    static Stack<string> pageNameStack = new();
    private static string currentPage;
    private void Awake()
    {
        Instance = this;
        pages = GetComponentsInChildren<UIHomePage>();
    }

    private void Start()
    {
        if (string.IsNullOrWhiteSpace(currentPage))
        {
            ChangePage(startPage);
        }
        else
        {
            ChangePage(currentPage);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            BackPreviousPage();
        }
        topPanel.gameObject.SetActive(pageNameStack.Count > 0);
    }

    public void ChangePage(string pageName, bool isStackUI = false)
    {
        Debug.Log($"Change to page: " + pageName);
        appNameTitle.gameObject.SetActive(pageName.Equals(startPage));


        foreach (var item in pages)
        {
            if (item.PageName.Equals(pageName))
            {
                item.Open();
                Debug.Log("Find page to open");
                if (isStackUI)
                {
                    Debug.Log("Push page to stack: " + pageName);
                    pageNameStack.Push(currentPage);
                }
                ShowHomeTopUI(item.isShowTopDecor);
                currentPage = pageName;
            }
            else
            {
                item.Close();
            }
        }
    }

    public void ShowHomeTopUI(bool state)
    {
        top.gameObject.SetActive(state);
    }

    public void BackPreviousPage()
    {
        if (pageNameStack.Count > 0)
        {
            Debug.Log("Back to previous page");
            var page = pageNameStack.Pop();
            ChangePage(page);
        }
    }
}
