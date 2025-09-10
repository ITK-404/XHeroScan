using Org.BouncyCastle.Ocsp;
using UnityEngine;

public class FurnitureDrag : MonoBehaviour
{
    [SerializeField] private FurnitureItem furnitureItem;
    private Vector3 startPosition;
    private void OnMouseDown()
    {
        furnitureItem.StartDrag();
        FurnitureItem.SnapShotTemp = furnitureItem.data;
        startPosition = furnitureItem.GetWorldPosition();
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
        
    }

    private void OnMouseUp()
    {
        furnitureItem.DeActiveDrag();

        if(startPosition != furnitureItem.GetWorldPosition())
        {
            furnitureItem.CreareEditCommandBySnapShot();
        }
    }
}