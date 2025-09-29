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

    }

    public void Undo()
    {
        Debug.Log($"Find {InstanceID} to delete");
        var furniture = FurnitureManager.Instance.GetFurnitureByInstanceID(InstanceID);
        furniture.Destroy();
    }
}
