using System.Collections.Generic;
using UnityEngine;
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

        Debug.Log("Old list");
        ShowPositionList(oldList);
        Debug.Log("new list");
        ShowPositionList(newFurnitureData);
    }

    private void ShowPositionList(List<DrawingInstanced> list)
    {
        foreach(var item in list)
        {
            Debug.Log($"ID {item.instanceID} {item.worldPosition}");
        }
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
        if(room == null)
        {
            Debug.Log("phòng bị null, không thế restore lại state trước");
        }
        Debug.Log("Before Total room count:  " + RoomStorage.rooms.Count);
        CheckpointManager.Instance.ClearRoomById(room.ID);
        CheckpointManager.Instance.RestoreRoom(room);
       
        FurnitureManager.Instance.UpdateFurnitureState(list);
        Debug.Log("After Total room count:  " + RoomStorage.rooms.Count);

        var _room = RoomStorage.GetRoomByID(room.ID);
        //CameraResizeByFloor.Instance.Resize(room.checkpoints);
    }

    
}
