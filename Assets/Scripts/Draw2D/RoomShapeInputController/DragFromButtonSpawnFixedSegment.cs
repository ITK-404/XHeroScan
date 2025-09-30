using UnityEngine;
using UnityEngine.EventSystems;

public class DragFromButtonSpawnFixedSegment_Passthrough : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Placement")]
    public LayerMask pickLayer;
    public float fixedLength = 3f;
    public bool pivotAtCenter = true;
    public float hoverHeight = 0f;

    [Header("Snapping")]
    public bool snapAngles = false;
    public float angleStepDeg = 15f;
    public float gridSnap = 0f;

    [Header("Preview")]
    public bool useManagerPreviewLine = true;
    public Color previewColor = Color.green;
    public float previewPointRadius = 0.05f;

    [Header("Commit")]
    public LineType lineTypeOnDrop = LineType.Wall;
    public bool restorePreviousLineType = true;

    [Header("Debug")]
    public bool verboseLogs = false;

    [SerializeField] private GameObject ButtomPanel;

    Camera _cam;
    CheckpointManager _cm;
    HandleCheckpointManger _hcm;

    bool _dragging, _hasValidPlacement, _armed, _changedType;
    Vector3 _p0, _p1, _center, _lastDir = Vector3.forward;

    GameObject _prevP0Mesh, _prevP1Mesh;
    LineType _oldLineType;
    RectTransform _bottomRect;

    Camera UiCamForPanel
    {
        get
        {
            var canvas = ButtomPanel ? ButtomPanel.GetComponentInParent<Canvas>() : null;
            if (!canvas || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas.worldCamera ? canvas.worldCamera : Camera.main;
        }
    }

    bool IsInsideBottomPanel(Vector2 screenPos)
    {
        if (!_bottomRect) _bottomRect = ButtomPanel ? ButtomPanel.GetComponent<RectTransform>() : null;
        return _bottomRect && RectTransformUtility.RectangleContainsScreenPoint(_bottomRect, screenPos, UiCamForPanel);
    }

    void Awake()
    {
        _cam = Camera.main;
        _cm  = FindFirstObjectByType<CheckpointManager>();
        _hcm = FindFirstObjectByType<HandleCheckpointManger>();
        if (!_cm)  Debug.LogError("[DragPassthrough] Missing CheckpointManager.");
        if (!_hcm) Debug.LogError("[DragPassthrough] Missing HandleCheckpointManger.");
        if (ButtomPanel) _bottomRect = ButtomPanel.GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragging = true; _hasValidPlacement = false; _armed = false; _changedType = false;
        if (_cm) _oldLineType = _cm.currentLineType;

        if (!IsInsideBottomPanel(eventData.position))
        {
            Arm();
            UpdatePlacement(eventData.position);
        }
        if (verboseLogs) Debug.Log("[DragPassthrough] Begin.");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        if (!_armed && !IsInsideBottomPanel(eventData.position)) Arm();
        if (_armed) UpdatePlacement(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        // Hủy kéo: đảm bảo không còn firstPoint treo
        if (!_armed || IsInsideBottomPanel(eventData.position))
        {
            _hcm?.StopPlacing();
            CleanupAndRestore();
            _dragging = false;
            if (verboseLogs) Debug.Log("[DragPassthrough] Cancelled (inside panel/not armed).");
            return;
        }

        UpdatePlacement(eventData.position);

        if (_hasValidPlacement && _cm && _hcm)
        {
            // bật đặt point cho lượt này
            _hcm.ResumePlacing();

            // đúng 2 point: p0 -> p1
            _hcm.HandleSingleWallPlacement(_p0);
            _hcm.HandleSingleWallPlacement(_p1);

            // tắt ngay để không chain p1->p2...
            _hcm.StopPlacing();

            if (verboseLogs) Debug.Log($"[DragPassthrough] Dropped: {_p0} -> {_p1}");
        }

        CleanupAndRestore();
        _dragging = false;
    }

    void Arm()
    {
        _armed = true;
        if (_cm) { _cm.currentLineType = lineTypeOnDrop; _changedType = true; }

        // bật đặt point cho phiên kéo này (phòng trường hợp flagOff đang true)
        _hcm?.ResumePlacing();    

        _prevP0Mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _prevP1Mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foreach (var go in new[] { _prevP0Mesh, _prevP1Mesh })
        {
            go.transform.localScale = Vector3.one * (previewPointRadius * 2f);
            var col = go.GetComponent<Collider>(); if (col) Destroy(col);
            var r = go.GetComponent<Renderer>();
            if (r) { var m = new Material(Shader.Find("Sprites/Default")); m.color = previewColor; r.material = m; }
        }
        if (verboseLogs) Debug.Log("[DragPassthrough] Armed.");
    }

    void CleanupAndRestore()
    {
        if (useManagerPreviewLine && _cm?.DrawingTool) _cm.DrawingTool.ClearPreviewLine();
        if (_prevP0Mesh) Destroy(_prevP0Mesh);
        if (_prevP1Mesh) Destroy(_prevP1Mesh);
        _prevP0Mesh = _prevP1Mesh = null;
        if (_changedType && restorePreviousLineType && _cm) _cm.currentLineType = _oldLineType;
        _changedType = false;
    }

    void UpdatePlacement(Vector2 screenPos)
    {
        if (!_armed) return;

        if (!TryRaycastOnFloor(screenPos, out var hit))
        {
            _hasValidPlacement = false; return;
        }
        _hasValidPlacement = true;

        _center = hit.point; _center.y += hoverHeight;
        if (gridSnap > 0f)
        {
            _center.x = Mathf.Round(_center.x / gridSnap) * gridSnap;
            _center.z = Mathf.Round(_center.z / gridSnap) * gridSnap;
        }

        var dir = hit.point - _center; dir.y = 0f;
        if (dir.sqrMagnitude > 1e-5f) _lastDir = dir.normalized;

        if (snapAngles)
        {
            float yaw = Mathf.Atan2(_lastDir.x, _lastDir.z) * Mathf.Rad2Deg;
            yaw = Mathf.Round(yaw / angleStepDeg) * angleStepDeg;
            _lastDir = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad)).normalized;
        }

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

        if (_prevP0Mesh) _prevP0Mesh.transform.position = _p0;
        if (_prevP1Mesh) _prevP1Mesh.transform.position = _p1;

        if (useManagerPreviewLine && _cm?.DrawingTool != null)
        {
            _cm.DrawingTool.ClearPreviewLine();
            _cm.DrawingTool.DrawPreviewLine(_p0, _p1);
        }
    }

    bool TryRaycastOnFloor(Vector2 screenPos, out RaycastHit hit)
    {
        if (!_cam) _cam = Camera.main;
        hit = default; if (!_cam) return false;

        var ray = _cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out hit, 1000f, pickLayer, QueryTriggerInteraction.Ignore)) return true;

        const float hY = 0f;
        var ground = new Plane(Vector3.up, new Vector3(0f, hY, 0f));
        if (ground.Raycast(ray, out float enter))
        {
            var p = ray.GetPoint(enter);
            hit = new RaycastHit { point = new Vector3(p.x, hY, p.z), distance = enter };
            return true;
        }
        return false;
    }
}
