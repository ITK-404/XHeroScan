using UnityEngine;

public class FurniturePoint : MonoBehaviour
{
    public static Camera mainCam;
    public Transform center;
    public CheckpointType checkpointType;
    public FurnitureItem furniture;
    private void Awake()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }
    }

    private void OnMouseDrag()
    {
        if(furniture == null)
        {
            Destroy(gameObject);
            return;
        }
        FurnitureItem.OnDragPoint = true;
        furniture.DragPoint(this);
    }

    private void OnMouseUp()
    {
        FurnitureItem.OnDragPoint = false;
    }
}