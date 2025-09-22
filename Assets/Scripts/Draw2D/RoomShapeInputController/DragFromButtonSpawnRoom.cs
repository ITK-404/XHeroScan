using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class DragFromButtonSpawnRoom : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Placement params")]
    public float width = 20f;
    public float depth = 5f;
    public LayerMask pickLayer = ~0;      // cho phép ray trúng bất kỳ collider nào
    public float gridSnap = 0.1f;
    public GameObject distanceTextPrefab;

    [Header("Spawn visuals (tự tạo nếu để null)")]
    public GameObject checkpointPrefab;
    public Material lineMaterial;
    public float lineWidth = 0.03f;

    [Header("Render layering (index -> Y)")]
    public int roomIndex = 2;
    public float layerStepY = 0.002f;
    public float roomWallLift = 0.003f;

    [SerializeField] private GameObject ButtomPanel;
    [SerializeField] private bool requireExitPanelToActivate = true;
    [SerializeField] private bool autoSizeByDevice = true;

    // runtime
    private bool isDragging = false;
    private bool dragActivated = false;
    private Vector3 lastHitPos = Vector3.zero;

    // preview
    private GameObject previewGO;
    private LineRenderer previewLR;
    private Material previewMatInstance;

    // labels
    private readonly List<GameObject> edgeLabels = new();

    // cache
    private RectTransform _bottomPanelRect;
    private CheckpointManager checkPointManager;

    private Room _lastCreatedRoom;


    private void Start()
    {
        checkPointManager = FindFirstObjectByType<CheckpointManager>();
    }
    private void OnDestroy()
    {
        if (previewGO) Destroy(previewGO);
        if (previewMatInstance) Destroy(previewMatInstance);
        ClearEdgeLabels();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ApplySizeByDevice();

        isDragging = true;
        dragActivated = !requireExitPanelToActivate || !IsInsideBottomPanel(eventData.position, eventData.pressEventCamera);

        if (dragActivated)
        {
            if (TryScreenToWorld(eventData.position, eventData.pressEventCamera, out var world))
            {
                CreateOrEnsurePreview();
                UpdatePreviewAt(world);
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        if (requireExitPanelToActivate && !dragActivated)
        {
            if (!IsInsideBottomPanel(eventData.position, eventData.pressEventCamera))
            {
                dragActivated = true;
                if (TryScreenToWorld(eventData.position, eventData.pressEventCamera, out var world))
                {
                    CreateOrEnsurePreview();
                    UpdatePreviewAt(world);
                }
            }
            return;
        }

        if (TryScreenToWorld(eventData.position, eventData.pressEventCamera, out var hit))
        {
            CreateOrEnsurePreview();
            UpdatePreviewAt(hit);
        }
        else
        {
            HidePreview();
        }
    }
    
public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        if (dragActivated && TryScreenToWorld(eventData.position, eventData.pressEventCamera, out var hit))
        {
            _lastCreatedRoom = CommitRoomAt(hit);
        }

        isDragging = false;
        dragActivated = false;
        HidePreview();
        ClearEdgeLabels();

        if (_lastCreatedRoom != null)
            CameraResizeByFloor.Instance.Resize(_lastCreatedRoom.checkpoints);
    }

    // ================= Preview =================
    private void CreateOrEnsurePreview()
    {
        if (previewGO != null) { previewGO.SetActive(true); return; }

        previewGO = new GameObject("[Room Preview 20x5]");
        previewLR = previewGO.AddComponent<LineRenderer>();
        previewLR.useWorldSpace = true;
        previewLR.loop = true;
        previewLR.alignment = LineAlignment.View;
        previewLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        previewLR.receiveShadows = false;
        previewLR.numCornerVertices = 2;
        previewLR.widthMultiplier = lineWidth;
        previewLR.positionCount = 5;

        var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        previewMatInstance = new Material(shader);
        if (previewMatInstance.HasProperty("_BaseColor")) previewMatInstance.SetColor("_BaseColor", new Color(0.2f, 1f, 0.2f, 1f));
        else if (previewMatInstance.HasProperty("_Color")) previewMatInstance.SetColor("_Color", new Color(0.2f, 1f, 0.2f, 1f));
        previewLR.material = previewMatInstance;
    }

    private void UpdatePreviewAt(Vector3 world)
    {
        lastHitPos = world;

        float baseY = roomIndex * layerStepY;
        float y = baseY + roomWallLift;

        // snap tâm (XZ)
        Vector3 c = new Vector3(
            Mathf.Round(world.x / gridSnap) * gridSnap,
            y,
            Mathf.Round(world.z / gridSnap) * gridSnap
        );

        float hx = width * 0.5f;
        float hz = depth * 0.5f;

        Vector3 a = new Vector3(c.x - hx, y, c.z - hz);
        Vector3 b = new Vector3(c.x - hx, y, c.z + hz);
        Vector3 d = new Vector3(c.x + hx, y, c.z + hz);
        Vector3 e = new Vector3(c.x + hx, y, c.z - hz);

        if (previewLR)
        {
            previewLR.SetPosition(0, a);
            previewLR.SetPosition(1, b);
            previewLR.SetPosition(2, d);
            previewLR.SetPosition(3, e);
            previewLR.SetPosition(4, a);
        }

        ShowEdgeLengths(a, b, d, e);
    }

    private void HidePreview()
    {
        if (previewGO) previewGO.SetActive(false);
    }

    private void ClearEdgeLabels()
    {
        for (int i = 0; i < edgeLabels.Count; i++)
            if (edgeLabels[i]) Destroy(edgeLabels[i]);
        edgeLabels.Clear();
    }
    
    private Room CommitRoomAt(Vector3 world)
    {
        float baseY = roomIndex * layerStepY;
        float yMesh = baseY;
        float yShow = baseY + roomWallLift;

        Vector3 c = new Vector3(
            Mathf.Round(world.x / gridSnap) * gridSnap,
            yShow,
            Mathf.Round(world.z / gridSnap) * gridSnap
        );

        float hx = width * 0.5f;
        float hz = depth * 0.5f;

        Vector3 v0 = new Vector3(c.x - hx, yMesh, c.z - hz);
        Vector3 v1 = new Vector3(c.x - hx, yMesh, c.z + hz);
        Vector3 v2 = new Vector3(c.x + hx, yMesh, c.z + hz);
        Vector3 v3 = new Vector3(c.x + hx, yMesh, c.z - hz);

        Vector2 l0 = new Vector2(v0.x, v0.z);
        Vector2 l1 = new Vector2(v1.x, v1.z);
        Vector2 l2 = new Vector2(v2.x, v2.z);
        Vector2 l3 = new Vector2(v3.x, v3.z);

        Vector3 s0 = new Vector3(v0.x, yShow, v0.z);
        Vector3 s1 = new Vector3(v1.x, yShow, v1.z);
        Vector3 s2 = new Vector3(v2.x, yShow, v2.z);
        Vector3 s3 = new Vector3(v3.x, yShow, v3.z);

        var room = new Room()
        {
            checkpoints = new List<Vector2> { l0, l1, l2, l3 },
            extraCheckpoints = new List<Vector2>(),
            wallLines = new List<WallLine>
        {
            new WallLine(s0, s1, LineType.Wall),
            new WallLine(s1, s2, LineType.Wall),
            new WallLine(s2, s3, LineType.Wall),
            new WallLine(s3, s0, LineType.Wall),
        }
        };

        room.center = GeoUtil.Centroid(room.checkpoints);

        // GẮN HƯỚNG MẶC ĐỊNH CHO TẤT CẢ WALLLINE (0° = Nam/Z−)
        HeadingManager.UpdateAllWallHeadings(room, 0f);
        // HeadingManager.HeadingDeg(room.checkpoints[0], room.checkpoints[1]);

        RoomStorage.UpdateOrAddRoom(room);

        if (checkPointManager != null)
        {
            checkPointManager.DrawWallLineByRoom(room);
            checkPointManager.CreateRoomMeshCtrl(room);
            checkPointManager.AddGameObjectCheckPointToGlobalVariable(room);
            checkPointManager.RedrawAllRooms();
        }

        UndoRedoController.Instance.AddToUndo(new CreateRoomCommand(room));
        return room;
    }

    private Camera UiCamForPanel
    {
        get
        {
            var canvas = ButtomPanel ? ButtomPanel.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return null;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null
                : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);
        }
    }
    private bool IsInsideBottomPanel(Vector2 screenPos, Camera eventCam = null)
    {
        if (!_bottomPanelRect) _bottomPanelRect = ButtomPanel ? ButtomPanel.GetComponent<RectTransform>() : null;
        if (!_bottomPanelRect) return false;
        var cam = eventCam != null ? eventCam : UiCamForPanel;
        return RectTransformUtility.RectangleContainsScreenPoint(_bottomPanelRect, screenPos, cam);
    }

    private void ApplySizeByDevice()
    {
        if (!autoSizeByDevice) return;

        int w = Screen.width, h = Screen.height;
        int sw = Mathf.Min(w, h), sh = Mathf.Max(w, h);

        if ((sw == 1170 && sh == 2532) || (sw == 2532 && sh == 1170)) { width = 5f; depth = 20f; return; }
        if ((sw == 1812 && sh == 2176) || (sw == 2176 && sh == 1812)) { width = 20f; depth = 5f; return; }

        float tol = 0.04f;
        bool approxPhone = Mathf.Abs(sw - 1170) / 1170f <= tol && Mathf.Abs(sh - 2532) / 2532f <= tol;
        bool approxTablet = Mathf.Abs(sw - 1812) / 1812f <= tol && Mathf.Abs(sh - 2176) / 2176f <= tol;
        if (approxPhone) { width = 5f; depth = 20f; return; }
        if (approxTablet) { width = 20f; depth = 5f; return; }

        float aspect = (float)sh / (float)sw;
        if (aspect >= 1.8f || sw <= 1280)
        {
            width = 5f; depth = 20f;
            CameraResizeByFloor.Instance.isLandscape = false;
        }
        else
        {
            width = 20f; depth = 5f;
            CameraResizeByFloor.Instance.isLandscape = true;
        }
    }

    // Raycast trúng gì cũng được; nếu không trúng thì hạ xuống plane Y=0
    private bool TryScreenToWorld(Vector2 screenPos, Camera evtCam, out Vector3 world)
    {
        world = default;
        var cam = evtCam != null ? evtCam : Camera.main;
        if (cam == null) return false;

        var ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var rh, 5000f, pickLayer))
        {
            world = rh.point;

            return true;
        }

        // fallback: cắt plane Y=0
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float dist))
        {
            world = ray.GetPoint(dist);
            return true;
        }

        return false;
    }

    // ==== label độ dài các cạnh (như bạn đang dùng) ====
    private void ShowEdgeLengths(Vector3 a, Vector3 b, Vector3 d, Vector3 e)
    {
        ClearEdgeLabels();
        if (distanceTextPrefab == null) return;

        const float LABEL_TOWARD_INWARD = -0.5f;
        const float LABEL_LIFT_Y = 0.01f;
        const float LABEL_SHIFT_X = 0.18f;
        const float LABEL_SHIFT_Z = 0.18f;
        const float AXIS_EPS = 1e-5f;

        Vector3 center = (a + b + d + e) * 0.25f;
        (Vector3 p0, Vector3 p1)[] edges = new (Vector3, Vector3)[] { (a,b), (b,d), (d,e), (e,a) };

        for (int i = 0; i < edges.Length; i++)
        {
            var (p0, p1) = edges[i];
            Vector3 mid = (p0 + p1) * 0.5f;

            Vector3 inward = center - mid; inward.y = 0f;
            if (inward.sqrMagnitude < AXIS_EPS) inward = Vector3.forward;
            Vector3 inwardN = inward.normalized;

            Vector3 pos = mid + inwardN * LABEL_TOWARD_INWARD;
            pos.y += LABEL_LIFT_Y;

            float dxEdge = Mathf.Abs(p1.x - p0.x);
            float dzEdge = Mathf.Abs(p1.z - p0.z);

            if (dzEdge > dxEdge)
            {
                float signX = Mathf.Sign(mid.x - center.x);
                if (Mathf.Abs(mid.x - center.x) < AXIS_EPS) signX = (inward.x >= 0f ? 1f : -1f);
                pos.x += signX * LABEL_SHIFT_X;
            }
            else
            {
                float signZ = Mathf.Sign(mid.z - center.z);
                if (Mathf.Abs(mid.z - center.z) < AXIS_EPS) signZ = (inward.z >= 0f ? 1f : -1f);
                pos.z += signZ * LABEL_SHIFT_Z;
            }

            GameObject label = Instantiate(distanceTextPrefab, pos, Quaternion.identity, previewGO ? previewGO.transform : null);
            label.name = $"EdgeLength_{i}";
            edgeLabels.Add(label);

            float len = Vector3.Distance(p0, p1);
            var tmp = label.GetComponent<TMPro.TMP_Text>();
            if (tmp) { tmp.text = $"{len:0.##} m"; tmp.color = Color.red; tmp.fontSize = 5f; tmp.alignment = TMPro.TextAlignmentOptions.Center; }
            else
            {
                var uiText = label.GetComponent<Text>();
                if (uiText) { uiText.text = $"{len:0.##} m"; uiText.color = Color.black; }
            }

            Vector3 tangent = p1 - p0; tangent.y = 0f;
            if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.right;
            tangent.Normalize();

            Quaternion rot = Quaternion.LookRotation(-Vector3.up, -inwardN);
            Vector3 rightNow = Vector3.Cross(inwardN, -Vector3.up).normalized;
            if (Vector3.Dot(rightNow, tangent) < 0f) rot = Quaternion.AngleAxis(180f, inwardN) * rot;
            label.transform.rotation = rot;
        }
    }
}
