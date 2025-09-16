using System.Collections.Generic;
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

        var runtimeFurnitures = FurnitureManager.Instance.GetAllFurniture();
        foreach (var furniture in runtimeFurnitures)
        {
            if (furniture == null || string.IsNullOrEmpty(furniture.data.roomID)) continue;
            furnitureInsideRoom.Add(furniture.data);
        }
    }

    public MovePointRoomCommand CreateUndoCommand()
    {
        if (roomIDChanged.Count == 0) return null;

        foreach (var item in roomIDChanged)
        {
            Debug.Log("Room ID changed: " + item);
        }
        List<Room> currentRoomData = new();
        List<DrawingInstanced> currentChangedList = new();
        // just save room that changed
        foreach (var item in snapShotRoomData)
        {
            if (roomIDChanged.Contains(item.ID))
            {
                currentRoomData.Add(item);
            }
        }
        // just save furniture inside room changed
        foreach (var item in furnitureInsideRoom)
        {
            if (roomIDChanged.Contains(item.roomID))
            {
                currentChangedList.Add(item);
            }
        }
        return new MovePointRoomCommand(currentRoomData, currentChangedList);
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
        UndoRedoController.scanARTempList.Add(command);
    }

    public void TryAddChangedRoomID(string roomID)
    {
        // add room changed ID to save
        if (!string.IsNullOrEmpty(roomID) && !roomIDChanged.Contains(roomID))
            roomIDChanged.Add(roomID);
    }
}
