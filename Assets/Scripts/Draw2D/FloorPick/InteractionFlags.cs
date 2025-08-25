// đặt file riêng: InteractionFlags.cs
public static class InteractionFlags
{
    // true khi đang kéo point/handle của floor
    public static bool IsFloorHandleDragging = false;

    // (tuỳ chọn) true khi đang kéo cả phòng/sàn (move room)
    public static bool IsRoomFloorDragging = false;
}
