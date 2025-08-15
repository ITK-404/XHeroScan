using System.Collections.Generic;
using UnityEngine;

public class FurnitureMapping3D : MonoBehaviour
{
    [SerializeField] private List<MappingModelFurniture> furniturePrefabs;
    private void Start()
    {
        var list = FurnitureManager.tempSaveDataFurnitureDatas;
        if (list == null || list.Count == 0)
        {
            Debug.LogWarning("No furniture data to load.");
            return;
        }
        foreach(var item in list)
        {
            var prefab = GetFurniturePrefabByID(item.ItemID);
            if (prefab == null) continue;
            var furnitureInstance = Instantiate(prefab, item.worldPosition, Quaternion.Euler(0, item.rotation, 0));
            furnitureInstance.SetData(item);
        }
    }

    private MappingModelFurniture GetFurniturePrefabByID(string itemItemID)
    {
        foreach (var furniture in furniturePrefabs)
        {
            if (furniture.ItemID == itemItemID)
            {
                return furniture;
            }
        }
        return null;
    }
}