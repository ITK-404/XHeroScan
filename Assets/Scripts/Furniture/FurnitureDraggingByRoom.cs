using System.Collections.Generic;
using UnityEngine;

public class FurnitureDraggingByRoom
{
    public string roomID;
    private FurnitureManager furnitureManager;
    private List<FurnitureItem> validFurnitureToDrag = new();


    public void Clear()
    {
        roomID = "";
        validFurnitureToDrag.Clear();
    }
    public void SetRoomID(string roomID)
    {
         furnitureManager = FurnitureManager.Instance;
        Clear();
        Debug.Log("Set room id: " + roomID);
        this.roomID = roomID;
    }

    public void StartDrag()
    {
        Debug.Log("Start Drag furniture");
        if (string.IsNullOrEmpty(roomID))
        {
            Debug.Log("RoomID đang null hoặc rỗng");
            return;
        }
        // set visible if room is off
        furnitureManager.SetVisibleObjects(roomID, true);

        foreach(var item in furnitureManager.GetAllFurniture())
        {
            // filer right here
            Debug.Log($"{item.data.roomID == roomID} {item.furnitureMergeToWall.IsInWall()}");
            
            if (item.lineType == LineType.None)
            {
                if (item.data.roomID == roomID)
                {
                    Debug.Log($"Find item to dragging with room: ", item.gameObject);
                    validFurnitureToDrag.Add(item);
                }
            }
            
        }

        foreach(var item in validFurnitureToDrag)
        {
            item.furnitureDrag.SetCanMove(canMove:true);
        }
    }

    public void EndDrag()
    {
        Debug.Log("End drag furniture");
        foreach(var item in validFurnitureToDrag)
        {
            item.furnitureDrag.SetCanMove(canMove: false);
            item.furnitureMergeToWall.EndSnap();
        }
    }

    public void Dragging(Vector3 delta)
    {
        Debug.Log($"Dragging list furniture {validFurnitureToDrag.Count}");
        delta.y = 0;
        foreach (var item in validFurnitureToDrag)
        {
            var draggingPosition = item.GetWorldPosition() + delta;
            //Debug.Log("Dragging position: " + draggingPosition);
            item.furnitureDrag.DraggingByPosition(draggingPosition);
            //item.modelContainer.transform.position += delta;
        }
    }
}