using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
public partial class FurnitureManager : MonoBehaviour
{
    public class FurniturePlacementController
    {
        public FurnitureItem tempDragItem;
        private FurnitureDrag furnitureDrag;
        private FurnitureManager furnitureManager;

        public FurniturePlacementController(FurnitureManager furnitureManager)
        {
            this.furnitureManager = furnitureManager;
        }

        public void StartDrag(string ItemID)
        {
            // Bởi vì người dùng đã chạm và giữ tay để kéo vật thể trước đó nên các logic này cần được kích hoạt thủ công
            var worldMousePosition = furnitureManager.GetWorldMousePosition();
            worldMousePosition.y = SpawnHeight;
            // setup manual 
            tempDragItem = furnitureManager.InitItemByID(ItemID);
            tempDragItem.transform.position = worldMousePosition;
            tempDragItem?.InitLineAndText();
            // setup logic drag
            furnitureDrag = tempDragItem.GetComponentInChildren<FurnitureDrag>();
            furnitureDrag.SetCanMove(canMove: true);
            furnitureDrag.StartMoveSetup();

            furnitureManager.SelectFurniture(tempDragItem);
            if (tempDragItem == null)
            {
                Debug.LogWarning("Furniture item with ID " + ItemID + " not found.");
                return;
            }
            FurnitureItem.OnDragFurniture = true;

        }

        public void DropDragItem()
        {
            // setup manuall cho creation command
            UndoRedoController.Instance.AddToUndo(new CreateItemCommand(tempDragItem.data));
            // add vào runtime list
            runtimeFurnitures.Add(tempDragItem);
            // tắt logic di chuyển, ghép vật thể vào tường gần nhất
            furnitureDrag.SetCanMove(canMove: false);
            tempDragItem.DeActiveDrag();
           
            if(tempDragItem.lineType != LineType.None)
                tempDragItem.furnitureMergeToWall.ForceSnapToWall();
            
            furnitureManager.SelectFurniture(null);
            tempDragItem = null;

            SaveLoadManager.MakeDirty();
            FurnitureItem.OnDragFurniture = false;

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

      
    }
}

