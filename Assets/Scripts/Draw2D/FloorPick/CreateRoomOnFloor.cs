using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CreateRoomOnFloor : MonoBehaviour
{
    [Header("UI")]
    public Button CreateRoomButton;      // Nút bật/tắt chế độ tạo phòng

    [Header("Raycast")]
    public LayerMask floorMask = ~0;     // Layer của các Floor_<ID> có MeshCollider (và/hoặc con của chúng)

    [Header("Preview Style")]
    public float lineWidth = 0.02f;
    public Color previewOKLine = new Color(0.2f, 1f, 0.2f, 1f);
    public Color previewBadLine = new Color(1f, 0.2f, 0.2f, 1f);
    public Color previewOKFill = new Color(0.2f, 1f, 0.2f, 0.15f);
    public Color previewBadFill = new Color(1f, 0.2f, 0.2f, 0.2f);

    // >>> NEW: Layering để room luôn nổi trên floor
    [Header("Render Layering")]
    public int roomIndex = 2;           // phòng ở index = 2 (floor = 1, item = 99...)
    public float layerStepY = 0.002f;   // mỗi index cách nhau 2mm
    public float roomWallLift = 0.003f; // line/marker của room nhô thêm để tránh z-fighting

    private CheckpointManager checkPointManager;

    // Trạng thái
    private bool placingActive = false;

    // Kéo-để-tạo (drag)
    private bool isDragging = false;
    private Vector3 dragStartWorld;             // P1 (khi MouseDown)
    private Transform dragFloorRoot;            // RoomFloor root tại P1
    private Collider dragFloorCol;             // collider tại P1 (thông tin phụ)

    // Marker P1
    private GameObject firstMarker = null;

    // ===== Preview objects =====
    private GameObject previewRootGO;
    private LineRenderer previewLine;
    private GameObject previewFillGO;
    private MeshFilter previewFillMF;
    private MeshRenderer previewFillMR;
    private Mesh previewFillMesh;
    private Material previewLineMat;
    private Material previewFillMat;

    public static bool IsCreateRooom = false;

// ===== Dimension config (preview thước đo) =====
[Header("Dimension Preview")]
[Tooltip("Tỉ lệ độ dày line so với line viền")]
public float dimLineMul = 0.6f;
[Tooltip("Độ lệch thước so với cạnh (m)")]
public float dimOffset = 0.10f;
[Tooltip("Độ dài cánh mũi tên (m)")]
public float dimHeadSize = 0.10f;
[Tooltip("Font size cho TextMesh")]
public int dimFontSize = 100;
[Tooltip("Kích cỡ ký tự trong world")]
public float dimCharSize = 0.03f;

// Reusable dimension elements
private LineRenderer lenLR, lenHeadL, lenHeadR; // top (length)
private LineRenderer widLR, widHeadB, widHeadT; // left (width)
private TextMesh     lenText, widText;

    void Start()
    {
        checkPointManager = FindFirstObjectByType<CheckpointManager>();
        if (CreateRoomButton != null)
            CreateRoomButton.onClick.AddListener(TogglePlacingMode);

        // Tạo material mặc định cho preview
        var unlit = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        previewLineMat = new Material(unlit);
        previewFillMat = new Material(unlit);
    }

    void OnDestroy()
    {
        if (CreateRoomButton != null)
            CreateRoomButton.onClick.RemoveListener(TogglePlacingMode);
        DestroyPreviewImmediate();
        if (firstMarker) Destroy(firstMarker);

        IsCreateRooom = false;
    }

    void Update()
    {
        if (!placingActive) return;

        // Bỏ qua khi click lên UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Bắt đầu kéo (đặt P1) khi nhấn chuột
        if (Input.GetMouseButtonDown(0))
        {
            if (TryGetMouseWorldOnFloor(out var pos, out var col, out var root))
            {
                // Trúng floor: clamp nhẹ P1 cho chắc (nằm trong/biên)
                float yPlane = pos.y;
                var p1Clamped = ClampPointToFloor(pos, root, yPlane);

                isDragging = true;
                IsCreateRooom = true;

                dragStartWorld = p1Clamped;
                dragFloorRoot = root;
                dragFloorCol = col;

                if (checkPointManager != null && checkPointManager.checkpointPrefab != null)
                {
                    firstMarker = Instantiate(checkPointManager.checkpointPrefab, p1Clamped + Vector3.up * 0.01f, Quaternion.identity);
                    firstMarker.name = "RoomP1_Preview";
                }

                UpdatePreview(dragStartWorld, pos, dragFloorRoot); // p2 sẽ được clamp trong UpdatePreview
            }
            else if (TryGetMouseOnAnyFloorClamped(out var p1Snap, out var root2))
            {
                // KHÔNG trúng collider floor, nhưng vẫn khởi tạo kéo: coi như P1 nằm trên mép/biên floor gần nhất
                isDragging = true;
                IsCreateRooom = true;

                dragStartWorld = p1Snap;
                dragFloorRoot = root2;
                dragFloorCol = root2.GetComponent<Collider>() ?? root2.GetComponentInChildren<Collider>();

                if (checkPointManager != null && checkPointManager.checkpointPrefab != null)
                {
                    firstMarker = Instantiate(checkPointManager.checkpointPrefab, p1Snap + Vector3.up * 0.01f, Quaternion.identity);
                    firstMarker.name = "RoomP1_Preview";
                }

                // Khởi tạo preview với p2 = p1Snap (ngay tại mép); khi kéo tiếp, code MouseDrag đã xử lý clamp
                UpdatePreview(dragStartWorld, p1Snap, dragFloorRoot);
            }
            else
            {
                // Không có floor nào trong scene/tag → không khởi tạo
                isDragging = false;
                IsCreateRooom = false;
            }
        }

        // Đang kéo: cập nhật preview theo vị trí chuột hiện tại
        if (isDragging && Input.GetMouseButton(0))
        {
            if (TryGetMouseWorldOnFloor(out var pos, out var col, out var root) && root == dragFloorRoot)
            {
                UpdatePreview(dragStartWorld, pos, dragFloorRoot);
            }
            else
            {
                // Rời khỏi floor hoặc qua floor khác → ẩn/đỏ preview
                HidePreview();
            }
        }

        // Thả chuột: kết thúc kéo, nếu hợp lệ thì tạo room
        if (isDragging && Input.GetMouseButtonUp(0))
        {
            if (TryGetMouseWorldOnFloor(out var pos, out var col, out var root) && root == dragFloorRoot)
            {
                CommitRoom(dragStartWorld, pos, dragFloorRoot);
            }
            else
            {
                Debug.LogError("[CreateRoom] Thả chuột nhưng KHÔNG còn ở cùng RoomFloor → huỷ.");
                ResetDragState(keepPlacing: true);
            }
            IsCreateRooom = false;
        }
    }

    // ===== Toggle =====
    private void TogglePlacingMode()
    {
        placingActive = !placingActive;
        if (!placingActive)
        {
            ResetDragState(keepPlacing: false);
            IsCreateRooom = false;
        }

        if (CreateRoomButton != null)
        {
            var colors = CreateRoomButton.colors;
            colors.normalColor = placingActive ? new Color(0.8f, 1f, 0.8f) : Color.white;
            CreateRoomButton.colors = colors;
        }
    }

    // ===== Raycast chuột -> world pos + collider + RoomFloor root =====
    private bool TryGetMouseWorldOnFloor(out Vector3 pos, out Collider floorCol, out Transform floorRoot)
    {
        pos = default;
        floorCol = null;
        floorRoot = null;

        var cam = Camera.main;
        if (cam == null) return false;

        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 5000f, floorMask))
            return false;

        // leo lên parent để tìm object có tag "RoomFloor"
        Transform t = hit.collider ? hit.collider.transform : null;
        while (t != null && !t.CompareTag("RoomFloor")) t = t.parent;
        if (t == null) return false;

        floorRoot = t;
        // ưu tiên collider ngay trên root, nếu không có thì dùng collider vừa hit
        floorCol = t.GetComponent<Collider>() ?? hit.collider;

        pos = hit.point; // giữ nguyên cao độ thực tế
        return true;
    }

    // ===== Preview =====
