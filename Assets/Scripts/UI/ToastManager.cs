using System.Collections;
using UnityEngine;

public class ToastManager : MonoBehaviour
{
    

    public static ModularPopup Spawn(string description, GameObject popupPrefab)
    {
        var popup = Instantiate(popupPrefab).GetComponent<ModularPopup>();
        popup.AutoFindCanvasAndSetup();
        popup.Description = description;
        popup.AutoDestruct();

        return popup;
    }
}