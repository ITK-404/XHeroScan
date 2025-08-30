using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FloorMeshController : MonoBehaviour
{
    [Header("Binding")]
    [Tooltip("Để trống => chế độ multi-floor: dựng tất cả floors có trong FloorStorage.\nĐiền ID => chỉ dựng floor tương ứng.")]
    public string floorID;                  // rỗng => build all floors; có giá trị => single
    public float changeEpsilon = 1e-4f;     // ngưỡng phát hiện thay đổi toạ độ

    [Header("Material")]
    public Material floorMaterial;          // để trống => Unlit/Color mặc định

    [Header("Render")]
    public bool doubleSided = true;

    // ====== Single-floor (giữ lại để tương thích) ======
    GameObject _targetGO;
    MeshFilter _mf;
    MeshRenderer _mr;
    MeshCollider _mc;
    Mesh _mesh;
    Floor _boundFloor;
    string _boundID;
    readonly List<Vector2> _lastPoints = new();

    // ====== Multi-floor ======
    class PerFloor
    {
        public GameObject go;
        public MeshFilter mf;
        public MeshRenderer mr;
        public MeshCollider mc;
        public Mesh mesh;
        public readonly List<Vector2> lastPoints = new();
    }
    readonly Dictionary<string, PerFloor> _perFloor = new();

    void Awake()
    {
        if (floorMaterial == null)
        {
            var sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            floorMaterial = new Material(sh);
        }
    }

    void Update()
    {
        if (string.IsNullOrEmpty(floorID))
        {
            // === MULTI-FLOOR MODE ===
            UpdateAllFloors();
        }
        else
        {
            // === SINGLE-FLOOR MODE (giữ hành vi cũ) ===
            UpdateSingleFloor();
        }
    }

    // ===================== MULTI-FLOOR MODE =====================

    void UpdateAllFloors()
    {
        var list = FloorStorage.floors;
        if (list == null) return;

        // Cập nhật / tạo mới tất cả floors hiện có
        for (int i = 0; i < list.Count; i++)
        {
            var f = list[i];
            if (f == null || f.checkpoints == null || f.checkpoints.Count < 3) continue;

            if (!_perFloor.TryGetValue(f.ID, out var pf))
            {
                pf = CreatePerFloorGO(f.ID);
                _perFloor[f.ID] = pf;
            }

            // Build/refresh khi có thay đổi
            if (IsChangedList(f.checkpoints, pf.lastPoints, changeEpsilon))
            {
                RebuildInto(pf, f);
                SnapshotPointsInto(pf.lastPoints, f.checkpoints);
            }
        }

        // Thu dọn các GO của floors đã bị remove
        CleanupStaleFloors();
    }

    PerFloor CreatePerFloorGO(string id)
    {
        var pf = new PerFloor();
        string goName = $"Floor_{id}";

        pf.go = GameObject.Find(goName);
        if (pf.go == null) pf.go = new GameObject(goName);

        pf.go.tag = "RoomFloor";

        pf.mf = pf.go.GetComponent<MeshFilter>();
        if (pf.mf == null) pf.mf = pf.go.AddComponent<MeshFilter>();

        pf.mr = pf.go.GetComponent<MeshRenderer>();
        if (pf.mr == null) pf.mr = pf.go.AddComponent<MeshRenderer>();
        if (pf.mr.sharedMaterial == null) pf.mr.sharedMaterial = floorMaterial;

        pf.mc = pf.go.GetComponent<MeshCollider>();
        if (pf.mc == null) pf.mc = pf.go.AddComponent<MeshCollider>();

        pf.mesh = new Mesh { name = $"FloorMesh_{id}" };
        pf.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        pf.mf.sharedMesh = pf.mesh;

        return pf;
    }

    void RebuildInto(PerFloor pf, Floor f)
    {
        var cps = f.checkpoints;
        int n = cps.Count;

        var verts = new Vector3[n];
        var uvs   = new Vector2[n];

        // bounds 2D để UV
        float minX = cps[0].x, maxX = cps[0].x;
        float minY = cps[0].y, maxY = cps[0].y;
        for (int i = 1; i < n; i++)
        {
            var v = cps[i];
            if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
        }
        float invW = (maxX - minX) > 1e-6f ? 1f / (maxX - minX) : 1f;
        float invH = (maxY - minY) > 1e-6f ? 1f / (maxY - minY) : 1f;

        for (int i = 0; i < n; i++)
        {
            verts[i] = new Vector3(cps[i].x, 0f, cps[i].y);
            uvs[i]   = new Vector2((cps[i].x - minX) * invW, (cps[i].y - minY) * invH);
        }

        int triCountOneSide = (n - 2) * 3;
        int triCount = doubleSided ? triCountOneSide * 2 : triCountOneSide;
        var tris = new int[triCount];
        int t = 0;
        for (int i = 1; i < n - 1; i++) { tris[t++] = 0; tris[t++] = i; tris[t++] = i + 1; }
        if (doubleSided)
            for (int i = 1; i < n - 1; i++) { tris[t++] = 0; tris[t++] = i + 1; tris[t++] = i; }

        pf.mesh.Clear();
        pf.mesh.SetVertices(verts);
        pf.mesh.SetUVs(0, uvs);
        pf.mesh.SetTriangles(tris, 0, true);
        pf.mesh.RecalculateNormals();
        pf.mesh.RecalculateBounds();

        // đồng bộ MeshFilter & MeshCollider
        if (pf.mf.sharedMesh != pf.mesh) pf.mf.sharedMesh = pf.mesh;
        pf.mc.sharedMesh = null;
        pf.mc.sharedMesh = pf.mesh;

        if (pf.mr.sharedMaterial == null) pf.mr.sharedMaterial = floorMaterial;
    }

    void CleanupStaleFloors()
    {
        // Xây set ID còn tồn tại
        var alive = new HashSet<string>();
        for (int i = 0; i < FloorStorage.floors.Count; i++)
        {
            var f = FloorStorage.floors[i];
            if (f != null) alive.Add(f.ID);
        }

        // Thu dọn những cái không còn trong FloorStorage
        var toRemove = new List<string>();
        foreach (var kv in _perFloor)
            if (!alive.Contains(kv.Key)) toRemove.Add(kv.Key);

        foreach (var id in toRemove)
        {
            if (_perFloor.TryGetValue(id, out var pf))
            {
                if (pf.go) Destroy(pf.go);
            }
            _perFloor.Remove(id);
        }
    }

    static bool IsChangedList(List<Vector2> a, List<Vector2> b, float eps)
    {
        if (a == null || a.Count < 3) return true;
        if (b == null) return true;
        if (a.Count != b.Count) return true;
        for (int i = 0; i < a.Count; i++)
        {
            if (Mathf.Abs(a[i].x - b[i].x) > eps || Mathf.Abs(a[i].y - b[i].y) > eps)
                return true;
        }
        return false;
    }

    static void SnapshotPointsInto(List<Vector2> dst, List<Vector2> src)
    {
        dst.Clear();
        if (src == null) return;
        for (int i = 0; i < src.Count; i++) dst.Add(src[i]);
    }

    // ===================== SINGLE-FLOOR MODE =====================

    void UpdateSingleFloor()
    {
        // Bind theo floorID
        var f = FindByID(floorID);
        if (f == null || f.checkpoints == null || f.checkpoints.Count < 3)
        {
            UnbindSingle(clearMesh: true);
            return;
        }

        if (_boundFloor == null || _boundID != f.ID)
        {
            BindToSingle(f);
            EnsureTargetGO_Single();
            RebuildFromFloor_Single(_boundFloor);
            SnapshotPoints_Single(_boundFloor);
            return;
        }

        EnsureTargetGO_Single();

        if (IsChanged_Single(_boundFloor))
        {
            RebuildFromFloor_Single(_boundFloor);
            SnapshotPoints_Single(_boundFloor);
        }
    }

    Floor FindByID(string id)
    {
        if (string.IsNullOrEmpty(id) || FloorStorage.floors == null) return null;
        for (int i = 0; i < FloorStorage.floors.Count; i++)
            if (FloorStorage.floors[i].ID == id) return FloorStorage.floors[i];
        return null;
    }

    void BindToSingle(Floor f)
    {
        _boundFloor = f;
        _boundID = f != null ? f.ID : null;
    }

    void UnbindSingle(bool clearMesh)
    {
        _boundFloor = null;
        _boundID = null;
        _lastPoints.Clear();

        if (clearMesh && _mesh != null)
        {
            _mesh.Clear();
            if (_mf != null) _mf.sharedMesh = _mesh;
            if (_mc != null)
            {
                _mc.sharedMesh = null;
                _mc.sharedMesh = _mesh;
            }
        }
    }

    void EnsureTargetGO_Single()
    {
        if (string.IsNullOrEmpty(_boundID)) return;

        string goName = $"Floor_{_boundID}";
        if (_targetGO == null || _targetGO.name != goName)
        {
            _targetGO = GameObject.Find(goName);
            if (_targetGO == null)
                _targetGO = new GameObject(goName);

            _targetGO.tag = "RoomFloor";
        }

        _mf = _targetGO.GetComponent<MeshFilter>();
        if (_mf == null) _mf = _targetGO.AddComponent<MeshFilter>();

        _mr = _targetGO.GetComponent<MeshRenderer>();
        if (_mr == null) _mr = _targetGO.AddComponent<MeshRenderer>();
        if (_mr.sharedMaterial == null) _mr.sharedMaterial = floorMaterial;

        _mc = _targetGO.GetComponent<MeshCollider>();
        if (_mc == null) _mc = _targetGO.AddComponent<MeshCollider>();

        if (_mesh == null)
        {
            _mesh = new Mesh { name = $"FloorMesh_{_boundID}" };
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        _mf.sharedMesh = _mesh;
    }

    bool IsChanged_Single(Floor f)
    {
        var pts = f.checkpoints;
        if (pts == null || pts.Count < 3) return true;
        if (pts.Count != _lastPoints.Count) return true;

        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = _lastPoints[i];
            if (Mathf.Abs(a.x - b.x) > changeEpsilon || Mathf.Abs(a.y - b.y) > changeEpsilon)
                return true;
        }
        return false;
    }

    void SnapshotPoints_Single(Floor f)
    {
        _lastPoints.Clear();
        if (f.checkpoints == null) return;
        for (int i = 0; i < f.checkpoints.Count; i++)
            _lastPoints.Add(f.checkpoints[i]);
    }

    public void RebuildFromFloor_Single(Floor f)
    {
        if (f == null || f.checkpoints == null || f.checkpoints.Count < 3)
        {
            ClearMesh_Single();
            return;
        }

        EnsureTargetGO_Single();

        var cps = f.checkpoints;
        int n = cps.Count;

        var verts = new Vector3[n];
        var uvs   = new Vector2[n];

        float minX = cps[0].x, maxX = cps[0].x;
        float minY = cps[0].y, maxY = cps[0].y;
        for (int i = 1; i < n; i++)
        {
            var v = cps[i];
            if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
        }
        float invW = (maxX - minX) > 1e-6f ? 1f / (maxX - minX) : 1f;
        float invH = (maxY - minY) > 1e-6f ? 1f / (maxY - minY) : 1f;

        for (int i = 0; i < n; i++)
        {
            verts[i] = new Vector3(cps[i].x, 0f, cps[i].y);
            uvs[i]   = new Vector2((cps[i].x - minX) * invW, (cps[i].y - minY) * invH);
        }

        int triCountOneSide = (n - 2) * 3;
        int triCount = doubleSided ? triCountOneSide * 2 : triCountOneSide;
        var tris = new int[triCount];
        int t = 0;
        for (int i = 1; i < n - 1; i++) { tris[t++] = 0; tris[t++] = i; tris[t++] = i + 1; }
        if (doubleSided)
            for (int i = 1; i < n - 1; i++) { tris[t++] = 0; tris[t++] = i + 1; tris[t++] = i; }

        if (_mesh == null)
        {
            _mesh = new Mesh { name = $"FloorMesh_{_boundID}" };
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetUVs(0, uvs);
        _mesh.SetTriangles(tris, 0, true);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        if (_mf != null && _mf.sharedMesh != _mesh)
            _mf.sharedMesh = _mesh;

        if (_mr != null && _mr.sharedMaterial == null)
            _mr.sharedMaterial = floorMaterial;

        if (_mc != null)
        {
            _mc.sharedMesh = null;
            _mc.sharedMesh = _mesh;
        }
    }

    public void ClearMesh_Single()
    {
        if (_mesh != null)
        {
            _mesh.Clear();
            if (_mf != null) _mf.sharedMesh = _mesh;
            if (_mc != null)
            {
                _mc.sharedMesh = null;
                _mc.sharedMesh = _mesh;
            }
        }
    }
}
