using System;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;

public enum ResizeAxis
{
    X,
    Z,
    XZ
}

public enum FurnitureState
{
    Select,
    UnSelect
}

public partial class FurnitureItem : MonoBehaviour
{
    public static bool OnDragFurniture = false;
    public static bool OnDragPoint = false;

    private static Camera mainCam;
    public const float LIMIT_SIZE = 0.5f;
    public float minSizeX = 0.1f;
    public float minSizeZ = 0.1f;

    [Header("References")]
    public FurnitureData data;

    public SpriteRenderer spriteRender;

    [Header("Point")]
    [SerializeField] private GameObject checkPointParent;

    [SerializeField] private FurniturePoint leftPoint;
    [SerializeField] private FurniturePoint rightPoint;
    [SerializeField] private FurniturePoint topPoint;
    [SerializeField] private FurniturePoint bottomPoint;

    [SerializeField] private FurniturePoint bottomLeftPoint;
    [SerializeField] private FurniturePoint bottomRightPoint;
    [SerializeField] private FurniturePoint topLeftPoint;
    [SerializeField] private FurniturePoint topRightPoint;

    [SerializeField] private FurnitureRotate rotatePoint;

    [Header("Bounds")]
    [SerializeField] private Bounds bounds;

    private float currentRotation
    {
        get => data.rotation;
        set => data.rotation = value;
    }

    private FurnitureVisuals furnitureVisuals;
    
    private FurniturePoint[] pointsArray;
    private Vector3 startPos;
    private DrawingTool drawingTool;

    public float width
    {
        get => data.Width;
        set => data.Width = value;
    }

    public float height
    {
        get => data.Height;
        set => data.Height = value;
    }



    private void Awake()
    {
        furnitureVisuals = new FurnitureVisuals(this);
        
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

        DisableCheckPoint();
    }
    
    private void Start()
    {
        drawingTool = DrawingTool.Instance;
    }


    private IUpdateWhenMove[] IUpdateWhenMoves;

    public void InitLineAndText()
    {
        var topLine = new Outline(CreateLineRenderer(),
            topLeftPoint.gameObject,
            topRightPoint.gameObject);
        var rightLine = new Outline(CreateLineRenderer(),
            topRightPoint.gameObject,
            bottomRightPoint.gameObject);
        var leftLine = new Outline(CreateLineRenderer(),
            topLeftPoint.gameObject,
            bottomLeftPoint.gameObject);
        var bottomLine = new Outline(CreateLineRenderer(),
            bottomLeftPoint.gameObject,
            bottomRightPoint.gameObject);

        var topTextDistance = new TextDistance(CreateTextMeshPro(), topLine);
        var rightTextDistance = new TextDistance(CreateTextMeshPro(), rightLine);

        IUpdateWhenMoves = new IUpdateWhenMove[]
            { topLine, leftLine, rightLine, bottomLine, topTextDistance, rightTextDistance };
    }

    [SerializeField] private LineRenderer lineRendererPrefab;
    [SerializeField] private TextMeshPro textMeshProPrefab;

    private LineRenderer CreateLineRenderer()
    {
        var line = Instantiate(lineRendererPrefab, checkPointParent.transform);
        DrawingTool.Instance.SetupLine(line);
        return line;
    }

    private TextMeshPro CreateTextMeshPro()
    {
        var text = Instantiate(textMeshProPrefab, transform);
        return text;
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
        
        MakeDirty();
    }

    public void RefreshCheckPoints()
    {
        // update all check point position
        foreach (var item in pointsArray)
        {
            furnitureVisuals.Recalculator(item.transform, item.checkpointType, bounds, new Vector3(0, 0.1f, 0));
        }

        // update rotate point
        float z = bounds.size.y * 3 * FurnitureManager.Instance.ScaleByCameraZoom.Offset;
        z = Mathf.Clamp(z, 0.25f, float.MaxValue);
        Vector3 offset = new Vector3(0, 0.1f, -z);

        furnitureVisuals.Recalculator(rotatePoint.transform, CheckpointType.Bottom, bounds, offset);

        if (IUpdateWhenMoves == null) return;
        // update line
        foreach (var item in IUpdateWhenMoves)
        {
            item.Update();
        }
    }

    private void Update()
    {
        // limit
        width = Mathf.Clamp(width, minSizeX, 100);
        height = Mathf.Clamp(height, minSizeZ, 100);

        // scale sprite
        spriteRender.transform.localScale = new Vector3(width, height, 1 * height * 0.5f);

        // using for update by zoom in or zoom out
        if (IUpdateWhenMoves == null) return;
        foreach (var item in IUpdateWhenMoves)
        {
            item.UpdateWhenCameraZoom();
        }

        if (data != null)
        {
            data.worldPosition = spriteRender.transform.position;
        }
    }

    public void ResizeWithAnchor(Vector3 localPoint, FurniturePoint dragPoint, Transform anchorPoint,
        ResizeAxis resizeAxis)
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
        CheckpointType type = dragPoint.checkpointType;

        dragLocalUnrot = furnitureVisuals.ClampPointToBounds(
            dragLocalUnrot, type);

        // --- Tính center và size trong không gian unrotated ---
        Vector3 centerLocalUnrot = (anchorLocalUnrot + dragLocalUnrot) / 2f;
        Vector3 sizeLocal = bounds.size; // giữ cấu trúc: size.x -> width, size.z -> height

        sizeLocal = furnitureVisuals.ClampSizeToBounds(
            sizeLocal, resizeAxis, dragLocalUnrot, anchorLocalUnrot);

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
        
        MakeDirty();
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
        Debug.Log("Angle in degrees: " + angleDeg);
        // chuẩn hoá góc vào [0,360)
        angleDeg = (angleDeg % 360f + 360f) % 360f;

        angleDeg = FurnitureManager.Instance.CheckSnapRotation(angleDeg);
    
        currentRotation = (angleDeg + 180f) % 360f;

        spriteRender.transform.localRotation = Quaternion.Euler(90f, currentRotation, 0f);

        // cập nhật point/size nếu cần
        RefreshCheckPoints();
        UpdateWorldSizeFromLocal(); // nếu bạn đang dùng

        MakeDirty();

    }

    public void DisableCheckPoint()
    {
        checkPointParent.gameObject.SetActive(false);
    }

    public void EnableCheckPoint()
    {
        checkPointParent.gameObject.SetActive(true);
    }

    public Vector3 GetWorldPosition()
    {
        return spriteRender.transform.position;
    }

    public void SetWorldPosition(Vector3 worldPosition)
    {
        spriteRender.transform.position = worldPosition;
    }

    public void FetchData(FurnitureData furnitureData)
    {
        data = furnitureData;

        // Cập nhật các thuộc tính từ dữ liệu
        width = data.Width;
        height = data.Height;
        currentRotation = data.rotation;

        // Cập nhật vị trí và kích thước của sprite
        spriteRender.transform.position = data.worldPosition;
        spriteRender.transform.localScale = new Vector3(width, height, 1 * height * 0.5f);

        // Cập nhật bounds
        bounds.center = spriteRender.transform.localPosition;
        bounds.size = new Vector3(width, 1, height);

        RefreshCheckPoints();
    }

    private void MakeDirty()
    {
        SaveLoadManager.MakeDirty();
    }
}

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

