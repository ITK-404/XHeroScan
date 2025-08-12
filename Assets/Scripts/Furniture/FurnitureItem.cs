using System;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
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
    public static bool OnDragFurniture = false;
    public static bool OnDragPoint = false;
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

    [SerializeField] private FurnitureRotate rotatePoint;
    [SerializeField] private Bounds bounds;
    private FurniturePoint[] pointsArray;
    private Vector3 startPos;
    public float width, height = 1;

    [SerializeField] private float currentRotation;
    private void Awake()
    {
        bounds = new Bounds();
        bounds.center = spriteRender.transform.localPosition;
        bounds.size = new Vector3(width, 1, height);
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        foreach (var item in GetComponentsInChildren<FurniturePoint>())
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

    public void DragPoint(FurniturePoint point)
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
        RefreshCheckPoints();
    }

    public void RefreshCheckPoints()
    {
        foreach (var item in pointsArray)
        {
            Recalculator(item.transform, item.checkpointType, bounds, Vector3.zero);
        }
        Recalculator(rotatePoint.transform, CheckpointType.Bottom, bounds, new Vector3(0, 0, -1));
    }

    private void Update()
    {
        width = Mathf.Clamp(width, 0.1f, 100);
        height = Mathf.Clamp(height, 0.1f, 100);
        spriteRender.transform.localScale = new Vector3(width, height, 1 * height * 0.5f);
    }

    public void ResizeWithAnchor(Vector3 localPoint, FurniturePoint dragPoint, Transform anchorPoint, ResizeAxis resizeAxis)
    {
        // rotation hiện tại (dùng currentRotation của bạn)
        Quaternion rotation = Quaternion.Euler(0f, currentRotation, 0f);
        Vector3 originalCenter = bounds.center;
        // Chuyển vị trí drag và anchor về "local chưa xoay" (unrotated local space)
        Vector3 dragLocalUnrot = Quaternion.Inverse(rotation) * (localPoint - originalCenter);
        Vector3 anchorLocalUnrot = Quaternion.Inverse(rotation) * (anchorPoint.localPosition - originalCenter);

        // cập nhật dragLocalUnrot theo ý định (nếu bạn muốn lock trục, thay bằng anchor value)
        if (resizeAxis == ResizeAxis.Z) // chỉ scale Z -> giữ x bằng anchor.x
            dragLocalUnrot.x = anchorLocalUnrot.x;
        if (resizeAxis == ResizeAxis.X) // chỉ scale X -> giữ z bằng anchor.z
            dragLocalUnrot.z = anchorLocalUnrot.z;

        // --- Clamp trong không gian unrotated (giữ nguyên logic theo checkpoint type) ---
        var type = dragPoint.checkpointType;

        // Left
        if (type == CheckpointType.Left || type == CheckpointType.TopLeft || type == CheckpointType.BottomLeft)
        {
            if (dragLocalUnrot.x > -LIMIT_SIZE) dragLocalUnrot.x = -LIMIT_SIZE;
        }

        // Right
        if (type == CheckpointType.Right || type == CheckpointType.TopRight || type == CheckpointType.BottomRight)
        {
            if (dragLocalUnrot.x < LIMIT_SIZE) dragLocalUnrot.x = LIMIT_SIZE;
        }

        // Top (positive Z in unrotated local)
        if (type == CheckpointType.Top || type == CheckpointType.TopLeft || type == CheckpointType.TopRight)
        {
            if (dragLocalUnrot.z < LIMIT_SIZE) dragLocalUnrot.z = LIMIT_SIZE;
        }

        // Bottom (negative Z in unrotated local)
        if (type == CheckpointType.Bottom || type == CheckpointType.BottomLeft || type == CheckpointType.BottomRight)
        {
            if (dragLocalUnrot.z > -LIMIT_SIZE) dragLocalUnrot.z = -LIMIT_SIZE;
        }

        // --- Tính center và size trong không gian unrotated ---
        Vector3 centerLocalUnrot = (anchorLocalUnrot + dragLocalUnrot) / 2f;
        Vector3 sizeLocal = bounds.size; // giữ cấu trúc: size.x -> width, size.z -> height

        switch (resizeAxis)
        {
            case ResizeAxis.X:
                sizeLocal.x = Mathf.Abs(dragLocalUnrot.x - anchorLocalUnrot.x);
                break;
            case ResizeAxis.Z:
                sizeLocal.z = Mathf.Abs(dragLocalUnrot.z - anchorLocalUnrot.z);
                break;
            case ResizeAxis.XZ:
                sizeLocal.x = Mathf.Abs(dragLocalUnrot.x - anchorLocalUnrot.x);
                sizeLocal.z = Mathf.Abs(dragLocalUnrot.z - anchorLocalUnrot.z);
                break;
        }

        // --- Chuyển center trở về không gian local (có xoay) và cập nhật bounds ---
        bounds.center = originalCenter + rotation * centerLocalUnrot;
        bounds.size = sizeLocal;

        // cập nhật width/height nếu dùng chúng trực tiếp
        UpdateWorldSizeFromLocal();

        // Sau khi resize xong, cập nhật hiển thị / điểm:
        spriteRender.transform.localPosition = bounds.center;
        spriteRender.transform.localRotation = Quaternion.Euler(90, currentRotation, 0);

    }

    private void UpdateWorldSizeFromLocal()
    {
        // rotation in degrees around Y
        float angleDeg = currentRotation;
        float rad = angleDeg * Mathf.Deg2Rad;

        float c = Mathf.Abs(Mathf.Cos(rad));
        float s = Mathf.Abs(Mathf.Sin(rad));

        // nếu localWidth/localHeight là toàn bộ size (không half extents)
        float lx = bounds.size.x; // local width (X)
        float lz = bounds.size.z; // local height (Z)

        // AABB trên world X/Z
        //worldWidth = c * lx + s * lz;   // full size along world X
        //worldHeight = s * lx + c * lz;   // full size along world Z

        // cho tiện, cũng cập nhật public width/height nếu bạn dùng 2 biến đó hiển thị
        width = bounds.size.x;
        height = bounds.size.z;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    private void Recalculator(Transform point, CheckpointType type, Bounds bounds, Vector3 offset)
    {
        Vector3 newPosition = point.transform.localPosition;
        float xExtend = Mathf.Max(bounds.extents.x, LIMIT_SIZE);
        float yExtend = Mathf.Max(bounds.extents.z, LIMIT_SIZE);

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
        offset = Quaternion.Euler(0, currentRotation, 0) * offset;
        newPosition = bounds.center + offset;
        point.transform.localPosition = newPosition;
    }

    public void Dragging(Transform dragTransform)
    {
        var currentPos = GetWorldMousePosition();
        var delta = currentPos - startPos;
        dragTransform.localPosition += delta;
        startPos = currentPos;
        bounds.center = dragTransform.localPosition;

        RefreshCheckPoints();
        UpdateWorldSizeFromLocal();
        OnDragPoint = true;
    }

    public void DeActiveDrag()
    {
        OnDragPoint = false;
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

    public void StartDrag()
    {
        startPos = GetWorldMousePosition();
    }

    public void RotateToMouse()
    {
        Vector3 mouseWorld = GetWorldMousePosition();

        // Nếu bounds.center được lưu là local position relative tới THIS transform:
        Vector3 centerWorld = transform.TransformPoint(bounds.center);

        // Nếu bounds.center đã là world position thì dùng:
        // Vector3 centerWorld = bounds.center;

        Vector3 dir = mouseWorld - centerWorld;
        dir.y = 0f; // bỏ cao độ

        if (dir.sqrMagnitude < 1e-6f) return; // tránh chia 0 / LookRotation lỗi

        // Cách 1 — trực tiếp với Atan2: trả về angle (deg) với 0 = +Z (forward)
        float angleDeg = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        // chuẩn hoá góc vào [0,360)
        angleDeg = (angleDeg % 360f + 360f) % 360f;

        currentRotation = angleDeg;
        spriteRender.transform.localRotation = Quaternion.Euler(90f, currentRotation, 0f);

        // cập nhật point/size nếu cần
        RefreshCheckPoints();
        UpdateWorldSizeFromLocal(); // nếu bạn đang dùng
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

