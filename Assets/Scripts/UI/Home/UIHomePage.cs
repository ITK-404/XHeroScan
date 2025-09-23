using UnityEngine;
using UnityEngine.UI;

public class UIHomePage : MonoBehaviour
{
    [Dropdown(typeof(HomePageName))]public string PageName;
    [SerializeField] private GameObject container;
    private CanvasGroup canvasGroup;

    private void OnValidate()
    {
        if(container == null && transform.childCount > 0)
        {
            container = transform.GetChild(0).gameObject;
        }
    }

    public void Close()
    {
        container.gameObject.SetActive(false);
    }

    public void Open()
    {
        container.gameObject.SetActive(true);
    }
}
