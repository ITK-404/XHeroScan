using UnityEngine;

public class FurnitureRotate : MonoBehaviour
{
    [SerializeField] private FurnitureItem furnitureItem;
    private Quaternion rotate;
    private void OnMouseDown()
    {
        FurnitureItem.SnapShotTemp = furnitureItem.data;
        rotate = furnitureItem.data.size.rotation;
    }

    private void OnMouseDrag()
    {
        InteractionFlags.OnDragPoint = true;
        furnitureItem.RotateToMouse();
        
    }

    private void OnMouseUp()
    {
        InteractionFlags.OnDragPoint = false;

        if (rotate.Equals(furnitureItem.data.size.rotation) == false)
        {
            furnitureItem.CreareEditCommandBySnapShot();
        }
    }
}
