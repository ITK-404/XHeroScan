using System.Collections.Generic;
using UnityEngine;

public class DeleteRoomCommand : IUndoRedoCommand
{
    public Room room;
    private List<DrawingInstanced> itemInside = new();
    public DeleteRoomCommand(Room room)
    {
        itemInside.Clear();
        this.room = room;
        var furnitureInsideRoom = FurnitureManager.Instance.GetRuntimeItemInsideRoom(room.ID);
        foreach (var item in furnitureInsideRoom)
        {
            Debug.Log($"item save: " + item.data.instanceID);
            itemInside.Add(item.data);
        }
        Debug.Log($"item save: " + furnitureInsideRoom.Count);
    }

    public void Redo()
    {
        CheckpointManager.Instance.ClearRoomById(room.ID);
        FurnitureManager.Instance.ClearItemInRoom(room.ID);
    }

    public void Undo()
    {
        CheckpointManager.Instance.RestoreRoom(room);
        FurnitureManager.Instance.RestoreDrawingInstanced(itemInside);
    }
}
