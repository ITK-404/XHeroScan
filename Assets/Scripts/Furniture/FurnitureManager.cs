using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FurnitureManager : MonoBehaviour
{
    public static FurnitureManager Instance;

    public FurnitureItem furnitureItemPrefab;
    public ScaleByCameraZoom ScaleByCameraZoom;

    [Header("Snap Rotation Settings")]
    public bool IsSnapRotation;
    private List<float> snapAngles = new List<float> { -90, 90f, 180f, 0 };
    [SerializeField] private float snapThreshold = 15f;
    
    private FurnitureItem tempDragItem;
    private Camera mainCam;
    private List<FurnitureItem> runtimeFurnitures = new List<FurnitureItem>();
    
    private void Awake()
    {
        Instance = this;
        mainCam = Camera.main;
        ScaleByCameraZoom = GetComponent<ScaleByCameraZoom>();
    }

    public void StartDragItem(FurnitureItem prefab)
    {
        tempDragItem = Instantiate(prefab != null ? prefab : furnitureItemPrefab);
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
            if(Mathf.Abs(deltaAngle) < snapThreshold)
            {
                return item;
            }
        }

        return angle;
    }
}