using UnityEngine;

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

    }
}
