using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class CreateRoomOnFloor : MonoBehaviour
{
    [Header("UI")]
    public Button CreateRoomButton;

    [Header("Raycast")]
    public LayerMask floorMask = ~0;

    // ---------- Arrow Preview ----------
    [Header("Arrow Preview")]
    [Tooltip("Dùng mũi tên thay vì khung chữ nhật cho preview.")]
    public bool useArrowPreview = true;
    [Tooltip("Màu mũi tên và chữ trên mũi tên.")]
    public Color arrowColor = Color.white;
    [Tooltip("Độ dày thân mũi tên (LineRenderer).")]
    public float arrowWidth = 0.02f;
    [Tooltip("Độ dài hai cánh đầu mũi tên (m).")]
    public float arrowHeadSize = 0.15f;
    [Tooltip("Nhấc mũi tên lên khỏi mặt sàn (m) để tránh z-fighting).")]
    public float arrowLift = 0.012f;
    [Tooltip("Hiện thêm mũi tên đường chéo P1->P2.")]
    public bool showDiagonalArrow = true;

    [Header("Arrow Text")]
    [Tooltip("Cỡ font (TextMesh) cho text trên mũi tên.")]
    public int arrowTextFontSize = 100;
    [Tooltip("Kích cỡ ký tự trong world cho TextMesh.")]
    public float arrowTextCharSize = 0.03f;

    // Layering để room luôn nổi trên floor
    [Header("Render Layering")]
    public int roomIndex = 2;           // room ở index 2
    public float layerStepY = 0.002f;   // mỗi index cách nhau 2mm
    public float roomWallLift = 0.003f; // line/marker nhô nhẹ

    private CheckpointManager checkPointManager;

    private bool placingActive = false;

    // Drag state
    private bool isDragging = false;
    private Vector3 dragStartWorld;
    private Transform dragFloorRoot;
    private Collider dragFloorCol;

    // Marker P1
    private GameObject firstMarker = null;

    // ===== Preview root + material =====
    private GameObject previewRootGO;
    private Material previewLineMat;

    public static bool IsCreateRooom = false;

    // ===== Arrow renderers/texts =====
    private LineRenderer arrowX, arrowXHead;   // P1 -> (p2.x, p1.z)
    private LineRenderer arrowZ, arrowZHead;   // P1 -> (p1.x, p2.z)
    private LineRenderer arrowD, arrowDHead;   // P1 -> P2 (tuỳ chọn)
    private TextMesh arrowXText, arrowZText, arrowDText;

    void Start()
    {
        checkPointManager = FindFirstObjectByType<CheckpointManager>();
        if (CreateRoomButton != null) CreateRoomButton.onClick.AddListener(TogglePlacingMode);

        // Material Unlit để tô màu LineRenderer/TextMesh
        var unlit = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        previewLineMat = new Material(unlit);
    }

    void OnDestroy()
    {
        if (CreateRoomButton != null) CreateRoomButton.onClick.RemoveListener(TogglePlacingMode);
        DestroyPreviewImmediate();
        if (firstMarker) Destroy(firstMarker);
        IsCreateRooom = false;
    }

    void Update()
    {
        if (!placingActive) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Begin drag: set P1
        if (Input.GetMouseButtonDown(0))
        {
            if (TryGetMouseWorldOnFloor(out var pos, out var col, out var root))
            {
                float yPlane = pos.y;
                var p1Clamped = ClampPointToFloor(pos, root, yPlane);

                isDragging = true;
                IsCreateRooom = true;

                dragStartWorld = p1Clamped;
                dragFloorRoot = root;
                dragFloorCol = col;

                if (checkPointManager != null && checkPointManager.checkpointPrefab != null)
                {
                    firstMarker = Instantiate(checkPointManager.checkpointPrefab, p1Clamped + Vector3.up * 0.01f, Quaternion.identity);
                    firstMarker.name = "RoomP1_Preview";
                }

                UpdatePreview(dragStartWorld, pos, dragFloorRoot);
            }
            else if (TryGetMouseOnAnyFloorClamped(out var p1Snap, out var root2))
            {
                Debug.Log("Phòng được tạo ở đây");

                isDragging = true;
                IsCreateRooom = true;

                dragStartWorld = p1Snap;
                dragFloorRoot = root2;
                dragFloorCol = root2.GetComponent<Collider>() ?? root2.GetComponentInChildren<Collider>();

                if (checkPointManager != null && checkPointManager.checkpointPrefab != null)
                {
                    firstMarker = Instantiate(checkPointManager.checkpointPrefab, p1Snap + Vector3.up * 0.01f, Quaternion.identity);
                    firstMarker.name = "RoomP1_Preview";
                }

                UpdatePreview(dragStartWorld, p1Snap, dragFloorRoot);
            }
            else
            {
                isDragging = false;
                IsCreateRooom = false;
            }
        }

        // Dragging: update preview
        if (isDragging && Input.GetMouseButton(0))
        {
            if (TryGetMouseWorldOnFloor(out var pos, out var _, out var root) && root == dragFloorRoot)
            {
                UpdatePreview(dragStartWorld, pos, dragFloorRoot);
            }
            else
            {
                HidePreview();
            }
        }

        // End drag: commit room
        if (isDragging && Input.GetMouseButtonUp(0))
        {
            if (TryGetMouseWorldOnFloor(out var pos, out var _, out var root) && root == dragFloorRoot)
            {
                CommitRoom(dragStartWorld, pos, dragFloorRoot);
            }
            else
            {
                Debug.LogError("[CreateRoom] MouseUp nhưng không còn ở cùng RoomFloor -> huỷ.");
                ResetDragState(keepPlacing: true);
            }
            IsCreateRooom = false;
        }
    }

    // ===== Toggle =====
    private void TogglePlacingMode()
    {
        placingActive = !placingActive;
        if (!placingActive)
        {
            ResetDragState(keepPlacing: false);
            IsCreateRooom = false;
        }

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

        Transform t = hit.collider ? hit.collider.transform : null;
        while (t != null && !t.CompareTag("RoomFloor")) t = t.parent;
        if (t == null) return false;

        floorRoot = t;
        floorCol = t.GetComponent<Collider>() ?? hit.collider;
        pos = hit.point;
        return true;
    }

    // ===== Preview objects (arrow only) =====
    private void EnsurePreviewObjects(Transform parentForPreview)
    {
        if (previewRootGO == null)
        {
            previewRootGO = new GameObject("[Room Preview]");
            previewRootGO.transform.SetParent(null, worldPositionStays: true);
        }

        // helpers
        LineRenderer CreateLR(string name, float width, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(previewRootGO.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.widthMultiplier = width;
            lr.numCornerVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = previewLineMat;
            lr.positionCount = 2;
            lr.sortingOrder = order;
            return lr;
        }
        TextMesh CreateText(string name, int order, int fontSize, float charSize, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(previewRootGO.transform, false);
            var tm = go.AddComponent<TextMesh>();
            tm.text = "";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.color = col;
            var mr = go.GetComponent<MeshRenderer>(); if (mr) mr.sortingOrder = order;
            return tm;
        }

        // Arrow renderers + text
        if (arrowX == null)      arrowX      = CreateLR("ArrowX", arrowWidth, 20);
        if (arrowXHead == null)  arrowXHead  = CreateLR("ArrowXHead", arrowWidth, 21);
        if (arrowZ == null)      arrowZ      = CreateLR("ArrowZ", arrowWidth, 20);
        if (arrowZHead == null)  arrowZHead  = CreateLR("ArrowZHead", arrowWidth, 21);
        if (arrowD == null)      arrowD      = CreateLR("ArrowD", arrowWidth, 30);
        if (arrowDHead == null)  arrowDHead  = CreateLR("ArrowDHead", arrowWidth, 31);

        if (arrowXText == null) arrowXText = CreateText("ArrowXText", 22, arrowTextFontSize, arrowTextCharSize, arrowColor);
        if (arrowZText == null) arrowZText = CreateText("ArrowZText", 22, arrowTextFontSize, arrowTextCharSize, arrowColor);
        if (arrowDText == null) arrowDText = CreateText("ArrowDText", 32, arrowTextFontSize, arrowTextCharSize, arrowColor);

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

        arrowX = arrowXHead = arrowZ = arrowZHead = arrowD = arrowDHead = null;
        arrowXText = arrowZText = arrowDText = null;
    }

    private void UpdatePreview(Vector3 p1, Vector3 p2, Transform floorRoot)
    {
        EnsurePreviewObjects(floorRoot);

        // ===== ARROW PREVIEW =====
        if (!useArrowPreview) return;

        float yA = p1.y + arrowLift;
        Vector3 P1 = new(dragStartWorld.x, yA, dragStartWorld.z);
        Vector3 P2 = new(p2.x, yA, p2.z);
        Vector3 P1_to_X = new(p2.x, yA, dragStartWorld.z);
        Vector3 P1_to_Z = new(dragStartWorld.x, yA, p2.z);

        float dx = Mathf.Abs(P1_to_X.x - P1.x);
        float dz = Mathf.Abs(P1_to_Z.z - P1.z);
        float dd = Vector3.Distance(P1, P2);

        Color c = arrowColor;

        DrawArrow(arrowX, arrowXHead, arrowXText, P1, P1_to_X, c, $"{dx:0.##} m");
        DrawArrow(arrowZ, arrowZHead, arrowZText, P1, P1_to_Z, c, $"{dz:0.##} m");

        if (showDiagonalArrow)
        {
            if (arrowDText) arrowDText.gameObject.SetActive(true);
            DrawArrow(arrowD, arrowDHead, arrowDText, P1, P2, c, $"{dd:0.##} m");
        }
        else
        {
            if (arrowD) arrowD.enabled = false;
            if (arrowDHead) arrowDHead.enabled = false;
            if (arrowDText) arrowDText.gameObject.SetActive(false);
        }
    }
    void DrawArrow(LineRenderer body, LineRenderer head, TextMesh tmesh,
                   Vector3 a, Vector3 b, Color c, string txt)
    {
        if (!body || !head || !tmesh) return;

        body.enabled = true;
        body.startColor = c; body.endColor = c;
        body.widthMultiplier = arrowWidth;
        body.positionCount = 2;
        body.SetPosition(0, a);
        body.SetPosition(1, b);
        SetMatColor(body.material, c);

        Vector3 dir = (b - a); dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) { head.enabled = false; tmesh.text = ""; return; }
        dir.Normalize();

        Vector3 wingL = (Quaternion.Euler(0f, +25f, 0f) * -dir) * arrowHeadSize;
        Vector3 wingR = (Quaternion.Euler(0f, -25f, 0f) * -dir) * arrowHeadSize;

        head.enabled = true;
        head.startColor = c; head.endColor = c;
        head.widthMultiplier = arrowWidth;
        head.positionCount = 3;
        head.SetPosition(0, b);
        head.SetPosition(1, b + wingL);
        head.SetPosition(2, b + wingR);
        SetMatColor(head.material, c);

        // ===== text song song mũi tên, luôn nằm TRÊN line và không bị mirror =====
        tmesh.color = c;
        tmesh.text = txt;
        tmesh.anchor = TextAnchor.MiddleCenter;

        Vector3 mid = (a + b) * 0.5f;

        // pháp tuyến trái của mũi tên trong XZ
        Vector3 n = Vector3.Cross(Vector3.up, dir).normalized;

        // ép lên phía +Z để "trên" màn (nếu đang hướng -Z thì đảo lại)
        if (Vector3.Dot(n, Vector3.forward) < 0f) n = -n;

        float sideOffset = Mathf.Max(arrowWidth * 4f, 0.15f);
        float lift = Mathf.Max(arrowWidth * 0.6f, 0.015f);

        Quaternion rot = Quaternion.LookRotation(-Vector3.up, n);

        tmesh.transform.SetPositionAndRotation(mid + n * sideOffset + Vector3.up * lift, rot);

        // luôn render trên line
        var mr = tmesh.GetComponent<MeshRenderer>();
        if (mr)
        {
            var m = mr.material;
            m.renderQueue = 4000;
            if (m.HasProperty("_ZTest")) m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            mr.sortingOrder = Mathf.Max(body.sortingOrder, head.sortingOrder) + 10;
        }
    }

    // ===== Tạo room khi thả chuột =====
    private void CommitRoom(Vector3 p1, Vector3 p2, Transform floorRoot)
    {
        if (Vector3.Distance(p1, p2) < 0.01f)
        {
            Debug.LogWarning("[CreateRoom] Kéo quá ngắn -> bỏ qua.");
            ResetDragState(keepPlacing: true);
            return;
        }

        float minX = Mathf.Min(p1.x, p2.x);
        float maxX = Mathf.Max(p1.x, p2.x);
        float minZ = Mathf.Min(p1.z, p2.z);
        float maxZ = Mathf.Max(p1.z, p2.z);

        float baseRoomY = roomIndex * layerStepY;

        Vector3 v0 = new(minX, baseRoomY, minZ);
        Vector3 v1 = new(minX, baseRoomY, maxZ);
        Vector3 v2 = new(maxX, baseRoomY, maxZ);
        Vector3 v3 = new(maxX, baseRoomY, minZ);

        bool allOK =
            IsPointOnThisFloorRoot(v0, floorRoot) &&
            IsPointOnThisFloorRoot(v1, floorRoot) &&
            IsPointOnThisFloorRoot(v2, floorRoot) &&
            IsPointOnThisFloorRoot(v3, floorRoot);

        if (!allOK)
        {
            Debug.LogError("[CreateRoom] Một hoặc nhiều đỉnh KHÔNG thuộc RoomFloor -> không tạo phòng.");
            ResetDragState(keepPlacing: true);
            return;
        }

        if (!TryGetFloorFromRoot(floorRoot, out var floor) || floor == null)
        {
            Debug.LogError("[CreateRoom] Không tìm ra Floor từ floorRoot -> huỷ tạo phòng.");
            ResetDragState(keepPlacing: true);
            return;
        }

        // Checkpoints (XZ)
        Vector2 l0 = new(v0.x, v0.z);
        Vector2 l1 = new(v1.x, v1.z);
        Vector2 l2 = new(v2.x, v2.z);
        Vector2 l3 = new(v3.x, v3.z);

        // WallLines (nổi nhẹ)
        Vector3 v0_show = new(v0.x, v0.y + roomWallLift, v0.z);
        Vector3 v1_show = new(v1.x, v1.y + roomWallLift, v1.z);
        Vector3 v2_show = new(v2.x, v2.y + roomWallLift, v2.z);
        Vector3 v3_show = new(v3.x, v3.y + roomWallLift, v3.z);

        // Tạo room
        var room = new Room(floor)
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

        // Hướng mặc định Bắc

        checkPointManager.DrawWallLineByRoom(room);

        room.center = GeoUtil.Centroid(room.checkpoints);
        RoomStorage.UpdateOrAddRoom(room);
        floor.RegisterRoom(room);

        checkPointManager.CreateRoomMeshCtrl(room, room.center);
        //GameObject floorGO = new GameObject($"RoomFloor_{room.ID}");
        //floorGO.transform.SetPositionAndRotation(new Vector3(0f, baseRoomY, 0f), Quaternion.identity);
        //var meshCtrl = floorGO.AddComponent<RoomMeshController>();
        //meshCtrl.Initialize(room.ID);
        //meshCtrl.GenerateMesh(room.checkpoints);

        if (checkPointManager != null)
        {
            //checkPointManager.RoomFloorMap ??= new Dictionary<string, GameObject>();
            //checkPointManager.RoomFloorMap[room.ID] = floorGO;

            //var loopGO = new List<GameObject>();
            //if (checkPointManager.checkpointPrefab != null)
            //{
            //    loopGO.Add(Instantiate(checkPointManager.checkpointPrefab, v0_show, Quaternion.identity));
            //    loopGO.Add(Instantiate(checkPointManager.checkpointPrefab, v1_show, Quaternion.identity));
            //    loopGO.Add(Instantiate(checkPointManager.checkpointPrefab, v2_show, Quaternion.identity));
            //    loopGO.Add(Instantiate(checkPointManager.checkpointPrefab, v3_show, Quaternion.identity));
            //}


            //checkPointManager.loopMappings ??= new List<LoopMap>();
            //checkPointManager.loopMappings.Add(new LoopMap(room.ID, loopGO));
            //checkPointManager.AllCheckpoints.Add(loopGO);
            checkPointManager.AddGameObjectCheckPointToGlobalVariable(room);
            checkPointManager.RedrawAllRooms();
        }

        Debug.Log($"[CreateRoom] Tạo room {room.ID} thuộc floor {floor.ID} | 4 đỉnh: {l0}, {l1}, {l2}, {l3}");

        ResetDragState(keepPlacing: false);
        placingActive = false;
        if (CreateRoomButton != null)
        {
            var colors = CreateRoomButton.colors;
            colors.normalColor = Color.white;
            CreateRoomButton.colors = colors;
        }

        UndoRedoController.Instance.AddToUndo(new CreateRoomCommand(room.ID));
    }

    public void CreateRoomByRoomData(Room room)
    {
        
    }

    private void ResetDragState(bool keepPlacing)
    {
        isDragging = false;
        IsCreateRooom = false;

        dragFloorRoot = null;
        dragFloorCol = null;

        HidePreview();

        if (firstMarker) Destroy(firstMarker);
        firstMarker = null;

        if (!keepPlacing) DestroyPreviewImmediate();
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

        var fmc = floorRoot.GetComponent<FloorMeshController>() ?? floorRoot.GetComponentInChildren<FloorMeshController>();
        if (fmc != null && !string.IsNullOrEmpty(fmc.floorID))
        {
            floor = FindFloorByID(fmc.floorID);
            if (floor != null) return true;
        }

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
            var b = (i + 1 < n) ? poly[i + 1] : poly[0];
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
            var b = (i + 1 < n) ? poly[i + 1] : poly[0];
            if (DistPointToSegment2D(p, a, b) <= eps2) return true;
        }
        return false;
    }

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

    private bool TryGetMouseOnAnyFloorClamped(out Vector3 pClamped, out Transform floorRoot)
    {
        pClamped = default;
        floorRoot = null;

        var cam = Camera.main;
        if (cam == null) return false;

        if (!MouseToPlaneY(0f, out var onPlane)) return false;

        var candidates = GameObject.FindGameObjectsWithTag("RoomFloor");
        if (candidates == null || candidates.Length == 0) return false;

        float bestScore = float.PositiveInfinity;
        Transform bestRoot = null;
        Vector3 bestP = default;

        foreach (var go in candidates)
        {
            var root = go.transform;
            float y = root.position.y;

            var snapped = ClampPointToFloor(new Vector3(onPlane.x, y, onPlane.z), root, y);

            float score = (new Vector2(onPlane.x, onPlane.z) - new Vector2(snapped.x, snapped.z)).sqrMagnitude;

            if (score < bestScore)
            {
                bestScore = score;
                bestRoot = root;
                bestP = snapped;
            }
        }

        if (bestRoot != null)
        {
            floorRoot = bestRoot;
            pClamped = bestP;
            return true;
        }
        return false;
    }

    private Vector3 ClampPointToFloor(Vector3 worldPoint, Transform floorRoot, float defaultY)
    {
        if (TryGetFloorFromRoot(floorRoot, out var floor) &&
            floor.checkpoints != null && floor.checkpoints.Count >= 3)
        {
            Vector2 p = new Vector2(worldPoint.x, worldPoint.z);

            if (PointInPolygon2D(p, floor.checkpoints) || OnBoundary2D(p, floor.checkpoints, 1e-4f))
                return new Vector3(worldPoint.x, defaultY, worldPoint.z);

            float bestDist2 = float.PositiveInfinity;
            Vector2 best = p;

            for (int i = 0, n = floor.checkpoints.Count; i < n; i++)
            {
                var a = floor.checkpoints[i];
                var b = (i + 1 < n) ? floor.checkpoints[i + 1] : floor.checkpoints[0];

                Vector2 ab = b - a;
                float ab2 = Vector2.Dot(ab, ab);
                Vector2 cand;
                if (ab2 < 1e-10f)
                {
                    cand = a;
                }
                else
                {
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2);
                    cand = a + t * ab;
                }

                float d2 = (p - cand).sqrMagnitude;
                if (d2 < bestDist2)
                {
                    bestDist2 = d2;
                    best = cand;
                }
            }

            return new Vector3(best.x, defaultY, best.y);
        }

        var col = floorRoot ? (floorRoot.GetComponent<Collider>() ?? floorRoot.GetComponentInChildren<Collider>()) : null;
        if (col != null)
        {
            var cp = col.ClosestPoint(worldPoint);
            return new Vector3(cp.x, defaultY, cp.z);
        }

        return new Vector3(worldPoint.x, defaultY, worldPoint.z);
    }

    private bool MouseToPlaneY(float planeY, out Vector3 hit)
    {
        hit = default;
        var cam = Camera.main; if (cam == null) return false;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (plane.Raycast(ray, out float t))
        {
            hit = ray.GetPoint(t);
            return true;
        }
        return false;
    }

    // ===== util: set màu material (URP/Built-in) =====
    static void SetMatColor(Material m, Color c)
    {
        if (!m) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);   // URP/Lit/Unlit
        else if (m.HasProperty("_Color")) m.SetColor("_Color", c);      // Built-in/Unlit
        else if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c);
    }
}
