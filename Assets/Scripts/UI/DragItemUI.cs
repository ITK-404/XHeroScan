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
        var lineType = FurnitureManager.Instance.IsItemCanDragToWall(ItemID);
        bool canDrag = (lineType == LineType.Door || lineType == LineType.Window) && RoomStorage.rooms.Count > 0 || lineType == LineType.None;
        if (!canDrag)
        {
            ModularPopup.CreatePopup("Không thể thêm vật thể", "Cần tạo ít nhất một căn phòng", ModularPopup.PopupAsset.toastPopupError);
            return;
        }
        FurnitureManager.Instance.StartDragItem(ItemID);
        BottomSheetPageManager.Instance.blockTouchImage.raycastTarget = false;
        BottomSheetPageManager.Instance.CloseAll();
        Debug.Log($"Item ID Drag: " + ItemID);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }

}
