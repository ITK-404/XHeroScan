using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class EditRoomCommandCreator
{
    private List<string> roomIDChanged = new();
    private List<Room> snapShotRoomData = new();
    private List<DrawingInstanced> furnitureInsideRoom = new();

    public EditRoomCommandCreator()
    {
        // store all data
        foreach (var room in RoomStorage.rooms)
        {
            snapShotRoomData.Add(new Room(room));
        }

        if (FurnitureManager.Instance == null) return;

        furnitureInsideRoom = GetFurnitureData();
    }

    private List<DrawingInstanced> GetFurnitureData()
    {
        var runtimeFurnitures = new List<FurnitureItem>();
        var furnitureInsideRoom = new List<DrawingInstanced>();
        runtimeFurnitures = FurnitureManager.Instance.GetAllFurniture();
        foreach (var furniture in runtimeFurnitures)
        {
            if (furniture == null || string.IsNullOrEmpty(furniture.data.roomID)) continue;
            furnitureInsideRoom.Add(furniture.data);
        }

        return furnitureInsideRoom;
    }

    public MovePointRoomCommand CreateUndoCommand()
    {
        if (roomIDChanged == null) return null;
        if (roomIDChanged.Count == 0) return null;

        foreach (var item in roomIDChanged)
        {
            Debug.Log("Room ID changed: " + item);
        }
        List<Room> oldRoomData = new();
        List<Room> newRoomData = new();
        List<DrawingInstanced> oldFurnitureData = new();
        List<DrawingInstanced> newFurnitureData = new();
        // just save room that changed

        foreach (var item in snapShotRoomData)
        {
            if (roomIDChanged.Contains(item.ID))
            {
                oldRoomData.Add(item);
            }
        }

        foreach (var item in RoomStorage.rooms)
        {
            if (roomIDChanged.Contains(item.ID))
            {
                newRoomData.Add(new(item));
            }
        }

        // just save furniture inside room changed
        if (furnitureInsideRoom == null) return null;
        foreach (var item in furnitureInsideRoom)
        {
            if (roomIDChanged.Contains(item.roomID))
            {
                Debug.Log($"Old {item.instanceID} {item.worldPosition}");
                oldFurnitureData.Add(item);
            }
        }


        foreach (var item in GetFurnitureData())
        {
            if (roomIDChanged.Contains(item.roomID))
            {
                Debug.Log($"New {item.instanceID} {item.worldPosition}");
                newFurnitureData.Add(item);
            }
        }
        return new MovePointRoomCommand(oldRoomData, oldFurnitureData, newRoomData, newFurnitureData);
    }

    public void CreateAndAddUndoList()
    {
        var command = CreateUndoCommand();
        if (command == null) return;
        UndoRedoController.Instance.AddToUndo(command);
    }

    public void CreateAndAddScanARList()
    {
        Debug.Log($"Add to scan AR List");
        // tạo và thêm command vào list scanAR
        // lệnh này dùng trong scene scan AR, đảm bảo sau khi quay lại scene draw có thể hoàn tác được room
        var command = CreateUndoCommand();
        if (command == null) return;
        if (UndoRedoController.scanARTempList == null) return;
        UndoRedoController.scanARTempList.Add(command);
    }

    public void TryAddChangedRoomID(string roomID)
    {
        // add room changed ID to save
        if (!string.IsNullOrEmpty(roomID) && !roomIDChanged.Contains(roomID))
            roomIDChanged.Add(roomID);
    }
}
