using System;
using System.Collections.Generic;
using UnityEngine;

public enum CheckpointType
{
    Left,
    Right,
    Top,
    Bottom
}

public enum ResizeAxis
{
    X,
    Z,
    XZ
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
    [Header("Point")]
    [SerializeField] private FurniturePoint leftPoint;
    [SerializeField] private FurniturePoint rightPoint;
    [SerializeField] private FurniturePoint topPoint;
    [SerializeField] private FurniturePoint bottomPoint;

    private FurniturePoint[] pointsArray;
    public float width, height = 1;
    private void Awake()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        SetupPoint(leftPoint);
        SetupPoint(rightPoint);
        SetupPoint(topPoint);
        SetupPoint(bottomPoint);

        pointsArray = GetComponentsInChildren<FurniturePoint>();
    }


    private void SetupPoint(FurniturePoint point)
    {
        point.center = transform;
        //point.scaleHandle = value;
        point.furniture = this;
    }

    private Vector3 startPos;

    public void OnDragPoint(FurniturePoint point)
    {

        Vector3 newPos = GetWorldMousePosition();
        newPos = point.transform.parent.InverseTransformPoint(newPos);
        Bounds bounds = new Bounds();
        bounds.center = spriteRender.transform.localPosition;
        bounds.size = new Vector2(width, height);
        
        switch (point.checkpointType)
        {
            case CheckpointType.Left:
                ResizeWithAnchor(newPos, point.transform, rightPoint.transform, ref bounds, ResizeAxis.X);
                break;
            case CheckpointType.Right:
                ResizeWithAnchor(newPos, point.transform, leftPoint.transform, ref bounds, ResizeAxis.X);
                break;
            case CheckpointType.Top:
                ResizeWithAnchor(newPos, point.transform, bottomPoint.transform, ref bounds, ResizeAxis.Z);
                break;
            case CheckpointType.Bottom:
                ResizeWithAnchor(newPos, point.transform, topPoint.transform, ref bounds, ResizeAxis.Z);
                break;
            default:
                break;
        }
        
        width = bounds.size.x;
        height = bounds.size.y;
        spriteRender.transform.localPosition = bounds.center;

        foreach (var item in pointsArray)
        {
            Recalculator(item, bounds);
        }
    }


    private void Update()
    {
        width = Mathf.Clamp(width, 0.1f, 100);
        height = Mathf.Clamp(height, 0.1f, 100);
        spriteRender.transform.localScale = new Vector3(width, height, 1);
    }

    public void ResizeWithAnchor(Vector3 localPoint, Transform dragPoint, Transform anchorPoint, ref Bounds bounds, ResizeAxis resizeAxis)
    {

        float xPos = resizeAxis == ResizeAxis.XZ || resizeAxis == ResizeAxis.X ? localPoint.x : dragPoint.localPosition.x;
        float zPos = resizeAxis == ResizeAxis.XZ || resizeAxis == ResizeAxis.Z ? localPoint.z : dragPoint.localPosition.z;
        
        Vector3 newPosFiler = new Vector3(xPos, dragPoint.transform.position.y, zPos);
        
        Vector3 anchor = anchorPoint.localPosition;
        Vector3 center = (anchor + newPosFiler) / 2f;


        //float xCenter = resizeAxis == ResizeAxis.XZ || resizeAxis == ResizeAxis.X ? bounds.center.x : center.x;
        //float zCenter = resizeAxis == ResizeAxis.XZ || resizeAxis == ResizeAxis.Z ? bounds.center.z : center.z;

        //Vector3 filerCenter = new Vector3(xCenter, center.y, zCenter);

        Vector3 size = bounds.size;

        switch (resizeAxis)
        {
            case ResizeAxis.X:
                size.x = Mathf.Abs(newPosFiler.x - anchor.x);
                break;
            case ResizeAxis.Z:
                size.y = Mathf.Abs(newPosFiler.z - anchor.z);
                break;
            case ResizeAxis.XZ:
                size.x = Mathf.Abs(newPosFiler.x - anchor.x);
                size.y = Mathf.Abs(newPosFiler.z - anchor.z);
                break;
            default:
                break;
        }
        bounds.center = center;
        bounds.size = size;

        //dragPoint.localPosition = newPosFiler;

    }

    private void Recalculator(FurniturePoint point, Bounds bounds)
    {
        Vector3 newPosition = point.transform.localPosition;
        switch (point.checkpointType)
        {
            case CheckpointType.Left:
                newPosition = bounds.center - new Vector3(bounds.extents.x, 0, 0);
                break;
            case CheckpointType.Right:
                newPosition = bounds.center + new Vector3(bounds.extents.x, 0, 0);
                break;
            case CheckpointType.Top:
                newPosition = bounds.center + new Vector3(0, 0, bounds.extents.y);
                break;
            case CheckpointType.Bottom:
                newPosition = bounds.center - new Vector3(0, 0, bounds.extents.y);
                break;
            default:
                break;
        }
        point.transform.localPosition = newPosition;
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
}

[Serializable]
public class FurnitureData
{
    public string ItemID = "Item";
    public float Width = 1;
    public float Height = 1;
    public float ObjectHeight = 0.5f;
}

