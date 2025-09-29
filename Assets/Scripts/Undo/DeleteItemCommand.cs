using UnityEngine;

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
    }
}
