using System;
using System.Collections.Generic;
using UnityEngine;

public enum ScaleHandle
{
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
}

public class FurnitureItem : MonoBehaviour
{
    public static bool OnDrag = false; 
    private static GameObject pointHolder;
    private static Camera mainCam;

    public FurnitureData data;
    public FurniturePoint pointPrefab;
    
    public BoxCollider boxCollider;
    public SpriteRenderer spriteRender;
    private List<FurniturePoint> pointsList = new();

    private void Awake()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        // if (pointHolder == null)
        // {
        //     pointHolder = new GameObject("Point Holder");
        // }

        // foreach (ScaleHandle value in Enum.GetValues(typeof(ScaleHandle)))
        // {
        //     var point = Instantiate(pointPrefab, pointHolder.transform);
        //     point.center = transform;
        //     point.scaleHandle = value;
        //     point.furniture = this;
        //  
        //     pointsList.Add(point);
        // }
    }


  

    private Vector3 startPos;

    public void OnDragPoint(FurniturePoint point)
    {
        
    }

    private void OnMouseDrag()
    {
        transform.position = GetWorldMousePosition();
        OnDrag = true;
    }

    private void OnMouseUp()
    {
        OnDrag = false;
    }

    public void OnStartPoint(FurnitureItem point)
    {
        startPos = GetWorldMousePosition(); 
    }

    private Vector3 GetWorldMousePosition()
    {
        float distance = Vector3.Distance(mainCam.transform.position, FurnitureManager.Instance.transform.position);

        // Chuyển vị trí chuột sang tọa độ thế giới
        Vector3 worldMousePosition = mainCam.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, distance)
        );
        return worldMousePosition;
    }

    private void OnDrawGizmos()
    {
        if (boxCollider == null) return;
        Bounds bounds = boxCollider.bounds;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
        Vector3 dragSize = new Vector3(0.5f, 0, 0.5f);
        // top middle
        Gizmos.DrawWireCube(bounds.center + new Vector3(0, 0, bounds.size.z / 2), dragSize);
        // bottom middle
        Gizmos.DrawWireCube(bounds.center - new Vector3(0, 0, bounds.size.z / 2), dragSize);
        // right middle
        Gizmos.DrawWireCube(bounds.center + new Vector3(bounds.size.x / 2, 0, 0), dragSize);
        // left middle
        Gizmos.DrawWireCube(bounds.center - new Vector3(bounds.size.x / 2, 0, 0), dragSize);

        // top right
        Gizmos.DrawWireCube(bounds.center + new Vector3(bounds.size.x / 2, 0, bounds.size.z / 2), dragSize);
        // top left
        Gizmos.DrawWireCube(bounds.center + new Vector3(-bounds.size.x / 2, 0, bounds.size.z / 2), dragSize);
        // bottom right
        Gizmos.DrawWireCube(bounds.center + new Vector3(bounds.size.x / 2, 0, -bounds.size.z / 2), dragSize);
        // bottom left
        Gizmos.DrawWireCube(bounds.center + new Vector3(-bounds.size.x / 2, 0, -bounds.size.z / 2), dragSize);
    }
}

[Serializable]
public class FurnitureData
{
    public string ItemID = "Item";
    public float Width = 1;
    public float Height = 1;
    public float ObjectHeight = 0.5f;
}

