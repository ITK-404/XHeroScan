using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
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

    // public const float LIMIT_SIZE = 0.5f;
    public float minSizeX
    {
        get => data.size.widthMinMax.x / 2;
    }

    public float minSizeZ
    {
        get => data.size.lengthMinMax.x / 2;
    }

    [Header("Cấu hình để phân biệt cửa/cửa sổ và đồ nội thất")]
    [SerializeField] private bool allowSnapToWall = false; // có thể gắn vào tường
    [SerializeField] private bool allowShowAllCheckPoint = false; // hiển thị 1 phần check point (chỉ bật cho cửa, cửa sổ )
    [SerializeField] private bool allowRotationByCheckPoint = false; // bật point điều khiển rotation
    public bool allowEditWhenSnapToWall = false; // chỉ cho phép điểu chỉnh kích thước khi gắn vào tường
    public bool isUsingCenterPosToSnap = false; // khi biến này = false và allow snap to wall = true, furniture sẽ gắn vào tường bằng bottom anchor
    public bool alwayMakeSquare = false; // nếu kích hoạt thì hình dạng luôn tạo thành hình vuông
    public LineType lineType;
    [Header("References")]
    public DrawingInstanced data;

    public Transform modelContainer;
    public SpriteRenderer model2D;

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

    [Header("Prefabs")]
    [SerializeField] private LineRenderer lineRendererPrefab;

    [SerializeField] private TextMeshPro textMeshProPrefab;

    public FurnitureMergeToWall furnitureMergeToWall;

    private Quaternion currentRotation
    {
        get => data.size.rotation;
        set => data.size.rotation = value;
    }

    private FurnitureVisuals furnitureVisuals;
    private IUpdateWhenMove[] IUpdateWhenMoves;
    private FurniturePoint[] pointsArray;
    private Vector3 startPos;

    public float width
    {
        get => data.size.width;
        set => data.size.width = value;
    }

    public float length
    {
        get => data.size.length;
        set => data.size.length = value;
    }

    public float height
    {
        get => data.size.height;
        set => data.size.height = value;
    }

    private ObjectResizer resizer;

    private void Awake()
    {
        data.size.ClampSize();
        resizer = GetComponentInChildren<ObjectResizer>();
        resizer.Resize();

        furnitureVisuals = new FurnitureVisuals(this);
        furnitureMergeToWall = new FurnitureMergeToWall(this);

        bounds = new Bounds();
        bounds.center = modelContainer.transform.localPosition;
        bounds.size = new Vector3(width, 1, length);
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        pointsArray = GetComponentsInChildren<FurniturePoint>();
        foreach (var item in pointsArray)
        {
            item.furniture = this;
        }

        if (lineType == LineType.Window)
        {
            furnitureMergeToWall.SetupAnchor(CheckpointType.Left, CheckpointType.Right);
        }
        else
        {
            furnitureMergeToWall.SetupAnchor(CheckpointType.BottomLeft, CheckpointType.BottomRight);
        }

        DisableCheckPoint();
        RefreshCheckPointsByBounds();

        if (!allowShowAllCheckPoint)
        {
            topLeftPoint.gameObject.SetActive(false);
            topRightPoint.gameObject.SetActive(false);

            leftPoint.gameObject.SetActive(isUsingCenterPosToSnap);
            rightPoint.gameObject.SetActive(isUsingCenterPosToSnap);

            bottomLeftPoint.gameObject.SetActive(!isUsingCenterPosToSnap);
            bottomRightPoint.gameObject.SetActive(!isUsingCenterPosToSnap);

            topPoint.gameObject.SetActive(false);
            bottomPoint.gameObject.SetActive(false);
        }

        rotatePoint.gameObject.SetActive(allowRotationByCheckPoint);
    }


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

    public void DragPoint(FurniturePoint currentDragPoint)
    {
        Vector3 newPos = GetWorldMousePosition();
        newPos = currentDragPoint.transform.parent.InverseTransformPoint(newPos);

        RefreshCheckPointsByBounds();

        switch (currentDragPoint.checkpointType)
        {
            case CheckpointType.Left:
                ResizeWithAnchor(newPos, currentDragPoint, rightPoint.transform);
                break;
            case CheckpointType.Right:
                ResizeWithAnchor(newPos, currentDragPoint, leftPoint.transform);
                break;
            case CheckpointType.Top:
                ResizeWithAnchor(newPos, currentDragPoint, bottomPoint.transform);
                break;
            case CheckpointType.Bottom:
                ResizeWithAnchor(newPos, currentDragPoint, topPoint.transform);
                break;
            case CheckpointType.TopLeft:
                ResizeWithAnchor(newPos, currentDragPoint, bottomRightPoint.transform);
                break;
            case CheckpointType.TopRight:
                ResizeWithAnchor(newPos, currentDragPoint, bottomLeftPoint.transform);
                break;
            case CheckpointType.BottomLeft:
                ResizeWithAnchor(newPos, currentDragPoint, topRightPoint.transform);
                break;
            case CheckpointType.BottomRight:
                ResizeWithAnchor(newPos, currentDragPoint, topLeftPoint.transform);
                break;
            default:
                break;
        }

        MakeDirty();
    }

    public void RefreshCheckPointsByBounds()
    {
        // tính toán vị trí của check point dựa theo bound
        foreach (var item in pointsArray)
        {
            furnitureVisuals.Recalculator(item.transform, item.checkpointType, bounds, new Vector3(0, 0.1f, 0));
        }

        // Cập nhật point dùng để xoay object 
        float z = bounds.size.y * 3 * FurnitureManager.Instance.ScaleByCameraZoom.Offset;
        z = Mathf.Clamp(z, 0.25f, float.MaxValue);
        Vector3 offset = new Vector3(0, 0.1f, -z);
        furnitureVisuals.Recalculator(rotatePoint.transform, CheckpointType.Bottom, bounds, offset);

        // update line
        if (IUpdateWhenMoves == null) return;
        foreach (var item in IUpdateWhenMoves)
        {
            item.Update();
        }
    }

    private void Update()
    {
        // giới hạn dựa theo data
        width = Mathf.Clamp(width, minSizeX / 2, 100);
        length = Mathf.Clamp(length, minSizeZ / 2, 100);

        // scale sprite
        modelContainer.transform.localScale = new Vector3(width, length, 1 * length * 0.5f);
        data.worldPosition = modelContainer.transform.position;

        // using for update by zoom in or zoom out
        if (IUpdateWhenMoves == null) return;
        foreach (var item in IUpdateWhenMoves)
        {
            item.UpdateWhenCameraZoom();
        }

        if (allowEditWhenSnapToWall)
        {
            furnitureMergeToWall.Update();
        }
    }
    /// <summary>
    /// Hàm này được gọi khi người dùng muốn điều chỉnh kích thước bằng tay
    /// Tác dụng của hàm là sẽ khiến cho point được kéo được clamp lại theo trục quy định sẵn
    /// Vd: X thì chỉ kéo ngang, Z thì có thể kéo đọc, XZ thì có thể tác động cả 2
    /// </summary>
    /// <param name="localPoint"></param>
    /// <param name="dragPoint"></param>
    /// <param name="anchorPoint"></param>
    public void ResizeWithAnchor(Vector3 localPoint, FurniturePoint dragPoint, Transform anchorPoint)
    {
        // rotation hiện tại (dùng currentRotation của bạn)
        ResizeAxis resizeAxis = dragPoint.GetReSizeAxis();
        Quaternion rotation = Quaternion.Euler(0f, currentRotation.y, 0f);
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
            sizeLocal, resizeAxis, dragLocalUnrot, anchorLocalUnrot, alwayMakeSquare);

        // --- Chuyển center trở về không gian local (có xoay) và cập nhật bounds ---
        bounds.center = originalCenter + rotation * centerLocalUnrot;
        bounds.size = sizeLocal;

        // cập nhật width/height nếu dùng chúng trực tiếp
        UpdateWorldSizeFromLocal();

        // Sau khi resize xong, cập nhật hiển thị / điểm:
        modelContainer.transform.localPosition = bounds.center;
        SetRotation(currentRotation.y);
    }


    /// <summary>
    /// Gọi method này khi thực hiện công việc liên quan tới thay đổi kích thước
    /// </summary>
    private void UpdateWorldSizeFromLocal()
    {
        // rotation in degrees around Y
        float angleDeg = currentRotation.y;
        float rad = angleDeg * Mathf.Deg2Rad;

        float c = Mathf.Abs(Mathf.Cos(rad));
        float s = Mathf.Abs(Mathf.Sin(rad));

        // nếu localWidth/localHeight là toàn bộ size (không half extents)
        float lx = bounds.size.x; // local width (X)
        float lz = bounds.size.z; // local height (Z)

        width = bounds.size.x;
        length = bounds.size.z;
    }

    /// <summary>
    /// Kéo furniture theo delta của mouse, không dựa vào vị trí của mouse
    /// </summary>
    /// <param name="dragTransform"></param>
    public void Dragging(Transform dragTransform)
    {
        var currentPos = GetWorldMousePosition();
        var delta = currentPos - startPos;

        dragTransform.localPosition += delta;

        if (allowSnapToWall)
        {
            furnitureMergeToWall.StartSnap();
        }

        startPos = currentPos;
        bounds.center = dragTransform.localPosition;

        RefreshCheckPointsByBounds();
        UpdateWorldSizeFromLocal();
        MakeDirty();

        OnDragPoint = true;
    }

    /// <summary>
    /// Gọi khi drag kết thúc
    /// </summary>
    public void DeActiveDrag()
    {
        furnitureMergeToWall.EndSnap();
        OnDragPoint = false;
    }

    /// <summary>
    /// Lấy vị trí chuột ở world 
    /// </summary>
    /// <returns></returns>
    private Vector3 GetWorldMousePosition()
    {
        float distance = Vector3.Distance(mainCam.transform.position, FurnitureManager.Instance.transform.position);

        // Chuyển vị trí chuột sang tọa độ thế giới
        Vector3 worldMousePosition = mainCam.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, distance)
        );
        return worldMousePosition;
    }

    /// <summary>
    /// Gọi khi bắt đầu drag
    /// </summary>
    public void StartDrag()
    {
        startPos = GetWorldMousePosition();
    }

    /// <summary>
    /// Xoay furniture dựa theo góc của center tới chuột, tích hợp snap bên trong
    /// </summary>
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

        float yRotation = (angleDeg + 180f) % 360f;
        SetRotation(yRotation);
        // cập nhật point/size nếu cần
        RefreshCheckPointsByBounds();

        UpdateWorldSizeFromLocal(); // nếu bạn đang dùng

        MakeDirty();
    }

    public void DisableCheckPoint()
    {
        checkPointParent.gameObject.SetActive(false);
    }

    public void EnableCheckPoint()
    {
        bool canShowCheckPoint = (allowEditWhenSnapToWall && furnitureMergeToWall.IsInWall()) || !allowEditWhenSnapToWall;
        Debug.Log("Can show check point: " + canShowCheckPoint);
        if (canShowCheckPoint)
        {
            checkPointParent.gameObject.SetActive(true);
        }
    }

    public Vector3 GetWorldPosition()
    {
        return modelContainer.transform.position;
    }

    /// <summary>
    /// Cập nhật world position từ bên ngoài object
    /// </summary>
    /// <param name="worldPosition"></param>
    public void SetWorldPosition(Vector3 worldPosition)
    {
        modelContainer.transform.position = worldPosition;
        bounds.center = modelContainer.transform.localPosition;
    }

    /// <summary>
    /// Nhận data từ bên ngoài
    /// </summary>
    /// <param name="furnitureData"></param>
    public void FetchData(DrawingInstanced furnitureData)
    {
        data = furnitureData;

        // Cập nhật các thuộc tính từ dữ liệu

        // Cập nhật vị trí và kích thước của sprite
        data.size.ClampSize();
        // set from data
        SetWorldPosition(data.worldPosition);
        modelContainer.transform.localScale = new Vector3(width, length, 1 * length * 0.5f);

        SetRotation(currentRotation.y);
        // Cập nhật bounds
        bounds.center = modelContainer.transform.localPosition;
        bounds.size = new Vector3(width, 1, length);
        // cập nhật lại rotation và position theo check point
        RefreshCheckPointsByBounds();
    }

    /// <summary>
    /// Lấy các point dựa trên type
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public FurniturePoint GetFurniturePoint(CheckpointType type)
    {
        if (pointsArray == null) return null;
        foreach (var item in pointsArray)
        {
            if (item.checkpointType == type)
            {
                return item;
            }
        }

        return null;
    }

    public void SetRotation(float yRotation)
    {
        modelContainer.transform.localRotation = Quaternion.Euler(90, yRotation, 0);
        data.size.rotation.y = yRotation;
    }

    private void MakeDirty()
    {
        SaveLoadManager.MakeDirty();
    }

    /// <summary>
    /// Di chuyển furniture theo point nhưng vẫn giữa nguyên hình dạng
    /// </summary>
    /// <param name="type"></param>
    /// <param name="worldPosition"></param>
    public void MoveAnchorToPositionWithoutChangeShape(CheckpointType type, Vector3 worldPosition)
    {


        var targetAnchor = GetFurniturePoint(type);
        var furnitureWorldPosition = GetWorldPosition();
        Vector3 anchorWorldPos = targetAnchor.transform.position;

        // Calculate the offset from the object's center to the anchor in world space
        Vector3 centerToAnchorOffset = anchorWorldPos - furnitureWorldPosition;

        // The new center should be the target world position minus the offset
        Vector3 newCenterWorld = worldPosition - centerToAnchorOffset;

        // Convert the new center to local space relative to the parent
        Vector3 newCenterLocal = transform.InverseTransformPoint(newCenterWorld);

        var localPosition = newCenterLocal;
        var debugPoint = FurnitureManager.Instance.debugPoint;
        localPosition.y = modelContainer.transform.localPosition.y;

        if (isUsingCenterPosToSnap)
        {
            localPosition = transform.InverseTransformPoint(worldPosition);
        }

        // debugPoint.transform.SetParent(modelContainer.transform.parent);
        // debugPoint.transform.localPosition = actualPosition;

        bounds.center = localPosition;
        modelContainer.transform.localPosition = localPosition;

        RefreshCheckPointsByBounds();
        UpdateWorldSizeFromLocal();
    }

    public float GetHeightOffset()
    {
        return model2D.bounds.size.z;
    }

    public void SyncWithBounds()
    {
        var size = bounds.size;
        size.x = width;
        size.z = length;
        bounds.size = size;
    }

    public void Destroy()
    {
        Debug.Log("Destroy furniture");
        furnitureMergeToWall.TryRemoveWallLine();
        FurnitureManager.Instance.RemoveFromRuntime(this);
        FurnitureManager.Instance.SelectFurniture(null);
        Destroy(gameObject);
    }
}