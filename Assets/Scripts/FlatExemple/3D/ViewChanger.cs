using System;
using UnityEngine;

public class ViewChanger : MonoBehaviour
{
    public GameObject canvas2D;
    public GameObject canvas3D;
    
    public static event Action<ViewType> ViewChanged;
    public static ViewType currentViewType;
    public void ChangeView(ViewType viewType)
    {
        if (viewType == ViewType.VIew2D)
        {
            canvas2D.SetActive(true);
            canvas3D.SetActive(false);
        }
        else if (viewType == ViewType.View3D)
        {
            canvas2D.SetActive(false);
            canvas3D.SetActive(true);
        }
        currentViewType = viewType;
        ViewChanged?.Invoke(viewType);
    }
}