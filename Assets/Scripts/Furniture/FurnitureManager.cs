using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
public partial class FurnitureManager : MonoBehaviour
{
    public static FurnitureManager Instance;
    public static List<DrawingInstanced> tempSaveDataFurnitureDatas = new List<DrawingInstanced>();
    private static List<FurnitureItem> runtimeFurnitures = new List<FurnitureItem>();

    public FurnitureItem furnitureItemPrefab;
    public ScaleByCameraZoom ScaleByCameraZoom;

    [Header("Snap Rotation Settings")]
    [SerializeField] private float snapRotationThreshold = 15f;
    [SerializeField] private List<FurnitureItem> furnitureItems = new List<FurnitureItem>();
    public bool IsSnapRotation;

    [Header("Drag")]
    private FurnitureItem currentFurniture;
    [Header("Rotate")]
    private List<float> snapAngles = new List<float> { -90, 90f, 180f, 0 };

    private Camera mainCam;
    public FurnitureItem CurrentFurnitureItem() => currentFurniture;
    public const float SpawnHeight = 5;

    public FurnitureItem TempDragItem => placementController.tempDragItem;
    public FurniturePlacementController placementController;
    private void Awake()
    {
        Instance = this;
        placementController = new FurniturePlacementController(this);
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
        runtimeFurnitures.Clear();

        // clear before run
        if (tempSaveDataFurnitureDatas.Count > 0)
        {
            foreach (var data in tempSaveDataFurnitureDatas)
            {
                var prefab = Instance.GetFurniturePrefabByID(data.itemTemplateID);
                if (prefab == null) continue;
                var item = GameObject.Instantiate(prefab);
                item.FetchData(data);
                item.InitLineAndText();
                if (item.lineType == LineType.Door || item.lineType == LineType.Window)
                {
                    item.furnitureMergeToWall.ForceSnapToWall();
                }
                runtimeFurnitures.Add(item);
            }

        }
        Debug.Log("Loading furniture data: " + tempSaveDataFurnitureDatas.Count);
        Debug.Log("Loading Runtime data: " + runtimeFurnitures.Count);
        tempSaveDataFurnitureDatas.Clear();
    }

    public void SaveRuntimesToTemp()
    {
        Debug.Log("Loading furniture data: " + tempSaveDataFurnitureDatas.Count);
        Debug.Log("Loading Runtime data: " + runtimeFurnitures.Count);
        tempSaveDataFurnitureDatas.Clear();
        tempSaveDataFurnitureDatas = GetAllFurnitureData();
        runtimeFurnitures.Clear();
    }

    public void RemoveFromRuntime(FurnitureItem furnitureItem)
    {
        runtimeFurnitures.Remove(furnitureItem);
    }

    public void StartDragItem(string ItemID)
    {
        placementController.StartDrag(ItemID);
    }

    private FurnitureItem InitItemByID(string ItemID)
    {
        var prefab = GetFurniturePrefabByID(ItemID);

        if (prefab == null)
        {
            Debug.LogWarning("Furniture item with ID " + ItemID + " not found.");
            return null;
        }
        var instance = Instantiate(prefab != null ? prefab : furnitureItemPrefab);
        instance.data.InitNewInstanceID();
        return instance;
    }

    private FurnitureItem GetFurniturePrefabByID(string itemID)
    {
        return furnitureItems.Find(item => item.data.itemTemplateID == itemID);
    }


    public void SpawnFurnitureCenterScreen(string itemID)
    {
        var worldPointFromViewPort = mainCam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f));
        var centerPosition = new Vector3(worldPointFromViewPort.x, SpawnHeight, worldPointFromViewPort.z);
        var item = SpawnFurniture(itemID, centerPosition);
        UndoRedoController.Instance.AddToUndo(new CreateItemCommand(item.data));

    }

    public FurnitureItem SpawnFurniture(string itemID, Vector3 position)
    {
        var furniture = InitItemByID(itemID);

        if (!furniture) return null;

        furniture.transform.position = position;
        furniture.InitLineAndText();
        runtimeFurnitures.Add(furniture);
        Debug.Log("Spawn Position: " + position);


        return furniture;
    }

    private void Update()
    {

        placementController.Update();

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

        if (Input.GetKeyDown(KeyCode.Space))
        {
            SpawnFurnitureCenterScreen("bed_1");
        }
    }

    public void ClearAllFurnitures()
    {
        foreach (var furniture in runtimeFurnitures)
        {
            Destroy(furniture.gameObject);
        }

        runtimeFurnitures.Clear();
        tempSaveDataFurnitureDatas.Clear();
    }


    public void SelectFurniture(FurnitureItem furniture)
    {
        if (placementController.IsDragTempFurniture())
        {
            return;
        }
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

    public FurnitureItem GetFurnitureByInstanceID(string instanceID)
    {
        Debug.Log("Instance: ID" + instanceID);
        foreach (var item in runtimeFurnitures)
        {
            if (item.data.instanceID.Equals(instanceID))
            {
                return item;
            }
        }

        return null;
    }
    [SerializeField] private Vector3 offset = Vector3.zero;
    private Vector3 GetWorldMousePosition()
    {
        float distance = Vector3.Distance(mainCam.transform.position, transform.position);

        // Chuyển vị trí chuột sang tọa độ thế giới
        Vector3 worldMousePosition = mainCam.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, distance)
        );
        return worldMousePosition + offset;
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

    public void ForceSnapAllToNearestRoom()
    {
        Debug.Log("try snap to nearest room");
        foreach (var item in runtimeFurnitures)
        {
            item.furnitureMergeToWall.ForceSnapToWall();
        }
    }

    public void ResetAttachedItems()
    {
        foreach (var item in runtimeFurnitures)
        {
            item.furnitureMergeToWall.ResetAttached();
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
                if (item.furnitureMergeToWall.IsInWall() == false) continue;
                exportList.Add(item.furnitureMergeToWall.PDFWallLine);
            }
        }
        //Debug.Log("Xuất PDF: furniture Count" + exportList.Count);
        return exportList;
    }

    public List<DrawingInstanced> GetFurnitureInsideRoom(string iD)
    {
        List<DrawingInstanced> furnitures = new List<DrawingInstanced>();
        foreach (var item in runtimeFurnitures)
        {
            if (item.data.roomID == iD)
            {
                furnitures.Add(item.data);
            }
        }
        return furnitures;
    }

    public List<FurnitureItem> GetAllFurniture()
    {
        return runtimeFurnitures;
    }
}
