using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using System.Collections;

public class RoomMeshController : MonoBehaviour
{
    private static readonly Plane floorPlane = new Plane(Vector3.up, Vector3.zero); // vẫn giữ, nhưng không dùng cho drag nữa
    private static Camera mainCam;
    public string RoomID;
    private Vector3 dragStartWorldPos;
    public bool isDragging = false;
    private Room oldRoom;
    private CheckpointManager checkPointManager;

    [Header("Floor Material (optional)")]
    [SerializeField] private Material floorMaterial;

    [Header("Layering")]
    [SerializeField] public int index = 2;          // yêu cầu: index = 2
    [SerializeField] public float layerStepY = 0.002f; // bước cao độ mỗi layer (2mm) 

    private EditoRoomCommandCreator editRoomCommandCreator = new();
    private bool _cancelMultitouch = false;
    private void Awake()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }
        checkPointManager = CheckpointManager.Instance;
    }

#if UNITY_STANDALONE
    // PC: vẫn dùng OnMouseDown/Drag/Up
#else
    void LateUpdate()
    {
        if (!PenManager.isPenActive) return;

        // Nếu trước đó đã hủy do đa chạm, chờ nhả hết tay
        if (_cancelMultitouch)
        {
            if (Input.touchCount == 0) _cancelMultitouch = false;
            else return;
        }

        // đang kéo mà có >= 2 ngón -> hủy ngay, yêu cầu chọn lại
        if (isDragging && Input.touchCount >= 2)
        {
            isDragging = false;
            PenManager.isRoomFloorBeingDragged = false;
            if (checkPointManager != null) checkPointManager.IsDraggingRoom = false;

            furnitureDraggingByRoom.EndDrag();
            furnitureDraggingByRoom.Clear();

            _cancelMultitouch = true; // khóa cho đến khi nhả hết tay
            return;
        }

        if (Input.touchCount == 1)
        {
            if (InteractionFlags.IsOpenBottomSheetUI) return;
            if (InteractionFlags.OnDragFurniture) return;
            if (InteractionFlags.OnDragMovePoint) return;
            if (InteractionFlags.OnDragRotatePoint) return;
            if (PencilLine.isDragging) return;

            Touch touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (CheckTouchHitThisObject(touch.position))
                        OnStartDrag(touch.position);
                    break;

                case TouchPhase.Moved:
                    if (!isDragging) return;
                    DragRoom(touch.position);
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    OnEndDrag(touch.position);
                    break;
            }
        }
    }
