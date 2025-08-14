using UnityEngine;

public class FurnitureRotate : MonoBehaviour
{
    [SerializeField] private FurnitureItem furnitureItem;
    private void OnMouseDrag()
    {
        FurnitureItem.OnDragPoint = true;
        furnitureItem.RotateToMouse();
    }

    private void OnMouseUp()
    {
        FurnitureItem.OnDragPoint = false;
    }
}
