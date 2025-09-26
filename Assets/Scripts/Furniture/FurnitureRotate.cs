using UnityEngine;

public class FurnitureRotate : MonoBehaviour
{
    [SerializeField] private FurnitureItem furnitureItem;
    private Quaternion rotate;

    private void Awake()
    {
    }

    private void OnMouseDown()
    {
        StartRotate();
    }

    public void StartRotate()
    {
        FurnitureItem.SnapShotTemp = furnitureItem.data;
        rotate = furnitureItem.data.size.rotation;
    }

    public void OnMouseDrag()
    {
        Dragging();
    }

    public void Dragging()
    {
        InteractionFlags.OnDragMovePoint = true;
        furnitureItem.RotateToMouse();
    }

    private void OnMouseUp()
    {
        OnEndDrag();
    }

    public void OnEndDrag()
    {
        InteractionFlags.OnDragMovePoint = false;

        if (rotate.Equals(furnitureItem.data.size.rotation) == false)
        {
            furnitureItem.CreareEditCommandBySnapShot();
        }
    }
}
