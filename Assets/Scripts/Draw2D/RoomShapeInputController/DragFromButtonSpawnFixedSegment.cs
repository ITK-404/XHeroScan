using UnityEngine;
using UnityEngine.EventSystems;

public class DragFromButtonSpawnFixedSegment_Passthrough: MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Placement")]
    public LayerMask pickLayer;
    public float fixedLength = 3.0f;
    public bool pivotAtCenter = true;
    public float hoverHeight = 0.0f;

    [Header("Snapping (optional)")]
    public bool snapAngles = false;
    public float angleStepDeg = 15f;
    public float gridSnap = 0.0f;

    [Header("Preview")]
    public bool useManagerPreviewLine = true;
    public Color previewColor = Color.green;
    public float previewPointRadius = 0.05f; // chỉ để nhìn cho dễ (không ảnh hưởng dữ liệu)

    [Header("Commit")]
    public LineType lineTypeOnDrop = LineType.Wall;     // kiểu line sẽ dùng khi “giả click”
    public bool restorePreviousLineType = true;         // thả xong trả lại kiểu line cũ

    [Header("Debug")]
    public bool verboseLogs = false;

    // runtime
    Camera _cam;
    CheckpointManager _cm;
    HandleCheckpointManger _hcm;

    bool _dragging, _hasValidPlacement;
    Vector3 _p0, _p1, _center, _lastDir = Vector3.forward;

    GameObject _prevP0Mesh, _prevP1Mesh;
    LineType _oldLineType;

    void Awake()
    {
        _cam = Camera.main;
        _cm  = FindFirstObjectByType<CheckpointManager>();
        _hcm = FindFirstObjectByType<HandleCheckpointManger>();

        if (_cm == null)  Debug.LogError("[DragPassthrough] Missing CheckpointManager in scene.");
        if (_hcm == null) Debug.LogError("[DragPassthrough] Missing HandleCheckpointManger in scene.");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragging = true;
        _hasValidPlacement = false;

        // lưu kiểu line hiện tại rồi set kiểu line mong muốn
        if (_cm != null)
        {
            _oldLineType = _cm.currentLineType;
            _cm.currentLineType = lineTypeOnDrop;
        }

        // tạo 2 sphere preview (chỉ visual)
        _prevP0Mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _prevP1Mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foreach (var go in new[] { _prevP0Mesh, _prevP1Mesh })
        {
            go.name = go == _prevP0Mesh ? "PreviewP0" : "PreviewP1";
            go.transform.localScale = Vector3.one * (previewPointRadius * 2f);
            var col = go.GetComponent<Collider>(); if (col) Destroy(col);
            var r   = go.GetComponent<Renderer>();
            if (r) { var m = new Material(Shader.Find("Sprites/Default")); m.color = previewColor; r.material = m; }
        }

        UpdatePlacement(eventData.position);
        if (verboseLogs) Debug.Log("[DragPassthrough] Begin drag.");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        UpdatePlacement(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        UpdatePlacement(eventData.position); // cập nhật lần cuối

        // clear preview visuals
        if (useManagerPreviewLine && _cm?.DrawingTool != null)
            _cm.DrawingTool.ClearPreviewLine();
        if (_prevP0Mesh) Destroy(_prevP0Mesh);
        if (_prevP1Mesh) Destroy(_prevP1Mesh);

        if (!_hasValidPlacement || _cm == null || _hcm == null)
        {
            if (restorePreviousLineType && _cm != null) _cm.currentLineType = _oldLineType;
            _dragging = false;
            return;
        }

        // ===== CHÍNH: GIẢ LẬP 2 LẦN CLICK QUA PIPELINE CŨ =====
        // Lần 1: click P1 -> HandleSingleWallPlacement sẽ snap & spawn firstPoint
        _hcm.HandleSingleWallPlacement(_p0);
        // Lần 2: click P2 -> chạy toàn bộ logic A/B, extra, split, redraw...
        _hcm.HandleSingleWallPlacement(_p1);

        if (restorePreviousLineType) _cm.currentLineType = _oldLineType;

        if (verboseLogs) Debug.Log($"[DragPassthrough] Dropped: P0={_p0}, P1={_p1}, L={(_p1-_p0).magnitude:F3}m");
        _dragging = false;
    }

    // --- helpers ---
    void UpdatePlacement(Vector2 screenPos)
    {
        if (!TryRaycastOnFloor(screenPos, out var hit))
        {
            _hasValidPlacement = false;
            return;
        }
        _hasValidPlacement = true;

        // center + grid snap
        _center = hit.point; _center.y += hoverHeight;
        if (gridSnap > 0f)
        {
            _center.x = Mathf.Round(_center.x / gridSnap) * gridSnap;
            _center.z = Mathf.Round(_center.z / gridSnap) * gridSnap;
        }

        // hướng trên XZ
        var dir = (hit.point - _center); dir.y = 0f;
        if (dir.sqrMagnitude > 1e-5f) _lastDir = dir.normalized;

        if (snapAngles)
        {
            float yaw = Mathf.Atan2(_lastDir.x, _lastDir.z) * Mathf.Rad2Deg;
            yaw = Mathf.Round(yaw / angleStepDeg) * angleStepDeg;
            _lastDir = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad)).normalized;
        }

        // tính 2 đầu mút cố định length
        float L = Mathf.Max(0f, fixedLength);
        if (pivotAtCenter)
        {
            _p0 = _center - _lastDir * (0.5f * L);
            _p1 = _center + _lastDir * (0.5f * L);
        }
        else
        {
            _p0 = _center;
            _p1 = _center + _lastDir * L;
        }

        // update preview points
        if (_prevP0Mesh) _prevP0Mesh.transform.position = _p0;
        if (_prevP1Mesh) _prevP1Mesh.transform.position = _p1;

        // preview line qua DrawingTool (không persist)
        if (useManagerPreviewLine && _cm?.DrawingTool != null)
        {
            _cm.DrawingTool.ClearPreviewLine();
            _cm.DrawingTool.DrawPreviewLine(_p0, _p1);
        }
    }

    bool TryRaycastOnFloor(Vector2 screenPos, out RaycastHit hit)
    {
        if (_cam == null) _cam = Camera.main;
        hit = default; if (_cam == null) return false;

        Ray ray = _cam.ScreenPointToRay(screenPos);

        // Ưu tiên collider nền (pickLayer)
        if (Physics.Raycast(ray, out hit, 1000f, pickLayer, QueryTriggerInteraction.Ignore))
            return true;

        // Fallback: mặt phẳng y=0
        const float hY = 0f;
        var ground = new Plane(Vector3.up, new Vector3(0f, hY, 0f));
        if (ground.Raycast(ray, out float enter))
        {
            Vector3 p = ray.GetPoint(enter);
            hit = new RaycastHit { point = new Vector3(p.x, hY, p.z), distance = enter };
            return true;
        }
        return false;
    }
}
