using System;
using System.Collections.Generic;
using UnityEngine;

public enum CheckpointType
{
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
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
    private const float LIMIT_SIZE = 0.5f;

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

    [SerializeField] private FurniturePoint bottomLeftPoint;
    [SerializeField] private FurniturePoint bottomRightPoint;
    [SerializeField] private FurniturePoint topLeftPoint;
    [SerializeField] private FurniturePoint topRightPoint;


    private FurniturePoint[] pointsArray;
    public float width, height = 1;
    private void Awake()
    {
        bounds = new Bounds();
        bounds.center = spriteRender.transform.localPosition;
        bounds.size = new Vector3(width,1, height);
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        foreach(var item in GetComponentsInChildren<FurniturePoint>())
        {
            SetupPoint(item);
        }

        pointsArray = GetComponentsInChildren<FurniturePoint>();
    }


    private void SetupPoint(FurniturePoint point)
    {
        point.center = transform;
        //point.scaleHandle = value;
        point.furniture = this;
    }

    private Vector3 startPos;
    [SerializeField] private Bounds bounds;
    public void OnDragPoint(FurniturePoint point)
    {
        Vector3 newPos = GetWorldMousePosition();
        newPos = point.transform.parent.InverseTransformPoint(newPos);

        switch (point.checkpointType)
        {
            case CheckpointType.Left:
                ResizeWithAnchor(newPos, point, rightPoint.transform, ResizeAxis.X);
                break;
            case CheckpointType.Right:
                ResizeWithAnchor(newPos, point, leftPoint.transform, ResizeAxis.X);
                break;
            case CheckpointType.Top:
                ResizeWithAnchor(newPos, point, bottomPoint.transform, ResizeAxis.Z);
                break;
            case CheckpointType.Bottom:
                ResizeWithAnchor(newPos, point, topPoint.transform, ResizeAxis.Z);
                break;
            case CheckpointType.TopLeft:
                ResizeWithAnchor(newPos, point, bottomRightPoint.transform, ResizeAxis.XZ);
                break;
            case CheckpointType.TopRight:
                ResizeWithAnchor(newPos, point, bottomLeftPoint.transform, ResizeAxis.XZ);
                break;
            case CheckpointType.BottomLeft:
                ResizeWithAnchor(newPos, point, topRightPoint.transform, ResizeAxis.XZ);
                break;
            case CheckpointType.BottomRight:
                ResizeWithAnchor(newPos, point, topLeftPoint.transform, ResizeAxis.XZ);
                break;
            default:
                break;
        }

        width = bounds.size.x;
        height = bounds.size.z;
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
    private ClampHandler clamp = new();
    public void ResizeWithAnchor(Vector3 localPoint, FurniturePoint dragPoint, Transform anchorPoint, ResizeAxis resizeAxis)
    {

        float xPos = resizeAxis == ResizeAxis.XZ || resizeAxis == ResizeAxis.X
            ? localPoint.x : dragPoint.transform.localPosition.x;
        float zPos = resizeAxis == ResizeAxis.XZ || resizeAxis == ResizeAxis.Z
            ? localPoint.z : dragPoint.transform.localPosition.z;

        Vector3 dragPos = new Vector3(xPos, transform.position.y, zPos);

        dragPos = clamp.ClampPosition(dragPos, bounds.center, LIMIT_SIZE, dragPoint.checkpointType);

        Vector3 anchor = anchorPoint.localPosition;
        Vector3 center = (anchor + dragPos) / 2f;



        Vector3 size = bounds.size;

        switch (resizeAxis)
        {
            case ResizeAxis.X:
                size.x = Mathf.Abs(dragPos.x - anchor.x);
                break;
            case ResizeAxis.Z:
                size.z = Mathf.Abs(dragPos.z - anchor.z);
                break;
            case ResizeAxis.XZ:
                size.x = Mathf.Abs(dragPos.x - anchor.x);
                size.z = Mathf.Abs(dragPos.z - anchor.z);
                break;
            default:
                break;
        }
        bounds.center = center;
        bounds.size = size;

        //dragPoint.localPosition = newPosFiler;

    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    private void Recalculator(FurniturePoint point, Bounds bounds)
    {
        Vector3 newPosition = point.transform.localPosition;
        float xExtend = Mathf.Max(bounds.extents.x, LIMIT_SIZE);
        float yExtend = Mathf.Max(bounds.extents.z, LIMIT_SIZE);

        switch (point.checkpointType)
        {

            default:
                break;
        }
        Vector3 offset = Vector3.zero;
        var type = point.checkpointType;
        // left
        switch (type)
        {
            case CheckpointType.Left:
            case CheckpointType.TopLeft:
            case CheckpointType.BottomLeft:
                offset += new Vector3(-xExtend, 0, 0);
                break;

            default:
                break;
        }
        // right
        switch (type)
        {
            case CheckpointType.Right:
            case CheckpointType.TopRight:
            case CheckpointType.BottomRight:
                offset += new Vector3(xExtend, 0, 0);
                break;
            default:
                break;
        }
        // top
        switch (type)
        {
            case CheckpointType.Top:
            case CheckpointType.TopLeft:
            case CheckpointType.TopRight:
                offset += new Vector3(0, 0, yExtend);
                break;
            default:
                break;
        }
        // bottom
        switch (type)
        {
            case CheckpointType.Bottom:
            case CheckpointType.BottomLeft:
            case CheckpointType.BottomRight:
                offset += new Vector3(0, 0, -yExtend);
                break;
            default:
                break;
        }
        newPosition = bounds.center + offset;
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

