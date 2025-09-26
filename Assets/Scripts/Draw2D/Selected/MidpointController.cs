using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hiển thị 4 midpoint cho Room đang được chọn
/// </summary>
public class MidpointController : MonoBehaviour
{
    [Header("Refs")]
    private CheckpointManager checkPointManager;
    public static bool IsDraggingMidpoint { get; private set; }

    [Header("Midpoint Handles")]
    [SerializeField] private GameObject handlePrefab;   // nếu null thì tạo sphere primitive
    [SerializeField] private float handleSize = 0.25f;
    [SerializeField] private float pickMaxDistance = 3000f;

    [Header("Room Constraints & Elevation")]
    [SerializeField] private float minRoomSide = 0.20f;       // không cho co < 20cm
    [SerializeField] private int roomIndex = 2;               // cùng logic cao độ với CreateRoomOnFloor
    [SerializeField] private float layerStepY = 0.002f;
    [SerializeField] private float roomWallLift = 0.003f;     // nhô nhẹ cho line/marker

    private GameObject[] _handles = new GameObject[4];        // 0=Left,1=Top,2=Right,3=Bottom
    private Room _editingRoom = null;
    private float _baseRoomY;
    private bool _isDragging = false;
    private int _activeEdge = -1;

    // helper gắn index cho handle
    private class EdgeTag : MonoBehaviour { public int edgeIndex; }

    void Awake()
    {
        if (!checkPointManager)
            checkPointManager = FindFirstObjectByType<CheckpointManager>();
    }

    void Update()
    {
        // Nếu có external room -> khóa theo đó, không tự resolve nữa
        if (!string.IsNullOrEmpty(_externalRoomId))
        {
            var r = RoomStorage.GetRoomByID(_externalRoomId);
            if (r != _editingRoom)
            {
                if (r == null) { Hide(); return; }
                _editingRoom = r;
                _baseRoomY = roomIndex * layerStepY;
                BuildHandlesFor(_editingRoom);
            }

            if (_editingRoom != null) UpdateHandlePositions(_editingRoom);
        }
        else
        {
            // Không có external -> ẩn hết (hoặc bạn có thể giữ cơ chế tự resolve nếu muốn)
            if (_editingRoom != null) Hide();
        }

        if (_editingRoom == null) return;
        
        if (!_isDragging && Input.GetMouseButtonDown(0) && TryPickHandle(out int idx))
        {
            InteractionFlags.IsRoomFloorDragging = true; // dùng tạm khoa khóa để đặt
            _isDragging = true;
            _activeEdge = idx;
            IsDraggingMidpoint = true;
        }

        if (_isDragging && Input.GetMouseButton(0))
        {
            if (MouseToPlaneY(_baseRoomY, out var worldOnPlane))
            {
                ApplyEdgeDrag(_editingRoom, worldOnPlane, _activeEdge);
                UpdateHandlePositions(_editingRoom);
            }
        }

        if (_isDragging && Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
            _activeEdge = -1;
            IsDraggingMidpoint = false;
            InteractionFlags.IsRoomFloorDragging = false;
        }
    }

    // ===== Build / Destroy / Update handles =====
    private void BuildHandlesFor(Room room)
    {
        DestroyHandles();

        Vector3[] mids = GetEdgeMidpoints(room);
        for (int i = 0; i < 4; i++)
        {
            _handles[i] = CreateHandle(mids[i], i, new[] { "Left", "Top", "Right", "Bottom" }[i]);
        }
    }

    private void DestroyHandles()
    {
        for (int i = 0; i < _handles.Length; i++)
            if (_handles[i]) Destroy(_handles[i]);
        Array.Clear(_handles, 0, _handles.Length);
        _isDragging = false;
        _activeEdge = -1;
    }

    private void UpdateHandlePositions(Room room)
    {
        if (room == null || room.checkpoints == null || room.checkpoints.Count < 4) return;
        Vector3[] mids = GetEdgeMidpoints(room);
        for (int i = 0; i < 4; i++)
        {
            if (_handles[i])
            {
                var p = mids[i]; p.y = _baseRoomY + roomWallLift;
                _handles[i].transform.position = p;
            }
        }
    }

    private Vector3[] GetEdgeMidpoints(Room room)
    {
        var pts = room.checkpoints;
        float y = _baseRoomY + roomWallLift;
        if (pts == null || pts.Count < 4)
            return new[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };

        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
        for (int i = 0; i < pts.Count; i++)
        {
            var v = pts[i];
            if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
            if (v.y < minZ) minZ = v.y; if (v.y > maxZ) maxZ = v.y;
        }

        Vector3 bl = new Vector3(minX, y, minZ);
        Vector3 tl = new Vector3(minX, y, maxZ);
        Vector3 tr = new Vector3(maxX, y, maxZ);
        Vector3 br = new Vector3(maxX, y, minZ);

        return new[]
        {
            (bl + tl) * 0.5f, // Left
            (tl + tr) * 0.5f, // Top
            (tr + br) * 0.5f, // Right
            (br + bl) * 0.5f  // Bottom
        };
    }

