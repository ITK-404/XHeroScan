using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class DragFromButtonSpawnFloor : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
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

    private bool isDragging = false;
    private float yaw = 0f;
    private Vector3 lastHitPos = Vector3.zero;

    // --- Preview state ---
    private GameObject previewGO;
    private LineRenderer previewLR;
    private MeshFilter previewMF;
    private MeshRenderer previewMR;
    private Mesh previewMesh;

    // floor vừa place (để xóa được)
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
    private GameObject[] edgeHandles   = new GameObject[4]; // AB,BD,DE,EA

    // đánh dấu handle
    private class HandleTag : MonoBehaviour
    {
        public int index;    // 0..3
        public bool isCorner;
    }

    private void Awake()
    {
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

        // Ground plane pick
        var ground = GameObject.Find("GroundPlane");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GroundPlane";
            ground.transform.localScale = new Vector3(50, 1, 50);
            int floorPickLayer = LayerMask.NameToLayer("FloorPick");
            if (floorPickLayer != -1) ground.layer = floorPickLayer;
        }
        var mr = ground.GetComponent<MeshRenderer>();
        if (mr) mr.enabled = false;
        if (ground.GetComponent<Collider>() == null) ground.AddComponent<BoxCollider>();

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
        FloorStorage.floors.Clear();
        
        InteractionFlags.IsFloorHandleDragging = false;
    }

    private void Update()
    {
        // bắt chọn handle
        if (!isMovingHandle && Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            TryPickHandleUnderMouse();
        }

        if (!isMovingHandle || !hasRect) return;

        // KÉO HANDLE
        if (!Input.GetMouseButton(0))
        {
            isMovingHandle = false;
            activeIndex = -1;

            // đã dừng kéo ->  tắt cờ
            InteractionFlags.IsFloorHandleDragging = false;
            return;
        }

        if (!TryMouseOnGround(out Vector3 p)) return;

        // đưa p về local
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        yaw = 0f;
        lastHitPos = Vector3.zero;

        // dọn cũ
        ClearHandles();
        hasRect = false;

        if (previewGO == null)
        {
            previewGO = new GameObject("FloorPreview");
            previewGO.hideFlags = HideFlags.DontSave;

            previewLR = previewGO.AddComponent<LineRenderer>();
            previewLR.positionCount = 5;
            previewLR.loop = false;
            previewLR.widthMultiplier = lineWidth;
            previewLR.material = lineMaterial;
            previewLR.useWorldSpace = true;
            previewLR.numCornerVertices = 4;

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
        }

        previewGO.SetActive(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

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

            Vector3 c = lastHitPos; c.y = 0f;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

            Vector3 a = c + rot * new Vector3(-hw, 0, -hd);
            Vector3 b = c + rot * new Vector3(hw, 0, -hd);
            Vector3 d = c + rot * new Vector3(hw, 0,  hd);
            Vector3 e = c + rot * new Vector3(-hw, 0,  hd);

            previewLR.SetPosition(0, a);
            previewLR.SetPosition(1, b);
            previewLR.SetPosition(2, d);
            previewLR.SetPosition(3, e);
            previewLR.SetPosition(4, a);

            if (previewMesh == null) previewMesh = new Mesh();
            previewMesh.Clear();
            previewMesh.vertices  = new Vector3[] { a, b, d, e };
            previewMesh.triangles = new int[]     { 0, 1, 2, 0, 2, 3 };
            previewMesh.uv        = new Vector2[] { new(0,0), new(1,0), new(1,1), new(0,1) };
            previewMesh.RecalculateNormals();
            previewMesh.RecalculateBounds();
            previewMF.sharedMesh = previewMesh;

            ShowEdgeLengths(a, b, d, e, "m", 0.4f);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        if (lastHitPos != Vector3.zero)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            Vector3 c = lastHitPos; c.y = 0f;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

            Vector3 a = c + rot * new Vector3(-hw, 0, -hd);
            Vector3 b = c + rot * new Vector3( hw, 0, -hd);
            Vector3 d = c + rot * new Vector3( hw, 0,  hd);
            Vector3 e = c + rot * new Vector3(-hw, 0,  hd);

            // lưu state để edit
            rectCenter = c;
            rectYaw    = yaw;
            rectHalfW  = hw;
            rectHalfD  = hd;
            hasRect    = true;

            // dữ liệu
            var floor = new Floor();
            floor.checkpoints.Add(new Vector2(a.x, a.z));
            floor.checkpoints.Add(new Vector2(b.x, b.z));
            floor.checkpoints.Add(new Vector2(d.x, d.z));
            floor.checkpoints.Add(new Vector2(e.x, e.z));
            floor.floorLine.Add(new FloorLine(floor.checkpoints[0], floor.checkpoints[1]));
            floor.floorLine.Add(new FloorLine(floor.checkpoints[1], floor.checkpoints[2]));
            floor.floorLine.Add(new FloorLine(floor.checkpoints[2], floor.checkpoints[3]));
            floor.floorLine.Add(new FloorLine(floor.checkpoints[3], floor.checkpoints[0]));
            for (int i = 0; i < 4; i++) floor.heights.Add(0.1f);
            FloorStorage.floors.Add(floor);

            // Vẽ lên scene – GIỮ reference để xóa được
            // lastFloorGO = PlacementManager.Instance.PlaceRectFloor(
            //     lastHitPos, width, depth, yaw,
            //     checkpointPrefab, lineMaterial, lineWidth
            // );

            if (previewGO != null) previewGO.SetActive(true);

            SpawnHandles(a, b, d, e);
            RedrawRectangleFromState();
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
    }

    // ==== Hiển thị độ dài cạnh ====
    private void ShowEdgeLengths(Vector3 a, Vector3 b, Vector3 d, Vector3 e,
                                 string unit = " ", float outwardOffset = 0.3f)
    {
        for (int i = 0; i < edgeLabels.Count; i++)
            if (edgeLabels[i]) Destroy(edgeLabels[i]);
        edgeLabels.Clear();

        if (distanceTextPrefab == null) return;

        var cam = Camera.main;
        Vector3 center = (a + b + d + e) * 0.25f;

        (Vector3 p0, Vector3 p1)[] edges = new (Vector3, Vector3)[]
        {
            (a, b), (b, d), (d, e), (e, a)
        };

        for (int i = 0; i < edges.Length; i++)
        {
            var (p0, p1) = edges[i];
            Vector3 mid = (p0 + p1) * 0.5f;

            Vector3 outward = (mid - center); outward.y = 0f;
            Vector3 dir = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.forward;

            Vector3 pos = mid + dir * outwardOffset;

            GameObject label = Instantiate(distanceTextPrefab, pos, Quaternion.identity);
            label.name = $"EdgeLength_{i}";
            edgeLabels.Add(label);

            float len = Vector3.Distance(p0, p1);
            string text = $"{len:0.##} {unit}";

            var tmp = label.GetComponent<TMPro.TMP_Text>();
            if (tmp != null) tmp.text = text;
            else
            {
                var uiText = label.GetComponent<Text>();
                if (uiText != null) uiText.text = text;
            }

            if (cam != null)
                label.transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryPickHandleUnderMouse();
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

                // báo cho hệ input khác biết: đang kéo point floor
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
        if (!hasRect || FloorStorage.floors.Count == 0) return;

        var floor = FloorStorage.floors[FloorStorage.floors.Count - 1];

        Quaternion rot = Quaternion.Euler(0f, rectYaw, 0f);
        Vector3 c = rectCenter;

        Vector3 a = c + rot * new Vector3(-rectHalfW, 0, -rectHalfD);
        Vector3 b = c + rot * new Vector3( rectHalfW, 0, -rectHalfD);
        Vector3 d = c + rot * new Vector3( rectHalfW, 0,  rectHalfD);
        Vector3 e = c + rot * new Vector3(-rectHalfW, 0,  rectHalfD);

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

        if (floor.heights.Count != 4)
        {
            floor.heights.Clear();
            for (int i = 0; i < 4; i++) floor.heights.Add(0.1f);
        }
    }

    private void SpawnHandles(Vector3 a, Vector3 b, Vector3 d, Vector3 e)
    {
        ClearHandles();

        GameObject MakeHandle(Vector3 p, string name, bool isCorner, int idx)
        {
            GameObject h;
            if (checkpointPrefab != null)
            {
                h = Instantiate(checkpointPrefab, p, Quaternion.identity);
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

        cornerHandles[0] = MakeHandle(a, "Corner_A", true, 0);
        cornerHandles[1] = MakeHandle(b, "Corner_B", true, 1);
        cornerHandles[2] = MakeHandle(d, "Corner_D", true, 2);
        cornerHandles[3] = MakeHandle(e, "Corner_E", true, 3);

        edgeHandles[0] = MakeHandle((a + b) * 0.5f, "Edge_AB", false, 0);
        edgeHandles[1] = MakeHandle((b + d) * 0.5f, "Edge_BD", false, 1);
        edgeHandles[2] = MakeHandle((d + e) * 0.5f, "Edge_DE", false, 2);
        edgeHandles[3] = MakeHandle((e + a) * 0.5f, "Edge_EA", false, 3);
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

    private void RedrawRectangleFromState()
    {
        if (!hasRect || previewGO == null) return;

        Quaternion rot = Quaternion.Euler(0f, rectYaw, 0f);
        Vector3 c = rectCenter;

        Vector3 a = c + rot * new Vector3(-rectHalfW, 0, -rectHalfD);
        Vector3 b = c + rot * new Vector3( rectHalfW, 0, -rectHalfD);
        Vector3 d = c + rot * new Vector3( rectHalfW, 0,  rectHalfD);
        Vector3 e = c + rot * new Vector3(-rectHalfW, 0,  rectHalfD);

        previewLR.SetPosition(0, a);
        previewLR.SetPosition(1, b);
        previewLR.SetPosition(2, d);
        previewLR.SetPosition(3, e);
        previewLR.SetPosition(4, a);

        if (previewMesh == null) previewMesh = new Mesh();
        previewMesh.Clear();
        previewMesh.vertices  = new Vector3[] { a, b, d, e };
        previewMesh.triangles = new int[]     { 0, 1, 2, 0, 2, 3 };
        previewMesh.uv        = new Vector2[] { new(0,0), new(1,0), new(1,1), new(0,1) };
        previewMesh.RecalculateNormals();
        previewMesh.RecalculateBounds();
        previewMF.sharedMesh = previewMesh;

        if (cornerHandles[0]) cornerHandles[0].transform.position = a;
        if (cornerHandles[1]) cornerHandles[1].transform.position = b;
        if (cornerHandles[2]) cornerHandles[2].transform.position = d;
        if (cornerHandles[3]) cornerHandles[3].transform.position = e;

        if (edgeHandles[0]) edgeHandles[0].transform.position = (a + b) * 0.5f;
        if (edgeHandles[1]) edgeHandles[1].transform.position = (b + d) * 0.5f;
        if (edgeHandles[2]) edgeHandles[2].transform.position = (d + e) * 0.5f;
        if (edgeHandles[3]) edgeHandles[3].transform.position = (e + a) * 0.5f;

        ShowEdgeLengths(a, b, d, e, "m", 0.4f);
    }

    [Header("Flow")]
    public bool editAfterPlace = true;   // false: vẽ xong là dọn sạch

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

        // xoá floor cuối nếu còn giữ
        if (lastFloorGO) { PlacementManager.Instance.DestroyFloor(lastFloorGO); lastFloorGO = null; }

        hasRect = false;
        isMovingHandle = false;
        activeIndex = -1;

        InteractionFlags.IsFloorHandleDragging = false;
    }
}
