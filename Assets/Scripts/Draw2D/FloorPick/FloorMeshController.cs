using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FloorMeshController : MonoBehaviour
{
    [Header("Binding")]
    public string floorID;                  // để trống => tự bind floor cuối cùng trong FloorStorage
    public bool autoBindLastIfEmpty = true; // true: nếu floorID rỗng thì lấy floor cuối
    public float changeEpsilon = 1e-4f;     // ngưỡng phát hiện thay đổi toạ độ

    [Header("Material")]
    public Material floorMaterial;          // để trống => Unlit/Color mặc định

    [Header("Render")]
    public bool doubleSided = true;

    // target holder = GameObject "Floor_<ID>"
    GameObject _targetGO;
    MeshFilter _mf;
    MeshRenderer _mr;
    MeshCollider _mc;
    Mesh _mesh;

    // binding state
    Floor _boundFloor;
    string _boundID;

    // snapshot để phát hiện thay đổi
    readonly List<Vector2> _lastPoints = new List<Vector2>();

    void Awake()
    {
        // Chỉ chuẩn bị material sẵn; việc tạo MF/MR/MC dời sang EnsureTargetGO() khi có ID
        if (floorMaterial == null)
        {
            var sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            floorMaterial = new Material(sh);
            // Nếu muốn màu mặc định:
            // if (floorMaterial.HasProperty("_Color")) floorMaterial.SetColor("_Color", Color.red);
        }
    }

    void Update()
    {
        if (_boundFloor == null)
        {
            TryBind();
            if (_boundFloor == null) return;

            // Đảm bảo có Floor_<ID> + components
            EnsureTargetGO();

            // vừa bind xong → rebuild ngay
            RebuildFromFloor(_boundFloor);
            SnapshotPoints(_boundFloor);
            return;
        }

        if (!StillExists(_boundID))
        {
            Unbind(clearMesh: true);
            return;
        }

        if (IsChanged(_boundFloor))
        {
            EnsureTargetGO(); // nếu target bị xóa ngoài ý muốn
            RebuildFromFloor(_boundFloor);
            SnapshotPoints(_boundFloor);
        }
    }

    //======================== Binding ========================

    void TryBind()
    {
        if (!string.IsNullOrEmpty(floorID))
        {
            var f = FindByID(floorID);
            if (f != null) BindTo(f);
            return;
        }

        if (autoBindLastIfEmpty && FloorStorage.floors.Count > 0)
        {
            BindTo(FloorStorage.floors[FloorStorage.floors.Count - 1]);
        }
    }

    Floor FindByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < FloorStorage.floors.Count; i++)
            if (FloorStorage.floors[i].ID == id) return FloorStorage.floors[i];
        return null;
    }

    bool StillExists(string id)
    {
        for (int i = 0; i < FloorStorage.floors.Count; i++)
            if (FloorStorage.floors[i].ID == id) return true;
        return false;
    }

    void BindTo(Floor f)
    {
        _boundFloor = f;
        _boundID = f != null ? f.ID : null;
        if (f != null && string.IsNullOrEmpty(floorID)) floorID = f.ID; // lưu lại nếu rỗng
    }

    public void BindByID(string id)
    {
        floorID = id;
        _boundFloor = null;   // ép Update() bind lại
        _boundID = null;
    }

    public void Unbind(bool clearMesh)
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

    //=================== Target GameObject ===================

    /// <summary>
    /// Đảm bảo tồn tại GameObject tên "Floor_&lt;ID&gt;" và có đủ MeshFilter/MeshRenderer/MeshCollider.
    /// Gắn tag = "RoomFloor".
    /// </summary>
    void EnsureTargetGO()
    {
        if (string.IsNullOrEmpty(_boundID)) return;

        string goName = $"Floor_{_boundID}";
        if (_targetGO == null || _targetGO.name != goName)
        {
            // Tìm lại nếu bị đổi reference
            _targetGO = GameObject.Find(goName);
            if (_targetGO == null)
            {
                _targetGO = new GameObject(goName);
            }

            // Tag
            _targetGO.tag = "RoomFloor";
        }

        // Đảm bảo components
        _mf = _targetGO.GetComponent<MeshFilter>();
        if (_mf == null) _mf = _targetGO.AddComponent<MeshFilter>();

        _mr = _targetGO.GetComponent<MeshRenderer>();
        if (_mr == null) _mr = _targetGO.AddComponent<MeshRenderer>();

        _mc = _targetGO.GetComponent<MeshCollider>();
        if (_mc == null) _mc = _targetGO.AddComponent<MeshCollider>();

        // Mesh đối tượng
        if (_mesh == null)
        {
            _mesh = new Mesh { name = $"FloorMesh_{_boundID}" };
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        _mf.sharedMesh = _mesh;

        // Material
        if (_mr.sharedMaterial == null) _mr.sharedMaterial = floorMaterial;
    }

    //================== Change detection =====================

    bool IsChanged(Floor f)
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

    void SnapshotPoints(Floor f)
    {
        _lastPoints.Clear();
        if (f.checkpoints == null) return;
        for (int i = 0; i < f.checkpoints.Count; i++)
            _lastPoints.Add(f.checkpoints[i]);
    }

    //====================== Mesh build =======================

    public void RebuildFromFloor(Floor f)
    {
        if (f == null || f.checkpoints == null || f.checkpoints.Count < 3)
        {
            ClearMesh();
            return;
        }

        // Bảo đảm target GO & components sẵn sàng
        EnsureTargetGO();

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

        // fan triangulation (yêu cầu polygon lồi / đỉnh theo thứ tự nhất quán)
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

        // gán vào MeshFilter
        if (_mf != null && _mf.sharedMesh != _mesh)
            _mf.sharedMesh = _mesh;

        // đảm bảo có material
        if (_mr != null && _mr.sharedMaterial == null)
            _mr.sharedMaterial = floorMaterial;

        // đồng bộ MeshCollider
        if (_mc != null)
        {
            _mc.sharedMesh = null;   // clear trước để force update
            _mc.sharedMesh = _mesh;
        }
    }

    public void ClearMesh()
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