    private GameObject CreateHandle(Vector3 pos, int edgeIdx, string suffix)
    {
        GameObject h;
        if (handlePrefab)
        {
            h = Instantiate(handlePrefab, pos, Quaternion.identity);
            h.transform.localScale = Vector3.one * handleSize;

            // đảm bảo có collider để bắt ray
            if (!h.GetComponent<Collider>() && h.GetComponentInChildren<Collider>() == null)
            {
                var sc = h.AddComponent<SphereCollider>();
                sc.radius = 0.5f; // tuỳ tỉ lệ prefab
            }
            // nếu prefab không có renderer, thêm quả cầu con cho dễ thấy
            if (h.GetComponentInChildren<Renderer>() == null)
            {
                var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                vis.transform.SetParent(h.transform, false);
                vis.transform.localScale = Vector3.one * handleSize;
                var c = vis.GetComponent<Collider>(); if (c) Destroy(c); // chỉ dùng để nhìn, không cần collider phụ
            }
        }
        else
        {
            h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            h.transform.position = pos;
            h.transform.localScale = Vector3.one * handleSize;
            var c = h.GetComponent<Collider>(); if (c) c.isTrigger = false;
        }

        h.name = $"RoomMid_{suffix}";
        var tag = h.GetComponent<EdgeTag>() ?? h.AddComponent<EdgeTag>();
        tag.edgeIndex = edgeIdx;

        // nhô lên khỏi sàn để raycast thấy trước sàn
        var p = h.transform.position; p.y = _baseRoomY + roomWallLift; h.transform.position = p;

        return h;
    }

