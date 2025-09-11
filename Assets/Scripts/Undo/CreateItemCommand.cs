using UnityEngine;

public class CreateItemCommand : IUndoRedoCommand
{
    public string InstanceID;

    public CreateItemCommand(string instanceID)
    {
        Debug.Log("Tạo lệnh undo furniture: " + instanceID);
        InstanceID = instanceID;
    }

    public void Redo()
    {

    }

    public void Undo()
    {
        Debug.Log($"Find {InstanceID} to delete");
        var furniture = FurnitureManager.Instance.GetFurnitureByInstanceID(InstanceID);
        furniture.Destroy();
    }
}

public class DeleteItemCommand : IUndoRedoCommand
{
    public DrawingInstanced itemData;

    public DeleteItemCommand(DrawingInstanced itemData)
    {
        this.itemData = itemData;
    }

    public void Redo()
    {

    }

    public void Undo()
    {
        var item = FurnitureManager.Instance.SpawnFurniture(itemData.itemTemplateID, itemData.worldPosition);

        if (item == null)
        {
            Debug.Log("Item id null");
            return;
        }

        item.FetchData(itemData);
    }
}

public class EditItemCommand : IUndoRedoCommand
{
    public DrawingInstanced itemData;

    public EditItemCommand(DrawingInstanced itemData)
    {
        Debug.Log("Tạo lệnh edit cho item " + itemData.instanceID);
        this.itemData = itemData;
    }

    public void Redo()
    {
    }

    public void Undo()
    {
        var item = FurnitureManager.Instance.GetFurnitureByInstanceID(itemData.instanceID);
        item.furnitureMergeToWall.ResetAttached();
        item.FetchData(itemData);
        item.furnitureMergeToWall.TryToMergeAndSnapInAllWall();
    }
}

public class EditFloorCommand : IUndoRedoCommand
{
    public string floorId;
    public float width;
    public float length;
    public DimensionOkHandler okHandler;
    public EditFloorCommand(string floorID, float width, float length, DimensionOkHandler okHandler)
    {
        this.floorId = floorID;
        this.width = width;
        this.length = length;
        this.okHandler = okHandler;
    }
    public void Redo()
    {
    }
    public void Undo()
    {
        Debug.Log("Undo chỉnh sửa sàn " + floorId + " về kích thước " + width + "x" + length);
        var target = okHandler.FindFloor(floorId);

        if (target == null)
        {
            Debug.LogWarning("Không tìm thấy sàn để undo");
            return;
        }
        // Hàm đang sai chiều dài và rộng bị đảo ngược
        okHandler.TryUpdateFloor(target, length, width);
    }
}