#endif

    private bool CheckTouchHitThisObject(Vector2 screenPos)
    {
        Ray ray = mainCam.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            return hit.transform == this.transform;
        }

        return false;
    }

    // Hàm di chuyển Room theo vị trí chạm for Android
    void DragRoom(Vector2 screenPos)
    {
        if (!RoomInfoDisplay.IsConfirm) return;
        if (checkPointManager.selectedCheckpoint != null) return;
        if (Input.touchCount >= 2 && isDragging)
        {
            isDragging = false;
            InteractionFlags.IsRoomFloorDragging = false;
            if (checkPointManager != null) checkPointManager.IsDraggingRoom = false;

            furnitureDraggingByRoom.EndDrag();
            furnitureDraggingByRoom.Clear();

            _cancelMultitouch = true;
            return;
        }
        if (EventSystem.current.IsPointerOverGameObject()) return;
        if (CreateRoomOnFloor.IsCreateRooom) return;
        if (ConnectManager.isConnectActive) return;
        if (InteractionFlags.OnDragFurniture) return;
        if (InteractionFlags.OnDragMovePoint) return;

        InteractionFlags.IsRoomFloorDragging = true; // dùng tạm khoa khóa để đặt
        if (InteractionFlags.IsOpenBottomSheetUI) return;

        if (this == null || !this) return;                // Đã bị destroy
        if (gameObject == null || !gameObject.activeInHierarchy) return;
        if (transform == null) return;

        if (IsClickingOnBackgroundBlackUI(Input.mousePosition))
        {
            Debug.Log("Đang nhấn Background Black -> Không move Mesh");
            return;
        }


        Ray ray = mainCam.ScreenPointToRay(screenPos);

        Plane planeAtIndex = new Plane(Vector3.up, new Vector3(0f, index * layerStepY, 0f));

        if (planeAtIndex.Raycast(ray, out float distance))
        {
            Vector3 currentPos = ray.GetPoint(distance);
            Vector3 deltaRaw = currentPos - dragStartWorldPos;

            // Lấy dữ liệu room & floor
            Room room = RoomStorage.GetRoomByID(RoomID);
            Vector3 delta = deltaRaw;

            if (room != null)
            {
                Floor floor = FindFloorById(room.floorID);
                if (floor != null)
                {
                    // Kẹp delta để các đỉnh phòng không vượt polygon sàn
                    delta = ClampDeltaToFloorBinarySearch(room, deltaRaw, floor, index * layerStepY, 1e-3f);
                }
            }

            // Nếu delta ~ 0 thì thôi
            if (delta.sqrMagnitude <= 1e-12f) return;

            // Cập nhật mốc kéo theo delta thật sự di chuyển
            dragStartWorldPos += delta;

            // Move sàn (mesh)
            transform.position += delta;
            furnitureDraggingByRoom.Dragging(delta);

            // Update DATA của room (world-space)
            if (room != null)
            {
                for (int i = 0; i < room.checkpoints.Count; i++)
                {
                    Vector2 old = room.checkpoints[i];
                    room.checkpoints[i] = new Vector2(old.x + delta.x, old.y + delta.z);
                }
                for (int i = 0; i < room.extraCheckpoints.Count; i++)
                {
                    Vector2 old = room.extraCheckpoints[i];
                    room.extraCheckpoints[i] = new Vector2(old.x + delta.x, old.y + delta.z);
                }
                for (int i = 0; i < room.wallLines.Count; i++)
                {
                    room.wallLines[i].start += delta;
                    room.wallLines[i].end   += delta;
                }

                // Update các GameObject hiển thị (KHÔNG vẽ lại)
                if (checkPointManager != null)
                {
                    var mapping = checkPointManager.AllCheckpoints.Find(loop =>
                        checkPointManager.FindRoomIDForLoop(loop) == RoomID);
                    if (mapping != null)
                    {
                        foreach (var cp in mapping)
                            if (cp) cp.transform.position += delta;
                    }

                    if (checkPointManager.tempDoorWindowPoints.TryGetValue(RoomID, out var doorsInRoom))
                    {
                        foreach (var (line, p1GO, p2GO) in doorsInRoom)
                        {
                            if (p1GO) p1GO.transform.position += delta;
                            if (p2GO) p2GO.transform.position += delta;
                        }
                    }

                    var extras = GameObject.FindGameObjectsWithTag("CheckpointExtra");
                    for (int i = 0; i < extras.Length; i++)
                    {
                        var go = extras[i];
                        if (!go) continue;

                        Vector3 projected = go.transform.position + delta;
                        string rid = checkPointManager.FindRoomIDByPoint(projected);
                        if (!string.IsNullOrEmpty(rid) && rid == RoomID)
                            go.transform.position += delta;
                    }
                }
            }
        }

        checkPointManager.RedrawAllRooms();
    }

    public void Initialize(string roomID, Color color = default)
    {
        RoomID = roomID;

        Room room = RoomStorage.GetRoomByID(RoomID);
        if (room == null)
        {
            Debug.LogError($"RoomMeshController: Không tìm thấy Room với ID {RoomID}");
            return;
        }

        GenerateMesh(room.checkpoints);

        // Tạo MeshRenderer nếu chưa có
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // Nếu chưa có material, tạo material mặc định
        if (floorMaterial == null)
        {
            floorMaterial = new Material(Shader.Find("Unlit/Color"));
            Color usedColor = (color == default) ? Color.white : color;
            floorMaterial.color = usedColor;
        }

        meshRenderer.material = floorMaterial;

        StartCoroutine(DelayedAddCollider());

        // Đăng ký lại RoomFloorMap
        checkPointManager.RoomFloorMap[RoomID] = this.gameObject;
        Debug.Log($"Đã tự động đăng ký RoomFloorMap[{RoomID}] = {gameObject.name}");
    }

    IEnumerator DelayedAddCollider()
    {
        yield return null; // chờ 1 frame

        var mesh = GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh != null && mesh.triangles.Length >= 3)
        {
            var collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
    }

    public void GenerateMesh(List<Vector2> checkpoints)
    {
        Vector2 pivot = GetCentroid(checkpoints); // pivot thật

        // Dịch điểm về local-space để tạo mesh
        List<Vector2> offsetPoints = checkpoints
            .Select(p => new Vector2(p.x - pivot.x, p.y - pivot.y))
            .ToList();

        Mesh mesh = MeshGenerator.CreateRoomMesh(offsetPoints);

        if (GetComponent<MeshFilter>() == null)
            gameObject.AddComponent<MeshFilter>();
        if (GetComponent<MeshRenderer>() == null)
            gameObject.AddComponent<MeshRenderer>();

        GetComponent<MeshFilter>().mesh = mesh;

        var meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
            meshCollider.sharedMesh = mesh;

        // === Đặt lại transform để khớp world-space ở cao độ theo index ===
        transform.position = new Vector3(pivot.x, index * layerStepY, pivot.y);
    }

    private Vector2 GetCentroid(List<Vector2> points)
    {
        if (points == null || points.Count == 0)
            return Vector2.zero;

        float sumX = 0f, sumY = 0f;
        foreach (var p in points)
        {
            sumX += p.x;
            sumY += p.y;
        }

        return new Vector2(sumX / points.Count, sumY / points.Count);
    }

    private List<DrawingInstanced> oldList = new();
    private void OnStartDrag(Vector3 startDragPosition)
    {

        if (!CheckTouchHitThisObject(startDragPosition))
        {
            return;
        }

        if (checkPointManager != null)
        {
            PenManager.isRoomFloorBeingDragged = true;
            checkPointManager.IsDraggingRoom = true;
        }

        Ray ray = mainCam.ScreenPointToRay(startDragPosition);

        // ==== dùng plane ở cao độ theo index ====
        Plane planeAtIndex = new Plane(Vector3.up, new Vector3(0f, index * layerStepY, 0f));

        if (planeAtIndex.Raycast(ray, out float distance))
        {
            dragStartWorldPos = ray.GetPoint(distance);
            isDragging = true;
        }


        oldPosition = transform.position;
        oldCheckPointList = SaveCheckPointPosition(RoomID);

        furnitureDraggingByRoom.SetRoomID(RoomID);
        furnitureDraggingByRoom.StartDrag();

        editRoomCommandCreator.Init(RoomID);
    }
    private FurnitureDraggingByRoom furnitureDraggingByRoom = new FurnitureDraggingByRoom();

    private List<(Vector3, Vector3)> SaveCheckPointPosition(string RoomID)
    {
        var checkPointList = new List<(Vector3, Vector3)>();
        if (checkPointManager.tempDoorWindowPoints.TryGetValue(RoomID, out var doorsInRoom))
        {
            foreach (var (line, p1GO, p2GO) in doorsInRoom)
            {
                if (p1GO && p2GO)
                    checkPointList.Add((p1GO.transform.position, p2GO.transform.position));
            }
        }

        return checkPointList;
    }

    private void OnEndDrag(Vector2 screenPosition)
    {
        if (InteractionFlags.IsOpenBottomSheetUI) return;

        isDragging = false;

        if (checkPointManager != null)
        {
            PenManager.isRoomFloorBeingDragged = false;
            checkPointManager.IsDraggingRoom = false;
        }

        if (!CheckTouchHitThisObject(screenPosition))
        {
            return;
        }

        if (Vector3.Distance(oldPosition, transform.position) > 0.1f)
        {
            editRoomCommandCreator.CreateCommand();
        }

        //CreateUndoCommand();

        furnitureDraggingByRoom.EndDrag();
        furnitureDraggingByRoom.Clear();
    }

