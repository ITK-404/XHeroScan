using NUnit.Framework.Interfaces;
using System.Linq;
using UnityEngine;

public class CreateItemCommand : IUndoRedoCommand
{
    public string InstanceID;
    public DrawingInstanced itemData;
    public CreateItemCommand(DrawingInstanced itemData)
    {
        this.itemData = itemData;
        this.InstanceID = itemData.instanceID;
        Debug.Log("Tạo lệnh undo furniture: " + InstanceID);
    }

    public void Redo()
    {
        var item = FurnitureManager.Instance.SpawnFurniture(itemData.itemTemplateID, itemData.worldPosition);

        if (item == null)
        {
            Debug.Log("Item id null");
            return;
        }

        item.FetchData(itemData);
        item.furnitureMergeToWall.ForceSnapToWall();

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
        var InstanceID = itemData.instanceID;
        Debug.Log($"Find {InstanceID} to delete");
        var furniture = FurnitureManager.Instance.GetFurnitureByInstanceID(InstanceID);
        furniture.Destroy();
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
        item.furnitureMergeToWall.ForceSnapToWall();
    }
}

public class EditItemCommand : IUndoRedoCommand
{
    public DrawingInstanced oldData;
    public DrawingInstanced newData;
    public EditItemCommand(DrawingInstanced oldData, DrawingInstanced newData)
    {
        Debug.Log("Tạo lệnh edit cho item " + oldData.instanceID);
        this.oldData = oldData;
        this.newData = newData;
    }

    public void Redo()
    {
        FetchData(newData);
    }

    public void Undo()
    {
        FetchData(oldData);
    }

    private void FetchData(DrawingInstanced data)
    {
        var item = FurnitureManager.Instance.GetFurnitureByInstanceID(data.instanceID);
        item.furnitureMergeToWall.ResetAttached();
        item.FetchData(data);
        item.furnitureMergeToWall.TryToMergeAndSnapInAllWall();
    }
}
