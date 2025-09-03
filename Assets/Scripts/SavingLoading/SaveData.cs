using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    public string timestamp;
    public List<SavedPath> paths = new List<SavedPath>(); // Room
    public List<SavedFloor> floors = new List<SavedFloor>(); // Floor
    public List<DrawingInstanced> furnitureDatas = new List<DrawingInstanced>();
}

[System.Serializable]
public class SavedWallLine
{
    public Vector3 start;
    public Vector3 end;
    public LineType type;
    public bool isVisible;
    public float distanceHeight;
    public float Height;
    public bool isManualConnection;
    public string materialFront;
    public string materialBack;
}


[Serializable]
public class SavedPath
{
    public string roomID;
    public string groupID;        
    public string roomName;       
    public string floorMaterial;  
    public string floorID;
    
    public List<Vector2Serializable> points;
    public List<Vector2Serializable> pointsExtra;
    public List<float> heights;
    public List<SavedWallLine> wallLines;
    public Vector2Serializable compass;
    public Vector2Serializable center;
    public float headingCompass;
}

[Serializable]
public class Vector2Serializable
{
    public float x, y;

    public Vector2Serializable(Vector2 v)
    {
        x = v.x;
        y = v.y;
    }

    public Vector2 ToVector2()
    {
        return new Vector2(x, y);
    }
}

[Serializable]
public class SavedFloor
{
    public string floorID;
    public string floorName;

    public List<Vector2Serializable> points;
    public List<float> heights;
    public List<SavedFloorLine> floorLine;
    public Vector2Serializable center;
    public List<string> roomIDs;
}

[Serializable]
public class SavedFloorLine
{
    public Vector3 start;
    public Vector3 end;
}
