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

    // Trạng thái đặt điểm
    private bool placingActive = false;
    private Vector3? firstPointWorld = null;
    private GameObject firstMarker = null;         // marker tạm để hiển thị P1
    private Collider firstPointFloorCol = null;    // collider trúng tại click 1 (thông tin phụ)
    private Transform firstPointFloorRoot = null;  // root Transform có tag "RoomFloor" tại click 1 (thông tin chính)

    // ===== Preview objects =====
    private GameObject previewRootGO;
    private LineRenderer previewLine;
    private GameObject previewFillGO;
    private MeshFilter previewFillMF;
    private MeshRenderer previewFillMR;
    private Mesh previewFillMesh;
    private Material previewLineMat;
    private Material previewFillMat;

    // ===== Unity lifecycle =====
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
    }

    void Update()
    {
        if (!placingActive) return;

        // Bỏ qua khi click lên UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // === LIVE PREVIEW khi đã có P1 ===
        if (firstPointWorld != null)
        {
            if (TryGetMouseWorldOnFloor(out var pos, out var col, out var root) && root == firstPointFloorRoot)
            {
                UpdatePreview(firstPointWorld.Value, pos, root);
            }
            else
            {
                HidePreview();
            }
        }

        // Click chuột trái -> xử lý đặt P1/P2
        if (Input.GetMouseButtonUp(0))
        {
            HandleSingleRoom(); // tự lấy world pos từ chuột
        }
    }

    // ===== Toggle =====
    private void TogglePlacingMode()
    {
        placingActive = !placingActive;

        // reset state khi tắt
        if (!placingActive) ClearFirstPointState();

        // (tuỳ chọn) đổi màu nút khi bật/tắt
        if (CreateRoomButton != null)
        {
            var colors = CreateRoomButton.colors;
            colors.normalColor = placingActive ? new Color(0.8f, 1f, 0.8f) : Color.white;
            CreateRoomButton.colors = colors;
        }
    }

    // ===== Helpers =====
    private void ClearFirstPointState()
    {
        firstPointWorld = null;
        firstPointFloorCol = null;
        firstPointFloorRoot = null;
        if (firstMarker) Destroy(firstMarker);
        firstMarker = null;
        HidePreview();
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

    private void HidePreview()
    {
        if (previewRootGO != null) previewRootGO.SetActive(false);
    }

    private void EnsurePreviewObjects(Transform parentForPreview)
    {
        if (previewRootGO == null)
        {
            previewRootGO = new GameObject("[Room Preview]");
            // đặt cùng root để theo hệ quy chiếu floor (không cần parent cũng được)
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

        // Bật preview
        previewRootGO.SetActive(true);
    }

    // Raycast chuột xuống sàn → world pos + collider bị trúng + root Transform có tag "RoomFloor"
    // (chấp nhận tag ở parent; click có thể vào collider con)
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

        if (t == null)
        {
            // Không phải RoomFloor
            return false;
        }

        floorRoot = t;
        // ưu tiên collider ngay trên root, nếu không có thì dùng collider vừa hit
        floorCol = t.GetComponent<Collider>() ?? hit.collider;

        pos = hit.point; // giữ nguyên cao độ thực tế
        return true;
    }
    
    // tìm Floor data từ floorRoot 
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

    // ===== Geometry utils (2D XZ) =====
    private static bool PointInPolygon2D(Vector2 p, List<Vector2> poly)
    {
        if (poly == null || poly.Count < 3) return false;
        int c = 0;
        for (int i = 0, n = poly.Count; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            // odd-even
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

    // kiểm tra 1 điểm có thuộc Floor (dựa vào polygon), fallback raycast nếu thiếu dữ liệu
    private bool IsPointOnThisFloorRoot(Vector3 worldPoint, Transform floorRoot,
                                        float boundaryEps = 0.01f,
                                        float rayFallbackHeight = 3f)
    {
        // 1) Ưu tiên dùng dữ liệu polygon của Floor
        if (TryGetFloorFromRoot(floorRoot, out var floor) &&
            floor.checkpoints != null && floor.checkpoints.Count >= 3)
        {
            Vector2 p2 = new Vector2(worldPoint.x, worldPoint.z);
            if (PointInPolygon2D(p2, floor.checkpoints)) return true;
            if (OnBoundary2D(p2, floor.checkpoints, boundaryEps)) return true;
            return false;
        }

        // 2) Fallback: raycast xuống và kiểm ancestor = floorRoot
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

    // Cập nhật preview khi đã có P1 và đang rê chuột để chọn P2
    private void UpdatePreview(Vector3 p1, Vector3 p2, Transform floorRoot)
    {
        EnsurePreviewObjects(floorRoot);

        float minX = Mathf.Min(p1.x, p2.x);
        float maxX = Mathf.Max(p1.x, p2.x);
        float minZ = Mathf.Min(p1.z, p2.z);
        float maxZ = Mathf.Max(p1.z, p2.z);

        // Cùng mặt phẳng Y của p1
        float yPlane = p1.y;
        Vector3 v0 = new(minX, yPlane, minZ);
        Vector3 v1 = new(minX, yPlane, maxZ);
        Vector3 v2 = new(maxX, yPlane, maxZ);
        Vector3 v3 = new(maxX, yPlane, minZ);

        // Kiểm tra hợp lệ (4 góc nằm trên RoomFloor)
        bool ok =
            IsPointOnThisFloorRoot(v0, floorRoot) &&
            IsPointOnThisFloorRoot(v1, floorRoot) &&
            IsPointOnThisFloorRoot(v2, floorRoot) &&
            IsPointOnThisFloorRoot(v3, floorRoot);

        // Nâng nhẹ để thấy
        Vector3 lift = Vector3.up * 0.01f;
        Vector3 vv0 = v0 + lift;
        Vector3 vv1 = v1 + lift;
        Vector3 vv2 = v2 + lift;
        Vector3 vv3 = v3 + lift;

        // Line
        previewLine.material = previewLineMat;
        previewLine.startColor = ok ? previewOKLine : previewBadLine;
        previewLine.endColor   = ok ? previewOKLine : previewBadLine;
        previewLine.widthMultiplier = lineWidth;
        previewLine.positionCount = 4;
        previewLine.SetPositions(new[] { vv0, vv1, vv2, vv3 });

        // Fill mesh (quad)
        var verts = new Vector3[] { vv0, vv1, vv2, vv3 };
        var tris  = new int[] { 0, 1, 2, 0, 2, 3 };

        previewFillMesh.Clear();
        previewFillMesh.vertices = verts;
        previewFillMesh.triangles = tris;
        previewFillMesh.RecalculateNormals();
        previewFillMesh.RecalculateBounds();

        if (previewFillMR.sharedMaterial == null) previewFillMR.sharedMaterial = previewFillMat;
        // Set màu
        if (previewFillMR.sharedMaterial.HasProperty("_Color"))
            previewFillMR.sharedMaterial.color = ok ? previewOKFill : previewBadFill;
    }

    public void HandleSingleRoom()
    {
        if (!placingActive) return;

        // CLICK hiện tại phải trúng đúng một RoomFloor (root có tag RoomFloor)
        if (!TryGetMouseWorldOnFloor(out var position, out var floorCol, out var floorRoot))
        {
            Debug.LogError("[CreateRoom] Click KHÔNG trúng RoomFloor → huỷ.");
            ClearFirstPointState();
            return;
        }

        // CLICK 1
        if (firstPointWorld == null)
        {
            firstPointWorld = position;
            firstPointFloorCol  = floorCol;   // info phụ
            firstPointFloorRoot = floorRoot;  // info chính để so sánh

            // Marker P1
            if (checkPointManager != null && checkPointManager.checkpointPrefab != null)
            {
                firstMarker = Instantiate(checkPointManager.checkpointPrefab, position + Vector3.up * 0.01f, Quaternion.identity);
                firstMarker.name = "RoomP1_Preview";
            }

            // Khởi tạo preview ngay sau khi đặt P1
            UpdatePreview(firstPointWorld.Value, position, firstPointFloorRoot);
            return;
        }

        // CLICK 2 — bắt buộc cùng RoomFloor root với click 1 (không so sánh collider con)
        if (floorRoot != firstPointFloorRoot)
        {
            Debug.LogError("[CreateRoom] Hai click KHÔNG cùng một RoomFloor → không tạo phòng.");
            ClearFirstPointState();
            return;
        }

        // Hai điểm hợp lệ trên cùng Floor → tạo phòng chữ nhật theo đường chéo
        Vector3 p1 = firstPointWorld.Value;
        Vector3 p2 = position;

        if (Vector3.Distance(p1, p2) < 0.01f)
        {
            Debug.LogWarning("[CreateRoom] P2 quá gần P1 → bỏ qua.");
            ClearFirstPointState();
            return;
        }

        float minX = Mathf.Min(p1.x, p2.x);
        float maxX = Mathf.Max(p1.x, p2.x);
        float minZ = Mathf.Min(p1.z, p2.z);
        float maxZ = Mathf.Max(p1.z, p2.z);

        // 4 đỉnh theo world XZ, GIỮ y từ p1 (cùng mặt phẳng)
        float yPlane = p1.y;
        Vector3 v0 = new(minX, yPlane, minZ);
        Vector3 v1 = new(minX, yPlane, maxZ);
        Vector3 v2 = new(maxX, yPlane, maxZ);
        Vector3 v3 = new(maxX, yPlane, minZ);

        // Phải đúng RoomFloor
        bool allOK =
            IsPointOnThisFloorRoot(v0, firstPointFloorRoot) &&
            IsPointOnThisFloorRoot(v1, firstPointFloorRoot) &&
            IsPointOnThisFloorRoot(v2, firstPointFloorRoot) &&
            IsPointOnThisFloorRoot(v3, firstPointFloorRoot);

        if (!allOK)
        {
            Debug.LogError("[CreateRoom] Một hoặc nhiều đỉnh KHÔNG thuộc RoomFloor → không tạo phòng.");
            ClearFirstPointState();
            return;
        }

        // Dùng tọa độ 2D (x,z) để lưu room (tuỳ hệ toạ độ dự án của bạn)
        Vector2 l0 = new(v0.x, v0.z);
        Vector2 l1 = new(v1.x, v1.z);
        Vector2 l2 = new(v2.x, v2.z);
        Vector2 l3 = new(v3.x, v3.z);

        // Nâng hiển thị 0.01f khi vẽ line/marker
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

        // Floor GO (mesh holder) – ở đây dùng RoomMeshController như trước
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

        // Dọn & tắt chế độ
        ClearFirstPointState();
        placingActive = false;
        if (CreateRoomButton != null)
        {
            var colors = CreateRoomButton.colors;
            colors.normalColor = Color.white;
            CreateRoomButton.colors = colors;
        }

        Debug.Log($"[CreateRoom] Tạo room {room.ID} trong RoomFloor '{firstPointFloorRoot.name}' | 4 đỉnh: {l0}, {l1}, {l2}, {l3}");
    }
}
