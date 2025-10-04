using System.Collections.Generic;

public class EditoRoomCommandCreator
{
    private Room oldRoom = new();
    private List<DrawingInstanced> oldList = new();
    private string roomID;


    private bool canCreateCommand = false;
    public void Init(string roomID)
    {
        canCreateCommand = true;
        this.roomID = roomID;
        var room = RoomStorage.GetRoomByID(roomID);

        if(room == null)
        {
            canCreateCommand = false;
            return;
        }

        oldRoom = new Room(room);
        oldList = FurnitureManager.Instance.GetFurnitureInsideRoom(room.ID);
    }

    public void CreateCommand()
    {
        if (!canCreateCommand)
        {
            return;
        }
        var newRoom = new Room(RoomStorage.GetRoomByID(roomID));
        var oldRoom = new Room(this.oldRoom);
        var list = new List<DrawingInstanced>(oldList);
        UndoRedoController.Instance.AddToUndo(new EditRoomCommand(oldRoom, list, newRoom));
    }
}