    private bool TryPickHandle(out int edgeIdx)
    {
        edgeIdx = -1;
        var cam = Camera.main; if (!cam) return false;

        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, pickMaxDistance))
        {
            // lấy EdgeTag ở parent nếu collider ở child
            var tag = hit.collider ? hit.collider.GetComponentInParent<EdgeTag>() : null;
            if (tag != null) { edgeIdx = tag.edgeIndex; return true; }
        }
        return false;
    }

    private bool MouseToPlaneY(float y, out Vector3 p)
    {
        p = default;
        var cam = Camera.main; if (!cam) return false;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        if (plane.Raycast(ray, out float t)) { p = ray.GetPoint(t); return true; }
        return false;
    }

    // ===== Resize logic khi kéo midpoint =====
    private static void NormalizeRectOrder(List<Vector2> pts)
    {
        if (pts == null || pts.Count < 4) return;

        // Tìm min/max theo X/Z
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            var v = pts[i];
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.y < minZ) minZ = v.y;
            if (v.y > maxZ) maxZ = v.y;
        }

        // Gán lại đúng thứ tự BL, TL, TR, BR
        pts[0] = new Vector2(minX, minZ); // BL
        pts[1] = new Vector2(minX, maxZ); // TL
        pts[2] = new Vector2(maxX, maxZ); // TR
        pts[3] = new Vector2(maxX, minZ); // BR
    }

    // Resize an toàn theo edgeIdx (0=Left,1=Top,2=Right,3=Bottom)
    private void ApplyEdgeDrag(Room room, Vector3 mouseWorld, int edgeIdx)
    {
        if (room == null || room.checkpoints == null || room.checkpoints.Count < 4) return;

        // 1) Chuẩn hoá lại thứ tự trước khi tính
        NormalizeRectOrder(room.checkpoints);

        var p = room.checkpoints; // BL(0), TL(1), TR(2), BR(3)
        float leftX   = p[0].x;
        float rightX  = p[2].x;
        float bottomZ = p[0].y;
        float topZ    = p[2].y;

        // 2) Tính new bounds theo cạnh đang kéo + clamp minRoomSide
        float newLeftX   = leftX;
        float newRightX  = rightX;
        float newTopZ    = topZ;
        float newBottomZ = bottomZ;

        switch (edgeIdx)
        {
            case 0: // Left
                newLeftX = Mathf.Min(mouseWorld.x, rightX - minRoomSide);
                break;
            case 1: // Top
                newTopZ = Mathf.Max(mouseWorld.z, bottomZ + minRoomSide);
                break;
            case 2: // Right
                newRightX = Mathf.Max(mouseWorld.x, leftX + minRoomSide);
                break;
            case 3: // Bottom
                newBottomZ = Mathf.Min(mouseWorld.z, topZ - minRoomSide);
                break;
            default:
                return;
        }

        // 3) Guard: không cho width/height <= 0 (phòng co thành line)
        if (newRightX - newLeftX < minRoomSide * 0.999f) return;
        if (newTopZ   - newBottomZ < minRoomSide * 0.999f) return;

        // 4) Gán lại 4 đỉnh theo BL,TL,TR,BR
        p[0] = new Vector2(newLeftX,  newBottomZ);
        p[1] = new Vector2(newLeftX,  newTopZ);
        p[2] = new Vector2(newRightX, newTopZ);
        p[3] = new Vector2(newRightX, newBottomZ);

        // 5) Cập nhật wallLines & mesh như bạn đang làm
        float yShow = _baseRoomY + roomWallLift;
        Vector3 v0 = new Vector3(p[0].x, yShow, p[0].y);
        Vector3 v1 = new Vector3(p[1].x, yShow, p[1].y);
        Vector3 v2 = new Vector3(p[2].x, yShow, p[2].y);
        Vector3 v3 = new Vector3(p[3].x, yShow, p[3].y);

        if (room.wallLines == null || room.wallLines.Count < 4)
        {
            room.wallLines = new List<WallLine>
            {
                new WallLine(v0, v1, LineType.Wall),
                new WallLine(v1, v2, LineType.Wall),
                new WallLine(v2, v3, LineType.Wall),
                new WallLine(v3, v0, LineType.Wall),
            };
        }
        else
        {
            var wl = room.wallLines;
            wl[0].start = v0; wl[0].end = v1;
            wl[1].start = v1; wl[1].end = v2;
            wl[2].start = v2; wl[2].end = v3;
            wl[3].start = v3; wl[3].end = v0;
        }

        room.center = GeoUtil.Centroid(room.checkpoints);

        if (checkPointManager != null)
        {
            checkPointManager.DrawWallLineByRoom(room);
            var meshCtrl = FindMeshCtrl(room.ID);
            if (meshCtrl != null)
            {
                var t = meshCtrl.transform;
                t.position = new Vector3(t.position.x, roomIndex * layerStepY, t.position.z);
                meshCtrl.GenerateMesh(room.checkpoints);
            }
            checkPointManager.RedrawAllRooms();
        }

        SyncCornerCheckpointGOs(room, v0, v1, v2, v3);
    }
    
    private RoomMeshController FindMeshCtrl(string roomId)
    {
        var ctrls = FindObjectsByType<RoomMeshController>(
            FindObjectsInactive.Include,   // có lấy cả inactive hay không
            FindObjectsSortMode.None       // không cần sort theo InstanceID
        );
        for (int i = 0; i < ctrls.Length; i++)
        {
            if (ctrls[i] != null && string.Equals(ctrls[i].RoomID, roomId, StringComparison.Ordinal))
                return ctrls[i];
        }
        return null;
    }

    // ==== đồng bộ 4 corner checkpoint GameObject của phòng (nếu có) ====
    private void SyncCornerCheckpointGOs(Room room, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        if (checkPointManager == null) return;

        // tìm loop của room này
        List<GameObject> loop = null;
        foreach (var lp in checkPointManager.AllCheckpoints)
        {
            if (lp == null) continue;
            var rid = checkPointManager.FindRoomIDForLoop(lp);
            if (!string.IsNullOrEmpty(rid) && rid == room.ID) { loop = lp; break; }
        }
        if (loop == null) return;

        Vector3[] targets = { v0, v1, v2, v3 };

        // ưu tiên các GO không phải CheckpointExtra
        var cornerGOs = new List<GameObject>();
        foreach (var go in loop)
            if (go != null && !go.CompareTag("CheckpointExtra")) cornerGOs.Add(go);
        if (cornerGOs.Count < 4) cornerGOs = loop; // fallback

        if (cornerGOs.Count >= 4)
            for (int i = 0; i < 4; i++)
                if (cornerGOs[i]) cornerGOs[i].transform.position = targets[i];
    }
        private string _externalRoomId = null;

    // Gọi từ RoomInfoDisplay khi chọn room
    public void ShowForRoomID(string roomId)
    {
        _externalRoomId = roomId;
        var r = RoomStorage.GetRoomByID(roomId);
        if (r == null) { Hide(); return; }

        // Nếu không đúng HCN 4 đỉnh thì đừng bật editor
        if (r.checkpoints == null || r.checkpoints.Count != 4) { Hide(); return; }

        NormalizeRectOrder(r.checkpoints); // <-- thêm dòng này

        _editingRoom = r;
        _baseRoomY = roomIndex * layerStepY;
        BuildHandlesFor(_editingRoom);
        UpdateHandlePositions(_editingRoom);
    }

    // Gọi khi chọn Floor / bỏ chọn
    public void Hide()
    {
        _externalRoomId = null;
        _editingRoom = null;
        DestroyHandles();
    }
}
