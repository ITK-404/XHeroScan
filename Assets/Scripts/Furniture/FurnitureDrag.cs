
using UnityEngine;

public class FurnitureDrag : MonoBehaviour
{
    [SerializeField] private FurnitureItem furnitureItem;

    private void OnMouseDown()
    {
        furnitureItem.StartDrag();
    }

    private void OnMouseDrag()
    {
        if (Input.touchCount > 1)
        {
            return;
        }
        if (FurnitureManager.Instance.IsSelectFurniture(furnitureItem))
        {
            furnitureItem.Dragging(transform);
        }
        else
        {
            FurnitureManager.Instance.SelectFurniture(furnitureItem);
        }
    }

    private void OnMouseUp()
    {
        furnitureItem.DeActiveDrag();
    }
}
