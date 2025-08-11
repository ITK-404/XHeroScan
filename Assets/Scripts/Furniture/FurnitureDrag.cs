
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
        furnitureItem.Dragging(transform);
    }

    private void OnMouseUp()
    {
        furnitureItem.DeActiveDrag();
    }
}
