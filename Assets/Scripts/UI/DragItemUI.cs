using UnityEngine;
using UnityEngine.EventSystems;

public class DragItemUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [SerializeField, Dropdown(typeof(FurnitureName))] private string ItemID;
    public void OnDrag(PointerEventData eventData)
    {

    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // start drag
        FurnitureItem.OnDragFurniture = true;
        FurnitureManager.Instance.StartDragItem(ItemID);

        // UI này nằm trong BottomSheetPageManager
        BottomSheetPageManager.Instance.blockTouchImage.raycastTarget = false;
        BottomSheetPageManager.Instance.CloseAll();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        
    }

}
