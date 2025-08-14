using UnityEngine;

[CreateAssetMenu(fileName = "Popup Asset", menuName = "ScriptableObjects/PopupAsset", order = 1)]
public class PopupAsset : ScriptableObject
{
    public GameObject modularPopupYesNo;
    public GameObject modularPopupWarningDelete;
    public GameObject toastPopupComplete;
    public GameObject toastPopupError;
    public GameObject toastPopupDowload;
}