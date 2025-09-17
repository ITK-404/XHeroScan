using UnityEngine;
using UnityEngine.EventSystems;

public class DragItemUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [SerializeField,Dropdown(typeof(FurnitureName))] private string ItemID;
    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log("On Drag UI: over gameobject: " + IsOverUI());
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // start drag
        FurnitureItem.OnDragFurniture = true;
        FurnitureManager.Instance.StartDragItem(ItemID);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        bool isOverUI = IsOverUI();
        bool isNormalFurniture = FurnitureManager.Instance.IsTempNeedWallToSpawn();
        if (isOverUI)
        {
            FurnitureManager.Instance.ClearDragItem();
        }
        else
        {
            FurnitureManager.Instance.DropDragItem();
        }
        FurnitureItem.OnDragFurniture = false;
    }

    private bool IsOverUI()
    {
        bool isOverUI = EventSystem.current.IsPointerOverGameObject();
        Debug.Log("is Over UI: " + isOverUI);
        return isOverUI;
    }
}
