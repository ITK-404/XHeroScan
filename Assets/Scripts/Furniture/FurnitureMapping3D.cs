using System.Collections.Generic;
using UnityEngine;

public class FurnitureMapping3D : MonoBehaviour
{
    [SerializeField] private MappingModelFurniture furniturePrefab;
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
            var prefab = furniturePrefab;
            if (prefab == null) continue;
            var furnitureInstance = Instantiate(prefab, item.worldPosition, Quaternion.Euler(0, item.size.rotation.y, 0));
            furnitureInstance.SetData(item);
        }
    }
}