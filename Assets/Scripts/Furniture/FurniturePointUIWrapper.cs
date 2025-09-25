using UnityEngine;
using UnityEngine.EventSystems;

public class FurniturePointUIWrapper : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CheckpointType checkpointType;
    public FurnitureDragPointWarperUI warperUI;
    public void OnBeginDrag(PointerEventData eventData)
    {
        warperUI.StartDrag(checkpointType); 
    }

    public void OnDrag(PointerEventData eventData)
    {
        warperUI.Dragging(checkpointType);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        warperUI.EndDrag(checkpointType);
    }
}