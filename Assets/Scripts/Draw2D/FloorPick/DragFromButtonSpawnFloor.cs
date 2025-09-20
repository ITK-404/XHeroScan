using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class DragFromButtonSpawnFloor : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public static DragFromButtonSpawnFloor Instance;
    [Header("Placement params")]
    public float width = 20f;
    public float depth = 5f;
    public LayerMask pickLayer;
    public float gridSnap = 0.1f;
    public GameObject distanceTextPrefab;

    [Header("Spawn visuals (tự tạo nếu để null)")]
    public GameObject checkpointPrefab;
    public Material lineMaterial;
    public float lineWidth = 0.03f;

    [Header("Render layering (index -> Y)")]
    public int floorIndex = 1;       // floor = 1
    public float layerStepY = 0.002f; // mỗi index lệch 2mm
    public float lineLift = 0.0005f; // line nổi hơn mesh 0.5mm
    public float labelLift = 0.01f;   // label nổi hơn mesh 1cm
    
[SerializeField] private GameObject ButtomPanel;
[SerializeField] private bool requireExitPanelToActivate = true;

    private bool isDragging = false;
    private bool dragActivated = false;
    private float yaw = 0f;
    private Vector3 lastHitPos = Vector3.zero;

    // --- Preview state ---
    private GameObject previewGO;
    private LineRenderer previewLR;
    private MeshFilter previewMF;
    private MeshRenderer previewMR;
    private Mesh previewMesh;

    // floor vừa place (parent container)
    private GameObject lastFloorGO;

    // Labels cạnh
    private readonly List<GameObject> edgeLabels = new();

    // ---- State chỉnh sửa sau khi thả ----
    private bool hasRect = false;
    private Vector3 rectCenter;
    private float rectYaw;
    private float rectHalfW, rectHalfD;

    // chọn handle để move
    private int activeIndex = -1;
    private bool activeIsCorner = false;
    private bool isMovingHandle = false;

    // 4 điểm góc và 4 điểm giữa (handle)
    private GameObject[] cornerHandles = new GameObject[4]; // A,B,D,E
    private GameObject[] edgeHandles = new GameObject[4]; // AB,BD,DE,EA

    private string currentFloorId;
    [SerializeField]private bool autoSizeByDevice = true; // bật tắt chế độ tìm theo màn hình.

    // đánh dấu handle
    private class HandleTag : MonoBehaviour
    {
        public int index;    // 0..3
        public bool isCorner;
    }

    [Header("Flow")]
    public bool editAfterPlace = true;   // false: vẽ xong là dọn sạch

    private Floor previousFloor;

    private float BaseY(int index) => index * layerStepY;
    // Mỗi floor một GO chứa preview/handles/labels
    private static readonly Dictionary<string, GameObject> s_floorVisuals = new();

    private RectTransform _bottomPanelRect;
    private Camera UiCamForPanel {
        get {
            var canvas = ButtomPanel ? ButtomPanel.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return null; // overlay => trả null là đúng
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null
                : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);
        }
    }

    private bool IsInsideBottomPanel(Vector2 screenPos, Camera eventCam = null)
    {
        if (!_bottomPanelRect) _bottomPanelRect = ButtomPanel ? ButtomPanel.GetComponent<RectTransform>() : null;
        if (!_bottomPanelRect) return false;
        // Ưu tiên camera từ event data nếu có, fallback UiCamForPanel
        var cam = eventCam != null ? eventCam : UiCamForPanel;
        return RectTransformUtility.RectangleContainsScreenPoint(_bottomPanelRect, screenPos, cam);
    }

    private void Awake()
    {
        Instance = this;
        // Camera tối thiểu
        if (Camera.main == null)
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Color;
            cam.backgroundColor = Color.white;
            cam.transform.position = new Vector3(0, 15, -15);
            cam.transform.rotation = Quaternion.Euler(45, 0, 0);
            camGO.AddComponent<AudioListener>();
        }
        else
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Color.white;
        }

        // Pick layer
        if (pickLayer.value == 0)
        {
            int floorPickLayer = LayerMask.NameToLayer("FloorPick");
            pickLayer = (floorPickLayer != -1) ? (1 << floorPickLayer) : Physics.DefaultRaycastLayers;
        }

        // Line material mặc định
        if (lineMaterial == null)
        {
            var sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            lineMaterial = new Material(sh);
            if (sh != null && sh.name == "Unlit/Color")
                lineMaterial.SetColor("_Color", new Color(0.1f, 0.1f, 0.1f, 1f));
        }

        // Checkpoint prefab mặc định (giữ collider để click)
        if (checkpointPrefab == null)
        {
            var tpl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tpl.name = "CheckpointPrefab(AutoTemplate)";
            tpl.transform.localScale = Vector3.one * 0.2f;
            tpl.SetActive(false);
            tpl.hideFlags = HideFlags.HideAndDontSave;
            checkpointPrefab = tpl;
        }

        // PlacementManager + floorMat
        if (PlacementManager.Instance == null)
        {
            var go = new GameObject("PlacementManager");
            var pm = go.AddComponent<PlacementManager>();
            var floorMat = new Material(Shader.Find("Standard"));
            floorMat.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            pm.floorMat = floorMat;
        }
        else if (PlacementManager.Instance.floorMat == null)
        {
            var floorMat = new Material(Shader.Find("Standard"));
            floorMat.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            PlacementManager.Instance.floorMat = floorMat;
        }
    }

    private void OnDestroy()
    {
        CleanupAllVisuals();
        PlacementManager.Instance?.DestroyAllFloors();
        // FloorStorage.floors.Clear();
        InteractionFlags.IsFloorHandleDragging = false;
    }

    private void Update()
    {
        // bắt chọn handle
        if (!isMovingHandle && Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            // TryPickHandleUnderMouse();
        }

        if (!isMovingHandle || !hasRect) return;

        // KÉO HANDLE
        if (!Input.GetMouseButton(0))
        {
            isMovingHandle = false;
            activeIndex = -1;
            InteractionFlags.IsFloorHandleDragging = false;
            return;
        }

        if (!TryMouseOnGround(out Vector3 p)) return;

        Quaternion inv = Quaternion.Euler(0f, -rectYaw, 0f);
        Vector3 local = inv * (p - rectCenter);
        float minHalf = 0.05f;

        if (activeIsCorner)
        {
            rectHalfW = Mathf.Max(minHalf, Mathf.Abs(local.x));
            rectHalfD = Mathf.Max(minHalf, Mathf.Abs(local.z));
        }
        else
        {
            switch (activeIndex) // 0:AB, 1:BD, 2:DE, 3:EA
            {
                case 0:
                case 2: rectHalfD = Mathf.Max(minHalf, Mathf.Abs(local.z)); break;
                case 1:
                case 3: rectHalfW = Mathf.Max(minHalf, Mathf.Abs(local.x)); break;
            }
        }

        width = rectHalfW * 2f;
        depth = rectHalfD * 2f;

        RedrawRectangleFromState();
        SyncLastFloorDataToCurrentRect();
    }

    private void ApplySizeByDevice()
    {
        if (!autoSizeByDevice) return;

        int w = Screen.width;
        int h = Screen.height;

        // chuẩn hóa: sw = cạnh ngắn, sh = cạnh dài
        int sw = Mathf.Min(w, h);
        int sh = Mathf.Max(w, h);

        // match tuyệt đối
        if ((sw == 1170 && sh == 2532) || (sw == 2532 && sh == 1170))
        {
            width = 5f; depth = 20f; return;
        }
        if ((sw == 1812 && sh == 2176) || (sw == 2176 && sh == 1812))
        {
            width = 20f; depth = 5f; return;
        }

        // match xấp xỉ (±4%)
        float tol = 0.04f;
        bool approxPhone1170x2532 =
            Mathf.Abs(sw - 1170) / 1170f <= tol && Mathf.Abs(sh - 2532) / 2532f <= tol;
        bool approxTablet2176x1812 =
            Mathf.Abs(sw - 1812) / 1812f <= tol && Mathf.Abs(sh - 2176) / 2176f <= tol;

        if (approxPhone1170x2532)
        {
            width = 5f; depth = 20f; return;
        }
        if (approxTablet2176x1812)
        {
            width = 20f; depth = 5f; return;
        }

        // Fallback tổng quát:
        // - Màn rất “cao” (aspect >= 1.8) hoặc cạnh ngắn <= 1280 => coi như phone: 5x20
        // - Còn lại coi như tablet/large: 20x5
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        ApplySizeByDevice();
        // store previous
        previousFloor = FloorStorage.floors.Count > 0 ? FloorStorage.floors[0] : null;

        ResetSingleFloor();

        isDragging = true;
        dragActivated = !requireExitPanelToActivate;
        yaw = 0f;
        lastHitPos = Vector3.zero;

        if (dragActivated)
        {
            // Giữ nguyên phần bạn đang làm ở OnBeginDrag cũ:
            ResetSingleFloor();
            ClearHandles();
            hasRect = false;

            // tạo previewGO, previewLR, previewMF, previewMR...
            EnsurePreviewObjects();
            previewGO.SetActive(true);
        }

        ClearHandles();
        hasRect = false;

        if (previewGO == null)
        {
            // previewGO = new GameObject("FloorPreview");
            previewGO.hideFlags = HideFlags.DontSave;

            previewLR = previewGO.AddComponent<LineRenderer>();
            previewLR.positionCount = 5;
            previewLR.loop = false;
            previewLR.widthMultiplier = lineWidth;
            previewLR.material = lineMaterial;
            previewLR.useWorldSpace = true;
            previewLR.numCornerVertices = 4;
            // sắp xếp render theo index (LineRenderer hỗ trợ)
            previewLR.sortingOrder = floorIndex;

            previewMF = previewGO.AddComponent<MeshFilter>();
            previewMR = previewGO.AddComponent<MeshRenderer>();
            previewMesh = new Mesh { name = "FloorPreviewMesh" };
            previewMF.sharedMesh = previewMesh;

            var fillMat = new Material(Shader.Find("Standard"));
            fillMat.SetFloat("_Mode", 3);
            fillMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fillMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            fillMat.SetInt("_ZWrite", 0);
            fillMat.DisableKeyword("_ALPHATEST_ON");
            fillMat.EnableKeyword("_ALPHABLEND_ON");
            fillMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            fillMat.renderQueue = 3000;
            fillMat.color = new Color(0.2f, 0.6f, 1f, 0.15f);
            previewMR.sharedMaterial = fillMat;
            // (MeshRenderer sortingOrder có tác dụng cho transparent)
            previewMR.sortingOrder = floorIndex;
        }

        previewGO.SetActive(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        if (!dragActivated && requireExitPanelToActivate)
        {
            // Còn đang ở trong panel => chưa cho kéo thật
            if (IsInsideBottomPanel(eventData.position, eventData.pressEventCamera))
                return;

            // Vừa rời panel => kích hoạt kéo thật
            dragActivated = true;

            ResetSingleFloor();
            ClearHandles();
            hasRect = false;

            EnsurePreviewObjects();
            previewGO.SetActive(true);
        }

        // Nếu tới đây mà vẫn chưa activated (do option), thôi không xử lý
        if (!dragActivated) return;

        var cam = Camera.main; if (cam == null) return;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        int mask = pickLayer.value == 0 ? Physics.DefaultRaycastLayers : pickLayer.value;

        Vector3 p;
        if (Physics.Raycast(ray, out var hit, 3000f, mask)) p = hit.point;
        else
        {
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float enter)) return;
            p = ray.GetPoint(enter);
        }

        if (gridSnap > 0f)
        {
            p.x = Mathf.Round(p.x / gridSnap) * gridSnap;
            p.z = Mathf.Round(p.z / gridSnap) * gridSnap;
        }
        lastHitPos = p;

        if (Input.GetKeyDown(KeyCode.Q)) yaw -= 15f;
        if (Input.GetKeyDown(KeyCode.E)) yaw += 15f;

        if (previewGO != null)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;

            float baseY = BaseY(floorIndex);
            Vector3 c = lastHitPos; c.y = baseY + lineLift;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

            Vector3 a = c + rot * new Vector3(-hw, 0, -hd);
            Vector3 b = c + rot * new Vector3(hw, 0, -hd);
            Vector3 d = c + rot * new Vector3(hw, 0, hd);
            Vector3 e = c + rot * new Vector3(-hw, 0, hd);

            previewLR.SetPosition(0, a);
            previewLR.SetPosition(1, b);
            previewLR.SetPosition(2, d);
            previewLR.SetPosition(3, e);
            previewLR.SetPosition(4, a);

            if (previewMesh == null) previewMesh = new Mesh();
            previewMesh.Clear();
            previewMesh.vertices = new Vector3[] { a, b, d, e };
            previewMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            previewMesh.uv = new Vector2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
            previewMesh.RecalculateNormals();
            previewMesh.RecalculateBounds();
            previewMF.sharedMesh = previewMesh;

            ShowEdgeLengths(a, b, d, e);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        if (!dragActivated && requireExitPanelToActivate)
        {
            isDragging = false;
            
            if (previewGO) previewGO.SetActive(false);
            return;
        }

        if (lastHitPos != Vector3.zero)
        {
            Debug.Log("Trước khi tạo" + FloorStorage.floors.Count);

            float hw = width * 0.5f;
            float hd = depth * 0.5f;

            float baseY = BaseY(floorIndex);
            Vector3 c = lastHitPos; c.y = baseY; // dữ liệu floor nằm trên tầng của floor
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

            Vector3 a = c + rot * new Vector3(-hw, 0, -hd);
            Vector3 b = c + rot * new Vector3(hw, 0, -hd);
            Vector3 d = c + rot * new Vector3(hw, 0, hd);
            Vector3 e = c + rot * new Vector3(-hw, 0, hd);

            // Lưu state để edit
            rectCenter = c;
            rectYaw = yaw;
            rectHalfW = hw;
            rectHalfD = hd;
            hasRect = true;

            var floor = new Floor();

            floor.width = width;
            floor.length = depth;

            floor.checkpoints.Add(new Vector2(a.x, a.z));
            floor.checkpoints.Add(new Vector2(b.x, b.z));
            floor.checkpoints.Add(new Vector2(d.x, d.z));
            floor.checkpoints.Add(new Vector2(e.x, e.z));

            floor.floorLine.Add(new FloorLine(floor.checkpoints[0], floor.checkpoints[1]));
            floor.floorLine.Add(new FloorLine(floor.checkpoints[1], floor.checkpoints[2]));
            floor.floorLine.Add(new FloorLine(floor.checkpoints[2], floor.checkpoints[3]));
            floor.floorLine.Add(new FloorLine(floor.checkpoints[3], floor.checkpoints[0]));

            for (int i = 0; i < 4; i++) floor.heights.Add(0.1f);

            floor.center = GeoUtil.Centroid(floor.checkpoints);
            FloorStorage.floors.Add(floor);
            FloorStorage.UpdateOrAddFloor(floor);
            currentFloorId = floor.ID;

            string id = floor.ID; // read-only, đã được Floor tự gán từ constructor / storage

            // Tạo parent mới cho floor này
            // lastFloorGO = new GameObject($"Floor_{(string.IsNullOrEmpty(id) ? "NoID" : id)}");
            lastFloorGO = new GameObject($"FloorVis_{(string.IsNullOrEmpty(id) ? "NoID" : id)}");

            // Lưu vào registry nếu có id
            if (!string.IsNullOrEmpty(id))
                s_floorVisuals[id] = lastFloorGO;

            // Đưa preview vào parent
            if (previewGO != null)
            {
                previewGO.transform.SetParent(lastFloorGO.transform, true);
                previewGO.SetActive(true);

                // đảm bảo preview hiển thị đúng tầng
                if (previewLR) previewLR.sortingOrder = floorIndex;
                if (previewMR) previewMR.sortingOrder = floorIndex;
            }

            // Spawn handle vào parent
            SpawnHandles(a, b, d, e, lastFloorGO.transform);

            // Vẽ lại từ state (sẽ cập nhật mesh/line/label vị trí)
            RedrawRectangleFromState();

            CameraResizeByFloor.Instance.Resize(floor.checkpoints);

        }
        isDragging = false;

        if (!editAfterPlace)
        {
            // vẽ xong là dọn sạch
            if (lastFloorGO) { PlacementManager.Instance.DestroyFloor(lastFloorGO); lastFloorGO = null; }
            CleanupAllVisuals();
        }
        else
        {
            if (previewGO) previewGO.SetActive(true);
        }

        UndoRedoController.Instance.AddToUndo(new EditFloorCommand(previousFloor));


        Debug.Log("Trước khi tạo" + FloorStorage.floors.Count);
        Debug.Log("Draggin xong, tạo sàn hoàn tấc");
    }

    // ==== Hiển thị độ dài cạnh ====
    private void ShowEdgeLengths(Vector3 a, Vector3 b, Vector3 d, Vector3 e)
    {
        for (int i = 0; i < edgeLabels.Count; i++)
            if (edgeLabels[i]) Destroy(edgeLabels[i]);
        edgeLabels.Clear();

        if (distanceTextPrefab == null) return;

        const float LABEL_TOWARD_INWARD = -0.5f; // đẩy theo hướng "vào tâm"
        const float LABEL_LIFT_Y = 0.01f; // nâng khỏi sàn
        const float LABEL_SHIFT_X = 0.18f; // lệch hai bên cho cạnh dọc
        const float LABEL_SHIFT_Z = 0.18f; // lệch trên/dưới cho cạnh ngang
        const float AXIS_EPS = 1e-5f; // ngưỡng so sánh

        // --- tâm hình chữ nhật ---
        Vector3 center = (a + b + d + e) * 0.25f;

        (Vector3 p0, Vector3 p1)[] edges = new (Vector3, Vector3)[]
        {
        (a, b), (b, d), (d, e), (e, a)
        };

        for (int i = 0; i < edges.Length; i++)
        {
            var (p0, p1) = edges[i];

            // midpoint cạnh
            Vector3 mid = (p0 + p1) * 0.5f;

            Vector3 inward = center - mid;
            inward.y = 0f;
            if (inward.sqrMagnitude < AXIS_EPS) inward = Vector3.forward;
            Vector3 inwardN = inward.normalized;

            Vector3 pos = mid + inwardN * LABEL_TOWARD_INWARD;
            pos.y += LABEL_LIFT_Y;
            
            float dxEdge = Mathf.Abs(p1.x - p0.x);
            float dzEdge = Mathf.Abs(p1.z - p0.z);

            if (dzEdge > dxEdge) // trục Z
            {
                float signX = Mathf.Sign(mid.x - center.x);
                if (Mathf.Abs(mid.x - center.x) < AXIS_EPS)
                    signX = (inward.x >= 0f ? 1f : -1f);
                pos.x += signX * LABEL_SHIFT_X;
            }
            else // trục X
            {
                float signZ = Mathf.Sign(mid.z - center.z);
                if (Mathf.Abs(mid.z - center.z) < AXIS_EPS)
                    signZ = (inward.z >= 0f ? 1f : -1f);
                pos.z += signZ * LABEL_SHIFT_Z;
            }
            Transform parentTf = lastFloorGO != null
                ? lastFloorGO.transform
                : (previewGO != null ? previewGO.transform : null);

            GameObject label = Instantiate(distanceTextPrefab, pos, Quaternion.identity, parentTf);
            label.name = $"EdgeLength_{i}";
            edgeLabels.Add(label);

            // text
            float len = Vector3.Distance(p0, p1);
            string text = $"{len:0.##} m";

            var tmp = label.GetComponent<TMPro.TMP_Text>();
            if (tmp != null)
            {
                tmp.text = text;
                tmp.color = Color.red;
                tmp.fontSize = 5f;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
            }
            else
            {
                var uiText = label.GetComponent<UnityEngine.UI.Text>();
                if (uiText != null)
                {
                    uiText.text = text;
                    uiText.color = Color.black;
                }
            }

            Vector3 tangent = p1 - p0;
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.right;
            tangent.Normalize();

            Quaternion rot = Quaternion.LookRotation(-Vector3.up, -inwardN);

            Vector3 rightNow = Vector3.Cross(inwardN, -Vector3.up).normalized;

            if (Vector3.Dot(rightNow, tangent) < 0f)
            {
                rot = Quaternion.AngleAxis(180f, inwardN) * rot;
            }

            label.transform.rotation = rot;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // TryPickHandleUnderMouse();
    }

    private void TryPickHandleUnderMouse()
    {
        var cam = Camera.main; if (!cam) return;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        var hits = Physics.RaycastAll(ray, 3000f, Physics.DefaultRaycastLayers);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            var tag = h.collider ? h.collider.GetComponent<HandleTag>() : null;
            if (tag != null)
            {
                activeIndex = tag.index;
                activeIsCorner = tag.isCorner;
                isMovingHandle = true;

                // báo: đang kéo point floor
                InteractionFlags.IsFloorHandleDragging = true;
                return;
            }
        }
    }

    private bool TryMouseOnGround(out Vector3 point)
    {
        var cam = Camera.main; point = default;
        if (!cam) return false;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        int mask = pickLayer.value == 0 ? Physics.DefaultRaycastLayers : pickLayer.value;

        if (Physics.Raycast(ray, out var hit, 3000f, mask)) { point = hit.point; return true; }
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter)) { point = ray.GetPoint(enter); return true; }
        return false;
    }

    private void SyncLastFloorDataToCurrentRect()
    {
        if (!hasRect || string.IsNullOrEmpty(currentFloorId)) return;

        Floor floor = null;
        if (FloorStorage.floors != null)
        {
            foreach (var f in FloorStorage.floors)
                if (f != null && f.ID == currentFloorId) { floor = f; break; }
        }
        if (floor == null) return;

        Quaternion rot = Quaternion.Euler(0f, rectYaw, 0f);
        Vector3 c = rectCenter;

        Vector3 a = c + rot * new Vector3(-rectHalfW, 0, -rectHalfD);
        Vector3 b = c + rot * new Vector3(rectHalfW, 0, -rectHalfD);
        Vector3 d = c + rot * new Vector3(rectHalfW, 0, rectHalfD);
        Vector3 e = c + rot * new Vector3(-rectHalfW, 0, rectHalfD);

        floor.checkpoints.Clear();
        floor.checkpoints.Add(new Vector2(a.x, a.z));
        floor.checkpoints.Add(new Vector2(b.x, b.z));
        floor.checkpoints.Add(new Vector2(d.x, d.z));
        floor.checkpoints.Add(new Vector2(e.x, e.z));

        floor.floorLine.Clear();
        floor.floorLine.Add(new FloorLine(floor.checkpoints[0], floor.checkpoints[1]));
        floor.floorLine.Add(new FloorLine(floor.checkpoints[1], floor.checkpoints[2]));
        floor.floorLine.Add(new FloorLine(floor.checkpoints[2], floor.checkpoints[3]));
        floor.floorLine.Add(new FloorLine(floor.checkpoints[3], floor.checkpoints[0]));

        floor.center = GeoUtil.Centroid(floor.checkpoints);

        if (floor.heights.Count != 4)
        {
            floor.heights.Clear();
            for (int i = 0; i < 4; i++) floor.heights.Add(0.1f);
        }
        FloorStorage.UpdateOrAddFloor(floor);
    }

    private void SpawnHandles(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Transform parent)
    {
        ClearHandles();

        GameObject MakeHandle(Vector3 p, string name, bool isCorner, int idx, Transform pt)
        {
            // đảm bảo handle nằm đúng level floor
            p.y = BaseY(floorIndex) + lineLift;

            GameObject h;
            if (checkpointPrefab != null)
            {
                h = Instantiate(checkpointPrefab, p, Quaternion.identity, pt);
                h.SetActive(true);
                if (h.GetComponent<Collider>() == null)
                {
                    var sc = h.AddComponent<SphereCollider>();
                    sc.isTrigger = false;
                    sc.radius = 0.15f;
                }
            }
            else
            {
                h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                h.transform.SetParent(pt, true);
                h.transform.position = p;
                h.transform.localScale = Vector3.one * 0.3f;
                var sc = h.GetComponent<SphereCollider>();
                sc.isTrigger = false;
            }

            h.name = name;
            var tag = h.AddComponent<HandleTag>();
            tag.index = idx;
            tag.isCorner = isCorner;
            return h;
        }

        cornerHandles[0] = MakeHandle(a, "Corner_A", true, 0, parent);
        cornerHandles[1] = MakeHandle(b, "Corner_B", true, 1, parent);
        cornerHandles[2] = MakeHandle(d, "Corner_D", true, 2, parent);
        cornerHandles[3] = MakeHandle(e, "Corner_E", true, 3, parent);

        edgeHandles[0] = MakeHandle((a + b) * 0.5f, "Edge_AB", false, 0, parent);
        edgeHandles[1] = MakeHandle((b + d) * 0.5f, "Edge_BD", false, 1, parent);
        edgeHandles[2] = MakeHandle((d + e) * 0.5f, "Edge_DE", false, 2, parent);
        edgeHandles[3] = MakeHandle((e + a) * 0.5f, "Edge_EA", false, 3, parent);
    }

    private void ClearHandles()
    {
        for (int i = 0; i < cornerHandles.Length; i++)
            if (cornerHandles[i]) Destroy(cornerHandles[i]);
        for (int i = 0; i < edgeHandles.Length; i++)
            if (edgeHandles[i]) Destroy(edgeHandles[i]);

        for (int i = 0; i < edgeLabels.Count; i++)
            if (edgeLabels[i]) Destroy(edgeLabels[i]);
        edgeLabels.Clear();
    }

    public void RedrawRectangleFromState()
    {
        if (!hasRect || previewGO == null) return;

        Quaternion rot = Quaternion.Euler(0f, rectYaw, 0f);
        Vector3 c = rectCenter; // đã có y = baseY khi OnEndDrag

        Vector3 a = c + rot * new Vector3(-rectHalfW, 0, -rectHalfD);
        Vector3 b = c + rot * new Vector3(rectHalfW, 0, -rectHalfD);
        Vector3 d = c + rot * new Vector3(rectHalfW, 0, rectHalfD);
        Vector3 e = c + rot * new Vector3(-rectHalfW, 0, rectHalfD);

        previewLR.SetPosition(0, a + Vector3.up * lineLift);
        previewLR.SetPosition(1, b + Vector3.up * lineLift);
        previewLR.SetPosition(2, d + Vector3.up * lineLift);
        previewLR.SetPosition(3, e + Vector3.up * lineLift);
        previewLR.SetPosition(4, a + Vector3.up * lineLift);

        if (previewMesh == null) previewMesh = new Mesh();
        previewMesh.Clear();
        previewMesh.vertices = new Vector3[] { a, b, d, e };
        previewMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        previewMesh.uv = new Vector2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
        previewMesh.RecalculateNormals();
        previewMesh.RecalculateBounds();
        previewMF.sharedMesh = previewMesh;

        if (cornerHandles[0]) cornerHandles[0].transform.position = a + Vector3.up * lineLift;
        if (cornerHandles[1]) cornerHandles[1].transform.position = b + Vector3.up * lineLift;
        if (cornerHandles[2]) cornerHandles[2].transform.position = d + Vector3.up * lineLift;
        if (cornerHandles[3]) cornerHandles[3].transform.position = e + Vector3.up * lineLift;

        if (edgeHandles[0]) edgeHandles[0].transform.position = (a + b) * 0.5f + Vector3.up * lineLift;
        if (edgeHandles[1]) edgeHandles[1].transform.position = (b + d) * 0.5f + Vector3.up * lineLift;
        if (edgeHandles[2]) edgeHandles[2].transform.position = (d + e) * 0.5f + Vector3.up * lineLift;
        if (edgeHandles[3]) edgeHandles[3].transform.position = (e + a) * 0.5f + Vector3.up * lineLift;

        ShowEdgeLengths(a, b, d, e);
    }

    private void CleanupAllVisuals()
    {
        // labels
        for (int i = 0; i < edgeLabels.Count; i++) if (edgeLabels[i]) Destroy(edgeLabels[i]);
        edgeLabels.Clear();

        // handles
        for (int i = 0; i < cornerHandles.Length; i++) if (cornerHandles[i]) Destroy(cornerHandles[i]);
        for (int i = 0; i < edgeHandles.Length; i++) if (edgeHandles[i]) Destroy(edgeHandles[i]);
        System.Array.Clear(cornerHandles, 0, cornerHandles.Length);
        System.Array.Clear(edgeHandles, 0, edgeHandles.Length);

        // preview
        if (previewGO) { Destroy(previewGO); previewGO = null; }
        previewLR = null; previewMF = null; previewMR = null; previewMesh = null;

        // xoá floor parent nếu còn giữ
        // if (lastFloorGO) { PlacementManager.Instance.DestroyFloor(lastFloorGO); lastFloorGO = null; }
        if (lastFloorGO) { Destroy(lastFloorGO); lastFloorGO = null; }

        hasRect = false;
        isMovingHandle = false;
        activeIndex = -1;

        InteractionFlags.IsFloorHandleDragging = false;
    }
    // Tạo previewGO/previewLR/previewMF/previewMR nếu chưa có
    private void EnsurePreviewObjects()
    {
        if (previewGO != null) return;

        previewGO = new GameObject("FloorPreview");
        previewGO.hideFlags = HideFlags.DontSave;

        previewLR = previewGO.AddComponent<LineRenderer>();
        previewLR.positionCount = 5;
        previewLR.loop = false;
        previewLR.widthMultiplier = lineWidth;
        previewLR.material = lineMaterial;
        previewLR.useWorldSpace = true;
        previewLR.numCornerVertices = 4;
        previewLR.sortingOrder = floorIndex;

        previewMF = previewGO.AddComponent<MeshFilter>();
        previewMR = previewGO.AddComponent<MeshRenderer>();
        previewMesh = new Mesh { name = "FloorPreviewMesh" };
        previewMF.sharedMesh = previewMesh;

        var fillMat = new Material(Shader.Find("Standard"));
        fillMat.SetFloat("_Mode", 3);
        fillMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        fillMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        fillMat.SetInt("_ZWrite", 0);
        fillMat.DisableKeyword("_ALPHATEST_ON");
        fillMat.EnableKeyword("_ALPHABLEND_ON");
        fillMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        fillMat.renderQueue = 3000;
        fillMat.color = new Color(0.2f, 0.6f, 1f, 0.15f);
        previewMR.sharedMaterial = fillMat;
        previewMR.sortingOrder = floorIndex;
    }

    // === NẠP STATE THEO ID (set hasRect, tạo preview/handles, vẽ, và GHI ĐÈ floor theo ID) ===
    public void LoadStateFromFloorId(string id)
    {
        currentFloorId = id;
        // 1) Tìm đúng floor theo ID
        Floor f = null;
        if (FloorStorage.floors != null)
        {
            for (int i = 0; i < FloorStorage.floors.Count; i++)
            {
                var ff = FloorStorage.floors[i];
                if (ff != null && ff.ID == id) { f = ff; break; }
            }
        }
        if (f == null || f.checkpoints == null || f.checkpoints.Count < 4)
        {
            Debug.LogWarning($"[SpawnFloor] Không tìm thấy floor hợp lệ với ID={id}");
            return;
        }

        if (s_floorVisuals.TryGetValue(f.ID, out var oldGo) && oldGo)
        {
            Destroy(oldGo);                     // CHỈ xoá visual
            s_floorVisuals.Remove(f.ID);
        }
        lastFloorGO = new GameObject($"FloorVis_{f.ID}");

        // Tính state từ 4 điểm (A,B,D,E) trong XZ
        Vector2 a2 = f.checkpoints[0];
        Vector2 b2 = f.checkpoints[1];
        Vector2 d2 = f.checkpoints[2];
        Vector2 e2 = f.checkpoints[3];

        Vector2 center2 = (a2 + b2 + d2 + e2) * 0.25f;
        float halfW = Vector2.Distance(a2, b2) * 0.5f; // AB
        float halfD = Vector2.Distance(b2, d2) * 0.5f; // BD
        float yawDeg = Mathf.Atan2(b2.y - a2.y, b2.x - a2.x) * Mathf.Rad2Deg;

        // Bảo đảm preview/renderer đã có
        EnsurePreviewObjects();

        // Tạo preview
        s_floorVisuals[f.ID] = lastFloorGO;
        previewGO.transform.SetParent(lastFloorGO.transform, true);

        // Set state đầy đủ
        hasRect = true;
        rectCenter = new Vector3(center2.x, BaseY(floorIndex), center2.y);
        rectYaw = yawDeg;
        rectHalfW = halfW;
        rectHalfD = halfD;

        // Tính 4 góc để spawn đủ 8 handle
        Quaternion rot = Quaternion.Euler(0f, rectYaw, 0f);
        Vector3 c3 = rectCenter;
        Vector3 a3 = c3 + rot * new Vector3(-rectHalfW, 0, -rectHalfD);
        Vector3 b3 = c3 + rot * new Vector3(rectHalfW, 0, -rectHalfD);
        Vector3 d3 = c3 + rot * new Vector3(rectHalfW, 0, rectHalfD);
        Vector3 e3 = c3 + rot * new Vector3(-rectHalfW, 0, rectHalfD);

        ClearHandles();
        SpawnHandles(a3, b3, d3, e3, lastFloorGO.transform);

        // Vẽ lại preview
        RedrawRectangleFromState();

        // Ghi đè SẠCH dữ liệu của floor f theo state hiện tại
        SyncFloorDataByState(f);
        FloorStorage.UpdateOrAddFloor(f);
    }
    private void SyncFloorDataByState(Floor f)
    {
        if (!hasRect) return;

        Quaternion rot = Quaternion.Euler(0f, rectYaw, 0f);
        Vector3 c3 = rectCenter;

        Vector3 a3 = c3 + rot * new Vector3(-rectHalfW, 0, -rectHalfD);
        Vector3 b3 = c3 + rot * new Vector3(rectHalfW, 0, -rectHalfD);
        Vector3 d3 = c3 + rot * new Vector3(rectHalfW, 0, rectHalfD);
        Vector3 e3 = c3 + rot * new Vector3(-rectHalfW, 0, rectHalfD);

        // XÓA sạch & ghi lại 4 điểm
        f.checkpoints.Clear();
        f.checkpoints.Add(new Vector2(a3.x, a3.z));
        f.checkpoints.Add(new Vector2(b3.x, b3.z));
        f.checkpoints.Add(new Vector2(d3.x, d3.z));
        f.checkpoints.Add(new Vector2(e3.x, e3.z));

        f.floorLine.Clear();
        f.floorLine.Add(new FloorLine(f.checkpoints[0], f.checkpoints[1]));
        f.floorLine.Add(new FloorLine(f.checkpoints[1], f.checkpoints[2]));
        f.floorLine.Add(new FloorLine(f.checkpoints[2], f.checkpoints[3]));
        f.floorLine.Add(new FloorLine(f.checkpoints[3], f.checkpoints[0]));

        f.center = GeoUtil.Centroid(f.checkpoints);

        f.heights.Clear();
        for (int i = 0; i < 4; i++) f.heights.Add(0.1f);
    }
    // gọi để quay về trạng thái chỉ còn 1 floor (xoá hết data + visuals cũ)
    public void ResetSingleFloor()
    {
        // Nếu preview đang nằm trong lastFloorGO, tách ra trước khi phá parent
        if (previewGO && lastFloorGO && previewGO.transform.IsChildOf(lastFloorGO.transform))
            previewGO.transform.SetParent(null, true);

        // xoá visuals các floor cũ
        foreach (var kv in s_floorVisuals)
            if (kv.Value) Destroy(kv.Value);
        s_floorVisuals.Clear();

        // xoá toàn bộ mesh floor đã dựng bởi PlacementManager (nếu có)
        PlacementManager.Instance?.DestroyAllFloors();

        // xoá dữ liệu floor trong storage
        if (FloorStorage.floors != null) FloorStorage.floors.Clear();

        // dọn handle + label
        ClearHandles();

        // reset state
        hasRect = false;
        isMovingHandle = false;
        activeIndex = -1;
        currentFloorId = null;

        // xoá parent cũ nếu còn
        if (lastFloorGO) { Destroy(lastFloorGO); lastFloorGO = null; }

        // XOÁ preview hoàn toàn để lần sau tạo lại mới
        if (previewGO) { Destroy(previewGO); }
        previewGO = null;
        previewLR = null; previewMF = null; previewMR = null; previewMesh = null;

        InteractionFlags.IsFloorHandleDragging = false;
    }

}
