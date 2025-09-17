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

            if (tempDragItem == null)
            {
                Debug.LogWarning("Furniture item with ID " + ItemID + " not found.");
                return;
            }

            furnitureManager.SelectFurniture(tempDragItem);
        }

        public void ClearDragItem()
        {
            Destroy(tempDragItem.gameObject);
            tempDragItem = null;
        }

        public void DropDragItem()
        {
            tempDragItem?.InitLineAndText();
            runtimeFurnitures.Add(tempDragItem);
            tempDragItem = null;

            SaveLoadManager.MakeDirty();
        }

        public void Update()
        {
            if (tempDragItem)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    TryDropFurniture();
                }

                if (tempDragItem == null) return;
                var worldMousePosition = furnitureManager.GetWorldMousePosition();
                tempDragItem.transform.position = new Vector3(worldMousePosition.x, SpawnHeight, worldMousePosition.z);
            }
        }

        private void TryDropFurniture()
        {
            BottomSheetPageManager.Instance.blockTouchImage.raycastTarget = true;

            var tempFurniture = tempDragItem;
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
                DropDragItem();
                tempFurniture.SetWorldPosition(firstDoorPoint);
                tempFurniture.furnitureMergeToWall.ForceSnapToWall();
            }
            else
            {
                ClearDragItem();
            }
            FurnitureItem.OnDragFurniture = true;
        }
    }


    private static bool IsOverUI()
    {
        bool isOverUI = EventSystem.current.IsPointerOverGameObject();
        Debug.Log("is Over UI: " + isOverUI);
        return isOverUI;
    }
}

