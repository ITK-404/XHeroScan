using Org.BouncyCastle.Ocsp;
using UnityEngine;
using UnityEngine.UIElements;

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
        FurnitureItem.OnDragPoint = true;
        furnitureItem.RotateToMouse();
        
    }

    private void OnMouseUp()
    {
        FurnitureItem.OnDragPoint = false;

        if (rotate.Equals(furnitureItem.data.size.rotation) == false)
        {
            furnitureItem.CreareEditCommandBySnapShot();
        }
    }
}
