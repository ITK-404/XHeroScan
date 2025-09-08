using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FurnitureManager : MonoBehaviour
{
    public static FurnitureManager Instance;
    public static List<DrawingInstanced> tempSaveDataFurnitureDatas = new List<DrawingInstanced>();

    public FurnitureItem furnitureItemPrefab;
    public ScaleByCameraZoom ScaleByCameraZoom;

    [Header("Snap Rotation Settings")]
    [SerializeField] private float snapRotationThreshold = 15f;
    [SerializeField] private List<FurnitureItem> furnitureItems = new List<FurnitureItem>();
    public bool IsSnapRotation;

    [Header("Drag")]
    private FurnitureItem tempDragItem;
    private FurnitureItem currentFurniture;
    [Header("Rotate")]
    private static List<FurnitureItem> runtimeFurnitures = new List<FurnitureItem>();
    private List<float> snapAngles = new List<float> { -90, 90f, 180f, 0 };

    private Camera mainCam;
    public FurnitureItem CurrentFurnitureItem() => currentFurniture;
    public const float SpawnHeight = 5;


    private void Awake()
    {
        Instance = this;
        mainCam = Camera.main;
        ScaleByCameraZoom = GetComponent<ScaleByCameraZoom>();
    }

    private void Start()
    {
        if (tempSaveDataFurnitureDatas == null || tempSaveDataFurnitureDatas.Count == 0)
        {
            Debug.LogWarning("No furniture data to load.");
            return;
        }
        // clear before run
        runtimeFurnitures.Clear();

        foreach (var data in tempSaveDataFurnitureDatas)
        {
            var prefab = Instance.GetFurniturePrefabByID(data.itemTemplateID);
            if (prefab == null) continue;
            var item = GameObject.Instantiate(prefab);
            item.FetchData(data);
            item.InitLineAndText();
            runtimeFurnitures.Add(item);
        }

        // clear after using
        tempSaveDataFurnitureDatas.Clear();
        Debug.Log("Loading furniture data: " + tempSaveDataFurnitureDatas.Count);
    }

    public void RemoveFromRuntime(FurnitureItem furnitureItem)
    {
        runtimeFurnitures.Remove(furnitureItem);
    }

    public void StartDragItem(string ItemID)
    {
        tempDragItem = InitItemByID(ItemID);

        if (tempDragItem == null)
        {
            Debug.LogWarning("Furniture item with ID " + ItemID + " not found.");
            return;
        }

        SelectFurniture(tempDragItem);
    }

    private FurnitureItem InitItemByID(string ItemID)
    {
        var prefab = GetFurniturePrefabByID(ItemID);

        if (prefab == null)
        {
            Debug.LogWarning("Furniture item with ID " + ItemID + " not found.");
            return null;
        }

        return Instantiate(prefab != null ? prefab : furnitureItemPrefab);
    }

    private FurnitureItem GetFurniturePrefabByID(string itemID)
    {
        return furnitureItems.Find(item => item.data.itemTemplateID == itemID);
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

    public void SpawnFurnitureCenterScreen(string itemID)
    {
        var worldPointFromViewPort = mainCam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f));
        var centerPosition = new Vector3(worldPointFromViewPort.x, SpawnHeight, worldPointFromViewPort.z);
        SpawnFurniture(itemID, centerPosition);
    }

    public FurnitureItem SpawnFurniture(string itemID, Vector3 position)
    {
        var furniture = InitItemByID(itemID);
        furniture.transform.position = position;
        furniture.InitLineAndText();
        runtimeFurnitures.Add(furniture);
        Debug.Log("Spawn Position: " + position);
        return furniture;
    }

    private void Update()
    {
        if (tempDragItem)
        {
            tempDragItem.transform.position = GetWorldMousePosition();
        }

        if (Input.touchCount >= 2)
        {
            SelectFurniture(null);
            return;
        }
        // select handle
        if (currentFurniture && Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            var mousePos = Input.mousePosition;
            if (!Physics.Raycast(mainCam.ScreenPointToRay(mousePos), out var result))
            {
                SelectFurniture(null);
            }
        }
        // for testing 
        //if (currentFurniture && Input.GetKeyDown(KeyCode.A))
        //{
        //    var roomID = CheckpointManager.Instance.FindRoomIDByPoint(currentFurniture.GetWorldPosition());
        //    if (string.IsNullOrEmpty(roomID))
        //    {
        //        Debug.LogWarning("No room found for the current furniture position.");
        //        currentFurniture.data.roomID = null;
        //        return;
        //    }

        //    Debug.Log("Is in room: " + roomID);
        //    currentFurniture.data.roomID = roomID;
        //}

        //if (Input.GetKeyDown(KeyCode.Space))
        //{
        //    SpawnFurnitureCenterScreen("bed_1");
        //}
    }

    public void ClearAllFurnitures()
    {
        foreach (var furniture in runtimeFurnitures)
        {
            Destroy(furniture.gameObject);
        }

        runtimeFurnitures.Clear();
    }


    public void SelectFurniture(FurnitureItem furniture)
    {
        if (currentFurniture == null)
        {
            currentFurniture = furniture;
            currentFurniture?.EnableCheckPoint();
        }
        else
        {
            //if (currentFurniture == furniture)
            //{
            //    currentFurniture.DisableCheckPoint();
            //    currentFurniture = null;
            //    return;
            //}

            currentFurniture?.DisableCheckPoint();
            currentFurniture = furniture;
            currentFurniture?.EnableCheckPoint();
        }
  
    }

    private Vector3 GetWorldMousePosition()
    {
        float distance = Vector3.Distance(mainCam.transform.position, transform.position);

        // Chuyển vị trí chuột sang tọa độ thế giới
        Vector3 worldMousePosition = mainCam.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, distance)
        );
        return worldMousePosition;
    }

    public bool IsSelectFurniture(FurnitureItem furnitureItem)
    {
        return currentFurniture == furnitureItem;
    }


    public float CheckSnapRotation(float angle)
    {
        if (!IsSnapRotation) return angle;

        foreach (var item in snapAngles)
        {
            var deltaAngle = Mathf.DeltaAngle(angle, item);
            if (Mathf.Abs(deltaAngle) < snapRotationThreshold)
            {
                return item;
            }
        }

        return angle;
    }

    public static List<DrawingInstanced> GetAllFurnitureData()
    {
        List<DrawingInstanced> dataList = new List<DrawingInstanced>();
        foreach (var furniture in runtimeFurnitures)
        {
            dataList.Add(furniture.data);
        }

        return dataList;
    }

    public static void AddFurnitures(List<DrawingInstanced> saveDataFurnitureDatas)
    {
        tempSaveDataFurnitureDatas = saveDataFurnitureDatas;
    }
    public LayerMask furnitureLayerMask;
    public bool TryPickFurniture()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        FurnitureItem item = null;
        if (Physics.Raycast(ray, out var hitInfo, 1000000f, furnitureLayerMask))
        {
            item = hitInfo.collider.GetComponentInParent<FurnitureItem>();
        }

        SelectFurniture(item);
        return item != null;
    }

    public void CheckWallLineValidInRoom()
    {
        foreach (var item in runtimeFurnitures)
        {
            item.furnitureMergeToWall.CheckWallLineIsValidInRoom();
        }
    }

    public void TrySnapToNearestWall()
    {
        foreach (var item in runtimeFurnitures)
        {
            item.furnitureMergeToWall.SnapToNearestWallOfCurrentRoom();
        }
    }

    public void TrySnapToNearestRoom()
    {
        Debug.Log("try snap to nearest room");
        foreach (var item in runtimeFurnitures)
        {
            item.furnitureMergeToWall.SnapTemp(true);
        }
    }

    public void SetVisibleObjects(string roomID, bool state)
    {
        Debug.Log("Set visible objects");
        var room = RoomStorage.GetRoomByID(roomID);

        foreach (var item in runtimeFurnitures)
        {
            if (item.lineType == LineType.Door || item.lineType == LineType.Window) continue;
            //if (item.data.roomID != roomID) continue;

            Debug.Log("Find and check valid furniture");
            Vector3 worldPosition = item.GetWorldPosition();
            Vector2 worldPosition2D = new Vector2(worldPosition.x, worldPosition.z);
            bool isInPolygon = CheckpointManager.IsPointInPolygon(worldPosition2D, room.checkpoints);
            bool isAttachedToRoom = item.TryGetComponent(out FurnitureVisible value) && value.GetRoomID() == roomID;

            Debug.Log("Is In Polygon: " + isInPolygon);
            Debug.Log("Is attachef to room: " + isAttachedToRoom);

            if (isInPolygon || isAttachedToRoom)
            {
                Debug.Log($"{item.gameObject.name} này nằm trong room", item.gameObject);
                value.Show(state);
            }

            if (isInPolygon)
            {
                item.data.roomID = roomID;
            }


        }
    }

    public List<WallLine> GetPdfWallLine()
    {
        List<WallLine> exportList = new();

        foreach (FurnitureItem item in runtimeFurnitures)
        {
            if (item.lineType == LineType.Door || item.lineType == LineType.Window)
            {
                exportList.Add(item.furnitureMergeToWall.TypedWallLine);
            }
        }
        return exportList;
    }
}
