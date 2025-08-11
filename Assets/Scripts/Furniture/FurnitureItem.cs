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
    [SerializeField] private Transform pointParent;

    [SerializeField] private FurniturePoint leftPoint;
    [SerializeField] private FurniturePoint rightPoint;
    [SerializeField] private FurniturePoint topPoint;
    [SerializeField] private FurniturePoint bottomPoint;

    [SerializeField] private FurniturePoint bottomLeftPoint;
    [SerializeField] private FurniturePoint bottomRightPoint;
    [SerializeField] private FurniturePoint topLeftPoint;
    [SerializeField] private FurniturePoint topRightPoint;


    [SerializeField] private FurniturePoint[] pointsArray;
    public float width, height = 1;
    private void Awake()
    {
        pointParent.gameObject.SetActive(false);

        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        foreach (var item in pointsArray)
        {
            SetupPoint(item);
        }
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


        foreach (var item in pointsArray)
        {
            Recalculator(item);
        }
    }


    private void Update()
    {
        width = Mathf.Clamp(width, 0.1f, 100);
        height = Mathf.Clamp(height, 0.1f, 100);
        transform.localScale = new Vector3(width, height, 1);
    }
    private ClampHandler clamp = new();
    public void ResizeWithAnchor(Vector3 localPoint, FurniturePoint dragPoint, Transform anchorPoint, ResizeAxis resizeAxis)
    {

        float xPos = resizeAxis == ResizeAxis.XZ || resizeAxis == ResizeAxis.X
            ? localPoint.x : dragPoint.transform.position.x;
        float zPos = resizeAxis == ResizeAxis.XZ || resizeAxis == ResizeAxis.Z
            ? localPoint.z : dragPoint.transform.position.z;

        Vector3 dragPos = new Vector3(xPos, transform.position.y, zPos);

        dragPos = clamp.ClampPosition(dragPos, transform.position, LIMIT_SIZE, dragPoint.checkpointType);

        Vector3 anchor = anchorPoint.transform.position;
        Vector3 center = (anchor + dragPos) / 2f;


        Vector3 size = new Vector2(width, height);

        switch (resizeAxis)
        {
            case ResizeAxis.X:
                size.x = Mathf.Abs(dragPos.x - anchor.x);
                break;
            case ResizeAxis.Z:
                size.y = Mathf.Abs(dragPos.z - anchor.z);
                break;
            case ResizeAxis.XZ:
                size.x = Mathf.Abs(dragPos.x - anchor.x);
                size.y = Mathf.Abs(dragPos.z - anchor.z);
                break;
            default:
                break;
        }

        width = size.x;
        height = size.y;
        transform.position = center;
        //dragPoint.localPosition = newPosFiler;

    }

    private void Recalculator(FurniturePoint point)
    {
        Vector3 newPosition = point.transform.position;
        float xExtend = Mathf.Max(width / 2, LIMIT_SIZE);
        float yExtend = Mathf.Max(height / 2, LIMIT_SIZE);

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
        newPosition = transform.position + offset;
        point.transform.position = newPosition;
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

    public void OnDrop()
    {
        pointParent.gameObject.SetActive(true);
        pointParent.transform.parent = null;
        pointParent.transform.position = Vector3.zero;

        foreach (var item in pointsArray)
        {
            Recalculator(item);
        }
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

