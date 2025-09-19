using UnityEngine;

public class EditFloorCommand : IUndoRedoCommand
{
    public Floor floor;
    private DragFromButtonSpawnFloor dragHandler => DragFromButtonSpawnFloor.Instance;
    public EditFloorCommand(Floor floor)
    {
        this.floor = Floor.Clone(floor);
    }
    public void Redo()
    {
    }
    public void Undo()
    {
        Debug.Log("UNDO FLOOR");
        // xóa current
        var current = FloorStorage.floors.Count > 0 ? FloorStorage.floors[0] : null;
        if (current != null && floor != null && current.ID != floor.ID)
        {
            Debug.Log("Xóa floor hiện tại");
            dragHandler.ResetSingleFloor();
        }
        // tạo lại cái cũ
        if (floor == null)
        {
            Debug.Log("Xóa floor hiện tại");
            dragHandler.ResetSingleFloor();
        }
        else
        {
            Debug.Log("Tạo lại floor cũ");
            //dragHandler.ResetSingleFloor();
            FloorStorage.UpdateOrAddFloor(floor);
            dragHandler.LoadStateFromFloorId(floor.ID);
            CameraResizeByFloor.Instance.Resize(floor.checkpoints);
            //dragHandler.RedrawRectangleFromState();
        }

    }
}
