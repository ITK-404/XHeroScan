using UnityEngine;

public class InteractionFlagsDebug : MonoBehaviour
{
    public bool IsFloorHandleDragging = false;
    public bool IsRoomFloorDragging = false;
    public bool OnDragFurniture = false;
    public bool OnDragPoint = false;
    public bool IsOpenBottomSheetUI = false;

    void Update()
    {
        // Giả sử InteractionFlags là singleton hoặc static
        IsFloorHandleDragging = InteractionFlags.IsFloorHandleDragging;
        IsRoomFloorDragging = InteractionFlags.IsRoomFloorDragging;
        OnDragFurniture = InteractionFlags.OnDragFurniture;
        OnDragPoint = InteractionFlags.OnDragMovePoint;
        IsOpenBottomSheetUI = InteractionFlags.IsOpenBottomSheetUI;
    }
}