#if UNITY_EDITOR
    private void OnMouseDown()
    {
        if (!PenManager.isPenActive) return;
        if (isDragging) return;
        OnStartDrag(Input.mousePosition);
    }

    private void OnMouseUp()
    {
        OnEndDrag(Input.mousePosition);
    }

    private void OnMouseDrag()
    {
        if (this == null || gameObject == null || transform == null)
            return;
        if (!PenManager.isPenActive) return;
        if (!isDragging) return;
        DragRoom(Input.mousePosition);
    }
#endif

    // ==== đổi sang Vector3 để khớp transform.position (tránh lỗi ép kiểu) ====
    private Vector3 oldPosition;
    private List<(Vector3, Vector3)> oldCheckPointList = new List<(Vector3, Vector3)>();

    private void CreateUndoCommand()
    {
        if (oldRoom == null) return;
        MoveRoomData moveObject = new MoveRoomData();
        moveObject.RoomID = RoomID;
        moveObject.MovingObject = transform;

        moveObject.OldPosition = oldPosition;
        moveObject.CurrentPosition = transform.position;

        moveObject.OldRoom = new Room(oldRoom);
        moveObject.NewRoom = new Room(RoomStorage.GetRoomByID(RoomID));

        moveObject.OldCheckPointPos = new List<(Vector3, Vector3)>(oldCheckPointList);
        moveObject.CurrentCheckPointPos = SaveCheckPointPosition(RoomID);

        var command = new MoveRectangularUndoRedoCommand(moveObject);
        //UndoRedoController.Instance.AddToUndo(command);
    }

    // === Hàm ko cho move trên UI
    private bool IsClickingOnBackgroundBlackUI(Vector2 screenPosition)
    {
        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (result.gameObject.name == "Background Black")
            {
                Debug.Log("Click UI trên Background Black -> Không cho move point");
                return true;
            }
        }

        return false;
    }
    
    private static bool IsPointInPolygonXZ(Vector3 p, List<Vector2> poly, float eps = 0f)
    {
        // ray casting (odd-even). Làm việc trên XZ -> Vector2(x,z)
        int n = poly?.Count ?? 0;
        if (n < 3) return false;

        Vector2 pt = new Vector2(p.x, p.z);

        // Nếu muốn cho "kề biên" tính là inside, nới eps > 0 một chút
        // Kiểm tra nằm trên cạnh (optional, cho eps > 0)
        if (eps > 0f)
        {
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                if (DistancePointToSegment(pt, a, b) <= eps) return true;
            }
        }

        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 pi = poly[i];
            Vector2 pj = poly[j];

            bool intersect = ((pi.y > pt.y) != (pj.y > pt.y)) &&
                            (pt.x < (pj.x - pi.x) * (pt.y - pi.y) / (pj.y - pi.y + 1e-15f) + pi.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float ab2 = Vector2.Dot(ab, ab);
        if (ab2 < 1e-12f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2);
        Vector2 proj = a + t * ab;
        return Vector2.Distance(p, proj);
    }

    // Trả về các đỉnh world-space của phòng (chỉ dùng main checkpoints)
    private List<Vector3> GetRoomWorldVertices(Room room, float yOfFloor)
    {
        var verts = new List<Vector3>(room.checkpoints.Count);
        for (int i = 0; i < room.checkpoints.Count; i++)
        {
            var p = room.checkpoints[i];
            verts.Add(new Vector3(p.x, yOfFloor, p.y));
        }
        return verts;
    }

    private Vector3 ClampDeltaToFloorBinarySearch(
        Room room,
        Vector3 deltaRaw,
        Floor floor,
        float currentY,
        float eps = 1e-3f,
        int iters = 24,
        float keepAway = 0.01f,     // cách mép tối thiểu (1cm)
        int slideIters = 3,         // số vòng trượt
        float slideSnap = 0.03f,    // “bắt mép” dễ hơn khi gần mép
        float forceSlideRatio = 0.85f,
        float minTangentDot = 1e-4f
    )
    {
        if (floor == null || floor.checkpoints == null || floor.checkpoints.Count < 3)
            return deltaRaw;

        // Lấy các đỉnh phòng (world) tại cao độ sàn
        var startVerts = GetRoomWorldVertices(room, currentY);

        // --- Helper inline: kiểm tra "inside + cách mép >= keepAway"
        bool InsideWithMargin(Vector3 testDelta)
        {
            // duyệt các đỉnh
            for (int vi = 0; vi < startVerts.Count; vi++)
            {
                Vector3 q = startVerts[vi] + testDelta;
                if (!IsPointInPolygonXZ(q, floor.checkpoints, eps))
                    return false;

                // min dist tới cạnh
                int n = floor.checkpoints.Count;
                Vector2 pt = new Vector2(q.x, q.z);
                float minD = float.PositiveInfinity;
                Vector2 prev = floor.checkpoints[n - 1];
                for (int i = 0; i < n; i++)
                {
                    Vector2 cur = floor.checkpoints[i];
                    float d = DistancePointToSegment(pt, prev, cur);
                    if (d < minD) minD = d;
                    prev = cur;
                }
                if (minD < keepAway) return false;
            }
            return true;
        }

        if (!InsideWithMargin(Vector3.zero)) return Vector3.zero;          
        if (InsideWithMargin(deltaRaw)) return deltaRaw;                   

        float lo = 0f, hi = 1f;
        for (int k = 0; k < iters; k++)
        {
            float mid = 0.5f * (lo + hi);
            Vector3 testDelta = deltaRaw * mid;
            if (InsideWithMargin(testDelta)) lo = mid;
            else hi = mid;
        }
        Vector3 moved = deltaRaw * lo;

        // trượt dọc mép (nếu còn phần dư)
        Vector3 remaining = deltaRaw - moved;
        if (remaining.sqrMagnitude <= 1e-12f) return moved;

        // tạo verts hiện tại (moved)
        List<Vector3> vertsNow = new List<Vector3>(startVerts.Count);
        for (int i = 0; i < startVerts.Count; i++) vertsNow.Add(startVerts[i] + moved);

        // tìm cạnh gần nhất
        bool GetClosestEdge(out Vector2 a, out Vector2 b, out Vector3 nOut)
        {
            a = b = default;
            nOut = Vector3.zero;

            int n = floor.checkpoints.Count;
            if (n < 2) return false;

            // centroid phòng hiện tại (XZ)
            Vector2 c = Vector2.zero;
            for (int i = 0; i < vertsNow.Count; i++)
                c += new Vector2(vertsNow[i].x, vertsNow[i].z);
            c /= Mathf.Max(1, vertsNow.Count);

            float minD = float.PositiveInfinity;
            // xét tất cả đỉnh phòng và cạnh sàn
            for (int vi = 0; vi < vertsNow.Count; vi++)
            {
                Vector2 p = new Vector2(vertsNow[vi].x, vertsNow[vi].z);
                Vector2 prev = floor.checkpoints[n - 1];
                for (int i = 0; i < n; i++)
                {
                    Vector2 cur = floor.checkpoints[i];
                    float d = DistancePointToSegment(p, prev, cur);
                    if (d < minD)
                    {
                        minD = d; a = prev; b = cur;
                    }
                    prev = cur;
                }
            }
            if (minD > slideSnap + 1e-6f) return false;

            Vector2 e2 = (b - a);
            if (e2.sqrMagnitude < 1e-12f) return false;

            // 2 hướng pháp tuyến 2D
            Vector2 nA = new Vector2(-e2.y, e2.x).normalized;
            Vector2 nB = -nA;

            float probe = 0.05f;
            bool aIsOut = !IsPointInPolygonXZ(new Vector3(c.x + nA.x * probe, 0f, c.y + nA.y * probe), floor.checkpoints, 0f);
            Vector2 outward2 = aIsOut ? nA : nB;
            nOut = new Vector3(outward2.x, 0f, outward2.y);
            return true;
        }

        for (int it = 0; it < slideIters; it++)
        {
            if (!GetClosestEdge(out var ea, out var eb, out var nOut)) break;

            float outComp = Vector3.Dot(remaining, nOut);
            if (outComp > 0f) remaining -= nOut * outComp;

            // tiếp tuyến cạnh
            Vector2 e2 = (eb - ea);
            if (e2.sqrMagnitude < 1e-12f) break;
            Vector3 tHat = new Vector3(e2.x, 0f, e2.y).normalized;

            float tComp = Vector3.Dot(remaining, tHat);
            Vector3 slideTry;
            if (Mathf.Abs(tComp) < minTangentDot)
            {
                float mag = remaining.magnitude * forceSlideRatio;
                float sign = (Vector3.Dot(deltaRaw, tHat) >= 0f) ? 1f : -1f;
                slideTry = tHat * (sign * mag);
            }
            else slideTry = tHat * tComp;

            if (slideTry.sqrMagnitude <= 1e-12f) break;

            // binary clamp theo slideTry, vẫn giữ khoảng cách mép
            float lo2 = 0f, hi2 = 1f;
            for (int k = 0; k < iters; k++)
            {
                float mid = 0.5f * (lo2 + hi2);
                Vector3 test = moved + slideTry * mid;
                
                if (InsideWithMargin(test - Vector3.zero)) lo2 = mid;
                else hi2 = mid;
            }
            Vector3 slideMove = slideTry * lo2;
            if (slideMove.sqrMagnitude <= 1e-12f) break;

            moved += slideMove;
            remaining -= slideMove;
            for (int i = 0; i < vertsNow.Count; i++) vertsNow[i] += slideMove;

            if (remaining.sqrMagnitude <= 1e-12f) break;
        }

        return moved;
    }

    private static Floor FindFloorById(string id)
    {
        if (string.IsNullOrEmpty(id) || FloorStorage.floors == null) return null;
        for (int i = 0; i < FloorStorage.floors.Count; i++)
        {
            var f = FloorStorage.floors[i];
            if (f != null && f.ID == id) return f;
        }
        return null;
    }
}
