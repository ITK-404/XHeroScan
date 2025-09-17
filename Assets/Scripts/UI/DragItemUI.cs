using UnityEngine;
using UnityEngine.EventSystems;

public class DragItemUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [SerializeField, Dropdown(typeof(FurnitureName))] private string ItemID;
    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log("On Drag UI: over gameobject: " + IsOverUI());

    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // start drag
        FurnitureItem.OnDragFurniture = true;
        FurnitureManager.Instance.StartDragItem(ItemID);

        // UI này nằm trong BottomSheetPageManager
        BottomSheetPageManager.Instance.blockTouchImage.raycastTarget = false;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        BottomSheetPageManager.Instance.blockTouchImage.raycastTarget = true;
        
        var tempFurniture = FurnitureManager.Instance.TempDragItem;
        bool isOverUI = IsOverUI();
        bool isNormalFurniture = tempFurniture.lineType == LineType.None;
        float minDis = float.MaxValue;
        Vector3 firstDoorPoint = Vector3.zero;
        WallLine wallLine = null;
        
        if (isNormalFurniture == false)
        {
            foreach (var room in RoomStorage.rooms)
            {
                tempFurniture.furnitureMergeToWall.
                    FindNearestWallLine(room, tempFurniture.GetWorldPosition(), 0.2f, ref minDis, ref wallLine, ref firstDoorPoint);
            }
        }
        
        Debug.Log($"Is Over UI {isOverUI} WL not null {wallLine != null} normal {isNormalFurniture}");

        if (isOverUI == false && wallLine != null && isNormalFurniture == false)
        {
            FurnitureManager.Instance.DropDragItem();
            tempFurniture.SetWorldPosition(firstDoorPoint);
        }
        else
        {
            FurnitureManager.Instance.ClearDragItem();
        }
        FurnitureItem.OnDragFurniture = true;
    }

    private bool IsOverUI()
    {
        bool isOverUI = EventSystem.current.IsPointerOverGameObject();
        Debug.Log("is Over UI: " + isOverUI);
        return isOverUI;
    }
}
