using UnityEngine;
using UnityEngine.EventSystems;

public class FurnitureRotateUIWrapper : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform Rect;
    public FurnitureDragPointWarperUI furnitureDrag;
    private void Awake()
    {
        if (Rect == null)
            Rect = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        furnitureDrag.OnBeginRotateDrag();

    }
  
    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        furnitureDrag.OnEndRotateDrag();

    }

}
