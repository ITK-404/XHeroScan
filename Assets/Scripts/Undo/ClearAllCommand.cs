using System.Collections.Generic;

public class ClearAllCommand : IUndoRedoCommand
{
    private List<Room> rooms = new();
    private List<DrawingInstanced> itemData = new();
    public ClearAllCommand()
    {
        rooms = new List<Room>(RoomStorage.rooms);
        itemData = FurnitureManager.GetAllFurnitureData();
        // store all data here
    }

    public void Redo()
    {
        ClearAllRoomsButton.ClearAll();
    }

    public void Undo()
    {
        foreach(var room in rooms)
        {
            CheckpointManager.Instance.RestoreRoom(room);
        }

        FurnitureManager.Instance.RestoreDrawingInstanced(itemData);
    }
}