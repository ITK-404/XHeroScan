
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
public class MovePointRoomCommand : IUndoRedoCommand
{
    private List<Room> oldRoomSnapShoot = new();
    private List<DrawingInstanced> furnitureInsideRooms = new();
    public MovePointRoomCommand(Room oldRoom)
    {
        oldRoomSnapShoot.Add(oldRoom);
    }
    public MovePointRoomCommand(List<Room> rooms, List<DrawingInstanced> furnitureInsideRooms)
    {
        foreach (var room in rooms)
        {
            oldRoomSnapShoot.Add(room);
        }

        this.furnitureInsideRooms = furnitureInsideRooms;
    }
    public void Redo()
    {
    }
    public void Undo()
    {
        Debug.Log("Before Total room count:  " + RoomStorage.rooms.Count);
        foreach (var room in oldRoomSnapShoot)
        {
            CheckpointManager.Instance.ClearRoomById(room.ID);
            CheckpointManager.Instance.RestoreRoom(room);
        }

        // restore lại vị trí các furniture bên trong phòng
        foreach (var instacedData in furnitureInsideRooms)
        {
            var furniture = FurnitureManager.Instance.GetFurnitureByInstanceID(instacedData.instanceID);
            if (furniture == null) continue;
            Debug.Log(furniture.name + " Restore furniture in room ");
            furniture.FetchData(instacedData);
        }
        FurnitureManager.Instance.ForceSnapAllToNearestRoom();
        Debug.Log("After Total room count:  " + RoomStorage.rooms.Count);
    }
}
public class EditRoomCommand : IUndoRedoCommand
{
    private Room oldRoomSnapShoot = new();
    private List<DrawingInstanced> furnitureInsideRooms = new();
    public EditRoomCommand(Room oldRoom)
    {
        this.oldRoomSnapShoot = oldRoom;
        // lấy danh sách furniture bên trong phòng cũ, để khi undo có thể restore lại đúng vị trí
        furnitureInsideRooms = FurnitureManager.Instance.GetFurnitureInsideRoom(oldRoom.ID);
    }

    public void Redo()
    {
    }

    public void Undo()
    {
        Debug.Log("Before Total room count:  " + RoomStorage.rooms.Count);
        CheckpointManager.Instance.ClearRoomById(oldRoomSnapShoot.ID);
        CheckpointManager.Instance.RestoreRoom(oldRoomSnapShoot);

        RestoreFurnitureInRoom();
        Debug.Log("After Total room count:  " + RoomStorage.rooms.Count);
    }

    private void RestoreFurnitureInRoom()
    {

        // restore lại vị trí các furniture bên trong phòng
        foreach (var instacedData in furnitureInsideRooms)
        {
            var furniture = FurnitureManager.Instance.GetFurnitureByInstanceID(instacedData.instanceID);
            if (furniture == null) continue;
            Debug.Log(furniture.name + " Restore furniture in room ");
            furniture.FetchData(instacedData);
        }
        FurnitureManager.Instance.ForceSnapAllToNearestRoom();
    }
}