using System;
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

