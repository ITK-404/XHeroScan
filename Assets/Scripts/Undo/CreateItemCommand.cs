using System.Linq;
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
