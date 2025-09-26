using System;
using UnityEngine;

public class FurniturePoint : MonoBehaviour
{
    public static Camera mainCam;
    public CheckpointType checkpointType;
    public FurnitureItem furniture;
    public ResizeAxis resizeAxis;
    private void Awake()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("LoadDefaultPreset")]
    private void LoadDefaultPreset()
    {
        switch (checkpointType)
        {
            case CheckpointType.Left:
            case CheckpointType.Right:
                resizeAxis = ResizeAxis.X;
                break;
            case CheckpointType.Top:
            case CheckpointType.Bottom:
                resizeAxis = ResizeAxis.Z;
                break;
            case CheckpointType.TopLeft:
            case CheckpointType.TopRight:
            case CheckpointType.BottomLeft:
            case CheckpointType.BottomRight:
                resizeAxis = ResizeAxis.XZ;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
#endif
    private float width, length;
    private void OnMouseDown()
    {
        StartDragPoint();
    }

    public void StartDragPoint()
    {
        FurnitureItem.SnapShotTemp = furniture.data;
        width = furniture.width;
        length = furniture.length;
    }

    private void OnMouseDrag()
    {
        Dragging();
    }

    public void Dragging()
    {
        if (furniture == null)
        {
            Destroy(gameObject);
            return;
        }

        InteractionFlags.OnDragMovePoint = true;
        furniture.DragPoint(this);
    }

    private void OnMouseUp()
    {
        EndDrag();
    }

    public void EndDrag()
    {
        InteractionFlags.OnDragMovePoint = false;

        if (width != furniture.width || length != furniture.length)
        {
            furniture.CreareEditCommandBySnapShot();
        }
    }

    public ResizeAxis GetReSizeAxis()
    {
        return resizeAxis;
    }
}