private void EnsurePreviewObjects(Transform parentForPreview)
{
    // Root
    if (previewRootGO == null)
    {
        previewRootGO = new GameObject("[Room Preview]");
        previewRootGO.transform.SetParent(null, worldPositionStays: true);
    }

    // Materials
    if (previewLineMat == null || previewFillMat == null)
    {
        var unlit = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        if (previewLineMat == null) previewLineMat = new Material(unlit);
        if (previewFillMat == null) previewFillMat = new Material(unlit);
    }

    // Outline line
    if (previewLine == null)
    {
        var lineGO = new GameObject("Line");
        lineGO.transform.SetParent(previewRootGO.transform, false);
        previewLine = lineGO.AddComponent<LineRenderer>();
        previewLine.loop = true;
        previewLine.widthMultiplier = lineWidth;
        previewLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        previewLine.receiveShadows = false;
        previewLine.alignment = LineAlignment.View;
        previewLine.material = previewLineMat;
        previewLine.positionCount = 4;
        previewLine.sortingOrder = 5;
    }

    // Fill mesh
    if (previewFillGO == null)
    {
        previewFillGO = new GameObject("Fill");
        previewFillGO.transform.SetParent(previewRootGO.transform, false);
    }
    if (previewFillMF == null) previewFillMF = previewFillGO.AddComponent<MeshFilter>();
    if (previewFillMR == null) previewFillMR = previewFillGO.AddComponent<MeshRenderer>();
    previewFillMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    previewFillMR.receiveShadows = false;
    if (previewFillMR.sharedMaterial == null) previewFillMR.sharedMaterial = previewFillMat;
    if (previewFillMesh == null) previewFillMesh = new Mesh { name = "RoomPreviewFill" };
    previewFillMF.sharedMesh = previewFillMesh;

    // Helpers
    LineRenderer CreateLR(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(previewRootGO.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.widthMultiplier = lineWidth * dimLineMul;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = previewLineMat;
        lr.positionCount = 2;
        lr.sortingOrder = 10;
        return lr;
    }
    TextMesh CreateText(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(previewRootGO.transform, false);
        var tm = go.AddComponent<TextMesh>();
        tm.text = "";
        tm.anchor = TextAnchor.MiddleCenter;
        tm.fontSize = dimFontSize;
        tm.characterSize = dimCharSize;
        tm.color = Color.black;
        var mr = go.GetComponent<MeshRenderer>(); if (mr) mr.sortingOrder = 11;
        return tm;
    }

    // Dimensions — dùng if (x == null) thay vì ??=
    if (lenLR == null)    lenLR    = CreateLR("DimLen");
    if (lenHeadL == null) lenHeadL = CreateLR("DimLenHeadL");
    if (lenHeadR == null) lenHeadR = CreateLR("DimLenHeadR");
    if (lenText == null)  lenText  = CreateText("DimLenText");

    if (widLR == null)    widLR    = CreateLR("DimWid");
    if (widHeadB == null) widHeadB = CreateLR("DimWidHeadB");
    if (widHeadT == null) widHeadT = CreateLR("DimWidHeadT");
    if (widText == null)  widText  = CreateText("DimWidText");

    previewRootGO.SetActive(true);
}

    private void HidePreview()
    {
        if (previewRootGO != null) previewRootGO.SetActive(false);
    }

private void DestroyPreviewImmediate()
{
    if (previewRootGO != null) Destroy(previewRootGO);
    previewRootGO = null;

    previewLine = null;
    previewFillGO = null;
    previewFillMF = null;
    previewFillMR = null;
    previewFillMesh = null;
    
    lenLR = lenHeadL = lenHeadR = null;
    widLR = widHeadB = widHeadT = null;
    lenText = null;
    widText = null;
}


    private void UpdatePreview(Vector3 p1, Vector3 p2, Transform floorRoot)
    {
        EnsurePreviewObjects(floorRoot);

        float minX = Mathf.Min(p1.x, p2.x);
        float maxX = Mathf.Max(p1.x, p2.x);
        float minZ = Mathf.Min(p1.z, p2.z);
        float maxZ = Mathf.Max(p1.z, p2.z);

        float yPlane = p1.y;
        Vector3 v0 = new(minX, yPlane, minZ);
        Vector3 v1 = new(minX, yPlane, maxZ);
        Vector3 v2 = new(maxX, yPlane, maxZ);
        Vector3 v3 = new(maxX, yPlane, minZ);

        bool ok =
            IsPointOnThisFloorRoot(v0, floorRoot) &&
            IsPointOnThisFloorRoot(v1, floorRoot) &&
            IsPointOnThisFloorRoot(v2, floorRoot) &&
            IsPointOnThisFloorRoot(v3, floorRoot);

        Vector3 lift = Vector3.up * 0.01f;
        Vector3 vv0 = v0 + lift;
        Vector3 vv1 = v1 + lift;
        Vector3 vv2 = v2 + lift;
        Vector3 vv3 = v3 + lift;

        // Line
        previewLine.startColor = ok ? previewOKLine : previewBadLine;
        previewLine.endColor = ok ? previewOKLine : previewBadLine;
        previewLine.widthMultiplier = lineWidth;
        previewLine.positionCount = 4;
        previewLine.SetPositions(new[] { vv0, vv1, vv2, vv3 });

        // Fill
        var verts = new Vector3[] { vv0, vv1, vv2, vv3 };
        var tris = new int[] { 0, 1, 2, 0, 2, 3 };

        previewFillMesh.Clear();
        previewFillMesh.vertices = verts;
        previewFillMesh.triangles = tris;
        previewFillMesh.RecalculateNormals();
        previewFillMesh.RecalculateBounds();

        if (previewFillMR.sharedMaterial == null) previewFillMR.sharedMaterial = previewFillMat;
        if (previewFillMR.sharedMaterial.HasProperty("_Color"))
            previewFillMR.sharedMaterial.color = ok ? previewOKFill : previewBadFill;
        // ===== Dimensions (thước và text) =====
        float yDim = v0.y + 0.012f;
        Color dimCol = ok ? previewOKLine : previewBadLine;

        void SetLRColor(LineRenderer lr)
        {
            if (lr == null) return;
            lr.startColor = dimCol; lr.endColor = dimCol;
            lr.widthMultiplier = lineWidth * dimLineMul;
        }

        // Vẽ một dimension (thân + 2 đầu + text) — KHÔNG xoay theo camera
        void DrawDim(LineRenderer body, LineRenderer headA, LineRenderer headB, TextMesh text,
             Vector3 a, Vector3 b, bool offsetToOuter, Vector3 textAxis /* hướng chữ */)
        {
            if (body == null || headA == null || headB == null || text == null) return;

            Vector3 dir = b - a; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;

            Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 off = (offsetToOuter ? perp : -perp) * dimOffset;

            Vector3 A = new Vector3(a.x, yDim, a.z) + off;
            Vector3 B = new Vector3(b.x, yDim, b.z) + off;

            // thân
            body.positionCount = 2;
            body.SetPosition(0, A);
            body.SetPosition(1, B);
            SetLRColor(body);

            // đầu A
            Vector3 back = (-dir).normalized;
            Vector3 wingL = (Quaternion.Euler(0f, +25f, 0f) * back).normalized;
            Vector3 wingR = (Quaternion.Euler(0f, -25f, 0f) * back).normalized;
            headA.positionCount = 3;
            headA.SetPosition(0, A);
            headA.SetPosition(1, A + wingL * dimHeadSize);
            headA.SetPosition(2, A + wingR * dimHeadSize);
            SetLRColor(headA);

            // đầu B
            Vector3 fwrd = dir.normalized;
            wingL = (Quaternion.Euler(0f, +25f, 0f) * fwrd).normalized;
            wingR = (Quaternion.Euler(0f, -25f, 0f) * fwrd).normalized;
            headB.positionCount = 3;
            headB.SetPosition(0, B);
            headB.SetPosition(1, B + wingL * dimHeadSize);
            headB.SetPosition(2, B + wingR * dimHeadSize);
            SetLRColor(headB);


            float dist = Vector3.Distance(a, b);
            text.text = $"{dist:0.##} m";
            text.transform.position = (A + B) * 0.5f + Vector3.up * 0.001f;


            Vector3 axis = Vector3.ProjectOnPlane(textAxis, Vector3.up);
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.right;

            bool alongX = Mathf.Abs(axis.x) >= Mathf.Abs(axis.z);
            axis = alongX ? Vector3.right : Vector3.forward;

            Quaternion baseFlatDown = Quaternion.AngleAxis(90f, Vector3.right);
            float yaw = alongX ? -90f : 0f;                                      
            text.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseFlatDown;
        }

        DrawDim(lenLR, lenHeadL, lenHeadR, lenText, vv1, vv2, true, Vector3.forward);

        DrawDim(widLR, widHeadB, widHeadT, widText, vv0, vv1, false, Vector3.right);


    }

    // ===== Tạo room khi thả chuột =====
    private void CommitRoom(Vector3 p1, Vector3 p2, Transform floorRoot)
    {
        if (Vector3.Distance(p1, p2) < 0.01f)
        {
            Debug.LogWarning("[CreateRoom] Kéo quá ngắn → bỏ qua.");
            ResetDragState(keepPlacing: true);
            return;
        }

        float minX = Mathf.Min(p1.x, p2.x);
        float maxX = Mathf.Max(p1.x, p2.x);
        float minZ = Mathf.Min(p1.z, p2.z);
        float maxZ = Mathf.Max(p1.z, p2.z);

        // đặt “tầng” cho room bằng roomIndex
        float baseRoomY = roomIndex * layerStepY;

        Vector3 v0 = new(minX, baseRoomY, minZ);
        Vector3 v1 = new(minX, baseRoomY, maxZ);
        Vector3 v2 = new(maxX, baseRoomY, maxZ);
        Vector3 v3 = new(maxX, baseRoomY, minZ);

        bool allOK =
            IsPointOnThisFloorRoot(v0, floorRoot) &&
            IsPointOnThisFloorRoot(v1, floorRoot) &&
            IsPointOnThisFloorRoot(v2, floorRoot) &&
            IsPointOnThisFloorRoot(v3, floorRoot);

        if (!allOK)
        {
            Debug.LogError("[CreateRoom] Một hoặc nhiều đỉnh KHÔNG thuộc RoomFloor → không tạo phòng.");
            ResetDragState(keepPlacing: true);
            return;
        }

        // Lấy floor tương ứng
        if (!TryGetFloorFromRoot(floorRoot, out var floor) || floor == null)
        {
            Debug.LogError("[CreateRoom] Không tìm ra Floor từ floorRoot → huỷ tạo phòng.");
            ResetDragState(keepPlacing: true);
            return;
        }

        // Checkpoints (XZ)
        Vector2 l0 = new(v0.x, v0.z);
        Vector2 l1 = new(v1.x, v1.z);
        Vector2 l2 = new(v2.x, v2.z);
        Vector2 l3 = new(v3.x, v3.z);

        // WallLines (nổi nhẹ)
        Vector3 v0_show = new(v0.x, v0.y + roomWallLift, v0.z);
        Vector3 v1_show = new(v1.x, v1.y + roomWallLift, v1.z);
        Vector3 v2_show = new(v2.x, v2.y + roomWallLift, v2.z);
        Vector3 v3_show = new(v3.x, v3.y + roomWallLift, v3.z);

        // === Tạo room trên floor ===
        var room = new Room(floor)   // constructor này sẽ gán room.floorID = floor.ID
        {
            checkpoints = new List<Vector2> { l0, l1, l2, l3 },
            extraCheckpoints = new List<Vector2>(),
            wallLines = new List<WallLine>
        {
            new WallLine(v0_show, v1_show, LineType.Wall),
            new WallLine(v1_show, v2_show, LineType.Wall),
            new WallLine(v2_show, v3_show, LineType.Wall),
            new WallLine(v3_show, v0_show, LineType.Wall),
        }
        };
        // === HƯỚNG MẶC ĐỊNH CHO ROOM: Bắc ===
// Bắc = Z+ => heading = 0°, compass = (0,1)
room.headingCompass = 0f;
room.Compass = new Vector2(0f, 1f);

// === Gán heading cho từng đoạn tường theo chuẩn Bắc = 0° ===
for (int i = 0; i < room.wallLines.Count; i++)
{
    var wl = room.wallLines[i];
    wl.headingCompass = HeadingManager.HeadingDeg(wl.start, wl.end);
    room.wallLines[i] = wl; // struct-like assign (vì WallLine là class thì không cần thiết, nhưng cứ giữ an toàn)
}

        room.center = GeoUtil.Centroid(room.checkpoints);
        // Lưu storage
        RoomStorage.UpdateOrAddRoom(room);
        floor.RegisterRoom(room); // gắn room.ID vào floor.roomIDs

        GameObject floorGO = new GameObject($"RoomFloor_{room.ID}");
        floorGO.transform.SetPositionAndRotation(new Vector3(0f, baseRoomY, 0f), Quaternion.identity);
        var meshCtrl = floorGO.AddComponent<RoomMeshController>();
        meshCtrl.Initialize(room.ID);
        meshCtrl.GenerateMesh(room.checkpoints);

        // Gắn vào CheckpointManager
        if (checkPointManager != null)
        {
            checkPointManager.RoomFloorMap ??= new Dictionary<string, GameObject>();
            checkPointManager.RoomFloorMap[room.ID] = floorGO;

            var loopGO = new List<GameObject>();
            if (checkPointManager.checkpointPrefab != null)
            {
                loopGO.Add(Instantiate(checkPointManager.checkpointPrefab, v0_show, Quaternion.identity));
                loopGO.Add(Instantiate(checkPointManager.checkpointPrefab, v1_show, Quaternion.identity));
                loopGO.Add(Instantiate(checkPointManager.checkpointPrefab, v2_show, Quaternion.identity));
                loopGO.Add(Instantiate(checkPointManager.checkpointPrefab, v3_show, Quaternion.identity));
            }

            checkPointManager.loopMappings ??= new List<LoopMap>();
            checkPointManager.loopMappings.Add(new LoopMap(room.ID, loopGO));
            checkPointManager.AllCheckpoints.Add(loopGO);

            checkPointManager.RedrawAllRooms();
        }

        Debug.Log($"[CreateRoom] Tạo room {room.ID} thuộc floor {floor.ID} | 4 đỉnh: {l0}, {l1}, {l2}, {l3}");

        // Reset
        ResetDragState(keepPlacing: false);
        placingActive = false;
        if (CreateRoomButton != null)
        {
            var colors = CreateRoomButton.colors;
            colors.normalColor = Color.white;
            CreateRoomButton.colors = colors;
        }
    }

    private void ResetDragState(bool keepPlacing)
    {
        isDragging = false;
        IsCreateRooom = false;

        dragFloorRoot = null;
        dragFloorCol = null;

        HidePreview();

        if (firstMarker) Destroy(firstMarker);
        firstMarker = null;

        if (!keepPlacing)
        {
            // tắt preview hẳn
            DestroyPreviewImmediate();
        }
    }

    // ==================== Floor polygon-based checks ====================

    private Floor FindFloorByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var f in FloorStorage.floors)
            if (f != null && f.ID == id) return f;
        return null;
    }

    private bool TryGetFloorFromRoot(Transform floorRoot, out Floor floor)
    {
        floor = null;
        if (!floorRoot) return false;

        // Ưu tiên lấy từ FloorMeshController (đã có floorID)
        var fmc = floorRoot.GetComponent<FloorMeshController>() ?? floorRoot.GetComponentInChildren<FloorMeshController>();
        if (fmc != null && !string.IsNullOrEmpty(fmc.floorID))
        {
            floor = FindFloorByID(fmc.floorID);
            if (floor != null) return true;
        }

        // ID từ tên "Floor_<ID>" hoặc "RoomFloor_<ID>"
        string name = floorRoot.name;
        int idx = name.LastIndexOf('_');
        if (idx >= 0 && idx + 1 < name.Length)
        {
            string candidate = name.Substring(idx + 1);
            floor = FindFloorByID(candidate);
            if (floor != null) return true;
        }
        return false;
    }

    private static bool PointInPolygon2D(Vector2 p, List<Vector2> poly)
    {
        if (poly == null || poly.Count < 3) return false;
        int c = 0;
        for (int i = 0, n = poly.Count; i < n; i++)
        {
            var a = poly[i];
            var b = (i + 1 < n) ? poly[i + 1] : poly[0];
            if (((a.y > p.y) != (b.y > p.y)) &&
                (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + 1e-12f) + a.x))
                c++;
        }
        return (c & 1) == 1;
    }

    private static float DistPointToSegment2D(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float ab2 = Vector2.Dot(ab, ab);
        if (ab2 < 1e-12f) return (p - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2);
        var proj = a + t * ab;
        return (p - proj).sqrMagnitude;
    }

    private static bool OnBoundary2D(Vector2 p, List<Vector2> poly, float eps = 1e-4f)
    {
        if (poly == null || poly.Count < 2) return false;
        float eps2 = eps * eps;
        for (int i = 0, n = poly.Count; i < n; i++)
        {
            var a = poly[i];
            var b = (i + 1 < n) ? poly[i + 1] : poly[0];
            if (DistPointToSegment2D(p, a, b) <= eps2) return true;
        }
        return false;
    }

    // Kiểm tra 1 điểm world có thuộc Floor này không (dựa polygon), fallback raycast nếu thiếu dữ liệu
    private bool IsPointOnThisFloorRoot(Vector3 worldPoint, Transform floorRoot,
                                        float boundaryEps = 0.01f,
                                        float rayFallbackHeight = 3f)
    {
        if (TryGetFloorFromRoot(floorRoot, out var floor) &&
            floor.checkpoints != null && floor.checkpoints.Count >= 3)
        {
            Vector2 p2 = new Vector2(worldPoint.x, worldPoint.z);
            if (PointInPolygon2D(p2, floor.checkpoints)) return true;
            if (OnBoundary2D(p2, floor.checkpoints, boundaryEps)) return true;
            return false;
        }

        // Fallback: raycast xuống và kiểm ancestor = floorRoot
        Vector3 start = worldPoint + Vector3.up * rayFallbackHeight;
        var hits = Physics.RaycastAll(new Ray(start, Vector3.down), rayFallbackHeight * 2f, floorMask);
        for (int i = 0; i < hits.Length; i++)
        {
            var tr = hits[i].collider ? hits[i].collider.transform : null;
            if (tr != null && (tr == floorRoot || tr.IsChildOf(floorRoot)))
                return true;
        }
        return false;
    }
    
    // 0° = Bắc (Z+), 90° = Đông (X+), 180° = Nam (Z-), 270° = Tây (X-)
