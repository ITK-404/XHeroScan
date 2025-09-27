// đặt file riêng: PopupInteractionControl.cs
using UnityEngine;

public class PopupInteractionControl : MonoBehaviour
{

    public bool IsFloorHandleDragging = false;

    public bool IsRoomFloorDragging = false;
    public bool IsOpenBottomSheetUI = false;

    

    private void OnEnable()
    {
        InteractionFlags.IsFloorHandleDragging = IsFloorHandleDragging;
        InteractionFlags.IsRoomFloorDragging = IsRoomFloorDragging;
        InteractionFlags.IsOpenBottomSheetUI = IsOpenBottomSheetUI;
    }

    private void OnDisable()
    {
        InteractionFlags.IsFloorHandleDragging = !IsFloorHandleDragging;
        InteractionFlags.IsRoomFloorDragging = !IsRoomFloorDragging;
        InteractionFlags.IsOpenBottomSheetUI = !IsOpenBottomSheetUI;
    }

}