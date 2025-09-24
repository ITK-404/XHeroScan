using UnityEngine;
public class CreateFloorCommand : IUndoRedoCommand
{
    private Floor floor;
    private DragFromButtonSpawnFloor dragHandler => DragFromButtonSpawnFloor.Instance;

    public CreateFloorCommand(Floor floor)
    {
        this.floor = floor;
    }
    public void Redo()
    {
        Debug.Log($"DragFromButtonSpawnFloor {DragFromButtonSpawnFloor.Instance != null}");
        FloorStorage.UpdateOrAddFloor(Floor.Clone(floor));
        dragHandler.LoadStateFromFloorId(floor.ID);
        CameraResizeByFloor.Instance.Resize(floor.checkpoints);
    }

    public void Undo()
    {
        dragHandler.ResetSingleFloor();
    }
}
public class EditFloorCommand : IUndoRedoCommand
{
    public Floor oldFloor;
    public Floor newFloor;
    private DragFromButtonSpawnFloor dragHandler => DragFromButtonSpawnFloor.Instance;
    public EditFloorCommand(Floor oldFloor, Floor newFloor)
    {
        this.oldFloor = Floor.Clone(oldFloor);
        this.newFloor = Floor.Clone(newFloor);
    }
    public void Redo()
    {
        ReloadFloor(newFloor);
    }
    public void Undo()
    {
        ReloadFloor(oldFloor);
    }

    private void ReloadFloor(Floor oldFloor)
    {
        Debug.Log("UNDO FLOOR");
        // xóa current
        var current = FloorStorage.floors.Count > 0 ? FloorStorage.floors[0] : null;
        if (current != null && oldFloor != null && current.ID != oldFloor.ID)
        {
            Debug.Log("Xóa floor hiện tại");
            dragHandler.ResetSingleFloor();
        }
        // tạo lại cái cũ
        if (oldFloor == null)
        {
            Debug.Log("Xóa floor hiện tại");
            dragHandler.ResetSingleFloor();
        }
        else
        {
            Debug.Log("Tạo lại floor cũ");
            //dragHandler.ResetSingleFloor();
            FloorStorage.UpdateOrAddFloor(Floor.Clone(oldFloor));
            dragHandler.LoadStateFromFloorId(oldFloor.ID);
            CameraResizeByFloor.Instance.Resize(oldFloor.checkpoints);
            //dragHandler.RedrawRectangleFromState();
        }
    }
}
