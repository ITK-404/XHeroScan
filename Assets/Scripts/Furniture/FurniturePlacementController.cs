using System;
using UnityEngine;
using UnityEngine.EventSystems;
public partial class FurnitureManager : MonoBehaviour
{
    public class FurniturePlacementController
    {
        public FurnitureItem tempDragItem;
        private FurnitureManager furnitureManager;

        public FurniturePlacementController(FurnitureManager furnitureManager)
        {
            this.furnitureManager = furnitureManager;
        }

        public void StartDrag(string ItemID)
        {
            tempDragItem = furnitureManager.InitItemByID(ItemID);
            tempDragItem.GetComponentInChildren<FurnitureDrag>().SetCanMove(canMove: true);
            tempDragItem?.InitLineAndText();
            furnitureManager.SelectFurniture(tempDragItem);
            if (tempDragItem == null)
            {
                Debug.LogWarning("Furniture item with ID " + ItemID + " not found.");
                return;
            }

            //furnitureManager.SelectFurniture(tempDragItem);
        }

        //    public void ClearDragItem()
        //    {
        //        Destroy(tempDragItem.gameObject);
        //        tempDragItem = null;
        //    }

        public void DropDragItem()
        {
            UndoRedoController.Instance.AddToUndo(new CreateItemCommand(tempDragItem.data));
            runtimeFurnitures.Add(tempDragItem);

            tempDragItem.GetComponentInChildren<FurnitureDrag>().SetCanMove(canMove: false);
            tempDragItem = null;
            furnitureManager.SelectFurniture(null);

            SaveLoadManager.MakeDirty();
        }

        public void Update()
        {
            if (tempDragItem)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    DropDragItem();
                }
            }
        }

        //    private void TryDropFurniture()
        //    {
        //        Debug.Log($"BottomSheetPageManager.Instance.blockTouchImage.raycastTarget: {BottomSheetPageManager.Instance.blockTouchImage != null}");
        //        BottomSheetPageManager.Instance.blockTouchImage.raycastTarget = true;
        //        // Khởi tạo các biến cần thiết
        //        var worldPosition = tempDragItem.furnitureMergeToWall.GetCenterPosition();
        //        var tempFurniture = tempDragItem;
        //        var firstDoorPoint = Vector3.zero;

        //        bool isOverUI = IsOverUI();
        //        bool isNormalFurniture = tempFurniture.lineType == LineType.None;
        //        bool canMergeToWall = tempFurniture.furnitureMergeToWall.IsDragPosCanMerge(worldPosition, ref firstDoorPoint);

        //        Debug.Log($"Is Over UI {isOverUI} WL not null {canMergeToWall} normal {isNormalFurniture}");
        //        if (isNormalFurniture)
        //        {
        //            DropDragItem();
        //        }
        //        else if (!isOverUI && !isNormalFurniture && canMergeToWall)
        //        {
        //            DropDragItem();
        //            tempFurniture.MoveAnchorToPositionWithoutChangeShape(CheckpointType.Bottom, firstDoorPoint);
        //            tempFurniture.furnitureMergeToWall.ForceSnapToWall();
        //        }
        //        else
        //        {
        //            ClearDragItem();
        //        }
        //        FurnitureItem.OnDragFurniture = false;
        //    }

        //    public bool IsDragTempFurniture()
        //    {
        //        return tempDragItem != null;
        //    }
        //}


        //private static bool IsOverUI()
        //{
        //    bool isOverUI = EventSystem.current.IsPointerOverGameObject();
        //    Debug.Log("is Over UI: " + isOverUI);
        //    return isOverUI;
        //}
    }
}

