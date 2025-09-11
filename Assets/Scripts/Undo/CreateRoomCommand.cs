public class CreateRoomCommand : IUndoRedoCommand
{
    private string roomID;
    public CreateRoomCommand(string id)
    {
        roomID = id;
    }
    public void Redo()
    {
    }
    public void Undo()
    {
        CheckpointManager.Instance.ClearRoomById(roomID);
    }
}
public class DeleteRoomCommand : IUndoRedoCommand
{
    public Room room;

    public DeleteRoomCommand(Room room)
    {
        this.room = room;
    }

    public void Redo()
    {
    }
    public void Undo()
    {
        CheckpointManager.Instance.RestoreRoom(room);
    }
}

public class EditRoomCommand : IUndoRedoCommand
{
    private Room oldRoomSnapShoot;
    public EditRoomCommand(Room oldRoom)
    {
        this.oldRoomSnapShoot = oldRoom;
    }
    public void Redo()
    {
    }
    public void Undo()
    {
        CheckpointManager.Instance.ClearRoomById(oldRoomSnapShoot.ID);
        CheckpointManager.Instance.RestoreRoom(oldRoomSnapShoot);
        FurnitureManager.Instance.ResetAttachedItems();
        FurnitureManager.Instance.TrySnapToNearestRoom();
    }
}