// private static float HeadingDeg(Vector3 from, Vector3 to)
// {
//     Vector3 dir = to - from;
//     dir.y = 0f;                           // chỉ xét mặt phẳng XZ
//     if (dir.sqrMagnitude < 1e-8f) return 0f;

//     // Atan2(x, z) để ra 0° khi trỏ thẳng lên Z+
//     float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
//     if (angle < 0f) angle += 360f;
//     return angle;                         // [0,360)
// }
// Tìm 1 floor ứng viên gần chuột nhất và trả về P1 đã clamp vào mép sàn.
// Dùng khi bắt đầu bấm mà không raycast trúng floor nào.
    private bool TryGetMouseOnAnyFloorClamped(out Vector3 pClamped, out Transform floorRoot)
    {
        pClamped = default;
        floorRoot = null;

        var cam = Camera.main; 
        if (cam == null) return false;

        // Lấy giao điểm với mặt phẳng ngang (y = 0) làm mốc XZ
        if (!MouseToPlaneY(0f, out var onPlane)) return false;

        // Lấy tất cả floor theo tag "RoomFloor"
        var candidates = GameObject.FindGameObjectsWithTag("RoomFloor");
        if (candidates == null || candidates.Length == 0) return false;

        float bestScore = float.PositiveInfinity;
        Transform bestRoot = null;
        Vector3 bestP = default;

        foreach (var go in candidates)
        {
            var root = go.transform;
            // Ưu tiên dùng y của root (hoặc 0 nếu bạn muốn cố định)
            float y = root.position.y;

            // Snap vào polygon/collider của floor này
            var snapped = ClampPointToFloor(new Vector3(onPlane.x, y, onPlane.z), root, y);

            // Độ “hợp lý” = khoảng cách 2D giữa onPlane và điểm snap (càng gần càng tốt)
            float score = (new Vector2(onPlane.x, onPlane.z) - new Vector2(snapped.x, snapped.z)).sqrMagnitude;

            if (score < bestScore)
            {
                bestScore = score;
                bestRoot = root;
                bestP = snapped;
            }
        }

        if (bestRoot != null)
        {
            floorRoot = bestRoot;
            pClamped = bestP;
            return true;
        }
        return false;
    }
    // Trả về điểm world đã được "kéo" vào bên trong polygon sàn (hoặc sát mép nếu ở ngoài)
    private Vector3 ClampPointToFloor(Vector3 worldPoint, Transform floorRoot, float defaultY)
    {
        // Nếu có dữ liệu polygon của Floor → snap theo 2D (XZ)
        if (TryGetFloorFromRoot(floorRoot, out var floor) &&
            floor.checkpoints != null && floor.checkpoints.Count >= 3)
        {
            Vector2 p = new Vector2(worldPoint.x, worldPoint.z);

            // Nếu đã ở trong/biên → giữ nguyên (chỉ chuẩn hóa Y)
            if (PointInPolygon2D(p, floor.checkpoints) || OnBoundary2D(p, floor.checkpoints, 1e-4f))
                return new Vector3(worldPoint.x, defaultY, worldPoint.z);

            // Tìm điểm gần nhất trên mọi cạnh
            float bestDist2 = float.PositiveInfinity;
            Vector2 best = p;

            for (int i = 0, n = floor.checkpoints.Count; i < n; i++)
            {
                var a = floor.checkpoints[i];
                var b = (i + 1 < n) ? floor.checkpoints[i + 1] : floor.checkpoints[0];

                // project p lên đoạn ab
                Vector2 ab = b - a;
                float ab2 = Vector2.Dot(ab, ab);
                Vector2 cand;
                if (ab2 < 1e-10f)
                {
                    cand = a; // đoạn quá ngắn
                }
                else
                {
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2);
                    cand = a + t * ab;
                }

                float d2 = (p - cand).sqrMagnitude;
                if (d2 < bestDist2)
                {
                    bestDist2 = d2;
                    best = cand;
                }
            }

            return new Vector3(best.x, defaultY, best.y);
        }

        // Fallback: dùng collider gần nhất (nếu có)
        var col = floorRoot ? (floorRoot.GetComponent<Collider>() ?? floorRoot.GetComponentInChildren<Collider>()) : null;
        if (col != null)
        {
            var cp = col.ClosestPoint(worldPoint);
            return new Vector3(cp.x, defaultY, cp.z);
        }

        // Không có gì → giữ nguyên XZ, khóa Y
        return new Vector3(worldPoint.x, defaultY, worldPoint.z);
    }

    // Tính giao điểm ray chuột với mặt phẳng ngang tại y = planeY
    private bool MouseToPlaneY(float planeY, out Vector3 hit)
    {
        hit = default;
        var cam = Camera.main; if (cam == null) return false;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (plane.Raycast(ray, out float t))
        {
            hit = ray.GetPoint(t);
            return true;
        }
        return false;
    }

}
