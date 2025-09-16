using System.Collections.Generic;
using UnityEngine;

public class EditRoomCommandCreator
{
    private List<string> roomIDChanged = new();
    private List<Room> snapShotRoomData = new();
    private List<DrawingInstanced> furnitureInsideRoom = new();

    public EditRoomCommandCreator()
    {
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

    public void CreateUndoCommand()
    {
        if (roomIDChanged.Count == 0) return;

        foreach (var item in roomIDChanged)
        {
            Debug.Log("Room ID changed: " + item);
        }
        List<Room> currentRoomData = new();
        List<DrawingInstanced> currentChangedList = new();

        foreach (var item in snapShotRoomData)
        {
            if (roomIDChanged.Contains(item.ID))
            {
                currentRoomData.Add(item);
            }
        }
        foreach (var item in furnitureInsideRoom)
        {
            if (roomIDChanged.Contains(item.roomID))
            {
                currentChangedList.Add(item);
            }
        }
        UndoRedoController.Instance.AddToUndo(new MovePointRoomCommand(currentRoomData, currentChangedList));

    }

    public void TryAddChangedRoomID(string roomID)
    {
        if (!string.IsNullOrEmpty(roomID) && !roomIDChanged.Contains(roomID))
            roomIDChanged.Add(roomID);
    }
}
