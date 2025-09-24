
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

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

public class DeleteRoomCommand : IUndoRedoCommand
{
    public Room room;

    public DeleteRoomCommand(Room room)
    {
        this.room = room;
    }

    public void Redo()
    {
        CheckpointManager.Instance.ClearRoomById(room.ID);
    }

    public void Undo()
    {
        CheckpointManager.Instance.RestoreRoom(room);
    }
}
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
        foreach (var instacedData in drawingInstanceds)
        {
            var furniture = FurnitureManager.Instance.GetFurnitureByInstanceID(instacedData.instanceID);
            if (furniture == null) continue;
            furniture.FetchData(instacedData);
            Debug.Log($"World Position: {furniture.data.worldPosition}");
        }
        FurnitureManager.Instance.ForceSnapAllToNearestRoom();
        Debug.Log("After Total room count:  " + RoomStorage.rooms.Count);
    }
}

public class EditRoomCommand : IUndoRedoCommand
{
    private Room newRoom = new();
    private Room oldRoom = new();
    private List<DrawingInstanced> oldFurnitureData = new();
    private List<DrawingInstanced> newFurnitureData = new();
    public EditRoomCommand(Room oldRoom, List<DrawingInstanced> oldList, Room newRoom)
    {
        // lấy danh sách furniture bên trong phòng cũ, để khi undo có thể restore lại đúng vị trí
        this.oldRoom = oldRoom;
        oldFurnitureData = oldList;
       
        this.newRoom = newRoom;
        newFurnitureData = FurnitureManager.Instance.GetFurnitureInsideRoom(newRoom.ID);
    }


    public void Redo()
    {
        Restore(newRoom, newFurnitureData);
    }

    public void Undo()
    {
        Restore(oldRoom, oldFurnitureData);
    }

    private void Restore(Room room, List<DrawingInstanced> list)
    {
        // xóa phòng hiện tại và spawn phòng mới dựa trên ID
        
        Debug.Log("Before Total room count:  " + RoomStorage.rooms.Count);
        CheckpointManager.Instance.ClearRoomById(room.ID);
        CheckpointManager.Instance.RestoreRoom(room);
        
        RestoreFurnitureInRoom(oldFurnitureData);
        Debug.Log("After Total room count:  " + RoomStorage.rooms.Count);

        var _room = RoomStorage.GetRoomByID(oldRoom.ID);
        CameraResizeByFloor.Instance.Resize(room.checkpoints);
    }

    private void RestoreFurnitureInRoom(List<DrawingInstanced> drawList)
    {

        // restore lại vị trí các furniture bên trong phòng
        foreach (var instacedData in drawList)
        {
            var furniture = FurnitureManager.Instance.GetFurnitureByInstanceID(instacedData.instanceID);
            if (furniture == null) continue;
            Debug.Log(furniture.name + " Restore furniture in room ");
            furniture.FetchData(instacedData);
        }
        //FurnitureManager.Instance.ForceSnapAllToNearestRoom();
    }
}