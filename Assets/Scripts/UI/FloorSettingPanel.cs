using UnityEngine;
public class FloorSettingPanel : SettingPanel
{
    //[SerializeField] private HeightInputController heightInputController;
    public ToggleFlipGroup toggleFlipGroup;
    [SerializeField] private RoomInfoDisplay roomInfoDisplay;

    //public void OnChecking()
    //{
    //    var selectionKind = roomInfoDisplay.SelectionItem;
    //    Debug.Log("Selection Kind is: " + selectionKind);
    //    switch (selectionKind)
    //    {
    //        case RoomInfoDisplay.SelectionKind.None:
    //            break;
    //        case RoomInfoDisplay.SelectionKind.Room:
    //        case RoomInfoDisplay.SelectionKind.Floor:
    //            heightInputController.gameObject.SetActive(true);
    //            toggleFlipGroup.gameObject.SetActive(false);
    //            break;
    //        case RoomInfoDisplay.SelectionKind.Furniture:
    //            heightInputController.gameObject.SetActive(false);
    //            toggleFlipGroup.gameObject.SetActive(true);

    //            var widthInput = GetParameterInputField(IntParameterType.Width);
    //            var heightInput = GetParameterInputField(IntParameterType.Height);
    //            var distaneFromGroundInput = GetParameterInputField(IntParameterType.DistanceFromGround);
    //            var type = FurnitureManager.Instance.CurrentFurnitureItem().lineType;

    //            widthInput.gameObject.SetActive(true);
    //            heightInput.gameObject.SetActive(type == LineType.None);
    //            distaneFromGroundInput.gameObject.SetActive(type == LineType.Window);
    //            break;
    //        default:
    //            break;
    //    }
    //}
}
