using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Linq;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance;

    [Header("Prefabs")]
    public GameObject checkpointPrefab;

    public DrawingTool DrawingTool;

    public LineType currentLineType = LineType.Wall;
    public List<WallLine> wallLines = new List<WallLine>();
    public List<Room> rooms = new List<Room>();

    [Header("Camera")]
    public Camera drawingCamera; // Gán Camera chính vẽ 2D

    public bool isMovingCheckpoint = false;
    public List<GameObject> currentCheckpoints = new List<GameObject>();
    public GameObject selectedCheckpoint = null; // Điểm được chọn để di chuyển  
    public bool isDragging = false; // Kiểm tra xem có đang kéo điểm không 
    public bool isPreviewing = false; // Trạng thái preview
    public bool isClosedLoop = false; // Biến kiểm tra xem mạch đã khép kín chưa 

    public bool IsDraggingRoom = false;
    public GameObject previewCheckpoint = null;

    private List<List<GameObject>> allCheckpoints = new List<List<GameObject>>();

    public List<List<GameObject>> AllCheckpoints =>
        allCheckpoints; // Truy cập danh sách tất cả các checkpoint từ bên ngoài

    public Dictionary<string, GameObject> RoomFloorMap = new(); // roomID → floor GameObject

    private float closeThreshold = 0.2f; // Khoảng cách tối đa để chọn điểm
    private Vector3 previewPosition; // Vị trí preview
    private GameObject firstPoint = null;

    private WallLine selectedWallLineForDoor; // đoạn tường được chọn
    private SplitRoomManager splitRoomManager;

    private Room selectedRoomForDoor;
    private Vector3? firstDoorPoint = null; // lưu P1

    // Map loop checkpoint list => Room ID
    public List<LoopMap> loopMappings = new List<LoopMap>();
    // Lưu lại tất cả các cửa / cửa sổ để chèn lại sau khi rebuild wallLines

    // [RoomID] → List<(WallLine, GameObject p1, GameObject p2)>
    public Dictionary<string, List<(WallLine line, GameObject p1, GameObject p2)>> tempDoorWindowPoints
        = new Dictionary<string, List<(WallLine, GameObject, GameObject)>>();
    public string lastSelectedRoomID = null;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        
        splitRoomManager = FindFirstObjectByType<SplitRoomManager>();
        LoadPointsFromRoomStorage();
    }

    void Update()
    {        
        if (EventSystem.current.IsPointerOverGameObject())
        {
            isPreviewing = false;
            DrawingTool.ClearPreviewLine();
            if (previewCheckpoint != null)
            {
                Destroy(previewCheckpoint);
                previewCheckpoint = null;
            }
            return;
        }

        if (Input.GetMouseButton(0))
        {
            isPreviewing = true;
            previewPosition = GetWorldPositionFromScreen(Input.mousePosition);

            // Nếu đã có điểm đầu thì vẽ preview line đến chuột
            if (firstPoint != null)
            {
                Vector3 start = firstPoint.transform.position;
                DrawingTool.DrawPreviewLine(start, previewPosition);
            }

            if (previewCheckpoint == null)
            {
                previewCheckpoint = Instantiate(checkpointPrefab, previewPosition, Quaternion.identity);
                previewCheckpoint.name = "PreviewCheckpoint";
            }
            previewCheckpoint.transform.position = previewPosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isPreviewing = false;
            DrawingTool.ClearPreviewLine();

            if (previewCheckpoint != null)
            {
                Destroy(previewCheckpoint);
            }

            if (currentLineType == LineType.Wall)
                HandleSingleWallPlacement(previewPosition);
            else
                HandleCheckpointPlacement(previewPosition);

            DeselectCheckpoint();
            isDragging = false;
        }
    }

    public void SelectCheckpoint()
    {
        Vector3 clickPosition = GetWorldPositionFromScreen(Input.mousePosition);
        TrySelectCheckpoint(clickPosition);
    }

    public void HandleCheckpointPlacement(Vector3 position)
    {
        if (selectedCheckpoint != null) return; // Nếu đã chọn điểm, không cần đặt mới
        
        // === Nếu là cửa sổ/cửa và không đang vẽ loop thì xử lý riêng ===
        if (currentLineType == LineType.Door || currentLineType == LineType.Window)
        {
            InsertDoorOrWindow(position, currentLineType);
            return;
        }
    }

    // Put these fields in your class (outside of any method)
    private const float EDGE_EPS   = 0.001f; // kiểm tra nằm trên biên
    private const float SNAP_EPS   = 0.01f;  // snap ~ 1cm
    private const float DUP_EPS2   = 1e-6f;  // bỏ giao trùng A/B
    private const float DEDUP_EPS2 = 1e-6f;  // dedup intersections

    public void HandleSingleWallPlacement(Vector3 position)
    {
        if (ConnectManager.isConnectActive) return;

        List<GameObject> tempCreatedPoints = new();

        // 1) CLICK 1
        if (firstPoint == null)
        {
            firstPoint = Instantiate(checkpointPrefab, position, Quaternion.identity);
            firstPoint.transform.SetParent(null);
            return;
        }

        // 2) CLICK 2
        Vector3 startWorld = firstPoint.transform.position;
        Vector3 endWorld   = position;

        Vector2 aOrig = new(startWorld.x, startWorld.z);
        Vector2 bOrig = new(endWorld.x,   endWorld.z);

        bool anyRoomUpdated       = false;
        bool firstPointInsideRoom = false;
        List<Room> roomsToSplit   = new();

        foreach (Room room in RoomStorage.rooms.ToList())
        {
            bool aInside = PointInPolygon(aOrig, room.checkpoints);
            bool bInside = PointInPolygon(bOrig, room.checkpoints);
            bool aOnBoundary = OnBoundary(room, aOrig, EDGE_EPS);

            if (aInside || aOnBoundary) firstPointInsideRoom = true;

            // --- lấy / tạo LoopMap ---
            var map = loopMappings.FirstOrDefault(m => m.RoomID == room.ID);
            if (map == null)
            {
                map = new LoopMap(room.ID, new List<GameObject>());
                loopMappings.Add(map);
                allCheckpoints.Add(map.CheckpointsGO);
            }
            if ((aInside || aOnBoundary) && firstPoint != null && !map.CheckpointsGO.Contains(firstPoint))
                map.CheckpointsGO.Add(firstPoint);

            // 2.0. Chọn A,B (hình học)
            Vector2 A, B;
            bool insideInsideCase, A_fromHit, B_fromHit, haveValidPair;
            DetermineSegmentForRoom(room, aOrig, bOrig,
                                    out A, out B,
                                    out insideInsideCase,
                                    out A_fromHit, out B_fromHit,
                                    out haveValidPair);
            if (!haveValidPair) continue;

            // Snap về biên (nếu cần)
            if (!insideInsideCase && (A_fromHit || !aInside)) TrySnapToAnyEdge(room, A, SNAP_EPS, out A);
            if (!insideInsideCase && (B_fromHit || !bInside)) TrySnapToAnyEdge(room, B, SNAP_EPS, out B);

            // Tính lại trạng thái sau snap
            bool aNowInside = PointInPolygon(A, room.checkpoints);
            bool bNowInside = PointInPolygon(B, room.checkpoints);
            bool A_onBoundary2 = OnBoundary(room, A, EDGE_EPS);
            bool B_onBoundary2 = OnBoundary(room, B, EDGE_EPS);

            // 2.1. Quyết định vẽ
            bool A_anchored = A_onBoundary2 || A_fromHit;
            bool B_anchored = B_onBoundary2 || B_fromHit;
            bool shouldDraw = insideInsideCase || A_anchored || B_anchored;
            if (!shouldDraw) continue;

            Vector3 start = new(A.x, 0, A.y);
            Vector3 end   = new(B.x, 0, B.y);

            // 2.2. Spawn/insert điểm biên
            if (A_onBoundary2 && !room.checkpoints.Any(p => Vector2.Distance(p, A) < EDGE_EPS))
                tempCreatedPoints.Add(SpawnEdgeCheckpoint(room, map, start, A));
            if (B_onBoundary2 && !room.checkpoints.Any(p => Vector2.Distance(p, B) < EDGE_EPS))
                tempCreatedPoints.Add(SpawnEdgeCheckpoint(room, map, end, B));

            // marker cho đầu bên trong nhưng không trên biên (để người dùng thấy)
            if (bNowInside && !B_onBoundary2)
                tempCreatedPoints.Add(SpawnMarker(map, end));
            // (đầu A nếu trong và không trên biên chính là firstPoint)

            // 2.3. Marker các giao cắt với tường nội bộ
            SpawnInternalIntersections(room, map, A, B, tempCreatedPoints);

            // 2.4. Vẽ line
            DrawingTool.currentLineType = currentLineType;
            DrawingTool.DrawLineAndDistance(start, end);

            var newline = new WallLine { start = start, end = end, type = currentLineType };
            room.wallLines.Add(newline);
            DrawingTool.wallLines.Add(newline);

            RoomStorage.UpdateOrAddRoom(room);
            anyRoomUpdated = true;

            // 2.5. Tách nếu tạo mạch kín
            if (ShouldSplitSegment(room, A, B, A_onBoundary2, B_onBoundary2))
                roomsToSplit.Add(room);
        }

        // 3) KẾT THÚC
        if (!anyRoomUpdated)
        {
            if (!firstPointInsideRoom && firstPoint) Destroy(firstPoint);
            foreach (var p in tempCreatedPoints) if (p) Destroy(p);
        }
        else
        {
            if (!firstPointInsideRoom && firstPoint) Destroy(firstPoint);

            if (roomsToSplit.Count > 0)
                StartCoroutine(WaitAndSplitRooms(roomsToSplit.Distinct().ToList()));

            RedrawAllRooms();
        }

        firstPoint = null; // reset
    }

    private float DistPointSeg2(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / (ab.sqrMagnitude + 1e-12f);
        t = Mathf.Clamp01(t);
        Vector2 proj = a + t * ab;
        return (p - proj).sqrMagnitude;
    }

    private bool TrySnapToAnyEdge(Room room, Vector2 p, float snapEps, out Vector2 snapped)
    {
        float bestD2 = float.MaxValue;
        Vector2 best = p;
        foreach (var w in room.wallLines)
        {
            Vector2 a = new(w.start.x, w.start.z);
            Vector2 b = new(w.end.x,   w.end.z);
            float d2 = DistPointSeg2(p, a, b);
            if (d2 < bestD2)
            {
                bestD2 = d2;
                Vector2 ab = b - a;
                float t = Vector2.Dot(p - a, ab) / (ab.sqrMagnitude + 1e-12f);
                t = Mathf.Clamp01(t);
                best = a + t * ab;
            }
        }
        if (bestD2 <= snapEps * snapEps) { snapped = best; return true; }
        snapped = p; return false;
    }

    private IEnumerable<(Vector2 a, Vector2 b)> GetBoundaryEdges(Room room)
    {
        var cps = room.checkpoints;
        for (int i = 0, n = cps.Count; i < n; i++)
            yield return (cps[i], cps[(i + 1) % n]);
    }

    private bool OnBoundary(Room room, Vector2 p, float eps)
    {
        foreach (var (a, b) in GetBoundaryEdges(room))
            if (PointOnSegment(p, a, b, eps)) return true;
        return false;
    }

    private List<Vector2> DedupIntersections(List<Vector2> raw)
    {
        List<Vector2> outList = new();
        foreach (var p in raw)
        {
            bool dup = false;
            foreach (var q in outList)
                if ((p - q).sqrMagnitude < DEDUP_EPS2) { dup = true; break; }
            if (!dup) outList.Add(p);
        }
        return outList;
    }

    private bool SegIntersectOrTouchEps(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, float eps, out Vector2 inter)
    {
        inter = default;

        float d12 = Mathf.Min(
            DistPointSeg2(a1, b1, b2),
            Mathf.Min(DistPointSeg2(a2, b1, b2),
            Mathf.Min(DistPointSeg2(b1, a1, a2),
                    DistPointSeg2(b2, a1, a2))));
        if (d12 <= eps * eps)
        {
            Vector2 mid = Vector2.zero; int c = 0;
            if (DistPointSeg2(a1, b1, b2) <= eps * eps) { mid += a1; c++; }
            if (DistPointSeg2(a2, b1, b2) <= eps * eps) { mid += a2; c++; }
            if (DistPointSeg2(b1, a1, a2) <= eps * eps) { mid += b1; c++; }
            if (DistPointSeg2(b2, a1, a2) <= eps * eps) { mid += b2; c++; }
            if (c > 0) inter = mid / c;
            return true;
        }

        if (LineSegmentsIntersect(a1, a2, b1, b2, out inter)) return true;
        return false;
    }

    private bool TryBoundaryIntersection(Room room, Vector2 A, Vector2 B, out Vector2 hit, bool preferNearB)
    {
        hit = default; bool found = false; float best = float.MaxValue;
        foreach (var (e1, e2) in GetBoundaryEdges(room))
        {
            if (SegIntersectOrTouchEps(A, B, e1, e2, EDGE_EPS, out Vector2 inter))
            {
                float score = preferNearB ? (inter - B).sqrMagnitude : (inter - A).sqrMagnitude;
                if (score < best) { best = score; hit = inter; found = true; }
            }
        }
        return found;
    }

    private void DetermineSegmentForRoom(
        Room room, Vector2 aOrig, Vector2 bOrig,
        out Vector2 A, out Vector2 B,
        out bool insideInsideCase,
        out bool A_fromHit, out bool B_fromHit,
        out bool haveValidPair)
    {
        insideInsideCase = false; A_fromHit = B_fromHit = false; haveValidPair = false;
        A = aOrig; B = bOrig;

        bool aInside = PointInPolygon(aOrig, room.checkpoints);
        bool bInside = PointInPolygon(bOrig, room.checkpoints);

        var intersections = DedupIntersections(GetLinePolygonIntersections(aOrig, bOrig, room.checkpoints));

        Vector2 dir = (bOrig - aOrig);
        float dirLen2 = dir.sqrMagnitude + 1e-9f;
        var hits = intersections
            .Select(p => (p, t: Mathf.Clamp01(Vector2.Dot(p - aOrig, dir) / dirLen2)))
            .OrderBy(x => x.t)
            .ToList();

        if (!aInside && !bInside)
        {
            if (hits.Count >= 2)
            {
                int pickI = -1; float pickLen = -1f;
                for (int i = 0; i < hits.Count - 1; i++)
                {
                    float midT = 0.5f * (hits[i].t + hits[i + 1].t);
                    Vector2 mid = aOrig + (bOrig - aOrig) * midT;
                    if (PointInPolygon(mid, room.checkpoints))
                    {
                        float segLen = hits[i + 1].t - hits[i].t;
                        if (segLen > pickLen) { pickLen = segLen; pickI = i; }
                    }
                }
                if (pickI >= 0)
                {
                    A = hits[pickI].p;   A_fromHit = true;
                    B = hits[pickI+1].p; B_fromHit = true;
                    haveValidPair = true;
                }
            }
        }
        else if (!aInside && bInside)
        {
            if (hits.Count >= 1)
            {
                A = hits.OrderBy(h => Vector2.SqrMagnitude(h.p - bOrig)).First().p;
                A_fromHit = true; haveValidPair = true;
            }
            else if (TryBoundaryIntersection(room, aOrig, bOrig, out var h, preferNearB: true))
            {
                A = h; A_fromHit = true; haveValidPair = true;
            }
        }
        else if (aInside && !bInside)
        {
            if (hits.Count >= 1)
            {
                B = hits.OrderBy(h => Vector2.SqrMagnitude(h.p - aOrig)).First().p;
                B_fromHit = true; haveValidPair = true;
            }
            else if (TryBoundaryIntersection(room, aOrig, bOrig, out var h, preferNearB: false))
            {
                B = h; B_fromHit = true; haveValidPair = true;
            }
        }
        else
        {
            insideInsideCase = true; haveValidPair = true;
        }
    }

    private GameObject SpawnEdgeCheckpoint(Room room, LoopMap map, Vector3 pos, Vector2 p2)
    {
        InsertPointIntoWall(room, p2);
        var go = Instantiate(checkpointPrefab, pos, Quaternion.identity);
        go.transform.SetParent(null, true);
        map.CheckpointsGO.Add(go);
        return go;
    }

    private GameObject SpawnMarker(LoopMap map, Vector3 pos)
    {
        var go = Instantiate(checkpointPrefab, pos, Quaternion.identity);
        go.transform.SetParent(null, true);
        map.CheckpointsGO.Add(go);
        return go;
    }

    private void SpawnInternalIntersections(Room room, LoopMap map, Vector2 A, Vector2 B, List<GameObject> bucket)
    {
        foreach (var wall in room.wallLines.ToList())
        {
            Vector2 w1 = new(wall.start.x, wall.start.z);
            Vector2 w2 = new(wall.end.x,   wall.end.z);

            if (SegIntersectOrTouchEps(A, B, w1, w2, EDGE_EPS, out Vector2 inter))
            {
                if ((inter - A).sqrMagnitude < DUP_EPS2 || (inter - B).sqrMagnitude < DUP_EPS2) continue;

                var ipGO = Instantiate(checkpointPrefab, new Vector3(inter.x, 0, inter.y), Quaternion.identity);
                ipGO.transform.SetParent(null, true);
                map.CheckpointsGO.Add(ipGO);
                bucket.Add(ipGO);
            }
        }
    }

    private (HashSet<int> compsHit, bool[] compAnchored) BuildWallsGraphAndHits(Room room, Vector2 A, Vector2 B)
    {
        int n = room.wallLines.Count;
        bool[] anchored = new bool[n];
        List<int>[] adj = new List<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();

        for (int i = 0; i < n; i++)
        {
            var wl = room.wallLines[i];
            Vector2 s = new(wl.start.x, wl.start.z);
            Vector2 e = new(wl.end.x,   wl.end.z);
            anchored[i] = OnBoundary(room, s, EDGE_EPS) || OnBoundary(room, e, EDGE_EPS);
        }

        for (int i = 0; i < n; i++)
        {
            var wi = room.wallLines[i];
            Vector2 a1 = new(wi.start.x, wi.start.z);
            Vector2 a2 = new(wi.end.x,   wi.end.z);
            for (int j = i + 1; j < n; j++)
            {
                var wj = room.wallLines[j];
                Vector2 b1 = new(wj.start.x, wj.start.z);
                Vector2 b2 = new(wj.end.x,   wj.end.z);

                if (SegIntersectOrTouchEps(a1, a2, b1, b2, EDGE_EPS, out _)
                    || (a1 - b1).sqrMagnitude < DUP_EPS2 || (a1 - b2).sqrMagnitude < DUP_EPS2
                    || (a2 - b1).sqrMagnitude < DUP_EPS2 || (a2 - b2).sqrMagnitude < DUP_EPS2)
                {
                    adj[i].Add(j);
                    adj[j].Add(i);
                }
            }
        }

        int[] comp = new int[n];
        Array.Fill(comp, -1);
        List<bool> compHasAnchor = new();

        int cid = 0;
        for (int i = 0; i < n; i++)
        {
            if (comp[i] != -1) continue;
            bool hasAnchor = false;
            Stack<int> st = new(); st.Push(i);
            comp[i] = cid;
            while (st.Count > 0)
            {
                int u = st.Pop();
                if (anchored[u]) hasAnchor = true;
                foreach (var v in adj[u])
                    if (comp[v] == -1) { comp[v] = cid; st.Push(v); }
            }
            compHasAnchor.Add(hasAnchor);
            cid++;
        }

        HashSet<int> hitComps = new();
        for (int i = 0; i < n; i++)
        {
            var wl = room.wallLines[i];
            Vector2 s = new(wl.start.x, wl.start.z);
            Vector2 e = new(wl.end.x,   wl.end.z);
            if (SegIntersectOrTouchEps(A, B, s, e, EDGE_EPS, out _))
                hitComps.Add(comp[i]);
        }

        return (hitComps, compHasAnchor.ToArray());
    }

    private bool ShouldSplitSegment(Room room, Vector2 A, Vector2 B, bool A_onBoundary, bool B_onBoundary)
    {
        var (compsHit, compAnchored) = BuildWallsGraphAndHits(room, A, B);
        bool hitAnyAnchoredComp   = compsHit.Any(c => compAnchored[c]);
        int anchoredCompsHitCount = compsHit.Where(c => compAnchored[c]).Distinct().Count();

        return  (A_onBoundary && B_onBoundary)
            || ((A_onBoundary || B_onBoundary) && hitAnyAnchoredComp)
            || (anchoredCompsHitCount >= 2);
    }

    private IEnumerator WaitAndSplitRooms(List<Room> rooms)
    {
        yield return null; // chờ 1 frame

        foreach (var r in rooms)
            splitRoomManager.DetectAndSplitRoomIfNecessary(r);

        RedrawAllRooms();
    }

    private void InsertPointIntoWall(Room room, Vector2 point)
    {
        List<(int index, WallLine wall)> toSplit = new List<(int, WallLine)>();

        for (int i = 0; i < room.wallLines.Count; i++)
        {
            var wall = room.wallLines[i];
            Vector2 w1 = new Vector2(wall.start.x, wall.start.z);
            Vector2 w2 = new Vector2(wall.end.x, wall.end.z);

            if (PointOnSegment(point, w1, w2, 0.001f))
            {
                toSplit.Add((i, wall));
            }
        }

        // Cắt tất cả line tìm được (theo thứ tự ngược để tránh index lệch)
        for (int j = toSplit.Count - 1; j >= 0; j--)
        {
            var (index, wall) = toSplit[j];
            Vector3 point3D = new Vector3(point.x, 0, point.y);

            // Thêm checkpoint nếu chưa có
            if (!room.checkpoints.Any(p => Vector2.Distance(p, point) < 0.001f))
                room.checkpoints.Add(point);

            WallLine firstHalf = new WallLine { start = wall.start, end = point3D, type = wall.type };
            WallLine secondHalf = new WallLine { start = point3D, end = wall.end, type = wall.type };

            room.wallLines.RemoveAt(index);
            room.wallLines.Insert(index, secondHalf);
            room.wallLines.Insert(index, firstHalf);
        }

        // Lưu và vẽ lại
        if (toSplit.Count > 0)
        {
            RoomStorage.UpdateOrAddRoom(room);
            RedrawAllRooms();
        }
    }

    // Kiểm tra point nằm trên đoạn
    private bool PointOnSegment(Vector2 p, Vector2 a, Vector2 b, float tolerance)
    {
        float cross = Mathf.Abs((p.y - a.y) * (b.x - a.x) - (p.x - a.x) * (b.y - a.y));
        if (cross > tolerance) return false;

        float dot = (p.x - a.x) * (b.x - a.x) + (p.y - a.y) * (b.y - a.y);
        if (dot < 0) return false;

        float sqLen = (b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y);
        if (dot > sqLen) return false;

        return true;
    }

    private bool PointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        int crossings = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            if (((a.y > point.y) != (b.y > point.y)) &&
                (point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y + 1e-6f) + a.x))
                crossings++;
        }
        return (crossings % 2 == 1);
    }

    private List<Vector2> GetLinePolygonIntersections(Vector2 a, Vector2 b, List<Vector2> polygon)
    {
        List<Vector2> intersections = new();
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 p1 = polygon[i];
            Vector2 p2 = polygon[(i + 1) % polygon.Count];
            if (LineSegmentsIntersect(a, b, p1, p2, out Vector2 ip))
            {
                if (!intersections.Any(p => Vector2.Distance(p, ip) < 0.001f))
                    intersections.Add(ip);
            }
        }
        return intersections;
    }

    public static bool LineSegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, out Vector2 intersection)
    {
        intersection = Vector2.zero;

        float A1 = p2.y - p1.y;
        float B1 = p1.x - p2.x;
        float C1 = A1 * p1.x + B1 * p1.y;

        float A2 = q2.y - q1.y;
        float B2 = q1.x - q2.x;
        float C2 = A2 * q1.x + B2 * q1.y;

        float delta = A1 * B2 - A2 * B1;
        if (Mathf.Abs(delta) < 0.0001f) return false; // Song song

        float x = (B2 * C1 - B1 * C2) / delta;
        float y = (A1 * C2 - A2 * C1) / delta;

        Vector2 r = new Vector2(x, y);
        if (IsPointOnSegment(r, p1, p2) && IsPointOnSegment(r, q1, q2))
        {
            intersection = r;
            return true;
        }

        return false;
    }
    
    public static bool IsPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        return Mathf.Min(a.x, b.x) - 0.001f <= p.x && p.x <= Mathf.Max(a.x, b.x) + 0.001f &&
               Mathf.Min(a.y, b.y) - 0.001f <= p.y && p.y <= Mathf.Max(a.y, b.y) + 0.001f;
    }

    private string FindRoomIDByPoint(Vector3 worldPos)
    {
        Vector2 point2D = new Vector2(worldPos.x, worldPos.z);
        foreach (Room room in RoomStorage.rooms)
        {
            if (IsPointInPolygon(point2D, room.checkpoints))
            {
                return room.ID;
            }
        }

        return null;
    }

    // Hàm kiểm tra điểm có nằm trong polygon (ray casting algorithm)
    private bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        int j = polygon.Count - 1;
        bool inside = false;

        for (int i = 0; i < polygon.Count; j = i++)
        {
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) /
                (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public void InsertDoorOrWindow(Vector3 clickPosition, LineType type)
    {
        if (firstDoorPoint == null)
        {
            // Lần click đầu tiên: chọn đoạn wall gần nhất
            float minDist = float.MaxValue;
            selectedRoomForDoor = null;
            selectedWallLineForDoor = null;

            foreach (Room room in RoomStorage.rooms)
            {
                foreach (var wl in room.wallLines)
                {
                    if (wl.type != LineType.Wall) continue; // chỉ chọn từ tường thường

                    Vector3 projected = ProjectPointOnLineSegment(wl.start, wl.end, clickPosition);
                    float dist = Vector3.Distance(clickPosition, projected);

                    if (dist < minDist)
                    {
                        minDist = dist;
                        selectedRoomForDoor = room;
                        selectedWallLineForDoor = wl;
                        firstDoorPoint = projected;
                    }
                }
            }

            if (selectedWallLineForDoor == null)
            {
                Debug.LogWarning("Không tìm thấy đoạn tường phù hợp.");
                firstDoorPoint = null;
                return;
            }

            // Hiển thị preview
            GameObject p1Obj = Instantiate(checkpointPrefab, firstDoorPoint.Value, Quaternion.identity);
            p1Obj.name = $"{type}_P1_PREVIEW";

            Debug.Log($"[InsertDoorOrWindow] Đã chọn P1: {firstDoorPoint}");
            return;
        }
        else
        {
            // Lần click thứ 2: xác định đoạn cửa
            Vector3 p1 = firstDoorPoint.Value;
            Vector3 p2 = ProjectPointOnLineSegment(selectedWallLineForDoor.start, selectedWallLineForDoor.end,
                clickPosition);

            if (Vector3.Distance(p1, p2) < 0.01f)
            {
                Debug.LogWarning("P2 trùng P1, không hợp lệ.");
                return;
            }

            // Xoá P1_PREVIEW nếu còn
            var preview = GameObject.Find($"{type}_P1_PREVIEW");
            if (preview != null) Destroy(preview);

            // Tạo WallLine mới cho cửa/cửa sổ
            WallLine door = new WallLine(p1, p2, type);
            selectedRoomForDoor.wallLines.Add(door);

            GameObject p1Obj = Instantiate(checkpointPrefab, p1, Quaternion.identity);
            GameObject p2Obj = Instantiate(checkpointPrefab, p2, Quaternion.identity);
            p1Obj.name = $"{type}_P1";
            p2Obj.name = $"{type}_P2";

            if (!tempDoorWindowPoints.ContainsKey(selectedRoomForDoor.ID))
                tempDoorWindowPoints[selectedRoomForDoor.ID] = new List<(WallLine, GameObject, GameObject)>();

            tempDoorWindowPoints[selectedRoomForDoor.ID].Add((door, p1Obj, p2Obj));

            Debug.Log($"[InsertDoorOrWindow] Đã thêm {type}: {p1} -> {p2}");

            string roomID = selectedRoomForDoor.ID;

            // Reset
            firstDoorPoint = null;
            selectedWallLineForDoor = null;
            selectedRoomForDoor = null;

            RedrawAllRooms();

            // Cập nhật lại mesh sàn (dù checkpoint không đổi, vẫn gọi vì có thể cần render lại mặt sàn)
            var floorGO = GameObject.Find($"RoomFloor_{roomID}");
            if (floorGO != null)
            {
                var meshCtrl = floorGO.GetComponent<RoomMeshController>();
                if (meshCtrl != null)
                    meshCtrl.GenerateMesh(RoomStorage.GetRoomByID(roomID).checkpoints);
            }
            else
            {
                Debug.LogWarning($"Không tìm thấy RoomFloor_{roomID} để cập nhật mesh.");
            }
        }
    }

    public Vector3 ProjectPointOnLineSegment(Vector3 a, Vector3 b, Vector3 point)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(point - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        return a + t * ab;
    }

    public void RedrawAllRooms()
    {
        DrawingTool.ClearAllLines();

        foreach (Room room in RoomStorage.rooms)
        {
            foreach (var wl in room.wallLines)
            {
                DrawingTool.currentLineType = wl.type;
                DrawingTool.DrawLineAndDistance(wl.start, wl.end);
            }
        }
    }

    bool TrySelectCheckpoint(Vector3 position)
    {
        float minDistance = closeThreshold;
        GameObject nearestCheckpoint = null;

        foreach (var loop in allCheckpoints)
        {
            foreach (var checkpoint in loop)
            {
                if (checkpoint == null) continue;

                float distance = Vector3.Distance(checkpoint.transform.position, position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestCheckpoint = checkpoint;
                }
            }
        }

        if (!isClosedLoop)
        {
            foreach (var checkpoint in currentCheckpoints)
            {
                if (checkpoint == null) continue;

                float distance = Vector3.Distance(checkpoint.transform.position, position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestCheckpoint = checkpoint;
                }
            }
        }

        foreach (var kvp in tempDoorWindowPoints)
        {
            foreach (var (line, p1GO, p2GO) in kvp.Value)
            {
                if (p1GO != null)
                {
                    float dist1 = Vector3.Distance(p1GO.transform.position, position);
                    if (dist1 < minDistance)
                    {
                        minDistance = dist1;
                        nearestCheckpoint = p1GO;
                    }
                }

                if (p2GO != null)
                {
                    float dist2 = Vector3.Distance(p2GO.transform.position, position);
                    if (dist2 < minDistance)
                    {
                        minDistance = dist2;
                        nearestCheckpoint = p2GO;
                    }
                }
            }
        }

        if (nearestCheckpoint != null)
        {
            selectedCheckpoint = nearestCheckpoint;
            return true;
        }

        return false;
    }

    public void ToggleConnectionBetweenCheckpoints(GameObject pointA, GameObject pointB)
    {
        Vector3 start = pointA.transform.position;
        Vector3 end = pointB.transform.position;

        string roomID = FindRoomIDByPoint(start);
        if (string.IsNullOrEmpty(roomID)) return;

        if (!RoomFloorMap.TryGetValue(roomID, out GameObject floorGO)) return;
        Room room = RoomStorage.GetRoomByID(roomID);
        if (room == null) return;

        // Kiểm tra đã tồn tại line chưa
        WallLine existingLine = room.wallLines.FirstOrDefault(w =>
            (Vector3.Distance(w.start, start) < 0.01f && Vector3.Distance(w.end, end) < 0.01f) ||
            (Vector3.Distance(w.start, end) < 0.01f && Vector3.Distance(w.end, start) < 0.01f)
        );

        if (existingLine != null)
        {
            float length = Vector3.Distance(existingLine.start, existingLine.end);

            if (length > 0.01f)
            {
                room.wallLines.Remove(existingLine);
                Debug.Log($"[Disconnect] Gỡ nối {pointA.name} ↔ {pointB.name}");
            }
            else
            {
                Debug.LogWarning($"[GIỮ LẠI] Không gỡ vì line = {length:F2} ➜ giữ kết nối.");
            }
        }
        else
        {
            WallLine line = new WallLine(start, end, LineType.Wall);
            room.wallLines.Add(line);
            Debug.Log($"[Connect] Nối {pointA.name} ↔ {pointB.name}");
        }

        RoomStorage.UpdateOrAddRoom(room);
        DrawingTool.ClearAllLines();
        RedrawAllRooms();

        splitRoomManager.DetectAndSplitRoomIfNecessary(room);
    }

    public string FindRoomIDForLoop(List<GameObject> loop)
    {
        foreach (var mapping in loopMappings)
        {
            if (ReferenceEquals(mapping.CheckpointsGO, loop)) return mapping.RoomID;
        }

        Debug.LogWarning("Loop không tìm thấy RoomID!");
        return null;
    }

    public void DeselectCheckpoint()
    {
        selectedCheckpoint = null;
        isMovingCheckpoint = false;
    }

    public Vector3 GetWorldPositionFromScreen(Vector3 screenPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // Mặt phẳng ngang y=0
        float distance;
        if (groundPlane.Raycast(ray, out distance))
        {
            return ray.GetPoint(distance);
        }

        return ray.GetPoint(5f);
    }

    // === Load points from RoomStorage
    void LoadPointsFromRoomStorage()
    {
        var rooms = RoomStorage.rooms;
        if (rooms.Count == 0)
        {
            Debug.Log("Không có Room nào để hiển thị.");
            return;
        }

        foreach (var room in rooms)
        {
            // === Tạo lại checkpoint GameObject từ room.checkpoints
            List<GameObject> loopGO = new List<GameObject>();
            foreach (var pt in room.checkpoints)
            {
                Vector3 worldPos = new Vector3(pt.x, 0, pt.y);
                GameObject cp = Instantiate(checkpointPrefab, worldPos, Quaternion.identity);
                loopGO.Add(cp);
            }

            // === Lưu vào ánh xạ checkpoint<->RoomID
            allCheckpoints.Add(loopGO);
            loopMappings.Add(new LoopMap(room.ID, loopGO));

            // === Tạo lại mesh sàn (có thể drag)
            GameObject floorGO = new GameObject($"RoomFloor_{room.ID}");
            floorGO.transform.position = Vector3.zero;
            floorGO.transform.rotation = Quaternion.identity;
            floorGO.transform.localScale = Vector3.one;
            var meshCtrl = floorGO.AddComponent<RoomMeshController>();
            meshCtrl.Initialize(room.ID); // tự gọi GenerateMesh(room.checkpoints)

            // === Vẽ lại các wallLines
            foreach (var wl in room.wallLines)
            {
                DrawingTool.currentLineType = wl.type;
                DrawingTool.DrawLineAndDistance(wl.start, wl.end);

                // Nếu là cửa hoặc cửa sổ: tạo 2 điểm đầu/cuối riêng
                if (wl.type == LineType.Door || wl.type == LineType.Window)
                {
                    GameObject p1 = Instantiate(checkpointPrefab, wl.start, Quaternion.identity);
                    GameObject p2 = Instantiate(checkpointPrefab, wl.end, Quaternion.identity);
                    p1.name = $"{wl.type}_P1";
                    p2.name = $"{wl.type}_P2";

                    if (!tempDoorWindowPoints.ContainsKey(room.ID))
                        tempDoorWindowPoints[room.ID] = new List<(WallLine, GameObject, GameObject)>();

                    tempDoorWindowPoints[room.ID].Add((wl, p1, p2));
                }
            }
        }

        Debug.Log($"[LoadPointsFromRoomStorage] Đã load lại {rooms.Count} phòng, {allCheckpoints.Count} loop.");
    }

    void ShowIncompleteLoopPopup()
    {
        // PopupController.Show(
        //     "Mạch chưa khép kín!\nBạn muốn xóa dữ liệu vẽ tạm không?",
        //     onYes: () =>
        //     {
        //         Debug.Log("Người dùng chọn YES: Xóa toàn bộ checkpoint + line.");
        //         DeleteCurrentDrawingData();
        //     },
        //     onNo: () =>
        //     {
        //         Debug.Log("Người dùng chọn NO: Tiếp tục vẽ để khép kín.");
        //     }
        // );
        //
        var popup = Instantiate(ModularPopup.Prefab);
        popup.AutoFindCanvasAndSetup();
        popup.Header = "Mạch chưa khép kín!\\nBạn muốn xóa dữ liệu vẽ tạm không?";
        popup.ClickYesEvent = () =>
        {
            Debug.Log("Người dùng chọn YES: Xóa toàn bộ checkpoint + line.");
            DeleteCurrentDrawingData();
        };
        popup.ClickNoEvent = () => { Debug.Log("Người dùng chọn NO: Tiếp tục vẽ để khép kín."); };
        // popup.EventWhenClickButtons = () => { BackgroundUI.Instance.Hide(); };
        // BackgroundUI.Instance.Show(popup.gameObject, null);

        popup.autoClearWhenClick = true;
    }

    public void DeleteCurrentDrawingData()
    {
        foreach (var cp in currentCheckpoints)
        {
            if (cp != null)
                Destroy(cp);
        }

        currentCheckpoints.Clear();

        wallLines.Clear();
        DrawingTool.ClearAllLines();

        isClosedLoop = false;
        previewCheckpoint = null;
        selectedCheckpoint = null;

        foreach (var list in tempDoorWindowPoints.Values)
        {
            foreach (var (line, p1, p2) in list)
            {
                if (p1 != null) Destroy(p1);
                if (p2 != null) Destroy(p2);
            }
        }

        tempDoorWindowPoints.Clear();

        Debug.Log("Đã xóa toàn bộ dữ liệu vẽ chưa khép kín.");
    }

    public string GetSelectedRoomID()
    {
        if (selectedCheckpoint != null)
        {
            foreach (var loop in allCheckpoints)
            {
                if (loop.Contains(selectedCheckpoint))
                {
                    lastSelectedRoomID = FindRoomIDForLoop(loop);
                    return lastSelectedRoomID;
                }
            }
        }

        // Nếu đang kéo mesh ➜ lấy RoomID từ RoomMeshController đang hoạt động
        if (IsDraggingRoom)
        {
            var activeFloors = GameObject.FindObjectsByType<RoomMeshController>(FindObjectsSortMode.None);
            foreach (var floor in activeFloors)
            {
                if (floor.isDragging) // đã gán từ RoomMeshController
                {
                    lastSelectedRoomID = floor.RoomID;
                    return lastSelectedRoomID;
                }
            }
        }

        // Nếu đang không chọn gì nhưng vẫn có room đã chọn trước đó → giữ nguyên
        return lastSelectedRoomID;
    }
    public void ClearSelectedRoom()
    {
        lastSelectedRoomID = null;
        selectedCheckpoint = null;
        IsDraggingRoom = false;
    }
    public void CreateRectangleRoom(float width, float height, Vector3 center, string ID, bool isCreateCommand)
    {
        DeleteCurrentDrawingData();

        Vector3 p1 = new Vector3(center.x - width / 2, 0, center.z - height / 2);
        Vector3 p2 = new Vector3(center.x - width / 2, 0, center.z + height / 2);
        Vector3 p3 = new Vector3(center.x + width / 2, 0, center.z + height / 2);
        Vector3 p4 = new Vector3(center.x + width / 2, 0, center.z - height / 2);

        List<Vector3> corners = new List<Vector3> { p1, p2, p3, p4 };

        // Tạo checkpoint prefab tại từng góc
        CreateCheckPointGameObject(corners);

        // Tạo wallLines & vẽ line
        for (int i = 0; i < currentCheckpoints.Count; i++)
        {
            Vector3 start = currentCheckpoints[i].transform.position;
            Vector3 end = (i == currentCheckpoints.Count - 1)
                ? currentCheckpoints[0].transform.position
                : currentCheckpoints[i + 1].transform.position;

            DrawingTool.DrawLineAndDistance(start, end);
            wallLines.Add(new WallLine(start, end, LineType.Wall));
        }

        // Tạo Room & lưu
        Room newRoom = new Room();
        if (!string.IsNullOrEmpty(ID))
        {
            newRoom.SetID(ID);
        }

        foreach (GameObject cp in currentCheckpoints)
        {
            Vector3 pos = cp.transform.position;
            newRoom.checkpoints.Add(new Vector2(pos.x, pos.z));
        }

        if (MeshGenerator.CalculateArea(newRoom.checkpoints) > 0)
        {
            newRoom.checkpoints.Reverse();
            Debug.Log("Đã đảo chiều polygon để mesh đúng mặt.");
        }

        newRoom.wallLines.AddRange(wallLines);

        RoomStorage.rooms.Add(newRoom);

        // Tạo mesh sàn
        CreateRoomMeshCtrl(newRoom, center);

        // Ánh xạ loop
        AddGameObjectCheckPointToGlobalVariable(newRoom.ID, currentCheckpoints);

        currentCheckpoints.Clear();
        wallLines.Clear();

        DrawingTool.DrawAllLinesFromRoomStorage();
        Debug.Log($"Đã tạo Room hình chữ nhật: {width} x {height} m, RoomID: {newRoom.ID}");

        if (!isCreateCommand) return;

        var data = new RectangularCreatingData();
        data.width = width;
        data.heigh = height;
        data.RoomID = newRoom.ID;
        data.position = center;
        UndoRedoController.Instance.AddToUndo(new CreateRectangularCommand(data));
    }

    public void CreateRoomByRoomData(Room room,Vector3 position)
    {
        // chuyển đổi list sang vector3
        Debug.Log("Create Room by room data: "+room.ID);
        var convertList = new List<Vector3>();
        foreach(var item in room.checkpoints)
        {
            Vector3 pos = new Vector3(item.x, 0, item.y);
            convertList.Add(pos);
        }
        // tạo check dạng game object
        CreateCheckPointGameObject(convertList);
    
        // vẽ line dữa theo wall line
        foreach (WallLine item in room.wallLines)
        {
            Debug.Log($"Start {item.start} End{item.end}");
            DrawingTool.DrawLineAndDistance(item.start, item.end);
        }
        // đảm bảo data trong command độc lập với data runtime
        RoomStorage.rooms.Add(new Room(room));

        // thêm list game object check point vào global data
        AddGameObjectCheckPointToGlobalVariable(room.ID, currentCheckpoints);
        // init floor mesh 
        CreateRoomMeshCtrl(room,position);
        
        
        currentCheckpoints.Clear();
        wallLines.Clear();
        
        DrawingTool.DrawAllLinesFromRoomStorage();
    }

    private void CreateCheckPointGameObject(List<Vector3> corners)
    {
        foreach (Vector3 pos in corners)
        {
            var cp = Instantiate(checkpointPrefab, pos, Quaternion.identity);
            currentCheckpoints.Add(cp);
        }
    }

    private void AddGameObjectCheckPointToGlobalVariable(string roomID,List<GameObject> checkPoints)
    {
        List<GameObject> loopRef = new List<GameObject>(checkPoints);
        allCheckpoints.Add(loopRef);
        loopMappings.Add(new LoopMap(roomID, loopRef));
    }

    private void CreateRoomMeshCtrl(Room newRoom,Vector3 position)
    {
        GameObject floorGO = new GameObject($"RoomFloor_{newRoom.ID}");
        RoomMeshController meshCtrl = floorGO.AddComponent<RoomMeshController>();
        meshCtrl.Initialize(newRoom.ID);
        floorGO.transform.position = position;
    }

}