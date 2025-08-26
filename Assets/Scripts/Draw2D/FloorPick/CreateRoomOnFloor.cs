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

    private CheckpointManager checkPointManager;

    // Trạng thái
    private bool placingActive = false;

    // Kéo-để-tạo (drag)
    private bool isDragging = false;
    private Vector3 dragStartWorld;             // P1 (khi MouseDown)
    private Transform dragFloorRoot;            // RoomFloor root tại P1
    private Collider  dragFloorCol;             // collider tại P1 (thông tin phụ)

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
                isDragging = true;
                dragStartWorld = pos;
                dragFloorRoot  = root;
                dragFloorCol   = col;

                // Marker P1
                if (checkPointManager != null && checkPointManager.checkpointPrefab != null)
                {
                    firstMarker = Instantiate(checkPointManager.checkpointPrefab, pos + Vector3.up * 0.01f, Quaternion.identity);
                    firstMarker.name = "RoomP1_Preview";
                }

                // Khởi tạo preview
                UpdatePreview(dragStartWorld, pos, dragFloorRoot);
            }
            else
            {
                // Không trúng RoomFloor → không cho kéo
                isDragging = false;
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
        }
    }

    // ===== Toggle =====
    private void TogglePlacingMode()
    {
        placingActive = !placingActive;
        if (!placingActive) ResetDragState(keepPlacing: false);

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
        if (previewRootGO == null)
        {
            previewRootGO = new GameObject("[Room Preview]");
            previewRootGO.transform.SetParent(null, worldPositionStays: true);

            // Line
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

            // Fill
            previewFillGO = new GameObject("Fill");
            previewFillGO.transform.SetParent(previewRootGO.transform, false);
            previewFillMF = previewFillGO.AddComponent<MeshFilter>();
            previewFillMR = previewFillGO.AddComponent<MeshRenderer>();
            previewFillMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            previewFillMR.receiveShadows = false;
            previewFillMR.sharedMaterial = previewFillMat;
            previewFillMesh = new Mesh { name = "RoomPreviewFill" };
            previewFillMF.sharedMesh = previewFillMesh;
        }

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
        previewLine.endColor   = ok ? previewOKLine : previewBadLine;
        previewLine.widthMultiplier = lineWidth;
        previewLine.positionCount = 4;
        previewLine.SetPositions(new[] { vv0, vv1, vv2, vv3 });

        // Fill
        var verts = new Vector3[] { vv0, vv1, vv2, vv3 };
        var tris  = new int[] { 0, 1, 2, 0, 2, 3 };

        previewFillMesh.Clear();
        previewFillMesh.vertices = verts;
        previewFillMesh.triangles = tris;
        previewFillMesh.RecalculateNormals();
        previewFillMesh.RecalculateBounds();

        if (previewFillMR.sharedMaterial == null) previewFillMR.sharedMaterial = previewFillMat;
        if (previewFillMR.sharedMaterial.HasProperty("_Color"))
            previewFillMR.sharedMaterial.color = ok ? previewOKFill : previewBadFill;
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

        float yPlane = p1.y;
        Vector3 v0 = new(minX, yPlane, minZ);
        Vector3 v1 = new(minX, yPlane, maxZ);
        Vector3 v2 = new(maxX, yPlane, maxZ);
        Vector3 v3 = new(maxX, yPlane, minZ);

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

        Vector2 l0 = new(v0.x, v0.z);
        Vector2 l1 = new(v1.x, v1.z);
        Vector2 l2 = new(v2.x, v2.z);
        Vector2 l3 = new(v3.x, v3.z);

        Vector3 v0_show = v0 + Vector3.up * 0.01f;
        Vector3 v1_show = v1 + Vector3.up * 0.01f;
        Vector3 v2_show = v2 + Vector3.up * 0.01f;
        Vector3 v3_show = v3 + Vector3.up * 0.01f;

        var room = new Room
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

        RoomStorage.UpdateOrAddRoom(room);

        // Floor GO (mesh holder) – như cũ
        GameObject floorGO = new GameObject($"RoomFloor_{room.ID}");
        floorGO.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var meshCtrl = floorGO.AddComponent<RoomMeshController>();
        meshCtrl.Initialize(room.ID);
        meshCtrl.GenerateMesh(room.checkpoints);

        // Gắn vào CheckpointManager để có thể kéo/snap ngay
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
            // checkPointManager.AllCheckpoints ??= new List<List<GameObject>>();
            checkPointManager.AllCheckpoints.Add(loopGO);

            checkPointManager.RedrawAllRooms();
        }

        Debug.Log($"[CreateRoom] Tạo room {room.ID} trong RoomFloor '{floorRoot.name}' | 4 đỉnh: {l0}, {l1}, {l2}, {l3}");

        // Tắt chế độ sau khi tạo xong (tuỳ ý)
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

        // Fallback: đoán ID từ tên "Floor_<ID>" hoặc "RoomFloor_<ID>"
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
            var b = poly[(i + 1) % n];
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
            var b = poly[(i + 1) % n];
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
}
