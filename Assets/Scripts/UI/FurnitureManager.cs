using System.Collections.Generic;
using UnityEngine;

public class FurnitureManager : MonoBehaviour
{
    public static FurnitureManager Instance;
    public Sprite defaultSprite;
    public FurnitureItem furnitureItemPrefab;

    private FurnitureItem tempDragItem;
    private Camera mainCam;
    public Vector3 offset;
    private List<FurnitureItem> runtimeFurnitures = new List<FurnitureItem>();
    
    private void Awake()
    {
        Instance = this;
        mainCam = Camera.main;
    }
    
    public void CreateDragItem(FurnitureItem prefab)
    {
        tempDragItem = Instantiate(prefab != null ? prefab : furnitureItemPrefab);
    }

    public void ClearItem()
    {
        Destroy(tempDragItem.gameObject);
        tempDragItem = null;
    }

    public void Drop()
    {
        runtimeFurnitures.Add(tempDragItem);
        tempDragItem = null;
    }

    private void Update()
    {
        if (tempDragItem)
        {
            tempDragItem.transform.position = GetWorldMousePosition();
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
    
    private Vector3 GetWorldMousePosition()
    {
        float distance = Vector3.Distance(mainCam.transform.position, transform.position);

        // Chuyển vị trí chuột sang tọa độ thế giới
        Vector3 worldMousePosition = mainCam.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, distance)
        );
        return worldMousePosition;
    }
}