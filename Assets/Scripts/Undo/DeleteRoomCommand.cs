using System.Collections.Generic;
using UnityEngine;

public class DeleteRoomCommand : IUndoRedoCommand
{
    public Room room;
    private List<DrawingInstanced> doorAndWindows = new();
    public DeleteRoomCommand(Room room)
    {
        this.room = room;
        var furnitureInsideRoom = FurnitureManager.Instance.GetRuntimeItemInsideRoom(room.ID);
        foreach(var item in furnitureInsideRoom)
        {
            doorAndWindows.Add(item.data);
        }
    }

    public void Redo()
    {
        CheckpointManager.Instance.ClearRoomById(room.ID);
        FurnitureManager.Instance.ClearItemInRoom(room.ID);
    }

    public void Undo()
    {
        CheckpointManager.Instance.RestoreRoom(room);
        FurnitureManager.Instance.RestoreDrawingInstanced(doorAndWindows);
    }
}
