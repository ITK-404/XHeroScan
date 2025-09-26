// đặt file riêng: InteractionFlags.cs
public static class InteractionFlags
{
    // true khi đang kéo point/handle của floor
    public static bool IsFloorHandleDragging = false;

    // (tuỳ chọn) true khi đang kéo cả phòng/sàn (move room)
    public static bool IsRoomFloorDragging = false;

    // khi đang kéo vật thể
    public static bool OnDragFurniture = false;
    // khi đang kéo point để điều chỉnh kích thước
    public static bool OnDragMovePoint = false;
    public static bool OnDragRotatePoint = false;
    public static bool IsOpenBottomSheetUI = false;
    public static bool IsEdit = false;

}
