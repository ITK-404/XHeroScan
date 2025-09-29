using System.Collections.Generic;
using UnityEngine;

public class MovePointRoomCommand : IUndoRedoCommand
{
    private List<Room> undoRooms = new();
    private List<Room> redoRooms = new();
    private List<DrawingInstanced> undoFurnitures = new();
    private List<DrawingInstanced> redoFurnitures = new();

    public MovePointRoomCommand(Room oldRoom)
    {
        undoRooms.Add(oldRoom);
    }

    public MovePointRoomCommand(List<Room> undoRooms, List<DrawingInstanced> undoFurnitures, List<Room> redoRooms, List<DrawingInstanced> redoFurnitures)
    {
        this.undoRooms.AddRange(undoRooms);
        this.undoFurnitures = undoFurnitures;

        this.redoRooms.AddRange(redoRooms);
        this.redoFurnitures = redoFurnitures;
    }

    public void Redo()
    {
        DeleteAndCreateNewRoom(redoRooms, redoFurnitures);
    }

    public void Undo()
    {
        DeleteAndCreateNewRoom(undoRooms, undoFurnitures);
    }

    private void DeleteAndCreateNewRoom(List<Room> rooms,List<DrawingInstanced> drawingInstanceds)
    {
        Debug.Log("Before Total room count:  " + RoomStorage.rooms.Count);
        // xóa những phòng chịu ảnh hưởng
        foreach (var room in rooms)
        {
            CheckpointManager.Instance.ClearRoomById(room.ID);
            CheckpointManager.Instance.RestoreRoom(room);
        }

        // restore lại vị trí các furniture bên trong phòng
        FurnitureManager.Instance.UpdateFurnitureState(drawingInstanceds);
        Debug.Log("After Total room count:  " + RoomStorage.rooms.Count);
    }
}
