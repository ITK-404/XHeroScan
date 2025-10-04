
public class CreateRoomCommand : IUndoRedoCommand
{
    private string roomID;
    private Room room;
    public CreateRoomCommand(Room room)
    {
        roomID = room.ID;
        this.room = new Room(room);
    }

    public void Redo()
    {
        CheckpointManager.Instance.RestoreRoom(room);
    }

    public void Undo()
    {
        CheckpointManager.Instance.ClearRoomById(roomID);
    }
}
