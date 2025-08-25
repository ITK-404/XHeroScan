using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FurnitureData
{
    public string RoomID = string.Empty;
    public string ItemID = string.Empty; // ID duy nhất của item
    public Vector3 worldPosition = Vector3.zero;
    public float Width = 1;
    public float Height = 1;
    public float ObjectHeight = 0.5f;
    public float rotation = 0f; // rotation in degrees around Y axis
}
[Serializable]
public struct DrawItemSize
{
    public float width;
    public float height;
    public float length;

    public Quaternion rotation;
    
    public Vector2 widthMinMax;
    public Vector2 heightMinMax;
    public Vector2 lengthMinMax;

    public float offsetY;
    
}

public enum ItemType
{
    
}



[Serializable]
public struct DrawingTemplate
{
    // Instanced item can use data for reset or initialize data
    public DrawItemSize defaultSize;
    public ItemType defaultItemType;
    public string description;
}
[Serializable]
public struct DrawingInstanced
{
    public string roomID;
    public string itemTemplateID;
    public string itemVariantID;
    public Vector3 worldPosition;
    public ItemType currentItemType;
    public DrawItemSize size;
}