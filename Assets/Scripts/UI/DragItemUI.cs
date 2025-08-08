using UnityEngine;
using UnityEngine.EventSystems;

public class DragItemUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    public FurnitureItem furnitureItemPrefab;
    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log("On Drag UI: over gameobject: "+IsOverUI());
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // start drag
        FurnitureManager.Instance.CreateDragItem(furnitureItemPrefab);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (IsOverUI())
        {
            FurnitureManager.Instance.ClearItem();
        }
        else
        {
            FurnitureManager.Instance.Drop();
        }
    }

    private bool IsOverUI()
    {
        return EventSystem.current.IsPointerOverGameObject();
    }
}