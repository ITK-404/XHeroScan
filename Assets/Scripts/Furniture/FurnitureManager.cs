using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FurnitureManager : MonoBehaviour
{
    public static FurnitureManager Instance;

    public FurnitureItem furnitureItemPrefab;
    public ScaleByCameraZoom ScaleByCameraZoom;

    [SerializeField] private List<FurnitureItem> furnitureItems = new List<FurnitureItem>();
    
    [Header("Snap Rotation Settings")]
    public bool IsSnapRotation;

    private List<float> snapAngles = new List<float> { -90, 90f, 180f, 0 };
    [SerializeField] private float snapThreshold = 15f;

    private FurnitureItem tempDragItem;
    private Camera mainCam;
    private static List<FurnitureItem> runtimeFurnitures = new List<FurnitureItem>();

    private void Awake()
    {
        Instance = this;
        mainCam = Camera.main;
        ScaleByCameraZoom = GetComponent<ScaleByCameraZoom>();
    }

    private void Start()
    {
        if(tempSaveDataFurnitureDatas == null || tempSaveDataFurnitureDatas.Count == 0)
        {
            Debug.LogWarning("No furniture data to load.");
            return;
        }
        foreach (var data in tempSaveDataFurnitureDatas)
        {
            var prefab = Instance.GetFurniturePrefabByID(data.ItemID);
            if(prefab == null) continue;
            var item = GameObject.Instantiate(prefab);
            item.FetchData(data);
            item.InitLineAndText();
            runtimeFurnitures.Add(item);
        }
        
        tempSaveDataFurnitureDatas.Clear();
        Debug.Log("Loading furniture data: " + tempSaveDataFurnitureDatas.Count);
    }

    public void StartDragItem(string ItemID)
    {
        var prefab = GetFurniturePrefabByID(ItemID);
     
        if (prefab == null)
        {
            Debug.LogWarning("Furniture item with ID " + ItemID + " not found.");
            return;
        }
        
        tempDragItem = Instantiate(prefab != null ? prefab : furnitureItemPrefab);
        SelectFurniture(tempDragItem);
    }
    
    private FurnitureItem GetFurniturePrefabByID(string itemID)
    {
        return furnitureItems.Find(item => item.data.ItemID == itemID);
    }

    public void ClearDragItem()
    {
        Destroy(tempDragItem.gameObject);
        tempDragItem = null;
    }

    public void DropDragItem()
    {
        tempDragItem?.RefreshCheckPoints();
        tempDragItem?.InitLineAndText();
        runtimeFurnitures.Add(tempDragItem);
        tempDragItem = null;
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

        if (currentFurniture && Input.GetKeyDown(KeyCode.A))
        {
            var roomID = CheckpointManager.Instance.FindRoomIDByPoint(currentFurniture.GetWorldPosition());
            if (string.IsNullOrEmpty(roomID))
            {
                Debug.LogWarning("No room found for the current furniture position.");
                currentFurniture.data.RoomID = null;
                return;
            }

            Debug.Log("Is in room: " + roomID);
            currentFurniture.data.RoomID = roomID;
        }
    }

    public void ClearAllFurnitures()
    {
        foreach (var furniture in runtimeFurnitures)
        {
            Destroy(furniture.gameObject);
        }

        runtimeFurnitures.Clear();
    }

    private FurnitureItem currentFurniture;

    public void SelectFurniture(FurnitureItem furniture)
    {
        if (currentFurniture == null)
        {
            currentFurniture = furniture;
            currentFurniture?.EnableCheckPoint();
        }
        else
        {
            if (currentFurniture == furniture)
            {
                currentFurniture.DisableCheckPoint();
                currentFurniture = null;
                return;
            }

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
            if (Mathf.Abs(deltaAngle) < snapThreshold)
            {
                return item;
            }
        }

        return angle;
    }

    public static List<FurnitureData> GetAllFurnitureData()
    {
        List<FurnitureData> dataList = new List<FurnitureData>();
        foreach (var furniture in runtimeFurnitures)
        {
            dataList.Add(furniture.data);
        }

        return dataList;
    }

    public static List<FurnitureData> tempSaveDataFurnitureDatas = new List<FurnitureData>();
    public static void AddFurnitures(List<FurnitureData> saveDataFurnitureDatas)
    {
        tempSaveDataFurnitureDatas = saveDataFurnitureDatas;
    }
}