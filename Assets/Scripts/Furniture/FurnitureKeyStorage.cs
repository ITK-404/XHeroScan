using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Furniture Key Storage", menuName = "ScriptableObjects/FurnitureKeyStorage", order = 1)]
public class FurnitureKeyStorage : ScriptableObject
{
    [Serializable]
    public struct KeyStorage
    {
        public string ItemID;
        public Sprite sprite2D;
        public GameObject model3D;        
    }
    
    public List<KeyStorage> furnitureKeys;
    
    public KeyStorage GetFurnitureKeyByID(string itemID)
    {
        foreach (var key in furnitureKeys)
        {
            if (key.ItemID == itemID)
            {
                return key;
            }
        }
        Debug.LogWarning($"FurnitureKeyStorage: Không tìm thấy Key với ID: {itemID}");
        return default;
    }
}